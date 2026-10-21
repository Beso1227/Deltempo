using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// WinUI 3 port of the Windows Memory Cleaner and Standby Cache modal.
///
/// Safety posture preserved exactly: the purge paths call the same Core
/// MemoryOptimizerService APIs the WPF modal calls, with the same selection
/// rules (a zone is only purged when it is selected AND available on this OS).
///
/// Two structural notes:
///   - MemoryAreaSnapshot.IsSelected is a plain property with no
///     INotifyPropertyChanged. The WPF modal compensated with
///     ItemsControl.Items.Refresh(); here each zone is wrapped so selection is
///     observable and the CheckBox updates without a manual refresh.
///   - IconGlyph carries Segoe MDL2 code points inherited from Core. The
///     MDL2 -> Segoe Fluent Icons audit is still outstanding; see Tokens.xaml.
/// </summary>
public sealed partial class MemoryOptimizerViewModel : ObservableObject
{
    public MemoryOptimizerViewModel()
    {
        CloseCommand = new RelayCommand(() =>
        {
            SoundService.PlayClickSound();
            CloseRequested?.Invoke();
            TelemetryRefreshRequested?.Invoke();
        });

        SelectAllCommand = new RelayCommand(() => SetAllSelected(true));
        DeselectAllCommand = new RelayCommand(() => SetAllSelected(false));

        FlushZoneCommand = new AsyncRelayCommand<MemoryZoneRowViewModel>(row =>
        {
            if (row is null)
            {
                return Task.CompletedTask;
            }

            SoundService.PlayClickSound();
            return FlushZoneAsync(row);
        });

        QuickTrimCommand = new AsyncRelayCommand(QuickTrimAsync);
        PurgeSelectedCommand = new AsyncRelayCommand(PurgeSelectedAsync);
    }

    public event Action<string, LogLevel>? LogRequested;

    public event Action? CloseRequested;

    public event Action? TelemetryRefreshRequested;

    public ObservableCollection<MemoryZoneRowViewModel> Zones { get; } = [];

    [ObservableProperty]
    public partial string TotalRamText { get; set; } = "--";

    [ObservableProperty]
    public partial string UsedRamText { get; set; } = "--";

    [ObservableProperty]
    public partial string StandbyCacheText { get; set; } = "--";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Loading memory zones...";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool CanRunPurge => !IsBusy;

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand SelectAllCommand { get; }

    public IRelayCommand DeselectAllCommand { get; }

    public IAsyncRelayCommand<MemoryZoneRowViewModel> FlushZoneCommand { get; }

    public IAsyncRelayCommand QuickTrimCommand { get; }

    public IAsyncRelayCommand PurgeSelectedCommand { get; }

    /// <summary>Mirrors the WPF RefreshData(): telemetry plus the zone list.</summary>
    public void Refresh()
    {
        try
        {
            UpdateTelemetry();

            List<MemoryAreaSnapshot> snapshots = MemoryOptimizerService.GetMemoryAreaSnapshots();

            Zones.Clear();
            foreach (MemoryAreaSnapshot snapshot in snapshots)
            {
                Zones.Add(new MemoryZoneRowViewModel(snapshot, FlushZoneCommand));
            }

            int available = snapshots.Count(s => s.IsAvailableOnThisOs);
            StatusText = $"{snapshots.Count} memory zones · {available} available for privileged NT flush";
        }
        catch (Exception ex)
        {
            StatusText = "Memory zones could not be read.";
            LogRequested?.Invoke($"Failed to refresh memory zones: {ex.Message}", LogLevel.Warning);
        }
    }

    private void UpdateTelemetry()
    {
        try
        {
            MemoryInfo mem = MemoryOptimizerService.GetMemoryInfo();
            TotalRamText = mem.FormattedTotal;
            UsedRamText = $"{mem.FormattedUsed} ({mem.UsedPercent:F0}%) In Use";
            StandbyCacheText = mem.FormattedSystemCache;
        }
        catch (Exception ex)
        {
            // Telemetry is decorative; never let it break the zone list.
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Telemetry update suppressed: {ex.Message}");
        }
    }

    private void SetAllSelected(bool selected)
    {
        SoundService.PlayClickSound();
        foreach (MemoryZoneRowViewModel zone in Zones)
        {
            zone.IsSelected = selected;
        }
    }

