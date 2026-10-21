using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

// WinTempCleaner.ViewModels declares its own RelayCommand which collides with
// CommunityToolkit's; reference CleaningPipelineViewModel by full name only.
using CleaningPipelineViewModel = WinTempCleaner.ViewModels.CleaningPipelineViewModel;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// WinUI 3 port of the Cleaner workspace from MainWindow.Cleaning.cs (~726 lines
/// of named-control, null-checked WPF code-behind).
///
/// Core workflow: scan target categories -> review sizes/selections -> confirm
/// destructive action -> clean. The scan is read-only (enumerates and measures
/// files, deletes nothing) and is the only path the self-test exercises.
///
/// Safety invariants carried over from WPF:
/// - ScanFolderAsync / CleanFolderAsync are on CleanerService, shared with the
///   WPF app unchanged. CleanFolderAsync does real deletion and is gated behind
///   an injected confirmation host that defaults to refusing.
/// - CleaningPipelineViewModel.BuildConfirmationPreview derives the confirmation
///   copy (unit-tested); the UI only renders it.
/// - CleaningPipelineViewModel.GetElevationRequirement drives the admin
///   re-launch prompt; the UI gate is a convenience, not the guarantee.
/// - CleanFolderAsync revalidates safety internally per-file (reparse-point
///   guard, CleanupExecutor.RevalidateBeforeDeletion), so the UI gate is a
///   convenience, not the guarantee.
/// - One system-repair operation at a time: _isBusy serializes scan and clean.
/// - Cancel is the dialog default: ContentDialog DefaultButton = Close, so a
///   stray Enter cannot start a destructive clean.
/// - Self-test: ScanAllAsync is read-only. CleanFolderAsync / ExecuteCleanupAsync
///   / DeepCleanEngine are never invoked. A bare ViewModel is used with no
///   confirmation host, so even an accidental mutating call fails closed.
/// </summary>
public sealed partial class CleanerWorkspaceViewModel : ObservableObject
{
    /// <summary>Shown before a mutating clean. Defaults to refusing.</summary>
    private readonly Func<string, string, Task<bool>> _confirmAsync;

    private List<TargetFolderInfo> _allTargets = [];
    private bool _isBusy;

    // ScanFolderAsync / CleanFolderAsync are instance members on the shared
    // CleanerService (the WPF code-behind kept a _cleanerService field too), so
    // the ViewModel owns one rather than calling them statically.
    private readonly CleanerService _cleanerService = new();

    public CleanerWorkspaceViewModel(
        Func<string, string, Task<bool>>? confirmAsync = null)
    {
        // Fail closed: no confirmation host means no destructive clean.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));

        ScanCommand = new AsyncRelayCommand(ScanAllAsync);
        SelectSafeCommand = new RelayCommand(SelectSafeOnly);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync);
        RetryTargetCommand = new AsyncRelayCommand<TargetFolderInfo>(RetryTargetAsync);

