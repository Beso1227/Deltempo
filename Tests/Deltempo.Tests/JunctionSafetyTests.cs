using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Regression tests for the junction/symlink data-loss bug.
///
/// On a developer profile dozens of agent folders junction to a single shared store
/// (e.g. ~/.gemini/config/skills). Both Directory.Delete(recursive:true) and
/// SearchOption.AllDirectories follow a junction and operate on the TARGET, so a
/// recursive delete of one "leftover" folder would have wiped the shared store for
/// every other folder linked to it.
///
/// These tests prove three invariants:
///   1. A reparse point is never accepted as a deletion target.
///   2. A directory tree that CONTAINS a junction is refused as a unit.
///   3. Measuring a tree does not descend through a junction (no phantom double-counting).
/// </summary>
public class JunctionSafetyTests : IDisposable
{
    private readonly string _sandboxDir;

    public JunctionSafetyTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), "Deltempo_Junction_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        try
        {
            // NEVER Directory.Delete(sandbox, recursive:true): the sandbox contains junctions
            // in these tests, and a recursive delete would follow them and destroy the
            // shared-store fixture. Strip links first, without ever descending into one.
            RemoveLinksOnly(_sandboxDir);

            if (Directory.Exists(_sandboxDir))
                Directory.Delete(_sandboxDir, recursive: true);
        }
        catch { /* temp folder; the OS will reap it */ }
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
                    // Delete the link itself, never its target.
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
        Path.Combine(new[] { _sandboxDir }.Concat(parts).ToArray());

    /// <summary>Creates a directory junction and fails the test if the platform refuses.</summary>
    private static void CreateJunction(string linkPath, string targetPath)
    {
        // mklink /J requires the link path to NOT exist yet; only its parent must.
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        Directory.CreateDirectory(targetPath);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.StandardOutput.ReadToEnd();
        proc.StandardError.ReadToEnd();
        proc.WaitForExit(15000);

        Assert.True(Directory.Exists(linkPath) &&
                    (File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0,
            $"Could not create test junction '{linkPath}' -> '{targetPath}' (exit {proc.ExitCode}).");
    }

    private static long WriteFile(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = new byte[bytes];
        // Arbitrary filler bytes for test data; not security material, so a seeded PRNG is appropriate.
        new Random(42).NextBytes(payload); // DevSkim: ignore DS148264
        File.WriteAllBytes(path, payload);
        return bytes;
    }

    private static LeftoverItem DirItem(string path) => new()
    {
        Type = LeftoverType.Directory,
        PathOrKey = path,
        Description = "test leftover"
    };

    // ------------------------------------------------------------------
    // Invariant 1: a reparse point is never a valid deletion target.
    // ------------------------------------------------------------------

    [Fact]
    public void IsSafeToDeleteResidual_JunctionPath_ReturnsFalse()
    {
        var link = P("agentFolder", "skills");
        var target = P("sharedStore");
        WriteFile(Path.Combine(target, "installed-skill.md"), 4096);
        CreateJunction(link, target);

        Assert.False(InstalledAppService.IsSafeToDeleteResidual(link));
    }

    [Fact]
    public void IsSafeToDeleteResidual_PlainLeftoverFolder_ReturnsTrue()
    {
        // Guards against the fix over-blocking: a normal orphan must still be deletable.
        var plain = P("OrphanApp");
        Directory.CreateDirectory(plain);

        Assert.True(InstalledAppService.IsSafeToDeleteResidual(plain));
    }

    // ------------------------------------------------------------------
    // Invariant 2: a tree CONTAINING a junction is refused as a unit,
    // and the shared store it points at survives untouched.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Purge_TreeContainingJunction_IsRefused_AndSharedStoreSurvives()
    {
        var target = P("sharedStore");
        long markerBytes = WriteFile(Path.Combine(target, "installed-skill.md"), 8192);

        var leftover = P("leftoverAgent");
        Directory.CreateDirectory(leftover);
        WriteFile(Path.Combine(leftover, "cache.bin"), 2048);
        CreateJunction(Path.Combine(leftover, "skills"), target);

        var res = await RootLeftoverPurgeService.PurgeLeftoversAsync(new[] { DirItem(leftover) });

        Assert.Equal(0, res.ItemsPurgedCount);
        Assert.True(res.ErrorsCount >= 1, "Expected the nested-junction refusal to be reported as an error.");
        Assert.Contains(res.ErrorMessages, m => m.Contains("junction", StringComparison.OrdinalIgnoreCase));

        // The leftover folder must still exist (refused, not partially deleted).
        Assert.True(Directory.Exists(leftover));

        // The shared store and its payload must be completely intact.
        Assert.True(Directory.Exists(target));
        var payload = Path.Combine(target, "installed-skill.md");
        Assert.True(File.Exists(payload));
        Assert.Equal(markerBytes, new FileInfo(payload).Length);
    }

    [Fact]
    public async Task Purge_JunctionNestedTwoLevelsDeep_IsRefused_AndSharedStoreSurvives()
    {
        // Proves FindNestedReparsePoints walks nested directories without descending into links.
        var target = P("sharedStore");
        WriteFile(Path.Combine(target, "skill-lock.json"), 1024);

        var leftover = P("leftoverAgent");
        var deep = Path.Combine(leftover, "a", "b");
        Directory.CreateDirectory(deep);
        WriteFile(Path.Combine(deep, "x.txt"), 512);
        CreateJunction(Path.Combine(deep, "skills"), target);

        var res = await RootLeftoverPurgeService.PurgeLeftoversAsync(new[] { DirItem(leftover) });

        Assert.Equal(0, res.ItemsPurgedCount);
        Assert.True(res.ErrorsCount >= 1);
        Assert.True(Directory.Exists(leftover));
        Assert.True(File.Exists(Path.Combine(target, "skill-lock.json")));
    }

    [Fact]
    public async Task Purge_JunctionAsRootTarget_IsRefused_AndSharedStoreSurvives()
    {
        // Guards the root-level check: the candidate path itself is a link.
        var target = P("sharedStore");
        WriteFile(Path.Combine(target, "installed-skill.md"), 2048);
        var link = P("linkedFolder");
        CreateJunction(link, target);

        var res = await RootLeftoverPurgeService.PurgeLeftoversAsync(new[] { DirItem(link) });

        Assert.Equal(0, res.ItemsPurgedCount);
        Assert.True(res.ErrorsCount >= 1);
        Assert.True(File.Exists(Path.Combine(target, "installed-skill.md")),
            "Shared store must survive when the candidate path is itself a junction.");
    }

    [Fact]
    public async Task Purge_JunctionCycle_CompletesWithoutHanging()
    {
        // FindNestedReparsePoints is iterative and never descends into a link, so a cycle
        // (leftover/loop -> leftover) must be detected immediately rather than looping.
        var leftover = P("leftoverAgent");
        Directory.CreateDirectory(leftover);
        CreateJunction(Path.Combine(leftover, "loop"), leftover);

        var purge = RootLeftoverPurgeService.PurgeLeftoversAsync(new[] { DirItem(leftover) });
        var winner = await Task.WhenAny(purge, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.True(ReferenceEquals(winner, purge),
            "Purge did not complete within 30s — junction cycle caused a hang.");

        var res = await purge;
        Assert.Equal(0, res.ItemsPurgedCount);
        Assert.True(Directory.Exists(leftover));
    }

    [Fact]
    public async Task Purge_SharedStoreReachedOnlyThroughJunction_LeavesTargetPayloadIntact()
    {
        // Worst real-world case: the store is reached solely through the junction, so a
        // recursive delete would erase the target's contents outright.
        var target = P("outsideStore");
        WriteFile(Path.Combine(target, "skill.md"), 4096);

        var leftover = P("leftoverAgent");
        Directory.CreateDirectory(leftover);
        CreateJunction(Path.Combine(leftover, "skills"), target);

        var res = await RootLeftoverPurgeService.PurgeLeftoversAsync(new[] { DirItem(leftover) });

        Assert.Equal(0, res.ItemsPurgedCount);
        Assert.True(File.Exists(Path.Combine(target, "skill.md")));
    }

    // ------------------------------------------------------------------
    // Invariant 3: sizing never descends through a junction, so one shared
    // store is not counted once per link.
    // ------------------------------------------------------------------

    [Fact]
    public void CalculateDirectorySizeSafe_DoesNotDescendIntoJunction()
    {
        var target = P("sharedStore");
        long storeBytes = WriteFile(Path.Combine(target, "installed-skill.md"), 64 * 1024);

        var leftover = P("leftoverAgent");
        Directory.CreateDirectory(leftover);
        long ownBytes = WriteFile(Path.Combine(leftover, "own-cache.bin"), 4096);
        CreateJunction(Path.Combine(leftover, "skills"), target);

        long measured = RootLeftoverScannerService.CalculateDirectorySizeSafe(leftover);

        // Only the folder's own bytes: the linked store contributes nothing.
        Assert.Equal(ownBytes, measured);
        Assert.True(measured < storeBytes,
            $"Measured {measured} bytes; the {storeBytes}-byte linked store must not be counted.");
        Assert.Equal(storeBytes, RootLeftoverScannerService.CalculateDirectorySizeSafe(target));
    }

    [Fact]
    public void CalculateDirectorySizeSafe_SameStoreLinkedFromManyFolders_CountedOnceAtRealPath()
    {
        var target = P("sharedStore");
        long storeBytes = WriteFile(Path.Combine(target, "s.bin"), 32 * 1024);

        // Model the real profile: N agent folders each junction the SAME store.
        for (int i = 0; i < 5; i++)
        {
            var agent = P($"agent{i}");
            Directory.CreateDirectory(agent);
            CreateJunction(Path.Combine(agent, "skills"), target);
        }

        // Every agent folder measures as empty (its only content is a link).
        for (int i = 0; i < 5; i++)
            Assert.Equal(0, RootLeftoverScannerService.CalculateDirectorySizeSafe(P($"agent{i}")));

        // The real store still reports its true size exactly once.
        Assert.Equal(storeBytes, RootLeftoverScannerService.CalculateDirectorySizeSafe(target));
    }

    // ------------------------------------------------------------------
    // Sanity: the guard must not break ordinary deletion of a link-free tree.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Purge_LinkFreeTree_DeletesNormallyAndCountsBytes()
    {
        var leftover = P("OrphanApp");
        long a = WriteFile(Path.Combine(leftover, "a.bin"), 4096);
        long b = WriteFile(Path.Combine(leftover, "sub", "b.bin"), 2048);

        var res = await RootLeftoverPurgeService.PurgeLeftoversAsync(new[] { DirItem(leftover) });

        Assert.Equal(1, res.ItemsPurgedCount);
        Assert.Equal(0, res.ErrorsCount);
        Assert.False(Directory.Exists(leftover));
        Assert.Equal(a + b, res.TotalReclaimedBytes);
    }
}
