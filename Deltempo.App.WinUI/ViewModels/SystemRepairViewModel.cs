using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>One subsystem row in the health matrix.</summary>
public sealed partial class SubsystemRowViewModel : ObservableObject
{
    public SubsystemRowViewModel(string name)
    {
        Name = name;
        Details = "Not assessed yet";
    }

    public string Name { get; }

    public string Details { get; private set; } = "Not assessed yet";

    /// <summary>Green when healthy, red when not. Hex so the ViewModel stays free of UI types.</summary>
    public string IndicatorHex { get; private set; } = "#5E6E80";

    public bool IsHealthy { get; private set; }

    [ObservableProperty]
    public partial bool IsAssessed { get; private set; }

    /// <summary>
    /// Resolves the status colours here rather than in a trigger.
    ///
    /// The WPF version used BrushConverter calls scattered through the
    /// code-behind, which meant the green/red rule lived in six places. One
    /// place now, and the two UIs cannot disagree about what "healthy" looks like.
    /// </summary>
    public void Set(bool healthy, string details)
    {
        IsHealthy = healthy;
        Details = details;
        IndicatorHex = healthy ? "#10B981" : "#EF4444";
        IsAssessed = true;

        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(IndicatorHex));
        OnPropertyChanged(nameof(IsHealthy));
    }

    /// <summary>Post-repair state: everything healthy, with the tool's label.</summary>
    public void MarkRemediated(string details)
    {
        Set(true, details);
    }
}

/// <summary>Verdict headline on the executive remediation card.</summary>
public sealed partial class RemediationVerdictViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Glyph { get; set; } = "\uE7BA";

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Details { get; set; } = string.Empty;

    /// <summary>Card surface / border / accent, carried as hex for the same reason.</summary>
    [ObservableProperty]
    public partial string BackgroundHex { get; set; } = "#291E14";

    [ObservableProperty]
    public partial string BorderHex { get; set; } = "#8CF59E0B";

    [ObservableProperty]
    public partial string AccentHex { get; set; } = "#F59E0B";

    /// <summary>All-clear presentation.</summary>
    public void SetHealthy(string title, string details)
    {
        Glyph = "\uE73E";
        Title = title;
        Details = details;
        BackgroundHex = "#142B20";
        BorderHex = "#3D10B981";
        AccentHex = "#10B981";
    }

    /// <summary>Anomalies-found presentation.</summary>
    public void SetIssues(int issuesCount, string details)
    {
        Glyph = "\uE7BA";
        Title = $"Integrity Anomalies Detected ({issuesCount} Subsystems)";
        Details = details;
        BackgroundHex = "#291E14";
        BorderHex = "#8CF59E0B";
        AccentHex = "#F59E0B";
    }

    /// <summary>All-green presentation after a successful autonomous repair.</summary>
    public void SetRemediated(string title, string details)
    {
        Glyph = "\uE73E";
        Title = title;
        Details = details;
        BackgroundHex = "#142B20";
        BorderHex = "#3D10B981";
        AccentHex = "#10B981";
    }
}

/// <summary>
/// WinUI 3 port of the System Repair / Health Console.
///
/// Every tool behind this screen is a privileged, mutating system operation:
/// DISM RestoreHealth, SFC /scannow, WinSxS component cleanup, CHKDSK,
/// Windows Update stack reset, and a Winsock/network stack reset. So the safety
/// properties are the point of this port, not an afterthought:
///
///  1. ONE OPERATION AT A TIME. The WPF modal guarded every one of its nine
///     buttons with `if (_isSystemRepairRunning) return`. That lock is
///     reproduced as a single re-entrancy check in RunRepairAsync, so a second
///     request is dropped regardless of which button raised it. Running DISM
///     and CHKDSK concurrently is exactly the failure mode this prevents.
///  2. CANCEL IS ALWAYS REACHABLE and closing mid-run asks first, then cancels.
///  3. Restore point pruning is gated on an injected confirmation that defaults
///     to refusing.
///  4. Elevation is surfaced, not assumed: a non-admin user sees a banner and
///     an explicit relaunch option rather than silently failing mid-operation.
///
/// Self-test policy: none of the nine tools may run under the self-test. They
/// mutate the live system, several need Administrator, and CHKDSK and the
/// network reset can leave Windows unusable if interrupted. The self-test loads
/// the surface and reads the elevation state only.
public sealed partial class SystemRepairViewModel : ObservableObject
{
    /// <summary>Shown before a destructive/system-mutating action. Defaults to refusing.</summary>
    private readonly Func<string, string, Task<bool>> _confirmAsync;

