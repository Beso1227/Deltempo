using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// One process row in the process manager.
///
/// Immutable by design: the list is rebuilt on every refresh, so a row never has to
/// raise a change notification - it is replaced, not updated.
/// </summary>
public sealed class ProcessRow
{
    public required string ProcessName { get; init; }

    public required string WindowTitle { get; init; }

    public required int ProcessId { get; init; }

    public required long WorkingSetBytes { get; init; }

    /// <summary>
    /// False for a process the service refuses to act on. Core decides this; the UI
    /// only renders it, so the refusal rule lives in exactly one place.
    /// </summary>
    public required bool IsSafeToClose { get; init; }

    public string MemoryText => WorkingSetBytes >= 1024L * 1024 * 1024
        ? $"{WorkingSetBytes / (1024d * 1024 * 1024):F1} GB"
        : $"{WorkingSetBytes / (1024d * 1024):F0} MB";

    public string TitleText => string.IsNullOrWhiteSpace(WindowTitle)
        ? ProcessName
        : $"{ProcessName} - {WindowTitle}";

    public bool CanTrim => IsSafeToClose;

    public bool CanEndTask => IsSafeToClose;
}

/// <summary>
/// WinUI 3 port of the WPF process manager overlay (MainWindow.xaml's
/// ProcessModalOverlay, driven from MainWindow.Memory.cs).
///
/// The WPF version was an overlay with its own list, its own refresh timer and its
/// own two buttons; it is a workspace here because it is a tool in its own right, not
/// a sub-dialog of the memory screen.
///
/// Windows Core Process Immunity is honoured the strict way: a protected process is
/// never listed and never offered an action. ProcessOptimizerService already refuses
/// to trim or kill one, and this list goes further - showing a system-critical
/// process next to an "End task" button is an invitation that the service will
/// refuse anyway, so it is not offered in the first place.
/// </summary>
public sealed partial class ProcessWorkspaceViewModel : ObservableObject
{
    /// <summary>Working set above which a process is worth showing.</summary>
    private const long MemoryThresholdBytes = 20L * 1024 * 1024;

    public ProcessWorkspaceViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        TrimCommand = new AsyncRelayCommand<ProcessRow>(TrimAsync);
        EndTaskCommand = new AsyncRelayCommand<ProcessRow>(EndTaskAsync);
    }

    public ObservableCollection<ProcessRow> Processes { get; } = new();

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "Reading process memory...";

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand<ProcessRow> TrimCommand { get; }

    public IAsyncRelayCommand<ProcessRow> EndTaskCommand { get; }

    public event Action<string, LogLevel>? LogRequested;

    /// <summary>WPF's ProcessSearchBox: filters by process name or window title.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>The scanned set before the search filter; the list always shows the filtered one.</summary>
    private readonly List<ProcessRow> _all = [];

    /// <summary>
    /// Test seam: replaces the scanned set directly, the way StartupWorkspaceViewModel
    /// does, so search can be exercised without a real process walk.
    /// </summary>
    internal void LoadFrom(IEnumerable<ProcessRow> rows)
    {
        _all.Clear();
        _all.AddRange(rows);
        ApplySearch();
    }

    /// <summary>WPF's ProcessSearchBox_TextChanged broke the query into the list.</summary>
    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        string query = SearchText.Trim();
        Processes.Clear();
        if (string.IsNullOrEmpty(query))
        {
            foreach (ProcessRow row in _all) Processes.Add(row);
            return;
        }

        foreach (ProcessRow row in _all)
        {
            if (row.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.TitleText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                Processes.Add(row);
            }
        }
    }

    /// <summary>
    /// Reads the live process list. Read-only: nothing here changes a process, it
    /// only reads what the OS reports.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = string.Empty;

        try
        {
            List<ProcessMemoryInfo> heavy =
                await ProcessOptimizerService.GetHeavyProcessesAsync(MemoryThresholdBytes);

            _all.Clear();
            Processes.Clear();
            int protectedCount = 0;

            foreach (ProcessMemoryInfo process in heavy)
            {
                if (ProcessOptimizerService.IsProtectedProcess(process.ProcessName))
                {
                    protectedCount++;
                    continue;
                }

                ProcessRow row = new()
                {
                    ProcessName = process.ProcessName,
                    WindowTitle = process.WindowTitle,
                    ProcessId = process.ProcessId,
                    WorkingSetBytes = process.WorkingSetBytes,
                    IsSafeToClose = process.IsSafeToClose,
                };
                _all.Add(row);
            }

            // A refresh re-applies the active search, the way WPF's ApplyProcessFilter
            // re-filtered the whole scan output.
            ApplySearch();

            SummaryText = Processes.Count == 0
                ? "No process is holding more than 20 MB."
                : $"{Processes.Count} process{(Processes.Count == 1 ? string.Empty : "es")} above 20 MB"
                  + (protectedCount > 0
                      ? $" - {protectedCount} protected system process{(protectedCount == 1 ? string.Empty : "es")} not listed."
                      : ".");
        }
        catch (Exception ex)
        {
            StatusText = $"Could not read the process list: {ex.Message}";
            LogRequested?.Invoke(StatusText, LogLevel.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Trims a process working set. The privileged call itself lives in Core; this
    /// only decides whether to ask.
    /// </summary>
    public async Task TrimAsync(ProcessRow? row)
    {
        if (row is null || !row.CanTrim)
        {
            return;
        }

        bool trimmed = await Task.Run(() => ProcessOptimizerService.TrimProcessMemory(row.ProcessId));

        StatusText = trimmed
            ? $"Trimmed {row.ProcessName} ({row.MemoryText} working set)."
            : $"Windows refused to trim {row.ProcessName}.";

        LogRequested?.Invoke(StatusText, trimmed ? LogLevel.Info : LogLevel.Warning);
        await RefreshAsync();
    }

    /// <summary>
    /// Ends a process the user chose. Protected processes never reach this method:
    /// they are not listed, and the service refuses them anyway.
    /// </summary>
    public async Task EndTaskAsync(ProcessRow? row)
    {
        if (row is null || !row.CanEndTask)
        {
            return;
        }

        bool ended = await Task.Run(() => ProcessOptimizerService.SafeTerminateProcess(row.ProcessId));

        StatusText = ended
            ? $"Ended {row.ProcessName}."
            : $"Windows refused to end {row.ProcessName}.";

        LogRequested?.Invoke(StatusText, ended ? LogLevel.Info : LogLevel.Warning);
        await RefreshAsync();
    }
}
