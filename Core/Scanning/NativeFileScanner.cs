using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace WinTempCleaner.Core.Scanning;

/// <summary>
/// Lightweight discovered file representation from the low-level scanner.
/// </summary>
public readonly record struct DiscoveredFileItem(
    string FullPath,
    string FileName,
    long SizeBytes,
    DateTime LastWriteTimeUtc,
    FileAttributes Attributes);

/// <summary>
/// High-speed native Win32 directory traversal engine.
/// Utilizes FindFirstFileExW with FindExInfoBasic and FIND_FIRST_EX_LARGE_FETCH
/// to minimize kernel transitions and bypass .NET Managed Enumeration object allocation overhead.
/// Automatically detects and skips Reparse Points (symlinks, junctions) for security.
/// </summary>
public static class NativeFileScanner
{
    #region Win32 Native Interop

    private enum FINDEX_INFO_LEVELS
    {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1,
        FindExInfoMaxInfoLevel
    }

    private enum FINDEX_SEARCH_OPS
    {
        FindExSearchNameMatch = 0,
        FindExSearchLimitToDirectories = 1,
        FindExSearchLimitToDevices = 2,
        FindExSearchMaxSearchOp
    }

    private const uint FIND_FIRST_EX_CASE_SENSITIVE = 0x00000001;
    private const uint FIND_FIRST_EX_LARGE_FETCH = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    private sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindClose(IntPtr hFindFile);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFindHandle FindFirstFileExW(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATAW lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        uint dwAdditionalFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextFileW(SafeFindHandle hFindFile, out WIN32_FIND_DATAW lpFindFileData);

    private const int ERROR_NO_MORE_FILES = 18;
    private const int ERROR_FILE_NOT_FOUND = 2;
    private const int ERROR_PATH_NOT_FOUND = 3;
    private const int ERROR_ACCESS_DENIED = 5;

    #endregion

    /// <summary>
    /// Converts Win32 FILETIME struct to UTC DateTime.
    /// </summary>
    private static DateTime ToDateTimeUtc(ref System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        long fileTime = ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        return DateTime.FromFileTimeUtc(fileTime);
    }

    /// <summary>
    /// Scans a root directory recursively using Win32 FindFirstFileExW with large fetch.
    /// Emits discovered files to a callback delegate. Reparse points are strictly skipped.
    /// </summary>
    public static void ScanDirectory(
        string rootDirectory,
        Action<DiscoveredFileItem> fileCallback,
        CancellationToken ct = default,
        bool recurse = true)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            return;

        var dirQueue = new Queue<string>();
        dirQueue.Enqueue(rootDirectory);

        while (dirQueue.Count > 0)
        {
            if (ct.IsCancellationRequested) break;
            string currentDir = dirQueue.Dequeue();

            string searchPattern = Path.Combine(currentDir, "*");

            using var findHandle = FindFirstFileExW(
                searchPattern,
                FINDEX_INFO_LEVELS.FindExInfoBasic,
                out WIN32_FIND_DATAW findData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                FIND_FIRST_EX_LARGE_FETCH);

            if (findHandle.IsInvalid)
                continue;

            do
            {
                if (ct.IsCancellationRequested) break;

                string fileName = findData.cFileName;
                if (fileName == "." || fileName == "..")
                    continue;

                var attributes = (FileAttributes)findData.dwFileAttributes;

                // Security check: Ignore reparse points (symlinks, directory junctions, volume mount points)
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                string fullPath = Path.Combine(currentDir, fileName);

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (recurse)
                    {
                        dirQueue.Enqueue(fullPath);
                    }
                }
                else
                {
                    long length = ((long)findData.nFileSizeHigh << 32) | (uint)findData.nFileSizeLow;
                    var writeUtc = ToDateTimeUtc(ref findData.ftLastWriteTime);

                    fileCallback(new DiscoveredFileItem(
                        FullPath: fullPath,
                        FileName: fileName,
                        SizeBytes: length,
                        LastWriteTimeUtc: writeUtc,
                        Attributes: attributes));
                }
            }
            while (FindNextFileW(findHandle, out findData));
        }
    }

    /// <summary>
    /// Asynchronously streams discovered files via a bounded Channel.
    /// Enables pipelined producer-consumer processing where files are evaluated concurrently with disk enumeration.
    /// </summary>
    public static IAsyncEnumerable<DiscoveredFileItem> EnumerateFilesAsync(
        string rootDirectory,
        CancellationToken ct = default,
        bool recurse = true,
        int channelCapacity = 8192)
    {
        var channel = Channel.CreateBounded<DiscoveredFileItem>(new BoundedChannelOptions(channelCapacity)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _ = Task.Run(() =>
        {
            try
            {
                ScanDirectory(rootDirectory, item =>
                {
                    // Synchronously write with wait to respect backpressure
                    while (!channel.Writer.TryWrite(item))
                    {
                        if (ct.IsCancellationRequested) break;
                        channel.Writer.WaitToWriteAsync(ct).AsTask().GetAwaiter().GetResult();
                    }
                }, ct, recurse);
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
                return;
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, ct);

        return channel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Computes aggregated size, count, and top largest files with minimal overhead.
    /// </summary>
    public static (long totalBytes, int fileCount, List<DiscoveredFileItem> topFiles) ScanDirectoryStats(
        string rootDirectory,
        DateTime? modifiedBeforeUtc = null,
        int topFileCount = 30,
        CancellationToken ct = default)
    {
        long totalBytes = 0;
        int fileCount = 0;
        var topList = new List<DiscoveredFileItem>(topFileCount + 1);

        ScanDirectory(rootDirectory, item =>
        {
            if (modifiedBeforeUtc.HasValue && item.LastWriteTimeUtc > modifiedBeforeUtc.Value)
                return;

            totalBytes += item.SizeBytes;
            fileCount++;

            // Maintain top file list
            if (topList.Count < topFileCount)
            {
                topList.Add(item);
                if (topList.Count == topFileCount)
                {
                    topList.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                }
            }
            else if (item.SizeBytes > topList[^1].SizeBytes)
            {
                topList[^1] = item;
                topList.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            }
        }, ct);

        return (totalBytes, fileCount, topList);
    }
}
