using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Verifies ECDSA P-256 signatures on update manifests.
/// Embedded public key in SubjectPublicKeyInfo format.
/// Fails closed on missing, invalid, or forged signatures.
/// </summary>
public static class ManifestSignatureVerifier
{
    // Embedded ECDSA P-256 public key for Deltempo update signing.
    // This is the public half of the signing key pair; the private key
    // is stored in GitHub Actions secrets (UPDATER_SIGNING_KEY).
    // The key is base64-encoded SubjectPublicKeyInfo (DER).
    private const string PublicKeyBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE";

    /// <summary>
    /// Verifies the manifest signature using the embedded public key.
    /// </summary>
    /// <param name="manifest">The deserialized manifest to verify.</param>
    /// <param name="canonicalJson">The canonical JSON string that was signed.</param>
    /// <returns>True if signature is valid; false otherwise.</returns>
    public static bool VerifySignature(UpdateManifest manifest, string? canonicalJson = null)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Signature))
            return false;

        try
        {
            string json = canonicalJson ?? manifest.ToCanonicalJson();
            byte[] dataBytes = Encoding.UTF8.GetBytes(json);
            byte[] signatureBytes = Convert.FromBase64String(manifest.Signature);

            byte[] publicKeyBytes = Convert.FromBase64String(PublicKeyBase64);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies a manifest JSON string end-to-end: parse, validate schema, verify signature.
    /// </summary>
    public static (UpdateManifest? manifest, string? error) VerifyManifestJson(string manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            return (null, "Manifest JSON is empty.");

        UpdateManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return (null, $"Manifest JSON parse error: {ex.Message}");
        }

        if (manifest == null)
            return (null, "Manifest deserialized to null.");

        if (manifest.SchemaVersion != 2)
            return (null, $"Unsupported schema version {manifest.SchemaVersion}. Expected 2.");

        if (!manifest.Product.Equals("Deltempo", StringComparison.OrdinalIgnoreCase))
            return (null, $"Unexpected product '{manifest.Product}'.");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            return (null, "Manifest version is missing.");

        if (manifest.Artifact == null || string.IsNullOrWhiteSpace(manifest.Artifact.Sha256))
            return (null, "Manifest artifact or SHA-256 is missing.");

        if (!VerifySignature(manifest))
            return (null, "Manifest signature verification failed. Manifest may have been tampered with.");

        return (manifest, null);
    }
}
