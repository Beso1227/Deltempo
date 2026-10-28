using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

// See MemoryWorkspaceViewModel for why this is an alias rather than a
// `using WinTempCleaner.ViewModels;` - that namespace declares its own
// RelayCommand, which collides with the CommunityToolkit one.
using StartupItem = WinTempCleaner.Services.StartupItem;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>Filter chips above the startup list.</summary>
public enum StartupFilter
{
    All,
    HighImpact,
    Disabled,
    Orphaned,
}

/// <summary>
/// WinUI 3 port of the Startup Applications workspace.
///
/// The load, filter, and hero-stat logic lived in MainWindow.SystemManagers.cs
/// (~150 lines) as a chain of null-checked assignments to named TextBlocks. It is
/// bound data here instead.
///
/// The safety property that matters here is PROTECTED ITEMS. A protected startup
/// entry (a driver, security agent, or other system-critical launch) cannot be
/// toggled or removed: <c>CanToggle</c> is false when <c>IsProtected</c> is true,
/// and the model enforces the same rule independently inside
/// StartupManagerService. The UI gate is a convenience, not the guarantee - the
/// service is where the rule actually lives, and that is unchanged.
///
/// ONE INTENTIONAL DEVIATION: Optimize Boot asks for confirmation. The WPF modal
/// bulk-disabled high-impact startup entries with no prompt. That writes to the
/// registry across many entries at once and there is no undo button, so
/// cancelling first costs one click and prevents a change the user cannot easily
/// see or reverse. Nothing that previously succeeded is now impossible; a bulk
/// mutation simply asks first.
public sealed partial class StartupWorkspaceViewModel : ObservableObject
{
    /// <summary>Shown before a mutating action. Defaults to refusing.</summary>
    private readonly Func<string, string, Task<bool>> _confirmAsync;

    private readonly Action<string>? _openFolder;

    private List<StartupItem> _allItems = [];

    /// <summary>
    /// Replaces the scanned set and re-applies the active filter.
    ///
    /// The filter and the hero counters are pure presentation over the scanned
    /// set, and they are the part most likely to drift from the WPF app this was
    /// ported from. Exposing the seam lets them be exercised without a registry
    /// and Task Scheduler scan of the whole machine.
    /// </summary>
    internal void LoadFrom(IReadOnlyList<StartupItem> items)
    {
        _allItems = [.. items];
        ApplyFilter();
    }

    public StartupWorkspaceViewModel(
        Func<string, string, Task<bool>>? confirmAsync = null,
        Action<string>? openFolder = null)
    {
        // Fail closed: no confirmation host means no bulk mutation.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));
        _openFolder = openFolder;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SetFilterCommand = new RelayCommand<StartupFilter>(SetFilter);
        OptimizeBootCommand = new AsyncRelayCommand(OptimizeBootAsync);
        CleanOrphanedCommand = new AsyncRelayCommand<StartupItem>(CleanOrphanedAsync);
        RevealCommand = new RelayCommand<StartupItem>(Reveal);
        ToggleCommand = new AsyncRelayCommand<StartupItem>(ToggleAsync);
        ToggleExpandCommand = new RelayCommand<StartupItem>(ToggleExpand);
        AnalyzeItemCommand = new AsyncRelayCommand<StartupItem>(AnalyzeItemAsync);
        SearchOnlineCommand = new RelayCommand<StartupItem>(SearchOnline);
        BatchAnalyzeCommand = new AsyncRelayCommand(BatchAnalyzeAsync);
    }

    public event Action<string, LogLevel>? LogRequested;

    public ObservableCollection<StartupItem> Items { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } =
        "Scanning startup entries, Windows registry hives & Task Scheduler...";

    [ObservableProperty]
    public partial string TotalText { get; set; } = "-- apps";

    [ObservableProperty]
    public partial string HighImpactText { get; set; } = "-- high impact";

    [ObservableProperty]
    public partial string BootDelayText { get; set; } = "-- delay";

    [ObservableProperty]
    public partial string OrphanedText { get; set; } = "-- cleanable";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial StartupFilter ActiveFilter { get; set; } = StartupFilter.All;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand<StartupFilter> SetFilterCommand { get; }

