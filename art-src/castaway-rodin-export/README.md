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
   hands; the 41 bones are a SUBSET of the clip skeleton, so under the GENERIC rig the existing
   clips bind by TRANSFORM PATH — matching mixamorig bone names — with no retarget; the missing
   middle/ring finger curves simply have no target and are ignored), skinned mesh 7,658 verts /
   34 vgroups, FBX 7700, imports at 1.889 m (Unity import height-normalizes to 1.0 u via
   `CharacterAssetGen.TargetImportHeightU`).

## Committed subset (harvest PR 2026-07-05)

Committed to git: this README, `base.fbx` (geometry-only), `base_basic_shaded.fbx` (Mixamo
input / color preview), `castaway_rigged_tpose.fbx` (the rigged deliverable),
`texture_diffuse.png` + `texture_normal.png` (what integration actually consumes — the project
renders URP with de-lit albedo, same as the current castaway). NOT committed (dead weight for a
URP-Unlit-style project, re-downloadable / in the local export folder if ever needed):
`base_basic_pbr.fbx`, `texture_metallic.png`, `texture_roughness.png`, `texture_pbr.png`,
`shaded.png` (preview bake).

## Integration handoff (DONE — ticket 86cajwp23, on PR #260)

- Unity import as **GENERIC + transform-path** (`CreateFromThisModel` on the rigged FBX),
  height-normalize to 1.0 u. **NOT Humanoid, NOT `CopyFromOther`** — the Mixamo Humanoid
  muscle-space retarget CONE-EXPLODES the skinned mesh at runtime under the scaled scene
  hierarchy (ticket `86ca8rdkp`; live anti-Humanoid gate). Generic binds each of the existing
  18 clips by transform path onto v2's mesh (v2's 41 bones are a subset of the clip skeleton —
  only middle/ring fingers missing), so NO retarget is needed. Implemented in
  `CharacterAssetGen.ConfigureV2BaseFbx` (Generic, `CreateFromThisModel`, height-normalize).
- `HeldAxeRig` bone-axis re-measure: `CharacterAssetGen.CastawayV2HandAxisTrace` dumps v2's
  `mixamorig:RightHand` local frame; seat prior in `MovementCameraScene.HeldAxeV2RelEuler` /
  `HeldAxeV2LocalOffsetFromHand` (soak-dialable via F9).
- `CharacterAssetGen.RecolorShirtToTan` gated OFF for v2 (no shirt on the new base).
- `Castaway_Attribution.txt` updated (Generic pipeline + a v2 section).
- Old castaway stays live behind the `CharacterAssetGen.UseCastawayV2` toggle
  (env `FARHORIZON_CASTAWAY_V2=1` at bootstrap) until v2 passes the Sponsor soak in a shipped
  build, then v2 is promoted to the default + v1 removed (staged, Sponsor-locked plan).
- **Gear modules (in-house, Blender):** chest strap / wristbands / future wear as separate
  palette meshes skinned to the same skeleton — deliberately NOT baked into the Rodin mesh.
  Modular-parts history and the v1–v7 in-house build (kept for reference + palette/module
  patterns): `art-src/castaway_character.blend`, `art-src/char_palette.png`.

## Customization goal (Sponsor vision)

Player-adjustable look later: color swaps (palette rows) + swappable gear meshes. The Rodin base
is welded (hair/beard included); v1 customization = gear modules + texture recolors. Deeper
modularity (hair/beard swaps) = phase 2, carving the welded mesh by texture region if needed.
