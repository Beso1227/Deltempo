<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="120" height="120" style="border-radius: 24px; box-shadow: 0 10px 30px rgba(6, 182, 212, 0.35);" />

  <h1>Deltempo</h1>

  <p><strong>Your Windows PC, lighter and faster than the day you bought it.</strong></p>
  <p>The open-source, zero-bloat disk cleaner and NT kernel memory optimizer for Windows 10 &amp; 11.</p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><img src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=06B6D4&logo=windows&logoColor=white" alt="Release v1.3.0" /></a>
    <a href="https://github.com/Beso1227/Deltempo/actions"><img src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=Build" alt="CI Status" /></a>
    <a href="Tests/Deltempo.Tests"><img src="https://img.shields.io/badge/Tests-126%20Passing-10B981?style=for-the-badge&logo=xunit&logoColor=white" alt="126 xUnit Tests Passing" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white" alt="License MIT" /></a>
    <a href="https://beso1227.github.io/Deltempo/"><img src="https://img.shields.io/badge/Official_Site-Live_Web-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white" alt="Official Website" /></a>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe">
      <img src="https://img.shields.io/badge/⚡_DOWNLOAD_DELTEMPO.EXE_(v1.3.0)-3B82F6?style=for-the-badge&logoColor=white" alt="Download Standalone Deltempo.exe" height="42" />
    </a>
  </p>

  <p>
    <a href="#-quick-start">Quick Start</a> •
    <a href="#-why-deltempo">Why Deltempo?</a> •
    <a href="#-features-at-a-glance">Features</a> •
    <a href="#-cleaning-targets">26 Cleaning Scopes</a> •
    <a href="#-terminal-cli">CLI Commands</a> •
    <a href="#-comparison">Comparison</a> •
    <a href="https://beso1227.github.io/Deltempo/">Web Simulator ↗</a>
  </p>

</div>

---

## 🌟 Why Deltempo?

Over time, Windows collects gigabytes of forgotten clutter: old graphics driver packages, DirectX shader caches, unfinished game downloads, and massive Windows upgrade leftovers (`$WINDOWS.~BT`, `ESD`). Simultaneously, closed programs leave unpurged memory cached in system standby lists, causing random micro-stutters when gaming or multitasking.

Most cleaners are bloated, full of ads, or paywall the features you actually need. **Deltempo was built to be different**:

<table>
  <tr>
    <td width="50%">
      <h3>⚡ 100% Standalone &amp; Portable</h3>
      <p>Single <strong>64 MB</strong> executable. No installer, no background telemetry, no registry junk, and no administrator reboot required. Just download and run.</p>
    </td>
    <td width="50%">
      <h3>🧠 Smart NT Kernel Memory Boost</h3>
      <p>Integrates the proven engine from <strong>WinMemoryCleaner</strong> to flush standby lists and working sets safely without closing your active tabs or games.</p>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <h3>🎯 26 Deep Cleanup Targets</h3>
      <p>Cleans gigabytes of NVIDIA App OTA packages, GPU shader caches, messaging & social app caches (WhatsApp, Telegram, Teams, Discord), and upgrade residue that standard cleaners completely miss.</p>
    </td>
    <td width="50%">
      <h3>🛡️ 24-Hour Safety Shield &amp; Recycle Bin Undo</h3>
      <p>Automatically protects files modified in the last 24h. File deletions can optionally route to the <strong>Windows Recycle Bin</strong> for instant undo, while active messaging and store logins remain 100% protected.</p>
    </td>
  </tr>
</table>

---

## 🚀 Quick Start

You can use Deltempo in three intuitive ways:

### 1. The Desktop App (Default)
Just double-click **`Deltempo.exe`**! You'll get a clean Windows 11 Fluent interface:
- **Scan & Clean**: Choose from 25 categorized cleaning targets with one click.
- **Live Memory Meter**: Monitor RAM usage and flush standby memory on demand.
- **Large Files Explorer**: Identify space hogs with safe vs. protected badges.
- **Startup Accelerator**: See startup boot impact and toggle entries with full registry rollback.

### 2. The Command Line (CLI)
Launching `Deltempo.exe` once automatically registers the `deltempo` command across PowerShell, CMD, and Windows Terminal:

```powershell
# Quick dry run to see reclaimable space
deltempo scan

# Safe cleanup (preserves recent files and credentials)
deltempo clean --safe

# Instant 1-click RAM boost across process working sets
deltempo boost

# Find large files (>500 MB) with safety verdicts
deltempo large --min 500MB
```

### 3. The System Tray Guardian
Minimize Deltempo to run unobtrusively in your system notification area. Right-click the tray icon anytime for instant 1-click cleaning, RAM optimization, or quick status telemetry.

---

