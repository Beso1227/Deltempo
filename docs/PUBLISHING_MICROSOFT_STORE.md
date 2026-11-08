# Publishing the Deltempo WinUI Shell to the Microsoft Store

Status: **in progress (2026-11-08).** Gates 1–4 are done or done-with-a-manual-tail;
the pipeline produces a schema-validated `.msix` that installs and launches under
a test identity, and refuses a placeholder identity unless explicitly allowed.
What remains is the free Partner Center account (name reservation → real identity
values), pasting the listing copy and screenshots from section 7 into the
submission form, and one human pass over the packaged UI. The WPF
head stays the GitHub release channel as shipped today; this plan is only about
the WinUI 3 shell (`src/Deltempo.App.WinUI`, `WindowsPackageType=None` — now
proven to run inside a package, see section 6).

The gate the plan exists to satisfy: the shell must keep sharing
`Deltempo.Core` with the shipping app unchanged, must keep its own
single-instance mutex so it can run beside the WPF app during the transition,
and must pass Store review on the first honest submission — no policy
workarounds.

## 1. Identity and signing (decide first, everything else follows)

- **The account is free.** Both Individual and Company developer accounts have no
  registration fee (individual fee removed 2025-09-10, company fee removed
  2026-05-07); onboard at <https://storedeveloper.microsoft.com>, not through the
  legacy Partner Center entry points. Individual onboarding needs a personal
  Microsoft account plus government-ID + selfie verification; company onboarding
  adds business verification. No credit card, no subscription.
- **No certificate to buy.** MSIX/AppX packages submitted to the Store do not need
  a CA-trusted signature - the Store re-signs every package with a Microsoft
  certificate after certification. A paid certificate is only required for the
  MSI/EXE submission path or for distributing the MSIX outside the Store. This is
  why the pipeline produces an unsigned package on purpose.
- Create the Store listing in Partner Center first, then copy the **Package/Properties
  identity values** (Name, Publisher CN, PublisherId) - the `Publisher` in the package
  manifest must equal whichever identity Partner Center assigns. Do not guess.
  `tools/build-store-package.ps1` takes them as `-IdentityName` / `-Publisher` and
  rewrites the staged manifest; the committed template keeps its placeholders.
- Version scheme: `Major.Minor.Build.Rev` monotonically above the current WinUI shell
  version (`v{Assembly.Version} A·WinUI 3` in `MainWindow.xaml.cs:FooterVersion`), not
  above the WPF release numbers; independent tracks on purpose. The pack script reads
  `AssemblyVersion` from `Directory.Build.props`, so the package and the footer cannot
  disagree.
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
   `AppxManifest.xml`, generated logos in place, PDBs and stray `*.log` run
   artifacts pruned — `shell-startup.log` is written next to the exe, so a
   publish folder that has been launched once would otherwise ship its log) →
   `makeappx pack`
   resolved from the `Microsoft.Windows.SDK.BuildTools` NuGet the repo already
   depends on. makeappx schema-validates the manifest (it caught a parenthesised
   and an out-of-schema attribute during bring-up) and the script fails closed
   on any packaging error. No Visual Studio anywhere in the chain. Output:
   `packaging/Store/out/<Identity>_<Version>_<arch>.msix`, unsigned — the Store
   resigns after submission, matching `/p:AppxPackageSigningEnabled=false` in
   the documented Store workflow. One run produces one artifact: the script
   clears previous packages from `packaging/Store/out/` first, because a stale
   placeholder-identity build next to a real one is how the wrong file gets
   uploaded.

Manifest facts locked in by this bring-up (pinned in `StorePackagingAssetTests`):

- The full-trust entry point must be declared launchably: `Executable="Deltempo.WinUI.exe"`
  plus `EntryPoint="Windows.FullTrustApplication"` **on the `Application`
  element**, and the package must carry the `runFullTrust` restricted capability.
  That capability is scope, not elevation — the Store flavor still starts
  unelevated per `app.store.manifest`.
- **Identity is a build input, not repo state.** The committed
  `Package.appxmanifest` keeps `DeltempoWinUIPreview` / `CN=Placeholder-Publisher`
  so a stale real identity can never be mistaken for the current one; the script
  rewrites the **staged** copy from `-IdentityName` / `-Publisher` /
  `-DisplayName` / `-PublisherDisplayName`, and refuses a placeholder identity
  unless `-AllowPlaceholder` is passed (CI's dry run is the only caller that
  does). Version comes from `AssemblyVersion` in `Directory.Build.props`.
