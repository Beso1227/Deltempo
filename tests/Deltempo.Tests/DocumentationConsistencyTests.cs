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

        // Categories that are run by a scheduled job rather than by the pull-request gate.
        // A test excluded here must NOT be advertised in the published count, otherwise the
        // badge would promise coverage that an ordinary pull request never exercises.
        var gateExcludedCategories = new[] { "Benchmark", "FuzzDeep" };

        int total = 0;

        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                var attributes = method.GetCustomAttributes(inherit: true);

                bool isGateExcluded = attributes
                    .OfType<TraitAttribute>()
                    .Any(t => t.Name == "Category" && gateExcludedCategories.Contains(t.Value, StringComparer.Ordinal));

                if (isGateExcluded) continue;

                bool isFact = attributes.Any(a => a is FactAttribute);
                bool isTheory = attributes.Any(a => a is TheoryAttribute);
                if (!isFact && !isTheory) continue;

                int rows = attributes.OfType<InlineDataAttribute>().Count();

                total += Math.Max(1, rows);
            }
        }

        return total;
    }

    /// <summary>
    /// A Code of Conduct with no way to report a violation is not a policy, it is
    /// decoration. Contributor Covenant 2.1 requires the Enforcement section to name
    /// a contact method; the shipped file said only "may be reported to the project
    /// maintainers", which names nobody and specifies no channel.
    /// </summary>
    [Fact]
    public void CodeOfConduct_NamesAnActualReportingChannel()
    {
        var coc = File.ReadAllText(Path.Combine(RepoRoot(), "CODE_OF_CONDUCT.md"));

        Assert.True(Regex.IsMatch(coc, @"##\s+Enforcement"),
            "CODE_OF_CONDUCT.md has no Enforcement section.");
        Assert.True(Regex.IsMatch(coc, @"advisories/new|report-abuse|@[A-Za-z0-9._-]+\.[A-Za-z]{2,}"),
            "CODE_OF_CONDUCT.md names no reporting channel. Contributor Covenant 2.1 requires an "
            + "explicit contact method in the Enforcement section; without one the policy is unenforceable.");
        Assert.False(Regex.IsMatch(coc, @"\[INSERT CONTACT"),
            "CODE_OF_CONDUCT.md still contains the Contributor Covenant [INSERT CONTACT METHOD] placeholder.");
    }

    /// <summary>
    /// The CoC was not linked from the README, CONTRIBUTING or SECURITY, so the only
    /// way to find it was to know it existed.
    /// </summary>
    [Fact]
    public void CodeOfConduct_IsLinkedFromEveryPlaceItGoverns()
    {
        var root = RepoRoot();
        foreach (var file in new[] { "README.md", "CONTRIBUTING.md", "SECURITY.md" })
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.True(text.Contains("CODE_OF_CONDUCT.md", StringComparison.Ordinal),
                $"{file} does not link CODE_OF_CONDUCT.md. A code of conduct that is not linked " +
                "is one nobody reads.");
        }
    }

    /// <summary>
    /// SECURITY.md shipped a supported-versions table listing 1.5.x/1.4.x/1.3.x while
    /// the application was at 3.x - so the security policy declared only end-of-life
    /// lines as supported and omitted every current release.
    /// </summary>
    [Fact]
    public void SecurityPolicy_SupportedVersions_IncludeTheCurrentMajor()
    {
        var root = RepoRoot();
        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        var version = Regex.Match(props, @"<Version>([^<]+)</Version>");
        Assert.True(version.Success, "Directory.Build.props has no <Version> element.");
        string major = version.Groups[1].Value.Split('.')[0];

        var security = File.ReadAllText(Path.Combine(root, "SECURITY.md"));
        var table = Regex.Match(security, @"##\s+Supported Versions(?<body>.*?)(?=\n##\s|\z)", RegexOptions.Singleline);
        Assert.True(table.Success, "SECURITY.md has no Supported Versions section.");

        // Match any version cell beginning with the current major, e.g. "3.2.x" or
        // "3.x" - not literally "<major>.x", which a per-minor table never contains.
        var mentionsCurrentMajor = Regex.IsMatch(table.Groups["body"].Value, $@"\|[\s:]*v?{Regex.Escape(major)}(\.\d+)?\.x?\b");
        Assert.True(mentionsCurrentMajor,
            $"SECURITY.md's supported-versions table does not mention the current major line {major}. " +
            "A security policy that lists only EOL versions is actively misleading.");
    }

    /// <summary>
    /// SECURITY.md claimed updates were "verified end-to-end ... and ECDSA P-256
    /// manifest signatures". The signature verifier is not wired into the live update
    /// path and its embedded key is a placeholder, so this was a false security claim
    /// on the one document a security researcher is most likely to read literally.
    /// </summary>
    [Fact]
    public void SecurityDocs_DoNotClaimUnenforcedManifestSignatureVerification()
    {
        var root = RepoRoot();

        foreach (var file in new[] { "SECURITY.md", ".github/PULL_REQUEST_TEMPLATE.md" })
        {
            var text = File.ReadAllText(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));

            // A bare "verified ... and manifest signature" claim. Negated or
            // explicitly-caveated mentions ("not yet enforced") are fine and are
            // exactly what we want, so require the claim to appear un-caveated.
            var claims = Regex.Matches(text,
                @"verified[^.\n]{0,120}\bmanifest signature",
                RegexOptions.IgnoreCase);

            foreach (Match claim in claims)
            {
                // Look for a negation within the same sentence.
                var sentence = claim.Value;
                Assert.False(Regex.IsMatch(sentence, @"not\b|never\b|no[nt]-?yet|placeholder|\*\*What is"),
                    $"{file} claims manifest-signature verification is part of the enforced chain: " +
                    $"\"{sentence.Trim()}\". ManifestSignatureVerifier is not called from the live " +
                    "update path and its embedded key is a placeholder - see docs/THREAT_MODEL.md " +
                    "section 2.5. State it as a known gap, not a control.");
            }
        }
    }

    /// <summary>
    /// The issue templates labelled with "bug", "triage" and "enhancement" while the
    /// repo declared a `type/*` taxonomy that had never been applied. "triage" was
    /// defined nowhere at all. The templates now match the labels that actually exist.
    /// </summary>
    [Fact]
    public void IssueTemplates_ApplyLabelsThatAreActuallyDeclared()
    {
        var root = RepoRoot();
        var settings = File.ReadAllText(Path.Combine(root, ".github", "settings.yml"));

        var declared = Regex.Matches(settings, @"- name: ""([^""]+)""")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(declared.Count > 0, "No labels declared in .github/settings.yml.");

        var templateDir = Path.Combine(root, ".github", "ISSUE_TEMPLATE");
        var offenders = new List<string>();

        foreach (var file in Directory.GetFiles(templateDir, "*.yml"))
        {
            var labels = Regex.Match(File.ReadAllText(file), @"^labels:\s*\[(.*)\]", RegexOptions.Multiline);
            if (!labels.Success) continue;

            foreach (var raw in labels.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var name = raw.Trim('"', '\'', ' ');
                if (!declared.Contains(name))
                    offenders.Add($"{Path.GetFileName(file)} applies \"{name}\", which .github/settings.yml does not declare");
            }
        }

        Assert.True(offenders.Count == 0,
            "Issue templates apply labels that the repository does not declare: " +
            string.Join("; ", offenders) +
            ". GitHub silently creates undeclared labels, so this produces labels outside the " +
            "project's own taxonomy. Either declare them or stop applying them.");
    }

    /// <summary>
    /// Three different copyright wordings shipped across LICENSE, README.md and
    /// Directory.Build.props. LICENSE is the legal instrument, so it is canonical;
    /// the other two must match it.
    /// </summary>
    [Fact]
    public void CopyrightHolder_MatchesTheLicenseEverywhere()
    {
        var root = RepoRoot();

        var license = Regex.Match(File.ReadAllText(Path.Combine(root, "LICENSE")),
            @"Copyright \(c\) (?<year>\d{4}) (?<holder>.+?)\r?\n");
        Assert.True(license.Success, "LICENSE has no recognizable copyright line.");

        string year = license.Groups["year"].Value;
        string holder = license.Groups["holder"].Value.Trim();

        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        // The year is captured separately; the "rest" group must be the HOLDER, not
        // "year holder". A first attempt made this capture the year too and then
        // compared "2026 Deltempo Contributors..." against "Deltempo Contributors",
        // which fails on correct input.
        var propsCopyright = Regex.Match(props, @"<Copyright>Copyright \(c\) (?<year>\d{4}) (?<holder>[^<]+)</Copyright>");
        Assert.True(propsCopyright.Success, "Directory.Build.props has no <Copyright> element.");
        Assert.True(propsCopyright.Groups["holder"].Value.Trim().StartsWith(holder, StringComparison.Ordinal),
            $"Directory.Build.props says \"{propsCopyright.Groups["holder"].Value.Trim()}\" but LICENSE says " +
            $"\"{year} {holder}\". LICENSE is the legal instrument and is canonical.");
        Assert.True(propsCopyright.Groups["year"].Value == year,
            $"Directory.Build.props copyright year is {propsCopyright.Groups["year"].Value} but LICENSE says {year}.");

        foreach (var file in Directory.GetFiles(root, "README.*.md"))
        {
            var text = File.ReadAllText(file);
            var m = Regex.Match(text, @"Copyright \(c\) (?<year>\d{4}) (?<holder>.+?)\r?\n");
            if (!m.Success) continue;

            Assert.True(m.Groups["holder"].Value.Trim() == holder && m.Groups["year"].Value == year,
                $"{Path.GetFileName(file)} says \"Copyright (c) {m.Groups["year"].Value} {m.Groups["holder"].Value.Trim()}\" " +
                $"but LICENSE says \"{year} {holder}\".");
        }
    }

    /// <summary>
    /// The changelog's CURRENT release card is a live claim, not a historical record:
    /// it is the entry a visitor reads first and the one that describes code that
    /// exists right now. Older cards are history and must keep their original numbers.
    ///
    /// Scoped deliberately to the first release card only, because the file also
    /// contains 909 / 692 / 501 / 480 for earlier versions. Rewriting those would
    /// falsify the record.
    /// </summary>
    [Fact]
    public void Changelog_CurrentReleaseCard_MatchesThePublishedTestCount()
    {
        var root = RepoRoot();
        var html = File.ReadAllText(Path.Combine(root, "docs", "changelog", "index.html"));

        // The current card is the first release-version heading, up to the next card.
        var currentCard = Regex.Match(html,
            @"<h2 class=""release-version"">v(?<ver>[\d.]+)[^<]*</h2>(?<body>.*?)(?=<h2 class=""release-version""|\z)",
            RegexOptions.Singleline);
        Assert.True(currentCard.Success, "docs/changelog/index.html has no release-version heading.");

        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        int declared = int.Parse(Regex.Match(readme, @"tests-(\d+)_passed").Groups[1].Value);

        var claims = Regex.Matches(currentCard.Groups["body"].Value, @"(\d+)\s+(?:[A-Za-z]+\s+){0,2}?tests\b", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .ToList();

        foreach (var claim in claims)
        {
            Assert.True(claim == declared,
                $"The current changelog release card (v{currentCard.Groups["ver"].Value}) states {claim} tests " +
                $"but the published badge says {declared}. The current card describes code that exists now and " +
                "must track the suite; only the OLDER cards keep their historical numbers.");
        }
    }

    /// <summary>
    /// The prose-claim regex used by the guards above looks for a number ADJACENT to
    /// the word "tests". The structured field reads <c>"testCount": 1089</c> - the
    /// number follows the word and nothing test-shaped follows the number - so the
    /// regex matches nothing there and the field silently rotted to 1018 while the
    /// badge had moved past 1055. Answer engines read ai-catalog.json as structured
    /// metadata, so the field needs its own assertion.
    /// </summary>
    [Fact]
    public void PublishedAiCatalog_TestCountField_MatchesThePublishedBadge()
    {
        var root = RepoRoot();
        var catalog = File.ReadAllText(Path.Combine(root, "docs", "ai-catalog.json"));

        var field = Regex.Match(catalog, "\"testCount\"\\s*:\\s*(\\d+)");
        Assert.True(field.Success, "docs/ai-catalog.json has no testCount field.");

        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        int declared = int.Parse(Regex.Match(readme, @"tests-(\d+)_passed").Groups[1].Value);

        Assert.True(int.Parse(field.Groups[1].Value) == declared,
            $"docs/ai-catalog.json testCount is {field.Groups[1].Value} but the published badge says {declared}. " +
            "The field is machine-read metadata; sweep it together with the badge, prose and translations.");
    }

    /// <summary>
    /// The sitemap is how search engines learn a static page exists. A page published
    /// without an entry is invisible to crawlers; an entry pointing at a missing page
    /// is a 404 that Search Console reports against the whole sitemap. Checked in both
    /// directions, because both rot paths have happened on this site.
    /// </summary>
    [Fact]
    public void Sitemap_LinksEveryPublishedPageAndOnlyExistingOnes()
    {
        var root = RepoRoot();
        var xml = File.ReadAllText(Path.Combine(root, "docs", "sitemap.xml"));

        const string prefix = "https://beso1227.github.io/Deltempo/";

        var locations = Regex.Matches(xml, @"<loc>([^<]+)</loc>")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();
        Assert.True(locations.Count > 0, "docs/sitemap.xml has no <loc> entries.");

        foreach (var loc in locations)
        {
            Assert.True(loc.StartsWith(prefix, StringComparison.Ordinal),
                $"docs/sitemap.xml entry {loc} is not under {prefix}.");

            var relative = loc[prefix.Length..].Trim('/');
            var file = string.IsNullOrEmpty(relative)
                ? Path.Combine(root, "docs", "index.html")
                : Path.Combine(root, "docs", relative.Replace('/', Path.DirectorySeparatorChar), "index.html");

            Assert.True(File.Exists(file),
                $"docs/sitemap.xml lists {loc} but {Path.GetRelativePath(root, file).Replace('\\', '/')} does not exist.");
        }

        var listed = locations
            .Select(loc => loc[prefix.Length..].Trim('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in Directory.GetDirectories(Path.Combine(root, "docs")))
        {
            if (!File.Exists(Path.Combine(dir, "index.html"))) continue;

            var page = Path.GetRelativePath(root, dir).Replace('\\', '/')[("docs/".Length)..];
            Assert.True(listed.Contains(page),
                $"docs/{page}/index.html is published but missing from docs/sitemap.xml, so crawlers cannot discover it.");
        }
    }

    /// <summary>
    /// The SEO head (canonical, description, JSON-LD) lives in static HTML that no
    /// compiler and no test ever rendered, so it rots silently. The JSON-LD half is
    /// the dangerous one: every consumer DROPS a malformed block without an error, so
    /// a single typo deletes a page's structured data and nothing anywhere complains.
    /// </summary>
    [Fact]
    public void EveryPublishedPage_HasSeoHeadAndParseableJsonLd()
    {
        var root = RepoRoot();

        var pages = new List<string> { Path.Combine(root, "docs", "index.html") };
        pages.AddRange(Directory.GetDirectories(Path.Combine(root, "docs"))
            .Where(d => File.Exists(Path.Combine(d, "index.html")))
            .Select(d => Path.Combine(d, "index.html")));

        foreach (var page in pages)
        {
            var relative = Path.GetRelativePath(root, page).Replace('\\', '/');
            var html = File.ReadAllText(page);

            Assert.True(Regex.IsMatch(html, @"<link rel=""canonical"" href=""https://beso1227\.github\.io/"),
                $"{relative} has no canonical link pointing at the published site.");
            Assert.True(Regex.IsMatch(html, @"<meta name=""description"" content=""[^""]+"""),
                $"{relative} has no meta description.");
            Assert.True(Regex.IsMatch(html, @"<title>[^<]+</title>"),
                $"{relative} has no <title>.");

            var blocks = Regex.Matches(html, @"<script type=""application/ld\+json"">(.*?)</script>", RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();
            Assert.True(blocks.Count > 0, $"{relative} has no JSON-LD block.");

            foreach (var block in blocks)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(block);
                    _ = doc.RootElement;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    Assert.Fail($"{relative} contains a malformed JSON-LD block ({ex.Message}). " +
                        "Consumers drop invalid JSON-LD silently, so the page would lose its structured " +
                        "data with no error anywhere else.");
                }
            }
        }
    }

    /// <summary>
    /// llms.txt is the file an AI crawler reads FIRST (llmstxt.org convention). A page
    /// missing from it exists for search engines but not for answer engines - exactly
    /// the state /compare/ and /alternatives/ shipped in until this guard existed.
    /// The sitemap is the source of truth for what counts as published.
    /// </summary>
    [Fact]
    public void LlmsTxt_LinksEveryPublishedPage()
    {
        var root = RepoRoot();
        var xml = File.ReadAllText(Path.Combine(root, "docs", "sitemap.xml"));
        var llms = File.ReadAllText(Path.Combine(root, "docs", "llms.txt"));

        var missing = Regex.Matches(xml, @"<loc>([^<]+)</loc>")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(loc => !llms.Contains(loc, StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            "docs/llms.txt does not link: " + string.Join(", ", missing) +
            ". llms.txt is the AI-facing entry point; every published page must be reachable from it.");
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
