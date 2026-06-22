# Blender Weapon & Tool Asset Pipeline — Far Horizon

## Question

What is the best-practice end-to-end Blender workflow for Devon to produce the axe/knife/sword/spear weapon-and-tool pack under Route A: a cohesive matched set built with one shared style spec, one shared low-poly palette material, exported cleanly to Unity 6 URP, and drive-able via Blender MCP?

---

## Bottom Line

Use a **flat-color palette texture approach (Imphenzia / PixPal pattern)**: one 128×128 PNG palette texture, one URP Unlit or Simple Lit material shared by all weapons, UV islands placed on specific palette color blocks in Blender. This is the only approach that achieves: (a) perfect style-system cohesion across the whole set, (b) SRP Batcher compatibility at 1 draw call per weapon (same shader variant, same texture), and (c) zero baking pipeline — every weapon is a pure blockout-to-export workflow with no normal-map bake step. The Blender side is Shade Smooth + Mark Sharp on key hard edges (Blender 4.1+ workflow, no Edge Split modifier), FBX export with -Y Forward / Z Up / FBX Unit Scale, and a shared `.blend` source file that holds all four weapons.

---

## Evidence

### E1 — Imphenzia PixPal Palette Texture System
**Source:** Imphenzia, "PixPal Palette Texture," imphenzia.com/imphenzia-pixpal, published and maintained 2022–2025. Blender/Unity/Godot/UE5 example projects are public domain.
**Strength: Strong** (shipped-tool documentation by a solo-dev creator who demonstrated the approach across multiple released games; Unity URP version confirmed functional).
**What it says:** One 128×128 pixel PNG contains all colours in a grid. Every mesh in the scene shares the same material (one URP shader + one texture). UV islands are not unwrapped in the traditional sense — they are simply **scaled to near-zero and placed on the pixel(s) that represent the target colour**. To change a face's colour, move its UV island to a different palette pixel. This means zero per-asset texture, zero baking, and every mesh can be batched under the SRP Batcher because they all share the same shader variant and the same texture object. The 128×128 texture supports hundreds of distinct colour regions.

### E2 — SRP Batcher Batching Rule (same shader variant)
**Source:** Unity Manual, "Scriptable Render Pipeline Batcher in URP," docs.unity3d.com/Manual/SRPBatcher.html, Unity 6 (6000.x). Official engine documentation.
**Strength: Strong**.
**What it says:** The SRP Batcher batches draw calls that share the same **shader variant** — not the same material instance. Any number of material instances using the same shader variant are batched. Palette texture approach uses one URP/UnlitShader (or URP/SimpleLit) across all weapons — they all batch at 1 or 2 draw calls regardless of count. Per-asset baked textures with separate materials would NOT batch (each unique material = one draw call batch break unless they happen to share shader variant, which does not save much in practice). To remain SRP Batcher compatible, avoid MaterialPropertyBlocks and ensure each material declares its properties via a CBUFFER named `UnityPerMaterial` — URP built-in shaders (Unlit, Simple Lit) already satisfy this.

### E3 — Flat Shading vs Smooth + Mark Sharp in Blender 4.1+
**Sources:**
- Blender Manual, "Shade Smooth & Flat," docs.blender.org/manual/en/latest/modeling/meshes/editing/face/shading.html. Official documentation. **Strong.**
- KatsBits, "Shade Smooth (Mesh Smoothing)," katsbits.com/codex/smoothing/, 2024. Well-maintained community reference. **Strong.**
- Polycount discussion, "Exporting Smoothing information from Blender via FBX," polycount.com/discussion/155012, forum consensus. **Moderate.**
**What it says:** In Blender 4.1+, Auto Smooth and the Edge Split modifier are no longer needed. Workflow: apply **Shade Smooth** to the whole object, then in Edit Mode select edges that should be hard (silhouette breaks, blade-to-face transitions, grip-to-head transitions) and use **Edge > Mark Sharp**. Blender automatically treats those edges as normal splits. On FBX export, set **Smoothing = "Normals Only"** — this exports the computed per-vertex normals directly, which Unity respects. Unity's Model Import Inspector > Normals should be set to **Import** (not Calculate). The faceted look for Far Horizon weapons is achieved by marking every edge that separates visually distinct faces. For a fully faceted chunky look (like the inspiration board references), mark ALL edges sharp — equivalent to Shade Flat but without the vertex-count explosion.

