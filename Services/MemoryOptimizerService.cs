using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
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
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long CurrentBytes { get; set; }
    public double UsedPercent { get; set; }
    public bool IsAvailableOnThisOs { get; set; } = true;
    public bool IsSelected { get; set; } = true;
    public string SafetyBadge { get; set; } = "SAFE TO PURGE";

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

    // Cached frozen brushes. These properties are read on every memory-telemetry
    // refresh tick; allocating a new SolidColorBrush per read created steady GC
    // pressure. Frozen brushes are thread-safe and skip change-notification overhead.
    private static readonly Brush HighUsageBrush = CreateFrozenBrush("#EF4444");     // red
    private static readonly Brush ElevatedUsageBrush = CreateFrozenBrush("#F59E0B"); // amber
    private static readonly Brush NormalUsageBrush = CreateFrozenBrush("#00E5FF");   // cyan

    private static SolidColorBrush CreateFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public Brush UsedPercentBrush => UsedPercent switch
    {
        > 90 => HighUsageBrush,
        > 70 => ElevatedUsageBrush,
        _ => NormalUsageBrush
    };

    public Brush UsageBarBrush => UsedPercent switch
    {
        > 90 => HighUsageBrush,
        > 70 => ElevatedUsageBrush,
        _ => (System.Windows.Application.Current?.FindResource("BrandHeroGradientBrush") as Brush)
                ?? NormalUsageBrush
    };
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

public class MemoryOptimizationResult
{
    public long MeasuredBytesFreed { get; set; }
    public MemoryOperationStatus Status { get; set; } = MemoryOperationStatus.Failed;
    public int ProcessesOptimized { get; set; }
    public long ExecutionTimeMs { get; set; }
    public long MeasuredAvailableBefore { get; set; }
    public long MeasuredAvailableAfter { get; set; }
    public long MeasuredDelta => Math.Max(0, MeasuredAvailableAfter - MeasuredAvailableBefore);
    public string FormattedReclaimed => TargetFolderInfo.FormatBytes(MeasuredBytesFreed);
    public string FormattedMeasuredDelta => TargetFolderInfo.FormatBytes(MeasuredDelta);
    public List<MemoryAreaResult> AreaResults { get; set; } = new();
    public bool Success => Status is MemoryOperationStatus.Success or MemoryOperationStatus.PartialSuccess;
}

public class MemoryAreaResult
{
    public MemoryTargetType Target { get; set; }
    public MemoryOperationStatus Status { get; set; } = MemoryOperationStatus.Failed;
    public bool Success => Status is MemoryOperationStatus.Success or MemoryOperationStatus.PartialSuccess;
    public long MeasuredBytesFreed { get; set; }
    public string FormattedFreed => TargetFolderInfo.FormatBytes(MeasuredBytesFreed);
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
                        return AdjustTokenPrivileges(tokenHandle, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero);
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

            var succeededCount = results.Count(r => r.Success);
            var failedCount = results.Count(r => !r.Success);
            MemoryOperationStatus overallStatus;
            if (failedCount == 0)
                overallStatus = MemoryOperationStatus.Success;
            else if (succeededCount > 0)
                overallStatus = MemoryOperationStatus.PartialSuccess;
            else
                overallStatus = MemoryOperationStatus.Failed;

