using System.IO;
using System.Text.RegularExpressions;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

/// <summary>
/// Builds the In-Use &amp; Locked Files inspector's rows from the audit trail of a
/// finished cleanup.
///
/// A locked file never reaches the deletion stage, so the only record of it is a
/// <see cref="DeletionAuditRecord"/> with <see cref="CleanupErrorCategory.FileLocked"/>.
/// The locking process is not modelled separately: the executor folds it into the
/// record's reason string ("... process: chrome (PID: 1234)"), which is why this
/// collector parses it back out.
///
/// Extracted verbatim from WPF's CelebrationViewLocked_Click so both shells show the
/// same rows for the same run instead of drifting apart.
/// </summary>
public static class LockedFileCollector
{
    /// <summary>Fallback shown when the audit record carries no reason at all.</summary>
    public const string DefaultLockReason = "File currently in use by active process.";

    private static readonly Regex LockingProcessPattern = new(
        @"process:\s*([^\s(]+)(?:\s*\(PID:\s*(\d+)\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns one row per file the run could not delete because a process held it open.
    /// Order follows the target list and then the audit records, matching what WPF
    /// rendered. Null targets and targets that never ran a transaction contribute
    /// nothing.
    /// </summary>
    public static List<LockedFileItem> FromTargets(IEnumerable<TargetFolderInfo?>? targets)
    {
        var lockedRecords = new List<LockedFileItem>();

        if (targets is null)
        {
            return lockedRecords;
        }

        foreach (TargetFolderInfo? target in targets)
        {
            if (target?.LastTransactionResult?.AuditRecords is not { } auditRecords)
            {
                continue;
            }

            foreach (DeletionAuditRecord record in auditRecords)
            {
                if (record.ErrorCategory != CleanupErrorCategory.FileLocked)
                {
                    continue;
                }

                string processName = string.Empty;
                int processId = 0;
                if (!string.IsNullOrEmpty(record.ErrorOrSkipReason))
                {
                    Match match = LockingProcessPattern.Match(record.ErrorOrSkipReason);
                    if (match.Success)
                    {
                        processName = match.Groups[1].Value;
                        if (int.TryParse(match.Groups[2].Value, out int parsedId))
                        {
                            processId = parsedId;
                        }
                    }
                }

                lockedRecords.Add(new LockedFileItem
                {
                    FilePath = record.FilePath,
                    FileName = Path.GetFileName(record.FilePath),
                    SizeBytes = record.SizeBytes,
                    LockingProcessName = processName,
                    LockingProcessId = processId,
                    LockReason = record.ErrorOrSkipReason ?? DefaultLockReason,
                });
            }
        }

        return lockedRecords;
    }
}
