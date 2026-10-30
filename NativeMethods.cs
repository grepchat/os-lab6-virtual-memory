using System.Runtime.InteropServices;
using System.Text;

namespace VirtualMemoryLab;

/// <summary>
/// Объявления системных функций Win32 API (kernel32.dll) и структур,
/// используемых для исследования виртуальной памяти.
/// </summary>
internal static class NativeMethods
{
    // Архитектура процессора (SYSTEM_INFO.wProcessorArchitecture)
    public const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
    public const ushort PROCESSOR_ARCHITECTURE_MIPS = 1;
    public const ushort PROCESSOR_ARCHITECTURE_ALPHA = 2;
    public const ushort PROCESSOR_ARCHITECTURE_PPC = 3;
    public const ushort PROCESSOR_ARCHITECTURE_ARM = 5;
    public const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
    public const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
    public const ushort PROCESSOR_ARCHITECTURE_ARM64 = 12;

    // Состояние страниц (MEMORY_BASIC_INFORMATION.State) и тип операции VirtualAlloc/VirtualFree
    public const uint MEM_COMMIT = 0x00001000;
    public const uint MEM_RESERVE = 0x00002000;
    public const uint MEM_DECOMMIT = 0x00004000;
    public const uint MEM_RELEASE = 0x00008000;
    public const uint MEM_FREE = 0x00010000;

    // Тип физической памяти (MEMORY_BASIC_INFORMATION.Type)
    public const uint MEM_PRIVATE = 0x00020000;
    public const uint MEM_MAPPED = 0x00040000;
    public const uint MEM_IMAGE = 0x01000000;

    // Атрибуты защиты страниц (таблица 1 методических указаний)
    public const uint PAGE_NOACCESS = 0x01;
    public const uint PAGE_READONLY = 0x02;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_WRITECOPY = 0x08;
    public const uint PAGE_EXECUTE = 0x10;
    public const uint PAGE_EXECUTE_READ = 0x20;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    // Модификаторы атрибутов защиты
    public const uint PAGE_GUARD = 0x100;
    public const uint PAGE_NOCACHE = 0x200;
    public const uint PAGE_WRITECOMBINE = 0x400;

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public IntPtr lpMinimumApplicationAddress;
        public IntPtr lpMaximumApplicationAddress;
        public UIntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUS
    {
        public uint dwLength;        // sizeof(MEMORYSTATUS)
        public uint dwMemoryLoad;    // процент используемой памяти
        public UIntPtr dwTotalPhys;     // всего физической памяти, байт
        public UIntPtr dwAvailPhys;     // свободно физической памяти, байт
        public UIntPtr dwTotalPageFile; // всего памяти файла подкачки, байт
        public UIntPtr dwAvailPageFile; // свободно памяти файла подкачки, байт
        public UIntPtr dwTotalVirtual;  // размер пользовательской части адресного пространства
        public UIntPtr dwAvailVirtual;  // свободно в пользовательской части адресного пространства
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;       // базовый адрес группы страниц
        public IntPtr AllocationBase;    // базовый адрес региона
        public uint AllocationProtect;   // атрибут защиты при резервировании региона
        public UIntPtr RegionSize;       // размер группы смежных страниц, байт
        public uint State;               // MEM_FREE / MEM_RESERVE / MEM_COMMIT
        public uint Protect;             // текущий атрибут защиты
        public uint Type;                // MEM_IMAGE / MEM_MAPPED / MEM_PRIVATE
    }

    [DllImport("kernel32.dll")]
    public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll")]
    public static extern void GlobalMemoryStatus(ref MEMORYSTATUS lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr VirtualQuery(IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer, UIntPtr dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
        uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", EntryPoint = "K32GetMappedFileNameW", CharSet = CharSet.Unicode)]
    public static extern uint GetMappedFileName(IntPtr hProcess, IntPtr lpv,
        StringBuilder lpFilename, uint nSize);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();
}
