using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class LargeFileInfo : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);
    public string Category { get; set; } = "Other";
    public string CategoryIcon { get; set; } = "\uE8A5";
    public DateTime LastModified { get; set; }
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? "";
    public string DriveLetter => !string.IsNullOrEmpty(FilePath) && FilePath.Length >= 2 && FilePath[1] == ':' ? FilePath[..2].ToUpperInvariant() : "C:";

    // Deterministic & AI Safety Properties
    private int _safetyScore = 0;
    public int SafetyScore { get => _safetyScore; set { _safetyScore = value; OnPropertyChanged(); } }
    public int AiSafetyScore { get => SafetyScore; set => SafetyScore = value; }
    public SafetyRiskTier SafetyTier { get; set; } = SafetyRiskTier.Protected;

    private string _verdict = "PROTECTED";
    public string Verdict { get => _verdict; set { _verdict = value; OnPropertyChanged(); } }
    public string AiVerdict { get => Verdict; set => Verdict = value; }
    public string VerdictShort { get; set; } = "PROTECTED";

    private string _badgeColor = "#EF4444";
    public string BadgeColor { get => _badgeColor; set { _badgeColor = value; OnPropertyChanged(); } }
    public string AiBadgeColor { get => BadgeColor; set => BadgeColor = value; }

    private string _badgeBackground = "#2A0E0E";
    public string BadgeBackground { get => _badgeBackground; set { _badgeBackground = value; OnPropertyChanged(); } }

    private string _badgeBorder = "#EF4444";
    public string BadgeBorder { get => _badgeBorder; set { _badgeBorder = value; OnPropertyChanged(); } }

    private string _origin = string.Empty;
    public string Origin { get => _origin; set { _origin = value; OnPropertyChanged(); } }
    public string AiOrigin { get => Origin; set => Origin = value; }

    private string _impact = string.Empty;
    public string Impact { get => _impact; set { _impact = value; OnPropertyChanged(); } }
    public string AiImpact { get => Impact; set => Impact = value; }

    private string _explanation = string.Empty;
    public string Explanation { get => _explanation; set { _explanation = value; OnPropertyChanged(); } }
    public string AiExplanation { get => Explanation; set => Explanation = value; }

    private bool _isSafe;
    public bool IsSafe { get => _isSafe; set { _isSafe = value; OnPropertyChanged(); } }
    public bool IsAiSafe { get => IsSafe; set => IsSafe = value; }

    // Online AI Intelligence & Deep Forensics
    private bool _isAiAnalyzing;
    public bool IsAiAnalyzing
    {
        get => _isAiAnalyzing;
        set { if (_isAiAnalyzing != value) { _isAiAnalyzing = value; OnPropertyChanged(); } }
    }

    private bool _isAiOnlineVerified;
    public bool IsAiOnlineVerified
    {
        get => _isAiOnlineVerified;
        set { if (_isAiOnlineVerified != value) { _isAiOnlineVerified = value; OnPropertyChanged(); } }
    }

    private string _aiRecommendation = string.Empty;
    public string AiRecommendation
    {
        get => _aiRecommendation;
        set { if (_aiRecommendation != value) { _aiRecommendation = value; OnPropertyChanged(); } }
    }

    private string _aiProviderLabel = "Deterministic Rules";
    public string AiProviderLabel
    {
        get => _aiProviderLabel;
        set { if (_aiProviderLabel != value) { _aiProviderLabel = value; OnPropertyChanged(); } }
    }

    private OnlineSafetyReport? _onlineReport;
    public OnlineSafetyReport? OnlineReport
    {
        get => _onlineReport;
        set { if (_onlineReport != value) { _onlineReport = value; OnPropertyChanged(); } }
    }

    public void ApplyOnlineReport(OnlineSafetyReport report)
    {
        OnlineReport = report;
        AiVerdict = report.VerdictDisplay;
        VerdictShort = report.VerdictDisplay;
        SafetyScore = report.SafetyScore;
        BadgeColor = report.BadgeColor;
        BadgeBackground = report.BadgeBackground;
        BadgeBorder = report.BadgeBorder;
        Origin = report.Origin;
        Impact = report.ImpactIfDeleted;
        Explanation = report.WhatIsIt;
        AiRecommendation = report.Recommendation;
        IsSafe = report.Verdict is OnlineSafetyVerdict.SafeToDelete;
        IsAiOnlineVerified = true;
        IsAiAnalyzing = false;
        AiProviderLabel = report.ProviderUsed;

        OnPropertyChanged(nameof(AiVerdict));
        OnPropertyChanged(nameof(VerdictShort));
        OnPropertyChanged(nameof(SafetyScore));
        OnPropertyChanged(nameof(BadgeColor));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeBorder));
        OnPropertyChanged(nameof(AiBadgeColor));
        OnPropertyChanged(nameof(Origin));
        OnPropertyChanged(nameof(AiOrigin));
        OnPropertyChanged(nameof(Impact));
        OnPropertyChanged(nameof(AiImpact));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(AiExplanation));
        OnPropertyChanged(nameof(AiRecommendation));
        OnPropertyChanged(nameof(IsSafe));
        OnPropertyChanged(nameof(IsAiSafe));
        OnPropertyChanged(nameof(IsAiOnlineVerified));
        OnPropertyChanged(nameof(IsAiAnalyzing));
        OnPropertyChanged(nameof(AiProviderLabel));
        OnPropertyChanged(nameof(OnlineReport));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LargeFileScanResult
{
    public List<LargeFileInfo> Files { get; set; } = new();
    public int TotalDiscovered { get; set; }
    public int DisplayLimit { get; set; }
    public bool WasTruncated => TotalDiscovered > DisplayLimit;
    public long TotalBytesScanned { get; set; }
    public int DirectoriesScanned { get; set; }
    public List<string> InaccessibleDirectories { get; set; } = new();
    public bool ScanCompleted { get; set; } = true;
}

