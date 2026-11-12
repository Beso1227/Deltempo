using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the post-clean summary.
///
/// Pure presentation over a <see cref="CleanSummary"/>: the numbers come from the run
/// the cleaner already completed, so this file can never disagree with what was
/// deleted. Export reuses <see cref="CleanerService.GenerateAuditReport"/> - the same
/// report the WPF app wrote - and writes it to the Desktop under the same file name
/// convention.
/// </summary>
public sealed partial class CleanSummaryView : UserControl
{
    private CleanSummary? _summary;
    private IReadOnlyList<TargetFolderInfo> _targets = [];
    private bool _safeMode;

    public CleanSummaryView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user dismisses the summary.</summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Raised when the run kept files because a process held them open and the user
    /// wants to inspect them. WPF did the same from its celebration modal; the shell
    /// owns the inspector, so the view only asks for it.
    /// </summary>
    public event Action? ViewLockedFilesRequested;

    /// <summary>Fills the summary from a completed run.</summary>
    public void Show(CleanSummary summary, IReadOnlyList<TargetFolderInfo> targets, bool safeMode)
    {
        ArgumentNullException.ThrowIfNull(summary);

        _summary = summary;
        _targets = targets;
        _safeMode = safeMode;

        var skipDetails = new List<string>();
        if (summary.TotalFilesInUse > 0) skipDetails.Add($"{summary.TotalFilesInUse:N0} in use");
        if (summary.TotalFilesRecentShielded > 0) skipDetails.Add($"{summary.TotalFilesRecentShielded:N0} shielded (under 24h old)");
        if (summary.TotalFilesPolicyProtected > 0) skipDetails.Add($"{summary.TotalFilesPolicyProtected:N0} policy protected");
        if (summary.TotalFilesFailed > 0) skipDetails.Add($"{summary.TotalFilesFailed:N0} failed");

        // WPF worded the title differently when something was held open, because
        // "Completed!" next to a skip count reads as a contradiction. The deep clean
        // had its own title ("1-Click Deep Clean Complete") on its celebration modal.
        TitleText.Text = summary.IsDeepClean
            ? "1-Click Deep Clean Complete"
            : skipDetails.Count > 0
                ? "Cleanup Completed (Some Files Kept)"
                : "Cleanup Completed!";

        ReclaimedText.Text = skipDetails.Count > 0
            ? $"Reclaimed {summary.FormattedFreedSize}"
            : $"Successfully Reclaimed {summary.FormattedFreedSize}";

        // A deep clean's RAM figure is the one number the disk stat cannot carry.
        RamText.Text = $"RAM freed: {summary.FormattedRamFreed}";
        RamText.Visibility = summary.IsDeepClean ? Visibility.Visible : Visibility.Collapsed;

        // The audit report lists what each category did. A deep clean walks its own
        // internal stages rather than the visible categories, so that report would be
        // a ledger of work that never happened; the button is hidden instead of
        // writing a false one.
        ExportButton.Visibility = summary.IsDeepClean ? Visibility.Collapsed : Visibility.Visible;

        // WPF showed "View Locked Files (N)" only when the run actually kept files. The
        // button is the way back to the skipped files, so an in-use count without it is
        // a dead end that also contradicts the inspector's own hint text.
        ViewLockedFilesButton.Content = $"View Locked Files ({summary.TotalFilesInUse:N0})";
        ViewLockedFilesButton.Visibility =
            summary.TotalFilesInUse > 0 ? Visibility.Visible : Visibility.Collapsed;

        FilesText.Text = $"{summary.TotalFilesDeleted:N0}";
        FoldersText.Text = $"{summary.TotalFoldersDeleted:N0}";
        // WPF's celebration RAM card (MainWindow.xaml:2249, Cleaning.cs:368/:510): the
        // deep clean's standby flush is the real RAM figure; a category clean keeps
        // the same "-- MB" placeholder WPF wrote there, since a disk-only run frees
        // no RAM. The card is always visible so the grid keeps WPF's 4-stat shape.
        RamStatText.Text = summary.IsDeepClean ? summary.FormattedRamFreed : "-- MB";
        TimeText.Text = $"{summary.ElapsedTime.TotalSeconds:N1}s";

        SkipText.Text = skipDetails.Count > 0
            ? $"Kept: {string.Join(", ", skipDetails)}"
            : string.Empty;
        SkipText.Visibility = skipDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowInUseApps(targets, summary);

        // A second run overwrites the previous outcome line, never stacks.
        StatusLine.Text = string.Empty;
        StatusLine.Visibility = Visibility.Collapsed;
        ExportButton.IsEnabled = true;
    }

    /// <summary>
    /// Names the applications that are holding cleanup files open, so the user knows
    /// what to close before running the clean again.
    /// </summary>
    /// <remarks>
    /// The run already knew the answer: every in-use file carries the holder's name
    /// on its audit record. It was never shown - the celebration printed a COUNT
    /// ("77 in use") and left the user with nothing to act on, and the In-Use
    /// inspector's own process-name parser no longer matched the reason string the
    /// executor writes, so even that view could not name the application.
    /// </remarks>
    private void ShowInUseApps(IReadOnlyList<TargetFolderInfo> targets, CleanSummary summary)
    {
        if (summary.TotalFilesInUse <= 0)
        {
            InUsePanel.Visibility = Visibility.Collapsed;
            InUseAppsList.ItemsSource = null;
            return;
        }

        List<LockedFileItem> lockedRows = LockedFileCollector.FromTargets(targets);
        List<InUseApp> apps = LockedFileCollector.AggregateByApplication(lockedRows);

        if (apps.Count == 0)
        {
            // The count is known but the holder is not: say so honestly instead of
            // inventing a name.
            InUseHeadlineText.Text = $"{summary.TotalFilesInUse:N0} files are still in use.";
            InUseHintText.Text =
                "An application is holding them open, but Windows did not name it. " +
                "Close whatever is using those folders and run the clean again.";
            InUseAppsList.ItemsSource = null;
            InUsePanel.Visibility = Visibility.Visible;
            return;
        }

        int appCount = apps.Count;
        InUseHeadlineText.Text =
            $"{summary.TotalFilesInUse:N0} file{(summary.TotalFilesInUse == 1 ? string.Empty : "s")} could not be deleted - " +
            $"{appCount} application{(appCount == 1 ? " is" : "s are")} holding them open.";
        InUseHintText.Text =
            "Close the application(s) below and run the clean again - the files will be deleted.";
        InUseAppsList.ItemsSource = apps;
        InUsePanel.Visibility = Visibility.Visible;
    }

    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        if (_summary is null)
        {
            return;
        }

        try
        {
            string report = CleanerService.GenerateAuditReport(_targets, _summary, _safeMode);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(desktop, $"Deltempo_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            File.WriteAllText(path, report);

            StatusLine.Text = $"Audit report saved to {path}";
            StatusLine.Visibility = Visibility.Visible;
            ExportButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            // Exporting is a convenience; a read-only Desktop must not take the app down
            // or hide the summary the user just earned.
            StatusLine.Text = $"Unable to export report: {ex.Message}";
            StatusLine.Visibility = Visibility.Visible;
        }
    }

    private void OnDoneClicked(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void OnViewLockedFilesClicked(object sender, RoutedEventArgs e)
        => ViewLockedFilesRequested?.Invoke();
}
