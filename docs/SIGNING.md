# Code Signing & SmartScreen

How Deltempo release binaries are signed, and what that does and does not fix.

## The important expectation

**Signing alone does not remove the SmartScreen warning.** Users of a newly
signed release still see a prompt on first download.

Microsoft's own documentation is explicit: a valid certificate changes the
prompt from *"Unknown publisher"* to a **named, verifiable publisher**, and lets
trust **carry forward** to future versions. The warning itself clears only once
the file accumulates reputation — *"several weeks and hundreds of clean installs
from a wide audience."*

| Distribution | First-download behaviour |
| :--- | :--- |
| Microsoft Store (MSIX) | No warning, ever — Microsoft re-signs |
| Valid OV / EV certificate | Warning until reputation accumulates; publisher name shown |
| Self-signed | Same as unsigned |
| No signature | "Windows protected your PC" — user must click **Run anyway** |

> **EV certificates no longer bypass SmartScreen.** That behaviour was removed
> in 2024. Paying a premium for EV solely to avoid the warning is not justified.

Two practical consequences:

1. **An unsigned release must start reputation from zero on every single
   version.** Signing once means later releases inherit publisher trust.
2. **Only Store distribution guarantees zero warnings.**

## Choosing a certificate

| Option | Cost | Best for |
| :--- | :--- | :--- |
| **SignPath Foundation** | **Free** | Open-source projects — Deltempo's recommended path |
| Azure Artifact Signing | ~$9.99/mo | Non-Store distribution, no hardware token |
| Microsoft Store (MSIX) | Free | Guaranteed zero warnings |
| OV certificate | $150–300/yr | Outside Artifact Signing's supported regions |
| EV certificate | $400+/yr | Enterprise procurement only — not for SmartScreen |

Deltempo is a public MIT-licensed repository, so it qualifies for the
**SignPath Foundation** free signing program.

## Option A — SignPath Foundation (free, recommended for Deltempo)

1. Apply at <https://about.signpath.io/opensource>.
2. Once approved, create an **artifact configuration** for `*.exe`.
3. Add repository secrets:

   | Secret | Value |
   | :--- | :--- |
   | `SIGNPATH_TOKEN` | SignPath API token |
   | `SIGNPATH_ORG_ID` | Organisation id |
   | `SIGNPATH_PROJECT_ID` | Certificate configuration project id |

4. Push a `v*` tag. **`.github/workflows/release-signpath.yml`** handles the rest.

Use this workflow *instead of* `release.yml` — both trigger on `v*` tags.

## Option B — Traditional certificate (PFX)

Works with the default `release.yml` unchanged.

```powershell
# Encode and upload the certificate
.\scripts\prepare_signing_secret.ps1 -PfxPath .\deltempo.pfx -SetSecret
```

The script validates the PFX opens with the given password, warns if it is
expiring within 30 days, and sets:

| Secret | Value |
| :--- | :--- |
| `SIGNING_CERTIFICATE_PFX` | Base64-encoded `.pfx` bytes |
| `SIGNING_CERTIFICATE_PASSWORD` | PFX password |

## Release signing policy

`release.yml` **fails the build** if a certificate is expected but missing, so an
unsigned release cannot ship by accident. To deliberately ship unsigned, set the
repository **variable**:

```
REQUIRE_SIGNING = false
```

That downgrades the failure to a loud warning in the log. Default is `true`.

The signing step also:

- **Timestamps** the signature (`/tr timestamp.digicert.com`) so it remains
  valid after the certificate expires.
- **Verifies** each binary with `signtool verify /pa` before publishing, so a
  failed or missing signature cannot reach a release.
- Deletes the PFX from the runner in a `finally` block, so the private key is
  never left on disk if signing fails midway.

## Verifying a downloaded binary

```powershell
# Confirm the publisher identity
Get-AuthenticodeSignature .\Deltempo.exe | Format-List

# Status should be Valid, and Subject show the publisher
```

```powershell
# Confirm the file matches the published checksum
Get-FileHash .\Deltempo.exe -Algorithm SHA256
```

Compare against `checksums.sha256` attached to the release. The in-app
updater performs this same verification before applying an update.

## For end users on a first download

Until SmartScreen reputation accumulates, a signed Deltempo release may still
prompt. Direct users to verify the SHA-256 against the published
`checksums.sha256` — that is a stronger integrity guarantee than the SmartScreen
prompt it replaces.
