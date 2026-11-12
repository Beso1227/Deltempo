using System.Windows;
using System.Windows.Controls;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Models;

namespace WinTempCleaner;

// The post-clean celebration's "these applications are holding files open" notice.
public partial class MainWindow
{
    /// <summary>
    /// Names the applications that are holding cleanup files open, so the user knows
    /// what to close before running the clean again.
    /// </summary>
    /// <remarks>
    /// The run already knew the answer - every in-use file carries the holder's name
    /// on its audit record - but the celebration only printed a COUNT ("77 in use"),
    /// which leaves the user with a number and nothing to do about it. The In-Use
    /// inspector's own process-name parser also no longer matched the reason string
    /// the executor writes, so the application names never reached the screen at all.
    /// </remarks>
    private void ShowInUseApps(int totalFilesInUse)
    {
        if (totalFilesInUse <= 0)
        {
            CelebrationInUsePanel.Visibility = Visibility.Collapsed;
            CelebrationInUseAppsList.ItemsSource = null;
            return;
        }

        List<InUseApp> apps = LockedFileCollector.AggregateByApplication(
            LockedFileCollector.FromTargets(_targets));

        if (apps.Count == 0)
        {
            // The count is known but the holder is not: say so honestly instead of
            // inventing a name.
            CelebrationInUseHeadlineText.Text = $"{totalFilesInUse:N0} files are still in use.";
            CelebrationInUseHintText.Text =
                "An application is holding them open, but Windows did not name it. " +
                "Close whatever is using those folders and run the clean again.";
            CelebrationInUseAppsList.ItemsSource = null;
            CelebrationInUsePanel.Visibility = Visibility.Visible;
            return;
        }

        int appCount = apps.Count;
        CelebrationInUseHeadlineText.Text =
            $"{totalFilesInUse:N0} file{(totalFilesInUse == 1 ? string.Empty : "s")} could not be deleted - " +
            $"{appCount} application{(appCount == 1 ? " is" : "s are")} holding them open.";
        CelebrationInUseHintText.Text =
            "Close the application(s) below and run the clean again - the files will be deleted.";
        CelebrationInUseAppsList.ItemsSource = apps;
        CelebrationInUsePanel.Visibility = Visibility.Visible;
    }
}
