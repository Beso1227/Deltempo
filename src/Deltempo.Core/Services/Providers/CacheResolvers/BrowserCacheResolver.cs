using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class BrowserCacheResolver
{
    public static List<string> Resolve()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dirs = new List<string>();

        // Chromium browser User Data roots
        var chromiumRoots = new (string BaseDir, string FallbackSubDir)[]
        {
            (Path.Combine(localAppData, "Google", "Chrome", "User Data"), "Default"),
            (Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "Default"),
            (Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), "Default"),
            (Path.Combine(localAppData, "Vivaldi", "User Data"), "Default"),
            (Path.Combine(localAppData, "Arc", "User Data"), "Default"),
            (Path.Combine(localAppData, "Yandex", "YandexBrowser", "User Data"), "Default"),
            (Path.Combine(localAppData, "Opera Software", "Opera Stable"), ""),
            (Path.Combine(localAppData, "Opera Software", "Opera GX Stable"), "")
        };

        foreach (var (baseDir, fallbackSub) in chromiumRoots)
        {
            if (!Directory.Exists(baseDir)) continue;

            var profileDirs = new List<string>();

            // Find all profiles (Default, Profile 1, Profile 2, etc.)
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(baseDir))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (dirName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        dirName.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("System Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        profileDirs.Add(subDir);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            // If no profile subdirectories matched, treat baseDir itself as the container (e.g. Opera)
            if (profileDirs.Count == 0)
            {
                profileDirs.Add(baseDir);
            }

            foreach (var prof in profileDirs)
            {
                dirs.Add(Path.Combine(prof, "Cache"));
                dirs.Add(Path.Combine(prof, "Cache", "Cache_Data"));
                dirs.Add(Path.Combine(prof, "Code Cache"));
                dirs.Add(Path.Combine(prof, "GPUCache"));
                dirs.Add(Path.Combine(prof, "DawnCache"));
                dirs.Add(Path.Combine(prof, "ShaderCache"));
                dirs.Add(Path.Combine(prof, "GrShaderCache"));
                dirs.Add(Path.Combine(prof, "Crashpad", "reports"));
                dirs.Add(Path.Combine(prof, "blob_storage"));
            }
        }

        // Gecko / Firefox-based browsers
        var geckoRoots = new string[]
        {
            Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles"),
            Path.Combine(localAppData, "Floorp", "Profiles"),
            Path.Combine(localAppData, "Waterfox", "Profiles"),
            Path.Combine(localAppData, "LibreWolf", "Profiles"),
            Path.Combine(localAppData, "zen", "Profiles")
        };

        foreach (var gRoot in geckoRoots)
        {
            if (!Directory.Exists(gRoot)) continue;
            try
            {
                foreach (var prof in Directory.EnumerateDirectories(gRoot))
                {
                    dirs.Add(Path.Combine(prof, "cache2"));
                    dirs.Add(Path.Combine(prof, "startupCache"));
                    dirs.Add(Path.Combine(prof, "thumbnails"));
                    dirs.Add(Path.Combine(prof, "jumpListCache"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        return dirs;
    }
}
