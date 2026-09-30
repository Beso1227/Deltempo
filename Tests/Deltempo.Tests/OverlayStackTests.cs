using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Regression tests for the top-bar overlay stacking bug.
///
/// Symptom: opening Force Delete left it covering every other surface, so the next top-bar button
/// the user clicked appeared to open *underneath* it. Cause: WPF paints later siblings of a Panel
/// on top, Force Delete is declared last in the shell XAML, and opening it never hid the others.
///
/// OverlayStack removes the dependency on declaration order by promoting whatever just became
/// visible, so this cannot regress when a modal is added or moved.
/// </summary>
public class OverlayStackTests
{
    /// <summary>
    /// WPF elements require an STA thread; xunit runs tests on an MTA pool thread. Each body runs
    /// on a dedicated STA thread and rethrows its failure on the original thread.
    /// </summary>
    private static void RunSta(Action body)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        failure?.Throw();
    }

    private static (Grid Root, FrameworkElement A, FrameworkElement B, FrameworkElement C) BuildStack()
    {
        // Mirrors the shell layout: three sibling overlays, Force Delete declared last.
        var root = new Grid();
        var a = new Border { Visibility = Visibility.Collapsed };
        var b = new Border { Visibility = Visibility.Collapsed };
        var c = new Border { Visibility = Visibility.Collapsed };

        root.Children.Add(a);
        root.Children.Add(b);
        root.Children.Add(c);

        return (root, a, b, c);
    }

    [Fact]
    public void RaiseToTop_LastDeclaredOverlay_GainsTopZIndex() => RunSta(() =>
    {
        var (root, a, b, c) = BuildStack();

        OverlayStack.RaiseToTop(c);

        Assert.True(OverlayStack.IsTopmost(c));
        Assert.True(Panel.GetZIndex(c) > Panel.GetZIndex(b));
        Assert.True(Panel.GetZIndex(c) > Panel.GetZIndex(a));
    });

    [Fact]
    public void RaiseToTop_OpeningAnotherOverlayAfterForceDelete_PutsItOnTop() => RunSta(() =>
    {
        // The exact reported flow: Force Delete opens, then the user clicks another tab.
        var (root, largeFiles, _, forceDelete) = BuildStack();

        forceDelete.Visibility = Visibility.Visible;
        OverlayStack.RaiseToTop(forceDelete);
        Assert.True(OverlayStack.IsTopmost(forceDelete));

        // The next surface opens underneath unless it is explicitly promoted.
        largeFiles.Visibility = Visibility.Visible;
        OverlayStack.RaiseToTop(largeFiles);

        Assert.True(OverlayStack.IsTopmost(largeFiles),
            "The newly opened overlay must be painted above Force Delete, not beneath it.");
        Assert.True(Panel.GetZIndex(largeFiles) > Panel.GetZIndex(forceDelete));
    });

    [Fact]
    public void RaiseToTop_DoesNotDisturbSiblingOrder() => RunSta(() =>
    {
        var (root, a, b, c) = BuildStack();

        OverlayStack.RaiseToTop(a);
        OverlayStack.RaiseToTop(b);
        OverlayStack.RaiseToTop(c);

        // Opening a, then b, then c must leave that exact stacking order.
        Assert.True(Panel.GetZIndex(c) > Panel.GetZIndex(b));
        Assert.True(Panel.GetZIndex(b) > Panel.GetZIndex(a));
    });

    [Fact]
    public void TrackNewestOnTop_VisibilityChange_PromotesAutomatically() => RunSta(() =>
    {
        // This is what the shell actually calls: promotion must happen on show, not only when
        // callers remember to invoke it.
        var (root, _, _, forceDelete) = BuildStack();
        OverlayStack.TrackNewestOnTop(forceDelete);

        forceDelete.Visibility = Visibility.Visible;

        Assert.True(OverlayStack.IsTopmost(forceDelete));
    });

    [Fact]
    public void TrackNewestOnTop_SubsequentOpen_BeatsForceDelete() => RunSta(() =>
    {
        var (root, largeFiles, _, forceDelete) = BuildStack();
        OverlayStack.TrackNewestOnTop(forceDelete);
        OverlayStack.TrackNewestOnTop(largeFiles);

        forceDelete.Visibility = Visibility.Visible;
        largeFiles.Visibility = Visibility.Visible;

        Assert.True(OverlayStack.IsTopmost(largeFiles));
    });

    [Fact]
    public void IsTopmost_ConsidersOnlyVisibleSiblings() => RunSta(() =>
    {
        var (root, a, b, c) = BuildStack();

        a.Visibility = Visibility.Visible;
        b.Visibility = Visibility.Visible;

        // 'c' is declared last but hidden, so it cannot be covering anything and is topmost
        // among the visible set by declaration order.
        Assert.True(OverlayStack.IsTopmost(b));
    });

    [Fact]
    public void RaiseToTop_NullOrUnparentedOverlay_IsSafe() => RunSta(() =>
    {
        // Called during construction before the visual tree is populated — must not throw.
        OverlayStack.RaiseToTop(null);
        OverlayStack.TrackNewestOnTop(null);
        OverlayStack.RaiseToTop(new Border());

        Assert.True(OverlayStack.IsTopmost(new Border()));
    });
}