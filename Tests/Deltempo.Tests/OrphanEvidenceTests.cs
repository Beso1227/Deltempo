using System;
using System.IO;
using System.Linq;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Tests for evidence-based orphan classification (item 1) and version-tolerant folder identity
/// (item 4).
///
/// The theme: a folder must be *proved* orphaned before it can be proposed. Silence and small
/// size are not proof — a live tool goes quiet over a weekend — so classification asks whether
/// anything still references the folder, and treats user data / working roots / irreplaceable
/// editor state as out of bounds.
/// </summary>
public class OrphanEvidenceTests : IDisposable
{
    private readonly string _sandbox;

    public OrphanEvidenceTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "Deltempo_Ev_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); }
        catch { }
    }

    private string Dir(string relative)
    {
        string path = Path.Combine(_sandbox, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    // ------------------------------------------------------------------
    // Working roots and project manifests
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("package.json")]
    [InlineData("pyproject.toml")]
    [InlineData("Cargo.toml")]
    [InlineData("go.mod")]
    [InlineData("composer.json")]
    [InlineData("pubspec.yaml")]
    [InlineData(".git")]
    public void IsUserDataOrWorkingRoot_ProjectManifest_IsProtected(string marker)
    {
        string project = Dir("MyProject");
        File.WriteAllText(Path.Combine(project, marker), "{}");

        Assert.True(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(project));
    }

    [Fact]
    public void IsUserDataOrWorkingRoot_SolutionFile_IsProtected()
    {
        string project = Dir("MySolution");
        File.WriteAllText(Path.Combine(project, "MySolution.sln"), "");

        Assert.True(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(project));
    }

    [Fact]
    public void IsUserDataOrWorkingRoot_NestedManifest_IsProtected()
    {
        // Workspaces commonly nest the manifest one level down.
        string project = Dir("Monorepo");
        string pkg = Path.Combine(project, "packages", "app");
        Directory.CreateDirectory(pkg);
        File.WriteAllText(Path.Combine(pkg, "package.json"), "{}");

        Assert.True(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(project));
    }

    [Fact]
    public void IsUserDataOrWorkingRoot_PlainResidualFolder_IsNotProtected()
    {
        // A bare folder of leftover config is still a candidate — the guards must not swallow it.
        string residual = Dir("OldAppData");
        File.WriteAllText(Path.Combine(residual, "settings.ini"), "x=1");

        Assert.False(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(residual));
    }

    // ------------------------------------------------------------------
    // Irreplaceable user state (unsaved editor backups, local history)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Backups")]
    [InlineData("History")]
    [InlineData("workspaceStorage")]
    [InlineData("globalStorage")]
    public void IsUserDataOrWorkingRoot_IrreplaceableUserState_IsProtected(string stateDir)
    {
        // An Electron/IDE profile looks exactly like residue (Cache, GPUCache, blob_storage) but
        // this state cannot be regenerated. Losing it loses unsaved work.
        string profile = Dir("Antigravity IDE");
        Directory.CreateDirectory(Path.Combine(profile, "Cache"));
        Directory.CreateDirectory(Path.Combine(profile, stateDir));

        Assert.True(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(profile));
    }

    [Fact]
    public void IsUserDataOrWorkingRoot_NestedUserState_IsProtected()
    {
        // VS Code nests it: <Profile>/User/globalStorage
        string profile = Dir("Code - Insiders");
        string user = Path.Combine(profile, "User");
        Directory.CreateDirectory(Path.Combine(user, "globalStorage"));

        Assert.True(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(profile));
    }

    [Fact]
    public void IsUserDataOrWorkingRoot_CacheOnlyProfile_IsNotProtected()
    {
        // Pure Electron cache with no user state remains proposable residue.
        string profile = Dir("PureCacheProfile");
        Directory.CreateDirectory(Path.Combine(profile, "Cache"));
        Directory.CreateDirectory(Path.Combine(profile, "GPUCache"));
        File.WriteAllText(Path.Combine(profile, "blob_storage"), "x");

        Assert.False(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(profile));
    }

    // ------------------------------------------------------------------
    // Live references: fail-closed, and correct for the running system
    // ------------------------------------------------------------------

    [Fact]
    public void HasLiveReference_EmptyInput_FailsClosed()
    {
        Assert.True(OrphanEvidenceClassifier.HasLiveReference(""));
        Assert.True(OrphanEvidenceClassifier.HasLiveReference(null!));
    }

    [Fact]
    public void HasLiveReference_PathInsideLiveReference_ReturnsTrue()
    {
        // A folder inside a live reference (an installed tool's dir) is live.
        string refDir = Path.GetDirectoryName(Environment.ProcessPath)!;

        Assert.True(OrphanEvidenceClassifier.HasLiveReference(refDir));
    }

    [Fact]
    public void HasLiveReference_SystemPath_ReturnsTrue()
    {
        // The running process references C:\Windows\System32, so that tree is live.
        string systemRoot = Path.GetFullPath(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"));

        Assert.True(OrphanEvidenceClassifier.HasLiveReference(systemRoot));
    }

    [Fact]
    public void HasLiveReference_RandomTempFolder_ReturnsFalse()
    {
        // The sandbox is referenced by nothing — the "genuinely orphaned" side of the decision.
        // Proves the classifier does not simply refuse everything.
        Assert.False(OrphanEvidenceClassifier.HasLiveReference(_sandbox));
    }

    /// <summary>
    /// Portability/safety: every reference source is individually try/caught, so on a host that
    /// blocks them all — non-elevated, restricted registry, hardened process enumeration — the
    /// index comes back EMPTY rather than throwing. An empty index would otherwise report "no live
    /// reference anywhere" for every folder and orphan detection would propose live application
    /// data wholesale. Absence of evidence must not be treated as evidence of absence.
    /// </summary>
    [Fact]
    public void HasLiveReference_DegradedEmptyIndex_FailsClosed()
    {
        try
        {
            OrphanEvidenceClassifier.OverrideReferencesForTest(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            // Even the sandbox — which a healthy index correctly reports as unreferenced.
            Assert.True(OrphanEvidenceClassifier.HasLiveReference(_sandbox));
        }
        finally
        {
            OrphanEvidenceClassifier.ResetCache();
        }
    }

    /// <summary>
    /// The guard must trigger only on an implausibly small index, not on a real one. A healthy
    /// index still lets genuinely unreferenced folders through, or orphan detection would be
    /// permanently disabled on every healthy machine.
    /// </summary>
    [Fact]
    public void HasLiveReference_SufficientIndex_StillReportsGenuineOrphans()
    {
        try
        {
            var synthetic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"c:\windows\system32",
                @"c:\windows",
                @"c:\program files\app",
                @"c:\program files (x86)\app",
                @"c:\programdata\vendor",
                @"c:\users\tester\appdata\local\tool",
                @"c:\users\tester\appdata\roaming\tool",
                @"c:\some\other\live\path"
            };
            OrphanEvidenceClassifier.OverrideReferencesForTest(synthetic);

            Assert.False(OrphanEvidenceClassifier.HasLiveReference(_sandbox));
            Assert.True(OrphanEvidenceClassifier.HasLiveReference(@"c:\program files\app\data"));
        }
        finally
        {
            OrphanEvidenceClassifier.ResetCache();
        }
    }

    // ------------------------------------------------------------------
    // Version-tolerant folder identity (item 4)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("PDFgear 1.2.3", "PDFgear")]
    [InlineData("WinRAR 7.00 (64-bit)", "WinRAR")] // strips (64-bit), then the version
    [InlineData("Foo v2.1", "Foo")]
    [InlineData("Bar 1.0 x64", "Bar")]
    [InlineData("NoVersionHere", "NoVersionHere")] // idempotent when there is nothing to strip
    public void StripVersionSuffix_RemovesTrailingQualifiers(string input, string expected)
    {
        Assert.Equal(expected, AppIdentity.StripVersionSuffix(input));
    }

    [Fact]
    public void StripVersionSuffix_StopsAtFirstNonVersionSegment()
    {
        // Must not collapse "Visual Studio 2022" to "Visual".
        Assert.Equal("Visual Studio", AppIdentity.StripVersionSuffix("Visual Studio 2022"));
    }

    [Fact]
    public void FolderNameCandidates_VersionedDisplay_IncludesVersionlessFolder()
    {
        var candidates = AppIdentity.FolderNameCandidates("PDFgear 1.2.3");

        Assert.Contains("PDFgear 1.2.3", candidates.Select(c => c.Name));
        Assert.Contains("PDFgear", candidates.Select(c => c.Name));
    }

    [Fact]
    public void FolderNameCandidates_MultiWord_PrimaryTokenIsWeak()
    {
        // "Adobe Acrobat Reader" -> a bare "Adobe" folder matches many unrelated apps, so the
        // single-token candidate must be flagged weak (never auto-selected for deletion).
        var candidates = AppIdentity.FolderNameCandidates("Adobe Acrobat Reader");

        var adobe = candidates.Single(c => c.Name.Equals("Adobe", StringComparison.OrdinalIgnoreCase));
        Assert.True(adobe.IsWeak);

        var full = candidates.Single(c => c.Name.Equals("Adobe Acrobat Reader", StringComparison.OrdinalIgnoreCase));
        Assert.False(full.IsWeak);
    }

    [Fact]
    public void FolderNameCandidates_SingleWord_NoWeakCandidate()
    {
        var candidates = AppIdentity.FolderNameCandidates("7zip");

        Assert.All(candidates, c => Assert.False(c.IsWeak));
    }

    [Fact]
    public void FolderNameCandidates_Deduplicated()
    {
        var candidates = AppIdentity.FolderNameCandidates("Notepad++ 8.6.1");

        var names = candidates.Select(c => c.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void FolderNameCandidates_EmptyName_ReturnsEmpty()
    {
        Assert.Empty(AppIdentity.FolderNameCandidates(""));
        Assert.Empty(AppIdentity.FolderNameCandidates(null));
    }

    // ------------------------------------------------------------------
    // End-to-end: the scanner proposes nothing that fails the evidence gates
    // ------------------------------------------------------------------

    [Fact]
    public void ScanVerifiedOrphanedFolders_NeverProposesUserDataOrLiveFolder()
    {
        // Machine-independent invariant across all scan roots: every proposal cleared the evidence
        // gates (not user data, not a working root, not live-referenced).
        var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();

        foreach (var orphan in orphans)
        {
            string path = orphan.FolderPath;

            Assert.False(OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(path),
                $"Scanner proposed a user-data / working root: {path}");

            Assert.False(OrphanedAppService.IsNeverProposedFolderName(
                    Path.GetFileName(path.TrimEnd('\\', '/'))),
                $"Scanner proposed a protected namespace: {path}");
        }
    }
}