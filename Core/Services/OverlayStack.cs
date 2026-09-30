using System.Windows;
using System.Windows.Controls;

namespace WinTempCleaner.Services;

/// <summary>
/// Keeps stacked overlay surfaces in the order they were opened.
///
/// WPF paints later siblings of a <see cref="Panel"/> on top, so an overlay that opens without
/// any reordering can end up *beneath* a sibling that merely happens to be declared later in the
/// XAML. In the shell that meant opening one tool left it covering the next tab the user clicked,
/// which appeared to open "underneath" it.
///
/// Promoting the overlay that just became visible removes that dependency on declaration order,
/// so adding or moving a modal in the XAML can no longer change which surface is on top.
/// </summary>
public static class OverlayStack
{
    /// <summary>
    /// Moves <paramref name="overlay"/> above every sibling in its parent panel. No-op when the
    /// overlay has no <see cref="Panel"/> parent yet (e.g. not yet loaded into the visual tree).
    /// </summary>
    public static void RaiseToTop(FrameworkElement? overlay)
    {
        if (overlay?.Parent is not Panel parent) return;

        int top = 0;
        foreach (UIElement child in parent.Children)
        {
            if (ReferenceEquals(child, overlay)) continue;
            top = Math.Max(top, Panel.GetZIndex(child));
        }

        Panel.SetZIndex(overlay, top + 1);
    }

    /// <summary>
    /// Hooks <paramref name="overlay"/> so that every time it becomes visible it is promoted above
    /// its siblings. Idempotent and safe to call for overlays that may never be shown.
    /// </summary>
    public static void TrackNewestOnTop(FrameworkElement? overlay)
    {
        if (overlay == null) return;

        overlay.IsVisibleChanged += (_, _) =>
        {
            if (overlay.Visibility == Visibility.Visible) RaiseToTop(overlay);
        };
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is painted at or above every other visible sibling —
    /// i.e. nothing can visually cover it. Used to assert overlay stacking in tests.
    /// </summary>
    public static bool IsTopmost(FrameworkElement candidate)
    {
        if (candidate.Parent is not Panel parent) return true;

        int candidateZ = Panel.GetZIndex(candidate);
        foreach (UIElement child in parent.Children)
        {
            if (ReferenceEquals(child, candidate)) continue;
            if (child is FrameworkElement fe && fe.Visibility == Visibility.Visible &&
                Panel.GetZIndex(child) > candidateZ)
            {
                return false;
            }
        }

        return true;
    }
}