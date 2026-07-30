# Advisory PlayMode job — behavior-change + 10-failure triage (ticket 86cama53u)

**Investigator:** Devon · **Date:** 2026-07-08 · **Class:** investigation (non-build lane), no production code touched.

**TL;DR:** The advisory `playmode` job stopped hanging at play-mode-enter and now **completes in ~2 min** because of **PR #249** (`581668a`, ticket `86cabfa21`, merged 2026-07-03) — "InitTestScene unbounded-unload was the root cause". Once it completes, it deterministically reports **10 failures**. All 10 triage to **test-harness / test-setup / headless-clock artifacts — ZERO production bugs**. The advisory→required promotion is NOT ready (a required job cannot ship red) but is CLOSE: the failures are deterministic and test-only; fix the 3 harness classes → 0 failures → then the Sponsor can flip it.

---

## AC1 — the 10 failing tests + what changed to make the job complete

### What changed (cause of hang→complete)

Evidence — main-associated `ci.yml` run conclusions flip cleanly at 2026-07-03:

| Date | run | SHA | run-conclusion | playmode job |
|---|---|---|---|---|
| ≤ 2026-07-01 | 28515601099 … 28350246749 | dde3bcf … | **cancelled** | hang → ~21-min timeout |
| 2026-07-03 21:42 | (from #249 onward) | 581668a | failure/success | **completes ~2–4 min** |
| 2026-07-06 19:29 | 28817657531 | bb54dee (#265) | success | playmode job = **failure, completes 2.5 min** |
| 2026-07-07 21:35 | 28900340169 (PR #279) | merge-ref 9ba6dd2 | — | playmode = **failure, completes 3.1 min** |

Cause: **PR #249 (`581668a`, `86cabfa21`) — "fix(ci): PlayMode suite no longer hangs — InitTestScene unbounded-unload was the root cause"** (merged 2026-07-03 21:42). This is NOT the Configurable-Enter-Play-Mode setting (that's been `m_EnterPlayModeOptions: 3` since #61, `e49bcc3` — unchanged). The `#274–#281` window the ticket suggested is a **red herring**: the flip predates it by 3 days.

Bounded note: a few post-#249 runs still show run-level `cancelled`, but NOT from a playmode hang — e.g. run 28715420406 (#250) playmode ran only ~4 min then the RUN was cancelled by a failing `capture` job; run 28734652691 was a superseded push (structure/build cancelled early at ~1 min). The playmode job itself completes post-#249.

**Doc staleness (recommend update — I did NOT edit, read-only scope):** `unity-conventions.md` line 19 ("The advisory `playmode` job ALWAYS hangs at play-mode-enter and is cancelled at its own ~21-min timeout") and memory `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]` are now **STALE** — the job completes since #249 (2026-07-03). Both should be corrected.

### The failure set is NOT stable across the cited runs

The ticket says "same 10" on run 28817657531 and PR #279. It IS 10 in both, but **membership differs** — the cited run 28817657531 is at `bb54dee` (#265), **14 commits behind current HEAD** (`ddd92aa`, #279):

| # | Test | #265 run (28817657531) | LIVE / #279 run (28900340169) |
|---|---|---|---|
| 1–8 | `ChopTreePlayModeTests` (8) | RED | RED |
| 9a | `HeldBeltWeaponVisualPlayModeTests.DebugCycle_RefusedWhileWeaponSelected_…` | RED | **GREEN — fixed by #275 (`bfdbbc2`, `86cajt6jz`)** |
| 9b | `HeldWeaponDialPlayModeTests.NonAxeHeldScale_NudgeFactor_ResizesLiveScale` | (test post-dates #265) | **RED (new)** |
| 10 | `LeftClickConsumePlayModeTests.LeftClick_BerryEat_RefillsTheHungerHudBar` | RED | RED |

**The LIVE (current-HEAD) failure set is:** 8× ChopTree + NonAxeHeldScale + BerryEat = 10.

---

## AC2 — triage (PR #251 taxonomy: real bug / stale-test / env-quarantine)

### Group A — 8× `ChopTreePlayModeTests` → headless-clock over-count; PR #255 pin present but INEFFECTIVE in CI

> **⚠ CORRECTION (Drew, ticket 86camf6vz, 2026-07-08) — the Group-A "headless-clock over-count" reasoning below is REFUTED.** The CI `[clock-probe]` (run **28926857959**) measured `captureDeltaTimeHonored=True` on the self-hosted runner — so `Time.captureDeltaTime` IS honored in CI, the #255 pin was NOT "ineffective because CI doesn't honor it," and the reds are NOT a "`deltaTime≈0` headless cadence over-count." Reconstructing the 8 reds from the #279 CI XML (run 28900340169) + the production code shows **three distinct test-side mechanisms, none of them the `Time.time` clock**: (A) **5 cadence reds** = a clock-INDEPENDENT harness bug — the rig sets `chopInterval=0`, so the held chain re-arms the next swing the SAME frame the clip completes and the old `while (SwingInProgress\|ImpactPending) yield null` steppers spin to their wall-clock budget, over-counting to `chopsToFell`; (B) **1 fade red** (`ChoppedTree_FadesOutAndIsRemoved`, line 415) = the renderer-captured-before-construction rig bug (empty renderer set → `IsVisible=false`); (C) **2 regrow reds** = the fade/regrow tween rode `Time.unscaledDeltaTime` (a SEPARATE clock the captureDeltaTime pin doesn't touch), which froze in headless so the state machine never reached the regrow check — the stump never regrew (the XML shows `Assert.IsFalse(IsFelled)` → **was True**, i.e. NOT the "regrew too fast" claimed below — the opposite). **Zero production bugs — unchanged.** Full per-test evidence + line-number mapping: `team/analysis/2026-07-08-chop-overcount-truecause.md`. The specific stale sentences to read past below: "headless-clock over-count", "INEFFECTIVE in CI", "the exact `deltaTime≈0` headless cadence over-count", "the pin is not effective in the CI headless environment", and "regrew too fast".

Failure signatures (identical in both runs):
- `HoldChain_OneCompletedSwing_…NoDoubleApply`: Expected 5, **was 12**
- `HoldChain_OneImpactPerSwing_NotInputPollRate`: Expected 1, **was 3** (over 20 idle frames)
- `HoldChain_TreeFallsOnlyAfterNCompletedSwings`: Expected 1, **was 4**
- `HoldingLmb_RepeatsSwings_UntilReleased`: Expected 3, **was 5**
- `HoldChain_NextSwingWaitsForClipToFinish_NotImpactDelay`: Expected False, **was True** (swing began mid-clip)
- `ChoppedTree_FadesOutAndIsRemoved_…`: Expected True, **was False** (faded before the post-fell visible window)
- `FelledStump_RegrowsAfterTimer_…` + `TreesDepleteAndRegrowIndependently`: Expected False, **was True** (regrew too fast)

This is the **exact `deltaTime≈0` headless cadence over-count** documented in `unity-conventions.md` line 13 — the family PR #255 (`8785d57`, `86cajt6j8`) claimed to stabilize with a `Time.captureDeltaTime` fixed-clock harness. **The pin IS present in source** (`ChopTreePlayModeTests.cs:76` sets `Time.captureDeltaTime = 0.01f` in SetUp, `:142` restores 0 in TearDown) — yet the over-count PERSISTS in CI at both `bb54dee` and current HEAD. **So the pin is not effective in the CI headless `-batchmode` PlayMode environment** (the #255 fix was likely validated in a context where the pin IS honored — local editor/windowed — and never re-checked in the completing CI job because the job was advisory + was hanging/uncancelled-checked when #255 merged 2026-07-05).

**Verdict: NOT real bugs.** The documented discriminators all hold: `ChoppableTreeState.LandChop` increments once per call behind an `IsChoppable` guard; `ApplyChopEffect` is single-flighted by `_impactPending`; and the **shipped windowed `-verifyChop` capture PASSES at real framerate** (the `capture` job on PR #279 run 28900340169 = **pass**). → **stale-test class (fix the harness, keep them LIVE — do NOT `[Ignore]`-quarantine; they still guard the machine-gun-chop bug class).**

### Group B — `HeldWeaponDialPlayModeTests.NonAxeHeldScale_NudgeFactor_ResizesLiveScale` → test-harness stale before-snapshot

Failure: `Assert.Greater(holder.transform.localScale.x, holderScaleBefore.x)` → Expected greater than **1.0**, was **0.892499983**.

`WeaponMeshScale = { 1f, 0.85f, 0.95f, 0.90f }` — the KNIFE (index 1) baseline is **0.85**, and **0.85 × 1.05 = 0.8925** = the exact "But was". So the nudge math WORKS (knife scaled 0.85→0.8925; `Assert.Greater(_cycle.CurrentScale, scaleBefore)` at :146 PASSED). The test snapshots `holderScaleBefore` at :142 as **1.0** — the default cube localScale — because setting `_index=1` via reflection (:136) does not settle the knife's 0.85 baseline onto the holder before the snapshot. 0.8925 < the stale 1.0 → red.

**Verdict: NOT a real bug — test-harness bug.** New test from #270 (`8d49cb3`, `86cakkfz9`) that never ran green in the (advisory) CI job. → **stale-test class.**

### Group C — `LeftClickConsumePlayModeTests.LeftClick_BerryEat_RefillsTheHungerHudBar` → test-setup bug (berry not in selected belt slot)

Failure: `Assert.IsTrue(ate, "the LEFT-CLICK eat fires")` — `TryConsumeSelected()` returned false.

Decisive log evidence (PR #279 run playmode.log): at the failing assert (`…cs:272`, log line 16531) the trace immediately prior is `[consume-trace] left-click with no consumable selected (or guard held) -> no-op` (line 16522) — i.e. `ShouldConsumeOnClick` returned false. A **sibling test in the SAME run eats a berry via the same left-click seam successfully** ("`[consume-trace] ate 1 berry via left-click -> berries=2`", line 16599). So the seam + `UiInputGate` gate are fine; only THIS test's berry never landed in a **selected belt slot** — its `inv.Model.AddItem(def,1)` + belt-slot-search-then-`SelectBelt` loop (`…cs:257–264`) no-ops when the single berry doesn't fill a belt slot → default belt slot 0 empty → `SelectedBeltStack.IsEmpty` → consume no-ops.

**Verdict: NOT a real bug — test-setup bug.** The shipped left-click berry-eat is a soaked M-U2 feature AND a sibling test proves the seam. → **stale-test class.**

---

## AC3 — recommendation

**Real production bugs found: ZERO.** All 10 LIVE failures are test-harness / test-setup / headless-clock artifacts. DebugCycle (in the cited-run set) is already fixed by #275.

**Advisory→required promotion: NOT ready, but close.** A required job cannot ship red, and all 10 are red today. But they are (a) deterministic (identical set across two runs), (b) test-only (no production defect), (c) fixable. Sequence: land the 3 draft fix-tickets below → confirm the CI playmode job reaches **0 failures on main** across ≥2 consecutive runs → THEN the Sponsor flips `playmode` from advisory to required in `ci.yml` (a `.github` change, sponsor-gated — OOS here, do NOT flip).

### Draft fix-tickets (orchestrator files)

1. **`fix(playmode): chop-family cadence tests over-count in CI despite the #255 Time.captureDeltaTime pin`** [Devon, S-M, test-only]. 8 tests in `ChopTreePlayModeTests`. The pin at `:76` is present but ineffective in CI `-batchmode`. AC: (a) determine whether `Time.captureDeltaTime` is honored in headless `-batchmode` PlayMode (it appears NOT to be — over-count persists); (b) if not, restructure the cadence assertions to be clock-independent — e.g. count COMPLETED swings via the production single-flight guard, or inject a deterministic fake clock into `ChopTree`, or drive frame counts via explicit `yield return null` loops rather than `WaitForSeconds`/time windows; (c) tests must stay LIVE (still red on a real machine-gun-chop regression), NOT `[Ignore]`-quarantined. Ref `86cajt6j8`. Production unchanged (`-verifyChop` green).
2. **`fix(playmode): HeldWeaponDialPlayModeTests.NonAxeHeldScale stale before-snapshot`** [Devon/Drew, S, test-only]. Snapshot `holderScaleBefore` AFTER the knife's 0.85 baseline settles onto the holder (a frame / explicit apply), or compare against the settled baseline rather than the default 1.0. Ref `86cakkfz9` (#270). Nudge math is correct (0.85×1.05=0.8925).
3. **`fix(playmode): LeftClick_BerryEat_RefillsTheHungerHudBar berry not in selected belt`** [Devon/Drew, S, test-only]. Guarantee the berry is in the SELECTED belt slot before `TryConsumeSelected()` (reuse the shared `GiveAndSelect` helper the sibling water tests use). Confirmed via `[consume-trace]` no-op at the failing assert + a sibling berry-eat succeeding in the same run.

### Recommended doc/memory updates (read-only scope — flagged, not applied)

- `unity-conventions.md` line 19 + memory `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`: correct the "ALWAYS hangs at play-mode-enter" claim — the job COMPLETES since #249 (2026-07-03). Soaks remain the interaction gate until the harness reds are fixed + the job is promoted.
- Add to `unity-conventions.md` §Headless: `Time.captureDeltaTime` may NOT be honored in headless `-batchmode` PlayMode (CI evidence: the #255 chop pin is ineffective there) — prefer clock-independent cadence assertions over `Time.captureDeltaTime` for CI-run PlayMode tests.

---

## Evidence index (all fetched, none extrapolated)

- Main run **28817657531** (`bb54dee`/#265, 2026-07-06): playmode job `85461642246` = failure, step "PlayMode tests" 19:34:17→19:36:12 (~2 min, completes); artifact `FarHorizon-playmode-bb54dee…` (id 8119571736).
- PR #279 run **28900340169** (merge-ref `9ba6dd2`, 2026-07-07 21:35, includes #275): playmode fail 3m11s (completes); `capture` job = pass; artifact `FarHorizon-playmode-9ba6dd2…` (id 8152030343).
- XML summaries: #265 run `total=271 failed=10`; #279 run `total=272 failed=10`.
- Commits: #249 `581668a` (2026-07-03), #255 `8785d57` (2026-07-05), #270 `8d49cb3`, #275 `bfdbbc2` (2026-07-07 21:01), #279 `ddd92aa` (2026-07-07 22:13), enter-play-options `e49bcc3` (#61).
- Source: `ChopTreePlayModeTests.cs:66-76,142`; `HeldWeaponDialPlayModeTests.cs:129-154` + `HeldWeaponCycleDebug.cs:144` (`WeaponMeshScale`); `LeftClickConsume.cs:121-197` + `UiInputGate.cs:24,54-57`.
