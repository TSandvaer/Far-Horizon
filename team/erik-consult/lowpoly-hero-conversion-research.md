# Low-Poly Hero Conversion — getting the Castaway to read chunky-faceted like the rest of the game

## Question
The live Castaway v2 hero (Rodin-generated, Mixamo-rigged) reads as a dense smooth
organic mesh; the world + 8-weapon family read as chunky faceted flat-shaded palette
assets. Sponsor (2026-07-06): *"the hyper3d character is not low poly like the rest of
the game … revisit the in-house low poly path."* What is the best pipeline to get a HERO
CHARACTER that reads chunky-faceted-low-poly, graded by evidence?

## Bottom line
The mismatch is **three compounding causes, not one**: (1) mesh density ~7,658 verts /
≈15k tris (a *realistic* low-poly count, not a *chunky-facet* count); (2) a **normal map**
on a **URP/Lit** material adding smooth surface micro-detail; (3) a **baked diffuse** that
still carries soft painted shading. The weapons sit in URP/**Unlit** + flat palette + 50–500
tris. **Recommended route: RETOPO-DOWN the existing Sponsor-approved Rodin mesh** to a hero
budget of **~1,500–3,000 tris, flat-shade it, drop the normal map, re-texture to the flat
palette (`char_palette.png` blocks), then re-rig through Mixamo** — the proven back-half of
the existing pipeline. This preserves the *approved bearded-friendly identity and silhouette
by construction* (the exact thing 7 procedural attempts failed on), spends **zero Rodin
credits**, and is fully in-house. Rodin low-poly re-gen (Route 3) is the fallback if retopo
deformation fails. A shader-only flat-shade (Route 4) **cannot** work at this density, and a
fresh hand-model (Route 2) is a closed option per project history. **Do not ship a
decimate/normal-strip alone — the flat palette re-texture is mandatory or it still mismatches.**

## Evidence

### On-disk ground truth (Read directly this session — STRONG)
- `art-src/castaway-rodin-export/README.md` — skinned mesh **7,658 verts / 34 vgroups**,
  Rodin **Quad Mesh ~8000**, **Baked Normal ON**, 41 bones, imports at 1.889 m. The density
  was a *topology-picker choice at Confirm time*, not a Rodin limitation.
- `Assets/Scripts/Editor/CharacterAssetGen.cs` (lines 323–359) — the in-engine material is
  **URP/Lit**: `_BaseMap` = de-lit diffuse, `_Smoothness` 0.03, `_Metallic` 0, **and a
  `_BumpMap` normal map** (`texture_normal.png`, forced to NormalMap type). The comment itself
  admits "the baked albedo already carries shading." Contrast: the weapon family is URP/**Unlit**
  + one flat `weapon_palette.png`, no normal, no lighting (`blender-asset-pipeline.md` §2).
  → The hero is literally in a *different shading model* from everything around it.
- `texture_diffuse.png` (viewed) — even de-lit, it carries **soft painted gradients** (skin
  form-shading, beard strand detail). Flat palette blocks (`char_palette.png`, viewed — a tidy
  swatch grid) are the opposite.
- `castaway_concept_apose.png` (viewed) — the **Sponsor-approved** concept already reads
  faceted-chunky. The approved *look* was never the problem; the generated *geometry+material*
  drifted denser/smoother than the concept. This means the target is well-defined and the
  identity is locked in an asset we already hold.

### Poly-budget literature (MODERATE — multiple guides, converging)
- Stylized-indie hero characters commonly ship **~1,000–3,000 tris** for a chunky flat-shaded
  read; "low poly is relative to platform and *you model for facets and silhouette, not just
  tri count*." Realistic PC heroes are 15k–50k tris — the current mesh sits in *that* bracket,
  which is precisely why it reads "not low poly." (low-poly.com; polycount; gameslearningsociety.)
  → Target **~1,500–3,000 tris**: chunky enough that facets read as *planes*, high enough that
  a rigged humanoid still deforms.

### Facet size = polygon size (STRONG — first principles + our own doc)
- A flat-shading trick (`_FlatShading` ddx/ddy, or Shade Flat) makes each **triangle** a flat
  plane. At ≈15k tris the facets are *tiny* → reads as low-frequency shading **noise**, not
  chunky planes. `lowpoly-quality.md` §2/§3 codifies this: `_FlatShading` is **props/rocks
  only**, and "do NOT flat-shade the welded/dense mesh — reads as a spike polyhedron." Flat-
  shading is a *necessary component after reduction*, never a standalone fix. → **Route 4 ruled
  out as a standalone; mandatory as a step post-reduction.**

### Retopo vs decimate for a mesh that will be rigged (STRONG)
- Decimate (Collapse) preserves silhouette cheaply but yields **triangle-soup topology that
  deforms badly at joints** — fine for static props, poor for animated characters. Quad-based
  retopo (Quad Remesher / Instant Meshes / Blender QuadriFlow) yields loops that **follow the
  form and deform predictably**. Voxel remesh is explicitly *not for meshes that deform*.
  (Blender 5.1 Manual "Remeshing"; SuperRenders; RebusFarm; yelzkizi.) → This is the direct
  answer to the pipeline doc's **Quad-not-Tri** gotcha (`character-pipeline.md` Step 2): keep
  quads through the reduction, and Mixamo re-rig stays clean.

### Rodin low-poly capability (MODERATE — vendor + one trade write-up, rigging quality unproven)
- Rodin exposes a **Low Poly** style and a Gen-2.5 **Smart Low-poly (BETA)** mode that
  "reconstructs meshes face-by-face producing artist-style triangle/quad geometry for real-time
  use," plus Triangle presets at 2K/20K/… and a Quad mode. (hyper3d.ai; 80.lv Gen-2.5 write-up
  — vendor/marketing, not an independent benchmark.) → Rodin *can* emit lower-poly, but (a) the
  BETA topology's **rig-deformation quality is unproven for this project**, (b) it costs Creator
  credits, (c) re-generation risks slight identity drift vs an asset we already have approved.

### Project history (STRONG — our own record)
- `character-pipeline.md` + `art-direction.md` (2026-07-05): **7 procedural bpy hero builds each
  failed the Sponsor's bar on GESTALT** ("cleared every itemized defect and still didn't land");
  the Sponsor then **ratified concept→web-Rodin→Mixamo as the ONLY hero route** and it succeeded
  in under an hour. The gestalt-failure diagnostic + the "2+ attempts fail → switch route" rule
  make a **fresh hand-model (Route 2) a closed option**, not a discouraged one.

## Application to Far Horizon — ranked recommendation

### ✅ Route 1 (RECOMMENDED): Retopo-down the existing approved mesh
Why it wins: it keeps the **Sponsor-approved bearded-friendly identity/silhouette by
construction** (derived *from* the approved mesh — the one thing procedural modeling could
never nail), spends **zero Rodin credits**, is **fully in-house Blender**, and a flat-palette
low-poly base actually *serves the customization goal* (palette-row recolors) better than a
baked-texture dense mesh. Shares its entire back-half (flat-shade + palette + Mixamo re-rig)
with Route 3, so the work is not wasted if we later fall back.

**Concrete step-list (hand to a Blender-capable dev — Devon/Drew; source is `art-src/castaway-rodin-export/base.fbx`, the geometry-only unrigged export):**
1. **Retopo the unrigged `base.fbx` to ~1,500–3,000 tris, quads.** Tool order by cost/in-house
   posture: **Blender QuadriFlow remesh (free, built-in)** or **Instant Meshes (free, CC0)**
   first; **Quad Remesher (~$60 perpetual Blender addon)** only if QuadriFlow's loops are too
   muddy around face/hands — a small one-time buy, consistent with in-house-first. Keep hands as
   simple fists/mittens (matches the 41-bone no-finger rig already validated).
   - ⚠ **Do NOT decimate-collapse** (tri-soup → joint deformation fails). Planar-decimate is
     tempting for chunky facets but leaves n-gons/poor joints — retopo to quads, *then* facet
     via shading.
2. **Flat-shade for the chunky read.** Shade Smooth + **Mark Sharp on all (or most) edges**
   (`blender-asset-pipeline.md` §4) — facet size now = the ~2k-tri polygons = chunky planes.
   Optionally keep limbs lightly smooth if fully-faceted reads too spiky at the joints (Sponsor
   judge-gate 1 decides).
3. **Drop the normal map entirely** and **re-texture to the flat palette.** UV → scale islands
   to palette blocks (`char_palette.png`, the weapon-pipeline §5 idiom), replacing the baked
   `texture_diffuse`. In Unity, move the material off URP/Lit+BumpMap toward the project's flat
   idiom (URP/Unlit palette, matching weapons — OR URP/Lit with `_Smoothness` 0 and **no**
   `_BumpMap`; the exact shader is Devon's call, but the normal map must go). This step is
   **non-negotiable** — a low mesh with the old soft diffuse still mismatches.
4. **Re-rig via Mixamo** (`character-pipeline.md` Step 3): markers chin/wrists/elbows/knees/
   groin, Symmetry ON, Standard Skeleton; download **With Skin** once + clips **Without Skin**.
5. **Unity import — follow the LIVE Generic path, not the README's "Humanoid" line.** The v2
   README says "import as Humanoid" but the shipped castaway is **Generic** (memory
   [[castaway-v2-is-live-hero]] / [[sponsor-approved-handoff-can-have-tech-error]]; Humanoid
   *explodes the mesh under a scaled hierarchy* — `character-pipeline.md` Step 4). Confirm the
   rig type against the live `CharacterAssetGen` path before wiring clips. (Integration detail —
   Devon/Drew's lane, flagged here so the README's stale line doesn't get blind-followed.)

**Sponsor judge-gates (where the calls go):**
- **Gate 1 (cheap, pre-rig):** a Blender render (View Transform = **Standard**, per pipeline
  §2/§10) of the static A-pose after steps 1–3 — *does it read chunky-faceted-on-style next to
  a weapon?* Decides tri-target and full-vs-partial faceting *before* spending rig effort.
- **Gate 2 (post-rig):** Mixamo Idle+Walk preview — elbows/knees/shoulders no pinch/collapse
  (the Quad-not-Tri survival check).
- **Gate 3 (the real judge):** **shipped-exe capture from the ANCHORED gameplay camera** (not a
  mesh-framing camera — `character-pipeline.md` Step 4 ⚠, the Humanoid-explode hiding trap).
  Editor previews have lied on this project; the built exe is the gate.

**Biggest honest risk:** auto-retopo gives *evenly-sized* facets, not the *art-directed* facets
of the concept (deliberate hair spikes, planar cheeks). This is acceptable and roughly matches
the concept render, but if the Sponsor wants art-directed facet placement that's hand-retopo
territory (expensive). Gate 1 surfaces this early and cheaply.

### 🟡 Route 3 (FALLBACK): Rodin re-generate low-poly
If Route 1's retopo deformation can't be made rig-clean, re-run the *proven* pipeline: re-upload
`castaway_concept_apose.png` (already approved, faceted-styled) to Rodin, but at Confirm pick
**Smart Low-poly (BETA)** or a **low quad density (~1,500–3,000)**, **Baked Normal OFF**, then
the same steps 3–5 above. Costs Creator credits, BETA rig-quality unproven, minor identity-drift
risk. Same destination as Route 1 — hence "fallback," not "parallel."

### ❌ Route 2 (NOT RECOMMENDED): fresh in-house hand-model
Closed option per project record: 7 procedural hero builds failed on gestalt; the Sponsor
ratified Rodin as the *only* hero route. Weapons succeeded interactively because they're
hard-surface simple solids; an organic *rigged humanoid* is the exact case that failed. An
interactive Sponsor-in-loop burst would not change the odds enough to justify reopening it —
and Route 1 already delivers an in-house low-poly result *without* re-modeling.

### ❌ Route 4 (RULED OUT standalone): shader-only flat-shade on the dense mesh
At ≈15k tris flat-shading reads as noise, not chunky planes (facet = polygon size); and the
normal map + soft diffuse would still carry smoothness. `lowpoly-quality.md` explicitly forbids
flat-shading dense/welded meshes. It is a *mandatory step* only *after* reduction, never a fix
on its own.

## Follow-up a tool run would need (I have no Bash this session)
- **Exact current tri count** of `base.fbx` / `castaway_rigged_tpose.fbx` — the README gives
  7,658 verts (≈15k tris for a closed mesh); confirm tris in Blender (`--background --python`
  print of `len(mesh.polygons)` triangulated) before locking the reduction ratio.
- **A retopo A/B** at ~1.5k / ~2.5k / ~3.5k tris flat-shaded, rendered next to a weapon, for
  Gate 1 — the cheapest way to let the Sponsor pick the facet density.

## Sources
- [How to make a low poly 3D model — low-poly.com](https://low-poly.com/blog/how-to-make-low-poly-3d-model)
- [How many polygons should my character have? — Games Learning Society](https://www.gameslearningsociety.org/how-many-polygons-should-my-character-have/)
- [Poly count for mobile characters — polycount](https://polycount.com/discussion/230521/how-many-polys-should-character-models-have-in-a-mobile-game)
- [Remeshing — Blender 5.1 Manual](https://docs.blender.org/manual/en/latest/modeling/meshes/retopology.html)
- [Quad Remesher for Blender — SuperRenders](https://superrendersfarm.com/article/quad-remesher-blender-retopology)
- [Reduce topology: Decimate, Remesh & Retopo — yelzkizi](https://yelzkizi.org/how-to-reduce-topology-in-blender-decimate-remesh/)
- [Hyper3D Rodin — hyper3d.ai](https://hyper3d.ai/) and [Low-Poly style page](https://hyper3d.ai/styles/low-poly)
- [Hyper3D Rodin Gen-2.5 Smart Low-poly — 80.lv](https://80.lv/articles/how-hyper3d-rodin-gen-2-5-is-bringing-production-level-control-to-ai-3d-generation)
- Project ground truth (Read this session): `art-src/castaway-rodin-export/README.md`,
  `Assets/Scripts/Editor/CharacterAssetGen.cs` (L323–359), `texture_diffuse.png`,
  `char_palette.png`, `castaway_concept_apose.png`; docs `character-pipeline.md`,
  `lowpoly-quality.md`, `blender-asset-pipeline.md`, `art-direction.md`.
