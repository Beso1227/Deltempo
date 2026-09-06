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
    public bool IsPatchUpdate { get; set; }
    public string CommitSha { get; set; } = string.Empty;
    public string ShortCommitSha => CommitSha.Length >= 7 ? CommitSha[..7] : CommitSha;
    public DateTime? Timestamp { get; set; }
    public string ExpectedSha256 { get; set; } = string.Empty;
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

    public static async Task<ReleaseInfo?> CheckForUpdatesAsync(UpdateChannel? channel = null, CancellationToken ct = default)
    {
        // Resolve channel from user settings if not explicitly specified
        if (!channel.HasValue)
        {
            var userSetting = SettingsService.Current.UpdateChannel?.Trim().ToLowerInvariant();
            if (userSetting == "stable")
            {
                channel = UpdateChannel.Stable;
            }
            else if (userSetting == "patch")
            {
                channel = UpdateChannel.Patch;
            }
            else
            {
                channel = UpdateChannel.Auto;
            }
        }

        if (channel == UpdateChannel.Stable)
        {
            return await CheckForStableUpdateAsync(ct);
        }

        if (channel == UpdateChannel.Patch)
        {
            var patchOnly = await CheckForPatchUpdateAsync(ct);
            if (patchOnly != null && patchOnly.CheckSucceeded && patchOnly.IsNewer)
            {
                return patchOnly;
            }
            return await CheckForStableUpdateAsync(ct);
        }

        // --- INTELLIGENT SMART AUTO-DETECT MODE (Default) ---
        // Concurrently query both Stable official releases and Continuous Patches
        var stableTask = CheckForStableUpdateAsync(ct);
        var patchTask = CheckForPatchUpdateAsync(ct);

        await Task.WhenAll(stableTask, patchTask);

        var stable = await stableTask;
        var patch = await patchTask;

        // Arbitration 1: A newer official milestone release exists (e.g. v1.4.0 > v1.3.3)
        if (stable != null && stable.CheckSucceeded && stable.IsNewer)
        {
            // If patch is also newer and was published AFTER or AT the stable release,
            // patch contains the stable release plus extra fixes.
            if (patch != null && patch.CheckSucceeded && patch.IsNewer &&
                patch.Timestamp >= (stable.Timestamp ?? DateTime.MinValue))
            {
                return patch;
            }
            return stable;
        }

        // Arbitration 2: No newer stable milestone, check if a continuous patch exists
        if (patch != null && patch.CheckSucceeded && patch.IsNewer)
        {
            return patch;
        }

        // Arbitration 3: Neither is newer -> return status info
        return (stable != null && stable.CheckSucceeded) ? stable : patch;
    }

    public static async Task<ReleaseInfo?> CheckForPatchUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            PatchManifest? manifest = null;
            string releaseName = "";
            string body = "";
            DateTime publishedAt = DateTime.UtcNow;
            string downloadUrl = "";
            long sizeBytes = 0;

            // ─── TIER 1: DIRECT HIGH-AVAILABILITY CDN MANIFEST INGESTION ───
            // Uses cache-busting query and NoCache headers to ensure fresh CDN delivery.
            try
            {
                long cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string manifestCdnUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/patch/patch-manifest.json?cb={cacheBuster}";
                using var cdnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cdnCts.CancelAfter(TimeSpan.FromSeconds(6));

                using var req = new HttpRequestMessage(HttpMethod.Get, manifestCdnUrl);
                req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

                using var cdnResponse = await DownloadHttpClient.SendAsync(req, cdnCts.Token);
                if (cdnResponse.IsSuccessStatusCode)
                {
                    string manifestRaw = await cdnResponse.Content.ReadAsStringAsync(cdnCts.Token);
                    var parsed = ParsePatchManifest(manifestRaw);
                    if (parsed != null)
                    {
                        var validation = PatchMetadataValidator.Validate(parsed);
                        if (validation.IsValid)
                        {
                            manifest = parsed;
                            downloadUrl = manifest.DownloadUrl;
                            sizeBytes = manifest.FileSizeBytes;
                            publishedAt = manifest.Timestamp;
                            body = manifest.CommitMessage;
                        }
                        else
                        {
                            System.Diagnostics.Trace.WriteLine($"[Deltempo] CDN patch manifest failed validation: {validation.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Direct CDN patch manifest check fallback: {ex.Message}");
            }

            // ─── TIER 2: GITHUB REST API FALLBACK ──────────────────────────
            // Queried when the direct CDN asset is not yet available or failed.
            if (manifest == null || string.IsNullOrEmpty(downloadUrl))
            {
                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/tags/patch";
                using var response = await ApiHttpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    if (manifest == null)
                    {
                        return new ReleaseInfo { CheckSucceeded = false, StatusMessage = "Could not reach update server." };
                    }
                }
                else
                {
                    string json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    releaseName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                    body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
                    string publishedAtStr = root.TryGetProperty("published_at", out var pubEl) ? pubEl.GetString() ?? "" : "";
                    DateTime.TryParse(publishedAtStr, out publishedAt);

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

                    manifest ??= ParsePatchManifest(body);
                }
            }

            // Ensure download URL is safely defaulted if missing from manifest
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/patch/Deltempo.exe";
            }

            string remoteCommitSha = manifest?.CommitSha ?? "";
            DateTime remoteTimestamp = manifest?.Timestamp ?? publishedAt;
            string remoteMessage = manifest?.CommitMessage ?? body;

            if (string.IsNullOrEmpty(remoteCommitSha))
            {
                var match = Regex.Match(body, @"Commit:\s*([0-9a-fA-F]{7,40})", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    remoteCommitSha = match.Groups[1].Value;
                }
            }

            string remoteSha256 = manifest?.Sha256 ?? "";

            // Evaluate patch availability via deterministic PatchDiscoveryEngine
            var discovery = PatchDiscoveryEngine.EvaluatePatchAvailability(
                localCommitSha: BuildInfo.CommitSha,
                localExeSha256: BuildInfo.CurrentExecutableSha256,
                localBuildDateUtc: BuildInfo.BuildDateUtc,
                lastInstalledSha: SettingsService.Current.LastInstalledPatchSha,
                lastInstalledHash: SettingsService.Current.LastInstalledPatchHash,
                remoteCommitSha: remoteCommitSha,
                remoteSha256: remoteSha256,
                remoteTimestampUtc: remoteTimestamp);

            string shortSha = remoteCommitSha.Length >= 7 ? remoteCommitSha[..7] : remoteCommitSha;
            string displayTag = string.IsNullOrEmpty(shortSha) ? "Continuous Patch" : $"Patch: {shortSha}";

            return new ReleaseInfo
            {
                CheckSucceeded = true,
                IsPatchUpdate = true,
                IsNewer = discovery.IsNewer,
                TagName = displayTag,
                ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? displayTag : releaseName,
                Body = remoteMessage,
                DownloadUrl = downloadUrl,
                FileSizeBytes = sizeBytes,
                CommitSha = remoteCommitSha,
                Timestamp = remoteTimestamp,
                VersionString = $"{BuildInfo.BaseVersion.ToString(3)}-patch",
                ExpectedSha256 = remoteSha256,
                StatusMessage = discovery.Reason
            };
        }
        catch (Exception ex)
        {
            return new ReleaseInfo { CheckSucceeded = false, StatusMessage = ex.Message };
        }
    }

    public static async Task<ReleaseInfo?> CheckForStableUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var response = await ApiHttpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new ReleaseInfo { CheckSucceeded = false, StatusMessage = "Could not reach stable update server." };
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

            var cleanTag = Regex.Replace(tagName, @"^[^\d]*", "");
            if (!Version.TryParse(cleanTag, out var remoteVer))
            {
                var parts = cleanTag.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min))
                {
                    int build = parts.Length >= 3 && int.TryParse(parts[2], out var b) ? b : 0;
                    remoteVer = new Version(maj, min, build);
                }
                else
                {
                    remoteVer = new Version(1, 0, 0);
                }
            }

            static Version Normalize(Version v) =>
                new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

            bool isNewer = Normalize(remoteVer) > Normalize(CurrentVersion);

            return new ReleaseInfo
            {
                CheckSucceeded = true,
                IsPatchUpdate = false,
                TagName = tagName,
                ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
                Body = body,
                DownloadUrl = downloadUrl,
                FileSizeBytes = sizeBytes,
                IsNewer = isNewer,
                VersionString = cleanTag,
                Timestamp = publishedAt,
                StatusMessage = isNewer ? $"New stable release {tagName} available." : "Running latest stable release."
            };
        }
        catch (Exception ex)
        {
            return new ReleaseInfo { CheckSucceeded = false, StatusMessage = ex.Message };
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PatchManifest? ParsePatchManifest(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var match = Regex.Match(text, @"<!--\s*DELTEMPO_PATCH_MANIFEST\s*(\{.*?\})\s*-->", RegexOptions.Singleline);
            if (match.Success)
            {
                return JsonSerializer.Deserialize<PatchManifest>(match.Groups[1].Value, JsonOptions);
            }

            var codeBlockMatch = Regex.Match(text, @"```json:manifest\s*(\{.*?\})\s*```", RegexOptions.Singleline);
            if (codeBlockMatch.Success)
            {
                return JsonSerializer.Deserialize<PatchManifest>(codeBlockMatch.Groups[1].Value, JsonOptions);
            }

            if (text.TrimStart().StartsWith("{") && text.TrimEnd().EndsWith("}"))
            {
                return JsonSerializer.Deserialize<PatchManifest>(text, JsonOptions);
            }
        }
        catch
        {
        }

        return null;
    }

    public static async Task DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double> progress, string? expectedSha256 = null, string? commitSha = null, CancellationToken ct = default)
    {
        // 0. Mutual Exclusion: Acquire cross-process update lock
        using var updateLock = PatchInstallationLock.TryAcquire(TimeSpan.FromSeconds(5));
        if (!updateLock.HasLock)
        {
            throw new InvalidOperationException("Another Deltempo update operation is currently in progress. Please wait for it to finish.");
        }

        // 1. Strict Host & Protocol Security Assertion (Anti-SSRF / Anti-Tamper)
        if (!PatchMetadataValidator.IsValidDownloadUrl(downloadUrl, out string urlReason))
        {
            throw new SecurityException($"Security violation: {urlReason}");
        }

        string tempFile = Path.Combine(Path.GetTempPath(), $"Deltempo_Update_{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await DownloadHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

            var buffer = new byte[65536];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, ct);
                totalRead += read;
                if (totalBytes > 0)
                {
                    progress.Report((double)totalRead / totalBytes * 100.0);
                }
            }

            await fileStream.FlushAsync(ct);
            fileStream.Close();

            // 2. Cryptographic Integrity & PE Validation
            var fi = new FileInfo(tempFile);
            if (!PatchIntegrityVerifier.VerifyStagedArtifact(tempFile, expectedSha256 ?? "", fi.Length, out string integrityError))
            {
                throw new SecurityException($"Update integrity verification failed: {integrityError}");
            }

            // Prepare Atomic Hot-Swap Handover via robust cmd.exe swap script
            string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            {
                currentExePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
            }

            int currentPid = Environment.ProcessId;
            string cmdScript = Path.Combine(Path.GetTempPath(), $"deltempo_swap_{Guid.NewGuid():N}.cmd");
            string logFile = Path.Combine(Path.GetTempPath(), "deltempo_update.log");

            string scriptContent = GenerateSwapScript(currentPid, currentExePath, tempFile, logFile);
            File.WriteAllText(cmdScript, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{cmdScript}\"\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            // Persist installed patch metadata to settings before handover
            try
            {
                if (!string.IsNullOrWhiteSpace(commitSha))
                {
                    SettingsService.Current.LastInstalledPatchSha = commitSha;
                }
                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    SettingsService.Current.LastInstalledPatchHash = expectedSha256;
                }
                SettingsService.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Failed to persist patch update metadata: {ex.Message}");
            }

            Process.Start(psi);

            // Clean shutdown & immediate exit to release all locks instantly
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
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
            throw;
        }
    }

    public static string GenerateSwapScript(int targetPid, string targetExePath, string sourceExePath, string logFilePath)
    {
        string backupExePath = $"{targetExePath}.old";
        return $@"@echo off
setlocal enabledelayedexpansion

set ""TARGET_PID={targetPid}""
set ""TARGET_EXE={targetExePath}""
set ""SOURCE_EXE={sourceExePath}""
set ""BACKUP_EXE={backupExePath}""
set ""LOG_FILE={logFilePath}""

echo [%DATE% %TIME%] Deltempo professional updater handover initiated > ""%LOG_FILE%""
echo Target PID: %TARGET_PID% >> ""%LOG_FILE%""
echo Target EXE: %TARGET_EXE% >> ""%LOG_FILE%""
echo Source EXE: %SOURCE_EXE% >> ""%LOG_FILE%""

:: Step 1: Wait up to 30s for the running Deltempo process to exit
set /a WAIT_COUNT=0
:wait_process
tasklist /FI ""PID eq %TARGET_PID%"" 2>nul | findstr /i ""%TARGET_PID%"" >nul
if not errorlevel 1 (
    set /a WAIT_COUNT+=1
    if !WAIT_COUNT! geq 30 (
        echo [%DATE% %TIME%] Process exit wait timed out, proceeding to swap >> ""%LOG_FILE%""
        goto perform_swap
    )
    timeout /t 1 /nobreak >nul
    goto wait_process
)

:perform_swap
:: Step 2: Extra brief pause for antivirus & OS file handles to release
timeout /t 1 /nobreak >nul

:: Step 3: Remove any previous backup file if it exists
if exist ""%BACKUP_EXE%"" del /f /q ""%BACKUP_EXE%"" >nul 2>&1

:: Step 4: Rename current target to .old (succeeds even if locked by read handles)
set /a RENAME_RETRY=0
:rename_loop
if not exist ""%TARGET_EXE%"" goto deploy_new
move /y ""%TARGET_EXE%"" ""%BACKUP_EXE%"" >nul 2>&1
if not errorlevel 1 (
    echo [%DATE% %TIME%] Successfully moved target to backup on attempt !RENAME_RETRY! >> ""%LOG_FILE%""
    goto deploy_new
)
set /a RENAME_RETRY+=1
if !RENAME_RETRY! geq 20 (
    echo [%DATE% %TIME%] Direct rename failed, attempting direct copy >> ""%LOG_FILE%""
    goto direct_copy
)
timeout /t 1 /nobreak >nul
goto rename_loop

:deploy_new
:: Step 5: Move new binary into target location
set /a MOVE_RETRY=0
:move_loop
move /y ""%SOURCE_EXE%"" ""%TARGET_EXE%"" >nul 2>&1
if not errorlevel 1 (
    echo [%DATE% %TIME%] Successfully installed new binary on move attempt !MOVE_RETRY! >> ""%LOG_FILE%""
    goto swap_success
)
set /a MOVE_RETRY+=1
if !MOVE_RETRY! geq 20 (
    echo [%DATE% %TIME%] Move failed, attempting copy fallback >> ""%LOG_FILE%""
    goto direct_copy
)
timeout /t 1 /nobreak >nul
goto move_loop

:direct_copy
copy /y ""%SOURCE_EXE%"" ""%TARGET_EXE%"" >nul 2>&1
if not errorlevel 1 (
    echo [%DATE% %TIME%] Direct copy succeeded >> ""%LOG_FILE%""
    goto swap_success
)

:: Step 6: Rollback on total failure - Restore original binary
echo [%DATE% %TIME%] Update failed! Initiating automatic rollback >> ""%LOG_FILE%""
if exist ""%BACKUP_EXE%"" (
    move /y ""%BACKUP_EXE%"" ""%TARGET_EXE%"" >nul 2>&1
    echo [%DATE% %TIME%] Rollback restored original executable >> ""%LOG_FILE%""
)
if exist ""%TARGET_EXE%"" (
    start """" ""%TARGET_EXE%""
)
goto cleanup_self

:swap_success
echo [%DATE% %TIME%] Update succeeded! Cleaning temporary files >> ""%LOG_FILE%""
if exist ""%BACKUP_EXE%"" del /f /q ""%BACKUP_EXE%"" >nul 2>&1
if exist ""%SOURCE_EXE%"" del /f /q ""%SOURCE_EXE%"" >nul 2>&1

:: Launch the updated binary
start """" ""%TARGET_EXE%""

:cleanup_self
echo [%DATE% %TIME%] Updater finished. Self-deleting script >> ""%LOG_FILE%""
start /b """" cmd /c ""timeout /t 2 /nobreak >nul & del /f /q """"%~f0"""" >nul 2>&1""
exit /b 0
";
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
