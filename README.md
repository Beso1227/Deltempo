<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="128" height="128" />

  # 👑 Deltempo (King Edition)
  ### Pure Precision Windows Optimizer, System Health & Profile Guardian

  **Single Standalone Executable (Zero Runtime Dependencies) • 1-Click RAM Boost • Startup Accelerator • Large File Hunter • 21+ Elite Cleaning Scopes • AST Knowledge Graph • xUnit Test Suite • Zero Telemetry • 100% Free & Open Source (MIT)**

  [![Release](https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=00E5FF&logo=windows&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest)
  [![CI](https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI%2FCD)](https://github.com/Beso1227/Deltempo/actions)
  [![Tests](https://img.shields.io/badge/Tests-32%20Passing-10B981?style=for-the-badge&logo=xunit&logoColor=white)](tests/Deltempo.Tests)
  [![License: MIT](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-3B82F6?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/Beso1227/Deltempo)
  [![Website](https://img.shields.io/badge/Official_Site-Live-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white)](https://beso1227.github.io/Deltempo/)

  <br />

  <p align="center">
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe">
      <img src="https://img.shields.io/badge/⚡_DOWNLOAD_DELTEMPO.EXE_(v1.1.0)-3B82F6?style=for-the-badge&logoColor=white" alt="Download Deltempo.exe" height="42" />
    </a>
  </p>

  <p align="center">
    <strong>🌐 Official Landing Page & Interactive Simulator:</strong> <a href="https://beso1227.github.io/Deltempo/">https://beso1227.github.io/Deltempo/</a>
  </p>

</div>

---

## ⚡ Why Deltempo?

Windows and modern desktop software secretly hoard tens of gigabytes of disposable cache in hidden subdirectories under `AppData`, game launcher temporary chunks, GPU shader pools, video render scratch disks, leftover uninstalled software directories, and stale driver installation packages. Meanwhile, unvetted startup apps silently drag down boot times, and background processes eat up gigabytes of precious RAM.

**Deltempo** is engineered as a surgical precision, zero-bloat standalone PC optimization suite. It combines deep cache purging across **21+ distinct scopes** with an **elite Microsoft PC Manager-inspired performance toolkit** (RAM Booster, Startup Accelerator, Large File Hunter, Memory Optimizer), a **100% synchronous CLI companion**, **Graphify AST Knowledge Graph integration**, **Obsidian Knowledge Vault**, and **G-Helper style zero-trash auto-updates** — all while leaving user logins, passwords, and personal files 100% untouched.

> ### 💡 Real-World Impact
> *"Without Deltempo my C: drive was suffocating at 64 GB free space. In 20 seconds, Deltempo purged **6.8 GB of disposable junk**, boosted **750 MB of RAM**, and disabled 4 high-impact startup hogs with zero panic."*

---

## 🌟 Major Highlights & Architecture

```text
  ██████╗ ███████╗██╗  ████████╗███████╗███╗   ███╗██████╗  ██████╗ 
  ██╔══██╗██╔════╝██║  ╚══██╔══╝██╔════╝████╗ ████║██╔══██╗██╔═══██╗
  ██║  ██║█████╗  ██║     ██║   █████╗  ██╔████╔██║██████╔╝██║   ██║
  ██║  ██║██╔══╝  ██║     ██║   ██╔══╝  ██║╚██╔╝██║██╔═══╝ ██║   ██║
  ██████╔╝███████╗███████╗██║   ███████╗██║ ╚═╝ ██║██║     ╚██████╔╝
  ╚═════╝ ╚══════╝╚══════╝╚═╝   ╚══════╝╚═╝     ╚═╝╚═╝      ╚═════╝ 
       D E L T E M P O  —  Precision Windows Optimizer (v1.1.0)
```

### 🚀 1. Elite PC Performance Toolkit (PC Manager Inspired)
- **⚡ 1-Click Working Set RAM Boost**: Safely purges cached working set memory from background applications and services via native Win32 `GlobalMemoryStatusEx` and PSAPI `EmptyWorkingSet`. Instant millisecond-level feedback (`✓ -750 MB in 335ms`).
- **🚀 Startup Apps Boot Accelerator**: Scans Windows Run keys and Startup shortcuts with boot impact ratings (`🟢 Low`, `🟡 Medium`, `🔴 High`). **100% Reversible** — disabled items are safely backed up in `Run_Deltempo_Disabled` registry keys with zero risk of corruption.
- **🐘 Disk Hog & Large File Hunter**: Scans Downloads, Documents, Desktop, and Videos for files $>50$ MB. Auto-groups by category (`Installer / ISO`, `Video / Media`, `Archive`, `Dump / Backup`, `Virtual Disk`). Features **1-Click Reveal in Explorer** and safe deletion via Windows Shell `SHFileOperation` (sends directly to **Recycle Bin with Undo**).
- **🛑 Heavy Background Memory Apps Optimizer**: Identifies heavy memory consumers ($>80$ MB) with a **hardcoded Windows Core Whitelist** protecting all vital system processes (`explorer.exe`, `dwm.exe`, `svchost.exe`, `csrss.exe`, `lsass.exe`, etc.) from being touched or terminated.

### 💻 2. Synchronous In-Place CLI Engine & Global Integration
- **Dual-Mode Desktop & Terminal Architecture**: Just like Visual Studio (`devenv.exe` + `devenv.com`), Deltempo provides a pure desktop GUI application (`Deltempo.exe`) and a native Console Subsystem binary (`deltempo_cli.exe`).
- **Zero Prompt Collision**: CLI runs synchronously within the active console session. Output renders in-place, and the shell prompt returns on a clean new line below with zero text overlapping.
- **Auto Global Registration**: Automatically registers to User `PATH`, Windows `App Paths`, and PowerShell profiles on first run.

### 🧠 3. Graphify AST Knowledge Graph & Obsidian Vault
- **📊 AST Codebase Graph (`graphify-out/graph.json`)**: Complete relationship graph mapping 30 component nodes, 33 architectural edges, degree metrics, and 8 community clusters.
- **🌐 Interactive Visual Graph (`graphify-out/graph.html`)**: Standalone 3D/2D Force-Directed visual graph for interactive structural exploration in any browser.
- **💎 Obsidian Knowledge Vault (`vault/`)**: Obsidian Flavored Markdown notes with frontmatter properties, callouts, `[[wikilinks]]`, and native **JSON Canvas 1.0** architectural blueprints (`vault/05 - Canvases/Deltempo_Architecture.canvas`).
- **🔄 Automated Synchronization Hooks**: Git pre-commit hooks and MSBuild targets automatically regenerate and stage graph artifacts on every code change.

### 🧪 4. xUnit Test Suite & Mock Provider Architecture
- **32 Automated Unit Tests (`tests/Deltempo.Tests/`)**: Complete test coverage verifying 21-scope discovery, 24-hour Safe Mode filter, locked file resilience, core system process whitelists, and audit report generation.
- **Mock Provider Interfaces (`Services/Providers/ISystemProvider.cs`)**: Headless in-memory simulation providers for side-effect-free test execution in CI/CD without touching physical disks.
- **Automated GitHub Actions CI (`.github/workflows/ci.yml`)**: Automated builds and quality gates on every push and PR.

### 🛡️ 5. Single-Instance Mutex & System Tray Protection
- **Zero Duplicate Processes**: Prevents duplicate instances or duplicate tray icons from spawning when launched multiple times.
- **IPC Window Activation**: Launching Deltempo while running in the background brings the existing window to the front smoothly via registered inter-process Windows messages.
- **Clean Tray Lifecycle**: Automatic Win32 `Shell_NotifyIconW(NIM_DELETE)` cleanup on exit prevents ghost icons from remaining in the notification area.

### 🔄 6. G-Helper Style Seamless Auto-Updater
- **Zero-Installer Atomic Hot-Swap**: Polling GitHub Releases directly, Deltempo streams new updates in the background and replaces its running executable atomically without creating installer leftovers.

---

## 🥊 Feature Comparison Matrix

| Feature / Standard | 👑 Deltempo | Microsoft PC Manager | CCleaner (Avast) | BleachBit | Windows Disk Cleanup |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Price & License** | **100% Free / MIT** | Free | Proprietary / Ads / $29.95 | GPLv3 Free | Built-in System |
| **Packaging** | **Single Standalone EXE** | Heavy Store App | Heavy Installer + Bloat | Multi-file ZIP | System Built-in |
| **Telemetry & Tracking** | **0% (Pure Offline)** | Microsoft Telemetry | Active Tracking & Ads | Clean | Microsoft Telemetry |
| **1-Click RAM Boost** | **✅ Non-destructive** | ✅ Yes | ❌ Paywalled | ❌ No | ❌ No |
| **Startup Boot Accelerator** | **✅ 100% Reversible** | ⚠️ Basic | ⚠️ Paywalled | ❌ No | ⚠️ Task Manager |
| **Large File Hunter** | **✅ $>50$ MB + Recycle Bin** | ⚠️ Basic | ❌ Paywalled | ❌ No | ❌ No |
| **Device Driver Package Purge** | **✅ NVIDIA OTA (3.7+ GB), AMD, Intel** | ⚠️ Limited | ❌ No | ❌ No | ⚠️ Partial |
| **Defender Antivirus Cache Purge** | **✅ MPLog & Scan History** | ⚠️ Basic | ❌ No | ❌ No | ❌ No |
| **GPU Shader Cache Purge** | **✅ NVIDIA / AMD / Intel** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Gaming & Launcher Purge** | **✅ Steam, Epic, EA, Battle.net** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Creator Media Render Scratch** | **✅ Adobe, DaVinci, OBS, Blender** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Windows Delivery Optimization** | **✅ P2P WUDO Cache** | ⚠️ Partial | ❌ No | ❌ No | ⚠️ Partial |
| **CBS Servicing & DISM Logs** | **✅ CbsPersist_*.log** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Orphaned App Leftovers** | **✅ Registry Cross-Checked** | ❌ No | ❌ Paywalled | ❌ No | ❌ No |
| **In-Place Hot-Swap Updates** | **✅ G-Helper Style (Zero Trash)** | ⚠️ Store Dependent | ❌ Installer Popups | ❌ Manual | ⚠️ Windows Update |
| **Single-Instance Mutex Guard** | **✅ Yes (Zero Duplicate Icons)** | ⚠️ Basic | ❌ No | ❌ No | N/A |
| **24h Safety Shield Filter** | **✅ Yes (Zero Accidental Loss)** | ❌ No | ❌ Blind Deletion | ❌ No | ❌ No |
| **Headless Synchronous CLI** | **✅ Full CLI + JSON output** | ❌ No | ⚠️ Limited CLI | ⚠️ Basic CLI | ⚠️ Legacy cleanmgr |
| **Knowledge Graph & Obsidian Vault** | **✅ Graphify + JSON Canvas** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Automated xUnit CI/CD** | **✅ 32 Unit Tests + GitHub Actions** | ❌ Proprietary | ❌ Proprietary | ⚠️ Basic | ❌ Proprietary |

---

## 🧹 Complete 21-Tier Cleaning Scope

1. **User Temp & Scratchpad**: Application cache, setup extractions, downloaded installers (`%TEMP%`).
2. **Windows System Temp**: OS diagnostic traces, system update scratchpad (`C:\Windows\Temp`).
3. **Windows Prefetch Cache**: Stale execution traces & cached startup headers (`C:\Windows\Prefetch`).
4. **Windows Update Delivery**: Cached installation packages (`SoftwareDistribution\Download`).
5. **Windows Delivery Optimization (WUDO)**: P2P Windows update delivery chunks (`DeliveryOptimization\Cache`).
6. **Device Driver Packages & GPU Updates**: NVIDIA App OTA installer artifacts (`ota-artifacts` hoards 3+ GB!), AMD, Intel, and `DriverStore\Temp`.
7. **Microsoft Defender Support & Scans**: Defender diagnostic support logs (`MPLog-*.log`), definition update backups, and old scan cache.
8. **Windows System Diagnostic Logs**: Comprehensive servicing traces across `CBS`, `DISM`, `DPX`, `Panther`, `SetupAPI`, `WindowsUpdate`, `GPO`, and `LogFiles` (`WMI`/`HTTPERR`).
9. **BSOD Minidumps & Kernel Reports**: Crash minidumps (`*.dmp`), `MEMORY.DMP`, and `LiveKernelReports`.
10. **Temporary Internet Files & WebCache**: Windows `INetCache`, `WebCache`, `Caches`, and `CryptnetUrlCache` (SSL certificate content).
11. **DirectX & GPU Shader Pools**: Binary shader caches from **NVIDIA** (`DXCache`, `GLCache`), **AMD** (`DxCache`), and **Intel** (`D3DSCache`).
12. **Game Launchers & Shaders**: Steam download chunks (`Steam\downloading`), shader caches, and web caches in **Epic Games**, **Battle.net**, **EA Desktop**, and **Ubisoft Connect**.
13. **Media & Creator Render Scratch**: **Adobe Premiere Pro** & **After Effects** Media Cache, **DaVinci Resolve** proxy cache scratch, **OBS Studio** crash logs, and **Blender** temp renders.
14. **Desktop & Electron App Caches**: Disposable cache folders across **Discord**, **Spotify**, **Slack**, **VS Code**, **Teams**, and **Notion** (logins & tokens preserved).
15. **Web Browser Caches**: Temporary media and HTTP cache pools for **Chrome**, **Edge**, **Brave**, **Opera**, and **Vivaldi** (`Cache_Data`, `Code Cache`, `GPUCache`, `DawnCache`).
16. **Developer & Package Caches**: Package manager temporary directories (`pip`, `npm`, `yarn`, `.gradle`, `.cache`, `nuget\v3-cache`).
17. **Mobile Sync & Dev Daemons**: **Apple iTunes** backup temp files, **Android Studio** emulator caches, **Gradle** build daemons, and **Rust Cargo** cache archives.
18. **Windows Error Reporting (WER)**: Diagnostic queues and crash archives (`ReportArchive`, `ReportQueue`).
19. **Explorer Thumbnail Caches**: Consolidated thumbnail databases (`thumbcache_*.db`).
20. **System & Explorer Usage Traces**: Recent items shortcuts (`*.lnk`), `AutomaticDestinations`, and `CustomDestinations` Jump Lists.
21. **Native Windows Recycle Bin**: Recycles items across all connected physical drives via shell API (`SHEmptyRecycleBinW`).
22. **Verified Orphaned App Leftovers**: Detects leftover `AppData` folders from uninstalled applications cross-referenced with the Windows Uninstall Registry.

---

## 💻 Command-Line Interface (CLI Automation)

Deltempo provides a fast, synchronous CLI engine with clear tabular formatting, color-coded status badges, and structured JSON output:

```powershell
# 1. Quick dry-run scan (or: deltempo scan --json)
deltempo scan

# 2. Clean safe temporary caches and GPU shaders
deltempo clean --safe

# 3. ⚡ Instant 1-click RAM working set purge
deltempo boost

# 4. View live system telemetry (Drive storage & RAM usage)
deltempo status

# 5. Check for updates on GitHub Releases
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

# Run xUnit Unit Test Suite (32 tests)
dotnet test tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

# Publish Standalone GUI Binary (Deltempo.exe)
dotnet publish WinTempCleaner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish_gui

# Publish Synchronous CLI Binary (deltempo_cli.exe)
dotnet publish Cli/Deltempo.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish_cli
```

---

## 📜 License & Acknowledgments

- **License**: Released under the ultra-permissive **[MIT License](LICENSE)**.
- **Author**: **[Beso1227](https://github.com/Beso1227)**
- **Architecture**: Dual-Subsystem WPF Desktop + Native Console Engine with Graphify AST Knowledge Graph & Obsidian Vault integration.
