# Runner Disconnect Watchdog — install guide

A small operator-side watchdog that recovers the **alive-but-disconnected**
self-hosted CI runner failure mode that cost ~1h of orchestration idle.

## What it detects + does

On this **S0 Modern-Standby** laptop, deep-idle network suspend silently drops
the runner's GitHub long-poll connection. The symptom:

- the `Runner.Listener` process stays **ALIVE**, but
- `gh api repos/TSandvaer/Far-Horizon/actions/runners` reports the runner
  **`offline`**, and
- queued CI jobs sit **unpicked** (a connected idle runner grabs a queued job in
  seconds — so a long-`queued` job + an "idle/offline" runner means the
  *connection* died, not the process).

When the watchdog sees **OFFLINE on GitHub AND the listener process alive**, it:

1. logs the detection (state + PID + GitHub status),
2. kills the stale `Runner.Listener` (scoped to **this** runner's directory — it
   never touches a second runner's listener), and
3. relaunches `run.cmd` **in your interactive user session** (NOT as a service —
   running as a service loses the Unity Hub license and Unity exits 198).

It does **nothing** when the runner is online, when the listener is *dead* (that
is the `/runner-autostart` process-dead case, not this one), or when `gh` is not
authenticated (it cannot prove offline, so it stays hands-off).

### How it complements the existing tools

| Tool | Recovers |
|---|---|
| `keep-screens-alive` skill | display sleep |
| `/runner-autostart` | runner **process dead** (exited) |
| **this watchdog** | runner **connection dead** (process alive, GitHub says offline) |

## Prerequisites

- **GitHub CLI authenticated.** The watchdog shells out to `gh api`, so `gh`
  must be installed and logged in as your token (the one that registered the
  runner). Verify:
  ```powershell
  gh auth status
  ```
  If not logged in: `gh auth login`. (If `gh` is not authenticated the watchdog
  logs `AUTH-ERROR` and takes no action — it will never kill/relaunch on an auth
  failure.)
- Runner `far-horizon-local` registered + running interactively from
  `C:\actions-runner-farhorizon\run.cmd` (the normal daily setup).

## Step 1 — Sanity-check the script once, by hand

Open PowerShell **as your normal interactive user** (not Administrator) and run
a single pass:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Trunk\PRIVATE\Far-Horizon\tools\ops\runner-disconnect-watchdog.ps1 -Once
```

Expected when the runner is healthy:

```
[<ts>] [START] Runner disconnect watchdog | repo=TSandvaer/Far-Horizon runner=far-horizon-local ...
[<ts>] [OK] Runner 'far-horizon-local' is ONLINE. No action.
[<ts>] [DONE] One-shot pass complete.
```

Logs are written to `C:\actions-runner-farhorizon\_watchdog\runner-disconnect-watchdog.log`.

> Adjust the path if your worktree/clone of Far Horizon is elsewhere — point at
> wherever `tools\ops\runner-disconnect-watchdog.ps1` actually lives.

## Step 2 — Install as a Scheduled Task (logon-triggered, repeats every 5 min)

Run this **once** in your normal-user PowerShell. It registers a task that runs
the watchdog as **you** (interactive logon), at logon, repeating every 5 minutes
indefinitely. Edit the two paths in the first two lines if your clone differs.

```powershell
$Script   = 'C:\Trunk\PRIVATE\Far-Horizon\tools\ops\runner-disconnect-watchdog.ps1'
$TaskName = 'FarHorizon-RunnerDisconnectWatchdog'

$action  = New-ScheduledTaskAction -Execute 'powershell.exe' `
             -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$Script`" -Once"

# Trigger: at logon, then repeat every 5 minutes for ever.
$trigger = New-ScheduledTaskTrigger -AtLogOn
$trigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) `
             -RepetitionInterval (New-TimeSpan -Minutes 5) `
             -RepetitionDuration ([TimeSpan]::MaxValue)).Repetition

# Run as the INTERACTIVE logged-in user (preserves the Unity Hub license).
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

# Don't kill the task if a pass runs long; allow it on battery (laptop).
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries `
             -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
             -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
             -Principal $principal -Settings $settings -Force
```

> **Why interactive / not a service:** the runner licenses Unity through your
> logged-in Unity Hub session. A service-context relaunch loses that and Unity
> exits 198. The task runs as `$env:USERNAME` with `LogonType Interactive` for
> exactly this reason — so when the watchdog relaunches `run.cmd`, the new runner
> inherits your license context.

## Step 3 — Verify the task is registered + fire it once

```powershell
Get-ScheduledTask -TaskName 'FarHorizon-RunnerDisconnectWatchdog'
Start-ScheduledTask -TaskName 'FarHorizon-RunnerDisconnectWatchdog'   # run now
Get-Content C:\actions-runner-farhorizon\_watchdog\runner-disconnect-watchdog.log -Tail 10
```

You should see a fresh `START` + `OK` (or a recovery sequence if it caught a real
disconnect).

## Managing the task

```powershell
# Pause / resume
Disable-ScheduledTask -TaskName 'FarHorizon-RunnerDisconnectWatchdog'
Enable-ScheduledTask  -TaskName 'FarHorizon-RunnerDisconnectWatchdog'

# Remove entirely
Unregister-ScheduledTask -TaskName 'FarHorizon-RunnerDisconnectWatchdog' -Confirm:$false
```

## Notes / safety

- **Idempotent + safe to run repeatedly.** Each pass acts at most once; a
  10-minute relaunch **cooldown** stops a relaunch storm while GitHub's status
  catches up to a fresh re-register (~10-30s after relaunch).
- **Single-runner scoped.** The watchdog matches `far-horizon-local` by name and
  scopes the listener kill to `C:\actions-runner-farhorizon\bin\`. It will not
  touch `far-horizon-local-2` (`C:\actions-runner-2\`), which Far Horizon keeps
  intentionally OFFLINE (the 2nd runner breaks windowed captures).
- **No `.github/` changes, no Unity build, no CI artifact** — purely an
  operator-side reliability tool.
- **Optional alternate config** (different runner / repo): the script takes
  `-Repo`, `-RunnerName`, `-RunnerDir`, `-IntervalSeconds`, `-CooldownSeconds`,
  `-LogDir`. Run `Get-Help <script> -Detailed` for all parameters.
