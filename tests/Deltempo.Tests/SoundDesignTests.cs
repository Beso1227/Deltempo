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
    public void SoundsAreOriginalWork_WithDocumentedProvenance()
    {
        // The cues are synthesised. What the repo must never lose is the recipe: the
        // script that produces them and the note explaining the design, or the audio
        // becomes four unexplained binaries that nobody dares touch.
        string root = RepoRoot();

        Assert.True(File.Exists(Path.Combine(root, "scripts", "generate_sounds.ps1")),
            "scripts/generate_sounds.ps1 is missing - the cues would no longer be reproducible.");
        Assert.True(File.Exists(Path.Combine(root, "src", "Deltempo.Core", "Assets", "Sounds", "CREDITS.md")),
            "src/Deltempo.Core/Assets/Sounds/CREDITS.md is missing - sound provenance must stay documented.");

        string credits = File.ReadAllText(Path.Combine(root, "src", "Deltempo.Core", "Assets", "Sounds", "CREDITS.md"));
        Assert.Contains("generate_sounds.ps1", credits, StringComparison.OrdinalIgnoreCase);

        // No stale third-party attribution: the Kenney pack is no longer shipped, so a
        // license file or a credit that still names it would be a false claim.
        Assert.False(File.Exists(Path.Combine(root, "src", "Deltempo.Core", "Assets", "Sounds", "KENNEY-LICENSE.txt")),
            "KENNEY-LICENSE.txt is still present but no Kenney sample is shipped.");
        Assert.DoesNotContain("kenney.nl", credits, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllFourSourceSamplesArePresentOnDisk()
    {
        string dir = Path.Combine(RepoRoot(), "src", "Deltempo.Core", "Assets", "Sounds");
        foreach (string name in new[] { "click.wav", "success.wav", "error.wav", "warning.wav" })
        {
            string path = Path.Combine(dir, name);
            Assert.True(File.Exists(path), $"src/Deltempo.Core/Assets/Sounds/{name} is missing.");
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
    // Caching and playback
    // ------------------------------------------------------------------

    private static string SoundServiceSource() => File.ReadAllText(
        Path.Combine(RepoRoot(), "src", "Deltempo.Core", "Services", "SoundService.cs"));

    [Fact]
    public void PreparedCue_IsCached_SoPlaybackDoesNotReloadTheResource()
    {
        // The click path runs on nearly every interaction; re-reading and re-normalising
        // the resource per call would be wasteful work on the interaction path.
        string source = SoundServiceSource();

        Assert.Contains("ConcurrentDictionary<SoundCue, byte[]>", source);
        Assert.Contains("GetOrAdd", source);
    }

    [Fact]
    public void PlaybackUsesOneLoadedPlayerPerCue_NotAThreadPerSound()
    {
        // The previous engine started a Task and a fresh SoundPlayer with PlaySync() for
        // every single cue: a thread-pool work item per click, a resource parse per cue,
        // and no way to stop one sound when another started.
        string source = SoundServiceSource();

        Assert.Contains("ConcurrentDictionary<SoundCue, SoundPlayer>", source);
        Assert.Contains("player.Load()", source);
        Assert.DoesNotContain("PlaySync", source);
        Assert.DoesNotContain("Task.Run", source);
    }

    // ------------------------------------------------------------------
    // Playback lane: what may sound while something else is sounding
    // ------------------------------------------------------------------

    [Fact]
    public void Click_IsDropped_WhileTheLaneIsBusy()
    {
        var lane = new SoundLane();

        Assert.Equal(LaneDecision.Play, lane.Admit(SoundCue.Success, 250, 1_000, out _));

        // 100 ms later the success cue is still sounding, so the tick must not layer on
        // top of it - it is background feedback, not an event worth interrupting.
        Assert.Equal(LaneDecision.Drop, lane.Admit(SoundCue.Click, 45, 1_100, out SoundCue? interrupted));
        Assert.Null(interrupted);
    }

    [Fact]
    public void Click_SoundsAgain_OnceTheCueAndItsGuardHavePassed()
    {
        var lane = new SoundLane();
        lane.Admit(SoundCue.Click, 45, 1_000, out _);

        // Busy for the cue's own length plus the guard.
        Assert.True(lane.IsBusy(1_000 + 45 + SoundLane.GuardMs - 1));
        Assert.Equal(LaneDecision.Drop, lane.Admit(SoundCue.Click, 45, 1_020, out _));

        Assert.False(lane.IsBusy(1_000 + 45 + SoundLane.GuardMs));
        Assert.Equal(LaneDecision.Play, lane.Admit(SoundCue.Click, 45, 1_000 + 45 + SoundLane.GuardMs, out _));
    }

    [Fact]
    public void ResultCue_InterruptsWhateverIsSounding()
    {
        var lane = new SoundLane();
        lane.Admit(SoundCue.Click, 45, 1_000, out _);

        // A result always gets to speak, and it names the cue the caller must stop.
        Assert.Equal(LaneDecision.Play, lane.Admit(SoundCue.Error, 210, 1_010, out SoundCue? interrupted));
        Assert.Equal(SoundCue.Click, interrupted);
        Assert.Equal(SoundCue.Error, lane.Current);
    }

    [Fact]
    public void ResultsDoNotStack_OnEachOtherEither()
    {
        var lane = new SoundLane();
        lane.Admit(SoundCue.Success, 250, 1_000, out _);

        // Two operations finishing back to back: the newer answer replaces the older one
        // instead of playing both at once.
        Assert.Equal(LaneDecision.Play, lane.Admit(SoundCue.Warning, 300, 1_050, out SoundCue? interrupted));
        Assert.Equal(SoundCue.Success, interrupted);
    }

    [Fact]
    public void MutingReleasesTheLane()
    {
        var lane = new SoundLane();
        lane.Admit(SoundCue.Success, 250, 1_000, out _);

        lane.Release();

        Assert.False(lane.IsBusy(1_001));
        Assert.Null(lane.Current);
        Assert.Equal(LaneDecision.Play, lane.Admit(SoundCue.Click, 45, 1_001, out _));
    }

    // ------------------------------------------------------------------
    // The result of a cleanup run
    // ------------------------------------------------------------------

    private static WinTempCleaner.Models.CleanSummary Summary(
        long freedBytes,
        int failed = 0,
        int skipped = 0,
        int inUse = 0,
        int shielded = 0,
        int policy = 0) => new()
        {
            TotalFreedBytes = freedBytes,
            TotalFilesFailed = failed,
            TotalFilesSkipped = skipped,
            TotalFilesInUse = inUse,
            TotalFilesRecentShielded = shielded,
            TotalFilesPolicyProtected = policy,
        };

    [Fact]
    public void CleanRun_ThatFreedSomethingAndLeftNothing_GetsSuccess()
    {
        Assert.Equal(SoundCue.Success, SoundService.CleanResultCue(Summary(1024 * 1024)));
    }

    [Theory]
    [InlineData(4096, 1, 0, 0, 0, 0)]   // one file failed, but space was reclaimed
    [InlineData(4096, 0, 1, 0, 0, 0)]   // skipped
    [InlineData(4096, 0, 0, 3, 0, 0)]   // in use
    [InlineData(4096, 0, 0, 0, 2, 0)]   // recently shielded
    [InlineData(4096, 0, 0, 0, 0, 1)]   // policy protected
    [InlineData(0, 0, 0, 0, 0, 0)]      // nothing to free: the answer is "there was nothing to do"
    public void CleanRun_ThatNeedsAttention_GetsWarning(
        long freed,
        int failed,
        int skipped,
        int inUse,
        int shielded,
        int policy)
    {
        var summary = Summary(freed, failed, skipped, inUse, shielded, policy);

        Assert.Equal(SoundCue.Warning, SoundService.CleanResultCue(summary));
    }

    [Fact]
    public void CleanRun_ThatOnlyFailed_GetsError()
    {
        // Nothing moved and files failed: that is a failed operation, not a partial one.
        Assert.Equal(SoundCue.Error, SoundService.CleanResultCue(Summary(0, failed: 5)));
        Assert.Equal(SoundCue.Error, SoundService.CleanResultCue(Summary(0, failed: 5, inUse: 3)));
    }

    [Fact]
    public void BothShellsApplyTheSavedSoundPreference_AtStartup()
    {
        // WPF used to leave SoundService.IsSoundEnabled at its default true and only
        // write it from the header toggle, so a muted app came back unmuted. Both shells
        // must push the persisted value on launch.
        string wpf = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Deltempo.App.Wpf", "MainWindow.xaml.cs"));
        string winui = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Deltempo.App.WinUI", "App.xaml.cs"));

        Assert.Contains("SoundService.IsSoundEnabled = SettingsService.Current.SoundEnabled", wpf);
        Assert.Contains("SoundService.IsSoundEnabled = WinTempCleaner.Services.SettingsService.Current.SoundEnabled", winui);
    }

    // ------------------------------------------------------------------
    // Tone: what the cues sound like, not just how loud they are
    // ------------------------------------------------------------------

    /// <summary>
    /// Zero-crossing rate converted to the frequency it corresponds to. It is not a
    /// real spectral centroid, but it is enough to catch a cue that sits below what a
    /// laptop speaker can reproduce - which is exactly how the old click failed.
    /// </summary>
    private static double DominantHz(byte[] wav)
    {
        int rate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24));
        return Brightness(wav) * rate / 2.0;
    }

    [Fact]
    public void Click_SitsInTheBandLaptopSpeakersCanReproduce()
    {
        double hz = DominantHz(Load(SoundCue.Click));

        Assert.InRange(hz, 500, 4000);
    }

    [Fact]
    public void EveryCue_EndsOnSilence_SoItCannotPop()
    {
        // A cue that stops while the waveform is still moving is a step, and a step is
        // an audible click - the one defect that reads as "broken" rather than "quiet".
        foreach (SoundCue cue in Enum.GetValues<SoundCue>())
        {
            byte[] wav = Load(cue);
            int end = DataOffset(wav) + DataLength(wav) - 2;
            short last = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(end));
            float peak = Peak(wav);

            Assert.True(Math.Abs(last) <= peak * 0.01f * short.MaxValue,
                $"{cue} ends on a step of {last} against a peak of {peak:0.00}; that pops.");
        }
    }

    [Fact]
    public void EveryCue_IsDcFree()
    {
        // A DC offset costs headroom, and a cue that starts on one adds a step at the
        // very first sample. These are synthesised, so there is no excuse for any.
        foreach (SoundCue cue in Enum.GetValues<SoundCue>())
        {
            byte[] wav = Load(cue);
            int offset = DataOffset(wav);
            int length = DataLength(wav);

            long sum = 0;
            for (int i = offset; i + 1 < offset + length; i += 2)
            {
                sum += BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(i));
            }

            double mean = sum / (double)(length / 2);
            Assert.True(Math.Abs(mean) < 30.0, $"{cue} carries a DC offset of {mean:0.0} LSB.");
        }
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
