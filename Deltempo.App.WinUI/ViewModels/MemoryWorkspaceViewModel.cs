using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

// Fully qualified on purpose. WinTempCleaner.ViewModels also declares a
// RelayCommand, so `using WinTempCleaner.ViewModels;` makes every
// RelayCommand reference in this file ambiguous. Naming the dependency
// explicitly is cheaper than discovering that again in the next file.
using MemoryOptimizationViewModel = WinTempCleaner.ViewModels.MemoryOptimizationViewModel;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// WinUI 3 port of the MainWindow memory hero card and its Quick Boost action.
///
/// In the WPF window this logic lived in MainWindow.Memory.cs (58 lines) as a
/// hand-maintained PropertyChanged subscription that poked named TextBlocks and
/// ProgressBars one at a time. Six named controls to remember; adding a seventh
/// meant remembering to update it in two places.
///
/// Here the hero card binds to these properties directly and the detail overlay
/// is opened by a command, so there is nothing to keep in sync by hand.
///
/// Telemetry is pushed in from the shell's timer via RefreshTelemetry, matching
/// the WPF arrangement where MainWindow owned the refresh cadence. The boost
/// path is the same MemoryOptimizationViewModel the WPF hero used, so the
/// selection rules and the privileged NT calls are unchanged.
public sealed partial class MemoryWorkspaceViewModel : ObservableObject
{
    private readonly MemoryOptimizationViewModel _boost = new();

    public MemoryWorkspaceViewModel()
    {
        OpenDetailedViewCommand = new RelayCommand(OpenDetailedView);

        _boost.PropertyChanged += (_, e) =>
        {
            // Mirror the boost ViewModel's state onto the hero card. The WPF
            // version did the same in a PropertyChanged handler, but against
            // named controls rather than against bound properties.
            if (e.PropertyName is nameof(MemoryOptimizationViewModel.UsedPercent))
            {
                OnPropertyChanged(nameof(UsedPercent));
                OnPropertyChanged(nameof(UsedPercentText));
                OnPropertyChanged(nameof(RamDetailText));
            }
            else if (e.PropertyName is nameof(MemoryOptimizationViewModel.IsBoosting))
            {
                OnPropertyChanged(nameof(BoostButtonText));
                OnPropertyChanged(nameof(IsBoosting));
            }
        };
    }

    public event Action<string, LogLevel>? LogRequested;

    public event Action? DetailedViewRequested;

    [ObservableProperty]
    public partial string RamDetailText { get; set; } = "Reading RAM telemetry...";

    [ObservableProperty]
    public partial bool IsBoosting { get; set; }

    /// <summary>0-100. Drives the hero progress bar.</summary>
    public double UsedPercent => _boost.UsedPercent;

    public string UsedPercentText => $"{_boost.UsedPercent:F0}%";

    /// <summary>
    /// Button label doubles as the busy indicator. Matches the WPF hero, which
    /// swapped the content of HeroBoostRamBtn while a boost was in flight.
    /// </summary>
    public string BoostButtonText => _boost.BoostButtonText;

    public IRelayCommand OpenDetailedViewCommand { get; }

    /// <summary>
    /// Re-reads live RAM telemetry. Cheap and read-only; the shell calls this on
    /// its existing refresh cadence.
    /// </summary>
    public void RefreshTelemetry()
    {
        _boost.RefreshTelemetry();

        RamDetailText = _boost.FormattedDetail;

        OnPropertyChanged(nameof(UsedPercent));
        OnPropertyChanged(nameof(UsedPercentText));
        OnPropertyChanged(nameof(BoostButtonText));
        OnPropertyChanged(nameof(IsBoosting));
    }

    /// <summary>
    /// Quick Boost. This issues privileged NT memory operations (working-set
    /// trimming and standby cache eviction), which is why the self-test never
    /// calls it.
    /// </summary>
    public async Task ExecuteBoostAsync()
    {
        _boost.LogRequested += OnBoostLog;
        IsBoosting = true;

        try
        {
            await _boost.ExecuteBoostAsync();
            RefreshTelemetry();
        }
        finally
        {
            _boost.LogRequested -= OnBoostLog;
            IsBoosting = false;
            OnPropertyChanged(nameof(IsBoosting));
            OnPropertyChanged(nameof(BoostButtonText));
        }
    }

    private void OnBoostLog(string message, LogLevel level)
        => LogRequested?.Invoke(message, level);

    private void OpenDetailedView()
    {
        SoundService.PlayClickSound();
        DetailedViewRequested?.Invoke();
    }
}