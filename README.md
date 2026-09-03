<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="128" height="128" />

  # Deltempo
  ### Open Source Windows Cleaner and Memory Optimizer

  **Portable Executable • NT Kernel Memory Cleaner • Large File Hunter • Startup Manager • 25 Cleanup Targets • Browser Cache Cleaner • Zero Telemetry • MIT Licensed**

  [![Release](https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=00E5FF&logo=windows&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest)
  [![CI](https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI%2FCD)](https://github.com/Beso1227/Deltempo/actions)
  [![Tests](https://img.shields.io/badge/Tests-79%20Passing-10B981?style=for-the-badge&logo=xunit&logoColor=white)](Tests/Deltempo.Tests)
  [![License: MIT](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-3B82F6?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/Beso1227/Deltempo)
  [![Website](https://img.shields.io/badge/Official_Site-Live-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white)](https://beso1227.github.io/Deltempo/)

  <br />

  <p align="center">
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe">
      <img src="https://img.shields.io/badge/DOWNLOAD_DELTEMPO.EXE_(v1.3.0)-3B82F6?style=for-the-badge&logoColor=white" alt="Download Deltempo.exe" height="42" />
    </a>
  </p>

  <p align="center">
    <strong>Web Documentation:</strong> <a href="https://beso1227.github.io/Deltempo/">https://beso1227.github.io/Deltempo/</a>
  </p>

</div>

---

## Overview

Windows and installed applications write gigabytes of disposable data to your disk: cache folders in `AppData`, old driver installers, GPU shader caches, game updates, and upgrade remnants (`$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD`). Startup entries slow down boot times, and closed programs leave memory parked in standby lists.

Deltempo cleans these files across 25 targets and reclaims memory. It preserves your browser passwords, active sessions, and personal files. It runs as a single portable executable with both a graphical desktop interface and a console tool.

---

## Features

### NT Kernel Memory Cleaner
Integrates the memory management engine from WinMemoryCleaner (by Igor Mundstein).
- **Working set flush**: Flushes process working sets across Windows in a single kernel call with `SeProfileSingleProcessPrivilege`, followed by per-process trims with `SeDebugPrivilege`.
- **Standby list purge**: Purges cached pages left behind by closed applications (`NtSetSystemInformation` class 80).
- **System file cache reset**: Flushes and resets filesystem cache with `SetSystemFileCacheSize` and `SystemFileCacheInformation`.
- **Modified page flush**: Writes dirty modified pages to disk before freeing them from RAM.
- **Combined page list**: Triggers page combining to deduplicate identical memory pages.
- **Disk buffer flush**: Opens raw volume handles (`\\.\C:`, `\\.\D:`) without buffering to discard cached volume blocks.
- **Process immunity list**: Protects system processes from memory trimming, including `csrss`, `dwm`, `explorer`, `lsass`, `services`, `smss`, `svchost`, and Windows Defender.

### Storage Cleanup (25 Targets)
- **Windows system caches**: User temp, system temp, prefetch files, component store temp files, font caches, and update delivery caches.
- **Windows upgrade residue**: Removes post-update files like `$WINDOWS.~BT`, `$WINDOWS.~WS`, `$WinREAgent\Scratch`, and `ESD`.
- **Browser caches**: Cleans disk and shader caches for Chrome, Edge, Brave, Opera, Vivaldi, Arc, and Firefox profiles.
- **Display driver packages**: Cleans installer archives from NVIDIA (including OTA packages), AMD, and Intel.
- **Game launchers**: Cleans download chunks and shader caches for Steam, Epic Games, Riot, Battle.net, and EA Desktop.
- **Media and render scratch**: Cleans temporary render files from Adobe Premiere, Photoshop, DaVinci Resolve, CapCut, OBS Studio, and Blender.
- **Developer caches**: Cleans package manager caches for npm, yarn, pnpm, pip, NuGet, Go, Rust Cargo, and .NET temp files.

### Large File Finder
- Finds files larger than 50 MB on any drive or custom path.
- Inspects files and flags them as safe to remove (driver installers, old crash dumps, temporary archives) or protected (game assets, system files, machine learning models).
- Sends files to the Windows Recycle Bin with undo support via `SHFileOperation`.

### Startup Manager
- Scans Windows Run keys and startup folders.
- Displays boot impact ratings for each program.
- Disables startup entries with automatic registry backup to `Run_Deltempo_Disabled` for simple reversal.

### Command-Line Interface
- Runs synchronously from your terminal (`deltempo_cli.exe` or `deltempo`).
- Supports scanning, cleaning, dry runs, RAM boosting, startup toggling, and large file inspection.
- Formats output as tables or JSON.

---

## Comparison

| Feature | Deltempo | Microsoft PC Manager | CCleaner | BleachBit | Windows Disk Cleanup |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **License** | **Free / MIT** | Free | Proprietary / Ads | Free / GPLv3 | Built into Windows |
| **Format** | **Portable EXE** | Store package | Installer with ads | Zip archive | System utility |
| **Telemetry** | **None** | Microsoft telemetry | User tracking | None | Microsoft telemetry |
| **NT Kernel Standby Flush** | **Yes** | No | Paid version only | No | No |
| **System File Cache Reset** | **Yes** | No | No | No | No |
| **Large File Finder** | **Yes** | Basic | Paid version only | No | No |
| **Recycle Bin Undo** | **Yes** | Permanent delete | Permanent delete | Permanent delete | Permanent delete |
| **Windows Upgrade Files** | **Yes** | Basic | Paid version only | No | Partial |
| **Driver Package Cleanup** | **Yes** | Limited | No | No | Partial |
| **Shader Cache Cleanup** | **Yes** | No | No | No | No |
| **Gaming Platform Caches** | **Yes** | No | No | No | No |
| **Media Render Scratch** | **Yes** | No | No | No | No |
| **CLI Automation** | **Yes** | No | Limited | Basic | Legacy flags |
| **Unit Tests** | **79 tests** | Proprietary | Proprietary | Basic | Proprietary |

---

## Cleaning Targets

1. **User Temp**: Temporary files from `%TEMP%`.
2. **System Temp**: OS logs and temporary update files from `C:\Windows\Temp`.
3. **Prefetch**: Execution headers from `C:\Windows\Prefetch`.
4. **Update Delivery**: Downloaded update files from `SoftwareDistribution\Download`.
5. **Windows Upgrade Residue**: Leftover folders from updates (`$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD`, `Windows.old`).
6. **Delivery Optimization (WUDO)**: Peer update cache from `DeliveryOptimization\Cache`.
7. **Component and Font Caches**: Font caches, `WinSxS\Temp`, and `SoftwareDistribution\ScanFile`.
8. **Driver Packages**: NVIDIA App OTA installer artifacts, AMD, and Intel temp folders.
9. **Microsoft Defender Logs**: Support logs and update backup caches.
10. **System Diagnostic Logs**: CBS, DISM, SetupAPI, and Windows Update log files.
11. **Crash Dumps**: Kernel crash dumps (`*.dmp`, `MEMORY.DMP`) and LiveKernelReports.
12. **Internet Files**: Windows `INetCache` and `CryptnetUrlCache`.
13. **GPU Shaders**: DirectX and OpenGL shader caches for NVIDIA, AMD, and Intel.
14. **Game Launchers**: Download chunks and caches for Steam, Epic Games, Riot, Battle.net, and EA.
15. **Media Render Scratch**: Cache folders for Adobe Premiere, Photoshop, DaVinci Resolve, CapCut, OBS, and Blender.
16. **Desktop App Caches**: Cache directories for Discord, Telegram, Spotify, Slack, VS Code, and Teams.
17. **Store App Packages**: Temporary cache directories for modern Windows Store applications.
18. **Web Browsers**: Cache folders for Chrome, Edge, Brave, Opera, Vivaldi, Arc, and Firefox.
19. **Package Managers**: Caches for pip, npm, yarn, pnpm, NuGet, Go, Cargo, and Bun.
20. **Development Daemons**: Android Studio emulator caches, Gradle daemons, and iTunes backups.
21. **Error Reporting**: Windows Error Reporting archives and queues.
22. **Explorer Thumbnails**: Thumbnail database files (`thumbcache_*.db`).
23. **Recent Item Shortcuts**: Recent file links and Jump Lists.
24. **Recycle Bin**: Empties the Windows Recycle Bin across all drives.
25. **Orphaned App Leftovers**: Leftover AppData folders from uninstalled software.

---

## Command-Line Usage

```powershell
# Scan all categories
deltempo scan

# Clean temporary files, upgrade residue, and shader caches
deltempo clean --safe

# Test run without deleting files
deltempo clean --safe --dry-run

# Boost RAM using 7 active memory zones
deltempo boost

# Deep purge across all 8 NT kernel memory zones
deltempo boost --all

# Find files over 500 MB
deltempo large --min 500MB

# View storage and RAM status
deltempo status

# List startup programs
deltempo startup

# Disable a startup program
deltempo startup disable "Cortana"
```

---

## Building and Testing

### Requirements
- Windows 10 or 11 (64-bit)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Commands
```powershell
# Clone the repository
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Run tests
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

# Build standalone executables
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

---

## License

Released under the [MIT License](LICENSE).  
Maintained by [Beso1227](https://github.com/Beso1227).
