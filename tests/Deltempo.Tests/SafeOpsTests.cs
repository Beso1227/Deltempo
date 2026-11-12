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
        // Must not propagate: best-effort call sites rely on this. The assertion makes
        // the "and continues" half observable - the test used to contain no assert at
        // all, so it only failed if the exception escaped, never if the call swallowed
        // something it should have reported.
        bool reachedAfter = false;

        SafeOps.Try("test", () => throw new InvalidOperationException("boom"));

        reachedAfter = true;
        Assert.True(reachedAfter, "SafeOps.Try must swallow the exception so the caller continues.");
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
    public void Try_WithThrowingFunc_ReturnsFalse()
    {
        // SafeOps.Try(operation, func, fallback) is the live API - RootLeftoverPurgeService
        // uses it. The old TryBool wrapper had no product caller; this pins the real one.
        bool result = SafeOps.Try("test", () => throw new InvalidOperationException("boom"), false);

        Assert.False(result);
    }

    [Fact]
    public void Try_WithNonThrowingFunc_ReturnsTrue()
    {
        bool result = SafeOps.Try("test", () => true, false);

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
