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
    public string ToCanonicalJson()
    {
        // Build canonical form manually for deterministic signature
        return $"{{\"architecture\":\"{Architecture}\",\"channel\":\"{Channel}\",\"commitSha\":\"{CommitSha}\",\"minimumSupportedWindows\":\"{MinimumSupportedWindows}\",\"product\":\"{Product}\",\"publishedAtUtc\":\"{PublishedAtUtc:O}\",\"schemaVersion\":{SchemaVersion},\"version\":\"{Version}\"}}";
    }
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
