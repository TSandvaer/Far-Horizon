# Art Direction — Sponsor inspiration board

**Status:** Sponsor-set art-direction north-star (2026-06-08). These are the
reference images Sponsor named as "kinda like the art direction I would prefer."
**Every session and sub-agent doing visual / level / tile / prop / palette work
must look at the actual images** in [`inspiration/`](../../inspiration/) before
proposing or implementing — the text below is a guide to what to SEE in them, not
a replacement for seeing them.

> **How to use:** `Read` the PNGs in `inspiration/` directly (they render as images).
> This doc captures the extracted direction so it survives in context; the images
> are the ground truth. When Sponsor drops a new reference into `inspiration/`,
> add a catalog entry here.

## The references

### `inspiration/2026-06-08_07h53_24.png` — garden/shrine (the LUSH pole)
Top-down ornamental garden: an octagonal multi-tier **fountain at human scale**
(a structure a small character walks up to — NOT a monument), set in **concentric
rings of irregular warm-tan cut-stone pavers** with darker grout and moss/grass
creeping into the joints. **Dense, layered, flowering planting** — red/pink bushes,
a purple-flowering shrub, many distinct greens — clustered naturally at every path
edge and ringing the fountain. Soft painterly light, tree-canopy depth, gentle
edge-vignette. The character is **small in a big, alive world**.
Dominant palette: deep forest greens `#1F322C`/`#0E3321`, mid leaf `#2E623A`,
light foliage `#80AB6F`, warm paver cream `#DED1C0`, stone-grey `#6D705B`.

### `inspiration/2026-06-08_07h54_44.png` — village courtyard (the LIVED-IN pole)
Top-down medieval village yard: **worn irregular flagstone in MULTIPLE blended
tones** (cream/tan/grey) with **grass and bare dirt invading the joints and worn
patches** — the paving reads aged and walked-on, never a uniform tile grid.
Half-timber buildings (timber + plaster + terracotta roofs) frame the yard, and
the decoration is **all real, purposeful objects with a use-story** — a wooden
bench, barrels, **flower boxes / planters**, a lantern post, potted blooms — dense
but completely readable. Warm cohesive palette.
Dominant palette: worn-stone grey-green `#566054`, warm flagstone `#C1B091`/`#978A6B`,
timber browns `#896F5E`/`#503D41`, shadow `#38343A`.

## The shared direction (what to emulate)

1. **Small player, large dense ALIVE world.** Both confirm the project north-star
   ([[tile-scale-small-player-large-world]], [[world-feel-big-and-endless]]): the
   character is a small element; the world is big, layered, and full.
2. **Stone is FINE-to-medium, irregular, and MULTI-TONE.** Pavers/cobbles are small
   relative to the character, organic (not blocky), blended across several tones,
   and **worn — grass, moss, and dirt invade the joints and worn patches.** Uniform
   single-tone blocky tiles are the anti-pattern. (Directly matches Sponsor's
   "cobble finer / path tiles too big" soak notes on T8.)
3. **Landmarks are HUMAN-SCALE.** The fountain reads as a real fountain a person uses
   — proportionate, not oversized. (Directly matches the T8 "well way out of
   proportion" soak note: a well should read ~1–1.5× player height, like this
   fountain, not a monument.)
4. **Decoration = lush, layered, and PURPOSEFUL.** Flowering bushes + varied greens
   clustered at path edges and around landmarks (garden), or real objects with a
   use-story — bench, barrels, flower boxes, lantern (village). **Every decoration
   element reads intentional.** Random scattered shapeless props ("spiky burr trash")
   are the anti-pattern — this is exactly the class Sponsor flagged for removal on T8.
5. **Warm, rich, COHESIVE palette with controlled accent color.** Tan/cream stone +
   terracotta + many harmonious greens, with saturated flower accents (red / pink /
   purple / yellow) used sparingly for life. All sub-1.0 HDR-safe per
   [`html5-export.md`].
6. **Depth via layering + soft shading.** Canopy over ground, soft light/shadow,
   gentle vignette — a painterly, not flat, read.

## Reconciling with the "drop the grass → clean grey cobble" decision

