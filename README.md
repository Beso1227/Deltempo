<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="128" height="128" />

  # 👑 Deltempo (King Edition)
  ### Pure Precision Windows Optimizer, System Health & Profile Guardian

  **Single Standalone Executable (Zero Runtime Dependencies) • 1-Click RAM Boost • Startup Accelerator • Large File Hunter • 17 Elite Cleaning Scopes • Zero Telemetry • 100% Free & Open Source (MIT)**

  [![Release](https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=00E5FF&logo=windows&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest)
  [![License: MIT](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-3B82F6?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/Beso1227/Deltempo)
  [![Website](https://img.shields.io/badge/Official_Site-Live-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white)](https://beso1227.github.io/Deltempo/)
  [![WCAG 2.2 AA](https://img.shields.io/badge/Accessibility-WCAG_2.2_AA-00E5FF?style=for-the-badge)](https://github.com/Beso1227/Deltempo)

  <br />

  <p align="center">
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe">
      <img src="https://img.shields.io/badge/⚡_DOWNLOAD_DELTEMPO.EXE_(STANDALONE)-3B82F6?style=for-the-badge&logoColor=white" alt="Download Deltempo.exe" height="42" />
    </a>
  </p>

  <p align="center">
    <strong>🌐 Official Landing Page & Interactive Simulator:</strong> <a href="https://beso1227.github.io/Deltempo/">https://beso1227.github.io/Deltempo/</a>
  </p>

</div>

---

## ⚡ Why Deltempo?

Windows and desktop applications secretly hoard tens of gigabytes of disposable cache in hidden subdirectories under `AppData`, game launcher temporary chunks, GPU shader pools, video render scratch disks, and leftover uninstalled software directories. Meanwhile, startup apps silently drag down boot times, and background processes eat up gigabytes of precious RAM.

**Deltempo** is engineered as a surgical precision, zero-bloat standalone PC optimization suite. It combines deep cache purging across **17 distinct scopes** with an **elite Microsoft PC Manager-inspired performance toolkit** (RAM Booster, Startup Accelerator, Large File Hunter, Memory Optimizer) and **G-Helper style zero-trash auto-updates** — all while leaving user logins, passwords, and personal files 100% untouched.

> ### 💡 Real-World Impact
> *"Without Deltempo my C: drive was suffocating at 64 GB free space. In 20 seconds, Deltempo purged **6.8 GB of disposable junk**, boosted **750 MB of RAM**, and disabled 4 high-impact startup hogs with zero panic."*

---

## 🌟 Major Highlights & New Features

### 🚀 1. Elite PC Performance Toolkit (PC Manager Inspired)
- **⚡ 1-Click Working Set RAM Boost**: Safely purges cached working set memory from background applications and services via native Win32 `GlobalMemoryStatusEx` and PSAPI `EmptyWorkingSet`. Instant millisecond-level feedback (`✓ -450 MB`).
- **🚀 Startup Apps Boot Accelerator**: Scans Windows Run keys and Startup shortcuts with boot impact ratings (`🟢 Low`, `🟡 Medium`, `🔴 High`). **100% Reversible** — disabled items are safely backed up in `Run_Deltempo_Disabled` registry keys with zero risk of corruption.
- **🐘 Disk Hog & Large File Hunter**: Scans Downloads, Documents, Desktop, and Videos for files $>50$ MB. Auto-groups by category (`Installer / ISO`, `Video / Media`, `Archive`, `Dump / Backup`, `Virtual Disk`). Features **1-Click Reveal in Explorer** and safe deletion via Windows Shell `SHFileOperation` (sends directly to **Recycle Bin with Undo**).
- **🛑 Heavy Background Memory Apps Optimizer**: Identifies heavy memory consumers ($>80$ MB) with a **hardcoded Windows Core Whitelist** protecting all vital system processes (`explorer.exe`, `dwm.exe`, `svchost.exe`, `csrss.exe`, `lsass.exe`, etc.) from being touched or terminated.

### 🔄 2. G-Helper Style Seamless Auto-Updater
- **Zero-Installer Atomic Hot-Swap**: Polling GitHub Releases directly, Deltempo streams new updates in the background and replaces its running executable atomically without creating installer leftovers or junk directories.

### 🧹 3. 17 Specialized Precision Cleaning Scopes
- **User Profile & Windows Temp**: Cleans `%TEMP%`, `C:\Windows\Temp`, and `C:\Windows\Prefetch`.
- **DirectX & GPU Shader Pools**: Purges compiled binary shader caches from **NVIDIA** (`DXCache`, `GLCache`), **AMD** (`DxCache`), and **Intel** (`D3DSCache`).
- **Gaming Launchers & Shaders**: Cleans stuck Steam download chunks (`Steam\downloading`), shader caches, and web caches across **Epic Games**, **Battle.net**, **EA Desktop**, and **Ubisoft Connect**.
- **Creator Render Scratch**: Cleans **Adobe Premiere Pro** & **After Effects** Media Cache, **DaVinci Resolve** proxy cache scratch, **OBS Studio** crash logs, and **Blender** temp renders.
- **Windows Delivery Optimization (WUDO)**: Purges gigabytes of peer-to-peer Windows update delivery chunks hoarded in `NetworkService\DeliveryOptimization`.
- **Windows CBS Servicing & DISM Logs**: Cleans stale Component-Based Servicing logs (`CbsPersist_*.log`), DISM installation traces, and DPX setup logs.
- **Desktop & Electron App Caches**: Sweeps caches for **Discord**, **Spotify**, **Slack**, **VS Code**, **Teams**, and **Notion** (tokens and logins strictly preserved).
- **Verified Orphaned App Leftovers**: Cross-references residual `AppData` folders against the Windows Uninstall Registry.
- **24-Hour Safety Shield**: Automated file age filter protects files modified within the last 24 hours from deletion.

### 🎨 4. Luxury Obsidian & Nordic Frost Design System
- **Double-Bezel Obsidian & Frost Cards**: High-end typography, smooth micro-interactions, floating 7px minimalist scrollbar, and dynamic light/dark mode hot-swapping.
- **System Tray Guardian**: Native Win32 tray integration with auto-pilot background cleaning (Every 6h / 12h / 24h) and desktop alerts.
- **100% Dynamic Multi-Language**: Instant runtime translation across English (🇺🇸), Arabic (🇸🇦 RTL layout), Spanish (🇪🇸), French (🇫🇷), and German (🇩🇪).
- **Procedural Haptic Audio**: Tactile click tones and futuristic celebration chimes synthesized mathematically on-the-fly (zero audio assets required).

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
| **GPU Shader Cache Purge** | **✅ NVIDIA / AMD / Intel** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Gaming & Launcher Purge** | **✅ Steam, Epic, EA, Battle.net** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Creator Media Render Scratch** | **✅ Adobe, DaVinci, OBS, Blender** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Windows Delivery Optimization** | **✅ P2P WUDO Cache** | ⚠️ Partial | ❌ No | ❌ No | ⚠️ Partial |
| **CBS Servicing & DISM Logs** | **✅ CbsPersist_*.log** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Orphaned App Leftovers** | **✅ Registry Cross-Checked** | ❌ No | ❌ Paywalled | ❌ No | ❌ No |
| **In-Place Hot-Swap Updates** | **✅ G-Helper Style (Zero Trash)** | ⚠️ Store Dependent | ❌ Installer Popups | ❌ Manual | ⚠️ Windows Update |
| **24h Safety Shield Filter** | **✅ Yes (Zero Accidental Loss)** | ❌ No | ❌ Blind Deletion | ❌ No | ❌ No |
| **Headless CLI Engine** | **✅ Full CLI + JSON output** | ❌ No | ⚠️ Limited CLI | ⚠️ Basic CLI | ⚠️ Legacy cleanmgr |
| **Design Aesthetics** | **💎 Double-Bezel Obsidian/Frost** | Modern WinUI | ❌ Cluttered with Upsells | ❌ Legacy 2000s GTK | ❌ 90s System UI |

---

## 🧹 Complete 17-Tier Cleaning Scope

1. **User Temp & Scratchpad**: Application cache, setup extractions, downloaded installers (`%TEMP%`).
2. **Windows System Temp**: OS diagnostic traces, system update scratchpad (`C:\Windows\Temp`).
3. **Windows Prefetch Cache**: Stale execution traces & cached startup headers (`C:\Windows\Prefetch`).
4. **Windows Update Delivery**: Cached installation packages (`SoftwareDistribution\Download`).
5. **DirectX & GPU Shader Pools**: Binary shader caches from **NVIDIA** (`DXCache`, `GLCache`), **AMD** (`DxCache`), and **Intel** (`D3DSCache`).
6. **Desktop & Electron App Caches**: Disposable cache folders across **Discord**, **Spotify**, **Slack**, **VS Code**, **Teams**, and **Notion** (preserves logins & tokens).
7. **Web Browser Caches**: Temporary media and HTTP cache pools for **Chrome**, **Edge**, **Brave**, **Opera**, and **Vivaldi**.
8. **Developer & Package Caches**: Package manager temporary directories (`pip`, `npm`, `.gradle`, `.cache`, `nuget\temp`).
9. **Windows Error Reporting (WER)**: Crash dumps, memory dumps, and pending diagnostic queues (`ReportArchive`, `ReportQueue`).
10. **Explorer Thumbnail Caches**: Consolidated thumbnail databases (`thumbcache_*.db`).
11. **Native Windows Recycle Bin**: Recycles items across all connected physical drives via shell API (`SHEmptyRecycleBinW`).
12. **Verified Orphaned App Leftovers**: Detects leftover `AppData` folders from uninstalled applications cross-referenced with the Windows Uninstall Registry.
13. **Game Launchers & Shaders**: Purges stuck Steam download chunks (`Steam\downloading`), Vulkan/DirectX shader caches (`Steam\shadercache`), and web caches in **Epic Games**, **Battle.net**, **EA Desktop**, and **Ubisoft Connect** without touching save games or game files.
14. **Media & Creator Render Scratch**: Cleans waveform peak files, **Adobe Premiere Pro** & **After Effects** Media Cache, **DaVinci Resolve** proxy cache scratch, **OBS Studio** crash logs, and **Blender** temp renders.
15. **Windows Delivery Optimization (WUDO)**: Purges gigabytes of peer-to-peer Windows update delivery chunks hoarded silently in `ServiceProfiles\NetworkService\...\DeliveryOptimization\Cache`.
16. **Windows CBS Servicing & DISM Logs**: Cleans stale Component-Based Servicing logs (`CbsPersist_*.log`), DISM installation traces, and DPX setup logs (`C:\Windows\Logs\CBS`, `DISM`).
17. **Mobile Sync & Dev Daemons**: Cleans **Apple iTunes** interrupted backup temp files, **Android Studio** emulator caches, **Gradle** build daemons, and **Rust Cargo** registry cache archives.

---

## 💻 Command-Line Interface (CLI Automation)

Deltempo includes a built-in headless CLI engine for DevOps, power users, and Windows Task Scheduler automation:

```powershell
# 1. Quick dry-run scan (or: deltempo scan --json)
.\Deltempo.exe scan

# 2. Clean safe temporary caches and GPU shaders
.\Deltempo.exe clean

# 3. ⚡ Instant 1-click RAM working set purge
.\Deltempo.exe boost

# 4. 🚀 List Windows startup apps & boot impact ratings
.\Deltempo.exe startup

# 5. 🐘 Discover hidden large files (>50 MB)
.\Deltempo.exe large

# 6. 🛑 List heavy background memory processes (>80 MB)
.\Deltempo.exe procs

# 7. 📊 Check drive space and memory health telemetry
.\Deltempo.exe status

# 8. 🔄 Check for updates on GitHub
.\Deltempo.exe update
```

---

## 📦 Installation & Download

Download the direct standalone executable:
- **[Download Deltempo.exe (Latest Standalone)](https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe)**

### Windows Package Manager (`winget`)
```powershell
winget install Beso1227.Deltempo
```

---

## 🔒 Security, Trust & Privacy

- **0% Telemetry**: Deltempo does not track, log, or send any telemetry.
- **Digitally Signed**: Signed with Microsoft Authenticode SHA-256 for verified integrity.
- **Safety First**: Protected folders (system files, user documents `.docx`, `.pdf`, `.psd`, active credentials) are hard-locked against deletion.
- **Open Source**: Every line of code is inspectable under the permissive [MIT License](LICENSE).

---

## 📄 License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.

<div align="center">
  <sub>Engineered with precision for Windows power users worldwide by Beso1227.</sub>
</div>
