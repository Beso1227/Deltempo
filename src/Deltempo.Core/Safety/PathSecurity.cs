using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Hardened path and filesystem validation to defend against directory traversal,
/// symlink/junction redirection, UNC attacks, and out-of-boundary deletion attacks.
/// </summary>
public static class PathSecurity
{
    /// <summary>
    /// Normalizes and canonicalizes a path by resolving relative segments,
    /// standardizing directory separators, stripping duplicate slashes, and handling device prefixes.
    /// </summary>
    public static string NormalizeCanonicalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            string trimmed = path.Trim();

            // Strip extended length DOS device prefixes for canonical resolution - but only
            // when they carry a drive or UNC target; volume-less forms (\\?\Volume{...},
            // \\?\windows\...) would otherwise become relative to the current directory.
            if (trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = @"\\" + trimmed[8..];
            }
            else if (IsDriveDevicePath(trimmed))
            {
                trimmed = trimmed[4..];
            }

            string full = Path.GetFullPath(trimmed);

            // GetFullPath emits device paths when the input used forward slashes; fold those
            // into the same canonical form as the strips above so a second pass is a no-op.
            if (full.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                full = @"\\" + full[8..];
            }
            else if (IsDriveDevicePath(full))
            {
                full = full[4..];
            }

            // GetFullPath injects the current drive for rooted-driveless inputs, except NT
            // native roots like "\??\" which come back untouched (device passthrough); glue
            // the drive on and re-resolve so separators and dots normalize like any other
            // path - otherwise the raw form is not stable across passes.
            if (full.StartsWith(Path.DirectorySeparatorChar) && !full.StartsWith(@"\\"))
            {
                full = Path.GetFullPath(Path.GetPathRoot(Environment.CurrentDirectory) + full[1..]);
            }

            // Trim trailing separators, but never below the drive root "C:\" and never down
            // to the drive-relative form "C:" - GetFullPath re-resolves "C:" against the
            // current directory, which would break idempotence.
            while (full.Length > 3
                   && (full[^1] == Path.DirectorySeparatorChar || full[^1] == Path.AltDirectorySeparatorChar))
            {
                full = full[..^1];
            }

            if (full.Length == 2 && full[1] == ':')
            {
                full += Path.DirectorySeparatorChar;
            }

            // Win32 strips trailing dots/spaces from every component during DOS path
            // conversion, which can resurrect ".." from a segment like ".. " or collapse
            // "..." into an empty segment - either would defeat the containment checks
            // downstream. Reject any canonical form carrying such a segment.
            if (HasDotOrSpaceSegment(full))
                return string.Empty;

