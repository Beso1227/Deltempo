# Deltempo — Project Map

Read this before grepping. It is the shortest path from "where does X live" to the
file that owns X, plus the rules that must not be broken while you are there.

Verified at commit `0713664` (2026-11-09), app version **3.3.0** (release prep; tag pending).
Nothing here is aspirational: every path and count below is checkable, and the
place to check is `.ua/knowledge-graph.json` (see *Querying*).

## What this is

Deltempo is a Windows 10/11 cleaner and memory optimizer. It scans registered cache
scopes, classifies every file with a deterministic safety engine, and deletes only
what clears it. One `Core` engine, three front ends: the shipped WPF GUI, a WinUI 3
shell kept in parity with it, and a headless CLI.

## Projects (`deltempo.sln`)

| Project | Path | Kind | Role |
| --- | --- | --- | --- |
| `Deltempo.Core` | `src/Deltempo.Core` | library | Engine: cleaning, scanning, safety, discovery, update, services |
| `WinTempCleaner` (WPF) | `src/Deltempo.App.Wpf` | `WinExe` | **Shipped GUI** — published as `Deltempo.exe` |
| `Deltempo.App.WinUI` | `src/Deltempo.App.WinUI` | `WinExe` | WinUI 3 shell in parity preview |
| `Deltempo.App.WinUI.ViewModels` | `src/Deltempo.App.WinUI.ViewModels` | library | MVVM view models for the WinUI shell |
| `Deltempo.Cli` | `src/Deltempo.Cli` | `Exe` | Headless host — published as `deltempo_cli.exe` |
| `Deltempo.Tests` | `tests/Deltempo.Tests` | test | xunit.v3 suite; references Core + ViewModels |

Dependencies are flat and one-directional: every front end references `Core`, and
only the WinUI shell references `ViewModels`.

## Layers (8, from the knowledge graph)

| Layer | Nodes | What belongs there |
| --- | --- | --- |
| `core-library` | 125 | `src/Deltempo.Core` — platform-agnostic engine |
| `winui-shell` | 63 | `src/Deltempo.App.WinUI` — XAML views, view models, bootstrap |
| `wpf-shell` | 31 | `src/Deltempo.App.Wpf` — shipped GUI, modal views, converters |
| `cli` | 3 | `src/Deltempo.Cli` — command host |
| `ci-automation` | 37 | `.github/`, `scripts/` — workflows, build/test/release tooling |
| `packaging` | 4 | `packaging/` — MSIX manifest, WinGet manifests |
| `documentation` | 18 | README translations, SECURITY, CONTRIBUTING, notices |
| `build-configuration` | 10 | Solution, MSBuild props, package pins, editor config, entry shims |

## Entry points

| Path | Reached from |
| --- | --- |
| `deltempo.cmd`, `deltempo.ps1` | Shims that forward every argument to `deltempo_cli.exe` and preserve its exit code |
| `src/Deltempo.Cli/Program.cs` | The CLI behind both shims |
| `src/Deltempo.App.Wpf/App.xaml.cs` | Shipped GUI startup → `MainWindow.xaml.cs` |
| `src/Deltempo.App.WinUI/App.xaml.cs` | Preview shell startup; `--self-test` runs the CI smoke gate (`ShellDiagnostics.cs`) |
| `.github/workflows/ci.yml` | Required status check **Build, Test & Quality Gate** |

## Invariants

1. **Safety is a denylist, never a whitelist.** `ProtectionPolicy` refuses known-bad
   locations; every change adds a refusal, never an exception. Recursive deletes must
   ask `ProtectionPolicy.TryGetProtectedDescendant` first (`RootLeftoverPurgeService`
   is the caller that proves why: `Directory.Delete(recursive: true)` removes a
   subtree in one syscall, so a per-file check never runs).
2. **Version lives only in `Directory.Build.props`.** `Version`, `FileVersion` and
   `AssemblyVersion` are set there; project files must not override them.
3. **The published test count must agree everywhere.** The number appears in the
   README badge and prose, its 8 translations, `SECURITY.md`, `docs/ai-catalog.json`,
   `docs/llms.txt`, `docs/llms-full.txt`, `docs/index.html` and the changelog's
   *current* card. `DocumentationConsistencyTests` fails the build when any of them
   drift. Bump all current claims together.
4. **Format the solution, not a project.** `dotnet format deltempo.sln
   --verify-no-changes --no-restore` — a per-project run covers a third of the code.
5. **The suite runs as an executable.** xunit.v3 on Microsoft.Testing.Platform cannot
   be driven by `dotnet test` on the .NET 10 SDK: build the test project, then run
   `Deltempo.Tests.exe`.
6. **`docs/` is published to the site.** Internal notes, drafts and agent scratch go
   to `.internal/` (gitignored), never into `docs/`.

## Commands