## ✨ Features at a Glance

### 🧠 Dual-Engine NT Kernel Memory Cleaner
Built with low-level Win32 and Windows NT kernel APIs, bringing the best memory optimization techniques into a modern workflow:
- **Standby List Purging**: Clears cached memory left behind by terminated apps via `NtSetSystemInformation` (`SystemMemoryListInformation` class 80).
- **Process Working Sets Flush**: Reclaims inactive RAM across all user applications via `SeProfileSingleProcessPrivilege` and `SeDebugPrivilege`.
- **System File Cache Reset**: Flushes and resets OS filesystem cache boundaries with `SetSystemFileCacheSize`.
- **Modified Pages Flush**: Writes dirty pages to disk before freeing RAM.
- **Combined Page List**: Deduplicates identical physical memory blocks across running applications.
- **Process Immunity Protection**: System-critical processes (`csrss`, `dwm`, `explorer`, `lsass`, `services`, `smss`, `svchost`, and Windows Defender) are automatically shielded.

---

### 📦 Large File Hunter (With Recycle Bin Undo!)
Running out of storage? Deltempo scans all connected drives for space-wasting files over 50 MB:
- **Safety Heuristics**: Intelligently identifies disposable files (driver installers, setup extracts, post-mortem crash dumps) while shielding protected assets (game archives, `.pak` files, machine learning weights).
- **Recycle Bin Undo**: Files are sent to the Windows Recycle Bin using native shell APIs (`SHFileOperation`), meaning you can restore anything with Ctrl+Z or right-click Restore.

---

### 🚀 Reversible Startup Accelerator
Tired of slow boot times? Deltempo scans your startup programs and calculates real boot delay impact:
- **Impact Ratings**: Highlights high-impact launchers (Discord Canary, Spotify, Steam, Epic Games).
- **100% Reversible**: Rather than deleting registry keys, Deltempo moves disabled entries to `Run_Deltempo_Disabled`. You can re-enable any app instantly with a single toggle.

---

## 🎯 26 Cleaning Targets

Deltempo inspects 26 specialized cleaning scopes across your system. Your personal documents, browser passwords, and active login sessions are **never** touched.

<details open>
<summary><strong>🪟 1. Windows System &amp; Upgrades (12 Scopes)</strong></summary>
<br />

| Scope | What It Cleans | Typical Savings |
| :--- | :--- | :---: |
| **User Temporary Files** | `%TEMP%` application scratch files and installer extracts | 1 – 5 GB |
| **Windows System Temp** | `C:\Windows\Temp` OS servicing logs and update staging | 500 MB – 2 GB |
| **Windows Prefetch Cache** | `C:\Windows\Prefetch` execution headers for uninstalled apps | 50 – 200 MB |
| **Update Delivery Packages** | `SoftwareDistribution\Download` superseded update files | 2 – 10 GB |
| **Windows Upgrade Residue** | `$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD` post-upgrade archives | 5 – 30+ GB |
| **Delivery Optimization (WUDO)** | `NetworkService\...\DeliveryOptimization\Cache` P2P update chunks | 2 – 8 GB |
| **Component &amp; Font Caches** | `WinSxS\Temp`, font caches, downloaded program files | 300 MB – 1 GB |
| **Microsoft Defender Logs** | `ProgramData\Microsoft\Windows Defender\Support` MPLogs | 200 MB – 1 GB |
| **System Diagnostic Logs** | CBS servicing logs, DISM component logs, SetupAPI traces | 500 MB – 3 GB |
| **Crash Dumps &amp; BSOD** | Kernel crash dumps (`*.dmp`, `MEMORY.DMP`), LiveKernelReports | 1 – 15 GB |
| **Explorer Thumbnails &amp; Usage** | `thumbcache_*.db`, Jump Lists, and recent file histories | 200 – 800 MB |
| **Windows Recycle Bin** | Empties `$Recycle.Bin` across all mounted physical drives | Varies |

</details>

<details>
<summary><strong>🚗 2. Display Drivers &amp; Hardware (2 Scopes)</strong></summary>
<br />

| Scope | What It Cleans | Typical Savings |
| :--- | :--- | :---: |
| **NVIDIA App OTA Packages** | `AppData\Local\NVIDIA Corporation\ota-artifacts` leftover driver bundles | 3 – 10 GB |
| **AMD &amp; Intel Driver Temp** | `DriverStore\Temp`, `C:\AMD\Temp`, `C:\Intel\Logs` package extractors | 1 – 4 GB |

</details>

<details>
<summary><strong>🎮 3. Gaming &amp; GPU Shaders (2 Scopes)</strong></summary>
<br />

