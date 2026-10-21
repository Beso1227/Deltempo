using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class StoreAppCacheResolver
{
    // Strict blacklist of all communication, messaging, meeting, collaboration, and identity store packages
    public static readonly string[] ProtectedStorePackageKeywords =
    {
        "whatsapp", "telegram", "msteams", "teams", "discord", "slack", "signal",
        "skype", "zoom", "viber", "element", "wechat", "line", "kakao", "messenger",
        "session", "threema", "wire", "icq", "mattermost", "webex", "cisco-spark", "ciscospark",
        "ringcentral", "thunderbird", "outlook", "rocketchat", "keybase", "zulip", "chime", "flock",
        "matrix", "accountscontrol", "aad", "cloudexperiencehost", "bioenrollment", "auth"
    };

    public static bool IsProtectedStorePackage(string packageNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(packageNameOrPath)) return false;
        var lower = packageNameOrPath.ToLowerInvariant();
        foreach (var kw in ProtectedStorePackageKeywords)
        {
            if (lower.Contains(kw)) return true;
        }
        return false;
    }

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
                    var pkgName = Path.GetFileName(pkg);

                    // ZERO-TOUCH POLICY: Completely skip any messaging, communication, collaboration, or auth package!
                    if (IsProtectedStorePackage(pkgName))
                    {
                        continue;
                    }

                    // 100% safe standard UWP temporary/cache locations for generic non-communication store apps
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
                // Profile-specific safe disposable caches ONLY (HTTP cache, compiled shaders, bytecode)
                dirs.Add(Path.Combine(profileDir, "Cache"));
                dirs.Add(Path.Combine(profileDir, "Cache", "Cache_Data"));
                dirs.Add(Path.Combine(profileDir, "Code Cache"));
                dirs.Add(Path.Combine(profileDir, "GPUCache"));
                dirs.Add(Path.Combine(profileDir, "DawnCache"));
                dirs.Add(Path.Combine(profileDir, "ShaderCache"));
                dirs.Add(Path.Combine(profileDir, "GrShaderCache"));
                // Strictly NEVER add profileDir itself, IndexedDB, Service Worker, Login Data, Cookies, Local Storage, or Web Data!
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
