using System.Diagnostics;
using System.IO;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Startup recovery logic for incomplete update transactions.
/// Handles crash/power-loss recovery before GUI initialization.
/// </summary>
public static class UpdateRecoveryHelper
{
    /// <summary>
    /// Finds and attempts recovery of any incomplete update transactions.
    /// Call this BEFORE GUI initialization on normal startup.
    /// </summary>
    public static async Task<int> RecoverIncompleteTransactionsAsync(Action<string>? logAction = null)
    {
        var incomplete = TransactionJournal.FindIncompleteTransactions();

        if (incomplete.Count == 0)
            return 0;

        Log(logAction, $"Found {incomplete.Count} incomplete transaction(s). Attempting recovery...");

        int recovered = 0;
        int failed = 0;

        foreach (var tx in incomplete)
        {
            try
            {
                Log(logAction, $"--- Transaction {tx.TransactionId} (state: {tx.State}, version {tx.Version}) ---");

                switch (tx.State)
                {
                    case TransactionState.Discovered:
                    case TransactionState.Downloaded:
                    case TransactionState.DownloadVerified:
                    case TransactionState.Staged:
                    case TransactionState.StageVerified:
                        Log(logAction, "  State: Pre-install. Cleaning up staged files...");
                        CleanupTransactionArtifacts(tx, logAction);
                        tx.TransitionTo(TransactionState.RolledBack, "Recovered: cleaned up pre-install artifacts");
                        recovered++;
                        break;

                    case TransactionState.BackupCreated:
                    case TransactionState.InstallStarted:
                        Log(logAction, "  State: Install incomplete. Attempting rollback...");
                        if (AttemptRollback(tx, logAction))
                        {
                            tx.TransitionTo(TransactionState.RolledBack, "Recovered: rolled back incomplete installation");
                            recovered++;
                        }
                        else
                        {
                            Log(logAction, "  WARNING: Rollback failed. Manual intervention may be required.");
                            failed++;
                        }
                        break;

                    case TransactionState.Installed:
                    case TransactionState.Launched:
                        Log(logAction, "  State: Installed but not verified. Checking installed binary...");
                        if (VerifyInstalledBinary(tx))
                        {
                            Log(logAction, "  Installed binary is valid. Marking as committed.");
                            tx.TransitionTo(TransactionState.Committed, "Recovered: binary verified post-install");
                            recovered++;
                        }
                        else
                        {
                            Log(logAction, "  Installed binary is invalid. Rolling back...");
                            if (AttemptRollback(tx, logAction))
                            {
                                tx.TransitionTo(TransactionState.RolledBack, "Recovered: rolled back invalid binary");
                                recovered++;
                            }
                            else
                            {
                                Log(logAction, "  WARNING: Rollback failed. Manual intervention may be required.");
                                failed++;
                            }
                        }
                        break;

                    default:
                        Log(logAction, $"  State: {tx.State}. No recovery action needed.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log(logAction, $"  ERROR during recovery: {ex.Message}");
                failed++;
            }
        }

        Log(logAction, $"Recovery complete: {recovered} recovered, {failed} failed.");
        return failed > 0 ? 1 : 0;
    }

    private static void Log(Action<string>? logAction, string msg)
    {
        logAction?.Invoke($"[Deltempo] {msg}");
        Trace.WriteLine($"[Deltempo] {msg}");
    }

    private static void CleanupTransactionArtifacts(TransactionJournal tx, Action<string>? logAction)
    {
        try
        {
            string dir = tx.GetDirectoryPath();
            if (Directory.Exists(dir))
            {
                string stagedDir = Path.Combine(dir, "staged");
                if (Directory.Exists(stagedDir))
                    Directory.Delete(stagedDir, recursive: true);

                Log(logAction, "  Staged files cleaned up.");
            }
        }
        catch (Exception ex)
        {
            Log(logAction, $"  WARNING: Could not clean up artifacts: {ex.Message}");
        }
    }

    private static bool AttemptRollback(TransactionJournal tx, Action<string>? logAction)
    {
        try
        {
            if (string.IsNullOrEmpty(tx.BackupPath) || !File.Exists(tx.BackupPath))
            {
                Log(logAction, "  No backup available for rollback.");
                return false;
            }

            if (string.IsNullOrEmpty(tx.TargetPath))
            {
                Log(logAction, "  No target path recorded.");
                return false;
            }

            File.Move(tx.BackupPath, tx.TargetPath, overwrite: true);

            if (!File.Exists(tx.TargetPath))
            {
                Log(logAction, "  Restored binary does not exist.");
                return false;
            }

            var fi = new FileInfo(tx.TargetPath);
            if (fi.Length == 0)
            {
                Log(logAction, "  Restored binary is empty.");
                return false;
            }

            // Verify PE header
            using var fs = new FileStream(tx.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] mz = new byte[2];
            if (fs.Read(mz, 0, 2) != 2 || mz[0] != 0x4D || mz[1] != 0x5A)
            {
                Log(logAction, "  Restored binary is not a valid PE executable.");
                return false;
            }

            Log(logAction, $"  Backup restored ({fi.Length} bytes).");
            return true;
        }
        catch (Exception ex)
        {
            Log(logAction, $"  Rollback error: {ex.Message}");
            return false;
        }
    }

    private static bool VerifyInstalledBinary(TransactionJournal tx)
    {
        try
        {
            if (string.IsNullOrEmpty(tx.TargetPath) || !File.Exists(tx.TargetPath))
                return false;

            var fi = new FileInfo(tx.TargetPath);
            if (fi.Length == 0)
                return false;

            // Verify PE header
            using var fs = new FileStream(tx.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] mz = new byte[2];
            if (fs.Read(mz, 0, 2) != 2 || mz[0] != 0x4D || mz[1] != 0x5A)
                return false;

            // Verify SHA-256 if expected hash was recorded
            if (!string.IsNullOrWhiteSpace(tx.ExpectedSha256))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                fs.Seek(0, SeekOrigin.Begin);
                byte[] hash = sha.ComputeHash(fs);
                string actualHash = Convert.ToHexString(hash).ToLowerInvariant();
                if (!actualHash.Equals(tx.ExpectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
