using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Converters;

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

        string[] parts = spec.Split(',');
        if (parts.Length < 5 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold))
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