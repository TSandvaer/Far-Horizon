# Iron progression — Model A implementation spec (Priya, 2026-07-06)

> **Status:** dispatch-ready ticket split for orchestrator → ClickUp create on list `901523878268`.
> Priya can't reach ClickUp — the orchestrator creates the parent + children and applies tags.
> **Ticket:** `86cakkg04` (this spec). **Design source (LOCKED):** the Sponsor's Q1–Q5 + BONUS
> rulings in `team/DECISIONS.md` 2026-07-06 (the six "Iron progression …" entries), grounded on
> `team/erik-consult/iron-progression-design-options.md` **§ Model A** (committed in this PR — the
> citation-source rule). This doc is the destination-shaping spec; the dev picks the mechanism
> inside the constraints. Shape mirrors `island-2.0-ticket-draft.md` (the ratified 3-bucket form).

---

## 0. The locked design (do not re-litigate — these are Sponsor rulings)

From DECISIONS.md 2026-07-06:

- **Model A** — the full mine-ore + smelter chain (Sponsor picked it over Erik's Model-D
  recommendation). Ore nodes → mine → carry ore → smelt at a furnace → iron ingots → craft iron.
- **Work-led earn feel** — the crafting/smelting **grind** is the point; ore is *findable without
  heavy exploration*. Effort lives in mining volume + fuel + smelt time, NOT in a hunt.
- **NEW forge/furnace buildable** — a distinct structure (NOT the bonfire), crafting-table-class,
  extending the survival arc: shipwreck → … → **furnace** → iron.
- **Peaceful** — NO combat guard. The whole iron chain ships **independent of the combat cluster**
  (no hard dep on `86cabcdpn`/snake/boar). Mining is safe.
- **Both difficulty dials** — **ore rarity** + **smelt cost** (fuel/time/material), each with
  per-tier easy/med/hard presets, registered in SettingsCatalog per the existing tweakable pattern.
- **Pickaxe = the 5th tool type, both tiers** — `wpn_pickaxe_stone_01` + `wpn_pickaxe_iron_01`,
  authored via a Sponsor-judged Blender burst, per `[[weapon-two-tier-style-stone-iron]]`.
- **`86cah7y5b`** (find-in-world weapon acquisition) stays a **standalone** feature — NOT absorbed
  into iron progression.

**North-star reminder:** iron is *the upgrade* — the visual language already says so
(`[[weapon-two-tier-style-stone-iron]]`: knapped stone is the first-craft, forged iron is the
reward). The progression's job is to make earning it feel **deserved** through work, not luck.

---

## 1. Shared-concept vocabulary contract (READ FIRST — parallel-dispatch discipline)

Multiple tickets below touch the SAME new concepts. To keep the parallel PRs mergeable, these
identifiers are **prescribed** (not left to each dev to invent). Ticket **I-0 is the type-author**
(Pattern A — it mints these FIRST, merges, then the consumers build against merged-on-main names).
Where I-0 hasn't landed, a consumer that must reference one uses the exact string below.

| Concept | Prescribed identifier | Owner / export site |
|---|---|---|
| Raw iron-ore item id | `IronOreId = "iron_ore"` | `ItemCatalog` (I-0) |
| Iron-ingot item id | `IronIngotId = "iron_ingot"` | `ItemCatalog` (I-0) |
| Stone pickaxe id | `PickaxeStoneId = "pickaxe_stone"` | `ItemCatalog` + `WeaponCatalog` (I-0) |
| Iron pickaxe id | `PickaxeIronId = "pickaxe_iron"` | `ItemCatalog` + `WeaponCatalog` (I-0) |
| Smelt-recipe data type | `SmeltRecipe` (input id + count → output id + count + fuel + seconds) | new type in `Assets/Scripts/Runtime/Items/` (I-0) |
| Ore-rarity setting id | `IronOreRarityId = "iron_ore_rarity"` | `SettingsCatalog` (I-0 mints; I-2 flips LIVE) |
| Smelt-cost setting ids | `SmeltFuelCostId = "smelt_fuel_cost"`, `SmeltTimeId = "smelt_time"`, `SmeltOrePerIngotId = "smelt_ore_per_ingot"` | `SettingsCatalog` (I-0 mints; I-3 flips LIVE) |
| SettingsCatalog registrar | `PopulateIron(...)` — a NEW per-feature Populate method | `SettingsCatalog` (I-0) — **never grow the base `Populate` signature** (the PopulateThirst/PopulateChop/PopulateStones de-collision precedent) |

