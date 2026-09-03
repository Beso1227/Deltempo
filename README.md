# Deltempo

![Deltempo Logo](app_icon.png)

## Open Source Windows Cleaner and Memory Optimizer

Portable Executable • NT Kernel Memory Cleaner • Large File Hunter • Startup Manager • 25 Cleanup Targets • Browser Cache Cleaner • Zero Telemetry • MIT Licensed

[![Release](https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&color=00E5FF&logo=windows&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest)
[![CI](https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI%2FCD)](https://github.com/Beso1227/Deltempo/actions)
[![Tests](https://img.shields.io/badge/Tests-79%20Passing-10B981?style=for-the-badge&logo=xunit&logoColor=white)](Tests/Deltempo.Tests)
[![License: MIT](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-3B82F6?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/Beso1227/Deltempo)
[![Runtime](https://img.shields.io/badge/Framework-.NET%2010.0%20Standalone-7C3AED?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Website](https://img.shields.io/badge/Official_Site-Live-8B5CF6?style=for-the-badge&logo=googlechrome&logoColor=white)](https://beso1227.github.io/Deltempo/)

[![Download Deltempo](https://img.shields.io/badge/DOWNLOAD_DELTEMPO.EXE_(v1.3.0)-3B82F6?style=for-the-badge&logoColor=white)](https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe)

**Live Web Documentation & Simulator**: [https://beso1227.github.io/Deltempo/](https://beso1227.github.io/Deltempo/)

---

## Overview

Windows and modern desktop applications deposit gigabytes of unmonitored temporary data across your drives. Over time, stale driver packages, DirectX shader caches, stuck game launcher downloads, and post-update upgrade archives (`$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD`) accumulate silently. Simultaneously, terminated applications leave unpurged memory cached in NT kernel standby lists, leading to system micro-stutters and memory pressure.

**Deltempo** is an open-source, zero-telemetry utility designed to solve both problems cleanly:

1. **Deep Disk Cleanup**: Scans across 25 dedicated targets with an automated 24-Hour Safety Shield protecting recently modified files, active installers, and application login states.
2. **Kernel Memory Management**: Integrates the native NT memory management engine from WinMemoryCleaner (by Igor Mundstein) to flush process working sets, purge standby lists, reset system file caches, and deduplicate identical memory pages.
3. **Large File Hunter**: Analyzes files larger than 50 MB, categorizing them into safe-to-clean items vs. protected game archives, with Windows Recycle Bin undo support.
4. **Reversible Startup Accelerator**: Scans Windows Run keys and startup directories, analyzes boot impact, and provides non-destructive toggle controls backed by registry backups.
5. **Unified Standalone Binary**: Runs as a single, portable executable (`Deltempo.exe`) featuring a Windows 11 Fluent vector interface and instant global CLI command registration.

---

## Architecture and Core Capabilities

### 1. NT Kernel Memory Cleaner

Deltempo integrates low-level Win32 and NT kernel memory APIs to reclaim unreferenced memory without crashing running processes.

- **Process Working Sets Flush**: Acquires `SeProfileSingleProcessPrivilege` and `SeDebugPrivilege` to flush working sets across all user processes via `EmptyWorkingSet` and native kernel calls.
- **Standby List Purge**: Reclaims unreferenced cached pages via `NtSetSystemInformation` (`SystemMemoryListInformation` class 80).
- **System File Cache Reset**: Flushes and resets filesystem cache boundaries with `SetSystemFileCacheSize` and `SystemFileCacheInformation`.
- **Modified Page Flush**: Flushes dirty modified pages to storage before freeing their memory allocations.
- **Combined Page List**: Deduplicates identical physical memory blocks across processes.
- **Raw Volume Buffer Flush**: Opens unbuffered handles to drive volumes (`\\.\C:`, `\\.\D:`) to invalidate stale disk cache blocks.
- **Process Immunity Protection**: System-critical processes (`csrss`, `dwm`, `explorer`, `lsass`, `services`, `smss`, `svchost`, and Windows Defender) are automatically shielded from memory operations.

### 2. Deep Storage Cleanup (25 Scopes)

| Domain | Scope | Description & Target Paths | Safety Status |
| :--- | :--- | :--- | :---: |
| **Windows System** | User Temp | `%TEMP%` application scratch files and installer extracts | 🟢 Safe |
| **Windows System** | System Temp | `C:\Windows\Temp` OS servicing logs and update staging | 🟢 Safe |
| **Windows System** | Prefetch Cache | `C:\Windows\Prefetch` execution headers for uninstalled apps | 🟢 Safe |
| **Windows System** | Update Delivery | `SoftwareDistribution\Download` superseded update packages | 🟢 Safe |
| **Windows System** | Upgrade Residue | `$WINDOWS.~BT`, `$WINDOWS.~WS`, `ESD` post-upgrade files | 🟢 Safe |
| **Windows System** | Delivery Optimization | `NetworkService\...\DeliveryOptimization\Cache` P2P chunks | 🟢 Safe |
| **Windows System** | Component & Font Caches | `WinSxS\Temp`, font caches, downloaded program files | 🟢 Safe |
| **Windows System** | Microsoft Defender Logs | `ProgramData\Microsoft\Windows Defender\Support` MPLogs | 🟢 Safe |
| **Windows System** | Diagnostic Logs | CBS servicing logs, DISM component logs, SetupAPI traces | 🟢 Safe |
| **Windows System** | Crash Dumps | `*.dmp`, `MEMORY.DMP`, and LiveKernelReports | 🟢 Safe |
| **Windows System** | Explorer Thumbnails | `thumbcache_*.db`, Jump Lists, and recent link histories | 🟢 Safe |
| **Windows System** | Recycle Bin | Empties `$Recycle.Bin` across all mounted physical drives | 🟢 Safe |
| **Device Drivers** | NVIDIA App OTA Packages | `AppData\Local\NVIDIA Corporation\ota-artifacts` (3–10 GB) | 🟢 Safe |
| **Device Drivers** | AMD & Intel Driver Temp | `DriverStore\Temp`, `C:\AMD\Temp`, `C:\Intel\Logs` | 🟢 Safe |
| **Gaming & Shaders** | Game Launchers | Steam downloading chunks, Epic Games webcache, Battle.net | 🟢 Safe |
| **Gaming & Shaders** | GPU Shader Pools | DirectX DXCache, Vulkan GLCache, Intel D3DSCache | 🟢 Safe |
| **Media & Creator** | Render Scratch | Adobe Premiere/AE Media Cache, DaVinci Resolve, OBS, Blender | 🟢 Safe |
| **Desktop Apps** | Electron & App Caches | Discord, Spotify, Slack, Telegram, VS Code media caches | 🟢 Safe |
| **Desktop Apps** | Windows Store UWP | MSIX `LocalCache` across New Teams, WhatsApp, Xbox App | 🟢 Safe |
| **Developer Tools** | Package Manager Caches | npm, pip, yarn, pnpm, NuGet v3, Cargo, and Go build cache | 🟢 Safe |
| **Developer Tools** | Daemons & Emulators | Android Studio emulator cache, Gradle daemons, iTunes sync | 🟢 Safe |
| **Maintenance** | Orphaned AppData | Leftover folders from uninstalled apps verified via Registry | 🟢 Safe |
| **Browsers** | Chromium Profiles | Google Chrome, Microsoft Edge, Brave, Opera, Vivaldi, Arc | 🟢 Safe |
| **Browsers** | Gecko Profiles | Mozilla Firefox, Floorp, Waterfox, LibreWolf, Zen profiles | 🟢 Safe |
| **Browsers** | Internet Files | Windows `INetCache` and `CryptnetUrlCache` | 🟢 Safe |

### 3. Large File Hunter

- **Drive-Wide Heuristic Scanner**: Identifies files over 50 MB across all connected fixed volumes.
- **Safety Classification Engine**: Distinguishes between safe disposable files (driver installers, setup extracts, post-mortem crash dumps) and protected assets (game archives, `.pak` files, executable binaries, local AI model weights).
- **Recycle Bin Integration**: Deletions utilize the native Windows shell API (`SHFileOperation`), allowing files to be inspected and restored from the Windows Recycle Bin if needed.

### 4. Reversible Startup Manager

- Scans Windows Run registry hives (`HKCU` and `HKLM`) and Startup folders.
- Computes boot delay ratings for each item.
- Disables startup entries by migrating keys to `Run_Deltempo_Disabled` rather than deleting them, ensuring changes can be reversed with a single toggle.

---

## Head-to-Head Comparison

| Feature | Deltempo (v1.3.0) | Microsoft PC Manager | CCleaner | BleachBit | Windows Cleanmgr |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **License** | **Free / MIT** | Free | Proprietary / Freemium | Free / GPLv3 | Built-in |
| **Distribution** | **Single Standalone EXE** | Microsoft Store | Installer with bundled ads | Zip / Installer | Windows component |
| **Telemetry & Ads** | **Zero Telemetry (100% Offline)** | Microsoft telemetry | User tracking & popups | None | Diagnostic telemetry |
| **NT Standby List Purge** | **Yes (`NtSetSystemInformation`)** | No | Paid version only | No | No |
| **Working Sets Trim** | **Yes (Dual-Engine Win32)** | Yes | Paid version only | No | No |
| **System File Cache Reset** | **Yes (`SetSystemFileCacheSize`)** | No | No | No | No |
| **Large File Hunter** | **Yes (Safe vs Protected heuristic)** | Basic | Paid version only | No | No |
| **Deletion Reversibility** | **Yes (Windows Recycle Bin undo)** | Permanent delete | Permanent delete | Permanent delete | Permanent delete |
| **Windows Upgrade Residue** | **Yes (`$WINDOWS.~BT`, `ESD`)** | Basic | Paid version only | No | Partial |
| **GPU Shader Cache Purge** | **Yes (DirectX / Vulkan)** | No | No | No | No |
| **Driver OTA Cleanup** | **Yes (NVIDIA App 3–10 GB)** | Limited | No | No | Partial |
| **Creator Render Scratch** | **Yes (Adobe, DaVinci, Blender)** | No | No | No | No |
| **Built-in CLI Automation** | **Yes (Instant global registration)** | No | Limited | Basic | Legacy switches |
| **Automated Test Suite** | **79 xUnit Tests (100% passing)** | Proprietary | Proprietary | Basic | Proprietary |

---

## Command-Line Interface (CLI)

`Deltempo.exe` provides global command-line automation. Running the executable registers the `deltempo` command across PowerShell, Command Prompt, and Windows Terminal.

### Example Commands

```powershell
# 1. Scan all 25 cleanup targets
deltempo scan

# 2. Perform safe cleanup (preserves files modified within 24h)
deltempo clean --safe

# 3. Dry-run cleanup without modifying disk
deltempo clean --safe --dry-run

# 4. Boost memory across process working sets and standby lists
deltempo boost

# 5. Perform deep purge across all 8 NT kernel memory zones
deltempo boost --all

# 6. Hunt large files greater than 500 MB across all drives
deltempo large --min 500MB

# 7. Check current system telemetry and memory pressure in JSON
deltempo status --format json

# 8. List startup programs and their boot delay impact
deltempo startup

# 9. Safely disable a startup entry with registry backup
deltempo startup disable "Discord"
```

### CLI Output Preview

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

## Security and Privacy

- **Zero Telemetry**: Deltempo contains no background analytics, network callbacks, or user tracking. It operates 100% offline.
- **Safety Shield**: The scanner preserves files modified within the last 24 hours, active process working directories, and temporary installer locks.
- **Authenticode Verification**: Release binaries are built with automated SHA-256 hash verification:

```powershell
# Verify executable signature
Get-AuthenticodeSignature Deltempo.exe
```

---

## Building from Source

### Prerequisites

- Windows 10 or Windows 11 (64-bit, x64)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone and Compile

```powershell
# 1. Clone repository
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# 2. Run test suite
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release

# 3. Compile standalone self-contained release executable
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

The output executable will be compiled to `publish/Deltempo.exe` and copied to the root directory with its companion SHA-256 checksum manifest.

---

## Contributing

Contributions are welcome. Please ensure that all pull requests maintain 100% test passing status:

```powershell
dotnet test Tests/Deltempo.Tests/Deltempo.Tests.csproj
```

---

## License

Deltempo is open-source software licensed under the [MIT License](LICENSE).  
Developed and maintained by [Beso1227](https://github.com/Beso1227) and open-source contributors.