```powershell
dotnet build deltempo.sln -c Release
dotnet format deltempo.sln --verify-no-changes --no-restore
dotnet build tests\Deltempo.Tests\Deltempo.Tests.csproj -c Release
tests\Deltempo.Tests\bin\Release\net10.0-windows\Deltempo.Tests.exe --filter "Category!=Benchmark"
pwsh -ExecutionPolicy Bypass -File scripts\build_release_exe.ps1   # publish\Deltempo.exe + deltempo_cli.exe
```

## Where knowledge lives

| Artifact | Answers | Freshness marker |
| --- | --- | --- |
| `.ua/knowledge-graph.json` | file and symbol summaries, layers, guided tour | `.ua/meta.json` → `gitCommitHash` |
| `.ua/fingerprints.json` | which files changed since the last analysis | — |
| `graphify-out/graph.json` + `GRAPH_REPORT.md` | call graph, communities, god nodes, PR impact | `.graphify_build_state.json` → built-at commit |
| `graphify-out/graph.html` | interactive viewer (needs network: vis-network from unpkg) | regenerated with the graph |
| `.archify/architecture-*/deltempo-architecture.html` | one-page architecture picture: 3 shells → Core → engines → OS surfaces, with the refusal path and the update path | `candidate.json` → `meta.repository.revision` |
| `.megamemory/` (MCP `megamemory`) | decisions, hard-won patterns, why things are the way they are | per concept |
| `CONTEXT.md` | canonical domain vocabulary | manual |
| `docs/` | the published site (source, not notes) | — |

The site is published from `docs/` to `https://beso1227.github.io/Deltempo/`.
GitHub Pages serves the *domain root* of an account from exactly one
repository — the one named after the account — so the bare domain
`beso1227.github.io/` belongs to `Beso1227/Beso1227.github.io`, not to this
repository. That repository existed until 2026-11-10, holding the domain-root
`robots.txt`, a sitemap index and a redirect to the project site. It was
deleted rather than kept in sync, for three reasons: nothing links to the bare
domain (every canonical URL, sitemap entry and the install one-liner carry the
`/Deltempo/` path), the project already serves its own `robots.txt` and
`sitemap.xml` from `docs/`, and a sitemap index exists to describe several
sites on one domain - this account has one. The bare domain therefore shows
GitHub's "no site here" page, and that is the whole cost.

If the project ever takes its own domain, the arrangement changes: a `CNAME` in
`docs/` makes this repository serve the domain root, `docs/robots.txt` becomes
the root policy, and the canonical URLs should follow the new domain.

`.archify/` is the only one of these that is **gitignored**: it is agent output from the
`archify` skill (no CLI, nothing on `PATH` — invoke the skill). The artifact that matters is
the HTML, and it is self-contained: inline SVG and CSS, one external link that is only the
upstream credit. Its own gate covers 1440/1600/1920/2048 in light and dark with
`overflowX: false` at every viewport, so it opens straight from disk. It describes topology,
not per-file detail — re-run the skill when the components or the arrows between them change,
not on every commit. Current run: 21 components, 20 relationships, 3 cards, pinned to the
revision recorded in its `candidate.json`.

## Querying

```powershell
# Understand-Anything graph — no dependencies
node .ua\ua-query.mjs stats
node .ua\ua-query.mjs file CleanupPlanner
node .ua\ua-query.mjs layer core-library
node .ua\ua-query.mjs search "safety shield"
node .ua\ua-query.mjs edges file:src/Deltempo.Core/Cleaning/CleanupPlanner.cs
node .ua\ua-query.mjs tour
```

Through MCP (graphify server), against the same directory:

- `graph_stats`, `god_nodes`, `get_community`, `get_node`, `get_neighbors`
- `query_graph` (natural language, BFS/DFS), `shortest_path`
- `list_prs`, `triage_prs`, `get_pr_impact`

Through MCP (megamemory server): `list_roots` at session start, `understand` before a
task, `create_concept` / `update_concept` after one.

## Rebuilding

```powershell
graphify update . --force      # re-extract code, no LLM needed (~1 min, 7k+ nodes)
scripts\generate_graphify.ps1  # same thing, as a script
```

`/understand` (the Understand-Anything skill) rebuilds `.ua/` incrementally from
`.ua/fingerprints.json`; it is the only tool that writes the layer and tour data.

The architecture diagram is regenerated by the `archify` skill, not by a command: it takes a
`candidate.json` (components, connections, cards), delivers the HTML and then runs its own
validate → deliver → check → browser-check gates. Opening the HTML needs nothing — no build,
no server, no network.

**Staleness rule:** if `.ua/meta.json`'s `gitCommitHash` is not `HEAD`, the summaries
predate your checkout — `node .ua\ua-query.mjs stats` says so in one line. The same
check for graphify is at the top of `graph_stats` output. For archify, compare
`candidate.json` → `meta.repository.revision` with `git log`; because it only draws
components, a revision behind HEAD is normal and only matters if `src/` changed shape.
