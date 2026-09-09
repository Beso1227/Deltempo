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

// Startup item management and background process manager modals.
public partial class MainWindow
{

    private async void OpenStartupModal_Click(object sender, RoutedEventArgs e)
    {
        StartupModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        await ReloadStartupItemsAsync();
    }

    private async Task ReloadStartupItemsAsync()
    {
        StartupStatusText.Text = "Scanning startup entries & Windows registry hives...";
        StartupSearchBox.Text = string.Empty;

        _allStartupItems = await StartupManagerService.GetStartupItemsAsync();
        ApplyStartupFilter();

        int enabledCount = _allStartupItems.Count(x => x.IsEnabled);
        int disabledCount = _allStartupItems.Count - enabledCount;
        StartupStatusText.Text = $"Found {_allStartupItems.Count} startup programs ({enabledCount} enabled, {disabledCount} disabled)";
    }

    private void RefreshStartup_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = ReloadStartupItemsAsync();
    }

    private void StartupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        StartupSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(StartupSearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyStartupFilter();
    }

    private void ApplyStartupFilter()
    {
        if (_allStartupItems == null) return;
        string query = StartupSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            StartupItemsControl.ItemsSource = _allStartupItems;
        }
        else
        {
            StartupItemsControl.ItemsSource = _allStartupItems.Where(x =>
                x.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.LocationDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    private void CloseStartupModal_Click(object sender, RoutedEventArgs e)
    {
        StartupModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void StartupSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is StartupItem item)
        {
            bool isEnabled = cb.IsChecked == true;

            // If item requires Administrator rights and app is running non-elevated:
            if (!ElevationService.IsRunAsAdmin() && (item.Location.Contains("HKLM") || item.Location.Contains("Common")))
            {
                cb.IsChecked = !isEnabled;
                MessageBox.Show(
                    $"Administrator privileges are required to modify system-wide startup app '{item.DisplayTitle}'.\n\nPlease click the 'Admin' button in the top bar to elevate Deltempo.",
                    "Elevation Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AddLog($"Elevation required to toggle '{item.DisplayTitle}'.", LogLevel.Warning);
                return;
            }

            bool success = StartupManagerService.ToggleStartupItem(item, isEnabled);
            if (success)
            {
                AddLog($"Startup app '{item.DisplayTitle}' is now {(isEnabled ? "ENABLED" : "DISABLED")}.", LogLevel.Info);
                int enabledCount = _allStartupItems.Count(x => x.IsEnabled);
                int disabledCount = _allStartupItems.Count - enabledCount;
                StartupStatusText.Text = $"Found {_allStartupItems.Count} startup programs ({enabledCount} enabled, {disabledCount} disabled)";
            }
            else
            {
                cb.IsChecked = !isEnabled;
                AddLog($"Could not change startup status for '{item.DisplayTitle}'.", LogLevel.Warning);
            }
        }
    }

    // 2. Large File Hunter Handlers
    private CancellationTokenSource? _largeFileScanCts;

    private async void OpenProcessModal_Click(object sender, RoutedEventArgs e)
    {
        ProcessModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        await ReloadProcessesAsync();
    }

    private async Task ReloadProcessesAsync()
    {
        ProcessStatusSummaryText.Text = "Analyzing running background tasks...";
        ProcessSearchBox.Text = string.Empty;

        _allProcesses = await ProcessOptimizerService.GetHeavyProcessesAsync(20L * 1024 * 1024);
        ApplyProcessFilter();

        long totalRam = _allProcesses.Sum(x => x.WorkingSetBytes);
        ProcessStatusSummaryText.Text = $"{_allProcesses.Count} active apps ({TargetFolderInfo.FormatBytes(totalRam)} RAM)";
    }

    private void RefreshProcess_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = ReloadProcessesAsync();
    }

    private void ProcessSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ProcessSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(ProcessSearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        if (_allProcesses == null) return;
        string query = ProcessSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            ProcessItemsControl.ItemsSource = _allProcesses;
        }
        else
        {
            ProcessItemsControl.ItemsSource = _allProcesses.Where(x =>
                x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.CategoryDescription.Contains(query, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    private void CloseProcessModal_Click(object sender, RoutedEventArgs e)
    {
        ProcessModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void TrimProcessMemory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProcessMemoryInfo proc)
        {
            var pids = proc.ProcessIds.Count > 0 ? proc.ProcessIds : new List<int> { proc.ProcessId };
            var (ok, freed) = ProcessOptimizerService.TrimProcessMemoryEx(pids);
            if (ok)
            {
                if (freed > 0)
                {
                    AddLog($"Trimmed '{proc.DisplayName}': Reclaimed {TargetFolderInfo.FormatBytes(freed)} RAM.", LogLevel.Success);
                }
                else
                {
                    AddLog($"Trimmed working memory for '{proc.DisplayName}'.", LogLevel.Success);
                }
                UpdateMemoryTelemetry();
                _ = ReloadProcessesAsync();
            }
            else
            {
                AddLog($"Notice: '{proc.DisplayName}' working set is already minimal or restricted.", LogLevel.Info);
            }
        }
    }

    private void TerminateProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProcessMemoryInfo proc)
        {
            var res = MessageBox.Show(
                $"Safely end background task '{proc.DisplayName}'?",
                "End Background Task",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = ProcessOptimizerService.SafeTerminateProcess(proc.ProcessIds.Count > 0 ? proc.ProcessIds : new List<int> { proc.ProcessId });
                if (ok)
                {
                    AddLog($"Terminated task '{proc.DisplayName}'.", LogLevel.Info);
                    UpdateMemoryTelemetry();
                    _ = ReloadProcessesAsync();
                }
                else
                {
                    AddLog($"Could not terminate task '{proc.DisplayName}' (access restricted or protected).", LogLevel.Warning);
                }
            }
        }
    }

    private async void TrimAllProcesses_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        var res = await MemoryOptimizerService.OptimizeRamAsync();
        AddLog($"[Process Optimizer] Trimmed background working sets: Reclaimed {res.FormattedReclaimed}.", LogLevel.Success);
        UpdateMemoryTelemetry();
        _ = ReloadProcessesAsync();
    }
}