- **Iron-tier weapon/tool item ids** (sword/knife/spear/axe iron variants) follow the EXISTING
  `WeaponCatalog`/`ItemCatalog` convention — the dev **reads the live catalog** and matches its
  scheme; do NOT mint a parallel one. The pickaxe ids above are the only NEW tool ids this feature
  introduces. If the live catalog's tier scheme differs from `pickaxe_stone`/`pickaxe_iron`,
  **STOP-and-report** (the Sponsor-approved-handoff-can-have-a-tech-error discipline) rather than
  silently diverging.
- **Cross-review check:** when peer-reviewing any two parallel iron PRs, grep the sibling branch
  for these identifiers and flag any divergence as **REQUEST_CHANGES** (vocabulary divergence is
  mergeability-blocking, not NIT-class).

---

## 2. Ground-truth systems these tickets EXTEND (reuse, don't reinvent)

Verified on `main` this session — the iron chain rhymes with existing idioms:

- **HP-node harvest + active left-click verb** — `ChopTree.cs`: `ShouldChopOnClick` (pure static
  guard truth-table), `RequestChopClick`/`SetChopHeld` (input-independent seams), the three world-
  click guards (`UiInputGate.CaptureWorldInput` · `InventoryUI.IsPointerOverUI` · RMB-orbit via
  `Input.GetMouseButton(1)`), `ResolveNearestChoppable` (nearest-in-range resolver over many
  instances), `chopsToFell` (HP), `CastawayCharacter.TriggerChop` (the Mixamo Attack swing). The
  **mining verb is this pattern's sibling** — one strike per click, never proximity-auto.
- **Node → lootable drop** — `LogPileSpawner`/`LogPile` + `PickableLooter` (E-loot) + `IPickable`.
  A depleted ore node drops iron-ore the same way a felled tree drops a log pile.
- **Buildable structure** — `CampfirePlacement.cs`: proximity spot + all-or-nothing material gate
  (`Inventory.SpendWood`), `HasBuilt`, editor-time authored into `Boot.unity` via
  `MovementCameraScene.BuildCampfire` (**NOT** Awake — the legs-up serialization class). The
  **forge is this pattern's sibling** (a NEW structure alongside the campfire).
- **Craft seam** — `CraftSpot.cs` + `Inventory.CraftAxe()`/`PickUpAxe()` (thin, one-recipe today);
  `ItemCatalog` (`ById`/`BuildDefaults`/canonical id consts), `WeaponCatalog`/`WeaponDef`
  (`BuildDefaults`, id-matched to the belt item). Iron crafting **extends the craft seam**.
- **Held tool chain (MANDATORY doc: `procedural-animation-verbs.md`)** — `Animator` →
  `CastawayArmPose` (order 50) → `HeldToolRig`/`HeldAxeRig` (order 100); the mine-swing verb is an
  additive `LateUpdate` offset (NO new Animator clip/state/layer). Held-pickaxe seat rides
  `HeldWeaponPlacement`/`HeldWeaponCycleDebug` per-weapon arrays.
- **SettingsCatalog tweakable pattern** — stable id consts, a per-feature `PopulateX` method,
  `RangeSettingEntry`/`FloatSettingEntry`/`IntSettingEntry`, `Available=false` "(soon)" extension
  hooks for not-yet-built params, `SettingsCategory.IsPlayer` vs dev-console split. The two dials
  follow this verbatim.
- **Blender asset (MANDATORY doc: `blender-asset-pipeline.md`)** — shared `Mat_WeaponPalette` +
  one `weapon_palette.png` (NO per-asset atlas), faceted-chunky, `wpn_`/`prop_`/`env_` naming,
  two-tier style per `[[weapon-two-tier-style-stone-iron]]` + `[[weapon-asset-material-honest-pattern-via-geometry]]`.

---

## 3. Sequencing & dispatch order

**Build-slot reality (`[[single-unity-build-slot-serializes-orchestration]]`):** any ticket that
regenerates a committed scene/asset or needs a Unity build is a **build-lane** ticket and
serializes on the ONE build slot — including behind the **island C-lane** (C2 `86cakk4w8` in
flight; C2→C3→C4 serial per DECISIONS 2026-07-06). Iron build-tickets touch `Boot.unity`
(the seed-42 start island), NOT the POC scene, so there's **no scene collision** with the C-lane —
but they still queue on the single build slot. The **non-build lane never idles**: I-0 (pure-logic)
and the I-1 pickaxe R&D burst run in PARALLEL with the C-lane immediately.

