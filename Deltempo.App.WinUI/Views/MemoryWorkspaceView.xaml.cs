using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of the memory hero card from MainWindow.xaml plus
/// MainWindow.Memory.cs.
///
/// This is a WORKSPACE panel, not an overlay: it fills the content column for
/// the "Memory Optimization Tool" nav item rather than floating above the
/// workspace underneath it.
///
/// Everything except the boost click lives in the ViewModel. The click handler
/// exists only because the boost is an async operation and this view owns no
/// other async surface; the actual privileged NT work is
/// MemoryOptimizationViewModel.ExecuteBoostAsync, which is never invoked by the
/// self-test.
/// </summary>
public sealed partial class MemoryWorkspaceView : UserControl
{
    public MemoryWorkspaceView()
    {
        ViewModel = new MemoryWorkspaceViewModel();
        InitializeComponent();
    }

    public MemoryWorkspaceViewModel ViewModel { get; }

    private async void OnBoostClick(object sender, RoutedEventArgs e)
        => await ViewModel.ExecuteBoostAsync();
}