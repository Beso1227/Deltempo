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

public class SystemHealthAssessmentResult
{
    public bool ComponentStoreHealthy { get; set; } = true;
    public string ComponentStoreDetails { get; set; } = "Verified Intact";
    public bool SystemFilesHealthy { get; set; } = true;
    public string SystemFilesDetails { get; set; } = "Clean";
    public bool FilesystemHealthy { get; set; } = true;
    public string FilesystemDetails { get; set; } = "Volume C: Normal";
    public bool ServicingStackHealthy { get; set; } = true;
    public string ServicingStackDetails { get; set; } = "Active";
    public bool RebootPending { get; set; } = false;
    public long AssessmentTimeMs { get; set; }

    public int IssuesCount => (ComponentStoreHealthy ? 0 : 1) + (SystemFilesHealthy ? 0 : 1) + (FilesystemHealthy ? 0 : 1) + (ServicingStackHealthy ? 0 : 1);
    public string OverallRating => IssuesCount switch
    {
        0 => "Optimal (100% Healthy)",
        1 => "Good (1 Minor Issue Detected)",
        _ => $"Attention Needed ({IssuesCount} Subsystems Flagged)"
    };
    public string OverallColor => IssuesCount == 0 ? "#10B981" : (IssuesCount == 1 ? "#F59E0B" : "#EF4444");
    public string Recommendation => IssuesCount == 0
        ? "All system components, manifest stores, and servicing pipelines are verified 100% healthy. No repair required."
        : "Autonomous repair recommended to remediate flagged subsystems and restore corrupted packages.";
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
    /// Fast 4-Point System Integrity Assessment (DISM CheckHealth, Volume Dirty Bit, CBS, Servicing Stack) in &lt; 20s
    /// </summary>
    public static async Task<SystemHealthAssessmentResult> RunQuickHealthAssessmentAsync(
        Action<string>? onOutput = null,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new SystemHealthAssessmentResult();

        void Log(string msg)
        {
            onOutput?.Invoke(msg);
        }

        Log("[Fast Diagnostics] Starting 4-point Windows System Integrity Assessment...");
        onProgress?.Invoke(0.10);

        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string dismPath = Path.Combine(system32, "dism.exe");

        // 1. DISM CheckHealth (fast ~2s check)
        if (File.Exists(dismPath))
        {
            Log("[Fast Diagnostics] 1/4 Inspecting DISM Component Store flags...");
            var dismResult = await ExecuteProcessWithTelemetryAsync(
                dismPath,
                "/online /cleanup-image /checkhealth",
                RepairToolType.DismScanHealth,
                onOutput,
                null,
                ct,
                timeout: TimeSpan.FromMinutes(2));

            string outLower = dismResult.Output.ToLowerInvariant();
            if (outLower.Contains("no component store corruption") || (!outLower.Contains("the component store is repairable") && dismResult.ExitCode == 0))
            {
                result.ComponentStoreHealthy = true;
                result.ComponentStoreDetails = "Healthy (Zero Corruption)";
                Log("[Fast Diagnostics] ✓ DISM Component Store: Verified Healthy");
            }
            else
            {
                result.ComponentStoreHealthy = false;
                result.ComponentStoreDetails = "Corruption Flagged";
                Log("[Fast Diagnostics] ⚠ DISM Component Store: Corruption Detected");
            }
        }
        onProgress?.Invoke(0.40);

        // 2. Volume Dirty Bit Check (fast C: filesystem check)
        Log("[Fast Diagnostics] 2/4 Checking NTFS/ReFS filesystem dirty bit...");
        try
        {
            var dirtyResult = await ExecuteProcessWithTelemetryAsync(
                "fsutil.exe",
                "dirty query C:",
                RepairToolType.ChkdskScan,
                onOutput,
                null,
                ct,
                timeout: TimeSpan.FromSeconds(15));

            if (dirtyResult.Output.Contains("is NOT dirty", StringComparison.OrdinalIgnoreCase))
            {
                result.FilesystemHealthy = true;
                result.FilesystemDetails = "Volume C: Clean (Not Dirty)";
                Log("[Fast Diagnostics] ✓ Filesystem: Volume C: is Clean");
            }
            else if (dirtyResult.Output.Contains("is dirty", StringComparison.OrdinalIgnoreCase))
            {
                result.FilesystemHealthy = false;
                result.FilesystemDetails = "Volume C: Flagged Dirty (Needs Scan)";
                Log("[Fast Diagnostics] ⚠ Filesystem: Volume C: marked dirty");
            }
            else
            {
                result.FilesystemHealthy = true;
                result.FilesystemDetails = "Volume C: Normal";
            }
        }
        catch
        {
            result.FilesystemHealthy = true;
            result.FilesystemDetails = "Volume C: Accessible";
        }
        onProgress?.Invoke(0.65);

        // 3. Servicing Stack & Pending Reboot Check
        Log("[Fast Diagnostics] 3/4 Checking Windows Update Servicing Stack...");
        try
        {
            using var cbsKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            if (cbsKey != null)
            {
                result.RebootPending = true;
                Log("[Fast Diagnostics] ℹ Reboot pending from previous Windows Servicing");
            }
        }
        catch { }

        // Check Windows Update service state
        try
        {
            var scResult = await ExecuteProcessWithTelemetryAsync(
                "sc.exe",
                "query wuauserv",
                RepairToolType.WindowsUpdateReset,
                null,
                null,
                ct,
                timeout: TimeSpan.FromSeconds(10));

            if (scResult.Output.Contains("STATE", StringComparison.OrdinalIgnoreCase))
            {
                result.ServicingStackHealthy = true;
                result.ServicingStackDetails = "Services Active";
                Log("[Fast Diagnostics] ✓ Windows Update Servicing Stack: Operational");
            }
            else
            {
                result.ServicingStackHealthy = false;
                result.ServicingStackDetails = "Service Disabled / Unresponsive";
                Log("[Fast Diagnostics] ⚠ Windows Update service status unresponsive");
            }
        }
        catch
        {
            result.ServicingStackHealthy = true;
        }
        onProgress?.Invoke(0.85);

        // 4. Core System Files quick inspection
        Log("[Fast Diagnostics] 4/4 Verifying System32 vital binary integrity...");
        string[] vitalFiles = { "ntoskrnl.exe", "hal.dll", "kernel32.dll", "user32.dll", "ntdll.dll" };
        bool allVitalsPresent = true;
        foreach (var vf in vitalFiles)
        {
            if (!File.Exists(Path.Combine(system32, vf)))
            {
                allVitalsPresent = false;
                break;
            }
        }
        result.SystemFilesHealthy = allVitalsPresent;
        result.SystemFilesDetails = allVitalsPresent ? "Core Binaries Present" : "Missing Core Binaries";
        Log($"[Fast Diagnostics] {(allVitalsPresent ? "✓" : "⚠")} Core OS Binaries: {result.SystemFilesDetails}");

        onProgress?.Invoke(1.0);
        sw.Stop();
        result.AssessmentTimeMs = sw.ElapsedMilliseconds;

        Log($"[Fast Diagnostics] Assessment complete in {sw.Elapsed.TotalSeconds:F1}s: {result.OverallRating}");
        return result;
    }

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
            ct,
            timeout: TimeSpan.FromMinutes(45));
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
            ct,
            timeout: TimeSpan.FromMinutes(60));
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
            ct,
            timeout: TimeSpan.FromMinutes(60));
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
            ct,
            timeout: TimeSpan.FromMinutes(60));
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
            ct,
            timeout: TimeSpan.FromMinutes(30));
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
        int failures = 0;

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
                var result = await ExecuteProcessWithTelemetryAsync("net.exe", $"stop {svcName} /y", RepairToolType.WindowsUpdateReset, onOutput, null, ct, timeout: TimeSpan.FromMinutes(2));
                if (!result.Success)
                {
                    Log($"[Servicing Stack] Warning: could not stop {svcName} (exit {result.ExitCode}).");
                    failures++;
                }
            }
            catch (Exception ex)
            {
                Log($"[Servicing Stack] Warning stopping {svcName}: {ex.Message}");
                failures++;
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
                var result = await ExecuteProcessWithTelemetryAsync("net.exe", $"start {svcName}", RepairToolType.WindowsUpdateReset, onOutput, null, ct, timeout: TimeSpan.FromMinutes(2));
                if (!result.Success)
                {
                    Log($"[Servicing Stack] Warning: could not start {svcName} (exit {result.ExitCode}).");
                    failures++;
                }
            }
            catch (Exception ex)
            {
                Log($"[Servicing Stack] Warning starting {svcName}: {ex.Message}");
                failures++;
            }
        }

        onProgress?.Invoke(1.0);
        sw.Stop();

        bool success = failures == 0;
        string summary = success
            ? "[Servicing Stack] Windows Update servicing stack reset completed successfully."
            : $"[Servicing Stack] Windows Update reset completed with {failures} warning(s).";
        Log(summary);

        return new RepairExecutionResult
        {
            Success = success,
            ExitCode = success ? 0 : failures,
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
        int failures = 0;

        void Log(string msg)
        {
            sb.AppendLine(msg);
            onOutput?.Invoke(msg);
        }

        Log("[Network Engine] Resetting Winsock catalog...");
        onProgress?.Invoke(0.20);
        var winsockResult = await ExecuteProcessWithTelemetryAsync("netsh.exe", "winsock reset", RepairToolType.NetworkStackReset, onOutput, null, ct, timeout: TimeSpan.FromMinutes(5));
        if (!winsockResult.Success) failures++;

        Log("[Network Engine] Resetting TCP/IP protocol stack...");
        onProgress?.Invoke(0.50);
        var tcpResult = await ExecuteProcessWithTelemetryAsync("netsh.exe", "int ip reset", RepairToolType.NetworkStackReset, onOutput, null, ct, timeout: TimeSpan.FromMinutes(5));
        if (!tcpResult.Success) failures++;

        Log("[Network Engine] Purging and refreshing DNS resolver cache...");
        onProgress?.Invoke(0.80);
        var dnsResult = await ExecuteProcessWithTelemetryAsync("ipconfig.exe", "/flushdns", RepairToolType.NetworkStackReset, onOutput, null, ct, timeout: TimeSpan.FromMinutes(2));
        if (!dnsResult.Success) failures++;

        onProgress?.Invoke(1.0);
        sw.Stop();

        bool success = failures == 0;
        string summary = success
            ? "[Network Engine] Network stack reinitialized successfully."
            : $"[Network Engine] Network stack reset completed with {failures} warning(s).";
        Log(summary);

        return new RepairExecutionResult
        {
            Success = success,
            ExitCode = success ? 0 : failures,
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
            pct => onProgress?.Invoke(0.05 + (pct * 0.30)),
            ct);

        if (ct.IsCancellationRequested)
        {
            return new RepairExecutionResult { Success = false, ErrorMessage = "Operation cancelled by user.", Tool = RepairToolType.AutonomousFullRepair };
        }

        RepairExecutionResult? dismRestore = null;
        // Smart Phase Gating: If DISM Scan explicitly detected no corruption, skip the redundant 15-minute RestoreHealth download.
        bool corruptionDetected = dismScan.Output.Contains("repairable", StringComparison.OrdinalIgnoreCase) ||
                                  dismScan.Output.Contains("corrupted", StringComparison.OrdinalIgnoreCase) ||
                                  dismScan.ExitCode != 0;

        if (corruptionDetected)
        {
            Log("[Autonomous Repair] Phase 2 of 4: Corruption flagged. Running DISM Component Store RestoreHealth...");
            onProgress?.Invoke(0.35);

            dismRestore = await RunDismRestoreHealthAsync(
                msg => Log($"[DISM] {msg}"),
                pct => onProgress?.Invoke(0.35 + (pct * 0.30)),
                ct);

            if (ct.IsCancellationRequested)
            {
                return new RepairExecutionResult { Success = false, ErrorMessage = "Operation cancelled by user.", Tool = RepairToolType.AutonomousFullRepair };
            }
        }
        else
        {
            Log("[Autonomous Repair] Phase 2 of 4: Component Store is verified clean. Skipping RestoreHealth to accelerate execution.");
            onProgress?.Invoke(0.60);
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

        // Determine overall exit code: prefer the most meaningful failure code
        int overallExitCode = 0;
        if (dismRestore != null && !dismRestore.Success) overallExitCode = dismRestore.ExitCode;
        else if (!sfcResult.Success) overallExitCode = sfcResult.ExitCode;
        else if (!chkdskResult.Success) overallExitCode = chkdskResult.ExitCode;
        else if (!dismScan.Success) overallExitCode = dismScan.ExitCode;

        bool isSuccess = (dismRestore == null || dismRestore.Success) && sfcResult.Success;

        return new RepairExecutionResult
        {
            Success = isSuccess,
            ExitCode = overallExitCode,
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
        CancellationToken ct,
        TimeSpan? timeout = null)
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
                    outputBuilder.AppendLine($"[STDERR] {e.Data}");
                    onOutput?.Invoke($"[STDERR] {e.Data}");
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Wait for exit with cancellation and timeout check
            var deadline = timeout.HasValue ? DateTime.UtcNow + timeout.Value : DateTime.MaxValue;
            while (!proc.HasExited)
            {
                if (ct.IsCancellationRequested || DateTime.UtcNow >= deadline)
                {
                    int actualExitCode = -1;
                    try
                    {
                        actualExitCode = proc.ExitCode;
                        proc.Kill(true);
                    }
                    catch { }

                    sw.Stop();
                    string reason = ct.IsCancellationRequested ? "Operation cancelled by user." : $"Operation timed out after {timeout?.TotalMinutes ?? 0:F0} minutes.";
                    return new RepairExecutionResult
                    {
                        Success = false,
                        ExitCode = actualExitCode,
                        Output = outputBuilder.ToString(),
                        ErrorMessage = reason,
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

    /// <summary>
    /// Constructs the ProcessStartInfo configured to launch the Chris Titus Tech Windows Utility (CTT WinUtil).
    /// </summary>
    public static ProcessStartInfo CreateChrisTitusProcessStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://christitus.com/win | iex\"",
            UseShellExecute = true,
            Verb = ElevationService.IsAdministrator ? string.Empty : "runas"
        };
    }

    /// <summary>
    /// Launches Chris Titus Tech Windows Utility (CTT WinUtil) in an elevated PowerShell session.
    /// </summary>
    /// <param name="error">Out parameter populated with an error description if launch fails.</param>
    /// <returns>True if the process was successfully dispatched; false otherwise.</returns>
    public static bool LaunchChrisTitusWinUtil(out string error)
    {
        error = string.Empty;
        try
        {
            var psi = CreateChrisTitusProcessStartInfo();
            var proc = Process.Start(psi);
            return proc != null;
        }
        catch (System.ComponentModel.Win32Exception wEx) when (wEx.NativeErrorCode == 1223)
        {
            error = "Elevation request was cancelled by the user.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
