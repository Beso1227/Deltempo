using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace WinTempCleaner.Services;

public static partial class MemoryOptimizerService
{
    #region WinMemoryCleaner Constants & Structs

    private const string SeProfSingleProcessName = "SeProfileSingleProcessPrivilege";
    private const string SeIncreaseQuotaName = "SeIncreaseQuotaPrivilege";
    private const string SeDebugName = "SeDebugPrivilege";

    private const int PrivilegeAttributeEnabled = 2;
    private const int TokenAdjustPrivileges = 0x0020;
    private const int TokenQuery = 0x0008;

    // AdjustTokenPrivileges returns TRUE even when only some privileges were
    // adjusted; ERROR_NOT_ALL_ASSIGNED (1300) is reported via GetLastError.
    private const int ERROR_NOT_ALL_ASSIGNED = 1300;

    // NTSTATUS values (ntstatus.h). NtSetSystemInformation returns these directly.
    private const int STATUS_SUCCESS = 0x00000000;
    private const int STATUS_NOT_IMPLEMENTED = unchecked((int)0xC0000002); // -1073741822
    private const int STATUS_ACCESS_DENIED = unchecked((int)0xC0000022);     // -1073741518
    private const int STATUS_PRIVILEGE_NOT_HELD = unchecked((int)0xC0000061); // -1073741471
    private const int STATUS_INVALID_INFO_CLASS = unchecked((int)0xC0000003); // -1073741821
    private const int STATUS_NOT_SUPPORTED = unchecked((int)0xC00000BB);      // -1073741637
    private const int STATUS_UNSUCCESSFUL = unchecked((int)0xC0000001);      // -1073741823

    // Win32 errors we care about, folded into the NTSTATUS space so callers only have to
    // deal with one vocabulary. This mirrors the kernel's NTSTATUS_FROM_WIN32 macro:
    //   0xC0000000 | (win32 & 0xFFFF)
    // NOTE: error 5 therefore becomes 0xC0000005, which is NOT the same value as
    // STATUS_ACCESS_DENIED (0xC0000022). The two are mapped separately below.
    private const int ERROR_ACCESS_DENIED_WIN32 = 5;
    private const int ERROR_PRIVILEGE_NOT_HELD_WIN32 = 1314;

    private static int StatusFromWin32(int win32) =>
        unchecked((int)0xC0000000 | (win32 & 0xFFFF));

    // NT System Information Classes
    private const int SystemFileCacheInformation = 21; // 0x15
    private const int SystemMemoryListInformation = 80; // 0x50
    private const int SystemCombinePhysicalMemoryInformation = 130; // 0x82
    private const int SystemRegistryReconciliationInformation = 155; // 0x9B

    // NT System Memory List Commands
    private const int MemoryEmptyWorkingSets = 2;
    private const int MemoryFlushModifiedList = 3;
    private const int MemoryPurgeStandbyList = 4;
    private const int MemoryPurgeLowPriorityStandbyList = 5;

    // Volume & Drive IOCTLs
    private const int FsctlDiscardVolumeCache = 589828; // 0x00090054
    private const int IoControlResetWriteOrder = 589832; // 0x000900F8
    private const int FlagsNoBuffering = 536870912; // 0x20000000

    private const int ProcessQueryInformation = 0x0400;
    private const int ProcessSetQuota = 0x0100;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TokenPrivileges
    {
        public int Count;
        public long Luid;
        public int Attr;
    }

    // MEMORY_COMBINE_INFORMATION_EX (winnt.h):
    //   PVOID BaseAddress; PVOID PagesCombined; ULONG Flags;
    // Flags is a 32-bit ULONG, not a 64-bit field. It must be zero. Both declarations put
    // Flags at offset 16, so the declared width only affects total size (20 vs 24).
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MemoryCombineInformationEx
    {
        public IntPtr Handle;
        public IntPtr PagesCombined;
        public int Flags;
    }

    // SYSTEM_FILE_CACHE_INFORMATION, 32-bit view (winnt.h): nine ULONG fields, 36 bytes.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SystemFileCacheInformation32
    {
        public int CurrentSize;
        public int PeakSize;
        public int PageFaultCount;
        public int MinimumWorkingSet;
        public int PeakWorkingSet;
        public int CurrentSizeIncludingTransitionInPages;
        public int PeakSizeIncludingTransitionInPages;
        public int TransitionRePurposeCount;
        public int Flags;
    }

    // SYSTEM_FILE_CACHE_INFORMATION, 64-bit view (winnt.h):
    //   ULONG CurrentSize; ULONG PeakSize; ULONG PageFaultCount;
    //   SIZE_T MinimumWorkingSet; SIZE_T PeakWorkingSet;
    //   SIZE_T CurrentSizeIncludingTransitionInPages; SIZE_T PeakSizeIncludingTransitionInPages;
    //   ULONG TransitionRePurposeCount; SIZE_T Flags;
    //
    // The first three fields and TransitionRePurposeCount are 32-bit ULONGs, and Flags is a
    // 64-bit SIZE_T. Declaring them as `long`/`int` respectively produced a 68-byte buffer with
    // MinimumWorkingSet at offset 24 instead of 16, so the kernel read the caller's PeakSize field
    // as the minimum working set. Natural (unpacked) sequential layout reproduces the C layout
    // exactly: 4 + 4 pad + 8*6 + 4 + 4 pad + 8 = 64 bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemFileCacheInformation64
    {
        public uint CurrentSize;
        public uint PeakSize;
        public uint PageFaultCount;
        public long MinimumWorkingSet;
        public long PeakWorkingSet;
        public long CurrentSizeIncludingTransitionInPages;
        public long PeakSizeIncludingTransitionInPages;
        public uint TransitionRePurposeCount;
        public long Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MemoryStatusEx
    {
        public int Length;
        public int MemoryLoad;
        public long TotalPhys;
        public long AvailPhys;
        public long TotalPageFile;
        public long AvailPageFile;
        public long TotalVirtual;
        public long AvailVirtual;
        public long AvailExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public uint cb;
        public UIntPtr CommitTotal;
        public UIntPtr CommitLimit;
        public UIntPtr CommitPeak;
        public UIntPtr PhysicalTotal;
        public UIntPtr PhysicalAvailable;
        public UIntPtr SystemCache;
        public UIntPtr KernelTotal;
        public UIntPtr KernelPaged;
        public UIntPtr KernelNonpaged;
        public UIntPtr PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    #endregion

    #region WinMemoryCleaner P/Invoke Signatures

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TokenPrivileges newState,
        int bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetSystemInformation(
        int SystemInformationClass,
        IntPtr SystemInformation,
        uint SystemInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemFileCacheSize(
        IntPtr minimumFileCacheSize,
        IntPtr maximumFileCacheSize,
        int flags);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
        FileAccess dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        int dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        int dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

    #endregion
}
