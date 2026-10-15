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
    #region Scoped-query memo

    // Set for the duration of a BeginQueryScope. Keyed by the exact path asked about, so
    // a hit is only ever returned for the same file - which is what makes this safe where
    // session reuse was not.
    private static System.Collections.Concurrent.ConcurrentDictionary<string, List<LockingProcessInfo>>? QueryCache;

    // The scope currently in force, so a cache hit can be attributed to the run that
    // earned it. Per-scope counters stay exact even when several scans overlap.
    private static QueryScope? _currentScope;

    private static int _openScopes;

    // Diagnostics. The interesting ratio is queries ISSUED against queries SERVED FROM
    // CACHE: the saving is exactly the redundant repeat-asking the executor used to do.
    private static long _queriesIssued;
    private static long _queriesCached;

    /// <summary>Lock queries asked since process start.</summary>
    public static long QueriesIssued => Interlocked.Read(ref _queriesIssued);

    /// <summary>Lock queries answered from an active memo since process start.</summary>
    public static long QueriesServedFromCache => Interlocked.Read(ref _queriesCached);

    #endregion

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
            // Only a genuine sharing/lock violation means "locked". Any other IOException
            // (path too long, invalid device name, transient device error, directory instead
            // of file) is a FAILURE TO DETERMINE, not evidence of a lock. Reporting those as
            // locked fed the executor's "in use -> schedule for reboot deletion" branch, which
            // armed a MoveFileEx against a path we had never actually confirmed was busy.
            int hr = ex.HResult & 0xFFFF;
            if (hr == 32 /* ERROR_SHARING_VIOLATION */ || hr == 33 /* ERROR_LOCK_VIOLATION */)
            {
                return true;
            }

            Trace.WriteLine($"[RestartManager] IsFileLocked('{filePath}') inconclusive: {ex.Message}");
            return false;
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
    /// Memoizes lock queries for the duration of a bulk run, so a run costs one Restart
    /// Manager query per DISTINCT file rather than one per query site.
    /// </summary>
    /// <remarks>
    /// GetLockingProcesses opens a Restart Manager session per call, and start/end are real
    /// work inside the Restart Manager service - more expensive than the query itself. The
    /// executor asks about the SAME file up to three times on a failure path: it queries
    /// the lockers, calls IsFileLocked (which queries again on an access-denied), and then
    /// queries once more to attribute the lock for the user-facing message. All three ask
    /// an identical question about an identical path within a few milliseconds.
    ///
    /// So the redundancy is eliminated at the query level rather than at the session level.
    /// That distinction matters: an earlier attempt at this optimisation POOLED THE SESSIONS
    /// instead, re-registering each new path on a reused handle. RmRegisterResources does
    /// NOT replace the session's resource set - it accumulates. A subsequent query on a
    /// different, unlocked file therefore still reported the previous file's holder. That
    /// is not a performance defect, it is a SAFETY defect: it makes the app claim a file is
    /// held by a process that is not holding it, which arms a reboot deletion against it.
    /// A per-path memo cannot produce that error, because it only ever returns an answer
    /// about the path that was asked about.
    ///
    /// The cache is scoped, so it cannot go stale between runs: disposing the scope drops
    /// it, and only the outermost scope clears it.
    /// </remarks>
    public static QueryScope BeginQueryScope() => new();

    /// <summary>
    /// A bulk-run memo scope. Disposing it drops the memo; nested scopes are reference
    /// counted so only the outermost dispose releases anything.
    /// </summary>
    public sealed class QueryScope : IDisposable
    {
        private long _queriesServedFromCache;

        public QueryScope()
        {
            // A nested scope is free and shares the outer's state, so an inner dispose
            // cannot blank entries the outer run still needs.
            if (Interlocked.Increment(ref _openScopes) == 1)
            {
                _currentScope = this;
                QueryCache = new System.Collections.Concurrent.ConcurrentDictionary<string, List<LockingProcessInfo>>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Repeat asks about a path that were answered from the memo rather than by asking
        /// the Restart Manager again. Per-scope, so it is exact even while other scans run
        /// concurrently.
        /// </summary>
        public long QueriesServedFromCache => Interlocked.Read(ref _queriesServedFromCache);

        internal void NoteCacheHit() => Interlocked.Increment(ref _queriesServedFromCache);

        public void Dispose()
        {
            if (Interlocked.Decrement(ref _openScopes) > 0) return;
            QueryCache = null;
            _currentScope = null;
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

        // Inside a scope, the answer for a path is memoized. The executor asks about the
        // same file up to three times on a failure path within a few milliseconds, and a
        // cache keyed by the exact path cannot answer a question about a different file -
        // which is the property session reuse lacked.
        System.Collections.Concurrent.ConcurrentDictionary<string, List<LockingProcessInfo>>? cache = QueryCache;

        Interlocked.Increment(ref _queriesIssued);

        if (cache != null && cache.TryGetValue(filePath, out List<LockingProcessInfo>? cached))
        {
            _currentScope?.NoteCacheHit();
            Interlocked.Increment(ref _queriesCached);
            return cached;
        }

        string sessionKey = Guid.NewGuid().ToString();
        if (RmStartSession(out uint handle, 0, sessionKey) != 0)
        {
            return result;
        }

        // Set only when the Restart Manager actually answered, so an inconclusive query is
        // never memoized as "not locked".
        bool queryCompleted = false;

        try
        {
            string[] resources = [filePath];
            int res = RmRegisterResources(handle, 1, resources, 0, null, 0, null);
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

            // The query itself succeeded, whether or not anything holds the file. Both an
            // empty list and a populated list are real answers worth memoizing.
            queryCompleted = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RestartManager] Error querying lock for '{filePath}': {ex.Message}");
        }
        finally
        {
            RmEndSession(handle);
        }

        // Only a completed query is memoized. Caching the empty result of a query that
        // failed or was inconclusive would turn a transient Restart Manager hiccup into a
        // permanent "not locked" answer for the rest of the run - the same class of bug as
        // the AI cache pinning a file to an offline verdict.
        if (cache != null && queryCompleted)
        {
            cache[filePath] = result;
        }

        return result;
    }

    /// <summary>
    /// Schedules a locked file for automatic deletion on the next system reboot using Win32 MoveFileEx.
    /// Writes to HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations.
    /// Requires elevated administrator privileges.
    /// </summary>
    /// <remarks>
    /// This is the most dangerous primitive in the cleanup pipeline, because Windows executes
    /// the queued delete at Session Manager time - long after every safety check has run and
    /// the process is gone. Nothing revalidates at that point: a DIFFERENT file may occupy the
    /// path by then, and no ProtectionPolicy or reparse check is ever consulted.
    /// It therefore enforces its own guards rather than trusting every caller to have done so.
    /// A caller that reached here through a swallowed exception must not be able to arm a
    /// delete for a path the safety engine never cleared.
    /// </remarks>
    public static bool ScheduleRebootDeletion(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            return false;
        }

        // 1. Reject anything the centralized policy protects, or that is not canonically
        //    resolvable. NormalizeCanonicalPath returns empty for malformed/traversal input.
        string canonicalPath = PathSecurity.NormalizeCanonicalPath(filePath);
        if (string.IsNullOrEmpty(canonicalPath) || PathSecurity.HasPathTraversalSequences(filePath))
        {
            Trace.WriteLine($"[RestartManager] Refusing reboot deletion of non-canonical path '{filePath}'.");
            return false;
        }

        if (ProtectionPolicy.IsProtected(canonicalPath, out string protectedReason))
        {
            Trace.WriteLine($"[RestartManager] Refusing reboot deletion of protected path '{canonicalPath}': {protectedReason}");
            return false;
        }

        // 2. Refuse reparse points. A junction or symlink queued for deletion lets the
        //    Session Manager act on the link target, which is outside the approved scope.
        if (PathSecurity.IsReparsePointOrLink(canonicalPath))
        {
            Trace.WriteLine($"[RestartManager] Refusing reboot deletion of reparse point '{canonicalPath}'.");
            return false;
        }

        try
        {
            // Delete the path the guards just validated, not the raw caller-supplied string.
            return MoveFileExW(canonicalPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RestartManager] Failed scheduling reboot deletion for '{canonicalPath}': {ex.Message}");
            return false;
        }
    }

    public static readonly HashSet<string> ProtectedSystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "smss", "csrss", "wininit", "services", "lsass", "svchost", "fontdrvhost",
        "winlogon", "explorer", "dwm", "sihost", "taskhostw", "RuntimeBroker", "SearchHost",
        "StartMenuExperienceHost", "ShellExperienceHost", "Deltempo", "deltempo_cli"
    };

    public static readonly HashSet<string> DisposableBackgroundHelperProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "crashpad_handler", "GoogleUpdate", "MicrosoftEdgeUpdate", "identity_helper",
        "setup", "msiexec", "updater", "update", "browser_broker",
        "filecoauth", "microsoftedgesh"
    };

    public static bool IsProtectedSystemProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        return ProtectedSystemProcessNames.Contains(processName);
    }

    /// <summary>
    /// Attempts to terminate non-critical helper processes locking disposable files.
    /// Strictly protects critical Windows kernel, shell, and security processes.
    /// </summary>
    public static int TerminateLockingProcesses(string filePath, bool backgroundHelpersOnly = true)
    {
        var lockers = GetLockingProcesses(filePath);
        int terminated = 0;
        int currentPid = Environment.ProcessId;

        foreach (var locker in lockers)
        {
            if (locker.ProcessId <= 4 || locker.ProcessId == currentPid) continue;
            if (IsProtectedSystemProcess(locker.ProcessName)) continue;

            if (backgroundHelpersOnly && !DisposableBackgroundHelperProcesses.Contains(locker.ProcessName))
            {
                continue;
            }

            try
            {
                using var proc = Process.GetProcessById(locker.ProcessId);
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: false);
                    proc.WaitForExit(150);
                    terminated++;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[RestartManager] Failed terminating locker '{locker.ProcessName}' (PID {locker.ProcessId}): {ex.Message}");
            }
        }

        return terminated;
    }
}
