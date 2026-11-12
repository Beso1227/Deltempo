using System.Collections.Frozen;
using System.IO;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Centralized, immutable protection policy enforcing non-negotiable safety rules.
/// Dictates which directories, file types, credentials, session stores, and system paths
/// are strictly PROTECTED and forbidden from automatic or manual deletion.
/// </summary>
public static class ProtectionPolicy
{
    private static readonly FrozenSet<string> SensitiveExtensions = new string[]
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
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ProtectedRootFileNames = new string[]
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
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> CommunicationAppNames = new string[]
    {
        "whatsapp", "telegram", "msteams", "teams", "discord", "slack", "signal",
        "skype", "zoom", "viber", "element", "wechat", "line", "kakao", "messenger",
        "session", "threema", "wire", "icq", "mattermost", "webex", "cisco-spark",
        "ciscospark", "thunderbird", "outlook", "rocketchat", "keybase", "zulip",
        "ringcentral", "flock", "chime"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ProtectedSessionFilePrefixes = new string[]
    {
        "local state", "login data", "cookies", "web data", "preferences",
        "secure preferences", "settings.dat", "roaming.lock", "key_data",
        "accounts", "tokens", "credentials", "user.dat", "userclasses.dat",
        "storage.json", "state.vscdb", "session.db", "persistent.conf"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ForbiddenSystemBinaryExtensions = new string[]
    {
        ".dll", ".sys", ".exe", ".inf", ".cat", ".ocx", ".cpl", ".msc", ".drv", ".com", ".scr", ".efi"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] PermittedSystem32SubdirectoryMarkers =
    [
        @"system32\driverstore\temp",
        @"system32\driverstate",
        @"system32\logfiles",
        @"system32\winevt\logs"
    ];

    private static bool IsPermittedSystem32Subdirectory(string pathLower)
    {
        string normalized = pathLower.Replace('/', '\\');
        foreach (var marker in PermittedSystem32SubdirectoryMarkers)
        {
            if (normalized.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly object CustomExclusionsLock = new();
    private static readonly HashSet<string> CustomPathExclusions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> CustomExtensionExclusions = new(StringComparer.OrdinalIgnoreCase);

    // Lowercased ONCE when exclusions are set, not per file. IsProtected runs once per
    // file per safety check; re-calling ToLowerInvariant on every exclusion for every
    // file was pure allocation churn inside the hottest loop in the app.
    private static readonly HashSet<string> CustomPathExclusionsLower = new(StringComparer.Ordinal);

    /// <summary>
    /// Updates custom exclusions atomically in a thread-safe manner.
    /// </summary>
    public static void SetCustomExclusions(IEnumerable<string>? paths, IEnumerable<string>? extensions)
    {
        lock (CustomExclusionsLock)
        {
            CustomPathExclusions.Clear();
            CustomPathExclusionsLower.Clear();
            if (paths != null)
            {
                foreach (var p in paths)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        string normalized = p.Trim().Replace('/', '\\').TrimEnd('\\');
                        CustomPathExclusions.Add(normalized);
                        CustomPathExclusionsLower.Add(normalized.ToLowerInvariant());
                    }
                }
            }

            CustomExtensionExclusions.Clear();
            if (extensions != null)
            {
                foreach (var ext in extensions)
                {
                    if (!string.IsNullOrWhiteSpace(ext))
                    {
                        string e = ext.Trim();
                        if (!e.StartsWith('.')) e = "." + e;
                        CustomExtensionExclusions.Add(e);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a given file path is classified as strictly PROTECTED.
    /// If true, the file CANNOT be deleted under any circumstances.
    /// </summary>
    public static bool IsProtected(string filePath, out string matchedReason)
    {
        return IsProtected(filePath, out matchedReason, knownAttributes: null);
    }

    /// <summary>
    /// Attributes-aware core. The <paramref name="knownAttributes"/> overload exists for
    /// callers that already hold the file's attributes from a single FindFirstFileExW
    /// round-trip (the native scanner, the deletion revalidator). Rule 10 below - the
    /// reparse-point guard - is otherwise the only rule that touches the filesystem, and it
    /// did so three times per file (Exists, Attributes, LinkTarget), which made the
    /// protection check, not the directory walk, the dominant cost of a scan. When
    /// attributes are known, the guard reduces to the same
    /// FILE_ATTRIBUTE_REPARSE_POINT bit test the stat-based version performs, with no
    /// round-trip at all. Passing null keeps the stat-based behaviour exactly.
    /// </summary>
    public static bool IsProtected(string filePath, out string matchedReason, FileAttributes? knownAttributes)
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

        // 0. User Configured Custom Exclusions (Paths & Extensions)
        lock (CustomExclusionsLock)
        {
            if (CustomPathExclusions.Count > 0)
            {
                foreach (var exclusion in CustomPathExclusions)
                {
                    if (pathLower.StartsWith(exclusion.ToLowerInvariant()) || PathSecurity.IsSubpathOf(path, exclusion))
                    {
                        matchedReason = $"User custom excluded path: {exclusion}";
                        return true;
                    }
                }
            }

            if (CustomExtensionExclusions.Count > 0)
            {
                if (CustomExtensionExclusions.Contains(ext))
                {
                    matchedReason = $"User custom excluded file extension: {ext}";
                    return true;
                }
            }
        }

        // 1. Root OS Critical Files (pagefile, hiberfil, bootmgr)
        if (ProtectedRootFileNames.Contains(fileName))
        {
            matchedReason = "Windows critical OS root system file";
            return true;
        }

        // 2. Windows Core System Directories (System32, SysWOW64, WinSxS, Boot, Recovery)
        // Roots are resolved and canonicalized ONCE per process (see CanonicalSystemRoots);
        // re-deriving GetFolderPath + four Path.Combine calls and re-canonicalizing each
        // root on every file was the single largest per-file cost after the reparse guard.
        if (PathSecurity.IsSubpathOfCanonical(path, CanonicalSystemRoots.System32))
        {
            if (IsPermittedSystem32Subdirectory(pathLower))
            {
                if (ForbiddenSystemBinaryExtensions.Contains(ext))
                {
                    matchedReason = "Protected Windows system binary/driver in system directory";
                    return true;
                }
                // Permitted disposable staging/log subdirectory inside System32 - proceed to downstream evaluation
            }
            else
            {
                matchedReason = "Protected Windows kernel/system directory";
                return true;
            }
        }
        else if (PathSecurity.IsSubpathOfCanonical(path, CanonicalSystemRoots.SysWOW64) ||
            PathSecurity.IsSubpathOfCanonical(path, CanonicalSystemRoots.WinSxS) ||
            PathSecurity.IsSubpathOfCanonical(path, CanonicalSystemRoots.Boot) ||
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
        if (IsDeveloperOrCloudCredentialPath(pathLower, fileName))
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

        // 7. Durable AppData Application State (licences, cloud sync databases, credential
        // vaults, SSH/GPG key stores, shell jump lists). These are STATE, not cache.
        if (IsAppDataStateCore(pathLower))
        {
            matchedReason = "Application data: licence, cloud sync, credential vault, or key store state";
            return true;
        }

        // 8. Gaming Platform Assets & Libraries (Steam, Epic Games, GOG, Riot, Ubisoft, EA)
        // Exempt disposable cache, temp download chunks, compiled shaders, webcaches, crashdumps, and logs:
        bool isGamingCacheOrTemp =
            pathLower.Contains(@"\steamapps\downloading\", StringComparison.OrdinalIgnoreCase) ||
            pathLower.Contains(@"\steamapps\temp\", StringComparison.OrdinalIgnoreCase) ||
            pathLower.Contains(@"\steamapps\shadercache\", StringComparison.OrdinalIgnoreCase) ||
            pathLower.Contains(@"\saved\webcache\", StringComparison.OrdinalIgnoreCase) ||
            pathLower.Contains(@"\saved\logs\", StringComparison.OrdinalIgnoreCase) ||
            pathLower.Contains(@"\saved\crashes\", StringComparison.OrdinalIgnoreCase) ||
            pathLower.Contains(@"\saved\crashreportclient\", StringComparison.OrdinalIgnoreCase);

        if (!isGamingCacheOrTemp && (
            pathLower.Contains(@"\steamapps\") || pathLower.Contains(@"\steamlibrary\") ||
            pathLower.Contains(@"\epic games\") || pathLower.Contains(@"\gog games\") ||
            pathLower.Contains(@"\gog galaxy\") || pathLower.Contains(@"\riot games\") ||
            pathLower.Contains(@"\ea games\") || pathLower.Contains(@"\origin games\")))
        {
            matchedReason = "Steam / Gaming Platform Assets";
            return true;
        }

        // 9. Sensitive Extensions (KeePass databases, cryptographic keys, personal documents, source code, AI weights)
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

        // 10. NTFS Reparse Point, Symlink or Junction
        // When the caller already holds the file's attributes (native scanner, deletion
        // revalidator), this bit test is exactly what the stat-based check evaluates - a
        // name-surrogate link always carries FILE_ATTRIBUTE_REPARSE_POINT - so no extra
        // filesystem round-trip is spent re-proving what the caller just read.
        if (knownAttributes.HasValue)
        {
            if ((knownAttributes.Value & FileAttributes.ReparsePoint) != 0)
            {
                matchedReason = "Filesystem Reparse Point, Symbolic Link, or Directory Junction";
                return true;
            }
        }
        else if (PathSecurity.IsReparsePointOrLink(path))
        {
            matchedReason = "Filesystem Reparse Point, Symbolic Link, or Directory Junction";
            return true;
        }

        // 11. Non-disposable installer and application state that a cleanup root may contain.
        // Each of these directories was individually resolvable and several were previously
        // listed as cleanup targets. They are refused HERE as well as being removed from those
        // lists, so a resolver regression, a user-supplied rulepack, or a stale settings entry
        // cannot reintroduce them.
        foreach (string stateDir in NonDisposableStateDirectories)
        {
            if (IsSameDirectoryOrUnder(path, stateDir))
            {
                matchedReason = "Non-disposable installer or application state";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Directories that are individually resolvable and look like caches but hold state whose
    /// deletion is destructive and unrecoverable. Built once per process from the machine's
    /// real special-folder locations so the markers are absolute rather than guessed suffixes.
    /// </summary>
    private static readonly FrozenSet<string> NonDisposableStateDirectories = BuildNonDisposableStateDirectories();

    private static FrozenSet<string> BuildNonDisposableStateDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var dirs = new List<string>
        {
            // MSI source caches. msiexec requires these to REPAIR and to UNINSTALL every
            // installed MSI product; deleting them permanently breaks Programs and Features
            // for the whole machine rather than freeing scratch space.
            Path.Combine(progData, "Package Cache"),
            Path.Combine(winDir, "Installer", "$PatchCache$"),

            // Per-user .NET SDK and runtime install root (dotnet.exe hosts, sdk\, packs\,
            // shared\). Not a cache despite the AppData location.
            Path.Combine(localAppData, "Microsoft", "dotnet"),

            // Audacity crash / unsaved-session recovery: in-progress recordings that were
            // never written to disk.
            Path.Combine(localAppData, "Audacity", "SessionData"),

            // Extracted cargo crate source, required by `cargo build --offline`.
            Path.Combine(userProfile, ".cargo", "registry", "src"),

            // Defender signature rollback copies.
            Path.Combine(progData, "Microsoft", "Windows Defender", "Definition Updates", "Backup"),

            // EA Desktop application settings, not a cache.
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "UserConfig")
        };

        return dirs
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.TrimEnd('\\', '/'))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when <paramref name="path"/> IS <paramref name="directory"/> or lives beneath it.
    /// Equality is required as well as the prefix test: a pure "starts with directory + separator"
    /// check never matches a path that is the marked directory itself, which would leave the
    /// directory's own root unprotected while protecting everything inside it.
    /// </summary>
    private static bool IsSameDirectoryOrUnder(string path, string directory)
    {
        if (string.Equals(path, directory, StringComparison.OrdinalIgnoreCase)) return true;
        return path.StartsWith(directory + "\\", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Process-invariant system roots, resolved and canonicalized ONCE. Rule 2 used to call
    /// <c>Environment.GetFolderPath(Windows)</c> plus four <c>Path.Combine</c>s and then
    /// re-canonicalize each combined root on EVERY file - for a scan of half a million files
    /// that was two million Combine calls and two million redundant canonicalizations of
    /// strings that never change while the process runs.
    /// </summary>
    private static class CanonicalSystemRoots
    {
        private static string Canonical(string path) =>
            string.IsNullOrEmpty(path) ? string.Empty : PathSecurity.NormalizeCanonicalPath(path);

        private static string CanonicalUnderWindows(string sub) =>
            Canonical(Path.Combine(CanonicalWindowsDirectory, sub));

        private static readonly string CanonicalWindowsDirectory =
            Canonical(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        public static readonly string System32 = CanonicalUnderWindows("System32");
        public static readonly string SysWOW64 = CanonicalUnderWindows("SysWOW64");
        public static readonly string WinSxS = CanonicalUnderWindows("WinSxS");
        public static readonly string Boot = CanonicalUnderWindows("Boot");
    }

    private static bool IsUserPersonalDirectory(string path)
    {
        // Same one-time derivation: the user profile, the thirteen personal directories
        // under it and the Public-profile folders never change while the process runs, so
        // they are canonicalized once and compared with the allocation-free canonical
        // containment test instead of re-canonicalizing each root on every file.
        foreach (var dir in CanonicalUserProtectedDirs)
        {
            if (PathSecurity.IsSubpathOfCanonical(path, dir))
            {
                return true;
            }
        }

        foreach (var folder in CanonicalPublicPersonalFolders)
        {
            if (!string.IsNullOrEmpty(folder) && PathSecurity.IsSubpathOfCanonical(path, folder))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] CanonicalUserProtectedDirs = BuildCanonicalUserProtectedDirs();

    private static string[] BuildCanonicalUserProtectedDirs()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile)) return [];

        string[] userProtectedDirs =
        [
            "Documents",
            "Desktop",
            "Pictures",
            "Music",
            "Videos",
            "Contacts",
            "Favorites",
            "Searches",
            "Saved Games",
            "Source",
            "Repos",
            "Projects",
            "OneDrive"
        ];

        var canonical = new List<string>(userProtectedDirs.Length);
        foreach (var sub in userProtectedDirs)
        {
            string c = PathSecurity.NormalizeCanonicalPath(Path.Combine(userProfile, sub));
            if (!string.IsNullOrEmpty(c)) canonical.Add(c);
        }
        return canonical.ToArray();
    }

    /// <summary>
    /// Canonicalized once at first use: the per-machine Public profile libraries.
    /// the per-machine Public profile libraries. Never treated as disposable - a file here
    /// was put there deliberately so other accounts could see it.
    /// </summary>
    private static readonly string[] CanonicalPublicPersonalFolders = BuildCanonicalPublicPersonalFolders();

    private static string[] BuildCanonicalPublicPersonalFolders()
    {
        var folders = new List<string>(7);

        void Add(string raw)
        {
            string c = PathSecurity.NormalizeCanonicalPath(raw);
            if (!string.IsNullOrEmpty(c)) folders.Add(c);
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonMusic));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonVideos));

        // .NET exposes no CommonDownloads, so derive the Public root from CommonDocuments
        // and cover the remaining shared folders by name.
        string commonDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        string? publicRoot = string.IsNullOrEmpty(commonDocs) ? null : Path.GetDirectoryName(commonDocs);
        if (!string.IsNullOrEmpty(publicRoot))
        {
            Add(Path.Combine(publicRoot, "Downloads"));
            Add(Path.Combine(publicRoot, "Libraries"));
        }

        return folders.ToArray();
    }

    private static bool IsDeveloperOrCloudCredentialPath(string pathLower, string fileName)
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

    /// <summary>
    /// AppData subtrees holding DURABLE application state rather than disposable cache:
    /// cloud sync databases, licence/activation data, credential vaults, key stores, and
    /// shell jump lists. Every marker is anchored with a trailing separator so a near-miss
    /// name (for example "OneDriveCache") is never refused.
    /// </summary>
    private static readonly FrozenSet<string> AppDataStateSubtreeMarkers = new string[]
    {
        @"\microsoft\onedrive",
        @"\microsoft\office",
        @"\microsoft\credentials",
        @"\recent\automaticdestinations",
        @"\recent\customdestinations",
        @"\vault\",
        @"\ssh\",
        @"\gnupg\",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-user shell hive files living directly in the AppData ROOT. Prefix-matched because
    /// the shell writes timestamped siblings (NTUSER.DAT-1FA2B3C4).
    /// </summary>
    private static readonly FrozenSet<string> AppDataStateRootFilePrefixes = new string[]
    {
        "ntuser.dat",
        "usrclass.dat",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Upper bound on directories inspected by <see cref="TryGetProtectedDescendant"/>.
    /// Exceeding it FAILS CLOSED: an incomplete scan must never authorise a recursive delete.
    /// </summary>
    private const int ProtectedDescendantScanCap = 5000;

    /// <summary>
    /// True when the path is durable application state inside a user AppData tree.
    /// Public entry point; normalizes the path first.
    /// </summary>
    public static bool IsAppDataApplicationState(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string canonical = PathSecurity.NormalizeCanonicalPath(path);
        return !string.IsNullOrEmpty(canonical) && IsAppDataStateCore(canonical);
    }

    /// <summary>
    /// Core predicate. Lowercases defensively rather than trusting the caller: a
    /// case-sensitive marker match silently fails to protect, which is the worst
    /// possible failure direction for a safety guard.
    /// </summary>
    private static bool IsAppDataStateCore(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        string pathLower = path.ToLowerInvariant();

        // Normalize to a DIRECTORY-shaped string (trailing separator) so a path that IS the
        // marked folder still matches. Without this, marker "\vault\" would match
        // "AppData\Roaming\Vault\keys" but NOT "AppData\Roaming\Vault" itself - and the
        // directory-unit guard is exactly the case that needs to fire.
        string probe = pathLower.EndsWith(@"\", StringComparison.Ordinal) ? pathLower : pathLower + @"\";

        // Scoped to AppData trees on purpose: "vault", "ssh" and "gnupg" are only refuse
        // targets beneath %APPDATA% / %LOCALAPPDATA% / LocalLow. A user's own
        // C:\Projects\vault\ source tree is not application state.
        if (!probe.Contains(@"\appdata\", StringComparison.Ordinal)) return false;

        foreach (var marker in AppDataStateSubtreeMarkers)
        {
            if (probe.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
        }

        string fileName = Path.GetFileName(pathLower);
        foreach (var prefix in AppDataStateRootFilePrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Guards RECURSIVE directory deletion. <see cref="IsProtected"/> is evaluated per FILE,
    /// so it never runs for <c>Directory.Delete(path, recursive: true)</c> - a scope root that
    /// merely CONTAINS a protected subtree would otherwise be deleted straight through it.
    ///
    /// Returns true and sets <paramref name="matchedSubtree"/> to the first protected subtree
    /// found beneath <paramref name="path"/>. Fails closed when the scan cap is exceeded.
    /// </summary>
    public static bool TryGetProtectedDescendant(string path, out string matchedSubtree)
    {
        matchedSubtree = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;

        string canonical = PathSecurity.NormalizeCanonicalPath(path);
        if (string.IsNullOrEmpty(canonical) || !Directory.Exists(canonical)) return false;

        // The denylist is AppData-scoped, so nothing outside an AppData tree can match.
        if (!canonical.ToLowerInvariant().Contains(@"\appdata\", StringComparison.Ordinal)) return false;

        if (IsAppDataStateCore(canonical))
        {
            matchedSubtree = canonical;
            return true;
        }

        var queue = new Queue<string>();
        queue.Enqueue(canonical);
        int examined = 0;

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue; // unreadable directory: cannot prove it is safe, but cannot prove otherwise either
            }

            foreach (string child in children)
            {
                if (++examined > ProtectedDescendantScanCap)
                {
                    // Fail closed: an incomplete scan must never authorise a recursive delete.
                    matchedSubtree = current;
                    return true;
                }

                try
                {
                    // Never traverse a link: a junction here could redirect the walk (and any
                    // later delete) outside the tree this guard is reasoning about.
                    if (PathSecurity.IsReparsePointOrLink(child)) continue;

                    string childCanonical = PathSecurity.NormalizeCanonicalPath(child);
                    if (string.IsNullOrEmpty(childCanonical)) continue;

                    string childLower = childCanonical.ToLowerInvariant();
                    if (IsAppDataStateCore(childLower))
                    {
                        matchedSubtree = childCanonical;
                        return true;
                    }

                    queue.Enqueue(childCanonical);
                }
                catch
                {
                    continue;
                }
            }
        }

        return false;
    }

    private static readonly string[] PackageAndScriptCachePathMarkers =
    [
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
        @"\code cache\",
        @"\gpucache\",
        @"\dawncache\",
        @"\scriptcache\",
        @"\shadercache\",
        @"\appdata\local\temp\",
        @"\windows\temp\",
        @"\local\temp\",
        @"\inetcache\",
        @"\temporary internet files\",
        @"\deliveryoptimization\",
        @"\softwaredistribution\download\",
        @"\softwaredistribution\",
        @"\webcache\",
        @"\cache_data\",
        @"\cache2\entries\",
        @"\cache2\",
        @"\crashdumps\",
        @"\crashpad\",
        @"\dumps\",
        @"\logs\",
        @"\logfiles\",
        @"\temp\",
        @"\tmp\",
        @"\nvidia corporation\installer2\",
        @"\nvidia corporation\downloader\",
        @"\nvidia app\",
        @"\nvidia\",
        @"\amd\",
        @"\intel\",
        @"\realtek\",
        @"\driverstore\temp\",
        @"\driverstore\",
        @"\driverstate\",
        @"\ebwebview\",
        @"\cef\",
        @"\chromium\",
        @"\spotify\",
        @"\discord\",
        @"\slack\",
        @"\packages\",
        @"\tempstate\",
        @"\localcache\",
        @"\d3dscache\",
        @"\dxcache\",
        @"\steamapps\downloading\",
        @"\steamapps\temp\",
        @"\steamapps\shadercache\"
    ];

    public static bool IsPackageOrScriptCachePath(string pathLower)
    {
        ArgumentNullException.ThrowIfNull(pathLower);

        foreach (var marker in PackageAndScriptCachePathMarkers)
        {
            if (pathLower.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Also recognize standard cache or temporary folder segments
        if (pathLower.Contains(@"\cache\") || pathLower.Contains(@"\caches\") ||
            pathLower.Contains(@"\temp\") || pathLower.Contains(@"\tmp\") ||
            pathLower.Contains(@"\logs\") || pathLower.Contains(@"\webcache\"))
        {
            return true;
        }

        return false;
    }
}
