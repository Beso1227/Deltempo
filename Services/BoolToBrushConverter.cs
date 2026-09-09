using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WinTempCleaner.Services;

public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string raw) return value;

        var parts = raw.Split(';');
        if (parts.Length < 2) return value;

        bool isError = value is bool b && b;
        string token = isError ? parts[0].Trim() : parts[1].Trim();

        if (token.StartsWith("#") || token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return (SolidColorBrush)new BrushConverter().ConvertFrom(token)!;
        }

        return Application.Current.TryFindResource(token) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