### E4 — FBX Export Settings: Blender to Unity (Axis + Scale)
**Sources:**
- RustyCruise Labs, "Exporting FBX from Blender to Unity," rustycruiselabs.com/devlogs/generic/2024-10-19-blender-to-unity-settings/, October 2024. Practical devlog with confirmed settings. **Moderate.**
- Polynook, "How to Export Models from Blender to Unity," polynook.com/learn/lesson/how-to-export-models-from-blender-to-unity. Maintained tutorial. **Moderate.**
- EdyJ, "Blender to Unity FBX Exporter," github.com/EdyJ/blender-to-unity-fbx-exporter. Open-source addon addressing axis/scale issues. **Strong** (widely cited, thousands of downloads).
**What it says (consensus across three sources):**
- **Apply Scalings:** "FBX Unit Scale" (do NOT use "FBX All" for skinned/rigged meshes; for static props either works but FBX Unit Scale is safest).
- **Forward:** -Y Forward.
- **Up:** Z Up.
- **Use Space Transform:** Unchecked (do NOT also check Apply Transform — it corrupts rigs).
- In Unity import settings: enable **Bake Axis Conversion** on the Model tab.
- **Smoothing:** Normals Only (see E3).
- **Tangent Space:** ON (required for normal maps if ever added; harmless otherwise).
- Before export, ALWAYS apply: **Ctrl+A > All Transforms** (Location + Rotation + Scale all at (0,0,0) and (1,1,1)).

### E5 — Low-Poly Weapon Blockout Workflow (Topology + Triangle Counts)
**Sources:**
- Sponsor-provided ChatGPT PDF guide (LowPoly_Survival_Game_Blender_Guide.pdf). Moderate (generalist summary, not a primary source).
- Grant Abbitt, "Crafting a Low-Poly Axe in Blender," grantabbitt.substack.com/p/crafting-a-low-poly-axe-in-blender. Widely-watched tutorial creator, practical workflow. **Moderate.**
- Game-industry consensus (Polycount, Blender Artists, multiple udemy course descriptions). **Moderate collectively.**
**What it says:** Low-poly hard-surface weapon workflow:
- Start with a **plane** (for flat objects like axe heads, blades) or a **cube** (for hafts/grips). Avoid starting with a cylinder for weapon heads — adds unnecessary loops.
- Use **Mirror Modifier (X or Z axis)** with Clipping enabled for symmetric heads.
- **Silhouette first**: block major shape with minimal geometry using Knife Tool (K) or Loop Cuts (Ctrl+R) + Extrude (E). Do NOT add edge loops to define detail yet.
- For haft/handle: **5-6 sided cylinder** (low-poly look; 8 sides gets too round). Scale and Loop Cut to add hand-wrap indent sections.
- For blade taper: slide loop cut toward blade center (GG — edge slide) to push geometry toward the cutting edge. This creates the taper without adding triangles.
- **Chunky cartoon look requires asymmetry and intentional slight bends**: slightly bent hafts, off-center axe head, notches. These make it read as hand-made vs. machined.
- The white "edge-highlight plane" on the inspiration board (references `21h06_54`, `21h08_08`) is a **physical thin polygon** — a narrow inset or separate flat plane extruded from the blade face and painted white on the palette. It is NOT a shader effect. Add it as a separate mesh island UV-mapped to the white palette color.
- Target triangle counts for Far Horizon style: Axe 200–400, Knife 80–200, Sword 200–500, Spear 150–300. Well below GPU concern threshold.

### E6 — Palette Texture UV Workflow in Blender (Practical)
**Sources:**
- ContinueBreak, "Texturing the Vehicle Model in Blender (Part 3) Low Poly for UE4," continuebreak.com/articles/texturing-low-poly-vehicle-model-ue4-part-3/. Step-by-step documented project. **Moderate.**
- WintermuteDigital, "Texturing Low Poly Art with Colour Palettes in Blender," wintermutedigital.com/post/lowpoly-colour-palettes/. (Connection refused at time of research, but content mirrored at BlenderNation). **Moderate.**
- BlenderNation, "Texturing Low Poly Art with Color Palettes," blendernation.com/2020/05/21/texturing-low-poly-art-with-color-palettes/. **Moderate.**
**What it says:** Workflow:
1. Create a small image (64×64 or 128×128 pixels) in Blender: Image Editor > New Image > set resolution, fill with desired colour blocks. Use Blender's built-in Color Picker to place exact palette colours in discrete regions (8×8 or 16×16 pixel blocks is enough for per-face colour assignment).
2. In Shader Editor on every mesh: add **Image Texture** node → connect Colour output to Principled BSDF Base Colour input (or to a URP Unlit equivalent in Unity). Assign the palette PNG.
3. In UV Editor: after unwrapping, **Scale all UV islands to near-zero** (S > 0.001 > Enter). Move each island to sit over its target colour block (G + move). Use the UV grid overlay to verify placement.
4. For faces requiring a different colour, select those faces in Edit Mode, go to UV Editor, and move just those UV islands to the appropriate block.
5. Export: File > Export > FBX. Include the palette PNG alongside the FBX (or embed; embedding is unreliable for game engine import — keep separate).
6. In Unity: import FBX, create a material using URP Unlit shader, assign the palette PNG as the Base Map. Drag the material onto every weapon mesh. The SRP Batcher now batches all of them.

