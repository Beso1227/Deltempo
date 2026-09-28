using System.Diagnostics;

namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Central helper for recording deliberately-ignored failures.
/// <para>
/// Deltempo performs many best-effort operations (attribute probes, ACL grants,
/// process kills, registry reads) where a failure is non-fatal and the caller
/// deliberately continues. Swallowing those with a bare <c>catch { }</c> hides
/// real problems: a user can be told "cleanup complete" while a folder silently
/// failed, with no evidence of why.
/// </para>
/// <para>
/// These helpers keep the "ignore, but leave evidence" semantics while routing
/// the evidence into the rolling diagnostic log via <see cref="Trace"/>, which
/// App.InitializeDiagnostics() attaches in release builds. Still local-file only,
/// preserving the zero-telemetry promise.
/// </para>
/// </summary>
public static class SafeOps
{
    /// <summary>
    /// Runs an action that may fail harmlessly, tracing the failure if it throws.
    /// </summary>
    public static void Try(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{operation}] ignored: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs an action that may fail harmlessly, tracing the failure and returning
    /// <paramref name="fallback"/> when it throws.
    /// </summary>
    public static T Try<T>(string operation, Func<T> func, T fallback)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{operation}] ignored, using fallback: {ex.GetType().Name}: {ex.Message}");
            return fallback;
        }
    }

    /// <summary>
    /// Runs an action that may fail harmlessly, tracing the failure and returning false.
    /// </summary>
    public static bool TryBool(string operation, Func<bool> func) => Try(operation, func, false);
}
