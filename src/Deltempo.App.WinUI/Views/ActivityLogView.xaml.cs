using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the activity log stream.
///
/// The shell owns the collection and the clipboard, so this view can be opened and
/// closed without losing the history, and Clear is a deliberate click on the same
/// collection the rest of the shell writes to. Nothing here deletes or measures
/// anything, which is why the self-test can show it safely.
/// </summary>
public sealed partial class ActivityLogView : UserControl
{
    public ActivityLogView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user asks for the log on the clipboard.</summary>
    public event Action? CopyRequested;

    /// <summary>Raised when the user asks to empty the stream.</summary>
    public event Action? ClearRequested;

    /// <summary>
    /// Binds the stream. Passing the shell's observable collection (rather than a copy)
    /// is what lets a line appended by a background scan appear here immediately.
    /// </summary>
    public void Bind(System.Collections.IEnumerable entries) => LogItems.ItemsSource = entries;

    /// <summary>
    /// Scrolls to the newest line. Called when the drawer opens and, throttled, by the
    /// shell as entries arrive - WPF did the same through LogScrollViewer.ScrollToEnd().
    /// </summary>
    public void ScrollToEnd() => LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null, true);

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyRequested?.Invoke();

    private void OnClearClick(object sender, RoutedEventArgs e) => ClearRequested?.Invoke();
}
