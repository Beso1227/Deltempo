# Task: Repo reorganization + WinUI 3 feature parity + multi-arch shipping (Deltempo)

Repo: D:\Projects\deltempo (Windows, .NET 10, WPF + WinUI 3 + CLI + xunit.v3 tests).

Work directly on `main`. No feature branches (user decision 2026-10-17).
The WPF app (WinTempCleaner.csproj) stays the default shipping UI — do NOT change its behavior,
delete its features, or touch its publish path. This task ADDS to the WinUI app and reorganizes
the repo. Execute phases in strict order 0 → A → B → C. Each phase ends with the Part D gate
GREEN and its own Conventional Commit. Never batch a reorg move and a code change in one commit.

---

## Part 0 — Repository reorganization (ZERO behavior changes)

Problem: the WPF app sprawls at the repo root, shared code lives in root folders LINKED into Core
via `<Compile Include="..\Services\**">`, and build junk sits next to real sources. Target layout:

```
/
├── deltempo.sln, Directory.Build.props, Directory.Packages.props, global.json, .editorconfig
├── src/
│   ├── Deltempo.Core/        ← shared: Services\, Models\, ViewModels\, Assets\Sounds\ (moved IN, links deleted)
│   ├── Deltempo.App.Wpf/     ← old UI: WinTempCleaner.csproj, App.xaml(+.cs),
│   │                           MainWindow.xaml(+.cs + 8 MainWindow.*.cs partials),
│   │                           Converters\, Views\, app.manifest, app.ico
│   ├── Deltempo.App.WinUI/   ← new UI (moved as-is, unchanged)
│   └── Deltempo.Cli/         ← moved from Cli\
├── tests/Deltempo.Tests/     ← moved from Tests\
├── docs/, packaging/, scripts/, .github/
└── (no source files, exes, or stray scripts at repo root)
```

Steps, in order. Run the Part D gate after EACH sub-step, not at the end:

