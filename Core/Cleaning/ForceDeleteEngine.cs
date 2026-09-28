using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Cleaning;

public enum ForceDeleteStage
{
    Validate,
    Shielded,
    BreakLock,
    ResetAttributes,
    TakeOwnership,
    Delete,
    ScheduleReboot
}

public sealed record ForceDeleteAttempt(
    string Path,
    ForceDeleteStage Stage,
    bool Success,
    int Win32Error,
    string Message);

public sealed class ForceDeleteOptions
{
    public bool StripReadOnlySystemHidden { get; init; } = true;
    public bool TakeOwnershipAndResetAcl { get; init; } = true;
    public bool TerminateLockingProcesses { get; init; }
    public bool ScheduleRebootIfLocked { get; init; } = true;
    public bool SendToRecycleBinInstead { get; init; }
    public int DeleteRetryPasses { get; init; } = 3;
    public bool DryRun { get; init; }
    public bool ExplicitOverrideConfirmed { get; init; }
}

public sealed class ForceDeleteResult
{
    public int FilesDeleted { get; set; }
    public int DirectoriesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public int RebootScheduledCount { get; set; }
    public int ShieldedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ForceDeleteAttempt> Attempts { get; } = new();
    public string FormattedFreed => TargetFolderInfo.FormatBytes(BytesFreed);
}

public sealed class StubbornTargetProfile
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public bool IsDirectory { get; init; }

    public long SizeBytes { get; init; }
    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);
    public ForceDeleteGateDecision GateDecision { get; init; } = new(ForceDeleteTier.Allowed, string.Empty, string.Empty);
    public bool HasReadOnlyOrSystemAttributes { get; init; }
    public bool IsReparsePoint { get; init; }
    public List<LockingProcessInfo> LockingProcesses { get; init; } = new();
    public string SummaryIssues { get; init; } = string.Empty;
}

public static class ForceDeleteEngine
{
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFileAttributesW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileAttributesW(string lpFileName, uint dwFileAttributes);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveDirectoryW(string lpPathName);

