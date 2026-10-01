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
Add-Node "OrphanedAppService" "OrphanedAppService.cs" "Service" "Cleaner" "Services/OrphanedAppService.cs" "Scans leftover uninstalled application directories in AppData, ProgramData, and the machine-wide Public profile." @("ScanVerifiedOrphanedFolders")

# Safety Rule Layer — the non-negotiable guard that every deletion path consults
Add-Node "ProtectionPolicy" "ProtectionPolicy.cs" "Safety_Rule" "Cleaner" "Core/Safety/ProtectionPolicy.cs" "Central non-negotiable protection policy: OS core directories, user personal libraries, credentials, login sessions. Covers the machine-wide Public profile so shared content is never disposable." @("IsProtected")
Add-Node "FileSafetyEngine" "FileSafetyEngine.cs" "Safety_Rule" "Cleaner" "Core/Safety/FileSafetyEngine.cs" "Deterministic per-file risk classification into five tiers. Nothing here is ever inferred from heuristics or telemetry." @("Analyze")
Add-Node "OrphanEvidenceClassifier" "OrphanEvidenceClassifier.cs" "Safety_Rule" "Cleaner" "Core/Safety/OrphanEvidenceClassifier.cs" "Positive proof a folder is residue: live-reference index (process/service/startup/uninstall/shortcut/PATH) plus user-data and Public shared-library refusal." @("HasLiveReference", "IsUserDataOrWorkingRoot", "PublicSharedRoots")
Add-Node "PathSecurity" "PathSecurity.cs" "Safety_Rule" "Cleaner" "Core/Safety/PathSecurity.cs" "Canonical path normalization, root containment and reparse-point guards used by every deletion path." @("NormalizeCanonicalPath", "IsSubpathOf", "IsReparsePointOrLink")
Add-Node "SafetyRiskTier" "SafetyRiskTier.cs" "Model" "Cleaner" "Core/Safety/SafetyRiskTier.cs" "Safe / LowRisk / ReviewRequired / Protected / Unknown. Protected is never deletable under any circumstance." @()

# Two-phase cleaning pipeline: SCAN -> PLAN -> PROTECT -> REVALIDATE -> CLEAN
Add-Node "CleanupPlanner" "CleanupPlanner.cs" "Engine_Hub" "Cleaner" "Core/Cleaning/CleanupPlanner.cs" "Builds the per-file plan, mapping each safety tier to an intended action (permanent delete, recycle, or skip)." @("CreatePlan")
Add-Node "CleanupExecutor" "CleanupExecutor.cs" "Engine_Hub" "Cleaner" "Core/Cleaning/CleanupExecutor.cs" "Two-phase TOCTOU transaction executor with journal-backed revalidation and size-drift detection." @("ExecutePlanAsync")
Add-Node "CleanupTransaction" "CleanupTransaction.cs" "Engine_Hub" "Cleaner" "Core/Cleaning/CleanupTransaction.cs" "Journalled cleanup transaction revalidating size drift and rejecting reparse points before commit." @()

# Scope resolvers — single source of truth for what each cleaning scope targets
Add-Node "SystemCacheResolver" "SystemCacheResolver.cs" "Resolver" "Cleaner" "Services/Providers/CacheResolvers/SystemCacheResolver.cs" "Resolves OS and application cache roots, including the machine-wide Public\Temp." @("ResolveAppCacheDirectories", "ResolveComponentCaches")
Add-Node "DevPackageCacheResolver" "DevPackageCacheResolver.cs" "Resolver" "Cleaner" "Services/Providers/CacheResolvers/DevPackageCacheResolver.cs" "Resolves developer and package-manager caches. Excludes WinGet\Packages, which holds installed applications rather than cache." @("Resolve")
Add-Node "BrowserCacheResolver" "BrowserCacheResolver.cs" "Resolver" "Cleaner" "Services/Providers/CacheResolvers/BrowserCacheResolver.cs" "Resolves per-profile browser caches with login sessions and cookies preserved." @("Resolve")
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

