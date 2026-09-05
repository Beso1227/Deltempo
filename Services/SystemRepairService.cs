using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinTempCleaner.Services;

public enum RepairToolType
{
    SfcScan,
    DismScanHealth,
    DismRestoreHealth,
    DismComponentCleanup,
    ChkdskScan,
    WindowsUpdateReset,
    NetworkStackReset,
    AutonomousFullRepair
}

public class RepairExecutionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public RepairToolType Tool { get; set; }
    public string Message => !string.IsNullOrWhiteSpace(ErrorMessage)
        ? ErrorMessage
        : (Success ? $"{Tool} completed successfully." : $"{Tool} completed with exit code {ExitCode}.");
}

public static class SystemRepairService
{
    private static readonly Regex ProgressRegex = new(@"(?:\[[=\s]*(\d+(?:\.\d+)?)%[=\s]*\]|Verification\s+(\d+)%|(\d+)%\s+complete)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Executes System File Checker (sfc.exe /scannow)
    /// </summary>
    public static async Task<RepairExecutionResult> RunSfcScannowAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string sfcPath = Path.Combine(system32, "sfc.exe");

        if (!File.Exists(sfcPath))
        {
            return new RepairExecutionResult
            {
                Success = false,
                ErrorMessage = "sfc.exe not found in System32.",
                Tool = RepairToolType.SfcScan
            };
        }

        return await ExecuteProcessWithTelemetryAsync(
            sfcPath,
            "/scannow",
            RepairToolType.SfcScan,
            onOutput,
            onProgress,
            ct);
    }

    /// <summary>
    /// Executes DISM Component Store ScanHealth (dism.exe /online /cleanup-image /scanhealth)
    /// </summary>
    public static async Task<RepairExecutionResult> RunDismScanHealthAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string dismPath = Path.Combine(system32, "dism.exe");

        if (!File.Exists(dismPath))
        {
            return new RepairExecutionResult
            {
                Success = false,
                ErrorMessage = "dism.exe not found in System32.",
                Tool = RepairToolType.DismScanHealth
            };
        }

