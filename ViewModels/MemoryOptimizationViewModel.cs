using System;
using System.Threading.Tasks;
using System.Windows.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.ViewModels;

/// <summary>
/// ViewModel for RAM telemetry and NT kernel memory optimization actions.
/// Decouples memory boosting and telemetry logic from MainWindow code-behind.
/// </summary>
public class MemoryOptimizationViewModel : ViewModelBase
{
    private double _usedPercent;
    private string _formattedUsed = "0 MB";
    private string _formattedTotal = "0 MB";
    private string _formattedDetail = "0 MB / 0 MB Used";
    private bool _isBoosting;
    private string _boostButtonText = "Quick Boost";

    public MemoryOptimizationViewModel()
    {
        BoostCommand = new AsyncRelayCommand(ExecuteBoostAsync, () => !IsBoosting);
        RefreshTelemetry();
    }

    public double UsedPercent
    {
        get => _usedPercent;
        set => SetProperty(ref _usedPercent, value);
    }

    public string FormattedUsed
    {
        get => _formattedUsed;
        set => SetProperty(ref _formattedUsed, value);
    }

    public string FormattedTotal
    {
        get => _formattedTotal;
        set => SetProperty(ref _formattedTotal, value);
    }

    public string FormattedDetail
    {
        get => _formattedDetail;
        set => SetProperty(ref _formattedDetail, value);
    }

    public bool IsBoosting
    {
        get => _isBoosting;
        private set
        {
            if (SetProperty(ref _isBoosting, value))
            {
                (BoostCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string BoostButtonText
    {
        get => _boostButtonText;
        set => SetProperty(ref _boostButtonText, value);
    }

    public ICommand BoostCommand { get; }

    public event Action<string, LogLevel>? LogRequested;

    public void RefreshTelemetry()
    {
        try
        {
            var mem = MemoryOptimizerService.GetMemoryInfo();
            UsedPercent = mem.UsedPercent;
            FormattedUsed = mem.FormattedUsed;
            FormattedTotal = mem.FormattedTotal;
            FormattedDetail = $"{mem.FormattedUsed} / {mem.FormattedTotal} Used";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[MemoryOptimizationViewModel] Telemetry read failed: {ex.Message}");
        }
    }

    public async Task<MemoryOptimizationResult?> ExecuteBoostAsync()
    {
        if (IsBoosting) return null;

        IsBoosting = true;
        BoostButtonText = "Boosting...";
        SoundService.PlayClickSound();

        try
        {
            var res = await MemoryOptimizerService.OptimizeRamAsync();
            RefreshTelemetry();
            BoostButtonText = $"✓ -{res.FormattedReclaimed}";
            LogRequested?.Invoke($"[RAM Engine] Boost Complete: Reclaimed {res.FormattedReclaimed} across {res.ProcessesOptimized} processes in {res.ExecutionTimeMs}ms.", LogLevel.Success);
            await Task.Delay(1500);
            return res;
        }
        catch (Exception ex)
        {
            LogRequested?.Invoke($"RAM optimization error: {ex.Message}", LogLevel.Warning);
            return null;
        }
        finally
        {
            BoostButtonText = "Quick Boost";
            IsBoosting = false;
        }
    }
}
