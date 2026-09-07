using System.IO;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Validates update download URLs to prevent SSRF, MITM, or untrusted payload staging.
/// Enforces HTTPS and origin from official GitHub release distribution hosts and repository paths.
/// </summary>
public static class UpdateSecurityValidator
{
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

        // Strict path validation: must be under Beso1227/Deltempo/releases/download/
        string path = uri.AbsolutePath;
        if (!path.StartsWith("/Beso1227/Deltempo/releases/download/", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Untrusted download path '{path}'. Must originate from Beso1227/Deltempo releases.";
            return false;
        }

        // Validate artifact filename
        string fileName = Path.GetFileName(path);
        string[] allowedFileNames = ["Deltempo.exe", "deltempo_cli.exe"];
        if (!allowedFileNames.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
        {
            reason = $"Unexpected artifact filename '{fileName}'. Expected Deltempo.exe or deltempo_cli.exe.";
            return false;
        }

        return true;
    }
}
