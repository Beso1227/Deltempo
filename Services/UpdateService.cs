using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

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

    public static async Task<ReleaseInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
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

            var cleanTag = Regex.Replace(tagName, @"^[^\d]*", ""); // remove 'v' or letters
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
                TagName = tagName,
                ReleaseName = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
                Body = body,
                DownloadUrl = downloadUrl,
                FileSizeBytes = sizeBytes,
                IsNewer = isNewer,
                VersionString = cleanTag
            };
        }
        catch (OperationCanceledException)
        {
            return new ReleaseInfo { CheckSucceeded = false };
        }
        catch
        {
            return new ReleaseInfo { CheckSucceeded = false };
        }
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
