namespace WinTempCleaner.Core.Update;

public enum PatchMatchKind
{
    ExactBinaryMatch,
    ExactCommitMatch,
    PersistentInstalledMatch,
    NewerCommitFound,
    TimestampFallbackNewer,
    UpToDate
}

public class PatchDiscoveryResult
{
    public bool IsNewer { get; set; }
    public PatchMatchKind MatchKind { get; set; }
    public string Reason { get; set; } = string.Empty;

    public static PatchDiscoveryResult UpToDate(PatchMatchKind kind, string reason) =>
        new() { IsNewer = false, MatchKind = kind, Reason = reason };

    public static PatchDiscoveryResult UpdateAvailable(PatchMatchKind kind, string reason) =>
        new() { IsNewer = true, MatchKind = kind, Reason = reason };
}

/// <summary>
/// Deterministic evaluation engine for patch build identity.
/// Solves the "Same Version, New Code" requirement: recognizes newer patch builds
/// even when the semantic product version remains identical.
/// Defends against reinstall loops by deriving state from exact binary hash and git commit SHA.
/// </summary>
public static class PatchDiscoveryEngine
{
    public static PatchDiscoveryResult EvaluatePatchAvailability(
        string localCommitSha,
        string localExeSha256,
        DateTime localBuildDateUtc,
        string lastInstalledSha,
        string lastInstalledHash,
        string remoteCommitSha,
        string remoteSha256,
        DateTime remoteTimestampUtc)
    {
        string normLocalSha = (localCommitSha ?? "").Trim();
        string normLocalExeHash = (localExeSha256 ?? "").Trim();
        string normLastSha = (lastInstalledSha ?? "").Trim();
        string normLastHash = (lastInstalledHash ?? "").Trim();
        string normRemoteSha = (remoteCommitSha ?? "").Trim();
        string normRemoteHash = (remoteSha256 ?? "").Trim();

        // 1. Exact Binary SHA-256 Match: The installed running executable matches the remote patch binary bit-for-bit
        if (!string.IsNullOrEmpty(normRemoteHash) && !string.IsNullOrEmpty(normLocalExeHash) &&
            normRemoteHash.Equals(normLocalExeHash, StringComparison.OrdinalIgnoreCase))
        {
            return PatchDiscoveryResult.UpToDate(
                PatchMatchKind.ExactBinaryMatch,
                "Currently executing binary matches remote patch SHA-256 exactly.");
        }

        // 2. Persistent Installed State Match: This exact patch commit or hash was already installed
        if (!string.IsNullOrEmpty(normRemoteSha) && !string.IsNullOrEmpty(normLastSha) &&
            normRemoteSha.Equals(normLastSha, StringComparison.OrdinalIgnoreCase))
        {
            return PatchDiscoveryResult.UpToDate(
                PatchMatchKind.PersistentInstalledMatch,
                $"Patch commit '{normRemoteSha[..Math.Min(7, normRemoteSha.Length)]}' is already recorded as installed.");
        }

        if (!string.IsNullOrEmpty(normRemoteHash) && !string.IsNullOrEmpty(normLastHash) &&
            normRemoteHash.Equals(normLastHash, StringComparison.OrdinalIgnoreCase))
        {
            return PatchDiscoveryResult.UpToDate(
                PatchMatchKind.PersistentInstalledMatch,
                "Patch binary SHA-256 is already recorded as installed.");
        }

        // 3. Exact Git Commit SHA Match: The running assembly is compiled from the same commit
        if (!string.IsNullOrEmpty(normRemoteSha) && !normLocalSha.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            bool isSameCommit = normRemoteSha.StartsWith(normLocalSha, StringComparison.OrdinalIgnoreCase) ||
                                normLocalSha.StartsWith(normRemoteSha, StringComparison.OrdinalIgnoreCase);

            if (isSameCommit)
            {
                return PatchDiscoveryResult.UpToDate(
                    PatchMatchKind.ExactCommitMatch,
                    $"Running assembly commit '{normLocalSha[..Math.Min(7, normLocalSha.Length)]}' matches remote commit.");
            }

            // 4. Different Git Commit: This is a new patch commit on main
            // Guard against running a newer local dev build with a remote stale commit
            if (remoteTimestampUtc < localBuildDateUtc.AddMinutes(-30))
            {
                return PatchDiscoveryResult.UpToDate(
                    PatchMatchKind.UpToDate,
                    "Remote patch is older than current local build.");
            }

            return PatchDiscoveryResult.UpdateAvailable(
                PatchMatchKind.NewerCommitFound,
                $"New patch commit '{normRemoteSha[..Math.Min(7, normRemoteSha.Length)]}' differs from running '{normLocalSha[..Math.Min(7, normLocalSha.Length)]}'.");
        }

        // 5. Fallback when local commit is unknown: Use timestamp comparison with safety buffer
        if (remoteTimestampUtc > localBuildDateUtc.AddMinutes(2))
        {
            return PatchDiscoveryResult.UpdateAvailable(
                PatchMatchKind.TimestampFallbackNewer,
                "Remote patch build timestamp is newer than current executable.");
        }

        return PatchDiscoveryResult.UpToDate(
            PatchMatchKind.UpToDate,
            "Local build is up to date.");
    }
}
