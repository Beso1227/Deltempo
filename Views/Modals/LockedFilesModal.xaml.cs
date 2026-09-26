using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

public partial class LockedFilesModal : UserControl
{
    private readonly ObservableCollection<LockedFileItem> _items = new();

    public event Action<string, LogLevel>? LogRequested;
    public event Action? Closed;

    public LockedFilesModal()
    {
        InitializeComponent();
        LockedFilesItemsControl.ItemsSource = _items;
    }

    public void PopulateAndOpen(IEnumerable<LockedFileItem> lockedItems)
    {
        _items.Clear();
        foreach (var item in lockedItems)
        {
            _items.Add(item);
        }

        LockedSummaryText.Text = $"{_items.Count:N0} in-use file{(_items.Count == 1 ? "" : "s")} held by active applications";
        Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
    }

    public void CloseModal()
    {
        Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        Closed?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseModal();
    }

    private void ScheduleSingleReboot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LockedFileItem item)
        {
            try
            {
                bool success = RestartManagerService.ScheduleRebootDeletion(item.FilePath);
                if (success)
                {
                    item.StatusText = "✓ Scheduled for Reboot";
                    LogRequested?.Invoke($"Registered '{item.FileName}' for deletion upon next system reboot", LogLevel.Success);
                }
                else
                {
                    item.StatusText = "Requires Admin";
                    LogRequested?.Invoke($"Could not schedule '{item.FileName}' for reboot deletion (Administrator rights required)", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                item.StatusText = "Failed";
                LogRequested?.Invoke($"Error scheduling reboot deletion for '{item.FileName}': {ex.Message}", LogLevel.Error);
            }
        }
    }

    private void ScheduleAllReboot_Click(object sender, RoutedEventArgs e)
    {
        int scheduled = 0;
        int failed = 0;

        foreach (var item in _items)
        {
            try
            {
                if (RestartManagerService.ScheduleRebootDeletion(item.FilePath))
                {
                    item.StatusText = "✓ Scheduled for Reboot";
                    scheduled++;
                }
                else
                {
                    item.StatusText = "Requires Admin";
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        if (scheduled > 0)
        {
            LogRequested?.Invoke($"Scheduled {scheduled:N0} in-use file(s) for deletion upon next system reboot via Session Manager", LogLevel.Success);
            MessageBox.Show(
                $"Successfully scheduled {scheduled:N0} file(s) for automatic deletion on the next Windows reboot.\n\nFiles will be purged by the Windows Session Manager before any apps launch.",
                "Reboot Purge Scheduled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else if (failed > 0)
        {
            MessageBox.Show(
                "Unable to register reboot purge for the selected files. Elevated Administrator privileges are required to configure Session Manager pending operations.",
                "Elevation Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
