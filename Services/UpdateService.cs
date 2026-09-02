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
}

public static class UpdateService
{
    private const string RepoOwner = "Beso1227";
    private const string RepoName = "Deltempo";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static UpdateService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Updater", "1.0"));
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static async Task<ReleaseInfo?> CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync();
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
                // Fallback to major.minor
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

            bool isNewer = remoteVer > CurrentVersion;

            return new ReleaseInfo
            {
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
            return null;
        }
    }

    public static async Task DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken ct = default)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"Deltempo_Update_{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[16384];
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

            fileStream.Close();

            // Prepare Atomic Hot-Swap Handover
            string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            {
                currentExePath = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
            }

            int currentPid = Process.GetCurrentProcess().Id;
            string swapScript = Path.Combine(Path.GetTempPath(), $"deltempo_swap_{Guid.NewGuid():N}.bat");

            // Batch script waits for current PID to terminate, overwrites EXE atomically, relaunches and self-deletes
            string scriptContent = $@"@echo off
setlocal
set PID={currentPid}
set TARGET=""{currentExePath}""
set SOURCE=""{tempFile}""

:wait_loop
tasklist /fi ""pid eq %PID%"" 2>NUL | find ""%PID%"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >NUL
    goto wait_loop
)

copy /y %SOURCE% %TARGET% >NUL 2>&1
del /f /q %SOURCE% >NUL 2>&1

start """" %TARGET%

(goto) 2>nul & del ""%~f0""
";

            File.WriteAllText(swapScript, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{swapScript}\"",
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
            catch { }
            throw;
        }
    }
}
