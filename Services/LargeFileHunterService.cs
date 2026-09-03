using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    // AI Safety Properties
    public int AiSafetyScore { get; set; } = 0;
    public AiSafetyTier AiSafetyTier { get; set; } = AiSafetyTier.HighRiskKeep;
    public string AiVerdict { get; set; } = "PROTECTED";
    public string VerdictShort { get; set; } = "PROTECTED";
    public string AiBadgeColor { get; set; } = "#EF4444";
    public string BadgeBackground { get; set; } = "#2A0E0E";
    public string BadgeBorder { get; set; } = "#EF4444";
    public string AiOrigin { get; set; } = string.Empty;
    public string AiImpact { get; set; } = string.Empty;
    public string AiExplanation { get; set; } = string.Empty;
    public bool IsAiSafe { get; set; }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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

    public static async Task<List<LargeFileInfo>> ScanLargeFilesAsync(
        long minSizeBytes = 50L * 1024 * 1024,
        string targetScope = "ALL",
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<LargeFileInfo>();
            var rootsToScan = ResolveRoots(targetScope);

            int totalRoots = rootsToScan.Count;
            int currentRootIndex = 0;

            foreach (var root in rootsToScan)
            {
                if (ct.IsCancellationRequested) break;
                currentRootIndex++;

                if (!Directory.Exists(root)) continue;

                // Stack-based iterative DFS up to MaxRecursionDepth = 12
                var dirStack = new Stack<(string Path, int Depth)>();
                dirStack.Push((root, 0));

                const int maxDepth = 12;

                while (dirStack.Count > 0)
                {
                    if (ct.IsCancellationRequested) break;

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

                    // Enumerate subdirectories safely
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
                            System.Diagnostics.Trace.WriteLine($"[Deltempo] Subdir enumeration suppressed: {ex.Message}");
                        }
                    }

                    // Enumerate files safely
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
                            if (ct.IsCancellationRequested) break;
                            if (ExcludedFileNames.Contains(file.Name)) continue;

                            try
                            {
                                long length = file.Length;
                                if (length >= minSizeBytes)
                                {
                                    var (cat, icon) = ClassifyFileCategory(file.Extension);
                                    var aiResult = AiFileSafetyService.AnalyzeFile(file.FullName, file.Name, cat, length, file.LastWriteTime);

                                    results.Add(new LargeFileInfo
                                    {
                                        FilePath = file.FullName,
                                        FileName = file.Name,
                                        SizeBytes = length,
                                        LastModified = file.LastWriteTime,
                                        Category = cat,
                                        CategoryIcon = icon,
                                        AiSafetyScore = aiResult.SafetyScore,
                                        AiSafetyTier = aiResult.Tier,
                                        AiVerdict = aiResult.Verdict,
                                        VerdictShort = aiResult.VerdictShort,
                                        AiBadgeColor = aiResult.BadgeColor,
                                        BadgeBackground = aiResult.BadgeBackground,
                                        BadgeBorder = aiResult.BadgeBorder,
                                        AiOrigin = aiResult.Origin,
                                        AiImpact = aiResult.Impact,
                                        AiExplanation = aiResult.Explanation,
                                        IsAiSafe = aiResult.IsSafeToAutoClean,
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
                        System.Diagnostics.Trace.WriteLine($"[Deltempo] File enumeration suppressed: {ex.Message}");
                    }
                }

                progress?.Report((int)((double)currentRootIndex / totalRoots * 100));
            }

            return results.OrderByDescending(f => f.SizeBytes).Take(250).ToList();
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

        // If specific drive passed (e.g. "C:", "D:")
        if (!string.Equals(targetScope, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(targetScope))
        {
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
}
