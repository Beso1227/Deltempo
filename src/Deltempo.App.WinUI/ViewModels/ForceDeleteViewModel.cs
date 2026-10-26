using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// Picks paths for the staging list. WinUI 3 has no OpenFileDialog, so the view
/// supplies a Storage.Picker-backed implementation and the ViewModel stays
/// testable without any UI.
/// </summary>
public interface IPathPicker
{
    Task<IReadOnlyList<string>> PickFilesAsync();

    Task<IReadOnlyList<string>> PickFoldersAsync();
}

/// <summary>
/// WinUI 3 port of the Force Delete staging surface.
///
/// This is the most safety-sensitive screen in the app. Every safety rule from
/// the WPF implementation is preserved, and three were made structural rather
/// than incidental:
///
///  1. Shielded targets (AbsoluteBlock) are staged pre-DESELECTED, and
///     EligibleTargets() re-filters on tier, so a shielded path cannot reach
///     the engine even if selection state is corrupted or set from a binding.
///  2. The destructive confirmation is INJECTED and defaults to refusing. With
///     no confirmation host supplied, Execute cannot delete anything.
///  3. The picker is injected and defaults to null. With no picker, staging is
///     impossible, so Execute has nothing to act on.
///
/// The confirmation text itself is built here, not in the view, so the exact
/// wording reviewed in the WPF app is preserved verbatim.
/// </summary>
public sealed partial class ForceDeleteViewModel : ObservableObject
{
    /// <summary>Shown before a permanent purge. Returns true only on explicit yes.</summary>
    private readonly Func<string, string, Task<bool>> _confirmAsync;

    public ForceDeleteViewModel(
        IPathPicker? picker = null,
        Func<string, string, Task<bool>>? confirmAsync = null)
    {
        _picker = picker;

        // Fail closed: an absent confirmation host means "no" forever.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));

        // Matches the WPF default: lockers are terminated unless the user opts out.
        // This is a destructive capability, so it starts in the WPF-prescribed state
        // rather than the safer-looking one.
        TerminateLockers = true;
        SendToRecycleBin = false;

        AddFilesCommand = new AsyncRelayCommand(
            () => StageAsync(_picker is null ? null : _picker.PickFilesAsync));
        AddFolderCommand = new AsyncRelayCommand(
            () => StageAsync(_picker is null ? null : _picker.PickFoldersAsync));
        ClearAllCommand = new RelayCommand(ClearAll);
        DryRunCommand = new AsyncRelayCommand(() => RunPurgeAsync(dryRun: true));
        ExecuteCommand = new AsyncRelayCommand(ExecuteAsync);
        CloseCommand = new RelayCommand(() =>
        {
            SoundService.PlayClickSound();
            CloseRequested?.Invoke();
        });

        ToggleSelectionCommand = new RelayCommand<ForceDeleteTargetRowViewModel>(row =>
        {
            if (row is null || row.Tier == ForceDeleteTier.AbsoluteBlock)
            {
                // Shielded rows can never be selected, by any path.
                return;
            }

            row.IsSelected = !row.IsSelected;
            UpdateSummary();
        });
    }

    private readonly IPathPicker? _picker;

    public event Action<string, LogLevel>? LogRequested;

    /// <summary>
    /// Single funnel for log lines raised outside this class, so external callers
    /// (the view) do not have to be able to raise the event itself.
    /// </summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
        => LogRequested?.Invoke(message, level);

    public event Action? CloseRequested;

    /// <summary>Raised with (title, message) when a fatal error needs a dialog.</summary>
    public event Action<string, string>? FatalError;

