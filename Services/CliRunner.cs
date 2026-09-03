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
        // Add top padding to separate from PowerShell prompt
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

        // Clean trailing spacing & flush buffer
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
        Console.WriteLine("  Usage: deltempo <command> [options]\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("scan", "Dry-run scan across all 25+ deep cache, shader & junk scopes");
        PrintCmdRow("clean", "Clean safe temporary caches, shader pools & render disks");
        PrintCmdRow("boost", "⚡ 1-Click RAM boost & background working set purge");
        PrintCmdRow("startup", "🚀 List Windows startup apps & boot impact ratings");
        PrintCmdRow("large", "🐘 Find hidden large files (>50 MB) eating storage");
        PrintCmdRow("procs", "🛑 List heavy background memory processes (>80 MB)");
        PrintCmdRow("status", "📊 View OS Drive storage & live RAM telemetry snapshot");
        PrintCmdRow("update", "🔄 Check for newer releases on GitHub");
        PrintCmdRow("help", "Display this help guide\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  OPTIONS:");
        Console.ResetColor();
        PrintOptRow("--safe", "Protect files modified in the last 24 hours [Default: ON]");
        PrintOptRow("--all", "Include orphaned app leftovers and deep caches");
        PrintOptRow("--json", "Output results in structured machine-readable JSON");
        PrintOptRow("--silent, -s", "Run silently without console output (exit code only)");
        PrintOptRow("--export <file>", "Export a timestamped audit log to the given path\n");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  QUICK EXAMPLES:");
        Console.ResetColor();
        Console.WriteLine("    deltempo scan");
        Console.WriteLine("    deltempo clean --safe");
        Console.WriteLine("    deltempo boost");
        Console.WriteLine("    deltempo startup");
        Console.WriteLine("    deltempo status --json");
    }

    private static void PrintCmdRow(string name, string desc)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"    {name,-14} ");
        Console.ResetColor();
        Console.WriteLine(desc);
    }

    private static void PrintOptRow(string opt, string desc)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"    {opt,-18} ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(desc);
        Console.ResetColor();
    }

    private static async Task<int> HandleScanAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        bool silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase) || args.Contains("-s", StringComparer.OrdinalIgnoreCase);

        var targets = CleanerService.GetDefaultTargets();

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  🔍 [Deltempo] Scanning Windows & User Profile directories...\n");
            Console.ResetColor();
        }

        using var cts = new CancellationTokenSource();
        var tasks = targets.Select(t => new CleanerService().ScanFolderAsync(t, (msg, level) => { }, cts.Token)).ToList();
        await Task.WhenAll(tasks);

        long totalBytes = targets.Sum(t => t.SizeBytes);
        int totalFiles = targets.Sum(t => t.FileCount);

        if (isJson)
        {
            var jsonObj = new
            {
                timestamp = DateTime.UtcNow,
                totalReclaimableBytes = totalBytes,
                formattedTotal = TargetFolderInfo.FormatBytes(totalBytes),
                totalFiles = totalFiles,
                categories = targets.Select(t => new
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
            foreach (var t in targets)
            {
                Console.WriteLine($"  │ {t.Name,-38} │ {t.FileCount,8:N0} │ {t.FormattedSize,15} │");
            }
            Console.WriteLine("  └────────────────────────────────────────┴──────────┴─────────────────┘");
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Total Reclaimable Space: {TargetFolderInfo.FormatBytes(totalBytes)} ({totalFiles:N0} files found)");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> HandleCleanAsync(string[] args)
    {
        bool safeMode = !args.Contains("--unsafe", StringComparer.OrdinalIgnoreCase);
        bool cleanAll = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
        bool silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase) || args.Contains("-s", StringComparer.OrdinalIgnoreCase);

        string? exportPath = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--export", StringComparison.OrdinalIgnoreCase))
                exportPath = args[i + 1];
        }

        var targets = CleanerService.GetDefaultTargets();
        var selectedTargets = cleanAll ? targets : targets.Where(t => !t.IsOrphanedAppFolder).ToList();

        if (!silent)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🧹 [Deltempo] Cleaning {selectedTargets.Count} categories (Safety Shield: {(safeMode ? "ACTIVE" : "OFF")})...\n");
            Console.ResetColor();
        }

        using var cts = new CancellationTokenSource();
        var cleanerService = new CleanerService();
        long totalFreed = 0;
        int totalFilesDeleted = 0;
        int totalFoldersDeleted = 0;
        int totalFilesSkipped = 0;

        foreach (var target in selectedTargets)
        {
            var (freed, filesDel, foldersDel, filesSkip) = await cleanerService.CleanFolderAsync(
                target,
                safeMode,
                (msg, level) =>
                {
                    if (!silent && level == LogLevel.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"    ✓ {msg}");
                        Console.ResetColor();
                    }
                },
                progress => { },
                cts.Token);

            totalFreed += freed;
            totalFilesDeleted += filesDel;
            totalFoldersDeleted += foldersDel;
            totalFilesSkipped += filesSkip;
        }

        var summary = new CleanSummary
        {
            TotalFreedBytes = totalFreed,
            TotalFilesDeleted = totalFilesDeleted,
            TotalFoldersDeleted = totalFoldersDeleted,
            TotalFilesSkipped = totalFilesSkipped,
            ElapsedTime = TimeSpan.Zero
        };

        if (!silent)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✨ CLEANUP COMPLETE: Reclaimed {summary.FormattedFreedSize} ({totalFilesDeleted:N0} files deleted, {totalFilesSkipped:N0} protected)");
            Console.ResetColor();
        }

        if (!string.IsNullOrEmpty(exportPath))
        {
            try
            {
                string report = CleanerService.GenerateAuditReport(selectedTargets, summary, safeMode);
                File.WriteAllText(exportPath, report);
                if (!silent) Console.WriteLine($"  📄 Audit report exported to: {exportPath}");
            }
            catch (Exception ex)
            {
                if (!silent) Console.WriteLine($"  ⚠️ Failed to write audit report: {ex.Message}");
            }
        }

        return 0;
    }

    private static async Task<int> HandleBoostAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        bool silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase) || args.Contains("-s", StringComparer.OrdinalIgnoreCase);

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ⚡ [Deltempo] Purging background working set memory...");
            Console.ResetColor();
        }

        var res = await MemoryOptimizerService.OptimizeRamAsync();

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(res, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (!silent)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ RAM Boost Complete: Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} processes in {res.ExecutionTimeMs}ms.");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> HandleStartupAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var items = await StartupManagerService.GetStartupItemsAsync();

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
            Console.WriteLine($"  │ {status,-8} │ {item.ImpactText,-12} │ {item.Name,-28} │ {item.Publisher,-23} │");
        }
        Console.WriteLine("  └──────────┴──────────────┴──────────────────────────────┴─────────────────────────┘");
        return 0;
    }

    private static async Task<int> HandleLargeFilesAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        string scope = "ALL";
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i].Equals("--drive", StringComparison.OrdinalIgnoreCase) || args[i].Equals("--scope", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                scope = args[i + 1];
            }
        }

        long minBytes = 50L * 1024 * 1024;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--min", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                string raw = args[i + 1].Trim().ToUpperInvariant();
                if (raw.EndsWith("GB") && double.TryParse(raw[..^2], out double gbs))
                    minBytes = (long)(gbs * 1024 * 1024 * 1024);
                else if (raw.EndsWith("G") && double.TryParse(raw[..^1], out double g))
                    minBytes = (long)(g * 1024 * 1024 * 1024);
                else if (raw.EndsWith("MB") && double.TryParse(raw[..^2], out double mbs))
                    minBytes = (long)(mbs * 1024 * 1024);
                else if (raw.EndsWith("M") && double.TryParse(raw[..^1], out double m))
                    minBytes = (long)(m * 1024 * 1024);
                else if (long.TryParse(raw, out long bytes))
                    minBytes = bytes;
            }
        }

        bool clean = args.Contains("--clean", StringComparer.OrdinalIgnoreCase);
        bool safeOnly = args.Contains("--safe-only", StringComparer.OrdinalIgnoreCase) || args.Contains("--ai-safe", StringComparer.OrdinalIgnoreCase);

        if (!isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  [Deltempo] AI Large File Hunter (>{TargetFolderInfo.FormatBytes(minBytes)}) on scope '{scope}'...\n");
            Console.ResetColor();
        }

        var files = await LargeFileHunterService.ScanLargeFilesAsync(minBytes, scope);

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine("  ┌──────────────┬──────────────────────────────┬─────────────────────┬──────────────────────────────────────┐");
        Console.WriteLine($"  │ {"SIZE",-12} │ {"AI SAFETY VERDICT",-28} │ {"CATEGORY",-19} │ {"FILE NAME",-36} │");
        Console.WriteLine("  ├──────────────┼──────────────────────────────┼─────────────────────┼──────────────────────────────────────┤");
        foreach (var f in files.Take(35))
        {
            string name = f.FileName.Length > 36 ? f.FileName.Substring(0, 33) + "..." : f.FileName;
            string verdict = f.AiVerdict.Length > 28 ? f.AiVerdict.Substring(0, 25) + "..." : f.AiVerdict;
            Console.WriteLine($"  │ {f.FormattedSize,-12} │ {verdict,-28} │ {f.Category,-19} │ {name,-36} │");
        }
        Console.WriteLine("  └──────────────┴──────────────────────────────┴─────────────────────┴──────────────────────────────────────┘");

        long totalBytes = files.Sum(x => x.SizeBytes);
        var safeFiles = files.Where(x => x.IsAiSafe).ToList();
        long safeBytes = safeFiles.Sum(x => x.SizeBytes);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Discovered {files.Count} large files ({TargetFolderInfo.FormatBytes(totalBytes)}).");
        Console.WriteLine($"  ✓ AI Identified {safeFiles.Count} safe to clean ({TargetFolderInfo.FormatBytes(safeBytes)}): Stale installers, dumps, temp.");
        Console.ResetColor();

        if (clean)
        {
            var toRecycle = safeOnly ? safeFiles : files;
            if (toRecycle.Count == 0)
            {
                Console.WriteLine("  • No matching files selected for recycling.");
                return 0;
            }

            Console.WriteLine($"\n  • Recycling {toRecycle.Count} files ({TargetFolderInfo.FormatBytes(toRecycle.Sum(x => x.SizeBytes))}) to Windows Recycle Bin...");
            var (succ, fail, freed) = LargeFileHunterService.BatchMoveToRecycleBin(toRecycle);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Successfully recycled {succ} files ({TargetFolderInfo.FormatBytes(freed)} freed) with undo capability.");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> HandleProcsAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var procs = await ProcessOptimizerService.GetHeavyProcessesAsync();

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(procs, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  🛑 [Deltempo] Heavy Background Memory Apps (>80 MB)\n");
        Console.ResetColor();

        Console.WriteLine("  ┌──────────┬──────────────┬────────────────────────────────────────────────────┐");
        Console.WriteLine($"  │ {"PID",-8} │ {"MEMORY",-12} │ {"PROCESS NAME",-50} │");
        Console.WriteLine("  ├──────────┼──────────────┼────────────────────────────────────────────────────┤");
        foreach (var p in procs)
        {
            string name = p.DisplayName.Length > 50 ? p.DisplayName.Substring(0, 47) + "..." : p.DisplayName;
            Console.WriteLine($"  │ {p.ProcessId,-8} │ {p.FormattedMemory,-12} │ {name,-50} │");
        }
        Console.WriteLine("  └──────────┴──────────────┴────────────────────────────────────────────────────┘");
        return 0;
    }

    private static int HandleStatus(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var drive = DriveTelemetryService.GetSystemDriveTelemetry();
        var mem = MemoryOptimizerService.GetMemoryInfo();

        if (isJson)
        {
            var obj = new { drive, memory = mem };
            Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  📊 [Deltempo] System Telemetry Snapshot\n");
        Console.ResetColor();

        Console.WriteLine($"  💾 OS Drive ({drive.DriveLetter}): {drive.FormattedFree} free of {drive.FormattedTotal} ({drive.FreePercentage:F1}% Free)");
        Console.WriteLine($"  ⚡ Memory (RAM):  {mem.FormattedUsed} used of {mem.FormattedTotal} ({mem.UsedPercent:F0}% Used)");
        return 0;
    }

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
}
