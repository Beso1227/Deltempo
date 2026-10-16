using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public enum MemoryTargetType
{
    WorkingSet,
    StandbyList,
    StandbyListLowPriority,
    ModifiedPageList,
    CombinedPageList,
    SystemFileCache,
    ModifiedFileCache,
    RegistryCache
}

public class MemoryAreaSnapshot
{
    public MemoryTargetType Target { get; set; }
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long CurrentBytes { get; set; }
    public double UsedPercent { get; set; }
    public bool IsAvailableOnThisOs { get; set; } = true;
    public bool IsSelected { get; set; } = true;
    public string SafetyBadge { get; set; } = "UNCATEGORIZED";

    public string FormattedCurrent => TargetFolderInfo.FormatBytes(CurrentBytes);
    public string FormattedTotal => TargetFolderInfo.FormatBytes(TotalBytes);
    public string FormattedFree => TargetFolderInfo.FormatBytes(FreeBytes);
    public string FormattedUsedPercent => $"{UsedPercent:F1}%";

    public string IconGlyph => Target switch
    {
        MemoryTargetType.WorkingSet => "\uE950", // memory chip
        MemoryTargetType.StandbyList => "\uE8B7", // package
        MemoryTargetType.StandbyListLowPriority => "\uE756", // app
        MemoryTargetType.ModifiedPageList => "\uE8A5", // document
        MemoryTargetType.CombinedPageList => "\uF012", // archive/combine
        MemoryTargetType.SystemFileCache => "\uEDA2", // storage drive
        MemoryTargetType.ModifiedFileCache => "\uE714", // file
        MemoryTargetType.RegistryCache => "\uE793", // registry
        _ => "\uE950"
    };

    // Severity is data, not a brush.
    //
    // This used to expose WPF SolidColorBrush properties, which forced Core to
    // compile against System.Windows.Media and made the WinUI 3 port unable to
    // consume this model at all. The 70/90 thresholds are design intent worth
    // keeping, so they live here as a level plus an ARGB hex string; each UI
    // resolves the hex with its own brush factory.
    public MemoryUsageSeverity Severity => UsedPercent switch
    {
        > 90 => MemoryUsageSeverity.High,
        > 70 => MemoryUsageSeverity.Elevated,
        _ => MemoryUsageSeverity.Normal
    };

    /// <summary>ARGB hex for <see cref="Severity"/>; opaque.</summary>
    public string SeverityColorHex => Severity switch
    {
        MemoryUsageSeverity.High => "#FFEF4444",     // red
        MemoryUsageSeverity.Elevated => "#FFF59E0B", // amber
        _ => "#FF00F2B0"                             // cyber mint
    };
}

/// <summary>Usage band for a memory area, so UIs can style without WPF types.</summary>
public enum MemoryUsageSeverity
{
    Normal,
    Elevated,
    High
}

public enum MemoryOperationStatus
{
    Success,
    PartialSuccess,
    NoApplicableTargets,
    AccessDenied,
    Unsupported,
    Failed
}

/// <summary>
/// Outcome of a memory optimization run.
///
/// Measurement semantics, kept deliberately distinct:
/// <list type="bullet">
/// <item><description><b>Requested</b> â€” <see cref="RequestedTargetCount"/> targets the caller asked for.</description></item>
/// <item><description><b>Confirmed</b> â€” <see cref="SucceededTargetCount"/> / <see cref="FailedTargetCount"/>, derived from the NTSTATUS each native call actually returned.</description></item>
/// <item><description><b>Measured</b> â€” <see cref="MeasuredAvailableDelta"/>, the observed change in
/// <c>GlobalMemoryStatusEx.ullAvailPhys</c> around the run.</description></item>
/// </list>
/// <para>
/// <see cref="MeasuredAvailableDelta"/> is <b>not</b> the amount of cache purged and is <b>not</b> a
/// performance measurement. Available physical memory already includes the standby cache, so discarding
/// the standby list can raise this figure while removing data Windows had cached to make file access fast.
/// A larger number here does not imply the machine will perform better; it may well perform slightly
/// worse until that cache is re-read from disk.
/// </para>
/// </summary>
public class MemoryOptimizationResult
{
    public MemoryOperationStatus Status { get; set; } = MemoryOperationStatus.Failed;
    public int ProcessesOptimized { get; set; }
    public long ExecutionTimeMs { get; set; }

