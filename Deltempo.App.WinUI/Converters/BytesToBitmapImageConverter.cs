using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Deltempo.App.WinUI.Converters;

/// <summary>
/// Turns PNG-encoded icon bytes into a WinUI <see cref="BitmapImage"/>.
///
/// Core's AppIconService returns raw PNG bytes so one extraction path can serve
/// both frameworks; this is the WinUI half of that pair (the WPF half is
/// ByteArrayToImageSourceConverter). WinUI cannot consume a WPF ImageSource and
/// Core cannot know which framework it is running under.
///
/// Decoding is left to BitmapImage, which does it asynchronously once
/// SetSource is called. That is why this is safe to call from the UI thread
/// while the application list is populating - nothing is decoded inline.
///
/// No second cache is kept: BitmapImage already defers the decode, and the list
/// shows tens of items, so caching decoded images would trade memory for
/// nothing.
public sealed partial class BytesToBitmapImageConverter : IValueConverter
{
    /// <summary>
    /// Optional decode width. App icons render at 20-26px, so decoding the
    /// source PNG at full resolution would waste memory for every row.
    /// </summary>
    public int DecodePixelWidth { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            // DataWriter.DetachStream is the synchronous escape hatch: it flushes
            // the buffer and hands back the stream. StoreAsync cannot be used
            // here because a synchronous converter cannot await it.
            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                writer.DetachStream();
            }

            var image = new BitmapImage();
            if (DecodePixelWidth > 0)
            {
                image.DecodePixelWidth = DecodePixelWidth;
            }

            image.SetSource(stream);

            return image;
        }
        catch (Exception)
        {
            // Corrupt or truncated icon data. The caller falls back to the
            // category glyph, matching the WPF converter's behaviour.
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("BytesToBitmapImageConverter is one-way.");
}
