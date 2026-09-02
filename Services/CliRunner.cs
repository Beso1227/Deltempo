using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Normalize primary command / flag
        string cmd = args.Length > 0 ? args[0].ToLowerInvariant().TrimStart('-') : "help";

        switch (cmd)
        {
            case "test":
                bool passed = await Tests.TestRunner.RunVerificationAsync();
                return passed ? 0 : 1;

            case "scan":
            case "s":
                return await HandleScanAsync(args);

            case "clean":
            case "c":
                return await HandleCleanAsync(args);

            case "boost":
            case "ram":
            case "b":
                return await HandleBoostAsync(args);

            case "startup":
            case "start":
                return await HandleStartupAsync(args);

            case "large":
            case "disk":
            case "l":
                return await HandleLargeFilesAsync(args);

            case "procs":
            case "proc":
            case "p":
                return await HandleProcsAsync(args);

            case "status":
            case "info":
            case "i":
                return HandleStatus(args);

            case "update":
            case "u":
                return await HandleUpdateAsync(args);

            case "help":
            case "h":
            case "?":
            default:
                PrintHelp();
                return 0;
        }
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
   ___       _ _                               
  / _ \___  | | |_ ___ _ __ ___  _ __   ___    
 / /_)/ _ \ | | __/ _ \ '_ ` _ \| '_ \ / _ \   
