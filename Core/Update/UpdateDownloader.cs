using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Cryptography;
using WinTempCleaner.Core.Update;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Streaming download engine with bounded retries, exponential backoff,
/// on-the-fly SHA-256 calculation, maximum-size limits, redirect target validation,
/// and staged transaction directory isolation.
/// </summary>
public static class UpdateDownloader
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    /// <summary>
    /// Hard cap on download size: 200 MB. Release binaries for Deltempo are far smaller;
    /// anything larger is rejected to prevent disk exhaustion / decompression-bomb style abuse.
    /// </summary>
    private const long MaxDownloadSizeBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Maximum number of HTTP redirects to follow. Prevents redirect-loop abuse.
    /// </summary>
    private const int MaxRedirects = 10;

    private static readonly HttpClient Client = new(
        new HttpClientHandler { AllowAutoRedirect = false },
        disposeHandler: false)
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    static UpdateDownloader()
    {
        Client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Updater", "1.0"));
    }

    /// <summary>
    /// Downloads an artifact to the transaction staging directory with retry, hash verification,
    /// maximum-size enforcement, redirect target validation, and streaming progress reporting.
    /// </summary>
    /// <param name="downloadUrl">Initial download URL (must already pass <see cref="UpdateSecurityValidator.IsValidDownloadUrl"/>).</param>
    /// <param name="stagingDir">Directory to stage the downloaded file in.</param>
    /// <param name="expectedFileName">Expected artifact filename.</param>
    /// <param name="expectedSha256">Expected SHA-256 hash (from authenticated manifest). Empty disables hash check.</param>
    /// <param name="expectedSize">Expected file size in bytes (from authenticated manifest). 0 disables size check.</param>
    /// <param name="progress">Progress reporter (0-100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="maxDownloadSizeBytes">Maximum allowed download size. Defaults to <see cref="MaxDownloadSizeBytes"/>.</param>
    public static async Task<(string filePath, string sha256, long fileSize)> DownloadAsync(
        string downloadUrl,
        string stagingDir,
        string expectedFileName,
        string expectedSha256 = "",
        long expectedSize = 0,
        IProgress<double>? progress = null,
        CancellationToken ct = default,
        long maxDownloadSizeBytes = MaxDownloadSizeBytes)
    {
        Directory.CreateDirectory(stagingDir);
        string stagedPath = Path.Combine(stagingDir, expectedFileName);

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)], ct);
            }

            try
            {
                return await DownloadSingleAttemptAsync(
                    downloadUrl, stagedPath, expectedSha256, expectedSize, progress, ct, maxDownloadSizeBytes);
            }
            catch (HttpRequestException) when (attempt < MaxRetries)
            {
                // Transient HTTP failure, retry
            }
            catch (TaskCanceledException) when (attempt < MaxRetries && !ct.IsCancellationRequested)
            {
                // Timeout, retry
            }
        }

        // Final attempt - let exception propagate
        return await DownloadSingleAttemptAsync(
            downloadUrl, stagedPath, expectedSha256, expectedSize, progress, ct, maxDownloadSizeBytes);
    }

    private static async Task<(string filePath, string sha256, long fileSize)> DownloadSingleAttemptAsync(
        string downloadUrl,
        string stagedPath,
        string expectedSha256,
        long expectedSize,
        IProgress<double>? progress,
        CancellationToken ct,
        long maxDownloadSizeBytes)
    {
        // Manually follow redirects, validating each hop against the allowlist.
        // Auto-redirect is disabled on the client so we can inspect every hop.
        var currentUri = new Uri(downloadUrl, UriKind.Absolute);
        int redirectCount = 0;

        HttpResponseMessage response;
        while (true)
        {
            using var responseMessage = await Client.GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, ct);

            int statusCode = (int)responseMessage.StatusCode;
            if (statusCode >= 300 && statusCode < 400)
            {
                // Redirect — validate the target before following.
                if (redirectCount >= MaxRedirects)
                {
                    throw new SecurityException($"Too many redirects (max {MaxRedirects}).");
                }

                var location = responseMessage.Headers.Location;
                if (location == null)
                {
                    throw new SecurityException("Redirect response missing Location header.");
                }

                var redirectUri = new Uri(currentUri, location);

                if (!UpdateSecurityValidator.IsValidRedirectTarget(redirectUri, out string redirectReason))
                {
                    throw new SecurityException($"Redirect target rejected: {redirectReason}");
                }

                currentUri = redirectUri;
                redirectCount++;
                continue;
            }

            response = responseMessage;
            break;
        }

        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        long totalBytes = contentLength ?? -1L;

        // Reject oversized downloads before reading the body (Content-Length known).
        if (totalBytes > 0 && totalBytes > maxDownloadSizeBytes)
        {
            throw new InvalidOperationException(
                $"Download size limit exceeded: Content-Length {totalBytes} exceeds maximum {maxDownloadSizeBytes} bytes.");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

        var buffer = new byte[65536];
        long totalRead = 0;
        int read;

        using var sha = SHA256.Create();

        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, ct);
            sha.TransformBlock(buffer, 0, read, null, 0);
            totalRead += read;

            // Enforce max size even when Content-Length is absent or misleading (streaming abort).
            if (totalRead > maxDownloadSizeBytes)
            {
                fileStream.Close();
                File.Delete(stagedPath);
                throw new InvalidOperationException(
                    $"Download size limit exceeded: streamed {totalRead} bytes, maximum {maxDownloadSizeBytes} bytes.");
            }

            if (totalBytes > 0)
            {
                progress?.Report((double)totalRead / totalBytes * 100.0);
            }
        }

        sha.TransformFinalBlock([], 0, 0);
        string actualSha256 = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

        await fileStream.FlushAsync(ct);
        fileStream.Close();

        // Verify size if expected.
        if (expectedSize > 0 && Math.Abs(totalRead - expectedSize) > 1024)
        {
            File.Delete(stagedPath);
            throw new InvalidOperationException($"Downloaded file size mismatch: expected {expectedSize}, got {totalRead}.");
        }

        // Verify hash if expected.
        if (!string.IsNullOrWhiteSpace(expectedSha256) &&
            !actualSha256.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(stagedPath);
            throw new InvalidOperationException($"SHA-256 mismatch after download. Expected: {expectedSha256}, Got: {actualSha256}");
        }

        return (stagedPath, actualSha256, totalRead);
    }
}
