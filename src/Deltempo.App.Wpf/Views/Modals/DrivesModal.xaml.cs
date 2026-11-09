using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

/// <summary>
/// READ-ONLY report for every storage device connected to this machine.
///
/// One row is one physical device (SSD, HDD, USB stick, SD card), not one volume:
/// a disk split into C: and D: is a single row labelled with both letters.
///
/// Two modes share one surface:
///   * Health - what Windows can prove about each physical disk (model, media,
///     bus, HealthStatus, and the reliability counters when the provider allows).
///   * Speed Test - a real sequential write plus unbuffered read on every device,
///     one at a time, through its roomiest volume.
///
/// Nothing here deletes anything. The speed test writes one temporary file per
/// device, reads it back with FILE_FLAG_NO_BUFFERING and always removes it again.
/// When the provider denies a fact (temperature, wear) the row says "Not
/// reported" instead of inventing a number.
/// </summary>
public partial class DrivesModal : UserControl
{
    private readonly ObservableCollection<DriveRow> _healthRows = new();
    private readonly ObservableCollection<DriveSpeedRow> _speedRows = new();
    private CancellationTokenSource? _benchmarkCts;
    private DriveReportMode _mode = DriveReportMode.Health;
    private bool _loadingHealth;
    private bool _benchmarking;

    public event Action<string, LogLevel>? LogRequested;
    public event Action? Closed;

    public DrivesModal()
    {
        InitializeComponent();
        HealthItemsControl.ItemsSource = _healthRows;
        SpeedItemsControl.ItemsSource = _speedRows;
    }

    public DriveReportMode Mode => _mode;

    /// <summary>Opens the report in the requested mode and refreshes the data.</summary>
    public void Open(DriveReportMode mode)
    {
        _mode = mode;
        Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        ApplyModeChrome();
        _ = LoadHealthAsync();

        if (mode == DriveReportMode.SpeedTest)
        {
            _ = RunSpeedTestAsync();
        }
    }

    public void CloseModal()
    {
        _benchmarkCts?.Cancel();
        Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        Closed?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseModal();

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == DriveReportMode.Health)
        {
            _mode = DriveReportMode.SpeedTest;
            ApplyModeChrome();
            _ = RunSpeedTestAsync();
            return;
        }

        if (_benchmarking)
        {
            _benchmarkCts?.Cancel();
            return;
        }

