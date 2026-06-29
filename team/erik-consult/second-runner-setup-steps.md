# Second Self-Hosted CI Runner — Setup Steps

**Ticket:** `86caffc23` — implements spike `86cabkhjg`
**Research basis:** `team/erik-consult/unity-concurrent-build-cache-isolation.md`
**Status:** ✅ DONE + VERIFIED 2026-06-29. The draft body below is Erik's reference; the corrections box records what was ACTUALLY run.

---

## ✅ ACTUAL SETUP (2026-06-29, verified) — corrections to the draft below

- **Runner-1 real path/name:** `C:\actions-runner-farhorizon\` registered as `far-horizon-local` (the draft's `C:\actions-runner\` path was WRONG). This runner build has **no `bin\Runner.Version` file** — read the version from `_diag\Runner_*.log` ("Version: …") or the releases API, not `Get-Content …\Runner.Version`.
- **Version:** `2.335.1` (matched runner-1; also the latest release at the time).
- **Runner-2:** downloaded to `C:\actions-runner-2\`, configured `--name far-horizon-local-2 --labels unity --work C:\actions-runner-2\_work --replace` (the auto labels `self-hosted, Windows, X64` give it the same set as runner-1). Runner-group prompt → Enter (Default). NOT a service.
- **`.env`** at `C:\actions-runner-2\.env`: `UPM_CACHE_ROOT=C:\upm-cache-2` + `BEE_CACHE_DIRECTORY=C:\bee-cache-2`.
- **ci.yml:** NO change needed (label-dispatch + per-ref `concurrency` already handle 2 runners), so the §2 optional ci.yml env-var diff was NOT applied.
- **Verification (instead of the §3 manual RAM trial):** re-ran two CI runs on DIFFERENT refs simultaneously → one landed on each runner, both `unity` jobs SUCCESS, **0 EPERM** in either log. RAM was a non-issue (63.5 GB machine; ~2–4 GB/build). Cache isolation via separate `--work` + `.env` roots confirmed.
- **Result:** build-slot cap bumped 1→2 (this PR's CLAUDE.md edit + the `single-unity-build-slot-serializes-orchestration` memory).

---

## 1. Runner registration — exact steps (Sponsor runs these)

### Prerequisites
- Close Unity Hub and any running Unity editor instances before starting.
- Confirm no in-flight CI run: check GitHub Actions → Far Horizon → Actions tab.
- Have your GitHub Personal Access Token (or use the browser token flow in step 1b).

### Step 1a — Download the runner package into a new directory

Open PowerShell **as your normal interactive user** (not Administrator, not a service account):

```powershell
# Find the exact version of your existing runner:
Get-Content C:\actions-runner\bin\Runner.Version
# Example output: 2.321.0 — use that exact string below.

mkdir C:\actions-runner-2
cd C:\actions-runner-2

# Replace 2.321.0 with whatever Get-Content printed above:
$ver = "2.321.0"
Invoke-WebRequest `
  -Uri "https://github.com/actions/runner/releases/download/v$ver/actions-runner-win-x64-$ver.zip" `
  -OutFile runner.zip
Expand-Archive runner.zip -DestinationPath .
Remove-Item runner.zip
```

### Step 1b — Generate a registration token

Run this in your terminal (the `gh` CLI must be authenticated — it already is if runner 1 was registered with it):

```powershell
gh api -X POST repos/TSandvaer/Far-Horizon/actions/runners/registration-token --jq .token
```

Copy the token output. It expires in **60 minutes** — complete Step 1c before then.

Alternatively: GitHub → Far Horizon repo → Settings → Actions → Runners → "New self-hosted runner" — the token is shown on that page. You do NOT need to follow GitHub's install steps there, just grab the token.

### Step 1c — Configure the 2nd runner

Still in `C:\actions-runner-2\`, run:

```powershell
.\config.cmd `
  --url https://github.com/TSandvaer/Far-Horizon `
  --token <PASTE-TOKEN-FROM-STEP-1b> `
  --name far-horizon-local-2 `
  --labels self-hosted,windows,unity `
  --work C:\actions-runner-2\_work `
  --runnergroup Default `
  --replace
```

