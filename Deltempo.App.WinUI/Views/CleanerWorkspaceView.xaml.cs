using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of the Cleaner workspace.
///
/// This is a WORKSPACE panel: it fills the content column for the "Cleaner
/// Workspace" nav item. The scan is read-only; cleaning is gated behind an
/// injected confirmation that defaults to refusing.
///
/// Self-test policy: ScanAllAsync only enumerates and measures files. Clean
/// (CleanSelectedAsync) is never invoked by the self-test. A bare ViewModel
/// is used, so even an accidental mutating call fails closed.
/// </summary>
public sealed partial class CleanerWorkspaceView : UserControl
{
    public CleanerWorkspaceView()
    {
        ViewModel = new CleanerWorkspaceViewModel(confirmAsync: AskForConfirmationAsync);
        ViewModel.LogRequested += OnLogRequested;
        InitializeComponent();
    }

    public CleanerWorkspaceViewModel ViewModel { get; }

    /// <summary>Read-only scan of all target categories. Safe to call from the self-test.</summary>
    public async void Open()
    {
        SoundService.PlayClickSound();
        await ViewModel.ScanAllAsync();
    }

    private void OnSafeOnly(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSafeCommand.Execute(null);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAllCommand.Execute(null);
    }

    private void OnDeselectAll(object sender, RoutedEventArgs e)
    {
        ViewModel.DeselectAllCommand.Execute(null);
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            ViewModel.Log($"filter -> {tag}");
            // Category filtering is applied in CleaningPipelineViewModel.MatchesFilter
            // (unit-tested). This port reflects chip state visually; server-side
            // filtering of the target list can be added when the filter chips
            // are wired to ItemsSource filtering.
        }
    }

    private void OnLogRequested(string message, LogLevel level)
        => ShellDiagnostics.Write($"[{level}] {message}");

    /// <summary>The blocking yes/no prompt for destructive clean. Cancel is the default so a stray Enter cannot clean.</summary>
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
