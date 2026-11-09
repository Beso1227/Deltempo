using System.Globalization;
using System.IO;
using WinTempCleaner.Core.Abstractions;

namespace WinTempCleaner.Services;

/// <summary>
/// Result of <c>DISM /Online /Cleanup-Image /AnalyzeComponentStore</c>, the same
/// analysis Windows Disk Cleanup performs before it offers "Windows Update Cleanup".
/// </summary>
public sealed record ComponentStoreAnalysis(
    long ActualSizeBytes,
    long SharedWithWindowsBytes,
    long BackupsAndDisabledFeaturesBytes,
    long CacheAndTemporaryDataBytes,
    int ReclaimablePackageCount,
    bool CleanupRecommended,
    DateTime? LastCleanupUtc)
{
    /// <summary>True when at least one size field was parsed; distinguishes an empty
    /// component store from unparseable output.</summary>
    public bool HasData => ActualSizeBytes > 0 || BackupsAndDisabledFeaturesBytes > 0 || CacheAndTemporaryDataBytes > 0;

    /// <summary>
    /// Bytes <c>/StartComponentCleanup</c> is expected to reclaim: superseded update
    /// backups plus the temporary servicing cache. This is the figure Disk Cleanup
    /// shows as "Windows Update Cleanup".
    /// </summary>
    public long EstimatedReclaimableBytes =>
        Math.Max(0, BackupsAndDisabledFeaturesBytes) + Math.Max(0, CacheAndTemporaryDataBytes);
}

/// <summary>
/// Parses and collects the DISM component-store analysis.
/// </summary>
/// <remarks>
/// The parser is deliberately English-only and the process is always invoked with
/// <c>/English</c>, because the surrounding strings ("Actual Size of Component Store")
/// are the only stable contract the tool offers. If a future build ignores /English the
/// parse yields no fields and callers report "analysis unavailable" instead of showing
/// a fabricated number.
/// </remarks>
public static class DismComponentStoreAnalyzer
{
    private static string GetDismPath()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // A 32-bit process on 64-bit Windows must use Sysnative or WOW64 redirection
        // sends it to the 32-bit DISM, which cannot service the online image.
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            string sysnative = Path.Combine(winDir, "Sysnative", "dism.exe");
            if (File.Exists(sysnative)) return sysnative;
        }

        return Path.Combine(winDir, "System32", "dism.exe");
    }

    /// <summary>
    /// Runs the analysis. Requires an elevated token (DISM exits 740 otherwise) and
    /// returns <c>null</c> rather than throwing, because every caller treats the
    /// analysis as advisory.
    /// </summary>
    public static async Task<ComponentStoreAnalysis?> AnalyzeAsync(CancellationToken ct = default)
    {
        try
        {
            string dismPath = GetDismPath();
            if (!File.Exists(dismPath)) return null;

            var result = await SystemProcessRunner.Instance.RunAsync(
                dismPath,
                "/English /Online /Cleanup-Image /AnalyzeComponentStore",
                TimeSpan.FromMinutes(10),
                ct).ConfigureAwait(false);

            // DISM sometimes reports the store as healthy yet returns a non-zero code
            // for a servicing lock. Only a successful run is trustworthy.
            if (result.TimedOut || result.ExitCode != 0) return null;

            return Parse(result.StandardOutput + "\n" + result.StandardError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] AnalyzeComponentStore failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Pure parser for the <c>/English</c> output. Returns <c>null</c> when no
    /// component-store field is present, so "DISM printed an error" and "the store is
    /// genuinely empty" stay distinguishable.
    /// </summary>
    public static ComponentStoreAnalysis? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        long actual = 0;
        long shared = 0;
        long backups = 0;
        long cache = 0;
        int packages = 0;
        bool recommended = false;
        DateTime? lastCleanup = null;
        bool sawAnyField = false;

        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            if (value.Length == 0) continue;

            if (key.Equals("Actual Size of Component Store", StringComparison.OrdinalIgnoreCase))
            {
                actual = ParseSize(value);
                sawAnyField = true;
            }
            else if (key.Equals("Shared with Windows", StringComparison.OrdinalIgnoreCase))
            {
                shared = ParseSize(value);
            }
            else if (key.Equals("Backups and Disabled Features", StringComparison.OrdinalIgnoreCase))
            {
                backups = ParseSize(value);
                sawAnyField = true;
            }
            else if (key.Equals("Cache and Temporary Data", StringComparison.OrdinalIgnoreCase))
            {
                cache = ParseSize(value);
                sawAnyField = true;
            }
            else if (key.Equals("Number of Reclaimable Packages", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    packages = parsed;
                    sawAnyField = true;
                }
            }
            else if (key.Equals("Component Store Cleanup Recommended", StringComparison.OrdinalIgnoreCase))
            {
                recommended = value.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) ||
                              value.StartsWith("True", StringComparison.OrdinalIgnoreCase);
                sawAnyField = true;
            }
            else if (key.Equals("Date of Last Cleanup", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime cleaned))
                {
                    lastCleanup = DateTime.SpecifyKind(cleaned, DateTimeKind.Local);
                }
            }
        }

        if (!sawAnyField) return null;

        return new ComponentStoreAnalysis(actual, shared, backups, cache, packages, recommended, lastCleanup);
    }

    /// <summary>
    /// Parses DISM size text ("9.57 GB", "475.23 MB") without depending on the current
    /// culture's decimal separator; DISM's English output always uses a dot.
    /// </summary>
    public static long ParseSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        string trimmed = value.Trim();
        long multiplier = 1;

        if (trimmed.EndsWith("TB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L * 1024 * 1024 * 1024;
        else if (trimmed.EndsWith("GB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L * 1024 * 1024;
        else if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L * 1024;
        else if (trimmed.EndsWith("KB", StringComparison.OrdinalIgnoreCase)) multiplier = 1024L;
        else if (trimmed.EndsWith("B", StringComparison.OrdinalIgnoreCase)) multiplier = 1;

        string number = new string(trimmed
            .TakeWhile(c => char.IsDigit(c) || c == '.' || c == ',')
            .ToArray())
            .Replace(',', '.');

        if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return (long)(parsed * multiplier);
        }

        return 0;
    }
}
