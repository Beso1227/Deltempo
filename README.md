<div align="center">

  <img src="docs/app_icon.png" alt="Deltempo Logo" width="88" height="88" />

  # Deltempo: Open-Source Windows Cleaner, App Uninstaller & Memory Optimizer

  <p><strong>Fast, privacy-first Windows cleaner, deep root uninstaller, startup intelligence engine, and NT memory optimizer for Windows 10 & 11.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><img src="https://img.shields.io/github/v/release/Beso1227/Deltempo?label=Release&color=06B6D4" alt="Latest Release" /></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&label=CI%20Build" alt="CI Build Status" /></a>
    <a href="docs/TESTING.md"><img src="https://img.shields.io/badge/Tests-598%20Passed%20(0%20failed)-10B981" alt="Tests: 598 Passed" /></a>
    <a href="docs/THREAT_MODEL.md"><img src="https://img.shields.io/badge/Security-STRIDE%20Hardened-8B5CF6" alt="STRIDE Hardened" /></a>
    <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" /></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><img src="https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white" alt="C# 13" /></a>
    <a href="#supported-platforms"><img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D4?logo=windows" alt="Platform Support" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/github/license/Beso1227/Deltempo?color=10B981" alt="License: MIT" /></a>
  </p>

  <p>
    <a href="#telemetry--offline-guarantee"><img src="https://img.shields.io/badge/Telemetry-Zero%20%7C%20100%25%20Offline-10B981" alt="Zero Telemetry" /></a>
    <a href="docs/ARCHITECTURE.md"><img src="https://img.shields.io/badge/Memory%20Engine-NT%20Kernel%20Native-0EA5E9" alt="NT Kernel Native" /></a>
    <a href="#installation"><img src="https://img.shields.io/badge/winget-Beso1227.Deltempo-0078D4" alt="winget package" /></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><img src="https://img.shields.io/badge/Binary-Portable%20Single--File-F59E0B" alt="Portable Single File" /></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><img src="https://img.shields.io/badge/Safety%20Model-Two--Phase%20Verified-8B5CF6" alt="Two Phase Verified Safety" /></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><img src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?color=10B981&label=Downloads&logo=github" alt="GitHub Downloads" /></a>
    <a href="https://github.com/Beso1227/Deltempo/stargazers"><img src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=flat&color=F59E0B&logo=github" alt="GitHub Stars" /></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><img src="https://img.shields.io/badge/PRs-Welcome-brightgreen" alt="PRs Welcome" /></a>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest">Download v1.7.0</a> •
    <a href="https://beso1227.github.io/Deltempo/"><strong>Deltempo Official Website</strong></a> •
    <a href="docs/ARCHITECTURE.md">Architecture</a> •
    <a href="docs/THREAT_MODEL.md">Threat Model</a> •
    <a href="docs/TESTING.md">Testing Guide</a> •
    <a href="docs/BENCHMARKS.md">Benchmarks</a> •
    <a href="docs/RELEASES.md">Release Engineering</a> •
    <a href="SECURITY.md">Security Policy</a> •
    <a href="#quick-start">Quick Start</a>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.jpg" controls="controls" width="100%"></video>
  <p><sub>🎬 <strong>Deltempo Product Showcase Video</strong> (34s · 1080p · 30fps)</sub></p>

</div>

---

## At a Glance

