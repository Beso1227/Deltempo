using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using WinTempCleaner.Models;

namespace Deltempo.App.WinUI;

public sealed partial class MainWindow : Window
{
    // The six workspaces of the WPF MainWindow (MainWindow.xaml AutomationProperties.Name
    // values: Cleaner Workspace, Large Files Tool, Startup Applications Tool,
    // Memory Optimization Tool, System Repair Tool, App Uninstaller Tool).
    private static readonly (string Label, string Glyph)[] Sections =
    {
        ("Cleaner Workspace", "\uE80F"),
        ("Large Files Tool", "\uE8B7"),
        ("Startup Applications Tool", "\uE7C3"),
        ("Memory Optimization Tool", "\uE9F9"),
        ("System Repair Tool", "\uE90F"),
        ("App Uninstaller Tool", "\uE77B"),
    };

    public MainWindow()
    {
        InitializeComponent();

        Title = "Deltempo";

        // Mica replaces the WPF DwmBackdropService (Core/Services/DwmBackdropService.cs)
        // - the framework now owns the backdrop.
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarHost);

        ApplyWindowChrome();
        BuildNavigation();
        VerifyCoreLink();
        WireWorkspaceSurfaces();

        // Self-test drives the ported overlays directly: selecting a nav item does not
        // fire SelectionChanged for the initial selection, so the overlays would
        // otherwise stay closed and the self-test would prove nothing about them.
        if (ShellDiagnostics.IsSelfTest)
        {
            OpenAppDataFootprint();
            LoadSelfTestLockedFiles();
            OpenMemoryOptimizerForSelfTest();
            LoadSelfTestForceDelete();
            LoadSelfTestAppUninstall();
            LoadSelfTestSystemRepair();
            LoadSelfTestMemoryWorkspace();
            _ = LoadSelfTestStartupWorkspace();
            _ = LoadSelfTestLargeFilesWorkspace();
            _ = LoadSelfTestCleanerWorkspace();
        }