    /// <summary>Available physical RAM sampled before the run, in bytes.</summary>
    public long MeasuredAvailableBefore { get; set; }

    /// <summary>Available physical RAM sampled after the run, in bytes.</summary>
    public long MeasuredAvailableAfter { get; set; }

    /// <summary>
    /// Observed change in available physical RAM, clamped at zero. Never negative, because a drop in
    /// available memory does not mean negative bytes were freed. Not a cache-purge total and not a
    /// performance score â€” see the remarks on <see cref="MemoryOptimizationResult"/>.
    /// </summary>
    public long MeasuredAvailableDelta => Math.Max(0, MeasuredAvailableAfter - MeasuredAvailableBefore);

    /// <summary>Number of targets the caller requested.</summary>
    public int RequestedTargetCount { get; set; }

    /// <summary>Number of targets whose native call reported success.</summary>
    public int SucceededTargetCount => AreaResults.Count(r => r.Success);

    /// <summary>Number of targets whose native call did not report success.</summary>
    public int FailedTargetCount => AreaResults.Count(r => !r.Success);

    public string FormattedMeasuredDelta => TargetFolderInfo.FormatBytes(MeasuredAvailableDelta);

    public List<MemoryAreaResult> AreaResults { get; set; } = new();
    public bool Success => Status is MemoryOperationStatus.Success or MemoryOperationStatus.PartialSuccess;
}

public class MemoryAreaResult
{
    public MemoryTargetType Target { get; set; }
    public MemoryOperationStatus Status { get; set; } = MemoryOperationStatus.Failed;
    public bool Success => Status is MemoryOperationStatus.Success or MemoryOperationStatus.PartialSuccess;
    public string ErrorMessage { get; set; } = "";
    public int? ProcessesOptimized { get; set; }
}

public class MemoryInfo
{
    public long TotalPhysicalBytes { get; set; }
    public long AvailablePhysicalBytes { get; set; }
    public long SystemCacheBytes { get; set; }
    public long CommitTotalBytes { get; set; }
    public long CommitLimitBytes { get; set; }
    public bool MeasurementSucceeded { get; set; }
    public long UsedPhysicalBytes => Math.Max(0, TotalPhysicalBytes - AvailablePhysicalBytes);
    public double UsedPercent => TotalPhysicalBytes > 0 ? (double)UsedPhysicalBytes / TotalPhysicalBytes * 100.0 : 0.0;
    public string FormattedUsed => TargetFolderInfo.FormatBytes(UsedPhysicalBytes);
    public string FormattedTotal => TargetFolderInfo.FormatBytes(TotalPhysicalBytes);
    public string FormattedAvailable => TargetFolderInfo.FormatBytes(AvailablePhysicalBytes);
    public string FormattedSystemCache => TargetFolderInfo.FormatBytes(SystemCacheBytes);
}

/// <summary>
/// Authentic Windows NT Kernel Memory Cleaner Engine (Directly ported from IgorMundstein/WinMemoryCleaner).
/// Interfaces with native NT memory management APIs to safely purge working sets, standby lists,
/// system file cache, modified page lists, combined page lists, volume disk buffers, and registry cache.
/// </summary>
public static partial class MemoryOptimizerService
{
    #region Whitelist Protection

