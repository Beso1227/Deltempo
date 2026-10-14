using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
}