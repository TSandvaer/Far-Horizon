# Snow-cap chunky/faceted plan — ticket 86cahmxh6 (follow-up of POC 86caa9zpp / PR #226)

## Sponsor direction (verbatim, 2026-07-02 walk-soak item 4)
"snow cap could be less smooth and round and a bit more chunky/faceted". The climb is APPROVED — do NOT regress the climbable-slope tuning.

## Anchor (lowpoly-quality §0)
A snow cap on a mountain is a layer of SNOW collecting on the cold upper peak — in the chunky-cartoon
board style (`inspiration/2026-06-12_21h12_49.png` + `21h16_13.png`) it reads as ANGULAR broken white
PLANES with hard faceted edges following the ridge, NOT a smooth rounded dome of white. The build must
satisfy THAT: the snow ZONE reads chunky/faceted side-on; grass/rock lower zones + climbability unchanged.

## Reference read (looked at the images)
- `21h12_49`: snow band = jagged crystalline white planes, hard edges, sits on the faceted grey rock.
- `21h16_13`: peaks are sharp triangular facets; snow is an angular patch near the summit, not a smooth cap.

## Current cause of the smooth read (NextIslandPocGen.cs)
- Terrain is ONE welded grid mesh built with `RecalculateNormals()` → smooth averaged normals everywhere
  (line 284). Great for grass/rock dunes; wrong for the snow cap.
- `MountainHeightAt` is a smooth raised-cosine dome (line 156-167) → smooth summit surface.
- `ColorAt` bands snow as a smooth `SmoothStep` white above `SnowlineFrac=0.72` (line 336).

## Design — single mesh, mixed normals + bounded angular snow displacement
Keep grass/rock/beach/seabed + collider + climbability BYTE-EQUIVALENT; face-facet ONLY the snow zone.

1. **Snow-zone classification** — a vertex/face is "snow zone" when its mountain-height fraction
   `MountainHeightAt(x,z)/MtnPeakHeight >= SnowFacetFrac` (a threshold at/just below `SnowlineFrac` so the
   faceting covers the full visible white band with a small blend margin). Pure function of XZ (deterministic).
2. **Angular height displacement (snow zone only)** — add a small angular/blocky perturbation
   `SnowFacetAmp * angularNoise(x,z)` to the snow verts so the summit surface is broken planes, not a smooth
   dome. BOUNDED so the local slope stays climbable: amp small (~2.5u) at a coarse cell (~14u) → added slope
   `atan2(2*amp, cell) ≈ atan2(5,14) ≈ 20°` worst case, stacked on a near-FLAT summit (raised-cosine is
   flattest at the top) → stays well under the 45° NavMesh agent max. Displacement fades to 0 at the snow
   line (a `SmoothStep` in-ramp) so there is no cliff between the smooth rock flank and the faceted cap.
3. **Flat per-face normals for snow-zone triangles** — build the mesh with EXPLICIT normals (NO
   `RecalculateNormals`): smooth-averaged normals for non-snow verts (preserve the dune look), per-face flat
   normals for snow-zone faces (the `LowPolyMeshes.FacetedRock` idiom — own 3 verts per snow face + face
   normal + `Vector3.Dot(fn, faceCentre)` up-orient guard). This is what makes the snow read as angular
   planes even at modest displacement. Non-snow stays welded-smooth.
4. **Collider** — built from the SAME displaced mesh so the climb is measured on the real faceted surface
   (NavMesh bakes the truth, not a smooth proxy).
5. **`ColorAt` unchanged** (keyed on the smooth `MountainHeightAt`) → the snow-white/rock/grass banding +
   the `AC3_HeroMountain_HasHeightThresholdSnowCap` regression guard stay green. Snow stays a
   height-threshold WHITE VERTEX COLOUR (carried constraint — NO snow texture).

## Constraints held
- No new shader (shared `FarHorizon/LowPolyVertexColor`); snow = height-threshold white vertex colour.
- Grass/rock/beach zones + the raised-cosine `MountainHeightAt` dome constants UNCHANGED
  (`MtnPeakHeight=135` / `MtnFootRadius=300` / ratio 0.45 — `PlayModeMirror_MountainProportion` pins it).
- Climb tuning as-approved: displacement bounded + summit-flat, verified by the shipped NavMesh trace +
  the `HeroMountain_IsClimbable_AgentPathsFromFootToSummit` PlayMode guard (the mirror dome is unchanged, so
  it stays green; the REAL faceted surface is proven by the `-verifyPocIsland` NAVMESH COVERAGE + highest-
  reachable-Y trace from the shipped exe).
- Welded terrain NOT flat-shaded wholesale (lowpoly-quality §3 spike-polyhedron rule) — ONLY the snow faces.

## Regression guard (PR #216 gate)
- New EditMode test `SnowZone_IsFaceted_HasAngularPlanes`: build the POC terrain, assert the snow-zone
  triangles carry PER-FACE (non-averaged) normals — i.e. adjacent snow faces differ in normal beyond a
  smooth threshold — while a non-snow (grass/rock) sample stays smooth. Catches the bug CLASS (snow reverts
  to smooth-welded), not just the instance.
- Existing guards stay green: `AC3_HeroMountain_HasHeightThresholdSnowCap`,
  `AC3_HeroMountain_IsClimbable_SlopeUnderNavMeshAgentMax`, `HeroMountain_IsClimbable_AgentPaths...`,
  `BuiltPocTerrain_HasSnowWhiteVerts_IsClipped_AndCarriesACollider`.

## Cross-lane integration check (PR #216 gate)
Adjacent surfaces this change CAN touch: (a) the terrain MESH build (snow triangles now unwelded → vert/tri
count rises — the CLIPPED-triangle-count test asserts `< 0.92 * fullGrid`, still true; verify); (b) the
COLLIDER / NavMesh (displaced snow → re-baked `PocNavMesh.asset`; climb trace must stay high-Y); (c) the
committed generated assets (`NextIslandPoc.unity` + `PocNavMesh.asset` regenerated + re-committed —
code-only ships nothing, unity-conventions.md §"procedural committed assets go stale"); (d) the side-profile
CAPTURE (`PocIslandVerifyCapture` unchanged — it already frames the side profile).

## Build/commit order (unity-conventions.md:24-25 — avoid the checkout-wipe trap)
1. Commit the `.cs` edits FIRST (NextIslandPocGen.cs + the new EditMode test).
2. `tools/debug/build_poc_island.sh --keep-churn` (regen POC scene + NavMesh + build exe + capture).
3. Surgically re-add ONLY the regenerated committed POC assets: `Assets/Scenes/NextIslandPoc.unity`
   `Assets/NavMesh/PocNavMesh.asset` (+ `.meta` if changed); restore the rest of the bootstrap churn.
4. Evidence: `poc_mountain_side.png` + `poc_mountain_side2.png` (side profile) + `poc_gameplay.png` +
   the `[poc-trace]` NAVMESH COVERAGE / climb-RESULT / PERF lines.

## LOCAL-UNITY COLLISION (Drew parallel build)
`gh run list --limit 3` before any local Unity run; wait out in-progress self-hosted jobs; on EPERM/PackageCache
rename → another local Unity holds the lock — wait 5-10 min, retry ONCE, never clean caches.
