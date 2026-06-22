# QA Acceptance Plan — scatter bushes + harvestable berries (`86caa5zz3`)

**Ticket:** `feat(world): scatter bushes around the island (varied sizes/types) + harvestable berry bushes`
**PR:** _(pending — Unity CI lane FROZEN; AC-driven, lighter-depth pre-prep. Re-confirm vs the real diff when the PR opens.)_
**Owner:** Drew (scatter + harvest) + Devon (berry-item/inventory hook). Reviewer: Devon. **Blocked-by:** inventory `86caa4bya`. SIBLING of chop/stone.
**Pattern source of truth:** `RockScatterPlayModeTests.cs` (scatter presence + editor-vs-runtime + grounding), `RoundIslandNav*` (seed-42/NavMesh), `Inventory.cs` (stack surface). **Feeds:** the HUNGER need `86caamkp8` (the eat-action this ticket ships calls hunger's `AddFood` — but what eating RESTORES is DEFERRED to `86caamkp8`).

## What this feature is (one line)

Bushes scattered in the seed-42 world in varied sizes/types; SOME are berry-bushes; proximity + interact (no tool) harvests berries → `berries` stackable inventory item (with a cute low-poly icon); berries regrow after a tweakable timer; a basic eat/consume action removes one berry from inventory (what it RESTORES is `86caamkp8`).

## Process-gate preflight (HARD gates)

1. **Self-Test Report** present before review (UX-visible). Build stamp + run/observed. Missing → REQUEST_CHANGES.
2. **Regression guard line in Done** + **cross-lane integration check** (inventory stack seam + settings `berry regrowth time` registry + the eat-action → `86caamkp8` seam — name them).
3. **No `team/DECISIONS.md` touch.** **CI green** by run ID on the PR head SHA (empty run = fail). **(Gated on Unity-license CI thaw.)**

## Test strategy split

- **PlayMode (primary):** harvest a berry-bush → `berries` to inventory (stacks); berries regrow after the (driveable) timer; the eat-action consumes ONE berry from inventory; harvest is proximity-gated; only berry-bush variants carry berries (a plain bush yields nothing).
- **EditMode:** seed-42 bush scatter present + island-shape/NavMesh invariant; bush VARIETY (≥ a few distinct sizes/types — not uniform clones); grounding (bushes rest ON terrain, scale-immune); regrowth timer random-within-min/max; settings-registry binding (`berry regrowth time`); the berry item + icon defined.
- **Shipped-build capture (mandatory):** bushes visible on the island (varied, not cloned) → harvest berries → berries in inventory → (fast-forward timer) berries regrow. Boot clean. Grounding via gameplay-cam visual.

## Acceptance cases — 1:1

| AC | Expectation | Layer | Silent-killer guard |
|---|---|---|---|
| **AC1** | Bushes scattered across seed-42 in varied sizes/types; integrate with existing scatter; do NOT break seed-42/NavMesh; rest ON terrain. | EditMode (present, seed-42/NavMesh invariant, grounding) + shipped capture. | **Seed-42 LOCK + NavMesh coverage** non-regression; scale-immune grounding. |
| **AC2** | Bush VARIETY — a few distinct sizes + types so the scatter reads natural, not cloned. | EditMode (≥N distinct variants present) + shipped capture (visual variety). | Assert MORE than one variant/size actually placed (a "variety" that ships one cloned bush is the silent killer). |
| **AC3** | SOME bushes carry berries (a berry-bush variant); harvest (proximity + interact, no tool) → `berries` stackable to inventory; berry item + cute low-poly icon defined (IconBaker route). | PlayMode (harvest → stack) + EditMode (item + icon exist). | **Berry-bush vs plain-bush discrimination** (a plain bush must yield nothing — the negative). **Stack-sum/façade seam (carry-forward).** **Proximity gate** (far → no harvest). |
| **AC4** | Berry bushes regrow berries after a tweakable delay (random min/max); the bush persists, only berries deplete + regrow; `berry regrowth time` setting registered. | EditMode (timer/range, driveable; setting binds) + PlayMode (berries regrow, bush persists). | Random-within-bounds (N≥8). Driveable timer (no 10-min CI wait). **Bush-persists** (only the berries cycle — a regrow that re-spawns the whole bush is wrong). |
| **AC5** | Eat/consume action removes ONE berry from inventory (what it RESTORES is DEFERRED to `86caamkp8`). | PlayMode (eat → berry count −1). | **The eat→hunger seam ownership** (cross-lane with `86caamkp8`): this ticket ONLY removes the berry; the `AddFood` restore lives in `86caamkp8`. **Graceful no-HungerNeed:** if hunger hasn't landed, the eat-action must no-op the restore gracefully (consume the berry, skip the missing `AddFood`) — NOT null-ref. |
| **AC6** | Regression guard: PlayMode — bushes grounded; harvest → `berries` stacks; regrow after tweakable timer; eat consumes a berry. | PlayMode green from `-testResults` XML `<test-run result="Passed">`. | Empty run = fail. |

## Silent-killer seams

1. **Stack-sum/façade drift** — berries added to a non-summed field; existing `InventoryTests` MUST stay green (this is the exact bug class that "pickup_count > 0 passed during the dual-spawn era").
2. **Eat→hunger seam half-wiring / null-ref** when `86caamkp8` hasn't landed yet (sequence risk — see G1).
3. **Berry-bush vs plain-bush mis-harvest** + **cloned "variety"**.
4. **Seed-42 / NavMesh regression** + **floating bushes** (gameplay-cam grounding).
5. **CI-waits-10-min regrow timer** — must be driveable.
6. **Editor-vs-runtime divergence** — load Boot at runtime + assert (per `RockScatterPlayModeTests`).

## AC gaps / ambiguities (flag for Priya)

- **G1 — eat-action vs HUNGER need (`86caamkp8`) sequence + test ownership.** AC5 ships the eat-action (consume) but DEFERS what it restores to `86caamkp8`. If THIS ticket lands FIRST, the eat-action has no `AddFood` to call → must no-op gracefully. The atomic "berry −1 AND hunger +restore" test belongs to `86caamkp8` (it owns `AddFood`); THIS ticket tests only "berry −1 on eat." **Recommend:** Priya pin the test split + the graceful-no-HungerNeed behavior (this is the mirror of hunger's G1). Sequence: either bushes after hunger, OR bushes ships the eat-action behind a "if HungerNeed present" guard.
- **G2 — berry item name single-source-of-truth.** The berry item is defined here, but the wood items are defined in inventory `86caa4bya`. Confirm `berries` is named per the inventory ticket's item convention (single source of truth), mirroring `86caa96rd`'s "follow `86caa4bya`'s name" rule.
- **G3 — settings-panel host availability** (`86caa4bqp` `in progress`): AC4 registers `berry regrowth time`; sequence or extension-hook.
