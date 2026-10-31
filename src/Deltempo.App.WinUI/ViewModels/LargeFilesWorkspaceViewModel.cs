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

    /// <summary>The scan the Cancel button can stop. Null between scans.</summary>
    private CancellationTokenSource? _cts;

    /// <summary>Latch for the online batch analysis; the command's CanExecute reads it.</summary>
    private bool _isBatchAnalyzing;

    /// <summary>Marshals scan progress onto the UI thread.</summary>
    private readonly Action<Action> _post;

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
        Action<string>? openFolder = null,
        Action<Action>? post = null)
    {
        // Fail closed: no confirmation host means no recycle.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));
        _openFolder = openFolder;

        // The service reports progress from a worker thread. The view supplies
        // DispatcherQueue.TryEnqueue; tests pass nothing and it runs inline.
        _post = post ?? (action => action());

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsLoading);
        SelectSafeCommand = new RelayCommand(SelectSafe);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        RecycleSelectedCommand = new AsyncRelayCommand(RecycleSelectedAsync);
        RecycleItemCommand = new AsyncRelayCommand<LargeFileInfo>(RecycleItemAsync);
        RevealCommand = new RelayCommand<LargeFileInfo>(Reveal);
        ToggleSelectionCommand = new RelayCommand<LargeFileInfo>(ToggleSelection);
        ViewModeCommand = new RelayCommand<LargeFileViewMode>(SetViewMode);
        AnalyzeItemCommand = new AsyncRelayCommand<LargeFileInfo>(AnalyzeItemAsync);
        BatchAnalyzeCommand = new AsyncRelayCommand(BatchAnalyzeAsync, () => !_isBatchAnalyzing && !IsLoading);
        OpenRecycleBinCommand = new RelayCommand(OpenRecycleBin);

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

    /// <summary>
    /// Category filter tag in the shape of WPF's LargeFileCategoryComboBox
    /// (MainWindow.xaml:4568): "ALL", "SAFE" (IsAiSafe only), "RISK" (unsafe only), or
    /// an exact category name such as "Installer / ISO", "Game Asset / Pak",
    /// "Video / Media" or "Archive" compared OrdinalIgnoreCase like WPF's filter.
    /// </summary>
    public string CategoryFilter { get; set; } = "ALL";

    /// <summary>
    /// Sort tag in the shape of WPF's LargeFileSortComboBox: "SIZE_DESC" (largest
    /// first), "SIZE_ASC", "SAFETY" (SafetyScore desc, then size desc) or "NAME"
    /// (file name asc). WPF's CollectionViewSource sort order, applied here by
    /// rebuilding the visible Items list.
    /// </summary>
    public string SortMode { get; set; } = "SIZE_DESC";

    /// <summary>
    /// True while the online batch analysis fans out. The batch is the same
    /// online-AI path WPF's AskAiSelectedLargeFilesBtn drove; it never runs inside
    /// the self-test.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBatchAnalyzing { get; set; }

    /// <summary>
    /// Empty-state copy. WPF rewrote the two TextBlocks in place for the scanning,
    /// cancelled and failed cases; the same three states are data here so the
    /// panel never claims "No large files found" while it is still looking.
    /// </summary>
    [ObservableProperty]
    public partial string EmptyTitle { get; set; } = "No large files found";

    [ObservableProperty]
    public partial string EmptyHint { get; set; } =
        "Scan your drives to see large files. Safe-to-clean files are marked with a green badge.";

    /// <summary>
    /// Shows the empty panel while a scan runs even if a previous result set is
    /// still in Items, which is what WPF did - and hides it again once the scan
    /// produces rows.
    /// </summary>
    public bool ShowEmptyState => IsLoading || !HasItems;

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

    /// <summary>
    /// WPF's primary action read "Recycle Selected (N B)" and updated with the
    /// selection, so the size was visible before the confirmation opened.
    /// </summary>
    public string RecycleSelectedText =>
        $"Recycle Selected ({TargetFolderInfo.FormatBytes(SelectedBytes)})";

    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelScanCommand { get; }
    public IRelayCommand SelectSafeCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RecycleSelectedCommand { get; }
    public IAsyncRelayCommand<LargeFileInfo> RecycleItemCommand { get; }
    public IRelayCommand<LargeFileInfo> RevealCommand { get; }
    public IRelayCommand<LargeFileInfo> ToggleSelectionCommand { get; }
    public IRelayCommand<LargeFileViewMode> ViewModeCommand { get; }
    public IAsyncRelayCommand<LargeFileInfo> AnalyzeItemCommand { get; }
    public IAsyncRelayCommand BatchAnalyzeCommand { get; }
    public IRelayCommand OpenRecycleBinCommand { get; }

    /// <summary>Read-only file enumeration and safety classification. Deletes nothing.</summary>
    public async Task ScanAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        string readableScope = ScanScope == "ALL" ? "all drives"
            : ScanScope == "USER" ? "user profile"
            : ScanScope;

        StatusText = $"Scanning {readableScope} for files > {TargetFolderInfo.FormatBytes(MinSizeBytes)}...";
        EmptyTitle = $"Scanning {readableScope}...";
        EmptyHint = "Analyzing drives for large files. This may take a moment.";

        try
        {
            // Read-only discovery. No deletion, no network, no registry writes.
            // The percentage mirrors WPF's status line, which overwrote itself with
            // "Scanning {scope}... ({pct}%)" on every service callback.
            var progress = new Progress<int>(pct => _post(() =>
                StatusText = $"Scanning {readableScope}... ({pct}%)"));

            var result = await LargeFileHunterService.ScanLargeFilesAsync(
                minSizeBytes: MinSizeBytes,
                targetScope: ScanScope,
                maxResults: 250,
                progress: progress,
                ct: token);

            _allFiles = result.Files;
            ApplyViewMode();
            UpdateStorageSummary();
            UpdateSelectionSummary();

            // The service cancels cooperatively: it stops enumerating and returns
            // whatever it had. It does not throw, so the partial result is the only
            // signal that the user stopped it - WPF read neither and reported a
            // cancelled scan as a complete one.
            if (!result.ScanCompleted)
            {
                StatusText = _allFiles.Count == 0
                    ? "Scan cancelled."
                    : $"Scan cancelled. Showing the {_allFiles.Count} files found so far.";

                if (_allFiles.Count == 0)
                {
                    EmptyTitle = "Scan cancelled";
                    EmptyHint = "Click Scan Now to run a new scan.";
                }

                LogRequested?.Invoke("[LargeFiles] Scan cancelled", LogLevel.Warning);
                return;
            }

            // Rows present means the panel is hidden (ShowEmptyState), so this copy is
            // reset either way rather than left claiming "Scanning..." behind a list.
            EmptyTitle = "No large files found";
            EmptyHint = "Try a different scope or lower the minimum file size.";

            // The Settings auto-query promise fires only after a clean scan: the
            // gate, the empty-file guard and cancellation are AutoQueryAfterScanAsync's
            // own business. A blocked or partial scan skips it on ScanCompleted above.
            await AutoQueryAfterScanAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
            EmptyTitle = "Scan cancelled";
            EmptyHint = "Click Scan Now to run a new scan.";
            LogRequested?.Invoke("[LargeFiles] Scan cancelled", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusText = $"Scan completed with warnings: {ex.Message}";
            EmptyTitle = "Scan encountered an issue";
            EmptyHint = ex.Message;
            // ex.ToString(): a scan failure has thrown with an empty Message before.
            LogRequested?.Invoke($"[LargeFiles] Scan failed: {ex}", LogLevel.Warning);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsLoading = false;
        }
    }

    /// <summary>
    /// Stops the running scan at the service's next yield point and leaves the
    /// rows it already produced in place. WPF had this path wired to a CTS that
    /// its disabled Scan button could never reach.
    /// </summary>
    private void CancelScan() => _cts?.Cancel();

    partial void OnIsLoadingChanged(bool value)
    {
        CancelScanCommand.NotifyCanExecuteChanged();
        BatchAnalyzeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnIsBatchAnalyzingChanged(bool value)
    {
        BatchAnalyzeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BatchAnalyzeText));
    }

    /// <summary>WPF's AskAiSelectedLargeFilesBtn label behaviour in one property.</summary>
    public string BatchAnalyzeText => IsBatchAnalyzing ? "Analyzing..." : "Analyze with AI";


    private void ApplyViewMode()
    {
        Items.Clear();
        foreach (LargeFileInfo f in ApplyListFilters(_allFiles))
        {
            Items.Add(f);
        }
        HasItems = Items.Count > 0;
        OnPropertyChanged(nameof(ShowEmptyState));
        UpdateSelectionSummary();
    }

    /// <summary>
    /// WPF's FilterLargeFileItem + LargeFileSort_Changed in one rebuild: the category
    /// filter and the sort mode act on the scanned result without touching the scan
    /// itself. Exposed so the view can re-apply either choice without a re-scan.
    /// </summary>
    public void ApplyFilters()
    {
        if (IsLoading) return;

        ApplyViewMode();
        SoundService.PlayClickSound();
    }

    /// <summary>Filter then sort, exactly the WPF order semantics.</summary>
    private List<LargeFileInfo> ApplyListFilters(List<LargeFileInfo> files)
        => FilterAndSort(files, CategoryFilter, SortMode);

    /// <summary>
    /// The filter + sort core, pure and internal so the parity suite can prove the
    /// WPF semantics (FilterLargeFileItem + the four sort orders) on real data.
    /// </summary>
    internal static List<LargeFileInfo> FilterAndSort(
        List<LargeFileInfo> files,
        string categoryFilter,
        string sortMode)
    {
        IEnumerable<LargeFileInfo> filtered = files;

        if (categoryFilter != "ALL")
        {
            if (categoryFilter == "SAFE")
            {
                filtered = filtered.Where(f => f.IsAiSafe);
            }
            else if (categoryFilter == "RISK")
            {
                filtered = filtered.Where(f => !f.IsAiSafe);
            }
            else
            {
                // WPF compared the category text OrdinalIgnoreCase.
                filtered = filtered.Where(f =>
                    string.Equals(f.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
            }
        }

        List<LargeFileInfo> list = filtered.ToList();

        switch (sortMode)
        {
            case "SIZE_ASC":
                list.Sort((a, b) => a.SizeBytes.CompareTo(b.SizeBytes));
                break;
            case "SAFETY":
                list.Sort((a, b) =>
                {
                    int byScore = b.SafetyScore.CompareTo(a.SafetyScore);
                    return byScore != 0 ? byScore : b.SizeBytes.CompareTo(a.SizeBytes);
                });
                break;
            case "NAME":
                list.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
                break;
            case "SIZE_DESC":
            default:
                list.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                break;
        }

        return list;
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

    /// <summary>
    /// Opens Explorer on the Windows Recycle Bin, where every recycle this
    /// workspace performs can be undone.
    /// </summary>
    private void OpenRecycleBin()
    {
        SoundService.PlayClickSound();
        LargeFileHunterService.OpenWindowsRecycleBin();
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

        // RefreshLargeFileHeroStats + AddLog, the WPF order: the hero numbers change
        // when the verdict reclassifies safety.
        UpdateStorageSummary();
        LogRequested?.Invoke(
            $"[LargeFiles] AI analyzed '{item.FileName}': {item.VerdictShort}",
            LogLevel.Info);
    }

    /// <summary>
    /// WPF's AskAiSelectedLargeFiles_Click (MainWindow.Settings.cs:339): analyze the
    /// ticked files; with no explicit selection fall back to the files that were
    /// never verified online, and if everything was verified, re-analyze all of
    /// them. Target resolution lives in LargeFileHunterService.SelectAnalyzeTargets
    /// so the post-scan auto-query obeys the same rule.
    /// </summary>
    public async Task BatchAnalyzeAsync()
    {
        if (_isBatchAnalyzing) return;

        List<LargeFileInfo> selected = LargeFileHunterService.SelectAnalyzeTargets(_allFiles);
        if (selected.Count == 0)
        {
            StatusText = "No files need AI analysis. Select files or run a scan first.";
            return;
        }

        SoundService.PlayClickSound();
        await RunBatchAnalyzeCoreAsync(selected);
    }

    /// <summary>
    /// The latch + progress + summary body both the manual batch and the post-scan
    /// auto-query share. The caller decides target resolution and whether a click
    /// sound plays; this runs the online fan-out, flips BatchAnalyzeCommand's gate
    /// (the manual button can't double-drive while this is live) and reports.
    /// </summary>
    private async Task RunBatchAnalyzeCoreAsync(IReadOnlyList<LargeFileInfo> targets)
    {
        _isBatchAnalyzing = true;
        IsBatchAnalyzing = true;

        try
        {
            LogRequested?.Invoke(
                $"[LargeFiles] Starting online AI analysis for {targets.Count} large files...",
                LogLevel.Info);

            // WPF wrote the percentage into the selection summary text; the status
            // line is the same kind of feedback without stealing the selection.
            var progress = new Progress<int>(pct => _post(() =>
                StatusText = $"AI analyzing {targets.Count} files: {pct}%"));

            // WPF's BatchAnalyzeWithAiAsync runs the fan-out under
            // ConfigureAwait(false), so ApplyOnlineReport raises PropertyChanged
            // from thread-pool threads. WPF marshals that silently; WinUI 3
            // classic {Binding} does not, so the row updates would land off the
            // UI thread. The fan-out therefore lives here for the WinUI shell:
            // same target rule, same throttle depth, same progress reporting,
            // but every row mutation is marshaled through _post first. Core's
            // batch method remains untouched for the WPF shell.
            using var throttler = new SemaphoreSlim(4);
            int completed = 0;

            var analyses = targets.Select(async item =>
            {
                await throttler.WaitAsync().ConfigureAwait(true);
                try
                {
                    if (string.IsNullOrWhiteSpace(item.FilePath)) return;
                    _post(() => item.IsAiAnalyzing = true);
                    var report = await OnlineFileIntelligenceService
                        .AnalyzeFileAsync(item.FilePath)
                        .ConfigureAwait(true);
                    _post(() => item.ApplyOnlineReport(report));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[Deltempo AI] Item analysis error: {ex.Message}");
                    _post(() => item.IsAiAnalyzing = false);
                }
                finally
                {
                    throttler.Release();
                    int done = Interlocked.Increment(ref completed);
                    ((IProgress<int>)progress).Report(done * 100 / Math.Max(1, targets.Count));
                }
            });

            await Task.WhenAll(analyses).ConfigureAwait(true);

            LogRequested?.Invoke(
                $"[LargeFiles] Completed AI analysis for {targets.Count} files.",
                LogLevel.Info);
            StatusText = $"AI analysis completed for {targets.Count} files.";
            UpdateStorageSummary();
            UpdateSelectionSummary();
        }
        finally
        {
            _isBatchAnalyzing = false;
            IsBatchAnalyzing = false;
        }
    }

    /// <summary>
    /// The Settings promise "ask the provider about unknown large files as soon as
    /// a scan finishes" runs through here. The gate is a seam rather than a direct
    /// SettingsService read so a test can observe the wiring without mutating the
    /// process-global settings store; production uses the AppSettings pair, which
    /// is what WPF's identical handler reads.
    /// </summary>
    internal Func<bool>? AutoQueryGate { get; set; }

    /// <summary>
    /// The auto-query's analysis step, overridable the same way: tests record the
    /// targets instead of touching the provider surface.
    /// </summary>
    internal Func<IReadOnlyList<LargeFileInfo>, Task>? AutoQueryRunner { get; set; }

    /// <summary>
    /// Production gate: the user must have enabled BOTH online AI and the
    /// auto-query switch. Auto-querying an opt-out surface would be the checkbox
    /// lying again - the exact defect this feature is being written to fix.
    /// </summary>
    internal static bool DefaultAutoQueryGate() =>
        SettingsService.Current.AutoQueryAiForLargeFiles && SettingsService.Current.EnableOnlineAiSafety;

    /// <summary>
    /// Runs the shared batch over WPF's target rule after a clean scan, silently
    /// when nothing needs analysis. Not a user click: no click sound, and a
    /// cancelled or partial scan never reaches here (ScanAsync gates on
    /// ScanCompleted before calling).
    /// </summary>
    internal async Task AutoQueryAfterScanAsync()
    {
        if (!(AutoQueryGate ?? DefaultAutoQueryGate)()) return;
        if (_isBatchAnalyzing) return;

        List<LargeFileInfo> targets = LargeFileHunterService.SelectAnalyzeTargets(_allFiles);
        if (targets.Count == 0) return;

        Func<IReadOnlyList<LargeFileInfo>, Task> runner = AutoQueryRunner ?? RunBatchAnalyzeCoreAsync;
        _isBatchAnalyzing = true;
        IsBatchAnalyzing = true;
        try
        {
            // The automatic path is not a click: statuses and logs flow from the
            // shared core, but no sound plays.
            await runner(targets);
        }
        finally
        {
            _isBatchAnalyzing = false;
            IsBatchAnalyzing = false;
        }
    }

    private void UpdateSelectionSummary()
    {
        int count = SelectedCount;
        long bytes = SelectedBytes;
        SelectedSummaryText = $"Selected: {count} files ({TargetFolderInfo.FormatBytes(bytes)})";
        HasSelection = count > 0;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(RecycleSelectedText));
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
