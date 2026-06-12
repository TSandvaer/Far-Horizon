# Style Guide v2 — Chunky Cartoon Low-Poly (whole-game)

**Ticket:** 86ca8cbhr · **Owner:** Uma · **Reviewer:** Tess (consistency vs board) · **Status:** DIRECTION — docs only, no implementation here.
**Source of truth:** the 10 PNGs in [`inspiration/`](../../inspiration/) (board v2, Sponsor rebase 2026-06-12 evening) + [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md). **Look at the images before implementing any surface — this doc is the translation, the images are ground truth.**

---

## 0. The tonal anchor (read this first)

**Far Horizon is a warm toy you want to pick up.** The rebased board says one thing across every surface: chunky, faceted, saturated, hand-made — a cheerful low-poly world rendered like a polished indie diorama, not a realistic one. The castaway is a little wooden-toy adventurer; his axe is a chunky prop with a hand-sharpened edge; the trees are clustered blobs of bright green; the hills are big honest triangles under a clear sky.

What does NOT change: the north-star feeling. **Small hopeful player in a big alive world, warm and inviting, a journey toward the horizon.** The rebase shifts the *rendering language* (soft-realistic → chunky-cartoon), not the emotional target. If a stylization beat makes the world colder, slicker, or more "AAA," it's wrong even if it's faceted — cut it. Toy-warm and cheerful is the gate.

**Three poles, one language** (this is why the realistic-lush world refs left the board): the character, the tools, and the world must all read as carved from the same faceted, saturated, soft-lit material. A realistic terrain under a cartoon character breaks the toy; a cartoon character on cartoon terrain *is* the game.

---

## 1. The shared visual grammar (applies to ALL surfaces)

Every surface below inherits these. They are the "same material" rule made concrete.

1. **Faceted, flat/smooth-shaded geometry.** Coarse polygon counts where the facets are *legible* — you can see the planes. Tools/character use the welded-vert + ~60° smoothing-angle "smooth-shaded over coarse facets" look (per `unity-conventions.md` §Low-poly mesh patterns); terrain/rocks lean *harder*-faceted (visible flat triangles, near-flat shading). Both are the same family — the difference is smoothing angle, not a different style.
2. **Bold readable silhouettes.** Every object reads at orbit-camera distance from its outline alone. Chunky over delicate. No thin spindly geometry (it both reads poorly AND triggers the thin-foliage normal bug — see §4).
3. **One crisp edge-highlight / bevel plane per hero edge.** The tools' signature: a lighter, near-white bevel plane along the working edge of a blade/head (axe bit, sword spine). It's *geometry* (a chamfer facet catching light), not a texture line. This is the single most identity-defining detail of the board's prop language.
4. **Saturated but warm.** Higher saturation than Zone-D's soft-realistic palette — greens are vivid, the axe head is a confident barn-red — but pulled toward warm, not neon. Sub-1.0 on every channel still (HDR/sRGB-clamp discipline carries from the HUD spec and Zone-D; see §6).
5. **Soft even key light + gentle AO; mild hand-made asymmetry.** The board objects sit in soft studio light with a little ambient occlusion in the crevices and a soft contact shadow. Nothing is perfectly symmetric or machined — slight bends, slight irregularity = "made by hand," which is the toy charm.

---

## 2. CHARACTER — feeds ticket 86ca8ca1m

**Reference:** `inspiration/2026-06-12_21h00_32.png` (chunky castaway). **Scope is STYLE ONLY** — the reference's bearded rugged adult identity is NOT adopted; the `_castaway_judge/` sheets + U2-6 warm palette remain the LOCKED young/hopeful identity (per 86ca8ca1m and `art-direction.md`). We transfer proportions + face *language* + material treatment onto our young castaway.

### Proportion ratios (target — implementable)

| Measure | Target | Notes |
|---|---|---|
| Head : total height | **~1 : 3** (head ≈ 1/3 of body height; "2.5–3 heads tall" range) | The reference reads ~3 heads tall. This is the single biggest readability lever — oversize the head first. Land ~3.0; Sponsor soak can push toward 2.5 if "cuter" is wanted. |
| Hands | **oversized blocky mittens**, ~1.3–1.5× a realistic hand relative to the new arm | Simplified — minimal/no separated fingers; a chunky mitten/club hand reads better at distance and animates cleaner on the existing rig. |
| Feet | **chunky blocky bare feet**, wide and short | Reference is barefoot — fits castaway. Wide stable base, no thin ankles. |
| Limbs | **chunky cylindrical**, slightly tapered, no thin joints | Arms/legs are sausage-chunky, not anatomical. Keeps the toy read + survives rig deformation. |
| Torso | compact, slightly stout | Short relative to the big head; the head dominates. |

