# Far Horizon — Visual-Quality Deep Analysis of the Generated Poly World

**Role:** senior technical artist, art/look angle only (a sibling agent covers performance).
**Date:** 2026-07-01. **Repo:** `c:\Trunk\PRIVATE\Far-Horizon` (READ-ONLY analysis).

**Branch caveat:** the checked-out branch (`orch/coordination`) is BEHIND `origin/main` for exactly three look files: `Assets/Shaders/GradientSkybox.shader` (+69: sun disk, PR #194), `Assets/Scripts/Editor/QualityPassGen.cs` (+56: sun defaults + `ResolveSunDirection`), `Assets/Scripts/Editor/WorldBootstrap.cs` (+36: `SunElevationDeg=18`). Those three are cited from `origin/main` (verified via `git show`); everything else is byte-identical between the branch and main (verified via `git diff --stat`).

**Standing constraint for every proposal:** generated assets are COMMITTED snapshots — a regen-code change only reaches the build after `BootstrapProject.Run` re-bakes + `Boot.unity`/`Assets/Settings/*.mat` are re-committed (memory: unity-procedural-committed-assets-go-stale). Scatter placement is byte-locked to seed 42: never add/remove draws on an existing `System.Random` stream — use a NEW seed salt (`seed+777/888/999` precedent, `LowPolyZoneGen.cs:1012,1052,1092`) or a position-derived stream (`leanRnd` precedent, `LowPolyZoneGen.cs:1161-1162`).

**The art bar (from the actual inspiration PNGs, viewed):** chunky toy-like saturated low-poly; faceted flat-shaded props on smooth-rolled ground; bold silhouettes readable at orbit distance; warm/lush; grounds read FULL (flowers, mushrooms, stumps, rocks); tree vocabulary = blob canopies AND tiered pines (`21h10_44`, `21h11_03`, `21h12_49`, `21h16_13`); clouds = puffy multi-lobe masses with flattish undersides (`21h10_44`, `21h16_13`); water = saturated teal/blue with crisp banks; long warm shadows; grass = dense chunky 3D blade tufts with white/purple flower accents (`low poly grass.png`, `21h22_33`).

---

## 1. Element-by-element

### 1.1 Sky (gradient)
**Now:** 3-stop vertical gradient shader (`GradientSkybox.shader:86-105` working tree; same on main), stops from `WorldLookPalette.cs:31-37` (#4D94E0 zenith / #73B3EB mid / #CCE3EB horizon), `_MidPoint 0.18` + `_Softness 0.85` set at `QualityPassGen.cs:59-60` so the saturated blue sits in the band the pitch-55 orbit actually frames. Fog colour == horizon stop (seam-kill, `QualityPassGen.cs:100-107`).
**Read vs bar:** solid. The cheerful-blue-at-gameplay-angle fix already went through two soak rounds (S2 history in `WorldLookPalette.cs:19-30`). The gradient is azimuth-uniform, which is its one flatness: real toy-diorama skies (board `21h16_13`) warm up around the sun.
**Proposals:**
- **(SKY-1) Warm horizon bias toward the sun azimuth.** In the frag, lerp the horizon stop a few % toward `_SunColor` by `pow(saturate(dot(normalize(float3(dirWS.x,0,dirWS.z)), sunAzimuthDir)), ~3)` × (1−t). Ties the accepted low sun into the sky; the ocean-horizon framing (where the Sponsor judged the sun) gets a subtle warm bed. Payoff **med**, effort **S**, risk **low** (additive, sub-1.0, keep seam-kill by capping the bias so fog colour ≈ horizon stop within a few LSB — or drive fog colour only from the un-biased stop as today; the fog is uniform so keep bias ≤ ~0.06 to avoid a visible fog↔sky mismatch at the sun azimuth).

### 1.2 Sun
**Now (main):** big warm-white disk (`_SunSize 0.95` = biggest in range, hardness 60, colour 0.98/0.86/0.86) composited lerp-core + additive corona so hue survives bloom (`origin/main GradientSkybox.shader` sun block); direction baked from the real "Sun" light (`QualityPassGen.ResolveSunDirection`, main) at the **Sponsor-accepted 18° elevation** (`WorldBootstrap.SunElevationDeg`, soak 55bde02).
**Read vs bar:** Sponsor-accepted 2026-06-30 — **do not re-tune hue/size/elevation.** The lerp-core trick is exactly right.
**Proposals:** none load-bearing. SKY-1 above is the only complement. (Optional micro-polish: 1-2% slow `_SunSize` breathing would fight the "static = calm" read of a sun — skip.)

### 1.3 Clouds
**Now:** 6-10 flat-shaded multi-blob clouds (`WorldBootstrap.BuildClouds:306-367`), 5-6 spheroids each, yScale 0.78, 3-value near-white cyan (body #C7EAF2 / cap #E6F7FC / underside #80C7D9, `WorldBootstrap.cs:54-56`), altitude 28-42u, shared-wind drift 0.22-0.48 u/s with wrap (`CloudDrift.cs`). Shadow casting OFF (`WorldBootstrap.cs:355`).
**Read vs bar:** shape language and drift match the board sheet (`21h10_44`). Two gaps: (a) board clouds have **flat-ish undersides** (cumulus read) — ours are jittered spheroids top AND bottom, so some read as lumpy potatoes; (b) all clouds share one silhouette recipe (5-6 blobs, radius 5-7.5) — the board sheet mixes small 2-blob wisps with big 7-blob banks.
**Proposals:**
- **(CLD-1) Flat cloud base.** In `CloudBlob`/`AppendFlatBlob` (`LowPolyMeshes.cs:710-829`), clamp final vertex Y to `max(y, baseY)` with `baseY ≈ −0.25×radius` before flat-shading (per-blob, after displacement). Instantly reads "cumulus" like `21h16_13`/`21h10_44`. Payoff **med-high**, effort **S**, risk **low** (shape-only; cloud count/positions untouched — placement draws live in WorldBootstrap, mesh internals have their own `rnd.Next()` seed). Keep the 3-value step: the flat base is naturally the shadow-teal underside.
- **(CLD-2) Silhouette variety.** Widen `blobs` to 3-8 and radius to 4-9 keyed off the existing per-cloud draws (careful: these ARE WorldBootstrap stream draws — change the RANGES, not the number of draws, so the stream length is identical; positions re-roll only if ranges change values consumed → they don't, `rnd.NextDouble()` count is unchanged; the cloud SHAPES will differ = soak). Payoff **med**, effort **S**, risk **low-med** (clouds not Sponsor-locked; contrast values ARE — keep the 3 cyans).
- **(CLD-3, flagged for perf sibling) Drifting cloud shadows.** The board vista shows cloud shadow patches on the land. Cheapest honest route: turn `shadowCastingMode` ON for the 6-10 cloud renderers (they're big + few) — but at sun elevation 18° shadows land far sideways and may miss the island; a faker alternative is 2-3 large soft `BlobShadowDisc`-style decals drifting with a pseudo-cloud offset. Payoff **med** (alive-world), effort **M**, risk **med** (perf + the low sun makes real shadows unpredictable — prototype behind a console toggle first).

### 1.4 Sea water
**Now:** one 1400u square unwelded flat-facet grid (`BuildIslandWater`, `LowPolyZoneGen.cs:1525-1652`), vertex teal gradient `WaterShallow #1A9EA8-ish (0.10,0.62,0.66) → WaterDeep (0.10,0.50,0.60)` by distance past the warped coast (`:1557-1560`), transparent `LowPolyWater.shader` with in-vertex swell (`_WaveAmp 0.45 / _WaveLen 11 / _WaveSpeed 1.1`, `LowPolyZoneGen.cs:1933-1935`), depth-fade foam 1.5u, `_FogCap 0.5` teal-vs-sky horizon hold, alpha 1 (solid).
**Read vs bar:** colour + swell are Sponsor-iterated (multiple pixel-sampled soak fixes, `LowPolyZoneGen.cs:104-141`) — don't re-tune the teal anchors. The structural gap vs the board (`21h16_52` lake sparkle, "lively motion" memory): **lighting is static.** The mesh carries fixed +Y facet normals (`:1645`), the swell displaces positions in the vert stage, but `frag` shades with the interpolated static normal (`LowPolyWater.shader:162`) — so the moving sea never glints; it's a flat-lit moving carpet.
**Proposals:**
- **(SEA-1) Dynamic facet glints — the single biggest water upgrade.** In `LowPolyWater.shader` frag, derive the true displaced-surface facet normal with the derivative trick already proven in the sister shader (`LowPolyVertexColor.shader:232-234`): `float3 n = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));` (positionWS is post-swell). Use it for ndotl AND add a small Blinn specular toward the sun: `spec = pow(saturate(dot(n, normalize(L+V))), ~48) × sunColor × ~0.35`. As the swell rolls, individual facets catch the 18° sun and twinkle — the classic low-poly sea sparkle, and the sparkle crossing the 1.02 bloom threshold gives a free glitter corona. Payoff **HIGH** (lively water = explicit Sponsor preference; kills the flat-carpet read), effort **S/M** (≈10 shader lines + a `_GlintStrength` property defaulting 0 so the pond material stays glassy at 0), risk **low-med** (shader-only; put `_GlintStrength` on the dev-console `WorldLookTunables` surface so the Sponsor dials it; keep OFF for the pond — the pond is locked-still). ~3-4 ALU + one pow — flag to perf sibling, trivial.
- **(SEA-2) Slight facet colour shimmer (fallback/companion).** If glints alone feel too speculative, add ±2-3% value modulation keyed off `sin(worldXZ + time)` on the water albedo — cheaper read of "moving facets". Payoff **med**, effort **S**, risk **low**.

### 1.5 Foam
**Now:** three cooperating layers — (1) terrain-painted band: full-strength within ~3u of the waterline fading by 8.5u, ×0.95 (`IslandColorAt`, `LowPolyZoneGen.cs:869-871`); (2) water-mesh baked ring: core 4u, fade 9u, strength 0.92 (`:1520-1524,1563-1566`); (3) dynamic depth-fade foam 1.5u in-shader (`LowPolyWater.shader:175-195`). All use the same warm `FoamEdge #E8E2D0`.
**Read vs bar:** "foam on all edges" is a Sponsor-praised feature — keep presence. But the board's foam (`21h16_52`, `21h13_31`) is a **thin crisp line, sometimes doubled**, not a ~6-9u soft white gradient; our triple-wide-band stack is the main residual "pale wash" contributor at the coast (a historic Sponsor complaint surface: pale shore/first-frame wash).
**Proposals:**
- **(FOM-1) Two-tone foam: crisp core line + faint wash.** In the two BAKED layers, replace the single smoothstep with: full `FoamEdge` within ~1.2u of the waterline, then a LOW-strength (~0.30) wash out to the current band. One extra `if`/lerp in `IslandColorAt` + the water-grid loop. Reads as "surf line + lap zone" like the board instead of a bleached halo. Payoff **med-high**, effort **S**, risk **med** — the coast was soaked round-heavy (86ca9xyqa); ship behind a re-bake with a predict-before-soak line, and keep total foam coverage similar (presence unchanged, distribution crisped).
- **(FOM-2) Breathing foam edge.** In `LowPolyWater.shader`, modulate `_FoamDistance` by the same swell phase: `foamDist *= 1 + 0.35×sin(dot(posWS.xz, dir)/len + t)` — the dynamic foam line advances/retreats with the swell (the "washing up on shore" the Sponsor liked, `LowPolyZoneGen.cs:1925-1928`). Payoff **med**, effort **S**, risk **low** (shader-only, sea material only — `_FoamAmount 0` keeps the pond dead).

### 1.6 Beach / coast
**Now:** warped organic coastline (2-octave azimuth noise, ±26u, `ShoreRadiusAt:701-719`), 42% cliff sectors (`CliffinessAt:724-734`), 16u warm-golden beach band + 4.5u wet shelf dipping 0.12u under the water (`HeightAtRadial:745-823`, consts `:244-252`), sand ramp `SandLo/SandHi` (0.78,0.64,0.39 → 0.88,0.75,0.49, `:47-48`), damp/rock darkening reserved for cliff feet only (`IslandColorAt:853-860`).
**Read vs bar:** the organic outline + warm sand are Sponsor-locked wins (memories: world-is-big-organic-island, coast-polish restore). Gaps: (a) the sand band is EMPTY — the board's "ground reads full" carry-over applies to beaches too (driftwood, shells, pebbles); the scatter actively rejects the beach strip (`OnLandmass` fringe rejection `:924-927`); (b) **cliff sectors read as smooth grey ramps**, not rock walls — the 1.65u welded grid + smooth normals can't make a 7.5u drop read columnar/faceted like `21h21_30`'s cliffs; colour alone (RockCol paint) carries them today.
**Proposals:**
- **(BCH-1) Beach accents pass.** New sub-stream (`seed+1111`): sparse driftwood (existing `TaperedCylinder` sticks, larger + sun-bleached grey-brown `QuantizeFine` tint), a few half-buried pebbles (existing `FacetedRock` 0.15-0.3), on beach sectors only (`CliffinessAt < 0.3`, radius in `[coast−BeachWidth, coast−WetShelfWidth]`), ~25-40 total. Payoff **med**, effort **S**, risk **low** (additive stream; NavMesh-free like sticks).
- **(BCH-2) Cliff-lip outcrops.** New sub-stream (`seed+1212`): on strong-cliff azimuths (`CliffinessAt > 0.7`), place 2-4 LARGE `FacetedRock` chunks (scale 2.5-5, RockCol) straddling the lip so the cliff silhouette breaks into hard facets against the sea. This is the cheap route to `21h21_30`'s columnar-rock read without touching the locked heightfield. Payoff **med-high** (coast silhouette at orbit), effort **M**, risk **low-med** (visual-only; keep off the NavMesh or carve obstacles like trees).

### 1.7 Ground / terrain
**Now:** 200×200 welded grid over a 330u disc, radial organic heightfield + 3-octave hills amp 9 inland (`HillHeightAt:534-544`), smooth normals (the LOCKED Zone-D dune roll — `lowpoly-quality.md §3` explicitly forbids flat-shading it), colour = radial `GrassLo→GrassHi` lerp + height `GrassRise` + rock on hilltops + fine ±0.05 per-vertex value jitter at ~0.08u hash grid (`IslandColorAt:831-887`).
**Read vs bar:** the single biggest large-area gap. The board grounds (`21h16_13`, `21h13_31`, `21h22_52`) are **patchworks of distinct green tones at the multi-metre scale** — light lime meadows against deep green — while ours is one continuous radial ramp + sub-vertex noise, i.e. a uniform green carpet with fine grain. The jitter (13× hash) is far finer than the 1.65u vertex spacing, so it reads as texture noise, not tonal patches.
**Proposals:**
- **(GRD-1) Macro meadow patches — highest payoff-per-line in the whole report.** In `IslandColorAt`, before the jitter, blend the grass toward 2 extra tones by LOW-frequency world noise: e.g. `patch = Perlin(ox+wx×0.045, oz+wz×0.045)` → lerp toward a sunlit lime (`~0.55,0.68,0.30`) above 0.62, and toward a deeper meadow green (`~0.26,0.44,0.19`) below 0.35, smoothstepped. Pure function of (wx,wz,ox,oz): deterministic, zero RNG-stream impact, **zero silhouette change** (colour only → seed-42 shape lock intact). 8-15u patches match the board's scale. Payoff **HIGH** (the whole island stops being monochrome), effort **S** (~8 lines + regen), risk **low-med** (needs one soak; keep all tones sub-1.0 and inside the warm family; PondCollarGreen paint is applied after grass so unaffected — verify collar contrast still reads).
- **(GRD-2) Optional: quantize the patch field** (`floor(patch×3)/3`) for harder-edged tonal patches (closer to the board's per-facet patch look) — try both in the same soak build via a console float.

### 1.8 Trees
**Now:** 320 trees, all ONE species — blob-canopy (4-6 welded spheroids, 3-value greens `CanopyBody/Top/Shadow`, `LowPolyZoneGen.cs:85-87`; mesh `BlobCanopy` `LowPolyMeshes.cs:501-557`) on a straight 6-side tapered trunk; 55% "tall jungle" variant = longer trunk + higher canopy (`BuildTree:1138-1214`); seeded yaw + ±20% height + 3-8° lean via position-derived stream (`:1160-1172`); canopy-only wind sway approved (`CanopyVertexColorMat:1741-1767`).
**Read vs bar:** the blob language is right (`21h11_03`), and sway-on-trees-only is the Sponsor's exact call (#172). Two gaps: (a) **every tree in the forest shares the same 3 greens** — `21h11_03`'s four trees are four *different* green hues (yellow-green, mid, deep, teal-leaning); at orbit distance our canopy mass fuses into one tone; (b) **no pines** — the board's vista/ground shots (`21h12_49`, `21h16_13`, `21h21_30`, `21h22_33`) are pine-dominated, and art-direction.md explicitly says "Pine trees join blob trees in the tree vocabulary"; the vocabulary is currently unshipped; (c) trunks are perfectly straight cylinders — board trunks are chunky and slightly bent (the lean tilts the whole tree but the shaft itself is rigid).
**Proposals:**
- **(TRE-1) Per-tree canopy hue variation.** In `BuildTree`, derive a hue-shift from the existing position-keyed `leanRnd` stream (add draws THERE, not on `rnd` — placement stays byte-identical) and pass shifted copies of the 3 greens into `BlobCanopy` (e.g. ±0.05 R, ±0.06 G rotations, 4 quantized variants). Vertex-colour baked → **zero material churn, zero draw-call cost**. Payoff **HIGH** (forest gains depth at orbit instantly), effort **S**, risk **low** (colour-only, additive; soak).
- **(TRE-2) Pine variant.** A `PineTree` builder: 3-4 stacked `Cone`s (existing mesh fn, `LowPolyMeshes.cs:1226-1252`) with decreasing radius on a `TaperedCylinder` trunk, its own 2-3 green values (cooler/deeper than blob greens per `21h12_49`), flat or welded — welded+smooth matches our canopy idiom; consider `_FlatShading` toggle material for the crisper board pine read. Route: position-derived selection (`leanRnd.NextDouble() < ~0.35` → pine instead of blob) so the 320 placements are untouched and no new stream is needed; only tree TYPE at fixed positions changes. Payoff **HIGH** (unlocks the board's tree vocabulary; vista shots are unreachable without pines), effort **M**, risk **med** (a new silhouette class = Sponsor soak; NavMesh obstacle logic reused as-is).
- **(TRE-3) Trunk kink.** Build trunks from 2 stacked `TaperedCylinder` segments with a 4-8° mid-kink (position-derived angle). Payoff **low-med**, effort **S**, risk **low**. Do after TRE-1/2 — it's garnish.

### 1.9 Rocks / stones
**Now:** flat-shaded `FacetedRock` (32 facets, anisotropic chunk, per-facet value 0.80-1.0, outward-winding enforced, geometric AO in alpha ×0.5, `LowPolyMeshes.cs:335-481`; material `RockVertexColorMat` with the 24-step `QuantizeFine` pink-cast fix, `LowPolyZoneGen.cs:1811-1841`), 60 boulders in 2-4-piece outcrops + 70 pickable pebbles.
**Read vs bar:** this element already ate its soak rounds and matches `21h10_44`/`low poly grass.png` rocks well (warm light grey, chunky, AO-grounded). QuantizeFine (Rec 1) and vertex-AO (Rec 6) are landed.
**Proposals:**
- **(RCK-1) Subtle rim adoption (Rec 4 follow-through).** The shader's `_RimIntensity` term exists but nothing opts in (`LowPolyVertexColor.shader:77-79,288-290`). Try `_RimIntensity 0.10-0.15 / _RimPower 3` on the ROCK material only — a whisper of caught-sun on boulder silhouettes (the board's edge-light read, cheap fallback to the Blender chamfer). Payoff **low-med**, effort **S** (one line in `RockVertexColorMat` + regen), risk **low** (dial via console; 0 = today).
- (Boulder scale variety at outcrops is already good; no shape work needed.)

### 1.10 Bushes / berries
**Now:** 80 squat `BushBlob` domes (same 3-value idiom, own slightly-saturated greens `:94-96`), 40% carry `BerryCluster` — 20-30 small red dots, 2-value alternated, golden-angle packed (`LowPolyMeshes.cs:645-684`, the "flowers→berries" #101 fix). Stationary by dedicated material (#172 soak-NIT, `BushVertexColorMat:1782-1801`).
**Read vs bar:** solid and Sponsor-corrected twice; leave the core alone. One idea: **(BSH-1)** extend TRE-1's hue variation to bushes (same position-derived trick) so undergrowth isn't one green — piggybacks on the same PR. Payoff **low-med**, effort **S**, risk **low**.

### 1.11 Grass
**Now:** 360 `GrassClump` tufts — 7 thin two-sided CARDS each, 0.55u, up-biased normals (the iter-8 dark-shard fix), bright-biased single green, stationary (`LowPolyMeshes.cs:1167-1220`, `LowPolyZoneGen.cs:1266-1315`).
**Read vs bar:** the biggest single gap vs the Sponsor's newest reference (`low poly grass.png`, catalogued 2026-06-29): the reference is a **dense meadow of chunky 3D blade geometry with white/purple flower accents**; ours is ~360 sparse thin-card tufts on a 45,000 m² island — effectively invisible at gameplay framing, and the blades are exactly the "flat billboard" the art note rules out. Ticket `86cabc737` (Grass POC — chunky-blade tufts, stationary) already exists; this section is its visual spec.
**Proposals:**
- **(GRS-1) Chunky blade mesh.** New `GrassBladeTuft`: 5-9 blades, each a 2-3-segment triangular PRISM (or a folded 2-plane wedge — 4-6 tris/blade) with real width and a pointed tip, splayed + 1-2 crossed per tuft, slight per-blade height/hue (2 greens), flat-shaded with up-biased normals (keep the iter-8 rule: never let RecalculateNormals near coincident two-sided geometry). Base at y=0. Payoff **HIGH at player framing** (this is what the player stares at all game), effort **M**.
- **(GRS-2) Patch-based lushness, not global density.** Reuse GRD-1's meadow-patch noise as a placement MASK: concentrate tufts inside "lush patches" (accept-probability ∝ patch value) so patches read DENSE like the reference while total count stays budget-friendly; between patches the macro colour variation carries the ground. New sub-stream (`seed+1313`) or replace the existing grass pass wholesale in the POC (grass placement is not itself a Sponsor-praised artifact; the seed-42 lock protects the island + existing look, so replacing the grass pass needs a soak + regen, not a stream-preservation dance — but keep the rock-overlap reject `OverlapsAnyRock:1331-1341`).
- **(GRS-3) Flower accents.** In ~20% of tufts add 1-3 tiny flower heads (5-tri "cross-quad + dot" or a 6-vert mini-blob) in white + purple (`low poly grass.png`, `21h22_33` show exactly these two). Material-honest: petal white ~0.9 sub-1.0, purple ~(0.55,0.35,0.70). Stationary. Payoff **med-high** (the "lush purposeful decoration" carry-over), effort **S** on top of GRS-1, risk **low**.

### 1.12 Pond
**Now:** organic lobed rim (`PondRimFactor:2055-2065`), uniform calm fresh-blue B>G gradient (`PondShallow/Deep:158-159`), glassy micro-swell, foam MASTER-OFF (`MakePondMaterial:1981-2025`), carved knee-deep bowl with two-segment wall + collar paint + hill-flatten (`:269-516`).
**Read vs bar:** **9+ Sponsor soak rounds (#130 saga). LOCKED. Do not touch** — colour, stillness, foam-off, recess geometry, collar paint are all explicit Sponsor decisions. Only note: if SEA-1 glints ship, the pond material must keep `_GlintStrength 0` (a glittering pond would reopen "still pool").

### 1.13 Mountains / vista
**Now:** 5 discrete mountain ISLANDS ringing the sea (2-4 multi-ring ridged peaks each, snow caps, secondary shoulders, `FacetedMountain` `LowPolyMeshes.cs:853-1012`), each grounded on a `FacetedLandmass` shelf (`:1029-1094`), warm tan-brown body #9E8061 that survives the cool ambient (`WorldBootstrap.cs:78-83`), tint fade capped 0.25 + fog does the recession (double-fade fix), distances ×0.62 (`WorldLookConfig.cs:30-38`).
**Read vs bar:** heavily iterated (floating-shards → grounded islands → warmth ×2); silhouettes + snow now match `21h16_13`'s language. One believability gap: the board's distant landmasses carry a **green/forest band between rock and water** (`21h16_13`: pine band at the mountain feet) — ours are bare rock shelves rising from the sea, reading slightly "asteroid".
**Proposals:**
- **(VIS-1) Vegetation band on the vista islands.** Cheapest: in `FacetedLandmass`, colour the TOP-dome faces a muted canopy green (lerped toward the cluster tint so it recedes in lockstep) while flanks stay rock — a green cap = "forested isle" at 400-500u for zero extra meshes. Optional second step: 6-12 tiny cones (pine proxies, 20-30 tris total) scattered on each shelf top, static-batched. Payoff **med**, effort **S** (colour) / **M** (cones), risk **low** (far decor, fog-faded; keep tint-lockstep so no seam-kill drift).
- **(VIS-2) One peak count/height re-roll for variety** (2-peak islands read a bit "twin bumps"): widen per-peak height jitter (0.82-1.22 → 0.7-1.35). Payoff **low**, effort **S**, risk **low**.

### 1.14 Sticks / small stones
**Now:** 70 + 70, own sub-streams, horizontal tapered sticks (±6° pitch wobble) + mini `FacetedRock` pebbles (`LowPolyZoneGen.cs:1414-1486`).
**Read vs bar:** fine; they're loot-props first. No visual work warranted beyond BCH-1's beach cousins.

### 1.15 Fog + post stack
**Now:** Exp² density 0.0016, colour == horizon stop (seam-kill), `QualityPassGen.cs:84-108`; Bloom 0.25 @ threshold 1.02, warm grade (contrast 6, saturation 12, WB +8), vignette 0.28, Neutral tonemap (`:114-185`); SMAA on camera.
**Read vs bar:** the Uma-directed "bloom down / lighter grade" retune landed; sub-1.0 palette discipline + threshold 1.02 is a coherent HDR system — **any new bright element must stay sub-1.0 flat, and only intentional sparkle (SEA-1 spec) should cross the bloom threshold.** No changes proposed; the stack is the glue other proposals must respect.

### 1.16 Alive-world motion inventory (cross-cutting)
Currently moving: water swell, canopy sway, cloud drift, campfire (loop object). Sponsor's rule: lively is good, but ground cover stays still (#172). The board adds one missing class: **fauna silhouettes**.
- **(ALV-1) Birds.** 2-4 bird flecks (2-3 tris each — a bent chevron), slow wide orbits at 20-30u altitude with gentle bob + occasional flap-scale-pulse, `CloudDrift`-style serialized component (editor-authored, runtime translate only). `21h13_31` and `21h16_13` both feature birds; "small player, big ALIVE world" is the north-star sentence. Payoff **med-high charm per triangle**, effort **S/M**, risk **low** (additive; keep them off the NavMesh/gameplay; one-shot trace line per the discipline).

---

## 2. Do-NOT-touch list (Sponsor-locked or bug-scarred)
1. Pond: everything (colour/still/foam-off/recess) — #130 ×9 rounds.
2. Seed 42 island outline, scatter placements, waterline — byte-locked (tests assert it).
3. Sand warm anchors + damp-only-on-cliffs (`:47-49, 853-860`) — coast-polish soak.
4. Sea teal anchors `WaterShallow/Deep` — pixel-sampled ×3 soak fixes.
5. Sun elevation 18° / size 0.95 / warm-white — Sponsor live-dialed + accepted (55bde02).
6. Cloud 3-value cyans — S2 contrast fix.
7. Fog colour == SkyHorizon seam-kill (single-constant binding) — never fork it.
8. Smooth-normal welded terrain (never flat-shade it), grass/bush stationary (#172), sway on tree canopies only.
9. `_FoamAmount` master-gate semantics (pond OFF depends on it).
10. Outward-winding enforcement + explicit per-face normals on all flat-shaded meshes (`lowpoly-quality.md §1`).

## 3. Process constraints on every proposal
- Regen + commit: every mesh/colour change requires `BootstrapProject.Run` re-bake + committing `Boot.unity`/`Assets/Settings/*.mat` (committed-snapshot rule) — a code-only PR ships nothing.
- Predict-Before-Soak + gameplay-cam capture evidence for anything visual (TESTING_BAR); side-profile silhouette check for anything physical (lowpoly-quality §0).
- New dials go on the dev-console (`WorldLookTunables` seam, main) — Danish-keyboard-safe keys only if any new direct handle is added (PgUp/PgDn/F-keys/letters).
- Colour discipline: sub-1.0 all channels; near-neutral tints (chroma < 0.10) through `QuantizeFine` (Rec 1).
- RNG discipline: new prop classes = new seed salts; per-instance variation on existing props = position-derived streams (`leanRnd` precedent).

## 4. Ranked top-10 (visual angle; quick wins first)

| # | Proposal | Payoff | Effort | Regression risk |
|---|----------|--------|--------|-----------------|
| 1 | **GRD-1 Macro meadow patches** — low-freq 2-3-tone green variation in `IslandColorAt`; kills the monochrome carpet, matches the board's patchwork ground | High | S | Low-med (colour-only, shape byte-identical; one soak) |
| 2 | **TRE-1 Per-tree canopy hue variation** (+ BSH-1 bushes) — position-derived hue shifts baked into vertex colour; forest gains tonal depth free of draw calls | High | S | Low |
| 3 | **CLD-1 Flat cloud bases** — clamp blob undersides; spheroid potatoes become cumulus toys | Med-high | S | Low |
| 4 | **SEA-1 Dynamic facet glints on the sea** — ddx/ddy facet normal + small sun specular in `LowPolyWater`; the sea starts to LIVE and sparkle (pond stays 0) | High | S/M | Low-med (dialable, shader-only) |
| 5 | **FOM-1/FOM-2 Crisp two-tone foam + breathing edge** — thin bright surf line + faint wash, swell-modulated foam distance; de-bleaches the coast while keeping foam-on-all-edges | Med-high | S/M | Med (coast is soak-scarred; predict-before-soak) |
| 6 | **TRE-2 Pine tree variant** — stacked-cone pines at ~35% of existing positions (position-derived selection); unlocks the board's core tree vocabulary | High | M | Med (new silhouette class → soak) |
| 7 | **GRS-1/2/3 Grass meadow POC** (ticket `86cabc737`) — chunky prism blades in dense patches + white/purple flowers; the reference image made real at player framing | High | M/L | Med (new ground-cover read; perf-coupled — patch masking contains it) |
| 8 | **BCH-2 Cliff-lip rock outcrops** — large FacetedRock chunks on strong-cliff azimuths; smooth grey ramps become rock coast | Med-high | M | Low-med |
| 9 | **ALV-1 Birds** — 2-4 drifting chevron silhouettes; big-alive-world charm for a handful of triangles | Med-high | S/M | Low |
| 10 | **VIS-1 Vista-island green band** (+ BCH-1 beach driftwood/shells as the same-PR sibling) — forest cap colour on landmass shelves; distant isles stop reading barren | Med | S | Low |

Honourable mentions (below the cut): SKY-1 sun-azimuth horizon warmth (S, med), RCK-1 rock rim-light adoption (S, low-med), TRE-3 trunk kink (S, low-med), CLD-3 cloud shadows (M, perf-coupled — prototype behind a toggle), GRD-2 quantized patch field (free A/B inside #1's soak build).

**Suggested sequencing note (single Unity build slot):** #1+#2+#3 (+#10, +RCK-1) are all bake-time colour/mesh-shape tweaks with disjoint files from #4+#5 (shader work) — two clean PR waves, each one regen+soak; #6 and #7 are their own feature PRs with dedicated soaks.
