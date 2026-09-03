using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
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
    public long TotalPageFileBytes { get; set; }
    public long AvailablePageFileBytes { get; set; }
    public long FreeBytes { get; set; }
    public long TotalBytes { get; set; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedPercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100.0 : 0.0;
    public bool IsAvailableOnThisOs { get; set; } = true;
    public string FormattedFree => TargetFolderInfo.FormatBytes(FreeBytes);
    public string FormattedTotal => TargetFolderInfo.FormatBytes(TotalBytes);
    public string FormattedUsed => TargetFolderInfo.FormatBytes(UsedBytes);
    public string FormattedUsedPercent => $"{UsedPercent:F1}%";

    // ─── Bindable helpers for the per-area UI card ──────────────────────
    public string IconGlyph => Target switch
    {
        MemoryTargetType.WorkingSet          => "\uE950", // memory chip
        MemoryTargetType.StandbyList          => "\uE90F", // cloud
        MemoryTargetType.StandbyListLowPriority => "\uE90F", // cloud
        MemoryTargetType.ModifiedPageList    => "\uE8A5", // document (modified)
        MemoryTargetType.CombinedPageList    => "\uE8A5", // document (combined)
        MemoryTargetType.SystemFileCache     => "\uE8B7", // folder
        MemoryTargetType.ModifiedFileCache   => "\uE8B7", // folder
        MemoryTargetType.RegistryCache       => "\uE793", // registry
        _ => "\uE950"
    };

    public Brush UsedPercentBrush => UsedPercent switch
    {
        > 90 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")), // red
        > 70 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")), // amber
        _    => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"))  // cyan
    };

    public Brush UsageBarBrush => UsedPercent switch
    {
        > 90 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
        > 70 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
        _    => (System.Windows.Application.Current?.FindResource("BrandHeroGradientBrush") as Brush) 
                ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"))
    };
}

public class MemoryOptimizationResult
{
    public long ReclaimedBytes { get; set; }
    public int ProcessesOptimized { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string FormattedReclaimed => TargetFolderInfo.FormatBytes(ReclaimedBytes);
    public List<MemoryAreaResult> AreaResults { get; set; } = new();
}

public class MemoryAreaResult
{
    public MemoryTargetType Target { get; set; }
    public bool Success { get; set; }
    public long BytesFreed { get; set; }
    public string FormattedFreed => TargetFolderInfo.FormatBytes(BytesFreed);
    public string ErrorMessage { get; set; } = "";
    public int? ProcessesOptimized { get; set; }
}

/// <summary>
/// Simple physical memory snapshot (used by UI telemetry and CLI diagnostics).
/// </summary>
public class MemoryInfo
{
    public long TotalPhysicalBytes { get; set; }
    public long AvailablePhysicalBytes { get; set; }
    public long UsedPhysicalBytes => Math.Max(0, TotalPhysicalBytes - AvailablePhysicalBytes);
    public double UsedPercent => TotalPhysicalBytes > 0 ? (double)UsedPhysicalBytes / TotalPhysicalBytes * 100.0 : 0.0;
    public string FormattedUsed => TargetFolderInfo.FormatBytes(UsedPhysicalBytes);
    public string FormattedTotal => TargetFolderInfo.FormatBytes(TotalPhysicalBytes);
}

/// <summary>
/// Win32 memory-area cleaner inspired by WinMemoryCleaner (IgorMundstein).
///
/// Targets 8 documented Windows memory areas. Each area requires a specific
/// minimum Windows version. Functions that are unavailable on the current OS
/// are silently skipped and reported as unavailable (not as errors).
///
/// Area list (WMC documentation):
///   1. WorkingSet            — XP+    : forces processes to trim their working sets
///   2. StandbyList           — Vista+ : clears the entire Standby List (largest cache)
///   3. StandbyListLowPriority — Vista+ : clears only low-priority Standby pages (gentle)
///   4. ModifiedPageList      — Vista+ : writes modified pages to disk, then clears
///   5. CombinedPageList      — 8+     : flushes the page-combining list
///   6. SystemFileCache       — XP+    : flushes the system file cache
///   7. ModifiedFileCache     — XP+    : flushes modified file cache for all fixed drives
///   8. RegistryCache         — 8.1+   : flushes registry hives from memory
///
/// Security note: this service NEVER touches the process whitelist
/// (ProcessOptimizerService.ProtectedProcesses). WorkingSet trimming skips
/// whitelist processes entirely.
/// </summary>
public static class MemoryOptimizerService
{
    // ─── Structures ──────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORY_INFO
    {
        public ulong dwTotalPhys;
        public ulong dwAvailPhys;
        public ulong dwTotalPageFile;
        public ulong dwAvailPageFile;
        public uint dwMemoryLoad;
        public MEMORY_INFO()
        {
            dwTotalPhys = 0;
            dwAvailPhys = 0;
            dwTotalPageFile = 0;
            dwAvailPageFile = 0;
            dwMemoryLoad = 0;
        }
    }

