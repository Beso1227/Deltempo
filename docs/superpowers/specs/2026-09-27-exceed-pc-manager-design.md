# Deltempo: Next-Gen Cleaner Engine (Exceeding Microsoft PC Manager)

## Overview

This specification details the enhancements to make Deltempo definitively superior to Microsoft PC Manager, CCleaner, and BleachBit, focusing on two key pillars:

1. **Deeper & Smarter Storage Reclamation**: Targeting multi-gigabyte cache pools that generic cleaners miss (Delivery Optimization peer cache, CBS persistent servicing archives, LiveKernelReports, WinGet staging, and full multi-GPU DX/GL shader stores).
2. **Native Memory & Working Set Optimizer ("RAM Boost Engine")**: Real-time memory trimming via high-performance Win32 API working-set flushing, giving users immediate memory reclamation with zero background bloat.

---

## 1. Storage Targets & System Cache Resolver Expansion

### A. Delivery Optimization (`DeliveryOptimization`)

* Paths:
  * `C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache`
  * `C:\ProgramData\Microsoft\Network\Downloader`
* Safety: Only purge completed chunks or stale cached payload files; never delete the directory junction or active state DBs.

### B. CBS & Windows Servicing Persistent Archives (`WinServicingLogs`)

* Paths:
  * `C:\Windows\Logs\CBS` (specifically targets `CbsPersist_*.log` and `CbsPersist_*.cab`)
  * `C:\Windows\Logs\DISM\dism.log`
  * `C:\Windows\LiveKernelReports`
* Retention: Files older than 24 hours to prevent contention with active Windows Update or DISM transactions.

### C. WinGet and Microsoft Store Staging (`WinGetAndAppPackages`)

* Paths:
  * `%LOCALAPPDATA%\Microsoft\WinGet\Cache`
  * `%LOCALAPPDATA%\Microsoft\WinGet\Packages`
  * `%LOCALAPPDATA%\Packages\*\AC\INetCache`
  * `%LOCALAPPDATA%\Packages\*\LocalCache`

### D. Comprehensive Multi-Vendor GPU Shader Caches

* Paths:
  * NVIDIA: `%LOCALAPPDATA%\NVIDIA\DXCache`, `%LOCALAPPDATA%\NVIDIA\GLCache`
  * AMD: `%LOCALAPPDATA%\AMD\DxCache`
  * Intel & Windows: `%LOCALAPPDATA%\D3DSCache` across all drives and user profiles.

---

## 2. Native System RAM Optimizer (`SystemMemoryOptimizationService`)

### P/Invoke & Logic

* Interops with `psapi.dll`'s `EmptyWorkingSet(IntPtr hProcess)`.
* Process Filtering:
  * Skip process ID 0 (Idle) and 4 (System).
  * Exclude critical system processes (`csrss`, `smss`, `services`, `lsass`, `wininit`, `dwm`).
  * Exclude self (`Deltempo` / `deltempo_cli`).
* Returns `MemoryOptimizationResult`:
  * `TotalWorkingSetBeforeBytes`
  * `TotalWorkingSetAfterBytes`
  * `BytesReclaimed`
  * `ProcessesOptimizedCount`

---

## 3. UI and CLI Integration

* **CLI**: Add `--boost` / `-b` flag to run RAM optimization alongside or independently of clean operations.
* **Core Services**: Register the new categories into `CleanerService.GetDefaultTargets()`.

---

## 4. Test Strategy

* Unit tests for `SystemMemoryOptimizationService` confirming it executes safely and returns non-negative memory metrics.
* Unit tests for `SystemCacheResolver` verifying resolution of Delivery Optimization, CBS archive filters, and WinGet targets.
* Run full 660+ regression test suite.
