using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace WinTempCleaner.Models;

/// <summary>Test shape. Defaults are a 128 MB file read twice, measured honestly.</summary>
public sealed record DriveBenchmarkOptions(
    long FileSizeBytes = 128L * 1024 * 1024,
    int ReadPasses = 2,
    int ChunkSizeBytes = 1024 * 1024)
{
    /// <summary>Smaller shape for tests: 32 MB, one read pass.</summary>
    public static DriveBenchmarkOptions Quick { get; } = new(32L * 1024 * 1024, 1);
}

public static partial class DriveHealthService
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileBegin = 0;
    private const uint FileFlagNoBuffering = 0x20000000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint CacheAlignment = 4096;
    private const long BytesPerMegabyte = 1024L * 1024L;
    private const long MinimumFreeSpaceBytes = 64L * 1024 * 1024;
    private const long MinimumTestSizeBytes = 16L * 1024 * 1024;

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
    private static extern bool ReadFile(
        SafeFileHandle hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    /// <summary>
    /// Real sequential write + read benchmark on one drive.
    ///
    /// The write is a normal FileStream with <see cref="FileOptions.WriteThrough"/>
    /// and a flush-to-disk, so the number is what reached the medium. The read
    /// opens the file with FILE_FLAG_NO_BUFFERING, which bypasses the Windows file
    /// cache - otherwise the second pass would measure RAM, not the drive. The
    /// temporary file is always removed.
    /// </summary>
    public static Task<DriveBenchmarkResult> RunReadWriteBenchmarkAsync(
        string driveLetter,
        DriveBenchmarkOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(driveLetter);

        return Task.Run(() =>
            RunBenchmarkCore(driveLetter, options ?? new DriveBenchmarkOptions(), ct), ct);
    }

    /// <summary>
    /// Benchmarks every physical storage device once, one at a time (concurrent
    /// tests would measure each other's I/O and report nonsense). A device with
    /// several volumes is tested through its roomiest volume; a device with no
    /// mounted volume reports that there is nowhere to write instead of being
    /// silently skipped.
    /// </summary>
    public static async Task<IReadOnlyList<DriveBenchmarkResult>> RunBenchmarkForAllDrivesAsync(
        DriveBenchmarkOptions? options = null,
        Action<DriveBenchmarkResult>? onDriveCompleted = null,
        CancellationToken ct = default)
    {
        var results = new List<DriveBenchmarkResult>();
        foreach (var drive in GetAllDriveHealth())
        {
            ct.ThrowIfCancellationRequested();

            DriveBenchmarkResult result;
            if (!drive.HasMountedVolume || string.IsNullOrWhiteSpace(drive.DriveLetter))
            {
                result = new DriveBenchmarkResult
                {
                    DriveLetter = string.Empty,
                    DriveName = drive.DisplayName,
                    MediaType = drive.MediaType,
                    DiskNumber = drive.DiskNumber,
                    Success = false,
                    ErrorMessage = "This device has no mounted volume, so there is nowhere to write the test file."
                };
            }
            else
            {
                result = await RunReadWriteBenchmarkAsync(drive.DriveLetter, options, ct)
                    .ConfigureAwait(false);
                result.DriveName = drive.DisplayName;
                result.MediaType = drive.MediaType;
                result.DiskNumber = drive.DiskNumber;
            }

            results.Add(result);
            onDriveCompleted?.Invoke(result);
        }

        return results;
    }

    private static DriveBenchmarkResult RunBenchmarkCore(
        string driveLetter,
        DriveBenchmarkOptions options,
        CancellationToken ct)
    {
        string letter = NormalizeDriveLetter(driveLetter);
        var result = new DriveBenchmarkResult { DriveLetter = letter, DriveName = letter };

        if (options.FileSizeBytes <= 0 || options.ChunkSizeBytes <= 0 || options.ReadPasses <= 0)
        {
            return Fail(result, "Invalid benchmark options.");
        }

        string root;
        try
        {
            var drive = new DriveInfo(letter + "\\");
            if (!drive.IsReady) return Fail(result, "Drive is not ready.");
            if (drive.DriveType is DriveType.Network or DriveType.CDRom or DriveType.NoRootDirectory)
            {
                return Fail(result, $"Drive type {drive.DriveType} cannot be benchmarked.");
            }

            if (drive.AvailableFreeSpace < MinimumFreeSpaceBytes)
            {
                return Fail(result, $"Needs at least {TargetFolderInfo.FormatBytes(MinimumFreeSpaceBytes)} free.");
            }

            root = drive.Name;
            long size = Math.Min(options.FileSizeBytes, Math.Max(MinimumTestSizeBytes, drive.AvailableFreeSpace / 10));
            size = RoundDownToChunk(size, options.ChunkSizeBytes);
            if (size < MinimumTestSizeBytes)
            {
                return Fail(result, "Not enough free space for a reliable test.");
            }

            result.TestSizeBytes = size;
        }
        catch (Exception ex)
        {
            return Fail(result, ex.Message);
        }

        string testPath = Path.Combine(root, $"DeltempoSpeedTest_{Guid.NewGuid():N}.tmp");
        var total = Stopwatch.StartNew();
        double writeSpeedMBs = 0;

        try
        {
            WriteTestFile(testPath, result.TestSizeBytes, options.ChunkSizeBytes, ct, out writeSpeedMBs);
            result.WriteSpeedMBs = writeSpeedMBs;

            if (!TryMeasureReadSpeed(testPath, result.TestSizeBytes, options, ct,
                    out double readSpeed, out int passes, out long readBytes, out string? readError))
            {
                return Fail(result, readError ?? "Read benchmark failed.", writeSpeedMBs);
            }

            if (readBytes <= 0)
            {
                return Fail(result, "Read benchmark read no data.", writeSpeedMBs);
            }

            result.ReadSpeedMBs = readSpeed;
            result.ReadPasses = passes;
            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            return Fail(result, "Cancelled.", writeSpeedMBs);
        }
        catch (Exception ex)
        {
            return Fail(result, ex.Message, writeSpeedMBs);
        }
        finally
        {
            total.Stop();
            result.ElapsedMs = total.ElapsedMilliseconds;
            DeleteTestFile(testPath);
        }

        return result;
    }

    private static void WriteTestFile(string path, long size, int chunkSize, CancellationToken ct, out double writeSpeedMBs)
    {
        byte[] buffer = new byte[chunkSize];
        RandomNumberGenerator.Fill(buffer);

        long written = 0;
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = chunkSize,
            Options = FileOptions.WriteThrough
        };

        using var stream = new FileStream(path, options);
        var watch = Stopwatch.StartNew();
        while (written < size)
        {
            ct.ThrowIfCancellationRequested();
            int toWrite = (int)Math.Min(chunkSize, size - written);
            stream.Write(buffer, 0, toWrite);
            written += toWrite;
        }

        stream.Flush(flushToDisk: true);
        watch.Stop();
        writeSpeedMBs = ToMegabytesPerSecond(written, watch.Elapsed.TotalSeconds);
    }

    private static bool TryMeasureReadSpeed(
        string path,
        long size,
        DriveBenchmarkOptions options,
        CancellationToken ct,
        out double bestSpeedMBs,
        out int completedPasses,
        out long bytesRead,
        out string? error)
    {
        bestSpeedMBs = 0;
        completedPasses = 0;
        bytesRead = 0;
        error = null;

        int chunk = options.ChunkSizeBytes;
        IntPtr rawBuffer = IntPtr.Zero;
        try
        {
            using SafeFileHandle handle = CreateFileW(
                path,
                GenericRead,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagNoBuffering | FileFlagSequentialScan,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            rawBuffer = Marshal.AllocHGlobal(chunk + (int)CacheAlignment);
            IntPtr aligned = AlignUp(rawBuffer, CacheAlignment);

            for (int pass = 0; pass < options.ReadPasses; pass++)
            {
                ct.ThrowIfCancellationRequested();

                // Each pass is a fresh sequential read. Without this rewind the
                // handle stays at EOF after pass one and the next pass reads zero
                // bytes, which used to look like a truncated test file.
                if (!SetFilePointerEx(handle, 0, out _, FileBegin))
                {
                    error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return false;
                }

                long passBytes = 0;
                var watch = Stopwatch.StartNew();
                while (passBytes < size)
                {
                    ct.ThrowIfCancellationRequested();

                    uint toRead = (uint)Math.Min(chunk, size - passBytes);
                    if (!ReadFile(handle, aligned, toRead, out uint read, IntPtr.Zero))
                    {
                        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        return false;
                    }

                    if (read == 0) break;
                    passBytes += read;
                }

                watch.Stop();
                bytesRead += passBytes;
                completedPasses = pass + 1;

                double seconds = Math.Max(0.0001, watch.Elapsed.TotalSeconds);
                double speed = ToMegabytesPerSecond(passBytes, seconds);
                if (speed > bestSpeedMBs) bestSpeedMBs = speed;

                if (passBytes < size)
                {
                    error = "The drive returned fewer bytes than the test file holds.";
                    return false;
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (rawBuffer != IntPtr.Zero) Marshal.FreeHGlobal(rawBuffer);
        }
    }

    private static void DeleteTestFile(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(150);
            }
        }
    }

    private static DriveBenchmarkResult Fail(DriveBenchmarkResult result, string message, double writeSpeedMBs = 0)
    {
        result.Success = false;
        result.ErrorMessage = message;

        if (writeSpeedMBs > 0)
        {
            result.WriteSpeedMBs = writeSpeedMBs;
        }

        return result;
    }

    private static double ToMegabytesPerSecond(long bytes, double seconds) =>
        seconds <= 0 ? 0 : Math.Round(bytes / (double)BytesPerMegabyte / seconds, 1);

    private static long RoundDownToChunk(long value, int chunk) =>
        value / chunk * chunk;

    private static IntPtr AlignUp(IntPtr pointer, uint alignment)
    {
        long address = pointer.ToInt64();
        long aligned = (address + alignment - 1) & ~((long)alignment - 1);
        return new IntPtr(aligned);
    }
}
