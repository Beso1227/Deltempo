using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the UI quality bar the design tokens and shell XAML are supposed to hold:
/// readable contrast, a type scale with a legibility floor, and accessible hit targets.
///
/// These assert against the real resource files and the real MainWindow.xaml, so a future
/// palette tweak or an ad-hoc FontSize cannot silently regress accessibility.
/// </summary>
public class DesignSystemAccessibilityTests
{
    private static string ReadRepoFile(string relative)
    {
        // Walk up from the test assembly to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Deltempo.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }

    // ------------------------------------------------------------------
    // Contrast (WCAG 2.2 AA)
    // ------------------------------------------------------------------

    private static double Channel(string hex, int offset) =>
        Convert.ToInt32(hex.Substring(offset, 2), 16) / 255.0;

    private static double RelativeLuminance(string hex)
    {
        double F(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * F(Channel(hex, 1)) + 0.7152 * F(Channel(hex, 3)) + 0.0722 * F(Channel(hex, 5));
    }

    private static double ContrastRatio(string a, string b)
    {
        double la = RelativeLuminance(a), lb = RelativeLuminance(b);
        if (la < lb) (la, lb) = (lb, la);
        return (la + 0.05) / (lb + 0.05);
    }

    private static string BrushColor(string appXaml, string key)
    {
        var match = Regex.Match(appXaml, $"<SolidColorBrush x:Key=\"{key}\" Color=\"(#[0-9A-Fa-f]{{6}})\"");
        Assert.True(match.Success, $"Brush '{key}' not found in App.xaml.");
        return match.Groups[1].Value;
    }

    [Theory]
    [InlineData("TextHighBrush")]
    [InlineData("TextMediumBrush")]
    [InlineData("TextMutedBrush")]
    public void TextBrushes_MeetWcagAA_ForBodyText(string brushKey)
    {
        string appXaml = ReadRepoFile("App.xaml");

        // Against every surface these are actually painted on.
        string[] surfaces = { "CanvasDarkBrush", "SurfaceCardBrush", "SurfaceSubCardBrush", "HeaderDockBrush" };

        foreach (string surfaceKey in surfaces)
        {
            double ratio = ContrastRatio(BrushColor(appXaml, brushKey), BrushColor(appXaml, surfaceKey));
            Assert.True(ratio >= 4.5,
                $"{brushKey} on {surfaceKey} is {ratio:0.00}:1 — below the 4.5:1 WCAG AA minimum for body text.");
        }
    }

    [Fact]
    public void ScrollThumbBrush_MeetsNonTextContrast_Bar()
    {
        string appXaml = ReadRepoFile("App.xaml");

        double vsCanvas = ContrastRatio(BrushColor(appXaml, "ScrollThumbBrush"), BrushColor(appXaml, "CanvasDarkBrush"));
        Assert.True(vsCanvas >= 3.0,
            $"Scrollbar thumb is {vsCanvas:0.00}:1 on the canvas — below the 3:1 WCAG 1.4.11 minimum for controls.");
    }

    [Fact]
    public void AccentColors_MeetNonTextContrast_Bar()
    {
        string appXaml = ReadRepoFile("App.xaml");
        string canvas = BrushColor(appXaml, "CanvasDarkBrush");

        foreach (string key in new[] { "ElectricCyanBrush", "AmberWarningBrush", "RoseErrorBrush", "EmeraldGreenBrush" })
        {
            double ratio = ContrastRatio(BrushColor(appXaml, key), canvas);
            Assert.True(ratio >= 3.0, $"{key} is {ratio:0.00}:1 — below the 3:1 minimum for meaningful non-text UI.");
        }
    }

    // ------------------------------------------------------------------
    // Typography
    // ------------------------------------------------------------------

    [Fact]
    public void TypeScale_IsDocumented_AndHasALegibilityFloor()
    {
        string appXaml = ReadRepoFile("App.xaml");

        foreach (string key in new[]
                 {
                     "TextSizeMicro", "TextSizeCaption", "TextSizeBody",
                     "TextSizeBodyLarge", "TextSizeSubtitle", "TextSizeTitle", "TextSizeDisplay"
                 })
        {
            Assert.Contains($"x:Key=\"{key}\"", appXaml);
        }
    }

    [Fact]
    public void TypeScale_StepsAreMonotonic()
    {
        string appXaml = ReadRepoFile("App.xaml");

        double Value(string key)
        {
            var m = Regex.Match(appXaml, $"<sys:Double x:Key=\"{key}\">([\\d.]+)</sys:Double>");
            Assert.True(m.Success, $"Type-scale step '{key}' missing.");
            return double.Parse(m.Groups[1].Value);
        }

        string[] order =
        {
            "TextSizeMicro", "TextSizeCaption", "TextSizeBody",
            "TextSizeBodyLarge", "TextSizeSubtitle", "TextSizeTitle", "TextSizeDisplay"
        };

        for (int i = 1; i < order.Length; i++)
        {
            Assert.True(Value(order[i]) > Value(order[i - 1]),
                $"Type scale must increase: {order[i]} ({Value(order[i])}) should be larger than {order[i - 1]} ({Value(order[i - 1])}).");
        }

        // Body text must be readable, and the floor for anything the user must read is 11.
        Assert.True(Value("TextSizeBody") >= 12);
        Assert.True(Value("TextSizeCaption") >= 11);
    }

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("App.xaml")]
    [InlineData(@"Views\Modals\SystemRepairModal.xaml")]
    [InlineData(@"Views\Modals\AppUninstallModal.xaml")]
    [InlineData(@"Views\Modals\ForceDeleteModal.xaml")]
    [InlineData(@"Views\Modals\MemoryOptimizerModal.xaml")]
    [InlineData(@"Views\Modals\LockedFilesModal.xaml")]
    public void NoUiFile_UsesIllegibleFontSizes(string file)
    {
        string xaml = ReadRepoFile(file);

        var tooSmall = Regex.Matches(xaml, "FontSize=\"(\\d+(?:\\.\\d+)?)\"")
            .Select(m => double.Parse(m.Groups[1].Value))
            .Where(size => size < 10)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        Assert.True(tooSmall.Count == 0,
            $"{file} uses unreadable FontSize values: {string.Join(", ", tooSmall)}. " +
            "10 DIP is the legibility floor; use a TextSize* token instead of a magic number.");
    }

    // ------------------------------------------------------------------
    // Accessible names and hit targets
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData(@"Views\Modals\AppUninstallModal.xaml")]
    [InlineData(@"Views\Modals\ForceDeleteModal.xaml")]
    [InlineData(@"Views\Modals\LockedFilesModal.xaml")]
    public void EveryButton_HasTextOrAccessibleName(string file)
    {
        string xaml = ReadRepoFile(file);

        var unnamed = Regex.Matches(xaml, @"<Button\b.*?(?:/>|</Button>)", RegexOptions.Singleline)
            .Select(m => m.Value)
            .Where(b => b.Contains("x:Name=", StringComparison.Ordinal))
            .Where(b =>
            {
                bool hasName = b.Contains("AutomationProperties.Name", StringComparison.Ordinal);
                bool hasText = Regex.IsMatch(b, "<TextBlock[^>]*\\sText=\"[A-Za-z0-9]") ||
                               Regex.IsMatch(b, "Content=\"[A-Za-z0-9]");
                return !hasName && !hasText;
            })
            .Select(b => Regex.Match(b, "x:Name=\"([A-Za-z0-9_]+)\"").Groups[1].Value)
            .ToList();

        Assert.True(unnamed.Count == 0,
            $"{file} has icon-only buttons with no AutomationProperties.Name: {string.Join(", ", unnamed)}. " +
            "A screen reader announces an unnamed button as just \"button\".");
    }

