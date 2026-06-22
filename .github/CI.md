# Far Horizon CI

`.github/workflows/ci.yml` runs on every push/PR to `main`. Two jobs:

| Job | Runner | License | Billed? | What it gates |
|-----|--------|---------|---------|---------------|
| `structure` | GitHub-hosted `ubuntu-latest` | none | free (public-action minutes; tiny) | repo hygiene — no committed Unity artifacts, `.meta` presence, asmdef validity, headless entry-point methods present, version pin |
| `unity` | **self-hosted** `[self-hosted, windows, unity]` | **the machine's existing local Unity license** | **zero** (self-hosted) | BootstrapProject.Run → EditMode → PlayMode → BuildWindows, NUnit-XML result gate, console-error gate (URP warnings allowlisted), build-result gate |

## Why this route (not game-ci docker, not a Sponsor license secret)

The ticket (86ca86fqq) framed the license activation as "the one Sponsor-interactive gated step." It turned out **not to be needed** for a green CI, because:

1. **game-ci docker is blocked twice over on this repo.** The `unityci/editor` images are Linux-only and lag the Unity release stream; there is **no published image for the `6000.4.x` editor line** with the Windows IL2CPP module a `StandaloneWindows64` build needs. And even if one existed, it bills GitHub-hosted minutes on a **private** repo and still needs a Sponsor-supplied `UNITY_LICENSE` secret.
2. **This machine already has the exact editor + a working license.** `6000.4.10f1` is installed at `C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe` and already ran U1's EditMode 4/4 + PlayMode 2/2 + a successful Windows build. A self-hosted runner reuses that license and exact-version editor — no new secret, no version-drift risk, no billed minutes.
3. **The `structure` job protects `main` independently** of the Unity job, so the required-status-check on `main` works the moment this lands, even before the self-hosted runner is online.

If the project later wants hosted CI (e.g. to remove the single-machine dependency), the migration path is game-ci once a `6000.4.x` Windows IL2CPP image ships — at which point the Sponsor would create a `UNITY_LICENSE` secret (Personal-license ULF) and a `unity-hosted` job replaces the `self-hosted` `runs-on`. Documented here so the decision is reversible.

## Registering the self-hosted runner (one-time, on this machine)

The `unity` job is **inert until a runner with the `unity` + `windows` labels is registered and online.** Until then it shows as "waiting for a runner" / queued — it does NOT fail PRs (the `structure` job is the binding gate). Registration is an admin action on the repo (Settings → Actions → Runners → New self-hosted runner) — surfaced to the orchestrator/Sponsor as the one operational follow-up:

1. Repo → **Settings → Actions → Runners → New self-hosted runner** → Windows x64. GitHub shows a download + `config.cmd` command with a one-time registration token.
2. Run `config.cmd` in a working dir on this machine; when prompted for **labels**, add: `unity,windows` (the `self-hosted` label is automatic).
3. **Run it interactively with `run.cmd` — do NOT install as a service.** A Windows-service install (`config.cmd` offers this, and `svc.cmd install` does it) runs the runner as `NT AUTHORITY\NETWORK SERVICE` in **session 0**, which is non-interactive and **cannot reach Unity's per-user license** → Unity builds fail with a "sign-in required" / license error even though GitHub shows the runner online (diagnosed 2026-06-19). Start the runner with `run.cmd` in the **Sponsor's interactive desktop session** instead, where the machine's Unity license is active (the same context U1's local runs used).
4. **Reboot-survival without storing credentials:** use the `runner-autostart` project skill (`.claude/skills/runner-autostart/`). It installs a logon-triggered Scheduled Task ("run only when user is logged on") that re-launches `run.cmd` in the interactive session on each sign-in, and disables the dormant service so it can't grab the shared runner registration and conflict. This is **attended-reboot** survival (you log in; the task then starts the runner). True unattended/overnight auto-start is unsupported on this domain-joined machine — it would require storing the EDC domain password on disk and may be silently reset by GPO.

Once the runner is online, re-run any open PR's checks (or push a trivial commit) and the `unity` job picks up.

## Required-status-check coordination with U2 (branch protection)

U2 owns branch protection on `main`. The required check to add is **`structure (hosted, no license)`** — it is deterministic and license-free, so it can be required immediately. The **`unity (...)`** check should be added as required **only after** the self-hosted runner is confirmed online (otherwise PRs block forever on a queued job with no runner). Sequence: require `structure` now → require `unity` once the runner is registered.

## Artifacts

Every `unity` run uploads, **before any cleanup**, two artifacts (`.gitignore` eats `*.log` / `test-results*.xml` / `Build/`, so artifacts are the only way they survive):

- `unity-ci-logs-<sha>` — the four batchmode logs + both NUnit result XMLs.
- `FarHorizon-Windows-<sha>` — the built `Build/Windows/**` (the soak exe).

## Local pre-push checks

Run the same gates locally before pushing:

```bash
.github/workflows/scripts/structure_check.sh          # hygiene gate (the hosted job)
# after a local Unity test run produced ci-out/*.xml + *.log:
.github/workflows/scripts/check_unity_log.sh ci-out/*.log
python3 .github/workflows/scripts/parse_test_results.py ci-out/test-results-editmode.xml EditMode
python3 .github/workflows/scripts/parse_test_results.py ci-out/test-results-playmode.xml PlayMode
```
