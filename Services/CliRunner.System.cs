using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

// CLI command handlers: startup, status, update, kill and system repair.
public static partial class CliRunner
{

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
            Console.WriteLine(JsonSerializer.Serialize(items.Select(i => new
            {
                i.Name,
                i.FriendlyName,
                i.Command,
                i.ExePath,
                i.Location,
                i.LocationDisplay,
                i.IsEnabled,
                i.IsFileMissing,
                i.Impact,
                i.ImpactText,
                i.Publisher,
                i.IsProtected
            }), new JsonSerializerOptions { WriteIndented = true }));
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
        Console.WriteLine($"  • Memory (RAM):  [{GetProgressBar(mem.UsedPercent, 20)}] {mem.FormattedUsed} used of {mem.FormattedTotal} ({mem.UsedPercent:F0}% Used)");
        Console.WriteLine($"  📦 Standby Cache: {mem.FormattedSystemCache} reclaimable from closed programs");

        return 0;
    }

    // ─── UPDATE COMMAND ────────────────────────────────────────────────

    private static async Task<int> HandleUpdateAsync(string[] args)
    {
        bool checkOnly = HasFlag(args, "check", "--check", "-c");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  🔄 [Deltempo] Checking for updates from GitHub Releases...");
        Console.ResetColor();

        var release = await UpdateService.CheckForUpdatesAsync();
        if (release == null || !release.CheckSucceeded)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠️ Could not check for updates. Please check your internet connection or try again later.");
            if (!string.IsNullOrEmpty(release?.StatusMessage))
            {
                Console.WriteLine($"     Details: {release.StatusMessage}");
            }
            Console.ResetColor();
            return 1;
        }

        if (release.IsNewer)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✨ New Release available: {release.TagName}");
            Console.WriteLine($"     Currently running: {BuildInfo.VersionWithPatchDisplay}");
            if (release.FileSizeBytes > 0)
            {
                Console.WriteLine($"     Artifact Size:     {TargetFolderInfo.FormatBytes(release.FileSizeBytes)}");
            }
            if (!string.IsNullOrWhiteSpace(release.Body))
            {
                Console.WriteLine($"     Release Notes:     {release.Body.Split('\n')[0].Trim()}");
            }
            Console.ResetColor();

            if (checkOnly || HasFlag(args, "--dry-run", "-d"))
            {
                Console.WriteLine("\n  Run 'deltempo update' to download and install this release.");
                return 2; // Exit code 2 indicates update is available in check mode
            }

            // Perform automatic in-place update installation
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  ⬇️ Downloading verified release binary...");
            Console.ResetColor();

            var progress = new Progress<double>(pct =>
            {
                Console.Write($"\r  Progress: [{pct,5:F1}%]");
            });

            try
            {
                await UpdateService.DownloadAndApplyUpdateAsync(
                    release.DownloadUrl,
                    progress);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  ✓ Update verified and staged successfully. Deltempo will now restart.");
                Console.ResetColor();
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  ❌ Update installation failed: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Deltempo is up to date ({BuildInfo.VersionWithPatchDisplay}).");
            if (!string.IsNullOrEmpty(release.StatusMessage))
            {
                Console.WriteLine($"     Status: {release.StatusMessage}");
            }
            Console.ResetColor();
            return 0;
        }
    }

    // ─── KILL COMMAND ──────────────────────────────────────────────────

    private static int HandleKill(string[] args)
    {
        int myPid = Process.GetCurrentProcess().Id;
        var myProcessName = Process.GetCurrentProcess().ProcessName;
        var procs = Process.GetProcesses()
            .Where(p => p.Id != myPid &&
                        (p.ProcessName.Equals(myProcessName, StringComparison.OrdinalIgnoreCase) ||
                         p.ProcessName.Equals("Deltempo", StringComparison.OrdinalIgnoreCase) ||
                         p.ProcessName.Equals("WinTempCleaner", StringComparison.OrdinalIgnoreCase)))
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
        Console.WriteLine($"  Terminated {killed} running Deltempo process(es).");
        Console.ResetColor();
        return procs.Count == 0 ? 1 : 0;
    }

    private static async Task<int> HandleRepairAsync(string[] args)
    {
        string subCmd = args.Length > 1 ? args[1].ToLowerInvariant() : "all";
        bool isJson = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
        bool silent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase));

        if (!ElevationService.IsAdministrator)
        {
            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Administrator elevation required." }));
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [Notice] Administrator privileges are required to run Windows system integrity repairs.");
            Console.WriteLine("           Please run your command prompt or terminal as Administrator.");
            Console.ResetColor();
            return 1;
        }

        if (!silent && !isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  [Deltempo System Integrity] Executing repair target: {subCmd.ToUpperInvariant()}...\n");
            Console.ResetColor();
        }

        Action<string>? onOutput = (silent || isJson) ? null : msg =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    {msg}");
            Console.ResetColor();
        };

        RepairExecutionResult result;
        switch (subCmd)
        {
            case "sfc":
                result = await SystemRepairService.RunSfcScannowAsync(onOutput);
                break;
            case "dism":
            case "restorehealth":
                result = await SystemRepairService.RunDismRestoreHealthAsync(onOutput);
                break;
            case "scanhealth":
            case "check":
                result = await SystemRepairService.RunDismScanHealthAsync(onOutput);
                break;
            case "winsxs":
            case "cleanup":
                result = await SystemRepairService.RunDismComponentCleanupAsync(onOutput);
                break;
            case "chkdsk":
            case "disk":
                string drive = args.Length > 2 && !args[2].StartsWith("-") ? args[2] : "C:";
                result = await SystemRepairService.RunChkdskScanAsync(drive, onOutput);
                break;
            case "update":
            case "windowsupdate":
                result = await SystemRepairService.ResetWindowsUpdateStackAsync(onOutput);
                break;
            case "network":
            case "net":
            case "winsock":
                result = await SystemRepairService.ResetNetworkStackAsync(onOutput);
                break;
            case "all":
            default:
                result = await SystemRepairService.RunAutonomousHealthCheckAndRepairAsync(onOutput);
                break;
        }

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                tool = result.Tool.ToString(),
                success = result.Success,
                exitCode = result.ExitCode,
                executionTimeMs = result.ExecutionTimeMs,
                errorMessage = result.ErrorMessage
            }, new JsonSerializerOptions { WriteIndented = true }));
            return result.Success ? 0 : 1;
        }

        Console.WriteLine();
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ [{result.Tool}] Completed successfully in {result.ExecutionTimeMs / 1000.0:F1}s.");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ [{result.Tool}] Finished with warnings or errors (Exit Code: {result.ExitCode}).");
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                Console.WriteLine($"    Details: {result.ErrorMessage}");
            }
            Console.ResetColor();
            return result.ExitCode != 0 ? result.ExitCode : 1;
        }
    }
}
