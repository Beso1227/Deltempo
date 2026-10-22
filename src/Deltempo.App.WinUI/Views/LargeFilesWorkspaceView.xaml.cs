using Deltempo.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System.Diagnostics;
using WinTempCleaner.Core.Discovery;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.Views;

/// <summary>
/// WinUI 3 port of the Large File Hunter workspace.
///
/// This is a WORKSPACE panel: it fills the content column for the "Large Files
/// Tool" nav item. The scan is read-only; recycling is gated behind an injected
/// confirmation that defaults to refusing.
///
/// Self-test policy: Open() calls ScanAsync, which only enumerates files and
/// classifies them. RecycleSelectedAsync / RecycleItemAsync / AnalyzeItemAsync
/// are never invoked by the self-test.
/// </summary>
public sealed partial class LargeFilesWorkspaceView : UserControl
{
    public LargeFilesWorkspaceView()
    {
        ViewModel = new LargeFilesWorkspaceViewModel(
            confirmAsync: AskForConfirmationAsync,
            openFolder: OpenInFolder);
        InitializeComponent();

        // Scope chips are data-driven: a fixed pair could not scan one drive.
        PopulateScopeChips();

        // Toggle list/treemap visibility when the view mode changes.
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LargeFilesWorkspaceViewModel.ActiveViewMode))
            {
                ApplyViewModeVisibility();
            }
            else if (e.PropertyName == nameof(LargeFilesWorkspaceViewModel.HasItems))
            {
                ApplyViewModeVisibility();
            }
        };
    }

    public LargeFilesWorkspaceViewModel ViewModel { get; }

    /// <summary>Read-only scan of drives for large files. Safe to call from the self-test.</summary>
    public async void Open()
    {
        await ViewModel.ScanAsync();
    }

    private void ApplyViewModeVisibility()
    {
        bool isTreemap = ViewModel.ActiveViewMode == LargeFileViewMode.Treemap;
        bool hasItems = ViewModel.HasItems;

        LargeFilesList.Visibility = isTreemap || !hasItems
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

        TreemapCanvas.Visibility = isTreemap && hasItems
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

        if (isTreemap && hasItems)
        {
            RenderTreemap();
        }
    }

    private void RenderTreemap()
    {
        TreemapCanvas.Children.Clear();

        double w = TreemapCanvas.ActualWidth;
        double h = TreemapCanvas.ActualHeight;
        if (w <= 20 || h <= 20 || ViewModel.Items.Count == 0) return;

        var nodes = TreemapLayoutEngine.CalculateLayout(
            ViewModel.Items,
            f => f.SizeBytes,
            w,
            h,
            maxTiles: 120);

        foreach (var node in nodes)
        {
            var item = node.Item;
            double tileX = Math.Max(0, node.X + 1.5);
            double tileY = Math.Max(0, node.Y + 1.5);
            double tileW = Math.Max(2, node.Width - 3);
            double tileH = Math.Max(2, node.Height - 3);

            var border = new Border
            {
                Width = tileW,
                Height = tileH,
                CornerRadius = new CornerRadius(6),
                BorderThickness = item.IsSelected ? new Thickness(2) : new Thickness(1),
                Background = new SolidColorBrush(ParseColor(item.IsAiSafe ? "#13271E" : "#27161A")),
                BorderBrush = new SolidColorBrush(ParseColor(item.IsAiSafe ? "#00E676" : "#FF8A80")),
            };

            // WinUI has no FrameworkElement.ToolTip (that is WPF); the attached
            // property is the only way to set one from code.
            ToolTipService.SetToolTip(border, new ToolTip
            {
                Content = $"{item.FileName}\nSize: {item.FormattedSize}\nCategory: {item.Category}\nVerdict: {item.VerdictShort}\n{item.FilePath}\n\nClick to select"
            });

            if (tileW > 45 && tileH > 28)
            {
                var panel = new StackPanel
                {
                    Margin = new Thickness(4, 3, 4, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var nameText = new TextBlock
                {
                    Text = item.FileName,
                    FontSize = tileW > 100 ? 11 : 9.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(ParseColor("#F8FAFC")),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = Math.Max(10, tileW - 8)
                };
                panel.Children.Add(nameText);

                if (tileH > 46)
                {
                    var sizeText = new TextBlock
                    {
                        Text = item.FormattedSize,
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(
                            item.IsAiSafe ? ParseColor("#00E676") : ParseColor("#FF8A80")),
                        Margin = new Thickness(0, 1, 0, 0)
                    };
                    panel.Children.Add(sizeText);
                }

                border.Child = panel;
            }

            border.Tapped += (_, _) =>
            {
                ViewModel.ToggleSelectionCommand.Execute(item);
            };

            Canvas.SetLeft(border, tileX);
            Canvas.SetTop(border, tileY);
            TreemapCanvas.Children.Add(border);
        }
    }

    private void OnTreemapSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel.ActiveViewMode == LargeFileViewMode.Treemap && ViewModel.HasItems)
        {
            RenderTreemap();
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        // "#RRGGBB" -> Windows.UI.Color
        string h = hex.TrimStart('#');
        byte r = byte.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    /// <summary>Maps a chip's Tag back onto the scope/size/mode and re-scans.</summary>
    private void OnScopeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            ViewModel.ScanScope = tag;
            SoundService.PlayClickSound();
            _ = ViewModel.ScanAsync();
        }
    }

    private void OnSizeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && long.TryParse(tag, out long bytes) && bytes > 0)
        {
            ViewModel.MinSizeBytes = bytes;
            SoundService.PlayClickSound();
            _ = ViewModel.ScanAsync();
        }
    }

    /// <summary>
    /// Builds the scope chips in the same order as the WPF combo: All Drives, then one
    /// chip per drive reported by the shared hunter, then User Profile &amp; OneDrive.
    /// </summary>
    private void PopulateScopeChips()
    {
        if (ScopeChips.Children.Count > 0) return;

        AddScopeChip("All Drives", "ALL", isChecked: true);
        foreach (string drive in LargeFileHunterService.GetAvailableDrives())
        {
            AddScopeChip($"Drive {drive}", drive);
        }
        AddScopeChip("User Profile", "USER");
    }

    private void AddScopeChip(string label, string tag, bool isChecked = false)
    {
        var chip = new RadioButton
        {
            Content = label,
            Tag = tag,
            GroupName = "LargeFileScope",
            Style = (Style)Resources["LargeFileChip"],
            IsChecked = isChecked,
        };
        chip.Checked += OnScopeChanged;
        ScopeChips.Children.Add(chip);
    }

    private void OnViewModeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            var mode = tag == "TREEMAP" ? LargeFileViewMode.Treemap : LargeFileViewMode.List;
            ViewModel.ViewModeCommand.Execute(mode);
        }
    }

    /// <summary>The blocking yes/no prompt for recycle. Cancel is the default so a stray Enter cannot recycle files.</summary>
    private async Task<bool> AskForConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                FontSize = 13,
            },
            PrimaryButtonText = "Yes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private void OpenInFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch
        {
            // Reveal is a best-effort convenience; never fail the app because of it.
        }
    }
}
