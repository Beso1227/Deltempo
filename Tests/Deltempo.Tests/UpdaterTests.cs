using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

public class UpdateManifestTests
{
    [Fact]
    public void ToCanonicalJson_DeterministicOutput()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234def56789",
            PublishedAtUtc = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            Artifact = new UpdateArtifactInfo
            {
                Name = "Deltempo.exe",
                Sha256 = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd",
                SizeBytes = 52428800,
                DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v1.3.3/Deltempo.exe"
            }
        };

        string json1 = manifest.ToCanonicalJson();
        string json2 = manifest.ToCanonicalJson();

        Assert.Equal(json1, json2);
        Assert.Contains("\"schemaVersion\":2", json1);
        Assert.Contains("\"version\":\"1.3.3\"", json1);
        Assert.Contains("\"product\":\"Deltempo\"", json1);
    }

    [Fact]
    public void ToCanonicalJson_ExcludesSignature()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "patch",
            Version = "1.3.3-patch",
            CommitSha = "abc1234",
            Signature = "some_signature_value"
        };

        string json = manifest.ToCanonicalJson();
        Assert.DoesNotContain("signature", json);
    }

    [Fact]
    public void ToCanonicalJson_ProducesValidJson()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "patch",
            Version = "1.3.3-patch",
            CommitSha = "abc1234"
        };

        string json = manifest.ToCanonicalJson();
        Assert.StartsWith("{", json);
        Assert.EndsWith("}", json);

        // Verify it can be parsed
        var parsed = JsonSerializer.Deserialize<UpdateManifest>(json);
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed.SchemaVersion);
    }
}

public class ManifestSignatureVerifierTests
{
    [Fact]
    public void VerifySignature_EmptySignature_ReturnsFalse()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234",
            Signature = ""
        };

        bool result = ManifestSignatureVerifier.VerifySignature(manifest);
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_NullManifest_ReturnsFalse()
    {
        bool result = ManifestSignatureVerifier.VerifySignature(null!);
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_InvalidSignature_ReturnsFalse()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234",
            Signature = "not_a_valid_base64_signature"
        };

        bool result = ManifestSignatureVerifier.VerifySignature(manifest);
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_TamperedManifest_ReturnsFalse()
    {
        // Create a manifest with a valid-looking but incorrect signature
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234",
            Signature = Convert.ToBase64String(new byte[64]) // Dummy signature
        };

        bool result = ManifestSignatureVerifier.VerifySignature(manifest);
        Assert.False(result);
    }

    [Fact]
    public void VerifyManifestJson_EmptyJson_ReturnsError()
    {
        var (manifest, error) = ManifestSignatureVerifier.VerifyManifestJson("");
        Assert.Null(manifest);
        Assert.NotNull(error);
    }

    [Fact]
    public void VerifyManifestJson_InvalidJson_ReturnsError()
    {
        var (manifest, error) = ManifestSignatureVerifier.VerifyManifestJson("not json");
        Assert.Null(manifest);
        Assert.NotNull(error);
    }

    [Fact]
    public void VerifyManifestJson_NullJson_ReturnsError()
    {
        var (manifest, error) = ManifestSignatureVerifier.VerifyManifestJson(null!);
        Assert.Null(manifest);
        Assert.NotNull(error);
    }

    [Fact]
    public void VerifyManifestJson_WrongSchemaVersion_ReturnsError()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 1,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234"
        };
        string json = JsonSerializer.Serialize(manifest);

        var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);
        Assert.Null(result);
        Assert.Contains("schema version", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyManifestJson_WrongProduct_ReturnsError()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Product = "Malware",
            Version = "1.3.3",
            CommitSha = "abc1234"
        };
        string json = JsonSerializer.Serialize(manifest);

        var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);
        Assert.Null(result);
        Assert.Contains("product", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyManifestJson_MissingVersion_ReturnsError()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "",
            CommitSha = "abc1234"
        };
        string json = JsonSerializer.Serialize(manifest);

        var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);
        Assert.Null(result);
        Assert.Contains("version", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyManifestJson_MissingArtifact_ReturnsError()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234",
            Artifact = null
        };
        string json = JsonSerializer.Serialize(manifest);

        var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);
        Assert.Null(result);
        Assert.Contains("artifact", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyManifestJson_MissingArtifactSha256_ReturnsError()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Channel = "stable",
            Version = "1.3.3",
            CommitSha = "abc1234",
            Artifact = new UpdateArtifactInfo
            {
                Name = "Deltempo.exe",
                Sha256 = "",
                SizeBytes = 1024,
                DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v1.3.3/Deltempo.exe"
            }
        };
        string json = JsonSerializer.Serialize(manifest);

        var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);
        Assert.Null(result);
        Assert.Contains("SHA-256", error!, StringComparison.OrdinalIgnoreCase);
    }
}