### E7 — Pivot / Origin Placement for HeldTool Rig
**Sources:**
- Unity Discussions, "Weapon Bones / Extra Bones With Humanoid Avatar Rig," discussions.unity.com/t/weapon-bones-extra-bones-with-humanoid-avatar-rig/1677538. **Moderate.**
- Unity Blog, "Advanced Animation Rigging: Character and Props Interaction," blog.unity.com/technology/advanced-animation-rigging-character-and-props-interaction. **Strong** (official Unity source).
- General Blender+Unity convention (Polycount, multiple tutorials). **Moderate collectively.**
**What it says:** For a held weapon prop the pivot must be at the **grip point** — the position in 3D space that coincides with the character's hand bone / attachment socket when held. For a hand axe or knife: origin = center of grip (top of handle), with the weapon pointing along the **+Z axis** in Blender (blade up), which becomes +Y in Unity after axis conversion. To set in Blender: place 3D cursor at grip midpoint (Shift+RClick or Shift+S > Cursor to Selected on a vertex), then Object > Set Origin > Origin to 3D Cursor. A common mistake is leaving origin at the world origin (0,0,0) — this causes the weapon to "orbit" around its base when the player hand rotates. The HeldAxe/HeldTool rig in Far Horizon's `HeldAxeRig.cs` anchors by parenting the weapon to a hand-socket bone; the socket bone's Z-forward is the barrel direction. Standardizing origin placement across all weapons makes them hot-swappable in the generalized `HeldTool` rig without per-weapon offsets.

### E8 — Blender MCP Capabilities and Limits
**Sources:**
- Ahuja Sid, "blender-mcp," github.com/ahujasid/blender-mcp. Open-source MCP server for Blender. **Strong** (primary implementation).
- StraySpark Studio, "Blender MCP: AI-Assisted 3D Modeling Step-by-Step (2026)," strayspark.studio/blog/blender-mcp-ai-assisted-3d-modeling-step-by-step-2026. Practical guide. **Moderate.**
**What it says:** Blender MCP exposes Blender's Python API via `execute_blender_code` (run arbitrary `bpy` Python), plus convenience tools for create-primitive, set-material, get-scene-info. Confirmed capabilities for this pipeline: creating mesh primitives, applying modifiers (Mirror, Decimate), setting material/UV data via Python, batch renaming, setting origins, applying transforms, running FBX export via `bpy.ops.export_scene.fbx`. **Critical limitation:** "LLMs cannot produce production-quality topology or handle edge flow, quad density, deformation-friendly geometry" (StraySpark source). Blender MCP is best used for: setup tasks (scene units, collections, base primitive creation), batch operations (rename all, apply all transforms, set all materials to palette), and export automation. The actual shape-design (blade profile, haft silhouette, edge loop placement, Mark Sharp assignments) requires the human artist or Devon iterating in the Blender viewport. MCP-driven `execute_blender_code` can script the palette UV assignment step (scale all UV islands to 0.001, translate each island to its palette block) after the human has defined the UV islands — this is the highest-value MCP automation in this pipeline.

