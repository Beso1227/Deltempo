using System.Runtime.InteropServices;
using System.Timers;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class AutoCleanService
{
    private static System.Timers.Timer? _timer;
    private static System.Timers.Timer? _idleTimer;
    private static bool _isCleaning;
    private static DateTime _lastIdleCleanUtc = DateTime.MinValue;
    private static CleanerService? _cleanerInstance;
    private static CleanerService CleanerInstance => _cleanerInstance ??= new CleanerService();

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (GetLastInputInfo(ref info))
        {
            uint idleTicks = unchecked((uint)Environment.TickCount) - info.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }
        return TimeSpan.Zero;
    }

    public static void Start()
    {
        Stop();

        if (SettingsService.Current.EnableAutoPilot)
        {
            double intervalMs = Math.Max(1, SettingsService.Current.AutoCleanIntervalHours) * 60 * 60 * 1000;
            _timer = new System.Timers.Timer(intervalMs);
            _timer.Elapsed += async (s, e) => await ExecuteSilentAutoCleanAsync();
            _timer.AutoReset = true;
            _timer.Start();
        }

        if (SettingsService.Current.AutoCleanOnIdle)
        {
            // Check idle state every 60 seconds
            _idleTimer = new System.Timers.Timer(60_000);
            _idleTimer.Elapsed += async (s, e) =>
            {
                if (!SettingsService.Current.AutoCleanOnIdle) return;
                var idleTime = GetIdleTime();
                int thresholdMinutes = Math.Max(1, SettingsService.Current.AutoCleanIdleMinutes);
                if (idleTime.TotalMinutes >= thresholdMinutes)
                {
                    // Throttle idle cleanups to at most once every 4 hours
                    if ((DateTime.UtcNow - _lastIdleCleanUtc).TotalHours >= 4)
                    {
                        _lastIdleCleanUtc = DateTime.UtcNow;
                        await ExecuteSilentAutoCleanAsync();
                    }
                }
            };
            _idleTimer.AutoReset = true;
            _idleTimer.Start();
        }
    }

    public static void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }

        if (_idleTimer != null)
        {
            _idleTimer.Stop();
            _idleTimer.Dispose();
            _idleTimer = null;
        }
    }

    public static async Task ExecuteSilentAutoCleanAsync()
    {
        if (_isCleaning) return;
        _isCleaning = true;

        try
        {
            var targets = CleanerService.GetDefaultTargets();

            // Only clean safe targets
            var safeTargets = targets.Where(t => !t.IsOrphanedAppFolder).ToList();
            using var cts = new CancellationTokenSource();

            long totalFreed = 0;
            int totalFiles = 0;

            foreach (var target in safeTargets)
            {
                var (freed, filesDel, foldersDel, filesSkip) = await CleanerInstance.CleanFolderAsync(
                    target,
                    safeMode24Hours: true,
                    logAction: (msg, lvl) => { },
                    progressReport: p => { },
                    ct: cts.Token);

                totalFreed += freed;
                totalFiles += filesDel;
            }

            if (totalFreed > 0 && SettingsService.Current.AutoCleanNotify)
            {
                TrayService.ShowNotification(
                    "Deltempo Auto-Pilot Guardian",
                    $"Silently reclaimed {TargetFolderInfo.FormatBytes(totalFreed)} of background junk across {totalFiles:N0} files.");
            }

            // Background RAM Guardian: check if memory auto-optimization is enabled and free RAM is below threshold
            if (SettingsService.Current.MemoryAutoOptimizeEnabled)
            {
                var memInfo = MemoryOptimizerService.GetMemoryInfo();
                double freePercent = 100.0 - memInfo.UsedPercent;
                if (freePercent < SettingsService.Current.MemoryAutoOptimizeFreeRamThresholdPercent)
                {
                    var ramRes = await MemoryOptimizerService.OptimizeRamAsync(new[]
                    {
                        MemoryTargetType.StandbyList,
                        MemoryTargetType.StandbyListLowPriority
                    });

                    if (ramRes.MeasuredBytesFreed > 0 && SettingsService.Current.MemoryShowNotifications)
                    {
                        TrayService.ShowNotification(
                            "Deltempo Memory Guardian",
                            $"Auto-purged standby cache: reclaimed {ramRes.FormattedReclaimed} to relieve RAM pressure.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
        finally
        {
            _isCleaning = false;
        }
    }
}
