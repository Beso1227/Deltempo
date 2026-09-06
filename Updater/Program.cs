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
            "--recover" or "-r" => RecoverIncompleteTransactions(),
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

    private static int RecoverIncompleteTransactions()
    {
        var incomplete = TransactionJournal.FindIncompleteTransactions();

        if (incomplete.Count == 0)
        {
            Console.WriteLine("No incomplete transactions found.");
            return 0;
        }

        Console.WriteLine($"Found {incomplete.Count} incomplete transaction(s):");
        foreach (var tx in incomplete)
        {
            Console.WriteLine($"  {tx.TransactionId}: {tx.State} (version {tx.Version}, updated {tx.UpdatedAtUtc:O})");
        }

        return 0;
    }
}
