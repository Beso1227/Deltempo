using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

public partial class SystemRepairModal : UserControl
{
    private bool _isSystemRepairRunning;
    private CancellationTokenSource? _systemRepairCts;

    public event Action<string, LogLevel>? LogRequested;
    public event Action? Closed;

    public SystemRepairModal()
    {
        InitializeComponent();
    }

    public void Open()
    {
        SoundService.PlayClickSound();
        Visibility = Visibility.Visible;
        RefreshSystemRepairAdminStatus();
        _ = LoadRestorePointsAsync();
    }

    public bool CloseModal()
    {
        if (_isSystemRepairRunning)
        {
            var res = MessageBox.Show(
                "A system repair operation is currently running. Closing this dialog will cancel the running operation. Continue?",
                "Cancel Repair Operation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return false;
            CancelSystemRepair_Click(this, new RoutedEventArgs());
        }

        SoundService.PlayClickSound();
        Visibility = Visibility.Collapsed;
        Closed?.Invoke();
        return true;
    }

    private void CloseSystemRepairModal_Click(object sender, RoutedEventArgs e)
    {
        CloseModal();
    }

    private void ElevateForSystemRepair_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        ElevationService.RestartAsAdmin();
    }

    public void RefreshSystemRepairAdminStatus()
    {
        bool isAdmin = ElevationService.IsAdministrator;
        SystemRepairElevationBanner.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

        if (isAdmin)
        {
            SystemRepairAdminBadgeBorder.Background = (Brush)FindResource("AdminBadgeBgBrush");
            SystemRepairAdminBadgeBorder.BorderBrush = (Brush)FindResource("AdminBadgeBorderBrush");
            SystemRepairAdminBadgeText.Foreground = (Brush)FindResource("AdminBadgeTextBrush");
            SystemRepairAdminBadgeText.Text = "Administrator";
        }
        else
        {
            SystemRepairAdminBadgeBorder.Background = (Brush)FindResource("WarningBadgeBgBrush");
            SystemRepairAdminBadgeBorder.BorderBrush = (Brush)FindResource("WarningBadgeBorderBrush");
            SystemRepairAdminBadgeText.Foreground = (Brush)FindResource("WarningBadgeTextBrush");
            SystemRepairAdminBadgeText.Text = "Unprivileged (Standard)";
        }
    }