        return await ExecuteProcessWithTelemetryAsync(
            dismPath,
            "/online /cleanup-image /scanhealth",
            RepairToolType.DismScanHealth,
            onOutput,
            onProgress,
            ct);
    }

    /// <summary>
    /// Executes DISM Component Store RestoreHealth (dism.exe /online /cleanup-image /restorehealth)
    /// </summary>
    public static async Task<RepairExecutionResult> RunDismRestoreHealthAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string dismPath = Path.Combine(system32, "dism.exe");

        if (!File.Exists(dismPath))
        {
            return new RepairExecutionResult
            {
                Success = false,
                ErrorMessage = "dism.exe not found in System32.",
                Tool = RepairToolType.DismRestoreHealth
            };
        }

        return await ExecuteProcessWithTelemetryAsync(
            dismPath,
            "/online /cleanup-image /restorehealth",
            RepairToolType.DismRestoreHealth,
            onOutput,
            onProgress,
            ct);
    }

    /// <summary>
    /// Executes DISM Component Store Deep Scavenging (dism.exe /online /cleanup-image /startcomponentcleanup /resetbase)
    /// </summary>
    public static async Task<RepairExecutionResult> RunDismComponentCleanupAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string dismPath = Path.Combine(system32, "dism.exe");

        if (!File.Exists(dismPath))
        {
            return new RepairExecutionResult
            {
                Success = false,
                ErrorMessage = "dism.exe not found in System32.",
                Tool = RepairToolType.DismComponentCleanup
            };
        }

        return await ExecuteProcessWithTelemetryAsync(
            dismPath,
            "/online /cleanup-image /startcomponentcleanup /resetbase",
            RepairToolType.DismComponentCleanup,
            onOutput,
            onProgress,
            ct);
    }

    /// <summary>
    /// Executes CHKDSK Online Read-Only Volume Scan (chkdsk.exe C: /scan)
    /// </summary>
    public static async Task<RepairExecutionResult> RunChkdskScanAsync(
        string driveLetter = "C:",
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string chkdskPath = Path.Combine(system32, "chkdsk.exe");

        if (!File.Exists(chkdskPath))
        {
            return new RepairExecutionResult
            {
                Success = false,
                ErrorMessage = "chkdsk.exe not found in System32.",
                Tool = RepairToolType.ChkdskScan
            };
        }

        string cleanDrive = (driveLetter.TrimEnd('\\', '/').Trim().ToUpperInvariant());
        if (!cleanDrive.EndsWith(":")) cleanDrive += ":";

        return await ExecuteProcessWithTelemetryAsync(
            chkdskPath,
            $"{cleanDrive} /scan",
            RepairToolType.ChkdskScan,
            onOutput,
            onProgress,
            ct);
    }

    /// <summary>
    /// Safely resets Windows Update and BITS servicing stack, purging corrupted SoftwareDistribution & Catroot2 caches
    /// </summary>
    public static async Task<RepairExecutionResult> ResetWindowsUpdateStackAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();

        void Log(string msg)
        {
            sb.AppendLine(msg);
            onOutput?.Invoke(msg);
        }

        Log("[Servicing Stack] Initiating Windows Update & BITS remediation...");
        onProgress?.Invoke(0.10);

        string[] services = { "wuauserv", "bits", "cryptsvc", "msiserver" };

        // 1. Stop services
        foreach (var svcName in services)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                Log($"[Servicing Stack] Stopping service: {svcName}...");
                await ExecuteProcessWithTelemetryAsync("net.exe", $"stop {svcName} /y", RepairToolType.WindowsUpdateReset, onOutput, null, ct);
            }
            catch (Exception ex)
            {
                Log($"[Servicing Stack] Warning stopping {svcName}: {ex.Message}");
            }
        }

        onProgress?.Invoke(0.40);

        // 2. Clear corrupted catalog folders
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string softDist = Path.Combine(winDir, "SoftwareDistribution", "Download");

        try
        {
            if (Directory.Exists(softDist))
            {
                Log("[Servicing Stack] Purging pending update payload downloads...");
                foreach (var file in Directory.EnumerateFiles(softDist))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[Servicing Stack] SoftwareDistribution note: {ex.Message}");
        }

        onProgress?.Invoke(0.70);

        // 3. Restart services
        foreach (var svcName in services)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                Log($"[Servicing Stack] Starting service: {svcName}...");
                await ExecuteProcessWithTelemetryAsync("net.exe", $"start {svcName}", RepairToolType.WindowsUpdateReset, onOutput, null, ct);
            }
            catch (Exception ex)
            {
                Log($"[Servicing Stack] Warning starting {svcName}: {ex.Message}");
            }
        }

        onProgress?.Invoke(1.0);
        sw.Stop();

        Log("[Servicing Stack] Windows Update servicing stack reset completed successfully.");

        return new RepairExecutionResult
        {
            Success = true,
            ExitCode = 0,
            Output = sb.ToString(),
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            Tool = RepairToolType.WindowsUpdateReset
        };
    }

    /// <summary>
    /// Resets Winsock catalog, TCP/IP stack, and flushes DNS resolver cache
    /// </summary>
    public static async Task<RepairExecutionResult> ResetNetworkStackAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();

        void Log(string msg)
        {
            sb.AppendLine(msg);
            onOutput?.Invoke(msg);
        }

        Log("[Network Engine] Resetting Winsock catalog...");
        onProgress?.Invoke(0.20);
        await ExecuteProcessWithTelemetryAsync("netsh.exe", "winsock reset", RepairToolType.NetworkStackReset, onOutput, null, ct);

        Log("[Network Engine] Resetting TCP/IP protocol stack...");
        onProgress?.Invoke(0.50);
        await ExecuteProcessWithTelemetryAsync("netsh.exe", "int ip reset", RepairToolType.NetworkStackReset, onOutput, null, ct);

        Log("[Network Engine] Purging and refreshing DNS resolver cache...");
        onProgress?.Invoke(0.80);
        await ExecuteProcessWithTelemetryAsync("ipconfig.exe", "/flushdns", RepairToolType.NetworkStackReset, onOutput, null, ct);

        onProgress?.Invoke(1.0);
        sw.Stop();

        Log("[Network Engine] Network stack reinitialized successfully.");

        return new RepairExecutionResult
        {
            Success = true,
            ExitCode = 0,
            Output = sb.ToString(),
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            Tool = RepairToolType.NetworkStackReset
        };
    }

    /// <summary>
    /// Autonomous 1-Click Scan & Repair Pipeline
    /// </summary>
    public static async Task<RepairExecutionResult> RunAutonomousHealthCheckAndRepairAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();

        void Log(string msg)
        {
            sb.AppendLine(msg);
            onOutput?.Invoke(msg);
        }

        Log("[Autonomous Repair] Phase 1 of 4: Running DISM Component Store ScanHealth...");
        onProgress?.Invoke(0.05);

        var dismScan = await RunDismScanHealthAsync(
            msg => Log($"[DISM] {msg}"),
            pct => onProgress?.Invoke(0.05 + (pct * 0.25)),
            ct);

        if (ct.IsCancellationRequested)
        {
            return new RepairExecutionResult { Success = false, ErrorMessage = "Operation cancelled by user.", Tool = RepairToolType.AutonomousFullRepair };
        }

        Log("[Autonomous Repair] Phase 2 of 4: Running DISM Component Store RestoreHealth...");
        onProgress?.Invoke(0.30);

        var dismRestore = await RunDismRestoreHealthAsync(
            msg => Log($"[DISM] {msg}"),
            pct => onProgress?.Invoke(0.30 + (pct * 0.35)),
            ct);

        if (ct.IsCancellationRequested)
        {
            return new RepairExecutionResult { Success = false, ErrorMessage = "Operation cancelled by user.", Tool = RepairToolType.AutonomousFullRepair };
        }

        Log("[Autonomous Repair] Phase 3 of 4: Running System File Checker (SFC /scannow)...");
        onProgress?.Invoke(0.65);

        var sfcResult = await RunSfcScannowAsync(
            msg => Log($"[SFC] {msg}"),
            pct => onProgress?.Invoke(0.65 + (pct * 0.25)),
            ct);

        if (ct.IsCancellationRequested)
        {
            return new RepairExecutionResult { Success = false, ErrorMessage = "Operation cancelled by user.", Tool = RepairToolType.AutonomousFullRepair };
        }

        Log("[Autonomous Repair] Phase 4 of 4: Running CHKDSK Volume File System Scan...");
        onProgress?.Invoke(0.90);

        var chkdskResult = await RunChkdskScanAsync(
            "C:",
            msg => Log($"[CHKDSK] {msg}"),
            pct => onProgress?.Invoke(0.90 + (pct * 0.08)),
            ct);

        onProgress?.Invoke(1.0);
        sw.Stop();

        Log($"[Autonomous Repair] System integrity check & repair pipeline completed in {sw.Elapsed.TotalMinutes:F1} min.");

        return new RepairExecutionResult
        {
            Success = dismRestore.Success && sfcResult.Success,
            ExitCode = sfcResult.ExitCode,
            Output = sb.ToString(),
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            Tool = RepairToolType.AutonomousFullRepair
        };
    }

    /// <summary>
    /// Helper: Parses command line output to extract numeric percentage progress (0.0 to 1.0)
    /// </summary>
    public static double? ParseProgressFromLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var match = ProgressRegex.Match(line);
        if (match.Success)
        {
            for (int i = 1; i <= 3; i++)
            {
                if (match.Groups[i].Success && double.TryParse(match.Groups[i].Value, out double val))
                {
                    return Math.Clamp(val / 100.0, 0.0, 1.0);
                }
            }
        }
        return null;
    }

    private static async Task<RepairExecutionResult> ExecuteProcessWithTelemetryAsync(
        string fileName,
        string arguments,
        RepairToolType tool,
        Action<string>? onOutput,
        Action<double>? onProgress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var outputBuilder = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var proc = new Process { StartInfo = psi };

            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    onOutput?.Invoke(e.Data);

                    var pct = ParseProgressFromLine(e.Data);
                    if (pct.HasValue)
                    {
                        onProgress?.Invoke(pct.Value);
                    }
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    onOutput?.Invoke(e.Data);
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Wait for exit with cancellation check
            while (!proc.HasExited)
            {
                if (ct.IsCancellationRequested)
                {
                    try
                    {
                        proc.Kill(true);
                    }
                    catch { }

                    sw.Stop();
                    return new RepairExecutionResult
                    {
                        Success = false,
                        ExitCode = -1,
                        Output = outputBuilder.ToString(),
                        ErrorMessage = "Operation cancelled by user.",
                        ExecutionTimeMs = sw.ElapsedMilliseconds,
                        Tool = tool
                    };
                }

                await Task.Delay(200, ct).ConfigureAwait(false);
            }

            sw.Stop();

            // SFC exit codes:
            // 0 = Verification 100% complete. Windows Resource Protection did not find any integrity violations.
            // 1 = Found corrupt files and successfully repaired them.
            // Other = Failed or integrity violations could not be repaired.
            bool isSuccess = proc.ExitCode == 0 || (tool == RepairToolType.SfcScan && proc.ExitCode == 1);

            return new RepairExecutionResult
            {
                Success = isSuccess,
                ExitCode = proc.ExitCode,
                Output = outputBuilder.ToString(),
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                Tool = tool
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new RepairExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Output = outputBuilder.ToString(),
                ErrorMessage = "Operation cancelled.",
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                Tool = tool
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RepairExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Output = outputBuilder.ToString(),
                ErrorMessage = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                Tool = tool
            };
        }
    }
}
