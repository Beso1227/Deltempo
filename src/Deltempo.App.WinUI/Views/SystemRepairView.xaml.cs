using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of Views/Modals/SystemRepairModal.xaml(.cs) (1054 + 503 lines).
///
/// Nearly all of this moved into SystemRepairViewModel. What stayed here is
/// only what WinUI requires a view to own:
///
///  - the ContentDialog host for the two confirmations (close-during-repair and
///    restore point pruning)
///  - the clipboard write, since Clipboard lives in the Windows SDK
///  - relaunching elevated, which the ViewModel exposes as an injected action
///
/// Self-test policy: no repair tool is ever invoked here. Every one of them
/// mutates the live system; several require Administrator; interrupting CHKDSK
/// or the Winsock reset can leave Windows needing further repair. The self-test
/// only constructs the ViewModel to read its elevation state and its fail-closed
/// confirmation host.
/// </summary>
public sealed partial class SystemRepairView : UserControl
{
    public SystemRepairView()
    {
        ViewModel = new SystemRepairViewModel(
            confirmAsync: AskForConfirmationAsync,
            elevate: ElevateAndRestart);

        InitializeComponent();

        ViewModel.FatalError += OnFatalError;
        ViewModel.ClipboardRequested += OnClipboardRequested;
    }

    public SystemRepairViewModel ViewModel { get; }

    /// <summary>
    /// Mirrors the WPF Open(): refresh elevation, then enumerate restore points.
    /// Both are read-only.
    /// </summary>
    public async void Open()
    {
        Visibility = Visibility.Visible;
        await ViewModel.OpenAsync();
    }

    /// <summary>
    /// Mirrors the WPF CloseModal(). Returns false when a repair is running and
    /// the user declines to cancel it, so the caller keeps the surface open.
    /// </summary>
    public async Task<bool> RequestCloseAsync()
    {
        if (!await ViewModel.RequestCloseAsync())
        {
            return false;
        }

        Visibility = Visibility.Collapsed;
        return true;
    }

    // ---- Dialog host --------------------------------------------------------

    /// <summary>
    /// The blocking yes/no prompt for cancelling a running repair and for
    /// pruning restore points. Returns true only on the affirmative button.
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
            CloseButtonText = "No",

            // Cancel is the default: neither cancelling a repair nor deleting
            // restore points should be one stray Enter away.
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

    // ---- Platform bits the ViewModel cannot own -----------------------------

    /// <summary>
    /// Writes the terminal transcript to the clipboard.
    ///
    /// Clipboard can throw when another process holds it open, which is common
    /// on a busy desktop. The failure is reported rather than swallowed so a
    /// broken Copy button is visible instead of silent.
    /// </summary>
    private void OnClipboardRequested(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            ViewModel.Log($"Failed to copy terminal log: {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// Relaunches Deltempo elevated.
    ///
    /// Injected into the ViewModel so the elevation state and the relaunch path
    /// are testable without actually restarting the process.
    /// </summary>
    private static void ElevateAndRestart() => ElevationService.RestartAsAdmin();
}
