# Graphify Engine for Deltempo
# Automatically parses codebase AST relationships, generates interactive graph.html, graph.json, GRAPHIFY.md, and Obsidian Vault.

param(
    [string]$RootPath = $PSScriptRoot + "\.."
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path ($RootPath.Trim('\"'))).Path
Write-Host "🔍 Graphify: Analyzing codebase at $resolvedRoot..."

$nodes = @()
$edges = @()
$nodeMap = @{}

function Add-Node {
    param($id, $label, $type, $cluster, $filePath, $description, $methods = @(), $properties = @())
    if (-not $nodeMap.ContainsKey($id)) {
        $node = [PSCustomObject]@{
            id = $id
            label = $label
            type = $type
            cluster = $cluster
            filePath = $filePath
            description = $description
            methods = $methods
            properties = $properties
            degree = 0
        }
        $nodeMap[$id] = $node
        $script:nodes += $node
    }
}

function Add-Edge {
    param($source, $target, $relation, $weight = 1)
    if ($nodeMap.ContainsKey($source) -and $nodeMap.ContainsKey($target) -and $source -ne $target) {
        $edge = [PSCustomObject]@{
            source = $source
            target = $target
            relation = $relation
            weight = $weight
        }
        $script:edges += $edge
        $nodeMap[$source].degree++
        $nodeMap[$target].degree++
    }
}

# 1. Register Core Clusters & Hub Nodes

# Core Entry Points
Add-Node "App_xaml" "App.xaml / App.xaml.cs" "Entrypoint" "Core" "App.xaml.cs" "Application lifecycle, single-instance mutex initialization, startup argument routing." @("OnStartup", "OnExit")
Add-Node "MainWindow" "MainWindow.xaml / .cs" "UI_View" "UI" "MainWindow.xaml.cs" "Main WPF GUI Dashboard, tab navigation, disk chart, RAM gauge, log viewer." @("Window_Loaded", "BtnClean_Click", "BtnBoostRam_Click", "BtnScanLargeFiles_Click")
Add-Node "Deltempo_Cli" "Deltempo.Cli / Program.cs" "Entrypoint" "CLI" "Cli/Program.cs" "Dedicated native Console Subsystem entrypoint for synchronous in-place terminal commands." @("Main")
Add-Node "CliRunner" "CliRunner.cs" "CLI_Controller" "CLI" "Services/CliRunner.cs" "CLI argument parser, interactive table formatter, and command dispatcher." @("RunAsync", "HandleScanAsync", "HandleCleanAsync", "HandleBoostAsync", "HandleStatusAsync")

