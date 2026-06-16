# BIG ROUND ISLAND — implementation plan (ticket 86ca9a7qn)

Sponsor (verbatim): "make the island much much bigger, round, with water on all sides.
the mountains should sit on other islands. if this cannot be achieved its a showstopper.
also i want the island to be quite big with elevation (hills). dense forest/jungle with high trees."

Feasibility-FIRST → new world basis. Branch off `drew/world-look-impl` (inherit accepted
sky/water/cloud/foam shaders + mountain style). ONLY island SHAPE/SIZE/elevation/forest change.

## Current world (what I'm replacing)

- `LowPolyZoneGen.BuildZone` builds a RECTANGULAR STRIP: terrain X±45, Z -12..56; a 1D Z-profile
  `HeightAt(fz,fx)` (beach dips at fz=0, rises to inland meadow). Water is a seaward grid on the
  −Z side ONLY. Foam at `WaterlineWorldZ -10`.
- Player NavMesh/click-move runs on `MovementCameraScene.TestGround` (flat Y=0, GroundHalf=30,
  Z -10..+30, renderer DISABLED). Visual terrain is `Ground_Play`.
- Spawn = origin ~(0,0,6); loop objects CraftSpot(8,6) ChopTree(-9,-7) FirePit(4,-8) BeachDebris(-3,-3) —
  all within ~10u of origin.
- Vista mountains: `WorldBootstrap.BuildVista` builds 5 clusters on landmass bases at 230-410u
  (already SEPARATE islands — the existing design already grounds them off the play space).

## New world basis (round island)

KEEP origin as the island CENTRE (spawn + loop objects sit near centre, dry, inland).

1. **Radial-falloff heightmap** — `HeightAt(worldX, worldZ)`:
   - radial distance r from origin. Land plateau for r < IslandCoreR; smooth falloff to below
     sea level past IslandShoreR (radial shore ring → true round coast, water on ALL sides).
   - + multi-octave Perlin HILLS (elevation) across the land, amplitude ramped DOWN near the
     coast (clean waterline) and UP inland (hills/elevation per Sponsor).
   - + a gentle wet-shelf at the radial waterline (carry the accepted soft-foam wash, now RADIAL).
   - MUCH bigger: IslandShoreR ~120u (≈240u diameter) vs the current ~90×68 strip.
2. **Terrain mesh** — a big square grid (covers the island disc + a sea margin); height from the
   radial `HeightAt`; sand→grass→rock vertex colours by elevation + a radial foam ring at the coast.
3. **Water on ALL sides** — replace the −Z-only seaward grid with a LARGE water plane/disc centred
   at origin, extending well past the island to the fog horizon (covers all 360°). Radial foam ring.
4. **Mountains on SEPARATE islands** — keep `BuildVista`'s landmass-base design (already separate
   islands), but ensure every island clears the BIGGER main island (push out: near edge must be
   ≥ IslandShoreR + margin) + ring them around the sea (not just the +Z forward arc — water is on
   all sides now, so islands can ring more of the horizon while keeping open-sky gaps).
5. **Dense TALL jungle** — taller trees (trunk + canopy taller; add a tall pine/jungle variant),
   denser radial scatter across the land (not the current sparse inland-only band), thinning at
   the coast. Keep low-poly.
6. **NavMesh** — grow the walkable proxy to cover the bigger island so click-move works across it.
7. **Perf** — measure FPS / draw calls / batches at the target size; report. Showstopper if
   prohibitive (don't silently shrink).

## Proof-first (de-risk the showstopper)

Headless bootstrap run + EditMode geometry asserts BEFORE judging visually:
- roundness: sample HeightAt around rings → land inside core, sea outside shore, ~uniform coast
  radius across all azimuths (true round, not a strip).
- water-on-all-sides: the sea plane covers ±X and ±Z past the island.
- mountain-island separation: every vista landmass near-edge ≥ IslandShoreR + margin (off the main island).
- elevation: inland height range proves real hills (max-min > threshold).

## Test rewrite (the strip→round basis change)

The existing WaterSceneTests / WorldLookSceneTests hardcode the strip (seaward-Z gradient, foam at
Z-10, loop objects at fixed Z, vista distances). These MUST be rewritten to the round-island
contract (radial coast, radial foam ring, vista clears the bigger island). This is the explicit
"→ new world basis" — the tests move with the basis. Regression-guard the NEW contract.
