# Gameplay-wave dispatch map — file-overlap + sequence/parallel groups

**Author:** Priya · **Date:** 2026-06-23 · **Branch:** `priya/groom-gameplay-wave`
**Purpose:** make the gameplay wave fan-out-ready the moment #100 (`86cabh907`, axe) merges and the Sponsor releases the held wave. This doc tells the orchestrator what to SEQUENCE vs PARALLELIZE, grounded in the actual file shapes on `main` (verified 2026-06-23, main @ `6936989`).

Scope of this pass: the four wave tickets the brief named — thirst `86caamkv7`, chop `86caa4c5c`, stones `86caa4c96`, sticks `86caa96rd`. (Settings `86caa4bqp` / inventory `86caa4bya` / hunger `86caamkp8` are already merged or in-flight; the foundations they own are on `main` — see "Foundations already landed" below.)

---

## Foundations already landed on main (changes the dependency picture)

The STATE.md header (2026-06-19) still reads as if settings + inventory + hunger are mid-flight. As of main @ `6936989` (this grooming pass), the following are MERGED on main:

- **`SurvivalNeed.cs`** — the shared abstract need base (hunger OWNS it, Pattern A). Surface: `Current01`/`Current`/`Max`/`IsCritical`, `event Action<float> Changed`, `TickSeconds`, `decayPerSecond`/`floor01`/`criticalThreshold01`, protected `Satisfy`, per-tier decay + `ApplyDifficulty`. **Thirst extends THIS — the base is on main, no Pattern-A wait.**
- **`HungerNeed.cs`** — `AddFood` / `TryEatBerry` / `berryRestoreAmount`. The eat-a-berry seam is wired.
- **`Settings/` registry** — `SettingsRegistry` (`AddFloat`/`AddRange`/`AddInt`/`Get`/`Has`/`ApplyAll`/`LoadAll`/`ResetAll`), `SettingEntry` + Float/Int/Range entries, `SettingsCatalog`, `SettingsPanel`, `UiInputGate`. The registry IS on main.
- **`Inventory.cs` + `InventoryUI.cs` + `ItemCatalog`** (`AxeId`/`WoodId`/`BerryId`, `AddWood`, `CountItem`, belt selection). The generic item model is on main.
- **`BerryBush.cs` / `EatBerryAction.cs`** — bushes `86caa5zz3` landed (PR #101).
- **World-gen:** `LowPolyZoneGen.ScatterIslandProps` (trees/rocks/bushes/grass via `GroundPoint` scale-immune grounding), seed `42` LOCKED (`IslandSeed = 42`), `ChopTree.cs` exists (the thin U2-3 chop — to be generalized by `86caa4c5c`).

⚠ One open question for the orchestrator: confirm whether settings `86caa4bqp` (#83) and inventory `86caa4bya` (#90) are FULLY merged to main vs the in-flight state the STATE header describes. The merged file set on main shows the registry + inventory CODE present; if #83/#90 are still mid-rebase the AC-gated registrations still apply. The wave tickets all carry the extension-hook fallback (AC5a/AC4b/AC3a) so they're safe either way.

---

## File-overlap matrix (the four wave tickets)

| File / surface | thirst `86caamkv7` | chop `86caa4c5c` | stones `86caa4c96` | sticks `86caa96rd` |
|---|---|---|---|---|
| `LowPolyZoneGen.cs` (`ScatterIslandProps` + Build* + `GroundPoint`) | **W** (pond placement) | R (existing tree scatter) | **W** (small-stone scatter) | **W** (stick/branch scatter) |
| `WorldBootstrap.cs` (scene scatter wiring) | **W** (pond) | R | possible W | possible W |
| `Inventory.cs` / `ItemCatalog` | — (NOT an item) | **W** (`chopped wood`) | **W** (`picked up stones` item) | R (reuse chop's `chopped wood`) |
| `SurvivalNeed.cs` / new `ThirstNeed.cs` | **W** (new `ThirstNeed`) | — | — | — |
| `SettingsCatalog.Populate` (registry registration) | **W** (thirst tweakables) | **W** (`tool-use speed` + `tree regrowth time`) | **W** (`stone respawn time`) | **W** (`tree-chop wood yield`) |
| `ChopTree.cs` (chop mechanic) | — | **W** (generalize the thin chop) | — | R (drives chop-yield setting) |
| Scene `Boot.unity` (large binary — merge-conflict magnet) | **W** (pond + need wiring) | possible W | possible W | possible W |

**W = writes/owns · R = reads/reuses · — = untouched**

### The two real collision zones

1. **`LowPolyZoneGen.ScatterIslandProps` + `WorldBootstrap` + `Boot.unity`** — thirst (pond), stones (small-stone scatter), and sticks (stick scatter) ALL add to the same scatter pass + the same scene. Chop only READS the existing tree scatter. Two world-scatter PRs touching `ScatterIslandProps` + `Boot.unity` in parallel = guaranteed merge conflict on the big binary scene + the scatter method. **These three must SERIALIZE through the world-gen surface, not parallelize.**

2. **`SettingsCatalog.Populate`** — chop, stones, sticks, AND thirst each register settings into the same `Populate` method. Four parallel PRs each appending a `reg.AddFloat(...)` block to the same method = textual conflicts. Per the wave tickets' AC fallbacks, the registrations can ship behind extension hooks if the panel isn't on main — but where they DO land in `Populate`, they collide. **Sequence the registrations, or land them as a single follow-up after the mechanics.** (The hunger-registration follow-up `86cabd75y` already proves this pattern: registration deferred to a separate S-ticket against `Populate`.)

**No hard CODE dependency between the four** (the brief's Q1 confirm): each depends only on already-merged foundations (`SurvivalNeed`, `Inventory`/`ItemCatalog`, `SettingsRegistry`, `LowPolyZoneGen`). They gate each other only on the two SHARED-FILE collision zones above + the single Unity build slot — NOT on each other's feature output. Per `no-gating-without-dependency`, that's a sequencing/scarce-resource constraint, not a blocker.

---

## Single-Unity-build-slot cap

Per `single-unity-build-slot-serializes-orchestration`: **≤1 Unity-build ticket in flight.** All four wave tickets are UX-visible → each needs a shipped-build capture → each holds the build slot when it builds. So the build-slot cap, NOT the dependency graph, is the dominant serializer. Fan out the NON-build lane (Tess acceptance plans, Uma any visual direction, Drew/Devon design/spec, Priya board) in parallel; the BUILD work goes one-at-a-time.

---

## Recommended dispatch order

The build-slot cap means "parallel" here = parallel AUTHORING/branch-work where files don't collide, serialized at the build/merge gate. Concretely:

### Group A (first — independent of the world-scatter collision zone)
- **thirst `86caamkv7`** — owns a DIFFERENT surface from chop/stones/sticks on the inventory side (it's NOT an item), and extends the already-merged `SurvivalNeed`. Its ONLY scatter-zone overlap is the pond in `ScatterIslandProps`/`Boot.unity`. Dispatch FIRST of the world-touching tickets OR sequence it adjacent to one stone/stick PR — but never concurrent with another `ScatterIslandProps` writer. Owner: Devon (need + drink) + Drew (pond).

### Group B (the chop/stick wood pair — sequence, don't parallelize the seam)
- **chop `86caa4c5c`** FIRST (owns the chop mechanic + the `WoodPerChop` named constant + `chopped wood` item path), then
- **sticks `86caa96rd`** (reuses chop's `chopped wood`, registers the `tree-chop wood yield` setting, converts chop's read to the setting). The chop-yield-reads-the-setting seam (chop AC2a ↔ sticks AC4a) is explicitly a "land chop's named constant first, sticks wires the setting" sequence. **Chop before sticks.** Both touch `ScatterIslandProps`/scene → serialize anyway.

### Group C (stones — independent mechanic, shares only the scatter zone)
- **stones `86caa4c96`** — small-stone scatter + pickup. No seam with chop/sticks beyond the shared `ScatterIslandProps` + `ItemCatalog`. Slot it wherever the build slot + world-gen surface is free — after or between B's PRs.

### Why this order
- **Build slot** forces one build at a time regardless → the order is really "which single ticket holds the slot next."
- **World-gen collision zone** (`ScatterIslandProps` + `Boot.unity`) is the tightest serializer: thirst-pond, stones-scatter, sticks-scatter all write it. Run them one at a time; rebase each onto the prior's merge (use `merge-from-main` to avoid force-push, `merge-from-main-avoids-force-push-rebase`).
- **chop → sticks** is a HARD ordering (the yield-setting seam); the rest is build-slot-ordered, not dependency-ordered.

**Net recommended sequence:** `thirst` → `chop` → `sticks` → `stones` (or interleave stones anywhere after thirst that the world-gen surface is free). The non-build lane (Tess plans for each, Uma pond/stone visual notes if wanted) fans out in parallel from day one.

### Settings registrations — batch or sequence
The four tickets' `SettingsCatalog.Populate` registrations collide. Recommended: each ticket ships its tweakable as a NAMED serialized field (the live source), and the panel registration lands in `Populate` SEQUENCED behind the mechanics — either folded into each ticket's PR if it's the build-slot holder at the time, or batched into a registration follow-up (mirror `86cabd75y`). Never two `Populate`-writing PRs concurrent.

---

## AC-readiness verdict (per ticket)

- **thirst `86caamkv7`** — READY. Full ACs + OOS + success-test + the 2026-06-19 clarification block (AC1a Pattern-A base / AC2a seed-42 pond / AC3a drink-seam test / AC5a panel-gate). `SurvivalNeed` base is now ON MAIN so the Pattern-A wait is resolved.
- **chop `86caa4c5c`** — READY (hardened this pass). Added AC5a: the `tool-use speed` + `tree regrowth time` registrations are panel-gated with the extension-hook fallback (was the one missing parity clause vs the siblings).
- **stones `86caa4c96`** — READY (hardened this pass). Added the whole clarification block it was missing: AC2a item-vocabulary from `ItemCatalog`, AC3a panel-gate fallback, AC4a seed-42/NavMesh non-regression.
- **sticks `86caa96rd`** — READY. Full ACs + the 2026-06-19 block (AC4a chop-yield-setting ownership / AC4b panel-gate). The chop↔stick seam is pinned on both sides.

All four are dispatch-ready — an author can pick any up without a clarifying question.

---

## RE-VERIFY ADDENDUM (Priya, 2026-06-24 — post thirst/3-bar-HUD/pond merges; main @ `b1afb05`)

The body above was authored 2026-06-23 @ `6936989`. Since then thirst (#124), the three-bar HUD (#129), and the pond landed on main, and the pond re-soak rework (PR #130, `devon/86cadj4g7-pond-fresh-blue`, OPEN/MERGEABLE) is in flight. Re-verifying the three to-do wave tickets (chop `86caa4c5c` / sticks `86caa96rd` / stones `86caa4c96`) against current main:

**Verdict: all 3 STILL dispatch-ready.** No dep regressed; the deltas below are AC-accuracy refinements (now pinned on each ticket as a "MAIN-STATE RE-VERIFY" block), not new blockers.

### What changed on main vs the matrix above

1. **`SettingsCatalog` de-collision is now PROVEN, not theoretical.** Thirst landed its tweakables via a NEW `SettingsCatalog.PopulateThirst(reg, thirst)` method called from `Build(...)` — NOT by editing `Populate`'s body. So the matrix's "four PRs all append to `Populate` → textual conflict" risk is mitigated: each wave ticket adds its OWN `PopulateChop`/`PopulateSticks`/`PopulateStones` method; the only shared line is the one-line call inside `Build` (trivial). Pinned on all 3 tickets (V2/V3).

2. **`tool-use speed` is ALREADY a greyed extension-hook row in `Populate`** (`ToolSpeedId` = `"tool_use_speed"`, `available:false`, dummy getter). Chop FLIPS IT LIVE — it does NOT register a new row (would be a duplicate-id collision). Pinned on chop (V1).

3. **`ItemCatalog` ids are all on main:** `WoodId="wood"`, `StoneId="stone"`, `BerryId="berry"`, `AxeId="axe"`. The tickets' Sponsor-resource names ("chopped wood" / "picked up stones") are display labels — the canonical code ids are `WoodId`/`StoneId`. Stones' AC2a "if not yet named, coordinate" fallback is now MOOT (the id exists). Pinned on chop (V3) / sticks (V2) / stones (V1).

4. **Settings-panel host (86caa4bqp) is MERGED** → the AC5a/AC4b/AC3a "ship behind a hook if the panel isn't on main" fallbacks are RESOLVED for the panel-availability axis. The registrations land LIVE. (The chop↔stick yield-read seam (chop AC2a ↔ sticks AC4a) is unchanged and still the one hard cross-ticket seam.)

### ⚠ NEW SEAM — #130 `OverlapsAnyRock` (was NOT in the matrix above)

PR #130 reworks `LowPolyZoneGen.ScatterIslandProps`: the rock pass now records `rockFootprints` (a `List<Vector4>` of placed-rock XZ + `RockFootprintRadius*scale`) and the grass pass calls the new PUBLIC `LowPolyZoneGen.OverlapsAnyRock(rockFootprints, x, z)` (consts `RockFootprintRadius=0.55f`, `GrassRockPad=0.35f`) so grass never sprouts through a stone.

- **Stones + sticks DO scatter into the SAME rock-region #130 rewrites** → they MUST (a) dispatch AFTER #130 merges, (b) extend the #130-reworked rock pass (which already maintains `rockFootprints`), and (c) consume `OverlapsAnyRock` so the new small-stone / stick props don't bury inside a boulder AND don't regress the grass-in-stone fix. A scatter PR that ignores `rockFootprints` = REQUEST_CHANGES. Pinned on stones (V2) / sticks (V1).
- **Chop only READS the tree scatter** → no #130 collision; rebase-only. Pinned on chop (V4).
- **Sequencing impact:** #130 joins thirst-pond as a `ScatterIslandProps`+`Boot.unity` writer. The "never two `ScatterIslandProps` writers concurrent" rule now means: land #130 FIRST, then stones/sticks serialize behind it (rebase via `merge-from-main`, no force-push). The net recommended order is unchanged — `thirst`(merged) → `chop` → `sticks` → `stones` — with the added gate that the two scatter tickets (stones/sticks) wait for #130 to land before their scatter pass goes in.
