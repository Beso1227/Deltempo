using System.Diagnostics;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Scanning;

/// <summary>
/// High-resolution telemetry tracker for measuring scanning & cleaning velocity,
/// throughput (files/sec and MB/s), and real-time completion estimates.
/// </summary>
public sealed class ScanVelocityTracker
{
    private long _totalFiles;
    private long _totalBytes;
    private readonly long _startTimestamp;
    private long _lastSampleTimestamp;
    private long _lastSampleFiles;
    private long _lastSampleBytes;
    private double _smoothedFilesPerSec;
    private double _smoothedBytesPerSec;
    private readonly object _sampleLock = new();

    public ScanVelocityTracker()
    {
        _startTimestamp = Stopwatch.GetTimestamp();
        _lastSampleTimestamp = _startTimestamp;
    }

    public long TotalFiles => Interlocked.Read(ref _totalFiles);
    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <summary>
    /// Records newly processed file items and bytes atomically.
    /// </summary>
    public void RecordProgress(long bytesAdded, int filesAdded = 1)
    {
        Interlocked.Add(ref _totalFiles, filesAdded);
        Interlocked.Add(ref _totalBytes, bytesAdded);
    }

    /// <summary>
    /// Computes and returns current throughput snapshots, smoothing transient spikes.
    /// </summary>
    public (double filesPerSec, double bytesPerSec) SampleThroughput()
    {
        long now = Stopwatch.GetTimestamp();
        lock (_sampleLock)
        {
            double deltaSeconds = Stopwatch.GetElapsedTime(_lastSampleTimestamp, now).TotalSeconds;
            if (deltaSeconds >= 0.25)
            {
                long currentFiles = Interlocked.Read(ref _totalFiles);
                long currentBytes = Interlocked.Read(ref _totalBytes);

                long filesDiff = currentFiles - _lastSampleFiles;
                long bytesDiff = currentBytes - _lastSampleBytes;

                double instantFilesPerSec = deltaSeconds > 0 ? filesDiff / deltaSeconds : 0;
                double instantBytesPerSec = deltaSeconds > 0 ? bytesDiff / deltaSeconds : 0;

                // Exponential moving average (alpha = 0.4) for smooth UI readout
                if (_smoothedFilesPerSec <= 0)
                {
                    _smoothedFilesPerSec = instantFilesPerSec;
                    _smoothedBytesPerSec = instantBytesPerSec;
                }
                else
                {
                    _smoothedFilesPerSec = (0.4 * instantFilesPerSec) + (0.6 * _smoothedFilesPerSec);
                    _smoothedBytesPerSec = (0.4 * instantBytesPerSec) + (0.6 * _smoothedBytesPerSec);
                }

                _lastSampleTimestamp = now;
                _lastSampleFiles = currentFiles;
                _lastSampleBytes = currentBytes;
            }

            return (_smoothedFilesPerSec, _smoothedBytesPerSec);
        }
    }

    /// <summary>
    /// Formats current velocity string for UI displays, e.g. "3,450 files/s • 120.5 MB/s".
    /// </summary>
    public string GetFormattedVelocity()
    {
        var (filesSec, bytesSec) = SampleThroughput();
        string bytesRate = TargetFolderInfo.FormatBytes((long)bytesSec);
        return $"{filesSec:N0} files/s • {bytesRate}/s";
    }

    /// <summary>
    /// Estimates remaining completion time given current progress and total targets count.
    /// </summary>
    public TimeSpan? EstimateRemaining(int completedUnits, int totalUnits)
    {
        if (completedUnits <= 0 || totalUnits <= completedUnits) return null;
        double elapsedSec = Elapsed.TotalSeconds;
        double rate = (double)completedUnits / elapsedSec;
        if (rate <= 0) return null;

        int remainingUnits = totalUnits - completedUnits;
        return TimeSpan.FromSeconds(remainingUnits / rate);
    }
}
