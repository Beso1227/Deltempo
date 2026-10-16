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

        // Self-test drives the ported overlays directly: selecting a nav item does not
        // fire SelectionChanged for the initial selection, so the overlays would
        // otherwise stay closed and the self-test would prove nothing about them.
        if (ShellDiagnostics.IsSelfTest)
        {
            OpenAppDataFootprint();
            LoadSelfTestLockedFiles();
        }

        ShellDiagnostics.MaybeSelfTest(this);
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
    /// Opens the ported AppData footprint report over the content area. This is the
    /// Stage 2 proof screen: real Core data, no delete affordance.
    /// </summary>
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
                SectionLabel.Text = label;
                PlaceholderTitle.Text = label;
                PlaceholderBody.Text =
                    $"Stage 1 shell placeholder for '{label}'. No cleanup logic is wired up; nothing on this screen touches disk.";

                // The footprint report is the one Stage 2 screen that exists, so it
                // hangs off the Cleaner workspace until it has a real home.
                if (label == Sections[0].Label)
                {
                    OpenAppDataFootprint();
                }
            }
        };
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