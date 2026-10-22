using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// Workspace panel for the process manager. The buttons live inside a DataTemplate,
/// so their click handlers run here and pick the row out of the DataContext instead
/// of trying to bind a command through the template - which cannot reach the panel's
/// ViewModel from inside a DataTemplate.
/// </summary>
public sealed partial class ProcessWorkspaceView : UserControl
{
    public ProcessWorkspaceView()
    {
        InitializeComponent();

        ViewModel.LogRequested += OnLog;
    }

    public ProcessWorkspaceViewModel ViewModel { get; } = new();

    public event Action<string, LogLevel>? LogRequested;

    private void OnLog(string message, LogLevel level)
        => LogRequested?.Invoke(message, level);

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
        => await ViewModel.RefreshAsync();

    private async void OnTrimClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProcessRow row)
        {
            await ViewModel.TrimCommand.ExecuteAsync(row);
        }
    }

    private async void OnEndTaskClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProcessRow row)
        {
            await ViewModel.EndTaskCommand.ExecuteAsync(row);
        }
    }
}