    public ObservableCollection<ForceDeleteTargetRowViewModel> Targets { get; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "No targets staged.";

    [ObservableProperty]
    public partial string ProtectedNotice { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasProtectedNotice { get; set; }

    [ObservableProperty]
    public partial bool HasTargets { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsExecuting { get; set; }

    /// <summary>Staging profile runs can be slow; buttons disable while one is in flight.</summary>
    public bool CanStage => !IsBusy && !IsExecuting;

    [ObservableProperty]
    public partial bool TerminateLockers { get; set; }

    [ObservableProperty]
    public partial bool SendToRecycleBin { get; set; }

    /// <summary>Both destructive actions require at least one eligible target.</summary>
    public bool HasEligibleTargets => EligibleTargets().Count > 0;

    public bool CanRunPurge => HasEligibleTargets && !IsExecuting;

    public IAsyncRelayCommand AddFilesCommand { get; }

    public IAsyncRelayCommand AddFolderCommand { get; }

    public IRelayCommand ClearAllCommand { get; }

    public IAsyncRelayCommand DryRunCommand { get; }

    public IAsyncRelayCommand ExecuteCommand { get; }

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand<ForceDeleteTargetRowViewModel> ToggleSelectionCommand { get; }

    /// <summary>Opens with an empty staging list, mirroring the WPF OpenEmpty().</summary>
    public void OpenEmpty()
    {
        Targets.Clear();
        UpdateSummary();
        SoundService.PlayClickSound();
    }

    /// <summary>Selected, non-shielded targets only. Shielded paths never reach the engine.</summary>
    public List<ForceDeleteTargetRowViewModel> EligibleTargets()
        => Targets
            .Where(t => t.IsSelected && t.Tier != ForceDeleteTier.AbsoluteBlock)
            .ToList();

    private async Task StageAsync(Func<Task<IReadOnlyList<string>>>? pick)
    {
        if (IsBusy || pick is null)
        {
            if (pick is null)
            {
                LogRequested?.Invoke("No file picker is available on this surface.", LogLevel.Warning);
            }

            return;
        }

        IReadOnlyList<string> paths;
        try
        {
            paths = await pick();
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Selection error: {ex.Message}", LogLevel.Warning);
            return;
        }

        await StagePathsAsync(paths);
    }

    /// <summary>
    /// Profiles and appends paths, skipping duplicates and anything already staged.
    /// Profiling walks the target tree, so it runs off the UI thread.
    /// </summary>
    public async Task StagePathsAsync(IReadOnlyList<string> paths)
    {
        List<string> candidates = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Where(p => !Targets.Any(i => string.Equals(i.Item.FilePath, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0)
        {
            UpdateSummary();
            return;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(CanRunPurge));
        OnPropertyChanged(nameof(CanStage));

        List<ForceDeleteTargetItem> staged = [];
        List<string> errors = [];

        try
        {
            (List<ForceDeleteTargetItem> Results, List<string> Failures) outcome = await Task.Run(() =>
            {
                var results = new List<ForceDeleteTargetItem>();
                var failures = new List<string>();

                foreach (string path in candidates)
                {
                    try
                    {
                        StubbornTargetProfile profile = ForceDeleteService.InspectPath(path);

                        results.Add(new ForceDeleteTargetItem
                        {
                            FilePath = profile.Path,
                            FileName = string.IsNullOrEmpty(profile.Name)
                                ? Path.GetFileName(profile.Path)
                                : profile.Name,
                            SizeBytes = profile.SizeBytes,
                            IsDirectory = profile.IsDirectory,
                            IssueSummary = profile.SummaryIssues,
                            LockingProcessName = profile.LockingProcesses.Count > 0 ? profile.LockingProcesses[0].ProcessName : string.Empty,
                            LockingProcessId = profile.LockingProcesses.Count > 0 ? profile.LockingProcesses[0].ProcessId : 0,
                            GateTier = profile.GateDecision.Tier,
                            // Shielded targets are pre-deselected so they can never be purged.
                            IsSelected = profile.GateDecision.Tier != ForceDeleteTier.AbsoluteBlock,
                        });
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"Could not inspect '{path}': {ex.Message}");
                    }
                }

                return (Results: results, Failures: failures);
            });

            staged = outcome.Results;
            errors = outcome.Failures;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanRunPurge));
            OnPropertyChanged(nameof(CanStage));
        }

        foreach (string error in errors)
        {
            LogRequested?.Invoke(error, LogLevel.Error);
        }

        foreach (ForceDeleteTargetItem item in staged)
        {
            Targets.Add(new ForceDeleteTargetRowViewModel(item, ToggleSelectionCommand));
        }

        if (staged.Count > 0)
        {
            LogRequested?.Invoke(
                $"Staged {staged.Count} target{((staged.Count == 1) ? "" : "s")} for force delete.",
                LogLevel.Info);
        }

        if (errors.Count > 0)
        {
            LogRequested?.Invoke(
                $"{errors.Count} selection{((errors.Count == 1) ? " was" : "s were")} skipped (unreadable or inaccessible).",
                LogLevel.Warning);
        }

        UpdateSummary();
    }

    private void ClearAll()
    {
        Targets.Clear();
        UpdateSummary();
        SoundService.PlayClickSound();
    }

    private void UpdateSummary()
    {
        int shielded = Targets.Count(i => i.Tier == ForceDeleteTier.AbsoluteBlock);
        int confirm = Targets.Count(i => i.Tier == ForceDeleteTier.OverrideRequired);

        SummaryText = Targets.Count == 0
            ? "No targets staged."
            : $"{Targets.Count:N0} target{(Targets.Count == 1 ? "" : "s")} staged for force delete";

        if (shielded > 0)
        {
            HasProtectedNotice = true;
            ProtectedNotice =
                $"{shielded} target{((shielded == 1) ? " is" : "s are")} strictly shielded " +
                "(System32, boot files, registry hives, drive roots, Deltempo's own binaries) " +
                "and can never be deleted.";
        }
        else if (confirm > 0)
        {
            HasProtectedNotice = true;
            ProtectedNotice =
                $"{confirm} target{((confirm == 1) ? " requires" : "s require")} explicit confirmation before deletion.";
        }
        else
        {
            HasProtectedNotice = false;
            ProtectedNotice = string.Empty;
        }

        HasTargets = Targets.Count > 0;
        OnPropertyChanged(nameof(HasEligibleTargets));
        OnPropertyChanged(nameof(CanRunPurge));
        OnPropertyChanged(nameof(CanStage));
    }

    private async Task ExecuteAsync()
    {
        List<ForceDeleteTargetRowViewModel> targets = EligibleTargets();
        if (targets.Count == 0)
        {
            return;
        }

        bool confirmed = await _confirmAsync(
            "Confirm Permanent Force Delete",
            BuildConfirmationText(targets));

        if (!confirmed)
        {
            LogRequested?.Invoke("Force delete cancelled by user.", LogLevel.Warning);
            return;
        }

        await RunPurgeAsync(dryRun: false, overrideConfirmed: true);
    }

    /// <summary>
    /// The permanent-delete confirmation body. Wording is carried over from the WPF
    /// modal unchanged: it is reviewed copy, and changing it would change what the
    /// user is agreeing to.
    /// </summary>
    internal string BuildConfirmationText(List<ForceDeleteTargetRowViewModel> targets)
    {
        int fileCount = targets.Count(t => !t.Item.IsDirectory);
        int dirCount = targets.Count - fileCount;
        long totalBytes = targets.Sum(t => t.Item.SizeBytes);

        var breakdown = new List<string>();
        if (fileCount > 0)
        {
            breakdown.Add($"{fileCount:N0} file{((fileCount == 1) ? "" : "s")}");
        }

        if (dirCount > 0)
        {
            breakdown.Add($"{dirCount:N0} folder{((dirCount == 1) ? "" : "s")}");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Permanently delete {string.Join(" and ", breakdown)} ({TargetFolderInfo.FormatBytes(totalBytes)})?");
        sb.AppendLine();

        // Show a few concrete targets so the user confirms the right items.
        foreach (ForceDeleteTargetRowViewModel item in targets.Take(8))
        {
            sb.AppendLine($"  • {item.FileName}{(item.Item.IsDirectory ? "\\" : string.Empty)}");
        }

        if (targets.Count > 8)
        {
            sb.AppendLine($"  • …and {targets.Count - 8} more");
        }

        sb.AppendLine();
        sb.AppendLine("This bypasses read-only/hidden/system attributes, takes ownership, and grants full access.");
        sb.Append("This action CANNOT be undone.");

        // Tier B targets sit in a different risk class (installed applications, user
        // documents). Say so explicitly rather than lumping them in with temp files.
        List<ForceDeleteTargetRowViewModel> protectedTargets =
            targets.Where(t => t.Tier == ForceDeleteTier.OverrideRequired).ToList();

        if (protectedTargets.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"WARNING: {protectedTargets.Count} of these {targets.Count} item(s) are PROTECTED by the safety policy:");

            foreach (ForceDeleteTargetRowViewModel item in protectedTargets.Take(5))
            {
                sb.AppendLine($"  • {item.FileName}{(item.Item.IsDirectory ? "\\" : string.Empty)}");
            }

            if (protectedTargets.Count > 5)
            {
                sb.AppendLine($"  • …and {protectedTargets.Count - 5} more");
            }

            sb.AppendLine();
            sb.Append("These may be installed applications or personal data. Review the list carefully.");
        }

        if (TerminateLockers)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("WARNING: Applications holding these files will be force-terminated, which may discard unsaved work.");
        }

        if (SendToRecycleBin)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Items will be sent to the Recycle Bin instead of being erased, so they can be restored.");
        }

        return sb.ToString();
    }

