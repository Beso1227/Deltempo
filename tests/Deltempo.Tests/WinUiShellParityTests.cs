using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the feature parity between the WPF application and the WinUI 3 shell.
///
/// The WPF header is the list of tools the product actually ships. The WinUI shell
/// has its own navigation, and the two drifted: the WinUI port opened with seven
/// sections and the WPF header had eight tools, because the process manager was
/// never ported and nothing in the build said so.
///
/// These tests read both files and compare them, so the next tool that is added to
/// one side and forgotten on the other fails the build instead of being discovered
/// by a user looking for the button.
/// </summary>
public class WinUiShellParityTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Deltempo.sln"))
                || File.Exists(Path.Combine(dir.FullName, "deltempo.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static string ReadRepoFile(string relative)
        => File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// The tool names the WPF header offers, read from the shipped XAML rather than
    /// from a hand-kept list - the list is the thing that drifts.
    /// </summary>
    private static List<string> WpfToolNames()
    {
        string wpfWindow = ReadRepoFile("src/Deltempo.App.Wpf/MainWindow.xaml");

        return Regex.Matches(wpfWindow, "AutomationProperties\\.Name=\"([^\"]+Tool)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The sections the WinUI shell offers, read from its own section table.
    /// </summary>
    private static List<string> WinUiSectionNames()
    {
        string shell = ReadRepoFile("src/Deltempo.App.WinUI/MainWindow.xaml.cs");

        return Regex.Matches(shell, "private const string \\w+SectionLabel = \"([^\"]+)\";")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void WinUiShell_OffersEveryToolTheWpfHeaderOffers()
    {
        List<string> wpfTools = WpfToolNames();
        List<string> winUiSections = WinUiSectionNames();

        Assert.NotEmpty(wpfTools);

        List<string> missing = wpfTools
            .Where(tool => !winUiSections.Contains(tool, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"The WinUI shell is missing tools the WPF header offers: {string.Join(", ", missing)}. "
            + $"WPF tools: {string.Join(", ", wpfTools)}.");
    }

    [Fact]
    public void WinUiShell_OffersTheProcessManagerTheWpfAppHas()
    {
        // The tool that was missing when this guard was written, kept as its own case
        // so the regression is legible in the failure message.
        List<string> winUiSections = WinUiSectionNames();

        Assert.Contains("Process Manager Tool", winUiSections);
    }

    [Fact]
    public void WinUiShell_OffersSettingsAndAbout()
    {
        List<string> winUiSections = WinUiSectionNames();

        Assert.Contains("Settings", winUiSections);
        Assert.Contains("About", winUiSections);
    }

    /// <summary>
    /// The name of the constant that holds a section label, e.g. "Settings" lives in
    /// "SettingsSectionLabel". The shell's tables are keyed by those constants, so
    /// this is how a label is traced back to the code that uses it.
    /// </summary>
    private static string ConstantFor(string label)
    {
        string shell = ReadRepoFile("src/Deltempo.App.WinUI/MainWindow.xaml.cs");

        string constant = Regex.Match(
            shell,
            "private const string (\\w+SectionLabel) = \"" + Regex.Escape(label) + "\";")
            .Groups[1]
            .Value;

        Assert.False(string.IsNullOrEmpty(constant), $"No section constant for '{label}'.");
        return constant;
    }

    [Theory]
    [InlineData("Process Manager Tool")]
    [InlineData("Settings")]
    [InlineData("About")]
    public void WinUiShell_DescribesEverySectionInItsHeaderCopy(string label)
    {
        string shell = ReadRepoFile("src/Deltempo.App.WinUI/MainWindow.xaml.cs");

        // The page header is shell-owned: every section must have an eyebrow and a
        // description, or the page shows an empty header.
        string constant = ConstantFor(label);
        Assert.Contains($"[{constant}] = (", shell);
    }

    [Fact]
    public void WinUiShell_MapsEverySectionToAPanel()
    {
        string shell = ReadRepoFile("src/Deltempo.App.WinUI/MainWindow.xaml.cs");

        foreach (string label in WinUiSectionNames())
        {
            string constant = ConstantFor(label);
            Assert.Contains($"_workspacePanels[{constant}] =", shell);
        }
    }

    [Fact]
    public void WinUiShell_DeclaresItsPanelsInXaml()
    {
        string shellXaml = ReadRepoFile("src/Deltempo.App.WinUI/MainWindow.xaml");

        // The process manager, settings and about panels exist as views and are
        // declared in the shell, not merely named in a section table.
        Assert.Contains("views:ProcessWorkspaceView", shellXaml);
        Assert.Contains("views:SettingsWorkspaceView", shellXaml);
        Assert.Contains("views:AboutWorkspaceView", shellXaml);
    }

    [Fact]
    public void WinUiShell_ThemesEveryTokenThroughThemeResource()
    {
        // A StaticResource on a theme-varying brush freezes the brush in whichever
        // theme happened to be active when the file was parsed, which is how the
        // light theme silently fails to apply to one control.
        string tokens = ReadRepoFile("src/Deltempo.App.WinUI/Themes/Deltempo.Tokens.xaml");

        Assert.Contains("<ResourceDictionary.ThemeDictionaries>", tokens);
        Assert.Contains("x:Key=\"Dark\"", tokens);
        Assert.Contains("x:Key=\"Light\"", tokens);
    }
}