### E9 — Cohesive Style-System Discipline (Referenced Set Products)
**Sources:**
- RetroStyle Games, "Low Poly Game Art: An Ultimate Guide," retrostylegames.com/blog/low-poly-game-art-an-ultimate-guide/. **Moderate.**
- Low Poly Ultimate Pack (Broken Vector, itch.io/brokenvector/ultimate-low-poly-survival). Commercial reference for how shipped survival game prop sets are structured. **Moderate.**
- Art direction board (`inspiration/` PNGs `21h06_54`, `21h07_20`, `21h07_42`, `21h08_08`). **Strong for this project** (Sponsor-set ground truth).
**What it says:** Cohesion across a weapon/tool set requires deciding one parameter early and holding it: the texturing model. The inspiration board references show a consistent system: **flat-shaded heads/blades with white edge-highlight planes, wooden hafts in a single warm-brown, segmented grip wrapping, mild asymmetry**. Per the art direction doc, the current shipped axe (Sketchfab CC-BY with baked texture atlas) is an outlier — it "imports its own baked lighting" and reads as a foreign object against a flat-shaded world. The palette texture approach (E1/E6) is what locks the family together: every weapon is coloured from the same ~20-colour palette; changing one palette colour shifts the whole set consistently (useful for difficulty-tier colour-coding or environment variants).

---

## Application to Far Horizon

### Recommended End-to-End Workflow

**Phase 0 — Shared Setup (once, before modelling any weapon)**
1. Create `WeaponPack.blend` in `Assets/Art/Props/WeaponPack/`.
2. Scene Units: Metric, Unit Scale 1.0, character reference = 1.8m. Create collections: `Blockout`, `LowPoly`, `Export`.
3. Create the **palette texture**: 128×128 PNG, white background, fill ~20 named colour blocks (see palette spec below). Save as `weapon_palette.png` in the same folder. Keep this texture in Blender as a data block.
4. Create one **Blender material** named `WeaponPalette`: Image Texture node → palette PNG → Principled BSDF Base Colour. Set Roughness = 1.0, Metallic = 0.0, Specular = 0.0 (flat look; no reflections).
5. In Unity: create one URP material named `Mat_WeaponPalette` using **Universal Render Pipeline/Unlit** shader (SRP Batcher compatible, no lighting calculation). Assign `weapon_palette.png` as Base Map. This is the ONLY material the entire weapon set uses.

**Phase 1 — Per-Weapon Modelling (repeat for axe, knife, sword, spear)**

Axe:
- Add Plane → rotate 90° on X → Mirror Modifier (X axis, Clipping ON).
- Knife Tool (K): cut axe head profile. Delete outer faces. Second Mirror (Y) for thickness.
- Extrude outer boundary edges on Y axis for blade taper; edge-slide (GG) cutting edge inward.
- Add 5-sided Cylinder for haft → add 3–4 loop cuts → use Extrude Along Normals for grip wrap bands.
- Separate thin inset face from blade front for the **white highlight plane** (I = Inset, then separate mesh island → UV to white colour block).
- In Mesh Properties > Normals: right-click mesh → Shade Smooth. In Edit Mode > Edge Select: Mark Sharp on all silhouette edges (head outline, haft corners, grip bands). Every edge that should be a hard visual break = Mark Sharp.
- Target: ~300 triangles total.

Knife:
- Add Cube → scale flat on Z → Mirror (X). Loop Cut for blade taper. Separate cylinder handle (4-sided low-poly).
- Blade edge highlight: Inset blade face, select thin inset strip → UV to white block.
- Target: ~150 triangles.

Sword:
- Cube for blade body → scale thin on Y, tall on Z. Loop Cut at crossguard and grip. Mirror (X) for crossguard width.
- Handle: cylinder (6 sides) → loop cuts for pommel and grip sections.
- Target: ~350 triangles.

Spear:
- Cylinder (5 sides) for shaft → extend tall. Separate cone-tip primitive (5 sides) at top.
- Mark Sharp on shaft-to-tip transition ring.
- Target: ~200 triangles.

**Phase 2 — UV Colour Assignment (all weapons)**
For each weapon:
1. In Edit Mode, select all faces (A).
2. UV Editor: U > Smart UV Project (angle limit 66°, island margin 0.001). This unwraps; but we don't care about shape — only placement.
3. Select all UV islands, S > 0.001 (scale to near-zero). Move to the base colour for that weapon part: axe-head UV → red block, haft UV → brown block, edge highlight UV → white block, grip wrap UV → dark-brown block.
4. For spear shaft: move UV to a warm tan block; tip to stone-grey block.
5. Repeat for all weapons. Each weapon uses a SUBSET of the shared palette, so colours are consistent across the set (same red for all metal heads, same brown for all wood hafts).

MCP automation opportunity: once UV islands are manually placed for the first weapon, a `run_python` / `execute_blender_code` script can batch-apply the UV island positioning to all subsequent weapons using stored UV coordinates.

