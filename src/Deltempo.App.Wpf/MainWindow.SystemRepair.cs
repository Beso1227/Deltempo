using System.Windows;

namespace WinTempCleaner;

// System integrity repair modal coordination.
public partial class MainWindow
{
    private void OpenSystemRepairModal_Click(object sender, RoutedEventArgs e)
    {
        SwitchWorkspaceView(WorkspaceView.SystemRepair);
    }

    private void CloseSystemRepairModal_Click(object sender, RoutedEventArgs e)
    {
        if (SystemRepairModalOverlay.CloseModal())
        {
            SwitchWorkspaceView(WorkspaceView.Cleaner);
        }
    }
}
