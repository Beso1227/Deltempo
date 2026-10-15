# Third-Party Notices

Deltempo is MIT-licensed and its own source is fully visible. It does,
however, **launch and run one third-party utility**, and that deserves to be
stated plainly rather than buried.

---

## Chris Titus Tech Windows Utility (WinUtil)

| | |
|---|---|
| **Author** | [Chris Titus](https://christitus.com) |
| **Project** | [Chris Titus Tech Windows Utility](https://christitus.com/win) |
| **License** | As published by its author; **not MIT**, and not under Deltempo's control |
| **What Deltempo does** | Downloads the current script from `https://christitus.com/win` and runs it locally in an elevated PowerShell session |

### When you click it

Clicking **CTT WinUtil** in the System Repair workspace causes Deltempo to:

1. Fetch the script at `https://christitus.com/win` over HTTPS.
2. Execute it in a **new elevated PowerShell window** (Administrator), with
   `-ExecutionPolicy Bypass`, exactly as WinUtil's own documentation instructs.

Because the fetch is piped straight into the shell (`irm … | iex`), **whatever
that URL serves at that moment runs with administrator rights on your
machine.** Deltempo does not pin, verify, mirror, inspect or re-host that
script, and it cannot. If the vendor's endpoint were ever changed or
compromised, code would run elevated on every machine that clicked the button.

### What this means for Deltempo's audit claims

Deltempo's safety guarantees — the deterministic 5-tier safety engine,
pre-deletion revalidation, reparse-point and traversal defence, the
cryptographic release verification — apply to **Deltempo's own cleaning
code**. They do **not** extend to WinUtil, whose code Deltempo has never
seen. Deltempo's own documentation states it never runs untrusted code;
launching a remote script elevated is a deliberate, user-initiated exception
to that rule, and it is called out here rather than glossed over.

### Prefer to inspect first?

Open the script in your browser before running it:

- <https://christitus.com/win>

Deltempo also exposes a browser-only path in code
(`SystemRepairService.OpenChrisTitusWinUtilPage`) that opens the same vendor
page without executing anything.

### Attribution

WinUtil is the work of **Chris Titus**. Deltempo does not redistribute it,
endorse it, or claim any part of it. If you find WinUtil useful, support
the author directly.

---

## Deltempo itself

- **Copyright** © 2026 Deltempo Contributors
- **License** MIT — see [LICENSE](LICENSE)
- **Source** <https://github.com/Beso1227/Deltempo>

Deltempo's name, code, and trademark are the property of the Deltempo
contributors. "WinUtil" and "Chris Titus Tech" are the property of their
respective owner and are used here only to identify the third-party tool
being launched.