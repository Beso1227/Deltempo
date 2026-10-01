using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Converters;

/// <summary>
/// Collapses an element when the owning window is narrower than the given threshold.
/// The title-bar nav is a centred island between two auto-sized pods, so it is the first
/// thing to break in a small window: measured against real renders, the eight labelled nav
/// buttons need ~1225px and were already clipping "Force Delete" at 1360px. Below the
/// threshold the labels collapse and the buttons fall back to icon-only (each button keeps
/// its ToolTip and AutomationProperties.Name, so nothing becomes unreachable or unreadable
/// to assistive tech).
/// Usage: Visibility="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Window},
///          Converter={StaticResource WidthToCompactVisibilityConverter}, ConverterParameter=1280}"
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