# Hand-tool / weapon STYLE SPEC — Route A (in-house Blender matched set)

**Status:** FINALIZED 2026-06-19 (Uma, ticket `86cabh8rt`); **CORRECTED AGAINST THE SHIPPED SET
2026-07-27** (Uma, ticket `86cah7ym9` AC1) — the family is **CLOSED at 3 tiers × 5 types**, red is
retired, and §2/§4 are now measured from `Assets/Art/Props/WeaponPack/` rather than described from
intent. **Read the Corrections block below before §1** — it outranks the body.
Supersedes the DRAFT seed.
Locks the two open parameters (shading model + palette hexes) against the **live world
look** — verified against the inspiration board, the tool refs, and the live world
materials/shaders — not decided in the abstract.

> **Correction 2026-06-19 (axe-head material).** The hero axe HEAD is **STONE / knapped
> FLINT**, not a red metal — the Sponsor rejected a red axe head (memory
> `weapon-asset-material-honest-pattern-via-geometry`: a tool reads as its MATERIAL; a red
> metal head reads forged, fighting the hand-whittled anchor). The head maps to
> `flint-grey #8E8A82` + `dark-flint #5C5853` (the value already used in the built axe), and
> its surface PATTERN is **knapped flake-scar facets modeled into the geometry** — NOT a
> detail-texture / normal-map (preserves the shared ~1-draw-call palette material). **⚠ The red-lashing note that followed here is SUPERSEDED by the Corrections below
> (2026-07-19, then 2026-07-27)** — the Sponsor removed ALL red ("remove the red things"); there is
> NO red lashing on any shipped tier (the stone-tier axe head binds with no red cord).