public class TransactionJournalTests : IDisposable
{
    private readonly string _testDir;

    public TransactionJournalTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"tx_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void NextState_ValidTransitions_ReturnsExpectedState()
    {
        Assert.Equal(TransactionState.Downloaded,
            TransactionJournal.NextState(TransactionState.Discovered, TransactionState.Downloaded));

        Assert.Equal(TransactionState.DownloadVerified,
            TransactionJournal.NextState(TransactionState.Downloaded, TransactionState.DownloadVerified));

        Assert.Equal(TransactionState.Staged,
            TransactionJournal.NextState(TransactionState.DownloadVerified, TransactionState.Staged));

        Assert.Equal(TransactionState.StageVerified,
            TransactionJournal.NextState(TransactionState.Staged, TransactionState.StageVerified));

        Assert.Equal(TransactionState.BackupCreated,
            TransactionJournal.NextState(TransactionState.StageVerified, TransactionState.BackupCreated));

        Assert.Equal(TransactionState.InstallStarted,
            TransactionJournal.NextState(TransactionState.BackupCreated, TransactionState.InstallStarted));

        Assert.Equal(TransactionState.Installed,
            TransactionJournal.NextState(TransactionState.InstallStarted, TransactionState.Installed));

        Assert.Equal(TransactionState.Launched,
            TransactionJournal.NextState(TransactionState.Installed, TransactionState.Launched));

        Assert.Equal(TransactionState.HealthCheckPassed,
            TransactionJournal.NextState(TransactionState.Launched, TransactionState.HealthCheckPassed));

        Assert.Equal(TransactionState.Committed,
            TransactionJournal.NextState(TransactionState.HealthCheckPassed, TransactionState.Committed));
    }

    [Fact]
    public void NextState_InvalidTransition_ReturnsNull()
    {
        // Failed can be reached from any state via wildcard, so test truly invalid transitions
        Assert.Null(TransactionJournal.NextState(TransactionState.Committed, TransactionState.Discovered));
        Assert.Null(TransactionJournal.NextState(TransactionState.RolledBack, TransactionState.Discovered));
        Assert.Null(TransactionJournal.NextState(TransactionState.Failed, TransactionState.Discovered));
    }

    [Fact]
    public void NextState_Rollback_FromInstallStarted()
    {
        Assert.Equal(TransactionState.RolledBack,
            TransactionJournal.NextState(TransactionState.InstallStarted, TransactionState.RolledBack));
    }

    [Fact]
    public void NextState_Rollback_FromInstalled()
    {
        Assert.Equal(TransactionState.RolledBack,
            TransactionJournal.NextState(TransactionState.Installed, TransactionState.RolledBack));
    }

    [Fact]
    public void NextState_Rollback_FromLaunched()
    {
        Assert.Equal(TransactionState.RolledBack,
            TransactionJournal.NextState(TransactionState.Launched, TransactionState.RolledBack));
    }

    [Fact]
    public void NextState_Failure_FromAnyState()
    {
        Assert.Equal(TransactionState.Failed,
            TransactionJournal.NextState(TransactionState.Discovered, TransactionState.Failed));
        Assert.Equal(TransactionState.Failed,
            TransactionJournal.NextState(TransactionState.Downloaded, TransactionState.Failed));
        Assert.Equal(TransactionState.Failed,
            TransactionJournal.NextState(TransactionState.Installed, TransactionState.Failed));
    }

    [Fact]
    public void TransitionTo_InvalidTransition_ThrowsException()
    {
        var journal = new TransactionJournal { TransactionId = "test-tx" };
        journal.TransitionTo(TransactionState.Downloaded);

        Assert.Throws<InvalidOperationException>(() =>
            journal.TransitionTo(TransactionState.Installed));
    }

    [Fact]
    public void SaveAndLoad_PersistsCorrectly()
    {
        string txId = Guid.NewGuid().ToString("N");
        var journal = new TransactionJournal
        {
            TransactionId = txId,
            Channel = "patch",
            Version = "1.3.3",
            CommitSha = "abc1234",
            TargetPath = @"C:\test\Deltempo.exe",
            BackupPath = @"C:\test\Deltempo.exe.old",
            ExpectedSha256 = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            ExpectedSizeBytes = 52428800,
            CallerPid = 12345
        };
        journal.TransitionTo(TransactionState.Downloaded);
        journal.TransitionTo(TransactionState.DownloadVerified);

        string journalPath = Path.Combine(_testDir, txId, "transaction.json");
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        string json = JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(journalPath, json);

        string loadedJson = File.ReadAllText(journalPath);
        var loaded = JsonSerializer.Deserialize<TransactionJournal>(loadedJson);

        Assert.NotNull(loaded);
        Assert.Equal(txId, loaded.TransactionId);
        Assert.Equal(TransactionState.DownloadVerified, loaded.State);
        Assert.Equal("patch", loaded.Channel);
        Assert.Equal("1.3.3", loaded.Version);
        Assert.Equal(12345, loaded.CallerPid);
    }

