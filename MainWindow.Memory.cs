using System.Windows;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

// RAM telemetry and NT kernel boost actions.
public partial class MainWindow
{
    private void UpdateMemoryTelemetry()
    {
        try
        {
            var mem = MemoryOptimizerService.GetMemoryInfo();
            if (HeroRamPercentText != null)
                HeroRamPercentText.Text = $"{mem.UsedPercent:F0}%";
            if (HeroRamDetailText != null)
                HeroRamDetailText.Text = $"{mem.FormattedUsed} / {mem.FormattedTotal} Used";
            if (HeroRamProgressBar != null)
                HeroRamProgressBar.Value = mem.UsedPercent;

            if (MemoryModalOverlay != null && MemoryModalOverlay.Visibility == Visibility.Visible)
            {
                MemoryModalOverlay.UpdateTelemetry(mem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private void OpenMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        MemoryModalOverlay.Open();
    }

    private void CloseMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        MemoryModalOverlay.CloseModal();
        UpdateMemoryTelemetry();
    }

    private async void HeroBoostRamBtn_Click(object sender, RoutedEventArgs e)
    {
        HeroBoostRamBtn.IsEnabled = false;
        HeroBoostRamBtn.Content = "Boosting...";
        SoundService.PlayClickSound();

        try
        {
            var res = await MemoryOptimizerService.OptimizeRamAsync();
            UpdateMemoryTelemetry();
            AddLog($"[RAM Engine] Boost Complete: Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} processes in {res.ExecutionTimeMs}ms.", LogLevel.Success);
            HeroBoostRamBtn.Content = $"✓ -{res.FormattedReclaimed}";
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            AddLog($"RAM optimization error: {ex.Message}", LogLevel.Warning);
        }
        finally
        {
            HeroBoostRamBtn.IsEnabled = true;
            HeroBoostRamBtn.Content = "Quick Boost";
        }
    }

    // 1. Startup Accelerator Handlers
    private List<StartupItem> _allStartupItems = new();
}
