# Spike — per-instance UPM cache isolation (unblock concurrent Unity builds on one machine)

**Status:** proposed (cheap "do-first" verification spike)
**Owner suggestion:** Devon (build/CI surface) or orchestrator R&D-lane
**Timebox:** 2 hours, hard stop
**Tracker:** relates to the away-queue "harden CI / concurrent builds" item + autonomy-tuning-plan item H; distinct from the orphan-run fix `86caammpq`.

---

## The one question this answers

> Is the bootstrap `EPERM rename` collision (the thing that forces ONE Unity build at a
> time across all worktrees + CI) **fully sourced from Unity's shared UPM caches** — and
> therefore defeatable on a single machine by giving each Unity instance its own cache —
> **or** does it come from some other shared lock (Asset Store cache, Hub state) that
> per-instance env vars can't isolate?

If UPM-sourced → single-machine 2× is unlocked with **zero new hardware/license**, just
env-var exports baked into `serve_soak.sh` + `ci.yml`. If not → we've cheaply proven the
2nd-machine fallback is actually necessary, instead of assuming it.

## Why this overturns the prior conclusion

The earlier 3-agent investigation (team/STATE.md) concluded "a 2nd runner on THIS machine
re-races the PackageCache (NO help); true 2× needs a 2nd MACHINE + Unity seat." Two verified
facts say that's too pessimistic:

1. **License does not bind same-machine concurrency.** Unity Support: concurrent batchmode
   builds on one machine are fine as long as the license is valid; the only lock is *"Multiple
   Unity instances cannot open the **same project**"* — which never triggers across distinct
   worktrees (different project folders).
2. **The shared caches are relocatable per-instance** via env vars read at process launch:
   `UPM_CACHE_PATH` (uncompressed packages — likely EPERM culprit), `UPM_NPM_CACHE_PATH`
   (registry data), `UPM_GIT_LFS_CACHE_PATH` (Git LFS). The docs note the env var is a *global*
   override per process — which is exactly what we want: export a **distinct** value in each
   worktree's build shell and the two instances stop sharing a rename target.

Sources: Unity Manual "Customize the global cache" + "Customize the asset package cache
location"; Unity Support "Running multiple instances of Unity".

---

## Step 0 — SAFETY / coordination (do not skip)

This spike launches Unity instances, which **contend for the very build slot the orchestrator
is holding**. Before starting:

- Confirm **no in-flight CI**: `gh run list --branch main --limit 5` (and any active PR branch).
- Confirm **no local Unity / FarHorizon.exe running** (Task Manager / `tasklist | grep -i unity`).
- **Tell the orchestrator session to hold all Unity-slot dispatch for the 2h window** — this is
  the one spike that deliberately runs TWO builds at once, so it must own the slot exclusively.

## Method

Use two worktrees that each hold a buildable checkout at the **same known-good commit** (e.g.
`Far-Horizon-devon-wt` + `Far-Horizon-drew-wt`, or the main worktree + one persona worktree —
pick whatever is clean). Point each instance's caches **outside its worktree** so the
dirty-worktree guard in `serve_soak.sh` is never tripped and the tree stays clean.

### Pass A — isolate the EPERM with concurrent bootstrap only (fast, ~10–15 min)

In **two separate shells**, set distinct cache roots, then launch `BootstrapProject.Run`
concurrently (bootstrap is where the EPERM was originally observed):

```bash
# --- shell 1 (worktree A) ---
export UPM_CACHE_PATH="$TEMP/upm-A/packages"
export UPM_NPM_CACHE_PATH="$TEMP/upm-A/npm"
export UPM_GIT_LFS_CACHE_PATH="$TEMP/upm-A/lfs"
UNITY="/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -nographics \
  -projectPath "/c/Trunk/PRIVATE/Far-Horizon-devon-wt" \
  -executeMethod FarHorizon.EditorTools.BootstrapProject.Run \
  -logFile "$TEMP/spike-bootstrap-A.log"

# --- shell 2 (worktree B), started within a few seconds of shell 1 ---
export UPM_CACHE_PATH="$TEMP/upm-B/packages"
export UPM_NPM_CACHE_PATH="$TEMP/upm-B/npm"
export UPM_GIT_LFS_CACHE_PATH="$TEMP/upm-B/lfs"
UNITY="/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -nographics \
  -projectPath "/c/Trunk/PRIVATE/Far-Horizon-drew-wt" \
  -executeMethod FarHorizon.EditorTools.BootstrapProject.Run \
  -logFile "$TEMP/spike-bootstrap-B.log"
```

Notes:
- Launch Unity.exe **directly** (not via Hub) so the process reads the exported env at launch —
  the docs' "restart the Hub" caveat applies to Hub-launched editors, not direct batchmode.
- Caches start **cold** per instance, so first bootstrap is slower than usual — that's expected,
  not a failure.

### Pass B — realistic concurrent full build (only if Pass A is clean)

Repeat with the **same env exports** but run the full `serve_soak.sh` in each worktree
concurrently (bootstrap → build → stamp-verify → capture). This proves the isolation holds
through the package-heavy import + build, not just bootstrap.

## Pass / fail

- **PASS** — both logs contain `[BootstrapProject] complete` (Pass A) / `result=Succeeded`
  (Pass B), **no `EPERM` / rename error in either log**, and `$TEMP/upm-A` + `$TEMP/upm-B`
  each populate independently. → The EPERM is UPM-sourced and isolatable. Proceed to
  productionization.
- **FAIL** — either log shows `EPERM` / a rename collision / a "cannot access … being used by
  another process" error. → A non-UPM shared lock is involved. Capture the exact failing path
  from the log, then check whether it points at the **Asset Store cache** (separate relocation
  var — see the linked manual page) or Hub state. If the lock is non-relocatable, the
  2nd-machine fallback is confirmed necessary.

## Evidence to capture (attach to the report)

- Both bootstrap logs (Pass A) and both build logs (Pass B).
- Proof the runs **overlapped** (start/end timestamps from each shell, or a `ps`/Task Manager
  shot showing two Unity.exe at once).
- `ls` of `$TEMP/upm-A` and `$TEMP/upm-B` showing independent population.
- The exact EPERM path string if it fails.

## Decision tree on outcome

- **Clean →** productionize: bake the per-launch env exports into `serve_soak.sh` and the
  `unity` job in `ci.yml` (and into each runner's env if a 2nd self-hosted runner is added
  later). File a productionization ticket. Single-machine 2× for local builds is then live.
- **EPERM persists →** file a finding ticket with the captured failing path; escalate to the
  2nd-machine + 2nd-seat fallback (the prior investigation's recommendation), now evidence-backed.

## Out of scope

- 2nd physical machine / VM procurement and licensing.
- Unity Accelerator (local import cache) setup — separate, compounding slot-shortening lever.
- The orphan-run-holds-runner concurrency-group fix (`86caammpq`) — independent CI bug.
- Branch-protection / required-check changes.

## Honest ceiling note

Even on a PASS, two Unity batch builds on one laptop share cores/RAM/disk → expect ~1.4–1.6×
throughput, not a clean 2×, plus per-instance cache disk cost (hundreds of MB–GB each). The win
is real for local worktree overlap; it is not free and not linear.
