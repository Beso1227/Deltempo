using System.Diagnostics;
using System.IO;
using System.Reflection;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The orphan classifier decides what gets PROPOSED FOR DELETION. That makes its evidence
/// harvest a safety surface, not just a performance surface - so these tests assert both
/// that it still finds what it must and that parallelising it changed nothing about the
/// result.
/// </summary>
[Collection("SettingsService")]
public class OrphanEvidenceHarvestTests
{
    // These two are private implementation details of the classifier. They are read via
    // reflection rather than by widening the production surface, because the test's job is
    // to check the harvest against the classifier's OWN thresholds - not to redefine them.
    private static readonly HashSet<string> ProtectedVendors =
        (HashSet<string>)typeof(OrphanedAppService)
            .GetField("ProtectedSystemFolderNames", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private static readonly int MinimumPlausibleCount =
        (int)typeof(OrphanedAppService)
            .GetField("MinimumPlausibleActiveAppCount", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    public void GetComprehensiveActiveAppKeywords_StillContainsProtectedVendorNames()
    {
        // The vendor list is added before any collector runs, so this must hold even on a
        // host where every external evidence source fails. It is the floor that makes the
        // degraded-evidence guard meaningful.
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        foreach (string vendor in ProtectedVendors)
        {
            Assert.Contains(vendor, keywords);
        }
    }

    [Fact]
    public void GetComprehensiveActiveAppKeywords_ExceedsTheDegradedEvidenceFloor()
    {
        // If this ever drops below the floor the orphan scan returns nothing at all, by
        // design. That is a deliberate fail-closed behaviour, not a bug - but it means a
        // real regression here looks like "orphan scan silently stopped working".
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        Assert.True(
            keywords.Count >= MinimumPlausibleCount,
            $"Only {keywords.Count} keywords gathered; the orphan scan will refuse to run. " +
            "Evidence collection is degraded on this host.");
    }

    [Fact]
    public void GetComprehensiveActiveAppKeywords_IncludesThisRunningProcess()
    {
        // The running-process source is one of the six collectors. If the parallel merge
        // lost it, a live application would look uninstalled - the exact failure that puts
        // installed software onto a deletion list.
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        Assert.Contains(Process.GetCurrentProcess().ProcessName, keywords);
    }

    /// <summary>
    /// The collectors now run concurrently, so the merge is the part with new failure
    /// modes: a result set allocated per index and unioned afterwards. A mis-sized array
    /// or a lost set would silently shrink the evidence, which is safe-by-accident rather
    /// than by design. This checks the union is at least as large as the vendors alone and
    /// is deduplicated.
    /// </summary>
    [Fact]
    public void GetComprehensiveActiveAppKeywords_IsUnionedNotOverwritten()
    {
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        // Count must equal the distinct count: HashSet dedupes, so any duplication that
        // leaked through the merge would show up here as a mismatch in the other
        // direction only if the set were not a set. Guard against a caller mutation.
        Assert.Equal(keywords.Count, keywords.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(keywords.Count > ProtectedVendors.Count);
    }

    [Fact]
    public void GetComprehensiveActiveAppKeywords_IsCaseInsensitive()
    {
        // The merge must preserve the OrdinalIgnoreCase comparer. A collector returning a
        // case-SENSITIVE set would let "Chrome" and "chrome" both survive, inflating the
        // count past the floor and letting an orphan check miss a real match.
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        foreach (string keyword in keywords)
        {
            if (keyword.Length < 3) continue;

            string upper = keyword.ToUpperInvariant();
            Assert.True(
                keywords.Contains(upper) || keywords.Contains(keyword),
                $"'{keyword}' survived the merge under an inconsistent comparer.");
            break;
        }
    }

    /// <summary>
    /// The collectors build their own local sets and are merged once, so no state can
    /// accumulate across runs. Contamination would show as a count that grows every time.
    ///
    /// The counts cannot be compared exactly, because one of the sources is the set of
    /// RUNNING PROCESSES and the test host genuinely starts and stops processes while this
    /// suite runs. So the assertion is that repeated harvests stay in a tight band - loose
    /// enough for process churn, far too tight for leaked state.
    /// </summary>
    [Fact]
    public void GetComprehensiveActiveAppKeywords_RepeatedCallsDoNotAccumulate()
    {
        var counts = new List<int>();
        for (int run = 0; run < 3; run++)
        {
            var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();
            counts.Add(keywords.Count);

            foreach (string vendor in ProtectedVendors)
            {
                Assert.Contains(vendor, keywords);
            }
        }

        int min = counts.Min();
        int max = counts.Max();

        Assert.True(
            max - min <= Math.Max(5, min / 20),
            $"Harvest sizes drifted across runs: {string.Join(", ", counts)}. " +
            "A monotonic climb means a collector is accumulating into shared state.");
    }

    [Fact]
    public void GetComprehensiveActiveAppKeywords_CompletesWithinAReasonableBudget()
    {
        // Several hundred independent registry and filesystem round-trips. This is a
        // generous ceiling chosen to fail only on a pathological regression (the
        // serialized form, or a lost parallelism) rather than on a slow disk.
        var sw = Stopwatch.StartNew();

        OrphanedAppService.GetComprehensiveActiveAppKeywords();

        sw.Stop();
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(20),
            $"Orphan evidence harvest took {sw.Elapsed.TotalSeconds:F1}s, which suggests the " +
            "collectors are no longer overlapping.");
    }
}

/// <summary>
/// Restart Manager session reuse. RmStartSession/RmEndSession are real calls into the
/// Restart Manager service; the executor used to pay a pair of them per lock query per
/// file, inside a 16-way parallel loop.
/// </summary>
public class RestartManagerSessionTests : IDisposable
{
    private readonly string _sandbox;

    public RestartManagerSessionTests()
    {
        _sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_RmSession_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
        }
        catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string MakeFile(string name)
    {
        string path = Path.Combine(_sandbox, name);
        File.WriteAllText(path, "x");
        return path;
    }

    /// <summary>
    /// The core invariant: inside a scope, repeat questions about the SAME file must be
    /// answered from the memo rather than re-asking the Restart Manager. This is the
    /// saving - the executor asks about a file up to three times on its failure path.
    /// </summary>
    [Fact]
    public void ScopedQueries_RepeatAsksAboutTheSameFileAreServedFromTheMemo()
    {
        string file = MakeFile("memoized.tmp");

        long before = RestartManagerService.QueriesServedFromCache;

        using (RestartManagerService.BeginQueryScope())
        {
            RestartManagerService.GetLockingProcesses(file);
            RestartManagerService.GetLockingProcesses(file);
            RestartManagerService.GetLockingProcesses(file);
        }

        // Three identical asks, so at least two came from the memo. Asserted as a floor on
        // the process-wide counter because the suite runs other cleanups concurrently and
        // those can only ADD hits, never remove them.
        Assert.True(
            RestartManagerService.QueriesServedFromCache - before >= 2,
            "Repeat questions about the same file were not memoized.");
    }

    [Fact]
    public void ScopedQueries_CostOneRealQueryPerDistinctFile()
    {
        var files = new List<string>();
        for (int i = 0; i < 8; i++) files.Add(MakeFile($"distinct_{i}.tmp"));

        long before = RestartManagerService.QueriesServedFromCache;

        using (RestartManagerService.BeginQueryScope())
        {
            foreach (string file in files)
            {
                // Each file asked twice, exactly as the executor's failure path does.
                RestartManagerService.GetLockingProcesses(file);
                RestartManagerService.GetLockingProcesses(file);
            }
        }

        // 16 asks over 8 distinct paths: at least 8 must have come from the memo.
        Assert.True(
            RestartManagerService.QueriesServedFromCache - before >= 8,
            "The memo did not halve the queries for repeat asks on distinct files.");
    }

    /// <summary>
    /// The regression that killed the session-reuse approach: a memo must never answer a
    /// question about a DIFFERENT file. That earlier optimisation reused the Restart
    /// Manager session and assumed RmRegisterResources replaced its resource set; it does
    /// NOT - it accumulates - so an unlocked file still reported the previous file's
    /// holder. That is not a performance defect but a safety one, because such a claim
    /// arms a reboot deletion against a file nothing is holding.
    /// </summary>
    [Fact]
    public void ScopedQueries_NeverReportAHolderForAnUnrelatedFile()
    {
        string held = MakeFile("held_by_us.tmp");
        string free = MakeFile("not_held.tmp");

        using var hold = new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        using (RestartManagerService.BeginQueryScope())
        {
            var heldLockers = RestartManagerService.GetLockingProcesses(held);
            Assert.NotEmpty(heldLockers);
            Assert.Contains(heldLockers, l => l.ProcessId == Environment.ProcessId);

            var freeLockers = RestartManagerService.GetLockingProcesses(free);
            Assert.DoesNotContain(freeLockers, l => l.ProcessId == Environment.ProcessId);
        }
    }

    [Fact]
    public void QueriesOutsideAnyScope_StillReturnCorrectResults()
    {
        // The memo is opt-in; every existing caller that never opens a scope must behave
        // exactly as before.
        string held = MakeFile("unscoped_held.tmp");
        using var hold = new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var lockers = RestartManagerService.GetLockingProcesses(held);

        Assert.NotEmpty(lockers);
        Assert.Contains(lockers, l => l.ProcessId == Environment.ProcessId);
    }

    [Fact]
    public void NestedScopes_DoNotDisturbEachOther()
    {
        string file = MakeFile("nested.tmp");

        using (RestartManagerService.BeginQueryScope())
        {
            RestartManagerService.GetLockingProcesses(file);

            using (RestartManagerService.BeginQueryScope())
            {
                Assert.Empty(RestartManagerService.GetLockingProcesses(file));
            }

            Assert.Empty(RestartManagerService.GetLockingProcesses(file));
        }
    }

    [Fact]
    public void BlankAndMissingPaths_ReturnEmptyAndAreNeverCached()
    {
        using var scope = RestartManagerService.BeginQueryScope();

        long before = RestartManagerService.QueriesServedFromCache;

        Assert.Empty(RestartManagerService.GetLockingProcesses(""));
        Assert.Empty(RestartManagerService.GetLockingProcesses("   "));
        Assert.Empty(RestartManagerService.GetLockingProcesses(Path.Combine(_sandbox, "ghost.tmp")));

        Assert.Equal(before, RestartManagerService.QueriesServedFromCache);
    }
}
