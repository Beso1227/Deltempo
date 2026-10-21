using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Services;

public partial class CleanerService
{
    #region Native Windows Shell & Kernel APIs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFileW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveDirectoryW(string lpPathName);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TokenPrivileges NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TokenPrivileges
    {
        public int Count;
        public long Luid;
        public int Attr;
    }

    private const int PrivilegeAttributeEnabled = 2;
    private const int TokenAdjustPrivileges = 0x0020;
    private const int TokenQuery = 0x0008;

    private const string SeBackupName = "SeBackupPrivilege";
    private const string SeRestoreName = "SeRestorePrivilege";
    private const string SeTakeOwnershipName = "SeTakeOwnershipPrivilege";
    private const string SeSecurityName = "SeSecurityPrivilege";

    public static void EnableFileManagementPrivileges()
    {
        SetIncreasePrivilege(SeBackupName);
        SetIncreasePrivilege(SeRestoreName);
        SetIncreasePrivilege(SeTakeOwnershipName);
        SetIncreasePrivilege(SeSecurityName);
    }

    private static bool SetIncreasePrivilege(string privilegeName)
    {
        try
        {
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out IntPtr tokenHandle))
            {
                try
                {
                    var tp = new TokenPrivileges { Count = 1, Attr = PrivilegeAttributeEnabled };
                    if (LookupPrivilegeValue(null, privilegeName, ref tp.Luid))
                    {
                        return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Deltempo.Cleaner] Privilege escalation for {privilegeName} failed: {ex.Message}");
        }
        return false;
    }

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    #endregion
}
