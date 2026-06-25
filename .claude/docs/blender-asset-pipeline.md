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
- **Character reference:** 1.8m cube in the scene at all times — all weapon proportions scale against it. Hafts read "chunky" at ~0.08m diameter vs 1.8m height.
- **Collections:** `Blockout` / `LowPoly` / `Export`. Keep finished weapons in `Export` collection for FBX selection.
- **Naming convention:** `wpn_axe_01`, `wpn_knife_01`, `wpn_sword_01`, `wpn_spear_01`, `prop_crate_wood_01`, `env_rock_03`. Consistent prefix (`wpn_` / `prop_` / `env_`) + material + index. Unity mirrors the filename as the asset name.

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

---

*Full evidence and source citations: `team/erik-consult/blender-weapon-asset-pipeline-research.md`*
