using System;
using System.IO;
using System.Security.Cryptography;
using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

public class PatchIntegrityVerifierTests : IDisposable
{
    private readonly string _tempFile;

    public PatchIntegrityVerifierTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"patch_integrity_test_{Guid.NewGuid():N}.dat");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }
        catch { }
    }

    [Fact]
    public void VerifySha256_ExactMatch_ReturnsTrue()
    {
        byte[] content = System.Text.Encoding.UTF8.GetBytes("Deltempo Patch Integrity Test Data 12345");
        File.WriteAllBytes(_tempFile, content);

        using var sha = SHA256.Create();
        string expectedHash = Convert.ToHexString(sha.ComputeHash(content)).ToLowerInvariant();

        bool match = PatchIntegrityVerifier.VerifySha256(_tempFile, expectedHash, out string actualHash);
        Assert.True(match);
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void VerifySha256_Mismatch_ReturnsFalseAndOutputsActualHash()
    {
        byte[] content = System.Text.Encoding.UTF8.GetBytes("Legitimate Content");
        File.WriteAllBytes(_tempFile, content);

        string wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

        bool match = PatchIntegrityVerifier.VerifySha256(_tempFile, wrongHash, out string actualHash);
        Assert.False(match);
        Assert.NotEqual(wrongHash, actualHash);
    }

    [Fact]
    public void VerifyPeHeader_ValidWindowsExecutable_ReturnsTrue()
    {
        string cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        if (File.Exists(cmdPath))
        {
            bool isPe = PatchIntegrityVerifier.VerifyPeHeader(cmdPath, out string error);
            Assert.True(isPe, $"cmd.exe should be a valid PE: {error}");
        }
    }

    [Fact]
    public void VerifyPeHeader_PlainTextFile_ReturnsFalse()
    {
        string dummyText = new string('A', 2048);
        File.WriteAllText(_tempFile, dummyText);
        bool isPe = PatchIntegrityVerifier.VerifyPeHeader(_tempFile, out string error);

        Assert.False(isPe);
        Assert.Contains("MZ signature not found", error);
    }

    [Fact]
    public void VerifyStagedArtifact_HashMismatch_FailsClosed()
    {
        File.WriteAllText(_tempFile, "corrupted update content");
        string expectedHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";

        bool valid = PatchIntegrityVerifier.VerifyStagedArtifact(_tempFile, expectedHash, 100, out string reason);
        Assert.False(valid);
        Assert.NotEmpty(reason);
    }
}
