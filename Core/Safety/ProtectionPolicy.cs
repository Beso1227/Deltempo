using System.IO;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Centralized, immutable protection policy enforcing non-negotiable safety rules.
/// Dictates which directories, file types, credentials, session stores, and system paths
/// are strictly PROTECTED and forbidden from automatic or manual deletion.
/// </summary>
public static class ProtectionPolicy
{
    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Credentials, Key Stores & Password Managers
        ".kdbx", ".kdb", ".key", ".pem", ".pfx", ".p12", ".crt", ".cer", ".asc",
        ".gpg", ".pgp", ".pkcs12", ".ovpn", ".ssh",

        // Sensitive Personal & Office Documents
        ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".pdf",
        ".odt", ".ods", ".odp", ".rtf", ".txt", ".csv",

        // Creative, 3D & Design Projects
        ".psd", ".ai", ".blend", ".prproj", ".aep", ".dwg", ".c4d", ".fig",

        // Developer Source Code & Project Definitions
        ".sln", ".csproj", ".fsproj", ".vcxproj", ".cs", ".rs", ".go",
        ".py", ".cpp", ".c", ".h", ".hpp", ".java", ".kt", ".swift",
        ".ts", ".js", ".html", ".css", ".sql", ".sh", ".ps1",

        // Machine Learning & AI Model Weights
        ".safetensors", ".gguf", ".onnx", ".pt", ".pth", ".ckpt", ".h5"
    };

    private static readonly HashSet<string> ProtectedRootFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "dumpstack.log",
        "bootmgr",
        "bootnxt",
        "ntldr",
        "ntdetect.com",
        "autoexec.bat",
        "config.sys"
    };

    private static readonly HashSet<string> CommunicationAppNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "whatsapp", "telegram", "msteams", "teams", "discord", "slack", "signal",
        "skype", "zoom", "viber", "element", "wechat", "line", "kakao", "messenger",
        "session", "threema", "wire", "icq", "mattermost", "webex", "cisco-spark",
        "ciscospark", "thunderbird", "outlook", "rocketchat", "keybase", "zulip",
        "ringcentral", "flock", "chime"
    };

    private static readonly HashSet<string> ProtectedSessionFilePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "local state", "login data", "cookies", "web data", "preferences",
        "secure preferences", "settings.dat", "roaming.lock", "key_data",
        "accounts", "tokens", "credentials", "user.dat", "userclasses.dat",
        "storage.json", "state.vscdb", "session.db", "persistent.conf"
    };

    /// <summary>
    /// Checks if a given file path is classified as strictly PROTECTED.
    /// If true, the file CANNOT be deleted under any circumstances.
    /// </summary>
    public static bool IsProtected(string filePath, out string matchedReason)
    {
        matchedReason = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            matchedReason = "Invalid or empty path";
            return true;
        }

        string path = PathSecurity.NormalizeCanonicalPath(filePath);
        if (string.IsNullOrEmpty(path))
        {
            matchedReason = "Could not resolve canonical path";
            return true;
        }

        string pathLower = path.ToLowerInvariant();
        string fileName = Path.GetFileName(pathLower);
        string ext = Path.GetExtension(pathLower);

        // 1. Root OS Critical Files (pagefile, hiberfil, bootmgr)
        if (ProtectedRootFileNames.Contains(fileName))
        {
            matchedReason = "Windows critical OS root system file";
            return true;
        }

        // 2. Windows Core System Directories (System32, SysWOW64, WinSxS, Boot, Recovery)
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string system32 = Path.Combine(winDir, "System32");
        string syswow64 = Path.Combine(winDir, "SysWOW64");
        string winsxs = Path.Combine(winDir, "WinSxS");
        string boot = Path.Combine(winDir, "Boot");

        if (PathSecurity.IsSubpathOf(path, system32) ||
            PathSecurity.IsSubpathOf(path, syswow64) ||
            PathSecurity.IsSubpathOf(path, winsxs) ||
            PathSecurity.IsSubpathOf(path, boot) ||
            pathLower.Contains(@"\system volume information") ||
            pathLower.Contains(@"\$recycle.bin"))
        {
            // Note: System Restore and Recycle Bin are cleaned via dedicated Windows APIs, never raw file deletion
            matchedReason = "Protected Windows kernel/system directory";
            return true;
        }

        // 3. User Libraries & Personal Storage (Documents, Desktop, Pictures, Music, Videos)
        if (IsUserPersonalDirectory(path))
        {
            matchedReason = "User personal library or workspace folder (Documents/Desktop/Pictures/Projects)";
            return true;
        }

        // 4. Developer & System Credentials / SSH / Cloud Keys
        if (IsDeveloperOrCloudCredentialPath(pathLower, fileName, ext))
        {
            matchedReason = "Developer / SSH / Cloud authentication credential or key";
            return true;
        }

        // 5. Browser Credentials, Saved Passwords & Login Sessions
        if (IsBrowserCredentialOrSession(pathLower, fileName))
        {
            matchedReason = "Web browser saved login credentials, cookies, or session database";
            return true;
        }

        // 6. Communication & Messaging App State (Telegram, WhatsApp, Discord, Slack, etc.)
        if (IsCommunicationAppState(pathLower, fileName, ext))
        {
            matchedReason = "Messaging or communication application active session, auth key, or database";
            return true;
        }

        // 7. Gaming Platform Assets & Libraries (Steam, Epic Games, GOG, Riot, Ubisoft, EA)
        if (pathLower.Contains(@"\steamapps\") || pathLower.Contains(@"\steamlibrary\") ||
            pathLower.Contains(@"\epic games\") || pathLower.Contains(@"\gog games\") ||
            pathLower.Contains(@"\gog galaxy\") || pathLower.Contains(@"\riot games\") ||
            pathLower.Contains(@"\ea games\") || pathLower.Contains(@"\origin games\"))
        {
            matchedReason = "Steam / Gaming Platform Assets";
            return true;
        }

        // 8. Sensitive Extensions (KeePass databases, cryptographic keys, personal documents, source code, AI weights)
        // If a sensitive document is sitting in a temp or download folder, we STILL protect it!
        if (SensitiveExtensions.Contains(ext))
        {
            // If the file resides inside a recognized package or application script cache,
            // source/script/text files are cached dependencies or compiled scripts, NOT user documents.
            if (IsPackageOrScriptCachePath(pathLower) &&
                ext is ".js" or ".ts" or ".py" or ".cs" or ".rs" or ".go" or ".cpp" or ".c" or ".h" or ".hpp"
                    or ".java" or ".kt" or ".swift" or ".html" or ".css" or ".sql" or ".sh" or ".ps1" or ".txt" or ".csv")
            {
                // Safe package/script cache dependency - permitted for cache purge
            }
            else
            {
                matchedReason = ext switch
                {
                    ".safetensors" or ".gguf" or ".onnx" or ".pt" or ".pth" or ".ckpt" or ".h5"
                        => "AI Model Weights and Machine Learning Assets",
                    ".kdbx" or ".kdb" or ".key" or ".pem" or ".pfx" or ".p12"
                        => "Cryptographic Key or Password Database",
                    _ => $"Protected sensitive file type ({ext})"
                };
                return true;
            }
        }

        // 9. NTFS Reparse Point, Symlink or Junction
        if (PathSecurity.IsReparsePointOrLink(path))
        {
            matchedReason = "Filesystem Reparse Point, Symbolic Link, or Directory Junction";
            return true;
        }

        return false;
    }

    private static bool IsUserPersonalDirectory(string path)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile)) return false;

        string[] userProtectedDirs =
        {
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Pictures"),
            Path.Combine(userProfile, "Music"),
            Path.Combine(userProfile, "Videos"),
            Path.Combine(userProfile, "Contacts"),
            Path.Combine(userProfile, "Favorites"),
            Path.Combine(userProfile, "Searches"),
            Path.Combine(userProfile, "Saved Games"),
            Path.Combine(userProfile, "Source"),
            Path.Combine(userProfile, "Repos"),
            Path.Combine(userProfile, "Projects"),
            Path.Combine(userProfile, "OneDrive")
        };

        foreach (var dir in userProtectedDirs)
        {
            if (PathSecurity.IsSubpathOf(path, dir))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDeveloperOrCloudCredentialPath(string pathLower, string fileName, string ext)
    {
        if (pathLower.Contains(@"\.ssh\") ||
            pathLower.Contains(@"\.gnupg\") ||
            pathLower.Contains(@"\.aws\") ||
            pathLower.Contains(@"\.azure\") ||
            pathLower.Contains(@"\.kube\") ||
            pathLower.Contains(@"\.docker\") ||
            pathLower.Contains(@"\.git\") ||
            fileName == "id_rsa" || fileName == "id_ed25519" || fileName == "known_hosts" ||
            fileName == "authorized_keys" || fileName == ".gitconfig" || fileName == ".netrc")
        {
            return true;
        }

        return false;
    }

    private static bool IsBrowserCredentialOrSession(string pathLower, string fileName)
    {
        bool inBrowserTree = pathLower.Contains(@"\google\chrome\") ||
                             pathLower.Contains(@"\microsoft\edge\") ||
                             pathLower.Contains(@"\bravesoftware\") ||
                             pathLower.Contains(@"\mozilla\firefox\") ||
                             pathLower.Contains(@"\opera software\");

        if (inBrowserTree)
        {
            // Login data, cookies, web data, secure preferences, state
            if (fileName.StartsWith("login data") ||
                fileName.StartsWith("cookies") ||
                fileName.StartsWith("web data") ||
                fileName.StartsWith("local state") ||
                fileName.StartsWith("preferences") ||
                fileName.StartsWith("secure preferences") ||
                fileName.StartsWith("history") ||
                fileName.StartsWith("bookmarks") ||
                pathLower.Contains(@"\sessions\") ||
                pathLower.Contains(@"\session storage\") ||
                pathLower.Contains(@"\indexeddb\") ||
                pathLower.Contains(@"\sync data\"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCommunicationAppState(string pathLower, string fileName, string ext)
    {
        // 1. Zero-touch sandbox protection for ALL messaging, communication, meeting, and auth Store packages
        if (pathLower.Contains(@"\packages\"))
        {
            foreach (var app in CommunicationAppNames)
            {
                if (pathLower.Contains(app)) return true;
            }
        }

        // 2. Telegram Desktop authentication keys (inside tdata)
        // ONLY user_data\cache, temp, and dumps subfolders inside tdata are disposable.
        if (pathLower.Contains(@"\tdata\"))
        {
            if (!pathLower.Contains(@"\tdata\user_data\cache\") &&
                !pathLower.Contains(@"\tdata\temp\") &&
                !pathLower.Contains(@"\tdata\dumps\"))
            {
                return true;
            }
        }

        // 2. Communication Apps in AppData (WhatsApp, Teams, Discord, Slack, Signal, etc.)
        foreach (var app in CommunicationAppNames)
        {
            if (pathLower.Contains(app))
            {
                // Protected database & config extensions inside communication apps
                if (ext is ".db" or ".db-wal" or ".db-shm" or ".sqlite" or ".sqlite-wal" or
                           ".sqlite-shm" or ".ldb" or ".json" or ".conf" or ".cfg" or ".ini")
                {
                    return true;
                }

                // Protect session and account prefixes
                foreach (var prefix in ProtectedSessionFilePrefixes)
                {
                    if (fileName.StartsWith(prefix)) return true;
                }

                // If inside communication app, protect anything outside explicit cache/temp folders
                bool inExplicitCache = pathLower.Contains(@"\gpucache\") ||
                                       pathLower.Contains(@"\dawncache\") ||
                                       pathLower.Contains(@"\crashpad\") ||
                                       pathLower.Contains(@"\temp\") ||
                                       pathLower.Contains(@"\dumps\") ||
                                       pathLower.Contains(@"\avatars\") ||
                                       pathLower.Contains(@"\all users\cache\") ||
                                       pathLower.Contains(@"\cache\") ||
                                       pathLower.Contains(@"\cache2\entries\") ||
                                       pathLower.Contains(@"\logs\");

                if (!inExplicitCache)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static readonly string[] PackageAndScriptCachePathMarkers =
    {
        @"\npm-cache\",
        @"\pip\cache\",
        @"\yarn\cache\",
        @"\pnpm\store\",
        @"\pnpm-cache\",
        @"\nuget\v3-cache\",
        @"\nuget\plugins-cache\",
        @"\.cache\",
        @"\.gradle\caches\",
        @"\.cargo\registry\cache\",
        @"\.cargo\git\db\",
        @"\.rustup\downloads\",
        @"\.rustup\tmp\",
        @"\.bun\install\cache\",
        @"\deno\deps\",
        @"\go-build\",
        @"\.m2\repository\.cache\",
        @"\.m2\temp\",
        @"\temp\.net\",
        @"\code cache\js\",
        @"\code cache\wasm\",
        @"\gpucache\",
        @"\scriptcache\"
    };

    public static bool IsPackageOrScriptCachePath(string pathLower)
    {
        foreach (var marker in PackageAndScriptCachePathMarkers)
        {
            if (pathLower.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
