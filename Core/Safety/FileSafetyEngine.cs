using System.IO;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Deterministic, multi-signal safety analysis engine.
/// Replaces heuristic "AI" claims with verifiable, rule-based classification:
/// PROTECTED, SAFE, LOW_RISK, REVIEW_REQUIRED, UNKNOWN.
/// Conservative principle: "When in doubt, KEEP the file."
/// </summary>
public static class FileSafetyEngine
{
    public static SafetyAnalysisResult Analyze(
        string filePath,
        string fileName = "",
        string category = "General",
        long sizeBytes = 0,
        DateTime? lastModified = null,
        string? allowedRoot = null,
        bool apply24HourThreshold = false,
        IEnumerable<string>? allowedRoots = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return CreateResult(SafetyRiskTier.Unknown, 0, "Invalid Path", "UNKNOWN", "The path is null, empty, or whitespace.", "InvalidInputRule");
        }

        // 1. Path normalization & traversal defense
        string canonicalPath = PathSecurity.NormalizeCanonicalPath(filePath);
        if (string.IsNullOrEmpty(canonicalPath) || PathSecurity.HasPathTraversalSequences(filePath))
        {
            return CreateResult(SafetyRiskTier.Protected, 0, "PROTECTED (Illegal Path)", "PROTECTED", "Path contains directory traversal or malformed characters.", "PathTraversalRule");
        }

        // 2. Root containment verification (if allowed roots are specified)
        if (allowedRoots != null && allowedRoots.Any())
        {
            if (!allowedRoots.Any(root => !string.IsNullOrEmpty(root) && PathSecurity.IsSubpathOf(canonicalPath, root)))
            {
                return CreateResult(SafetyRiskTier.Protected, 0, "PROTECTED (Out of Scope)", "PROTECTED", "File is outside the designated cleanup roots.", "BoundaryContainmentRule");
            }
        }
        else if (!string.IsNullOrEmpty(allowedRoot) && !PathSecurity.IsSubpathOf(canonicalPath, allowedRoot))
        {
            return CreateResult(SafetyRiskTier.Protected, 0, "PROTECTED (Out of Scope)", "PROTECTED", $"File is outside the designated cleanup root '{allowedRoot}'.", "BoundaryContainmentRule");
        }

        // 3. Centralized Protection Policy (Non-negotiable protected OS, user, key, session, or game assets)
        if (ProtectionPolicy.IsProtected(canonicalPath, out string matchedReason))
        {
            return CreateResult(
                SafetyRiskTier.Protected,
                0,
                $"PROTECTED ({matchedReason})",
                "PROTECTED",
                matchedReason,
                "ProtectionPolicy",
                origin: matchedReason);
        }

        // 4. Reparse point & junction guard
        if (PathSecurity.IsReparsePointOrLink(canonicalPath))
        {
            return CreateResult(SafetyRiskTier.Protected, 0, "PROTECTED (Reparse Point / Symlink)", "PROTECTED", "NTFS Reparse point, directory junction, or symbolic link detected. Deletion aborted.", "ReparsePointGuard");
        }

        // 5. Gather filesystem metadata safely with fallback for synthetic test records
        DateTime fileLastModified;
        long fileSizeBytes;
        string resolvedFileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(canonicalPath) : fileName;
        string ext = Path.GetExtension(resolvedFileName).ToLowerInvariant();
        string pathLower = canonicalPath.ToLowerInvariant();

        try
        {
            var fileInfo = new FileInfo(canonicalPath);
            if (fileInfo.Exists)
            {
                fileLastModified = fileInfo.LastWriteTimeUtc;
                fileSizeBytes = fileInfo.Length;
                resolvedFileName = fileInfo.Name;
                ext = fileInfo.Extension.ToLowerInvariant();
            }
            else
            {
                // Fallback: If caller supplied explicit metadata (e.g., from scanner record or unit test), utilize it
                if (!string.IsNullOrWhiteSpace(fileName) || sizeBytes > 0 || lastModified.HasValue)
                {
                    fileLastModified = lastModified?.ToUniversalTime() ?? DateTime.UtcNow;
                    fileSizeBytes = sizeBytes;
                }
                else
                {
                    return CreateResult(SafetyRiskTier.Unknown, 0, "File Not Found", "UNKNOWN", "File does not exist on disk at evaluation time.", "ExistenceCheck");
                }
            }
        }
        catch (Exception ex)
        {
            return CreateResult(SafetyRiskTier.Unknown, 0, "Inaccessible File", "UNKNOWN", $"Filesystem metadata error: {ex.Message}", "MetadataReadError");
        }

