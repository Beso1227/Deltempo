using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static partial class CliRunner
{
    public static Func<List<TargetFolderInfo>>? TargetsResolver { get; set; }

    public static List<TargetFolderInfo> ResolveTargets() => TargetsResolver?.Invoke() ?? CleanerService.GetDefaultTargets();

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
                var targets = ResolveTargets();
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

            case "deep-clean":
            case "deepclean":
            case "1click":
            case "deep":
            case "all-in-one":
                exitCode = await HandleDeepCleanAsync(args);
                break;

            case "restore-points":
            case "restorepoints":
            case "restore":
            case "vss":
                exitCode = await HandleRestorePointsAsync(args);
                break;

            case "duplicates":
            case "duplicate":
            case "dupes":
                exitCode = await HandleDuplicatesAsync(args);
                break;

            case "schedule":
            case "task":
                exitCode = await HandleScheduleAsync(args);
                break;

            case "shell":
            case "context-menu":
                exitCode = HandleShellExtension(args);
                break;

            case "clean":
            case "c":
                exitCode = await HandleCleanAsync(args);
                break;

            case "smart":
            case "smart-clean":
            case "smartclean":
            case "safe":
                exitCode = await HandleSmartCleanAsync(args);
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

            case "repair":
            case "integrity":
            case "fix":
                exitCode = await HandleRepairAsync(args);
                break;

            case "force-delete":
            case "forcedelete":
            case "fdel":
            case "shred":
            case "unlock":
            case "stubborn":
                exitCode = await HandleForceDeleteAsync(args);
                break;

            case "uninstall":
            case "uninst":
            case "remove":
            case "rm":
                exitCode = await HandleUninstallAsync(args);
                break;

            case "register":
            case "unregister":
                exitCode = CliRegistrationService.HandleRegisterCommand(args);
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
        Console.WriteLine($"       D E L T E M P O - Windows Cleaner and Memory Optimizer (v{UpdateService.CurrentVersion.ToString(3)})\n");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Usage: deltempo <command> [subcommand|target] [options]\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  CLEANUP & CACHE COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("deep-clean", "Autonomous 1-click full OS cleanup: RAM, DISM, 26 scopes & VSS");
        PrintCmdRow("smart-clean", "1-click safe cleanup: purges 100% safe disposable caches only");
        PrintCmdRow("restore-points", "Inspect & purge old System Restore Points (--clean, --all)");
        PrintCmdRow("scan [category]", "Scan temporary files and cache targets");
        PrintCmdRow("clean [category]", "Clean safe temporary caches and shaders");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  STORAGE & BIG FILES COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("large [path]", "Find large files (options: --min, --type, --safe, --sort, --top)");
        PrintCmdRow("large clean", "Move disposable large files to the Recycle Bin (--dry-run, --yes)");
        PrintCmdRow("large inspect <file>", "Inspect file deletion safety and risk profile");
        PrintCmdRow("large delete <file>", "Move a specific file to the Recycle Bin");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  PERFORMANCE & RAM COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("boost", "Purge background process working sets, standby list & system cache (default)");
        PrintCmdRow("boost --all", "Deep purge all 8 Windows NT Kernel memory zones");
        PrintCmdRow("boost --standby", "Purge closed application standby page list (normal + low priority)");
        PrintCmdRow("boost --cache", "Flush and reset Windows System File Cache");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  SYSTEM INTEGRITY & CORRUPTION REPAIR:");
        Console.ResetColor();
        PrintCmdRow("repair [all]", "Autonomous 1-click health check & corruption repair");
        PrintCmdRow("repair sfc", "Run System File Checker (sfc /scannow)");
        PrintCmdRow("repair dism", "Restore component store health (dism /restorehealth)");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  FORCE DELETE & STUBBORN FILE PURGE:");
        Console.ResetColor();
        PrintCmdRow("force-delete <path>", "Permanently delete stubborn locked file/folder (--terminate-lockers, --yes)");
        PrintCmdRow("force-delete scan <path>", "Inspect stubbornness, lock owners, and safety tier for target");
        PrintCmdRow("force-delete pending", "List files registered for automatic deletion on next Windows reboot");
        PrintCmdRow("repair scanhealth", "Scan component store for corruption (dism /scanhealth)");
        PrintCmdRow("repair winsxs", "Scavenge superseded components (WinSxS cleanup)");
        PrintCmdRow("repair chkdsk [drive]", "Verify volume filesystem integrity (chkdsk /scan)");
        PrintCmdRow("repair update", "Reset Windows Update & BITS servicing stack");
        PrintCmdRow("repair network", "Reset Winsock, TCP/IP stack & flush DNS");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  STARTUP & PROCESS COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("startup", "List Windows startup apps and boot impact ratings");
        PrintCmdRow("startup disable <app>", "Disable a startup application from launching on boot");
        PrintCmdRow("startup enable <app>", "Re-enable a previously disabled startup application");
        PrintCmdRow("procs", "List heavy background memory apps (>20 MB)");
        PrintCmdRow("procs trim <pid|name>", "Trim working set memory of a specific process");
        PrintCmdRow("procs kill <pid|name>", "Terminate a heavy runaway background process");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  UNINSTALL & ROOT REMOVAL COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("uninstall <app>", "Eradicate application and residual traces from roots (--dry-run, --force, --silent, --json)");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  UTILITY & STATUS COMMANDS:");
        Console.ResetColor();
        PrintCmdRow("duplicates [path]", "Find duplicate files via 3-stage hash matching (--delete, --strategy)");
        PrintCmdRow("schedule", "Manage automatic weekly background maintenance in Task Scheduler");
        PrintCmdRow("shell", "Manage Windows Explorer right-click context menu integration");
        PrintCmdRow("status", "System telemetry dashboard with visual ASCII meters & admin status");
        PrintCmdRow("update", "Check for newer releases on GitHub");
        PrintCmdRow("register", "Opt-in shell integration: user PATH, Win+R alias & PowerShell function");
        PrintCmdRow("register --status", "Show current shell-integration registration state");
        PrintCmdRow("unregister", "Remove all shell integration (PATH, registry & profile entries)");
        PrintCmdRow("kill", "Close running Deltempo background instances");
        PrintCmdRow("help", "Display this interactive help guide");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  GLOBAL OPTIONS:");
        Console.ResetColor();
        PrintOptRow("--smart, --safe-only", "Target only 100% safe disposable caches (skip orphaned apps)");
        PrintOptRow("--recycle-bin, -r", "Send deleted files to the Windows Recycle Bin (undoable)");
        PrintOptRow("--unsafe", "Disable 24-hour file modification protection (enabled by default)"); // DevSkim: ignore DS172412
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
        Console.WriteLine("    deltempo register --status");
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

    private static async Task<int> HandleForceDeleteAsync(string[] args)
    {
        bool isJson = HasFlag(args, "--json", "-j");
        string subCmd = args.Length > 1 && !args[1].StartsWith("-") ? args[1].ToLowerInvariant() : "now";

        if (subCmd == "scan" || subCmd == "inspect" || subCmd == "check")
        {
            if (args.Length < 3)
            {
                Console.WriteLine("  [!] Usage: deltempo force-delete scan <path>");
                return 1;
            }
            string path = args[2].Trim('"', '\'');
            return HandleInspectForceDeleteTarget(path, isJson);
        }

        if (subCmd == "pending" || subCmd == "reboot-list")
        {
            return HandleListPendingRebootPurges(isJson);
        }

        var targets = new List<string>();
        int startIndex = (subCmd == "now" || subCmd == "delete" || subCmd == "purge" || subCmd == "shred") ? 2 : 1;

        // Options that consume a following value. Without this, "--retry 5" would treat "5"
        // as a deletion target and resolve it against the current working directory.
        var valuedOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--retry" };

        for (int i = startIndex; i < args.Length; i++)
        {
            if (args[i].StartsWith("-"))
            {
                if (valuedOptions.Contains(args[i]))
                {
                    i++; // Skip this option's value so it is never treated as a path.
                }
                continue;
            }

            targets.Add(args[i].Trim('"', '\''));
        }

        if (targets.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [!] Usage: deltempo force-delete [now|scan] <file_or_folder_path> [options]");
            Console.WriteLine("      Options: --terminate-lockers, --recycle, --dry-run, --yes, --retry <N>, --json");
            Console.WriteLine("               --force-override  (required to delete policy-protected targets)");
            Console.ResetColor();
            return 1;
        }

        bool yesPrompt = HasFlag(args, "--yes", "-y");
        bool terminateLockers = HasFlag(args, "--terminate-lockers", "-t");
        bool recycle = HasFlag(args, "--recycle", "-r");
        bool dryRun = HasFlag(args, "--dry-run", "-d");
        int retries = int.TryParse(GetOptionValue(args, "--retry"), out int r) ? r : 3;

        var profiles = targets.Select(t => ForceDeleteEngine.Profile(t)).ToList();

        if (!isJson)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  [Deltempo] Force Delete & Stubborn Purge ({targets.Count} target(s))\n");
            Console.ResetColor();

            foreach (var p in profiles)
            {
                string kind = p.IsDirectory ? "Directory" : "File";
                Console.WriteLine($"  * Target:   {p.Path}");
                Console.WriteLine($"    Kind:     {kind} ({p.FormattedSize})");
                Console.WriteLine($"    Status:   {p.SummaryIssues}");
                if (p.GateDecision.Tier == ForceDeleteTier.AbsoluteBlock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    GATE:     ABSOLUTE BLOCK - {p.GateDecision.Rationale}");
                    Console.ResetColor();
                }
                else if (p.GateDecision.Tier == ForceDeleteTier.OverrideRequired)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"    GATE:     Explicit override required - {p.GateDecision.Rationale}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
        }

        if (profiles.All(p => p.GateDecision.Tier == ForceDeleteTier.AbsoluteBlock))
        {
            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { status = "error", message = "All targets are strictly protected system paths." }));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] All selected targets are strictly protected system paths. Deletion blocked.");
                Console.ResetColor();
            }
            return 1;
        }

        // Tier B (OverrideRequired) targets are shielded unless the operator explicitly
        // opts in with --force-override. --yes alone only means "yes, delete the eligible items".
        bool forceOverride = HasFlag(args, "--force-override", "-F");
        int overrideRequired = profiles.Count(p => p.GateDecision.Tier == ForceDeleteTier.OverrideRequired);

        if (overrideRequired > 0 && !forceOverride)
        {
            if (isJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "error",
                    message = $"{overrideRequired} target(s) are protected by the safety policy and require --force-override.",
                    overrideRequired
                }));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [!] {overrideRequired} target(s) are protected by the safety policy and will be SHIELDED:");
                foreach (var p in profiles.Where(p => p.GateDecision.Tier == ForceDeleteTier.OverrideRequired))
                {
                    Console.WriteLine($"        * {p.Path}");
                    Console.WriteLine($"          {p.GateDecision.Rationale}");
                }
                Console.WriteLine();
                Console.WriteLine("      Re-run with --force-override to delete these as well.");
                Console.ResetColor();
            }
            return 1;
        }

        if (!yesPrompt)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  [!] Are you sure you want to PERMANENTLY force delete the eligible items? (y/N): ");
            Console.ResetColor();
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || (!input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !input.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("  * Operation cancelled.");
                return 0;
            }
        }

        var options = new ForceDeleteOptions
        {
            StripReadOnlySystemHidden = true,
            TakeOwnershipAndResetAcl = true,
            TerminateLockingProcesses = terminateLockers,
            ScheduleRebootIfLocked = true,
            SendToRecycleBinInstead = recycle,
            DeleteRetryPasses = retries,
            DryRun = dryRun,
            ExplicitOverrideConfirmed = forceOverride
        };

        var result = await ForceDeleteService.ExecuteForceDeleteAsync(
            targets,
            options,
            logAction: (msg, lvl) =>
            {
                if (!isJson && lvl == LogLevel.Error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    {msg}");
                    Console.ResetColor();
                }
            });

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                filesDeleted = result.FilesDeleted,
                directoriesDeleted = result.DirectoriesDeleted,
                bytesFreed = result.BytesFreed,
                rebootScheduled = result.RebootScheduledCount,
                shielded = result.ShieldedCount,
                failed = result.FailedCount,
                attempts = result.Attempts
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [OK] Finished: {result.FilesDeleted} file(s), {result.DirectoriesDeleted} folder(s) deleted ({result.FormattedFreed} reclaimed)");
            Console.ResetColor();
            if (result.RebootScheduledCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [*] {result.RebootScheduledCount} item(s) scheduled for automatic deletion on next Windows reboot.");
                Console.ResetColor();
            }
            if (result.ShieldedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  [*] {result.ShieldedCount} protected/system item(s) preserved.");
                Console.ResetColor();
            }
            if (result.FailedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [X] {result.FailedCount} item(s) could not be removed.");
                Console.ResetColor();
            }
        }

        return result.FailedCount > 0 ? 1 : 0;
    }

    private static int HandleInspectForceDeleteTarget(string path, bool isJson)
    {
        var profile = ForceDeleteEngine.Profile(path);
        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  [Inspect Target] {profile.Path}");
        Console.ResetColor();
        Console.WriteLine($"  * Exists:        {profile.Exists}");
        Console.WriteLine($"  * Kind:          {(profile.IsDirectory ? "Directory" : "File")}");
        Console.WriteLine($"  * Size:          {profile.FormattedSize}");
        Console.WriteLine($"  * Read-Only/Sys: {profile.HasReadOnlyOrSystemAttributes}");
        Console.WriteLine($"  * Reparse Point: {profile.IsReparsePoint}");
        Console.WriteLine($"  * Status:        {profile.SummaryIssues}");
        Console.WriteLine($"  * Gate Tier:     {profile.GateDecision.Tier} ({profile.GateDecision.Rationale})");

        if (profile.LockingProcesses.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  Locking Process(es):");
            foreach (var proc in profile.LockingProcesses)
            {
                Console.WriteLine($"    * {proc.ProcessName} (PID {proc.ProcessId}) - {proc.AppDescription}");
            }
            Console.ResetColor();
        }

        return 0;
    }

    private static int HandleListPendingRebootPurges(bool isJson)
    {
        var pending = new List<string>();
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (key?.GetValue("PendingFileRenameOperations") is string[] multi)
            {
                for (int i = 0; i < multi.Length; i += 2)
                {
                    string src = multi[i];
                    string? dest = (i + 1 < multi.Length) ? multi[i + 1] : null;
                    if (string.IsNullOrEmpty(dest))
                    {
                        pending.Add(src);
                    }
                }
            }
        }
        catch { }

        if (isJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { pendingDeletions = pending }));
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Windows Session Manager Pending Reboot Purges ({pending.Count} item(s)):\n");
        Console.ResetColor();

        if (pending.Count == 0)
        {
            Console.WriteLine("    No files currently registered for next-reboot deletion.");
        }
        else
        {
            foreach (var item in pending)
            {
                Console.WriteLine($"    * {item}");
            }
        }

        return 0;
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

    private static bool TryValidatePath(string? path, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is null or empty.";
            return false;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "Path contains invalid characters.";
            return false;
        }

        if (path.Length > 260)
        {
            error = "Path exceeds maximum supported length (260 characters).";
            return false;
        }

        if (path.Contains("..") || path.Contains("~"))
        {
            error = "Path contains traversal sequences (.. or ~).";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Path.IsPathRooted(fullPath))
            {
                error = "Path must be absolute.";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"Path is malformed: {ex.Message}";
            return false;
        }

        return true;
    }
}
