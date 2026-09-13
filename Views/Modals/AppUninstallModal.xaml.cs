using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace WinTempCleaner.Views.Modals;

public partial class AppUninstallModal : UserControl
{
    private List<InstalledAppItem> _allApps = new();
    private InstalledAppItem? _currentInspectedApp;
    private RootScanResult? _currentScanResult;
    private string _currentAppFilter = "All";
    private string _currentDrawerFilter = "All";

    public event Action<string, LogLevel>? LogRequested;
    public event Action? Closed;

    public AppUninstallModal()
    {
        InitializeComponent();
    }

    public void Open()
    {
        SoundService.PlayClickSound();
        Visibility = Visibility.Visible;
        _ = LoadAppsAsync();
    }

    public void CloseModal()
    {
        SoundService.PlayClickSound();
        Visibility = Visibility.Collapsed;
        RootLeftoversDrawer.Visibility = Visibility.Collapsed;
        Closed?.Invoke();
    }

    private void CloseAppUninstallModal_Click(object sender, RoutedEventArgs e)
    {
        CloseModal();
    }

    private async Task LoadAppsAsync()
    {
        StatusFooterText.Text = "Auditing Windows registry and installation manifests...";
        AppCountBadge.Text = "Scanning...";

        try
        {
            var apps = await Task.Run(() => InstalledAppService.GetInstalledApps());
            _allApps = apps.ToList();
            ApplyFilter();
            StatusFooterText.Text = $"Loaded {_allApps.Count} applications. Ready for deep root inspection.";
        }
        catch (Exception ex)
        {
            StatusFooterText.Text = $"Error auditing applications: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Error: {ex.Message}", LogLevel.Warning);
        }
    }

    private void ApplyFilter()
    {
        string query = SearchAppTextBox.Text.Trim();
        IEnumerable<InstalledAppItem> queryable = _allApps;

        // Apply Search across name, publisher, engine, category, and description
        if (!string.IsNullOrWhiteSpace(query))
        {
            queryable = queryable.Where(a =>
                a.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.UninstallEngine.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(a.Category) && a.Category.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(a.Description) && a.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply Filter Chip
        switch (_currentAppFilter)
        {
            case "Browsers":
                queryable = queryable.Where(a => string.Equals(a.Category, "Browsers", StringComparison.OrdinalIgnoreCase) || string.Equals(a.Category, "Browser", StringComparison.OrdinalIgnoreCase));
                break;
            case "Gaming":
                queryable = queryable.Where(a => string.Equals(a.Category, "Gaming", StringComparison.OrdinalIgnoreCase) || string.Equals(a.Category, "Games", StringComparison.OrdinalIgnoreCase));
                break;
            case "Dev Tools":
                queryable = queryable.Where(a => string.Equals(a.Category, "Development", StringComparison.OrdinalIgnoreCase) || string.Equals(a.Category, "Dev Tools", StringComparison.OrdinalIgnoreCase));
                break;
            case "Runtimes":
                queryable = queryable.Where(a => a.IsCoreRuntime || string.Equals(a.Category, "Runtime", StringComparison.OrdinalIgnoreCase) || string.Equals(a.Category, "Runtimes", StringComparison.OrdinalIgnoreCase));
                break;
            case "Large":
                queryable = queryable.Where(a => a.EstimatedSizeBytes >= 500L * 1024 * 1024);
                break;
            case "Recent":
                queryable = queryable.Where(a => !string.IsNullOrWhiteSpace(a.InstallDate));
                break;
            case "Broken":
                queryable = queryable.Where(a => a.IsBroken);
                break;
        }

        var filtered = queryable.ToList();
        AppsListView.ItemsSource = filtered;
        AppCountBadge.Text = $"{filtered.Count} Apps (Total: {_allApps.Count})";
    }

    private void SearchAppTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchAppPlaceholder != null)
        {
            SearchAppPlaceholder.Visibility = string.IsNullOrEmpty(SearchAppTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        ApplyFilter();
    }

    private void FilterTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            SoundService.PlayClickSound();
            FilterAllBtn.Tag = null;
            FilterBrowsersBtn.Tag = null;
            FilterGamingBtn.Tag = null;
            FilterDevBtn.Tag = null;
            FilterRuntimesBtn.Tag = null;
            FilterLargeBtn.Tag = null;
            FilterBrokenBtn.Tag = null;

            btn.Tag = "Active";

            if (btn == FilterBrowsersBtn) _currentAppFilter = "Browsers";
            else if (btn == FilterGamingBtn) _currentAppFilter = "Gaming";
            else if (btn == FilterDevBtn) _currentAppFilter = "Dev Tools";
            else if (btn == FilterRuntimesBtn) _currentAppFilter = "Runtimes";
            else if (btn == FilterLargeBtn) _currentAppFilter = "Large";
            else if (btn == FilterBrokenBtn) _currentAppFilter = "Broken";
            else _currentAppFilter = "All";

            ApplyFilter();
        }
    }

    private async void RefreshAppsBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        await LoadAppsAsync();
    }

    private void AppItemToggleExpand_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is FrameworkElement elem && elem.DataContext is InstalledAppItem app)
        {
            app.IsExpanded = !app.IsExpanded;
        }
    }

    private void OpenInstallLocation_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is FrameworkElement elem && elem.DataContext is InstalledAppItem app)
        {
            string? targetPath = null;
            if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
            {
                targetPath = app.InstallLocation;
            }
            else if (!string.IsNullOrWhiteSpace(app.InstallLocation))
            {
                string? parent = Path.GetDirectoryName(app.InstallLocation);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    targetPath = parent;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetPath,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }

    #region AI Intelligence Integration

    private async void FetchSingleAppAi_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is InstalledAppItem app)
        {
            btn.IsEnabled = false;
            app.IsExpanded = true;
            StatusFooterText.Text = $"Consulting AI engine for {app.DisplayName}...";
            try
            {
                await AnalyzeAppWithAiAsync(app, forceOnline: true);
                StatusFooterText.Text = $"AI intelligence updated for {app.DisplayName}.";
                ApplyFilter();
                if (_currentInspectedApp == app)
                {
                    UpdateDrawerAiCard(app);
                }
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void DrawerFetchAiBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInspectedApp == null) return;
        SoundService.PlayClickSound();
        DrawerFetchAiBtn.IsEnabled = false;
        StatusFooterText.Text = $"Consulting AI engine for {_currentInspectedApp.DisplayName}...";
        try
        {
            await AnalyzeAppWithAiAsync(_currentInspectedApp, forceOnline: true);
            UpdateDrawerAiCard(_currentInspectedApp);
            ApplyFilter();
            StatusFooterText.Text = $"AI intelligence updated for {_currentInspectedApp.DisplayName}.";
        }
        finally
        {
            DrawerFetchAiBtn.IsEnabled = true;
        }
    }

    private async void FetchAllAiBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        var unclassifiedOrGeneric = _allApps
            .Where(a => !a.IsAiEnriched && (a.Category == "General" || a.Category == "Unknown" || a.Description.Contains("Windows application installed on this system.")))
            .ToList();

        if (unclassifiedOrGeneric.Count == 0)
        {
            MessageBox.Show("All applications have already been classified or enriched with intelligence!", "AI Analysis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        FetchAllAiBtn.IsEnabled = false;
        StatusFooterText.Text = $"Batch AI analyzing {unclassifiedOrGeneric.Count} applications...";
        int processed = 0;

        try
        {
            foreach (var app in unclassifiedOrGeneric)
            {
                StatusFooterText.Text = $"AI analyzing [{++processed}/{unclassifiedOrGeneric.Count}]: {app.DisplayName}...";
                await AnalyzeAppWithAiAsync(app, forceOnline: false);
            }
            StatusFooterText.Text = $"Batch AI analysis completed for {unclassifiedOrGeneric.Count} applications.";
            ApplyFilter();
        }
        finally
        {
            FetchAllAiBtn.IsEnabled = true;
        }
    }

    private async Task AnalyzeAppWithAiAsync(InstalledAppItem app, bool forceOnline = false)
    {
        var report = await AppIntelligenceService.AnalyzeAppAsync(app, forceOnline);
        if (report != null)
        {
            app.Description = report.Description;
            app.Category = report.Category;
            app.SafetyAdvice = report.SafetyAdvice;
            app.SafetyVerdict = report.SafetyVerdict;
            app.IsCoreRuntime = report.IsCoreRuntime;
            app.IsAiEnriched = report.IsAiGenerated;
            app.AiProviderUsed = report.ProviderUsed;
        }
    }

    private void UpdateDrawerAiCard(InstalledAppItem app)
    {
        DrawerAppNameText.Text = app.DisplayName;
        DrawerCategoryText.Text = app.Category;
        DrawerSafetyText.Text = app.SafetyVerdict;
        DrawerProviderText.Text = app.IsAiEnriched ? (string.IsNullOrEmpty(app.AiProviderUsed) ? "AI Verified" : app.AiProviderUsed) : "Catalog";
        DrawerAppDescriptionText.Text = app.Description;
        DrawerAppImpactText.Text = string.IsNullOrEmpty(app.SafetyAdvice) ? "Safe to remove if no longer needed." : app.SafetyAdvice;

        try
        {
            var categoryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(app.CategoryBadgeColor);
            DrawerCategoryText.Foreground = new System.Windows.Media.SolidColorBrush(categoryColor);
            DrawerCategoryBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(28, categoryColor.R, categoryColor.G, categoryColor.B));

            var safetyColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(app.SafetyBadgeColor);
            DrawerSafetyText.Foreground = new System.Windows.Media.SolidColorBrush(safetyColor);
            DrawerSafetyBadge.BorderBrush = new System.Windows.Media.SolidColorBrush(safetyColor);
            DrawerSafetyBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(25, safetyColor.R, safetyColor.G, safetyColor.B));

            if (app.AppIcon != null)
            {
                DrawerAppIconImage.Source = app.AppIcon;
                DrawerAppIconImage.Visibility = Visibility.Visible;
                DrawerIconFallbackBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                DrawerAppIconImage.Source = null;
                DrawerAppIconImage.Visibility = Visibility.Collapsed;
                DrawerIconFallbackBorder.Visibility = Visibility.Visible;
                DrawerIconFallbackGlyph.Text = app.FallbackGlyph;
                DrawerIconFallbackGlyph.Foreground = DrawerCategoryText.Foreground;
                DrawerIconFallbackBorder.Background = DrawerCategoryBadge.Background;
            }
        }
        catch
        {
            // Fallback safe brush
        }
    }

    #endregion

    #region Trace Scanning & Root Leftovers Drawer

    private async void ScanTracesBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is InstalledAppItem app)
        {
            await InspectAppTracesAsync(app);
        }
    }

    private async void ForceRootCleanBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is InstalledAppItem app)
        {
            var confirm = MessageBox.Show(
                $"Force Root Clean will scan and directly purge all file, registry, and shortcut remnants of:\n\n{app.DisplayName}\n\nUse this when the official uninstaller is broken or missing. Proceed?",
                "Force Root Clean", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            await InspectAppTracesAsync(app);
        }
    }

    private async void DeepUninstallBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (sender is Button btn && btn.DataContext is InstalledAppItem app)
        {
            if (string.IsNullOrWhiteSpace(app.UninstallString) && string.IsNullOrWhiteSpace(app.QuietUninstallString))
            {
                var forceConfirm = MessageBox.Show(
                    $"No official uninstaller was detected for {app.DisplayName}.\n\nWould you like to perform a Force Root Clean instead?",
                    "Uninstaller Not Found", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (forceConfirm == MessageBoxResult.Yes)
                {
                    await InspectAppTracesAsync(app);
                }
                return;
            }

            var confirm = MessageBox.Show(
                $"Deep Uninstall will:\n" +
                $"1. Terminate running processes for {app.DisplayName}\n" +
                $"2. Run the official uninstaller ({app.UninstallEngine})\n" +
                $"3. Perform a Deep Root Scan to eradicate leftover traces\n\n" +
                $"Proceed with uninstallation?",
                "Deep Root Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            StatusFooterText.Text = $"Terminating background processes and launching uninstaller for {app.DisplayName}...";
            LogRequested?.Invoke($"[App Uninstaller] Initiating deep uninstall for {app.DisplayName} ({app.UninstallEngine})...", LogLevel.Info);

            bool success = await InstalledAppService.UninstallAppAsync(app, silent: false);
            if (success)
            {
                StatusFooterText.Text = $"Official uninstaller finished. Scanning roots for leftover traces...";
                LogRequested?.Invoke($"[App Uninstaller] Uninstaller process exited. Scanning root remnants...", LogLevel.Info);
                await InspectAppTracesAsync(app);
            }
            else
            {
                StatusFooterText.Text = $"Uninstaller could not be completed or timed out.";
                LogRequested?.Invoke($"[App Uninstaller] Uninstallation failed or was cancelled.", LogLevel.Warning);
            }
        }
    }

    private async Task InspectAppTracesAsync(InstalledAppItem app)
    {
        _currentInspectedApp = app;
        RootDrawerTitle.Text = $"Root Remnants: {app.DisplayName}";
        RootReclaimableSizeBadge.Text = "Scanning...";
        UpdateDrawerAiCard(app);
        RootLeftoversDrawer.Visibility = Visibility.Visible;
        StatusFooterText.Text = $"Scanning registry, user profile, and system folders for {app.DisplayName}...";

        var result = await RootLeftoverScannerService.ScanAppTracesAsync(app);
        _currentScanResult = result;

        RootReclaimableSizeBadge.Text = $"{result.FormattedTotalSize} ({result.Items.Count} Traces)";
        StatusFooterText.Text = $"Found {result.Items.Count} trace(s) ({result.FormattedTotalSize}) for {app.DisplayName}.";

        ApplyDrawerTabFilter();
        UpdateDrawerSelectionSummary();
    }

    private void ApplyDrawerTabFilter()
    {
        if (_currentScanResult == null) return;

        IEnumerable<LeftoverItem> queryable = _currentScanResult.Items;
        switch (_currentDrawerFilter)
        {
            case "Files":
                queryable = queryable.Where(i => i.Type == LeftoverType.File || i.Type == LeftoverType.Directory);
                break;
            case "Registry":
                queryable = queryable.Where(i => i.Type == LeftoverType.RegistryKey || i.Type == LeftoverType.RegistryValue);
                break;
            case "Shortcuts":
                queryable = queryable.Where(i => i.Type == LeftoverType.Shortcut || i.Type == LeftoverType.Service || i.Type == LeftoverType.ScheduledTask);
                break;
        }

        LeftoversItemsControl.ItemsSource = queryable.ToList();
    }

    private void DrawerTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            SoundService.PlayClickSound();
            DrawerTabAllBtn.Tag = null;
            DrawerTabFilesBtn.Tag = null;
            DrawerTabRegistryBtn.Tag = null;
            DrawerTabShortcutsBtn.Tag = null;

            btn.Tag = "Active";

            if (btn == DrawerTabFilesBtn) _currentDrawerFilter = "Files";
            else if (btn == DrawerTabRegistryBtn) _currentDrawerFilter = "Registry";
            else if (btn == DrawerTabShortcutsBtn) _currentDrawerFilter = "Shortcuts";
            else _currentDrawerFilter = "All";

            ApplyDrawerTabFilter();
        }
    }

    private void SelectAllTraces_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_currentScanResult != null)
        {
            foreach (var item in _currentScanResult.Items) item.IsSelected = true;
            ApplyDrawerTabFilter();
            UpdateDrawerSelectionSummary();
        }
    }

    private void SelectRecommendedTraces_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_currentScanResult != null)
        {
            foreach (var item in _currentScanResult.Items)
            {
                item.IsSelected = (item.Confidence != LeftoverConfidence.Low);
            }
            ApplyDrawerTabFilter();
            UpdateDrawerSelectionSummary();
        }
    }

    private void TraceCheckbox_Click(object sender, RoutedEventArgs e)
    {
        UpdateDrawerSelectionSummary();
    }

    private void UpdateDrawerSelectionSummary()
    {
        if (_currentScanResult == null) return;

        int selectedCount = _currentScanResult.Items.Count(i => i.IsSelected);
        long selectedSize = _currentScanResult.Items.Where(i => i.IsSelected).Sum(i => i.SizeBytes);

        RootDrawerSelectionSummary.Text = $"{selectedCount} of {_currentScanResult.Items.Count} trace(s) selected ({TargetFolderInfo.FormatBytes(selectedSize)}).";
        PurgeRootTracesBtn.IsEnabled = selectedCount > 0;
    }

    private void CloseRootDrawer_Click(object sender, RoutedEventArgs e)
    {
        RootLeftoversDrawer.Visibility = Visibility.Collapsed;
    }

    private async void PurgeRootTracesBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentScanResult == null || !_currentScanResult.Items.Any(i => i.IsSelected)) return;

        SoundService.PlayClickSound();
        PurgeRootTracesBtn.IsEnabled = false;
        StatusFooterText.Text = $"Eradicating selected root traces for {_currentInspectedApp?.DisplayName}...";

        var selected = _currentScanResult.Items.Where(i => i.IsSelected).ToList();
        var purgeResult = await RootLeftoverPurgeService.PurgeLeftoversAsync(selected);

        SoundService.PlaySuccessSound();
        StatusFooterText.Text = $"Purged {purgeResult.ItemsPurgedCount} trace(s) ({purgeResult.FormattedReclaimed} reclaimed) in {purgeResult.ExecutionTimeMs}ms.";
        LogRequested?.Invoke($"[Deep Root Purge] Eradicated {purgeResult.ItemsPurgedCount} traces ({purgeResult.FormattedReclaimed}) for {_currentInspectedApp?.DisplayName}.", LogLevel.Success);

        RootLeftoversDrawer.Visibility = Visibility.Collapsed;
        await LoadAppsAsync();
    }

    #endregion
}
