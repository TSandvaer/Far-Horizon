# QA Acceptance Plan — THIRST need + freshwater pond (`86caamkv7`)

**Ticket:** `feat(survival): THIRST need — decays over time + a freshwater pond (drink-from-hand) restores it`
**PR:** _(pending — Unity CI lane FROZEN on a runner Unity-license outage; AC-driven, not diff-driven. Re-confirm against the real diff when the PR opens.)_
**Authored:** pre-wave forward-prep against the LOCKED ACs (not started). No builds / runs tonight.
**Pattern source of truth (read at HEAD on `main`):** `Assets/Scripts/Runtime/WarmthNeed.cs` + `WarmthNeedTests`/`WarmthNeedPlayModeTests` (the need MIRRORS warmth EXACTLY). World-gen seam: `Assets/Scripts/Editor/LowPolyZoneGen.cs` + `Assets/Scripts/Editor/WorldBootstrap.cs`. Scatter/NavMesh guard precedents: `Assets/Tests/EditMode/RoundIslandNavTests.cs`, `Assets/Tests/PlayMode/RoundIslandNavCoveragePlayModeTests.cs`, `Assets/Tests/PlayMode/RockScatterPlayModeTests.cs`.
**HUD consumer (downstream):** `86caamkxv` binds `ThirstNeed.Current01`/`.IsCritical`/`.Changed`. This ticket OWNS that contract surface.
**Drink interaction precedent:** the stone-pickup / harvest interaction pattern (proximity + interact) — NOT an inventory item.

## What this feature is (two lines)

(1) A `ThirstNeed` MonoBehaviour mirroring the warmth surface EXACTLY (decay over a real Time.time window; `Current01`/`Current`/`Max`/`IsCritical`/`Changed`; `TickSeconds`; floor, not fail), with an `AddWater(amount)` satisfaction hook.
(2) A small FRESHWATER POND placed in the seed-42 island world (distinct from the salt sea), with a DRINK-FROM-HAND interaction: proximity + interact → a small per-scoop restore, repeatable, no tool, no inventory item.

## Process-gate preflight (HARD gates — bounce on any miss)

1. **Self-Test Report comment** present before I review. UX-visible (HUD bar + a world feature). What was run, **on which build stamp**, what was observed — concrete values only. Missing → REQUEST_CHANGES.
2. **Regression guard line in the Done clause** + **cross-lane integration check** (the cross-lane seams: the world-gen scatter/NavMesh AND the HUD-bar contract — name both).
3. **PR body states** the chosen thirst decay default + per-scoop restore default + rationale (AC1/AC3 ask for "propose a default"; the fiction is "thirsty AFTER the berries" → thirst proposed faster than hunger).
4. **No diff touch on `team/DECISIONS.md`** (non-Priya). If touched → bounce.
5. **CI green** cited by run ID on the PR head SHA. Empty run = fail. **(Gated on the Unity-license CI thaw.)**
6. **Vocabulary contract** (shared need base with hunger `86caamkp8`): whichever need lands first OWNS the base; the second EXTENDS it. At review grep the hunger branch for the base type + surface names; ANY divergence = **REQUEST_CHANGES (mergeability-blocking, not a NIT).**

## Test strategy — EditMode / PlayMode / shipped-build-capture split

- **EditMode (need math, fast headless):** decay over a window via `TickSeconds`, floor rest, clamp, `AddWater` restore + clamp, `Changed` on decay AND scoop, no-op `Changed` discipline, `IsCritical`. Mirror `WarmthNeedTests` 1:1.
- **EditMode (world-gen determinism):** the pond is PRESENT in the seed-42 generation and does NOT shift the island shape / existing scatter (deterministic-seed assertion — mirror `RoundIslandNavTests` / the scatter scene guards). World-gen runs editor-time (`LowPolyZoneGen`/`WorldBootstrap` are in `Assets/Scripts/Editor`) → the presence + shape-invariance check is EditMode-appropriate.
- **PlayMode (decay FIRES over a real Time.time window — deltaTime≈0 trap):** thirst measurably DROPS; `AddWater` restores end-to-end. Mirror `WarmthNeedPlayModeTests`.
- **PlayMode (pond reachable + drink-scoop works):** the pond is ON/near the NavMesh + reachable (mirror `RoundIslandNavCoveragePlayModeTests`); a drink-scoop near the pond raises thirst by the per-scoop amount; repeatable (N scoops raise N×amount up to max). Proximity gating: a scoop attempt FAR from the pond does NOTHING.
- **Shipped-build capture (mandatory — UX-visible world feature):** the pond visible IN the world (reads as fresh water, NOT the salt sea); walk to it; drink-scoop raises the thirst readout (bar once `86caamkxv` lands, else a log trace). Boot is clean (no `USER WARNING:`/`USER ERROR:`). **Grounding check (carry-forward `verify-grounding-soaks-by-gameplay-cam-visual`):** open the player-framing gameplay-cam capture and SEE the pond sits on/in the terrain — metrics + high-angle captures have lied before (served floating builds twice).

## Acceptance cases — mapped 1:1 to the ACs

