using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Verifies ECDSA P-256 signatures on update manifests.
///
/// Trusted public keys are provisioned explicitly at startup via
/// <see cref="ConfigureTrustedKeys"/>, <see cref="TryLoadProvisionedKeyFromFile"/>, or
/// <see cref="TryLoadProvisionedKeyFromEnvironment"/>. With no keys configured the verifier
/// fails closed: no manifest signature will ever be accepted.
///
/// Never embed a private signing key in this assembly or repository. Only public verification
/// key material may be provisioned, and only by the operator.
/// </summary>
public static class ManifestSignatureVerifier
{
    private static readonly Dictionary<string, byte[]> _trustedKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _keysLock = new();

    /// <summary>
    /// Currently provisioned trusted key identifiers (read-only snapshot).
    /// Empty means fail-closed: no manifest will verify.
    /// </summary>
    public static IReadOnlyList<string> TrustedKeyIds
    {
        get
        {
            lock (_keysLock)
            {
                return _trustedKeys.Keys.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Configures the set of trusted ECDSA P-256 verification keys, replacing any previous set.
    /// Keys are base64-encoded SubjectPublicKeyInfo (DER) paired with a key identifier.
    /// </summary>
    public static void ConfigureTrustedKeys(IEnumerable<(string keyId, string publicKeyBase64)> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var next = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (keyId, publicKeyBase64) in keys)
        {
            if (string.IsNullOrWhiteSpace(keyId))
                throw new ArgumentException("Key identifier must not be empty.", nameof(keys));
            if (string.IsNullOrWhiteSpace(publicKeyBase64))
                throw new ArgumentException($"Public key for key id '{keyId}' must not be empty.", nameof(keys));

            byte[] der;
            try
            {
                der = Convert.FromBase64String(publicKeyBase64);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException($"Invalid base64 public key for key id '{keyId}'.", nameof(keys), ex);
            }

            // Validate that the key imports as an ECDSA key.
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(der, out _);

            next[keyId] = der;
        }

        lock (_keysLock)
        {
            _trustedKeys.Clear();
            foreach (var kvp in next)
                _trustedKeys[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Clears all trusted keys, returning the verifier to fail-closed state.
    /// Also marks host provisioning as settled, so a test that isolates the trusted set is
    /// never re-seeded from the machine's environment.
    /// </summary>
    public static void ClearTrustedKeys()
    {
        lock (_keysLock)
        {
            _trustedKeys.Clear();
        }

        lock (ProvisioningLock)
        {
            _provisioningAttempted = true;
        }
    }

    /// <summary>
    /// Loads a trusted public key from a PEM file (SubjectPublicKeyInfo block).
    /// Fails closed if the file is missing, unreadable, or contains no valid key.
    /// </summary>
    public static bool TryLoadProvisionedKeyFromFile(string keyId, string pemFilePath)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Key identifier must not be empty.", nameof(keyId));
        if (string.IsNullOrWhiteSpace(pemFilePath))
            throw new ArgumentException("PEM file path must not be empty.", nameof(pemFilePath));

        if (!File.Exists(pemFilePath))
            return false;

        try
        {
            var pem = File.ReadAllText(pemFilePath);
            var der = ExtractDerFromPem(pem, "PUBLIC KEY");
            if (der == null)
                return false;

            ConfigureTrustedKeys(new[] { (keyId, Convert.ToBase64String(der)) });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads a trusted public key from an environment variable containing base64-encoded
    /// SubjectPublicKeyInfo (DER) bytes.
    /// Fails closed if the variable is unset or invalid.
    /// </summary>
    public static bool TryLoadProvisionedKeyFromEnvironment(string keyId, string environmentVariableName)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Key identifier must not be empty.", nameof(keyId));
        if (string.IsNullOrWhiteSpace(environmentVariableName))
            throw new ArgumentException("Environment variable name must not be empty.", nameof(environmentVariableName));

        var base64 = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        try
        {
            var der = Convert.FromBase64String(base64);
            ConfigureTrustedKeys(new[] { (keyId, base64) });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Environment variable naming the trusted key id (the manifest's <c>kid</c>).</summary>
    public const string TrustedKeyIdVariable = "DELTEMPOMANIFEST_TRUSTED_KEY_ID";

    /// <summary>Environment variable holding base64 SubjectPublicKeyInfo (DER) key material.</summary>
    public const string TrustedKeyMaterialVariable = "DELTEMPOMANIFEST_TRUSTED_KEY";

    /// <summary>Environment variable holding a path to a PEM public key file.</summary>
    public const string TrustedKeyFileVariable = "DELTEMPOMANIFEST_TRUSTED_KEY_FILE";

    /// <summary>Key id assumed when only a key file is provisioned and no id is supplied.</summary>
    public const string DefaultTrustedKeyId = "deltempo-release-2026a";

    private static readonly object ProvisioningLock = new();
    private static bool _provisioningAttempted;

    /// <summary>
    /// Resolves the operator-provisioned trusted key and installs it, once per process.
    ///
    /// This exists because the loading APIs are otherwise only reachable from tests: without a
    /// call site in the shipping code, provisioning a key would still leave verification
    /// permanently fail-closed. Resolution order:
    /// <list type="number">
    /// <item><description>Key file named by <see cref="TrustedKeyFileVariable"/>.</description></item>
    /// <item><description>Key material in <see cref="TrustedKeyMaterialVariable"/> (base64 DER).</description></item>
    /// <item><description>A PEM key at <c>%ProgramData%\Deltempo\update-signing-key.pub</c>.</description></item>
    /// </list>
    /// The key id comes from <see cref="TrustedKeyIdVariable"/>, falling back to
    /// <see cref="DefaultTrustedKeyId"/>. Absent or malformed provisioning simply yields no
    /// keys, which keeps the existing fail-closed behaviour.
    /// </summary>
    public static bool TryProvisionTrustedKeysFromHost()
    {
        lock (ProvisioningLock)
        {
            if (_provisioningAttempted)
                return TrustedKeyIds.Count > 0;
            _provisioningAttempted = true;

            lock (_keysLock)
            {
                if (_trustedKeys.Count > 0)
                    return true; // Explicitly configured; never override.
            }

            string? keyId = Environment.GetEnvironmentVariable(TrustedKeyIdVariable);
            if (string.IsNullOrWhiteSpace(keyId))
                keyId = DefaultTrustedKeyId;

            try
            {
                string? path = Environment.GetEnvironmentVariable(TrustedKeyFileVariable);
                if (!string.IsNullOrWhiteSpace(path) && TryLoadProvisionedKeyFromFile(keyId, path))
                    return true;

                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TrustedKeyMaterialVariable))
                    && TryLoadProvisionedKeyFromEnvironment(keyId, TrustedKeyMaterialVariable))
                    return true;

                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Deltempo", "update-signing-key.pub");
                if (TryLoadProvisionedKeyFromFile(keyId, fallback))
                    return true;
            }
            catch
            {
                // Provisioning is best-effort discovery. Any failure leaves zero keys,
                // which is the fail-closed state.
            }

            return false;
        }
    }

    /// <summary>
    /// Verifies the manifest signature using the currently provisioned trusted keys.
    /// Returns false if no keys are configured (fail-closed), the manifest is null,
    /// the signature is missing/malformed, or the key identifier is unknown.
    /// </summary>
    public static bool VerifySignature(UpdateManifest manifest, string? canonicalJson = null)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Signature))
            return false;

        // Operator provisioning is resolved here rather than at a startup call site, so no
        // entry point can reach verification with the trusted set still empty.
        TryProvisionTrustedKeysFromHost();

        byte[] publicKeyBytes;
        lock (_keysLock)
        {
            if (_trustedKeys.Count == 0)
                return false; // Fail closed: no provisioned key.

            if (string.IsNullOrWhiteSpace(manifest.KeyId) ||
                !_trustedKeys.TryGetValue(manifest.KeyId, out publicKeyBytes!))
                return false; // Unknown key identifier.
        }

        try
        {
            string json = canonicalJson ?? manifest.ToCanonicalJson();
            byte[] dataBytes = Encoding.UTF8.GetBytes(json);
            byte[] signatureBytes = Convert.FromBase64String(manifest.Signature);

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
            manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson, UpdateJsonContext.Default.UpdateManifest);
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

        if (string.IsNullOrWhiteSpace(manifest.KeyId))
            return (null, "Manifest key identifier is missing.");

        if (manifest.Artifact == null || string.IsNullOrWhiteSpace(manifest.Artifact.Sha256))
            return (null, "Manifest artifact or SHA-256 is missing.");

        if (!VerifySignature(manifest))
            return (null, "Manifest signature verification failed. Manifest may have been tampered with.");

        return (manifest, null);
    }

    private static byte[]? ExtractDerFromPem(string pem, string label)
    {
        var begin = $"-----BEGIN {label}-----";
        var end = $"-----END {label}-----";

        int beginIdx = pem.IndexOf(begin, StringComparison.Ordinal);
        int endIdx = pem.IndexOf(end, beginIdx >= 0 ? beginIdx + begin.Length : 0, StringComparison.Ordinal);
        if (beginIdx < 0 || endIdx < 0)
            return null;

        var b64 = pem.Substring(beginIdx + begin.Length, endIdx - (beginIdx + begin.Length));
        var sb = new StringBuilder();
        foreach (char c in b64)
        {
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        }

        try
        {
            return Convert.FromBase64String(sb.ToString());
        }
        catch
        {
            return null;
        }
    }
}
