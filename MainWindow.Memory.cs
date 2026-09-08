using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

// RAM telemetry, memory-zone modal and NT kernel boost actions.
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
                UpdateModalMemoryTelemetry(mem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private void UpdateModalMemoryTelemetry(MemoryInfo? mem = null)
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

    private void OpenMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        MemoryModalOverlay.Visibility = Visibility.Visible;
        RefreshMemoryModalData();
    }

    private void CloseMemoryCleanerModal_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        MemoryModalOverlay.Visibility = Visibility.Collapsed;
        UpdateMemoryTelemetry();
    }

    private void RefreshMemoryModalData()
    {
        try
        {
            UpdateModalMemoryTelemetry();
            var snapshots = MemoryOptimizerService.GetMemoryAreaSnapshots();
            MemoryAreaItemsControl.ItemsSource = snapshots;

            int available = snapshots.Count(s => s.IsAvailableOnThisOs);
            MemoryModalStatusText.Text = $"{snapshots.Count} memory zones · {available} available for privileged NT flush";
        }
        catch (Exception ex)
        {
            AddLog($"Failed to refresh memory zones: {ex.Message}", LogLevel.Warning);
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
        AddLog("[Process Optimizer] Trimming non-whitelisted process working sets...", LogLevel.Info);
        var res = await MemoryOptimizerService.OptimizeAreaAsync(MemoryTargetType.WorkingSet);
        if (res.Success)
        {
            AddLog($"✓ Process Working Sets Trimmed: Reclaimed {res.FormattedFreed} across {res.ProcessesOptimized} tasks.", LogLevel.Success);
            RefreshMemoryModalData();
            UpdateMemoryTelemetry();
        }
        else
        {
            AddLog($"Working sets trim failed: {res.ErrorMessage}", LogLevel.Error);
        }
    }

    private async void PurgeSelectedZones_Click(object sender, RoutedEventArgs e)
    {
        if (MemoryAreaItemsControl.ItemsSource is not IEnumerable<MemoryAreaSnapshot> list)
            return;

        var selected = list.Where(s => s.IsSelected && s.IsAvailableOnThisOs).Select(s => s.Target).ToArray();
        if (selected.Length == 0)
        {
            AddLog("No memory zones selected for purge.", LogLevel.Warning);
            return;
        }

        PurgeSelectedZonesBtn.IsEnabled = false;
        PurgeSelectedZonesBtn.Content = "Purging NT Cache...";
        SoundService.PlayClickSound();

        try
        {
            var result = await MemoryOptimizerService.OptimizeRamAsync(selected);
            AddLog($"[NT Kernel] Memory Clean Complete: Purged {result.FormattedReclaimed} in {result.ExecutionTimeMs}ms across {result.AreaResults.Count} zones.", LogLevel.Success);
            RefreshMemoryModalData();
            UpdateMemoryTelemetry();
        }
        catch (Exception ex)
        {
            AddLog($"Memory purge error: {ex.Message}", LogLevel.Error);
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
                AddLog($"Flushed {target}: Reclaimed {result.FormattedFreed}.", LogLevel.Success);
                RefreshMemoryModalData();
                UpdateMemoryTelemetry();
            }
            else
            {
                AddLog($"Failed to flush {target}: {result.ErrorMessage}", LogLevel.Error);
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error flushing {target}: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "Flush";
        }
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
