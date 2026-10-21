using System.IO;
using System.Runtime.InteropServices;
using WinTempCleaner.Core.Cleaning;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class ForceDeleteService
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static StubbornTargetProfile InspectPath(string path)
    {
        return ForceDeleteEngine.Profile(path);
    }

    public static async Task<ForceDeleteResult> ExecuteForceDeleteAsync(
        IEnumerable<string> targets,
        ForceDeleteOptions options,
        Action<string, LogLevel>? logAction = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var result = await ForceDeleteEngine.ForceDeleteAsync(targets, options, logAction, progress, ct);

        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }

        WriteAuditLog(result);

        return result;
    }

    private static void WriteAuditLog(ForceDeleteResult result)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string auditDir = Path.Combine(appData, "Deltempo");
            Directory.CreateDirectory(auditDir);
            string auditFile = Path.Combine(auditDir, "force-delete-audit.log");

            using var sw = new StreamWriter(auditFile, append: true);
            foreach (var att in result.Attempts)
            {
                sw.WriteLine($"[{DateTime.UtcNow:O}] Stage={att.Stage} Success={att.Success} Error={att.Win32Error} Path={att.Path} Note={att.Message}");
            }
        }
        catch { }
    }
}
