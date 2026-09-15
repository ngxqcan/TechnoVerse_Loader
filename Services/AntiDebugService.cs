using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using TechnoVerseLoader.Config;

namespace TechnoVerseLoader.Services
{
    public static class AntiDebugService
    {
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        private static readonly string[] BlacklistedProcesses =
        {
            "x64dbg", "x32dbg", "x96dbg",
            "ida", "ida64", "idag", "idag64",
            "dnspy", "dnspy.console", "de4dot",
            "cheatengine", "cheatengine-x86_64", "cheatengine-i386",
            "fiddler", "wireshark",
            "httpdebugger", "httpdebuggerui",
            "processhacker", "procmon", "procmon64",
            "scylla", "ghidra", "megadumper",
            "proxifier", "proxifier32", "proxifier64", "prxhandler",
            "charles", "burp", "burpsuite", "mitmproxy", "reqable",
            "tcpview", "netlimiter", "simpleproxy"
        };

        private static readonly string[] BlacklistedWindowTitles =
        {
            "x64dbg", "x32dbg", "dnspy", "cheat engine",
            "ida pro", "http debugger", "process hacker",
            "scylla", "wireshark", "de4dot",
            "proxifier", "charles proxy", "burp suite",
            "mitmproxy", "reqable", "tcpview"
        };

        private static bool _isTerminating;
        private static Thread? _monitorThread;
        private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null
        })
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        public static void StartMonitoring()
        {
            // Initial check
            CheckAndEnforce();

            // Background continuous check
            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "AntiDebugMonitor"
            };
            _monitorThread.Start();
        }

        private static void MonitorLoop()
        {
            while (!_isTerminating)
            {
                try
                {
                    CheckAndEnforce();
                }
                catch
                {
                    // Transient errors ignored
                }

                Thread.Sleep(2000);
            }
        }

        public static void CheckAndEnforce()
        {
            if (_isTerminating) return;

            string? detectedReason = PerformCheck();
            if (!string.IsNullOrEmpty(detectedReason))
            {
                TriggerKill(detectedReason);
            }
        }

        private static string? PerformCheck()
        {
            // 1. Managed Debugger (.NET)
            if (Debugger.IsAttached)
            {
                return "Managed Debugger Attached (.NET Debugger)";
            }

            // 2. Native IsDebuggerPresent
            if (IsDebuggerPresent())
            {
                return "Native Debugger Present (Kernel32.IsDebuggerPresent)";
            }

            // 3. Remote Debugger
            try
            {
                bool isRemote = false;
                if (CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemote) && isRemote)
                {
                    return "Remote Debugger Attached (Kernel32.CheckRemoteDebuggerPresent)";
                }
            }
            catch { }

            // 4. Blacklisted processes and window titles (with proper Process.Dispose cleanup)
            Process[]? processes = null;
            try
            {
                processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        string name = proc.ProcessName.ToLowerInvariant();
                        foreach (var target in BlacklistedProcesses)
                        {
                            if (name.Contains(target))
                            {
                                return $"Blacklisted Tool Detected: {proc.ProcessName}.exe (PID: {proc.Id})";
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                        {
                            string title = proc.MainWindowTitle.ToLowerInvariant();
                            foreach (var target in BlacklistedWindowTitles)
                            {
                                if (title.Contains(target))
                                {
                                    return $"Blacklisted Window Title Detected: '{proc.MainWindowTitle}' ({proc.ProcessName}.exe)";
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Access denied on protected system processes is normal
                    }
                }
            }
            catch { }
            finally
            {
                if (processes != null)
                {
                    foreach (var p in processes)
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }

            // 5. Injected Sniffing / Hooking DLLs
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                foreach (ProcessModule mod in currentProc.Modules)
                {
                    string modName = mod.ModuleName.ToLowerInvariant();
                    if (modName.Contains("prxdrv") || modName.Contains("proxifier") ||
                        modName.Contains("httpdebugger") || modName.Contains("easyhook") ||
                        modName.Contains("minhook"))
                    {
                        return $"Injected Proxy / Hook Module Detected: {mod.ModuleName}";
                    }
                }
            }
            catch { }

            return null;
        }

        private static void TriggerKill(string reason)
        {
            if (_isTerminating) return;
            _isTerminating = true;

            try
            {
                var config = AppConfig.Load();
                string webhookUrl = config.SecurityWebhookUrl;

                if (!string.IsNullOrWhiteSpace(webhookUrl))
                {
                    SendWebhookNotification(webhookUrl, reason, config);
                }
            }
            catch { }

            // Force exit process immediately
            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch
            {
                Environment.Exit(0);
            }
        }

        private static void SendWebhookNotification(string webhookUrl, string reason, AppConfig config)
        {
            try
            {
                string hwid = HwidService.GetHwid();
                string userInfo = !string.IsNullOrWhiteSpace(config.SavedDiscordId)
                    ? $"{config.SavedUsername} ({config.SavedDiscordId})"
                    : "Not logged in";

                var embed = new
                {
                    username = "TechnoVerse Sentinel",
                    embeds = new[]
                    {
                        new
                        {
                            title = "Security Alert: Debug / Crack Attempt Detected",
                            description = "A reverse engineering / traffic sniffing attempt was detected on client machine.",
                            color = 15548997,
                            fields = new[]
                            {
                                new { name = "Threat Reason", value = reason, @inline = false },
                                new { name = "User", value = userInfo, @inline = true },
                                new { name = "HWID", value = hwid, @inline = true },
                                new { name = "Computer", value = Environment.MachineName, @inline = true },
                                new { name = "OS Version", value = Environment.OSVersion.ToString(), @inline = true },
                                new { name = "Time (UTC)", value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), @inline = true }
                            },
                            footer = new
                            {
                                text = "TechnoVerse Anti-Tamper Protection"
                            },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                string json = JsonSerializer.Serialize(embed);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                _ = HttpClient.PostAsync(webhookUrl, content).GetAwaiter().GetResult();
            }
            catch
            {
                // Network failure ignored during kill
            }
        }
    }
}
