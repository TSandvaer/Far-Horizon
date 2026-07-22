# tools/ops — operator-side reliability tooling registry

One-line index of durable operator/infra reliability tools (machine setup, CI
runner reliability, soak/build operations). Reuse/extend before rebuilding.

Distinct from `tools/debug/` (Blender/build *instruments* for asset + feel
iteration). This dir holds *operations* tooling the Sponsor runs on his machine.

| Tool | Purpose |
|---|---|
| `runner-disconnect-watchdog.ps1` | Recovers the **alive-but-disconnected** self-hosted CI runner failure mode (S0 Modern-Standby deep-idle drops the GitHub long-poll → `Runner.Listener` process stays ALIVE but `gh api .../actions/runners` reports `offline`, so queued jobs sit unpicked). Polls `gh api repos/TSandvaer/Far-Horizon/actions/runners` ~every 5 min; on **offline + listener-alive** it logs the detect, kills the stale listener (scoped to the runner's own dir — never touches `far-horizon-local-2`), and relaunches `run.cmd` in the **interactive** user session (NOT a service — preserves the Unity license). No-ops when online, when the listener is *dead* (that's the `/runner-autostart` process-dead case), or when `gh` isn't authenticated. Idempotent; 10-min relaunch cooldown. `-Once` for a single pass (used by the Scheduled-Task install). Complements `keep-screens-alive` (display sleep) + `/runner-autostart` (process dead). Install via `runner-disconnect-watchdog-INSTALL.md` (Sponsor registers it as a logon-triggered Scheduled Task running as the interactive user). |
| `runner-disconnect-watchdog-INSTALL.md` | Copy-paste Scheduled-Task install guide for the watchdog above (logon-triggered, repeats every 5 min, runs as the interactive logged-in user to keep the Unity license). The Sponsor runs the install on his machine. |