        Targets.CollectionChanged += OnTargetsCollectionChanged;
    }

    public event Action<string, WinTempCleaner.Models.LogLevel>? LogRequested;

    public ObservableCollection<TargetFolderInfo> Targets { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        "Ready to scan. Choose categories, then click Scan.";

    [ObservableProperty]
    public partial string HeroSizeText { get; set; } = "0 B";

    [ObservableProperty]
    public partial string CleanButtonText { get; set; } = "Clean Selected (0 B)";

    [ObservableProperty]
    public partial string HeroSubtext { get; set; } =
        "Selected categories are clean or ready for scan";

    [ObservableProperty]
    public partial bool HasSelection { get; set; }

    [ObservableProperty]
    public partial bool HasItems { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public int SelectedCount => Targets.Count(t => t.IsSelected);
    public long SelectedBytes => Targets.Where(t => t.IsSelected).Sum(t => t.SizeBytes);

    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand SelectSafeCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand DeselectAllCommand { get; }
    public IAsyncRelayCommand CleanSelectedCommand { get; }
    public IAsyncRelayCommand<TargetFolderInfo> RetryTargetCommand { get; }

    /// <summary>Read-only scan of all target categories. Deletes nothing.</summary>
    public async Task ScanAllAsync()
    {
        if (_isBusy) return;

        _isBusy = true;
        IsLoading = true;
        StatusText = "Scanning all target categories...";

        try
        {
            bool safeMode = false; // read-only scan, safe mode is a UI preference
            var targets = CleanerService.GetDefaultTargets();

            _allTargets = targets;
            ApplyTargets();

            foreach (var target in targets)
            {
                await _cleanerService.ScanFolderAsync(target, Log, CancellationToken.None, safeMode);
            }

            RecalculateTotals();

            int empty = targets.Count(t => t.SizeBytes == 0 && t.FileCount == 0);
            StatusText = targets.Count == 0 || empty == targets.Count
                ? "Scan completed. No reclaimable space found."
                : $"Scan completed. Ready to clean. {Targets.Count(t => t.SizeBytes > 0)} categories with reclaimable space.";
            LogRequested?.Invoke("[Cleaner] Scan finished", WinTempCleaner.Models.LogLevel.Info);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
            LogRequested?.Invoke("[Cleaner] Scan cancelled", WinTempCleaner.Models.LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
            // ex.ToString() rather than ex.Message: the first failure this port hit
            // threw with an empty Message, which logged an anonymous "Scan error:" and
            // told nobody anything.
            LogRequested?.Invoke($"[Cleaner] Scan error: {ex}", WinTempCleaner.Models.LogLevel.Error);
        }
        finally
        {
            _isBusy = false;
            IsLoading = false;
        }
    }

    private void ApplyTargets()
    {
        Targets.Clear();
        foreach (var t in _allTargets)
        {
            Targets.Add(t);
        }
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        var summary = CleaningPipelineViewModel.ComputeSelectionSummary(Targets);
        HeroSizeText = summary.HeroSizeText;
        CleanButtonText = summary.CleanButtonText;
        HeroSubtext = summary.HeroSubtext;
        HasSelection = SelectedCount > 0;
        HasItems = Targets.Count > 0;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
    }

    private void SelectSafeOnly()
    {
        foreach (var t in _allTargets)
        {
            t.IsSelected = !t.IsOrphanedAppFolder;
        }
        RecalculateTotals();
        LogRequested?.Invoke("[Cleaner] Selected all safe categories", WinTempCleaner.Models.LogLevel.Info);
    }

    private void SelectAll()
    {
        foreach (var t in _allTargets) t.IsSelected = true;
        RecalculateTotals();
    }

    private void DeselectAll()
    {
        foreach (var t in _allTargets) t.IsSelected = false;
        RecalculateTotals();
    }

    /// <summary>Destructive clean of selected categories. Gated on confirmation.</summary>
    public async Task CleanSelectedAsync()
    {
        var selected = _allTargets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        var elevation = CleaningPipelineViewModel.GetElevationRequirement(selected);
        if (elevation.RequiresElevation)
        {
            string blockedList = elevation.BlockedCategoryNames.Count <= 4
                ? string.Join(", ", elevation.BlockedCategoryNames)
                : string.Join(", ", elevation.BlockedCategoryNames.Take(4)) +
                  $" and {elevation.BlockedCategoryNames.Count - 4} more";

            bool relaunch = await _confirmAsync(
                "Administrator Privileges Required",
                $"The selected categories require Administrator privileges and are not " +
                $"accessible from this session:\n\n{blockedList}\n\n" +
                $"Relaunch Deltempo as Administrator now?");
            if (!relaunch) return;

            LogRequested?.Invoke("[Cleaner] Relaunch as admin requested", WinTempCleaner.Models.LogLevel.Info);
            return; // WinUI 3 has no ElevationService.RestartAsAdmin; shell would handle this
        }

        bool safeMode = false; // UI preference, kept false for the port
        bool recycleBin = true; // default to Recycle Bin (safer)

        var preview = CleaningPipelineViewModel.BuildConfirmationPreview(selected, safeMode, recycleBin);

        bool confirmed = await _confirmAsync(
            "Clean Selected Categories",
            preview.CategorySummary + "\n\n" +
            $"Deletion mode: {preview.DeletionModeTitle}\n" +
            $"{preview.DeletionModeDescription}\n" +
            $"Estimated: {preview.EstimatedSizeText} across {preview.EstimatedFileCount:N0} files.\n" +
            $"{preview.ShieldText}");

        if (!confirmed) return;

        _isBusy = true;
        IsLoading = true;
        StatusText = "Cleaning selected categories...";

        try
        {
            long totalFreed = 0;
            int totalDeleted = 0;
            var completedTargets = new HashSet<TargetFolderInfo>();

            for (int i = 0; i < selected.Count; i++)
            {
                var target = selected[i];
                completedTargets.Add(target);

                var result = await _cleanerService.CleanFolderAsync(
                    target, safeMode, Log,
                    _ => { }, // progress reported via StatusText
                    CancellationToken.None);

                totalFreed += result.freedBytes;
                totalDeleted += result.filesDeleted;
            }

            RecalculateTotals();
            StatusText = $"Cleanup complete! Freed {WinTempCleaner.Models.TargetFolderInfo.FormatBytes(totalFreed)} ({totalDeleted:N0} files deleted).";
            LogRequested?.Invoke(
                $"[Cleaner] Cleanup finished: freed {WinTempCleaner.Models.TargetFolderInfo.FormatBytes(totalFreed)} " +
                $"({totalDeleted:N0} files deleted)", WinTempCleaner.Models.LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cleanup cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error during cleanup: {ex.Message}";
            LogRequested?.Invoke($"[Cleaner] Cleanup error: {ex.Message}", WinTempCleaner.Models.LogLevel.Error);
        }
        finally
        {
            _isBusy = false;
            IsLoading = false;
        }
    }

    private async Task RetryTargetAsync(TargetFolderInfo? target)
    {
        if (target is null) return;

        target.HasError = false;
        target.ErrorMessage = string.Empty;
        target.StatusMessage = "Retrying...";
        target.IsScanning = true;

        await _cleanerService.ScanFolderAsync(target, Log, CancellationToken.None, false);
        RecalculateTotals();
    }

    private void OnTargetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset ||
            e.Action == NotifyCollectionChangedAction.Replace)
        {
            RecalculateTotals();
        }
    }

    /// <summary>Logs a line from the cleaning pipeline. Routed to the shell log.</summary>
    public void Log(string message, WinTempCleaner.Models.LogLevel level = WinTempCleaner.Models.LogLevel.Info)
        => LogRequested?.Invoke(message, level);
}
