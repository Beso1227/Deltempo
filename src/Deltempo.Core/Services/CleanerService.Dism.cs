using System.Diagnostics;
using System.IO;
using WinTempCleaner.Core.Abstractions;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public partial class CleanerService
{
    /// <summary>
    /// Reclaims superseded Windows component-store payloads through DISM.
    /// </summary>
    /// <param name="resetBase">
    /// When true, appends <c>/ResetBase</c>: this is what Disk Cleanup's "Windows Update
    /// Cleanup" does, and it makes every installed update permanently non-uninstallable.
    /// The interactive scope passes true only after the user selects it explicitly; the
    /// autonomous deep-clean stage leaves it false.
    /// </param>
    /// <remarks>
    /// The previous implementation redirected stdout/stderr, never read either pipe, and
    /// then called WaitForExit(180_000). A servicing run produces far more output than a
    /// pipe buffer holds, so the child could block forever on a full pipe while the parent
    /// waited on it. It also read ExitCode before proving the process had exited. This now
    /// goes through SystemProcessRunner, which drains both streams concurrently and
    /// enforces the timeout for real.
    /// </remarks>
    public static async Task<(bool Success, long ReclaimedBytes, string Message)> RunDismComponentCleanupAsync(
        Action<string, LogLevel>? logAction = null,
        CancellationToken ct = default,
        bool resetBase = false)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            return (false, 0, "Administrator privileges required to run Component Store cleanup.");
        }

        string dismPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
        if (!File.Exists(dismPath)) return (false, 0, "dism.exe not found");

        try
        {
            logAction?.Invoke(
                resetBase
                    ? "Purging all superseded Windows update payloads via DISM (/StartComponentCleanup /ResetBase)..."
                    : "Initiating Windows Component Store (WinSxS) deep cleanup via DISM (purging superseded updates)...",
                LogLevel.Info);

            // Analysis is advisory: if DISM cannot report the store size the cleanup still
            // proceeds, and the reclaimed figure is reported as zero rather than invented.
            var before = await DismComponentStoreAnalyzer.AnalyzeAsync(ct).ConfigureAwait(false);

            string arguments = resetBase
                ? "/English /Online /Cleanup-Image /StartComponentCleanup /ResetBase /NoRestart"
                : "/English /Online /Cleanup-Image /StartComponentCleanup /NoRestart";

            // Servicing a large component store legitimately takes tens of minutes; the
            // bound exists so a wedged servicing stack cannot hang the app forever.
            var result = await SystemProcessRunner.Instance.RunAsync(
                dismPath,
                arguments,
                TimeSpan.FromMinutes(60),
                ct).ConfigureAwait(false);

            // 3010 is ERROR_SUCCESS_REBOOT_REQUIRED: cleanup completed, reboot to finish.
            if (result.TimedOut || (result.ExitCode != 0 && result.ExitCode != 3010))
            {
                string detail = LastMeaningfulLine(result.StandardOutput, result.StandardError);
                logAction?.Invoke(
                    $"DISM component cleanup failed ({(result.TimedOut ? "timed out" : $"exit code {result.ExitCode}")})" +
                    (detail.Length > 0 ? $": {detail}" : "."),
                    LogLevel.Warning);
                return (false, 0, result.TimedOut ? "DISM timed out." : $"DISM exit code: {result.ExitCode}");
            }

            var after = await DismComponentStoreAnalyzer.AnalyzeAsync(ct).ConfigureAwait(false);

            long reclaimed = 0;
            if (before != null && after != null)
            {
                reclaimed = Math.Max(0, before.ActualSizeBytes - after.ActualSizeBytes);
            }
            else if (before != null)
            {
                // Analysis after the run is unavailable; report the pre-run estimate
                // instead of claiming the cleanup freed nothing.
                reclaimed = before.EstimatedReclaimableBytes;
            }

            if (result.ExitCode == 3010)
            {
                logAction?.Invoke(
                    $"Windows Component Store cleanup completed ({TargetFolderInfo.FormatBytes(reclaimed)} reclaimed). A reboot is required to finish.",
                    LogLevel.Success);
                return (true, reclaimed, "Component store cleaned successfully. Reboot required to finish.");
            }

            logAction?.Invoke(
                $"Windows Component Store cleanup completed: {TargetFolderInfo.FormatBytes(reclaimed)} reclaimed.",
                LogLevel.Success);
            return (true, reclaimed, "Component store cleaned successfully.");
        }
        catch (OperationCanceledException)
        {
            logAction?.Invoke("DISM component cleanup was cancelled.", LogLevel.Warning);
            throw;
        }
        catch (Exception ex)
        {
            logAction?.Invoke($"DISM execution note: {ex.Message}", LogLevel.Warning);
            return (false, 0, ex.Message);
        }
    }

    private static string LastMeaningfulLine(string stdout, string stderr)
    {
        foreach (string raw in (stdout + "\n" + stderr).Split('\n').Reverse())
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("===", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal)) continue;
            return line.Length > 240 ? line[..240] : line;
        }

        return string.Empty;
    }

    public static (long UsedBytes, int SnapshotCount) QueryShadowStorageInfo()
    {
        if (!ElevationService.IsRunAsAdmin()) return (0, 0);

        long usedBytes = 0;
        int count = 0;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "vssadmin.exe"),
                Arguments = "list shadowstorage",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = SystemProcessRunner.DrainOutput(proc, TimeSpan.FromSeconds(10));

                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("Used Shadow Copy Storage", StringComparison.OrdinalIgnoreCase) ||
                        (line.Contains("Used", StringComparison.OrdinalIgnoreCase) && line.Contains("Storage", StringComparison.OrdinalIgnoreCase)))
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            var sizePart = parts[1].Split('(')[0].Trim();
                            usedBytes += ParseSizeStringToBytes(sizePart);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Deltempo.Cleaner] Vssadmin query shadowstorage failed: {ex.Message}");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "vssadmin.exe"),
                Arguments = "list shadows",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = SystemProcessRunner.DrainOutput(proc, TimeSpan.FromSeconds(10));

                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("Shadow Copy ID:", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Shadow Copy Set ID:", StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Deltempo.Cleaner] Vssadmin query shadows failed: {ex.Message}");
        }

        return (usedBytes, Math.Max(count, usedBytes > 0 ? 1 : 0));
    }

    public static long ParseSizeStringToBytes(string sizeStr)
    {
        ArgumentNullException.ThrowIfNull(sizeStr);

        try
        {
            var trimmed = sizeStr.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return 0;

            double multiplier = 1;
            if (trimmed.EndsWith("TB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L * 1024 * 1024 * 1024;
            else if (trimmed.EndsWith("GB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L * 1024 * 1024;
            else if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L * 1024;
            else if (trimmed.EndsWith("KB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L;
            else if (trimmed.EndsWith("B", StringComparison.OrdinalIgnoreCase)) multiplier = 1;

            var numberPart = new string(trimmed.TakeWhile(c => char.IsDigit(c) || c == '.' || c == ',').ToArray())
                .Replace(',', '.');

            if (double.TryParse(numberPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return (long)(val * multiplier);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Deltempo.Cleaner] ParseSizeStringToBytes note: {ex.Message}");
        }
        return 0;
    }

    public static async Task<(bool Success, long ReclaimedBytes, string Message)> CleanRestorePointsAsync(
        bool purgeAll = false,
        Action<string, LogLevel>? logAction = null,
        CancellationToken ct = default)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            return (false, 0, "Administrator privileges required to manage restore points.");
        }

        return await Task.Run(() =>
        {
            try
            {
                var before = QueryShadowStorageInfo();
                if (before.UsedBytes == 0 && before.SnapshotCount == 0)
                {
                    logAction?.Invoke("Checked System Restore Points: No shadow copies found (0 bytes).", LogLevel.Info);
                    return (true, 0, "No restore points found to delete.");
                }

                logAction?.Invoke(purgeAll
                    ? "Purging all Volume Shadow Copies & System Restore Points..."
                    : "Purging older System Restore Points (safely preserving newest restore point)...", LogLevel.Info);

                string args = purgeAll
                    ? "delete shadows /all /quiet"
                    : "delete shadows /for=C: /oldest /quiet";

                var psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "vssadmin.exe"),
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return (false, 0, "Could not start vssadmin.exe");

                proc.WaitForExit(30_000);

                var after = QueryShadowStorageInfo();
                long reclaimed = Math.Max(0, before.UsedBytes - after.UsedBytes);
                if (reclaimed == 0 && before.UsedBytes > 0 && proc.ExitCode == 0)
                {
                    reclaimed = before.UsedBytes;
                }

                logAction?.Invoke($"Restore points cleanup finished: {TargetFolderInfo.FormatBytes(reclaimed)} reclaimed.", LogLevel.Success);
                return (true, reclaimed, "Restore points cleaned successfully.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error cleaning restore points: {ex.Message}", LogLevel.Warning);
                return (false, 0, ex.Message);
            }
        }, ct);
    }
}
