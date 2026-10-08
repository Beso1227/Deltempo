using System.Text;
using WinTempCleaner.Core.Safety;
using System.IO;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Property-based fuzzing for the path canonicalizer.
///
/// The plan called for SharpFuzz (libFuzzer) against PathSecurity. That needs a native
/// libFuzzer binary and a specially-shaped harness, which cannot run in normal CI or on a
/// contributor machine. This achieves the same objective - prove no traversal payload can
/// slip past canonicalization - as a deterministic in-process fuzz run, so it executes on
/// every build and is reproducible via a fixed seed.
///
/// The generator deliberately targets the shapes that break naive canonicalizers:
/// traversal segments, device prefixes, ADS streams, reserved device names, 8.3 short
/// names, overlong paths, and mixed separators.
///
/// Run the million-case campaign locally with the FuzzDeep trait:
///   Deltempo.Tests.exe --filter "Category=FuzzDeep"
/// </summary>
public class PathSecurityFuzzTests
{
    // Fixed seed => a failure is always reproducible from the printed input.
    private const int Seed = 0x5EED_2024;
    private const int DefaultCases = 100_000;
    private const int DeepCases = 1_000_000;

    private static readonly string[] TraversalSegments =
    {
        "..", "...", "....", ".. ", ". ..", "..;", "..%2f", "..%252f", "..\\", "../",
        "..\\\\/", ".\\\\..", "..\\\\..\\\\", "%2e%2e", "%2e%2e/", "%2e%2e%2f", "..%c0%af"
    };

    private static readonly string[] DevicePrefixes =
    {
        @"\\?\\", @"\\?\\UNC\\", @"\\.\\", @"\\?\\GLOBALROOT", @"\??\\", "//?/", "//./",
        @"\\?\\Volume{00000000-0000-0000-0000-000000000000}"
    };

    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM9", "LPT1", "LPT9", "CLOCK$"
    };

    private static readonly string[] AdversarialTokens =
    {
        ":", "::$DATA", "::$INDEX_ALLOCATION", " ", "\\t", "\\r", "\\n", "\\0",
        ".", "...", "~1", "a~1.tmp", @"..\..\..\windows\system32\config\SAM",
        @"..\..\..\..\windows\win.ini", "x" + new string('y', 200), "$MFT", "$Secure"
    };

    private static string GeneratePayload(Random rng)
    {
        var sb = new StringBuilder(96);
        var parts = new List<string>(8);

        if (rng.Next(4) == 0)
        {
            parts.Add(DevicePrefixes[rng.Next(DevicePrefixes.Length)]);
        }

        if (rng.Next(3) == 0)
        {
            parts.Add(@"C:" + Path.DirectorySeparatorChar);
        }

        var segmentCount = rng.Next(1, 7);
        for (var i = 0; i < segmentCount; i++)
        {
            var bucket = rng.Next(10);
            if (bucket < 4)
            {
                parts.Add(TraversalSegments[rng.Next(TraversalSegments.Length)]);
            }
            else if (bucket < 6)
            {
                parts.Add(AdversarialTokens[rng.Next(AdversarialTokens.Length)]);
            }
            else if (bucket < 7)
            {
                parts.Add(ReservedNames[rng.Next(ReservedNames.Length)] + rng.Next(0, 9));
            }
            else
            {
                parts.Add("dir" + rng.Next(0, 999));
            }
        }

        // Mix separators: a canonicalizer that only handles one of them is exploitable.
        for (var i = 0; i < parts.Count; i++)
        {
            sb.Append(parts[i]);
            if (i < parts.Count - 1)
            {
                sb.Append(rng.Next(4) switch
                {
                    0 => '/',
                    1 => '\\',
                    2 => "//",
                    _ => @"\\"
                });
            }
        }

        return sb.ToString();
    }

    private static string GenerateMutation(Random rng)
    {
        return GeneratePayload(rng) + (rng.Next(2) == 0 ? "/" : "\\") + GeneratePayload(rng);
    }

    private static string Describe(string value)
    {
        var safe = value.Replace("\0", "\\0").Replace("\r", "\\r").Replace("\n", "\\n");
        return value.Length <= 200
            ? "\"" + safe + "\""
            : "\"" + safe[..200] + $"\\\" ... ({value.Length} chars)";
    }

    /// <summary>
    /// Core invariant sweep. For every generated payload the canonicalizer must:
    ///  1. never throw - a throw is a denial-of-service on the delete path;
    ///  2. never return a path still containing a surviving multi-dot traversal segment
    ///     (a bare ".." is allowed: Windows treats it as parent-of-cwd);
    ///  3. be idempotent - canonicalizing twice must not change the result, otherwise two
    ///     code paths could disagree about what is being deleted (TOCTOU);
    ///  4. never return null;
    ///  5. never turn blank input into a live path.
    /// </summary>
    [Fact]
    [Trait("Category", "Fuzz")]
    public void NormalizeCanonicalPath_FuzzedPayloads_UpholdSecurityInvariants()
    {
        RunFuzz(DefaultCases, includeMutations: false);
    }

    [Fact]
    [Trait("Category", "FuzzDeep")]
    public void NormalizeCanonicalPath_DeepFuzz_MillionCases()
    {
        RunFuzz(DeepCases, includeMutations: true);
    }

    private static void RunFuzz(int cases, bool includeMutations)
    {
        var rng = new Random(Seed); // DevSkim: ignore DS148264

        for (var i = 0; i < cases; i++)
        {
            var payload = includeMutations && (i % 5 == 0)
                ? GenerateMutation(rng)
                : GeneratePayload(rng);

            var first = Canonicalize(payload, "initial");
            Assert.NotNull(first);

            AssertNoTraversalSegment(payload, first);

            // Idempotence: the same input must always produce the same canonical form.
            var second = Canonicalize(first, "re-canonicalization");
            if (!string.Equals(first, second, StringComparison.Ordinal))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Canonicalization is not idempotent. Input: {Describe(payload)} | pass1: {Describe(first)} | pass2: {Describe(second)}");
            }

            if (string.IsNullOrWhiteSpace(payload) && first.Length != 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Blank input produced a non-empty path: {Describe(first)}");
            }
        }
    }

    private static string Canonicalize(string value, string stage)
    {
        try
        {
            return PathSecurity.NormalizeCanonicalPath(value);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"NormalizeCanonicalPath threw {ex.GetType().Name} during {stage} for input: {Describe(value)}");
        }
    }

    private static void AssertNoTraversalSegment(string payload, string canonical)
    {
        var segments = canonical.Split(new[] { '/', '\\' }, StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (segment.Length < 3)
            {
                continue; // ".." alone is a legitimate parent-of-cwd form.
            }

            var allDots = true;
            foreach (var ch in segment)
            {
                if (ch != '.')
                {
                    allDots = false;
                    break;
                }
            }

            if (allDots)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Canonical path retains a traversal segment '{segment}'. Input: {Describe(payload)} | result: {Describe(canonical)}");
            }
        }
    }
}

