using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// Workspace panel for the shared settings. The language list is filled here rather
/// than in XAML because the items carry a language code next to their display name,
/// and the code is what the setting stores.
/// </summary>
public sealed partial class SettingsWorkspaceView : UserControl
{
    public SettingsWorkspaceView()
    {
        InitializeComponent();

        foreach ((string code, string name) in SettingsWorkspaceViewModel.Languages)
        {
            var item = new ComboBoxItem { Content = name, Tag = code };
            LanguageCombo.Items.Add(item);
        }

        // The panel lives in a grid rather than a frame, so it is never navigated to
        // and the navigation events would never fire: the subscriptions live here.
        ViewModel.LogRequested += OnLog;
        ViewModel.ThemeChangeRequested += OnThemeChangeRequested;

        SelectLanguage(ViewModel.Language);
    }

    public SettingsWorkspaceViewModel ViewModel { get; } = new();

    public event Action<string, LogLevel>? LogRequested;

    public event Action<bool>? ThemeChangeRequested;

    private void OnLog(string message, LogLevel level)
        => LogRequested?.Invoke(message, level);

    private void OnThemeChangeRequested(bool isDark)
        => ThemeChangeRequested?.Invoke(isDark);

    private void SelectLanguage(string code)
    {
        foreach (ComboBoxItem item in LanguageCombo.Items)
        {
            if (item.Tag as string == code)
            {
                LanguageCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((LanguageCombo.SelectedItem as ComboBoxItem)?.Tag is string code)
        {
            ViewModel.Language = code;
        }
    }

    private async void OnCheckForUpdatesClick(object sender, RoutedEventArgs e)
        => await ViewModel.CheckForUpdatesCommand.ExecuteAsync(null);

    private void OnOpenReleasesClick(object sender, RoutedEventArgs e)
        => ViewModel.OpenReleasesPageCommand.Execute(null);
}
