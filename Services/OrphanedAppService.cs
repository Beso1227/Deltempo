using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class OrphanedAppService
{
    /// <summary>
    /// Lowest active-app keyword count that indicates the harvest actually worked. The set is
    /// seeded from a static vendor whitelist, then augmented with every running process name and
    /// shortcut target on the machine. Even a freshly installed Windows image reports several
    /// hundred (svchost, dwm, explorer, RuntimeBroker, SearchHost, plus hundreds of services), so
    /// a count far below this means every live source was blocked rather than that the machine is
    /// genuinely empty.
    /// </summary>
    private const int MinimumPlausibleActiveAppCount = 32;

    // Strict whitelist of Windows system components, runtimes, drivers, hardware vendors, and core tools
    private static readonly HashSet<string> ProtectedSystemFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows & OS core components
        "Windows", "Windows NT", "Windows Mail", "Windows Media Player", "Windows Defender",
        "Windows Defender Advanced Threat Protection", "Windows Security", "WindowsPowerShell",
        "Windows Photo Viewer", "Windows Sidebar", "WindowsApps", "Common Files", "Internet Explorer",
        "Microsoft", "Microsoft.NET", "dotnet", "Reference Assemblies", "MSBuild", "PackageManagement",
        "ModifiableWindowsApps", "InstallShield Installation Information", "Uninstall Information",
        "Package Cache", "Packages", "USOShared", "USOPrivate", "SoftwareDistribution", "ssh",
        "System Volume Information", "$Recycle.Bin", "VirtualStore", "ConnectedDevicesPlatform",
        "D3DSCache", "Publishers", "PlaceholderTileLogoFolder", "CrashDumps", "Temp", "Programs",
        "Application Data", "Documents", "Start Menu", "Desktop", "Common",

        // Windows platform services that keep state under the user profile (Comms holds the
        // communications/backup service data; PeerDistRepub is the BITS/WSUS peer store).
        // Both are live OS infrastructure, not residue of an uninstalled app.
        "Comms", "PeerDistRepub", "PeerDist", "WSUS", "WindowsSelfHost",

        // Hardware, Drivers, Chipsets, GPU vendors
        "Intel", "NVIDIA", "NVIDIA Corporation", "AMD", "Realtek", "ASUS", "Dell", "HP", "Lenovo",
        "Logitech", "Corsair", "Razer", "SteelSeries", "Synaptics", "Dolby", "Broadcom", "Qualcomm",
        "Alps", "Apple", "Apple Computer",

        // Core Development Ecosystems, Runtimes & Platforms
        "Git", "nodejs", "Python", "PowerShell", "Docker", "DockerDesktop", "WSL", "vcpkg", "pip",
        "npm", "yarn", "NuGet", "Gradle", "Android", "Rust", "Go", "Java", "Oracle", "Steam",
        "Epic Games", "EpicGamesLauncher", "JetBrains", "Unity", "Spotify", "Discord", "Slack",
        "Telegram Desktop", "WhatsApp", "WhatsAppDesktop", "Signal", "Skype", "Viber", "Element", "LINE", "WeChat",
        "Mattermost", "Rocket.Chat", "RocketChat", "Cisco-Spark", "CiscoSparkLauncher", "Webex", "WebexTeams",
        "RingCentral", "Thunderbird", "Outlook", "Keybase", "Zulip", "Chime", "Flock", "Kakao", "KakaoTalk",
        "Messenger", "Session", "Threema", "Wire", "ICQ", "MSTeams", "Teams",
        "Code", "GitHubDesktop", "BraveSoftware", "Mozilla", "Zoom", "Notion",
        "Figma", "Cursor", "Windsurf", "Deltempo", "deltempo_cli"
    };

    /// <summary>
    /// Folder names that must never be proposed as orphaned app folders: package-manager stores,
    /// skill/plugin/MCP vocabulary, and user-data roots. These are live tool infrastructure or
    /// user content — not residue of an uninstalled application — so classification refuses them
    /// up front (fail-closed) instead of relying on age/size shields that lapse the moment a
    /// folder goes quiet.
    /// </summary>
    private static readonly HashSet<string> NeverProposedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Package-manager stores & language toolchains (shared by every installed tool)
        "npm-cache", "node-gyp", "node_modules", "corepack", "pnpm", "pnpm-store",
        "pip", "pipx", "pypa", "PSResourceGet", "uv", "scoop", "cargo", "NuGet", "yarn",
        "ms-playwright", "ms-playwright-go", "fontconfig", "kotlin", "Dart",

        // Portable CLI tool installs: registry-absent binaries that are infrastructure, not residue
        "gk", "csdevkit",

        // Skill / plugin / MCP / agent namespaces — the goal forbids deleting these outright
        "skills", "agents", "plugins", "commands", "mcp", "hooks", "workspace", "extensions",

        // User data roots that are never app residue
        "Backup", "Backups", "Saved Games", "My Games"
    };

    /// <summary>
    /// Folder-name prefixes identifying agent/tool namespaces regardless of suffix
    /// (claude*, gemini*, codex*, copilot*, ...). Structural, not machine-specific.
    /// </summary>
    private static readonly string[] ToolNamespacePrefixes =
    {
        "claude", "gemini", "codex", "copilot", "openai", "anthropic", "opencode", "hermes"
    };

    /// <summary>
    /// True when <paramref name="dirName"/> is a package-manager store, agent namespace, or
    /// skill/plugin/MCP vocabulary rather than an application's own folder. Fail-closed guard
    /// used by orphan classification and the residual-safety shield: these names are never
    /// proposed for deletion, regardless of age or size evidence.
    /// </summary>
    public static bool IsNeverProposedFolderName(string? dirName)
    {
        if (string.IsNullOrWhiteSpace(dirName)) return false;

        if (NeverProposedFolderNames.Contains(dirName)) return true;

        // Unix-style hidden tool config (.claude, .gemini, .codex, ...) and npm scope folders (@org/pkg).
        if (dirName.StartsWith(".", StringComparison.Ordinal)) return true;
        if (dirName.StartsWith("@", StringComparison.Ordinal)) return true;

        // Suffix conventions used by agent/CLI/extension installers.
        if (dirName.EndsWith("-updater", StringComparison.OrdinalIgnoreCase) ||
            dirName.EndsWith("-mcp", StringComparison.OrdinalIgnoreCase) ||
            dirName.EndsWith("-cli", StringComparison.OrdinalIgnoreCase) ||
            dirName.EndsWith("-extension", StringComparison.OrdinalIgnoreCase) ||
            dirName.EndsWith("-skills", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var prefix in ToolNamespacePrefixes)
        {
            if (dirName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Folder identity match: a candidate is live when it equals an installed identity or when
    /// EITHER side contains the other at a WORD BOUNDARY (both bounded to 4+ chars). The reverse
    /// direction covers versioned and suffixed identities — folder <c>PDFgear</c> vs
    /// <c>PDFgear 1.2.3</c>, folder <c>lean-ctx</c> vs <c>lean-ctx-bin</c>.
    /// </summary>
    /// <remarks>
    /// The word-boundary requirement is load-bearing, not cosmetic. Containment was originally a
    /// plain substring test, and the keyword set is populated from running process names, so any
    /// short generic process ("tool", "setup", "update") silently marked EVERY folder containing
    /// that text as an active install. On a machine with a process named <c>tool</c>, the orphan
    /// <c>MiniTool GA Uploader</c> matched on the substring "Tool" and no residue was ever
    /// proposed — from any scan root. Requiring the match to start at a word boundary keeps real
    /// matches ("Snipping Tool" vs "Tool") while rejecting infix ones ("MiniTool" vs "Tool").
    /// </remarks>
    internal static bool IsActiveDirectory(string dirName, IEnumerable<string> activeApps)
    {
        if (string.IsNullOrWhiteSpace(dirName) || activeApps == null) return false;

        foreach (var app in activeApps)
        {
            if (string.IsNullOrWhiteSpace(app)) continue;

            if (app.Equals(dirName, StringComparison.OrdinalIgnoreCase)) return true;
            if (app.Length >= 4 && ContainsAtWordBoundary(dirName, app)) return true;
            if (dirName.Length >= 4 && ContainsAtWordBoundary(app, dirName)) return true;
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="needle"/> occurs inside <paramref name="haystack"/> starting at a
    /// word boundary (string start, or preceded by a non-alphanumeric separator). Case-insensitive.
    /// </summary>
    private static bool ContainsAtWordBoundary(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;

        int index = haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            if (index == 0) return true;

            char preceding = haystack[index - 1];
            if (!char.IsLetterOrDigit(preceding)) return true;

            index = haystack.IndexOf(needle, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }


    public static HashSet<string> GetComprehensiveActiveAppKeywords()
    {
        var activeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add Known Protected Vendors
        foreach (var vendor in ProtectedSystemFolderNames)
        {
            activeKeywords.Add(vendor);
        }

        // 2. Scan Running Processes
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrEmpty(proc.ProcessName))
                    {
                        activeKeywords.Add(proc.ProcessName);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        // 3. Scan Start Menu & Desktop Shortcuts and Portable Executables
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] shortcutRoots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "OneDrive", "Desktop")
        };

        foreach (var smPath in shortcutRoots)
        {
            if (Directory.Exists(smPath))
            {
                try
                {
                    var opt = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
                    var dirInfo = new DirectoryInfo(smPath);
                    foreach (var file in dirInfo.EnumerateFiles("*", opt))
                    {
                        string ext = file.Extension.ToLowerInvariant();
                        if (ext == ".lnk" || ext == ".exe" || ext == ".url")
                        {
                            var name = Path.GetFileNameWithoutExtension(file.Name);
                            activeKeywords.Add(name);
                            foreach (var token in name.Split(' ', '-', '_', '.'))
                            {
                                if (token.Length >= 3) activeKeywords.Add(token);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
            }
        }

        // 4. Scan Registry Uninstall Entries (Both 64-bit and 32-bit WOW64)
        string[] registryRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var rootKey in registryRoots)
        {
            try
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(rootKey);
                ExtractNamesFromKey(hklmKey, activeKeywords);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }

            try
            {
                using var hkcuKey = Registry.CurrentUser.OpenSubKey(rootKey);
                ExtractNamesFromKey(hkcuKey, activeKeywords);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 5. Scan Registry App Paths
        string[] appPathRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
        };

        foreach (var apRoot in appPathRoots)
        {
            try
            {
                using var apKey = Registry.LocalMachine.OpenSubKey(apRoot);
                if (apKey != null)
                {
                    foreach (var sub in apKey.GetSubKeyNames())
                    {
                        string cleanSub = Path.GetFileNameWithoutExtension(sub);
                        if (!string.IsNullOrEmpty(cleanSub)) activeKeywords.Add(cleanSub);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 6. Scan Active Windows Services
        try
        {
            using var srvKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (srvKey != null)
            {
                foreach (var sName in srvKey.GetSubKeyNames())
                {
                    try
                    {
                        using var sSub = srvKey.OpenSubKey(sName);
                        var imgPath = sSub?.GetValue("ImagePath")?.ToString();
                        if (!string.IsNullOrEmpty(imgPath))
                        {
                            var clean = imgPath.Trim('"', ' ');
                            var fName = Path.GetFileNameWithoutExtension(clean);
                            if (!string.IsNullOrEmpty(fName)) activeKeywords.Add(fName);
                            var dName = Path.GetFileName(Path.GetDirectoryName(clean) ?? "");
                            if (!string.IsNullOrEmpty(dName)) activeKeywords.Add(dName);
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        // 7. Scan Modern Windows Store / AppX Packages
        try
        {
            const string appModelKeyPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            using var baseKey = Registry.CurrentUser.OpenSubKey(appModelKeyPath);
            if (baseKey != null)
            {
                foreach (var pkgName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var pkgKey = baseKey.OpenSubKey(pkgName);
                        if (pkgKey == null) continue;

                        string displayName = pkgKey.GetValue("DisplayName")?.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(displayName) && !displayName.StartsWith("@{") && !displayName.StartsWith("ms-resource:"))
                        {
                            activeKeywords.Add(displayName);
                            foreach (var token in displayName.Split(' ', '-', '_', '.'))
                            {
                                if (token.Length >= 3) activeKeywords.Add(token);
                            }
                        }

                        int underscoreIdx = pkgName.IndexOf('_');
                        if (underscoreIdx > 0)
                        {
                            string prefix = pkgName.Substring(0, underscoreIdx);
                            activeKeywords.Add(prefix);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // 8. Package-managed & portable tool identities (npm/pip/cargo/scoop/winget/PATH).
        //    Registry absence is not evidence of uninstall for these tools: a live uv, lean-ctx,
        //    or npm-global folder would otherwise classify as an orphan once its age shield lapses.
        AddInstalledToolIdentities(activeKeywords);

        return activeKeywords;
    }

    /// <summary>
    /// Adds folder/exe identities for installed tooling that never appears in the registry:
    /// npm/pnpm global packages, pip user scripts, cargo binaries, scoop apps, winget links,
    /// and PATH-resolved executables.
    /// </summary>
    private static void AddInstalledToolIdentities(HashSet<string> keywords)
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // npm global packages and their command shims (%APPDATA%\npm)
        string npmPrefix = Path.Combine(appData, "npm");
        AddChildDirectoryNames(keywords, Path.Combine(npmPrefix, "node_modules"), includeScopeChildren: true);
        AddFileBaseNames(keywords, npmPrefix, "*.cmd");
        AddFileBaseNames(keywords, npmPrefix, "*.ps1");

        // pip user scripts (<PythonRoot>\Scripts) and pipx virtualenvs
        foreach (string pyRoot in new[]
                 {
                     Path.Combine(appData, "Python"),
                     Path.Combine(localAppData, "Programs", "Python")
                 })
        {
            try
            {
                if (!Directory.Exists(pyRoot)) continue;
                foreach (var ver in Directory.EnumerateDirectories(pyRoot))
                {
                    AddFileBaseNames(keywords, Path.Combine(ver, "Scripts"), "*.exe");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }
        AddChildDirectoryNames(keywords, Path.Combine(localAppData, "pipx", "venvs"));

        // cargo installed binaries + scoop application installs
        AddFileBaseNames(keywords, Path.Combine(profile, ".cargo", "bin"), "*.exe");
        AddChildDirectoryNames(keywords, Path.Combine(profile, "scoop", "apps"));

        // winget app execution links + portable package dirs
        AddFileBaseNames(keywords, Path.Combine(localAppData, "Microsoft", "WinGet", "Links"), "*.exe");
        AddChildDirectoryNames(keywords, Path.Combine(localAppData, "Microsoft", "WinGet", "Packages"));

        // PATH-resolved executables (portable CLIs live here), excluding Windows system dirs
        AddPathExecutableIdentities(keywords);
    }

    private static void AddChildDirectoryNames(HashSet<string> keywords, string root, bool includeScopeChildren = false)
    {
        try
        {
            if (!Directory.Exists(root)) return;
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var d in new DirectoryInfo(root).EnumerateDirectories("*", opt))
            {
                keywords.Add(d.Name);

                // npm scope folders (@org/pkg): identity lives in the scoped package name.
                if (includeScopeChildren && d.Name.StartsWith("@", StringComparison.Ordinal))
                {
                    foreach (var child in d.EnumerateDirectories("*", opt))
                    {
                        keywords.Add(child.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void AddFileBaseNames(HashSet<string> keywords, string root, string pattern)
    {
        try
        {
            if (!Directory.Exists(root)) return;
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                // Include reparse-point files: winget app execution aliases are links, and only
                // their NAMES are consumed here — enumeration never descends into a link.
                AttributesToSkip = 0
            };

            foreach (var f in new DirectoryInfo(root).EnumerateFiles(pattern, opt))
            {
                string name = Path.GetFileNameWithoutExtension(f.Name);
                if (!string.IsNullOrWhiteSpace(name)) keywords.Add(name);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void AddPathExecutableIdentities(HashSet<string> keywords)
    {
        try
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            int entries = 0;
            foreach (string raw in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (++entries > 200) break;

                string dir = raw.Trim().Trim('"');
                if (dir.Length == 0) continue;

                // System directories drown the set with generic names and are already covered
                // by ProtectedSystemFolderNames.
                if (dir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Directory.Exists(dir)) continue;

                string leaf = Path.GetFileName(dir.TrimEnd('\\', '/'));
                if (!string.IsNullOrWhiteSpace(leaf)) keywords.Add(leaf);

                AddFileBaseNames(keywords, dir, "*.exe");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }


    private static void ExtractNamesFromKey(RegistryKey? key, HashSet<string> keywords)
    {
        if (key == null) return;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            try
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(displayName))
                {
                    keywords.Add(displayName);
                    foreach (var word in displayName.Split(' ', '-', '_', '.'))
                    {
                        if (word.Length >= 3) keywords.Add(word);
                    }
                }

                var installLocation = subKey.GetValue("InstallLocation")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(installLocation))
                {
                    var dirName = Path.GetFileName(installLocation.TrimEnd('\\'));
                    if (!string.IsNullOrEmpty(dirName)) keywords.Add(dirName);
                }

                var uninstallString = subKey.GetValue("UninstallString")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(uninstallString))
                {
                    var clean = uninstallString.Trim('"', ' ');
                    var dirName = Path.GetFileName(Path.GetDirectoryName(clean) ?? "");
                    if (!string.IsNullOrEmpty(dirName)) keywords.Add(dirName);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Scans candidate system roots for verified orphaned application remnants across multi-level directory trees.
    /// Features adaptive modification age shielding, structural residual analysis, and 100% fail-closed safety.
    /// </summary>
    public static List<TargetFolderInfo> ScanVerifiedOrphanedFolders()
    {
        var orphans = new List<TargetFolderInfo>();
        var activeApps = GetComprehensiveActiveAppKeywords();

        // Degraded-evidence guard. A folder is only "orphaned" if nothing points at it, and proving
        // that requires knowing what is installed and running. If the keyword harvest came back
        // implausibly small, every source failed (restricted registry, blocked process enumeration,
        // hardened host) and this scan cannot tell residue from live software. Proposing nothing is
        // the correct outcome: under-reporting costs the user a manual pass, whereas over-reporting
        // puts installed applications onto a deletion list.
        if (activeApps.Count < MinimumPlausibleActiveAppCount)
        {
            Trace.WriteLine(
                $"[Deltempo] Orphan scan skipped: only {activeApps.Count} active-app keywords gathered " +
                $"(minimum {MinimumPlausibleActiveAppCount}); evidence collection appears degraded.");
            return orphans;
        }

        // 1. Program Files, Program Files (x86), ProgramData & Local Programs
        string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

        ScanDir(pf64, activeApps, orphans, isProgramFiles: true, maxDepth: 1);
        if (!string.Equals(pf64, pf86, StringComparison.OrdinalIgnoreCase))
        {
            ScanDir(pf86, activeApps, orphans, isProgramFiles: true, maxDepth: 1);
        }
        ScanDir(progData, activeApps, orphans, isProgramFiles: true, maxDepth: 1);
        ScanDir(localPrograms, activeApps, orphans, isProgramFiles: true, maxDepth: 1);

        // 2. User AppData (Local, Roaming, LocalLow)
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");

        ScanDir(localAppData, activeApps, orphans, isProgramFiles: false, maxDepth: 1);
        ScanDir(roamingAppData, activeApps, orphans, isProgramFiles: false, maxDepth: 1);
        ScanDir(localLow, activeApps, orphans, isProgramFiles: false, maxDepth: 1);

        // 3. User State Roots (.config, .cache, Saved Games)
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dotConfig = Path.Combine(userProfile, ".config");
        string dotCache = Path.Combine(userProfile, ".cache");
        string savedGames = Path.Combine(userProfile, "Saved Games");

        ScanDir(dotConfig, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(dotCache, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(savedGames, activeApps, orphans, isProgramFiles: false, maxDepth: 0);

        // 4. VirtualStore Remnants
        string vsPf = Path.Combine(localAppData, "VirtualStore", "Program Files");
        string vsPf86 = Path.Combine(localAppData, "VirtualStore", "Program Files (x86)");
        string vsProgData = Path.Combine(localAppData, "VirtualStore", "ProgramData");

        ScanDir(vsPf, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(vsPf86, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        ScanDir(vsProgData, activeApps, orphans, isProgramFiles: false, maxDepth: 0);

        // 5. Downloaded Installation Caches
        string dlInstProgData = Path.Combine(progData, "Downloaded Installations");
        string dlInstLocal = Path.Combine(localAppData, "Downloaded Installations");

        ScanDir(dlInstProgData, activeApps, orphans, isProgramFiles: true, maxDepth: 0);
        ScanDir(dlInstLocal, activeApps, orphans, isProgramFiles: false, maxDepth: 0);

        // 6. Machine-wide Public profile root.
        //
        // Some applications park their entire per-user data folder in C:\Users\Public (for
        // example an Electron/WebView2 app writing its Chromium profile there). When the app is
        // uninstalled, that folder is pure residue and can run to hundreds of MB of cache.
        //
        // This deliberately scans ONLY the Public root at depth 0, never a recursive walk, and
        // relies on the existing refusal chain for the shared libraries sitting beside it:
        //   * Safety Rule 1b refuses package-manager / agent namespace names outright.
        //   * Safety Rule 6 refuses user data and working roots. OrphanEvidenceClassifier's
        //     PublicSharedRoots() covers Documents, Desktop, Downloads, Pictures, Music, Videos,
        //     Libraries and AccountPictures, so shared user content is never proposed.
        //   * Safety Rule 7 requires positive proof of no live reference (process, service,
        //     startup entry, uninstall record, shortcut target or PATH entry) before a folder
        //     can be proposed at all.
        string? publicRoot = ResolvePublicProfileRoot();
        if (!string.IsNullOrEmpty(publicRoot))
        {
            ScanDir(publicRoot, activeApps, orphans, isProgramFiles: false, maxDepth: 0);
        }

        return orphans;
    }

    /// <summary>
    /// The machine-wide Public profile root, derived from the Common Documents location so it
    /// resolves even when the profile is not located at the conventional C:\Users\Public.
    /// Returns null when the location cannot be determined.
    /// </summary>
    private static string? ResolvePublicProfileRoot()
    {
        string commonDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        if (string.IsNullOrEmpty(commonDocs)) return null;

        string? root = Path.GetDirectoryName(commonDocs);
        return string.IsNullOrEmpty(root) ? null : root;
    }

    private static void ScanDir(string baseDir, HashSet<string> activeApps, List<TargetFolderInfo> orphans, bool isProgramFiles, int maxDepth)
    {
        if (!Directory.Exists(baseDir)) return;

        try
        {
            var dirInfoBase = new DirectoryInfo(baseDir);
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var dir in dirInfoBase.EnumerateDirectories("*", opt))
            {
                ProcessDirectoryCandidate(dir, activeApps, orphans, isProgramFiles, depth: 0, maxDepth: maxDepth);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void ProcessDirectoryCandidate(
        DirectoryInfo dir,
        HashSet<string> activeApps,
        List<TargetFolderInfo> orphans,
        bool isProgramFiles,
        int depth,
        int maxDepth)
    {
        var dirName = dir.Name;

        // Safety Rule 1: Protected system names and hardware vendors
        if (ProtectedSystemFolderNames.Contains(dirName)) return;

        // Safety Rule 1b: package-manager stores, agent namespaces, and skill/plugin/MCP vocabulary
        // are never orphan candidates. These hold live shared tool state — deleting one breaks
        // every installed tool that uses it — so they are refused before any age/size evidence
        // is even considered.
        if (IsNeverProposedFolderName(dirName)) return;

        // Safety Rule 2: Explicit system component blocks
        if (dirName.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            dirName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            dir.FullName.Contains("Common Files", StringComparison.OrdinalIgnoreCase) ||
            dir.FullName.Contains("Package Cache", StringComparison.OrdinalIgnoreCase) ||
            dirName.StartsWith("regid.", StringComparison.OrdinalIgnoreCase) ||
            dirName.StartsWith("$", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Safety Rule 3: Reparse point (Junction/Symlink)
        if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) return;

        // Safety Rule 4: Critical folder shield
        if (!InstalledAppService.IsSafeToDeleteResidual(dir.FullName)) return;

        // Safety Rule 5: Match against active installed applications, running processes, services, shortcuts
        bool isActive = IsActiveDirectory(dirName, activeApps);

        if (isActive)
        {
            // If active at parent level, evaluate uninstalled sub-products (depth 1)
            if (depth < maxDepth)
            {
                try
                {
                    var opt = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };
                    foreach (var child in dir.EnumerateDirectories("*", opt))
                    {
                        ProcessDirectoryCandidate(child, activeApps, orphans, isProgramFiles, depth + 1, maxDepth);
                    }
                }
                catch { }
            }
            return;
        }

        // Safety Rule 6: User data and active working roots are never residue. Documents /
        // OneDrive / My Games / Saved Games hold user content, and a folder holding a project
        // manifest (.git, package.json, *.sln) is someone's active work — not an uninstalled
        // app's leftovers. Checked before any file enumeration so 100s of MB are never walked.
        if (OrphanEvidenceClassifier.IsUserDataOrWorkingRoot(dir.FullName)) return;

        // Safety Rule 7: Live-reference proof. A running process, registered service, startup
        // entry, uninstall record, shortcut target, or PATH entry that resolves inside this
        // folder means it is in use. This is the evidence that separates a quiet-but-live tool
        // folder (Antigravity IDE, Qoder, a *.cache tool store) from genuine residue, which
        // age and size alone cannot do.
        if (OrphanEvidenceClassifier.HasLiveReference(dir.FullName)) return;

        try
        {
            var subEnumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            bool hasUninstaller = false;
            bool hasBrokenUninstaller = false;
            int exeCount = 0;
            int dllCount = 0;
            long size = 0;
            int fileCount = 0;

            foreach (var f in dir.EnumerateFiles("*", subEnumOptions))
            {
                fileCount++;
                size += f.Length;

                string fl = f.Name.ToLowerInvariant();
                if (fl.EndsWith(".exe"))
                {
                    exeCount++;
                    if (fl.Contains("unins") || fl.Contains("uninstall") || fl.Contains("setup"))
                    {
                        hasUninstaller = true;
                        break;
                    }
                }
                else if (fl.EndsWith(".dat") && (fl.Contains("unins") || fl.Contains("uninstall")))
                {
                    hasBrokenUninstaller = true;
                }
                else if (fl.EndsWith(".dll") || fl.EndsWith(".sys"))
                {
                    dllCount++;
                }

                if (fileCount > 5000) break;
            }

            // If it has an active uninstaller or multiple functional executables, it's still an installed app!
            if (hasUninstaller || exeCount > 1) return;

            // Single exe: accept unbounded reverse containment (identity contains folder name) as
            // still-installed evidence. This intentionally has NO 4-char floor — a 2-3 char folder
            // ("vlc", "gk") holding exactly one executable and named inside an installed identity is
            // far more likely that app's own payload than an orphan. The bounded bidirectional match
            // in Safety Rule 5 only covers folder names of 4+ chars, so this remains reachable for
            // short names.
            if (exeCount == 1 && activeApps.Any(a => a.Contains(dirName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // Age shield, now anchored on evidence rather than silence:
            // - Broken uninstaller leftovers (unins*.dat without unins*.exe): 1 hour. This is
            //   POSITIVE evidence of a partial uninstall, so it may clear fast.
            // - Everything else: 7 days. Rules 6/7 already proved the folder is not user data and
            //   that nothing references it; a full quiet week is then the remaining bar. The old
            //   24-hour allowance for pure cache/config trees let a live tool nobody happened to
            //   touch over a weekend be proposed as "residual from uninstalled app".
            var age = DateTime.Now - dir.LastWriteTime;
            TimeSpan requiredAge = hasBrokenUninstaller
                ? TimeSpan.FromHours(1)
                : TimeSpan.FromDays(7);

            if (age < requiredAge) return;

            // In user AppData, require at least 1 KB or an empty directory
            if (!isProgramFiles && size < 1024 && fileCount > 0) return;

            // Prevent duplicate entries
            if (orphans.Any(o => string.Equals(o.FolderPath, dir.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string category = isProgramFiles ? "Program Files Residuals" : "App Leftovers";
            string desc = hasBrokenUninstaller
                ? $"Incomplete uninstallation residual in {dir.FullName}"
                : (exeCount == 0 && dllCount == 0)
                    ? $"Residual cache & configuration from uninstalled app in {dir.FullName}"
                    : $"Residual files from uninstalled application in {dir.FullName}";

            orphans.Add(new TargetFolderInfo
            {
                Id = $"Orphan_{SanitizeIdentifier(dir.FullName)}",
                Name = $"{dirName} (Residual Files)",
                Category = category,
                CategoryColor = "#F59E0B",
                SafetyBadge = "🛡️ Verified Leftover (Undoable)",
                SafetyBadgeColor = "#10B981",
                Description = desc,
                FolderPath = dir.FullName,
                IconGlyph = "\uE74D",
                SizeBytes = size,
                FileCount = fileCount,
                IsOrphanedAppFolder = true,
                RequiresAdmin = isProgramFiles,
                HasAccess = true,
                IsSelected = false // Unchecked by default for user safety and explicit consent
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static string SanitizeIdentifier(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Guid.NewGuid().ToString("N");
        return path.Replace('\\', '_')
                   .Replace('/', '_')
                   .Replace(':', '_')
                   .Replace(' ', '_');
    }
}
