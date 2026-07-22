# Castaway v4 — chamfered-blocky toy (look-dev PASSED 2026-07-18)

Hand-modeled in Blender via Blender MCP (Fable, Sponsor-directed session 2026-07-18 — explicit
exception to the fable-advisor-only policy). NOT the Rodin route: this is a deliberate style
experiment the Sponsor approved at look-dev after the blockout gate + final judge set.
**v3 stays the LIVE hero** until v4 passes rig + in-game soak (staged-toggle rollout per
`character-pipeline.md` §Rolling out a NEW character version).

## Files
- `castaway_v4.blend` — source. One collection `CastawayV4`, 40 segmented parts, **1,760 tris**.
  No camera/lights/reference meshes (judge helpers purged per pipeline doc §10).
- `castaway_v4_palette.png` — single 128×128 texture: flat color blocks (left half, 32×16 each)
  + painted 64×64 face patch at x[64,128) y[32,96) (brows/eyes/smile/stubble). Written
  sRGB-passthrough (byte image, hex as-is — do NOT pre-linearize on edits).
- `castaway-v4-renders/judge_*.png` — the Sponsor-judged set (front/side/back/¾/face/gameplay).
  `blockout_*.png` / `polish_*.png` are the earlier gate rounds.

## Spec (as grilled + approved)
Same identity as v3 (friendly castaway: teal torn shirt, brown rolled pants, rope belt, barefoot,
stubble), restyled: chamfered-blocky "wooden toy", segmented parts (rigid blocks per limb segment,
caps hidden in joints), ~3.9 heads tall, **height 1.9000 m = measured v3 raw-mesh height**
(`castaway-v3-rodin-export-lowpoly/base.fbx`), A-pose 45° (Mixamo-ready), mitten+thumb hands,
painted face + geometry nose, 3-tooth jagged hair fringe.

## Conventions inside the .blend
- Front = **-Y**, Z-up, metric 1.0. Feet soles at z=0, hair top at z=1.90.
- One material `CastawayV4Palette` (Principled, Roughness 1, Closest interpolation) on every part.
- UVs: all faces parked on palette color dots EXCEPT the head front quad, planar-mapped to the
  face patch (u [0.512,0.988], v [0.262,0.738]).
- Part naming: `head/neck/nose/ear_*/hair_*` (skin/hair), `torso/sleeve_*/shoulder_*` (teal),
  `pelvis/leg_upper_*/pants_lower_*` (pants), `cuff_*` (pants_dark), `shin_*/foot_*/arm_*/elbow_*/
  hand_*/thumb_*` (skin), `belt/knot/dangle_*` (rope).
- Shading: all polys smooth + ALL edges marked sharp (fully faceted; chamfers read as facets).
  Export with FBX `Smoothing = Normals Only` per `blender-asset-pipeline.md` §8.

## Handoff — next steps on approval (NOT done in the look-dev session)
1. Export A-pose FBX (§8 settings: -Y Forward / Z Up / FBX Unit Scale / Apply Transform OFF).
   Consider `Join`-ing the 40 parts into one mesh first for Mixamo upload (segmented islands in
   ONE object rig fine; 40 separate objects may not).
2. Mixamo web auto-rig (manual step): markers chin/wrists/elbows/knees/groin, Standard Skeleton.
   Verify root `mixamorig:Hips`, 0 unweighted verts (`character-pipeline.md` §3).
3. Unity: Generic rig (NEVER Humanoid — cone-explosion trap), default-OFF env toggle alongside
   v2/v3, capture-gate reconciliation + held-prop re-seat at activation (§Rolling out).
4. Held-tool seating on the mitten needs a fresh bone-axis measure (`procedural-animation-verbs.md`).
