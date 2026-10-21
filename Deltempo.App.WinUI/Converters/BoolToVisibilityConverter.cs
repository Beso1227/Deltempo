using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Deltempo.App.WinUI.Converters;

/// <summary>
/// Maps a bool to <see cref="Visibility"/>. The WinUI 3 equivalent of the WPF
/// BoolToVisConverter the WPF views rely on - WinUI does not coerce bool to
/// Visibility on its own.
/// </summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>When true, a false input collapses instead of showing (inverse).</summary>
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool flag = value is bool b && b;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("BoolToVisibilityConverter is one-way.");
}
