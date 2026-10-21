using Microsoft.UI.Xaml.Data;

namespace Deltempo.App.WinUI.Converters;

/// <summary>
/// Negates a bool and returns a bool. Not a Visibility.
///
/// This exists because of a bug worth not repeating: BoolToVisibilityConverter
/// looks like it should work on IsEnabled, and WPF forgives it because WPF
/// coerces bool to Visibility implicitly. WinUI 3 does not, and throws
/// InvalidCastException ("Unable to cast Visibility to Boolean") while applying
/// the binding - which takes the whole window down at load, not just the control.
///
/// Use this for IsEnabled / IsHitTestVisible / Opacity gates. Use
/// BoolToVisibilityConverter only for Visibility.
/// </summary>
public sealed partial class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not bool flag || !flag;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is not bool flag || !flag;
}
