# Hand-tool / weapon STYLE SPEC — Route A (in-house Blender matched set)

**Status:** FINALIZED 2026-06-19 (Uma, ticket `86cabh8rt`). Supersedes the DRAFT seed.
Locks the two open parameters (shading model + palette hexes) against the **live world
look** — verified against the inspiration board, the tool refs, and the live world
materials/shaders — not decided in the abstract.

> **Correction 2026-06-19 (axe-head material).** The hero axe HEAD is **STONE / knapped
> FLINT**, not a red metal — the Sponsor rejected a red axe head (memory
> `weapon-asset-material-honest-pattern-via-geometry`: a tool reads as its MATERIAL; a red
> metal head reads forged, fighting the hand-whittled anchor). The head maps to
> `flint-grey #8E8A82` + `dark-flint #5C5853` (the value already used in the built axe), and
> its surface PATTERN is **knapped flake-scar facets modeled into the geometry** — NOT a
> detail-texture / normal-map (preserves the shared ~1-draw-call palette material). The red
> `grip-wrap-red #7E3A3A` LASHING (the cord wrap that binds head to haft) STAYS red — that's
> rope, not the head.

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

### The shared weapon palette (the actual hex list for the texture)

| Slot | Token | Hex | RGB (0–1, sub-1.0) | Source (extracted from) | Usage across the set |
|---|---|---|---|---|---|
| W1 | `haft-wood` | `#7A5230` | 0.48, 0.32, 0.19 | `style-guide-v2 §6` haft/trunk wood | Axe/knife/spear/sword haft + shaft — the family's shared handle wood |
| W2 | `haft-wood-shadow` | `#5A3B22` | 0.35, 0.23, 0.13 | derived shade-step of W1 (–1 value) | Haft shadow facets + grip-wrap shadow band (the shade-step the faceted read needs) |
| W3 | `flint-grey` | `#8E8A82` | 0.56, 0.54, 0.51 | `style-guide-v2 §6` world rock (warm-grey); value already in the built axe | **Axe head** (knapped-flint body) + any stone tool head + stone spear-tip (Q1) — **warm-grey flint, NOT blue-grey, NOT metal**. Surface pattern = modeled flake-scar facets, not a texture. |
| W4 | `dark-flint` | `#5C5853` | 0.36, 0.35, 0.33 | knapped working-flint dark; value already in the built axe (same as W9) | Axe-head shadow cheek + the inner shadow facets between flake scars — the working-flint dark that makes the modeled flake-scar read |
| W5 | `blade-steel` | `#8C93A8` | 0.55, 0.58, 0.66 | sword/curved-blade refs `21h07_20`/`21h07_42` (cool slate-steel body) | **Sword + knife blade body** — the cool slate of the ref blades |
| W6 | `edge-bevel` | `#E4E2DC` | 0.89, 0.89, 0.86 | `style-guide-v2 §3/§6` tool edge bevel | **The signature near-white chamfer plane** on every hero edge (axe bit, sword/knife edge). Sub-1.0 — does NOT bloom. The identity detail of the whole family. |
| W7 | `bone-fitting` | `#CFC6AD` | 0.81, 0.78, 0.68 | `style-guide-v2 §3/§6` pommel/crossguard bone | Crossguard, pommel, bone spear-tip alternate (see Q1), bindings — off-white bone family |
| W8 | `grip-wrap-red` | `#7E3A3A` | 0.49, 0.23, 0.23 | `style-guide-v2 §3/§6` grip wrap | Segmented grip wrapping (sword/curved blade) **+ the axe-head LASHING** (the red cord that binds head to haft) — dark desat red, chunky segments. This is rope, NOT the head. |
| W9 | `dark-flint` | `#5C5853` | 0.36, 0.35, 0.33 | knapped working-flint dark; value already in the built axe | The deepest knapped-flint facets on the axe head (struck flake hollows) + the darkest stone-tip facets — the working-flint dark that makes the flake-scar read |

**Total: 9 slots** — base anchors (`haft-wood` W1, `flint-grey` W3, `blade-steel` W5,
`edge-bevel` W6, `bone-fitting` W7, `grip-wrap-red` W8) + derived shade-step (`haft-wood-shadow`
W2, which the faceted shade-read needs) + the knapped-flint pair (`flint-grey` W3 body /
`dark-flint` W9 dark, with W4 carrying the same dark-flint `#5C5853` as the axe-head shadow
step). A 128×128 grid holds these in generous 16-px blocks with room to extend without
disturbing existing UV placements. (W4 and W9 share the dark-flint hex by design — W4 is the
axe-head shadow facet, W9 is the deep stone-tip facet; both are the same struck-flint dark.)

