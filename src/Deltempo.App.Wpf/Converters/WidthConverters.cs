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
/// Switches the title-bar nav button Padding between a roomy "labelled" value and a tighter
/// "compact" (icon-only) value once the window drops below a width threshold.
/// </summary>
/// <remarks>
/// ConverterParameter format: <c>threshold,labelledLeft,labelledTop,labelledRight,labelledBottom,compactLeft,compactTop,compactRight,compactBottom</c>.
/// Example: <c>1280,12,4,12,4,4,6,4,6</c>.
/// <para>
/// Both regimes return a concrete <see cref="Thickness"/> on purpose. An earlier version
/// returned <see cref="DependencyProperty.UnsetValue"/> above the threshold to "fall back to
/// the style's Padding", but a binding installed as a <i>local</i> value that produces
/// UnsetValue resolves to the property <i>default</i> (0), not the next precedence source —
/// so the style's Padding setter was silently overridden and every labelled pill rendered
/// with 0 horizontal padding. Returning both values explicitly makes the nav's horizontal
/// rhythm a single source of truth that never collapses to 0.
/// </para>
/// </remarks>
public class WidthToCompactThicknessConverter : IValueConverter
{
    private const int PartsPerThickness = 4;

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

        // 9-part spec: "threshold,labelledL,labelledT,labelledR,labelledB,compactL,compactT,compactR,compactB".
        // Both regimes return a concrete Thickness so the nav's horizontal rhythm has one source
        // of truth. 5-part spec is the legacy "compact only, else defer to the style" form.
        int offset;
        if (parts.Length >= 9)
        {
            offset = width < threshold ? 5 : 1;
        }
        else if (width < threshold)
        {
            offset = 1;
        }
        else
        {
            return DependencyProperty.UnsetValue;
        }

        var nums = new double[PartsPerThickness];
        for (int i = 0; i < PartsPerThickness; i++)
        {
            if (!double.TryParse(parts[offset + i], NumberStyles.Float, CultureInfo.InvariantCulture, out nums[i]))
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
