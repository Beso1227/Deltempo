using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class SystemCacheResolver
{
    public static List<string> ResolveUpgradeLeftovers()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var rootDrive = Path.GetPathRoot(winDir) ?? @"C:\";
        return new List<string>
        {
            Path.Combine(rootDrive, "$WINDOWS.~BT"),
            Path.Combine(rootDrive, "$WINDOWS.~WS"),
            Path.Combine(rootDrive, "$WinREAgent", "Scratch"),
            Path.Combine(rootDrive, "ESD"),
            Path.Combine(rootDrive, "ESD", "Download"),
            Path.Combine(rootDrive, "Windows.old"),
            Path.Combine(rootDrive, "$SysReset"),
            Path.Combine(rootDrive, "$GetCurrent"),
            Path.Combine(winDir, "Panther"),
            Path.Combine(winDir, "Logs", "MoSetup"),
            Path.Combine(winDir, "System32", "Sysprep", "Panther")
        };
    }

    public static List<string> ResolveComponentCaches()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new List<string>
        {
            Path.Combine(winDir, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "FontCache"),
            Path.Combine(winDir, "SystemTemp"),
            Path.Combine(winDir, "Downloaded Program Files"),
            Path.Combine(winDir, "SoftwareDistribution", "ScanFile"),
            Path.Combine(winDir, "SoftwareDistribution", "DataStore", "Logs"),
            Path.Combine(winDir, "SoftwareDistribution", "PostRebootEventCache"),
            Path.Combine(winDir, "SoftwareDistribution", "AuthCabs"),
            Path.Combine(winDir, "Installer", "$PatchCache$"),
            Path.Combine(progData, "Package Cache"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "PeerDistPub"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "PeerDistSub"),
            Path.Combine(localAppData, "FontCache")
        };
    }

    public static List<string> ResolveDeviceDriverDirectories()
    {
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var rootDrive = Path.GetPathRoot(winDir) ?? @"C:\";

        var fixedDrives = new List<string> { rootDrive };
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName);
            foreach (var d in drives)
            {
                if (!fixedDrives.Contains(d, StringComparer.OrdinalIgnoreCase))
                {
                    fixedDrives.Add(d);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Drive enumeration warning: {ex.Message}");
        }

        var dirs = new List<string>
        {
            // NVIDIA installer packages, OTA framework and staging
            Path.Combine(progFiles, "NVIDIA Corporation", "Installer2"),
            Path.Combine(progFilesX86, "NVIDIA Corporation", "Installer2"),
            Path.Combine(progData, "NVIDIA Corporation", "NVIDIA App", "UpdateFramework", "ota-artifacts"),
            Path.Combine(progData, "NVIDIA Corporation", "NVIDIA App", "Installer"),
            Path.Combine(progData, "NVIDIA Corporation", "Downloader"),
            Path.Combine(progData, "NVIDIA Corporation", "GeForce Experience", "Download"),
            Path.Combine(progData, "NVIDIA", "Updates"),
            Path.Combine(progData, "NVIDIA", "DisplayDriver"),
            Path.Combine(progData, "NVIDIA Corporation", "NetService"),
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),

            // AMD packages, installers, DVR and shader caches
            Path.Combine(progData, "AMD", "Packages"),
            Path.Combine(progData, "AMD", "DVR"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "AMD", "DVR"),

            // Intel GFX installers, package caches, DSA downloads and logs
            Path.Combine(progData, "Intel", "Package Cache"),
            Path.Combine(progData, "Intel", "Logs"),
            Path.Combine(progData, "Intel", "DSA"),
            Path.Combine(progData, "Intel", "Installer"),
            Path.Combine(localAppData, "Intel", "DSA"),

            // Windows System32 DriverStore Temp & Staging
            Path.Combine(winDir, "System32", "DriverStore", "Temp"),
            Path.Combine(winDir, "System32", "DriverState")
        };

        // Driver unpack locations across all fixed drives (e.g. C:\NVIDIA, D:\AMD, C:\Intel)
        foreach (var drive in fixedDrives)
        {
            dirs.Add(Path.Combine(drive, "NVIDIA", "DisplayDriver"));
            dirs.Add(Path.Combine(drive, "NVIDIA"));
            dirs.Add(Path.Combine(drive, "AMD", "Packages"));
            dirs.Add(Path.Combine(drive, "AMD", "AMD_Radeon_Software_Installer"));
            dirs.Add(Path.Combine(drive, "AMD"));
            dirs.Add(Path.Combine(drive, "Intel", "GFX"));
            dirs.Add(Path.Combine(drive, "Intel", "Logs"));
            dirs.Add(Path.Combine(drive, "Intel"));
            dirs.Add(Path.Combine(drive, "Realtek"));
        }

        return dirs.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> ResolveDefenderDirectories()
    {
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new List<string>
        {
            Path.Combine(progData, "Microsoft", "Windows Defender", "Support"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Definition Updates", "Backup"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Quick"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Resource"),
            Path.Combine(progData, "Microsoft", "Windows Defender", "Scans", "History", "Store")
        };
    }

    public static List<string> ResolveWinSystemLogDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new List<string>
        {
            Path.Combine(winDir, "Logs"),
            Path.Combine(winDir, "Logs", "CBS"),
            Path.Combine(winDir, "Logs", "DISM"),
            Path.Combine(winDir, "Logs", "NetSetup"),
            Path.Combine(winDir, "Logs", "WindowsUpdate"),
            Path.Combine(winDir, "Logs", "MoSetup"),
            Path.Combine(winDir, "Panther"),
            Path.Combine(winDir, "Debug"),
            Path.Combine(winDir, "System32", "LogFiles"),
            Path.Combine(winDir, "tracing"),
            Path.Combine(progData, "Microsoft", "Diagnosis", "ETLLogs"),
            Path.Combine(progData, "Microsoft", "Diagnosis", "SoftLanding")
        };
    }

    public static List<string> ResolveSystemDumpDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return new List<string>
        {
            Path.Combine(winDir, "Minidump"),
            Path.Combine(winDir, "MEMORY.DMP"),
            Path.Combine(winDir, "LiveKernelReports"),
            Path.Combine(localAppData, "CrashDumps"),
            Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive"),
            Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue"),
            Path.Combine(progData, "Microsoft", "Windows", "WER", "Temp")
        };
    }

    public static List<string> ResolveTemporaryInternetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return new List<string>
        {
            Path.Combine(localAppData, "Microsoft", "Windows", "INetCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WebCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WebCache.old"),
            Path.Combine(winDir, "ServiceProfiles", "LocalService", "AppData", "Local", "Microsoft", "Windows", "INetCache"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "INetCache"),
            Path.Combine(localAppData, "Microsoft", "Windows", "IEDownloadHistory")
        };
    }

    public static List<string> ResolveGpuShaderDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var rootDrive = Path.GetPathRoot(winDir) ?? @"C:\";
        var localLow = Path.Combine(userProfile, "AppData", "LocalLow");

        return new List<string>
        {
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localLow, "NVIDIA", "PerDriverVersion", "DXCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "AMD", "GLCache"),
            Path.Combine(localLow, "AMD", "DxCache"),
            Path.Combine(localAppData, "Intel", "ShaderCache"),
            Path.Combine(localAppData, "Intel", "D3DSCache"),
            Path.Combine(rootDrive, "ProgramData", "NVIDIA Corporation", "NV_Cache")
        };
    }

    public static List<string> ResolveGamingLauncherDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        return new List<string>
        {
            Path.Combine(progFilesX86, "Steam", "steamapps", "downloading"),
            Path.Combine(progFilesX86, "Steam", "steamapps", "temp"),
            Path.Combine(progFilesX86, "Steam", "steamapps", "shadercache"),
            Path.Combine(progFilesX86, "Steam", "logs"),
            Path.Combine(progFilesX86, "Steam", "dump"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache_4430"),
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Logs"),
            Path.Combine(progData, "Battle.net", "Agent", "data", "cache"),
            Path.Combine(localAppData, "Battle.net", "Logs"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "Logs"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "DownloaderErrorLogs"),
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "UserConfig"),
            Path.Combine(localAppData, "Riot Games", "Riot Client", "Logs"),
            Path.Combine(localAppData, "Riot Games", "Install Riot Client", "Logs"),
            Path.Combine(localAppData, "Roblox", "logs"),
            Path.Combine(localAppData, "GOG.com", "Galaxy", "logs"),
            Path.Combine(progData, "GOG.com", "Galaxy", "webcache"),
            Path.Combine(roamingAppData, "Ubisoft Game Launcher", "logs"),
            Path.Combine(localAppData, "Ubisoft Game Launcher", "spool")
        };
    }

    public static List<string> ResolveMediaCreatorDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new List<string>
        {
            Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache Files"),
            Path.Combine(roamingAppData, "Adobe", "Common", "Media Cache"),
            Path.Combine(roamingAppData, "Adobe", "Common", "Peak Files"),
            Path.Combine(localAppData, "Adobe", "After Effects", "Disk Cache"),
            Path.Combine(localAppData, "Adobe", "Photoshop", "Scratch"),
            Path.Combine(localAppData, "CapCut", "User Data", "Cache"),
            Path.Combine(localAppData, "CapCut", "User Data", "Temp"),
            Path.Combine(localAppData, "Blackmagic Design", "DaVinci Resolve", "Support", "ProxyMedia"),
            Path.Combine(roamingAppData, "obs-studio", "logs"),
            Path.Combine(roamingAppData, "obs-studio", "crashes"),
            Path.Combine(localAppData, "Blender Foundation", "Blender", "temp"),
            Path.Combine(localAppData, "Audacity", "SessionData"),
            Path.Combine(userProfile, ".thumbnails")
        };
    }

    public static List<string> ResolveMobileDevDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new List<string>
        {
            Path.Combine(roamingAppData, "Apple Computer", "iTunes", "iPad Software Updates"),
            Path.Combine(roamingAppData, "Apple Computer", "iTunes", "iPhone Software Updates"),
            Path.Combine(roamingAppData, "Apple Computer", "iTunes", "iPod Software Updates"),
            Path.Combine(localAppData, "Apple Computer", "iTunes", "SubscriptionCache"),
            Path.Combine(userProfile, ".android", "avd", "cache"),
            Path.Combine(localAppData, "Android", "Sdk", "system-images", "cache"),
            Path.Combine(userProfile, ".gradle", "daemon"),
            Path.Combine(userProfile, ".gradle", "workers"),
            Path.Combine(userProfile, ".cargo", "registry", "src"),
            Path.Combine(userProfile, ".rustup", "downloads"),
            Path.Combine(localAppData, "flutter", "cache"),
            Path.Combine(localAppData, "Pub", "Cache")
        };
    }

    public static List<string> ResolveDeliveryOptimizationDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new List<string>
        {
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
            Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Logs"),
            Path.Combine(winDir, "SoftwareDistribution", "DeliveryOptimization"),
            Path.Combine(progData, "Microsoft", "Network", "Downloader"),
            Path.Combine(localAppData, "Microsoft", "Windows", "DeliveryOptimization")
        };
    }

    /// <summary>
    /// Absolute path to the machine-wide Public profile's Temp folder, derived from the Common
    /// Documents location so it resolves even when the profile is not at C:\Users\Public.
    /// Returns an empty string when it cannot be determined, which every caller already treats
    /// as "no such directory" and skips.
    /// </summary>
    private static string PublicProfileTempDirectory()
    {
        string commonDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        if (string.IsNullOrEmpty(commonDocs)) return string.Empty;

        string? publicRoot = Path.GetDirectoryName(commonDocs);
        return string.IsNullOrEmpty(publicRoot) ? string.Empty : Path.Combine(publicRoot, "Temp");
    }

    public static List<string> ResolveAppCacheDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var dirs = new List<string>
        {
            // Machine-wide Public profile scratch space. Public\Temp is shared by every
            // account on the machine and holds only transient app files, so it is disposable.
            // The Public profile's shared libraries (Documents, Desktop, Downloads, Pictures,
            // Music, Videos, Libraries, AccountPictures) are deliberately NOT listed: those hold
            // files users place there on purpose, and ProtectionPolicy refuses them outright.
            PublicProfileTempDirectory(),

            Path.Combine(localAppData, "Spotify", "Data"),
            Path.Combine(localAppData, "Spotify", "Storage"),
            Path.Combine(localAppData, "Spotify", "Browser", "Cache"),
            Path.Combine(localAppData, "Spotify", "Browser", "GPUCache"),
            Path.Combine(roamingAppData, "Spotify", "Users"),
            Path.Combine(roamingAppData, "Code", "Cache"),
            Path.Combine(roamingAppData, "Code", "CachedData"),
            Path.Combine(roamingAppData, "Code", "CachedExtensions"),
            Path.Combine(roamingAppData, "Code", "GPUCache"),
            Path.Combine(roamingAppData, "Code", "Code Cache"),
            Path.Combine(roamingAppData, "Code", "logs"),
            Path.Combine(roamingAppData, "Cursor", "Cache"),
            Path.Combine(roamingAppData, "Cursor", "CachedData"),
            Path.Combine(roamingAppData, "Cursor", "GPUCache"),
            Path.Combine(roamingAppData, "Cursor", "Code Cache"),
            Path.Combine(roamingAppData, "Windsurf", "Cache"),
            Path.Combine(roamingAppData, "Windsurf", "GPUCache"),
            Path.Combine(roamingAppData, "Notion", "Cache"),
            Path.Combine(roamingAppData, "Notion", "GPUCache"),
            Path.Combine(roamingAppData, "Notion", "Code Cache"),
            Path.Combine(roamingAppData, "Slack", "Cache"),
            Path.Combine(roamingAppData, "Slack", "Code Cache"),
            Path.Combine(roamingAppData, "Microsoft", "Teams", "Cache"),
            Path.Combine(roamingAppData, "Microsoft", "Teams", "blob_storage"),
            Path.Combine(localAppData, "CapCut", "User Data", "Cache"),
            Path.Combine(localAppData, "Docker", "log"),
            Path.Combine(localAppData, "Docker", "tmp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DockerDesktop", "tmp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wsl", "logs")
        };

        var jbRoot = Path.Combine(localAppData, "JetBrains");
        if (Directory.Exists(jbRoot))
        {
            try
            {
                foreach (var ideDir in Directory.EnumerateDirectories(jbRoot))
                {
                    dirs.Add(Path.Combine(ideDir, "caches"));
                    dirs.Add(Path.Combine(ideDir, "log"));
                    dirs.Add(Path.Combine(ideDir, "tmp"));
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
