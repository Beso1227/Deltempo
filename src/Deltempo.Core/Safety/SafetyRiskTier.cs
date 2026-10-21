namespace WinTempCleaner.Core.Safety;

/// <summary>
/// Formal risk classification for files encountered during scan and cleanup.
/// The system adheres strictly to the rule: "Never optimize for deleting more files.
/// Optimize for proving that every deletion is safe."
/// </summary>
public enum SafetyRiskTier
{
    /// <summary>
    /// Critical operating system components, user personal data, credentials,
    /// authentication databases, SSH keys, sessions, or source code.
    /// CANNOT BE DELETED UNDER ANY CIRCUMSTANCES.
    /// </summary>
    Protected = 0,

    /// <summary>
    /// Deterministically verified disposable cache or temporary artifact.
    /// Matches known cache patterns, resides strictly within an authorized root,
    /// is not locked, has no sensitive extensions, and is safe to purge.
    /// </summary>
    Safe = 1,

    /// <summary>
    /// Low-risk temporary files that meet age thresholds (e.g. older than 24 hours),
    /// inactive crash reports, or compiler output in recognized project temp paths.
    /// </summary>
    LowRisk = 2,

    /// <summary>
    /// Files in disposable locations that have non-standard extensions, executable code,
    /// or ambiguity regarding application state. Requires explicit user confirmation.
    /// Default action: KEEP.
    /// </summary>
    ReviewRequired = 3,

    /// <summary>
    /// Unrecognized file or ambiguous context. Safety cannot be proven.
    /// Default action: ALWAYS KEEP.
    /// </summary>
    Unknown = 4
}
