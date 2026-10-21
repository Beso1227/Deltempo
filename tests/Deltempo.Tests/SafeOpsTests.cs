using System;
using System.Collections.Generic;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class SafeOpsTests
{
    [Fact]
    public void Try_WithNonThrowingAction_DoesNotInvokeFallback()
    {
        bool invoked = false;

        SafeOps.Try("test", () => invoked = true);

        Assert.True(invoked);
    }

    [Fact]
    public void Try_WithThrowingAction_SwallowsAndContinues()
    {
        // Must not propagate: best-effort call sites rely on this.
        SafeOps.Try("test", () => throw new InvalidOperationException("boom"));
    }

    [Fact]
    public void Try_WithThrowingAction_StillRunsRemainingLogic()
    {
        bool reachedAfter = false;

        try { throw new InvalidOperationException("boom"); }
        catch (Exception)
        {
            SafeOps.Try("test", () => throw new InvalidOperationException("nested"));
        }

        reachedAfter = true;
        Assert.True(reachedAfter);
    }

    [Fact]
    public void TryGeneric_WithThrowingFunc_ReturnsFallback()
    {
        int result = SafeOps.Try("test", () => throw new InvalidOperationException("boom"), -1);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void TryGeneric_WithNonThrowingFunc_ReturnsValue()
    {
        int result = SafeOps.Try("test", () => 42, -1);

        Assert.Equal(42, result);
    }

    [Fact]
    public void TryBool_WithThrowingFunc_ReturnsFalse()
    {
        bool result = SafeOps.TryBool("test", () => throw new InvalidOperationException("boom"));

        Assert.False(result);
    }

    [Fact]
    public void TryBool_WithNonThrowingFunc_ReturnsTrue()
    {
        bool result = SafeOps.TryBool("test", () => true);

        Assert.True(result);
    }

    [Fact]
    public void TryGeneric_WithCollectionFallback_ReturnsEmptyCollection()
    {
        List<string> files = SafeOps.Try(
            "test",
            () => throw new UnauthorizedAccessException("denied"),
            new List<string>());

        Assert.NotNull(files);
        Assert.Empty(files);
    }
}