**Phase 3 — Origin Placement**
For each weapon:
1. In Edit Mode, select vertices defining the grip midpoint.
2. Shift+S > Cursor to Selected.
3. Object Mode > Right-click > Set Origin > Origin to 3D Cursor.
4. Verify: weapon should point blade-end along +Z in Blender. This becomes +Y in Unity after axis conversion. The HeldTool rig in Unity parents each weapon to a hand socket bone whose +Y = blade-forward.

**Phase 4 — Pre-Export Checklist**
- Ctrl+A > Apply All Transforms on each weapon object.
- Verify scale = (1,1,1), rotation = (0,0,0), location = (0,0,0) before export. If origin is at grip (not world 0,0,0), the OBJECT origin is at grip but the object DATA may be offset — this is correct; do not apply origin as transform.
- Shift+N (Recalculate Normals) → check normals are outward-facing (Overlay > Face Orientation: all blue).
- Confirm material slot = WeaponPalette for every mesh object.
- Confirm no loose geometry (M > Merge by Distance, threshold 0.001).

**Phase 5 — FBX Export**
File > Export > FBX (.fbx):
- Selected Objects: ON (export one weapon at a time, or all in one FBX — Unity accepts both; separate files preferred for per-asset import control).
- Apply Scalings: **FBX Unit Scale**.
- Forward: **-Y Forward**.
- Up: **Z Up**.
- Use Space Transform: **UNCHECKED**.
- Apply Transform: **UNCHECKED** (this is the deprecated option; Use Space Transform replaces it).
- Smoothing: **Normals Only** (exports the Mark Sharp-derived normals; Unity reads them correctly).
- Tangent Space: **ON**.
- Materials: export embedded or reference external PNG — recommend reference external (copy palette PNG alongside FBX).
- Armature: not needed for static weapon meshes.

Save FBX files to `Assets/Art/Props/WeaponPack/`: `wpn_axe_01.fbx`, `wpn_knife_01.fbx`, `wpn_sword_01.fbx`, `wpn_spear_01.fbx`.

