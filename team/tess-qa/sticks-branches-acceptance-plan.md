# QA Acceptance Plan — scatter pick-up sticks & branches (`86caa96rd`)

**Ticket:** `feat(world): scatter pick-up sticks & branches around the island — 1 wood each`
**PR:** _(pending — Unity CI lane FROZEN; AC-driven, lighter-depth pre-prep. Re-confirm vs the real diff when the PR opens.)_
**Owner:** Drew (scatter + pickup) + Devon (inventory hook + settings wiring). Reviewer: Devon. **Blocked-by:** inventory `86caa4bya` + settings `86caa4bqp`. SIBLING of chop/stone/bushes. **Coordinates with chop `86caa4c5c`** (this ticket adds the `tree-chop wood yield` setting chop must READ).
**Pattern source of truth:** `RockScatterPlayModeTests.cs` (scatter + editor-vs-runtime + grounding), `RoundIslandNav*`, `Inventory.cs` (`AddWood`/`WoodCount` — sticks add to the SAME `chopped wood` resource chop yields; do NOT define a second wood type).

## What this feature is (two lines)

(1) Small sticks/branches scattered in the seed-42 world in varied sizes; proximity + interact (no tool) → exactly **1 wood** to inventory as the SAME `chopped wood` resource chop yields; the picked stick is consumed.
(2) A `tree-chop wood yield` setting registered into the settings panel that drives `86caa4c5c`'s chop yield live (the low-yield stick vs high-yield chop balance the Sponsor dials).

## Process-gate preflight (HARD gates)

1. **Self-Test Report** present before review (UX-visible). Build stamp + run/observed. Missing → REQUEST_CHANGES.
2. **Regression guard line in Done** + **cross-lane integration check** (the SAME `chopped wood` item reuse + the chop-yield-reads-the-setting seam with `86caa4c5c` — name them; this is the highest cross-lane-coupling ticket of the family).
3. **No `team/DECISIONS.md` touch.** **CI green** by run ID on the PR head SHA (empty run = fail). **(Gated on Unity-license CI thaw.)**

## Test strategy split

- **PlayMode (primary):** pick up a stick → exactly 1 `chopped wood` to inventory (stacks); the stick is consumed (removed from world); pickup is proximity-gated (far → nothing); changing the `tree-chop wood yield` setting → a tree chop yields the new amount (live).
- **EditMode:** seed-42 stick scatter present + varied sizes + island-shape/NavMesh invariant; grounding (sticks rest ON terrain, scale-immune); the `1 wood per stick` named constant (not magic-scattered); the settings-registry binding (`tree-chop wood yield` drives chop's live yield).
- **Shipped-build capture (mandatory):** sticks scattered on the island (varied) → walk up + pick up → 1 wood in inventory; AND the chop-yield setting visibly changing tree-chop output (the Sponsor's soak-tuning beat). Boot clean. Grounding via gameplay-cam visual.

## Acceptance cases — 1:1

| AC | Expectation | Layer | Silent-killer guard |
|---|---|---|---|
| **AC1** | Sticks/branches scattered across seed-42 in VARIOUS SIZES; integrate with existing scatter; do NOT break seed-42/NavMesh/existing scatter; rest ON terrain. | EditMode (present, varied, seed-42/NavMesh invariant, grounding) + shipped capture. | **Seed-42 LOCK + NavMesh coverage** non-regression (the family-wide killer); scale-immune grounding; assert >1 size/variant (cloned "variety" = silent killer). |
| **AC2** | Pick up (proximity + interact, no tool — REUSE the stone/berry pattern, not a new input) → 1 wood to the `chopped wood` resource; stick consumed. | PlayMode. | **SAME-item reuse:** assert the stick adds to the SAME `chopped wood` count chop uses — a second wood type is an explicit OOS violation. **Stack-sum/façade seam (carry-forward).** **Proximity gate** (the negative). **Consumed-once** (stick removed; can't re-pick). |
| **AC3** | Yield CONTRAST: 1 wood per stick, FAR LESS than a tree chop; the 1-wood value is a NAMED constant (not magic-scattered). | EditMode (named constant; 1 < chop-yield). | Assert the stick yield (1) is strictly LESS than the chop yield default — the whole point is the low/high contrast. |
| **AC4** | Register a `tree-chop wood yield` setting; wire `86caa4c5c`'s chop to READ it (live, no restart); default = chop's current yield; stick stays 1. | EditMode/PlayMode (setting binds + drives chop yield live). | **The cross-lane seam:** changing the setting must change the TREE-CHOP output, not the stick's 1. This is THE integration test of the ticket — owned HERE (it owns the setting). Confirm chop (`86caa4c5c`) actually reads it (not a hardcoded constant left behind). |
| **AC5** | Sticks do NOT respawn per-spot in v1 (finite early gather) UNLESS trivially free; if respawn wanted, mirror stone-respawn; else OOS + note. | PlayMode (a picked stick stays gone) OR documented OOS. | Confirm the chosen behavior matches the ticket (no-respawn default). If respawn IS added, it inherits the random-min/max + driveable-timer guards. |
| **AC6** | Regression guard: PlayMode — sticks grounded on seed-42; pick up → exactly 1 `chopped wood` (stacks); the `tree-chop wood yield` setting drives chop yield (change → chop yields new amount). Mirror the stone-pickup test shape. | PlayMode green from `-testResults` XML `<test-run result="Passed">`. | Empty run = fail. The setting-drives-chop assertion is the load-bearing cross-lane guard. |

## Silent-killer seams

1. **Second-wood-type regression** — sticks defining their OWN wood item instead of reusing `chopped wood` (explicit OOS). Assert the SAME count.
2. **Stack-sum/façade drift** — existing `InventoryTests` MUST stay green.
3. **Chop-yield seam left hardcoded** — the setting registered but `86caa4c5c`'s chop NOT actually reading it (a dead knob). The change-setting → chop-output test is the guard.
4. **Seed-42 / NavMesh regression** + **floating sticks** (gameplay-cam grounding).
5. **Editor-vs-runtime divergence** — load Boot at runtime + assert (per `RockScatterPlayModeTests`).

## AC gaps / ambiguities (flag for Priya)

- **G1 — chop-yield seam ownership across `86caa4c5c` ↔ `86caa96rd`** (the mirror of chop's G1). `86caa4c5c` AC2 yields wood; THIS ticket's AC4 registers the setting + wires chop to read it. If chop lands FIRST with a hardcoded yield, THIS ticket converts it. **Recommend:** Priya pin the seam + which ticket's AC6 owns the change-setting → chop-output test (recommend HERE, since this ticket owns the setting). Sequence: this ticket alongside/after chop.
- **G2 — `chopped wood` vs `wood` item name.** The ticket says reuse `86caa4bya`'s item, "if it names it `wood` rather than `chopped wood`, follow that name." Confirm the inventory ticket's actual item name before the stick PR hardcodes a string — single source of truth is `86caa4bya`.
- **G3 — stick respawn (AC5) is a soak-deferred Sponsor question.** AC5 leaves respawn OOS unless trivially free, and asks to surface at soak if it reads thin. Flag this as a Sponsor soak-question, not a hard AC.
- **G4 — settings-panel host availability** (`86caa4bqp` `in progress`): AC4 registers into it; sequence or extension-hook.