        _ = RunSpeedTestAsync();
    }

    private void ApplyModeChrome()
    {
        bool speed = _mode == DriveReportMode.SpeedTest;

        TitleText.Text = speed ? "Drive Speed Test" : "Drives Health";
        SubtitleText.Text = speed
            ? "Real sequential write + read on every storage device"
            : "Every storage device connected to this PC";

        HealthScroll.Visibility = speed ? Visibility.Collapsed : Visibility.Visible;
        SpeedScroll.Visibility = speed ? Visibility.Visible : Visibility.Collapsed;

        SummaryText.Text = speed
            ? "Every storage device is tested once, one at a time, with a temporary file, so one device's I/O cannot distort another's result."
            : string.IsNullOrEmpty(SummaryText.Text) || SummaryText.Text.StartsWith("Reading", StringComparison.Ordinal)
                ? "Reading storage device health from the Windows Storage provider..."
                : SummaryText.Text;

        ActionBtn.Content = speed ? "Re-run Speed Test" : "Run Speed Test";
        ActionBtn.IsEnabled = true;
        FooterText.Text = speed
            ? "The temporary test file is deleted on every path, including cancel."
            : "Health is read-only. Nothing is written or deleted.";
    }

    private async Task LoadHealthAsync()
    {
        if (_loadingHealth) return;
        _loadingHealth = true;

        try
        {
            var drives = await Task.Run(DriveHealthService.GetAllDriveHealth);

            _healthRows.Clear();
            foreach (var drive in drives)
            {
                _healthRows.Add(BuildHealthRow(drive));
            }

            if (drives.Count == 0)
            {
                SummaryText.Text = "No storage devices were found.";
                return;
            }

            int unknown = drives.Count(d => !d.HealthScore.HasValue);
            SummaryText.Text = unknown == 0
                ? $"{drives.Count} storage device{(drives.Count == 1 ? string.Empty : "s")} reported by Windows Storage"
                : $"{drives.Count} storage device{(drives.Count == 1 ? string.Empty : "s")} found, {unknown} without a health verdict";

            foreach (var drive in drives)
            {
                if (drive.HealthStatus is "Warning" or "Unhealthy")
                {
                    LogRequested?.Invoke(
                        $"[Drive Health] {drive.DisplayName} reports {drive.HealthStatus}.",
                        LogLevel.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            SummaryText.Text = "Drive health could not be read.";
            FooterText.Text = ex.Message;
            LogRequested?.Invoke($"[Drive Health Error] {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _loadingHealth = false;
        }
    }

    private async Task RunSpeedTestAsync()
    {
        if (_benchmarking) return;

        _benchmarking = true;
        _benchmarkCts?.Dispose();
        _benchmarkCts = new CancellationTokenSource();
        var ct = _benchmarkCts.Token;

        _speedRows.Clear();
        StatusText.Text = "Testing every storage device, one at a time...";
        ActionBtn.Content = "Cancel";
        ActionBtn.IsEnabled = true;

        try
        {
            var results = await DriveHealthService.RunBenchmarkForAllDrivesAsync(
                options: null,
                onDriveCompleted: result => Dispatcher.Invoke(() =>
                {
                    _speedRows.Add(BuildSpeedRow(result));
                    StatusText.Text = $"Measured {_speedRows.Count} storage device{(_speedRows.Count == 1 ? string.Empty : "s")}...";

                    LogRequested?.Invoke(
                        result.Success
                            ? $"[Drive Speed] {result.DriveName} ({result.MediaType}): read {result.FormattedReadSpeed}, write {result.FormattedWriteSpeed} on {result.DriveLetter} over {result.FormattedTestSize}."
                            : $"[Drive Speed] {result.DriveName} could not be tested: {result.ErrorMessage}",
                        result.Success ? LogLevel.Info : LogLevel.Warning);
                }),
                ct: ct);

            int ok = results.Count(r => r.Success);
            StatusText.Text = results.Count == 0
                ? "No storage devices were found."
                : $"Completed: {ok}/{results.Count} storage devices measured.";

            if (ok > 0 && ok == results.Count)
            {
                SoundService.PlaySuccessSound();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Speed test cancelled. Temporary files were removed.";
            LogRequested?.Invoke("[Drive Speed] Cancelled by the user.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            StatusText.Text = "The speed test failed.";
            LogRequested?.Invoke($"[Drive Speed Error] {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _benchmarking = false;
            ActionBtn.Content = "Re-run Speed Test";
            ActionBtn.IsEnabled = true;
        }
    }

    private static DriveRow BuildHealthRow(DriveHealthInfo drive)
    {
        string subtitle = string.Join("  ·  ", new[]
        {
            drive.HealthStatus,
            drive.MediaType,
            drive.BusType,
            drive.IsSystemDrive ? "system" : null
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        string detail = drive.Recommendation;
        if (!drive.ReliabilityCountersAvailable)
        {
            detail += " Reliability counters need elevation.";
        }

        return new DriveRow(
            drive.DisplayName,
            subtitle,
            detail,
            drive.HealthScore.HasValue ? $"{drive.HealthScore.Value}%" : "Unknown",
            ResolveBrush(drive.HealthColor),
            $"Temp {drive.FormattedTemperature}",
            $"Wear {drive.FormattedWear}",
            drive.FormattedFreeSpace);
    }

    private static DriveSpeedRow BuildSpeedRow(DriveBenchmarkResult result)
    {
        string subtitle = result.Success
            ? $"{result.MediaType}  ·  {result.FormattedTestSize} test file  ·  tested on {result.DriveLetter}  ·  {result.ElapsedMs:N0} ms"
            : result.MediaType;

        string detail = result.Success
            ? $"Read is the best of {result.ReadPasses} unbuffered pass{(result.ReadPasses == 1 ? string.Empty : "es")}; the file is deleted afterwards."
            : result.ErrorMessage ?? "This device could not be tested.";

        return new DriveSpeedRow(
            result.DriveName,
            subtitle,
            detail,
            result.Success ? result.FormattedReadSpeed : "Failed",
            ResolveBrush(result.Success ? "#10B981" : "#EF4444"),
            result.FormattedWriteSpeed);
    }

    private static Brush ResolveBrush(string hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    /// <summary>Presentation-only row for the health report. One-way data.</summary>
    private sealed record DriveRow(
        string Title,
        string Subtitle,
        string Detail,
        string Status,
        Brush StatusBrush,
        string Metric1,
        string Metric2,
        string Metric3);

    /// <summary>Presentation-only row for the speed report. One-way data.</summary>
    private sealed record DriveSpeedRow(
        string Title,
        string Subtitle,
        string Detail,
        string ReadSpeed,
        Brush StatusBrush,
        string WriteSpeed);
}
