using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of the Cleaner workspace.
///
/// This is a WORKSPACE panel: it fills the content column for the "Cleaner
/// Workspace" nav item. The scan is read-only; cleaning is gated behind an
/// injected confirmation that defaults to refusing.
///
/// Self-test policy: ScanAllAsync only enumerates and measures files. Clean
/// (CleanSelectedAsync) is never invoked by the self-test. A bare ViewModel
/// is used, so even an accidental mutating call fails closed.
/// </summary>
public sealed partial class CleanerWorkspaceView : UserControl
{
    public CleanerWorkspaceView()
    {
        ViewModel = new CleanerWorkspaceViewModel(
            confirmAsync: AskForConfirmationAsync,
            post: action => DispatcherQueue.TryEnqueue(() => action()));
        ViewModel.LogRequested += OnLogRequested;
        InitializeComponent();
    }

    public CleanerWorkspaceViewModel ViewModel { get; }

    /// <summary>
    /// Raised when the hero's AppData Report button is pressed. The shell owns the
    /// footprint overlay, mirroring the WPF hero button
    /// (MainWindow.Cleaning.cs HeroAppDataReportBtn_Click, which opens
    /// AppDataFootprintModalOverlay). The report is read-only and deliberately does
    /// not block a scan or a clean in flight.
    /// </summary>
    public event Action? FootprintRequested;

    /// <summary>
    /// Raised when the dock's Activity Log button is pressed. The stream itself lives
    /// in the shell, because every workspace writes to it - this view only owns the
    /// control that opens it.
    /// </summary>
    public event Action? ActivityLogToggleRequested;

    /// <summary>Read-only scan of all target categories. Safe to call from the self-test.</summary>
    public async void Open()
    {
        await ViewModel.ScanAllAsync();
    }

    /// <summary>
    /// Re-labels everything this panel owns after a language switch: the scanned target
    /// categories (LocalizationService.LocalizeTarget), the idle status line, and the
    /// dock's state-dependent Activity Log / Hide Log pair. Static copy is handled by
    /// the shell's attached-key walk; this is only what a key cannot express.
    /// </summary>
    public void RefreshLocalization(bool activityLogVisible)
    {
        ViewModel.ApplyLocalization();
        SetLogState(activityLogVisible, LogToggleBadge.Visibility == Visibility.Visible);
    }

    /// <summary>
    /// Mirrors the drawer's state onto the dock button: label, chevron and the unseen
    /// dot. WPF's ToggleLogBtn_Click did the same three things by hand
    /// (E70D/chevron-up + "Hide Log" while open, E756 + "Activity Log" while closed,
    /// badge collapsed on open).
    /// </summary>
    public void SetLogState(bool visible, bool unseen)
    {
        LogToggleIcon.Glyph = visible ? "\uE70D" : "\uE756";
        LogToggleText.Text = LocalizationService.Get(visible ? "HideLog" : "ActivityLog");
        LogToggleBadge.Visibility = visible || !unseen
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnToggleActivityLogClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        ActivityLogToggleRequested?.Invoke();
    }

    /// <summary>
    /// Layout probe for the self-test: how wide the header's two rows want to be at a
    /// given content width.
    ///
    /// The stats row cannot clip silently - if it wants more than the content column
    /// the self-test has to say so. The action strip is scrollable by design (WPF's
    /// hero strip scrolls too), so its number only states whether a user at the
    /// minimum window width sees a scrollbar. Measures only; nothing is arranged.
    /// </summary>
    internal (double Stats, double Actions) MeasureHeaderAt(double contentWidth)
    {
        CleanerHeader.Measure(new Windows.Foundation.Size(contentWidth, double.PositiveInfinity));

        // Direct unconstrained measures: a ScrollViewer feeds its content the viewport
        // constraint, so the strip's DesiredSize from the header measure can be the
        // clipped width, not the natural one the fit claim needs.
        CleanerHeaderStats.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        CleanerHeaderActions.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        return (CleanerHeaderStats.DesiredSize.Width, CleanerHeaderActions.DesiredSize.Width);
    }

    /// <summary>
    /// Raised with the row whose "Inspect" pill was pressed. The shell owns the
    /// overlay, the way it owns every other modal surface, and shows it.
    /// </summary>
    public event Action<TargetFolderInfo>? TopFilesRequested;

