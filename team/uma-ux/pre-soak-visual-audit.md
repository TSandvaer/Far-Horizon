# Pre-Soak Visual Audit — surface the NEXT likely soak-rejects BEFORE the next soak

**Owner:** Uma · **Type:** proactive visual audit (direction only, no implementation) · **Status:** PUNCH-LIST for the current fix-wave.
**Why this exists:** the Sponsor has been catching visual issues ONE PER SOAK (hair spike→tuft, clothes-too-subtle, axe-placement, gray slab) — each one a full reject→fix→re-soak round-trip. This audit looks ahead to fold the high-severity next-issues into the CURRENT wave instead of paying another round-trip each.

**What I audited against (ground truth):**
- Board v2 PNGs in [`inspiration/`](../../inspiration/) — esp. `21h00_32` (castaway pole), `21h08_08` (axe pole), `21h10_44` (tree/cloud/rock/grass set), `21h13_31` (tree-field world-feel), `21h16_13` (mountain-valley vista).
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) + [`style-guide-v2.md`](style-guide-v2.md) + [`castaway-style-v2.md`](castaway-style-v2.md) + [`beach-water-direction.md`](beach-water-direction.md).
- **The SHIPPED scene source** (what the Sponsor actually sees, not the spec): `MovementCameraScene.cs`, `BootstrapProject.cs`, `WorldBootstrap.cs`, `LowPolyZoneGen.cs`, `QualityPassGen.cs`, `CastawayCharacter.cs`; the integrated FBX textures (`MiniChibiKid/`, `CastawayAxe/`).

**Method note (honesty):** the Sponsor's stamp-`31ce95c` soak screenshots are NOT in my context as images (they live in the orchestrator's session + gitignored `Captures/`, not the repo). So this audit reads the SHIPPED SCENE SOURCE as ground truth — what the code bakes into Boot.unity — cross-referenced against the board PNGs. Every item below cites the specific source line/asset that produces the issue, so each is verifiable, not a guess. Items that genuinely need the rendered frame to confirm are labelled **[confirm-in-soak]**.

**Excluded (already in-flight per dispatch — Devon/Drew own these, NOT re-listed):** hair-front-tuft, clothes-weathering/too-subtle, axe placement, gray slab. Where an in-flight item has an *adjacent* un-flagged facet, I note the adjacency explicitly and scope only the new part.

---

## The ranked top-5 likely-next-issues (fold the HIGHs into the current wave)

| Rank | Item | Severity | One-line |
|---|---|---|---|
| **1** | Axe is a *realistic* rustic hatchet, not the board's flat barn-red faceted axe | **HIGH** | Placement is fixed; the STYLE still mismatches the board's whole tool language — adjacent to the in-flight placement item but a distinct call. |
| **2** | No clouds anywhere in the sky | **HIGH** | The board puts bright teal puffy clouds in the sky of *every* world ref; an empty sky reads flat + un-toylike. |
| **3** | No far-horizon mountain/vista backdrop | **HIGH** | The north-star is literally "the far horizon"; the board's vistas are snow-capped faceted peaks. The horizon is currently empty fog. |
| **4** | Sky tint reads cool/muted, not bright open daylight | **MED** | `SkyTint` is cool-blue + exposure modest; board skies are brighter, warmer-clean, more saturated. |
| **5** | Held-axe size/scale read **[confirm-in-soak]** | **MED** | The 267× bone-scale comp is hand-tuned; a too-big/too-small hatchet is the next-most-likely placement-adjacent reject. |

Everything below details each, plus the MED/LOW tail.

---

## HIGH — fold these into the current wave

### 1. The axe is a realistic sourced hatchet, not the board's flat-faceted barn-red axe — HIGH
**What's off:** The shipped axe is the sourced "rustic hatchet" FBX (`Assets/Art/Props/CastawayAxe/`). Its baseColor atlas (`Material_002_baseColor_png.png`) is a *realistic* texture — wood-grain striations, metallic gradient blade, and a literal **spiral screw/bolt motif** on the head. The board's axe (`21h08_08`) is the opposite: **flat barn-red head, a single crisp near-white edge-bevel plane, a smooth gently-bent brown haft, zero surface texture.** The board's *entire tool language* (style-guide-v2 §3) is "faceted flat-shaded + one white edge-bevel + no painted detail." The shipped axe violates all three.

