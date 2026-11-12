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
        @"(?:process:|in use by:|locked by)\s*([^\s,(]+)(?:\s*\(PID:?\s*(\d+)\))?",
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

                string processName = record.LockingProcessName ?? string.Empty;
                int processId = record.LockingProcessId;

                // Fallback for records written before the holder was modelled on the
                // record itself. The executor writes "In use by: chrome (PID 1234)",
                // while the old regex expected "process: chrome (PID: 1234)" - so it
                // never matched and every row fell back to the generic reason with no
                // application name at all.
                if (string.IsNullOrEmpty(processName) && !string.IsNullOrEmpty(record.ErrorOrSkipReason))
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

    /// <summary>
    /// Groups the in-use files by the application holding them, most files first.
    /// </summary>
    /// <remarks>
    /// This is what turns "77 files are in use" into something the user can act on:
    /// "close these three applications and run the clean again". Files held by a
    /// process we could not name are grouped under a single unnamed holder rather
    /// than being dropped, so the count always matches the report.
    /// </remarks>
    public static List<InUseApp> AggregateByApplication(IEnumerable<LockedFileItem> rows)
    {
        var apps = new Dictionary<(string Name, int Pid), InUseApp>(NameComparer);

        foreach (LockedFileItem row in rows)
        {
            string name = string.IsNullOrWhiteSpace(row.LockingProcessName)
                ? "An application holding the file open"
                : row.LockingProcessName.Trim();

            var key = (name, row.LockingProcessId);
            if (!apps.TryGetValue(key, out InUseApp? app))
            {
                app = new InUseApp { ProcessName = name, ProcessId = row.LockingProcessId };
                apps[key] = app;
            }

            app.FileCount++;
            app.Bytes += row.SizeBytes;
        }

        return apps.Values
            .OrderByDescending(a => a.FileCount)
            .ThenBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static readonly IEqualityComparer<(string Name, int Pid)> NameComparer =
        new ProcessKeyComparer();

    private sealed class ProcessKeyComparer : IEqualityComparer<(string Name, int Pid)>
    {
        public bool Equals((string Name, int Pid) x, (string Name, int Pid) y) =>
            x.Pid == y.Pid && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Name, int Pid) obj) =>
            HashCode.Combine(obj.Pid, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