    [Fact]
    public void FindIncompleteTransactions_DoesNotThrow()
    {
        var result = TransactionJournal.FindIncompleteTransactions();
        Assert.NotNull(result);
    }
}

public class PatchMetadataValidatorUrlTests
{
    [Theory]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v1.3.3/Deltempo.exe", true)]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v1.3.3/deltempo_cli.exe", true)]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/patch/DeltempoUpdater.exe", true)]
    public void IsValidDownloadUrl_ValidGitHubReleaseUrls_ReturnsTrue(string url, bool expected)
    {
        bool result = PatchMetadataValidator.IsValidDownloadUrl(url, out string reason);
        Assert.Equal(expected, result);
        if (!expected) Assert.NotEmpty(reason);
    }

    [Theory]
    [InlineData("http://github.com/Beso1227/Deltempo/releases/download/v1.3.3/Deltempo.exe", false)]
    [InlineData("https://evil.com/Beso1227/Deltempo/releases/download/v1.3.3/Deltempo.exe", false)]
    [InlineData("https://github.com/attacker/Deltempo/releases/download/v1.3.3/Deltempo.exe", false)]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v1.3.3/malware.exe", false)]
    [InlineData("ftp://github.com/Beso1227/Deltempo/releases/download/v1.3.3/Deltempo.exe", false)]
    public void IsValidDownloadUrl_InvalidUrls_ReturnsFalse(string url, bool expected)
    {
        bool result = PatchMetadataValidator.IsValidDownloadUrl(url, out string reason);
        Assert.Equal(expected, result);
        Assert.NotEmpty(reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("   ")]
    public void IsValidDownloadUrl_EmptyOrMalformed_ReturnsFalse(string url)
    {
        bool result = PatchMetadataValidator.IsValidDownloadUrl(url, out string reason);
        Assert.False(result);
        Assert.NotEmpty(reason);
    }
}

public class PatchIntegrityVerifierPeTests : IDisposable
{
    private readonly string _tempFile;

    public PatchIntegrityVerifierPeTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"pe_test_{Guid.NewGuid():N}.dat");
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
    }

    [Fact]
    public void VerifyPeHeader_TooSmall_ReturnsFalse()
    {
        File.WriteAllBytes(_tempFile, new byte[100]);
        bool result = PatchIntegrityVerifier.VerifyPeHeader(_tempFile, out string error);
        Assert.False(result);
        Assert.Contains("too small", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyPeHeader_NoMzSignature_ReturnsFalse()
    {
        byte[] data = new byte[2048];
        data[0] = 0x00;
        File.WriteAllBytes(_tempFile, data);
        bool result = PatchIntegrityVerifier.VerifyPeHeader(_tempFile, out string error);
        Assert.False(result);
        Assert.Contains("MZ signature not found", error);
    }

    [Fact]
    public void VerifyPeHeader_ValidMzButBadPe_ReturnsFalse()
    {
        byte[] data = new byte[2048];
        data[0] = 0x4D;
        data[1] = 0x5A;
        data[0x3C] = 0xFF;
        data[0x3D] = 0xFF;
        data[0x3E] = 0xFF;
        data[0x3F] = 0xFF;
        File.WriteAllBytes(_tempFile, data);
        bool result = PatchIntegrityVerifier.VerifyPeHeader(_tempFile, out string error);
        Assert.False(result);
    }

    [Fact]
    public void VerifySha256_EmptyExpectedHash_ReturnsFalse()
    {
        File.WriteAllText(_tempFile, "test content");
        bool result = PatchIntegrityVerifier.VerifySha256(_tempFile, "", out string actual);
        Assert.False(result);
        Assert.Empty(actual);
    }

    [Fact]
    public void VerifySha256_NonexistentFile_ReturnsFalse()
    {
        bool result = PatchIntegrityVerifier.VerifySha256(@"C:\nonexistent\file.exe", "abc", out string actual);
        Assert.False(result);
    }

    [Fact]
    public void VerifyStagedArtifact_NonexistentFile_ReturnsFalse()
    {
        bool result = PatchIntegrityVerifier.VerifyStagedArtifact(
            @"C:\nonexistent\file.exe", "", 0, out string reason);
        Assert.False(result);
        Assert.Contains("does not exist", reason);
    }

    [Fact]
    public void VerifyPeHeader_WindowsCmd_ReturnsTrue()
    {
        string cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        if (File.Exists(cmdPath))
        {
            bool isPe = PatchIntegrityVerifier.VerifyPeHeader(cmdPath, out string error);
            Assert.True(isPe, $"cmd.exe should be a valid PE: {error}");
        }
    }
}
