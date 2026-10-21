using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 host for the AppData footprint report.
///
/// Owns no measurement logic: <see cref="AppDataFootprintViewModel"/> does the
/// work and exposes commands, so this file is wiring only.
/// </summary>
public sealed partial class AppDataFootprintView : UserControl
{
    private bool _opened;

    public AppDataFootprintView()
    {
        InitializeComponent();

        ViewModel = new AppDataFootprintViewModel();
        ViewModel.CloseRequested += () => CloseRequested?.Invoke();
    }

    public event Action? CloseRequested;

    /// <summary>Raised with app-log lines, forwarded from the ViewModel.</summary>
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

    public AppDataFootprintViewModel ViewModel { get; }

    /// <summary>Opens the report and starts the first measurement.</summary>
    public void PopulateAndOpen()
    {
        if (_opened)
        {
            return;
        }

        _opened = true;
        ViewModel.RefreshCommand.Execute(null);
    }
}
