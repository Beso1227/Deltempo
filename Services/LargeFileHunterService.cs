using System.IO;
using System.Runtime.InteropServices;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class LargeFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);
    public string Category { get; set; } = "Other";
    public DateTime LastModified { get; set; }
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? "";
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

    public static async Task<List<LargeFileInfo>> ScanLargeFilesAsync(long minSizeBytes = 50L * 1024 * 1024, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            var results = new List<LargeFileInfo>();

            var searchRoots = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            };

            int folderIndex = 0;
            foreach (var root in searchRoots)
            {
                folderIndex++;
                progress?.Report((int)((double)folderIndex / searchRoots.Count * 100));

                if (!Directory.Exists(root))
                    continue;

                try
                {
                    var opt = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        MaxRecursionDepth = 4,
                        ReturnSpecialDirectories = false
                    };

                    var dirInfo = new DirectoryInfo(root);
                    foreach (var file in dirInfo.EnumerateFiles("*", opt))
                    {
                        try
                        {
                            if (file.Length >= minSizeBytes)
                            {
                                results.Add(new LargeFileInfo
                                {
                                    FilePath = file.FullName,
                                    FileName = file.Name,
                                    SizeBytes = file.Length,
                                    LastModified = file.LastWriteTime,
                                    Category = ClassifyFileCategory(file.Extension)
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }

            return results.OrderByDescending(f => f.SizeBytes).Take(150).ToList();
        });
    }

    public static bool MoveToRecycleBin(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

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

    private static string ClassifyFileCategory(string ext)
    {
        var lower = ext.ToLowerInvariant();
        return lower switch
        {
            ".iso" or ".exe" or ".msi" or ".pkg" or ".dmg" => "Installer / ISO",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" => "Video / Media",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archive",
            ".dmp" or ".log" or ".bak" or ".old" or ".tmp" => "Dump / Backup",
            ".vmdk" or ".vhd" or ".vhdx" or ".qcow2" => "Virtual Disk",
            _ => "Document / Other"
        };
    }
}
