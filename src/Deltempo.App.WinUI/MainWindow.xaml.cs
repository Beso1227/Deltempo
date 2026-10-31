using System.Reflection;
using System.Runtime.InteropServices;
using Deltempo.App.WinUI.Localization;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using WinRT.Interop;
using WinTempCleaner.Core.Abstractions;
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
    // the same tooltip copy that button carried. LocKey is the LocalizationService key
    // for the visible name; Label stays the stable identity used by the panel map and
    // the selection pill, so a language switch can never re-point a nav row.
    private static readonly (string Label, string LocKey, string Glyph, string Help, bool Danger)[] ToolSections =
    {
        (CleanerSectionLabel, "NavCleaner", "\uE856", "Switch to Junk Cleaner workspace & storage dashboard", false),
        (LargeFilesSectionLabel, "NavLargeFiles", "\uE8B7", "Scan and locate large space-consuming files across your drives safely", false),
        (StartupSectionLabel, "NavStartup", "\uE7B5", "Inspect and manage applications configured to launch on Windows boot", false),
        (ProcessManagerSectionLabel, "NavProcesses", "\uE9F5", "Inspect memory-heavy applications and optimize inactive background working sets", false),
        (MemorySectionLabel, "NavMemory", "\uE950", "Purge standby memory and trim inactive process working sets using native Windows APIs", false),
        (SystemRepairSectionLabel, "NavRepair", "\uE74C", "Diagnose and repair Windows system file corruption (SFC, DISM, and CHKDSK)", false),
        (AppUninstallerSectionLabel, "NavApps", "\uE71D", "Installed applications manager, uninstaller & residual bloatware purger", false),
        // WPF styled this one's pill with its danger brushes.
        (ForceDeleteSectionLabel, "NavForceDelete", "\uE74D", "Pick stubborn files or folders (locked, read-only, permission-denied) and permanently force delete them", true),
    };

    /// <summary>
    /// The preferences, pinned to the bottom of the rail behind a rule. A tool is
    /// something the user goes and does; a preference is something they set once and
    /// forget. Splitting them keeps the tool list short enough to scan.
    /// </summary>
    private static readonly (string Label, string LocKey, string Glyph, string Help, bool Danger)[] FooterSections =
    {
        (SettingsSectionLabel, "NavSettings", "\uE713", "Appearance, sound, language, automation and update preferences", false),
        (AboutSectionLabel, "NavAbout", "\uE946", "Version, license and where to find the source", false),
    };

    /// <summary>Section label to glyph, for the page header's leading tile.</summary>
    private static readonly Dictionary<string, string> SectionGlyphs =
        ToolSections.Concat(FooterSections).ToDictionary(s => s.Label, s => s.Glyph);

    /// <summary>Section label to its LocalizationService key. Label is identity, this is copy.</summary>
    private static readonly Dictionary<string, string> SectionLocKeys =
        ToolSections.Concat(FooterSections).ToDictionary(s => s.Label, s => s.LocKey);

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

    /// <summary>
    /// The rail's item containers, keyed by section label. The selection pill has to know
    /// where the active row is, and only the container can answer that.
    /// </summary>
    private readonly Dictionary<string, ListViewItem> _railItems = [];

    /// <summary>
    /// Every rail item in build order, used for the one-time entrance stagger. A list
    /// rather than a dictionary because that animation cares about the order the rail is
    /// read in, not about lookup.
    /// </summary>
    private readonly List<ListViewItem> _railItemOrder = [];

    /// <summary>The section the rail currently has selected. Kept so a theme switch
    /// can repaint the active glyph, which caches a brush rather than inheriting one.</summary>
    private string _activeSectionLabel = string.Empty;

    /// <summary>True while the shell is in the dark theme.</summary>
    private bool _isDarkTheme = true;

    /// <summary>True once the status bar is carrying a failure message, so a language
    /// switch does not overwrite it with the localized "Ready".</summary>
    private bool _footerHasError;

    // ---------------------------------------------------------------------
    // Activity log stream
    // ---------------------------------------------------------------------

    /// <summary>
    /// The session's log entries, in the order they happened. Every workspace writes
    /// here through <see cref="OnFootprintLog"/>, which is why the collection lives in
    /// the shell and the drawer is a shell row rather than a piece of the cleaner.
    ///
    /// Bounded to the same 2000 entries as WPF's _logs: a long scan or a churn-heavy
    /// clean can otherwise grow the collection and its visual tree without limit.
    /// </summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<LogEntry> _logs = [];

    private const int MaxLogEntries = 2000;

    /// <summary>Timestamp of the last auto-scroll, so appends do not force a layout pass
    /// per line. 120 ms is WPF's throttle.</summary>
    private long _lastLogScrollTicks;

    /// <summary>True while the drawer is showing; drives the unseen badge.</summary>
    private bool _isActivityLogOpen;

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

        // Restore the language the user picked in Settings. Neither shell did this
        // before, which is why a restart always came back in English even though the
        // preference was saved. Set before BuildNavigation so the rail tooltips and
        // the page header are built in the restored language, not re-painted after.
        if (SettingsService.Current.Language is { Length: > 0 } startupLanguage)
        {
            LocalizationService.CurrentLanguage = startupLanguage;
        }

        LocalizationService.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            DisposeMemoryHotkey();
            DisposeMinimizeToTrayHook();

            // The window is gone; the icon handles we handed it via WM_SETICON
            // (taskbar / Alt-Tab) are ours and must not leak.
            if (_iconSmall != IntPtr.Zero)
            {
                DestroyIcon(_iconSmall);
                _iconSmall = IntPtr.Zero;
            }
            if (_iconBig != IntPtr.Zero)
            {
                DestroyIcon(_iconBig);
                _iconBig = IntPtr.Zero;
            }
        };

        ApplyWindowChrome();
        ApplyWindowIcon();
        InitializeTray();
        InitializeMemoryHotkey();
        ApplyTheme(SettingsService.Current.IsDarkMode, persist: false);
        RefreshSoundToggleGlyph(SoundService.IsSoundEnabled);
        OnAlwaysOnTopRequested(SettingsService.Current.MemoryAlwaysOnTop);
        BuildNavigation();
        ApplyLanguage(logToActivity: false);
        VerifyCoreLink();
        WireWorkspaceSurfaces();
        WireActivityLog();

        // The shell paints only the selected workspace, so the self-test drives every
        // other surface's load path directly. SelectionChanged cannot do it: the
        // initial selection is assigned before the handler is attached.
        if (ShellDiagnostics.IsSelfTest)
        {
            OpenAppDataFootprint();
            LoadSelfTestLockedFiles();
            LoadSelfTestForceDelete();
            LoadSelfTestAppUninstall();
            LoadSelfTestSystemRepair();
            LoadSelfTestMemoryWorkspace();
            _ = LoadSelfTestStartupWorkspace();
            _ = LoadSelfTestLargeFilesWorkspace();
            _ = LoadSelfTestCleanerWorkspace();
            _ = LoadSelfTestProcessWorkspace();
            LoadSelfTestSettings();
            LoadSelfTestUpdateOffer();
            LogSelfTestLocalization();

            // Last, and only now: the memory optimizer. Every overlay above closes the
            // one before it, so the final frame of a manual self-test run shows one
            // dialog over a real workspace instead of three stacked on each other.
            OpenMemoryOptimizerForSelfTest();
            LogSelfTestOverlays();
        }
        else if (SettingsService.Current.CheckUpdatesOnStartup)
        {
            // WPF's startup check. Outside the self-test: it reaches the network.
            _ = StartupUpdateCheckAsync();
        }

        ShellDiagnostics.MaybeSelfTest(this);
    }

    /// <summary>
    /// Renders the update offer from a synthetic release and closes it again. Nothing
    /// is fetched, nothing is opened: the self-test proves the card lays out and that
    /// the version and changelog it shows come from the ReleaseInfo it was handed.
    /// </summary>
    private void LoadSelfTestUpdateOffer()
    {
        var probe = new ReleaseInfo
        {
            TagName = "v0.0.0-preview",
            VersionString = "0.0.0",
            ReleaseName = "Self-test release",
            Body = "Self-test changelog line: the offer renders the release body verbatim.",
            IsNewer = true,
            CheckSucceeded = true,
        };

        ShowUpdateOffer(probe);

        ShellDiagnostics.Write(
            $"update offer loaded (self-test, display only): version={UpdateOfferOverlay.Release?.TagName}, " +
            $"visible={UpdateOfferOverlay.Visibility}, build={CurrentVersionLabel}");

        CloseUpdateOffer();
    }

    /// <summary>
    /// Reports which overlays are on screen at the end of the load sequence.
    ///
    /// This is the regression guard for two dialogs stacking: the self-test opens the
    /// footprint, the locked-files inspector and the memory optimizer in turn, and
    /// each of them used to set its own Visibility, so all three stayed visible on top
    /// of one another. Only the last one opened may remain.
    /// </summary>
    private void LogSelfTestOverlays()
    {
        var visible = new List<string>();

        foreach ((string name, FrameworkElement overlay) in new (string Name, FrameworkElement View)[]
                 {
                     (nameof(FootprintView), FootprintView),
                     (nameof(LockedFilesOverlay), LockedFilesOverlay),
                     (nameof(MemoryOverlay), MemoryOverlay),
                     (nameof(CleanSummaryOverlay), CleanSummaryOverlay),
                     (nameof(UpdateOfferOverlay), UpdateOfferOverlay),
                 })
        {
            if (overlay.Visibility == Visibility.Visible)
            {
                visible.Add(name);
            }
        }

        ShellDiagnostics.Write(
            $"overlays exclusive (self-test): {visible.Count} visible [{string.Join(", ", visible)}]");
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
    /// Exercises the settings surface without changing anything.
    ///
    /// Refresh() only reads SettingsService, so it cannot write a setting, register an
    /// Explorer verb or create a scheduled task - the load path is guarded in the
    /// ViewModel for exactly that reason. What is worth proving is that every list the
    /// panel offers still matches the values the settings hold, because a drop-down that
    /// selects nothing is how a stored choice silently becomes unedited.
    /// </summary>
    private void LoadSelfTestSettings()
    {
        SettingsWorkspacePanel.Refresh();

        var vm = SettingsWorkspacePanel.ViewModel;
        int selected =
            (vm.Language is { Length: > 0 } ? 1 : 0) +
            (vm.UpdateChannel is { Length: > 0 } ? 1 : 0) +
            (vm.AiProvider is { Length: > 0 } ? 1 : 0);

        ShellDiagnostics.Write(
            $"settings surface loaded (self-test, display only): category={vm.Category}, " +
            $"choices resolved={selected}/3, " +
            $"shield-safe-delete={vm.SendToRecycleBin}, autopilot={vm.EnableAutoPilot}, " +
            $"online-ai={vm.EnableOnlineAiSafety} (provider={vm.AiProvider}, " +
            $"key-row={(vm.ShowAiApiKeyRow ? "shown" : "hidden")}), " +
            $"{vm.ExcludedPaths.Count} protected path(s), " +
            $"{vm.ExcludedExtensions.Count} protected extension(s)");
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
        HideOverlaysExcept(MemoryOverlay);
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
        // Close/log wiring lives in WireWorkspaceSurfaces so the inspector is dismissible
        // in a real session too; re-subscribing here would double every log line.
        HideOverlaysExcept(LockedFilesOverlay);

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
    /// <summary>
    /// Exercises the Large Files workspace.
    ///
    /// ScanAsync only enumerates and classifies files; it deletes nothing. The
    /// confirmation host is not supplied, so the ViewModel's recycle commands fail
    /// closed even if they were reached.
    ///
    /// The walk is bounded here on purpose. "ALL" walks every fixed drive to depth
    /// 12 and never finishes inside the 20 s self-test window; the user profile is
    /// unbounded too - its duration is whatever Defender and the file metadata
    /// cache decide - which is how this line used to vanish from the log on a cold
    /// run. The application's own folder is a real directory tree that finishes in
    /// milliseconds, so the read-only path is actually observed.
    /// </summary>
    private async Task LoadSelfTestLargeFilesWorkspace()
    {
        var probe = new Deltempo.App.WinUI.ViewModels.LargeFilesWorkspaceViewModel
        {
            ScanScope = AppContext.BaseDirectory,
            MinSizeBytes = 0,
        };

        probe.LogRequested += OnFootprintLog;

        Task scan = probe.ScanAsync();
        Task bound = Task.Delay(TimeSpan.FromSeconds(10));

        if (await Task.WhenAny(scan, bound) != scan)
        {
            ShellDiagnostics.Write(
                "large files scan still running (self-test, read only): wait bound reached");
            return;
        }

        int safeCount = probe.Items.Count(f => f.IsSafe);
        int protectedCount = probe.Items.Count - safeCount;

        ShellDiagnostics.Write(
            $"large files workspace loaded (self-test, read only): " +
            $"{probe.Items.Count} files, {safeCount} safe, {protectedCount} protected, " +
            $"status=\"{probe.StatusText}\", empty=\"{probe.EmptyTitle}\"");
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

        // The per-category Inspect pills must be offered now that a scan populated
        // the TopFiles ledgers: a target with files collected has to say so, or the
        // pill would stay invisible and the inspector unreachable.
        int inspectable = probe.Targets.Count(t => t.HasTopFiles);
        ShellDiagnostics.Write(
            $"top-files inspector (self-test, display only): {inspectable}/{probe.Targets.Count} " +
            $"categories offer Inspect, largest=\"{(probe.Targets.Where(t => t.HasTopFiles).SelectMany(t => t.TopFiles).OrderByDescending(f => f.SizeBytes).FirstOrDefault()?.FormattedSize ?? "-")}\"");

        // The header has to fit at the 800px minimum window width: 800 - 64px rail -
        // 36px of view padding = 700px of content. The stats row is not scrollable, so
        // it must fit outright; the action strip scrolls by design, the way the WPF
        // hero strip does, so its number only decides whether a scrollbar shows.
        var (statsWidth, actionsWidth) = CleanerWorkspacePanel.MeasureHeaderAt(700);
        ShellDiagnostics.Write(
            $"cleaner header fit (self-test, layout only): stats {statsWidth:N0}px " +
            $"{(statsWidth > 700 ? "OVERFLOW" : "fit")}, actions {actionsWidth:N0}px " +
            $"{(actionsWidth > 700 ? "scroll (WPF does the same)" : "fit")} in 700px");

        // The drive card is rendered by the panel's own ViewModel at construction -
        // DriveInfo reads only, no benchmark, no boost. Report what it painted.
        var shellVm = CleanerWorkspacePanel.ViewModel;
        ShellDiagnostics.Write(
            $"drive telemetry card (self-test, display only): \"{shellVm.DriveLabel}\", " +
            $"{shellVm.DriveFreeText}, {shellVm.DriveDetailsText}, {shellVm.DriveHealthText}, " +
            $"session={shellVm.SessionFreedText}");
        ShellDiagnostics.Write(
            $"hero actions (self-test, display only): boost \"{shellVm.GameBoostText}\" " +
            $"active={GameBoostService.IsBoostActive}, benchmark \"{shellVm.BenchmarkText}\"");

        // The shield is a toggle the port had hardcoded to false, so the only thing worth
        // proving here is that the value the user sets reaches the copy they read before a
        // clean - display only, no clean is started.
        var preview = WinTempCleaner.ViewModels.CleaningPipelineViewModel
            .BuildConfirmationPreview(probe.AllTargets, safeMode: true, recycleBin: true);
        ShellDiagnostics.Write(
            $"cleaner safety shield wiring (self-test, display only): toggle={probe.SafeMode}, " +
            $"preview=\"{preview.ShieldText}\"");

        // The drawer is display-only: open it, append one synthetic line, and report the
        // state the dock button now shows. No clean, no scan, no file touched.
        ToggleActivityLog();
        AppendActivityLog(new LogEntry
        {
            Message = "Self-test: activity log drawer rendered from the shared LogEntry model.",
        });
        ShellDiagnostics.Write(
            $"activity log drawer loaded (self-test, display only): entries={_logs.Count}, " +
            $"open={_isActivityLogOpen}, badge={(_logs.Count > 0 && !_isActivityLogOpen ? "visible" : "hidden")}");

        // The completion surface is display-only, so it is safe to fill from synthetic
        // numbers: Show() writes text and nothing else. Export stays behind a click, and
        // the self-test never clicks.
        LoadSelfTestCleanSummary(probe);
    }

    /// <summary>
    /// Fills the cleanup summary with a synthetic run so the self-test proves the overlay
    /// loads and formats, without deleting anything.
    /// </summary>
    private void LoadSelfTestCleanSummary(Deltempo.App.WinUI.ViewModels.CleanerWorkspaceViewModel probe)
    {
        var summary = new WinTempCleaner.Models.CleanSummary
        {
            TotalFreedBytes = 1_928_734_720,
            TotalFilesDeleted = 12_480,
            TotalFoldersDeleted = 214,
            TotalFilesSkipped = 9,
            TotalFilesInUse = 6,
            TotalFilesRecentShielded = 2,
            TotalFilesPolicyProtected = 1,
            ElapsedTime = TimeSpan.FromSeconds(4.7),
        };

        CleanSummaryOverlay.Show(summary, probe.AllTargets, safeMode: false);
        ShellDiagnostics.Write(
            $"clean summary loaded (self-test, display only): " +
            $"{summary.FormattedFreedSize}, {summary.TotalFilesDeleted:N0} files, " +
            $"{summary.TotalFilesSkipped} kept");

        // The deep-clean variant renders differently: its own title, the RAM figure the
        // disk stat cannot carry, and no audit export - its per-category ledger would
        // describe work the engine never did. Filling it here proves the branch loads.
        var deep = new WinTempCleaner.Models.CleanSummary
        {
            IsDeepClean = true,
            TotalFreedBytes = 3_650_722_201,
            RamFreedBytes = 2_684_354_560,
            TotalFilesDeleted = 21_906,
            TotalFoldersDeleted = 431,
            TotalFilesSkipped = 14,
            ElapsedTime = TimeSpan.FromSeconds(38.2),
        };

        CleanSummaryOverlay.Show(deep, probe.AllTargets, safeMode: false);
        ShellDiagnostics.Write(
            $"deep clean summary loaded (self-test, display only): " +
            $"{deep.FormattedFreedSize} disk, {deep.FormattedRamFreed} RAM, " +
            $"{deep.TotalFilesDeleted:N0} files");
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
        CleanerWorkspacePanel.TopFilesRequested += OpenTopFilesInspector;
        // The drive card's "Large Files" pill (WPF MainWindow.xaml:770) walks to the
        // Large Files workspace the same way every other shell hand-off does.
        CleanerWorkspacePanel.LargeFilesRequested += () => NavigateTo(LargeFilesSectionLabel);
        // The RAM card's "RAM Cleaner" pill (WPF OpenMemoryCleanerModal_Click,
        // MainWindow.Memory.cs:41) walks to the Memory workspace likewise.
        CleanerWorkspacePanel.MemoryCleanerRequested += () => NavigateTo(MemorySectionLabel);
        CleanerWorkspacePanel.ViewModel.CleanCompleted += OnCleanCompleted;

        // The locked-files inspector is a real surface, not a self-test prop. WPF opened
        // it from the celebration any time a run kept files; wiring it here (once, in
        // every entry path) is what gives its close button and log lines a subscriber
        // outside the self-test.
        LockedFilesOverlay.LogRequested += OnFootprintLog;
        LockedFilesOverlay.CloseRequested += CloseLockedFilesOverlay;
        CleanSummaryOverlay.ViewLockedFilesRequested += ShowLockedFilesFromCleanSummary;

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
        SettingsWorkspacePanel.AlwaysOnTopRequested += OnAlwaysOnTopRequested;
        SettingsWorkspacePanel.CheckForUpdatesRequested += () => _ = CheckForUpdatesAsync(silent: false);
    }

    /// <summary>
    /// Connects the drawer to the shell's stream.
    ///
    /// WPF kept the log inside the cleaner: one ObservableCollection on MainWindow,
    /// an ItemsControl under the dock, and AddLog appending from every scan and clean.
    /// The port had the collection writer and the panel missing, so every workspace's
    /// log line went to shell-startup.log and nowhere the user could read it.
    /// </summary>
    private void WireActivityLog()
    {
        ActivityLogPanel.Bind(_logs);
        ActivityLogPanel.CopyRequested += CopyActivityLog;
        ActivityLogPanel.ClearRequested += ClearActivityLog;
        CleanerWorkspacePanel.ActivityLogToggleRequested += ToggleActivityLog;
        UpdateActivityLogChrome();
    }

    /// <summary>
    /// Opens or closes the drawer. WPF's ToggleLogBtn_Click did the same and also moved
    /// the bottom corner radius from the dock to the drawer so the open pair still read
    /// as one rounded stack; here the drawer is a shell row, so the only state is
    /// visibility plus the button's own label, glyph and badge.
    /// </summary>
    private void ToggleActivityLog()
    {
        if (_isActivityLogOpen)
        {
            CloseActivityLog();
            return;
        }

        _isActivityLogOpen = true;
        ActivityLogPanel.Visibility = Visibility.Visible;
        UpdateActivityLogChrome();
        ActivityLogPanel.ScrollToEnd();
    }

    private void CloseActivityLog()
    {
        _isActivityLogOpen = false;
        ActivityLogPanel.Visibility = Visibility.Collapsed;
        UpdateActivityLogChrome();
    }

    /// <summary>
    /// Pushes visibility and the unseen dot onto the cleaner's dock button. WPF showed
    /// the dot whenever the drawer was closed and the stream was not empty, which is what
    /// "you have output you are not looking at" means there.
    /// </summary>
    private void UpdateActivityLogChrome()
        => CleanerWorkspacePanel.SetLogState(_isActivityLogOpen, _logs.Count > 0);

    /// <summary>
    /// Appends one line to the stream. Must run on the UI thread - the drawer's
    /// collection is bound to a visual tree.
    /// </summary>
    private void AppendActivityLog(LogEntry entry)
    {
        _logs.Add(entry);

        // Bounded stream: drop the oldest entries so a long scan cannot grow the
        // collection and its visual tree without limit.
        int overflow = _logs.Count - MaxLogEntries;
        for (int i = 0; i < overflow; i++)
        {
            _logs.RemoveAt(0);
        }

        // Throttled auto-scroll: forcing a layout pass per entry is what made the WPF
        // drawer stall under a fast scan.
        if (_isActivityLogOpen &&
            System.Diagnostics.Stopwatch.GetElapsedTime(_lastLogScrollTicks).TotalMilliseconds >= 120)
        {
            _lastLogScrollTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            ActivityLogPanel.ScrollToEnd();
        }

        // The badge only changes when the stream goes from empty to not empty (or back),
        // so there is no reason to touch the dock on every line.
        if (!_isActivityLogOpen && _logs.Count == 1)
        {
            UpdateActivityLogChrome();
        }
    }

    private void CopyActivityLog()
    {
        var text = new System.Text.StringBuilder();
        foreach (var entry in _logs)
        {
            text.AppendLine($"[{entry.FormattedTime}] [{entry.Level}] {entry.Message}");
        }

        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text.ToString());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            // The clipboard is a shared OS resource; another process can hold it.
            ShellDiagnostics.Write($"activity log copy failed: {ex.Message}");
            return;
        }

        AppendActivityLog(new LogEntry { Message = "Activity log copied to clipboard." });
    }

    private void ClearActivityLog()
    {
        _logs.Clear();
        UpdateActivityLogChrome();
    }

    // ---------------------------------------------------------------------
    // Updates
    // ---------------------------------------------------------------------

    /// <summary>The release being offered, if the offer is open.</summary>
    private ReleaseInfo? _pendingRelease;

    /// <summary>This build's version, as the update surfaces name it.</summary>
    private static string CurrentVersionLabel =>
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}";

    /// <summary>
    /// Asks the release feed whether a newer build exists.
    ///
    /// Read-only in both directions: it queries GitHub and reports what it found. A
    /// newer release opens the offer, which hands the download to the browser - this
    /// shell never downloads or replaces itself. A silent check (startup, tray timer)
    /// does not re-offer a version the user already dismissed, which is the rule WPF
    /// applied through DismissedVersion.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool silent)
    {
        SettingsWorkspacePanel.ViewModel.BeginUpdateCheck();

        bool includePrereleases = string.Equals(
            SettingsService.Current.UpdateChannel,
            "PreRelease",
            StringComparison.OrdinalIgnoreCase);

        try
        {
            ReleaseInfo? release = await UpdateService.CheckForUpdatesAsync(includePrereleases);

            SettingsService.Update(s => s.LastUpdateCheckTimestamp = DateTime.Now.ToString("g"));

            if (release is null)
            {
                SettingsWorkspacePanel.ViewModel.ReportUpdateCheck(
                    "Could not reach the release feed. Nothing was changed.",
                    isError: !silent);
                return;
            }

            if (release.IsNewer)
            {
                bool alreadyDismissed =
                    !string.IsNullOrEmpty(release.VersionString) &&
                    release.VersionString.Equals(
                        SettingsService.Current.DismissedVersion,
                        StringComparison.OrdinalIgnoreCase);

                if (silent && alreadyDismissed)
                {
                    SettingsWorkspacePanel.ViewModel.ReportUpdateCheck(
                        $"{release.TagName} is available; this version was dismissed, so it is not offered again.");
                    return;
                }

                SettingsWorkspacePanel.ViewModel.ReportUpdateCheck(
                    $"Version {release.VersionString} is available. This build is {CurrentVersionLabel}.");

                _pendingRelease = release;
                ShowUpdateOffer(release);
                return;
            }

            // A feed that answered with a problem reports CheckSucceeded == false; that
            // is a failed check, not "you are up to date".
            if (!release.CheckSucceeded)
            {
                SettingsWorkspacePanel.ViewModel.ReportUpdateCheck(
                    $"Update check failed: {release.StatusMessage}",
                    isError: true);
                return;
            }

            SettingsWorkspacePanel.ViewModel.ReportUpdateCheck(
                $"This build is up to date ({CurrentVersionLabel}).");
        }
        catch (Exception ex)
        {
            SettingsWorkspacePanel.ViewModel.ReportUpdateCheck(
                $"Update check failed: {ex.Message}",
                isError: true);
        }
    }

    /// <summary>Shows the offer over whatever workspace is open.</summary>
    private void ShowUpdateOffer(ReleaseInfo release)
    {
        HideOverlaysExcept(UpdateOfferOverlay);
        UpdateOfferOverlay.Show(release, CurrentVersionLabel);
        UpdateOfferOverlay.Dismissed -= OnUpdateOfferDismissed;
        UpdateOfferOverlay.Dismissed += OnUpdateOfferDismissed;
        UpdateOfferOverlay.Visibility = Visibility.Visible;
        ShellMotion.Enter(UpdateOfferOverlay, riseDip: 16);

        OnFootprintLog($"New version available: {release.TagName}", WinTempCleaner.Models.LogLevel.Info);
    }

    /// <summary>
    /// "Later". WPF persisted the version so a silent check would not offer it again;
    /// that is the whole reason the setting exists.
    /// </summary>
    private void OnUpdateOfferDismissed(ReleaseInfo release)
    {
        if (!string.IsNullOrEmpty(release.VersionString))
        {
            SettingsService.Update(s => s.DismissedVersion = release.VersionString);
        }

        _pendingRelease = null;
        CloseUpdateOffer();
    }

    private void CloseUpdateOffer()
    {
        UpdateOfferOverlay.Dismissed -= OnUpdateOfferDismissed;
        ShellMotion.Exit(
            UpdateOfferOverlay,
            () => UpdateOfferOverlay.Visibility = Visibility.Collapsed);
    }

    /// <summary>
    /// WPF's startup check: two and a half seconds after the window opens, and only
    /// when the user left "check on launch" on. Silent, so a dismissed version stays
    /// dismissed; never runs in the self-test, which must not touch the network.
    /// </summary>
    private async Task StartupUpdateCheckAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2500));
            await CheckForUpdatesAsync(silent: true);
        }
        catch (Exception ex)
        {
            OnFootprintLog(
                $"Startup update check failed: {ex.Message}",
                WinTempCleaner.Models.LogLevel.Warning);
        }
    }

    /// <summary>
    /// The settings panel asks the shell to change the theme, because the shell owns
    /// the window's RequestedTheme and the system caption buttons that follow it.
    /// </summary>
    private void OnThemeChangeRequested(bool isDark) => ApplyTheme(isDark, persist: true);

    /// <summary>
    /// "Always on top" is a property of this window, so the panel reports it and the
    /// shell applies it. WPF set Topmost on its window; the WinUI equivalent is the
    /// overlay presenter, which does not need a P/Invoke.
    /// </summary>
    private void OnAlwaysOnTopRequested(bool alwaysOnTop)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = alwaysOnTop;
        }
    }

    /// <summary>Opens the detailed memory manager over whatever workspace is showing.</summary>
    private void OpenMemoryOptimizerOverlay()
    {
        HideOverlaysExcept(MemoryOverlay);
        MemoryOverlay.Open();
    }

    /// <summary>
    /// Collapses every overlay except the one about to open.
    ///
    /// Each overlay set its own Visibility and knew nothing about the others, so opening
    /// the Memory Optimizer while the Locked Files inspector was up stacked two dialogs,
    /// each translucent enough to read the other through. WPF could not do that: every
    /// modal was a child of one grid and opening one closed the last. This restores that
    /// rule without moving a single view.
    /// </summary>
    private void HideOverlaysExcept(FrameworkElement keep)
    {
        foreach (FrameworkElement overlay in new FrameworkElement[]
                 {
                     LockedFilesOverlay,
                     MemoryOverlay,
                     FootprintView,
                     CleanSummaryOverlay,
                     UpdateOfferOverlay,
                 })
        {
            if (!ReferenceEquals(overlay, keep) && overlay.Visibility == Visibility.Visible)
            {
                overlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void CloseMemoryOptimizerOverlay()
        => ShellMotion.Exit(
            MemoryOverlay,
            () => MemoryOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed);

    private void CloseLockedFilesOverlay()
        => ShellMotion.Exit(
            LockedFilesOverlay,
            () => LockedFilesOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed);

    /// <summary>
    /// Shows the post-clean summary over whatever is on screen.
    ///
    /// WPF opened CelebrationModalOverlay from ExecuteCleanupAsync. The run itself lives
    /// in the ViewModel, which reports the numbers it actually produced; the shell only
    /// decides where they appear.
    /// </summary>
    private void OnCleanCompleted(WinTempCleaner.Models.CleanSummary summary)
    {
        // The result cue belongs to the run, not to a button: this fires for the selected
        // clean and the deep clean alike, and the rule lives in SoundService so the WPF
        // app answers a run with the same sound.
        SoundService.PlayCleanResultSound(summary);

        HideOverlaysExcept(CleanSummaryOverlay);
        CleanSummaryOverlay.Show(
            summary,
            CleanerWorkspacePanel.ViewModel.AllTargets,
            CleanerWorkspacePanel.ViewModel.LastRunUsedSafeMode);
        CleanSummaryOverlay.CloseRequested -= CloseCleanSummary;
        CleanSummaryOverlay.CloseRequested += CloseCleanSummary;
        CleanSummaryOverlay.Visibility = Visibility.Visible;
        ShellMotion.Enter(CleanSummaryOverlay, riseDip: 16);
    }

    private void CloseCleanSummary()
    {
        CleanSummaryOverlay.CloseRequested -= CloseCleanSummary;
        ShellMotion.Exit(
            CleanSummaryOverlay,
            () => CleanSummaryOverlay.Visibility = Visibility.Collapsed);
    }

    /// <summary>
    /// Opens the in-use inspector for the run that just finished.
    ///
    /// WPF collapsed the celebration and populated the inspector in the same click, with
    /// no transition between the two; keeping that here means the user never sees both
    /// dialogs at once, which is also what HideOverlaysExcept enforces.
    /// </summary>
    private void ShowLockedFilesFromCleanSummary()
    {
        CleanSummaryOverlay.Visibility = Visibility.Collapsed;
        OpenLockedFilesInspector();
    }

    /// <summary>
    /// Builds the inspector's rows from the last run's audit trail.
    ///
    /// A file kept because a process held it open leaves exactly one trace: an audit
    /// record whose category is FileLocked. The record also carries the locking process
    /// and size, which is what the inspector shows and what the reboot-purge action
    /// needs.
    /// </summary>
    private void OpenLockedFilesInspector()
    {
        List<LockedFileItem> locked = WinTempCleaner.Core.Cleaning.LockedFileCollector.FromTargets(
            CleanerWorkspacePanel.ViewModel.AllTargets);

        HideOverlaysExcept(LockedFilesOverlay);
        LockedFilesOverlay.Load(locked);
    }

    /// <summary>
    /// WPF's InspectTarget_Click was the same trade: the top files a category's scan
    /// already collected get an inspector, without re-walking the disk and without
    /// arming a delete. The rows carry TargetFolderInfo, so the shell receives the
    /// target from the view and the overlay renders it.
    /// </summary>
    private void OpenTopFilesInspector(TargetFolderInfo target)
    {
        HideOverlaysExcept(TopFilesOverlay);
        TopFilesOverlay.Open(target);
        OnFootprintLog(
            $"Opened the top-files inspector for {target.Name} (read only - nothing is deleted)",
            LogLevel.Info);
    }

    private void CloseTopFilesOverlay() => TopFilesOverlay.Close();

    /// <summary>
    /// Escape closes whichever overlay is on top.
    ///
    /// The WPF window listed every modal in Window_KeyDown, because an overlay that
    /// covers the whole window leaves no way back otherwise. Only three surfaces are
    /// genuine overlays in this shell; the rest are workspaces or ContentDialogs, and a
    /// dialog already answers Escape itself.
    /// </summary>
    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (UpdateOfferOverlay.Visibility == Visibility.Visible && _pendingRelease is { } pending)
        {
            // Escape means "later", and later has always meant "do not offer this
            // version again on a silent check".
            OnUpdateOfferDismissed(pending);
            args.Handled = true;
        }
        else if (CleanSummaryOverlay.Visibility == Visibility.Visible)
        {
            CloseCleanSummary();
            args.Handled = true;
        }
        else if (MemoryOverlay.Visibility == Visibility.Visible)
        {
            CloseMemoryOptimizerOverlay();
            args.Handled = true;
        }
        else if (FootprintView.Visibility == Visibility.Visible)
        {
            CloseAppDataFootprint();
            args.Handled = true;
        }
        else if (TopFilesOverlay.Visibility == Visibility.Visible)
        {
            CloseTopFilesOverlay();
            args.Handled = true;
        }
        else if (LockedFilesOverlay.Visibility == Visibility.Visible)
        {
            CloseLockedFilesOverlay();
            args.Handled = true;
        }
    }

    /// <summary>
    /// F5 / Ctrl+R re-runs the scan of the workspace that is showing.
    ///
    /// WPF bound both keys to the cleaner's Scan button because the cleaner dashboard
    /// was the only place a scan lived. Here every list workspace owns its own refresh,
    /// so the same key refreshes what the user is actually looking at.
    /// </summary>
    private void OnRefreshInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (CleanerWorkspacePanel.Visibility == Visibility.Visible)
        {
            CleanerWorkspacePanel.ViewModel.ScanCommand.Execute(null);
        }
        else if (LargeFilesWorkspacePanel.Visibility == Visibility.Visible)
        {
            LargeFilesWorkspacePanel.ViewModel.ScanCommand.Execute(null);
        }
        else if (StartupWorkspacePanel.Visibility == Visibility.Visible)
        {
            StartupWorkspacePanel.ViewModel.RefreshCommand.Execute(null);
        }
        else if (ProcessWorkspacePanel.Visibility == Visibility.Visible)
        {
            ProcessWorkspacePanel.ViewModel.RefreshCommand.Execute(null);
        }
        else if (AppUninstallWorkspacePanel.Visibility == Visibility.Visible)
        {
            AppUninstallWorkspacePanel.ViewModel.RefreshCommand.Execute(null);
        }
        else
        {
            return;
        }

        args.Handled = true;
    }

    /// <summary>
    /// Ctrl+Enter starts the cleaner's clean, confirmation first.
    ///
    /// WPF did the same from any workspace. Restricting it to the cleaner removes the
    /// one way a stray key chord could start a destructive operation from a screen that
    /// shows no selection.
    /// </summary>
    private void OnCleanSelectedInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (CleanerWorkspacePanel.Visibility != Visibility.Visible)
        {
            return;
        }

        // A summary is on top of the cleaner; a stray chord must not start another run
        // behind it.
        if (CleanSummaryOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        if (CleanerWorkspacePanel.ViewModel.CleanSelectedCommand.CanExecute(null))
        {
            CleanerWorkspacePanel.ViewModel.CleanSelectedCommand.Execute(null);
            args.Handled = true;
        }
    }

    /// <summary>
    /// The close button on a promoted panel returns to the first workspace, which
    /// is what closing the matching WPF modal did
    /// (AppDataFootprintModalOverlay.Closed =&gt; SwitchWorkspaceView(Cleaner)).
    /// </summary>
    private void ReturnToFirstWorkspace() => NavList.SelectedIndex = 0;

    private void OpenAppDataFootprint()
    {
        HideOverlaysExcept(FootprintView);
        FootprintView.LogRequested += OnFootprintLog;
        FootprintView.CloseRequested -= CloseAppDataFootprint;
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

    /// <summary>
    /// Every workspace reports through this one handler: to the diagnostic file always,
    /// and to the on-screen drawer when the shell is running normally.
    ///
    /// The dispatch is asynchronous because scan and clean run off the UI thread and must
    /// never stall waiting for the log to paint - the same reason WPF switched from
    /// Dispatcher.Invoke to BeginInvoke.
    /// </summary>
    private void OnFootprintLog(string message, WinTempCleaner.Models.LogLevel level)
    {
        ShellDiagnostics.Write($"[{level}] {message}");

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
        };

        DispatcherQueue.TryEnqueue(() => AppendActivityLog(entry));
    }

    // ---------------------------------------------------------------------
    // Global memory hotkey
    // ---------------------------------------------------------------------

    /// <summary>Id passed to RegisterHotKey. Deltempo-only atom, unrelated to the tray.</summary>
    private const int MemoryHotkeyId = 0xD319;

    /// <summary>Subclass id on the window HWND. TrayService uses 0xD317 on the same
    /// window; comctl32 supports any number of subclasses per HWND.</summary>
    private const nuint MemoryHotkeySubclassId = 0xD319;

    private const int WM_HOTKEY = 0x0312;

    private static readonly SUBCLASSPROC MemoryHotkeySubclassProc = OnMemoryHotkeyMessage;

    /// <summary>The shell that registered the hotkey. The window class is
    /// single-instance by mutex, so one owner is the whole truth.</summary>
    private static MainWindow? _memoryHotkeyOwner;

    private IntPtr _memoryHotkeyHwnd;
    private bool _isMemoryHotkeyRegistered;
    private string _memoryHotkeyLabel = string.Empty;

    // ------------------------------------------------------------------
    // Minimize-to-tray. WPF's MinimizeButton_Click (MainWindow.xaml.cs:1130)
    // routed the custom caption button through TrayService.MinimizeToTray when
    // the persisted MinimizeToTray setting was on. The WinUI shell draws its
    // caption buttons from the OS, so the same behaviour is reached by
    // intercepting SC_MINIMIZE in this third subclass on the window HWND
    // (tray 0xD317 and hotkey 0xD319 already share it). The SubclassId is
    // Deltempo-unique like its siblings.
    // ------------------------------------------------------------------
    private const uint WM_SYSCOMMAND = 0x0112;
    private const long SC_MINIMIZE = 0xF020;
    private const nuint MinimizeTraySubclassId = 0xD31A;

    private static readonly SUBCLASSPROC MinimizeTraySubclassProc = OnMinimizeTrayMessage;
    private static MainWindow? _minimizeTrayOwner;

    /// <summary>
    /// Installs the SC_MINIMIZE interceptor. Depends on the tray (a hidden window
    /// the tray cannot restore from would orphan the app), so it lives inside
    /// the tray-visible branch of InitializeTray. Skipped in the self-test.
    /// </summary>
    private void InitializeMinimizeToTrayHook()
    {
        if (ShellDiagnostics.IsSelfTest) return;

        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (!SetWindowSubclass(hwnd, MinimizeTraySubclassProc, MinimizeTraySubclassId, IntPtr.Zero))
            {
                ShellDiagnostics.Write("[Tray] minimize-to-tray subclass failed; the setting stays inert.");
                return;
            }

            _minimizeTrayOwner = this;
        }
        catch (Exception ex)
        {
            ShellDiagnostics.Write($"[Tray] minimize-to-tray hook suppressed: {ex.Message}");
        }
    }

    /// <summary>Unwinds the subclass. Safe to call twice.</summary>
    private static void DisposeMinimizeToTrayHook()
    {
        if (_minimizeTrayOwner is not { } owner) return;

        try
        {
            RemoveWindowSubclass(WindowNative.GetWindowHandle(owner), MinimizeTraySubclassProc, MinimizeTraySubclassId);
        }
        catch
        {
            // Window already gone: nothing to unwind.
        }

        _minimizeTrayOwner = null;
    }

    /// <summary>
    /// SC_MINIMIZE handler: swallow the minimize and hide to tray, exactly what
    /// WPF's MinimizeButton_Click did; otherwise fall through to the system.
    /// Reads the live setting so toggling MinimizeToTray in Settings takes effect
    /// on the next minimize without a restart.
    /// </summary>
    private static IntPtr OnMinimizeTrayMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (_minimizeTrayOwner is { } owner
            && uMsg == WM_SYSCOMMAND
            && (wParam.ToInt64() & 0xFFF0) == SC_MINIMIZE
            && SettingsService.Current.MinimizeToTray
            && TrayService.IsVisible)
        {
            owner.DispatcherQueue.TryEnqueue(owner.HideToTray);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private delegate IntPtr SUBCLASSPROC(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass,
        IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Registers the stored MemoryGlobalHotkey (default CTRL+SHIFT+M) as a system-wide
    /// RAM optimization trigger - a port of WPF's OnSourceInitialized block, lines
    /// 164-186 of src/Deltempo.App.Wpf/MainWindow.xaml.cs.
    ///
    /// Skipped in the self-test: a global hotkey is not a display surface, and
    /// stealing the combination from whoever is typing would be a side effect.
    /// Also skipped under MemoryCompactMode, exactly as WPF honours that setting.
    /// </summary>
    private void InitializeMemoryHotkey()
    {
        if (ShellDiagnostics.IsSelfTest) return;
        if (SettingsService.Current.MemoryCompactMode) return;

        try
        {
            if (!HotkeyParser.TryParse(
                    SettingsService.Current.MemoryGlobalHotkey,
                    out uint modifiers,
                    out uint virtualKey))
            {
                return;
            }

            _memoryHotkeyLabel = SettingsService.Current.MemoryGlobalHotkey;
            _memoryHotkeyHwnd = WindowNative.GetWindowHandle(this);

            if (!RegisterHotKey(_memoryHotkeyHwnd, MemoryHotkeyId, modifiers, virtualKey))
            {
                int error = Marshal.GetLastWin32Error();
                ShellDiagnostics.Write(
                    $"[Hotkey] registration failed for '{_memoryHotkeyLabel}' (Win32 error {error}); the memory hotkey is off.");
                return;
            }

            _isMemoryHotkeyRegistered = true;
            _memoryHotkeyOwner = this;

            if (!SetWindowSubclass(_memoryHotkeyHwnd, MemoryHotkeySubclassProc, MemoryHotkeySubclassId, IntPtr.Zero))
            {
                // Without the subclass the key fires but nobody hears it: worse than
                // not registering at all, because the combination is then globally
                // eaten. Undo the registration and leave the key to its usual owner.
                UnregisterHotKey(_memoryHotkeyHwnd, MemoryHotkeyId);
                _isMemoryHotkeyRegistered = false;
                _memoryHotkeyOwner = null;
                ShellDiagnostics.Write("[Hotkey] window subclass failed; the memory hotkey is off.");
                return;
            }

            ShellDiagnostics.Write($"[Hotkey] '{_memoryHotkeyLabel}' registered for instant RAM optimization.");
        }
        catch (Exception ex)
        {
            ShellDiagnostics.Write($"[Hotkey] registration suppressed: {ex.Message}");
        }
    }

    /// <summary>Unwinds registration and subclass. Safe to call twice.</summary>
    private void DisposeMemoryHotkey()
    {
        if (_memoryHotkeyHwnd == IntPtr.Zero) return;

        if (_isMemoryHotkeyRegistered)
        {
            UnregisterHotKey(_memoryHotkeyHwnd, MemoryHotkeyId);
        }

        RemoveWindowSubclass(_memoryHotkeyHwnd, MemoryHotkeySubclassProc, MemoryHotkeySubclassId);

        _isMemoryHotkeyRegistered = false;
        _memoryHotkeyOwner = null;
        _memoryHotkeyHwnd = IntPtr.Zero;
    }

    /// <summary>
    /// Static because comctl32 keeps the delegate alive for the window's lifetime;
    /// the owner routes the message to the live shell's dispatcher.
    /// </summary>
    private static IntPtr OnMemoryHotkeyMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY && wParam.ToInt32() == MemoryHotkeyId && _memoryHotkeyOwner is not null)
        {
            var owner = _memoryHotkeyOwner;
            owner.DispatcherQueue.TryEnqueue(() => _ = owner.RunMemoryHotkeyAsync());
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// The optimize the hotkey promises. The wasted-Awaits trap WPF documented applies
    /// identically here: a fire-and-forget Task whose exception nobody reads dies as an
    /// UnobservedTaskException, so the catch keeps the failure in the Activity Log.
    /// </summary>
    private async Task RunMemoryHotkeyAsync()
    {
        try
        {
            var result = await MemoryOptimizerService.OptimizeRamAsync();
            MemoryWorkspacePanel.ViewModel.RefreshTelemetry();
            OnFootprintLog(
                $"[Hotkey {_memoryHotkeyLabel}] Instant RAM optimization complete: " +
                $"{result.SucceededTargetCount}/{result.RequestedTargetCount} targets confirmed in {result.ExecutionTimeMs}ms. " +
                $"Available RAM moved {result.FormattedMeasuredDelta}; purged cache is re-read from disk on demand.",
                LogLevel.Success);
        }
        catch (Exception ex)
        {
            OnFootprintLog($"[Hotkey {_memoryHotkeyLabel}] RAM optimization failed: {ex.Message}", LogLevel.Warning);
        }
    }

    // ---------------------------------------------------------------------
    // Tray
    // ---------------------------------------------------------------------

    private bool _isExiting;

    /// <summary>
    /// Creates the tray icon when Settings asks for one.
    ///
    /// WPF did the same thing from its MainWindow with the WPF TrayService overload.
    /// The shell owns the presentation - the flyout menu, hide and restore - because
    /// those need WinUI types; the icon, tooltip, Explorer-restart recovery, balloon
    /// notifications and the low-disk alert are the shared Core implementation.
    /// </summary>
    private void InitializeTray()
    {
        if (!SettingsService.Current.MemoryShowInTray)
        {
            return;
        }

        IntPtr hwnd = WindowNative.GetWindowHandle(this);

        TrayService.InitializeForHwnd(
            hwnd,
            action => DispatcherQueue.TryEnqueue(() => action()),
            showContextMenu: ShowTrayMenu,
            restoreWindow: RestoreFromTray,
            hideWindow: HideToTray,
            onCleanSafeNow: () => NavigateTo(CleanerSectionLabel),
            onOpenSettings: () => NavigateTo(SettingsSectionLabel),
            onOptimizeRam: OptimizeRamFromTrayAsync,
            onPurgeStandby: PurgeStandbyFromTrayAsync,
            onCheckUpdates: () => _ = CheckForUpdatesAsync(silent: false),
            onExit: ExitFromTray);

        if (TrayService.IsVisible)
        {
            AppWindow.Closing += OnAppWindowClosing;
            InitializeMinimizeToTrayHook();

            // Same probe the WPF app ran from UpdateDriveTelemetry: a machine that is
            // already below the threshold hears about it once the guardian is up.
            TrayService.CheckLowDiskSpaceAndNotify(DriveTelemetryService.GetSystemDriveTelemetry());
        }
        else
        {
            ShellDiagnostics.Write("tray: the shell refused the icon; close-to-tray stays off");
        }
    }

    /// <summary>
    /// Close-to-tray, with the two guards that keep the app from becoming unquittable:
    /// the user must have asked for it, and an icon must actually exist to restore from.
    /// WPF additionally warned on close while a clean was in flight
    /// (MainWindow.xaml.cs:1142-1149) - the busy guard below is that port.
    /// </summary>
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        // WPF's MessageBox prompt on a busy close. Cancellation is the shell's only
        /// synchronous option, so: cancel, ask, and exit for real only on consent.
        // The dialog's default button is Close, so an unattended Enter refuses - the
        // destructive-confirmation contract stays fail-closed.
        if (IsAnyWorkspaceBusy())
        {
            args.Cancel = true;
            _ = ConfirmBusyExitThenExitAsync();
            return;
        }

        if (!SettingsService.Current.MemoryCloseToTray || !SettingsService.Current.MemoryShowInTray)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();

        if (!TrayService.IsVisible)
        {
            args.Cancel = false;
        }
    }

    /// <summary>True while a scan or a clean holds the cleaner's one-op-at-a-time lock.</summary>
    private bool IsAnyWorkspaceBusy() => CleanerWorkspacePanel.ViewModel.IsBusy;

    /// <summary>
    /// The WinUI port of WPF's "A cleanup operation is currently in progress. Do you
    /// really want to exit?" prompt. Returns without exiting when the user refuses.
    /// </summary>
    private async Task ConfirmBusyExitThenExitAsync()
    {
        bool exitConfirmed = await ConfirmExitWhileBusyAsync();

        if (exitConfirmed)
        {
            ForceExit();
        }
    }

    private async Task<bool> ConfirmExitWhileBusyAsync()
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "Operation in Progress",
                Content = "A cleanup operation is currently in progress. Do you really want to exit?",
                PrimaryButtonText = "Exit",
                CloseButtonText = "Stay",
                DefaultButton = ContentDialogButton.Close,
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            ShellDiagnostics.Write($"busy-exit dialog failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private void HideToTray() => AppWindow.Hide();

    /// <summary>
    /// The title-bar language pill, WPF's LanguageComboBox_SelectionChanged
    /// (MainWindow.Ui.cs:115): assign the code, the shell's LanguageChanged walk
    /// does the re-labelling and the RTL mirror. Assigning an unknown tag is a
    /// no-op rather than an English reset, matching LocalizationService's guards.
    /// </summary>
    private void OnTitleLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string langCode)
        {
            return;
        }

        if (string.Equals(langCode, LocalizationService.CurrentLanguage, StringComparison.Ordinal))
        {
            return;
        }

        SoundService.PlayClickSound();
        LocalizationService.CurrentLanguage = langCode;
    }

    /// <summary>
    /// Keeps the pill's selection honest with whichever surface changed the
    /// language (this combo, or the full picker in Settings), without re-raising
    /// the selection change.
    /// </summary>
    private void SyncTitleLanguageCombo()
    {
        int index = LocalizationService.CurrentLanguage switch
        {
            "ar" => 1,
            "es" => 2,
            "fr" => 3,
            "de" => 4,
            _ => 0,
        };

        if (TitleLanguageCombo.SelectedIndex != index)
        {
            TitleLanguageCombo.SelectedIndex = index;
        }
    }

    /// <summary>
    /// The title-bar privilege capsule's elevate action, WPF's AdminElevationButton_Click
    /// (MainWindow.xaml.cs:673): already elevated = nothing to do; otherwise confirm
    /// (the same copy WPF shows) and relaunch elevated through the shared
    /// ElevationService, which starts the elevated process and exits this one.
    /// </summary>
    private async void OnPrivilegeCapsuleClick(object sender, RoutedEventArgs e)
    {
        SoundService.PlayClickSound();

        if (ElevationService.IsAdministrator) return;

        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "Elevate to Administrator",
                Content = "Relaunch Deltempo with Administrator privileges?\n\n" +
                          "This grants access to clean Windows Update cache, System Temp, Prefetch, and Driver packages.",
                PrimaryButtonText = "Relaunch",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ElevationService.RestartAsAdmin();
            }
        }
        catch (Exception ex)
        {
            ShellDiagnostics.Write($"elevation dialog failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void RestoreFromTray()
    {
        AppWindow.Show();
        Activate();
        SetForegroundWindow(WindowNative.GetWindowHandle(this));
    }

    /// <summary>Real exit path: clears the close interception, drops the icon, closes.
    /// Honours WPF's busy prompt: an in-flight clean must be confirmed before exit.</summary>
    private void ExitFromTray()
    {
        if (IsAnyWorkspaceBusy())
        {
            _ = ConfirmBusyExitThenExitAsync();
            return;
        }

        _isExiting = true;
        TrayService.Dispose();
        Close();
    }

    /// <summary>Unconditional exit used after a busy-exit prompt was consented to.</summary>
    private void ForceExit()
    {
        _isExiting = true;
        TrayService.Dispose();
        Close();
    }

    private void ShowTrayMenu()
    {
        var flyout = new MenuFlyout();

        var mem = MemoryOptimizerService.GetMemoryInfo();
        var drive = DriveTelemetryService.GetSystemDriveTelemetry();

        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = "Deltempo Guardian",
            IsEnabled = false,
        });
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = $"RAM {mem.UsedPercent:0.0}%  ·  {drive.DriveLetter} {drive.FormattedFree} free",
            IsEnabled = false,
        });
        flyout.Items.Add(new MenuFlyoutSeparator());

        AddTrayItem(flyout, "\uE74D", "Clean Safe Files…", () =>
        {
            // WPF's CleanSafeFromTrayAsync (MainWindow.Settings.cs:126): select the
            // safe categories and run the clean. SmartCleanAsync carries the same
            // confirmation every in-app clean uses; a tray-initiated clean is not a
            // licence to skip the confirm gate.
            NavigateTo(CleanerSectionLabel);
            _ = CleanerWorkspacePanel.ViewModel.SmartCleanAsync();
        });
        AddTrayItem(flyout, "\uE713", "Open Settings", () => NavigateTo(SettingsSectionLabel));
        AddTrayItem(flyout, "\uE945", "Optimize RAM", () => _ = OptimizeRamFromTrayAsync());
        AddTrayItem(flyout, "\uE9D9", "Purge Standby Memory", () => _ = PurgeStandbyFromTrayAsync());
        AddTrayItem(flyout, "\uE895", "Check for Updates", () =>
            _ = CheckForUpdatesAsync(silent: false));
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddTrayItem(flyout, "\uE7E8", "Exit Deltempo", ExitFromTray);

        // WPF opened the context menu at the cursor. FlyoutShowOptions wants client
        // coordinates in DIPs, so the screen point is converted with the window handle
        // and divided by the rasterization scale.
        Point position = new(24, 24);
        double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        if (GetCursorPos(out POINT cursor) && ScreenToClient(WindowNative.GetWindowHandle(this), ref cursor))
        {
            position = new Point(cursor.X / scale, cursor.Y / scale);
        }

        // SetForegroundWindow before the flyout, same rule the WPF tray path obeys
        // (TrayService.ShowLuxuryContextMenu: foreground, then menu.IsOpen). Only for
        // a VISIBLE window though: activating the hidden-window state makes the
        // flyout never display at all (verified live 2026-10-30 - the bridge popup was
        // created yet never shown once the hidden main window was foregrounded), and
        // the tray menu's common case is precisely a hidden window. For the hidden
        // case the topmost promotion below carries the z-order instead.
        IntPtr mainHwnd = WindowNative.GetWindowHandle(this);
        if (AppWindow.IsVisible)
        {
            SetForegroundWindow(mainHwnd);
        }
        flyout.ShowAt(RootGrid, new FlyoutShowOptions { Position = position });

        // Second half of the same parity fix. WPF's Popup windows carry WS_EX_TOPMOST
        // from the framework, which is why the WPF tray menu never fought the taskbar;
        // a WinUI flyout popup (WinUIDesktopWin32WindowClass) is created WITHOUT it,
        // so the always-topmost Shell_TrayWnd can occlude the part of the menu that
        // overlaps the notification strip. Promote the fresh popup to topmost right
        // after ShowAt created it - verified live 2026-10-30: the popup hwnd was
        // foreground yet below Shell_TrayWnd in the z-order until this call.
        PromoteTrayFlyoutPopup();
    }

    // The tray flyout promotion needs classic user32 plumbing; constants are
    // suffixed to avoid colliding with script-style names. EnumWindows walks the
    // z-order top-down, GetWindowThreadProcessId returns the thread id, and
    // SetWindowPos takes the insert-after window as an IntPtr.
    private const long HWND_TOPMOST_FLAG = -1;
    private const uint SWP_NOSIZE_FLAG = 0x0001;
    private const uint SWP_NOMOVE_FLAG = 0x0002;
    private const uint SWP_NOACTIVATE_FLAG = 0x0010;

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int max);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [UnmanagedFunctionPointerAttribute(System.Runtime.InteropServices.CallingConvention.StdCall)]
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Promotes the tray flyout's freshly-created popup window to HWND_TOPMOST.
    ///
    /// EnumWindows walks the z-order from the top: the popup was just foregrounded,
    /// so the first visible top-level window of this process that is not the main
    /// window is the flyout. ShowAt returns before WinUI's composition creates the
    /// popup's hwnd (verified live 2026-10-30: the log said "no flyout popup found"
    /// while the probe saw the popup milliseconds later), so the search retries
    /// across dispatcher ticks. Logged either way; a failed promotion degrades to
    /// the pre-fix z-order instead of crashing the shell.
    /// </summary>
    private const int TrayPromotionAttempts = 8;

    private void PromoteTrayFlyoutPopup()
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        TryPromoteTrayFlyoutPopup(queue, TrayPromotionAttempts);
    }

    private void TryPromoteTrayFlyoutPopup(Microsoft.UI.Dispatching.DispatcherQueue queue, int attemptsLeft)
    {
        try
        {
            IntPtr main = WindowNative.GetWindowHandle(this);
            uint thisPid = GetCurrentProcessId();
            IntPtr popup = IntPtr.Zero;

            EnumWindows((h, l) =>
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == thisPid && h != main)
                {
                    var name = new System.Text.StringBuilder(64);
                    GetClassName(h, name, 64);
                    string cls = name.ToString();
                    // Two things are deliberately NOT assumed:
                    // - Visibility: the flyout's host window reports IsWindowVisible
                    //   false while WinUI renders its content by composition (verified
                    //   live 2026-10-30 with class Microsoft.UI.Content.PopupWindowSiteBridge).
                    // - Exact class: the host is "WinUIDesktopWin32WindowClass" on some
                    //   WindowsAppSDK builds and "Microsoft.UI.Content.PopupWindowSiteBridge"
                    //   on others, both verified live. Accept this process's top-level
                    //   windows that are either rendered or a popup host, minus the main
                    //   window and input-method windows.
                    bool isCandidate = IsWindowVisible(h) || cls.Contains("PopupWindow");
                    bool isIme = cls == "MSCTFIME UI" || cls == "IME";
                    if (isCandidate && !isIme)
                    {
                        popup = h;
                        ShellDiagnostics.Write($"[Tray] flyout popup found: 0x{h.ToInt64():X} class {cls} visible {IsWindowVisible(h)}");
                        return false; // the top-most match found; stop the walk
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (popup != IntPtr.Zero)
            {
                SetWindowPos(
                    popup,
                    new IntPtr(HWND_TOPMOST_FLAG),
                    0, 0, 0, 0,
                    SWP_NOSIZE_FLAG | SWP_NOMOVE_FLAG | SWP_NOACTIVATE_FLAG);
                ShellDiagnostics.Write($"[Tray] flyout popup 0x{popup.ToInt64():X} promoted TOPMOST");
                return;
            }

            if (attemptsLeft > 1)
            {
                // The popup hwnd does not exist yet; look again next dispatcher tick.
                queue.TryEnqueue(() => TryPromoteTrayFlyoutPopup(queue, attemptsLeft - 1));
                return;
            }

            ShellDiagnostics.Write("[Tray] no flyout popup found to promote; the menu may sit under the taskbar");
        }
        catch (Exception ex)
        {
            // A failed promotion degrades to the pre-fix behaviour (menu under the
            // strip) rather than crashing the shell. Logged so it is never silent.
            ShellDiagnostics.Write($"[Tray] flyout promotion failed: {ex.Message}");
        }
    }

    private static void AddTrayItem(MenuFlyout flyout, string glyph, string text, Action action)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = new FontIcon { Glyph = glyph, FontFamily = (FontFamily)Application.Current.Resources["IconFont"] },
        };
        item.Click += (_, _) => action();
        flyout.Items.Add(item);
    }

    private async Task<MemoryOptimizationResult> OptimizeRamFromTrayAsync()
    {
        var result = await MemoryOptimizerService.OptimizeRamAsync();
        MemoryWorkspacePanel.ViewModel.RefreshTelemetry();
        ShellDiagnostics.Write(
            $"[Tray] RAM optimize: {result.SucceededTargetCount}/{result.RequestedTargetCount} targets, " +
            $"{result.FormattedMeasuredDelta}");
        return result;
    }

    private async Task<MemoryOptimizationResult> PurgeStandbyFromTrayAsync()
    {
        var result = await MemoryOptimizerService.OptimizeRamAsync(
            [MemoryTargetType.StandbyList, MemoryTargetType.StandbyListLowPriority]);
        MemoryWorkspacePanel.ViewModel.RefreshTelemetry();
        ShellDiagnostics.Write(
            $"[Tray] Standby purge: {result.SucceededTargetCount}/{result.RequestedTargetCount} zones");
        return result;
    }

    private void NavigateTo(string label)
    {
        foreach (var list in new[] { NavList, NavFooterList })
        {
            foreach (var entry in list.Items)
            {
                if (entry is ListViewItem { Tag: string tag } && tag == label)
                {
                    list.SelectedItem = entry;
                    return;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // --- App icon (WPF parity: Window.Icon + taskbar icon) --------------------
    // WM_SETICON drives the taskbar and Alt-Tab (big) and, if the shell ever draws
    // the system caption again, the small icon. The custom title bar hides the
    // system caption, so the visible top-bar icon is the chip in TitleBarHost.
    private const int WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x00000010;
    private const int SM_CXSMICON_WIN = 49;
    private const int SM_CYSMICON_WIN = 50;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInstance, string name, int type, int cx, int cy, int load);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, int count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private IntPtr _iconSmall = IntPtr.Zero;
    private IntPtr _iconBig = IntPtr.Zero;

    private static string IconPath => Path.Combine(AppContext.BaseDirectory, "app.ico");

    private void ApplyWindowIcon()
    {
        try
        {
            if (File.Exists(IconPath))
            {
                int cx = GetSystemMetrics(SM_CXSMICON_WIN);
                if (cx <= 0) cx = 16;
                int cy = GetSystemMetrics(SM_CYSMICON_WIN);
                if (cy <= 0) cy = 16;

                _iconSmall = LoadImage(IntPtr.Zero, IconPath, IMAGE_ICON, cx, cy, LR_LOADFROMFILE);
                _iconBig = LoadImage(IntPtr.Zero, IconPath, IMAGE_ICON, cx * 2, cy * 2, LR_LOADFROMFILE);
            }
            else
            {
                // No .ico beside the exe (its copy step was skipped): the icon
                // ApplicationIcon embedded into the PE still carries the identity,
                // which is exactly what WPF's pack resource resolved to.
                ExtractIconEx(Environment.ProcessPath ?? "", 0, out _iconBig, out _iconSmall, 1);
            }

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (_iconSmall != IntPtr.Zero)
                SendMessage(hwnd, WM_SETICON, ICON_SMALL, _iconSmall);
            if (_iconBig != IntPtr.Zero)
                SendMessage(hwnd, WM_SETICON, ICON_BIG, _iconBig);

            // The chip inside the 48px custom title bar. BitmapImage decodes the ICO
            // itself; its largest frame is what the system scaler downsamples here,
            // so no separate scale branch is needed for the 34px chip.
            if (File.Exists(IconPath))
            {
                TitleBarLogo.Source = new BitmapImage(new Uri(IconPath));
            }
        }
        catch
        {
            // Icon association is cosmetic; never let it break shell startup.
        }
    }

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
            _footerHasError = true;
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
    /// WPF's SoundToggleBtn_Click (MainWindow.Ui.cs:85): flip the sound preference
    /// and keep everything else in step with it. The header toggle is a real
    /// preference, not a session mute: it has to survive a restart and stay in step
    /// with the toggle in Settings, so it writes through SettingsService.
    /// </summary>
    private void OnSoundToggleClick(object sender, RoutedEventArgs e)
    {
        bool enabled = !SoundService.IsSoundEnabled;
        SoundService.IsSoundEnabled = enabled;
        SettingsService.Update(s => s.SoundEnabled = enabled);
        RefreshSoundToggleGlyph(enabled);

        if (SettingsWorkspacePanel.ViewModel.SoundEnabled != enabled)
        {
            SettingsWorkspacePanel.ViewModel.SoundEnabled = enabled;
        }

        if (enabled)
        {
            SoundService.PlayClickSound();
            OnFootprintLog("Haptic sound effects enabled.", LogLevel.Info);
        }
        else
        {
            OnFootprintLog("Haptic sound effects muted.", LogLevel.Info);
        }
    }

    /// <summary>WPF's SoundToggleIcon paint: E767 speaker when audible, E74F when muted.</summary>
    private void RefreshSoundToggleGlyph(bool enabled)
    {
        SoundToggleIcon.Glyph = enabled ? "\uE767" : "\uE74F";
        SoundToggleIcon.Foreground = enabled ? ResolveBrush("ElectricCyanBrush") : ResolveBrush("TextMutedBrush");
    }

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

        foreach (var (label, _, glyph, help, danger) in ToolSections)
        {
            AddNavItem(NavList, label, glyph, help, danger);
        }

        foreach (var (label, _, glyph, help, danger) in FooterSections)
        {
            AddNavItem(NavFooterList, label, glyph, help, danger);
        }

        // The search box offers exactly the destinations the rail offers, so it can
        // never send the user somewhere the rail does not have. It lists visible
        // names; OnSectionSearchChosen maps the chosen name back to its Label.
        SectionSearchBox.ItemsSource = ToolSections
            .Concat(FooterSections)
            .Select(section => SectionDisplayName(section.Label))
            .ToList();

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

        PlayRailEntrance();


    }

    /// <summary>
    /// Adds one rail item: the glyph, the accent bar that marks the active section,
    /// and the name that reaches a screen reader and a pointer.
    /// </summary>
    private void AddNavItem(ListView list, string label, string glyph, string help, bool danger)
    {
        (UIElement content, FontIcon icon) = BuildNavContent(glyph);

        var item = new ListViewItem
        {
            Tag = label,
            Content = content,
        };

        _navEntries[label] = new NavEntry(icon, danger);
        _railItems[label] = item;
        _railItemOrder.Add(item);

        // Without this the item announces as an empty list item. The WPF nav
        // set AutomationProperties.Name on each workspace, and dropping it
        // made every nav destination unreachable by name - for screen
        // readers and for UI automation alike.
        //
        // The rail shows glyphs only, so the name also has to reach a sighted
        // pointer: that is the tooltip, carrying the label on hover. HelpText
        // keeps the WPF button's descriptive tooltip copy available to assistive
        // tech instead of putting a two-line balloon over every row.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, SectionDisplayName(label));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(item, help);
        ToolTipService.SetToolTip(item, SectionDisplayName(label));
        ToolTipService.SetPlacement(item, Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Right);

        WireNavPointerFeedback(item, label, danger);

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
        if (args.SelectedItem is not string chosen)
        {
            return;
        }

        foreach (var section in ToolSections.Concat(FooterSections))
        {
            if (SectionDisplayName(section.Label) == chosen)
            {
                SelectSection(section.Label);
                return;
            }
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
        PlaceholderTitle.Text = SectionDisplayName(label);
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
        PlaceholderPanel.Visibility = hasPanel ? Visibility.Collapsed : Visibility.Visible;        // Every panel paints the window brush, but the Core link card sits in the same
        // content column and would otherwise show through the promoted panels, whose
        // own surface is deliberately transparent.
        CoreLinkCard.Visibility = hasPanel ? Visibility.Collapsed : Visibility.Visible;

        // WPF's drawer lived inside the cleaner workspace, so leaving that screen took it
        // with it. The toggle lives in the cleaner dock here, so a log left open over an
        // unrelated screen would be a panel with no control able to close it.
        if (_isActivityLogOpen && label != CleanerSectionLabel)
        {
            CloseActivityLog();
        }


        active.Open?.Invoke();

        // Content that replaced other content gets one short entrance so a section
        // change reads as a change instead of a teleport. Same curve, same length,
        // every section - the motion belongs to the shell, not to each panel.
        ShellMotion.Enter(ContentColumn);
    }

    /// <summary>
    /// Section copy for the shell-owned page header, as LocalizationService keys. The
    /// eyebrow names the area, the title names the screen, and the description says what
    /// the screen will and will not do to the machine. Every destructive screen says so
    /// in its own words, which is why this is a table of prose rather than a format
    /// string over the label.
    /// </summary>
    private static readonly Dictionary<string, (string EyebrowKey, string DescriptionKey)> SectionCopy = new()
    {
        [CleanerSectionLabel] = ("PageEyebrowCleanup", "PageDescCleaner"),
        [LargeFilesSectionLabel] = ("PageEyebrowDisk", "PageDescLargeFiles"),
        [StartupSectionLabel] = ("PageEyebrowBoot", "PageDescStartup"),
        [ProcessManagerSectionLabel] = ("PageEyebrowProcesses", "PageDescProcesses"),
        [MemorySectionLabel] = ("PageEyebrowPerformance", "PageDescMemory"),
        [SystemRepairSectionLabel] = ("PageEyebrowIntegrity", "PageDescRepair"),
        [AppUninstallerSectionLabel] = ("PageEyebrowSoftware", "PageDescApps"),
        [ForceDeleteSectionLabel] = ("PageEyebrowLastResort", "PageDescForceDelete"),
        [SettingsSectionLabel] = ("PageEyebrowPreferences", "PageDescSettings"),
        [AboutSectionLabel] = ("PageEyebrowAbout", "PageDescAbout"),
    };

    /// <summary>
    /// Paints the page header for one section. The header belongs to the shell rather
    /// than to the panels so the eight workspaces cannot drift apart: same eyebrow, same
    /// title size, same description treatment everywhere.
    /// </summary>
    private void SetPageHeader(string label)
    {
        (string eyebrowKey, string descriptionKey) = SectionCopy.TryGetValue(label, out var copy)
            ? copy
            : (string.Empty, string.Empty);

        PageEyebrow.Text = eyebrowKey.Length > 0 ? LocalizationService.Get(eyebrowKey) : "WORKSPACE";
        PageTitle.Text = SectionDisplayName(label);
        PageDescription.Text = descriptionKey.Length > 0 ? LocalizationService.Get(descriptionKey) : string.Empty;

        // The leading tile carries the section's own glyph, so the header and the
        // rail always agree about which section is open.
        PageHeaderIcon.Glyph = SectionGlyphs.TryGetValue(label, out string? glyph)
            ? glyph
            : "\uE8F1";
    }

    /// <summary>The section's visible name in the active language; Label stays the key.</summary>
    private static string SectionDisplayName(string label)
        => SectionLocKeys.TryGetValue(label, out string? locKey)
            ? LocalizationService.Get(locKey)
            : label;

    /// <summary>
    /// Re-paints every piece of shell chrome after a language switch: the mirrored
    /// layout for Arabic, the rail's tooltips and accessible names, the section search
    /// list, the page header, and every attached key in the tree.
    ///
    /// WPF mirrored its whole window for Arabic (FlowDirection on the MainWindow). The
    /// rail therefore moves to the right edge here too, because RootGrid carries the
    /// flow direction. The title bar is pinned back to left-to-right: the min/max/close
    /// buttons are drawn by the OS at the window's top-right, and mirroring that row
    /// would move our reserved 144px column to the left and let the search box run
    /// underneath the caption buttons.
    /// </summary>
    private void ApplyLanguage(bool logToActivity)
    {
        bool isRtl = LocalizationService.IsRightToLeft;

        RootGrid.FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TitleBarHost.FlowDirection = FlowDirection.LeftToRight;

        // The title-bar pill must show the language in force, whoever set it -
        // this pill, or the full picker in Settings (WPF's pill only ever saw
        // its own selection, so it had no drift to guard against).
        SyncTitleLanguageCombo();

        foreach ((string label, ListViewItem item) in _railItems)
        {
            string name = SectionDisplayName(label);
            ToolTipService.SetToolTip(item, name);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, name);
        }

        // The search box offers the same destinations in the new language; the chosen
        // text is mapped back to a Label before the section is selected.
        SectionSearchBox.ItemsSource = ToolSections
            .Concat(FooterSections)
            .Select(section => SectionDisplayName(section.Label))
            .ToList();

        if (_activeSectionLabel.Length > 0)
        {
            SetPageHeader(_activeSectionLabel);
            PlaceholderTitle.Text = SectionDisplayName(_activeSectionLabel);
        }

        PrivilegeLabel.Text = ElevationService.IsAdministrator
            ? LocalizationService.Get("AdminLabel")
            : "Standard user";

        if (!_footerHasError)
        {
            FooterText.Text = LocalizationService.Get("Ready");
        }

        // Everything declared with loc:Loc.Key / PlaceholderKey / ToolTipKey / NameKey.
        // Declarative copy is re-applied here; dynamic text stays owned by its view model.
        Loc.ApplyTree(RootGrid);

        // The cleaner dock owns the Activity Log / Hide Log label, which is a state pair
        // rather than a static label, so it cannot carry an attached key.
        CleanerWorkspacePanel.RefreshLocalization(_isActivityLogOpen);

        if (logToActivity)
        {
            ShellDiagnostics.Write($"language switched to {LocalizationService.CurrentLanguage.ToUpperInvariant()}");
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLanguage(logToActivity: true);

    /// <summary>
    /// Proves the attached-key layer is actually wired: counts the elements the tree walk
    /// reaches, lists any key that resolves to itself (a missing dictionary entry would
    /// otherwise surface as a bare English word in the UI), and round-trips the Arabic
    /// mirror back to the language the run started in.
    ///
    /// Read-only apart from LocalizationService.CurrentLanguage, which is restored before
    /// returning. No scan, no delete, no network.
    /// </summary>
    private void LogSelfTestLocalization()
    {
        IReadOnlyList<string> keys = Loc.CollectKeys(RootGrid);
        int distinct = keys.Distinct(StringComparer.Ordinal).Count();

        List<string> unknown = keys
            .Where(key => !LocalizationService.ContainsKey(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        ShellDiagnostics.Write(
            $"localization keys: {keys.Count} bound elements, {distinct} distinct, " +
            $"{unknown.Count} unknown{(unknown.Count == 0 ? string.Empty : ": " + string.Join(", ", unknown))}");

        string previous = LocalizationService.CurrentLanguage;
        LocalizationService.CurrentLanguage = "ar";
        string mirrored = RootGrid.FlowDirection.ToString();
        LocalizationService.CurrentLanguage = previous;
        string restored = RootGrid.FlowDirection.ToString();

        ShellDiagnostics.Write(
            $"localization mirror: arabic={mirrored}, restored={restored}, language={LocalizationService.CurrentLanguage}");
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
        _workspacePanels[SettingsSectionLabel] = (SettingsWorkspacePanel, SettingsWorkspacePanel.Refresh);
        _workspacePanels[AboutSectionLabel] = (AboutWorkspacePanel, null);
    }

    /// <summary>
    /// Builds one rail item's visual: the section glyph, and nothing else. The name is
    /// deliberately not in the content - the strip is icon-only and the name arrives as
    /// the item's tooltip - and neither is the selection bar: one pill serves the whole
    /// rail, so switching sections slides a single bar instead of blinking two.
    /// </summary>
    private (UIElement Content, FontIcon Icon) BuildNavContent(string glyph)
    {
        // The glyph owns its brush rather than reading a theme brush. The hover and
        // selection fades animate that instance's Color, and animating a shared brush
        // would recolour every row that reads it at the same time.
        var brush = new SolidColorBrush(ResolveColor("NavIconMutedBrush"));

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 19,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return (icon, icon);
    }

    /// <summary>
    /// Reads a colour out of the palette the window is actually rendering with.
    ///
    /// Two traps meet here. First, the token dictionaries are *merged* into the app's
    /// resources, so their ThemeDictionaries are not on Application.Current.Resources:
    /// asking it directly resolves the brush through the application's requested theme
    /// instead, which is the theme the window was not switched to. Second, ApplyTheme
    /// switches the palette on the window root, so even a correct global lookup answers
    /// for the wrong theme. Dark mode hides both - light mode painted dark grey glyphs
    /// onto a pale pane, at roughly half the contrast the tokens intend.
    ///
    /// So the dictionary is chosen from the shell's own theme flag and looked up in the
    /// merged dictionaries by hand.
    /// </summary>
    private Windows.UI.Color ResolveColor(string key) =>
        ResolveBrush(key) is SolidColorBrush brush ? brush.Color : Colors.Gray;

    /// <summary>
    /// The brush behind <see cref="ResolveColor"/>, for the places that need the brush
    /// itself (the selection pill paints with it) rather than just its colour.
    /// </summary>
    private Brush? ResolveBrush(string key)
    {
        string themeKey = _isDarkTheme ? "Dark" : "Light";

        return FindThemeBrush(Application.Current.Resources, themeKey, key)
            ?? FindThemeBrush(Application.Current.Resources, "Default", key)
            ?? (Application.Current.Resources.TryGetValue(key, out object? value) ? value as Brush : null);
    }

    /// <summary>
    /// Searches one resource dictionary, and everything merged into it, for a themed
    /// brush. Depth is capped because a resource graph may legitimately cycle.
    /// </summary>
    private static Brush? FindThemeBrush(
        ResourceDictionary dictionary,
        string themeKey,
        string brushKey,
        int depth = 0)
    {
        if (depth > 4)
        {
            return null;
        }

        if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out object? theme) &&
            theme is ResourceDictionary themed &&
            themed.TryGetValue(brushKey, out object? entry) &&
            entry is Brush brush)
        {
            return brush;
        }

        foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
        {
            if (FindThemeBrush(merged, themeKey, brushKey, depth + 1) is Brush nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// Repaints the rail for a new active section and moves the selection pill to it.
    /// Glyphs fade between colours rather than switching, so a fast click sequence reads
    /// as one highlight travelling down the strip instead of a row of small flashes.
    ///
    /// Dangerous sections (Force Delete) keep their danger tint in both states - WPF
    /// styled that pill with its danger brushes rather than the accent - and the pill
    /// takes the danger colour with it.
    /// </summary>
    private void UpdateNavSelection(string activeLabel)
    {
        foreach ((string label, NavEntry entry) in _navEntries)
        {
            bool isActive = label == activeLabel;
            AnimateIconColor(entry.Icon, ResolveColor(IconKey(entry.Danger, isActive, hovered: false)), 160);
        }

        MoveSelectionPill(activeLabel);
    }

    /// <summary>
    /// Puts the selection pill beside the active row, sliding it there when it is already
    /// on screen. One pill for the whole rail means a switch is a movement, and the bar
    /// can travel across the rule into the preference cluster without a second element.
    /// </summary>
    private void MoveSelectionPill(string activeLabel)
    {
        if (!_railItems.TryGetValue(activeLabel, out ListViewItem? item) ||
            !_navEntries.TryGetValue(activeLabel, out NavEntry? entry))
        {
            return;
        }

        if (item is null || entry is null)
        {
            return;
        }

        string indicatorKey = entry.Danger ? "DestructiveRedBrush" : "ElectricCyanBrush";
        if (Application.Current.Resources.TryGetValue(indicatorKey, out object? value) && value is Brush brush)
        {
            NavSelectionPill.Background = brush;
        }

        // On the first selection the row may not be measured yet. Wait for layout rather
        // than leaving the pill parked at the top of the rail.
        if (item.ActualHeight <= 0)
        {
            void Retry(object? sender, object args)
            {
                RailHost.LayoutUpdated -= Retry;
                MoveSelectionPill(activeLabel);
            }

            RailHost.LayoutUpdated += Retry;
            return;
        }

        double rowTop = item.TransformToVisual(RailHost).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
        float centre = (float)(rowTop + ((item.ActualHeight - NavSelectionPill.Height) / 2d));

        if (NavSelectionPill.Opacity <= 0)
        {
            // Fade up to the resting value: Enter animates toward the element's current
            // opacity, and the pill starts life (and every theme rebuild) at zero.
            NavSelectionPill.Opacity = 1;
            ShellMotion.Enter(NavSelectionPill, riseDip: 0, milliseconds: 180);
        }

        ShellMotion.SlideTo(NavSelectionPill, centre, 300);
    }

    /// <summary>
    /// Fades a rail glyph to a colour. The animation runs through the element's
    /// Foreground path because a brush is not a visual and cannot be targeted directly;
    /// the brush it writes to is the row's own instance, never a shared theme brush.
    /// </summary>
    private static void AnimateIconColor(FontIcon icon, Windows.UI.Color to, double milliseconds)
    {
        if (icon.Foreground is not SolidColorBrush brush || brush.Color == to)
        {
            return;
        }

        if (!ShellMotion.MotionEnabled)
        {
            brush.Color = to;
            return;
        }

        try
        {
            var animation = new ColorAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true,
            };

            Storyboard.SetTarget(animation, icon);
            Storyboard.SetTargetProperty(
                animation,
                "(Microsoft.UI.Xaml.Controls.IconElement.Foreground).(Microsoft.UI.Xaml.Media.SolidColorBrush.Color)");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
        catch (Exception)
        {
            // A correct colour that lands in one step still tells the truth about the
            // state; only the fade is lost, so this is not worth failing a navigation for.
            brush.Color = to;
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
    /// Pointer feedback for a rail row: the glyph brightens, lifts about six percent and
    /// settles back on exit, and dips while pressed. All of it is compositor animation,
    /// so sweeping the pointer down the strip never queues a layout pass. Colours are
    /// read per event rather than captured once, so a theme switch is picked up by the
    /// next hover instead of needing the rail rebuilt.
    /// </summary>
    private void WireNavPointerFeedback(ListViewItem item, string label, bool danger)
    {
        if (!_navEntries.TryGetValue(label, out NavEntry? entry) || entry is null)
        {
            return;
        }

        FontIcon icon = entry.Icon;

        void Paint(bool hovered)
        {
            bool isActive = _activeSectionLabel == label;
            AnimateIconColor(icon, ResolveColor(IconKey(danger, isActive, hovered)), 150);
        }

        item.PointerEntered += (_, _) =>
        {
            Paint(hovered: true);
            ShellMotion.ScaleTo(icon, 1.08f, 150);
        };

        item.PointerExited += (_, _) =>
        {
            Paint(hovered: false);
            ShellMotion.ScaleTo(icon, 1f, 180);
        };

        item.PointerPressed += (_, _) => ShellMotion.ScaleTo(icon, 0.92f, 90);

        item.PointerReleased += (_, _) => ShellMotion.ScaleTo(icon, 1.08f, 140);
    }

    /// <summary>
    /// The rail arrives once, top to bottom, twelve milliseconds apart. A pane that
    /// appears in a single frame reads as a screenshot; the stagger is what says the
    /// strip is a list of destinations. Builds once, on the first window, and never on
    /// navigation - a strip that re-staggers on every click would be noise.
    /// </summary>
    private void PlayRailEntrance()
    {
        if (!ShellMotion.MotionEnabled || _railItemOrder.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _railItemOrder.Count; i++)
        {
            ListViewItem item = _railItemOrder[i];
            int delay = 12 * i;

            if (delay == 0)
            {
                ShellMotion.Enter(item, riseDip: 8f, milliseconds: 220);
                continue;
            }

            item.Opacity = 0;
            var queue = item.DispatcherQueue;
            _ = Task.Delay(delay).ContinueWith(
                _ => queue.TryEnqueue(() =>
                {
                    item.Opacity = 1;
                    ShellMotion.Enter(item, riseDip: 8f, milliseconds: 220);
                }),
                TaskScheduler.Default);
        }
    }

    /// <summary>One navigation entry's parts that change with the selection.</summary>
    private sealed record NavEntry(FontIcon Icon, bool Danger);

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
