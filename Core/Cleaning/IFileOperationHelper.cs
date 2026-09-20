using System.IO;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Modern Windows Shell file operation helper utilizing COM IFileOperation (Windows Vista+)
/// and batched SHFileOperation fallback.
/// Eliminates single-thread lock contention and enables high-throughput batch recycling.
/// </summary>
public static class IFileOperationHelper
{
    #region COM IFileOperation Interfaces & Guid

    [ComImport]
    [Guid("947aab5f-0a45-4c40-b45d-107325a991b3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        uint Advise(IntPtr pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(FileOperationFlags dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog([MarshalAs(UnmanagedType.Interface)] object popd);
        void SetProperties([MarshalAs(UnmanagedType.Interface)] object pproparray);
        void SetOwnerWindow(IntPtr hwndOwner);
        void ApplyPropertiesToItem([MarshalAs(UnmanagedType.Interface)] object psi);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        void RenameItem([MarshalAs(UnmanagedType.Interface)] object psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, [MarshalAs(UnmanagedType.Interface)] object pfopsItem);
        void RenameItems([MarshalAs(UnmanagedType.Interface)] object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem([MarshalAs(UnmanagedType.Interface)] object psiItem, [MarshalAs(UnmanagedType.Interface)] object psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, [MarshalAs(UnmanagedType.Interface)] object pfopsItem);
        void MoveItems([MarshalAs(UnmanagedType.Interface)] object punkItems, [MarshalAs(UnmanagedType.Interface)] object psiDestinationFolder);
        void CopyItem([MarshalAs(UnmanagedType.Interface)] object psiItem, [MarshalAs(UnmanagedType.Interface)] object psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, [MarshalAs(UnmanagedType.Interface)] object pfopsItem);
        void CopyItems([MarshalAs(UnmanagedType.Interface)] object punkItems, [MarshalAs(UnmanagedType.Interface)] object psiDestinationFolder);
        void DeleteItem([MarshalAs(UnmanagedType.Interface)] object psiItem, [MarshalAs(UnmanagedType.Interface)] object pfopsItem);
        void DeleteItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        void PerformOperations();
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetAnyOperationsAborted();
    }

    [ComImport]
    [Guid("3ad05575-8857-4850-9277-11b85bdb8e09")]
    private class FileOperationClass
    {
    }

    [Flags]
    private enum FileOperationFlags : uint
    {
        FOF_MULTIDESTFILES = 0x0001,
        FOF_CONFIRMMOUSE = 0x0002,
        FOF_SILENT = 0x0004,
        FOF_RENAMEONCOLLISION = 0x0008,
        FOF_NOCONFIRMATION = 0x0010,
        FOF_WANTMAPPINGHANDLE = 0x0020,
        FOF_ALLOWUNDO = 0x0040,
        FOF_FILESONLY = 0x0080,
        FOF_SIMPLEPROGRESS = 0x0100,
        FOF_NOCONFIRMMKDIR = 0x0200,
        FOF_NOERRORUI = 0x0400,
        FOF_NOCOPYSECURITYATTRIBS = 0x0800,
        FOF_NORECURSION = 0x1000,
        FOF_NO_CONNECTED_ELEMENTS = 0x2000,
        FOF_WANTNUKEWARNING = 0x4000,
        FOF_NORECURSEREPARSE = 0x8000,
        FOFX_NOSKIPJUNCTIONS = 0x00010000,
        FOFX_PREFERHARDLINK = 0x00020000,
        FOFX_SHOWELEVATIONPROMPT = 0x00040000,
        FOFX_EARLYFAILURE = 0x00100000,
        FOFX_PRESERVEFILEEXTENSIONS = 0x00200000,
        FOFX_KEEPNEWERFILE = 0x00400000,
        FOFX_NOCOPYHOOKS = 0x00800000,
        FOFX_NOMINIMIZEBOX = 0x01000000,
        FOFX_MOVEACLSACROSSVOLUMES = 0x02000000,
        FOFX_DONTDISPLAYSOURCEPATH = 0x04000000,
        FOFX_DONTDISPLAYDESTPATH = 0x08000000,
        FOFX_RECYCLEONDELETE = 0x00080000
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    private static readonly Guid IShellItemGuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    #endregion

    #region Win32 Legacy SHFileOperation Batch Fallback

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCTW
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public int wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    private const int FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCTW FileOp);

    #endregion

    /// <summary>
    /// Recycles a single file safely without locking across threads.
    /// Prefers COM IFileOperation, falling back to SHFileOperationW on error.
    /// </summary>
    public static bool RecycleFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
            return false;

        try
        {
            var fileOp = (IFileOperation)new FileOperationClass();
            fileOp.SetOperationFlags(
                FileOperationFlags.FOF_ALLOWUNDO |
                FileOperationFlags.FOF_NOCONFIRMATION |
                FileOperationFlags.FOF_SILENT |
                FileOperationFlags.FOF_NOERRORUI);

            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, IShellItemGuid, out object shellItem);
            if (hr == 0 && shellItem != null)
            {
                fileOp.DeleteItem(shellItem, null!);
                fileOp.PerformOperations();
                Marshal.ReleaseComObject(shellItem);
                Marshal.ReleaseComObject(fileOp);
                return !File.Exists(filePath) && !Directory.Exists(filePath);
            }
        }
        catch
        {
            // Fallback to SHFileOperation below
        }

        // Fallback: SHFileOperationW with double-null termination
        try
        {
            var shOp = new SHFILEOPSTRUCTW
            {
                wFunc = FO_DELETE,
                pFrom = filePath + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
            };
            int result = SHFileOperationW(ref shOp);
            return result == 0 && !File.Exists(filePath) && !Directory.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Recycles a batch of files in a single pass using COM IFileOperation or batch SHFileOperationW.
    /// Orders of magnitude faster than serial one-by-one file recycling.
    /// </summary>
    public static int RecycleBatch(IReadOnlyList<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0) return 0;

        int succeeded = 0;

        // Try batch COM IFileOperation
        try
        {
            var fileOp = (IFileOperation)new FileOperationClass();
            fileOp.SetOperationFlags(
                FileOperationFlags.FOF_ALLOWUNDO |
                FileOperationFlags.FOF_NOCONFIRMATION |
                FileOperationFlags.FOF_SILENT |
                FileOperationFlags.FOF_NOERRORUI);

            var shellItems = new List<object>(filePaths.Count);
            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, IShellItemGuid, out object item);
                if (hr == 0 && item != null)
                {
                    fileOp.DeleteItem(item, null!);
                    shellItems.Add(item);
                }
            }

            if (shellItems.Count > 0)
            {
                fileOp.PerformOperations();

                foreach (var item in shellItems)
                {
                    Marshal.ReleaseComObject(item);
                }
                Marshal.ReleaseComObject(fileOp);

                foreach (var path in filePaths)
                {
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        succeeded++;
                    }
                }
                return succeeded;
            }
        }
        catch
        {
            // Fall through to SHFileOperation batch fallback
        }

        // Fallback: Group in 100-file multi-string double-null terminated buffers
        const int chunkSize = 100;
        for (int i = 0; i < filePaths.Count; i += chunkSize)
        {
            var chunk = filePaths.Skip(i).Take(chunkSize).ToList();
            var sb = new System.Text.StringBuilder();
            foreach (var path in chunk)
            {
                sb.Append(path).Append('\0');
            }
            sb.Append('\0');

            try
            {
                var shOp = new SHFILEOPSTRUCTW
                {
                    wFunc = FO_DELETE,
                    pFrom = sb.ToString(),
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                };
                int res = SHFileOperationW(ref shOp);
                if (res == 0)
                {
                    foreach (var path in chunk)
                    {
                        if (!File.Exists(path) && !Directory.Exists(path))
                        {
                            succeeded++;
                        }
                    }
                }
            }
            catch
            {
                // Single-item fallback for the remainder of this chunk
                foreach (var p in chunk)
                {
                    if (RecycleFile(p)) succeeded++;
                }
            }
        }

        return succeeded;
    }
}
