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

// Large File Hunter: drive scan, AI safety verdicts, selection and recycling.
public partial class MainWindow
{

    private bool _isLargeFileScanRunning;

    private void PopulateLargeFileDrives()
    {
        if (LargeFileDriveComboBox.Items.Count > 0) return;

        LargeFileDriveComboBox.Items.Add(new ComboBoxItem { Content = "All Drives", Tag = "ALL", IsSelected = true });

        var drives = LargeFileHunterService.GetAvailableDrives();
        foreach (var d in drives)
        {
            LargeFileDriveComboBox.Items.Add(new ComboBoxItem { Content = $"Drive {d}", Tag = d });
        }

        LargeFileDriveComboBox.Items.Add(new ComboBoxItem { Content = "User Profile & OneDrive", Tag = "USER" });
        if (LargeFileCategoryComboBox != null && LargeFileCategoryComboBox.SelectedIndex < 0)
        {
            LargeFileCategoryComboBox.SelectedIndex = 0;
        }
    }

    private void RefreshLargeFileHeroStats()
    {
        if (!_isLoaded || _largeFiles == null) return;
        long totalBytes = _largeFiles.Sum(f => f.SizeBytes);
        int safeCount = _largeFiles.Count(f => f.IsAiSafe);
        long safeBytes = _largeFiles.Where(f => f.IsAiSafe).Sum(f => f.SizeBytes);
        long protectedBytes = Math.Max(0, totalBytes - safeBytes);
        int protectedCount = Math.Max(0, _largeFiles.Count - safeCount);

        if (LargeFilesTotalStorageText != null)
            LargeFilesTotalStorageText.Text = $"{TargetFolderInfo.FormatBytes(totalBytes)} ({_largeFiles.Count} files)";

        if (LargeFilesSafeStorageText != null)
            LargeFilesSafeStorageText.Text = $"{TargetFolderInfo.FormatBytes(safeBytes)} ({safeCount} safe)";

        if (LargeFilesProtectedStorageText != null)
            LargeFilesProtectedStorageText.Text = $"{TargetFolderInfo.FormatBytes(protectedBytes)} ({protectedCount} protected)";
    }

