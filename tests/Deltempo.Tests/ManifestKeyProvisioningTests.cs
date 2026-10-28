using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Proves that an operator can actually unblock signed updates by provisioning a key.
///
/// This exists because the loading APIs (<c>TryLoadProvisionedKeyFromFile</c> /
/// <c>TryLoadProvisionedKeyFromEnvironment</c>) had **no call site outside the test suite**.
/// The verifier therefore failed closed forever: dropping a key on disk would not have been
/// enough, because nothing in the shipping code ever read it.
///
/// These tests drive the real resolution order through environment variables and assert that
/// <c>VerifySignature</c> succeeds - i.e. the wiring works, not just the primitives.
/// </summary>
// Shares a collection with UpdatePipelineSecurityTests: ManifestSignatureVerifier holds its
// trusted-key set in process-global statics, so these classes would otherwise race each other
// and clear/reseed the key set mid-run.
[Collection("UpdateSignatureState")]
public class ManifestKeyProvisioningTests : IDisposable
{
    private const string TestKeyId = "provisioned-test-key";
    private static readonly string[] EnvVars =
    {
        ManifestSignatureVerifier.TrustedKeyIdVariable,
        ManifestSignatureVerifier.TrustedKeyMaterialVariable,
        ManifestSignatureVerifier.TrustedKeyFileVariable,
    };

