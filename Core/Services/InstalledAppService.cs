using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinTempCleaner.Services;

namespace WinTempCleaner.Models;

public class InstalledAppItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
    }

    private string _publisher = string.Empty;
    public string Publisher
    {
        get => _publisher;
        set { if (_publisher != value) { _publisher = value; OnPropertyChanged(); } }
    }

    private string _displayVersion = string.Empty;
    public string DisplayVersion
    {
        get => _displayVersion;
        set { if (_displayVersion != value) { _displayVersion = value; OnPropertyChanged(); } }
    }

    private string _installDate = string.Empty;
    public string InstallDate
    {
        get => _installDate;
        set { if (_installDate != value) { _installDate = value; OnPropertyChanged(); } }
    }

    private long _estimatedSizeBytes;
    public long EstimatedSizeBytes
    {
        get => _estimatedSizeBytes;
        set { if (_estimatedSizeBytes != value) { _estimatedSizeBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedSize)); } }
    }
    public string FormattedSize => TargetFolderInfo.FormatBytes(EstimatedSizeBytes);

    private string _uninstallString = string.Empty;
    public string UninstallString
    {
        get => _uninstallString;
        set { if (_uninstallString != value) { _uninstallString = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUninstaller)); OnPropertyChanged(nameof(IsBroken)); OnPropertyChanged(nameof(UninstallEngine)); OnPropertyChanged(nameof(UninstallCommandDisplay)); } }
    }

    private string _quietUninstallString = string.Empty;
    public string QuietUninstallString
    {
        get => _quietUninstallString;
        set { if (_quietUninstallString != value) { _quietUninstallString = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUninstaller)); OnPropertyChanged(nameof(IsBroken)); OnPropertyChanged(nameof(UninstallEngine)); OnPropertyChanged(nameof(UninstallCommandDisplay)); } }
    }

    private string _installLocation = string.Empty;
    public string InstallLocation
    {
        get => _installLocation;
        set { if (_installLocation != value) { _installLocation = value; OnPropertyChanged(); OnPropertyChanged(nameof(InstallLocationDisplay)); OnPropertyChanged(nameof(CanOpenInstallFolder)); OnPropertyChanged(nameof(OpenFolderVisibility)); } }
    }

    public string DisplayIcon { get; set; } = string.Empty;

    private string _registryKeyPath = string.Empty;
    public string RegistryKeyPath
    {
        get => _registryKeyPath;
        set { if (_registryKeyPath != value) { _registryKeyPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(RegistryKeyPathDisplay)); } }
    }

    public bool IsSystemComponent { get; set; }
    public bool IsWindowsStoreApp { get; set; }

    private string _packageFullName = string.Empty;
    public string PackageFullName
    {
        get => _packageFullName;
        set { if (_packageFullName != value) { _packageFullName = value; OnPropertyChanged(); } }
    }

    // Visual Icon & Fallback Glyph
    private ImageSource? _appIcon;
    public ImageSource? AppIcon
    {
        get => _appIcon;
        set { if (_appIcon != value) { _appIcon = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAppIcon)); } }
    }
    public bool HasAppIcon => AppIcon != null;
    public string FallbackGlyph => Category switch
    {
        "Browsers" => "\uE774",           // Globe / Web
        "Gaming" => "\uE7FC",             // Controller
        "Development" => "\uEBE8",        // Code
        "Productivity" => "\uE7BE",       // Document / Reading
        "Media" or "Media & Audio" => "\uE8B9", // Music / Media
        "System & Hardware" => "\uE950",  // Devices / Chip
        "Runtimes" => "\uE90F",           // Framework / Blocks
        "Networking & Security" => "\uE72E", // Shield / Lock
        "Utilities" => "\uE713",          // Settings / Gear
        _ => "\uE71D"                     // Package / App
    };

    // AI & Catalog Intelligence
    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set { if (_description != value) { _description = value; OnPropertyChanged(); } }
    }

    private string _category = "General";
    public string Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CategoryBadgeColor));
                OnPropertyChanged(nameof(CategoryBadgeBackground));
                OnPropertyChanged(nameof(FallbackGlyph));
            }
        }
    }

    private string _safetyAdvice = string.Empty;
    public string SafetyAdvice
    {
        get => _safetyAdvice;
        set { if (_safetyAdvice != value) { _safetyAdvice = value; OnPropertyChanged(); OnPropertyChanged(nameof(SafetyAdviceDisplay)); } }
    }

    private string _safetyVerdict = "Safe to Remove";
    public string SafetyVerdict
    {
        get => _safetyVerdict;
        set { if (_safetyVerdict != value) { _safetyVerdict = value; OnPropertyChanged(); } }
    }

    private bool _isCoreRuntime;
    public bool IsCoreRuntime
    {
        get => _isCoreRuntime;
        set
        {
            if (_isCoreRuntime != value)
            {
                _isCoreRuntime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SafetyBadgeColor));
                OnPropertyChanged(nameof(SafetyBadgeBackground));
                OnPropertyChanged(nameof(SafetyBadgeBorder));
                OnPropertyChanged(nameof(SafetyAdviceDisplay));
            }
        }
    }

    private bool _isAiEnriched;
    public bool IsAiEnriched
    {
        get => _isAiEnriched;
        set
        {
            if (_isAiEnriched != value)
            {
                _isAiEnriched = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiProviderDisplay));
            }
        }
    }

    private string _aiProviderUsed = string.Empty;
    public string AiProviderUsed
    {
        get => _aiProviderUsed;
        set
        {
            if (_aiProviderUsed != value)
            {
                _aiProviderUsed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AiProviderDisplay));
            }
        }
    }

    public string HelpLink { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    // Expansion State
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandedVisibility));
                OnPropertyChanged(nameof(ExpandGlyph));
            }
        }
    }
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public string ExpandGlyph => IsExpanded ? "\uE70E" : "\uE70D"; // Chevron Up vs Down

    // Formatted Inspection Helpers
    public string AiProviderDisplay => IsAiEnriched
        ? (string.IsNullOrWhiteSpace(AiProviderUsed) ? "AI Verified" : $"AI Verified ({AiProviderUsed})")
        : "Catalog Intelligence";

    public string SafetyAdviceDisplay => !string.IsNullOrWhiteSpace(SafetyAdvice)
        ? SafetyAdvice
        : (IsCoreRuntime
            ? "System critical runtime or driver. Removing this may cause Windows instability or break dependent apps."
            : "Safe to remove if you no longer use this application.");

    public string SafetyBadgeColor => IsCoreRuntime ? "#EF4444" : "#10B981";
    public string SafetyBadgeBackground => IsCoreRuntime ? "#1AEF4444" : "#1A10B981";
    public string SafetyBadgeBorder => IsCoreRuntime ? "#40EF4444" : "#4010B981";

    public string InstallLocationDisplay => !string.IsNullOrWhiteSpace(InstallLocation) ? InstallLocation : "Not specified by installer";
    public bool CanOpenInstallFolder => !string.IsNullOrWhiteSpace(InstallLocation) && (Directory.Exists(InstallLocation) || Directory.Exists(Path.GetDirectoryName(InstallLocation) ?? ""));
    public Visibility OpenFolderVisibility => CanOpenInstallFolder ? Visibility.Visible : Visibility.Collapsed;

    public string UninstallCommandDisplay => !string.IsNullOrWhiteSpace(QuietUninstallString)
        ? QuietUninstallString
        : (!string.IsNullOrWhiteSpace(UninstallString) ? UninstallString : "No uninstall command registered (Broken / Manual clean recommended)");

    public string RegistryKeyPathDisplay => !string.IsNullOrWhiteSpace(RegistryKeyPath) ? RegistryKeyPath : "Windows Store Package / Managed App";

    public string CategoryBadgeColor => Category switch
    {
        "Browsers" => "#06B6D4",
        "Gaming" => "#A855F7",
        "Development" => "#3B82F6",
        "Productivity" => "#10B981",
        "Media" or "Media & Audio" => "#EC4899",
        "System & Hardware" => "#F59E0B",
        "Runtimes" => "#EF4444",
        "Networking & Security" => "#6366F1",
        "Utilities" => "#00E5FF",
        _ => "#94A3B8"
    };

    public string CategoryBadgeBackground => Category switch
    {
        "Browsers" => "#1406B6D4",
        "Gaming" => "#14A855F7",
        "Development" => "#143B82F6",
        "Productivity" => "#1410B981",
        "Media" or "Media & Audio" => "#14EC4899",
        "System & Hardware" => "#14F59E0B",
        "Runtimes" => "#14EF4444",
        "Networking & Security" => "#146366F1",
        "Utilities" => "#1400E5FF",
        _ => "#1494A3B8"
    };

    public bool HasUninstaller => !string.IsNullOrWhiteSpace(UninstallString) || !string.IsNullOrWhiteSpace(QuietUninstallString) || IsWindowsStoreApp;
    public bool IsBroken => !HasUninstaller;

    public string UninstallEngine => IsWindowsStoreApp ? "MSIX / Store" : DetectUninstallEngine(UninstallString, QuietUninstallString);

    private static string DetectUninstallEngine(string uninstallStr, string quietStr)
    {
        string str = $"{uninstallStr} {quietStr}".ToLowerInvariant();
        if (str.Contains("msiexec")) return "MSI";
        if (str.Contains("unins000") || str.Contains("innosetup")) return "Inno Setup";
        if (str.Contains("uninstall.exe") || str.Contains("uninst.exe") || str.Contains("nsis")) return "NSIS";
        if (str.Contains("installshield") || str.Contains("_isreg")) return "InstallShield";
        if (str.Contains("wise")) return "Wise";
        if (str.Contains("update.exe --uninstall") || str.Contains("squirrel")) return "Squirrel";
        return "Standard";
    }
}

