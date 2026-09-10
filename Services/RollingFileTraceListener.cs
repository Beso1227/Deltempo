using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinTempCleaner.Services;

/// <summary>
/// Trace listener writing to a size-rolled local file so the diagnostic traces
/// emitted across the core are visible in released builds (previously they were
/// Debug-only). Local file only — no network activity, preserving the
/// zero-telemetry promise.
/// </summary>
internal sealed class RollingFileTraceListener : TextWriterTraceListener
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private const int MaxBackups = 3;

    private readonly object _gate = new();

    /// <summary>
    /// Creates the listener (rolling if needed), or returns null when the log
    /// location is unavailable — diagnostics must never break startup.
    /// </summary>
    public static RollingFileTraceListener? TryCreate(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            RollIfNeeded(path);
            return new RollingFileTraceListener(path);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Diagnostics] File trace listener unavailable: {ex.Message}");
            return null;
        }
    }

    private RollingFileTraceListener(string path)
        : base(new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true })
    {
    }

    private static void RollIfNeeded(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length < MaxBytes) return;

        // deltempo.log -> deltempo.1.log -> deltempo.2.log -> deltempo.3.log (delete)
        string oldest = Path.ChangeExtension(path, $".{MaxBackups}.log");
        if (File.Exists(oldest)) File.Delete(oldest);

        for (int i = MaxBackups - 1; i >= 1; i--)
        {
            string source = Path.ChangeExtension(path, $".{i}.log");
            if (File.Exists(source))
            {
                File.Move(source, Path.ChangeExtension(path, $".{i + 1}.log"));
            }
        }

        File.Move(path, Path.ChangeExtension(path, ".1.log"));
    }

    public override void Write(string? message)
    {
        lock (_gate)
        {
            base.Write($"{DateTime.UtcNow:O} {message}");
        }
    }

    public override void WriteLine(string? message)
    {
        lock (_gate)
        {
            base.WriteLine($"{DateTime.UtcNow:O} {message}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                Writer?.Flush();
            }
        }

        base.Dispose(disposing);
    }
}