    private static readonly HashSet<string> SystemProcessWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "system idle process", "registry", "memory compression",
        "smss", "csrss", "wininit", "services", "lsass", "svchost", "fontdrvhost",
        "winlogon", "dwm", "explorer", "sihost", "taskhostw", "ctfmon",
        "shellexperiencehost", "startmenuexperiencehost", "searchhost", "taskmgr",
        "deltempo", "wintempcleaner", "audiodg", "spoolsv", "conhost", "runtimebroker",
        "searchindexer", "wmiprvse", "dllhost", "smartscreen", "msmpeng", "nissrv",
        "securityhealthservice", "securityhealthsystray", "mpdefendercoreprocess", "mpdefendercoreservice",
        "sense", "secure system", "vmmem", "vmmemwsl", "dasHost", "lsiso", "ngcsvc",
        "compattelrunner", "deviceassociationframeworkproviderhost", "tiworker", "trustedinstaller",
        "wlanext", "wudfhost", "sedsvc", "mpsvc"
    };

    #endregion

    #region Privilege Escalation (WinMemoryCleaner Implementation)

    /// <summary>
    /// Enables a token privilege for the current process.
    /// </summary>
    /// <returns>
    /// True if the privilege was successfully enabled. False if the privilege could not
    /// be acquired (for example, the process lacks it, or <see cref="ERROR_NOT_ALL_ASSIGNED"/>
    /// indicates only some requested privileges were adjusted).
    /// </returns>
    private static bool SetIncreasePrivilege(string privilegeName)
    {
        try
        {
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out IntPtr tokenHandle))
            {
                try
                {
                    var newState = new TokenPrivileges
                    {
                        Count = 1,
                        Luid = 0L,
                        Attr = PrivilegeAttributeEnabled
                    };

                    if (LookupPrivilegeValue(null, privilegeName, ref newState.Luid))
                    {
                        bool adjusted = AdjustTokenPrivileges(tokenHandle, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero);
                        if (adjusted)
                        {
                            // AdjustTokenPrivileges can return TRUE even when not all
                            // requested privileges were granted. Check GetLastError to
                            // detect ERROR_NOT_ALL_ASSIGNED and treat it as failure.
                            int lastError = Marshal.GetLastWin32Error();
                            if (lastError == ERROR_NOT_ALL_ASSIGNED)
                            {
                                Trace.WriteLine($"[Deltempo] Privilege '{privilegeName}' not all assigned (ERROR_NOT_ALL_ASSIGNED).");
                                return false;
                            }
                            return true;
                        }
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
        }
        catch
        {
            // Suppressed
        }
        return false;
    }

    /// <summary>
    /// Attempts to enable a privilege and records the outcome.
    /// A failed acquisition is not fatal â€” the underlying NT call may still succeed for an
    /// elevated process, or for operations that do not strictly require the privilege. The
    /// result is traced so a silent degradation is visible rather than reported as success.
    /// </summary>
    private static bool TryEnablePrivilege(string privilegeName)
    {
        bool acquired = SetIncreasePrivilege(privilegeName);
        if (!acquired)
            Trace.WriteLine($"[Deltempo] Could not enable '{privilegeName}'. The operation may still proceed but is not guaranteed to succeed.");
        return acquired;
    }

    /// <summary>
    /// Disables a token privilege previously enabled by <see cref="SetIncreasePrivilege"/>.
    /// Disabling an unheld privilege is a harmless no-op (AdjustTokenPrivileges with
    /// a zero attribute simply has nothing to remove).
    /// </summary>
    private static void DisablePrivilege(string privilegeName)
    {
        try
        {
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out IntPtr tokenHandle))
            {
                try
                {
                    var newState = new TokenPrivileges
                    {
                        Count = 1,
                        Luid = 0L,
                        Attr = 0 // SE_PRIVILEGE_DISABLED
                    };

                    if (LookupPrivilegeValue(null, privilegeName, ref newState.Luid))
                    {
                        AdjustTokenPrivileges(tokenHandle, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero);
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
            Trace.WriteLine($"[Deltempo] Privilege revert failed for '{privilegeName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Privilege hygiene for memory-boost operations: ensures SeProfileSingleProcess,
    /// SeIncreaseQuota, and SeDebug are not left enabled for the remainder of the
    /// process lifetime (defense in depth against post-compromise privilege reuse).
    /// </summary>
    private static void RevertBoostPrivileges()
    {
        DisablePrivilege(SeProfSingleProcessName);
        DisablePrivilege(SeIncreaseQuotaName);
        DisablePrivilege(SeDebugName);
    }

    #endregion

    #region Memory Telemetry API

    public static MemoryInfo GetMemoryInfo()
    {
        long totalPhys = 0;
        long availPhys = 0;
        long sysCache = 0;
        long commitTotal = 0;
        long commitLimit = 0;

        var memStatus = new MemoryStatusEx();
        if (GlobalMemoryStatusEx(memStatus))
        {
            totalPhys = memStatus.TotalPhys;
            availPhys = memStatus.AvailPhys;
            commitTotal = memStatus.TotalPageFile - memStatus.AvailPageFile;
            commitLimit = memStatus.TotalPageFile;
        }

        var perf = new PERFORMANCE_INFORMATION { cb = (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>() };
        if (GetPerformanceInfo(out perf, perf.cb))
        {
            long pageSize = (long)perf.PageSize.ToUInt64();
            sysCache = (long)perf.SystemCache.ToUInt64() * pageSize;
            if (totalPhys <= 0)
            {
                totalPhys = (long)perf.PhysicalTotal.ToUInt64() * pageSize;
                availPhys = (long)perf.PhysicalAvailable.ToUInt64() * pageSize;
            }
        }

        return new MemoryInfo
        {
            TotalPhysicalBytes = totalPhys,
            AvailablePhysicalBytes = availPhys,
            SystemCacheBytes = sysCache,
            CommitTotalBytes = commitTotal,
            CommitLimitBytes = commitLimit,
            MeasurementSucceeded = totalPhys > 0
        };
    }

    public static List<MemoryAreaSnapshot> GetMemoryAreaSnapshots()
    {
        var memInfo = GetMemoryInfo();
        long totalPhys = memInfo.TotalPhysicalBytes;
        long sysCache = memInfo.SystemCacheBytes;

        var snapshots = new List<MemoryAreaSnapshot>();

        foreach (var target in AllTargetTypes())
        {
            var info = TargetDescription(target);

            long currentBytes = target switch
            {
                MemoryTargetType.StandbyList => sysCache,
                MemoryTargetType.SystemFileCache => sysCache,
                MemoryTargetType.ModifiedPageList => 0,
                MemoryTargetType.CombinedPageList => 0,
                MemoryTargetType.WorkingSet => 0,
                MemoryTargetType.StandbyListLowPriority => 0,
                MemoryTargetType.ModifiedFileCache => 0,
                MemoryTargetType.RegistryCache => 0,
                _ => 0
            };

            double pct = totalPhys > 0 && currentBytes > 0 ? ((double)currentBytes / totalPhys) * 100.0 : 0;

            snapshots.Add(new MemoryAreaSnapshot
            {
                Target = target,
                DisplayName = info.DisplayName,
                Description = info.Description,
                TotalBytes = totalPhys,
                FreeBytes = memInfo.AvailablePhysicalBytes,
                CurrentBytes = currentBytes,
                UsedPercent = Math.Min(100.0, pct),
                IsAvailableOnThisOs = info.IsAvailableOnThisOs,
                IsSelected = true,
                SafetyBadge = info.SafetyBadge
            });
        }

        return snapshots;
    }

    #endregion

    #region WinMemoryCleaner Execution Engine

    public static async Task<MemoryOptimizationResult> OptimizeRamAsync(
        MemoryTargetType[]? targets = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var beforeMem = GetMemoryInfo();
                var results = new List<MemoryAreaResult>();
                int totalProcessesTrimmed = 0;

                var targetTypes = targets ?? DefaultActiveTargetTypes();

                foreach (var t in targetTypes)
                {
                    if (ct.IsCancellationRequested) break;

                    var areaRes = ExecuteAreaClean(t);
                    if (areaRes.ProcessesOptimized.HasValue)
                    {
                        totalProcessesTrimmed += areaRes.ProcessesOptimized.Value;
                    }
                    results.Add(areaRes);
                }

                ReleaseAppMemory();

                sw.Stop();
                var afterMem = GetMemoryInfo();
                long measured = Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes);
                Trace.WriteLine($"[Deltempo] Memory run measured available-RAM delta of {measured} bytes across {targetTypes.Length} requested target(s). Delta is an observation, not a performance gain.");

                var succeededCount = results.Count(r => r.Success);
                var failedCount = results.Count(r => !r.Success);
                MemoryOperationStatus overallStatus;
                if (targetTypes.Length == 0)
                    overallStatus = MemoryOperationStatus.NoApplicableTargets;
                else if (failedCount == 0)
                    overallStatus = MemoryOperationStatus.Success;
                else if (succeededCount > 0)
                    overallStatus = MemoryOperationStatus.PartialSuccess;
                else
                    overallStatus = MemoryOperationStatus.Failed;

                return new MemoryOptimizationResult
                {
                    Status = overallStatus,
                    ProcessesOptimized = totalProcessesTrimmed,
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    MeasuredAvailableBefore = beforeMem.AvailablePhysicalBytes,
                    MeasuredAvailableAfter = afterMem.AvailablePhysicalBytes,
                    RequestedTargetCount = targetTypes.Length,
                    AreaResults = results
                };
            }
            finally
            {
                // Privilege hygiene: never leave elevated token privileges enabled
                // beyond the boost operation itself.
                RevertBoostPrivileges();
            }
        }, ct);
    }

    public static async Task<MemoryAreaResult> OptimizeAreaAsync(
        MemoryTargetType target,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var res = ExecuteAreaClean(target);
                ReleaseAppMemory();
                return res;
            }
            finally
            {
                RevertBoostPrivileges();
            }
        }, ct);
    }

    /// <summary>
    /// Maps a raw NTSTATUS (or a Win32 error folded into the NTSTATUS space) onto an
    /// explicit outcome. Returning a bare <c>Failed</c> for every non-zero status hid the
    /// difference between "this build cannot do that" and "you need Administrator", which are
    /// very different things to show a user.
    /// </summary>
    private static MemoryOperationStatus MapNativeStatus(int status)
    {
        if (status == STATUS_SUCCESS) return MemoryOperationStatus.Success;

        // Native NTSTATUS values.
        if (status is STATUS_NOT_IMPLEMENTED or STATUS_NOT_SUPPORTED or STATUS_INVALID_INFO_CLASS)
            return MemoryOperationStatus.Unsupported;
        if (status is STATUS_ACCESS_DENIED or STATUS_PRIVILEGE_NOT_HELD)
            return MemoryOperationStatus.AccessDenied;

        // Win32 errors folded in by NTSTATUS_FROM_WIN32 (0xC0000000 | (err & 0xFFFF)).
        if (status == StatusFromWin32(ERROR_ACCESS_DENIED_WIN32)
            || status == StatusFromWin32(ERROR_PRIVILEGE_NOT_HELD_WIN32))
            return MemoryOperationStatus.AccessDenied;

        return MemoryOperationStatus.Failed;
    }

    /// <summary>Human-readable reason for a non-success status, for logs and tooltips.</summary>
    private static string DescribeNativeStatus(int status)
    {
        if (status == STATUS_SUCCESS) return "";

        string kind = MapNativeStatus(status) switch
        {
            MemoryOperationStatus.Unsupported => "not supported by this build of Windows",
            MemoryOperationStatus.AccessDenied => "access denied - run Deltempo as Administrator",
            _ => "the kernel rejected the operation",
        };
        return $"0x{status:X8}: {kind}";
    }

    private static MemoryAreaResult ExecuteAreaClean(MemoryTargetType target)
    {
        var result = new MemoryAreaResult { Target = target };
        var desc = TargetDescription(target);

        if (!desc.IsAvailableOnThisOs)
        {
            result.Status = MemoryOperationStatus.Unsupported;
            result.ErrorMessage = "Not supported on this OS";
            return result;
        }

        var beforeMem = GetMemoryInfo();

        try
        {
            // Every helper returns the raw NTSTATUS so the outcome can be mapped explicitly.
            int ntStatus = STATUS_SUCCESS;
            int procs = 0;

            switch (target)
            {
                case MemoryTargetType.WorkingSet:
                    procs = OptimizeWorkingSet();
                    // Working-set trimming is a best-effort sweep over many processes; zero
                    // trimmed is only a failure if the very first handle could not be opened.
                    ntStatus = procs > 0
                        ? STATUS_SUCCESS
                        : StatusFromWin32(Marshal.GetLastWin32Error());
                    result.ProcessesOptimized = procs;
                    break;

                case MemoryTargetType.StandbyList:
                    ntStatus = OptimizeStandbyList(lowPriority: false);
                    break;

                case MemoryTargetType.StandbyListLowPriority:
                    ntStatus = OptimizeStandbyList(lowPriority: true);
                    break;

                case MemoryTargetType.ModifiedPageList:
                    ntStatus = OptimizeModifiedPageList();
                    break;

                case MemoryTargetType.CombinedPageList:
                    ntStatus = OptimizeCombinedPageList();
                    break;

                case MemoryTargetType.SystemFileCache:
                    ntStatus = OptimizeSystemFileCache();
                    break;

                case MemoryTargetType.ModifiedFileCache:
                    ntStatus = OptimizeModifiedFileCache();
                    break;

                case MemoryTargetType.RegistryCache:
                    ntStatus = OptimizeRegistryCache();
                    break;

                default:
                    ntStatus = STATUS_NOT_IMPLEMENTED;
                    break;
            }

            var afterMem = GetMemoryInfo();
            long measured = Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes);
            if (measured > 0)
                Trace.WriteLine($"[Deltempo] OptimizeArea({target}) measured available-RAM delta of {measured} bytes. Observation only, not a performance gain.");

            result.Status = MapNativeStatus(ntStatus);
            result.ErrorMessage = result.Status == MemoryOperationStatus.Success
                ? ""
                : DescribeNativeStatus(ntStatus);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = MemoryOperationStatus.Failed;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    #endregion

    #region WinMemoryCleaner Direct Source Implementations

    /// <summary>
    /// Optimize the working set (WinMemoryCleaner specification).
    /// 1. Empties working sets across the ENTIRE OS via NT Kernel NtSetSystemInformation (MemoryEmptyWorkingSets).
    /// 2. Iterates process handles with SeDebugPrivilege to empty individual process working sets.
    /// </summary>
    private static int OptimizeWorkingSet()
    {
        // 1. Global NT Kernel Empty Working Sets (ALL processes across Windows in 1 call!)
        TryEnablePrivilege(SeProfSingleProcessName);
        var handle = GCHandle.Alloc(MemoryEmptyWorkingSets, GCHandleType.Pinned);
        try
        {
            NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf<int>());
        }
        catch
        {
            // Fallback to process loop
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        // 2. Process-by-process EmptyWorkingSet using SeDebugPrivilege
        TryEnablePrivilege(SeDebugName);
        int trimmedCount = 0;
        var processes = Process.GetProcesses();

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.Id <= 4) continue;
                    if (SystemProcessWhitelist.Contains(process.ProcessName)) continue;

                    IntPtr hProc = OpenProcess(ProcessQueryInformation | ProcessSetQuota, false, process.Id);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            if (EmptyWorkingSet(hProc))
                            {
                                trimmedCount++;
                            }
                        }
                        finally
                        {
                            CloseHandle(hProc);
                        }
                    }
                    else
                    {
                        if (EmptyWorkingSet(process.Handle))
                        {
                            trimmedCount++;
                        }
                    }
                }
                catch
                {
                    // Protected process access denied
                }
            }
        }

        return trimmedCount;
    }

    /// <summary>
    /// Optimize the standby list (WinMemoryCleaner specification).
    /// </summary>
    private static int OptimizeStandbyList(bool lowPriority = false)
    {
        TryEnablePrivilege(SeProfSingleProcessName);

        int cmd = lowPriority ? MemoryPurgeLowPriorityStandbyList : MemoryPurgeStandbyList;
        var handle = GCHandle.Alloc(cmd, GCHandleType.Pinned);

        try
        {
            return NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf<int>());
        }
        catch
        {
            return STATUS_UNSUCCESSFUL;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    /// <summary>
    /// Optimize the system file cache (WinMemoryCleaner specification).
    /// Calls BOTH NtSetSystemInformation(SystemFileCacheInformation) AND SetSystemFileCacheSize.
    /// </summary>
    private static int OptimizeSystemFileCache()
    {
        TryEnablePrivilege(SeIncreaseQuotaName);

        int ntStatus = STATUS_UNSUCCESSFUL;
        try
        {
            object systemFileCacheInformation;
            // Is64BitProcess, NOT Is64BitOperatingSystem. NtSetSystemInformation interprets
            // the buffer according to the CALLER's view, so a 32-bit (WOW64) process running
            // on 64-bit Windows must supply the 32-bit layout. Branching on the OS bitness
            // would hand the kernel a 64-bit struct it would misparse.
            if (Environment.Is64BitProcess)
            {
                systemFileCacheInformation = new SystemFileCacheInformation64
                {
                    MinimumWorkingSet = -1L,
                    PeakWorkingSet = -1L
                };
            }
            else
            {
                systemFileCacheInformation = new SystemFileCacheInformation32
                {
                    MinimumWorkingSet = int.MaxValue,
                    PeakWorkingSet = int.MaxValue
                };
            }

            var handle = GCHandle.Alloc(systemFileCacheInformation, GCHandleType.Pinned);
            try
            {
                int status = NtSetSystemInformation(SystemFileCacheInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(systemFileCacheInformation));
                ntStatus = status;
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }
        catch
        {
            // Ignored, proceed to Win32 cache size flush
        }

        // Win32 fallback. Prefer the NT result when it succeeded; otherwise report the
        // Win32 failure so an access-denied is distinguishable from a silent no-op.
        bool win32Success = false;
        int win32Status = STATUS_UNSUCCESSFUL;
        try
        {
            var fileCacheSize = IntPtr.Subtract(IntPtr.Zero, 1); // Flush
            win32Success = SetSystemFileCacheSize(fileCacheSize, fileCacheSize, 0);
            if (!win32Success)
                win32Status = StatusFromWin32(Marshal.GetLastWin32Error());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] SetSystemFileCacheSize threw: {ex.Message}");
        }

        if (ntStatus == STATUS_SUCCESS || win32Success) return STATUS_SUCCESS;
        // Prefer the access-denied signal over a generic NT failure when both failed.
        if (MapNativeStatus(ntStatus) == MemoryOperationStatus.AccessDenied) return ntStatus;
        return win32Status;
    }

    /// <summary>
    /// Optimize the modified page list (WinMemoryCleaner specification).
    /// </summary>
    private static int OptimizeModifiedPageList()
    {
        TryEnablePrivilege(SeProfSingleProcessName);

        var handle = GCHandle.Alloc(MemoryFlushModifiedList, GCHandleType.Pinned);
        try
        {
            return NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf<int>());
        }
        catch
        {
            return STATUS_UNSUCCESSFUL;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    /// <summary>
    /// Optimize the combined page list (WinMemoryCleaner specification).
    /// </summary>
    private static int OptimizeCombinedPageList()
    {
        TryEnablePrivilege(SeProfSingleProcessName);

        var memoryCombineInformationEx = new MemoryCombineInformationEx();
        var handle = GCHandle.Alloc(memoryCombineInformationEx, GCHandleType.Pinned);

        try
        {
            return NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryCombineInformationEx));
        }
        catch
        {
            return STATUS_UNSUCCESSFUL;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    /// <summary>
    /// Optimize the modified file cache across all local fixed drives (WinMemoryCleaner specification).
    /// Discards volume cache and resets write order on \\.\Drive: raw handles.
    /// </summary>
    private static int OptimizeModifiedFileCache()
    {
        bool anySuccess = false;
        int lastFailure = STATUS_UNSUCCESSFUL;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive == null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
                continue;

            try
            {
                using var handle = OpenVolumeHandle(drive.Name);
                if (handle == null || handle.IsInvalid)
                {
                    // Requires Administrator to open \\.\C: with write access. Recording the
                    // reason lets the caller report access-denied instead of a bare failure.
                    lastFailure = StatusFromWin32(Marshal.GetLastWin32Error());
                    continue;
                }

                var buffer = Marshal.AllocHGlobal(1);
                try
                {
                    DeviceIoControl(handle, IoControlResetWriteOrder, buffer, 1, IntPtr.Zero, 0, out _, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                try
                {
                    DeviceIoControl(handle, FsctlDiscardVolumeCache, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                }
                catch
                {
                    // Ignored
                }

                if (FlushFileBuffers(handle))
                {
                    anySuccess = true;
                }
            }
            catch (Exception ex)
            {
                lastFailure = StatusFromWin32(Marshal.GetLastWin32Error());
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Volume cache flush failed: {ex.Message}");
            }
        }

        return anySuccess ? STATUS_SUCCESS : lastFailure;
    }

    private static SafeFileHandle? OpenVolumeHandle(string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
            return null;

        try
        {
            return CreateFile(
                @"\\.\" + driveLetter.TrimEnd(':', '\\') + ":",
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                (int)FileAttributes.Normal | FlagsNoBuffering,
                IntPtr.Zero);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Optimize the registry cache (WinMemoryCleaner specification).
    /// </summary>
    private static int OptimizeRegistryCache()
    {
        try
        {
            return NtSetSystemInformation(SystemRegistryReconciliationInformation, IntPtr.Zero, 0);
        }
        catch
        {
            return STATUS_UNSUCCESSFUL;
        }
    }

    /// <summary>
    /// Release the cleaner application's own working set (WinMemoryCleaner specification).
    /// </summary>
    private static void ReleaseAppMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch
        {
            // Ignored
        }
    }

    #endregion

    #region Helpers & Area Metadata
    private static readonly Dictionary<MemoryTargetType, (string DisplayName, string Description, bool IsAvailableOnThisOs, string SafetyBadge)>
        _targetDescriptions = new()
        {
            { MemoryTargetType.WorkingSet,             ("Process Working Sets",       "Global NT Kernel working set sweep + process trim with SeDebugPrivilege. Trims active pages back to disk; apps may re-fault them in, causing brief slowdowns.", true, "TRIMS ACTIVE PAGES") },
            { MemoryTargetType.StandbyList,             ("Standby List (Full)",        "Discards the standby cache of closed applications. Windows re-reads these files on next open. No data loss, but cold-start reads get slower.", true, "DISCARDS FILE CACHE") },
            { MemoryTargetType.StandbyListLowPriority,  ("Standby (Low Priority)",     "Purges only low-priority cached pages. Least disruptive option; mostly frees zero unless memory is under pressure.", true, "LEAST DISRUPTIVE") },
            { MemoryTargetType.SystemFileCache,        ("System File Cache",          "Flushes the Windows filesystem cache to disk. Files must be re-read from disk afterwards.", true, "FLUSHES TO DISK") },
            { MemoryTargetType.ModifiedPageList,       ("Modified Page List",         "Writes modified dirty pages to disk, then clears them from active RAM. Adds disk I/O pressure.", true, "WRITES DIRTY PAGES") },
            { MemoryTargetType.CombinedPageList,       ("Combined Page List",         "Triggers the memory page combiner to merge identical physical pages. Purely an internal de-duplication pass.", true, "DE-DUPLICATES") },
            { MemoryTargetType.ModifiedFileCache,      ("Volume File Cache",          "Flushes volume write order & discards volume cache on fixed drive handles. Requires Administrator.", true, "REQUIRES ADMIN") },
            { MemoryTargetType.RegistryCache,          ("Registry Cache",             "Reconciles & flushes cached registry hives from active RAM. Registry access may briefly stall.", true, "FLUSHES HIVES") }
        };

    private static (string DisplayName, string Description, bool IsAvailableOnThisOs, string SafetyBadge) TargetDescription(MemoryTargetType t)
    {
        return _targetDescriptions.TryGetValue(t, out var d) ? d : (t.ToString(), "Unknown target", true, "UNCATEGORIZED");
    }

    public static MemoryTargetType[] AllTargetTypes()
    {
        return new[]
        {
            MemoryTargetType.WorkingSet,
            MemoryTargetType.StandbyList,
            MemoryTargetType.StandbyListLowPriority,
            MemoryTargetType.SystemFileCache,
            MemoryTargetType.ModifiedPageList,
            MemoryTargetType.CombinedPageList,
            MemoryTargetType.ModifiedFileCache,
            MemoryTargetType.RegistryCache
        };
    }

    /// <summary>
    /// Default active areas cleaned during 1-click RAM boost (WinMemoryCleaner specification).
    /// </summary>
    public static MemoryTargetType[] DefaultActiveTargetTypes()
    {
        return new[]
        {
            MemoryTargetType.WorkingSet,
            MemoryTargetType.StandbyList,
            MemoryTargetType.SystemFileCache,
            MemoryTargetType.ModifiedPageList,
            MemoryTargetType.CombinedPageList,
            MemoryTargetType.ModifiedFileCache,
            MemoryTargetType.RegistryCache
        };
    }
    #endregion
}