**Why it's likely the next reject:** the axe is the loop's hero tool, in-hand, on-screen constantly. The Sponsor already soaked the axe twice (re-do `86ca8ce6y`, then placement). Once placement reads right, the eye moves to the *surface* — and a wood-grained, screw-bolted, glinty-metal hatchet next to a chunky flat-shaded toy castaway is exactly the "realistic prop breaks the toy" failure style-guide-v2 §0 warns about. This is **adjacent to the in-flight placement item but a distinct call** (placement = where it sits in the hand; style = what it's made of).

**Fix suggestion:** Either (a) **re-tune the axe materials toward the board** — strip the realistic atlas, drive flat barn-red `#A33B30` (0.64,0.23,0.19) head + pale-steel `#E4E2DC` edge-bevel + warm-brown `#7A5230` haft per style-guide-v2 §6, matte smoothness (~0.1), no metallic; OR (b) **replace with a Blender-MCP scripted faceted axe** built to `21h08_08` (the style-guide-v2 §7 #1 "stylize the hero axe" ticket-shape — it was ranked the cheapest high-visibility win and may have been skipped when the sourced FBX landed). Option (b) is more on-board but heavier; option (a) is the cheap fold-in. **Recommend (a) for this wave, (b) as the clean follow-up.** Flag to Sponsor: "the held axe is a realistic hatchet — want it re-styled flat-faceted-barn-red to match the board, or is the rustic look intentional?"

### 2. No clouds in the sky — HIGH
**What's off:** There are zero clouds in the shipped scene (`grep -i cloud Assets/Scripts/` → nothing). The board puts **bright teal/cyan puffy faceted clouds** in the sky of essentially every world reference — `21h10_44` (a whole top row of cloud variants), `21h13_31` (clouds over the tree-field), `21h16_13` (clouds incl. a raining one over the valley). Clouds are a *signature* element of the board's toy-diorama sky (style-guide-v2 §4 "Clouds: bright teal/cyan puffy cartoon clouds — same blob language as the canopies").

**Why it's likely the next reject:** when the Sponsor orbits up/out (the orbit camera allows pitch to 70° + the horizon), the sky fills a big share of the frame and it's currently an empty gradient. After the ground-level items (axe, character) settle, the empty sky is the most board-divergent large surface left. The board's clouds are too prominent across too many refs for their absence to go unnoticed.

**Fix suggestion:** add a sparse set of **blob-cluster clouds** — the exact same `LowPolyMeshes.BlobCanopy`/faceted-spheroid language as the tree canopies, but tinted bright pale-cyan (sub-1.0, e.g. `0.78,0.90,0.94`), placed high and far as a few floating clusters. Cheap (reuses the canopy mesh path), editor-time serialized into the env root like the scatter. Keep them FEW and chunky (board shows ~4-6 in frame, not an overcast). Honors HDR-clamp (sub-1.0 so post doesn't bloom them to white). This is a small, high-delight, board-faithful fold-in.

### 3. No far-horizon mountain/vista backdrop — HIGH
**What's off:** The horizon is empty — terrain fades into warm fog with nothing behind it (`grep -i mountain|peak|snow Assets/Scripts/` → nothing in the world build). The board's wide shots (`21h16_13`, `21h21_30`, `21h22_05`) all put **faceted grey-to-snow-capped mountains** on the horizon as the vista backdrop. Per art-direction.md the mountains ARE the "vista backdrop" and the whole game's north-star is **"the far horizon — a journey, a destination."** Right now there's no destination on the horizon to journey toward — just fog.

**Why it's likely the next reject:** this is the single biggest "the world feels BIG and ENDLESS" lever, and it's the literal name of the game. The Sponsor's locked core feel is "world feels BIG and ENDLESS — a journey." An empty fogged horizon undercuts exactly that. It's lower-frequency than the axe (you only see it when orbiting to the horizon) which is why it ranks #3 not #1, but its absence is a direct hit on the north-star.

**Fix suggestion:** a ring (or seaward-arc) of **distant faceted low-poly mountains** — big hard-faceted grey-warm triangles with a snow-white cap facet (`21h16_13`), placed far out past the playable terrain, NON-walkable / no collider (pure backdrop, never touches NavMesh). Scale them large + push them past the fog mid-distance so they read as a hazed far destination, not a wall. Editor-time serialized into the env root. Devon/Drew call: a few big static meshes; cheap relative to its north-star payoff. **This is the highest-leverage "make it feel like Far Horizon" fold-in** — flag for Sponsor as "want mountains on the horizon to journey toward?" since adding a whole vista layer is a scope call.

---

## MED — strong candidates; fold if the wave has room

### 4. Sky reads cool/muted, not the board's bright open daylight — MED
**What's off:** `QualityPassGen.BuildGradientSkybox` sets `_SkyTint (0.55,0.62,0.72)` (a cool-desaturated blue), `_GroundColor (0.78,0.72,0.58)` warm horizon, `_Exposure 1.05`. The board skies (`21h13_31`, `21h16_13`) are **brighter, cleaner, more saturated open-daylight blue** — cheerful, not hazy/muted. style-guide-v2 §4 explicitly calls for "a clearer, brighter sky… open daylight, not hazy-warm." The current tint is more muted than the board.

**Why MED not HIGH:** it's a dial-tune, not a missing element, and the warm-horizon ground color is correct. But a muted sky pulls the whole frame's mood toward overcast vs the board's sunny-toy cheer — and it compounds with #2/#3 (empty muted sky = the most un-board part of an orbit-up view).

**Fix suggestion:** lift `_SkyTint` toward a brighter, slightly-more-saturated daylight blue (e.g. `0.50,0.66,0.86`), nudge `_Exposure` up modestly (~1.1-1.15) — but **re-verify against the HDR-clamp + bloom** (style-guide-v2 §6: sub-1.0, don't let the brighter sky bloom to white through the post stack). A soak-tunable pair of values; pin Tess on "sky reads bright-cheerful-daylight, not muted/overcast."

### 5. Held-axe size/scale — **[confirm-in-soak]** — MED
**What's off (potential):** the held-axe scale (`HeldAxeLocalScale 0.0015`) is hand-derived from the 267× bone-scale compensation (`MovementCameraScene.cs` §80-93) — "a hatchet a touch under half the kid's ~0.95u height." That's a delicate hand-tune; the comment itself notes a prior version shipped as a "30-50-world-unit GIANT." A scale that's a touch too big (toward the giant failure) or too small (toy-pick) is the **most-likely placement-ADJACENT reject** once the grip position reads right.

**Why MED + [confirm-in-soak]:** I can't judge final scale from source — it needs the rendered frame. But it's flagged because it's the same delicate dimension as the in-flight placement item, and the Sponsor has already rejected axe scale once. **Pin Tess:** in the held-axe soak frame, eyeball the hatchet against the castaway's height — board-proportion is "chunky, a touch oversized in the mitten hand," NOT towering or doll-tiny. If it's off, it's a one-line `HeldAxeLocalScale` nudge — cheap to fold if the soak shows it.

### 6. The flat moss-grey TestGround coexists with the saturated terrain — adjacency note to the in-flight "gray slab" — MED
**What's off:** `MovementCameraScene.BuildFlatGround` still builds a flat `TestGround` plane, material `_BaseColor (0.42,0.46,0.40)` "muted moss-grey," smoothness 0.05 — and it's layered UNDER the `WorldBootstrap` saturated vertex-color terrain in the same shipped scene. The seaward edge was already trimmed (`SeawardGroundZ -10`) because it was occluding the ocean. **The in-flight "gray slab" fix likely targets THIS** — so I'm not re-listing it. **Adjacency I AM flagging:** the trimmed TestGround may still show its flat moss-grey at the player's feet / under the loop spots where it isn't covered by the saturated terrain, creating a dull-grey patch in the otherwise-saturated field. **[confirm-in-soak]** whether the slab's color reads through anywhere the player stands. If the in-flight fix only *trims* the slab (not recolors/removes it), the leftover patch is a follow-on.

**Fix suggestion:** if the slab can't be removed (it carries the NavMesh-bake collider + click-raycast for the loop test ground), at minimum **match its material to the terrain's grass color** (`GrassLo 0.30,0.48,0.20` or the vertex-color terrain) so it disappears into the field instead of reading as a grey shelf. Confirm with Drew whether the in-flight fix already covers this.

---

## LOW — note for completeness; don't block the wave

### 7. Grass tufts thin-foliage normal risk — LOW / [confirm-in-soak]
The scatter grass clumps (`LowPolyZoneGen` ~60 clumps) are exactly the geometry class that hit the iter8 near-black-shard normal bug (unity-conventions.md §Low-poly mesh patterns). If they read dark/shard-y in the soak it's a known fix (distinct verts per face + up-biased normals + the N·L probe). LOW because the team has hit + fixed this before and likely applied the pattern — but worth a Tess eyeball on the grass in the shipped frame. Not a board-divergence per se, an artifact-risk.

### 8. Campfire flame emission bloom — LOW / [confirm-in-soak]
The flame uses emissive URP/Lit (`_EmissionColor color * 1.15`) + the post bloom. The comment notes the fire-light was already tuned DOWN once (it "blew out to a white orb"). The flame emission at 1.15× + bloom could still over-glow. LOW because it only matters when the fire is lit (late in the loop) and was already partly addressed; eyeball the lit-fire frame for a white-blob flame vs a contained warm tongue.

### 9. Water foam / shoreline read — LOW / [confirm-in-soak]
The beach-water work is recent (PR #28/#32). The foam band is a vertex-color strip (beach-water-direction.md §2). Whether the shoreline reads as a crisp toy coast (board `21h16_52`) vs a muddy seam is soak-only. LOW because it's freshly-shipped + spec'd; flag only if the soak shows a hard plane-edge or a missing foam line.

### 10. Castaway face/eye read — LOW / [confirm-in-soak], identity-adjacent
The chibi base ships with its INTRINSIC face (`CastawayCharacter` keeps the FBX toon materials; the recolor repaints the atlas but the eye GEOMETRY is the chibi's own). The board/castaway-style-v2 §3 wants "big dark warm eyes, bright open friendly expression." The chibi base is already big-eyed + young, so this is likely fine — LOW. Flag only if the soak shows the face reading flat/expressionless or the eyes too small/dark-hard. This is identity-adjacent to the in-flight clothes-weathering work; bundle any face note there if one surfaces.

---

## Honest verdict

The scene is **substantially on-board** — the blob-canopy trees, multi-value greens, faceted terrain, warm key light, gradient sky, water, blob shadow, and chunky chibi are all genuinely board-faithful, and the loop props (stump/tree/fire) ride the established Zone-D mesh idiom. I did **not** manufacture issues — the LOW tail is mostly "eyeball this in the soak," not real divergences.

But there are **three genuine HIGH board-divergences** the Sponsor is likely to catch in successive soaks if not folded now: the **realistic axe** (whole tool language mismatch), **no clouds**, and **no far-horizon vista** (the north-star). These are the candidates to roll into the current wave. The two cloud/mountain adds are small, board-faithful, high-delight fold-ins; the axe re-style is the one most likely to be the very next reject because it's the constantly-on-screen hero tool.

---

## Cross-references

- [`inspiration/`](../../inspiration/) — board v2 PNGs (ground truth): `21h08_08` (axe pole — the §1 target), `21h10_44`/`21h13_31`/`21h16_13` (clouds + mountains + bright sky — §2/§3/§4 targets).
- [`team/uma-ux/style-guide-v2.md`](style-guide-v2.md) — §3 tool language (axe), §4 clouds + terrain + sky re-tune, §6 palette anchors (barn-red axe, cloud/sky colors), §7 ticket-shapes (the "stylize hero axe" + trees were ranked cheap wins).
- [`team/uma-ux/castaway-style-v2.md`](castaway-style-v2.md) — §3 face/eye target (§10 here).
- [`team/uma-ux/beach-water-direction.md`](beach-water-direction.md) — water/shoreline (§9 here).
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) — mountains-as-vista-backdrop + far-horizon north-star (§3 here); cloud catalog.
- [`.claude/docs/unity-conventions.md`](../../.claude/docs/unity-conventions.md) — thin-foliage normal bug (§7), editor-vs-runtime serialize (any new cloud/mountain meshes go editor-time into the env root), HDR-clamp + AlwaysIncludedShaders.
- **Shipped-scene source (ground truth for this audit):** `Assets/Scripts/Editor/MovementCameraScene.cs` (TestGround, held-axe scale §80-93, hair), `Assets/Scripts/Editor/WorldBootstrap.cs` (env build), `Assets/Scripts/Editor/LowPolyZoneGen.cs` (scatter density, terrain/grass palette), `Assets/Scripts/Editor/QualityPassGen.cs` (skybox/fog/post), `Assets/Art/Props/CastawayAxe/Material_002_baseColor_png.png` (the realistic axe atlas, §1).
