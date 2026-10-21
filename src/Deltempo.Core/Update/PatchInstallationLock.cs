namespace WinTempCleaner.Core.Update;

/// <summary>
/// Cross-process mutual exclusion lock for update operations.
/// Prevents concurrent update attempts between GUI and CLI instances.
/// </summary>
public sealed class PatchInstallationLock : IDisposable
{
    private const string MutexName = @"Local\Deltempo_Patch_Update_Lock";
    private readonly Mutex? _mutex;
    private readonly bool _hasLock;
    private bool _disposed;

    private PatchInstallationLock(Mutex? mutex, bool hasLock)
    {
        _mutex = mutex;
        _hasLock = hasLock;
    }

    public bool HasLock => _hasLock;

    /// <summary>
    /// Attempts to acquire the system-wide update lock.
    /// Returns a disposable lock instance. If HasLock is false, another update is active.
    /// </summary>
    public static PatchInstallationLock TryAcquire(TimeSpan timeout)
        => TryAcquire(MutexName, timeout);

    /// <summary>
    /// Attempts to acquire a named lock with a custom mutex name (for testing).
    /// </summary>
    public static PatchInstallationLock TryAcquire(string mutexName, TimeSpan timeout)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(false, mutexName);
            bool acquired;
            try
            {
                acquired = mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // Abandoned mutex is still acquired by the calling thread in .NET
                acquired = true;
            }

            if (!acquired)
            {
                mutex.Dispose();
                mutex = null;
            }

            return new PatchInstallationLock(mutex, acquired);
        }
        catch
        {
            mutex?.Dispose();
            return new PatchInstallationLock(null, false);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_hasLock && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }
                try
                {
                    _mutex.Dispose();
                }
                catch { }
            }
            else if (_mutex != null)
            {
                try
                {
                    _mutex.Dispose();
                }
                catch { }
            }
        }
    }
}