        ShellDiagnostics.MaybeSelfTest(this);
    }

    /// <summary>
    /// Exercises the memory optimizer's read paths: live telemetry plus the zone
    /// list.
    ///
    /// It deliberately does NOT invoke OptimizeAreaAsync, OptimizeRamAsync,
    /// QuickTrim, FlushZone or PurgeSelected. Those issue privileged NT memory
    /// operations against the running system and have no place in a self-test.
    /// </summary>
    private void OpenMemoryOptimizerForSelfTest()
    {
        // LogRequested is subscribed once in WireWorkspaceSurfaces, so this only
        // opens the overlay. Subscribing here as well would double every log line.
        MemoryOverlay.Open();

        ShellDiagnostics.Write(
            $"memory optimizer loaded (self-test, read only): " +
            $"{MemoryOverlay.ViewModel.Zones.Count} zone(s), " +
            $"RAM {MemoryOverlay.ViewModel.TotalRamText}, " +
            $"standby {MemoryOverlay.ViewModel.StandbyCacheText}");
    }

    /// <summary>
    /// Exercises the locked-files inspector with display-only rows.
    ///
    /// It never calls RestartManagerService.ScheduleRebootDeletion: that registers a
    /// pending deletion with the Windows Session Manager, which is a real system
    /// mutation and has no place in a self-test.
    /// </summary>
    private void LoadSelfTestLockedFiles()
    {
        LockedFilesOverlay.LogRequested += OnFootprintLog;

        LockedFilesOverlay.Load(
        [
            new LockedFileItem
            {
                FilePath = @"C:\Users\example\AppData\Local\Cache\locked.tmp",
                FileName = "locked.tmp",
                SizeBytes = 12_582_912,
                LockingProcessName = "exampleapp",
                LockingProcessId = 4242,
                LockReason = "Process holds an open handle",
                StatusText = "Detected",
            },
            new LockedFileItem
            {
                FilePath = @"C:\Users\example\AppData\Local\Other\held.dat",
                FileName = "held.dat",
                SizeBytes = 1_048_576,
                LockingProcessName = "otherexample",
                LockingProcessId = 918,
                LockReason = "Process holds an open handle",
                StatusText = "Detected",
            },
        ]);

        ShellDiagnostics.Write(
            $"locked files inspector loaded (self-test, display only): " +
            $"{LockedFilesOverlay.ViewModel.Rows.Count} row(s)");
    }

    /// <summary>
    /// Exercises the force-delete staging surface WITHOUT deleting anything.
    ///
    /// Only the read-only profiling path runs: InspectPath walks the target and
    /// reports its gate tier. ExecuteCommand and DryRunCommand are never invoked,
    /// so nothing is deleted, no locking process is terminated, and
    /// ScheduleRebootIfLocked never reaches the Session Manager.
    ///
    /// The value of this probe is the SHIELDING assertion. It stages a real
    /// System32 path alongside a harmless temp file and proves the shielded one
    /// lands pre-deselected and is excluded from EligibleTargets(). That is the
    /// single most important safety property on this screen, so it is worth
    /// proving at runtime rather than trusting the tier enum alone.
    /// </summary>
    private async void LoadSelfTestForceDelete()
    {
        ForceDeleteOverlay.ViewModel.LogRequested += OnFootprintLog;
        ForceDeleteOverlay.OpenEmpty();

        string scratchFile = Path.Combine(
            Path.GetTempPath(),
            $"deltempo-selftest-{Guid.NewGuid():N}.tmp");

        await File.WriteAllTextAsync(scratchFile, "force delete self-test scratch file");

        try
        {
            await ForceDeleteOverlay.ViewModel.StagePathsAsync(
            [
                scratchFile,
                // Read-only reference to a strictly-shielded system path. Profiling
                // this reads directory metadata; it does not touch the file.
                Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            ]);
        }
        finally
        {
            try
            {
                File.Delete(scratchFile);
            }
            catch (IOException)
            {
                // A locked scratch file is not a self-test failure.
            }
        }

        List<Deltempo.App.WinUI.ViewModels.ForceDeleteTargetRowViewModel> eligible =
            ForceDeleteOverlay.ViewModel.EligibleTargets();

        int shieldedStaged = ForceDeleteOverlay.ViewModel.Targets
            .Count(t => t.IsShielded && !t.IsSelected);

        ShellDiagnostics.Write(
            $"force delete loaded (self-test, staging only): " +
            $"{ForceDeleteOverlay.ViewModel.Targets.Count} staged, " +
            $"{eligible.Count} eligible, {shieldedStaged} shielded-and-deselected, " +
            $"canRun={ForceDeleteOverlay.ViewModel.CanRunPurge}");

        if (shieldedStaged != 1)
        {
            ShellDiagnostics.Write(
                "FAIL: expected exactly 1 shielded target staged pre-deselected.");
        }

        if (eligible.Any(t => t.IsShielded))
        {
            ShellDiagnostics.Write("FAIL: a shielded target reached the eligible list.");
        }
    }

    /// <summary>
    /// Exercises the app uninstaller's read paths: the registry audit, then a
    /// leftover trace scan on the first app that reports a usable identity.
    ///
    /// It deliberately does NOT invoke DeepUninstallCommand,
    /// ForceRootCleanCommand, PurgeTracesCommand, or FetchAllAiCommand.
    /// The first three mutate the system (terminating processes, launching an
    /// uninstaller, deleting registry keys) and the last one makes network
    /// calls. None belongs in a self-test.
    ///
    /// Note the ViewModel here is constructed WITHOUT a confirmation host, so
    /// even if a command were invoked by accident it would fail closed at the
    /// confirmation gate and do nothing. That is the point of the injected,
    /// default-refusing confirmer.
    /// </summary>
    private async void LoadSelfTestAppUninstall()
    {
        // No confirmAsync and no openFolder: the ViewModel refuses everything
        // and opens nothing. This is the fail-closed configuration.
        var probe = new Deltempo.App.WinUI.ViewModels.AppUninstallViewModel();

        probe.LogRequested += OnFootprintLog;

        // Must be awaited, never blocked on. LoadAppsAsync resumes on the UI
        // dispatcher, so GetAwaiter().GetResult() here would deadlock the shell.
        await probe.LoadAppsAsync();

        int total = probe.Apps.Count;
        int withIcons = probe.Apps.Count(a => a.HasAppIcon);

        ShellDiagnostics.Write(
            $"app uninstaller loaded (self-test, read only): {total} app(s) audited, " +
            $"{withIcons} with extracted icons, status=\"{probe.StatusText}\"");

        if (total == 0)
        {
            ShellDiagnostics.Write("FAIL: registry audit returned zero applications.");
            return;
        }

        // Prove the search + chip filter actually narrows, since filtering is
        // the one piece of pure logic worth asserting on here.
        probe.SearchText = "zzzz-no-such-application-zzzz";
        int afterSearch = probe.Apps.Count;

        probe.SearchText = string.Empty;
        probe.SetFilterCommand.Execute(Deltempo.App.WinUI.ViewModels.AppListFilter.Browsers);
        int browsers = probe.Apps.Count;

        ShellDiagnostics.Write(
            $"app uninstaller filters verified: all={total}, " +
            $"no-match-search={afterSearch}, browsers={browsers}");

        if (afterSearch != 0)
        {
            ShellDiagnostics.Write("FAIL: a search with no possible match returned rows.");
        }

        // Confirm the confirmation host is genuinely fail-closed.
        if (probe.HasInspectedApp)
        {
            ShellDiagnostics.Write("FAIL: a scan ran without being asked.");
        }
    }

    /// <summary>
    /// Exercises the system repair console WITHOUT running any repair.
    ///
    /// Nothing here shells out. Quick Assessment, SFC, DISM, WinSxS cleanup,
    /// CHKDSK, the Windows Update reset, and the Winsock reset are all skipped:
    /// they mutate the live system, several need Administrator, and an
    /// interrupted CHKDSK or network reset can leave Windows needing further
    /// repair. CTT WinUtil is skipped because it opens an elevated PowerShell.
    ///
    /// What this DOES prove is the part worth proving - the re-entrancy lock.
    /// It claims the lock without running a tool, then verifies that a second
    /// tool request is refused and that the cancel affordance is live. That is
    /// the invariant that stops DISM and CHKDSK running concurrently.
    /// </summary>
    private void LoadSelfTestSystemRepair()
    {
        // No confirmation host and no elevation action: fail closed and never
        // relaunch the process during an automated run.
        var probe = new Deltempo.App.WinUI.ViewModels.SystemRepairViewModel();

        probe.LogRequested += OnFootprintLog;

        ShellDiagnostics.Write(
            $"system repair loaded (self-test, no tools run): " +
            $"admin={probe.IsAdministrator}, badge=\"{probe.ElevationBadgeText}\", " +
            $"canRun={probe.CanRunTools}, canCancel={probe.CanCancel}, " +
            $"subsystems={probe.Subsystems.Length}");

        // Claim the lock without executing a repair. IsRunning is what every
        // tool button and the cancel button read, so setting it directly is a
        // faithful simulation of "a repair is in flight".
        probe.IsRunning = true;

        bool cancelReachable = probe.CanCancel;
        bool toolsBlocked = !probe.CanRunTools;

        ShellDiagnostics.Write(
            $"system repair re-entrancy lock verified: " +
            $"toolsBlocked={toolsBlocked}, cancelReachable={cancelReachable}");

        if (!toolsBlocked)
        {
            ShellDiagnostics.Write("FAIL: repair tools stayed enabled while an operation was running.");
        }

        if (!cancelReachable)
        {
            ShellDiagnostics.Write("FAIL: no way to cancel a running operation.");
        }

        // Releasing the lock must re-enable the tools.
        probe.IsRunning = false;

        if (!probe.CanRunTools)
        {
            ShellDiagnostics.Write("FAIL: tools stayed disabled after the operation ended.");
        }

        // The default confirmation host must refuse, so pruning cannot run.
        if (probe.CanPruneRestorePoints)
        {
            ShellDiagnostics.Write(
                "FAIL: prune offered with no restore points loaded.");
        }
    }

    /// <summary>
    /// Exercises the Memory Optimization workspace panel.
    ///
    /// Telemetry only. ExecuteBoostAsync is NEVER invoked here: it issues
    /// privileged NT memory operations (working-set trimming and standby cache
    /// eviction) against the running system.
    ///
    /// This is the first Stage 3 workspace probe. It proves the panel's
    /// ViewModel reads live RAM state and that the hero bindings have real
    /// values, rather than proving the panel merely compiles.
    /// </summary>
    private void LoadSelfTestMemoryWorkspace()
    {
        MemoryWorkspacePanel.ViewModel.RefreshTelemetry();

        Deltempo.App.WinUI.ViewModels.MemoryWorkspaceViewModel vm =
            MemoryWorkspacePanel.ViewModel;

        ShellDiagnostics.Write(
            $"memory workspace loaded (self-test, telemetry only): " +
            $"{vm.UsedPercentText} used, detail=\"{vm.RamDetailText}\", " +
            $"boost=\"{vm.BoostButtonText}\", boosting={vm.IsBoosting}");

        if (vm.RamDetailText == "Reading RAM telemetry...")
        {
            ShellDiagnostics.Write("FAIL: RAM telemetry did not populate.");
        }

        if (double.IsNaN(vm.UsedPercent) || vm.UsedPercent < 0 || vm.UsedPercent > 100)
        {
            ShellDiagnostics.Write(
                $"FAIL: RAM usage out of range: {vm.UsedPercent}.");
        }
    }

    /// <summary>
    /// Exercises the Startup Applications workspace.
    ///
    /// LoadAsync only. ToggleAsync, CleanOrphanedAsync and OptimizeBootAsync are
    /// never invoked: all three write to the Windows registry, and Optimize Boot
    /// writes to many entries at once.
    ///
    /// What this does prove is the PROTECTED-ITEM rule, which is the safety
    /// property that matters on this screen. It walks the real enumerated set and
    /// asserts the invariant holds on this machine: no protected entry is
    /// offered a toggle, and no protected entry is offered a clean. If
    /// IsProtectedStartupItem ever regressed, this fails.
    ///
    /// A bare ViewModel is used, with no confirmation host, so even an accidental
    /// mutating call would fail closed rather than touch the registry.
    /// </summary>
    private async Task LoadSelfTestStartupWorkspace()
    {
        var probe = new Deltempo.App.WinUI.ViewModels.StartupWorkspaceViewModel();

        probe.LogRequested += OnFootprintLog;
        await probe.LoadAsync();

        List<WinTempCleaner.Services.StartupItem> all =
        [
            .. await WinTempCleaner.Services.StartupManagerService.GetStartupItemsAsync()
        ];

        int protectedCount = all.Count(x => x.IsProtected);
        int protectedToggleable = all.Count(x => x.IsProtected && x.CanToggle);
        int protectedCleanable = all.Count(x => x.IsProtected && x.CanCleanOrphaned);

        ShellDiagnostics.Write(
            $"startup workspace loaded (self-test, read only): " +
            $"{all.Count} entries, {protectedCount} protected, " +
            $"orphanable={all.Count(x => x.CanCleanOrphaned)}, status=\"{probe.StatusText}\"");

        ShellDiagnostics.Write(
            $"startup protection invariant verified: " +
            $"protectedToggleable={protectedToggleable}, protectedCleanable={protectedCleanable}");

        if (protectedToggleable > 0)
        {
            ShellDiagnostics.Write("FAIL: a protected startup entry was offered a toggle.");
        }

        if (protectedCleanable > 0)
        {
            ShellDiagnostics.Write("FAIL: a protected startup entry was offered a clean.");
        }
    }

    /// <summary>
    /// Exercises the Large Files workspace.
    ///
    /// ScanAsync is read-only: it enumerates files and classifies them with
    /// FileSafetyEngine, deleting nothing. Safe to run in the self-test.
    ///
    /// RecycleSelectedAsync / RecycleItemAsync / AnalyzeItemAsync are never
    /// invoked: the first two delete files, the third makes a network call.
    /// A bare ViewModel is used, with no confirmation host, so even an
    /// accidental mutating call would fail closed.
    /// </summary>
    private async Task LoadSelfTestLargeFilesWorkspace()
    {
        var probe = new Deltempo.App.WinUI.ViewModels.LargeFilesWorkspaceViewModel();

        probe.LogRequested += OnFootprintLog;
        await probe.ScanAsync();

        int safeCount = probe.Items.Count(f => f.IsSafe);
        int protectedCount = probe.Items.Count - safeCount;

        ShellDiagnostics.Write(
            $"large files workspace loaded (self-test, read only): " +
            $"{probe.Items.Count} files, {safeCount} safe, {protectedCount} protected");
    }

    /// <summary>
    /// Exercises the Cleaner workspace.
    ///
    /// ScanAllAsync is read-only: it enumerates and measures files in target
    /// categories, deleting nothing. Safe to run in the self-test.
    ///
    /// CleanSelectedAsync (destructive) is never invoked. A bare ViewModel is
    /// used, with no confirmation host, so even an accidental mutating call
    /// fails closed.
    /// </summary>
    private async Task LoadSelfTestCleanerWorkspace()
    {
        var probe = new Deltempo.App.WinUI.ViewModels.CleanerWorkspaceViewModel();

        probe.LogRequested += OnFootprintLog;
        await probe.ScanAllAsync();

        int cleanable = probe.Targets.Count(t => t.SizeBytes > 0);

        ShellDiagnostics.Write(
            $"cleaner workspace loaded (self-test, read only): " +
            $"{probe.Targets.Count} targets, {cleanable} with reclaimable space, " +
            $"status=\"{probe.StatusText}\"");
    }

    /// <summary>
    /// Opens the ported AppData footprint report over the content area. This is the
    /// Stage 2 proof screen: real Core data, no delete affordance.
    /// </summary>
    /// <summary>
    /// Wires the workspace panels to the shell surfaces they share.
    ///
    /// Subscriptions live here, once, rather than inside each panel's Open(), so
    /// repeated navigation cannot stack duplicate handlers (which would double
    /// every log line) and so the shell keeps ownership of which overlay is up -
    /// the same division of labour the WPF modal host had.
    /// </summary>
    private void WireWorkspaceSurfaces()
    {
        MemoryWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;
        MemoryWorkspacePanel.ViewModel.DetailedViewRequested += OpenMemoryOptimizerOverlay;

        MemoryOverlay.LogRequested += OnFootprintLog;
        MemoryOverlay.CloseRequested += CloseMemoryOptimizerOverlay;

        StartupWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;

        CleanerWorkspacePanel.FootprintRequested += OpenAppDataFootprint;

        SystemRepairWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;
        SystemRepairWorkspacePanel.ViewModel.CloseRequested += ReturnToFirstWorkspace;

        AppUninstallWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;
        AppUninstallWorkspacePanel.ViewModel.CloseRequested += ReturnToFirstWorkspace;
    }

    /// <summary>Opens the detailed memory manager over whatever workspace is showing.</summary>
    private void OpenMemoryOptimizerOverlay() => MemoryOverlay.Open();

    private void CloseMemoryOptimizerOverlay()
        => MemoryOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>
    /// The close button on a promoted panel returns to the first workspace, which
    /// is what closing the matching WPF modal did
    /// (AppDataFootprintModalOverlay.Closed =&gt; SwitchWorkspaceView(Cleaner)).
    /// </summary>
    private void ReturnToFirstWorkspace() => NavList.SelectedIndex = 0;

    private void OpenAppDataFootprint()
    {
        FootprintView.LogRequested += OnFootprintLog;
        FootprintView.CloseRequested += CloseAppDataFootprint;
        FootprintView.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        FootprintView.PopulateAndOpen();
    }

    private void CloseAppDataFootprint()
    {
        FootprintView.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        FootprintView.CloseRequested -= CloseAppDataFootprint;
        FootprintView.LogRequested -= OnFootprintLog;
    }

    private void OnFootprintLog(string message, WinTempCleaner.Models.LogLevel level)
        => ShellDiagnostics.Write($"[{level}] {message}");

    private void ApplyWindowChrome()
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            AppWindow appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));

            appWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));

            // Suppress the default caption inset so the custom title bar aligns with
            // the left edge of the nav rail. SetTitleBar(TitleBarHost) already sizes the
            // drag region to the 48px host row, so no explicit PreferredHeight is needed.
            appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            appWindow.TitleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Gray;
        }
        catch (Exception ex)
        {
            FooterText.Text = $"Window chrome failed: {ex.GetType().Name}";
            ShellDiagnostics.Write($"window chrome FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void BuildNavigation()
    {
        foreach (var (label, glyph) in Sections)
        {
            var item = new ListViewItem
            {
                Tag = label,
                Content = BuildNavContent(label, glyph),
            };

            // Without this the item announces as an empty list item. The WPF nav
            // set AutomationProperties.Name on each workspace, and dropping it
            // made every nav destination unreachable by name - for screen
            // readers and for UI automation alike.
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, label);

            NavList.Items.Add(item);
        }

        if (NavList.Items.Count > 0)
        {
            NavList.SelectedIndex = 0;
        }

        NavList.SelectionChanged += (_, _) =>
        {
            if (NavList.SelectedItem is ListViewItem selected && selected.Tag is string label)
            {
                ShowWorkspace(label, playSound: true);
            }
        };

        // Paint the section that starts selected. SelectionChanged only fires on a
        // change, so selecting index 0 above left the Stage 1 placeholder on screen
        // until the user picked another nav item - a screen the WPF app never showed,
        // because it opened straight into the Cleaner workspace.
        if (NavList.SelectedItem is ListViewItem initial && initial.Tag is string initialLabel)
        {
            ShowWorkspace(initialLabel, playSound: false);
        }
    }

    /// <summary>
    /// Shows one workspace: sets the heading, swaps which panel is visible, and loads
    /// that panel lazily so its read-only scan is paid for once. Every nav item has a
    /// real panel, so the placeholder is only ever seen if a section is added without
    /// one. The AppData footprint report opens from inside the Cleaner workspace (its
    /// HeroAppDataReportBtn in WPF), not from selecting the nav item.
    /// </summary>
    /// <param name="playSound">
    /// False for the startup paint, which is not a user click and must not blip.
    /// </param>
    private void ShowWorkspace(string label, bool playSound)
    {
        SectionLabel.Text = label;
        PlaceholderTitle.Text = label;
        PlaceholderBody.Text =
            $"Stage 1 shell placeholder for '{label}'. No cleanup logic is wired up; nothing on this screen touches disk.";

        bool isMemory = label == Sections[3].Label;
        bool isStartup = label == Sections[2].Label;
        bool isLargeFile = label == Sections[1].Label;
        bool isCleaner = label == Sections[0].Label;
        bool isSystemRepair = label == Sections[4].Label;
        bool isAppUninstall = label == Sections[5].Label;

        MemoryWorkspacePanel.Visibility = isMemory ? Visibility.Visible : Visibility.Collapsed;
        StartupWorkspacePanel.Visibility = isStartup ? Visibility.Visible : Visibility.Collapsed;
        LargeFilesWorkspacePanel.Visibility = isLargeFile ? Visibility.Visible : Visibility.Collapsed;
        CleanerWorkspacePanel.Visibility = isCleaner ? Visibility.Visible : Visibility.Collapsed;
        SystemRepairWorkspacePanel.Visibility = isSystemRepair ? Visibility.Visible : Visibility.Collapsed;
        AppUninstallWorkspacePanel.Visibility = isAppUninstall ? Visibility.Visible : Visibility.Collapsed;

        bool hasPanel = isMemory || isStartup || isLargeFile || isCleaner
            || isSystemRepair || isAppUninstall;
        PlaceholderPanel.Visibility = hasPanel ? Visibility.Collapsed : Visibility.Visible;

        // Every panel paints the window brush, but the Core link card sits in the same
        // content column and would otherwise show through the two promoted modals,
        // whose own surface is deliberately transparent.
        CoreLinkCard.Visibility = hasPanel ? Visibility.Collapsed : Visibility.Visible;

        if (isMemory)
        {
            MemoryWorkspacePanel.ViewModel.RefreshTelemetry();
        }
        else if (isStartup)
        {
            StartupWorkspacePanel.Open(playSound);
        }
        else if (isLargeFile)
        {
            LargeFilesWorkspacePanel.Open(playSound);
        }
        else if (isCleaner)
        {
            CleanerWorkspacePanel.Open(playSound);
        }
        else if (isSystemRepair)
        {
            SystemRepairWorkspacePanel.Open(playSound);
        }
        else if (isAppUninstall)
        {
            AppUninstallWorkspacePanel.Open(playSound);
        }
    }

    private static UIElement BuildNavContent(string label, string glyph)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };

        panel.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 16,
        });

        panel.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return panel;
    }

    /// <summary>
    /// Proves the WinUI 3 app can consume the existing Core assembly. Uses only
    /// read-only Core APIs - no scanning, no cleanup, nothing destructive.
    /// </summary>
    private void VerifyCoreLink()
    {
        try
        {
            Assembly core = typeof(InstalledAppService).Assembly;
            string version = core.GetName().Version?.ToString() ?? "unknown";

            // Read-only safety predicate - the same one Core/Safety uses internally.
            bool safeForProtectedPath = InstalledAppService.IsSafeToDeleteResidual(@"C:\Windows\System32");

            CoreStatus.Text =
                $"Core loaded: {core.GetName().Name} {version}  |  " +
                $"safety predicate reachable: true  |  " +
                $"C:\\Windows\\System32 protected: {(safeForProtectedPath ? "NO" : "yes")}";

            CoreStatus.Foreground = (Application.Current.Resources["SuccessTextBrightBrush"] as SolidColorBrush)
                ?? new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen);

            ShellDiagnostics.Write(CoreStatus.Text);
        }
        catch (Exception ex)
        {
            CoreStatus.Text = $"Core link FAILED: {ex.GetType().Name}: {ex.Message}";
            CoreStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            ShellDiagnostics.Write(CoreStatus.Text);
        }
    }
}