    public static string EnsureLongPathPrefix(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) return path;
        if (path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\?\UNC\" + path.Substring(2);
        }
        return @"\\?\" + path;
    }

    public static bool ExistsLong(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string longPath = EnsureLongPathPrefix(path);
        uint attrs = GetFileAttributesW(longPath);
        return attrs != INVALID_FILE_ATTRIBUTES;
    }

    public static StubbornTargetProfile Profile(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return new StubbornTargetProfile
            {
                Path = targetPath ?? string.Empty,
                SummaryIssues = "Invalid or empty path."
            };
        }

        string normalized = PathSecurity.NormalizeCanonicalPath(targetPath);
        var decision = ForceDeleteGate.Evaluate(normalized);

        bool exists = ExistsLong(normalized);
        bool isDir = Directory.Exists(normalized);
        bool isFile = File.Exists(normalized);
        long size = 0;
        bool isReadOnly = false;
        bool isReparse = PathSecurity.IsReparsePointOrLink(normalized);
        var lockers = new List<LockingProcessInfo>();

        if (exists)
        {
            try
            {
                uint attrs = GetFileAttributesW(EnsureLongPathPrefix(normalized));
                if (attrs != INVALID_FILE_ATTRIBUTES)
                {
                    isReadOnly = (attrs & (0x01 | 0x04)) != 0;
                }
            }
            catch { }

            if (isFile)
            {
                try
                {
                    size = new FileInfo(normalized).Length;
                    lockers = RestartManagerService.GetLockingProcesses(normalized);
                }
                catch { }
            }
            else if (isDir)
            {
                try
                {
                    size = CalculateDirectorySize(normalized);
                }
                catch { }
            }
        }

        var issues = new List<string>();
        if (decision.Tier == ForceDeleteTier.AbsoluteBlock)
        {
            issues.Add("SYSTEM CRITICAL (BLOCKED)");
        }
        else if (decision.Tier == ForceDeleteTier.OverrideRequired)
        {
            issues.Add("Protected item (Consent needed)");
        }

        if (isReadOnly) issues.Add("Read-Only / System attribute");
        if (isReparse) issues.Add("Reparse Point / Symbolic Link");
        if (lockers.Count > 0)
        {
            issues.Add($"Locked by {lockers.Count} process(es): {string.Join(", ", lockers.Select(l => l.ProcessName))}");
        }

        return new StubbornTargetProfile
        {
            Path = normalized,
            Name = Path.GetFileName(normalized) switch
            {
                "" => normalized,
                var n => n
            },
            Exists = exists,
            IsDirectory = isDir,
            SizeBytes = size,
            GateDecision = decision,
            HasReadOnlyOrSystemAttributes = isReadOnly,
            IsReparsePoint = isReparse,
            LockingProcesses = lockers,
            SummaryIssues = issues.Count > 0 ? string.Join(" • ", issues) : "Ready for removal"
        };
    }

    private static long CalculateDirectorySize(string dirPath)
    {
        long total = 0;
        try
        {
            var di = new DirectoryInfo(dirPath);
            foreach (var fi in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { total += fi.Length; } catch { }
            }
        }
        catch { }
        return total;
    }
    public static async Task<ForceDeleteResult> ForceDeleteAsync(
        IEnumerable<string> targets,
        ForceDeleteOptions options,
        Action<string, LogLevel>? log = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ForceDeleteResult();
        var targetList = targets.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        int total = targetList.Count;
        if (total == 0) return result;

        ProcessPrivilegeService.EnableRequiredPrivileges();

        await Task.Run(() =>
        {
            int current = 0;
            foreach (var target in targetList)
            {
                if (ct.IsCancellationRequested) break;
                current++;
                progress?.Report((double)current / total);

                ExecuteSingleTarget(target, options, result, log);
            }
        }, ct);

        return result;
    }

    private static void ExecuteSingleTarget(
        string targetPath,
        ForceDeleteOptions options,
        ForceDeleteResult result,
        Action<string, LogLevel>? log)
    {
        string normalized = PathSecurity.NormalizeCanonicalPath(targetPath);
        var decision = ForceDeleteGate.Evaluate(normalized);

        if (decision.Tier == ForceDeleteTier.AbsoluteBlock)
        {
            result.ShieldedCount++;
            result.Attempts.Add(new ForceDeleteAttempt(normalized, ForceDeleteStage.Shielded, false, 0, decision.Rationale));
            log?.Invoke($"[Shield] Refused deletion of critical system target: {normalized} ({decision.Rationale})", LogLevel.Warning);
            return;
        }

        if (decision.Tier == ForceDeleteTier.OverrideRequired && !options.ExplicitOverrideConfirmed)
        {
            result.ShieldedCount++;
            result.Attempts.Add(new ForceDeleteAttempt(normalized, ForceDeleteStage.Shielded, false, 0, "Explicit override confirmation required."));
            log?.Invoke($"[Shield] Target requires explicit override confirmation: {normalized}", LogLevel.Warning);
            return;
        }

        if (!ExistsLong(normalized))
        {
            result.Attempts.Add(new ForceDeleteAttempt(normalized, ForceDeleteStage.Validate, true, 0, "Target does not exist."));
            return;
        }

        if (options.DryRun)
        {
            log?.Invoke($"[DryRun] Would force delete: {normalized}", LogLevel.Info);
            if (Directory.Exists(normalized))
            {
                result.DirectoriesDeleted++;
            }
            else
            {
                result.FilesDeleted++;
            }
            result.Attempts.Add(new ForceDeleteAttempt(normalized, ForceDeleteStage.Delete, true, 0, "Dry run simulated."));
            return;
        }

        if (options.SendToRecycleBinInstead)
        {
            bool recycled = CleanupExecutor.SendFileToRecycleBin(normalized);
            if (recycled && !ExistsLong(normalized))
            {
                result.FilesDeleted++;
                result.Attempts.Add(new ForceDeleteAttempt(normalized, ForceDeleteStage.Delete, true, 0, "Moved to Recycle Bin."));
                log?.Invoke($"Recycled: {normalized}", LogLevel.Success);
                return;
            }
        }

        bool isDirectory = Directory.Exists(normalized);

        if (PathSecurity.IsReparsePointOrLink(normalized))
        {
            DeleteReparsePointLink(normalized, isDirectory, result, log);
            return;
        }

        if (isDirectory)
        {
            DeleteDirectoryRecursive(normalized, options, result, log);
        }
        else
        {
            DeleteFileWithEscalation(normalized, options, result, log);
        }
    }

    private static void DeleteReparsePointLink(
        string path,
        bool isDirectory,
        ForceDeleteResult result,
        Action<string, LogLevel>? log)
    {
        try
        {
            // A read-only/system attribute on the link itself blocks Directory.Delete/File.Delete,
            // so strip it first. Only the link is touched — never its target.
            StripAttributes(path);

            if (isDirectory)
            {
                Directory.Delete(path, recursive: false);
                result.DirectoriesDeleted++;
            }
            else
            {
                File.Delete(path);
                result.FilesDeleted++;
            }
            result.Attempts.Add(new ForceDeleteAttempt(path, ForceDeleteStage.Delete, true, 0, "Reparse point link unlinked."));
            log?.Invoke($"Unlinked junction/symlink: {path}", LogLevel.Success);
        }
        catch (Exception ex)
        {
            result.FailedCount++;
            result.Attempts.Add(new ForceDeleteAttempt(path, ForceDeleteStage.Delete, false, Marshal.GetLastWin32Error(), ex.Message));
            log?.Invoke($"Failed to delete reparse point link '{path}': {ex.Message}", LogLevel.Error);
        }
    }

    private static void DeleteFileWithEscalation(
        string filePath,
        ForceDeleteOptions options,
        ForceDeleteResult result,
        Action<string, LogLevel>? log)
    {
        long fileSize = 0;
        try { fileSize = new FileInfo(filePath).Length; } catch { }

        if (options.StripReadOnlySystemHidden)
        {
            StripAttributes(filePath);
        }

        if (options.TakeOwnershipAndResetAcl)
        {
            ForceGrantPermissions(filePath, isDirectory: false);
        }

        if (options.TerminateLockingProcesses)
        {
            try
            {
                int killed = RestartManagerService.TerminateLockingProcesses(filePath, backgroundHelpersOnly: false);
                if (killed > 0)
                {
                    log?.Invoke($"Terminated {killed} process(es) locking '{Path.GetFileName(filePath)}'", LogLevel.Info);
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: the delete still proceeds and may succeed without lock termination.
                System.Diagnostics.Trace.WriteLine($"[ForceDelete] Lock termination failed for '{filePath}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        bool deleted = CleanupExecutor.DeletePermanently(filePath, out int win32Err, out string? failDetail);

        for (int pass = 1; !deleted && pass <= options.DeleteRetryPasses; pass++)
        {
            Thread.Sleep(60 * pass);
            StripAttributes(filePath);
            deleted = CleanupExecutor.DeletePermanently(filePath, out win32Err, out failDetail);
        }

        if (deleted && !ExistsLong(filePath))
        {
            result.FilesDeleted++;
            result.BytesFreed += fileSize;
            result.Attempts.Add(new ForceDeleteAttempt(filePath, ForceDeleteStage.Delete, true, 0, "Deleted permanently."));
            log?.Invoke($"Force deleted file: {Path.GetFileName(filePath)}", LogLevel.Success);
            return;
        }

        if (options.ScheduleRebootIfLocked && RestartManagerService.ScheduleRebootDeletion(filePath))
        {
            result.RebootScheduledCount++;
            result.Attempts.Add(new ForceDeleteAttempt(filePath, ForceDeleteStage.ScheduleReboot, true, win32Err, "Scheduled for deletion on next Windows reboot."));
            log?.Invoke($"Locked file scheduled for reboot purge: {Path.GetFileName(filePath)}", LogLevel.Warning);
            return;
        }

        result.FailedCount++;
        result.Attempts.Add(new ForceDeleteAttempt(filePath, ForceDeleteStage.Delete, false, win32Err, failDetail ?? "File remains locked or inaccessible."));
        log?.Invoke($"Failed to delete file '{Path.GetFileName(filePath)}': {failDetail ?? $"Win32 Error {win32Err}"}", LogLevel.Error);
    }

    private static void DeleteDirectoryRecursive(
        string dirPath,
        ForceDeleteOptions options,
        ForceDeleteResult result,
        Action<string, LogLevel>? log)
    {
        try
        {
            if (options.TakeOwnershipAndResetAcl)
            {
                ForceGrantPermissions(dirPath, isDirectory: true);
            }
            if (options.StripReadOnlySystemHidden)
            {
                StripAttributes(dirPath);
            }

            var di = new DirectoryInfo(dirPath);

            // Snapshot children with inaccessible entries ignored, so one locked or
            // ACL-denied subfolder cannot abort deletion of its healthy siblings.
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = 0
            };

            foreach (var fi in di.EnumerateFiles("*", enumerationOptions))
            {
                DeleteFileWithEscalation(fi.FullName, options, result, log);
            }

            foreach (var sub in di.EnumerateDirectories("*", enumerationOptions))
            {
                if (PathSecurity.IsReparsePointOrLink(sub.FullName))
                {
                    DeleteReparsePointLink(sub.FullName, isDirectory: true, result, log);
                }
                else
                {
                    DeleteDirectoryRecursive(sub.FullName, options, result, log);
                }
            }

            bool dirRemoved = false;
            try
            {
                Directory.Delete(dirPath, recursive: false);
                dirRemoved = true;
            }
            catch
            {
                dirRemoved = RemoveDirectoryW(EnsureLongPathPrefix(dirPath));
            }

            if (!dirRemoved && options.ScheduleRebootIfLocked)
            {
                if (RestartManagerService.ScheduleRebootDeletion(dirPath))
                {
                    result.RebootScheduledCount++;
                    result.Attempts.Add(new ForceDeleteAttempt(dirPath, ForceDeleteStage.ScheduleReboot, true, 0, "Directory scheduled for reboot purge."));
                    log?.Invoke($"Directory scheduled for reboot purge: {dirPath}", LogLevel.Warning);
                    return;
                }
            }

            if (dirRemoved)
            {
                result.DirectoriesDeleted++;
                result.Attempts.Add(new ForceDeleteAttempt(dirPath, ForceDeleteStage.Delete, true, 0, "Directory removed."));
            }
            else
            {
                result.FailedCount++;
                result.Attempts.Add(new ForceDeleteAttempt(dirPath, ForceDeleteStage.Delete, false, Marshal.GetLastWin32Error(), "Directory could not be removed."));
            }
        }
        catch (Exception ex)
        {
            result.FailedCount++;
            result.Attempts.Add(new ForceDeleteAttempt(dirPath, ForceDeleteStage.Delete, false, Marshal.GetLastWin32Error(), ex.Message));
            log?.Invoke($"Directory recursive cleanup error for '{dirPath}': {ex.Message}", LogLevel.Error);
        }
    }

    private static void StripAttributes(string path)
    {
        try
        {
            string longPath = EnsureLongPathPrefix(path);
            SetFileAttributesW(longPath, FILE_ATTRIBUTE_NORMAL);
            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch (Exception ex)
        {
            // Non-fatal: the native delete path below re-strips attributes itself.
            System.Diagnostics.Trace.WriteLine($"[ForceDelete] StripAttributes failed for '{path}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ForceGrantPermissions(string path, bool isDirectory)
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (identity.User == null) return;

            if (isDirectory)
            {
                var di = new DirectoryInfo(path);
                var ds = di.GetAccessControl();
                ds.SetOwner(identity.User);
                ds.AddAccessRule(new FileSystemAccessRule(
                    identity.User,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                di.SetAccessControl(ds);
            }
            else
            {
                var fi = new FileInfo(path);
                var fs = fi.GetAccessControl();
                fs.SetOwner(identity.User);
                fs.AddAccessRule(new FileSystemAccessRule(
                    identity.User,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                fi.SetAccessControl(fs);
            }
        }
        catch (Exception ex)
        {
            // Non-fatal: deletion is retried and ultimately falls back to reboot scheduling.
            System.Diagnostics.Trace.WriteLine($"[ForceDelete] Ownership grant failed for '{path}' (isDir={isDirectory}): {ex.GetType().Name}: {ex.Message}");
        }
    }
}
