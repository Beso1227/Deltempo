using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTempCleaner.Services;

/// <summary>
/// Converts a percentage (0–100) + a total width into a pixel width for a progress bar thumb.
/// Usage: <MultiBinding Converter="{StaticResource PercentWidthConverter}">
///          <Binding Path="UsedPercent"/>
///          <Binding RelativeSource="{RelativeSource AncestorType=Grid}" Path="ActualWidth"/>
///        </MultiBinding>
/// </summary>
public class PercentWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return new GridLength(0);

        if (values[0] is double pct && values[1] is double totalWidth)
        {
            if (totalWidth <= 0) return new GridLength(0, GridUnitType.Pixel);
            double pixelWidth = Math.Max(0, Math.Min(totalWidth, totalWidth * pct / 100.0));
            return new GridLength(pixelWidth, GridUnitType.Pixel);
        }

        if (values[0] is int pctInt)
        {
            double pctVal = pctInt;
            if (values[1] is double totalWidth2)
            {
                if (totalWidth2 <= 0) return new GridLength(0, GridUnitType.Pixel);
                double pixelWidth = Math.Max(0, Math.Min(totalWidth2, totalWidth2 * pctVal / 100.0));
                return new GridLength(pixelWidth, GridUnitType.Pixel);
            }
        }

        return new GridLength(0, GridUnitType.Pixel);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
