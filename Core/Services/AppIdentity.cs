using System.Text.RegularExpressions;

namespace WinTempCleaner.Models;

/// <summary>
/// A folder name an app plausibly owns on disk, derived from its registry DisplayName.
/// </summary>
/// <param name="Name">Candidate folder name.</param>
/// <param name="IsWeak">
/// True when the name is only one significant word of a multi-word identity (e.g. "Adobe" from
/// "Adobe Acrobat Reader"). Weak candidates match many unrelated folders and must never be
/// auto-selected for deletion.
/// </param>
public readonly record struct IdentityCandidate(string Name, bool IsWeak);

/// <summary>
/// Derives the folder names an installed application plausibly owns from its DisplayName.
///
/// DisplayNames are marketing strings, not folder names: "PDFgear 1.2.3" ships a folder called
/// "PDFgear", and "Visual Studio Code" ships "Code - Insiders". Matching folders on the raw
/// DisplayName misses every versioned install, so residue is left behind. This strips version and
/// qualifier segments and yields the remaining plausible names.
///
/// Weak (single-token) candidates are emitted last and flagged: they only describe part of the
/// identity, so they are surfaced for review but never pre-selected.
/// </summary>
public static class AppIdentity
{
    private static readonly Regex VersionSegment =
        new(@"^v?\d+(\.\d+)*$|^x86$|^x64$|^32bit$|^64bit$|^32-bit$|^64-bit$|^arm64$|^win32$|^win64$|^msi$|^\(.+\)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Bracket/paren-stripped variant of a DisplayName ("Foo (64-bit)" -> "Foo 64-bit").</summary>
    public static string Sanitize(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;
        return displayName.Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "").Trim();
    }

    /// <summary>
    /// Strips trailing version and architecture qualifier segments, repeatedly, until a segment
    /// that is not version-like remains: "WinRAR 7.00 (64-bit)" -> "WinRAR", "PDFgear 1.2.3" ->
    /// "PDFgear". Stops at the first segment that is not version-like, so "Visual Studio 2022"
    /// becomes "Visual Studio" and never "Visual".
    /// </summary>
    public static string StripVersionSuffix(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        string current = name.Trim();

        bool trimmed;
        do
        {
            trimmed = false;
            int lastSpace = current.LastIndexOf(' ');
            if (lastSpace <= 0) break;

            string last = current.Substring(lastSpace + 1).Trim();
            if (last.Length == 0 || VersionSegment.IsMatch(last))
            {
                current = current.Substring(0, lastSpace).Trim();
                trimmed = true;
            }
        }
        while (trimmed);

        return current;
    }

    /// <summary>
    /// Ordered, de-duplicated folder names this app may own: the DisplayName, its sanitized form,
    /// each version-stripped form, and finally the leading significant word as a weak fallback.
    /// Names shorter than 3 characters are dropped — too short to be a safe folder match.
    /// </summary>
    public static IReadOnlyList<IdentityCandidate> FolderNameCandidates(string? displayName)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string trimmed = value.Trim();
            if (trimmed.Length < 3) return;
            if (seen.Add(trimmed)) ordered.Add(trimmed);
        }

        Add(displayName ?? string.Empty);
        string sanitized = Sanitize(displayName);
        Add(sanitized);

        foreach (string step in VersionStrippedChain(sanitized))
            Add(step);

        // Weak single-token fallback: only from a genuinely multi-word identity.
        string baseName = ordered.Count > 0 ? ordered[0] : sanitized;
        string? primaryWord = baseName
            .Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => w.Length >= 4);

        var result = new List<IdentityCandidate>();
        foreach (string name in ordered)
        {
            bool isWeak = primaryWord != null &&
                          name.Length >= 3 &&
                          !name.Equals(primaryWord, StringComparison.OrdinalIgnoreCase) &&
                          name.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
                              .All(w => w.Equals(primaryWord, StringComparison.OrdinalIgnoreCase));
            result.Add(new IdentityCandidate(name, isWeak));
        }

        // Emit the weak token last so callers can stop before it if they only want strong matches.
        if (primaryWord != null && seen.Add(primaryWord))
        {
            result.Add(new IdentityCandidate(primaryWord, true));
        }

        return result;
    }

    private static IEnumerable<string> VersionStrippedChain(string name)
    {
        string current = name;
        for (int i = 0; i < 4; i++)
        {
            string stripped = StripVersionSuffix(current);
            if (string.IsNullOrWhiteSpace(stripped) ||
                stripped.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            yield return stripped;
            current = stripped;
        }
    }
}