using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Services;

public static class CliRegistrationService
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public static void EnsureCliRegistered()
    {
        try
        {
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
                return;

            var exeDir = Path.GetDirectoryName(currentExePath);
            if (string.IsNullOrEmpty(exeDir))
                return;

            // 1. Create native console wrappers in the app directory for synchronous terminal execution
            EnsureCliWrapperFiles(exeDir, currentExePath);

            // 2. Register PowerShell profile function for 100% synchronous execution in all PowerShell sessions
            RegisterPowerShellProfile(exeDir, currentExePath);

            // 3. Register in Windows App Paths (Enables Win+R "deltempo" & Windows Shell execution)
            RegisterAppPaths("deltempo.exe", currentExePath, exeDir);
            RegisterAppPaths("deltempo", currentExePath, exeDir);

            // 4. Ensure current folder is in User PATH environment variable
            RegisterToUserPath(exeDir);
        }
        catch
        {
            // Non-critical background registration failure
        }
    }

    // ─── OPT-IN REGISTRATION COMMAND (`deltempo register`) ─────────────

    /// <summary>
    /// Handles the explicit `deltempo register` / `deltempo unregister` commands.
    /// Shell integration (user PATH, App Paths registry keys, PowerShell profile function,
    /// console wrapper scripts) is applied ONLY through this command; no other CLI
    /// invocation mutates the host machine.
    /// </summary>
    /// <param name="args">
    /// Supported forms:
    /// `register` — install shell integration;
    /// `register --status` — report current state without mutating anything;
    /// `register --remove` / `unregister` — remove every integration artifact.
    /// </param>
    /// <returns>Process exit code (0 on success).</returns>
    public static int HandleRegisterCommand(string[] args)
    {
        bool remove = HasAnyFlag(args, "--remove", "--unregister", "-r") ||
                      (args.Length > 0 && args[0].Equals("unregister", StringComparison.OrdinalIgnoreCase));
        bool statusOnly = HasAnyFlag(args, "--status");

        Console.WriteLine();

        if (statusOnly)
        {
            var s = GetRegistrationStatus();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  [Deltempo] Shell Integration Status");
            Console.ResetColor();
            PrintStatusRow("User PATH contains install directory", s.IsRegisteredInUserPath);
            PrintStatusRow("Win+R alias (App Paths registry)", s.IsRegisteredInAppPaths);
            PrintStatusRow("PowerShell profile function", s.IsRegisteredInPowerShellProfile);
            PrintStatusRow("Console wrapper scripts (.cmd / .ps1)", s.HasWrapperScripts);
            Console.WriteLine();
            Console.WriteLine(s.IsFullyRegistered
                ? "  State: REGISTERED. Run `deltempo unregister` to remove."
                : "  State: NOT REGISTERED. Run `deltempo register` to install.");
            Console.WriteLine();
            return 0;
        }

        if (remove)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [Deltempo] Removing shell integration (PATH, App Paths, PowerShell profile, wrappers)...");
            Console.ResetColor();
            bool removed = UnregisterAll();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(removed
                ? "  ✓ Shell integration removed. Invoke `deltempo` by full path if still needed."
                : "  ✓ Shell integration was not present (nothing to remove).");
            Console.ResetColor();
            Console.WriteLine();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  [Deltempo] Installing shell integration (explicit opt-in)...");
        Console.ResetColor();
        Console.WriteLine("     • Adds the install directory to the user PATH");
        Console.WriteLine("     • Registers Win+R aliases (App Paths registry)");
        Console.WriteLine("     • Adds a synchronous `deltempo` function to PowerShell profiles");
        Console.WriteLine("     • Creates console wrapper scripts next to the executable");
        Console.WriteLine();

        EnsureCliRegistered();

        var after = GetRegistrationStatus();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(after.IsFullyRegistered
            ? "  ✓ Registration complete. `deltempo` is available in new terminals and Win+R."
            : "  ⚠ Registration finished with partial coverage. Run `deltempo register --status` for details.");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static void PrintStatusRow(string label, bool value)
    {
        Console.Write("  ");
        Console.ForegroundColor = value ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.Write(value ? "✓" : "·");
        Console.ResetColor();
        Console.WriteLine($" {label,-42} {(value ? "YES" : "no")}");
    }

    /// <summary>
    /// Immutable snapshot of the current shell-integration state on this machine.
    /// </summary>
    public sealed record RegistrationStatus(
        bool IsRegisteredInUserPath,
        bool IsRegisteredInAppPaths,
        bool IsRegisteredInPowerShellProfile,
        bool HasWrapperScripts)
    {
        /// <summary>All four integration artifacts are present.</summary>
        public bool IsFullyRegistered =>
            IsRegisteredInUserPath && IsRegisteredInAppPaths &&
            IsRegisteredInPowerShellProfile && HasWrapperScripts;
    }

    /// <summary>
    /// Reads the current integration state without mutating anything.
    /// </summary>
    public static RegistrationStatus GetRegistrationStatus()
    {
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        string exeDir = string.IsNullOrEmpty(exePath) ? string.Empty : Path.GetDirectoryName(exePath) ?? string.Empty;

        bool inPath = false;
        try
        {
            if (!string.IsNullOrEmpty(exeDir))
            {
                inPath = (Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Any(p => string.Equals(p, exeDir, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch { }

        bool appPaths = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\deltempo.exe");
            appPaths = key != null && !string.IsNullOrEmpty(key.GetValue("") as string);
        }
        catch { }

        bool profile = false;
        try
        {
            const string marker = "# Deltempo Synchronous CLI";
            foreach (string p in GetPowerShellProfilePaths())
            {
                if (File.Exists(p) && File.ReadAllText(p).Contains(marker))
                {
                    profile = true;
                    break;
                }
            }
        }
        catch { }

        bool wrappers = false;
        try
        {
            if (!string.IsNullOrEmpty(exeDir))
            {
                wrappers = File.Exists(Path.Combine(exeDir, "deltempo.cmd")) &&
                           File.Exists(Path.Combine(exeDir, "deltempo.ps1"));
            }
        }
        catch { }

        return new RegistrationStatus(inPath, appPaths, profile, wrappers);
    }

    /// <summary>
    /// Removes every shell-integration artifact Deltempo may have created:
    /// the user PATH entry, App Paths registry keys, the PowerShell profile function,
    /// and the console wrapper scripts. Safe to call when nothing was registered.
    /// </summary>
    /// <returns><c>true</c> if at least one artifact was removed.</returns>
    public static bool UnregisterAll()
    {
        bool removedAnything = false;

        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        string exeDir = string.IsNullOrEmpty(exePath) ? string.Empty : Path.GetDirectoryName(exePath) ?? string.Empty;

        // 1. Remove the install directory from the user PATH
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
            var filtered = paths.Where(p => !string.Equals(p, exeDir, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count != paths.Count)
            {
                Environment.SetEnvironmentVariable("Path", string.Join(";", filtered), EnvironmentVariableTarget.User);
                removedAnything = true;
            }
        }
        catch { }

        // 2. Delete App Paths registry keys
        try
        {
            const string appPathsKey = @"Software\Microsoft\Windows\CurrentVersion\App Paths\";
            foreach (string appName in new[] { "deltempo.exe", "deltempo" })
            {
                using var key = Registry.CurrentUser.OpenSubKey(appPathsKey + appName, writable: true);
                if (key != null)
                {
                    key.Close();
                    Registry.CurrentUser.DeleteSubKeyTree(appPathsKey + appName, throwOnMissingSubKey: false);
                    removedAnything = true;
                }
            }
        }
        catch { }

        // 3. Remove the PowerShell profile function snippet
        try
        {
            var regex = new System.Text.RegularExpressions.Regex(
                @"(?:\r?\n)?# Deltempo Synchronous CLI\r?\nfunction deltempo\s*\{[^}]*\}\r?\n?",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (string p in GetPowerShellProfilePaths())
            {
                if (!File.Exists(p)) continue;
                string existing = File.ReadAllText(p);
                string updated = regex.Replace(existing, string.Empty);
                if (updated != existing)
                {
                    File.WriteAllText(p, updated);
                    removedAnything = true;
                }
            }
        }
        catch { }

        // 4. Delete console wrapper scripts
        try
        {
            if (!string.IsNullOrEmpty(exeDir))
            {
                foreach (string wrapper in new[] { "deltempo.cmd", "deltempo.ps1" })
                {
                    string p = Path.Combine(exeDir, wrapper);
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                        removedAnything = true;
                    }
                }
            }
        }
        catch { }

        return removedAnything;
    }

    private static string[] GetPowerShellProfilePaths()
    {
        string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return new[]
        {
            Path.Combine(userDocs, "PowerShell", "Microsoft.PowerShell_profile.ps1"),
            Path.Combine(userDocs, "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1")
        };
    }

    private static bool HasAnyFlag(string[] args, params string[] flags) =>
        args.Any(a => flags.Any(f => a.Equals(f, StringComparison.OrdinalIgnoreCase)));

    private static void EnsureCliWrapperFiles(string exeDir, string exePath)
    {
        try
        {
            string cliTarget = Path.Combine(exeDir, "deltempo_cli.exe");
            bool hasCliBinary = File.Exists(cliTarget);
            string targetBinary = hasCliBinary ? "deltempo_cli.exe" : Path.GetFileName(exePath);

            string cmdFile = Path.Combine(exeDir, "deltempo.cmd");
            string cmdContent = hasCliBinary
                ? $"@echo off\r\n\"%~dp0deltempo_cli.exe\" %*\r\n"
                : $"@echo off\r\nstart /b /wait \"\" \"%~dp0{targetBinary}\" %*\r\n";

            if (!File.Exists(cmdFile) || File.ReadAllText(cmdFile) != cmdContent)
            {
                File.WriteAllText(cmdFile, cmdContent);
            }

            string ps1File = Path.Combine(exeDir, "deltempo.ps1");
            string ps1Content = hasCliBinary
                ? $"& \"$PSScriptRoot\\deltempo_cli.exe\" @args\r\n"
                : $"& \"$PSScriptRoot\\{targetBinary}\" @args | Out-Host\r\n";

            if (!File.Exists(ps1File) || File.ReadAllText(ps1File) != ps1Content)
            {
                File.WriteAllText(ps1File, ps1Content);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static bool IsValidCliBinary(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var fi = new FileInfo(path);
            if (fi.Length > 20 * 1024 * 1024) return true; // Single-file self-contained publish
            string dll = Path.ChangeExtension(path, ".dll");
            return File.Exists(dll); // Framework-dependent build with sibling dll
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterPowerShellProfile(string exeDir, string currentExePath)
    {
        try
        {
            string[] profilePaths = GetPowerShellProfilePaths();

            string cliExe = Path.Combine(exeDir, "deltempo_cli.exe");
            if (!IsValidCliBinary(cliExe))
            {
                // In development environments, check sibling CLI output directories
                string[] searchCandidates = new[]
                {
                    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "Cli", "bin", "Debug", "net10.0-windows", "win-x64", "deltempo_cli.exe")),
                    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "Cli", "bin", "Release", "net10.0-windows", "win-x64", "deltempo_cli.exe")),
                    Path.GetFullPath(Path.Combine(exeDir, "..", "publish_cli", "deltempo_cli.exe")),
                    Path.GetFullPath(Path.Combine(exeDir, "..", "..", "publish_cli", "deltempo_cli.exe"))
                };

                foreach (var candidate in searchCandidates)
                {
                    if (IsValidCliBinary(candidate))
                    {
                        cliExe = candidate;
                        break;
                    }
                }
            }

            bool isNativeCli = IsValidCliBinary(cliExe);
            string targetBinary = isNativeCli ? cliExe : currentExePath;

            // When executing a GUI binary in console, pipe through Out-Host to enforce synchronous completion and a clean new line
            string execCommand = isNativeCli
                ? $"& \"{targetBinary}\" @args"
                : $"& \"{targetBinary}\" @args | Out-Host";

            string snippet = $"\r\n# Deltempo Synchronous CLI\r\nfunction deltempo {{ {execCommand} }}\r\n";

            foreach (var p in profilePaths)
            {
                var dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (File.Exists(p))
                {
                    string existing = File.ReadAllText(p);
                    if (existing.Contains("function deltempo"))
                    {
                        var regex = new System.Text.RegularExpressions.Regex(@"# Deltempo Synchronous CLI\r?\nfunction deltempo\s*\{[^}]*\}", System.Text.RegularExpressions.RegexOptions.Multiline);
                        if (regex.IsMatch(existing))
                        {
                            string updated = regex.Replace(existing, $"# Deltempo Synchronous CLI\r\nfunction deltempo {{ {execCommand} }}");
                            if (updated != existing)
                            {
                                File.WriteAllText(p, updated);
                            }
                        }
                    }
                    else
                    {
                        File.AppendAllText(p, snippet);
                    }
                }
                else
                {
                    File.WriteAllText(p, snippet);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void RegisterAppPaths(string appName, string exePath, string exeDir)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@$"Software\Microsoft\Windows\CurrentVersion\App Paths\{appName}");
            if (key != null)
            {
                key.SetValue("", exePath);
                key.SetValue("Path", exeDir);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }

    private static void RegisterToUserPath(string exeDir)
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();

            if (!paths.Any(p => string.Equals(p, exeDir, StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(exeDir);
                var newPath = string.Join(";", paths);
                Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);

                // Broadcast change to Windows shell and running terminals
                SendMessageTimeout(
                    (IntPtr)HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    1000,
                    out _);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
        }
    }
}
