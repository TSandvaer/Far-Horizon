# Castaway v3 — Rodin Smart-Low-poly hero (source-of-truth export)

Provenance for the Sponsor-locked **castaway v3** hero (ticket `86cak41d4`). Same asset-creation
route as v1/v2 (`.claude/docs/character-pipeline.md`): concept → Hyper3D Rodin → Mixamo → Unity
**GENERIC** rig. This folder is the SOURCE-OF-TRUTH export; the integration-consumed subset (rigged
mesh + posterized diffuse) is committed under `Assets/Art/Character/Castaway/v3/`.

## Identity
Hopeful, young-ish **mid-30s adventurer** castaway — torn teal shirt, warm palette. Distinct from the
v2 bearded-rugged base. v3 is the Sponsor-locked NEXT hero, integrated **DORMANT** here: v2 stays the
LIVE default; v3 activates only under the `FARHORIZON_CASTAWAY_V3=1` env toggle (default OFF) until a
separate activation/soak PR flips the default.

## Concept
- `../castaway_v3_concept_apose.png` — the A-pose concept reference fed to Rodin (arms ~45° down-and-out,
  clean torso gaps; gear stripped, empty open hands, faceted-low-poly render style prompted — the
  concept-prep checklist in `character-pipeline.md` §1).
- `../castaway_v3_lowpoly.blend` — the low-poly Blender working source.

## Rodin (hyper3d.ai, 3D tab, Image-to-3D)
- **Route: Smart-Low-poly (Quad / Low density)** — the low-poly conversion route (see Erik's route
  research `team/erik-consult/lowpoly-hero-conversion-research.md`, cited in the PR body). Result:
  **7.4k tris of real facet geometry** (genuine hard-edge low-poly, not a decimated high-poly).
- Symmetric ON; De-light ON (flat albedo for the URP toon/unlit look); Skip Face Restore.
- **Diffuse posterization:** `texture_diffuse_posterized.png` is the de-lit diffuse run through an
  **HSV posterize** (banded flat diffuse) — the v3 style albedo. `texture_diffuse.png` is the raw
  Rodin de-lit diffuse (kept for reference). v3's Unity material binds the POSTERIZED PNG.

### Export contents
| File | Role |
|---|---|
| `base.fbx` | Rodin base mesh (un-rigged) |
| `base_basic_pbr.fbx` / `base_basic_shaded.fbx` | shaded/PBR preview variants |
| `castaway_v3_mixamo_upload.fbx` | the upload sent to Mixamo for auto-rig |
| `texture_diffuse_posterized.png` | **the v3 style albedo (Unity `_BaseMap`)** |
| `texture_diffuse.png` | raw Rodin de-lit diffuse (reference) |
| `texture_normal/metallic/roughness/pbr.png` | PBR maps — **UNUSED** by v3 (no normal/metallic/roughness) |
| `shaded.png` | Rodin preview render |
| `mixamo/` | the Mixamo-rigged clips (see below) |

## Mixamo (mixamo.com, free Adobe account)
- Auto-rig: **Standard Skeleton (65)**. The rig came back **41 bones incl. 16 finger bones**, root
  `mixamorig:Hips`, all verts weighted (a valid Standard result — bone count varies with source finger
  geometry; `character-pipeline.md` §3).
- Clips applied + downloaded to `mixamo/`:
  - `Idle.fbx` — **WITH skin** (mesh + rig + Idle take). This is the v3 base FBX consumed by Unity
    (committed as `Assets/Art/Character/Castaway/v3/castaway_v3_rigged.fbx`).
  - `Walking.fbx` / `Running.fbx` / `Breathing Idle.fbx` — **WITHOUT skin** (clip only; bind by
    `mixamorig:*` transform path onto the v3 mesh — same idiom v2 uses).

### FBX-7700 stray-node caveat (handle on import)
`mixamo/Idle.fbx` carries a **stray empty `Armature` node** from the FBX-7700 export. It is harmless:
a Generic + `CreateFromThisModel` import builds the avatar from the `mixamorig:*` skeleton and the empty
node becomes an inert transform. `ConfigureV3BaseFbx` imports the mesh+rig only (`importAnimation=false`)
and asserts a VALID avatar, so the stray node cannot silently break clip binding.

## Unity integration handoff
- Rig = **GENERIC**, `CreateFromThisModel` — NOT Humanoid (`86ca8rdkp`: Humanoid muscle-space retarget
  cone-explodes the skinned mesh under a scaled hierarchy).
- The existing 18 WITHOUT-skin clips (BreathingIdle/Walk/Run/Jump*/Melee/Crouch*/hit-reacts/…) bind onto
  the v3 mesh by transform path (shared `mixamorig:*` names) — no retarget, no v3-specific clip import.
- Material: **URP/Unlit** with `texture_diffuse_posterized.png` as Base Map; NO normal/metallic/roughness
  (the weapon-family material contract, `86cak3r3k`). A URP/Lit-no-normal variant is available behind
  `FARHORIZON_CASTAWAY_V3_LIT=1` for the soak A/B (the Sponsor rules at soak).
- Held-weapon re-seat on the v3 hand is a SEPARATE activation follow-up (PR #254 weapon surfaces frozen).
