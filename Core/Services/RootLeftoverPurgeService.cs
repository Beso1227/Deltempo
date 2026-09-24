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
        string? appName = null,
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
                    switch (item.Type)
                    {
                        case LeftoverType.File:
                        case LeftoverType.Shortcut:
                            PurgeFile(item, result);
                            break;

                        case LeftoverType.Directory:
                            PurgeDirectory(item, result);
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
                        catch { }
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
            catch { }

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

    private static void PurgeDirectory(LeftoverItem item, PurgeResult res)
    {
        if (string.IsNullOrWhiteSpace(item.PathOrKey) || !Directory.Exists(item.PathOrKey)) return;

        if (!InstalledAppService.IsSafeToDeleteResidual(item.PathOrKey))
        {
            res.ErrorsCount++;
            res.ErrorMessages.Add($"Safety shield blocked deletion of protected path: {item.PathOrKey}");
            return;
        }

        try
        {
            long size = RootLeftoverScannerService.CalculateDirectorySizeSafe(item.PathOrKey);

            // Strip attributes on all files and directories
            var di = new DirectoryInfo(item.PathOrKey);
            foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f.FullName, FileAttributes.Normal); } catch { }
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
                    !parent.EnumerateFileSystemInfos().Any())
                {
                    parent.Delete();
                }
            }
            catch { }
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
                            catch { }
                        }
                    }
                }
            }
            catch { }

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
                catch { }
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
            catch { }

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

        RegistryKey root = hive.ToUpperInvariant() switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
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
