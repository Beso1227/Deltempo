using System.Text.Json.Serialization;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Canonical Version 2 Schema for Deltempo update manifests.
/// Supports both Stable and Patch channels with ECDSA P-256 signature authentication.
/// </summary>
public class UpdateManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    [JsonPropertyName("product")]
    public string Product { get; set; } = "Deltempo";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("commitSha")]
    public string CommitSha { get; set; } = string.Empty;

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "win-x64";

    [JsonPropertyName("minimumSupportedWindows")]
    public string MinimumSupportedWindows { get; set; } = "10.0.17763";

    [JsonPropertyName("publishedAtUtc")]
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = string.Empty;

    [JsonPropertyName("artifact")]
    public UpdateArtifactInfo? Artifact { get; set; }

    [JsonPropertyName("cliArtifact")]
    public UpdateArtifactInfo? CliArtifact { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Produces the canonical JSON string for signature verification.
    /// Excludes the signature field itself. Uses deterministic key ordering.
    /// </summary>
    /// <remarks>
    /// The artifact descriptors MUST be inside the signed payload. If they are not, the
    /// signature only authenticates metadata (version, channel) and not *what will be
    /// downloaded* - anyone able to alter the manifest could substitute a different
    /// downloadUrl and a matching sha256, and the signature would still verify.
    /// </remarks>
    public string ToCanonicalJson()
    {
        // Build canonical form manually for deterministic signature
        return $"{{\"architecture\":{Json(Architecture)},\"artifact\":{ArtifactJson(Artifact)}," +
               $"\"channel\":{Json(Channel)},\"cliArtifact\":{ArtifactJson(CliArtifact)}," +
               $"\"commitSha\":{Json(CommitSha)},\"keyId\":{Json(KeyId)}," +
               $"\"minimumSupportedWindows\":{Json(MinimumSupportedWindows)},\"product\":{Json(Product)}," +
               $"\"publishedAtUtc\":\"{PublishedAtUtc:O}\",\"schemaVersion\":{SchemaVersion},\"version\":{Json(Version)}}}";
    }

    /// <summary>
    /// JSON-escapes a string. Without this, any quote or backslash inside a value would
    /// corrupt the canonical form and could make two different manifests produce the same
    /// signed bytes.
    /// </summary>
    private static string Json(string? value) =>
        System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);

    private static string ArtifactJson(UpdateArtifactInfo? a) =>
        a == null
            ? "null"
            : $"{{\"downloadUrl\":{Json(a.DownloadUrl)},\"name\":{Json(a.Name)}," +
              $"\"sha256\":{Json(a.Sha256)},\"sizeBytes\":{a.SizeBytes}}}";
}

public class UpdateArtifactInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;
}
