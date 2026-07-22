# Iron-Tier Crafting / Progression — Design Options (Sponsor-input material)

**Ticket:** `86caju051` (sponsor-gate — design conversation). This note enumerates coherent
stone→iron progression models for the Sponsor to rule on. **It is NOT a locked spec and I do
not decide.** Erik-consult, 2026-07-06.

## Question

The 8-weapon stone+iron set is fully in-game since PR #254 (`86cajkk7h`). **Stone is the live
crafted tier; iron is imported + shown in the tier-contrast lineup but has NO acquisition path.**
What progression model turns "iron exists in the world" into "the player earns iron" — in a way
that (a) extends the existing island survival arc rather than bolting on a new genre, (b) fits the
3-tier difficulty contract, and (c) composes with the open combat cluster?

## Bottom line

Four models below. **My top-ranked is Model D — Hybrid salvage-primer → forge-completion**: the
player *finds* raw iron in the world (shipwreck scrap / ore chunks — reuses the `86cah7y5b`
find-in-world acquisition system and the existing pickup/inventory) then *processes* it at a light
forge/furnace structure (reuses the campfire-loop + crafting-table patterns) to craft the iron
tier at the bench. It is the only model that delivers BOTH the kid-friendly discovery beat AND the
adult-satisfying earn-it gate, reuses the most existing systems, and folds the find-in-world combat
ticket in as a component rather than a competitor. Confidence: **moderate-high** on the
system-fit / reuse analysis (grounded in the live codebase + vision arc); the final feel call
(how grindy the gate should be) is a Sponsor soak decision, not mine.

## Existing systems this must build on (ground truth)

- **Survival arc (vision doc):** shipwreck → pick up branches/stones → craft table (wood) →
  wood+stone axe → chop wood → stone bonfire (warmth) → berries (hunger) → pond (thirst).
  The arc's spine is **gather → craft-at-a-station → use**. Iron progression should rhyme with it.
