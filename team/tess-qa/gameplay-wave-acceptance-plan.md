# Gameplay-Wave Acceptance & Test-Coverage Plan

**Scope:** the QA acceptance + test-coverage plan the wave's QA pass runs against. Lead ticket (FULL depth): THIRST `86caamkv7`. Skeleton depth (filled as each dispatches): chop `86caa4c5c`, sticks/branches `86caa96rd`, stones `86caa4c96`.
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

## 2. CHOP TREES `86caa4c5c` — skeleton (fill on dispatch)

**Feature:** chop a tree with the equipped+SELECTED axe (proximity + input) → N chops deplete it to a stump → yields `chopped wood` into inventory (stacks); stump regrows after a tweakable (random min/max) timer; chop-anim speed tweakable.
**Owner:** Drew (trees + chop) + Devon (inventory hook). Reviewer: Devon.

### AC → verification map (skeleton)
| AC | Layer | Interaction-only? |
|---|---|---|
| AC1 chop w/ selected axe + anim-speed tweakable | PlayMode (chop gated on axe = selected belt item) + soak | **Yes — soak gate** (held-axe + input) |
| AC2 N chops → stump → `chopped wood` to inventory (stacks) | PlayMode (deplete-once; `Inventory.AddItem(wood)`; stacks) | Partly |
| AC3 regrow after tweakable random min/max timer | PlayMode (stump→tree on timer; range read from named field) | Timer behaviour testable; feel = soak |
| AC4 chop feedback (shake/leaf/fall); stump persists | Shipped capture (low-poly feedback) | **Soak/visual** |
| AC5 seed-42 world integrity; nearest-in-range tree | EditMode/PlayMode (seed-42 + NavMesh non-regression) | No |
| AC6 guard: chop → wood stacks; deplete-once; tree→stump→tree | Both suites green | — |

### Traps to assert (skeleton)
- **OWNERSHIP (AC2a):** chop OWNS the per-chop wood amount as a NAMED constant (`WoodPerChop`/`DefaultChopYield`) — NO magic literal. The `tree-chop wood yield` SETTING that drives it is OWNED by sticks `86caa96rd` AC4 — chop does NOT register it; chop's AC6 tests ONLY the chop mechanic, NOT the setting-drives-yield integration (that's sticks' AC6). **Verify NO dead knob:** no hardcoded yield literal the setting can't override.
- **AC5a HARD-DEP:** `tool-use speed` + `tree regrowth time` registrations are gated on settings panel `86caa4bqp`; the mechanic/regrowth/feedback are NOT — ship the values as named serialized fields; if the panel's unmerged, registrations behind a named extension hook + follow-up. NO dead knob.
- **Seed-42 LOCK** + NavMesh non-regression (same guard as thirst AC2).
- **Selected-axe gate:** chop must require the axe be the SELECTED belt item — assert a chop with the axe NOT selected does nothing.

### Chop soak probes (skeleton)
1. Equip + SELECT the axe; chop a tree — wood goes to inventory and stacks; tree depletes to a stump after N chops. 2. Without the axe selected, chopping does nothing. 3. Wait the regrowth window — the stump regrows. 4. World unchanged (seed-42 scatter/NavMesh intact). 5. (Settings) `tool-use speed` + `tree regrowth time` change behaviour live.

---

## 3. STICKS & BRANCHES `86caa96rd` — skeleton (fill on dispatch)

**Feature:** small sticks/branches scattered in various sizes across the seed-42 island; pick up (proximity + interact, no tool) → **exactly 1** `chopped wood` to inventory (stacks); the LOW-yield wood source vs chopping. Also REGISTERS the `tree-chop wood yield` setting that drives chop's yield.
**Owner:** Drew (scatter + pickup) + Devon (inventory hook + settings wiring). Reviewer: Devon.

### AC → verification map (skeleton)
| AC | Layer | Interaction-only? |
|---|---|---|
| AC1 sticks scattered, various sizes, grounded on seed-42 | EditMode/PlayMode (scatter present + grounded + seed-42 non-regression) + shipped capture | Grounding = **soak/visual** |
| AC2 pick up (no tool) → exactly 1 `chopped wood`; stick consumed | PlayMode (mirror `BerryBushPlayModeTests` / stone-pickup shape) | **Yes — soak gate** |
| AC3 yield CONTRAST: 1 wood/stick, named constant | EditMode (1-wood constant; far less than chop) | No |
| AC4 register `tree-chop wood yield` setting; wire chop to READ it live | EditMode/PlayMode (change setting → tree chop yields new amount, live, no restart) | Live-binding test |
| AC5 sticks finite (no per-spot respawn v1 unless trivially free) | (note the OOS; surface respawn question at soak if it reads thin) | Soak observation |
| AC6 guard: sticks grounded + seed-42 intact; pick → 1 wood stacks; setting drives chop yield | Both suites green | — |

### Traps to assert (skeleton)
- **AC4a OWNS the chop-yield SETTING + the setting-drives-yield integration test** — the cross-lane seam. **DEAD-KNOB GUARD (the silent killer):** the test must be `change the tree-chop wood yield setting → a real tree chop yields the new amount (live)`. Verify chop ACTUALLY reads this setting and no hardcoded yield literal remains. A registered-but-ignored setting green-passes a registration test and fails the player. This is the `pickup_count > 0 passed during the whole dual-spawn era` bug class — assert the EFFECT, not just the registration.
- **Item vocabulary:** sticks reuse chop's `chopped wood` item — do NOT define a second wood type. Grep the inventory branch/main; a divergent id = REQUEST_CHANGES.
- **EXACTLY 1 wood** per stick (not ≥1) — assert the precise amount, and that the stick is consumed (removed from world).
- **AC4b HARD-DEP** on settings panel `86caa4bqp` for the registration only.
- **Seed-42 LOCK** + grounding (`modelSoleGround` scale-immune) + NavMesh non-regression.

