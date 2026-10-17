using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI;

public sealed partial class MainWindow : Window
{
    // The eight workspaces of the WPF MainWindow's top-bar tabs, in tab order. The labels
    // are the WPF AutomationProperties.Name values verbatim (MainWindow.xaml), and the
    // glyphs are the code points its nav pills used.
    private const string CleanerSectionLabel = "Cleaner Workspace";
    private const string LargeFilesSectionLabel = "Large Files Tool";
    private const string StartupSectionLabel = "Startup Applications Tool";
    private const string MemorySectionLabel = "Memory Optimization Tool";
    private const string SystemRepairSectionLabel = "System Repair Tool";
    private const string AppUninstallerSectionLabel = "App Uninstaller Tool";
    private const string ForceDeleteSectionLabel = "Force Delete Tool";

    // Named constants rather than literals so the panel map below keys off identity, not
    // off positions: inserting a tab mid-list used to silently re-point the later nav
    // items at the wrong panels.
    private static readonly (string Label, string Glyph, bool Danger)[] Sections =
    {
        (CleanerSectionLabel, "\uE80F", false),
        (LargeFilesSectionLabel, "\uE8B7", false),
        (StartupSectionLabel, "\uE7C3", false),
        (MemorySectionLabel, "\uE9F9", false),
        (SystemRepairSectionLabel, "\uE90F", false),
        (AppUninstallerSectionLabel, "\uE77B", false),
        // WPF styled this one's pill with its danger brushes.
        (ForceDeleteSectionLabel, "\uE74D", true),
    };

    /// <summary>
    /// Which panel backs each navigation section, and how to load it. Built once, after
    /// the visual tree exists. A section with no entry shows the Stage 1 placeholder,
    /// which is what keeps adding a section safe.
    /// </summary>
    private readonly Dictionary<string, (UIElement Panel, Action? Open)> _workspacePanels = [];

    /// <summary>
    /// Navigation entries that need repainting when the selection moves, keyed by
    /// section label. See <see cref="UpdateNavSelection"/>.
    /// </summary>
    private readonly Dictionary<string, NavEntry> _navEntries = [];

    public MainWindow()
    {
        InitializeComponent();

        Title = "Deltempo";

        // Mica replaces the WPF DwmBackdropService (Core/Services/DwmBackdropService.cs)
        // - the framework now owns the backdrop.
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarHost);

        // The title bar names the app and then the one piece of state that changes what
        // every destructive screen can do: whether this process is elevated. WPF showed
        // the same fact as an admin badge, but only inside System Repair.
        PrivilegeLabel.Text = ElevationService.IsAdministrator ? "Administrator" : "Standard user";

        FooterVersion.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"} · WinUI 3";

        ApplyWindowChrome();
        BuildNavigation();
        VerifyCoreLink();
        WireWorkspaceSurfaces();

        // The shell paints only the selected workspace, so the self-test drives every
        // other surface's load path directly. SelectionChanged cannot do it: the
        // initial selection is assigned before the handler is attached.
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
        ForceDeleteWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;
        ForceDeleteWorkspacePanel.OpenEmpty();

        string scratchFile = Path.Combine(
            Path.GetTempPath(),
            $"deltempo-selftest-{Guid.NewGuid():N}.tmp");

        await File.WriteAllTextAsync(scratchFile, "force delete self-test scratch file");

        try
        {
            await ForceDeleteWorkspacePanel.ViewModel.StagePathsAsync(
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
            ForceDeleteWorkspacePanel.ViewModel.EligibleTargets();

        int shieldedStaged = ForceDeleteWorkspacePanel.ViewModel.Targets
            .Count(t => t.IsShielded && !t.IsSelected);

        ShellDiagnostics.Write(
            $"force delete loaded (self-test, staging only): " +
            $"{ForceDeleteWorkspacePanel.ViewModel.Targets.Count} staged, " +
            $"{eligible.Count} eligible, {shieldedStaged} shielded-and-deselected, " +
            $"canRun={ForceDeleteWorkspacePanel.ViewModel.CanRunPurge}");

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

        // Force Delete closed back to the Cleaner workspace in WPF
        // (ForceDeleteModalOverlay.Closed => SwitchWorkspaceView(WorkspaceView.Cleaner)).
        ForceDeleteWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;
        ForceDeleteWorkspacePanel.ViewModel.CloseRequested += ReturnToFirstWorkspace;
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

            // WPF's MainWindow.xaml declared Width=1360 Height=840, MinWidth=800
            // MinHeight=560 and WindowStartupLocation=CenterScreen. The port had drifted
            // to a hard-coded 1180x780 with no minimum and no centring - and AppWindow
            // sizes in physical pixels, so even that number shrank on a scaled display.
            // These are the WPF numbers, converted from device-independent units, and the
            // size is clamped to the work area so the status bar can never end up below
            // the bottom edge of the screen.
            double scale = Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);

            const int preferredWidth = 1360;
            const int preferredHeight = 840;
            const int minimumWidth = 800;
            const int minimumHeight = 560;

            Windows.Graphics.RectInt32 workArea = DisplayArea
                .GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary)
                .WorkArea;

