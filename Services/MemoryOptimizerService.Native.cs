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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MemoryCombineInformationEx
    {
        public IntPtr Handle;
        public IntPtr PagesCombined;
        public long Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SystemFileCacheInformation32
    {
        public int CurrentSize;
        public int PeakSize;
        public int PageFaultCount;
        public int MinimumWorkingSet;
        public int MaximumWorkingSet;
        public int CurrentSizeIncludingTransitionInPages;
        public int PeakSizeIncludingTransitionInPages;
        public int TransitionRePurposeCount;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SystemFileCacheInformation64
    {
        public long CurrentSize;
        public long PeakSize;
        public long PageFaultCount;
        public long MinimumWorkingSet;
        public long MaximumWorkingSet;
        public long CurrentSizeIncludingTransitionInPages;
        public long PeakSizeIncludingTransitionInPages;
        public long TransitionRePurposeCount;
        public int Flags;
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
