<div align="center">

  <img src="docs/app_icon.png" alt="Deltempo Logo" width="96" height="96" />

  # Deltempo

  <p><strong>Open-source, privacy-first Windows cleaner and memory optimizer.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><img src="https://img.shields.io/github/v/release/Beso1227/Deltempo?label=Release&color=06B6D4" alt="Latest Release" /></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&label=CI%20Build" alt="CI Status" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/github/license/Beso1227/Deltempo?color=10B981" alt="License: MIT" /></a>
    <a href="https://beso1227.github.io/Deltempo/"><img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D4" alt="Platform Support" /></a>
    <a href="https://beso1227.github.io/Deltempo/"><img src="https://img.shields.io/badge/Website-Live-8B5CF6" alt="Official Website" /></a>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest">Download Latest Release</a> •
    <a href="https://beso1227.github.io/Deltempo/">Official Website</a> •
    <a href="https://beso1227.github.io/Deltempo/docs/">Documentation</a> •
    <a href="SECURITY.md">Security Policy</a> •
    <a href="#quick-start">Quick Start</a>
  </p>

</div>

---

## What is Deltempo?

**Deltempo** is a transparent, open-source utility designed to help Windows users recover disk space and optimize system memory safely. It targets disposable application caches, orphaned installer artifacts, temporary build output, and stale system diagnostics without endangering personal documents, browser sessions, or core operating system files.

Unlike traditional system cleaners that treat cleanup as an opaque black box, Deltempo operates on an explicit **safety-first architecture**. Every candidate target is classified into a structured risk tier, simulated before execution, bounded within strict root paths, and revalidated immediately before deletion to eliminate time-of-check to time-of-use (TOCTOU) race conditions.

In addition to disk cleaning, Deltempo incorporates native Windows NT kernel memory management routines. It allows users and system administrators to purge standby memory lists and trim inactive process working sets through official Win32 and NT system calls, all packaged into a single, portable executable with both graphical and command-line interfaces.

---

## Why Deltempo?

* **Open Source & Auditable**: Distributed under the permissive MIT license. All cleanup rules, safety checks, and native API invocations are fully inspectable in C#.
* **Safety-First Architecture**: Built on a deterministic two-phase model (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) with path boundary enforcement, reparse point rejection, and protected system folder exclusion.
* **Native Windows NT Memory Management**: Purges standby memory lists and trims background process working sets via native `NtSetSystemInformation` and `EmptyWorkingSet` system calls.
* **Dual Interface (GUI & CLI)**: Use a modern Windows desktop interface for visual analysis or automate routines via a headless CLI with structured JSON output.
* **Zero Telemetry & Local Operation**: Operates 100% offline. Contains no analytics tracking, no third-party advertisements, and no cloud dependencies.
* **Single Portable Binary**: Shipped as a self-contained, single-file Windows executable. No installer, background service, or extra runtime installation is required.

---

## Features

### 1. Disk Cleanup Engine
* **31+ Targeted Scopes**: Scans disposable caches across Windows servicing, graphics drivers, developer toolchains, gaming platforms, creator applications, desktop software, and web browsers.
* **Deterministic Simulation**: Supports dry-run execution (`--dry-run`) across both the GUI and CLI, showing exactly what would be removed without touching disk state.
* **Orphaned Application Detection**: Discovers leftover directories in `%AppData%` from applications that have already been uninstalled from the system registry.

### 2. NT Kernel Memory Optimization
* **Standby List Purge**: Reclaims cached memory pages from closed programs using native NT kernel calls.
* **Process Working Set Trimming**: Safely trims unreferenced physical memory across inactive user applications.
* **System File Cache Reset**: Flushes operating system filesystem cache working set limits.
* **System Process Shielding**: Automatically excludes protected Windows processes (`csrss.exe`, `dwm.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe`, and Windows Defender) from memory operations.

