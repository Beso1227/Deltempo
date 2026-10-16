using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the Windows Memory Cleaner modal.
///
/// No dialogs to replace here (unlike the locked-files inspector), so this file
/// is wiring only. All behavior lives in <see cref="MemoryOptimizerViewModel"/>.
/// </summary>
public sealed partial class MemoryOptimizerView : UserControl
{
    public MemoryOptimizerView()
    {
        InitializeComponent();

        ViewModel = new MemoryOptimizerViewModel();
        ViewModel.CloseRequested += () => CloseRequested?.Invoke();
    }

    public event Action? CloseRequested;

    public event Action? TelemetryRefreshRequested
    {
        add
        {
            if (value is not null)
            {
                ViewModel.TelemetryRefreshRequested += value;
            }
        }
        remove
        {
            if (value is not null)
            {
                ViewModel.TelemetryRefreshRequested -= value;
            }
        }
    }

    public event Action<string, WinTempCleaner.Models.LogLevel>? LogRequested
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

    public MemoryOptimizerViewModel ViewModel { get; }

    /// <summary>Opens the modal and reads live telemetry plus the zone list.</summary>
    public void Open()
    {
        SoundService.PlayClickSound();
        Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        ViewModel.Refresh();
    }
}