### Face language (drives the "expressive" read; identity stays young/hopeful)

- **Eyes: big, dark, rounded.** The reference's defining feature — large dark almond/oval eyes set fairly low and wide on the face, taking up a confident share of the face area. **Adopt the eye SIZE/placement language; keep our young-hopeful expression** (bright, open, friendly — NOT the reference's heavy brow). Big eyes = young + expressive; that's exactly on-identity.
- **No beard, no age lines.** The reference's beard/brow are explicitly out of scope — our castaway is smooth-faced and young (identity ground truth: `_castaway_judge/`). The luminance/identity guard stays green (per 86ca8ca1m AC).
- **Simplified features overall:** small simple nose/mouth, flat smooth-shaded skin with soft AO under the chin/brow. Warm skin tone from U2-6 carries unchanged.

### Material / palette carry

- **U2-6 warm castaway recolor carries verbatim** — warm skin, the leather accent, the recent v4 brighter/warmer pass (worktree commit `6a68c64` "warmer/brighter v4-reference recolor + leather accent"). The stylization changes *shape*, not the color identity.
- Flat smooth-shaded materials (no realistic skin shading); soft contact shadow / blob shadow re-fit to the new wider stance (per 86ca8ca1m AC — blob shadow must re-fit).

### Model-source recommendation — **Blender-MCP proportion-edit of the existing rig (first choice)**

Per `unity-conventions.md` §Asset creation, Blender MCP is the first-choice creation/edit route. For the character: **edit the existing rigged Quaternius mesh in Blender (scale-up head, chunk-up hands/feet/limbs) rather than sourcing a new base** — this preserves the NavMesh/Animator rig so Idle/Walk survive (the hard 86ca8ca1m AC), avoids a fresh `avatarSetup` T-pose round, and keeps the U2-6 material slots intact (recolor must still enumerate ALL 6 materials — `unity-conventions.md` §FBX/rigs). New-mesh route is the fallback only if the rig can't take the proportion edit cleanly. Devon owns the implementation call; this is the recommended path.

> **Trap-class flags for Devon (from `unity-conventions.md`):** normalize intrinsic import height after any mesh edit (~1u); recolor enumerates all 6 materials (4-slot assumption erased the face before); editor-time serialized + shipped-build capture (Awake-built hierarchies ship mangled — the "legs-up" class). Idle/Walk must survive the proportion edit — that's the binding rig check.

---

## 3. TOOLS / PROPS — the axe `21h08_08` as worked example

**References:** `21h06_54` (pickaxe), `21h07_20` (sword), `21h07_42` (curved blade), `21h08_08` (axe). The board gives a *coherent tool language* — define it once, apply per prop.

### The tool language (the shared recipe)

1. **Faceted head/blade** — coarse flat-shaded planes, a few big facets, NOT a smooth curve. The blade tapers in clear planar steps.
2. **The edge-highlight bevel** — a distinct lighter/near-white chamfer plane runs along the working edge (axe bit, sword spine-and-edge). This is GEOMETRY catching light (a bevel facet), not a painted line. **This is the identity detail of the whole prop family** — every tool gets it on its hero edge.
3. **Chunky slightly-bent wooden haft** — warm mid-brown, faceted, with a gentle hand-made bend (not a straight machined dowel). Tapers slightly along its length.
4. **Segmented wrapped grip** (where present — sword/curved blade) — dark desaturated red `#7E3A3A`-family wrap in chunky segments, with a pale stone/bone pommel and crossguard (`#CFC6AD`-family off-white). Mild asymmetry.
5. **Mild hand-made asymmetry throughout** — nothing perfectly symmetric; the toy is carved, not CNC'd.

### Worked example — the survival hero axe (`21h08_08`)

The M-U2 loop's hero tool. Build it to read exactly like `21h08_08`:

