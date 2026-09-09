using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Services;

public class BoolToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isError = value is bool b && b;
        return isError ? new Thickness(1.5) : new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
