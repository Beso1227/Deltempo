using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>The filter chips above the application list.</summary>
public enum AppListFilter
{
    All,
    Browsers,
    Gaming,
    DevTools,
    Runtimes,
    Large,
    Broken,
    Store,
}

/// <summary>Grouping tabs inside the root-remnants drawer.</summary>
public enum LeftoverDrawerFilter
{
    All,
    Files,
    Registry,
    Shortcuts,
}

/// <summary>
/// WinUI 3 port of the App Uninstaller / Deep Root Clean surface.
///
/// Safety properties carried over from the WPF modal:
///
///  1. Nothing is purged without an explicit confirmation. The confirmation
///     host is INJECTED and defaults to refusing, so a ViewModel constructed
///     without one cannot uninstall or purge anything.
///  2. Deep Uninstall is refused when no uninstaller string exists; the user is
///     offered Force Root Clean (scan + purge) instead, which is the only path
///     taken in that case, and only after its own confirmation.
///  3. "Select Recommended" excludes Low-confidence traces. Low confidence
///     means the scanner inferred the match, so it is never pre-ticked.
///  4. The System Restore Point checkpoint is opt-in and defaults OFF, matching
///     the WPF checkbox. Creating a restore point is a privileged system
///     mutation, so it must not happen by surprise.
///
/// Filtering is pure data here (an enum plus a query), replacing the WPF
/// version's string tags and sender-identity comparisons.
public sealed partial class AppUninstallViewModel : ObservableObject
{
    /// <summary>Shown before any destructive action. Returns true only on explicit yes.</summary>
    private readonly Func<string, string, Task<bool>> _confirmAsync;

    /// <summary>Opens a path in the shell (File Explorer). Null means no-op.</summary>
    private readonly Action<string>? _openFolder;

