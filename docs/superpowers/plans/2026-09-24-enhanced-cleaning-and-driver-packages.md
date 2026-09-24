# Enhanced Cleaning, Scanning, Safety Measurements & Device Driver Packages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernize Deltempo's cleaning, scanning, and safety measurement engines to achieve feature parity with Microsoft PC Manager, specifically enabling perfect detection, safe vetting, and full cleanup of Device Driver Packages and vendor GPU/hardware update staging caches.

**Architecture:** 
1. Refine `ProtectionPolicy` to whitelist verified, disposable System32 sub-caches (`DriverStore\Temp`, `DriverState`, `LogFiles`) while maintaining strict defense against OS kernel files and system executable binaries (`.sys`, `.dll`, `.exe`, `.inf`, `.cat`).
2. Enhance `FileSafetyEngine` with deterministic cache rules for driver staging packages.
3. Expand `SystemCacheResolver` to discover comprehensive NVIDIA, AMD, and Intel installer unpacker directories.
4. Implement `WindowsDriverMaintenanceService` to trigger native Windows PnP DriverStore maintenance (`pnpclean.dll,RunDLL_PnpClean /DRIVERS /MAXCLEAN`) alongside filesystem purging when running with administrative privileges.

**Tech Stack:** C# 13, .NET 10.0 WPF, Win32 P/Invoke & Process Execution, xUnit, FluentAssertions.

**Spec:** In-chat approved design from superpowers:brainstorming session on 2026-09-24.

## Global Constraints
- Target Framework: `net10.0-windows10.0.19041.0` (and `net10.0-windows` for Core).
- Never allow deletion of `.sys`, `.dll`, `.exe`, `.inf`, `.cat`, or `.kdbx` from protected Windows system directories or driver stores outside official APIs.
- Fail closed: If any path cannot be verified as safely disposable, preserve it by default (`SafetyRiskTier.Unknown` or `SafetyRiskTier.Protected`).
- Zero broken tests: All 596 existing tests must continue to pass, with new tests added for all new functionality.

---

### Task 1: System32 Safe Cache Exception & Binary Guard in ProtectionPolicy

**Files:**
- Modify: `Core/Safety/ProtectionPolicy.cs`
- Test: `Tests/Deltempo.Tests/ProtectionPolicyTests.cs`

**Interfaces:**
- `ProtectionPolicy.IsProtected(string filePath, out string matchedReason)`
- `ProtectionPolicy.IsSafeSystemSubdirectory(string path, out string matchedCategory)`

- [ ] **Step 1: Write the failing tests in `ProtectionPolicyTests.cs`**

```csharp
[Theory]
[InlineData(@"C:\Windows\System32\DriverStore\Temp\setup.log", false)]
[InlineData(@"C:\Windows\System32\DriverStore\Temp\staging.tmp", false)]
[InlineData(@"C:\Windows\System32\DriverState\cache.dat", false)]
[InlineData(@"C:\Windows\System32\DriverStore\Temp\driver.sys", true)]
[InlineData(@"C:\Windows\System32\DriverStore\FileRepository\nv_dispi.inf_amd64\nv_dispi.inf", true)]
[InlineData(@"C:\Windows\System32\drivers\etc\hosts", true)]
[InlineData(@"C:\Windows\System32\kernel32.dll", true)]
public void ProtectionPolicy_System32SubpathSafety_HandlesSafeExceptionsCorrectly(string path, bool expectedProtected)
{
    bool isProtected = ProtectionPolicy.IsProtected(path, out string reason);
    Assert.Equal(expectedProtected, isProtected);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "ProtectionPolicy_System32SubpathSafety_HandlesSafeExceptionsCorrectly"`
Expected: FAIL (because `DriverStore\Temp\setup.log` currently returns `true` / is protected).

- [ ] **Step 3: Implement safe System32 exception handling in `ProtectionPolicy.cs`**