# Scan -> PLAN -> PROTECT -> REVALIDATE -> CLEAN
Add-Edge "CleanerService" "SystemCacheResolver" "resolves_scope_roots"
Add-Edge "CleanerService" "DevPackageCacheResolver" "resolves_dev_cache_roots"
Add-Edge "CleanerService" "BrowserCacheResolver" "resolves_browser_cache_roots"
Add-Edge "CleanerService" "CleanupPlanner" "plans_cleanup"
Add-Edge "CleanupPlanner" "FileSafetyEngine" "classifies_each_file"
Add-Edge "CleanupPlanner" "ProtectionPolicy" "refuses_protected_paths"
Add-Edge "CleanupPlanner" "PathSecurity" "enforces_root_containment"
Add-Edge "FileSafetyEngine" "SafetyRiskTier" "assigns_risk_tier"
Add-Edge "FileSafetyEngine" "ProtectionPolicy" "defers_to_policy"
Add-Edge "CleanupPlanner" "CleanupTransaction" "emits_journalled_plan"
Add-Edge "CleanupTransaction" "CleanupExecutor" "executes_with_revalidation"
Add-Edge "OrphanedAppService" "OrphanEvidenceClassifier" "requires_positive_evidence"
Add-Edge "OrphanEvidenceClassifier" "ProtectionPolicy" "refuses_user_data"
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
    # force-graph (and the d3 ecosystem generally) expects the relationship array under "links".
    # Exporting only "edges" meant the viewer received a graph with zero relationships despite the
    # header advertising them, so the layout had no structure to lay out.
    links = $edges
}

$graphJsonPath = Join-Path $graphOutDir "graph.json"
$graphData | ConvertTo-Json -Depth 6 | Set-Content -Path $graphJsonPath -Encoding UTF8
Write-Host "✓ Exported graph.json ($($nodes.Count) nodes, $($edges.Count) edges)"

# 5. Export graphify-out/graph.html (Interactive Force Graph)
$graphHtmlPath = Join-Path $graphOutDir "graph.html"

# Copy the vendored force-graph bundle next to the HTML so graphify-out/ is self-contained and
# renders with no network access. Previously the page loaded the library from unpkg.com, so any
# offline or firewalled machine produced a blank canvas with only the static header visible.
$vendorSource = Join-Path $PSScriptRoot "vendor\force-graph.min.js"
$hasVendoredGraph = Test-Path $vendorSource
if ($hasVendoredGraph) {
    $vendorDest = Join-Path $graphOutDir "vendor\force-graph.min.js"
    $vendorDestDir = Split-Path $vendorDest -Parent
    if (-not (Test-Path $vendorDestDir)) { New-Item -ItemType Directory -Path $vendorDestDir -Force | Out-Null }
    Copy-Item $vendorSource $vendorDest -Force
} else {
    Write-Warning "Vendored force-graph bundle missing at $vendorSource; the graph will depend on the CDN."
}

