using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

// Fully qualified on purpose. WinTempCleaner.ViewModels also declares a
// RelayCommand, which collides with the CommunityToolkit one.
using LargeFileInfo = WinTempCleaner.Services.LargeFileInfo;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>View mode for the Large Files workspace.</summary>
public enum LargeFileViewMode
{
    List,
    Treemap,
}

/// <summary>
/// WinUI 3 port of the Large File Hunter workspace from MainWindow.LargeFiles.cs
/// (~600 lines of named-control, null-checked WPF code-behind).
///
/// All of the scan/filter/select/sort/recycle logic lived as a chain of
/// null-checked assignments to named ListBoxes, ComboBoxes, TextBlocks and a
/// Treemap Canvas. It is bound data here instead.
///
/// Safety invariants carried over from WPF:
/// - The scan (LargeFileHunterService.ScanLargeFilesAsync) is read-only: it
///   enumerates files and classifies them, deleting nothing. Safe to run in the
///   self-test.
/// - MoveToRecycleBin / BatchMoveToRecycleBin do real deletion. They are gated
///   behind an injected confirmation host that defaults to refusing, so a
///   ViewModel built without one cannot recycle anything.
/// - MoveToRecycleBin revalidates safety internally (reparse-point guard,
///   CleanupExecutor.RevalidateBeforeDeletion), so the UI gate is a convenience,
///   not the guarantee.
/// - Online AI analysis (AnalyzeItemWithAiAsync / BatchAnalyzeWithAiAsync) is a
///   network call and is never invoked by the self-test.
///
/// The deterministic safety verdict (SafetyTier, IsSafe, BadgeColor, ...) is
/// computed by FileSafetyEngine during the scan and carried on each LargeFileInfo.
/// </summary>
public sealed partial class LargeFilesWorkspaceViewModel : ObservableObject
{
    /// <summary>Shown before a mutating recycle. Defaults to refusing.</summary>
    private readonly Func<string, string, Task<bool>> _confirmAsync;

    /// <summary>Reveals a file in Explorer. Supplied by the view.</summary>
    private readonly Action<string>? _openFolder;

    private List<LargeFileInfo> _allFiles = [];

    /// <summary>
    /// Replaces the scanned set and recomputes everything derived from it.
    ///
    /// The split between safe and protected storage is the number a user reads
    /// before deciding to recycle anything, and it is pure presentation over the
    /// scanned set. Exposing the seam lets it be exercised without a full disk
    /// scan.
    /// </summary>
    internal void LoadFrom(IReadOnlyList<LargeFileInfo> files)
    {
        _allFiles = [.. files];
        ApplyViewMode();
        UpdateStorageSummary();
        UpdateSelectionSummary();
    }

    /// <summary>
    /// Recomputes the storage headline and the safe/protected split from the
    /// current set. Safe bytes are counted from the per-file verdict, and the
    /// protected figure is the remainder - so the two always add up to the
    /// total, which is what makes the split readable.
    /// </summary>
    private void UpdateStorageSummary()
    {
        long totalBytes = _allFiles.Sum(f => f.SizeBytes);
        long safeBytes = _allFiles.Where(f => f.IsSafe).Sum(f => f.SizeBytes);
        long protectedBytes = Math.Max(0, totalBytes - safeBytes);

        TotalStorageText = $"{TargetFolderInfo.FormatBytes(totalBytes)} ({_allFiles.Count} files)";
        SafeStorageText = $"{TargetFolderInfo.FormatBytes(safeBytes)} ({_allFiles.Count(f => f.IsSafe)} safe)";
        ProtectedStorageText = $"{TargetFolderInfo.FormatBytes(protectedBytes)} ({_allFiles.Count(f => !f.IsSafe)} protected)";

        StatusText = _allFiles.Count == 0
            ? "No large files found. Try a different scope or lower the minimum file size."
            : $"Discovered {_allFiles.Count} files ({TargetFolderInfo.FormatBytes(totalBytes)}). " +
              $"{_allFiles.Count(f => f.IsSafe)} verified safe to clean.";
    }