Earlier (T4/#424) Sponsor said "drop the grass" and locked a **clean grey cobble**
base. That was a rejection of **ugly baked-in moss/spiky tufts**, NOT a rejection of
vegetation. These references make the real target clear: **vegetation belongs, but as
deliberate, high-quality, clustered planting** (flowering bushes, layered greens at
edges/landmarks) — not random baked-in grass speckle. So the S1-yard end-state is
**warmer and more planted than a bare grey carpet**: a clean fine-cobble GROUND with
deliberate lush planting composed AT the edges and landmarks. The cobble base stays
the locked material; a future **deliberate-planting / decoration pass** layers the
lush look on top (Uma to spec, Drew to implement) — distinct from the current T8
revision, which is scale + trash-cleanup only.

## How this maps to current work

- **T8 revision (in flight):** corroborates all four Sponsor soak notes — finer
  cobble, smaller slab stones, human-scale well, remove the shapeless-prop trash.
- **Future planting/decoration pass:** the lush flowering-bush + purposeful-object
  layer these references call for — a new Uma spec → Drew ticket, after T8 lands.
- **S2+ biomes & wider world:** the "fine multi-tone worn stone + purposeful dense
  decoration + human-scale landmarks + cohesive warm palette" formula generalizes.

## Execution lessons (S1 path — 3 soak bounces, 2026-06-08)

Hard-won from the S1 yard path being soak-rejected three times ("too big" → "awful" →
"terrible / not like a real path"). These are about EXECUTION, not concept:

- **Lean on the gen tech Sponsor already LOVED; do not invent fresh procedural art that
  misses.** Sponsor loved the procedural **cobble** (`tools/gen_s1_cobble_floor.py`, Voronoi).
  A brand-new procedural **ashlar slab** generator (`gen_s1_slab_path.py`) — mechanically
  clean, Tess-approved, doctrine-compliant — was still feel-rejected as "not like a real
  path." Lesson: when one procedural generator lands well, derive new ground surfaces as
  *tuned variants of it* (the fine-cobble-paver path = finer cobble), not as a fresh
  tessellation model the eye reads as "programmer art." Procedural can hit the bar (cobble
  proved it) — but only by riding a proven recipe, not inventing per-surface.
- **A path must read like a REAL path.** Sponsor's path references (`inspiration/` — village
  street `08h00_28` fine small-cobble lane; Graveyard Keeper `11h18`, Stardew `11h19`, island
  `08h01` worn dirt) are **fine small cobble or worn dirt** — never big blocky cut-stone
  slabs. Big slabs read "built/decorative," not "walked." Sponsor's call: **fine small-cobble
  pavers**, and "cobblestone should only be PARTS of the walking background" → the ground is
  VARIED (dirt/earth + grass + cobble patches), NOT a wall-to-wall cobble carpet.
- **Mechanically-clean ≠ Sponsor-approved on a visual surface.** Three times the PR was
  Tess-APPROVE + CI-green + doctrine-pinned and STILL bounced on feel. For first-of-class
  art, the orch in-context render + Sponsor soak are the real gate; a green PR is necessary,
  not sufficient. Show an in-context render BEFORE a full soak to catch misses cheaply (it
  still missed here — so also pre-judge the render hard against the references, not just
  "is there green / right scale").
- **`inspiration/` grew to 13 images (2026-06-08)** — the catalog in this doc lists the first
  two; treat the whole folder as ground truth and `Read` all of them for any ground/path work.

### The decisive lesson (after 7 bounces — pipeline pivot, 2026-06-08)

The S1 ground bounced **seven** times (slab too-big → awful → terrible → chaos → corners-too-big
→ "what is improved?" → "the background looks like crap, I can't see how we'll make a nice game").
The earlier "lean on the loved procedural cobble" lesson was only *half* right and led us astray:

- **Procedural generation CANNOT reach hand-authored tileset quality for a COMPOSED, varied
  ground.** The uniform cobble worked procedurally *because it is uniform*. But a ground that
  composes dirt + grass + a legible path with *soft blended edges* is a hand-authored-art problem
  — the references (Stardew, Graveyard Keeper) are professional pixel-art **tilesets**, not
  generated fields. Procedural blob/dither/Voronoi composition reads as "programmer art" no matter
  how it's tuned. **Don't iterate procedural composition toward a hand-painted bar; switch tools.**
