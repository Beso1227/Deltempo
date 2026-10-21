using System.IO;
using Microsoft.Win32;

namespace WinTempCleaner.Core.Discovery;

/// <summary>
/// Proactively discovers game launcher libraries, development environment package caches,
/// and browser installations across all mounted logical drives on the host system.
/// </summary>
public static class MultiDriveDetector
{
    /// <summary>
    /// Returns all fixed and removable logical drive roots currently mounted and ready (e.g. "C:\", "D:\").
    /// </summary>
    public static List<string> GetReadyDriveRoots()
    {
        var roots = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                {
                    roots.Add(drive.RootDirectory.FullName);
                }
            }
        }
        catch
        {
            roots.Add("C:\\");
        }

        return roots.Count > 0 ? roots : new List<string> { "C:\\" };
    }

    /// <summary>
    /// Detects all Steam library directories across all drives by parsing steamapps\libraryfolders.vdf.
    /// </summary>
    public static List<string> DiscoverSteamLibraries()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Locate primary Steam install via Windows Registry
        string? steamInstallPath = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam") ??
                            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam") ??
                            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");

            steamInstallPath = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(steamInstallPath))
            {
                steamInstallPath = steamInstallPath.Replace('/', '\\');
            }
        }
        catch { }

        // Fallback default paths if registry missing
        if (string.IsNullOrEmpty(steamInstallPath) || !Directory.Exists(steamInstallPath))
        {
            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (Directory.Exists(defaultPath))
            {
                steamInstallPath = defaultPath;
            }
        }

        if (!string.IsNullOrEmpty(steamInstallPath) && Directory.Exists(steamInstallPath))
        {
            libraries.Add(steamInstallPath);

            // Parse libraryfolders.vdf
            string vdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                ParseSteamLibraryFoldersVdf(vdfPath, libraries);
            }
        }

        // 2. Drive-root probe fallback for common library patterns
        foreach (var root in GetReadyDriveRoots())
        {
            var candidate = Path.Combine(root, "SteamLibrary");
            if (Directory.Exists(candidate))
            {
                libraries.Add(candidate);
            }

            var candidate2 = Path.Combine(root, "Steam");
            if (Directory.Exists(candidate2))
            {
                libraries.Add(candidate2);
            }
        }

        return libraries.Where(Directory.Exists).ToList();
    }

    /// <summary>
    /// Simple parser extracting path values from Valve's KeyValues VDF file.
    /// </summary>
    private static void ParseSteamLibraryFoldersVdf(string vdfPath, HashSet<string> libraries)
    {
        try
        {
            foreach (var rawLine in File.ReadLines(vdfPath))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                {
                    // Format: "path" "D:\\SteamLibrary"
                    var tokens = line.Split('"', StringSplitOptions.TrimEntries)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    int pathIdx = tokens.FindIndex(t => string.Equals(t, "path", StringComparison.OrdinalIgnoreCase));
                    if (pathIdx >= 0 && pathIdx + 1 < tokens.Count)
                    {
                        string candidatePath = tokens[pathIdx + 1].Replace(@"\\", @"\");
                        if (!string.IsNullOrWhiteSpace(candidatePath) && Directory.Exists(candidatePath))
                        {
                            libraries.Add(candidatePath);
                        }
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Discovers developer cache directories respecting environment variables (CARGO_HOME, GRADLE_USER_HOME, etc.)
    /// </summary>
    public static List<string> DiscoverDevPackageCaches()
    {
        var dirs = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Rust Cargo
        var cargoHome = Environment.GetEnvironmentVariable("CARGO_HOME");
        var cargoCache = !string.IsNullOrEmpty(cargoHome)
            ? Path.Combine(cargoHome, "registry", "cache")
            : Path.Combine(userProfile, ".cargo", "registry", "cache");
        if (Directory.Exists(cargoCache)) dirs.Add(cargoCache);

        // Gradle
        var gradleHome = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        var gradleCaches = !string.IsNullOrEmpty(gradleHome)
            ? Path.Combine(gradleHome, "caches")
            : Path.Combine(userProfile, ".gradle", "caches");
        if (Directory.Exists(gradleCaches)) dirs.Add(gradleCaches);

        // Go build cache
        var goCache = Environment.GetEnvironmentVariable("GOCACHE");
        if (!string.IsNullOrEmpty(goCache) && Directory.Exists(goCache))
        {
            dirs.Add(goCache);
        }
        else
        {
            var defaultGo = Path.Combine(localAppData, "go-build");
            if (Directory.Exists(defaultGo)) dirs.Add(defaultGo);
        }

        // NuGet
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        var nugetDir = !string.IsNullOrEmpty(nugetPackages)
            ? nugetPackages
            : Path.Combine(userProfile, ".nuget", "packages");
        if (Directory.Exists(nugetDir)) dirs.Add(nugetDir);

        // npm
        var npmCache = Path.Combine(localAppData, "npm-cache");
        if (Directory.Exists(npmCache)) dirs.Add(npmCache);

        // Yarn
        var yarnCache = Path.Combine(localAppData, "Yarn", "Cache");
        if (Directory.Exists(yarnCache)) dirs.Add(yarnCache);

        // pnpm
        var pnpmStore = Path.Combine(localAppData, "pnpm", "store");
        if (Directory.Exists(pnpmStore)) dirs.Add(pnpmStore);

        // Bun
        var bunCache = Path.Combine(userProfile, ".bun", "install", "cache");
        if (Directory.Exists(bunCache)) dirs.Add(bunCache);

        // Pip
        var pipCache = Path.Combine(localAppData, "pip", "cache");
        if (Directory.Exists(pipCache)) dirs.Add(pipCache);

        return dirs;
    }
}
