<#
.SYNOPSIS
    Regenerates the four interface sounds embedded in Deltempo.Core.

.DESCRIPTION
    The cues are synthesised, not sampled, so the whole family shares one voice: the
    same attack shape, the same harmonic recipe and the same tail. That is what makes
    four separate sounds feel like one product instead of four downloads.

    Tuning (equal temperament, A4 = 440 Hz):

      click    1250 Hz + 2500 Hz partial + a short noise transient   45 ms
      success  C6 (1046.50) -> G6 (1567.98), ascending fifth        250 ms
      error    Eb4 (311.13) -> Bb3 (233.08), descending fourth      210 ms
      warning  G5 (783.99) twice, the "check this" double beep      300 ms

    Every cue is generated mono 44.1 kHz 16-bit, DC-free, with a raised-cosine attack
    and a fade-out so nothing starts or ends on a step (a step is an audible pop). The
    files are written at 0.75 peak; SoundService normalises each cue to its own target
    level at load time, so the mix balance is not decided by this script.

    Output: src/Deltempo.Core/Assets/Sounds/{click,success,error,warning}.wav
    Deterministic: same input, same bytes, so a regenerated file only shows up in git
    when the recipe actually changed.

.EXAMPLE
    pwsh -File scripts/generate_sounds.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot '..\src\Deltempo.Core\Assets\Sounds'
}

