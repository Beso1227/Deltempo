using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WinTempCleaner.Core.Update;

namespace WinTempCleaner.Updater;

/// <summary>
/// 13-stage update transaction coordinator with fail-closed behavior,
/// automatic rollback, and health check verification.
/// </summary>
public class UpdateTransactionCoordinator
{
    private const string UpdateMutexName = @"Local\Deltempo_Update_Tx_Lock";
    private const int ProcessExitTimeoutMs = 30000;
    private const int HealthCheckTimeoutMs = 30000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, int dwFlags);

    private const int MOVEFILE_REPLACE_EXISTING = 0x1;
    private const int MOVEFILE_COPY_ALLOWED = 0x2;

    private readonly TransactionJournal _journal;
    private readonly Action<string>? _logAction;

    public UpdateTransactionCoordinator(TransactionJournal journal, Action<string>? logAction = null)
    {
        _journal = journal;
        _logAction = logAction;
    }

    private void Log(string msg)
    {
        _logAction?.Invoke($"[DeltempoUpdater] {msg}");
        Trace.WriteLine($"[DeltempoUpdater] {msg}");
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

            // Stage 10: Backup current executable
            _journal.TransitionTo(TransactionState.BackupCreated);
            Log($"Backing up current executable to {_journal.BackupPath}");

            try
            {
                if (File.Exists(_journal.TargetPath))
                {
                    if (File.Exists(_journal.BackupPath))
                        File.Delete(_journal.BackupPath);

                    File.Copy(_journal.TargetPath, _journal.BackupPath, false);
                }
            }
            catch (Exception ex)
            {
                Log($"Backup failed: {ex.Message}");
                _journal.TransitionTo(TransactionState.Failed, $"Backup failed: {ex.Message}");
                return false;
            }

            // Stage 11: Replace executable using atomic MoveFileEx
            _journal.TransitionTo(TransactionState.InstallStarted);
            Log($"Replacing {_journal.TargetPath} with {_journal.StagedPath}");

            bool moveOk = MoveFileExW(_journal.StagedPath, _journal.TargetPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED);
            if (!moveOk)
            {
                Log("MoveFileEx failed. Attempting File.Move fallback.");
                try
                {
                    File.Move(_journal.StagedPath, _journal.TargetPath, overwrite: true);
                    moveOk = true;
                }
                catch (Exception ex)
                {
                    Log($"File.Move fallback failed: {ex.Message}");
                }
            }

            if (!moveOk)
            {
                Log("File replacement failed. Rolling back.");
                await RollbackAsync();
                return false;
            }

            // Stage 12: Verify installed binary
            if (!File.Exists(_journal.TargetPath))
            {
                Log("Installed file not found after replacement.");
                await RollbackAsync();
                return false;
            }

            var installedInfo = new FileInfo(_journal.TargetPath);
            if (_journal.ExpectedSizeBytes > 0 && Math.Abs(installedInfo.Length - _journal.ExpectedSizeBytes) > 1024)
            {
                Log($"Installed file size mismatch: expected {_journal.ExpectedSizeBytes}, got {installedInfo.Length}.");
                await RollbackAsync();
                return false;
            }

            _journal.TransitionTo(TransactionState.Installed);
            Log("Binary installed successfully.");

            // Stage 13: Launch updated binary with --update-handshake
            Log("Launching updated binary with health handshake.");
            var psi = new ProcessStartInfo
            {
                FileName = _journal.TargetPath,
                Arguments = $"--update-handshake {_journal.TransactionId}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log($"Failed to launch updated binary: {ex.Message}");
                await RollbackAsync();
                return false;
            }

            _journal.TransitionTo(TransactionState.Launched);

            // Wait for health check signal
            bool healthOk = await WaitForHealthCheckAsync(ct);
            if (!healthOk)
            {
                Log("Health check failed or timed out. Rolling back.");
                await RollbackAsync();
                return false;
            }

            // Stage 15: Commit transaction
            _journal.TransitionTo(TransactionState.HealthCheckPassed);
            _journal.TransitionTo(TransactionState.Committed);
            Log("Transaction committed successfully.");

            // Schedule backup cleanup (delayed to avoid locking)
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
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

    private async Task<bool> WaitForCallerExitAsync(CancellationToken ct)
    {
        if (_journal.CallerPid <= 0)
            return true; // No PID to wait for

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ProcessExitTimeoutMs)
        {
            if (ct.IsCancellationRequested)
                return false;

            try
            {
                var proc = Process.GetProcessById(_journal.CallerPid);
                if (proc.HasExited)
                {
                    Log($"Caller process {_journal.CallerPid} has exited.");
                    return true;
                }
                proc.Dispose();
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                Log($"Caller process {_journal.CallerPid} no longer exists (exited).");
                return true;
            }
            catch (InvalidOperationException)
            {
                // Process has exited but PID slot still transitioning
                Log($"Caller process {_journal.CallerPid} no longer accessible (exited).");
                return true;
            }

            await Task.Delay(500, ct);
        }

        return false;
    }

    private async Task<bool> WaitForHealthCheckAsync(CancellationToken ct)
    {
        string healthSignalPath = _journal.GetHealthSignalPath();
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < HealthCheckTimeoutMs)
        {
            if (ct.IsCancellationRequested)
                return false;

            // Check for health.signal file
            if (File.Exists(healthSignalPath))
            {
                Log("Health signal file detected.");
                return true;
            }

            await Task.Delay(500, ct);
        }

        return false;
    }

    private async Task RollbackAsync()
    {
        Log("Initiating rollback.");
        _journal.TransitionTo(TransactionState.RolledBack);

        if (string.IsNullOrEmpty(_journal.BackupPath) || !File.Exists(_journal.BackupPath))
        {
            Log("No backup available for rollback.");
            return;
        }

        try
        {
            bool ok = MoveFileExW(_journal.BackupPath, _journal.TargetPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED);
            if (!ok)
            {
                File.Move(_journal.BackupPath, _journal.TargetPath, overwrite: true);
            }

            // Verify restored binary is valid before launching
            if (!File.Exists(_journal.TargetPath))
            {
                Log("CRITICAL: Restored binary does not exist after rollback move.");
                _journal.TransitionTo(TransactionState.Failed, "Restored binary missing after rollback.");
                return;
            }

            var fi = new FileInfo(_journal.TargetPath);
            if (fi.Length == 0)
            {
                Log("CRITICAL: Restored binary is empty (0 bytes) after rollback.");
                _journal.TransitionTo(TransactionState.Failed, "Restored binary is empty after rollback.");
                return;
            }

            // Verify PE header of restored binary
            using (var fs = new FileStream(_journal.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] mz = new byte[2];
                if (fs.Read(mz, 0, 2) != 2 || mz[0] != 0x4D || mz[1] != 0x5A)
                {
                    Log("CRITICAL: Restored binary is not a valid PE executable after rollback.");
                    _journal.TransitionTo(TransactionState.Failed, "Restored binary is not a valid PE after rollback.");
                    return;
                }
            }

            Log($"Rollback verified. Previous binary restored ({fi.Length} bytes).");

            // Launch previous binary
            var psi = new ProcessStartInfo
            {
                FileName = _journal.TargetPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"Rollback failed: {ex.Message}");
            _journal.TransitionTo(TransactionState.Failed, $"Rollback failed: {ex.Message}");
        }
    }
}
