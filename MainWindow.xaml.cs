using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class MainWindow : Window
{
    private readonly CleanerService _cleanerService = new();
    private readonly ObservableCollection<TargetFolderInfo> _targets = new();
    private readonly ObservableCollection<LogEntry> _logs = new();
    private readonly ObservableCollection<JunkFileItem> _inspectedFiles = new();
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private bool _isAdmin;
    private long _sessionTotalFreed;

    public MainWindow()
    {
        InitializeComponent();

        TargetCardsItemsControl.ItemsSource = _targets;
        LogItemsControl.ItemsSource = _logs;
        InspectorItemsControl.ItemsSource = _inspectedFiles;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CheckAdminPrivileges();
        UpdateDriveTelemetry();
        InitializeTargets();
        await RunScanAllAsync();
    }

    private void CheckAdminPrivileges()
    {
        _isAdmin = ElevationService.IsRunAsAdmin();
        if (_isAdmin)
        {
            AdminBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#162A20"));
            AddLog("Running with Administrator privileges (Full access to all system locations)", LogLevel.Success);
        }
        else
        {
            AdminBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A1E16"));
            AddLog("Running as Standard User (Some system directories may be restricted)", LogLevel.Warning);
        }
    }

    private void UpdateDriveTelemetry(long additionalFreedBytes = 0)
    {
        var telemetry = DriveTelemetryService.GetSystemDriveTelemetry();
        DriveTelemetryLabel.Text = $"OS Drive ({telemetry.DriveLetter})";
        DriveTelemetryPercentage.Text = $"{telemetry.FreePercentage:F1}% Free";
        DriveTelemetryDetails.Text = $"{telemetry.FormattedFree} free of {telemetry.FormattedTotal}";
        DriveUsageBar.Value = telemetry.UsedPercentage;

        if (additionalFreedBytes > 0)
        {
            _sessionTotalFreed += additionalFreedBytes;
            HeroSubtext.Text = $"Reclaimed {TargetFolderInfo.FormatBytes(_sessionTotalFreed)} this session • {telemetry.FormattedFree} currently free";
        }
    }

    private void InitializeTargets()
    {
        _targets.Clear();
        var defaultTargets = CleanerService.GetDefaultTargets();
        foreach (var target in defaultTargets)
        {
            _targets.Add(target);
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (InspectorModalOverlay.Visibility == Visibility.Visible)
            {
                CloseInspector_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5 || (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
        {
            if (!_isBusy)
            {
                ScanButton_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (!_isBusy)
            {
                CleanButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            var res = MessageBox.Show("A cleanup operation is currently in progress. Do you really want to exit?",
                "Cancel Cleanup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
            _cts?.Cancel();
        }
        Close();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        await RunScanAllAsync();
    }

    private async Task RunScanAllAsync()
    {
        _isBusy = true;
        SetControlsEnabled(false);
        _cts = new CancellationTokenSource();

        AppProgressBar.IsIndeterminate = true;
        ProgressStatusText.Text = "Scanning all target categories...";
        ProgressPercentageText.Text = "--";
        AddLog("Starting full precision scan of temporary locations...", LogLevel.Info);

        try
        {
            var scanTasks = _targets.Select(target => _cleanerService.ScanFolderAsync(target, AddLog, _cts.Token));
            await Task.WhenAll(scanTasks);

            RecalculateTotals();
            ProgressStatusText.Text = "Scan completed. Ready to clean.";
            AddLog($"Scan finished. Total reclaimable space: {HeroSizeText.Text}", LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Scan cancelled.";
            AddLog("Scan operation was cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "Scan error.";
            AddLog($"Scan error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            AppProgressBar.IsIndeterminate = false;
            AppProgressBar.Value = 0;
            ProgressPercentageText.Text = "0%";
            _isBusy = false;
            SetControlsEnabled(true);
        }
    }

    private async void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selectedTargets = _targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0)
        {
            MessageBox.Show("Please select at least one category to clean.",
                "No Selection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        bool safeMode = SafeModeCheckBox.IsChecked == true;
        _isBusy = true;
        SetControlsEnabled(false);
        CancelButton.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();

        var stopwatch = Stopwatch.StartNew();
        long totalFreed = 0;
        int totalFilesDeleted = 0;
        int totalFoldersDeleted = 0;
        int totalFilesSkipped = 0;

        AddLog($"Starting cleanup with Safety Shield {(safeMode ? "ENABLED (>24h old only)" : "DISABLED (all files)")}...", LogLevel.Info);

        try
        {
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                if (_cts.IsCancellationRequested) break;

                var target = selectedTargets[i];
                double targetBaseProgress = (double)i / selectedTargets.Count * 100;
                double targetSpan = 100.0 / selectedTargets.Count;

                ProgressStatusText.Text = $"Cleaning {target.Name}...";

                var progressHandler = new Action<double>(val =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        double currentPct = targetBaseProgress + (val * targetSpan);
                        AppProgressBar.Value = Math.Min(100, currentPct);
                        ProgressPercentageText.Text = $"{(int)AppProgressBar.Value}%";
                    });
                });

                var (freed, filesDel, foldersDel, filesSkip) = await _cleanerService.CleanFolderAsync(
                    target,
                    safeMode,
                    AddLog,
                    progressHandler,
                    _cts.Token);

                totalFreed += freed;
                totalFilesDeleted += filesDel;
                totalFoldersDeleted += foldersDel;
                totalFilesSkipped += filesSkip;
            }

            stopwatch.Stop();
            AppProgressBar.Value = 100;
            ProgressPercentageText.Text = "100%";

            RecalculateTotals();
            UpdateDriveTelemetry(totalFreed);

            var summary = new CleanSummary
            {
                TotalFreedBytes = totalFreed,
                TotalFilesDeleted = totalFilesDeleted,
                TotalFoldersDeleted = totalFoldersDeleted,
                TotalFilesSkipped = totalFilesSkipped,
                ElapsedTime = stopwatch.Elapsed
            };

            ProgressStatusText.Text = "Cleanup complete!";
            AddLog($"Cleanup Finished: Freed {summary.FormattedFreedSize} ({totalFilesDeleted:N0} deleted, {totalFilesSkipped:N0} protected) in {stopwatch.Elapsed.TotalSeconds:N1}s", LogLevel.Success);

            MessageBox.Show($"Deltempo Cleanup Completed!\n\n" +
                            $"• Reclaimed Disk Space: {summary.FormattedFreedSize}\n" +
                            $"• Files Safely Deleted: {totalFilesDeleted:N0}\n" +
                            $"• Subfolders Purged: {totalFoldersDeleted:N0}\n" +
                            $"• Files Protected / In-Use: {totalFilesSkipped:N0}\n" +
                            $"• Total Time: {stopwatch.Elapsed.TotalSeconds:N1} seconds",
                            "Deltempo Hero Cleanup Summary",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Cleanup cancelled.";
            AddLog("Cleanup was cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "Error during cleanup.";
            AddLog($"Cleanup Error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _isBusy = false;
            CancelButton.Visibility = Visibility.Collapsed;
            SetControlsEnabled(true);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
        ProgressStatusText.Text = "Cancelling operation...";
    }

    private void SelectSafeOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            // Select all 100% Safe caches, leave orphaned app leftovers for user review
            target.IsSelected = !target.IsOrphanedAppFolder;
        }
        RecalculateTotals();
        AddLog("Selected all 🟢 100% Safe Cache categories.", LogLevel.Info);
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            target.IsSelected = true;
        }
        RecalculateTotals();
    }

    private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            target.IsSelected = false;
        }
        RecalculateTotals();
    }

    private void TargetCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RecalculateTotals();
    }

    private void SafeModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        bool isSafe = SafeModeCheckBox.IsChecked == true;
        AddLog(isSafe 
            ? "Safety Shield ENABLED: Files created/modified within last 24 hours will be preserved." 
            : "Safety Shield DISABLED: All files in selected categories will be removed.", 
            LogLevel.Info);
    }

    private void InspectTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TargetFolderInfo target)
        {
            _inspectedFiles.Clear();
            foreach (var f in target.TopFiles)
            {
                _inspectedFiles.Add(f);
            }

            InspectorTitleText.Text = $"{target.Name} — Top Junk Files";
            InspectorSubtitleText.Text = $"Showing {target.TopFiles.Count} largest files found in {target.FolderPath}";
            InspectorModalOverlay.Visibility = Visibility.Visible;
        }
    }

    private void CloseInspector_Click(object sender, RoutedEventArgs e)
    {
        InspectorModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void ExportAuditReport_Click(object sender, RoutedEventArgs e)
    {
        var summary = new CleanSummary
        {
            TotalFreedBytes = _sessionTotalFreed,
            TotalFilesDeleted = 0,
            TotalFilesSkipped = 0
        };

        var report = CleanerService.GenerateAuditReport(_targets, summary, SafeModeCheckBox.IsChecked == true);
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var filePath = Path.Combine(desktopPath, $"Deltempo_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        try
        {
            File.WriteAllText(filePath, report);
            AddLog($"Audit report saved to: {filePath}", LogLevel.Success);
            MessageBox.Show($"Audit report exported successfully to your Desktop:\n\n{filePath}",
                "Audit Report Exported",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to export report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RecalculateTotals()
    {
        long selectedBytes = 0;
        int selectedFiles = 0;

        foreach (var target in _targets.Where(t => t.IsSelected))
        {
            selectedBytes += target.SizeBytes;
            selectedFiles += target.FileCount;
        }

        var formattedSize = TargetFolderInfo.FormatBytes(selectedBytes);
        HeroSizeText.Text = formattedSize;
        CleanButtonText.Text = $"Clean Selected ({formattedSize})";

        if (selectedBytes == 0)
        {
            HeroSubtext.Text = "Selected categories are clean or ready for scan";
        }
        else
        {
            HeroSubtext.Text = $"{selectedFiles:N0} junk items selected for precision removal";
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        QuickScanBtn.IsEnabled = enabled;
        CleanButton.IsEnabled = enabled;
    }

    private void ToggleLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (LogDrawerBorder.Visibility == Visibility.Visible)
        {
            LogDrawerBorder.Visibility = Visibility.Collapsed;
            ToggleLogText.Text = "Activity Log";
            ToggleLogIcon.Text = "\uE756";
        }
        else
        {
            LogDrawerBorder.Visibility = Visibility.Visible;
            ToggleLogText.Text = "Hide Log";
            ToggleLogIcon.Text = "\uE70D";
            LogScrollViewer.ScrollToEnd();
        }
    }

    private void CopyLogButton_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var log in _logs)
        {
            sb.AppendLine($"[{log.FormattedTime}] [{log.Level}] {log.Message}");
        }

        Clipboard.SetText(sb.ToString());
        AddLog("Activity log copied to clipboard.", LogLevel.Info);
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
    }

    private void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        Dispatcher.Invoke(() =>
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };
            _logs.Add(entry);
            if (LogDrawerBorder.Visibility == Visibility.Visible)
            {
                LogScrollViewer.ScrollToEnd();
            }
        });
    }
}