            return new MemoryOptimizationResult
            {
                MeasuredBytesFreed = measured,
                Status = overallStatus,
                ProcessesOptimized = totalProcessesTrimmed,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                MeasuredAvailableBefore = beforeMem.AvailablePhysicalBytes,
                MeasuredAvailableAfter = afterMem.AvailablePhysicalBytes,
                AreaResults = results
            };
        }, ct);
    }

    public static async Task<MemoryAreaResult> OptimizeAreaAsync(
        MemoryTargetType target,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var res = ExecuteAreaClean(target);
            ReleaseAppMemory();
            return res;
        }, ct);
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
            bool success = false;
            int procs = 0;

            switch (target)
            {
                case MemoryTargetType.WorkingSet:
                    procs = OptimizeWorkingSet();
                    success = procs > 0;
                    result.ProcessesOptimized = procs;
                    break;

                case MemoryTargetType.StandbyList:
                    success = OptimizeStandbyList(lowPriority: false);
                    break;

                case MemoryTargetType.StandbyListLowPriority:
                    success = OptimizeStandbyList(lowPriority: true);
                    break;

                case MemoryTargetType.ModifiedPageList:
                    success = OptimizeModifiedPageList();
                    break;

                case MemoryTargetType.CombinedPageList:
                    success = OptimizeCombinedPageList();
                    break;

                case MemoryTargetType.SystemFileCache:
                    success = OptimizeSystemFileCache();
                    break;

                case MemoryTargetType.ModifiedFileCache:
                    success = OptimizeModifiedFileCache();
                    break;

                case MemoryTargetType.RegistryCache:
                    success = OptimizeRegistryCache();
                    break;
            }

            var afterMem = GetMemoryInfo();
            long measured = Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes);

            result.Status = success ? MemoryOperationStatus.Success : MemoryOperationStatus.Failed;
            result.MeasuredBytesFreed = measured;
            result.ErrorMessage = success ? "" : "Operation rejected or privilege denied";
            return result;
        }
        catch (Exception ex)
        {
            result.Status = MemoryOperationStatus.Failed;
            result.MeasuredBytesFreed = 0;
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
        SetIncreasePrivilege(SeProfSingleProcessName);
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
        SetIncreasePrivilege(SeDebugName);
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
    private static bool OptimizeStandbyList(bool lowPriority = false)
    {
        SetIncreasePrivilege(SeProfSingleProcessName);

        int cmd = lowPriority ? MemoryPurgeLowPriorityStandbyList : MemoryPurgeStandbyList;
        var handle = GCHandle.Alloc(cmd, GCHandleType.Pinned);

        try
        {
            int status = NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf<int>());
            return status == 0;
        }
        catch
        {
            return false;
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
    private static bool OptimizeSystemFileCache()
    {
        SetIncreasePrivilege(SeIncreaseQuotaName);

        try
        {
            object systemFileCacheInformation;
            if (Environment.Is64BitOperatingSystem)
            {
                systemFileCacheInformation = new SystemFileCacheInformation64
                {
                    MinimumWorkingSet = -1L,
                    MaximumWorkingSet = -1L
                };
            }
            else
            {
                systemFileCacheInformation = new SystemFileCacheInformation32
                {
                    MinimumWorkingSet = int.MaxValue,
                    MaximumWorkingSet = int.MaxValue
                };
            }

            var handle = GCHandle.Alloc(systemFileCacheInformation, GCHandleType.Pinned);
            try
            {
                NtSetSystemInformation(SystemFileCacheInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(systemFileCacheInformation));
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

        try
        {
            var fileCacheSize = IntPtr.Subtract(IntPtr.Zero, 1); // Flush
            return SetSystemFileCacheSize(fileCacheSize, fileCacheSize, 0);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Optimize the modified page list (WinMemoryCleaner specification).
    /// </summary>
    private static bool OptimizeModifiedPageList()
    {
        SetIncreasePrivilege(SeProfSingleProcessName);

        var handle = GCHandle.Alloc(MemoryFlushModifiedList, GCHandleType.Pinned);
        try
        {
            int status = NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf<int>());
            return status == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    /// <summary>
    /// Optimize the combined page list (WinMemoryCleaner specification).
    /// </summary>
    private static bool OptimizeCombinedPageList()
    {
        SetIncreasePrivilege(SeProfSingleProcessName);

        var memoryCombineInformationEx = new MemoryCombineInformationEx();
        var handle = GCHandle.Alloc(memoryCombineInformationEx, GCHandleType.Pinned);

        try
        {
            int status = NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryCombineInformationEx));
            return status == 0;
        }
        catch
        {
            return false;
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
    private static bool OptimizeModifiedFileCache()
    {
        bool anySuccess = false;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive == null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
                continue;

            try
            {
                using var handle = OpenVolumeHandle(drive.Name);
                if (handle == null || handle.IsInvalid)
                    continue;

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
            catch
            {
                // Suppressed
            }
        }

        return anySuccess;
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
    private static bool OptimizeRegistryCache()
    {
        try
        {
            int status = NtSetSystemInformation(SystemRegistryReconciliationInformation, IntPtr.Zero, 0);
            return status == 0;
        }
        catch
        {
            return false;
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
            { MemoryTargetType.WorkingSet,             ("Process Working Sets",       "Global NT Kernel working set sweep + process trim with SeDebugPrivilege.", true, "SAFE TO TRIM") },
            { MemoryTargetType.StandbyList,             ("Standby List (Full)",        "Clears entire cached RAM pool from closed applications. Maximum reclaim.", true, "100% SAFE") },
            { MemoryTargetType.StandbyListLowPriority,  ("Standby (Low Priority)",     "Purges only low-priority cached pages. Gentle reclaim with minimal disruption.", true, "GENTLE TRIM") },
            { MemoryTargetType.SystemFileCache,        ("System File Cache",          "Flushes Windows filesystem cache via NT Information & Win32 CacheSize.", true, "SAFE TO FLUSH") },
            { MemoryTargetType.ModifiedPageList,       ("Modified Page List",         "Writes modified dirty pages to disk, then clears them from active RAM.", true, "DIRTY FLUSH") },
            { MemoryTargetType.CombinedPageList,       ("Combined Page List",         "Triggers memory page combiner (de-duplicated identical physical pages).", true, "SAFE DE-DUP") },
            { MemoryTargetType.ModifiedFileCache,      ("Volume File Cache",          "Flushes volume write order & discards volume cache on fixed drive handles.", true, "DISK FLUSH") },
            { MemoryTargetType.RegistryCache,          ("Registry Cache",             "Reconciles & flushes cached registry hives from active RAM.", true, "SAFE TO FLUSH") }
        };

    private static (string DisplayName, string Description, bool IsAvailableOnThisOs, string SafetyBadge) TargetDescription(MemoryTargetType t)
    {
        return _targetDescriptions.TryGetValue(t, out var d) ? d : (t.ToString(), "", true, "SAFE");
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