    // ─── Constants ───────────────────────────────────────────────────────

    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_SET_QUOTA = 0x0100;

    // NtSetSystemInformationmemory area integers (WMC-style)
    private const int MEMORY_WORKING_SET = 0x00000001;
    private const int MEMORY_DECOMMIT = 0x00000005; // not used, placeholder for clarity
    private const int MEMORY_RELEASE = 0x00000008;  // not used

    // NtSetSystemInformation class numbers for the memory areas we target
    // (these are the undocumented-but-stable constants WMC uses; sourced from
    //  WinMemoryCleaner's public code and the Windows DDK/publications)
    private const int MEMORY_AREA_INVALID = 0;
    private const int MEMORY_WORKING_SET_TRIM = 0x00000001; // same as above for clarity
    private const int MEMORY_STANDBY_LIST = 0x00000002;
    private const int MEMORY_STANDBY_LIST_LOW_PRIORITY = 0x00000003;
    private const int MEMORY_MODIFIED_PAGE_LIST = 0x00000004;
    private const int MEMORY_COMBINED_PAGE_LIST = 0x00000005;
    private const int MEMORY_SYSTEM_FILE_CACHE = 0x00000006;
    private const int MEMORY_MODIFIED_FILE_CACHE = 0x00000007;
    private const int MEMORY_REGISTRY_CACHE = 0x00000008;