| Scope | What It Cleans | Typical Savings |
| :--- | :--- | :---: |
| **Game Launchers &amp; Chunks** | Steam downloading chunks, Epic Games webcache, Battle.net, Riot | 2 – 15 GB |
| **DirectX &amp; GPU Shader Pools** | DirectX DXCache, Vulkan GLCache, Intel D3DSCache (fixes micro-stuttering) | 1 – 6 GB |

</details>

<details>
<summary><strong>🎬 4. Media &amp; Creator Scratch (1 Scope)</strong></summary>
<br />

| Scope | What It Cleans | Typical Savings |
| :--- | :--- | :---: |
| **Render &amp; Preview Scratch** | Adobe Premiere/AE Media Cache, DaVinci Resolve proxy previews, OBS, Blender | 5 – 40+ GB |

</details>

<details>
<summary><strong>💬 5. Communication, Desktop Apps &amp; Developer Tools (6 Scopes)</strong></summary>
<br />

| Scope | What It Cleans | Typical Savings |
| :--- | :--- | :---: |
| **Messaging &amp; Social Apps** | WhatsApp, Telegram, Teams, Discord, Slack, Signal, Skype, Zoom media/code caches (100% Login &amp; Session Protected) | 1 – 6 GB |
| **Desktop &amp; Electron Apps** | Spotify, Notion, VS Code, Cursor, Windsurf, JetBrains IDE caches | 1 – 5 GB |
| **Windows Store / UWP Apps** | Safe MSIX cache state across Store apps, WebView2 engine caches | 500 MB – 3 GB |
| **Package Manager Caches** | npm, pip, yarn, pnpm, NuGet v3, Cargo, and Go build caches | 2 – 15 GB |
| **Development Daemons** | Android Studio emulator cache, Gradle daemons, iTunes sync temp | 2 – 8 GB |
| **Orphaned AppData Leftovers** | Remnant folders from uninstalled apps verified against Registry | 500 MB – 4 GB |

</details>

<details>
<summary><strong>🌐 6. Multi-Profile Browsers &amp; Web (3 Scopes)</strong></summary>
<br />

| Scope | What It Cleans | Typical Savings |
| :--- | :--- | :---: |
| **Chromium Browser Profiles** | Google Chrome, Edge, Brave, Opera, Vivaldi, Arc disk &amp; code caches | 1 – 8 GB |
| **Gecko Firefox Profiles** | Mozilla Firefox, Floorp, Waterfox, LibreWolf, Zen cache directories | 500 MB – 4 GB |
| **Temporary Internet Files** | Windows `INetCache` and `CryptnetUrlCache` SSL revocation caches | 200 MB – 1 GB |

</details>

---

## 💻 Terminal CLI in Action

The terminal CLI is synchronous, fast, and supports JSON output for scripting and automation.

```powershell
# Scan with categorized summary
deltempo scan

# 1-Click Smart Clean (100% safe disposable caches only)
deltempo smart-clean

# Safe cleaning with locked-file handling and optional Recycle Bin routing
deltempo clean --safe --recycle-bin

# Autonomous full-system deep cleanup: RAM, DISM, 26 scopes & VSS
deltempo deep-clean

# Flush process working sets and purge standby memory
deltempo boost

# Deep purge across all 8 NT kernel memory zones
deltempo boost --all

# Large files inspection with formatted table
deltempo large --min 500MB

# Output system telemetry in structured JSON
deltempo status --format json

# Manage startup programs
deltempo startup
deltempo startup disable "Cortana"
```

### Formatted CLI Output Example

```text
PS C:\> deltempo large --min 500MB
┌──────────┬──────────────────────────┬──────────────────────┬──────────────────────────────────┐
│ SIZE     │ SAFETY VERDICT           │ CATEGORY             │ FILE NAME                        │
├──────────┼──────────────────────────┼──────────────────────┼──────────────────────────────────┤
│ 25.4 GB  │ PROTECTED (Game Asset)   │ Game Asset / Pak     │ BendGame-WindowsNoEditor.pak     │
│  7.4 GB  │ SAFE TO DELETE           │ Game Shader Cache    │ Steam_Shader_Cache_chunk0.bin    │
│  6.8 GB  │ SAFE TO DELETE           │ Installer / ISO      │ NVIDIA_Driver_560.70_Extract.exe │
│  4.2 GB  │ SAFE TO DELETE           │ Crash / Dump         │ MEMORY.DMP                       │
│  4.0 GB  │ PROTECTED (Model Weight) │ Model Weights        │ weights.bin                      │
└──────────┴──────────────────────────┴──────────────────────┴──────────────────────────────────┘
✔ Scanned 47.8 GB • Identified 18.4 GB safe to recycle with full Undo support.
```

---

## 📊 Head-to-Head Comparison