    private ForceDeleteOptions BuildOptions(bool dryRun, bool overrideConfirmed)
        => new()
        {
            StripReadOnlySystemHidden = true,
            TakeOwnershipAndResetAcl = true,
            TerminateLockingProcesses = TerminateLockers,
            ScheduleRebootIfLocked = true,
            SendToRecycleBinInstead = SendToRecycleBin,
            DeleteRetryPasses = 3,
            DryRun = dryRun,
            // Tier B (OverrideRequired) targets are only unlocked once the user
            // answers the confirmation prompt.
            ExplicitOverrideConfirmed = overrideConfirmed,
        };

    private async Task RunPurgeAsync(bool dryRun, bool overrideConfirmed = false)
    {
        List<ForceDeleteTargetRowViewModel> targets = EligibleTargets();
        if (targets.Count == 0)
        {
            return;
        }

        IsExecuting = true;
        OnPropertyChanged(nameof(CanRunPurge));
        OnPropertyChanged(nameof(CanStage));
        SetTargetsStatus("Running…", inProgress: true);

        try
        {
            ForceDeleteResult result = await ForceDeleteService.ExecuteForceDeleteAsync(
                targets.Select(t => t.Item.FilePath),
                BuildOptions(dryRun, overrideConfirmed),
                logAction: (msg, level) => LogRequested?.Invoke(msg, level));

            if (dryRun)
            {
                foreach (ForceDeleteTargetRowViewModel item in targets)
                {
                    item.SetStatus("Simulated");
                }

                LogRequested?.Invoke(
                    $"Dry run finished: {result.FilesDeleted} file(s) and {result.DirectoriesDeleted} " +
                    $"folder(s) would be removed, {result.RebootScheduledCount} would be scheduled for reboot.",
                    LogLevel.Info);
            }
            else
            {
                var succeeded = new HashSet<string>(
                    result.Attempts.Where(a => a.Stage == ForceDeleteStage.Delete && a.Success).Select(a => a.Path),
                    StringComparer.OrdinalIgnoreCase);

                // A target registered with the Session Manager WILL be removed on the
                // next reboot - that is a success, not a failure.
                var scheduledForReboot = new HashSet<string>(
                    result.Attempts.Where(a => a.Stage == ForceDeleteStage.ScheduleReboot && a.Success).Select(a => a.Path),
                    StringComparer.OrdinalIgnoreCase);

                foreach (ForceDeleteTargetRowViewModel item in targets)
                {
                    item.SetStatus(
                        succeeded.Contains(item.Item.FilePath) ? "✓ Deleted"
                        : scheduledForReboot.Contains(item.Item.FilePath) ? "Reboot Purge"
                        : "Failed");
                }

                LogRequested?.Invoke(
                    $"Force delete finished: {result.FilesDeleted} file(s), {result.DirectoriesDeleted} " +
                    $"folder(s) removed, {result.FormattedFreed} reclaimed.",
                    result.FailedCount > 0 ? LogLevel.Warning : LogLevel.Success);

                // Same rule as a cleanup: only a run that actually removed something, and
                // left nothing behind, gets the success interval.
                bool removedNothing = result.FilesDeleted == 0
                    && result.DirectoriesDeleted == 0
                    && result.RebootScheduledCount == 0;

                if (removedNothing || result.FailedCount > 0 || result.ShieldedCount > 0)
                {
                    SoundService.PlayWarningSound();
                }
                else
                {
                    SoundService.PlaySuccessSound();
                }

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
            SoundService.PlayErrorSound();
            LogRequested?.Invoke($"Force delete operation aborted: {ex.Message}", LogLevel.Error);
            FatalError?.Invoke("Force Delete Failed", $"The force delete operation could not be completed.\n\n{ex.Message}");
        }
        finally
        {
            IsExecuting = false;
            SetTargetsStatus(null, inProgress: false);
            UpdateSummary();
        }
    }

    private void SetTargetsStatus(string? status, bool inProgress)
    {
        foreach (ForceDeleteTargetRowViewModel item in Targets)
        {
            if (item.Tier == ForceDeleteTier.AbsoluteBlock)
            {
                item.SetStatus("Shielded");
            }
            else if (status is not null)
            {
                item.SetStatus(status);
            }

            item.IsActionInProgress = inProgress;
        }
    }
}

/// <summary>
/// One staged force-delete target.
///
/// Wraps <see cref="ForceDeleteTargetItem"/> so the tier is immutable from the
/// UI's point of view: a shielded row cannot be toggled even if a binding sets
/// IsSelected directly, because EligibleTargets() filters on Tier again.
/// </summary>
public sealed partial class ForceDeleteTargetRowViewModel : ObservableObject
{
    public ForceDeleteTargetRowViewModel(ForceDeleteTargetItem item, IRelayCommand<ForceDeleteTargetRowViewModel> toggleCommand)
    {
        Item = item;
        ToggleCommand = toggleCommand;
        IsSelected = item.IsSelected;
        StatusText = item.StatusText;
    }