Key argument notes:
- `--name far-horizon-local-2` — must differ from your existing runner. Verify the first runner's name in GitHub → Settings → Actions → Runners; adjust if it differs from `far-horizon-local`.
- `--labels self-hosted,windows,unity` — these three labels MUST match exactly; `ci.yml`'s `unity` and `playmode` jobs select `runs-on: [self-hosted, windows, unity]`.
- `--work C:\actions-runner-2\_work` — gives this runner its OWN checkout root. Each runner checks out to `C:\actions-runner-2\_work\Far-Horizon\Far-Horizon\`, so its `Library/PackageCache` is physically separate from runner 1's `C:\actions-runner\_work\...` path. This is the primary isolation mechanism.
- `--replace` — safe to include if you re-run config; harmless on first run.

Accept all prompts (default runner group, no service install — see §RUN INTERACTIVELY below).

### Step 1d — Persist UPM_CACHE_ROOT and BEE_CACHE_DIRECTORY for runner 2

This is the belt-and-suspenders guard against the global UPM package cache race (separate from `Library/PackageCache`). The global UPM cache lives at `%LOCALAPPDATA%\Unity\cache` by default — both runners share it if not overridden. The Bee IL2CPP build cache (`%APPDATA%\Local\Unity\Caches\bee`) is a second potential race.

**Permanent approach (recommended):** create a `.env` file that the runner agent reads automatically at startup. In `C:\actions-runner-2\`, create (or edit) `.env`:

```
UPM_CACHE_ROOT=C:\upm-cache-2
BEE_CACHE_DIRECTORY=C:\bee-cache-2
```

Runner 1 does NOT need a `.env` change — it keeps using the default cache paths, which become exclusive to it once runner 2 is redirected. If you prefer to also give runner 1 an explicit path for symmetry, create `C:\actions-runner\.env`:

```
UPM_CACHE_ROOT=C:\upm-cache-1
BEE_CACHE_DIRECTORY=C:\bee-cache-1
```

After saving the `.env` file(s), **restart the runner session(s)** (Ctrl+C in each window, then `.\run.cmd`) so the env vars take effect. Unity creates the cache dirs automatically on first use — no need to mkdir them.

**Alternative (per-session, no .env file):** set them manually before each `.\run.cmd`:

```powershell
$env:UPM_CACHE_ROOT = "C:\upm-cache-2"
$env:BEE_CACHE_DIRECTORY = "C:\bee-cache-2"
.\run.cmd
```

### RUN INTERACTIVELY (mandatory — do not install as a Windows service)

**Per the `runner-unity-license-needs-interactive-user` constraint:** the Unity Editor licenses via the logged-in user's Unity Hub login. Running the runner as a Windows service means it runs under a different session context and loses Hub entitlements — Unity exits with code 198 (no license). Always run interactively in your own session:

```powershell
# In C:\actions-runner-2\, with $env:UPM_CACHE_ROOT set (see §1d):
.\run.cmd
```

Keep this PowerShell window open while CI is running. You need TWO such windows open concurrently — one for runner 1 (`C:\actions-runner\run.cmd`) and one for runner 2 (`C:\actions-runner-2\run.cmd`).

**Startup sequence each day (with .env files in place):**
1. Open PowerShell window A → start runner 1:
   ```powershell
   cd C:\actions-runner
   .\run.cmd
   ```
2. Open PowerShell window B → start runner 2 (env vars load automatically from `.env`):
   ```powershell
   cd C:\actions-runner-2
   .\run.cmd
   ```

### Bee cache note

The Bee incremental build cache (IL2CPP artifacts) lives at `%LOCALAPPDATA%\Unity\Caches\bee` by default. Two concurrent IL2CPP builds can race on it. The `.env`-based `BEE_CACHE_DIRECTORY` split in §1d gives each runner its own Bee cache. First run after split will be cold (slower); subsequent runs warm up. Source: `team/erik-consult/unity-concurrent-build-cache-isolation.md` §Application to Embergrave / Far Horizon.

---

## 2. ci.yml changes — what is required vs optional

### Required: NONE

Two runners with identical labels (`self-hosted,windows,unity`) means GitHub dispatches any `runs-on: [self-hosted, windows, unity]` job to whichever runner is idle first. The existing ci.yml works correctly with two runners — no label changes, no matrix, no concurrency-group changes.

The `needs: unity` serialization on `playmode` still does the right thing: within a single PR's workflow run, `playmode` waits for `unity` on that run to complete before starting. Across two *different* PRs, their workflow runs are independent — each picks up a free runner and builds concurrently. This is exactly the throughput gain.

The existing `concurrency: group: ci-${{ github.ref }}` cancel-in-progress is per-ref (per-branch / per-PR) — two different PRs have different refs and do NOT cancel each other. No change needed.

### Optional (recommended): add workspace-relative cache env vars to the unity and playmode job env blocks

The `.env` isolation in §1d handles the global UPM and Bee caches at the runner level. For an additional layer (and so the cache path appears in the CI log for debugging), you can also pin them workspace-relatively in ci.yml:

```yaml
# In BOTH the `unity` job and the `playmode` job — add to the existing env: block:
env:
  UNITY: 'C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe'
  RESULTS: ${{ github.workspace }}\ci-out
  UPM_CACHE_ROOT: ${{ github.workspace }}\ci-upm-cache    # ADD — isolates global UPM per-workspace
  BEE_CACHE_DIRECTORY: ${{ github.workspace }}\ci-bee-cache  # ADD — isolates Bee per-workspace