| Feature / Standard | Deltempo (v1.3.0) | Microsoft PC Manager | CCleaner (Avast) | BleachBit | Windows Cleanmgr |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **License** | **Free &amp; Open Source (MIT)** | Free | Freemium / Adware | Free (GPLv3) | Built-in Windows |
| **Distribution** | **Single Portable EXE** | Store package | Heavy installer + upsells | Zip archive | Built-in |
| **Telemetry &amp; Tracking** | **Zero Telemetry (100% Offline)** | Microsoft telemetry | User tracking &amp; analytics | None | Diagnostic telemetry |
| **Standby List Purge** | **✅ Native NT Kernel (`NtSetSystemInformation`)** | ❌ No | ❌ Paid Pro version only | ❌ No | ❌ No |
| **Working Sets Trim** | **✅ Dual-Engine Win32 (`EmptyWorkingSet`)** | ✅ Supported | ❌ Paid Pro version only | ❌ No | ❌ No |
| **System File Cache Reset** | **✅ Native (`SetSystemFileCacheSize`)** | ❌ No | ❌ No | ❌ No | ❌ No |
| **Large File Hunter** | **✅ Safe vs. Protected Heuristics** | ⚠️ Basic | ❌ Paid Pro version only | ❌ No | ❌ No |
| **Undo / Restore Safety** | **✅ Windows Recycle Bin Undo (`SHFileOperation`)** | ❌ Permanent delete | ❌ Permanent delete | ❌ Permanent delete | ❌ Permanent delete |
| **1-Click Smart Clean** | **✅ 100% Safe Disposable Preset** | ⚠️ Basic | ❌ Paid Pro version only | ❌ No | ❌ No |
| **Login Session Protection** | **✅ WhatsApp, Telegram, Teams, Store &amp; Browsers** | ⚠️ Wipes LocalCache | ⚠️ Erases sessions | ⚠️ Erases sessions | ❌ No |
| **Proactive Low Disk Alerts** | **✅ System Tray Notification Sentinel** | ❌ No | ❌ Paid Pro version only | ❌ No | ❌ No |
| **Windows Upgrade Residue** | **✅ Deep Purge (`$WINDOWS.~BT`, `ESD`)** | ⚠️ Basic | ❌ Paid Pro version only | ❌ No | ⚠️ Partial |
| **GPU Shader Cache Purge** | **✅ DirectX DXCache + Vulkan GLCache** | ❌ No | ❌ No | ❌ No | ❌ No |
| **NVIDIA App Driver OTA** | **✅ Cleans 3–10 GB installer caches** | ⚠️ Partial | ❌ No | ❌ No | ⚠️ Partial |
| **Media &amp; Creator Scratch** | **✅ Adobe, DaVinci, OBS, Blender** | ❌ No | ❌ No | ❌ No | ❌ No |
| **CLI Automation** | **✅ Instant global registration** | ❌ No | ⚠️ Limited | ⚠️ Basic | ⚠️ Legacy switches |
| **Automated Test Coverage** | **✅ 126 xUnit Tests (100% Passing)** | ❌ Proprietary | ❌ Proprietary | ⚠️ Basic | ❌ Proprietary |

---

## 🔒 Security, Trust &amp; Privacy

- **Zero Telemetry**: Deltempo has no analytics, no phone-home servers, and no advertisements. It operates completely offline.
- **Safety First**: Your photos, documents, desktop files, browser passwords, and active login sessions are never touched.
- **Authenticode Verified**: Every release is built with automated SHA-256 hash manifests:

```powershell
# Verify executable signature in PowerShell
Get-AuthenticodeSignature Deltempo.exe
```

---

## 🛠️ Building from Source

### Prerequisites
- 64-bit Windows 10 or 11
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Commands
```powershell
# 1. Clone the repository
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# 2. Run the automated test suite
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

# 3. Build the single-file standalone executable
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

The resulting executable will be available at `./Deltempo.exe` along with its SHA-256 manifest.

---

## 🤝 Community &amp; Contributing

Contributions, feature requests, and bug reports are warmly welcomed!
- Found a bug or want a new cleaning scope? Open an [Issue](https://github.com/Beso1227/Deltempo/issues).
- Want to contribute code? Fork the repo, make your changes, and submit a [Pull Request](https://github.com/Beso1227/Deltempo/pulls). Please ensure all 79 tests pass:
  ```powershell
  dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj
  ```

If Deltempo saved you disk space and sped up your PC, please give the repo a ⭐ **Star** on GitHub &mdash; it helps more Windows users discover the project!

---

## 📜 License

Deltempo is open-source software licensed under the **[MIT License](LICENSE)**.  
Crafted and maintained with care by **[Beso1227](https://github.com/Beso1227)** and open-source contributors.
