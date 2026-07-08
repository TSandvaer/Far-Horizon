# TRUE cause of the historical chop-cadence CI over-count (ticket 86camf6vz)

**Investigator:** Drew (independent — Devon authored the triage #282 + fix #288) · **Date:** 2026-07-08 · **Class:** investigation (non-build lane), no production/test code touched.

**TL;DR:** The `Time.captureDeltaTime` ("headless clock") hypothesis is **REFUTED** — the CI `[clock-probe]` measured `captureDeltaTimeHonored=True` (run 28926857959). Reconstructing the 8 pre-#288 `ChopTreePlayModeTests` reds from the actual #279 CI XML (run **28900340169**, merge-ref `9ba6dd2`) + the production code + the #288 diff, the reds split into **THREE distinct test-side mechanisms, none of them the `Time.time` clock**:

| # tests | mechanism | clock-dependent? |
|---|---|---|
| **5** cadence/over-count | test rig sets `chopInterval=0` → the held chain re-arms the next swing the SAME frame the clip completes → the harness's `while (SwingInProgress\|ImpactPending) yield null` steppers never see a stable false edge and spin to their wall-clock budget, landing extra swings | **NO** — reproduces with captureDeltaTime honored |
| **1** fade-visibility | demo tree's `MeshRenderer` added AFTER `ChoppableTreeState` captured `GetComponentsInChildren<Renderer>()` at construction → `IsVisible` reads an empty set → `false` | **NO** |
| **2** regrow | fade/regrow tween advances `_tweenT += Time.unscaledDeltaTime`; that clock froze in headless (tween never progressed → the felled tree never reached the regrow check), so the stump never regrew | a SEPARATE clock (`unscaledDeltaTime`), NOT the captureDeltaTime-pinned `Time.time` |

**Production defect count: ZERO.** All three are test-harness / test-rig / test-clock artifacts. Confirms (with per-test evidence) the "strong candidates" already noted in `unity-conventions.md` §Headless (the #288 doc bullet) and answers the "cause under separate investigation" it flagged.

**Method note (bounded claim):** I did NOT run the pre-#288 suite locally — a CI build was in flight on the self-hosted runner (run 28938889075), and launching local Unity would race the CI build on PackageCache (documented EPERM class, unity-conventions §Headless). Instead the mechanism is named from the **exact CI XML failure messages + line numbers** cross-referenced against the production `Update()`/`Tick()` flow and the #288 diff — which discriminate the three mechanisms unambiguously (the failing assert line + the "But was" value + the code path are decisive). A fresh local repro would only confirm magnitudes; it cannot change which assert failed or on which line.

---

## Evidence — the 8 reds from the #279 CI XML (run 28900340169), each mapped

All messages + line numbers quoted verbatim from `test-results-playmode.xml` (artifact `FarHorizon-playmode-9ba6dd2…`, id 8152030343, not expired).

### Mechanism A — the same-frame re-arm (5 tests, clock-INDEPENDENT over-count)

| Test | line | Expected → But was | `chopsToFell` | note |
|---|---|---|---|---|
| `HoldChain_OneCompletedSwing_IsExactlyOneChop_NoDoubleApply` | 669 | 5 → **12** | **12** | over-count = fell cap |
| `HoldChain_TreeFallsOnlyAfterNCompletedSwings` | 697 | 1 → **4** | **4** | over-count = fell cap |
| `HoldingLmb_RepeatsSwings_UntilReleased` | 514 | 3 → **5** | **5** | over-count = fell cap |
| `HoldChain_OneImpactPerSwing_NotInputPollRate` | 596 | 1 → **3** | 10 | intermediate assert; ~3 swings span the `while(ImpactPending)` window |
| `HoldChain_NextSwingWaitsForClipToFinish_NotImpactDelay` | 639 | False → **True** | 10 | `Assert.IsFalse(ImpactPending)` inside `while(SwingInProgress)` |

**The discriminating signature:** the three "fell-cap" tests each over-counted to **exactly their own `chopsToFell`** (12, 4, 5). That is the fingerprint of the chain running to fell *during a single harness step* — not a random clock leap (which would give varying, non-`chopsToFell` values).

**Root cause (code trace).** In the shipped `ChopTree.Update()`:
- impact resolves at the top of the frame (`ChopTree.cs:541`); then `if (SwingInProgress) return;` is the clip-completion gate (`:618`); then the inter-swing cooldown `if (Now - _lastChopAt < Mathf.Max(0f, chopInterval)) return;` (`:650`); then `BeginChopSwing` re-arms `_swingEndsAt = Now + …` (`:782`).
- The test SetUp sets **`_tree.chopInterval = 0f`** (old test `:116` — "no click cooldown in the test"). With the cooldown zeroed, the *only* gate between swings is `SwingInProgress`. On the frame the clip finishes (`Now >= _swingEndsAt`), the SAME `Update()` falls through `:618` (SwingInProgress now false), passes the zero cooldown at `:650`, and re-arms a new swing at `:782` — so after that frame's Update, `SwingInProgress` reads **true again**.
- The old harness stepper (`StepHeldSwing`) did `while (_tree.SwingInProgress && Time.time - start < 2f) yield return null;` (old test `:361`). Because the re-arm is same-frame, the poll never observes the false edge → the loop spins to its **2f wall-clock budget** (200 frames at the honored 0.01 step), completing several swings and landing chops until STOP-ON-FALL fires at `chopsToFell` (`ChopTree.cs:550`). Hence the fell-cap over-count.
- `HoldChain_OneImpactPerSwing` shows the same root via the *other* bounded wait: `while (_tree.ImpactPending && Time.time - start < 1f)` spans ~3 swing-impacts (cadence 0.4s in a 1s window) before the intermediate assert at `:596`.
- `HoldChain_NextSwingWaitsForClipToFinish` fails because `while(SwingInProgress)` cannot distinguish clip-1 from clip-2: with `chopInterval=0` clip-2 arms the instant clip-1 ends, so the loop's next iteration asserts `IsFalse(ImpactPending)` against clip-2's *legitimately* pending impact. The **production** gate is correct (no mid-clip interrupt); the harness just can't see the boundary without a cooldown gap.

**Why clock-independent:** the re-arm is same-frame regardless of the `deltaTime` value — the clock only changes *how many frames* the 2f/1f budget spans, never *whether* the re-arm is same-frame. So it reproduces with captureDeltaTime honored (locally and in CI). This is exactly why the #255 `Time.captureDeltaTime` pin could not have fixed these — and #288's fix (`StepUntil(() => _tree.Chops > before)`, an observable-COUNT primitive) does.

**Why the shipped game is fine** (corrected per Devon's review, comment 4914491970 — NIT-1): NOT a cooldown-created window. With the shipping defaults `swingClipLengthSeconds = 1.6f` (`ChopTree.cs:269`, or the live `MeleeClipLength`) ≫ `chopInterval = 0.25f` (`:219`), the cooldown — measured from `_lastChopAt` set at swing-begin (`:651`) — is long-elapsed by the time the ~1.6s clip finishes, so **production re-arms the next swing the same frame too**; there is no `SwingInProgress` false-edge window in production either. Production is safe because of **single-flight** (one impact/one chop per swing, `:586`) + clip-paced cadence + **STOP-ON-FALL** (`:550`), and decisively the **absence of any `while(SwingInProgress)` polling stepper** — that stepper is a pure test artifact. This agrees with line 42: #288's fix works by watching the observable `Chops` COUNT, not the un-observable `SwingInProgress` edge. The `-verifyChop` windowed capture passing at real framerate corroborates.

### Mechanism B — renderer captured before construction (1 test, clock-INDEPENDENT rig bug)

`ChoppedTree_FadesOutAndIsRemoved_AfterDelay_ThenRegrows`, **line 415**, `Expected True But was False`, message *"right after felling, the tree is still visible (mid-fell, pre-fade)"*.

The demo tree's `MeshRenderer` is added at old test `:410` — AFTER `_tree = _treeGo.AddComponent<ChopTree>()` in SetUp (`:107`). `ChoppableTreeState` captured its renderer set at construction (empty), so `IsVisible` returns false (`ChopTree.cs:971-974`). The FIRST post-fell assert (`Assert.IsTrue(IsTreeVisible)`, `:415`) therefore reds immediately — nothing to do with fade timing or any clock. #288 moved the `AddComponent<MeshRenderer>()` into SetUp before the `ChopTree` component (mirrors the real Boot tree, a real mesh at construction).

### Mechanism C — tween step rode `Time.unscaledDeltaTime` (2 tests, a SEPARATE clock)

| Test | line | Expected → But was |
|---|---|---|
| `FelledStump_RegrowsAfterTimer_IntoAChoppableTree` | 460 | False → **True** |
| `TreesDepleteAndRegrowIndependently` | 1035 | False → **True** |

Both fail `Assert.IsFalse(IsFelled, "the stump regrew…")` with **was True** — i.e. the tree was **still felled** at the end of the wait: it **never regrew**. (This corrects the triage's "regrew too fast" paraphrase — the XML shows the opposite: never progressed.)

Code trace: on the felling chop, `LandChop` sets `_felled = true` (IsFelled true immediately) AND `BeginFelling()` sets `_felling = true` (`ChopTree.cs:1035-1041`). `Tick()` then does `if (_felling) { StepFelling(); return; }` (`:1001`) — the state machine RETURNS EARLY while any tween flag is set. The regrow check (`if (Now >= _regrowAt) BeginRegrow();`, `:1007/:1015`) is only reached after `_felling`/`_fadingOut`/`_regrowing`/`_removed` are all false. The tweens advance `_tweenT += _dt` where `_dt = Time.unscaledDeltaTime` (old code; now `ChopTree.cs:990`). In the CI headless run `unscaledDeltaTime` did not advance deterministically → the tween froze → `_felling` never cleared → the Tick returned at `:1001` forever → the tree never reached the regrow check → still felled.

Note this is a THIRD clock, not the captureDeltaTime clock: the regrow *schedule* (`_regrowAt = Now + delay`, Now = `Time.time`) IS on the pinned clock and would fire correctly — but the state machine can't reach it because the *tween step* rode `unscaledDeltaTime`. captureDeltaTime pins `Time.time`/`Time.deltaTime` (probe-confirmed), not the tween's `unscaledDeltaTime`. #288 fixed it by driving `_dt` off the injected test clock (`:991-998`).

---

## Reconciliation with the refuted hypothesis

The #288 PR labelled a hypothesis: *"the self-hosted CI runner does not honor `Time.captureDeltaTime` under `-batchmode -nographics`."* The shipped `[clock-probe]` refuted it in CI (run 28926857959): `captureDeltaTimeHonored=True`, advance exactly 0.10s / 10 frames. Good process (labelled + shipped the disconfirming probe). With the clock exonerated, the reds' true causes are Mechanisms A/B/C above — all test-side, all confirmed against the #279 XML.

Corollary on the #255-era "validated locally, passed" assumption (triage line 56, labelled "likely"): Mechanism A is clock-independent, so the cadence tests could NOT have been green in *any* environment with the old `while(SwingInProgress)` stepper + `chopInterval=0`. If they looked green when #255 merged, it was because the advisory `playmode` job was hanging / not conclusion-checked (per the triage), not because the captureDeltaTime pin worked. Mechanisms B and C are the exceptions that could pass in the *windowed editor* (real renderer set present after the test-body AddComponent had run in a prior frame; real `unscaledDeltaTime`), which plausibly explains a partial "looks fine locally" read.

## Recommended doc follow-up (out of this ticket's owned scope — flagged, not applied)

- `unity-conventions.md` §Headless: the #288 bullet says the cadence cause is "under separate investigation" with "strong candidates." That investigation is now CLOSED (this note / ticket 86camf6vz) — a one-line edit can change "under separate investigation" → "confirmed (86camf6vz): the three mechanisms A/B/C." Mechanical; left for the owner of that doc since this PR's scope is `team/analysis/*` + STATE.md.

## Evidence index (all fetched, none extrapolated)

- CI probe: run **28926857959** (merge-ref `4a748e1b`, branch `f401983`) — `[clock-probe] … captureDeltaTimeHonored=True`, quoted in Tess's QA comment 4912724349 on PR #288.
- Historical reds: run **28900340169** (#279, merge-ref `9ba6dd2`), artifact `FarHorizon-playmode-9ba6dd2…` (id 8152030343) → `test-results-playmode.xml`, `total=272 failed=10` (8 ChopTree + NonAxeHeldScale + BerryEat). The 8 ChopTree messages + line numbers quoted above.
- Pre-#288 test code: `Assets/Tests/PlayMode/ChopTreePlayModeTests.cs` @ `c810a8e` (SetUp `chopInterval=0f` :116; `StepHeldSwing` `while(SwingInProgress…)` :361; renderer AddComponent :410).
- Production: `Assets/Scripts/Runtime/ChopTree.cs` @ HEAD — `Update()` :541/:550/:618/:650/:782; `chopInterval=0.25` default :215; `ChoppableTreeState.Tick()` :983-1022 (early-return at :1001), `LandChop` :1030-1045, tween `_dt` :990.
- #288 merge `cbcf474`: seam-only production diff (`Time.time`→`Now`, `_dt` tween seam) + the harness `StepHeldSwing`→`StepUntil(Chops>before)` + renderer-to-SetUp + docs bullet.
