# Castaway v2 — Rodin base (Sponsor-approved 2026-07-05)

New hero castaway replacing the current in-game base (`Assets/Art/Character/Castaway/`).
**Identity change (Sponsor decision 2026-07-05):** bearded rugged adult survivor — deliberately
reverses the earlier "young + happy" lock (Sponsor chose "Full reference look" against
`inspiration/2026-06-12_21h00_32.png`, then iterated the concept to a FRIENDLY-NEUTRAL expression).

## Provenance (every step Sponsor-approved in-session)

1. **Concept:** `art-src/castaway_concept_apose.png` — openai-image (gpt-image-1) image-to-image
   chain from the inspiration reference: A-pose, empty hands, no spear, no blue cloth, no chest
   strap, no wristbands (gear becomes separate in-house modules), faceted low-poly render style,
   friendly-neutral expression.
2. **Rodin:** hyper3d.ai web UI (Creator tier, manual upload by Sponsor — the MCP free-trial key
   endpoint is dead, returns 400 on any image). Gen-2.5 / High, **De-light ON**, PBR temp 7,
   topology **Quad Mesh ~8000**, Baked Normal, "Keep Quads". Export: Base Model, .fbx,
   Shaded + PBR, 2K → this folder (`base*.fbx` + `texture_*.png`, `shaded.png`).
3. **Mixamo auto-rig:** `castaway_rigged_tpose.fbx` — uploaded `base_basic_shaded.fbx`,
   markers chin/wrists/elbows/knees/groin, Symmetry ON, Standard Skeleton LOD.
   **Verified in Blender:** root `mixamorig:Hips`, 41 bones (no-finger variant — fine: fist
   hands; Humanoid retarget is muscle-space so existing clips carry), skinned mesh 7,658 verts /
   34 vgroups, FBX 7700, imports at 1.889 m (Unity import will height-normalize to 1.0 u like
   `CharacterAssetGen.TargetImportHeightU` does today).

## Committed subset (harvest PR 2026-07-05)

Committed to git: this README, `base.fbx` (geometry-only), `base_basic_shaded.fbx` (Mixamo
input / color preview), `castaway_rigged_tpose.fbx` (the rigged deliverable),
`texture_diffuse.png` + `texture_normal.png` (what integration actually consumes — the project
renders URP with de-lit albedo, same as the current castaway). NOT committed (dead weight for a
URP-Unlit-style project, re-downloadable / in the local export folder if ever needed):
`base_basic_pbr.fbx`, `texture_metallic.png`, `texture_roughness.png`, `texture_pbr.png`,
`shaded.png` (preview bake).

## Integration handoff (next steps — team ticket)

- Unity import as Humanoid (`CreateFromThisModel` on the rigged FBX), height-normalize to 1.0 u,
  retarget the existing 18 Mixamo clips via `CopyFromOther` (same recipe as current castaway).
- `HeldAxeRig` / `CastawayArmPose` bone-axis re-measure ritual (procedural-animation-verbs.md).
- Retire `CharacterAssetGen.RecolorShirtToTan` (no shirt on the new base).
- Update `Castaway_Attribution.txt` (same Hyper3D+Mixamo pipeline, new generation).
- Old castaway stays live until the new one passes the Sponsor soak in a shipped build
  (staged, soak-gated rollout — Sponsor-locked plan).
- **Gear modules (in-house, Blender):** chest strap / wristbands / future wear as separate
  palette meshes skinned to the same skeleton — deliberately NOT baked into the Rodin mesh.
  Modular-parts history and the v1–v7 in-house build (kept for reference + palette/module
  patterns): `art-src/castaway_character.blend`, `art-src/char_palette.png`.

## Customization goal (Sponsor vision)

Player-adjustable look later: color swaps (palette rows) + swappable gear meshes. The Rodin base
is welded (hair/beard included); v1 customization = gear modules + texture recolors. Deeper
modularity (hair/beard swaps) = phase 2, carving the welded mesh by texture region if needed.