Add helper to distinguish safe System32 disposable staging locations:
```csharp
private static readonly HashSet<string> PermittedSystem32Subdirectories = new(StringComparer.OrdinalIgnoreCase)
{
    @"system32\driverstore\temp",
    @"system32\driverstate",
    @"system32\logfiles",
    @"system32\winevt\logs"
};

private static readonly HashSet<string> ForbiddenSystemExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    ".dll", ".sys", ".exe", ".inf", ".cat", ".ocx", ".cpl", ".msc", ".drv"
};
```
In `ProtectionPolicy.IsProtected`, check if path is in `PermittedSystem32Subdirectories`:
- If file extension is in `ForbiddenSystemExtensions`, it remains strictly `PROTECTED`.
- If inside a permitted sub-directory and NOT a forbidden system binary, permit it to proceed to safety tier evaluation.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "ProtectionPolicy_System32SubpathSafety_HandlesSafeExceptionsCorrectly"`
Expected: PASS.

- [ ] **Step 5: Run full test suite**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --no-restore`
Expected: PASS (all tests pass).

---

### Task 2: Driver Staging Cache Rules in FileSafetyEngine

**Files:**
- Modify: `Core/Safety/FileSafetyEngine.cs`
- Test: `Tests/Deltempo.Tests/ProtectionPolicyTests.cs` (or new test method)

**Interfaces:**
- `FileSafetyEngine.EvaluateVerifiedCachePatterns(string pathLower, string fileName, string ext, long sizeBytes)`

- [ ] **Step 1: Write failing test for driver package staging recognition**

```csharp
[Theory]
[InlineData(@"C:\Program Files\NVIDIA Corporation\Installer2\Display.Driver\nvdisp.nvi", "SAFE (Hardware Driver Package Staging)")]
[InlineData(@"C:\ProgramData\NVIDIA Corporation\Downloader\latest_driver.exe", "SAFE (Hardware Driver Package Staging)")]
[InlineData(@"C:\Windows\System32\DriverStore\Temp\scratch.tmp", "SAFE (Hardware Driver Package Staging)")]
public void FileSafetyEngine_DriverPackageStaging_ClassifiedAsSafe(string path, string expectedVerdictPrefix)
{
    var result = FileSafetyEngine.Analyze(path, allowedRoot: Path.GetDirectoryName(path));
    Assert.Equal(SafetyRiskTier.Safe, result.Tier);
    Assert.StartsWith(expectedVerdictPrefix, result.Verdict);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "FileSafetyEngine_DriverPackageStaging_ClassifiedAsSafe"`
Expected: FAIL.

- [ ] **Step 3: Implement driver package staging patterns in `FileSafetyEngine.cs`**

Add rule in `EvaluateVerifiedCachePatterns`:
```csharp
// Hardware Driver Staging & Installer Caches (NVIDIA, AMD, Intel, DriverStore Temp)
if (pathLower.Contains(@"\nvidia corporation\installer2\") ||
    pathLower.Contains(@"\nvidia corporation\downloader\") ||
    pathLower.Contains(@"\nvidia app\updateframework\ota-artifacts\") ||
    pathLower.Contains(@"\amd_radeon_software_installer\") ||
    pathLower.Contains(@"\amd\packages\") ||
    pathLower.Contains(@"\intel\package cache\") ||
    pathLower.Contains(@"\driverstore\temp\"))
{
    return CreateResult(
        SafetyRiskTier.Safe,
        95,
        "SAFE (Hardware Driver Package Staging)",
        "VERIFIED DRIVER CACHE",
        $"Hardware driver installation and update staging artifact ({FormatBytes(sizeBytes)}). Safe to purge once installed.",
        "DriverStagingPackageRule",
        origin: "Hardware Driver Staging / Installer Cache",
        impact: "Zero impact — driver is already installed.");
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "FileSafetyEngine_DriverPackageStaging_ClassifiedAsSafe"`
Expected: PASS.

---

### Task 3: Comprehensive Driver Staging Locations in SystemCacheResolver

**Files:**
- Modify: `Services/Providers/CacheResolvers/SystemCacheResolver.cs`
- Test: `Tests/Deltempo.Tests/CleanerServiceTests.cs`

**Interfaces:**
- `SystemCacheResolver.ResolveDeviceDriverDirectories()`

- [ ] **Step 1: Write test verifying all required vendor driver directories are resolved**