- **Architecture is declared, never implied.** `<Identity ProcessorArchitecture>`
  is required: omitting it declares the package "neutral", which would offer an
  x64-only self-contained payload to ARM64 devices. `-Architecture x64|arm64`
  sets it and the output filename together — the old hardcoded `_x64.msix` was
  cosmetic and is gone.

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
  maximise it for the capture. **Progress 2026-11-08:** four 1900×990 captures
  of the Store flavor exist and are regenerated by
  `tools/capture-store-screenshots.ps1` (see section 7); the PNGs themselves
  stay in gitignored `.internal/store-listing/` because they are build output,
  not source.
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
   **Identity and architecture parameterised 2026-11-08:** the job now passes
   `-Architecture x64 -AllowPlaceholder` explicitly, and a real build omits
   `-AllowPlaceholder` so a placeholder package cannot be produced by accident.
   **Install verification added 2026-11-08:** `tools/test-store-package.ps1`
   builds a test-identity package, signs it with a self-signed cert trusted in
   `TrustedPeople`, installs it with `Add-AppxPackage`, launches it, records
   where the data folder lands and whether a new crash entry appeared, then
   removes it. A self-signed certificate can carry any subject, so the same
   script can later verify the exact Partner Center identity before submission.
   **Verified 2026-11-08:** the test package builds and `signtool sign` succeeds
   (`DeltempoStoreTest_3.2.0.0_x64.msix`, identity `DeltempoStoreTest` /
   `CN=Deltempo Store Test`). The trust gate then stops with exit code 1 and
   prints the one command that needs elevation, because `Add-AppxPackage`
   accepts only `LocalMachine\TrustedPeople` — `CurrentUser\TrustedPeople` was
   measured and is **not** enough (`0x800B0109`, "root certificate of the
   signature must be trusted"). Developer Mode and sideloading are both off on
   this machine, so there is no unelevated install path at all.
   **Install run 2026-11-08 (after one elevated `Import-Certificate`):**
   `Add-AppxPackage` accepted the signed package unelevated, the package
   registered as `DeltempoStoreTest_3.2.0.0_x64__bcfvmjkqmxwsp` with
   `Architecture = X64`, the shell launched from
   `shell:AppsFolder\<PFN>!DeltempoWinUI` and stayed up for the 10 s
   observation window, `crash.log` was unchanged, and `Remove-AppxPackage`
   unregistered it cleanly. Two facts to carry forward:
   - **No data redirection.** The packaged shell wrote to
     `%LOCALAPPDATA%\Deltempo` exactly as the portable flavor does — not into
     `%LOCALAPPDATA%\Packages\<PFN>\LocalCache`. The privacy-policy paths and
     the "data survives uninstall" decision therefore stay valid for the Store
     channel unchanged.
   - **Uninstall leaves user data.** `Remove-AppxPackage` removed the package
     and its container but left `%LOCALAPPDATA%\Deltempo` (settings, caches,
     quarantine, crash log), matching the portable channel's documented
     behaviour rather than silently deleting the user's settings.
   Still open from the manual checklist: the tray icon/flyout on a real
   taskbar, the unelevated scan/clean pass, and the two elevation prompts —
   those need a human eye with the package installed (`-KeepInstalled`).
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

## 7. Store listing copy (paste-ready)

Everything Partner Center asks for, written here so the submission form is a
copy job rather than an authoring task. Only the product name and the identity
values depend on the account (section 1); the rest is settled.

**Product name.** Reserve `Deltempo` in Partner Center. If it is taken, the
fallback is `Deltempo Cleaner` - the package `DisplayName` in
`src/Deltempo.App.WinUI/packaging/Store/Package.appxmanifest` must then be
changed to match, because the Store shows the manifest name under the tile.
Reservation is the user's step: it needs the MSA login and ID verification.

**Category.** Utilities & tools -> PC management.

**Short description** (256-character field):

> Safety-first Windows maintenance: a precision storage cleaner, native memory
> optimizer, startup manager, app uninstaller and system repair console. Open
> source, zero telemetry, no ads and no upsells.

**Description** (10,000-character field; this is ~2,300):

> Deltempo is an open-source Windows maintenance suite built around one rule:
> never delete something the user did not agree to lose.
>
> **Precision storage cleaner.** Scans disposable data across Windows, browser,
> developer and gaming locations - temp folders, thumbnail and icon caches,
> update leftovers, NuGet/npm/pip/Cargo/Gradle caches, GPU shader caches and
> stale logs. Every candidate is classified into a risk tier and shown with its
> size before anything is removed.
>
> **Safety architecture, not a promise.** Cleaning runs SCAN -> PLAN -> PROTECT
> -> REVALIDATE -> CLEAN. Paths are bounded to authorised roots, revalidated
> immediately before removal, and recorded in a transaction journal you can
> read afterwards. Load-bearing caches that only look disposable - MSI source
> caches, per-user .NET SDK installs, driver vendor roots, Cargo registry
> sources - are refused by design.
>
> **Native memory optimizer.** Flushes standby page lists and trims inactive
> working sets through the Windows NT kernel calls (NtSetSystemInformation,
> EmptyWorkingSet) rather than a simulated "RAM boost".
>
> **Startup manager with verdicts.** Explains what happens if an entry is
> disabled - safe, caution or risky - from offline heuristics, so a boot entry
> is not a blind toggle.
>
> **App uninstaller and leftover sweeper.** Silent bulk uninstall, leftover
> AppData/ProgramData/registry trace cleanup, and a forced wipe for apps whose
> own uninstaller is broken.
>
> **System repair.** Runs SFC, DISM and CHKDSK from one console with the output
> streamed live, and asks for elevation only for the steps that need it.
>
> **Large file and process views.** Finds the files actually occupying a drive
> and shows which processes hold memory, with live icons and working-set sizes.
>
> **Built for long sessions.** Obsidian Dark and Porcelain Slate Light themes,
> right-to-left layout for Arabic, and a system tray guardian with live RAM
> telemetry and one-click boost.
>
> **Privacy.** Zero telemetry. No account, no ads, no analytics, no network
> call during scan, clean, memory or uninstall operations. Everything the app
> writes stays in %LOCALAPPDATA%\Deltempo and is listed in the privacy policy.
>
> Open source under the MIT licence; the full source is on GitHub.

**What's new in this version** (1,500-character field):

> First Microsoft Store release of the Deltempo desktop shell.
>
> - Runs as a standard user and asks for elevation only for the operations that
>   require it (system repair, restore-point removal), instead of starting
>   elevated.
> - Native WinUI 3 interface with the same Deltempo.Core engine as the portable
>   build, so scan, clean and memory behaviour is identical across channels.
> - Full right-to-left layout for Arabic and the shell's shipped language set
>   (English, Arabic, Spanish, French, German; the Cleaner workspace is fully
>   translated, remaining workspaces follow).
> - Store updates are delivered by the Store; the portable build keeps its own
>   signed update channel.

**Search terms** (seven, 30 characters each):

`disk cleaner`, `storage cleanup`, `memory optimizer`, `app uninstaller`,
`startup manager`, `sfc dism repair`, `open source utility`

**Screenshots.** Four, 1900x990, in `.internal/store-listing/`
(`01-cleaner.png`, `02-repair.png`, `03-settings.png`, `04-about.png`). They
are gitignored scratch and are regenerated - not hand-edited - by
`tools/capture-store-screenshots.ps1`, which launches the Store flavor
(`-p:DeltempoMSIX=true`) so the About shot does not carry the portable build's
"not yet code-signed" note. The Processes, Apps and Large Files views are
deliberately absent: they show this machine's installed software and window
titles, which is not the reviewer's business.

**Age rating (IARC questionnaire).** No user-generated content, no chat, no
advertising, no in-app purchases, no location or personal-data collection, no
gambling, no violence, no mature content. Expected outcome: 3+ / Everyone.

**System requirements.** Windows 10 version 2004 (build 19041) or later, x64
(`TargetDeviceFamily` in the manifest is `Windows.Desktop`
`MinVersion="10.0.19041.0"`). Self-contained: no separate .NET or Windows App
SDK runtime install.

**Privacy policy URL.** <https://beso1227.github.io/Deltempo/privacy.html> -
the page section 4 already requires; it discloses the crash log's paths and
the data folder that outlives uninstall (section 5).

**Pricing and availability.** Free, no in-app purchases, no trial. Publish to
all markets. The shell ships five languages (English, Arabic, Spanish, French,
German) and Arabic gets native right-to-left flow; the localization is
workspace-by-workspace, which is why the listing does not claim full coverage.

## 8. What NOT to do

- Do not merge the WPF and WinUI heads to "reuse the pipeline" — the parity tests
  and `docs/UI_CONSOLIDATION.md` retirement sequence are the planned convergence;
  the Store flavor ships from the WinUI head alone.
- Do not weaken the fail-closed update verifier to "fit" Store updates; Store
  packages update through the Store, and the GitHub flavor keeps its own path.
- Do not add a background task, widget or startup task now — none exist, none are
  needed, and each adds review surface.
