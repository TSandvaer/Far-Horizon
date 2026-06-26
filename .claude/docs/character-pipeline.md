# 3D Character Pipeline — Hyper3D Rodin → Mixamo → Unity

**Status:** Gen + auto-rig steps **validated 2026-06-15** (a chunky-low-poly castaway generated, rigged, and cleanly animating Idle+Walk in Mixamo's preview). In-engine/URP adoption verification is the open step (ticket `86ca8r72j`). The *recipe* below is reusable regardless of whether any single generated asset is adopted — it applies to future characters and props too.

This is the third asset-creation route for Far Horizon, alongside **procedural** (`LowPolyMeshes`/`FacetedRock`) and **Blender/Blender-MCP sourcing**. Reach for it when you want a generated, on-style chunky low-poly **character** (the procedural route fights organic humanoid shapes; see `[[unity-conventions]]` §Asset creation "source when procedural fights the style").

## Route at a glance
openai-image concept → **Hyper3D Rodin** Image-to-3D (mesh) → **Mixamo** auto-rig (Hyper3D output is UN-rigged) → **Unity Generic rig** (NOT Humanoid — see Step 4).

## Step 1 — Concept reference (openai-image)
- **Image-to-3D ≫ Text-to-3D** for a specific look. Generate a full-body character, plain solid background, even lighting, single centered subject (`mcp__openai-image__text-to-image`, portrait `1024x1536`, quality `high`). Iterate identity with `image-to-image` off a chosen base to keep the character consistent.
- ⚠️ **POSE IS DRIVEN BY THE REFERENCE IMAGE, not the prompt.** In Rodin Image-to-3D the mesh copies the reference's pose. Rodin's **"T/A Pose" button only inserts the text "T/A Pose" into the prompt — it does NOT re-pose the mesh.** An arms-down (I-pose) reference → arms-down mesh, which is the *hard* case for auto-rig (shoulder/armpit weights pinch). To get a riggable mesh, **supply an A-pose reference** (arms ~45° down-and-out, clear gaps from torso, feet shoulder-width) — re-pose the concept via `image-to-image` ("keep the character identical, change ONLY the pose to a clean symmetric A-pose…"). A/T-pose costs nothing extra here and saves a wasted generation later.

## Step 2 — Hyper3D Rodin (web UI, hyper3d.ai, Creator tier)
- Use the **3D** tab. **NOT the Avatar tab** — that is ChatAvatar = animatable *faces* only + realistic → dead end for a stylized full body (verified).
- **Image to 3D** → upload the A-pose reference.
- Model **Gen-2.5**. **Turbo** is the cheap iterate mode (protect credits — Creator tier is limited); **Gen-2.5 / High** for the final. A clean reference often nails it in one Gen-2.5 pass.
- Geometry presets: **Symmetric** ON for a humanoid; leave Detailed/Soft default.
- On **Confirm**, the topology picker offers Smart-Low-poly(BETA) / Triangle / **Quad Mesh**:
  - Choose **Quad Mesh** — **quads deform far better than triangles when rigged.** Density **~8000** for a chunky character (keeps face + separated fingers, rigs clean, light in Unity); 4000 = chunkier/leaner. Keep **Baked Normal** on.
  - It then upsells "Confirm via Triangular for more details" → click **Keep Quads** (Triangular = a sharp tri mesh up to **1M** polys = far too heavy + worse deformation).
  - "Is the generated object symmetrical?" → **Yes** for a humanoid (cleaner mirrored topology/weights).
- **Material:** **De-light ON** — strips baked shadows so the albedo is flat and lights correctly in-engine (fits the URP toon look). **Skip Face Restore** (pushes the toon face toward realism). HD/4K unnecessary — 2K is plenty.
- **Pack/Export:** **Base Model** (not LOD/High-poly), format **.fbx**, **Shaded + PBR**, **2K** → **Download** (not "Send", which is the Blender-send path). Export is a folder/zip: `base*.fbx` + `texture_diffuse|normal|roughness|metallic|pbr.png`.
- Alternative driver: the **Blender MCP** has Hyper3D Rodin tools (`mcp__blender__generate_hyper3d_model_via_images`/`_text`, `poll_rodin_job_status`, `import_generated_asset`) — but they need the **Blender addon connected** (`get_hyper3d_status` errors otherwise). The web UI is the no-setup route.

## Step 3 — Mixamo auto-rig (mixamo.com, free Adobe account)
Hyper3D output ships **un-rigged**; Mixamo adds the skeleton + skin weights + retargetable clips.
- **Upload Character** → the `.fbx` (use the `_basic_shaded` variant so the preview shows color). Auto-Rigger: drag markers onto **chin, both wrists, both elbows, both knees, groin**; **Use Symmetry** ON; **Skeleton LOD = Standard Skeleton (65)** (a clean, standard bone hierarchy — binds cleanly by transform path under the Generic rig; finger bones are harmless on mitten hands).
- Apply **Idle** + **Walking**; watch hips/knees/shoulders/elbows for pinch/collapse. **Mixamo previews ONE animation at a time** — applying a new clip replaces the on-screen one; it is NOT lost, each downloads separately.
- **Download split for Unity (Generic rig):** the character **With Skin** once (= mesh + rig + that clip) + each animation **Without Skin** (= skeleton+anim only, binds by transform path under Generic). Format **FBX for Unity(.fbx)**, **30 fps**, Keyframe Reduction **None**. Sanity check: with-skin FBX is multi-MB (carries the mesh); without-skin is ~hundreds of KB.

## Step 4 — Unity
- Import the FBX. **Rig = GENERIC, NOT Humanoid** (hard-won — adoption 2026-06-15, ticket `86ca8rdkp` / PR #47): the Mixamo **Humanoid** rig **EXPLODES the skinned mesh into a cone at runtime** when the character sits under a SCALED scene hierarchy (the muscle retarget fights the parent scale — body flung far off-spawn, hand bones at thousands of units). **Generic** (transform-path binding, no muscle retarget) renders clean at the player. With-skin FBX carries the mesh + skeleton; without-skin clips bind by transform path (no avatar-copy step needed for Generic). URP material with `texture_diffuse` as Base Map (de-lit albedo → flat/toon).
- ⚠ **A spike/capture camera that FRAMES or FOLLOWS the mesh bounds can HIDE a runtime displacement** that a fixed/anchored gameplay camera would expose: the viability spike's Humanoid captures looked clean ONLY because the capture camera tracked the displaced mesh. When validating a rig, capture from the ANCHORED gameplay camera (or a fixed world camera) — not one that re-frames to the mesh. (Sibling of the `smr.bounds` capture trap in `[[unity-conventions]]`.)
- Mixamo bones carry **lossyScale ~1** (e.g. `mixamorig:RightHand`) — NO 267×-lossy-bone trap (unlike the old Sketchfab chibi's `RightHand_010`); held props can pose hand-local without the world-offset workaround.
- **Judge from a real build** (project lore: editor previews lied on the procedural humanoid — the "legs-up" incident, `[[unity-conventions]]` §editor-vs-runtime). The shipped-exe look is what the Sponsor judges.

## Cost & artifacts
- **Hyper3D** Creator tier = limited credits (Turbo cheap, Gen-2.5/High more — iterate on Turbo). **Mixamo** is free. openai-image concepts don't touch Hyper3D credits — generate references freely.
- Artifact convention: `inspiration/hyper3d-castaway/` — `concepts/` (openai-image refs), the gen GUID subfolder (`base*.fbx` + `texture_*.png`), and the Mixamo `Idle.fbx`(with-skin)/`Walking.fbx`(without-skin). These are untracked spike inputs unless promoted into `Assets/`.