```
                 ┌─ I-1 Pickaxe asset burst (orch R&D lane, parallel) ──────────┐
                 │                                                              ↓
I-0 Foundation ──┼─→ I-2 Ore nodes + mine verb (build) ─┐                       │
(non-build,      │                                       ├─→ I-4 Iron craft ────┴─→ I-5 Chain soak
 FIRST, parallel)└─→ I-3 Forge + smelt flow (build) ─────┘        unlock (build)      + capture gate
                     (I-2 and I-3 both dep only on I-0; sequence on the build slot, either order)
```

**Order the orchestrator dispatches in:**
1. **I-0** (non-build) + **I-1** (R&D lane) — immediately, parallel to the C-lane.
2. **I-2** then **I-3** (or I-3 then I-2) — one at a time on the build slot, after I-0 merges and the
   C-lane frees a slot. They're independent of each other; pick the order by build-queue timing.
3. **I-4** — after BOTH I-2 and I-3 are on main (+ I-1's pickaxe asset for the iron-pickaxe recipe).
4. **I-5** — after I-4; the end-to-end chain soak + capture consolidation.

---

## 4. The tickets

Each is dispatch-ready: an author picks it up and starts without a clarifying question. ACs use
the **Commander's Intent** 3-bucket shape (🎯 destination / 🔒 constraints / 🎚️ tunable defaults).

---

### I-0 — Iron item + recipe + settings **foundation** (type-author-first, non-build)

**Title:** `feat(items): iron-progression data foundation — ore/ingot/pickaxe ids + SmeltRecipe + SettingsCatalog dials [M]`
**Tags:** `feat`, `items` · **List:** `901523878268` · **Status:** `to do` · **Owner:** Devon
**Size:** M · **Deps:** none (dispatch immediately, non-build lane) · **Blocks:** I-2, I-3, I-4

**🎯 Destination:** the shared iron vocabulary EXISTS in code so the mining / forge / craft
tickets build against merged-on-main names instead of inventing their own. Concretely: `iron_ore`
+ `iron_ingot` are canonical `ItemDef`s in `ItemCatalog.BuildDefaults`; `pickaxe_stone` +
`pickaxe_iron` ids exist in both `ItemCatalog` (belt-eligible Tools, like the axe) and
`WeaponCatalog`; a `SmeltRecipe` data type expresses ore→ingot (input id+count, output id+count,
fuel, seconds); and `SettingsCatalog.PopulateIron` registers the ore-rarity + smelt-cost setting
ids as **extension hooks** (`Available=false` "(soon)") plus the three per-tier difficulty presets
as data. *Strip-test:* a competent dev, given only this, could add the ids/type/registrar and the
downstream tickets would compile against them.

**🔒 Constraints:**
- Use the **exact identifiers in §1's vocabulary table** — that IS the point of this ticket.
  *Why:* every consumer ticket imports these; a rename breaks the parallel PRs.
- Iron-tier **weapon** item ids (sword/knife/etc.) follow the EXISTING `WeaponCatalog`/`ItemCatalog`
  scheme — read the live catalog, match it; do NOT mint a parallel tiering. *Why:* the 8-weapon set
  already ships a tier scheme; two schemes = non-mergeable + a broken belt lookup.
- `PopulateIron` is a **NEW per-feature method**; do NOT grow the base `Populate` signature.
  *Why:* the PopulateThirst/PopulateChop/PopulateStones de-collision precedent (SettingsCatalog).
- Setting ids mint as **`Available=false` "(soon)" extension hooks** here — the node/forge tickets
  flip them LIVE. *Why:* the dial has no live system to bind yet; the documented extension-hook idiom.
- The two dials are **dev-console by default** (`SettingsCategory` non-`IsPlayer`) unless the Sponsor
  soak asks otherwise. *Why:* matches the chop/stone/berry tweakables (dev-tune, then bake presets).
- Pure-logic only — **no scene/prefab/build change** in this PR. *Why:* keeps it in the non-build
  lane so it lands parallel to the C-lane build slot.

**🎚️ Tunable defaults (Sponsor-soak tunes; the author predicts, does not mandate):** seed the three
difficulty presets as data — **easy** = ore common / cheap-fast smelt; **medium** = the balanced
default; **hard** = ore sparse / fuel-and-time-costly smelt. Concrete starting numbers (e.g. ore
nodes per island, ore-per-ingot, fuel-per-smelt, seconds-per-smelt) are the author's *prediction* —
flag each `default X — Sponsor-soak tunes`. The node/forge tickets consume these.

**OOS:** ore-node spawning (I-2); the forge structure + smelt runtime (I-3); iron recipe crafting
(I-4); the pickaxe MESH (I-1); any `Boot.unity` authoring.

**Success tests (EditMode, pure-logic — no .asset round-trip, `BuildDefaults` in code):**
- `ItemCatalog.ById("iron_ore")` / `("iron_ingot")` / `("pickaxe_stone")` / `("pickaxe_iron")`
  all return non-null defs of the expected `ItemKind`.
- `WeaponCatalog.ById("pickaxe_stone")` / `("pickaxe_iron")` resolve.
- A `SmeltRecipe` round-trips input→output correctly (ore count in, ingot count out, fuel + seconds).
- `SettingsCatalog.PopulateIron` registers `iron_ore_rarity` + the three smelt-cost ids; each is
  present, `Available=false`, and carries the three difficulty presets.

---

### I-1 — Pickaxe asset burst (orch R&D lane — **spec the handoff, not the modeling**)

**Title:** `feat(art): stone + iron pickaxe — 5th tool type, Sponsor-judged Blender burst [M, R&D-lane]`
**Tags:** `feat`, `art`, `asset` · **List:** `901523878268` · **Status:** `to do`
**Owner:** Orchestrator (R&D lane — MCP-bound Blender + Sponsor-interactive iteration), harvest PR
**Size:** M · **Deps:** soft on I-0 for `pickaxe_stone`/`pickaxe_iron` ids · **Blocks:** I-4 (iron
pickaxe recipe); soft-blocks I-2's held-tool swap (mining dev's against a placeholder until delivery)

