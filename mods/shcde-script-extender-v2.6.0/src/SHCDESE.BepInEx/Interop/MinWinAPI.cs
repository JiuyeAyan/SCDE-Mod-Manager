using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SHCDESE.Interop;

public static class MinWinAPI
{

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr LoadLibraryA(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    public static IntPtr GetProcAddressEx(IntPtr hModule, string procName)
    {
        IntPtr result = MinWinAPI.GetProcAddress(hModule, procName);
        if (result == IntPtr.Zero)
            LogHelper.Warning($"GetProcAddressProxy: {hModule.ToString("X")}::{procName} -> returned null");

        return result;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);


    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

    [DllImport("user32.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int MessageBoxA(IntPtr hWnd, string lpText, string lpCaption, uint uType);
    public const uint MB_YESNO = 0x00000004;
    public const uint MB_ICONWARNING = 0x00000030;
    public const uint MB_DEFBUTTON2 = 0x00000100;
    public const int IDYES = 6;

    [StructLayout(LayoutKind.Sequential)]
    public struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    public const uint MEM_FREE = 0x10000;
    public const uint MEM_COMMIT = 0x1000;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_EXECUTE_READ = 0x20;
    public const uint PAGE_READONLY = 0x02;
    public const uint PAGE_NOACCESS = 0x01;

    public const uint MEM_IMAGE = 0x1000000;
    public const uint MEM_MAPPED = 0x40000;
    public const uint MEM_PRIVATE = 0x20000;

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EVENT
    {
        public DebugEventType dwDebugEventCode;     // Type of the debug event
        public uint dwProcessId;                    // Process ID where the event occurred
        public uint dwThreadId;                     // Thread ID where the event occurred
        public DebugEventUnion u;                   // Union holding event-specific information
    }

    public enum DebugEventType : uint
    {
        EXCEPTION_DEBUG_EVENT = 1,
        CREATE_THREAD_DEBUG_EVENT = 2,
        CREATE_PROCESS_DEBUG_EVENT = 3,
        EXIT_THREAD_DEBUG_EVENT = 4,
        EXIT_PROCESS_DEBUG_EVENT = 5,
        LOAD_DLL_DEBUG_EVENT = 6,
        UNLOAD_DLL_DEBUG_EVENT = 7,
        OUTPUT_DEBUG_STRING_EVENT = 8,
        RIP_EVENT = 9
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DebugEventUnion
    {
        [FieldOffset(0)]
        public EXCEPTION_DEBUG_INFO Exception;

        [FieldOffset(0)]
        public CREATE_THREAD_DEBUG_INFO CreateThread;

        [FieldOffset(0)]
        public CREATE_PROCESS_DEBUG_INFO CreateProcess;

        [FieldOffset(0)]
        public EXIT_THREAD_DEBUG_INFO ExitThread;

        [FieldOffset(0)]
        public EXIT_PROCESS_DEBUG_INFO ExitProcess;

        [FieldOffset(0)]
        public LOAD_DLL_DEBUG_INFO LoadDll;

        [FieldOffset(0)]
        public UNLOAD_DLL_DEBUG_INFO UnloadDll;

        [FieldOffset(0)]
        public OUTPUT_DEBUG_STRING_INFO DebugString;

        [FieldOffset(0)]
        public RIP_INFO RipInfo;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct EXCEPTION_DEBUG_INFO
    {
        public EXCEPTION_RECORD ExceptionRecord;
        public uint dwFirstChance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREATE_THREAD_DEBUG_INFO
    {
        public IntPtr hThread;
        public IntPtr lpThreadLocalBase;
        public IntPtr lpStartAddress;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREATE_PROCESS_DEBUG_INFO
    {
        public IntPtr hFile;
        public IntPtr hProcess;
        public IntPtr hThread;
        public IntPtr lpBaseOfImage;
        public uint dwDebugInfoFileOffset;
        public uint nDebugInfoSize;
        public IntPtr lpThreadLocalBase;
        public IntPtr lpStartAddress;
        public IntPtr lpImageName;
        public ushort fUnicode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EXIT_THREAD_DEBUG_INFO
    {
        public uint dwExitCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EXIT_PROCESS_DEBUG_INFO
    {
        public uint dwExitCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LOAD_DLL_DEBUG_INFO
    {
        public IntPtr hFile;
        public IntPtr lpBaseOfDll;
        public uint dwDebugInfoFileOffset;
        public uint nDebugInfoSize;
        public IntPtr lpImageName;
        public ushort fUnicode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UNLOAD_DLL_DEBUG_INFO
    {
        public IntPtr lpBaseOfDll;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OUTPUT_DEBUG_STRING_INFO
    {
        public IntPtr lpDebugStringData;
        public ushort fUnicode;
        public ushort nDebugStringLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RIP_INFO
    {
        public uint dwError;
        public uint dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EXCEPTION_RECORD
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public IntPtr ExceptionRecord;
        public IntPtr ExceptionAddress;
        public uint NumberParameters;
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        //public IntPtr[] ExceptionInformation;
        public IntPtr ExceptionInformation1;
        public IntPtr ExceptionInformation2;
        public IntPtr ExceptionInformation3;
        public IntPtr ExceptionInformation4;
        public IntPtr ExceptionInformation5;
        public IntPtr ExceptionInformation6;
        public IntPtr ExceptionInformation7;
        public IntPtr ExceptionInformation8;
        public IntPtr ExceptionInformation9;
        public IntPtr ExceptionInformation10;
        public IntPtr ExceptionInformation11;
        public IntPtr ExceptionInformation12;
        public IntPtr ExceptionInformation13;
        public IntPtr ExceptionInformation14;
        public IntPtr ExceptionInformation15;
    }


    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    public static string ProtectionToString(uint protect)
    {
        return protect switch
        {
            0x01 => "---",                  // PAGE_NOACCESS
            0x02 => "R--",                  // PAGE_READONLY
            0x04 => "RW-",                  // PAGE_READWRITE
            0x08 => "RWC",                  // PAGE_WRITECOPY
            0x10 => "E--",                  // PAGE_EXECUTE
            0x20 => "ER-",                  // PAGE_EXECUTE_READ
            0x40 => "ERW",                  // PAGE_EXECUTE_READWRITE
            0x80 => "ERWC",                 // PAGE_EXECUTE_WRITECOPY
            _ => "???"
        };
    }

    public static string TypeToString(uint type)
    {
        return type switch
        {
            MEM_IMAGE => "IMG",
            MEM_MAPPED => "MAP",
            MEM_PRIVATE => "PRV",
            _ => "---"
        };
    }
    const uint CF_UNICODETEXT = 13;
    const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GlobalUnlock(IntPtr hMem);

    public static void SetClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            EmptyClipboard();

            // Allocate global memory for string
            int bytes = (text.Length + 1) * 2; // Unicode = 2 bytes per char
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                // Copy string into allocated memory
                byte[] buffer = Encoding.Unicode.GetBytes(text + "\0");
                Marshal.Copy(buffer, 0, target, buffer.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            CloseClipboard();
        }
    }

}
