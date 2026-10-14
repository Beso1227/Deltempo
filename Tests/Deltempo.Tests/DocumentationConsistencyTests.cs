using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the two documentation facts that silently rot and actively mislead:
/// the test command contributors and maintainers are told to run, and the test
/// count they are told the suite has.
///
/// <para>
/// The test command matters because xunit.v3 runs on Microsoft.Testing.Platform:
/// on the .NET 10 SDK <c>dotnet test</c> is rejected outright ("Testing with VSTest
/// target is no longer supported"). The release workflow shipped with the broken
/// invocation, so every tag push failed at "Run Tests" while CI - which invokes the
/// test executable directly - stayed green.
///
/// The count matters because it is duplicated across 9 READMEs, SECURITY.md and a
/// shields.io badge slug, all of which were stale by hundreds of tests.
/// </para>
/// </summary>
public class DocumentationConsistencyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Deltempo.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>
    /// Files that legitimately mention the broken command while explaining why it
    /// cannot be used, plus historical plan logs that record what was run at the time.
    /// </summary>
    private static readonly string[] AllowedDotnetTestMentions =
    {
        ".github/workflows/ci.yml",        // explains the MTP limitation
        ".github/workflows/release.yml",   // ditto
        ".github/PULL_REQUEST_TEMPLATE.md",// ditto
        "docs/TESTING.md",
        "docs/BENCHMARKS.md",
        "README.md",
        "scripts/benchmark.ps1",
        "scripts/run_tests_for_coverage.cmd",
        "docs/superpowers/",               // historical plan logs - a record, not an instruction
        "CONTRIBUTING.md",                 // explains why dotnet test cannot be used
    };

    [Fact]
    public void NoFile_TellsTheReaderToRunDotnetTest()
    {
        var root = RepoRoot();
        // A command line, not a prose mention: `dotnet test` at the start of a line
        // (optionally after a shell comment marker).
        var commandPattern = new Regex(@"^\s*(#\s*|rem\s+)?dotnet\s+test\b", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        var offenders = new List<string>();

        foreach (var path in EnumerateInstructionFiles(root))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (AllowedDotnetTestMentions.Any(a => relative.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (commandPattern.IsMatch(File.ReadAllText(path)))
                offenders.Add(relative);
        }

        Assert.True(offenders.Count == 0,
            "`dotnet test` cannot drive xunit.v3 on the .NET 10 SDK. These files still tell the " +
            "reader to run it: " + string.Join(", ", offenders) +
            ". Use: dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release, then run " +
            "Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe (see docs/TESTING.md).");
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData(".github/workflows/release.yml")]
    public void Workflow_ThatRunsTests_UsesTheMtpTestExecutable(string workflow)
    {
        // Guards the regression that mattered most: ci.yml was correct and release.yml
        // was not, so a release could fail while CI was green.
        var text = File.ReadAllText(Path.Combine(RepoRoot(), workflow));

        Assert.False(Regex.IsMatch(text, @"run:\s*dotnet\s+test"),
            $"{workflow} runs `dotnet test`, which cannot drive xunit.v3 on the .NET 10 SDK. " +
            "Invoke Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe instead.");
        Assert.True(text.Contains("Deltempo.Tests.exe"),
            $"{workflow} must invoke the MTP test executable directly.");
    }

    [Fact]
    public void Installer_TrustAnchor_IsNotADecorativeFallback()
    {
        // docs/checksums.sha256 was published once and never regenerated, so it could
        // only ever produce a hash mismatch. Either it is regenerated per release or
        // it must not exist and must not be referenced as a fallback.
        var root = RepoRoot();
        var installer = File.ReadAllText(Path.Combine(root, "docs", "win"));

        var staleManifestOnDisk = File.Exists(Path.Combine(root, "docs", "checksums.sha256"));
        Assert.False(staleManifestOnDisk,
            "docs/checksums.sha256 is stale and can only cause the installer to abort. " +
            "Regenerate it on every release or keep it deleted.");

        Assert.False(Regex.IsMatch(installer, @"\$pages\s*=\s*'https://beso1227\.github\.io"),
            "docs/win must not reference a Pages-hosted manifest that nothing regenerates.");
    }

    [Fact]
    public void PublishedTestCount_MatchesTheSuiteItself()
    {
        // The count is duplicated in 9 READMEs (badge slug + prose + table cell) and
        // SECURITY.md. A hardcoded badge rots silently, so pin it here instead.
        var root = RepoRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        var badge = Regex.Match(readme, @"tests-(\d+)_passed");
        Assert.True(badge.Success, "README.md is missing the shields.io test-count badge.");

        var declared = int.Parse(badge.Groups[1].Value);

        var prose = Regex.Match(readme, @"(\d+)\s+automated tests");
        Assert.True(prose.Success, "README.md no longer states the automated test count.");
        var proseCount = int.Parse(prose.Groups[1].Value);
        Assert.True(declared == proseCount,
            $"README.md badge says {declared} tests but the prose says {proseCount}.");

        foreach (var file in Directory.GetFiles(root, "README.*.md"))
        {
            var text = File.ReadAllText(file);
            var m = Regex.Match(text, @"tests-(\d+)_passed");
            if (m.Success)
                Assert.True(declared == int.Parse(m.Groups[1].Value),
                    $"{Path.GetFileName(file)} advertises {m.Groups[1].Value} tests; README.md says {declared}. " +
                    "Translations must stay in sync.");
        }
    }

    /// <summary>
    /// The badge is only one of several places the count is published. The AI-facing
    /// files matter most: llms.txt, llms-full.txt and ai-catalog.json are consumed by
    /// answer engines, so a stale count there is not a cosmetic bug - it is a false
    /// statement propagated to users who never see this repository. The public website
    /// carries the same claim on its comparison table and trust badges.
    ///
    /// Scoped deliberately: only files that make a CURRENT claim are checked. Historical
    /// per-release counts in the changelog and BENCHMARKS.md are a record of what was true
    /// at the time and must NOT be rewritten when the suite grows.
    /// </summary>
    [Theory]
    [InlineData("docs/llms.txt")]
    [InlineData("docs/llms-full.txt")]
    [InlineData("docs/ai-catalog.json")]
    [InlineData("SECURITY.md")]
    public void PublishedTestCount_IsConsistentAcrossAiFacingAndWebsiteFiles(string relativePath)
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        int declared = int.Parse(Regex.Match(readme, @"tests-(\d+)_passed").Groups[1].Value);

        // A current claim is a number adjacent to a test word. Allow the 995/995 form
        // and "N automated tests" / "tests: N" style prose; ignore anything else.
        var claims = Regex.Matches(text, @"(\d+)\s*(?:/\s*\d+\s*)?(?:[A-Za-z]+\s+){0,2}?tests\b", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .ToList();

        foreach (var claim in claims)
        {
            Assert.True(claim == declared,
                $"{relativePath} states {claim} tests but the published badge says {declared}. " +
                "AI-facing metadata is read by answer engines, so a stale count propagates as a fact. " +
                "Update it, or - if the number is a historical record of a past release - reword it so it " +
                "is not mistaken for a current claim.");
        }
    }

    [Fact]
    public void PublishedWebsite_TestCount_MatchesTheSuite()
    {
        // docs/index.html is the public homepage. It asserted "612 tests" for months
        // after the suite passed 900, on the page every visitor sees first.
        var root = RepoRoot();
        var html = File.ReadAllText(Path.Combine(root, "docs", "index.html"));

        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        int declared = int.Parse(Regex.Match(readme, @"tests-(\d+)_passed").Groups[1].Value);

        // Case-insensitive, and tolerant of an intervening adjective. The first version
        // of this guard only matched "N Tests" / "N Automated Tests" and happily missed
        // "612 Verified Tests", "612 Passing Tests" and "612 automated verification
        // tests" - all three of which were sitting on the live homepage.
        var claims = Regex.Matches(html, @"(\d+)\s+(?:[A-Za-z]+\s+){0,2}?tests\b", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .ToList();

        foreach (var claim in claims)
        {
            Assert.True(claim == declared,
                $"docs/index.html states {claim} tests but the published badge says {declared}. " +
                "The homepage is the most visible surface; it must not advertise a stale count.");
        }
    }

    /// <summary>
    /// Guards a claim that was false when this was written: `winget install
    /// Beso1227.Deltempo` appeared in ai-catalog.json, llms.txt and llms-full.txt while
    /// no such package existed (verified against api.winget.run, 0 results) and the
    /// manifest in packaging/winget/ still carried a placeholder
    /// `InstallerSha256: REPLACE_WITH_SHA256_...` at a stale PackageVersion. A
    /// non-existent install command in machine-readable metadata is a factual error
    /// that answer engines will repeat as fact.
    /// </summary>
    [Theory]
    [InlineData("docs/llms.txt")]
    [InlineData("docs/llms-full.txt")]
    [InlineData("docs/ai-catalog.json")]
    [InlineData("README.md")]
    public void NoFile_AdvertisesAnUnpublishedWingetPackage(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.False(Regex.IsMatch(text, @"\bwinget\s+install\s+Beso1227\.Deltempo", RegexOptions.IgnoreCase),
            $"{relativePath} tells the reader to run `winget install Beso1227.Deltempo`, but that package " +
            "is not published. Only advertise a winget command once the manifest is live in winget-pkgs " +
            "AND packaging/winget/*.yaml has a real InstallerSha256 and a matching PackageVersion.");
    }

    /// <summary>
    /// The release workflow writes the digest manifest as `checksums.sha256` and the
    /// one-line installer fetches exactly that name. RELEASES.md documented it as
    /// `SHA256SUMS.txt`, so a maintainer following the guide would look for a file that
    /// does not exist.
    /// </summary>
    [Fact]
    public void ReleaseDocs_NameTheChecksumManifestThatIsActuallyPublished()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "RELEASES.md"));

        Assert.False(text.Contains("SHA256SUMS", StringComparison.OrdinalIgnoreCase),
            "docs/RELEASES.md names the digest manifest SHA256SUMS.txt, but release.yml publishes it as " +
            "`checksums.sha256` and docs/win fetches exactly that name.");

        Assert.True(text.Contains("checksums.sha256", StringComparison.Ordinal),
            "docs/RELEASES.md must name `checksums.sha256`, the file that is actually published and consumed.");
    }

    /// <summary>
    /// Version is centralized in Directory.Build.props and mirrored into the README
    /// (all nine), BENCHMARKS.md, the PWA manifest and ai-catalog.json. ai-catalog.json
    /// shipped at 2.0.0 while the app was 3.1.0, because nothing checked it.
    /// </summary>
    [Fact]
    public void Version_IsConsistentAcrossBuildPropsAndPublishedMetadata()
    {
        var root = RepoRoot();

        string props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        var version = Regex.Match(props, @"<Version>([^<]+)</Version>");
        Assert.True(version.Success, "Directory.Build.props has no <Version> element.");
        string current = version.Groups[1].Value.Trim();

        // The PWA manifest caches-busts its start_url with the version, so it is a
        // real consumer of the number, not decoration.
        string manifest = File.ReadAllText(Path.Combine(root, "docs", "manifest.json"));
        Assert.True(manifest.Contains($"v={current}", StringComparison.Ordinal),
            $"docs/manifest.json does not reference v={current} (the version in Directory.Build.props). " +
            "A stale cache-buster means returning visitors keep the old build.");

        string catalog = File.ReadAllText(Path.Combine(root, "docs", "ai-catalog.json"));
        var catalogVersion = Regex.Match(catalog, "\"version\"\\s*:\\s*\"([^\"]+)\"");
        Assert.True(catalogVersion.Success, "docs/ai-catalog.json has no version field.");
        Assert.True(catalogVersion.Groups[1].Value == current,
            $"docs/ai-catalog.json advertises version {catalogVersion.Groups[1].Value} but the build is {current}.");

        foreach (var file in Directory.GetFiles(root, "README.*.md"))
        {
            var text = File.ReadAllText(file);
            var m = Regex.Match(text, @"v(\d+\.\d+\.\d+)\s*\(Production Release\)");
            if (!m.Success) continue;

            Assert.True(m.Groups[1].Value == current,
                $"{Path.GetFileName(file)} says v{m.Groups[1].Value} but Directory.Build.props says {current}.");
        }
    }

    /// <summary>
    /// The guard above only checks INTERNAL consistency - badge against prose against
    /// translations. It happily passed for months while the README advertised a count that had
    /// drifted from the real suite, which is exactly the rot its own name promises to prevent.
    ///
    /// This compares the published count against the suite as it actually exists in this
    /// assembly. Feasible because the suite uses no MemberData/ClassData/TheoryData: every case
    /// is a [Fact] or a [Theory] with inline rows, so the case count is derivable by reflection.
    /// If a dynamic data source is ever introduced, this must be revisited - it would
    /// undercount rather than fail loudly.
    ///
    /// Benchmarks are excluded because the published number describes the
    /// --filter "Category!=Benchmark" run, which is what CI executes.
    /// </summary>
    [Fact]
    public void PublishedTestCount_MatchesTheRealSuiteSize()
    {
        var root = RepoRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        var badge = Regex.Match(readme, @"tests-(\d+)_passed");
        Assert.True(badge.Success, "README.md is missing the shields.io test-count badge.");

        int published = int.Parse(badge.Groups[1].Value);
        int actual = CountRunnableTestCases(typeof(DocumentationConsistencyTests).Assembly);

        Assert.True(published == actual,
            $"README.md advertises {published} automated tests but the suite contains {actual}. " +
            "Update the badge slug and the prose in all READMEs plus SECURITY.md to the real count.");
    }

    /// <summary>
    /// Counts the cases the CI run executes: one per [Fact], and one per [InlineData] row for
    /// each [Theory] (a [Theory] with no inline rows is counted as a single case).
    /// </summary>
    private static int CountRunnableTestCases(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.DeclaredOnly;

        int total = 0;

        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                var attributes = method.GetCustomAttributes(inherit: true);

                bool isBenchmark = attributes
                    .OfType<TraitAttribute>()
                    .Any(t => t.Name == "Category" && t.Value == "Benchmark");

                if (isBenchmark) continue;

                bool isFact = attributes.Any(a => a is FactAttribute);
                bool isTheory = attributes.Any(a => a is TheoryAttribute);
                if (!isFact && !isTheory) continue;

                int rows = attributes.OfType<InlineDataAttribute>().Count();

                total += Math.Max(1, rows);
            }
        }

        return total;
    }

    private static IEnumerable<string> EnumerateInstructionFiles(string root)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".txt", ".yml", ".yaml", ".ps1", ".cmd" };
        var skip = new[] { "\\bin\\", "\\obj\\", "\\.git\\", "\\artifacts\\", "\\docs\\superpowers\\" };

        return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .Where(f => !skip.Any(s => f.Contains(s, StringComparison.OrdinalIgnoreCase)));
    }
}
