# Gameplay-Wave Acceptance & Test-Coverage Plan

**Scope:** the QA acceptance + test-coverage plan the wave's QA pass runs against. ALL FOUR tickets now FULL depth: THIRST `86caamkv7`, CHOP `86caa4c5c`, STICKS/BRANCHES `86caa96rd`, STONES `86caa4c96` — fleshed against ground-truth `main` surfaces (`ChopTree.cs`, `BerryBush.cs`, `Inventory.cs`/`ItemCatalog`, `LowPolyZoneGen.ScatterIslandProps`). Dispatch-ready: an author/QA can run a pass against any of the four without a clarifying question.

> **⚠ ITEM-ID GROUND-TRUTH (read before any per-ticket review).** The ticket PROSE says `chopped wood` / `picked up stones`, but the SINGLE source of truth — `ItemCatalog` on `main` — declares `WoodId = "wood"` (display `"Wood"`) and `StoneId = "stone"` (display `"Stone"`). The catalog already ships the `stone` def in `BuildDefaults`. The vocabulary contract in each section pins the real id; a PR that mints `"chopped wood"`/`"picked up stones"` as a NEW item id = REQUEST_CHANGES (mergeability-blocking — it won't stack with the canonical item).
**Authored:** wave-support prep against the LOCKED ACs (incl. Priya's 2026-06-19/06-23 AC clarifications) + the dispatch map `team/priya-pl/gameplay-wave-plan.md` + ground-truth `main` surfaces. AC-driven, not diff-driven — re-confirm every claim against the real diff when each PR opens.
**Relationship to the per-ticket plans:** this is the WAVE-LEVEL companion. The per-ticket plans (`thirst-need-acceptance-plan.md`, `chop-tree-acceptance-plan.md`, `sticks-branches-acceptance-plan.md`, `stone-pickup-acceptance-plan.md`) hold the per-AC depth; THIS doc owns (a) the FULL thirst plan refreshed against the merged `SurvivalNeed` base, (b) the cross-need / cross-lane integration matrix no single ticket owns, and (c) the wave-wide silent-killer register + soak-probe handoff. Where a per-ticket plan and this doc disagree on a concrete surface, the ground-truth `main` source wins — cite it.

**Ground-truth surfaces read at HEAD on `main` (031d43a):**
- `Assets/Scripts/Runtime/SurvivalNeed.cs` — the SHARED abstract need base (hunger `86caamkp8` landed it; thirst EXTENDS it). Surface: `Current01` / `Current` / `Max` / `IsCritical` / `event Action<float> Changed` / `TickSeconds(float)` / protected `Satisfy(float)` / `SatisfyFull()` / `decayPerSecond` / `floor01` / `criticalThreshold01` / `startFull` / `startFraction01` / `ApplyDifficulty(DifficultyTier)` + `easy/med/hardDecayPerSecond`. `Start()`/`Update()` are `protected virtual`.
- `Assets/Scripts/Runtime/WarmthNeed.cs` + `WarmthNeedTests` / `WarmthNeedPlayModeTests` / `WarmthNeedSceneTests` — the need test-shape template.
- World-gen seam: `Assets/Scripts/Editor/LowPolyZoneGen.cs` + `WorldBootstrap.cs` (`IslandShoreR = 120u`).
- Scatter / NavMesh / seed-42 guards: `RoundIslandNavTests`, `RoundIslandNavCoveragePlayModeTests` (≥90% coverage, worst-ring ≤ azimuths/4, raycast from the SHIPPED Ground collider), `RockScatterPlayModeTests`, `SeededScatterVariationTests`.
- Inventory + interaction seam: `Assets/Scripts/Runtime/Inventory.cs`, `AxePickup.cs`; tests `InventoryModelTests`/`InventoryFacadeTests`/`InventoryBeltHeldAxePlayModeTests`; harvest precedent `BerryBushPlayModeTests` / `EatBerryActionPlayModeTests`.

---

## 0. Wave process-gate preflight (HARD gates — apply to EVERY wave PR; bounce on any miss)

Every wave ticket is Sponsor-gated + UX-visible. The bar (TESTING_BAR.md) is non-negotiable:

1. **Self-Test Report comment** present before I review — what was run, **on which build stamp** (`BUILD <tag> | <UTC> | <sha>` from the HUD), what was observed. Concrete values only, never invented. Missing → REQUEST_CHANGES (hard gate).
2. **Regression guard line in the Done clause** (PR #216 process gate) + **cross-lane integration check** in the Self-Test Report — naming the specific cross-lane seams that ticket crosses (see §5 matrix).
3. **Shipped-build capture** from the BUILT exe (windowed launch, in-game capture, HUD build-stamp visible) attached/quoted — editor evidence is necessary, never sufficient (editor-vs-runtime divergence class). For any new visual surface (pond, sticks, stumps, small-stone scatter) the **player-framing gameplay-cam** capture is the grounding gate, NOT a top-down/metrics check (`verify-grounding-soaks-by-gameplay-cam-visual` — top-down + metrics have both served a floating build).
4. **Frame-Debugger / SRP-batcher audit** quoted in the Self-Test Report for any PR adding a new shader/material/scatter renderer (no MPB break; all props inside `CBUFFER_START(UnityPerMaterial)`).
5. **CI green** cited by run ID on the PR head SHA. An EMPTY run (`total == 0`) is a FAILURE, not a pass. ⚠ The CI playmode job is unreliable (hangs at play-mode-enter) — "CI green with playmode skipped" ≠ pass for interaction-only ACs; the SOAK is their real gate (`advisory-playmode-job-unreliable-soak-is-interaction-gate`).
6. **No diff touch on `team/DECISIONS.md`** by a non-Priya PR → bounce.
7. **Diagnose-Before-Fix** for any `fix(...)` PR in the wave: diagnosed root cause + one cited isolation result in the body BEFORE the fix, else bounce.
8. **Sample-size discipline (N≥8)**: any "deterministic" / "always" claim in a Self-Test Report needs ≥8 runs of evidence, not N=3.

---

## 1. THIRST `86caamkv7` — FULL acceptance plan

**Feature (two lines):**
(1) A `ThirstNeed : SurvivalNeed` MonoBehaviour that EXTENDS the merged base and adds a typed `AddWater(amount)` hook routing through the protected `Satisfy` primitive — surface byte-identical to warmth/hunger (decay over a real `Time.time` window; floor not fail).
(2) A small FRESHWATER POND in the seed-42 island world (distinct from the salt sea) with a DRINK-FROM-HAND interaction: proximity + interact → small per-scoop restore, repeatable, **no tool, no inventory item**.

**Ownership split (AC1a/AC3a, pinned):** Devon owns the need model + `AddWater` + the drink interaction; Drew owns the pond placement. The drink-scoop → `AddWater` seam test is OWNED by this ticket (no coverage gap to assume away). May land as one coordinated PR or two — the AC3/AC6 drink-seam test lands with whichever PR ships the drink interaction.

### 1a. AC → verification-method map

| AC | Expectation | Verification layer | Interaction-only? (soak = real gate) |
|---|---|---|---|
| **AC1** decay | `ThirstNeed` decays over a `Time.time` window; surface mirrors warmth/hunger EXACTLY; decay tuned per fiction ("thirsty after berries" → faster than hunger default). | **EditMode** (`TickSeconds(t)` drops `Current` by `decayPerSecond*t`; surface-parity asserts) + **PlayMode** (measurable real-window drop — deltaTime≈0 trap). | No — testable headless. |
| **AC2** pond | Small freshwater pond in seed-42 world, distinct from the sea, cute/warm low-poly, integrated with world-gen; does NOT break island shape / NavMesh / scatter. | **EditMode** (pond present in seed-42 gen + island-shape + existing-scatter byte-invariance — mirror `RoundIslandNavTests`/`SeededScatterVariationTests`). **PlayMode** (NavMesh-coverage non-regression — `RoundIslandNavCoveragePlayModeTests` shape). **Shipped capture** (reads as fresh water vs sea; grounded). | Fresh-vs-salt READ + grounding = **soak/visual** gate. |
| **AC3** drink | Proximity + interact → small per-scoop restore; repeatable; no tool; **no inventory item**; per-scoop amount tweakable. | **PlayMode** (scoop near pond → thirst +per-scoop; N scoops → N× up to max; scoop FAR → no change; inventory UNTOUCHED). **Shipped capture** (walk to pond, scoop, readout rises). | **Yes — interaction-only → SOAK is the real gate.** |
| **AC4** floor | Thirst stops at `floor01`; NOT a fail-state. | **EditMode** (`TickSeconds(huge)` rests at floor; holds; no overshoot to 0). | No. |
| **AC5** settings | `thirst decay rate` + `water scoop amount` (opt. floor/critical) registered into the settings panel `86caa4bqp`. | **EditMode/PlayMode** registry test (mirror `86caa4bqp` AC6 — setting BINDS + DRIVES the live param, not a stored value). | Live-binding correctness = test; perceived effect = soak. **HARD-DEP on `86caa4bqp`** (AC5a) — if unmerged, ship behind a named extension hook + follow-up, do NOT block the need on the panel. |
| **AC6** guard | Paired EditMode + PlayMode: decay over a window; pond present + on/near NavMesh + reachable (no seed-42/NavMesh break); scoop → +amount; floor halts decay; `Changed` on decay AND scoop. | Both suites green from `-testResults` XML `<test-run result="Passed">`. | — |

### 1b. Runtime-vs-test-layer traps to assert against (the silent killers)

1. **`base.Start()` / `base.Update()` must be called.** `SurvivalNeed.Start()`/`Update()` are `protected virtual` and seed `_current` + `_lastTickTime` + fire the initial `Changed`. If `ThirstNeed` overrides either and forgets `base.<Method>()`, decay silently never ticks AND the HUD never gets its first paint — and a headless EditMode test that drives `TickSeconds` directly **passes anyway** (it bypasses Start/Update). **Guard:** a PlayMode test that adds `ThirstNeed` via `AddComponent`, yields a frame, and asserts the first `Changed` fired + `Current` decays over a real window — NOT just a `TickSeconds` math test.
2. **Drink satisfies the VISIBLE need value, not just backing data** (`soak-fail-test-pass-instrument-runtime`). `AddWater` must route through `Satisfy` → `SetCurrent` → fire `Changed`, so `Current01` (the bar fill) actually moves. Assert on `Current01` / the `Changed` payload, not a private field. A scoop that mutates a backing field without firing `Changed` green-passes a data assert but freezes the HUD bar.
3. **`Changed`-on-scoop is load-bearing** (HUD subscribe-never-poll). `Changed` must fire on decay AND on every scoop. `SetCurrent` already no-ops when the value is unchanged (`Mathf.Approximately`) — so a scoop AT max fires nothing; that's correct, but a scoop with headroom MUST fire. Assert the `Changed` event count rises by exactly 1 per effective scoop.
4. **Proximity gating is load-bearing.** A scoop that restores ANYWHERE (no distance check) green-passes "thirst rose" and breaks the fiction. Always assert the NEGATIVE: a scoop FAR from the pond does NOTHING (no `Changed`, no value move).
5. **Water-as-inventory-item regression.** Thirst is NOT berries. A copy-paste from the berry eat-action that routes water through `Inventory.AddItem` is a design break. Assert inventory item-count is UNCHANGED across a scoop (no entry created/consumed).
6. **Seed-42 LOCK** (`world-is-big-round-island`). The pond is a DETERMINISTIC ADD on the locked seed-42 terrain (AC2a) — it must NOT re-roll the seed, shift the silhouette, move existing tree/rock/bush/stick scatter, or shrink NavMesh coverage. **Guard:** pre/post island-shape + existing-scatter byte-invariance (EditMode) AND NavMesh-coverage non-regression vs pre-pond (PlayMode, ≥90% + worst-ring ≤ azimuths/4). If an inland pond can't be placed without perturbing the seed-42 stream → **SPONSOR-GATE escalation, do NOT silently re-roll** (seed lock outranks pond placement).
7. **Floating pond** (`verify-grounding-soaks-by-gameplay-cam-visual`). Verify the pond sits IN the terrain via the player-framing gameplay-cam capture, not top-down/metrics — both have lied (served a floating build twice).
8. **deltaTime≈0 headless false-green** on decay — the PlayMode real-`Time.time`-window test is mandatory; an EditMode-only decay proof is insufficient (it bypasses the Update path that the shipped exe actually runs).
9. **Difficulty-tier wiring** (new vs the old per-ticket plan — the base now carries it). `ThirstNeed` inherits `ApplyDifficulty` + `easy/med/hardDecayPerSecond`. Assert thirst's three tiers are set sensibly (easy < med < hard) and `ApplyDifficulty(tier)` copies the right rate into the active `decayPerSecond`. A tier change must take effect live (no restart).

### 1c. Vocabulary contract (mergeability-blocking — REQUEST_CHANGES, not a NIT)

`ThirstNeed` EXTENDS the merged `SurvivalNeed` from `main` (AC1a, Pattern A — hunger landed the base). At review, grep the diff: it must NOT re-declare a base type, MUST inherit `Current01`/`Current`/`Max`/`IsCritical`/`Changed`/`TickSeconds`/`Satisfy` verbatim, and add only the typed `AddWater`. ANY divergence (a differing base name, a re-shaped `Current01`, a parallel decay model) = **REQUEST_CHANGES (mergeability-blocking)**.

### 1d. Thirst soak-probe list (Sponsor: test & confirm THIS — explicit per `soak-handoff-path-and-explicit-test-checklist`)

Exe: `Build/Windows/FarHorizon.exe` — verify the HUD build stamp matches the PR head SHA before judging.
1. Walk the island, FIND the freshwater pond — it reads CLEARLY as fresh water, distinct from the surrounding salt sea (not a sea inlet). Confirm it sits IN/on the terrain (not floating/sunk).
2. Stand at the pond, drink (the interact input) — each scoop restores a SMALL amount of thirst; the thirst readout (bar once `86caamkxv` lands; else the trace) RISES per scoop. Repeatable (multiple scoops keep restoring up to full).
3. Try to drink FAR from the pond — nothing happens (no restore away from water).
4. Confirm thirst DECAYS over time as you walk (the bar drifts down); it presses sooner than hunger ("thirsty after the berries").
5. Confirm the rest of the world is UNCHANGED — same island shape, trees/rocks/bushes/sticks in the same places, you can still walk everywhere (the pond didn't shift the world).
6. (If settings panel present) the `thirst decay rate` + `water scoop amount` sliders visibly change behaviour live, no restart.

---

## 2. CHOP TREES `86caa4c5c` — FULL acceptance plan

**Feature (two lines):**
(1) GENERALIZES the shipped thin `ChopTree.cs` (on `main`): proximity (`chopRadius 2.2u`, planar XZ to the player root) + the axe gate → paced discrete chops (`chopInterval 0.6s`) that add `WoodPerChop` wood to the inventory; after `chopsToFell` the tree FELLS (sink+tip `StepFelling` tween) and leaves a STUMP.
(2) ADDS the two new behaviours the ticket needs on top of that shipped seam: a regrow timer (stump → tree after a RANDOM delay in a serialized `[min,max]` window, mirroring `BerryBush.ScheduleRegrow`) and the tweakable knobs (`tool-use speed` = chop-anim/interval; `tree regrowth time` min/max) exposed as NAMED serialized fields then registered into the settings panel.

**Ground-truth on `main` (read at HEAD):** `ChopTree.cs` already ships `woodPerChop`/`chopsToFell`/`chopInterval` serialized fields, the axe gate (`inventory.HasAxe`), `inventory.AddWood(n)` (a shim that forwards to `Model.AddItem(woodDef, n)`), `Chop()`/`BeginFelling()`/`StepFelling()`, and `Chops`/`IsFelled` test accessors. It does NOT yet ship: the regrow timer, the stump-persists state, the SELECTED-axe gate (it currently gates on `HasAxe` = OWNED, not SELECTED — see trap 4), or the settings registrations. `ChopSceneTests` guards scene presence; `ChopTreePlayModeTests` guards the chop→wood→fell mechanic.

**Ownership split (AC2a, pinned — the dead-knob seam):** THIS ticket OWNS the chop MECHANIC + the per-chop wood amount as a NAMED source (`woodPerChop` / a `WoodPerChop`/`DefaultChopYield` constant) — NOT a magic-scattered literal. The `tree-chop wood yield` SETTING that drives it is OWNED by sticks `86caa96rd` AC4. THIS ticket does NOT register the yield setting and its AC6 does NOT test setting-drives-yield (that's sticks AC6). Owner: Drew (trees + chop mechanic/feedback/regrow) + Devon (inventory hook + the two settings registrations). Reviewer: Devon.

### 2a. AC → verification-method map

| AC | Expectation | Verification layer | Interaction-only? (soak = real gate) |
|---|---|---|---|
| **AC1** chop w/ SELECTED axe; anim-speed tweakable | Chop fires on proximity + input ONLY when the axe is the SELECTED belt item (`IsAxeSelectedInBelt`), not merely owned; chop-anim/interval driven by the `tool-use speed` named field. | **PlayMode** (chop gated on SELECTED axe — assert a chop with axe owned-but-NOT-selected does nothing; chop with axe selected yields wood) + **soak** (held-axe + input feel). | **Yes — held-axe + input → SOAK is the real gate.** |
| **AC2** N chops → stump → wood to inventory (stacks) | `chopsToFell` chops deplete the tree to a STUMP; each chop adds `WoodPerChop` `wood` to the inventory; stacks per stack-size; deplete-once (a felled tree yields no more). | **PlayMode** (`ChopTreePlayModeTests` shape — chop → `Model.CountItem(WoodId)` rises by `WoodPerChop`; `chopsToFell` → `IsFelled`; further proximity yields nothing; full-pack leftover handled like `BerryBush.Harvest`). | Partly — testable headless via the public seam. |
| **AC3** regrow after tweakable random `[min,max]` timer | Stump regrows into a tree when a timer (RANDOM within serialized `treeRegrowMin/MaxSeconds`) elapses; mirrors `BerryBush.ScheduleRegrow`/`Regrow`. | **PlayMode** (felled → wait fast-window → tree state returns + choppable again; delay within `[min,max]` — mirror `BerryBushPlayModeTests.RegrowTime_FallsWithinTheConfiguredRange`). | Timer behaviour testable; perceived wait = soak. |
| **AC4** chop feedback (flinch/chips/fell puff/whump); stump persists | Per-chop flinch + wood-chip burst + THOCK; fell puff + whump; STUMP persists through the regrow window; grow-in pop on regrow (Uma §2). Cute/warm low-poly. | **Shipped capture** (player-framing gameplay-cam — flinch + chips + topple + stump visible in the BUILT exe, not just editor). | **Soak/visual — the legs-up rule applies.** |
| **AC5** seed-42 world integrity; nearest-in-range tree | Chop READS the existing seed-42 tree scatter (does NOT add to it); chop targets the NEAREST in-range tree; no seed-42/NavMesh perturbation. | **EditMode** (chop reads existing `LP_Tree` scatter; no new scatter draw on `rnd`; seed-42 shape fields unchanged — mirror `SeededScatterVariationTests`). **PlayMode** (NavMesh coverage non-regression — `RoundIslandNavCoveragePlayModeTests`, ≥90% + worst-ring ≤ azimuths/4). | No — testable. |
| **AC6** guard | Paired EditMode + PlayMode: chop → `wood` added (stacks); deplete-once (chop a felled tree yields nothing); tree → stump → tree (regrow). | Both suites green from `-testResults` XML `<test-run result="Passed">`. | — |

### 2b. Runtime-vs-test-layer traps to assert against (the silent killers)

1. **The DEAD-KNOB seam (AC2a) — the wave's headline silent killer.** Chop's per-chop yield must be a SINGLE named source the `tree-chop wood yield` setting (sticks AC4) can drive — no hardcoded literal the setting can't override. `ChopTree.cs` on `main` already reads `Mathf.Max(1, woodPerChop)` (a named serialized field — good), but verify at review that nothing re-introduces a scattered literal and that sticks' setting actually binds to THIS field. The "setting registered but chop ignoring it" failure green-passes a registration test and fails the player — this is the `pickup_count > 0 passed during the whole dual-spawn era` class. The setting-drives-chop assertion is owned by sticks AC6; chop's job is to leave the field reachable.
2. **SELECTED-axe vs OWNED-axe gate (AC1) — a regression risk in the generalization.** The shipped `ChopTree.Update` gates on `inventory.HasAxe` (= OWNED, `OwnsItem(AxeId)`). AC1 calls for chop gated on the axe being the SELECTED belt item — `inventory.IsAxeSelectedInBelt`. If the generalization leaves the `HasAxe` gate in place, chop fires while the axe is owned-but-holstered → breaks the fiction (you chop with no axe in hand). **Guard the NEGATIVE:** assert a chop with the axe OWNED but NOT selected does NOTHING (no wood, no chop). Confirm at review which gate the impl uses — flag if it's still `HasAxe`.
3. **Deplete-once.** A felled tree must yield no further wood. The shipped code sets `_felled` + early-returns, but the regrow ADD reintroduces a state transition — assert that AFTER fell and BEFORE regrow, standing at the stump with the axe selected yields nothing; and that after regrow it's choppable anew (a fresh `chopsToFell` cycle, wood count rises again). A regrow that forgets to reset `_chops`/`_felled` either never re-fells or double-counts.
4. **`base`-less regrow timer / `Time.time` window.** The regrow is a new timer on `ChopTree.Update`. Mirror `BerryBush`: schedule `_regrowAt = Time.time + Random[min,max]`, regrow when `Time.time >= _regrowAt`. The fell tween is `Time.unscaledDeltaTime`-paced (cosmetic, lands fast headless) — the regrow TIMER must be real-`Time.time` so a PlayMode wall-clock window proves it; an EditMode-only "math" proof bypasses `Update` (deltaTime≈0 headless false-green class). Assert the regrow within a real fast window (set min/max ≈0.2/0.3s like the bush test).
5. **Stump persistence is a VISUAL state, not just a flag.** AC4 says the stump persists through the regrow window. Assert the felled visual leaves a stump (the sink+tip end-state or a swapped stump mesh) that is STILL PRESENT when the regrow timer is mid-window — not destroyed on fell then re-created on regrow (a flicker/gap). The capture must show a stump, not bare ground, during the wait.
6. **Cosmetic feedback must NOT perturb the wood/fell assertions (Uma §5).** The flinch tween, chip burst, and fell puff are pure transform/instantiate work on serialized visuals, `Time.unscaledDeltaTime`-paced. Assert the AC6 wood-count / `IsFelled` / regrow asserts are UNCHANGED by the feedback (run the mechanic test headless — the chips must not allocate into or gate the chop count). No `new GameObject` per chop in the chop rhythm (pool — unity6-mastery §5).
7. **Seed-42 LOCK — chop is READ-only on the scatter, but the regrow must not re-roll.** Chop does NOT add to `ScatterIslandProps` (it reads existing `LP_Tree` instances). The regrow re-shows the SAME tree at the SAME plant point — assert the regrown tree is at the original position, not a fresh scatter draw (which would perturb the seed-42 stream). If the impl ever routes regrow through a re-scatter, that's a seed-42 violation → bounce.
8. **`AddWood` shim vs direct `AddItem` (AC2a migration note).** `Inventory.AddWood(n)` currently forwards to `Model.AddItem(woodDef, n)`; the inventory contract §7 notes chop will switch to `AddItem` directly under this ticket. Either path is fine — but assert the wood lands as the `WoodId` (`"wood"`) item that STACKS with sticks' wood (§5c), not a parallel entry. Grep the diff: no second wood def minted.

### 2c. Vocabulary contract (mergeability-blocking — REQUEST_CHANGES, not a NIT)

The wood item is the catalog's `ItemCatalog.WoodId` = the literal id **`"wood"`** (display **`"Wood"`**), NOT a new `"chopped wood"` id — the tickets' prose says "chopped wood" but the SINGLE source of truth is `ItemCatalog` on `main` (`WoodId = "wood"`). Chop must yield `catalog.ById(ItemCatalog.WoodId)` (via `AddWood`/`AddItem`), never mint a parallel wood def. At review grep the diff for any new `ItemDef`/`"chopped wood"`/`"chopwood"` id → divergence = **REQUEST_CHANGES (mergeability-blocking)** (the wood must stack with sticks' wood, §5c). The per-chop yield field name (`WoodPerChop`/`DefaultChopYield`/`woodPerChop`) is the source sticks' setting binds to — pin it at review so sticks' AC4 wiring targets the right symbol (parallel-shared-concept vocabulary discipline).

### 2d. Interaction-feel probes (per Uma #128 — the chop is the wave's only build-up/payoff beat)

Uma's spec makes chop "the earned one" — verify the FEEL family reads, not just that wood appears:
- **Per-chop flinch (A) + chips (B) + THOCK (D):** each landed chop, the tree visibly RECOILS (~3–5° tip away from the player, ≤0.18s, one recoil no wobble); a small burst of 4–6 faceted brown chips pops out + arcs down (~0.4s); one soft woody THOCK. The chips/THOCK/flinch fire TOGETHER on each paced chop (the rhythm IS the feedback cadence). Verify it's lively-but-lightly-damped per `sponsor-prefers-natural-lively-motion` — not a hard snap (reads dead), not a long wobble (reads broken).
- **The fell (climax):** the existing sink+tip topple + a BIGGER chip/leaf puff (8–10 bits, green+brown) at the canopy + a base dust puff + a soft felling WHUMP as the tip lands (paired with the visual landing, not the first frame). Reads as "the world handing you wood and tipping over," never violent.
- **Stump → regrow grow-in pop:** when the stump regrows, the tree returns with a light grow-in pop (scale 0→1, ~12% overshoot, ≤0.35s) — the island quietly heals. Regrow audio default OFF (Uma Q1, soak's call).
- **Toy-warm gate:** if any beat reads louder/harsher/"AAA juice," it's wrong even if clear (Uma §0). Brown chips route through `QuantizeFine` (no pink-cast).
- **Spot-check the audio cue exists + is soft** (Devon buses the THOCK + whump — one-shots; sub-1.0 "calm" loudness, never a startle). Audible cue spot-check per the QA-of-Uma-direction contract.

### 2e. Chop soak-probe list (Sponsor: test & confirm THIS)

Exe: `Build/Windows/FarHorizon.exe` — verify the HUD build stamp matches the PR head SHA before judging.
1. SELECT the axe on the belt, walk to a tree, chop — wood ticks into the inventory and STACKS; the tree depletes to a STUMP after N chops; each chop FEELS like a landed blow (the tree flinches, chips puff, a soft THOCK).
2. The fell reads as a generous topple (sink+tip + a leaf/chip puff + a soft whump) — earned, not violent.
3. With the axe OWNED but NOT selected (a different belt item active), chopping does NOTHING — no wood, the tree stays standing (the held-axe gate).
4. Wait the regrowth window — the stump regrows into a tree (a gentle grow-in pop) and is choppable again. The stump is visibly THERE the whole wait (not bare ground).
5. The rest of the world is UNCHANGED — same island shape, trees/rocks/bushes/sticks/stones in place, you can still walk everywhere (chop didn't shift the world).
6. (If settings panel present) the `tool-use speed` slider visibly changes the chop pace, and `tree regrowth time` changes the regrow delay — live, no restart.

---

## 3. STICKS & BRANCHES `86caa96rd` — FULL acceptance plan

**Feature (two lines):**
(1) SMALL sticks/branches scattered in VARIOUS SIZES across the seed-42 island (a new scatter pass in `LowPolyZoneGen.ScatterIslandProps`, grounded via `GroundPoint`), cute/warm low-poly; the player PICKS one up by hand (proximity + interact, NO tool — the BerryBush idiom) → adds **exactly 1** `wood` to the inventory; the stick is CONSUMED (removed from world). The LOW-yield wood path (1/pickup) vs chopping's high yield.
(2) REGISTERS the `tree-chop wood yield` setting into the panel + WIRES chop `86caa4c5c`'s yield field to read it live (the second half of the Sponsor's prompt — the dead-knob seam THIS ticket owns).

**Ground-truth on `main`:** the scatter precedent to MIRROR is the bush pass (`ScatterIslandProps`, lines ~633–662): it uses its OWN sub-stream `new System.Random(seed + 777)` so it draws SEPARATELY and does NOT perturb the island shape, the existing tree/rock/grass placement (which consumed the main `rnd` = `seed+555` in a fixed order), or the NavMesh. Sticks MUST add an analogous parallel sub-stream (e.g. `seed + 888`) — appending draws to the main `rnd` re-rolls everything downstream (the seed-42 violation, trap 1). Grounding via `GroundPoint(groundCol, x, z)` (scale-immune raycast). The pickup idiom is `BerryBush` (proximity + edge-trigger, no tool, `Model.AddItem`); the wood item is `ItemCatalog.WoodId` (`"wood"`).

**Ownership split (AC4a, pinned — mirror of chop AC2a):** THIS ticket OWNS the `tree-chop wood yield` SETTING (registration + wiring chop's named field to consume it live) AND the cross-lane integration test (setting → real chop yields new amount). The chop ticket OWNS the named yield field; its AC6 does NOT duplicate the integration test. The stick's 1-wood (AC3) is a NAMED constant, NOT routed through this setting in v1. Owner: Drew (scatter + pickup) + Devon (inventory hook + settings wiring). Reviewer: Devon.

### 3a. AC → verification-method map

| AC | Expectation | Verification layer | Interaction-only? (soak = real gate) |
|---|---|---|---|
| **AC1** sticks scattered, various sizes, grounded on seed-42 | New scatter pass; procedural size variation / a few prefab variants (twig → branch); rest ON the terrain (scale-immune grounding); does NOT break seed-42 / NavMesh / existing scatter. | **EditMode** (sticks present in seed-42 gen at expected count + grounded; existing tree/rock/bush placement BYTE-INVARIANT — mirror `SeededScatterVariationTests` lock). **PlayMode** (NavMesh coverage non-regression). **Shipped capture** (grounded, not floating/sunk; player-framing cam). | Grounding = **soak/visual gate.** |
| **AC2** pick up (no tool) → exactly 1 `wood`; stick consumed | Proximity + interact, NO tool (BerryBush idiom); pickup adds EXACTLY 1 `wood`; the stick is removed from the world. | **PlayMode** (mirror `BerryBushPlayModeTests` — walk-up → 1 `wood`; far → nothing; stick destroyed; STACKS on a second stick; full-pack leftover handled). | **Yes — soak gate** (walk-up pickup). |
| **AC3** yield CONTRAST: 1 wood/stick, named constant | A stick yields EXACTLY 1 wood — far less than a tree chop; the 1 is a named constant (promotable later). | **EditMode** (the constant == 1; structurally < chop's `WoodPerChop` default). | No. |
| **AC4** register `tree-chop wood yield` setting; wire chop to READ it live | Setting registered into the panel registry; chop's yield field BINDS to it; changing the setting changes a REAL chop's output live, no restart. Default = chop's named-constant value. | **EditMode/PlayMode** (the cross-lane integration test — change setting → a real `ChopTree.Chop()` yields the new amount, live). **HARD-DEP on `86caa4bqp`** (AC4b). | **The dead-knob guard — see trap 1.** |
| **AC5** sticks finite (no per-spot respawn v1) | Sticks do NOT respawn per-spot in v1 (finite early-game gather) unless trivially free with the existing respawn pattern. | **Note the OOS;** surface the respawn question at soak if the island reads thin (Uma §3: no grow-in pop for finite sticks). | Soak observation. |
| **AC6** guard | Paired EditMode + PlayMode: sticks grounded + seed-42 intact; pick → 1 `wood` (stacks); the registered setting drives chop yield (change → chop yields new amount). Mirror the stone-pickup test shape. | Both suites green from `-testResults` XML `<test-run result="Passed">`. | — |

### 3b. Runtime-vs-test-layer traps to assert against (the silent killers)

1. **THE DEAD-KNOB SEAM (AC4a) — this ticket's headline silent killer.** AC4 owns BOTH the `tree-chop wood yield` registration AND the integration test. The test that catches the dead knob is: **change the setting → a REAL `ChopTree.Chop()` yields the new amount, live (no restart)** — NOT just "the setting is registered" (a registration-only test green-passes a setting that nothing reads). Verify chop ACTUALLY reads this setting's value into its yield field and NO hardcoded yield literal remains in `ChopTree` (the `pickup_count > 0 passed the whole dual-spawn era` class — assert the EFFECT on chop's output, not the registration). At review, grep `ChopTree` for the yield read-site + confirm it resolves to the registered setting, not a stale `woodPerChop` literal the setting can't reach.
2. **SEED-42 SUB-STREAM (the other headline killer).** The stick scatter MUST use a parallel sub-stream (`new System.Random(seed + 888)` or similar), like the bush pass's `seed + 777` — NOT extra draws on the main `rnd`. **Guard:** assert the existing tree/rock/grass/bush placement is BYTE-INVARIANT pre/post the stick add (count + positions of `LP_Tree`/rocks/bushes unchanged) AND the seed-42 island-shape fields (`ShoreRadiusAt`/`HeightAtRadial`/`CliffinessAt`/`IslandSeed==42`/`IslandShoreR==120`) unchanged (mirror `SeededScatterVariationTests.Seed42_IslandShapeFields_AreDeterministic`). A scatter that appends to `rnd` shifts every downstream prop — green per-feature, broken world. If sticks can't be added without perturbing the stream → SPONSOR-GATE escalation (seed lock outranks the add).
3. **EXACTLY 1 wood, and the FULL-PACK case.** Assert the PRECISE amount (1, not ≥1) per stick, and — mirroring `BerryBushPlayModeTests.Harvest_IntoFullInventory_AddsNothing_NoOverCredit` — that picking a stick into a FULL pack adds 0 (no over-credit, no negative store) and the stick handling is sane (consumed-but-wasted, or NOT consumed if the impl chooses to leave it — pin which at review; the bush CONSUMES even on a wasted harvest, so the parity choice is "stick consumed even if pack full" unless the ticket says otherwise).
4. **Stick CONSUMED, not toggled.** Unlike the persistent bush (berries toggle, bush stays), the stick is REMOVED from the world on pickup (AC2). Assert the stick GameObject is destroyed/deactivated after pickup and a second interact at the same spot does nothing (no double-yield, no ghost stick). The edge-trigger must not re-fire on a consumed stick.
5. **Grounding is scale-immune (AC1).** Sticks rest ON the terrain via `GroundPoint` (the raycast), not at y=0, across the varied scatter scales — assert grounded (root within ~1.5u of sampled ground, mirror `SeededScatterVariationTests.Trees_StillSeatedOnGround`). A floating/sunk stick is the grounding failure class — verify by the player-framing gameplay-cam capture, NOT top-down/metrics (`verify-grounding-soaks-by-gameplay-cam-visual`).
6. **Shared interaction input (§5d).** Stick pickup reuses the SAME proximity+interact as drink/stone/berry — do NOT invent a new input. Assert the input resolves to the NEAREST in-range interactable; when a stick + a stone (+ a bush) are all in range, one interact must not double-trigger across them.
7. **AC4b HARD-DEP** on settings panel `86caa4bqp` for the AC4 registration ONLY. The scatter + pickup (AC1–AC3, AC5) do not depend on the panel. If the panel hasn't merged, the registration ships behind a named extension hook + follow-up — but the chop-yield FIELD wiring (chop reading a single source) must still be in place so there's no dead knob when the registration lands.

### 3c. Vocabulary contract (mergeability-blocking — REQUEST_CHANGES, not a NIT)

Sticks reuse chop's wood item — the catalog's `ItemCatalog.WoodId` = **`"wood"`** (display `"Wood"`), NOT a new `"chopped wood"`/`"stick wood"` id (the ticket prose says "chopped wood" but `ItemCatalog` on `main` is the single source of truth). Pickup must do `Model.AddItem(catalog.ById(ItemCatalog.WoodId), 1)` — the SAME item chop yields, so they STACK in one slot (§5c). Grep the diff for any new wood `ItemDef` → divergence = **REQUEST_CHANGES (mergeability-blocking)**. The `tree-chop wood yield` setting id + the chop-yield field name must match what chop `86caa4c5c` exposes (parallel-shared-concept vocabulary discipline) — pin both at review so the wiring binds correctly.

### 3d. Interaction-feel probes (per Uma #128 — sticks are "the soft free gift")

- **Squash-bob (A) + pickup pop (C) + rustle (D):** on pickup the stick does a tiny squash + lift (~12%, ≤0.3s), then scales up ~15% and vanishes (the "into my arms" read), with one short DRY leafy RUSTLE — the QUIETEST cue in the wave. Verify it reads as a soft free gift, almost too easy (the early thin-wood path).
- **No chip burst (the distinguisher):** sticks omit the chip burst (B) deliberately — that ABSENCE is what makes the stick read LIGHTER than the chop and SOFTER than the stone. Verify no chip puff on a stick pickup.
- **Variety feeds feel:** the squash-bob amplitude scales with the stick's visual size (a big branch bobs a touch more than a twig — the `localScale.x` scaling the bush uses). Spot-check across sizes.
- **No grow-in pop:** sticks are finite (no respawn v1) so there's no return-pop. Confirm none.
- **Audible cue spot-check:** the rustle exists, is soft, sub-1.0, never a startle (Devon buses it). Toy-warm gate (Uma §0).

### 3e. Sticks soak-probe list (Sponsor: test & confirm THIS)

Exe: `Build/Windows/FarHorizon.exe` — verify the HUD build stamp matches the PR head SHA before judging.
1. Sticks/branches are scattered around the island in VARIOUS SIZES (twigs through fallen branches), resting ON the ground (not floating/sunk).
2. Walk up to a stick, pick it up (the interact input, no tool) → exactly 1 wood in the inventory; the stick disappears with a soft squash + rustle (the "here, take this" gift).
3. Picking a stick gives FAR less wood than chopping a tree (1 vs the chop yield).
4. Picking a stick AND chopping a tree both add to the SAME wood pile in the inventory (one stack, not two "wood" entries).
5. (If settings panel present) changing `tree-chop wood yield` visibly changes how much wood a TREE CHOP gives — live, no restart. The stick stays 1.
6. The rest of the world is UNCHANGED — same island shape, trees/rocks/bushes/stones in place, you can walk everywhere (sticks didn't shift the world).

---

## 4. STONES `86caa4c96` — FULL acceptance plan

**Feature (two lines):**
(1) SMALL stones (pebbles/small rocks) scattered in the seed-42 world (reuse/extend the existing rock-scatter pass or a small-stone variant), cute/warm low-poly; the player PICKS one up by hand (proximity + interact, NO tool — the BerryBush idiom) → adds the `stone` stackable to the inventory. Only SMALL stones are pickable (bigger rocks = FUTURE pickaxe-mining, OOS).
(2) Picked spots RESPAWN a stone after a RANDOM delay in a serialized `[min,max]` window (mirror `BerryBush.ScheduleRegrow`); the respawn time is exposed as named serialized fields then registered into the settings panel.

**Ground-truth on `main`:** the existing rock pass is `ScatterIslandProps` lines ~596–616 (`rockTarget 60`, `BuildRock(parent, GroundPoint(...), scale, rnd)`), consuming the main `rnd` (`seed+555`) stream. The small-stone scatter must follow the BUSH precedent — a parallel sub-stream (`new System.Random(seed + 999)` or similar) so it does NOT perturb the existing rock/tree/grass/bush placement (trap 1). Grounding via `GroundPoint` (scale-immune). Pickup idiom = `BerryBush` (proximity + edge-trigger, no tool, `Model.AddItem`). The stone item is `ItemCatalog.StoneId` = the literal id **`"stone"`** (display `"Stone"`).

**Ownership split:** Owner: Drew (small-stone scatter + pickup + respawn) + Devon (inventory hook + the respawn registration). Reviewer: Devon. No cross-lane yield seam here (stones don't share the chop-yield setting) — the seams are the shared scatter zone (§5e) + shared interaction input (§5d) + shared item-model.

### 4a. AC → verification-method map

| AC | Expectation | Verification layer | Interaction-only? (soak = real gate) |
|---|---|---|---|
| **AC1** small stones scattered; only small pickable | Small-stone scatter across the island; bigger existing rocks are NOT pickable (no pickup component on them). | **EditMode** (small stones present at expected count; existing rock/tree/bush placement byte-invariant — `SeededScatterVariationTests` lock). **PlayMode** (NavMesh non-regression). **Shipped capture** (small pebbles read distinct from the big rocks). | Visual + the small-vs-big distinction = **soak.** |
| **AC2** pick up (no tool) → `stone` stacks | Proximity + interact, no tool; pickup adds the `stone` stackable to inventory; stacks per stack-size. | **PlayMode** (mirror `BerryBushPlayModeTests` — walk-up → `Model.CountItem(StoneId)` rises by 1; far → nothing; STACKS; full-pack leftover handled). | **Yes — soak gate.** |
| **AC3** respawn after tweakable random `[min,max]` timer | A PICKED spot respawns a stone when its timer (RANDOM within serialized `respawnMin/MaxSeconds`) elapses; mirrors `BerryBush.ScheduleRegrow`/`Regrow`. | **PlayMode** (pick → spot empty → wait fast-window → a stone respawns AT THE SAME spot; delay within `[min,max]`). **HARD-DEP on `86caa4bqp`** (AC3a) for the registration. | Timer testable; perceived wait = soak. |
| **AC4** grounding (scale-immune); seed-42/NavMesh intact | Stones rest ON the terrain (scale-immune `GroundPoint`); the scatter is a DETERMINISTIC ADD on the locked seed-42 terrain; no NavMesh shrink. | **EditMode** (grounded — root near sampled ground; seed-42 shape fields + existing scatter unchanged). **PlayMode** (NavMesh coverage non-regression — `RoundIslandNavCoveragePlayModeTests` ≥90% + worst-ring ≤ azimuths/4). | Grounding = **soak/visual gate.** |
| **AC5** guard | Paired EditMode + PlayMode: pick → `stone` added (stacks); a picked spot respawns after the (tweakable) timer. | Both suites green from `-testResults` XML `<test-run result="Passed">`. | — |

### 4b. Runtime-vs-test-layer traps to assert against (the silent killers)

1. **SEED-42 SUB-STREAM (AC4a) — the headline killer (shared with sticks/pond, §5e).** The small-stone scatter MUST use a parallel sub-stream (`new System.Random(seed + 999)` or similar) like the bush's `seed + 777` — NOT extra draws on the main `rnd` (which the existing rock pass consumes). **Guard:** existing rock/tree/grass/bush placement BYTE-INVARIANT pre/post the stone add AND the seed-42 shape fields unchanged (`SeededScatterVariationTests.Seed42_IslandShapeFields_AreDeterministic`). Reusing/EXTENDING the rock pass is the riskiest path — if the impl ADDS draws inside the existing `rnd`-driven rock loop, it shifts every downstream prop. Verify it's an additive sub-stream pass, not an inline extension of the `rnd` rock loop. Re-roll needed → SPONSOR-GATE escalation (seed lock outranks the add).
2. **RESPAWN-SPOT IDENTITY (the stone-specific killer).** A respawn must re-fill the SAME picked spot, NOT spawn a stone elsewhere or bump a global count. **Guard:** assert the respawned stone is at the ORIGINAL pick position (within epsilon), not just "a stone exists again." A respawn that re-scatters globally green-passes a count check and breaks the fiction (and risks the seed-42 stream). This is the stone analogue of "assert the spot, not the count."
3. **Real-`Time.time` respawn window.** The respawn timer is real-`Time.time` (mirror `BerryBush`: `_respawnAt = Time.time + Random[min,max]`, respawn when `Time.time >= _respawnAt`). A PlayMode wall-clock test (min/max ≈0.2/0.3s) proves it; an EditMode-only math proof bypasses `Update` (deltaTime≈0 headless false-green class). Assert the delay within `[min,max]` (mirror `RegrowTime_FallsWithinTheConfiguredRange`).
4. **Only SMALL stones pickable (AC1).** Assert the big existing rocks have NO pickup component / are NOT consumable — interacting near a big rock does NOTHING (the future pickaxe-mining surface, OOS). A pickup that grabs any rock breaks the design intent. Guard the NEGATIVE.
5. **Grounding scale-immune + the floating class.** Stones rest ON the terrain via `GroundPoint` across varied scatter scales — assert grounded (root within ~1.5u of sampled ground). Verify by the player-framing gameplay-cam capture, NOT top-down/metrics (`verify-grounding-soaks-by-gameplay-cam-visual` — top-down + metrics have both served a floating build).
6. **Stone CONSUMED + STACKS + full-pack.** The stone is removed on pickup; a second stone STACKS in the same slot (one `stone` stack, not parallel entries); a pickup into a FULL pack adds 0 with no over-credit/negative store (mirror `BerryBushPlayModeTests.Harvest_IntoFullInventory_AddsNothing_NoOverCredit`). Assert all three.
7. **Shared interaction input (§5d).** Stone pickup reuses the SAME proximity+interact as drink/stick/berry — no new input; resolves to the nearest in-range interactable. When a stone + a stick (+ a bush) are co-located, one interact must not double-trigger.
8. **AC3a HARD-DEP** on settings panel `86caa4bqp` for the `stone respawn time` registration ONLY. The scatter + pickup + respawn BEHAVIOUR ship as named serialized fields. If the panel hasn't merged, the registration ships behind a named extension hook + follow-up — NO dead knob (a serialized respawn field the eventual setting can't reach).

### 4c. Vocabulary contract (mergeability-blocking — REQUEST_CHANGES, not a NIT)

The stone item is the catalog's `ItemCatalog.StoneId` = the literal id **`"stone"`** (display **`"Stone"`**), NOT a new `"picked up stones"`/`"pebble"` id (the ticket prose says "picked up stones" but `ItemCatalog` on `main` is the single source of truth — `StoneId = "stone"`). Pickup must do `Model.AddItem(catalog.ById(ItemCatalog.StoneId), 1)`, never mint a parallel stone def or a second registry (AC2a). Grep the inventory branch/main + the diff for any divergent stone id → **REQUEST_CHANGES (mergeability-blocking, not a NIT)**. The stone def already EXISTS in `ItemCatalog.BuildDefaults` (`stone.Init(StoneId, "Stone", ...)`) — so this is a strict CONSUME of the existing id, no catalog change needed.

### 4d. Interaction-feel probes (per Uma #128 — stones are "the hard cool gift")

- **Hop-and-settle (A) + pickup pop (C) + *tok* (D):** on pickup, a small HOP-and-settle (a pebble pops up a few cm + settles, ~10%, ≤0.28s — harder/snappier than the stick's soft squash), then a crisp scale-up-and-vanish (~0.2s), with one short DRY HARD *tok/click* — cooler and harder than the stick's rustle. The *tok* is the single clearest stick-vs-stone distinguisher; a blindfolded player should know which they did.
- **Optional tiny grit puff (B, minimal):** an OPTIONAL 2–3-bit faint GREY grit puff at the lift point (≤0.3s, much smaller than the chop's chips) — grey near-neutral, routed through `QuantizeFine` (no pink-cast). Keep tiny or omit (Uma Q2, soak's call). Verify it doesn't clutter.
- **Respawn grow-in pop (AC3):** when a stone respawns, it returns at the spot with a gentle grow-in pop (scale 0→1, ~12% overshoot, ≤0.35s) — the island refills its gifts. Respawn audio default OFF (Uma Q2 parity with tree regrow).
- **Material voice:** stone reads a shade COOLER/HARDER than the stick without thinking about it (same pickup grammar, different material voice — Uma's "one family, instantly distinguishable").
- **Audible cue spot-check:** the *tok* exists, is a light pleasant click (NOT a heavy boulder thud — these are pebbles), sub-1.0 (Devon buses it). Toy-warm gate.

### 4e. Stones soak-probe list (Sponsor: test & confirm THIS)

Exe: `Build/Windows/FarHorizon.exe` — verify the HUD build stamp matches the PR head SHA before judging.
1. Small stones are scattered around the island, resting ON the ground (not floating/sunk).
2. Walk up to a small stone, pick it up (the interact input, no tool) → a stone goes into the inventory and STACKS; the pickup reads as a hard cool gift (a small hop + a dry *tok*, distinct from the stick's soft rustle).
3. Bigger rocks are NOT pickable (interacting near them does nothing — future pickaxe-mining).
4. Wait the respawn window — a stone reappears AT THE SAME spot you picked it from (a gentle grow-in pop), not somewhere random.
5. The rest of the world is UNCHANGED — same island shape, trees/rocks/bushes/sticks in place, you can walk everywhere (stones didn't shift the world).
6. (If settings panel present) `stone respawn time` changes the respawn delay — live, no restart.

---

## 5. CROSS-NEED & CROSS-LANE INTEGRATION MATRIX (this doc OWNS it — no single ticket does)

This is the coverage no per-ticket plan owns — the seams BETWEEN tickets where a green-per-ticket suite still ships a broken wave. The "AC4 green ≠ mobs engage" / "pickup_count > 0 passed the whole dual-spawn era" bug class lives HERE.

### 5a. Three-need HUD coexistence (thirst + hunger + warmth) — feeds `86caamkxv`
- All three needs (`WarmthNeed`, `HungerNeed`, `ThirstNeed`) expose the SAME `SurvivalNeed` surface; the HUD `86caamkxv` binds all three with ONE code path (subscribe to `Changed`, read `Current01`). **Integration assert (owned by the HUD ticket but flagged here as the wave seam):** three bars render INDEPENDENTLY — satisfying thirst moves ONLY the thirst bar, not hunger/warmth; each `Changed` event updates only its own bar. A shared-event or shared-index bug clobbers sibling bars. The HUD ticket is HARD-BLOCKED-BY hunger + thirst (both expose the read surface) — verify the surface is identical across all three at HUD review (grep all three for `Current01`/`IsCritical`/`Changed`).
- **Critical-state independence:** thirst going `IsCritical` must not flip hunger/warmth's critical flag. Assert per-need.

### 5b. Chop ↔ sticks chop-yield setting seam (the dead-knob seam)
- Sticks `86caa96rd` AC4 OWNS the `tree-chop wood yield` setting + the integration test; chop `86caa4c5c` AC2 OWNS the named yield constant the setting drives. **The one integration test that catches the dead knob:** change the setting → a REAL tree chop yields the new amount, live. Owned by sticks AC6; NOT duplicated in chop. At chop review verify chop reads the named source (no hardcoded literal); at sticks review verify the setting actually drives chop's output.

### 5c. Shared inventory wood item (chop + sticks → same `"wood"` item)
- Chop yields `wood` (`ItemCatalog.WoodId = "wood"`, display `"Wood"`); sticks yield 1 `wood` — the SAME item id, NOT the prose `"chopped wood"`. **Assert:** picking sticks AND chopping both stack into the SAME inventory slot (one item id `"wood"`, one stack), not two parallel wood entries. Grep both branches for the item id; any `"chopped wood"`/`"stick wood"` divergence = REQUEST_CHANGES (mergeability-blocking).

### 5d. Shared interaction input (drink / pick-stick / pick-stone / harvest-berry)
- Drink-from-hand, stick-pickup, stone-pickup, and berry-harvest all reuse the SAME proximity+interact pattern (do NOT invent new inputs). **Assert:** the interact input resolves to the NEAREST in-range interactable deterministically (no ambiguous double-trigger when a pond + a stick + a stone are all in range at once). A wave seam: four interactables sharing one input must not fight.

### 5e. Shared world-gen scatter integrity (pond + sticks + stones all add to seed-42)
- Every world-add (pond, sticks, small stones) is a deterministic ADD on the LOCKED seed-42 terrain. **Wave-level risk:** each lands separately, but TOGETHER they must STILL leave seed-42 byte-stable + NavMesh coverage ≥90%. When the LAST of the three lands, re-run the seed-42 + coverage guards against ALL three present — a pairwise-clean set can still collectively shift the world or carve coverage. Flag this as the wave-close regression sweep.

---

## 6. Wave-close regression sweep + milestone journey probe

Before any wave RC is handed to the Sponsor for soak (RC-boundary mandatory probe):
1. **One complete player journey** on the shipped exe: boot → walk island → drink at pond (thirst rises) → harvest berries (hunger) → warm at campfire (warmth) → pick sticks + stones → chop a tree → confirm inventory stacks → save/quit/reload/resume (if save is in scope) → resume. Log in `team/tess-qa/journey-probe-<date>.md`.
2. **Any console `USER WARNING:` / `USER ERROR:` is a blocker.** Any item-id resolution failure is a blocker. Any missing/uncollectable loot (a stick/stone/wood that can't be picked, or doesn't stack) is a blocker.
3. **Re-run the seed-42 + NavMesh-coverage guards with all three world-adds present** (§5e) — the collective-shift catch.
4. **HUD three-bar coexistence** verified on the shipped exe (§5a) once `86caamkxv` lands — satisfy each need in turn, confirm only its own bar moves.

---

## 7. AC gaps / ambiguities (status)

The G1–G4 gaps from the original wave-prep pass (PR #86) are RESOLVED by Priya's AC clarifications now in the tickets:
- **G1 (chop-yield setting ownership)** → resolved by chop AC2a + sticks AC4a (sticks owns the setting + the integration test; chop owns the named constant).
- **G2 (pond vs seed-42 lock)** → resolved by thirst AC2a (deterministic ADD; re-roll = sponsor-gate escalation).
- **G3 (thirst two-owner integration)** → resolved by thirst AC3a (drink-seam test owned by the thirst ticket).
- **G4 / AC5a / AC4b / AC3a (settings-panel host)** → resolved across all four (registrations gated on `86caa4bqp`; mechanics proceed behind named extension hooks; NO dead knob).

**No NEW blocking gaps found** in this full-depth pass. One ground-truth note promoted to a review-time guard (not a gap — the tickets are correct, the prose just diverges from the code): the ticket prose `chopped wood` / `picked up stones` are DISPLAY phrasings; the canonical `ItemCatalog` ids are `"wood"` / `"stone"` (and the `stone` def already ships in `BuildDefaults`). Each section's vocabulary contract pins the real id so a reviewer catches a PR that mints the prose string as a new id. Two impl-choice items to confirm AT REVIEW (not blockers): chop's gate must move from `HasAxe` (owned) to `IsAxeSelectedInBelt` (selected) per AC1 (trap §2b.2); the scatter sub-stream offsets (sticks/stones) must be NEW parallel streams like the bush's `seed+777`, not appends to the main `rnd`.

Open soak-time questions to surface (non-blocking, defaults shipped): stick respawn (sticks AC5 — finite vs respawn, Sponsor's call at soak); chop-yield as int vs int-range (sticks AC4); thirst decay default rate relative to hunger (thirst AC1); Uma's Q1 regrow/respawn audio (default OFF), Q2 stone grit puff (default tiny/omit), Q3 fell chip-burst size, Q4 stick squash-bob amplitude — all one-line feel tweaks, none gate impl.