| Property | Detail |
| :--- | :--- |
| **Official Website** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Platform** | Windows 10 & 11 (64-bit / x64) |
| **Version** | v1.7.0 (Production Release) |
| **License** | Open Source ([MIT](LICENSE)) |
| **Interfaces** | Modern Desktop GUI (WPF Fluent) and Headless Terminal CLI |
| **Distribution** | Portable single-file executable (self-contained, no installer required) |
| **Test Coverage** | 598 automated tests (100% pass rate, 0 failed, 0 skipped), adversarial filesystem fuzzing |
| **Telemetry** | Zero telemetry. Scan, clean, memory, and uninstaller operations execute 100% offline |
| **Safety Engine** | Two-phase planning (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) with 5 risk tiers & transaction journaling |
| **App Uninstaller** | Bulk silent uninstaller, BCU engine, leftover AppData/Registry trace cleanup, forced wipe for broken apps |
| **Restore Points** | Optional pre-uninstall Windows System Restore Points (user-controlled, disabled by default) |
| **Service Intelligence** | Explains: *"Will anything go wrong if I disable this?"* via 3 safety verdicts, offline heuristics & multi-model AI |
| **Process Manager** | Real-time process listing with live high-DPI application icon extraction and memory footprint analysis |
| **WinUtil Integration** | 1-Click launcher for Chris Titus Tech WinUtil (CTT) utility directly from the toolbar |
| **Memory Engine** | Native Windows NT kernel calls (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Preferences Hub** | Categorized 4-tab control center (*Updates*, *General*, *Memory*, *Storage & Safety*) |
| **Tray Guardian** | High-DPI native Win32 icon (`LoadCrispTrayIcon`) with live RAM telemetry & 1-click Boost |
| **Themes & A11y** | Pro eye-comfort Porcelain Slate Light mode, Obsidian Dark mode, and full Arabic RTL layout safety |
| **Updates** | Cryptographically verified (SHA-256) Stable & Beta release channels with atomic staging |

---

## What is Deltempo?

**Deltempo** is a modern, high-performance, open-source Windows maintenance suite engineered to safely reclaim storage space, thoroughly uninstall stubborn software, monitor startup boot impact, and optimize system memory. It purges disposable application caches, orphaned installer remnants, build artifacts, and stale system logs without touching personal documents, browser credentials, or critical operating system components.

Traditional cleanup utilities often function as opaque black boxes, install bundled adware, or leave deep registry residue behind. Deltempo is engineered on a **safety-first architecture**: candidate paths are classified into explicit risk tiers, simulated before deletion, bounded within authorized directory roots, and revalidated immediately before removal to guard against filesystem race conditions.

In addition to disk cleanup, Deltempo includes low-level Windows NT kernel memory management tools to flush standby page lists and trim inactive working sets through official Win32 and NT system calls.

---

## ⚔️ How Deltempo Compares to Competitors

Most Windows cleaning and optimization utilities either bundle commercial adware, require invasive background services, lock essential features behind paid subscriptions, or rely on outdated codebases. 

Deltempo is completely open-source, contains zero telemetry, requires no installation, and provides an end-to-end suite combining precision cleaning, deep root software uninstallation, kernel-level memory management, and startup service intelligence.

| Capability / Feature | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **License & Codebase** | **MIT Open Source (C# 13 / .NET 10)** | Proprietary / Commercial | GPLv3 Open Source (Python/GTK) | Apache 2.0 Open Source (.NET) | Proprietary (Microsoft) |
| **Telemetry & Privacy** | **Zero Telemetry (100% Offline)** | ⚠️ Trackers & Data Collection | ✅ Zero Telemetry | Minimal Telemetry | Windows Diagnostic Telemetry |
| **Bundled Adware / Upsells** | **None / Never** | ⚠️ Historical adware bundles & upsells | None | None | None |
| **Memory Engine** | **Native NT Kernel (`NtSetSystemInformation`)** | ⚠️ Basic (Paid Pro only) | ❌ None | ❌ None | ❌ None |
| **Deep Root App Uninstaller** | **Silent Bulk + Leftovers + Quarantine** | ⚠️ Basic (Paid Pro for deep) | ❌ None | ✅ Comprehensive | ❌ Basic Add/Remove only |
| **Optional Restore Points** | **Optional (User Choice, Off by Default)** | ⚠️ Automated / Paid feature | ❌ None | Optional | Manual system toggle |
| **Startup Service Intelligence** | **Yes ("Will anything break?" 3-tier verdicts)** | ❌ Plain on/off toggle list | ❌ None | Detailed Registry list | Basic Task Manager metrics |
| **Leftover Trace Sweeper** | **AppData, ProgramData, Registry & Shortcuts** | ⚠️ Paid Pro only | ❌ None | ✅ Manual Registry search | ❌ None |
| **Quarantine Vault (Rollback)** | **Compressed timestamped backup vault** | ❌ None | ❌ None | ❌ None | ❌ None |
| **Developer & Shader Caches** | **NuGet, npm, pip, Cargo, Gradle, GPU Shaders** | ❌ Browser & Windows only | ⚠️ Partial | ❌ None | ❌ None |
| **Large Files Discovery** | **AI-Categorized (>50MB, risk-classified)** | ❌ Basic File Search | ❌ None | ❌ None | Basic drive breakdown |
| **System File Repair** | **Integrated SFC, DISM & CHKDSK** | ❌ Separate paid utility | ❌ None | ❌ None | Manual Command Prompt |
| **WinUtil (Chris Titus) Integration** | **1-Click Integrated Elevated Launcher** | ❌ None | ❌ None | ❌ None | ❌ None |
| **Modern UI & Eye Comfort** | **Obsidian Dark & Porcelain Slate Light (WPF)** | Outdated / Cluttered | Legacy GTK2/3 interface | Legacy WinForms interface | Built-in Windows Settings |
| **Full CLI Parity** | **Yes (`deltempo` CLI with `--json` & dry-run)** | ⚠️ Limited command switches | Basic CLI | Basic CLI | ❌ None |
| **Distribution** | **Portable Single-File (68 MB, no installer)** | Requires setup installer & services | Installer or portable zip | Requires installer & runtime | Built into OS |

---

## 🌟 Core Features Deep Dive

### 1. 🧹 Precision 26+ Scope Storage Cleaner
Deltempo targets disposable data across system, developer, and gaming environments without touching user documents, active authentication tokens, or personal settings:
* **Operating System Scopes**: User Temp (`%TEMP%`), Windows Temp (`C:\Windows\Temp`), Prefetch, Windows Update download caches (`SoftwareDistribution\Download`), Windows Upgrade residue (`$WINDOWS.~BT`), Delivery Optimization caches, Windows Error Reporting (`WER`), Memory Dumps, and Font/Thumbnail caches.
* **GPU & Game Shaders**: NVIDIA App / GeForce Experience OTA cache, AMD Radeon Software cache, DirectX shader caches (`D3DSCache`), Vulkan pipelines (`GLCache`), Steam shader pre-caching, and Epic Games launcher webcaches.
* **Developer Ecosystem**: NuGet v3 local cache, npm cache, pip cache, Rust Cargo target cache, Gradle caches, Android Studio emulator temporary snapshots, and VS Code extension caches.
* **Modern Browsers & Communication**: Chromium profiles (Chrome, Edge, Brave, Opera, Vivaldi, Arc) and Gecko profiles (Firefox) disposable cache directories with **login sessions strictly preserved**; Discord, Slack, and Spotify media caches.
* **Safety Shield (>24h)**: Optional safety guard that exempts any file created or modified within the last 24 hours to prevent clashing with active background installers or running editors.
* **TOCTOU Guard**: Verifies file boundaries, attributes, and canonical paths immediately prior to unlinking to eliminate race conditions.

---

### 2. 📦 Deep Root App Uninstaller & Leftover Sweeper
Say goodbye to stubborn bloatware, half-deleted software, and messy uninstallation wizards:
* **Unified Application Inventory**: Scans both 64-bit and 32-bit registry hives (`HKLM`, `HKCU`) alongside modern Windows Store (AppX/UWP) packages. Displays real installation size, publisher verification, version, and install dates.
* **Optional System Restore Point**: Unlike other utilities that force a slow 2-minute system restore point or skip it entirely, Deltempo gives full control to the user. A dedicated toggle lets you decide whether to create a pre-uninstall restore checkpoint (**turned off by default**).
* **Silent Multi-App Bulk Removal**: Select multiple applications and trigger unattended uninstallation without clicking through dozens of repetitive installer dialogs.
* **Deep Root Leftover Sweep**: After an application uninstaller finishes, Deltempo's scanner hunts down orphaned remnants:
  * Registry branches: `HKCU\Software\<Vendor>`, `HKLM\Software\<Vendor>`, `HKLM\Software\WOW6432Node\<Vendor>`.
  * Filesystem directories: `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * Startup entries and Start Menu orphaned shortcuts.
* **Force Wipe for Corrupt Software**: If an uninstaller is broken, missing, or throws errors, Deltempo forcefully cleans all related filesystem directories and deregisters its registry keys cleanly.
* **Quarantine Vault**: All purged leftover files can be compressed and sealed into a timestamped, recoverable ZIP archive in the Quarantine Vault before deletion.

---

### 3. ⚡ Native Windows NT Kernel Memory Optimizer
Unlike consumer "RAM cleaners" that simply force memory into the swap file and slow down your PC, Deltempo utilizes native, documented Windows NT kernel system calls:
* **Standby List Invalidation**: Calls `NtSetSystemInformation` with `SystemMemoryListInformation` (class `80`) to flush unused cached standby memory pages back into the available pool for high-demand tasks (gaming, compiling, rendering).
* **Inactive Working Set Trimming**: Leverages `EmptyWorkingSet` with elevated process tokens (`SeProfileSingleProcessPrivilege` and `SeDebugPrivilege`) to release abandoned working sets from inactive background processes.
* **Critical Process Shield**: Core Windows components (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe`, and Windows Defender) are automatically shielded and never trimmed.
* **Background Auto-Boost**: Can monitor memory pressure in the background and automatically trigger a clean when physical RAM usage exceeds a user-defined threshold (e.g., 85%).

---

### 4. 🧠 Startup Manager & Service Intelligence
Stop wondering what programs are slowing down your PC's boot time:
* **"Will anything go wrong if I disable this?"**: Every startup application and background service is analyzed with an intelligent 3-tier verdict badge:
  * 🟢 **SafeToDisable**: Convenience launchers, game updaters, and communication apps that do not need to boot with Windows.
  * 🟡 **CautionNeeded**: Audio control panels, trackpad utilities, or peripheral software where hotkeys or tray menus might become inactive until opened manually.
  * 🔴 **EssentialKeep**: Security suites, cloud sync backup agents, or essential hardware drivers.
* **Dual Intelligence Pipeline**:
  * **Offline Heuristics**: Instant deterministic classification based on digital signatures, verified vendor identities, binary paths, and known process databases.
  * **Optional Multi-Provider AI**: On-demand detailed operational summaries supporting OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter, and local offline models (Ollama, LM Studio).
* **100% Reversible Registry Toggles**: Disabled items are stored safely in `Run_Deltempo_Disabled` registry keys. Any item can be re-enabled with a single click.

---

### 5. 🔍 AI-Categorized Large File Inspector
Find out what is actually consuming your disk space:
* **Multi-Drive Scanning**: Rapidly scan `C:\` or any secondary fixed drive for files exceeding customizable size thresholds (>50 MB, >100 MB, >500 MB, >1 GB).
* **Automatic Category Tagging**: Intelligently groups discoveries into Archives (`.zip`, `.rar`, `.7z`), Disk Images (`.iso`, `.vhd`), Virtual Machine disks (`.vmdk`, `.vhdx`), Installers (`.msi`, `.exe`), Video/Audio media, and Stale Log files.
* **Safety Risk Tiering**: Every large file is evaluated for safety before you touch it, preventing accidental deletion of hypervisor disks or important game installations.

---

### 6. 🛠️ Windows System Repair & CTT WinUtil Integration
Diagnose and repair Windows operating system corruption directly from the interface:
* **SFC (System File Checker)**: Executes `sfc /scannow` in an elevated context to repair corrupt system files.
* **DISM Servicing**: Checks, scans, and restores Windows Component Store health (`/Cleanup-Image /RestoreHealth`).
* **WinSxS Base Reset**: Cleans up superseded component store versions to recover gigabytes after major Windows updates.
* **CHKDSK & Network Reset**: Schedule disk volume verification on next boot or flush DNS and reset Winsock stacks with one click.
* **Chris Titus Tech WinUtil (CTT)**: Integrated 1-click launcher runs the renowned elevated PowerShell WinUtil suite for debloating, telemetry removal, and automated winget software setup.

---

### 7. 📊 Real-Time Process Manager
* **Native High-DPI Icon Extraction**: Live extraction of 32-bit crisp executable icons using native Win32 `SHGetFileInfo` and `ExtractIconEx` routines.
* **Memory & PID Telemetry**: Real-time process memory footprint, process ID, publisher information, and file path.
* **Safe Termination**: Protected kill routines prevent accidental termination of critical Windows system processes.

---

### 8. 🎨 Pro Eye-Comfort Themes & Multilingual RTL Support
Designed with obsessive attention to user experience:
* **Pro Eye-Comfort Light Mode**: A soothing, Fluent/macOS porcelain and slate theme (`#F1F5F9`) that completely eliminates eye strain, replaces glaring white screens, and uses high-contrast Ocean Azure (`#0284C7`) accents.
* **Obsidian Dark Mode**: Sleek, deep-space dark theme with vibrant electric cyan accents, subtle glassmorphism, and double-bezel cards.
* **Comprehensive Multilingual Coverage**: Full native translations across **English, Arabic, Spanish, French, and German**.
* **RTL Layout & Numeric Protection**: Arabic mode activates true Right-to-Left (RTL) window flow while strictly enforcing Left-to-Right formatting on metrics, paths, and progress indicators (`0.0 MB`, `32%`, `C:\...`) so numbers and storage stats are never reversed or garbled.

---

### 9. 🔔 Pixel-Perfect System Tray Guardian
* **True High-DPI Win32 Icon (`LoadCrispTrayIcon`)**: Employs direct Win32 GDI icon creation (`CreateIconIndirect`) with 32-bit ARGB alpha transparency, delivering razor-sharp rendering on 100%, 125%, 150%, 175%, and 200%+ scaling displays without blurring.
* **Live Hover Telemetry**: Displays real-time memory usage in the tray tooltip: `RAM: 42% (13.4 GB / 31.9 GB)`.
* **Quick Context Actions**: Right-click to trigger **1-Click Boost Memory** or **Quick Smart Clean** without opening the main window.
* **Explorer Resilience**: Listens for the Windows `TaskbarCreated` broadcast message to automatically restore the icon if `explorer.exe` restarts.

---

## 💻 CLI Reference & Headless Automation

Deltempo includes a high-performance, scriptable CLI (`deltempo_cli.exe` or `deltempo` command) designed for scheduled tasks, system administrators, and headless environments.

```powershell
# Preview cleanable targets without deleting files (dry-run)
deltempo clean --dry-run

# Run safe cleanup and send deleted files to the Windows Recycle Bin
deltempo clean --safe --recycle-bin

# Flush standby RAM and trim process working sets
deltempo boost

# Deep uninstallation of an application without creating a restore point
deltempo uninstall "Google Chrome" --silent --force

# Optional restore point creation during uninstallation
deltempo uninstall "Epic Games Launcher" --restore-point

# Output system telemetry and memory health in structured JSON
deltempo status --json
```

### CLI Command Summary

| Command | Purpose | Key Flags & Options |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Scan targets for disposable data | `--json`, `--silent` |
| `deltempo clean [filter]` | Clean disposable caches | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Quick purge of verified safe caches | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Autonomous full cleanup (RAM, DISM, scopes) | `--dry-run`, `--json`, `--yes` |
| `deltempo boost` | Optimize system memory via NT kernel | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo uninstall <app>` | Deep root uninstallation and leftover purge | `--dry-run`, `--force`, `--silent`, `--no-quarantine`, `--restore-point`, `--json` |
| `deltempo large [path]` | Scan drives for space-consuming files | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>`| Inspect file risk tier and safety verdict | `--json` |
| `deltempo large clean` | Recycle disposable large files | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo startup [list]` | Inspect startup applications and boot impact | `--high`, `--json` |
| `deltempo startup disable <app>`| Reversibly disable a startup program | N/A |
| `deltempo startup enable <app>` | Restore a disabled startup program | N/A |
| `deltempo repair [subcommand]` | Windows integrity check & servicing repair | `sfc`, `dism`, `winsxs`, `chkdsk`, `update`, `network` |
| `deltempo status` | Display system telemetry and memory info | `--json` |
| `deltempo update [check]` | Check for official releases or apply update | `check`, `--dry-run` |
| `deltempo register` | Opt-in shell integration (PATH, Win+R alias) | `--status`, `--remove` |
| `deltempo unregister` | Remove all shell integration | N/A |

---

## 🚀 Quick Start

### Option 1: Portable Standalone Executable (Recommended)
1. Download **`Deltempo.exe`** from the [Latest Release](https://github.com/Beso1227/Deltempo/releases/latest) page.
2. Run `Deltempo.exe` directly (no installer required, self-contained single-file).
3. Click **Scan Now** or **1-Click Deep Clean** to reclaim space.

### Option 2: Windows Package Manager (WinGet)
```powershell
winget install Beso1227.Deltempo
```

### Option 3: Terminal Registration
Running `Deltempo.exe` automatically registers user-level App Paths so you can press <kbd>Win</kbd> + <kbd>R</kbd> and type `deltempo`, or use `deltempo` directly in any PowerShell or Command Prompt terminal.

---

## 🔒 Privacy & Security Guarantee

Deltempo is architected with security and user privacy as non-negotiable fundamentals:

1. **Zero Telemetry Guarantee**: Deltempo contains **zero telemetry**, analytics libraries, advertising SDKs, or background ping trackers. Routine scanning, cleaning, memory optimization, and uninstallation run **100% offline**.
2. **Deterministic Safety Pipeline**:
   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```
   Candidate files are planned, verified against protected directory boundaries (Documents, Desktop, Code Repositories, SSH keys, credentials), and revalidated immediately before deletion.
3. **Reparse Point & Traversal Defense**: NTFS directory junctions, symbolic links, and volume mount points are automatically rejected to prevent traversal attacks outside target boundaries.
4. **Local Drive Boundary**: Confined exclusively to local fixed drives; remote network shares and UNC paths are blocked.
5. **Cryptographic Release Verification**: Automatic update checks enforce HTTPS and verify binary SHA-256 digests against signed GitHub release manifests.

---

## 🛠️ Building from Source

### Prerequisites
* Windows 10 or 11 (64-bit / x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ or Windows PowerShell 5.1

### Compilation & Testing
```powershell
# Clone the repository
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Compile the entire solution in Release configuration
dotnet build deltempo.sln -c Release

# Execute the automated test suite (598 passing unit & integration tests)
dotnet test deltempo.sln -c Release

# Package the standalone single-file release binaries (GUI & CLI)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

The resulting single-file executables will be published to:
* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 📜 License

Deltempo is free and open-source software licensed under the **[MIT License](LICENSE)**.

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## 🌐 Community & Resources

| Resource | Link |
| :--- | :--- |
| **Official Website** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Latest Releases** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Architecture Specification** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **Threat Model & Security** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **Testing Guide** | [docs/TESTING.md](docs/TESTING.md) |
| **Contributing Guide** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Security Policy** | [SECURITY.md](SECURITY.md) |
| **Bug Reports & Issues** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
