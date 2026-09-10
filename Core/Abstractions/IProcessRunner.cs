using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WinTempCleaner.Core.Abstractions;

public record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Abstraction for invoking external processes (e.g. dism, cleanmgr, sfc, winget).
/// </summary>
public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, CancellationToken ct = default);
}

/// <summary>
/// Production process runner using System.Diagnostics.Process.
/// </summary>
public class SystemProcessRunner : IProcessRunner
{
    public static readonly SystemProcessRunner Instance = new();

    public async Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
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

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        return new ProcessExecutionResult(process.ExitCode, stdout, stderr);
    }
}