1. **Move shared code into Core physically.** `git mv` root `Services\`, `Models\`, `ViewModels\`
   and `Assets\Sounds\` into `src\Deltempo.Core\`. Then DELETE the `<Compile Include="..\Services\**">`,
   `..\Models\**`, `..\ViewModels\**` link groups from Deltempo.Core.csproj and repoint the four
   EmbeddedResource wav paths (`click/success/error/warning.wav`, LogicalName
   `WinTempCleaner.Assets.Sounds.*` must stay identical). Physical files beat virtual links —
   no project may compile another project's folder tree.
2. **Move the WPF app.** `git mv` App.xaml, App.xaml.cs, MainWindow.xaml, MainWindow.xaml.cs, all
   eight `MainWindow.*.cs` partials (Cleaning, LargeFiles, Memory, Settings, SystemManagers,
   SystemRepair, Ui, Updates), `Converters\`, `Views\`, `app.manifest`, `app.ico` and
   `WinTempCleaner.csproj` into `src\Deltempo.App.Wpf\`. Then DELETE the whole
   `<Compile/Page/ApplicationDefinition/EmbeddedResource/None Remove="...">` exclusion block in that
   csproj (it exists only because everything sat at root — deleting it is the payoff of this reorg).
3. **Move WinUI, CLI, Tests.** `git mv` → `src\Deltempo.App.WinUI\`, `src\Deltempo.Cli\`,
   `tests\Deltempo.Tests\`. Update all project paths in `deltempo.sln`.
4. **Fix every hard path reference** (verified to exist):
   - `.github\workflows\release.yml` — `dotnet publish WinTempCleaner.csproj` and
     `dotnet publish Cli\Deltempo.Cli.csproj` → new paths (keep all publish flags identical)
   - `scripts\build_release_exe.ps1` and any other scripts/*.ps1|*.cmd naming old paths
   - `tests\Deltempo.Tests\Deltempo.Tests.csproj` — the four `..\..\Converters\*.cs` links →
     `..\..\src\Deltempo.App.Wpf\Converters\*.cs`
   - `tests\...\PackagingConfigurationTests.cs` (asserts on the `WinTempCleaner.csproj` path)
   - README/CONTRIBUTING build & run examples (`.\bin\Debug\net10.0-windows\Deltempo.exe` becomes
     `src\Deltempo.App.Wpf\bin\Debug\net10.0-windows\Deltempo.exe`) across ALL 9 READMEs + docs.
     Check whether any DocumentationConsistencyTests guard asserts these paths; update the guard
     in the same commit if so.
5. **STABILITY CONTRACT — must NOT change** (load-bearing for the install script, UpdateService,
   checksums.sha256 and winget manifests):
   - `AssemblyName` stays `Deltempo` and `deltempo_cli` (exe filenames are contractual)
   - `RootNamespace` stays `WinTempCleaner` (resource lookup and serialization depend on it)
   - All `InternalsVisibleTo` names, `TargetFramework`s, LogicalName resource ids, and the
     test-exe path convention
6. **Clean the junk** (each verified as build output or stray before deleting):
   `WinTempCleaner_r2lo2qef_wpftmp.csproj` (stale wpftmp artifact), root `Deltempo.exe` and
   `deltempo_cli.exe` (published copies), `fix1.py`, `encoded.txt`, `ci_runs.json`,
   `__pycache__\`, and the gitignored `dist*/publish*/artifacts/TestResults` output dirs.
   `node_modules\`, `package.json`, `graphify-out\`, `vault\`: ASK the user before removing.
   `github.token`: NEVER commit, NEVER delete without asking (untracked + gitignored).
   Use `git mv` everywhere to preserve history. NO content edits inside reorg commits.
7. **Architecture hardening**: `Deltempo.Core` sets `UseWPF=true`. Grep every source Core compiles
   for `System.Windows`. If clean, remove `UseWPF` from Core so the WinUI app references a truly
   UI-free core, and rebuild. If not clean, list the remaining types and STOP — do not move types
   between assemblies in this task.

---

## Part A — Feature parity: fill every gap in Deltempo.App.WinUI

Current WinUI shell (Deltempo.App.WinUI\MainWindow.xaml.cs, `_workspacePanels` section map) has
7 sections: Cleaner, Large Files, Startup, Memory, System Repair, App Uninstaller, Force Delete.
Views already ported: CleanerWorkspaceView, LargeFilesWorkspaceView, StartupWorkspaceView,
MemoryWorkspaceView, MemoryOptimizerView, SystemRepairView, AppUninstallView, ForceDeleteView,
LockedFilesView, AppDataFootprintView.

Port these from the WPF app, in this order:

1. **Process Manager tool** — new `Views\ProcessWorkspaceView.xaml` wired into the section map.
   Reuse the shared services (ProcessOptimizerService, RestartManagerService) and honor Windows
   Core Process Immunity: never list or kill protected system processes.
2. **Settings panel** — port the WPF settings surface: SettingsService/AppSettings with its clamps
   (LowDiskAlertThresholdGb 1..500, AutoCleanIntervalHours 1..168) and the existing
   SettingsSecretProtector DPAPI path for AiApiKey. Never re-implement encryption, never log secrets.
3. **About panel** — version from the assembly/Directory.Build.props, license, repo/docs links.
4. **Language switcher** — reuse `LocalizationService.cs` exactly as the WPF ComboBox does
   (en, ar, es, fr, de, ...). No new localization system, no new resource files.
5. **Dark/Light theme toggle** — drive `Deltempo.Tokens.xaml` ThemeDictionaries (Light/Dark) and
   persist the choice in AppSettings.
6. **Haptics/sound toggle** — reuse Core's embedded sounds (`WinTempCleaner.Assets.Sounds.*`).
7. **Auto-clean scheduling** — wire to the existing AutoCleanService; keep its
   Interlocked.CompareExchange re-entrancy guard and StopAutoClean cancellation semantics intact.
8. **Update-check flow** — wire to UpdateService as a READ-ONLY status UI (current vs latest tag,
   a link that opens the GitHub releases page). No auto-install, no signature claims —
   THREAT_MODEL.md documents that manifest-signature verification is NOT enforced.

Non-negotiable rules for Part A:
- Services live in Core and are shared between both UIs. NEVER duplicate business logic in the
  WinUI UI layer, and never add `System.Windows.*` types to shared code.
- UI-thread marshaling goes through the existing UiDispatcher abstraction; each app supplies its
  own dispatcher implementation.
- XAML translation rules: `x:Bind` where possible; `VisualStateManager` instead of property
  `Triggers` (WinUI has no Setter.Triggers); `Microsoft.UI.Xaml.Data.IValueConverter`;
  `Flyout`/`TeachingTip` instead of `Popup`; `CornerRadius` instead of BorderThickness rounding;
  `DynamicResource` for theme tokens.
- Copy design VALUES from the WPF themes (hex brushes, spacing, radii, font sizes) into
  `Deltempo.Tokens.xaml`. Do NOT copy WPF XAML verbatim — it will not compile under WinUI.
- Every new behavior gets xunit.v3 tests in tests\Deltempo.Tests. Any new converter must be added
  to the test csproj via explicit per-file `<Compile Include>` to be testable.
- Do not modify WPF files except where a shared service signature genuinely forces it, and then
  only additively with the WPF build kept green.

---

## Part B — Multi-arch: separate x64 and arm64 builds, keep each artifact lean

Goal: match the WPF app's architecture coverage (x64 + arm64) WITHOUT one fat universal binary.
Ship TWO separate per-arch WinUI artifacts. Never combine arch payloads. No AnyCPU.

1. `Deltempo.App.WinUI.csproj`: set `<Platforms>x64;arm64</Platforms>` and REMOVE the hard-coded
   `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` — the RID is supplied per publish
   (`-r win-x64` / `-r win-arm64`). Keep `WindowsPackageType=None`,
   `WindowsAppSDKSelfContained=true`, `SelfContained=true`, `TargetPlatformMinVersion 10.0.17763.0`
   (parity with the WPF minimum), `ApplicationManifest=app.manifest` (PerMonitorV2 DPI).
2. `.github\workflows\release.yml`: add WinUI legs to the existing rid matrix (WPF + CLI already
   publish x64 and arm64):
   `dotnet publish src\Deltempo.App.WinUI\Deltempo.App.WinUI.csproj -c Release -r <rid> --self-contained true`
   → zip the output → attach `Deltempo.WinUI-<version>-x64.zip` and
   `Deltempo.WinUI-<version>-arm64.zip`, labelled **Preview** in the release-notes table
   (WPF `Deltempo.exe` remains the default download).
3. Add both zips to `checksums.sha256` generation. Do NOT change the docs/win install script or
   UpdateService asset names — they target `Deltempo.exe` and must keep working untouched.
4. Size discipline: the measured x64 payload is currently ~302 MB (self-contained Windows App SDK).
   Record the per-arch size in the PR summary. Any trimming attempt (MSIX, trimmed SDK payload)
   must not break the double-click unpackaged run; default outcome is per-arch zip, no fat build.

---

## Part C — Hard-won repo facts (violating any of these fails the build or CI)

- Build: `dotnet build deltempo.sln -c Release`. A RUNNING `Deltempo.WinUI.exe` locks its own bin
  output → MSB3027/MSB3021 on Deltempo.Core.dll. Close the app first; it is a file lock, not a
  code error.
- Format gate: `dotnet format deltempo.sln --verify-no-changes --no-restore` — solution level
  ONLY. Running it against a single project is weaker than CI and has shipped a CI failure before.
- Tests: `dotnet test` CANNOT drive xunit.v3 on the .NET 10 SDK. Build
  `tests\Deltempo.Tests\Deltempo.Tests.csproj -c Release`, then run
  `tests\Deltempo.Tests\bin\Release\net10.0-windows\Deltempo.Tests.exe --filter "Category!=Benchmark"`.
- **Doc-consistency guards**: DocumentationConsistencyTests pins the published test count across
  README.md + 8 translations, SECURITY.md, docs\llms.txt, docs\llms-full.txt,
  docs\ai-catalog.json, docs\index.html and the changelog badge. Adding tests REQUIRES sweeping
  the number everywhere — the guard tells you the new count; it does not fix it. Negative-test
  every new guard (inject a bad value → confirm the test fails → restore → confirm green).
- UI verification: the app is requireAdministrator, so UIPI blocks non-elevated automation and
  screenshots. Launch via elevated `Start-Process -Verb RunAs`, drive UI Automation by
  `AutomationProperties.Name` + InvokePattern, and capture with PrintWindow to
  `C:\Users\bhany\nav-shots` (NEVER %TEMP% — something sweeps it). Probe a pre-existing button as
  a control to distinguish "my feature is missing" from "UIPI is blocking you".
- Commits: Conventional Commits, and write commit messages with
  `[IO.File]::WriteAllText($path,$text,[System.Text.UTF8Encoding]::new($false))` —
  `Out-File -Encoding utf8` injects a BOM that ends up in the commit subject.
- The shell here blocks `Stop-Process`/`taskkill` by default; if a build hits the DLL lock, ask
  before allowlisting, then close the app and rebuild.

---

## Acceptance criteria (all must be demonstrably true)

1. After Part 0: sln builds 0 errors, format gate clean, full test suite green (minus Benchmark),
   exe names and namespaces unchanged, release.yml/script/test path references updated, and
   `git log --follow` on a moved file still traces to its original history.
2. Repo root contains ONLY: sln/props/global.json/.editorconfig, src\, tests\, docs\, packaging\,
   scripts\, .github\, and top-level documentation (README*, LICENSE, SECURITY.md, ...).
3. After Part A: the WinUI shell nav exposes every WPF tool INCLUDING Process Manager, plus
   Settings, About, language switcher, theme toggle, haptics, auto-clean and update status.
4. After Part B: release.yml emits exactly two new Preview assets
   (`Deltempo.WinUI-<version>-x64.zip`, `-arm64.zip`) next to the UNCHANGED WPF/CLI assets and
   checksums; per-arch payload sizes recorded.
5. WPF app still builds, runs, and is unaffected in behavior; `dotnet format` and the full suite
   green after every phase.
6. Every claim in the final summary is backed by a command that was actually run, with its output.
