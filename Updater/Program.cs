using System.Diagnostics;
using System.Text.Json;
using WinTempCleaner.Core.Update;
using WinTempCleaner.Updater;

namespace DeltempoUpdater;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Deltempo Updater v1.0.0");

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        return command switch
        {
            "--transaction" or "-t" => await RunTransactionAsync(args),
            "--recover" or "-r" => await RecoverIncompleteTransactionsAsync(),
            "--version" or "-v" => 0,
            _ => PrintUsage()
        };
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Usage: DeltempoUpdater.exe [command]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  --transaction <txId>  Execute an update transaction");
        Console.WriteLine("  --recover             Recover incomplete transactions after crash/reboot");
        Console.WriteLine("  --version             Display updater version");
        return 0;
    }

    private static async Task<int> RunTransactionAsync(string[] args)
    {
        string? txId = null;
        int callerPid = 0;

        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] is "--transaction" or "-t")
                txId = args[i + 1];
            if (args[i] is "--caller-pid")
                int.TryParse(args[i + 1], out callerPid);
        }

        if (string.IsNullOrWhiteSpace(txId))
        {
            Console.Error.WriteLine("Error: --transaction requires a transaction ID.");
            return 1;
        }

        var journal = TransactionJournal.Load(txId);
        if (journal == null)
        {
            Console.Error.WriteLine($"Error: Transaction journal not found for '{txId}'.");
            return 1;
        }

        if (journal.CallerPid <= 0 && callerPid > 0)
            journal.CallerPid = callerPid;

        Console.WriteLine($"Executing transaction {txId} (state: {journal.State})");

        var coordinator = new UpdateTransactionCoordinator(journal, msg =>
        {
            Console.WriteLine(msg);
            Trace.WriteLine(msg);
        });

        bool success = await coordinator.ExecuteAsync();

        Console.WriteLine(success ? "Transaction completed successfully." : "Transaction failed.");
        return success ? 0 : 1;
    }

    private static async Task<int> RecoverIncompleteTransactionsAsync()
    {
        var incomplete = TransactionJournal.FindIncompleteTransactions();

        if (incomplete.Count == 0)
        {
            Console.WriteLine("No incomplete transactions found.");
            return 0;
        }

        Console.WriteLine($"Found {incomplete.Count} incomplete transaction(s). Attempting recovery...");

        int recovered = 0;
        int failed = 0;

        foreach (var tx in incomplete)
        {
            Console.WriteLine();
            Console.WriteLine($"--- Transaction {tx.TransactionId} (state: {tx.State}, version {tx.Version}) ---");

            try
            {
                // Determine appropriate recovery action based on state
                switch (tx.State)
                {
                    case TransactionState.Discovered:
                    case TransactionState.Downloaded:
                    case TransactionState.DownloadVerified:
                    case TransactionState.Staged:
                    case TransactionState.StageVerified:
                        // Downloaded but not installed - safe to clean up
                        Console.WriteLine("  State: Pre-install. Cleaning up staged files...");
                        CleanupTransactionArtifacts(tx);
                        tx.TransitionTo(TransactionState.RolledBack, "Recovered: cleaned up pre-install artifacts");
                        recovered++;
                        break;

                    case TransactionState.BackupCreated:
                    case TransactionState.InstallStarted:
                        // Installation was in progress but didn't complete - attempt rollback
                        Console.WriteLine("  State: Install incomplete. Attempting rollback...");
                        bool rolledBack = AttemptRollback(tx);
                        if (rolledBack)
                        {
                            tx.TransitionTo(TransactionState.RolledBack, "Recovered: rolled back incomplete installation");
                            recovered++;
                        }
                        else
                        {
                            Console.WriteLine("  WARNING: Rollback failed. Manual intervention may be required.");
                            failed++;
                        }
                        break;

                    case TransactionState.Installed:
                    case TransactionState.Launched:
                        // Binary was replaced but health check didn't complete - verify installed binary
                        Console.WriteLine("  State: Installed but not verified. Checking installed binary...");
                        bool valid = VerifyInstalledBinary(tx);
                        if (valid)
                        {
                            Console.WriteLine("  Installed binary is valid. Marking as committed.");
                            tx.TransitionTo(TransactionState.Committed, "Recovered: binary verified post-install");
                            recovered++;
                        }
                        else
                        {
                            Console.WriteLine("  Installed binary is invalid. Rolling back...");
                            bool rollbackOk = AttemptRollback(tx);
                            if (rollbackOk)
                            {
                                tx.TransitionTo(TransactionState.RolledBack, "Recovered: rolled back invalid binary");
                                recovered++;
                            }
                            else
                            {
                                Console.WriteLine("  WARNING: Rollback failed. Manual intervention may be required.");
                                failed++;
                            }
                        }
                        break;

                    default:
                        Console.WriteLine($"  State: {tx.State}. No recovery action needed.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR during recovery: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Recovery complete: {recovered} recovered, {failed} failed.");
        return failed > 0 ? 1 : 0;
    }

    private static void CleanupTransactionArtifacts(TransactionJournal tx)
    {
        try
        {
            string dir = tx.GetDirectoryPath();
            if (Directory.Exists(dir))
            {
                // Only delete staged files, keep journal for audit trail
                string stagedDir = Path.Combine(dir, "staged");
                if (Directory.Exists(stagedDir))
                    Directory.Delete(stagedDir, recursive: true);

                Console.WriteLine("  Staged files cleaned up.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WARNING: Could not clean up artifacts: {ex.Message}");
        }
    }

    private static bool AttemptRollback(TransactionJournal tx)
    {
        try
        {
            if (string.IsNullOrEmpty(tx.BackupPath) || !File.Exists(tx.BackupPath))
            {
                Console.WriteLine("  No backup available for rollback.");
                return false;
            }

            if (string.IsNullOrEmpty(tx.TargetPath))
            {
                Console.WriteLine("  No target path recorded.");
                return false;
            }

            // Restore backup to target
            File.Move(tx.BackupPath, tx.TargetPath, overwrite: true);

            // Verify restored binary
            if (!File.Exists(tx.TargetPath))
            {
                Console.WriteLine("  Restored binary does not exist.");
                return false;
            }

            var fi = new FileInfo(tx.TargetPath);
            if (fi.Length == 0)
            {
                Console.WriteLine("  Restored binary is empty.");
                return false;
            }

            Console.WriteLine($"  Backup restored ({fi.Length} bytes).");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Rollback error: {ex.Message}");
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
                {
                    Console.WriteLine($"  SHA-256 mismatch: expected {tx.ExpectedSha256}, got {actualHash}");
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
