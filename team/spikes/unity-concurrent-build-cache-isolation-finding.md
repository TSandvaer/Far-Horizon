# Finding — concurrent Unity builds on ONE machine: the PackageCache EPERM did not reproduce in the 2 resolving legs, and deleting `PackageCache` alone can itself wedge a warm checkout

**Status:** ✅ measured 2026-07-31 (spike `86cabkhjg`, editor `6000.4.11f1`).
**⚠ Text corrected 2026-07-31** (`86cazhkx9`, from the [#387 peer review](https://github.com/TSandvaer/Far-Horizon/pull/387#issuecomment-5145440412)): four over-claims narrowed — the effective sample (§Legs), the EPERM detector's missing negative control (§Negative control), the unscoped safety claim (§Recommendation), and this title. **No measurement changed and no leg was re-run**; the same raw logs are the source. What was measured stands — what it *supports* is narrower.
**Spec this answers:** `team/spikes/unity-concurrent-build-cache-isolation-spike.md`.
**Prior research this tests:** `team/erik-consult/concurrent-unity-build-isolation-research.md` (routes 1–4)
and `team/erik-consult/unity-concurrent-build-cache-isolation.md`.
**Harness (reproducible, committed):** `tools/debug/unity_concurrency_trial.sh`.
**Raw logs:** `%LOCALAPPDATA%\Temp\fh-conc\logs\` (gitignored scratch — per-leg names quoted below).

---

## Headline

1. **The EPERM did not reproduce — `EPERM` count = 0 in all 18 logs — but only 2 of the 8 legs can
   bear weight on that.** Two Unity `-batchmode` instances running concurrently from two worktrees
   completed cleanly — bootstrap *and* full `BuildWindows` — whether they SHARED one cold cache
   root, shared the REAL warm user cache *and* the real Bee cache, or had fully isolated roots.
   **⚠ Scope, added on review:** the EPERM is a rename at the `.tmp-* → com.unity.X@hash`
   extraction site, which exists **only during package resolution**. Only legs **1** and **5**
   actually resolved (`Done resolving packages in` = 1); legs **2/3** aborted pre-resolve and legs
   **6/7/8** are builds against a warm `PackageCache` — all five show `doneResolve=0`, so their
   "no EPERM" is **vacuous** (see §Legs). The honest line is **two concurrent resolving trials,
   four resolving instances, exactly one of them (leg 5) on the real user cache — all clean.**
   Against a documented low-rate flake, N=1 on the production condition is a **hint, not a
   refutation**. The isolation bake it was meant to justify still has nothing to justify it.
2. **Per-project `Library/PackageCache` is not a shared surface across worktrees at all** —
   so the documented rename collision is *structurally* not a cross-worktree contention.
   Unity prints the absolute per-checkout path for every registered package.
3. **The documented wedge remedy is INCOMPLETE — the delete alone can itself wedge a warm
   checkout.** Deleting `Library/PackageCache` while the rest of `Library/` is warm **WEDGES** the
   checkout, and a bare re-run does **not** clear it. `touch Packages/manifest.json` clears it in
   one run. **⚠ Scope, added on review:** this was measured from a **healthy warm** checkout. The
   documented remedy targets a **different initial state** — an already-partial/wedged
   `PackageCache` after a failed resolve, where `Library/PackageManager`'s validity is untested.
   So the finding is *"pair the delete with a state-cache invalidation"*, **not** *"the old remedy
   was simply wrong"* (see §Two things this corrects).
4. **Cache isolation is not what caps Unity concurrency here — and this result does NOT reopen the
   build-slot cap.** The cap rests on two **cache-independent** constraints: `ci.yml:226-228`
   (`concurrency:` / `group: unity-build` / `cancel-in-progress: false`, **no ref suffix** → one
   build job repo-wide regardless of caches) and `total_count: 1` registered runners. **The cap was
   never held in place by the EPERM, so proving the EPERM absent does not move it.** Do not re-run
   this spike expecting the cap to change. **Recommendation: do NOT raise the build-slot cap.**

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

`doneResolve` = count of `Done resolving packages in` in that leg's log(s). **It is the column that
decides whether a leg's `EPERM=0` means anything**, because the EPERM is a rename at the
`.tmp-* → com.unity.X@hash` extraction site and that site is touched **only during resolution**.

| # | Leg | Cache condition | `doneResolve` | Bears weight on EPERM? | Result |
|---|---|---|---|---|---|
| **1** | 2 concurrent bootstraps, both worktrees fully COLD (no `Library/` at all) | ONE shared cold throwaway root | **1 / 1** | ✅ **YES** | **BOTH `[BootstrapProject] complete`.** `EPERM=0` in both. Resolve 41.23 s (A) / 34.80 s (B), 33 packages each. |
| 2 | 2 concurrent bootstraps × 3 trials, warm `Library/` + deleted `PackageCache` | real shared user cache (no env vars) | **0** (×6) | ❌ vacuous — aborted pre-resolve | **6/6 FAILED** — `error CS0234` × 15, ~5 s each, `EPERM=0`. See §The wedge. |
| 3 | 2 concurrent bootstraps, NO reset at all (bare re-run of leg 2's state) | real shared user cache | **0** (×2) | ❌ vacuous — same pre-resolve abort | **2/2 FAILED identically.** `EPERM=0`. Proves the state does not self-heal. |
| 4 | 1 bootstrap (control) after `touch Packages/manifest.json` | real shared user cache | 1 | ❌ **solo** — 1 instance, cannot evidence *concurrent* contention | **`[BootstrapProject] complete`**, `csErr=0`, `PackageCache` back to 33 entries. |
| **5** | 2 concurrent FORCED re-resolves (`touch` both manifests) | real shared user cache — the faithful production condition | **1 / 1** | ✅ **YES** — and the only leg on the **real user cache** | **`EPERM=0` in both.** Resolve 1.22 s (A) / 26.78 s (B), both `[BootstrapProject] complete`. |
| 6 | **2 concurrent FULL `BuildWindows` builds** | real shared user cache + **real shared Bee cache**, no env vars at all | **0** | ❌ **vacuous** — warm `PackageCache`, rename site never touched | **BOTH `[FarHorizonBuilder] result=Succeeded size=115335954 bytes`.** A `00:59:00.293→01:01:17.459`, B `00:59:00.293→01:01:17.528` (≈2 m 17 s each, identical exe size). |
| 7 | 2 concurrent `BuildWindows` builds, incremental | fully ISOLATED per-instance `UPM_*` + `BEE_CACHE_DIRECTORY` | **0** | ❌ **vacuous** — same | Both `result=Succeeded size=115335954 bytes`. A 20.3 s, B 20.5 s. |
| 8 | 1 `BuildWindows` build, incremental (control) | isolated throwaway root | **0** | ❌ **vacuous** — same, and solo | `result=Succeeded size=115335954 bytes`, 16.8 s. |

**⚠ Corrected on review (`86cazhkx9`) — `2 of 8 legs are load-bearing`, not 8.** Reproduce the split
with one command over the surviving logs:
`grep -c "Done resolving packages in" %TEMP%\fh-conc\logs\*.log`.

**⚠ The claim "leg 6 IS the ticket's actual success criterion" is withdrawn as stated.** Legs
**6, 7 and 8 all show `doneResolve=0`** — they are builds against an already-warm `PackageCache`, so
no package was extracted, no rename occurred, and "no EPERM" follows **by construction**. Leg 6
therefore **cannot be evidence about the EPERM at all**; it satisfies "no EPERM" vacuously.

Leg 6 *is* strong evidence for a **different and still-useful claim**: two concurrent full builds
complete and emit byte-identical artifacts (`result=Succeeded size=115335954 bytes` in all five
build logs). **Keep the two claims separate** — the build-completion claim is well supported; the
EPERM claim rests only on legs 1 and 5.

**Overlap is measured, not asserted — by licensing timestamp, not by the probe.** Each Unity log
carries its own wall-clock `[Licensing::IpcConnector] Successfully connected` ISO line
(`build-default-1-A.log:12`); bracketing it against each log's final write:

| Leg | A start (Z) | B start (Z) | both end (Z) | overlap |
|---|---|---|---|---|
| 1 `shared-1` | `00:43:00.6055159` | `00:43:00.7208457` | `00:46:42.02` / `00:46:42.76` | ~3 m 41 s |
| 5 `faithful-default` | `00:55:34.865295` | `00:55:34.8483611` | `00:57:14.41` / `00:57:14.61` | ~1 m 39 s |
| **6 `build-default-1`** | `00:59:00.9687991` | `00:59:00.9288133` | `01:01:17.20` / `01:01:17.28` | **~2 m 16 s** |
| 7 `build-isolated-1` | `01:01:19.6131416` | `01:01:19.4283865` | `01:01:38.77` / `01:01:38.90` | ~19.3 s |

**⚠ Method correction (`86cazhkx9`):** the earlier sentence *"overlap is measured in every concurrent
leg"* was false as to method. The `tasklist` probe fires at `T+25 s`, and leg 7's two instances both
exited at ~19.3 s, so that probe printed nothing for leg 7 (and no probe output was captured for
legs 2/3/5/7). The overlap is real — the licensing-timestamp bracket above shows it — but the probe
demonstrates it only for legs 1 and 6. Leg 1's probe at `00:43:21.118` printed `Unity.exe 73152` and
`Unity.exe 183616`, launched 67 ms apart; leg 6's at `00:59:25.622` printed `105544` + `169688`,
~1.2 GB RSS each.

### Measured throughput — and why the number is weak

Legs 7 + 8 are the only apples-to-apples pair (both *incremental*, same warm state): two builds
finish in 20.5 s concurrently vs 2 × 16.8 s = 33.6 s serially → **≈1.64×**. That lands at the top of
Erik's estimated 1.4–1.6× band. **Do not quote it as settled:** N=1 per leg, incremental-only, and
the absolute times (17–20 s) are small enough that fixed process-startup cost dominates and noise is
proportionally large. A full-build pair (leg 6's ≈2 m 17 s class) was not run solo, so the
throughput multiple for a *cold* build is unmeasured. Anyone acting on a throughput number owes a
proper N≥8 measurement first.

---

## Negative control — the EPERM detector IS demonstrated to fire

**Added on review (`86cazhkx9`).** As originally written, the whole negative result rested on an
assertion that had **never been shown capable of going RED**. That is exactly the failure class this
project committed as a rule in **PR #383 (`ebaaf82`)**, and which `unity-conventions.md` §CI
architecture already states (`86cav8y74`): *"run the negative control through the SAME assertion and
require it to RED… a threshold nothing fails is not a threshold."*

The detector is `unity_concurrency_trial.sh:189` —
`grep -qE "EPERM: operation not permitted, rename"`. Run verbatim against a fixture built from the
real EPERM text quoted in `bootstrap_with_retry.sh:11-13`, and against a genuinely clean spike log:

```
=== POSITIVE CONTROL (known-bad fixture) ===
eperm=EPERM   <-- detector FIRED (RED)
=== same regex over build-default-1-A.log (real, clean) ===
eperm=-       <-- correctly silent
```

**The detector discriminates**: it reds on the real failure string and stays silent on a clean log.
So `EPERM=0` is no longer resting on an unexercised grep.

**Two limits, stated plainly:** the fixture is a *synthetic* reproduction of the string, not a
naturally-occurring EPERM — nobody has yet observed this detector fire on a real run in this repo;
and the widened bare-token `EPERM` grep across all 18 logs independently returns **0**, which covers
the "narrow pattern missed it" risk from the other direction.

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

   **⚠ Initial-state caveat (added on review, `86cazhkx9`) — this does NOT mean the old remedy was
   simply wrong.** The experiment deleted `PackageCache` from a **healthy warm** checkout (leg 1
   finished clean at 02:46; leg 2 deleted at 02:49). The documented remedy targets a **different
   initial state**: an already-partial/wedged `PackageCache` *after a failed resolve*, where
   `Library/PackageManager`'s validity is untested. That these are genuinely different states is
   supported by `bootstrap_with_retry.sh:6-7`, which records run `27699769706` EPERM'ing on a
   **cold** runner with **no pre-existing `Library/PackageCache`** — and that run *re-resolved*
   rather than restoring, so no state cache was valid there. The defensible statement is
   **"INCOMPLETE — pair the delete with a state-cache invalidation"**, which is safe and correct in
   *both* initial states. Do not upgrade it to "the remedy IS the wedge" on one run on one machine.
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

### ⛔ The single-build-slot cap is UNAFFECTED by this result — do not re-run this spike expecting it to move

**Recorded explicitly (`86cazhkx9`) so a future reader does not repeat the experiment hoping the cap
will shift.** CLAUDE.md's `≤1 Unity-build ticket in flight` cap is held by
**`.github/workflows/ci.yml:226-228`** — the `unity-build` `concurrency` group with
`cancel-in-progress: false` and **no ref suffix** — plus `total_count: 1` registered runners. Both
are **cache-independent**. **The cap was never held in place by the EPERM, so proving the EPERM
absent does not reopen it.** Any downstream ticket that was expected to shrink on this spike's
result should be re-planned on that basis.

What this spike *does* change is the **reason** attached to the cap: it removes a wrong one ("both
share the `PackageCache`" — they do not, the paths are per-checkout) while leaving the right
conclusion standing on the A/B-confirmed windowed-capture pin. `unity-conventions.md` §Process notes
already recorded the right root in June: *"ONE self-hosted runner … THAT is the hard serialization
ceiling; the PackageCache EPERM race is a SECONDARY amplifier, NOT the root."* That still holds.

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
- **⚠ SCOPED CLAIM (corrected on review, `86cazhkx9`) — two local concurrent *bare `BuildWindows`*
  invocations against a WARM `PackageCache` completed cleanly when the runner is idle** —
  demonstrated in leg 6 (both `result=Succeeded`, ~1.2 GB RSS each, no isolation) — but they share
  cores, RAM and disk. **This claim does NOT extend to `serve_soak`.** The original wording ("two
  local concurrent builds are safe") invites the reading *"two personas can `serve_soak` at once"*,
  which this spike **did not test**:
  - **`serve_soak` is untested** — it bootstraps (a real resolve) *and then* runs a **windowed
    capture**. Neither half was exercised concurrently.
  - **`-verify*` / capture gates are untested and were touched by no leg** — the runner-1-pinned
    windowed gates (`ci.yml:503`, `:507-508`) are the A/B-confirmed breakage surface, and **that is
    precisely where the runner-1 pinning constraint lives**.
  - **The CI composite is untested** — CI's `build` job chains `bootstrap_with_retry.sh` → EditMode
    → `BuildWindows` in one workspace (`ci.yml:295-337`); the spike never ran that concurrently.

  **Concurrent `serve_soak` / `-verify` remains untested and unclaimed.** The one measured
  throughput multiple here is ≈1.64× on incremental builds at N=1; treat it as a hint, not a figure,
  and measure properly (N≥8, full builds) before any decision rests on it.

## Out of scope (unchanged)

2nd physical machine / VM; Unity Accelerator; standing up runner-2 (`86caffc23`); the CI
build/capture split (`86cafz9tg`, already landed); branch-protection changes; raising the
build-slot cap.
