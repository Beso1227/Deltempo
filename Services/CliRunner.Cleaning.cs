using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

// CLI command handlers: deep-clean, restore-points, scan, smart-clean and clean.
public static partial class CliRunner
{

    // ─── DEEP CLEAN (1-CLICK ALL-IN-ONE) ───────────────────────────────

    private static async Task<int> HandleDeepCleanAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool silent = HasFlag(args, "--silent", "-s");
        bool yesPrompt = HasFlag(args, "--yes", "-y");
        bool purgeAllVss = HasFlag(args, "--purge-all-restore-points");

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"  [Deltempo 1-Click Deep Clean]");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(@"  Autonomous full-system optimization: RAM flush, 26 disk scopes, DISM & VSS.");
            Console.ResetColor();
            Console.WriteLine();
        }

        if (!yesPrompt && !silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("  Execute autonomous deep clean now? [Y/n]: ");
            Console.ResetColor();
            var key = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key) && key != "y" && key != "yes")
            {
                Console.WriteLine("  Cancelled by user.");
                return 0;
            }
            Console.WriteLine();
        }

        using var cts = new CancellationTokenSource();
        var progress = new Progress<DeepCleanProgress>(p =>
        {
            if (!silent && !isJson)
            {
                int barWidth = 24;
                int filled = (int)(p.OverallPercent * barWidth);
                string bar = new string('█', filled) + new string('░', Math.Max(0, barWidth - filled));
                string detail = p.DetailMessage.Length > 48 ? p.DetailMessage.Substring(0, 45) + "..." : p.DetailMessage;
                Console.Write($"\r  [{bar}] {p.OverallPercent * 100,3:0}% | {detail.PadRight(48)}");
            }
        });

        var result = await DeepCleanEngine.ExecuteDeepCleanAsync(
            logAction: (msg, lvl) => { },
            progress: progress,
            purgeAllRestorePoints: purgeAllVss,
            ct: cts.Token).ConfigureAwait(false);

        if (!silent && !isJson)
        {
            Console.WriteLine("\n");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║              ✓ 1-CLICK DEEP CLEAN COMPLETED                     ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"  💾 Total Disk Junk Purged : {result.FormattedDiskFreed} ({result.FilesDeleted:N0} files)");
            Console.WriteLine($"  🧠 RAM Memory Recovered   : {result.FormattedRamFreed}");
            Console.WriteLine($"  📁 Categories Processed   : {result.CategoriesProcessed} scopes");
            Console.WriteLine($"  ⏱️  Execution Duration    : {result.Duration.TotalSeconds:0.1}s");
            Console.WriteLine();

            if (result.SummaryHighlights.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  Highlights:");
                Console.ResetColor();
                foreach (var h in result.SummaryHighlights)
                {
                    Console.WriteLine($"    • {h}");
                }
                Console.WriteLine();
            }
        }
        else if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                timestamp = DateTime.UtcNow,
                diskFreedBytes = result.DiskFreedBytes,
                formattedDiskFreed = result.FormattedDiskFreed,
                ramFreedBytes = result.RamFreedBytes,
                formattedRamFreed = result.FormattedRamFreed,
                filesDeleted = result.FilesDeleted,
                foldersDeleted = result.FoldersDeleted,
                categoriesProcessed = result.CategoriesProcessed,
                dismCleaned = result.DismCleaned,
                restorePointsCleaned = result.RestorePointsCleaned,
                durationSeconds = result.Duration.TotalSeconds,
                highlights = result.SummaryHighlights
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        return 0;
    }

    // ─── RESTORE POINTS COMMAND ────────────────────────────────────────

    private static async Task<int> HandleRestorePointsAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool clean = HasFlag(args, "--clean", "-c") || HasFlag(args, "--purge");
        bool purgeAll = HasFlag(args, "--all");

        if (!ElevationService.IsRunAsAdmin())
        {
            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = "Administrator privileges required to query or clean restore points." }));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ❌ Error: Managing Windows Restore Points requires elevated Administrator privileges.");
                Console.ResetColor();
            }
            return 1;
        }

        var (used, count) = CleanerService.QueryShadowStorageInfo();

        if (clean)
        {
            if (!isJson)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  🛡️ Cleaning Windows System Restore Points (Safe mode: {(purgeAll ? "Purge All" : "Keep Latest")})...");
                Console.ResetColor();
            }

            var (ok, reclaimed, msg) = await CleanerService.CleanRestorePointsAsync(purgeAll, (m, l) => { });

            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = ok,
                    reclaimedBytes = reclaimed,
                    formattedReclaimed = TargetFolderInfo.FormatBytes(reclaimed),
                    message = msg
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.WriteLine($"  {(ok ? "✓" : "⚠️")} {msg} Reclaimed: {TargetFolderInfo.FormatBytes(reclaimed)}");
                Console.ResetColor();
            }
            return ok ? 0 : 1;
        }

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                usedBytes = used,
                formattedUsed = TargetFolderInfo.FormatBytes(used),
                snapshotCount = count
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  🛡️ Windows System Restore Points & Shadow Copies (VSS):");
            Console.ResetColor();
            Console.WriteLine($"     Used Shadow Storage: {TargetFolderInfo.FormatBytes(used)}");
            Console.WriteLine($"     Detected Snapshots : {count}");
            Console.WriteLine();
            Console.WriteLine("  💡 Run 'deltempo restore-points --clean' to purge older points (safely keeping latest).");
            Console.WriteLine("  💡 Run 'deltempo restore-points --clean --all' to purge all shadow copies.");
        }

        return 0;
    }

    // ─── SCAN COMMAND ──────────────────────────────────────────────────

    private static async Task<int> HandleScanAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool silent = HasFlag(args, "--silent", "-s");
        string? filter = GetFilterKeyword(args, 1);

        var allTargets = CleanerService.GetDefaultTargets();
        var targets = string.IsNullOrWhiteSpace(filter)
            ? allTargets
            : allTargets.Where(t => MatchesFilter(t, filter)).ToList();

        if (targets.Count == 0)
        {
            if (!silent && !isJson)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⚠️ No categories matched keyword '{filter}'. Showing all available targets.");
                Console.ResetColor();
            }
            targets = allTargets;
        }

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🔍 [Deltempo] Scanning {targets.Count} Windows & User Profile directories...\n");
            Console.ResetColor();
        }

        using var cts = new CancellationTokenSource();
        var cleanerService = new CleanerService();
        var tasks = targets.Select(t => cleanerService.ScanFolderAsync(t, (msg, level) => { }, cts.Token)).ToList();
        await Task.WhenAll(tasks);

        // Sort by reclaimable size descending so largest appear at the top
        var sortedTargets = targets.OrderByDescending(t => t.SizeBytes).ToList();
        long totalBytes = sortedTargets.Sum(t => t.SizeBytes);
        int totalFiles = sortedTargets.Sum(t => t.FileCount);

        if (isJson)
        {
            var jsonObj = new
            {
                timestamp = DateTime.UtcNow,
                totalReclaimableBytes = totalBytes,
                formattedTotal = TargetFolderInfo.FormatBytes(totalBytes),
                totalFiles = totalFiles,
                filterApplied = filter,
                categories = sortedTargets.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    category = t.Category,
                    sizeBytes = t.SizeBytes,
                    formattedSize = t.FormattedSize,
                    fileCount = t.FileCount
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (!silent)
        {
            Console.WriteLine("  ┌────────────────────────────────────────┬──────────┬─────────────────┐");
            Console.WriteLine($"  │ {"CATEGORY",-38} │ {"FILES",-8} │ {"RECLAIMABLE",-15} │");
            Console.WriteLine("  ├────────────────────────────────────────┼──────────┼─────────────────┤");
            foreach (var t in sortedTargets)
            {
                Console.Write($"  │ {t.Name,-38} │ {t.FileCount,8:N0} │ ");
                if (t.SizeBytes > 1024L * 1024 * 1024)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (t.SizeBytes > 50L * 1024 * 1024)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (t.SizeBytes == 0)
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                else
                    Console.ForegroundColor = ConsoleColor.White;

                Console.Write($"{t.FormattedSize,15}");
                Console.ResetColor();
                Console.WriteLine(" │");
            }
            Console.WriteLine("  └────────────────────────────────────────┴──────────┴─────────────────┘");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Total Reclaimable Space: {TargetFolderInfo.FormatBytes(totalBytes)} ({totalFiles:N0} files found)");
            Console.ResetColor();

            if (totalBytes > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  💡 Tip: Run 'deltempo clean{(string.IsNullOrWhiteSpace(filter) ? "" : " " + filter)}' to purge these safe temporary caches.");
                Console.ResetColor();
            }
        }

        return 0;
    }

    // ─── SMART CLEAN COMMAND ───────────────────────────────────────────

    private static async Task<int> HandleSmartCleanAsync(string[] args)
    {
        var smartArgs = args.Concat(new[] { "--smart" }).ToArray();
        return await HandleCleanAsync(smartArgs);
    }

    // ─── CLEAN COMMAND ─────────────────────────────────────────────────

    private static async Task<int> HandleCleanAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool safeMode = !HasFlag(args, "--unsafe");
        bool cleanAll = HasFlag(args, "--all");
        bool smartOnly = HasFlag(args, "--smart", "--safe-only");
        bool recycleBin = HasFlag(args, "--recycle-bin", "--recycle", "-r");
        bool dryRun = HasFlag(args, "--dry-run", "-d");
        bool yesPrompt = HasFlag(args, "--yes", "-y");
        bool silent = HasFlag(args, "--silent", "-s");

        if (recycleBin)
        {
            SettingsService.Current.SendToRecycleBin = true;
        }

        string? exportPath = GetOptionValue(args, "--export");
        string? filter = GetFilterKeyword(args, 1);

        var allTargets = CleanerService.GetDefaultTargets();
        var selectedTargets = allTargets
            .Where(t => cleanAll || !t.IsOrphanedAppFolder)
            .Where(t => !smartOnly || ((t.SafetyBadge.Contains("Verified") || t.SafetyBadge.Contains("100%")) && !t.IsOrphanedAppFolder))
            .Where(t => string.IsNullOrWhiteSpace(filter) || MatchesFilter(t, filter))
            .ToList();

        if (selectedTargets.Count == 0)
        {
            if (!silent) Console.WriteLine($"  ⚠️ No targets matched filter '{filter}'. Use 'deltempo scan' to view available categories.");
            return 1;
        }

        if (dryRun)
        {
            if (!silent && !isJson)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [DRY RUN SIMULATION] Deltempo would clean {selectedTargets.Count} categories (Safety Shield: {(safeMode ? "ACTIVE" : "OFF")})\n");
                Console.ResetColor();
            }

            // Use the same CleanupPlanner path as live cleanup for accurate dry-run
            bool applyShield = safeMode;
            bool sendToRecycle = SettingsService.Current.SendToRecycleBin;
            long dryTotal = 0;
            int dryFiles = 0;
            var dryResults = new List<(string Name, long PlannedBytes, string Formatted, int PlannedFiles, int Protected, int ReviewRequired)>();

            foreach (var t in selectedTargets)
            {
                var directories = CleanerService.ResolveDirectoriesForFolderPublic(t);
                if (directories.Count == 0) continue;

                var plan = CleanupPlanner.CreatePlan(
                    scopeId: t.Id,
                    scopeName: t.Name,
                    directories: directories,
                    category: t.Category,
                    apply24HourShield: applyShield,
                    sendToRecycleBin: sendToRecycle);

                long scopePlanned = plan.Actions.Where(a => a.Action != IntendedCleanupAction.SkipProtected &&
                    a.Action != IntendedCleanupAction.SkipReviewRequired).Sum(a => a.SizeBytes);
                int scopeFiles = plan.Actions.Count(a => a.Action != IntendedCleanupAction.SkipProtected &&
                    a.Action != IntendedCleanupAction.SkipReviewRequired);

                dryTotal += scopePlanned;
                dryFiles += scopeFiles;

                dryResults.Add((t.Name, scopePlanned, TargetFolderInfo.FormatBytes(scopePlanned), scopeFiles,
                    plan.Actions.Count(a => a.Action == IntendedCleanupAction.SkipProtected),
                    plan.Actions.Count(a => a.Action == IntendedCleanupAction.SkipReviewRequired)));
            }

            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    dryRun = true,
                    safetyShieldActive = safeMode,
                    totalReclaimableBytes = dryTotal,
                    formattedTotal = TargetFolderInfo.FormatBytes(dryTotal),
                    totalFiles = dryFiles,
                    categories = dryResults.Select(r => new { r.Name, plannedBytes = r.PlannedBytes, r.Formatted, plannedFiles = r.PlannedFiles, protectedCount = r.Protected, reviewRequiredCount = r.ReviewRequired })
                }, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            foreach (var r in dryResults.Where(r => r.PlannedBytes > 0))
            {
                Console.WriteLine($"    • Would purge {r.Name}: {r.Formatted} ({r.PlannedFiles} files)");
            }
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Dry Run Complete: {TargetFolderInfo.FormatBytes(dryTotal)} in {dryFiles:N0} files would be reclaimed (after safety filtering).");
            Console.ResetColor();
            return 0;
        }

        if (!silent && !yesPrompt && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  ⚠️ Proceed with cleaning {selectedTargets.Count} categories? (y/N): ");
            Console.ResetColor();
            var key = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(key) || (!key.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !key.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("  • Cleanup cancelled by user.");
                return 0;
            }
        }

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🧹 [Deltempo] Purging {selectedTargets.Count} categories (Safety Shield: {(safeMode ? "ACTIVE" : "OFF")})...\n");
            Console.ResetColor();
        }

        using var cts = new CancellationTokenSource();
        var cleanerService = new CleanerService();
        long totalFreed = 0;
        int totalFilesDeleted = 0;
        int totalFoldersDeleted = 0;
        int totalFilesSkipped = 0;

        int currentIdx = 0;
        foreach (var target in selectedTargets)
        {
            currentIdx++;
            if (!silent && !isJson)
            {
                double pct = (double)currentIdx / selectedTargets.Count * 100.0;
                Console.Write($"\r  [{GetProgressBar(pct, 18)}] {pct,5:F0}% Cleaning {target.Name,-30}");
            }

            var (freed, filesDel, foldersDel, filesSkip) = await cleanerService.CleanFolderAsync(
                target,
                safeMode,
                (msg, level) => { },
                progress => { },
                cts.Token);

            totalFreed += freed;
            totalFilesDeleted += filesDel;
            totalFoldersDeleted += foldersDel;
            totalFilesSkipped += filesSkip;
        }

        if (!silent && !isJson)
        {
            Console.Write($"\r  [{GetProgressBar(100, 18)}]  100% Completed precision clean                      \n\n");
        }

        var summary = new CleanSummary
        {
            TotalFreedBytes = totalFreed,
            TotalFilesDeleted = totalFilesDeleted,
            TotalFoldersDeleted = totalFoldersDeleted,
            TotalFilesSkipped = totalFilesSkipped,
            ElapsedTime = TimeSpan.Zero
        };

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                totalFreedBytes = totalFreed,
                formattedFreed = summary.FormattedFreedSize,
                totalFilesDeleted = totalFilesDeleted,
                totalFoldersDeleted = totalFoldersDeleted,
                totalFilesSkipped = totalFilesSkipped,
                safetyShield = safeMode
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (!silent)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✨ CLEANUP COMPLETE: Successfully reclaimed {summary.FormattedFreedSize}!");
            Console.WriteLine($"     • Files purged:    {totalFilesDeleted:N0}");
            Console.WriteLine($"     • Folders removed: {totalFoldersDeleted:N0}");
            Console.WriteLine($"     • Files protected: {totalFilesSkipped:N0} (Shield >24h)");
            Console.ResetColor();
        }

        if (!string.IsNullOrEmpty(exportPath))
        {
            try
            {
                string report = CleanerService.GenerateAuditReport(selectedTargets, summary, safeMode);
                File.WriteAllText(exportPath, report);
                if (!silent) Console.WriteLine($"\n  📄 Audit report exported to: {exportPath}");
            }
            catch (Exception ex)
            {
                if (!silent) Console.WriteLine($"\n  ⚠️ Failed to write audit report: {ex.Message}");
            }
        }

        return 0;
    }
}
