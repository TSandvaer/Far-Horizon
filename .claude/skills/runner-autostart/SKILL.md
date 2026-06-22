---
name: runner-autostart
description: Set up (or tear down) reboot-survival for the Far Horizon GitHub Actions self-hosted Unity runner. Use when the user says "runner-autostart", "/runner-autostart", "make the runner survive reboots", "auto-start the runner on logon", "runner autostart on/off/status", or otherwise wants the self-hosted runner at C:\actions-runner-farhorizon to relaunch run.cmd automatically after a reboot + login. Installs a logon-triggered Scheduled Task that launches run.cmd in the INTERACTIVE desktop session (so Unity's per-user license is present) and disables the leftover NETWORK SERVICE runner service so it cannot grab the registration and conflict. Attended-reboot only — NO auto-login, NO stored password.
---

# runner-autostart

Reboot-survival for the Far Horizon self-hosted Unity build runner, **without**
storing any credentials.

## Why this exists (the load-bearing fact)

A GitHub Actions runner installed as a **Windows service** runs in **session 0 as
NETWORK SERVICE**, which **cannot reach Unity's per-user license sign-in** — so
Unity builds fail. That's why the service approach broke ("something wrong with
the sign-in"). The correct setup for a Unity runner is to launch `run.cmd` in the
sponsor's **interactive desktop session**, where the Unity license is available.

This skill makes that interactive launch happen automatically at logon via a
**Scheduled Task** (not a service), and neutralizes the dormant service so the two
can never fight over the single runner registration in `C:\actions-runner-farhorizon`.

It is **attended-reboot only**: there is no auto-login and no stored password. You
log in after a reboot; the task then starts the runner. (Unattended/overnight
reboot would require auto-login with the EDC domain password stored on disk — a
security + GPO-policy problem that was deliberately rejected during design.)

## Modes

- `on` (also the **default** when invoked with no argument) — create the logon
  Scheduled Task + disable the runner service. Idempotent (`-Force` replaces an
  existing task). Triggers **one UAC elevation prompt** (the service change needs
  admin). **Never launches a competing `run.cmd`** — it only registers the task,
  which fires on *future* logons, so any runner already running is untouched.
- `off` — remove the logon Scheduled Task. Leaves the service **Disabled**
  (service mode is broken for Unity anyway; re-enabling it is a deliberate
  separate act, and the command to do so is printed).
- `status` — read-only report: task installed? service start-mode/state? is a
  `Runner.Listener` process currently running? No elevation, no changes.

## When invoked

1. Determine the mode from the user's argument: `on`, `off`, or `status`.
   **No argument → `on`.**
2. Run the bundled script from this skill's directory:
   ```
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<skill-dir>/runner-autostart.ps1" -Mode <on|off|status>
   ```
   (`<skill-dir>` is this file's directory: `.claude/skills/runner-autostart/`.)
3. For `on`/`off`: a **UAC prompt will appear** — tell the user to approve it.
   The script self-elevates, does the work in the elevated child, and writes the
   result back so the captured output reflects what actually changed. If the user
   declines UAC, nothing changes and the script says so.
4. Report the script's output concisely. For `on`, remind the user:
   - their currently-running `run.cmd` was **not** touched, and
   - the task takes effect on the **next logon** (reboot or sign-out/in) — they
     don't need to do anything else now.
5. Do **not** start, stop, or restart any runner `run.cmd` yourself as part of
   this skill — the skill manages the *task* and the *service* only.

## Facts (verified at design time, 2026-06-19)

- Runner dir: `C:\actions-runner-farhorizon`; launcher: `run.cmd` (stock — loops
  `run-helper.cmd`, restarts on exit code 1).
- Service: `actions.runner.TSandvaer-Far-Horizon.far-horizon-local` (the script
  discovers it by the `actions.runner.*far-horizon*` pattern rather than
  hardcoding, in case it changes).
- Machine is EDC domain-joined (`EDC\538252`); the task is registered for the
  **current** user automatically (`$env:USERDOMAIN\$env:USERNAME`), not hardcoded.

## Notes

- The Scheduled Task runs `run.cmd` at the user's **Limited** run level (not
  elevated) to match a normal manual launch.
- `ExecutionTimeLimit` is set to **unlimited** so Task Scheduler never kills the
  long-running runner; `MultipleInstances = IgnoreNew` is the single-instance guard.
- Reversal is always available: `off` removes the task; the service can be
  restored with `Set-Service -Name <svc> -StartupType Manual`.
