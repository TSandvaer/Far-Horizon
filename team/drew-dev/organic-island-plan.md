# Organic Irregular Island + LINE diagnosis (ticket 86ca9qwr3, PR #50 continuation)

## AC0 — the "LINE" — DIAGNOSED (trace-proven)
**Real cause: the URP main-light real-time SHADOW-DISTANCE boundary.**
- `FarHorizonURP.asset`: `m_ShadowDistance: 50`, `m_ShadowCascadeCount: 1`.
- The Sun's soft directional shadows render only within 50u of the camera. The hard edge where
  shadow-receiving stops paints a dead-straight (large-radius arc reads straight at island scale)
  dark band on the terrain.
- Proof chain (isolation probe `-diagLine`, scatter/vista/clouds hidden):
  1. scatter hidden → line STILL there → not a tree/object shadow.
  2. `-noShadows` → line GONE → it IS a shadow.
  3. `-shadowDist 20` → line moves off-frame (toward cam); `-shadowDist 160` → line GONE (pushed past
     the visible play area) → it's the shadow-DISTANCE boundary.
  4. shifted camera +40u X → line tracks the CAMERA (camera-relative ring). Sponsor reads it as
     "world-fixed" because the orbit cam stays ~over spawn (origin) in normal play, so the ring barely
     moves in world.
- Captures: `ci-out/diagline/{baseline,noshadows,sd20,sd160,shift}/`.

**Fix:** set `urp.shadowDistance` past the visible play area (≈160u) + a cascade-border fade, in
`BootstrapProject.ConfigureUrp()` so it bakes into FarHorizonURP.asset reproducibly. PRESERVES tree
shadows (trees near spawn are well within 160u) and does NOT touch the Sun light or tree-shadow setup
(OOS respected — flag to orch that the fix is the URP shadow DISTANCE, adjacent-but-distinct from the
"Sun + tree-shadow light setup" OOS).

## AC1 — organic coastline
Warp the shore radius by azimuth: `ShoreRadiusAt(angle) = IslandShoreR + coastNoise * CoastIrregAmp`.
Everything that keys off `IslandShoreR` (height falloff, sand/grass/foam bands, water foam ring) must
key off the warped `ShoreRadiusAt`. Clip the terrain mesh past shore+margin so no square grid edge
reads from overhead.

## AC2 — beach vs cliff
Low-freq azimuth noise → cliff sectors (CliffFraction of the coast). Cliff: steep vertical drop + rock
color. Beach: flat sand strip + sand color.

## AC3 — beach level with grass
Flatten the raised dome: land stays ~grass level to the coast; transition = flat sand strip (beach) or
vertical cliff (cliff), NOT a long downhill ramp.

## AC4 — foam all edges
Foam follows the warped waterline (distance-to-ShoreRadiusAt), every azimuth.

## AC5 — preserve
Re-bake whole-island NavMesh; float grounding UNTOUCHED; camera hill-collision + windowed preserved.

## AC6 — the knob
Named shape constants: CoastIrregAmp, CliffFraction, BeachWidth. Bake 3-4 SEEDS + capture
overhead+gameplay each into ci-out/ for the Sponsor to pick.
