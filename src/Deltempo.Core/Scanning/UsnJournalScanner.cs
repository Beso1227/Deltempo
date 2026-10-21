using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinTempCleaner.Core.Scanning;

/// <summary>
/// NTFS USN (Update Sequence Number) Change Journal & MFT scanner.
/// Reads the Master File Table directly via FSCTL_ENUM_USN_DATA when running elevated on NTFS volumes,
/// providing ultra-fast disk-wide indexing (1M+ files in seconds).
/// Gracefully detects when USN enumeration is unavailable and signals fallback to NativeFileScanner.
/// </summary>
public static class UsnJournalScanner
{
    #region Win32 P/Invoke Constants & Structs

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;

    private const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
    private const uint FSCTL_ENUM_USN_DATA = 0x000900b3;

    [StructLayout(LayoutKind.Sequential)]
    private struct USN_JOURNAL_DATA
    {
        public ulong UsnJournalID;
        public long FirstUsn;
        public long NextUsn;
        public long LowestValidUsn;
        public long MaxUsn;
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_ENUM_DATA_V0
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct USN_RECORD_HEADER
    {
        public uint RecordLength;
        public ushort MajorVersion;
        public ushort MinorVersion;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref MFT_ENUM_DATA_V0 lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        out USN_JOURNAL_DATA lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    #endregion

    /// <summary>
    /// Checks whether the specified drive is an NTFS volume with active USN Journal and accessible by the current process.
    /// </summary>
    public static bool IsUsnJournalSupported(string driveRoot)
    {
        if (string.IsNullOrWhiteSpace(driveRoot)) return false;

        try
        {
            string drive = driveRoot.TrimEnd('\\', '/');
            if (drive.Length == 2 && drive[1] == ':')
            {
                var driveInfo = new DriveInfo(drive);
                if (!driveInfo.IsReady || !string.Equals(driveInfo.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            string volumePath = $@"\\.\{drive}";
            using var handle = CreateFileW(
                volumePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid) return false;

            bool ok = DeviceIoControl(
                handle,
                FSCTL_QUERY_USN_JOURNAL,
                IntPtr.Zero,
                0,
                out USN_JOURNAL_DATA journalData,
                (uint)Marshal.SizeOf<USN_JOURNAL_DATA>(),
                out _,
                IntPtr.Zero);

            return ok && journalData.UsnJournalID != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fast representation of an MFT record entry.
    /// </summary>
    public readonly record struct UsnFileEntry(
        ulong FileReferenceNumber,
        ulong ParentFileReferenceNumber,
        string FileName,
        uint FileAttributes);

    /// <summary>
    /// Enumerates all file and directory entries on an NTFS drive via USN change journal.
    /// Emits UsnFileEntry records to a callback delegate.
    /// </summary>
    public static bool EnumerateVolumeEntries(
        string driveRoot,
        Action<UsnFileEntry> entryCallback,
        CancellationToken ct = default)
    {
        string drive = driveRoot.TrimEnd('\\', '/');
        string volumePath = $@"\\.\{drive}";

        using var handle = CreateFileW(
            volumePath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid) return false;

        var journalData = new USN_JOURNAL_DATA();
        if (!DeviceIoControl(
            handle,
            FSCTL_QUERY_USN_JOURNAL,
            IntPtr.Zero,
            0,
            out journalData,
            (uint)Marshal.SizeOf<USN_JOURNAL_DATA>(),
            out _,
            IntPtr.Zero))
        {
            return false;
        }

        var mftEnumData = new MFT_ENUM_DATA_V0
        {
            StartFileReferenceNumber = 0,
            LowUsn = 0,
            HighUsn = journalData.NextUsn
        };

        const int bufferSize = 64 * 1024; // 64KB buffer for bulk MFT chunks
        byte[] buffer = new byte[bufferSize];

        while (!ct.IsCancellationRequested)
        {
            if (!DeviceIoControl(
                handle,
                FSCTL_ENUM_USN_DATA,
                ref mftEnumData,
                (uint)Marshal.SizeOf<MFT_ENUM_DATA_V0>(),
                buffer,
                bufferSize,
                out uint bytesReturned,
                IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                // ERROR_HANDLE_EOF (38) or ERROR_NO_MORE_ITEMS indicates completion
                break;
            }

            if (bytesReturned <= 8)
                break;

            // The first 8 bytes of the output buffer contain the next USN for subsequent calls
            mftEnumData.StartFileReferenceNumber = BitConverter.ToUInt64(buffer, 0);

            int offset = 8;
            while (offset < bytesReturned && !ct.IsCancellationRequested)
            {
                uint recordLength = BitConverter.ToUInt32(buffer, offset);
                if (recordLength == 0) break;

                ushort majorVersion = BitConverter.ToUInt16(buffer, offset + 4);
                if (majorVersion == 2)
                {
                    ulong fileRef = BitConverter.ToUInt64(buffer, offset + 8);
                    ulong parentRef = BitConverter.ToUInt64(buffer, offset + 16);
                    uint fileAttributes = BitConverter.ToUInt32(buffer, offset + 32);
                    ushort fileNameLength = BitConverter.ToUInt16(buffer, offset + 56);
                    ushort fileNameOffset = BitConverter.ToUInt16(buffer, offset + 58);

                    if (fileNameLength > 0 && offset + fileNameOffset + fileNameLength <= bytesReturned)
                    {
                        string fileName = System.Text.Encoding.Unicode.GetString(buffer, offset + fileNameOffset, fileNameLength);
                        entryCallback(new UsnFileEntry(fileRef, parentRef, fileName, fileAttributes));
                    }
                }

                offset += (int)recordLength;
            }
        }

        return true;
    }
}
