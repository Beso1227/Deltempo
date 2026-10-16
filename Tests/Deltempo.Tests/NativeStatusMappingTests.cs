using System;
using System.Reflection;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Covers the mapping from raw NTSTATUS / Win32 errors onto explicit
/// <see cref="MemoryOperationStatus"/> values.
///
/// Before this mapping existed, every non-zero status collapsed into
/// <c>MemoryOperationStatus.Failed</c> with the message "Operation rejected or privilege
/// denied". <c>AccessDenied</c> was never assigned anywhere in the codebase, and
/// <c>Unsupported</c> was reachable only through a table flag that is set to true for every
/// target - so "run as Administrator" and "this build of Windows cannot do that" were
/// indistinguishable to the user.
/// </summary>
public class NativeStatusMappingTests
{
    private static MemoryOperationStatus Map(int status)
    {
        var m = typeof(MemoryOptimizerService)
            .GetMethod("MapNativeStatus", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        return (MemoryOperationStatus)m.Invoke(null, new object[] { status })!;
    }

    private static string Describe(int status)
    {
        var m = typeof(MemoryOptimizerService)
            .GetMethod("DescribeNativeStatus", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        return (string)m.Invoke(null, new object[] { status })!;
    }

    private static int StatusFromWin32(int win32) =>
        unchecked((int)0xC0000000 | (win32 & 0xFFFF));

    // ── Success ────────────────────────────────────────────────────────────────
    [Fact]
    public void StatusZero_MapsToSuccess() => Assert.Equal(MemoryOperationStatus.Success, Map(0));

    [Fact]
    public void StatusZero_HasEmptyDescription() => Assert.Equal("", Describe(0));

    // ── Unsupported ────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(unchecked((int)0xC0000002))] // STATUS_NOT_IMPLEMENTED
    [InlineData(unchecked((int)0xC00000BB))] // STATUS_NOT_SUPPORTED
    [InlineData(unchecked((int)0xC0000003))] // STATUS_INVALID_INFO_CLASS
    public void UnsupportedStatuses_MapToUnsupported(int status)
        => Assert.Equal(MemoryOperationStatus.Unsupported, Map(status));

    // ── Access denied ──────────────────────────────────────────────────────────
    [Theory]
    [InlineData(unchecked((int)0xC0000022))] // STATUS_ACCESS_DENIED
    [InlineData(unchecked((int)0xC0000061))] // STATUS_PRIVILEGE_NOT_HELD
    public void NativeAccessDenied_MapToAccessDenied(int status)
        => Assert.Equal(MemoryOperationStatus.AccessDenied, Map(status));

    [Fact]
    public void Win32AccessDenied_FoldedIntoNtStatus_MapsToAccessDenied()
    {
        // ERROR_ACCESS_DENIED = 5 -> 0xC0000005. Deliberately NOT STATUS_ACCESS_DENIED
        // (0xC0000022); the two encodings are distinct and both must be handled.
        int folded = StatusFromWin32(5);
        Assert.Equal(unchecked((int)0xC0000005), folded);
        Assert.NotEqual(unchecked((int)0xC0000022), folded);
        Assert.Equal(MemoryOperationStatus.AccessDenied, Map(folded));
    }

    [Fact]
    public void Win32PrivilegeNotHeld_MapsToAccessDenied()
        => Assert.Equal(MemoryOperationStatus.AccessDenied, Map(StatusFromWin32(1314)));

    // ── Anything else is a plain failure ──────────────────────────────────────
    [Theory]
    [InlineData(unchecked((int)0xC0000001))] // STATUS_UNSUCCESSFUL
    [InlineData(unchecked((int)0xC000000D))] // STATUS_INVALID_PARAMETER
    [InlineData(unchecked((int)0xC000009A))] // STATUS_INSUFFICIENT_RESOURCES
    [InlineData(1)]
    [InlineData(-1)]
    public void OtherStatuses_MapToFailed(int status)
        => Assert.Equal(MemoryOperationStatus.Failed, Map(status));

    // ── Descriptions are actionable ────────────────────────────────────────────
    [Theory]
    [InlineData(unchecked((int)0xC0000022))]
    [InlineData(unchecked((int)0xC0000005))] // NTSTATUS_FROM_WIN32(ERROR_ACCESS_DENIED)
    public void AccessDenied_DescriptionTellsUserToElevate(int status)
    {
        string d = Describe(status);
        Assert.Contains("Administrator", d);
        Assert.Contains("0x", d);
    }

    [Theory]
    [InlineData(unchecked((int)0xC0000002))]
    [InlineData(unchecked((int)0xC00000BB))]
    public void Unsupported_DescriptionNamesTheBuild(int status)
    {
        string d = Describe(status);
        Assert.Contains("not supported", d);
        Assert.DoesNotContain("Administrator", d);
    }

    [Fact]
    public void Failed_DescriptionDoesNotClaimAdministratorIsTheFix()
    {
        // A generic kernel rejection must not be mislabelled as a permissions problem,
        // which would send the user to run as Administrator for no reason.
        string d = Describe(unchecked((int)0xC0000001));
        Assert.DoesNotContain("Administrator", d);
        Assert.Contains("0x", d);
    }

    /// <summary>
    /// Guards the property the whole mapping exists to provide: a caller can always tell
    /// "unsupported" from "needs elevation", and neither is reported as success.
    /// </summary>
    [Fact]
    public void Mapping_NeverReportsFailureAsSuccess()
    {
        int[] allStatuses =
        {
            0,
            unchecked((int)0xC0000001), unchecked((int)0xC0000002), unchecked((int)0xC0000003),
            unchecked((int)0xC0000022), unchecked((int)0xC0000061), unchecked((int)0xC000009A),
            unchecked((int)0xC00000BB), StatusFromWin32(5), StatusFromWin32(1314), 1, -1,
        };

        foreach (int s in allStatuses)
        {
            var mapped = Map(s);
            if (s == 0)
                Assert.Equal(MemoryOperationStatus.Success, mapped);
            else
                Assert.NotEqual(MemoryOperationStatus.Success, mapped);
        }
    }
}
