using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Enforces comprehensive file management, backup, restore, ownership, and debug privileges for Deltempo.
/// Grants the application the highest native authority to perform cleanup tasks without access-denied restrictions.
/// </summary>
public static class ProcessPrivilegeService
{
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
    private const string SeDebugName = "SeDebugPrivilege";

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TokenPrivileges NewState,
        int BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static bool _privilegesEnabled;
    private static readonly object LockObj = new();

    public static void EnableRequiredPrivileges()
    {
        if (_privilegesEnabled) return;
        lock (LockObj)
        {
            if (_privilegesEnabled) return;
            SetIncreasePrivilege(SeBackupName);
            SetIncreasePrivilege(SeRestoreName);
            SetIncreasePrivilege(SeTakeOwnershipName);
            SetIncreasePrivilege(SeSecurityName);
            SetIncreasePrivilege(SeDebugName);
            _privilegesEnabled = true;
        }
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
            Trace.WriteLine($"[ProcessPrivilegeService] Privilege escalation for {privilegeName} failed: {ex.Message}");
        }
        return false;
    }
}