/ ___/  __/ | | ||  __/ | | | | | |_) | (_) |  
\/    \___| |_|\__\___|_| |_| |_| .__/ \___/   
                                 |_|           
        Precision Windows Optimizer & System Health (King Edition)");
        Console.ResetColor();

        Console.WriteLine("\nUsage: Deltempo.exe <command> [options]\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚡ QUICK COMMANDS:");
        Console.ResetColor();
        Console.WriteLine("  scan                  Dry-run scan of all 17 cache & junk scopes");
        Console.WriteLine("  clean                 Clean safe temporary caches & shader pools");
        Console.WriteLine("  boost                 ⚡ Instant 1-click RAM working set purge");
        Console.WriteLine("  startup               🚀 List Windows startup apps & boot impact");
        Console.WriteLine("  large                 🐘 Find large files (>50 MB) eating storage");
        Console.WriteLine("  procs                 🛑 List heavy background memory apps (>80 MB)");
        Console.WriteLine("  status                📊 View OS Drive & RAM health telemetry");
        Console.WriteLine("  update                🔄 Check for latest release on GitHub");
        Console.WriteLine("  help                  Show this friendly guide\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("🛠️  OPTIONS & MODIFIERS:");
        Console.ResetColor();
        Console.WriteLine("  --safe                Enable 24h Safety Shield (protects files < 24h old) [Default]");
        Console.WriteLine("  --all                 Include orphaned app leftovers and deep caches");
        Console.WriteLine("  --json                Output results in structured JSON format");
        Console.WriteLine("  --silent, -s          Execute silently with zero console output");
        Console.WriteLine("  --export <file>       Save a timestamped audit report to a file\n");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("💡 EXAMPLES:");
        Console.ResetColor();
        Console.WriteLine("  Deltempo.exe scan");
        Console.WriteLine("  Deltempo.exe clean --safe");
        Console.WriteLine("  Deltempo.exe boost");
        Console.WriteLine("  Deltempo.exe startup");
        Console.WriteLine("  Deltempo.exe large");
        Console.WriteLine("  Deltempo.exe status --json\n");
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
            Console.WriteLine("\n🔍 [Deltempo] Scanning Windows & User Profile directories...\n");
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
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine($"{"CATEGORY",-38} | {"FILES",-8} | {"RECLAIMABLE",-15}");
            Console.WriteLine("--------------------------------------------------------------------------------");
            foreach (var t in targets)
            {
                Console.WriteLine($"{t.Name,-38} | {t.FileCount,8:N0} | {t.FormattedSize,15}");
            }
            Console.WriteLine("================================================================================");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ TOTAL RECLAIMABLE SPACE: {TargetFolderInfo.FormatBytes(totalBytes)} across {totalFiles:N0} files\n");
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
            Console.WriteLine($"\n🧹 [Deltempo] Cleaning {selectedTargets.Count} categories (Safety Shield: {(safeMode ? "ENABLED" : "DISABLED")})...\n");
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
                        Console.WriteLine($"  ✓ {msg}");
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
            Console.WriteLine("\n================================================================================");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✨ CLEANUP COMPLETE: Reclaimed {summary.FormattedFreedSize} ({totalFilesDeleted:N0} files deleted, {totalFilesSkipped:N0} protected)");
            Console.ResetColor();
            Console.WriteLine("================================================================================\n");
        }

        if (!string.IsNullOrEmpty(exportPath))
        {
            try
            {
                string report = CleanerService.GenerateAuditReport(selectedTargets, summary, safeMode);
                File.WriteAllText(exportPath, report);
                if (!silent) Console.WriteLine($"Audit report exported to: {exportPath}\n");
            }
            catch (Exception ex)
            {
                if (!silent) Console.WriteLine($"Failed to write audit report: {ex.Message}\n");
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
            Console.WriteLine("\n⚡ [Deltempo] Purging background working set memory...");
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
            Console.WriteLine($"✓ RAM Boost Complete: Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} processes in {res.ExecutionTimeMs}ms.\n");
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
        Console.WriteLine($"\n🚀 [Deltempo] Startup Apps & Boot Impact ({items.Count} items found)\n");
        Console.ResetColor();

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"{"STATUS",-10} | {"IMPACT",-14} | {"APPLICATION",-30} | {"PUBLISHER"}");
        Console.WriteLine("--------------------------------------------------------------------------------");
        foreach (var item in items)
        {
            string status = item.IsEnabled ? "[ENABLED]" : "[DISABLED]";
            Console.WriteLine($"{status,-10} | {item.ImpactText,-14} | {item.Name,-30} | {item.Publisher}");
        }
        Console.WriteLine("================================================================================\n");
        return 0;
    }

    private static async Task<int> HandleLargeFilesAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n🐘 [Deltempo] Searching for large files (>50 MB)...\n");
        Console.ResetColor();

        var files = await LargeFileHunterService.ScanLargeFilesAsync();

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"{"SIZE",-12} | {"CATEGORY",-18} | {"FILE NAME"}");
        Console.WriteLine("--------------------------------------------------------------------------------");
        foreach (var f in files.Take(25))
        {
            Console.WriteLine($"{f.FormattedSize,-12} | {f.Category,-18} | {f.FileName}");
        }
        Console.WriteLine("================================================================================");
        Console.WriteLine($"Total Large Files Found: {files.Count} ({TargetFolderInfo.FormatBytes(files.Sum(x => x.SizeBytes))})\n");
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
        Console.WriteLine($"\n🛑 [Deltempo] Heavy Background Processes (>80 MB)\n");
        Console.ResetColor();

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"{"PID",-8} | {"MEMORY",-12} | {"PROCESS NAME"}");
        Console.WriteLine("--------------------------------------------------------------------------------");
        foreach (var p in procs)
        {
            Console.WriteLine($"{p.ProcessId,-8} | {p.FormattedMemory,-12} | {p.DisplayName}");
        }
        Console.WriteLine("================================================================================\n");
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
        Console.WriteLine("\n📊 [Deltempo] System Telemetry Snapshot\n");
        Console.ResetColor();

        Console.WriteLine($"  💾 OS Drive ({drive.DriveLetter}): {drive.FormattedFree} free of {drive.FormattedTotal} ({drive.FreePercentage:F1}% Free)");
        Console.WriteLine($"  ⚡ Memory (RAM):  {mem.FormattedUsed} used of {mem.FormattedTotal} ({mem.UsedPercent:F0}% Used)");
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleUpdateAsync(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n🔄 [Deltempo] Checking for updates on GitHub...");
        Console.ResetColor();

        var release = await UpdateService.CheckForUpdatesAsync();
        if (release != null && release.IsNewer)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ New version available: {release.TagName} (Current: v1.0.0)");
            Console.WriteLine($"  Download: {release.DownloadUrl}\n");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ You are running the latest version of Deltempo (v1.0.0).\n");
            Console.ResetColor();
        }

        return 0;
    }
}
