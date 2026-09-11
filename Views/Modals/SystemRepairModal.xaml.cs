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

    private async void RunAutonomousRepair_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRepairActionAsync("Autonomous System Repair", (onProgress, log, ct) =>
        {
            return SystemRepairService.RunAutonomousHealthCheckAndRepairAsync(log, onProgress, ct);
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
}
