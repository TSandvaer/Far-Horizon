# QA Acceptance Plan — pick up small stones (`86caa4c96`)

**Ticket:** `feat(gameplay): pick up small stones — to inventory + tweakable respawn`
**PR:** _(pending — Unity CI lane FROZEN; AC-driven, lighter-depth pre-prep. Re-confirm vs the real diff when the PR opens.)_
**Owner:** Drew (stone scatter + pickup) + Devon (inventory hook). Reviewer: Devon. **Blocked-by:** inventory `86caa4bya` + settings `86caa4bqp`. Ticket 4 of 4.
**Pattern source of truth:** `Assets/Tests/PlayMode/RockScatterPlayModeTests.cs` (scatter presence + editor-vs-runtime + grounding), `RoundIslandNavTests`/`RoundIslandNavCoveragePlayModeTests` (seed-42/NavMesh), `Inventory.cs` (stack surface). This is the SIMPLEST world-resource (no tool, no anim, no deplete-to-stump state).

## What this feature is (one line)

Small pickable stones scattered in the seed-42 world (only SMALL ones — bigger rocks are future pickaxe-mining, OOS); proximity + interact (no tool) → `picked up stones` into inventory (stacks); a picked spot respawns a stone after a tweakable (random min/max) timer; stones rest ON the terrain (scale-immune grounding).

## Process-gate preflight (HARD gates)

1. **Self-Test Report** present before review (UX-visible). Build stamp + run/observed. Missing → REQUEST_CHANGES.
2. **Regression guard line in Done** + **cross-lane integration check** (inventory stack seam + settings respawn registry — name them).
3. **No `team/DECISIONS.md` touch.** **CI green** by run ID on the PR head SHA (empty run = fail). **(Gated on Unity-license CI thaw.)**

## Test strategy split

- **PlayMode (primary):** pick up → `picked up stones` to inventory (stacks); only SMALL stones pickable (a big rock is NOT pickable — the negative); picked spot respawns after the (driveable) timer; pickup is proximity-gated (far → no pickup).
- **EditMode:** seed-42 stone scatter present + island-shape/NavMesh invariant; grounding math (stones sit ON terrain, scale-immune — NOT float/sink); respawn timer random-within-min/max; settings-registry binding (`stone respawn time` drives the live param).
- **Shipped-build capture (mandatory):** pick up a stone in the built exe → it appears in inventory → spot respawns (fast-forward via the tweakable timer). Boot clean. Grounding via gameplay-cam visual (stones sit on the terrain, not floating — `verify-grounding-soaks-by-gameplay-cam-visual`).

## Acceptance cases — 1:1

| AC | Expectation | Layer | Silent-killer guard |
|---|---|---|---|
| **AC1** | Small stones scattered (reuse/extend rock scatter or a small-stone variant); only SMALL pickable; big rocks = future mining (OOS). | EditMode (scatter present, seed-42 invariant) + PlayMode (big rock NOT pickable). | **Small-vs-big discrimination** is load-bearing — a pickup that grabs a big "mining" rock breaks the Sponsor's explicit scope split. Test the NEGATIVE (big rock → no pickup). |
| **AC2** | Pick up (proximity + interact, NO tool) → `picked up stones` to inventory (stacks). | PlayMode. | **Stack-sum/façade seam (carry-forward):** the stack count sums correctly + existing `InventoryTests` stay green. **Proximity gate:** far → no pickup (the negative). |
| **AC3** | Stones respawn after a tweakable (~10 min default) RANDOM min/max delay; picked spot respawns on timer. | EditMode (timer/range math, driveable) + PlayMode (spot respawns). | Random-within-bounds (N≥8 samples in [min,max], not all identical). Driveable timer (don't make CI wait 10 min). |
| **AC4** | Stones rest ON terrain (scale-immune grounding); do NOT break scatter / NavMesh. | EditMode (grounding + seed-42/NavMesh invariant) + shipped capture (visual grounding). | **Scale-immune grounding** (the 100×-FBX float/sink trap; mirror the rock grounding). **Seed-42 LOCK + NavMesh coverage** non-regression. |
| **AC5** | Regression guard: PlayMode — pick up → `picked up stones` stacks; respawns after the tweakable timer. | PlayMode green from `-testResults` XML `<test-run result="Passed">`. | Empty run = fail. Respawn-state (present → picked → respawned) asserted, not just the count. |

## Silent-killer seams

1. **Stack-sum/façade drift** — stones added to a non-summed field; existing `InventoryTests` MUST stay green.
2. **Small-vs-big mis-pickup** — grabbing a future-mining big rock (scope break).
3. **Floating/sinking stones** — scale-immune grounding via gameplay-cam visual, not metrics (which have lied).
4. **Seed-42 / NavMesh regression.**
5. **CI-waits-10-min respawn timer** — must be driveable headless.
6. **Editor-vs-runtime divergence** — load Boot at runtime + assert presence (per `RockScatterPlayModeTests`).

## AC gaps / ambiguities (flag for Priya)

- **G1 — "small" definition.** AC1 says only SMALL stones are pickable, big rocks are future-mining. What's the threshold (scale? a tag? a separate prefab variant)? If big rocks are the EXISTING `RockMesh` scatter and small stones are NEW, the pickable set is clean; if a single scatter has both, a size threshold must be pinned testably. **Recommend:** Priya/Drew pin the discriminator (prefab variant vs scale tag) so AC1's NEGATIVE test is unambiguous.
- **G2 — respawn vs sticks (`86caa96rd` AC5) inconsistency.** Stones respawn (AC3); sticks deliberately do NOT (`86caa96rd` AC5). Both are "free early-game gather" siblings — confirm the Sponsor wants this asymmetry (it's intentional per the stick ticket, but worth a soak confirm).
- **G3 — settings-panel host availability** (`86caa4bqp` `in progress`): AC3 registers into it; sequence or extension-hook.
