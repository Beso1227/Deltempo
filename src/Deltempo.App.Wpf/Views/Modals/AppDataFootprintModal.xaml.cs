using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

/// <summary>
/// READ-ONLY view of where AppData space goes.
///
/// There is deliberately no delete affordance in this surface. It renders
/// <see cref="AppDataFootprintEntry"/> rows and nothing else - no selection state, no
/// checkbox, no clean button - because a report that can delete is not a report. Users who want
/// a row cleaned are pointed at the existing scope that already covers it.
/// </summary>
public partial class AppDataFootprintModal : UserControl
{
    private readonly ObservableCollection<FootprintRow> _rows = new();
    private bool _measuring;

    public event Action<string, LogLevel>? LogRequested;
    public event Action? Closed;

    public AppDataFootprintModal()
    {
        InitializeComponent();
        FootprintItemsControl.ItemsSource = _rows;
    }

    /// <summary>Opens the modal and kicks off the first measurement.</summary>
    public void PopulateAndOpen()
    {
        Visibility = Visibility.Visible;
        SoundService.PlayClickSound();
        _ = MeasureAsync();
    }

    public void CloseModal()
    {
        Visibility = Visibility.Collapsed;
        SoundService.PlayClickSound();
        Closed?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseModal();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await MeasureAsync();

    /// <summary>
    /// Measures off the UI thread: Analyze walks every top-level folder in three AppData trees
    /// recursively, which is far too slow to block the dispatcher on.
    /// </summary>
    private async Task MeasureAsync()
    {
        if (_measuring) return;
        _measuring = true;

        RefreshBtn.IsEnabled = false;
        FootprintSummaryText.Text = "Measuring AppData…";
        FootprintFooterText.Text = "Scanning Roaming, Local and LocalLow. Large profiles take a moment.";

        try
        {
            var entries = await Task.Run(() => AppDataFootprintService.Analyze());

            _rows.Clear();
            foreach (var row in BuildRows(entries))
            {
                _rows.Add(row);
            }

            long total = entries.Sum(e => e.SizeBytes);
            FootprintSummaryText.Text = entries.Count == 0
                ? "No AppData folders could be measured."
                : $"{Models.TargetFolderInfo.FormatBytes(total)} across {entries.Count:N0} folders";

            foreach (var group in entries.GroupBy(e => e.RootLabel).OrderByDescending(g => g.Sum(e => e.SizeBytes)))
            {
                LogRequested?.Invoke(
                    $"AppData report - {group.Key}: {Models.TargetFolderInfo.FormatBytes(group.Sum(e => e.SizeBytes))} across {group.Count()} folders",
                    LogLevel.Info);
            }

            FootprintFooterText.Text = "Nothing in this list is selected. Close to return to the Cleaner.";
        }
        catch (Exception ex)
        {
            FootprintSummaryText.Text = "The AppData report could not be completed.";
            FootprintFooterText.Text = ex.Message;
            LogRequested?.Invoke($"AppData footprint report failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            RefreshBtn.IsEnabled = true;
            _measuring = false;
        }
    }

    private static IEnumerable<FootprintRow> BuildRows(IReadOnlyCollection<AppDataFootprintEntry> entries)
    {
        long largest = entries.Count > 0 ? entries.Max(e => e.SizeBytes) : 0;

        foreach (var e in entries)
        {
            double share = largest > 0 ? (double)e.SizeBytes / largest * 100.0 : 0;

            string badgeText;
            string badgeForeground;
            string badgeBackground;
            Brush bar;

            if (e.IsProtectedState)
            {
                badgeText = "Protected state";
                badgeForeground = "#F59E0B";
                badgeBackground = "#2A1E16";
                bar = ResolveBrush("#F59E0B");
            }
            else if (e.CoveredByExistingScope)
            {
                badgeText = "Covered by a scope";
                badgeForeground = "#10B981";
                badgeBackground = "#12241C";
                bar = ResolveBrush("#10B981");
            }
            else
            {
                badgeText = "Not currently cleaned";
                badgeForeground = "#94A3B8";
                badgeBackground = "#1B232C";
                bar = ResolveBrush("#64748B");
            }

            yield return new FootprintRow(
                $"{e.Name}  ·  {e.RootLabel}",
                e.Path,
                Models.TargetFolderInfo.FormatBytes(e.SizeBytes),
                share,
                badgeText,
                badgeForeground,
                badgeBackground,
                bar);
        }
    }

    private static Brush ResolveBrush(string hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    /// <summary>
    /// Presentation-only row. One-way data, no setters, so nothing here can be turned into a
    /// deletion target.
    /// </summary>
    private sealed record FootprintRow(
        string DisplayName,
        string Path,
        string FormattedSize,
        double SharePercent,
        string BadgeText,
        string BadgeForeground,
        string BadgeBackground,
        Brush BarBrush);
}
