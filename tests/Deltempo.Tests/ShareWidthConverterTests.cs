using System;
using System.Globalization;
using WinTempCleaner.Converters;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// W2 UI support: the proportional fill bar.
///
/// These exist because the bar shipped BROKEN twice. It rendered as a perfectly normal-looking
/// empty track, so no screenshot review and no XAML assertion caught it - the fault was a type
/// mismatch and an unresolvable RelativeSource, both of which fail silently at runtime. A
/// converter is the one part of this that can actually be tested.
/// </summary>
public class ShareWidthConverterTests
{
    private static double Convert(object percent, object totalWidth) =>
        (double)new ShareWidthConverter().Convert(
            [percent, totalWidth], typeof(double), null!, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(100.0, 150.0, 150.0)]  // the largest folder fills the whole track
    [InlineData(50.0, 150.0, 75.0)]    // half
    [InlineData(25.0, 150.0, 37.5)]
    [InlineData(0.0, 150.0, 0.0)]
    [InlineData(10.0, 100.0, 10.0)]
    public void Convert_ProportionalShare_ProducesExpectedPixelWidth(
        double percent, double totalWidth, double expected) =>
        Assert.Equal(expected, Convert(percent, totalWidth), 3);

    /// <summary>
    /// A share above 100 must never let the fill escape its track, and a negative one must
    /// never render a bar growing leftwards.
    /// </summary>
    [Theory]
    [InlineData(140.0, 150.0, 150.0)]
    [InlineData(-25.0, 150.0, 0.0)]
    public void Convert_OutOfRangeShare_IsClampedToTheTrack(double percent, double total, double expected) =>
        Assert.Equal(expected, Convert(percent, total), 3);

    /// <summary>
    /// An unmeasured track (ActualWidth 0 during layout) must collapse to zero rather than
    /// producing NaN or Infinity, either of which throws during measure and takes the window down.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void Convert_NonPositiveTrackWidth_CollapsesToZero(double totalWidth) =>
        Assert.Equal(0.0, Convert(50.0, totalWidth), 3);

    [Fact]
    public void Convert_MissingValue_ReturnsZero()
    {
        var converter = new ShareWidthConverter();

        Assert.Equal(0.0, (double)converter.Convert([], typeof(double), null!, CultureInfo.InvariantCulture));
        Assert.Equal(0.0, (double)converter.Convert([50.0], typeof(double), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_NonNumericInput_ReturnsZeroRatherThanThrowing()
    {
        var converter = new ShareWidthConverter();

        Assert.Equal(0.0, (double)converter.Convert(
            ["not a number", 150.0], typeof(double), null!, CultureInfo.InvariantCulture));
        Assert.Equal(0.0, (double)converter.Convert(
            [50.0, null!], typeof(double), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_AcceptsIntShares()
    {
        var converter = new ShareWidthConverter();

        // A binding may hand over an int rather than a double depending on the source property.
        Assert.Equal(75.0, (double)converter.Convert([50, 150.0], typeof(double), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        var converter = new ShareWidthConverter();

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(0d, [typeof(double)], null!, CultureInfo.InvariantCulture));
    }
}
