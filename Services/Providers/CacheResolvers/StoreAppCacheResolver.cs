using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class StoreAppCacheResolver
{
    public static List<string> Resolve()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packagesRoot = Path.Combine(localAppData, "Packages");
        var dirs = new List<string>();

        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot))
                {
                    // 100% safe standard UWP temporary/cache locations
                    dirs.Add(Path.Combine(pkg, "AC", "INetCache"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                    dirs.Add(Path.Combine(pkg, "CrashDump"));

                    // Safe sub-cache inside LocalState ONLY (never LocalState itself which stores settings/databases)
                    var localStateCache = Path.Combine(pkg, "LocalState", "Cache");
                    if (Directory.Exists(localStateCache))
                    {
                        dirs.Add(localStateCache);
                    }

                    // Safe sub-caches inside LocalCache
                    // CRITICAL: NEVER add pkg\LocalCache wholesale!
                    // In Centennial (Desktop Bridge) apps, LocalCache\Roaming and LocalCache\Local contain virtualized %APPDATA%
                    // (e.g. Telegram Desktop UWP session tdata, Slack, etc.).
                    // In WebView2 apps (WhatsApp Desktop, New Teams), LocalCache\EBWebView contains Login Data and IndexedDB!
                    var localCache = Path.Combine(pkg, "LocalCache");
                    if (Directory.Exists(localCache))
                    {
                        var msInet = Path.Combine(localCache, "Microsoft", "Windows", "INetCache");
                        if (Directory.Exists(msInet)) dirs.Add(msInet);

                        var msInet2 = Path.Combine(localCache, "Microsoft", "INetCache");
                        if (Directory.Exists(msInet2)) dirs.Add(msInet2);

                        var tempDir = Path.Combine(localCache, "Temp");
                        if (Directory.Exists(tempDir)) dirs.Add(tempDir);

                        var ebRoot = Path.Combine(localCache, "EBWebView");
                        if (Directory.Exists(ebRoot))
                        {
                            AddEbWebViewSafeCaches(ebRoot, dirs);
                        }

                        var msTeamsEbRoot = Path.Combine(localCache, "Microsoft", "MSTeams", "EBWebView");
                        if (Directory.Exists(msTeamsEbRoot))
                        {
                            AddEbWebViewSafeCaches(msTeamsEbRoot, dirs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        return dirs;
    }

    public static void AddEbWebViewSafeCaches(string ebRoot, List<string> dirs)
    {
        try
        {
            if (!Directory.Exists(ebRoot)) return;

            // Global EBWebView cache subfolders
            dirs.Add(Path.Combine(ebRoot, "Crashpad", "reports"));
            dirs.Add(Path.Combine(ebRoot, "Crashpad", "completed"));
            dirs.Add(Path.Combine(ebRoot, "ShaderCache"));
            dirs.Add(Path.Combine(ebRoot, "GrShaderCache"));
            dirs.Add(Path.Combine(ebRoot, "GPUPersistentCache"));

            // Enumerate profile directories inside EBWebView (Default, Profile 1, etc.)
            foreach (var profileDir in Directory.EnumerateDirectories(ebRoot))
            {
                var dirName = Path.GetFileName(profileDir);
                if (dirName.Equals("Crashpad", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("ShaderCache", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("GrShaderCache", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("GPUPersistentCache", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Profile-specific safe disposable caches ONLY
                dirs.Add(Path.Combine(profileDir, "Cache"));
                dirs.Add(Path.Combine(profileDir, "Cache", "Cache_Data"));
                dirs.Add(Path.Combine(profileDir, "Code Cache"));
                dirs.Add(Path.Combine(profileDir, "GPUCache"));
                dirs.Add(Path.Combine(profileDir, "DawnCache"));
                dirs.Add(Path.Combine(profileDir, "ShaderCache"));
                dirs.Add(Path.Combine(profileDir, "GrShaderCache"));
                dirs.Add(Path.Combine(profileDir, "Service Worker", "CacheStorage"));
                // Strictly NEVER add profileDir itself, IndexedDB, Login Data, Cookies, Local Storage, or Web Data!
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
