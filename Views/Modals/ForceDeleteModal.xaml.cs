using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

public partial class ForceDeleteModal : UserControl
{
    private readonly ObservableCollection<ForceDeleteTargetItem> _items = new();
    private bool _isExecuting;

    public event Action<string, LogLevel>? LogRequested;
    public event Action? Closed;

    public ForceDeleteModal()
    {
        InitializeComponent();
        ForceDeleteItemsControl.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => RefreshFooterState();
    }

    /// <summary>
    /// Opens the modal with an empty staging list. The user picks files/folders to purge.
    /// </summary>
    public void OpenEmpty()
    {
        _items.Clear();
        UpdateSummary();
        Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
    }

    /// <summary>
    /// Profiles each supplied path through the safety gate and stages it for review.
    /// </summary>
    public void PopulateAndOpen(IEnumerable<string> paths)
    {
        _items.Clear();
        AddPaths(paths);
        UpdateSummary();
        Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select files to force delete",
                Multiselect = true,
                CheckFileExists = false,
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
            {
                AddPaths(dialog.FileNames);
                SoundService.PlayClickSound();
            }
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"File selection error: {ex.Message}", LogLevel.Warning);
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select folder to force delete",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                AddPaths(new[] { dialog.FolderName });
                SoundService.PlayClickSound();
            }
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Folder selection error: {ex.Message}", LogLevel.Warning);
        }
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        UpdateSummary();
        SoundService.PlayClickSound();
    }

    /// <summary>
    /// Profiles and appends paths, skipping duplicates and anything already staged.
    /// </summary>
    private void AddPaths(IEnumerable<string> paths)
    {
        int added = 0;
        int skipped = 0;

        foreach (string path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            // Skip paths already staged (case-insensitive, so the same file picked twice is a no-op).
            if (_items.Any(i => string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            try
            {
                StubbornTargetProfile profile = ForceDeleteService.InspectPath(path);

                _items.Add(new ForceDeleteTargetItem
                {
                    FilePath = profile.Path,
                    FileName = string.IsNullOrEmpty(profile.Name)
                        ? System.IO.Path.GetFileName(profile.Path)
                        : profile.Name,
                    SizeBytes = profile.SizeBytes,
                    IsDirectory = profile.IsDirectory,
                    IssueSummary = profile.SummaryIssues,
                    LockingProcessName = profile.LockingProcesses.Count > 0 ? profile.LockingProcesses[0].ProcessName : string.Empty,
                    LockingProcessId = profile.LockingProcesses.Count > 0 ? profile.LockingProcesses[0].ProcessId : 0,
                    GateTier = profile.GateDecision.Tier,
                    // Shielded targets are pre-deselected so they can never be purged.
                    IsSelected = profile.GateDecision.Tier != ForceDeleteTier.AbsoluteBlock
                });
                added++;
            }
            catch (Exception ex)
            {
                skipped++;
                LogRequested?.Invoke($"Could not inspect '{path}': {ex.Message}", LogLevel.Error);
            }
        }

        if (added > 0)
        {
            LogRequested?.Invoke($"Staged {added} target{((added == 1) ? "" : "s")} for force delete.", LogLevel.Info);
        }

        if (skipped > 0)
        {
            LogRequested?.Invoke($"{skipped} selection{((skipped == 1) ? " was" : "s were")} skipped (already staged or unreadable).", LogLevel.Warning);
        }

        PickerHintText.Visibility = _items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateSummary();
    }

    public void CloseModal()
    {
        Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        Closed?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseModal();

    private void UpdateSummary()
    {
        int shielded = _items.Count(i => i.GateTier == ForceDeleteTier.AbsoluteBlock);
        int confirm = _items.Count(i => i.GateTier == ForceDeleteTier.OverrideRequired);

        TargetSummaryText.Text = $"{_items.Count:N0} target{(_items.Count == 1 ? "" : "s")} staged for force delete";

        if (shielded > 0)
        {
            ProtectedNoticePanel.Visibility = Visibility.Visible;
            ProtectedNoticeText.Text =
                $"{shielded} target{((shielded == 1) ? " is" : "s are")} strictly shielded (System32, boot files, registry hives, drive roots, Deltempo's own binaries) and can never be deleted.";
        }
        else if (confirm > 0)
        {
            ProtectedNoticePanel.Visibility = Visibility.Visible;
            ProtectedNoticeText.Text =
                $"{confirm} target{((confirm == 1) ? " requires" : "s require")} explicit confirmation before deletion.";
        }
        else
        {
            ProtectedNoticePanel.Visibility = Visibility.Collapsed;
        }

        RefreshFooterState();
    }

    private void RefreshFooterState()
    {
        if (_items.Count == 0)
        {
            TargetSummaryText.Text = "No targets staged.";
        }

        int eligible = EligibleTargets().Count;
        ExecuteBtn.IsEnabled = !_isExecuting && eligible > 0;
        DryRunBtn.IsEnabled = !_isExecuting && eligible > 0;
    }

    /// <summary>
    /// Selected, non-shielded targets only. Shielded paths never reach the engine.
    /// </summary>
    private List<ForceDeleteTargetItem> EligibleTargets()
        => _items.Where(i => i.IsSelected && i.GateTier != ForceDeleteTier.AbsoluteBlock).ToList();

    private ForceDeleteOptions BuildOptions(bool dryRun, bool overrideConfirmed)
        => new()
        {
            StripReadOnlySystemHidden = true,
            TakeOwnershipAndResetAcl = true,
            TerminateLockingProcesses = TerminateLockersToggle.IsChecked == true,
            ScheduleRebootIfLocked = true,
            SendToRecycleBinInstead = RecycleToggle.IsChecked == true,
            DeleteRetryPasses = 3,
            DryRun = dryRun,
            // Tier B (OverrideRequired) targets are only unlocked once the user answers the confirmation prompt.
            ExplicitOverrideConfirmed = overrideConfirmed
        };

    private async void DryRun_Click(object sender, RoutedEventArgs e)
        => await RunPurgeAsync(dryRun: true);

    private async void Execute_Click(object sender, RoutedEventArgs e)
    {
        var targets = EligibleTargets();
        if (targets.Count == 0)
        {
            return;
        }

        if (!ConfirmDestructivePurge(targets))
        {
            LogRequested?.Invoke("Force delete cancelled by user.", LogLevel.Warning);
            return;
        }

        await RunPurgeAsync(dryRun: false, overrideConfirmed: true);
    }

    /// <summary>
    /// Blocking confirmation shown before a permanent purge. Returns false if the user backs out.
    /// </summary>
    private bool ConfirmDestructivePurge(List<ForceDeleteTargetItem> targets)
    {
        int fileCount = targets.Count(t => !t.IsDirectory);
        int dirCount = targets.Count - fileCount;
        long totalBytes = targets.Sum(t => t.SizeBytes);
        string totalSize = TargetFolderInfo.FormatBytes(totalBytes);

        var breakdown = new List<string>();
        if (fileCount > 0) breakdown.Add($"{fileCount:N0} file{((fileCount == 1) ? "" : "s")}");
        if (dirCount > 0) breakdown.Add($"{dirCount:N0} folder{((dirCount == 1) ? "" : "s")}");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Permanently delete {string.Join(" and ", breakdown)} ({totalSize})?");
        sb.AppendLine();

        // Show a few concrete targets so the user confirms the right items.
        foreach (var item in targets.Take(8))
        {
            sb.AppendLine($"  • {item.FileName}{(item.IsDirectory ? "\\" : string.Empty)}");
        }
        if (targets.Count > 8)
        {
            sb.AppendLine($"  • …and {targets.Count - 8} more");
        }

        sb.AppendLine();
        sb.AppendLine("This bypasses read-only/hidden/system attributes, takes ownership, and grants full access.");
        sb.Append("This action CANNOT be undone.");

        if (TerminateLockersToggle.IsChecked == true)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("WARNING: Applications holding these files will be force-terminated, which may discard unsaved work.");
        }

        if (RecycleToggle.IsChecked == true)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Items will be sent to the Recycle Bin instead of being erased, so they can be restored.");
        }

        MessageBoxResult result = MessageBox.Show(
            sb.ToString(),
            "Confirm Permanent Force Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private async Task RunPurgeAsync(bool dryRun, bool overrideConfirmed = false)
    {
        var targets = EligibleTargets();
        if (targets.Count == 0)
        {
            return;
        }

        _isExecuting = true;
        RefreshFooterState();
        SetTargetsStatus("Running…", inProgress: true);

        try
        {
            ForceDeleteResult result = await ForceDeleteService.ExecuteForceDeleteAsync(
                targets.Select(t => t.FilePath),
                BuildOptions(dryRun, overrideConfirmed),
                logAction: (msg, level) => LogRequested?.Invoke(msg, level));

            if (dryRun)
            {
                foreach (var item in targets)
                {
                    item.StatusText = "Simulated";
                }

                LogRequested?.Invoke(
                    $"Dry run finished: {result.FilesDeleted} file(s) and {result.DirectoriesDeleted} folder(s) would be removed, {result.RebootScheduledCount} would be scheduled for reboot.",
                    LogLevel.Info);
            }
            else
            {
                var succeeded = new HashSet<string>(
                    result.Attempts.Where(a => a.Stage == ForceDeleteStage.Delete && a.Success).Select(a => a.Path),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in targets)
                {
                    item.StatusText = succeeded.Contains(item.FilePath) ? "✓ Deleted" : "Failed";
                }

                LogRequested?.Invoke(
                    $"Force delete finished: {result.FilesDeleted} file(s), {result.DirectoriesDeleted} folder(s) removed, {result.FormattedFreed} reclaimed.",
                    result.FailedCount > 0 ? LogLevel.Warning : LogLevel.Success);

                if (result.RebootScheduledCount > 0)
                {
                    LogRequested?.Invoke(
                        $"{result.RebootScheduledCount} locked item(s) registered for automatic deletion on the next Windows reboot.",
                        LogLevel.Warning);
                }

                if (result.ShieldedCount > 0)
                {
                    LogRequested?.Invoke(
                        $"{result.ShieldedCount} protected system path(s) were shielded and left untouched.",
                        LogLevel.Info);
                }
            }

            UpdateSummary();
        }
        catch (Exception ex)
        {
            SetTargetsStatus("Failed", inProgress: false);
            LogRequested?.Invoke($"Force delete operation aborted: {ex.Message}", LogLevel.Error);
            MessageBox.Show(
                $"The force delete operation could not be completed.\n\n{ex.Message}",
                "Force Delete Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isExecuting = false;
            SetTargetsStatus(null, inProgress: false);
            RefreshFooterState();
        }
    }

    private void SetTargetsStatus(string? status, bool inProgress)
    {
        foreach (var item in _items)
        {
            if (item.GateTier == ForceDeleteTier.AbsoluteBlock)
            {
                item.StatusText = "Shielded";
            }
            else if (status != null)
            {
                item.StatusText = status;
            }

            item.IsActionInProgress = inProgress;
        }
    }
}
