using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

/// <summary>
/// Proves whether a path on disk belongs to one specific application.
///
/// <c>InstallLocation</c> is read verbatim from the registry, so it describes where the
/// installer happened to put the app - not what the folder contains. Users routinely install
/// several unrelated apps into one shared folder (<c>D:\Setups</c>), and an uninstaller that
/// runs from that folder leaves the folder itself plus every other app's files in place.
/// Treating the registry path as "the app's main installation directory" therefore proposed
/// the shared folder - and, through the pre-uninstall snapshot, every unrelated installer in
/// it - as High-confidence leftovers. Select all + purge then deleted data the app never owned.
///
/// Ownership is decided by name, never by location alone:
///
///  * A directory is owned when its leaf name matches a strong <see cref="AppIdentity"/>
///    candidate for the app, or when it sits inside a standard install root and directly
///    contains an executable named after the app.
///  * A file is owned when it lives inside an owned directory, when one of its directory
///    segments under the install location is named after the app, or when its own name is.
///
/// Weak candidates (the single word of a multi-word identity, e.g. "Adobe" from "Adobe Acrobat
/// Reader") never prove ownership: they match unrelated folders and must stay review-only.
/// </summary>
public static class InstallLocationOwnership
{
    private static readonly HashSet<string> StandardInstallRoots = BuildStandardInstallRoots();

    /// <summary>
    /// Suffixes that may follow an app's name without turning the match into a different
    /// product: "PDFgearPortable" and "PDFgear Setup" are the same app, "Visual Studio Code"
    /// is not "Visual Studio". Accepting any remainder would claim a neighbour's folder.
    /// </summary>
    private static readonly HashSet<string> AllowedNameSuffixes = new(StringComparer.Ordinal)
    {
        "portable", "setup", "installer", "install", "app", "desktop", "client", "player",
        "viewer", "beta", "alpha", "insiders", "canary", "nightly", "stable", "x64", "x86",
        "arm64", "win32", "win64", "msi"
    };

    /// <summary>True when <paramref name="directoryPath"/> is provably this app's own folder.</summary>
    public static bool IsDirectoryOwnedByApp(InstalledAppItem? app, string? directoryPath)
    {
        if (app is null || string.IsNullOrWhiteSpace(app.DisplayName) || string.IsNullOrWhiteSpace(directoryPath))
            return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(directoryPath).TrimEnd('\\', '/');
        }
        catch
        {
            return false;
        }

        string leaf = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(leaf)) return false;

        if (MatchesStrongName(app.DisplayName, leaf)) return true;

        // Content proof is restricted to real install roots. A shared user folder such as
        // D:\Setups may happen to contain the app's exe, but that still does not make the
        // folder itself disposable.
        return IsInsideStandardInstallRoot(fullPath) && ContainsAppExecutable(app.DisplayName, fullPath);
    }

    /// <summary>
    /// True when <paramref name="filePath"/> is provably this app's file. <paramref name="installLocation"/>
    /// scopes the directory-segment walk so an unrelated ancestor with the same name (a user
    /// profile called "PDFgear", for example) cannot bless the whole tree.
    /// </summary>
    public static bool IsFileOwnedByApp(InstalledAppItem? app, string? filePath, string? installLocation)
    {
        if (app is null || string.IsNullOrWhiteSpace(app.DisplayName) || string.IsNullOrWhiteSpace(filePath))
            return false;

        string fullFile;
        try
        {
            fullFile = Path.GetFullPath(filePath);
        }
        catch
        {
            return false;
        }

        if (MatchesStrongName(app.DisplayName, Path.GetFileNameWithoutExtension(fullFile)))
            return true;

        string install = string.Empty;
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            try
            {
                install = Path.GetFullPath(installLocation).TrimEnd('\\', '/');
            }
            catch
            {
                install = string.Empty;
            }
        }

        if (install.Length > 0 && IsDirectoryOwnedByApp(app, install))
            return true;

        if (install.Length > 0 && IsUnderOrEqual(install, fullFile))
        {
            string relative = Path.GetRelativePath(install, fullFile);
            string[] segments = relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            // Every segment except the file name itself is a directory the app would have to own.
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (MatchesStrongName(app.DisplayName, segments[i]))
                    return true;
            }
        }

        return false;
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="ancestor"/> or lives beneath it.</summary>
    public static bool IsUnderOrEqual(string? ancestor, string? path)
    {
        if (string.IsNullOrWhiteSpace(ancestor) || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            string a = Path.GetFullPath(ancestor).TrimEnd('\\', '/');
            string p = Path.GetFullPath(path).TrimEnd('\\', '/');

            if (string.Equals(a, p, StringComparison.OrdinalIgnoreCase)) return true;

            return p.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   p.StartsWith(a + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesStrongName(string displayName, string candidate)
    {
        string normalized = Normalize(candidate);
        if (normalized.Length < 3) return false;

        foreach (IdentityCandidate identity in AppIdentity.FolderNameCandidates(displayName))
        {
            if (identity.IsWeak) continue;

            string known = Normalize(identity.Name);
            if (known.Length < 3) continue;

            // Equality covers the normal case. A prefix is only accepted when the remainder
            // is a version number ("PDFgear 1.2.3" -> "pdfgear123") or a known distribution
            // qualifier ("PDFgearPortable"). Any other remainder belongs to a different
            // product that merely shares the prefix ("Visual Studio Code").
            if (normalized.Equals(known, StringComparison.Ordinal))
            {
                return true;
            }

            if (!normalized.StartsWith(known, StringComparison.Ordinal))
            {
                continue;
            }

            string remainder = normalized[known.Length..];
            if (remainder.Length > 0 &&
                (remainder.All(char.IsDigit) || AllowedNameSuffixes.Contains(remainder)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAppExecutable(string displayName, string directory)
    {
        try
        {
            foreach (string exe in Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
            {
                if (MatchesStrongName(displayName, Path.GetFileNameWithoutExtension(exe)))
                    return true;
            }
        }
        catch
        {
            // An unreadable folder proves nothing; refuse ownership rather than guess.
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="path"/> is one of the standard install roots or lives beneath
    /// one. Used to decide whether removing an empty parent folder is routine vendor cleanup
    /// (inside Program Files) or the destruction of a user-owned shared container.
    /// </summary>
    public static bool IsUnderStandardInstallRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return IsInsideStandardInstallRoot(Path.GetFullPath(path).TrimEnd('\\', '/'));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInsideStandardInstallRoot(string directory)
    {
        string? current = directory;
        // Two levels cover "C:\Program Files\Vendor\Product" and
        // "%LOCALAPPDATA%\Programs\Product" without blessing arbitrary drive folders.
        for (int depth = 0; depth < 3 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            if (StandardInstallRoots.Contains(current.TrimEnd('\\', '/')))
                return true;

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static HashSet<string> BuildStandardInstallRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                roots.Add(path.TrimEnd('\\', '/'));
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"));

        return roots;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            // '+' and '#' are part of product names ("Notepad++", "C#"). Folding them away
            // made "Notepad" own the "Notepad++" folder - a different application.
            if (char.IsLetterOrDigit(c) || c == '+' || c == '#')
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
