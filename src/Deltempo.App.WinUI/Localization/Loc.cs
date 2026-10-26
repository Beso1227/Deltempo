using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Localization;

/// <summary>
/// Binds static XAML copy to <see cref="LocalizationService"/> so a language switch
/// re-paints the tree without a per-view assignment list.
///
/// This is a presentation hook, not a second localization system: every value still
/// comes from Core's <see cref="LocalizationService.Get"/>, which owns the five
/// dictionaries and the English fallback. WPF keeps its explicit ApplyLocalization()
/// because that pass also re-measures totals; the attached properties here exist
/// because WinUI has no equivalent named-control field to walk.
///
/// Usage:
///   loc:Loc.Key="NavCleaner"          visible label (TextBlock / Button / chip)
///   loc:Loc.PlaceholderKey="Search"   PlaceholderText on TextBox / AutoSuggestBox
///   loc:Loc.ToolTipKey="..."          ToolTipService.ToolTip
///   loc:Loc.NameKey="..."             AutomationProperties.Name
///
/// The attached value is stored on the element, so a language switch is one tree
/// walk (<see cref="ApplyTree"/>) with no registry to keep in sync or leak.
/// </summary>
public static class Loc
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, OnTextKeyChanged));

    public static readonly DependencyProperty PlaceholderKeyProperty = DependencyProperty.RegisterAttached(
        "PlaceholderKey",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, OnPlaceholderKeyChanged));

    public static readonly DependencyProperty ToolTipKeyProperty = DependencyProperty.RegisterAttached(
        "ToolTipKey",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, OnToolTipKeyChanged));

    public static readonly DependencyProperty NameKeyProperty = DependencyProperty.RegisterAttached(
        "NameKey",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, OnNameKeyChanged));

    public static void SetKey(DependencyObject element, string value)
        => element.SetValue(KeyProperty, value);

    public static string GetKey(DependencyObject element)
        => (string)element.GetValue(KeyProperty);

    public static void SetPlaceholderKey(DependencyObject element, string value)
        => element.SetValue(PlaceholderKeyProperty, value);

    public static string GetPlaceholderKey(DependencyObject element)
        => (string)element.GetValue(PlaceholderKeyProperty);

    public static void SetToolTipKey(DependencyObject element, string value)
        => element.SetValue(ToolTipKeyProperty, value);

    public static string GetToolTipKey(DependencyObject element)
        => (string)element.GetValue(ToolTipKeyProperty);

    public static void SetNameKey(DependencyObject element, string value)
        => element.SetValue(NameKeyProperty, value);

    public static string GetNameKey(DependencyObject element)
        => (string)element.GetValue(NameKeyProperty);

    private static void OnTextKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key && key.Length > 0)
        {
            ApplyText(d, key);
        }
    }

    private static void OnPlaceholderKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key && key.Length > 0)
        {
            ApplyPlaceholder(d, key);
        }
    }

    private static void OnToolTipKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key && key.Length > 0)
        {
            ToolTipService.SetToolTip(d, LocalizationService.Get(key));
        }
    }

    private static void OnNameKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key && key.Length > 0)
        {
            AutomationProperties.SetName(d, LocalizationService.Get(key));
        }
    }

    private static void ApplyText(DependencyObject element, string key)
    {
        string value = LocalizationService.Get(key);

        // CheckBox and RadioButton are ButtonBase, so one arm covers the chips as
        // well as plain buttons. TextBlock is matched first because it is the most
        // common target and has its own Text property.
        switch (element)
        {
            case TextBlock text:
                text.Text = value;
                break;
            case ButtonBase button:
                button.Content = value;
                break;
            case MenuFlyoutItem menuItem:
                menuItem.Text = value;
                break;
        }
    }

    private static void ApplyPlaceholder(DependencyObject element, string key)
    {
        string value = LocalizationService.Get(key);

        switch (element)
        {
            case TextBox textBox:
                textBox.PlaceholderText = value;
                break;
            case AutoSuggestBox suggestBox:
                suggestBox.PlaceholderText = value;
                break;
            case PasswordBox passwordBox:
                passwordBox.PlaceholderText = value;
                break;
        }
    }

    /// <summary>
    /// Re-applies every attached key under <paramref name="root"/>, including the
    /// root itself. Returns how many translations were written, which the self-test
    /// logs so a silently empty key shows up as a count, not as a blank label.
    /// </summary>
    public static int ApplyTree(DependencyObject root)
    {
        int applied = 0;
        ApplyElement(root, ref applied);

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            applied += ApplyTree(VisualTreeHelper.GetChild(root, i));
        }

        return applied;
    }

    /// <summary>
    /// Every distinct text key attached under <paramref name="root"/>. The self-test
    /// uses this to prove each XAML key actually exists in the dictionary: an unknown
    /// key resolves to itself, which would otherwise show up as a bare word in the UI.
    /// </summary>
    public static IReadOnlyList<string> CollectKeys(DependencyObject root)
    {
        var keys = new List<string>();
        CollectKeys(root, keys);
        return keys;
    }

    private static void CollectKeys(DependencyObject root, List<string> keys)
    {
        if (root is FrameworkElement)
        {
            if (GetKey(root) is { Length: > 0 } key)
            {
                keys.Add(key);
            }

            if (GetPlaceholderKey(root) is { Length: > 0 } placeholderKey)
            {
                keys.Add(placeholderKey);
            }

            if (GetToolTipKey(root) is { Length: > 0 } toolTipKey)
            {
                keys.Add(toolTipKey);
            }

            if (GetNameKey(root) is { Length: > 0 } nameKey)
            {
                keys.Add(nameKey);
            }
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            CollectKeys(VisualTreeHelper.GetChild(root, i), keys);
        }
    }

    private static void ApplyElement(DependencyObject element, ref int applied)
    {
        if (element is FrameworkElement)
        {
            if (GetKey(element) is { Length: > 0 } key)
            {
                ApplyText(element, key);
                applied++;
            }

            if (GetPlaceholderKey(element) is { Length: > 0 } placeholderKey)
            {
                ApplyPlaceholder(element, placeholderKey);
                applied++;
            }

            if (GetToolTipKey(element) is { Length: > 0 } toolTipKey)
            {
                ToolTipService.SetToolTip(element, LocalizationService.Get(toolTipKey));
                applied++;
            }

            if (GetNameKey(element) is { Length: > 0 } nameKey)
            {
                AutomationProperties.SetName(element, LocalizationService.Get(nameKey));
                applied++;
            }
        }
    }
}