public static class InstalledAppService
{
    private static readonly HashSet<string> ProtectedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "System32", "SysWOW64", "Program Files", "Program Files (x86)",
        "Users", "Default", "Public", "Microsoft", "AppData", "Local", "Roaming",
        "LocalLow", "ProgramData", "Common Files", "Windows Defender", "SoftwareDistribution"
    };

    public static IReadOnlyList<InstalledAppItem> GetInstalledApps()
    {
        var result = new Dictionary<string, InstalledAppItem>(StringComparer.OrdinalIgnoreCase);

        string[] subKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var subKeyPath in subKeys)
        {
            ReadRegistryUninstallKeys(Registry.LocalMachine, subKeyPath, result);
            ReadRegistryUninstallKeys(Registry.CurrentUser, subKeyPath, result);
        }

        // Read Modern Windows Store / AppX / MSIX Packages
        ReadAppxPackages(result);

        return result.Values
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReadRegistryUninstallKeys(RegistryKey rootKey, string subKeyPath, Dictionary<string, InstalledAppItem> acc)
    {
        try
        {
            using var baseKey = rootKey.OpenSubKey(subKeyPath);
            if (baseKey == null) return;

            foreach (var appKeyName in baseKey.GetSubKeyNames())
            {
                try
                {
                    using var appKey = baseKey.OpenSubKey(appKeyName);
                    if (appKey == null) continue;

                    string? name = appKey.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    // Skip Windows Hotfixes and Updates
                    string? releaseType = appKey.GetValue("ReleaseType")?.ToString();
                    if (string.Equals(releaseType, "Security Update", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(releaseType, "Update", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("KB", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int isSysComp = 0;
                    if (appKey.GetValue("SystemComponent") is int sysVal) isSysComp = sysVal;
                    if (isSysComp == 1) continue;

                    long sizeBytes = 0;
                    if (appKey.GetValue("EstimatedSize") is int sizeKb)
                    {
                        sizeBytes = sizeKb * 1024L;
                    }

                    string publisher = appKey.GetValue("Publisher")?.ToString()?.Trim() ?? string.Empty;
                    string version = appKey.GetValue("DisplayVersion")?.ToString()?.Trim() ?? string.Empty;
                    string installDate = appKey.GetValue("InstallDate")?.ToString()?.Trim() ?? string.Empty;
                    string uninstallStr = appKey.GetValue("UninstallString")?.ToString()?.Trim() ?? string.Empty;
                    string quietUninstallStr = appKey.GetValue("QuietUninstallString")?.ToString()?.Trim() ?? string.Empty;
                    string installLocation = appKey.GetValue("InstallLocation")?.ToString()?.Trim() ?? string.Empty;
                    string icon = appKey.GetValue("DisplayIcon")?.ToString()?.Trim() ?? string.Empty;

                    if (!acc.ContainsKey(name))
                    {
                        var appItem = new InstalledAppItem
                        {
                            DisplayName = name,
                            Publisher = publisher,
                            DisplayVersion = version,
                            InstallDate = installDate,
                            EstimatedSizeBytes = sizeBytes,
                            UninstallString = uninstallStr,
                            QuietUninstallString = quietUninstallStr,
                            InstallLocation = installLocation,
                            DisplayIcon = icon,
                            RegistryKeyPath = $@"{((rootKey == Registry.CurrentUser) ? "HKCU" : "HKLM")}\{subKeyPath}\{appKeyName}",
                            IsSystemComponent = false,
                            IsWindowsStoreApp = false
                        };
                        PopulateAppIntelligence(appItem, appKey);
                        appItem.AppIcon = AppIconService.GetAppIcon(appItem.DisplayIcon, appItem.InstallLocation, appItem.DisplayName);
                        acc[name] = appItem;
                    }
                }
                catch
                {
                    // Ignore inaccessible subkeys
                }
            }
        }
        catch
        {
            // Ignore registry failures
        }
    }

    private static void PopulateAppIntelligence(InstalledAppItem item, RegistryKey appKey)
    {
        string comments = appKey.GetValue("Comments")?.ToString()?.Trim() ?? string.Empty;
        string helpLink = appKey.GetValue("HelpLink")?.ToString()?.Trim() ??
                          appKey.GetValue("URLInfoAbout")?.ToString()?.Trim() ?? string.Empty;

        item.Comments = comments;
        item.HelpLink = helpLink;

        // 1. Check if we already have an AI Report in local cache
        var cached = AppIntelligenceService.GetCachedReport(item);
        if (cached != null)
        {
            item.Description = cached.WhatIsIt;
            item.Category = cached.Category;
            item.SafetyAdvice = cached.RemovalImpact;
            item.SafetyVerdict = cached.SafetyVerdict;
            item.IsAiEnriched = true;
            item.AiProviderUsed = cached.ProviderUsed;
            item.IsCoreRuntime = cached.SafetyVerdict.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                 cached.SafetyVerdict.Contains("Keep", StringComparison.OrdinalIgnoreCase);
            return;
        }

        // 2. Curated offline catalog lookup
        var entry = AppDescriptionCatalog.FindCatalogEntry(item.DisplayName, item.Publisher);
        if (entry != null)
        {
            item.Description = entry.Description;
            item.Category = entry.Category;
            item.SafetyAdvice = entry.SafetyAdvice;
            item.IsCoreRuntime = entry.IsCoreRuntime;
            item.SafetyVerdict = entry.IsCoreRuntime ? "Core Runtime / Driver - Keep" : "Safe to Remove";
            return;
        }

        // 3. Inspect PE Binary FileDescription if available
        string peDesc = ExtractPeFileDescription(item);
        if (!string.IsNullOrWhiteSpace(peDesc) &&
            !string.Equals(peDesc, item.DisplayName, StringComparison.OrdinalIgnoreCase) &&
            peDesc.Length > 8)
        {
            var (_, hCat, hAdvice, hCore) = AppDescriptionCatalog.ClassifyUnknownApp(
                item.DisplayName,
                item.Publisher,
                item.InstallLocation,
                item.UninstallEngine);

            item.Description = peDesc;
            item.Category = hCat;
            item.SafetyAdvice = hAdvice;
            item.IsCoreRuntime = hCore;
            item.SafetyVerdict = hCore ? "Core Runtime / Driver - Keep" : "Safe to Remove";
            return;
        }

        // 4. Registry Comments if informative
        if (!string.IsNullOrWhiteSpace(comments) && comments.Length > 10 && !comments.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var (_, hCat, hAdvice, hCore) = AppDescriptionCatalog.ClassifyUnknownApp(
                item.DisplayName,
                item.Publisher,
                item.InstallLocation,
                item.UninstallEngine);

            item.Description = comments;
            item.Category = hCat;
            item.SafetyAdvice = hAdvice;
            item.IsCoreRuntime = hCore;
            item.SafetyVerdict = hCore ? "Core Runtime / Driver - Keep" : "Safe to Remove";
            return;
        }

        // 5. Heuristic classification
        var (cDesc, cCat, cAdvice, cCore) = AppDescriptionCatalog.ClassifyUnknownApp(
            item.DisplayName,
            item.Publisher,
            item.InstallLocation,
            item.UninstallEngine);

        item.Description = cDesc;
        item.Category = cCat;
        item.SafetyAdvice = cAdvice;
        item.IsCoreRuntime = cCore;
        item.SafetyVerdict = cCore ? "Core Runtime / Driver - Keep" : "Safe to Remove";
    }

    private static string ExtractPeFileDescription(InstalledAppItem item)
    {
        try
        {
            string candidateExe = string.Empty;

            if (!string.IsNullOrWhiteSpace(item.DisplayIcon))
            {
                string iconPath = item.DisplayIcon.Trim('\"', '\'').Split(',')[0].Trim();
                if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
                {
                    candidateExe = iconPath;
                }
            }

            if (string.IsNullOrEmpty(candidateExe) && !string.IsNullOrWhiteSpace(item.InstallLocation) && Directory.Exists(item.InstallLocation))
            {
                var files = Directory.GetFiles(item.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    candidateExe = files[0];
                }
            }

            if (!string.IsNullOrEmpty(candidateExe) && File.Exists(candidateExe))
            {
                var vi = FileVersionInfo.GetVersionInfo(candidateExe);
                if (!string.IsNullOrWhiteSpace(vi.FileDescription))
                {
                    return vi.FileDescription.Trim();
                }
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    public static IReadOnlyList<string> FindResidualLeftovers(InstalledAppItem app)
    {
        var leftovers = new List<string>();
        if (string.IsNullOrWhiteSpace(app.DisplayName) || app.DisplayName.Length < 3)
            return leftovers;

        // Never look for residuals of generic names like "Windows" or "Microsoft"
        if (ProtectedDirectoryNames.Contains(app.DisplayName) ||
            app.DisplayName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return leftovers;
        }

        string[] targetRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        foreach (var root in targetRoots)
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                var candidate = Path.Combine(root, app.DisplayName);
                if (Directory.Exists(candidate) && IsSafeToDeleteResidual(candidate))
                {
                    leftovers.Add(candidate);
                }

                if (!string.IsNullOrWhiteSpace(app.Publisher) &&
                    app.Publisher.Length >= 4 &&
                    !ProtectedDirectoryNames.Contains(app.Publisher))
                {
                    var pubCandidate = Path.Combine(root, app.Publisher, app.DisplayName);
                    if (Directory.Exists(pubCandidate) && IsSafeToDeleteResidual(pubCandidate))
                    {
                        leftovers.Add(pubCandidate);
                    }
                }
            }
            catch
            {
                // Access issues ignored
            }
        }

        return leftovers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsSafeToDeleteResidual(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            string fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');

            // Must not be root of a drive (e.g. C:\)
            var root = Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/');
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)) return false;

            // Must not match protected core directories
            string dirName = Path.GetFileName(fullPath);
            if (ProtectedDirectoryNames.Contains(dirName)) return false;

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');

            if (fullPath.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(winDir + "\\", StringComparison.OrdinalIgnoreCase) ||
                fullPath.Equals(progFiles, StringComparison.OrdinalIgnoreCase) ||
                fullPath.Equals(progFilesX86, StringComparison.OrdinalIgnoreCase) ||
                fullPath.Equals(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<Process> GetRunningProcesses(InstalledAppItem app)
    {
        var matched = new List<Process>();
        try
        {
            string appDataLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), app.DisplayName);
            string appDataRoaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), app.DisplayName);

            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    if (proc.HasExited) continue;

                    string mainModulePath = string.Empty;
                    try { mainModulePath = proc.MainModule?.FileName ?? string.Empty; } catch { }

                    bool matchesPath = !string.IsNullOrWhiteSpace(app.InstallLocation) &&
                                       !string.IsNullOrWhiteSpace(mainModulePath) &&
                                       mainModulePath.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase);

                    bool matchesAppData = !string.IsNullOrWhiteSpace(mainModulePath) &&
                                          (mainModulePath.StartsWith(appDataLocal, StringComparison.OrdinalIgnoreCase) ||
                                           mainModulePath.StartsWith(appDataRoaming, StringComparison.OrdinalIgnoreCase));

                    bool matchesName = string.Equals(proc.ProcessName, app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                                       (app.DisplayName.Length >= 4 && proc.ProcessName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase));

                    if (matchesPath || matchesAppData || matchesName)
                    {
                        matched.Add(proc);
                    }
                }
                catch { }
            }
        }
        catch { }
        return matched;
    }

    public static async Task<int> TerminateAppProcessesAsync(InstalledAppItem app)
    {
        return await Task.Run(() =>
        {
            var procs = GetRunningProcesses(app);
            int count = 0;
            foreach (var p in procs)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(1500);
                    count++;
                }
                catch { }
            }
            return count;
        });
    }

    public static async Task<bool> UninstallAppAsync(InstalledAppItem app, bool silent = false, int timeoutMs = 180000)
    {
        // First terminate any running instances of the app so uninstaller isn't blocked
        await TerminateAppProcessesAsync(app);

        // Modern Windows Store / AppX / MSIX Package uninstallation
        if (app.IsWindowsStoreApp && !string.IsNullOrWhiteSpace(app.PackageFullName))
        {
            return await UninstallAppxPackageAsync(app.PackageFullName);
        }

        string cmd = silent && !string.IsNullOrWhiteSpace(app.QuietUninstallString)
            ? app.QuietUninstallString
            : app.UninstallString;

        if (string.IsNullOrWhiteSpace(cmd)) return false;

        return await Task.Run(() =>
        {
            try
            {
                // Parse binary and arguments
                string fileName;
                string args = string.Empty;

                cmd = cmd.Trim();
                if (cmd.StartsWith("\""))
                {
                    int quoteClose = cmd.IndexOf('\"', 1);
                    if (quoteClose > 0)
                    {
                        fileName = cmd.Substring(1, quoteClose - 1);
                        args = cmd.Substring(quoteClose + 1).Trim();
                    }
                    else
                    {
                        fileName = cmd.Trim('\"');
                    }
                }
                else
                {
                    int spaceIdx = cmd.IndexOf(' ');
                    if (spaceIdx > 0)
                    {
                        fileName = cmd.Substring(0, spaceIdx);
                        args = cmd.Substring(spaceIdx + 1).Trim();
                    }
                    else
                    {
                        fileName = cmd;
                    }
                }

                // If silent was requested and no quiet string was available, inject known silent switches based on engine
                if (silent && string.IsNullOrWhiteSpace(app.QuietUninstallString))
                {
                    string engine = app.UninstallEngine;
                    if (engine == "Inno Setup" && !args.Contains("/VERYSILENT", StringComparison.OrdinalIgnoreCase))
                    {
                        args = $"{args} /VERYSILENT /SUPPRESSMSGBOXES /NORESTART".Trim();
                    }
                    else if (engine == "NSIS" && !args.Contains("/S", StringComparison.OrdinalIgnoreCase))
                    {
                        args = $"{args} /S".Trim();
                    }
                    else if (engine == "MSI" && !args.Contains("/qn", StringComparison.OrdinalIgnoreCase))
                    {
                        args = $"{args} /qn /norestart".Trim();
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(timeoutMs);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        });
    }

    public static async Task<bool> UninstallAppxPackageAsync(string packageFullName)
    {
        return await Task.Run(() =>
        {
            try
            {
                string script = $"Remove-AppxPackage -Package '{packageFullName.Replace("'", "''")}' -ErrorAction Stop";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    bool finished = proc.WaitForExit(60000);
                    return finished && proc.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AppX Uninstall] Error: {ex.Message}");
            }
            return false;
        });
    }

    private static void ReadAppxPackages(Dictionary<string, InstalledAppItem> acc)
    {
        try
        {
            const string appModelKeyPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            using var baseKey = Registry.CurrentUser.OpenSubKey(appModelKeyPath);
            if (baseKey == null) return;

            foreach (var pkgName in baseKey.GetSubKeyNames())
            {
                try
                {
                    using var pkgKey = baseKey.OpenSubKey(pkgName);
                    if (pkgKey == null) continue;

                    string displayName = pkgKey.GetValue("DisplayName")?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("@{") || displayName.StartsWith("ms-resource:"))
                    {
                        continue;
                    }

                    string pkgRoot = pkgKey.GetValue("PackageRootFolder")?.ToString()?.Trim() ?? string.Empty;
                    string pkgId = pkgKey.GetValue("PackageID")?.ToString()?.Trim() ?? pkgName;

                    // Skip internal system dependency frameworks
                    if (displayName.StartsWith("Microsoft.NET", StringComparison.OrdinalIgnoreCase) ||
                        displayName.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase) ||
                        displayName.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase) ||
                        displayName.StartsWith("Microsoft.WindowsAppRuntime", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!acc.ContainsKey(displayName))
                    {
                        var appItem = new InstalledAppItem
                        {
                            DisplayName = displayName,
                            Publisher = ExtractPublisherFromPackageId(pkgId),
                            DisplayVersion = ExtractVersionFromPackageId(pkgId),
                            InstallLocation = pkgRoot,
                            PackageFullName = pkgName,
                            IsSystemComponent = false,
                            IsWindowsStoreApp = true,
                            UninstallString = $"powershell.exe -NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package {pkgName}\"",
                            QuietUninstallString = $"powershell.exe -NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package {pkgName}\"",
                            Category = "Utilities"
                        };

                        acc[displayName] = appItem;
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static string ExtractPublisherFromPackageId(string pkgId)
    {
        int underscoreIdx = pkgId.IndexOf('_');
        if (underscoreIdx > 0)
        {
            string prefix = pkgId.Substring(0, underscoreIdx);
            int dotIdx = prefix.IndexOf('.');
            if (dotIdx > 0)
            {
                return prefix.Substring(dotIdx + 1);
            }
            return prefix;
        }
        return "Windows Store";
    }

    private static string ExtractVersionFromPackageId(string pkgId)
    {
        var parts = pkgId.Split('_');
        if (parts.Length >= 2)
        {
            return parts[1];
        }
        return string.Empty;
    }
}

public static class AppIconService
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
    private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource? GetAppIcon(string displayIcon, string installLocation, string displayName)
    {
        string rawKey = $"{displayIcon}|{installLocation}|{displayName}";
        if (Cache.TryGetValue(rawKey, out var cached))
        {
            return cached;
        }

        ImageSource? icon = ExtractIconInternal(displayIcon, installLocation, displayName);
        Cache[rawKey] = icon;
        return icon;
    }

    private static ImageSource? ExtractIconInternal(string displayIcon, string installLocation, string displayName)
    {
        try
        {
            // 1. Try DisplayIcon from registry
            if (!string.IsNullOrWhiteSpace(displayIcon))
            {
                var (filePath, index) = ParseIconPath(displayIcon);
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    var img = ExtractFromFile(filePath, index);
                    if (img != null) return img;
                }
            }

            // 2. Try InstallLocation for executables or standalone icons
            if (!string.IsNullOrWhiteSpace(installLocation))
            {
                string expLocation = Environment.ExpandEnvironmentVariables(installLocation.Trim('\"', '\''));
                if (Directory.Exists(expLocation))
                {
                    // Check for standalone .ico or .png files in the root folder
                    try
                    {
                        var iconFiles = Directory.GetFiles(expLocation, "*.ico", SearchOption.TopDirectoryOnly);
                        if (iconFiles.Length > 0)
                        {
                            var img = LoadDirectImage(iconFiles[0]);
                            if (img != null) return img;
                        }
                    }
                    catch { }

                    // Check for main executable
                    try
                    {
                        var exeFiles = Directory.GetFiles(expLocation, "*.exe", SearchOption.TopDirectoryOnly);
                        foreach (var exe in exeFiles)
                        {
                            string fileName = Path.GetFileName(exe);
                            if (fileName.StartsWith("unins", StringComparison.OrdinalIgnoreCase) ||
                                fileName.StartsWith("setup", StringComparison.OrdinalIgnoreCase) ||
                                fileName.StartsWith("helper", StringComparison.OrdinalIgnoreCase) ||
                                fileName.StartsWith("crash", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var img = ExtractFromFile(exe, 0);
                            if (img != null) return img;
                        }
                    }
                    catch { }
                }
            }
        }
        catch
        {
            // Fallback gracefully on any system GDI/IO anomaly
        }

        return null;
    }

    private static (string FilePath, int Index) ParseIconPath(string raw)
    {
        string clean = Environment.ExpandEnvironmentVariables(raw.Trim('\"', '\'').Trim());
        int commaIndex = clean.LastIndexOf(',');
        if (commaIndex > 0)
        {
            string pathPart = clean[..commaIndex].Trim('\"', '\'').Trim();
            string indexPart = clean[(commaIndex + 1)..].Trim();
            if (int.TryParse(indexPart, out int idx))
            {
                return (pathPart, idx);
            }
            return (pathPart, 0);
        }
        return (clean, 0);
    }

    private static ImageSource? ExtractFromFile(string filePath, int index)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        if (filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            var direct = LoadDirectImage(filePath);
            if (direct != null) return direct;
        }

        IntPtr[] largeIcons = new IntPtr[1];
        IntPtr[] smallIcons = new IntPtr[1];

        try
        {
            uint extracted = ExtractIconEx(filePath, index, largeIcons, smallIcons, 1);
            IntPtr hIcon = (smallIcons[0] != IntPtr.Zero) ? smallIcons[0] : largeIcons[0];

            if (hIcon != IntPtr.Zero)
            {
                var bmp = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            if (largeIcons[0] != IntPtr.Zero) DestroyIcon(largeIcons[0]);
            if (smallIcons[0] != IntPtr.Zero && smallIcons[0] != largeIcons[0]) DestroyIcon(smallIcons[0]);
        }

        return null;
    }

    private static BitmapImage? LoadDirectImage(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(filePath, UriKind.Absolute);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 32;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }
}
