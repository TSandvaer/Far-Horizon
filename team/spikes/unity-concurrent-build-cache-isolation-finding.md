# Finding — concurrent Unity builds on ONE machine: the PackageCache EPERM did NOT reproduce, and the real hazard is the opposite of the documented remedy

**Status:** ✅ measured 2026-07-31 (spike `86cabkhjg`, editor `6000.4.11f1`).
**Spec this answers:** `team/spikes/unity-concurrent-build-cache-isolation-spike.md`.
**Prior research this tests:** `team/erik-consult/concurrent-unity-build-isolation-research.md` (routes 1–4)
and `team/erik-consult/unity-concurrent-build-cache-isolation.md`.
**Harness (reproducible, committed):** `tools/debug/unity_concurrency_trial.sh`.
**Raw logs:** `%LOCALAPPDATA%\Temp\fh-conc\logs\` (gitignored scratch — per-leg names quoted below).

---

## Headline

1. **The EPERM did not reproduce — in any of 8 legs, under any cache condition.** Two Unity
   `-batchmode` instances running concurrently from two worktrees completed cleanly — bootstrap
   *and* full `BuildWindows` — whether they SHARED one cold cache root, shared the REAL warm user
   cache *and* the real Bee cache, or had fully isolated roots. `EPERM: operation not permitted,
   rename` count = **0** in every log. **The spike's success criterion (two concurrent builds, no
   EPERM) is met with NO fix applied** — which means the isolation bake it was meant to justify has
   nothing to justify it.
2. **Per-project `Library/PackageCache` is not a shared surface across worktrees at all** —
   so the documented rename collision is *structurally* not a cross-worktree contention.
   Unity prints the absolute per-checkout path for every registered package.
3. **The real hazard found is the project's own documented remedy.** Deleting
   `Library/PackageCache` while the rest of `Library/` is warm **WEDGES** the checkout, and
   a bare re-run does **not** clear it. `touch Packages/manifest.json` clears it in one run.
4. **Cache isolation is not what caps Unity concurrency here.** One registered runner plus a
   repo-wide `concurrency: group: unity-build` in `ci.yml` cap CI at one build regardless of
   caches. **Recommendation: do NOT raise the build-slot cap.**

---

## Which of Erik's routes was taken

| Erik's route | Taken? | Why |
|---|---|---|
| Upgrade to `6000.4.11f1` before spiking (his prerequisite #1) | **Already satisfied** | `ci.yml:238` + `:1277` pin `6000.4.11f1`; every leg here ran that editor. |
| Add `BEE_CACHE_DIRECTORY` isolation (his "gap in the spike spec", #2) | **Adopted** | The harness exports it alongside the three `UPM_*` vars in every isolated/shared leg. |
| Per-instance `UPM_*` env isolation (his primary mechanism, #3) | **Implemented + tested** | This is the `isolated` leg. Mechanism *works* (see §Mechanics) — it just isn't needed for the failure it was aimed at. |
| Second runner with its own workspace (his `unity-concurrent-build-cache-isolation.md` recommendation) | **Out of scope + already refuted** | A/B-CONFIRMED that runner-2 online breaks windowed captures (`team/spikes/second-runner-breaks-windowed-captures-finding.md`), and standing up runner-2 is Sponsor-gated (`86caffc23`). |

**Deviation from the spec, deliberate:** the spec's Pass A/Pass B design only ever runs the
*isolated* configuration, so a clean result would have been unfalsifiable — it could not tell
"isolation fixed it" from "there was nothing to fix". Per `86caz5nr2` (a fix shown only working
proves nothing) two extra legs were added: **`default`** (no env vars at all — the exact condition
two personas hit today) and **`solo`** (one instance — the control that separates
"concurrency did it" from "the state did it"). The `solo`-class control is what caught the wedge
below; the spec's design would have mis-attributed it to concurrency.

---

## Legs and results

All legs: editor `6000.4.11f1`, worktrees `Far-Horizon-drew-conc-a-wt` (branch) and
`Far-Horizon-drew-conc-b-wt` (detached), both at `90d024b`, `git status` clean. Runner verified
`status=online busy=false`, `total_count=1`, no in-flight runs, before and during.

| # | Leg | Cache condition | Result |
|---|---|---|---|
| 1 | 2 concurrent bootstraps, both worktrees fully COLD (no `Library/` at all) | ONE shared cold throwaway root | **BOTH `[BootstrapProject] complete`.** `EPERM=0` in both. Resolve 41.23 s (A) / 34.80 s (B), 33 packages each. |
| 2 | 2 concurrent bootstraps × 3 trials, warm `Library/` + deleted `PackageCache` | real shared user cache (no env vars) | **6/6 FAILED** — `error CS0234` × 15, ~5 s each, `EPERM=0`. See §The wedge. |
| 3 | 2 concurrent bootstraps, NO reset at all (bare re-run of leg 2's state) | real shared user cache | **2/2 FAILED identically.** `EPERM=0`. Proves the state does not self-heal. |
| 4 | 1 bootstrap (control) after `touch Packages/manifest.json` | real shared user cache | **`[BootstrapProject] complete`**, `csErr=0`, `PackageCache` back to 33 entries. |
| 5 | 2 concurrent FORCED re-resolves (`touch` both manifests) | real shared user cache — the faithful production condition | **`EPERM=0` in both.** Resolve 1.22 s (A) / 26.78 s (B), both `[BootstrapProject] complete`. |
| 6 | **2 concurrent FULL `BuildWindows` builds** | real shared user cache + **real shared Bee cache**, no env vars at all | **BOTH `[FarHorizonBuilder] result=Succeeded size=115335954 bytes`.** A `00:59:00.293→01:01:17.459`, B `00:59:00.293→01:01:17.528` (≈2 m 17 s each, identical exe size). |
| 7 | 2 concurrent `BuildWindows` builds, incremental | fully ISOLATED per-instance `UPM_*` + `BEE_CACHE_DIRECTORY` | Both `result=Succeeded size=115335954 bytes`. A 20.3 s, B 20.5 s. |
| 8 | 1 `BuildWindows` build, incremental (control) | isolated throwaway root | `result=Succeeded size=115335954 bytes`, 16.8 s. |

**Leg 6 is the ticket's actual success criterion** — two Unity *builds* running concurrently from
different worktrees, no PackageCache EPERM — and it passed with **no fix applied at all**: no cache
env vars, both instances sharing the real UPM store *and* the real Bee cache. Overlap measured:
`Unity.exe 105544` + `Unity.exe 169688` both live at `00:59:25.622`, ~1.2 GB RSS each.

**Overlap is measured in every concurrent leg, not asserted.** Leg 1's probe at `00:43:21.118`
printed `Unity.exe 73152` and `Unity.exe 183616`, launched 67 ms apart (`00:42:55.909` /
`00:42:55.976`).

### Measured throughput — and why the number is weak

Legs 7 + 8 are the only apples-to-apples pair (both *incremental*, same warm state): two builds
finish in 20.5 s concurrently vs 2 × 16.8 s = 33.6 s serially → **≈1.64×**. That lands at the top of
Erik's estimated 1.4–1.6× band. **Do not quote it as settled:** N=1 per leg, incremental-only, and
the absolute times (17–20 s) are small enough that fixed process-startup cost dominates and noise is
proportionally large. A full-build pair (leg 6's ≈2 m 17 s class) was not run solo, so the
throughput multiple for a *cold* build is unmeasured. Anyone acting on a throughput number owes a
proper N≥8 measurement first.

---

## Mechanics established (these are the reusable facts)

- **`UPM_CACHE_ROOT` / `UPM_NPM_CACHE_PATH` are honoured on a direct `Unity.exe` launch.**
  A redirected root populated with the real cacache layout —
  `fh-conc/c-1/upm/npm/{content-v2,index-v5}`, 9.2 MB — while the real user cache
  (`%LOCALAPPDATA%\Unity\cache\upm\db`) kept its **2026-06-07** mtime, untouched. So the
  isolation lever is real; `env VAR=… Unity.exe` is sufficient and no Hub restart is involved.
- **`UPM_CACHE_PATH` (the "uncompressed packages" folder) has no default tree on this machine.**
  The real cache contains only `upm/db/{content-v2,index-v5,tmp}` — the npm/registry store. There
  is no `packages/` sibling to isolate, so that var is inert here.
- **Two concurrent batchmode editors both license fine.** Both logs carry
  `[Licensing::Client] Successfully resolved entitlement details`; the only documented lock
  (same *project* folder twice) is never approached from distinct worktrees. Erik's E-7 licensing
  *grey area* is unchanged by this — it is a legal question, not a technical one.
- **`Library/PackageCache` is per-checkout, and the log proves it.** Every registered package
  prints its absolute location, e.g.
  `com.unity.ai.navigation@2.0.5 (location: C:\Trunk\PRIVATE\Far-Horizon-drew-conc-a-wt\Library\PackageCache\com.unity.ai.navigation@9f76b145f0a8)`.
  Two worktrees therefore have **no shared rename target**, which is the mechanical reason the
  documented `.tmp-* → com.unity.X@hash` collision cannot be produced by cross-worktree
  concurrency.
- **33 packages resolve, of which 13 come from the registry and 20 are editor built-ins.** Only
  the registry set exercises the shared npm store, which bounds how much contention any amount of
  concurrency can create.

---

## The wedge — deleting `Library/PackageCache` alone poisons the checkout

**Observed signature** (`bootstrap-default-{1,2,3}-{A,B}.log`, `rerun-default-{A,B}.log`):

```
[Package Manager] Restoring resolved packages state from cache
[Package Manager] Registered 33 packages:
    com.unity.ai.navigation@2.0.5 (location: …\Library\PackageCache\com.unity.ai.navigation@9f76b145f0a8)
