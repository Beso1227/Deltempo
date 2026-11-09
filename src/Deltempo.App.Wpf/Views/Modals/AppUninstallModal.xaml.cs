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
    private bool _isBusy;

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
            case "Store":
                queryable = queryable.Where(a => a.UninstallEngine.Contains("MSIX", StringComparison.OrdinalIgnoreCase) || a.UninstallEngine.Contains("Store", StringComparison.OrdinalIgnoreCase));
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
            FilterStoreBtn.Tag = null;

            btn.Tag = "Active";

            if (btn == FilterBrowsersBtn) _currentAppFilter = "Browsers";
            else if (btn == FilterGamingBtn) _currentAppFilter = "Gaming";
            else if (btn == FilterDevBtn) _currentAppFilter = "Dev Tools";
            else if (btn == FilterRuntimesBtn) _currentAppFilter = "Runtimes";
            else if (btn == FilterLargeBtn) _currentAppFilter = "Large";
            else if (btn == FilterBrokenBtn) _currentAppFilter = "Broken";
            else if (btn == FilterStoreBtn) _currentAppFilter = "Store";
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

            if (app.AppIcon is { Length: > 0 })
            {
                var iconImage = new System.Windows.Media.Imaging.BitmapImage();
                iconImage.BeginInit();
                iconImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                iconImage.StreamSource = new MemoryStream(app.AppIcon, writable: false);
                iconImage.EndInit();
                iconImage.Freeze();

                DrawerAppIconImage.Source = iconImage;
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

    /// <summary>
    /// Single busy flag for every destructive or expensive action. The handlers guard on it,
    /// the purge button is disabled while it is set, and a small indeterminate bar shows the
    /// app is working instead of leaving the user to click again.
    /// </summary>
    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        BusyProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrWhiteSpace(status))
        {
            StatusFooterText.Text = status;
        }

        int selectedCount = _currentScanResult?.Items.Count(i => i.IsSelected) ?? 0;
        PurgeRootTracesBtn.IsEnabled = !busy && selectedCount > 0;
    }

    private async void ScanTracesBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_isBusy || sender is not Button btn || btn.DataContext is not InstalledAppItem app) return;

        SetBusy(true, $"Scanning registry, user profile, and system folders for {app.DisplayName}...");
        try
        {
            await InspectAppTracesAsync(app);
        }
        catch (Exception ex)
        {
            StatusFooterText.Text = $"Trace scan failed for {app.DisplayName}: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Trace scan failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ForceRootCleanBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_isBusy || sender is not Button btn || btn.DataContext is not InstalledAppItem app) return;

        // This button only opens the inspector. The purge is a separate, confirmed action,
        // so the prompt must not promise that clicking it deletes anything.
        var confirm = MessageBox.Show(
            $"Force Root Clean will scan for file, registry, and shortcut remnants of:\n\n{app.DisplayName}\n\n" +
            "Nothing is deleted until you review the results and confirm the purge. " +
            "Use this when the official uninstaller is broken or missing. Continue?",
            "Force Root Clean", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes) return;

        SetBusy(true, $"Scanning roots for {app.DisplayName}...");
        try
        {
            await InspectAppTracesAsync(app);
        }
        catch (Exception ex)
        {
            StatusFooterText.Text = $"Trace scan failed for {app.DisplayName}: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Trace scan failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void DeepUninstallBtn_Click(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();
        if (_isBusy || sender is not Button btn || btn.DataContext is not InstalledAppItem app) return;

        if (string.IsNullOrWhiteSpace(app.UninstallString) && string.IsNullOrWhiteSpace(app.QuietUninstallString))
        {
            var forceConfirm = MessageBox.Show(
                $"No official uninstaller was detected for {app.DisplayName}.\n\nWould you like to scan for root remnants instead?",
                "Uninstaller Not Found", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (forceConfirm == MessageBoxResult.Yes)
            {
                SetBusy(true, $"Scanning roots for {app.DisplayName}...");
                try
                {
                    await InspectAppTracesAsync(app);
                }
                catch (Exception ex)
                {
                    StatusFooterText.Text = $"Trace scan failed for {app.DisplayName}: {ex.Message}";
                    LogRequested?.Invoke($"[App Uninstaller] Trace scan failed: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    SetBusy(false);
                }
            }
            return;
        }

        bool shouldCreateRestorePoint = CreateRestorePointCheckBox.IsChecked == true;
        string rpText = shouldCreateRestorePoint
            ? "• System Restore Point: Enabled (Checkpoint will be created)\n"
            : "• System Restore Point: Disabled (Optional checkpoint skipped)\n";

        var confirm = MessageBox.Show(
            $"Deep Uninstall will:\n" +
            $"1. Terminate running processes for {app.DisplayName}\n" +
            $"2. Run the official uninstaller ({app.UninstallEngine})\n" +
            $"3. Scan for leftover traces so you can review and purge them\n\n" +
            rpText + "\n" +
            $"Proceed with uninstallation?",
            "Deep Root Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes) return;

        SetBusy(true, $"Preparing deep uninstall for {app.DisplayName}...");

        try
        {
            if (shouldCreateRestorePoint)
            {
                StatusFooterText.Text = "Creating System Restore Point checkpoint...";
                LogRequested?.Invoke($"[App Uninstaller] Creating System Restore Point checkpoint for {app.DisplayName}...", LogLevel.Info);
                var restoreResult = await WinTempCleaner.Core.Safety.SystemRestorePointService.CreateRestorePointAsync($"Pre-Uninstall {app.DisplayName}");
                if (restoreResult.Success)
                {
                    LogRequested?.Invoke($"[App Uninstaller] System Restore Point created successfully (Sequence: {restoreResult.SequenceNumber}).", LogLevel.Success);
                }
                else
                {
                    LogRequested?.Invoke($"[App Uninstaller] System Restore Point skipped or unavailable: {restoreResult.Message}", LogLevel.Warning);

                    // The user explicitly asked for a safety net. Do not silently uninstall
                    // without one; let them decide whether to continue.
                    var proceed = MessageBox.Show(
                        $"A System Restore Point could not be created:\n\n{restoreResult.Message}\n\n" +
                        "Continue with the uninstall without a restore point?",
                        "Restore Point Not Created", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

                    if (proceed != MessageBoxResult.Yes)
                    {
                        StatusFooterText.Text = "Uninstall cancelled because no restore point was created.";
                        LogRequested?.Invoke("[App Uninstaller] Uninstall cancelled: restore point unavailable.", LogLevel.Warning);
                        return;
                    }
                }
            }

            StatusFooterText.Text = $"Capturing pre-uninstall trace snapshot for {app.DisplayName}...";
            var snapshot = await RootLeftoverScannerService.CreateSnapshotAsync(app);

            StatusFooterText.Text = $"Terminating background processes and launching uninstaller for {app.DisplayName}...";
            LogRequested?.Invoke($"[App Uninstaller] Initiating deep uninstall for {app.DisplayName} ({app.UninstallEngine})...", LogLevel.Info);

            bool success = await InstalledAppService.UninstallAppAsync(app, silent: false);
            if (success)
            {
                StatusFooterText.Text = "Official uninstaller finished. Scanning roots for leftover traces...";
                LogRequested?.Invoke("[App Uninstaller] Uninstaller process exited. Scanning root remnants...", LogLevel.Info);
                await InspectAppTracesAsync(app, snapshot);
            }
            else
            {
                // UninstallAppAsync now reports a timeout or a non-zero exit code as failure.
                // Do not scan and offer to purge a still-installed app as if it were gone.
                StatusFooterText.Text = "Uninstaller did not complete (cancelled, timed out, or failed). No leftovers were scanned.";
                LogRequested?.Invoke("[App Uninstaller] Uninstallation failed, was cancelled, or timed out.", LogLevel.Warning);
                MessageBox.Show(
                    $"The uninstaller for {app.DisplayName} did not report success.\n\n" +
                    "It may have been cancelled, may still be running, or may have failed. " +
                    "Nothing was purged. You can run a Trace scan to review the app's remnants.",
                    "Uninstall Did Not Complete", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusFooterText.Text = $"Uninstall failed: {ex.Message}";
            LogRequested?.Invoke($"[App Uninstaller] Deep uninstall aborted: {ex.Message}", LogLevel.Error);
            MessageBox.Show(
                $"The uninstall of {app.DisplayName} could not be completed.\n\n{ex.Message}",
                "Uninstall Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InspectAppTracesAsync(InstalledAppItem app, AppTraceSnapshot? snapshot = null)
    {
        _currentInspectedApp = app;
        RootDrawerTitle.Text = $"Root Remnants: {app.DisplayName}";
        RootReclaimableSizeBadge.Text = "Scanning...";
        UpdateDrawerAiCard(app);
        RootLeftoversDrawer.Visibility = Visibility.Visible;
        StatusFooterText.Text = $"Scanning registry, user profile, and system folders for {app.DisplayName}...";

        var result = await RootLeftoverScannerService.ScanAppTracesAsync(app, snapshot);
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
                queryable = queryable.Where(i => i.Type == LeftoverType.RegistryKey || i.Type == LeftoverType.RegistryValue || i.Type == LeftoverType.EnvironmentPath);
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
        PurgeRootTracesBtn.IsEnabled = !_isBusy && selectedCount > 0;
    }

    private void CloseRootDrawer_Click(object sender, RoutedEventArgs e)
    {
        RootLeftoversDrawer.Visibility = Visibility.Collapsed;
    }

    private async void PurgeRootTracesBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _currentScanResult == null) return;

        var selected = _currentScanResult.Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        string appName = _currentInspectedApp?.DisplayName ?? "this application";
        string selectedSize = TargetFolderInfo.FormatBytes(selected.Sum(i => i.SizeBytes));

        // The WPF surface used to delete on the first click. Every other destructive action
        // in this modal is confirmed, and this one also touches registry keys, services,
        // scheduled tasks, and PATH entries, so it gets the same gate.
        var confirm = MessageBox.Show(
            $"Permanently erase {selected.Count} selected leftover trace(s) ({selectedSize}) for:\n\n{appName}\n\n" +
            "This removes files, folders, registry keys and values, services, scheduled tasks, " +
            "and environment PATH entries.\n\nThis action CANNOT be undone. Proceed?",
            "Eradicate Root Traces", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
        {
            LogRequested?.Invoke("[Deep Root Purge] Cancelled by user.", LogLevel.Warning);
            return;
        }

        SoundService.PlayClickSound();
        SetBusy(true, $"Eradicating selected root traces for {appName}...");

        PurgeResult purgeResult;
        try
        {
            purgeResult = await RootLeftoverPurgeService.PurgeLeftoversAsync(selected, _currentInspectedApp);
        }
        catch (Exception ex)
        {
            SetBusy(false);
            StatusFooterText.Text = $"Root purge failed: {ex.Message}";
            LogRequested?.Invoke($"[Deep Root Purge] Purge aborted: {ex.Message}", LogLevel.Error);
            MessageBox.Show(
                $"The root purge could not be completed.\n\n{ex.Message}",
                "Root Purge Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        bool hadErrors = purgeResult.ErrorsCount > 0;

        // A success sound after a failed delete is a lie: use the warning cue when anything
        // was blocked or left behind.
        if (hadErrors) SoundService.PlayWarningSound();
        else SoundService.PlaySuccessSound();

        string summary =
            $"Purged {purgeResult.ItemsPurgedCount} trace(s) ({purgeResult.FormattedReclaimed} reclaimed) " +
            $"in {purgeResult.ExecutionTimeMs}ms.";

        if (purgeResult.RebootScheduledCount > 0)
        {
            summary += $" {purgeResult.RebootScheduledCount} locked item(s) will be deleted on the next reboot.";
        }

        if (hadErrors)
        {
            summary += $" {purgeResult.ErrorsCount} item(s) could not be purged.";
        }

        if (hadErrors)
        {
            LogRequested?.Invoke(
                $"[Deep Root Purge] Eradicated {purgeResult.ItemsPurgedCount} traces ({purgeResult.FormattedReclaimed}); " +
                $"{purgeResult.ErrorsCount} item(s) could not be purged for {appName}.",
                LogLevel.Warning);
        }
        else
        {
            LogRequested?.Invoke(
                $"[Deep Root Purge] Eradicated {purgeResult.ItemsPurgedCount} traces ({purgeResult.FormattedReclaimed}) for {appName}.",
                LogLevel.Success);
        }

        if (purgeResult.RebootScheduledCount > 0)
        {
            LogRequested?.Invoke(
                $"[Deep Root Purge] {purgeResult.RebootScheduledCount} locked item(s) registered for automatic deletion " +
                "on the next Windows reboot.",
                LogLevel.Warning);
            foreach (string path in purgeResult.RebootScheduledItems)
            {
                LogRequested?.Invoke($"[Deep Root Purge] Reboot deletion: {path}", LogLevel.Info);
            }
        }

        foreach (string error in purgeResult.ErrorMessages)
        {
            LogRequested?.Invoke($"[Deep Root Purge] {error}", LogLevel.Warning);
        }

        // Re-scan so the drawer shows only what actually survived. This also keeps the list
        // honest when some items were blocked or are waiting for a reboot.
        if (_currentInspectedApp != null)
        {
            try
            {
                await InspectAppTracesAsync(_currentInspectedApp);
            }
            catch (Exception ex)
            {
                // Without a fresh result the drawer would still show items that are already
                // gone. Drop the stale list rather than present it as current.
                _currentScanResult = null;
                LogRequested?.Invoke($"[Deep Root Purge] Post-purge rescan failed: {ex.Message}", LogLevel.Warning);
            }
        }

        // LoadAppsAsync writes its own status line; restore the purge outcome afterwards.
        await LoadAppsAsync();
        StatusFooterText.Text = summary;
        SetBusy(false);

        if (_currentScanResult == null || _currentScanResult.Items.Count == 0)
        {
            RootLeftoversDrawer.Visibility = Visibility.Collapsed;
        }

        if (hadErrors || purgeResult.RebootScheduledCount > 0)
        {
            var detail = new System.Text.StringBuilder();
            detail.AppendLine(summary);
            detail.AppendLine();

            if (purgeResult.RebootScheduledCount > 0)
            {
                detail.AppendLine("These locked items will be removed automatically on the next reboot:");
                foreach (string path in purgeResult.RebootScheduledItems.Take(8))
                {
                    detail.AppendLine($"• {path}");
                }
                if (purgeResult.RebootScheduledItems.Count > 8)
                {
                    detail.AppendLine($"… and {purgeResult.RebootScheduledItems.Count - 8} more");
                }
                detail.AppendLine();
            }

            if (hadErrors)
            {
                detail.AppendLine("These items could not be purged and are still on the system:");
                foreach (string error in purgeResult.ErrorMessages.Take(8))
                {
                    detail.AppendLine($"• {error}");
                }
                if (purgeResult.ErrorMessages.Count > 8)
                {
                    detail.AppendLine($"… and {purgeResult.ErrorMessages.Count - 8} more");
                }
            }

            MessageBox.Show(
                detail.ToString().TrimEnd(),
                hadErrors ? "Root Purge Completed With Issues" : "Some Items Scheduled for Reboot",
                MessageBoxButton.OK,
                hadErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
    }

    #endregion
}
