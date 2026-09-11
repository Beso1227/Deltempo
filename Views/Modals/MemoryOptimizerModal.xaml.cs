using System.Windows;
using System.Windows.Controls;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

public partial class MemoryOptimizerModal : UserControl
{
    public event Action<string, LogLevel>? LogRequested;
    public event Action? TelemetryRefreshRequested;
    public event Action? Closed;

    public MemoryOptimizerModal()
    {
        InitializeComponent();
    }

    public void Open()
    {
        SoundService.PlayClickSound();
        Visibility = Visibility.Visible;
        RefreshData();
    }

    public void CloseModal()
    {
        SoundService.PlayClickSound();
        Visibility = Visibility.Collapsed;
        TelemetryRefreshRequested?.Invoke();
        Closed?.Invoke();
    }

    private void CloseMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        CloseModal();
    }

    public void RefreshData()
    {
        try
        {
            UpdateTelemetry();
            var snapshots = MemoryOptimizerService.GetMemoryAreaSnapshots();
            MemoryAreaItemsControl.ItemsSource = snapshots;

            int available = snapshots.Count(s => s.IsAvailableOnThisOs);
            MemoryModalStatusText.Text = $"{snapshots.Count} memory zones · {available} available for privileged NT flush";
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Failed to refresh memory zones: {ex.Message}", LogLevel.Warning);
        }
    }

    public void UpdateTelemetry(MemoryInfo? mem = null)
    {
        try
        {
            mem ??= MemoryOptimizerService.GetMemoryInfo();
            if (ModalTotalRamText != null)
                ModalTotalRamText.Text = mem.FormattedTotal;
            if (ModalUsedRamText != null)
                ModalUsedRamText.Text = $"{mem.FormattedUsed} ({mem.UsedPercent:F0}%) In Use";
            if (ModalStandbyCacheText != null)
                ModalStandbyCacheText.Text = mem.FormattedSystemCache;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Telemetry update suppressed: {ex.Message}");
        }
    }

    private void SelectAllMemoryZones_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (MemoryAreaItemsControl.ItemsSource is IEnumerable<MemoryAreaSnapshot> list)
        {
            foreach (var item in list) item.IsSelected = true;
            MemoryAreaItemsControl.Items.Refresh();
        }
    }

    private void DeselectAllMemoryZones_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (MemoryAreaItemsControl.ItemsSource is IEnumerable<MemoryAreaSnapshot> list)
        {
            foreach (var item in list) item.IsSelected = false;
            MemoryAreaItemsControl.Items.Refresh();
        }
    }

    private async void QuickTrimWorkingSets_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        LogRequested?.Invoke("[Process Optimizer] Trimming non-whitelisted process working sets...", LogLevel.Info);
        var res = await MemoryOptimizerService.OptimizeAreaAsync(MemoryTargetType.WorkingSet);
        if (res.Success)
        {
            LogRequested?.Invoke($"✓ Process Working Sets Trimmed: Reclaimed {res.FormattedFreed} across {res.ProcessesOptimized} tasks.", LogLevel.Success);
            RefreshData();
            TelemetryRefreshRequested?.Invoke();
        }
        else
        {
            LogRequested?.Invoke($"Working sets trim failed: {res.ErrorMessage}", LogLevel.Error);
        }
    }

    private async void PurgeSelectedZones_Click(object sender, RoutedEventArgs e)
    {
        if (MemoryAreaItemsControl.ItemsSource is not IEnumerable<MemoryAreaSnapshot> list)
            return;

        var selected = list.Where(s => s.IsSelected && s.IsAvailableOnThisOs).Select(s => s.Target).ToArray();
        if (selected.Length == 0)
        {
            LogRequested?.Invoke("No memory zones selected for purge.", LogLevel.Warning);
            return;
        }

        PurgeSelectedZonesBtn.IsEnabled = false;
        PurgeSelectedZonesBtn.Content = "Purging NT Cache...";
        SoundService.PlayClickSound();

        try
        {
            var result = await MemoryOptimizerService.OptimizeRamAsync(selected);
            LogRequested?.Invoke($"[NT Kernel] Memory Clean Complete: Purged {result.FormattedReclaimed} in {result.ExecutionTimeMs}ms across {result.AreaResults.Count} zones.", LogLevel.Success);
            RefreshData();
            TelemetryRefreshRequested?.Invoke();
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Memory purge error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            PurgeSelectedZonesBtn.IsEnabled = true;
            PurgeSelectedZonesBtn.Content = "Purge Selected Zones";
        }
    }

    private async void PerAreaBoostButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not MemoryTargetType target)
            return;

        btn.IsEnabled = false;
        btn.Content = "⋯";
        SoundService.PlayClickSound();

        try
        {
            var result = await MemoryOptimizerService.OptimizeAreaAsync(target);
            if (result.Success)
            {
                LogRequested?.Invoke($"Flushed {target}: Reclaimed {result.FormattedFreed}.", LogLevel.Success);
                RefreshData();
                TelemetryRefreshRequested?.Invoke();
            }
            else
            {
                LogRequested?.Invoke($"Failed to flush {target}: {result.ErrorMessage}", LogLevel.Error);
            }
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Error flushing {target}: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Flush";
        }
    }
}
