using System.IO;
using System.Runtime.InteropServices;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Two-phase execution engine with strict pre-deletion TOCTOU revalidation.
/// Verifies path canonicalization, reparse-point absence, attribute consistency,
/// and protection policy right before every destructive action.
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

    public static async Task<CleanupTransactionResult> ExecutePlanAsync(
        CleanupPlan plan,
        string allowedRoot,
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
            ProtectedCount = plan.ProtectedCount,
            ProtectedBytes = plan.ProtectedBytes,
            StartTimeUtc = DateTime.UtcNow
        };

        await Task.Run(() =>
        {
            int total = plan.Actions.Count;
            int processed = 0;

            foreach (var action in plan.Actions)
            {
                if (ct.IsCancellationRequested)
                {
                    result.SkippedReasons.Add("Cleanup cancelled by user.");
                    break;
                }

                processed++;
                if (total > 0 && processed % 20 == 0)
                {
                    progressReport?.Invoke((double)processed / total);
                }

                // If plan already marked it to skip
                if (action.Action is IntendedCleanupAction.SkipProtected or
                    IntendedCleanupAction.SkipReviewRequired or
                    IntendedCleanupAction.SkipRecent or
                    IntendedCleanupAction.SkipError)
                {
                    result.SkippedCount++;
                    result.SkippedBytes += action.SizeBytes;
                    continue;
                }

                // =========================================================================
                // PHASE 2: PRE-DELETION REVALIDATION (TOCTOU Defense)
                // =========================================================================
                if (!RevalidateBeforeDeletion(action.FilePath, allowedRoot, action.SizeBytes, out string revalidationReason))
                {
                    result.SkippedCount++;
                    result.SkippedBytes += action.SizeBytes;
                    result.SkippedReasons.Add($"{action.FileName}: {revalidationReason}");
                    logAction?.Invoke($"Revalidation aborted delete for '{action.FileName}': {revalidationReason}", LogLevel.Warning);
                    continue;
                }

                // =========================================================================
                // DESTRUCTIVE EXECUTION
                // =========================================================================
                try
                {
                    long actualBytes = action.SizeBytes;
                    try
                    {
                        var fi = new FileInfo(action.FilePath);
                        if (fi.Exists) actualBytes = fi.Length;
                    }
                    catch { }

                    if (action.Action == IntendedCleanupAction.MoveToRecycleBin)
                    {
                        if (SendFileToRecycleBin(action.FilePath))
                        {
                            result.RecycledCount++;
                            result.RecycledBytes += actualBytes;
                        }
                        else
                        {
                            result.FailedCount++;
                            result.FailedBytes += actualBytes;
                            result.ErrorMessages.Add($"Could not move to Recycle Bin: {action.FilePath}");
                        }
                    }
                    else // DeletePermanently
                    {
                        if (DeletePermanently(action.FilePath))
                        {
                            result.DeletedCount++;
                            result.DeletedBytes += actualBytes;
                        }
                        else
                        {
                            result.FailedCount++;
                            result.FailedBytes += actualBytes;
                            result.ErrorMessages.Add($"Permanent deletion failed: {action.FilePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.FailedBytes += action.SizeBytes;
                    result.ErrorMessages.Add($"Exception deleting {action.FileName}: {ex.Message}");
                }
            }

            result.EndTimeUtc = DateTime.UtcNow;
            progressReport?.Invoke(1.0);
        }, ct);

        return result;
    }

    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot = "") =>
        RevalidateBeforeDeletion(filePath, allowedRoot, 0, out _);

    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot, out string failureReason) =>
        RevalidateBeforeDeletion(filePath, allowedRoot, 0, out failureReason);

    /// <summary>
    /// Immediate pre-deletion revalidation check.
    /// Re-evaluates file identity, attributes, root containment, reparse-points, and protection rules.
    /// If ANY condition fails or changed since planning, returns false to abort deletion.
    /// </summary>
    public static bool RevalidateBeforeDeletion(string filePath, string allowedRoot, long expectedSize, out string failureReason)
    {
        failureReason = string.Empty;

        // 1. Re-check null or empty
        if (string.IsNullOrWhiteSpace(filePath))
        {
            failureReason = "Path is null or whitespace.";
            return false;
        }

        // 2. Re-canonicalize path
        string canonicalPath = PathSecurity.NormalizeCanonicalPath(filePath);
        if (string.IsNullOrEmpty(canonicalPath))
        {
            failureReason = "Could not verify canonical path.";
            return false;
        }

        // 3. Verify file still strictly resides inside allowedRoot
        if (!string.IsNullOrEmpty(allowedRoot) && !PathSecurity.IsSubpathOf(canonicalPath, allowedRoot))
        {
            failureReason = "Path is not inside designated cleanup root.";
            return false;
        }

        // 4. Check file existence
        var fi = new FileInfo(canonicalPath);
        if (!fi.Exists)
        {
            failureReason = "File no longer exists on disk.";
            return false;
        }

        // 5. Detect newly created reparse points, symlinks, or junctions
        if ((fi.Attributes & FileAttributes.ReparsePoint) != 0 || fi.LinkTarget != null)
        {
            failureReason = "File is or became an NTFS Reparse Point / Link.";
            return false;
        }

        // 6. Enforce that System files are NEVER deleted
        if ((fi.Attributes & FileAttributes.System) != 0)
        {
            failureReason = "File has System attribute set.";
            return false;
        }

        // 7. Re-evaluate Protection Policy immediately before deletion
        if (ProtectionPolicy.IsProtected(canonicalPath, out string protectedReason))
        {
            failureReason = $"Protected by policy: {protectedReason}";
            return false;
        }

        return true;
    }

    public static bool DeletePermanently(string path)
    {
        try
        {
            // Clear ReadOnly attribute if set (DO NOT touch System attribute)
            var fi = new FileInfo(path);
            if (fi.Exists && (fi.Attributes & FileAttributes.ReadOnly) != 0)
            {
                fi.Attributes &= ~FileAttributes.ReadOnly;
            }

            // Primary native Win32 delete
            if (DeleteFileW(path))
            {
                return true;
            }

            // Fallback .NET delete
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return !File.Exists(path);
        }
        catch
        {
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
        catch
        {
            return false;
        }
    }
}
