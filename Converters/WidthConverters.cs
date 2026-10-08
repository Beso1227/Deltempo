using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Converters;

/// <summary>
/// Title-bar nav "compact mode" converters. All three key off the owning window's
/// <c>ActualWidth</c> and switch the nav to icon-only below a threshold, so they are
/// kept together: one place to re-measure or re-tune the breakpoints.
/// </summary>
/// <remarks>
/// Usage pattern (each takes <c>ConverterParameter</c> as an invariant-culture string):
/// <code>
/// Visibility="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Window},
///              Converter={StaticResource WidthToCompactVisibilityConverter}, ConverterParameter=1280}"
/// </code>
/// </remarks>
internal static class CompactThreshold
{
    /// <summary>
    /// Parses the leading <c>threshold[,compact…]</c> spec and reports whether it is usable.
    /// </summary>
    public static bool TrySplit(string? spec, int minimumParts, out string[] parts, out double threshold)
    {
        parts = spec?.Split(',') ?? Array.Empty<string>();
        threshold = 0;

        return parts.Length >= minimumParts &&
               double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out threshold);
    }
}

/// <summary>
/// Collapses an element when the owning window is narrower than the given threshold.
/// The title-bar nav is a centred island between two auto-sized pods, so it is the first
/// thing to break in a small window: measured against real renders, the eight labelled nav
/// buttons need ~1225px and were already clipping "Force Delete" at 1360px. Below the
/// threshold the labels collapse and the buttons fall back to icon-only (each button keeps
/// its ToolTip and AutomationProperties.Name, so nothing becomes unreachable or unreadable
/// to assistive tech).
/// </summary>
public class WidthToCompactVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width)
        {
            return Visibility.Visible;
        }

        if (!double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold))
        {
            return Visibility.Visible;
        }

        // Use Collapsed (not Hidden) so collapsed nav labels do not reserve any width.
        return width < threshold ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns a smaller numeric value once the window drops below a width threshold.
/// Used to relax the title-bar nav's MinWidth in compact (icon-only) mode: eight buttons
/// at the 34 DIP MinTouchTarget need ~302px, which overflows a 720px window. Compact mode
/// uses 28 DIP, which still clears the 24 DIP WCAG 2.5.8 minimum and keeps the vertical
/// MinHeight at the full touch target.
/// </summary>
/// <remarks>
/// ConverterParameter format: <c>threshold,compactValue</c> - e.g. <c>1280,28</c>.
/// </remarks>
public class WidthToCompactDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || parameter is not string spec)
        {
            return DependencyProperty.UnsetValue;
        }

        if (!CompactThreshold.TrySplit(spec, 2, out string[] parts, out double threshold) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double compactValue))
        {
            return DependencyProperty.UnsetValue;
        }

        return width < threshold ? compactValue : DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Switches a Thickness to a tighter "compact" value once the window drops below a width
/// threshold. The title-bar nav buttons inherit Padding="13,6"; in icon-only mode that
/// horizontal padding is pure waste and was the reason the eight-button nav still
/// overflowed at 720px even after the labels collapsed.
/// </summary>
/// <remarks>
/// ConverterParameter format: <c>threshold,left,top,right,bottom</c> - e.g. <c>1280,4,6,4,6</c>.
/// The value is only used when compact; otherwise the element keeps whatever Padding it
/// inherited from its style, so this returns <see cref="DependencyProperty.UnsetValue"/>
/// rather than a value, letting the style's own Padding continue to apply.
/// </remarks>
public class WidthToCompactThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || parameter is not string spec)
        {
            return DependencyProperty.UnsetValue;
        }

        if (!CompactThreshold.TrySplit(spec, 5, out string[] parts, out double threshold))
        {
            return DependencyProperty.UnsetValue;
        }

        if (width >= threshold)
        {
            // Not compact: fall back to the style's Padding.
            return DependencyProperty.UnsetValue;
        }

        var nums = new double[4];
        for (int i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out nums[i]))
            {
                return DependencyProperty.UnsetValue;
            }
        }

        return new Thickness(nums[0], nums[1], nums[2], nums[3]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
