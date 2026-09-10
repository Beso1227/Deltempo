using System.Windows;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

// RAM telemetry and NT kernel boost actions.
public partial class MainWindow
{
    private readonly ViewModels.MemoryOptimizationViewModel _memoryViewModel = new();

    private void InitializeMemoryViewModel()
    {
        _memoryViewModel.LogRequested += (msg, level) => AddLog(msg, level);
        _memoryViewModel.PropertyChanged += (_, e) =>
        {
            if (HeroRamPercentText != null)
                HeroRamPercentText.Text = $"{_memoryViewModel.UsedPercent:F0}%";
            if (HeroRamDetailText != null)
                HeroRamDetailText.Text = _memoryViewModel.FormattedDetail;
            if (HeroRamProgressBar != null)
                HeroRamProgressBar.Value = _memoryViewModel.UsedPercent;
            if (HeroBoostRamBtn != null)
            {
                HeroBoostRamBtn.IsEnabled = !_memoryViewModel.IsBoosting;
                HeroBoostRamBtn.Content = _memoryViewModel.BoostButtonText;
            }
        };
    }

    private void UpdateMemoryTelemetry()
    {
        _memoryViewModel.RefreshTelemetry();
        if (MemoryModalOverlay != null && MemoryModalOverlay.Visibility == Visibility.Visible)
        {
            var mem = MemoryOptimizerService.GetMemoryInfo();
            MemoryModalOverlay.UpdateTelemetry(mem);
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
        await _memoryViewModel.ExecuteBoostAsync();
    }

    // 1. Startup Accelerator Handlers
    private List<StartupItem> _allStartupItems = new();
}
