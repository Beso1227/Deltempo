using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The shell/tray parity batch: the five WPF behaviours the final scan found
/// missing from the preview's shell —
///
/// 1. the header quick mute/unmute toggle (WPF SoundToggleBtn),
/// 2. minimize-to-tray, which the persisted MinimizeToTray setting had never reached,
/// 3. the busy close prompt WPF raised while a clean was in flight,
/// 4. the tray "Clean Safe Files" item actually running the clean,
/// 5. the startup row's web "search online" action.
///
/// Shape-only, like ShellPolishParityTests: each check pairs a source fact the
/// shell build would break if it drifted with the WPF counterpart that must keep
/// existing too. Nothing here launches a browser, plays sound or closes a window.
/// </summary>
public class ParityBatchATests
{
    private static string ReadWinUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.WinUI" }.Concat(parts).ToArray()));

    private static string ReadWpf(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.Wpf" }.Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "deltempo.sln")))
        {
            dir = Directory.GetParent(dir)!.FullName;
        }

        return dir;
    }

    // ------------------------------------------------------------------
    // 1. Header mute toggle.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfHeader_TogglesSoundThroughThePersistedSetting()
    {
        // The counterpart: the header flip is a preference, not a session mute.
        string wpf = ReadWpf("MainWindow.Ui.cs");
        Assert.Contains("SoundService.IsSoundEnabled = enabled", wpf);
        Assert.Contains("SettingsService.Update(s => s.SoundEnabled = enabled)", wpf);
    }

    [Fact]
    public void WinUiStatusBar_OffersTheSameSoundToggle()
    {
        string shell = ReadWinUi("MainWindow.xaml");
        Assert.Contains("x:Name=\"SoundToggleButton\"", shell);
        string code = ReadWinUi("MainWindow.xaml.cs");

        // Same two writes as WPF: the runtime switch and the persisted setting.
        Assert.Contains("SoundService.IsSoundEnabled = enabled", code);
        Assert.Contains("SettingsService.Update(s => s.SoundEnabled = enabled)", code);

        // The icon pair must stay in step with WPF's: the handler writes the same
        // escape literals the WPF code does (E767 speaker / E74F muted).
        Assert.Contains("\\uE767", code);
        Assert.Contains("\\uE74F", code);
    }

    // ------------------------------------------------------------------
    // 2. Minimize-to-tray.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfMinimize_GoesToTheTrayWhenTheSettingSaysSo()
    {
        string wpf = ReadWpf("MainWindow.xaml.cs");
        Assert.Contains("if (SettingsService.Current.MinimizeToTray)", wpf);
        Assert.Contains("TrayService.MinimizeToTray()", wpf);
    }

    [Fact]
    public void WinUiShell_InterceptsMinimizeForTheTray()
    {
        string code = ReadWinUi("MainWindow.xaml.cs");

        // A third subclass on the window HWND beside tray (0xD317) and hotkey (0xD319).
        Assert.Contains("SetWindowSubclass(hwnd, MinimizeTraySubclassProc", code);
        Assert.Contains("0xD31A", code);
        Assert.Contains("SC_MINIMIZE", code);

        // It reads the live setting and it can never orphan the app:
        // a hidden window the tray cannot restore from must not happen.
        Assert.Contains("SettingsService.Current.MinimizeToTray", code);
        Assert.Contains("TrayService.IsVisible", code);
    }

    // ------------------------------------------------------------------
    // 3. Busy close guard.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfClose_PromptsWhileACleanIsInProgress()
    {
        // The counterpart prompt: WPF's MessageBox guard.
        string wpf = ReadWpf("MainWindow.xaml.cs");
        Assert.Contains("A cleanup operation is currently in progress", wpf);
    }

    [Fact]
    public void WinUiClose_RefusesByDefaultWhileBusy()
    {
        string code = ReadWinUi("MainWindow.xaml.cs");

        // The closing event asks first; the dialog's default button is Close,
        // so an unattended Enter refuses and the operation keeps running.
        Assert.Contains("IsAnyWorkspaceBusy()", code);
        Assert.Contains("Operation in Progress", code);
        Assert.Contains("A cleanup operation is currently in progress. Do you really want to exit?", code);
        Assert.Contains("DefaultButton = ContentDialogButton.Close", code);

        // The tray exit path honours the same guard, not just the caption button.
        Assert.Matches(
            @"private void ExitFromTray\(\)[\s\S]{0,200}?IsAnyWorkspaceBusy\(\)",
            code);
    }

    // ------------------------------------------------------------------
    // 4. Tray clean-now.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfTrayClean_RunsTheCleanAfterSelectingSafe()
    {
        // The counterpart: select safe, then execute the clean, in one flow.
        string wpf = ReadWpf("MainWindow.Settings.cs");
        Assert.Contains("SelectSafeOnlyButton_Click(this, new RoutedEventArgs());", wpf);
        Assert.Contains("await ExecuteCleanupAsync();", wpf);
    }

    [Fact]
    public void WinUiTrayClean_RunsTheConfirmedClean()
    {
        string code = ReadWinUi("MainWindow.xaml.cs");

        // Same flow, public in the ViewModel: select safe + clean, with the
        // confirmation every in-app clean carries.
        Assert.Contains("_ = CleanerWorkspacePanel.ViewModel.SmartCleanAsync();", code);
        string vm = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "CleanerWorkspaceViewModel.cs"));
        Assert.Contains("await CleanSelectedAsync();", vm);
    }

    // ------------------------------------------------------------------
    // 5. Startup "search online" row action.
    // ------------------------------------------------------------------

    [Fact]
    public void WpfStartupRow_OpensTheProcessOnGoogle()
    {
        string wpf = ReadWpf("MainWindow.SystemManagers.cs");
        Assert.Contains("https://www.google.com/search?q=", wpf);
        Assert.Contains("process windows startup", wpf);
    }

    [Fact]
    public void WinUiStartupRow_CarriesTheSameSearchOnlineAction()
    {
        string view = ReadWinUi("Views", "StartupWorkspaceView.xaml");
        Assert.Contains("SearchOnlineCommand", view);
        Assert.Contains("Search web for process reputation &amp; safety", view);

        string vm = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "ViewModels", "StartupWorkspaceViewModel.cs"));
        Assert.Contains("https://www.google.com/search?q=", vm);
        Assert.Contains("process windows startup", vm);

        // Null items refuse rather than open a blank search.
        Assert.Contains("if (item is null) return;", vm);
    }
}
