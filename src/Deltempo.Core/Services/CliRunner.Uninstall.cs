using System.Diagnostics;
using System.Text.Json;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static partial class CliRunner
{
    private static async Task<int> HandleUninstallAsync(string[] args)
    {
        // deltempo uninstall <app-query> [--dry-run] [--force] [--silent] [--restore-point] [--json]
        if (args.Length < 2 || args[1].StartsWith("-", StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Usage: deltempo uninstall <app-name-or-id> [options]\n");
            Console.ResetColor();
            Console.WriteLine("  Options:");
            Console.WriteLine("    --dry-run, -d         Simulate leftover scanning without modifying system");
            Console.WriteLine("    --force, -f           Bypass interactive confirmation prompt");
            Console.WriteLine("    --silent, -s          Pass silent/quiet switches to uninstaller");
            Console.WriteLine("    --restore-point, -rp  Create optional pre-uninstall Windows System Restore Point (disabled by default)");
            Console.WriteLine("    --json                Output results in structured machine-readable JSON");
            return 1;
        }

        string appQuery = args[1];
        bool isDryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-d", StringComparison.OrdinalIgnoreCase));
        bool isForce = args.Any(a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-f", StringComparison.OrdinalIgnoreCase));
        bool isSilent = args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase));
        bool createRestorePoint = args.Any(a => string.Equals(a, "--restore-point", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-rp", StringComparison.OrdinalIgnoreCase));
        bool outputJson = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));

        if (!outputJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  🔍 Scanning installed software matching '{appQuery}'...");
            Console.ResetColor();
        }

        var apps = await Task.Run(() => InstalledAppService.GetInstalledApps());
        var matches = apps.Where(a =>
            a.DisplayName.Contains(appQuery, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(a.Publisher) && a.Publisher.Contains(appQuery, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(a.PackageFullName) && a.PackageFullName.Contains(appQuery, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (matches.Count == 0)
        {
            if (outputJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = $"No installed application matching '{appQuery}' found." }));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✖ No installed application matching '{appQuery}' found.");
                Console.ResetColor();
            }
            return 1;
        }

        InstalledAppItem targetApp = matches[0];
        if (matches.Count > 1)
        {
            var exactMatch = matches.FirstOrDefault(a => string.Equals(a.DisplayName, appQuery, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                targetApp = exactMatch;
            }
            else if (!outputJson && !isForce)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Found {matches.Count} matching applications:");
                for (int i = 0; i < matches.Count; i++)
                {
                    Console.WriteLine($"    [{i + 1}] {matches[i].DisplayName} ({matches[i].DisplayVersion}) - {matches[i].UninstallEngine}");
                }
                Console.WriteLine();
                Console.Write("  Select application number (or Enter for [1]): ");
                string? input = Console.ReadLine();
                if (int.TryParse(input, out int selection) && selection >= 1 && selection <= matches.Count)
                {
                    targetApp = matches[selection - 1];
                }
                Console.ResetColor();
            }
        }

        if (!outputJson)
        {
            Console.WriteLine($"  Target: {targetApp.DisplayName} {targetApp.DisplayVersion}");
            Console.WriteLine($"  Engine: {targetApp.UninstallEngine}");
            if (!string.IsNullOrEmpty(targetApp.InstallLocation))
            {
                Console.WriteLine($"  Path:   {targetApp.InstallLocation}");
            }
            Console.WriteLine();
        }

        if (!isDryRun && !isForce && !outputJson)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  Proceed with root eradication of '{targetApp.DisplayName}'? [y/N]: ");
            Console.ResetColor();
            var key = Console.ReadKey();
            Console.WriteLine();
            if (key.KeyChar != 'y' && key.KeyChar != 'Y')
            {
                Console.WriteLine("  Aborted by user.");
                return 0;
            }
        }

        // 1. System Restore Point Checkpoint
        if (!isDryRun && createRestorePoint)
        {
            if (!outputJson)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("  [1/4] Creating Windows System Restore Point... ");
            }

            var rpResult = await SystemRestorePointService.CreateRestorePointAsync($"Pre-Uninstall {targetApp.DisplayName}");
            if (!outputJson)
            {
                if (rpResult.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"OK (Seq: {rpResult.SequenceNumber})");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Skipped ({rpResult.Message})");
                }
                Console.ResetColor();
            }
        }

        // 2. Official Uninstaller Execution
        AppTraceSnapshot? snapshot = null;
        if (!isDryRun)
        {
            snapshot = await RootLeftoverScannerService.CreateSnapshotAsync(targetApp);

            if (!outputJson)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [2/4] Launching {targetApp.UninstallEngine} uninstaller... ");
            }

            bool uninstalled = await InstalledAppService.UninstallAppAsync(targetApp, silent: isSilent);
            if (!outputJson)
            {
                if (uninstalled)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Completed");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Failed or bypassed, proceeding to root remnant sweep");
                }
                Console.ResetColor();
            }
        }

        // 3. Deep Root Remnants Scan
        if (!outputJson)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  [3/4] Scanning root remnants (Registry, COM, AppData, Shell, PATH, Tasks)... ");
        }

        var scanResult = await RootLeftoverScannerService.ScanAppTracesAsync(targetApp, snapshot);
        if (!outputJson)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Found {scanResult.Items.Count} trace(s) ({scanResult.FormattedTotalSize})");
            Console.ResetColor();
        }

        if (isDryRun)
        {
            if (outputJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    appName = targetApp.DisplayName,
                    dryRun = true,
                    totalItems = scanResult.Items.Count,
                    totalSizeBytes = scanResult.TotalSizeBytes,
                    formattedSize = scanResult.FormattedTotalSize,
                    items = scanResult.Items.Select(i => new { type = i.Type.ToString(), path = i.PathOrKey, confidence = i.Confidence.ToString(), sizeBytes = i.SizeBytes })
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n  --- REMNANT ITEMS DISCOVERED (DRY RUN) ---");
                Console.ResetColor();
                foreach (var item in scanResult.Items)
                {
                    Console.WriteLine($"  • [{item.TypeBadge}] {item.PathOrKey} ({item.FormattedSize}) - {item.Confidence}");
                }
            }
            return 0;
        }

        // 4. Residual Eradication & Locked File Scheduling
        if (!outputJson)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  [4/4] Eradicating remnants... ");
        }

        var purgeResult = await RootLeftoverPurgeService.PurgeLeftoversAsync(scanResult.Items);

        if (outputJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                appName = targetApp.DisplayName,
                success = true,
                purgedItems = purgeResult.ItemsPurgedCount,
                failedItems = purgeResult.ErrorsCount,
                rebootScheduledItems = purgeResult.RebootScheduledCount,
                reclaimedBytes = purgeResult.TotalReclaimedBytes,
                formattedReclaimed = purgeResult.FormattedReclaimed,
                executionTimeMs = purgeResult.ExecutionTimeMs
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done!");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ✓ Purged Traces:       {purgeResult.ItemsPurgedCount}");
            Console.WriteLine($"  ✓ Reclaimed Disk:      {purgeResult.FormattedReclaimed}");
            if (purgeResult.RebootScheduledCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⚠ Reboot Scheduled:    {purgeResult.RebootScheduledCount} locked files will be deleted on next system reboot");
            }
            Console.ResetColor();
        }

        return 0;
    }
}
