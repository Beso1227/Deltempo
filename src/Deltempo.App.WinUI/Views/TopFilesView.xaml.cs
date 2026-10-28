using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the WPF InspectorModalOverlay, the per-category top-files
/// inspector WPF opened with the row's "Inspect" pill (InspectTarget_Click).
///
/// The DetailRowDataTemplate keeps inspecting read-only: nothing here deletes,
/// selects or schedules - WPF's modal was the same kind of pure peek.
/// </summary>
public sealed partial class TopFilesView : UserControl
{
    public TopFilesView()
    {
        InitializeComponent();
    }

    /// <summary>Wired so the shell can log the close alongside its other overlays.</summary>
    public event System.Action? CloseRequested;

    /// <summary>
    /// Mirrors the WPF InspectTarget_Click body: copy the target's TopFiles into the
    /// list, set the title/subtext and show. Called with rows collected by the last
    /// scan; nothing is re-walked on open.
    /// </summary>
    public void Open(TargetFolderInfo target)
    {
        IEnumerable<JunkFileItem> files = target.TopFiles ?? [];
        FilesList.ItemsSource = files.ToList();
        TitleText.Text = $"{target.Name} \u2014 Top Junk Files";
        SubtitleText.Text =
            $"Showing {files.Count()} largest files found in {target.FolderPath}";

        Visibility = Visibility.Visible;
        ShellMotion.Enter(this, riseDip: 14);
    }

    public void Close() => Visibility = Visibility.Collapsed;

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Visibility = Visibility.Collapsed;
        CloseRequested?.Invoke();
    }
}
