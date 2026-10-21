namespace WinTempCleaner.Services;

/// <summary>
/// Runs a callback on the UI thread of whichever UI framework is hosting these shared
/// services.
///
/// The services used to hard-code WPF (<c>System.Windows.Application.Current.Dispatcher</c>).
/// Under the WinUI 3 shell that property is null, so the fallback ran the callback inline
/// on a worker thread. Raising <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// from there crosses into WinRT and fails with RPC_E_WRONG_THREAD (0x8001010E) - which is
/// exactly how the first Cleaner scan in the WinUI shell died: every target kept its 0 B
/// placeholder because the size assignments were rejected before they landed.
///
/// The host installs its own marshaler once at startup (the WinUI shell installs a
/// DispatcherQueue marshaler from App.OnLaunched). With no host installed the callback runs
/// inline, which is what the CLI and the test suite rely on.
/// </summary>
public static class UiDispatcher
{
    private static Action<Action>? _invoker;

    /// <summary>
    /// Installs the host's UI-thread marshaler. Call once, from the UI thread.
    /// </summary>
    public static void Install(Action<Action> invoker)
    {
        ArgumentNullException.ThrowIfNull(invoker);

        _invoker = invoker;
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the host UI thread, or inline when no host has
    /// installed a marshaler.
    ///
    /// The marshaler is synchronous on purpose: callers read the value back immediately
    /// afterwards (CleanFolderAsync adjusts <c>folder.SizeBytes</c> based on its own
    /// notification), so queuing the work and returning would race them. WPF's
    /// Dispatcher.Invoke behaved the same way.
    /// </summary>
    public static void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Action<Action>? invoker = _invoker;
        if (invoker is null)
        {
            action();
            return;
        }

        invoker(action);
    }
}