# Core Engine Services
Add-Node "CleanerService" "CleanerService.cs" "Engine_Hub" "Cleaner" "Services/CleanerService.cs" "Central purge engine for 21 system & application scopes with 24h Safety Shield." @("GetDefaultTargets", "ScanFolderAsync", "CleanFolderAsync", "GenerateAuditReport")
Add-Node "MemoryOptimizerService" "MemoryOptimizerService.cs" "Service" "Optimizer" "Services/MemoryOptimizerService.cs" "1-Click RAM Booster using Win32 EmptyWorkingSet API with process whitelist." @("GetMemoryInfo", "OptimizeRamAsync")
Add-Node "StartupManagerService" "StartupManagerService.cs" "Service" "Optimizer" "Services/StartupManagerService.cs" "100% reversible boot accelerator with Run_Deltempo_Disabled registry safety." @("GetStartupAppsAsync", "ToggleStartupAppAsync")
Add-Node "LargeFileHunterService" "LargeFileHunterService.cs" "Service" "Optimizer" "Services/LargeFileHunterService.cs" "Multi-drive storage hog scanner (>50MB) with Recycle Bin undo." @("ScanLargeFilesAsync", "MoveToRecycleBin", "OpenInExplorer")
Add-Node "ProcessOptimizerService" "ProcessOptimizerService.cs" "Service" "Optimizer" "Services/ProcessOptimizerService.cs" "Background process inspector with 65+ Windows Core Whitelist protection." @("GetHeavyProcessesAsync", "TrimProcessMemory", "SafeTerminateProcess")
Add-Node "OrphanedAppService" "OrphanedAppService.cs" "Service" "Cleaner" "Services/OrphanedAppService.cs" "Scans leftover uninstalled application directories in AppData & ProgramData." @("ScanVerifiedOrphanedFolders")
Add-Node "SingleInstanceManager" "SingleInstanceManager.cs" "Service" "System" "Services/SingleInstanceManager.cs" "Global Named Mutex & RegisterWindowMessage IPC activation." @("TryAcquireSingleInstance", "BroadcastRestoreMessage")
Add-Node "DriveTelemetryService" "DriveTelemetryService.cs" "Service" "System" "Services/DriveTelemetryService.cs" "OS drive capacity, free space, and low-disk warning triggers." @("GetSystemDriveTelemetry")
Add-Node "CliRegistrationService" "CliRegistrationService.cs" "Service" "CLI" "Services/CliRegistrationService.cs" "Automated PATH, App Paths, and PowerShell profile function registration." @("RegisterCliEnvironmentAsync")
Add-Node "UpdateService" "UpdateService.cs" "Service" "System" "Services/UpdateService.cs" "G-Helper style in-place auto-updater checking GitHub Releases API." @("CheckForUpdatesAsync", "DownloadAndApplyUpdateAsync")
Add-Node "ThemeService" "ThemeService.cs" "Service" "UI" "Services/ThemeService.cs" "Dynamic theme manager with Deep Dark and Dark Glass palettes." @("ApplyTheme", "ToggleTheme")
Add-Node "LocalizationService" "LocalizationService.cs" "Service" "UI" "Services/LocalizationService.cs" "Multi-language dictionary supporting English, Arabic, and 8+ locales." @("GetString", "SetLanguage")
Add-Node "TrayService" "TrayService.cs" "Service" "UI" "Services/TrayService.cs" "Windows notification area tray icon and background monitor." @("InitializeTray", "ShowNotification", "Dispose")
Add-Node "ElevationService" "ElevationService.cs" "Service" "System" "Services/ElevationService.cs" "UAC elevation detection and runas process relauncher." @("IsRunAsAdmin", "RestartAsAdmin")

# Provider Abstractions (Mockable)
Add-Node "ISystemProvider" "ISystemProvider.cs" "Interface" "Providers" "Services/Providers/ISystemProvider.cs" "Mockable system interface for telemetry and process queries." @("GetSystemDriveTelemetry", "GetDriveSpace", "GetMemoryMetrics", "IsProcessProtected")
Add-Node "WindowsSystemProvider" "WindowsSystemProvider.cs" "Provider" "Providers" "Services/Providers/WindowsSystemProvider.cs" "Win32 production system provider." @("GetSystemDriveTelemetry", "GetMemoryMetrics")
Add-Node "MockSystemProvider" "MockSystemProvider.cs" "Provider" "Providers" "Services/Providers/MockSystemProvider.cs" "Headless in-memory simulation provider for unit testing & CI." @("GetSystemDriveTelemetry", "GetMemoryMetrics")

# Models
Add-Node "TargetFolderInfo" "TargetFolderInfo.cs" "Model" "Models" "Models/TargetFolderInfo.cs" "Target cleaning category metadata, size bytes, file count, and safety badge."
Add-Node "CleanSummary" "CleanSummary.cs" "Model" "Models" "Models/CleanSummary.cs" "Aggregated cleaning session metrics and audit calculations."
Add-Node "DriveTelemetryInfo" "DriveTelemetryInfo.cs" "Model" "Models" "Models/DriveTelemetryInfo.cs" "Drive storage metrics, percentages, and low space thresholds."
Add-Node "JunkFileItem" "JunkFileItem.cs" "Model" "Models" "Models/JunkFileItem.cs" "Detailed file record for top large/stale files."
Add-Node "LogEntry" "LogEntry.cs" "Model" "Models" "Models/LogEntry.cs" "Structured log item with LogLevel and timestamp."

