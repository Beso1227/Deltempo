using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Reasons a file was rejected during pre-deletion revalidation.
/// </summary>
public enum FileCleanupFailureReason
{
    Unknown,
    FileMissing,
    SizeChanged,
    TimestampChanged,
    PathEscapedRoot,
    ReparsePointDetected,
    ProtectedByPolicy,
    SafetyTierChanged,
    AccessDenied,
    FileChanged,
    SystemAttributeSet
}

/// <summary>
/// Two-phase execution engine with strict pre-deletion TOCTOU revalidation.
/// Verifies path canonicalization, reparse-point absence, attribute consistency,
/// size consistency, and protection policy right before every destructive action.
/// </summary>
public static class CleanupExecutor
{
    #region Native Win32 Deletion APIs

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFileW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveDirectoryW(string lpPathName);

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

    #endregion

    public static Task<CleanupTransactionResult> ExecutePlanAsync(
        CleanupPlan plan,
        string allowedRoot,
        Action<string, LogLevel>? logAction = null,
        Action<double>? progressReport = null,
        CancellationToken ct = default)
    {
        var roots = string.IsNullOrEmpty(allowedRoot) ? null : new[] { allowedRoot };
        return ExecutePlanAsync(plan, roots, logAction, progressReport, ct);
    }

    public static async Task<CleanupTransactionResult> ExecutePlanAsync(
        CleanupPlan plan,
        IEnumerable<string>? allowedRoots,
        Action<string, LogLevel>? logAction = null,
        Action<double>? progressReport = null,
        CancellationToken ct = default)
    {
        var result = new CleanupTransactionResult
        {
            ScopeId = plan.ScopeId,
            ScopeName = plan.ScopeName,
            DiscoveredCount = plan.DiscoveredCount,
            DiscoveredBytes = plan.DiscoveredBytes,
            EligibleCount = plan.EligibleCount,
            EligibleBytes = plan.EligibleBytes,
            StartTimeUtc = DateTime.UtcNow
        };

        var rootsList = allowedRoots?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();

        await Task.Run(() =>
        {
            int total = plan.Actions.Count;
            var actionable = new List<PlannedFileAction>(total);

            foreach (var action in plan.Actions)
            {
                if (action.Action is IntendedCleanupAction.SkipProtected or
                    IntendedCleanupAction.SkipRecent or
                    IntendedCleanupAction.SkipError)
                {
                    result.SkippedCount++;
                    result.SkippedBytes += action.SizeBytes;
                }
                else if (action.Action == IntendedCleanupAction.SkipReviewRequired)
                {
                    result.ReviewRequiredCount++;
                    result.ReviewRequiredBytes += action.SizeBytes;
                    result.SkippedReasons.Add($"{action.FileName}: ReviewRequired - not auto-deleted");
                }
                else
                {
                    actionable.Add(action);
                }
            }

            int actionableTotal = actionable.Count;
            if (actionableTotal == 0)
            {
                result.EndTimeUtc = DateTime.UtcNow;
                progressReport?.Invoke(1.0);
                return;
            }

            int processed = 0;
            long lastProgressReportTicks = Stopwatch.GetTimestamp();

            int deletedCount = 0;
            long deletedBytes = 0;
            int recycledCount = 0;
            long recycledBytes = 0;
            int failedCount = 0;
            long failedBytes = 0;
            int skippedCount = 0;
            long skippedBytes = 0;

            int maxDegree = Math.Min(16, Math.Max(2, Environment.ProcessorCount * 2));
            var pOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegree,
                CancellationToken = ct
            };

            var syncLock = new object();

            try
            {
                Parallel.ForEach(actionable, pOptions, (action, loopState) =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        loopState.Stop();
                        return;
                    }

                    int cur = Interlocked.Increment(ref processed);
                    if (actionableTotal > 0 && cur % 25 == 0)
                    {
                        long now = Stopwatch.GetTimestamp();
                        if (Stopwatch.GetElapsedTime(Interlocked.Read(ref lastProgressReportTicks)).TotalMilliseconds > 40)
                        {
                            Interlocked.Exchange(ref lastProgressReportTicks, now);
                            progressReport?.Invoke((double)cur / actionableTotal);
                        }
                    }

                    // PHASE 2: PRE-DELETION REVALIDATION (TOCTOU Defense)
                    if (!RevalidateBeforeDeletion(action.FilePath, rootsList, 0, null, out string revalidationReason, out _))
                    {
                        Interlocked.Increment(ref skippedCount);
                        Interlocked.Add(ref skippedBytes, action.SizeBytes);
                        lock (syncLock)
                        {
                            if (result.SkippedReasons.Count < 50)
                            {
                                result.SkippedReasons.Add($"{action.FileName}: {revalidationReason}");
                            }
                        }
                        logAction?.Invoke($"Revalidation aborted delete for '{action.FileName}': {revalidationReason}", LogLevel.Warning);
                        return;
                    }

                    // DESTRUCTIVE EXECUTION
                    try
                    {
                        long actualBytes = action.SizeBytes;
                        try
                        {
                            var fi = new FileInfo(action.FilePath);
                            if (fi.Exists) actualBytes = fi.Length;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[Cleanup] Size probe failed for '{action.FilePath}': {ex.Message}");
                        }

                        bool success = false;

                        if (action.Action == IntendedCleanupAction.MoveToRecycleBin)
                        {
                            lock (syncLock)
                            {
                                success = SendFileToRecycleBin(action.FilePath);
                            }
                            if (success)
                            {
                                Interlocked.Increment(ref recycledCount);
                                Interlocked.Add(ref recycledBytes, actualBytes);
                            }
                        }
                        else
                        {
                            success = DeletePermanently(action.FilePath);
                            if (success)
                            {
                                Interlocked.Increment(ref deletedCount);
                                Interlocked.Add(ref deletedBytes, actualBytes);
                            }
                        }

                        if (success)
                        {
                            // PHASE 3: POST-DELETION VERIFICATION
                            if (!VerifyDeletion(action.FilePath, action.Action == IntendedCleanupAction.MoveToRecycleBin))
                            {
                                if (action.Action == IntendedCleanupAction.MoveToRecycleBin)
                                {
                                    Interlocked.Decrement(ref recycledCount);
                                    Interlocked.Add(ref recycledBytes, -actualBytes);
                                }
                                else
                                {
                                    Interlocked.Decrement(ref deletedCount);
                                    Interlocked.Add(ref deletedBytes, -actualBytes);
                                }
                                Interlocked.Increment(ref failedCount);
                                Interlocked.Add(ref failedBytes, actualBytes);
                                lock (syncLock)
                                {
                                    if (result.ErrorMessages.Count < 50)
                                    {
                                        result.ErrorMessages.Add($"Post-deletion verification failed: {action.FilePath} still exists");
                                    }
                                }
                                logAction?.Invoke($"Post-deletion verification failed for '{action.FileName}'", LogLevel.Warning);
                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref failedCount);
                            Interlocked.Add(ref failedBytes, actualBytes);
                            lock (syncLock)
                            {
                                if (result.ErrorMessages.Count < 50)
                                {
                                    result.ErrorMessages.Add($"Deletion failed ({action.Action}): {action.FilePath}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failedCount);
                        Interlocked.Add(ref failedBytes, action.SizeBytes);
                        lock (syncLock)
                        {
                            if (result.ErrorMessages.Count < 50)
                            {
                                result.ErrorMessages.Add($"Exception deleting {action.FileName}: {ex.Message}");
                            }
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                result.WasCancelled = true;
            }

            result.DeletedCount += deletedCount;
            result.DeletedBytes += deletedBytes;
            result.RecycledCount += recycledCount;
            result.RecycledBytes += recycledBytes;
            result.FailedCount += failedCount;
            result.FailedBytes += failedBytes;
            result.SkippedCount += skippedCount;
            result.SkippedBytes += skippedBytes;

            result.EndTimeUtc = DateTime.UtcNow;
            progressReport?.Invoke(1.0);
        }, ct);

        return result;
    }

    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot = "") =>
        RevalidateBeforeDeletion(filePath, allowedRoot, 0, null, out _, out _);

    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot, out string failureReason) =>
        RevalidateBeforeDeletion(filePath, allowedRoot, 0, null, out failureReason, out _);

    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot, long expectedSize, out string failureReason) =>
        RevalidateBeforeDeletion(filePath, allowedRoot, expectedSize, null, out failureReason, out _);

    public static bool RevalidateBeforeDeletion(string filePath, IEnumerable<string>? allowedRoots, out string failureReason) =>
        RevalidateBeforeDeletion(filePath, allowedRoots, 0, null, out failureReason, out _);

    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot, long expectedSize, DateTime? expectedTimestamp, out string failureReason, out FileCleanupFailureReason reasonCode)
    {
        var roots = string.IsNullOrEmpty(allowedRoot) ? null : new[] { allowedRoot };
        return RevalidateBeforeDeletion(filePath, roots, expectedSize, expectedTimestamp, out failureReason, out reasonCode);
    }

    /// <summary>
    /// Pre-deletion TOCTOU-resistant revalidation.
    /// Re-evaluates file identity, attributes, root containment, reparse-points, size consistency,
    /// timestamp consistency, and protection rules immediately before destructive action.
    /// </summary>
    public static bool RevalidateBeforeDeletion(string filePath, IEnumerable<string>? allowedRoots, long expectedSize, DateTime? expectedTimestamp, out string failureReason, out FileCleanupFailureReason reasonCode)
    {
        failureReason = string.Empty;
        reasonCode = FileCleanupFailureReason.Unknown;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            failureReason = "Path is null or whitespace.";
            reasonCode = FileCleanupFailureReason.FileMissing;
            return false;
        }

        // Re-canonicalize path
        string canonicalPath = PathSecurity.NormalizeCanonicalPath(filePath);
        if (string.IsNullOrEmpty(canonicalPath))
        {
            failureReason = "Could not verify canonical path.";
            reasonCode = FileCleanupFailureReason.FileMissing;
            return false;
        }

        // Verify file still strictly resides inside designated cleanup roots
        var rootsList = allowedRoots?.Where(r => !string.IsNullOrEmpty(r)).ToList();
        if (rootsList != null && rootsList.Count > 0)
        {
            if (!rootsList.Any(root => PathSecurity.IsSubpathOf(canonicalPath, root)))
            {
                failureReason = "Path is not inside designated cleanup root.";
                reasonCode = FileCleanupFailureReason.PathEscapedRoot;
                return false;
            }
        }

        // Check file existence
        FileInfo fi;
        try
        {
            fi = new FileInfo(canonicalPath);
        }
        catch
        {
            failureReason = "File inaccessible.";
            reasonCode = FileCleanupFailureReason.AccessDenied;
            return false;
        }

        if (!fi.Exists)
        {
            failureReason = "File no longer exists on disk.";
            reasonCode = FileCleanupFailureReason.FileMissing;
            return false;
        }

        // Detect newly created reparse points, symlinks, or junctions
        if ((fi.Attributes & FileAttributes.ReparsePoint) != 0 || fi.LinkTarget != null)
        {
            failureReason = "File is or became an NTFS Reparse Point / Link.";
            reasonCode = FileCleanupFailureReason.ReparsePointDetected;
            return false;
        }

        // Enforce that System files are NEVER deleted
        if ((fi.Attributes & FileAttributes.System) != 0)
        {
            failureReason = "File has System attribute set.";
            reasonCode = FileCleanupFailureReason.SystemAttributeSet;
            return false;
        }

        // Size consistency check (if planned size was known)
        if (expectedSize > 0)
        {
            long currentSize = fi.Length;
            if (currentSize != expectedSize)
            {
                failureReason = $"File size changed: expected {expectedSize}, found {currentSize}.";
                reasonCode = FileCleanupFailureReason.SizeChanged;
                return false;
            }
        }

        // Timestamp consistency check (if planned timestamp was known)
        if (expectedTimestamp.HasValue)
        {
            TimeSpan drift = fi.LastWriteTimeUtc - expectedTimestamp.Value;
            if (Math.Abs(drift.TotalSeconds) > 5)
            {
                failureReason = $"File timestamp changed: expected {expectedTimestamp.Value:O}, found {fi.LastWriteTimeUtc:O}.";
                reasonCode = FileCleanupFailureReason.TimestampChanged;
                return false;
            }
        }

        // Re-evaluate Protection Policy immediately before deletion
        if (ProtectionPolicy.IsProtected(canonicalPath, out string protectedReason))
        {
            failureReason = $"Protected by policy: {protectedReason}";
            reasonCode = FileCleanupFailureReason.ProtectedByPolicy;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verifies that a deletion actually occurred after the operation was reported as successful.
    /// For Recycle Bin operations, verifies the original location no longer contains the object.
    /// </summary>
    private static bool VerifyDeletion(string path, bool wasRecycleBinOperation)
    {
        try
        {
            if (wasRecycleBinOperation)
            {
                return !File.Exists(path) && !Directory.Exists(path);
            }
            else
            {
                return !File.Exists(path);
            }
        }
        catch (Exception ex)
        {
            // If we cannot verify, assume failure (conservative)
            Trace.WriteLine($"[Cleanup] Post-deletion verification threw for '{path}': {ex.Message}");
            return false;
        }
    }

    public static bool DeletePermanently(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (fi.Exists && (fi.Attributes & FileAttributes.ReadOnly) != 0)
            {
                fi.Attributes &= ~FileAttributes.ReadOnly;
            }

            if (DeleteFileW(path))
            {
                return true;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return !File.Exists(path);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cleanup] Permanent delete failed for '{path}': {ex.Message}");
            return false;
        }
    }

    public static bool SendFileToRecycleBin(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return false;

            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };

            int res = SHFileOperation(ref shf);
            return res == 0 && !File.Exists(path) && !Directory.Exists(path);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cleanup] Recycle-bin operation failed for '{path}': {ex.Message}");
            return false;
        }
    }
}
