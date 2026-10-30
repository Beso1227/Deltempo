# Publishing the Deltempo WinUI Shell to the Microsoft Store

Status: **plan, not yet started.** The WPF head stays the GitHub release channel as
shipped today; this plan is only about the WinUI 3 shell (`src/Deltempo.App.WinUI`,
currently unpackaged `WindowsPackageType=None`).

The gate the plan exists to satisfy: the shell must keep sharing
`Deltempo.Core` with the shipping app unchanged, must keep its own
single-instance mutex so it can run beside the WPF app during the transition,
and must pass Store review on the first honest submission — no policy
workarounds.

## 1. Identity and signing (decide first, everything else follows)

- Create the Store listing in Partner Center first, then copy the **Package/Properties
  identity values** (Name, Publisher CN, PublisherId) — the `Publisher` in the package
  manifest must equal whichever identity Partner Center assigns. Do not guess.
- Version scheme: `Major.Minor.Build.Rev` monotonically above the current WinUI shell
  version (`v{Assembly.Version} A·WinUI 3` in `MainWindow.xaml.cs:FooterVersion`), not
  above the WPF release numbers; independent tracks on purpose.
- Store submissions are unsigned by us: remove any personal signing step for the
  Store flavor and let the Store resign. Keep the portable/GitHub flavor's signing
  posture untouched (THREAT_MODEL §2.5 checksum gate must keep verifying GitHub
  artifacts, not Store ones).

## 2. The elevation problem — the plan's hard center

`src/Deltempo.App.WinUI/app.manifest` declares `requireAdministrator`, which is how
the shell runs today. **Store review rejects auto-elevation**; MSIX apps must start
as the standard user and elevate deliberately. This is work, not a flag:

1. Change `requestedExecutionLevel` to `asInvoker` (keep `SMI/2016` namespace rules
   and `longPathAware` verbatim — see the recorded manifest gotcha).
2. `ElevationService.IsAdministrator` already reports the real state and the title bar
   already says `Administrator` / `Standard user`; confirm every workspace that
   silently assumed elevation now surfaces it (System Repair and restore-point
   cleaning are the known elevated-only flows — `CleanRestorePointsAsync` requires
   it).
3. Elevated actions re-launch or elevate per task via the standard `runas` prompt;
   the app must keep working unelevated for everything else.

Do the elevation split **before** any packaging work so the Store build and the
portable build stay in step.

## 3. Packaging route

**Decision record (final, 2026-10-30).** Two routes were closed on evidence, and
the working pipeline is `tools/build-store-package.ps1`:

