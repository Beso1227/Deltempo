using System.Text.Json.Serialization;

namespace WinTempCleaner.Core.Update;

public class PatchArtifactInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Deltempo.exe";

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
/// Machine-readable patch manifest uniquely identifying a continuous patch build.
/// Supports both modern structured schema and flat properties for maximum compatibility.
/// </summary>
public class PatchManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("product")]
    public string Product { get; set; } = "Deltempo";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "patch";

    [JsonPropertyName("baseVersion")]
    public string BaseVersion { get; set; } = "1.3.4";

    [JsonPropertyName("targetVersion")]
    public string TargetVersion { get; set; } = "1.3.4";

    [JsonPropertyName("commitSha")]
    public string CommitSha { get; set; } = string.Empty;

    [JsonPropertyName("shortSha")]
    public string ShortSha { get; set; } = string.Empty;

    [JsonPropertyName("buildId")]
    public string BuildId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "win-x64";

    [JsonPropertyName("minimumSupportedWindows")]
    public string MinimumSupportedWindows { get; set; } = "10.0.17763";

    [JsonPropertyName("commitMessage")]
    public string CommitMessage { get; set; } = string.Empty;

    [JsonPropertyName("artifact")]
    public PatchArtifactInfo? Artifact { get; set; }

    [JsonPropertyName("cliArtifact")]
    public PatchArtifactInfo? CliArtifact { get; set; }

    // Flat compatibility getters/setters
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl
    {
        get => Artifact?.DownloadUrl ?? _flatDownloadUrl;
        set => _flatDownloadUrl = value;
    }
    private string _flatDownloadUrl = string.Empty;

    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes
    {
        get => Artifact?.SizeBytes ?? _flatFileSizeBytes;
        set => _flatFileSizeBytes = value;
    }
    private long _flatFileSizeBytes;

    [JsonPropertyName("sha256")]
    public string Sha256
    {
        get => Artifact?.Sha256 ?? _flatSha256;
        set => _flatSha256 = value;
    }
    private string _flatSha256 = string.Empty;
}