### Sticks soak probes (skeleton)
1. Sticks/branches scattered around the island in various sizes, resting ON the ground (not floating/sunk). 2. Walk up, pick one up → exactly 1 wood in inventory; the stick disappears. 3. Picking a stick gives FAR less wood than chopping a tree. 4. (Settings) changing `tree-chop wood yield` visibly changes how much wood a TREE CHOP gives, live. 5. World unchanged (seed-42 scatter/NavMesh intact).

---

## 4. STONES `86caa4c96` — skeleton (fill on dispatch)

**Feature:** small stones scattered in the world; pick up (proximity + interact, no tool) → `picked up stones` stackable to inventory; stones respawn after a tweakable random min/max timer. Only SMALL stones pickable (bigger = future pickaxe-mining, OOS).
**Owner:** Drew (scatter + pickup) + Devon (inventory hook). Reviewer: Devon.

### AC → verification map (skeleton)
| AC | Layer | Interaction-only? |
|---|---|---|
| AC1 small stones scattered, only small pickable | EditMode/PlayMode + shipped capture | Visual |
| AC2 pick up (no tool) → `picked up stones` stacks | PlayMode (mirror stone/berry pickup shape; `AddItem(stoneId,1)`) | **Yes — soak gate** |
| AC3 respawn after tweakable random min/max timer | PlayMode (picked spot respawns on timer; range from named field) | Timer testable; feel = soak |
| AC4 grounding (scale-immune); seed-42/NavMesh intact | EditMode/PlayMode (grounded; seed-42 + coverage non-regression) | Grounding = **soak/visual** |
| AC5 guard: pick → `picked up stones` stacks; respawns after timer | Both suites green | — |

### Traps to assert (skeleton)
- **AC2a item vocabulary:** `picked up stones` is the id `86caa4bya`'s ItemCatalog declares — follow it verbatim; do NOT invent a parallel `stone` id or a second registry. Grep the inventory branch; a divergent id = REQUEST_CHANGES.
- **AC3a HARD-DEP** on settings panel `86caa4bqp` for the `stone respawn time` registration only; the respawn BEHAVIOUR ships as named serialized fields. NO dead knob.
- **AC4a seed-42 LOCK** + NavMesh coverage non-regression (deterministic ADD on the rock-scatter pass + `GroundPoint`).
- **Respawn-spot identity:** a respawn must re-fill the SAME picked spot, not spawn elsewhere — assert the spot, not just a global count.

### Stones soak probes (skeleton)
1. Small stones scattered, resting ON the ground. 2. Pick one up → `picked up stones` in inventory (stacks). 3. Bigger rocks are NOT pickable (future mining). 4. Wait the respawn window — a stone reappears at the picked spot. 5. World unchanged (seed-42 scatter/NavMesh intact). 6. (Settings) `stone respawn time` changes the respawn delay live.

---

## 5. CROSS-NEED & CROSS-LANE INTEGRATION MATRIX (this doc OWNS it — no single ticket does)

This is the coverage no per-ticket plan owns — the seams BETWEEN tickets where a green-per-ticket suite still ships a broken wave. The "AC4 green ≠ mobs engage" / "pickup_count > 0 passed the whole dual-spawn era" bug class lives HERE.

### 5a. Three-need HUD coexistence (thirst + hunger + warmth) — feeds `86caamkxv`
- All three needs (`WarmthNeed`, `HungerNeed`, `ThirstNeed`) expose the SAME `SurvivalNeed` surface; the HUD `86caamkxv` binds all three with ONE code path (subscribe to `Changed`, read `Current01`). **Integration assert (owned by the HUD ticket but flagged here as the wave seam):** three bars render INDEPENDENTLY — satisfying thirst moves ONLY the thirst bar, not hunger/warmth; each `Changed` event updates only its own bar. A shared-event or shared-index bug clobbers sibling bars. The HUD ticket is HARD-BLOCKED-BY hunger + thirst (both expose the read surface) — verify the surface is identical across all three at HUD review (grep all three for `Current01`/`IsCritical`/`Changed`).
- **Critical-state independence:** thirst going `IsCritical` must not flip hunger/warmth's critical flag. Assert per-need.

### 5b. Chop ↔ sticks chop-yield setting seam (the dead-knob seam)
- Sticks `86caa96rd` AC4 OWNS the `tree-chop wood yield` setting + the integration test; chop `86caa4c5c` AC2 OWNS the named yield constant the setting drives. **The one integration test that catches the dead knob:** change the setting → a REAL tree chop yields the new amount, live. Owned by sticks AC6; NOT duplicated in chop. At chop review verify chop reads the named source (no hardcoded literal); at sticks review verify the setting actually drives chop's output.

### 5c. Shared inventory `chopped wood` item (chop + sticks → same item)
- Chop yields `chopped wood`; sticks yield 1 `chopped wood` — the SAME item. **Assert:** picking sticks AND chopping both stack into the same inventory slot (one item id, one stack), not two parallel "wood" entries. Grep both branches for the item id; divergence = REQUEST_CHANGES.

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

**No NEW blocking gaps found** in this pass. Open soak-time questions to surface (non-blocking): stick respawn (sticks AC5 — finite vs respawn, Sponsor's call at soak); chop-yield as int vs int-range (sticks AC4, Sponsor's call from soak); thirst decay default rate relative to hunger (thirst AC1 — PR proposes, soak confirms).