    private void OnInspectTargetClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is FrameworkElement { DataContext: TargetFolderInfo target })
        {
            TopFilesRequested?.Invoke(target);
        }
    }

    // Row buttons cannot reach the page's commands through an ElementName
    // {Binding} - WinUI 3 does not resolve the path from inside a DataTemplate
    // (the template owns its own namescope, where "Root" is invisible), so the
    // binding silently failed. The click is handled here, same as Inspect above.
    private void OnRowRetryClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is FrameworkElement { DataContext: TargetFolderInfo target })
        {
            ViewModel.RetryTargetCommand.Execute(target);
        }
    }

    private void OnFootprintRequested(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        ViewModel.Log("Opened the AppData footprint report (read only - nothing is deleted)", LogLevel.Info);
        FootprintRequested?.Invoke();
    }

    /// <summary>
    /// Read-only health report for every storage device connected to the PC, one row
    /// per physical disk (its volume letters are shown on the row). Rows come from
    /// DriveHealthService (Windows Storage WMI); nothing is written.
    /// </summary>
    private async void OnDrivesHealthClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_reportDialogOpen) return;

        try
        {
            var drives = await Task.Run(DriveHealthService.GetAllDriveHealth);
            ViewModel.Log(
                $"[Drive Health] Opened the report for {drives.Count} storage device(s) (read only).",
                LogLevel.Info);

            await ShowDriveReportAsync("Drives Health", BuildHealthBody(drives));
        }
        catch (Exception ex)
        {
            ViewModel.Log($"Drive health report failed: {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// Real sequential write + unbuffered read on every storage device (each device
    /// once, through its roomiest volume), then a report of the measured numbers.
    /// The ViewModel serializes runs through IsBenchmarking; the button is also
    /// disabled for the duration.
    /// </summary>
    private async void OnSpeedTestClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (ViewModel.IsBenchmarking || _reportDialogOpen) return;

        var results = await ViewModel.RunDriveBenchmarkAsync();
        if (results.Count == 0)
        {
            ViewModel.Log("Drive speed test produced no results.", LogLevel.Warning);
            return;
        }

        await ShowDriveReportAsync("Drive Speed Test", BuildSpeedBody(results));
    }

    private bool _reportDialogOpen;

    private async Task ShowDriveReportAsync(string title, UIElement body)
    {
        _reportDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = new ScrollViewer { Content = body, MaxHeight = 460 },
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ViewModel.Log($"Drive report dialog failed: {ex.Message}", LogLevel.Warning);
        }
        finally
        {
            _reportDialogOpen = false;
        }
    }

    private static StackPanel BuildHealthBody(IReadOnlyList<DriveHealthInfo> drives)
    {
        var panel = new StackPanel { Spacing = 10 };

        if (drives.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No storage devices were found.",
                TextWrapping = TextWrapping.Wrap,
            });
            return panel;
        }

        panel.Children.Add(new TextBlock
        {
            Text = "One row per physical device: a disk with C: and D: is a single row. "
                + "Health, model, media type and serial come from the Windows Storage provider. "
                + "Temperature, wear and power-on hours need elevation; they read 'Not reported' "
                + "rather than a guess when unavailable.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 12,
        });

        foreach (var drive in drives)
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock
            {
                Text = $"{drive.DisplayName} - {drive.HealthStatus}"
                    + (drive.HealthScore.HasValue ? $" ({drive.HealthScore.Value}%)" : string.Empty),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{drive.MediaType} · {drive.BusType} · {drive.FormattedCapacity}",
                Opacity = 0.8,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            row.Children.Add(new TextBlock
            {
                Text = $"Temp {drive.FormattedTemperature} · Wear {drive.FormattedWear} · "
                    + $"Power-on {drive.FormattedPowerOnHours} · {drive.FormattedFreeSpace}",
                Opacity = 0.8,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            row.Children.Add(new TextBlock
            {
                Text = drive.Recommendation
                    + (drive.ReliabilityCountersAvailable ? string.Empty : " Reliability counters need elevation."),
                Opacity = 0.7,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(row);
        }

        return panel;
    }

    private static StackPanel BuildSpeedBody(IReadOnlyList<DriveBenchmarkResult> results)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Each storage device is tested once, one at a time: a temporary file is written with "
                + "write-through, read back with unbuffered I/O, then deleted.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 12,
        });

        foreach (var result in results)
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock
            {
                Text = result.Success
                    ? $"{result.DriveName} - read {result.FormattedReadSpeed}, write {result.FormattedWriteSpeed}"
                    : $"{result.DriveName} - could not be tested",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            row.Children.Add(new TextBlock
            {
                Text = result.Success
                    ? $"{result.MediaType} · {result.FormattedTestSize} test file · tested on {result.DriveLetter} · "
                      + $"{result.ReadPasses} read pass(es) · {result.ElapsedMs:N0} ms"
                    : result.ErrorMessage ?? "This device could not be tested.",
                Opacity = 0.8,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(row);
        }

        return panel;
    }

    /// <summary>
    /// Raised when the drive card's "Large Files" pill is pressed (WPF's
    /// OpenLargeFilesModal_Click, MainWindow.xaml:770). The shell owns the section
    /// map, so the pill only asks to be taken to the Large Files workspace.
    /// </summary>
    public event Action? LargeFilesRequested;

    private void OnOpenLargeFilesClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        LargeFilesRequested?.Invoke();
    }

    /// <summary>
    /// Raised when the RAM card's "RAM Cleaner" pill is pressed (WPF's
    /// OpenMemoryCleanerModal_Click, MainWindow.Memory.cs:41). The shell owns the
    /// section map, so the pill only asks to be taken to the Memory workspace.
    /// </summary>
    public event Action? MemoryCleanerRequested;

    private void OnMemoryCleanerClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        MemoryCleanerRequested?.Invoke();
    }

    /// <summary>Engage / restore the Ultimate Performance plan. Mutating, never self-test.</summary>
    private void OnGameBoostClick(object sender, RoutedEventArgs e) => _ = ViewModel.ToggleGameBoostAsync();

    /// <summary>Launches the third-party CTT WinUtil. Mutating, never self-test.</summary>
    private void OnWinUtilClick(object sender, RoutedEventArgs e) => ViewModel.LaunchWinUtil();

    private void OnSafeOnly(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSafeCommand.Execute(null);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAllCommand.Execute(null);
    }

    private void OnDeselectAll(object sender, RoutedEventArgs e)
    {
        ViewModel.DeselectAllCommand.Execute(null);
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            // The filter is applied by the ViewModel through the shared
            // CleaningPipelineViewModel.MatchesFilter predicate, so the chip and the
            // list cannot disagree.
            ViewModel.FilterTag = tag;
            SoundService.PlayClickSound();
        }
    }

    private void OnLogRequested(string message, LogLevel level)
        => ShellDiagnostics.Write($"[{level}] {message}");

    /// <summary>The blocking yes/no prompt for destructive clean. Cancel is the default so a stray Enter cannot clean.</summary>
    private async Task<bool> AskForConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                FontSize = 13,
            },
            PrimaryButtonText = "Yes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