| Part | Treatment | Color (sub-1.0, HDR-safe) |
|---|---|---|
| Axe head (body) | faceted wedge, a few big planes, mild asymmetry between cheeks | barn red `#A33B30` (≈0.64, 0.23, 0.19) — confident, warm, NOT fire-engine red |
| Edge bevel | distinct near-white chamfer plane along the cutting edge — the signature | pale steel `#E4E2DC` (≈0.89, 0.89, 0.86) |
| Top horn / poll | the head's small upper hook/horn reads in silhouette — keep it faceted | same barn red, a half-step darker on the shadow facet |
| Haft | chunky, gently bent, faceted, slight taper | warm mid-brown `#7A5230` (≈0.48, 0.32, 0.19) |

The axe stands ~as tall as the haft is long — chunky, a little oversized in the castaway's mitten hand (toy proportion, matches the character's big-hands read).

### Sword / curved blade / pickaxe — style-reference only, NOT scheduled

`21h07_20` / `21h07_42` (combat-ish) and `21h06_54` (pickaxe) signal future interest. **Nothing is scheduled** — they pin the tool language so when the Sponsor shapes M-U3+ tools/combat, the recipe above already applies. Do not build them this milestone.

### Creation route — Blender MCP

Per `unity-conventions.md` §Asset creation: `execute_blender_code` scripts the faceted meshes directly (ideal for this flat-shaded look), `get_viewport_screenshot` to iterate against `21h08_08`, scripted FBX export into `Assets/Art/`. The edge-bevel is a modeled chamfer facet — script it as geometry, not a texture.

---

## 4. NATURE / WORLD — blob canopies + faceted terrain (the Zone-D re-tune)

**References:** `21h10_44` (tree/cloud/rock/grass set), `21h11_03` (four trees), `21h12_49` (Blender asset-pack scene), `21h13_31` (grassy tree-field render), `21h16_13` (mountain-valley scene). The three scene refs (`12_49`/`13_31`/`16_13`) are **new this guide** — they show full compositions (terrain, water, mountains, sky, post) and are the ground truth for what Zone-D becomes. *(They are present in `inspiration/` but were not yet in the `art-direction.md` catalog — catalog amendment proposed in §8.)*

### Trees — blob canopies

- **Canopy = clustered faceted spheres/blobs**, NOT a single smooth dome. Each tree is a cluster of a few overlapping low-poly spheroids (`21h11_03` shows 4 variants of exactly this). Hard-faceted, legible planes.
- **3–4 green values per tree** — the canopy mixes a few flat greens (vivid mid-green body, a brighter top-lit green, a darker shadow-side green). This multi-value clustering is what makes the blobs read as foliage, not a green ball.
- **Trunk = simple straight tapered cylinder**, warm brown, faceted, often slightly bent (the `21h10_44` gnarled variants). No fine branches — the canopy carries the read.
- **Saturated, toy-like, readable at orbit distance.** This is the WORLD direction — the current Zone-D trees migrate toward these silhouettes.
- **Thin-foliage trap (hard rule, `unity-conventions.md` §Low-poly mesh patterns):** these blob canopies are SOLID volumes, not thin double-sided cards — which is good, it sidesteps the thin-foliage near-black-shard normal bug. If any grass/leaf card geometry is used, use distinct verts per face + up-biased normals + the `N·L` probe. Prefer solid blob volumes wherever possible.

### Rocks

- Faceted grey boulders (`21h10_44` bottom row) — a few big planes, lighter top-facets catching the key light, darker sides. Mild asymmetry, clustered in groups. Warm-grey, not blue-grey.

### Grass

- Simple bladed tufts (`21h10_44`) — small clusters of a few flat blades. **Hard-faceted, up-biased normals** (the thin-foliage rule is load-bearing here — this is exactly the iter8 grass-shard class). Vivid green. Sparse scatter, not a dense lawn — purposeful decoration, world-stays-readable.

### Clouds

- Bright teal/cyan puffy cartoon clouds (`21h10_44` top row) — clustered faceted blobs, same blob language as the canopies but in light cyan. Chunky, few, floating. They reinforce the toy-diorama read against the sky.

### Terrain — the big re-tune