    /// <summary>Relaunches the app elevated. Null means unavailable.</summary>
    private readonly Action? _elevate;

    private CancellationTokenSource? _cts;

    public SystemRepairViewModel(
        Func<string, string, Task<bool>>? confirmAsync = null,
        Action? elevate = null)
    {
        // Fail closed: with no confirmation host, pruning cannot be requested.
        _confirmAsync = confirmAsync ?? ((_, _) => Task.FromResult(false));
        _elevate = elevate;

        QuickAssessmentCommand = new AsyncRelayCommand(RunQuickAssessmentAsync);
        AutonomousRepairCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("Autonomous System Repair", RunAutonomousRepairAsync));
        SfcCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("System File Checker (SFC)", RunSfcAsync));
        DismCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("DISM RestoreHealth", RunDismAsync));
        WinSxSCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("WinSxS Component Store Scavenging", RunWinSxSAsync));
        ChkdskCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("Filesystem Integrity (CHKDSK C:)", RunChkdskAsync));
        UpdateResetCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("Windows Update Servicing Stack Reset", RunUpdateResetAsync));
        NetworkResetCommand = new AsyncRelayCommand(() =>
            RunRepairAsync("Network & Winsock Stack Reset", RunNetworkResetAsync));
        LaunchWinUtilCommand = new RelayCommand(LaunchWinUtil);
        CancelCommand = new RelayCommand(Cancel);
        CloseCommand = new AsyncRelayCommand(CloseAsync);
        ElevateCommand = new RelayCommand(Raise);
        ClearLogCommand = new RelayCommand(ClearLog);
        CopyLogCommand = new RelayCommand(CopyLog);
        RefreshRestorePointsCommand = new AsyncRelayCommand(LoadRestorePointsAsync);
        PruneRestorePointsCommand = new AsyncRelayCommand(PruneRestorePointsAsync);

        Subsystems =
        [
            new SubsystemRowViewModel("Component Store"),
            new SubsystemRowViewModel("System Files"),
            new SubsystemRowViewModel("Filesystem"),
            new SubsystemRowViewModel("Servicing Stack"),
        ];

        RefreshElevationState();
    }

    public event Action<string, LogLevel>? LogRequested;

    public event Action? CloseRequested;

    /// <summary>Raised with (title, message) when an operation fails unrecoverably.</summary>
    public event Action<string, string>? FatalError;

    /// <summary>Convenience for surfacing an unrecoverable failure.</summary>
    public void ReportFatal(string title, string message) => FatalError?.Invoke(title, message);

    public SubsystemRowViewModel ComponentStore => Subsystems[0];

    public SubsystemRowViewModel SystemFiles => Subsystems[1];

    public SubsystemRowViewModel Filesystem => Subsystems[2];

    public SubsystemRowViewModel ServicingStack => Subsystems[3];

    public SubsystemRowViewModel[] Subsystems { get; }

    public RemediationVerdictViewModel Verdict { get; } = new();

    /// <summary>Terminal transcript. Bound as text; appended to, never re-bound.</summary>
    public string TerminalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string RunningOperationName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready. Run a quick assessment to begin.";

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial bool IsVerdictVisible { get; set; }

    [ObservableProperty]
    public partial bool IsAdministrator { get; set; }

    [ObservableProperty]
    public partial string ElevationBadgeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OverallRatingText { get; set; } = "Scanning Subsystems...";

    [ObservableProperty]
    public partial string OverallRatingHex { get; set; } = "#5E6E80";

    [ObservableProperty]
    public partial string RestorePointsBadgeText { get; set; } = "...";

    [ObservableProperty]
    public partial string RestorePointsSummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasRestorePoints { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingRestorePoints { get; set; }

    public ObservableCollection<RestorePointDetail> RestorePoints { get; } = [];

    /// <summary>Every repair button's enablement is derived from this one flag.</summary>
    public bool CanRunTools => !IsRunning;

    public bool CanCancel => IsRunning;

    public bool NeedsElevation => !IsAdministrator;

    public bool CanPruneRestorePoints => RestorePoints.Count > 1 && !IsRunning;

    public IAsyncRelayCommand QuickAssessmentCommand { get; }

    public IAsyncRelayCommand AutonomousRepairCommand { get; }

    public IAsyncRelayCommand SfcCommand { get; }

    public IAsyncRelayCommand DismCommand { get; }

    public IAsyncRelayCommand WinSxSCommand { get; }

    public IAsyncRelayCommand ChkdskCommand { get; }

    public IAsyncRelayCommand UpdateResetCommand { get; }

    public IAsyncRelayCommand NetworkResetCommand { get; }

    public IRelayCommand LaunchWinUtilCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand CloseCommand { get; }

    public IRelayCommand ElevateCommand { get; }

    public IRelayCommand ClearLogCommand { get; }

    public IRelayCommand CopyLogCommand { get; }

    public IAsyncRelayCommand RefreshRestorePointsCommand { get; }

    public IAsyncRelayCommand PruneRestorePointsCommand { get; }

    /// <summary>
    /// Mirrors the WPF Open(): refresh elevation, then load restore points.
    ///
    /// Neither path mutates the system. Restore point enumeration is a read.
    /// </summary>
    public async Task OpenAsync()
    {
        SoundService.PlayClickSound();
        RefreshElevationState();
        await LoadRestorePointsAsync();
    }

    private void RefreshElevationState()
    {
        IsAdministrator = ElevationService.IsAdministrator;
        ElevationBadgeText = IsAdministrator ? "Administrator" : "Unprivileged (Standard)";

        OnPropertyChanged(nameof(NeedsElevation));
    }

    private void Raise()
    {
        SoundService.PlayClickSound();
        _elevate?.Invoke();
    }

    /// <summary>
    /// Mirrors the WPF CloseModal(): if a repair is running, confirm before
    /// cancelling it. Returns false when the user backs out, so the caller can
    /// keep the surface open.
    /// </summary>
    public async Task<bool> RequestCloseAsync()
    {
        if (IsRunning)
        {
            bool proceed = await _confirmAsync(
                "Cancel Repair Operation",
                "A system repair operation is currently running. Closing this dialog will " +
                "cancel the running operation. Continue?");

            if (!proceed)
            {
                return false;
            }

            Cancel();
        }

        SoundService.PlayClickSound();
        CloseRequested?.Invoke();
        return true;
    }

    private async Task CloseAsync() => await RequestCloseAsync();

    private void Cancel()
    {
        SoundService.PlayClickSound();

        if (_cts is { IsCancellationRequested: false })
        {
            AppendTerminal("[Operation] Cancelling running operation...");
            _cts.Cancel();
            StatusText = "Operation cancelled by user.";
        }
    }

    private void ClearLog()
    {
        SoundService.PlayClickSound();
        TerminalText = string.Empty;
        OnPropertyChanged(nameof(TerminalText));
    }

    private void CopyLog()
    {
        SoundService.PlayClickSound();
        LogRequested?.Invoke("System repair terminal output copied to clipboard.", LogLevel.Info);
        ClipboardRequested?.Invoke(TerminalText);
    }

    /// <summary>Raised with the terminal text; the view owns the clipboard.</summary>
    public event Action<string>? ClipboardRequested;

    /// <summary>
    /// The single entry point for terminal output.
    ///
    /// Repair tools stream output from worker threads, so every line funnels
    /// through one place that marshals to the UI thread. Keeping the marshalling
    /// here means no caller can forget it.
    /// </summary>
    private void AppendTerminal(string line)
    {
        void Append()
        {
            var sb = new StringBuilder(TerminalText)
                .Append(line)
                .Append(Environment.NewLine);

            TerminalText = sb.ToString();
            OnPropertyChanged(nameof(TerminalText));
        }

        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (dispatcher is null)
        {
            Append();
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            Append();
        }
        else
        {
            dispatcher.TryEnqueue(() => Append());
        }
    }

    /// <summary>Single funnel for log lines raised outside this class.</summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
        => LogRequested?.Invoke(message, level);

    // ---- The 4-point read-only assessment ----------------------------------

    /// <summary>
    /// Runs the 4-point health assessment. Read-only: DISM CheckHealth, the
    /// volume dirty bit, the CBS log, and the servicing stack state. This is the
    /// only tool here that a self-test may safely invoke, and even then only
    /// outside the automated run because it shells out to DISM.
    /// </summary>
    private async Task RunQuickAssessmentAsync()
    {
        if (!TryBeginOperation("Quick Integrity Assessment"))
        {
            return;
        }

        SoundService.PlayClickSound();
        ProgressPercent = 0;
        IsIndeterminate = false;
        OverallRatingText = "Scanning Subsystems...";
        OverallRatingHex = "#5E6E80";
        AppendTerminal($"\n>>> [{DateTime.Now:HH:mm:ss}] Starting 4-Point Quick Health Assessment...");

        try
        {
            SystemHealthAssessmentResult result = await SystemRepairService
                .RunQuickHealthAssessmentAsync(AppendTerminal, ApplyProgress, _cts!.Token);

            ProgressPercent = 100;
            StatusText = $"Assessment complete: {result.OverallRating}";

            ComponentStore.Set(result.ComponentStoreHealthy, result.ComponentStoreDetails);
            SystemFiles.Set(result.SystemFilesHealthy, result.SystemFilesDetails);
            Filesystem.Set(result.FilesystemHealthy, result.FilesystemDetails);
            ServicingStack.Set(result.ServicingStackHealthy, result.ServicingStackDetails);

            OverallRatingText = result.OverallRating;
            OverallRatingHex = result.OverallColor;

            IsVerdictVisible = true;

            if (result.IssuesCount == 0)
            {
                Verdict.SetHealthy("Subsystems Verified 100% Intact", result.Recommendation);
                SoundService.PlaySuccessSound();
            }
            else
            {
                Verdict.SetIssues(result.IssuesCount, result.Recommendation);
                SoundService.PlayWarningSound();
            }

            LogRequested?.Invoke(
                $"[Health Assessment] {result.OverallRating}",
                result.IssuesCount == 0 ? LogLevel.Success : LogLevel.Warning);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Assessment cancelled.";
            AppendTerminal("\n<<< Assessment Cancelled.");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendTerminal($"\n[ERROR] {ex.Message}");
            LogRequested?.Invoke($"[Health Assessment Error] {ex.Message}", LogLevel.Error);
            ReportFatal("Assessment Failed", $"The health assessment could not be completed.\n\n{ex.Message}");
        }
        finally
        {
            EndOperation();
        }
    }

    // ---- The mutating tools -------------------------------------------------

    /// <summary>
    /// The re-entrancy lock, in one place.
    ///
    /// Returns false when an operation is already running, which is the exact
    /// guard the WPF modal applied independently in each of its nine handlers.
    /// Funnelling every tool through here means the lock cannot be forgotten
    /// when a tenth tool is added.
    /// </summary>
    private bool TryBeginOperation(string operationName)
    {
        if (IsRunning)
        {
            return false;
        }

        IsRunning = true;
        RunningOperationName = operationName;
        _cts = new CancellationTokenSource();

        // A running repair hides the verdict card: its contents describe a
        // previous assessment and would be misleading mid-operation.
        IsVerdictVisible = false;

        OnPropertyChanged(nameof(CanRunTools));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanPruneRestorePoints));
        return true;
    }

    private void EndOperation()
    {
        IsRunning = false;
        RunningOperationName = string.Empty;

        _cts?.Dispose();
        _cts = null;

        OnPropertyChanged(nameof(CanRunTools));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanPruneRestorePoints));
    }

    /// <summary>
    /// Runs one repair tool under the lock, with progress and terminal output
    /// wired up and cancellation honoured.
    /// </summary>
    private async Task RunRepairAsync(
        string operationName,
        Func<Action<double>, Action<string>, CancellationToken, Task<RepairExecutionResult>> action)
    {
        if (!TryBeginOperation(operationName))
        {
            return;
        }

        SoundService.PlayClickSound();
        ProgressPercent = 0;
        IsIndeterminate = false;
        AppendTerminal($"\n>>> [{DateTime.Now:HH:mm:ss}] Starting {operationName}...");

        try
        {
            RepairExecutionResult result = await action(ApplyProgress, AppendTerminal, _cts!.Token);

            ProgressPercent = 100;
            StatusText = result.Message;

            LogRequested?.Invoke(
                $"[System Repair] {result.Message}",
                result.Success ? LogLevel.Success : LogLevel.Warning);

            AppendTerminal(
                $"\n<<< [{DateTime.Now:HH:mm:ss}] {operationName} Finished. " +
                $"ExitCode: {result.ExitCode}. Success: {result.Success}");

            if (result.Success)
            {
                SoundService.PlaySuccessSound();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = $"{operationName} was cancelled.";
            AppendTerminal($"\n<<< [{DateTime.Now:HH:mm:ss}] {operationName} Cancelled.");
            LogRequested?.Invoke($"[System Repair] {operationName} cancelled.", LogLevel.Warning);
            SoundService.PlayWarningSound();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendTerminal($"\n[ERROR] Exception occurred: {ex.Message}");
            LogRequested?.Invoke($"[System Repair Error] {ex.Message}", LogLevel.Error);
            ReportFatal("System Repair Failed", $"{RunningOperationName} could not be completed.\n\n{ex.Message}");
            SoundService.PlayErrorSound();
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    /// Normalizes tool progress to a 0-100 percentage.
    ///
    /// Tools report either a 0..1 fraction or a 0..100 percentage depending on
    /// the tool, and some overshoot. Clamping here keeps every bar identical.
    /// </summary>
    private void ApplyProgress(double value)
    {
        double percent = value <= 1.0 ? value * 100 : value;
        percent = Math.Min(100, Math.Max(0, percent));

        void Apply()
        {
            ProgressPercent = percent;
            OnPropertyChanged(nameof(ProgressPercent));
        }

        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            Apply();
        }
        else
        {
            dispatcher.TryEnqueue(() => Apply());
        }
    }

    // ---- Tool bodies. Named so the command wiring above reads as a list. ----

    private Task<RepairExecutionResult> RunAutonomousRepairAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
    {
        Task<RepairExecutionResult> task =
            SystemRepairService.RunAutonomousHealthCheckAndRepairAsync(log, onProgress, ct);

        return AwaitAutonomousAsync(task);
    }

    /// <summary>
    /// Applies the all-green subsystem state after a successful autonomous
    /// repair. Kept separate from the command so the await and the UI update
    /// cannot interleave.
    /// </summary>
    private async Task<RepairExecutionResult> AwaitAutonomousAsync(Task<RepairExecutionResult> task)
    {
        RepairExecutionResult result = await task;

        if (result.Success)
        {
            void Apply()
            {
                ComponentStore.MarkRemediated("DISM Serviced");
                SystemFiles.MarkRemediated("SFC Protected");
                Filesystem.MarkRemediated("Filesystem Verified");
                ServicingStack.MarkRemediated("Stack Active");

                OverallRatingText = "Remediated (100% Healthy)";
                OverallRatingHex = "#10B981";

                Verdict.SetRemediated(
                    "Autonomous Remediation Succeeded",
                    "All DISM manifests, protected system files, and volumes have been " +
                    "verified and restored.");

                IsVerdictVisible = true;
            }

            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcher is null || dispatcher.HasThreadAccess)
            {
                Apply();
            }
            else
            {
                dispatcher.TryEnqueue(() => Apply());
            }
        }

        return result;
    }

    private Task<RepairExecutionResult> RunSfcAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
        => SystemRepairService.RunSfcScannowAsync(log, onProgress, ct);

    private Task<RepairExecutionResult> RunDismAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
        => SystemRepairService.RunDismRestoreHealthAsync(log, onProgress, ct);

    private Task<RepairExecutionResult> RunWinSxSAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
        => SystemRepairService.RunDismComponentCleanupAsync(log, onProgress, ct);

    private Task<RepairExecutionResult> RunChkdskAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
        => SystemRepairService.RunChkdskScanAsync("C:", log, onProgress, ct);

    private Task<RepairExecutionResult> RunUpdateResetAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
        => SystemRepairService.ResetWindowsUpdateStackAsync(log, onProgress, ct);

    private Task<RepairExecutionResult> RunNetworkResetAsync(
        Action<double> onProgress, Action<string> log, CancellationToken ct)
        => SystemRepairService.ResetNetworkStackAsync(log, onProgress, ct);

    /// <summary>
    /// Launches the third-party CTT WinUtil in an elevated PowerShell window.
    ///
    /// Not gated on the running lock or a confirmation, matching the WPF modal:
    /// it launches a separate, visible, elevated window that the user drives
    /// directly, and it does not mutate anything itself.
    /// </summary>
    private void LaunchWinUtil()
    {
        SoundService.PlayClickSound();
        AppendTerminal($"\n>>> [{DateTime.Now:HH:mm:ss}] Launching Chris Titus Tech Windows Utility (CTT WinUtil)...");
        AppendTerminal($"    {SystemRepairService.ChrisTitusCreditLine}");
        LogRequested?.Invoke(
            "[CTT WinUtil] Launching Chris Titus Tech WinUtil in elevated PowerShell...",
            LogLevel.Info);

        bool launched = SystemRepairService.LaunchChrisTitusWinUtil(out string error);

        if (launched)
        {
            AppendTerminal(
                $"<<< [{DateTime.Now:HH:mm:ss}] Chris Titus Tech WinUtil launched in an elevated PowerShell window.");
            LogRequested?.Invoke($"[CTT WinUtil] {SystemRepairService.ChrisTitusCreditLine}", LogLevel.Success);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            AppendTerminal($"[ERROR] Failed to launch CTT WinUtil: {error}");
            LogRequested?.Invoke($"[CTT WinUtil] {error}", LogLevel.Warning);
        }
    }

    // ---- Restore points -----------------------------------------------------

    /// <summary>Read-only: enumerates VSS snapshots and shadow storage usage.</summary>
    public async Task LoadRestorePointsAsync()
    {
        IsLoadingRestorePoints = true;

        try
        {
            RestorePointStorageInfo info = await RestorePointManagerService
                .QueryDetailedRestorePointsAsync();

            RestorePointsBadgeText = $"{info.SnapshotCount} snapshot{(info.SnapshotCount == 1 ? "" : "s")}";

            RestorePointsSummaryText = info.SnapshotCount > 0
                ? $"Shadow storage active: {info.FormattedUsed} allocated across {info.SnapshotCount} restore points."
                : "No active restore points found or VSS shadow storage is unallocated.";

            RestorePoints.Clear();
            foreach (RestorePointDetail point in info.Points)
            {
                RestorePoints.Add(point);
            }

            HasRestorePoints = info.Points.Count > 0;
        }
        catch (Exception ex)
        {
            AppendTerminal($"[RestorePoints] Query error: {ex.Message}");
        }
        finally
        {
            IsLoadingRestorePoints = false;
            OnPropertyChanged(nameof(CanPruneRestorePoints));
        }
    }

    /// <summary>
    /// Purges older VSS snapshots while deliberately retaining the newest.
    ///
    /// Two guards, both preserved: an explicit confirmation (injected, and
    /// defaulting to refusing), and pruning only being offered when more than
    /// one snapshot exists, so there is always something left to restore to.
    /// </summary>
    private async Task PruneRestorePointsAsync()
    {
        if (!CanPruneRestorePoints)
        {
            return;
        }

        bool confirmed = await _confirmAsync(
            "Prune Older Restore Points",
            "Safely prune legacy VSS restore points?\n\nThis will purge older snapshots " +
            "while retaining the latest restore point intact for safety.");

        if (!confirmed)
        {
            return;
        }

        SoundService.PlayClickSound();
        AppendTerminal("\n>>> Starting VSS Restore Point Pruning (retaining newest)...");

        try
        {
            var (success, reclaimed, message) = await RestorePointManagerService
                .PruneOlderRestorePointsAsync((msg, lvl) =>
                    AppendTerminal($"[{lvl}] {msg}"));

            if (success)
            {
                SoundService.PlaySuccessSound();
                AppendTerminal($"<<< VSS Pruning Finished. Reclaimed: {TargetFolderInfo.FormatBytes(reclaimed)}.");
                LogRequested?.Invoke($"[Restore Points] {message}", LogLevel.Success);
            }
            else
            {
                AppendTerminal($"<<< VSS Pruning: {message}");
                LogRequested?.Invoke($"[Restore Points] {message}", LogLevel.Warning);
                SoundService.PlayWarningSound();
            }

            await LoadRestorePointsAsync();
        }
        catch (Exception ex)
        {
            AppendTerminal($"[RestorePoints] Prune error: {ex.Message}");
            LogRequested?.Invoke($"[Restore Points] Prune failed: {ex.Message}", LogLevel.Error);
        }
    }
}