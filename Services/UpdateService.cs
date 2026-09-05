using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public enum UpdateChannel
{
    Patch,
    Stable
}

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
}

public class PatchManifest
{
    public string Channel { get; set; } = "patch";
    public string BaseVersion { get; set; } = "1.3.3";
    public string CommitSha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string CommitMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
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
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/tags/patch";
            using var response = await ApiHttpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new ReleaseInfo { CheckSucceeded = false };
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

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

            if (string.IsNullOrEmpty(downloadUrl))
            {
                return new ReleaseInfo { CheckSucceeded = false };
            }

            var manifest = ParsePatchManifest(body);
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

            string localSha = BuildInfo.CommitSha;
            bool isNewer = false;

            if (!string.IsNullOrEmpty(remoteCommitSha) && !localSha.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                bool isSameCommit = remoteCommitSha.StartsWith(localSha, StringComparison.OrdinalIgnoreCase) ||
                                    localSha.StartsWith(remoteCommitSha, StringComparison.OrdinalIgnoreCase);

                if (!isSameCommit)
                {
                    isNewer = remoteTimestamp >= BuildInfo.BuildDateUtc.AddMinutes(-10);
                }
            }
            else
            {
                isNewer = remoteTimestamp > BuildInfo.BuildDateUtc.AddMinutes(2);
            }

            string shortSha = remoteCommitSha.Length >= 7 ? remoteCommitSha[..7] : remoteCommitSha;
            string displayTag = string.IsNullOrEmpty(shortSha) ? "Continuous Patch" : $"Patch: {shortSha}";

            return new ReleaseInfo
            {
                CheckSucceeded = true,
                IsPatchUpdate = true,
                IsNewer = isNewer,
                TagName = displayTag,
                ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? displayTag : releaseName,
                Body = remoteMessage,
                DownloadUrl = downloadUrl,
                FileSizeBytes = sizeBytes,
                CommitSha = remoteCommitSha,
                Timestamp = remoteTimestamp,
                VersionString = $"{BuildInfo.BaseVersion.ToString(3)}-patch"
            };
        }
        catch
        {
            return new ReleaseInfo { CheckSucceeded = false };
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
                return new ReleaseInfo { CheckSucceeded = false };
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
                Timestamp = publishedAt
            };
        }
        catch
        {
            return new ReleaseInfo { CheckSucceeded = false };
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

    public static async Task DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken ct = default)
    {
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

            // Prepare Atomic Hot-Swap Handover via robust cmd.exe swap script
            // (Immune to PowerShell execution policies, cold-start delays, and syntax quirks)
            string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            {
                currentExePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
            }

            int currentPid = Environment.ProcessId;
            string cmdScript = Path.Combine(Path.GetTempPath(), $"deltempo_swap_{Guid.NewGuid():N}.cmd");
            string logFile = Path.Combine(Path.GetTempPath(), "deltempo_update.log");

            string scriptContent = $@"@echo off
setlocal enabledelayedexpansion

set ""TARGET_PID={currentPid}""
set ""TARGET_EXE={currentExePath}""
set ""SOURCE_EXE={tempFile}""
set ""LOG_FILE={logFile}""

echo [%DATE% %TIME%] Deltempo updater handover started > ""%LOG_FILE%""
echo Target PID: %TARGET_PID% >> ""%LOG_FILE%""
echo Target EXE: %TARGET_EXE% >> ""%LOG_FILE%""
echo Source EXE: %SOURCE_EXE% >> ""%LOG_FILE%""

:: 1. Wait for current Deltempo process to terminate (up to 30s)
set /a WAIT_COUNT=0
:wait_process
tasklist /FI ""PID eq %TARGET_PID%"" 2>nul | findstr /i ""%TARGET_PID%"" >nul
if not errorlevel 1 (
    set /a WAIT_COUNT+=1
    if !WAIT_COUNT! geq 30 (
        echo [%DATE% %TIME%] Timed out waiting for process to exit >> ""%LOG_FILE%""
        goto perform_copy
    )
    timeout /t 1 /nobreak >nul
    goto wait_process
)

:perform_copy
:: Extra pause for OS handle and antivirus to release file lock
timeout /t 1 /nobreak >nul

:: 2. Overwrite target with retry loop (up to 25 attempts, 1 second intervals)
set /a RETRY=0
:copy_loop
copy /y ""%SOURCE_EXE%"" ""%TARGET_EXE%"" >nul 2>&1
if not errorlevel 1 (
    echo [%DATE% %TIME%] Copy succeeded on attempt !RETRY! >> ""%LOG_FILE%""
    goto copy_success
)
set /a RETRY+=1
if !RETRY! geq 25 (
    echo [%DATE% %TIME%] Copy failed after !RETRY! attempts >> ""%LOG_FILE%""
    goto finish
)
timeout /t 1 /nobreak >nul
goto copy_loop

:copy_success
del /f /q ""%SOURCE_EXE%"" >nul 2>&1
echo [%DATE% %TIME%] Launching updated executable >> ""%LOG_FILE%""
start """" ""%TARGET_EXE%""

:finish
echo [%DATE% %TIME%] Updater finished, self-deleting >> ""%LOG_FILE%""
start /b """" cmd /c ""timeout /t 2 /nobreak >nul & del /f /q """"%~f0"""" >nul 2>&1""
exit /b 0
";

            File.WriteAllText(cmdScript, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{cmdScript}\"\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

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
}