    [Fact]
    public void HeaderToolButton_HasAccessibleHitTarget()
    {
        string appXaml = ReadRepoFile("App.xaml");

        var style = Regex.Match(appXaml, "<Style x:Key=\"HeaderToolButton\".*?</Style>", RegexOptions.Singleline);
        Assert.True(style.Success, "HeaderToolButton style missing.");

        Assert.Contains("MinHeight", style.Value);
        Assert.Contains("MinTouchTarget", style.Value);
        Assert.Contains("FocusVisualStyle", style.Value);

        // The declared target must clear the WCAG 2.5.8 minimum of 24 DIP.
        var minTarget = Regex.Match(appXaml, @"<sys:Double x:Key=""MinTouchTarget"">([\d.]+)</sys:Double>");
        Assert.True(minTarget.Success);
        Assert.True(double.Parse(minTarget.Groups[1].Value) >= 24,
            "MinTouchTarget must be at least the 24 DIP WCAG 2.5.8 minimum.");
    }

    [Fact]
    public void FocusVisualStyle_IsDefined_AndUsedByInteractiveStyles()
    {
        string appXaml = ReadRepoFile("App.xaml");

        Assert.Contains("x:Key=\"AccessibleFocusVisualStyle\"", appXaml);

        // Every custom button style must draw a focus indicator rather than suppressing it.
        // A style that declares BasedOn inherits the focus visual from its base, so only
        // stand-alone styles need to carry the setter themselves.
        foreach (string styleKey in new[]
                 {
                     "HeaderToolButton", "PillButton", "HeroCTAButton",
                     "WindowControlButton", "WindowCloseButton"
                 })
        {
            var style = Regex.Match(appXaml, $@"<Style x:Key=""{styleKey}"".*?</Style>", RegexOptions.Singleline);
            Assert.True(style.Success, $"Style '{styleKey}' missing.");

            bool inheritsFocus = style.Value.Contains("BasedOn=", StringComparison.Ordinal);
            if (inheritsFocus) continue;

            Assert.True(style.Value.Contains("FocusVisualStyle", StringComparison.Ordinal),
                $"Style '{styleKey}' must keep a visible focus indicator.");
        }

        // A BasedOn style must still point at a base that declares one.
        var closeStyle = Regex.Match(appXaml, @"<Style x:Key=""WindowCloseButton""[^>]*BasedOn=""\{StaticResource (\w+)\}""", RegexOptions.Singleline);
        Assert.True(closeStyle.Success, "WindowCloseButton should inherit from WindowControlButton.");
        var baseKey = closeStyle.Groups[1].Value;
        var baseStyle = Regex.Match(appXaml, $@"<Style x:Key=""{baseKey}"".*?</Style>", RegexOptions.Singleline);
        Assert.True(baseStyle.Value.Contains("FocusVisualStyle", StringComparison.Ordinal),
            $"Base style '{baseKey}' must declare the focus visual that '{baseKey}'-derived styles inherit.");
    }
}