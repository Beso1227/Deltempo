# Test Fixtures

## `test-update-signing-key.pem` / `test-update-verification-key.pub`

An ECDSA **P-256** key pair used **only** by `UpdatePipelineSecurityTests` to exercise the
manifest-signature verifier with a real sign/verify round trip, plus the negative cases
(wrong key, `kid` mismatch, tampered payload, malformed signature).

**This key is public knowledge. It is checked into the repository on purpose. It is not a
secret and must never be treated as one.**

- It signs nothing outside the test suite.
- No release, manifest, or build step reads it.
- `Deltempo` does not ship it: the fixture directory is not copied to any build output and is
  not referenced by `src/Deltempo.App.Wpf/WinTempCleaner.csproj` or `Deltempo.Cli`.
- It is not a trusted update key. `ManifestSignatureVerifier` holds **no** embedded key at all;
  an operator must provision a real one explicitly (see `docs/SIGNING.md` and `SECURITY.md`).
  With no provisioned key, verification fails closed.

If this key were ever able to sign something that a shipping build trusted, that would be a
critical vulnerability. Nothing in this repository is wired that way.