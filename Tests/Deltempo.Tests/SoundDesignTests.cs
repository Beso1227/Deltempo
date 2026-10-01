using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Xunit;
using WinTempCleaner.Services;

namespace Deltempo.Tests;

/// <summary>
/// Guards the app's interface audio: that the CC0 samples are actually embedded and
/// loadable, that they are level-matched and never clip, that they stay short enough not
/// to fatigue, and that the result cues are distinguishable by ear alone.
///
/// These assert on the prepared bytes the player actually consumes, not on the source
/// assets, so a bad asset swap fails here rather than in front of a user.
/// </summary>
public class SoundDesignTests
{
    private static byte[] Load(SoundCue cue) => SoundService.Prepare(cue);

    private static int DataOffset(byte[] wav)
    {
        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(pos + 4));
            if (id == "data") return pos + 8;
            if (size <= 0) break;
            pos += 8 + size + (size % 2);
        }
        throw new InvalidOperationException("no data chunk");
    }

    private static int DataLength(byte[] wav)
    {
        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(pos + 4));
            if (id == "data") return size;
            if (size <= 0) break;
            pos += 8 + size + (size % 2);
        }
        throw new InvalidOperationException("no data chunk");
    }

    private static float Peak(byte[] wav)
    {
        int offset = DataOffset(wav);
        int length = DataLength(wav);
        float peak = 0f;
        for (int i = offset; i + 1 < offset + length; i += 2)
        {
            short sample = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(i));
            peak = Math.Max(peak, Math.Abs(sample) / (float)short.MaxValue);
        }
        return peak;
    }

    /// <summary>Zero-crossing rate, a cheap proxy for how bright or dull a cue sounds.</summary>
    private static double Brightness(byte[] wav)
    {
        int offset = DataOffset(wav);
        int length = DataLength(wav);
        int crossings = 0;
        short previous = 0;
        for (int i = offset; i + 1 < offset + length; i += 2)
        {
            short s = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(i));
            if (previous <= 0 && s > 0) crossings++;
            previous = s;
        }
        return crossings / (double)(length / 2);
    }

    // ------------------------------------------------------------------
    // Assets are present and loadable
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SoundCue.Click)]
    [InlineData(SoundCue.Success)]
    [InlineData(SoundCue.Error)]
    [InlineData(SoundCue.Warning)]
    public void Cue_IsEmbeddedAndLoads(SoundCue cue)
    {
        byte[] wav = Load(cue);

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.True(DataLength(wav) > 0, $"{cue} has an empty data chunk.");
        Assert.True(Peak(wav) > 0.05f, $"{cue} is silent.");
    }

    [Fact]
    public void SoundsAreSourcedFromLicensedAssets_WithCredits()
    {
        // The samples are third-party files. If the pack or its license is dropped, the
        // repo must not silently keep shipping unattributed audio.
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "Assets", "Sounds", "CREDITS.md")),
            "Assets/Sounds/CREDITS.md is missing - sound provenance must stay documented.");
        Assert.True(File.Exists(Path.Combine(root, "Assets", "Sounds", "KENNEY-LICENSE.txt")),
            "The upstream CC0 license text must be kept alongside the samples.");

        string credits = File.ReadAllText(Path.Combine(root, "Assets", "Sounds", "CREDITS.md"));
        Assert.Contains("CC0", credits, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kenney.nl", credits, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllFourSourceSamplesArePresentOnDisk()
    {
        string dir = Path.Combine(RepoRoot(), "Assets", "Sounds");
        foreach (string name in new[] { "click.wav", "success.wav", "error.wav", "warning.wav" })
        {
            string path = Path.Combine(dir, name);
            Assert.True(File.Exists(path), $"Assets/Sounds/{name} is missing.");
            Assert.True(new FileInfo(path).Length > 512, $"{name} is suspiciously small.");
        }
    }

    // ------------------------------------------------------------------
    // Level discipline
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SoundCue.Click)]
    [InlineData(SoundCue.Success)]
    [InlineData(SoundCue.Error)]
    [InlineData(SoundCue.Warning)]
    public void Cue_NeverClips(SoundCue cue)
    {
        float peak = Peak(Load(cue));

        Assert.True(peak <= SoundService.PeakCeiling,
            $"{cue} peaks at {peak:0.000}, above the {SoundService.PeakCeiling} ceiling - this distorts.");
    }

    [Theory]
    [InlineData(SoundCue.Click)]
    [InlineData(SoundCue.Success)]
    [InlineData(SoundCue.Error)]
    [InlineData(SoundCue.Warning)]
    public void Cue_IsNormalisedToItsTargetLevel(SoundCue cue)
    {
        // Normalisation is what stops one mastered-loud sample from dominating the mix.
        // Without it, swapping in a new asset silently rebalances the whole app.
        float peak = Peak(Load(cue));
        Assert.InRange(peak, 0.45f, SoundService.PeakCeiling);
    }

    [Fact]
    public void Click_IsTheQuietestCue_SoItStaysBackground()
    {
        float click = Peak(Load(SoundCue.Click));

        foreach (SoundCue cue in new[] { SoundCue.Success, SoundCue.Error, SoundCue.Warning })
        {
            float other = Peak(Load(cue));
            Assert.True(other > click,
                $"{cue} ({other:0.00}) is not louder than Click ({click:0.00}); a routine " +
                "interaction would compete with the operation result.");
        }
    }

    // ------------------------------------------------------------------
    // Duration budget
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SoundCue.Click)]
    [InlineData(SoundCue.Success)]
    [InlineData(SoundCue.Error)]
    [InlineData(SoundCue.Warning)]
    public void Cue_IsShortEnoughToNotFatigue(SoundCue cue)
    {
        Assert.InRange(SoundService.CueDurationMs(cue), 5, 700);
    }

    [Fact]
    public void Click_StaysUnderOneHundredMilliseconds()
    {
        Assert.True(SoundService.CueDurationMs(SoundCue.Click) <= 100,
            "The click fires on nearly every interaction; past 100 ms it becomes a stutter.");
    }

    // ------------------------------------------------------------------
    // Distinguishability
    // ------------------------------------------------------------------

    [Fact]
    public void SuccessAndError_AreAcousticallyDistinct()
    {
        // A dull cue and a bright one must not collapse into the same sound, or the
        // result is ambiguous without looking at the screen.
        double success = Brightness(Load(SoundCue.Success));
        double error = Brightness(Load(SoundCue.Error));

        Assert.True(success > error * 1.5,
            $"Success ({success:0.0000}) is not clearly brighter than Error ({error:0.0000}); " +
            "the two result cues would be hard to tell apart.");
    }

    [Fact]
    public void NoTwoCues_ShareIdenticalAudio()
    {
        // Guards against two cues being pointed at the same file by mistake.
        var seen = Enum.GetValues<SoundCue>()
            .Select(c => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Load(c))))
            .ToList();

        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    // ------------------------------------------------------------------
    // Caching
    // ------------------------------------------------------------------

    [Fact]
    public void PreparedCue_IsCached_SoPlaybackDoesNotReloadTheResource()
    {
        // The click path runs on nearly every interaction; re-reading and re-normalising
        // the resource per call would be wasteful work on the interaction path.
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "Services", "SoundService.cs"));

        Assert.Contains("ConcurrentDictionary<SoundCue, byte[]>", source);
        Assert.Contains("GetOrAdd", source);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Deltempo.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