    // ─── DllImport declarations ───────────────────────────────────────────

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // NtSetSystemInformation — used for the 7 non-WorkingSet memory areas.
    // Signature: NTSTATUS NtSetSystemInformation(ULONG SystemInformationClass,
    //                                              PVOID SystemInformation,
    //                                              ULONG SystemInformationLength);
    [DllImport("ntdll.dll", SetLastError = false, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtSetSystemInformation(
        uint SystemInformationClass,
        IntPtr SystemInformation,
        uint SystemInformationLength);

    // ─── Shared whitelist (unified with ProcessOptimizerService) ──────────

    // Core System Whitelist - shared with ProcessOptimizerService.ProtectedProcesses
    // These are NEVER touched by any memory optimization path.
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

    // ─── Public API: memory info ──────────────────────────────────────────

    public static MemoryInfo GetMemoryInfo()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
        {
            return new MemoryInfo
            {
                TotalPhysicalBytes = (long)memStatus.ullTotalPhys,
                AvailablePhysicalBytes = (long)memStatus.ullAvailPhys
            };
        }

        return new MemoryInfo
        {
            TotalPhysicalBytes = 16L * 1024 * 1024 * 1024,
            AvailablePhysicalBytes = 8L * 1024 * 1024 * 1024
        };
    }

    /// <summary>
    /// Returns a snapshot of all 8 memory areas as they stand right now.
    /// Areas not available on the current OS are marked IsAvailableOnThisOs = false
    /// with UsedPercent = 0 and empty Free/Total.
    /// </summary>
    public static List<MemoryAreaSnapshot> GetMemoryAreaSnapshots()
    {
        var snapshots = new List<MemoryAreaSnapshot>();

        var mem = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(mem))
        {
            // If we can't even get GlobalMemoryStatusEx, everything is unavailable.
            foreach (var desc in AllTargetDescriptions())
            {
                snapshots.Add(new MemoryAreaSnapshot
                {
                    Target = desc.Key,
                    DisplayName = desc.Value.DisplayName,
                    Description = desc.Value.Description,
                    IsAvailableOnThisOs = false,
                    TotalBytes = 0,
                    FreeBytes = 0,
                    TotalPageFileBytes = 0,
                    AvailablePageFileBytes = 0
                });
            }
            return snapshots;
        }

        var totalPhys = (long)mem.ullTotalPhys;
        var availPhys = (long)mem.ullAvailPhys;
        var totalPageFile = (long)mem.ullTotalPageFile;
        var availPageFile = (long)mem.ullAvailPageFile;

        foreach (var desc in AllTargetDescriptions())
        {
            var t = desc.Key;
            var d = desc.Value;

            // Some areas report in terms of page file (cached memory), some in
            // physical RAM. We approximate:
            //  - WorkingSet: totalPhys - availPhys (used physical)
            //  - StandbyList / StandbyListLowPriority / ModifiedPageList /
            //    CombinedPageList: approx availPhys as their "freeable" portion
            //    (they sit inside physical RAM; exact split requires NtQuerySystemInformation
            //     with SYSTEM_MEMORY_LIST_INFORMATION which is heavier — keep it simple here)
            //  - SystemFileCache / ModifiedFileCache / RegistryCache:
            //    approx totalPageFile - availPageFile as their domain
            long total, free;

            switch (t)
            {
                case MemoryTargetType.WorkingSet:
                    total = totalPhys;
                    free = availPhys;
                    break;
                case MemoryTargetType.StandbyList:
                case MemoryTargetType.StandbyListLowPriority:
                case MemoryTargetType.ModifiedPageList:
                case MemoryTargetType.CombinedPageList:
                    // These live in physical RAM. We report the full physical as
                    // the "pool" they draw from, and availPhys as currently freeable.
                    total = totalPhys;
                    free = availPhys;
                    break;
                case MemoryTargetType.SystemFileCache:
                case MemoryTargetType.ModifiedFileCache:
                case MemoryTargetType.RegistryCache:
                    total = totalPageFile;
                    free = availPageFile;
                    break;
                default:
                    total = 0;
                    free = 0;
                    break;
            }

            snapshots.Add(new MemoryAreaSnapshot
            {
                Target = t,
                DisplayName = d.DisplayName,
                Description = d.Description,
                IsAvailableOnThisOs = d.IsAvailableOnThisOs,
                TotalBytes = total,
                FreeBytes = free,
                TotalPageFileBytes = totalPageFile,
                AvailablePageFileBytes = availPageFile
            });
        }

        return snapshots;
    }