- **The right tool: an AI-gen / authored TILESET with TRANSITION tiles + Godot's autotile (terrain
  set) system.** Godot autotile + Wang transition tiles is *literally* the engine feature for the
  "seamless material blend" Sponsor kept asking for. PixelLab `create_topdown_tileset` is a
  Wang/terrain-transition generator (corner autotiling, connected tilesets) — the matching gen
  tool. This is consistent with what already looks GOOD in the project (AI-gen characters/mobs);
  the failure was specifically the *procedural-composited* ground.
- **A standalone offline RENDER tool is an INVALID visual gate if it can diverge from the
  in-game camera.** Drew's `_yard_render/*.png` showed finer + feathered ground and passed both
  orch pre-judge AND Tess — but the live game rendered blocky/hard-edged because the render tool
  zoomed/composited differently than the in-game `CameraDirector`. **Verify visual work in the
  ACTUAL running build (Playwright screenshot of the HTML5 game), never an offline render that
  isn't the game.** A render that doesn't match the game produces false approvals.

### The FINAL pivot (after the AI-tileset build also missed — 2026-06-08)

Even the AI-gen-tileset + Godot-autotile path (above) + a full coordinate-layout build with real PixelLab
building props was served and **rejected again on feel**: *"it still sucks, I think we need to find a way
where I can design the levels. I want nice background graphics and this is not gonna do it."* Sponsor named
the structural fix himself, popup-confirmed, now the standing direction (memory
`level-design-pivot-godot-editor-sourced-tileset`):

- **Ground art = a SOURCED professional top-down tileset pack** (commercial-license, Stardew/Graveyard-Keeper
  tier), NOT AI-gen and NOT procedural. Both AI-Wang and procedural ground missed the bar repeatedly across
  the whole S1 saga; reference-quality 2D backgrounds are professionally hand-crafted tilesets. First pick:
  **Cainos "Pixel Art Top Down – Basic"** (free, 32×32 — matches the project grid). Upgrade path: Seliel /
  Mana Seed (premium, human-made, cohesive whole-game ecosystem). **Do NOT restart AI/procedural ground gen.**
- **Levels are HAND-AUTHORED BY SPONSOR in the Godot editor**, not team-built from coordinate specs. The
  spec→code→soak loop is a lossy telephone game that kept missing. The team's job flips to: hand Sponsor a
  *paintable* TileMap scene + a TileSet/prop palette + a short how-to, and Sponsor composes with real-time
  in-engine feedback. S1 becomes a hand-authored TileMap map, not a code-assembled `S1YardChunk`.
- **What carries forward:** the 5 PixelLab building assets (`assets/props/s1_yard/*.png`) are good and become
  placeable props. The v5 autotile/procedural GROUND and the coordinate-layout-build approach are superseded.
- **The meta-lesson (reinforced a 3rd time):** ≥2 rejections on the same surface = the APPROACH is wrong, not
  the tuning. Escalate to changing the tool/workflow, not another iteration. See `orchestration-overview.md`
  § "Outcome over motion". The procgen "randomized maps" M3 directive is in tension with hand-design — likely
  resolution: hand-design hubs/authored areas, procgen the randomized depths (confirm before sequencing).

## General principles layer — the `game-art` skill
Before any visual / asset / animation / palette task, also consult the global **`game-art`
skill** (`~/.claude/skills/game-art/SKILL.md`) — Sponsor directive 2026-06-08, "always refer
to the game-art skill." It is the GENERAL principles layer (style selection, asset pipeline,
color theory, the 12 animation principles + frame-count guides, resolution/scale, naming,
anti-patterns: "art serves gameplay; focus detail on the player area; test silhouette at
gameplay distance; same object = same color family"). This doc + `inspiration/` are the
PROJECT-SPECIFIC look that those general principles get applied toward. Memory:
[[art-work-refer-to-game-art-skill]].

## Cross-references
- `~/.claude/skills/game-art/SKILL.md` — general game-art principles (consult for all art work).
- `inspiration/` — the image files (ground truth; look at them).
- `team/uma-ux/palette.md`, `team/uma-ux/visual-direction.md` — Uma's canon to fold
  these principles into.
- `team/uma-ux/s1-cloister-yard.md` + `s1-yard-ground-composition.md` — the active
  S1-yard vision these references refine.
- `.claude/docs/html5-export.md` — HDR-clamp / sub-1.0 palette discipline.
- Memory: [[tile-scale-small-player-large-world]], [[world-feel-big-and-endless]],
  [[game-world-journey-arc]], [[s1-cloister-yard-open-world-direction]].
