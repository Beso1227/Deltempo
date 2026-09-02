<div align="center">

  <img src="app_icon.png" alt="Deltempo Logo" width="128" height="128" />

  # Deltempo — Pure Precision Windows & User Profile Cleaner
  ### 👑 The definitive open-source, zero-bloat, single-file portable Windows cleanup utility & modern CCleaner alternative.

  [![Release](https://img.shields.io/github/v/release/yourusername/deltempo?style=for-the-badge&color=blue)](https://github.com/yourusername/deltempo/releases)
  [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](https://opensource.org/licenses/MIT)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6?style=for-the-badge&logo=windows)](https://github.com/yourusername/deltempo)
  [![Type](https://img.shields.io/badge/Portable-Single%20EXE-10B981?style=for-the-badge)](https://github.com/yourusername/deltempo/releases)
  [![Telemetry](https://img.shields.io/badge/Telemetry-0%25%20Zero-success?style=for-the-badge)](https://github.com/yourusername/deltempo)
  [![Accessibility](https://img.shields.io/badge/WCAG%202.2-Level%20AA-purple?style=for-the-badge)](https://github.com/yourusername/deltempo)

  <p align="center">
    <a href="#-quick-download">Quick Download</a> •
    <a href="#-key-features">Key Features</a> •
    <a href="#-the-user-profile--orphaned-app-hunter">User Profile Hunter</a> •
    <a href="#-why-deltempo-vs-others">Why Deltempo?</a> •
    <a href="#-cleaning-categories">Cleanup Scope</a> •
    <a href="#-building-from-source">Build from Source</a> •
    <a href="#-faq">FAQ</a>
  </p>

</div>

---

## 🚀 Overview

**Deltempo** is an ultra-fast, modern, open-source Windows cleaner engineered to safely reclaim **10 to 40+ GB** of wasted storage space from hidden system directories and user profile bloat (`C:\Users\<Username>\AppData`).

Unlike legacy optimizer tools that bundle background telemetry, intrusive ads, or fake "registry repair" gimmicks, Deltempo gives you **100% authentic byte-for-byte precision**, a stunning **Double-Bezel Obsidian UI**, and runs as a **single standalone 2.4 MB portable executable** with zero installation required.

---

## 🌟 Key Features

- ⚡ **Single Portable Standalone EXE**: Zero installer, zero registry clutter, zero background services. Run directly from Desktop, Downloads, or a USB drive.
- 👑 **Deep User Profile & AppData Hunter**: Solves the #1 Windows problem—reclaiming tens of gigabytes stuck in `AppData\Local` and `AppData\Roaming` without deleting your account logins or configurations.
- 🎮 **DirectX & GPU Shader Cache Purge**: Cleans compiled graphic shader dumps from NVIDIA (`DXCache`/`GLCache`), AMD (`DxCache`), D3DSCache, and Intel (often saving **5–20 GB**).
- 🧹 **Desktop Apps Cache Sweeper**: Safely strips disposable `GPUCache`, `Code Cache`, and media pools in Discord, Spotify, Slack, VS Code, Teams, and Notion.
- 🕵️ **Verified Orphaned App Residue Detector**: Cross-references the Windows Uninstall Registry, Start Menu, and running processes to detect true dead leftover folders from uninstalled programs.
- 🛡️ **Safety Shield Guardrail (<24h Protection)**: Automatically preserves recently created or modified files to prevent disrupting active installers or background processes.
- 📊 **Real-Time OS Drive Telemetry**: Live Drive C: gauge bar and dynamic space reclamation counter.
- 🔍 **Top Files Inspector**: Drill down into any category to view and inspect the top 15 largest individual junk files with full paths and timestamps.
- ♿ **WCAG 2.2 AA Accessible & Keyboard Driven**: Full screen-reader support (Narrator/NVDA), high-contrast focus rings, and intuitive shortcuts (`F5` Rescan, `Ctrl+Enter` Clean, `Esc` Close).
- 🔒 **100% Private & Open Source**: Zero telemetry, zero network requests, 100% offline, MIT licensed.

---

## 📊 Why Deltempo vs. Others?

| Feature | 🛡️ Deltempo | Generic / Ad-Driven Cleaners | Built-in Windows Disk Cleanup |
|---|:---:|:---:|:---:|
| **Open-Source & Free Forever** | **Yes (MIT License)** | ❌ No (Freemium/Ads/Paywalls) | ⚠️ Proprietary |
| **Zero Install / Single Portable EXE** | **Yes (2.4 MB)** | ❌ Needs Heavy Installer | ⚠️ System Component |
| **User Profile & AppData Cache Sweeper** | **Yes (Discord/Spotify/Slack/IDE)** | ⚠️ Limited / Paid Tier | ❌ No |
| **DirectX & GPU Shader Cache Purge** | **Yes (5–20 GB Reclaimed)** | ❌ No | ❌ No |
| **Verified Orphaned App Leftovers** | **Yes (Registry Cross-Referenced)** | ⚠️ Unreliable / Paid | ❌ No |
| **Safety Shield (<24h File Protection)**| **Yes** | ❌ Blind Deletion | ❌ No |
| **Top Junk Files Inspector** | **Yes (Inspect Large Files)** | ❌ Paid / Locked | ❌ No |
| **Modern Luxury Obsidian UI** | **Yes (Double-Bezel + Dark)** | ❌ Outdated / Cluttered | ❌ Legacy 90s UI |
| **Telemetry & Data Collection** | **0% (Completely Offline)** | ❌ Invasive Background Analytics | ⚠️ Microsoft Telemetry |

---

## 🧹 Cleanup Scope & Categories

1. **User Temp & Scratchpad (`%TEMP%`)**: Temporary extracts, installer unpacks, and application scratch data in `AppData\Local\Temp`.
2. **Windows System Temp (`C:\Windows\Temp`)**: System-level update scratchpads, driver setup residue, and OS diagnostic traces.
3. **Windows Prefetch (`C:\Windows\Prefetch`)**: Obsolete application launch caches and stale prefetch traces.
4. **Windows Update Cache (`SoftwareDistribution\Download`)**: Old downloaded Windows update packages and installer dumps.
5. **DirectX & GPU Shader Caches**: NVIDIA `DXCache`/`GLCache`, AMD `DxCache`, `D3DSCache`, and Intel `ShaderCache`.
6. **Desktop Apps Cache Sweeper**: Disposable cache subfolders (`GPUCache`, `Code Cache`, `Cache_Data`) across Discord, Spotify, Slack, VS Code, Teams, Notion, and Steam.
7. **Web Browsers Cache Pool**: Cache pools for Google Chrome, Microsoft Edge, Brave, and Mozilla Firefox (passwords and cookies are preserved).
8. **Developer & Build Caches**: Package caches for `pip`, `npm`, `.gradle\caches`, `.cache`, `.yarn`, and `nuget\temp`.
9. **Error Reports & Crash Dumps**: Windows Error Reporting (`WER`) traces, BSOD memory dumps, and process crash logs.
10. **Explorer Thumbnail Cache**: Cached thumbnail databases (`thumbcache_*.db`) that often cause Windows Explorer lag.
11. **Windows Recycle Bin**: Native API-level purge across all connected drives.
12. **Verified Orphaned App Residuals**: Dead directories left behind by previously uninstalled software, verified through process and registry inspection.

---

## ⚡ Quick Download

Download the latest standalone executable from the [Releases](https://github.com/yourusername/deltempo/releases) page:
- **`Deltempo.exe`** (Single portable executable, ~2.4 MB, requires no installation)

> **Note**: Deltempo automatically requests Administrator privileges on launch to enable full access to system-level directories (`C:\Windows\Temp`, `Prefetch`, and `SoftwareDistribution`).

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Windows 10 / 11 (x64)

### Build Commands
```powershell
# Clone the repository
git clone https://github.com/yourusername/deltempo.git
cd deltempo

# Build Release binary
dotnet build -c Release

# Publish single-file standalone executable
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

---

## 🤝 Contributing

Contributions, bug reports, and feature requests are welcome! Check out [CONTRIBUTING.md](CONTRIBUTING.md) to get started.

---

## 📄 License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.
