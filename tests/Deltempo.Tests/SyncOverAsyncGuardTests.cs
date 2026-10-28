using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Sync-over-async drift guard. Blocking on async from a worker thread
/// (.GetAwaiter().GetResult(), .Result, .Wait()) deadlocks when the awaited
/// work needs the thread that is doing the blocking, which is why every
/// current site is deliberate and documented. This test pins the known
/// deliberate sites by file and count; if the numbers drift, a site was
/// added or removed without pairing it with (a) an intent comment at the
/// call site and (b) a test of the contract it relies on (the installed
/// UiDispatcher for marshal sites, explicit CancellationToken.None or a
/// bounded wait for cleanup sites). The failure message repeats that rule.
/// </summary>
public class SyncOverAsyncGuardTests
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

    /// <summary>
    /// The deliberate sites as of this audit: file -> expected hit count.
    /// Hit counts themselves are the drift detector — a local count may be
    /// off by refactors, so the assertion is only about the file SET and
    /// rough per-file magnitude staying recognised.
    /// </summary>
    public static IEnumerable<object[]> DeliberateSites()
    {
        yield return new object[] { Path.Combine("src", "Deltempo.App.WinUI", "App.xaml.cs"), 1 };
        yield return new object[] { Path.Combine("src", "Deltempo.App.Wpf", "App.xaml.cs"), 3 };
        yield return new object[] { Path.Combine("src", "Deltempo.Core", "Abstractions", "IProcessRunner.cs"), 4 };
        yield return new object[] { Path.Combine("src", "Deltempo.Core", "Scanning", "NativeFileScanner.cs"), 1 };
        yield return new object[] { Path.Combine("src", "Deltempo.Core", "Services", "CleanerService.cs"), 1 };
    }

    [Theory]
    [MemberData(nameof(DeliberateSites))]
    public void DeliberateSite_StaysRecognised(string relativePath, int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string text = File.ReadAllText(Path.Combine(RepoRoot(), relativePath));

        int actual = Regex.Matches(
            text,
            @"GetAwaiter\(\)\.GetResult\(\)|\.Result\b|\.Wait\(\)",
            RegexOptions.IgnoreCase).Count;

        Assert.True(actual == expectedCount,
            $"{relativePath} now has {actual} sync-over-async sites, this audit pinned {expectedCount}. " +
            "A new site MUST be added here only after (1) an intent comment at the call site naming " +
            "why blocking is safe (no dispatcher wait / no cancelled work) and (2) a test of that " +
            "contract. Update the pinned count in SyncOverAsyncGuardTests once both are done.");
    }
}