- **Live systems (verified via board/STATE/memory):** crafting table + recipes, stone-tier weapon
  crafting, chop-tree → wood, campfire (warmth loop + lit-fire object), berry harvest, pond drink,
  inventory/belt + pickup, snake enemy (`86caaz4vn`, live since #234). Hit-reaction clips are
  PARKED (DECISIONS 2026-06-17) awaiting a damage/combat-feedback trigger.
- **Weapon tiers (Sponsor-locked, memory `weapon-two-tier-style-stone-iron`):** STONE = knapped
  biface + wood haft (first-craft); IRON = forged flat-smooth blades + iron handles + leather grips
  (the *upgrade*). The visual language already says "iron is the reward." The progression just has
  to make earning it feel deserved.
- **Combat cluster (names read from PR/away-queue/git — I cannot reach the full board):**
  `86cah7y5b` find-in-world weapon acquisition · `86cabcdpn` Combat/HP/death DESIGN (grill-Sponsor) ·
  `86caaz4vn` Snake enemy (live) · plus the brief-named **boar** (larger fauna / food+threat) and
  **status-effects** items. Treat boar/status-effects as the same cluster even where I lack the IDs.
- **Asset routes (memory `in-house-asset-routes-over-paid-tools`):** world/props/structures =
  procedural + URP Shader Graph *or* Blender faceted-chunky (furnace/anvil = Blender or procedural
  primitive, shared palette); creatures (boar) = Hyper3D Rodin → Mixamo. No paid AI-3D.
- **Difficulty (memory `difficulty-settings-easy-medium-hard`):** every system ships easy/med/hard
  presets from the start (need-decay, enemy aggro, and now: iron-gate grind). Snake POC established
  the "F-key-tune → bake three presets" pattern; iron progression inherits it.

---

## Model A — Ore-mining + smelting chain (the Valheim/Minecraft spine)

**Player journey:** discover iron-ore rock nodes in the world (cliffs, cave mouths, mountain feet)
→ mine them with a stone pickaxe (a new tool in the set, or the axe re-purposed) → carry ore to a
**furnace/smelter** the player builds (wood + stone, like the bonfire) → smelt ore → iron ingots
over a timer → craft iron tier at the bench (possibly needing an **anvil** too).

- **New systems demanded:** mineable ore nodes (extends the chop/harvest interaction — a node with
  HP that drops ore); a **furnace** structure + a smelting timer/queue (extends the campfire "lit
  object + fuel" loop); ore + ingot item types; likely a **pickaxe** tool. Optionally an anvil.
- **Reuses:** harvest/HP-node interaction (chop code), campfire loop (furnace), crafting bench,
  inventory/belt, procedural/Blender assets for nodes + furnace.
- **Difficulty fit:** strong. Easy = few nodes, instant/fast smelt, low ore cost. Hard = sparse
  nodes, long smelt, high ore cost, fuel consumption. Clean tier surface.
- **Scope:** **L** (furnace + smelt timer + ore nodes + pickaxe + recipes is the biggest of the four;
  2-3 new systems). Highest adult-satisfaction; highest build cost + most new Unity-build tickets
  serializing on the single build slot.
- **Composes with combat:** neutral-to-good — ore nodes can live near the snake/boar range to add
  risk-reward; no direct dependency. Doesn't need the combat cluster to ship.

## Model B — Find-in-world iron caches (pure discovery / salvage)

**Player journey:** iron weapons/tools are found ready-made as world loot — shipwreck salvage on the
beach, ruins, buried caches, or drops. No processing. This is essentially the `86cah7y5b`
find-in-world weapon acquisition ticket used *as* the iron path.

- **New systems demanded:** loot spawns / lootable containers (chest/wreck/cache) + a spawn table.
  Minimal — reuses pickup/inventory almost entirely.
- **Reuses:** pickup, inventory/belt, procedural/Blender container props. **Directly consumes**
  `86cah7y5b` rather than competing with it.
- **Difficulty fit:** moderate. Easy = iron caches common + obvious. Hard = rare, hidden, or
  guarded (by the snake/boar). Tunable via spawn density, but the *earn* is "explore + find," not
  "work for it" — shallower progression than A/D.
- **Scope:** **S-M** (smallest; one loot/container system). Fastest to ship.
- **Composes with combat:** **strong** — "iron is guarded by a boar / found past a snake nest"
  turns the combat cluster into the gate, giving both systems purpose. Weakness: with no crafting
  gate, iron feels *found* not *forged*, slightly undercutting the Sponsor-locked "iron is the
  upgrade" visual language.

## Model C — Trade / quest / milestone-gated

**Player journey:** iron unlocks via a narrative objective or exchange — a wrecked-ship cargo you
must repair/open, a milestone ("survive N days," "light the signal"), or an NPC/castaway you trade
with.

- **New systems demanded:** a quest/objective tracker + UI; optionally an NPC (full character
  pipeline: Rodin → Mixamo + dialogue). Heaviest *narrative* scaffolding.
- **Reuses:** inventory (trade), settings/HUD (objective tracker). Least reuse of the survival-loop
  systems.
- **Difficulty fit:** weak-moderate. Quests gate *access* not *effort*; difficulty mostly affects
  the guarding challenge, not the progression itself.
- **Scope:** **M-L** (quest system is M; add an NPC and it's L + a character-pipeline dependency).
- **Composes with combat:** okay (a quest can be "defeat the boar"), but an NPC on a
  solo-castaway-island fights the established fiction (the whole premise is *alone on an unexplored
  island*). **Weakest thematic fit** of the four.

## Model D — Hybrid: salvage-primer → forge-completion (RECOMMENDED)

**Player journey:** the player *finds* raw iron in the world — **shipwreck scrap / ore chunks**
(reuses `86cah7y5b` find-in-world + pickup) — then *processes* it at a **light forge/furnace**
(lighter than Model A's full mining chain: no ore nodes/pickaxe; the raw iron is found, not mined)
to craft the iron tier at the bench. The wreck you washed in on becomes the iron source — closing
the shipwreck-arc loop the vision opens with.

- **New systems demanded:** a **forge/furnace** structure + a single smelt/forge step (reuses
  campfire loop); a raw-iron (scrap/ore-chunk) item + iron-ingot item; find-in-world spawn for the
  scrap. NO mineable nodes, NO pickaxe — that's what makes it M not L.
- **Reuses:** the MOST of any model — find-in-world (`86cah7y5b`), pickup/inventory, campfire loop,
  crafting bench, procedural/Blender forge asset.
- **Difficulty fit:** strong AND clean. Easy = scrap common + no forge needed (craft direct). Med =
  scrap findable + one forge step. Hard = scrap rare/guarded + fuel-costed forge + more scrap per
  weapon. Two independent dials (find-rarity + forge-cost) map naturally onto three tiers.
- **Scope:** **M** (one structure + one process step + two item types + a spawn hook — meaningfully
  less than A).
- **Composes with combat:** **strongest** — scrap can be boar-guarded / snake-nest-adjacent, so the
  combat cluster becomes the *risk* half of a risk-reward, while the forge is the *work* half. It
  makes find-in-world (`86cah7y5b`) a **component** of iron progression instead of an alternative
  to it, and gives the parked hit-reaction/combat-feedback content a reason to wire up.

---

## Ranking (evidence / effort grading)

| Rank | Model | Thematic fit | System reuse | Difficulty fit | Combat compose | Scope | Verdict |
|---|---|---|---|---|---|---|---|
| **1** | **D Hybrid salvage→forge** | Strong (closes shipwreck loop) | **Highest** | Strong (2 dials) | **Strongest** | **M** | Best balance; find + forge = kid-discovery + adult-earn |
| 2 | A Ore-mining + smelt | Strong | High | Strong | Neutral | **L** | Richest but heaviest; most build-slot cost |
| 3 | B Find-in-world caches | Moderate (found ≠ forged) | High | Moderate | Strong | **S-M** | Cheapest + fastest; shallowest progression |
| 4 | C Trade/quest/NPC | **Weak** (breaks alone-on-island) | Low | Weak | Okay | M-L | Weakest island fit; NPC = char-pipeline dep |

**Evidence strength:** the reuse/system-fit analysis is **strong** (grounded in the live codebase,
the vision arc, and Sponsor-locked memory). The *feel* calls (how grindy, how rare) are
**soak-decided**, not research-decided — flagged as Sponsor questions below, not asserted here.
**Weak spot:** I could not reach the full board, so boar/status-effect ticket IDs are inferred from
PR/away-queue history; verify their exact scope before locking any composition claim.

---

## Sponsor decision sheet (the calls only you can make)

**Q1 — Which progression model?**
- (a) **Model D Hybrid — find raw iron, forge it** *(Erik-recommended)*
- (b) Model A — mine ore + build a smelter (richest, biggest build)
- (c) Model B — just find finished iron in the world (cheapest, shallowest)
- (d) Model C — quest/trade/NPC gate (weakest island fit)

**Q2 — How does iron feel to earn?**
- (a) **Discovery-led** — mostly about exploring + finding (leans B / light-D)
- (b) **Work-led** — mostly about a crafting/smelting grind (leans A / heavy-D)
- (c) **Both, balanced** — find the raw material, then work it (the D default)

**Q3 — New crafting station: do we add a forge/furnace, or reuse the campfire/bench?**
- (a) **New forge/furnace structure** (distinct from bonfire; adds a build beat — needed for A/D)
- (b) Reuse the existing campfire as the smelter (cheaper; less visual clarity)
- (c) No station — craft iron directly at the existing bench once you have the material (B / easy-D)

**Q4 — Should iron acquisition be guarded by the combat cluster (boar / snake)?**
- (a) **Yes — iron is risk-reward, guarded by fauna** (needs combat/HP `86cabcdpn` first; strongest
  synergy, adds a hard dependency + sequencing)
- (b) **No — iron is peaceful gather/craft** (ships independent of combat; safest sequencing)
- (c) Difficulty-dependent — unguarded on easy, guarded on hard

**Q5 — Difficulty dials for the iron gate (pick the surface to expose):**
- (a) **Raw-iron rarity** (how often scrap/ore spawns)
- (b) **Forge/smelt cost** (fuel + time + material-per-weapon)
- (c) **Both** *(recommended — gives independent easy/med/hard presets, per the snake-POC tune→bake
  pattern)*

## Open questions to resolve before this becomes a spec

1. Exact scope + IDs of the boar + status-effect combat tickets (I inferred from PR/away-queue
   history; the full board was unreachable) — confirm before locking Q4's dependency.
2. Whether `86cah7y5b` (find-in-world weapon acquisition) is scheduled as a standalone feature or
   should be absorbed as the "find raw iron" component of Model D.
3. Whether a **pickaxe** is wanted as a new tool (only Model A needs one; it'd extend the
   Sponsor-locked weapon set with a 5th type per tier).
</content>
</invoke>
