# Elite techniques — reach-for-these references

The mandatory-read set already says "Read EVERY `.claude/docs/*.md`" — this file folds Erik's
2026-06-15 Unity-3D-mastery research (`team/erik-consult/unity-3d-mastery-path.md` +
`developer-accuracy-performance-research.md`, which agents do NOT auto-read) into the
guaranteed-read surface. **Pointers, not tutorials** — each entry says when to reach for the
technique + where the authoritative source lives. Read the linked source before implementing;
filing the technique here makes it discoverable, it does not replace the source.

> **Scope note:** this OPERATIONALIZES the knowledge (so every dispatch picks it up). The
> ADOPTION of each technique is its own backlog ticket (`86ca9a340` Diagnose-Before-Fix ·
> `86ca9a36g` PlayMode locomotion-sampling · `86ca9a39q` Configurable Enter Play Mode ·
> `86ca9a3b3` SRP-batcher gate, + the foot-IK / chunk-LOD / shader adopt-tickets). Don't
> implement here; reach for these when the matching pain shows up in a dispatched ticket.

## Grounding imported-rig avatars (the float saga's domain)

- **Ground the avatar ROOT to `terrainHit + K` (a FIXED constant K), NEVER a per-frame
  `BakeMesh` world-Y.** Mixamo / Hyper3D FBX bake a 100× cm→m node scale onto the
  `SkinnedMeshRenderer` transform that `localToWorldMatrix` then DOUBLE-applies — the source
  of the 8-attempt float/sink saga (fixed PR #47, `e1289ef`). Per-frame `BakeMesh`
  sole-chasing DIVERGES under that scale (a transient wrong-scale sole at walk-onset over-corrects
  the snap → the character sinks to `avatarRootY ≈ −68`); a stable terrain-raycast + a fixed
  offset does not.
- **For any world-space sole measurement, use a unit-scale matrix**
  (`Matrix4x4.TRS(pos, rot, Vector3.one)`) — measuring through the FBX's own
  `localToWorldMatrix` re-applies the 100× scale and gives garbage sole-Y.
- **The elite path for real foot-planting on uneven terrain is Animation Rigging Two-Bone IK**
  (`com.unity.animation.rigging`; Unity Learn "Working with Animation Rigging"). This is the
  durable upgrade beyond the fixed-offset snap — adopt it when terrain stops being near-flat.
  (Open Q for Devon: confirm `com.unity.animation.rigging` is in the package manifest before the
  next character PR.)
- **Cross-ref:** `unity-conventions.md` §FBX / rigs / characters (the ground-snap / sole-vs-root
  finding + the intrinsic-height-normalization rule live there).

## Procedural terrain at scale (the big-island + journey-out arc)

- **Sebastian Lague "Procedural Landmass Generation" chunk-LOD architecture** is the reference
  for the big-island redesign and the M-U5+ expansion (`github.com/SebLague/Procedural-Landmass-Generation`).
  Reach for it when the world stops fitting in one hand-built scene — chunked terrain with
  distance-based LOD is how the "BIG and ENDLESS" north-star scales without tanking frame time.

## URP stylized shaders (closes two recurring bug classes)

- **`ddx`/`ddy` fragment-normal flat-shading (Hextant Studios) kills the winding-inversion class
  by construction.** Computing the face normal in the fragment shader from screen-space
  derivatives means you never depend on per-vertex normals or triangle winding — the
  coincident-opposite-winding "black shards" class (unity-conventions §Low-poly mesh patterns)
  and the stale-material sky-drift class can't recur. Reach for it on any new flat-shaded surface.
- **keijiro `UnitySkyboxShaders`** — gradient skybox reference (the Zone-D look's sky).
- **Cyanilux** — depth / shoreline / water shader references (the shoreline-foam + waterline
  tuning the world-look pass repeatedly chased).