### 3. System Inspection & Control
* **Large File Hunter**: Locates space-consuming files with entropy analysis, safety classification, and optional Windows Recycle Bin deletion.
* **Startup Accelerator**: Audits registry and startup folder launch items, calculates boot impact, and provides 100% reversible disabling via registry backup keys.
* **System Servicing & Repair**: Convenient CLI access to Windows integrity tools including SFC, DISM, WinSxS cleanup, and CHKDSK.

---

## Safety First

The primary design principle of Deltempo is:

> **Never optimize for deleting more files. Optimize for proving that every deletion is safe.**

Deltempo replaces opaque deletion scripts with a disciplined 5-stage pipeline:

```text
┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
│  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
└────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
```

### Safety Pipeline Stages

1. **Scan**: Discovers candidate items in authorized target scopes while collecting metadata (size, age, attributes, and path hierarchy).
2. **Plan**: Assembles a `CleanupPlan` containing candidate actions. Identical planning logic is executed during both simulation (`--dry-run`) and live cleanup.
3. **Protect**: Evaluates candidate files against global protection policies, strictly shielding user profiles, personal documents, desktop files, git repositories, and credentials.
4. **Revalidate**: Immediately before invoking destructive APIs, paths are canonicalized, verified against subpath boundaries, checked for NTFS junctions/reparse points, and checked against system file attributes.
5. **Clean**: Executes authorized deletions while maintaining a structured audit trail of processed, deleted, recycled, and skipped bytes.

### 5-Tier Safety Classification

Every candidate file is evaluated by `FileSafetyEngine` and assigned to one of five risk tiers:

| Tier | Classification | Policy & Runtime Behavior |
| :--- | :--- | :--- |
| **Protected** | System & User Files | Critical operating system binaries, personal user directories (Documents, Pictures, Desktop), source repositories, SSH keys, active credentials, and session databases. **Strictly immutable — deletion is blocked.** |
| **Safe** | Verified Caches | Deterministically verified disposable cache or temporary data located strictly within an authorized scope and matching known transient patterns. **Eligible for deletion.** |
| **LowRisk** | Aged Temp Files | Temporary files meeting conservative age thresholds (e.g. older than 24 hours), inactive crash reports, or compiler artifacts in designated build output directories. **Eligible for deletion.** |
| **ReviewRequired** | Ambiguous / Code | Files residing within disposable locations that have non-standard extensions, executable headers, or active lock state. **Action defaults to KEEP unless explicitly confirmed.** |
| **Unknown** | Unverified Context | Any file or directory where safety cannot be proven conclusively from trusted rules. **Strictly preserved.** |

### Additional Defensive Controls
* **Reparse Point & Symlink Defense**: Deltempo detects and rejects NTFS junctions, directory symbolic links, and volume reparse points to prevent link-traversal attacks outside designated target roots.
* **UNC & Remote Path Rejection**: Operations are confined to local fixed physical drives; remote network shares and UNC paths are rejected.
* **Windows Recycle Bin Integration**: Deletions can be routed through the Windows Shell Recycle Bin (`SHFileOperation`), preserving the ability to restore files.

---

## Quick Start

### Running the Desktop Application (GUI)

