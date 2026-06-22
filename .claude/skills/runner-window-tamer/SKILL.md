---
name: runner-window-tamer
description: Stop the Far Horizon self-hosted runner's per-step console windows from popping up and stealing focus while you work. Use when the user says "runner-window-tamer", "/runner-window-tamer", "the runner CMD windows keep stealing focus", "stop the CI console popups", "tame the runner windows", "runner windows are disturbing", or wants the GitHub Actions runner's job-step command windows to stop grabbing foreground while orchestration / CI runs. Installs an admin-free background watcher (hidden, Startup-folder launcher) that minimizes any ConsoleWindowClass window descended from Runner.Worker.exe the moment it appears — without touching the run.cmd window, the user's own consoles, or the capture-gate game window.

---

# runner-window-tamer

Keeps the self-hosted runner's CI step consoles from stealing your focus.

## Why this exists

The runner must run **interactively** (so Unity's per-user license is reachable — see
`runner-autostart` + `unity-conventions.md`). The cost of interactive mode is that
every CI job step (`shell: cmd` / `shell: bash` in `.github/workflows/ci.yml`) spawns
a **console window that pops to the foreground and grabs focus**. With CI firing on
every PR push during orchestration, that's a stream of focus-stealing popups. Service
mode hid them (session 0) but couldn't license Unity, so it's not an option.

This watcher minimizes those console windows (no-activate) the instant they appear,
so focus returns to whatever you were doing.

## What it touches — and what it deliberately does NOT

It minimizes a window ONLY when BOTH are true:
1. window class is `ConsoleWindowClass` (a classic console window), AND
2. its owning process is a **descendant of a `Runner.Worker.exe`** (a CI job step).

That scoping deliberately EXCLUDES:
- **your `run.cmd` runner window** — it's an *ancestor* of Worker, not a descendant;
- **your own cmd/PowerShell windows** — not under Worker;
- **the capture-gate GAME window** (`FarHorizon.exe`) — it's a Unity window class, not
  `ConsoleWindowClass`, so it is never minimized. This matters: the capture gate
  (`ci.yml` step "Shipped-build capture gate") launches the exe windowed because
  `ScreenCapture` needs a real swapchain — minimizing it would produce black frames
  and FAIL CI. The tamer leaves it alone by construction.

When no job is running (no `Runner.Worker`), the watcher sleeps and does no process
enumeration — near-zero idle cost.

## Modes

- `on` (default) — install an admin-free **Startup-folder launcher** (a hidden `.vbs`
  that starts the watcher at logon, no UAC) AND start the watcher now. Idempotent.
- `off` — stop the running watcher + remove the Startup launcher.
- `status` — report whether the launcher is installed, whether the watcher is running,
  and whether a CI job is currently active.
- `loop` — internal (the watcher itself); not invoked directly.

## When invoked

1. Determine the mode (`on` / `off` / `status`); no argument → `on`.
2. **The user runs the script themselves** — the auto-mode classifier blocks Claude
   from running a `.ps1` via `-ExecutionPolicy Bypass`, so hand the user this command
   (a NEW PowerShell window; no admin needed):
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File "<skill-dir>/runner-window-tamer.ps1" -Mode <on|off|status>
   ```
   (`<skill-dir>` = `.claude/skills/runner-window-tamer/`.)
3. Tell the user it's admin-free (no UAC), survives reboots via the Startup launcher,
   and takes effect immediately for the currently-running runner.
4. Suggest verifying during the next real CI run; the watcher logs each minimized
   window to `%TEMP%\fh-window-tamer.log` for tuning.

## Tuning notes

- Poll cadence is 250 ms while a job runs (1500 ms idle). If popups still flash briefly
  before minimizing, lower the active-poll sleep.
- If a future workflow uses Windows Terminal instead of classic consoles, the class
  check (`ConsoleWindowClass`) won't match — extend it then.
- Composes with `runner-autostart` (that one brings the runner back on reboot; this one
  keeps its windows out of your way). They're independent toggles.
