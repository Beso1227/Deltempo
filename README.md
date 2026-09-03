<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="128" height="128" />

  # 👑 Deltempo (King Edition)
  ### Pure Precision Windows Optimizer, System Health & Profile Guardian

  **Single Standalone Executable (Zero Runtime Dependencies) • Windows NT Kernel Memory Cleaner (WinMemoryCleaner & RAMMap Engine) • AI Large File Hunter with 100% Binary Safety Certainty • Startup Boot Accelerator • Multi-Process Memory Optimizer • 25+ Elite Deep Cleaning Scopes • Multi-Profile Browser Engine • AST Knowledge Graph • 67 xUnit Tests • Zero Telemetry • 100% Free & Open Source (MIT)**

  [![Release](https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=00E5FF&logo=windows&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest)
  [![CI](https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI%2FCD)](https://github.com/Beso1227/Deltempo/actions)
  [![Tests](https://img.shields.io/badge/Tests-67%20Passing-10B981?style=for-the-badge&logo=xunit&logoColor=white)](Tests/Deltempo.Tests)
  [![License: MIT](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-3B82F6?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/Beso1227/Deltempo)
  [![Website](https://img.shields.io/badge/Official_Site-Live-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white)](https://beso1227.github.io/Deltempo/)

  <br />

  <p align="center">
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe">
      <img src="https://img.shields.io/badge/⚡_DOWNLOAD_DELTEMPO.EXE_(v1.3.0)-3B82F6?style=for-the-badge&logoColor=white" alt="Download Deltempo.exe" height="42" />
    </a>
  </p>

  <p align="center">
    <strong>🌐 Official Landing Page & Interactive Simulator:</strong> <a href="https://beso1227.github.io/Deltempo/">https://beso1227.github.io/Deltempo/</a>
  </p>

</div>

---

## ⚡ Why Deltempo?

Windows and modern desktop software secretly hoard tens of gigabytes of disposable cache in hidden subdirectories under `AppData`, game launcher temporary chunks, GPU shader pools, video render scratch disks, leftover uninstalled software directories, stale driver installation packages, and post-upgrade system leftovers (`$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD`). Meanwhile, unvetted startup apps silently drag down boot times, and background processes eat up gigabytes of precious RAM.

**Deltempo** is engineered as a surgical precision, zero-bloat standalone PC optimization suite. It combines a deep scan cleaning engine across **25+ distinct scopes** (matching and exceeding Microsoft PC Manager's deep cleanup) with an **elite performance toolkit** (Authentic Windows NT Kernel Memory Cleaner, AI Large File Hunter with 100% Binary Safety Certainty, Startup Accelerator, Grouped Process Memory Optimizer), a **Luxury Obsidian System Tray Guardian**, a **100% synchronous CLI companion**, **Graphify AST Knowledge Graph integration**, **Obsidian Knowledge Vault**, and **G-Helper style zero-trash auto-updates** — all while leaving user logins, passwords, and personal files 100% untouched.

> ### 💡 Real-World Impact
> *"Without Deltempo my C: drive was suffocating at 64 GB free space. In 20 seconds, Deltempo purged **14.2 GB of disposable junk and Windows upgrade leftovers**, purged **4.2 GB of cached Standby RAM** via privileged NT kernel calls, and disabled 4 high-impact startup hogs with zero panic."*

---

## 🌟 Major Highlights & Architecture

```text
  ██████╗ ███████╗██╗  ████████╗███████╗███╗   ███╗██████╗  ██████╗ 
  ██╔══██╗██╔════╝██║  ╚══██╔══╝██╔════╝████╗ ████║██╔══██╗██╔═══██╗
  ██║  ██║█████╗  ██║     ██║   █████╗  ██╔████╔██║██████╔╝██║   ██║
  ██║  ██║██╔══╝  ██║     ██║   ██╔══╝  ██║╚██╔╝██║██╔═══╝ ██║   ██║
  ██████╔╝███████╗███████╗██║   ███████╗██║ ╚═╝ ██║██║     ╚██████╔╝
  ╚═════╝ ╚══════╝╚══════╝╚═╝   ╚══════╝╚═╝     ╚═╝╚═╝      ╚═════╝ 
       D E L T E M P O  —  Precision Windows Optimizer (v1.3.0)
```

### 🧠 1. Authentic Windows NT Kernel Memory Cleaner (WinMemoryCleaner & RAMMap Engine)
- **Privilege Escalation via Windows Token APIs**: Automatically requests and enables `SeProfileSingleProcessPrivilege` and `SeIncreaseQuotaPrivilege` using `OpenProcessToken` and `AdjustTokenPrivileges`, unlocking privileged kernel memory list manipulation.
- **Native NT System Information (0x50)**: Directly marshals `SystemMemoryListInformation` (decimal 80) commands to the Windows NT Kernel:
  - `MemoryPurgeStandbyList = 4`: Purges the complete Standby List (reclaims cached pages from closed apps).
  - `MemoryPurgeLowPriorityStandbyList = 5`: Gentle flush of lowest-priority standby pages with minimal cache disruption.
  - `MemoryFlushModifiedList = 3`: Writes dirty modified pages to disk before freeing them from RAM.
  - `MemoryEmptyWorkingSets = 2`: Empties system-wide working sets.
- **Windows System File Cache Reset**: Flushes and resets system working set file cache via `SetSystemFileCacheSize((IntPtr)(-1), (IntPtr)(-1), 0)`.
- **Direct Kernel Telemetry**: Real-time PSAPI performance counters via `GetPerformanceInfo` from `psapi.dll`, accurately reporting `SystemCache`, `CommitTotal`, and physical RAM byte values.
- **Dedicated 8-Zone Double-Bezel Modal (`MemoryModalOverlay`)**: 3-pod telemetry strip (Physical RAM, Reclaimable Standby Cache with Emerald glow, and Kernel Isolation Shield), per-zone safety badges, live cache size indicators, and individual surgical `[ ⚡ Flush ]` buttons.
- **Security Whitelist Immunity**: Critical Windows processes (`System`, `Registry`, `smss`, `csrss`, `services`, `lsass`, `svchost`, `dwm`, `explorer`, `Deltempo`, `Defender`) remain 100% immune from trimming.

### 🐘 2. AI Large Files Hunter with 100% Binary Safety Certainty
- **100% Binary Decision Model**: Completely eliminated ambiguous "Review Needed" classifications. Every single large file is deterministically categorized into:
  - `SAFE TO DELETE` (`#10B981` Emerald Glow): Driver installation extractors (NVIDIA, AMD), stale crash dumps, aborted downloads, package manager cache archives, and downloaded setup archives.
  - `PROTECTED` (`#EF4444` Crimson Badge): Active game assets (Steam, Epic, GOG, Riot), operating system libraries (`System32`, `Program Files`), AI model weights (`.bin`, `.safetensors`, `.onnx`), virtual machine disks, and personal media.
- **Plain-English Reasoning Capsules**: Sub-card explanation displaying exact file origin (`AiOrigin`) and direct safety impact (`AiExplanation`).
- **Safe Shell Recycling**: All deletions utilize Windows Shell `SHFileOperation` to send files to the **Recycle Bin with full Undo capability**.
- **1-Click Reveal in Explorer**: Instantly locate files in Windows File Explorer.

### 🎨 3. Anti-Slop Vector UI/UX & Concentric Double-Bezel Architecture
- **Strict Anti-Emoji Compliance**: Replaced all cartoon emojis (`🐘`, `🤖`, `🗑️`, `🟢`, `🔴`) with native Windows Segoe Fluent vector glyphs (`&#xE950;`, `&#xE8B7;`, `&#xE756;`, `&#xE714;`, `&#xF012;`, `&#xE7FC;`, `&#xE943;`, `&#xEDA2;`).
- **Symmetric Hero Dashboard**: Clean, balanced top section featuring Drive C: and RAM telemetry cards with high-contrast readouts, gradient progress bars, and instant action pills.
- **Machined Concentric Modals**: Double-bezel outer frames (`#232B3A` hairlines) with dark OLED acrylic core surfaces (`#0C0F14` / `#111620`).
- **Pillar 2 Utility Dock**: 1-click access to `[  Large Files ]`, `[  RAM Cleaner ]`, and `[  Startup ]`.

### 🚀 4. Advanced Deep Scan & System Cleanup Engine (25+ Scopes)
- **Windows Upgrade & Setup Leftovers**: Deep scans and cleans gigabytes of post-update residue including `C:\$WINDOWS.~BT`, `C:\$WINDOWS.~WS`, `C:\$WinREAgent\Scratch`, `C:\ESD`, and `C:\Windows.old` leftovers (typically saving **5 GB to 30+ GB** after Windows feature updates).
- **Windows Store & Modern UWP App Packages**: Scans and cleans `LocalCache`, `AC\INetCache`, `AC\Temp`, `TempState`, and `CrashDump` across all installed Windows Store/MSIX packages (e.g. New Microsoft Teams, WhatsApp Desktop, Spotify Store, Xbox App, Modern Outlook) while preserving credentials.
- **Windows Component, Font & Servicing Caches**: System `FontCache` (LocalService & NetworkService), `Downloaded Program Files`, `WinSxS\Temp`, `WinSxS\ManifestCache`, `SoftwareDistribution\ScanFile`, and `PeerDist` BranchCache.
- **Multi-Profile Web Browser Deep Cleaning**: Dynamically scans all user profiles across **Google Chrome, Microsoft Edge, Brave, Opera, Opera GX, Vivaldi, Arc, and Yandex** (`Cache_Data`, `Code Cache`, `GPUCache`, `DawnCache`, `ShaderCache`, `GrShaderCache`, `Service Worker Storage`, `Crashpad`, `blob_storage`). Includes Gecko profile scanning for **Mozilla Firefox, Floorp, Waterfox, LibreWolf, and Zen**.
- **Creator & Media Scratchpads**: **Adobe Premiere Pro**, **After Effects**, **Photoshop** AutoRecover/scratch, **CapCut PC** cache, **DaVinci Resolve** proxy cache, **OBS Studio** logs/crash dumps, **Blender** render temp, and **Audacity** session scratch.
- **Gaming Launchers & Platform Caches**: **Steam** (`downloading`, `shadercache`, `appcache\httpcache`), **Epic Games Launcher**, **Riot Games / Valorant / LoL**, **EA App / Origin**, **Battle.net / Blizzard**, **Ubisoft Connect**, **GOG Galaxy**, and **Roblox**.
- **Developer & Engine Caches**: **JetBrains IDEs**, **Go build cache** (`go-build`), **Rustup** downloads, **Cargo**, **NuGet v3**, **pip**, **npm** (Local & Roaming), **pnpm**, **Yarn**, **Bun**, **Deno**, **.NET SDK temp**, and **Gradle**.

### ⚡ 5. Startup Boot Accelerator & Reversible Registry Shield
- **Reversible Registry Backups**: Scans Windows Run keys and Startup shortcuts with boot impact ratings. Disabled entries are backed up into `Run_Deltempo_Disabled` registry keys for 100% reversible rollbacks.

### 🛡️ 6. Single-Instance Guardian & Launch Reliability
- **Headless Ghost Auto-Purge**: Automatically detects and terminates orphaned background instances (`MainWindowHandle == 0`) before startup, ensuring clean desktop initialization.
- **Native Administrator Manifest**: Embedded `<requestedExecutionLevel level="requireAdministrator" />` ensures all high-privilege kernel operations succeed without permission errors.

### 💻 7. Synchronous In-Place CLI Engine
- **Dual-Mode Architecture**: Standalone GUI binary (`Deltempo.exe`) paired with a native Console Subsystem companion (`deltempo_cli.exe`).
- **Clean In-Place Formatting**: Output renders directly within the active terminal session, with the shell prompt returning cleanly below.

### 🧠 8. Graphify AST Knowledge Graph & Obsidian Vault
- **AST Codebase Graph (`graphify-out/graph.json`)**: Architectural map with component nodes, dependencies, and cluster communities.
- **Interactive Force Graph (`graphify-out/graph.html`)**: Standalone 3D/2D visual graph for architectural exploration.
- **Obsidian Vault (`vault/`)**: Formatted markdown notes and native JSON Canvas 1.0 architecture maps.

### 🧪 9. 67 Automated Unit Tests & CI/CD
- **Full Test Suite (`Tests/Deltempo.Tests/`)**: 67 automated xUnit tests validating all 25+ cleanup tiers, memory snapshot engines, 8-zone target enumeration, mock providers, and audit report generation.
- **GitHub Actions CI (`.github/workflows/ci.yml`)**: Continuous automated testing and quality gates.

---

## 🥊 Feature Comparison Matrix

| Feature / Standard | 👑 Deltempo (v1.3.0) | Microsoft PC Manager | CCleaner (Avast) | BleachBit | Windows Disk Cleanup |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Price & License** | **100% Free / MIT** | Free | Proprietary / Ads / $29.95 | GPLv3 Free | Built-in System |
| **Packaging** | **Single Standalone EXE** | Heavy Store App | Heavy Installer + Bloat | Multi-file ZIP | System Built-in |
| **Telemetry & Tracking** | **0% (Pure Offline)** | Microsoft Telemetry | Active Tracking & Ads | Clean | Microsoft Telemetry |
| **NT Kernel Standby List Purging** | **✅ Yes (WinMemoryCleaner API)** | ❌ No | ❌ Paywalled | ❌ No | ❌ No |
| **System File Cache Reset** | **✅ Yes (SetSystemFileCacheSize)** | ❌ No | ❌ No | ❌ No | ❌ No |
| **AI Large File Hunter** | **✅ 100% Binary Certainty** | ⚠️ Basic | ❌ Paywalled | ❌ No | ❌ No |
| **Recycle Bin Safe Deletion (Undo)** | **✅ SHFileOperation Undo** | ❌ Permanent Deletion | ❌ Permanent | ❌ Permanent | ❌ Permanent |
| **Windows Upgrade Leftovers** | **✅ $WINDOWS.~BT, ESD, ~WS** | ⚠️ Basic | ❌ Paywalled | ❌ No | ⚠️ Partial |
| **Windows Store / UWP App Caches** | **✅ All Store App Packages** | ⚠️ Basic | ❌ Paywalled | ❌ No | ❌ No |
| **Multi-Profile Browser Cleaner** | **✅ Chrome, Edge, Brave, Arc, Firefox** | ⚠️ Basic | ⚠️ Partial | ⚠️ Basic | ❌ No |
| **Startup Boot Accelerator** | **✅ 100% Reversible** | ⚠️ Basic | ⚠️ Paywalled | ❌ No | ⚠️ Task Manager |
| **Grouped Process Optimizer** | **✅ 65+ System Whitelist** | ⚠️ Basic | ❌ No | ❌ No | ❌ No |
| **Luxury Tray Guardian** | **✅ Dark Glass + Live Telemetry** | ⚠️ Basic | ❌ Ad-heavy popup | ❌ No | ❌ No |
| **Device Driver Package Purge** | **✅ NVIDIA OTA (3.7+ GB), AMD, Intel** | ⚠️ Limited | ❌ No | ❌ No | ⚠️ Partial |
| **GPU Shader Cache Purge** | **✅ NVIDIA / AMD / Intel** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Gaming & Launcher Purge** | **✅ Steam, Epic, Riot, EA, Battle.net** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Creator Media Render Scratch** | **✅ Adobe, CapCut, DaVinci, OBS, Blender** | ❌ No | ❌ No | ❌ No | ❌ No |
| **In-Place Hot-Swap Updates** | **✅ G-Helper Style (Zero Trash)** | ⚠️ Store Dependent | ❌ Installer Popups | ❌ Manual | ⚠️ Windows Update |
| **Single-Instance Mutex Guard** | **✅ Yes (UIPI IPC Window Focus)** | ⚠️ Basic | ❌ No | ❌ No | N/A |
| **Headless Synchronous CLI** | **✅ Full CLI + JSON output** | ❌ No | ⚠️ Limited CLI | ⚠️ Basic CLI | ⚠️ Legacy cleanmgr |
| **Automated xUnit CI/CD** | **✅ 67 Unit Tests + Actions** | ❌ Proprietary | ❌ Proprietary | ⚠️ Basic | ❌ Proprietary |

---

## 🧹 Complete 25-Tier Deep Cleaning Scope

1. **User Temp & Scratchpad**: Application cache, setup extractions, downloaded installers (`%TEMP%`).
2. **Windows System Temp**: OS diagnostic traces, system update scratchpad (`C:\Windows\Temp`).
3. **Windows Prefetch Cache**: Stale execution traces & cached startup headers (`C:\Windows\Prefetch`).
4. **Windows Update Delivery**: Cached installation packages (`SoftwareDistribution\Download`).
5. **Windows Upgrade & Setup Leftovers**: Post-upgrade remnants, `$WINDOWS.~BT`, `$WINDOWS.~WS`, `$WinREAgent\Scratch`, `ESD`, and `Windows.old`.
6. **Windows Delivery Optimization (WUDO)**: P2P Windows update delivery chunks (`DeliveryOptimization\Cache`).
7. **Windows Component & Font Caches**: Windows `FontCache`, `Downloaded Program Files`, `WinSxS\Temp`, `WinSxS\ManifestCache`, `SoftwareDistribution\ScanFile`, and `BranchCache`.
8. **Device Driver Packages & GPU Updates**: NVIDIA App OTA installer artifacts (`ota-artifacts` hoards 3+ GB!), AMD, Intel, and `DriverStore\Temp`.
9. **Microsoft Defender Support & Scans**: Defender diagnostic support logs (`MPLog-*.log`), definition update backups, and old scan cache.
10. **Windows System Diagnostic Logs**: Comprehensive servicing traces across `CBS`, `DISM`, `DPX`, `Panther`, `SetupAPI`, `WindowsUpdate`, `GPO`, and `LogFiles` (`WMI`/`HTTPERR`).
11. **BSOD Minidumps & Kernel Reports**: Crash minidumps (`*.dmp`), `MEMORY.DMP`, and `LiveKernelReports`.
12. **Temporary Internet Files & WebCache**: Windows `INetCache`, `WebCache`, `Caches`, and `CryptnetUrlCache` (SSL certificate content).
13. **DirectX & GPU Shader Pools**: Binary shader caches from **NVIDIA** (`DXCache`, `GLCache`), **AMD** (`DxCache`), and **Intel** (`D3DSCache`).
14. **Game Launchers & Shaders**: Steam download chunks (`Steam\downloading`), shader caches, and web caches in **Epic Games**, **Riot Games / Valorant / LoL**, **Battle.net**, **EA Desktop**, **Ubisoft Connect**, **GOG Galaxy**, and **Roblox**.
15. **Media & Creator Render Scratch**: **Adobe Premiere Pro**, **After Effects**, **Photoshop** AutoRecover, **CapCut PC**, **DaVinci Resolve** proxy cache scratch, **OBS Studio** crash logs, **Blender** temp renders, and **Audacity** session data.
16. **Desktop & Electron App Caches**: Disposable cache folders across **Discord**, **WhatsApp Desktop**, **Telegram Desktop**, **Spotify**, **Slack**, **VS Code**, **Cursor**, **Windsurf**, **Teams**, and **Notion** (logins & tokens preserved).
17. **Windows Store Apps & Modern UWP Caches**: `LocalCache`, `AC\INetCache`, `AC\Temp`, and `TempState` across all Windows Store / MSIX app packages (New Teams, Xbox App, etc.).
18. **Web Browser Caches (Multi-Profile Engine)**: Temporary media and HTTP cache pools for **Chrome**, **Edge**, **Brave**, **Opera**, **Opera GX**, **Vivaldi**, **Arc**, and **Yandex** across all profiles, plus **Firefox, Floorp, Waterfox, LibreWolf, and Zen**.
19. **Developer & Package Caches**: Package manager temporary directories (`pip`, `npm` local & roaming, `yarn`, `pnpm`, `.gradle`, `.cache`, `nuget\v3-cache`, `Go build cache`, `Rustup`, `Bun`, `Deno`, `.NET temp`).
20. **Mobile Sync & Dev Daemons**: **Apple iTunes** backup temp files, **Android Studio** emulator caches, **Gradle** build daemons, and **Rust Cargo** cache archives.
21. **Windows Error Reporting (WER)**: Diagnostic queues and crash archives (`ReportArchive`, `ReportQueue`).
22. **Explorer Thumbnail Caches**: Consolidated thumbnail databases (`thumbcache_*.db`).
23. **System & Explorer Usage Traces**: Recent items shortcuts (`*.lnk`), `AutomaticDestinations`, and `CustomDestinations` Jump Lists.
24. **Native Windows Recycle Bin**: Recycles items across all connected physical drives via shell API (`SHEmptyRecycleBinW`).
25. **Verified Orphaned App Leftovers**: Detects leftover `AppData` folders from uninstalled applications cross-referenced with the Windows Uninstall Registry.

---

## 💻 Command-Line Interface (CLI Automation)

Deltempo provides a fast, synchronous CLI engine with clear tabular formatting, color-coded status badges, and structured JSON output:

```powershell
# 1. Quick dry-run scan (or: deltempo scan --json)
deltempo scan

# 2. Clean safe temporary caches, upgrade leftovers and GPU shaders
deltempo clean --safe

# 3. ⚡ Instant 1-click RAM working set purge & standby list flush
deltempo boost

# 4. 🐘 AI Large File Hunter (>500MB discovery across drives)
deltempo large --min 500MB

# 5. View live system telemetry (Drive storage & RAM usage)
deltempo status

# 6. Terminate orphaned background/headless Deltempo instances
deltempo kill

# 7. Check for updates on GitHub Releases
deltempo update
```

---

## 🛠️ Building & Running Tests Locally

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Run xUnit Unit Test Suite (67 tests)
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

# Publish Standalone GUI Binary (Deltempo.exe)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

---

## 📜 License & Acknowledgments

- **License**: Released under the ultra-permissive **[MIT License](LICENSE)**.
- **Author**: **[Beso1227](https://github.com/Beso1227)**
- **Architecture**: Dual-Subsystem WPF Desktop + Native Console Engine with Graphify AST Knowledge Graph & Obsidian Vault integration.
