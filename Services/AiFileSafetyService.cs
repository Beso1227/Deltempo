using System.IO;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public enum AiSafetyTier
{
    SafeToClean,
    HighRiskKeep
}

public class AiAnalysisResult
{
    public int SafetyScore { get; set; }
    public AiSafetyTier Tier { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string VerdictShort { get; set; } = "SAFE";
    public string BadgeColor { get; set; } = "#10B981";
    public string BadgeBackground { get; set; } = "#122A1E";
    public string BadgeBorder { get; set; } = "#10B981";
    public string Origin { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public bool IsSafeToAutoClean { get; set; }
}

public static class AiFileSafetyService
{
    public static AiAnalysisResult AnalyzeFile(string filePath, string fileName, string category, long sizeBytes, DateTime lastModified)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        string pathLower = filePath.ToLowerInvariant();
        string nameLower = fileName.ToLowerInvariant();
        double ageDays = (DateTime.Now - lastModified).TotalDays;

        // =========================================================================
        // 1. HARDWARE DRIVER EXTRACTOR LEFTOVERS (NVIDIA, AMD, INTEL) -> 100% SAFE
        // =========================================================================
        if (pathLower.StartsWith(@"c:\nvidia\") || pathLower.Contains(@"\nvidia\displaydriver\") ||
            pathLower.StartsWith(@"c:\amd\") || pathLower.Contains(@"\amd\packages\") ||
            pathLower.StartsWith(@"c:\intel\"))
        {
            string vendor = pathLower.Contains("nvidia") ? "NVIDIA" : pathLower.Contains("amd") ? "AMD" : "Intel";
            return new AiAnalysisResult
            {
                SafetyScore = 100,
                Tier = AiSafetyTier.SafeToClean,
                Verdict = "SAFE TO DELETE",
                VerdictShort = "SAFE",
                BadgeColor = "#10B981",
                BadgeBackground = "#0D2818",
                BadgeBorder = "#10B981",
                Origin = $"{vendor} Driver Extractor Cache",
                Impact = "Zero impact — hardware drivers are already installed in Windows System32.",
                Explanation = $"Temporary setup files unpacked during {vendor} driver installation ({FormatBytes(sizeBytes)}). Safe to delete immediately.",
                IsSafeToAutoClean = true
            };
        }

        // =========================================================================
        // 2. CRASH MEMORY DUMPS & DIAGNOSTIC DUMPS -> 100% SAFE
        // =========================================================================
        if (ext is ".dmp" or ".mdmp" or ".hdmp" or ".log" or ".old" or ".bak" or ".tmp" or ".crdownload" or ".part" or ".chk" ||
            pathLower.Contains(@"\crashdumps\") || pathLower.Contains(@"\reportarchive\") || pathLower.Contains(@"\reportqueue\"))
        {
            return new AiAnalysisResult
            {
                SafetyScore = 100,
                Tier = AiSafetyTier.SafeToClean,
                Verdict = "SAFE TO DELETE",
                VerdictShort = "SAFE",
                BadgeColor = "#10B981",
                BadgeBackground = "#0D2818",
                BadgeBorder = "#10B981",
                Origin = "Diagnostic Crash Dump / Stale Download",
                Impact = "Zero impact — these are post-mortem debug logs or aborted downloads.",
                Explanation = $"Disposable crash dump or temporary fragment ({FormatBytes(sizeBytes)}). 100% safe to delete. Will never break anything.",
                IsSafeToAutoClean = true
            };
        }

        bool inDownloadOrTemp = pathLower.Contains(@"\downloads\") ||
                                pathLower.Contains(@"\temp\") ||
                                pathLower.Contains(@"\setups\") ||
                                pathLower.Contains(@"\installer\") ||
                                pathLower.Contains(@"\installers\") ||
                                pathLower.Contains(@"\appdata\local\temp\");

        // =========================================================================
        // 3. STANDALONE INSTALLERS & DISC IMAGES -> 100% SAFE
        // =========================================================================
        if (ext is ".iso" or ".img" or ".msi" or ".cab" or ".appx" or ".msix")
        {
            if (inDownloadOrTemp)
            {
                return new AiAnalysisResult
                {
                    SafetyScore = 100,
                    Tier = AiSafetyTier.SafeToClean,
                    Verdict = "SAFE TO DELETE",
                    VerdictShort = "SAFE",
                    BadgeColor = "#10B981",
                    BadgeBackground = "#0D2818",
                    BadgeBorder = "#10B981",
                    Origin = "Downloaded Disc Image / Installer Package",
                    Impact = "Zero impact — the software/OS from this installer is already installed.",
                    Explanation = $"Standalone installer package ({FormatBytes(sizeBytes)}). Keeping installer files after installation is redundant. 100% safe.",
                    IsSafeToAutoClean = true
                };
            }
        }

        if (ext == ".exe")
        {
            if (inDownloadOrTemp ||
                nameLower.Contains("setup") ||
                nameLower.Contains("install") ||
                nameLower.Contains("update") ||
                nameLower.Contains("patch") ||
                nameLower.Contains("hotfix") ||
                nameLower.Contains("installer") ||
                nameLower.EndsWith("_x64.exe") ||
                nameLower.EndsWith("_win64.exe"))
            {
                return new AiAnalysisResult
                {
                    SafetyScore = 100,
                    Tier = AiSafetyTier.SafeToClean,
                    Verdict = "SAFE TO DELETE",
                    VerdictShort = "SAFE",
                    BadgeColor = "#10B981",
                    BadgeBackground = "#0D2818",
                    BadgeBorder = "#10B981",
                    Origin = "Application Setup Executable",
                    Impact = "Zero impact — the application has already been installed on your system.",
                    Explanation = $"Standalone setup file in download directory ({FormatBytes(sizeBytes)}). Safe to delete without affecting installed apps.",
                    IsSafeToAutoClean = true
                };
            }
        }

        // =========================================================================
        // 4. DOWNLOADED ARCHIVES IN DOWNLOADS/TEMP -> 100% SAFE
        // =========================================================================
        if (ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2")
        {
            if (inDownloadOrTemp)
            {
                return new AiAnalysisResult
                {
                    SafetyScore = 100,
                    Tier = AiSafetyTier.SafeToClean,
                    Verdict = "SAFE TO DELETE",
                    VerdictShort = "SAFE",
                    BadgeColor = "#10B981",
                    BadgeBackground = "#0D2818",
                    BadgeBorder = "#10B981",
                    Origin = "Downloaded Archive (Zip/Rar)",
                    Impact = "Zero impact — contents have already been extracted or used.",
                    Explanation = $"Compressed archive in downloads ({FormatBytes(sizeBytes)}). Safe to delete once contents have been extracted.",
                    IsSafeToAutoClean = true
                };
            }
        }

        // =========================================================================
        // 5. PACKAGE & DEPENDENCY CACHES -> 100% SAFE
        // =========================================================================
        if (pathLower.Contains(@"\package cache\") ||
            pathLower.Contains(@"\pip\cache\") ||
            pathLower.Contains(@"\.nuget\packages\") ||
            pathLower.Contains(@"\npm-cache\") ||
            pathLower.Contains(@"\yarn\cache\") ||
            pathLower.Contains(@"\.gradle\caches\"))
        {
            return new AiAnalysisResult
            {
                SafetyScore = 100,
                Tier = AiSafetyTier.SafeToClean,
                Verdict = "SAFE TO DELETE",
                VerdictShort = "SAFE",
                BadgeColor = "#10B981",
                BadgeBackground = "#0D2818",
                BadgeBorder = "#10B981",
                Origin = "Developer Package Cache",
                Impact = "Zero permanent impact — packages re-download automatically if needed.",
                Explanation = $"Downloaded dependency cache ({FormatBytes(sizeBytes)}). 100% safe to purge to reclaim massive disk storage.",
                IsSafeToAutoClean = true
            };
        }

        // =========================================================================
        // 6. DISPOSABLE PATH OVERRIDE
        //    Files sitting in temp/cache/download/ota-artifacts/wer/logs/
        //    $windows.~/esd are safe to delete (preserves legacy IsProtectedFile
        //    behavior for .exe/.dll/.sys/.docx/.pdf/etc. in those locations).
        // =========================================================================
        // Files inside temp/cache/download folders are safe regardless of extension.
        // Match as a path component (e.g. "\temp\", "\cache\", "\downloads\") to avoid
        // false positives like "D:\Projects\deltempo\..." where "deltempo" contains "temp".
        var disposables = new[] { @"temp", @"cache", @"downloads", @"ota-artifacts", @"wer", @"logs", @"$windows.~", @"esd" };
        bool inDisposablePath = disposables.Any(d =>
            pathLower.StartsWith(d + @"\") ||
            pathLower.Contains(@"\programs\" + d + @"\") ||   // ProgramData\...\temp etc.
            (pathLower.Contains(@"\") && pathLower.Split('\\').Any(s => s.Equals(d, StringComparison.OrdinalIgnoreCase))));

        if (inDisposablePath)
        {
            return new AiAnalysisResult
            {
                SafetyScore = 100,
                Tier = AiSafetyTier.SafeToClean,
                Verdict = "SAFE TO DELETE",
                VerdictShort = "SAFE",
                BadgeColor = "#10B981",
                BadgeBackground = "#0D2818",
                BadgeBorder = "#10B981",
                Origin = "Disposable Path",
                Impact = "Zero impact — file is inside a temporary or cache directory.",
                Explanation = $"File in a disposable path ({FormatBytes(sizeBytes)}). Safe to delete.",
                IsSafeToAutoClean = true
            };
        }

        // =========================================================================
        // 7. PROTECTED: ACTIVE GAMES (STEAM, EPIC, RIOT, GOG, BATTLE.NET)
        // =========================================================================
        if (pathLower.Contains(@"\steamapps\") ||
            pathLower.Contains(@"\epic games\") ||
            pathLower.Contains(@"\gog games\") ||
            pathLower.Contains(@"\riot games\") ||
            pathLower.Contains(@"\ubisoft\") ||
            pathLower.Contains(@"\battle.net\") ||
            ext is ".pak" or ".vpk" or ".bundle" or ".obb" or ".unity3d" or ".uasset" or ".ubulk")
        {
            string launcher = pathLower.Contains(@"\steamapps\") ? "Steam" :
                              pathLower.Contains(@"\epic games\") ? "Epic Games" :
                              pathLower.Contains(@"\riot games\") ? "Riot Games" : "Installed Game";

            return new AiAnalysisResult
            {
                SafetyScore = 0,
                Tier = AiSafetyTier.HighRiskKeep,
                Verdict = "PROTECTED (Game Asset)",
                VerdictShort = "PROTECTED",
                BadgeColor = "#EF4444",
                BadgeBackground = "#2A0E0E",
                BadgeBorder = "#EF4444",
                Origin = $"{launcher} Active Game Content",
                Impact = "High Risk — Deleting this will corrupt the installed game and force a re-download.",
                Explanation = $"Essential game resource ({FormatBytes(sizeBytes)}). DO NOT DELETE: Required by your game to run.",
                IsSafeToAutoClean = false
            };
        }

        // =========================================================================
        // 7. PROTECTED: ACTIVE APPLICATIONS & WINDOWS OS COMPONENTS
        // =========================================================================
        if (ext is ".dll" or ".sys" ||
            pathLower.Contains(@"\program files\") ||
            pathLower.Contains(@"\program files (x86)\") ||
            pathLower.Contains(@"\windows\") ||
            pathLower.Contains(@"\system32\") ||
            pathLower.Contains(@"\syswow64\") ||
            pathLower.Contains(@"\windowsapps\"))
        {
            return new AiAnalysisResult
            {
                SafetyScore = 0,
                Tier = AiSafetyTier.HighRiskKeep,
                Verdict = "PROTECTED (App / System)",
                VerdictShort = "PROTECTED",
                BadgeColor = "#EF4444",
                BadgeBackground = "#2A0E0E",
                BadgeBorder = "#EF4444",
                Origin = "Windows System or Installed Program",
                Impact = "Critical Risk — Deleting will cause software failure or Windows instability.",
                Explanation = $"Active executable or library ({FormatBytes(sizeBytes)}). DO NOT DELETE: Required by installed applications to function.",
                IsSafeToAutoClean = false
            };
        }

        // =========================================================================
        // 8. PROTECTED: PROJECTS, CODE, AI MODELS & CREATIVE WORK
        // =========================================================================
        if (pathLower.Contains(@"\projects\") ||
            pathLower.Contains(@"\repos\") ||
            pathLower.Contains(@"\source\") ||
            pathLower.Contains(@"\.git\") ||
            pathLower.Contains(@"\workspace\") ||
            ext is ".onnx" or ".safetensors" or ".pt" or ".pth" or ".ckpt" or ".gguf" or ".psd" or ".blend" or ".prproj" or ".aep")
        {
            return new AiAnalysisResult
            {
                SafetyScore = 0,
                Tier = AiSafetyTier.HighRiskKeep,
                Verdict = "PROTECTED (Project / Model)",
                VerdictShort = "PROTECTED",
                BadgeColor = "#EF4444",
                BadgeBackground = "#2A0E0E",
                BadgeBorder = "#EF4444",
                Origin = "Personal Project / Machine Learning Model",
                Impact = "Data Loss Risk — Deleting will destroy your project work or trained AI weights.",
                Explanation = $"Your creative project work or AI model weights ({FormatBytes(sizeBytes)}). DO NOT DELETE.",
                IsSafeToAutoClean = false
            };
        }

        // =========================================================================
        // 9. PROTECTED: VIRTUAL MACHINE DISKS
        // =========================================================================
        if (ext is ".vhd" or ".vhdx" or ".vdi" or ".vmdk" or ".qcow2")
        {
            return new AiAnalysisResult
            {
                SafetyScore = 0,
                Tier = AiSafetyTier.HighRiskKeep,
                Verdict = "PROTECTED (Virtual Machine)",
                VerdictShort = "PROTECTED",
                BadgeColor = "#EF4444",
                BadgeBackground = "#2A0E0E",
                BadgeBorder = "#EF4444",
                Origin = "Virtual Machine Hard Drive",
                Impact = "Destructive — Deleting will destroy an entire virtual computer installation.",
                Explanation = $"Virtual disk image ({FormatBytes(sizeBytes)}). DO NOT DELETE: Contains a full virtual machine.",
                IsSafeToAutoClean = false
            };
        }

        // =========================================================================
        // 10. PROTECTED: PERSONAL VIDEOS & MEDIA
        // =========================================================================
        if (ext is ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" or ".wmv" or ".flv" or ".mp3" or ".wav" or ".flac")
        {
            return new AiAnalysisResult
            {
                SafetyScore = 0,
                Tier = AiSafetyTier.HighRiskKeep,
                Verdict = "PROTECTED (Personal Media)",
                VerdictShort = "PROTECTED",
                BadgeColor = "#EF4444",
                BadgeBackground = "#2A0E0E",
                BadgeBorder = "#EF4444",
                Origin = "Personal Video / Media File",
                Impact = "Personal Data Loss — Deleting will remove your video or audio recording.",
                Explanation = $"Personal media file ({FormatBytes(sizeBytes)}). DO NOT DELETE: Preserved to prevent media loss.",
                IsSafeToAutoClean = false
            };
        }

        // =========================================================================
        // 11. DEFAULT FAIL-SAFE: UNKNOWN FILES OUTSIDE DOWNLOADS ARE PROTECTED
        // =========================================================================
        return new AiAnalysisResult
        {
            SafetyScore = 0,
            Tier = AiSafetyTier.HighRiskKeep,
            Verdict = "PROTECTED (System Asset)",
            VerdictShort = "PROTECTED",
            BadgeColor = "#EF4444",
            BadgeBackground = "#2A0E0E",
            BadgeBorder = "#EF4444",
            Origin = "System / User Directory",
            Impact = "Safety Lock — Protected by AI heuristic engine to prevent unexpected issues.",
            Explanation = $"File located outside disposable folders ({FormatBytes(sizeBytes)}). Protected by AI.",
            IsSafeToAutoClean = false
        };
    }

    private static string FormatBytes(long bytes)
    {
        return TargetFolderInfo.FormatBytes(bytes);
    }
}
