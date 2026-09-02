using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinTempCleaner.Services;

public static class SingleInstanceManager
{
    private const string MutexName = "Global\\Deltempo_App_SingleInstance_Mutex_v1";
    public const string ShowWindowMessageName = "DELTEMPO_RESTORE_SHOW_WINDOW_MSG";

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int HWND_BROADCAST = 0xffff;
    private const int SW_RESTORE = 9;
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }
        catch (AbandonedMutexException)
        {
            // Previous instance crashed or exited without clean release; ownership acquired
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            return true;
        }
    }

    public static void NotifyExistingInstance()
    {
        try
        {
            uint msg = RegisterWindowMessage(ShowWindowMessageName);
            if (msg != 0)
            {
                PostMessage((IntPtr)HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
            }

            int currentPid = Process.GetCurrentProcess().Id;
            var processes = Process.GetProcessesByName("Deltempo")
                .Concat(Process.GetProcessesByName("WinTempCleaner"));

            foreach (var p in processes)
            {
                try
                {
                    if (p.Id != currentPid)
                    {
                        var hWnd = p.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindowAsync(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    public static void Release()
    {
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
