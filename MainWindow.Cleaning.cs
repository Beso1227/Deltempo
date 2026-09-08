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

// Scan, smart-clean, 1-click deep clean, target selection, inspector and audit export.
public partial class MainWindow
{

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
            bool safeMode = SafeModeCheckBox.IsChecked == true;
            var scanTasks = _targets.Select(target => _cleanerService.ScanFolderAsync(target, AddLog, _cts.Token, safeMode));
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

    private void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selectedTargets = _targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0)
        {
            AddLog("Please select at least one category to clean.", LogLevel.Warning);
            ProgressStatusText.Text = "No categories selected.";
            return;
        }

        long totalEstimatedBytes = selectedTargets.Sum(t => t.SizeBytes);
        long totalEstimatedFiles = selectedTargets.Sum(t => (long)t.FileCount);
        bool safeMode = SafeModeCheckBox.IsChecked == true;

        ConfirmModalSizeText.Text = TargetFolderInfo.FormatBytes(totalEstimatedBytes);
        ConfirmModalShieldText.Text = safeMode ? "Safety Shield: Active" : "Safety Shield: Disabled";
        ConfirmModalShieldBadge.BorderBrush = safeMode ? (Brush)FindResource("EmeraldGreenBrush") : (Brush)FindResource("AmberWarningBrush");
        ConfirmModalShieldBadge.Background = safeMode ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10241B")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A1E16"));

        bool recycleBin = SettingsService.Current.SendToRecycleBin;
        if (recycleBin)
        {
            ConfirmModalDeletionModeTitle.Text = "Recycle Bin Protection: Active";
            ConfirmModalDeletionModeDesc.Text = "Files will be moved to the Windows Recycle Bin and can be restored if needed.";
            ConfirmModalDeletionModeIcon.Text = "\uE74D";
            ConfirmModalDeletionModeIcon.Foreground = (Brush)FindResource("ElectricCyanBrush");
            ConfirmModalDeletionModeBorder.BorderBrush = (Brush)FindResource("HairlineBorderBrush");
        }
        else
        {
            ConfirmModalDeletionModeTitle.Text = "Permanent Deletion: Active";
            ConfirmModalDeletionModeDesc.Text = "Files will be permanently deleted from disk to maximize free space and cannot be restored.";
            ConfirmModalDeletionModeIcon.Text = "\uE7BA";
            ConfirmModalDeletionModeIcon.Foreground = (Brush)FindResource("AmberWarningBrush");
            ConfirmModalDeletionModeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DF59E0B"));
        }

        ConfirmModalSummaryText.Text = $"Cleaning {selectedTargets.Count} selected categories ({totalEstimatedFiles:N0} estimated files). System integrity, user credentials, and personal files remain 100% protected.";
        ConfirmModalOverlay.Visibility = Visibility.Visible;
    }

    private void CancelConfirmModal_Click(object sender, RoutedEventArgs e)
    {
        ConfirmModalOverlay.Visibility = Visibility.Collapsed;
    }

    private async void ProceedConfirmModal_Click(object sender, RoutedEventArgs e)
    {
        ConfirmModalOverlay.Visibility = Visibility.Collapsed;
        await ExecuteCleanupAsync();
    }

    private async Task ExecuteCleanupAsync()
    {
        var selectedTargets = _targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0) return;

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

                long lastDispatchTicks = 0;
                var progressHandler = new Action<double>(val =>
                {
                    long now = Stopwatch.GetTimestamp();
                    if (val >= 1.0 || Stopwatch.GetElapsedTime(Interlocked.Read(ref lastDispatchTicks)).TotalMilliseconds >= 50)
                    {
                        Interlocked.Exchange(ref lastDispatchTicks, now);
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            double currentPct = targetBaseProgress + (val * targetSpan);
                            AppProgressBar.Value = Math.Min(100, currentPct);
                            ProgressPercentageText.Text = $"{(int)AppProgressBar.Value}%";
                        }));
                    }
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

            // Zero out cleaned targets immediately so hero size & button reflect reality right now,
            // then schedule a background rescan to re-measure all remaining dirty targets.
            foreach (var t in selectedTargets)
            {
                t.SizeBytes = 0;
                t.FileCount = 0;
            }
            RecalculateTotals(); // push zeroed values to HeroSizeText + CleanButtonText instantly

            // Background rescan: re-measures remaining targets and updates the hero with real numbers
            _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                await Dispatcher.InvokeAsync(() => ScanButton_Click(this, new RoutedEventArgs()));
            });

            _lastSummary = new CleanSummary
            {
                TotalFreedBytes = totalFreed,
                TotalFilesDeleted = totalFilesDeleted,
                TotalFoldersDeleted = totalFoldersDeleted,
                TotalFilesSkipped = totalFilesSkipped,
                ElapsedTime = stopwatch.Elapsed
            };

            ProgressStatusText.Text = "Cleanup complete!";
            AddLog($"Cleanup Finished: Freed {_lastSummary.FormattedFreedSize} ({totalFilesDeleted:N0} deleted, {totalFilesSkipped:N0} protected) in {stopwatch.Elapsed.TotalSeconds:N1}s", LogLevel.Success);

            // Show Animated Celebration Modal Dialog
            if (totalFilesSkipped > 0)
            {
                CelebrationModalTitleText.Text = "Cleanup Completed with Exceptions";
                CelebrationReclaimedText.Text = $"Reclaimed {_lastSummary.FormattedFreedSize} ({totalFilesSkipped:N0} in-use/protected files safely skipped)";
            }
            else
            {
                CelebrationModalTitleText.Text = "Cleanup Completed!";
                CelebrationReclaimedText.Text = $"Successfully Reclaimed {_lastSummary.FormattedFreedSize}";
            }
            CelebrationFilesText.Text = $"{totalFilesDeleted:N0}";
            CelebrationFoldersText.Text = $"{totalFoldersDeleted:N0}";
            CelebrationRamText.Text = "-- MB";
            CelebrationTimeText.Text = $"{stopwatch.Elapsed.TotalSeconds:N1}s";
            CelebrationModalOverlay.Visibility = Visibility.Visible;
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

    private void CloseCelebration_Click(object sender, RoutedEventArgs e)
    {
        CelebrationModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void CelebrationExport_Click(object sender, RoutedEventArgs e)
    {
        CelebrationModalOverlay.Visibility = Visibility.Collapsed;
        ExportAuditReport_Click(sender, e);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
        ProgressStatusText.Text = "Cancelling operation...";
    }

    private async void HeroOneClickDeepCleanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        SoundService.PlayClickSound();
        _isBusy = true;
        _cts = new CancellationTokenSource();
        SetControlsEnabled(false);
        CancelButton.Visibility = Visibility.Visible;
        CancelButton.IsEnabled = true;

        ProgressStatusText.Text = "Running 1-Click Deep Clean...";
        HeroSubtext.Text = "Autonomous deep clean in progress...";

        try
        {
            var progress = new Progress<DeepCleanProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressStatusText.Text = $"[{p.CurrentStage}] {p.DetailMessage}";
                    HeroSubtext.Text = p.DetailMessage;
                });
            });

            var result = await DeepCleanEngine.ExecuteDeepCleanAsync(
                logAction: (msg, lvl) => Dispatcher.Invoke(() => AddLog(msg, lvl)),
                progress: progress,
                purgeAllRestorePoints: false,
                ct: _cts.Token);

            _lastSummary = new CleanSummary
            {
                TotalFreedBytes = result.DiskFreedBytes,
                TotalFilesDeleted = result.FilesDeleted,
                TotalFoldersDeleted = result.FoldersDeleted,
                TotalFilesSkipped = result.FilesSkipped,
                ElapsedTime = result.Duration
            };

            ProgressStatusText.Text = $"1-Click Deep Clean complete! Reclaimed {result.FormattedDiskFreed} disk, {result.FormattedRamFreed} RAM.";
            HeroSubtext.Text = $"Last Clean: {result.FormattedDiskFreed} disk, {result.FormattedRamFreed} RAM freed in {result.Duration.TotalSeconds:0.1}s";
            AddLog($"1-Click Deep Clean complete: Reclaimed {result.FormattedDiskFreed} disk, {result.FormattedRamFreed} RAM ({result.FilesDeleted:N0} files deleted)", LogLevel.Success);

            // Update Telemetry & Hero Cards
            UpdateDriveTelemetry();
            UpdateMemoryTelemetry();

            // Display Celebration Modal
            CelebrationModalTitleText.Text = "1-Click Deep Clean Complete";
            CelebrationReclaimedText.Text = $"Reclaimed {result.FormattedDiskFreed} Disk & {result.FormattedRamFreed} RAM";
            CelebrationFilesText.Text = $"{result.FilesDeleted:N0}";
            CelebrationFoldersText.Text = $"{result.FoldersDeleted:N0}";
            CelebrationRamText.Text = result.FormattedRamFreed;
            CelebrationTimeText.Text = $"{result.Duration.TotalSeconds:0.1}s";
            CelebrationModalOverlay.Visibility = Visibility.Visible;

            // Trigger a background rescan to refresh target cards
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                await Dispatcher.InvokeAsync(() => ScanButton_Click(this, new RoutedEventArgs()));
            });
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Deep Clean cancelled.";
            HeroSubtext.Text = "Deep clean cancelled by user.";
            AddLog("1-Click Deep Clean was cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "Error during Deep Clean.";
            HeroSubtext.Text = $"Error: {ex.Message}";
            AddLog($"1-Click Deep Clean error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _isBusy = false;
            CancelButton.Visibility = Visibility.Collapsed;
            SetControlsEnabled(true);
        }
    }

    private async void SmartCleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        SelectSafeOnlyButton_Click(sender, e);
        await ExecuteCleanupAsync();
    }

    private void SelectSafeOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _targets)
        {
            // Select all 100% Safe caches, leave orphaned app leftovers for user review
            target.IsSelected = !target.IsOrphanedAppFolder;
        }
        RecalculateTotals();
        AddLog("Selected all 100% safe cache categories.", LogLevel.Info);
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
        int safeCount = 0;
        int reviewCount = 0;

        foreach (var target in _targets.Where(t => t.IsSelected))
        {
            selectedBytes += target.SizeBytes;
            selectedFiles += target.FileCount;
            if (target.SafetyBadge.Contains("REVIEW", StringComparison.OrdinalIgnoreCase))
                reviewCount++;
            else
                safeCount++;
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
            string breakdown = reviewCount > 0 ? $" ({safeCount} safe, {reviewCount} review required)" : " (100% verified safe)";
            HeroSubtext.Text = $"{selectedFiles:N0} junk items selected across {_targets.Count(t => t.IsSelected)} categories{breakdown}";
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        if (HeroScanBtn != null) HeroScanBtn.IsEnabled = enabled;
        if (CleanButton != null) CleanButton.IsEnabled = enabled;
        if (HeroOneClickDeepCleanBtn != null) HeroOneClickDeepCleanBtn.IsEnabled = enabled;
        if (FooterOneClickBtn != null) FooterOneClickBtn.IsEnabled = enabled;
    }

    private void ToggleLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (LogDrawerBorder.Visibility == Visibility.Visible)
        {
            LogDrawerBorder.Visibility = Visibility.Collapsed;
            BottomActionDockBorder.CornerRadius = new CornerRadius(0, 0, 19, 19);
            ToggleLogText.Text = "Activity Log";
            ToggleLogIcon.Text = "\uE756";
        }
        else
        {
            LogDrawerBorder.Visibility = Visibility.Visible;
            BottomActionDockBorder.CornerRadius = new CornerRadius(0, 0, 0, 0);
            LogDrawerBorder.CornerRadius = new CornerRadius(0, 0, 19, 19);
            ToggleLogText.Text = "Hide Log";
            ToggleLogIcon.Text = "\uE70D";
            LogScrollViewer.ScrollToEnd();
        }
    }
}
