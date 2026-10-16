using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Deltempo.App.WinUI.Converters;

/// <summary>
/// Converts an ARGB hex string ("#FFF59E0B" or "#F59E0B") into a SolidColorBrush.
///
/// The footprint report carries its badge and bar colors as data (hex strings)
/// rather than as brush objects, so the ViewModel stays testable without any
/// WinUI media types. This is the WinUI 3 equivalent of the inline
/// ColorConverter calls in the WPF AppDataFootprintModal code-behind.
///
/// Hex is parsed by hand rather than through a platform helper so the accepted
/// formats are explicit and cannot drift with framework changes.
/// </summary>
public sealed partial class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && TryParseHex(hex, out Color color))
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("HexToBrushConverter is one-way.");

    internal static bool TryParseHex(string hex, out Color color)
    {
        color = default;

        string text = hex.Trim().TrimStart('#');
        if (text.Length != 6 && text.Length != 8)
        {
            return false;
        }

        if (!uint.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out uint packed))
        {
            return false;
        }

        // 6-digit input is opaque; 8-digit input is ARGB.
        byte a = text.Length == 8 ? (byte)((packed >> 24) & 0xFF) : (byte)0xFF;
        byte r = (byte)((packed >> 16) & 0xFF);
        byte g = (byte)((packed >> 8) & 0xFF);
        byte b = (byte)(packed & 0xFF);

        color = Color.FromArgb(a, r, g, b);
        return true;
    }
}