    public LargeFilesWorkspaceViewModel(
        Func<string, string, Task<bool>>? confirmAsync = null,
        Action<string>? openFolder = null)
    {
        // Fail closed: no confirmation host means no recycle.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));
        _openFolder = openFolder;

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        SelectSafeCommand = new RelayCommand(SelectSafe);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        RecycleSelectedCommand = new AsyncRelayCommand(RecycleSelectedAsync);
        RecycleItemCommand = new AsyncRelayCommand<LargeFileInfo>(RecycleItemAsync);
        RevealCommand = new RelayCommand<LargeFileInfo>(Reveal);
        ToggleSelectionCommand = new RelayCommand<LargeFileInfo>(ToggleSelection);
        ViewModeCommand = new RelayCommand<LargeFileViewMode>(SetViewMode);
        AnalyzeItemCommand = new AsyncRelayCommand<LargeFileInfo>(AnalyzeItemAsync);

        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public event Action<string, LogLevel>? LogRequested;

    public ObservableCollection<LargeFileInfo> Items { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        "Ready to scan. Choose a drive and minimum size, then click Scan Now.";

    [ObservableProperty]
    public partial string TotalStorageText { get; set; } = "--";

    [ObservableProperty]
    public partial string SafeStorageText { get; set; } = "--";

    [ObservableProperty]
    public partial string ProtectedStorageText { get; set; } = "--";

    [ObservableProperty]
    public partial string SelectedSummaryText { get; set; } = "Selected: 0 files (0 B)";

    [ObservableProperty]
    public partial bool HasSelection { get; set; }

    [ObservableProperty]
    public partial bool HasItems { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial LargeFileViewMode ActiveViewMode { get; set; } = LargeFileViewMode.List;

    /// <summary>
    /// Scan scope as the chip tags express it: "ALL", "USER", or a drive root such as
    /// "C:\". The chips write it and ScanAsync reads it, so the visible scope is the
    /// scope that was actually scanned.
    /// </summary>
    public string ScanScope { get; set; } = "ALL";

    /// <summary>Minimum file size in bytes, chosen by the min-size chips.</summary>
    public long MinSizeBytes { get; set; } = 50L * 1024 * 1024;

    public int SelectedCount => Items.Count(f => f.IsSelected);
    public long SelectedBytes => Items.Where(f => f.IsSelected).Sum(f => f.SizeBytes);

    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand SelectSafeCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RecycleSelectedCommand { get; }
    public IAsyncRelayCommand<LargeFileInfo> RecycleItemCommand { get; }
    public IRelayCommand<LargeFileInfo> RevealCommand { get; }
    public IRelayCommand<LargeFileInfo> ToggleSelectionCommand { get; }
    public IRelayCommand<LargeFileViewMode> ViewModeCommand { get; }
    public IAsyncRelayCommand<LargeFileInfo> AnalyzeItemCommand { get; }

    /// <summary>Read-only file enumeration and safety classification. Deletes nothing.</summary>
    public async Task ScanAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusText = "Scanning for large files...";

        try
        {
            // Read-only discovery. No deletion, no network, no registry writes.
            var result = await LargeFileHunterService.ScanLargeFilesAsync(
                minSizeBytes: MinSizeBytes,
                targetScope: ScanScope,
                maxResults: 250);

            _allFiles = result.Files;
            ApplyViewMode();
            UpdateStorageSummary();
            UpdateSelectionSummary();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan completed with warnings: {ex.Message}";
            LogRequested?.Invoke($"[LargeFiles] Scan failed: {ex.Message}", LogLevel.Warning);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyViewMode()
    {
        Items.Clear();
        foreach (LargeFileInfo f in _allFiles)
        {
            Items.Add(f);
        }
        HasItems = Items.Count > 0;
        UpdateSelectionSummary();
    }

    private void SetViewMode(LargeFileViewMode mode)
    {
        ActiveViewMode = mode;
        SoundService.PlayClickSound();
    }

    private void SelectSafe()
    {
        SoundService.PlayClickSound();
        foreach (var f in _allFiles) f.IsSelected = f.IsSafe;
        UpdateSelectionSummary();
    }

    private void SelectAll()
    {
        SoundService.PlayClickSound();
        foreach (var f in _allFiles) f.IsSelected = true;
        UpdateSelectionSummary();
    }

    private void ClearSelection()
    {
        SoundService.PlayClickSound();
        foreach (var f in _allFiles) f.IsSelected = false;
        UpdateSelectionSummary();
    }

    private void ToggleSelection(LargeFileInfo? item)
    {
        if (item is null) return;
        SoundService.PlayClickSound();
        item.IsSelected = !item.IsSelected;
        UpdateSelectionSummary();
    }

    private async Task RecycleSelectedAsync()
    {
        var selected = _allFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        SoundService.PlayClickSound();

        long totalBytes = selected.Sum(f => f.SizeBytes);
        bool confirmed = await _confirmAsync(
            "Recycle Selected Large Files",
            $"Safely move {selected.Count} selected files ({TargetFolderInfo.FormatBytes(totalBytes)}) " +
            $"to the Windows Recycle Bin?\n\nThey can be restored anytime from the Recycle Bin with undo.");

        if (!confirmed) return;

        var (succ, fail, freed) = LargeFileHunterService.BatchMoveToRecycleBin(selected);
        foreach (var item in selected)
        {
            if (!File.Exists(item.FilePath)) _allFiles.Remove(item);
        }
        ApplyViewMode();

        LogRequested?.Invoke(
            $"Batch recycled {succ} large files ({TargetFolderInfo.FormatBytes(freed)}) to Recycle Bin.",
            LogLevel.Success);
        StatusText = $"Successfully recycled {succ} files ({TargetFolderInfo.FormatBytes(freed)}) to Recycle Bin";
        SoundService.PlaySuccessSound();
    }

    private async Task RecycleItemAsync(LargeFileInfo? item)
    {
        if (item is null) return;

        SoundService.PlayClickSound();

        bool confirmed = await _confirmAsync(
            "Recycle Large File",
            $"Send '{item.FileName}' ({item.FormattedSize}) to the Windows Recycle Bin?\n\n" +
            $"AI Verdict: {item.AiVerdict}\n\nThis file can be restored anytime from the Recycle Bin.");

        if (!confirmed) return;

        bool ok = LargeFileHunterService.MoveToRecycleBin(item.FilePath);
        if (ok)
        {
            LogRequested?.Invoke(
                $"Moved '{item.FileName}' ({item.FormattedSize}) to Recycle Bin.", LogLevel.Success);
            _allFiles.Remove(item);
            ApplyViewMode();
        }
        else
        {
            LogRequested?.Invoke($"Could not recycle file '{item.FileName}'.", LogLevel.Warning);
        }
    }

    private void Reveal(LargeFileInfo? item)
    {
        if (item is null) return;
        SoundService.PlayClickSound();

        string revealPath = item.FilePath;
        if (!File.Exists(revealPath))
        {
            revealPath = item.DirectoryPath;
        }
        if (Directory.Exists(revealPath) || File.Exists(revealPath))
        {
            _openFolder?.Invoke(revealPath);
        }
    }

    private async Task AnalyzeItemAsync(LargeFileInfo? item)
    {
        if (item is null) return;

        SoundService.PlayClickSound();
        await LargeFileHunterService.AnalyzeItemWithAiAsync(item);
    }

    private void UpdateSelectionSummary()
    {
        int count = SelectedCount;
        long bytes = SelectedBytes;
        SelectedSummaryText = $"Selected: {count} files ({TargetFolderInfo.FormatBytes(bytes)})";
        HasSelection = count > 0;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (LargeFileInfo item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (LargeFileInfo item in e.NewItems)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
        UpdateSelectionSummary();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LargeFileInfo.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    /// <summary>Single funnel for log lines raised outside this class.</summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
        => LogRequested?.Invoke(message, level);
}
