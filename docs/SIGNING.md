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
| **SignPath Foundation (OV)** | **Free** | Open source — Deltempo's recommended path |
| Azure Artifact Signing | ~$9.99/mo | Non-Store distribution, no hardware token |
| Microsoft Store (MSIX) | Free | Guaranteed zero warnings |
| OV certificate | $150–300/yr | Outside Artifact Signing's supported regions |
| EV certificate | $400+/yr | Enterprise procurement only — not for SmartScreen |
| SignPath self-signed | Free | **Pipeline testing only** — see below |

Deltempo is a public MIT-licensed repository, so it is eligible to apply for the
**SignPath Foundation** OV program.

> ### ⚠️ Self-signed certificates do not help end users
>
> SignPath's Community tier offers self-signed certificates. Their own
> documentation states these are *"not signed by any certificate authority and
> therefore not trusted"* and are intended *"for testing your release
> process."*
>
> Because Windows does not trust the root, a self-signed build behaves the same
> as an unsigned one for a normal end user: they still get a blocking warning,
> and would additionally have to install the certificate as a trusted root
> manually — which no ordinary user will do.
>
> They are still worth creating, to validate the signing pipeline before
> committing to a real certificate. Swapping to an OV certificate later requires
> no workflow change.

## Option A — SignPath (recommended for Deltempo)

### A1. SignPath Foundation (OV) — what you want

1. Apply at <https://about.signpath.io/opensource>.
2. In the SignPath dashboard, set up:
   - an **Artifact Configuration** whose root element is a `<zip-file>`
   - a **Signing Policy** (note both slugs)
   - a **Trusted Build System** for `GitHub.com`
3. Install the **SignPath GitHub App** and grant it access to this repository.
   This is required for the audit-log checks the connector performs.
4. Add repository secrets:

   | Secret | Where to find it |
   | :--- | :--- |
   | `SIGNPATH_API_TOKEN` | Account → API Tokens |
   | `SIGNPATH_ORGANIZATION_ID` | Organization settings |
   | `SIGNPATH_PROJECT_SLUG` | Project slug |
   | `SIGNPATH_SIGNING_POLICY` | Signing Policy slug |
   | `SIGNPATH_ARTIFACT_CONFIG` | Artifact Configuration slug *(optional)* |

5. Push a `v*` tag. **`.github/workflows/release-signpath.yml`** handles the rest.

Use this workflow *instead of* `release.yml` — both trigger on `v*` tags, so only
one should have a `v*` tag trigger active at a time.

### A2. Self-signed — to validate the pipeline now

You can create one immediately under **Manage Certificates → Create Self-Signed
X.509**. Do this to prove the pipeline works end to end. It will **not** change
what end users see (see the warning above), and it needs no approval, so it is
worth doing while the Foundation application is pending.

The private key is generated inside SignPath's HSM and never leaves it, which is
why the workflow signs through the SignPath action rather than importing a PFX.

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
