using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using WinTempCleaner.Core.Update;

namespace WinTempCleaner.Updater;

/// <summary>
/// Streaming download engine with bounded retries, exponential backoff,
/// on-the-fly SHA-256 calculation, and staged transaction directory isolation.
/// </summary>
public static class UpdateDownloader
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    static UpdateDownloader()
    {
        Client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Updater", "1.0"));
    }

    /// <summary>
    /// Downloads an artifact to the transaction staging directory with retry, hash verification,
    /// and streaming progress reporting.
    /// </summary>
    public static async Task<(string filePath, string sha256, long fileSize)> DownloadAsync(
        string downloadUrl,
        string stagingDir,
        string expectedFileName,
        string expectedSha256 = "",
        long expectedSize = 0,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
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
                return await DownloadSingleAttemptAsync(downloadUrl, stagedPath, expectedSha256, expectedSize, progress, ct);
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
        return await DownloadSingleAttemptAsync(downloadUrl, stagedPath, expectedSha256, expectedSize, progress, ct);
    }

    private static async Task<(string filePath, string sha256, long fileSize)> DownloadSingleAttemptAsync(
        string downloadUrl,
        string stagedPath,
        string expectedSha256,
        long expectedSize,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        using var response = await Client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
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
            if (totalBytes > 0)
            {
                progress?.Report((double)totalRead / totalBytes * 100.0);
            }
        }

        sha.TransformFinalBlock([], 0, 0);
        string actualSha256 = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

        await fileStream.FlushAsync(ct);
        fileStream.Close();

        // Verify size if expected
        if (expectedSize > 0 && Math.Abs(totalRead - expectedSize) > 1024)
        {
            File.Delete(stagedPath);
            throw new InvalidOperationException($"Downloaded file size mismatch: expected {expectedSize}, got {totalRead}.");
        }

        // Verify hash if expected
        if (!string.IsNullOrWhiteSpace(expectedSha256) &&
            !actualSha256.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(stagedPath);
            throw new InvalidOperationException($"SHA-256 mismatch after download. Expected: {expectedSha256}, Got: {actualSha256}");
        }

        return (stagedPath, actualSha256, totalRead);
    }
}
