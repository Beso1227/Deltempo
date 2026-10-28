using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The shell-polish batch the WinUI preview still owed, as text-level guards on the
/// real files (the shell's markup is not unit-constructible, the way WinUiShellParityTests
/// reads it):
///
/// - the per-category Inspect pill and the top-files overlay it opens,
/// - the rolling local file log the WPF App attached and the preview did not,
/// - the Escape chain covering every genuine overlay.
///
/// These checks are deliberately shape-only: they cannot assert behaviour inside a
/// rendered tree, so each one pairs a source fact the shell build would break if it
/// drifted with a concrete WPF counterpart that must keep existing too.
/// </summary>
public class ShellPolishParityTests
{
    private static string ReadWinUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.WinUI" }.Concat(parts).ToArray()));

    private static string ReadWpf(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.Wpf" }.Concat(parts).ToArray()));

    [Fact]
    public void WpfInspector_PeeksTopFiles_ByBindingHasTopFiles()
    {
        // The counterpart itself must keep the shape the WinUI guard copies.
        string wpf = ReadWpf("MainWindow.xaml");
        Assert.Contains("InspectTarget_Click", wpf);
        Assert.Contains("HasTopFiles", wpf);
    }

    [Fact]
    public void WinUiCleanerRow_OffersTheSameInspectPill()
    {
        string view = ReadWinUi("Views", "CleanerWorkspaceView.xaml");

        Assert.Contains("OnInspectTargetClick", view);

        // Only a scanned top-files ledger earns the pill, exactly as WPF gated it.
        var inspect = Regex.Match(
            view, @"OnInspectTargetClick[\s\S]{0,600}?Visibility=""\{Binding HasTopFiles");
        Assert.True(inspect.Success,
            "the WinUI Inspect pill must show only when HasTopFiles holds");

        // And the shell hosts the overlay the click opens.
        string shell = ReadWinUi("MainWindow.xaml");
        Assert.Contains("views:TopFilesView x:Name=\"TopFilesOverlay\"", shell);
    }

    [Fact]
    public void WinUiEscapeChain_CoversTheTopFilesOverlay()
    {
        string shellCode = ReadWinUi("MainWindow.xaml.cs");

        // WPF listed InspectorModalOverlay in its Escape chain; the overlay without
        // it could only be left by finding one 42-px close button.
        Assert.Contains("TopFilesOverlay.Visibility == Visibility.Visible", shellCode);

        // The overlay really closes (not merely renders).
        Assert.Contains("CloseTopFilesOverlay", shellCode);
    }

    [Fact]
    public void WinUiApp_AttachesTheRollingLogFile()
    {
        string app = ReadWinUi("App.xaml.cs");

        // The Core wrapper the shared listener actually lives behind.
        Assert.Contains("DiagnosticLogFileService.TryAttach", app);

        // The crash record survives without a console: same contract WPF's crash.log had.
        Assert.Contains("crash.log", app);

        // And the rolled file is distinct from the WPF app's, so both shells' roll
        // chains never race the same file.
        Assert.Contains("deltempo-winui.log", app);
        string wpfApp = ReadWpf("App.xaml.cs");
        Assert.Contains("\"deltempo.log\"", wpfApp);
    }

    [Fact]
    public void WpfStillOwnsTheRollingImplementation_InCore()
    {
        // The listener is the single implementation; the new wrapper must delegate,
        // not reimplement the roll chain.
        string wrapper = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.Core", "Services", "DiagnosticLogFileService.cs"));
        Assert.Contains("RollingFileTraceListener.TryCreate(path)", wrapper);
    }

    [Fact]
    public void WinUiAbout_StatesTheSameFiveGuaranteesAsWpf()
    {
        string wpf = ReadWpf("MainWindow.xaml");
        string winui = ReadWinUi("Views", "AboutWorkspaceView.xaml");

        List<string> guarantees =
        [
            "100% Open Source (MIT License)",
            "Zero telemetry — no tracking, no analytics, no phone home",
            "Safety Shield protects files modified in the last 24 hours",
            "Recycle Bin mode available — deleted files can be restored",
            "Deterministic rule-based safety engine — no heuristics guessing",
        ];

        var missing = guarantees.Where(g => !winui.Contains(g, StringComparison.Ordinal)).ToList();
        Assert.True(
            missing.Count == 0,
            "the WinUI About panel must carry the WPF About modal's guarantees; missing: " +
            string.Join(" | ", missing));

        var wpfMissing = guarantees.Where(g => !wpf.Contains(g, StringComparison.Ordinal)).ToList();
        Assert.True(wpfMissing.Count == 0,
            "this fixture copied WPF's About modal wrongly; fix the fixture, not the port");

        Assert.Contains("SmartScreen", winui);
        Assert.Contains("Beso1227", winui);
    }

    [Fact]
    public void WinUiSecondInstance_AcquiresItsOwnMutexAndDefers()
    {
        string app = ReadWinUi("App.xaml.cs");

        // Second launches surface the running copy instead of opening a twin. The
        // preview's own mutex keeps it compatible with the shipped WPF app.
        Assert.Contains("SingleInstanceManager.TryAcquire", app);
        Assert.Contains("Deltempo_WinUI_Preview_SingleInstance_Mutex_v1", app);
        Assert.Contains("NotifyExistingInstance", app);

        // The Core side the notify path leans on: broadcast + enumeration.
        string core = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.Core", "Services", "SingleInstanceManager.cs"));
        Assert.Contains("PostMessage", core);
        Assert.Contains("SetForegroundWindow", core);
    }

    /// <summary>Ascend until the solution file shows the repo root.</summary>
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            if (File.Exists(Path.Combine(dir, "deltempo.sln"))) return dir;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException("could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