    private async Task QuickTrimAsync()
    {
        SoundService.PlayClickSound();
        LogRequested?.Invoke("[Process Optimizer] Trimming non-whitelisted process working sets...", LogLevel.Info);

        await RunBusyAsync(async () =>
        {
            MemoryAreaResult res = await MemoryOptimizerService.OptimizeAreaAsync(MemoryTargetType.WorkingSet);
            if (res.Success)
            {
                LogRequested?.Invoke(
                    $"✓ Process Working Sets Trimmed across {res.ProcessesOptimized ?? 0} tasks. " +
                    "Trimmed pages are re-faulted from disk on next access.",
                    LogLevel.Success);
                Refresh();
                TelemetryRefreshRequested?.Invoke();
            }
            else
            {
                LogRequested?.Invoke($"Working sets trim failed: {res.ErrorMessage}", LogLevel.Error);
            }
        });
    }

    private async Task PurgeSelectedAsync()
    {
        MemoryTargetType[] selected = Zones
            .Where(z => z.IsSelected && z.Snapshot.IsAvailableOnThisOs)
            .Select(z => z.Snapshot.Target)
            .ToArray();

        if (selected.Length == 0)
        {
            LogRequested?.Invoke("No memory zones selected for purge.", LogLevel.Warning);
            return;
        }

        SoundService.PlayClickSound();

        await RunBusyAsync(async () =>
        {
            MemoryOptimizationResult result = await MemoryOptimizerService.OptimizeRamAsync(selected);
            LogRequested?.Invoke(
                $"[NT Kernel] Memory Clean Complete: {result.SucceededTargetCount}/{result.RequestedTargetCount} " +
                $"zones confirmed by the kernel in {result.ExecutionTimeMs}ms. Available RAM moved " +
                $"{result.FormattedMeasuredDelta}; purged cache is re-read from disk on demand.",
                LogLevel.Success);
            Refresh();
            TelemetryRefreshRequested?.Invoke();
        });
    }

    private async Task FlushZoneAsync(MemoryZoneRowViewModel row)
    {
        await RunBusyAsync(async () =>
        {
            MemoryAreaResult result = await MemoryOptimizerService.OptimizeAreaAsync(row.Snapshot.Target);
            if (result.Success)
            {
                LogRequested?.Invoke(
                    $"Flushed {row.Snapshot.Target}: kernel confirmed the operation. " +
                    "Purged pages are re-read from disk on next access.",
                    LogLevel.Success);
                Refresh();
                TelemetryRefreshRequested?.Invoke();
            }
            else
            {
                LogRequested?.Invoke($"Failed to flush {row.Snapshot.Target}: {result.ErrorMessage}", LogLevel.Error);
            }
        });
    }

    /// <summary>
    /// Serializes the privileged operations and gates the actions while one runs.
    /// The WPF modal disabled only the purge button; here every command is gated
    /// because the NT purge paths are not safe to overlap.
    /// </summary>
    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(CanRunPurge));

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"Memory operation error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanRunPurge));
        }
    }
}

/// <summary>
/// One memory zone row.
///
/// Wraps <see cref="MemoryAreaSnapshot"/> so selection is observable (the Core
/// model has no notification) and so the row can own its Flush command, which
/// x:Bind cannot reach from a page-level command inside a DataTemplate.
/// </summary>
public sealed partial class MemoryZoneRowViewModel : ObservableObject
{
    public MemoryZoneRowViewModel(MemoryAreaSnapshot snapshot, IAsyncRelayCommand<MemoryZoneRowViewModel> flushCommand)
    {
        Snapshot = snapshot;
        FlushCommand = flushCommand;
        IsSelected = snapshot.IsSelected;
    }

    public MemoryAreaSnapshot Snapshot { get; }

    public string DisplayName => Snapshot.DisplayName;

    public string Description => Snapshot.Description;

    public string FormattedCurrent => Snapshot.FormattedCurrent;

    public string SafetyBadge => Snapshot.SafetyBadge;

    public string IconGlyph => Snapshot.IconGlyph;

    public string SeverityColorHex => Snapshot.SeverityColorHex;

    public bool IsAvailableOnThisOs => Snapshot.IsAvailableOnThisOs;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IAsyncRelayCommand<MemoryZoneRowViewModel> FlushCommand { get; }
}