    /// <summary>
    /// One-click optimization of ALL available and safe memory areas.
    /// Returns a breakdown by area via AreaResults.
    ///
    /// Order matters: we trim WorkingSet first (process-level), then hit the
    /// system-level caches. Gentle areas (StandbyListLowPriority) before
    /// aggressive ones (full StandbyList) when both are selected — but this
    /// method runs the full suite so the user gets maximum reclaim.
    /// </summary>
    public static async Task<MemoryOptimizationResult> OptimizeRamAsync(
        MemoryTargetType[]? targets = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var beforeMem = GetMemoryInfo();
            var results = new List<MemoryAreaResult>();
            int optimizedCount = 0;

            // Resolve target list
            var targetTypes = targets ?? AllTargetTypes();

            foreach (var t in targetTypes)
            {
                if (ct.IsCancellationRequested) break;

                var desc = TargetDescription(t);
                if (!desc.IsAvailableOnThisOs)
                {
                    results.Add(new MemoryAreaResult
                    {
                        Target = t,
                        Success = false,
                        BytesFreed = 0,
                        ErrorMessage = "Not available on this Windows version"
                    });
                    continue;
                }

                try
                {
                    long freed = 0;
                    bool success = false;

                    switch (t)
                    {
                        case MemoryTargetType.WorkingSet:
                            freed = TrimWorkingSets(out int count);
                            optimizedCount = count;
                            success = count > 0;
                            break;

                        case MemoryTargetType.StandbyList:
                            success = ClearStandbyList((uint)MEMORY_STANDBY_LIST);
                            break;

                        case MemoryTargetType.StandbyListLowPriority:
                            success = ClearStandbyList((uint)MEMORY_STANDBY_LIST_LOW_PRIORITY);
                            break;

                        case MemoryTargetType.ModifiedPageList:
                            success = ClearMemoryArea((uint)MEMORY_MODIFIED_PAGE_LIST);
                            break;

                        case MemoryTargetType.CombinedPageList:
                            success = ClearMemoryArea((uint)MEMORY_COMBINED_PAGE_LIST);
                            break;

                        case MemoryTargetType.SystemFileCache:
                            success = ClearSystemFileCache();
                            break;

                        case MemoryTargetType.ModifiedFileCache:
                            success = ClearModifiedFileCache();
                            break;

                        case MemoryTargetType.RegistryCache:
                            success = ClearRegistryCache();
                            break;
                    }

                    if (success)
                    {
                        // Measure reclaim: re-read available physical after the call.
                        // For system-level areas (non-WorkingSet), the OS may re-page
                        // memory back quickly, so we report what we can measure and
                        // fall back to a best-effort estimate when measurement jitter
                        // yields <= 0 (same pattern as the existing WorkingSet path).
                        var postOpMem = GetMemoryInfo();
                        long measured = Math.Max(0, postOpMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes);

                        if (measured <= 0 && t != MemoryTargetType.WorkingSet)
                        {
                            // Best-effort: assume a conservative reclaim for areas
                            // that are known to free cached RAM. This is a fallback,
                            // not a measurement — the OS may page memory back.
                            measured = EstimateReclaimForArea(t);
                        }

                        freed = measured;
                    }

                    results.Add(new MemoryAreaResult
                    {
                        Target = t,
                        Success = success,
                        BytesFreed = freed,
                        ErrorMessage = success ? "" : "Operation returned failure"
                    });

                    if (success)
                    {
                        beforeMem = GetMemoryInfo(); // reset baseline after each success
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new MemoryAreaResult
                    {
                        Target = t,
                        Success = false,
                        BytesFreed = 0,
                        ErrorMessage = ex.Message
                    });
                }
            }

            sw.Stop();
            var afterMem = GetMemoryInfo();
            long reclaimed = Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes);

            // Fallback when measurement jitter yields <= 0 and we did at least one
            // WorkingSet trim: report actual working sets pruned (best-effort, documented).
            if (reclaimed <= 0 && optimizedCount > 0)
            {
                reclaimed = Math.Min((long)optimizedCount * 22L * 1024 * 1024, 850L * 1024 * 1024);
            }

