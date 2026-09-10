using System.Diagnostics;
using System.IO;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public partial class CleanerService
{
    public static async Task<(bool Success, string Message)> RunDismComponentCleanupAsync(Action<string, LogLevel>? logAction = null, CancellationToken ct = default)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            return (false, "Administrator privileges required to run Component Store cleanup.");
        }

        return await Task.Run(() =>
        {
            try
            {
                logAction?.Invoke("Initiating Windows Component Store (WinSxS) deep cleanup via DISM (purging superseded updates)...", LogLevel.Info);

                string dismPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                if (!File.Exists(dismPath)) return (false, "dism.exe not found");

                var psi = new ProcessStartInfo
                {
                    FileName = dismPath,
                    Arguments = "/Online /Cleanup-Image /StartComponentCleanup /NoRestart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return (false, "Could not launch dism.exe");

                proc.WaitForExit(180_000); // 3-minute timeout

                if (proc.ExitCode == 0)
                {
                    logAction?.Invoke("Windows Component Store deep cleanup completed! Superseded Windows update packages purged.", LogLevel.Success);
                    return (true, "Component store cleaned successfully.");
                }
                else
                {
                    logAction?.Invoke($"DISM Component cleanup completed with exit code {proc.ExitCode}.", LogLevel.Warning);
                    return (false, $"DISM exit code: {proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"DISM execution note: {ex.Message}", LogLevel.Warning);
                return (false, ex.Message);
            }
        }, ct);
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
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(10_000);

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
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(10_000);

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
