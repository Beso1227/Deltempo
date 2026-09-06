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

            // Strip extended length DOS device prefixes for canonical resolution
            if (trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = @"\\" + trimmed[8..];
            }
            else if (trimmed.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[4..];
            }

            string full = Path.GetFullPath(trimmed);
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }

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
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                return (fi.Attributes & FileAttributes.ReparsePoint) != 0 || fi.LinkTarget != null;
            }

            if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                return (di.Attributes & FileAttributes.ReparsePoint) != 0 || di.LinkTarget != null;
            }
        }
        catch
        {
            // If attributes cannot be read safely, treat as potentially hazardous reparse point
            return true;
        }

        return false;
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
        catch
        {
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
        catch { }

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
