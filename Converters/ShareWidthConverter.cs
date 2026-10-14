using System;
using System.Globalization;
using System.Windows.Data;

namespace WinTempCleaner.Converters;

/// <summary>
/// Turns a 0-100 share percentage plus a track width into a pixel width for a proportional
/// fill bar.
///
/// Why this exists rather than reusing <see cref="PercentWidthConverter"/>: that converter
/// returns a <c>GridLength</c>, which can only drive <c>ColumnDefinition.Width</c> - but a
/// MultiBinding using <c>RelativeSource AncestorType=Grid</c> cannot resolve from inside a
/// ColumnDefinition, because a ColumnDefinition is not part of the visual tree. Bound to
/// <c>Border.Width</c> (a double) the GridLength fails the type check and the binding fails
/// SILENTLY. The visible symptom is a perfectly normal-looking empty track, which is why this
/// shipped unnoticed twice.
///
/// Usage:
/// <code>
///   &lt;MultiBinding Converter="{StaticResource ShareWidthConverter}"&gt;
///     &lt;Binding Path="SharePercent"/&gt;
///     &lt;Binding Path="ActualWidth" RelativeSource="{RelativeSource AncestorType=Grid}"/&gt;
///   &lt;/MultiBinding&gt;
/// </code>
/// The bound track MUST declare a fixed Width, otherwise its width depends on the fill it
/// contains and the measurement is circular.
/// </summary>
public class ShareWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0d;
        if (values[0] is not IConvertible percent || values[1] is not IConvertible total)
        {
            return 0d;
        }

        double totalWidth;
        double share;
        try
        {
            totalWidth = total.ToDouble(culture);
            share = percent.ToDouble(culture);
        }
        catch
        {
            return 0d;
        }

        if (totalWidth <= 0) return 0d;

        // Clamp: a share above 100 must never let the fill escape its track, and a negative
        // one must never render a bar that grows the row to the left.
        double clamped = Math.Max(0, Math.Min(100, share));
        return Math.Max(0, Math.Min(totalWidth, totalWidth * clamped / 100.0));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException("ShareWidthConverter is one-way.");
}
