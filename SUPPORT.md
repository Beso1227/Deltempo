# Support

Deltempo is maintained by one person. That shapes where your question should go.

## Before you open anything

Most questions are already answered, and checking here is faster than waiting for a reply:

| If your question is about... | Start with |
| :--- | :--- |
| Is it safe? Will it delete something I need? | [The refusal table in the README](README.md#21-what-deltempo-refuses-to-delete) — the specific paths Deltempo refuses to delete, and why |
| How do I use a command? | `deltempo help` — prints the full command reference |
| How do I build or test it? | [CONTRIBUTING.md](CONTRIBUTING.md) — mirrors the CI gate exactly |
| Is it affected by <X>? | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) — threat analysis and known gaps |
| Is version N still maintained? | [SECURITY.md](SECURITY.md) § Supported Versions — only the current minor line receives fixes |
| What does the verification chain actually cover? | [SECURITY.md](SECURITY.md) § Security Controls, including what is *not* enforced yet |

## Where to ask

- **Something is broken** → [bug report](https://github.com/Beso1227/Deltempo/issues/new?template=bug_report.yml).
  Please include `deltempo status --json` output and the exact command you ran.
- **Something is missing** → [feature request](https://github.com/Beso1227/Deltempo/issues/new?template=feature_request.yml).
  If it deletes anything, the blast-radius question in the template is not optional.
- **Usage question** → there is no Discussions board enabled, so this has to be an
  issue. That is a poor fit and is a known gap; use the `question` label so it can
  be sorted out of the bug backlog.
- **Security vulnerability** → **not** a public issue. Use
  [private vulnerability reporting](https://github.com/Beso1227/Deltempo/security/advisories/new).
- **Conduct or Code of Conduct concern** → see [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) § Enforcement.

## What to expect

Realistically: this is a solo-maintained project, so triage may take a few days and
some reports will not get a reply at all. Issues that are not reproduced tend to go
unanswered rather than be closed, which is a failure mode worth knowing about.

If you can reproduce something in the **safety engine** — a file that was deleted
when it should have been kept, or kept when it should have been deleted — that is
the highest-value report this project receives. Please open it even if you cannot
build it, and attach the path.
