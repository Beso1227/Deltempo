using System.IO;
using System.Reflection;

namespace WinTempCleaner.Models;

public static class BuildInfo
{
    private static string? _commitSha;
    private static string? _shortCommitSha;
    private static Version? _baseVersion;
    private static DateTime? _buildDate;

    public static Version BaseVersion
    {
        get
        {
            if (_baseVersion == null)
            {
                _baseVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            }
            return _baseVersion;
        }
    }

    public static string CommitSha
    {
        get
        {
            if (_commitSha == null)
            {
                ExtractVersionInfo();
            }
            return _commitSha ?? "unknown";
        }
    }

    public static string ShortCommitSha
    {
        get
        {
            if (_shortCommitSha == null)
            {
                var sha = CommitSha;
                _shortCommitSha = sha.Length >= 7 ? sha[..7] : sha;
            }
            return _shortCommitSha;
        }
    }

    public static DateTime BuildDateUtc
    {
        get
        {
            if (_buildDate == null)
            {
                try
                {
                    var path = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        path = Path.Combine(AppContext.BaseDirectory, "Deltempo.exe");
                    }
                    if (File.Exists(path))
                    {
                        _buildDate = File.GetLastWriteTimeUtc(path);
                    }
                }
                catch
                {
                    // Fallback to now if file attributes cannot be read
                }
                _buildDate ??= DateTime.UtcNow;
            }
            return _buildDate.Value;
        }
    }

    public static string VersionWithPatchDisplay =>
        $"v{BaseVersion.ToString(3)} ({ShortCommitSha})";

    private static void ExtractVersionInfo()
    {
        try
        {
            var infoVer = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                var plusIdx = infoVer.IndexOf('+');
                if (plusIdx >= 0 && plusIdx < infoVer.Length - 1)
                {
                    _commitSha = infoVer[(plusIdx + 1)..].Trim();
                    return;
                }
            }
        }
        catch
        {
        }

        _commitSha = "unknown";
    }
}