The scene refs (`13_31`, `16_13`) define it: **large flat-faceted ground triangles in saturated grass-greens**, visible planar facets (you can see the big triangles), gentle rolling hills. Mountains (`16_13`) are hard-faceted grey-to-snow peaks — big confident planes, a snow-white cap facet, warm-grey body. This is a clear shift from Zone-D's soft-realistic terrain toward visible-facet cartoon terrain.

> **Vertex-color terrain note (`unity-conventions.md` §Build stripping):** the faceted saturated-green terrain wants the custom URP vertex-color shader (URP/Lit ignores vertex color) — and it must be registered in `AlwaysIncludedShaders` or the build strips it (proven spike failure). The flat-faceted look = hard normals (smoothing angle 0° on terrain, unlike the character's 60°).

### Water

- `16_13` shows a **thin bright-blue ribbon** — a simple saturated-blue faceted strip, low detail, reads as "a stream through the valley." Not the realistic shader water of a Zone-D pass. Flat, bright, toy-like.

### Zone-D delta — what carries vs what re-tunes

**Delta baseline:** the current shipped build (the bootstrap/M-U2 captures show the placeholder soak scene — flat yellow ground + primitive props; the full Zone-D warm-hazy look lives in the `EmbergraveUnitySlice` reference + CLAUDE.md's "Zone-D look" definition: gradient skybox / bloom / grading / fog / warm-lush soft-realistic). The board v2 re-tunes that post/render language:

| Zone-D element | Carries? | Re-tune |
|---|---|---|
| **Gradient skybox** | CARRIES | Re-tune toward a **clearer, brighter sky** (the scene refs read as open daylight, not hazy-warm). Keep warm, lower the haze. |
| **Bloom** | CARRIES (reduced) | Pull bloom DOWN — the board objects have crisp facet edges, not glowy ones. A touch of bloom on bright highlights is fine; heavy bloom softens the facets and breaks the chunky read. |
| **Color grading** | CARRIES (lighter) | Lighter, more neutral-warm grade. The board is saturated-but-clean — a heavy filmic grade muddies the toy colors. Let the saturated greens/reds speak. |
| **Fog** | CARRIES (much lighter / distance-only) | The scene refs are crisp to mid-distance. Use fog only for far-horizon depth (it still serves the "big endless world" north-star), not as a near-field haze. |
| **Soft-realistic shading** | RE-TUNE → flat/faceted | The core shift. Terrain/rocks → hard normals + visible facets; trees → blob canopies; everything reads carved, not photographed. |
| **Warm cohesive palette** | CARRIES | Warmth is a hard carry-over (§5). Saturation goes UP; warmth stays. |

Net: **keep the post-processing STACK** (it's already correctly serialized into the VolumeProfile per the `VolumeProfile.Add<T>` serialization fix, U5/PR #4 — don't re-break that), but **re-tune its intensities** toward crisp-bright-saturated and shift the *geometry/shading* language toward faceted-cartoon. The skybox/bloom/grade/fog are dials to turn, not systems to rip out.

---

## 5. Explicit carry-overs (survive the rebase — do NOT lose these)

Per `art-direction.md` and the locked north-star:

- **Small-player / big-alive-world** — the whole point. Stylization must not shrink the world's sense of scale; the castaway stays a small hopeful element inside a big diorama.
- **Human-scale landmarks** — readable, purposeful landmarks the player measures themselves against; they just get rendered chunkier now.
- **Purposeful (not cluttered) decoration** — the lush *feeling* carries; grass/rocks/trees are sparse and intentional, never noise. World-stays-readable.
- **Warm cohesive palette** — warmth is non-negotiable. Saturation rises; the warm bias stays. No cold/slick drift.
- **Sub-1.0 HDR-clamp-safe color discipline** — every channel < 1.0 (carries from the Zone-D look + the U2-5 HUD spec). Saturated ≠ blown-out.
- **The post-processing stack as a SERIALIZED-into-VolumeProfile asset** — re-tune intensities, never let the `Add<T>`-not-serialized regression back in (U5 guard stays green).

---

## 6. Palette reconciliation — warm carry-over vs board saturation; HUD compatibility

### Warm-cohesive ⟷ higher-saturation — how they coexist

They're not in tension: **warm = the hue bias; saturated = the chroma level.** The board pushes chroma UP (vivid greens, confident barn-red) while keeping the hue family warm. The rule: **raise saturation, keep the warm hue bias, stay sub-1.0.** A saturated vivid green is fine; a cold blue-shifted green is not. The axe is a warm barn-red (`#A33B30`), not a cold crimson. The sky stays warm-bright, not cold-clear.

Anchor swatches established by this guide (all sub-1.0, HDR-safe — extend as surfaces land):

| Surface | Color | RGB (0–1) |
|---|---|---|
| Axe head | barn red `#A33B30` | 0.64, 0.23, 0.19 |
| Tool edge bevel | pale steel `#E4E2DC` | 0.89, 0.89, 0.86 |
| Haft / trunk wood | warm brown `#7A5230` | 0.48, 0.32, 0.19 |
| Grip wrap | dark desat red `#7E3A3A` | 0.49, 0.23, 0.23 |
| Pommel / crossguard | off-white bone `#CFC6AD` | 0.81, 0.78, 0.68 |
| Canopy (body green) | vivid mid-green `#4C9E3A` | 0.30, 0.62, 0.23 |
| Canopy (top-lit) | bright green `#7BC65A` | 0.48, 0.78, 0.35 |
| Canopy (shadow) | deep green `#2F6B2A` | 0.18, 0.42, 0.16 |
| Rock | warm grey `#8E8A82` | 0.56, 0.54, 0.51 |
| Water ribbon | bright stream blue `#3E8FC4` | 0.24, 0.56, 0.77 |

> These are direction anchors derived from the PNGs by eye, the QA-pin baseline for Tess. Tune in-build against the captures; treat as starting values, not locks. (Greens/blue here are the saturated *world* end; the warm bias holds because the hue centers stay warm-leaning and the lighting/grade are warm.)

### HUD ember-band compatibility — CHECKED, compatible

The U2-5 HUD palette (`u2-5-survival-hud-spec.md` §3) is ember gold `#E8B25C` / dusk orange `#D98A4E` / coal red `#B5563C` / charcoal `#2E2A2B` on low-alpha dark plates, all sub-1.0. **This survives the rebase unchanged and stays compatible** for three reasons:

1. **Same discipline** — the HUD is already built sub-1.0, warm, drawn from the world's own colors. That's exactly board-v2's palette rule.
2. **Warm family alignment** — the ember-gold/dusk-orange band sits in the same warm hue family as the axe's barn-red and the world's warm bias. The HUD ink belongs to the same world.
3. **No clash with the new saturation** — the HUD's coal-red (`#B5563C`) is deliberately a muted dying-ember red, distinct from the axe's barn-red (`#A33B30`); both are warm, neither is a screaming alarm red. They read as siblings, not collisions. The saturated world greens are spatially separated from the warm HUD corner and don't fight it.

**One watch-item for Tess (not a change):** the world's saturation going UP slightly raises the contrast the HUD plates must hold against bright saturated-green ground. The HUD's low-alpha dark plate (`rgba(0,0,0,0.55)`) was tuned for sand/foliage legibility; re-verify plate legibility over the *new* saturated-green terrain in a soak. Likely fine (the plate is dark and the ink is warm-bright), but it's the one place the world re-tune touches the HUD. **No spec change proposed — a soak re-check item only.**

---

## 7. SEQUENCED ticket-shape proposal — Sponsor-visible effect per effort

Ordered for **maximum visible delight per unit of effort**, sequencing-aware (M-U2 must close first; character is already filed). These are ticket *shapes*, not filed tickets — orchestrator files against this with real IDs.

> **Hard sequencing constraint (carry from 86ca8ca1m):** do NOT destabilize the M-U2 scene mid-milestone. Anything touching the live milestone scene waits for M-U2 close (U2-7 + loop soak). The two cheap wins below (axe, trees) are scene-touching, so they open the post-M-U2 wave.

| # | Ticket shape | Surface | Effort | Sponsor-visible effect | Why this rank |
|---|---|---|---|---|---|
| **1** | **Stylize the hero axe** to `21h08_08` (faceted head + white edge-bevel + bent haft) | tools | **S** (one prop, Blender-MCP scripted, no rig) | HIGH — it's the loop's hero tool, in-hand, on-screen constantly | Cheapest high-visibility win. No rig, no NavMesh, isolated prop. The edge-bevel establishes the whole tool language in one cheap asset. **Top recommendation.** |
| **2** | **Blob-canopy tree set** (3–4 canopy variants + trunk, replacing Zone-D trees) | world | **S–M** (Blender-MCP scripted blobs, a few variants) | HIGH — trees fill the frame; swapping them re-skins the whole world's read instantly | Cheap (solid blob volumes sidestep the foliage-normal bug) + transforms the world silhouette wholesale. Pairs naturally with #1 as the "world starts looking like the board" wave. |
| **3** | **Castaway chunky stylization** (`86ca8ca1m`, already filed) | character | **L** (rig-preserving mesh edit + re-test Idle/Walk + identity guard) | HIGH — it's the player, always on screen | Highest intrinsic visibility but **L effort + rig risk** (Idle/Walk must survive, identity guard, blob-shadow re-fit), so it ranks after the two cheap wins land the world around him. Already filed — sequence it as the wave's anchor L-task. |
| **4** | **Rocks + grass + clouds decoration set** | world | **M** (several small props + the grass-normal care) | MED — fills out the diorama, reinforces the toy read | Lower per-asset visibility than trees but completes the nature family. Grass carries the thin-foliage normal-bug risk — budget the `N·L` probe. |
| **5** | **Zone-D terrain + post re-tune** (faceted terrain, lighter bloom/fog/grade, brighter sky, water ribbon) | world / post | **L** (terrain shader + NavMesh re-bake + post-dial tuning + full soak) | HIGH ceiling, but RISKY | Biggest transformation but biggest blast radius — terrain re-mesh touches NavMesh (click-to-move can die — `unity-conventions.md` §NavMesh), the vertex-color shader needs `AlwaysIncludedShaders` registration, post re-tune needs careful soak. Do it LAST when the chunky props/character already prove the direction, so the expensive terrain pass aims at a known target. |

**Top-3 recommendation:** **1) hero axe** (cheapest high-visibility, establishes tool language) → **2) blob-canopy trees** (cheap, re-skins the whole world read) → **3) castaway stylization** (`86ca8ca1m`, the player, already filed — the wave anchor). Terrain/post re-tune (#5) is the big finale, sequenced last to aim at a proven target and contain its NavMesh/shader blast radius.

---

## 8. Doc-catalog amendment proposed (for orchestrator/Priya)

`art-direction.md`'s board-v2 catalog lists **7** references but `inspiration/` holds **10** PNGs. The three uncatalogued ones are load-bearing world-scene refs this guide leans on:

- `2026-06-12_21h12_49.png` — Blender low-poly asset-pack scene (trees/rocks/grass/mushrooms/logs against faceted mountains) — the "asset family in one frame" world ref.
- `2026-06-12_21h13_31.png` — grassy tree-field render (faceted blob trees scattered over rolling saturated-green terrain under cartoon clouds) — the orbit-distance world read.
- `2026-06-12_21h16_13.png` — mountain-valley scene (faceted grey-to-snow peaks, conifer blob trees, thin bright-blue water ribbon, rain cloud) — terrain + water + mountain + sky direction.

**Proposed:** add a Nature/world-scene catalog entry to `art-direction.md` covering these three. *(I do not edit `art-direction.md` in this docs-only ticket; flagging for the orchestrator to route — likely a quick Priya/Uma follow-up.)*

---

## Cross-references

- Ticket **86ca8cbhr** (this guide) · **86ca8ca1m** (character stylization — §2 feeds it) · axe/tools + trees/world ticket shapes (§7, to be filed).
- [`inspiration/`](../../inspiration/) — the 10 board-v2 PNGs (ground truth).
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) — board-v2 catalog + carry-overs (§8 amendment proposed).
- [`.claude/docs/unity-conventions.md`](../../.claude/docs/unity-conventions.md) — Blender-MCP asset route (§Asset creation); low-poly mesh / thin-foliage-normal / smoothing-angle patterns; vertex-color terrain shader + `AlwaysIncludedShaders`; NavMesh re-bake; FBX/rig + recolor-all-materials traps; `VolumeProfile.Add<T>` serialization guard.
- [`team/uma-ux/u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) §3 — HUD ember-band palette (§6 compatibility check).
- `EmbergraveUnitySlice` (read-only) + CLAUDE.md "Zone-D look" — the delta baseline for §4.
