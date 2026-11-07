using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinTempCleaner.Models;
using WinTempCleaner.Core.Safety;

namespace WinTempCleaner.Services;

public class PurgeResult
{
    public int ItemsPurgedCount { get; set; }
    public int RebootScheduledCount { get; set; }
    public int ErrorsCount { get; set; }
    public long TotalReclaimedBytes { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string FormattedReclaimed => TargetFolderInfo.FormatBytes(TotalReclaimedBytes);
    public List<string> ErrorMessages { get; set; } = new();
    public List<string> RebootScheduledItems { get; set; } = new();
}

public static class RootLeftoverPurgeService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = false)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    private const uint HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private static readonly HashSet<string> ProtectedSystemProcessNames = RestartManagerService.ProtectedSystemProcessNames;

    /// <summary>
    /// Safely purges all selected leftover items across 12 vectors.
    /// Proactively unlocks in-use files with RestartManager and supports boot-time MoveFileEx scheduling as fallback.
    /// </summary>
    public static async Task<PurgeResult> PurgeLeftoversAsync(
        IEnumerable<LeftoverItem> items,
        InstalledAppItem? app = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new PurgeResult();
        bool environmentPathPurged = false;

        // Perform safe deletion across all leftover vectors
        await Task.Run(() =>
        {
            foreach (var item in items.Where(i => i.IsSelected))
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (IsOutsideAppOwnership(item, app))
                    {
                        result.ErrorsCount++;
                        result.ErrorMessages.Add(
                            $"Safety shield blocked '{item.PathOrKey}': it is inside the app's install " +
                            "location but is not named after the app, so it may belong to another " +
                            "application sharing that folder.");
                        continue;
                    }

                    switch (item.Type)
                    {
                        case LeftoverType.File:
                        case LeftoverType.Shortcut:
                            PurgeFile(item, result);
                            break;

                        case LeftoverType.Directory:
                            PurgeDirectory(item, result, app);
                            break;

                        case LeftoverType.RegistryValue:
                            PurgeRegistryValue(item, result);
                            break;

                        case LeftoverType.RegistryKey:
                            PurgeRegistryKey(item, result);
                            break;

                        case LeftoverType.Service:
                            PurgeService(item, result);
                            break;

                        case LeftoverType.ScheduledTask:
                            PurgeScheduledTask(item, result);
                            break;

                        case LeftoverType.EnvironmentPath:
                            PurgeEnvironmentPath(item, result);
                            environmentPathPurged = true;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorsCount++;
                    result.ErrorMessages.Add($"[{item.TypeBadge}] {item.PathOrKey}: {ex.Message}");
                }
            }

            // Flush shell notifications so desktop/taskbar immediately drop broken icons
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }

            // Broadcast environment changes if PATH was updated
            if (environmentPathPurged)
            {
                BroadcastEnvironmentChange();
            }

        }, ct);

        sw.Stop();
        result.ExecutionTimeMs = sw.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// True when a file/folder item sits at or under the app's install location but is not
    /// provably owned by that app.
    ///
    /// The scanner no longer proposes these, so this is the second line of defence: a stale
    /// scan result, a hand-built list, or a future scanner regression must not be able to turn
    /// "purge leftovers" into "delete the shared folder every app was installed into". Items
    /// outside the install location (AppData, registry, WER reports, Store containers) are
    /// already name-scoped by the scanner and are not affected.
    /// </summary>
    private static bool IsOutsideAppOwnership(LeftoverItem item, InstalledAppItem? app)
    {
        if (app is null || string.IsNullOrWhiteSpace(app.InstallLocation))
            return false;

        if (item.Type is not (LeftoverType.Directory or LeftoverType.File or LeftoverType.Shortcut))
            return false;

        string install = app.InstallLocation;
        string target = item.PathOrKey;
        if (string.IsNullOrWhiteSpace(target))
            return false;

        // Only judge items that overlap the install location: the folder itself, something
        // inside it, or one of its ancestors.
        bool targetUnderInstall = InstallLocationOwnership.IsUnderOrEqual(install, target);
        bool targetContainsInstall = InstallLocationOwnership.IsUnderOrEqual(target, install);
        if (!targetUnderInstall && !targetContainsInstall) return false;

        // An ancestor of the install location is never the app's to delete. Without this,
        // a name-owned install folder ("D:\Setups\PDFgear") would bless "D:\Setups" itself -
        // the shared container the app merely lives in - and a recursive delete would take
        // every other application with it.
        if (!targetUnderInstall) return true;

        // A name-proven install folder owns everything beneath it.
        if (InstallLocationOwnership.IsDirectoryOwnedByApp(app, install))
            return false;

        // Shared container: each entry has to prove itself.
        return item.Type == LeftoverType.Directory
            ? !InstallLocationOwnership.IsDirectoryOwnedByApp(app, target)
            : !InstallLocationOwnership.IsFileOwnedByApp(app, target, install);
    }

    /// <summary>
    /// Decides whether an empty parent folder may be removed after its child was purged.
    ///
    /// The parent is routinely a publisher folder ("C:\Program Files\Vendor") and removing it
    /// keeps the tree tidy. It can also be the app's install location itself - the shared
    /// container the app happened to be installed into ("D:\Setups"). That folder belongs to
    /// the user, so purging the app-named child must leave it in place even when it is empty.
    /// </summary>
    private static bool CanRemoveEmptyParent(string parentPath, InstalledAppItem? app)
    {
        if (app is null || string.IsNullOrWhiteSpace(app.InstallLocation))
            return true;

        bool parentIsInstallOrAncestor =
            InstallLocationOwnership.IsUnderOrEqual(parentPath, app.InstallLocation);

        // Outside the standard roots an ancestor is a user-owned container; inside Program
        // Files / LocalAppData\Programs it is ordinary vendor cleanup.
        return !parentIsInstallOrAncestor ||
               InstallLocationOwnership.IsUnderStandardInstallRoot(parentPath);
    }

    private static void PurgeFile(LeftoverItem item, PurgeResult res)
    {
        if (string.IsNullOrWhiteSpace(item.PathOrKey) || !File.Exists(item.PathOrKey)) return;

        try
        {
            var fi = new FileInfo(item.PathOrKey);
            long size = fi.Length;

            // Strip ReadOnly / System / Hidden attributes
            File.SetAttributes(item.PathOrKey, FileAttributes.Normal);
            File.Delete(item.PathOrKey);

            res.TotalReclaimedBytes += size;
            res.ItemsPurgedCount++;
        }
        catch (Exception ex)
        {
            // Lock handling: inspect lockers and attempt proactive process termination
            bool resolved = false;
            try
            {
                var lockers = RestartManagerService.GetLockingProcesses(item.PathOrKey);
                foreach (var locker in lockers)
                {
                    if (!ProtectedSystemProcessNames.Contains(locker.ProcessName))
                    {
                        try
                        {
                            using var proc = Process.GetProcessById(locker.ProcessId);
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(1000);
                        }
                        catch (Exception killEx)
                        {
                            // Non-fatal: the process may have already exited (harmless race).
                            System.Diagnostics.Trace.WriteLine($"[RootPurge] Could not kill locker {locker.ProcessName} (PID {locker.ProcessId}): {killEx.GetType().Name}");
                        }
                    }
                }

                if (lockers.Count > 0)
                {
                    File.SetAttributes(item.PathOrKey, FileAttributes.Normal);
                    File.Delete(item.PathOrKey);
                    res.ItemsPurgedCount++;
                    resolved = true;
                }
            }
            catch
            {
                // Non-fatal: falls through to reboot scheduling when locks cannot be cleared.
                System.Diagnostics.Trace.WriteLine($"[RootPurge] Locked-file purge fallback for '{item.PathOrKey}' did not resolve in place.");
            }

            if (!resolved)
            {
                bool scheduled = RestartManagerService.ScheduleRebootDeletion(item.PathOrKey);
                if (scheduled)
                {
                    res.RebootScheduledCount++;
                    res.RebootScheduledItems.Add(item.PathOrKey);
                }
                else
                {
                    res.ErrorsCount++;
                    res.ErrorMessages.Add($"File delete failed: {item.PathOrKey} ({ex.Message})");
                }
            }
        }
    }

    /// <summary>
    /// Collects junction/symlink entries beneath <paramref name="root"/> without ever descending
    /// into one. Recursive delete and recursive enumeration both traverse into a link's target,
    /// so any link found here means the tree cannot be removed safely as a unit. The walk is
    /// iterative and checks the attribute before descending, which also makes it immune to
    /// junction cycles.
    /// </summary>
    private static List<string> FindNestedReparsePoints(string root, int cap = 10)
    {
        var found = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = 0
        };

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(current).EnumerateFileSystemInfos("*", opts);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RootPurge] Link scan skipped '{current}': {ex.Message}");
                continue;
            }

            foreach (var entry in entries)
            {
                bool isLink;
                bool isDir;
                try
                {
                    isLink = (entry.Attributes & FileAttributes.ReparsePoint) != 0;
                    isDir = entry is DirectoryInfo;
                }
                catch
                {
                    continue;
                }

                if (isLink)
                {
                    found.Add(entry.FullName);
                    if (found.Count >= cap) return found;
                    continue; // never descend into a link
                }

                if (isDir) pending.Push(entry.FullName);
            }
        }

        return found;
    }

    private static void PurgeDirectory(LeftoverItem item, PurgeResult res, InstalledAppItem? app)
    {
        if (string.IsNullOrWhiteSpace(item.PathOrKey) || !Directory.Exists(item.PathOrKey)) return;

        if (!InstalledAppService.IsSafeToDeleteResidual(item.PathOrKey))
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Safety shield blocked deletion of protected path: {item.PathOrKey}");
            return;
        }

        // Directory.Delete(recursive: true) removes a whole subtree in one syscall, so the
        // per-file IsProtected check that CleanupPlanner/Executor rely on never runs for it.
        // Refuse the folder when anything beneath it is durable AppData application state
        // (OneDrive sync DB, Office licences, credential vaults, key stores). Checked once
        // here so it covers every recursive delete below, including the post-unlock retry.
        if (ProtectionPolicy.TryGetProtectedDescendant(item.PathOrKey, out string protectedSubtree))
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add(
                $"Safety shield blocked deletion of '{item.PathOrKey}': it contains protected " +
                $"application data ('{protectedSubtree}'). Remove or move that data manually, then retry.");
            return;
        }

        try
        {
            long size = RootLeftoverScannerService.CalculateDirectorySizeSafe(item.PathOrKey);

            // Directory.Delete(recursive: true) follows junctions and removes the TARGET's
            // contents, not the link. Refuse the folder outright when it contains any link: on a
            // developer profile one leftover agent folder can hold a junction to a shared store of
            // installed skills, and deleting through it would destroy data this scan does not own.
            var nestedLinks = FindNestedReparsePoints(item.PathOrKey);
            if (nestedLinks.Count > 0)
            {
                res.ErrorsCount++;
                res.ErrorMessages.Add(
                    $"Safety shield blocked recursive delete of '{item.PathOrKey}': contains " +
                    $"{nestedLinks.Count} junction/symlink entr{(nestedLinks.Count == 1 ? "y" : "ies")} " +
                    $"(e.g. '{nestedLinks[0]}'). Remove or repoint the link manually, then retry.");
                return;
            }

            // Strip attributes on all files and directories
            try
            {
                var di = new DirectoryInfo(item.PathOrKey);
                foreach (var f in di.EnumerateFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                }).ToList())
                {
                    SafeOps.Try($"RootPurge/strip '{f.FullName}'", () => File.SetAttributes(f.FullName, FileAttributes.Normal));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RootPurge] Attribute sweep skipped for '{item.PathOrKey}': {ex.Message}");
            }

            Directory.Delete(item.PathOrKey, recursive: true);

            res.TotalReclaimedBytes += size;
            res.ItemsPurgedCount++;

            // Clean empty parent folder if publisher folder is now empty
            try
            {
                var parent = Directory.GetParent(item.PathOrKey);
                if (parent != null &&
                    parent.Exists &&
                    InstalledAppService.IsSafeToDeleteResidual(parent.FullName) &&
                    !parent.EnumerateFileSystemInfos().Any() &&
                    CanRemoveEmptyParent(parent.FullName, app))
                {
                    parent.Delete();
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: the leftover parent folder is cosmetic; the payload was already purged.
                System.Diagnostics.Trace.WriteLine($"[RootPurge] Parent cleanup skipped for '{item.PathOrKey}': {ex.GetType().Name}: {ex.Message}");
            }
        }
        catch
        {
            // Directory lock handling: query RestartManager to proactively terminate application lockers
            bool unlockedAny = false;
            try
            {
                var di = new DirectoryInfo(item.PathOrKey);
                foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    var lockers = RestartManagerService.GetLockingProcesses(f.FullName);
                    foreach (var locker in lockers)
                    {
                        if (!ProtectedSystemProcessNames.Contains(locker.ProcessName))
                        {
                            try
                            {
                                using var proc = Process.GetProcessById(locker.ProcessId);
                                proc.Kill(entireProcessTree: true);
                                proc.WaitForExit(1000);
                                unlockedAny = true;
                            }
                            catch (Exception ex)
                            {
                                // Non-fatal: the process may have already exited (harmless race).
                                System.Diagnostics.Trace.WriteLine($"[RootPurge] Could not kill locker {locker.ProcessName} (PID {locker.ProcessId}): {ex.GetType().Name}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RootPurge] Locker sweep failed for '{item.PathOrKey}': {ex.Message}");
            }

            // If lockers were terminated, attempt directory deletion again
            if (unlockedAny)
            {
                try
                {
                    long size = RootLeftoverScannerService.CalculateDirectorySizeSafe(item.PathOrKey);
                    Directory.Delete(item.PathOrKey, recursive: true);
                    res.TotalReclaimedBytes += size;
                    res.ItemsPurgedCount++;
                    return;
                }
                catch (Exception ex)
                {
                    // Non-fatal: the reboot-scheduling fallback below still applies.
                    System.Diagnostics.Trace.WriteLine($"[RootPurge] Retry delete failed for '{item.PathOrKey}': {ex.Message}");
                }
            }

            // Fallback: schedule remaining files and directory for reboot deletion via MoveFileEx
            bool scheduledAny = false;
            try
            {
                var di = new DirectoryInfo(item.PathOrKey);
                foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (RestartManagerService.ScheduleRebootDeletion(f.FullName))
                    {
                        scheduledAny = true;
                        res.RebootScheduledItems.Add(f.FullName);
                    }
                }
                if (RestartManagerService.ScheduleRebootDeletion(item.PathOrKey))
                {
                    scheduledAny = true;
                    res.RebootScheduledItems.Add(item.PathOrKey);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RootPurge] Reboot scheduling failed for '{item.PathOrKey}': {ex.Message}");
            }

            if (scheduledAny)
            {
                res.RebootScheduledCount++;
            }
            else
            {
                res.ErrorsCount++;
                res.ErrorMessages.Add($"Folder delete failed: {item.PathOrKey}");
            }
        }
    }

    private static void PurgeRegistryValue(LeftoverItem item, PurgeResult res)
    {
        try
        {
            (RegistryKey rootKey, string subKey) = ParseRegistryHive(item.PathOrKey);
            if (rootKey == null) return;

            using (rootKey)
            using (var key = rootKey.OpenSubKey(subKey, writable: true))
            {
                if (key != null && !string.IsNullOrWhiteSpace(item.SubKeyOrValueName))
                {
                    key.DeleteValue(item.SubKeyOrValueName, throwOnMissingValue: false);
                    res.ItemsPurgedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Registry value delete failed: {item.PathOrKey}\\{item.SubKeyOrValueName} ({ex.Message})");
        }
    }

    private static void PurgeRegistryKey(LeftoverItem item, PurgeResult res)
    {
        try
        {
            (RegistryKey rootKey, string subKey) = ParseRegistryHive(item.PathOrKey);
            if (rootKey == null) return;

            using (rootKey)
            {
                int lastSlash = subKey.LastIndexOf('\\');
                if (lastSlash > 0)
                {
                    string parentPath = subKey.Substring(0, lastSlash);
                    string keyToDelete = subKey.Substring(lastSlash + 1);

                    using var parentKey = rootKey.OpenSubKey(parentPath, writable: true);
                    if (parentKey != null)
                    {
                        parentKey.DeleteSubKeyTree(keyToDelete, throwOnMissingSubKey: false);
                        res.ItemsPurgedCount++;
                    }
                }
                else
                {
                    rootKey.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
                    res.ItemsPurgedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Registry key delete failed: {item.PathOrKey} ({ex.Message})");
        }
    }

    private static void PurgeService(LeftoverItem item, PurgeResult res)
    {
        try
        {
            string svcName = item.PathOrKey;
            // Stop service first
            RunProcessHidden("sc.exe", $"stop \"{svcName}\"");
            Thread.Sleep(200);

            // Delete service
            var procRes = RunProcessHidden("sc.exe", $"delete \"{svcName}\"");
            if (procRes == 0)
            {
                res.ItemsPurgedCount++;
            }
            else
            {
                res.ErrorsCount++;
                res.ErrorMessages.Add($"Failed to delete service: {svcName}");
            }
        }
        catch (Exception ex)
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Service purge failed: {item.PathOrKey} ({ex.Message})");
        }
    }

    private static void PurgeScheduledTask(LeftoverItem item, PurgeResult res)
    {
        try
        {
            string taskName = item.PathOrKey;
            var procRes = RunProcessHidden("schtasks.exe", $"/delete /f /tn \"{taskName}\"");
            if (procRes == 0)
            {
                res.ItemsPurgedCount++;
            }
        }
        catch (Exception ex)
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Task purge failed: {item.PathOrKey} ({ex.Message})");
        }
    }

    private static void PurgeEnvironmentPath(LeftoverItem item, PurgeResult res)
    {
        try
        {
            (RegistryKey rootKey, string subKey) = ParseRegistryHive(item.PathOrKey);
            if (rootKey == null || string.IsNullOrWhiteSpace(item.TargetPath)) return;

            using (rootKey)
            using (var key = rootKey.OpenSubKey(subKey, writable: true))
            {
                if (key == null) return;

                string valName = !string.IsNullOrWhiteSpace(item.SubKeyOrValueName) ? item.SubKeyOrValueName : "Path";
                string currentPath = key.GetValue(valName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(currentPath)) return;

                string target = item.TargetPath.Trim().TrimEnd('\\');
                var segments = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => s.Trim())
                                          .Where(s => !string.Equals(s.TrimEnd('\\'), target, StringComparison.OrdinalIgnoreCase))
                                          .ToList();

                string updatedPath = string.Join(";", segments);
                key.SetValue(valName, updatedPath, RegistryValueKind.ExpandString);
                res.ItemsPurgedCount++;
            }
        }
        catch (Exception ex)
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Environment PATH purge failed: {item.TargetPath} ({ex.Message})");
        }
    }

    private static void BroadcastEnvironmentChange()
    {
        try
        {
            SendMessageTimeout(
                (IntPtr)HWND_BROADCAST,
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "Environment",
                SMTO_ABORTIFHUNG,
                1500,
                out _);
        }
        catch { }
    }

    private static (RegistryKey, string) ParseRegistryHive(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return (null!, string.Empty);

        string clean = fullPath.TrimStart('\\');
        int firstSlash = clean.IndexOf('\\');
        if (firstSlash < 0) return (null!, string.Empty);

        string hive = clean.Substring(0, firstSlash);
        string subKey = clean.Substring(firstSlash + 1);

        // OpenBaseKey returns a fresh, per-call handle: every caller wraps the result in
        // `using`, and disposing the static Registry.CurrentUser/LocalMachine/ClassesRoot
        // objects would break every later registry access in the process.
        RegistryKey root = hive.ToUpperInvariant() switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default),
            "HKCR" or "HKEY_CLASSES_ROOT" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Default),
            _ => null!
        };

        return (root, subKey);
    }

    private static int RunProcessHidden(string fileName, string args)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (proc != null)
            {
                proc.WaitForExit(3000);
                return proc.ExitCode;
            }
        }
        catch { }
        return -1;
    }
}
