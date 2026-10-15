using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinTempCleaner.Core.Abstractions;

public record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut = false)
{
    /// <summary>True when the child was killed because it exceeded its timeout.</summary>
    public bool WasTerminated => TimedOut || ExitCode < 0;
}

/// <summary>
/// Abstraction for invoking external processes (e.g. dism, cleanmgr, sfc).
/// </summary>
public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, CancellationToken ct = default);
}

/// <summary>
/// Production process runner using System.Diagnostics.Process.
/// </summary>
/// <remarks>
/// The correct way to run a child process from .NET is to start both stream reads
/// <em>before</em> waiting for exit. The old shape used across this codebase was:
///
/// <code>
/// var err = proc.StandardError.ReadToEnd();   // blocks here
/// proc.WaitForExit(5000);                    // ...so this timeout never applies
/// </code>
///
/// That has two failure modes. The read blocks until the child closes the pipe,
/// so the advertised timeout is decorative and the caller can hang forever. And
/// because only one of the two redirected streams is drained, a chatty child fills
/// the other pipe, blocks on the write, and never closes stdout - so the read that
/// is already blocking never returns either. This type drains both streams
/// concurrently and enforces the timeout for real.
/// </remarks>
public class SystemProcessRunner : IProcessRunner
{
    public static readonly SystemProcessRunner Instance = new();

    /// <summary>Infinite timeout, no kill. Prefer the overload that takes one.</summary>
    public Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
        => RunAsync(fileName, arguments, Timeout.InfiniteTimeSpan, ct, standardOutputEncoding: null);

    /// <summary>
    /// Runs a child process, draining stdout and stderr concurrently.
    /// </summary>
    /// <param name="timeout">
    /// How long to wait before the child is killed. <see cref="Timeout.InfiniteTimeSpan"/>
    /// waits forever. On expiry the whole process tree is killed and the result is
    /// returned with <see cref="ProcessExecutionResult.TimedOut"/> set.
    /// </param>
    /// <param name="standardOutputEncoding">
    /// Decoding to apply to stdout. Leave null for the default console codepage.
    /// </param>
    public async Task<ProcessExecutionResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken ct = default,
        Encoding? standardOutputEncoding = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (standardOutputEncoding != null)
        {
            psi.StandardOutputEncoding = standardOutputEncoding;
        }

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ProcessExecutionResult(-1, string.Empty, ex.Message, TimedOut: false);
        }

        // Both reads must be in flight before we wait, or a full pipe deadlocks the child.
        var stdoutTask = ReadAllAsync(process.StandardOutput, ct);
        var stderrTask = ReadAllAsync(process.StandardError, ct);

        var timedOut = false;
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            waitCts.CancelAfter(timeout);
        }

        try
        {
            await process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A caller-initiated cancellation is a real cancellation and must propagate.
            if (ct.IsCancellationRequested)
            {
                KillQuietly(process);
                throw;
            }

            // Otherwise the timeout expired: kill the tree and report it honestly
            // rather than blocking the UI thread indefinitely.
            timedOut = true;
            KillQuietly(process);
            await DrainAfterKillAsync(process).ConfigureAwait(false);
        }

        string stdout = await SafeAwaitAsync(stdoutTask, ct).ConfigureAwait(false);
        string stderr = await SafeAwaitAsync(stderrTask, ct).ConfigureAwait(false);

        return new ProcessExecutionResult(timedOut ? -1 : SafeExitCode(process), stdout, stderr, timedOut);
    }

    /// <summary>
    /// Blocking wrapper for the existing synchronous call sites.
    /// </summary>
    /// <remarks>
    /// Safe against deadlock: every await inside <see cref="RunAsync(string,string,TimeSpan,CancellationToken)"/>
    /// uses <c>ConfigureAwait(false)</c>, so no continuation is posted to the calling
    /// synchronization context. The caller still blocks for at most <paramref name="timeout"/>,
    /// which is exactly the bound the original code claimed but did not enforce.
    /// </remarks>
    public ProcessExecutionResult Run(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken ct = default,
        Encoding? standardOutputEncoding = null)
        => RunAsync(fileName, arguments, timeout, ct, standardOutputEncoding).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Reads a process' stdout to completion while draining stderr concurrently, enforcing
    /// <paramref name="timeout"/> for real and killing the child if it overruns.
    /// </summary>
    /// <remarks>
    /// Use this when the caller has already started the <see cref="Process"/> (for example to
    /// carry a richer <see cref="ProcessStartInfo"/>), but still wants the drain-both-pipes
    /// and bound-the-wait behaviour. <see cref="Run(string,string,TimeSpan,CancellationToken,Encoding)"/>
    /// is preferred wherever the runner can own the whole lifecycle.
    /// </remarks>
    public static string DrainOutput(Process process, TimeSpan timeout)
    {
        var stdoutTask = ReadAllAsync(process.StandardOutput, CancellationToken.None);
        var stderrTask = ReadAllAsync(process.StandardError, CancellationToken.None);

        using var waitCts = new CancellationTokenSource();
        waitCts.CancelAfter(timeout);

        try
        {
            process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            KillQuietly(process);
            DrainAfterKillAsync(process).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        return SafeAwaitAsync(stdoutTask, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private static async Task<string> ReadAllAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new StringBuilder();
        var chunk = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(chunk.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            buffer.Append(chunk, 0, read);
        }
        return buffer.ToString();
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may have exited between the check and the kill, or we may
            // lack rights. Either way there is nothing useful left to do here.
        }
    }

    private static async Task DrainAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best effort: we already killed it and we already report a timeout.
        }
    }

    private static int SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static async Task<string> SafeAwaitAsync(Task<string> task, CancellationToken ct)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }
            return string.Empty;
        }
    }
}
