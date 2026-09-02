---
title: CleanerService
tags:
  - service
  - cleaner
  - safety-shield
---

# 🧹 CleanerService

The central cleaning engine in Deltempo.

> [!success] 24-Hour Safety Shield
> Any file whose \LastWriteTime\ is younger than 24 hours is automatically preserved to prevent breaking active installations and downloads.

## Scopes Supported
- **Device Driver Packages & GPU Updates**: NVIDIA App OTA artifacts, AMD, Intel.
- **Microsoft Defender Logs & History**: MPLog support files, scan history cache.
- **Windows System Diagnostic Logs**: CBS, DISM, DPX, Panther, SetupAPI, LogFiles.
- **BSOD Minidumps & Kernel Reports**: Memory crash dumps, LiveKernelReports.
- **Temporary Internet Files & WebCache**: INetCache, WebCache, CryptnetUrlCache.
- **DirectX & GPU Shaders**: D3DSCache, NVIDIA DXCache, AMD DxCache.
- **Browser Caches**: Chrome, Brave, Edge, Opera, Firefox.
- **Recycle Bin**: EmptyRecycleBin Win32 API.