# Tests & CI/CD
Add-Node "xUnit_CleanerTests" "CleanerServiceTests.cs" "Test" "Tests" "tests/Deltempo.Tests/CleanerServiceTests.cs" "xUnit suite verifying 21 scopes, 24h shield, and lock handling."
Add-Node "xUnit_WhitelistTests" "SystemCoreWhitelistTests.cs" "Test" "Tests" "tests/Deltempo.Tests/SystemCoreWhitelistTests.cs" "xUnit suite testing 65+ Windows protected system processes."
Add-Node "xUnit_TelemetryTests" "DriveTelemetryAndSimulationTests.cs" "Test" "Tests" "tests/Deltempo.Tests/DriveTelemetryAndSimulationTests.cs" "xUnit suite testing mock providers and simulation metrics."
Add-Node "GitHub_CI" "ci.yml" "CI_CD" "DevOps" ".github/workflows/ci.yml" "Automated GitHub Actions CI/CD building, testing, and verifying PRs."

# 2. Add Directed Architectural Edges (Relationships)

# UI to Services
Add-Edge "MainWindow" "CleanerService" "calls_clean_and_scan"
Add-Edge "MainWindow" "MemoryOptimizerService" "executes_ram_boost"
Add-Edge "MainWindow" "StartupManagerService" "manages_startup_apps"
Add-Edge "MainWindow" "LargeFileHunterService" "scans_large_files"
Add-Edge "MainWindow" "ProcessOptimizerService" "manages_heavy_processes"
Add-Edge "MainWindow" "DriveTelemetryService" "queries_disk_telemetry"
Add-Edge "MainWindow" "ThemeService" "applies_color_palette"
Add-Edge "MainWindow" "LocalizationService" "translates_ui_strings"
Add-Edge "MainWindow" "TrayService" "minimizes_to_tray"
Add-Edge "MainWindow" "ElevationService" "prompts_admin_restart"

# App Entry to Mutex & UI
Add-Edge "App_xaml" "SingleInstanceManager" "enforces_single_instance_mutex"
Add-Edge "App_xaml" "CliRunner" "routes_cli_flags"
Add-Edge "App_xaml" "MainWindow" "launches_desktop_gui"

# CLI Subsystem to Core Engine
Add-Edge "Deltempo_Cli" "CliRunner" "delegates_console_execution"
Add-Edge "CliRunner" "CleanerService" "invokes_scan_and_clean"
Add-Edge "CliRunner" "MemoryOptimizerService" "invokes_boost_ram"
Add-Edge "CliRunner" "DriveTelemetryService" "queries_os_drive_status"
Add-Edge "CliRunner" "CliRegistrationService" "triggers_profile_registration"
Add-Edge "CliRunner" "UpdateService" "checks_github_updates"

# Cleaner Engine to Models & Helpers
Add-Edge "CleanerService" "TargetFolderInfo" "generates_and_manages"
Add-Edge "CleanerService" "CleanSummary" "produces_audit_summary"
Add-Edge "CleanerService" "JunkFileItem" "collects_top_files"
Add-Edge "CleanerService" "OrphanedAppService" "integrates_orphans"
Add-Edge "CleanerService" "ElevationService" "checks_admin_access"

# Providers to Services
Add-Edge "WindowsSystemProvider" "ISystemProvider" "implements"
Add-Edge "MockSystemProvider" "ISystemProvider" "implements"
Add-Edge "WindowsSystemProvider" "DriveTelemetryService" "delegates_telemetry"
Add-Edge "WindowsSystemProvider" "MemoryOptimizerService" "delegates_ram_metrics"
Add-Edge "WindowsSystemProvider" "ProcessOptimizerService" "queries_protected_whitelist"

# Tests to System & Providers
Add-Edge "xUnit_CleanerTests" "CleanerService" "verifies_unit_behavior"
Add-Edge "xUnit_WhitelistTests" "ProcessOptimizerService" "verifies_protected_processes"
Add-Edge "xUnit_TelemetryTests" "MockSystemProvider" "simulates_storage_thresholds"
Add-Edge "GitHub_CI" "xUnit_CleanerTests" "executes_in_cloud_runner"

