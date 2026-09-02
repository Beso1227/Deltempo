---
title: Core Architecture
tags:
  - architecture
  - wpf
  - dotnet10
aliases:
  - Architecture
---

# 🏛️ Deltempo Core Architecture

Deltempo is architected with a **Dual-Subsystem Model**:

1. **WPF Desktop UI Subsystem** (\WinExe\): Runs completely silent without spawning background console windows.
2. **Native Console Subsystem** (\IMAGE_SUBSYSTEM_WINDOWS_CUI\): Runs synchronously inside PowerShell and Command Prompt without prompt interleaving or race conditions.

> [!important] Single Instance Guarantee
> Managed by [[02 - Services/SingleInstanceManager|SingleInstanceManager]] using a named system mutex (\Global\Deltempo_App_SingleInstance_Mutex_v1\) and Windows Message Broadcasts.

## Key Component Links
- [[02 - Services/CleanerService|CleanerService]]
- [[02 - Services/MemoryOptimizerService|MemoryOptimizerService]]
- [[02 - Services/StartupManagerService|StartupManagerService]]
- [[02 - Services/LargeFileHunterService|LargeFileHunterService]]
- [[02 - Services/ProcessOptimizerService|ProcessOptimizerService]]
