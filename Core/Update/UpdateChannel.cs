namespace WinTempCleaner.Core.Update;

/// <summary>
/// Defines the update channel for Deltempo.
/// </summary>
public enum UpdateChannel
{
    /// <summary>
    /// Automated continuous rolling builds generated directly from qualifying commits on main.
    /// </summary>
    Patch,

    /// <summary>
    /// Formal milestone production releases (e.g. v1.3.3, v1.3.4, v1.4.0).
    /// </summary>
    Stable,

    /// <summary>
    /// Intelligent auto-detection: Delivers stable milestones or latest continuous patches on current base version.
    /// </summary>
    Auto
}
