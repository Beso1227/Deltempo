using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WinTempCleaner.Core.Abstractions;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

/// <summary>
/// Why a published driver package is or is not reclaimable.
/// </summary>
public enum DriverPackageState
{
    /// <summary>At least one device currently binds to this package. Never touched.</summary>
    Bound,

    /// <summary>Not bound right now, but still installed (an oem*.inf copy exists in
    /// %WinDir%\INF). The device may simply be disconnected, so this is kept.</summary>
    Installed,

    /// <summary>Not bound, and a newer package with the same original INF name exists.
    /// This is the classic "old GPU/Bluetooth driver" leftover.</summary>
    Superseded,

    /// <summary>Not bound, not installed, and never claimed a device: staging residue
    /// left behind by an interrupted or replaced driver install.</summary>
    Staged
}

public class SupersededDriverPackage
{
    public string PublishedName { get; set; } = string.Empty; // e.g. oem80.inf
    public string OriginalName { get; set; } = string.Empty;  // e.g. ibtusb.inf
    public string ProviderName { get; set; } = string.Empty;  // e.g. Intel Corporation
    public string ClassName { get; set; } = string.Empty;     // e.g. Bluetooth
    public string DriverVersion { get; set; } = string.Empty;
    public DateTime? DriverDate { get; set; }
    public Version? ParsedVersion { get; set; }
    public long EstimatedSizeBytes { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public DriverPackageState State { get; set; } = DriverPackageState.Installed;
}

public class DriverStoreScanResult
{
    public long TotalSupersededBytes { get; set; }

    /// <summary>Reclaimable packages: superseded plus staged (see
    /// <see cref="DriverPackageState"/>). The name is kept for compatibility with the
    /// scan summary the UI has always consumed.</summary>
    public int SupersededPackageCount { get; set; }

    public List<SupersededDriverPackage> SupersededPackages { get; set; } = new();

    /// <summary>Packages currently bound to a device and therefore never targeted.</summary>
    public int BoundPackageCount { get; set; }

    /// <summary>Unbound packages retained because they are still installed.</summary>
    public int InstalledPackageCount { get; set; }

    /// <summary>Reclaimable packages that were never installed at all.</summary>
    public int StagedPackageCount { get; set; }
}

public record DriverMaintenanceResult(bool Success, long BytesFreed, int PackagesPurged, string Message)
{
    public void Deconstruct(out bool success, out string message)
    {
        success = Success;
        message = Message;
    }

    public void Deconstruct(out bool success, out long bytesFreed, out int packagesPurged, out string message)
    {
        success = Success;
        bytesFreed = BytesFreed;
        packagesPurged = PackagesPurged;
        message = Message;
    }

    public static implicit operator (bool Success, string Message)(DriverMaintenanceResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return (r.Success, r.Message);
    }
    public static implicit operator (bool Success, long BytesFreed, int PackagesPurged, string Message)(DriverMaintenanceResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return (r.Success, r.BytesFreed, r.PackagesPurged, r.Message);
    }
}

/// <summary>
/// Service managing Windows Plug-and-Play (PnP) driver package discovery and maintenance.
/// - Classifies every published package as bound / installed / superseded / staged.
/// - Reclaims only packages that are NOT bound to hardware and are either superseded by
///   a newer version of the same INF or were never installed at all.
/// - Deletes through <c>pnputil /delete-driver</c> with no force flag, so the Windows
///   driver store itself performs the final safety check.
/// - Never deletes a FileRepository folder directly: the store owns that layout.
/// - Purges DriverStore staging/temp areas and DriverState caches directly.
/// - Measures exact disk space reclaimed before and after execution.
/// </summary>
public static class WindowsDriverMaintenanceService
{
    private static string GetPnpUtilPath()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        // Use Sysnative if a 32-bit process is running on 64-bit Windows to avoid filesystem redirection
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            string sysnative = Path.Combine(winDir, "Sysnative", "pnputil.exe");
            if (File.Exists(sysnative)) return sysnative;
        }

