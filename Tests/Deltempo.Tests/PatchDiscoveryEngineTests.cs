using System;
using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

public class PatchDiscoveryEngineTests
{
    [Fact]
    public void EvaluatePatchAvailability_SameVersionDifferentCommit_DetectsUpdateAvailable()
    {
        string localSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56";
        string localHash = "1111111111111111111111111111111111111111111111111111111111111111";
        DateTime localBuildDate = DateTime.UtcNow.AddHours(-1);

        string remoteSha = "5775274112233445566778899aabbccddeeff001";
        string remoteHash = "2222222222222222222222222222222222222222222222222222222222222222";
        DateTime remoteTimestamp = DateTime.UtcNow;

        var result = PatchDiscoveryEngine.EvaluatePatchAvailability(
            localCommitSha: localSha,
            localExeSha256: localHash,
            localBuildDateUtc: localBuildDate,
            lastInstalledSha: "",
            lastInstalledHash: "",
            remoteCommitSha: remoteSha,
            remoteSha256: remoteHash,
            remoteTimestampUtc: remoteTimestamp);

        Assert.True(result.IsNewer, result.Reason);
        Assert.Equal(PatchMatchKind.NewerCommitFound, result.MatchKind);
    }

    [Fact]
    public void EvaluatePatchAvailability_ExactBinaryHashMatch_ReportsUpToDate()
    {
        string binaryHash = "7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b60578";

        var result = PatchDiscoveryEngine.EvaluatePatchAvailability(
            localCommitSha: "local_sha_123",
            localExeSha256: binaryHash,
            localBuildDateUtc: DateTime.UtcNow.AddDays(-1),
            lastInstalledSha: "",
            lastInstalledHash: "",
            remoteCommitSha: "remote_sha_456",
            remoteSha256: binaryHash, // Exact same hash
            remoteTimestampUtc: DateTime.UtcNow);

        Assert.False(result.IsNewer);
        Assert.Equal(PatchMatchKind.ExactBinaryMatch, result.MatchKind);
        Assert.Contains("matches remote patch SHA-256 exactly", result.Reason);
    }

    [Fact]
    public void EvaluatePatchAvailability_ExactCommitMatch_ReportsUpToDate()
    {
        string commitSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56";

        var result = PatchDiscoveryEngine.EvaluatePatchAvailability(
            localCommitSha: commitSha,
            localExeSha256: "hash_local_111",
            localBuildDateUtc: DateTime.UtcNow.AddDays(-1),
            lastInstalledSha: "",
            lastInstalledHash: "",
            remoteCommitSha: commitSha, // Exact same commit
            remoteSha256: "hash_remote_222",
            remoteTimestampUtc: DateTime.UtcNow);

        Assert.False(result.IsNewer);
        Assert.Equal(PatchMatchKind.ExactCommitMatch, result.MatchKind);
        Assert.Contains("matches remote commit", result.Reason);
    }

    [Fact]
    public void EvaluatePatchAvailability_PersistentInstalledShaMatch_PreventsReinstallLoop()
    {
        string remoteSha = "e77637c385b2a0ef88cf788874bb7c76";

        var result = PatchDiscoveryEngine.EvaluatePatchAvailability(
            localCommitSha: "unknown",
            localExeSha256: "current_hash_123",
            localBuildDateUtc: DateTime.UtcNow.AddDays(-1),
            lastInstalledSha: remoteSha, // User already installed this patch in previous run
            lastInstalledHash: "",
            remoteCommitSha: remoteSha,
            remoteSha256: "new_remote_hash_456",
            remoteTimestampUtc: DateTime.UtcNow);

        Assert.False(result.IsNewer);
        Assert.Equal(PatchMatchKind.PersistentInstalledMatch, result.MatchKind);
        Assert.Contains("already recorded as installed", result.Reason);
    }

    [Fact]
    public void EvaluatePatchAvailability_PersistentInstalledHashMatch_PreventsReinstallLoop()
    {
        string remoteHash = "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0";

        var result = PatchDiscoveryEngine.EvaluatePatchAvailability(
            localCommitSha: "unknown",
            localExeSha256: "local_hash_999",
            localBuildDateUtc: DateTime.UtcNow.AddDays(-1),
            lastInstalledSha: "",
            lastInstalledHash: remoteHash, // Already applied
            remoteCommitSha: "new_commit_888",
            remoteSha256: remoteHash,
            remoteTimestampUtc: DateTime.UtcNow);

        Assert.False(result.IsNewer);
        Assert.Equal(PatchMatchKind.PersistentInstalledMatch, result.MatchKind);
        Assert.Contains("already recorded as installed", result.Reason);
    }

    [Fact]
    public void EvaluatePatchAvailability_StaleRemoteCommit_DoesNotOfferDowngrade()
    {
        DateTime localBuildDate = DateTime.UtcNow;
        DateTime remoteOlderDate = DateTime.UtcNow.AddHours(-2); // 2 hours older

        var result = PatchDiscoveryEngine.EvaluatePatchAvailability(
            localCommitSha: "newer_local_commit_999",
            localExeSha256: "hash_local_999",
            localBuildDateUtc: localBuildDate,
            lastInstalledSha: "",
            lastInstalledHash: "",
            remoteCommitSha: "older_remote_commit_111",
            remoteSha256: "hash_remote_111",
            remoteTimestampUtc: remoteOlderDate);

        Assert.False(result.IsNewer);
        Assert.Contains("older than current local build", result.Reason);
    }
}
