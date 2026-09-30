using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using WinTempCleaner.Services;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Evidence-based classification support for orphan detection.
///
/// Age and size are only weak evidence: a live tool folder goes quiet for a weekend, and a
/// 1 KB config file clears the size floor just as easily as genuine residue. This classifier
/// answers a stronger question — "is anything still pointing at this folder?" — from evidence
/// the OS already maintains: running processes, services, startup entries, uninstall records,
/// Start Menu/Desktop shortcut targets, and PATH.
///
/// Everything here is fail-closed: when evidence cannot be gathered the caller must NOT treat
/// the folder as provably orphaned.
/// </summary>
public static class OrphanEvidenceClassifier
{
    /// <summary>Folder markers of an active working/installed root rather than app residue.</summary>
    private static readonly string[] WorkingRootMarkers =
    {
        "package.json", "pyproject.toml", "setup.py", "requirements.txt", "Pipfile",
        "Cargo.toml", "go.mod", "composer.json", "Gemfile", "build.gradle",
        "pubspec.yaml", "CMakeLists.txt", "Makefile", ".git"
    };

    /// <summary>Extensions marking an IDE/solution root.</summary>
    private static readonly string[] WorkingRootExtensions = { ".sln", ".csproj", ".fsproj", ".code-workspace" };

    /// <summary>
    /// Folders holding user state that cannot be regenerated: unsaved editor backups, local file
    /// history, extension storage, snippets. An Electron/IDE profile left behind by a crashed or
    /// removed app ("Antigravity IDE", "Code - Insiders") looks exactly like residue — Cache,
    /// GPUCache, blob_storage — but also contains this. Losing it loses unsaved work, so a folder
    /// holding any of it is never residue, however long it has been quiet.
    /// </summary>
    private static readonly string[] IrreplaceableUserStateNames =
    {
        "Backups", "History", "workspaceStorage", "globalStorage", "snippets",
        "User", "extensions", "Service Worker"
    };

    private static HashSet<string>? s_references;

    /// <summary>Test hook: forces the reference index to be rebuilt on next use.</summary>
    internal static void ResetCache() => Interlocked.Exchange(ref s_references, null);

    private static HashSet<string> References =>
        s_references ??= BuildLiveReferences();

    /// <summary>
    /// True when the folder is a user data tree (Documents, OneDrive, My Games, Saved Games) or an
    /// active working/project root. Such folders are never app residue, so never proposed.
    /// </summary>
    public static bool IsUserDataOrWorkingRoot(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return true;

        if (IsInsideUserDataTree(folderPath)) return true;
        if (HasIrreplaceableUserState(folderPath)) return true;
        return HasWorkingRootMarker(folderPath);
    }

