# Blender Asset Pipeline — Far Horizon Daily-Use Guardrails

**MANDATORY pre-work read for all Far Horizon Blender / weapon / tool / prop work.**
This is the concise decision-forcing checklist. Full citations and depth at `team/erik-consult/blender-weapon-asset-pipeline-research.md`.

---

## 0. The Style Contract — Read This Before You Open Blender

The weapon/tool/prop family lives in a **single flat-shaded palette world**. The deciding constraint is the shading model: a per-asset baked texture atlas (what the current shipped axe uses) imports its own lighting and reads as a foreign object beside faceted flat geometry. That approach is BANNED for new assets.

Every new asset must:
- Use the **shared palette material** (one URP/Unlit mat + one 128×128 `weapon_palette.png`) — never a per-asset texture.
- Match the inspiration board (`inspiration/21h08_08.png` for axe; `21h06_54`, `21h07_20`, `21h07_42` for the full family). Look at the actual images before modeling anything.
- Be faceted and chunky, not smooth or machined.

> The current shipped axe (`Assets/Art/Props/CastawayAxe/`) is a **placeholder, NOT the style anchor**. Do not tune its look as if it were final.

---

## 1. Project / Scene Setup (do once per `.blend`)

- **Units:** Metric, Unit Scale = 1.0.
- **Save the `.blend` source OUTSIDE `Assets/`** — `art-src/weapons_reauthor.blend` is the live source (established 2026-07-03). Unity auto-imports any `.blend` under `Assets/` when Blender is installed on the machine, so a WIP source dropped next to the FBXs would pollute the Unity project with a duplicate auto-imported model. Only the exported FBX goes into `Assets/Art/Props/WeaponPack/`.
- **Character reference:** 1.8m cube in the scene at all times — all weapon proportions scale against it. Hafts read "chunky" at ~0.08m diameter vs 1.8m height.
- **Collections:** `Blockout` / `LowPoly` / `Export`. Keep finished weapons in `Export` collection for FBX selection.
- **Naming convention:** `wpn_axe_01`, `wpn_knife_01`, `wpn_sword_01`, `wpn_spear_01`, `prop_crate_wood_01`, `env_rock_03`. Consistent prefix (`wpn_` / `prop_` / `env_`) + material + index. Unity mirrors the filename as the asset name.
- **Tier-row convention in `weapons_reauthor.blend`** (wood-tier weapon burst, 2026-07-08): tiers are laid out by Y-row — iron at `y=0.6`, stone at `y=0.0`, wood at `y=-0.6`. `ref_character_18m` is the permanent scale-reference object (the 1.8m character-reference cube from the bullet above) — **never delete it**; reference it by name (`bpy.data.objects['ref_character_18m']`) when scaling new assets against it.

---

## 2. Shared Palette Material — Set Up ONCE, Use on Everything

**Rule: one material to rule them all. No per-asset textures, ever.**

### In Blender
1. Create `weapon_palette.png`: Image Editor > New Image > 128×128, white background. Paint ~12–20 colour blocks, each 16×8 pixels minimum (large enough to UV-snap to). Save to `Assets/Art/Props/WeaponPack/weapon_palette.png`.
2. Create Blender material `WeaponPalette`: Image Texture node → palette PNG → Principled BSDF Base Colour. Set Roughness = 1.0, Metallic = 0.0, Specular = 0.0. This previews flat colour in Blender.
3. Assign `WeaponPalette` as the **only material slot** on every weapon mesh.

### In Unity
1. Create `Mat_WeaponPalette` using **Universal Render Pipeline/Unlit** shader.
2. Assign `weapon_palette.png` as Base Map.
3. Apply this single material to every imported weapon mesh.
4. **Verify with Frame Debugger:** all weapons in the scene should batch into one or two SetPass calls (same shader variant + same texture = SRP Batcher groups them).

**Why:** SRP Batcher batches by shader variant, not by material count. One URP/Unlit shader across 20 weapons = effectively 1 draw call. A unique baked texture per weapon = a draw-call break per weapon — kills batching. (Unity 6 Manual, "SRPBatcher in URP," official doc.)

**Palette starter (12 slots for the weapon set):**

| Slot | Hex | Usage |
|---|---|---|
| WoodBrown | `#8B5E3C` | All hafts |
| WoodDark | `#6B4423` | Grip-wrap shadow band |
| MetalRed | `#C0392B` | Axe head |
| MetalGrey | `#7F8C8D` | Sword blade, spear tip |
| EdgeWhite | `#F5F5F0` | Blade edge-highlight plane (all weapons) |
| GripLeather | `#5C3317` | Crossguard, handle wrap |
| SpearShaft | `#D4A96A` | Spear shaft warm tan |
| StoneGrey | `#95A5A6` | Spear tip, rocks |
| WarmAccent | `#E8B84B` | Decorative grip ring |
| KnifeBlade | `#BDC3C7` | Knife blade |
| ShadowFace | `#4A3728` | Underside/shadow face |
| Black | `#1A1A1A` | Edge outlines if used |

### Scripting the palette PNG via bpy — 4 color-pipeline gotchas (castaway v2/v3, 2026-07-05/06)

When creating/editing a palette PNG via `bpy` (MCP `execute_blender_code` or a headless `--python` script) instead of hand-painting it in the Image Editor, four traps make a CORRECT palette (or a posterized texture) look wrong:

