# Second Self-Hosted CI Runner — Setup Steps

**Ticket:** `86caffc23` — implements spike `86cabkhjg`
**Research basis:** `team/erik-consult/concurrent-unity-build-isolation-research.md`
**Status:** DRAFT for Sponsor execution. Finalize ci.yml change AFTER registration is confirmed.

---

## 1. Runner registration — exact steps (Sponsor runs these)

### Prerequisites
- Close Unity Hub and any running Unity editor instances before starting.
- Confirm no in-flight CI run: check GitHub Actions → Far Horizon → Actions tab.
- Have your GitHub Personal Access Token (or use the browser token flow in step 1b).

### Step 1a — Download the runner package into a new directory

Open PowerShell **as your normal interactive user** (not Administrator, not a service account):

```powershell
mkdir C:\actions-runner-2
cd C:\actions-runner-2
# Download the same runner version as your existing runner.
# Check C:\actions-runner\ for the version number (look at actions-runner\bin\Runner.Listener.dll
# version, or run: C:\actions-runner\config.cmd --version).
# Replace <VERSION> with the exact version, e.g. 2.317.0
Invoke-WebRequest -Uri "https://github.com/actions/runner/releases/download/v<VERSION>/actions-runner-win-x64-<VERSION>.zip" -OutFile runner.zip
Expand-Archive runner.zip -DestinationPath .
Remove-Item runner.zip
```

### Step 1b — Generate a registration token

In your browser: GitHub → your Far Horizon repo → Settings → Actions → Runners → "New self-hosted runner".
Copy the **token** shown on that page (it expires in 1 hour). You do NOT need to follow GitHub's install steps there — just grab the token.

### Step 1c — Configure the 2nd runner

Still in `C:\actions-runner-2\`, run:

```powershell
.\config.cmd `
  --url https://github.com/<YOUR-ORG-OR-USER>/Far-Horizon `
  --token <PASTE-TOKEN-HERE> `
  --name far-horizon-local-2 `
  --labels self-hosted,windows,unity `
  --work C:\actions-runner-2\_work `
  --runnergroup Default `
  --replace
```