    /// <summary>
    /// True when the folder holds user state that cannot be regenerated (unsaved editor backups,
    /// local history, extension storage). Checked at depth 1 because Electron profiles nest these
    /// under a per-IDE subfolder ("Antigravity IDE/User", "Code/User/globalStorage").
    /// </summary>
    private static bool HasIrreplaceableUserState(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (string name in IrreplaceableUserStateNames)
            {
                if (Directory.Exists(Path.Combine(folderPath, name))) return true;
            }

            foreach (var dir in new DirectoryInfo(folderPath).EnumerateDirectories("*", opt))
            {
                foreach (string name in IrreplaceableUserStateNames)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, name))) return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OrphanEvidence] User-state probe failed for '{folderPath}': {ex.Message}");
            return true; // fail closed
        }
    }

    /// <summary>
    /// True when anything on the machine still points into <paramref name="folderPath"/> — a running
    /// process, a registered service, a startup entry, an install-location record, a shortcut
    /// target, or a PATH entry. Such a folder is in use and must never be proposed for deletion.
    /// </summary>
    public static bool HasLiveReference(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return true;

        try
        {
            string normalized = Normalize(folderPath);
            if (normalized.Length == 0) return true; // cannot canonicalize -> fail closed

            foreach (string reference in References)
            {
                if (reference.Length == 0) continue;

                // Reference sits inside the folder (app installed there), or the folder sits inside
                // the reference (app's parent store). Either way it is not residue.
                if (normalized.StartsWith(reference, StringComparison.OrdinalIgnoreCase) ||
                    reference.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            // Fail closed: if evidence collection breaks, do not let the folder be called orphaned.
            Trace.WriteLine($"[OrphanEvidence] Reference lookup failed for '{folderPath}': {ex.Message}");
            return true;
        }
    }

    /// <summary>Documents / OneDrive / My Games / Saved Games trees hold user data, never residue.</summary>
    private static bool IsInsideUserDataTree(string folderPath)
    {
        string p = Normalize(folderPath);
        if (p.Length == 0) return true;

        string[] userDataRoots =
        {
            Normalize(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            Normalize(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)),
            Normalize(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games")),
            Normalize(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "My Games"))
        };

        foreach (string root in userDataRoots)
        {
            if (root.Length == 0) continue;
            if (p.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Detects an active project/workspace root (package manifests, VCS, solution files).</summary>
    private static bool HasWorkingRootMarker(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var marker in WorkingRootMarkers)
            {
                if (File.Exists(Path.Combine(folderPath, marker))) return true;
                if (Directory.Exists(Path.Combine(folderPath, marker))) return true;
            }

            foreach (var file in new DirectoryInfo(folderPath).EnumerateFiles("*", opt))
            {
                string ext = file.Extension;
                foreach (string marker in WorkingRootExtensions)
                {
                    if (ext.Equals(marker, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            // Depth-1 and depth-2: workspaces commonly nest their manifest inside a package folder
            // ("packages\app\package.json"), so walk one more level before giving up.
            var depth1 = new DirectoryInfo(folderPath).EnumerateDirectories("*", opt);
            foreach (var dir in depth1)
            {
                foreach (var marker in WorkingRootMarkers)
                {
                    if (File.Exists(Path.Combine(dir.FullName, marker))) return true;
                }

                var depth2 = new DirectoryInfo(dir.FullName).EnumerateDirectories("*", opt);
                foreach (var nested in depth2)
                {
                    foreach (var marker in WorkingRootMarkers)
                    {
                        if (File.Exists(Path.Combine(nested.FullName, marker))) return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[OrphanEvidence] Working-root probe failed for '{folderPath}': {ex.Message}");
            return true; // fail closed
        }
    }

    private static HashSet<string> BuildLiveReferences()
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Running processes
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                using (proc)
                {
                    try
                    {
                        string? mainModule = proc.MainModule?.FileName;
                        if (string.IsNullOrWhiteSpace(mainModule)) continue;

                        refs.Add(Normalize(mainModule));
                        refs.Add(Normalize(Path.GetDirectoryName(mainModule)));
                    }
                    catch { /* access denied on protected processes */ }
                }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[OrphanEvidence] Process scan failed: {ex.Message}"); }

        // 2. Registered services (drivers included)
        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (services != null)
            {
                foreach (string name in services.GetSubKeyNames())
                {
                    try
                    {
                        using var svc = services.OpenSubKey(name);
                        string image = svc?.GetValue("ImagePath")?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(image)) continue;

                        string exe = ExtractExePath(image);
                        if (exe.Length == 0) continue;

                        refs.Add(Normalize(exe));
                        refs.Add(Normalize(Path.GetDirectoryName(exe)));
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex) { Trace.WriteLine($"[OrphanEvidence] Service scan failed: {ex.Message}"); }

        // 3. Startup entries
        (RegistryKey Hive, string Path)[] runKeys =
        {
            (Registry.CurrentUser,   @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.CurrentUser,   @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
            (Registry.LocalMachine,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce")
        };

        foreach (var (hive, path) in runKeys)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;

                foreach (string valueName in key.GetValueNames())
                {
                    string data = key.GetValue(valueName)?.ToString() ?? string.Empty;
                    string exe = ExtractExePath(data);
                    if (exe.Length == 0) continue;

                    refs.Add(Normalize(exe));
                    refs.Add(Normalize(Path.GetDirectoryName(exe)));
                }
            }
            catch { }
        }

        // 4. Uninstall records: install location + uninstall/icon targets
        (RegistryKey Hive, string Path)[] uninstallRoots =
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (hive, path) in uninstallRoots)
        {
            try
            {
                using var root = hive.OpenSubKey(path);
                if (root == null) continue;

                foreach (string sub in root.GetSubKeyNames())
                {
                    try
                    {
                        using var entry = root.OpenSubKey(sub);
                        if (entry == null) continue;

                        string install = entry.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(install))
                            refs.Add(Normalize(install.Trim().Trim('"')));

                        foreach (string name in new[] { "UninstallString", "QuietUninstallString", "DisplayIcon" })
                        {
                            string value = entry.GetValue(name)?.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(value)) continue;

                            string exe = ExtractExePath(value);
                            if (exe.Length == 0) continue;
                            refs.Add(Normalize(Path.GetDirectoryName(exe)));
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 5. Shortcut targets (Start Menu, Desktop, Startup)
        string[] shortcutRoots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Startup"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup")
        };

        foreach (string root in shortcutRoots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;

                var opt = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                foreach (var lnk in new DirectoryInfo(root).EnumerateFiles("*.lnk", opt))
                {
                    try
                    {
                        string target = StartupManagerService.ResolveShortcutTarget(lnk.FullName);
                        if (string.IsNullOrWhiteSpace(target)) continue;

                        refs.Add(Normalize(target));
                        refs.Add(Normalize(Path.GetDirectoryName(target)));
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 6. PATH entries (portable CLI installs)
        try
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string entry in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string dir = entry.Trim().Trim('"');
                if (dir.Length == 0) continue;
                refs.Add(Normalize(dir));
            }
        }
        catch { }

        return refs;
    }

    /// <summary>Pulls the first quoted or bare executable path out of a command line / icon record.</summary>
    private static string ExtractExePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        string trimmed = command.Trim();

        int comma = trimmed.IndexOf(',');
        if (comma > 0) trimmed = trimmed.Substring(0, comma); // DisplayIcon: "path",index

        trimmed = trimmed.Trim();
        if (trimmed.StartsWith('"'))
        {
            int close = trimmed.IndexOf('"', 1);
            return close > 1 ? trimmed.Substring(1, close - 1) : string.Empty;
        }

        int space = trimmed.IndexOf(' ');
        if (space > 0) trimmed = trimmed.Substring(0, space);

        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed : string.Empty;
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            return PathSecurity.NormalizeCanonicalPath(path);
        }
        catch
        {
            try { return path.Trim().TrimEnd('\\', '/'); }
            catch { return string.Empty; }
        }
    }
}