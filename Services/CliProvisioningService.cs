using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using WinTempCleaner.Core.Update;

namespace WinTempCleaner.Services;

/// <summary>
/// Owns the on-disk lifecycle of the native console-subsystem (CUI) binary that backs the
/// <c>deltempo</c> terminal command.
/// </summary>
/// <remarks>
/// The GUI executable is a WinExe whose manifest demands <c>requireAdministrator</c>. Windows
/// brokers such a launch through the AppInfo elevation service, which detaches the new process
/// from the invoking terminal's console and forces a UAC prompt on every single command, so a
/// GUI binary can never act as a usable in-terminal CLI. Only a CUI binary launched
/// <c>asInvoker</c> inherits the caller's console, streams output synchronously, and returns a
/// meaningful exit code. This service guarantees such a binary exists and is what
/// <c>deltempo</c> resolves to.
/// </remarks>
public static class CliProvisioningService
{
    /// <summary>Canonical file name of the published console CLI companion.</summary>
    public const string CliFileName = "deltempo_cli.exe";

    /// <summary>Start of the PowerShell profile block managed by Deltempo.</summary>
    public const string MarkerBegin = "# Deltempo Synchronous CLI";

    /// <summary>End of the PowerShell profile block managed by Deltempo.</summary>
    public const string MarkerEnd = "# End Deltempo Synchronous CLI";

    // A real .NET apphost is ~150 KB; the floor only exists to reject empty files and stubs
    // (a hand-written shim or a truncated download) before they become the command target.
    private const long MinPlausibleBytes = 64 * 1024;
    private const long SingleFileThresholdBytes = 20L * 1024 * 1024;