    private static string FixturesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));

    private static readonly string PublicKeyPemPath =
        Path.Combine(FixturesDir, "test-update-verification-key.pub");

    private static readonly string PrivateKeyPemPath =
        Path.Combine(FixturesDir, "test-update-signing-key.pem");

    public ManifestKeyProvisioningTests()
    {
        foreach (var v in EnvVars) Environment.SetEnvironmentVariable(v, null);
        ManifestSignatureVerifier.ClearTrustedKeys();
        ResetProvisioningAttempted();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var v in EnvVars) Environment.SetEnvironmentVariable(v, null);
        ManifestSignatureVerifier.ClearTrustedKeys();
        ResetProvisioningAttempted();
    }

    /// <summary>
    /// ClearTrustedKeys marks provisioning settled so tests stay isolated. These tests need to
    /// re-run the real discovery path, so the flag is reset via reflection.
    /// </summary>
    private static void ResetProvisioningAttempted()
    {
        var f = typeof(ManifestSignatureVerifier).GetField(
            "_provisioningAttempted", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(f);
        f.SetValue(null, false);
    }

    private static byte[] DerFromPem(string path, string label)
    {
        string pem = File.ReadAllText(path);
        var begin = $"-----BEGIN {label}-----";
        var end = $"-----END {label}-----";
        int bi = pem.IndexOf(begin, StringComparison.Ordinal);
        int ei = pem.IndexOf(end, bi >= 0 ? bi + begin.Length : 0, StringComparison.Ordinal);
        Assert.True(bi >= 0 && ei >= 0, $"PEM block '{label}' not found in {path}.");
        var b64 = pem.Substring(bi + begin.Length, ei - (bi + begin.Length));
        var sb = new StringBuilder();
        foreach (char c in b64)
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        return Convert.FromBase64String(sb.ToString());
    }

    private static UpdateManifest SignedManifest()
    {
        // The fixture's PEM is labelled "PRIVATE KEY" but holds SEC1 ECPrivateKey bytes,
        // so it must be imported with ImportECPrivateKey (matching UpdatePipelineSecurityTests).
        byte[] der = DerFromPem(PrivateKeyPemPath, "PRIVATE KEY");
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(der, out _);

        var manifest = new UpdateManifest
        {
            Product = "Deltempo",
            Version = "9.9.9",
            KeyId = TestKeyId,
            Artifact = new UpdateArtifactInfo
            {
                Name = "Deltempo.exe",
                SizeBytes = 1234,
                Sha256 = new string('a', 64),
                DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v9.9.9/Deltempo.exe",
            },
        };

        manifest.Signature = Convert.ToBase64String(
            ecdsa.SignData(Encoding.UTF8.GetBytes(manifest.ToCanonicalJson()), HashAlgorithmName.SHA256));
        return manifest;
    }

    // ── The wiring gap this test class exists to close ─────────────────────────

    [Fact]
    public void ProvisioningFromKeyFile_MakesVerificationSucceed()
    {
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyIdVariable, TestKeyId);
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyFileVariable, PublicKeyPemPath);
        ResetProvisioningAttempted();

        Assert.True(ManifestSignatureVerifier.VerifySignature(SignedManifest()),
            "An operator-provisioned key file must make a correctly signed manifest verify.");
    }

    [Fact]
    public void ProvisioningFromBase64Material_MakesVerificationSucceed()
    {
        string b64 = Convert.ToBase64String(DerFromPem(PublicKeyPemPath, "PUBLIC KEY"));
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyIdVariable, TestKeyId);
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyMaterialVariable, b64);
        ResetProvisioningAttempted();

        Assert.True(ManifestSignatureVerifier.VerifySignature(SignedManifest()),
            "Base64 key material in the environment must make verification succeed.");
    }

    [Fact]
    public void ProvisionedKeyWithWrongKeyId_RejectsTheManifest()
    {
        // The manifest is signed by the provisioned key but claims a different kid.
        // Key binding must still fail.
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyIdVariable, "some-other-key");
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyFileVariable, PublicKeyPemPath);
        ResetProvisioningAttempted();

        Assert.False(ManifestSignatureVerifier.VerifySignature(SignedManifest()),
            "kid binding must survive host provisioning; a mismatched kid must not verify.");
    }

    // ── Fail-closed behaviour is preserved ────────────────────────────────────

    [Fact]
    public void NoProvisioning_StillFailsClosed()
    {
        ResetProvisioningAttempted();

        Assert.False(ManifestSignatureVerifier.VerifySignature(SignedManifest()),
            "With nothing provisioned, verification must fail closed.");
        Assert.Empty(ManifestSignatureVerifier.TrustedKeyIds);
    }

    [Fact]
    public void ProvisionedKey_DoesNotVerifyATamperedManifest()
    {
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyIdVariable, TestKeyId);
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyFileVariable, PublicKeyPemPath);
        ResetProvisioningAttempted();

        var manifest = SignedManifest();
        Assert.NotNull(manifest.Artifact);
        manifest.Artifact.Sha256 = new string('b', 64); // mutate after signing

        Assert.False(ManifestSignatureVerifier.VerifySignature(manifest),
            "A valid key must not make a tampered manifest verify.");
    }

    [Fact]
    public void ProvisioningIsIdempotent_AndDoesNotOverrideExplicitConfiguration()
    {
        ManifestSignatureVerifier.ConfigureTrustedKeys(new[]
        {
            (TestKeyId, Convert.ToBase64String(DerFromPem(PublicKeyPemPath, "PUBLIC KEY")))
        });

        // Point host provisioning at a path that does not exist. It must not disturb the
        // explicitly configured key.
        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyFileVariable,
            Path.Combine(Path.GetTempPath(), "definitely-missing-key.pub"));
        ResetProvisioningAttempted();

        Assert.True(ManifestSignatureVerifier.VerifySignature(SignedManifest()));
        Assert.Equal(new[] { TestKeyId }, ManifestSignatureVerifier.TrustedKeyIds.ToArray());
    }

    [Fact]
    public void DefaultKeyId_IsUsedWhenNoIdIsSupplied()
    {
        // Documents the default an operator gets if they only drop a key file in place.
        Assert.False(string.IsNullOrWhiteSpace(ManifestSignatureVerifier.DefaultTrustedKeyId));

        Environment.SetEnvironmentVariable(ManifestSignatureVerifier.TrustedKeyFileVariable, PublicKeyPemPath);
        ResetProvisioningAttempted();

        Assert.True(ManifestSignatureVerifier.TryProvisionTrustedKeysFromHost());
        Assert.Equal(
            new[] { ManifestSignatureVerifier.DefaultTrustedKeyId },
            ManifestSignatureVerifier.TrustedKeyIds.ToArray());
    }
}
