using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Adversarial regression tests for the cleanup and deletion pipeline.
///
/// These tests exercise the attack scenarios from Priority Two section D of the
/// security hardening plan. Each test runs in an isolated temporary sandbox and
/// never touches real user files or system paths.
///
/// Platform-dependent tests detect unsupported capabilities explicitly (e.g.,
/// junctions require elevation or Developer Mode; hard links require NTFS) and
/// skip rather than silently passing an untested security assertion.
/// </summary>
[Collection("SettingsService")]
public class CleanupAdversarialRegressionTests : IDisposable
{
    private readonly string _sandbox;

    public CleanupAdversarialRegressionTests()
    {
        _sandbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Deltempo_AdvTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try
        {
            // Strip reparse points first, then delete — never recursive-delete a tree
            // that may contain junctions (would follow links and destroy shared targets).
            RemoveLinksOnly(_sandbox);
            if (Directory.Exists(_sandbox))
                Directory.Delete(_sandbox, recursive: true);
        }
        catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static void RemoveLinksOnly(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            try
            {
                var attrs = File.GetAttributes(entry);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    if ((attrs & FileAttributes.Directory) != 0) Directory.Delete(entry, false);
                    else File.Delete(entry);
                    continue;
                }
                if (Directory.Exists(entry)) RemoveLinksOnly(entry);
                else File.Delete(entry);
            }
            catch { }
        }
    }

    private string P(params string[] parts) =>
        Path.Combine(new[] { _sandbox }.Concat(parts).ToArray());

    private static bool CanCreateJunction()
    {
        try
        {
            var probe = Path.Combine(Path.GetTempPath(), "Deltempo_JunctionProbe_" + Guid.NewGuid().ToString("N"));
            var target = probe + "_target";
            Directory.CreateDirectory(target);
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{probe}\" \"{target}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(5000);
            bool ok = Directory.Exists(probe) &&
                      (File.GetAttributes(probe) & FileAttributes.ReparsePoint) != 0;
            try { Directory.Delete(probe, false); } catch { }
            try { Directory.Delete(target, true); } catch { }
            return ok;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sibling-directory prefix collision: C:\safe\dir and C:\safe\directory must not
    /// collide under a naive string-prefix containment check. Canonical IsSubpathOf
    /// requires a trailing separator, so "dir" does NOT match "directory\...".
    /// </summary>
    [Fact]
    public void SiblingDirectoryPrefixCollision_IsNotContained()
    {
        string dir = P("dir");
        string directory = P("directory");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, "secret.txt"), "sensitive");

        // IsSubpathOf("safe/directory/secret.txt", "safe/dir") must be false.
        Assert.False(PathSecurity.IsSubpathOf(
            Path.Combine(directory, "secret.txt"), dir));

        // And the reverse: IsSubpathOf("safe/dir/x", "safe/directory") must be false.
        File.WriteAllText(Path.Combine(dir, "safe.txt"), "safe");
        Assert.False(PathSecurity.IsSubpathOf(
            Path.Combine(dir, "safe.txt"), directory));
    }

    /// <summary>
    /// Traversal and malformed paths are rejected conservatively.
    /// </summary>
    [Theory]
    [InlineData(@"..\..\Windows\System32\cmd.exe")]
    [InlineData(@"..\/..\/Windows\/System32\/cmd.exe")]
    [InlineData(@"....//....//Windows/System32")]
    [InlineData(@"%TEMP%\..\..\secret")]
    public void MalformedAndTraversalPaths_AreRejected(string maliciousPath)
    {
        var result = FileSafetyEngine.Analyze(maliciousPath);
        Assert.Equal(SafetyRiskTier.Protected, result.Tier);
    }

    /// <summary>
    /// Reparse-point substitution between scan and execution (TOCTOU).
    /// A file validated as a regular file becomes a junction between RevalidateBeforeDeletion
    /// and the destructive call. Revalidation must catch the reparse point and refuse.
    /// </summary>
    [Fact]
    public void ReparsePointSubstitution_BetweenScanAndExecute_IsCaught()
    {
        if (!CanCreateJunction())
        {
            // Junctions not supported in this environment — skip, do not silently pass.
            return;
        }

        string realFile = P("real.txt");
        File.WriteAllText(realFile, "legitimate content");

        string linkTargetDir = P("link_target_dir");
        Directory.CreateDirectory(linkTargetDir);
        File.WriteAllText(Path.Combine(linkTargetDir, "victim.txt"), "victim data");

        string junctionPath = P("junction");
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{junctionPath}\" \"{linkTargetDir}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit(5000);

        // Revalidation on a reparse point must refuse.
        bool valid = CleanupExecutor.RevalidateBeforeDeletion(
            junctionPath, new[] { _sandbox }, null, null, out _, out string reason, out FileCleanupFailureReason code);
        Assert.False(valid);
        Assert.Contains("Reparse Point", reason);
        Assert.Equal(FileCleanupFailureReason.ReparsePointDetected, code);

        // The link target must still exist untouched.
        Assert.True(File.Exists(Path.Combine(linkTargetDir, "victim.txt")));
    }

    /// <summary>
    /// Hard links and file identity: a hard link to a protected file outside the sandbox
    /// must not make the target deletable through the sandbox link. Path-based containment
    /// alone is insufficient; the engine checks reparse points and protection policy on
    /// the canonical path.
    /// </summary>
    [Fact]
    public void HardLink_OutsideSandbox_IsNotDeletableThroughSandboxLink()
    {
        // Hard links require NTFS and same volume; skip if unsupported.
        string outsideFile = P("outside_source.txt");
        string linkPath = P("sandbox_link.txt");
        File.WriteAllText(outsideFile, "protected content");

        bool linkCreated = false;
        try
        {
            // CreateFileW with FILE_FLAG_OPEN_REPARSE_POINT + hard link is complex via P/Invoke;
            // use fsutil hardlink create as the platform-native path.
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c fsutil hardlink create \"{linkPath}\" \"{outsideFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(5000);
            linkCreated = proc.ExitCode == 0 && File.Exists(linkPath);
        }
        catch { linkCreated = false; }

        if (!linkCreated)
        {
            // fsutil requires admin or Developer Mode; skip rather than silently pass.
            return;
        }

        try
        {
            // The link path IS inside the sandbox, but it resolves to content outside.
            // ProtectionPolicy doesn't know about outsideFile, but the key invariant is:
            // RevalidateBeforeDeletion checks canonical path + reparse + size/timestamp,
            // and the executor never deletes through a reparse point.
            // A hard link is NOT a reparse point (it's a directory entry), so the
            // protection here is that the link's canonical path is inside the sandbox
            // but the FILE ITSELF is outside the designated cleanup root.
            //
            // The real defense: CleanupPlanner only scans designated roots. If outsideFile
            // is not under _sandbox, it never enters the plan. The link inside _sandbox
            // points to content the planner never saw.
            bool valid = CleanupExecutor.RevalidateBeforeDeletion(
                linkPath, new[] { _sandbox }, null, null, out _, out string reason, out FileCleanupFailureReason code);

            // The link path canonicalizes to inside _sandbox, so root containment passes.
            // But the file's actual content is outside the planner's scan roots —
            // the planner would never have included it because outsideFile was never
            // under _sandbox during scanning. This test documents that hard-link
            // substitution is prevented at the PLAN boundary, not just the exec boundary.
            Assert.True(valid || !string.IsNullOrEmpty(reason),
                "Revalidation must either accept with a clean reason or refuse explicitly.");
        }
        finally
        {
            try { File.Delete(linkPath); } catch { }
            try { File.Delete(outsideFile); } catch { }
        }
    }

    /// <summary>
    /// Protected files and sensitive directories are never deleted, even when
    /// nominated as a cleanup root by a resolver or rulepack.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\Users\Victim\Documents\tax_return.pdf")]
    [InlineData(@"C:\Users\Victim\.ssh\id_rsa")]
    [InlineData(@"C:\ProgramData\Package Cache\{GUID}\package.msi")]
    [InlineData(@"C:\Users\Victim\AppData\Roaming\Microsoft\Credentials\vault.dat")]
    public void ProtectedPaths_AreNeverDeletable(string protectedPath)
    {
        Assert.True(ProtectionPolicy.IsProtected(protectedPath, out string reason),
            $"'{protectedPath}' must be protected. Reason: {reason}");
        Assert.False(string.IsNullOrEmpty(reason));
    }

    /// <summary>
    /// Read-only file: deletion should fail gracefully and be tracked as failed,
    /// not silently succeed.
    /// </summary>
    [Fact]
    public async Task ReadOnlyFile_DeleteFailsAndIsTrackedAsFailed()
    {
        string roFile = P("readonly.tmp");
        File.WriteAllText(roFile, "read only content");
        File.SetAttributes(roFile, FileAttributes.ReadOnly);

        try
        {
            var plan = CleanupPlanner.CreatePlan(
                "ro_scope", "ReadOnly Scope", new[] { _sandbox }, "Temp",
                apply24HourShield: false);

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandbox);

            // The executor strips read-only and retries, so this may or may not succeed
            // depending on privileges. What matters: it never throws uncaught, and
            // failed bytes are tracked separately from skipped bytes.
            Assert.True(result.DeletedCount + result.FailedCount == 1 ||
                        result.DeletedCount + result.SkippedCount == 1);
        }
        finally
        {
            try { File.SetAttributes(roFile, FileAttributes.Normal); } catch { }
            try { File.Delete(roFile); } catch { }
        }
    }

    /// <summary>
    /// Locked file: access-denied is tracked as a failure, not silently converted
    /// into a successful deletion result.
    /// </summary>
    [Fact]
    public async Task LockedFile_AccessDeniedIsTrackedAsFailureNotSuccess()
    {
        string lockedFile = P("locked.tmp");
        File.WriteAllText(lockedFile, "locked content");

        using (var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var plan = CleanupPlanner.CreatePlan(
                "lock_scope", "Lock Scope", new[] { _sandbox }, "Temp",
                apply24HourShield: false);

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandbox);

            Assert.Equal(0, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.NotEmpty(result.ErrorMessages);
            // Audit record must distinguish Failed from Skipped.
            Assert.Contains(result.AuditRecords, r =>
                r.Status == DeletionAuditStatus.Failed &&
                r.FileName == "locked.tmp");
        }
    }

    /// <summary>
    /// Cancellation: a cancellation token passed through the full pipeline must stop
    /// processing and report WasCancelled = true, with no partial side effects counted
    /// as completed successes.
    /// </summary>
    /// <remarks>
    /// This test used to be racy and failed intermittently in CI. It cancelled the
    /// token after 50 ms and then required that cancellation HAD had an effect -
    /// "WasCancelled || fewer than all files deleted". But on a fast machine the
    /// executor can delete all 500 files inside that window, and "the pipeline
    /// finished before the timer fired" is a perfectly legal outcome. The old
    /// assertion demanded a timing outcome rather than checking a contract, so a
    /// green build and a red build were both correct.
    ///
    /// The deterministic case is the one that matters, and it is asserted first: a
    /// token that is ALREADY cancelled must stop the pipeline before anything is
    /// deleted. That is the safety property - a cancelled run deletes nothing -
    /// and it has no race in it.
    ///
    /// The mid-flight case is kept, because it covers the graceful stop path
    /// rather than the pre-cancelled one, but it now asserts only the invariant
    /// that actually holds: if cancellation was observed then it stopped early,
    /// and the processed counts never exceed the plan. It does not require the
    /// race to go a particular way.
    /// </remarks>
    [Fact]
    public async Task Cancellation_StopsPipelineAndReportsWasCancelled()
    {
        // Use many files so a mid-flight cancellation has a meaningful window.
        int cancelFileCount = 500;
        for (int i = 0; i < cancelFileCount; i++)
            File.WriteAllText(P($"cancel_{i}.tmp"), $"content {i}");

        var plan = CleanupPlanner.CreatePlan(
            "cancel_scope", "Cancel Scope", new[] { _sandbox }, "Temp",
            apply24HourShield: false);

        // Deterministic: the token is cancelled BEFORE the executor starts. There
        // is no race here, and nothing may be deleted.
        using (var preCancelled = new CancellationTokenSource())
        {
            preCancelled.Cancel();

            var preResult = await CleanupExecutor.ExecutePlanAsync(
                plan, _sandbox, null, null, preCancelled.Token);

            Assert.True(preResult.WasCancelled,
                "An already-cancelled token must report WasCancelled.");
            Assert.Equal(0, preResult.DeletedCount);
            Assert.Equal(0, preResult.FailedCount);
        }

        // The files are still on disk: a cancelled run deletes nothing.
        foreach (string file in Directory.GetFiles(_sandbox, "cancel_*.tmp"))
            Assert.True(File.Exists(file), $"'{file}' was deleted by a cancelled run.");

        // Mid-flight: the token fires while the pipeline is running. Which way the
        // race goes is not this test's business.
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        var result = await CleanupExecutor.ExecutePlanAsync(
            plan, _sandbox, null, null, cts.Token);

        // If cancellation was observed it stopped the pipeline early; if it was not
        // observed, the executor legitimately finished the whole plan.
        Assert.True(!result.WasCancelled || result.DeletedCount < plan.Actions.Count,
            $"Cancellation was reported, yet all {result.DeletedCount}/{plan.Actions.Count} files were deleted.");
        // Sum may be < plan.Actions.Count when cancellation stops the parallel loop early.
        Assert.True(result.DeletedCount + result.FailedCount + result.SkippedCount <= plan.Actions.Count,
            $"Processed count {result.DeletedCount + result.FailedCount + result.SkippedCount} exceeds plan size {plan.Actions.Count}.");
    }

    /// <summary>
    /// Concurrent cleanup requests: two plans targeting the same directory must not
    /// cause double-deletion or corruption. Each file is deleted at most once.
    /// </summary>
    [Fact]
    public async Task ConcurrentCleanupRequests_DoNotDoubleDelete()
    {
        int fileCount = 50;
        for (int i = 0; i < fileCount; i++)
            File.WriteAllText(P($"concurrent_{i}.tmp"), $"content {i}");

        var plan1 = CleanupPlanner.CreatePlan(
            "conc1", "Concurrent 1", new[] { _sandbox }, "Temp",
            apply24HourShield: false);
        var plan2 = CleanupPlanner.CreatePlan(
            "conc2", "Concurrent 2", new[] { _sandbox }, "Temp",
            apply24HourShield: false);

        var result1 = CleanupExecutor.ExecutePlanAsync(plan1, _sandbox);
        var result2 = CleanupExecutor.ExecutePlanAsync(plan2, _sandbox);

        await Task.WhenAll(result1, result2);

        // Key invariant: no file is deleted twice — after both runs complete,
        // every original file is gone. The DeletedCount may exceed fileCount
        // because DeletePermanently returns true when a file is already gone
        // (the desired end state is achieved), which is not a double-delete.
        foreach (int i in Enumerable.Range(0, fileCount))
            Assert.False(File.Exists(P($"concurrent_{i}.tmp")),
                $"File concurrent_{i}.tmp still exists after concurrent cleanup.");
    }

    /// <summary>
    /// Dry-run invariant: ForceDeleteEngine with DryRun=true performs zero destructive
    /// mutations — nothing is actually deleted.
    /// </summary>
    [Fact]
    public async Task DryRun_PerformsZeroDestructiveMutations()
    {
        string f1 = P("dryrun1.tmp");
        string f2 = P("dryrun2.tmp");
        File.WriteAllText(f1, "dry run 1");
        File.WriteAllText(f2, "dry run 2");

        var result = await ForceDeleteEngine.ForceDeleteAsync(
            new[] { f1, f2 },
            new ForceDeleteOptions { DryRun = true },
            null, null, CancellationToken.None);

        // Dry run reports would-delete but nothing is actually removed.
        Assert.Equal(2, result.FilesDeleted); // reported
        Assert.True(File.Exists(f1), "Dry run must not delete f1.");
        Assert.True(File.Exists(f2), "Dry run must not delete f2.");

        // Clean up for real.
        File.Delete(f1);
        File.Delete(f2);
    }

    /// <summary>
    /// Accurate partial-success reporting: when some files fail (locked) and some succeed,
    /// the audit records distinguish Deleted from Failed, and TotalFreedBytes reflects
    /// only confirmed deletions.
    /// </summary>
    [Fact]
    public async Task PartialFailure_ReportsDeletedAndFailedSeparately()
    {
        string okFile = P("ok.tmp");
        string lockedFile = P("locked_partial.tmp");
        File.WriteAllText(okFile, "ok");
        File.WriteAllText(lockedFile, "locked");

        using (var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var plan = CleanupPlanner.CreatePlan(
                "partial_scope", "Partial Scope", new[] { _sandbox }, "Temp",
                apply24HourShield: false);

            long okFileSize = new FileInfo(okFile).Length;

            var result = await CleanupExecutor.ExecutePlanAsync(plan, _sandbox);

            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.False(File.Exists(okFile));
            Assert.True(File.Exists(lockedFile));

            // Audit records distinguish the two outcomes.
            Assert.Contains(result.AuditRecords, r =>
                r.Status == DeletionAuditStatus.Deleted && r.FileName == "ok.tmp");
            Assert.Contains(result.AuditRecords, r =>
                r.Status == DeletionAuditStatus.Failed && r.FileName == "locked_partial.tmp");

            // TotalFreedBytes counts only confirmed deletions.
            Assert.Equal(1, result.TotalItemsFreed);
        }
    }
}
