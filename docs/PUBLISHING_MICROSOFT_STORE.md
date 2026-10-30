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
- **App PRI found missing from publish (fixed 2026-10-30).** `dotnet publish`
  leaves the app's own `Deltempo.WinUI.pri` in the build root while the
  framework PRIs land in publish; launching the publish-folder exe then dies
  at first paint with a stowed `XamlParseException` (crash log captured at
  `MainWindow.g.i.cs:LoadComponent`). The runtime pass x-raying the embedded
  manifest also caught a **stale apphost cache**: an incremental publish
  after a flavor switch can embed the wrong manifest, so never trust a
  non-clean publish that changes `ApplicationManifest`. Fixes, both
  fail-closed: `StageAppPriIntoPublish` in the WinUI csproj (copies the PRI
  into publish, errors when impossible) and a staging check in
  `tools/build-store-package.ps1` that aborts packaging without the PRI.
  Pinned by `StorePackagingAssetTests.Publish_StagesTheAppPriOrFailsClosed`;
  the shipped `.msix` has since been rebuilt with the PRI inside and the
  unelevated launch re-verified (see section 6, gate 1 progress).
- **Tray flyout z-order fixed (2026-10-30).** The WinUI tray context menu opened
  behind the taskbar: the flyout's popup host window carries no
  `WS_EX_TOPMOST`, so the always-topmost `Shell_TrayWnd` occluded whatever
  overlapped the notification strip (probe-verified before the fix). Three
  live facts the fix encodes, all verified during the session:
  a popup host reports `IsWindowVisible == false` while composition still
  renders its content; the host class is `WinUIDesktopWin32WindowClass` on
  some WindowsAppSDK builds and `Microsoft.UI.Content.PopupWindowSiteBridge`
  on others, so the finder identifies by exclusion (own process, own
  top-levels, not main, not IME) instead of class-matching; and calling
  `SetForegroundWindow` on the *hidden* main window keeps the flyout from
  ever displaying, so the foreground grant is gated on window visibility.
  The promotion (`SetWindowPos` `HWND_TOPMOST` with `SWP_NOACTIVATE`) logs
  through ShellDiagnostics and degrades to the pre-fix z-order on failure.
  Pinned by `WinUiTrayFlyout_IsPromotedAboveTheTaskbar`; WPF unaffected
  (framework popups are born topmost).
- A/W: `WindowsAppSDK` 2.4.0 self-contained packages are accepted by the Store; pin
  the package version in `Directory.Packages.props` as today, no floating refs.

## 4. Assets the Store will actually check

- The app now carries a real icon (see the WinUI app-icon work, `InitializeForHwnd`
  / `TitleBarLogo` / `WM_SETICON`); the MSI/Store surface needs dedicated Store
  logos: 44×44, 50×50, 71×71, 150×150, 310×310, plus 256×256 installer-style PNG.
  Generate them from the largest frame of `src/Deltempo.App.Wpf/app.ico`; do not
  scale down screenshots. **Progress 2026-10-30:** the required set is generated
  and committed (`16/44/50/71/150/256`, from `tools/generate-store-logos.ps1`,
  pinned by `StorePackagingAssetTests`); 310 is not required for a Win32
  full-trust submission.
- Screenshots at least 1366×768 — the shell's default window is 1360×840, so
  maximise it for the capture.
- A privacy policy URL is mandatory; "zero telemetry, all local" (README claim,
  enforced in the repo) is the pitch, but the crash log `%LOCALAPPDATA%\Deltempo\crash.log`
  still contains paths — say so in the policy in one sentence so review never bounces.
  **Progress 2026-10-30:** `docs/privacy.html` is that policy — it carries exactly
  that disclosure and lists every file the app writes — served on the site at
  <https://beso1227.github.io/Deltempo/privacy.html> and linked from the README's
  privacy section. Use that URL verbatim in Partner Center.
- Age rating: standard questionnaire, expect PEGI 3 / ESRB E with no extra content.

## 5. Behavior review — the deltas to fix before submission

- **Uninstall hygiene:** the journal, logs and crash log under
  `%LOCALAPPDATA%\Deltempo\` outlive MSIX removal. Add a light uninstall cleanup
  (the app cannot run post-restart, so write a `Deployment`
  `AddPackage/RemovePackage` hook or accept and document the leftover folder;
  picking the documented-approach is fine, but it must be written down here once
  shipped). **Decision 2026-10-30 — accepted and documented (this paragraph is
  the write-down):** the Deltempo data folder outlives uninstallation on
  *both* channels — the WPF head has always behaved this way on GitHub
  installs, so the Store flavor matching it keeps the two channels
  consistent. No `Deployment` hook is registered: the cleanup the hook could
  perform is user-hostile by default (it would destroy the journals that let
  a user review what a cleanup run deleted), §7 forbids the background-task
  surface it would need, and the folder is small, plain files under the
  user's own profile. The privacy policy (docs/privacy.html, the Partner
  Center URL) discloses the folder's paths; a user who wants it gone deletes
  `%LOCALAPPDATA%\Deltempo\` after removal, which the policy page says when
  the leftover note is added there.
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
   two restricted surfaces. **Progress 2026-10-30 (agent-driven):** the
   unelevated launch itself is verified on the machine — the publish-folder
   exe starts without elevation (binary manifest check: asInvoker, no
   `requireAdministrator` string), and a second launch defers to the running
   copy with exit code 0 (`SingleInstanceManager.TryAcquire` +
   `NotifyExistingInstance` path exercised live). The scan/clean flows and
   the title-bar privilege label remain for a human eye (step 1 of the
   manual checklist); this pass also caught and fixed the publish-PRI bug
   (section 3).
2. **MSIX package job** — **done 2026-10-30, section 3's pipeline**: the
   `winui-store-package` CI job publishes the Store flavor, packs the
   schema-validated `.msix` with `tools/build-store-package.ps1` (makeappx
   from the SDK BuildTools NuGet; no Visual Studio), fail-closed-verifies the
   package blob and uploads it plus the publish folder as artifacts.
3. **Store logos + policy text** in the package manifest and this repo's docs.
   **Done 2026-10-30:** the logo set (16/44/50/71/150/256) is generated from
   `app.ico` per section 3 and pinned by tests; the privacy policy page
   (`docs/privacy.html`, served at
   <https://beso1227.github.io/Deltempo/privacy.html>) carries the crash-log
   disclosure section 4 requires, linked from the README.
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