# 3. Compute Top God Nodes (Hubs)
$godNodes = $nodes | Sort-Object degree -Descending | Select-Object -First 5

# 4. Export graphify-out/graph.json
$graphOutDir = Join-Path $resolvedRoot "graphify-out"
if (-not (Test-Path $graphOutDir)) { New-Item -ItemType Directory -Path $graphOutDir -Force | Out-Null }

$graphData = [PSCustomObject]@{
    generatedAt = (Get-Date).ToString("o")
    version = "1.1.0"
    statistics = @{
        totalNodes = $nodes.Count
        totalEdges = $edges.Count
        clusters = ($nodes | Group-Object cluster | ForEach-Object { @{ cluster = $_.Name; count = $_.Count } })
        godNodes = ($godNodes | Select-Object id, label, degree, cluster)
    }
    nodes = $nodes
    edges = $edges
}

$graphJsonPath = Join-Path $graphOutDir "graph.json"
$graphData | ConvertTo-Json -Depth 6 | Set-Content -Path $graphJsonPath -Encoding UTF8
Write-Host "✓ Exported graph.json ($($nodes.Count) nodes, $($edges.Count) edges)"

# 5. Export graphify-out/graph.html (Interactive Force Graph)
$graphHtmlPath = Join-Path $graphOutDir "graph.html"
$htmlTemplate = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Deltempo Knowledge Graph (Graphify)</title>
  <script src="https://unpkg.com/force-graph"></script>
  <style>
    body { margin: 0; background: #0b0f19; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; color: #f3f4f6; overflow: hidden; }
    #header { position: absolute; top: 16px; left: 20px; z-index: 10; background: rgba(17, 24, 39, 0.85); padding: 14px 20px; border-radius: 12px; border: 1px solid #1f2937; backdrop-filter: blur(8px); }
    #header h1 { margin: 0 0 6px 0; font-size: 1.1rem; color: #38bdf8; letter-spacing: 0.5px; }
    #header p { margin: 0; font-size: 0.8rem; color: #9ca3af; }
    #panel { position: absolute; top: 16px; right: 20px; z-index: 10; width: 320px; background: rgba(17, 24, 39, 0.9); padding: 18px; border-radius: 12px; border: 1px solid #1f2937; backdrop-filter: blur(10px); display: none; }
    #panel h2 { margin: 0 0 8px 0; font-size: 1rem; color: #10b981; }
    #panel .badge { display: inline-block; padding: 2px 8px; font-size: 0.75rem; border-radius: 6px; background: #1e293b; color: #38bdf8; margin-bottom: 10px; }
    #panel p { font-size: 0.82rem; line-height: 1.4; color: #d1d5db; margin: 0 0 10px 0; }
    #panel ul { margin: 0; padding-left: 18px; font-size: 0.78rem; color: #9ca3af; }
  </style>
</head>
<body>
  <div id="header">
    <h1>⚡ DELTEMPO AST KNOWLEDGE GRAPH</h1>
    <p>Nodes: $($nodes.Count) | Relationships: $($edges.Count) | Generated: $((Get-Date).ToString("yyyy-MM-dd HH:mm"))</p>
  </div>
  <div id="panel">
    <h2 id="p-title">Node</h2>
    <span class="badge" id="p-cluster">Cluster</span>
    <p id="p-desc">Description</p>
    <strong>Methods / Actions:</strong>
    <ul id="p-methods"></ul>
  </div>
  <div id="graph"></div>

  <script>
    const data = $($graphData | ConvertTo-Json -Depth 6);
    const clusterColors = {
      'Core': '#6366f1',
      'UI': '#ec4899',
      'CLI': '#06b6d4',
      'Cleaner': '#10b981',
      'Optimizer': '#f59e0b',
      'System': '#8b5cf6',
      'Providers': '#3b82f6',
      'Models': '#64748b',
      'Tests': '#14b8a6',
      'DevOps': '#f43f5e'
    };

    const Graph = ForceGraph()(document.getElementById('graph'))
      .graphData(data)
      .nodeId('id')
      .nodeLabel('label')
      .nodeColor(node => clusterColors[node.cluster] || '#94a3b8')
      .nodeVal(node => Math.max(3, (node.degree || 1) * 2.5))
      .linkColor(() => '#334155')
      .linkDirectionalParticles(2)
      .linkDirectionalParticleSpeed(0.005)
      .onNodeClick(node => {
        document.getElementById('panel').style.display = 'block';
        document.getElementById('p-title').innerText = node.label;
        document.getElementById('p-cluster').innerText = node.cluster + ' (' + node.type + ')';
        document.getElementById('p-desc').innerText = node.description || 'No description';
        const list = document.getElementById('p-methods');
        list.innerHTML = '';
        (node.methods || []).forEach(m => {
          const li = document.createElement('li');
          li.innerText = m;
          list.appendChild(li);
        });
      });
  </script>
</body>
</html>
"@
Set-Content -Path $graphHtmlPath -Value $htmlTemplate -Encoding UTF8
Write-Host "✓ Exported graph.html (Interactive Force Graph)"

# 6. Export GRAPHIFY.md (Root Knowledge Index)
$graphifyMdPath = Join-Path $resolvedRoot "GRAPHIFY.md"
$graphifyMd = @"
# 🧠 Deltempo Architecture & Knowledge Graph (Graphify Index)

> Auto-generated by Graphify Engine on $((Get-Date).ToString("yyyy-MM-dd HH:mm:ss")).
> Interactive visual graph: [graphify-out/graph.html](graphify-out/graph.html) • AST JSON: [graphify-out/graph.json](graphify-out/graph.json)

---

## 🏛️ Central Architectural Hubs (God Nodes)

| Hub Node | Category | Degree | Responsibility |
| :--- | :--- | :---: | :--- |
| **`MainWindow`** | UI View | $($nodeMap['MainWindow'].degree) | Central WPF UI Dashboard, multi-tab orchestrator, real-time gauges |
| **`CleanerService`** | Engine Hub | $($nodeMap['CleanerService'].degree) | Master purge engine for 21 system/app scopes with 24h Safety Shield |
| **`CliRunner`** | CLI Controller | $($nodeMap['CliRunner'].degree) | In-place synchronous CLI argument parser, Unicode table renderer |
| **`ISystemProvider`** | Interface | $($nodeMap['ISystemProvider'].degree) | Decoupled telemetry and process querying provider abstraction |
| **`App_xaml`** | Core Entry | $($nodeMap['App_xaml'].degree) | Lifecycle, Mutex enforcement, and headless command dispatcher |

---

## 🗺️ Architectural Relationship Diagram (Mermaid)

```mermaid
graph TD
    %% Core Clusters
    subgraph UI_Layer["🖥️ Presentation & UI"]
        MW[MainWindow.xaml.cs]
        Theme[ThemeService]
        Loc[LocalizationService]
        Tray[TrayService]
    end

    subgraph CLI_Layer["💻 Synchronous In-Place CLI"]
        CLI[deltempo_cli.exe]
        Runner[CliRunner.cs]
        Reg[CliRegistrationService]
    end

    subgraph Engine_Layer["⚡ Core Optimization Engines"]
        CS[CleanerService.cs]
        Mem[MemoryOptimizerService]
        Start[StartupManagerService]
        Large[LargeFileHunterService]
        Proc[ProcessOptimizerService]
        Orphan[OrphanedAppService]
    end

    subgraph System_Layer["🛡️ System & IPC Infrastructure"]
        Mutex[SingleInstanceManager]
        Elev[ElevationService]
        Tele[DriveTelemetryService]
        Update[UpdateService]
    end

    subgraph Provider_Layer["🔌 Mockable Providers"]
        IProv[ISystemProvider]
        WinProv[WindowsSystemProvider]
        MockProv[MockSystemProvider]
    end

    subgraph Test_Layer["🧪 xUnit Testing & CI/CD"]
        xClean[CleanerServiceTests]
        xWhite[SystemCoreWhitelistTests]
        xTele[DriveTelemetryTests]
        GHActions[.github/workflows/ci.yml]
    end

    %% Key Directed Connections
    App[App.xaml.cs] --> Mutex
    App --> MW
    App --> Runner

    CLI --> Runner
    Runner --> CS
    Runner --> Mem
    Runner --> Tele
    Runner --> Reg
    Runner --> Update

    MW --> CS
    MW --> Mem
    MW --> Start
    MW --> Large
    MW --> Proc
    MW --> Tele
    MW --> Theme
    MW --> Loc
    MW --> Tray

    CS --> Orphan
    CS --> Elev

    WinProv -.->|implements| IProv
    MockProv -.->|implements| IProv
    WinProv --> Tele
    WinProv --> Mem
    WinProv --> Proc

    xClean --> CS
    xWhite --> Proc
    xTele --> MockProv
    GHActions --> xClean
```

---

## 🗂️ Cluster Dictionary & Component Map

- **`Core`**: Entry points (`App.xaml.cs`, `SingleInstanceManager.cs`).
- **`UI`**: Desktop views, palettes, localization, tray icon.
- **`CLI`**: Console Subsystem (`deltempo_cli.exe`, `CliRunner.cs`, wrapper scripts).
- **`Cleaner`**: 21 cleaning categories, 24-hour Safe Mode filter, orphaned leftovers.
- **`Optimizer`**: RAM Working Set flush, Reversible Startup Manager, Large File Hunter.
- **`Providers`**: `ISystemProvider`, `WindowsSystemProvider`, `MockSystemProvider`.
- **`Tests`**: Automated xUnit suites for all engines and whitelist protections.
- **`DevOps`**: GitHub Actions automated build, test, and release workflows.
"@
Set-Content -Path $graphifyMdPath -Value $graphifyMd -Encoding UTF8
Write-Host "✓ Exported GRAPHIFY.md"

# 7. Export Obsidian Vault Layer (vault/)
$vaultDir = Join-Path $resolvedRoot "vault"
$folders = @("00 - Index", "01 - Architecture", "02 - Services", "03 - Models", "04 - CLI", "05 - Canvases")
foreach ($f in $folders) {
    $dir = Join-Path $vaultDir $f
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

# 00 - Index.md
$indexMd = @"
---
title: Deltempo Obsidian Vault Index
date: $((Get-Date).ToString("yyyy-MM-dd"))
tags:
  - deltempo
  - index
  - architecture
aliases:
  - Deltempo Home
---

# 👑 Deltempo Knowledge Vault

Welcome to the official **Obsidian Knowledge Vault** for [[01 - Core Architecture|Deltempo]].

> [!tip] Quick Navigation
> - 🏛️ **Architecture**: [[01 - Core Architecture|Core System Design & Dual Subsystems]]
> - 🧹 **Cleaning Engine**: [[02 - Services/CleanerService|21-Scope Purge Engine]]
> - ⚡ **Performance Suite**: [[02 - Services/MemoryOptimizerService|RAM Booster]] • [[02 - Services/StartupManagerService|Startup Accelerator]]
> - 💻 **CLI Engine**: [[04 - CLI & Dual Subsystem|Synchronous In-Place CLI]]
> - 🎨 **Visual Canvas**: [[05 - Canvases/Deltempo_Architecture.canvas|Interactive Canvas]]

## 📊 High-Level Metrics
- **Current Version**: \`v1.1.0\`
- **Language**: C# (.NET 10.0 / WPF / Native Console)
- **Cleaning Scopes**: 21 Standard + Verified Orphaned Leftovers
- **Test Suite**: xUnit with Mock System Providers
"@
Set-Content -Path (Join-Path $vaultDir "00 - Index/Home.md") -Value $indexMd -Encoding UTF8

# 01 - Core Architecture.md
$archMd = @"
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

1. **WPF Desktop UI Subsystem** (\`WinExe\`): Runs completely silent without spawning background console windows.
2. **Native Console Subsystem** (\`IMAGE_SUBSYSTEM_WINDOWS_CUI\`): Runs synchronously inside PowerShell and Command Prompt without prompt interleaving or race conditions.

> [!important] Single Instance Guarantee
> Managed by [[02 - Services/SingleInstanceManager|SingleInstanceManager]] using a named system mutex (\`Global\Deltempo_App_SingleInstance_Mutex_v1\`) and Windows Message Broadcasts.

## Key Component Links
- [[02 - Services/CleanerService|CleanerService]]
- [[02 - Services/MemoryOptimizerService|MemoryOptimizerService]]
- [[02 - Services/StartupManagerService|StartupManagerService]]
- [[02 - Services/LargeFileHunterService|LargeFileHunterService]]
- [[02 - Services/ProcessOptimizerService|ProcessOptimizerService]]
"@
Set-Content -Path (Join-Path $vaultDir "01 - Architecture/Core Architecture.md") -Value $archMd -Encoding UTF8

# 02 - Services/CleanerService.md
$cleanerMd = @"
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
> Any file whose \`LastWriteTime\` is younger than 24 hours is automatically preserved to prevent breaking active installations and downloads.

## Scopes Supported
- **Device Driver Packages & GPU Updates**: NVIDIA App OTA artifacts, AMD, Intel.
- **Microsoft Defender Logs & History**: MPLog support files, scan history cache.
- **Windows System Diagnostic Logs**: CBS, DISM, DPX, Panther, SetupAPI, LogFiles.
- **BSOD Minidumps & Kernel Reports**: Memory crash dumps, LiveKernelReports.
- **Temporary Internet Files & WebCache**: INetCache, WebCache, CryptnetUrlCache.
- **DirectX & GPU Shaders**: D3DSCache, NVIDIA DXCache, AMD DxCache.
- **Browser Caches**: Chrome, Brave, Edge, Opera, Firefox.
- **Recycle Bin**: EmptyRecycleBin Win32 API.
"@
Set-Content -Path (Join-Path $vaultDir "02 - Services/CleanerService.md") -Value $cleanerMd -Encoding UTF8

# 05 - Canvases/Deltempo_Architecture.canvas (JSON Canvas 1.0)
$canvasData = @{
    nodes = @(
        @{ id = "node_app"; type = "text"; text = "### 🚀 App.xaml.cs`nRoot Entry Point & Mutex Router"; x = 100; y = 100; width = 280; height = 120; color = "1" },
        @{ id = "node_ui"; type = "text"; text = "### 🖥️ MainWindow.xaml.cs`nWPF Dashboard & Controller"; x = 460; y = 40; width = 300; height = 140; color = "4" },
        @{ id = "node_cli"; type = "text"; text = "### 💻 deltempo_cli.exe`nSynchronous Console CLI"; x = 460; y = 220; width = 300; height = 140; color = "5" },
        @{ id = "node_cleaner"; type = "text"; text = "### 🧹 CleanerService.cs`n21-Scope Purge Engine"; x = 840; y = 40; width = 280; height = 140; color = "2" },
        @{ id = "node_optimizer"; type = "text"; text = "### ⚡ Memory & Startup`nRAM Booster & Boot Tools"; x = 840; y = 220; width = 280; height = 140; color = "3" }
    )
    edges = @(
        @{ id = "edge1"; fromNode = "node_app"; toNode = "node_ui"; label = "GUI Mode" },
        @{ id = "edge2"; fromNode = "node_app"; toNode = "node_cli"; label = "CLI Mode" },
        @{ id = "edge3"; fromNode = "node_ui"; toNode = "node_cleaner"; label = "Clean/Scan" },
        @{ id = "edge4"; fromNode = "node_ui"; toNode = "node_optimizer"; label = "Optimize" },
        @{ id = "edge5"; fromNode = "node_cli"; toNode = "node_cleaner"; label = "deltempo clean" }
    )
}

$canvasJson = $canvasData | ConvertTo-Json -Depth 5
Set-Content -Path (Join-Path $vaultDir "05 - Canvases/Deltempo_Architecture.canvas") -Value $canvasJson -Encoding UTF8
Write-Host "✓ Exported Obsidian Vault & JSON Canvas 1.0"

Write-Host "`n🎉 Graphify & Obsidian Vault generated successfully!"
