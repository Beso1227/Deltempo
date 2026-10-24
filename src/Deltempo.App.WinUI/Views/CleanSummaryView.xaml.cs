using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    /// <summary>Fills the summary from a completed run.</summary>
    public void Show(CleanSummary summary, IReadOnlyList<TargetFolderInfo> targets, bool safeMode)
    {
        _summary = summary;
        _targets = targets;
        _safeMode = safeMode;

        var skipDetails = new List<string>();
        if (summary.TotalFilesInUse > 0) skipDetails.Add($"{summary.TotalFilesInUse:N0} in use");
        if (summary.TotalFilesRecentShielded > 0) skipDetails.Add($"{summary.TotalFilesRecentShielded:N0} shielded (under 24h old)");
        if (summary.TotalFilesPolicyProtected > 0) skipDetails.Add($"{summary.TotalFilesPolicyProtected:N0} policy protected");
        if (summary.TotalFilesFailed > 0) skipDetails.Add($"{summary.TotalFilesFailed:N0} failed");

        // WPF worded the title differently when something was held open, because
        // "Completed!" next to a skip count reads as a contradiction.
        TitleText.Text = skipDetails.Count > 0
            ? "Cleanup Completed (Some Files Kept)"
            : "Cleanup Completed!";

        ReclaimedText.Text = skipDetails.Count > 0
            ? $"Reclaimed {summary.FormattedFreedSize}"
            : $"Successfully Reclaimed {summary.FormattedFreedSize}";

        FilesText.Text = $"{summary.TotalFilesDeleted:N0}";
        FoldersText.Text = $"{summary.TotalFoldersDeleted:N0}";
        TimeText.Text = $"{summary.ElapsedTime.TotalSeconds:N1}s";

        SkipText.Text = skipDetails.Count > 0
            ? $"Kept: {string.Join(", ", skipDetails)}"
            : string.Empty;
        SkipText.Visibility = skipDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // A second run overwrites the previous outcome line, never stacks.
        StatusLine.Text = string.Empty;
        StatusLine.Visibility = Visibility.Collapsed;
        ExportButton.IsEnabled = true;
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
}