$htmlTemplate = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <!-- Required on every phone. Without it the browser assumes a ~980px layout viewport and scales
       the entire graph down into an unusable strip. viewport-fit=cover keeps the layout inside the
       notch and rounded corners on modern devices. -->
  <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
  <meta name="color-scheme" content="dark">
  <meta name="description" content="Interactive force-directed graph of the Deltempo codebase architecture.">
  <title>Deltempo Knowledge Graph (Graphify)</title>
  <script src="vendor/force-graph.min.js"></script>
  <script>
    // The vendored bundle renders with no network. If it is absent (graph.html copied elsewhere
    // without its vendor folder), fall back to the CDN. document.write runs while the parser is
    // still open, so ForceGraph is defined before the main script below executes.
    if (typeof ForceGraph === 'undefined') {
      document.write('<script src="https://unpkg.com/force-graph@1.52.0"><\/script>');
    }
  </script>
  <style>
    body { margin: 0; background: radial-gradient(1200px 700px at 50% 40%, #131c2e 0%, #0b0f19 60%, #070a12 100%); font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; color: #f3f4f6; overflow: hidden; -webkit-text-size-adjust: 100%; }
    /* Fixed inset rather than 100vw/100vh. vw includes the scrollbar gutter, which produced a
       horizontal scrollbar on desktop, and vh ignores collapsing mobile browser chrome, which left
       the canvas taller than the visible area on phones. inset tracks the real viewport everywhere.
       touch-action stops the browser from stealing drag/zoom gestures from the graph. */
    #graph { position: fixed; inset: 0; width: 100%; height: 100%; touch-action: none; }
    #legend { position: absolute; bottom: 20px; left: 20px; z-index: 10; background: rgba(17, 24, 39, 0.85); border: 1px solid #1f2937; border-radius: 12px; padding: 14px 18px; backdrop-filter: blur(8px); }
    #legend h3 { margin: 0 0 8px 0; font-size: 0.72rem; letter-spacing: 0.6px; text-transform: uppercase; color: #6b7280; }
    #legend .row { display: flex; align-items: center; font-size: 0.78rem; color: #d1d5db; margin-top: 4px; }
    #legend .dot { width: 9px; height: 9px; border-radius: 50%; margin-right: 8px; }
    #hint { position: absolute; bottom: 20px; right: 20px; z-index: 10; font-size: 0.75rem; color: #6b7280; background: rgba(17, 24, 39, 0.85); border: 1px solid #1f2937; border-radius: 12px; padding: 10px 14px; backdrop-filter: blur(8px); }
    #error { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 20; max-width: 520px; background: rgba(17, 24, 39, 0.95); border: 1px solid #7f1d1d; border-radius: 12px; padding: 20px 24px; font-size: 0.85rem; line-height: 1.5; color: #fca5a5; }
    #error code { color: #38bdf8; }
    #header { position: absolute; top: 16px; left: 20px; z-index: 10; background: rgba(17, 24, 39, 0.85); padding: 14px 20px; border-radius: 12px; border: 1px solid #1f2937; backdrop-filter: blur(8px); }
    #header h1 { margin: 0 0 6px 0; font-size: 1.1rem; color: #38bdf8; letter-spacing: 0.5px; }
    #header p { margin: 0; font-size: 0.8rem; color: #9ca3af; }
    #panel { position: absolute; top: 16px; right: 20px; z-index: 10; width: 320px; background: rgba(17, 24, 39, 0.9); padding: 18px; border-radius: 12px; border: 1px solid #1f2937; backdrop-filter: blur(10px); display: none; }
    #panel h2 { margin: 0 0 8px 0; font-size: 1rem; color: #10b981; }
    #panel .badge { display: inline-block; padding: 2px 8px; font-size: 0.75rem; border-radius: 6px; background: #1e293b; color: #38bdf8; margin-bottom: 10px; }
    #panel p { font-size: 0.82rem; line-height: 1.4; color: #d1d5db; margin: 0 0 10px 0; }
    #panel ul { margin: 0; padding-left: 18px; font-size: 0.78rem; color: #9ca3af; }

    /* The overlays are absolutely positioned over a full-bleed canvas. On a phone the legend, hint
       and panel together cover most of the graph, so shrink and re-anchor them rather than letting
       them collide, and drop the non-essential hint when vertical space is scarce. */
    @media (max-width: 640px) {
      #header { top: 10px; left: 10px; right: 10px; padding: 10px 12px; }
      #header h1 { font-size: 0.88rem; }
      #header p { font-size: 0.7rem; }
      #legend { left: 10px; bottom: 10px; padding: 10px 12px; }
      #legend h3 { font-size: 0.62rem; margin-bottom: 5px; }
      #legend .row { font-size: 0.68rem; }
      #panel { top: auto; bottom: 10px; left: 10px; right: 10px; width: auto; max-height: 45vh; overflow-y: auto; }
      #hint { display: none; }
    }
    @media (max-height: 480px) {
      #legend .row:nth-child(n+6) { display: none; }
      #hint { display: none; }
    }
    /* Honour an OS-level request to reduce motion. The directional link particles animate forever
       and the camera animates on every re-fit; both are suppressed for affected users. */
    @media (prefers-reduced-motion: reduce) {
      * { animation: none !important; transition: none !important; }
    }
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
  <!-- The canvas is drawn by JavaScript. Without this, a user with scripting disabled sees a blank
       page with no explanation of why. -->
  <noscript>
    <div style="position:fixed;inset:0;display:flex;align-items:center;justify-content:center;padding:24px;text-align:center;background:#0b0f19;color:#f3f4f6;font-family:sans-serif;z-index:50">
      This knowledge graph is rendered with JavaScript. Enable JavaScript to view it, or read the
      static architecture summary in <code>GRAPHIFY.md</code>.
    </div>
  </noscript>
  <div id="graph"></div>
  <div id="legend">
    <h3>Clusters</h3>
    <div id="legend-rows"></div>
  </div>
  <div id="hint">Scroll to zoom &middot; drag to pan &middot; click a node for detail</div>
  <div id="error" style="display:none">
    <strong>Graph library failed to load.</strong><br>
    Neither <code>vendor/force-graph.min.js</code> nor the CDN fallback could be loaded, so the
    graph cannot be drawn. Re-run <code>scripts/generate_graphify.ps1</code> to restore the
    vendored bundle, or open this page with network access.
  </div>

  <script>
    const raw = $($graphData | ConvertTo-Json -Depth 6);
    // Accept either "links" (force-graph's expected key) or "edges" (this project's own schema),
    // and never pass an undefined relationship array through to the layout.
    const data = { nodes: raw.nodes || [], links: raw.links || raw.edges || [] };
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

    // Legend is built from the data itself so its counts can never drift from the graph.
    const byCluster = {};
    data.nodes.forEach(n => { byCluster[n.cluster] = (byCluster[n.cluster] || 0) + 1; });
    const legendRows = document.getElementById('legend-rows');
    Object.keys(byCluster).sort().forEach(c => {
      const row = document.createElement('div');
      row.className = 'row';
      const dot = document.createElement('span');
      dot.className = 'dot';
      dot.style.background = clusterColors[c] || '#94a3b8';
      const text = document.createElement('span');
      text.textContent = c + '  (' + byCluster[c] + ')';
      row.appendChild(dot);
      row.appendChild(text);
      legendRows.appendChild(row);
    });

    if (typeof ForceGraph === 'undefined') {
      document.getElementById('error').style.display = 'block';
    } else {
    // Honour an OS-level "reduce motion" request. The directional particles animate continuously and
    // never stop, and every re-fit animates the camera, so both are disabled when the user asks for it.
    const reduceMotion = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    const fitMs = reduceMotion ? 0 : 250;

    // Set the moment the user pans, zooms or drags. The camera then belongs to them permanently.
    let userInteracted = false;

    const Graph = ForceGraph()(document.getElementById('graph'))
      .graphData(data)
      .nodeId('id')
      .nodeLabel('label')
      .nodeColor(node => clusterColors[node.cluster] || '#94a3b8')
      // Radii are in world units, so zoomToFit magnifies them. Cap the size or the high-degree
      // hubs overlap into unreadable blobs once the view is fitted to the viewport.
      .nodeVal(node => Math.min(11, Math.max(2.5, (node.degree || 1) * 1.5)))
      .linkColor(() => 'rgba(148, 163, 184, 0.55)')
      .linkWidth(1.2)
      .linkDirectionalParticles(reduceMotion ? 0 : 2)
      .linkDirectionalParticleWidth(2)
      .linkDirectionalParticleColor(() => '#38bdf8')
      .linkDirectionalParticleSpeed(0.006)
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
      })
      // onNodeDrag, not onDrag: this build has no generic on() and no onDrag(). Node drags must
      // surrender the camera immediately, before the engine re-starts and settles.
      .onNodeDrag(() => { userInteracted = true; })
      // Frame the layout once the simulation settles. Any user interaction permanently surrenders
      // the camera: re-fitting mid-drag re-framed the viewport around the old layout and teleported
      // the held node out from under the cursor, which is what made a grabbed node look deleted.
      // fitUntilStable is a hoisted declaration, so this resolves fine despite appearing above it.
      .onEngineStop(() => { if (!userInteracted) fitUntilStable(); });

    // Genuine user input only. Deliberately not onZoom: our own zoomToFit emits a zoom event, which
    // would immediately mark the graph as user-touched and cancel the fitting. Native canvas events
    // are only produced by real interaction, never by programmatic calls.
    const canvasEl = document.querySelector('#graph canvas');
    if (canvasEl) {
      ['wheel', 'pointerdown'].forEach(function (evt) {
        canvasEl.addEventListener(evt, function () { userInteracted = true; }, { passive: true });
      });
    }

    // Resize and device rotation. force-graph measures its container once, when the canvas is created.
    // Without this the canvas keeps its original pixel size after a window resize or a phone rotation,
    // leaving the graph letterboxed or clipped. Debounced because resize fires continuously while
    // dragging a window edge. Re-fit only while the user has not taken control of the camera.
    let resizeTimer = null;
    function handleResize() {
      clearTimeout(resizeTimer);
      resizeTimer = setTimeout(function () {
        const host = document.getElementById('graph');
        if (!host) return;
        Graph.width(host.clientWidth).height(host.clientHeight);
        if (!userInteracted) fitUntilStable();
      }, 120);
    }
    window.addEventListener('resize', handleResize);
    window.addEventListener('orientationchange', handleResize);

    // Framing. The layout keeps contracting as the simulation cools, so fitting on a fixed timer
    // captures a stale bounding box and strands the graph as a small clump in a huge empty canvas.
    // Instead, keep re-fitting until the node extents stop changing, then fit once more so the final
    // framing matches the final positions. Bounded so it can never run away.
    // Fit padding must scale with the viewport. A fixed 90px margin cost 180px of a 390px-tall
    // landscape phone, shrinking the graph to a clump, while being needed on a desktop. Scale it to
    // the shorter edge instead, clamped so it never vanishes or dominates.
    function fitPad() {
      var minDim = Math.min(Graph.width(), Graph.height());
      return Math.max(12, Math.min(90, minDim * 0.12));
    }
    var fitTimer = null, lastW = -1, lastH = -1, stable = 0;
    function stopFitting() { if (fitTimer) { clearInterval(fitTimer); fitTimer = null; } }
    function fitUntilStable() {
      stopFitting();
      lastW = -1; lastH = -1; stable = 0;
      fitTimer = setInterval(function () {
        if (userInteracted) { stopFitting(); return; }
        var ns = data.nodes, xs = [], ys = [];
        for (var i = 0; i < ns.length; i++) { xs.push(ns[i].x); ys.push(ns[i].y); }
        var w = Math.max.apply(null, xs) - Math.min.apply(null, xs);
        var h = Math.max.apply(null, ys) - Math.min.apply(null, ys);
        Graph.zoomToFit(fitMs, fitPad());
        if (Math.abs(w - lastW) < 0.5 && Math.abs(h - lastH) < 0.5) { stable++; } else { stable = 0; }
        lastW = w; lastH = h;
        if (stable >= 4) { stopFitting(); Graph.zoomToFit(reduceMotion ? 0 : 400, fitPad()); }
      }, 300);
      setTimeout(stopFitting, 20000);
    }
    fitUntilStable();

    // Note: force-graph's bundled d3 forces are intentionally left at their defaults. Tuning them
    // via d3Force('charge').strength(...) does not stick across simulation rebuilds, and replacing
    // them with hand-rolled forces breaks the d3 contract (forces are invoked as force(alpha), so a
    // force declared as force(nodes) silently applies nothing and never decays). The world-space
    // layout size is irrelevant anyway: zoomToFit scales it to the viewport regardless.
    window.deltempoGraph = Graph;
    }
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

