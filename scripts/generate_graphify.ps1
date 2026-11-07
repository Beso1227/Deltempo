# Regenerate the Deltempo code knowledge graph with the upstream `graphify` CLI.
#
# Replaces the hand-written generator this file used to contain. That version
# emitted a fixed ~40-node graph from hardcoded nodes and edges, with pre-restructure
# paths (`Services/CleanerService.cs`, `Core/Safety/…`), and it overwrote
# graphify-out/graph.json, graphify-out/graph.html and vault/ with that content --
# silently replacing the real extracted graph (7000+ nodes) with something that only
# described the old layout. Nothing referenced it, so it is now a wrapper around the
# tool that actually extracts the graph. The previous revision stays in git history.

param(
    [string]$RootPath = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Continue'

$resolvedRoot = (Resolve-Path $RootPath).Path
$graphify = Get-Command graphify -ErrorAction SilentlyContinue
if (-not $graphify) {
    Write-Error @'
graphify CLI not found on PATH.

Install it first (see PROJECT_MAP.md, "Rebuilding"), then re-run this script.
Nothing was written, so graphify-out/ still holds the previous graph.
'@
    exit 1
}

Write-Host "Regenerating the code graph at $resolvedRoot ..."
& $graphify.Source update $resolvedRoot --force
if ($LASTEXITCODE -ne 0) {
    Write-Error "graphify exited with code $LASTEXITCODE; graphify-out/ may be incomplete."
    exit $LASTEXITCODE
}

$graphJson = Join-Path $resolvedRoot 'graphify-out\graph.json'
if (Test-Path $graphJson) {
    $sizeKb = [math]::Round((Get-Item $graphJson).Length / 1KB, 0)
    Write-Host "Wrote $graphJson ($sizeKb KB)"
}
Write-Host 'Also updated: graphify-out/graph.html, graphify-out/GRAPH_REPORT.md'
