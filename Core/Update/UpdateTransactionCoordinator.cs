using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WinTempCleaner.Core.Update;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// 13-stage update transaction coordinator with fail-closed behavior,
/// automatic rollback, and health check verification.
/// </summary>
public class UpdateTransactionCoordinator
{
    private const string UpdateMutexName = @"Local\Deltempo_Update_Tx_Lock";
    private static readonly TimeSpan DefaultProcessExitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultHealthCheckTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, int dwFlags);

    private const int MOVEFILE_REPLACE_EXISTING = 0x1;
    private const int MOVEFILE_COPY_ALLOWED = 0x2;

    private readonly TransactionJournal _journal;
    private readonly Action<string>? _logAction;
    private readonly TimeSpan _processExitTimeout;
    private readonly TimeSpan _healthCheckTimeout;
    private readonly TimeSpan _pollInterval;

    public UpdateTransactionCoordinator(
        TransactionJournal journal,
        Action<string>? logAction = null,
        TimeSpan? processExitTimeout = null,
        TimeSpan? healthCheckTimeout = null,
        TimeSpan? pollInterval = null)
    {
        _journal = journal;
        _logAction = logAction;
        _processExitTimeout = processExitTimeout ?? DefaultProcessExitTimeout;
        _healthCheckTimeout = healthCheckTimeout ?? DefaultHealthCheckTimeout;
        _pollInterval = pollInterval ?? DefaultPollInterval;

        if (_processExitTimeout <= TimeSpan.Zero || _healthCheckTimeout <= TimeSpan.Zero || _pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(processExitTimeout), "Timeouts and polling interval must be positive.");
    }

    private void Log(string msg)
    {
        _logAction?.Invoke($"[Deltempo:updater] {msg}");
        Trace.WriteLine($"[Deltempo:updater] {msg}");
    }

    /// <summary>
    /// Executes the full update transaction: wait for caller, backup, replace, verify, health check.
    /// </summary>
    public async Task<bool> ExecuteAsync(CancellationToken ct = default)
    {
        Mutex? mutex = null;
        try
        {
            // Stage 7: Acquire cross-process update mutex
            mutex = new Mutex(false, UpdateMutexName);
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(10)))
                {
                    Log("Could not acquire update mutex. Another update may be in progress.");
                    _journal.TransitionTo(TransactionState.Failed, "Mutex acquisition failed.");
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                // Abandoned mutex is still acquired
            }

            Log($"Transaction {_journal.TransactionId} started. State: {_journal.State}");

            // Stage 8: Wait for caller process to terminate
            if (!await WaitForCallerExitAsync(ct))
            {
                Log("Caller process did not exit within timeout. Failing closed.");
                _journal.TransitionTo(TransactionState.Failed, "Caller PID did not exit within timeout.");
                return false;
            }

            // Stage 9: Verify staged binary
            if (!File.Exists(_journal.StagedPath))
            {
                Log($"Staged file not found: {_journal.StagedPath}");
                _journal.TransitionTo(TransactionState.Failed, "Staged file missing.");
                return false;
            }

            var stagedInfo = new FileInfo(_journal.StagedPath);
            if (_journal.ExpectedSizeBytes > 0 && Math.Abs(stagedInfo.Length - _journal.ExpectedSizeBytes) > 1024)
            {
                Log($"Staged file size mismatch: expected {_journal.ExpectedSizeBytes}, got {stagedInfo.Length}.");
                _journal.TransitionTo(TransactionState.Failed, "Staged file size mismatch.");
                return false;
            }

            // Stage 10: Backup current executable and free target path
            _journal.TransitionTo(TransactionState.BackupCreated);
            Log($"Backing up current executable to {_journal.BackupPath}");

            string localOldPath = $"{_journal.TargetPath}.old";
            RetryFileAction(() =>
            {
                if (File.Exists(localOldPath))
                    File.Delete(localOldPath);
            });

            try
            {
                if (File.Exists(_journal.TargetPath))
                {
                    // Copy to BackupPath in updatesDir for persistent disaster recovery
                    RetryFileAction(() =>
                    {
                        if (File.Exists(_journal.BackupPath))
                            File.Delete(_journal.BackupPath);
                        File.Copy(_journal.TargetPath, _journal.BackupPath, true);
                    });

                    // Rename TargetPath to TargetPath.old in same directory (frees TargetPath name immediately)
                    bool renamed = RetryFileAction(() =>
                    {
                        File.Move(_journal.TargetPath, localOldPath, overwrite: true);
                    });

                    if (renamed)
                    {
                        Log($"Renamed current executable to {localOldPath}");
                    }
                    else
                    {
                        Log("Rename to local .old fallback could not complete; will attempt direct overwrite in Stage 11.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Backup failed: {ex.Message}");
                _journal.TransitionTo(TransactionState.Failed, $"Backup failed: {ex.Message}");
                return false;
            }

            // Stage 11: Install new executable
            _journal.TransitionTo(TransactionState.InstallStarted);
            Log($"Installing {_journal.StagedPath} to {_journal.TargetPath}");

            bool moveOk = RetryFileAction(() =>
            {
                File.Copy(_journal.StagedPath, _journal.TargetPath, overwrite: true);
            });

            if (moveOk)
            {
                Log("File.Copy to TargetPath succeeded.");
            }
            else
            {
                Log("File.Copy failed. Attempting File.Move fallback.");
                moveOk = RetryFileAction(() =>
                {
                    File.Move(_journal.StagedPath, _journal.TargetPath, overwrite: true);
                });

                if (moveOk)
                {
                    Log("File.Move to TargetPath succeeded.");
                }
                else
                {
                    Log("File.Move failed. Attempting MoveFileEx fallback.");
                    moveOk = MoveFileExW(_journal.StagedPath, _journal.TargetPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED);
                }
            }

            if (!moveOk)
            {
                Log("File replacement failed. Rolling back.");
                await RollbackAsync(null);
                return false;
            }

            // Stage 12: Verify installed binary
            if (!File.Exists(_journal.TargetPath))
            {
                Log("Installed file not found after replacement.");
                await RollbackAsync(null);
                return false;
            }

            var installedInfo = new FileInfo(_journal.TargetPath);
            if (_journal.ExpectedSizeBytes > 0 && Math.Abs(installedInfo.Length - _journal.ExpectedSizeBytes) > 1024)
            {
                Log($"Installed file size mismatch: expected {_journal.ExpectedSizeBytes}, got {installedInfo.Length}.");
                await RollbackAsync(null);
                return false;
            }

            _journal.TransitionTo(TransactionState.Installed);
            Log("Binary installed successfully.");

            // Create health handshake named event BEFORE launching updated binary
            string eventName = $"Local\\Deltempo_Health_{_journal.TransactionId}";
            using var healthEvent = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

            // Stage 13: Launch updated binary with --update-handshake
            Log("Launching updated binary with health handshake.");
            var psi = new ProcessStartInfo
            {
                FileName = _journal.TargetPath,
                Arguments = $"--update-handshake {_journal.TransactionId}",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(_journal.TargetPath) ?? ""
            };

            Process? launchedProc = null;
            try
            {
                launchedProc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log($"Failed to launch updated binary: {ex.Message}");
                await RollbackAsync(null);
                return false;
            }

            _journal.TransitionTo(TransactionState.Launched);

            // Wait for health check signal (EventWaitHandle or health.signal file, monitoring for early crash)
            bool healthOk = await WaitForHealthCheckAsync(healthEvent, launchedProc, ct);
            if (!healthOk)
            {
                Log("Health check failed, timed out, or process terminated prematurely. Rolling back.");
                await RollbackAsync(launchedProc);
                return false;
            }

            // Stage 15: Commit transaction
            _journal.TransitionTo(TransactionState.HealthCheckPassed);
            _journal.TransitionTo(TransactionState.Committed);
            Log("Transaction committed successfully.");

            // Clean up staged artifact immediately upon commit
            try
            {
                if (File.Exists(_journal.StagedPath))
                    File.Delete(_journal.StagedPath);
                string stagedDir = Path.GetDirectoryName(_journal.StagedPath)!;
                if (Directory.Exists(stagedDir) && Directory.GetFileSystemEntries(stagedDir).Length == 0)
                    Directory.Delete(stagedDir);
            }
            catch { }

            // Schedule backup cleanup (delayed to avoid locking)
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    if (File.Exists(localOldPath))
                        File.Delete(localOldPath);
                }
                catch { }
                try
                {
                    if (File.Exists(_journal.BackupPath))
                        File.Delete(_journal.BackupPath);
                }
                catch { }
            });

            return true;
        }
        finally
        {
            try { mutex?.ReleaseMutex(); } catch { }
            try { mutex?.Dispose(); } catch { }
        }
    }

    private static bool RetryFileAction(Action action, int maxAttempts = 5, int delayMs = 200)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                action();
                return true;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }
        return false;
    }

    private async Task<bool> WaitForCallerExitAsync(CancellationToken ct)
    {
        if (_journal.CallerPid <= 0)
            return true; // No PID to wait for

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < _processExitTimeout)
        {
            if (ct.IsCancellationRequested)
                return false;

            try
            {
                using var proc = Process.GetProcessById(_journal.CallerPid);
                if (proc.HasExited)
                {
                    Log($"Caller process {_journal.CallerPid} has exited.");
                    await Task.Delay(200, ct);
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                Log($"Caller process {_journal.CallerPid} no longer exists (exited).");
                await Task.Delay(200, ct);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Process has exited but PID slot still transitioning
                Log($"Caller process {_journal.CallerPid} no longer accessible (exited).");
                await Task.Delay(200, ct);
                return true;
            }

            await Task.Delay(_pollInterval, ct);
        }

        return false;
    }

    private async Task<bool> WaitForHealthCheckAsync(EventWaitHandle? healthEvent, Process? launchedProc, CancellationToken ct)
    {
        string healthSignalPath = _journal.GetHealthSignalPath();
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < _healthCheckTimeout)
        {
            if (ct.IsCancellationRequested)
                return false;

            // 1. Check EventWaitHandle signal
            if (healthEvent != null)
            {
                try
                {
                    if (healthEvent.WaitOne(0))
                    {
                        Log("Health check EventWaitHandle signaled.");
                        return true;
                    }
                }
                catch { }
            }

            // 2. Check for health.signal file
            if (File.Exists(healthSignalPath))
            {
                Log("Health signal file detected.");
                return true;
            }

            // 3. Check if launched process exited prematurely without health signal
            if (launchedProc != null)
            {
                try
                {
                    if (launchedProc.HasExited)
                    {
                        // Check one final time if file signal exists
                        if (File.Exists(healthSignalPath))
                        {
                            Log("Health signal file detected upon process exit.");
                            return true;
                        }

                        Log($"Launched process terminated prematurely with exit code {launchedProc.ExitCode}.");
                        return false;
                    }
                }
                catch { }
            }

            if (healthEvent != null)
            {
                int waitIndex = WaitHandle.WaitAny([healthEvent, ct.WaitHandle], _pollInterval);
                if (waitIndex == 0)
                {
                    Log("Health check EventWaitHandle signaled during wait.");
                    return true;
                }
            }
            else
            {
                await Task.Delay(_pollInterval, ct);
            }
        }

        return false;
    }

    public Task RollbackAsync() => RollbackAsync(null);

    private Task RollbackAsync(Process? launchedProc)
    {
        Log("Initiating rollback.");
        _journal.TransitionTo(TransactionState.RolledBack);

        // Terminate unresponsive or crashing updated process to release locks on TargetPath
        if (launchedProc != null)
        {
            try
            {
                if (!launchedProc.HasExited)
                {
                    Log($"Terminating launched process PID {launchedProc.Id} to release target binary file lock...");
                    launchedProc.Kill(entireProcessTree: true);
                    launchedProc.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log($"Warning: could not terminate launched process: {ex.Message}");
            }
        }

        string localOldPath = $"{_journal.TargetPath}.old";
        string? sourceBackup = File.Exists(localOldPath) ? localOldPath :
                               (File.Exists(_journal.BackupPath) ? _journal.BackupPath : null);

        if (string.IsNullOrEmpty(sourceBackup))
        {
            Log("No backup available for rollback.");
            return Task.CompletedTask;
        }

        try
        {
            bool restored = RetryFileAction(() =>
            {
                File.Copy(sourceBackup, _journal.TargetPath, overwrite: true);
            });

            if (!restored)
            {
                bool ok = MoveFileExW(sourceBackup, _journal.TargetPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED);
                if (!ok)
                {
                    RetryFileAction(() =>
                    {
                        File.Move(sourceBackup, _journal.TargetPath, overwrite: true);
                    });
                }
            }

            // Verify restored binary is valid before launching
            if (!File.Exists(_journal.TargetPath))
            {
                Log("CRITICAL: Restored binary does not exist after rollback move.");
                _journal.TransitionTo(TransactionState.Failed, "Restored binary missing after rollback.");
                return Task.CompletedTask;
            }

            var fi = new FileInfo(_journal.TargetPath);
            if (fi.Length == 0)
            {
                Log("CRITICAL: Restored binary is empty (0 bytes) after rollback.");
                _journal.TransitionTo(TransactionState.Failed, "Restored binary is empty after rollback.");
                return Task.CompletedTask;
            }

            // Verify PE header of restored binary
            using (var fs = new FileStream(_journal.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] mz = new byte[2];
                if (fs.Read(mz, 0, 2) != 2 || mz[0] != 0x4D || mz[1] != 0x5A)
                {
                    Log("CRITICAL: Restored binary is not a valid PE executable after rollback.");
                    _journal.TransitionTo(TransactionState.Failed, "Restored binary is not a valid PE after rollback.");
                    return Task.CompletedTask;
                }
            }

            Log($"Rollback verified. Previous binary restored ({fi.Length} bytes).");

            // Launch previous binary
            var psi = new ProcessStartInfo
            {
                FileName = _journal.TargetPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(_journal.TargetPath) ?? ""
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"Rollback failed: {ex.Message}");
            _journal.TransitionTo(TransactionState.Failed, $"Rollback failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