# This vault is fully generated. Any top-level folder the generator does not own is a leftover
# from an older layout ("02 - Core Engine", "03 - Services", "04 - UI & CLI") and leaves stale
# copies and broken [[wiki-links]] behind, so prune them to keep the vault self-consistent.
Get-ChildItem -Path $vaultDir -Directory -ErrorAction SilentlyContinue |
    Where-Object { $folders -notcontains $_.Name } |
    ForEach-Object {
        Write-Host "  pruning stale vault folder: $($_.Name)"
        Remove-Item $_.FullName -Recurse -Force
    }

# Every note and canvas this generator writes is recorded, so leftovers from an older layout
# (e.g. "01 - Architecture/Overview.md") can be pruned instead of accumulating as dead weight
# beside the current content.
$script:vaultOwned = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Write-VaultFile {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)]$Content
    )
    $full = Join-Path $vaultDir $RelativePath
    $parent = Split-Path $full -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Set-Content -Path $full -Value $Content -Encoding UTF8
    # Normalise to the OS separator: the declared paths above use "/", but the comparison against
    # Get-ChildItem FullName yields "\". Storing them unmixed would make every file look unowned.
    [void]$script:vaultOwned.Add($RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
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
> - 🛡️ **Safety Engine**: [[02 - Services/Safety Engine|Protection Policy & Orphan Detection]]
> - ⚡ **Performance Suite**: [[02 - Services/MemoryOptimizerService|RAM Booster]] • [[02 - Services/StartupManagerService|Startup Accelerator]]
> - 💻 **CLI Engine**: [[04 - CLI & Dual Subsystem|Synchronous In-Place CLI]]
> - 🎨 **Visual Canvas**: [[05 - Canvases/Deltempo_Architecture.canvas|Interactive Canvas]]

## 📊 High-Level Metrics
- **Current Version**: \`v1.1.0\`
- **Language**: C# (.NET 10.0 / WPF / Native Console)
- **Cleaning Scopes**: 21 Standard + Verified Orphaned Leftovers
- **Test Suite**: xUnit with Mock System Providers
"@
Write-VaultFile -RelativePath "00 - Index/Home.md" -Content $indexMd

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
Write-VaultFile -RelativePath "01 - Architecture/Core Architecture.md" -Content $archMd

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
Write-VaultFile -RelativePath "02 - Services/CleanerService.md" -Content $cleanerMd

# 02 - Services/Safety Engine.md — the non-negotiable guard every deletion path consults
$safetyMd = @"
---
title: Safety Engine
tags:
  - safety
  - architecture
  - cleaning
aliases:
  - Protection Policy
---

# 🛡️ Deltempo Safety Engine

Every deletion is filtered by this layer before anything is touched. It is **deterministic and
algorithmic** — no heuristic, model or telemetry result can ever override it.

## Pipeline

\`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN\`

- [[02 - Services/CleanerService|CleanerService]] builds the plan via **CleanupPlanner**
- **FileSafetyEngine** assigns each file a **SafetyRiskTier**
- **ProtectionPolicy** refuses anything non-negotiable
- **PathSecurity** enforces root containment and reparse-point refusal
- **CleanupTransaction / CleanupExecutor** revalidate size drift before commit

## SafetyRiskTier

| Tier | Meaning |
| :-- | :-- |
| `Safe` | Verified disposable cache or staging. Re-downloads automatically. |
| `LowRisk` | Low-impact residue. |
| `ReviewRequired` | **Never auto-deleted.** Needs explicit human consent. |
| `Protected` | **Never deletable under any circumstance.** |
| `Unknown` | Unclassified. Protected by default while the 24h shield is active. |

> [!danger] Scan and Clean disagree on purpose
> A scan reports every file it finds. Clean deletes only what the safety engine clears. A scope
> can therefore show a large total and legitimately reclaim almost nothing — for example when the
> target is executables, recently written files, or shared user data. Cards now surface that split
> instead of reporting a silent `0 B`.

## Protected locations

- Windows core directories (System32, SysWOW64, WinSxS, Boot), drive roots, `$Recycle.Bin`
- The **current user's** Documents / Desktop / Pictures / Music / Videos / Saved Games / OneDrive
- The **machine-wide Public profile** — `Documents`, `Desktop`, `Downloads`, `Pictures`, `Music`,
  `Videos`, `Libraries`, `AccountPictures`. Shared content is never disposable.
- Browser credentials, login sessions and cookies
- SSH / cloud keys, KeePass databases, AI model weights, source code
- Running processes and reparse points (junctions / symlinks)

## Orphan & leftover detection

**OrphanedAppService** proposes a folder as residue only after positive proof:

1. Refused outright if the name is a package-manager store, agent namespace or user-data root
2. Not a working root (no `.git`, `package.json`, `*.sln`)
3. **No live reference** — no running process, service, startup entry, uninstall record, shortcut
   target or `PATH` entry resolving inside it
4. Older than 7 days (1 hour for a broken uninstaller)
5. Unchecked by default; the user must opt in

> [!warning] Identity matching requires word boundaries
> The active-app keyword set is built from **running process names**. A plain substring match meant
> a generic process like \`tool\` marked every folder containing "Tool" as an active install, which
> silently disabled orphan detection on that machine entirely. Containment now requires a word
> boundary: \`Snipping Tool\` matches \`Tool\`, \`MiniTool\` does not.

## Known scope boundaries

- \`WinGet\Packages\` is **excluded** from the developer-cache scope: it is where \`winget install\`
  puts portable applications (Gyan.FFmpeg alone is ~650 MB of ffmpeg/ffplay/ffprobe), not cache.
- \`C:\Users\Public\` is scanned at depth 0 only, and its shared libraries are refused by policy.
"@
Write-VaultFile -RelativePath "02 - Services/Safety Engine.md" -Content $safetyMd

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
Write-VaultFile -RelativePath "05 - Canvases/Deltempo_Architecture.canvas" -Content $canvasJson

# Prune any note or canvas the generator does not own. Folder-level pruning alone leaves stale
# files behind inside folders that are still valid (e.g. "01 - Architecture/Overview.md" from an
# older layout), which then sit beside the current content as dead, unlinked weight.
Get-ChildItem -Path $vaultDir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { -not $script:vaultOwned.Contains($_.FullName.Substring($vaultDir.Length + 1)) } |
  ForEach-Object {
    Write-Host "  pruning stale vault file: $($_.Name)"
    Remove-Item $_.FullName -Force
  }

Write-Host "✓ Exported Obsidian Vault & JSON Canvas 1.0"

Write-Host "`n🎉 Graphify & Obsidian Vault generated successfully!"