    public IAsyncRelayCommand OptimizeBootCommand { get; }

    public IAsyncRelayCommand<StartupItem> CleanOrphanedCommand { get; }

    public IRelayCommand<StartupItem> RevealCommand { get; }

    public IAsyncRelayCommand<StartupItem> ToggleCommand { get; }

    public IRelayCommand<StartupItem> ToggleExpandCommand { get; }

    public IAsyncRelayCommand<StartupItem> AnalyzeItemCommand { get; }

    /// <summary>WPF's StartupSearchOnline_Click (MainWindow.SystemManagers.cs:159).</summary>
    public IRelayCommand<StartupItem> SearchOnlineCommand { get; }

    public IAsyncRelayCommand BatchAnalyzeCommand { get; }

    /// <summary>WPF's StartupSearchBox: filters by any of the row's identifying text.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Enumerates startup entries across the registry hives and Task Scheduler.
    /// Read-only: this only discovers what exists.
    /// </summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Scanning startup entries, Windows registry hives & Task Scheduler...";

        try
        {
            _allItems = await StartupManagerService.GetStartupItemsAsync();

            ApplyFilter();
            UpdateHeroStats();

            int enabled = _allItems.Count(x => x.IsEnabled);
            int disabled = _allItems.Count - enabled;

            StatusText = $"Found {_allItems.Count} startup programs ({enabled} enabled, {disabled} disabled)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error scanning startup entries: {ex.Message}";
            LogRequested?.Invoke($"[Startup] Scan failed: {ex.Message}", LogLevel.Warning);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetFilter(StartupFilter filter)
    {
        ActiveFilter = filter;
        SoundService.PlayClickSound();
        ApplyFilter();
    }

    /// <summary>WPF's StartupSearchBox_TextChanged broke the search into the same filter.</summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<StartupItem> queryable = _allItems;

        queryable = ActiveFilter switch
        {
            StartupFilter.HighImpact => queryable.Where(x => x.IsEnabled && x.Impact == BootImpact.High),
            StartupFilter.Disabled => queryable.Where(x => !x.IsEnabled),
            StartupFilter.Orphaned => queryable.Where(x => x.IsFileMissing),
            _ => queryable,
        };

        // WPF's query matched five fields in OrdinalIgnoreCase: title, name,
        // publisher, location and raw command - a location search is how you find
        // "which startup entry comes from this folder".
        string query = SearchText.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            queryable = queryable.Where(x =>
                x.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.LocationDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Command.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        Items.Clear();
        foreach (StartupItem item in queryable)
        {
            Items.Add(item);
        }
    }

    private void UpdateHeroStats()
    {
        TotalText = $"{_allItems.Count} apps";
        HighImpactText = $"{_allItems.Count(x => x.IsEnabled && x.Impact == BootImpact.High)} high impact";
        OrphanedText = $"{_allItems.Count(x => x.IsFileMissing)} cleanable";
        BootDelayText = $"~{StartupManagerService.CalculateEstimatedBootDelaySeconds(_allItems):F1}s delay";
    }

    /// <summary>
    /// Bulk-disables non-essential high-impact startup entries.
    ///
    /// Gated on a confirmation because this writes to the registry for many
    /// entries at once. The service itself decides which items are safe to
    /// disable; this only decides whether to ask first.
    /// </summary>
    private async Task OptimizeBootAsync()
    {
        if (_allItems.Count == 0)
        {
            return;
        }

        SoundService.PlayClickSound();

        int candidates = _allItems.Count(x => x.IsEnabled && x.Impact == BootImpact.High);
        if (candidates == 0)
        {
            StatusText = "All active startup apps are already optimized.";
            return;
        }

        bool confirmed = await _confirmAsync(
            "Optimize Boot",
            $"Disable {candidates} non-essential high-impact startup " +
            $"{(candidates == 1 ? "entry" : "entries")}?\n\n" +
            "This writes to the Windows registry and will lengthen the next boot. " +
            "You can re-enable any entry individually afterwards.");

        if (!confirmed)
        {
            return;
        }

        List<StartupItem> disabled = StartupManagerService.OptimizeBoot(_allItems);

        if (disabled.Count > 0)
        {
            LogRequested?.Invoke(
                $"Boot optimized: safely disabled {disabled.Count} non-essential high-impact " +
                "background startup apps.",
                LogLevel.Success);

            StatusText =
                $"Optimized boot: {disabled.Count} non-essential apps disabled. Boot delay reduced.";
        }
        else
        {
            StatusText = "All active startup apps are already optimized.";
        }

        UpdateHeroStats();
        ApplyFilter();
    }

    private async Task CleanOrphanedAsync(StartupItem? item)
    {
        if (item is null || !item.CanCleanOrphaned)
        {
            return;
        }

        SoundService.PlayClickSound();

        bool confirmed = await _confirmAsync(
            "Clean Orphaned Entry",
            $"Remove orphaned startup entry for '{item.DisplayTitle}'?\n\n" +
            $"Location: {item.LocationDisplay}");

        if (!confirmed)
        {
            return;
        }

        if (StartupManagerService.RemoveOrphanedStartupItem(item))
        {
            _allItems.Remove(item);
            LogRequested?.Invoke($"Cleaned orphaned startup entry: '{item.DisplayTitle}'", LogLevel.Success);
        }
        else
        {
            LogRequested?.Invoke(
                $"Failed to remove orphaned entry: '{item.DisplayTitle}'",
                LogLevel.Warning);
        }

        UpdateHeroStats();
        ApplyFilter();
    }

    private void Reveal(StartupItem? item)
    {
        if (item is null || _openFolder is null)
        {
            return;
        }

        SoundService.PlayClickSound();

        // Prefer the resolved exe; fall back to the raw registry command, which
        // is all that exists once the file is gone (the orphaned case).
        string path = !string.IsNullOrWhiteSpace(item.ExePath) && File.Exists(item.ExePath)
            ? item.ExePath
            : item.Command;

        if (File.Exists(path))
        {
            _openFolder(path);
        }
    }

    /// <summary>
    /// Enables or disables one entry.
    ///
    /// The CanToggle check here is a UI convenience. The real enforcement is
    /// inside ToggleStartupItem, which refuses protected items regardless of
    /// what the UI allowed.
    /// </summary>
    private async Task ToggleAsync(StartupItem? item)
    {
        if (item is null || IsLoading)
        {
            return;
        }

        SoundService.PlayClickSound();

        if (!item.CanToggle)
        {
            StatusText = $"'{item.DisplayTitle}' is a protected system startup entry and cannot be changed.";
            LogRequested?.Invoke(
                $"[Startup] Refused to toggle protected entry: '{item.DisplayTitle}'",
                LogLevel.Warning);
            return;
        }

        bool target = !item.IsEnabled;
        bool ok = StartupManagerService.ToggleStartupItem(item, target);

        StatusText = ok
            ? $"{(target ? "Enabled" : "Disabled")} '{item.DisplayTitle}'"
            : $"Could not change '{item.DisplayTitle}'. Administrator rights may be required.";

        LogRequested?.Invoke(
            $"[Startup] {(ok ? "Updated" : "Failed to update")} '{item.DisplayTitle}'",
            ok ? LogLevel.Success : LogLevel.Warning);

        UpdateHeroStats();
        ApplyFilter();

        await Task.CompletedTask;
    }

    private void ToggleExpand(StartupItem? item)
    {
        if (item is null)
        {
            return;
        }

        SoundService.PlayClickSound();
        item.IsExpanded = !item.IsExpanded;
    }

    /// <summary>
    /// WPF's StartupItemAi_Click: expands the row and asks
    /// StartupIntelligenceService for a disable-impact briefing (forceOnline,
    /// so the row button is the user's explicit online AI request; the batch
    /// below stays offline-first). Never called by the self-test.
    /// </summary>
    private async Task AnalyzeItemAsync(StartupItem? item)
    {
        if (item is null)
        {
            return;
        }

        SoundService.PlayClickSound();

        // If already expanded and analyzed, toggle collapse - WPF did the same.
        if (item.IsExpanded && item.IsAiEnriched)
        {
            item.IsExpanded = false;
            return;
        }

        item.IsExpanded = true;
        StatusText = $"Analyzing disable impact for '{item.DisplayTitle}' with AI...";

        try
        {
            StartupAiReport report = await StartupIntelligenceService.AnalyzeStartupItemAsync(item, forceOnline: true);

            item.Description = report.Description;
            item.DisableVerdict = report.Verdict;
            item.DisableImpact = report.DisableImpact;
            item.Recommendation = report.Recommendation;
            item.IsAiEnriched = true;
            item.AiProviderUsed = report.ProviderUsed;

            StatusText = $"Analysis complete for '{item.DisplayTitle}' via {report.ProviderUsed}.";
            LogRequested?.Invoke(
                $"AI Startup Intelligence analyzed '{item.DisplayTitle}': {report.Verdict} ({report.ProviderUsed})",
                LogLevel.Info);
        }
        catch (Exception ex)
        {
            StatusText = $"AI analysis failed: {ex.Message}";
            LogRequested?.Invoke($"Startup AI error: {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// WPF's StartupBatchAi_Click: walks the not-yet-analyzed entries offline
    /// first (forceOnline: false means the service falls back to its local
    /// intelligence instead of querying a provider). Never called by the
    /// self-test.
    /// </summary>
    private async Task BatchAnalyzeAsync()
    {
        if (_allItems.Count == 0)
        {
            return;
        }

        SoundService.PlayClickSound();

        var unanalyzed = _allItems
            .Where(x => !x.IsAiEnriched &&
                        x.DisableVerdict == StartupDisableVerdict.SafeToDisable &&
                        string.IsNullOrWhiteSpace(x.Recommendation))
            .ToList();
        if (unanalyzed.Count == 0)
        {
            unanalyzed = [.. _allItems.Where(x => !x.IsAiEnriched)];
        }

        if (unanalyzed.Count == 0)
        {
            StatusText = "All startup items are already analyzed and verified.";
            return;
        }

        int count = 0;
        int total = unanalyzed.Count;

        try
        {
            foreach (StartupItem item in unanalyzed)
            {
                count++;
                StatusText = $"AI analyzing startup items ({count}/{total}): {item.DisplayTitle}...";

                StartupAiReport report = await StartupIntelligenceService.AnalyzeStartupItemAsync(item, forceOnline: false);
                if (report is not null)
                {
                    item.Description = report.Description;
                    item.DisableVerdict = report.Verdict;
                    item.DisableImpact = report.DisableImpact;
                    item.Recommendation = report.Recommendation;
                    item.IsAiEnriched = true;
                    item.AiProviderUsed = report.ProviderUsed;
                }
            }

            StatusText = $"Batch AI analysis completed for {total} startup items.";
            LogRequested?.Invoke($"Completed AI analysis on {total} startup programs.", LogLevel.Success);
        }
        catch (Exception ex)
        {
            StatusText = $"Batch analysis ended: {ex.Message}";
        }
    }

    /// <summary>Single funnel for log lines raised outside this class.</summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
        => LogRequested?.Invoke(message, level);

    /// <summary>
    /// Port of WPF's StartupSearchOnline_Click (MainWindow.SystemManagers.cs:159-174):
    /// opens the entry's name on Google so the user can research the process's
    /// reputation and safety. Fire-and-forget like the WPF version - the click sound
    /// plays, the launch failure stays silent.
    /// </summary>
    public void SearchOnline(StartupItem? item)
    {
        if (item is null) return;

        SoundService.PlayClickSound();

        string query = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.DisplayTitle;
        try
        {
            Process.Start(new ProcessStartInfo(
                $"https://www.google.com/search?q={Uri.EscapeDataString(query + " process windows startup")}")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // The WPF handler swallowed launch failures the same way.
        }
    }

    /// <summary>
    /// Reveals a file in Explorer with it selected. Supplied by the view so the
    /// shell integration stays out of this ViewModel.
    /// </summary>
    public void OpenInShell(string path)
    {
        try
        {
            // /select, is the Explorer idiom for "show me this file".
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log($"Could not reveal '{path}': {ex.Message}", LogLevel.Warning);
        }
    }
}
