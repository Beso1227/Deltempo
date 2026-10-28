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

    private void OnFootprintRequested(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        ViewModel.Log("Opened the AppData footprint report (read only - nothing is deleted)", LogLevel.Info);
        FootprintRequested?.Invoke();
    }

    /// <summary>
    /// Non-destructive read benchmark on the OS drive. The ViewModel serializes runs
    /// through its IsBenchmarking latch; the button is also disabled for the duration.
    /// </summary>
    private void OnSpeedTestClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        _ = ViewModel.RunDriveBenchmarkAsync();
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
