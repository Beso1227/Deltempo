using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Converters;

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

        string[] parts = spec.Split(',');
        if (parts.Length < 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold) ||
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
