# QA Acceptance Plan — chop trees for wood (`86caa4c5c`)

**Ticket:** `feat(gameplay): chop trees for wood — wood to inventory + tweakable regrowth`
**PR:** _(pending — Unity CI lane FROZEN on Unity-license outage; AC-driven, lighter-depth pre-prep. Re-confirm against the real diff when the PR opens.)_
**Owner:** Drew (world trees + chop) + Devon (inventory hook). Reviewer: Devon. **Blocked-by:** inventory/belt `86caa4bya` + settings panel `86caa4bqp`. Ticket 3 of 4 (settings → inventory → chop → stone).
**Pattern source of truth:** the scatter + editor-vs-runtime guard `Assets/Tests/PlayMode/RockScatterPlayModeTests.cs`; the NavMesh/seed-42 guards `RoundIslandNavTests` / `RoundIslandNavCoveragePlayModeTests`; the inventory stack surface `Assets/Scripts/Runtime/Inventory.cs` (`AddWood`/`WoodCount`/`Changed`); the settings registry `86caa4bqp` AC6 pattern.

## What this feature is (one line)

Chop a tree with the equipped+selected axe (proximity + input) → N chops deplete it to a stump → yields `chopped wood` into inventory (stacks); the stump regrows after a tweakable (random min/max) timer; chop-anim speed tweakable via the settings panel.

## Process-gate preflight (HARD gates)

1. **Self-Test Report** present before review (UX-visible). On which build stamp, what was run/observed. Missing → REQUEST_CHANGES.
2. **Regression guard line in Done** + **cross-lane integration check** (the cross-lane seams: inventory `AddWood`/stack AND the settings-registry yield/anim-speed AND the `86caa96rd` chop-yield-reads-the-setting seam — name them).
3. **No diff touch on `team/DECISIONS.md`**. **CI green** by run ID on the PR head SHA (empty run = fail). **(Gated on Unity-license CI thaw.)**

## Test strategy split

- **PlayMode (primary — the feature is interactive + world-stateful):** chop → wood-to-inventory (stacks); N-chops-deplete-to-stump; stump regrows after the (driveable) timer; chop targets the NEAREST in-range tree; chop requires the axe SELECTED (not just owned).
- **EditMode:** seed-42 tree scatter present + island-shape/NavMesh invariant (the chop must use the EXISTING scatter, not re-roll it); the regrowth timer math (random-within-min/max bounds); the settings-registry binding (`tool-use speed` + `tree regrowth time` drive live params).
- **Shipped-build capture (mandatory):** chop a tree in the built exe → wood appears in the inventory ledger → tree depletes to a stump → (fast-forward via the tweakable timer) stump regrows. Boot clean (no `USER WARNING:`/`USER ERROR:`). Grounding: stump sits ON the terrain (gameplay-cam visual, not top-down — `verify-grounding-soaks-by-gameplay-cam-visual`).

## Acceptance cases — 1:1

| AC | Expectation | Layer | Silent-killer guard |
|---|---|---|---|
| **AC1** | Chop with axe EQUIPPED + SELECTED (belt item); proximity + input; chop-anim speed tweakable via `tool-use speed` setting. | PlayMode (chop gated on selected axe) + EditMode (setting binds anim speed). | **Axe-SELECTED gate** is load-bearing — a chop that works with no axe / a non-selected belt slot green-passes "wood added" and breaks the loop intent. Test the NEGATIVE (no axe selected → no chop). |
| **AC2** | Chopping yields `chopped wood` into inventory; N chops deplete the tree → stump; stacks per stack-size. | PlayMode. | **Façade/stack-sum seam (carry-forward):** assert `WoodCount` (or the contract count) SUMS correctly across stacks AND existing `InventoryTests` stay green — a chop that adds to a parallel field the HUD doesn't read is the silent killer. **Deplete-once:** N chops yield N× wood once, not infinite wood from one tree. |
| **AC3** | Trees regrow after a tweakable delay (~10 min default), RANDOM within a min/max range; stump → tree on timer. | EditMode (timer/range math, driveable) + PlayMode (stump regrows). | **Random-within-bounds** not a fixed value (assert N≥8 regrowth samples fall in [min,max] and aren't all identical). Driveable timer (don't make CI wait 10 min — the timer must be settable/fast-forwardable like the need `TickSeconds`). |
| **AC4** | Visual feedback (shake/leaf particles/fall); stump persists through regrowth. | Shipped capture (visual) + PlayMode (stump object persists). | Stump-persistence is the state guard (a stump that vanishes then pops a tree reads broken). Visual is a soak/Sponsor call. |
| **AC5** | World integrity: EXISTING seed-42 tree scatter; chop does NOT break island scatter / NavMesh; targets nearest in-range tree. | EditMode (seed-42/NavMesh invariance) + PlayMode (nearest-target). | **Seed-42 LOCK + NavMesh coverage non-regression** (mirror `RockScatterPlayModeTests` + `RoundIslandNavCoverage`). A stump that punches a NavMesh hole strands the player. |
| **AC6** | Regression guard: PlayMode — chop → `chopped wood` stacks; tree depletes then regrows after the tweakable timer. | PlayMode green from `-testResults` XML `<test-run result="Passed">`. | Empty run = fail. Tree-state machine (tree→stump→tree) asserted, not just the wood count. |

## Silent-killer seams

1. **Façade/stack-sum drift** — wood added to a field the HUD/contract doesn't sum (the inventory façade seam, carry-forward). Existing `InventoryTests` MUST stay green.
2. **Seed-42 / NavMesh regression** from world-state mutation (stump punching coverage).
3. **Axe-selected bypass** — chop works without the proper equipped+selected axe.
4. **CI-waits-10-min timer** — the regrowth timer must be driveable for headless CI; a real-time-only timer makes AC6 untestable headless.
5. **Editor-vs-runtime divergence** — trees/stumps pass EditMode but ship mangled in the loaded scene (the "legs-up" class — load Boot at runtime + assert, per `RockScatterPlayModeTests`).

## AC gaps / ambiguities (flag for Priya)

- **G1 — chop-yield ownership crosses `86caa96rd`.** AC2 ("chopping yields wood") must READ the `tree-chop wood yield` setting that `86caa96rd` REGISTERS — not a hardcoded constant. If chop lands FIRST with a hardcoded yield, `86caa96rd` converts it. **Recommend:** pin the seam — chop owns the mechanic + a named `wood per chop` constant; `86caa96rd` owns the setting that drives it. Confirm one AC6 test asserts chop reads the live setting (it lives in `86caa96rd` AC6 — confirm no gap).
- **G2 — "reuse the existing chop animation" (AC1)** assumes a chop anim exists. Confirm it does (axe-in-hand work `86caa83wn` is locomotion, not chop). If no chop anim exists, AC1 grows.
- **G3 — settings-panel host availability** (`86caa4bqp` `in progress`): AC1/AC3 register into it; confirm it's merged before chop lands, or extension-hook.
