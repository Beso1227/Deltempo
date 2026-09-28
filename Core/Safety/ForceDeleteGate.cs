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

    private static readonly string[] AbsoluteBlockedDirectories =
    [
        @"windows\system32",
        @"windows\syswow64",
        @"windows\winsxs",
        @"windows\boot",
        @"windows\fonts",
        @"windows\system",
        @"windows\servicing"
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

        // 5. Tier A: Critical Windows core directories
        foreach (var blocked in AbsoluteBlockedDirectories)
        {
            if (lower.Contains(blocked, StringComparison.OrdinalIgnoreCase))
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

        // 8. If under Program Files or ProgramData without being a standard cache path, require explicit override
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\').ToLowerInvariant();
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\').ToLowerInvariant();
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\').ToLowerInvariant();

        if (lower.StartsWith(progFiles + "\\") || lower.StartsWith(progFilesX86 + "\\") || lower.StartsWith(progData + "\\"))
        {
            if (!ProtectionPolicy.IsPackageOrScriptCachePath(lower))
            {
                return new ForceDeleteGateDecision(ForceDeleteTier.OverrideRequired, normalized, "Application directory requires explicit confirmation to delete.");
            }
        }

        return new ForceDeleteGateDecision(ForceDeleteTier.Allowed, normalized, "Target is verified safe for deletion.");
    }
}
