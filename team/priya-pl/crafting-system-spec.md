# Crafting-system redesign — spec + re-scope (Priya, 2026-07-08)

> **Status:** dispatch-shaping spec + a re-scoped ticket-DRAFT table for the orchestrator to create on
> list `901523878268` (Priya does NOT create ClickUp tickets — away-mode, the orchestrator owns all
> ClickUp writes). This is the destination-shaping doc; the dev picks the mechanism inside the
> constraints. Shape mirrors `island-2.0-ticket-draft.md` (the ratified 3-bucket Commander's-Intent form).
> **Design source (AUTHORITATIVE):** ticket `86camz6n0` — the grill-resolved crafting-system design
> (Sponsor + orchestrator, 2026-07-08), grounded on the forge-soak feedback (build `4cb464b`, I-3 PR
> #292) captured in `86camyvzw` / `86camyvwn`.
> **Supersedes:** `iron-model-a-spec.md` (merged #268) on the **crafting-table question** — Model-A sided
> with "extend the thin `CraftSpot` bench"; this redesign RETIRES that bench for a placed recipe-menu table
> and generalises the whole crafting surface to three tiers + a unified place-to-build flow. Model-A's
> shipped mine (#287) + smelt (#292) mechanics are REUSED, not re-litigated.

---

## 0. The locked design (grill-resolved — do NOT re-litigate)

From ticket `86camz6n0` (Sponsor + orchestrator, 2026-07-08). These are rulings, not proposals:

1. **Unified place-to-build structures.** The crafting **table** (wood+stone), the **forge** (much more
   stone), and the **campfire** are all placed in the world via ONE flow: gather materials → place the
   structure → it appears. Structures are **INVISIBLE until placed** (no pre-visible marker). This
   generalises the Sponsor's forge-soak directive (`86camyvzw`: "the forge must NOT be visible before it
   is built — the player builds it by gathering the ingredients and PLACING it") to every structure.
2. **Crafting table = a recipe MENU.** Recipes grouped by tier; each greyed until its tier is unlocked
   AND the player can afford the materials; click-to-craft. RETIRES the pre-placed auto-craft stump
   (`CraftSpot`).
3. **Three tiers at the table: WOOD → STONE → IRON**, each with **axe / pickaxe / spear / dagger / sword**
   (~15 recipes). Codebase says "knife"; Sponsor says "dagger" — reconciled in §6.
4. **Tier-gated loop.** Hand-gather branches + loose stones → place table → craft WOOD tools → **WOOD
   pickaxe mines STONE from BOULDERS** → craft STONE tools → **STONE pickaxe mines IRON-ORE** (shipped I-2
   #287) → build FORGE, smelt → **iron BARS** (shipped I-3 #292) → craft IRON tools at the table.
5. **Reuse shipped work.** I-2 ore-mining (#287) + I-3 smelt (#292) mechanics stay; the forge gets
   reworked to the place-to-build (invisible-until-placed) flow + a much higher stone cost.
6. **New heavy pieces:** the placed-buildable system + the recipe-menu UI + the material-cost crafting
   seam + boulder-mining + **WOOD-TIER ART** (the shipped weapon models are stone+iron ONLY per
   `[[weapon-two-tier-style-stone-iron]]`; the wood tier needs NEW models).

---

## 1. Reconciliation with shipped `main` (verified this session against `origin/main` @ #292)

The grill design's code-map is close; three items diverge from what actually ships and MUST be honoured —
they change scope. Verified by reading the live sources, not inferred.

### 1a. Already ships (REUSE, do not rebuild)

| Concern | Ships as | Reuse in |
|---|---|---|
| **Hand-gather WOOD (branches)** | `StickProp.cs` (`86caa96rd`) — fallen stick, E-loot, yields 1 `wood`, no respawn; scattered via `LowPolyZoneGen.BuildStick` + fixed `BuildWiredStick`. **NOT new** — the brief's code-map calls branches "NEW"; they already ship. | Seq ① bootstrap = VERIFY, not build |
| **Hand-gather STONE (pebbles)** | `StoneProp.cs` (`86caa4c96`) — loose small stone, E-loot, yields 1 `stone`, respawns; `BuildStone`/`BuildWiredStone`. Its own doc calls boulders "the FUTURE pickaxe-MINING target — OOS here". | Seq ① bootstrap; boulders = seq ② |
| **Active-click mine verb** | `MineOre.cs` (#287) — `ShouldMineOnClick` pure guard truth-table, nearest-node resolver, impact-delayed single-flight strike, hold-to-repeat cadence, `OrePile`/`OrePileSpawner` E-loot drop, editor-authored node pool, live rarity dial. Gated on `IsPickaxeSelected` (stone OR iron). | Seq ② boulder-mining is a **`MineOre` sibling** |
| **Buildable structure idiom** | `CampfirePlacement.cs` / `ForgePlacement.cs` — proximity spot + all-or-nothing material gate (`Inventory.SpendWood`/`SpendStone`), `HasBuilt`, editor-authored into `Boot.unity`, ships unbuilt. `Forge.CanAfford` is a pure static. | Seq ①/③ place-to-build extends this seam |
| **Smelt chain** | `Forge.cs` + `SmeltRecipe.cs` + `IronDifficulty.cs` (#292) — build → auto-smelt-on-proximity, ore+fuel→ingot over a timer, two live smelt-cost dials. | Seq ③ reworks the FRONT (placement) only |
| **Item / weapon data seam** | `ItemCatalog` (`iron_ore`/`iron_ingot`/`pickaxe_stone`/`pickaxe_iron` + `axe`/`spear`/`wood`/`stone`), `Combat.WeaponCatalog` (axe/spear/pickaxe×2 WeaponDefs), `InventoryModel` (`AddItem`/`RemoveItem`/`CountItem`/`AddToolToBelt`/`IsSelectedBeltItem`/`OwnsItem`). | The craft seam extends THIS — no parallel model |

### 1b. Net-new (this redesign builds)

- **Placed-buildable "place-to-build" system** (invisible-until-placed; ghost + confirm) — replaces the
  pre-authored fixed spot for ALL three structures.
- **Recipe-menu UI** (UI Toolkit, grouped-by-tier, grey-until-unlocked-and-affordable, click-to-craft).
- **Material-cost crafting seam** — today `Inventory.CraftAxe()` just calls `PickUpAxe()` and **mints an
  axe for FREE** (no cost). NO material-cost recipe exists. The seam = debit inputs via `RemoveItem`
  (all-or-nothing, the `SpendWood` idiom) → grant the output tool/weapon via `AddToolToBelt`.
- **Boulder-mining** (wood-pickaxe → stone) — a `MineOre` sibling; boulders are decorative today
  (`RockVerifyCapture`).
- **WOOD-TIER ART** — 5 new wood-tier tool/weapon meshes (no wood FBX exists).
- **WOOD-tier + IRON-tier + missing-STONE-tier item/weapon ids** (see §6).

### 1c. Retires

- **`CraftSpot.cs`** (auto-craft stump — one free recipe) → replaced by the recipe-menu table.
- **The free-acquisition weapon paths** — `AxePickup` / `SpearPickup` / `PickaxePickup` world pickups +
  `Inventory.CraftAxe` free-mint + the `HeldWeaponCycleDebug` [B]-cycle knife/sword — are **reworked into
  table recipes** (seq ②). (`HeldWeaponCycleDebug` stays as a DEV look-soak aid; it stops being the only
  way to see a knife/sword once daggers/swords are craftable.) The world pickups may survive as an early
  "find-in-world" alternative if the Sponsor wants — flag at soak; the DEFAULT is retire → craft.

---

## 2. The unified place-to-build flow (invisible until placed)

**Sponsor-locked (`86camz6n0` §1 + `86camyvzw`):** structures are invisible until the player places them;
gather materials → place → it appears. This is the load-bearing NEW system of seq ①.

**The flow (all three structures — table, forge, campfire):**

1. The player has a placeable structure available (see the placement-trigger recommendation in §7-A —
   my recommended default is a **build menu** entry per structure, greyed until affordable).
2. Selecting it enters **PLACEMENT MODE**: a translucent GHOST of the structure tracks the ground in
   front of the player (or under the cursor); a validity read (flat-enough ground, not overlapping water
   / another structure / the player) tints the ghost valid/invalid.
3. A **confirm** input places the structure: debit the materials all-or-nothing (`SpendWood`/`SpendStone`
   idiom), instantiate the structure at the ghost pose. A **cancel** input exits placement with no debit.
4. The structure now exists in the world (the forge then smelts, the table then opens its menu, the
   campfire then lights).

**Why this retires the fixed-spot model:** `CampfirePlacement`/`ForgePlacement` today author an unbuilt
structure at a FIXED editor-time spot and raise it on proximity. The Sponsor rejected that for the forge
("must NOT be visible before it is built"). The unified flow removes the pre-authored spot entirely —
there is no marker to see before placement.

**Regression boundary:** the placement act still ends in the SAME data seam the current builds use
(`Inventory.SpendWood`/`SpendStone` → `Campfire.Light` / `Forge.Build`), so the smelt runtime + warmth
binding are untouched. Only the FRONT (how the structure comes to exist + where) changes.

---

## 3. The crafting-table recipe-menu UX

**Destination:** walking near a placed crafting table opens a **recipe menu** — a UI-Toolkit panel (the
`InventoryUI` / settings-panel family) — showing the tools/weapons the player can build, grouped by tier.

- **Grouping:** three tier sections (WOOD / STONE / IRON), each with its 5 tool rows (axe / pickaxe /
  spear / dagger / sword).
- **State per row:**
  - **Craftable** — the tier is unlocked AND the player can afford the materials → the row is active;
    click crafts it (debit inputs, grant the tool to the belt/inventory).
  - **Unaffordable** — tier unlocked but materials short → greyed, shows the cost + what's missing.
  - **Locked** — tier not yet unlocked → greyed / shows the unlock hint (so the player sees the ladder).
- **Interaction:** open on proximity (or an interact key at the table); click-to-craft; no drag. Closing
  is symmetric with the inventory panel (the `UiInputGate` world-input guard applies so a click on the
  menu never leaks to a world verb).
- **Tier-unlock model (proposed default — §7-C, tunable):** WOOD unlocks when the table is placed; STONE
  unlocks once the player has ever obtained a **wood pickaxe** (the tool that gates boulder-stone); IRON
  unlocks once the player has ever obtained an **iron ingot**.

The menu is authored in seq ① with the WOOD tier live; seq ② adds STONE rows, seq ③ adds IRON rows. The
menu is a data layer over the recipe set — it does not care whether an output id is tiered or not (§6).

---

## 4. The tier-gated loop + bootstrap

```
 hand-gather (EXISTS):  sticks → wood ·  loose pebbles → stone
        │
        ▼   place TABLE (wood + stone)  ── recipe menu opens
   WOOD tier craftable ───────────────────────────────────────┐
        │  craft WOOD PICKAXE (all-wood)                        │
        ▼                                                       │
   WOOD-pick mines STONE from BOULDERS (NEW — MineOre sibling)  │  (bootstrap pebbles cover the
        │  volume stone                                         │   table + first wood tools;
        ▼                                                       │   boulders are the VOLUME source)
   STONE tier craftable ──────────────────────────────────────┘
        │  craft STONE PICKAXE (knapped stone + wood)
        ▼
   STONE-pick mines IRON-ORE (SHIPPED — MineOre #287)
        │  iron ore
        ▼   place FORGE (much more stone) → smelt (SHIPPED #292)
   iron BARS
        ▼
   IRON tier craftable ── craft IRON tools at the table
```

**Bootstrap coherence (load-bearing):** the loop needs enough hand-gathered wood (sticks) AND stone
(pebbles) to afford the FIRST table + the first WOOD tools before any pickaxe-mining is possible. So loose
sticks + pebbles are **KEPT** (not retired) as the bootstrap; boulders (via wood-pick) become the STONE
**volume** source. The vision's "first axe using wood and stone" maps to the **STONE tier** (knapped stone
head + wood haft) — the WOOD tier is a NEW cruder pre-stone rung the grill added (all-wood tools, whose
job is to let you mine your first boulders).

---

## 5. Per-tier recipe list + proposed material costs

All costs are **🎚️ predictions** — flag each `default X — Sponsor-soak tunes`; the Predict-Before-Soak
gate grades the author's prediction against the soak. Inputs use the existing resources: `wood`, `stone`,
`iron_ingot` (all `ItemKind.Resource`, debited via `RemoveItem`).

### Structures (place-to-build)

| Structure | Proposed cost | Note |
|---|---|---|
| Crafting table | 5 wood + 3 stone | bootstrap-affordable from hand-gather |
| Campfire | 3 wood | current `CampfirePlacement.woodCost`, unchanged |
| Forge | 6 wood + **12 stone** | "forge >> weapons" — much more stone than the current 4w+5s; a stone furnace |

### Tools / weapons (15 recipes = 3 tiers × 5 tools)

| Tool | WOOD tier | STONE tier | IRON tier |
|---|---|---|---|
| **Axe** | 3 wood | 3 wood + 3 stone | 2 wood + 3 iron_ingot |
| **Pickaxe** | 3 wood | 3 wood + 3 stone | 2 wood + 3 iron_ingot |
| **Spear** | 2 wood | 2 wood + 2 stone | 2 wood + 2 iron_ingot |
| **Dagger** | 2 wood | 2 wood + 2 stone | 1 wood + 2 iron_ingot |
| **Sword** | 4 wood | 4 wood + 4 stone | 2 wood + 4 iron_ingot |

Monotonic by design (iron costs the scarce ingot; stone gates on boulder-stone; wood is the cheap
bootstrap). The forge cost dwarfs any single weapon so the iron tier feels *earned*.

**Per-tier capability + stats (proposed — §7-B, tunable):**
- **Mining capability gate (mechanical, NOT just cosmetic):** WOOD pickaxe mines boulders→stone only;
  STONE pickaxe mines iron-ore (+ stone); IRON pickaxe = fastest (fewer strikes) + future-proof. This is
  the real tier gate — reuse `MineOre`'s pickaxe-selected check, widened per tier.
- **Combat stats:** tiers differ by the EXISTING `WeaponDef` numbers (`damage`/`reach`/`attackSpeed`) —
  wood weakest → iron best. Seed wood ≈ 0.7× stone, iron ≈ 1.3× stone (predict; soak dials).
- **Durability:** NO durability system in v1 (none exists in the codebase; adding one is a separate
  feature). Flagged as a future consideration, NOT in this re-scope.

---

## 6. Naming reconciliation

### 6a. "knife" → "dagger" (Sponsor)

Adopt **"dagger"** as the item/weapon id + display name + team vocabulary going forward. **Keep the FBX
filenames `wpn_knife_*_01`** as-is (an asset rename is pure churn with no gameplay benefit); wire them to
the `dagger_*` ids with a one-line mapping note. Update `HeldWeaponCycleDebug`'s comment/label from
"knife" to "dagger" opportunistically (not a blocking rename).

### 6b. The tiered item-id scheme (the real integration hazard — see §7-D)

Ground truth: the shipped combat ids are **UN-tiered** (`"axe"`, `"spear"`) and are already the *stone
tier* (knapped stone + wood, per `[[weapon-two-tier-style-stone-iron]]`). The pickaxe IS tiered
(`pickaxe_stone` / `pickaxe_iron`). Knife + sword are **debug meshes with NO ItemDef/WeaponDef**. No wood
tier exists at all.

**Recommended scheme (lowest-regression):** the recipe is a **data row carrying an explicit
`outputItemId`**; the menu maps a (tier, tool) cell → whatever the live id is. Keep the shipped ids stable
(`"axe"`/`"spear"` REMAIN the stone-tier ids — do NOT migrate them, or you break the chop gate, held-axe,
pickups, and the combat POC). Mint NEW ids only for the missing cells:
- WOOD tier (all 5 new): `axe_wood`, `pickaxe_wood`, `spear_wood`, `dagger_wood`, `sword_wood`.
- STONE tier: `"axe"` + `"spear"` (exist) + `pickaxe_stone` (exists) + NEW `dagger_stone`, `sword_stone`.
- IRON tier: `pickaxe_iron` (exists) + NEW `axe_iron`, `spear_iron`, `dagger_iron`, `sword_iron`.

This is a deliberately MIXED scheme (un-tiered stone anchors + tiered wood/iron). The alternative — a clean
`<tool>_<tier>` migration — is rejected as too high a regression surface for the shipped `"axe"`/`"spear"`.
**This is an ADVISEMENT item (§7-D):** the mixed scheme works but is the kind of vocabulary call the grill
design didn't resolve; if the Sponsor/orchestrator prefer the clean migration, that's a bigger seq-①
sub-task and I'll re-scope.

---

## 7. Open design calls — recommendations (ADVISEMENT NEEDED)

The grill design (`86camz6n0`) resolved the WHAT; these four calls it did not cover. I PROPOSE a default
for each (baked into the drafts below) and flag them for the orchestrator/Sponsor to confirm or overturn.

- **A — Placement trigger + mechanic (free-cursor vs fixed-spot).** "Invisible until placed" is
  Sponsor-LOCKED (`86camyvzw`). The remaining call is HOW the player places. **My rec:** a free-cursor
  **ghost + confirm** build mode (ghost tracks the ground, validity tint, confirm to place, cancel to
  exit), entered from a build-menu entry per affordable structure. Alternative (cheaper): keep a
  proximity fixed-spot but hide the structure until built — rejected because it doesn't match "the player
  places it." Confidence: moderate — this is a feel call the Sponsor may want to soak.
- **B — Per-tier stat/capability/durability deltas.** **My rec:** tiers gate MINING capability
  (wood→stone-boulders, stone→iron-ore) + scale `WeaponDef` combat stats (wood 0.7× / iron 1.3× stone);
  **no durability in v1**. All numeric deltas are tunable predictions.
- **C — Tier-unlock model.** **My rec:** WOOD unlocks on table-placed; STONE on first-wood-pickaxe-owned;
  IRON on first-iron-ingot-owned. Alternative: pure-affordability (no separate unlock — a tier greys only
  on cost). Confidence: moderate; either is cheap.
- **D — The mixed vs clean tiered-id scheme (§6b).** **My rec:** mixed scheme + explicit-`outputItemId`
  recipe rows (keep shipped `"axe"`/`"spear"` stable). Overturn → a clean `<tool>_<tier>` migration is a
  bigger seq-① sub-task with a real regression surface (chop gate / held-axe / pickups / combat POC).

None of these block drafting — the drafts carry my recommended defaults as 🎚️ tunables, so a competent
dev can start; a Sponsor overturn is a one-field edit to the relevant draft.

---

## 8. Sequencing & dispatch order

Build-slot reality (`[[single-unity-build-slot-serializes-orchestration]]`): every seq below is a
**build-lane** ticket (touches `Boot.unity` + needs a Unity build) → they **serialize** on the ONE build
slot. The non-build lane (this spec, the wood-tier art R&D burst, reviews) runs in parallel.

```
 ① Table foundation ──→ ② Boulder-mining + STONE tier ──→ ③ Forge rework + IRON tier ──→ ④ Full-chain soak
 (placed-build + menu +      (MineOre sibling +                (place-to-build forge +          (WOOD→STONE→IRON
  material-cost seam +        stone-tier recipes;               iron-tier recipes; absorbs        end-to-end +
  WOOD tier + wood art)       reworks world-pickups)            I-4, forge-vis, NITs)             3-tier A/B)
```

- **① is the foundation** — the placed-buildable system + recipe menu + material-cost seam are the new
  systems everything else consumes. Nothing after it can start until it merges.
- **Wood-tier ART** runs as a **parallel R&D burst** (orchestrator lane — Sponsor-judged Blender, like the
  #254/#283 weapon bursts) alongside ①; ① dev's against placeholder wood meshes until the art lands.
- **② then ③** serialize behind ① on the build slot. **④** is last (needs the full chain in one build).

---

## 9. The ticket-DRAFT table (orchestrator creates on `901523878268`)

Four dispatch-ready drafts. ACs use the Commander's-Intent 3-bucket shape. Each is written so an author
picks it up without a clarifying question. Work-type tag drives the acceptance gates
(`dispatch-template.md` §Work-type tag).

---

### DRAFT ① — Crafting-table foundation: placed-buildable + recipe menu + material-cost seam + WOOD tier

**Title:** `feat(gameplay): crafting-table redesign — place-to-build + recipe-menu + material-cost crafting + WOOD tier [L]`
**Tags:** `feat`, `gameplay` · **Status:** `to do` · **Owner:** Devon (engine/UI + new system) · **Reviewer:** Drew
**Size:** L · **Deps:** none (build-lane; serialize on the build slot) · **Blocks:** ②, ③, ④

**🎯 Destination:** the castaway gathers wood + stone, **places a crafting table** (invisible until placed),
walks up to it to open a **recipe MENU**, and crafts **WOOD-tier** tools/weapons by spending materials —
the FIRST material-cost crafting in the game, replacing the free auto-craft stump. *Strip-test:* the
Sponsor hand-gathers sticks + pebbles, places a table where he chooses, opens its menu, and clicks to
craft a wood axe/pickaxe that costs wood — and the old walk-to-the-stump free axe is gone.
**Relevant bars:** Bar 4 (the table reads as a real crafting table on the first try — anchor sentence +
side-profile capture), Bar 3 (wood-tier tools read as their material — sharpened wood, faceted, no
texture), Bar 5 (wood tools judged in-hand via the picker).

**🔒 Constraints (must obey):**
- **Retire `CraftSpot`** (the auto-craft stump) + its `BuildCraftSpot` scene authoring — the table
  replaces it. *Why:* the design RETIRES the free one-recipe stump; leaving it ships two craft entries.
- **Place-to-build is invisible-until-placed** (§2) — NO pre-authored visible marker/spot. The placement
  ends in the existing `Inventory.SpendWood`/`SpendStone` all-or-nothing debit seam. *Why:* Sponsor-locked
  (`86camyvzw`); reuse the proven debit seam, don't fork it.
- **Material-cost craft seam = debit via `RemoveItem` (all-or-nothing) → grant via `AddToolToBelt`** on
  `InventoryModel` — do NOT build a parallel inventory/recipe model, and do NOT extend the free-mint
  `CraftAxe` path. A recipe is a **data row with an explicit `outputItemId`** (§6b). *Why:* the item-model
  is the one data seam; `CraftAxe` mints free and is being retired.
- **Recipe menu is a UI-Toolkit panel in the `InventoryUI`/settings-panel family**, gated by the
  `UiInputGate` world-input guard so a menu click never leaks to a world verb. *Why:* the shipped UI idiom
  + the click-guard invariant (`[[active-input-not-proximity-auto-for-actions]]`).
- **WOOD-tier ids are NEW** (`axe_wood`/`pickaxe_wood`/`spear_wood`/`dagger_wood`/`sword_wood`); keep the
  shipped `"axe"`/`"spear"`/`pickaxe_stone` ids STABLE (§6b — do NOT migrate them). Adopt **"dagger"** id +
  display; reuse the `wpn_knife_*` FBX (§6a). *Why:* migrating shipped ids breaks the chop gate / held-axe
  / pickups / combat POC.
- **Structures + menu authored editor-time into `Boot.unity` + regen-and-commit** — NOT built at Awake
  (the legs-up class); a code-only PR ships the stale scene. *Why:* `unity-conventions.md §editor-vs-runtime`
  + `[[unity-procedural-committed-assets-go-stale]]`.
- **Do NOT touch the seed-42 scatter / start-island world gen** beyond removing the CraftSpot object.
  *Why:* `[[world-is-big-round-island]]` LOCKED.
- **Do NOT wire STONE/IRON recipes live** — their rows show as **Locked** (greyed) placeholders; ②/③ flip
  them live. *Why:* their material sources (boulder-stone, ingots) don't exist yet.

**🎚️ Defaults (tunable — predict, don't mandate; flag `default X — Sponsor-soak tunes`):** table cost 5
wood + 3 stone; WOOD recipe costs per §5; the placement mechanic (§7-A rec: ghost + confirm); the
tier-unlock model (§7-C rec: WOOD unlocks on table-placed). Wood in-hand seat via `HeldWeaponPlacement`.

**Route:** the placement mechanic + menu layout are the dev's to build inside the constraints; §7-A/C are
recommended defaults, not mandates.

**OOS:** boulder-mining + STONE recipes (②); forge + IRON recipes (③); the wood-tier ART meshes (parallel
R&D burst — ① dev's against placeholders); durability; the world-pickup rework (②).

**Files in play:** NEW `CraftingTable.cs` / `CraftingTablePlacement.cs` / recipe-menu UI + a `Recipe` data
type; `Inventory.cs`/`InventoryModel.cs` (craft seam — extend, don't fork); `ItemCatalog.cs` +
`Combat/WeaponCatalog.cs` (wood-tier ids); `MovementCameraScene.cs`/`BootstrapProject.cs` (retire
`BuildCraftSpot`, author the table); `Boot.unity` (regen). DELETE `CraftSpot.cs` + `CraftVerifyCapture` (or
repoint).

**Success tests:** EditMode — a recipe debits inputs all-or-nothing (short mats → no craft, no debit) +
grants the output; the tier-unlock truth-table; wood-tier ids resolve in both catalogs. PlayMode/interaction
— place table → open menu → craft wood axe → it lands on the belt; a click on the menu is swallowed (no
world verb). Scene-presence guard (a `CampfireSceneTests` sibling): the table + menu refs serialize.
Shipped-build capture (interaction-soak gate — `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`)
+ side-profile capture of the table (Bar 4). **Predict-Before-Soak** + bounded-convergence line.
**Regression guard:** a test that reds if `CraftAxe`'s retirement strands a caller (chop/held-axe still green).

---

### DRAFT ② — Boulder-mining (wood-pick → stone) + STONE-tier recipes

**Title:** `feat(gameplay): boulder-mining (wood pickaxe → stone) + STONE-tier table recipes [L]`
**Tags:** `feat`, `gameplay` · **Status:** `to do` · **Owner:** Drew (game content + mine sibling) · **Reviewer:** Devon
**Size:** L · **Deps:** ① on main (menu + material-cost seam) · **Build-lane** · **Blocks:** ③, ④

**🎯 Destination:** with a **wood pickaxe** selected, the castaway mines **stone from BOULDERS** (active
left-click, like ore/chop) — the VOLUME stone source beyond hand-gathered pebbles — then crafts the
**STONE tier** at the table. The shipped world-pickup weapons (axe/spear/pickaxe) become **table recipes**.
*Strip-test:* the Sponsor, holding a wood pickaxe, clicks a boulder several times until it drops a stone
pile, loots it, and crafts a stone axe/pickaxe at the table — stone is now *earned by mining*, not just
found as pebbles. **Relevant bars:** Bar 3 (the boulder reads as rock; stone tools read as knapped stone),
Bar 4 (a boulder looks like a real rock outcrop — anchor sentence + side-profile), Bar 2 (the mine swing
is lively), Bar 1 (organic boulder placement — no grid).

**🔒 Constraints:**
- **Boulder-mining is a `MineOre` SIBLING — reuse it, do NOT invent a new verb.** Same
  `ShouldMineOnClick` pure guard + three world-click guards + impact-delayed single-flight strike +
  hold-to-repeat cadence + nearest-node resolver + `OrePile`/`OrePileSpawner`-style loot drop (a
  **stone-pile** dropping `stone`). *Why:* `[[active-input-not-proximity-auto-for-actions]]` + the mine
  verb is already proven; a parallel path is the documented failure.
- **Gate on the WOOD pickaxe selected** (`axe_wood`/`pickaxe_wood` scheme — the `pickaxe_wood` id) — the
  mining analog of `IsPickaxeSelected`, widened for the wood tier. *Why:* the tool-gated verb pattern is
  the tier gate.
- **Boulders authored editor-time into `Boot.unity` + regen-and-commit**; boulders are decorative today
  (`RockVerifyCapture`) — make a discrete mineable-boulder pool, do NOT mutate the seed-42 scatter RNG
  stream. *Why:* editor-vs-runtime + `[[world-is-big-round-island]]` + committed-asset staleness.
- **KEEP loose pebbles (`StoneProp`) as the bootstrap** — do NOT retire them; boulders are the VOLUME
  source, pebbles bootstrap the table + first wood tools (§4). *Why:* the loop can't start without a
  bootstrap stone source.
- **Rework the world-pickups into STONE recipes** — `AxePickup`/`SpearPickup`/`PickaxePickup` become
  craftable rows (`"axe"`/`"spear"`/`pickaxe_stone` + NEW `dagger_stone`/`sword_stone`). Default = retire
  the world pickups; flag "keep as find-in-world alt?" at soak. *Why:* the design routes acquisition
  through the table; keep shipped stone ids stable (§6b).
- **Flip the STONE tier's menu rows LIVE** (unlock model §7-C: stone unlocks on first-wood-pickaxe-owned).
  *Why:* ① left them as Locked placeholders.

**🎚️ Defaults:** strikes-to-break a boulder (default ~4, mirror `MineOre.strikesToBreak` band 1–10);
stone yielded per boulder; boulder count/density; regrow window `[min,max]` (organic, like ore nodes);
STONE recipe costs per §5. Predict against Bar 7 (three tiers) where difficulty applies.

**OOS:** the forge + IRON tier (③); the WOOD tier (①); combat balance; durability.

**Files in play:** NEW `MineBoulder`/stone-node reuse of `MineOre` + a `StonePile`/`StonePileSpawner`
(sibling of `OrePile`); STONE recipe rows in the menu; `ItemCatalog`/`WeaponCatalog` (`dagger_stone`/
`sword_stone`, `pickaxe_wood` gate); `MovementCameraScene` (author boulders, retire pickups); `Boot.unity`
(regen).

**Success tests:** EditMode — `ShouldMineOnClick` boulder truth-table (wood-pickaxe-selected × in-range ×
guards); STONE recipes debit/grant. PlayMode — wood-pick mines a boulder → stone pile → E-loot stone →
craft stone axe. Scene-presence guard for boulders. Shipped-build capture + side-profile (Bar 4).
Predict-Before-Soak + bounded-convergence. **Regression guard:** iron-ore mining (`MineOre` #287) stays
green (the boulder sibling doesn't regress it).

---

### DRAFT ③ — Forge rework (place-to-build) + IRON-tier recipes (ABSORBS I-4, forge-visibility, NITs)

**Title:** `feat(gameplay): forge place-to-build rework + IRON-tier table recipes [L]`
**Tags:** `feat`, `gameplay` · **Status:** `to do` · **Owner:** Devon · **Reviewer:** Drew
**Size:** L · **Deps:** ① (menu + place-to-build) + ② (stone tier + stone-pickaxe → ore) on main · **Build-lane** · **Blocks:** ④
**Absorbs:** `86cakkmy2` (I-4 iron craft unlock) · `86camyvzw` (forge invisible-until-placed) · `86camw8rm` (Forge GC.Alloc NIT)

**🎯 Destination:** the forge becomes a **place-to-build** structure (invisible until placed, per the
unified flow) instead of a pre-visible fixed spot, and with iron ingots in hand the player crafts the
**IRON tier** at the table — the shipped iron FBX set (#254/#283) goes LIVE as the wielded upgrade. This
closes the chain: mine → smelt → **forge the upgrade at the table**. *Strip-test:* the Sponsor gathers a
lot of stone, PLACES a forge where he chooses (nothing was there before), smelts ore→ingots, and crafts an
iron axe/pickaxe/sword at the table — iron is earned and wielded. **Relevant bars:** Bar 4 (the placed
forge reads as a real stone furnace — anchor + side-profile), Bar 3 (iron reads as forged iron in-hand),
Bar 2 (the working forge has life — glow while smelting), Bar 5 (iron in-hand via the picker), Bar 7 (three
smelt-cost tiers).

**🔒 Constraints:**
- **Forge uses the unified place-to-build flow (§2)** — remove the pre-visible forge + `ForgePlacement`
  fixed spot; the player places it (invisible until placed). REUSE the ① placement system. *Why:*
  Sponsor-locked (`86camyvzw`).
- **Keep the shipped smelt runtime** (`Forge.cs` build→smelt, `SmeltRecipe`, the two live smelt-cost dials,
  the test clock seam) — rework only the FRONT (placement). *Why:* #292 shipped + is soaked; don't
  re-litigate the smelt loop.
- **Forge cost = much more stone** than the current 4w+5s (default 6w + 12s, §5). *Why:* "forge >>
  weapons" (`86camz6n0`).
- **IRON recipes at the TABLE** (not a forge-side bench) — add IRON rows to the ① menu, gated on
  `iron_ingot` (all-or-nothing debit). IRON ids: `pickaxe_iron` (exists) + NEW `axe_iron`/`spear_iron`/
  `dagger_iron`/`sword_iron` (§6b); wire the shipped iron FBXs. *Why:* the design crafts every tier at the
  ONE table; keep the id scheme consistent.
- **Fold the #292 NIT** (`86camw8rm`): cache `Forge.CurrentRecipe` (per-frame `GC.Alloc` in `Update`) or
  make it non-allocating; fix the doc drift. *Why:* unity6-mastery §5 no-per-frame-alloc; it's in Forge.cs
  which this PR touches.
- **Resolve the smelt-feed feel** (`86camyvzw` open question: auto-begin-on-proximity vs explicit-feed):
  DEFAULT keep the shipped auto-tend station model for v1; surface explicit-feed as a soak question (the
  Sponsor's place-to-build direction leans explicit). *Why:* don't over-scope; let the soak decide.
- **Editor-time authoring + regen-and-commit; peaceful (no combat dep).** *Why:* standing rules.

**🎚️ Defaults:** forge cost 6w+12s; IRON recipe costs per §5; the smelt-feed model (rec: keep auto-tend);
iron in-hand seat via `HeldWeaponPlacement`. Confirm BOTH difficulty dials (ore-rarity ② source +
smelt-cost) live end-to-end across easy/med/hard.

**OOS:** the ore/stone mining verbs (②); new smelt mechanics beyond placement; find-in-world iron;
distinct ore-vs-ingot ICONS (`86camyvwn` stays a separate fable session); durability.

**Files in play:** `Forge.cs`/`ForgePlacement.cs` (place-to-build rework + NIT); IRON recipe rows;
`ItemCatalog`/`WeaponCatalog` (iron ids); `MovementCameraScene.BuildForge` (retire fixed spot); `Boot.unity`
(regen).

**Success tests:** EditMode — IRON recipes debit ingots all-or-nothing/grant; `CurrentRecipe` no longer
per-frame allocs (a `GC.Alloc` assertion). PlayMode — place forge → smelt → craft iron axe → equip.
Scene-presence guard. Shipped-build capture + side-profile of the placed forge (Bar 4) + iron in-hand
picker (Bar 5). Predict-Before-Soak + bounded-convergence. **Regression guard:** the shipped smelt timer
(#292) stays green.

---

### DRAFT ④ — Full-chain soak + capture-gate consolidation (ABSORBS I-5)

**Title:** `test(gameplay): crafting chain end-to-end soak — gather→table→wood→stone→iron + 3-tier A/B [M]`
**Tags:** `test`, `gameplay`, `sponsor-gate` · **Status:** `to do` · **Owner:** Tess · **Reviewer:** Drew or Devon (by surface)
**Size:** M · **Deps:** ③ on main · **Build-lane** (the soak build) · **Blocks:** none (the gate)
**Absorbs:** `86cakkn15` (I-5 iron chain soak — now the WHOLE WOOD→STONE→IRON chain)

**🎯 Destination:** ONE shipped soak build that lets the Sponsor play the **whole crafting chain end to
end** — hand-gather → place a table → craft wood tools → mine boulders for stone → craft stone tools → mine
ore → place a forge → smelt → craft + equip an iron tool — and judge it as a whole, PLUS a 3-tier A/B
(easy/med/hard presets of the ore-rarity + smelt-cost dials). *Strip-test:* the Sponsor plays one build and
the full "gather → craft → upgrade through three tiers" loop is live, findable, and every judged step is
live-triggerable — no placeholder gaps.

**🔒 Constraints:**
- **Every judged step FINDABLE + live-triggerable** in the served build; agree any descope BEFORE the
  build. *Why:* `[[sponsor-rejects-unsoakable-placeholders]]`.
- **Serve from `Build\soak-<N>\`** with the exact exe path + expected HUD build stamp + a "test THIS"
  checklist; **PLAY the exe** at real framing before serving. *Why:*
  `[[soak-handoff-path-and-explicit-test-checklist]]` + `[[soak-builds-go-in-project-build-folder]]` +
  `[[served-unverified-soaks-need-played-verification]]`.
- **Consolidate the per-ticket VerifyCaptures** — table, boulder, forge, wood/stone/iron in-hand — all
  green from the BUILT exe. Carry a **Predict-Before-Soak** line + a **bounded-convergence claim** (bars
  tested: 1,2,3,4,5,7; bars NOT tested). *Why:* the testing bar for feel/soak PRs.

**🎚️ Defaults:** the three difficulty presets are the A/B subject (the dialed values from ②/③ soaks bake).

**OOS:** new features; combat; find-in-world; icons (`86camyvwn`).

**Files in play:** capture consolidation + a soak checklist; no new gameplay.

**Success tests / gate:** the full chain is playable in one shipped build; Tess QA PASS + Sponsor soak on
the chain + the 3-tier A/B; all VerifyCaptures green.

---

## 10. Ticket reconciliation (supersede / absorb / keep)

| Existing ticket | Disposition |
|---|---|
| `86cakkmy2` — I-4 iron crafting unlock (`to do`) | **ABSORBED into ③** — iron crafting now happens at the redesigned table, not the thin bench. Close as superseded-by-③. |
| `86cakkn15` — I-5 iron chain soak (`to do`, `sponsor-gate`) | **ABSORBED into ④** — ④ is the WHOLE chain soak (wood→stone→iron), a superset. Close as superseded-by-④. |
| `86camyvzw` — forge invisible-until-placed (`to do`) | **ABSORBED into ③** — the forge place-to-build rework IS ③'s front-half. Close as superseded-by-③. |
| `86camw8rm` — Forge `CurrentRecipe` GC.Alloc NIT (`to do`) | **FOLDED into ③** — ③ touches `Forge.cs`; fold the perf + doc fix. Close as folded-into-③. |
| `86camyvwn` — distinct ore-vs-ingot icons (`to do`, `design`) | **KEEP SEPARATE** — a Sponsor-interactive fable icon-design session, no dep beyond I-0 (on main). NOT in this re-scope. |
| `iron-model-a-spec.md` (merged #268) | **SUPERSEDED on the crafting-table question** (§header) — its mine/smelt mechanics REUSED; its "extend thin CraftSpot" assumption retired. |

(Priya does NOT write ClickUp — the orchestrator flips these statuses + creates ①–④ in its tool round.)

---

## 11. Test / capture gates (cross-cutting, per `team/TESTING_BAR.md`)

Applies to every build ticket (①–④):

- **Paired EditMode + PlayMode/interaction tests** — pure-logic guards (recipe debit/grant truth-tables,
  mine guard tables, unlock tables) in EditMode; end-to-end verbs in PlayMode, with the interaction-soak
  as the real gate where the PlayMode job is advisory-unreliable
  (`[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`).
- **Scene-presence guards** — every editor-authored object (table, boulder pool, placed forge) gets a
  `CampfireSceneTests`/`ForgeSceneTests`-sibling guard that it + its refs serialize into `Boot.unity`.
- **Shipped-build capture gate** — every visual surface (table, boulder, forge, wood/stone/iron in-hand)
  ships evidence from the BUILT exe + side-profile captures for Bar 4 physical features.
- **Self-Test Report** before Tess reviews each UX-visible PR.
- **Predict-Before-Soak + bounded-convergence** on every feel/look PR.
- **Committed-generated-assets** — regen + commit or the build ships the stale snapshot.
- **No mutable statics** (or a `[RuntimeInitializeOnLoadMethod]` reset), per `StaticStateResetTests`.

---

## 12. Decision drafts (batched into `team/DECISIONS.md`; the entry is added in this PR per the dispatch brief)

- **Decision draft:** crafting system redesigned to a **unified place-to-build flow** (table + forge +
  campfire, invisible until placed) + a **recipe-MENU crafting table** (3 tiers WOOD→STONE→IRON × axe/
  pickaxe/spear/dagger/sword, greyed-until-unlocked-and-affordable, click-to-craft) with a **material-cost
  craft seam** (`RemoveItem`→`AddToolToBelt`; retires the free-mint `CraftAxe` + the `CraftSpot` auto-craft
  stump). Re-scoped into 4 build-lane tickets ①–④; absorbs I-4 `86cakkmy2` + forge-vis `86camyvzw` + NIT
  `86camw8rm` (→③) and I-5 `86cakkn15` (→④); icons `86camyvwn` stays a separate fable follow-up. Source:
  ticket `86camz6n0` (grill-resolved, Sponsor + orchestrator 2026-07-08).
- **Decision draft:** the shipped I-2 mine (#287) + I-3 smelt (#292) mechanics are REUSED unchanged;
  boulder-stone-mining is a `MineOre` sibling; the hand-gather bootstrap (`StickProp`/`StoneProp`) already
  ships and is KEPT as bootstrap (boulders = the stone VOLUME source). Model-A's "extend the thin CraftSpot
  bench" assumption is SUPERSEDED.
- **Decision draft (recommended, ADVISEMENT-flagged §7):** tiered item-id scheme is MIXED — shipped
  `"axe"`/`"spear"`/`pickaxe_stone` ids stay STABLE (they ARE the stone tier); NEW ids only for wood/iron
  cells + `dagger`/`sword`; recipes carry an explicit `outputItemId`. "knife" → "dagger" id+display; FBX
  filenames unchanged.
