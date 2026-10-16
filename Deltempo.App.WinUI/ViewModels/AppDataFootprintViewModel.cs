using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// WinUI 3 port of the AppData footprint report.
///
/// This is a REPORT. There is no delete affordance, no selection state and no
/// checkbox - the same contract the WPF modal documents at
/// Views/Modals/AppDataFootprintModal.xaml.cs. Rows are presentation-only, so
/// nothing here can be turned into a deletion target.
///
/// The measuring work stays off the UI thread: Analyze walks every top-level
/// folder in three AppData trees recursively, which blocks far too long to run
/// on the dispatcher.
///
/// Properties use CommunityToolkit.Mvvm partial properties rather than
/// [ObservableProperty] fields: the WinUI 3 / CsWinRT generators require
/// partial properties for AOT-compatible marshalling (MVVMTK0045).
/// </summary>
public sealed partial class AppDataFootprintViewModel : ObservableObject
{
    /// <summary>Fixed share-bar track width. Must match the track in the view.</summary>
    private const double ShareTrackWidth = 150;

    public AppDataFootprintViewModel()
    {
        // Partial properties cannot carry initializers, so defaults are set here.
        SummaryText = "Press Re-measure to scan AppData.";
        FooterText = "Report only - nothing here is selected, and nothing is deleted.";

        RefreshCommand = new AsyncRelayCommand(MeasureAsync);
        CloseCommand = new RelayCommand(() =>
        {
            SoundService.PlayClickSound();
            CloseRequested?.Invoke();
        });
    }

    /// <summary>Raised with app-log lines. The shell subscribes when it gains a log pane.</summary>
    public event Action<string, LogLevel>? LogRequested;

    /// <summary>Raised when the user closes the report.</summary>
    public event Action? CloseRequested;

    public ObservableCollection<FootprintRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; }

    [ObservableProperty]
    public partial string FooterText { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Inverse of <see cref="IsBusy"/>, so the button can bind IsEnabled directly.</summary>
    public bool CanRefresh => !IsBusy;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand CloseCommand { get; }

    private async Task MeasureAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SoundService.PlayClickSound();
        IsBusy = true;
        OnPropertyChanged(nameof(CanRefresh));
        SummaryText = "Measuring AppData...";
        FooterText = "Scanning Roaming, Local and LocalLow. Large profiles take a moment.";

        try
        {
            // Read-only analysis. No targets are registered and nothing is deleted.
            List<AppDataFootprintEntry> entries =
                await Task.Run(() => AppDataFootprintService.Analyze());

            long largest = entries.Count > 0 ? entries.Max(e => e.SizeBytes) : 0;

            Rows.Clear();
            foreach (AppDataFootprintEntry entry in entries)
            {
                Rows.Add(FootprintRowViewModel.FromEntry(entry, largest));
            }

            long total = entries.Sum(e => e.SizeBytes);
            SummaryText = entries.Count == 0
                ? "No AppData folders could be measured."
                : $"{TargetFolderInfo.FormatBytes(total)} across {entries.Count:N0} folders";

            foreach (var group in entries
                         .GroupBy(e => e.RootLabel)
                         .OrderByDescending(g => g.Sum(e => e.SizeBytes)))
            {
                LogRequested?.Invoke(
                    $"AppData report - {group.Key}: {TargetFolderInfo.FormatBytes(group.Sum(e => e.SizeBytes))} " +
                    $"across {group.Count()} folders",
                    LogLevel.Info);
            }

            FooterText = "Nothing in this list is selected. Close to return to the Cleaner.";
        }
        catch (Exception ex)
        {
            SummaryText = "The AppData report could not be completed.";
            FooterText = ex.Message;
            LogRequested?.Invoke($"AppData footprint report failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanRefresh));
        }
    }
}

/// <summary>
/// Presentation-only row. No setters after construction, no commands, and no
/// identifier a cleanup path could consume - the read-only guarantee is structural.
/// </summary>
public sealed record FootprintRowViewModel
{
    /// <summary>Fixed share-bar track width in pixels; matches the track in the view.</summary>
    private const double ShareTrackWidth = 150;

    public required string DisplayName { get; init; }

    public required string Path { get; init; }

    public required string FormattedSize { get; init; }

    /// <summary>0-100, relative to the largest folder in the report.</summary>
    public required double SharePercent { get; init; }

    /// <summary>
    /// Bar width in pixels. The WPF original used a MultiBinding over SharePercent
    /// plus the measured grid width; WinUI 3 has no MultiBinding, so the width is
    /// resolved here against the fixed track instead.
    /// </summary>
    public double BarWidth => SharePercent / 100.0 * ShareTrackWidth;

    public required string BadgeText { get; init; }

    /// <summary>ARGB hex, resolved to a brush by the view's converter.</summary>
    public required string BadgeForeground { get; init; }

    /// <summary>ARGB hex, resolved to a brush by the view's converter.</summary>
    public required string BadgeBackground { get; init; }

    /// <summary>ARGB hex, resolved to a brush by the view's converter.</summary>
    public required string BarColor { get; init; }

    public static FootprintRowViewModel FromEntry(AppDataFootprintEntry entry, long largestBytes)
    {
        double share = largestBytes > 0 ? (double)entry.SizeBytes / largestBytes * 100.0 : 0;

        (string badgeText, string foreground, string background, string bar) = entry switch
        {
            { IsProtectedState: true } => ("Protected state", "#FFF59E0B", "#FF2A1E16", "#FFF59E0B"),
            { CoveredByExistingScope: true } => ("Covered by a scope", "#FF10B981", "#FF12241C", "#FF10B981"),
            _ => ("Not currently cleaned", "#FF94A3B8", "#FF1B232C", "#FF64748B"),
        };

        return new FootprintRowViewModel
        {
            DisplayName = $"{entry.Name}  ·  {entry.RootLabel}",
            Path = entry.Path,
            FormattedSize = TargetFolderInfo.FormatBytes(entry.SizeBytes),
            SharePercent = share,
            BadgeText = badgeText,
            BadgeForeground = foreground,
            BadgeBackground = background,
            BarColor = bar,
        };
    }
}