Key argument notes:
- `--name far-horizon-local-2` — must differ from your existing runner (probably `far-horizon-local`).
- `--labels self-hosted,windows,unity` — these three labels MUST match exactly; `ci.yml`'s `unity` and `playmode` jobs select `runs-on: [self-hosted, windows, unity]`.
- `--work C:\actions-runner-2\_work` — gives this runner its OWN checkout root. Each runner checks out to `C:\actions-runner-2\_work\Far-Horizon\Far-Horizon\`, so its `Library/PackageCache` is physically separate from runner 1's `C:\actions-runner\_work\...` path. This is the primary isolation mechanism.
- `--replace` — safe to include if you re-run config; harmless on first run.

Accept all prompts (default runner group, no service install — see §RUN INTERACTIVELY below).

### Step 1d — Set the UPM_CACHE_ROOT environment variable for runner 2

This is the belt-and-suspenders guard against the global UPM package cache race (separate from `Library/PackageCache`). The global UPM cache lives at `%LOCALAPPDATA%\Unity\cache` by default — both runners share it if not overridden.

Before starting runner 2 each time, in the same PowerShell window:

```powershell
$env:UPM_CACHE_ROOT = "C:\actions-runner-2\upm-cache"
```

Then start the runner (see §RUN INTERACTIVELY). Runner 1 uses the default `%LOCALAPPDATA%\Unity\cache` path; runner 2 gets its own root. You may also add `BEE_CACHE_DIRECTORY` for Bee build-cache isolation (see §Bee cache note below).

### RUN INTERACTIVELY (mandatory — do not install as a Windows service)

**Per the `runner-unity-license-needs-interactive-user` constraint:** the Unity Editor licenses via the logged-in user's Unity Hub login. Running the runner as a Windows service means it runs under a different session context and loses Hub entitlements — Unity exits with code 198 (no license). Always run interactively in your own session:

```powershell
# In C:\actions-runner-2\, with $env:UPM_CACHE_ROOT set (see §1d):
.\run.cmd
```

Keep this PowerShell window open while CI is running. You need TWO such windows open concurrently — one for runner 1 (`C:\actions-runner\run.cmd`) and one for runner 2 (`C:\actions-runner-2\run.cmd`).

**Startup sequence each day:**
1. Open PowerShell window A → start runner 1: `cd C:\actions-runner; .\run.cmd`
2. Open PowerShell window B → set env var → start runner 2:
   ```powershell
   cd C:\actions-runner-2
   $env:UPM_CACHE_ROOT = "C:\actions-runner-2\upm-cache"
   $env:BEE_CACHE_DIRECTORY = "C:\actions-runner-2\bee-cache"
   .\run.cmd
   ```

### Bee cache note

The Bee incremental build cache (IL2CPP artifacts) lives at `%USERPROFILE%\AppData\Local\Unity\Caches\bee` by default. Two concurrent IL2CPP builds can race on it. Setting `BEE_CACHE_DIRECTORY` per runner (as shown above) gives each its own Bee cache. First run will be cold (slower); subsequent runs reuse the warm per-runner cache. Source: `team/erik-consult/concurrent-unity-build-isolation-research.md` §E-4.

---

## 2. ci.yml change (draft diff — finalize AFTER runner is registered and online)

**Current state:** one runner, one `unity` job, one `playmode` job. The `concurrency` group `ci-${{ github.ref }}` cancels superseded runs on the same ref — this is correct and should stay.

**What changes:** two runners with identical labels means GitHub will dispatch the two `unity`-lane jobs (or two runs against different PRs) to whichever runner is free. No matrix, no label splitting needed — GitHub Actions self-hosted runner dispatch is first-available across runners sharing the same labels.

**The `needs: unity` serialization in `playmode`** currently prevents two Unity processes from running at once on the single runner. With two runners this serialization is still desirable within a single workflow run (so `playmode` doesn't start a second Unity process on the same runner as `unity` on that PR). But across two different PR runs, the runners are independent — no change needed.

**The only required ci.yml change** is to add `UPM_CACHE_ROOT` and `BEE_CACHE_DIRECTORY` to the `unity` and `playmode` job `env:` blocks so that concurrent runs on the SAME runner (same machine, back-to-back from the same runner instance) also get workspace-isolated caches:

```yaml
# In both the `unity` job and the `playmode` job, add to the existing env: block:
env:
  UNITY: 'C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe'
  RESULTS: ${{ github.workspace }}\ci-out
  UPM_CACHE_ROOT: ${{ github.workspace }}\ci-upm-cache    # ADD THIS
  BEE_CACHE_DIRECTORY: ${{ github.workspace }}\ci-bee-cache  # ADD THIS
