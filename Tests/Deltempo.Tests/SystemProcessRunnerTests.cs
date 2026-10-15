using System.Diagnostics;
using WinTempCleaner.Core.Abstractions;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Guards the two ways the old hand-rolled process handling failed:
/// an undrained pipe that deadlocked the caller forever, and a
/// WaitForExit(timeout) that never applied because the read came first.
/// </summary>
[Collection("ProcessRunner")]
public class SystemProcessRunnerTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task RunAsync_CapturesStdoutAndExitCode()
    {
        ProcessExecutionResult result = await SystemProcessRunner.Instance.RunAsync(
            "cmd.exe", "/c echo hello-deltempo", Generous);

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello-deltempo", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_SurfacesNonZeroExitCode()
    {
        ProcessExecutionResult result = await SystemProcessRunner.Instance.RunAsync(
            "cmd.exe", "/c exit 3", Generous);

        Assert.False(result.TimedOut);
        Assert.Equal(3, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_DrainsBothPipes_ChildWritingFarMoreThanThePipeBufferDoesNotDeadlock()
    {
        // ~1 MB of stdout. That is well past the 64 KB anonymous-pipe buffer, so the
        // child blocks on write unless *both* streams are being drained concurrently.
        // The old shape drained one stream synchronously and then waited, so the child
        // blocked forever and the reader blocked forever with it.
        ProcessExecutionResult result = await SystemProcessRunner.Instance.RunAsync(
            "cmd.exe",
            "/c for /L %i in (1,1,5000) do @echo .......................................................................................................................",
            Generous);

        Assert.False(result.TimedOut);
        Assert.True(
            result.StandardOutput.Length > 64 * 1024,
            $"expected more than one pipe buffer of stdout, got {result.StandardOutput.Length} chars");
    }

    [Fact]
    public async Task RunAsync_TimeoutIsEnforcedAndTheChildIsKilled()
    {
        // ~29 s of work against a 2 s budget. The old code called WaitForExit(timeout)
        // only after a blocking ReadToEnd had already returned, so the timeout was
        // decorative; with no redirected output this is the cleanest reproduction of
        // "the caller waits far longer than it asked to".
        var sw = Stopwatch.StartNew();
        ProcessExecutionResult result = await SystemProcessRunner.Instance.RunAsync(
            "cmd.exe", "/c ping -n 30 127.0.0.1 > nul", TimeSpan.FromSeconds(2));
        sw.Stop();

        Assert.True(result.TimedOut, "a 2s budget against a ~29s child must report a timeout");
        Assert.True(result.WasTerminated);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(25), $"returned after {sw.Elapsed}, expected the kill to land near 2s");
    }

    [Fact]
    public async Task RunAsync_ChildThatExitsCleanlyIsNotReportedAsTimedOut()
    {
        ProcessExecutionResult result = await SystemProcessRunner.Instance.RunAsync(
            "cmd.exe", "/c ping -n 2 127.0.0.1 > nul", Generous);

        Assert.False(result.TimedOut);
        Assert.False(result.WasTerminated);
    }

    [Fact]
    public async Task RunAsync_MissingExecutableIsReportedNotThrown()
    {
        ProcessExecutionResult result = await SystemProcessRunner.Instance.RunAsync(
            "deltempo-definitely-not-a-real-binary.exe", string.Empty, Generous);

        Assert.False(result.TimedOut);
        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.StandardError);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationPropagates()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SystemProcessRunner.Instance.RunAsync(
            "cmd.exe", "/c ping -n 30 127.0.0.1 > nul", Generous, cts.Token));
    }

    [Fact]
    public void Run_BlockingOverloadReturnsInsteadOfHanging()
    {
        ProcessExecutionResult result = SystemProcessRunner.Instance.Run(
            "cmd.exe", "/c echo blocking-overload", TimeSpan.FromSeconds(20));

        Assert.False(result.TimedOut);
        Assert.Contains("blocking-overload", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }
}
