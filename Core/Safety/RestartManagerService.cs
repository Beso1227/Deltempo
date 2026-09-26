using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Core.Safety;

public record LockingProcessInfo(
    int ProcessId,
    string ProcessName,
    string AppDescription);

/// <summary>
/// Native Windows Restart Manager & Boot Purge engine.
/// Queries rstrtmgr.dll to detect which active applications hold file locks on in-use temp caches,
/// and provides MoveFileExW registration for reboot-scheduled junk purges.
/// </summary>
public static class RestartManagerService
{
    #region Win32 P/Invoke Definitions

    private const int CCH_RM_MAX_APP_NAME = 255;
    private const int CCH_RM_MAX_SVC_NAME = 63;
    private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        string[] rgsFileNames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        out uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint dwSessionHandle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string lpExistingFileName, string? lpNewFileName, uint dwFlags);

    #endregion

    /// <summary>
    /// Fast non-invasive probe to test if a file handle is actively locked by another process with exclusive share.
    /// Returns true if a sharing violation or active handle lock prevents access.
    /// </summary>
    public static bool IsFileLocked(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException ex)
        {
            int hr = ex.HResult & 0xFFFF;
            if (hr == 32 || hr == 33) return true;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            var lockers = GetLockingProcesses(filePath);
            return lockers.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Inspects an active file lock using Windows Restart Manager and returns the holding processes.
    /// Returns empty list if file is unlocked or query cannot be fulfilled.
    /// </summary>
    public static List<LockingProcessInfo> GetLockingProcesses(string filePath)
    {
        var result = new List<LockingProcessInfo>();
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            return result;
        }

        string sessionKey = Guid.NewGuid().ToString();
        int res = RmStartSession(out uint handle, 0, sessionKey);
        if (res != 0) return result;

        try
        {
            string[] resources = [filePath];
            res = RmRegisterResources(handle, 1, resources, 0, null, 0, null);
            if (res != 0) return result;

            uint procInfoNeeded = 0;
            uint procInfoCount = 0;
            uint rebootReasons = 0;

            // First call to query count of affected processes
            res = RmGetList(handle, out procInfoNeeded, ref procInfoCount, null, out rebootReasons);
            if (res == 234 /* ERROR_MORE_DATA */ && procInfoNeeded > 0)
            {
                var processInfo = new RM_PROCESS_INFO[procInfoNeeded];
                procInfoCount = procInfoNeeded;

                res = RmGetList(handle, out procInfoNeeded, ref procInfoCount, processInfo, out rebootReasons);
                if (res == 0)
                {
                    for (int i = 0; i < procInfoCount; i++)
                    {
                        int pid = processInfo[i].Process.dwProcessId;
                        string appName = processInfo[i].strAppName;

                        string resolvedProcName = string.Empty;
                        try
                        {
                            using var proc = Process.GetProcessById(pid);
                            resolvedProcName = proc.ProcessName;
                        }
                        catch
                        {
                            resolvedProcName = !string.IsNullOrWhiteSpace(appName) ? appName : $"PID {pid}";
                        }

                        result.Add(new LockingProcessInfo(pid, resolvedProcName, appName));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RestartManager] Error querying lock for '{filePath}': {ex.Message}");
        }
        finally
        {
            RmEndSession(handle);
        }

        return result;
    }

    /// <summary>
    /// Schedules a locked file for automatic deletion on the next system reboot using Win32 MoveFileEx.
    /// Writes to HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations.
    /// Requires elevated administrator privileges.
    /// </summary>
    public static bool ScheduleRebootDeletion(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            return false;
        }

        try
        {
            return MoveFileExW(filePath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RestartManager] Failed scheduling reboot deletion for '{filePath}': {ex.Message}");
            return false;
        }
    }

    public static readonly HashSet<string> ProtectedSystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "smss", "csrss", "wininit", "services", "lsass", "svchost", "fontdrvhost",
        "winlogon", "explorer", "dwm", "sihost", "taskhostw", "RuntimeBroker", "SearchHost",
        "StartMenuExperienceHost", "ShellExperienceHost", "Deltempo", "deltempo_cli"
    };

    public static bool IsProtectedSystemProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        return ProtectedSystemProcessNames.Contains(processName);
    }
}
