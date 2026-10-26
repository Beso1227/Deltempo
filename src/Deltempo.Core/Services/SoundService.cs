using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Media;
using WinTempCleaner.Models;

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
/// The prepared bytes are cached, and so is a loaded <see cref="SoundPlayer"/> per cue,
/// so an interaction replays a finished buffer instead of re-reading, re-scaling and
/// re-loading a resource.
///
/// Playback itself goes through one lane (<see cref="SoundLane"/>), because two sounds
/// starting at the same moment do not layer into "richer audio", they layer into mush.
/// A click never stacks on itself and never cuts a result cue short; a result cue always
/// sounds and takes the lane from whatever tick was under it.
/// </summary>
public static class SoundService
{
    /// <summary>Absolute ceiling as a fraction of full scale. Nothing may exceed it.</summary>
    internal const double PeakCeiling = 0.89;

    private const string ResourcePrefix = "WinTempCleaner.Assets.Sounds.";

    private static readonly ConcurrentDictionary<SoundCue, byte[]> CueCache = new();

    /// <summary>
    /// One loaded player per cue. <see cref="SoundPlayer.Load"/> reads the WAV once;
    /// after that <see cref="SoundPlayer.Play"/> returns immediately and playback runs
    /// inside winmm, so a click costs a hand-off instead of a thread and a reparse.
    /// </summary>
    private static readonly ConcurrentDictionary<SoundCue, SoundPlayer> Players = new();

    private static readonly SoundLane Lane = new();

    private static bool _isSoundEnabled = true;

    /// <summary>
    /// Master switch. When false every cue is a no-op, and a cue already sounding is
    /// stopped - muting should be immediate, not "after this chime finishes".
    /// </summary>
    public static bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set
        {
            if (_isSoundEnabled == value) return;

            _isSoundEnabled = value;
            if (!value) Silence();
        }
    }

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

    /// <summary>
    /// The cue for a finished cleanup, decided in one place so the same run cannot sound
    /// different depending on which shell is open.
    ///
    /// Freeing nothing because the selection was already clean is a warning, not a
    /// success: the user pressed Clean and the answer is "there was nothing to do".
    /// Failures with nothing freed is the only error - a partial success still succeeded.
    /// </summary>
    public static void PlayCleanResultSound(CleanSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Play(CleanResultCue(summary));
    }

    /// <summary>
    /// Which cue a finished cleanup has earned. Separate from playback so the mapping can
    /// be tested without an audio device.
    /// </summary>
    internal static SoundCue CleanResultCue(CleanSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        int needsAttention = summary.TotalFilesSkipped
            + summary.TotalFilesInUse
            + summary.TotalFilesRecentShielded
            + summary.TotalFilesPolicyProtected;

        if (summary.TotalFilesFailed > 0 && summary.TotalFreedBytes <= 0)
        {
            return SoundCue.Error;
        }

        if (summary.TotalFilesFailed > 0 || needsAttention > 0 || summary.TotalFreedBytes <= 0)
        {
            return SoundCue.Warning;
        }

        return SoundCue.Success;
    }

    private static void Play(SoundCue cue)
    {
        if (!_isSoundEnabled) return;

        SoundPlayer player;
        int durationMs;
        try
        {
            player = Players.GetOrAdd(cue, CreatePlayer);
            durationMs = CueDurationMs(cue);
        }
        catch (Exception ex)
        {
            // Audio is never important enough to take the app down.
            Trace.WriteLine($"[Deltempo] Sound load failed for {cue}: {ex.Message}");
            return;
        }

        lock (Lane)
        {
            if (Lane.Admit(cue, durationMs, Environment.TickCount64, out SoundCue? interrupted)
                == LaneDecision.Drop)
            {
                return;
            }

            if (interrupted is { } previous && Players.TryGetValue(previous, out SoundPlayer? playing))
            {
                Stop(playing);
            }

            try
            {
                player.Play();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Sound playback suppressed for {cue}: {ex.Message}");
                Lane.Release();
            }
        }
    }

    /// <summary>Stops every cue, and forgets what was sounding.</summary>
    private static void Silence()
    {
        lock (Lane)
        {
            foreach (SoundPlayer player in Players.Values)
            {
                Stop(player);
            }

            Lane.Release();
        }
    }

    private static void Stop(SoundPlayer player)
    {
        // Stop() throws when the sound never started (no device, muted session). That is
        // not worth reporting - the caller only wanted silence.
        try { player.Stop(); }
        catch (Exception ex) { Trace.WriteLine($"[Deltempo] Sound stop ignored: {ex.Message}"); }
    }

    private static SoundPlayer CreatePlayer(SoundCue cue)
    {
        var player = new SoundPlayer(new MemoryStream(CueCache.GetOrAdd(cue, Prepare), writable: false));

        // Load() on this thread, once: it parses the header, copies the PCM into winmm's
        // buffer and throws here if the asset is broken. Doing it at play time is what
        // used to make the first click of a session late.
        player.Load();
        return player;
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

/// <summary>What the lane decided about a cue that asked to sound.</summary>
internal enum LaneDecision
{
    /// <summary>The cue may play; if it is a result cue it also takes the lane.</summary>
    Play,

    /// <summary>The cue is a click and something is already sounding.</summary>
    Drop,
}

/// <summary>
/// One lane, because a desktop app has one pair of speakers and four cues that can be
/// triggered from different places at the same moment.
///
/// The rules are deliberately asymmetric:
///
///   * A click is the tick of the interface. It is short and quiet, it fires on nearly
///     every interaction, and it must never be the reason a result is missed. So while
///     anything is sounding, a click is dropped rather than layered or queued - queuing
///     would also play it late, detached from the action that caused it.
///   * A result cue (success, error, warning) always sounds and interrupts the click
///     underneath it. Two results in quick succession interrupt each other, which is
///     correct: the second one is the answer to the newer operation.
///
/// Busy is measured from the cue's own duration plus a guard, because "is it still
/// playing" cannot be asked of winmm. Everything is driven by the clock that is passed
/// in, which is what makes the rule testable without an audio device.
/// </summary>
internal sealed class SoundLane
{
    /// <summary>
    /// Silence after a cue before the tick is allowed back in. Without it the click lands
    /// inside the tail of the cue that just finished, which reads as a smear.
    /// </summary>
    internal const int GuardMs = 30;

    private long _freeAtMs;
    private SoundCue? _current;

    /// <summary>The cue holding the lane, or null when nothing was played recently.</summary>
    public SoundCue? Current => _current;

    public bool IsBusy(long nowMs) => _freeAtMs > nowMs;

    /// <summary>
    /// Asks the lane for permission to sound <paramref name="cue"/>. When this returns
    /// <see cref="LaneDecision.Play"/>, <paramref name="interrupted"/> names the cue the
    /// caller should stop first (it is no longer the lane's owner).
    /// </summary>
    public LaneDecision Admit(SoundCue cue, int durationMs, long nowMs, out SoundCue? interrupted)
    {
        interrupted = null;
        bool busy = IsBusy(nowMs);

        if (cue == SoundCue.Click && busy)
        {
            return LaneDecision.Drop;
        }

        if (busy) interrupted = _current;

        _current = cue;
        _freeAtMs = nowMs + Math.Max(0, durationMs) + GuardMs;
        return LaneDecision.Play;
    }

    /// <summary>Frees the lane immediately, e.g. when sound is muted mid-cue.</summary>
    public void Release()
    {
        _current = null;
        _freeAtMs = 0;
    }
}