    private static readonly HttpClient ApiClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    static CliProvisioningService()
    {
        ApiClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deltempo-Updater", "1.0"));
        ApiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>
    /// User-writable directory holding the console binary and its shims. Deliberately the same
    /// cache the official one-line installers already use, so a manual <c>win-cli</c> install and
    /// automatic provisioning converge on one file instead of racing each other.
    /// </summary>
    public static string ToolDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Deltempo", "bin");

    /// <summary>Absolute path the console binary is staged at once provisioned.</summary>
    public static string StagedCliPath => Path.Combine(ToolDirectory, CliFileName);

    /// <summary>
    /// Matches exactly the block this service writes, so repair and removal can never drift apart
    /// and a stale absolute path left by an older install is always replaced.
    /// </summary>
    private static readonly Regex ManagedBlockRegex = new(
        @"^[ \t]*" + Regex.Escape(MarkerBegin) + @"[^\r\n]*\r?\n[ \t]*function[ \t]+(?:global:)?deltempo[^\r\n]*\r?\n(?:[ \t]*" +
        Regex.Escape(MarkerEnd) + @"[^\r\n]*(?:\r?\n)?)?",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Outcome of a provisioning attempt.</summary>
    public sealed record CliProvisioningResult(bool Success, string BinaryPath, string Message)
    {
        internal static CliProvisioningResult Fail(string message) => new(false, string.Empty, message);
    }

    /// <summary>
    /// Whether the file is a complete, plausible Deltempo console binary rather than a
    /// placeholder, a stub, or a truncated download.
    /// </summary>
    public static bool IsUsableCliBinary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            var info = new FileInfo(path);
            if (info.Length < MinPlausibleBytes) return false;

            // Framework-dependent build: the small apphost is only meaningful next to its managed dll.
            if (File.Exists(Path.ChangeExtension(path, ".dll"))) return true;

            // Self-contained single-file publish carries the whole runtime.
            return info.Length > SingleFileThresholdBytes;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the binary carries its own runtime, making it safe to copy on its own. Anything
    /// smaller is a framework-dependent apphost that only runs from its own output folder.
    /// </summary>
    public static bool IsSelfContained(string path)
    {
        try
        {
            return new FileInfo(path).Length > SingleFileThresholdBytes;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Locates a usable console binary that already exists on this machine.</summary>
    public static string? FindLocalCliBinary()
    {
        foreach (string dir in EnumerateCandidateDirectories())
        {
            string candidate = Path.Combine(dir, CliFileName);
            if (IsUsableCliBinary(candidate)) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateCandidateDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in BuildCandidateDirectories())
        {
            if (seen.Add(dir)) yield return dir;
        }
    }

    private static IEnumerable<string> BuildCandidateDirectories()
    {
        yield return ToolDirectory;

        string? baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            yield return baseDir;
            foreach (string relative in new[] { "publish_cli", "publish", "dist" })
            {
                string? resolved = SafeCombine(baseDir, "..", "..", "..", relative);
                if (resolved != null) yield return resolved;
            }
        }

        string? repoRoot = FindRepositoryRoot(baseDir);
        if (repoRoot != null)
        {
            foreach (string relative in new[]
            {
                Path.Combine("Cli", "bin", "Debug", "net10.0-windows", "win-x64"),
                Path.Combine("Cli", "bin", "Release", "net10.0-windows", "win-x64")
            })
            {
                yield return Path.Combine(repoRoot, relative);
            }
        }
    }

    private static string? FindRepositoryRoot(string? start)
    {
        DirectoryInfo? dir = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
        for (int depth = 0; dir != null && depth < 8; depth++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Deltempo.Cli.csproj")) ||
                File.Exists(Path.Combine(dir.FullName, "deltempo.sln")))
            {
                return dir.FullName;
            }
        }
        return null;
    }

    private static string? SafeCombine(params string[] parts)
    {
        try
        {
            return Path.GetFullPath(Path.Combine(parts));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Guarantees a verified console binary is staged in the tool directory, preferring a copy
    /// that already exists locally and otherwise fetching the official release artifact with a
    /// mandatory SHA-256 match. Never throws and never leaves a partial file behind.
    /// </summary>
    public static async Task<CliProvisioningResult> EnsureCliBinaryAsync(CancellationToken ct = default)
    {
        if (IsUsableCliBinary(StagedCliPath))
        {
            return new CliProvisioningResult(true, StagedCliPath, "Console CLI already provisioned.");
        }

        string? local = FindLocalCliBinary();
        if (local != null)
        {
            // A framework-dependent apphost is only valid inside its own output folder, next to its
            // managed dll and runtimeconfig. Copying the exe alone would stage a binary that
            // cannot start, so such a build is used in place instead of being relocated.
            if (!IsSelfContained(local))
            {
                return new CliProvisioningResult(true, local, $"Using local console CLI at {local}.");
            }

            try
            {
                Directory.CreateDirectory(ToolDirectory);
                File.Copy(local, StagedCliPath, overwrite: true);
                if (IsUsableCliBinary(StagedCliPath))
                {
                    return new CliProvisioningResult(true, StagedCliPath, $"Staged console CLI from {local}.");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] CLI staging failed: {ex.Message}");
            }
        }

        CliReleaseAsset? asset = await UpdateService.ResolveLatestCliAssetAsync(ct).ConfigureAwait(false);
        if (asset == null)
        {
            return CliProvisioningResult.Fail("Could not resolve the official CLI release asset.");
        }

        if (!UpdateSecurityValidator.IsValidDownloadUrl(asset.DownloadUrl, out string reason))
        {
            return CliProvisioningResult.Fail($"Rejected CLI download URL: {reason}");
        }

        string staging = Path.Combine(ToolDirectory, ".cli-staging");
        try
        {
            Directory.CreateDirectory(ToolDirectory);
            var (filePath, _, _) = await UpdateDownloader.DownloadAsync(
                asset.DownloadUrl,
                staging,
                CliFileName,
                asset.Sha256,
                asset.SizeBytes,
                progress: null,
                ct).ConfigureAwait(false);

            File.Move(filePath, StagedCliPath, overwrite: true);
            return new CliProvisioningResult(true, StagedCliPath, $"Downloaded and verified {CliFileName}.");
        }
        catch (Exception ex)
        {
            return CliProvisioningResult.Fail($"CLI download failed: {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
        }
    }

    // ─── PURE GENERATORS ──────────────────────────────────────────────
    // Kept side-effect free and internal so the exact bytes written to disk are unit-testable.

    /// <summary>
    /// Body of the <c>deltempo.cmd</c> shim. Calls the console binary directly (no <c>start</c>),
    /// because <c>start</c> discards the child's exit code and would make every scripted command
    /// report success regardless of what actually happened.
    /// </summary>
    internal static string BuildCmdContent(string cliPath) =>
        "@echo off\r\n" +
        "\"" + EscapeForCmd(cliPath) + "\" %*\r\n" +
        "exit /b %ERRORLEVEL%\r\n";

    /// <summary>Body of the <c>deltempo.ps1</c> shim, preserving the native exit code.</summary>
    internal static string BuildPs1Content(string cliPath) =>
        "& \"" + EscapeForPowerShell(cliPath) + "\" @args\r\n" +
        "exit $LASTEXITCODE\r\n";

    /// <summary>The exact managed block written into PowerShell profiles.</summary>
    internal static string BuildProfileBlock(string cliPath) =>
        MarkerBegin + "\r\n" +
        "function deltempo { & \"" + EscapeForPowerShell(cliPath) + "\" @args }\r\n" +
        MarkerEnd + "\r\n";

    /// <summary>
    /// Replaces any previously managed block with a fresh one, leaving all user-authored content
    /// untouched. This is what repairs a stale path left behind by an older install or a move.
    /// </summary>
    internal static string UpsertManagedBlock(string profileText, string cliPath)
    {
        string stripped = ManagedBlockRegex.Replace(profileText ?? string.Empty, string.Empty);
        string body = stripped.TrimEnd('\r', '\n');
        string block = BuildProfileBlock(cliPath);

        return body.Length == 0 ? block : body + "\r\n\r\n" + block;
    }

    /// <summary>Strips the managed block, reporting whether anything was actually removed.</summary>
    internal static bool RemoveManagedBlock(string profileText, out string updated)
    {
        updated = ManagedBlockRegex.Replace(profileText ?? string.Empty, string.Empty);
        return !string.Equals(updated, profileText, StringComparison.Ordinal);
    }

    /// <summary>Whether the profile already routes <c>deltempo</c> to exactly this binary.</summary>
    internal static bool ProfileTargetsBinary(string profileText, string cliPath) =>
        (profileText ?? string.Empty).Contains(BuildProfileBlock(cliPath).TrimEnd('\r', '\n'), StringComparison.Ordinal);

    private static string EscapeForCmd(string path) => path.Replace("\"", "\"\"");

    private static string EscapeForPowerShell(string path) => path.Replace("`", "``").Replace("\"", "`\"");
}