    public AppUninstallViewModel(
        Func<string, string, Task<bool>>? confirmAsync = null,
        Action<string>? openFolder = null)
    {
        // Fail closed: an absent confirmation host means "no" forever.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));
        _openFolder = openFolder;

        RefreshCommand = new AsyncRelayCommand(LoadAppsAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        SetFilterCommand = new RelayCommand<AppListFilter>(SetFilter);
        OpenInstallFolderCommand = new RelayCommand<InstalledAppItem?>(OpenInstallFolder);
        ToggleExpandCommand = new RelayCommand<InstalledAppItem?>(ToggleExpand);
        FetchAiCommand = new AsyncRelayCommand<InstalledAppItem>(app => AnalyzeAsync(app, forceOnline: true));
        FetchAllAiCommand = new AsyncRelayCommand(FetchAllAsync);
        ScanTracesCommand = new AsyncRelayCommand<InstalledAppItem>(ScanTracesAsync);
        ForceRootCleanCommand = new AsyncRelayCommand<InstalledAppItem>(ForceRootCleanAsync);
        DeepUninstallCommand = new AsyncRelayCommand<InstalledAppItem>(DeepUninstallAsync);
        SelectAllTracesCommand = new RelayCommand(SelectAllTraces);
        SelectRecommendedTracesCommand = new RelayCommand(SelectRecommendedTraces);
        SetLeftoverFilterCommand = new RelayCommand<LeftoverDrawerFilter>(SetLeftoverFilter);
        PurgeTracesCommand = new AsyncRelayCommand(PurgeTracesAsync);
        CloseDrawerCommand = new RelayCommand(CloseDrawer);
        CloseCommand = new RelayCommand(() =>
        {
            SoundService.PlayClickSound();
            CloseRequested?.Invoke();
        });

        // WPF defaults both checkboxes to unchecked. A restore point is a
        // privileged system mutation, so it must stay opt-in.
        CreateRestorePoint = false;

        SetFilter(AppListFilter.All);
        SetLeftoverFilter(LeftoverDrawerFilter.All);
    }

    public event Action<string, LogLevel>? LogRequested;

    public event Action? CloseRequested;

    /// <summary>Raised with (title, message) when a fatal error needs a dialog.</summary>
    public event Action<string, string>? FatalError;

    /// <summary>Every application found by the registry/manifest audit.</summary>
    private List<InstalledAppItem> _allApps = [];

    /// <summary>
    /// Replaces the scanned set and re-applies the active search and filter.
    ///
    /// The search and category filtering is pure presentation over the scanned
    /// set, and it is the part most likely to drift from the WPF app this was
    /// ported from. Exposing the seam lets that logic be exercised without a
    /// registry scan of the whole machine.
    /// </summary>
    internal void LoadFrom(IReadOnlyList<InstalledAppItem> apps)
    {
        _allApps = [.. apps];
        ApplyFilter();
    }

    /// <summary>Scan result for the app whose traces are in the drawer.</summary>
    private RootScanResult? _scanResult;

    private InstalledAppItem? _inspectedApp;

    /// <summary>The filtered, displayed list. Bound directly by the view.</summary>
    public ObservableCollection<InstalledAppItem> Apps { get; } = [];

    /// <summary>Leftover traces for the current drawer filter. Not observable per-item.</summary>
    public ObservableCollection<LeftoverItem> Leftovers { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        "Ready to inspect and safely uninstall applications from their roots.";

    [ObservableProperty]
    public partial string AppCountText { get; set; } = "Scanning...";

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial AppListFilter ActiveFilter { get; set; } = AppListFilter.All;

    [ObservableProperty]
    public partial LeftoverDrawerFilter ActiveLeftoverFilter { get; set; } = LeftoverDrawerFilter.All;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsDrawerOpen { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool CreateRestorePoint { get; set; }

    [ObservableProperty]
    public partial string DrawerTitle { get; set; } = "Root Remnants";

    [ObservableProperty]
    public partial string DrawerReclaimableText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DrawerSelectionSummary { get; set; } = string.Empty;

    /// <summary>App currently open in the drawer, for the AI card.</summary>
    public InstalledAppItem? InspectedApp
    {
        get => _inspectedApp;
        private set
        {
            _inspectedApp = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasInspectedApp));
        }
    }

    public bool HasInspectedApp => InspectedApp is not null;

    public bool CanPurgeTraces => Leftovers.Any(l => l.IsSelected) && !IsBusy;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ClearSearchCommand { get; }

    public IRelayCommand<AppListFilter> SetFilterCommand { get; }

    public IRelayCommand<InstalledAppItem?> OpenInstallFolderCommand { get; }

    public IRelayCommand<InstalledAppItem?> ToggleExpandCommand { get; }

    public IAsyncRelayCommand<InstalledAppItem> FetchAiCommand { get; }

    public IAsyncRelayCommand FetchAllAiCommand { get; }

    public IAsyncRelayCommand<InstalledAppItem> ScanTracesCommand { get; }

    public IAsyncRelayCommand<InstalledAppItem> ForceRootCleanCommand { get; }

    public IAsyncRelayCommand<InstalledAppItem> DeepUninstallCommand { get; }

    public IRelayCommand SelectAllTracesCommand { get; }

    public IRelayCommand SelectRecommendedTracesCommand { get; }

    public IRelayCommand<LeftoverDrawerFilter> SetLeftoverFilterCommand { get; }

    public IAsyncRelayCommand PurgeTracesCommand { get; }

    public IRelayCommand CloseDrawerCommand { get; }

    public IRelayCommand CloseCommand { get; }

    /// <summary>Scans the registry and installation manifests, then applies the active filter.</summary>
    public async Task LoadAppsAsync()
    {
        IsLoading = true;
        StatusText = "Auditing Windows registry and installation manifests...";
        AppCountText = "Scanning...";

        try
        {
            _allApps = [.. await Task.Run(InstalledAppService.GetInstalledApps)];
            ApplyFilter();
            StatusText = $"Loaded {_allApps.Count} applications. Ready for deep root inspection.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error auditing applications: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Error: {ex.Message}", LogLevel.Warning);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Re-runs the search + chip filter and republishes the list.
    ///
    /// Reassigning the whole collection (rather than filtering in place) keeps
    /// this a single code path for every input change, and is what the WPF
    /// version did when it reset ItemsSource.
    /// </summary>
    private void ApplyFilter()
    {
        IEnumerable<InstalledAppItem> queryable = _allApps;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string query = SearchText.Trim();
            queryable = queryable.Where(a =>
                a.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.UninstallEngine.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(a.Category) &&
                 a.Category.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(a.Description) &&
                 a.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        queryable = ActiveFilter switch
        {
            AppListFilter.Browsers => queryable.Where(a =>
                string.Equals(a.Category, "Browsers", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Category, "Browser", StringComparison.OrdinalIgnoreCase)),
            AppListFilter.Gaming => queryable.Where(a =>
                string.Equals(a.Category, "Gaming", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Category, "Games", StringComparison.OrdinalIgnoreCase)),
            AppListFilter.DevTools => queryable.Where(a =>
                string.Equals(a.Category, "Development", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Category, "Dev Tools", StringComparison.OrdinalIgnoreCase)),
            AppListFilter.Runtimes => queryable.Where(a =>
                a.IsCoreRuntime ||
                string.Equals(a.Category, "Runtime", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Category, "Runtimes", StringComparison.OrdinalIgnoreCase)),
            AppListFilter.Large => queryable.Where(a => a.EstimatedSizeBytes >= 500L * 1024 * 1024),
            AppListFilter.Broken => queryable.Where(a => a.IsBroken),
            AppListFilter.Store => queryable.Where(a =>
                a.UninstallEngine.Contains("MSIX", StringComparison.OrdinalIgnoreCase) ||
                a.UninstallEngine.Contains("Store", StringComparison.OrdinalIgnoreCase)),
            _ => queryable,
        };

        List<InstalledAppItem> filtered = [.. queryable];

        Apps.Clear();
        foreach (InstalledAppItem app in filtered)
        {
            Apps.Add(app);
        }

        AppCountText = $"{filtered.Count} Apps (Total: {_allApps.Count})";
    }

    private void SetFilter(AppListFilter filter)
    {
        ActiveFilter = filter;
        SoundService.PlayClickSound();
        ApplyFilter();
    }

    private void OpenInstallFolder(InstalledAppItem? app)
    {
        if (app is null || _openFolder is null)
        {
            return;
        }

        SoundService.PlayClickSound();

        // Fall back to the parent folder: installers often record a path that no
        // longer exists, but the containing directory usually does.
        string? target = null;
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            target = app.InstallLocation;
        }
        else if (!string.IsNullOrWhiteSpace(app.InstallLocation))
        {
            string? parent = Path.GetDirectoryName(app.InstallLocation);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                target = parent;
            }
        }

        if (target is not null)
        {
            _openFolder(target);
        }
    }

    private void ToggleExpand(InstalledAppItem? app)
    {
        if (app is null)
        {
            return;
        }

        SoundService.PlayClickSound();

        // IsExpanded raises PropertyChanged, so the detail card's Visibility
        // binding updates without the list being republished.
        app.IsExpanded = !app.IsExpanded;
    }

    private async Task AnalyzeAsync(InstalledAppItem? app, bool forceOnline)
    {
        if (app is null || IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            StatusText = forceOnline
                ? $"Consulting AI engine for {app.DisplayName}..."
                : $"Analyzing {app.DisplayName}...";

            AppAiReport? report = await AppIntelligenceService.AnalyzeAppAsync(app, forceOnline);

            if (report is not null)
            {
                // Writing through the model keeps the list, the drawer card, and
                // the WPF app all reading from one source of truth.
                app.Description = report.Description;
                app.Category = report.Category;
                app.SafetyAdvice = report.SafetyAdvice;
                app.SafetyVerdict = report.SafetyVerdict;
                app.IsCoreRuntime = report.IsCoreRuntime;
                app.IsAiEnriched = report.IsAiGenerated;
                app.AiProviderUsed = report.ProviderUsed;
            }

            ApplyFilter();
            StatusText = $"AI intelligence updated for {app.DisplayName}.";
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"[App Uninstaller] AI analysis failed: {ex.Message}", LogLevel.Warning);
            StatusText = $"AI analysis failed for {app.DisplayName}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FetchAllAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SoundService.PlayClickSound();

        List<InstalledAppItem> pending = [.. _allApps.Where(a =>
            !a.IsAiEnriched &&
            (a.Category == "General" ||
             a.Category == "Unknown" ||
             a.Description.Contains("Windows application installed on this system.")))];

        if (pending.Count == 0)
        {
            StatusText = "All applications have already been classified or enriched with intelligence!";
            return;
        }

        int processed = 0;

        // Sequential on purpose: this hits a network service, and the WPF
        // version was sequential to avoid tripping provider rate limits.
        foreach (InstalledAppItem app in pending)
        {
            if (IsBusy)
            {
                return;
            }

            StatusText = $"AI analyzing [{++processed}/{pending.Count}]: {app.DisplayName}...";
            await AnalyzeAsync(app, forceOnline: false);
        }

        ApplyFilter();
        StatusText = $"Batch AI analysis completed for {pending.Count} applications.";
    }

    private async Task ScanTracesAsync(InstalledAppItem? app)
    {
        if (app is null)
        {
            return;
        }

        await InspectAsync(app, snapshot: null);
    }

    private async Task ForceRootCleanAsync(InstalledAppItem? app)
    {
        if (app is null)
        {
            return;
        }

        SoundService.PlayClickSound();

        bool confirmed = await _confirmAsync(
            "Force Root Clean",
            "Force Root Clean will scan and directly purge all file, registry, and shortcut " +
            $"remnants of:\n\n{app.DisplayName}\n\nUse this when the official uninstaller is " +
            "broken or missing. Proceed?");

        if (!confirmed)
        {
            return;
        }

        await InspectAsync(app, snapshot: null);
    }

    /// <summary>
    /// Deep Uninstall: restore point (opt-in), snapshot, terminate processes,
    /// run the official uninstaller, then rescan for leftovers.
    ///
    /// Every step that mutates the system is gated behind the confirmation
    /// below, including the uninstaller launch itself.
    /// </summary>
    private async Task DeepUninstallAsync(InstalledAppItem? app)
    {
        if (app is null || IsBusy)
        {
            return;
        }

        SoundService.PlayClickSound();

        bool hasUninstaller =
            !string.IsNullOrWhiteSpace(app.UninstallString) ||
            !string.IsNullOrWhiteSpace(app.QuietUninstallString);

        // No uninstaller available: offer Force Root Clean instead of failing.
        // This mirrors the WPF branch exactly, including its separate prompt.
        if (!hasUninstaller)
        {
            bool forceConfirm = await _confirmAsync(
                "Uninstaller Not Found",
                $"No official uninstaller was detected for {app.DisplayName}.\n\n" +
                "Would you like to perform a Force Root Clean instead?");

            if (forceConfirm)
            {
                await InspectAsync(app, snapshot: null);
            }

            return;
        }

        bool wantsRestorePoint = CreateRestorePoint;
        string restorePointText = wantsRestorePoint
            ? "â€¢ System Restore Point: Enabled (Checkpoint will be created)\n"
            : "â€¢ System Restore Point: Disabled (Optional checkpoint skipped)\n";

        bool confirmed = await _confirmAsync(
            "Deep Root Uninstall",
            "Deep Uninstall will:\n" +
            $"1. Terminate running processes for {app.DisplayName}\n" +
            $"2. Run the official uninstaller ({app.UninstallEngine})\n" +
            "3. Perform a Deep Root Scan to eradicate leftover traces\n\n" +
            restorePointText + "\n" +
            "Proceed with uninstallation?");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;

        try
        {
            if (wantsRestorePoint)
            {
                StatusText = "Creating System Restore Point checkpoint...";
                LogRequested?.Invoke(
                    $"[App Uninstaller] Creating System Restore Point checkpoint for {app.DisplayName}...",
                    LogLevel.Info);

                var restoreResult = await SystemRestorePointService
                    .CreateRestorePointAsync($"Pre-Uninstall {app.DisplayName}");

                LogRequested?.Invoke(
                    restoreResult.Success
                        ? $"[App Uninstaller] System Restore Point created successfully (Sequence: {restoreResult.SequenceNumber})."
                        : $"[App Uninstaller] System Restore Point skipped or unavailable: {restoreResult.Message}",
                    restoreResult.Success ? LogLevel.Success : LogLevel.Warning);
            }

            StatusText = $"Capturing pre-uninstall trace snapshot for {app.DisplayName}...";
            AppTraceSnapshot snapshot = await RootLeftoverScannerService.CreateSnapshotAsync(app);

            StatusText = $"Terminating background processes and launching uninstaller for {app.DisplayName}...";
            LogRequested?.Invoke(
                $"[App Uninstaller] Initiating deep uninstall for {app.DisplayName} ({app.UninstallEngine})...",
                LogLevel.Info);

            bool success = await InstalledAppService.UninstallAppAsync(app, silent: false);

            if (success)
            {
                StatusText = "Official uninstaller finished. Scanning roots for leftover traces...";
                LogRequested?.Invoke(
                    "[App Uninstaller] Uninstaller process exited. Scanning root remnants...",
                    LogLevel.Info);

                IsBusy = false;
                await InspectAsync(app, snapshot);
                return;
            }

            StatusText = "Uninstaller could not be completed or timed out.";
            LogRequested?.Invoke(
                "[App Uninstaller] Uninstallation failed or was cancelled.",
                LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusText = $"Uninstallation failed: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Deep uninstall aborted: {ex.Message}", LogLevel.Error);
            FatalError?.Invoke("Uninstall Failed", $"The uninstall of {app.DisplayName} could not be completed.\n\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Scans for leftover traces and opens the drawer on the results.</summary>
    private async Task InspectAsync(InstalledAppItem app, AppTraceSnapshot? snapshot)
    {
        IsDrawerOpen = true;
        IsBusy = true;

        try
        {
            InspectedApp = app;
            DrawerTitle = $"Root Remnants: {app.DisplayName}";
            DrawerReclaimableText = "Scanning...";
            StatusText = $"Scanning registry, user profile, and system folders for {app.DisplayName}...";

            RootScanResult result = await RootLeftoverScannerService.ScanAppTracesAsync(app, snapshot);
            _scanResult = result;

            DrawerReclaimableText = $"{result.FormattedTotalSize} ({result.Items.Count} Traces)";
            StatusText = $"Found {result.Items.Count} trace(s) ({result.FormattedTotalSize}) for {app.DisplayName}.";

            ApplyLeftoverFilter();
        }
        catch (Exception ex)
        {
            DrawerReclaimableText = "Scan failed";
            StatusText = $"Trace scan failed for {app.DisplayName}: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Trace scan failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanPurgeTraces));
        }
    }

    private void ApplyLeftoverFilter()
    {
        Leftovers.Clear();

        if (_scanResult is not null)
        {
            IEnumerable<LeftoverItem> queryable = _scanResult.Items;

            queryable = ActiveLeftoverFilter switch
            {
                LeftoverDrawerFilter.Files => queryable.Where(i =>
                    i.Type == LeftoverType.File || i.Type == LeftoverType.Directory),
                LeftoverDrawerFilter.Registry => queryable.Where(i =>
                    i.Type == LeftoverType.RegistryKey ||
                    i.Type == LeftoverType.RegistryValue ||
                    i.Type == LeftoverType.EnvironmentPath),
                LeftoverDrawerFilter.Shortcuts => queryable.Where(i =>
                    i.Type == LeftoverType.Shortcut ||
                    i.Type == LeftoverType.Service ||
                    i.Type == LeftoverType.ScheduledTask),
                _ => queryable,
            };

            foreach (LeftoverItem item in queryable)
            {
                Leftovers.Add(item);
            }
        }

        UpdateSelectionSummary();
    }

    private void SetLeftoverFilter(LeftoverDrawerFilter filter)
    {
        ActiveLeftoverFilter = filter;
        ApplyLeftoverFilter();
    }

    private void SelectAllTraces()
    {
        SoundService.PlayClickSound();

        if (_scanResult is not null)
        {
            foreach (LeftoverItem item in _scanResult.Items)
            {
                item.IsSelected = true;
            }
        }

        ApplyLeftoverFilter();
    }

    /// <summary>
    /// Ticks everything except Low-confidence traces.
    ///
    /// Low confidence means the scanner inferred the match rather than proving
    /// it, so it is never pre-selected. Preserved exactly from the WPF modal.
    /// </summary>
    private void SelectRecommendedTraces()
    {
        SoundService.PlayClickSound();

        if (_scanResult is not null)
        {
            foreach (LeftoverItem item in _scanResult.Items)
            {
                item.IsSelected = item.Confidence != LeftoverConfidence.Low;
            }
        }

        ApplyLeftoverFilter();
    }

    private void UpdateSelectionSummary()
    {
        if (_scanResult is null)
        {
            DrawerSelectionSummary = string.Empty;
            OnPropertyChanged(nameof(CanPurgeTraces));
            return;
        }

        int selectedCount = _scanResult.Items.Count(i => i.IsSelected);
        long selectedSize = _scanResult.Items.Where(i => i.IsSelected).Sum(i => i.SizeBytes);

        DrawerSelectionSummary =
            $"{selectedCount} of {_scanResult.Items.Count} trace(s) selected " +
            $"({TargetFolderInfo.FormatBytes(selectedSize)}).";

        OnPropertyChanged(nameof(CanPurgeTraces));
    }

    /// <summary>Called when a trace checkbox is toggled, to refresh the summary.</summary>
    public void NotifyTraceSelectionChanged() => UpdateSelectionSummary();

    private void CloseDrawer()
    {
        SoundService.PlayClickSound();
        IsDrawerOpen = false;
    }

    /// <summary>
    /// Erases the selected traces. Gated on a confirmation that names the app
    /// and the count, because this deletes registry keys and PATH entries in
    /// addition to files.
    /// </summary>
    private async Task PurgeTracesAsync()
    {
        if (_scanResult is null || !CanPurgeTraces)
        {
            return;
        }

        List<LeftoverItem> selected = [.. _scanResult.Items.Where(i => i.IsSelected)];

        if (selected.Count == 0)
        {
            return;
        }

        bool confirmed = await _confirmAsync(
            "Eradicate Root Traces",
            $"Permanently erase {selected.Count} selected leftover trace(s) for " +
            $"{InspectedApp?.DisplayName}?\n\nThis removes files, folders, registry keys, " +
            "registry values, services, scheduled tasks, and environment PATH entries.\n\n" +
            "This action CANNOT be undone.");

        if (!confirmed)
        {
            LogRequested?.Invoke("[Deep Root Purge] Cancelled by user.", LogLevel.Warning);
            return;
        }

        SoundService.PlayClickSound();
        IsBusy = true;

        try
        {
            StatusText = $"Eradicating selected root traces for {InspectedApp?.DisplayName}...";

            PurgeResult purgeResult = await RootLeftoverPurgeService.PurgeLeftoversAsync(selected);

            SoundService.PlaySuccessSound();
            StatusText =
                $"Purged {purgeResult.ItemsPurgedCount} trace(s) ({purgeResult.FormattedReclaimed} reclaimed) " +
                $"in {purgeResult.ExecutionTimeMs}ms.";

            LogRequested?.Invoke(
                $"[Deep Root Purge] Eradicated {purgeResult.ItemsPurgedCount} traces " +
                $"({purgeResult.FormattedReclaimed}) for {InspectedApp?.DisplayName}.",
                LogLevel.Success);

            if (purgeResult.RebootScheduledCount > 0)
            {
                LogRequested?.Invoke(
                    $"[Deep Root Purge] {purgeResult.RebootScheduledCount} locked item(s) registered " +
                    "for automatic deletion on the next Windows reboot.",
                    LogLevel.Warning);
            }

            foreach (string error in purgeResult.ErrorMessages)
            {
                LogRequested?.Invoke($"[Deep Root Purge] {error}", LogLevel.Warning);
            }

            IsDrawerOpen = false;
            _scanResult = null;
            Leftovers.Clear();

            await LoadAppsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Root purge failed: {ex.Message}";
            LogRequested?.Invoke($"[Deep Root Purge] Purge aborted: {ex.Message}", LogLevel.Error);
            FatalError?.Invoke("Root Purge Failed", $"The root purge could not be completed.\n\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanPurgeTraces));
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>Single funnel for log lines raised outside this class.</summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
        => LogRequested?.Invoke(message, level);

    /// <summary>Opens a path in the shell. Supplied by the view.</summary>
    public void OpenInShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log($"Could not open '{path}': {ex.Message}", LogLevel.Warning);
        }
    }
}
