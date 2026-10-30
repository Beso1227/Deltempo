using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
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

    /// <summary>Marshals worker-thread progress onto the UI thread.</summary>
    private readonly Action<Action> _post;

    private List<TargetFolderInfo> _allTargets = [];

    // The visible scan owns the first 70% of the progress bar, and each discovery
    // pass that follows gets its own slice. Without this the bar would sit at 100%
    // while three directory walks were still running.
    private const double DefaultScanShare = 70;
    private const double OrphanDiscoveryPercent = 80;
    private const double StoreOrphanDiscoveryPercent = 90;

    /// <summary>
    /// Replaces the scanned set and re-applies the view.
    ///
    /// The selection helpers - which decide what a "select safe" or a "select all"
    /// click would include - are pure presentation over the scanned set, and they
    /// are the part most likely to drift from the WPF app this was ported from.
    /// Exposing the seam lets them be exercised without a scan of every target
    /// category on the machine.
    /// </summary>
    internal void LoadFrom(IReadOnlyList<TargetFolderInfo> targets)
    {
        _allTargets = [.. targets];
        LocalizeTargets(_allTargets);
        TrackSelection(targets);
        ApplyTargets();
    }
    private bool _isBusy;

    /// <summary>The run that the Cancel button can stop. Null between runs.</summary>
    private CancellationTokenSource? _cts;

    // ScanFolderAsync / CleanFolderAsync are instance members on the shared
    // CleanerService (the WPF code-behind kept a _cleanerService field too), so
    // the ViewModel owns one rather than calling them statically.
    private readonly CleanerService _cleanerService = new();

    public CleanerWorkspaceViewModel(
        Func<string, string, Task<bool>>? confirmAsync = null,
        Action<Action>? post = null)
    {
        // Fail closed: no confirmation host means no destructive clean.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));

        // The cleaner reports progress from a worker thread. A shell that has a UI thread
        // passes a marshaler; a test passes nothing and the callback runs inline.
        _post = post ?? (action => action());

        ScanCommand = new AsyncRelayCommand(ScanAllAsync);
        SelectSafeCommand = new RelayCommand(SelectSafeOnly);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync);
        SmartCleanCommand = new AsyncRelayCommand(SmartCleanAsync, () => !IsBusy);
        DeepCleanCommand = new AsyncRelayCommand(DeepCleanAsync, () => !IsBusy);
        RetryTargetCommand = new AsyncRelayCommand<TargetFolderInfo>(RetryTargetAsync);
        CancelCommand = new RelayCommand(CancelRun, () => IsBusy);
        ExportAuditCommand = new RelayCommand(ExportAudit);
        RamBoostCommand = new AsyncRelayCommand(RunRamBoostAsync, () => !IsBoosting);

        // WPF's InitializeMemoryViewModel (MainWindow.Memory.cs:12) chained the
        // RAM engine's log line into the shell's activity log; the workspace's
        // LogRequested feeds the same sink here.
        _memoryBoost.LogRequested += (message, level) => LogRequested?.Invoke(message, level);

        Targets.CollectionChanged += OnTargetsCollectionChanged;

        // Drive card reads disk metadata only - free space and health heuristics.
        // WPF refreshed the same card from its load path; the WinUI panel is built in
        // InitializeComponent, so this is the equivalent moment. The RAM card rides
        // the same cadence, like WPF's UpdateMemoryTelemetry did from the load path.
        RefreshDriveTelemetry();
    }

    public event Action<string, WinTempCleaner.Models.LogLevel>? LogRequested;

    public ObservableCollection<TargetFolderInfo> Targets { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        LocalizationService.Get("ReadyStatus");

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

    // ---------------------------------------------------------------------
    // Drive telemetry card (port of WPF's UpdateDriveTelemetry)
    // ---------------------------------------------------------------------

    /// <summary>The letter the drive card reports, e.g. "C:".</summary>
    private string _driveLetter = "C:";

    /// <summary>"OS Drive (C:)" in the active language. De-composed per refresh, so a
    /// language switch re-renders it exactly as WPF's ApplyLocalization did.</summary>
    [ObservableProperty]
    public partial string DriveLabel { get; set; } = "OS Drive (C:)";

    /// <summary>"42.3% Free".</summary>
    [ObservableProperty]
    public partial string DriveFreeText { get; set; } = "-- Free";

    /// <summary>0-100, the used portion, matching WPF's DriveUsageBar.Value.</summary>
    [ObservableProperty]
    public partial double DriveUsedPercent { get; set; } = 50;

    /// <summary>"120 GB free of 475 GB".</summary>
    [ObservableProperty]
    public partial string DriveDetailsText { get; set; } = "Calculating disk space...";

    /// <summary>"SSD 99%".</summary>
    [ObservableProperty]
    public partial string DriveHealthText { get; set; } = "SSD --";

    /// <summary>Hex colour from DriveHealthInfo.HealthColor, through the shared
    /// HexToBrush converter in the view.</summary>
    [ObservableProperty]
    public partial string DriveHealthColor { get; set; } = "#10B981";

    /// <summary>Speed Test button label; becomes the measured speed while idle.</summary>
    [ObservableProperty]
    public partial string BenchmarkText { get; set; } = "Speed Test";

    [ObservableProperty]
    public partial bool IsBenchmarking { get; set; }

    // ---------------------------------------------------------------------
    // RAM telemetry card (port of WPF's RAM Telemetry & Quick Optimizer Card,
    // MainWindow.xaml:834-918, driven by MainWindow.Memory.cs's _memoryViewModel)
    // ---------------------------------------------------------------------

    /// <summary>
    /// The shared RAM engine Core ships - the very instance the WPF hero drove
    /// (MemoryOptimizationViewModel owns the boost latch, the telemetry read and
    /// the result log line), reused here instead of re-specced in this project.
    /// </summary>
    private readonly WinTempCleaner.ViewModels.MemoryOptimizationViewModel _memoryBoost = new();

    /// <summary>0-100, drives the hero RAM bar like WPF's HeroRamProgressBar.</summary>
    [ObservableProperty]
    public partial double RamUsedPercent { get; set; } = 50;

    /// <summary>"42%", WPF's HeroRamPercentText ("--%" before the first read).</summary>
    [ObservableProperty]
    public partial string RamUsedPercentText { get; set; } = "--%";

    /// <summary>"8 GB / 16 GB Used", WPF's HeroRamDetailText.</summary>
    [ObservableProperty]
    public partial string RamDetailText { get; set; } = "Reading RAM telemetry...";

    [ObservableProperty]
    public partial bool IsBoosting { get; set; }

    [ObservableProperty]
    public partial string BoostButtonText { get; set; } = "Quick Boost";

    /// <summary>
    /// Port of WPF's UpdateMemoryTelemetry (MainWindow.Memory.cs:31): re-reads live
    /// RAM telemetry into the hero card. Cheap and read-only, so eager calls - and
    /// the cleaner's existing refresh cadence, which routes here through
    /// RefreshDriveTelemetry's callers - cannot hurt.
    /// </summary>
    public void RefreshRamTelemetry()
    {
        _memoryBoost.RefreshTelemetry();
        RamUsedPercent = _memoryBoost.UsedPercent;
        RamUsedPercentText = $"{_memoryBoost.UsedPercent:F0}%";
        RamDetailText = _memoryBoost.FormattedDetail;
        IsBoosting = _memoryBoost.IsBoosting;
        BoostButtonText = _memoryBoost.BoostButtonText;
    }

    /// <summary>
    /// Port of WPF's HeroBoostRamBtn_Click (MainWindow.Memory.cs:51): Quick Boost,
    /// privileged NT working-set trim and standby eviction. The engine carries its
    /// own busy latch and click/success/error cues, exactly as WPF relied on.
    /// </summary>
    public async Task RunRamBoostAsync()
    {
        if (IsBoosting) return;

        IsBoosting = true;
        BoostButtonText = "Boosting...";
        try
        {
            await _memoryBoost.ExecuteBoostAsync();
        }
        finally
        {
            RefreshRamTelemetry();
            // The engine resets its own latch in its finally; ours is derived
            // state and must follow it, not lead it.
            IsBoosting = _memoryBoost.IsBoosting;
            BoostButtonText = _memoryBoost.BoostButtonText;
        }
    }

    // ---------------------------------------------------------------------
    // Game Boost + CTT WinUtil hero actions (port of WPF's HeroGameBoostBtn /
    // HeroWinUtilBtn handlers)
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial string GameBoostText { get; set; } = LocalizationService.Get("HeroGameBoost");

    /// <summary>#10B981 while boosting, #94A3B8 idle - WPF's exact pair. Gray at rest,
    /// unlike WPF's XAML which painted the icon green before any refresh did.</summary>
    [ObservableProperty]
    public partial string GameBoostIconColor { get; set; } = "#94A3B8";

    [ObservableProperty]
    public partial string GameBoostToolTip { get; set; } =
        "1-Click Game Boost: sets Windows to Ultimate Performance, flushes standby RAM & throttles background updaters";

    /// <summary>True while the toggle call is in flight.</summary>
    private bool _isBoostBusy;

    [ObservableProperty]
    public partial bool IsGameBoostBusy { get; set; }

    /// <summary>Bytes this shell has freed since launch, across runs.</summary>
    private long _sessionFreedBytes;

    /// <summary>The session stat card is hidden until a run actually freed something.</summary>
    [ObservableProperty]
    public partial bool HasSessionFreed { get; set; }

    /// <summary>"1.2 GB" - the value the This Session card shows.</summary>
    public string SessionFreedText => TargetFolderInfo.FormatBytes(_sessionFreedBytes);

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// True for the whole of a scan or a clean, including the confirmation dialog-free
    /// part of it. Drives the progress strip and the Cancel button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>0-100. WPF drove its dock bar from the same number.</summary>
    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    /// <summary>The percentage shown next to the bar, e.g. "63%".</summary>
    [ObservableProperty]
    public partial string ProgressText { get; set; } = "0%";

    /// <summary>Summary of the last clean, for the completion surface.</summary>
    public WinTempCleaner.Models.CleanSummary? LastSummary { get; private set; }

    /// <summary>The scanned set, in the order the list shows it. The audit report needs it.</summary>
    public IReadOnlyList<TargetFolderInfo> AllTargets => _allTargets;

    /// <summary>
    /// Whether the last run applied the Safety Shield. Recorded rather than assumed,
    /// because the audit report states it.
    /// </summary>
    public bool LastRunUsedSafeMode { get; private set; }

    /// <summary>Raised when a clean finishes, so the shell can celebrate it.</summary>
    public event Action<WinTempCleaner.Models.CleanSummary>? CleanCompleted;

    /// <summary>Category chip selection: ALL, SAFE, SYSTEM, GAMING, MEDIA, DEV or RULEPACK.</summary>
    [ObservableProperty]
    public partial string FilterTag { get; set; } = "ALL";

    /// <summary>Free-text filter over target name, category and scope path.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// The Safety Shield: when on, files written in the last 24 hours inside a
    /// shield-eligible category are neither measured nor deleted.
    ///
    /// It is off by default, which is what WPF's SafeModeCheckBox declared
    /// (IsChecked="False"). The port hardcoded false in both the scan and the clean,
    /// so the shield existed in the service and had no way to be turned on.
    /// </summary>
    [ObservableProperty]
    public partial bool SafeMode { get; set; }

    partial void OnFilterTagChanged(string value) => ApplyTargets();

    partial void OnSearchTextChanged(string value) => ApplyTargets();

    /// <summary>
    /// WPF logged the shield state on every toggle - "Safety Shield ENABLED: Files
    /// created/modified within last 24 hours will be preserved." - and did not rescan,
    /// because the shield changes what a clean may delete, not what exists on disk.
    /// </summary>
    partial void OnSafeModeChanged(bool value)
    {
        LogRequested?.Invoke(
            value
                ? "Safety Shield ENABLED: Files created/modified within last 24 hours will be preserved."
                : "Safety Shield DISABLED: All files in selected categories will be removed.",
            WinTempCleaner.Models.LogLevel.Info);
    }

    partial void OnIsBusyChanged(bool value)
    {
        // WPF disabled Scan and Clean for the whole run (SetControlsEnabled(false)); the
        // scan button's IsLoading binding and this property carry the same rule here.
        CancelCommand.NotifyCanExecuteChanged();
        SmartCleanCommand.NotifyCanExecuteChanged();
        DeepCleanCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanClean));
    }

    /// <summary>
    /// Stops the scan or clean that is running.
    ///
    /// WPF showed the same Cancel button on its dock for the whole run. Cancelling is a
    /// request: targets already started finish what they are doing, and the loop checks
    /// the token between targets, which is why the post-run accounting below only claims
    /// the targets that were actually reached.
    /// </summary>
    private void CancelRun()
    {
        if (_cts is not { IsCancellationRequested: false } cts)
        {
            return;
        }

        cts.Cancel();
        StatusText = "Cancelling...";
        LogRequested?.Invoke("[Cleaner] Cancel requested", WinTempCleaner.Models.LogLevel.Warning);
    }

    public int SelectedCount => Targets.Count(t => t.IsSelected);

    /// <summary>
    /// The Clean button's enabled state: something is selected and no run is in flight.
    /// WPF expressed the same rule by disabling its controls around a run.
    /// </summary>
    public bool CanClean => HasSelection && !IsBusy;
    public long SelectedBytes => Targets.Where(t => t.IsSelected).Sum(t => t.SizeBytes);

    public IAsyncRelayCommand ScanCommand { get; }
    public IRelayCommand SelectSafeCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand DeselectAllCommand { get; }
    public IAsyncRelayCommand CleanSelectedCommand { get; }
    public IAsyncRelayCommand SmartCleanCommand { get; }
    public IAsyncRelayCommand DeepCleanCommand { get; }
    public IAsyncRelayCommand<TargetFolderInfo> RetryTargetCommand { get; }

    /// <summary>Stops the run in flight. WPF had the same button on its dock.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>Writes the audit report from the cleaner header, as WPF did.</summary>
    public IRelayCommand ExportAuditCommand { get; }

    /// <summary>
    /// Quick Boost from the RAM telemetry card (WPF's HeroBoostRamBtn). Bound
    /// directly; the CanExecute latch below mirrors the hero button's IsEnabled.
    /// The engine's privileged targets make this the one command the self-test
    /// never executes.
    /// </summary>
    public IAsyncRelayCommand RamBoostCommand { get; }

    /// <summary>Read-only scan of all target categories. Deletes nothing.</summary>
    public async Task ScanAllAsync()
    {
        if (_isBusy) return;

        _isBusy = true;
        IsBusy = true;
        IsLoading = true;
        ProgressPercent = 0;
        ProgressText = "0%";
        StatusText = "Scanning all target categories...";

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        try
        {
            bool safeMode = SafeMode; // the shield also changes what the scan measures
            var targets = CleanerService.GetDefaultTargets();
            LocalizeTargets(targets);

            _allTargets = targets;
            TrackSelection(targets);
            ApplyTargets();

            int done = 0;
            foreach (var target in targets)
            {
                token.ThrowIfCancellationRequested();
                StatusText = $"Scanning ({done + 1}/{targets.Count}) {target.Name}...";

                await _cleanerService.ScanFolderAsync(target, Log, token, safeMode);

                done++;
                ProgressPercent = (double)done / targets.Count * DefaultScanShare;
                ProgressText = $"{(int)ProgressPercent}%";
            }

            // Three more sets exist that the default categories cannot express, and
            // each costs a walk before it can report anything: app leftovers whose
            // program is gone, MSIX package data whose app was uninstalled, and the
            // built-in plus user rulepacks. WPF loaded them after its visible scan
            // completed rather than holding the list back, and so does this.
            await AddDiscoveredTargetsAsync(
                "Looking for orphaned app leftovers...",
                OrphanDiscoveryPercent,
                () => CleanerService.LoadOrphanedTargetsAsync(token),
                token);

            await AddDiscoveredTargetsAsync(
                "Looking for orphaned Microsoft Store data...",
                StoreOrphanDiscoveryPercent,
                () => CleanerService.LoadStoreOrphanTargetsAsync(token),
                token);

            await AddDiscoveredTargetsAsync(
                "Applying cache rulepacks...",
                100,
                () => CleanerService.LoadDynamicRulepackTargetsAsync(token),
                token);

            RecalculateTotals();
            RefreshDriveTelemetry();

            int empty = targets.Count(t => t.SizeBytes == 0 && t.FileCount == 0);
            StatusText = targets.Count == 0 || empty == targets.Count
                ? "Scan completed. No reclaimable space found."
                : $"Scan completed. Ready to clean. {Targets.Count(t => t.SizeBytes > 0)} categories with reclaimable space.";
            LogRequested?.Invoke("[Cleaner] Scan finished", WinTempCleaner.Models.LogLevel.Info);

            // WPF re-checked the free-space threshold every time its dashboard refreshed.
            // The WinUI shell has no dashboard card, so the scan that produces the numbers
            // is also the moment to warn. Read-only: TrayService only raises a balloon, and
            // CheckLowDiskSpaceAndNotify self-guards on the Settings toggle and a 2h cooldown.
            TrayService.CheckLowDiskSpaceAndNotify(DriveTelemetryService.GetSystemDriveTelemetry());
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
            _cts?.Dispose();
            _cts = null;
            _isBusy = false;
            IsBusy = false;
            IsLoading = false;
        }
    }

    /// <summary>
    /// Runs one discovery pass and appends what it found.
    ///
    /// A duplicate id is skipped rather than added: a user rulepack that describes a
    /// folder the default categories already cover would otherwise become a second,
    /// independently tickable row for the same bytes, and the hero total would count
    /// them twice.
    /// </summary>
    private async Task AddDiscoveredTargetsAsync(
        string status,
        double progressPercent,
        Func<Task<List<TargetFolderInfo>>> load,
        CancellationToken token)
    {
        StatusText = status;

        List<TargetFolderInfo> discovered = await load();
        token.ThrowIfCancellationRequested();

        int added = 0;
        foreach (var target in discovered)
        {
            if (_allTargets.Any(existing => existing.Id == target.Id))
            {
                continue;
            }

            LocalizationService.LocalizeTarget(target);
            _allTargets.Add(target);
            TrackSelection([target]);
            added++;
        }

        ProgressPercent = progressPercent;
        ProgressText = $"{(int)progressPercent}%";
        ApplyTargets();

        // Always logged, including "0 new": a discovery pass that silently finds
        // nothing is indistinguishable from one that never ran.
        LogRequested?.Invoke(
            $"[Cleaner] {status.TrimEnd('.')}: {discovered.Count} found, {added} new",
            WinTempCleaner.Models.LogLevel.Info);
    }

    /// <summary>
    /// Port of WPF's UpdateDriveTelemetry: free percentage, used bar, details line and
    /// the S.M.A.R.T. health pill on the drive card. Read-only (DriveInfo plus
    /// heuristics), so it is safe anywhere including the self-test.
    /// </summary>
    /// <remarks>
    /// WPF's overload also advanced <c>_sessionTotalFreed</c> and overwrote its hero
    /// subtext with "Reclaimed X this session". Here the session figure lives on its
    /// own stat card and the freed accounting happens next to
    /// <c>_sessionFreedBytes</c>, so a rescan or a selection change cannot clobber it.
    /// </remarks>
    public void RefreshDriveTelemetry()
    {
        var telemetry = DriveTelemetryService.GetSystemDriveTelemetry();
        _driveLetter = telemetry.DriveLetter;

        string osLabel = LocalizationService.Get("DriveOsLabel");
        DriveLabel = osLabel.Contains("C:")
            ? osLabel.Replace("C:", telemetry.DriveLetter.TrimEnd('\\'))
            : $"{osLabel} ({telemetry.DriveLetter})";
        DriveFreeText = $"{telemetry.FreePercentage:F1}% {LocalizationService.Get("DriveFree")}";
        DriveDetailsText = $"{telemetry.FormattedFree} {LocalizationService.Get("DriveOf")} {telemetry.FormattedTotal}";
        DriveUsedPercent = telemetry.UsedPercentage;

        // The RAM card shares the drive card's refresh cadence, the way WPF's
        // load-path refresh touched both (UpdateDriveTelemetry + UpdateMemoryTelemetry).
        RefreshRamTelemetry();

        HasSessionFreed = _sessionFreedBytes > 0;
        OnPropertyChanged(nameof(SessionFreedText));

        // WPF swallowed snapshot failures the same way: the card falls back to its
        // last painted values rather than taking the workspace down.
        try
        {
            var health = DriveHealthService.GetDriveHealthSnapshot(telemetry.DriveLetter);
            DriveHealthText = $"SSD {health.HealthScore}%";
            DriveHealthColor = health.HealthColor;
        }
        catch
        {
        }
    }

    /// <summary>
    /// Port of WPF's DriveBenchmarkBtn_Click: non-destructive sequential read speed
    /// test. Deletes nothing; it reads and reports. The button label doubles as last
    /// result ("4,213 MB/s") or "Retry" after a failure, exactly as WPF did.
    /// </summary>
    public async Task RunDriveBenchmarkAsync()
    {
        if (IsBenchmarking) return;

        IsBenchmarking = true;
        BenchmarkText = "Testing...";
        LogRequested?.Invoke(
            "[Storage Benchmark] Running non-destructive sequential read speed test on OS drive...",
            WinTempCleaner.Models.LogLevel.Info);

        try
        {
            // WPF hardcoded "C:" here; the card it sits in always shows the system
            // drive anyway, so passing the refreshed letter keeps the two honest.
            var res = await DriveHealthService.RunQuickReadBenchmarkAsync(_driveLetter, null, CancellationToken.None);
            BenchmarkText = res.FormattedSpeed;
            LogRequested?.Invoke(
                $"[Storage Benchmark] Complete: {res.FormattedSpeed} sustained read on {res.DriveLetter} ({res.ExecutionTimeMs}ms).",
                WinTempCleaner.Models.LogLevel.Success);
            SoundService.PlaySuccessSound();
        }
        catch (Exception ex)
        {
            BenchmarkText = "Retry";
            LogRequested?.Invoke($"[Storage Benchmark Error] {ex.Message}", WinTempCleaner.Models.LogLevel.Warning);
        }
        finally
        {
            IsBenchmarking = false;
        }
    }

    /// <summary>
    /// Port of WPF's HeroGameBoostBtn_Click: engages or restores the Ultimate
    /// Performance plan, standby purge and background throttling through the shared
    /// GameBoostService.
    /// </summary>
    public async Task ToggleGameBoostAsync()
    {
        if (_isBoostBusy) return;

        _isBoostBusy = true;
        IsGameBoostBusy = true;
        SoundService.PlayClickSound();

        bool targetState = !GameBoostService.IsBoostActive;
        GameBoostText = targetState ? "Engaging..." : "Restoring...";

        try
        {
            var res = await GameBoostService.ToggleGameBoostAsync(targetState, CancellationToken.None);

            if (res.IsActive)
            {
                GameBoostText = "Boost Active";
                GameBoostIconColor = "#10B981";
                GameBoostToolTip =
                    "Game Boost Active: Ultimate Performance profile engaged, standby RAM purged, background tasks throttled. Click to disable.";
                LogRequested?.Invoke($"[Game Boost] {res.Message}", WinTempCleaner.Models.LogLevel.Success);
                SoundService.PlaySuccessSound();
            }
            else
            {
                GameBoostText = LocalizationService.Get("HeroGameBoost");
                GameBoostIconColor = "#94A3B8";
                GameBoostToolTip =
                    "1-Click Game Boost: sets Windows to Ultimate Performance, flushes standby RAM & throttles background updaters";
                LogRequested?.Invoke($"[Game Boost] {res.Message}", WinTempCleaner.Models.LogLevel.Info);
                SoundService.PlayClickSound();
            }

            RefreshDriveTelemetry();
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke(
                $"[Game Boost Error] Failed to switch state: {ex.Message}",
                WinTempCleaner.Models.LogLevel.Warning);
            GameBoostText = LocalizationService.Get("HeroGameBoost");
            GameBoostIconColor = "#94A3B8";
        }
        finally
        {
            _isBoostBusy = false;
            IsGameBoostBusy = false;
        }
    }

    /// <summary>
    /// Port of WPF's HeroWinUtilBtn_Click: launches the third-party CTT WinUtil in an
    /// elevated PowerShell session. Nothing here is Deltempo's own code - the launch is
    /// the upstream SystemRepairService call and the credit lines are logged either way.
    /// Never invoked by the self-test: it opens an external elevated process.
    /// </summary>
    public void LaunchWinUtil()
    {
        SoundService.PlayClickSound();
        LogRequested?.Invoke(
            "[CTT WinUtil] Launching Chris Titus Tech Windows Utility in an elevated PowerShell session...",
            WinTempCleaner.Models.LogLevel.Info);
        LogRequested?.Invoke(
            $"[CTT WinUtil] Third-party tool by {SystemRepairService.ChrisTitusAuthor} - {SystemRepairService.ChrisTitusWinUtilUrl}",
            WinTempCleaner.Models.LogLevel.Info);

        bool launched = SystemRepairService.LaunchChrisTitusWinUtil(out string error);
        if (launched)
        {
            LogRequested?.Invoke($"[CTT WinUtil] {SystemRepairService.ChrisTitusCreditLine}", WinTempCleaner.Models.LogLevel.Success);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            LogRequested?.Invoke($"[CTT WinUtil] {error}", WinTempCleaner.Models.LogLevel.Warning);
        }
    }

    /// <summary>
    /// Applies a completed run's freed bytes to the session card and refreshes the
    /// drive telemetry. WPF called UpdateDriveTelemetry(additionalFreedBytes) at this
    /// exact moment for the same reason.
    /// </summary>
    private void RecordSessionFreed(long bytesFreed)
    {
        if (bytesFreed <= 0) return;

        _sessionFreedBytes += bytesFreed;
        RefreshDriveTelemetry();
    }

    /// <summary>
    /// Re-labels every scanned category in the active language, then rebuilds the
    /// visible rows. Port of WPF ApplyLocalization's "Localize All 12 Target Categories"
    /// block: the rows bind to non-observed models, so the collection is rebuilt rather
    /// than relying on a property-changed notification that never comes.
    /// </summary>
    public void ApplyLocalization()
    {
        LocalizeTargets(_allTargets);
        ApplyTargets();

        // WPF replaced the dock's status line only when a run was not in flight, so a
        // language switch cannot make an in-progress scan look finished.
        if (!_isBusy)
        {
            StatusText = LocalizationService.Get("ReadyStatus");
        }

        // The drive card's composed strings carry dictionary keys, so it has to be
        // re-rendered by hand after a switch - the same UpdateDriveTelemetry() WPF's
        // ApplyLocalization ended with.
        RefreshDriveTelemetry();
    }

    private static void LocalizeTargets(IEnumerable<TargetFolderInfo> targets)
    {
        foreach (var target in targets)
        {
            LocalizationService.LocalizeTarget(target);
        }
    }

    private void ApplyTargets()
    {
        Targets.Clear();
        foreach (var t in _allTargets)
        {
            // One shared predicate: CleaningPipelineViewModel.MatchesFilter is the
            // same code the WPF cleaner filters through, so the chips cannot drift.
            if (CleaningPipelineViewModel.MatchesFilter(t, FilterTag, SearchText))
            {
                Targets.Add(t);
            }
        }
        RecalculateTotals();
    }

    /// <summary>
    /// Recomputes the selection summary whenever a row checkbox flips.
    ///
    /// The chip commands recompute themselves, but a user ticking one target directly
    /// in the list is the common case and used to leave the hero and the Clean button
    /// showing the pre-click total.
    /// </summary>
    private void TrackSelection(IEnumerable<TargetFolderInfo> targets)
    {
        foreach (var t in targets)
        {
            t.PropertyChanged -= OnTargetPropertyChanged;
            t.PropertyChanged += OnTargetPropertyChanged;
        }
    }

    private void OnTargetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TargetFolderInfo.IsSelected))
        {
            RecalculateTotals();
        }
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
        OnPropertyChanged(nameof(CanClean));
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

    /// <summary>
    /// WPF's Smart Clean: tick every 100% safe category and clean it in one press.
    ///
    /// Orphaned app leftovers are deliberately left out - they are the categories
    /// that need a human to look at them - which is exactly what the WPF
    /// SelectSafeOnlyButton_Click did before it called ExecuteCleanupAsync. The
    /// confirmation still opens, because the clean it leads to is destructive.
    /// </summary>
    public async Task SmartCleanAsync()
    {
        if (_isBusy) return;

        SelectSafeOnly();

        if (!_allTargets.Any(t => t.IsSelected && t.SizeBytes > 0))
        {
            StatusText = "Nothing safe to clean. Scan now to measure the categories first.";
            return;
        }

        await CleanSelectedAsync();
    }

    /// <summary>
    /// WPF's 1-Click Deep Clean: DeepCleanEngine sweeps every safe cache, runs DISM
    /// component cleanup, purges all but the newest restore point and flushes standby
    /// memory.
    ///
    /// Two differences from WPF, both deliberate. WPF started this from a single
    /// click with no confirmation at all, and its celebration modal reported only the
    /// disk figure; here the run asks first, and the summary carries the RAM figure
    /// too. The engine returns a partial result when cancelled rather than throwing,
    /// so the cancellation check below is what tells the two apart.
    /// </summary>
    public async Task DeepCleanAsync()
    {
        if (_isBusy) return;

        bool confirmed = await _confirmAsync(
            "1-Click Deep Clean",
            "Run the autonomous deep clean?\n\n" +
            "It purges every safe cache category, runs DISM component cleanup " +
            "(superseded Windows update payloads are removed and must be re-downloaded " +
            "if they are ever needed), purges all but the newest System Restore point, " +
            "and flushes standby memory.\n\n" +
            "Cache files deleted this way do not go to the Recycle Bin.");

        if (!confirmed) return;

        _isBusy = true;
        IsBusy = true;
        IsLoading = true;
        LastRunUsedSafeMode = false;
        ProgressPercent = 0;
        ProgressText = "0%";
        StatusText = "Running 1-Click Deep Clean...";

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        try
        {
            var progress = new Progress<DeepCleanProgress>(p => _post(() =>
            {
                // The engine reports 0-1; the bar is 0-100.
                ProgressPercent = Math.Clamp(p.OverallPercent * 100, 0, 100);
                ProgressText = $"{(int)ProgressPercent}%";
                StatusText = $"[{p.CurrentStage}] {p.DetailMessage}";
            }));

            var result = await DeepCleanEngine.ExecuteDeepCleanAsync(
                logAction: Log,
                progress: progress,
                purgeAllRestorePoints: false,
                ct: token);

            ProgressPercent = 100;
            ProgressText = "100%";

            if (token.IsCancellationRequested)
            {
                StatusText =
                    $"Deep clean cancelled. Freed {result.FormattedDiskFreed} disk and " +
                    $"{result.FormattedRamFreed} RAM before stopping.";
                LogRequested?.Invoke(
                    "[Cleaner] 1-Click Deep Clean cancelled by user",
                    WinTempCleaner.Models.LogLevel.Warning);
                return;
            }

            var summary = new WinTempCleaner.Models.CleanSummary
            {
                IsDeepClean = true,
                TotalFreedBytes = result.DiskFreedBytes,
                RamFreedBytes = result.RamFreedBytes,
                TotalFilesDeleted = result.FilesDeleted,
                TotalFoldersDeleted = result.FoldersDeleted,
                TotalFilesSkipped = result.FilesSkipped,
                ElapsedTime = result.Duration,
            };
            LastSummary = summary;
            RecordSessionFreed(result.DiskFreedBytes);

            StatusText =
                $"1-Click Deep Clean complete. Reclaimed {result.FormattedDiskFreed} disk, " +
                $"{result.FormattedRamFreed} RAM ({result.FilesDeleted:N0} files deleted).";
            LogRequested?.Invoke(
                $"[Cleaner] 1-Click Deep Clean finished: {result.FormattedDiskFreed} disk, " +
                $"{result.FormattedRamFreed} RAM ({result.FilesDeleted:N0} files, " +
                $"{result.FoldersDeleted:N0} folders) in {result.Duration.TotalSeconds:N1}s",
                WinTempCleaner.Models.LogLevel.Success);

            // The caches it just emptied are stale in the list; re-measure them.
            _ = RescanAfterCleanAsync();

            CleanCompleted?.Invoke(summary);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Deep clean cancelled.";
            LogRequested?.Invoke(
                "[Cleaner] 1-Click Deep Clean cancelled by user",
                WinTempCleaner.Models.LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusText = $"Error during deep clean: {ex.Message}";
            LogRequested?.Invoke(
                $"[Cleaner] 1-Click Deep Clean error: {ex}",
                WinTempCleaner.Models.LogLevel.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _isBusy = false;
            IsBusy = false;
            IsLoading = false;
        }
    }

    /// <summary>Destructive clean of selected categories. Gated on confirmation.</summary>
    public async Task CleanSelectedAsync()
    {
        if (_isBusy) return;

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

            // Same call the WPF cleaner made. It starts an elevated copy and exits this
            // process, which also releases the single-instance mutex the new copy needs.
            WinTempCleaner.Services.ElevationService.RestartAsAdmin();
            return;
        }

        bool safeMode = SafeMode;
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
        IsBusy = true;
        IsLoading = true;
        LastRunUsedSafeMode = safeMode;
        ProgressPercent = 0;
        ProgressText = "0%";
        StatusText = "Cleaning selected categories...";

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long totalFreed = 0;
        int totalDeleted = 0;
        int totalFoldersDeleted = 0;
        int totalFilesSkipped = 0;
        int totalFilesInUse = 0;
        int totalFilesShielded = 0;
        int totalFilesPolicy = 0;
        int totalFilesFailed = 0;

        // Targets this run actually reached, so the post-run accounting does not claim
        // success for work the loop broke out of before doing.
        var completedTargets = new HashSet<TargetFolderInfo>();

        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                // WPF broke out of the loop between targets rather than throwing, so a
                // cancelled run still reported the partial totals it had actually achieved.
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var target = selected[i];
                // Recorded as reached BEFORE the await, so a cancelled run can tell which
                // targets were attempted from the ones it never got to.
                completedTargets.Add(target);

                double targetBaseProgress = (double)i / selected.Count * 100;
                double targetSpan = 100.0 / selected.Count;

                StatusText = $"Cleaning {target.Name}...";

                // The service reports progress from a worker thread. WPF marshalled through
                // Dispatcher.BeginInvoke; here the injected marshaler does the same, and the
                // 50 ms gate keeps the dispatcher from being flooded by the file loop.
                long lastDispatchTicks = 0;
                void OnProgress(double value)
                {
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (value < 1.0 &&
                        System.Diagnostics.Stopwatch.GetElapsedTime(Interlocked.Read(ref lastDispatchTicks))
                            .TotalMilliseconds < 50)
                    {
                        return;
                    }

                    Interlocked.Exchange(ref lastDispatchTicks, now);
                    _post(() =>
                    {
                        ProgressPercent = Math.Min(100, targetBaseProgress + (value * targetSpan));
                        ProgressText = $"{(int)ProgressPercent}%";
                    });
                }

                var (freed, filesDel, foldersDel, filesSkip, filesFailed) =
                    await _cleanerService.CleanFolderAsync(target, safeMode, Log, OnProgress, token);

                totalFreed += freed;
                totalDeleted += filesDel;
                totalFoldersDeleted += foldersDel;
                totalFilesSkipped += filesSkip;

                if (target.LastTransactionResult != null)
                {
                    totalFilesInUse += target.LastTransactionResult.InUseLockedCount;
                    totalFilesShielded += target.LastTransactionResult.RecentShieldedCount;
                    totalFilesPolicy += target.LastTransactionResult.PolicyProtectedCount;
                    totalFilesFailed += target.LastTransactionResult.FailedExecutionCount;
                }
            }

            stopwatch.Stop();
            ProgressPercent = 100;
            ProgressText = "100%";

            // Zero out the targets that actually completed, so the hero reflects reality
            // right now. Targets the cancelled loop never reached keep their numbers; the
            // rescan below is what corrects them.
            bool runWasCancelled = token.IsCancellationRequested;
            foreach (var target in selected)
            {
                if (runWasCancelled && !completedTargets.Contains(target))
                {
                    continue;
                }

                target.SizeBytes = 0;
                target.FileCount = 0;
            }

            RecalculateTotals();

            // Background rescan: re-measures whatever is left and corrects the hero.
            _ = RescanAfterCleanAsync();

            var summary = new WinTempCleaner.Models.CleanSummary
            {
                TotalFreedBytes = totalFreed,
                TotalFilesDeleted = totalDeleted,
                TotalFoldersDeleted = totalFoldersDeleted,
                TotalFilesSkipped = totalFilesSkipped,
                TotalFilesInUse = totalFilesInUse,
                TotalFilesRecentShielded = totalFilesShielded,
                TotalFilesPolicyProtected = totalFilesPolicy,
                TotalFilesFailed = totalFilesFailed,
                ElapsedTime = stopwatch.Elapsed,
            };
            LastSummary = summary;
            RecordSessionFreed(totalFreed);

            // The skip breakdown is the difference between "it worked" and "it worked but
            // 400 files were held open" - WPF printed the same list on its dock.
            var skipDetails = new List<string>();
            if (totalFilesInUse > 0) skipDetails.Add($"{totalFilesInUse:N0} in use");
            if (totalFilesShielded > 0) skipDetails.Add($"{totalFilesShielded:N0} shielded <24h");
            if (totalFilesPolicy > 0) skipDetails.Add($"{totalFilesPolicy:N0} protected");
            if (totalFilesFailed > 0) skipDetails.Add($"{totalFilesFailed:N0} failed");
            string skipDetailText = skipDetails.Count > 0 ? $" ({string.Join(", ", skipDetails)})" : string.Empty;

            StatusText = runWasCancelled
                ? $"Cleanup cancelled. Freed {summary.FormattedFreedSize} before stopping."
                : $"Cleanup complete. Freed {summary.FormattedFreedSize} ({totalDeleted:N0} files deleted{skipDetailText}).";

            LogRequested?.Invoke(
                $"[Cleaner] Cleanup finished: freed {summary.FormattedFreedSize} " +
                $"({totalDeleted:N0} deleted{skipDetailText}) in {stopwatch.Elapsed.TotalSeconds:N1}s",
                WinTempCleaner.Models.LogLevel.Success);

            CleanCompleted?.Invoke(summary);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cleanup cancelled.";
            LogRequested?.Invoke("[Cleaner] Cleanup cancelled by user", WinTempCleaner.Models.LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusText = $"Error during cleanup: {ex.Message}";
            LogRequested?.Invoke($"[Cleaner] Cleanup error: {ex.Message}", WinTempCleaner.Models.LogLevel.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _isBusy = false;
            IsBusy = false;
            IsLoading = false;
        }
    }

    /// <summary>
    /// Re-measures after a clean so the list shows what is actually left.
    ///
    /// The delay gives the filesystem a moment to settle - WPF waited 600 ms before
    /// re-running its scan for the same reason.
    /// </summary>
    private async Task RescanAfterCleanAsync()
    {
        try
        {
            await Task.Delay(600);
            await ScanAllAsync();
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke(
                $"[Cleaner] Post-clean rescan failed: {ex.Message}",
                WinTempCleaner.Models.LogLevel.Warning);
        }
    }

    private async Task RetryTargetAsync(TargetFolderInfo? target)
    {
        if (target is null) return;

        target.HasError = false;
        target.ErrorMessage = string.Empty;
        target.StatusMessage = "Retrying...";
        target.IsScanning = true;

        await _cleanerService.ScanFolderAsync(target, Log, CancellationToken.None, SafeMode);
        RecalculateTotals();
    }

    /// <summary>
    /// Writes the audit report for the categories on screen.
    ///
    /// WPF's header button exported the same file - CleanerService.GenerateAuditReport
    /// under Deltempo_Audit_&lt;timestamp&gt;.txt on the Desktop - and announced the
    /// outcome in a MessageBox. The outcome lands on the status line here: the
    /// ViewModel has no dialog host, and the summary overlay's own Export already
    /// reports itself that way.
    /// </summary>
    private void ExportAudit()
    {
        try
        {
            // The last completed run's numbers when there is one, otherwise the session
            // total - which is the report WPF wrote before any clean had happened.
            var summary = LastSummary ?? new CleanSummary
            {
                TotalFreedBytes = _sessionFreedBytes,
            };

            // The shield line describes the categories the report lists. After a
            // selected-category run that is the shield the deletion actually used;
            // otherwise it is the toggle as it stands now. A deep clean never applies
            // the shield, so its recorded value would misdescribe a later report.
            bool shieldForReport = LastSummary is { IsDeepClean: false }
                ? LastRunUsedSafeMode
                : SafeMode;

            string report = CleanerService.GenerateAuditReport(_allTargets, summary, shieldForReport);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(desktop, $"Deltempo_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            File.WriteAllText(path, report);

            StatusText = $"Audit report saved to {path}";
            LogRequested?.Invoke($"[Cleaner] Audit report saved to {path}", LogLevel.Success);
        }
        catch (Exception ex)
        {
            // A read-only Desktop must not take the workspace down.
            StatusText = $"Unable to export report: {ex.Message}";
            LogRequested?.Invoke($"[Cleaner] Audit export failed: {ex.Message}", LogLevel.Warning);
        }
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