1. **Byte-image `pixels` writes are sRGB-passthrough — never pre-linearize.** A `bpy.data.images.new(...)` byte image (the normal case, `float_buffer=False`) stores `pixels` floats as raw-bytes/255, i.e. already sRGB-encoded; writing a hex color's 0–1 values straight in is CORRECT. Running the value through an sRGB→linear conversion first (correct only for FLOAT-buffer images) double-transforms and darkens/oversaturates every swatch — the "pumpkin skin" failure.
2. **The AgX view transform (Blender 4.x default) visibly desaturates flat palette colors in the viewport.** A punchy hex swatch previews washed-out/muddy under AgX even though the pixel data is right. Set **Color Management → View Transform = Standard** before judging URP/Unlit palette colors in the viewport (that also matches how Unity Unlit will render them), or judge the PNG in an image viewer.
3. **The GPU texture is STALE after a script writes `image.pixels`.** The viewport keeps rendering the pre-edit palette until the texture re-uploads — call `image.update_tag()` (or `image.reload()` after saving to disk) plus a viewport redraw before screenshotting/judging, or a genuine color fix looks like it "changed nothing".

4. **Posterize in HSV, never per-channel RGB (castaway v3, `86cak41d4`).** `round(rgb*4)/4`-style per-channel quantization HUE-SHIFTS regions that should stay one flat color (produced a red neck blotch, green-tinted stubble, red ankles on the castaway diffuse). Convert to HSV first, quantize **V→5 steps** and **S→4 steps**, leave **H untouched**, convert back to RGB before writing pixels. This produced `texture_diffuse_posterized.png`, the Sponsor-locked "posterized flats" hero texture treatment (referenced from `character-pipeline.md`'s Smart-Low-poly step; harvest PR pending).

**Net effect if you skip these:** a byte-correct palette (or a logically-correct posterize) on disk can still fail every Blender-side visual check (wrong hue / washed out / hue-shifted regions / still the old colors). Verify the actual PNG bytes before concluding the color logic is broken.

### Palette-plus-face-patch hybrid: color dots PLUS one painted detail region on the SAME texture (castaway v4, 2026-07-18)

The shared-palette convention above (every UV island scaled to zero and parked on a flat color dot) does not preclude ONE spatially-varying painted detail region on the same texture. Castaway v4's palette PNG (still a single 128×128 image) carries the usual color-dot blocks on one region PLUS a painted 64×64 face patch (brows/eyes/smile/stubble) elsewhere on the same PNG. Every mesh face's UV stays parked on a color dot as usual EXCEPT the head-front quad, which is instead **planar-mapped** into the face-patch region (local mesh-space x→u, z→v, with a small ~1.5px inset to avoid bleeding into the color-dot region) — so ONE face gets a real unwrap while every other face on the model stays a zero-area dot. Keep the shared material's Image Texture node interpolation set to **Closest** (not the default Linear) so both the flat color dots and the painted patch stay crisp with no bleed/blur at their boundary — the face patch is drawn as **pixel rects, not curves** (e.g. 10×12px eyes with a 3×3 white highlight, a brow bar, a 2px smile line, a stubble band), and Linear interpolation would blur those edges into noise at gameplay distance. The sRGB-passthrough / Standard-view-transform / `update_tag()` rules above (color-pipeline gotchas 1–3) apply unchanged to this combined texture. Applies to any future asset needing ONE readable painted detail (a face, a label, a decal) while keeping the rest of the shared-palette convention intact. Full worked example: `art-src/castaway-v4-README.md` + `art-src/castaway_v4_palette.png`.

---

## 3. Modeling — Silhouette First, Details Never

**Rule: block the silhouette with minimum geometry. Do NOT add loops for detail — add loops only to push the silhouette.**

- Start with a **Plane** (axe head, blade) or **Cube** (haft, stock). Never start with a high-sided cylinder for a weapon head.
- **Mirror Modifier (X axis, Clipping = ON)** for all symmetric heads. Apply before export.
- Use **Knife (K)** and **Loop Cut (Ctrl+R)** + **Extrude (E)** to shape. Use **Edge Slide (GG)** to push loops toward the cutting edge for blade taper — no extra geometry added.
- **Haft/handle:** 5–6-sided cylinder. 8+ sides is too round for this style.
- **Chunky cartoon look comes from off-center axe heads, notched grip bands, and faceted facets — NOT a bent haft.** The HAFT is STRAIGHT (Sponsor decision 2026-06-23: the whole weapon family — axe/knife/sword/spear — has straight handles, matching the board axe `21h08_08`; the earlier "slight 2–5° haft bend" rule is RETIRED). Keep the imperfection in the head / grip / facet detail, not in a curved handle.
- **White edge-highlight plane** (the crisp read in the inspiration board): inset (I, ~0.05m) the front blade face → separate that thin strip as its own mesh island → UV it to the `EdgeWhite` palette block. This is physical geometry, NOT a shader effect.

**Triangle budgets (Far Horizon style):**

| Asset | Target tris |
|---|---|
| Knife | 80–300 |
| Axe | 150–400 |
| Sword | 200–500 |
| Spear | 150–300 |
| World prop (rock, stump) | 50–200 |

No Sub-D modifiers. No Bevel modifier. No Subdivision Surface. These are hard polygon assets.

### Family-extension route: duplicate an approved sibling instead of reblocking from scratch (pickaxe burst, ticket `86cakkmmz`, pending I-1 harvest PR)

The "start with a Plane/Cube" rule above is for the FIRST asset in a family. When adding a new same-tier tool that shares its handle family with an approved sibling (e.g. a new stone/iron tool alongside the approved axes): **duplicate the approved sibling object, delete ONLY its head mesh-island, keep the haft/grip/pommel islands verbatim** — their exact geometry AND palette UVs carry forward untouched. Guarantees family consistency (identical handles across the tier) and zero re-UV work; only the new head gets modeled fresh. Before deleting, verify island identity by vertex-count + z-range (§12 item 5) — never infer membership from position/z-order. Precedent: `wpn_pickaxe_stone_01` (88 tris) / `wpn_pickaxe_iron_01` (154 tris) built from the approved axe siblings' 32-vert / 22-vert head-island deletions (`art-src/weapons_reauthor.blend`).

### Closed-shell heads (knife/sword) can't be extruded from a ring after deletion — build a fresh shell instead (wood-tier weapon burst, 2026-07-08)

The family-extension route above (duplicate sibling, delete only the head island) assumes the remaining haft/grip stump has an open ring to extrude a new head from. That holds for **spear / axe / pickaxe** — their heads are open-ended where they meet the haft, so deleting the head leaves a clean boundary ring to extrude the replacement head from.

**Knife and sword are different: the handle is a CLOSED shell and the blade is a SEPARATE closed shell** (not one continuous mesh with an open junction). Deleting the blade's faces leaves **no boundary ring at all** — there is nothing to extrude from, and an extrude-from-selection op against the empty face-loop fails (empty-selection divide-by-zero). **Fix:** don't try to extrude a new blade from the handle stump. Build the new blade as its own fresh closed shell (rings of 4 verts stepping down the blade profile, closed with an apex vert at the tip) and `Join` it to the handle shell, the same way the original asset was built. Spear/axe/pickaxe stay extrude-from-ring; knife/sword are build-fresh-shell-and-join.

### Crosswise-mounted heads (pickaxe / hammer / mattock class) need a BOX-section eye, not a diamond/lens section (ticket `86cakkmmz`, pending I-1 harvest PR)

The biface diamond/lens cross-section used for in-line blade heads (axe/knife/sword/spear) thins toward its top ridge — fine when the haft meets the head from below, but it fails for a **crosswise-mounted head** whose eye the haft passes straight THROUGH: where the eye overlaps the haft's TOP, the hex haft's corners poke through the head's sloped upper faces as a visible wood-notch defect. **Widening the diamond does NOT fix it** — the section fundamentally cannot enclose a box near its ridge (cost 2 iterations to diagnose on the pickaxe burst). **Fix:** model the eye segment (the short run the haft passes through) as its own **BOX section** — flat top over the haft cap, wide enough to fully enclose the haft — then transition to diamond/tapered sections for the arms. Keep the eye box modest or the head reads as a mushroom cap (an iteration-3 defect): `~0.06 × 0.06 × 0.084` against the family's `~0.05`-diameter haft was the fit that read correctly. In-line biface heads are unaffected — keep the diamond/lens language for those.

**The crosswise class ALSO needs its own in-hand seat euler (PR #283 finding, merged):** a crosswise-mounted head seated at an in-line weapon's dialed rig rotation presents EDGE-ON to the camera — it reads blade-like, not a T-tool, even though the mesh is correct. The axe-seat baseline is a valid mechanical start (shared haft/grip origin), but plan a per-tool seat dial (Sponsor picker/F9, per [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]) for every crosswise tool — do NOT copy an in-line sibling's euler and call the look done, and do NOT guess-dial it yourself.

---

## 4. Shading — Shade Smooth + Mark Sharp (Blender 4.1+)

**Rule: NO Edge Split modifier. NO Auto Smooth checkbox. Those are pre-4.1 workflows. Use Mark Sharp only.**

1. In Object Mode: right-click mesh → **Shade Smooth**. This makes the whole mesh smooth-shaded.
2. In Edit Mode, Edge Select mode: select every edge that must be a hard visual break (silhouette edges, blade-to-face transitions, grip band edges, head-to-haft transitions).
3. **Edge > Mark Sharp** (or Ctrl+F shortcut, or right-click > Mark Sharp). Blender treats marked edges as normal splits.
4. For a FULLY faceted look (every polygon reads as a flat plane — the Far Horizon default for most weapons): select ALL edges (A) → Mark Sharp. This achieves the faceted appearance without the vertex-count explosion that Shade Flat would cause.

**Why this matters for Unity:** on FBX export with `Smoothing = Normals Only`, Blender exports the computed per-vertex normals directly. Unity reads them on import (`Normals = Import`). The faceted/smooth split is preserved exactly. If you use Edge Split modifier instead, you get doubled geometry on every hard edge — vertex count inflates unnecessarily.

---

## 5. UV — Palette Block Placement (Not Traditional Unwrap)

**Rule: UV islands are NOT unwrapped to show surface detail. They are scaled to near-zero and PLACED on the palette colour block for that face's colour. No stretching check needed — the island is a dot.**

1. In Edit Mode, select all (A).
2. UV Editor: `U` > **Smart UV Project** (angle limit 66°, island margin 0.001). The shape of the unwrap does not matter.
3. Select all UV islands in the UV Editor. `S > 0.001 > Enter` — scales all islands to near-zero.
4. Move each island (or face group) to sit over its palette colour block: `G` + move, snap to the block's centre pixel.
5. To assign a different colour to specific faces: select those faces in Edit Mode, then in the UV Editor move just those UV islands to a different palette block.

**Gotcha:** if faces share the same UV island and need different colours, you must separate them first (in Edit Mode, select → `P > Separate by Selection` or manually split the island). Plan colour regions before unwrapping.

### Palette-tile coordinates + UV-cluster face selection (wood-tier weapon burst, 2026-07-08)

The durable rule is the **tier split by tile U-coordinate: faces with UV `u > 0.4` are stone/iron tones; faces with `u ≤ 0.4` are wood/leather tones.** This lets you select faces by their UV-cluster position (script a `u` test against each face's UV island) rather than hand-picking in the viewport or trusting object/material naming — useful when a single mesh mixes tiers (e.g. an iron head on a shared haft) or when auditing an existing asset's tier assignment.

Measured tile centers (a snapshot of the CURRENT `weapon_palette.png` — the u>0.4 split is the rule to trust long-term; these coordinates are an empirical reading of today's palette layout and must be **re-verified if the palette is ever repainted**):

| Tile | UV center (u, v) |
|---|---|
| Haft brown | (0.05, 0.05) |
| Dark brown | (0.145, 0.05) |
| Leather reds | ~(0.24–0.33, 0.05) |
| Iron blue-grey | ~(0.42, 0.05) |
| White | (0.515, 0.05) |
| Tan | (0.617, 0.05) |
| Stone grey | (0.805, 0.05) |
| Dark grey | (0.90, 0.05) |
| 2nd-row iron | y ≈ 0.15+ |

---

## 6. Origin / Pivot Placement for HeldTool Rig

**Rule: origin = grip midpoint, weapon pointing +Z (blade up) in Blender.**

This is load-bearing for the `HeldTool` rig in Unity. The rig parents the weapon to a hand socket bone; the socket's +Y axis is blade-forward in Unity (which maps from +Z in Blender after axis conversion). A wrong origin causes the weapon to orbit around its base when the player's hand rotates.

1. In Edit Mode, select the vertices defining the grip centre (top of handle, where the hand closes).
2. `Shift+S > Cursor to Selected`.
3. Object Mode > Right-click > **Set Origin > Origin to 3D Cursor**.
4. Verify: in Object Mode the origin gizmo sits at the grip, and the blade end points toward +Z.

Do not move the origin to world (0,0,0) after this step. The offset between the grip origin and the mesh geometry is intentional and correct.

---

## 7. Pre-Export Checklist (every weapon, every time)

Run these IN ORDER before File > Export > FBX:

- [ ] **Ctrl+A > Apply All Transforms** on every weapon object. Verify: Scale = (1,1,1), Rotation = (0°,0°,0°). Location may be non-zero if origin is at grip — that is correct; do NOT zero the location.
- [ ] **Shift+N > Recalculate Normals** (Outside). Verify with Overlay > Face Orientation: all blue, no red faces.
- [ ] No loose geometry: Edit Mode > M > **Merge by Distance** (threshold 0.001). Dismiss result.
- [ ] One material slot only: `WeaponPalette`. No additional material stubs.
- [ ] UV islands on palette blocks (spot-check 3–4 faces in UV Editor).
- [ ] Mirror Modifier applied (if still present).
- [ ] Pivot / origin confirmed at grip, blade pointing +Z.

---

## 8. FBX Export Settings — Exact Values

**File > Export > FBX (.fbx)**

| Setting | Value | Why |
|---|---|---|
| Selected Objects | ON | Export one weapon at a time for per-asset import control |
| Apply Scalings | **FBX Unit Scale** | Safe for both static and rigged meshes; avoids the 100× scale bug |
| Forward | **-Y Forward** | Aligns Blender's Y-back to Unity's Z-forward |
| Up | **Z Up** | Maps Blender Z-up to Unity Y-up via Bake Axis Conversion |
| Use Space Transform | **UNCHECKED** | Let Unity's Bake Axis Conversion handle it |
| Apply Transform | **UNCHECKED** | Deprecated; corrupts rigs if enabled |
| Smoothing | **Normals Only** | Exports Mark Sharp-derived normals; Unity reads them correctly |
| Tangent Space | ON | Required if normal maps are ever added; harmless now |
| Armature | OFF | Static weapon meshes have no rig |
| Materials | Reference external PNG | Do NOT embed — embedding is unreliable on Unity import |

**PDF note:** The attached PDF guide recommends `Apply Transform = ON` and `Smoothing = Face`. These are WRONG for this project. `Apply Transform` corrupts rigged meshes and is deprecated; `Smoothing = Face` discards the Mark Sharp normal data and falls back to flat-shaded per-face normals (loses the Shade Smooth gradient on grip cylinders). Use the settings in the table above.

Save FBX to `Assets/Art/Props/WeaponPack/`: `wpn_axe_01.fbx` etc.

> **§8 is for weapons/props/static meshes ONLY — NEVER for a character headed to Mixamo (castaway v4, `86catpwc4`, 2026-07-18).** These settings declare `Up=+Z / Front=+Y` in the FBX GlobalSettings and rely on Unity's Bake-Axis-Conversion to digest that; **Mixamo has no such option** and auto-rigs the character BACKWARD (back-facing load; Orient-step rotation + correct markers do NOT save it). Characters destined for Mixamo export with **Blender's FBX defaults** (`-Z Forward / Y Up / FBX_SCALE_NONE`, no geometry rotation) — confirmed root cause, the raw-parse verification ritual, and the full recipe live in `character-pipeline.md` §Step 3.

---

## 9. Unity Import Settings

After dragging the FBX into `Assets/Art/Props/WeaponPack/`:

**Model tab:**
- Normals: **Import** (reads Blender's exported normals).
- Tangents: Import.
- Bake Axis Conversion: **ON** (fixes Blender Z-up vs Unity Y-up without the 90° X-rotation hack).
- Optimize Mesh: ON — but verify the faceted normals survive. Check in Scene view; if shading looks wrong, disable.

**Materials tab:**
- Material Creation Mode: **None** (we assign `Mat_WeaponPalette` manually; Unity must not auto-create material stubs).

**After import:**
- Drag `Mat_WeaponPalette` onto the MeshRenderer component.
- Add a **Box Collider** (not MeshCollider — too expensive for a hand tool).
- Open Frame Debugger and verify all weapons in the scene batch into one SetPass call.

**Import scale — normalize by the STYLE-LOCKED dimension, not total/longest-axis (PR #100, `86cabh907`):** when a multi-part asset has one component whose size is Sponsor-LOCKED (e.g. the axe HEAD locked at 0.65×) and another that resizes (the haft lengthened 2×), do NOT normalize `globalScale` by the longest axis (≈ total length) — that holds total length constant and silently RE-SCALES the locked part when the other grows (it would have shrunk the approved head). Instead pin `globalScale` to hold the LOCKED dimension at its approved absolute size: head-height locked → keep head 0.471u → total length grows freely to 1.500u (`globalScale` 1.0576). Verify with the §12 vertex-bounds snippet that the locked component's measurement is invariant before/after the resize.

**Rotation corollary (#100 re-bake, 2026-06-23):** re-ORIENTING a locked component (e.g. rigidly rotating a tilted axe head upright to kill a dogleg) CHANGES its long-axis PROJECTION even though its true 3D size is invariant — the head's height-from-tip projection went 0.445→0.489u on a pure ~20° uprighting (the head got no bigger; its axis just realigned). If the import normalize constants reference that projection (`HeroAxeHeadHeightFromTipU` / `HeroAxeTargetHeadHeightU`), you MUST re-derive them after ANY rotation, or the locked-dimension normalize silently rescales the part (~9% head shrink here). Prefer a rotation-INVARIANT reference (the component's intrinsic bounding-box diagonal / true extent) over an axis-projection, or re-derive the projection constant on every re-orient.

---

## 10. Blender MCP — What to Automate, What Not To

MCP (`execute_blender_code`) is useful for mechanical batch steps. It cannot make creative topology or edge-flow decisions.

| Task | MCP-automate? | Notes |
|---|---|---|
| Create palette PNG with hex values | YES | `bpy` pixels array; one-time setup |
| Create / configure `WeaponPalette` material | YES | `bpy.data.materials.new` + node API |
| Apply All Transforms (Ctrl+A) | YES | `bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)` |
| Recalculate Normals | YES | `bpy.ops.mesh.normals_make_consistent(inside=False)` |
| Scale all UV islands to near-zero + move to palette block | YES — HIGH VALUE | Script the whole UV placement step after human defines the islands |
| FBX export with exact settings | YES | `bpy.ops.export_scene.fbx(...)` parameterised per-weapon |
| Blockout shape, blade profile, edge loops | HUMAN only | MCP cannot produce production-quality topology |
| Mark Sharp on specific edges | PARTIAL | Human pre-selects edges; MCP runs `bpy.ops.mesh.mark_sharp()` |
| Set origin from grip vertex | PARTIAL | Human selects vertices; MCP runs Set Origin |

### MCP availability — orchestrator R&D lane ONLY; dispatched personas use headless CLI

**The `mcp__blender__*` tools (including `execute_blender_code`) are available ONLY to the orchestrator's own session.** A dispatched persona's tool allow-set is Read / Write / Edit / Grep / Glob / Bash / Skill / WebFetch + the ClickUp MCP tools — it contains **no** `mcp__blender__*` tool. If you are a dispatched Devon/Drew reading this, you cannot call any Blender MCP tool, and `ToolSearch("blender")` will not surface a usable one.

**Dispatched personas drive Blender headlessly via CLI instead:**

```bash
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" \
  --background <path/to/source.blend> \
  --python <path/to/script.py>
```

Write the `bpy` operations (material setup, UV placement, transform-apply, normals, FBX export) as a standalone `.py` script and pass it via `--python`. **The `bpy` API is identical** — every snippet that would run in `execute_blender_code` runs unchanged in the script; every YES/PARTIAL row in the table above is achievable this way. Exit code 0 = success; non-zero = a Python exception (read stdout for the traceback). Commit the script next to the FBX so future passes re-run deterministically.

**Empirical precedent (PR #100, ticket `86cabh907`):** Devon re-authored `Assets/Art/Props/WeaponPack/wpn_axe_01.fbx` (axe-head shrink to 0.8×) by running Blender 5.1 `--background --python` headlessly — scaling only the 45 blade verts about the head-base pivot, preserving grip-point origin (0,0,0) + +Z forward axis + the single `WeaponPalette` material slot, then re-exporting FBX (`-Y Forward / Z Up / Normals Only`) — *because the MCP tools were not exposed to his agent.*

| Route | Who | When |
|---|---|---|
| `mcp__blender__execute_blender_code` | Orchestrator only (R&D lane) | Interactive iteration with a live Blender instance |
| `blender --background --python script.py` | Dispatched Devon / Drew | Deterministic mechanical steps: transforms, UV, export |

When briefing a dispatched Blender asset task, include the exact Blender executable path + a pointer to the `.blend` source so the persona scripts it headless from the start.

### `get_viewport_screenshot` returns black when the Blender window isn't drawing — render to a file instead (2026-07-06)

**Symptom:** `mcp__blender__get_viewport_screenshot` returns an all-black image with no error when the Blender window is minimized or not actively drawing.

**Diagnostic:** run `bpy.ops.wm.redraw_timer(type='DRAW_WIN_SWAP', iterations=2)` — completing in ~0.005–0.01ms average confirms the window genuinely isn't drawing (a real redraw takes measurably longer), i.e. the black screenshot is a draw-state problem, not a scene/lighting bug.

**Workaround — render a real camera shot to a file instead of the viewport screenshot tool:**
1. Point a camera at the target mesh (aim via `track_quat` / rotate toward the mesh's world-space centroid).
2. Set `scene.view_settings.view_transform = 'Standard'` (per §2 — matches how Unity Unlit renders palette colours; AgX would desaturate the judge shot).
3. Isolate the asset(s) under judgment: `obj.hide_render = True` on unrelated scene meshes (temporarily; restore after).
4. Add a temporary `SUN` light so the render isn't black from missing illumination.
5. Set `scene.render.filepath` and call `bpy.ops.render.render(write_still=True)`, then `Read` the PNG.
6. **Cleanup before saving the `.blend`:** delete the judge camera/light and any reference meshes imported for A/B comparison, then `bpy.ops.outliner.orphans_purge(do_recursive=True)` — don't let judge helpers leak into the saved source file.

```python
import bpy, mathutils

cam_data = bpy.data.cameras.new("JudgeCam")
cam = bpy.data.objects.new("JudgeCam", cam_data)
bpy.context.scene.collection.objects.link(cam)
target = bpy.data.objects["wpn_axe_stone_01"].matrix_world.translation
cam.location = target + mathutils.Vector((0, -1.2, 0.3))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = cam

sun_data = bpy.data.lights.new("JudgeSun", type='SUN')
sun = bpy.data.objects.new("JudgeSun", sun_data)
bpy.context.scene.collection.objects.link(sun)

bpy.context.scene.view_settings.view_transform = 'Standard'
bpy.context.scene.render.filepath = "<session scratchpad>/judge_axe.png"
bpy.ops.render.render(write_still=True)
```

**Prefer this render-to-file method over `get_viewport_screenshot` by default** for any Blender judging step — not only after hitting the black-screen symptom — whenever the Blender window's draw state is uncertain (minimized, backgrounded, long-running MCP session with focus elsewhere).

### A pure 90° profile render of a box-built/segmented character is illegible — bias the side camera ~12° off-axis (castaway v4, 2026-07-18)

A dead-on profile shot of a faceted/chamfered-block character (built from flat box segments, not an organic Rodin mesh) puts one flat plane square to the camera, so the render reads as a featureless silhouette regardless of lighting — no shading gradient to read form from. Bias the side/profile judge camera ~12° off the true profile axis so a sliver of the front plane catches distinct light and the shot reads as a form, not a flat card. Applies to the render-to-file judging method above for any faceted/chamfered-block asset shot from a directly-orthogonal angle, not just characters.

### `bpy.ops.import_scene.fbx` fails "Context missing active object" on armature-bearing FBX (castaway v3, `86cak41d4`)

Importing a plain-mesh FBX via `execute_blender_code` works fine, but an FBX carrying an **armature** (e.g. a Mixamo-rigged export) fails with `RuntimeError: Operator bpy.ops.object.mode_set.poll() Context missing active object` — the importer's armature-build step enters Edit Mode, which needs a real 3D-viewport context the MCP bridge's synthetic call doesn't provide.

**Fix — wrap the import in a manual context override targeting a `VIEW_3D` window/area/region:**

```python
import bpy
window = bpy.context.window_manager.windows[0]
area = next(a for a in window.screen.areas if a.type == 'VIEW_3D')
region = next(r for r in area.regions if r.type == 'WINDOW')
with bpy.context.temp_override(window=window, area=area, region=region):
    bpy.ops.import_scene.fbx(filepath=r"<path to rigged .fbx>")
```

Plain-mesh (non-armature) FBX imports do NOT need this — a mesh-only import succeeding earlier in the session won't reveal the gap, so apply the override proactively whenever the FBX carries a rig.

### `scene.camera` is UNBOUND after `bpy.ops.wm.open_mainfile` — rebind before rendering (wood-tier weapon burst, 2026-07-08)

Opening a different `.blend` via `bpy.ops.wm.open_mainfile(filepath=...)` does not carry over `scene.camera` even when the target file has a camera object in it — the next render call fails with "no camera" (`bpy.ops.render.render` raises because `scene.camera` is `None`). This bites every time a script opens a fresh `.blend` and then tries to render/judge in the same pass (e.g. the §10 render-to-file judging method above, run right after switching source files).

**Fix:** rebind the camera immediately after every `open_mainfile` call, before any render op:

```python
bpy.ops.wm.open_mainfile(filepath=r"<path to .blend>")
bpy.context.scene.camera = bpy.data.objects['Camera']  # rebind — open_mainfile does not restore scene.camera
```

If the file's camera object has a different name, look it up by type (`next(o for o in bpy.data.objects if o.type == 'CAMERA')`) rather than assuming `'Camera'`.

### `matrix_world` is STALE immediately after creating/linking objects or setting `rotation_euler` — call `view_layer.update()` before measuring (castaway v4, 2026-07-18)

Blender does not recompute the dependency graph synchronously on every `bpy.data.objects.new(...)` + link, or on every `rotation_euler` assignment — it happens on the next depsgraph evaluation, which does NOT occur automatically between consecutive statements in an `execute_blender_code` / headless script. Reading `obj.matrix_world` (or any world-space/camera-relative value derived from it — bounding-box union across parts, camera-relative aim vectors, cross-object distances) immediately after either op returns a STALE value (identity for a freshly linked object, or the pre-assignment value after a rotation write) — not what the object's current transform/parenting implies. This bit the same session twice, silently (no exception raised, just a wrong number):

1. **World-space measurement lied.** A 40-part, 1.90m-tall character measured `height=0.62` immediately after every part was created and linked — every part's `matrix_world` was still identity, so only the largest single part's LOCAL extent got measured, not the assembled whole.
2. **Camera-relative computation used a stale orientation.** A sun-light aim computed from `cam.matrix_world` right after repositioning the camera for a new shot used the PREVIOUS shot's orientation (the rotation assignment hadn't propagated yet) — produced a washed-out, unreadable render.

**Fix:** call `bpy.context.view_layer.update()` immediately after any object creation/linking, parenting change, or `rotation_euler`/`location`/`matrix_world` assignment, and BEFORE reading any world-space or camera-relative value off it. Cheap to call defensively — call it before every measurement or camera-aim step in a script, not just after a symptom shows up.

---

## 11. Style Checklist — Sign Off Before Calling an Asset "Done"

Check every item against the inspiration board (`21h08_08` for axe; `21h06_54`, `21h07_20`, `21h07_42` for the family):

- [ ] No smooth curved surfaces — all faces planar and visually faceted.
- [ ] Haft is STRAIGHT (Sponsor decision 2026-06-23 — weapon family handles are straight, not bent; the prior 2–5° bend rule is retired).
- [ ] White edge-highlight plane exists on every blade — narrow inset, UV on `EdgeWhite` block.
- [ ] Proportions match the 1.8m character reference — haft diameter ~0.08m.
- [ ] All faces use palette colours only — no per-face vertex colour paint, no per-asset textures.
- [ ] All mesh normals outward-facing (Overlay > Face Orientation: all blue).
- [ ] No internal geometry — Merge by Distance run, 0 loose verts.
- [ ] Origin at grip midpoint, blade points +Z in Blender.
- [ ] UV islands scaled to ~0.001, parked on correct palette blocks.
- [ ] Single material slot: `WeaponPalette`.

---

## 12. Pre-Serve Weapon Verification — Measure in Blender, NOT the Gameplay Capture

**The default CI gameplay capture CANNOT confirm a held weapon's size or look.** It's taken at SPAWN, where the weapon is a GROUND pickup not yet equipped (the held mesh is visibility-gated) — so the castaway is empty-handed and no weapon shows; and even once equipped, the rear-orbit gameplay angle frames it poorly. Do NOT treat a green CI capture as visual confirmation of weapon proportion. (Full evidence: `unity-conventions.md` § "DEFAULT gameplay capture does NOT frame a HELD weapon".)

**Pre-serve verification for a held-weapon geometry change (proven on PR #100 / `86cabh907`):**

1. **Measure in Blender (preferred).** After the headless scale op, print the actual vertex bounds of the resized component:
   ```python
   import bpy
   obj = bpy.data.objects["wpn_axe_01"]  # adjust mesh name
   xs = [(obj.matrix_world @ v.co).x for v in obj.data.vertices]
   print(f"Head X-width: {max(xs)-min(xs):.4f}m")
   ```
   Confirm the value matches the intended factor (e.g. `0.6638 × 0.65 = 0.4315`).
2. **Verify FBX identity.** Confirm the exported FBX is byte-identical to the known-good source mesh THEN a pure uniform scale (never a re-author) — uniform-scale-only is the hard rule that ended the #100 flat-wood saga.
3. **Confirm Unity import scale** — catches a wrong FBX Unit-Scale reimport (100× / 0.01×).

For a per-PR VISUAL judge, use a dedicated frontal weapon-display capture (the `WeaponSetVerifyCapture` / lineup-prefab path), not the default gameplay-back capture. The Sponsor still judges in-game by orbiting the cam himself — these steps are the TEAM's confidence gate before handing off the soak.

**4. Measure the JUNCTION ANGLE between components, not just each component's internal straightness (#100, `86cabh907`, 2026-06-23).** A multi-part prop can pass a "straightness" check and still read as BENT. On #100 the haft measured `residual_bend 0.0000°` (perfectly straight in X/Y/Z) and the head was byte-LOCKED (shape verified preserved) — yet the Sponsor saw a clear "bend." Root cause: the **head mounted 20.14° off the haft long axis** (head principal axis `(+0.3428,−0.0311,+0.9389)` vs haft +Z), reading as a dogleg at the head end. The straightness check measured only the haft RINGS; nothing angle-checked the head-vs-haft junction. **Rule: when verifying a multi-part prop is "straight," measure the MOUNT-LINE angle — the line from the JUNCTION point to the component's centroid, vs the shared long axis (e.g. `(haft-top → head-centroid)` vs `haft +Z`) — NOT the component's own internal principal/centroid axis.** The two references DIVERGE, and the internal axis is the WRONG one: on #100 (re-bake, 2026-06-23) zeroing the head's INTERNAL centroid-line (its intrinsic mass lean) left a **2.71° residual that STILL read as a dogleg**, while zeroing the MOUNT line about the junction got it to **0.02° = genuinely coaxial**. A straight haft + a locked head still read bent at the junction if you correct the wrong axis. Fix = a RIGID rotation of the off-axis component **about the JUNCTION point** until the mount line is coaxial (preserves the component's locked shape/size — not a reshape; re-derive any §9 import-normalize constant per the rotation corollary above).

**5. Verify mesh-island IDENTITY before transforming — don't infer membership from z-order (multi-island weapons, 2026-07-06).** A multi-part weapon can have MORE islands than "haft + head" — `wpn_axe_stone_01` is THREE separate mesh islands: head biface (32v), main haft (12v, rings only at z=0 and z=0.62), and a grip band (12v, a SEPARATE island spanning z 0.10–0.21). A "lowest island = haft, rest = head" heuristic silently swept the grip band into the head transform. **Rule: before any per-component edit, print each island's own vertex z-range and confirm the count and range match your mental model — never assume island count or membership from position alone.** Caught by re-measuring per-island z-ranges; fixed by inverting the selection, not by re-deriving from scratch. This check applies one level earlier than §12.4's junction-angle check — get the island grouping right before you ever measure an angle between components.

**6. Proportion-edit recipe: choose pivots ON the shared axis so junction coaxiality is preserved BY CONSTRUCTION (Sponsor-approved, 2026-07-06).** When resizing a multi-part weapon's proportions: thin the **haft** per-ring, radially, about **each ring's own centroid** (z untouched — the straight-haft rule stays intact; a grip band thinned by the same factor stays proud of the haft). Shrink the **head** uniformly about the **haft-top-ring centroid** (e.g. `(0,0,0.62)`), NOT the head's own centroid — a pivot chosen ON the haft axis keeps the head-haft junction coaxial automatically, without a separate angle-correction pass. This composes with the §12.4 junction-angle rule: get the pivot right and the mount-line check should already read ~0°. For a per-PR visual judge, A/B-render the edited mesh against the original FBX side-by-side (§10 render-to-file method) rather than trusting vertex-bounds numbers alone — a coaxiality regression is easy to see, harder to catch from measurements.

---

## Quick Reference: Critical Don'ts

| If you're tempted to... | Do this instead |
|---|---|
| Create a per-weapon texture / bake normals | Use the shared `weapon_palette.png` + palette UV placement |
| Use Edge Split modifier for hard edges | Mark Sharp in Edit Mode (Blender 4.1+ workflow) |
| Start with a high-sided cylinder for an axe head | Start with a Plane + Mirror Modifier |
| Add loops for surface detail | Add loops only to push the silhouette shape |
| Leave Smoothing = Face on FBX export | Smoothing = Normals Only |
| Check Apply Transform on FBX export | Leave Apply Transform UNCHECKED |
| Leave origin at world (0,0,0) | Set origin to grip midpoint before export |
| Add a MeshCollider in Unity | Add a Box Collider |
| Let Unity auto-create material stubs | Material Creation Mode = None; assign manually |
| Tune the current shipped axe as the style reference | The shipped axe is a placeholder — use `21h08_08` as the target |
| Reblock a new same-tier tool from scratch | Duplicate the approved sibling, delete only its head island (verify identity via §12 item 5 first), keep haft/grip/pommel verbatim |
| Use a diamond/lens eye section on a crosswise-mounted head (pickaxe/hammer/mattock) | Use a BOX-section eye wide enough to enclose the haft; taper to diamond only on the arms |
| Extrude a new knife/sword blade from the handle stump after deleting the old one | Build a fresh closed shell (rings + apex) and `Join` it — knife/sword blades are a separate closed shell, not an open ring |
| Render or screenshot right after `bpy.ops.wm.open_mainfile` | Rebind `scene.camera = bpy.data.objects['Camera']` first — `open_mainfile` leaves it unbound |

---

*Full evidence and source citations: `team/erik-consult/blender-weapon-asset-pipeline-research.md`*