    private async Task RunLargeFileScanAsync()
    {
        if (_isLargeFileScanRunning)
        {
            _largeFileScanCts?.Cancel();
            return;
        }

        _isLargeFileScanRunning = true;
        _largeFileScanCts = new CancellationTokenSource();
        var ct = _largeFileScanCts.Token;

        try
        {
            if (RescanLargeFilesBtn != null) RescanLargeFilesBtn.IsEnabled = false;

            string scope = "ALL";
            if (LargeFileDriveComboBox?.SelectedItem is ComboBoxItem selectedScope && selectedScope.Tag is string scopeTag)
            {
                scope = scopeTag;
            }

            long minBytes = 50L * 1024 * 1024;
            if (LargeFileSizeComboBox?.SelectedItem is ComboBoxItem selectedSize && selectedSize.Tag is string sizeStr && long.TryParse(sizeStr, out long parsedSize))
            {
                minBytes = parsedSize;
            }

            string readableScope = scope == "ALL" ? "all drives" : scope == "USER" ? "user profile" : scope;
            LargeFilesStatusText.Text = $"Scanning {readableScope} for files > {TargetFolderInfo.FormatBytes(minBytes)}...";
            if (LargeFilesEmptyState != null)
            {
                LargeFilesEmptyStateTitle.Text = $"Scanning {readableScope}...";
                LargeFilesEmptyStateHint.Text = "Analyzing drives for large files. This may take a moment.";
                LargeFilesEmptyState.Visibility = Visibility.Visible;
            }

            var progress = new Progress<int>(pct =>
            {
                LargeFilesStatusText.Text = $"Scanning {readableScope}... ({pct}%)";
            });

            var scanResult = await LargeFileHunterService.ScanLargeFilesAsync(minBytes, scope, maxResults: 250, progress: progress, ct: ct);
            var files = scanResult.Files;

            _largeFiles.Clear();
            foreach (var f in files)
            {
                _largeFiles.Add(f);
            }

            var view = CollectionViewSource.GetDefaultView(_largeFiles);
            if (view != null)
            {
                view.Filter = FilterLargeFileItem;
                view.Refresh();
            }

            UpdateLargeFileSelectionSummary();
            RefreshLargeFileHeroStats();

            long totalBytes = files.Sum(f => f.SizeBytes);
            int safeCount = files.Count(f => f.IsAiSafe);
            long safeBytes = files.Where(f => f.IsAiSafe).Sum(f => f.SizeBytes);

            LargeFilesStatusText.Text = $"Discovered {files.Count} files ({TargetFolderInfo.FormatBytes(totalBytes)}). AI verified {safeCount} 100% safe to clean.";

            // Update empty state visibility
            if (LargeFilesEmptyState != null)
            {
                if (files.Count == 0)
                {
                    LargeFilesEmptyStateTitle.Text = "No large files found";
                    LargeFilesEmptyStateHint.Text = "Try a different scope or lower the minimum file size.";
                    LargeFilesEmptyState.Visibility = Visibility.Visible;
                }
                else
                {
                    LargeFilesEmptyState.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch (OperationCanceledException)
        {
            LargeFilesStatusText.Text = "Scan cancelled.";
            if (LargeFilesEmptyState != null)
            {
                LargeFilesEmptyStateTitle.Text = "Scan cancelled";
                LargeFilesEmptyStateHint.Text = "Click Scan Now to run a new scan.";
                LargeFilesEmptyState.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            LargeFilesStatusText.Text = $"Scan completed with warnings: {ex.Message}";
            if (LargeFilesEmptyState != null)
            {
                LargeFilesEmptyStateTitle.Text = "Scan encountered an issue";
                LargeFilesEmptyStateHint.Text = ex.Message;
                LargeFilesEmptyState.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _isLargeFileScanRunning = false;
            if (RescanLargeFilesBtn != null) RescanLargeFilesBtn.IsEnabled = true;
        }
    }

    private async void OpenLargeFilesModal_Click(object sender, RoutedEventArgs e)
    {
        LargeFilesModalOverlay.Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        PopulateLargeFileDrives();
        await RunLargeFileScanAsync();
    }

    private async void LargeFileFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || LargeFilesModalOverlay == null) return;
        if (LargeFilesModalOverlay.Visibility == Visibility.Visible && !_isLargeFileScanRunning)
        {
            await RunLargeFileScanAsync();
        }
    }

    private void LargeFileCategoryFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _largeFiles == null || LargeFileCategoryComboBox == null || LargeFilesSelectedSummaryText == null) return;
        var view = CollectionViewSource.GetDefaultView(_largeFiles);
        if (view != null)
        {
            view.Filter = FilterLargeFileItem;
            view.Refresh();
        }
        UpdateLargeFileSelectionSummary();
    }

    private void LargeFileSort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _largeFiles == null || LargeFileSortComboBox == null) return;
        var view = CollectionViewSource.GetDefaultView(_largeFiles);
        if (view == null) return;

