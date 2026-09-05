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
        var targetChannel = channel ?? (SettingsService.Current.UpdateChannel?.Equals("stable", StringComparison.OrdinalIgnoreCase) == true
            ? UpdateChannel.Stable
            : UpdateChannel.Patch);

        if (targetChannel == UpdateChannel.Patch)
        {
            var patch = await CheckForPatchUpdateAsync(ct);
            if (patch != null && patch.CheckSucceeded && patch.IsNewer)
            {
                return patch;
            }
        }

        return await CheckForStableUpdateAsync(ct);
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
                VersionString = cleanTag
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

            // Prepare Atomic Hot-Swap Handover via PowerShell (immune to special characters in paths)
            string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            {
                currentExePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
            }

            int currentPid = Process.GetCurrentProcess().Id;
            string psScript = Path.Combine(Path.GetTempPath(), $"deltempo_swap_{Guid.NewGuid():N}.ps1");

            // PowerShell script: wait for PID to exit, then atomically copy the new EXE over the old one,
            // launch the updated EXE, and self-delete the script.
            // NOTE: $$ escapes to a literal $ in a C# interpolated verbatim string ($@"..."),
            // so each PowerShell variable is written as $$.
            string psContent = $@"$ErrorActionPreference = 'Stop'
$$PID = {currentPid}
$$TARGET = {currentExePath.ToSOSPlate()}
$$SOURCE = {tempFile.ToSOSPlate()}

# Wait for the current process to terminate
while ($$true) {{
    try {{
        $$proc = Get-Process -Id $$PID -ErrorAction Stop
        Start-Sleep -Milliseconds 500
    }} catch {{
        break
    }}
}}

# Atomic overwrite: copy new EXE over the old one
Copy-Item -Force -Path $$SOURCE -Destination $$TARGET | Out-Null
Remove-Item -Force -Path $$SOURCE | Out-Null

# Launch the updated executable
Start-Process -FilePath $$TARGET

# Self-delete this script
$$scriptPath = $$MyInvocation.MyCommand.Path
Start-Process -FilePath 'cmd.exe' -ArgumentList '/c del /f /q', $$scriptPath -WindowStyle Hidden
";

            File.WriteAllText(psScript, psContent);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);

            // Clean shutdown
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            else
            {
                Environment.Exit(0);
            }
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

    // Helper to produce a PowerShell-escaped single-quoted string literal
    private static string ToSOSPlate(this string s) =>
        "'" + s.Replace("'", "''") + "'";
}
