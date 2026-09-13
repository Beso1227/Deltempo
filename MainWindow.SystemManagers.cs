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

    private string _startupFilterTab = "ALL";

    private void OpenStartupModal_Click(object sender, RoutedEventArgs e)
    {
        SwitchWorkspaceView(WorkspaceView.Startup);
    }

    private async Task ReloadStartupItemsAsync()
    {
        StartupStatusText.Text = "Scanning startup entries, Windows registry hives & Task Scheduler...";
        StartupSearchBox.Text = string.Empty;

        _allStartupItems = await StartupManagerService.GetStartupItemsAsync();
        UpdateStartupHeroStats();
        ApplyStartupFilter();

        int enabledCount = _allStartupItems.Count(x => x.IsEnabled);
        int disabledCount = _allStartupItems.Count - enabledCount;
        StartupStatusText.Text = $"Found {_allStartupItems.Count} startup programs ({enabledCount} enabled, {disabledCount} disabled)";
    }

    private void UpdateStartupHeroStats()
    {
        if (_allStartupItems == null) return;
        int total = _allStartupItems.Count;
        int high = _allStartupItems.Count(x => x.IsEnabled && x.Impact == BootImpact.High);
        int orphaned = _allStartupItems.Count(x => x.IsFileMissing);
        double delaySec = StartupManagerService.CalculateEstimatedBootDelaySeconds(_allStartupItems);

        if (StartupTotalAppsHeroText != null)
            StartupTotalAppsHeroText.Text = $"{total} apps";

        if (StartupHighImpactHeroText != null)
            StartupHighImpactHeroText.Text = $"{high} high impact";

        if (StartupBootDelayHeroText != null)
            StartupBootDelayHeroText.Text = $"~{delaySec:F1}s delay";

        if (StartupOrphanedHeroText != null)
            StartupOrphanedHeroText.Text = $"{orphaned} cleanable";
    }

    private void RefreshStartup_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = ReloadStartupItemsAsync();
    }

    private void StartupFilterTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            SoundService.PlayClickSound();
            if (StartupFilterAllBtn != null) StartupFilterAllBtn.Tag = null;
            if (StartupFilterHighBtn != null) StartupFilterHighBtn.Tag = null;
            if (StartupFilterDisabledBtn != null) StartupFilterDisabledBtn.Tag = null;
            if (StartupFilterOrphanedBtn != null) StartupFilterOrphanedBtn.Tag = null;

            btn.Tag = "Active";

            if (btn == StartupFilterHighBtn) _startupFilterTab = "HIGH";
            else if (btn == StartupFilterDisabledBtn) _startupFilterTab = "DISABLED";
            else if (btn == StartupFilterOrphanedBtn) _startupFilterTab = "ORPHANED";
            else _startupFilterTab = "ALL";

            ApplyStartupFilter();
        }
    }

    private void StartupOptimizeBootBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_allStartupItems == null || _allStartupItems.Count == 0) return;

        var disabled = StartupManagerService.OptimizeBoot(_allStartupItems);
        if (disabled.Count > 0)
        {
            AddLog($"Boot optimized: safely disabled {disabled.Count} non-essential high-impact background startup apps.", LogLevel.Success);
            StartupStatusText.Text = $"Optimized boot: {disabled.Count} non-essential apps disabled. Boot delay reduced.";
            UpdateStartupHeroStats();
            ApplyStartupFilter();
        }
        else
        {
            StartupStatusText.Text = "All active startup apps are already optimized.";
        }
    }

    private void StartupCleanOrphaned_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            var res = MessageBox.Show(
                $"Remove orphaned startup entry for '{item.DisplayTitle}'?\n\nLocation: {item.LocationDisplay}",
                "Clean Orphaned Entry",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                if (StartupManagerService.RemoveOrphanedStartupItem(item))
                {
                    _allStartupItems?.Remove(item);
                    AddLog($"Cleaned orphaned startup entry: '{item.DisplayTitle}'", LogLevel.Success);
                    UpdateStartupHeroStats();
                    ApplyStartupFilter();
                }
                else
                {
                    AddLog($"Failed to remove orphaned entry: '{item.DisplayTitle}'", LogLevel.Warning);
                }
            }
        }
    }

    private void StartupReveal_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            string path = !string.IsNullOrWhiteSpace(item.ExePath) && File.Exists(item.ExePath)
                ? item.ExePath
                : item.Command;

            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
        }
    }

    private void StartupSearchOnline_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            string query = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.DisplayTitle;
            try
            {
                Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(query + " process windows startup")}")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }
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

        IEnumerable<StartupItem> filtered = _allStartupItems;

        if (_startupFilterTab == "HIGH")
        {
            filtered = filtered.Where(x => x.Impact == BootImpact.High);
        }
        else if (_startupFilterTab == "DISABLED")
        {
            filtered = filtered.Where(x => !x.IsEnabled);
        }
        else if (_startupFilterTab == "ORPHANED")
        {
            filtered = filtered.Where(x => x.IsFileMissing);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(x =>
                x.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.LocationDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            );
        }

        StartupItemsControl.ItemsSource = filtered.ToList();
    }

    private void CloseStartupModal_Click(object sender, RoutedEventArgs e)
    {
        SwitchWorkspaceView(WorkspaceView.Cleaner);
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
                UpdateStartupHeroStats();
            }
            else
            {
                cb.IsChecked = !isEnabled;
                AddLog($"Could not change startup status for '{item.DisplayTitle}'.", LogLevel.Warning);
            }
        }
    }

    private async void StartupItemAi_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            // If already expanded, toggle collapse
            if (item.IsExpanded && item.IsAiEnriched)
            {
                item.IsExpanded = false;
                return;
            }

            item.IsExpanded = true;
            StartupStatusText.Text = $"Analyzing disable impact for '{item.DisplayTitle}' with AI...";

            try
            {
                var report = await StartupIntelligenceService.AnalyzeStartupItemAsync(item, forceOnline: true);
                if (report != null)
                {
                    item.Description = report.Description;
                    item.DisableVerdict = report.Verdict;
                    item.DisableImpact = report.DisableImpact;
                    item.Recommendation = report.Recommendation;
                    item.IsAiEnriched = true;
                    item.AiProviderUsed = report.ProviderUsed;
                    StartupStatusText.Text = $"Analysis complete for '{item.DisplayTitle}' via {report.ProviderUsed}.";
                    AddLog($"AI Startup Intelligence analyzed '{item.DisplayTitle}': {report.Verdict} ({report.ProviderUsed})", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                StartupStatusText.Text = $"AI analysis failed: {ex.Message}";
                AddLog($"Startup AI error: {ex.Message}", LogLevel.Warning);
            }
        }
    }

    private void StartupItemCloseExpand_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is StartupItem item)
        {
            item.IsExpanded = false;
        }
    }

    private async void StartupBatchAi_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_allStartupItems == null || _allStartupItems.Count == 0) return;

        var unanalyzed = _allStartupItems.Where(x => !x.IsAiEnriched && x.DisableVerdict == StartupDisableVerdict.SafeToDisable && string.IsNullOrWhiteSpace(x.Recommendation)).ToList();
        if (unanalyzed.Count == 0)
        {
            unanalyzed = _allStartupItems.Where(x => !x.IsAiEnriched).ToList();
        }

        if (unanalyzed.Count == 0)
        {
            StartupStatusText.Text = "All startup items are already analyzed and verified.";
            return;
        }

        StartupBatchAiBtn.IsEnabled = false;
        int count = 0;
        int total = unanalyzed.Count;

        try
        {
            foreach (var item in unanalyzed)
            {
                count++;
                StartupStatusText.Text = $"AI analyzing startup items ({count}/{total}): {item.DisplayTitle}...";
                var report = await StartupIntelligenceService.AnalyzeStartupItemAsync(item, forceOnline: false);
                if (report != null)
                {
                    item.Description = report.Description;
                    item.DisableVerdict = report.Verdict;
                    item.DisableImpact = report.DisableImpact;
                    item.Recommendation = report.Recommendation;
                    item.IsAiEnriched = true;
                    item.AiProviderUsed = report.ProviderUsed;
                }
            }

            StartupStatusText.Text = $"Batch AI analysis completed for {total} startup items.";
            AddLog($"Completed AI analysis on {total} startup programs.", LogLevel.Success);
        }
        catch (Exception ex)
        {
            StartupStatusText.Text = $"Batch analysis ended: {ex.Message}";
        }
        finally
        {
            StartupBatchAiBtn.IsEnabled = true;
        }
    }

    // 2. Large File Hunter Handlers
    private CancellationTokenSource? _largeFileScanCts;

    private void OpenProcessModal_Click(object sender, RoutedEventArgs e)
    {
        SwitchWorkspaceView(WorkspaceView.Processes);
    }

    private async Task ReloadProcessesAsync()
    {
        ProcessStatusSummaryText.Text = "Analyzing running background tasks...";
        ProcessSearchBox.Text = string.Empty;

        _allProcesses = await ProcessOptimizerService.GetHeavyProcessesAsync(20L * 1024 * 1024);

        foreach (var proc in _allProcesses)
        {
            if (proc.AppIcon == null)
            {
                proc.AppIcon = AppIconService.GetAppIcon(proc.ExePath, Path.GetDirectoryName(proc.ExePath) ?? "", proc.DisplayName);
            }
        }

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
        SwitchWorkspaceView(WorkspaceView.Cleaner);
    }

    private void TrimProcessMemory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProcessMemoryInfo proc)
        {
            var pids = proc.ProcessIds.Count > 0 ? proc.ProcessIds : [proc.ProcessId];
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
                bool ok = ProcessOptimizerService.SafeTerminateProcess(proc.ProcessIds.Count > 0 ? proc.ProcessIds : [proc.ProcessId]);
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
