# Deltempo v3.0.0 — Force Delete

**The files Windows won't let you delete, now become deletable.**

---

## 🚨 BREAKING

`--yes` no longer deletes policy-protected targets. Add `--force-override` to opt in.

```powershell
# v2.x — deleted
deltempo force-delete now "C:\Program Files\Common Files" --yes

# v3.0.0 — shielded, exit 1
deltempo force-delete now "C:\Program Files\Common Files" --yes

# v3.0.0 — explicit
deltempo force-delete now "C:\Program Files\Common Files" --yes --force-override
```

Temp files and caches are unaffected.

---

## 🔒 Security Fix

`C:\Program Files`, `C:\ProgramData` and `C:\Program Files (x86)` **roots** were reported as safe to delete. Anything could queue the whole Program Files tree. Now permanently blocked.

Also stopped over-blocking — a personal folder named `windows\system32` is no longer undeletable.

---

## 🗑️ Force Delete

Six escalating strategies, so stubborn files actually go:

**Long paths** → **attributes** → **ownership** → **lock termination** → **native POSIX unlink** → **reboot purge**

Locked by a game, marked read-only, owned by TrustedInstaller — handled.

```powershell
deltempo force-delete <path>       # delete it
deltempo force-delete scan <path>  # why won't it budge?
```

| Flag | |
| :--- | :--- |
| `-t` | kill locking apps |
| `-r` | recycle bin |
| `-d` | dry run |
| `-F` | **override protection** |
| `--json` | machine-readable |

---

## 🖥️ New UI

**Force Delete** in the header. Pick files and folders, review stubbornness and lock owners, dry-run or execute — no UI freeze on big folders.

---

## 🔧 Fixed

- `--retry 5` no longer deletes a file named `5`
- Reboot-scheduled items show `Reboot Purge`, not `Failed`
- One locked subfolder no longer aborts its siblings
- Silent failures now logged to `%LOCALAPPDATA%\Deltempo\deltempo.log`

**692 tests passing.** Built clean from tag `v3.0.0`.

---

## 📦

`Deltempo.exe` · `deltempo_cli.exe` · `checksums.sha256`

Windows 10/11 x64 · no .NET needed

> Unsigned build — SmartScreen may warn. Verify SHA-256 or choose **Run anyway**.

[changelog](https://beso1227.github.io/Deltempo/changelog/) · [issues](https://github.com/Beso1227/Deltempo/issues)