```csharp
[Fact]
public void ResolveDeviceDriverDirectories_ContainsAllMajorVendorCaches()
{
    var dirs = SystemCacheResolver.ResolveDeviceDriverDirectories();
    Assert.Contains(dirs, d => d.Contains("Installer2", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(dirs, d => d.Contains("DriverStore", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(dirs, d => d.Contains("AMD", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(dirs, d => d.Contains("Intel", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "ResolveDeviceDriverDirectories_ContainsAllMajorVendorCaches"`
Expected: FAIL (because `Installer2` is not currently in the returned list).

- [ ] **Step 3: Update `ResolveDeviceDriverDirectories` in `SystemCacheResolver.cs`**

Expand with:
- `Path.Combine(progFiles, "NVIDIA Corporation", "Installer2")`
- `Path.Combine(progFilesX86, "NVIDIA Corporation", "Installer2")`
- `Path.Combine(progData, "NVIDIA Corporation", "GeForce Experience", "Download")`
- `Path.Combine(progData, "NVIDIA", "DisplayDriver")`
- `Path.Combine(rootDrive, "AMD", "AMD_Radeon_Software_Installer")`
- `Path.Combine(progData, "AMD", "DVR")`
- `Path.Combine(rootDrive, "Intel", "GFX")`
- `Path.Combine(progData, "Intel", "Package Cache")`
- `Path.Combine(progData, "Intel", "Logs")`

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "ResolveDeviceDriverDirectories_ContainsAllMajorVendorCaches"`
Expected: PASS.

---

### Task 4: Windows Native PnP Driver Maintenance Integration

**Files:**
- Create: `Services/WindowsDriverMaintenanceService.cs`
- Modify: `Services/CleanerService.cs`
- Test: `Tests/Deltempo.Tests/CleanerServiceTests.cs`

**Interfaces:**
- `WindowsDriverMaintenanceService.RunPnpDriverCleanAsync(Action<string, LogLevel> logAction, CancellationToken ct)`

- [ ] **Step 1: Write failing unit test for `WindowsDriverMaintenanceService`**

```csharp
[Fact]
public async Task WindowsDriverMaintenanceService_CanExecuteWithoutCrashing()
{
    // Verifies the service initializes and handles non-admin or admin execution gracefully
    var (success, message) = await WindowsDriverMaintenanceService.RunPnpDriverCleanAsync((msg, level) => { }, CancellationToken.None);
    Assert.NotNull(message);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "WindowsDriverMaintenanceService_CanExecuteWithoutCrashing"`
Expected: FAIL (class does not exist yet).

- [ ] **Step 3: Implement `WindowsDriverMaintenanceService.cs`**

Create `Services/WindowsDriverMaintenanceService.cs`:
- Check `ElevationService.IsRunAsAdmin()`. If not admin, report requiring admin.
- Launch `rundll32.exe pnpclean.dll,RunDLL_PnpClean /DRIVERS /MAXCLEAN` with a 30-second timeout, `CreateNoWindow = true`, `UseShellExecute = false`.
- Log output and execution status safely.

- [ ] **Step 4: Integrate into `CleanerService.CleanFolderAsync`**

In `CleanerService.CleanFolderAsync`:
When `folder.Id == "DeviceDriverPackages"` and running as Admin:
- In addition to cleaning the resolved directories via `CleanupPlanner`, invoke `await WindowsDriverMaintenanceService.RunPnpDriverCleanAsync(logAction, ct);`.
- Log success: `"Windows PnP Driver Maintenance executed: Obsolete driver packages safely purged from DriverStore."`

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --filter "WindowsDriverMaintenanceService_CanExecuteWithoutCrashing"`
Expected: PASS.

---

### Task 5: Full Regression Testing & Localization Verification

**Files:**
- Modify: `Services/LocalizationService.cs` (if needed for new messages)
- Test: Full `Tests/Deltempo.Tests` suite

- [ ] **Step 1: Run complete unit test suite**

Run: `dotnet test Tests\Deltempo.Tests\Deltempo.Tests.csproj --no-restore`
Expected: 100% PASS, zero regressions, all 596+ tests passing.

- [ ] **Step 2: Run CLI Dry-Run smoke check**

Run: `dotnet run --project WinTempCleaner.csproj -- dry-run` (or CLI test)
Expected: Executes without exceptions, lists `DeviceDriverPackages` and safety tiers correctly.
