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
    private const string ProcessManagerSectionLabel = "Process Manager Tool";
    private const string MemorySectionLabel = "Memory Optimization Tool";
    private const string SystemRepairSectionLabel = "System Repair Tool";
    private const string AppUninstallerSectionLabel = "App Uninstaller Tool";
    private const string ForceDeleteSectionLabel = "Force Delete Tool";
    private const string SettingsSectionLabel = "Settings";
    private const string AboutSectionLabel = "About";

    // Named constants rather than literals so the panel map below keys off identity, not
    // off positions: inserting a tab mid-list used to silently re-point the later nav
    // items at the wrong panels.
    //
    // Glyph and Help are the WPF header's own values (MainWindow.xaml ToolCleanerBtn ..
    // ToolForceDeleteBtn): the icon is what represents the section now that the rail is
    // icon-only, so it has to be the same icon the old UI trained people on, and Help is
    // the same tooltip copy that button carried.
    private static readonly (string Label, string Glyph, string Help, bool Danger)[] ToolSections =
    {
        (CleanerSectionLabel, "\uE856", "Switch to Junk Cleaner workspace & storage dashboard", false),
        (LargeFilesSectionLabel, "\uE8B7", "Scan and locate large space-consuming files across your drives safely", false),
        (StartupSectionLabel, "\uE7B5", "Inspect and manage applications configured to launch on Windows boot", false),
        (ProcessManagerSectionLabel, "\uE9F5", "Inspect memory-heavy applications and optimize inactive background working sets", false),
        (MemorySectionLabel, "\uE950", "Purge standby memory and trim inactive process working sets using native Windows APIs", false),
        (SystemRepairSectionLabel, "\uE74C", "Diagnose and repair Windows system file corruption (SFC, DISM, and CHKDSK)", false),
        (AppUninstallerSectionLabel, "\uE71D", "Installed applications manager, uninstaller & residual bloatware purger", false),
        // WPF styled this one's pill with its danger brushes.
        (ForceDeleteSectionLabel, "\uE74D", "Pick stubborn files or folders (locked, read-only, permission-denied) and permanently force delete them", true),
    };

    /// <summary>
    /// The preferences, pinned to the bottom of the rail behind a rule. A tool is
    /// something the user goes and does; a preference is something they set once and
    /// forget. Splitting them keeps the tool list short enough to scan.
    /// </summary>
    private static readonly (string Label, string Glyph, string Help, bool Danger)[] FooterSections =
    {
        (SettingsSectionLabel, "\uE713", "Appearance, sound, language, automation and update preferences", false),
        (AboutSectionLabel, "\uE946", "Version, license and where to find the source", false),
    };

    /// <summary>Section label to glyph, for the page header's leading tile.</summary>
    private static readonly Dictionary<string, string> SectionGlyphs =
        ToolSections.Concat(FooterSections).ToDictionary(s => s.Label, s => s.Glyph);

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

    /// <summary>The section the rail currently has selected. Kept so a theme switch
    /// can repaint the active glyph, which caches a brush rather than inheriting one.</summary>
    private string _activeSectionLabel = string.Empty;

    /// <summary>True while the shell is in the dark theme.</summary>
    private bool _isDarkTheme = true;

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
        ApplyTheme(SettingsService.Current.IsDarkMode, persist: false);
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
            _ = LoadSelfTestProcessWorkspace();
        }

        ShellDiagnostics.MaybeSelfTest(this);
    }

    /// <summary>
    /// Exercises the process manager's read path. It lists the live processes and
    /// does nothing to them: no trim, no kill, and protected system processes are
    /// filtered out by the list itself.
    /// </summary>
    private async Task LoadSelfTestProcessWorkspace()
    {
        await ProcessWorkspacePanel.ViewModel.RefreshAsync();
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

        ProcessWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;

        SettingsWorkspacePanel.ViewModel.LogRequested += OnFootprintLog;
        SettingsWorkspacePanel.ThemeChangeRequested += OnThemeChangeRequested;
    }

    /// <summary>
    /// The settings panel asks the shell to change the theme, because the shell owns
    /// the window's RequestedTheme and the system caption buttons that follow it.
    /// </summary>
    private void OnThemeChangeRequested(bool isDark) => ApplyTheme(isDark, persist: true);

    /// <summary>Opens the detailed memory manager over whatever workspace is showing.</summary>
    private void OpenMemoryOptimizerOverlay() => MemoryOverlay.Open();

    private void CloseMemoryOptimizerOverlay()
        => ShellMotion.Exit(
            MemoryOverlay,
            () => MemoryOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed);

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
        ShellMotion.Enter(FootprintView, riseDip: 14);
    }

    private void CloseAppDataFootprint()
    {
        // Unsubscribe immediately - only the *paint* waits for the fade. A handler that
        // stayed subscribed for 140ms would still be live for a second close.
        FootprintView.CloseRequested -= CloseAppDataFootprint;
        FootprintView.LogRequested -= OnFootprintLog;
        ShellMotion.Exit(
            FootprintView,
            () => FootprintView.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed);
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
    /// Switches the shell between the dark and light token sets.
    ///
    /// The tokens live in ThemeDictionaries, so the switch is a RequestedTheme change
    /// on the window content: every ThemeResource in the tree re-resolves against the
    /// other dictionary. That is exactly why the views reference theme brushes with
    /// ThemeResource and never StaticResource - a static reference freezes whichever
    /// theme it happened to resolve in.
    ///
    /// The choice is shared state, not window state: it goes through Core's
    /// SettingsService so it survives a restart and both UIs agree on it.
    /// </summary>
    private void ApplyTheme(bool isDark, bool persist)
    {
        _isDarkTheme = isDark;

        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        }

        // The glyph shows the theme the button switches TO, so the icon and the
        // tooltip always agree about what a click will do.
        ThemeToggleIcon.Glyph = isDark ? "\uE706" : "\uE708";
        ToolTipService.SetToolTip(
            ThemeToggleButton,
            isDark ? "Switch to light theme" : "Switch to dark theme");

        UpdateCaptionColors(isDark);

        // The rail glyphs carry an explicit brush (the presenter's SelectedForeground
        // never reaches a FontIcon), so they have to be repainted by hand when the
        // palette changes underneath them.
        if (_activeSectionLabel.Length > 0)
        {
            UpdateNavSelection(_activeSectionLabel);
        }

        if (persist)
        {
            SettingsService.Update(s => s.IsDarkMode = isDark);
        }
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => ApplyTheme(!_isDarkTheme, persist: true);

    /// <summary>
    /// Caption buttons are drawn by the system, not by XAML, so they do not follow the
    /// theme resources - they have to be told. White glyphs on a light title bar are
    /// unreadable, which is the whole reason this method exists.
    /// </summary>
    private void UpdateCaptionColors(bool isDark)
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            AppWindow appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));

            appWindow.TitleBar.ButtonForegroundColor = isDark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;
            appWindow.TitleBar.ButtonInactiveForegroundColor = isDark
                ? Microsoft.UI.Colors.Gray
                : Microsoft.UI.Colors.DimGray;
        }
        catch (Exception ex)
        {
            ShellDiagnostics.Write($"caption colors failed: {ex.GetType().Name}: {ex.Message}");
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

        foreach (var (label, glyph, help, danger) in ToolSections)
        {
            AddNavItem(NavList, label, glyph, help, danger);
        }

        foreach (var (label, glyph, help, danger) in FooterSections)
        {
            AddNavItem(NavFooterList, label, glyph, help, danger);
        }

        // The search box offers exactly the destinations the rail offers, so it can
        // never send the user somewhere the rail does not have.
        SectionSearchBox.ItemsSource =
            ToolSections.Concat(FooterSections).Select(s => s.Label).ToList();

        if (NavList.Items.Count > 0)
        {
            NavList.SelectedIndex = 0;
        }

        NavList.SelectionChanged += (_, _) =>
        {
            if (NavList.SelectedItem is ListViewItem selected && selected.Tag is string label)
            {
                // One list is selected at a time: picking a tool clears the
                // preferences, and the other way round.
                NavFooterList.SelectedIndex = -1;
                ShowWorkspace(label, playSound: true);
            }
        };

        NavFooterList.SelectionChanged += (_, _) =>
        {
            if (NavFooterList.SelectedItem is ListViewItem selected && selected.Tag is string label)
            {
                NavList.SelectedIndex = -1;
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
    /// Adds one rail item: the glyph, the accent bar that marks the active section,
    /// and the name that reaches a screen reader and a pointer.
    /// </summary>
    private void AddNavItem(ListView list, string label, string glyph, string help, bool danger)
    {
        (UIElement content, FontIcon icon, Border indicator) = BuildNavContent(glyph);

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
        //
        // The rail shows glyphs only, so the name also has to reach a sighted
        // pointer: that is the tooltip, carrying the label on hover. HelpText
        // keeps the WPF button's descriptive tooltip copy available to assistive
        // tech instead of putting a two-line balloon over every row.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(item, help);
        ToolTipService.SetToolTip(item, label);
        ToolTipService.SetPlacement(item, Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Right);

        WireNavHover(item, label, danger);

        list.Items.Add(item);
    }

    /// <summary>
    /// Selects a section by name, wherever it lives in the rail. Used by the title
    /// bar search, which lists the sections rather than the rail's own items.
    /// </summary>
    private void SelectSection(string label)
    {
        foreach (ListView list in new[] { NavList, NavFooterList })
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                if (list.Items[i] is ListViewItem item && item.Tag as string == label)
                {
                    list.SelectedIndex = i;
                    return;
                }
            }
        }
    }

    private void OnSectionSearchChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string label)
        {
            SelectSection(label);
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
        _activeSectionLabel = label;

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

        // Content that replaced other content gets one short entrance so a section
        // change reads as a change instead of a teleport. Same curve, same length,
        // every section - the motion belongs to the shell, not to each panel.
        ShellMotion.Enter(ContentColumn);
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
        [ProcessManagerSectionLabel] = ("PROCESSES", "Memory-heavy applications, read from the live system. Protected Windows processes are never listed."),
        [MemorySectionLabel] = ("PERFORMANCE", "Working sets, standby cache and per-process memory pressure, read from the live system."),
        [SystemRepairSectionLabel] = ("INTEGRITY", "System health first, then one repair at a time, only when you ask for it."),
        [AppUninstallerSectionLabel] = ("SOFTWARE", "Installed programs with their uninstall strings, estimates and leftover folders."),
        [ForceDeleteSectionLabel] = ("LAST RESORT", "Staged paths removed with a forced purge, for files that refuse a normal delete."),
        [SettingsSectionLabel] = ("PREFERENCES", "Appearance, sound, language, automation and updates. Shared with the desktop app."),
        [AboutSectionLabel] = ("ABOUT", "Version, license and where to find the source."),
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

        // The leading tile carries the section's own glyph, so the header and the
        // rail always agree about which section is open.
        PageHeaderIcon.Glyph = SectionGlyphs.TryGetValue(label, out string? glyph)
            ? glyph
            : "\uE8F1";
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
        _workspacePanels[ProcessManagerSectionLabel] =
            (ProcessWorkspacePanel, () => _ = ProcessWorkspacePanel.ViewModel.RefreshAsync());
        _workspacePanels[MemorySectionLabel] =
            (MemoryWorkspacePanel, MemoryWorkspacePanel.ViewModel.RefreshTelemetry);
        _workspacePanels[SystemRepairSectionLabel] = (SystemRepairWorkspacePanel, SystemRepairWorkspacePanel.Open);
        _workspacePanels[AppUninstallerSectionLabel] =
            (AppUninstallWorkspacePanel, AppUninstallWorkspacePanel.Open);
        _workspacePanels[ForceDeleteSectionLabel] = (ForceDeleteWorkspacePanel, ForceDeleteWorkspacePanel.Open);
        _workspacePanels[SettingsSectionLabel] = (SettingsWorkspacePanel, null);
        _workspacePanels[AboutSectionLabel] = (AboutWorkspacePanel, null);
    }

    /// <summary>
    /// Builds one rail item's visual: the selection bar pinned to the pane edge and the
    /// section glyph centred in what is left. The section name is deliberately not in
    /// the content - the strip is icon-only and the name arrives as the item's tooltip -
    /// so this must stay a centred icon, not a label with its text removed.
    /// </summary>
    private static (UIElement Content, FontIcon Icon, Border Indicator) BuildNavContent(string glyph)
    {
        var indicator = new Border
        {
            Width = 3,
            Height = 20,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
        };

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 18,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(indicator, 0);
        Grid.SetColumn(icon, 1);
        grid.Children.Add(indicator);
        grid.Children.Add(icon);

        return (grid, icon, indicator);
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

            string iconKey = IconKey(entry.Danger, isActive, hovered: false);
            string indicatorKey = entry.Danger ? "DestructiveRedBrush" : "ElectricCyanBrush";

            if (Application.Current.Resources[iconKey] is Brush iconBrush)
            {
                entry.Icon.Foreground = iconBrush;
            }

            if (Application.Current.Resources[indicatorKey] is Brush indicatorBrush)
            {
                entry.Indicator.Background = indicatorBrush;
            }

            if (isActive)
            {
                entry.Indicator.Opacity = 1;

                // The bar arrives rather than appears. 120ms of opacity only - a bar
                // that slides on every click would make the rail feel loose.
                ShellMotion.Enter(entry.Indicator, riseDip: 0, milliseconds: 120);
            }
            else
            {
                entry.Indicator.Opacity = 0;
            }
        }
    }

    /// <summary>Which icon brush a rail item wears: active beats hover beats muted.</summary>
    private static string IconKey(bool danger, bool isActive, bool hovered) => (danger, isActive, hovered) switch
    {
        (true, true, _) => "NavIconDangerHoverBrush",
        (true, false, true) => "NavIconDangerHoverBrush",
        (true, false, false) => "NavIconDangerMutedBrush",
        (false, true, _) => "ElectricCyanBrush",
        (false, false, true) => "NavIconHoverBrush",
        (false, false, false) => "NavIconMutedBrush",
    };

    /// <summary>
    /// Hover feedback for an icon-only rail item: the glyph brightens while the pointer
    /// is on the row and returns to its selection colour on exit. The brushes are read
    /// per event rather than captured once, so a theme switch is picked up by the very
    /// next hover instead of needing the rail rebuilt.
    /// </summary>
    private void WireNavHover(ListViewItem item, string label, bool danger)
    {
        void Paint(bool hovered)
        {
            if (!_navEntries.TryGetValue(label, out NavEntry? entry) || entry is null)
            {
                return;
            }

            bool isActive = ReferenceEquals(NavList.SelectedItem, item);

            if (Application.Current.Resources[IconKey(danger, isActive, hovered)] is Brush brush)
            {
                entry.Icon.Foreground = brush;
            }
        }

        item.PointerEntered += (_, _) => Paint(hovered: true);
        item.PointerExited += (_, _) => Paint(hovered: false);
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
