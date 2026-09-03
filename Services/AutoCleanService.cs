using System.Timers;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class AutoCleanService
{
    private static System.Timers.Timer? _timer;
    private static bool _isCleaning;

    public static void Start()
    {
        Stop();

        if (!SettingsService.Current.EnableAutoPilot) return;

        double intervalMs = Math.Max(1, SettingsService.Current.AutoCleanIntervalHours) * 60 * 60 * 1000;
        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += async (s, e) => await ExecuteSilentAutoCleanAsync();
        _timer.AutoReset = true;
        _timer.Start();
    }

    public static void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    public static async Task ExecuteSilentAutoCleanAsync()
    {
        if (_isCleaning) return;
        _isCleaning = true;

        try
        {
            var cleanerService = new CleanerService();
            var targets = CleanerService.GetDefaultTargets();

            // Only clean safe targets
            var safeTargets = targets.Where(t => !t.IsOrphanedAppFolder).ToList();
            using var cts = new CancellationTokenSource();

            long totalFreed = 0;
            int totalFiles = 0;

            foreach (var target in safeTargets)
            {
                var (freed, filesDel, foldersDel, filesSkip) = await cleanerService.CleanFolderAsync(
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
