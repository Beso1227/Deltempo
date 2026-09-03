using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine();

        string cmd = args.Length > 0 ? args[0].ToLowerInvariant().TrimStart('-') : "help";

        int exitCode = 0;
        switch (cmd)
        {
            case "test":
                Console.WriteLine("  🧪 Running Deltempo Internal Diagnostics...");
                var mem = MemoryOptimizerService.GetMemoryInfo();
                var targets = CleanerService.GetDefaultTargets();
                bool ok = mem.TotalPhysicalBytes > 0 && targets.Count >= 20;
                Console.WriteLine($"  ✓ Engine Status: {(ok ? "PASS" : "FAIL")}");
                Console.WriteLine($"  ✓ Discovered Scopes: {targets.Count} targets");
                Console.WriteLine($"  ✓ RAM Engine: {mem.FormattedUsed} used / {mem.FormattedTotal} total");
                exitCode = ok ? 0 : 1;
                break;

            case "scan":
            case "s":
                exitCode = await HandleScanAsync(args);
                break;

            case "clean":
            case "c":
                exitCode = await HandleCleanAsync(args);
                break;

            case "boost":
            case "ram":
            case "b":
                exitCode = await HandleBoostAsync(args);
                break;

            case "startup":
            case "start":
                exitCode = await HandleStartupAsync(args);
                break;

            case "large":
            case "big":
            case "bigfiles":
            case "disk":
            case "l":
                exitCode = await HandleLargeFilesAsync(args);
                break;

            case "procs":
            case "proc":
            case "p":
                exitCode = await HandleProcsAsync(args);
                break;

            case "status":
            case "info":
            case "i":
                exitCode = HandleStatus(args);
                break;

            case "update":
            case "u":
                exitCode = await HandleUpdateAsync(args);
                break;

            case "kill":
            case "close":
                exitCode = HandleKill(args);
                break;

            case "help":
            case "h":
            case "?":
            default:
                PrintHelp();
                exitCode = 0;
                break;
        }

        Console.WriteLine();
        Console.Out.Flush();
        return exitCode;
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"  ██████╗ ███████╗██╗  ████████╗███████╗███╗   ███╗██████╗  ██████╗ 
  ██╔══██╗██╔════╝██║  ╚══██╔══╝██╔════╝████╗ ████║██╔══██╗██╔═══██╗
  ██║  ██║█████╗  ██║     ██║   █████╗  ██╔████╔██║██████╔╝██║   ██║
  ██║  ██║██╔══╝  ██║     ██║   ██╔══╝  ██║╚██╔╝██║██╔═══╝ ██║   ██║
  ██████╔╝███████╗███████╗██║   ███████╗██║ ╚═╝ ██║██║     ╚██████╔╝
  ╚═════╝ ╚══════╝╚══════╝╚═╝   ╚══════╝╚═╝     ╚═╝╚═╝      ╚═════╝ ");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"       D E L T E M P O  —  Precision Windows Optimizer (v{UpdateService.CurrentVersion.ToString(3)})\n");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Usage: deltempo <command> [subcommand|target] [options]\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  CLEANUP & CACHE COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("scan [category]", "Scan junk & cache scopes (e.g. deltempo scan gpu, deltempo scan temp)");
        PrintCmdRow("clean [category]", "Clean temporary caches & shaders with Safety Shield protection");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  STORAGE & BIG FILES COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("large [path]", "Find large space-hogs (options: --min, --type, --safe, --sort, --top)");
        PrintCmdRow("large clean", "Recycle AI-verified safe large files to Recycle Bin (--dry-run, --yes)");
        PrintCmdRow("large inspect <file>", "Run AI safety analysis on a specific file (verdict, origin, impact)");
        PrintCmdRow("large delete <file>", "Safely move a specific file to Windows Recycle Bin");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  PERFORMANCE & RAM COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("boost", "Purge background process working sets & standby cache");
        PrintCmdRow("boost --all", "Deep purge all 8 Windows NT Kernel memory zones");
        PrintCmdRow("boost --standby", "Purge closed application standby page list");
        PrintCmdRow("boost --cache", "Flush and reset Windows System File Cache");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  SYSTEM & PROCESS COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("startup", "List Windows startup apps and boot impact ratings");
        PrintCmdRow("startup disable <app>", "Disable a startup application from launching on boot");
        PrintCmdRow("startup enable <app>", "Re-enable a previously disabled startup application");
        PrintCmdRow("procs", "List heavy background memory apps (>80 MB)");
        PrintCmdRow("procs trim <pid|name>", "Trim working set memory of a specific process");
        PrintCmdRow("procs kill <pid|name>", "Terminate a heavy runaway background process");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  UTILITY & STATUS COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("status", "System telemetry dashboard with visual ASCII meters & admin status");
        PrintCmdRow("update", "Check for newer releases on GitHub");
        PrintCmdRow("kill", "Close running Deltempo background instances");
        PrintCmdRow("help", "Display this interactive help guide");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  GLOBAL OPTIONS:");
        Console.ResetColor();
        PrintOptRow("--safe / --unsafe", "Toggle 24-hour file modification protection [Default: ON]");
        PrintOptRow("--dry-run, -d", "Simulate clean actions without deleting any files");
        PrintOptRow("--yes, -y", "Bypass interactive confirmation prompts (for scripts/CI)");
        PrintOptRow("--json, -j", "Output results in structured machine-readable JSON");
        PrintOptRow("--silent, -s", "Run silently without console output (exit code only)");
        PrintOptRow("--export <file>", "Export a timestamped audit log to the given path");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  QUICK EXAMPLES:");
        Console.ResetColor();
        Console.WriteLine("    deltempo scan temp");
        Console.WriteLine("    deltempo clean --dry-run");
        Console.WriteLine("    deltempo clean gpu --yes");
        Console.WriteLine("    deltempo boost --all");
        Console.WriteLine("    deltempo large Downloads --min 100MB");
        Console.WriteLine("    deltempo large clean --safe-only");
        Console.WriteLine("    deltempo large inspect \"C:\\Windows\\Temp\\stale_driver.exe\"");
        Console.WriteLine("    deltempo startup disable Discord");
        Console.WriteLine("    deltempo procs kill Chrome");
        Console.WriteLine("    deltempo status --json");
    }

    private static void PrintCmdRow(string name, string desc)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"    {name,-22} ");
        Console.ResetColor();
        Console.WriteLine(desc);
    }

    private static void PrintOptRow(string opt, string desc)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"    {opt,-20} ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(desc);
        Console.ResetColor();
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

    // ─── CLEAN COMMAND ─────────────────────────────────────────────────

    private static async Task<int> HandleCleanAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool safeMode = !HasFlag(args, "--unsafe");
        bool cleanAll = HasFlag(args, "--all");
        bool dryRun = HasFlag(args, "--dry-run", "-d");
        bool yesPrompt = HasFlag(args, "--yes", "-y");
        bool silent = HasFlag(args, "--silent", "-s");

        string? exportPath = GetOptionValue(args, "--export");
        string? filter = GetFilterKeyword(args, 1);

        var allTargets = CleanerService.GetDefaultTargets();
        var selectedTargets = allTargets
            .Where(t => cleanAll || !t.IsOrphanedAppFolder)
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

            using var dryCts = new CancellationTokenSource();
            var dryScanner = new CleanerService();
            await Task.WhenAll(selectedTargets.Select(t => dryScanner.ScanFolderAsync(t, (msg, lvl) => { }, dryCts.Token)));

            long dryTotal = selectedTargets.Sum(t => t.SizeBytes);
            int dryFiles = selectedTargets.Sum(t => t.FileCount);

            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    dryRun = true,
                    totalReclaimableBytes = dryTotal,
                    formattedTotal = TargetFolderInfo.FormatBytes(dryTotal),
                    totalFiles = dryFiles,
                    categories = selectedTargets.Select(t => new { t.Name, t.FormattedSize, t.FileCount })
                }, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            foreach (var t in selectedTargets.Where(t => t.SizeBytes > 0))
            {
                Console.WriteLine($"    • Would purge {t.Name}: {t.FormattedSize} ({t.FileCount} files)");
            }
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Dry Run Complete: Estimated {TargetFolderInfo.FormatBytes(dryTotal)} in {dryFiles:N0} files will be reclaimed.");
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

    // ─── BOOST / RAM COMMAND ───────────────────────────────────────────

    private static async Task<int> HandleBoostAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        bool silent = HasFlag(args, "--silent", "-s");
        bool deepAll = HasFlag(args, "--all", "--deep");
        bool standbyOnly = HasFlag(args, "--standby");
        bool workingSetsOnly = HasFlag(args, "--workingsets");
        bool modifiedOnly = HasFlag(args, "--modified");
        bool cacheOnly = HasFlag(args, "--cache");

        var beforeMem = MemoryOptimizerService.GetMemoryInfo();

        List<MemoryTargetType>? selectedTargets = null;
        string boostType = "Standard RAM Boost";

        if (deepAll)
        {
            boostType = "Deep 8-Zone NT Kernel Purge";
            selectedTargets = new List<MemoryTargetType>
            {
                MemoryTargetType.WorkingSet,
                MemoryTargetType.StandbyList,
                MemoryTargetType.StandbyListLowPriority,
                MemoryTargetType.ModifiedPageList,
                MemoryTargetType.CombinedPageList,
                MemoryTargetType.SystemFileCache,
                MemoryTargetType.ModifiedFileCache,
                MemoryTargetType.RegistryCache
            };
        }
        else if (standbyOnly)
        {
            boostType = "Standby Cache Purge";
            selectedTargets = new List<MemoryTargetType> { MemoryTargetType.StandbyList, MemoryTargetType.StandbyListLowPriority };
        }
        else if (workingSetsOnly)
        {
            boostType = "Process Working Sets Trim";
            selectedTargets = new List<MemoryTargetType> { MemoryTargetType.WorkingSet };
        }
        else if (cacheOnly)
        {
            boostType = "System File Cache Reset";
            selectedTargets = new List<MemoryTargetType> { MemoryTargetType.SystemFileCache };
        }
        else if (modifiedOnly)
        {
            boostType = "Modified Page Flush";
            selectedTargets = new List<MemoryTargetType> { MemoryTargetType.ModifiedPageList };
        }
        else
        {
            // Default smart boost: Working sets + Standby list
            selectedTargets = new List<MemoryTargetType>
            {
                MemoryTargetType.WorkingSet,
                MemoryTargetType.StandbyList,
                MemoryTargetType.StandbyListLowPriority,
                MemoryTargetType.SystemFileCache
            };
        }

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ⚡ [Deltempo] Executing {boostType}...");
            Console.WriteLine($"     Before: {beforeMem.FormattedUsed} used / {beforeMem.FormattedTotal} ({beforeMem.UsedPercent:F0}% Used)");
            Console.ResetColor();
        }

        var res = await MemoryOptimizerService.OptimizeRamAsync(selectedTargets?.ToArray());
        var afterMem = MemoryOptimizerService.GetMemoryInfo();
        long actualFreed = Math.Max(res.ReclaimedBytes, Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes));

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                boostType = boostType,
                executionTimeMs = res.ExecutionTimeMs,
                processesOptimized = res.ProcessesOptimized,
                reclaimedBytes = actualFreed,
                formattedReclaimed = TargetFolderInfo.FormatBytes(actualFreed),
                before = beforeMem,
                after = afterMem,
                areaResults = res.AreaResults.Select(a => new { a.Target, a.Success, a.FormattedFreed })
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (!silent)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ {boostType} Completed in {res.ExecutionTimeMs}ms!");
            Console.WriteLine($"     • Memory Reclaimed:   {TargetFolderInfo.FormatBytes(actualFreed)}");
            Console.WriteLine($"     • Processes Trimmed:  {res.ProcessesOptimized}");
            Console.WriteLine($"     • RAM Now In Use:     {afterMem.FormattedUsed} ({afterMem.UsedPercent:F0}%) — Available: {afterMem.FormattedAvailable}");
            Console.ResetColor();
        }

        return 0;
    }

    // ─── LARGE FILES (BIG FILES) COMMAND ───────────────────────────────

    private static async Task<int> HandleLargeFilesAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        string subCmd = args.Length > 1 && !args[1].StartsWith("-") ? args[1].ToLowerInvariant() : "scan";

        // Subcommand: inspect <file>
        if (subCmd == "inspect" || subCmd == "info")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo large inspect <file_path>");
                return 1;
            }
            string targetFile = args[2].Trim('"', '\'');
            return HandleInspectLargeFile(targetFile, isJson);
        }

        // Subcommand: delete / rm <file>
        if (subCmd == "delete" || subCmd == "rm")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo large delete <file_path>");
                return 1;
            }
            string targetFile = args[2].Trim('"', '\'');
            return HandleDeleteLargeFile(targetFile, HasFlag(args, "--yes", "-y"));
        }

        // Subcommand: clean / purge
        bool isCleanMode = subCmd == "clean" || subCmd == "purge" || HasFlag(args, "--clean");

        // Scope / path parsing
        string scope = "ALL";
        if (args.Length > 1 && !args[1].StartsWith("-") && subCmd != "scan" && subCmd != "find" && subCmd != "clean" && subCmd != "purge")
        {
            scope = args[1];
        }
        else if (args.Length > 2 && !args[2].StartsWith("-"))
        {
            scope = args[2];
        }

        string? scopeOpt = GetOptionValue(args, "--scope", "--path", "--drive");
        if (!string.IsNullOrEmpty(scopeOpt))
        {
            scope = scopeOpt;
        }

        // Options
        long minBytes = ParseBytes(GetOptionValue(args, "--min", "-m"), 50L * 1024 * 1024);
        int topLimit = int.TryParse(GetOptionValue(args, "--top", "-n"), out int n) ? n : 35;
        string? typeFilter = GetOptionValue(args, "--type");
        string? extFilter = GetOptionValue(args, "--ext");
        bool safeOnly = HasFlag(args, "--safe", "--safe-only", "--ai-safe");
        bool protectedOnly = HasFlag(args, "--protected");
        bool dryRun = HasFlag(args, "--dry-run", "-d");
        bool yesPrompt = HasFlag(args, "--yes", "-y");
        bool cleanAll = HasFlag(args, "--all");

        if (!isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🐘 [Deltempo] AI Large File Hunter (>{TargetFolderInfo.FormatBytes(minBytes)}) on '{scope}'...\n");
            Console.ResetColor();
        }

        var files = await LargeFileHunterService.ScanLargeFilesAsync(minBytes, scope);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(extFilter))
        {
            if (!extFilter.StartsWith(".")) extFilter = "." + extFilter;
            files = files.Where(f => f.FileName.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            files = files.Where(f => f.Category.Contains(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (safeOnly)
        {
            files = files.Where(f => f.IsAiSafe).ToList();
        }
        else if (protectedOnly)
        {
            files = files.Where(f => !f.IsAiSafe).ToList();
        }

        // Sorting
        string sort = GetOptionValue(args, "--sort") ?? "size";
        files = sort.ToLowerInvariant() switch
        {
            "date" or "time" => files.OrderByDescending(f => f.LastModified).ToList(),
            "name" => files.OrderBy(f => f.FileName).ToList(),
            _ => files.OrderByDescending(f => f.SizeBytes).ToList()
        };

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (files.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ No large files found matching the given criteria.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("  ┌──────────────┬──────────────────────────────┬─────────────────────┬──────────────────────────────────────┐");
        Console.WriteLine($"  │ {"SIZE",-12} │ {"AI SAFETY VERDICT",-28} │ {"CATEGORY",-19} │ {"FILE NAME",-36} │");
        Console.WriteLine("  ├──────────────┼──────────────────────────────┼─────────────────────┼──────────────────────────────────────┤");

        foreach (var f in files.Take(topLimit))
        {
            string name = f.FileName.Length > 36 ? f.FileName.Substring(0, 33) + "..." : f.FileName;
            string verdict = f.AiVerdict.Length > 28 ? f.AiVerdict.Substring(0, 25) + "..." : f.AiVerdict;

            Console.Write($"  │ {f.FormattedSize,-12} │ ");
            if (f.IsAiSafe)
                Console.ForegroundColor = ConsoleColor.Green;
            else
                Console.ForegroundColor = ConsoleColor.Red;

            Console.Write($"{verdict,-28}");
            Console.ResetColor();
            Console.WriteLine($" │ {f.Category,-19} │ {name,-36} │");
        }
        Console.WriteLine("  └──────────────┴──────────────────────────────┴─────────────────────┴──────────────────────────────────────┘");

        long totalBytes = files.Sum(x => x.SizeBytes);
        var safeFiles = files.Where(x => x.IsAiSafe).ToList();
        long safeBytes = safeFiles.Sum(x => x.SizeBytes);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Discovered {files.Count} large files ({TargetFolderInfo.FormatBytes(totalBytes)}).");
        Console.WriteLine($"  ✓ AI Identified {safeFiles.Count} safe to clean ({TargetFolderInfo.FormatBytes(safeBytes)}): Stale installers, dumps, temp.");
        Console.ResetColor();

        // If Clean / Purge action requested
        if (isCleanMode)
        {
            var toRecycle = safeOnly ? safeFiles : (cleanAll ? files : safeFiles);
            if (toRecycle.Count == 0)
            {
                Console.WriteLine("  • No matching files selected for recycling.");
                return 0;
            }

            if (dryRun)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  [DRY RUN] Would recycle {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin.");
                foreach (var item in toRecycle.Take(15))
                {
                    Console.WriteLine($"    • Would move: {item.FileName} ({item.FormattedSize})");
                }
                Console.ResetColor();
                return 0;
            }

            if (!yesPrompt)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\n  ⚠️ Move {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin? (y/N): ");
                Console.ResetColor();
                var key = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(key) || (!key.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !key.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("  • Recycling cancelled by user.");
                    return 0;
                }
            }

            Console.WriteLine($"\n  • Recycling {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin...");
            var (succ, fail, freed) = LargeFileHunterService.BatchMoveToRecycleBin(toRecycle);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully recycled {succ} files ({TargetFolderInfo.FormatBytes(freed)} freed) with undo capability.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  💡 Tip: Run 'deltempo large clean --safe-only' to safely recycle {safeFiles.Count} safe files ({TargetFolderInfo.FormatBytes(safeBytes)}).");
            Console.ResetColor();
        }

        return 0;
    }

    private static int HandleInspectLargeFile(string filePath, bool isJson)
    {
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ File not found: '{filePath}'");
            Console.ResetColor();
            return 1;
        }

        var fi = new FileInfo(filePath);
        var (category, _) = LargeFileHunterService.ClassifyFileCategory(fi.Extension);
        var ai = AiFileSafetyService.AnalyzeFile(fi.FullName, fi.Name, category, fi.Length, fi.LastWriteTime);

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                path = fi.FullName,
                name = fi.Name,
                sizeBytes = fi.Length,
                formattedSize = TargetFolderInfo.FormatBytes(fi.Length),
                created = fi.CreationTime,
                lastModified = fi.LastWriteTime,
                category = category,
                ai = ai
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  🔍 [AI Large File Inspection] {fi.Name}\n");
        Console.ResetColor();

        Console.WriteLine($"  • Full Path:      {fi.FullName}");
        Console.WriteLine($"  • File Size:      {TargetFolderInfo.FormatBytes(fi.Length)} ({fi.Length:N0} bytes)");
        Console.WriteLine($"  • Category:       {category}");
        Console.WriteLine($"  • Last Modified:  {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

        Console.Write("  • AI Verdict:     ");
        if (ai.IsSafeToAutoClean)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SAFE TO DELETE (Score: {ai.SafetyScore}/100)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"PROTECTED / KEEP (Score: {ai.SafetyScore}/100)");
        }
        Console.ResetColor();

        Console.WriteLine($"  • Inferred Origin: {ai.Origin}");
        Console.WriteLine($"  • System Impact:   {ai.Impact}");
        Console.WriteLine($"  • AI Explanation:  {ai.Explanation}");

        return 0;
    }

    private static int HandleDeleteLargeFile(string filePath, bool yesPrompt)
    {
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ File not found: '{filePath}'");
            Console.ResetColor();
            return 1;
        }

        var fi = new FileInfo(filePath);
        if (!yesPrompt)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  ⚠️ Move '{fi.Name}' ({TargetFolderInfo.FormatBytes(fi.Length)}) to Recycle Bin? (y/N): ");
            Console.ResetColor();
            var key = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(key) || (!key.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !key.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("  • Deletion cancelled.");
                return 0;
            }
        }

        bool success = LargeFileHunterService.MoveToRecycleBin(fi.FullName);
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully moved '{fi.Name}' to Recycle Bin with undo capability.");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ Failed to move '{fi.Name}' to Recycle Bin.");
            Console.ResetColor();
            return 1;
        }
    }

    // ─── STARTUP COMMAND ───────────────────────────────────────────────

    private static async Task<int> HandleStartupAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        string subCmd = args.Length > 1 && !args[1].StartsWith("-") ? args[1].ToLowerInvariant() : "list";

        if (subCmd == "disable" || subCmd == "off")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo startup disable <app_name>");
                return 1;
            }
            string appName = args[2].Trim('"', '\'');
            return await ToggleStartupAppAsync(appName, false);
        }

        if (subCmd == "enable" || subCmd == "on")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo startup enable <app_name>");
                return 1;
            }
            string appName = args[2].Trim('"', '\'');
            return await ToggleStartupAppAsync(appName, true);
        }

        var items = await StartupManagerService.GetStartupItemsAsync();

        if (HasFlag(args, "--high"))
        {
            items = items.Where(i => i.Impact == BootImpact.High).ToList();
        }
        else if (HasFlag(args, "--enabled"))
        {
            items = items.Where(i => i.IsEnabled).ToList();
        }
        else if (HasFlag(args, "--disabled"))
        {
            items = items.Where(i => !i.IsEnabled).ToList();
        }

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  🚀 [Deltempo] Windows Startup Apps ({items.Count} items found)\n");
        Console.ResetColor();

        Console.WriteLine("  ┌──────────┬──────────────┬──────────────────────────────┬─────────────────────────┐");
        Console.WriteLine($"  │ {"STATUS",-8} │ {"IMPACT",-12} │ {"APPLICATION",-28} │ {"PUBLISHER",-23} │");
        Console.WriteLine("  ├──────────┼──────────────┼──────────────────────────────┼─────────────────────────┤");
        foreach (var item in items)
        {
            string status = item.IsEnabled ? "ENABLED" : "DISABLED";
            Console.Write($"  │ ");
            if (item.IsEnabled)
                Console.ForegroundColor = ConsoleColor.Green;
            else
                Console.ForegroundColor = ConsoleColor.DarkGray;

            Console.Write($"{status,-8}");
            Console.ResetColor();
            Console.Write($" │ ");

            if (item.Impact == BootImpact.High)
                Console.ForegroundColor = ConsoleColor.Red;
            else if (item.Impact == BootImpact.Medium)
                Console.ForegroundColor = ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Green;

            Console.Write($"{item.ImpactText,-12}");
            Console.ResetColor();

            string name = item.Name.Length > 28 ? item.Name.Substring(0, 25) + "..." : item.Name;
            string pub = item.Publisher.Length > 23 ? item.Publisher.Substring(0, 20) + "..." : item.Publisher;
            Console.WriteLine($" │ {name,-28} │ {pub,-23} │");
        }
        Console.WriteLine("  └──────────┴──────────────┴──────────────────────────────┴─────────────────────────┘");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("\n  💡 Tip: Use 'deltempo startup disable <name>' to boost system boot times.");
        Console.ResetColor();
        return 0;
    }

    private static async Task<int> ToggleStartupAppAsync(string name, bool enable)
    {
        var items = await StartupManagerService.GetStartupItemsAsync();
        var match = items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                              i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ Startup item '{name}' not found. Run 'deltempo startup' to list items.");
            Console.ResetColor();
            return 1;
        }

        bool ok = StartupManagerService.ToggleStartupItem(match, enable);
        if (ok)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully {(enable ? "enabled" : "disabled")} startup application: '{match.Name}' ({match.Location})");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠️ Item '{match.Name}' is already {(enable ? "enabled" : "disabled")} or requires Administrator rights.");
            Console.ResetColor();
            return 1;
        }
    }

    // ─── PROCESSES COMMAND ─────────────────────────────────────────────

    private static async Task<int> HandleProcsAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        string subCmd = args.Length > 1 && !args[1].StartsWith("-") ? args[1].ToLowerInvariant() : "list";

        if (subCmd == "trim")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo procs trim <pid|process_name>");
                return 1;
            }
            return await HandleProcessActionAsync(args[2], isKill: false);
        }

        if (subCmd == "kill" || subCmd == "close")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  ⚠️ Usage: deltempo procs kill <pid|process_name>");
                return 1;
            }
            return await HandleProcessActionAsync(args[2], isKill: true);
        }

        var procs = await ProcessOptimizerService.GetHeavyProcessesAsync();

        int topLimit = int.TryParse(GetOptionValue(args, "--top", "-n"), out int n) ? n : 50;

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(procs.Take(topLimit), new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  🛑 [Deltempo] Heavy Background Memory Apps (>80 MB)\n");
        Console.ResetColor();

        Console.WriteLine("  ┌──────────┬──────────────┬────────────────────────────────────────────────────┐");
        Console.WriteLine($"  │ {"PID",-8} │ {"MEMORY",-12} │ {"PROCESS NAME",-50} │");
        Console.WriteLine("  ├──────────┼──────────────┼────────────────────────────────────────────────────┤");
        foreach (var p in procs.Take(topLimit))
        {
            string name = p.DisplayName.Length > 50 ? p.DisplayName.Substring(0, 47) + "..." : p.DisplayName;
            Console.WriteLine($"  │ {p.ProcessId,-8} │ {p.FormattedMemory,-12} │ {name,-50} │");
        }
        Console.WriteLine("  └──────────┴──────────────┴────────────────────────────────────────────────────┘");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("\n  💡 Tip: Use 'deltempo procs trim <pid>' or 'deltempo procs kill <pid>' to optimize memory.");
        Console.ResetColor();
        return 0;
    }

    private static async Task<int> HandleProcessActionAsync(string target, bool isKill)
    {
        var procs = await ProcessOptimizerService.GetHeavyProcessesAsync();
        ProcessMemoryInfo? match = null;

        if (int.TryParse(target, out int pid))
        {
            match = procs.FirstOrDefault(p => p.ProcessId == pid || p.ProcessIds.Contains(pid));
        }
        else
        {
            match = procs.FirstOrDefault(p => p.ProcessName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                                              p.FriendlyName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                                              p.ProcessName.Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        if (match == null)
        {
            if (int.TryParse(target, out int rawPid))
            {
                bool rawResult = isKill ? ProcessOptimizerService.SafeTerminateProcess(rawPid) : ProcessOptimizerService.TrimProcessMemory(rawPid);
                if (rawResult)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✓ Successfully {(isKill ? "terminated" : "trimmed")} PID {rawPid}.");
                    Console.ResetColor();
                    return 0;
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ❌ Process '{target}' not found in active memory.");
            Console.ResetColor();
            return 1;
        }

        bool ok = isKill ? ProcessOptimizerService.SafeTerminateProcess(match.ProcessIds.Count > 0 ? match.ProcessIds : new List<int> { match.ProcessId })
                         : ProcessOptimizerService.TrimProcessMemory(match.ProcessIds.Count > 0 ? match.ProcessIds : new List<int> { match.ProcessId });

        if (ok)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully {(isKill ? "terminated" : "trimmed memory for")} '{match.DisplayName}' ({match.FormattedMemory}).");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠️ Process '{match.DisplayName}' is protected by Windows whitelist or access was denied.");
            Console.ResetColor();
            return 1;
        }
    }

    // ─── STATUS COMMAND ────────────────────────────────────────────────

    private static int HandleStatus(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        var drive = DriveTelemetryService.GetSystemDriveTelemetry();
        var mem = MemoryOptimizerService.GetMemoryInfo();
        bool isAdmin = ElevationService.IsRunAsAdmin();

        if (isJson)
        {
            var obj = new { drive, memory = mem, isAdmin = isAdmin };
            Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  📊 [Deltempo] System Telemetry Dashboard\n");
        Console.ResetColor();

        Console.Write("  🛡️ Privileges:    ");
        if (isAdmin)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ELEVATED (Full Windows NT Kernel Access)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("STANDARD USER (Relaunch as Admin for full system clean)");
        }
        Console.ResetColor();

        double driveUsedPct = 100.0 - drive.FreePercentage;
        Console.WriteLine($"  💾 OS Drive ({drive.DriveLetter}): [{GetProgressBar(driveUsedPct, 20)}] {drive.FormattedFree} free of {drive.FormattedTotal} ({drive.FreePercentage:F1}% Free)");
        Console.WriteLine($"  ⚡ Memory (RAM):  [{GetProgressBar(mem.UsedPercent, 20)}] {mem.FormattedUsed} used of {mem.FormattedTotal} ({mem.UsedPercent:F0}% Used)");
        Console.WriteLine($"  📦 Standby Cache: {mem.FormattedSystemCache} reclaimable from closed programs");

        return 0;
    }

    // ─── UPDATE COMMAND ────────────────────────────────────────────────

    private static async Task<int> HandleUpdateAsync(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  🔄 [Deltempo] Checking for updates on GitHub...");
        Console.ResetColor();

        var release = await UpdateService.CheckForUpdatesAsync();
        if (release != null && release.IsNewer)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ New version available: {release.TagName} (Current: v{UpdateService.CurrentVersion.ToString(3)})");
            Console.WriteLine($"  📥 Download: {release.DownloadUrl}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ You are running the latest version of Deltempo (v{UpdateService.CurrentVersion.ToString(3)}).");
            Console.ResetColor();
        }

        return 0;
    }

    // ─── KILL COMMAND ──────────────────────────────────────────────────

    private static int HandleKill(string[] args)
    {
        int myPid = Process.GetCurrentProcess().Id;
        var procs = Process.GetProcessesByName("Deltempo")
            .Concat(Process.GetProcessesByName("WinTempCleaner"))
            .Where(p => p.Id != myPid)
            .ToList();

        int killed = 0;
        foreach (var p in procs)
        {
            try
            {
                p.Kill();
                p.WaitForExit(1000);
                killed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ Failed to terminate PID {p.Id}: {ex.Message}");
            }
            finally
            {
                p.Dispose();
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Terminated {killed} running Deltempo process(es).");
        Console.ResetColor();
        return 0;
    }

    // ─── HELPER FUNCTIONS ──────────────────────────────────────────────

    private static bool HasFlag(string[] args, params string[] flags)
    {
        return args.Any(a => flags.Contains(a, StringComparer.OrdinalIgnoreCase));
    }

    private static string? GetOptionValue(string[] args, params string[] optionNames)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (optionNames.Contains(args[i], StringComparer.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static string? GetFilterKeyword(string[] args, int defaultPos)
    {
        string? fromOpt = GetOptionValue(args, "--category", "--scope", "--filter");
        if (!string.IsNullOrEmpty(fromOpt)) return fromOpt;

        if (args.Length > defaultPos && !args[defaultPos].StartsWith("-"))
        {
            return args[defaultPos];
        }
        return null;
    }

    private static bool MatchesFilter(TargetFolderInfo target, string filter)
    {
        return target.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               target.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               target.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static long ParseBytes(string? raw, long defaultVal)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaultVal;
        string clean = raw.Trim().ToUpperInvariant();

        if (clean.EndsWith("GB") && double.TryParse(clean[..^2], out double gbs))
            return (long)(gbs * 1024 * 1024 * 1024);
        if (clean.EndsWith("G") && double.TryParse(clean[..^1], out double g))
            return (long)(g * 1024 * 1024 * 1024);
        if (clean.EndsWith("MB") && double.TryParse(clean[..^2], out double mbs))
            return (long)(mbs * 1024 * 1024);
        if (clean.EndsWith("M") && double.TryParse(clean[..^1], out double m))
            return (long)(m * 1024 * 1024);
        if (clean.EndsWith("KB") && double.TryParse(clean[..^2], out double kbs))
            return (long)(kbs * 1024);
        if (clean.EndsWith("K") && double.TryParse(clean[..^1], out double k))
            return (long)(k * 1024);
        if (long.TryParse(clean, out long bytes))
            return bytes;

        return defaultVal;
    }

    private static string GetProgressBar(double percentage, int width = 20)
    {
        percentage = Math.Clamp(percentage, 0.0, 100.0);
        int filled = (int)Math.Round(percentage / 100.0 * width);
        int empty = width - filled;
        return new string('█', filled) + new string('░', empty);
    }
}

