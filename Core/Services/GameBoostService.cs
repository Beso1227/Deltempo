using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinTempCleaner.Services;

public class GameBoostResult
{
    public bool IsActive { get; set; }
    public string ActivePowerPlan { get; set; } = "Default";
    public string? PreviousPowerPlan { get; set; }
    public long StandbyMemoryFreedBytes { get; set; }
    public int ThrottledProcessesCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class GameBoostService
{
    public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string BalancedPerformanceGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static readonly Regex GuidRegex = new(
        @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}",
        RegexOptions.Compiled);

    private static readonly string[] ThrottlableProcesses =
    [
        "OneDrive",
        "MicrosoftEdgeUpdate",
        "GoogleUpdate",
        "SearchApp",
        "Cortana",
        "Dropbox",
        "Spotify",
        "Teams",
        "EpicGamesLauncher"
    ];

    private static readonly Dictionary<int, ProcessPriorityClass> SavedPriorities = new();
    private static readonly object SyncLock = new();

    public static bool IsBoostActive { get; private set; }
    public static string? SavedPriorPowerSchemeGuid { get; private set; }

    public static string ExtractPowerSchemeGuid(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null!;

        var match = GuidRegex.Match(output);
        return match.Success ? match.Value : null!;
    }

    public static string GetTargetPowerSchemeGuid(bool useUltimate = true)
    {
        return useUltimate ? UltimatePerformanceGuid : HighPerformanceGuid;
    }

    public static IReadOnlyList<string> GetThrottlableProcessNames()
    {
        return ThrottlableProcesses;
    }

    public static async Task<GameBoostResult> ToggleGameBoostAsync(bool enable, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            lock (SyncLock)
            {
                if (enable == IsBoostActive)
                {
                    return new GameBoostResult
                    {
                        IsActive = IsBoostActive,
                        Message = IsBoostActive ? "Game Boost is already active." : "Game Boost is already disabled."
                    };
                }
            }

            var result = new GameBoostResult();

            if (enable)
            {
                // 1. Capture current power plan
                string? currentScheme = GetCurrentPowerScheme();
                SavedPriorPowerSchemeGuid = currentScheme;
                result.PreviousPowerPlan = currentScheme ?? "Balanced";

                // 2. Set power plan to Ultimate or High Performance
                bool switched = SetPowerScheme(UltimatePerformanceGuid);
                if (!switched)
                {
                    // Fallback to High Performance
                    switched = SetPowerScheme(HighPerformanceGuid);
                    result.ActivePowerPlan = switched ? "High Performance" : "Default";
                }
                else
                {
                    result.ActivePowerPlan = "Ultimate Performance";
                }

                // 3. Purge RAM Standby List and Working Sets
                try
                {
                    var optResult = await MemoryOptimizerService.OptimizeRamAsync(targets: null, ct: ct);
                    result.StandbyMemoryFreedBytes = optResult.MeasuredBytesFreed;
                }
                catch
                {
                    // Non-critical, continue
                }

                // 4. Throttle background non-essential processes
                int throttledCount = ThrottleBackgroundProcesses();
                result.ThrottledProcessesCount = throttledCount;

                lock (SyncLock)
                {
                    IsBoostActive = true;
                }

                result.IsActive = true;
                result.Message = $"Game Boost Activated: Power mode set to {result.ActivePowerPlan}, {throttledCount} background apps throttled.";
            }
            else
            {
                // Deactivate: restore power scheme
                string targetRestore = SavedPriorPowerSchemeGuid ?? BalancedPerformanceGuid;
                SetPowerScheme(targetRestore);
                result.ActivePowerPlan = "Standard (Restored)";

                // Restore process priorities
                RestoreBackgroundProcesses();

                lock (SyncLock)
                {
                    IsBoostActive = false;
                    SavedPriorPowerSchemeGuid = null;
                }

                result.IsActive = false;
                result.Message = "Game Boost Deactivated: Normal power plan and background priorities restored.";
            }

            return result;
        }, ct);
    }

    private static string? GetCurrentPowerScheme()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            return ExtractPowerSchemeGuid(output);
        }
        catch
        {
            return null;
        }
    }

    private static bool SetPowerScheme(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid)) return false;

        try
        {
            var psi = new ProcessStartInfo("powercfg", $"/setactive {guid}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static int ThrottleBackgroundProcesses()
    {
        int count = 0;
        lock (SyncLock)
        {
            SavedPriorities.Clear();
            foreach (var procName in ThrottlableProcesses)
            {
                try
                {
                    var processes = Process.GetProcessesByName(procName);
                    foreach (var proc in processes)
                    {
                        try
                        {
                            if (!SavedPriorities.ContainsKey(proc.Id))
                            {
                                SavedPriorities[proc.Id] = proc.PriorityClass;
                            }
                            proc.PriorityClass = ProcessPriorityClass.Idle;
                            count++;
                        }
                        catch
                        {
                            // Access denied or terminated
                        }
                    }
                }
                catch
                {
                    // Non-critical
                }
            }
        }
        return count;
    }

    private static void RestoreBackgroundProcesses()
    {
        lock (SyncLock)
        {
            foreach (var kvp in SavedPriorities)
            {
                try
                {
                    var proc = Process.GetProcessById(kvp.Key);
                    proc.PriorityClass = kvp.Value;
                }
                catch
                {
                    // Process may have exited
                }
            }
            SavedPriorities.Clear();
        }
    }
}