$source = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Deltempo.SoundForge
{
    public static class SoundForge
    {
        private const int Rate = 44100;

        // Peak the files are written at. SoundService re-normalises per cue.
        private const double WritePeak = 0.75;

        public static void WriteAll(string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "click.wav"), Click());
            File.WriteAllBytes(Path.Combine(directory, "success.wav"), Success());
            File.WriteAllBytes(Path.Combine(directory, "error.wav"), Error());
            File.WriteAllBytes(Path.Combine(directory, "warning.wav"), Warning());
        }

        // ----------------------------------------------------------------
        // Recipes
        // ----------------------------------------------------------------

        /// <summary>
        /// The tick. Short, pitched around 1.25 kHz so laptop speakers actually reproduce
        /// it, with a filtered noise transient on the front for the "touch" quality a
        /// pure sine lacks.
        /// </summary>
        private static byte[] Click()
        {
            double[] buffer = Buffer(45);

            AddTone(buffer, 1250.0, 0.0, 8.0, 1.00, 1.2, Harmonics(1.0, 0.0, 0.0));
            AddTone(buffer, 2500.0, 0.0, 5.0, 0.22, 1.0, Harmonics(1.0));
            AddTransient(buffer, 0.0, 2.4, 0.30);

            return Finish(buffer);
        }

        /// <summary>Ascending fifth: the "that worked" interval, bright but not shrill.</summary>
        private static byte[] Success()
        {
            double[] buffer = Buffer(250);

            AddTone(buffer, 1046.50, 0.0, 80.0, 1.00, 4.0, Harmonics(1.0, 0.22, 0.07));
            AddTone(buffer, 1567.98, 70.0, 75.0, 0.95, 4.0, Harmonics(1.0, 0.20, 0.06));

            return Finish(buffer);
        }

        /// <summary>
        /// Descending fourth, low and odd-harmonic. Dull on purpose: it must never be
        /// mistaken for the success cue from across the room.
        /// </summary>
        private static byte[] Error()
        {
            double[] buffer = Buffer(210);

            AddTone(buffer, 311.13, 0.0, 55.0, 1.00, 3.0, Harmonics(1.0, 0.0, 0.30, 0.0, 0.12));
            AddTone(buffer, 233.08, 85.0, 60.0, 0.95, 3.0, Harmonics(1.0, 0.0, 0.28, 0.0, 0.10));

            return Finish(buffer);
        }

        /// <summary>The same note twice: "finished, but look at this".</summary>
        private static byte[] Warning()
        {
            double[] buffer = Buffer(300);

            AddTone(buffer, 783.99, 0.0, 55.0, 1.00, 4.0, Harmonics(1.0, 0.15));
            AddTone(buffer, 783.99, 150.0, 60.0, 0.90, 4.0, Harmonics(1.0, 0.15));

            return Finish(buffer);
        }

        // ----------------------------------------------------------------
        // Synthesis
        // ----------------------------------------------------------------

        private static double[] Buffer(int milliseconds)
            => new double[Rate * milliseconds / 1000];

        private static double[] Harmonics(params double[] amplitudes) => amplitudes;

        /// <summary>
        /// One pitched voice: additive partials, raised-cosine attack, exponential decay.
        /// </summary>
        private static void AddTone(
            double[] buffer,
            double frequency,
            double startMs,
            double decayMs,
            double amplitude,
            double attackMs,
            double[] harmonics)
        {
            int start = (int)(startMs * Rate / 1000.0);
            int attack = Math.Max(1, (int)(attackMs * Rate / 1000.0));
            double decay = decayMs * Rate / 1000.0;

            for (int i = start; i < buffer.Length; i++)
            {
                double t = i - start;

                double envelope = t < attack
                    ? 0.5 * (1.0 - Math.Cos(Math.PI * t / attack))
                    : Math.Exp(-(t - attack) / decay);

                // Only a decayed tail may end the voice: at t = 0 the attack starts at
                // zero by design, and breaking on that would drop the whole note.
                if (t > attack && envelope < 1e-5) break;

                double sample = 0.0;
                for (int h = 0; h < harmonics.Length; h++)
                {
                    double partial = harmonics[h];
                    if (partial <= 0.0) continue;

                    double phase = 2.0 * Math.PI * frequency * (h + 1) * t / Rate;

                    // Higher partials decay faster, the way a struck body does. Without
                    // this the tail rings as a static chord instead of settling.
                    double partialDecay = Math.Exp(-(t / decay) * h * 0.6);
                    sample += partial * partialDecay * Math.Sin(phase);
                }

                buffer[i] += amplitude * envelope * sample;
            }
        }

        /// <summary>
        /// A band-limited noise burst for the attack of the tick. High-passed with a
        /// one-pole difference so it reads as "tick" rather than "white noise".
        /// </summary>
        private static void AddTransient(double[] buffer, double startMs, double decayMs, double amplitude)
        {
            int start = (int)(startMs * Rate / 1000.0);
            double decay = decayMs * Rate / 1000.0;

            // Fixed seed: the generated file must be reproducible byte for byte.
            uint state = 0x9E3779B9;
            double previous = 0.0;

            for (int i = start; i < buffer.Length; i++)
            {
                double t = i - start;

                state = unchecked(state * 1664525u + 1013904223u);
                double white = (state / (double)uint.MaxValue) * 2.0 - 1.0;

                double highPassed = white - previous;
                previous = white;

                double envelope = Math.Exp(-t / decay);
                if (envelope < 1e-5) break;

                buffer[i] += amplitude * envelope * highPassed * 0.5;
            }
        }

        // ----------------------------------------------------------------
        // Finishing
        // ----------------------------------------------------------------

        /// <summary>
        /// Removes any DC, fades the last few milliseconds to zero, scales to the write
        /// peak and packs to 16-bit PCM.
        /// </summary>
        private static byte[] Finish(double[] buffer)
        {
            double dc = 0.0;
            foreach (double sample in buffer) dc += sample;
            dc /= buffer.Length;

            double peak = 0.0;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] -= dc;
                peak = Math.Max(peak, Math.Abs(buffer[i]));
            }

            if (peak <= 1e-9)
            {
                throw new InvalidOperationException("Recipe produced silence.");
            }

            double scale = WritePeak / peak;

            // 12 ms tail fade. A cue that stops on a non-zero sample pops, and a pop is
            // the one thing that makes interface audio sound broken.
            int fade = Math.Min(buffer.Length, Rate * 12 / 1000);
            int fadeStart = buffer.Length - fade;

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                int dataBytes = buffer.Length * 2;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);          // PCM header size
                writer.Write((short)1);    // PCM
                writer.Write((short)1);    // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);    // byte rate
                writer.Write((short)2);    // block align
                writer.Write((short)16);   // bits per sample
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);

                for (int i = 0; i < buffer.Length; i++)
                {
                    double sample = buffer[i] * scale;

                    if (i >= fadeStart)
                    {
                        double progress = (i - fadeStart) / (double)fade;
                        sample *= 0.5 * (1.0 + Math.Cos(Math.PI * progress));
                    }

                    short pcm = (short)Math.Round(Math.Clamp(sample, -1.0, 1.0) * short.MaxValue);
                    writer.Write(pcm);
                }
            }

            return stream.ToArray();
        }
    }
}
'@

Add-Type -TypeDefinition $source -Language CSharp

$resolved = [IO.Path]::GetFullPath($OutputDirectory)
[Deltempo.SoundForge.SoundForge]::WriteAll($resolved)

Get-ChildItem $resolved -Filter *.wav | ForEach-Object {
    "generated {0,-12} {1,6} bytes" -f $_.Name, $_.Length
}
