using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the In-Use and Locked Files inspector.
///
/// Owns no logic: <see cref="LockedFilesViewModel"/> drives it. This file only
/// wires the WPF MessageBox calls to ContentDialog, which WinUI requires.
/// </summary>
public sealed partial class LockedFilesView : UserControl
{
    public LockedFilesView()
    {
        InitializeComponent();

        ViewModel = new LockedFilesViewModel();
        ViewModel.CloseRequested += () => CloseRequested?.Invoke();
        ViewModel.RebootScheduleCompleted += ShowScheduleOutcomeAsync;
    }

    public event Action? CloseRequested;

    public event Action<string, LogLevel>? LogRequested
    {
        add
        {
            if (value is not null)
            {
                ViewModel.LogRequested += value;
            }
        }
        remove
        {
            if (value is not null)
            {
                ViewModel.LogRequested -= value;
            }
        }
    }

    public LockedFilesViewModel ViewModel { get; }

    /// <summary>Mirrors the WPF PopulateAndOpen(items) entry point.</summary>
    public void Load(IEnumerable<LockedFileItem> lockedItems)
    {
        ViewModel.Load(lockedItems);
        Visibility = Visibility.Visible;
    }

    private async void ShowScheduleOutcomeAsync(RebootScheduleOutcome outcome)
    {
        // WinUI has no MessageBox; ContentDialog is the supported replacement and
        // needs an explicit XamlRoot when raised outside the visual tree load path.
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            CloseButtonText = "OK",
        };

        if (outcome.ElevationRequired)
        {
            dialog.Title = "Elevation Required";
            dialog.Content =
                "Unable to register reboot purge for the selected files. Elevated Administrator " +
                "privileges are required to configure Session Manager pending operations.";
        }
        else
        {
            dialog.Title = "Reboot Purge Scheduled";
            dialog.Content =
                $"Successfully scheduled {outcome.ScheduledCount:N0} file(s) for automatic deletion on " +
                "the next Windows reboot.\n\nFiles will be purged by the Windows Session Manager before any apps launch.";
        }

        await dialog.ShowAsync();
    }
}