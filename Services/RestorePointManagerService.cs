using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class RestorePointDetail
{
    public int SequenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string EventType { get; set; } = "Checkpoint";
    public DateTime CreationTime { get; set; }
    public string FormattedTime => CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string ShadowId { get; set; } = string.Empty;
}

public class RestorePointStorageInfo
{
    public long UsedBytes { get; set; }
    public string FormattedUsed => TargetFolderInfo.FormatBytes(UsedBytes);
    public int SnapshotCount { get; set; }
    public List<RestorePointDetail> Points { get; set; } = new();
}

[SupportedOSPlatform("windows")]
public static class RestorePointManagerService
{
    public static async Task<RestorePointStorageInfo> QueryDetailedRestorePointsAsync()
    {
        return await Task.Run(() =>
        {
            var info = new RestorePointStorageInfo();

            if (!ElevationService.IsRunAsAdmin())
            {
                return info;
            }

            var (usedBytes, snapshotCount) = CleanerService.QueryShadowStorageInfo();
            info.UsedBytes = usedBytes;
            info.SnapshotCount = snapshotCount;

            // 1. Query System Restore Points via PowerShell Get-ComputerRestorePoint
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command \"Get-ComputerRestorePoint | Select-Object -Property SequenceNumber,Description,CreationTime,RestorePointType | ConvertTo-Csv -NoTypeInformation\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(6000);

                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (lines.Length > 1) // skip header
                    {
                        foreach (var rawLine in lines.Skip(1))
                        {
                            var parts = ParseCsvLine(rawLine);
                            if (parts.Count >= 3)
                            {
                                int.TryParse(parts[0], out int seq);
                                string desc = parts[1];
                                DateTime dt = DateTime.Now;
                                DateTime.TryParse(parts[2], out dt);

                                string typeDesc = "System Checkpoint";
                                if (parts.Count >= 4 && int.TryParse(parts[3], out int typeCode))
                                {
                                    typeDesc = typeCode switch
                                    {
                                        0 => "Application Install",
                                        1 => "Application Uninstall",
                                        10 => "Device Driver Install",
                                        12 => "System Modify",
                                        _ => "System Checkpoint"
                                    };
                                }

                                info.Points.Add(new RestorePointDetail
                                {
                                    SequenceNumber = seq,
                                    Description = desc,
                                    CreationTime = dt,
                                    EventType = typeDesc
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RestorePointManager] PowerShell query failed: {ex.Message}");
            }

            // 2. Fallback to parsing vssadmin list shadows
            if (info.Points.Count == 0)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "vssadmin.exe"),
                        Arguments = "list shadows",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(8000);

                        int seq = 1;
                        RestorePointDetail? current = null;

                        foreach (var rawLine in output.Split('\n'))
                        {
                            string line = rawLine.Trim();
                            if (line.StartsWith("Shadow Copy ID:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (current != null) info.Points.Add(current);
                                current = new RestorePointDetail
                                {
                                    SequenceNumber = seq++,
                                    ShadowId = line.Substring(15).Trim()
                                };
                            }
                            else if (current != null && line.StartsWith("Creation Time:", StringComparison.OrdinalIgnoreCase))
                            {
                                string dateStr = line.Substring(14).Trim();
                                if (DateTime.TryParse(dateStr, out var parsedDt))
                                {
                                    current.CreationTime = parsedDt;
                                }
                                current.Description = "Volume Shadow Copy";
                                current.EventType = "VSS Snapshot";
                            }
                        }

                        if (current != null) info.Points.Add(current);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RestorePointManager] Vssadmin fallback failed: {ex.Message}");
                }
            }

            info.Points = info.Points.OrderByDescending(p => p.CreationTime).ToList();
            if (info.SnapshotCount == 0 && info.Points.Count > 0)
            {
                info.SnapshotCount = info.Points.Count;
            }

            return info;
        });
    }

    public static async Task<(bool Success, long ReclaimedBytes, string Message)> PruneOlderRestorePointsAsync(Action<string, LogLevel>? logAction = null)
    {
        return await CleanerService.CleanRestorePointsAsync(purgeAll: false, (msg, lvl) => logAction?.Invoke(msg, lvl));
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim('"', ' '));
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim('"', ' '));
        return result;
    }
}
