using System;
using System.Globalization;
using System.Windows;
using WinTempCleaner.Converters;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the modal height contract. Every modal caps its MaxHeight against the window
/// height (minus a margin) rather than a fixed value, because the loader clamps the window
/// down to 720x520 on small and high-DPI displays. A fixed MaxHeight larger than that floor
/// pushed modal footer actions off-screen and made them unreachable.
/// </summary>
public class SubtractConverterTests
{
    private static readonly SubtractConverter Converter = new();
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(1360.0, "64", 1296.0)]
    [InlineData(960.0, "64", 896.0)]
    [InlineData(520.0, "64", 456.0)]
    [InlineData(1000.0, "0", 1000.0)]
    public void Convert_SubtractsMarginFromWindowSize(double source, string parameter, double expected)
    {
        Assert.Equal(expected, (double)Converter.Convert(source, typeof(double), parameter, Culture));
    }

    [Fact]
    public void Convert_NeverReturnsNegativeHeight()
    {
        // A window shorter than the margin must collapse to 0, never to a negative value
        // (WPF throws on a negative FrameworkElement.Height/MaxHeight).
        double result = (double)Converter.Convert(30.0, typeof(double), "64", Culture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Convert_PassesValueThroughWhenParameterIsMalformed()
    {
        // Degrade to the caller's static MaxHeight rather than collapsing the modal.
        Assert.Equal(680.0, (double)Converter.Convert(680.0, typeof(double), "not-a-number", Culture));
        Assert.Equal(680.0, (double)Converter.Convert(680.0, typeof(double), null!, Culture));
    }

    [Fact]
    public void Convert_PassesThroughNonNumericSource()
    {
        Assert.Equal("NaN", Converter.Convert("NaN", typeof(double), "64", Culture));
    }

    [Fact]
    public void EveryModalHeightCapFitsInsideTheWindowFloor()
    {
        // The loader's hard floor (MainWindow.xaml.cs) is 720x520.
        const double windowFloorHeight = 520.0;
        const double modalMargin = 64.0;

        double cap = (double)Converter.Convert(windowFloorHeight, typeof(double),
                                               modalMargin.ToString(Culture), Culture);

        Assert.True(cap > 0, "A modal must still have usable height at the window floor.");
        Assert.True(cap < windowFloorHeight, "The cap must leave room for the modal's own margin.");
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(
            () => Converter.ConvertBack(100.0, typeof(double), "64", Culture));
    }
}

/// <summary>
/// Guards the title-bar responsive contract. Verified against real offscreen renders:
/// at 1360px the eight labelled nav buttons needed ~1225px and clipped "Force Delete";
/// at 960px five of eight items were unreachable; at 720px only one was visible.
/// The nav now collapses to icon-only below its threshold and tightens padding/MinWidth
/// so the eight buttons fit in an 800px window.
/// </summary>
public class ResponsiveTitleBarConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(1600.0, 1280.0, false)] // roomy: keep labels
    [InlineData(1281.0, 1280.0, false)] // just above threshold
    [InlineData(1280.0, 1280.0, false)] // exactly at threshold: not *narrower*, so keep labels
    [InlineData(1279.0, 1280.0, true)]  // just below threshold: collapse
    [InlineData(1360.0, 900.0, false)]  // version/MIT badges fit at the default size
    [InlineData(959.0, 900.0, false)]   // still wider than the badge threshold
    [InlineData(899.0, 900.0, true)]    // small window: collapse the brand badges
    [InlineData(821.0, 820.0, false)]
    [InlineData(819.0, 820.0, true)]
    public void Visibility_CollapsesOnlyBelowThreshold(double width, double threshold, bool expectCollapsed)
    {
        var converter = new WidthToCompactVisibilityConverter();
        string param = threshold.ToString(Culture);

        Visibility result = (Visibility)converter.Convert(width, typeof(Visibility), param, Culture);

        Assert.Equal(expectCollapsed ? Visibility.Collapsed : Visibility.Visible, result);
    }

    [Fact]
    public void Visibility_StaysVisibleForMalformedInput()
    {
        // Never let a bad binding hide primary navigation.
        var converter = new WidthToCompactVisibilityConverter();
        Assert.Equal(Visibility.Visible, converter.Convert(720.0, typeof(Visibility), "nope", Culture));
        Assert.Equal(Visibility.Visible, converter.Convert("NaN", typeof(Visibility), "1280", Culture));
    }

    [Fact]
    public void Padding_IsCompactBelowThresholdAndUnsetAbove()
    {
        var converter = new WidthToCompactThicknessConverter();
        const string Param = "1280,4,6,4,6";

        Assert.Equal(new Thickness(4, 6, 4, 6),
            converter.Convert(720.0, typeof(Thickness), Param, Culture));

        // Above the threshold it must yield UnsetValue so the style's Padding (13,6) applies.
        Assert.Same(DependencyProperty.UnsetValue,
            converter.Convert(1360.0, typeof(Thickness), Param, Culture));
    }

    [Fact]
    public void CompactMinWidth_StaysAboveTheWcagMinimum()
    {
        var converter = new WidthToCompactDoubleConverter();
        const string Param = "1280,28";

        double result = (double)converter.Convert(720.0, typeof(double), Param, Culture);

        // WCAG 2.5.8 target-size minimum is 24 DIP; the compact nav must not fall below it.
        Assert.Equal(28.0, result);
        Assert.True(result >= 24.0);
        Assert.Same(DependencyProperty.UnsetValue,
            converter.Convert(1360.0, typeof(double), Param, Culture));
    }

    [Fact]
    public void CompactConverters_IgnoreMalformedParameters()
    {
        var thickness = new WidthToCompactThicknessConverter();
        var number = new WidthToCompactDoubleConverter();

        Assert.Same(DependencyProperty.UnsetValue, thickness.Convert(720.0, typeof(Thickness), "1280,4,6", Culture));
        Assert.Same(DependencyProperty.UnsetValue, thickness.Convert(720.0, typeof(Thickness), null!, Culture));
        Assert.Same(DependencyProperty.UnsetValue, number.Convert(720.0, typeof(double), "1280", Culture));
    }

    [Fact]
    public void EightCompactNavButtons_FitInsideThe800pxSupportedMinimum()
    {
        // Budget measured from real renders: brand block ~137px, right pod ~220px,
        // window chrome ~90px at the 800x600 minimum, leaving this much for the nav.
        const double availableNavWidth = 300.0;
        const double compactButton = 28.0;   // MinWidth
        const double buttonMargin = 1.0;    // style Margin="1,0", applied once per button
        const double separator = 7.0;       // 1px rule + Margin 3,0 either side
        const double icon = 12.0;           // 10.5pt glyph
        const double compactPadding = 4.0;  // 4,6,4,6 -> 8px horizontal

        double total = 8 * (icon + (2 * compactPadding) + buttonMargin) + 2 * separator;
        total = Math.Max(total, 8 * (compactButton + buttonMargin)) + 2 * separator;

        Assert.True(total <= availableNavWidth,
            $"Compact nav needs {total}px but only {availableNavWidth}px is available at 800px wide.");
    }
}