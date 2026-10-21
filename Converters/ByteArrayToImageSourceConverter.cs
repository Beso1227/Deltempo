using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinTempCleaner.Converters;

/// <summary>
/// Turns PNG-encoded icon bytes into a frozen WPF <see cref="ImageSource"/>.
///
/// Core no longer produces WPF types: <c>AppIconService</c> returns raw PNG bytes
/// so WinUI 3 can build a SoftwareBitmapSource from the same bytes. This is the
/// WPF half of that pair, and it is where the framework-specific decoding lives
/// again.
///
/// The image is frozen before being handed back. App icons are produced on a
/// background thread during the registry scan, and an unfrozen BitmapImage
/// carries a dispatcher affinity that WPF would then reject on the UI thread.
/// </summary>
public sealed class ByteArrayToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes, writable: false);
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (NotSupportedException)
        {
            // Corrupt or truncated icon data. Fall back to the category glyph.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("ByteArrayToImageSourceConverter is one-way.");
}