```

`${{ github.workspace }}` expands to the runner's checkout root for that specific run — it is already distinct between runner 1 and runner 2 (different `--work` paths), so workspace-relative cache paths are naturally isolated across both runners AND across concurrent runs on the same runner.

**No `concurrency` group change needed.** The existing `ci-${{ github.ref }}` cancel-in-progress behavior is correct: it cancels a stale run on the same branch when a new push arrives — this is desirable regardless of runner count.

**No matrix change needed.** GitHub distributes queued jobs to available runners sharing the labels automatically.

**NOTE: Do NOT apply this ci.yml change until the 2nd runner is registered, online, and you have confirmed it picks up and completes one test run successfully.** Merging ci.yml before the runner is proven online risks a workflow that queues jobs that never start.

---

## 3. RAM-trial procedure — confirm headroom before locking it in

**Goal:** verify the machine has sufficient RAM for two concurrent Unity builds before the 2nd runner carries real PR traffic.

### When to run

After the 2nd runner is registered and has completed at least one solo test run successfully (confirming the license + labels + work dir are correct). Do NOT run the trial while any CI PR is in flight.

### Setup

Open two PowerShell windows, one per runner. Confirm both runners are STOPPED (`Ctrl+C` out of `run.cmd` in each).

Open Task Manager → Performance → Memory. Note current committed memory (baseline).

### Trigger two concurrent cold builds

**Window A (runner 1):**
```powershell
cd C:\actions-runner
.\run.cmd
```

**Window B (runner 2):**
```powershell
cd C:\actions-runner-2
$env:UPM_CACHE_ROOT = "C:\actions-runner-2\upm-cache"
$env:BEE_CACHE_DIRECTORY = "C:\actions-runner-2\bee-cache"
.\run.cmd
```

Now push a dummy commit (or re-run a recent workflow run from the GitHub Actions UI twice, triggering two runs against two different SHAs or two different PRs). Both runners will pick up a job simultaneously.

**Alternatively** (simpler, no CI involved): open two separate PowerShell windows and manually launch Unity in batchmode in each, pointing at two different worktrees:

```powershell
# Window A — use your existing main worktree
$env:UPM_CACHE_ROOT = "$env:TEMP\upm-trial-A"
$env:BEE_CACHE_DIRECTORY = "$env:TEMP\bee-trial-A"
& "C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "C:\Trunk\PRIVATE\Far-Horizon" `
  -executeMethod FarHorizon.EditorTools.BootstrapProject.Run `
  -logFile "$env:TEMP\trial-A.log"
```

```powershell
# Window B — use the erik worktree (or any second checkout)
$env:UPM_CACHE_ROOT = "$env:TEMP\upm-trial-B"
$env:BEE_CACHE_DIRECTORY = "$env:TEMP\bee-trial-B"
& "C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "C:\Trunk\PRIVATE\Far-Horizon-erik-wt" `
  -executeMethod FarHorizon.EditorTools.BootstrapProject.Run `
  -logFile "$env:TEMP\trial-B.log"
```

### What to watch

- **Task Manager → Memory:** peak committed during the overlap. Expected: ~2–4 GB per Unity cold bootstrap. If the machine has 16 GB RAM and normal Windows overhead is 4–6 GB, two Unity builds at 2–4 GB each should stay under 16 GB comfortably. If the machine has 8 GB, the trial may show it is marginal.
- **Both logs reach `[BootstrapProject] complete`** — check `$env:TEMP\trial-A.log` and `trial-B.log` after both processes exit.
- **No EPERM / rename errors** in either log — grep for `EPERM` and `rename`.
- **Distinct UPM cache roots populate independently** — after the runs, confirm `$env:TEMP\upm-trial-A\` and `upm-trial-B\` both contain package dirs.

### Pass / fail

- **PASS (all four):** both logs reach `[BootstrapProject] complete`; no EPERM; RAM peak stays below machine total minus 2 GB headroom; both cache roots populated.
  - Action: apply the ci.yml env-var diff (§2), merge to main, confirm two concurrent CI runs complete.
- **FAIL on RAM:** peak leaves less than 2 GB headroom. Do not run concurrent CI builds; the 2nd runner should only run when the 1st is idle (stagger, do not overlap).
- **FAIL on EPERM:** cache isolation is incomplete — capture the exact failing path from the log and file a finding on ticket `86cabkhjg`.

### Expected throughput after trial passes

~1.4–1.6× CI throughput (not 2×) — shared CPU cores, RAM, and disk I/O prevent linear scaling. The practical win is: two PRs in review can build concurrently instead of serializing, which cuts the end-to-end wait from ~2× build time to ~1× build time per PR. Source: `concurrent-unity-build-isolation-research.md` §E-8.

---

## Post-registration: update team state (orchestrator task, not Sponsor)

Once the runner is confirmed live and the RAM trial passes:
1. Update the `single-unity-build-slot-serializes-orchestration` memory entry: cap becomes 2 (not 1).
2. Update CLAUDE.md "≤1 Unity-build ticket in flight" → "≤2".
3. Merge the ci.yml env-var change via normal PR flow.
