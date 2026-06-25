using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// PROCEDURAL low-poly smooth-shaded terrain + mesh scatter for the production environment.
    /// Ported from the eval spike (EmbergraveUnitySlice, Assets/Scripts/Editor/LowPolyZoneGen.cs,
    /// iter-5/8 — READ-ONLY working spec per ticket 86ca86fux). This is the Sponsor-approved
    /// "Zone D" look (verbatim: "i love zone D + quality") built as the ACTUAL play space the
    /// character moves through — NOT a side-by-side A/B/C/D comparison vignette like the spike had.
    ///
    /// WHY PROCEDURAL (not a CC0 pack): the spike permitted procedural ("procedural is fine if
    /// headless pack-import is clunky"). FBX/GLB pack import in -batchmode needs per-mesh import
    /// settings (normals mode, smoothing angle, material assignment) that are GUI-shaped and fragile
    /// to drive headlessly. Procedural welded meshes give deterministic, reproducible-from-code
    /// geometry AND let us control the smooth-shading directly: a WELDED mesh (shared verts at face
    /// seams) + Mesh.RecalculateNormals() averages vertex normals across faces, which IS the
    /// technical definition of "low-poly with smooth shading" (averaged vertex normals, NOT
    /// flat/faceted per-face normals). The Sponsor judges the LOOK; the source is an implementation
    /// detail a CC0/ArtDrop path can replace later with no scene-shape change.
    ///
    /// PALETTE: WARM / LUSH per art-direction.md + inspiration/*.png (sunny survival POC) — sand=warm
    /// tan, field=warm harmonious greens, water=warm-leaning teal. All sub-1.0 / HDR-safe. This is
    /// the warm/lush direction, NOT the retired "dark sinister iso building" direction.
    /// </summary>
    public static class LowPolyZoneGen
    {
        // ---- WARM / LUSH palette (sub-1.0, per art-direction.md survival north-star) ----
        // FIRST-FRAME WARMTH TUNE (ticket 86ca8ce7j, absorbs pale-shore 86ca8a0u6): the shipped first
        // frame (capture_00, stamp 659e8d1) read WASHED-OUT — the player spawns on the sand band and the
        // pale sun-bleached SandHi (0.90,0.83,0.62) + warm fog + bloom + postExposure compounded into a
        // near-white cream wash. Pull the sand toward a warmer, less-blown tan (more saturated, lower
        // value) so the shore reads warm-golden, not bleached. The board (21h13_31) shore is a warm
        // amber-tan, not paper-white. Greens pushed slightly more saturated to ride the board's
        // saturated-but-warm rule. All still sub-1.0 / HDR-safe.
        // COAST-POLISH warm-sand RESTORE (ticket 86ca9xyqa AC#5c). The organic-island rewrite did NOT cool
        // the SandLo/SandHi anchors (they kept the first-frame warmth tune) — the warmth DISAPPEARED because
        // IslandColorAt drags the SEAWARD beach (the part the player actually sees at the waterline) toward
        // SandDamp via the belowLip/damp blend on EVERY sector (beach + cliff), so the visible wet edge read
        // as the dark cool damp tone, not warm golden sand. The fix is in IslandColorAt (reserve the damp/rock
        // blend for CLIFF sectors only); here we also (a) WARM the SandDamp anchor itself so even where damp
        // shows on a beach it stays golden-warm, and (b) push SandLo/SandHi a touch more golden so the wider
        // beach (AC#5b) reads richly warm across its whole band. All sub-1.0 / HDR-clamp-safe.
        // PUBLIC so the coast-polish warmth guard pins the anchors (a regression to a cool/pale sand fails CI).
        public static readonly Color SandLo = new Color(0.78f, 0.64f, 0.39f); // warm golden tan (richer gold; was .74/.62/.40)
        public static readonly Color SandHi = new Color(0.88f, 0.75f, 0.49f); // sunlit warm golden sand (was .82/.71/.47)
        public static readonly Color SandDamp = new Color(0.70f, 0.58f, 0.38f); // damp shore sand — kept WARM-golden (was .60/.50/.34 = too cool/dark)
        static readonly Color GrassLo = new Color(0.30f, 0.48f, 0.20f); // mid leaf green (more saturated)
        static readonly Color GrassHi = new Color(0.48f, 0.64f, 0.28f); // sunlit grass
        static readonly Color GrassRise = new Color(0.38f, 0.56f, 0.24f); // meadow rise
        // POND COLLAR RING (ticket 86cadj4g7 #130 ROUND 5 — Sponsor: "REMOVE the raised collar; paint a FLAT
        // darker-green ring on the terrain instead — I should walk on the green at ground level"). A darker
        // meadow green PAINTED into the terrain vertex colour around the pond bowl — NO raised geometry, NO
        // shadow lip. Clearly darker than the surrounding GrassLo/GrassHi so the collar reads as a distinct
        // green band; all channels well below 1.0 even under the warm key (intensity 1.25) so it CANNOT bloom
        // to the pale shoreline ring the OLD raised PondBank mesh produced (the #130 round-5 PROVEN white source:
        // toggle c removed the collar mesh → the pale ring vanished, build e5207d1).
        public static readonly Color PondCollarGreen = new Color(0.20f, 0.38f, 0.15f); // darker meadow collar
        // ROCK-AS-BOULDER soak-fix (86ca8m5zu, Sponsor soak 5f7e7ba: "items sticking up... all over").
        // The old warm-grey RockCol (0.55,0.52,0.47) silhouetted DARK under the Zone-D warm fog + grade at
        // small scatter size — a grey-blob spike read. Lifted to a warmer, LIGHTER sun-bleached stone so the
        // boulder reads as warm rock catching the key, not a dark shard: R>G>B warm lean, value pulled up so
        // it sits bright against the warm fog (0.80,0.80,0.74) instead of crushing to a dark silhouette.
        // Still sub-1.0 / HDR-clamp-safe (the per-instance *0.85..1.15 jitter in BuildRock keeps the top of
        // the band under 1.0). The board-v2 grey rocks (21h10_44) read as light warm stone, not charcoal.
        // v2 verify-capture + TINTDIAG instrumentation: the rocks read PINK because the coarse 12-step
        // material quantizer SPLIT the warm-grey ramp — R rounded UP across the 0.625 step while G/B rounded
        // DOWN, yielding (0.667, 0.583, 0.583) = R>G=B = a pink cast. The fix is NOT the base colour (any
        // base near 0.6 hits the same step boundary) — it's to quantize the ROCK tint on a FINER 24-step grid
        // (RockVertexColorMat -> QuantizeFine) that preserves the R>=G>=B warm-grey order. With this base on
        // the fine grid, TINTDIAG shows all tints stay warm-grey (no R>G=B pink), 4 distinct mats (low churn).
        static readonly Color RockCol = new Color(0.62f, 0.60f, 0.555f); // warm stone-grey (fine-quantized; no pink split)
        static readonly Color TrunkCol = new Color(0.42f, 0.30f, 0.19f); // warm bark
        static readonly Color LeafLo = new Color(0.26f, 0.42f, 0.20f);   // canopy shadow (legacy)
        static readonly Color LeafHi = new Color(0.44f, 0.58f, 0.28f);   // canopy lit (legacy)

        // ---- BLOB-CANOPY greens (board v2, ticket 86ca8ce7j) — the "3-4 green values per tree" rule.
        // Anchors from team/uma-ux/style-guide-v2.md §6 (derived by eye from inspiration/21h11_03 +
        // 21h10_44): vivid mid-green body, brighter top-lit green, deep shadow-side green. Saturated
        // but warm-leaning + sub-1.0 (HDR-safe). These are the multi-VALUE clustering that makes the
        // overlapping blobs read as foliage, not a single green ball. Tuned slightly warmer/softer than
        // the raw guide swatches so they sit in the warm Zone-D key without reading neon.
        static readonly Color CanopyBody   = new Color(0.30f, 0.58f, 0.24f); // vivid mid-green body
        static readonly Color CanopyTop    = new Color(0.48f, 0.74f, 0.34f); // bright top-lit green
        static readonly Color CanopyShadow = new Color(0.18f, 0.40f, 0.17f); // deep shadow-side green

        // ---- BUSH greens + BERRY red (ticket 86caa5zz3). The bush rides the SAME 3-value blob-green
        // language as the canopy (one idiom), pulled a touch more saturated/leafy so a low ground bush
        // reads distinct from a tree crown. The berry is a material-honest warm RED (the fruit reads as
        // the berry; no arbitrary tint — weapon/asset material-honest memory) — board v2 nature sheets
        // (21h12_49) show small red berries/fruit. Sub-1.0 (HDR-clamp-safe under the Zone-D post stack).
        static readonly Color BushBody   = new Color(0.26f, 0.52f, 0.22f); // leafy mid-green bush body
        static readonly Color BushTop    = new Color(0.44f, 0.70f, 0.30f); // bright top-lit leaf
        static readonly Color BushShadow = new Color(0.15f, 0.35f, 0.15f); // deep shadow-side leaf
        static readonly Color BerryRed   = new Color(0.78f, 0.16f, 0.22f); // warm berry red (material-honest)

        // ---- BEACH OCEAN water gradient (drew/beach-water-scene; Uma beach-water-direction §1).
        // Toy-bright teal that catches the sun — calm/inviting, NOT a realistic reflective shader-ocean.
        // The gradient is baked per-VERTEX (near-shore bright -> seaward deeper) and rides the existing
        // FarHorizon/LowPolyVertexColor shader, so the water reads SMOOTH against the FACETED shore
        // (Uma: the contrast is what makes the coast pop). All channels sub-1.0 — the Zone-D post stack
        // (bloom + warm grade + postExposure) compounds bright values; sub-1.0 survives without blooming
        // to white (the pale-shore first-frame lesson). Anchors derived by eye from inspiration/21h16_52
        // (lake-cabin) + Tess-QA-pinnable values in the brief's §1 color table. ----
        // BRIGHTENED + saturated (drew/ocean-camera-fix, 2026-06-13). The first soak-fix anchors
        // (#3FA6B0 shallow / #2E7E96 deep) shipped technically-teal but read as a PALE GREY strip in the
        // shipped seaward gameplay view (pixel-sampled the capture: the visible far water came out
        // (0.66,0.62,0.44) — warm grey-yellow, NOT teal). Root cause (trace + math): the visible water is
        // FAR (the near bright band is occluded by the beach crest), so it gets (a) the warm directional
        // key + warm SH ambient in the LowPolyVertexColor shader, (b) the warm distance fog (0.80,0.80,
        // 0.74), and (c) the warm post grade (WhiteBalance +12, warm colour filter) — three warm washes
        // that desaturate the original mid-value teal to grey. Pushing the teal BRIGHTER and more
        // saturated (higher G+B, B>G slightly, low R) makes it survive all three and land visibly teal in
        // the SHIPPED frame. Still sub-1.0 every channel (HDR-clamp-safe — verified against the post stack
        // in the shipped seaward capture, not just the editor).
        // NEAR-BAND SATURATION LIFT (drew/ocean-beach-soakfix2, 2026-06-13). Sponsor soaked main and
        // flagged the NEAR band as a PALE SKY-CYAN, not a believable teal sea. Pixel-sampled the shipped
        // seaward capture (ci-out/sea-caps/sea_seaward.png, stamp 3a548a7) to confirm + diagnose:
        //   near-band albedo (56,219,242)  ->  on-frame (148,164,145)   [a pale grey-green]
        //   teal saturation G+B-2R:  albedo 349  ->  on-frame 13  (~96% of the chroma destroyed)
        // Per-channel the post stack pushes R UP ~+90 (warm directional key * ndotl + warm SH ambient +
        // warm fog 0.80/0.80/0.74 + warm grade), and crushes B DOWN ~-97. The PRIOR fix chased this by
        // pushing the albedo BRIGHTER + more B-dominant (0.22/0.86/0.95) — exactly backwards: a
        // TOP-of-band B-dominant cyan is what BLOOMS to pale grey under the Zone-D bloom + warm grade, so
        // brighter made it MORE washed-out, not more teal. The lever is the OPPOSITE: DEEPEN (pull the
        // value down off the top of the band so bloom adds rather than blows out) + SATURATE (lower R hard
        // so the warm push lands controlled, keep G/B mid-high with B>=G so the warm-B-crush still leaves
        // a clean GREEN-LEANING teal). Tuned empirically against the shipped seaward capture (N>=2 builds,
        // pixel-sampled the near band each time) to land on-frame ~(70,160,165) — the saturated mid-value
        // teal the inspiration/21h16_52 lake + 21h16_13 river read at, NOT a pale near-white cyan. Still
        // sub-1.0 every channel (HDR-clamp-safe). The FAR band (WaterDeep) + foam are unchanged — this is
        // a near-band saturation lift, not a re-tune of the whole sea (AC1 scope).
        public static readonly Color WaterShallow = new Color(0.10f, 0.62f, 0.66f); // deep saturated near-shore teal
        public static readonly Color WaterDeep    = new Color(0.10f, 0.50f, 0.60f); // deeper seaward teal-blue (far band)
        // Foam edge line baked into the beach mesh's seaward-most rows (Uma §2). Warm off-white, sub-1.0
        // (NOT pure white — would bloom). Exposed for the scene-presence test's color-pin check.
        public static readonly Color FoamEdge     = new Color(0.91f, 0.89f, 0.82f); // #E8E2D0 warm foam

        // FRESHWATER POND colours (ticket 86caamkv7 / Uma §1a). The pond reads as DIFFERENT water from the
        // salt sea by CONTRAST: a cooler, brighter, BLUER freshwater cyan vs the sea's warm-leaning teal — the
        // single fastest "this is drinkable fresh water" signal at orbit distance. Its OWN two constants (the
        // sea's WaterShallow/WaterDeep are coast-tuned + Sponsor-soaked — DO NOT retune them). The freshwater
        // tell is B > G (the pond leans BLUE); the sea keeps G >= B (teal-green). Sub-1.0 every channel
        // (HDR-clamp-safe, same convention the sea follows). Gradient runs bank(shallow) -> centre(deep).
        // #130 ROUND 5: the OLD bright PondShallow (0.22,0.66,0.74) read PALE-CYAN at the grazing gameplay-orbit
        // pitch (the bright G+B caught the warm key on the far rim) — a light patch the Sponsor could still read
        // as "too white" even after the collar-ring fix. Per the dispatch Issue-3 contingency ("make the pond
        // uniformly fresh-blue with NO bright shallow rim — drop the rim below the bloom threshold / flatten the
        // gradient"), PondShallow is pulled DOWN toward PondDeep: a calmer fresh blue, still a CLEAR B>G freshwater
        // tell (0.70>0.50) but no longer bright/cyan, so the whole disc reads UNIFORMLY fresh-blue (no pale rim).
        public static readonly Color PondShallow = new Color(0.16f, 0.50f, 0.72f); // bank-edge fresh water: a calm fresh blue (B>G), NOT bright cyan (#130 round 5)
        public static readonly Color PondDeep    = new Color(0.12f, 0.42f, 0.68f); // pool centre: deeper cool blue (B > G = the freshwater tell)

        // Result of building the low-poly zone: the ground GameObject (Ground-layered, NavMesh +
        // raycast surface) so the caller can parent scatter + bake NavMesh over it.
        public class ZoneResult
        {
            public GameObject root;        // zone container
            public GameObject ground;      // the terrain mesh (on Ground layer, has MeshCollider)
        }

        // ============================================================================================
        // BIG ROUND ISLAND (ticket 86ca9a7qn — Sponsor: "make the island much much bigger, round, with
        // water on all sides. ... quite big with elevation (hills). dense forest/jungle with high trees").
        // ============================================================================================
        // THE NEW WORLD BASIS. The old beach-to-meadow STRIP (a 1D Z-profile over an X±45 / Z -12..56
        // rectangle, water on the −Z side only) is replaced by a RADIAL-FALLOFF round landmass centred at
        // the world origin (= the spawn + survival-loop centre), with water on ALL sides. The mountains
        // move OFF the main island onto their own distant islands ringing the sea (WorldBootstrap.BuildVista,
        // pushed clear of the bigger island).
        //
        // RADIAL HEIGHT MODEL (HeightAtRadial below):
        //   r = distance from origin in the XZ plane.
        //   - r <  IslandCoreR  : the land PLATEAU (full inland height + hills).
        //   - IslandCoreR..IslandShoreR : a smooth falloff ring (the land slopes down to the coast).
        //   - r ~  IslandShoreR : the WATERLINE (the radial coast — true round, water on all sides).
        //   - r >  IslandShoreR : the seabed drops below sea level (the surrounding sea).
        // Hills are multi-octave Perlin noise added over the plateau, amplitude ramped DOWN toward the
        // coast (a clean waterline) and UP inland (real elevation). The result is a believable round
        // island with rolling hills, not a flat disc.
        //
        // SIZE: IslandShoreR 120u → ~240u diameter, MUCH bigger than the old ~90×68 strip (bold default
        // per the brief — the Sponsor dials from the capture). The terrain mesh is a square grid covering
        // the island disc + a sea margin; the water plane covers well past it to the fog horizon.
        public const float IslandCoreR  = 78f;   // inside this radius = the land plateau (full hills)
        public const float IslandShoreR = 120f;  // the MEAN radial waterline (the coast) — ~240u diameter. The
                                                 // ACTUAL coast is AZIMUTH-WARPED around this (ShoreRadiusAt).
        public const float IslandFalloffEnd = 150f; // seabed has fully dropped below the sea by here (past the warped coast)
        // The inland BASE surface sits at ~Y0 (so it agrees with the spawn at Y0, the survival-loop objects,
        // and the flat NavMesh proxy at the centre). HILLS rise ABOVE this base; the coast falls BELOW it.
        public const float IslandPlateauH = 0.15f; // base inland land height just above the sea (hills rise from here)
        public const float IslandHillAmp = 9.0f;   // peak hill amplitude inland (real elevation / hills — Sponsor)
        public const float IslandSeabedDrop = -9f; // how far the seabed sinks past the shore (well below WaterY)
        // The terrain grid covers the disc + a sea margin so the round coast reads against open water on
        // every side (the grid is square; the ROUNDNESS comes from the radial height, not the grid shape).
        public const float IslandGridHalf = 165f;  // square terrain grid half-extent (covers shore 120 + margin)
        // COAST-POLISH (86ca9xyqa AC#6b — SMOOTH the JAGGED waterline): up 150 -> 200. The waterline staircase
        // is the terrain↔water intersection traced along the grid; 150 over 330u = 2.2u cells = visible steps.
        // 200 = 1.65u cells (~25% finer steps), so the coastline reads smoother — paired with the wider smooth
        // foam band (BuildIslandWater / IslandColorAt) which traces a soft curve OVER the residual steps. Still
        // < 65000 verts (UInt16-safe; the mesh also handles UInt32). The finer grid is the cheap half of the
        // fix; the smooth foam band is the half that actually makes the eye read a wave, not a staircase.
        public const int IslandSegX = 200, IslandSegZ = 200; // subdivision across the big disc (smooth slopes +
                                                       // a SMOOTHER irregular coast/clip + NavMesh) — up from 150.
                                                       // PUBLIC so the clip-proof test reads the live count (no
                                                       // stale hardcoded 150 when this is re-tuned).

        // ============================================================================================
        // ORGANIC IRREGULAR ISLAND shape knobs (ticket 86ca9qwr3 — Sponsor: "read as a REAL island, not a
        // disc"). The coast is the MEAN IslandShoreR WARPED by azimuth noise (so it's irregular, not a
        // circle); some coastal sectors are flat SAND BEACHES, others are steep rock CLIFFS; the interior
        // stays near grass level out to the coast (no walking down a dome). These are NAMED so the chosen
        // SEED's look is easy to re-bake / re-dial (AC6 — give-him-the-knob). The seed drives the azimuth
        // noise phase, so a different seed = a different (but same-character) island outline.
        // ============================================================================================
        // AC1 — coastline irregularity. The shore radius is warped by 2-octave azimuth noise of this
        // amplitude (peak ±u off the mean coast). Bigger = more bays/headlands; smaller = rounder. The warp
        // samples Perlin on a CIRCLE of radius CoastNoiseRadius — bigger radius = the azimuth sweep crosses
        // more noise cells = a genuinely irregular (not near-circular) coast.
        public const float CoastIrregAmp = 26f;        // ±u the coast wanders off the mean (bays + headlands)
        public const float CoastNoiseRadius = 2.0f;    // circle radius the azimuth noise samples. SMALL = the
                                                       // azimuth sweep crosses FEW noise cells = BROAD lazy bays
                                                       // (3-5 lobes). Large radius made a high-freq spiky urchin.
        // AC2 — cliff vs beach. A separate azimuth field selects CLIFF sectors: where it exceeds
        // (1 - CliffFraction) the coast is a steep rock cliff; elsewhere a flat sand beach. The fraction is
        // the share of the coastline that is cliff (the rest beach). Cliff sectors drop ~vertically; beach
        // sectors are a flat sand strip at ~grass level.
        public const float CliffFraction = 0.42f;      // ~42% of the coast is cliff, the rest sand beach
        public const float CliffNoiseRadius = 2.4f;    // circle radius the cliff/beach noise samples. SMALL = a
                                                       // few BROAD cliff/beach STRETCHES around the coast (not
                                                       // rapid alternation) — believable headlands vs bays.
        public const float CliffDropDepth = 7.5f;  // how far a cliff face plunges below the shore lip (steep)
        // AC3 — beach width. The flat sand strip (beach sectors) spans this many u inland of the waterline,
        // staying ~grass level — NOT a downhill ramp. Inland of it the land is grass at ~plateau level.
        // COAST-POLISH (86ca9xyqa AC#5b — WIDER beach): widened 9 -> 16u so the warm sand band reads as a
        // real beach between the grass and the water at the orbit distance (the 9u strip was a thin line).
        public const float BeachWidth = 16f;       // u of flat sand strip inland of the waterline (beach sectors)
        // COAST-POLISH (86ca9xyqa AC#5a — WATER MEETS THE SAND): the WET SHELF. The beach now dips BELOW WaterY
        // over this seaward-most band so the flat water plane LAPS UP onto the sand (no dry knife-edge gap — the
        // prior profile only reached WaterY exactly AT the coast radius, so the water met a single radius and
        // left a dry slope above it). The shelf is shallow (a few cm below the surface) so the water covers it
        // as a thin wet band, and the foam rides this lapped zone. Keeps WaterY itself UNCHANGED (OOS: the sea
        // read elsewhere) — only the BEACH geometry is lowered into the water at the waterline.
        public const float WetShelfWidth = 4.5f;   // u of beach that sits just BELOW WaterY (water laps onto it)
        public const float WetShelfDepth = 0.12f;  // how far below WaterY the inner wet-shelf edge sits (shallow)
        // AC1 — terrain mesh CLIP. Grid cells whose radius exceeds the warped coast by more than this margin
        // are dropped (no triangles) so NO straight square grid edge reads from overhead — only the irregular
        // landmass + a thin sea-shelf skirt remains; the big water plane fills the rest to the fog horizon.
        public const float IslandClipMargin = 14f; // keep a shelf this far past the warped coast, then clip
        // AC6 — the DEFAULT island shape seed (the Sponsor's pick, set here once chosen). WorldBootstrap bakes
        // this unless overridden with `-islandSeed N`. A different seed re-rolls the coast/cliff layout (same
        // character). The 3-4 seed VARIANTS captured for the Sponsor pick the value baked here.
        public const int IslandSeed = 42;          // Sponsor's pick from the seed-variant captures (86ca9qwr3)

        // ============================================================================================
        // FRESHWATER-POND BOWL DEPRESSION (ticket 86cadj4g7 — Sponsor #130 re-soak: "the pond is an ELEVATED
        // LIP, not a depression; carve a recessed BOWL"). REPLACES the old WorldBootstrap.RegroundFreshwaterPond
        // LIFT (which raised the water disc +0.10u ABOVE the terrain → the darker-green collar read as a raised
        // lip casting a shadow, and the water read flush, NOT sunken). Instead we CARVE the heightfield down at
        // the pond so the water sits in a visibly recessed bowl: the bowl FLOOR below the water surface, the
        // collar SLOPING DOWN into it (flush, no lip). Because HeightAtRadial is the SINGLE source of truth for
        // terrain height, the carve flows automatically into (a) the visible terrain mesh, (b) its MeshCollider,
        // and (c) the NavMesh bake — so the player NAVIGATES INTO the bowl and stands KNEE-DEEP (the agent Y
        // follows the carved floor; the water surface is above it). This solves the occlusion NATURALLY (the
        // water sits in a carved low spot — no lift needed). The carve is a LOCAL deliberate dip at the pond
        // ONLY; outside PondBowlOuterRadius the height is UNCHANGED, so the seed-42 island silhouette / scatter /
        // NavMesh elsewhere are untouched (asserted by the non-regression tests).
        // ============================================================================================
        // The pond centre in world XZ — MUST match MovementCameraScene.PondPosition (7,0,-3). The carve is
        // centred here so the bowl sits exactly under the authored pond water disc + bank.
        public const float PondCenterX = 7f;
        public const float PondCenterZ = -3f;

        // ===== RECESS GEOMETRY (ticket 86cadj4g7 — Sponsor #130 re-soak: "the pond is on a little hill;
        // recess it DOWN INTO the ground so the green collar sits at the SAME LEVEL as the surrounding
        // terrain — I should walk on the green, not inside it") ===========================================
        // The PRIMARY tunable is now the RECESS — how far the WATER SURFACE sits BELOW the surrounding ground
        // plateau. The earlier build recessed the water only ~0.10u below the plateau (FloorDrop 0.55 − wade
        // 0.45), so the green-rimmed pool read as a RAISED LENS/MOUND, not a sunk pool. This decouples the
        // RECESS (how deep the pool reads below ground) from the WADE depth (how deep the player stands in the
        // water once on the floor): the bowl FLOOR is carved RECESS + WADE below the plateau, so the water
        // surface (floor + wade) lands exactly RECESS below the plateau. The Sponsor soaked the recess dial
        // (PondNudge PgUp/PgDn) on build 1a3a427 and CHOSE DEEPER (0.75u below ground) — BAKED here as the new
        // default (#130 third re-soak). The live PondRecessNudge handle stays (PgUp/PgDn) for any future dial.
        public const float PondRecessKneeDeep = 0.75f;   // DEFAULT recess: water surface 0.75u BELOW the plateau (Sponsor's chosen DEEPER, #130 re-soak)
        // The WADE depth: how deep the player standing on the bowl FLOOR is submerged (water surface − floor).
        // Castaway knee ≈ 0.45u. Mirrored in WorldBootstrap.PondWaterDepthAboveFloor + MovementCameraScene.
        public const float PondWadeDepth = 0.45f;
        // How far the bowl FLOOR sits below the LOCAL (pre-carve) plateau = RECESS + WADE. With the DEEPER
        // recess (0.75, the Sponsor's chosen #130 re-soak value) + wade (0.45) the floor is carved 1.20u down,
        // the water surface lands 0.75u below the plateau (a clearly sunk pool), and the player on the floor
        // stands knee-deep. (Was 0.90 at the knee-deep 0.45 recess; the Sponsor dialed DEEPER on 1a3a427.)
        public const float PondBowlFloorDrop = PondRecessKneeDeep + PondWadeDepth; // 1.20
        // The flat-ish bowl FLOOR extends to this radius from the pond centre — covers the whole organic water
        // disc (nominal 2.6u × up to +18% rim ≈ 3.07u) so the floor is below the water everywhere the disc
        // shows (the player stands knee-deep anywhere in the pool, not just dead centre). Just under the disc
        // rim so the wall begins right at the disc edge (no wide exposed flat floor outside the disc).
        public const float PondBowlInnerRadius = 3.0f;
        // The bowl WALL slopes from the floor back up to UNDISTURBED plateau by this radius. Sized so the wall
        // stays GENTLE: the deeper 1.20u FloorDrop over the (5.4-3.0)=2.4u run → steepest ~37° (smoothstep
        // peaks 1.5× its average: atan(1.5·1.20/2.4)=36.9°), still WELL under the NavMesh agent's 45° max →
        // the bake covers the bowl floor + walls so the player can wade in. WIDENED 4.8 → 5.4 to keep the
        // steepest wall < 40° now that the bowl is DEEPER (0.75 recess; #130 re-soak) — keep outer−inner ≳
        // 1.5×floorDrop/tan40° (=2.14) so the steepest point stays < 40° (the PondBowl_WallSlope test pins this).
        public const float PondBowlOuterRadius = 5.4f;

        // SEA-HOLE CUT RADIUS (ticket 86cadj4g7 #130 ROUND 7). The sea plane is HOLED over the pond footprint so
        // it can never render through the carved bowl (the PROVEN #130 white-ring source). ROUND 6 dropped only
        // tris whose CENTROID was within PondBowlOuterRadius — but the open-sea grid is COARSE (WaterSeg 160 over
        // 1400u → ~8.75u cells, each tri LARGER than the whole bowl), so a tri whose centroid sat just OUTSIDE
        // 5.4u still BLANKETED the bowl from one side: a sea SLIVER survived at the waterline on the lobe
        // (crescent) azimuths → 0.182 pale fraction at rNorm 0.30 (down from 0.215 but not 0). FIX (round 7):
        // (1) drop a tri if the CLOSEST POINT on the triangle to the pond centre is within the cut radius — an
        //     overlap test that catches edge/vertex slivers a centroid test is blind to (a tri straddling the
        //     bowl mouth has all 3 verts outside yet an edge crossing the footprint); and
        // (2) cut at PondBowlOuterRadius + a half sea-cell-diagonal of MARGIN so the overlap test has slack for
        //     the organic rim lobes + the coarse grid's azimuth-dependent alignment (the crescent showed on ONE
        //     side = an asymmetric grid straddle). The closest-point overlap test ALONE already guarantees no
        //     retained tri reaches within the cut radius; the margin just sets that radius safely past the
        //     waterline annulus (world ~3.4u) and the bowl mouth (5.4u). Still INLAND (pond centre is ~107u
        //     inside the coast) + fully ringed by terrain above WaterY past PondBowlOuterRadius → the enlarged
        //     hole is invisible (no sea-gap), it only removes the intruding-through-bowl tris. The salt sea
        //     elsewhere (all coasts) is UNCHANGED. Computed from the grid constants so it tracks any future
        //     WaterSeg / extent change. One sea cell = (WaterHalfExtent*2)/WaterSeg = 8.75u; half its diagonal ≈ 6.19u.
        public static float SeaCellSize => (WaterHalfExtent * 2f) / WaterSeg;
        public static float SeaHoleCutRadius => PondBowlOuterRadius + SeaCellSize * Mathf.Sqrt(2f) * 0.5f;

        // ===== POND FOAM LEVELS (ticket 86cadj4g7 — Sponsor #130: "the pond should NOT foam like the sea") ==
        // The pond's _FoamDistance on the FarHorizon/LowPolyWater shader (foam = saturate(1 - gap/distance)).
        // Three discrete levels the live PondNudge [foam] handle cycles; the DEFAULT is OFF (a still pool).
        public const float PondFoamOff     = 0.0f;   // DEFAULT: no foam ring — a still glassy fresh pool (#130)
        public const float PondFoamLight   = 0.06f;  // a thin damp line hugging the bank only (≪ the 0.45u wade depth so the open disc never floods)
        public const float PondFoamSeaLike = 1.5f;   // the sea's foam band width (for A/B only — reads wrong on a pond, but the Sponsor can see the contrast)
        // The MASTER foam-amount gate (ticket 86cadj4g7 #130 re-soak — _FoamAmount on LowPolyWater.shader). OFF
        // (0) zeroes the WHOLE foam term incl. the gap≈0 shoreline razor line that _FoamDistance=0 alone left;
        // ON (1) lets _FoamDistance govern the band width. PondFoamAmountFor maps a foam STEP to its amount.
        public const float PondFoamAmountOff = 0.0f;  // master OFF: zero foam anywhere (kills the shoreline ring)
        public const float PondFoamAmountOn  = 1.0f;  // master ON: foam present, width per _FoamDistance
        /// <summary>The master _FoamAmount for a given pond foam _FoamDistance: 0 (off) when the distance is the
        /// OFF level, else 1 (the light/sea-like steps want foam, width per their distance). Keeps PondNudge +
        /// MakePondMaterial in lockstep so a foam step drives BOTH _FoamDistance and _FoamAmount consistently.</summary>
        public static float PondFoamAmountFor(float foamDistance) =>
            foamDistance <= PondFoamOff + 1e-4f ? PondFoamAmountOff : PondFoamAmountOn;

        /// <summary>
        /// The pond-bowl depression DELTA (≤ 0, a downward carve) at world XZ — the local recess that turns the
        /// flat pond plateau into a recessed BOWL (ticket 86cadj4g7). 0 outside PondBowlOuterRadius (the island
        /// elsewhere is UNCHANGED — the seed-42 silhouette lock), eases down across the wall (smoothstep, a
        /// gentle slope the NavMesh covers), and reaches the full −PondBowlFloorDrop on the flat floor inside
        /// PondBowlInnerRadius (below the water disc → knee-deep wade-in). Pure function of XZ (deterministic,
        /// byte-stable capture). PUBLIC so the bowl-geometry + NavMesh-coverage tests sample it directly.
        /// </summary>
        public static float PondDepressionDelta(float wx, float wz)
        {
            float dx = wx - PondCenterX, dz = wz - PondCenterZ;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d >= PondBowlOuterRadius) return 0f;            // undisturbed terrain past the bowl mouth
            if (d <= PondBowlInnerRadius) return -PondBowlFloorDrop; // the flat bowl floor (full depth)
            // The wall: ease from the floor (full depth at the inner radius) up to 0 at the outer radius. A
            // smoothstep gives a soft basin lip (no hard crease) that the welded terrain + RecalculateNormals
            // read as a smooth dip, and that the NavMesh voxelizer resolves as one continuous walkable slope.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PondBowlInnerRadius, PondBowlOuterRadius, d));
            return -PondBowlFloorDrop * (1f - t);
        }

        // ===== POND-RIM HILL FLATTEN (ticket 86cadj4g7 #130 re-soak — the ASYMMETRIC-MOUND root cause) =========
        // DIAGNOSIS (diagnose-via-trace, confirmed numerically): the bowl carve is a FIXED −PondBowlFloorDrop and
        // GroundPondInBowl raycasts only the CENTRE floor, but the multi-octave Perlin HILLS (IslandHillAmp 9.0)
        // still contribute up to ~0.54u of elevation INSIDE the pond footprint (the spawn-flatten holds hillDamp
        // at 0.06 here, but 0.06·9.0·1 ≈ 0.54u) — and that hill height VARIES azimuthally across the 9.6u-wide
        // footprint. So the surrounding RIM terrain is NOT a uniform plateau: on an azimuth where the rim's hill
        // sits >PondWadeDepth (0.45u) BELOW the centre's hill, the centre-grounded water surface lands ABOVE that
        // rim → a one-sided MOUND (the #130 capture: pond_b yaw=70° bulged, pond_c yaw=-70° read sunk = ASYMMETRY).
        // FIX (cause, not metric): flatten the HILL contribution to a single uniform value across the pond
        // footprint so the rim is one consistent plateau on EVERY azimuth; then the fixed carve + centre-grounding
        // puts the water below the rim on all sides — a true hole. The hills fade back in across a short collar
        // band [PondBowlOuterRadius, PondBowlOuterRadius + PondRimFlattenFade] so there is NO hard crease at the
        // mouth, and the island is byte-IDENTICAL beyond that band (seed-42 silhouette lock — the flatten is 1.0
        // there, a no-op). The fade band is the ONLY new terrain delta vs c7da32d outside the bowl; it is local,
        // small, and tracked by the non-regression test (zero at FarAway, full flatten inside the mouth).
        public const float PondRimFlattenFade = 3.0f; // u of collar over which the hills fade back in past the bowl mouth

        /// <summary>
        /// Hill-LEVELLING blend weight at world XZ for the pond footprint (1 = full per-vertex hills, 0 = the hill
        /// height is fully levelled toward FootprintHillLevel — the LOCAL surrounding ground level). 0 inside
        /// PondBowlOuterRadius (the footprint is a uniform plateau AT the local ground level → no asymmetric mound
        /// AND no raised fade-ramp rim), eases to 1 over the fade collar, and is EXACTLY 1 beyond it (the island is
        /// byte-unchanged — seed-42 lock). PUBLIC so the flush-rim non-regression tests sample it. Pure function of
        /// XZ (deterministic; byte-stable capture). NOTE: at weight 0 the footprint is NOT pulled to baseH=0.15 (the
        /// earlier defect that left the surrounding hills rising above it as a shadow-casting rim) — it is levelled
        /// to FootprintHillLevel so the footprint sits AT the local ground and the fade band is nearly flat.
        /// </summary>
        public static float PondHillFlatten(float wx, float wz)
        {
            float dx = wx - PondCenterX, dz = wz - PondCenterZ;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d <= PondBowlOuterRadius) return 0f;                              // inside the bowl mouth: levelled
            float fadeEnd = PondBowlOuterRadius + PondRimFlattenFade;
            if (d >= fadeEnd) return 1f;                                          // beyond the collar: full hills (island unchanged)
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PondBowlOuterRadius, fadeEnd, d));
        }

        // ===== POND COLLAR PAINT (ticket 86cadj4g7 #130 ROUND 5) — replaces the removed raised PondBank mesh =====
        // The Sponsor's redesign: REMOVE the raised collar ring mesh (the PROVEN white-shoreline-ring source —
        // its draped wall facets read pale/washed under the warm key) and PAINT a FLAT darker-green ring directly
        // into the terrain vertex colour around the pond bowl, so the player walks on green AT GROUND LEVEL with
        // no raised geometry + no shadow lip. The band covers the bowl WALL + MOUTH (where the old collar mesh
        // sat) plus a short fade past the mouth so the darker green eases into the surrounding grass with no hard
        // seam. Inner edge starts at the water rim (PondBowlInnerRadius — under the disc, never seen) so the
        // green reads as the bank framing the pool. ZERO past the fade so the seed-42 grass elsewhere is
        // UNCHANGED. Pure function of XZ (deterministic; byte-stable capture).
        public const float PondCollarPaintFade = 1.4f; // u past the bowl mouth over which the darker green eases into grass
        /// <summary>
        /// The pond-collar paint WEIGHT at world XZ (0 = pure surrounding grass, 1 = full PondCollarGreen). 1 across
        /// the bowl wall + mouth band [PondBowlInnerRadius, PondBowlOuterRadius], eases to 0 over a short fade past
        /// the mouth, EXACTLY 0 beyond it (seed-42 grass unchanged) AND 0 well inside the inner radius (under the
        /// water disc — no need to paint where the water hides it). PUBLIC so the collar-paint guard samples it.
        /// </summary>
        public static float PondCollarPaintWeight(float wx, float wz)
        {
            float dx = wx - PondCenterX, dz = wz - PondCenterZ;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float fadeEnd = PondBowlOuterRadius + PondCollarPaintFade;
            if (d >= fadeEnd) return 0f;                                  // beyond the fade: pure grass (seed-42 lock)
            if (d <= PondBowlInnerRadius) return 1f;                      // the bowl wall under/around the rim: full collar
            if (d <= PondBowlOuterRadius) return 1f;                      // wall + mouth band: full collar green
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PondBowlOuterRadius, fadeEnd, d)); // ease into grass
        }

        /// <summary>
        /// The damped multi-octave HILL height at world XZ (factored out of HeightAtRadial so the pond footprint
        /// levelling can sample the SAME hill field). Includes the landMask² interior-only fade and the spawn-
        /// flatten near-origin damping (0.06 within ~16u, easing to full by ~32u). <paramref name="landMask"/> and
        /// <paramref name="r"/> are passed in (the caller already computed them) so this is a pure arithmetic
        /// helper, not a re-derivation. Deterministic → byte-stable capture.
        /// </summary>
        public static float HillHeightAt(float wx, float wz, float ox, float oz, float landMask, float r)
        {
            float hill = (Mathf.PerlinNoise(ox + wx * 0.012f, oz + wz * 0.012f) - 0.5f) * 2f * 0.62f
                       + (Mathf.PerlinNoise(ox + wx * 0.030f, oz + wz * 0.030f) - 0.5f) * 2f * 0.28f
                       + (Mathf.PerlinNoise(ox + wx * 0.075f, oz + wz * 0.075f) - 0.5f) * 2f * 0.10f;
            float hillH = (hill * 0.5f + 0.5f) * IslandHillAmp * landMask * landMask;
            // SPAWN-FLATTEN: keep the immediate spawn + loop centre gentle (loop objects sit level, click-move
            // clean). Holds out to ~16u then eases the hills in over the next ~16u.
            float spawnFlat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 32f, r));
            return hillH * (0.06f + 0.94f * spawnFlat);
        }

        /// <summary>
        /// The single UNIFORM hill height the pond footprint is levelled to (ticket 86cadj4g7 #130 re-soak — the
        /// elevated-collar/berm fix). = the average damped hill height around a representative RING just OUTSIDE
        /// the bowl mouth, so the levelled footprint plateau sits at the LOCAL surrounding ground level (not the
        /// bare baseH). Averaging over azimuth gives ONE deterministic value (kills the asymmetric mound) that
        /// tracks the actual local terrain (kills the raised fade-ramp rim) — the two #130 defects reconciled.
        /// The ring is at PondBowlOuterRadius + half the fade (~6.3u): inside the spawn-flat zone (r&lt;16 from
        /// origin) where the pond sits, this samples the same damped-hill regime the surrounding grass shows.
        /// Pure function of the seed offset (no per-vertex term) → ONE constant per island, byte-stable.
        /// </summary>
        public static float FootprintHillLevel(float ox, float oz)
        {
            // Sample the damped hill on a ring at the fade midpoint, averaged over azimuth → the representative
            // local ground level the footprint should match. landMask is 1 here (the pond is deep interior, r≈7.6
            // ≪ IslandCoreR=78). r (from world origin) is recomputed per sample for the spawn-flatten damping.
            const int samples = 16;
            float ringR = PondBowlOuterRadius + PondRimFlattenFade * 0.5f;   // ~6.3u from the pond centre
            float sum = 0f;
            for (int i = 0; i < samples; i++)
            {
                float a = i / (float)samples * Mathf.PI * 2f;
                float wx = PondCenterX + Mathf.Cos(a) * ringR;
                float wz = PondCenterZ + Mathf.Sin(a) * ringR;
                float rFromOrigin = Mathf.Sqrt(wx * wx + wz * wz);
                sum += HillHeightAt(wx, wz, ox, oz, 1f, rFromOrigin);
            }
            return sum / samples;
        }

        /// <summary>
        /// Build a low-poly smooth-shaded zone: a height-varied terrain MESH (dunes near the shore
        /// rising to a gentle meadow inland), vertex-color gradient material (beach->field ramp by
        /// height + Z), low-poly mesh scatter (trees / rocks / grass clumps), and a simple water plane
        /// at the beach edge. Smooth normals via welded grid + RecalculateNormals.
        ///
        /// PORT NOTE: the spike's per-edge SEAM-FLATTENING (pulling X-edge columns to y=0 so a flat
        /// NavMesh connector could mate the side-by-side zones) is INTENTIONALLY DROPPED here — there
        /// is no neighbouring zone to stitch to in the single production play space, so the interior
        /// dunes + meadow rise are the whole surface. Everything else is carried verbatim.
        /// </summary>
        public static ZoneResult BuildZone(
            GameObject parent, string zoneId, float minX, float maxX,
            float shoreZ, float inlandFarZ, int seed, Material vertexColorMat, Material waterMat)
        {
            // BIG ROUND ISLAND (86ca9a7qn): the strip params (minX/maxX/shoreZ/inlandFarZ) are now the
            // LEGACY signature; the round island is sized by the Island* constants and centred at origin.
            // (Caller WorldBootstrap still passes the old extents — they're ignored for the island shape but
            // kept so the signature + any other call site stay source-compatible.)
            var root = new GameObject("Zone_" + zoneId);
            root.transform.SetParent(parent.transform, false);

            var ground = BuildIslandTerrainMesh(root, "Ground_" + zoneId, vertexColorMat, seed);

            var scatterRoot = new GameObject("LowPolyScatter");
            scatterRoot.transform.SetParent(root.transform, false);
            ScatterIslandProps(scatterRoot, seed, ground.GetComponent<MeshCollider>());

            BuildIslandWater(root, "Water_" + zoneId, waterMat, seed);

            return new ZoneResult { root = root, ground = ground };
        }

        // ============================================================================================
        // BIG ROUND ISLAND terrain mesh (86ca9a7qn). A square welded grid covering the island disc + a sea
        // margin; the ROUND landmass comes from the RADIAL height field (HeightAtRadial), not the grid shape.
        // WELDED + RecalculateNormals = the smooth-shaded low-poly look (averaged vertex normals).
        // ============================================================================================
        static GameObject BuildIslandTerrainMesh(GameObject parent, string name, Material mat, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = Vector3.zero; // island centred at origin (= spawn / loop centre)
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) go.layer = groundLayer; // -1 if the project lacks a Ground layer

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            SeedOffset(seed, out float ox, out float oz);

            int vCount = (IslandSegX + 1) * (IslandSegZ + 1);
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            float size = IslandGridHalf * 2f;
            for (int z = 0; z <= IslandSegZ; z++)
            for (int x = 0; x <= IslandSegX; x++)
            {
                int i = z * (IslandSegX + 1) + x;
                float fx = (float)x / IslandSegX, fz = (float)z / IslandSegZ;
                float wx = (fx - 0.5f) * size;    // world X (local == world; root at origin)
                float wz = (fz - 0.5f) * size;    // world Z
                float h = HeightAtRadial(wx, wz, ox, oz);
                verts[i] = new Vector3(wx, h, wz);
                cols[i] = IslandColorAt(wx, wz, h, ox, oz, seed);
            }

            // AC1 — CLIP the terrain to the irregular landmass (+ a sea-shelf skirt). A grid cell is emitted
            // ONLY if at least one of its 4 corners is within (warped coast + IslandClipMargin). Cells fully
            // in the deep sea are DROPPED so NO straight square grid edge reads from overhead — only the
            // organic landmass + a thin underwater shelf remain; the big water plane fills the rest to the
            // fog horizon. (The skirt keeps the coast meeting submerged geometry, never a floating land edge.)
            var tris = new List<int>(IslandSegX * IslandSegZ * 6);
            for (int z = 0; z < IslandSegZ; z++)
            for (int x = 0; x < IslandSegX; x++)
            {
                int i = z * (IslandSegX + 1) + x;
                if (!CellNearLandmass(verts, i, IslandSegX, ox, oz)) continue; // clip deep-sea cells
                tris.Add(i); tris.Add(i + IslandSegX + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + IslandSegX + 1); tris.Add(i + IslandSegX + 2);
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = vCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); // welded grid -> averaged smooth normals (low-poly smooth shading)
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh; // NavMesh bakes on the actual sloped island surface

            return go;
        }

        // AC1 terrain-clip test: is grid cell (lower-left vert index `i`) within the warped landmass + the
        // sea-shelf skirt? True if ANY of the cell's 4 corners is inside (ShoreRadiusAt + IslandClipMargin).
        // Deep-sea cells (all 4 corners well past the coast) are dropped so no square grid edge reads.
        static bool CellNearLandmass(Vector3[] verts, int i, int segX, float ox, float oz)
        {
            int stride = segX + 1;
            int[] corners = { i, i + 1, i + stride, i + stride + 1 };
            foreach (int ci in corners)
            {
                Vector3 v = verts[ci];
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (r <= ShoreRadiusAt(v.x, v.z, ox, oz) + IslandClipMargin) return true;
            }
            return false;
        }

        // ============================================================================================
        // ORGANIC COAST helpers (86ca9qwr3). The coast is the MEAN IslandShoreR WARPED by azimuth noise, so
        // it reads as a REAL island outline (bays + headlands), NOT a circle. The warp phase is derived from
        // the (ox, oz) noise offset already threaded through the height/colour/water builders, so EVERY
        // system (height, colour bands, foam, water foam ring, terrain clip) agrees on the SAME coast — and a
        // different seed (different ox/oz) gives a different outline of the same character (AC6 — the knob).
        // ============================================================================================

        // The warped coast radius at a given world-XZ azimuth. 2-octave azimuth noise (sampled on a unit
        // circle so it wraps seamlessly at 0/360) modulates the mean IslandShoreR by ±CoastIrregAmp.
        public static float ShoreRadiusAt(float wx, float wz, float ox, float oz)
        {
            float ang = Mathf.Atan2(wz, wx); // -PI..PI
            // Sample noise on a circle of azimuth (cos/sin) so there is NO seam at the 0/360 wrap. The circle
            // is scaled by a LARGE radius (CoastNoiseRadius) so the azimuth sweep traverses MANY noise cells
            // (a circle of radius 2.4 barely moves through Perlin space → a near-circular coast; a radius of
            // ~14 sweeps real bays + headlands). Two octaves: big bays + finer wobble. ox/oz re-roll the seed.
            float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
            // TWO LOW-freq octaves only: big lazy BAYS + headlands (a believable organic coast), NOT a spiky
            // star-burst. A small radius keeps the lobes broad; a gentle ×1.25 gain lifts the off-mean spread
            // without the high-freq spikes (the urchin look an aggressive gain + high octave produced).
            float n1 = Mathf.PerlinNoise(ox + (cx * CoastNoiseRadius + 50f),
                                         oz + (cz * CoastNoiseRadius + 50f)) - 0.5f;
            float n2 = Mathf.PerlinNoise(ox * 1.7f + (cx * CoastNoiseRadius * 1.9f + 90f),
                                         oz * 1.7f + (cz * CoastNoiseRadius * 1.9f + 90f)) - 0.5f;
            float warp = (n1 * 2f * 0.68f + n2 * 2f * 0.32f); // -1..1 signed, both low-freq (smooth bays)
            warp = Mathf.Clamp(warp * 1.25f, -1f, 1f);
            return IslandShoreR + warp * CoastIrregAmp;
        }

        // Cliffiness at an azimuth: 0 = flat sand beach sector, 1 = steep rock cliff sector. A SEPARATE
        // low-freq azimuth field thresholded by CliffFraction so ~that share of the coast is cliff. Smooth
        // (not a hard switch) so beach↔cliff transitions are believable. Public for the geometry tests.
        public static float CliffinessAt(float wx, float wz, float ox, float oz)
        {
            float ang = Mathf.Atan2(wz, wx);
            float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
            float n = Mathf.PerlinNoise(ox * 0.5f + (cx * CliffNoiseRadius + 210f),
                                        oz * 0.5f + (cz * CliffNoiseRadius + 210f)); // 0..1
            // Threshold so CliffFraction of the field is cliff. SmoothStep a narrow band around the threshold
            // so the beach→cliff edge is a short transition, not a hard seam.
            float thresh = 1f - CliffFraction;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(thresh - 0.10f, thresh + 0.10f, n));
        }

        // ORGANIC ISLAND HEIGHT FIELD (86ca9qwr3 — supersedes the dome-falloff 86ca9a7qn model). World XZ ->
        // world Y. Beach-LEVEL design (Sponsor AC3): the land stays ~grass level out to the warped coast —
        // NO walking down a dome — and the coastal transition is EITHER a flat sand strip (beach sectors) OR
        // a near-vertical rock drop (cliff sectors), keyed off CliffinessAt. Past the coast the seabed sinks.
        //   r ≲ coast-BeachWidth         : interior LAND at plateau level + hills (full inland)
        //   coast-BeachWidth .. coast     : the SHORE TRANSITION — flat sand strip (beach) OR cliff drop
        //   r ~ coast(azimuth)           : the WATERLINE (warped, irregular — water on all sides)
        //   coast .. IslandFalloffEnd     : the seabed drops to IslandSeabedDrop (below WaterY)
        // Public so the proof/geometry tests sample it directly.
        public static float HeightAtRadial(float wx, float wz, float ox, float oz)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);
            float coast = ShoreRadiusAt(wx, wz, ox, oz);  // the warped waterline radius at this azimuth
            float cliffy = CliffinessAt(wx, wz, ox, oz);  // 0 beach .. 1 cliff for this sector

            // LAND-LEVEL base: the interior + the beach strip stay at the plateau level (AC3 — no dome). The
            // land holds ~flat from the core out to the waterline, so there is no downhill walk to the shore.
            // The drop to the sea happens ONLY in the last stretch (the shore transition), shaped below.
            float baseH = IslandPlateauH;

            // SHORE TRANSITION (the last BeachWidth before the coast + the drop past it):
            //  - BEACH sectors: the land eases down a SHORT, GENTLE sand strip from plateau to the waterline
            //    over BeachWidth (a flat beach, not a long ramp), then the seabed sinks past the coast.
            //  - CLIFF sectors: the land holds at plateau level right up to the coast lip, then DROPS steeply
            //    (CliffDropDepth) over a narrow band — a rock cliff into the water.
            float seabed = WaterY + (IslandSeabedDrop - WaterY) *
                           Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast, IslandFalloffEnd, r));

            float shoreH;
            // Beach profile (COAST-POLISH 86ca9xyqa AC#5a — WATER MEETS THE SAND): the sand eases gently down
            // the DRY strip from plateau to ~the waterline over [coast-BeachWidth, coast-WetShelfWidth], then
            // continues dipping BELOW WaterY across the seaward WET SHELF [coast-WetShelfWidth, coast] so the
            // flat water plane LAPS UP onto the sand (a wet band) instead of meeting a single dry knife-edge.
            // The prior profile reached WaterY only exactly at the coast → a dry slope above the waterline.
            float dryT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - BeachWidth, coast - WetShelfWidth, r)); // 0 inland..1 at the wet line
            float dryH = Mathf.Lerp(IslandPlateauH, WaterY, dryT);                                                  // plateau -> ~WaterY across the dry strip
            float wetT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - WetShelfWidth, coast, r));              // 0 at the wet line..1 at the coast
            float wetH = Mathf.Lerp(WaterY, WaterY - WetShelfDepth, wetT);                                          // dips just below WaterY (water laps it)
            float beachH = r >= coast - WetShelfWidth ? wetH : dryH;
            // Cliff profile: hold plateau until very near the coast, then drop hard over a narrow lip.
            float cliffT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - BeachWidth * 0.35f, coast, r));
            float cliffH = Mathf.Lerp(IslandPlateauH, WaterY - CliffDropDepth * 0.15f, cliffT);
            shoreH = Mathf.Lerp(beachH, cliffH, cliffy);

            // Compose: inland of the beach strip the land is flat plateau (baseH); within the strip it follows
            // the shore profile; past the coast it follows the sinking seabed (below the cliff lip too).
            float h;
            if (r >= coast) h = Mathf.Min(shoreH, seabed); // past the waterline — the (possibly cliff) drop + seabed
            else if (r >= coast - BeachWidth) h = shoreH;   // the shore transition (beach strip / cliff lip)
            else h = baseH;                                  // flat interior land (grass at plateau level)

            // landMask: 1 well inland, easing to 0 at the coast — used to fade the hills off at the shore (a
            // clean waterline) and to NOT pile hills onto the flat beach strip.
            float landMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IslandCoreR, coast - BeachWidth, r));

            // HILLS: multi-octave Perlin elevation over the LAND interior only (faded off by landMask so the
            // beach strip + waterline stay flat/clean). World-space noise (ox/oz) — deterministic + continuous.
            // Damped by the same spawn-flatten the footprint level uses (factored into HillHeightAt).
            float hillH = HillHeightAt(wx, wz, ox, oz, landMask, r);

            // POND-RIM HILL LEVELLING (ticket 86cadj4g7 #130 re-soak — the asymmetric-mound fix AND the elevated-
            // collar/berm fix in ONE). Inside the pond footprint, LEVEL the per-vertex hill height toward a single
            // uniform value = the LOCAL surrounding hill height (FootprintHillLevel), so:
            //  (1) the footprint is a UNIFORM plateau on EVERY azimuth → the centre-grounded water sits below the
            //      rim on all sides (no one-sided mound — the original #130 fix intent), AND
            //  (2) that plateau sits at the LOCAL SURROUNDING ground level (not pulled down to the bare baseH=0.15
            //      while the hilly surroundings stay ~0.40) → the fade band from footprint to surroundings is
            //      nearly FLAT, with NO rising rim/lip encircling the pond. The earlier fix levelled toward ZERO
            //      hill (a 0.15 plateau), so the surrounding hills (~0.40) rose ABOVE it across the fade collar —
            //      a raised green rim casting an inner shadow (the #130 re-soak "green elevated with shadow").
            // PondHillFlatten is the blend weight (0 = fully levelled to the local value inside the mouth; 1 = full
            // per-vertex hills beyond the fade collar). Byte-IDENTICAL beyond the collar (weight 1 = no-op → seed-42
            // lock). The fade band is the ONLY terrain delta vs the locked island outside the bowl.
            float flatten = PondHillFlatten(wx, wz);                          // 1 = local hills, 0 = uniform level
            float footprintLevel = FootprintHillLevel(ox, oz);               // the LOCAL surrounding hill level
            hillH = Mathf.Lerp(footprintLevel, hillH, flatten);              // level toward the LOCAL ground (not 0)

            // Hills only ADD on the interior land (not on the beach strip / past the coast).
            if (r < coast - BeachWidth) h += hillH;

            // FRESHWATER-POND BOWL (ticket 86cadj4g7): carve a local recessed bowl at the pond so the water sits
            // in a visible low spot (the player wades in knee-deep). A downward delta (≤0) centred on the pond
            // XZ, 0 outside PondBowlOuterRadius → the island elsewhere is UNCHANGED (seed-42 silhouette lock).
            // Applied LAST so the dip rides on top of whatever land/hill height is here (the carve is relative).
            h += PondDepressionDelta(wx, wz);

            return h;
        }

        // Island vertex colour, keyed off the WARPED coast + per-sector CLIFFINESS (86ca9qwr3):
        //  - BEACH sectors: a warm SAND strip just inland of the warped waterline, GRASS over the interior.
        //  - CLIFF sectors: warm ROCK on the steep coastal drop (a rock cliff face), GRASS above the lip.
        //  - ROCK also on the high hilltops inland (elevation reads in colour).
        //  - FOAM follows the WARPED waterline (distance to ShoreRadiusAt) on EVERY azimuth (AC4).
        // Per-vertex jitter keeps facets distinct.
        static Color IslandColorAt(float wx, float wz, float height, float ox, float oz, int seed)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);
            float coast = ShoreRadiusAt(wx, wz, ox, oz);
            float cliffy = CliffinessAt(wx, wz, ox, oz);

            // GRASS over the interior land.
            Color grass = Color.Lerp(GrassLo, GrassHi, Mathf.Clamp01(Mathf.InverseLerp(coast, 0f, r)));
            grass = Color.Lerp(grass, GrassRise, Mathf.Clamp01((height - 2.5f) * 0.18f)); // sunlit hill rise

            // ROCK on the high hilltops inland (elevation).
            Color rock = RockCol;
            float hillRockT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IslandPlateauH + IslandHillAmp * 0.55f,
                                                                         IslandPlateauH + IslandHillAmp * 0.85f, height));
            Color land = Color.Lerp(grass, rock, hillRockT);

            // COASTAL band: within BeachWidth of the warped coast, blend in either SAND (beach sectors) or
            // ROCK (cliff sectors) by cliffiness. The band ramps in over the strip so grass meets it smoothly.
            float coastBandT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - BeachWidth, coast - BeachWidth * 0.5f, r));
            Color sand = Color.Lerp(SandLo, SandHi, Mathf.Clamp01((height + 0.2f) * 1.5f));
            Color coastalCol = Color.Lerp(sand, RockCol, cliffy);           // sand on beaches, rock on cliffs
            Color c = Color.Lerp(land, coastalCol, coastBandT);
            // COAST-POLISH (86ca9xyqa AC#5c — RESTORE WARM SAND): the damp/rock blend toward the wet line is now
            // RESERVED FOR CLIFF SECTORS (scaled by cliffy). On a BEACH sector (cliffy~0) the seaward sand STAYS
            // warm golden right to the waterline — the prior code dragged it toward the dark cool SandDamp on
            // EVERY sector, which is exactly why the beach lost its warmth in the organic rewrite. The damp/rock
            // band only darkens the steep CLIFF foot now (where wet rock reads correctly).
            Color belowLip = Color.Lerp(SandDamp, RockCol, cliffy);
            if (r >= coast - BeachWidth * 0.5f)
                c = Color.Lerp(c, belowLip, Mathf.Clamp01((r - (coast - BeachWidth * 0.5f)) / (BeachWidth * 0.6f)) * cliffy);

            // FOAM following the WARPED waterline (AC4 / 86ca9xyqa AC#6a — FOAM BACK). The WATER mesh's foam ring
            // is OCCLUDED by the sand shelf for r<coast (the terrain sits at/above the flat water there), so the
            // visible coast foam must come from the TERRAIN itself. Restored + STRENGTHENED: a WIDER warm-white
            // band centred on the warped waterline (full within the core, fading over a wider band) — wide enough
            // to ride the new wet shelf (AC#5a) AND to trace a SMOOTH curve over the residual grid steps (AC#6b),
            // so the eye reads a soft wave line, not a staircase. On CLIFF sectors the foam is thinner (waves
            // break on rock) — scaled by (1-cliffy*0.6). Sub-1.0 FoamEdge so it never blooms to white.
            float foamDist = Mathf.Abs(r - coast);
            float foamT = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((foamDist - 3.0f) / 5.5f));
            c = Color.Lerp(c, FoamEdge, foamT * 0.95f * (1f - cliffy * 0.55f));

            // POND COLLAR (ticket 86cadj4g7 #130 ROUND 5): paint the FLAT darker-green ring into the terrain
            // around the pond bowl (replaces the removed raised PondBank mesh — the PROVEN white-ring source).
            // Applied AFTER the foam blend so the (inland) pond collar is never washed by the coastal foam (the
            // pond sits at r≈7.6, far from any coast → foamT≈0 here anyway) and BEFORE the value jitter so the
            // collar green still gets the per-facet liveliness step. Painted on the terrain vertex colour = NO
            // raised geometry, NO shadow lip — the player walks on the green at ground level.
            float collarW = PondCollarPaintWeight(wx, wz);
            if (collarW > 0f) c = Color.Lerp(c, PondCollarGreen, collarW);

            // per-vertex value jitter so adjacent facets differ slightly (alive, not flat)
            float j = (Hash01(Mathf.RoundToInt(wx * 13f), Mathf.RoundToInt(wz * 13f), seed) - 0.5f) * 0.10f;
            c.r = Mathf.Clamp01(c.r + j); c.g = Mathf.Clamp01(c.g + j); c.b = Mathf.Clamp01(c.b + j);
            c.a = 1f;
            return c;
        }

        // ============================================================================================
        // BIG ROUND ISLAND scatter (86ca9a7qn): DENSE jungle with TALL trees across the whole land disc,
        // thinning at the coast; rock outcrops on the hills; grass tufts in the interior. Radial placement
        // (sample anywhere on the disc, accept by a land/coast probability), NOT the strip's inland-Z band.
        // Sponsor: "dense forest/jungle with high trees." Tree density + height pushed WAY up vs the old
        // sparse inland scatter, while keeping a near-spawn clearing so the survival loop reads.
        // ============================================================================================
        static void ScatterIslandProps(GameObject parent, int seed, MeshCollider groundCol)
        {
            var rnd = new System.Random(seed + 555);
            SeedOffset(seed, out float ox, out float oz); // SAME warp offset as the terrain (plant on the real land)

            // COAST-STATS TRACE (86ca9qwr3 instrument): dump the warped coast radius around 36 azimuths so the
            // outline's smoothness/irregularity is readable from the log (not eyeballed). A big azimuth-to-
            // azimuth JUMP = a spiky coast; smooth steps = broad bays.
            {
                var sb = new System.Text.StringBuilder("[world-trace] COAST radii: ");
                float prev = ShoreRadiusAt(1f, 0f, ox, oz); float maxJump = 0f; float mn = 1e9f, mx = -1e9f, sum = 0f;
                for (int a = 0; a < 36; a++)
                {
                    float ang = a / 36f * Mathf.PI * 2f;
                    float c = ShoreRadiusAt(Mathf.Cos(ang), Mathf.Sin(ang), ox, oz);
                    if (a < 18) sb.Append($"{c:F0} ");
                    maxJump = Mathf.Max(maxJump, Mathf.Abs(c - prev)); prev = c;
                    mn = Mathf.Min(mn, c); mx = Mathf.Max(mx, c); sum += c;
                }
                Debug.Log(sb.ToString());
                Debug.Log($"[world-trace] COAST stats: min={mn:F0} max={mx:F0} mean={sum/36:F0} maxAdjacentJump={maxJump:F1}u (big jump = spiky)");
            }

            // The walkable/plant-able land disc. The coast is WARPED (86ca9qwr3), so a fixed plantR would put
            // trees in the sea (in bays) or leave a gap (off headlands). Sample the OUTER bound and REJECT any
            // point past the warped coast minus a coastal-fringe margin (trees stay on grass, off the beach/
            // cliff strip). `OnLandmass` is the per-point warped-coast test.
            float plantOuterR = IslandShoreR + CoastIrregAmp; // sample out to the farthest possible headland
            float coastalFringe = BeachWidth + 3f;            // keep trees this far inland of the warped coast
            float spawnClearR = 13f;                          // keep the survival-loop centre an open clearing
            bool OnLandmass(float x, float z) =>
                Mathf.Sqrt(x * x + z * z) <= ShoreRadiusAt(x, z, ox, oz) - coastalFringe;

            // ---- DENSE TALL JUNGLE ----
            // Far more trees than the strip (~13 clusters → ~26 there). The island disc area is ~50× larger,
            // so a high target tree count reads as a real jungle, not a sparse scatter. Cap for perf headroom.
            int treeTarget = 320;     // dense jungle target (perf-measured; the showstopper risk #1)
            int treesPlaced = 0, treeGuard = 0;
            while (treesPlaced < treeTarget && treeGuard++ < treeTarget * 8)
            {
                // Uniform-area sample over the disc: r = R*sqrt(u) so trees aren't centre-biased.
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (rr < spawnClearR) continue;                 // open clearing at the loop centre
                if (!OnLandmass(x, z)) continue;                // reject sea / beach-strip points (warped coast)
                // Density rises inland: accept more readily away from the coast (jungle interior dense).
                float inlandT = Mathf.InverseLerp(plantOuterR, 0f, rr); // 0 coast .. 1 centre
                if (rnd.NextDouble() > Mathf.Clamp01(0.45f + inlandT * 0.55f)) continue;
                // TALL trees (Sponsor "high trees"): bigger base scale + a tall-jungle variant.
                bool tall = rnd.NextDouble() < 0.55f;
                float scale = tall ? (1.5f + (float)rnd.NextDouble() * 0.9f)   // tall jungle tree
                                   : (1.0f + (float)rnd.NextDouble() * 0.6f);  // mid blob tree
                BuildTree(parent, GroundPoint(groundCol, x, z), scale, rnd, true, tall);
                treesPlaced++;
            }

            // ---- ROCK OUTCROPS on the hills ----
            // Clustered boulders, biased toward the higher inland ground (the hill rock reads elevation).
            // Natural piles (2-4 each), spawn-clear, across the disc.
            // GRASS-IN-STONE FIX (ticket 86cadj4g7, Sponsor #130 soak): record each placed rock's XZ + footprint
            // radius so the grass loop below can REJECT any tuft that would land inside a boulder (the defect:
            // independent RNG draws let grass sprout through a stone). FacetedRock has base radius ~0.55u; the
            // rock's world footprint ≈ 0.55 × scale. Recording consumes NO extra rnd draws, so the seed-42
            // tree/rock placement is byte-identical (only overlapping GRASS candidates are now skipped).
            var rockFootprints = new List<Vector4>(64); // (x, z, radius, _) per placed rock
            int rockTarget = 60, rocksPlaced = 0, rockGuard = 0;
            while (rocksPlaced < rockTarget && rockGuard++ < rockTarget * 8)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float cxp = Mathf.Cos(ang) * rr, czp = Mathf.Sin(ang) * rr;
                if (rr < spawnClearR + 4f) continue;             // stay clear of the loop centre
                int n = 2 + rnd.Next(0, 3); // 2-4 boulders per outcrop
                for (int i = 0; i < n && rocksPlaced < rockTarget; i++)
                {
                    float x = cxp + (float)(rnd.NextDouble() - 0.5) * 3.6f;
                    float z = czp + (float)(rnd.NextDouble() - 0.5) * 3.6f;
                    if (!OnLandmass(x, z)) continue;             // warped coast (reject sea / beach strip)
                    float scale = 0.55f + (float)rnd.NextDouble() * 1.0f;
                    BuildRock(parent, GroundPoint(groundCol, x, z), scale, rnd);
                    rockFootprints.Add(new Vector4(x, z, RockFootprintRadius * scale, 0f));
                    rocksPlaced++;
                }
            }

            // ---- GRASS TUFTS — dense ground cover across the interior, sparse at the coast. ----
            // GRASS-IN-STONE FIX (ticket 86cadj4g7): reject any candidate that lands inside a placed boulder's
            // footprint (+ GrassRockPad margin) BEFORE building it — so grass never sprouts through a stone. A
            // linear scan over the ≤60 recorded rocks per candidate is cheap (≤ 60×360 ≈ 22k planar checks at
            // bootstrap, editor-time only). The candidate is still DRAWN from rnd in the same order (the reject
            // is a `continue` like the existing land/density rejects), so the seed-42 stream stays consistent.
            int clumpTarget = 360, clumpsPlaced = 0, clumpGuard = 0;
            while (clumpsPlaced < clumpTarget && clumpGuard++ < clumpTarget * 6)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (!OnLandmass(x, z)) continue;                 // warped coast (reject sea / beach strip)
                float inlandT = Mathf.InverseLerp(plantOuterR, 0f, rr);
                if (rnd.NextDouble() > Mathf.Clamp01(0.25f + inlandT * 0.7f)) continue;
                if (OverlapsAnyRock(rockFootprints, x, z)) continue; // no grass sprouting through a stone (#130)
                BuildGrassClump(parent, GroundPoint(groundCol, x, z),
                    0.5f + (float)rnd.NextDouble() * 0.5f, rnd);
                clumpsPlaced++;
            }

            // ---- BUSHES (ticket 86caa5zz3, AC1/AC2) — varied-size, varied-type leafy bushes across the
            // island; SOME carry berries (a berry-bush variant, AC3). ADDITIVE to the seed-42 world: this
            // uses its OWN System.Random (seed + 777), so it draws from a SEPARATE stream and does NOT
            // perturb the island shape (IslandSeed/ShoreRadiusAt), the terrain mesh, the existing
            // tree/rock/grass placement (which consumed `rnd` (seed+555) in a fixed order, untouched), or
            // the NavMesh (bushes are collider-free + carve NO obstacle — the player walks up to harvest).
            // The seed-42 lock is honored by construction (a parallel sub-stream, not a re-roll of the
            // existing one). Bushes ground via GroundPoint (scale-immune, like the stones). Berry bushes
            // get a BerryBush component (harvest+regrow); plain bushes are decorative.
            var bushRnd = new System.Random(seed + 777);
            int bushTarget = 80, bushesPlaced = 0, berryBushes = 0, bushGuard = 0;
            while (bushesPlaced < bushTarget && bushGuard++ < bushTarget * 8)
            {
                float ang = (float)bushRnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)bushRnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (rr < spawnClearR) continue;                 // keep the loop-centre clearing open
                if (!OnLandmass(x, z)) continue;                // warped coast (reject sea / beach strip)
                // Density rises inland (like the trees) so bushes read as undergrowth in the jungle.
                float inlandT = Mathf.InverseLerp(plantOuterR, 0f, rr);
                if (bushRnd.NextDouble() > Mathf.Clamp01(0.30f + inlandT * 0.55f)) continue;
                // VARIED SIZE (AC2): a wide scale range so the scatter reads natural, not cloned.
                float scale = 0.55f + (float)bushRnd.NextDouble() * 0.95f; // 0.55 .. 1.5
                // VARIED TYPE (AC2/AC3): ~40% of bushes are the BERRY variant (food source); the rest are
                // plain leafy bushes (variety + so berries feel found, not everywhere).
                bool berry = bushRnd.NextDouble() < 0.40f;
                BuildBush(parent, GroundPoint(groundCol, x, z), scale, berry, bushRnd);
                bushesPlaced++;
                if (berry) berryBushes++;
            }

            Debug.Log($"[world-trace] ScatterIslandProps placed {treesPlaced} trees (dense tall jungle), " +
                      $"{rocksPlaced} rocks, {clumpsPlaced} grass tufts, {bushesPlaced} bushes " +
                      $"({berryBushes} berry-bearing) on the ORGANIC island " +
                      $"(warped coast, outerR {plantOuterR:F0}u, fringe {coastalFringe:F0}u). " +
                      "Bushes use sub-seed (seed+777) — additive, seed-42 island/scatter/NavMesh untouched.");
        }

        // Raycast straight down onto the terrain collider to find the surface Y at (x,z) so props
        // sit ON the sloped ground (not floating / buried). Falls back to y=0 if the ray misses.
        static Vector3 GroundPoint(MeshCollider groundCol, float x, float z)
        {
            if (groundCol != null)
            {
                var ray = new Ray(new Vector3(x, 50f, z), Vector3.down);
                if (groundCol.Raycast(ray, out RaycastHit hit, 200f))
                    return hit.point;
            }
            return new Vector3(x, 0f, z);
        }

        // A low-poly tree: tapered trunk (welded cylinder, few sides) + a faceted icosphere-ish
        // canopy (welded, smooth-normaled). Optional NavMeshObstacle carve so the agent paths around.
        // BIG ROUND ISLAND (86ca9a7qn): `tall` builds a TALL JUNGLE tree (long bare trunk + a high canopy)
        // for the "high trees" the Sponsor asked for; the default builds the original blob tree.
        static void BuildTree(GameObject parent, Vector3 at, float scale, System.Random rnd, bool carve,
            bool tall = false)
        {
            var tree = new GameObject("LP_Tree");
            tree.transform.SetParent(parent.transform, false);
            tree.transform.position = at;

            // SEEDED LEAN + HEIGHT VARIATION (ticket 86caamnra — Erik R&D §G / Rank 7). The existing Y-yaw
            // (rnd) already keeps trees from facing identically; Rec 7 adds the two MISSING variations the
            // note calls out for "pine trunks": a seeded ±20% HEIGHT scale + a small APEX LEAN (a few degrees
            // off vertical). Both come from a per-tree DERIVED sub-stream keyed off the (rounded) plant
            // POSITION — NOT extra draws on the shared scatter `rnd` — so the existing tree/rock/grass
            // PLACEMENT (which consumes `rnd` (seed+555) in a fixed order) stays BYTE-IDENTICAL; this only
            // adds per-instance shape variation (AC7a: seed-42 island/scatter/NavMesh/waterline UNCHANGED —
            // the island shape is driven by SeedOffset/HeightAtRadial, never the scatter stream). The lean is
            // a small tilt of the whole tree composed AFTER the yaw, so the trunk base stays seated on the
            // ground while the apex leans (the note's "small xz translation of the apex", via a rotation).
            float yaw = (float)rnd.NextDouble() * 360f; // the existing seeded yaw (unchanged draw on `rnd`)
            var leanRnd = new System.Random(
                Mathf.RoundToInt(at.x * 31.7f) * 73856093 ^ Mathf.RoundToInt(at.z * 31.7f) * 19349663);
            float heightVar = 0.80f + (float)leanRnd.NextDouble() * 0.40f;   // ±20% trunk-height scale (0.80..1.20)
            float leanDeg = 3f + (float)leanRnd.NextDouble() * 5f;           // 3..8° apex lean off vertical
            float leanDir = (float)leanRnd.NextDouble() * 360f;              // which way it leans
            // Compose: yaw about Y, then a small tilt about the lean axis (a horizontal axis at leanDir).
            Quaternion tilt = Quaternion.AngleAxis(leanDeg,
                new Vector3(Mathf.Cos(leanDir * Mathf.Deg2Rad), 0f, Mathf.Sin(leanDir * Mathf.Deg2Rad)));
            tree.transform.rotation = tilt * Quaternion.Euler(0f, yaw, 0f);
            // Non-uniform: vary HEIGHT (Y) by ±20%, keep the trunk girth (XZ) at the base scale so a tall
            // tree stays slender, not a fat scaled-up blob.
            tree.transform.localScale = new Vector3(scale, scale * heightVar, scale);

            // TALL JUNGLE: a much taller, slightly thinner trunk (the canopy rides high) so the forest reads
            // as HIGH trees from orbit. The mid tree keeps the original chunky proportions.
            float trunkH = tall ? (3.6f + (float)rnd.NextDouble() * 1.4f) : 1.6f;
            float botR = tall ? 0.22f : 0.18f, topR = 0.12f;
            var trunk = MakeMeshObject(tree, "Trunk", LowPolyMeshes.TaperedCylinder(botR, topR, trunkH, 6),
                MakeFlatColorMat(TrunkCol, "LPTrunkMat"));
            trunk.transform.localPosition = Vector3.zero;

            // BLOB CANOPY (board v2): a CLUSTER of overlapping faceted spheroids in multi-VALUE greens
            // (CanopyShadow/Body/Top baked per-blob into vertex color), NOT a single smooth dome. 4-6
            // blobs per tree reads like inspiration/21h11_03's four variants. Solid volumes -> sidesteps
            // the thin-foliage normal trap (unity-conventions.md). One shared vertex-color material
            // renders all blobs' greens (no per-blob material churn). Tall trees get a fuller crown.
            int blobCount = tall ? (5 + rnd.Next(0, 3)) : (4 + rnd.Next(0, 3));
            float canopyR = tall ? 1.55f : 1.15f;
            var canopy = MakeMeshObject(tree, "Canopy",
                LowPolyMeshes.BlobCanopy(canopyR, blobCount, CanopyBody, CanopyTop, CanopyShadow, rnd.Next()),
                CanopyVertexColorMat());
            canopy.transform.localPosition = new Vector3(0f, trunkH + (tall ? 0.9f : 0.55f), 0f);

            if (carve)
            {
                var obstacle = tree.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                obstacle.radius = tall ? 0.5f : 0.4f;
                obstacle.height = trunkH + 1.6f;
                obstacle.center = new Vector3(0f, (trunkH + 1.6f) * 0.5f, 0f);
            }
        }

        static void BuildRock(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var rock = new GameObject("LP_Rock");
            rock.transform.SetParent(parent.transform, false);
            rock.transform.position = at;
            // FACETED STONE soak-fix2 (86ca8m5zu): both prior procedural shapes were rejected — the subdiv-0
            // octahedron read as a SPIKE, the subdiv-2 FacetedSphere read as a smooth dark MOUND (its weld
            // averages every facet into a continuous gradient — no stone read). The redo uses LowPolyMeshes
            // .FacetedRock: an ANGULAR, FLAT-SHADED (per-face normals) irregular chunk whose facets each catch
            // the key light at a different value — the board's (21h10_44) chunky-stone look. The mesh itself
            // carries the squat-chunky proportion + lumpiness, so the transform applies ONLY a yaw + a gentle
            // tilt (no thin Y-squash — that flattened it toward a mound). A light random uniform scale gives a
            // natural size range within an outcrop.
            rock.transform.rotation = Quaternion.Euler(
                (float)rnd.NextDouble() * 10f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 10f);
            rock.transform.localScale = Vector3.one * scale;
            // FacetedRock: subdiv-1 base (32 facets), anisotropic chunky displacement, FLAT face normals +
            // per-facet value baked to vertex colour. The warm-grey base goes in _Tint; the vertex value
            // (0.62 dark sides .. 1.0 light tops) multiplies onto it -> facet-to-facet stone value contrast.
            MakeMeshObject(rock, "RockMesh",
                LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: rnd.Next()),
                RockVertexColorMat(RockCol * (0.96f + (float)rnd.NextDouble() * 0.08f)));
        }

        // Grass tufts (the iter-8 dark-shard FIX lives in LowPolyMeshes.GrassClump — up-biased normals
        // both faces). Blade tint is pulled toward the LIT grass end of the terrain ramp (GrassHi) so
        // the tuft reads brighter than the ground it sits on (a tuft buried in same-tone ground
        // disappears).
        static void BuildGrassClump(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var clump = new GameObject("LP_GrassClump");
            clump.transform.SetParent(parent.transform, false);
            clump.transform.position = at;
            clump.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            clump.transform.localScale = Vector3.one * scale;
            // Bias toward GrassHi (sunlit) so the tuft pops off the ground ramp instead of blending in.
            Color blade = Color.Lerp(GrassLo, GrassHi, 0.55f + (float)rnd.NextDouble() * 0.45f);
            MakeMeshObject(clump, "Blades",
                LowPolyMeshes.GrassClump(0.55f, 7, rnd.Next()),
                MakeFlatColorMat(blade, "LPGrassMat"));
        }

        // ---- GRASS-IN-STONE FIX (ticket 86cadj4g7, Sponsor #130 soak) ----
        // FacetedRock(0.55) base radius — the boulder's world footprint ≈ this × the rock's scale. Public so the
        // overlap-rejection test can size a rock + assert grass placed inside it is rejected.
        public const float RockFootprintRadius = 0.55f;
        // Extra margin past a boulder's footprint within which grass is rejected — so a tuft doesn't even brush
        // the stone's edge (a tuft is ~0.5u wide). Tuned so grass keeps a visible gap to the rock.
        public const float GrassRockPad = 0.35f;

        /// <summary>
        /// True if planar XZ point (<paramref name="x"/>,<paramref name="z"/>) falls inside ANY recorded rock's
        /// footprint (+ GrassRockPad) — the reject test that keeps grass from sprouting through a boulder
        /// (ticket 86cadj4g7). <paramref name="rockFootprints"/> entries are (x, z, radius, _). PUBLIC so the
        /// overlap-rejection EditMode test drives it directly with a synthetic rock list.
        /// </summary>
        public static bool OverlapsAnyRock(List<Vector4> rockFootprints, float x, float z)
        {
            for (int i = 0; i < rockFootprints.Count; i++)
            {
                Vector4 f = rockFootprints[i];
                float dx = x - f.x, dz = z - f.y;
                float reach = f.z + GrassRockPad;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }

        static GameObject MakeMeshObject(GameObject parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        // A low-poly BUSH (ticket 86caa5zz3): a squat leafy blob dome (BushBlob — the same blob-green
        // idiom as the tree canopies) sitting ON the ground. The BERRY variant adds a child "Berries"
        // mesh (small red faceted spheres) + a BerryBush component (harvest+regrow) wired to the scene's
        // Inventory + player so the castaway can forage it. NO collider + carves NO NavMesh obstacle (the
        // player walks up to harvest). The berry red is baked into vertex colour, so the berries ride the
        // SAME vertex-color material as the bush greens (one shader, ~1-draw-call discipline). Built
        // editor-time + serialized (the visual + the wired component ship in Boot.unity — not Awake).
        static void BuildBush(GameObject parent, Vector3 at, float scale, bool berry, System.Random rnd)
        {
            var bush = new GameObject(berry ? "LP_BerryBush" : "LP_Bush");
            bush.transform.SetParent(parent.transform, false);
            bush.transform.position = at;
            bush.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            bush.transform.localScale = Vector3.one * scale;

            // The bush body: a squat blob dome in multi-value greens (vertex colour). Shared vertex-color
            // material (CanopyVertexColorMat — the canopy/bush both bake greens into vertex colour, so they
            // batch on one shader). 4-6 blobs reads like the board's leafy clumps.
            float bushR = 0.85f;
            int blobs = 4 + rnd.Next(0, 3);
            MakeMeshObject(bush, "BushBody",
                LowPolyMeshes.BushBlob(bushR, blobs, BushBody, BushTop, BushShadow, rnd.Next()),
                CanopyVertexColorMat());

            if (!berry) return;

            // BERRIES (the harvestable variant): a child mesh of MANY small dense red faceted spheres
            // studding the dome. A SEPARATE child so BerryBush can show/hide JUST the berries on
            // harvest/regrow (the bush body persists). The berry red is in vertex colour -> the SAME
            // vertex-color material. MANY (20-30) small dots so it reads as berries, not flowers (#101 soak-fix).
            var berries = MakeMeshObject(bush, "Berries",
                LowPolyMeshes.BerryCluster(bushR, 20 + rnd.Next(0, 11), BerryRed, rnd.Next()),
                CanopyVertexColorMat());

            // Wire the harvest+regrow component to the scene Inventory + player so a wandering castaway can
            // forage any berry bush. A deterministic regrowSeed (per-bush) so headless behavior is stable.
            var bb = bush.AddComponent<FarHorizon.BerryBush>();
            bb.hasBerries = true;
            bb.berriesVisual = berries.transform;
            bb.inventory = Object.FindObjectOfType<FarHorizon.Inventory>();
            var ctm = Object.FindObjectOfType<FarHorizon.ClickToMove>();
            if (ctm != null) bb.player = ctm.transform;
            // Wire the HungerNeed editor-time (serialized) so a scatter bush's no-arg EatBerry() never does a
            // per-use FindObjectOfType in the build (bake-time Find only, matching the Inventory/player wiring).
            bb.hunger = Object.FindObjectOfType<FarHorizon.HungerNeed>();
            bb.regrowSeed = rnd.Next(1, int.MaxValue);
        }

        // ---- The beach OCEAN (drew/beach-water-scene; Uma beach-water-direction §1-2) ----
        // SUPERSEDES the original 10x10 Unity-primitive Plane (single-tone over-glossy teal, scale.z=4,
        // tucked at shoreZ-14 — a small dim sheet that ended in a HARD plane-edge inside the fog and read
        // as "a puddle", not "the sea the castaway washed in from"). Diagnostic trace (drew/beach-water,
        // 2026-06-13) confirmed the water DID ship in Boot.unity but at pos(0,-0.25,-26) scale(9.9,1,4),
        // meshColors=0 (a primitive Plane carries no vertex colors so the gradient shader had nothing to
        // ride), smoothness 0.88 — invisible-as-a-beach for all those reasons, not absent.
        //
        // Now a WELDED SUBDIVIDED GRID so (1) the near->far teal vertex-color gradient has verts to
        // interpolate across, (2) the in-shader swell has verts to displace, and (3) RecalculateNormals
        // averages to the SMOOTH water sheet that pops against the faceted shore. Extends FAR seaward
        // (well past the fog line) so its far edge dissolves into the warm haze — the "far horizon" the
        // game points at — never a visible plane-edge. Sits just below the shore's lowest verts so the
        // beach's faceted edge dips INTO the water (the coastline is where the sloping beach passes below
        // the water Y, no hard seam). Editor-time authored + serialized (NOT Awake) — the swell is the
        // only animated part and it runs in the shader, so zero runtime geometry construction.
        public const float WaterY = -0.20f;          // sea surface; the round coast is where the land dips below it
        // BIG ROUND ISLAND water (86ca9a7qn): the sea is now a LARGE SQUARE PLANE centred at origin covering
        // the island disc + the surrounding sea on ALL sides, out to the fog horizon — replacing the old
        // −Z-only seaward strip grid. The ROUND coast comes from the radial terrain dipping below WaterY at
        // IslandShoreR; the water plane just fills everything below that all the way around.
        public const float WaterHalfExtent = 700f;   // half-size of the square sea plane — reaches well past the
                                                     // island to the fog horizon on every side (sea-to-horizon, all sides)
        const int WaterSeg = 160;                     // subdivision so the warped foam ring + swell have verts in
                                                      // EVERY quadrant (the warped coast needs a finer grid than
                                                      // the old circular ring; AC4 — foam on all edges, no gaps)
        // RADIAL SHORELINE FOAM ring (carries the accepted soft-wash foam, now ALL the way around the round
        // coast): a warm-white surf ring centred on the radial waterline (r == IslandShoreR), riding the
        // gentle coastal wet-shelf the swell laps. Near-dense ring resolution lands several rows in the band.
        // COAST-POLISH (86ca9xyqa AC#6a/#6b): the SEAWARD-visible water foam (r>coast, over the wet shelf the
        // beach now dips under) is the part not occluded by the sand. Widened so a soft surf band sits just off
        // the waterline AND traces a smooth curve over the coarse water grid (no blocky foam edge).
        const float WaterFoamCoreU = 4.0f;            // full-strength foam plateau within this radial band
        const float WaterFoamBandU = 9.0f;            // foam fades softly to clear beyond the core (wider so the
                                                      // coarse open-sea water grid lands foam verts every side +
                                                      // the surf band reads as a smooth curve, not a stepped edge)
        const float WaterFoamStrength = 0.92f;        // peak foam blend at the waterline (sub-1 keeps a hint of teal)
        static void BuildIslandWater(GameObject parent, string name, Material waterMat, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = 0; // not walkable / not NavMesh (no collider)
            go.transform.position = new Vector3(0f, WaterY, 0f); // sea centred at origin

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // GRID LATTICE: a square plane centred at origin. The depth gradient + radial foam ring key off
            // RADIAL distance from origin (not Z), so they read identically on every coast around the island.
            int lat = WaterSeg + 1;
            float size = WaterHalfExtent * 2f;
            SeedOffset(seed, out float wox, out float woz); // SAME offset as the terrain -> foam aligns to the coast
            var gridPos = new Vector3[lat * lat];
            var gridCol = new Color[lat * lat];
            for (int z = 0; z < lat; z++)
            for (int x = 0; x < lat; x++)
            {
                int i = z * lat + x;
                float fx = (float)x / WaterSeg, fz = (float)z / WaterSeg;
                float localX = (fx - 0.5f) * size;
                float localZ = (fz - 0.5f) * size;
                gridPos[i] = new Vector3(localX, 0f, localZ);
                float r = Mathf.Sqrt(localX * localX + localZ * localZ);
                // The WARPED coast radius at this azimuth (AC1) — the foam + depth gradient follow the
                // irregular waterline, not a circle, so foam wraps the actual organic coast on every side.
                float coast = ShoreRadiusAt(localX, localZ, wox, woz);
                float cliffy = CliffinessAt(localX, localZ, wox, woz);
                // Depth gradient by distance OUT from the WARPED coast (bright near shore, deepening seaward).
                float seawardDist = Mathf.Max(0f, r - coast);
                float depthT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seawardDist / 130f));
                Color c = Color.Lerp(WaterShallow, WaterDeep, depthT);
                // FOAM ring on the WARPED waterline (AC4 — all edges), thinner on cliff sectors (waves break
                // on rock) so beaches read foamier than cliffs.
                float foamDist = Mathf.Abs(r - coast);
                float foamT = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((foamDist - WaterFoamCoreU) / WaterFoamBandU));
                c = Color.Lerp(c, FoamEdge, foamT * WaterFoamStrength * (1f - cliffy * 0.5f));
                c.a = 1f;
                gridCol[i] = c;
            }

            // UNWELDED FLAT-SHADED facets with the TOP face as the FRONT face (the faceted-sea look + the
            // winding/normal contract). THE FIX (86ca9yn57 — sea INVISIBLE = pale skybox showing through):
            // the prior code forced the NORMAL ATTRIBUTE up (`if (fn.y<0) fn=-fn`) but emitted the triangle in
            // the SOURCE winding order — and URP `Cull Back` culls by triangle WINDING (screen-space vertex
            // order), NOT by the normal attribute. The grid's source winding makes the geometric front-face
            // point DOWN (cross((x+1)-x, (z+1)-x) = -Y), so the TOP is the BACK face and the above-looking
            // gameplay camera sees a culled back-face -> 0 water px -> the sky shows through == "water reads
            // SAME as sky". Proven via -waterProbe (ray hits Water_Play at 10-15u, insideBounds, yet 0 px) +
            // a straight-down capture (still 0 px = not occlusion, it's cull). FIX: when the source winding
            // faces down, REVERSE the emitted triangle (swap the 2nd/3rd index) so the FRONT face is UP and
            // Cull Back keeps it. The stored normal is recomputed from the EMITTED winding so normal == facing
            // (the WaterFacesUpTests +Y contract still holds, now backed by real winding, not a flipped proxy).
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            // POND-FOOTPRINT CUTOUT (ticket 86cadj4g7 #130 ROUND 6 — the PROVEN white-ring source). The sea is a
            // world-spanning plane at WaterY (-0.20); inland it is hidden UNDER the terrain — EXCEPT the freshwater
            // pond bowl, carved DOWN to its floor (water surface -0.35, 0.15u BELOW WaterY since the #130 recess
            // deepened to 0.75u). There the sea plane is EXPOSED inside the bowl and its teal+foam reads as a PALE
            // WHITE RING around the pond from overhead — the white the Sponsor kept soaking. Toggle-isolation PROVED
            // it (diag run: sea-plane-OFF dropped the overhead annulus-white 0.215 -> 0.000; bloom-off + collar-
            // removed both LEFT it). FIX: HOLE the sea plane over the pond footprint — skip any sea triangle whose
            // centroid falls within the pond bowl (PondBowlOuterRadius, where the terrain rises back above WaterY
            // and re-hides the sea), so the sea can never show through the bowl. The hole is inland + fully ringed
            // by terrain above WaterY, so it is invisible (no sea-gap) — it only removes the intruding-through-bowl
            // tris. The salt sea elsewhere (the coast, all sides) is UNCHANGED.
            void EmitTri(int a, int b, int c2)
            {
                // Cut the sea hole over the pond bowl: drop the triangle if it OVERLAPS the pond footprint (the
                // sea must not render through the recessed bowl — the #130 white-ring source). ROUND 7: an
                // OVERLAP test (closest point on the triangle to the pond centre within SeaHoleCutRadius), NOT
                // the round-6 CENTROID test — the coarse ~8.75u sea cells are larger than the whole bowl, so a
                // tri whose centroid sat just outside the cut radius still blanketed the bowl from one side
                // (the crescent sliver, 0.182 pale). The XZ closest-point-on-triangle distance catches any tri
                // whose vertex OR edge crosses the footprint, on every azimuth.
                if (SeaTriOverlapsPondFootprint(gridPos[a], gridPos[b], gridPos[c2])) return;

                Vector3 p0 = gridPos[a], p1 = gridPos[b], p2 = gridPos[c2];
                Vector3 fn = Vector3.Cross(p1 - p0, p2 - p0);
                if (fn.sqrMagnitude < 1e-12f) return;
                fn.Normalize();
                int bi = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                cols.Add(gridCol[a]); cols.Add(gridCol[b]); cols.Add(gridCol[c2]);
                if (fn.y >= 0f)
                {
                    // Source winding already faces UP — emit as-is (front face = top).
                    normals.Add(fn); normals.Add(fn); normals.Add(fn);
                    tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                }
                else
                {
                    // Source winding faces DOWN — REVERSE it so the FRONT (CCW) face is the TOP; the normal
                    // for the reversed winding is +fn. This is what makes Cull Back keep the sea from above.
                    fn = -fn;
                    normals.Add(fn); normals.Add(fn); normals.Add(fn);
                    tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                }
            }
            for (int z = 0; z < WaterSeg; z++)
            for (int x = 0; x < WaterSeg; x++)
            {
                int i = z * lat + x;
                EmitTri(i, i + 1, i + lat);
                EmitTri(i + 1, i + lat + 1, i + lat);
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetNormals(normals); // explicit flat per-face normals (UNWELDED faceted sea, all +Y)
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            Debug.Log($"[world-trace] BuildIslandWater built all-sides sea plane (half-extent {WaterHalfExtent:F0}u, " +
                      $"radial foam ring at r={IslandShoreR:F0}u, {verts.Count} verts, " +
                      $"pond hole r={SeaHoleCutRadius:F2}u closest-point overlap)");
        }

        /// <summary>
        /// True if the sea triangle (a,b,c) OVERLAPS the pond footprint — i.e. the XZ closest point on the
        /// triangle to the pond centre is within <see cref="SeaHoleCutRadius"/>. Used to HOLE the sea plane over
        /// the recessed pond bowl (ticket 86cadj4g7 #130 ROUND 7). Unlike a centroid test, this catches a coarse
        /// sea tri whose vertices all sit OUTSIDE the cut radius but whose EDGE crosses the footprint (the
        /// crescent-sliver class on lobe azimuths). Pure XZ math (the sea + pond are both axis-flat at their own
        /// Y), deterministic — testable in EditMode against the shipped mesh.
        /// </summary>
        public static bool SeaTriOverlapsPondFootprint(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector2 centre = new Vector2(PondCenterX, PondCenterZ);
            Vector2 pa = new Vector2(a.x, a.z), pb = new Vector2(b.x, b.z), pc = new Vector2(c.x, c.z);
            float d2 = ClosestPointOnTriangleSqDist(centre, pa, pb, pc);
            float r = SeaHoleCutRadius;
            return d2 <= r * r;
        }

        /// <summary>Squared distance from point <paramref name="p"/> to the closest point on triangle
        /// (a,b,c), all in 2D (XZ). Standard Ericson "Real-Time Collision Detection" barycentric region
        /// decomposition — returns 0 when p is inside the triangle. No allocations.</summary>
        private static float ClosestPointOnTriangleSqDist(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Check voronoi region of vertex a.
            Vector2 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector2.Dot(ab, ap), d2 = Vector2.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return ap.sqrMagnitude;
            // Vertex b.
            Vector2 bp = p - b;
            float d3 = Vector2.Dot(ab, bp), d4 = Vector2.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return bp.sqrMagnitude;
            // Edge ab.
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return (p - (a + v * ab)).sqrMagnitude;
            }
            // Vertex c.
            Vector2 cp = p - c;
            float d5 = Vector2.Dot(ab, cp), d6 = Vector2.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return cp.sqrMagnitude;
            // Edge ac.
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return (p - (a + w * ac)).sqrMagnitude;
            }
            // Edge bc.
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return (p - (b + w * (c - b))).sqrMagnitude;
            }
            // Inside the triangle's face region → distance 0.
            return 0f;
        }

        // ---- Materials ----
        // A flat-color URP/Lit material (smooth shading reads via the averaged vertex normals on the
        // mesh, not via texture). Colors are QUANTIZED to a coarse palette grid before keying so the
        // cache collapses the per-instance jitter into a SMALL number of shared materials (a few
        // greens/greys/browns) instead of one .mat per unique jittered color — avoiding asset churn.
        // The materials are NOT persisted as standalone .mat assets; assigned to sharedMaterial they
        // serialize INLINE into the saved scene (works in the standalone build). Cached per-bootstrap.
        static readonly Dictionary<string, Material> _flatCache = new Dictionary<string, Material>();
        static Material MakeFlatColorMat(Color c, string baseName)
        {
            Color q = Quantize(c);
            string key = baseName + "_" + ColorKey(q);
            if (_flatCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = key;
            mat.SetColor("_BaseColor", q);
            mat.SetFloat("_Smoothness", 0.06f); // matte: low-poly reads by shape + shading, not gloss
            _flatCache[key] = mat;
            return mat;
        }

        // The blob canopies bake their multi-value greens into per-vertex COLOR, so they need the
        // vertex-color shader (URP/Lit IGNORES vertex color — unity-conventions.md). One shared, cached,
        // INLINE material (no .mat asset) renders every tree's canopy; the shader is already registered
        // in AlwaysIncludedShaders by WorldBootstrap so it doesn't strip in the standalone build. _Tint
        // stays white so the vertex greens come through unmodified. Falls back to a single mid-green
        // URP/Lit (vertex color lost, but never magenta/broken) if the shader is somehow unresolved.
        static Material _canopyMat;
        static Material CanopyVertexColorMat()
        {
            if (_canopyMat != null) return _canopyMat;
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                _canopyMat = new Material(vc) { name = "LPBlobCanopyMat" };
                if (_canopyMat.HasProperty("_Tint")) _canopyMat.SetColor("_Tint", Color.white);
            }
            else
            {
                _canopyMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "LPBlobCanopyMat" };
                _canopyMat.SetColor("_BaseColor", CanopyBody);
                _canopyMat.SetFloat("_Smoothness", 0.06f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; blob canopy falls back to flat green");
            }
            return _canopyMat;
        }

        // ROCK vertex-color material (86ca8m5zu SOAKFIX2). The FacetedRock mesh bakes a per-FACET VALUE
        // (0.62 dark sides .. 1.0 light tops) into vertex colour — the facet-to-facet contrast that reads as
        // carved stone. URP/Lit IGNORES vertex colour, so the rock needs the FarHorizon/LowPolyVertexColor
        // shader (albedo = vertexColor * _Tint), same as the blob canopy. The warm-grey rock base goes in
        // _Tint (quantized + cached so jittered tints collapse to a small set — no .mat asset churn); the
        // vertex value multiplies onto it so every facet is a different value of the SAME warm-grey stone.
        // Falls back to flat URP/Lit (value contrast lost but never magenta) if the shader is unresolved.
        static readonly Dictionary<string, Material> _rockCache = new Dictionary<string, Material>();
        static Material RockVertexColorMat(Color baseTint)
        {
            // FINE quantization (24-step, not the coarse 12-step the other props use): the coarse grid split
            // the warm-grey rock ramp into a pink (R>G=B) cast (TINTDIAG). The fine grid preserves R>=G>=B —
            // still only ~4 distinct rock mats, so churn stays low.
            Color q = QuantizeFine(baseTint);
            string key = "LPRockMat_" + ColorKey(q);
            if (_rockCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material mat;
            if (vc != null)
            {
                mat = new Material(vc) { name = key };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", q);
                // VERTEX-COLOR AO (ticket 86caamnra — Rec 6): the FacetedRock mesh bakes a geometric AO proxy
                // into vertex-color ALPHA (low/downward crevice facets darker); raise _AOStrength so the
                // shader's lerp(1, alpha, _AOStrength) surfaces that contact-shadow depth at the rock's base.
                // ~0.5 = a believable contact darkening without crushing the crevices (the mesh AO floor is
                // 0.55). Guarded — a no-op on the URP/Lit fallback (which lacks the property).
                if (mat.HasProperty("_AOStrength")) mat.SetFloat("_AOStrength", 0.5f);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
                mat.SetColor("_BaseColor", q);
                mat.SetFloat("_Smoothness", 0.05f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; rock falls back to flat grey");
            }
            _rockCache[key] = mat;
            return mat;
        }

        // Clear the per-bootstrap material cache so a re-run does not return materials owned by a
        // destroyed scene (the editor keeps the static cache across executeMethod invocations).
        public static void ResetMaterialCache() { _flatCache.Clear(); _canopyMat = null; _rockCache.Clear(); }

        // Snap each channel to a coarse 12-step grid so jittered colors collapse into a small set.
        static Color Quantize(Color c)
        {
            const float steps = 12f;
            return new Color(
                Mathf.Round(c.r * steps) / steps,
                Mathf.Round(c.g * steps) / steps,
                Mathf.Round(c.b * steps) / steps, 1f);
        }

        // Finer 24-step quantizer for the ROCK tint — the coarse 12-step grid split the warm-grey ramp into
        // a pink (R>G=B) cast (TINTDIAG). 24 steps preserve R>=G>=B while still collapsing the per-instance
        // jitter to ~4 distinct rock materials (low churn).
        static Color QuantizeFine(Color c)
        {
            const float steps = 24f;
            return new Color(
                Mathf.Round(c.r * steps) / steps,
                Mathf.Round(c.g * steps) / steps,
                Mathf.Round(c.b * steps) / steps, 1f);
        }

        static string ColorKey(Color c) =>
            Mathf.RoundToInt(c.r * 255) + "_" + Mathf.RoundToInt(c.g * 255) + "_" + Mathf.RoundToInt(c.b * 255);

        // Vertex-color terrain material: URP/Lit does NOT multiply vertex color, so for LIT smooth
        // shading WITH vertex color we use the tiny custom shader shipped in Assets/Shaders
        // (LowPolyVertexColor.shader). If that shader resolves, use it; else fall back to URP/Lit
        // white (still smooth-shaded, just single-tone) so the build never breaks. The shader is
        // registered in AlwaysIncludedShaders by WorldBootstrap so the standalone build does not
        // strip it (the spike's magenta lesson).
        public static Material MakeTerrainVertexColorMaterial(string assetPath)
        {
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material mat;
            if (vc != null)
            {
                mat = new Material(vc);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.06f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; terrain falls back to white URP/Lit");
            }
            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        // The beach-ocean material (drew/beach-water-scene; Uma §1 task A). RIDES the existing
        // FarHorizon/LowPolyVertexColor shader (the same one canopy + terrain use) so the near->far teal
        // gradient baked into the water grid's vertex colors actually renders — URP/Lit IGNORES vertex
        // color, so the old URP/Lit water shipped single-tone (the diagnostic confirmed meshColors=0 was
        // moot anyway because the primitive Plane had none). The vertex-color shader is a flat diffuse +
        // SH-ambient lit model: the "moderate gloss / catches the warm sky" read (Uma's smoothness ~0.6
        // intent) comes from that soft lit shading + the bright sub-1.0 teal, NOT a mirror specular — a
        // high-gloss reflective water would break the toy (Uma §1: pull the gloss DOWN from 0.88). The
        // shader is registered in AlwaysIncludedShaders by WorldBootstrap/MovementCameraScene so it does
        // not strip. _WaveAmp > 0 turns ON the in-shader swell for the WATER ONLY (default 0 elsewhere).
        // Falls back to a sub-1.0 teal URP/Lit (gradient + swell lost, but never magenta) if unresolved.
        public static Material MakeWaterMaterial(string assetPath)
        {
            Material mat;
            // TRANSPARENT depth-fade FOAM water (ticket 86caamnmb): the water now rides the NEW
            // FarHorizon/LowPolyWater shader (a fork of LowPolyVertexColor on the Transparent queue with the
            // depth-fade foam + the PORTED _FogCap floor). The opaque LowPolyVertexColor shader stays for
            // every NON-water surface (terrain/canopy/rock). Registered in AlwaysIncludedShaders by
            // WorldBootstrap so it does not strip. Falls back to LowPolyVertexColor (opaque, no foam — still
            // teal + fog-cap), then to a flat teal URP/Lit, if unresolved (never magenta).
            var vc = Shader.Find("FarHorizon/LowPolyWater");
            if (vc == null) vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                mat = new Material(vc) { name = "LowPolyWaterMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white); // vertex teal unmodified
                // Gentle large-wavelength swell (Uma §1: "a breath, not surf"). The shader displaces Y
                // in-vertex; nothing runs per-frame on the CPU. SOFT-WASH RESTORE (86ca8t9pq S1): bump the
                // amplitude 0.06 -> 0.10u + a touch faster so the foam surf LINE at the coast visibly
                // BREATHES (advances/retreats up the gentle wet shelf) — the "water washing up on shore"
                // the Sponsor liked. Still a calm swell, not surf (a longer wavelength keeps it soft).
                // MOVING WAVES (86ca9yn57 AC2 — Sponsor: "the water should have waves that move"). The swell
                // shipped at amp 0.10 (peak ~0.05u) — imperceptible over the ~1400u sea, so it read STATIC.
                // Bump the amplitude so the surface visibly undulates AND shorten the wavelength so multiple
                // crests cross the framed sea (a single 18u-period wave on a vast plane barely moves on-screen).
                // Still a calm rolling swell at the toy scale, not surf (the WaveAmp/Len ratio stays gentle).
                if (mat.HasProperty("_WaveAmp")) mat.SetFloat("_WaveAmp", 0.45f);   // 0.10 -> 0.45 (peak ~0.22u — reads at sea scale)
                if (mat.HasProperty("_WaveLen")) mat.SetFloat("_WaveLen", 11f);     // 18 -> 11 (more crests cross the frame)
                if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 1.1f);// 0.95 -> 1.1 (the swell visibly travels)
                // FOG-CAP (86ca9yn57 AC1 — Sponsor: "I can't see any difference between water and sky"). The
                // global Exp^2 fog (colour == SkyHorizon, the mountain seam-kill anchor) washes the FAR SEA to
                // the sky colour, killing the sea↔sky horizon (diagnosed via -verifyCoast/-seaDiag: the far-sea
                // band read (0.80,0.88,0.91) == SkyHorizon). _FogCap floors the water's fog visibility so the
                // sea keeps >= this fraction of its own teal at the horizon — distinct from the pale sky — while
                // the mountains (different material instance, _FogCap=0) still dissolve via full fog (seam-kill
                // preserved). 0.5 keeps a believable atmospheric haze on the far sea (no harsh seam) yet a clear
                // teal-vs-sky boundary.
                if (mat.HasProperty("_FogCap")) mat.SetFloat("_FogCap", 0.5f);
                // DEPTH-FADE FOAM (ticket 86caamnmb AC2/AC4). _FoamColor == FoamEdge (#E8E2D0) so the dynamic
                // depth-fade foam and the static baked FoamEdge band read as ONE warm foam line where they
                // overlap (no double-bright seam). _FoamDistance ~1.5u: the foam band's seaward fade width at
                // a water↔object intersection (beach waterline, rocks, stumps). Both are no-ops on the
                // LowPolyVertexColor fallback (HasProperty guards) — that path ships opaque, no foam.
                if (mat.HasProperty("_FoamColor")) mat.SetColor("_FoamColor", FoamEdge);
                if (mat.HasProperty("_FoamDistance")) mat.SetFloat("_FoamDistance", 1.5f);
                if (mat.HasProperty("_WaterAlpha")) mat.SetFloat("_WaterAlpha", 1f); // solid teal sea (not see-through)
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "LowPolyWaterMat" };
                mat.SetColor("_BaseColor", WaterShallow); // sub-1.0 bright teal (gradient lost in fallback)
                mat.SetFloat("_Smoothness", 0.6f);
                mat.SetFloat("_Metallic", 0.0f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; water falls back to flat teal URP/Lit (no gradient, no swell)");
            }
            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        // ============================================================================================
        // FRESHWATER POND (ticket 86caamkv7 / Uma §1). The pond REUSES the ocean water infra (the same
        // FarHorizon/LowPolyWater shader) — NO new shader — with the deltas Uma §1 specifies so it reads as a
        // small, still, SAFE freshwater pool vs the vast salt sea. One extra material instance + one extra
        // mesh = additive, ~1-draw-call-friendly, no per-frame CPU (the swell is in-shader).
        // ============================================================================================

        /// <summary>
        /// Build the FRESHWATER POND material (ticket 86caamkv7 / Uma §1 deltas). A SIBLING of
        /// <see cref="MakeWaterMaterial"/> on the SAME FarHorizon/LowPolyWater shader — NO new shader — with
        /// the pond-feel deltas: glassy-calm swell (tiny amp/short wavelength/slow), a TIGHT damp bank
        /// (low _FoamDistance, no static surf ring), no horizon fog-floor (the pond is near). The vertex teal
        /// the sea uses is replaced by the pond's OWN PondShallow/PondDeep gradient baked into the mesh
        /// (BuildPondWaterMesh) — the material's _Tint stays white so it doesn't recolour the fresh blue.
        /// </summary>
        public static Material MakePondMaterial(string assetPath)
        {
            Material mat;
            var vc = Shader.Find("FarHorizon/LowPolyWater");
            if (vc == null) vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                mat = new Material(vc) { name = "PondWaterMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white); // pond's fresh-blue vertex colour unmodified
                // CALM delta (Uma §1b): a glassy-calm pool with the faintest breath — NOT the sea's swell.
                if (mat.HasProperty("_WaveAmp"))   mat.SetFloat("_WaveAmp", 0.04f);   // 0.45 sea -> 0.04 (barely-there shimmer)
                if (mat.HasProperty("_WaveLen"))   mat.SetFloat("_WaveLen", 4f);      // 11 sea -> 4 (short ripple at pond scale)
                if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 0.4f);  // 1.1 sea -> 0.4 (slow)
                // FOG-CAP delta (Uma §1d): the pond is inland + near, never reaches the fog horizon, so the
                // sea's teal-vs-sky fog-floor is irrelevant — 0 lets normal fog apply + avoids a faint over-
                // bright cast up close.
                if (mat.HasProperty("_FogCap")) mat.SetFloat("_FogCap", 0f);          // 0.5 sea -> 0 (no sea<->sky seam at pond range)
                // FOAM/EDGE delta — FOAM OFF (ticket 86cadj4g7 — Sponsor #130 re-soak: "the pond water should
                // NOT foam like the sea; the freshwater pond must be STILL, no foam ring"). The SEA keeps its
                // foam; only the POND loses it. ⚠ #130 RE-SOAK FIX: _FoamDistance == 0 is NOT sufficient. The
                // mask is foam = saturate(1 - gap/max(_FoamDistance,0.001)); for any gap > 0.001u it is 0 (no
                // open-water foam), BUT at the EXACT shoreline (water grazes the bank → gap → 0) it is still 1.0
                // — a razor-thin WHITE intersection line surviving along the whole bank (the white foam ring the
                // Sponsor STILL saw with the HUD showing FOAM:OFF). The real OFF switch is the master _FoamAmount
                // gate (LowPolyWater.shader): _FoamAmount == 0 zeroes the WHOLE foam term — colour AND alpha —
                // including those gap≈0 pixels, so OFF means ZERO foam anywhere on the pond. The SEA never sets
                // _FoamAmount (defaults to 1) so it is untouched. _FoamDistance is set to PondFoamOff (0) too for
                // consistency, but _FoamAmount is what actually removes the shoreline ring. The live PondNudge
                // [foam] handle drives BOTH (off → amount 0; light/sea-like → amount 1 + distance 0.06/1.5).
                if (mat.HasProperty("_FoamColor"))    mat.SetColor("_FoamColor", FoamEdge);
                if (mat.HasProperty("_FoamDistance")) mat.SetFloat("_FoamDistance", PondFoamOff); // 0 = no band width
                if (mat.HasProperty("_FoamAmount"))   mat.SetFloat("_FoamAmount", PondFoamAmountOff); // 0 = master OFF (kills the shoreline ring too)
                if (mat.HasProperty("_WaterAlpha"))   mat.SetFloat("_WaterAlpha", 1f);     // solid coloured sheet (no modelled bottom — OOS)
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PondWaterMat" };
                mat.SetColor("_BaseColor", PondShallow); // fresh blue (gradient lost in fallback)
                mat.SetFloat("_Smoothness", 0.6f);
                mat.SetFloat("_Metallic", 0.0f);
                Debug.LogWarning("[LowPolyZoneGen] water shader not found; pond falls back to flat blue URP/Lit (no gradient, no swell)");
            }
            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        // ===== ORGANIC POND OUTLINE (ticket 86cadj4g7, Sponsor 2026-06-24 "the pond should not be perfectly
        // round"; memory pond-organic-not-round) =====================================================
        // The pond outline must read ORGANIC — a natural blob / kidney / lily-pad lobe matching the organic
        // island (memory world-is-big-round-island) — NOT a clean geometric circle. We perturb the per-vertex
        // rim RADIUS by a smooth, low-frequency angular noise (a sum of a few sinusoids with irrational
        // frequencies + fixed phases). Low frequency = soft LOBES (natural + slightly kidney-shaped), never
        // jagged/spiky. The function is DETERMINISTIC (a pure function of the angle — no RNG) so the capture
        // stays byte-stable (BuildPondWaterMesh_IsDeterministic guards this) and so the WATER disc and the
        // grassy BANK ring share the SAME outline (both call this) — the bank keeps framing the water with no
        // gap/poke-through. Amplitude is BOUNDED (±~18%): the pond sits on the FLAT spawn-plateau (r<16; the
        // pond centre is at world (7,-3) ⇒ r≈7.6, max reach with bank ≈11.7 ≪ 16), so the rim stays within the
        // flat zone where the carved BOWL (WorldBootstrap.GroundPondInBowl + PondDepressionDelta) recesses the
        // whole pond uniformly — the water disc sits in the bowl, above its carved floor, around the whole
        // irregular rim (⚠ -verifyPond samples frame-CENTRE only, so this whole-rim recessing is enforced here
        // by construction, not by the gate). The bounded amplitude also keeps the rim well inside the bowl-floor
        // band (PondBowlInnerRadius 3.2u > the max rim ~3.07u) AND every rim vert at r > 0 (a positive radius)
        // so the disc sits over the flat floor everywhere and the outline never self-crosses.
        //
        // PER-VERTEX MULTIPLIER in [1-AMP*..., 1+AMP*...]; the three terms give 3 + 2 + 1 = soft compound lobes
        // around the ring without any single dominant axis (reads as a found natural pool, not an ellipse).
        const float PondRimNoiseAmp = 0.18f;   // ±18% radius wobble — clearly organic, never spiky at this low freq
        /// <summary>
        /// Deterministic organic-rim radius multiplier for the pond outline at angle <paramref name="ang"/>
        /// (radians). Shared by the water disc + the bank ring so they stay concentric/lobed together. Pure
        /// function of the angle (no RNG) — byte-stable capture. Returns a factor near 1.0 (±~PondRimNoiseAmp),
        /// always > 0, smoothly varying (low-frequency sinusoids → soft lobes, not jagged teeth).
        /// </summary>
        public static float PondRimFactor(float ang)
        {
            // Irrational-ish frequencies + fixed phases → a non-repeating soft blob over [0,2π). Weighted so the
            // 3-lobe term dominates (the kidney/lily-pad read), the 5-lobe adds a gentle secondary wobble, the
            // 2-lobe gives a slight overall elongation. Sum is bounded by the weight total (1.0) ⇒ factor in
            // [1-AMP, 1+AMP]. Low max frequency (5) keeps adjacent rim verts close in radius → smooth, not spiky.
            float n = 0.60f * Mathf.Sin(ang * 3f + 0.7f)
                    + 0.25f * Mathf.Sin(ang * 5f + 2.3f)
                    + 0.15f * Mathf.Sin(ang * 2f + 4.1f);
            return 1f + PondRimNoiseAmp * n;
        }

        /// <summary>
        /// Build the FRESHWATER POND water-surface mesh (ticket 86caamkv7 / Uma §1a): a flat faceted disc of
        /// nominal radius <paramref name="radius"/> with an ORGANIC/IRREGULAR rim (PondRimFactor — Sponsor "not
        /// perfectly round", ticket 86cadj4g7) and a bank(shallow)->centre(deep) vertex-colour gradient
        /// (PondShallow -> PondDeep), faces UP (+Y) so the orbit camera above sees it (the same winding/cull
        /// contract the sea grid honours — a fan disc wound CCW-from-above is front-facing under URP Cull Back).
        /// NO static surf ring (a pond has no wave-break — the dynamic depth-fade foam at the bank is the whole
        /// foam story, Uma §1c). UNWELDED flat facets so the low-poly faceting reads. More <paramref name="sides"/>
        /// than the old circle (the lobes need enough segments to read smoothly faceted, not coarse). Public so
        /// the scene author (MovementCameraScene.BuildFreshwaterPond) + tests build it.
        /// </summary>
        public static Mesh BuildPondWaterMesh(float radius, int sides = 22)
        {
            sides = Mathf.Max(8, sides);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            // A radial fan: a centre vert (deep) + a ring of ORGANIC rim verts (shallow). Each triangle is its
            // own UNWELDED facet so the faceting reads (sibling of the sea's per-face emit). The rim radius is
            // perturbed by PondRimFactor (deterministic smooth noise) so the outline reads as a natural lobed
            // blob, not a circle. The gradient runs bank->centre: rim = PondShallow (the bright fresh bank
            // water), centre = PondDeep (the cool blue pool depth) — the SAME depthT idiom the sea uses, keyed
            // off distance-to-centre instead of coast.
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                float r0r = radius * PondRimFactor(a0);                                // organic rim radius @ a0
                float r1r = radius * PondRimFactor(a1);                                // organic rim radius @ a1
                Vector3 c = Vector3.zero;                                              // centre (deep)
                Vector3 r0 = new Vector3(Mathf.Cos(a0) * r0r, 0f, Mathf.Sin(a0) * r0r); // rim (shallow)
                Vector3 r1 = new Vector3(Mathf.Cos(a1) * r1r, 0f, Mathf.Sin(a1) * r1r);
                int bi = verts.Count;
                // Wind centre -> r1 -> r0 so the geometric front (CCW from above) faces +Y (Cull Back keeps it).
                verts.Add(c); verts.Add(r1); verts.Add(r0);
                cols.Add(PondDeep); cols.Add(PondShallow); cols.Add(PondShallow);
                normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
            }

            var mesh = new Mesh { name = "LP_PondWater" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // The per-seed noise OFFSET used by the height/colour/water fields. Deterministic from the seed so
        // the terrain, the colour bands, the foam, AND the water foam ring all agree on the SAME warped coast
        // (AC1/AC4) — and a different seed gives a different island outline (AC6 — the knob).
        public static void SeedOffset(int seed, out float ox, out float oz)
        {
            var rnd = new System.Random(seed);
            ox = (float)rnd.NextDouble() * 100f;
            oz = (float)rnd.NextDouble() * 100f;
        }

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ x * 19349663 ^ y * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h & 0x7fffffff) % 100000) / 100000f;
            }
        }
    }
}
