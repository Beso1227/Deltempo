using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Converters;

/// <summary>
/// Subtracts <see cref="ConverterParameter"/> from the bound size so modal surfaces never
/// outgrow the available window. Without this a modal's fixed MaxHeight can exceed the
/// window floor (the loader clamps the window down to 720x520 on small or high-DPI
/// displays), which pushes the footer actions off-screen and makes them unreachable.
/// Usage: MaxHeight="{Binding ActualHeight, RelativeSource={RelativeSource AncestorType=Window},
///          Converter={StaticResource SubtractConverter}, ConverterParameter=64}"
/// </summary>
public class SubtractConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double source ||
            !double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double delta))
        {
            // Pass the original value through so a malformed binding degrades to the
            // static MaxHeight rather than collapsing the element to zero.
            return value;
        }

        return Math.Max(0, source - delta);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}