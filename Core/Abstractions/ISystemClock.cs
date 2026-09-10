using System;

namespace WinTempCleaner.Core.Abstractions;

/// <summary>
/// Abstraction over system time for deterministic, testable time-dependent operations.
/// </summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Production implementation using standard system UTC clock.
/// </summary>
public class SystemClock : ISystemClock
{
    public static readonly SystemClock Instance = new();
    public DateTime UtcNow => DateTime.UtcNow;
}
