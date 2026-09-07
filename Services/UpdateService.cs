using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using WinTempCleaner.Core.Update;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class ReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsNewer { get; set; }
    public string VersionString { get; set; } = "1.0.0";
    public bool CheckSucceeded { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

public static class UpdateService
{
    private const string RepoOwner = "Beso1227";
    private const string RepoName = "Deltempo";

    private static readonly HttpClient ApiHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly HttpClient DownloadHttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    static UpdateService()
    {
        ApiHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Updater", "1.0"));
        ApiHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        DownloadHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Downloader", "1.0"));
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static Version NormalizeVersion(Version v) =>
        new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

    public static Version ParseReleaseVersion(string tagName)
    {
        var cleanTag = Regex.Replace(tagName, @"^[^\d]*", "");
        if (Version.TryParse(cleanTag, out var parsedVer))
        {
            return NormalizeVersion(parsedVer);
        }

        var parts = cleanTag.Split('.');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min))
        {
            int build = parts.Length >= 3 && int.TryParse(parts[2], out var b) ? b : 0;
            return new Version(maj, min, build);
        }

        return new Version(1, 0, 0);
    }

    /// <summary>
    /// Checks for a new official release on GitHub Releases.
    /// Supports both Stable (releases/latest) and Pre-Release (releases) channels.
    /// </summary>
    public static async Task<ReleaseInfo?> CheckForUpdatesAsync(bool includePrereleases = false, CancellationToken ct = default)
    {
        try
        {
            if (includePrereleases)
            {
                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=10";
                using var response = await ApiHttpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return new ReleaseInfo { CheckSucceeded = false, StatusMessage = "Could not reach GitHub Releases server." };
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    return new ReleaseInfo { CheckSucceeded = true, IsNewer = false, StatusMessage = "No releases found on repository." };
                }

                ReleaseInfo? bestRelease = null;
                Version currentVer = NormalizeVersion(CurrentVersion);
                Version bestVersion = currentVer;

                foreach (var rel in doc.RootElement.EnumerateArray())
                {
                    if (rel.TryGetProperty("draft", out var draftEl) && draftEl.GetBoolean())
                        continue;

                    string tag = rel.TryGetProperty("tag_name", out var tEl) ? tEl.GetString() ?? "" : "";
                    var ver = ParseReleaseVersion(tag);
                    if (ver > bestVersion)
                    {
                        string rName = rel.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
                        string body = rel.TryGetProperty("body", out var bEl) ? bEl.GetString() ?? "" : "";
                        string pubStr = rel.TryGetProperty("published_at", out var pEl) ? pEl.GetString() ?? "" : "";
                        DateTime.TryParse(pubStr, out var pubDate);

                        string dlUrl = "";
                        long sBytes = 0;
                        if (rel.TryGetProperty("assets", out var aEl) && aEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var asset in aEl.EnumerateArray())
                            {
                                string aName = asset.TryGetProperty("name", out var anEl) ? anEl.GetString() ?? "" : "";
                                if (aName.Equals("Deltempo.exe", StringComparison.OrdinalIgnoreCase) ||
                                    aName.Equals("WinTempCleaner.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    dlUrl = asset.TryGetProperty("browser_download_url", out var dlEl) ? dlEl.GetString() ?? "" : "";
                                    sBytes = asset.TryGetProperty("size", out var sSizeEl) ? sSizeEl.GetInt64() : 0;
                                    break;
                                }
                            }
                        }

                        bestVersion = ver;
                        bestRelease = new ReleaseInfo
                        {
                            CheckSucceeded = true,
                            TagName = tag,
                            ReleaseName = string.IsNullOrWhiteSpace(rName) ? tag : rName,
                            Body = body,
                            DownloadUrl = dlUrl,
                            FileSizeBytes = sBytes,
                            IsNewer = true,
                            VersionString = Regex.Replace(tag, @"^[^\d]*", ""),
                            PublishedAt = pubDate,
                            StatusMessage = $"New release {tag} available."
                        };
                    }
                }

                if (bestRelease != null)
                {
                    return bestRelease;
                }

                return new ReleaseInfo
                {
                    CheckSucceeded = true,
                    IsNewer = false,
                    StatusMessage = "Running the latest release."
                };
            }
            else
            {
                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                using var response = await ApiHttpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return new ReleaseInfo { CheckSucceeded = false, StatusMessage = "Could not reach GitHub Releases server." };
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
                string releaseName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";

                string publishedAtStr = root.TryGetProperty("published_at", out var pubEl) ? pubEl.GetString() ?? "" : "";
                DateTime.TryParse(publishedAtStr, out var publishedAt);

                string downloadUrl = "";
                long sizeBytes = 0;

                if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsEl.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var anEl) ? anEl.GetString() ?? "" : "";
                        if (name.Equals("Deltempo.exe", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("WinTempCleaner.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var dlEl) ? dlEl.GetString() ?? "" : "";
                            sizeBytes = asset.TryGetProperty("size", out var sEl) ? sEl.GetInt64() : 0;
                            break;
                        }
                    }
                }

                var remoteVer = ParseReleaseVersion(tagName);
                bool isNewer = remoteVer > NormalizeVersion(CurrentVersion);
                var cleanTag = Regex.Replace(tagName, @"^[^\d]*", "");

                return new ReleaseInfo
                {
                    CheckSucceeded = true,
                    TagName = tagName,
                    ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
                    Body = body,
                    DownloadUrl = downloadUrl,
                    FileSizeBytes = sizeBytes,
                    IsNewer = isNewer,
                    VersionString = cleanTag,
                    PublishedAt = publishedAt,
                    StatusMessage = isNewer ? $"New release {tagName} available." : "Running the latest release."
                };
            }
        }
        catch (Exception ex)
        {
            return new ReleaseInfo { CheckSucceeded = false, StatusMessage = ex.Message };
        }
    }

    /// <summary>
    /// Downloads and applies the new official release using atomic staging and hot-swap replacement.
    /// </summary>
    public static async Task DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double> progress, string? expectedSha256 = null, CancellationToken ct = default)
    {
        // 0. Mutual Exclusion: Acquire cross-process update lock
        using var updateLock = PatchInstallationLock.TryAcquire(TimeSpan.FromSeconds(5));
        if (!updateLock.HasLock)
        {
            throw new InvalidOperationException("Another Deltempo update operation is currently in progress. Please wait for it to finish.");
        }

        // 1. Strict Host & Protocol Security Assertion (Anti-SSRF / Anti-Tamper)
        if (!UpdateSecurityValidator.IsValidDownloadUrl(downloadUrl, out string urlReason))
        {
            throw new SecurityException($"Security violation: {urlReason}");
        }

        // 2. Create transaction journal
        string txId = Guid.NewGuid().ToString("N");
        string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
        {
            currentExePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
        }

        string updatesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Deltempo", "Updates", txId);
        string stagedDir = Path.Combine(updatesDir, "staged");

        var journal = new TransactionJournal
        {
            TransactionId = txId,
            Channel = "release",
            Version = CurrentVersion.ToString(3),
            TargetPath = currentExePath,
            BackupPath = Path.Combine(updatesDir, "Deltempo.previous.exe"),
            ExpectedSha256 = expectedSha256 ?? "",
            ExpectedSizeBytes = 0,
            CallerPid = Environment.ProcessId
        };
        journal.TransitionTo(TransactionState.Discovered);

        try
        {
            // 3. Download to transaction staging directory
            string artifactName = Path.GetFileName(currentExePath);
            var (stagedPath, actualSha, fileSize) = await UpdateDownloader.DownloadAsync(
                downloadUrl, stagedDir, artifactName,
                expectedSha256 ?? "", 0, progress, ct);

            journal.StagedPath = stagedPath;
            journal.ExpectedSha256 = actualSha;
            journal.ExpectedSizeBytes = fileSize;
            journal.TransitionTo(TransactionState.Downloaded);

            // 4. Verify staged binary (SHA-256, Size, PE header, version)
            var fi = new FileInfo(stagedPath);
            if (!PatchIntegrityVerifier.VerifyStagedArtifact(stagedPath, actualSha, fi.Length, out string integrityError))
            {
                throw new SecurityException($"Update integrity verification failed: {integrityError}");
            }

            journal.TransitionTo(TransactionState.DownloadVerified);
            journal.TransitionTo(TransactionState.Staged);
            journal.TransitionTo(TransactionState.StageVerified);

            // 5. Persist journal and launch embedded updater mode of the same Deltempo.exe
            journal.Save();

            string updaterPath = currentExePath;
            if (!File.Exists(updaterPath))
            {
                throw new FileNotFoundException("Deltempo.exe not found. Cannot proceed with update.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"--update {txId}",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            // 6. Clean shutdown & exit to release file locks
            Process.Start(psi);

            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            Environment.Exit(0);
        }
        catch
        {
            journal.TransitionTo(TransactionState.Failed);
            try
            {
                if (Directory.Exists(stagedDir))
                    Directory.Delete(stagedDir, true);
            }
            catch { }
            throw;
        }
    }

    public static void CleanupPendingUpdateArtifacts()
    {
        Task.Run(() =>
        {
            try
            {
                // 1. Startup Recovery: If primary executable is missing or truncated and .old exists, restore it!
                string currentExePath = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
                {
                    currentExePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
                }

                string backupPath = $"{currentExePath}.old";
                if (File.Exists(backupPath))
                {
                    if (!File.Exists(currentExePath) || new FileInfo(currentExePath).Length == 0)
                    {
                        try
                        {
                            File.Move(backupPath, currentExePath, overwrite: true);
                            System.Diagnostics.Trace.WriteLine("[Deltempo] Recovered main binary from .old backup on startup.");
                        }
                        catch { }
                    }
                    else
                    {
                        // Current binary is healthy; delete the superseded .old backup
                        try { File.Delete(backupPath); } catch { }
                    }
                }

                // 2. Clean up old updater scripts and downloads in %TEMP% older than 15 minutes
                string tempDir = Path.GetTempPath();
                var dirInfo = new DirectoryInfo(tempDir);
                var threshold = DateTime.UtcNow.AddMinutes(-15);

                foreach (var file in dirInfo.EnumerateFiles("deltempo_swap_*.cmd"))
                {
                    try
                    {
                        if (file.CreationTimeUtc < threshold)
                            file.Delete();
                    }
                    catch { }
                }

                foreach (var file in dirInfo.EnumerateFiles("Deltempo_Update_*.exe"))
                {
                    try
                    {
                        if (file.CreationTimeUtc < threshold)
                            file.Delete();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] CleanupPendingUpdateArtifacts suppressed: {ex.Message}");
            }
        });
    }
}