    private void ClearSystemRepairLog_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        SystemRepairTerminalTextBox.Text = string.Empty;
    }

    private void CopySystemRepairLog_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        try
        {
            Clipboard.SetText(SystemRepairTerminalTextBox.Text);
            LogRequested?.Invoke("System repair terminal output copied to clipboard.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Failed to copy terminal log: {ex.Message}", LogLevel.Warning);
        }
    }

    private void AppendSystemRepairTerminal(string line)
    {
        Dispatcher.Invoke(() =>
        {
            SystemRepairTerminalTextBox.AppendText(line + Environment.NewLine);
            SystemRepairTerminalTextBox.ScrollToEnd();
        });
    }

    private void SetSystemRepairRunning(bool running, string operationName)
    {
        _isSystemRepairRunning = running;
        HeroAutonomousRepairBtn.IsEnabled = !running;
        HeroQuickAssessmentBtn.IsEnabled = !running;
        ToolSfcBtn.IsEnabled = !running;
        ToolDismRestoreBtn.IsEnabled = !running;
        ToolWinSxSBtn.IsEnabled = !running;
        ToolChkdskBtn.IsEnabled = !running;
        ToolUpdateResetBtn.IsEnabled = !running;
        ToolNetworkResetBtn.IsEnabled = !running;
        CancelSystemRepairBtn.IsEnabled = running;

        if (running)
        {
            SystemRepairStatusText.Text = $"Running {operationName}...";
            SystemRepairProgressBar.IsIndeterminate = false;
            ExecutiveRemediationCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelSystemRepairBtn.IsEnabled = false;
        }
    }

    private void CancelSystemRepair_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_systemRepairCts != null && !_systemRepairCts.IsCancellationRequested)
        {
            AppendSystemRepairTerminal("[Operation] Cancelling running operation...");
            _systemRepairCts.Cancel();
            SystemRepairStatusText.Text = "Operation cancelled by user.";
        }
    }

    private async void RunQuickAssessment_Click(object sender, RoutedEventArgs e)
    {
        if (_isSystemRepairRunning) return;
        SoundService.PlayClickSound();

        _systemRepairCts = new CancellationTokenSource();
        SetSystemRepairRunning(true, "Quick Integrity Assessment");
        SystemRepairProgressBar.Value = 0;
        SystemRepairProgressPercentText.Text = "0%";
        SubsystemsOverallRatingText.Text = "Scanning Subsystems...";
        SubsystemsOverallRatingText.Foreground = (Brush)FindResource("TextMediumBrush");
        AppendSystemRepairTerminal($"\n>>> [{DateTime.Now:HH:mm:ss}] Starting 4-Point Quick Health Assessment...");

        void OnProgress(double val)
        {
            Dispatcher.Invoke(() =>
            {
                double pct = val <= 1.0 ? val * 100 : val;
                SystemRepairProgressBar.Value = Math.Min(100, Math.Max(0, pct));
                SystemRepairProgressPercentText.Text = $"{Math.Round(SystemRepairProgressBar.Value)}%";
            });
        }

        try
        {
            var result = await SystemRepairService.RunQuickHealthAssessmentAsync(
                AppendSystemRepairTerminal,
                OnProgress,
                _systemRepairCts.Token);

            SystemRepairProgressBar.Value = 100;
            SystemRepairProgressPercentText.Text = "100%";
            SystemRepairStatusText.Text = $"Assessment complete: {result.OverallRating}";

            // Update 4 Matrix Subsystem Cards
            UpdateSubsystemPill(ComponentStoreIndicator, ComponentStoreStatusText, result.ComponentStoreHealthy, result.ComponentStoreDetails);
            UpdateSubsystemPill(SystemFilesIndicator, SystemFilesStatusText, result.SystemFilesHealthy, result.SystemFilesDetails);
            UpdateSubsystemPill(FilesystemIndicator, FilesystemStatusText, result.FilesystemHealthy, result.FilesystemDetails);
            UpdateSubsystemPill(ServicingStackIndicator, ServicingStackStatusText, result.ServicingStackHealthy, result.ServicingStackDetails);

            SubsystemsOverallRatingText.Text = result.OverallRating;
            if (new BrushConverter().ConvertFromString(result.OverallColor) is Brush overallBrush)
            {
                SubsystemsOverallRatingText.Foreground = overallBrush;
            }

            // Show Executive Summary Card
            ExecutiveRemediationCard.Visibility = Visibility.Visible;
            if (result.IssuesCount == 0)
            {
                if (new BrushConverter().ConvertFromString("#142B20") is Brush bg) ExecutiveRemediationCard.Background = bg;
                if (new BrushConverter().ConvertFromString("#3D10B981") is Brush border) ExecutiveRemediationCard.BorderBrush = border;
                RemediationVerdictIcon.Text = "\uE73E";
                if (new BrushConverter().ConvertFromString("#10B981") is Brush green)
                {
                    RemediationVerdictIcon.Foreground = green;
                    RemediationVerdictTitle.Foreground = green;
                }
                RemediationVerdictTitle.Text = "Subsystems Verified 100% Intact";
                RemediationVerdictDetails.Text = result.Recommendation;
                SoundService.PlaySuccessSound();
            }
            else
            {
                if (new BrushConverter().ConvertFromString("#291E14") is Brush bg) ExecutiveRemediationCard.Background = bg;
                if (new BrushConverter().ConvertFromString("#8CF59E0B") is Brush border) ExecutiveRemediationCard.BorderBrush = border;
                RemediationVerdictIcon.Text = "\uE7BA";
                if (new BrushConverter().ConvertFromString("#F59E0B") is Brush amber)
                {
                    RemediationVerdictIcon.Foreground = amber;
                    RemediationVerdictTitle.Foreground = amber;
                }
                RemediationVerdictTitle.Text = $"Integrity Anomalies Detected ({result.IssuesCount} Subsystems)";
                RemediationVerdictDetails.Text = result.Recommendation;
            }

            LogRequested?.Invoke($"[Health Assessment] {result.OverallRating}", result.IssuesCount == 0 ? LogLevel.Success : LogLevel.Warning);
        }
        catch (OperationCanceledException)
        {
            SystemRepairStatusText.Text = "Assessment cancelled.";
            AppendSystemRepairTerminal("\n<<< Assessment Cancelled.");
        }
        catch (Exception ex)
        {
            SystemRepairStatusText.Text = $"Error: {ex.Message}";
            AppendSystemRepairTerminal($"\n[ERROR] {ex.Message}");
        }
        finally
        {
            SetSystemRepairRunning(false, "Quick Assessment");
            _systemRepairCts?.Dispose();
            _systemRepairCts = null;
        }
    }

    private static void UpdateSubsystemPill(System.Windows.Shapes.Ellipse indicator, TextBlock statusText, bool healthy, string details)
    {
        string color = healthy ? "#10B981" : "#EF4444";
        if (new BrushConverter().ConvertFromString(color) is Brush brush)
        {
            indicator.Fill = brush;
            statusText.Text = details;
            statusText.Foreground = brush;
        }
    }

    private async void RunAutonomousRepair_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("Autonomous System Repair", async (onProgress, log, ct) =>
        {
            var res = await SystemRepairService.RunAutonomousHealthCheckAndRepairAsync(log, onProgress, ct);
            if (res.Success)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateSubsystemPill(ComponentStoreIndicator, ComponentStoreStatusText, true, "DISM Serviced");
                    UpdateSubsystemPill(SystemFilesIndicator, SystemFilesStatusText, true, "SFC Protected");
                    UpdateSubsystemPill(FilesystemIndicator, FilesystemStatusText, true, "Filesystem Verified");
                    UpdateSubsystemPill(ServicingStackIndicator, ServicingStackStatusText, true, "Stack Active");
                    SubsystemsOverallRatingText.Text = "Remediated (100% Healthy)";
                    if (new BrushConverter().ConvertFromString("#10B981") is Brush green)
                    {
                        SubsystemsOverallRatingText.Foreground = green;
                        RemediationVerdictIcon.Foreground = green;
                        RemediationVerdictTitle.Foreground = green;
                    }

                    ExecutiveRemediationCard.Visibility = Visibility.Visible;
                    if (new BrushConverter().ConvertFromString("#142B20") is Brush bg) ExecutiveRemediationCard.Background = bg;
                    if (new BrushConverter().ConvertFromString("#3D10B981") is Brush border) ExecutiveRemediationCard.BorderBrush = border;
                    RemediationVerdictIcon.Text = "\uE73E";
                    RemediationVerdictTitle.Text = "Autonomous Remediation Succeeded";
                    RemediationVerdictDetails.Text = "All DISM manifests, protected system files, and volumes have been verified and restored.";
                });
            }
            return res;
        });
    }

    private async void RunSfcScannow_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("System File Checker (SFC)", (onProgress, log, ct) =>
        {
            return SystemRepairService.RunSfcScannowAsync(log, onProgress, ct);
        });
    }

    private async void RunDismRestoreHealth_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("DISM RestoreHealth", (onProgress, log, ct) =>
        {
            return SystemRepairService.RunDismRestoreHealthAsync(log, onProgress, ct);
        });
    }

    private async void RunWinSxSCleanup_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("WinSxS Component Store Scavenging", (onProgress, log, ct) =>
        {
            return SystemRepairService.RunDismComponentCleanupAsync(log, onProgress, ct);
        });
    }

    private async void RunChkdskScan_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("Filesystem Integrity (CHKDSK C:)", (onProgress, log, ct) =>
        {
            return SystemRepairService.RunChkdskScanAsync("C:", log, onProgress, ct);
        });
    }

    private async void RunWindowsUpdateReset_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("Windows Update Servicing Stack Reset", (onProgress, log, ct) =>
        {
            return SystemRepairService.ResetWindowsUpdateStackAsync(log, onProgress, ct);
        });
    }

    private async void RunNetworkReset_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("Network & Winsock Stack Reset", (onProgress, log, ct) =>
        {
            return SystemRepairService.ResetNetworkStackAsync(log, onProgress, ct);
        });
    }

    private void RunChrisTitusWinUtil_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        AppendSystemRepairTerminal($"\n>>> [{DateTime.Now:HH:mm:ss}] Launching Chris Titus Tech Windows Utility (CTT WinUtil)...");
        LogRequested?.Invoke("[CTT WinUtil] Launching Chris Titus Tech Windows Utility in elevated PowerShell...", LogLevel.Info);

        bool launched = SystemRepairService.LaunchChrisTitusWinUtil(out string error);
        if (launched)
        {
            AppendSystemRepairTerminal($"<<< [{DateTime.Now:HH:mm:ss}] Chris Titus Tech WinUtil launched in elevated PowerShell window.");
            LogRequested?.Invoke("[CTT WinUtil] Chris Titus Tech WinUtil launched successfully.", LogLevel.Success);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            AppendSystemRepairTerminal($"[ERROR] Failed to launch CTT WinUtil: {error}");
            LogRequested?.Invoke($"[CTT WinUtil] {error}", LogLevel.Warning);
        }
    }

    private async Task ExecuteRepairActionAsync(string opName, Func<Action<double>, Action<string>, CancellationToken, Task<RepairExecutionResult>> action)
    {
        if (_isSystemRepairRunning) return;
        SoundService.PlayClickSound();

        _systemRepairCts = new CancellationTokenSource();
        SetSystemRepairRunning(true, opName);
        SystemRepairProgressBar.Value = 0;
        SystemRepairProgressPercentText.Text = "0%";
        AppendSystemRepairTerminal($"\n>>> [{DateTime.Now:HH:mm:ss}] Starting {opName}...");

        void OnProgress(double val)
        {
            Dispatcher.Invoke(() =>
            {
                double pct = val <= 1.0 ? val * 100 : val;
                SystemRepairProgressBar.Value = Math.Min(100, Math.Max(0, pct));
                SystemRepairProgressPercentText.Text = $"{Math.Round(SystemRepairProgressBar.Value)}%";
            });
        }

        try
        {
            var result = await action(OnProgress, AppendSystemRepairTerminal, _systemRepairCts.Token);

            SystemRepairProgressBar.Value = 100;
            SystemRepairProgressPercentText.Text = "100%";
            SystemRepairStatusText.Text = result.Message;

            var level = result.Success ? LogLevel.Success : LogLevel.Warning;
            LogRequested?.Invoke($"[System Repair] {result.Message}", level);
            AppendSystemRepairTerminal($"\n<<< [{DateTime.Now:HH:mm:ss}] {opName} Finished. ExitCode: {result.ExitCode}. Success: {result.Success}");

            if (result.Success)
            {
                SoundService.PlaySuccessSound();
            }
        }
        catch (OperationCanceledException)
        {
            SystemRepairStatusText.Text = $"{opName} was cancelled.";
            AppendSystemRepairTerminal($"\n<<< [{DateTime.Now:HH:mm:ss}] {opName} Cancelled.");
            LogRequested?.Invoke($"[System Repair] {opName} cancelled.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            SystemRepairStatusText.Text = $"Error: {ex.Message}";
            AppendSystemRepairTerminal($"\n[ERROR] Exception occurred: {ex.Message}");
            LogRequested?.Invoke($"[System Repair Error] {ex.Message}", LogLevel.Error);
        }
        finally
        {
            SetSystemRepairRunning(false, opName);
            _systemRepairCts?.Dispose();
            _systemRepairCts = null;
        }
    }

    private async void RefreshRestorePoints_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        await LoadRestorePointsAsync();
    }

    private async Task LoadRestorePointsAsync()
    {
        if (RefreshRestorePointsBtn != null) RefreshRestorePointsBtn.IsEnabled = false;
        try
        {
            var info = await RestorePointManagerService.QueryDetailedRestorePointsAsync();
            if (RestorePointsBadgeText != null)
            {
                RestorePointsBadgeText.Text = $"{info.SnapshotCount} snapshot{(info.SnapshotCount == 1 ? "" : "s")}";
            }
            if (RestorePointsStorageSummaryText != null)
            {
                RestorePointsStorageSummaryText.Text = info.SnapshotCount > 0
                    ? $"Shadow storage active: {info.FormattedUsed} allocated across {info.SnapshotCount} restore points."
                    : "No active restore points found or VSS shadow storage is unallocated.";
            }

            if (RestorePointsItemsControl != null)
            {
                RestorePointsItemsControl.ItemsSource = info.Points;
            }
            if (RestorePointsListBorder != null)
            {
                RestorePointsListBorder.Visibility = info.Points.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (PruneRestorePointsBtn != null)
            {
                PruneRestorePointsBtn.IsEnabled = info.SnapshotCount > 1;
            }
        }
        catch (Exception ex)
        {
            AppendSystemRepairTerminal($"[RestorePoints] Query error: {ex.Message}");
        }
        finally
        {
            if (RefreshRestorePointsBtn != null) RefreshRestorePointsBtn.IsEnabled = true;
        }
    }

    private async void PruneRestorePoints_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Safely prune legacy VSS restore points?\n\nThis will purge older snapshots while retaining the latest restore point intact for safety.",
            "Prune Older Restore Points",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        SoundService.PlayClickSound();
        if (PruneRestorePointsBtn != null) PruneRestorePointsBtn.IsEnabled = false;

        AppendSystemRepairTerminal("\n>>> Starting VSS Restore Point Pruning (retaining newest)...");
        try
        {
            var (success, reclaimed, message) = await RestorePointManagerService.PruneOlderRestorePointsAsync((msg, lvl) =>
            {
                Dispatcher.Invoke(() => AppendSystemRepairTerminal(msg));
            });

            if (success)
            {
                SoundService.PlaySuccessSound();
                AppendSystemRepairTerminal($"<<< VSS Pruning Finished. Reclaimed: {TargetFolderInfo.FormatBytes(reclaimed)}.");
                LogRequested?.Invoke($"[Restore Points] {message}", LogLevel.Success);
            }
            else
            {
                AppendSystemRepairTerminal($"<<< VSS Pruning: {message}");
                LogRequested?.Invoke($"[Restore Points] {message}", LogLevel.Warning);
            }

            await LoadRestorePointsAsync();
        }
        finally
        {
            if (PruneRestorePointsBtn != null) PruneRestorePointsBtn.IsEnabled = true;
        }
    }
}
