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

    private const int HWND_BROADCAST = 0xffff;
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }
        catch
        {
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
        }
        catch { }
    }

    public static void Release()
    {
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;
        }
        catch { }
    }
}