1. Download **`Deltempo.exe`** from the [Latest Release](https://github.com/Beso1227/Deltempo/releases/latest) page.
2. Launch `Deltempo.exe` (no installation required).
3. Click **Scan** to analyze cleanable data across selected categories.
4. Review the candidate list and click **Clean** to perform cleanup.

### Installing via WinGet

```powershell
winget install Beso1227.Deltempo
```

### Running via Command Line (CLI)

Launching `Deltempo.exe` once registers the global `deltempo` command across PowerShell and Windows Terminal:

```powershell
# Perform a simulation scan across all scopes
deltempo scan --dry-run

# Run safe cleanup routing disposable files to the Recycle Bin
deltempo clean --safe --recycle-bin

# Flush standby memory lists and trim inactive working sets
deltempo boost

# Inspect system status and memory distribution in JSON format
deltempo status --json
```

---

## CLI Reference

Deltempo provides a synchronous, scriptable command-line interface suitable for automation and scheduled maintenance.

### Command Overview

| Command | Purpose | Key Flags & Options |
| :--- | :--- | :--- |
| `deltempo scan [category]` | Scan targets for disposable data | `--dry-run`, `--json`, `--safe` |
| `deltempo clean [category]` | Clean disposable caches | `--dry-run`, `--recycle-bin`, `--safe`, `--json` |
| `deltempo smart-clean` | Quick purge of verified safe caches | `--dry-run`, `--json` |
| `deltempo deep-clean` | Autonomous full cleanup (RAM, DISM, scopes) | `--dry-run`, `--json` |
| `deltempo boost` | Optimize system memory | `--all`, `--standby`, `--cache`, `--json` |
| `deltempo large [path]` | Scan drives for space-consuming files | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>` |
| `deltempo large inspect <file>` | Inspect file risk tier and safety verdict | N/A |
| `deltempo large clean` | Recycle disposable large files | `--dry-run`, `--yes` |
| `deltempo startup` | Inspect startup applications and boot impact | `--json` |
| `deltempo startup disable <app>` | Reversibly disable a startup program | N/A |
| `deltempo startup enable <app>` | Restore a disabled startup program | N/A |
| `deltempo repair [subcommand]` | Windows integrity check & servicing repair | `sfc`, `dism`, `winsxs`, `chkdsk`, `update` |
| `deltempo status` | Display system telemetry and memory info | `--json` |
| `deltempo update [subcommand]` | Check for releases or continuous patches | `check`, `patch`, `channel` |

### Practical CLI Examples

```powershell
# 1. Preview cleanable cache size without modifying disk state
deltempo scan --dry-run

# 2. Target only temporary files older than 24 hours with JSON output
deltempo clean temp --safe --json

# 3. Purge the Windows standby memory page list specifically
deltempo boost --standby

# 4. Find all files larger than 1 GB on drive D:
deltempo large D:\ --min 1GB --top 20

# 5. Check whether a specific file is safe to delete
deltempo large inspect "C:\Users\username\AppData\Local\Temp\installer.exe"

# 6. Reversibly disable an unnecessary startup launcher
deltempo startup disable "Spotify"

# 7. Check for continuous rolling patches
deltempo update patch
```

---

## Cleaning Targets

Deltempo organizes its cleanup scopes across distinct system and application domains. Scopes marked with an asterisk require administrative elevation.

| Domain | Target Scope | Examples of Cleaned Data |
| :--- | :--- | :--- |
| **Windows System** | User Temp | Application scratchpads and installer extractions (`%TEMP%`) |
| | Windows System Temp* | Servicing logs and update staging (`C:\Windows\Temp`) |
| | Windows Prefetch* | Stale application execution headers (`C:\Windows\Prefetch`) |
| | Windows Update Cache* | Superseded package downloads (`SoftwareDistribution\Download`) |
| | Upgrade Residue* | Post-upgrade archives (`$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD`) |
| | Delivery Optimization* | Peer-to-peer Windows Update delivery cache chunks |
| | Component Caches* | Font caches, WinSxS temp scratchpads, DISM staging |
| | Diagnostic Logs* | CBS servicing logs, DISM logs, Panther, SetupAPI traces |
| | Crash Dumps* | Memory crash dumps (`MEMORY.DMP`), LiveKernelReports, minidumps |
| | Explorer Thumbnails | Cached thumbnail databases (`thumbcache_*.db`) |
| **Hardware & Drivers** | Driver Packages* | NVIDIA App/GeForce OTA packages, AMD/Intel temp installers |
| | GPU Shader Pools | Compiled DirectX (`D3DSCache`), Vulkan (`GLCache`), and Intel shaders |
| **Gaming & Media** | Game Launchers | Steam download chunks, Epic Games webcache, Battle.net cache |
| | Creator Render Caches | Adobe Media Cache, DaVinci proxy files, CapCut, Blender temp |
| **Apps & Social** | Desktop Apps | Discord, Spotify, Slack, VS Code, Notion GPU & code caches |
| | Messaging Apps | WhatsApp, Telegram, Teams media caches (credentials preserved) |
| | Windows Store Apps | Safe `LocalCache` and `INetCache` across packaged Store apps |
| | Orphaned AppData | Residual folders from uninstalled applications verified against registry |
| **Developer Tools** | Package Caches | NuGet v3, npm, pip, yarn, pnpm, Cargo, and Go build caches |
| | Dev Daemons | Android Studio emulator cache, Gradle daemons, iTunes sync temp |
| **Web Browsers** | Chromium Profiles | Chrome, Edge, Brave, Opera, Vivaldi disk & shader caches |
| | Gecko Profiles | Firefox, LibreWolf, Floorp cache pools (logins preserved) |
| | Temporary Internet | Windows `INetCache` and `CryptnetUrlCache` revocation caches |
| **Recovery** | Windows Recycle Bin | Empties `$Recycle.Bin` across all mounted physical drives |
| | VSS Restore Points* | Prunes older shadow copies while strictly retaining the latest |

> [!NOTE]
> Personal documents, browser cookies, saved passwords, active authentication sessions, and source code repositories are strictly excluded from cleanup.

---

## Memory Optimization

Windows uses unallocated RAM to cache recently accessed files and program state in the **Standby Page List**. While this generally improves responsiveness, heavily fragmented standby lists or unreleased process working sets can contribute to micro-stuttering during resource-intensive tasks or gaming.

Deltempo interfaces directly with Windows memory management APIs:

* **Standby List Invalidation**: Calls `NtSetSystemInformation` with the `SystemMemoryListInformation` command class (`80`) to purge standby pages and return them to the free memory pool.
* **Process Working Set Trimming**: Requests `SeProfileSingleProcessPrivilege` and `SeDebugPrivilege` to call `EmptyWorkingSet` across inactive user processes, releasing non-essential physical pages.
* **System File Cache Boundary Reset**: Calls `SetSystemFileCacheSize` to reset filesystem cache boundaries when system cache consumption becomes excessive.
* **Modified Page Flush**: Issues flush requests to ensure dirty memory pages are committed to storage before memory is freed.
* **Protected Process Shielding**: Deltempo maintains an immutable exclusion list preventing memory operations on critical system processes (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe`, and Windows Defender).

