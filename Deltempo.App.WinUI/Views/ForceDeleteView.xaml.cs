using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of Views/Modals/ForceDeleteModal.xaml(.cs).
///
/// The only genuinely framework-forced change lives here: WPF offered
/// OpenFileDialog / OpenFolderDialog, and WinUI 3 has no equivalent. The
/// replacement is the WinRT Storage.Picker, which in a desktop app must be
/// handed the window handle explicitly or it throws. All of that is confined
/// to <see cref="WindowsPathPicker"/> so the ViewModel stays picker-free.
///
/// The destructive confirmation is also routed here, as a ContentDialog. The
/// message text is built by the ViewModel so the wording the user reviewed in
/// the WPF build is preserved exactly.
///
/// Safety note for the self-test path: this view never stages a path and never
/// answers its own confirmation, so it cannot delete anything unless a human
/// picks files and answers the dialog.
/// </summary>
public sealed partial class ForceDeleteView : UserControl, IPathPicker
{
    public ForceDeleteView()
    {
        ViewModel = new ForceDeleteViewModel(
            picker: this,
            confirmAsync: AskForConfirmationAsync);

        InitializeComponent();

        ViewModel.FatalError += OnFatalError;
    }

    public ForceDeleteViewModel ViewModel { get; }

    /// <summary>Opens with an empty staging list, mirroring the WPF OpenEmpty().</summary>
    public void OpenEmpty() => ViewModel.OpenEmpty();

    // ---- IPathPicker -------------------------------------------------------

    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List,
        };

        // Multi-select is the WPF default on this dialog.
        picker.FileTypeFilter.Add("*");

        return await PickAsync(picker);
    }

    public async Task<IReadOnlyList<string>> PickFoldersAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };

        picker.FileTypeFilter.Add("*");

        return await PickAsync(picker);
    }

    /// <summary>
    /// Runs the picker against this window's HWND.
    ///
    /// Storage.Picker in a WinUI 3 desktop app is not COM-initialized for the
    /// XamlIsland; without InitializeWithWindow it throws. MustRunOnUIThread
    /// keeps the call on the dispatcher, which the picker also requires.
    /// </summary>
    private async Task<IReadOnlyList<string>> PickAsync(object picker)
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            object? result = picker switch
            {
                FileOpenPicker f => await f.PickMultipleFilesAsync(),
                FolderPicker fo => await fo.PickSingleFolderAsync(),
                _ => null,
            };

            return ToPaths(result);
        }
        catch (Exception ex)
        {
            // Includes the user cancelling the dialog, which surfaces as an
            // exception. Cancellation must stay silent; anything else is worth
            // surfacing as a log line.
            if (ex is System.Runtime.InteropServices.COMException
                or OperationCanceledException)
            {
                return [];
            }

            ViewModel.Log($"File picker error: {ex.Message}", LogLevel.Warning);
            return [];
        }
    }

    private static IReadOnlyList<string> ToPaths(object? result)
    {
        switch (result)
        {
            case IEnumerable<Windows.Storage.StorageFile> files:
                return [.. files.Select(f => f.Path)];

            case Windows.Storage.StorageFolder folder:
                return [folder.Path];

            default:
                // null == cancelled.
                return [];
        }
    }

    // ---- Confirmation + error dialogs --------------------------------------

    /// <summary>
    /// The blocking "you are about to permanently delete N things" prompt.
    /// Returns true only if the user presses the destructive default button.
    /// </summary>
    private async Task<bool> AskForConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 380,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    FontSize = 13,
                },
            },
            PrimaryButtonText = "Force Delete",
            CloseButtonText = "Cancel",

            // "Force Delete" is the proposed action, so it must not be the
            // default a stray Enter press activates.
            DefaultButton = ContentDialogButton.Close,
        };

        dialog.PrimaryButtonStyle = new Microsoft.UI.Xaml.Style(typeof(Button))
        {
            Setters =
            {
                new Microsoft.UI.Xaml.Setter(Control.BackgroundProperty, "#F43F5E"),
                new Microsoft.UI.Xaml.Setter(Control.ForegroundProperty, Microsoft.UI.Colors.White),
            },
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
}