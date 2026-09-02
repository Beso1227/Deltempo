<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="128" height="128" />

  # 👑 Deltempo (King Edition)
  ### Pure Precision Windows Optimizer, System Health & Profile Guardian

  **Single Standalone Executable (Zero Runtime Dependencies) • 1-Click RAM Boost • Startup Accelerator • Large File Hunter • Multi-Process Memory Optimizer • 25+ Elite Deep Cleaning Scopes • Multi-Profile Browser Engine • AST Knowledge Graph • 45 xUnit Tests • Zero Telemetry • 100% Free & Open Source (MIT)**

  [![Release](https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=00E5FF&logo=windows&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest)
  [![CI](https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI%2FCD)](https://github.com/Beso1227/Deltempo/actions)
  [![Tests](https://img.shields.io/badge/Tests-45%20Passing-10B981?style=for-the-badge&logo=xunit&logoColor=white)](Tests/Deltempo.Tests)
  [![License: MIT](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-3B82F6?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/Beso1227/Deltempo)
  [![Website](https://img.shields.io/badge/Official_Site-Live-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white)](https://beso1227.github.io/Deltempo/)

  <br />

  <p align="center">
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe">
      <img src="https://img.shields.io/badge/⚡_DOWNLOAD_DELTEMPO.EXE_(v1.2.0)-3B82F6?style=for-the-badge&logoColor=white" alt="Download Deltempo.exe" height="42" />
    </a>
  </p>

  <p align="center">
    <strong>🌐 Official Landing Page & Interactive Simulator:</strong> <a href="https://beso1227.github.io/Deltempo/">https://beso1227.github.io/Deltempo/</a>
  </p>

</div>

---

## ⚡ Why Deltempo?

Windows and modern desktop software secretly hoard tens of gigabytes of disposable cache in hidden subdirectories under `AppData`, game launcher temporary chunks, GPU shader pools, video render scratch disks, leftover uninstalled software directories, stale driver installation packages, and post-upgrade system leftovers (`$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD`). Meanwhile, unvetted startup apps silently drag down boot times, and background processes eat up gigabytes of precious RAM.

**Deltempo** is engineered as a surgical precision, zero-bloat standalone PC optimization suite. It combines a deep scan cleaning engine across **25+ distinct scopes** (matching and exceeding Microsoft PC Manager's deep cleanup) with an **elite performance toolkit** (RAM Booster, Startup Accelerator, Large File Hunter, Grouped Process Memory Optimizer), a **Luxury Obsidian System Tray Guardian**, a **100% synchronous CLI companion**, **Graphify AST Knowledge Graph integration**, **Obsidian Knowledge Vault**, and **G-Helper style zero-trash auto-updates** — all while leaving user logins, passwords, and personal files 100% untouched.

> ### 💡 Real-World Impact
> *"Without Deltempo my C: drive was suffocating at 64 GB free space. In 20 seconds, Deltempo purged **14.2 GB of disposable junk and Windows upgrade leftovers**, boosted **750 MB of RAM**, and disabled 4 high-impact startup hogs with zero panic."*

---

## 🌟 Major Highlights & Architecture

```text
  ██████╗ ███████╗██╗  ████████╗███████╗███╗   ███╗██████╗  ██████╗ 
  ██╔══██╗██╔════╝██║  ╚══██╔══╝██╔════╝████╗ ████║██╔══██╗██╔═══██╗
  ██║  ██║█████╗  ██║     ██║   █████╗  ██╔████╔██║██████╔╝██║   ██║
  ██║  ██║██╔══╝  ██║     ██║   ██╔══╝  ██║╚██╔╝██║██╔═══╝ ██║   ██║
  ██████╔╝███████╗███████╗██║   ███████╗██║ ╚═╝ ██║██║     ╚██████╔╝
  ╚═════╝ ╚══════╝╚══════╝╚═╝   ╚══════╝╚═╝     ╚═╝╚═╝      ╚═════╝ 
       D E L T E M P O  —  Precision Windows Optimizer (v1.2.0)
```

### 🚀 1. Advanced Deep Scan & System Cleanup Engine (Surpassing Microsoft PC Manager)
- **🗑️ Windows Upgrade & Setup Leftovers**: Deep scans and cleans gigabytes of post-update residue including `C:\$WINDOWS.~BT`, `C:\$WINDOWS.~WS`, `C:\$WinREAgent\Scratch`, `C:\ESD`, and `C:\Windows.old` leftovers (typically saving **5 GB to 30+ GB** after Windows 10/11 feature updates).
- **📦 Windows Store & Modern UWP App Packages**: Scans and cleans `LocalCache`, `AC\INetCache`, `AC\Temp`, `TempState`, and `CrashDump` across all installed Windows Store/MSIX packages (e.g. New Microsoft Teams `MSTeams_8wekyb3d8bbwe`, WhatsApp Desktop, Spotify Store, Xbox App, Modern Outlook, Microsoft News) while strictly preserving user credentials and settings.
- **⚙️ Windows Component, Font & Servicing Caches**: System `FontCache` (LocalService & NetworkService), `Downloaded Program Files`, `WinSxS\Temp`, `WinSxS\ManifestCache`, `SoftwareDistribution\ScanFile`, and `PeerDist` BranchCache.
- **🌐 Multi-Profile Web Browser Deep Cleaning**: Dynamically scans all user profiles (`Default`, `Profile 1..N`, `Guest Profile`, `System Profile`) across **Google Chrome, Microsoft Edge, Brave, Opera, Opera GX, Vivaldi, Arc, and Yandex**. Purges complete sub-cache suites (`Cache_Data`, `Code Cache`, `GPUCache`, `DawnCache`, `ShaderCache`, `GrShaderCache`, `Service Worker Storage`, `Crashpad`, `blob_storage`). Includes Gecko profile scanning for **Mozilla Firefox, Floorp, Waterfox, LibreWolf, and Zen**.
- **🎨 Creator & Media Scratchpads**: **Adobe Premiere Pro**, **After Effects**, **Photoshop** AutoRecover/scratch, **CapCut PC** cache, **DaVinci Resolve** proxy cache, **OBS Studio** logs/crash dumps, **Blender** render temp, and **Audacity** session scratch.
- **🎮 Gaming Launchers & Platform Caches**: **Steam** (`downloading`, `shadercache`, `appcache\httpcache`), **Epic Games Launcher**, **Riot Games / Valorant / LoL**, **EA App / Origin**, **Battle.net / Blizzard**, **Ubisoft Connect**, **GOG Galaxy**, and **Roblox**.
- **💻 Developer & Engine Caches**: **JetBrains IDEs**, **Go build cache** (`go-build`), **Rustup** downloads, **Cargo**, **NuGet v3**, **pip**, **npm** (Local & Roaming), **pnpm**, **Yarn**, **Bun**, **Deno**, **.NET SDK temp**, and **Gradle**.

### ⚡ 2. Elite PC Performance Toolkit
- **⚡ 1-Click Working Set RAM Boost**: Safely purges cached working set memory from background applications and services via native Win32 `GlobalMemoryStatusEx` and PSAPI `EmptyWorkingSet`. Instant millisecond-level feedback (`✓ -750 MB in 335ms`).
- **🚀 Startup Apps Boot Accelerator**: Scans Windows Run keys and Startup shortcuts with boot impact ratings (`🟢 Low`, `🟡 Medium`, `🔴 High`). **100% Reversible** — disabled items are safely backed up in `Run_Deltempo_Disabled` registry keys with auto-rollback on permission failures.
- **🐘 Disk Hog & Large File Hunter**: Scans Downloads, Documents, Desktop, and Videos for files $>50$ MB. Auto-groups by category (`Installer / ISO`, `Video / Media`, `Archive`, `Dump / Backup`, `Virtual Disk`). Features **1-Click Reveal in Explorer** and safe deletion via Windows Shell `SHFileOperation` (sends directly to **Recycle Bin with Undo**).
- **🛑 Grouped Process Memory Optimizer**: Intelligently groups multi-process apps (e.g. 20 `chrome.exe` PIDs or 6 `discord.exe` instances) into consolidated application entries with aggregate memory and batch trimming/termination. Fully protected by a **65+ Windows Core & Kernel Whitelist** (`Memory Compression`, `MsMpEng`, `NisSrv`, `Registry`, `vmmem`, `SearchIndexer`, `RuntimeBroker`, `svchost`, etc.).

### 🎯 3. Scope-Aware Cleaning Engine & Attribute Normalization
- **Targeted Safety Shield**: Protects active setup extractions in raw scratchpads (`%TEMP%` and `C:\Windows\Temp`) while allowing 100% of pure cache scopes (`BrowserCaches`, `GpuShaderCaches`, `AppCacheSweeper`, `WUDO`, `DeviceDriverPackages`, `WinUpgradeLeftovers`, etc.) to be purged directly.
- **Attribute Stripping**: Clears `ReadOnly`, `Hidden`, and `System` flags prior to deletion, eliminating silent deletion failures.

### 🎨 4. Elevated Desktop UI/UX Architecture
- **Segmented Performance Island**: Clean connected toolbar for `🚀 Startup`, `🐘 Large Files`, and `🛑 Processes`.
- **Unified Command Strip**: Structured separation between category filters and batch selection actions.
- **Floating Preference Pod**: Glass capsule grouping Language selector (English, Arabic, Spanish, French, German), Theme toggle 🌙, Sound FX 🔊, Settings ⚙️, and Administrator status badge.

### 🛡️ 5. Luxury Obsidian System Tray Guardian & UIPI IPC
- **💎 Dark Obsidian Glass Menu**: Tailored `#0C1017` dark acrylic aesthetic with 12px rounded corners, 24px soft drop shadow, icon badges (`#141B28`), and live telemetry header card displaying real-time RAM pressure and gradient progress indicators.
- **🛡️ UIPI-Protected Single-Instance IPC**: Automatically bypasses Windows User Interface Privilege Isolation (`ChangeWindowMessageFilter`) to bring existing background/minimized instances to the foreground smoothly with zero duplicate processes or ghost tray icons.
- **🤖 Autonomous Auto-Pilot Guardian**: Background timer silently cleans disposable caches on user-defined schedules (e.g. every 12 hours) with subtle desktop notifications.

### 💻 6. Synchronous In-Place CLI Engine & Global Integration
- **Dual-Mode Desktop & Terminal Architecture**: Just like Visual Studio (`devenv.exe` + `devenv.com`), Deltempo provides a pure desktop GUI application (`Deltempo.exe`) and a native Console Subsystem binary (`deltempo_cli.exe`).
- **Zero Prompt Collision**: CLI runs synchronously within the active console session. Output renders in-place, and the shell prompt returns on a clean new line below with zero text overlapping.
- **Auto Global Registration**: Automatically registers to User `PATH`, Windows `App Paths`, and PowerShell profiles on first run.

### 🧠 7. Graphify AST Knowledge Graph & Obsidian Vault
- **📊 AST Codebase Graph (`graphify-out/graph.json`)**: Complete relationship graph mapping component nodes, architectural edges, degree metrics, and community clusters.
- **🌐 Interactive Visual Graph (`graphify-out/graph.html`)**: Standalone 3D/2D Force-Directed visual graph for interactive structural exploration in any browser.
- **💎 Obsidian Knowledge Vault (`vault/`)**: Obsidian Flavored Markdown notes with frontmatter properties, callouts, `[[wikilinks]]`, and native **JSON Canvas 1.0** architectural blueprints (`vault/05 - Canvases/Deltempo_Architecture.canvas`).

### 🧪 8. xUnit Test Suite & Mock Provider Architecture
- **45 Automated Unit Tests (`Tests/Deltempo.Tests/`)**: Complete test coverage verifying 25-scope discovery, multi-profile browser discovery, scope-aware 24h Safe Mode filter, pure cache purges, locked file resilience, memory metrics, startup enumerators, core system process whitelists, and audit report generation.
- **Mock Provider Interfaces (`Services/Providers/ISystemProvider.cs`)**: Headless in-memory simulation providers for side-effect-free test execution in CI/CD without touching physical disks.
- **Automated GitHub Actions CI (`.github/workflows/ci.yml`)**: Automated builds and quality gates on every push and PR.

### 🔄 9. G-Helper Style Seamless Auto-Updater
- **Zero-Installer Atomic Hot-Swap**: Polling GitHub Releases directly, Deltempo streams new updates in the background and replaces its running executable atomically without creating installer leftovers.

---

## 🥊 Feature Comparison Matrix

| Feature / Standard | 👑 Deltempo | Microsoft PC Manager | CCleaner (Avast) | BleachBit | Windows Disk Cleanup |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Price & License** | **100% Free / MIT** | Free | Proprietary / Ads / $29.95 | GPLv3 Free | Built-in System |
| **Packaging** | **Single Standalone EXE** | Heavy Store App | Heavy Installer + Bloat | Multi-file ZIP | System Built-in |
| **Telemetry & Tracking** | **0% (Pure Offline)** | Microsoft Telemetry | Active Tracking & Ads | Clean | Microsoft Telemetry |
| **Windows Upgrade Leftovers** | **✅ $WINDOWS.~BT, ESD, ~WS** | ⚠️ Basic | ❌ Paywalled | ❌ No | ⚠️ Partial |
| **Windows Store / UWP App Caches** | **✅ All Store App Packages** | ⚠️ Basic | ❌ Paywalled | ❌ No | ❌ No |
| **Multi-Profile Browser Cleaner** | **✅ Chrome, Edge, Brave, Arc, Opera, Firefox** | ⚠️ Basic | ⚠️ Partial | ⚠️ Basic | ❌ No |
| **1-Click RAM Boost** | **✅ Non-destructive** | ✅ Yes | ❌ Paywalled | ❌ No | ❌ No |
| **Startup Boot Accelerator** | **✅ 100% Reversible** | ⚠️ Basic | ⚠️ Paywalled | ❌ No | ⚠️ Task Manager |
| **Large File Hunter** | **✅ $>50$ MB + Recycle Bin** | ⚠️ Basic | ❌ Paywalled | ❌ No | ❌ No |
| **Grouped Process Optimizer** | **✅ 65+ System Whitelist** | ⚠️ Basic | ❌ No | ❌ No | ❌ No |
| **Luxury Tray Guardian** | **✅ Dark Glass + Live Telemetry** | ⚠️ Basic | ❌ Ad-heavy popup | ❌ No | ❌ No |
| **Device Driver Package Purge** | **✅ NVIDIA OTA (3.7+ GB), AMD, Intel** | ⚠️ Limited | ❌ No | ❌ No | ⚠️ Partial |
| **Defender Antivirus Cache Purge** | **✅ MPLog & Scan History** | ⚠️ Basic | ❌ No | ❌ No | ❌ No |
| **GPU Shader Cache Purge** | **✅ NVIDIA / AMD / Intel** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Gaming & Launcher Purge** | **✅ Steam, Epic, Riot, EA, Battle.net, Roblox** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Creator Media Render Scratch** | **✅ Adobe, CapCut, DaVinci, OBS, Blender** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Windows Delivery Optimization** | **✅ P2P WUDO Cache** | ⚠️ Partial | ❌ No | ❌ No | ⚠️ Partial |
| **CBS Servicing & DISM Logs** | **✅ CbsPersist_*.log** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Orphaned App Leftovers** | **✅ Registry Cross-Checked** | ❌ No | ❌ Paywalled | ❌ No | ❌ No |
| **In-Place Hot-Swap Updates** | **✅ G-Helper Style (Zero Trash)** | ⚠️ Store Dependent | ❌ Installer Popups | ❌ Manual | ⚠️ Windows Update |
| **Single-Instance Mutex Guard** | **✅ Yes (UIPI IPC Window Focus)** | ⚠️ Basic | ❌ No | ❌ No | N/A |
| **24h Safety Shield Filter** | **✅ Scope-Aware (Zero Accidental Loss)** | ❌ No | ❌ Blind Deletion | ❌ No | ❌ No |
| **Headless Synchronous CLI** | **✅ Full CLI + JSON output** | ❌ No | ⚠️ Limited CLI | ⚠️ Basic CLI | ⚠️ Legacy cleanmgr |
| **Knowledge Graph & Obsidian Vault** | **✅ Graphify + JSON Canvas** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Automated xUnit CI/CD** | **✅ 45 Unit Tests + GitHub Actions** | ❌ Proprietary | ❌ Proprietary | ⚠️ Basic | ❌ Proprietary |

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

# Run xUnit Unit Test Suite (45 tests)
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

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
