using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TechnoVerseLoader.Services
{
    public sealed class CheeseLoadResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
    }

    public static class CheeseHookLoader
    {
        private const string TargetWindowClass = "VALORANTUnrealWindow";
        private const uint DontResolveDllReferences = 0x00000001;
        private const int WhGetMessage = 3;
        private const uint WmNull = 0x0000;
        private const int ImageDirectoryEntryExport = 0;

        private static IntPtr _hookHandle = IntPtr.Zero;
        private static IntPtr _dllModule = IntPtr.Zero;
        private static HookProc? _hookProc;
        private static bool _isLoaded;
        private static string _loadedDllPath = "";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern IntPtr ImageDirectoryEntryToData(
            IntPtr hModule, bool mappedAsImage, ushort directoryEntry, out uint size);

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        public static bool IsLoaded => _isLoaded;

        public static CheeseLoadResult LoadModule(string dllPath)
        {
            if (_isLoaded)
            {
                return new CheeseLoadResult
                {
                    Success = false,
                    Message = "Cheese module is already loaded. Logout first to reload."
                };
            }

            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
            {
                return Fail("DLL file not found.");
            }

            string? exportName = GetExportedFunctionName(dllPath);
            if (string.IsNullOrEmpty(exportName))
            {
                return Fail("Could not find an exported hook function in the DLL.");
            }

            IntPtr targetWindow = FindWindow(TargetWindowClass, null);
            if (targetWindow == IntPtr.Zero)
            {
                return Fail("Target window not found. Open VALORANT first (VALORANTUnrealWindow).");
            }

            uint threadId = GetWindowThreadProcessId(targetWindow, out _);
            if (threadId == 0)
            {
                return Fail("Could not resolve the target process thread.");
            }

            _dllModule = LoadLibraryEx(dllPath, IntPtr.Zero, DontResolveDllReferences);
            if (_dllModule == IntPtr.Zero)
            {
                return Fail($"LoadLibraryEx failed: {GetWin32Message()}");
            }

            IntPtr procAddress = GetProcAddress(_dllModule, exportName);
            if (procAddress == IntPtr.Zero)
            {
                FreeLibrary(_dllModule);
                _dllModule = IntPtr.Zero;
                return Fail($"GetProcAddress failed for export '{exportName}': {GetWin32Message()}");
            }

            _hookProc = Marshal.GetDelegateForFunctionPointer<HookProc>(procAddress);
            _hookHandle = SetWindowsHookEx(WhGetMessage, _hookProc, _dllModule, threadId);
            if (_hookHandle == IntPtr.Zero)
            {
                FreeLibrary(_dllModule);
                _dllModule = IntPtr.Zero;
                return Fail($"SetWindowsHookEx failed: {GetWin32Message()}");
            }

            PostThreadMessage(threadId, WmNull, IntPtr.Zero, IntPtr.Zero);
            _isLoaded = true;
            _loadedDllPath = dllPath;

            return new CheeseLoadResult
            {
                Success = true,
                Message = "Loaded"
            };
        }

        public static bool UnloadModule()
        {
            bool success = true;

            if (_hookHandle != IntPtr.Zero && !UnhookWindowsHookEx(_hookHandle))
            {
                success = false;
            }

            if (_dllModule != IntPtr.Zero && !FreeLibrary(_dllModule))
            {
                success = false;
            }

            _hookHandle = IntPtr.Zero;
            _dllModule = IntPtr.Zero;
            _hookProc = null;
            _isLoaded = false;

            if (!string.IsNullOrEmpty(_loadedDllPath))
            {
                try
                {
                    if (File.Exists(_loadedDllPath))
                    {
                        File.SetAttributes(_loadedDllPath, FileAttributes.Normal);
                        File.Delete(_loadedDllPath);
                    }
                }
                catch { }
                _loadedDllPath = "";
            }

            return success;
        }

        public static void WatchAndCleanOnExit(string dllPath)
        {
            _ = Task.Run(async () =>
            {
                // Cho den khi game tat
                while (IsTargetRunning())
                {
                    await Task.Delay(2000);
                }

                // Khi game tat thi unhook va xoa file dll ngay
                UnloadModule();
            });
        }

        public static bool IsTargetRunning()
        {
            return FindWindow(TargetWindowClass, null) != IntPtr.Zero;
        }

        private static CheeseLoadResult Fail(string message)
        {
            return new CheeseLoadResult { Success = false, Message = message };
        }

        private static string GetWin32Message()
        {
            int error = Marshal.GetLastWin32Error();
            return error == 0 ? "Unknown error" : new Win32Exception(error).Message;
        }

        private static string? GetExportedFunctionName(string dllPath)
        {
            IntPtr module = LoadLibraryEx(dllPath, IntPtr.Zero, DontResolveDllReferences);
            if (module == IntPtr.Zero)
                return null;

            try
            {
                IntPtr exportDirPtr = ImageDirectoryEntryToData(
                    module, true, ImageDirectoryEntryExport, out uint dirSize);

                if (exportDirPtr == IntPtr.Zero || dirSize < Marshal.SizeOf<ImageExportDirectory>())
                    return null;

                var exportDir = Marshal.PtrToStructure<ImageExportDirectory>(exportDirPtr);

                IntPtr namesRva = IntPtr.Add(module, (int)exportDir.AddressOfNames);
                IntPtr ordinalsRva = IntPtr.Add(module, (int)exportDir.AddressOfNameOrdinals);
                IntPtr functionsRva = IntPtr.Add(module, (int)exportDir.AddressOfFunctions);

                for (uint i = 0; i < exportDir.NumberOfNames; i++)
                {
                    uint nameRva = (uint)Marshal.ReadInt32(namesRva, (int)(i * 4));
                    string name = ReadNullTerminatedAnsiString(IntPtr.Add(module, (int)nameRva));

                    if (!string.Equals(name, "DllMain", StringComparison.OrdinalIgnoreCase))
                        return name;
                }

                return null;
            }
            finally
            {
                FreeLibrary(module);
            }
        }

        private static string ReadNullTerminatedAnsiString(IntPtr address)
        {
            var bytes = new StringBuilder();
            int offset = 0;
            while (true)
            {
                byte b = Marshal.ReadByte(address, offset++);
                if (b == 0) break;
                bytes.Append((char)b);
            }
            return bytes.ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ImageExportDirectory
        {
            public uint Characteristics;
            public uint TimeDateStamp;
            public ushort MajorVersion;
            public ushort MinorVersion;
            public uint Name;
            public uint Base;
            public uint NumberOfFunctions;
            public uint NumberOfNames;
            public uint AddressOfFunctions;
            public uint AddressOfNames;
            public uint AddressOfNameOrdinals;
        }
    }
}