> [!IMPORTANT]
> Memory optimization reclaims inactive physical RAM and flushes stale cache lists. It does not alter CPU clock speeds, GPU hardware limits, or claim artificial benchmark gains.

---

## Large File Hunter

The **Large File Hunter** audits storage volumes to identify space-consuming files with built-in safety guidance:

* **Configurable Scans**: Filters files above a size threshold (50 MB by default, configurable via `--min` in the CLI).
* **Automated Safety Classification**: Evaluates discovered items using `FileSafetyEngine`, labeling files as `Safe`, `Protected`, `LowRisk`, or `ReviewRequired`.
* **Asset Recognition**: Differentiates disposable clutter (driver installers, setup extracts, post-mortem memory dumps) from critical assets (virtual machine disks, database stores, game archives, model weights).
* **Recycle Bin Routing**: Deletions are sent to the Windows Shell Recycle Bin by default, allowing file restoration via standard Windows undo shortcuts.

---

## Startup Manager

The **Startup Manager** provides inspection and control over programs configured to start automatically on user logon:

* **Inspected Locations**:
  * `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  * `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`
  * Startup folder shortcuts (`shell:startup`)
* **Boot Impact Analysis**: Estimates startup delay using binary metadata, publisher identity, and known background launcher signatures.
* **100% Reversible Modifications**: Rather than deleting registry values, disabled entries are moved to a designated backup key (`Run_Deltempo_Disabled`). Any disabled item can be re-enabled at any time.

---

## Automatic Updates

Deltempo features a dual-channel update system designed for reliability and cryptographic integrity:

### Update Channels
* **Stable Channel**: Delivers formal milestone releases (e.g. `v1.3.3`, `v1.3.4`) verified and tagged on GitHub Releases.
* **Continuous Patch Channel**: Delivers rolling builds generated directly from qualifying commits on the `main` branch, allowing users to receive targeted fixes without waiting for milestone releases.

### Verification & Security Controls
* **Host & Protocol Enforcement**: Manifests and payloads must originate from authorized HTTPS endpoints (`github.com`, `raw.githubusercontent.com`).
* **Metadata Schema Validation**: Enforced by `PatchMetadataValidator`, validating commit hashes, file size bounds (10 MB – 500 MB), and 64-character hex SHA-256 signatures.
* **Cryptographic Hash Verification**: The downloaded binary is validated against its expected SHA-256 hash by `PatchIntegrityVerifier` before any staging operation occurs.
* **Cross-Process Installation Locks**: Managed via `PatchInstallationLock` using global named mutexes to prevent concurrent installation conflicts.
* **Atomic Swap & Rollback**: New builds are staged and swapped atomically. If the updated executable fails initial verification, the previous binary is restored automatically.

---

## Supported Platforms

| Operating System | Architecture | Support Status | Notes |
| :--- | :--- | :--- | :--- |
| **Windows 11** | `x64` (AMD64) | **Supported** | Version 21H2 or newer (64-bit) |
| **Windows 10** | `x64` (AMD64) | **Supported** | Version 1809 or newer (64-bit) |
| **Windows ARM64** | `ARM64` | **Experimental** | Supported via Windows 11 x64 emulation |
| **Windows 7 / 8.1** | Any | **Unsupported** | Target framework requires Windows 10+ |

Deltempo is compiled as a self-contained single-file binary (`win-x64`). No separate .NET runtime installation is required on supported systems.

---

## Project Architecture

```text
Deltempo/
├── Cli/                     # Headless command-line interface (Deltempo.Cli.csproj)
│   └── Program.cs           # CLI entry point, argument parsing, output formatters
├── Core/                    # Domain logic, safety rules, and update infrastructure
│   ├── Cleaning/            # CleanupPlan, CleanupPlanner, CleanupExecutor
│   ├── Safety/              # FileSafetyEngine, ProtectionPolicy, PathSecurity, SafetyRiskTier
│   └── Update/              # PatchManifest, PatchMetadataValidator, PatchIntegrityVerifier
├── Services/                # System integrations and subsystem engines
│   ├── CleanerService.cs    # Multi-scope target scanning and cleanup dispatch
│   ├── MemoryOptimizer.cs   # Win32 & NT kernel memory APIs (Standby list, Working sets)
│   ├── LargeFileHunter.cs   # Drive auditing, entropy scoring, Recycle Bin operations
│   ├── StartupService.cs    # Registry & startup shortcut management
│   ├── UpdateService.cs     # GitHub API release checker and patch installer
│   └── ElevationService.cs  # UAC detection and administrative elevation helpers
├── Models/                  # Data structures, telemetry models, target folder definitions
├── Views/ & ViewModels/     # WPF UI presentation layer (Fluent dark and light themes)
├── Tests/                   # Automated verification suite (xUnit on .NET 10)
│   └── Deltempo.Tests/      # Unit and integration tests covering safety, cleanup & updates
├── scripts/                 # Build, test, and release packaging automation
└── docs/                    # GitHub Pages website and technical documentation
```

### Key Architectural Boundaries
* **Separation of Planning and Execution**: The `Core.Cleaning` namespace cleanly separates scan analysis (`CleanupPlanner`) from disk operations (`CleanupExecutor`).
* **Autonomous Safety Engine**: `Core.Safety` has no dependencies on the UI and can be tested and verified independently.
* **Shared Engine**: Both the WPF GUI and the CLI call the exact same underlying service layers, ensuring identical behavior across interfaces.

---

## Development

### Prerequisites
* Windows 10 or 11 (64-bit)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ or Windows PowerShell 5.1

### Clone the Repository
```powershell
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo
```

### Build the Project
```powershell
# Restore dependencies
dotnet restore