        return Path.Combine(winDir, "System32", "pnputil.exe");
    }

    private static string GetDriverStoreRepositoryPath()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            string sysnative = Path.Combine(winDir, "Sysnative", "DriverStore", "FileRepository");
            if (Directory.Exists(sysnative)) return sysnative;
        }

        return Path.Combine(winDir, "System32", "DriverStore", "FileRepository");
    }

    private static string GetDriverStoreTempPath()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            string sysnative = Path.Combine(winDir, "Sysnative", "DriverStore", "Temp");
            if (Directory.Exists(sysnative)) return sysnative;
        }

        return Path.Combine(winDir, "System32", "DriverStore", "Temp");
    }

    private static string GetDriverStatePath()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            string sysnative = Path.Combine(winDir, "Sysnative", "DriverState");
            if (Directory.Exists(sysnative)) return sysnative;
        }

        return Path.Combine(winDir, "System32", "DriverState");
    }

    private static string GetInfDirectoryPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
    }

    public static long GetDriverStoreFileRepositorySize()
    {
        try
        {
            string repoDir = GetDriverStoreRepositoryPath();
            if (!Directory.Exists(repoDir)) return 0;

            long size = 0;
            var dirInfo = new DirectoryInfo(repoDir);
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var f in dirInfo.EnumerateFiles("*", opt))
            {
                size += f.Length;
            }
            return size;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Queries active devices to retrieve all published INF names currently bound to hardware.
    /// Used as a hard guardrail to never target an active device driver for deletion.
    /// </summary>
    public static HashSet<string> GetActiveDeviceDriverInfs()
    {
        var activeInfs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string pnpUtil = GetPnpUtilPath();
        if (!File.Exists(pnpUtil)) return activeInfs;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pnpUtil,
                Arguments = "/enum-devices /drivers",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return activeInfs;

            string output = SystemProcessRunner.DrainOutput(proc, TimeSpan.FromSeconds(30));

            using var reader = new StringReader(output);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("Driver Name:", StringComparison.OrdinalIgnoreCase))
                {
                    string inf = line.Substring("Driver Name:".Length).Trim();
                    if (!string.IsNullOrEmpty(inf) && inf.StartsWith("oem", StringComparison.OrdinalIgnoreCase))
                    {
                        activeInfs.Add(inf);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] GetActiveDeviceDriverInfs error: {ex.Message}");
        }

        return activeInfs;
    }

    /// <summary>
    /// Published packages that still have an oem*.inf copy in %WinDir%\INF, i.e. Windows
    /// considers them installed even when no device is bound to them right now.
    /// </summary>
    public static HashSet<string> GetInstalledOemInfNames()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string infDir = GetInfDirectoryPath();
            if (!Directory.Exists(infDir)) return installed;

            foreach (string file in Directory.EnumerateFiles(infDir, "oem*.inf", SearchOption.TopDirectoryOnly))
            {
                installed.Add(Path.GetFileName(file));
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] GetInstalledOemInfNames error: {ex.Message}");
        }

        return installed;
    }

    /// <summary>
    /// Assigns a <see cref="DriverPackageState"/> to every published package.
    /// </summary>
    /// <remarks>
    /// The ordering matters: an active binding always wins, because deleting the driver
    /// for connected hardware is never acceptable. Only then is superseded-ness and
    /// installation state consulted. A package whose device is merely unplugged stays
    /// <see cref="DriverPackageState.Installed"/> and is retained.
    /// </remarks>
    public static List<SupersededDriverPackage> ClassifyDriverPackages(
        IEnumerable<SupersededDriverPackage> packages,
        ISet<string> activeInfs,
        ISet<string> installedInfs)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(activeInfs);
        ArgumentNullException.ThrowIfNull(installedInfs);

        var list = packages.ToList();
        var groups = list
            .Where(p => !string.IsNullOrWhiteSpace(p.OriginalName))
            .GroupBy(p => p.OriginalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var package in list)
        {
            if (string.IsNullOrWhiteSpace(package.PublishedName))
            {
                package.State = DriverPackageState.Installed;
                continue;
            }

            if (activeInfs.Contains(package.PublishedName))
            {
                package.State = DriverPackageState.Bound;
                continue;
            }

            bool newerExists = groups.TryGetValue(package.OriginalName, out var siblings) &&
                               siblings.Any(s => !ReferenceEquals(s, package) && IsNewer(s, package));
            if (newerExists)
            {
                package.State = DriverPackageState.Superseded;
                continue;
            }

            package.State = installedInfs.Contains(package.PublishedName)
                ? DriverPackageState.Installed
                : DriverPackageState.Staged;
        }

        return list;
    }

    /// <summary>True when <paramref name="candidate"/> carries a later driver date, or an
    /// equal date and a later parsed version.</summary>
    private static bool IsNewer(SupersededDriverPackage candidate, SupersededDriverPackage reference)
    {
        DateTime candidateDate = candidate.DriverDate ?? DateTime.MinValue;
        DateTime referenceDate = reference.DriverDate ?? DateTime.MinValue;

        if (candidateDate != referenceDate) return candidateDate > referenceDate;

        Version candidateVersion = candidate.ParsedVersion ?? new Version(0, 0);
        Version referenceVersion = reference.ParsedVersion ?? new Version(0, 0);
        return candidateVersion > referenceVersion;
    }

    /// <summary>
    /// Scans DriverStore for third-party OEM driver packages that can be reclaimed:
    /// superseded versions and packages that were never installed. Does not require
    /// elevation to scan.
    /// </summary>
    public static async Task<DriverStoreScanResult> ScanSupersededDriverPackagesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() => ScanInternal(ct), ct).ConfigureAwait(false);
    }

    private static DriverStoreScanResult ScanInternal(CancellationToken ct)
    {
        var result = new DriverStoreScanResult();
        try
        {
            if (ct.IsCancellationRequested) return result;

            string pnpUtil = GetPnpUtilPath();
            if (!File.Exists(pnpUtil)) return result;

            var psi = new ProcessStartInfo
            {
                FileName = pnpUtil,
                Arguments = "/enum-drivers",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return result;

            string output = SystemProcessRunner.DrainOutput(proc, TimeSpan.FromSeconds(45));

            if (ct.IsCancellationRequested) return result;

            var allPackages = ParsePnpUtilOutput(output);
            if (allPackages.Count == 0) return result;

            var activeInfs = GetActiveDeviceDriverInfs();
            var installedInfs = GetInstalledOemInfNames();

            var classified = ClassifyDriverPackages(allPackages, activeInfs, installedInfs);

            result.BoundPackageCount = classified.Count(p => p.State == DriverPackageState.Bound);
            result.InstalledPackageCount = classified.Count(p => p.State == DriverPackageState.Installed);
            result.StagedPackageCount = classified.Count(p => p.State == DriverPackageState.Staged);

            var candidates = classified
                .Where(p => p.State is DriverPackageState.Superseded or DriverPackageState.Staged)
                .Where(p => !string.IsNullOrWhiteSpace(p.PublishedName))
                .OrderBy(p => p.OriginalName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(p => p.DriverDate ?? DateTime.MinValue)
                .ToList();

            if (candidates.Count == 0) return result;

            string repoDir = GetDriverStoreRepositoryPath();
            var index = BuildRepositoryIndex(repoDir, ct);
            var assignedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folderSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in candidates)
            {
                if (ct.IsCancellationRequested) break;

                string? folder = ResolvePackageFolder(package, repoDir, index, installedInfs, assignedFolders, ct);
                if (folder != null)
                {
                    assignedFolders.Add(folder);
                    if (!folderSizes.TryGetValue(folder, out long size))
                    {
                        size = CalculateDirectorySize(folder);
                        folderSizes[folder] = size;
                    }

                    package.FolderPath = folder;
                    package.EstimatedSizeBytes = size;
                }

                result.SupersededPackages.Add(package);
                result.TotalSupersededBytes += package.EstimatedSizeBytes;
            }

            result.SupersededPackageCount = result.SupersededPackages.Count;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] ScanSupersededDriverPackagesAsync exception: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Executes Deltempo's DriverStore maintenance:
    /// 1. Queries active hardware bindings to guard all in-use drivers.
    /// 2. Deletes superseded and never-installed packages via PnPUtil (no force flag).
    /// 3. Purges DriverStore Temp and DriverState staging areas.
    /// 4. Measures exact disk space reclaimed and verifies what remains.
    /// Requires administrator privileges.
    /// </summary>
    public static async Task<DriverMaintenanceResult> RunPnpDriverCleanAsync(
        Action<string, LogLevel>? logAction = null,
        CancellationToken ct = default)
    {
        if (!ElevationService.IsRunAsAdmin())
        {
            logAction?.Invoke("Administrator privileges required to execute Windows PnP driver maintenance.", LogLevel.Warning);
            return new DriverMaintenanceResult(false, 0, 0, "Administrator privileges required to execute Windows PnP driver maintenance.");
        }

        return await Task.Run(async () =>
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    return new DriverMaintenanceResult(false, 0, 0, "Operation cancelled.");
                }

                logAction?.Invoke("Starting Deltempo Native DriverStore & obsolete driver package maintenance...", LogLevel.Info);

                long initialRepoBytes = GetDriverStoreFileRepositorySize();

                var scan = ScanInternal(ct);
                logAction?.Invoke(
                    $"DriverStore inventory: {scan.BoundPackageCount} active, {scan.InstalledPackageCount} installed (device may be disconnected), " +
                    $"{scan.SupersededPackages.Count} reclaimable ({scan.StagedPackageCount} never installed).",
                    LogLevel.Info);

                string pnpUtil = GetPnpUtilPath();
                int purgedCount = 0;
                long estimatedPurgedBytes = 0;

                // Re-read bindings immediately before deletion: a device could have been
                // connected between the scan and now, which would make a candidate live.
                var activeDeviceInfs = GetActiveDeviceDriverInfs();

                foreach (var pkg in scan.SupersededPackages)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(pkg.PublishedName)) continue;

                    if (activeDeviceInfs.Contains(pkg.PublishedName))
                    {
                        logAction?.Invoke($"Preserved active hardware driver {pkg.PublishedName} ({pkg.OriginalName}): bound to current device", LogLevel.Info);
                        continue;
                    }

                    try
                    {
                        var result = await SystemProcessRunner.Instance.RunAsync(
                            pnpUtil,
                            $"/delete-driver \"{pkg.PublishedName}\"",
                            TimeSpan.FromMinutes(2),
                            ct).ConfigureAwait(false);

                        if (result.ExitCode == 0)
                        {
                            purgedCount++;
                            estimatedPurgedBytes += pkg.EstimatedSizeBytes;
                            logAction?.Invoke(
                                $"Purged obsolete driver package: {pkg.PublishedName} ({pkg.OriginalName} {pkg.DriverVersion}, {pkg.State})",
                                LogLevel.Info);
                        }
                        else
                        {
                            string detail = FirstMeaningfulLine(result.StandardOutput, result.StandardError);
                            logAction?.Invoke(
                                $"Preserved driver package {pkg.PublishedName} ({pkg.OriginalName}): " +
                                $"{(result.TimedOut ? "timed out" : $"Windows refused removal (exit {result.ExitCode})")}" +
                                (detail.Length > 0 ? $" - {detail}" : string.Empty),
                                LogLevel.Info);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[Deltempo] PnPUtil delete failed for {pkg.PublishedName}: {ex.Message}");
                    }
                }

                // Purge DriverStore Temp and DriverState staging areas directly
                long stagingFreed = PurgeDriverStagingDirectories(logAction, ct);

                // Measure exact DriverStore disk space reclaimed
                long finalRepoBytes = GetDriverStoreFileRepositorySize();
                long actualRepoFreedBytes = Math.Max(0, initialRepoBytes - finalRepoBytes);
                long effectiveFreedBytes = actualRepoFreedBytes + stagingFreed;
                if (effectiveFreedBytes == 0 && purgedCount > 0)
                {
                    // The deletion succeeded but Windows kept the payload referenced by a
                    // pending reboot; report the package's measured size rather than 0.
                    effectiveFreedBytes = estimatedPurgedBytes;
                }

                var remaining = ScanInternal(ct);
                int remainingCount = remaining.SupersededPackages.Count;

                if (purgedCount > 0)
                {
                    logAction?.Invoke(
                        $"Deltempo Driver Maintenance completed: {TargetFolderInfo.FormatBytes(effectiveFreedBytes)} reclaimed " +
                        $"({purgedCount} obsolete driver packages purged, {remainingCount} still pending).",
                        LogLevel.Success);
                    return new DriverMaintenanceResult(
                        true,
                        effectiveFreedBytes,
                        purgedCount,
                        $"Purged {purgedCount} obsolete driver packages ({TargetFolderInfo.FormatBytes(effectiveFreedBytes)} reclaimed)." +
                        (remainingCount > 0 ? $" {remainingCount} package(s) need a reboot or are still in use." : string.Empty));
                }

                logAction?.Invoke(
                    effectiveFreedBytes > 0
                        ? $"Deltempo Driver Maintenance completed: {TargetFolderInfo.FormatBytes(effectiveFreedBytes)} of staging cache reclaimed. No obsolete driver packages to remove."
                        : "Deltempo Driver Maintenance completed: All installed drivers are active or still required by a device.",
                    LogLevel.Info);

                return new DriverMaintenanceResult(
                    true,
                    effectiveFreedBytes,
                    0,
                    effectiveFreedBytes > 0
                        ? $"Reclaimed {TargetFolderInfo.FormatBytes(effectiveFreedBytes)} of driver staging cache."
                        : "Driver store is already clean. All packages are active or required.");
            }
            catch (OperationCanceledException)
            {
                return new DriverMaintenanceResult(false, 0, 0, "Operation cancelled.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Driver Maintenance note: {ex.Message}", LogLevel.Warning);
                return new DriverMaintenanceResult(false, 0, 0, ex.Message);
            }
        }, ct).ConfigureAwait(false);
    }

    private static string FirstMeaningfulLine(string stdout, string stderr)
    {
        foreach (string raw in (stdout + "\n" + stderr).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Equals("Microsoft PnP Utility", StringComparison.OrdinalIgnoreCase)) continue;
            return line.Length > 220 ? line[..220] : line;
        }

        return string.Empty;
    }

    private static long PurgeDriverStagingDirectories(Action<string, LogLevel>? logAction, CancellationToken ct)
    {
        long freedBytes = 0;
        string[] stagingDirs = [GetDriverStoreTempPath(), GetDriverStatePath()];

        foreach (var dir in stagingDirs)
        {
            if (ct.IsCancellationRequested) break;
            if (!Directory.Exists(dir)) continue;

            try
            {
                var dirInfo = new DirectoryInfo(dir);
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        long len = file.Length;
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        freedBytes += len;
                    }
                    catch { }
                }

                foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        subDir.Delete(true);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] PurgeDriverStagingDirectories error for {dir}: {ex.Message}");
            }
        }

        if (freedBytes > 0)
        {
            logAction?.Invoke($"Purged {TargetFolderInfo.FormatBytes(freedBytes)} from DriverStore staging and DriverState caches.", LogLevel.Info);
        }

        return freedBytes;
    }

    // ─── Repository mapping ─────────────────────────────────────────────
    //
    // PnPUtil reports a published (oemNN.inf) name, while the disk payload lives in
    // DriverStore\FileRepository\<original>.inf_<arch>_<hash>. A published package's
    // folder is resolved in two ways:
    //   1. Installed packages: %WinDir%\INF\oemNN.inf is byte-identical to the store's
    //      INF, so a SHA-256 match maps the package to exactly one folder.
    //   2. Anything else: match the INF's DriverVer against each candidate folder.

    private sealed class RepositoryIndex
    {
        public Dictionary<string, string> FolderByInfHash { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> FoldersByOriginalName { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static RepositoryIndex BuildRepositoryIndex(string repoDir, CancellationToken ct)
    {
        var index = new RepositoryIndex();
        if (!Directory.Exists(repoDir)) return index;

        IEnumerable<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(repoDir);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] BuildRepositoryIndex enumeration failed: {ex.Message}");
            return index;
        }

        foreach (string folder in folders)
        {
            if (ct.IsCancellationRequested) break;

            string folderName = Path.GetFileName(folder);
            int underscore = folderName.IndexOf('_');
            if (underscore <= 0) continue;

            string originalName = folderName[..underscore];
            if (!index.FoldersByOriginalName.TryGetValue(originalName, out var list))
            {
                list = new List<string>();
                index.FoldersByOriginalName[originalName] = list;
            }
            list.Add(folder);

            string infPath = Path.Combine(folder, originalName);
            if (!File.Exists(infPath))
            {
                try
                {
                    infPath = Directory.EnumerateFiles(folder, "*.inf", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? string.Empty;
                }
                catch
                {
                    infPath = string.Empty;
                }
            }

            if (infPath.Length > 0)
            {
                string? hash = TryHashFile(infPath);
                if (hash != null) index.FolderByInfHash[hash] = folder;
            }
        }

        return index;
    }

    private static string? ResolvePackageFolder(
        SupersededDriverPackage package,
        string repoDir,
        RepositoryIndex index,
        ISet<string> installedInfs,
        ISet<string> assignedFolders,
        CancellationToken ct)
    {
        if (!Directory.Exists(repoDir)) return null;

        // 1. Installed package: hash the oemNN.inf copy Windows keeps for uninstall/repair.
        if (installedInfs.Contains(package.PublishedName))
        {
            string oemInf = Path.Combine(GetInfDirectoryPath(), package.PublishedName);
            string? hash = TryHashFile(oemInf);
            if (hash != null &&
                index.FolderByInfHash.TryGetValue(hash, out string? mapped) &&
                !assignedFolders.Contains(mapped))
            {
                return mapped;
            }
        }

        // 2. Match the package's DriverVer against the store INF of each same-named folder.
        if (!index.FoldersByOriginalName.TryGetValue(package.OriginalName, out var candidates))
        {
            return null;
        }

        foreach (string folder in candidates)
        {
            if (ct.IsCancellationRequested) break;
            if (assignedFolders.Contains(folder)) continue;

            string infPath = Path.Combine(folder, package.OriginalName);
            if (!File.Exists(infPath))
            {
                try
                {
                    infPath = Directory.EnumerateFiles(folder, "*.inf", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? string.Empty;
                }
                catch
                {
                    infPath = string.Empty;
                }
            }

            if (infPath.Length == 0) continue;
            if (!TryReadDriverVer(infPath, out DateTime date, out Version? version)) continue;

            bool dateMatches = package.DriverDate == null || date.Date == package.DriverDate.Value.Date;
            bool versionMatches = package.ParsedVersion == null || (version != null && version == package.ParsedVersion);

            if (dateMatches && versionMatches) return folder;
        }

        // A single candidate is unambiguous even if its DriverVer could not be parsed.
        if (candidates.Count == 1 && !assignedFolders.Contains(candidates[0]))
        {
            return candidates[0];
        }

        return null;
    }

    private static bool TryReadDriverVer(string infPath, out DateTime date, out Version? version)
    {
        date = DateTime.MinValue;
        version = null;

        try
        {
            if (!File.Exists(infPath)) return false;

            // DriverVer sits in the [Version] section near the top; no need to read the
            // whole file for multi-megabyte .inf packages.
            foreach (string raw in File.ReadLines(infPath).Take(200))
            {
                string line = raw.Trim();
                if (!line.StartsWith("DriverVer", StringComparison.OrdinalIgnoreCase)) continue;

                int equals = line.IndexOf('=');
                if (equals < 0) return false;

                string value = line[(equals + 1)..].Trim();
                string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length > 0 && DateTime.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    date = parsedDate;
                }
                if (parts.Length > 1)
                {
                    if (Version.TryParse(parts[1], out Version? parsedVersion))
                    {
                        version = parsedVersion;
                    }
                }

                return date != DateTime.MinValue || version != null;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] TryReadDriverVer failed for {infPath}: {ex.Message}");
        }

        return false;
    }

    private static string? TryHashFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return null;
        }
    }

    public static List<SupersededDriverPackage> ParsePnpUtilOutput(string output)
    {
        var list = new List<SupersededDriverPackage>();
        if (string.IsNullOrWhiteSpace(output)) return list;

        using var reader = new StringReader(output);
        string? line;
        SupersededDriverPackage? current = null;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.StartsWith("Published Name:", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null && !string.IsNullOrEmpty(current.OriginalName))
                {
                    list.Add(current);
                }
                current = new SupersededDriverPackage
                {
                    PublishedName = line.Substring("Published Name:".Length).Trim()
                };
            }
            else if (current != null)
            {
                if (line.StartsWith("Original Name:", StringComparison.OrdinalIgnoreCase))
                {
                    current.OriginalName = line.Substring("Original Name:".Length).Trim();
                }
                else if (line.StartsWith("Provider Name:", StringComparison.OrdinalIgnoreCase))
                {
                    current.ProviderName = line.Substring("Provider Name:".Length).Trim();
                }
                else if (line.StartsWith("Class Name:", StringComparison.OrdinalIgnoreCase))
                {
                    current.ClassName = line.Substring("Class Name:".Length).Trim();
                }
                else if (line.StartsWith("Driver Version:", StringComparison.OrdinalIgnoreCase))
                {
                    string verStr = line.Substring("Driver Version:".Length).Trim();
                    current.DriverVersion = verStr;
                    var parts = verStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && DateTime.TryParse(parts[0], out var dt))
                    {
                        current.DriverDate = dt;
                    }
                    if (parts.Length > 1)
                    {
                        string vOnly = parts[1].Trim();
                        if (Version.TryParse(vOnly, out var ver))
                        {
                            current.ParsedVersion = ver;
                        }
                    }
                }
            }
        }

        if (current != null && !string.IsNullOrEmpty(current.OriginalName))
        {
            list.Add(current);
        }

        return list;
    }

    private static long CalculateDirectorySize(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
        try
        {
            long size = 0;
            var dir = new DirectoryInfo(path);
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var file in dir.EnumerateFiles("*", opt))
            {
                size += file.Length;
            }
            return size;
        }
        catch
        {
            return 0;
        }
    }
}