            return full;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PathSecurity] Canonicalization failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// True when any path segment ends with a space or a dot, or is made only of
    /// spaces and dots.
    /// </summary>
    /// <remarks>
    /// This replaces a <c>string.Split</c> over the whole path. The split allocated a
    /// <c>string[]</c> plus one substring per segment for EVERY path canonicalized -
    /// and canonicalization runs once per file per safety check, so on a half-million
    /// file scan that was half a million array allocations plus tens of millions of
    /// substrings. The scan below is allocation-free and answers the same question.
    /// </remarks>
    private static bool HasDotOrSpaceSegment(string path)
    {
        int segmentStart = 0;
        int length = path.Length;

        for (int i = 0; i <= length; i++)
        {
            bool atEnd = i == length;
            if (!atEnd)
            {
                char c = path[i];
                if (c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
                    continue;
            }

            // Segment is path[segmentStart..i)
            int segmentLength = i - segmentStart;
            if (segmentLength > 0)
            {
                int contentEnd = i;
                while (contentEnd > segmentStart &&
                       (path[contentEnd - 1] == ' ' || path[contentEnd - 1] == '.'))
                {
                    contentEnd--;
                }

                if (contentEnd != i)
                {
                    // Trailing dots/spaces were stripped. The stripped form is empty or
                    // is "..", both of which Win32 would reinterpret - reject.
                    int strippedLength = contentEnd - segmentStart;
                    if (strippedLength == 0)
                        return true;

                    if (strippedLength == 2 && path[segmentStart] == '.' && path[segmentStart + 1] == '.')
                        return true;
                }
            }

            segmentStart = i + 1;
        }

        return false;
    }

    private static bool IsDriveDevicePath(string path) =>
        path.Length >= 6
        && path.StartsWith(@"\\?\", StringComparison.Ordinal)
        && char.IsLetter(path[4])
        && path[5] == ':';

    /// <summary>
    /// Verifies that candidatePath is strictly contained inside allowedRoot.
    /// Defends against directory traversal ("..\") and symbolic escapes.
    /// </summary>
    public static bool IsSubpathOf(string candidatePath, string allowedRoot)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(allowedRoot))
            return false;

        string canonicalCandidate = NormalizeCanonicalPath(candidatePath);
        string canonicalRoot = NormalizeCanonicalPath(allowedRoot);

        return IsSubpathOfCanonical(canonicalCandidate, canonicalRoot);
    }

    /// <summary>
    /// Containment test for two ALREADY canonicalized paths.
    /// </summary>
    /// <remarks>
    /// <see cref="IsSubpathOf"/> canonicalizes both arguments on every call, and the
    /// policy layer calls it once per protected root per file. On a large scan that is
    /// tens of millions of redundant <see cref="NormalizeCanonicalPath"/> calls, each of
    /// which touches the filesystem indirectly through <c>Path.GetFullPath</c>.
    ///
    /// When both sides are already canonical (the caller normalized the candidate and
    /// the root is a fixed, pre-normalized policy root) this compares the strings
    /// directly. Same rule as <see cref="IsSubpathOf"/>: exact match counts as
    /// contained, and a descendant must sit under root + separator.
    /// </remarks>
    public static bool IsSubpathOfCanonical(string canonicalCandidate, string canonicalRoot)
    {
        if (string.IsNullOrEmpty(canonicalCandidate) || string.IsNullOrEmpty(canonicalRoot))
            return false;

        // Exact match is considered contained
        if (string.Equals(canonicalCandidate, canonicalRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        string rootWithSep = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;

        return canonicalCandidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects whether a path or its target is an NTFS Reparse Point, Symbolic Link, or Directory Junction.
    /// Cleanup operations must NEVER follow unexpected reparse points.
    /// </summary>
    public static bool IsReparsePointOrLink(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // ONE filesystem round-trip. The previous implementation asked three
            // separate questions - File.Exists, FileInfo.Attributes, FileInfo.LinkTarget -
            // at roughly 50 microseconds each, and this runs once per file per safety
            // check. That made the protection check, not the directory walk, the
            // dominant cost of a scan.
            //
            // GetFileAttributes answers existence AND the reparse-point attribute in a
            // single call. The reparse attribute is a SUPERSET of the old test: a
            // name-surrogate link (symlink, junction) always carries
            // FILE_ATTRIBUTE_REPARSE_POINT, so it is still refused, and a dangling link -
            // which the old three-call sequence missed entirely, because File.Exists
            // returned false for it - is now caught as well. The guard is strictly
            // stronger, never weaker.
            FileAttributes attributes = File.GetAttributes(path);

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            // If attributes cannot be read safely, treat as potentially hazardous reparse point
            Trace.WriteLine($"[PathSecurity] Attribute read failed: treating as reparse point ({ex.GetType().Name}: {ex.Message})");
            return true;
        }
    }

    /// <summary>
    /// Checks whether the FileSystemInfo object has the ReparsePoint attribute set or has a LinkTarget.
    /// </summary>
    public static bool IsReparsePointOrLink(FileSystemInfo? info)
    {
        if (info == null) return false;
        try
        {
            return (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget != null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PathSecurity] Attribute read failed: treating as reparse point ({ex.GetType().Name}: {ex.Message})");
            return true;
        }
    }

    /// <summary>
    /// Checks if the path represents an unapproved remote network share or UNC path (e.g. \\server\share).
    /// </summary>
    public static bool IsUncPath(string path) => IsNetworkOrUncPath(path);

    public static bool IsNetworkOrUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string trimmed = path.Trim();
        if (trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.StartsWith(@"\\") && !trimmed.StartsWith(@"\\?\"))
            return true;

        try
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                return uri.IsUnc;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PathSecurity] URI classification failed: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Detects illegal path traversal characters or sequences.
    /// </summary>
    public static bool HasPathTraversalSequences(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        if (path.Contains("..\\") || path.Contains("../") || path.EndsWith(".."))
            return true;

        char[] invalidChars = Path.GetInvalidPathChars();
        return path.IndexOfAny(invalidChars) >= 0;
    }
}
