using System.IO;
using WinTempCleaner.Core.Safety;

namespace WinTempCleaner.Core.Safety;

public enum ForceDeleteTier
{
    Allowed,
    OverrideRequired,
    AbsoluteBlock
}

public sealed record ForceDeleteGateDecision(
    ForceDeleteTier Tier,
    string NormalizedPath,
    string Rationale);

/// <summary>
/// Two-tier authorization gate for force-deletion operations.
/// Tier A (AbsoluteBlock): Critical operating system targets that brick the machine if removed.
/// Tier B (OverrideRequired): User files, sensitive extensions, or installed application directories requiring explicit confirmation.
/// Allowed: Safe temporary, cache, or disposable directories.
/// </summary>
public static class ForceDeleteGate
{
    private static readonly HashSet<string> AbsoluteBlockedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "dumpstack.log",
        "bootmgr",
        "bootnxt",
        "ntldr",
        "ntdetect.com",
        "autoexec.bat",
        "config.sys",
        "sam",
        "system",
        "software",
        "security",
        "default",
        "bcd",
        "ntuser.dat",
        "usrclass.dat"
    };

    /// <summary>
    /// Directories that are strictly off-limits, expressed RELATIVE to the Windows
    /// directory (Environment.SpecialFolder.Windows, i.e. C:\Windows). They are
    /// resolved with Path.Combine against that root, so a user folder that merely
    /// shares a name (e.g. D:\backup\windows\system32) is never caught by them.
    /// </summary>
    private static readonly string[] AbsoluteBlockedDirectories =
    [
        "System32",
        "SysWOW64",
        "WinSxS",
        "Boot",
        "Fonts",
        "System32\\config",
        "System32\\DriverStore",
        "assembly",
        "servicing"
    ];

    public static ForceDeleteGateDecision Evaluate(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, string.Empty, "Path is empty or whitespace.");
        }

        string normalized = PathSecurity.NormalizeCanonicalPath(targetPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, string.Empty, "Failed to normalize canonical path.");
        }

        string lower = normalized.ToLowerInvariant().Replace('/', '\\');

        // 1. Protect drive root (e.g., C:\ or D:\)
        string? root = Path.GetPathRoot(normalized);
        if (!string.IsNullOrEmpty(root))
        {
            string rootNorm = root.TrimEnd('\\').ToLowerInvariant();
            if (lower == rootNorm || lower == root.ToLowerInvariant())
            {
                return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, "Drive roots cannot be deleted.");
            }
        }

        // 2. Reject remote network/UNC paths
        if (PathSecurity.IsNetworkOrUncPath(normalized))
        {
            return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, "Network/UNC targets cannot be force deleted.");
        }

        // 3. Protect Deltempo's own executing binaries and base directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\').ToLowerInvariant();
        if (lower == baseDir || lower.StartsWith(baseDir + "\\"))
        {
            return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, "Cannot delete Deltempo's own operating binary or application root.");
        }

        // 4. Protect core Windows folder itself
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\').ToLowerInvariant();
        if (lower == winDir)
        {
            return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, "Windows directory root cannot be deleted.");
        }

        // 5. Tier A: Critical Windows core directories.
        // Anchored to the real Windows directory (not a substring match) so a user folder
        // such as D:\backup\windows\system32 is NOT falsely treated as system-critical.
        foreach (string relative in AbsoluteBlockedDirectories)
        {
            string blocked = Path.Combine(winDir, relative).ToLowerInvariant();
            if (lower == blocked || lower.StartsWith(blocked + "\\"))
            {
                return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, $"System-critical path '{blocked}' is strictly protected from deletion.");
            }
        }

        // 6. Tier A: Critical file names
        string fileName = Path.GetFileName(normalized);
        if (AbsoluteBlockedFiles.Contains(fileName))
        {
            return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, $"Critical OS system file '{fileName}' is strictly protected.");
        }

        // 7. Check standard ProtectionPolicy
        if (ProtectionPolicy.IsProtected(normalized, out string policyReason))
        {
            // Under Tier B (Explicit-Override), targets flagged by ProtectionPolicy (like sensitive extensions or user docs)
            // are allowed IF explicitly requested by the user, but require explicit override confirmation.
            return new ForceDeleteGateDecision(ForceDeleteTier.OverrideRequired, normalized, $"Protected by safety policy: {policyReason}. Explicit confirmation required.");
        }

        // 8. Program Files / ProgramData.
        // The ROOT directories are Tier A: deleting them would uninstall most of Windows.
        // Descendants outside known package caches are Tier B (explicit confirmation).
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\').ToLowerInvariant();
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\').ToLowerInvariant();
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\').ToLowerInvariant();

        foreach (string rootDir in new[] { progFiles, progFilesX86, progData })
        {
            if (string.IsNullOrEmpty(rootDir))
            {
                continue;
            }

            if (lower == rootDir)
            {
                return new ForceDeleteGateDecision(ForceDeleteTier.AbsoluteBlock, normalized, $"'{rootDir}' is an OS application root and can never be deleted.");
            }

            if (lower.StartsWith(rootDir + "\\") && !ProtectionPolicy.IsPackageOrScriptCachePath(lower))
            {
                return new ForceDeleteGateDecision(ForceDeleteTier.OverrideRequired, normalized, "Application directory requires explicit confirmation to delete.");
            }
        }

        return new ForceDeleteGateDecision(ForceDeleteTier.Allowed, normalized, "Target is verified safe for deletion.");
    }
}