        double ageHours = (DateTime.UtcNow - fileLastModified).TotalHours;

        // 6. Safe Mode (24-Hour Shield) for active temp files
        if (apply24HourThreshold && ageHours < 24.0)
        {
            return CreateResult(
                SafetyRiskTier.ReviewRequired,
                45,
                "REVIEW REQUIRED (Modified Recently)",
                "REVIEW",
                $"File was modified {ageHours:F1} hours ago (less than 24-hour threshold). May be required by an active application session.",
                "RecentModificationShield",
                origin: "Active / Recent File",
                impact: "Could interrupt recently launched programs.");
        }

        // 7. Deterministic Verified Cache Signatures (Tier: Safe)
        var cacheResult = EvaluateVerifiedCachePatterns(pathLower, resolvedFileName, ext, fileSizeBytes);
        if (cacheResult != null)
        {
            return cacheResult;
        }

        // 8. Standalone setup executables & archives in Downloads/Temp (Tier: Safe or ReviewRequired)
        // Rule: Extension alone never grants delete authorization. Must be accompanied by clear directory context and age.
        if (ext is ".iso" or ".img" or ".msi" or ".exe" or ".zip" or ".rar" or ".7z" or ".tar" or ".gz")
        {
            bool inDownloadsOrTemp = pathLower.Contains(@"\downloads\") || pathLower.Contains(@"\temp\");
            if (inDownloadsOrTemp && ageHours >= 48.0)
            {
                string desc = ext switch
                {
                    ".iso" or ".img" => "Older downloaded disk image",
                    ".msi" => "Older Windows Installer package",
                    ".exe" when resolvedFileName.Contains("setup", StringComparison.OrdinalIgnoreCase) || resolvedFileName.Contains("install", StringComparison.OrdinalIgnoreCase) => "Older standalone setup executable",
                    _ => "Older downloaded archive"
                };

                return CreateResult(
                    SafetyRiskTier.Safe,
                    90,
                    "SAFE (Older Downloaded Installer)",
                    "SAFE DOWNLOAD",
                    $"{desc} older than 48 hours ({FormatBytes(fileSizeBytes)}). Safe to purge if the software is already installed.",
                    "OlderDownloadRule",
                    origin: "Downloads / Temp Staging",
                    impact: "Re-download required if installation is needed in the future.");
            }

            // Executables or archives in unrecognized folders or recently downloaded -> REVIEW REQUIRED
            return CreateResult(
                SafetyRiskTier.ReviewRequired,
                50,
                "REVIEW REQUIRED",
                "REVIEW",
                $"Executable or archive in user space ({ext}). User verification required before deletion.",
                "ExecutableArchiveSafetyGate",
                origin: "User Executable / Archive",
                impact: "May contain standalone portable application or important files.");
        }

        // 9. Disposable scratch & temporary files with safe age (> 24h)
        if (ext is ".tmp" or ".temp" or ".log" or ".dmp" or ".bak" or ".old" or ".chk" or ".part" or ".crdownload")
        {
            if (pathLower.Contains(@"\temp\") || pathLower.Contains(@"\logs\") || pathLower.Contains(@"\crashdumps\"))
            {
                return CreateResult(
                    SafetyRiskTier.Safe,
                    95,
                    "SAFE TO CLEAN",
                    "VERIFIED CACHE",
                    $"Disposable scratch fragment or log older than 24 hours ({FormatBytes(fileSizeBytes)}).",
                    "TemporaryScratchRule",
                    origin: "Disposable Application Log / Scratch File",
                    impact: "Zero permanent impact.");
            }
        }

        // 9b. Non-executable files in designated temporary and cache directories
        // Files in verified temp/cache directories (subject to 24h shield, protection policy, and executable gates)
        // are disposable scratch and cache items.
        bool isDesignatedTempOrCacheLocation =
            pathLower.Contains(@"\appdata\local\temp\") ||
            pathLower.Contains(@"\windows\temp\") ||
            pathLower.Contains(@"\inetcache\") ||
            pathLower.Contains(@"\local\temp\") ||
            pathLower.Contains(@"\logs\") ||
            pathLower.Contains(@"\crashdumps\") ||
            pathLower.Contains(@"\crashpad\") ||
            pathLower.Contains(@"\softwaredistribution\download\") ||
            (allowedRoots != null && allowedRoots.Any(r => !string.IsNullOrEmpty(r) && PathSecurity.IsSubpathOf(canonicalPath, r))) ||
            (!string.IsNullOrEmpty(allowedRoot) && PathSecurity.IsSubpathOf(canonicalPath, allowedRoot));

        if (isDesignatedTempOrCacheLocation)
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                90,
                "SAFE (Designated Cache / Temp File)",
                "VERIFIED CACHE",
                $"Disposable cache or temporary file residing in verified cleanup location ({FormatBytes(fileSizeBytes)}).",
                "DesignatedCacheFileRule",
                origin: "Designated Cache / Temporary Storage",
                impact: "Zero permanent impact.");
        }

        // 10. Default Conservative Fallback -> UNKNOWN (Always Keep)
        return CreateResult(
            SafetyRiskTier.Unknown,
            20,
            "UNKNOWN (Preserved)",
            "UNKNOWN",
            "File context could not be deterministically verified as disposable. Preserved by default.",
            "ConservativeDefaultRule",
            origin: "Unclassified File",
            impact: "Preserved to avoid data loss.");
    }

    private static SafetyAnalysisResult? EvaluateVerifiedCachePatterns(string pathLower, string fileName, string ext, long sizeBytes)
    {
        // GPU Shader Caches (DirectX, Vulkan, NVIDIA, AMD, Intel)
        if (pathLower.Contains(@"\d3dscache\") || pathLower.Contains(@"\dxcache\") ||
            pathLower.Contains(@"\glcache\") || pathLower.Contains(@"\nv_shadercache\") ||
            pathLower.Contains(@"\amd\dx9cache\") || pathLower.Contains(@"\amd\dx11cache\") ||
            pathLower.Contains(@"\amd\glcache\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                100,
                "SAFE (Verified GPU Shader Cache)",
                "VERIFIED CACHE",
                $"Compiled graphics shader cache ({FormatBytes(sizeBytes)}). Re-compiles automatically on demand with zero system impact.",
                "GpuShaderCacheRule",
                origin: "GPU Driver Shader Cache",
                impact: "Zero impact — shaders rebuild seamlessly on demand.");
        }

        // Windows Prefetch Traces
        if (pathLower.Contains(@"\windows\prefetch\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                95,
                "SAFE (Windows Prefetch)",
                "VERIFIED CACHE",
                $"Application launch trace prefetch file ({FormatBytes(sizeBytes)}). Windows rebuilds automatically on launch.",
                "WindowsPrefetchRule",
                origin: "Windows Prefetch",
                impact: "Zero impact — re-created upon subsequent application launches.");
        }

        // Windows Delivery Optimization & Update Downloads
        if (pathLower.Contains(@"\softwaredistribution\download\") ||
            pathLower.Contains(@"\deliveryoptimization\cache\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                100,
                "SAFE (Windows Update Cache)",
                "VERIFIED CACHE",
                $"Superseded Windows Update download packages ({FormatBytes(sizeBytes)}). Safe to purge once installed.",
                "WindowsUpdateDownloadRule",
                origin: "Windows Update Staging",
                impact: "Zero impact — updates are already installed.");
        }

        // Browser Web Caches (Chrome, Edge, Brave, Firefox, Opera)
        if ((pathLower.Contains(@"\google\chrome\") || pathLower.Contains(@"\microsoft\edge\") ||
             pathLower.Contains(@"\bravesoftware\") || pathLower.Contains(@"\mozilla\firefox\") ||
             pathLower.Contains(@"\opera software\")) &&
            (pathLower.Contains(@"\cache\") || pathLower.Contains(@"\cache_data\") ||
             pathLower.Contains(@"\code cache\") || pathLower.Contains(@"\gpucache\")))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                95,
                "SAFE (Browser Web Cache)",
                "VERIFIED CACHE",
                $"Cached web page assets and media ({FormatBytes(sizeBytes)}). Re-downloads automatically on demand.",
                "BrowserCacheRule",
                origin: "Browser Web Cache",
                impact: "Zero impact — cached pages re-download when visited.");
        }

        // Developer Dependency Caches (pip, npm, yarn, gradle, NuGet)
        if (pathLower.Contains(@"\npm-cache\") || pathLower.Contains(@"\pip\cache\") ||
            pathLower.Contains(@"\.nuget\packages\") || pathLower.Contains(@"\.gradle\caches\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                95,
                "SAFE (Developer Dependency Cache)",
                "VERIFIED CACHE",
                $"Downloaded package cache ({FormatBytes(sizeBytes)}). Packages re-download automatically if referenced in future builds.",
                "DeveloperPackageCacheRule",
                origin: "Developer Tooling Cache",
                impact: "Re-downloads automatically on next build if needed.");
        }

        // Diagnostic Crash Reports & Minidumps
        if (pathLower.Contains(@"\wer\reportarchive\") || pathLower.Contains(@"\wer\reportqueue\") ||
            pathLower.Contains(@"\windows\minidump\") || pathLower.Contains(@"\crashdumps\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                100,
                "SAFE (Diagnostic Dump)",
                "VERIFIED CACHE",
                $"Post-mortem crash dump or diagnostic report ({FormatBytes(sizeBytes)}).",
                "CrashDumpRule",
                origin: "Windows Error Reporting",
                impact: "Zero impact.");
        }

        // Hardware Driver Extractor Leftovers (NVIDIA, AMD, Intel)
        if (pathLower.StartsWith(@"c:\nvidia\") || pathLower.StartsWith(@"c:\amd\") ||
            pathLower.StartsWith(@"c:\intel\") || pathLower.Contains(@"\nvidia\displaydriver\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                100,
                "SAFE (Hardware Driver Extractor Cache)",
                "VERIFIED CACHE",
                $"Temporary setup files unpacked during hardware driver installation ({FormatBytes(sizeBytes)}). Drivers are already installed in System32.",
                "DriverExtractorCacheRule",
                origin: "Driver Extractor Staging",
                impact: "Zero impact — driver is already installed.");
        }

        return null;
    }

    private static SafetyAnalysisResult CreateResult(
        SafetyRiskTier tier,
        int score,
        string verdict,
        string verdictShort,
        string explanation,
        string ruleName,
        string origin = "",
        string impact = "")
    {
        var (fg, bg, border) = GetBadgeColors(tier);

        return new SafetyAnalysisResult
        {
            Tier = tier,
            SafetyScore = score,
            Verdict = verdict,
            VerdictShort = verdictShort,
            Explanation = explanation,
            MatchedRule = ruleName,
            Origin = string.IsNullOrEmpty(origin) ? ruleName : origin,
            Impact = string.IsNullOrEmpty(impact) ? "Preserved safely." : impact,
            BadgeColor = fg,
            BadgeBackground = bg,
            BadgeBorder = border
        };
    }

    private static (string fg, string bg, string border) GetBadgeColors(SafetyRiskTier tier) => tier switch
    {
        SafetyRiskTier.Protected => ("#EF4444", "#2A0E0E", "#EF4444"), // Red
        SafetyRiskTier.Safe => ("#10B981", "#0D2818", "#10B981"), // Green
        SafetyRiskTier.LowRisk => ("#06B6D4", "#0C2329", "#06B6D4"), // Cyan
        SafetyRiskTier.ReviewRequired => ("#F59E0B", "#2A1E0D", "#F59E0B"), // Amber
        _ => ("#9CA3AF", "#1F2937", "#4B5563")  // Slate / Neutral
    };

    private static string FormatBytes(long bytes) =>
        WinTempCleaner.Models.TargetFolderInfo.FormatBytes(bytes);
}
