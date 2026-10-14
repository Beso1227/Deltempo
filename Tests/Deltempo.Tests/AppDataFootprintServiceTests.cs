using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// W2 coverage: the AppData footprint report.
///
/// The load-bearing requirement is not behavioural but structural. The plan's hard rule is
/// that this feature has NO delete code path - not a disabled flag, the absence of one. These
/// tests assert that structurally via reflection, so a future edit that adds a Remove/Clean
/// method (or returns the mutable TargetFolderInfo the cleaning pipeline consumes) fails here.
/// </summary>
public class AppDataFootprintServiceTests
{
    private static readonly string[] ForbiddenVerbFragments =
        ["delete", "remove", "clean", "purge", "erase", "wipe", "uninstall", "apply", "execute", "run"];

    // ------------------------------------------------- structural: no delete capability

    /// <summary>
    /// The central guarantee. A method whose name implies mutation, or which hands back
    /// TargetFolderInfo (the type the cleaning pipeline deletes through), would mean this
    /// "report" could delete.
    /// </summary>
    [Fact]
    public void AppDataFootprintService_ExposesNoDeleteCapability()
    {
        Type type = typeof(AppDataFootprintService);

        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            string name = method.Name.ToLowerInvariant();

            bool forbiddenVerb = ForbiddenVerbFragments.Any(f => name.Contains(f, StringComparison.Ordinal));
            bool returnsTarget = method.ReturnType == typeof(TargetFolderInfo) ||
                                 (method.ReturnType.IsGenericType &&
                                  method.ReturnType.GetGenericArguments().Contains(typeof(TargetFolderInfo)));

            Assert.False(forbiddenVerb,
                $"'{method.Name}' implies mutation. The footprint report must stay read-only.");
            Assert.False(returnsTarget,
                $"'{method.Name}' returns TargetFolderInfo, the type the cleaning pipeline deletes through.");
        }
    }

    /// <summary>
    /// The entry type must be immutable. A settable property would let a caller mutate a
    /// report row into a deletion target.
    /// </summary>
    [Fact]
    public void AppDataFootprintEntry_IsImmutable()
    {
        PropertyInfo[] mutable = typeof(AppDataFootprintEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
            .ToArray();

        Assert.True(mutable.Length == 0,
            "Report rows must be immutable, but these have public setters: " +
            string.Join(", ", mutable.Select(p => p.Name)));
    }

    /// <summary>
    /// An init-only accessor is a public setter at the IL level but cannot mutate after
    /// construction, so it satisfies immutability. A plain set does not.
    /// </summary>
    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    /// <summary>
    /// The service must not be able to hand out a deletion target through ANY return type.
    /// </summary>
    [Fact]
    public void AppDataFootprintService_NeverReturnsTargetFolderInfo()
    {
        Type type = typeof(AppDataFootprintService);

        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            IEnumerable<Type> reachable = method.ReturnType.IsGenericType
                ? method.ReturnType.GetGenericArguments()
                : [method.ReturnType];

            Assert.DoesNotContain(reachable, t => t == typeof(TargetFolderInfo) || t == typeof(JunkFileItem));
        }
    }

    // ------------------------------------------------- behavioural

    [Fact]
    public void Analyze_ReturnsEntriesSortedBySizeDescending()
    {
        var entries = AppDataFootprintService.Analyze();

        if (entries.Count == 0)
        {
            // No readable AppData tree on this machine - nothing to assert, but not a failure.
            return;
        }

        for (int i = 1; i < entries.Count; i++)
        {
            Assert.True(entries[i - 1].SizeBytes >= entries[i].SizeBytes,
                "Entries must be ordered largest first.");
        }
    }

    [Fact]
    public void Analyze_LabelsEveryEntryWithAKnownAppDataRoot()
    {
        string[] expected = ["Roaming", "Local", "LocalLow"];

        var entries = AppDataFootprintService.Analyze();

        Assert.All(entries, e => Assert.Contains(e.RootLabel, expected));
    }

    /// <summary>
    /// Entries are DEPTH-1 folders only. Emitting the AppData root as a row would double
    /// count every file in any total computed over the entries - a real bug this caught.
    /// </summary>
    [Fact]
    public void Analyze_ExcludesTheAppDataRootsThemselves()
    {
        var entries = AppDataFootprintService.Analyze();

        foreach (var entry in entries)
        {
            string name = Path.GetFileName(entry.Path.TrimEnd('\\'));
            Assert.False(
                name is "Roaming" or "Local" or "LocalLow",
                $"The AppData root '{entry.Path}' must not appear as its own entry; totals would double count.");
        }
    }

    [Fact]
    public void Analyze_EveryEntryHasAPathAndNonNegativeSize()
    {
        var entries = AppDataFootprintService.Analyze();

        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Path));
            Assert.True(e.SizeBytes >= 0, "Sizes cannot be negative.");
        });
    }

    [Fact]
    public void Analyze_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var entries = AppDataFootprintService.Analyze(cts.Token);

        Assert.True(entries.Count <= 1,
            "A pre-cancelled token must stop the walk promptly.");
    }

    /// <summary>
    /// The report must survive a machine where AppData cannot be walked, rather than throwing
    /// into the CLI.
    /// </summary>
    [Fact]
    public void Analyze_DoesNotThrow()
    {
        var exception = Record.Exception(() => AppDataFootprintService.Analyze());
        Assert.Null(exception);
    }

    // ------------------------------------------------- UI contract

    private static string ReadRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Deltempo.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }

    /// <summary>
    /// The service cannot delete (proven by reflection above), but the VIEW could still offer a
    /// button that does. This pins the XAML itself: no action button whose label implies
    /// mutation may exist in the report surface.
    /// </summary>
    [Fact]
    public void AppDataFootprintModal_HasNoDeleteAffordance()
    {
        string xaml = ReadRepoFile(@"Views\Modals\AppDataFootprintModal.xaml");

        var buttons = Regex.Matches(xaml, "<Button\\b.*?(?:/>|</Button>)", RegexOptions.Singleline)
            .Select(m => m.Value)
            .ToList();

        Assert.NotEmpty(buttons);

        foreach (var button in buttons)
        {
            foreach (var forbidden in new[] { "Clean", "Delete", "Remove", "Purge", "Erase", "Wipe" })
            {
                bool visibleLabel = Regex.IsMatch(button, $"Text=\"[^\"]*{forbidden}", RegexOptions.IgnoreCase)
                                 || Regex.IsMatch(button, $"Content=\"[^\"]*{forbidden}", RegexOptions.IgnoreCase)
                                 || Regex.IsMatch(button, $"ToolTip=\"[^\"]*{forbidden}", RegexOptions.IgnoreCase)
                                 || Regex.IsMatch(button, $"AutomationProperties\\.Name=\"[^\"]*{forbidden}", RegexOptions.IgnoreCase);

                Assert.False(visibleLabel,
                    $"The AppData report is read-only, but a button offers '{forbidden}'. Button was: {button}");
            }
        }
    }

    /// <summary>
    /// The read-only guarantee must also be visible to the person using it, not just enforced
    /// in code. Pins the copy so a future redesign cannot quietly drop it.
    /// </summary>
    [Fact]
    public void AppDataFootprintModal_StatesItDeletesNothing()
    {
        string xaml = ReadRepoFile(@"Views\Modals\AppDataFootprintModal.xaml");

        Assert.Contains("Report only", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nothing is deleted", xaml, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The modal must be reachable. Registered as an overlay, wired to Escape, and included in
    /// the Tab focus trap - a modal that opens but cannot be dismissed by keyboard traps the user.
    /// </summary>
    [Fact]
    public void AppDataFootprintModal_IsWiredIntoTheMainWindow()
    {
        string windowXaml = ReadRepoFile("MainWindow.xaml");
        string windowCode = ReadRepoFile("MainWindow.xaml.cs");
        string cleaningCode = ReadRepoFile("MainWindow.Cleaning.cs");

        Assert.Contains("AppDataFootprintModalOverlay", windowXaml);
        Assert.Contains("AppDataFootprintModalOverlay.CloseModal()", windowCode);
        Assert.Contains("AppDataFootprintModalOverlay.Visibility == Visibility.Visible) activeModal", windowCode);
        Assert.Contains("AppDataFootprintModalOverlay.PopulateAndOpen()", cleaningCode);
    }

    /// <summary>
    /// Guards the one failure the build and the accessibility gate cannot see: a code-behind
    /// reference to an element that is not declared in the XAML throws from
    /// InitializeComponent at RUNTIME, the first time the user opens the modal.
    ///
    /// The test project references Deltempo.Core, which does not compile Views\, so the modal
    /// cannot be instantiated from a test. UI Automation is unusable here too: the app is
    /// requireAdministrator and UIPI blocks a non-elevated caller from walking its tree
    /// (verified - UIA reports zero buttons for pre-existing controls as well). This static
    /// check is what closes that gap.
    /// </summary>
    [Fact]
    public void AppDataFootprintModal_ReferencesOnlyDeclaredElementNames()
    {
        string xaml = ReadRepoFile(@"Views\Modals\AppDataFootprintModal.xaml");

        var declared = Regex.Matches(xaml, "x:Name=\"([A-Za-z0-9_]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Every named element the code-behind touches at runtime.
        string[] referenced =
        [
            "FootprintItemsControl",
            "FootprintSummaryText",
            "FootprintFooterText",
            "RefreshBtn"
        ];

        Assert.All(referenced, name =>
            Assert.True(declared.Contains(name),
                $"The modal code-behind uses '{name}' but the XAML does not declare x:Name=\"{name}\". " +
                "InitializeComponent would throw the first time the user opens the report."));
    }

    /// <summary>
    /// The overlay must span the workspace rows so it actually covers the app, matching every
    /// other modal overlay declaration.
    /// </summary>
    [Fact]
    public void AppDataFootprintModal_IsDeclaredAsAColapsedOverlay()
    {
        string xaml = ReadRepoFile("MainWindow.xaml");

        var declaration = Regex.Match(xaml, "<modals:AppDataFootprintModal\\b[^>]*>", RegexOptions.Singleline);

        Assert.True(declaration.Success, "AppDataFootprintModal is not declared in MainWindow.xaml.");
        Assert.Contains("Visibility=\"Collapsed\"", declaration.Value);
        Assert.Contains("Grid.Row=\"1\"", declaration.Value);
    }
}