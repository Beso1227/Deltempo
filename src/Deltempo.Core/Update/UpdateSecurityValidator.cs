using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Validates update download URLs to prevent SSRF, MITM, or untrusted payload staging.
/// Enforces HTTPS and origin from official GitHub release distribution hosts and repository paths.
/// Also validates the final URI after HTTP redirects to prevent redirect bypass.
/// </summary>
public static class UpdateSecurityValidator
{
    /// <summary>
    /// Only the standard HTTPS port is allowed; non-standard ports are a common bypass trick.
    /// </summary>
    private const int AllowedHttpsPort = 443;

    /// <summary>
    /// Percent-encoded path traversal sequences that must be rejected even after decoding.
    /// </summary>
    private static readonly Regex TraversalPattern = new(
        @"(%2e%2e|%2e%2e%2f|%2e%2e%5c|\.\.|/\\\.\\\.|\\\.\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

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

        return ValidateUri(uri, reason: out reason);
    }

    /// <summary>
    /// Validates the final URI after an HTTP redirect. Rejects if the redirect target
    /// falls outside the allowlist, ensuring a redirect cannot bypass host/path checks.
    /// </summary>
    public static bool IsValidRedirectTarget(Uri finalUri, out string reason)
    {
        reason = string.Empty;
        if (finalUri == null)
        {
            reason = "Final URI is null.";
            return false;
        }

        return ValidateUri(finalUri, reason: out reason);
    }

    private static bool ValidateUri(Uri uri, out string reason)
    {
        reason = string.Empty;

        // Protocol must strictly be HTTPS.
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            reason = "Protocol must strictly be HTTPS.";
            return false;
        }

        // Port must be the default HTTPS port (443) or explicitly omitted.
        if (uri.Port != 0 && uri.Port != AllowedHttpsPort)
        {
            reason = $"Non-standard HTTPS port '{uri.Port}'. Only port {AllowedHttpsPort} is allowed.";
            return false;
        }

        // Reject user-info (e.g. https://user:pass@host/) — a common URL-tricking technique.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            reason = "URL contains user-info credentials.";
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

        // Reject encoded or literal path traversal in the path.
        string path = uri.AbsolutePath;
        string decodedPath;
        try
        {
            decodedPath = WebUtility.UrlDecode(path);
        }
        catch
        {
            decodedPath = path;
        }

        if (TraversalPattern.IsMatch(path) || TraversalPattern.IsMatch(decodedPath))
        {
            reason = "URL path contains traversal sequences.";
            return false;
        }

        // Strict path validation: must be under Beso1227/Deltempo/releases/download/
        if (!path.StartsWith("/Beso1227/Deltempo/releases/download/", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Untrusted download path '{path}'. Must originate from Beso1227/Deltempo releases.";
            return false;
        }

        // Validate artifact filename.
        string fileName = Path.GetFileName(path);
        string[] allowedFileNames = ["Deltempo.exe", "WinTempCleaner.exe", "deltempo_cli.exe", "checksums.sha256"];
        if (!allowedFileNames.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
        {
            reason = $"Unexpected artifact filename '{fileName}'. Expected Deltempo.exe, WinTempCleaner.exe, deltempo_cli.exe, or checksums.sha256.";
            return false;
        }

        return true;
    }
}