| AC | Expectation | How (test layer) | Tolerance / silent-killer guard |
|---|---|---|---|
| **AC1** | THIRST decays over a Time.time window; surface mirrors warmth EXACTLY; decay tuned per fiction (faster than hunger). | EditMode: `TickSeconds(t)` drops `Current` by rate*t. PlayMode: measurable real-window drop. | **Surface-shape parity** (HUD contract — same as hunger). **Decay ordering:** assert proposed thirst default decays faster than the hunger default (the "thirsty after berries" fiction) OR the PR justifies. |
| **AC2** | A small FRESHWATER POND in the seed-42 world, distinct from the sea, cute/warm low-poly, integrated with existing world-gen; does NOT break island shape / NavMesh / scatter. | EditMode: pond present in seed-42 gen + island-shape + existing-scatter invariance (mirror `RoundIslandNavTests`/scatter scene guards). Shipped capture: pond reads as fresh water vs the sea. | **Seed-42 LOCK (`world-is-big-round-island` memory):** assert the island silhouette / NavMesh coverage is UNCHANGED vs pre-pond (a pond that re-rolls the seed or carves the NavMesh is the silent killer — the world shifts under every other feature). **Fresh-vs-salt read:** a soak visual gate (the pond must not look like a sea inlet) — flag for Uma/Sponsor visual sign-off if ambiguous. |
| **AC3** | DRINK-FROM-HAND: proximity + interact → small per-scoop restore; repeatable; no tool, no inventory item; per-scoop amount tweakable. | PlayMode: scoop near pond → thirst +per-scoop; N scoops → N× up to max; scoop FAR from pond → no change. | **Proximity gating** is load-bearing — a scoop that works anywhere (no distance check) green-passes "thirst rose" but breaks the fiction. Assert the negative (far → no restore). **Not-an-item:** assert NO inventory entry is created/consumed (distinct from berries — a regression that routes water through inventory is a design break). |
| **AC4** | Simple FLOOR — thirst stops at `floor01`; NOT a fail-state. | EditMode: `TickSeconds(huge)` rests at floor; holds. | Floor-overshoot clamp (same as warmth/hunger). No death/dehydration event (OOS). |
| **AC5** | Tweakables registered into `86caa4bqp`: `thirst decay rate`, `water scoop amount` (optionally floor/critical). | EditMode/PlayMode registry test (mirror `86caa4bqp` AC6): setting binds + drives the live param. | Live-binding (not a dead label); naming exactness (`thirst decay rate`, `water scoop amount`). |
| **AC6** | Regression guard: paired EditMode + PlayMode — decay over a window; pond present + on/near NavMesh + reachable (does NOT break seed-42/NavMesh — assert like bush/stone scatter guards); scoop → thirst +amount; floor halts decay; `Changed` on decay AND scoop. | Both suites green from `-testResults` XML `<test-run result="Passed">`. | **`Changed`-on-scoop is load-bearing** (HUD subscribe-never-poll — a scoop that raises thirst but doesn't fire `Changed` silently freezes the bar). **NavMesh-coverage non-regression:** the reachable-coverage % must not drop vs pre-pond (a pond placed on a walkable region that wasn't re-baked strands the player / orphans terrain). |

## Silent-killer seams to guard (carry-forward + new)

1. **Seed-42 / NavMesh regression** (the world-shifts-under-everything killer). The pond must integrate WITHOUT re-rolling the seed, moving existing scatter, or shrinking NavMesh coverage. **Guard:** pre/post island-shape + NavMesh-coverage invariance assertions; mirror the existing round-island Nav tests.
2. **Surface-parity drift + `Changed`-not-firing** (HUD contract — same class as hunger). The HUD binds thirst identically to warmth; a divergent surface or a missing `Changed`-on-scoop silently breaks `86caamkxv`.
3. **Proximity bypass.** A drink that restores anywhere (no distance gate) passes the positive test and breaks the feature. Always test the FAR-from-pond negative.
4. **Water-as-inventory-item regression.** Thirst is NOT berries — no inventory entry. A copy-paste from the berry eat-action that routes water through inventory is a design break; assert inventory is untouched by a scoop.
5. **Floating pond** (`verify-grounding-soaks-by-gameplay-cam-visual`). Verify the pond sits IN the terrain via the player-framing gameplay-cam capture, not a top-down/metrics check (both lied before).
6. **deltaTime≈0 headless false-green** on decay (PlayMode real-window test).

## AC gaps / ambiguities found (flag for Priya)

- **G1 — Shared need base ownership** (same as hunger G2): hunger + thirst both may extract a shared base; neither NAMES it. Per parallel-shared-concept discipline this is mergeability-risk. **Recommend Pattern A** (whichever lands first owns the base; sequence them) OR a named vocabulary contract in both tickets before dispatch.
- **G2 — Pond placement vs seed-42 LOCK.** AC2 says "place a pond" and "do NOT break seed-42." If the pond is part of the seeded generation, placing it CHANGES the seed-42 output (the silhouette/scatter shift). Two readings: (a) the pond is a deterministic ADD on top of the locked terrain (no re-roll), or (b) it's a hand-placed scene object outside the generator. **Recommend:** Priya/Drew pin which — (a) needs the invariance test to assert the EXISTING scatter is byte-stable while the pond is added; (b) sidesteps seed risk but needs a scene-presence guard. This drives the AC6 test shape.
- **G3 — Owner split (Devon need + Drew pond) → integration ownership.** The need (Devon) and the pond+drink (Drew, per the ticket's "Drew on placing the pond") are TWO surfaces. The drink-scoop → `AddWater` seam crosses them. **Recommend:** pin that the drink-scoop → `AddWater` integration test is owned by ONE side (cite it), so the seam isn't a coverage gap (the dual-spawn class). Also: two owners on one ticket may need two PRs or one coordinated PR — confirm with Priya.
- **G4 — Settings-panel host availability** (same as hunger G3): AC5 registers into `86caa4bqp` (`in progress`). If the panel hasn't merged, AC5 has no host — sequence or extension-hook.
