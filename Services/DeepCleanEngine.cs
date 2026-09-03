using System.Diagnostics;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class DeepCleanProgress
{
    public string CurrentStage { get; set; } = "";
    public double OverallPercent { get; set; }
    public string DetailMessage { get; set; } = "";
}

public class DeepCleanResult
{
    public long DiskFreedBytes { get; set; }
    public long RamFreedBytes { get; set; }
    public int FilesDeleted { get; set; }
    public int FoldersDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public int CategoriesProcessed { get; set; }
    public bool DismCleaned { get; set; }
    public bool RestorePointsCleaned { get; set; }
    public TimeSpan Duration { get; set; }

    public string FormattedDiskFreed => TargetFolderInfo.FormatBytes(DiskFreedBytes);
    public string FormattedRamFreed => TargetFolderInfo.FormatBytes(RamFreedBytes);
    public List<string> SummaryHighlights { get; set; } = new();
}

public static class DeepCleanEngine
{
    public static async Task<DeepCleanResult> ExecuteDeepCleanAsync(
        Action<string, LogLevel>? logAction = null,
        IProgress<DeepCleanProgress>? progress = null,
        bool purgeAllRestorePoints = false,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new DeepCleanResult();

        void Report(string stage, double pct, string detail)
        {
            progress?.Report(new DeepCleanProgress
            {
                CurrentStage = stage,
                OverallPercent = pct,
                DetailMessage = detail
            });
            logAction?.Invoke($"[{stage}] {detail}", LogLevel.Info);
        }

        // =========================================================================
        // STAGE 1: Win32 Token Privilege Escalation
        // =========================================================================
        Report("Privilege Escalation", 0.05, "Activating SeBackupPrivilege & SeRestorePrivilege token rights...");
        CleanerService.EnableFileManagementPrivileges();

        if (ct.IsCancellationRequested) return result;

        // =========================================================================
        // STAGE 2: Deep RAM Optimization
        // =========================================================================
        Report("Memory Optimization", 0.15, "Flushing application Working Sets, Standby List & Modified Pages...");
        try
        {
            var ramRes = await MemoryOptimizerService.OptimizeRamAsync(null, ct);
            result.RamFreedBytes = ramRes.ReclaimedBytes;
            if (ramRes.ReclaimedBytes > 0)
            {
                result.SummaryHighlights.Add($"🧠 RAM Memory Freed: {ramRes.FormattedReclaimed}");
                logAction?.Invoke($"RAM Optimization completed: {ramRes.FormattedReclaimed} reclaimed across system memory pools.", LogLevel.Success);
            }
        }
        catch (Exception ex)
        {
            logAction?.Invoke($"RAM optimization note: {ex.Message}", LogLevel.Warning);
        }

        if (ct.IsCancellationRequested) return result;

        // =========================================================================
        // STAGE 3: Target Pool Preparation
        // =========================================================================
        Report("Target Analysis", 0.30, "Selecting all 26 system & application junk pools for deep purge...");
        var cleaner = new CleanerService();
        var allTargets = CleanerService.GetDefaultTargets();

        // Select all accessible targets
        foreach (var t in allTargets)
        {
            t.IsSelected = t.HasAccess;
        }

        var activeTargets = allTargets.Where(t => t.IsSelected).ToList();
        result.CategoriesProcessed = activeTargets.Count;

        // =========================================================================
        // STAGE 4: Clean Disk & Application Scopes
        // =========================================================================
        Report("Disk Cleanup", 0.45, $"Purging {activeTargets.Count} categories of temporary files, caches & shaders...");

        int targetIndex = 0;
        foreach (var target in activeTargets)
        {
            if (ct.IsCancellationRequested) break;
            targetIndex++;

            double pct = 0.45 + (0.30 * ((double)targetIndex / activeTargets.Count));
            progress?.Report(new DeepCleanProgress
            {
                CurrentStage = "Disk Cleanup",
                OverallPercent = pct,
                DetailMessage = $"Cleaning {target.Name} ({targetIndex}/{activeTargets.Count})..."
            });

            var (freed, files, folders, skipped) = await cleaner.CleanFolderAsync(
                target,
                safeMode24Hours: false,
                logAction: (msg, lvl) => logAction?.Invoke(msg, lvl),
                progressReport: _ => { },
                ct: ct);

            result.DiskFreedBytes += freed;
            result.FilesDeleted += files;
            result.FoldersDeleted += folders;
            result.FilesSkipped += skipped;
        }

        if (ct.IsCancellationRequested) return result;

        // =========================================================================
        // STAGE 5: Windows Component Store Scavenging (DISM)
        // =========================================================================
        if (ElevationService.IsRunAsAdmin())
        {
            Report("Component Store Cleanup", 0.80, "Scavenging superseded Windows updates via DISM (WinSxS)...");
            try
            {
                var (ok, msg) = await CleanerService.RunDismComponentCleanupAsync(logAction, ct);
                result.DismCleaned = ok;
                if (ok)
                {
                    result.SummaryHighlights.Add("📦 Windows Component Store (WinSxS) superseded packages purged.");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"DISM execution note: {ex.Message}", LogLevel.Info);
            }
        }

        if (ct.IsCancellationRequested) return result;

        // =========================================================================
        // STAGE 6: Windows System Restore Points Cleanup (VSS)
        // =========================================================================
        if (ElevationService.IsRunAsAdmin())
        {
            Report("Restore Points Cleanup", 0.90, purgeAllRestorePoints
                ? "Purging all Volume Shadow Copies & Restore Points..."
                : "Purging older System Restore Points (preserving latest restore point)...");
            try
            {
                var (ok, reclaimed, msg) = await CleanerService.CleanRestorePointsAsync(purgeAllRestorePoints, logAction, ct);
                result.RestorePointsCleaned = ok;
                if (reclaimed > 0)
                {
                    result.DiskFreedBytes += reclaimed;
                    result.SummaryHighlights.Add($"🛡️ System Restore Points: {TargetFolderInfo.FormatBytes(reclaimed)} reclaimed.");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Restore point note: {ex.Message}", LogLevel.Info);
            }
        }

        // =========================================================================
        // STAGE 7: Finalize & Summary
        // =========================================================================
        sw.Stop();
        result.Duration = sw.Elapsed;

        result.SummaryHighlights.Insert(0, $"💾 Total Disk Space Reclaimed: {result.FormattedDiskFreed} ({result.FilesDeleted:N0} files deleted)");

        Report("Complete", 1.0, $"1-Click Deep Clean completed! Reclaimed {result.FormattedDiskFreed} disk and {result.FormattedRamFreed} RAM.");

        return result;
    }
}
