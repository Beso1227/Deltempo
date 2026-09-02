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
        Console.WriteLine("       D E L T E M P O  —  Precision Windows Optimizer (v1.1.0)\n");
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

        var cleanerService = new CleanerService();
        var targets = CleanerService.GetDefaultTargets();

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  🔍 [Deltempo] Scanning Windows & User Profile directories...\n");
            Console.ResetColor();
        }

        var cts = new CancellationTokenSource();
        var tasks = targets.Select(t => cleanerService.ScanFolderAsync(t, (msg, level) => { }, cts.Token)).ToList();
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

        var cleanerService = new CleanerService();
        var targets = CleanerService.GetDefaultTargets();
        var selectedTargets = cleanAll ? targets : targets.Where(t => !t.IsOrphanedAppFolder).ToList();

        if (!silent)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🧹 [Deltempo] Cleaning {selectedTargets.Count} categories (Safety Shield: {(safeMode ? "ACTIVE" : "OFF")})...\n");
            Console.ResetColor();
        }

        var cts = new CancellationTokenSource();
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
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  🐘 [Deltempo] Searching for large files (>50 MB)...\n");
        Console.ResetColor();

        var files = await LargeFileHunterService.ScanLargeFilesAsync();

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine("  ┌──────────────┬────────────────────┬──────────────────────────────────────────────────┐");
        Console.WriteLine($"  │ {"SIZE",-12} │ {"CATEGORY",-18} │ {"FILE NAME",-48} │");
        Console.WriteLine("  ├──────────────┼────────────────────┼──────────────────────────────────────────────────┤");
        foreach (var f in files.Take(25))
        {
            string name = f.FileName.Length > 48 ? f.FileName.Substring(0, 45) + "..." : f.FileName;
            Console.WriteLine($"  │ {f.FormattedSize,-12} │ {f.Category,-18} │ {name,-48} │");
        }
        Console.WriteLine("  └──────────────┴────────────────────┴──────────────────────────────────────────────────┘");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Discovered {files.Count} large files ({TargetFolderInfo.FormatBytes(files.Sum(x => x.SizeBytes))})");
        Console.ResetColor();
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
            Console.WriteLine($"  ✓ New version available: {release.TagName} (Current: v1.1.0)");
            Console.WriteLine($"  📥 Download: {release.DownloadUrl}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ You are running the latest version of Deltempo (v1.1.0).");
            Console.ResetColor();
        }

        return 0;
    }
}