# Build GUI and CLI in Release configuration
dotnet build WinTempCleaner.csproj -c Release
dotnet build Cli/Deltempo.Cli.csproj -c Release
```

### Run the Application
```powershell
# Run the GUI application
dotnet run --project WinTempCleaner.csproj -c Release

# Run the CLI application
dotnet run --project Cli/Deltempo.Cli.csproj -c Release -- status
```

### Package Single-File Release Executable
```powershell
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```
The compiled, self-contained single-file executable will be generated at `publish/Deltempo.exe`.

---

## Testing

Deltempo maintains an automated test suite built on **xUnit** for .NET 10. The test suite verifies safety invariants, path protection policies, update validation, cleanup planning, and memory optimization logic.

### Running Tests
```powershell
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
```

### Test Coverage Areas
* **Path Security & Containment**: Verifies path canonicalization, prefix boundary containment, traversal attack prevention, junction detection, and network UNC path rejection.
* **Safety Risk Classification**: Validates that personal directories, credentials, repositories, and system files are reliably classified as `Protected`.
* **Two-Phase Cleanup Planning**: Confirms that dry-run simulations and live cleanup plans produce identical candidate sets and that pre-deletion revalidation catches altered disk state.
* **Update Verification**: Tests manifest schema validation, host allowlisting, HTTPS enforcement, SHA-256 digest validation, and corrupted payload detection.
* **Service Integrations**: Tests memory metric queries, startup registry key transformations, and large file classification heuristics.

---

## Security

Security and data integrity are central to Deltempo. The project maintains an active security policy and welcomes vulnerability reports.

* **Reporting Security Issues**: Please review our [SECURITY.md](SECURITY.md) for responsible disclosure guidelines. To report a security vulnerability or bypass in the safety engine, open a private security advisory on GitHub or contact the maintainers.
* **Local Processing Guarantee**: Deltempo processes all filesystem and system data locally on your device. It makes no external network requests other than querying GitHub for application updates when requested.
* **Non-Destructive Defaults**: Ambiguous files default to `KEEP`, and large file deletions default to the Windows Recycle Bin.

---

## Contributing

Contributions from the open-source community are welcome. To contribute:

1. **Fork** the repository on GitHub.
2. **Create a Branch** for your feature or bugfix (`git checkout -b feature/new-scope`).
3. **Make Changes** adhering to established C# coding conventions and architectural boundaries.
4. **Run Tests** to ensure all verification gates pass:
   ```powershell
   dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
   ```
5. **Open a Pull Request** with a detailed explanation of your changes.

For further guidelines on code style, UI standards, and issue triage, see [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

Deltempo is open-source software licensed under the **[MIT License](LICENSE)**.

```text
Copyright (c) 2026 Beso1227 / Deltempo Project

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
...
```

---

## Links

| Resource | Description | Location |
| :--- | :--- | :--- |
| **Official Website** | Product overview, simulation, and showcase | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Documentation** | In-depth user and developer guides | [beso1227.github.io/Deltempo/docs/](https://beso1227.github.io/Deltempo/docs/) |
| **Latest Releases** | Standalone binaries and release notes | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Changelog** | Complete history of changes and milestones | [docs/changelog/](https://beso1227.github.io/Deltempo/changelog/) |
| **FAQ** | Frequently asked questions | [docs/faq/](https://beso1227.github.io/Deltempo/faq/) |
| **Security Policy** | Vulnerability reporting and safety details | [SECURITY.md](SECURITY.md) |
| **Contributing** | Contribution workflow and coding rules | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Issue Tracker** | Bug reports, feature requests, and discussions | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
