using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WinTempCleaner.Core.Update;
using WinTempCleaner.Services;

namespace WinTempCleaner;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteConsoleInput(
        IntPtr hConsoleInput,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsWritten);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [StructLayout(LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public bool bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_RECORD_UNION
    {
        [FieldOffset(0)]
        public KEY_EVENT_RECORD KeyEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT_RECORD
    {
        public ushort EventType;
        public INPUT_RECORD_UNION Event;
    }

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const ushort KEY_EVENT = 0x0001;
    private const ushort VK_RETURN = 0x000D;

    protected override void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;

        // ═══════════════════════════════════════════════════════════════════
        // MODE 1: EMBEDDED UPDATER MODE — --update <transactionId>
        // Minimal mode: runs update transaction, returns exit code, no GUI.
        // Must be checked BEFORE any WPF initialization.
        // ═══════════════════════════════════════════════════════════════════
        if (args.Length >= 2 && args[0] == "--update")
        {
            int exitCode = RunEmbeddedUpdater(args[1]);
            Shutdown(exitCode);
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // MODE 2: NORMAL STARTUP — recover incomplete transactions first
        // ═══════════════════════════════════════════════════════════════════
        try
        {
            SetCurrentProcessExplicitAppUserModelID("Deltempo.Guardian.WindowsCleaner");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] SetAppUserModelID suppressed: {ex.Message}");
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Deltempo", "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
                File.AppendAllText(logFile, $"[{DateTime.Now}] Crash: {args.Exception}\n");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        };

        // Crash/power-loss recovery: attempt recovery of incomplete update transactions
        // BEFORE any GUI initialization. Skip the active handshake transaction if present.
        string? handshakeTxId = (args.Length >= 2 && args[0] == "--update-handshake") ? args[1] : null;
        try
        {
            UpdateRecoveryHelper.RecoverIncompleteTransactionsAsync(msg => Trace.WriteLine(msg), handshakeTxId)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Update recovery suppressed: {ex.Message}");
        }

        // Automatically ensure 'deltempo' is globally accessible in terminal & Win+R
        CliRegistrationService.EnsureCliRegistered();

        // ═══════════════════════════════════════════════════════════════════
        // MODE 3: HEALTH HANDSHAKE — signal health after update
        // ═══════════════════════════════════════════════════════════════════
        if (handshakeTxId != null)
        {
            HandleUpdateHandshake(handshakeTxId);
        }

        // Transaction-aware cleanup (preserves active update journals)
        UpdateService.CleanupPendingUpdateArtifacts();

        // ═══════════════════════════════════════════════════════════════════
        // MODE 4: CLI — any other arguments go to CLI runner
        // ═══════════════════════════════════════════════════════════════════
        if (args.Length > 0 && args[0] != "--update-handshake")
        {
            bool isAttached = SetupConsoleStream();
            int exitCode = CliRunner.RunAsync(args).GetAwaiter().GetResult();
            try { Console.Out.Flush(); } catch { }
            if (isAttached)
            {
                ReleaseConsoleAndSignalPrompt();
            }
            Environment.Exit(exitCode);
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // MODE 5: GUI — normal WPF application
        // ═══════════════════════════════════════════════════════════════════
        if (!SingleInstanceManager.TryAcquire())
        {
            SingleInstanceManager.NotifyExistingInstance();
            Shutdown(0);
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// Embedded updater mode. Runs the update transaction without starting the WPF UI.
    /// This allows Deltempo to update itself while shipping as a single executable.
    /// </summary>
    private static int RunEmbeddedUpdater(string transactionId)
    {
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Deltempo", "Updates", transactionId);
        Directory.CreateDirectory(logDir);
        string logFile = Path.Combine(logDir, "updater.log");

        void Log(string msg)
        {
            string line = $"[{DateTime.UtcNow:O}] {msg}";
            Console.WriteLine(line);
            Trace.WriteLine(line);
            try { File.AppendAllText(logFile, line + Environment.NewLine); } catch { }
        }

        Log("Deltempo Embedded Updater starting...");

        var journal = TransactionJournal.Load(transactionId);
        if (journal == null)
        {
            Log($"Error: Transaction journal not found for '{transactionId}'.");
            return 1;
        }

        Log($"Executing transaction {transactionId} (state: {journal.State}, target: {journal.TargetPath})");

        var coordinator = new UpdateTransactionCoordinator(journal, Log);
        bool success = coordinator.ExecuteAsync().GetAwaiter().GetResult();

        Log(success ? "Transaction completed successfully." : $"Transaction failed: {journal.ErrorMessage}");
        return success ? 0 : 1;
    }

    private static bool SetupConsoleStream()
    {
        try
        {
            // If already redirected (e.g. piped via '| Out-Host' or file redirection), don't attach console
            if (Console.IsOutputRedirected)
            {
                Console.OutputEncoding = Encoding.UTF8;
                return false;
            }

            bool attached = AttachConsole(ATTACH_PARENT_PROCESS);
            if (attached)
            {
                IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, ownsHandle: false);
                    var fs = new FileStream(safeHandle, FileAccess.Write);
                    var writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
                    Console.SetOut(writer);
                    Console.SetError(writer);
                    Console.OutputEncoding = Encoding.UTF8;
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// When attached to an interactive parent console, synthesizes an Enter keystroke
    /// into the console input buffer so that cmd.exe / PowerShell prints a fresh prompt
    /// on a brand-new line instead of leaving the cursor stranded on the output text.
    /// </summary>
    private static void ReleaseConsoleAndSignalPrompt()
    {
        try
        {
            Console.Out.Flush();
            Console.Error.Flush();

            IntPtr stdIn = GetStdHandle(STD_INPUT_HANDLE);
            if (stdIn != IntPtr.Zero && stdIn != new IntPtr(-1))
            {
                var records = new INPUT_RECORD[2];

                // Key Down: Enter
                records[0] = new INPUT_RECORD
                {
                    EventType = KEY_EVENT,
                    Event = new INPUT_RECORD_UNION
                    {
                        KeyEvent = new KEY_EVENT_RECORD
                        {
                            bKeyDown = true,
                            wRepeatCount = 1,
                            wVirtualKeyCode = VK_RETURN,
                            wVirtualScanCode = 0x1C,
                            UnicodeChar = '\r',
                            dwControlKeyState = 0
                        }
                    }
                };

                // Key Up: Enter
                records[1] = new INPUT_RECORD
                {
                    EventType = KEY_EVENT,
                    Event = new INPUT_RECORD_UNION
                    {
                        KeyEvent = new KEY_EVENT_RECORD
                        {
                            bKeyDown = false,
                            wRepeatCount = 1,
                            wVirtualKeyCode = VK_RETURN,
                            wVirtualScanCode = 0x1C,
                            UnicodeChar = '\r',
                            dwControlKeyState = 0
                        }
                    }
                };

                WriteConsoleInput(stdIn, records, (uint)records.Length, out _);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] ReleaseConsoleAndSignalPrompt exception: {ex.Message}");
        }
        finally
        {
            try { FreeConsole(); } catch { }
        }
    }

    /// <summary>
    /// Handles the --update-handshake argument from the embedded updater.
    /// Signals health to the updater via EventWaitHandle and health.signal file.
    /// </summary>
    private void HandleUpdateHandshake(string txId)
    {
        try
        {
            string updatesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Deltempo", "Updates", txId);

            string healthSignalPath = Path.Combine(updatesDir, "health.signal");

            // Wait for essential initialization (MainWindow loaded, tray icon ready)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    // Write health signal file
                    File.WriteAllText(healthSignalPath, $"OK:{DateTime.UtcNow:O}");

                    // Signal the EventWaitHandle
                    string eventName = $"Local\\Deltempo_Health_{txId}";
                    using var evt = System.Threading.EventWaitHandle.OpenExisting(eventName);
                    evt.Set();

                    Trace.WriteLine($"[Deltempo] Update health handshake signaled for transaction {txId}.");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Deltempo] Health handshake signal failed: {ex.Message}");
                }
            }));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Update handshake setup failed: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            TrayService.Dispose();
            SingleInstanceManager.Release();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }

        base.OnExit(e);
    }
}
