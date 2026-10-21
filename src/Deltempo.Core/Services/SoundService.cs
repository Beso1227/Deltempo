using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace WinTempCleaner.Services;

/// <summary>Semantic events the shell can acknowledge with sound.</summary>
public enum SoundCue
{
    /// <summary>Any discrete user action: button, tab, filter, row toggle.</summary>
    Click,

    /// <summary>An operation finished and the result is good.</summary>
    Success,

    /// <summary>An operation failed, was denied, or the user aborted it.</summary>
    Error,

    /// <summary>The operation completed but something needs attention.</summary>
    Warning,
}

/// <summary>
/// Plays the app's interface sounds.
///
/// The voices are professionally produced samples from Kenney's "Interface Sounds"
/// pack (CC0 1.0 - free for commercial use), embedded in the assembly rather than
/// synthesised at runtime. Provenance and the reasoning behind the selection are
/// recorded in Assets/Sounds/CREDITS.md.
///
/// Two things are still done to each sample, once, at load time:
///
///   1. Peak normalisation. The source files were mastered to different levels, so
///      every cue is scaled to its own target. Without this the click would either
///      vanish or dominate depending on which sample it happened to be.
///   2. A hard clamp below full scale, so no cue can clip on playback.
///
/// The prepared bytes are cached, so an interaction replays a finished buffer
/// instead of re-reading and re-scaling a resource.
/// </summary>
public static class SoundService
{
    /// <summary>Master switch. When false every cue is a no-op.</summary>
    public static bool IsSoundEnabled { get; set; } = true;

    /// <summary>Absolute ceiling as a fraction of full scale. Nothing may exceed it.</summary>
    internal const double PeakCeiling = 0.89;

    private const string ResourcePrefix = "WinTempCleaner.Assets.Sounds.";

    private static readonly ConcurrentDictionary<SoundCue, byte[]> CueCache = new();

    /// <summary>
    /// Target peak per cue. The click fires on nearly every interaction, so it is held
    /// below the result cues, which fire once per operation.
    /// </summary>
    private static double TargetPeak(SoundCue cue) => cue switch
    {
        SoundCue.Click => 0.50,
        SoundCue.Warning => 0.62,
        SoundCue.Error => 0.66,
        SoundCue.Success => 0.70,
        _ => 0.60,
    };

    private static string ResourceName(SoundCue cue) => cue switch
    {
        SoundCue.Click => ResourcePrefix + "click.wav",
        SoundCue.Success => ResourcePrefix + "success.wav",
        SoundCue.Error => ResourcePrefix + "error.wav",
        SoundCue.Warning => ResourcePrefix + "warning.wav",
        _ => ResourcePrefix + "click.wav",
    };

    // ------------------------------------------------------------------
    // Public playback API
    // ------------------------------------------------------------------

    public static void PlayClickSound() => Play(SoundCue.Click);

    public static void PlaySuccessSound() => Play(SoundCue.Success);

    public static void PlayErrorSound() => Play(SoundCue.Error);

    public static void PlayWarningSound() => Play(SoundCue.Warning);

    private static void Play(SoundCue cue)
    {
        if (!IsSoundEnabled) return;

        byte[] wav;
        try
        {
            wav = CueCache.GetOrAdd(cue, Prepare);
        }
        catch (Exception ex)
        {
            // Audio is never important enough to take the app down.
            Trace.WriteLine($"[Deltempo] Sound load failed for {cue}: {ex.Message}");
            return;
        }

        Task.Run(() =>
        {
            try
            {
                using var stream = new MemoryStream(wav, writable: false);
                using var player = new SoundPlayer(stream);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Sound playback suppressed for {cue}: {ex.Message}");
            }
        });
    }

    // ------------------------------------------------------------------
    // Resource preparation
    // ------------------------------------------------------------------

    /// <summary>Reads a cue's embedded sample and returns normalised, clamped bytes.</summary>
    internal static byte[] Prepare(SoundCue cue)
    {
        string name = ResourceName(cue);

        // Resolved against the assembly this type lives in. The same source file is
        // compiled into both the GUI assembly and Deltempo.Core, and each carries its
        // own copy of the resources under this exact name.
        using Stream stream = typeof(SoundService).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded sound '{name}' is missing.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Normalize(buffer.ToArray(), TargetPeak(cue));
    }

    /// <summary>
    /// Scales every sample so the loudest peak hits <paramref name="targetPeak"/>, then
    /// clamps below full scale. Exposed so tests can assert on the prepared audio.
    /// </summary>
    internal static byte[] Normalize(byte[] wav, double targetPeak)
    {
        if (!TryReadFormat(wav, out int offset, out int length, out int bytesPerSample))
            throw new InvalidOperationException("Sound asset is not a readable PCM WAV.");

        if (bytesPerSample != 2)
            throw new InvalidOperationException($"Expected 16-bit PCM, found {bytesPerSample * 8}-bit.");

        float peak = 0f;
        for (int i = offset; i + 1 < offset + length; i += 2)
            peak = Math.Max(peak, Math.Abs((float)BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(i))));

        if (peak <= 0f)
            throw new InvalidOperationException("Sound asset contains only silence.");

        // peak is in raw 16-bit sample units while targetPeak is a 0-1 fraction, so the
        // target has to be converted before scaling. Mixing the two units silently
        // collapses every sample to zero.
        double scale = (targetPeak * short.MaxValue) / peak;
        int ceiling = (int)(short.MaxValue * PeakCeiling);
        var result = (byte[])wav.Clone();

        for (int i = offset; i + 1 < offset + length; i += 2)
        {
            int sample = BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan(i));
            int scaled = (int)Math.Round(sample * scale);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(i), (short)Math.Clamp(scaled, -ceiling, ceiling));
        }

        return result;
    }

    /// <summary>Duration of a prepared cue, in whole milliseconds.</summary>
    internal static int CueDurationMs(SoundCue cue)
    {
        byte[] wav = CueCache.GetOrAdd(cue, Prepare);
        TryReadFormat(wav, out int offset, out int length, out int bytesPerSample);
        int sampleRate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24));
        return (int)(1000.0 * (length / (double)bytesPerSample) / sampleRate);
    }

    /// <summary>
    /// Walks the RIFF chunk list to find the format and data segments. These files carry
    /// a LIST/INFO metadata chunk between "fmt " and "data", so the data chunk is not at a
    /// fixed offset and the header cannot be assumed.
    /// </summary>
    private static bool TryReadFormat(byte[] wav, out int offset, out int length, out int bytesPerSample)
    {
        offset = length = bytesPerSample = 0;
        if (wav.Length < 44) return false;

        int sampleRate = 0;
        int pos = 12;

        while (pos + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(pos + 4));

            if (id == "fmt ")
            {
                short channels = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(pos + 10));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(pos + 12));
                short bits = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(pos + 22));
                bytesPerSample = channels * bits / 8;
            }
            else if (id == "data")
            {
                offset = pos + 8;
                length = Math.Min(size, wav.Length - offset);
                if (length <= 0 || bytesPerSample <= 0 || sampleRate <= 0) return false;
                return true;
            }

            if (size <= 0) return false;
            pos += 8 + size + (size % 2);
        }

        return false;
    }
}