**Discipline notes:**
- **Every channel sub-1.0** — `edge-bevel #E4E2DC` is deliberately off-white, NOT `#FFFFFF`;
  pure white blooms under the (reduced) bloom and breaks the crisp facet read
  (`style-guide-v2 §5` HDR-clamp carry).
- **Shade-steps are baked into the palette, not the shader.** Because the material is
  URP/Unlit, the form reads from (a) the world key light hitting the Mark-Sharp facets and
  (b) the W2/W4 darker palette blocks UV-assigned to shadow-side facets. This is the
  flat-shaded-palette pattern (Erik §E1) — the darks are *painted into the palette*, the
  facet breaks are *modeled*.
- **No new colors.** Every weapon part maps to one of the 9 slots. If a future item needs a
  color not here, it's a spec amendment (escalate), not an ad-hoc per-asset hex.

---

## 3. Locked principles (Route A)

- **One shared material, no per-asset baked atlas.** `Mat_WeaponPalette` (URP/Unlit + the
  §2 palette PNG) on EVERY mesh. The baked photographic atlas is the outlier-maker — never
  repeat it.
- **Poly budget:** chunky low-poly, single mesh per item. Targets (Erik §E5): axe 200–400,
  knife 80–200, sword 200–500, spear 150–300 tris. Silhouette over surface detail.
- **Silhouette language:** bold, readable at orbit distance; exaggerated heads/blades;
  chunky toy proportions; NO thin/spindly forms. Each item's *function* reads instantly.
- **The edge-bevel is the family signet** (`style-guide-v2 §3` rule #2): a distinct
  near-white chamfer **plane** (`edge-bevel #E4E2DC`) along every hero working edge —
  modeled geometry catching light (Erik §E5: a physical thin inset/plane UV'd to the white
  block), NOT a shader effect, NOT a painted line. Every cutting tool gets it on its hero edge.
- **Shared handle/grip motif:** same `haft-wood` + same gentle hand-made bend (2–5°, Erik
  §E5) + same grip proportion across ALL items — "made by the same castaway."
- **Mild hand-made asymmetry throughout** — nothing CNC-perfect; the toy is carved.
- **In-hand scale:** normalized to the castaway's right-hand bone; the current axe sets the
  reference scale — match it.
- **Consistent grip-point pivot + +Z-forward axis** (Erik §E7): origin at grip midpoint,
  blade pointing +Z in Blender (→ +Y in Unity post axis-conversion), so ONE `HeldTool` rig
  generalizes from today's `HeldAxeRig` and any item slots in WITHOUT per-item offset tuning.
- **In-house only — no CC assets** (no attribution obligations).

## 4. Per-item silhouette notes

| Item | Read | Palette mapping | Notes |
|---|---|---|---|
| **Axe** | wedge head on a stout bent handle | head=`flint-grey` W3 (+W4/W9 `dark-flint` flake-scar shadows), bit=`edge-bevel` W6, haft=`haft-wood` W1 (+W2), lashing=`grip-wrap-red` W8 | Re-make of the hero axe; the family's scale + grip reference. Head is **knapped FLINT** (warm-grey stone), its flake-scar pattern **modeled as facets** (not a texture). White chamfer bit, bent brown haft, **red cord lashing** binding head to haft. Match the built axe's flint head (`#8E8A82`/`#5C5853`), not a metal head. |
| **Knife** | short single blade, stubby grip | blade=`blade-steel` W5, edge=`edge-bevel` W6, grip=`haft-wood` W1 (+W2 wrap) | Shortest grip, smallest silhouette. Edge-bevel along the cutting edge. |
| **Sword** | long blade + crossguard + wrapped grip | blade=`blade-steel` W5, edge=`edge-bevel` W6 (see Q2), guard/pommel=`bone-fitting` W7, grip=`grip-wrap-red` W8 (+W2) | Longest blade; crossguard is the family's only "extra" detail beat. Build to `21h07_20`. |
| **Spear** | long shaft + compact point | shaft=`haft-wood` W1 (+W2), point=see **Q1** | Longest overall; thin-but-NOT-spindly shaft (chunky rule holds). No board ref exists — see Q1. |

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

## 7. ⚠️ Sponsor decisions — 3 OPEN QUESTIONS (Erik's, with Uma's recommendation)

These three are flagged for the Sponsor. Uma's recommendation on each:

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