**Phase 6 — Unity Import**
- Unity auto-imports FBX on file drop.
- Model tab: Normals = **Import**, Tangents = Import, Optimize Mesh = ON (verify normals survive after optimize — check in Scene view).
- Bake Axis Conversion = **ON** (fixes the Blender Z-up vs Unity Y-up difference without requiring the X-rotation hack).
- Materials tab: Material Creation Mode = **None** (we are assigning `Mat_WeaponPalette` manually; don't let Unity auto-create material stubs).
- Assign `Mat_WeaponPalette` (URP/Unlit + palette PNG) to the imported mesh via the MeshRenderer component.
- Verify Frame Debugger: all weapons in scene should batch into one SetPass call under GPU Resident Drawer (same shader variant + same texture).
- Add a Box Collider (primitive) per weapon. No MeshCollider — too expensive and unnecessary for simple hand tools.

---

## Palette Colour Spec (Starter — Devon/Uma refine)

The following 12 colours cover the full weapon set. A 128×128 texture divided into 16×8 blocks gives 16+ colour slots; the palette can be extended without breaking existing UV assignments.

| Slot | Name | Suggested hex | Usage |
|---|---|---|---|
| A1 | WoodBrown | #8B5E3C | Axe/knife/spear haft |
| A2 | WoodDark | #6B4423 | Grip wrap shadow band |
| A3 | MetalRed | #C0392B | Axe head (primary) |
| A4 | MetalGrey | #7F8C8D | Sword blade, spear tip, stone |
| A5 | EdgeWhite | #F5F5F0 | Blade edge highlight plane (all weapons) |
| A6 | GripLeather | #5C3317 | Sword crossguard + handle wrap |
| A7 | SpearShaft | #D4A96A | Spear shaft warm tan |
| A8 | StoneGrey | #95A5A6 | Spear tip |
| A9 | WarmAccent | #E8B84B | Decorative binding/grip ring |
| A10 | KnifeBlade | #BDC3C7 | Knife blade |
| A11 | ShadowFace | #4A3728 | Underside/shadow face for blades |
| A12 | Black | #1A1A1A | Outlines on edge geometry if used |

Build the palette texture in Blender: Image Editor > New (128×128) > fill each 16-wide strip with the target colour using the paint bucket tool or Blender's Python: `bpy.ops.image.new(name="weapon_palette", width=128, height=128)` + fill via `pixels` array.

---

## Blender MCP — Automation Targets for This Pipeline

Blender MCP can be driven by Claude via `execute_blender_code` to automate the mechanical parts:

| Task | MCP-automatable? | Notes |
|---|---|---|
| Create palette PNG with hex colours | YES — `bpy` pixels array | One-time setup script |
| Create base material with Image Texture node | YES — `bpy.data.materials.new` + node API | Set up once, applied to all |
| Batch apply transforms (Ctrl+A) | YES — `bpy.ops.object.transform_apply` | Pre-export step |
| Recalculate normals | YES — `bpy.ops.mesh.normals_make_consistent` | Pre-export |
| Scale UV islands to near-zero + move to palette block | YES — UV layer manipulation via `bpy` API | High-value script; saves repetitive UV work |
| Set origins from grip-vertex selection | PARTIAL — vertex selection is manual; origin set is scriptable | Human selects vertices; MCP runs Set Origin |
| FBX export with all correct settings | YES — `bpy.ops.export_scene.fbx(...)` with all args | One command; parameterise per-weapon |
| Modelling (blade profile, edge loops, Mirror Modifier) | PARTIAL — primitives + modifiers scriptable; creative edge placement is human work | Human does blockout; MCP can apply Mirror, Decimate, batch-rename |
| Mark Sharp on edges | PARTIAL — can mark all edges sharp globally; selective per-edge requires human selection | `bpy.ops.mesh.mark_sharp()` on selected edges; human pre-selects |

---

## Style-System Rules — Devon Checklist Per Asset

Before calling any weapon "done," verify these against the inspiration board (`21h06_54`, `21h07_20`, `21h07_42`, `21h08_08`):

1. **No smooth curved surfaces.** All faces are planar and visually faceted. No subdivisions, no Sub-D modifiers.
2. **Hafts have a slight intentional bend.** Not machine-straight. 2–5 degrees of curvature via vertex movement.
3. **White edge-highlight plane exists on every blade.** Narrow inset (I key, 0.05m) on the cutting face, UV-mapped to `A5 EdgeWhite`. This is what makes the inspiration-board tools read "crisp."
4. **Same proportional language across the set.** Hafts are "chunky" — diameter ~0.08m at grip vs 1.8m character height. Refer to the blockout character-reference cube.
5. **All faces use palette colours only.** No per-face vertex colour painting (conflicts with palette UV approach). No per-asset textures.
6. **All mesh normals outward-facing** (Overlay > Face Orientation: all blue, no red).
7. **No internal geometry** (loose verts, zero-area faces, overlapping faces). M > Merge by Distance before export.
8. **Origin at grip midpoint, weapon pointing +Z in Blender.**
9. **UVs on palette blocks, not stretched across any surface.** All islands scaled to ~0.001 in UV space.
10. **One material slot only** per weapon: `WeaponPalette`.

---

## Open Questions for Sponsor / Priya

1. **Spear tip material:** is the spear tip bone/stone/iron? This affects which palette block to use and the topology of the tip (angular facets for stone vs smooth taper for iron). The inspiration board shows an axe, pickaxe, sword, and curved blade but no spear — need a reference or style call before Devon models it.
2. **White edge-highlight plane approach:** the inspiration board shows it prominently on the axe and pickaxe heads. Should the sword blade also have it running along the full blade length? Or just at the tip? Uma should sign off on which weapons get the highlight-plane treatment in the style spec before Devon models.
3. **Blender MCP version:** the project drives Blender via Blender MCP. The official Blender MCP server (blender.org/lab/mcp-server/) returned 403 at time of research. Devon should verify whether the project is using the `ahujasid/blender-mcp` community server or the official one — the available tool set differs slightly between them.

---

## Evidence-Strength Summary

| Finding | Source type | Strength |
|---|---|---|
| Palette texture = best shared-material approach | Primary tool doc (Imphenzia) + multiple tutorials | Strong |
| SRP Batcher batches by shader variant | Official Unity 6 Manual | Strong |
| Mark Sharp + Shade Smooth in Blender 4.1+ | Official Blender Manual + community | Strong |
| FBX export settings (-Y Forward, Z Up, Normals Only) | Devlog + addon README + community consensus | Moderate |
| Weapon blockout topology | Tutorial (Grant Abbitt) + ChatGPT PDF guide + Polycount | Moderate |
| Palette UV workflow | ContinueBreak devlog + BlenderNation | Moderate |
| Origin at grip, +Z up | Unity Blog + discussions | Moderate |
| Blender MCP capabilities | GitHub README + StraySpark 2026 guide | Moderate |
| White-highlight-plane as physical geometry | Inferred from inspiration board inspection; no independent source | Weak (project-specific deduction) |

