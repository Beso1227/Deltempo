using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private static IEnumerable<string> EnumerateInstructionFiles(string root)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".txt", ".yml", ".yaml", ".ps1", ".cmd" };
        var skip = new[] { "\\bin\\", "\\obj\\", "\\.git\\", "\\artifacts\\", "\\docs\\superpowers\\" };

        return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .Where(f => !skip.Any(s => f.Contains(s, StringComparison.OrdinalIgnoreCase)));
    }
}
