using System;
using System.IO;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// End-to-end guard for the offline Ask AI round trip on a large-file row,
/// driven through the real Core path (LargeFileHunterService), reproducing
/// the live "Ask AI left the row UNKNOWN" report.
///
/// The bug it pins: ApplyOnlineReport never ran on the offline branch, so the
/// row kept VerdictShort = UNKNOWN despite a completed analysis and the busy
/// flag (IsAiAnalyzing) could remain latched if the caller's exception paths
/// misbehaved. The report also surfaced the same spine dead at the ViewModel
/// layer (an ElementName command binding inside a DataTemplate never fires in
/// WinUI 3); that half is pinned by LargeFilesUiRepairParityTests.
///
/// Reads-only settings; the online-AI switch is forced off for the duration of the
/// run and restored afterwards, so the offline branch is exercised on every machine
/// and the run can never hit the network.
/// </summary>
[Collection("SettingsService")]
public class LargeFileAnalyzeRoundTripTests
{
    [Fact]
    public async Task AnalyzeItemWithAi_Offline_UpdatesTheRow()
    {
        // A machine that has the online-AI switch on used to fail this test outright,
        // even though the offline branch is exactly what the test is about. Forcing
        // the switch off for the run makes the outcome depend on the code under test
        // instead of on the machine's settings; the original value is restored below.
        bool originalOnlineSafety = SettingsService.Current.EnableOnlineAiSafety;
        if (originalOnlineSafety)
        {
            SettingsService.Update(s => s.EnableOnlineAiSafety = false);
        }

        try
        {
            await AnalyzeItemWithAi_Offline_UpdatesTheRowCore();
        }
        finally
        {
            if (originalOnlineSafety)
            {
                SettingsService.Update(s => s.EnableOnlineAiSafety = originalOnlineSafety);
            }
        }
    }

    private static async Task AnalyzeItemWithAi_Offline_UpdatesTheRowCore()
    {
        var settings = SettingsService.Current;
        Assert.False(settings.EnableOnlineAiSafety,
            "online safety must be off for this run to guarantee no network");

        string dir = Path.Combine(Path.GetTempPath(), "deltempo-ai-repro");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "game37.arc");
        if (!File.Exists(file))
        {
            await File.WriteAllBytesAsync(file, new byte[1_500_000]);
        }

        var item = new LargeFileInfo
        {
            FilePath = file,
            FileName = Path.GetFileName(file),
            SizeBytes = new FileInfo(file).Length,
            Category = "Document / Other",
            LastModified = File.GetLastWriteTime(file),
        };

        Assert.Equal("PROTECTED", item.VerdictShort);
        Assert.False(item.IsAiOnlineVerified);

        var before = item.VerdictShort;
        await LargeFileHunterService.AnalyzeItemWithAiAsync(item);

        Assert.False(item.IsAiAnalyzing, "the analysis must end with the busy flag cleared");
        Assert.True(item.IsAiOnlineVerified, $"ApplyOnlineReport must have run (VerdictShort went {before} -> {item.VerdictShort})");
        Assert.NotEqual("UNKNOWN", item.VerdictShort);
        Assert.False(string.IsNullOrWhiteSpace(item.Origin), "the capsule must carry an origin line");
    }
}
