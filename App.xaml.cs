using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;

        if (args.Length > 0)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            if (args.Contains("--test", StringComparer.OrdinalIgnoreCase))
            {
                bool passed = await Tests.TestRunner.RunVerificationAsync();
                FreeConsole();
                Shutdown(passed ? 0 : 1);
                return;
            }

            if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
            {
                PrintHelp();
                FreeConsole();
                Shutdown(0);
                return;
            }

            if (args.Contains("--scan", StringComparer.OrdinalIgnoreCase))
            {
                await HandleCliScanAsync(args);
                FreeConsole();
                Shutdown(0);
                return;
            }

            if (args.Contains("--clean", StringComparer.OrdinalIgnoreCase))
            {
                await HandleCliCleanAsync(args);
                FreeConsole();
                Shutdown(0);
                return;
            }
        }

        base.OnStartup(e);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("             DELTEMPO CLI — PURE PRECISION WINDOWS CLEANER (KING EDITION)       ");
        Console.WriteLine("================================================================================");
        Console.WriteLine("Usage: Deltempo.exe [OPTIONS]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  --scan                Perform a multithreaded dry-run scan of all junk categories");
        Console.WriteLine("  --clean               Execute cleanup across selected categories");
        Console.WriteLine("  --safe                Enable 24-hour safety shield (protects files < 24h old)");
        Console.WriteLine("  --all                 Include orphaned app leftovers and developer caches");
        Console.WriteLine("  --silent, -s          Execute silently with zero console output (exit code only)");
        Console.WriteLine("  --json                Output results in structured JSON format");
        Console.WriteLine("  --export <path>       Save timestamped audit report to specified file path");
        Console.WriteLine("  --test                Run built-in automated test suite");
        Console.WriteLine("  --help, -h            Show this command-line help guide\n");
        Console.WriteLine("Examples:");
        Console.WriteLine("  Deltempo.exe --scan --json");
        Console.WriteLine("  Deltempo.exe --clean --safe");
        Console.WriteLine("  Deltempo.exe --clean --safe --export \"C:\\logs\\cleanup.log\"");
        Console.WriteLine("================================================================================\n");
    }

    private static async Task HandleCliScanAsync(string[] args)
    {
        bool isJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        bool silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase) || args.Contains("-s", StringComparer.OrdinalIgnoreCase);

        var cleanerService = new CleanerService();
        var targets = CleanerService.GetDefaultTargets();

        if (!silent && !isJson)
        {
            Console.WriteLine("\n[Deltempo] Scanning Windows & User Profile directories...\n");
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
            return;
        }

        if (!silent)
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine($"{"CATEGORY",-35} | {"FILES",-10} | {"RECLAIMABLE",-15}");
            Console.WriteLine("--------------------------------------------------------------------------------");
            foreach (var t in targets)
            {
                Console.WriteLine($"{t.Name,-35} | {t.FileCount,10:N0} | {t.FormattedSize,15}");
            }
            Console.WriteLine("================================================================================");
            Console.WriteLine($"TOTAL RECLAIMABLE DISK SPACE: {TargetFolderInfo.FormatBytes(totalBytes)} ({totalFiles:N0} files)\n");
        }
    }

    private static async Task HandleCliCleanAsync(string[] args)
    {
        bool safeMode = args.Contains("--safe", StringComparer.OrdinalIgnoreCase);
        bool cleanAll = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
        bool silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase) || args.Contains("-s", StringComparer.OrdinalIgnoreCase);

        string? exportPath = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--export", StringComparison.OrdinalIgnoreCase))
            {
                exportPath = args[i + 1];
            }
        }

        var cleanerService = new CleanerService();
        var targets = CleanerService.GetDefaultTargets();

        var selectedTargets = cleanAll 
            ? targets 
            : targets.Where(t => !t.IsOrphanedAppFolder).ToList();

        if (!silent)
        {
            Console.WriteLine($"\n[Deltempo] Starting Cleanup across {selectedTargets.Count} categories (Safety Shield: {(safeMode ? "ENABLED" : "DISABLED")})...");
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
                    if (!silent) Console.WriteLine($"  [{level}] {msg}");
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
            Console.WriteLine($"[Deltempo] CLEANUP COMPLETE: Reclaimed {summary.FormattedFreedSize} ({totalFilesDeleted:N0} files deleted, {totalFilesSkipped:N0} protected)");
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
    }
}
