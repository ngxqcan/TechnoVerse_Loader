using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TechnoVerseLoader.Config;

namespace TechnoVerseLoader.Services
{
    public class DownloadProgressArgs
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public int Percentage { get; set; }
        public double SpeedKbps { get; set; }
        public string StatusText { get; set; } = "";
    }

    public class DownloadLaunchService
    {
        private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        static DownloadLaunchService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TechnoVerse-Loader/1.0");
        }

        public async Task<string> DownloadPayloadAsync(
            string downloadUrl,
            string productKey,
            string fileName,
            string expectedSha256,
            IProgress<DownloadProgressArgs>? progress,
            CancellationToken cancellationToken = default,
            string? payloadPassword = null)
        {
            bool isEmulator = string.Equals(productKey, "emulator-restart", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(productKey, "emulator-no-restart", StringComparison.OrdinalIgnoreCase) ||
                              productKey.Contains("emulator", StringComparison.OrdinalIgnoreCase);

            bool isStealth = isEmulator ||
                             fileName.Contains("2PC", StringComparison.OrdinalIgnoreCase) ||
                             fileName.Contains("vgc", StringComparison.OrdinalIgnoreCase);

            if (isEmulator || isStealth)
            {
                KillEmulatorProcesses();
            }

            string productFolder;
            string targetFileName = fileName;

            if (isStealth)
            {
                // Thư mục ẩn sâu trong System Cache để khách hàng không biết file payload
                productFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TechnoVerse",
                    ".cache",
                    "sys_bin"
                );
                targetFileName = string.Equals(productKey, "emulator-no-restart", StringComparison.OrdinalIgnoreCase)
                    ? "vgc_emu.exe"
                    : "vgc_helper.exe";

                // Xóa thư mục Payloads lộ thiên cũ nếu có
                try
                {
                    string oldDir = Path.Combine(AppConfig.GetPayloadsDirectory(), SanitizeFolderName(productKey));
                    if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
                }
                catch { }
            }
            else
            {
                productFolder = Path.Combine(AppConfig.GetPayloadsDirectory(), SanitizeFolderName(productKey));
            }

            if (!Directory.Exists(productFolder))
            {
                Directory.CreateDirectory(productFolder);
            }

            if (isStealth)
            {
                try
                {
                    File.SetAttributes(productFolder, FileAttributes.Hidden);
                }
                catch { }
            }

            string targetFilePath = Path.Combine(productFolder, targetFileName);
            string hashMarkerFile = Path.Combine(productFolder, $".{targetFileName}.sha");

            // Reset file attributes on existing files if stealth previously set hidden attributes
            if (File.Exists(targetFilePath)) try { File.SetAttributes(targetFilePath, FileAttributes.Normal); } catch { }
            if (File.Exists(hashMarkerFile)) try { File.SetAttributes(hashMarkerFile, FileAttributes.Normal); } catch { }

            // Kiểm tra cache: Nếu file đã có và hash SHA256 trùng khớp
            bool isEncryptedBin = fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);
            if (!isEncryptedBin && File.Exists(targetFilePath) && !string.IsNullOrWhiteSpace(expectedSha256))
            {
                string existingHash = CalculateFileSha256(targetFilePath);
                if (string.Equals(existingHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (isStealth)
                    {
                        try { File.SetAttributes(targetFilePath, FileAttributes.Hidden); } catch { }
                    }
                    progress?.Report(new DownloadProgressArgs
                    {
                        BytesReceived = new FileInfo(targetFilePath).Length,
                        TotalBytes = new FileInfo(targetFilePath).Length,
                        Percentage = 100,
                        StatusText = "File up to date."
                    });
                    return targetFilePath;
                }
            }
            else if (isEncryptedBin && !string.IsNullOrWhiteSpace(expectedSha256))
            {
                // Đối với file mã hóa: nếu marker file trùng SHA-256 và file thực thi đã tồn tại -> Trả về ngay không cần tải lại 38MB
                string expectedExecutableName = isStealth
                    ? (string.Equals(productKey, "emulator-no-restart", StringComparison.OrdinalIgnoreCase) ? "vgc_emu.exe" : "vgc_helper.exe")
                    : "";

                string checkPath = !string.IsNullOrWhiteSpace(expectedExecutableName)
                    ? Path.Combine(productFolder, expectedExecutableName)
                    : "";

                if (File.Exists(hashMarkerFile))
                {
                    try
                    {
                        string savedSha = File.ReadAllText(hashMarkerFile).Trim();
                        if (string.Equals(savedSha, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            // Nếu đã xác định được file thực thi hoặc có ít nhất 1 file hợp lệ trong thư mục
                            if (!string.IsNullOrWhiteSpace(checkPath) && File.Exists(checkPath) && new FileInfo(checkPath).Length > 0)
                            {
                                progress?.Report(new DownloadProgressArgs
                                {
                                    BytesReceived = 100,
                                    TotalBytes = 100,
                                    Percentage = 100,
                                    StatusText = "File up to date."
                                });
                                return checkPath;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Tải về file tạm trước khi đổi tên
            string tempFile = targetFilePath + ".tmp";
            if (File.Exists(tempFile))
            {
                try { File.SetAttributes(tempFile, FileAttributes.Normal); File.Delete(tempFile); } catch { }
            }

            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                byte[] buffer = new byte[65536];
                long totalRead = 0;
                int bytesRead;
                var stopwatch = Stopwatch.StartNew();
                long lastBytesRead = 0;
                var lastTime = stopwatch.ElapsedMilliseconds;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    long now = stopwatch.ElapsedMilliseconds;
                    if (now - lastTime >= 150 || (totalBytes > 0 && totalRead == totalBytes))
                    {
                        double elapsedSec = (now - lastTime) / 1000.0;
                        double speedKbps = elapsedSec > 0 ? ((totalRead - lastBytesRead) / 1024.0) / elapsedSec : 0;
                        lastTime = now;
                        lastBytesRead = totalRead;

                        int percentage = totalBytes > 0 ? (int)((totalRead * 100) / totalBytes) : 0;

                        progress?.Report(new DownloadProgressArgs
                        {
                            BytesReceived = totalRead,
                            TotalBytes = totalBytes,
                            Percentage = percentage,
                            SpeedKbps = speedKbps,
                            StatusText = totalBytes > 0
                                ? $"Downloading: {FormatBytes(totalRead)} / {FormatBytes(totalBytes)} ({percentage}%)"
                                : $"Downloading: {FormatBytes(totalRead)}..."
                        });
                    }
                }
            }

            // Hoàn tất tải, ghi đè file đích an toàn
            SafeMoveFile(tempFile, targetFilePath);

            // Kiểm tra xem file tải về có phải là payload đã mã hóa (payload.bin / TVBIN1) không
            if (PayloadSecurityService.IsEncryptedPayloadFile(targetFilePath))
            {
                progress?.Report(new DownloadProgressArgs
                {
                    BytesReceived = totalBytes,
                    TotalBytes = totalBytes,
                    Percentage = 100,
                    StatusText = "Decrypting payload..."
                });

                byte[] encBytes = File.ReadAllBytes(targetFilePath);
                try { File.SetAttributes(targetFilePath, FileAttributes.Normal); File.Delete(targetFilePath); } catch { }

                var decResult = PayloadSecurityService.DecryptPayload(encBytes, payloadPassword);

                // Xác định tên file thực tế sau giải mã
                string realFileName = decResult.OriginalFileName;
                if (string.IsNullOrWhiteSpace(realFileName))
                {
                    realFileName = "payload" + (string.IsNullOrWhiteSpace(decResult.OriginalExtension) ? ".exe" : decResult.OriginalExtension);
                }

                // Nếu là emulator stealth, giữ tên vgc_emu.exe hoặc vgc_helper.exe trong thư mục cache
                if (isStealth)
                {
                    realFileName = string.Equals(productKey, "emulator-no-restart", StringComparison.OrdinalIgnoreCase)
                        ? "vgc_emu.exe"
                        : "vgc_helper.exe";
                }

                string finalDecryptedPath = Path.Combine(productFolder, realFileName);
                SafeWriteBytes(finalDecryptedPath, decResult.Data);
                targetFilePath = finalDecryptedPath;

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    try
                    {
                        if (File.Exists(hashMarkerFile)) try { File.SetAttributes(hashMarkerFile, FileAttributes.Normal); } catch { }
                        File.WriteAllText(hashMarkerFile, expectedSha256.Trim());
                        if (isStealth)
                        {
                            try { File.SetAttributes(hashMarkerFile, FileAttributes.Hidden); } catch { }
                        }
                    }
                    catch { }
                }
            }

            if (isStealth)
            {
                try
                {
                    File.SetAttributes(targetFilePath, FileAttributes.Hidden);
                }
                catch { }
            }

            progress?.Report(new DownloadProgressArgs
            {
                BytesReceived = totalBytes,
                TotalBytes = totalBytes,
                Percentage = 100,
                StatusText = "Download complete. Ready."
            });

            return targetFilePath;
        }

        public static void KillEmulatorProcesses()
        {
            // Direct taskkill on executable names for instant termination
            string[] directExeNames = new[] { "vgc_helper.exe", "vgc_emu.exe", "Techno Verse.exe", "ctxemu2.exe", "pipe_emulator.exe" };
            foreach (var exe in directExeNames)
            {
                try
                {
                    using var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /IM \"{exe}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    proc?.WaitForExit(300);
                }
                catch { }
            }

            // 1. Dùng taskkill với bộ lọc wildcard để diệt toàn bộ process tree kể cả khi tiến trình có đuôi lạ (như vgc_helper.exe\u00A0)
            string[] filters = new[]
            {
                "IMAGENAME eq vgc_helper*",
                "IMAGENAME eq vgc_emu*",
                "IMAGENAME eq TechnoVerse_2PC*",
                "IMAGENAME eq technoverse_2pc*",
                "IMAGENAME eq ctxemu2*",
                "IMAGENAME eq pipe_emulator*",
                "WINDOWTITLE eq *TechnoVerse Controller*",
                "WINDOWTITLE eq *Session Engine*",
                "WINDOWTITLE eq *Zaten Acik*"
            };

            foreach (var filter in filters)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /FI \"{filter}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(300);
                }
                catch { }
            }

            // 2. Quét qua .NET Process để triệt hạ theo đường dẫn thư mục sys_bin hoặc tên mờ
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string sysBinDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TechnoVerse", ".cache", "sys_bin"
                ).ToLowerInvariant();

                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id == currentPid) continue;

                        string pName = p.ProcessName;
                        string wTitle = "";
                        try { wTitle = p.MainWindowTitle; } catch { }

                        string? exePath = null;
                        try { exePath = p.MainModule?.FileName?.ToLowerInvariant(); } catch { }

                        bool isMatch = pName.Contains("vgc_helper", StringComparison.OrdinalIgnoreCase) ||
                                       pName.Contains("vgc_emu", StringComparison.OrdinalIgnoreCase) ||
                                       pName.Contains("TechnoVerse_2PC", StringComparison.OrdinalIgnoreCase) ||
                                       pName.Contains("ctxemu2", StringComparison.OrdinalIgnoreCase) ||
                                       pName.Contains("pipe_emulator", StringComparison.OrdinalIgnoreCase) ||
                                       wTitle.Contains("TechnoVerse Controller", StringComparison.OrdinalIgnoreCase) ||
                                       wTitle.Contains("Session Engine", StringComparison.OrdinalIgnoreCase) ||
                                       wTitle.Contains("Zaten Acik", StringComparison.OrdinalIgnoreCase) ||
                                       (!string.IsNullOrEmpty(exePath) && exePath.Contains(sysBinDir));

                        if (isMatch)
                        {
                            try { p.Kill(true); p.WaitForExit(300); }
                            catch { try { p.Kill(); } catch { } }
                        }
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch { }

            // Chờ để Windows OS giải phóng file lock, cổng mạng, Named Pipe và Mutex
            Thread.Sleep(500);
        }

        private static void SafeMoveFile(string src, string dst)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(dst))
                    {
                        try { File.SetAttributes(dst, FileAttributes.Normal); File.Delete(dst); } catch { }
                    }
                    File.Move(src, dst, overwrite: true);
                    return;
                }
                catch (Exception)
                {
                    KillEmulatorProcesses();
                    Thread.Sleep(250);
                    if (i == 9) throw;
                }
            }
        }

        private static void SafeWriteBytes(string dst, byte[] data)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(dst))
                    {
                        try { File.SetAttributes(dst, FileAttributes.Normal); File.Delete(dst); } catch { }
                    }
                    File.WriteAllBytes(dst, data);
                    return;
                }
                catch (Exception)
                {
                    KillEmulatorProcesses();
                    Thread.Sleep(250);
                    if (i == 9) throw;
                }
            }
        }

        public Process LaunchPayload(string filePath, string productKey = "")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Cannot find file to launch: " + filePath);
            }

            bool isEmulator = string.Equals(productKey, "emulator-restart", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(productKey, "emulator-no-restart", StringComparison.OrdinalIgnoreCase) ||
                              productKey.Contains("emulator", StringComparison.OrdinalIgnoreCase) ||
                              filePath.Contains("emulator", StringComparison.OrdinalIgnoreCase);

            bool isStealth = isEmulator ||
                             filePath.Contains("vgc_helper", StringComparison.OrdinalIgnoreCase) ||
                             filePath.Contains("vgc_emu", StringComparison.OrdinalIgnoreCase) ||
                             filePath.Contains("2PC", StringComparison.OrdinalIgnoreCase) ||
                             filePath.Contains("sys_bin", StringComparison.OrdinalIgnoreCase);

            if (isEmulator || isStealth)
            {
                KillEmulatorProcesses();
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string executableToRun = filePath;
            string workingDirectory = Path.GetDirectoryName(filePath) ?? "";

            // Nếu là file zip, tự động giải nén ra một thư mục riêng biệt trong Temp
            if (ext == ".zip")
            {
                string folderName = $"TV_{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:yyyyMMdd_HHmmss}";
                string extractFolder = Path.Combine(Path.GetTempPath(), folderName);
                if (!Directory.Exists(extractFolder))
                {
                    Directory.CreateDirectory(extractFolder);
                }

                ZipFile.ExtractToDirectory(filePath, extractFolder, true);

                // Ưu tiên 1: Tìm start.bat trong thư mục giải nén
                string startBat = Path.Combine(extractFolder, "start.bat");
                if (File.Exists(startBat))
                {
                    executableToRun = startBat;
                    workingDirectory = extractFolder;
                }
                else
                {
                    // Ưu tiên 2: Tìm file exe đầu tiên trong thư mục giải nén
                    string[] exeFiles = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories);
                    if (exeFiles.Length > 0)
                    {
                        executableToRun = exeFiles[0];
                        workingDirectory = Path.GetDirectoryName(executableToRun) ?? extractFolder;
                    }
                    else
                    {
                        // Ưu tiên 3: Tìm bất kỳ file .bat / .cmd nào
                        string[] batFiles = Directory.GetFiles(extractFolder, "*.bat", SearchOption.AllDirectories);
                        if (batFiles.Length > 0)
                        {
                            executableToRun = batFiles[0];
                            workingDirectory = Path.GetDirectoryName(batFiles[0]) ?? extractFolder;
                        }
                        else
                        {
                            throw new FileNotFoundException("No executable (.exe) or batch script (.bat) found in payload archive: " + extractFolder);
                        }
                    }
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executableToRun,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                Verb = "runas", // Chạy với quyền Administrator để hook driver và hiển thị cửa sổ
                WindowStyle = ProcessWindowStyle.Normal
            };

            var proc = Process.Start(startInfo);
            if (proc == null)
            {
                throw new InvalidOperationException("Failed to launch process!");
            }

            return proc;
        }

        public static string CalculateFileSha256(string filePath)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] hash = sha.ComputeHash(stream);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch
            {
                return "";
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F1} {sizes[order]}";
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
