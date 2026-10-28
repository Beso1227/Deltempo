using System.Diagnostics;
using System.IO;

namespace WinTempCleaner.Services;

/// <summary>
/// Public entry point for the size-rolled local file log so shells other than the
/// WPF app can attach it. <see cref="RollingFileTraceListener"/> stays internal with
/// <see cref="RollingFileTraceListener.TryCreate"/>; this static class is the
/// second shell's intended surface, not a second implementation.
/// </summary>
public static class DiagnosticLogFileService
{
    /// <summary>
    /// Attaches a rolling trace listener writing <c>path</c>, so the diagnostic
    /// traces emitted across the core are visible in released builds. Local file
    /// only — no network activity, preserving the zero-telemetry promise.
    /// Returns false when the location is unavailable; diagnostics must never
    /// break startup.
    /// </summary>
    public static bool TryAttach(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            var listener = RollingFileTraceListener.TryCreate(path);
            if (listener is null)
            {
                return false;
            }

            Trace.Listeners.Add(listener);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