**🎯 Destination:** two shipped FBX assets — `wpn_pickaxe_stone_01` + `wpn_pickaxe_iron_01` — that
read **in-hand** as a stone and an iron pickaxe (Sponsor-judged), sitting in the locked weapon
family on the shared palette, ready to hold + swing. **Relevant bars:** Bar 3 (reads as its
MATERIAL — knapped-stone head + wood haft vs forged-iron head + iron/leather grip, pattern via
modeled facets NOT texture), Bar 5 (judged IN-HAND via the discrete mesh-swap picker, never a bare
render), Bar 6 (a praised divergence is not a defect).

**🔒 Constraints:**
- Follow `blender-asset-pipeline.md` + `[[weapon-two-tier-style-stone-iron]]` **verbatim**: ONE
  shared `Mat_WeaponPalette` + `weapon_palette.png` (NO per-asset atlas), faceted-chunky, `wpn_`
  naming, hammered facets only where the two-tier style prescribes. *Why:* the shared-palette
  ~1-draw-call model + the shipped tier language.
- Stone tier = knapped biface-style head + wood haft (no lashing per the memory); iron tier =
  forged flat-smooth head + iron handle + leather grip. *Why:* the locked two-tier contract.
- **Spec-only deliverable from Priya's side** — this ticket describes the HANDOFF; the orchestrator
  runs the interactive burst (like the #254 weapon set) and closes with a harvest PR + a
  productionization follow-up wiring the FBX into the catalog. *Why:* orchestrator-never-codes R&D
  exception; the modeling route is the Sponsor's eye, not a persona dispatch.

**🎚️ Tunable defaults:** in-hand scale/seat is dialed at soak via `HeldWeaponPlacement` (Bar 8 —
direct-tweak, then bake); the author predicts a starting seat, the Sponsor dials it.

**OOS:** the mining VERB (I-2); the iron-pickaxe RECIPE (I-4); any gameplay wiring beyond the FBX +
catalog entry.

**Success tests / gate:** side-by-side in-hand capture of both pickaxes on the shared palette from
the shipped exe (Bar 5 picker path); Sponsor soak-PASS on the two meshes before the productionization
follow-up wires them into recipes.

---

### I-2 — Iron-ore world nodes + **mining verb** (build lane)

**Title:** `feat(gameplay): iron-ore nodes + active-click mining verb — mine ore into the belt [L]`
**Tags:** `feat`, `gameplay` · **List:** `901523878268` · **Status:** `to do` · **Owner:** Devon
**Size:** L · **Deps:** I-0 (ids + ore-rarity setting) · soft-dep I-1 (pickaxe mesh; placeholder
until delivery) · **Build-lane** (serialize behind the C-lane) · **Blocks:** I-4

**🎯 Destination:** the castaway finds **iron-ore rock nodes** in the world and mines them by
**active left-click** (like chopping): with the pickaxe SELECTED and a node in range, each click is
one mine-strike (one swing + progress); after the node's HP is spent it **drops iron-ore** as a
lootable pile the player loots with E, and the node depletes → regrows on a timer. Mining reads as
*work*, not a proximity-auto pickup. *Strip-test:* the Sponsor walks up to an ore node, clicks to
swing the pickaxe, breaks it after several strikes, and loots iron-ore — it feels like earning it.
**Relevant bars:** Bar 3 (the node reads as iron ORE in rock — ore veins as modeled faceted colour,
not a texture), Bar 4 (an ore node looks like a real ore outcrop on the first try — plain real-world
anchor sentence + side-profile capture), Bar 1 (organic placement — no grid), Bar 2 (the mine swing
is lively — the arm/pickaxe follow through).

**🔒 Constraints:**
- **Active left-click, never proximity-auto** — mirror `ChopTree`'s `ShouldChopOnClick` pure-static
  guard truth-table + the three world-click guards (`UiInputGate.CaptureWorldInput` ·
  `InventoryUI.IsPointerOverUI` · RMB-orbit `Input.GetMouseButton(1)`) + an input-independent
  `RequestMineClick`/`SetMineHeld` seam so headless PlayMode + the shipped capture exercise the SAME
  path. *Why:* `[[active-input-not-proximity-auto-for-actions]]` — one click = one strike; the
  Sponsor's "left-click must only act in the game world".
- **Gate on the PICKAXE selected in the belt** (`Inventory.IsSelectedBeltItem(PickaxeStoneId/…)`),
  the mining analog of `IsAxeSelectedInBelt` — NOT merely owned. *Why:* the tool-gated verb pattern.
- The mine SWING is an **additive `LateUpdate` offset via the `CastawayArmPose`→`HeldToolRig` chain**
  — **NO new Animator clip/state/layer/AvatarMask** (read `procedural-animation-verbs.md` first;
  measure the bone axis). Reuse `CastawayCharacter.TriggerChop`-class seam or add a sibling trigger
  per that doc's idiom. *Why:* the non-negotiable held-tool chain; a parallel swing path is the
  documented failure.
- Node → drop reuses `LogPileSpawner`/`PickableLooter`/`IPickable` (an ore-pile sibling of the log
  pile). *Why:* one loot path, not a parallel one.
- Nodes are authored **editor-time into `Boot.unity`** (a `MovementCameraScene.BuildOreNodes`-class
  method) OR read from a deterministic scatter — **NOT built at Awake** (the legs-up class). *Why:*
  `unity-conventions.md §editor-vs-runtime`.
- **Regen + commit** the scene/asset if the placement is generated — a code-only PR ships the stale
  snapshot. *Why:* `[[unity-procedural-committed-assets-go-stale]]`.
- Do NOT touch the seed-42 scatter RNG stream (READ-only if reusing it). *Why:* the world stays
  byte-identical (`[[world-is-big-round-island]]`).
- Flip `IronOreRarityId` **LIVE** (drives node count/density) — the I-0 extension hook goes live here.
- **Peaceful** — no combat/HP/damage dep; nodes are inert. *Why:* Sponsor Q4 locked.

**🎚️ Tunable defaults (predict, don't mandate — flag `default X — Sponsor-soak tunes`):** strikes-
to-break a node (default ~4, mirror `chopsToFell` band 1–10); ore yielded per broken node; node
count per the ore-rarity presets (easy common / hard sparse); regrow window `[min,max]` (organic,
like tree-regrowth). The Sponsor dials these; predict against Bar 7 (three tiers).

**OOS:** the furnace + smelting (I-3); crafting iron (I-4); the pickaxe MESH (I-1 — placeholder axe
mesh acceptable until delivery); combat.

**Success tests:**
- EditMode: `ShouldMineOnClick` truth-table (pickaxe-selected × in-range × not-over-UI × not-RMB-drag
  × no-modal) covers every guard; ore-rarity presets map to node counts.
- PlayMode: `RequestMineClick` N times breaks a node → an ore pile spawns → E-loot adds `iron_ore` to
  the belt; a click without the pickaxe selected is a no-op; a click over the belt UI is swallowed.
- Scene-presence guard (a `ChopSceneTests` sibling): the ore node(s) + component + refs serialize
  into `Boot.unity`.
- Shipped-build capture: the shipped exe mines a node → ore in belt (per the interaction-soak gate,
  since the PlayMode job is advisory-unreliable — `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`).
- Side-profile capture of an ore node (Bar 4). Predict-Before-Soak line + bounded-convergence claim.

---

### I-3 — Forge/furnace **buildable + smelt flow** (build lane)

**Title:** `feat(gameplay): forge/furnace buildable + smelt ore→iron-ingot over a timer [L]`
**Tags:** `feat`, `gameplay` · **List:** `901523878268` · **Status:** `to do` · **Owner:** Drew
**Size:** L · **Deps:** I-0 (`SmeltRecipe` + smelt-cost settings) · **Build-lane** (serialize behind
the C-lane; independent of I-2 — order by build-queue) · **Blocks:** I-4

**🎯 Destination:** the player **builds a NEW forge/furnace structure** from materials (wood + stone,
like the campfire build), then **smelts iron-ore into iron ingots** at it: load ore (+ fuel), a
**smelt timer** runs, and iron ingots come out. It's a distinct build beat in the survival arc
(shipwreck → … → furnace → iron) and the **work half** of the earn. *Strip-test:* the Sponsor
gathers ore + fuel, builds a furnace, feeds it ore, waits out the smelt, and collects iron ingots —
the grind IS the reward gate. **Relevant bars:** Bar 4 (the furnace looks like a real stone furnace
on the first try — plain anchor sentence + side-profile capture), Bar 3 (reads as stone-and-metal
material), Bar 2 (the lit/working furnace has life — glow/heat/smoke motion, not static), Bar 7
(three difficulty tiers via smelt cost).

**🔒 Constraints:**
- **Build interaction mirrors `CampfirePlacement`**: proximity spot + all-or-nothing material gate
  (`Inventory.SpendWood`/`SpendStone`-class), `HasBuilt`, negative case load-bearing ("not enough
  mats → no furnace, no debit"). *Why:* the proven buildable idiom + the wood-gate success-test
  precedent.
- The furnace is a **NEW structure, NOT the bonfire** — a distinct GameObject/prefab + its own
  build spot. *Why:* Sponsor Q3 locked (distinct from the campfire; visual clarity).
- Furnace + spot authored **editor-time into `Boot.unity`** (a `MovementCameraScene.BuildForge`-class
  method), ships UNLIT/unbuilt, raised when the player arrives with mats — **NOT Awake** (legs-up).
  *Why:* `unity-conventions.md §editor-vs-runtime`; the campfire precedent.
- The smelt runtime consumes a `SmeltRecipe` (I-0): ore + fuel in → ingot out after `seconds`. The
  timer is real elapsed time (a queue is fine); on completion the ingots land in the belt/inventory.
  *Why:* the work-led earn feel is the TIME + fuel cost, not instant.
- Flip the **smelt-cost settings LIVE** (`SmeltFuelCostId`/`SmeltTimeId`/`SmeltOrePerIngotId`) — the
  I-0 extension hooks go live here, driving the recipe's fuel/seconds/ore-per-ingot. *Why:* the
  second difficulty dial.
- **Regen + commit** the scene if generated. *Why:* `[[unity-procedural-committed-assets-go-stale]]`.
- Furnace ASSET route: procedural faceted primitive OR Blender (`env_`/`prop_` per
  `blender-asset-pipeline.md`), shared palette, material-honest (Bar 3). Route per
  `asset-routing.md`. *Why:* the in-house asset discipline (`[[in-house-asset-routes-over-paid-tools]]`).
- **Peaceful** — no combat dep. *Why:* Sponsor Q4.

**🎚️ Tunable defaults (predict, flag `default X — Sponsor-soak tunes`):** furnace build cost
(wood+stone units); ore-per-ingot; fuel-per-smelt; seconds-per-smelt — each mapped to the easy/med/
hard smelt-cost presets (easy cheap-fast / hard costly-slow). The Sponsor dials via the dev console,
then bake the three presets (the snake-POC F-key-tune → bake pattern; Bar 8).

**OOS:** ore NODES/mining (I-2); crafting the iron weapons/tools (I-4); the pickaxe (I-1); combat.

**Success tests:**
- EditMode: the material gate is all-or-nothing (too few mats → no build, no debit); a `SmeltRecipe`
  with fuel+ore inputs produces the right ingot count after `seconds`; smelt-cost presets map right.
- PlayMode/interaction: reach the spot with mats → furnace builds (`HasBuilt`); feed ore+fuel → after
  the timer, ingots appear; insufficient fuel → no smelt.
- Scene-presence guard (a `CampfireSceneTests` sibling): the forge + spot + refs serialize into `Boot.unity`.
- Shipped-build capture: the shipped exe builds the furnace + completes one smelt (interaction-soak
  gate). Side-profile capture of the furnace (Bar 4). Predict-Before-Soak + bounded-convergence claim.

---

### I-4 — Iron **crafting unlock** + difficulty dials confirmed end-to-end (build lane)

**Title:** `feat(gameplay): iron-tier crafting unlock — ingots → iron weapons/tools at the bench [L]`
**Tags:** `feat`, `gameplay` · **List:** `901523878268` · **Status:** `to do` · **Owner:** Devon
**Size:** L · **Deps:** I-2 + I-3 (both on main) + I-1 (iron pickaxe asset) · **Build-lane** (last
build ticket) · **Blocks:** I-5

**🎯 Destination:** with iron ingots in hand, the player **crafts the iron tier at the bench** — the
iron weapons + tools (including `pickaxe_iron`) that today only exist in the tier-contrast lineup
become **actually craftable + equippable**, gated on ingots. This closes Model A: mine → smelt →
**forge the upgrade**. Both difficulty dials are confirmed working end-to-end across the three tiers.
*Strip-test:* the Sponsor, holding iron ingots, crafts an iron axe/sword/pickaxe at the bench and
equips it — iron is now *earned and wielded*, not just displayed. **Relevant bars:** Bar 3 (iron
weapons read as forged iron in-hand), Bar 5 (in-hand judged via the picker), Bar 7 (three tiers),
Bar 6 (praised divergences preserved).

**🔒 Constraints:**
- Extend the **existing craft seam** (`CraftSpot` + `Inventory.CraftAxe`/`PickUp*` + `ItemCatalog`/
  `WeaponCatalog`) — add ingot-gated iron recipes; do NOT build a parallel crafting system. If a
  richer recipe-tree UI is needed beyond the thin seam, keep it minimal + flag scope to the
  orchestrator BEFORE building (the bandaid-retirement-scope-blowup discipline). *Why:* the thin
  craft seam is the stable data seam; a full recipe-tree is its own future ticket.
- Iron recipes consume `iron_ingot` (all-or-nothing debit, the `SpendWood` idiom) and produce the
  iron item/weapon ids **from the live `WeaponCatalog`/`ItemCatalog` scheme** (read it — §1). *Why:*
  the belt lookup binds item id = weapon id; a parallel id breaks equip.
- The iron pickaxe recipe wires I-1's `wpn_pickaxe_iron_01` mesh to `pickaxe_iron`. *Why:* the 5th
  tool type, iron tier.
- Confirm BOTH dials LIVE end-to-end (ore-rarity from I-2 + smelt-cost from I-3) across the three
  presets — this ticket is where the full easy/med/hard chain is provable. *Why:* Sponsor Q5.
- Regen + commit if scene-authored; editor-time authoring, not Awake. *Why:* the standing rules.
- **Peaceful** — no combat dep. *Why:* Sponsor Q4.

**🎚️ Tunable defaults:** ingots-per-iron-weapon (predict; the Sponsor dials); the iron in-hand seat
(Bar 5/8 picker + `HeldWeaponPlacement`, then bake).

**OOS:** find-in-world acquisition (`86cah7y5b`, standalone); combat use of the iron weapons (combat
cluster); new recipe-tree UI (flag if needed).

**Success tests:**
- EditMode: an iron recipe with sufficient ingots crafts the iron item; insufficient ingots → no
  craft, no debit; the crafted iron ids resolve in `WeaponCatalog`/`ItemCatalog`.
- PlayMode/interaction: ingots → craft iron axe/sword/pickaxe → equip → held on the belt.
- Shipped-build capture: the shipped exe crafts + equips an iron tool (interaction-soak gate).
  In-hand picker capture of the iron tier (Bar 5). Predict-Before-Soak + bounded-convergence claim.

---

### I-5 — Full-chain **soak + capture-gate consolidation**

**Title:** `test(gameplay): iron chain end-to-end soak — mine→smelt→craft + 3-tier difficulty A/B [M]`
**Tags:** `test`, `gameplay` · **List:** `901523878268` · **Status:** `to do` · **Owner:** Tess
**Size:** M · **Deps:** I-4 on main · **Build-lane** (the soak build) · **Blocks:** none (the gate)

**🎯 Destination:** ONE shipped soak build that lets the Sponsor play the **whole Model-A chain end
to end** — walk up to an ore node, mine ore, build a furnace, smelt ingots, craft + equip an iron
tool — and judge it as a whole, PLUS an A/B across the three difficulty presets so the ore-rarity +
smelt-cost dials are Sponsor-judged. *Strip-test:* the Sponsor plays one build and the full "earn
iron through work" loop is live, findable, and every judged step is triggerable — no placeholder
gaps (`[[sponsor-rejects-unsoakable-placeholders]]`).

**🔒 Constraints:**
- Every judged step must be **FINDABLE + live-triggerable** in the served build; agree any descope
  BEFORE the build. *Why:* `[[sponsor-rejects-unsoakable-placeholders]]`.
- Serve from the project `Build\soak-<N>\` folder with the exact exe path + expected HUD build stamp
  + a "test THIS" checklist. *Why:* `[[soak-handoff-path-and-explicit-test-checklist]]` +
  `[[soak-builds-go-in-project-build-folder]]`.
- **PLAY the exe** at real framing before serving — do not serve unplayed. *Why:*
  `[[served-unverified-soaks-need-played-verification]]`.
- Consolidate the per-ticket VerifyCaptures; confirm the shipped-build capture gate is green for each
  visual surface (ore node, furnace, iron in-hand). *Why:* `team/TESTING_BAR.md` shipped-build gate.
- Carry a **Predict-Before-Soak** line + a **bounded-convergence claim** (name the bars tested — 1,
  2, 3, 4, 5, 7 — and the bars NOT tested). *Why:* the testing bar for feel/soak PRs.

**🎚️ Tunable defaults:** the three difficulty presets are the A/B subject — the Sponsor's dialed
values from I-2/I-3/I-4 soaks bake here.

**OOS:** new features; combat; find-in-world.

**Success tests / gate:** the full chain is playable in one shipped build; Tess QA PASS + Sponsor
soak on the chain + the 3-tier A/B; all VerifyCaptures green.

---

## 5. Test / capture gates (cross-cutting, per `team/TESTING_BAR.md`)

Applies to every build ticket above (I-2 … I-5):

- **Paired EditMode + PlayMode/interaction tests** — pure-logic guards (truth-tables, recipes,
  preset maps) in EditMode; end-to-end verbs in PlayMode, with the interaction-soak as the real gate
  where the PlayMode job is advisory-unreliable (`[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`).
- **Scene-presence guards** — every editor-time-authored object (ore node, furnace, forge spot) gets
  a `ChopSceneTests`/`CampfireSceneTests`-sibling guard that the object + component + refs serialize
  into `Boot.unity` (the legs-up/mangled-serialization class).
- **Shipped-build capture gate** — every visual surface (ore node, furnace, iron in-hand) ships
  evidence captured from the BUILT exe, not the editor (`VerifyCapture` sibling + side-profile
  captures for Bar 4 physical features).
- **Self-Test Report** before Tess reviews each UX-visible PR.
- **Predict-Before-Soak + bounded-convergence** on every feel/look PR — a falsifiable pre-soak
  prediction graded against the soak + name the bars tested vs not.
- **Static-reset audit** — no mutable statics (or a `[RuntimeInitializeOnLoadMethod]` reset), per
  `StaticStateResetTests`.
- **Committed-generated-assets** — regen + commit or the build ships the stale snapshot.

---

## 6. Decision drafts (for the next DECISIONS batch — Priya-only file)

- **Decision draft:** iron progression Model-A implementation split = 6 tickets (I-0 foundation
  non-build · I-1 pickaxe R&D · I-2 ore nodes + mine verb · I-3 forge + smelt · I-4 iron craft
  unlock · I-5 chain soak); I-0 is type-author-first (Pattern A) minting the §1 vocabulary; build
  tickets serialize on the single build slot behind the island C-lane (no scene collision — iron
  touches `Boot.unity`, C-lane touches the POC scene). Source: `86cakkg04` spec.
- **Decision draft:** iron vocabulary locked — `iron_ore` / `iron_ingot` / `pickaxe_stone` /
  `pickaxe_iron` / `SmeltRecipe` / `PopulateIron` + the ore-rarity + smelt-cost setting ids (§1
  table). Iron-tier weapon ids follow the existing `WeaponCatalog` scheme (no parallel tiering).