    public ForceDeleteTargetItem Item { get; }

    public string FileName => Item.FileName;

    public string FilePath => Item.FilePath;

    public string FormattedSize => TargetFolderInfo.FormatBytes(Item.SizeBytes);

    public string IssueSummary => Item.IssueSummary;

    public string LockingProcessName => Item.LockingProcessName;

    public ForceDeleteTier Tier => Item.GateTier;

    public bool IsShielded => Tier == ForceDeleteTier.AbsoluteBlock;

    /// <summary>
    /// Inverse of <see cref="IsShielded"/>, for binding the selection checkbox's
    /// IsEnabled. Exposed as data because there is no bool-inverting control
    /// behaviour in WinUI 3 and a Visibility converter cannot drive IsEnabled.
    /// </summary>
    public bool IsSelectable => Tier != ForceDeleteTier.AbsoluteBlock;

    public bool RequiresOverride => Tier == ForceDeleteTier.OverrideRequired;

    // Gate-tier presentation is data, not a trigger.
    //
    // The WPF version used Style.Triggers/DataTrigger keyed on GateTier, which
    // WinUI 3 does not support. Resolving the badge here keeps one source of
    // truth for the tier wording and colours, and means the two UIs cannot
    // drift apart on what "Shielded" or "Confirm" look like.
    public string BadgeText => Tier switch
    {
        ForceDeleteTier.AbsoluteBlock => "Shielded",
        ForceDeleteTier.OverrideRequired => "Confirm",
        _ => "Safe",
    };

    public string BadgeBackgroundHex => Tier switch
    {
        ForceDeleteTier.AbsoluteBlock => "#FF261C0D", // WarningBadgeBgBrush
        ForceDeleteTier.OverrideRequired => "#FF261C0D",
        _ => "#FF0A0F14",                             // SurfaceSubCardBrush
    };

    public string BadgeBorderHex => Tier switch
    {
        ForceDeleteTier.AbsoluteBlock => "#FFF43F5E", // DestructiveRedBrush
        ForceDeleteTier.OverrideRequired => "#FFF59E0B",
        _ => "#FF16212B",                             // HairlineBorderBrush
    };

    public string BadgeForegroundHex => Tier switch
    {
        ForceDeleteTier.AbsoluteBlock => "#FFF43F5E",
        ForceDeleteTier.OverrideRequired => "#FFF59E0B",
        _ => "#FF10B981",                             // EmeraldGreenBrush
    };

    /// <summary>Shielded rows are locked off regardless of what a binding writes here.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial bool IsActionInProgress { get; set; }

    public IRelayCommand<ForceDeleteTargetRowViewModel> ToggleCommand { get; }

    internal void SetStatus(string status)
    {
        Item.StatusText = status;
        StatusText = status;
    }
}