```

`${{ github.workspace }}` expands to the per-runner checkout root (distinct between runner 1 and runner 2 due to separate `--work` paths), so these cache paths are naturally isolated across both runners and also across consecutive runs on the same runner.

**This ci.yml change is not required for the primary throughput win** (workspace isolation via `--work` handles that). It is belt-and-suspenders + debugging aid. Because ci.yml edits go through the PR workflow-permission wall (ticket `86cafhehe` — requires manual merge, auto-merge Action cannot update workflow files), treat this as a follow-up improvement after the runner is verified live.

**Do NOT apply the ci.yml change until the 2nd runner is registered, online, and has completed at least one test run successfully.** Merging it before the runner is live risks no visible behavior change, but it is cleaner to verify the runner first.

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
# Window B — use any SECOND checkout (e.g. a persona worktree that already exists)
# The key: it must be a DIFFERENT directory than Window A.
# Use C:\Trunk\PRIVATE\Far-Horizon-devon-wt, Far-Horizon-drew-wt, etc.
# If no second worktree is available, create one first:
#   git -C C:\Trunk\PRIVATE\Far-Horizon worktree add ..\Far-Horizon-trial-wt main
$env:UPM_CACHE_ROOT = "$env:TEMP\upm-trial-B"
$env:BEE_CACHE_DIRECTORY = "$env:TEMP\bee-trial-B"
& "C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "C:\Trunk\PRIVATE\Far-Horizon-trial-wt" `
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

~1.4–1.6× CI throughput (not 2×) — shared CPU cores, RAM, and disk I/O prevent linear scaling. The practical win is: two PRs in review can build concurrently instead of serializing, which cuts the end-to-end wait from ~2× build time to ~1× build time per PR. Source: `team/erik-consult/unity-concurrent-build-cache-isolation.md` §Cost and risk summary.

---

## Post-registration: update team state (orchestrator task, not Sponsor)

Once the runner is confirmed live and the RAM trial passes:
1. Update the `single-unity-build-slot-serializes-orchestration` memory entry: cap becomes 2 (not 1).
2. Update CLAUDE.md "≤1 Unity-build ticket in flight" → "≤2".
3. Merge the ci.yml env-var change via normal PR flow.
