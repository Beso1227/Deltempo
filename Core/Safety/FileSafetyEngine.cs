using System.Collections.Frozen;
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
            // Only stat the filesystem when the caller did NOT already supply the metadata.
            // The planner gets LastWriteTimeUtc and Length straight from the scanner's
            // FindFirstFileExW and passes them in; re-reading them here doubled the stat
            // count for every file in every scan. FileInfo construction is not free, and
            // on a 500k-file %TEMP% that was ~500k avoidable FileInfo allocations.
            bool callerSuppliedMetadata =
                !string.IsNullOrWhiteSpace(fileName) || sizeBytes > 0 || lastModified.HasValue;

            var fileInfo = new FileInfo(canonicalPath);
            if (fileInfo.Exists)
            {
                resolvedFileName = fileInfo.Name;

                if (!callerSuppliedMetadata)
                {
                    fileLastModified = fileInfo.LastWriteTimeUtc;
                    fileSizeBytes = fileInfo.Length;
                }
                else
                {
                    // Trust the scanner's metadata, but the extension and file name must
                    // come from the filesystem - the scanner record may carry a short name.
                    fileLastModified = lastModified?.ToUniversalTime() ?? fileInfo.LastWriteTimeUtc;
                    fileSizeBytes = sizeBytes;
                }

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
                    SafetyRiskTier.ReviewRequired,
                    70,
                    "REVIEW REQUIRED (Older Downloaded Installer)",
                    "REVIEW DOWNLOAD",
                    $"{desc} older than 48 hours ({FormatBytes(fileSizeBytes)}). May still be needed; verify the software is installed and the file is disposable before deletion.",
                    "OlderDownloadRule",
                    origin: "Downloads / Temp Staging",
                    impact: "User verification required; re-download required if installation is needed in the future.");
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
        // Authorization comes from the DIRECTORY. Two tiers, because "this file is inside a
        // configured cleanup target" is a much weaker statement than "this file is inside a
        // temp directory":
        //   - UnambiguousTempShape: the path IS a temp/scratch/crash location. Data formats
        //     (.json/.db/.xml/...) and extensionless files are disposable here.
        //   - MerelyInsideAuthorizedRoot: the file sits under a configured target. That proves
        //     the target was selected, not that every file in it is disposable, so a data
        //     format earns Safe here only because the root itself is curated. The curated
        //     roots are what makes this sound, which is why the dangerous ones are refused
        //     outright by ProtectionPolicy.NonDisposableStateDirectories rather than being
        //     trusted to this rule.
        // Unrecognized extensions fail closed to UNKNOWN.
        bool isUnambiguousTempShape =
            pathLower.Contains(@"\appdata\local\temp\") ||
            pathLower.Contains(@"\windows\temp\") ||
            pathLower.Contains(@"\local\temp\") ||
            pathLower.Contains(@"\inetcache\") ||
            pathLower.Contains(@"\crashdumps\") ||
            pathLower.Contains(@"\crashpad\") ||
            pathLower.Contains(@"\softwaredistribution\download\");

        bool isInsideAuthorizedRoot =
            (allowedRoots != null && allowedRoots.Any(r => !string.IsNullOrEmpty(r) && PathSecurity.IsSubpathOf(canonicalPath, r))) ||
            (!string.IsNullOrEmpty(allowedRoot) && PathSecurity.IsSubpathOf(canonicalPath, allowedRoot));

        bool isScratchFormat = !string.IsNullOrEmpty(ext) && ScratchExtensions.Contains(ext);
        bool isDisposableDataFormat = DataFormatExtensions.Contains(ext);

        // A logs\ directory is a weak location signal (a user folder named "logs" matches),
        // so it unlocks scratch formats only, never data formats.
        bool isWeakScratchLocation = pathLower.Contains(@"\logs\");

        bool qualifies = isScratchFormat
            ? (isInsideAuthorizedRoot || isUnambiguousTempShape || isWeakScratchLocation)
            : isDisposableDataFormat && (isInsideAuthorizedRoot || isUnambiguousTempShape);

        // An extensionless file proves nothing about its own nature. It is only disposable in
        // a genuine temp directory - "inside a cache-ish path" was the old test, and a bare
        // Contains("cache") substring matched CachedData, Caches and every vendor spelling.
        if (string.IsNullOrEmpty(ext) && isUnambiguousTempShape)
        {
            qualifies = true;
        }

        if (qualifies)
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

    /// <summary>
    /// Extensions that are scratch fragments regardless of where they are found: a temp file,
    /// a log, a partial download, a crash dump. Safe inside any authorized cleanup root.
    /// </summary>
    private static readonly FrozenSet<string> ScratchExtensions = new string[]
    {
        // Temporary, Scratch & Incomplete
        ".tmp", ".temp", ".cache", ".chk", ".swp", ".lock", ".etag", ".part", ".partial", ".crdownload",
        // Logs, Dumps & Diagnostic Traces
        ".log", ".old", ".bak", ".dmp", ".mdmp", ".wer", ".etl", ".sqm", ".out", ".err", ".log1", ".log2",
        // Installer payload fragments (not archives - .zip/.7z/.tar/.gz never reach this rule,
        // ExecutableArchiveSafetyGate returns REVIEW for them first)
        ".cab", ".inf"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extensions that are data formats a real application can depend on. These are only
    /// treated as disposable inside an authorized cleanup root or an unambiguous temp
    /// directory - never on the strength of the extension alone.
    /// The empty extension is deliberately absent: a file with no extension carries no
    /// evidence about its own nature, and treating "" as disposable previously marked every
    /// extensionless file under any authorized root as Safe (90).
    /// </summary>
    private static readonly FrozenSet<string> DataFormatExtensions = new string[]
    {
        // Binary, Data & Index Chunks
        ".dat", ".bin", ".data", ".blob", ".chunk", ".idx", ".pack", ".index", ".dir", ".node", ".wasm",
        // Text, Structured & Markup
        ".txt", ".xml", ".json", ".yaml", ".yml", ".csv",
        // Configuration
        ".ini", ".cfg", ".uuid",
        // Database fragments
        ".db", ".sqlite", ".sqlite-wal", ".sqlite-shm"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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

        // Windows Explorer Thumbnail & Icon Cache (thumbcache_*.db, iconcache_*.db)
        if (pathLower.Contains(@"\microsoft\windows\explorer\") &&
            (fileName.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) ||
             fileName.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase) ||
             ext is ".db"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                95,
                "SAFE (Windows Thumbnail Cache)",
                "VERIFIED CACHE",
                $"Windows Explorer thumbnail database ({FormatBytes(sizeBytes)}). Rebuilt automatically by Windows as folders are browsed.",
                "ExplorerThumbnailCacheRule",
                origin: "Windows Explorer Thumbnail Cache",
                impact: "Zero impact — thumbnails re-generate on demand.");
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

        // NOTE: There is deliberately NO rule matching a BARE drive-root vendor folder
        // (C:\NVIDIA\, C:\AMD\, C:\Intel\, C:\Realtek\). Those roots are where a driver
        // installer unpacks payload it has not consumed yet, so "any file under it is a
        // verified cache" is false during an installation. Only the specific staging
        // subdirectories are recognized, by DriverStagingPackageRule below.

        // Hardware Driver Staging & Installer Caches (NVIDIA, AMD, Intel, DriverStore Temp)
        if (pathLower.Contains(@"\nvidia corporation\installer2\") ||
            pathLower.Contains(@"\nvidia corporation\downloader\") ||
            pathLower.Contains(@"\nvidia app\updateframework\ota-artifacts\") ||
            pathLower.Contains(@"\nvidia app\installer\") ||
            pathLower.Contains(@"\geforce experience\download\") ||
            pathLower.Contains(@"\nvidia\updates\") ||
            pathLower.Contains(@"\nvidia\displaydriver\") ||
            pathLower.Contains(@"\nvidia corporation\netservice\") ||
            pathLower.Contains(@"\amd_radeon_software_installer\") ||
            pathLower.Contains(@"\amd\packages\") ||
            pathLower.Contains(@"\amd\dvr\") ||
            pathLower.Contains(@"\intel\package cache\") ||
            pathLower.Contains(@"\intel\dsa\") ||
            pathLower.Contains(@"\intel\gfx\") ||
            pathLower.Contains(@"\intel\logs\") ||
            pathLower.Contains(@"\intel(r) processor diagnostic tool\") ||
            pathLower.Contains(@"\driverstore\temp\") ||
            pathLower.Contains(@"\driverstate\"))
        {
            return CreateResult(
                SafetyRiskTier.Safe,
                95,
                "SAFE (Hardware Driver Package Staging)",
                "VERIFIED DRIVER CACHE",
                $"Hardware driver installation and update staging artifact ({FormatBytes(sizeBytes)}). Safe to purge once installed.",
                "DriverStagingPackageRule",
                origin: "Hardware Driver Staging / Installer Cache",
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
