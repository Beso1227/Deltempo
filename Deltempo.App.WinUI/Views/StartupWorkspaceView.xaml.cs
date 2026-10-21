using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of the Startup Applications workspace from MainWindow.xaml and
/// MainWindow.SystemManagers.cs.
///
/// This is a WORKSPACE panel: it fills the content column for the "Startup
/// Applications Tool" nav item.
///
/// Nearly everything moved into StartupWorkspaceViewModel. What stayed here is
/// the confirmation dialog host, since a ViewModel cannot show one and the
/// confirmation host is injected so the ViewModel can be exercised without any
/// UI at all.
///
/// Self-test policy: the self-test calls LoadAsync, which only enumerates
/// registry hives and Task Scheduler entries. It never calls ToggleAsync,
/// CleanOrphanedAsync or OptimizeBootAsync - all three write to the registry.
public sealed partial class StartupWorkspaceView : UserControl
{
    public StartupWorkspaceView()
    {
        ViewModel = new StartupWorkspaceViewModel(confirmAsync: AskForConfirmationAsync);
        InitializeComponent();
    }

    public StartupWorkspaceViewModel ViewModel { get; }

    /// <summary>
    /// Mirrors ReloadStartupItemsAsync. Read-only: this discovers entries
    /// across the registry hives and Task Scheduler, and changes nothing.
    /// </summary>
    public async void Open()
    {
        await ViewModel.LoadAsync();
    }

    /// <summary>
    /// Routes a toggle-switch flip into the ViewModel command.
    ///
    /// ToggleSwitch is not a ButtonBase in WinUI 3, so it exposes no Command.
    /// Toggled fires during template application too, so the handler checks that
    /// the model and the switch have actually diverged before doing anything -
    /// otherwise selecting the Startup nav item would fire a registry write.
    /// </summary>
    private void OnToggleToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { DataContext: WinTempCleaner.Services.StartupItem item } toggle &&
            toggle.IsOn != item.IsEnabled)
        {
            ViewModel.ToggleCommand.Execute(item);
        }
    }

    /// <summary>Maps a chip's Tag back onto the enum. Fires on template apply too, so it is idempotent.</summary>
    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, ignoreCase: true, out StartupFilter filter))
        {
            ViewModel.SetFilterCommand.Execute(filter);
        }
    }

    /// <summary>
    /// The blocking yes/no prompt for registry mutations. Cancel is the default
    /// so a stray Enter cannot disable startup entries or delete a registry key.
    /// </summary>
    private async Task<bool> AskForConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                FontSize = 13,
            },
            PrimaryButtonText = "Yes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
