using System.Runtime.InteropServices;
using System.Timers;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class AutoCleanService
{
    private static System.Timers.Timer? _timer;
    private static System.Timers.Timer? _idleTimer;
    /// <summary>
    /// Re-entrancy guard for the background auto-clean.
    /// </summary>
    /// <remarks>
    /// This was a plain non-volatile bool with no lock. Two independent timers - the
    /// interval timer and the idle timer - could both read it false and both enter
    /// ExecuteSilentAutoCleanAsync, running two concurrent clean sweeps over the same
    /// targets. CompareExchange makes the check-and-set atomic.
    /// </remarks>
    private static int _isCleaning;

    /// <summary>The CTS of the in-flight background run, so it can actually be cancelled.</summary>
    private static CancellationTokenSource? _currentRunCts;

    private static DateTime _lastIdleCleanUtc = DateTime.MinValue;
    private static CleanerService? _cleanerInstance;
    private static CleanerService CleanerInstance => _cleanerInstance ??= new CleanerService();

    /// <summary>
    /// True while a background auto-clean is in progress.
    /// </summary>
    public static bool IsCleaning => Volatile.Read(ref _isCleaning) != 0;

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
        // Atomic test-and-set: two timers must not both enter.
        if (Interlocked.CompareExchange(ref _isCleaning, 1, 0) != 0) return;

        try
        {
            var targets = CleanerService.GetDefaultTargets();

            // Only clean safe targets
            var safeTargets = targets.Where(t => !t.IsOrphanedAppFolder).ToList();
            // Previously this CTS was never cancelled, so the token threaded into
            // CleanFolderAsync was inert and a background auto-clean could not be stopped.
            // It is now published so StopAutoClean can cancel it, and the loop honours it.
            using var cts = new CancellationTokenSource();
            Volatile.Write(ref _currentRunCts, cts);

            long totalFreed = 0;
            int totalFiles = 0;
            int totalFailed = 0;
            int categoriesAttempted = 0;
            int categoriesFailed = 0;

            // Each target is cleaned inside its OWN try. A single try around the whole loop
            // meant an exception on target 3 abandoned targets 4-26 with no record at all,
            // while totalFreed from targets 1-2 still fired the success notification.
            foreach (var target in safeTargets)
            {
                // Honour a StopAutoClean request instead of running every remaining target.
                if (cts.IsCancellationRequested) break;

                categoriesAttempted++;
                try
                {
                    var (freed, filesDel, foldersDel, filesSkip, filesFailed) = await CleanerInstance.CleanFolderAsync(
                        target,
                        safeMode24Hours: true,
                        logAction: (msg, lvl) => { },
                        progressReport: p => { },
                        ct: cts.Token);

                    totalFreed += freed;
                    totalFiles += filesDel;
                    totalFailed += filesFailed;
                    if (filesFailed > 0 || target.HasError) categoriesFailed++;
                }
                catch (Exception ex)
                {
                    categoriesFailed++;
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Auto-clean aborted for target '{target.Name}': {ex.Message}");
                }
            }

            if (totalFreed > 0 && SettingsService.Current.AutoCleanNotify)
            {
                TrayService.ShowNotification(
                    "Deltempo Auto-Pilot Guardian",
                    categoriesFailed > 0
                        ? $"Reclaimed {TargetFolderInfo.FormatBytes(totalFreed)} of background junk across {totalFiles:N0} files, " +
                          $"but {categoriesFailed} of {categoriesAttempted} categories reported errors."
                        : $"Silently reclaimed {TargetFolderInfo.FormatBytes(totalFreed)} of background junk across {totalFiles:N0} files.");
            }

            // Background RAM Guardian: opt-in, and only when memory telemetry is actually valid.
            if (SettingsService.Current.MemoryAutoOptimizeEnabled)
            {
                var memInfo = MemoryOptimizerService.GetMemoryInfo();
                if (memInfo.MeasurementSucceeded && memInfo.TotalPhysicalBytes > 0)
                {
                    double freePercent = 100.0 - memInfo.UsedPercent;
                    if (freePercent < SettingsService.Current.MemoryAutoOptimizeFreeRamThresholdPercent)
                    {
                        var ramRes = await MemoryOptimizerService.OptimizeRamAsync(new[]
                        {
                            MemoryTargetType.StandbyList,
                            MemoryTargetType.StandbyListLowPriority
                        });

                        if (SettingsService.Current.MemoryShowNotifications)
                        {
                            // Deliberately does not claim a performance benefit. Windows serves new
                            // allocations from the standby cache first, so discarding it under memory
                            // pressure usually means the next allocation has to read from disk.
                            TrayService.ShowNotification(
                                "Deltempo Memory Guardian",
                                $"Discarded standby cache ({ramRes.SucceededTargetCount}/{ramRes.RequestedTargetCount} zones confirmed). " +
                                "Available RAM may rise; recently used files are re-read from disk on next access.");
                        }
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
            Volatile.Write(ref _isCleaning, 0);
            Interlocked.Exchange(ref _currentRunCts, null);
        }
    }

    /// <summary>
    /// Cancels an in-flight background auto-clean. Previously impossible: the CTS created
    /// per run was a local that was never cancelled, so the token passed into
    /// CleanFolderAsync did nothing.
    /// </summary>
    public static void StopAutoClean()
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _currentRunCts, null);
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run finished between the exchange and the cancel.
        }
    }
}
