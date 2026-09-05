# Deltempo Domain Context

Deltempo is a high-performance Windows PC guardian providing zero-overhead cache cleaning, NT kernel memory optimization, startup acceleration, and Windows system corruption repair.

## Language

### Core Optimization Engines

**Cleaning Target**:
A registered filesystem scope (cache, logs, shader repository, or temp store) that can be scanned and safely purged.
_Avoid_: Folder to delete, temp rule, clean item

**Safety Shield**:
A retention guard preventing deletion of files modified or created within the last 24 hours to prevent active session disruption.
_Avoid_: Safe mode filter, age filter, safety toggle

**Orphaned App**:
Leftover registry keys or AppData/ProgramData cache directories left behind by previously uninstalled applications.
_Avoid_: Dead software, leftover folders

**Memory Zone**:
One of the specialized NT kernel memory areas (Working Set, Modified Page List, Standby List, System Working Set) purged via native NT APIs.
_Avoid_: RAM cache, memory chunk

### System Integrity & Corruption Repair

**System Integrity**:
The verified, healthy state of Windows component store manifests, protected system binaries, and filesystem metadata.
_Avoid_: Fix Windows, corruption fixer

**Component Store (WinSxS)**:
The core Windows package repository managed by DISM that holds system component payloads and servicing packages.
_Avoid_: Windows installer folder, update cache

**System File Checker (SFC)**:
The native Windows utility (`sfc.exe`) that scans and replaces damaged, missing, or altered Windows system files against known good copies.
_Avoid_: System scan, file fixer

**DISM Servicing Repair**:
The Deployment Image Servicing and Management (`dism.exe`) pipeline that checks, scans, and restores healthy component store payload images.
_Avoid_: Image repair, dism command

**Filesystem Integrity (CHKDSK)**:
The volume metadata scanner (`chkdsk.exe`) that detects and repairs NTFS/ReFS master file table, indexing, and cluster discrepancies.
_Avoid_: Disk check, disk scan

**Servicing Stack Reset**:
The automated remediation workflow that stops Windows Update services, purges corrupted SoftwareDistribution/Catroot2 catalogs, and restarts services.
_Avoid_: Windows update fix, update repair
