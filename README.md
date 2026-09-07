<div align="center">

  <img src="docs/app_icon.png" alt="Deltempo Logo" width="88" height="88" />

  # Deltempo

  <p><strong>Open-source, privacy-first Windows cleaner and memory optimizer.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><img src="https://img.shields.io/github/v/release/Beso1227/Deltempo?label=Release&color=06B6D4" alt="Latest Release" /></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&label=CI%20Build" alt="CI Build Status" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/github/license/Beso1227/Deltempo?color=10B981" alt="License: MIT" /></a>
    <a href="#supported-platforms"><img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D4" alt="Platform Support" /></a>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest">Download Release</a> •
    <a href="https://beso1227.github.io/Deltempo/">Official Website</a> •
    <a href="https://beso1227.github.io/Deltempo/docs/">Documentation</a> •
    <a href="SECURITY.md">Security Policy</a> •
    <a href="#quick-start">Quick Start</a>
  </p>

</div>

---

## At a Glance

| Property | Detail |
| :--- | :--- |
| **Platform** | Windows 10 & 11 (64-bit / x64) |
| **License** | Open Source ([MIT](LICENSE)) |
| **Interfaces** | Modern Desktop GUI (WPF Fluent) and Headless Terminal CLI |
| **Distribution** | Portable single-file executable (self-contained, no installer required) |
| **Telemetry** | None. Routine scan, clean, and memory actions execute entirely offline |
| **Safety Engine** | Two-phase planning (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) with 5 risk tiers |
| **Memory Engine** | Native Windows NT kernel calls (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Preferences Hub** | Categorized 4-tab control center (*Updates*, *General*, *Memory*, *Storage & Safety*) |
| **Tray Guardian** | High-DPI native Win32 icon (`LoadCrispTrayIcon`) with live RAM telemetry & 1-click Boost |
| **Updates** | Cryptographically verified (SHA-256) Stable & Beta release channels with scheduled background polling |

---

## What is Deltempo?

**Deltempo** is an open-source Windows utility built to safely reclaim storage space and optimize system memory. It purges disposable application caches, orphaned installer remnants, build artifacts, and stale system logs without touching personal documents, browser credentials, or critical operating system components.

Traditional cleanup utilities often function as opaque black boxes or distribute bundled advertising. Deltempo is engineered on a **safety-first architecture**: candidate paths are classified into explicit risk tiers, simulated before deletion, bounded within authorized directory roots, and revalidated immediately before removal to guard against filesystem race conditions.

In addition to disk cleanup, Deltempo includes low-level Windows NT kernel memory management tools to flush standby page lists and trim inactive working sets through official Win32 and NT system calls.

---

## Why Deltempo?

* **Open Source & Auditable**: Permissively licensed under MIT. Every cleanup rule, safety check, and native API call is transparent C# code.
* **Safety-First Architecture**: Features a deterministic two-phase model (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) with path boundary enforcement, reparse point rejection, and protected folder shields.
* **Native Windows NT Memory Management**: Purges standby memory lists and trims process working sets via `NtSetSystemInformation` and `EmptyWorkingSet`.
* **Crisp High-DPI Tray Guardian**: Renders pixel-perfect 32-bit alpha icons across 100% to 200%+ DPI, displays real-time RAM usage in the tooltip, and recovers automatically on `explorer.exe` restarts.
* **Preferences & Update Hub**: Features a tabbed control center organizing update schedules, release channels (*Stable* vs *Beta*), low disk alerts, and memory auto-trimming.
* **Dual Interface (GUI & CLI)**: Use the desktop interface for interactive analysis or automate scheduled maintenance via the scriptable CLI with structured JSON support.
* **Zero Telemetry & Local Execution**: Routine operations run entirely offline. Contains no analytics tracking, advertisements, or third-party telemetry libraries.
* **Portable Operation**: Shipped as a self-contained single-file executable. No background daemons or separate runtime installations are required.

---

## Safety Architecture

The foundational rule of Deltempo's deletion engine is:

> **Never optimize for deleting more files. Optimize for proving that every deletion is safe.**

```text
┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
│  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
└────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
```

### Safety Pipeline Stages

1. **Scan**: Identifies candidate files in authorized scopes and extracts metadata (size, age, attributes, and path hierarchy).
2. **Plan**: Compiles a `CleanupPlan`. Dry-run simulation uses the same cleanup planning pipeline as live cleanup to preview candidate actions without executing destructive operations.
3. **Protect**: Matches paths against protection policies, shielding user directories (Documents, Pictures, Desktop), source code repositories, SSH keys, credentials, and active session tokens.
4. **Revalidate**: Mitigates TOCTOU (time-of-check to time-of-use) risk by revalidating canonical path identity, containment boundaries, reparse points, and attributes immediately before destructive operations.
5. **Clean**: Executes approved deletions while producing a structured transaction log of processed, deleted, recycled, and skipped bytes.

### 5-Tier Safety Classification

| Risk Tier | Scope & Classification | Policy & Runtime Action |
| :--- | :--- | :--- |
| **Protected** | System & User Files | Core OS binaries, personal user folders (Documents, Desktop), source repositories, SSH keys, credentials, and session stores. **Strictly immutable — deletion blocked.** |
| **Safe** | Verified Caches | Deterministically verified temporary data located strictly within an authorized scope and matching known cache patterns. **Eligible for deletion.** |
| **LowRisk** | Aged Temp Files | Temporary files exceeding conservative age thresholds (e.g., >24 hours) or inactive crash reports. **Eligible for deletion.** |
| **ReviewRequired** | Ambiguous / Executables | Files in disposable locations that contain executable headers, unknown extensions, or active locks. **Defaults to KEEP.** |
| **Unknown** | Unverified Context | Any file or directory where safety cannot be proven conclusively from verified rules. **Strictly preserved.** |

### Additional Defensive Controls
* **Reparse Point & Symlink Defense**: Rejects NTFS junctions, symbolic links, and volume mount points to prevent directory traversal outside target roots.
* **Local Drive Boundary**: Confined to local fixed physical drives; remote network shares and UNC paths are rejected.
* **Windows Recycle Bin Support**: Deletions can be routed through the Windows Shell Recycle Bin (`SHFileOperation`) for reversible file recovery.

---

## Quick Start

### Running the Desktop Application (GUI)

1. Download **`Deltempo.exe`** from the [Latest Release](https://github.com/Beso1227/Deltempo/releases/latest) page.
2. Run `Deltempo.exe` (no installation required).
3. Click **Scan** to analyze cleanable data across selected categories.
4. Review the candidate list and click **Clean** to reclaim space.

### Installing via WinGet

```powershell
winget install Beso1227.Deltempo
```

### Running via Command Line (CLI)

Running `Deltempo.exe` automatically registers user-level App Paths and a PowerShell profile helper so `deltempo` can be invoked directly from terminal sessions without modifying system directories:

```powershell
# Preview cleanable targets without deleting files
deltempo clean --dry-run

# Run safe cleanup routing disposable files to the Recycle Bin
deltempo clean --safe --recycle-bin

# Flush standby memory lists and trim inactive working sets
deltempo boost

# Inspect system status and memory distribution in JSON format
deltempo status --json
```

---

## CLI Reference

Deltempo includes a synchronous, scriptable CLI designed for terminal users and automated maintenance routines.

### Command Overview

| Command | Purpose | Key Flags & Options |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Scan targets for disposable data | `--json`, `--silent` |
| `deltempo clean [filter]` | Clean disposable caches | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Quick purge of verified safe caches | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Autonomous full cleanup (RAM, DISM, scopes) | `--dry-run`, `--json`, `--yes` |
| `deltempo boost` | Optimize system memory | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo large [path]` | Scan drives for space-consuming files | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | Inspect file risk tier and safety verdict | `--json` |
| `deltempo large clean` | Recycle disposable large files | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo startup [list]` | Inspect startup applications and boot impact | `--high`, `--json` |
| `deltempo startup disable <app>` | Reversibly disable a startup program | N/A |
| `deltempo startup enable <app>` | Restore a disabled startup program | N/A |
| `deltempo repair [subcommand]` | Windows integrity check & servicing repair | `sfc`, `dism`, `winsxs`, `chkdsk`, `update`, `network` |
| `deltempo status` | Display system telemetry and memory info | `--json` |
| `deltempo update [check]` | Check for official releases or apply update | `check`, `--dry-run` |

### Practical Examples

```powershell
# 1. Preview cleanup without modifying disk state
deltempo clean --dry-run

# 2. Target temporary files older than 24 hours with JSON output
deltempo clean temp --safe --json

# 3. Purge the Windows standby memory page list specifically
deltempo boost --standby

# 4. Find the top 20 files larger than 1 GB on drive D:
deltempo large D:\ --min 1GB --top 20

# 5. Check whether a specific file is safe to delete
deltempo large inspect "C:\Users\username\AppData\Local\Temp\installer.exe"

# 6. Reversibly disable an unnecessary startup program
deltempo startup disable "Spotify"

# 7. Check for updates on GitHub Releases
deltempo update check
```

---

## Cleaning Targets

Deltempo provides 25+ built-in cleanup scopes across system, developer, and application caches. Scopes marked with an asterisk require administrative elevation.

| Domain | Scope Targets | Examples of Cleaned Data |
| :--- | :--- | :--- |
| **Windows System** | User Temp, System Temp*, Prefetch*, Update Cache*, Upgrade Residue*, Delivery Optimization*, Component Caches*, Diagnostic Logs*, Crash Dumps*, Thumbnails | `%TEMP%`, `C:\Windows\Temp`, `Prefetch`, `SoftwareDistribution\Download`, `$WINDOWS.~BT`, `DeliveryOptimization\Cache`, `WinSxS\Temp`, `Minidump`, `thumbcache_*.db` |
| **Drivers & GPU** | Driver Packages*, GPU Shader Pools | NVIDIA App/GeForce OTA packages, AMD/Intel temp installers, DirectX (`D3DSCache`), Vulkan (`GLCache`) |
| **Gaming & Media** | Game Launchers, Creator Render Caches | Steam downloads & shader pools, Epic Games webcache, Adobe Media Cache, DaVinci proxies, Blender temp |
| **Apps & Social** | Desktop Apps, Messaging Apps, Windows Store Apps, Orphaned AppData | Discord, Spotify, Slack, VS Code caches; WhatsApp/Telegram caches (sessions preserved); uninstalled app folders |
| **Developer Tools** | Package Caches, Dev Daemons | NuGet v3, npm, pip, yarn, pnpm, Cargo, Go build, .gradle, and Android Studio emulator temp caches |
| **Web Browsers** | Chromium Profiles, Gecko Profiles, Temporary Internet | Multi-profile web and shader caches for Chrome, Edge, Brave, Opera, Vivaldi, Firefox, Arc (logins preserved) |
| **Recovery** | Windows Recycle Bin, VSS Restore Points* | Empties `$Recycle.Bin` across mounted drives; purges older VSS shadow copies while retaining the latest |

> [!NOTE]
> Personal documents, browser cookies, saved passwords, active authentication sessions, and source code repositories are strictly excluded from cleanup.

---

## Memory Optimization

Windows caches recently accessed files in the **Standby Page List**. While this optimizes file access, fragmented standby caches or unreleased working sets from closed apps can cause memory pressure during resource-intensive workloads.

Deltempo interfaces directly with native Windows memory management routines:

* **Standby List Invalidation**: Invokes `NtSetSystemInformation` with `SystemMemoryListInformation` (class `80`) to flush standby pages back to the free memory pool.
* **Process Working Set Trimming**: Requests `SeProfileSingleProcessPrivilege` and `SeDebugPrivilege` to call `EmptyWorkingSet` across inactive processes.
* **System File Cache Boundary Reset**: Calls `SetSystemFileCacheSize` to reset filesystem cache boundaries when cache growth becomes excessive.
* **Modified Page Flush**: Commits dirty memory pages to disk before releasing physical memory.
* **Protected Process Shielding**: Automatically excludes core system processes (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe`, and Windows Defender) from trimming operations.

---

## Large File Hunter

The **Large File Hunter** audits storage drives for space-consuming items with safety heuristics:

* **Configurable Thresholds**: Identifies files exceeding a size threshold (50 MB default, configurable via `--min`).
* **Automated Safety Classification**: Evaluates candidate files via `FileSafetyEngine`, labeling items as `Safe`, `Protected`, `LowRisk`, or `ReviewRequired`.
* **Asset Recognition**: Distinguishes disposable clutter (driver installers, setup extracts, crash dumps) from valuable files (virtual disks, databases, game archives, model weights).
* **Recycle Bin Routing**: Deletions are sent to the Windows Shell Recycle Bin by default, supporting standard Windows undo restoration.

---

## Startup Manager

The **Startup Manager** inspects programs configured to launch on Windows logon:

* **Inspected Locations**:
  * `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  * `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`
  * Startup folder shortcuts (`shell:startup`)
* **Boot Impact Scoring**: Estimates startup delay using binary metadata, publisher identity, and known launcher profiles.
* **100% Reversible Changes**: Disabled items are moved to a backup registry key (`Run_Deltempo_Disabled`) rather than deleted, allowing instant toggle restoration.

---

## Preferences & Update Hub

Deltempo features a categorized, segmented preferences center organized into four dedicated operational tabs:

* **Updates Tab**:
  * **Release Channels**: Switch between **Stable** (verified milestone releases recommended for all users) and **Beta / Pre-Release** (early access to cutting-edge features and experimental scopes).
  * **Automated Schedule**: Configure background update polling frequency: `Daily`, `Every 3 Days`, `Weekly`, or `Manual Only`.
  * **Silent Pre-Fetching**: Optionally stage verified update packages in the background so updates apply instantly upon confirmation.
  * **Version Telemetry Card**: Real-time status badge showing current installed version (`v1.3.6`), last checked timestamp, update check button, and 1-click link to official GitHub Release Notes.
* **General Tab**:
  * **System Startup**: Reversibly configure Deltempo to launch on Windows logon with optional auto-minimize to tray.
  * **Recycle Bin Routing**: Global toggle to route candidate files to the Windows Recycle Bin for safety and reversible recovery.
* **Memory Tab**:
  * **Background Auto-Boost**: Automatically purge standby lists and trim inactive working sets when system memory usage crosses user-defined pressure thresholds (e.g., 85%).
* **Storage & Safety Tab**:
  * **Low Disk Alerts**: Custom threshold alerts (`5 GB`, `10 GB`, `15 GB`, `20 GB`, `50 GB`) to warn operators before drive capacity exhaustion induces system instability or paging crashes.
  * **Safety Shields**: Enforce immutable directory locks on personal folders, repositories, and credentials.

---

## High-DPI System Tray Guardian

For background monitoring and fast access, Deltempo integrates a lightweight system tray daemon:

* **Native Win32 Scaling (`LoadCrispTrayIcon`)**: Employs direct Win32 GDI icon creation (`CreateIconIndirect`) with 32-bit ARGB alpha transparency, delivering pixel-perfect crispness on standard (100%), medium (125%, 150%), and high-density (175%, 200%+) Windows displays without blurring.
* **Real-Time RAM Telemetry**: Hovering over the tray icon displays live physical memory consumption directly in the tooltip:
  ```text
  Deltempo v1.3.6
  RAM: 42% (13.4 GB / 31.9 GB)
  ```
* **Instant Context Actions**: Right-click the tray icon to trigger **1-Click Boost Memory** or **Quick Smart Clean** immediately without bringing the main application window into focus.
* **Explorer Restart Resilience**: Automatically registers for the Win32 `TaskbarCreated` broadcast message, ensuring the tray icon seamlessly re-attaches if `explorer.exe` restarts or crashes.

---

## Automatic Updates

Deltempo auto-updates exclusively when a verified release is published to GitHub Releases:

* **Official GitHub Releases**: Queries the GitHub Releases API to discover verified builds matching the configured channel (*Stable* or *Beta*).
* **Cryptographic Hash Validation**: Every downloaded update is verified against expected SHA-256 digests by `PatchIntegrityVerifier` before any staging operation.
* **Host & Protocol Security**: `UpdateSecurityValidator` enforces strict HTTPS protocol and verified GitHub domain origin controls (`api.github.com`, `github.com`).
* **Atomic Swap & Rollback**: Staged updates are installed using cross-process mutex locks (`PatchInstallationLock`). If the new binary fails verification, the previous build is restored.

---

## Privacy

* **Does Deltempo collect telemetry?** No. Deltempo contains no telemetry, user tracking, or analytics libraries.
* **Does cleanup data leave your machine?** No. All scanning, classification, and deletion routines execute entirely on local drives.
* **What network communication occurs?** Network requests are strictly limited to HTTPS queries to GitHub (`api.github.com`, `github.com`) to check for application releases and download verified executables.
* **When does Deltempo contact GitHub?** Only when update checks are performed (via `deltempo update` or GUI settings).

---

## Limitations

* **Windows Only**: Designed exclusively for 64-bit Windows 10 and 11 (`x64`).
* **Elevation Requirements**: System-level scopes (Windows Temp, Prefetch, Servicing logs, WinSxS) require administrator privileges to scan and clean.
* **In-Use File Locks**: Files actively locked with exclusive handles by running processes cannot be removed until those processes close.
* **Memory Optimization Reality**: Flushing standby memory returns cached pages to the free pool; it does not alter physical hardware capacity or guarantee higher frame rates.
* **Storage Reclaim Variance**: Recovered space depends on individual machine history, browser activity, and third-party application cache usage.

---

## Supported Platforms

| Operating System | Architecture | Support Status | Minimum Requirement |
| :--- | :--- | :--- | :--- |
| **Windows 11** | `x64` (AMD64) | **Supported** | Version 21H2 or newer (64-bit) |
| **Windows 10** | `x64` (AMD64) | **Supported** | Version 1809 or newer (64-bit) |
| **Windows ARM64** | `ARM64` | **Experimental** | Supported via Windows 11 x64 emulation |
| **Windows 7 / 8.1** | Any | **Unsupported** | Target framework requires Windows 10+ |

Deltempo is compiled as a self-contained single-file executable (`win-x64`). No separate .NET runtime installation is required.

---

## Project Architecture

```text
Deltempo/
├── Cli/                     # Headless command-line interface (Deltempo.Cli.csproj)
├── Core/                    # Core domain logic, safety rules, and update verification
│   ├── Cleaning/            # CleanupPlan, CleanupPlanner, CleanupExecutor
│   ├── Safety/              # FileSafetyEngine, ProtectionPolicy, PathSecurity, SafetyRiskTier
│   └── Update/              # UpdateSecurityValidator, PatchIntegrityVerifier, TransactionJournal
├── Services/                # System integrations (CleanerService, MemoryOptimizer, LargeFileHunter)
├── Models/                  # Telemetry models, target folder definitions, and data structures
├── Views/ & ViewModels/     # WPF UI presentation layer (Fluent dark and light themes)
├── Tests/                   # Automated verification suite (xUnit on .NET 10)
├── scripts/                 # Build, test, and release packaging scripts
└── docs/                    # GitHub Pages website and technical documentation
```

---

## Development

### Prerequisites
* Windows 10 or 11 (64-bit)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ or Windows PowerShell 5.1

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Build GUI and CLI in Release configuration
dotnet build WinTempCleaner.csproj -c Release
dotnet build Cli/Deltempo.Cli.csproj -c Release

# Run the test suite
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

# Package the standalone release executable
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```
The compiled single-file binary is generated at `publish/Deltempo.exe`.

---

## Testing

Deltempo maintains an automated test suite using **xUnit** on .NET 10:

```powershell
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
```

### Coverage Scope
* **Path Security**: Canonicalization, prefix boundary containment, traversal attack prevention, junction detection, and UNC path rejection.
* **Safety Classification**: Validation that user profiles, personal documents, credentials, repositories, and system binaries are classified as `Protected`.
* **Two-Phase Cleanup Planning**: Validates that dry-run simulations match live candidate sets and that pre-deletion revalidation catches altered disk state.
* **Update Verification**: Schema validation, host allowlisting, HTTPS enforcement, and SHA-256 payload integrity.
* **Service Integrations**: Memory telemetry queries, startup registry key toggles, and file classification heuristics.

---

## Security

Please review [SECURITY.md](SECURITY.md) for vulnerability disclosure guidelines. To report a security vulnerability or a safety engine bypass, please open a private security advisory on GitHub or contact the maintainers.

---

## Contributing

1. Fork the repository on GitHub.
2. Create a focused branch (`git checkout -b feature/new-scope`).
3. Make changes adhering to established C# coding conventions and safety invariants.
4. Run the test suite (`dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release`).
5. Open a Pull Request with a clear description of your changes.

For further guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

Deltempo is open-source software licensed under the **[MIT License](LICENSE)**.

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## Links

| Resource | Description | Location |
| :--- | :--- | :--- |
| **Official Website** | Product overview, interactive simulator, and showcase | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Documentation** | User guides and architecture documentation | [beso1227.github.io/Deltempo/docs/](https://beso1227.github.io/Deltempo/docs/) |
| **Latest Releases** | Standalone binaries and milestone release notes | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Changelog** | Complete history of changes and milestones | [docs/changelog/](https://beso1227.github.io/Deltempo/changelog/) |
| **FAQ** | Frequently asked questions | [docs/faq/](https://beso1227.github.io/Deltempo/faq/) |
| **Security Policy** | Vulnerability disclosure and security posture | [SECURITY.md](SECURITY.md) |
| **Contributing** | Contributor guidelines and coding rules | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Issue Tracker** | Bug reports, feature requests, and discussions | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
