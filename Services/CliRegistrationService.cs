using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WinTempCleaner.Services;

/// <summary>
/// Installs and removes the shell integration that makes the <c>deltempo</c> command work in
/// cmd.exe, PowerShell, and Win+R.
/// </summary>
/// <remarks>
/// Every artifact this class creates points at a console-subsystem (CUI) binary, never at the
/// GUI executable. See <see cref="CliProvisioningService"/> for why the GUI binary cannot serve
/// as an in-terminal command target.
/// </remarks>
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

    /// <summary>
    /// The console binary every artifact is wired to, or <c>null</c> when it has not been
    /// provisioned yet. Call <see cref="CliProvisioningService.EnsureCliBinaryAsync"/> first.
    /// </summary>
    private static string? ResolveCliBinary() =>
        CliProvisioningService.IsUsableCliBinary(CliProvisioningService.StagedCliPath)
            ? CliProvisioningService.StagedCliPath
            : CliProvisioningService.FindLocalCliBinary();

    /// <summary>
    /// Registers shell integration only when a console binary is already present. Safe to call on
    /// every launch; it is a no-op when there is nothing to wire up and never touches the machine
    /// otherwise.
    /// </summary>
    public static void EnsureCliRegistered()
    {
        string? cliPath = ResolveCliBinary();
        if (cliPath == null) return;

        Apply(cliPath);
    }

    /// <summary>
    /// Provisions the console binary if needed, then registers shell integration against it.
    /// This is the entry point that makes <c>deltempo</c> usable after a single app launch.
    /// </summary>
    public static async Task<bool> EnsureCliRegisteredAsync(CancellationToken ct = default)
    {
        var provisioning = await CliProvisioningService.EnsureCliBinaryAsync(ct).ConfigureAwait(false);
        if (!provisioning.Success || !CliProvisioningService.IsUsableCliBinary(provisioning.BinaryPath))
        {
            Trace.WriteLine($"[Deltempo] CLI provisioning failed: {provisioning.Message}");
            return false;
        }

        Apply(provisioning.BinaryPath);
        return true;
    }

    private static void Apply(string cliPath)
    {
        try
        {
            Directory.CreateDirectory(CliProvisioningService.ToolDirectory);

            // 1. Console shims next to the binary, resolved through PATH in cmd.exe.
            EnsureCliWrapperFiles(cliPath);

            // 2. PowerShell function, which also repairs a stale path from an older install.
            RegisterPowerShellProfile(cliPath);

            // 3. Win+R / Windows Shell aliases.
            RegisterAppPaths("deltempo.exe", cliPath);
            RegisterAppPaths("deltempo", cliPath);

            // 4. Tool directory on the user PATH.
            RegisterToUserPath(CliProvisioningService.ToolDirectory);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] CLI registration suppressed: {ex.Message}");
        }
    }

    // ─── OPT-IN REGISTRATION COMMAND (`deltempo register`) ─────────────

    /// <summary>
    /// Handles the explicit `deltempo register` / `deltempo unregister` commands.
    /// Shell integration (user PATH, App Paths registry keys, PowerShell profile function,
    /// console wrapper scripts) is applied ONLY through this command or by the GUI on launch;
    /// no other CLI invocation mutates the host machine.
    /// </summary>
    /// <param name="args">
    /// Supported forms:
    /// `register` — install shell integration;
    /// `register --status` — report current state without mutating anything;
    /// `register --remove` / `unregister` — remove every integration artifact.
    /// </param>
    /// <returns>Process exit code (0 on success).</returns>
    public static async Task<int> HandleRegisterCommandAsync(string[] args, CancellationToken ct = default)
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
            PrintStatusRow("Console CLI binary provisioned", s.IsCliBinaryAvailable);
            PrintStatusRow("User PATH contains tool directory", s.IsRegisteredInUserPath);
            PrintStatusRow("Win+R alias (App Paths registry)", s.IsRegisteredInAppPaths);
            PrintStatusRow("PowerShell profile function", s.IsRegisteredInPowerShellProfile);
            PrintStatusRow("Console wrapper scripts (.cmd / .ps1)", s.HasWrapperScripts);
            Console.WriteLine();
            Console.WriteLine($"  Target: {s.CliBinaryPath ?? "(none — run `deltempo register` to provision)"}");
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
        Console.WriteLine("     • Ensures the console CLI binary is present (verified download if needed)");
        Console.WriteLine("     • Adds the tool directory to the user PATH");
        Console.WriteLine("     • Registers Win+R aliases (App Paths registry)");
        Console.WriteLine("     • Adds a synchronous `deltempo` function to PowerShell profiles");
        Console.WriteLine("     • Creates console wrapper scripts next to the binary");
        Console.WriteLine();

        bool installed = await EnsureCliRegisteredAsync(ct).ConfigureAwait(false);

        var after = GetRegistrationStatus();
        if (installed && after.IsFullyRegistered)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Registration complete. `deltempo` is available in new terminals and Win+R.");
            Console.ResetColor();
            Console.WriteLine("    Open a NEW terminal window for the command to be picked up.");
            Console.WriteLine();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ⚠ Registration finished with partial coverage. Run `deltempo register --status` for details.");
        Console.ResetColor();
        Console.WriteLine();
        return 1;
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
        bool IsCliBinaryAvailable,
        bool IsRegisteredInUserPath,
        bool IsRegisteredInAppPaths,
        bool IsRegisteredInPowerShellProfile,
        bool HasWrapperScripts,
        string? CliBinaryPath)
    {
        /// <summary>The console binary exists and all four integration artifacts are present.</summary>
        public bool IsFullyRegistered =>
            IsCliBinaryAvailable && IsRegisteredInUserPath && IsRegisteredInAppPaths &&
            IsRegisteredInPowerShellProfile && HasWrapperScripts;
    }

    /// <summary>
    /// Reads the current integration state without mutating anything. Every check is anchored to
    /// the tool directory rather than the running process, so the same answer is produced whether
    /// this is called from the GUI or from the console binary.
    /// </summary>
    public static RegistrationStatus GetRegistrationStatus()
    {
        string toolDir = CliProvisioningService.ToolDirectory;
        string? cliPath = ResolveCliBinary();
        bool cliAvailable = cliPath != null;

        bool inPath = false;
        try
        {
            inPath = (Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().TrimEnd('\\', '/'))
                .Any(p => string.Equals(p, toolDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
        }
        catch { }

        bool appPaths = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\deltempo.exe");
            appPaths = key != null && !string.IsNullOrEmpty(key.GetValue("") as string);
        }
        catch { }

        // The profile counts as registered only when it routes to a binary that actually exists,
        // so a stale absolute path left behind by an older install is reported as a failure
        // rather than a false success.
        bool profile = false;
        try
        {
            foreach (string p in GetPowerShellProfilePaths())
            {
                if (!File.Exists(p)) continue;
                string text = File.ReadAllText(p);
                if (!text.Contains(CliProvisioningService.MarkerBegin, StringComparison.Ordinal)) continue;

                if (cliPath != null && CliProvisioningService.ProfileTargetsBinary(text, cliPath))
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
            wrappers = File.Exists(Path.Combine(toolDir, "deltempo.cmd")) &&
                       File.Exists(Path.Combine(toolDir, "deltempo.ps1"));
        }
        catch { }

        return new RegistrationStatus(cliAvailable, inPath, appPaths, profile, wrappers, cliPath);
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
        string toolDir = CliProvisioningService.ToolDirectory;

        // 1. Remove the tool directory from the user PATH
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
            var filtered = paths
                .Where(p => !string.Equals(p.TrimEnd('\\', '/'), toolDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                .ToList();
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

        // 3 & 4. Remove the managed PowerShell block and the console wrapper scripts
        removedAnything |= RemoveFileArtifacts(GetPowerShellProfilePaths(), toolDir);

        return removedAnything;
    }

    /// <summary>
    /// Strips the managed block from each profile and deletes the console shims.
    /// </summary>
    /// <remarks>
    /// Extracted from <see cref="UnregisterAll"/> and kept internal so removal can be exercised
    /// against a sandbox. Calling the public <see cref="UnregisterAll"/> from a test would
    /// uninstall the CLI on the developer's own machine.
    /// </remarks>
    internal static bool RemoveFileArtifacts(IEnumerable<string> profilePaths, string toolDirectory)
    {
        bool removedAnything = false;

        try
        {
            foreach (string p in profilePaths)
            {
                if (!File.Exists(p)) continue;
                if (CliProvisioningService.RemoveManagedBlock(File.ReadAllText(p), out string updated))
                {
                    File.WriteAllText(p, updated);
                    removedAnything = true;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Profile cleanup suppressed: {ex.Message}");
        }

        try
        {
            foreach (string wrapper in new[] { "deltempo.cmd", "deltempo.ps1" })
            {
                string p = Path.Combine(toolDirectory, wrapper);
                if (File.Exists(p))
                {
                    File.Delete(p);
                    removedAnything = true;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] Wrapper cleanup suppressed: {ex.Message}");
        }

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

    /// <summary>
    /// Writes the cmd/ps1 shims into the tool directory, which is the directory placed on PATH.
    /// The shims always point at the resolved binary, so the command resolves through PATH even
    /// when the binary itself lives elsewhere (a framework-dependent build in its output folder).
    /// </summary>
    private static void EnsureCliWrapperFiles(string cliPath)
    {
        try
        {
            WriteIfChanged(
                Path.Combine(CliProvisioningService.ToolDirectory, "deltempo.cmd"),
                CliProvisioningService.BuildCmdContent(cliPath));
            WriteIfChanged(
                Path.Combine(CliProvisioningService.ToolDirectory, "deltempo.ps1"),
                CliProvisioningService.BuildPs1Content(cliPath));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] CLI wrapper write suppressed: {ex.Message}");
        }
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal)) return;
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Writes the managed <c>deltempo</c> function into both PowerShell profiles.
    /// </summary>
    /// <remarks>
    /// The block is rewritten through a single shared regex, so a function left behind by an older
    /// install (or pointing at a binary that has since moved) is repaired instead of surviving
    /// forever and shadowing the freshly installed command. User-authored content is preserved.
    /// </remarks>
    private static void RegisterPowerShellProfile(string cliPath)
    {
        foreach (string p in GetPowerShellProfilePaths())
        {
            try
            {
                string? dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string existing = File.Exists(p) ? File.ReadAllText(p) : string.Empty;
                string updated = CliProvisioningService.UpsertManagedBlock(existing, cliPath);
                if (!string.Equals(updated, existing, StringComparison.Ordinal))
                {
                    File.WriteAllText(p, updated);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Deltempo] PowerShell profile registration suppressed for {p}: {ex.Message}");
            }
        }
    }

    private static void RegisterAppPaths(string appName, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appName}");
            if (key != null)
            {
                key.SetValue("", exePath);
                key.SetValue("Path", Path.GetDirectoryName(exePath) ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Deltempo] App Paths registration suppressed: {ex.Message}");
        }
    }

    private static void RegisterToUserPath(string directory)
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
            string normalized = directory.TrimEnd('\\', '/');

            if (!paths.Any(p => string.Equals(p.TrimEnd('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(directory);
                Environment.SetEnvironmentVariable("Path", string.Join(";", paths), EnvironmentVariableTarget.User);

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
            Trace.WriteLine($"[Deltempo] User PATH registration suppressed: {ex.Message}");
        }
    }
}