        view.SortDescriptions.Clear();
        string tag = (LargeFileSortComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "SIZE_DESC";
        switch (tag)
        {
            case "SIZE_ASC":
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("SizeBytes", System.ComponentModel.ListSortDirection.Ascending));
                break;
            case "SAFETY":
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("SafetyScore", System.ComponentModel.ListSortDirection.Descending));
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("SizeBytes", System.ComponentModel.ListSortDirection.Descending));
                break;
            case "NAME":
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("FileName", System.ComponentModel.ListSortDirection.Ascending));
                break;
            case "SIZE_DESC":
            default:
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("SizeBytes", System.ComponentModel.ListSortDirection.Descending));
                break;
        }
        view.Refresh();
    }

    private bool FilterLargeFileItem(object obj)
    {
        if (obj is not LargeFileInfo item) return false;

        if (LargeFileCategoryComboBox?.SelectedItem is ComboBoxItem cItem && cItem.Tag is string tag && tag != "ALL")
        {
            if (tag == "SAFE") return item.IsAiSafe;
            if (tag == "RISK") return !item.IsAiSafe;
            if (!string.Equals(tag, item.Category, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private void UpdateLargeFileSelectionSummary()
    {
        if (!_isLoaded || LargeFilesSelectedSummaryText == null || RecycleSelectedLargeFilesBtn == null || _largeFiles == null)
            return;

        int selectedCount = _largeFiles.Count(f => f.IsSelected);
        long selectedBytes = _largeFiles.Where(f => f.IsSelected).Sum(f => f.SizeBytes);

        LargeFilesSelectedSummaryText.Text = $"Selected: {selectedCount} files ({TargetFolderInfo.FormatBytes(selectedBytes)})";
        RecycleSelectedLargeFilesBtn.Content = $"Recycle Selected ({TargetFolderInfo.FormatBytes(selectedBytes)})";
        RecycleSelectedLargeFilesBtn.IsEnabled = selectedCount > 0;
    }

    private void LargeFileItem_CheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateLargeFileSelectionSummary();
    }

    private void SelectSafeLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        foreach (var f in _largeFiles)
        {
            f.IsSelected = f.IsAiSafe;
        }
        UpdateLargeFileSelectionSummary();
    }

    private void SelectAllLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        foreach (var f in _largeFiles)
        {
            f.IsSelected = true;
        }
        UpdateLargeFileSelectionSummary();
    }

    private void ClearLargeFilesSelection_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        foreach (var f in _largeFiles)
        {
            f.IsSelected = false;
        }
        UpdateLargeFileSelectionSummary();
    }

    private void OpenRecycleBin_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        LargeFileHunterService.OpenWindowsRecycleBin();
    }

    private void RecycleSelectedLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        var selected = _largeFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        long totalBytes = selected.Sum(f => f.SizeBytes);
        var res = MessageBox.Show(
            $"Safely move {selected.Count} selected files ({TargetFolderInfo.FormatBytes(totalBytes)}) to the Windows Recycle Bin?\n\nThey can be restored anytime from the Recycle Bin with undo.",
            "Recycle Selected Large Files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            var (succ, fail, freed) = LargeFileHunterService.BatchMoveToRecycleBin(selected);
            foreach (var item in selected)
            {
                if (!File.Exists(item.FilePath))
                {
                    _largeFiles.Remove(item);
                }
            }

            UpdateDriveTelemetry(freed);
            UpdateLargeFileSelectionSummary();
            RefreshLargeFileHeroStats();
            AddLog($"Batch Recycled {succ} large files ({TargetFolderInfo.FormatBytes(freed)} freed) to Windows Recycle Bin.", LogLevel.Success);
            LargeFilesStatusText.Text = $"Successfully recycled {succ} files ({TargetFolderInfo.FormatBytes(freed)}) to Recycle Bin";
            SoundService.PlaySuccessSound();
        }
    }

    private async void RescanLargeFilesBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        await RunLargeFileScanAsync();
    }

    private void CloseLargeFilesModal_Click(object sender, RoutedEventArgs e)
    {
        _largeFileScanCts?.Cancel();
        LargeFilesModalOverlay.Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
    }

    private void RevealLargeFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LargeFileInfo info)
        {
            try
            {
                if (File.Exists(info.FilePath))
                {
                    string safePath = info.FilePath.Replace("\"", "");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{safePath}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }
    }

    private void RecycleLargeFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LargeFileInfo info)
        {
            var res = MessageBox.Show(
                $"Send '{info.FileName}' ({info.FormattedSize}) to the Windows Recycle Bin?\n\nAI Verdict: {info.AiVerdict}\n\nThis file can be restored anytime from the Recycle Bin.",
                "Recycle Large File",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = LargeFileHunterService.MoveToRecycleBin(info.FilePath);
                if (ok)
                {
                    AddLog($"Moved '{info.FileName}' ({info.FormattedSize}) to Recycle Bin.", LogLevel.Success);
                    _largeFiles.Remove(info);
                    UpdateDriveTelemetry();
                    UpdateLargeFileSelectionSummary();
                    RefreshLargeFileHeroStats();
                }
                else
                {
                    AddLog($"Could not recycle file '{info.FileName}'.", LogLevel.Warning);
                }
            }
        }
    }

    // 3. Process Optimizer Handlers
    private List<ProcessMemoryInfo> _allProcesses = new();
}