1. **Single-project SDK route, closed.** `dotnet publish -p:WindowsPackageType=MSIX
   -p:GenerateAppxPackageOnBuild=true -p:EnableMsixTooling=true` (manifest ItemGroup
   wired) ran three times green and silently emitted no package. The official docs
   (learn.microsoft.com, "Automate building and packaging your single-project MSIX
   app" / "CI for WinUI 3") run the packaging with **msbuild** — the AppxPackage
   targets ship with Visual Studio, which `dotnet publish` never loads and this
   dev machine does not have. The route would only work where VS exists (e.g. the
   CI runner), tying Store output to one environment: rejected.
2. **Windows Application Packaging project (`wapproj`), closed before creation.**
   It builds through the same VS-shipped AppxPackage targets, so it cannot build
   here either; creating an unbuildable project just to tick a checklist box is
   worse than the honest script.
3. **Winner — `tools/build-store-package.ps1`**: `dotnet publish` (Store flavor,
   `-p:DeltempoMSIX=true`) → staging (manifest lands as the package root's
   `AppxManifest.xml`, generated logos in place, PDBs pruned) → `makeappx pack`
   resolved from the `Microsoft.Windows.SDK.BuildTools` NuGet the repo already
   depends on. makeappx schema-validates the manifest (it caught a parenthesised
   and an out-of-schema attribute during bring-up) and the script fails closed
   on any packaging error. No Visual Studio anywhere in the chain. Output:
   `packaging/Store/out/<Identity>_<Version>_x64.msix`, unsigned — the Store
   resigns after submission, matching `/p:AppxPackageSigningEnabled=false` in
   the documented Store workflow.

Manifest facts locked in by this bring-up (pinned in `StorePackagingAssetTests`):

- The full-trust entry point must be declared launchably: `Executable="Deltempo.WinUI.exe"`
  plus `EntryPoint="Windows.FullTrustApplication"` **on the `Application`
  element**, and the package must carry the `runFullTrust` restricted capability.
  That capability is scope, not elevation — the Store flavor still starts
  unelevated per `app.store.manifest`.
- Placeholders remain until Section 2's Partner Center copy step: identity
  `DeltempoWinUIPreview` / `CN=Placeholder-Publisher`.

- Keep `WindowsAppSDKSelfContained=true` and `SelfContained=true` — the Store
  package must not need a separate runtime installer.
- The generated logo set in `src/Deltempo.App.WinUI/Assets/Store/` ships inside
  the package (`tools/generate-store-logos.ps1` is the only source of truth —
  regenerate on icon changes, never edit the PNGs by hand).
- `RuntimeIdentifier` stays per-architecture (`win-x64`, `win-arm64`); the Store
  flavor delivers one package per architecture, matching the existing publish
  pattern (see the `RuntimeIdentifier` rationale comment in the WinUI csproj
  header).
- CI gate (`winui-store-package` job in `.github/workflows/ci.yml`) builds the
  Store flavor, fail-closed-verifies the asInvoker manifest, the exe and the
  complete logo set, packs the `.msix` with the same script, verifies an output
  package actually exists (≥1 MB) and uploads both the publish folder and the
  `.msix` as Partner Center artifacts.
- The payload carries WPF assemblies (`PresentationFramework`, `WindowsBase`, …)
  because `Deltempo.Core` (shared with the WPF head) references WPF for
  `TrayService`. Pre-existing, not package-induced; noted so a future slimming
  does not read as a packaging regression.
- A/W: `WindowsAppSDK` 2.4.0 self-contained packages are accepted by the Store; pin
  the package version in `Directory.Packages.props` as today, no floating refs.

## 4. Assets the Store will actually check

- The app now carries a real icon (see the WinUI app-icon work, `InitializeForHwnd`
  / `TitleBarLogo` / `WM_SETICON`); the MSI/Store surface needs dedicated Store
  logos: 44×44, 50×50, 71×71, 150×150, 310×310, plus 256×256 installer-style PNG.
  Generate them from the largest frame of `src/Deltempo.App.Wpf/app.ico`; do not
  scale down screenshots.
- Screenshots at least 1366×768 — the shell's default window is 1360×840, so
  maximise it for the capture.
- A privacy policy URL is mandatory; "zero telemetry, all local" (README claim,
  enforced in the repo) is the pitch, but the crash log `%LOCALAPPDATA%\Deltempo\crash.log`
  still contains paths — say so in the policy in one sentence so review never bounces.
- Age rating: standard questionnaire, expect PEGI 3 / ESRB E with no extra content.

## 5. Behavior review — the deltas to fix before submission

- **Uninstall hygiene:** the journal, logs and crash log under
  `%LOCALAPPDATA%\Deltempo\` outlive MSIX removal. Add a light uninstall cleanup
  (the app cannot run post-restart, so write a `Deployment`
  `AddPackage/RemovePackage` hook or accept and document the leftover folder;
  picking the documented-approach is fine, but it must be written down here once
  shipped).
- **Single instance across two channels:** the mutexes are already namespaced
  (`Global\Deltempo_App_SingleInstance_Mutex_v2` WPF,
  `Global\Deltempo_WinUI_SingleInstance_Mutex_v1` WinUI — _renamed 2026-10-30_
  from the `..._Preview_...` shipping-bug name, with the collision choice
  documented in THREAT_MODEL.md section 2.9).
- **File associations / protocol:** none today; don't add speculative ones now.

## 6. Gate sequence (executed in order, one commit per step)

1. **Elevation split** — asInvoker manifest + unelevated-by-default path +
   per-action `runas`; parity tests for the title-bar privilege label and the
   restore-point flow under both levels. `_validation_`: a non-elevated run
   still scans and cleans standard paths, and asks for elevation only on the
   two known restricted surfaces. **Progress 2026-10-29:** the flavor split
   itself is done — `-p:DeltempoMSIX=true` selects `app.store.manifest`
   (asInvoker) while the preview/portable flavor keeps `app.manifest`
   (requireAdministrator, byte-equal posture to the WPF head); the per-task
   engines and bail-out texts are pinned by `StoreElevationParityTests`.
   Still open before submit: a real unelevated end-to-end pass over the
   standard-path scan/clean (manual) and the per-output confirmation on the
   two restricted surfaces.
2. **MSIX package job** — **done 2026-10-30, section 3's pipeline**: the
   `winui-store-package` CI job publishes the Store flavor, packs the
   schema-validated `.msix` with `tools/build-store-package.ps1` (makeappx
   from the SDK BuildTools NuGet; no Visual Studio), fail-closed-verifies the
   package blob and uploads it plus the publish folder as artifacts.
3. **Store logos + policy text** in the package manifest and this repo's docs.
4. **Rename the WinUI mutex** to its final name; document the collision choice in
   THREAT_MODEL (two processes with the same name would otherwise fight over it).
   **Done 2026-10-30:** renamed to `Global\Deltempo_WinUI_SingleInstance_Mutex_v1`;
   the deliberate distinct-from-WPF choice (both heads may run side by side) is
   THREAT_MODEL.md section 2.9, and `ShellPolishParityTests` asserts the "Preview"
   spelling is gone.
5. **Manual Partner Center submission**, Store flight first; record the flight
   decision in RELEASE_ENGINEERING.md once decided.
6. **Store-resign + review**: no action from us; keep the GitHub flavor's updates
   pipeline intact and independent.

## 7. What NOT to do

- Do not merge the WPF and WinUI heads to "reuse the pipeline" — the parity tests
  and `docs/UI_CONSOLIDATION.md` retirement sequence are the planned convergence;
  the Store flavor ships from the WinUI head alone.
- Do not weaken the fail-closed update verifier to "fit" Store updates; Store
  packages update through the Store, and the GitHub flavor keeps its own path.
- Do not add a background task, widget or startup task now — none exist, none are
  needed, and each adds review surface.