public static class LargeFileHunterService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public int wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    private const int FO_DELETE = 0x0003;
    private const short FOF_ALLOWUNDO = 0x0040;
    private const short FOF_NOCONFIRMATION = 0x0010;
    private const short FOF_SILENT = 0x0004;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "$RECYCLE.BIN",
        "System Volume Information",
        "Windows",
        "Recovery",
        "Boot",
        "WinSxS",
        "node_modules",
        ".git"
    };

    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "dumpstack.log",
        "bootmgr"
    };

    public static async Task<LargeFileScanResult> ScanLargeFilesAsync(
        long minSizeBytes = 50L * 1024 * 1024,
        string targetScope = "ALL",
        int maxResults = 250,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var allResults = new List<LargeFileInfo>();
            var inaccessibleDirs = new List<string>();
            var rootsToScan = ResolveRoots(targetScope);

            int totalRoots = rootsToScan.Count;
            int currentRootIndex = 0;
            int dirsScanned = 0;
            long totalBytesScanned = 0;
            bool scanCompleted = true;

            foreach (var root in rootsToScan)
            {
                if (ct.IsCancellationRequested) { scanCompleted = false; break; }
                currentRootIndex++;

                if (!Directory.Exists(root)) continue;

                var dirStack = new Stack<(string Path, int Depth)>();
                dirStack.Push((root, 0));

                const int maxDepth = 12;

                while (dirStack.Count > 0)
                {
                    if (ct.IsCancellationRequested) { scanCompleted = false; break; }

                    var (currentDir, depth) = dirStack.Pop();
                    DirectoryInfo dirInfo;

                    try
                    {
                        dirInfo = new DirectoryInfo(currentDir);
                        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if (ExcludedDirectoryNames.Contains(dirInfo.Name)) continue;
                    }
                    catch
                    {
                        continue;
                    }

                    dirsScanned++;

                    if (depth < maxDepth)
                    {
                        try
                        {
                            var subDirs = dirInfo.EnumerateDirectories("*", new EnumerationOptions
                            {
                                IgnoreInaccessible = true,
                                RecurseSubdirectories = false,
                                AttributesToSkip = FileAttributes.ReparsePoint
                            });

                            foreach (var subDir in subDirs)
                            {
                                if (ExcludedDirectoryNames.Contains(subDir.Name)) continue;
                                dirStack.Push((subDir.FullName, depth + 1));
                            }
                        }
                        catch (Exception ex)
                        {
                            inaccessibleDirs.Add(currentDir);
                            System.Diagnostics.Trace.WriteLine($"[Deltempo] Subdir enumeration suppressed: {ex.Message}");
                        }
                    }

                    try
                    {
                        var files = dirInfo.EnumerateFiles("*", new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = false,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        });

                        foreach (var file in files)
                        {
                            if (ct.IsCancellationRequested) { scanCompleted = false; break; }
                            if (ExcludedFileNames.Contains(file.Name)) continue;

                            try
                            {
                                long length = file.Length;
                                totalBytesScanned += length;
                                if (length >= minSizeBytes)
                                {
                                    var (cat, icon) = ClassifyFileCategory(file.Extension);
                                    var safety = FileSafetyEngine.Analyze(file.FullName, fileName: file.Name, category: cat, sizeBytes: length, lastModified: file.LastWriteTime);

                                    allResults.Add(new LargeFileInfo
                                    {
                                        FilePath = file.FullName,
                                        FileName = file.Name,
                                        SizeBytes = length,
                                        LastModified = file.LastWriteTime,
                                        Category = cat,
                                        CategoryIcon = icon,
                                        SafetyScore = safety.SafetyScore,
                                        SafetyTier = safety.Tier,
                                        Verdict = safety.Verdict,
                                        VerdictShort = safety.VerdictShort,
                                        BadgeColor = safety.BadgeColor,
                                        BadgeBackground = safety.BadgeBackground,
                                        BadgeBorder = safety.BadgeBorder,
                                        Origin = safety.Origin,
                                        Impact = safety.Impact,
                                        Explanation = safety.Explanation,
                                        IsSafe = safety.IsSafeToClean,
                                        IsSelected = false
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Trace.WriteLine($"[Deltempo] File query suppressed: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        inaccessibleDirs.Add(currentDir);
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] File enumeration suppressed: {ex.Message}");
                    }
                }

                progress?.Report((int)((double)currentRootIndex / totalRoots * 100));
            }

            var sorted = allResults.OrderByDescending(f => f.SizeBytes).ToList();
            int totalCount = sorted.Count;
            var displayFiles = sorted.Take(maxResults).ToList();

            return new LargeFileScanResult
            {
                Files = displayFiles,
                TotalDiscovered = totalCount,
                DisplayLimit = maxResults,
                TotalBytesScanned = totalBytesScanned,
                DirectoriesScanned = dirsScanned,
                InaccessibleDirectories = inaccessibleDirs,
                ScanCompleted = scanCompleted
            };
        }, ct);
    }

    public static List<string> GetAvailableDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                .Select(d => d.Name.TrimEnd('\\'))
                .ToList();
        }
        catch
        {
            return new List<string> { "C:" };
        }
    }

    private static List<string> ResolveRoots(string targetScope)
    {
        var roots = new List<string>();
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.Equals(targetScope, "USER", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(userProfile)) roots.Add(userProfile);

            string oneDrive = Path.Combine(userProfile, "OneDrive");
            if (Directory.Exists(oneDrive) && !roots.Contains(oneDrive)) roots.Add(oneDrive);

            return roots;
        }

        // If specific path, user folder, or drive passed
        if (!string.Equals(targetScope, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(targetScope))
        {
            // 1. Direct directory or full path
            if (Directory.Exists(targetScope))
            {
                roots.Add(Path.GetFullPath(targetScope));
                return roots;
            }

            // 2. Relative to current working directory
            string localPath = Path.GetFullPath(targetScope);
            if (Directory.Exists(localPath))
            {
                roots.Add(localPath);
                return roots;
            }

            // 3. User subfolder (e.g. "Downloads", "Documents", "Desktop", "Videos")
            string userFolder = Path.Combine(userProfile, targetScope);
            if (Directory.Exists(userFolder))
            {
                roots.Add(userFolder);
                return roots;
            }

            // 4. Drive letter (e.g. "C", "C:", "D:")
            string cleanDrive = targetScope.Trim().TrimEnd('\\');
            if (cleanDrive.Length == 1) cleanDrive += ":";
            cleanDrive += "\\";

            if (Directory.Exists(cleanDrive))
            {
                roots.Add(cleanDrive);
                return roots;
            }
        }

        // ALL Drives
        try
        {
            var fixedDrives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                .ToList();

            foreach (var drive in fixedDrives)
            {
                roots.Add(drive.RootDirectory.FullName);
            }
        }
        catch
        {
            roots.Add(@"C:\");
        }

        return roots;
    }

    public static (string Category, string Icon) ClassifyFileCategory(string ext)
    {
        var lower = ext.ToLowerInvariant();
        return lower switch
        {
            ".iso" or ".msi" or ".pkg" or ".dmg" or ".setup" => ("Installer / ISO", "\uE8B7"),
            ".exe" or ".dll" or ".sys" => ("Application / Binary", "\uE756"),
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" => ("Video / Media", "\uE714"),
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or ".tgz" => ("Archive", "\uF012"),
            ".pak" or ".bundle" or ".assets" or ".obb" or ".wad" => ("Game Asset / Pak", "\uE7FC"),
            ".gguf" or ".safetensors" or ".bin" or ".onnx" or ".pt" or ".pth" or ".ckpt" or ".model" => ("AI Model / Weights", "\uE943"),
            ".vmdk" or ".vhd" or ".vhdx" or ".qcow2" or ".img" or ".vdi" => ("Virtual Disk / Image", "\uEDA2"),
            ".dmp" or ".log" or ".bak" or ".old" or ".tmp" or ".part" or ".crdownload" => ("Dump / Temp / Download", "\uE9F9"),
            ".pdf" or ".psd" or ".ai" or ".blend" or ".prproj" or ".aep" or ".dwg" => ("Creative / Project", "\uE790"),
            _ => ("Document / Other", "\uE8A5")
        };
    }

    public static bool MoveToRecycleBin(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath)) return false;

            // Strict TOCTOU pre-deletion revalidation
            if (File.Exists(filePath))
            {
                if (!WinTempCleaner.Core.Cleaning.CleanupExecutor.RevalidateBeforeDeletion(filePath, allowedRoot: string.Empty, expectedSize: -1, out string failureReason))
                {
                    Trace.WriteLine($"[Deltempo] MoveToRecycleBin blocked by safety revalidation: {failureReason} for {filePath}");
                    return false;
                }
            }

            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = filePath + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };

            int res = SHFileOperation(ref shf);
            return res == 0;
        }
        catch
        {
            return false;
        }
    }

    public static (int Succeeded, int Failed, long TotalFreedBytes) BatchMoveToRecycleBin(IEnumerable<LargeFileInfo> files)
    {
        int succ = 0;
        int fail = 0;
        long freed = 0;

        foreach (var item in files)
        {
            if (MoveToRecycleBin(item.FilePath))
            {
                succ++;
                freed += item.SizeBytes;
            }
            else
            {
                fail++;
            }
        }

        return (succ, fail, freed);
    }

    public static void OpenWindowsRecycleBin()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:RecycleBinFolder",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static async Task AnalyzeItemWithAiAsync(LargeFileInfo item, CancellationToken ct = default)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FilePath)) return;

        item.IsAiAnalyzing = true;
        try
        {
            var report = await OnlineFileIntelligenceService.AnalyzeFileAsync(item.FilePath, ct);
            item.ApplyOnlineReport(report);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo AI] Item analysis error: {ex.Message}");
            item.IsAiAnalyzing = false;
        }
    }

    public static async Task BatchAnalyzeWithAiAsync(
        IEnumerable<LargeFileInfo> items,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var list = items.ToList();
        int total = list.Count;
        if (total == 0) return;

        int processed = 0;
        foreach (var item in list)
        {
            if (ct.IsCancellationRequested) break;
            await AnalyzeItemWithAiAsync(item, ct);
            processed++;
            progress?.Report((int)((double)processed / total * 100));
        }
    }
}
