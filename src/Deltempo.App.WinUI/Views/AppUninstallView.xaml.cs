using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of Views/Modals/AppUninstallModal.xaml(.cs) (909 + 569 lines).
///
/// What moved out of the WPF code-behind and into the ViewModel:
///  - filtering, drawer filtering, selection summaries, and every confirmation's
///    text, so the wording the user reviewed is preserved verbatim
///  - MessageBox became an injected confirmation host (ContentDialog here)
///
/// What stayed here, because it is framework-only:
///  - the ContentDialog plumbing (WPF MessageBox has no WinUI equivalent)
///  - the chip Checked handlers, which map a Tag back to an enum
///
/// Self-test safety: LoadAppsAsync and ScanTracesAsync are read-only and are the
/// only paths the self-test drives. Deep Uninstall, Force Root Clean, and
/// PurgeTraces each require a confirmation this view only supplies when a human
/// presses the button, and the self-test never invokes those commands.
/// </summary>
public sealed partial class AppUninstallView : UserControl
{
    public AppUninstallView()
    {
        ViewModel = new AppUninstallViewModel(
            confirmAsync: AskForConfirmationAsync,
            openFolder: OpenInShell);

        InitializeComponent();

        ViewModel.FatalError += OnFatalError;
    }

    public AppUninstallViewModel ViewModel { get; }

    /// <summary>
    /// Mirrors the WPF Open(): loads the audit and shows the surface.
    ///
    /// The caller owns LogRequested, matching the other ported overlays.
    /// </summary>
    public async void Open()
    {
        Visibility = Visibility.Visible;
        await ViewModel.LoadAppsAsync();
    }

    /// <summary>Mirrors the WPF CloseModal(): resets drawer state and hides.</summary>
    public void CloseModal()
    {
        SoundService.PlayClickSound();
        ViewModel.CloseDrawerCommand.Execute(null);
        Visibility = Visibility.Collapsed;
    }

    // ---- Chip handlers ------------------------------------------------------

    /// <summary>
    /// Maps a filter chip's Tag back onto the enum and forwards it.
    ///
    /// Checked fires during template application as well as on click, so this
    /// is idempotent: re-selecting the active filter simply re-applies it.
    /// </summary>
    private void OnFilterChipChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, ignoreCase: true, out AppListFilter filter))
        {
            ViewModel.SetFilterCommand.Execute(filter);
        }
    }

    private void OnDrawerChipChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, ignoreCase: true, out LeftoverDrawerFilter filter))
        {
            ViewModel.SetLeftoverFilterCommand.Execute(filter);
        }
    }

    /// <summary>
    /// LeftoverItem is a plain model with no change notification, so ticking a
    /// checkbox cannot update the summary by itself. This refreshes it, exactly
    /// as the WPF code-behind did.
    /// </summary>
    private void OnLeftoverChecked(object sender, RoutedEventArgs e)
        => ViewModel.NotifyTraceSelectionChanged();

    // ---- Row button handlers ---------------------------------------------------

    /// <summary>
    /// Row buttons cannot reach the page's commands through an ElementName
    /// {Binding} - WinUI 3 does not resolve the path from inside a DataTemplate
    /// (the template owns its own namescope, where "Root" is invisible), so the
    /// bindings silently failed. The clicks are handled here: read the app off
    /// the DataContext, execute the ViewModel command. The commands keep all of
    /// their gates and confirmations.
    /// </summary>
    private static InstalledAppItem? RowItem(object sender) =>
        (sender as FrameworkElement)?.DataContext as InstalledAppItem;

    private void OnRowFetchAiClick(object sender, RoutedEventArgs e)
        => ViewModel.FetchAiCommand.Execute(RowItem(sender)!);

    private void OnRowOpenInstallFolderClick(object sender, RoutedEventArgs e)
        => ViewModel.OpenInstallFolderCommand.Execute(RowItem(sender));

    private void OnRowScanTracesClick(object sender, RoutedEventArgs e)
        => ViewModel.ScanTracesCommand.Execute(RowItem(sender)!);

    private void OnRowForceRootCleanClick(object sender, RoutedEventArgs e)
        => ViewModel.ForceRootCleanCommand.Execute(RowItem(sender)!);

    private void OnRowDeepUninstallClick(object sender, RoutedEventArgs e)
        => ViewModel.DeepUninstallCommand.Execute(RowItem(sender)!);

    private void OnRowToggleExpandClick(object sender, RoutedEventArgs e)
        => ViewModel.ToggleExpandCommand.Execute(RowItem(sender));

    // ---- Dialog hosts -------------------------------------------------------

    /// <summary>
    /// The blocking yes/no prompt for uninstall and purge actions.
    /// Returns true only when the user presses the affirmative button.
    /// </summary>
    private async Task<bool> AskForConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    FontSize = 13,
                },
            },
            PrimaryButtonText = "Proceed",
            CloseButtonText = "Cancel",

            // Cancel is the default so a stray Enter never runs a destructive
            // action. The affirmative path requires a deliberate click.
            DefaultButton = ContentDialogButton.Close,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async void OnFatalError(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Close",
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Opens a folder in the shell. Wrapped because Explorer can fail for
    /// reasons the user can act on (path removed, permission), and a silent
    /// no-op would look like a broken button.
    /// </summary>
    private void OpenInShell(string path) => ViewModel.OpenInShell(path);
}
