using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// App-icon parity: WPF shows the app.ico in its custom title bar, on the
/// taskbar and in the tray; the WinUI shell must do the same through the three
/// mechanisms its port has available:
///
/// 1. ApplicationIcon in the csproj (Explorer / Task Manager / Alt-Tab identity),
/// 2. WM_SETICON small+big on the window handle (taskbar + Alt-Tab).
/// 3. the logo chip inside the 48px custom title bar (top bar), and the tray's
///    LoadCrispTrayIcon resolving app.ico from AppContext.BaseDirectory.
///
/// Shape-only, like the other shell parity tests: each check pairs a source
/// fact the WinUI build would break if it drifted with the WPF counterpart
/// that must keep existing too.
/// </summary>
public class AppIconParityTests
{
    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "deltempo.sln")))
        {
            dir = Directory.GetParent(dir)!.FullName;
        }

        return dir;
    }

    private static XDocument ReadProjectXDoc(params string[] parts) =>
        XDocument.Load(Path.Combine(new[] { RepoRoot(), "src" }.Concat(parts).ToArray()));

    private static string ReadWinUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.WinUI" }.Concat(parts).ToArray()));

    private static string ReadWpf(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "Deltempo.App.Wpf" }.Concat(parts).ToArray()));

    // ------------------------------------------------------------------
    // The shared asset.
    // ------------------------------------------------------------------

    [Fact]
    public void AppIcon_ExistsAndBothHeadsReferenceIt()
    {
        string iconPath = Path.Combine(RepoRoot(), "src", "Deltempo.App.Wpf", "app.ico");
        Assert.True(File.Exists(iconPath), "app.ico must exist at src/Deltempo.App.Wpf/app.ico");
        Assert.True(new FileInfo(iconPath).Length > 22, "app.ico must contain an icon directory");

        // WPF: ApplicationIcon + pack Window.Icon.
        string wpfCsproj = ReadWpf("WinTempCleaner.csproj");
        Assert.Contains("<ApplicationIcon>app.ico</ApplicationIcon>", wpfCsproj);
        Assert.Contains("Icon=\"pack://application:,,,/app.ico\"", ReadWpf("MainWindow.xaml"));

        // WinUI: same file, linked across projects, not a forked copy.
        XDocument winUi = ReadProjectXDoc("Deltempo.App.WinUI", "Deltempo.App.WinUI.csproj");
        Assert.Equal(
            originalIconPath(),
            projectProperty(winUi, "ApplicationIcon"));
    }

    private static string originalIconPath() => @"..\Deltempo.App.Wpf\app.ico";

    private static string projectProperty(XDocument doc, string elementName)
    {
        IEnumerable<XElement> found = doc
            .Descendants()
            .Where(e => e.Name.LocalName == elementName && string.IsNullOrEmpty(e.Attribute("Condition")?.Value));

        XElement? value = found.FirstOrDefault();
        Assert.True(value is not null, $"expected a {elementName} property");
        return Assert.IsType<XElement>(value).Value.Trim();
    }

    // ------------------------------------------------------------------
    // The runtime copies beside the exe.
    // ------------------------------------------------------------------

    [Fact]
    public void WinUiProject_CopiesAppIcoBesideTheExeForTrayAndChip()
    {
        // TrayService.LoadCrispTrayIcon resolves AppContext.BaseDirectory\app.ico,
        // and ApplyWindowIcon sources the title-bar chip from the same path.
        string winUi = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Deltempo.App.WinUI", "Deltempo.App.WinUI.csproj"));

        Assert.Contains("<Content Include=\"..\\Deltempo.App.Wpf\\app.ico\"", winUi);
        Assert.Contains("Link=\"app.ico\"", winUi);
        Assert.Contains("CopyToOutputDirectory=\"PreserveNewest\"", winUi);
    }

    // ------------------------------------------------------------------
    // Taskbar + Alt-Tab: WM_SETICON on the window handle.
    // ------------------------------------------------------------------

    [Fact]
    public void WinUiShell_AssociatesSmallAndBigIconsWithTheWindow()
    {
        string code = ReadWinUi("MainWindow.xaml.cs");

        // Both magnitudes must be set (small: title bar; big: taskbar / Alt-Tab),
        // the same contract WPF's Window.Icon fulfils internally.
        Assert.Contains("WM_SETICON", code);
        Assert.Contains("ICON_SMALL", code);
        Assert.Contains("ICON_BIG", code);
        Assert.Contains("LoadImage", code);
        Assert.Contains("ExtractIconEx", code);

        // The handles must not leak when the window closes.
        Assert.Contains("DestroyIcon(_iconSmall)", code);
        Assert.Contains("DestroyIcon(_iconBig)", code);

        // Never fatal: the icon is cosmetic and startup must survive its failure.
        Assert.Contains("AppContext.BaseDirectory", code);
    }

    // ------------------------------------------------------------------
    // Top bar: the chip inside the custom title bar.
    // ------------------------------------------------------------------

    [Fact]
    public void WinUiTopBar_LeadsWithTheLogoChipLikeWpf()
    {
        // WPF's chip: 34px rounded border, surface fill, hairline outline,
        // with app.ico rendered inside it (MainWindow.xaml "Brand Identity").
        string wpf = ReadWpf("MainWindow.xaml");
        Assert.Contains("<Image Source=\"app.ico\"", wpf);
        Assert.Contains("Width=\"34\"", wpf);
        Assert.Contains("CornerRadius=\"9\"", wpf);

        // WinUI: the same chip lives in TitleBarHost, and code-behind fills it.
        string shell = ReadWinUi("MainWindow.xaml");
        Assert.Contains("x:Name=\"TitleBarLogo\"", shell);
        string code = ReadWinUi("MainWindow.xaml.cs");
        Assert.Contains("TitleBarLogo.Source", code);

        // And it is associated before the tray, in the same shell-startup row
        // where WPF's register its Window.Icon.
        int chrome = code.IndexOf("ApplyWindowChrome()", StringComparison.Ordinal);
        int icon = code.IndexOf("ApplyWindowIcon()", StringComparison.Ordinal);
        Assert.True(chrome >= 0 && icon > chrome, "ApplyWindowIcon must follow ApplyWindowChrome in shell startup");
    }
}
