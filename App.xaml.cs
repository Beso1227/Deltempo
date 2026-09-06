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

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

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
        // BEFORE any GUI initialization.
        try
        {
            UpdateRecoveryHelper.RecoverIncompleteTransactionsAsync(msg => Trace.WriteLine(msg))
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
        if (args.Length >= 2 && args[0] == "--update-handshake")
        {
            HandleUpdateHandshake(args[1]);
        }

        // Transaction-aware cleanup (preserves active update journals)
        UpdateService.CleanupPendingUpdateArtifacts();

        // ═══════════════════════════════════════════════════════════════════
        // MODE 4: CLI — any other arguments go to CLI runner
        // ═══════════════════════════════════════════════════════════════════
        if (args.Length > 0 && args[0] != "--update-handshake")
        {
            SetupConsoleStream();
            int exitCode = CliRunner.RunAsync(args).GetAwaiter().GetResult();
            try { Console.Out.Flush(); } catch { }
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
        Console.WriteLine("Deltempo Embedded Updater v1.0.0");

        var journal = TransactionJournal.Load(transactionId);
        if (journal == null)
        {
            Console.Error.WriteLine($"Error: Transaction journal not found for '{transactionId}'.");
            return 1;
        }

        if (journal.CallerPid <= 0)
        {
            Console.Error.WriteLine("Error: No caller PID recorded in transaction journal.");
            return 1;
        }

        Console.WriteLine($"Executing transaction {transactionId} (state: {journal.State})");

        var coordinator = new UpdateTransactionCoordinator(journal, msg =>
        {
            Console.WriteLine(msg);
            Trace.WriteLine(msg);
        });

        bool success = coordinator.ExecuteAsync().GetAwaiter().GetResult();

        Console.WriteLine(success ? "Transaction completed successfully." : "Transaction failed.");
        return success ? 0 : 1;
    }

    private static void SetupConsoleStream()
    {
        try
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

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
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
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
