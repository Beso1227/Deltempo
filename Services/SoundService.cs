using System.IO;
using System.Media;

namespace WinTempCleaner.Services;

public static class SoundService
{
    public static bool IsSoundEnabled { get; set; } = true;

    public static void PlayClickSound()
    {
        if (!IsSoundEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Play subtle soft click using generated short PCM wave
                using var ms = GenerateToneWaveStream(880, 25, 0.15f);
                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        });
    }

    public static void PlaySuccessSound()
    {
        if (!IsSoundEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Play futuristic positive sweep (587Hz -> 880Hz -> 1174Hz)
                using var ms = GenerateChimeWaveStream();
                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        });
    }

    private static MemoryStream GenerateToneWaveStream(int frequency, int durationMs, float volume)
    {
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true);

        int sampleRate = 44100;
        int samples = sampleRate * durationMs / 1000;
        short bitsPerSample = 16;
        short channels = 1;

        // WAV Header
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + samples * 2);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(samples * 2);

        // Sine wave with gentle fade-out
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = 1.0 - ((double)i / samples); // Linear decay
            short sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * envelope * volume * short.MaxValue);
            writer.Write(sample);
        }

        ms.Position = 0;
        return ms;
    }

    private static MemoryStream GenerateChimeWaveStream()
    {
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true);

        int sampleRate = 44100;
        int durationMs = 320;
        int samples = sampleRate * durationMs / 1000;

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + samples * 2);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(samples * 2);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double progress = (double)i / samples;
            // Harmonic chime (D5 + A5 + D6)
            double wave = Math.Sin(2 * Math.PI * 587.33 * t) * 0.4 +
                          Math.Sin(2 * Math.PI * 880.00 * t) * 0.4 +
                          Math.Sin(2 * Math.PI * 1174.66 * t) * 0.2;

            double envelope = Math.Pow(1.0 - progress, 1.8);
            short sample = (short)(wave * envelope * 0.22f * short.MaxValue);
            writer.Write(sample);
        }

        ms.Position = 0;
        return ms;
    }
}
