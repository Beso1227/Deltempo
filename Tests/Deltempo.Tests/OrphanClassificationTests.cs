using System;
using System.IO;
using System.Linq;
using WinTempCleaner.Models;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Regression tests for orphan-classification false positives.
///
/// Three layers are proven here:
/// 1. Never-proposed namespaces (package-manager stores, agent skills/plugins/MCP) are refused
///    by name alone — age/size shields must never be able to override them.
/// 2. Folder identity matching is direction-agnostic: folder "Google" matches identity
///    "Google Chrome" and folder "PDFgear" matches "PDFgear 1.2.3", so versioned DisplayNames
///    cannot masquerade as orphans while short generic names still fail closed.
/// 3. The residual safety shield rejects namespace paths end-to-end, regardless of which scan
///    surfaced them.
/// </summary>
public class OrphanClassificationTests
{
    // ------------------------------------------------------------------
    // 1. Never-proposed namespaces
    // ------------------------------------------------------------------

    [Theory]
    // Package-manager stores & toolchains
    [InlineData("npm-cache")]
    [InlineData("node_modules")]
    [InlineData("node-gyp")]
    [InlineData("NuGet")]
    [InlineData("pip")]
    [InlineData("uv")]
    [InlineData("scoop")]
    [InlineData("cargo")]
    [InlineData("ms-playwright")]
    [InlineData("kotlin")]
    // Skill / plugin / MCP / agent vocabulary
    [InlineData("skills")]
    [InlineData("agents")]
    [InlineData("plugins")]
    [InlineData("commands")]
    [InlineData("mcp")]
    // Hidden tool config and npm scopes
    [InlineData(".claude")]
    [InlineData(".gemini")]
    [InlineData(".codex")]
    [InlineData("@modelcontextprotocol")]
    // Installer suffix conventions
    [InlineData("obsidian-updater")]
    [InlineData("chrome-devtools-mcp")]
    [InlineData("someportable-cli")]
    [InlineData("vscode-sqltools-extension")]
    [InlineData("vendor-skills")]
    // Agent namespace prefixes
    [InlineData("claude-cli-nodejs")]
    [InlineData("gemini-foo")]
    [InlineData("codex-runtimes")]
    [InlineData("copilot-cli")]
    [InlineData("opencode-aidesktop")]
    public void IsNeverProposedFolderName_ProtectedNamespaces_ReturnsTrue(string dirName)
    {
        Assert.True(OrphanedAppService.IsNeverProposedFolderName(dirName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ContosoApp")]
    [InlineData("Google")]
    [InlineData("PDFgear")]
    [InlineData("DaysGone")]
    [InlineData("Devolutions")]
    [InlineData("SomeUpdater")] // contains "Updater" but no -updater suffix
    [InlineData("mcphee")]      // contains "mcp" only as a substring, no -mcp suffix
    public void IsNeverProposedFolderName_OrdinaryAppFolders_ReturnsFalse(string? dirName)
    {
        Assert.False(OrphanedAppService.IsNeverProposedFolderName(dirName));
    }

    // ------------------------------------------------------------------
    // 2. Direction-agnostic identity matching
    // ------------------------------------------------------------------

    [Fact]
    public void IsActiveDirectory_VersionedDisplayName_MatchesShortFolderName()
    {
        // The P1 bug: folder "PDFgear" must match registry DisplayName "PDFgear 1.2.3".
        var keywords = new[] { "PDFgear 1.2.3", "WinRAR archiver" };

        Assert.True(OrphanedAppService.IsActiveDirectory("PDFgear", keywords));
        Assert.True(OrphanedAppService.IsActiveDirectory("WinRAR", keywords));
    }

    [Fact]
    public void IsActiveDirectory_ProductDisplayName_MatchesVendorFolder()
    {
        // Folder "Google" (Chrome's data root) must match identity "Google Chrome".
        var keywords = new[] { "Google Chrome", "Microsoft Visual Studio 2022" };

        Assert.True(OrphanedAppService.IsActiveDirectory("Google", keywords));
        Assert.True(OrphanedAppService.IsActiveDirectory("Visual Studio", keywords));
    }

    /// <summary>
    /// Regression: the active-app keyword set is populated from running process names, so a
    /// generic process such as "tool" previously marked every folder merely CONTAINING that
    /// text as an active install. On a machine with such a process, the genuine orphan
    /// "MiniTool GA Uploader" matched on the substring "Tool" and the orphan scanner proposed
    /// nothing from any scan root. Containment must respect word boundaries.
    /// </summary>
    [Fact]
    public void IsActiveDirectory_GenericKeyword_DoesNotMatchInsideAnotherWord()
    {
        var keywords = new[] { "tool", "Setup", "Snipping Tool", "Adobe" };

        // Infix occurrences must not match: an unrelated word that happens to end in the keyword.
        Assert.False(OrphanedAppService.IsActiveDirectory("MiniTool GA Uploader", keywords));
        Assert.False(OrphanedAppService.IsActiveDirectory("MySetupFiles", keywords));

        // Genuine word-boundary matches must still be recognised.
        Assert.True(OrphanedAppService.IsActiveDirectory("Snipping Tool", keywords));
        Assert.True(OrphanedAppService.IsActiveDirectory("Adobe", keywords));
        Assert.True(OrphanedAppService.IsActiveDirectory("Visual Studio 2022", new[] { "Visual Studio" }));
    }

    /// <summary>
    /// Portability: the keyword set is derived from whatever happens to be installed and running
    /// on the *current* machine. On a clean or minimal Windows install it is tiny or empty, so the
    /// matcher must degrade to "not active" rather than treating every folder as a live install.
    /// A machine-dependent matcher would silently disable orphan detection on someone else's PC.
    /// </summary>
    [Fact]
    public void IsActiveDirectory_MinimalOrEmptyKeywordSet_DoesNotSuppressProposals()
    {
        Assert.False(OrphanedAppService.IsActiveDirectory("SomeOldApp", Array.Empty<string>()));
        Assert.False(OrphanedAppService.IsActiveDirectory("SomeOldApp", new[] { "explorer", "svchost" }));

        // Guard against a null collection from a caller that failed to gather evidence.
        Assert.False(OrphanedAppService.IsActiveDirectory("SomeOldApp", null!));
    }

    /// <summary>
    /// Portability: word-boundary detection must work on non-ASCII folder names, which are routine
    /// on localized Windows installs. Combining marks and accented letters are letters, so
    /// "ÜberPhoto" is one word and must not match the keyword "Photo".
    /// </summary>
    [Fact]
    public void IsActiveDirectory_HandlesNonAsciiFolderNames()
    {
        var keywords = new[] { "Werkzeug", "Photo" };

        // Exact and leading-boundary matches still count.
        Assert.True(OrphanedAppService.IsActiveDirectory("Photo", keywords));
        Assert.True(OrphanedAppService.IsActiveDirectory("Photo Werkzeug", keywords));
        Assert.True(OrphanedAppService.IsActiveDirectory("Werkzeug", keywords));

        // Infix inside an accented word must not match.
        Assert.False(OrphanedAppService.IsActiveDirectory("ÜberPhoto", keywords));
    }

    /// <summary>
    /// Guards the MinimumPlausibleActiveAppCount threshold itself. If this ever fails on a real
    /// Windows host, orphan detection has been silently switched off there — the threshold is too
    /// high for that environment, not the environment genuinely empty. Running it in CI means a
    /// minimal image (windows-latest) proves the floor is safe rather than assuming it.
    /// </summary>
    [Fact]
    public void GetComprehensiveActiveAppKeywords_HarvestIsHealthyOnAnyRealWindowsHost()
    {
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        Assert.True(
            keywords.Count >= 32,
            $"Only {keywords.Count} active-app keywords were gathered; the orphan scan would be " +
            "disabled here, so the degraded-evidence threshold is too high for this environment.");
    }

    [Fact]
    public void IsActiveDirectory_ShortOrGenericNames_FailClosed()
    {
        // Reverse containment is bounded to 4+ chars so tiny folder names cannot
        // match every keyword (e.g. folder "Go" must not match "Google Chrome").
        var keywords = new[] { "Google Chrome" };

        Assert.False(OrphanedAppService.IsActiveDirectory("Go", keywords));
        Assert.False(OrphanedAppService.IsActiveDirectory("xyz-unrelated", keywords));
    }

    [Fact]
    public void IsActiveDirectory_ExactMatch_WinsRegardlessOfLength()
    {
        Assert.True(OrphanedAppService.IsActiveDirectory("Git", new[] { "Git" }));
    }

    [Fact]
    public void IsActiveDirectory_KeywordTooShortForContainment_MatchesOnlyExactly()
    {
        // Keywords under 4 chars may still equal, but must not contain-match a long folder.
        Assert.False(OrphanedAppService.IsActiveDirectory("GolangTool", new[] { "Go" }));
        Assert.True(OrphanedAppService.IsActiveDirectory("Go", new[] { "Go" }));
    }

    [Fact]
    public void IsActiveDirectory_EmptyInputs_ReturnFalse()
    {
        Assert.False(OrphanedAppService.IsActiveDirectory("", new[] { "App" }));
        Assert.False(OrphanedAppService.IsActiveDirectory(null!, new[] { "App" }));
        Assert.False(OrphanedAppService.IsActiveDirectory("Folder", Array.Empty<string>()));
        Assert.False(OrphanedAppService.IsActiveDirectory("Folder", null!));
    }

    [Fact]
    public void IsActiveDirectory_RealKeywordSet_MatchesVersionedRegistryApp()
    {
        // End-to-end against the live keyword builder: an installed app whose DisplayName
        // carries a version must still mark its versionless folder name as active.
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();
        Assert.NotEmpty(keywords);

        // Pick any real versioned identity present on this machine and prove the versionless
        // token matches. Fresh machines without one still pass; synthetic cases above cover
        // the behavior deterministically.
        var versioned = keywords.FirstOrDefault(k =>
            k.Length > 6 && char.IsDigit(k[k.Length - 1]) && k.Contains(' '));
        if (versioned != null)
        {
            string shortestToken = versioned
                .Split(' ', '-', '_', '.')
                .Where(t => t.Length >= 4)
                .OrderBy(t => t.Length)
                .FirstOrDefault() ?? versioned;

            Assert.True(OrphanedAppService.IsActiveDirectory(shortestToken, keywords),
                $"Folder '{shortestToken}' should match installed identity '{versioned}'.");
        }
    }

    // ------------------------------------------------------------------
    // 3. Residual safety shield end-to-end
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("skills")]
    [InlineData("npm-cache")]
    [InlineData("node_modules")]
    [InlineData(".claude")]
    [InlineData("@anthropic")]
    [InlineData("obsidian-updater")]
    public void IsSafeToDeleteResidual_NamespacePaths_ReturnsFalse(string dirName)
    {
        string path = Path.Combine(Path.GetTempPath(), "Deltempo_NsTest", dirName);

        Assert.False(InstalledAppService.IsSafeToDeleteResidual(path));
    }

    [Fact]
    public void IsSafeToDeleteResidual_OrdinaryFolderUnderTemp_ReturnsTrue()
    {
        // Guards against the new namespace rule over-blocking ordinary folders.
        string path = Path.Combine(Path.GetTempPath(), "Deltempo_NsTest", "ContosoApp");

        Assert.True(InstalledAppService.IsSafeToDeleteResidual(path));
    }

    // ------------------------------------------------------------------
    // 4. Scanner-level invariant: never propose a protected namespace
    // ------------------------------------------------------------------

    [Fact]
    public void ScanVerifiedOrphanedFolders_NeverProposesProtectedNamespace()
    {
        // Machine-independent invariant: whatever this machine reports, no proposal may
        // name a never-proposed namespace and none may be a reparse point.
        var orphans = OrphanedAppService.ScanVerifiedOrphanedFolders();

        foreach (var orphan in orphans)
        {
            string name = Path.GetFileName(orphan.FolderPath.TrimEnd('\\', '/'));

            Assert.False(OrphanedAppService.IsNeverProposedFolderName(name),
                $"Scanner proposed protected namespace: {orphan.FolderPath}");

            if (Directory.Exists(orphan.FolderPath))
            {
                Assert.False(
                    (File.GetAttributes(orphan.FolderPath) & FileAttributes.ReparsePoint) != 0,
                    $"Scanner proposed a reparse point: {orphan.FolderPath}");
            }
        }
    }

    [Fact]
    public void GetComprehensiveActiveAppKeywords_IncludesPackageManagedIdentities()
    {
        // npm globals, winget links, and PATH executables must contribute identities —
        // registry absence is not evidence of uninstall for package-managed tools.
        var keywords = OrphanedAppService.GetComprehensiveActiveAppKeywords();

        string npmBin = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");

        if (Directory.Exists(npmBin))
        {
            var shims = Directory.GetFiles(npmBin, "*.cmd")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            Assert.NotEmpty(shims);
            Assert.All(shims, shim => Assert.Contains(shim, keywords));
        }
        else
        {
            // No npm on this machine: at minimum PATH-derived identities must exist.
            Assert.NotEmpty(keywords);
        }
    }
}