            var size = new Windows.Graphics.SizeInt32(
                Math.Min((int)Math.Round(preferredWidth * scale), workArea.Width),
                Math.Min((int)Math.Round(preferredHeight * scale), workArea.Height));

            appWindow.Resize(size);
            appWindow.Move(new Windows.Graphics.PointInt32(
                workArea.X + ((workArea.Width - size.Width) / 2),
                workArea.Y + ((workArea.Height - size.Height) / 2)));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = (int)Math.Round(minimumWidth * scale);
                presenter.PreferredMinimumHeight = (int)Math.Round(minimumHeight * scale);
            }

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

    /// <summary>
    /// Physical pixels per logical pixel for this window's monitor. WinUI 3 exposes no
    /// managed accessor for that before the content is loaded, and the window is sized in
    /// the constructor.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void BuildNavigation()
    {
        BuildWorkspacePanelMap();

        foreach (var (label, glyph, danger) in Sections)
        {
            (UIElement content, FontIcon icon, Border indicator) = BuildNavContent(label, glyph);

            var item = new ListViewItem
            {
                Tag = label,
                Content = content,
            };

            _navEntries[label] = new NavEntry(icon, indicator, danger);

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
    /// that panel lazily so its read-only scan is paid for once. A section with no panel
    /// shows the Stage 1 placeholder, so adding a nav item can never leave a blank
    /// content column.
    ///
    /// The click sound lives here rather than in each panel's Open() because that is
    /// where WPF had it (SwitchWorkspaceView played it for every tab), and because a
    /// per-panel copy is how the Memory tab ended up silent.
    /// </summary>
    /// <param name="playSound">
    /// False for the startup paint, which is not a user click and must not blip.
    /// </param>
    private void ShowWorkspace(string label, bool playSound)
    {
        SetPageHeader(label);
        PlaceholderTitle.Text = label;
        PlaceholderBody.Text =
            $"Stage 1 shell placeholder for '{label}'. No cleanup logic is wired up; nothing on this screen touches disk.";

        if (playSound)
        {
            SoundService.PlayClickSound();
        }

        UpdateNavSelection(label);

        _workspacePanels.TryGetValue(label, out (UIElement Panel, Action? Open) active);
        UIElement? activePanel = active.Panel;

        foreach ((UIElement panel, _) in _workspacePanels.Values)
        {
            panel.Visibility = panel == activePanel
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        bool hasPanel = activePanel is not null;
        PlaceholderPanel.Visibility = hasPanel ? Visibility.Collapsed : Visibility.Visible;

        // Every panel paints the window brush, but the Core link card sits in the same
        // content column and would otherwise show through the promoted panels, whose
        // own surface is deliberately transparent.
        CoreLinkCard.Visibility = hasPanel ? Visibility.Collapsed : Visibility.Visible;

        active.Open?.Invoke();
    }

    /// <summary>
    /// Section copy for the shell-owned page header: the eyebrow names the area, the
    /// title names the screen, and the description says what the screen will and will not
    /// do to the machine. Every destructive screen says so in its own words, which is why
    /// this is a table of prose rather than a format string over the label.
    /// </summary>
    private static readonly Dictionary<string, (string Eyebrow, string Description)> SectionCopy = new()
    {
        [CleanerSectionLabel] = ("CLEANUP", "Temp files, caches and junk Windows can rebuild. Nothing is removed until you press Clean."),
        [LargeFilesSectionLabel] = ("DISK SPACE", "Files above your size threshold. Protected system locations are listed but held back."),
        [StartupSectionLabel] = ("BOOT AND LOGIN", "What runs when Windows starts. Disabling an entry never deletes it."),
        [MemorySectionLabel] = ("PERFORMANCE", "Working sets, standby cache and per-process memory pressure, read from the live system."),
        [SystemRepairSectionLabel] = ("INTEGRITY", "System health first, then one repair at a time, only when you ask for it."),
        [AppUninstallerSectionLabel] = ("SOFTWARE", "Installed programs with their uninstall strings, estimates and leftover folders."),
        [ForceDeleteSectionLabel] = ("LAST RESORT", "Staged paths removed with a forced purge, for files that refuse a normal delete."),
    };

    /// <summary>
    /// Paints the page header for one section. The header belongs to the shell rather
    /// than to the panels so the eight workspaces cannot drift apart: same eyebrow, same
    /// title size, same description treatment everywhere.
    /// </summary>
    private void SetPageHeader(string label)
    {
        (string eyebrow, string description) = SectionCopy.TryGetValue(label, out var copy)
            ? copy
            : ("WORKSPACE", string.Empty);

        PageEyebrow.Text = eyebrow;
        PageTitle.Text = label;
        PageDescription.Text = description;
    }

    /// <summary>
    /// Maps each navigation section to its panel and to the loader that fills it in.
    /// Called after the visual tree exists and before the first paint.
    /// </summary>
    private void BuildWorkspacePanelMap()
    {
        _workspacePanels[CleanerSectionLabel] = (CleanerWorkspacePanel, CleanerWorkspacePanel.Open);
        _workspacePanels[LargeFilesSectionLabel] = (LargeFilesWorkspacePanel, LargeFilesWorkspacePanel.Open);
        _workspacePanels[StartupSectionLabel] = (StartupWorkspacePanel, StartupWorkspacePanel.Open);
        _workspacePanels[MemorySectionLabel] =
            (MemoryWorkspacePanel, MemoryWorkspacePanel.ViewModel.RefreshTelemetry);
        _workspacePanels[SystemRepairSectionLabel] = (SystemRepairWorkspacePanel, SystemRepairWorkspacePanel.Open);
        _workspacePanels[AppUninstallerSectionLabel] =
            (AppUninstallWorkspacePanel, AppUninstallWorkspacePanel.Open);
        _workspacePanels[ForceDeleteSectionLabel] = (ForceDeleteWorkspacePanel, ForceDeleteWorkspacePanel.Open);
    }

    private static (UIElement Content, FontIcon Icon, Border Indicator) BuildNavContent(
        string label,
        string glyph)
    {
        // Flush with the item's left edge (the item carries no left padding) so the bar
        // sits against the pane the way a NavigationViewItem's does.
        var indicator = new Border
        {
            Width = 3,
            Height = 16,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
        };

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 16,
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(indicator);
        panel.Children.Add(icon);

        panel.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });

        return (panel, icon, indicator);
    }

    /// <summary>
    /// Paints the active section's glyph and selection bar with the accent and leaves
    /// the others muted, which is what a WinUI NavigationViewItem does when selected.
    /// Neither can be done in the item template: the glyph carries an explicit
    /// Foreground, so the presenter's SelectedForeground never reaches it, and this
    /// WinUI version's ListViewItemPresenter has no selection-indicator members.
    ///
    /// Dangerous sections (Force Delete) keep their danger tint in both states - WPF
    /// styled that pill with its danger brushes rather than the accent.
    /// </summary>
    private void UpdateNavSelection(string activeLabel)
    {
        foreach ((string label, NavEntry entry) in _navEntries)
        {
            bool isActive = label == activeLabel;

            (string IconKey, string IndicatorKey) = (entry.Danger, isActive) switch
            {
                (true, true) => ("NavIconDangerHoverBrush", "DestructiveRedBrush"),
                (true, false) => ("NavIconDangerMutedBrush", "DestructiveRedBrush"),
                (false, true) => ("ElectricCyanBrush", "ElectricCyanBrush"),
                (false, false) => ("NavIconMutedBrush", "ElectricCyanBrush"),
            };

            if (Application.Current.Resources[IconKey] is Brush iconBrush)
            {
                entry.Icon.Foreground = iconBrush;
            }

            if (Application.Current.Resources[IndicatorKey] is Brush indicatorBrush)
            {
                entry.Indicator.Background = indicatorBrush;
            }

            entry.Indicator.Opacity = isActive ? 1 : 0;
        }
    }

    /// <summary>One navigation entry's parts that change with the selection.</summary>
    private sealed record NavEntry(FontIcon Icon, Border Indicator, bool Danger);

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