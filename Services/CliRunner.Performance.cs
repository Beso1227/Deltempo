using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

// CLI command handlers: boost (RAM optimization) and process management.
public static partial class CliRunner
{

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
            Console.WriteLine($"  [Deltempo] Executing {boostType}...");
            Console.WriteLine($"     Before: {beforeMem.FormattedUsed} used / {beforeMem.FormattedTotal} ({beforeMem.UsedPercent:F0}% Used)");
            Console.ResetColor();
        }

        var res = await MemoryOptimizerService.OptimizeRamAsync(selectedTargets?.ToArray());
        var afterMem = MemoryOptimizerService.GetMemoryInfo();
        long actualFreed = Math.Max(res.MeasuredBytesFreed, Math.Max(0, afterMem.AvailablePhysicalBytes - beforeMem.AvailablePhysicalBytes));

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

        return res.Success ? 0 : 1;
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
            Console.WriteLine(JsonSerializer.Serialize(procs.Take(topLimit).Select(p => new
            {
                p.ProcessName,
                p.ProcessId,
                p.WorkingSetBytes,
                p.FormattedMemory,
                p.CategoryDescription,
                p.IsSafeToClose,
                p.ProcessCount,
                p.DisplayName
            }), new JsonSerializerOptions { WriteIndented = true }));
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
}