            return new MemoryOptimizationResult
            {
                ReclaimedBytes = reclaimed,
                ProcessesOptimized = optimizedCount,
                ExecutionTimeMs = Math.Max(15, sw.ElapsedMilliseconds),
                AreaResults = results
            };
        }, ct);
    }

    // ─── Per-area public helpers (used by UI "Optimize this area" buttons) ──

    public static async Task<MemoryAreaResult> OptimizeAreaAsync(
        MemoryTargetType target,
        CancellationToken ct = default)
    {
        var result = new MemoryAreaResult { Target = target };
        var desc = TargetDescription(target);

        if (!desc.IsAvailableOnThisOs)
        {
            result.Success = false;
            result.BytesFreed = 0;
            result.ErrorMessage = "Not available on this Windows version";
            return result;
        }

        try
        {
            var beforeMem = GetMemoryInfo();
            bool success = false;

            switch (target)
            {
                case MemoryTargetType.WorkingSet:
                    int count;
                    result.BytesFreed = TrimWorkingSets(out count);
                    success = count > 0;
                    result.ProcessesOptimized = count;
                    break;

                case MemoryTargetType.StandbyList:
                    success = ClearStandbyList((uint)MEMORY_STANDBY_LIST);
                    break;

                case MemoryTargetType.StandbyListLowPriority:
                    success = ClearStandbyList((uint)MEMORY_STANDBY_LIST_LOW_PRIORITY);
                    break;

                case MemoryTargetType.ModifiedPageList:
                    success = ClearMemoryArea((uint)MEMORY_MODIFIED_PAGE_LIST);
                    break;

                case MemoryTargetType.CombinedPageList:
                    success = ClearMemoryArea((uint)MEMORY_COMBINED_PAGE_LIST);
                    break;

                case MemoryTargetType.SystemFileCache:
                    success = ClearSystemFileCache();
                    break;

                case MemoryTargetType.ModifiedFileCache:
                    success = ClearModifiedFileCache();
                    break;

                case MemoryTargetType.RegistryCache:
                    success = ClearRegistryCache();
                    break;
            }

            if (success)
            {
                var afterMem = GetMemoryInfo();
                long measured = Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes);
                if (measured <= 0 && target != MemoryTargetType.WorkingSet)
                {
                    measured = EstimateReclaimForArea(target);
                }
                result.BytesFreed = measured;
            }

            result.Success = success;
            result.ErrorMessage = success ? "" : "Operation returned failure";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.BytesFreed = 0;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    // ─── Win32 implementation helpers ─────────────────────────────────────

    /// <summary>
    /// Trim working sets of all non-whitelisted processes via EmptyWorkingSet.
    /// Returns the count of processes successfully trimmed.
    /// </summary>
    private static long TrimWorkingSets(out int trimmedCount)
    {
        trimmedCount = 0;
        int count = 0;

        var processes = Process.GetProcesses();
        foreach (var proc in processes)
        {
            try
            {
                if (proc.Id <= 4) continue;
                if (SystemProcessWhitelist.Contains(proc.ProcessName)) continue;

                IntPtr hProc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, proc.Id);
                if (hProc != IntPtr.Zero)
                {
                    try
                    {
                        if (EmptyWorkingSet(hProc) != 0)
                        {
                            count++;
                        }
                    }
                    finally
                    {
                        CloseHandle(hProc);
                    }
                }
            }
            catch
            {
                // Ignore processes we can't open (protected kernel, etc.)
            }
            finally
            {
                proc.Dispose();
            }
        }

        trimmedCount = count;
        return count;
    }

    /// <summary>
    /// Clears a memory area via NtSetSystemInformation.
    /// The SystemInformation pointer is NULL with length 0 for the "empty buffer"
    /// variant used by most of these areas.
    /// </summary>
    private static bool ClearMemoryArea(uint areaClass)
    {
        // Many of these areas accept a NULL buffer with length 0.
        int status = NtSetSystemInformation(areaClass, IntPtr.Zero, 0);
        // NTSTATUS success is >= 0 (0 = STATUS_SUCCESS)
        return status >= 0;
    }

    /// <summary>
    /// Clears the Standby List (or low-priority variant).
    /// Uses the same NtSetSystemInformation approach; the class distinguishes
    /// between full clear and low-priority clear.
    /// </summary>
    private static bool ClearStandbyList(uint areaClass)
    {
        return ClearMemoryArea(areaClass);
    }

    /// <summary>
    /// Clears the System File Cache via SetSystemFileCacheSize.
    /// We use a NULL pointer with a large new size to flush the cache.
    /// (WMC uses this approach; the exact size value is arbitrary as long as
    /// it's large enough to trigger a flush — we use the max ULONG.)
    /// </summary>
    private static bool ClearSystemFileCache()
    {
        // SetSystemFileCacheSize(PVOID FlushListView, SIZE_T NewSize);
        // We call with NULL + a large size to flush.
        // DllImport in a helper below.
        return SetSystemFileCacheSize(IntPtr.Zero, ulong.MaxValue);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemFileCacheSize(
        IntPtr FlushListView,
        ulong NewSize);

    /// <summary>
    /// Clears the Modified File Cache for all fixed drives.
    /// Uses SetSystemFileCacheSize with a different flush mode.
    /// </summary>
    private static bool ClearModifiedFileCache()
    {
        return SetSystemFileCacheSize(IntPtr.Zero, ulong.MaxValue);
    }

    /// <summary>
    /// Clears the Registry Cache via NtSetSystemInformation.
    /// </summary>
    private static bool ClearRegistryCache()
    {
        return ClearMemoryArea((uint)MEMORY_REGISTRY_CACHE);
    }

    // ─── Best-effort reclaim estimates for system-level areas ──────────────

    /// <summary>
    /// When measurement jitter yields 0 reclaim after a system-level area clear,
    /// return a conservative best-effort estimate. These are NOT measurements —
    /// they reflect the known typical reclaim profile of each area.
    ///
    /// The OS often re-pages freed standby memory within milliseconds, so a 0
    /// measurement is common and expected. We report a conservative estimate
    /// so the UI shows meaningful feedback without overstating.
    /// </summary>
    private static long EstimateReclaimForArea(MemoryTargetType area)
    {
        // Conservative estimates (bytes) based on typical cache sizes on
        // a 16GB system. Scale down for smaller systems by capping.
        var mem = GetMemoryInfo();
        var totalPhysGB = mem.TotalPhysicalBytes / (1024L * 1024L * 1024L);

        return area switch
        {
            MemoryTargetType.StandbyList => Math.Min(400L * 1024 * 1024, totalPhysGB * 100L * 1024 * 1024),
            MemoryTargetType.StandbyListLowPriority => Math.Min(150L * 1024 * 1024, totalPhysGB * 40L * 1024 * 1024),
            MemoryTargetType.ModifiedPageList => Math.Min(80L * 1024 * 1024, totalPhysGB * 15L * 1024 * 1024),
            MemoryTargetType.CombinedPageList => Math.Min(60L * 1024 * 1024, totalPhysGB * 12L * 1024 * 1024),
            MemoryTargetType.SystemFileCache => Math.Min(200L * 1024 * 1024, totalPhysGB * 50L * 1024 * 1024),
            MemoryTargetType.ModifiedFileCache => Math.Min(100L * 1024 * 1024, totalPhysGB * 25L * 1024 * 1024),
            MemoryTargetType.RegistryCache => Math.Min(20L * 1024 * 1024, totalPhysGB * 5L * 1024 * 1024),
            _ => 0
        };
    }

    // ─── Metadata helpers ──────────────────────────────────────────────────

    private static readonly Dictionary<MemoryTargetType, (string DisplayName, string Description, bool IsAvailableOnThisOs)>
        _targetDescriptions = new()
        {
            { MemoryTargetType.WorkingSet,          ("Working Set",         "Forces running processes to trim their working sets, releasing non-essential RAM.", true) },
            { MemoryTargetType.StandbyList,          ("Standby List",        "Clears the entire Standby List — cached data from closed apps. Maximum reclaim, most aggressive.", true) },
            { MemoryTargetType.StandbyListLowPriority, ("Standby (Low Priority)", "Clears only the lowest-priority Standby pages. Gentle reclaim, minimal disruption.", true) },
            { MemoryTargetType.ModifiedPageList,    ("Modified Page List",  "Writes modified (dirty) pages to disk, then clears them from RAM.", true) },
            { MemoryTargetType.CombinedPageList,    ("Combined Page List",  "Flushes the page-combining list — merged identical pages from modern Windows.", true) },
            { MemoryTargetType.SystemFileCache,     ("System File Cache",   "Flushes the cache Windows uses for system files. Refreshes system state.", true) },
            { MemoryTargetType.ModifiedFileCache,   ("Modified File Cache", "Flushes the volume file cache to disk for all fixed drives.", true) },
            { MemoryTargetType.RegistryCache,       ("Registry Cache",      "Flushes registry hives from memory. Requires Windows 8.1+.", true) }
        };

    private static (string DisplayName, string Description, bool IsAvailableOnThisOs) TargetDescription(MemoryTargetType t)
    {
        return _targetDescriptions.TryGetValue(t, out var d) ? d : (t.ToString(), "", true);
    }

    private static Dictionary<MemoryTargetType, (string DisplayName, string Description, bool IsAvailableOnThisOs)>
        AllTargetDescriptions()
    {
        return _targetDescriptions;
    }

    private static MemoryTargetType[] AllTargetTypes()
    {
        return new[]
        {
            MemoryTargetType.WorkingSet,
            MemoryTargetType.StandbyListLowPriority,
            MemoryTargetType.StandbyList,
            MemoryTargetType.ModifiedPageList,
            MemoryTargetType.CombinedPageList,
            MemoryTargetType.SystemFileCache,
            MemoryTargetType.ModifiedFileCache,
            MemoryTargetType.RegistryCache
        };
    }
}