> **Correction 2026-07-19 (LIVE 3-tier reality + red removed + per-weapon swing clips —
> SUPERSEDES every `grip-wrap-red` reference below).** Two Sponsor decisions post-date this
> spec's 2026-06-19 finalize; where the body below disagrees, the live reality here wins.
>
> **(1) The family ships in THREE Sponsor-approved tiers — WOOD / STONE / IRON.** Verified from
> the shipped set `Assets/Art/Props/WeaponPack/wpn_{axe,knife,pickaxe,spear,sword}_{wood,stone,iron}_01.fbx`
> (memory `weapon-two-tier-style-stone-iron`; WOOD-tier add per DECISIONS.md "2026-07-18 — Wood-tier
> weapon set PASSED", PR #304). **The red `grip-wrap-red #7E3A3A` LASHING / grip-wrap is REMOVED**
> — the Sponsor said "remove the red things." Corrected grip recipes: **wood** = whittled
> haft-brown + tan cut facets + fire-hardened tip; **stone** = straight wood haft +
> `haft-wood-shadow`-dark (W2) grip band, NO lashing; **iron** = iron handle + segmented LEATHER
> grip (dark-brown, the `#5A3B22` W2 block repurposed as leather per the two-tier memory; no wood
> on iron). NO red on any tier — the shipped axe reads "dark leather-wrapped grip / do NOT recolor
> to barn-red" (gameplay-ui-direction.md, shipped-axe icon row). **§2 slot W8 `grip-wrap-red` is
> RETIRED from the weapon set** (it survives only as a `style-guide-v2 §3/§6` UI-era anchor; no
> shipped weapon uses it).
>
> **(2) Attack swings use a SEPARATE Mixamo clip per weapon class** (Sponsor, 2026-07-19).
> DECISIONS.md "2026-07-19 — Combat cluster order: SWINGS first, boar second" locks "a Mixamo clip
> per weapon class," recorded on ticket `86caffwv5`; axe / pickaxe / knife / sword / spear each
> get their OWN Sponsor-provided clip. The earlier "axe + pickaxe share ONE overhead clip" economy
> proposal (combat-cluster-design-brief.md, PR #320) is **RESOLVED-REJECTED.**

> ## ⚠ Correction 2026-07-27 — THE FAMILY IS CLOSED: 3 tiers × 5 types, red retired *and measured*
>
> **Ticket `86cah7ym9` AC1.** This block is the newest live truth; where §2 / §3 / §4 below disagree
> with it, **this block wins.** Every figure here was **measured from the shipped set on 2026-07-27**
> — the 15 FBXs and the palette PNG in `Assets/Art/Props/WeaponPack/` — not carried forward from the
> 2026-06-19 draft. This spec's failure mode has been *asserting a recipe nobody shipped*; the fix is
> to make the recipe checkable.
>
> **(1) THREE tiers — WOOD / STONE / IRON — FINAL. The "bone" tier is RETIRED.**
> Sponsor decision 2026-07-27 (`86cah7ym9`, comment `90150245469789`), verbatim: *"AC2 Q1 — bone
> tier: RETIRED. The weapon family is three tiers — wood / stone / iron — full stop. The 2026-07-01
> lock's 'wood→stone→bone/metal' phrasing is superseded; stop citing bone as a live tier in any
> spec, brief or ticket."* Reasons given in the popup: no crafting-chain hook (the shipped
> progression is wood-pick → stone → stone-pick → iron ore → forge → iron), no source material in
> the world, and a 4th tier costs 5 meshes + 5 recipes + 5 hand-seats. The bone-from-hunted-boar
> fiction was surfaced as the strongest case *for* keeping it and still declined.
> **Do not write a bone tier into this spec, a brief, or a ticket. The tier list is not open.**
> *Reversal path (not an open question):* a single bone piece could still return later as ONE
> data+asset PR through the per-piece recipe on `86cah7ym9` AC3.
>
> **(2) FIVE types — axe / pickaxe / spear / dagger / sword — FINAL. No sixth type.**
> Same decision, verbatim: *"AC2 Q2 — new weapon TYPES: NONE. The roster is the shipped five."*
> Both alternatives were surfaced and declined — **blunt** (club/mace; cheapest, might have ridden an
> existing swing class as pure data) and **ranged** (bow/sling; biggest gameplay delta but needs a new
> swing class **and** a projectile system that does not exist). **5 types × 3 tiers = the 15 meshes
> that are the complete contents of `Assets/Art/Props/WeaponPack/`** (verified by directory listing,
> 2026-07-27). Nothing in this spec should read as an invitation to add a type.
>
> **(3) Red is GONE from the weapon set — now MEASURED, not asserted.**
> Sponsor: *"remove the red things."* Verification 2026-07-27: every UV coordinate in all 15 shipped
> FBXs was sampled against `weapon_palette.png`, and **zero** land on any red block. The live PNG
> still physically *contains* three red blocks — `#7E3A3A` (the §2 W8 `grip-wrap-red`) plus
> `#A33B30` and `#7E2C24`, two reds the 2026-06-19 table never documented at all — but **all three
> are dead pixels: no mesh addresses them, and the shared material is referenced only by the
> WeaponPack FBXs and `Assets/Resources/WeaponSetLineup.prefab`.** They are listed as RETIRED rows in
> §2 *so that a reader finds them already crossed out rather than discovering an undocumented red
> block in the texture and assuming it is available.* **Do not UV anything onto them; do not
> re-introduce a lashing, cord, wrap or binding in red.** Removing the blocks from the PNG is a
> separate asset change and is NOT authorized here.
>
> **(3a) Scope of the red ban — props only, NOT the HUD.** This is a **material** rule
> (`quality-bars.md` #3, "no arbitrary colours on a material"): a red cord on a whittled tool reads
> as an arbitrary tint, not as a material. **HUD semantics are a different domain and are
> untouched** — the player's HP bar is deliberately the one saturated red in the corner
> (`VitalRed #CC474D → WoundOrange → DarkBlood`), and that exclusivity is what makes it read as
> "vital" without a label. See [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) §2.2, which already
> reconciles this. **Nothing here bans red everywhere — it bans red on a weapon surface.**
>
> **(4) Naming reconciliation (mesh file vs code) — both are correct, neither is a typo.**
> The dagger's **mesh files are `wpn_knife_<tier>_01.fbx`** while its **catalog ids are
> `dagger_wood` / `dagger_stone` / `dagger_iron`** and its swing class is `AnimIdDaggerStab` →
> `WeaponClassDagger` (`Assets/Scripts/Runtime/Combat/WeaponCatalog.cs`). The wood-set export script
> records the reason inline: *"dagger reuses knife naming (§6a)"*
> (`tools/debug/bl_20_export_wood_set.py:30`). §4 below carries both columns so nobody "fixes" one
> to match the other. Likewise the **stone axe and stone spear keep the pre-tier canonical ids
> `"axe"` / `"spear"`** (no `_stone` suffix) — `WeaponCatalog.cs:23`/`:25`.

**Tonal anchor.** *The whole tool family reads as if the castaway whittled them himself,
from the same wood and the same will, on the same beach.* Every item is hand-carved,
toy-like, a little asymmetric — a child's-storybook adventurer's kit, never a smith's
arsenal. When four of these lie side by side in the gameplay cam, the player should read
ONE maker, ONE material, ONE world. Cohesion is the whole point; the silhouette tells you
*what it is*, the shared palette + shading tell you *it belongs here*.

**Route decision (Sponsor-locked 2026-06-19, memory `weapon-tool-unified-style-inhouse-blender-set` + DECISIONS.md).**
Cohesion is a *style-system* call, not per-asset sourcing. All items (axe, knife, sword,
spear, …) go through **ONE Blender MCP pipeline** sharing **ONE low-poly palette material**.
The currently-shipped axe (`Assets/Art/Props/CastawayAxe/` — Viktor.G "One-handed stylized
axe", Sketchfab **CC-BY**, baked photographic atlas) is the **OUTLIER** vs the flat-faceted
Zone-D world — it imports its own baked lighting and reads as a foreign, more-detailed object.
Treat it as a **placeholder to be re-made**, not the anchor. Hold `21h08_08` as the visual
target. Re-making in-house also retires the CC-BY attribution obligation.

**Pipeline reference:** Erik's deep-research note
[`team/erik-consult/blender-weapon-asset-pipeline-research.md`](../erik-consult/blender-weapon-asset-pipeline-research.md)
(PR #97). This spec is the *style* contract; Erik's note is the *production* contract
(Blender steps, FBX settings, MCP automation targets). Devon reads BOTH.

---

## 1. LOCKED PARAMETER #1 — Shading model: **Shade Smooth + Mark Sharp (faceted read)**

**Decision: SHADE SMOOTH + MARK SHARP on every facet-break edge — NOT literal Shade Flat.**
The *rendered read* is hard-faceted; the *technique* is smooth-shade-plus-mark-sharp.

**Why — verified against the LIVE world, not the board on paper:**

- **The tool refs are unambiguously faceted.** `21h08_08` (axe) and `21h07_20` (sword)
  show coarse flat planes with crisp hard breaks at every silhouette edge — you can count
  the facets. The cutting edge is a *distinct lighter chamfer plane*, not a painted line
  (geometry catching light). This is the look to match.
- **The live world is faceted too.** `style-guide-v2.md §4` locks the terrain to **hard
  normals (smoothing angle 0°)** and rocks/mountains to "big confident planes." The live
  terrain shader is `Assets/Shaders/LowPolyVertexColor.shader` (faceted vertex-color, hard
  normals). A *smooth-curved* weapon beside hard-faceted terrain would be the new outlier —
  exactly the mistake we're retiring with the CC-BY axe.
- **The technique, per Erik §E3 (Blender 4.1+):** Auto Smooth / Edge Split are gone. Apply
  **Shade Smooth** to the whole mesh, then **Mark Sharp** every edge that should read as a
  hard facet break (silhouette edges, blade-to-cheek transitions, grip bands, haft corners).
  Export FBX with **Smoothing = "Normals Only."** This gives the faceted look **without the
  vertex-count explosion** of literal Shade Flat, and the normals survive import (Unity
  Model Inspector → Normals = **Import**, not Calculate).
- **Reconciliation of the apparent CLAUDE.md vs board conflict:** CLAUDE.md says "low-poly
  smooth-shaded"; the board says "faceted flat-shaded." These describe the same result from
  two ends — *Shade Smooth is the Blender operation; the faceted look is the Mark-Sharp
  output.* The character uses ~60° smoothing (soft sausage limbs); the world + tools use
  **near-0° / all-edges-sharp** (crisp facets). Tools sit with the WORLD, not the character.

**Rule for Devon:** Shade Smooth the whole mesh → Mark Sharp every visually-distinct facet
break → FBX Smoothing = Normals Only → Unity Normals = Import. For the fully-faceted chunky
read of these tools, **mark essentially every edge sharp** (equivalent to Shade Flat, without
the vert blow-up). Sub-1.0 every channel; **URP/Unlit** material (see §2) so the facet
*colors* are the read, not engine lighting — the world's key light + the palette's baked
shade-steps carry the form. (HDR-clamp discipline carries from `style-guide-v2.md §5`.)

---

## 2. LOCKED PARAMETER #2 — Shared palette (EXTRACTED from the live world palette)

**ONE shared 128×128 PNG palette texture + ONE URP/Unlit material** (`Mat_WeaponPalette`)
for ALL weapons — ~1 draw call across the whole set (Erik §E1/E2, SRP Batcher batches by
shader variant). UV islands scale to ~0.001 and sit on the palette block for that part
(Erik §E6). **No per-asset baked atlas** — the atlas is exactly what makes the current axe
an outlier.

**These hexes are EXTRACTED, not invented** — every one is an existing world/tool anchor
from Uma's `style-guide-v2.md §3/§6` and `gameplay-ui-direction.md §1` (the carved-wood UI
palette from PR #83). The weapon set reuses the world's own colors so it reads of-the-world.

### The shared weapon palette — AS SHIPPED (re-read from the live PNG, 2026-07-27)

> **⚠ This table was corrected 2026-07-27 (`86cah7ym9` AC1).** The 2026-06-19 version listed 9
> aspirational slots and described intended usage. **The table below is the live texture**: 12
> distinct colours read out of `Assets/Art/Props/WeaponPack/weapon_palette.png` (128×128), and a
> **LIVE USAGE** column measured by sampling every UV coordinate in all 15 shipped FBXs against that
> PNG. **No hex was invented, changed or removed here** — this is a transcription of what ships.
> Where a slot's shipped usage differs from the 2026-06-19 intent, the intent line is struck and the
> measurement stands.
>
> Layout: **nine** full-height vertical stripes over a `#7A5230` background fill, plus **two small
> iron blocks** at the lower left. **For a stripe** the `u` centre is the addressable coordinate and
> `v` is free. **W1 is the background fill, not a stripe** — it has no stripe centre, so it is
> addressed at a pinned `(u, v)` in the left band; see its row. Note `iron-light` sits *inside* that
> left band at `v` ≈ 0.109–0.172 (px y 106–113) — the one place the background is interrupted there.

| Slot | Token | Hex | RGB (0–1, sub-1.0) | UV centre | **LIVE USAGE — measured from the 15 shipped FBXs** |
|---|---|---|---|---|---|
| W1 | `haft-wood` | `#7A5230` | 0.48, 0.32, 0.19 | **(0.0352, 0.0352)** — left band, px x 0–12. **NOT `u` 0.5000** *(⚠ corrected 2026-07-27: 0.5000 is the centroid of a non-contiguous background, and it samples the white `#E4E2DC` stripe at px x 61–70 — UV'ing a haft there paints it near-white)* | **WOOD + STONE hafts/shafts.** Present on all 5 wood and all 5 stone pieces. **Absent from every IRON piece** — confirms "no wood on the iron tier." Measured addressing: `(0.0352, 0.0352)` on all 10 wood+stone pieces (354 UVs) plus `(0.05, 0.05)` on the wood axe and wood pickaxe (104 UVs). `blender-asset-pipeline.md` §5 lists `Haft brown (0.05, 0.05)` — same band, agrees. |
| W2 | `haft-wood-shadow` / **`leather`** | `#5A3B22` | 0.35, 0.23, 0.13 | u 0.1406 | **The only block used by all 15.** Three jobs: wood-tier shade facets **and the fire-hardened spear tip**, stone-tier grip band, iron-tier segmented leather grip. |
| — | *(undocumented red)* | `#A33B30` | 0.64, 0.23, 0.19 | u 0.2344 | **RETIRED — 0 UVs across all 15 meshes.** Never appeared in the 2026-06-19 table; listed here only so it is found already crossed out. Do not use. |
| — | *(undocumented red)* | `#7E2C24` | 0.49, 0.17, 0.14 | u 0.3281 | **RETIRED — 0 UVs across all 15 meshes.** Same as above. Do not use. |
| W5 | `blade-steel` / **`iron-base`** | `#8C93A8` | 0.55, 0.58, 0.66 | u 0.4219 | **IRON tier only** — all 5 iron pieces (blade bodies + axe-head slab). *2026-06-19 intent said "sword + knife blade body"; the shipped stone blades use flint-grey W3 instead, so this block is now the iron-tier base.* |
| W6 | `edge-bevel` | `#E4E2DC` | 0.89, 0.89, 0.86 | u 0.5156 | **STONE + IRON only** — all 10. **Absent from every WOOD piece** (a whittled tool has no honed edge; see §4.1). Sub-1.0 — does NOT bloom. |
| W7 | `bone-fitting` / **`cut-facet-pale`** | `#CFC6AD` | 0.81, 0.78, 0.68 | u 0.6094 | **WOOD tier only** — all 5 wood pieces; this is the "tan cut facets" of the whittled read. *2026-06-19 intent said "crossguard, pommel, bindings"; no shipped piece uses it that way.* |
| W8 | `grip-wrap-red` | `#7E3A3A` | 0.49, 0.23, 0.23 | u 0.7031 | **RETIRED — 0 UVs across all 15 meshes** (Corrections 2026-07-19 + 2026-07-27, "remove the red things"). Stone grip = W2 band; iron grip = W2 leather. Survives only as a style-guide UI-era anchor. Do not use. |
| W3 | `flint-grey` | `#8E8A82` | 0.56, 0.54, 0.51 | u 0.7969 | **STONE tier only** — all 5 stone pieces (knapped biface bodies + stone spear tip). **Warm-grey flint, NOT blue-grey, NOT metal.** Pattern = modeled flake-scar facets, never a texture. |
| W4 / W9 | `dark-flint` | `#5C5853` | 0.36, 0.35, 0.33 | u 0.8906 | **STONE axe + STONE pickaxe only** — the struck-flake belly hollows on a heavy head. Not used by the stone knife / sword / spear. *(W4 and W9 were always the same hex; they are one block, listed once.)* |
| — | `iron-light` | `#A6ADBF` | 0.65, 0.68, 0.75 | (0.0625, 0.1406) | **IRON AXE ONLY.** This is the block that makes "hammered faceting stays only on the axe head" true in the mesh, not just in prose. |
| — | `iron-dark2` | `#6B7181` | 0.42, 0.44, 0.51 | (0.1875, 0.1406) | **All 5 IRON pieces** — the forged shade-step under the `iron-base` body. |

**Total: 12 blocks in the shipped texture — 9 live, 3 retired reds.** The two iron blocks
(`iron-light`, `iron-dark2`) were added during the 2026-07-03 iron burst
(`[[weapon-two-tier-style-stone-iron]]`) and are recorded here for the first time; **the
one-texture / one-material contract is intact** (`Mat_WeaponPalette` is referenced only by the
WeaponPack FBXs and `Assets/Resources/WeaponSetLineup.prefab`). The 128×128 grid still has room to
extend without disturbing existing UV placements — but per Correction 2026-07-27 the family is
CLOSED, so extension is a per-piece exception (`86cah7ym9` AC3), not a standing allowance.

**Discipline notes:**
- **Every channel sub-1.0** — `edge-bevel #E4E2DC` is deliberately off-white, NOT `#FFFFFF`;
  pure white blooms under the (reduced) bloom and breaks the crisp facet read
  (`style-guide-v2 §5` HDR-clamp carry).
- **Shade-steps are baked into the palette, not the shader.** Because the material is
  URP/Unlit, the form reads from (a) the world key light hitting the Mark-Sharp facets and
  (b) the W2/W4 darker palette blocks UV-assigned to shadow-side facets. This is the
  flat-shaded-palette pattern (Erik §E1) — the darks are *painted into the palette*, the
  facet breaks are *modeled*.
- **No new colors.** Every weapon part maps to one of the **9 LIVE blocks** (the 3 retired reds are
  not available). If a future item needs a colour not here, it's a spec amendment (escalate), not an
  ad-hoc per-asset hex — and per Correction 2026-07-27 the family is closed, so "a future item" means
  a Sponsor-authorized per-piece exception via `86cah7ym9` AC3.

---

## 3. Locked principles (Route A)

- **One shared material, no per-asset baked atlas.** `Mat_WeaponPalette` (URP/Unlit + the
  §2 palette PNG) on EVERY mesh. The baked photographic atlas is the outlier-maker — never
  repeat it.
- **Poly budget:** chunky low-poly, single mesh per item. Silhouette over surface detail.
  ~~Targets (Erik §E5): axe 200–400, knife 80–200, sword 200–500, spear 150–300 tris.~~
  **⚠ SUPERSEDED by what shipped — see §4.1 for the measured per-tier counts.** The shipped set runs
  **58–182 tris**, i.e. roughly a third of the 2026-06-19 target; the family landed leaner and the
  Sponsor passed it. Match §4.1's measured ranges, not these numbers.
- **Silhouette language:** bold, readable at orbit distance; exaggerated heads/blades;
  chunky toy proportions; NO thin/spindly forms. Each item's *function* reads instantly.
- **The edge-bevel is the family signet** (`style-guide-v2 §3` rule #2): a distinct
  near-white chamfer **plane** (`edge-bevel #E4E2DC`) along every hero working edge —
  modeled geometry catching light (Erik §E5: a physical thin inset/plane UV'd to the white
  block), NOT a shader effect, NOT a painted line. ~~Every cutting tool gets it on its hero edge.~~ **⚠ CORRECTED
  2026-07-27: the edge-bevel is the signet of the STONE and IRON tiers only — no wood piece wears
  it** (measured: `#E4E2DC` has zero UVs on all 5 wood meshes). That absence is the point — a
  whittled stick has no honed edge, and *gaining* the white bevel at the stone rung is part of how
  the progression reads as "better gear." See §4.1.
- **Shared handle/grip motif:** same `haft-wood` + ~~same gentle hand-made bend (2–5°, Erik
  §E5)~~ + same grip proportion across ALL items — "made by the same castaway." **⚠ CORRECTED
  2026-07-27: the 2–5° bend is RETIRED — the whole family has STRAIGHT handles** (Sponsor decision
  2026-06-23; `blender-asset-pipeline.md` §3 and its §11 checklist both carry it). Measured: haft
  ring centroids are `cx +0.0000 / cy +0.0000` on every 6-vert ring of `wpn_axe_{wood,stone,iron}_01`
  — zero drift, dead straight. The hand-made imperfection lives in the head, grip band and facets,
  **never in a curved haft.** See §4.1.
- **Mild hand-made asymmetry throughout** — nothing CNC-perfect; the toy is carved.
- **In-hand scale:** normalized to the castaway's right-hand bone; the current axe sets the
  reference scale — match it.
- **Consistent grip-point pivot + +Z-forward axis** (Erik §E7): origin at grip midpoint,
  blade pointing +Z in Blender (→ +Y in Unity post axis-conversion), so ONE `HeldTool` rig
  generalizes from today's `HeldAxeRig` and any item slots in WITHOUT per-item offset tuning.
- **In-house only — no CC assets** (no attribution obligations).

## 4. The shipped family — 3 tiers × 5 types

> **⚠ REWRITTEN 2026-07-27 (`86cah7ym9` AC1).** The old §4 was a **4-row, single-tier** table that
> still described a red lashing and a wrapped grip. It contradicted the shipped weapons on three
> counts: it was missing the **pickaxe** entirely, it named colours the shipped pieces do not use,
> and it described a tier structure that has since closed. Everything below is measured from
> `Assets/Art/Props/WeaponPack/` on 2026-07-27.

### 4.1 The three tier recipes — the surface language

The tier is the **surface**; the type is the **silhouette** (§4.2). Read together, a player should
tell tier and type apart across the clearing without a label. **"Better gear at a glance": wood =
crude / pale / light · stone = knapped / grey / rugged · iron = clean / forged / darker-cool.**

| | **WOOD** (first rung) | **STONE** (first craft) | **IRON** (upgrade) |
|---|---|---|---|
| **Reads as** | *He whittled this yesterday and it barely works.* Crude, pale, light. | *He found a sharp rock and bound it well.* Knapped, grey, rugged. | *This came out of a forge.* Clean, forged, darker and cooler. |
| **Body / head** | Whittled **haft-brown** `#7A5230` with **tan cut facets** `#CFC6AD` — the pale worked-wood scars of a knife going at a branch | Knapped grey **biface** `#8E8A82`: multi-point outline + lens cross-section + seeded facet jitter; **`#5C5853` struck-flake belly hollows on the heavy heads (axe + pickaxe)** | **Flat-smooth single-tone blades** `#8C93A8` — thin planes, thin midrib. Sponsor verbatim: *"more like flat smooth blades than chunky metal pieces."* |
| **Hammered faceting** | n/a | n/a | **AXE HEAD ONLY** — flat slab + hammered 3-tone tri-facet cheeks (`#8C93A8` + **`#A6ADBF`** + `#6B7181`) + wedge-sharp bit. `#A6ADBF` appears on **`wpn_axe_iron_01` and nowhere else** — that measurement *is* the rule. Every other iron piece is base + `#6B7181` shade-step. |
| **Cutting edge** | **NONE — no `edge-bevel`.** A whittled stick has no honed edge; the tier is defined partly by *lacking* the family signet | **`#E4E2DC` white band on the cutting arc** — reads as the fresh-knapped edge | **`#E4E2DC` honed rim** along the working edge |
| **Haft / grip** | Haft-brown `#7A5230` + `#5A3B22` shade facets | **Straight 6-sided `#7A5230` haft + `#5A3B22` (WOOD_DARK) grip band.** **NO lashing, NO rawhide, NO cord** | **Iron handle + segmented LEATHER grip** (`#5A3B22`). **No wood on the iron tier** — `#7A5230` has zero UVs on all 5 iron meshes |
| **Spear tip** | **Fire-hardened: the dark `#5A3B22` block** — charred, not sharpened | Knapped flint `#8E8A82` | Forged `#8C93A8` |
| **Measured tris** | **58–90** (spear 58 · axe 66 · pickaxe 80 · sword 82 · knife 90) | **88–142** (pickaxe 88 · axe 100 · spear 106 · knife 138 · sword 142) | **140–182** (axe 140 · spear 146 · pickaxe 154 · knife 158 · sword 182) |
| **Provenance** | Sponsor PASS 2026-07-18 (DECISIONS.md), integrated PR #304 | Sponsor-locked 2026-07-03 burst, ticket `86cahnmf6` | Sponsor-locked 2026-07-03 burst, ticket `86cahnmf6` |

**The tri count climbing with the tier is deliberate and is part of the read** — the wood rung is
crude *because* it is cheap geometry. Do not "improve" a wood piece up into the stone budget.

### 4.2 The five types — the silhouette language

Silhouette is **tier-invariant**: an axe reads as an axe at every rung. Each type owns one swing
class, and that mapping is closed (Correction 2026-07-27 item 2).

| Type | Read | Mesh files | Catalog ids | Swing class |
|---|---|---|---|---|
| **Axe** | Wedge head on a stout, **straight** handle | `wpn_axe_{wood,stone,iron}_01.fbx` | `axe_wood` · **`axe`** *(stone — pre-tier canonical id)* · `axe_iron` | `AnimIdAxeChop` → `WeaponClassAxe` |
| **Pickaxe** | Twin-beak head, symmetric, on a long straight haft | `wpn_pickaxe_{wood,stone,iron}_01.fbx` | `pickaxe_wood` · `pickaxe_stone` · `pickaxe_iron` | `AnimIdPickaxeMine` → `WeaponClassPickaxe` |
| **Spear** | Longest overall — long shaft, compact point; thin-but-NOT-spindly (the chunky rule holds) | `wpn_spear_{wood,stone,iron}_01.fbx` | `spear_wood` · **`spear`** *(stone — pre-tier canonical id)* · `spear_iron` | `AnimIdSpearThrust` → `WeaponClassSpear` |
| **Dagger** | Smallest silhouette, shortest grip, single short blade | **`wpn_knife_{wood,stone,iron}_01.fbx`** *(file says knife, code says dagger — see Correction 2026-07-27 item 4; neither is a typo)* | `dagger_wood` · `dagger_stone` · `dagger_iron` | `AnimIdDaggerStab` → `WeaponClassDagger` |
| **Sword** | Longest blade + crossguard + grip | `wpn_sword_{wood,stone,iron}_01.fbx` | `sword_wood` · `sword_stone` · `sword_iron` | `AnimIdSwordSlash` → `WeaponClassSword` |

*(Ids + swing classes read from `Assets/Scripts/Runtime/Combat/WeaponCatalog.cs`, 2026-07-27.)*

### 4.3 RETIRED — do not resurrect

Listed as struck-out rows rather than deleted, so a reader who goes looking finds a "no" instead of
a gap. **Each of these is a Sponsor decision, not an editorial preference.**

| Retired | Retired by | Status now |
|---|---|---|
| ~~Red lashing / cord / rawhide binding on the axe head~~ | Sponsor, "remove the red things" (Corrections 2026-07-19 + 2026-07-27) | **Not on any shipped piece.** The stone axe head binds with no cord. Do not re-add. |
| ~~`grip-wrap-red #7E3A3A` (W8) as a grip wrap~~ | same | **0 UVs across all 15 meshes.** Grips are `#5A3B22` — a band on stone, segmented leather on iron. |
| ~~The undocumented reds `#A33B30`, `#7E2C24`~~ | same (never authorized in the first place) | **0 UVs across all 15 meshes.** Present in the PNG as dead pixels only. |
| ~~The "bone" tier~~ | Sponsor 2026-07-27, `86cah7ym9` comment `90150245469789` | **Three tiers, final.** Reversal path = one per-piece PR via `86cah7ym9` AC3 — not an open tier. |
| ~~A sixth weapon type (club / mace / bow / sling)~~ | Sponsor 2026-07-27, same comment | **Five types, final.** Blunt and ranged both surfaced and declined. |
| ~~`bone-fitting` W7 as crossguard / pommel / binding~~ | superseded by what shipped | The block is live but does a **different job**: the wood tier's pale cut facets (§4.1). No shipped piece uses it as a fitting. |

**The red ban is a MATERIAL rule and stops at the weapon surface.** The HUD's HP red is untouched
and deliberate — see Correction 2026-07-27 item 3a and
[`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) §2.2.

## 5. Production / rig notes (for Devon — defer to Erik's note for full steps)

- Produce the family **as a SET in one Blender MCP pass** (Erik Phase 0–6) — same material,
  same palette, same grip pivot — not item-by-item.
- **Shade Smooth + Mark Sharp** per §1; FBX **-Y Forward / Z Up / Normals Only / FBX Unit
  Scale**, Apply All Transforms first (Erik §E4). Unity: Normals=Import, Bake Axis
  Conversion ON, Material Creation Mode=None (assign `Mat_WeaponPalette` manually).
- **Re-make the hero axe** in this pipeline; retire `CastawayAxe` (Viktor.G CC-BY) + its
  license file once the in-house axe ships.
- **Generalize** `HeldAxe.cs` / `HeldAxeRig.cs` → a `HeldTool` rig (the held-axe soak-tuning
  already solved the hard part — don't redo it per item).
- **Attack swings = a SEPARATE Sponsor-provided Mixamo clip per weapon class** (Sponsor 2026-07-19;
  DECISIONS.md "2026-07-19 — Combat cluster order," ticket `86caffwv5`) — axe / pickaxe / knife /
  sword / spear each get their OWN clip. Do **NOT** share the axe clip for the pickaxe (the earlier
  economy proposal is resolved-rejected). Wire per the shipped chop pattern
  (`CastawayCharacter.TriggerChop`, `swingImpactDelaySeconds`) — animator-driven, not procedural.
- **1 Unity-build slot** (single-runner cap) — this is the Unity-heavy lane; sequence it
  (`single-unity-build-slot-serializes-orchestration`). The Blender/spec work is the
  non-Unity lane and fans out.
- **MCP automation targets** (Erik §"Automation Targets"): palette-PNG generation, material
  setup, UV-island scale-to-0.001 + palette-block placement, transform-apply, normals
  recalc, FBX export — all scriptable. Shape design (blade profile, edge-loop placement,
  Mark-Sharp selection) is human-iterated in-viewport (`get_viewport_screenshot` against the
  refs).

## 6. Acceptance (proposed)

- All family items in-engine share `Mat_WeaponPalette` + the §2 palette; no per-asset atlas;
  Frame Debugger shows them batching to ~1 SetPass.
- Lined up side by side in the gameplay cam, they read as ONE family (faceted shading +
  silhouette + grip motif + edge-bevel consistent).
- Each held in-hand at correct scale via the shared `HeldTool` rig.
- Hero axe re-made; Viktor.G CC-BY asset + license retired.
- Every channel sub-1.0 (HDR-clamp); `edge-bevel` is off-white `#E4E2DC`, never pure white.
- Shipped-build capture evidence (per the capture gate) before merge.

---

## 7. ~~⚠️ Sponsor decisions — 3 OPEN QUESTIONS~~ → ALL CLOSED (kept for rationale)

> **⚠ ALL THREE ARE CLOSED — kept for rationale only (marked 2026-07-27, `86cah7ym9` AC1).**
> This section is **history, not a live ask.** All 15 pieces shipped and were individually
> Sponsor-approved (`86cahnmf6` for stone+iron, DECISIONS.md 2026-07-18 + PR #304 for wood), so the
> shipped mesh — not the recommendation below — is the answer.
> **Q1 (spear tip)** — answered by ship, and it went Uma's way at the stone rung: **wood = the dark
> `#5A3B22` fire-hardened tip · stone = knapped flint `#8E8A82` · iron = forged `#8C93A8`** (§4.1).
> Bone was never used and the bone tier is now retired outright.
> **Q2 (sword bevel extent)** — answered by ship: both the stone and iron swords wear `#E4E2DC` and
> were approved as modelled. **Read the shipped mesh, do not re-derive extent from the
> recommendation below.**
> **Q3 (Blender-MCP variant)** — moot; the family shipped through the working pipeline.
> **Nothing in §7 is a pending Sponsor decision. Do not route it to the feature wave.**

These three *were* flagged for the Sponsor. Uma's recommendation on each, preserved for rationale:

**Q1 — Spear-tip material (bone / stone / iron).** No spear ref exists on the board.
> **Uma recommends: STONE** (`flint-grey #8E8A82` W3 body + `dark-flint #5C5853` W9 facets,
> warm-grey not blue-grey — the same knapped-flint pair as the axe head).
> *Rationale:* the castaway is early-survival, shipwrecked, whittling his own kit — a
> lashed-stone spear-point is the most on-narrative ("found a sharp rock, bound it to a
> stick"), and it reuses the world's own rock/flint anchor (now the axe-head material too) so
> the spear reads of-the-world with ZERO new color and visibly belongs to the same maker as
> the axe. Angular faceted stone tip (Erik §E5: stone = angular facets) sits perfectly
> with the Mark-Sharp shading. Bone (`bone-fitting #CFC6AD` W7) is the strong alternate if the
> Sponsor wants a softer/lighter read; iron would re-introduce a "forged" register that
> fights the hand-whittled anchor (not recommended). Both alternates are already in the
> palette, so this is a one-click call with no palette change either way.

**Q2 — Sword white edge-bevel along the FULL blade length, or just the tip/edge?**
> **Uma recommends: FULL working-edge length, ONE side only (the cutting edge), tapering off
> before the crossguard.** *Rationale:* the edge-bevel is the family signet (§3) — the sword
> must wear it to belong, and `21h07_20`/`21h07_42` both show a continuous lighter rim down
> the blade. Running it the full *cutting-edge* length (not a full-perimeter outline) keeps
> the "this is the sharp part" read and matches the axe's single-edge logic. ONE side keeps
> the tri-count in budget (§3) and avoids a symmetric "chrome-trimmed" look that would read
> machined, not carved. A full-perimeter white outline is NOT recommended (reads toy-plastic,
> breaks the hand-made anchor).

**Q3 — Blender-MCP server variant (ahujasid community vs official).**
> **Uma recommends: defer to Devon/infra — this is an implementation detail, not a style
> call.** *Style-side note:* the `execute_blender_code` Python-API path (palette-PNG gen, UV
> scale-to-0.001 + block placement, FBX export) that this spec depends on is present in the
> **ahujasid/blender-mcp community server** (Erik §E8, the primary cited implementation; the
> official server returned 403 at research time). So unless Devon confirms the official
> server is live AND exposes `execute_blender_code`, the community server is the working
> default. **No style impact either way** — both expose the bpy Python the pipeline needs.

---

## Cross-references

- [`style-guide-v2.md`](style-guide-v2.md) §3 (tool language + axe worked example) / §6
  (world palette anchors — the §2 hexes are extracted from here) — the load-bearing source.
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) §1 — the carved-wood UI palette
  (PR #83); the weapon `haft-wood`/`edge`/`bone` family is the same wood the UI is carved from.
- [`team/erik-consult/blender-weapon-asset-pipeline-research.md`](../erik-consult/blender-weapon-asset-pipeline-research.md)
  — the production contract (Blender steps, FBX settings, MCP automation). PR #97.
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) — the board; tools
  family `21h06_54`/`21h07_20`/`21h07_42`/`21h08_08`.
- DECISIONS.md + memory `weapon-tool-unified-style-inhouse-blender-set` — the Route A lock.
- **DECISIONS.md 2026-07-18 (Wood-tier PASSED) + 2026-07-19 (SWINGS-first cluster order) + memory
  `weapon-two-tier-style-stone-iron` + ticket `86caffwv5`** — the live 3-tier reality, red removal,
  and per-weapon swing-clip decision (Corrections 2026-07-19 at the top of this spec).
  Combat FEEL detail: `combat-cluster-design-brief.md` (PR #320).
- **Ticket `86cah7ym9` (AC1 = this correction; AC2 = the closing Sponsor decisions, comment
  `90150245469789`; AC3 = the reusable per-piece recipe for any future addition)** — the source of
  Correction 2026-07-27. **AC3 is the only route by which this family grows.**
- [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) §2.2 — the red reconciliation. The removal here
  is a **prop-material** rule (`quality-bars.md` #3); the HUD's `VitalRed` HP bar is a different
  domain and is untouched. Read it before concluding this spec bans red anywhere else.
- **Ground truth for §2/§4 (re-measurable at any time):** `Assets/Art/Props/WeaponPack/` — 15 FBXs
  + `weapon_palette.png` + `Mat_WeaponPalette.mat`; `Assets/Scripts/Runtime/Combat/WeaponCatalog.cs`
  (ids + `AnimId*` swing classes); `tools/debug/bl_20_export_wood_set.py` (wood-row export, and the
  `"dagger reuses knife naming"` note); `art-src/weapons_reauthor.blend` (the source rows).
