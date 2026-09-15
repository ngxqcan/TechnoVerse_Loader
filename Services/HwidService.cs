using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace TechnoVerseLoader.Services
{
    public static class HwidService
    {
        private static string? _cachedHwid;

        public static string GetHwid()
        {
            if (!string.IsNullOrEmpty(_cachedHwid))
                return _cachedHwid;

            var sb = new StringBuilder();

            // 1. Windows MachineGuid từ Registry (rất ổn định)
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key != null)
                {
                    object? guid = key.GetValue("MachineGuid");
                    if (guid != null)
                    {
                        sb.Append(guid.ToString()).Append("|");
                    }
                }
            }
            catch { }

            // 2. CPU Processor ID qua WMI
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var item in searcher.Get())
                {
                    string? id = item["ProcessorId"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        sb.Append(id).Append("|");
                        break;
                    }
                }
            }
            catch { }

            // 3. Motherboard Serial qua WMI
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var item in searcher.Get())
                {
                    string? serial = item["SerialNumber"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(serial))
                    {
                        sb.Append(serial).Append("|");
                        break;
                    }
                }
            }
            catch { }

            // 4. BIOS Serial qua WMI
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
                foreach (var item in searcher.Get())
                {
                    string? biosSerial = item["SerialNumber"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(biosSerial))
                    {
                        sb.Append(biosSerial).Append("|");
                        break;
                    }
                }
            }
            catch { }

            // 5. Volume Serial Number ổ hệ thống
            try
            {
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var driveInfo = new DriveInfo(systemDrive);
                sb.Append(driveInfo.VolumeLabel).Append("|");
            }
            catch { }

            string raw = sb.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = Environment.MachineName + "_" + Environment.UserName;
            }

            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            string hex = Convert.ToHexString(hashBytes).ToUpperInvariant();

            // Định dạng TV-XXXX-XXXX-XXXX-XXXX cho ngắn gọn và thẩm mỹ
            _cachedHwid = $"TV-{hex.Substring(0, 8)}-{hex.Substring(8, 8)}-{hex.Substring(16, 8)}";
            return _cachedHwid;
        }
    }
}
