using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Transaction states for the update lifecycle.
/// Enforces strict state transitions; any invalid transition is rejected.
/// </summary>
public enum TransactionState
{
    Discovered,
    Downloaded,
    DownloadVerified,
    Staged,
    StageVerified,
    BackupCreated,
    InstallStarted,
    Installed,
    Launched,
    HealthCheckPassed,
    Committed,
    RolledBack,
    Failed
}

/// <summary>
/// Persistent JSON journal for crash/power-loss recovery.
/// Saved at %ProgramData%\Deltempo\Updates\{txId}\transaction.json
/// </summary>
public class TransactionJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public TransactionState State { get; set; } = TransactionState.Discovered;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("commitSha")]
    public string CommitSha { get; set; } = string.Empty;

    [JsonPropertyName("stagedPath")]
    public string StagedPath { get; set; } = string.Empty;

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = string.Empty;

    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;

    [JsonPropertyName("expectedSha256")]
    public string ExpectedSha256 { get; set; } = string.Empty;

    [JsonPropertyName("expectedSizeBytes")]
    public long ExpectedSizeBytes { get; set; }

    [JsonPropertyName("callerPid")]
    public int CallerPid { get; set; }

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Valid state transitions. Returns the new state if valid, or null if invalid.
    /// </summary>
    public static TransactionState? NextState(TransactionState current, TransactionState desired)
    {
        if (current == desired)
            return desired;

        return current switch
        {
            TransactionState.Discovered when desired == TransactionState.Downloaded => desired,
            TransactionState.Downloaded when desired == TransactionState.DownloadVerified => desired,
            TransactionState.DownloadVerified when desired == TransactionState.Staged => desired,
            TransactionState.Staged when desired == TransactionState.StageVerified => desired,
            TransactionState.StageVerified when desired == TransactionState.BackupCreated => desired,
            TransactionState.BackupCreated when desired == TransactionState.InstallStarted => desired,
            TransactionState.InstallStarted when desired == TransactionState.Installed => desired,
            TransactionState.Installed when desired == TransactionState.Launched => desired,
            TransactionState.Launched when desired == TransactionState.HealthCheckPassed => desired,
            TransactionState.HealthCheckPassed when desired == TransactionState.Committed => desired,
            // Rollback can be initiated from most active states
            TransactionState.StageVerified when desired == TransactionState.RolledBack => desired,
            TransactionState.BackupCreated when desired == TransactionState.RolledBack => desired,
            TransactionState.InstallStarted when desired == TransactionState.RolledBack => desired,
            TransactionState.Installed when desired == TransactionState.RolledBack => desired,
            TransactionState.Launched when desired == TransactionState.RolledBack => desired,
            TransactionState.HealthCheckPassed when desired == TransactionState.RolledBack => desired,
            // Failure can be set from most states
            _ when desired == TransactionState.Failed => desired,
            _ => null
        };
    }

    public string GetDirectoryPath()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Deltempo", "Updates");
        return Path.Combine(baseDir, TransactionId);
    }

    public string GetJournalPath()
    {
        return Path.Combine(GetDirectoryPath(), "transaction.json");
    }

    public string GetHealthSignalPath()
    {
        return Path.Combine(GetDirectoryPath(), "health.signal");
    }

    public void Save()
    {
        UpdatedAtUtc = DateTime.UtcNow;
        string dir = GetDirectoryPath();
        Directory.CreateDirectory(dir);
        string json = JsonSerializer.Serialize(this, JsonOptions);
        string path = GetJournalPath();

        // Retry with backoff to handle concurrent access from multiple processes
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(fs);
                writer.Write(json);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    public static TransactionJournal? Load(string transactionId)
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Deltempo", "Updates");
        string journalPath = Path.Combine(baseDir, transactionId, "transaction.json");

        if (!File.Exists(journalPath))
            return null;

        try
        {
            string json = File.ReadAllText(journalPath);
            return JsonSerializer.Deserialize<TransactionJournal>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Scans for incomplete transactions (from crash/reboot) and returns them.
    /// </summary>
    public static List<TransactionJournal> FindIncompleteTransactions()
    {
        var result = new List<TransactionJournal>();
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Deltempo", "Updates");

        if (!Directory.Exists(baseDir))
            return result;

        foreach (var dir in Directory.GetDirectories(baseDir))
        {
            string journalPath = Path.Combine(dir, "transaction.json");
            if (!File.Exists(journalPath))
                continue;

            var journal = Load(Path.GetFileName(dir));
            if (journal != null && journal.State is not (TransactionState.Committed or TransactionState.RolledBack or TransactionState.Failed))
            {
                result.Add(journal);
            }
        }

        return result;
    }

    public void TransitionTo(TransactionState newState, string? errorMessage = null)
    {
        var validNext = NextState(State, newState);
        if (validNext == null)
        {
            throw new InvalidOperationException($"Invalid state transition: {State} -> {newState}");
        }

        State = validNext.Value;
        if (errorMessage != null)
            ErrorMessage = errorMessage;

        Save();
    }
}