…
Assets\Scripts\Runtime\AxeVerifyCapture.cs(4,29): error CS0234: The type or namespace name
'Universal' does not exist in the namespace 'UnityEngine.Rendering'
Aborting batchmode due to failure:
Scripts have compiler errors.
```

**Mechanism.** `Library/PackageManager/` holds the resolved-state cache — on this machine
`ProjectCache`, `ProjectCache.md5`, `projectResolution.json`. Deleting `Library/PackageCache`
does **not** invalidate it, so the next launch *restores* the resolution, registers all 33
packages at paths that no longer exist on disk, skips re-extraction entirely, and then fails
compilation because the package assemblies are gone. `PackageCache` stays at **0** entries.

**It is deterministic, not a race** — 6/6 across three concurrent trials, plus 2/2 on a bare
re-run with no further deletion. A race does not fail 8/8 at the same ~5 s abort point. This is
exactly why the `solo`/no-reset control legs were added: without them this reads as
"concurrency corrupts the cache", which is the wrong conclusion.

**It does not self-heal.** Leg 3 re-ran with no deletion and failed identically, `EPERM=0`.

**Remedy that works, in one run, with no delete:** `touch Packages/manifest.json` (content
unchanged — `git status` stays clean). The mtime bump invalidates the state cache; the next run
logs `Done resolving packages in …`, `csErr=0`, `[BootstrapProject] complete`, and `PackageCache`
repopulates to 33 entries (leg 4).

### Two things this corrects in `unity-conventions.md`

1. §Process notes says the fix for a wedged cache is *"DELETE the (regenerable)
   `Library/PackageCache` then re-run — NOT another bare re-run (it keeps failing)"*. Measured
   here, that delete is **what creates** a wedge that a bare re-run cannot clear. The delete needs
   to be paired with a state-cache invalidation (`touch Packages/manifest.json`, or delete
   `Library/PackageManager` alongside it).
2. §Process notes also says *"A 2nd self-hosted runner on the SAME machine does NOT add throughput
   either — both share the `PackageCache`"*. The two checkouts do **not** share
   `Library/PackageCache` (per-checkout `location:` paths above). The 2nd-runner conclusion still
   holds — but it holds on the A/B-confirmed windowed-capture fragility, not on cache sharing.
   Keeping the wrong reason attached to a right conclusion is how the next reader "fixes" the
   cache and thinks the cap can rise.

### `Hypothesis:` — this may be a latent bug in `bootstrap_with_retry.sh`

`bootstrap_with_retry.sh:110` does `rm -rf "Library/PackageCache"` between attempts and does
**not** touch `Library/PackageManager`. If the resolved-state cache survives a real EPERM'd run,
attempt 2 would hit the wedge above and fast-fail with `error CS0234` — a signature
`is_transient_pkgcache_failure()` (`:73`) does **not** match — so the wrapper would `exit
"$fail_rc"` at `:99` on a *non-transient* verdict, reporting "Scripts have compiler errors"
instead of the EPERM it was retrying.

**This is NOT reproduced.** Manufacturing a genuine EPERM was not possible in this window (it
never fired once). It is plausible that a resolve which fails mid-EPERM never writes the state
cache, in which case the retry is safe. **Verify against a real EPERM before patching.**
Follow-up ticket owed; deliberately not fixed in this PR (no mid-PR scope expansion).

---

## What actually caps Unity concurrency here (all verified this session)

| Constraint | Evidence | Cache isolation helps? |
|---|---|---|
| Exactly ONE runner registered | `gh api repos/TSandvaer/Far-Horizon/actions/runners` → `total_count: 1`, `far-horizon-local`, `status=online busy=false` | No |
| `build` job is serialised repo-wide | `ci.yml:226-227` — `concurrency: group: unity-build` (no ref suffix) → at most one build job across the whole repo | No — this is a config cap, independent of caches |
| `capture` job pinned + serialised | `ci.yml:503` extra `capture` label + `:507-508` `concurrency: group: unity-capture` (absolute); A/B-confirmed 4/4 clean vs 3/3 flaked | No |

So the EPERM was never the binding constraint, and per-instance cache isolation buys **nothing on
CI** as currently configured. The only reachable win is **local two-worktree concurrency** — and
that already works today with **zero** env-var changes (legs 1 and 5).

---

## Recommendation

- **Do NOT raise the CLAUDE.md `≤1 Unity-build ticket in flight` cap.** Three independent reasons:
  CI cannot parallelise anyway (one runner + the repo-wide `unity-build` group); the
  windowed-capture pin is A/B-confirmed; and the local win needs no cap change because it is not
  a CI slot. (The brief scoped this out of the PR either way — recorded here as the spike's
  answer, not as a change.)
- **Do NOT bake `UPM_*` / `BEE_CACHE_DIRECTORY` into `serve_soak.sh` or `ci.yml`.** The mechanism
  works, but it would be a bake against a failure that does not reproduce, and it would cost a
  cold cache per invocation (real download + extract time, plus per-root disk). Erik's Step 3
  should be **declined on evidence**, not deferred. Revisit only if an EPERM is ever captured
  *with* two concurrent instances in the same log window.
- **Fix the documented wedge remedy** (this PR) and **file the `bootstrap_with_retry.sh`
  hypothesis** as a follow-up to verify against a real EPERM.
- **Two local concurrent builds are safe to run when the runner is idle** — demonstrated in leg 6
  (both `result=Succeeded`, ~1.2 GB RSS each, no isolation) — but they share cores, RAM and disk.
  The one measured multiple here is ≈1.64× on incremental builds at N=1; treat it as a hint, not a
  figure, and measure properly (N≥8, full builds) before any decision rests on it.

## Out of scope (unchanged)

2nd physical machine / VM; Unity Accelerator; standing up runner-2 (`86caffc23`); the CI
build/capture split (`86cafz9tg`, already landed); branch-protection changes; raising the
build-slot cap.
