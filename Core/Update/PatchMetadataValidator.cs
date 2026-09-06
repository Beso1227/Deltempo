using System.Text.RegularExpressions;

namespace WinTempCleaner.Core.Update;

public class PatchValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static PatchValidationResult Success() => new() { IsValid = true };
    public static PatchValidationResult Failure(string error) => new() { IsValid = false, ErrorMessage = error };
}

/// <summary>
/// Validates remote patch manifest structure, fields, and security boundaries.
/// Enforces that remote metadata cannot define arbitrary execution targets or bypass host restrictions.
/// </summary>
public static class PatchMetadataValidator
{
    private static readonly Regex HexShaRegex = new(@"^[0-9a-fA-F]{7,40}$", RegexOptions.Compiled);
    private static readonly Regex HexSha256Regex = new(@"^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

    public static PatchValidationResult Validate(PatchManifest? manifest)
    {
        if (manifest == null)
        {
            return PatchValidationResult.Failure("Manifest is null or could not be parsed.");
        }

        if (!manifest.Channel.Equals("patch", StringComparison.OrdinalIgnoreCase))
        {
            return PatchValidationResult.Failure($"Invalid channel '{manifest.Channel}'. Expected 'patch'.");
        }

        if (!manifest.Product.Equals("Deltempo", StringComparison.OrdinalIgnoreCase))
        {
            return PatchValidationResult.Failure($"Unexpected product '{manifest.Product}'. Expected 'Deltempo'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CommitSha) || !HexShaRegex.IsMatch(manifest.CommitSha.Trim()))
        {
            return PatchValidationResult.Failure("CommitSha is missing or not a valid git commit hex identifier.");
        }

        string sha256 = manifest.Sha256?.Trim() ?? "";
        if (string.IsNullOrEmpty(sha256) || !HexSha256Regex.IsMatch(sha256))
        {
            return PatchValidationResult.Failure("Sha256 hash is missing or not a valid 64-character hex string.");
        }

        long size = manifest.FileSizeBytes;
        if (size < 10 * 1024 * 1024 || size > 500 * 1024 * 1024)
        {
            return PatchValidationResult.Failure($"File size {size} bytes is outside plausible bounds (10 MB – 500 MB).");
        }

        string downloadUrl = manifest.DownloadUrl?.Trim() ?? "";
        if (string.IsNullOrEmpty(downloadUrl))
        {
            return PatchValidationResult.Failure("DownloadUrl violation: URL is empty.");
        }

        if (!IsValidDownloadUrl(downloadUrl, out string urlError))
        {
            return PatchValidationResult.Failure($"DownloadUrl violation: {urlError}");
        }

        return PatchValidationResult.Success();
    }

    public static bool IsValidDownloadUrl(string url, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            reason = "URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "Malformed URI.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            reason = "Protocol must strictly be HTTPS.";
            return false;
        }

        string host = uri.Host.ToLowerInvariant();
        bool isAllowedHost = host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                             host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                             host.Equals("githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                             host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

        if (!isAllowedHost)
        {
            reason = $"Untrusted download host '{host}'. Must strictly originate from verified GitHub domains.";
            return false;
        }

        return true;
    }
}
