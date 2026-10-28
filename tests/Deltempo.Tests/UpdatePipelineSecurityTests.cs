using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinTempCleaner.Core.Update;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Security tests for the Deltempo update pipeline.
///
/// Uses a dedicated ECDSA P-256 test key pair stored under tests/Deltempo.Tests/Fixtures/.
/// The private key is used ONLY to sign test manifests in this file; it is never embedded
/// in production code. The public key is configured as a trusted key in tests that exercise
/// signature verification.
///
/// Production has NO trusted key configured by default — manifest verification fails closed
/// until an operator provisions one.
/// </summary>
// Serialised with ManifestKeyProvisioningTests: ManifestSignatureVerifier's trusted-key set is
// process-global static state, so both classes must not run concurrently.
[Collection("UpdateSignatureState")]
public class UpdatePipelineSecurityTests
{
    private const string TestKeyId = "test-key-1";

    private static string FixturesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));

    private static string TestPrivateKeyPem =>
        Path.Combine(FixturesDir, "test-update-signing-key.pem");

    private static string TestPublicKeyPem =>
        Path.Combine(FixturesDir, "test-update-verification-key.pub");

    /// <summary>
    /// Configures the verifier with the test public key so tests can verify signatures
    /// made with the test private key. Resets to fail-closed (no keys) after the test.
    /// </summary>
    private static string DerPublicKeyBase64
    {
        get
        {
            var pem = File.ReadAllText(TestPublicKeyPem);
            return ExtractDerBase64FromPem(pem, "PUBLIC KEY");
        }
    }

    private static byte[] DerPublicKeyBytes
    {
        get
        {
            var pem = File.ReadAllText(TestPublicKeyPem);
            var b64 = ExtractDerBase64FromPem(pem, "PUBLIC KEY");
            return Convert.FromBase64String(b64);
        }
    }

    private static byte[] DerPrivateKeyBytes
    {
        get
        {
            var pem = File.ReadAllText(TestPrivateKeyPem);
            var b64 = ExtractDerBase64FromPem(pem, "PRIVATE KEY");
            return Convert.FromBase64String(b64);
        }
    }

    private static string ExtractDerBase64FromPem(string pem, string label)
    {
        var begin = $"-----BEGIN {label}-----";
        var end = $"-----END {label}-----";
        int bi = pem.IndexOf(begin, StringComparison.Ordinal);
        int ei = pem.IndexOf(end, bi >= 0 ? bi + begin.Length : 0, StringComparison.Ordinal);
        Assert.True(bi >= 0 && ei >= 0, $"PEM block '{label}' not found.");
        var b64 = pem.Substring(bi + begin.Length, ei - (bi + begin.Length));
        var sb = new StringBuilder();
        foreach (char c in b64)
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// Creates a valid test manifest, signs it with the test private key, and returns the manifest.
    /// </summary>
    private static UpdateManifest CreateSignedManifest(string? keyId = null, string? version = null,
        string? artifactName = null, string? sha256 = null, long? sizeBytes = null,
        string? downloadUrl = null)
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 2,
            Product = "Deltempo",
            Channel = "stable",
            Version = version ?? "2.0.0-test",
            CommitSha = "abc1234def56789",
            Architecture = "win-x64",
            MinimumSupportedWindows = "10.0.17763",
            PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            KeyId = keyId ?? TestKeyId,
            Artifact = new UpdateArtifactInfo
            {
                Name = artifactName ?? "Deltempo.exe",
                Sha256 = sha256 ?? "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", // DevSkim: ignore DS173237
                SizeBytes = sizeBytes ?? 1024,
                DownloadUrl = downloadUrl ?? "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0-test/Deltempo.exe"
            }
        };

        // Sign the canonical JSON with the test private key.
        string canonicalJson = manifest.ToCanonicalJson();
        byte[] dataBytes = Encoding.UTF8.GetBytes(canonicalJson);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(DerPrivateKeyBytes, out _);
        byte[] signature = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
        manifest.Signature = Convert.ToBase64String(signature);

        return manifest;
    }

    /// <summary>
    /// Configures the verifier with the test key and asserts the round-trip verifies.
    /// </summary>
    [Fact]
    public void ManifestSignatureVerifier_PositivePath_ValidSignedManifest_VerifiesTrue()
    {
        // Arrange: provision the test key (fail-closed until then).
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest();

            // Act
            bool result = ManifestSignatureVerifier.VerifySignature(manifest);

            // Assert
            Assert.True(result, "A manifest signed with the provisioned test key must verify.");
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestSignatureVerifier_FailClosed_NoKeysConfigured_RejectsEverything()
    {
        // Arrange: no trusted keys configured.
        ManifestSignatureVerifier.ClearTrustedKeys();

        var manifest = CreateSignedManifest();

        // Act
        bool result = ManifestSignatureVerifier.VerifySignature(manifest);

        // Assert: fail closed — no trusted key means no signature is ever accepted.
        Assert.False(result, "With no trusted keys configured, signature verification must fail closed.");
    }

    [Fact]
    public void ManifestSignatureVerifier_Rejects_UnknownKeyId()
    {
        // Arrange: provision test key, but manifest signed with a different key id.
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest(keyId: "unknown-key-id");

            // Act
            bool result = ManifestSignatureVerifier.VerifySignature(manifest);

            // Assert
            Assert.False(result, "A manifest signed under an unknown key id must be rejected.");
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestSignatureVerifier_Rejects_TamperedManifest()
    {
        // Arrange: provision test key.
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest();
            // Tamper: change the version after signing.
            manifest.Version = "9.9.9-tampered";

            // Act
            bool result = ManifestSignatureVerifier.VerifySignature(manifest);

            // Assert
            Assert.False(result, "A tampered manifest must fail signature verification.");
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestSignatureVerifier_Rejects_WrongSigningKey()
    {
        // Arrange: provision test key, sign with a DIFFERENT key pair.
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            // Generate an unrelated key pair and sign with its private key.
            using var otherKey = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256")); // DevSkim: ignore DS440100
            var manifest = new UpdateManifest
            {
                SchemaVersion = 2,
                Product = "Deltempo",
                Channel = "stable",
                Version = "2.0.0-test",
                CommitSha = "abc1234",
                Architecture = "win-x64",
                MinimumSupportedWindows = "10.0.17763",
                PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                KeyId = TestKeyId,
                Artifact = new UpdateArtifactInfo
                {
                    Name = "Deltempo.exe",
                    Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", // DevSkim: ignore DS173237
                    SizeBytes = 1024,
                    DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0-test/Deltempo.exe"
                }
            };

            string canonicalJson = manifest.ToCanonicalJson();
            byte[] dataBytes = Encoding.UTF8.GetBytes(canonicalJson);
            byte[] signature = otherKey.SignData(dataBytes, HashAlgorithmName.SHA256);
            manifest.Signature = Convert.ToBase64String(signature);

            // Act
            bool result = ManifestSignatureVerifier.VerifySignature(manifest);

            // Assert
            Assert.False(result, "A manifest signed with a different (unprovisioned) private key must be rejected.");
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestSignatureVerifier_Rejects_EmptySignature()
    {
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest();
            manifest.Signature = "";

            bool result = ManifestSignatureVerifier.VerifySignature(manifest);
            Assert.False(result, "An empty signature must be rejected.");
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestSignatureVerifier_Rejects_MalformedSignature()
    {
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest();
            manifest.Signature = "not-valid-base64!!!";

            bool result = ManifestSignatureVerifier.VerifySignature(manifest);
            Assert.False(result, "A malformed signature must be rejected.");
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestVerifyManifestJson_Accepts_ValidSignedManifest()
    {
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest();
            string json = JsonSerializer.Serialize(manifest, UpdateJsonContext.Default.UpdateManifest);

            var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);

            Assert.NotNull(result);
            Assert.Null(error);
            Assert.Equal("2.0.0-test", result!.Version);
            Assert.Equal(TestKeyId, result.KeyId);
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestVerifyManifestJson_Rejects_UnsignedManifest()
    {
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = new UpdateManifest
            {
                SchemaVersion = 2,
                Product = "Deltempo",
                Version = "2.0.0-test",
                KeyId = TestKeyId,
                Artifact = new UpdateArtifactInfo
                {
                    Name = "Deltempo.exe",
                    Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", // DevSkim: ignore DS173237
                    SizeBytes = 1024,
                    DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0-test/Deltempo.exe"
                }
                // No signature set
            };

            string json = JsonSerializer.Serialize(manifest, UpdateJsonContext.Default.UpdateManifest);
            var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);

            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Contains("signature", error!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public void ManifestVerifyManifestJson_Rejects_WrongKeyId()
    {
        ManifestSignatureVerifier.ClearTrustedKeys();
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[] { (TestKeyId, DerPublicKeyBase64) });

        try
        {
            var manifest = CreateSignedManifest(keyId: "other-key");
            string json = JsonSerializer.Serialize(manifest, UpdateJsonContext.Default.UpdateManifest);

            var (result, error) = ManifestSignatureVerifier.VerifyManifestJson(json);

            Assert.Null(result);
            Assert.NotNull(error);
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    #region URL validation security tests

    [Theory]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe", true)]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/WinTempCleaner.exe", true)]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/checksums.sha256", true)]
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/deltempo_cli.exe", true)]
    public void UpdateSecurityValidator_Accepts_ValidReleaseUrls(string url, bool expected)
    {
        bool result = UpdateSecurityValidator.IsValidDownloadUrl(url, out string reason);
        Assert.Equal(expected, result);
        if (!expected) Assert.False(string.IsNullOrEmpty(reason));
    }

    [Theory]
    [InlineData("http://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe")] // plain HTTP // DevSkim: ignore DS137138
    [InlineData("https://evil.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe")] // disallowed host
    [InlineData("https://github.com/attacker/Deltempo/releases/download/v2.0.0/Deltempo.exe")] // wrong org
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/malware.exe")] // wrong filename
    [InlineData("ftp://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe")] // wrong scheme
    [InlineData("https://github.com:8443/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe")] // non-standard port
    [InlineData("https://user:pass@github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe")] // user-info trick
    [InlineData("https://github.com/Beso1227/Deltempo/releases/download/%2e%2e/Deltempo.exe")] // encoded traversal
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("   ")]
    public void UpdateSecurityValidator_Rejects_MaliciousUrls(string url)
    {
        bool result = UpdateSecurityValidator.IsValidDownloadUrl(url, out string reason);
        Assert.False(result, $"URL '{url}' must be rejected: {reason}");
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void UpdateSecurityValidator_Rejects_Redirect_To_DisallowedHost()
    {
        // A redirect to evil.com must be rejected even if the initial URL is valid.
        var redirectUri = new Uri("https://evil.com/malware.exe");
        bool result = UpdateSecurityValidator.IsValidRedirectTarget(redirectUri, out string reason);
        Assert.False(result);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void UpdateSecurityValidator_Accepts_Redirect_To_SameAllowlistedHost()
    {
        var redirectUri = new Uri("https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe");
        bool result = UpdateSecurityValidator.IsValidRedirectTarget(redirectUri, out string reason);
        Assert.True(result);
    }

    #endregion

    #region Download size and integrity tests

    [Fact]
    public void PatchIntegrityVerifier_Rejects_HashMismatch_AgainstManifest()
    {
        // The fix: staged artifact must be verified against the manifest's expected hash,
        // not against its own actual hash (self-verification).
        string tempFile = Path.GetTempFileName();
        try
        {
            // Write a valid PE-ish file (at least 1024 bytes) so PE validation passes
            // and we reach the hash comparison.
            var peBytes = new byte[2048];
            peBytes[0] = 0x4D; peBytes[1] = 0x5A; // MZ
            peBytes[0x3C] = 0x80; // e_lfanew
            peBytes[0x80] = 0x50; peBytes[0x81] = 0x45; peBytes[0x82] = 0x00; peBytes[0x83] = 0x00; // PE\0\0
            File.WriteAllBytes(tempFile, peBytes);

            string wrongSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; // DevSkim: ignore DS173237

            // VerifyStagedArtifact with wrong expected hash must fail.
            bool result = PatchIntegrityVerifier.VerifyStagedArtifact(
                tempFile, wrongSha, new FileInfo(tempFile).Length, out string reason);

            Assert.False(result);
            Assert.Contains("mismatch", reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void PatchIntegrityVerifier_Rejects_SizeMismatch_AgainstManifest()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0x4D, 0x5A });

            bool result = PatchIntegrityVerifier.VerifyStagedArtifact(
                tempFile, "", 999999, out string reason);

            Assert.False(result);
            Assert.Contains("size mismatch", reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(filePath);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    #endregion

    #region Fail-closed: no helper launch when checks fail

    [Fact]
    public async Task DownloadAndApplyUpdateAsync_Rejects_WhenNoTrustedKeyConfigured()
    {
        // Fail closed: with no trusted key configured, even a valid download URL must
        // not proceed if a manifest is required and cannot be authenticated.
        ManifestSignatureVerifier.ClearTrustedKeys();

        try
        {
            var progress = new Progress<double>();

            // A manifest URL is provided but no key is configured → fail closed.
            await Assert.ThrowsAsync<SecurityException>(async () =>
            {
                await UpdateService.DownloadAndApplyUpdateAsync(
                    "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe",
                    progress,
                    manifestUrl: "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/manifest.json");
            });
        }
        finally
        {
            ManifestSignatureVerifier.ClearTrustedKeys();
        }
    }

    [Fact]
    public async Task DownloadAndApplyUpdateAsync_Rejects_UntrustedHost()
    {
        var progress = new Progress<double>();

        // HTTP instead of HTTPS.
        await Assert.ThrowsAsync<SecurityException>(async () =>
        {
            await UpdateService.DownloadAndApplyUpdateAsync(
                "http://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe", // DevSkim: ignore DS137138
                progress);
        });

        // Disallowed host.
        await Assert.ThrowsAsync<SecurityException>(async () =>
        {
            await UpdateService.DownloadAndApplyUpdateAsync(
                "https://evil-site.com/malware.exe",
                progress);
        });
    }

    #endregion

    #region Fail-closed: published checksum must never silently disappear

    [Fact]
    public void ChecksumGate_ReleaseListsChecksumAsset_AndHashParsed_Allows()
    {
        // The normal path: checksums.sha256 listed AND its hash parsed. Nothing to
        // reject; the SHA-256 check runs as before.
        UpdateService.EnsurePublishedChecksumAvailable(
            "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/checksums.sha256",
            "a".PadRight(64, '0'),
            manifestUrl: null);
    }

    [Fact]
    public void ChecksumGate_ReleaseListsChecksumAsset_ButFetchFailed_Rejects()
    {
        // checksums.sha256 is advertised but the fetch threw (represented here by
        // no hash being supplied): the update must be refused, not verified by
        // Authenticode alone.
        var ex = Assert.Throws<SecurityException>(() =>
            UpdateService.EnsurePublishedChecksumAvailable(
                "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/checksums.sha256",
                expectedSha256: null,
                manifestUrl: null));

        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChecksumGate_ReleaseListsChecksumAsset_ButRowUnparseable_Rejects()
    {
        // The parse pass ran but found no row matching the artifact: same refusal.
        Assert.Throws<SecurityException>(() =>
            UpdateService.EnsurePublishedChecksumAvailable(
                "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/checksums.sha256",
                expectedSha256: string.Empty,
                manifestUrl: null));
    }

    [Fact]
    public void ChecksumGate_ReleaseHasNoChecksumAsset_StandsAsBefore()
    {
        // No checksums.sha256 asset on the release: Authenticode + HTTPS + size
        // remain the defense layers, and this accepted fallback is not an error.
        UpdateService.EnsurePublishedChecksumAvailable(
            releaseChecksumUrl: null,
            expectedSha256: null,
            manifestUrl: null);

        UpdateService.EnsurePublishedChecksumAvailable(
            releaseChecksumUrl: string.Empty,
            expectedSha256: null,
            manifestUrl: null);
    }

    [Fact]
    public void ChecksumGate_ManifestBranch_IsExempt()
    {
        // The manifest branch binds its hash inside the signed payload; the gate
        // must not reject it even when no separate checksum URL is known.
        UpdateService.EnsurePublishedChecksumAvailable(
            releaseChecksumUrl: "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/checksums.sha256",
            expectedSha256: null,
            manifestUrl: "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/manifest.json");
    }

    [Fact]
    public async Task DownloadAndApplyUpdateAsync_RejectsAtEntry_WhenChecksumWasExpectedButMissing()
    {
        // Integration: the gate fires before any download, so no network is touched.
        var progress = new Progress<double>();

        var ex = await Assert.ThrowsAsync<SecurityException>(async () =>
        {
            await UpdateService.DownloadAndApplyUpdateAsync(
                "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/Deltempo.exe",
                progress,
                expectedSha256: null,
                releaseChecksumUrl: "https://github.com/Beso1227/Deltempo/releases/download/v2.0.0/checksums.sha256");
        });

        Assert.Contains("Checksum verification was expected but failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
