using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the FRESHWATER POND (ticket 86caamkv7, AC2/AC2a/AC6). Reads the serialized Boot
    /// scene (binary scenes can't be GUID-grepped — the EditMode reader is authoritative) and pins:
    ///
    ///  • the pond is PRESENT in the shipped scene (serialized, not an Awake add — editor-vs-runtime trap);
    ///  • its FreshwaterPond drink seam is wired (thirst + player refs serialize);
    ///  • the pond is COLLIDER-FREE on every piece (AC2a — a collider would carve the NavMesh bake / block the
    ///    ground raycast; collider-free is what PROVES the pond cannot shrink NavMesh coverage). This is the
    ///    seed-42-lock guard's pond half: the pond is authored OUTSIDE the seeded LowPolyZoneGen stream
    ///    (MovementCameraScene, not ScatterIslandProps), so it provably cannot perturb the seed-42 island
    ///    silhouette / scatter; collider-free proves it also cannot perturb the NavMesh. (The whole-island
    ///    NavMesh COVERAGE non-regression is asserted end-to-end by RoundIslandNavCoveragePlayModeTests.)
    ///  • the pond water reads as FRESH water (the pond's own PondShallow/PondDeep blue, distinct from the sea).
    ///
    /// Sibling of BushSceneTests / BeachDebrisSceneTests (scene-presence + no-collider contract guards).
    /// </summary>
    public class FreshwaterPondSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesFreshwaterPond_Serialized_AndWired()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond,
                "the Boot scene must carry the FreshwaterPond serialized into the scene — the thirst source " +
                "ships from this scene, not an Awake add (unity-conventions.md editor-vs-runtime trap)");

            Assert.IsNotNull(pond.player, "the FreshwaterPond's player ref must serialize (the proximity gate reads it)");
            Assert.IsNotNull(pond.thirst, "the FreshwaterPond's ThirstNeed ref must serialize (the drink->AddWater seam)");
            Assert.Greater(pond.EffectiveDrinkRadius, 0f, "the pond must have a positive drink reach");
        }

        [Test]
        public void BootScene_CarriesDrinkAction_WiredToThePond()
        {
            // The DRINK INPUT call-site (Q) must serialize into the scene with its pond ref wired (BootstrapProject
            // adds the DrinkAction; MovementCameraScene wires the pond after authoring it). Drop either and the
            // shipped build has no way to drink — this reds in headless CI before a soak. Sibling intent to the
            // EatBerryAction wiring for hunger.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            DrinkAction drink = FindInScene<DrinkAction>(scene);
            Assert.IsNotNull(drink,
                "the Boot scene must carry the DrinkAction (the Q drink-input call-site) serialized — without " +
                "it nothing in the build invokes the drink seam (the dual-spawn 'tested but never invoked' class)");
            Assert.IsNotNull(drink.pond,
                "the DrinkAction's pond ref must serialize (wired by MovementCameraScene after the pond is authored)");
        }

        [Test]
        public void Pond_IsColliderFree_CannotCarveNavMeshOrBlockRaycast()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond, "the pond must be present");

            // AC2a: the pond must NOT shrink NavMesh coverage. The mechanism that PROVES it: every piece of the
            // pond (water surface, bank, accents) is collider-free, so it contributes NOTHING to the NavMesh
            // bake (which collects PhysicsColliders) and never blocks the click-move ground raycast. A collider
            // sneaking onto the pond would be the silent killer (it would carve a hole in the walkable disc).
            var colliders = pond.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(0, colliders.Length,
                "the pond + bank + accents must be COLLIDER-FREE so they cannot carve the NavMesh bake or " +
                "block the ground raycast (AC2a seed-42/NavMesh lock — the player walks up to drink)");
        }

        [Test]
        public void Pond_WaterReadsAsFreshWater_DistinctBluePalette()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond, "the pond must be present");

            // Find the pond water mesh (the PondWater child) and confirm it carries the FRESH-water blue
            // palette (PondShallow/PondDeep) — distinct from the salt sea's teal. The freshwater tell is B > G.
            var mf = FindChildMeshNamed(pond.transform, "LP_PondWater");
            Assert.IsNotNull(mf, "the pond must carry the freshwater water-surface mesh (LP_PondWater)");
            var cols = mf.colors;
            Assert.Greater(cols.Length, 0, "the pond water mesh must carry vertex colours (the fresh-blue gradient)");

            // Every vertex colour should be the pond's own fresh blue (PondShallow or PondDeep) — assert at
            // least one vert reads B > G (the freshwater lean the sea's teal never has: the sea keeps G >= B).
            bool anyFreshBlue = false;
            foreach (var c in cols) if (c.b > c.g + 0.001f) { anyFreshBlue = true; break; }
            Assert.IsTrue(anyFreshBlue,
                "the pond water must read as FRESH water — at least one vertex leans BLUE (B > G), the " +
                "freshwater tell the salt sea's teal (G >= B) never shows (Uma §1a)");
        }

        // BuildPondWaterMesh is deterministic (no RNG) — the capture is byte-stable build to build. A guard
        // so a future change that injected jitter (breaking the stable capture) reds here.
        [Test]
        public void BuildPondWaterMesh_IsDeterministic()
        {
            var a = LowPolyZoneGen.BuildPondWaterMesh(2.6f);
            var b = LowPolyZoneGen.BuildPondWaterMesh(2.6f);
            Assert.AreEqual(a.vertexCount, b.vertexCount, "the pond water mesh vertex count is deterministic");
            var va = a.vertices; var vb = b.vertices;
            for (int i = 0; i < va.Length; i++)
                Assert.AreEqual(va[i], vb[i], "the pond water mesh geometry is deterministic (no RNG) — a stable capture");
            // The fresh-blue palette is in the build (PondShallow rim, PondDeep centre): assert B > G shows up.
            bool freshLean = false;
            foreach (var c in a.colors) if (c.b > c.g + 0.001f) { freshLean = true; break; }
            Assert.IsTrue(freshLean, "the built pond water carries the freshwater B>G lean");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7, Sponsor 2026-06-24 "the pond should not be perfectly round") =
        // The pond outline must read ORGANIC — a natural lobed blob, NOT a perfect circle. The rim radius is
        // perturbed per-vertex by LowPolyZoneGen.PondRimFactor (deterministic smooth noise). This guards the
        // bug CLASS: a future change that reverted the outline to a uniform circle (every rim vert at the same
        // radius) reds here. Asserts (a) the rim is genuinely IRREGULAR (a meaningful spread of rim radii), and
        // (b) the perturbation stays BOUNDED + POSITIVE (no self-crossing, and — load-bearing for grounding —
        // the rim never balloons out of the flat spawn-plateau where the +0.10u reground clearance holds; the
        // -verifyPond gate only samples frame-CENTRE so the whole-rim grounding is enforced by THIS bound).
        [Test]
        public void BuildPondWaterMesh_OutlineIsOrganic_NotAPerfectCircle()
        {
            const float nominal = 2.6f;
            var mesh = LowPolyZoneGen.BuildPondWaterMesh(nominal);
            var verts = mesh.vertices;

            // Collect the planar radius of every RIM vertex (the centre verts sit at the origin, r≈0 — skip them).
            float minR = float.MaxValue, maxR = 0f;
            int rimCount = 0;
            foreach (var v in verts)
            {
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (r < 0.01f) continue;           // a centre (deep) vertex
                rimCount++;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
            }
            Assert.Greater(rimCount, 0, "the pond mesh must carry rim vertices");

            // (a) ORGANIC: the rim radii must SPREAD — a perfect circle has minR == maxR. Require a clear spread
            // (the lobes), so a revert to a uniform-radius circle fails. (~10% of nominal is a comfortable floor
            // below the ±18% design amplitude.)
            float spread = maxR - minR;
            Assert.Greater(spread, nominal * 0.10f,
                $"the pond outline must be ORGANIC (irregular rim) — rim radii spread {spread:F3} must exceed " +
                $"{nominal * 0.10f:F3} (a perfect circle has zero spread; Sponsor 'not perfectly round', 86cadj4g7)");

            // (b) BOUNDED + POSITIVE: the rim must stay within ~±25% of nominal (well inside the flat plateau so
            // the reground clearance holds around the whole rim) and never collapse to/through the centre.
            Assert.Greater(minR, nominal * 0.70f,
                $"the pond rim min radius {minR:F3} must stay positive + near nominal (never self-cross/collapse)");
            Assert.Less(maxR, nominal * 1.30f,
                $"the pond rim max radius {maxR:F3} must stay bounded (the rim must not balloon off the flat " +
                "spawn-plateau where the +0.10u reground clearance — and thus whole-rim grounding — holds)");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 #130 ROUND 5) — the raised PondBank collar MESH is GONE ========
        // The raised PondBank ring mesh was the PROVEN white-shoreline-ring source (the #130 round-5
        // -verifyPondDiag, build e5207d1: toggle c "collar/bank REMOVED" made the pale ring VANISH while bloom-
        // off / sea-off / foam-off-water all left it present). Its draped bowl-wall facets read pale/washed under
        // the warm key. Sponsor verbatim: "REMOVE the raised collar entirely — no bank ring mesh." This guards
        // the bug CLASS: a future re-add of any raised PondBank collar mesh reds here. (The collar is now PAINTED
        // into the terrain vertex colour — asserted by PondCollar_PaintedIntoTerrain_NotARaisedMesh below.)
        [Test]
        public void BootScene_HasNoRaisedPondBankCollarMesh()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond, "the pond must be present");

            // NO child named "PondBank" + NO mesh named "LP_PondBank" anywhere under the pond.
            var bankGo = pond.transform.Find("PondBank");
            Assert.IsNull(bankGo,
                "the pond must NOT carry a raised PondBank collar GameObject — it was the #130 round-5 PROVEN " +
                "white-shoreline-ring source (Sponsor: 'REMOVE the raised collar entirely — no bank ring mesh'). " +
                "The collar is now PAINTED into the terrain vertex colour (LowPolyZoneGen.PondCollarGreen).");
            var bankMesh = FindChildMeshNamed(pond.transform, "LP_PondBank");
            Assert.IsNull(bankMesh,
                "no LP_PondBank ring mesh may exist under the pond — a re-add of the raised collar mesh re-opens " +
                "the white-ring defect (its draped bowl-wall facets read pale/washed under the warm key, #130 round 5)");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 #130 ROUND 5) — the collar is a FLAT terrain-painted ring =======
        // The collar is now a FLAT darker-green ring painted into the TERRAIN vertex colour (no raised geometry,
        // no shadow lip — Sponsor: "I should walk on the green at ground level"). Assert PondCollarPaintWeight is
        // a real ring (full across the bowl wall + mouth band, 0 well beyond it = seed-42 grass unchanged) and
        // PondCollarGreen is a darker meadow green that CANNOT bloom (all channels well below the bloom threshold
        // even under the warm key). Pure-function check (the paint is deterministic; HeightAtRadial/IslandColorAt
        // are the single source of truth the terrain mesh derives from).
        [Test]
        public void PondCollar_PaintedIntoTerrain_NotARaisedMesh()
        {
            // (a) the paint is a real RING: full collar at the bowl mouth, eased to 0 a short way past it,
            // EXACTLY 0 far away (seed-42 grass byte-unchanged).
            float atMouth = LowPolyZoneGen.PondCollarPaintWeight(
                LowPolyZoneGen.PondCenterX, LowPolyZoneGen.PondCenterZ + LowPolyZoneGen.PondBowlOuterRadius - 0.2f);
            Assert.AreEqual(1f, atMouth, 1e-4f,
                "the collar paint must be FULL across the bowl wall + mouth band (a clear darker-green ring)");
            float farAway = LowPolyZoneGen.PondCollarPaintWeight(60f, -40f);
            Assert.AreEqual(0f, farAway, 1e-6f,
                "the collar paint must be EXACTLY 0 far from the pond — the seed-42 grass elsewhere is byte-unchanged");
            float pastFade = LowPolyZoneGen.PondCollarPaintWeight(
                LowPolyZoneGen.PondCenterX, LowPolyZoneGen.PondCenterZ +
                LowPolyZoneGen.PondBowlOuterRadius + LowPolyZoneGen.PondCollarPaintFade + 0.5f);
            Assert.AreEqual(0f, pastFade, 1e-6f,
                "the collar paint must be EXACTLY 0 just past the fade band — a LOCAL ring, not an island-wide warp");

            // (b) PondCollarGreen is a DARKER meadow green that cannot bloom (every channel ≪ 1.0 even × the warm
            // key intensity 1.25 + ambient ≈ 0.5; the OLD raised collar bloomed to the pale ring — the new flat
            // paint must read green, never white). Assert G dominates (a green) + all channels bounded low.
            Color c = LowPolyZoneGen.PondCollarGreen;
            Assert.Greater(c.g, c.r + 0.05f, "the collar paint must read GREEN (G > R) — a darker meadow green");
            Assert.Greater(c.g, c.b + 0.05f, "the collar paint must read GREEN (G > B), not a pale/neutral wash");
            Assert.Less(c.r, 0.40f, "the collar green's R must stay low (no warm wash that blooms)");
            Assert.Less(c.g, 0.55f, "the collar green's G must stay well under the bloom threshold even when lit");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 #130 ROUND 6 → strengthened ROUND 7) — the SEA does NOT render
        // through the pond bowl, INCLUDING edge/vertex slivers ===
        // PROVEN root cause of the persistent white shoreline ring (-verifyPondDiag toggle isolation: sea-plane-OFF
        // dropped the overhead annulus-white 0.215 -> 0.000; bloom-off + collar-removed both LEFT it). The sea
        // (Water_Play) is a world-spanning plane at WaterY (-0.20); the pond bowl is carved DOWN to a water surface
        // at -0.35, so the sea plane was EXPOSED inside the bowl and read as a PALE WHITE RING from overhead.
        // FIX: hole the sea plane over the pond footprint. ROUND 7 STRENGTHENING — the round-6 fix (and this test's
        // OLD form) used a CENTROID test, which was BLIND to the actual ship bug: the coarse ~8.75u sea cells are
        // larger than the whole 5.4u bowl, so a tri whose CENTROID sat just outside the cut radius still BLANKETED
        // the bowl from one side (a sea sliver at the waterline on the lobe azimuths → 0.182 pale at rNorm 0.30).
        // The centroid guard passed while the bug shipped — the classic proxy-predicate silent killer. This test
        // now asserts the SAME robust OVERLAP predicate the fix uses (closest XZ point on the tri to the pond
        // centre within SeaHoleCutRadius) so it catches an edge/vertex sliver, not only a centroid hit. Guards the
        // bug CLASS: any future change re-emitting sea geometry that TOUCHES the footprint reds HERE — the
        // down-angle / side-profile gates were physically blind to it; only the overhead view caught the percept.
        [Test]
        public void BootScene_SeaPlane_HasNoTrianglesOverThePondFootprint_NoSeaThroughBowl()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var sea = GameObject.Find("Water_Play");
            Assert.IsNotNull(sea, "the salt-sea plane (Water_Play) must exist in the shipped scene");
            var mf = sea.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "Water_Play must carry a MeshFilter");
            var mesh = mf.sharedMesh;
            Assert.IsNotNull(mesh, "Water_Play must carry a mesh");

            // Sea verts are in the object's local space; the object sits at (0, WaterY, 0) with identity rotation,
            // so localXZ == worldXZ. Walk every triangle; assert NO triangle OVERLAPS the pond footprint (closest
            // XZ point on the tri to the pond centre within SeaHoleCutRadius). A surviving overlapping tri is the
            // sea showing through the bowl — the #130 white-ring source. Using the SAME overlap predicate as the
            // BuildIslandWater cut (not a weaker centroid test) is what makes this guard catch the sliver class.
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            int offenders = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                if (LowPolyZoneGen.SeaTriOverlapsPondFootprint(a, b, c)) offenders++;
            }
            Assert.AreEqual(0, offenders,
                $"the sea plane must have NO triangles OVERLAPPING the pond footprint (centre " +
                $"{LowPolyZoneGen.PondCenterX},{LowPolyZoneGen.PondCenterZ} cut radius " +
                $"{LowPolyZoneGen.SeaHoleCutRadius:F2}u) — {offenders} found. A surviving overlapping tri is the " +
                "sea showing through the carved bowl = the PROVEN #130 white-ring source (diag: sea-OFF killed the " +
                "ring). The cutout in LowPolyZoneGen.BuildIslandWater (EmitTri closest-point overlap skip) must " +
                "hole the sea over the pond. NOTE: a CENTROID test passes while edge/vertex slivers ship — the " +
                "round-7 crescent bug; this guard uses the overlap predicate precisely to catch that class.");
        }

        // === UNIT GUARD (ticket 86cadj4g7 #130 ROUND 7) — the overlap predicate catches the SLIVER class a
        // centroid test misses. Pure math, no scene. This is the test that would have CAUGHT the round-6 bug. ===
        [Test]
        public void SeaTriOverlap_DetectsEdgeCrossingSliver_ACentroidTestWouldMiss()
        {
            float cx = LowPolyZoneGen.PondCenterX, cz = LowPolyZoneGen.PondCenterZ;
            float cut = LowPolyZoneGen.SeaHoleCutRadius;

            // A long thin sea triangle whose THREE vertices all sit OUTSIDE the cut radius AND whose CENTROID is
            // OUTSIDE the cut radius too, but whose EDGE a-b spans right across the pond centre (one side of the
            // bowl to the other). The third vert c is far away on one side so the centroid is pulled well clear —
            // so a centroid test PASSES this tri (the round-6 silent killer) while the edge shaves through the
            // footprint. This is the exact class the closest-point overlap test must catch.
            Vector3 a = new Vector3(cx - 30f, 0f, cz - 0.2f);   // far one side
            Vector3 b = new Vector3(cx + 30f, 0f, cz + 0.2f);   // far other side — edge a-b crosses the centre
            Vector3 c = new Vector3(cx, 0f, cz + 60f);          // far away → centroid pulled well outside the cut

            // Sanity: all three verts are OUTSIDE the cut radius (a vertex test would not fire on a/b/c alone).
            Assert.Greater((new Vector2(a.x - cx, a.z - cz)).magnitude, cut, "vert a must be outside the cut");
            Assert.Greater((new Vector2(b.x - cx, b.z - cz)).magnitude, cut, "vert b must be outside the cut");
            Assert.Greater((new Vector2(c.x - cx, c.z - cz)).magnitude, cut, "vert c must be outside the cut");
            // Sanity: the centroid is OUTSIDE the cut radius (the round-6 centroid test would PASS this tri).
            float gx = (a.x + b.x + c.x) / 3f - cx, gz = (a.z + b.z + c.z) / 3f - cz;
            Assert.Greater(Mathf.Sqrt(gx * gx + gz * gz), cut,
                "centroid must be outside the cut radius — this is the sliver a centroid test misses");

            // The overlap predicate MUST flag it (edge a-b passes through the pond centre → distance 0 ≤ cut).
            Assert.IsTrue(LowPolyZoneGen.SeaTriOverlapsPondFootprint(a, b, c),
                "the overlap predicate must DROP a tri whose edge crosses the footprint even when all verts AND " +
                "the centroid are outside the cut radius — exactly the round-6 sliver class. If this fails the " +
                "sea will show through the pond bowl as a shoreline crescent.");

            // And a tri comfortably clear of the footprint must NOT be flagged (no over-cutting the open sea).
            Vector3 fa = new Vector3(cx + (cut + 20f), 0f, cz);
            Vector3 fb = new Vector3(cx + (cut + 30f), 0f, cz + 8f);
            Vector3 fc = new Vector3(cx + (cut + 28f), 0f, cz - 8f);
            Assert.IsFalse(LowPolyZoneGen.SeaTriOverlapsPondFootprint(fa, fb, fc),
                "a tri well clear of the footprint must survive — the hole must not eat the open sea");
        }

        [Test]
        public void BootScene_CarriesFreshwaterPondVerifyCapture_Serialized()
        {
            // The shipped-build pond capture component (FreshwaterPondVerifyCapture) must SERIALIZE into the
            // Boot scene — the component-in-source-but-not-in-scene trap (CaptureGate.cs shipped inert in
            // PR #6 the same way). If it isn't on the Boot object, a reviewer's -verifyPond re-run produces
            // ZERO pond frames (the exact silent failure the shipped-build capture gate exists to catch), so
            // "the pond renders fresh-blue + grounded in the shipped build" goes UNPROVEN. Inert at normal
            // play (no -verifyPond flag) — this only guards the verify path's presence, never gameplay.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var cap = FindInScene<FreshwaterPondVerifyCapture>(scene);
            Assert.IsNotNull(cap,
                "the Boot scene must carry FreshwaterPondVerifyCapture serialized onto the Boot object — " +
                "without it a -verifyPond re-run captures nothing (the component-in-source-but-not-in-scene " +
                "silent-killer the capture gate exists to prevent)");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 / #130 re-soak) — the water sits ABOVE the carved bowl FLOOR =
        // The pond is now in a CARVED BOWL (LowPolyZoneGen.PondDepressionDelta), not lifted above the terrain.
        // At the pond CENTRE the carved bowl floor sits BELOW the water surface (the knee-deep geometry), so
        // the water surface is still measurably ABOVE the terrain-at-centre — the visibility invariant holds
        // (the disc renders over the floor, never occluded) AND the player wading to the centre stands knee-
        // deep. Guards the bug CLASS both ways: the disc must not re-sink UNDER the floor (occlusion), and the
        // floor must be carved BELOW the surface (a knee-deep pool, not flush). (The vertex-colour test green-
        // passed through the original defect — it tests the wrong layer; THIS asserts the on-terrain geometry
        // that actually decides visibility + the wade depth.)
        [Test]
        public void Pond_WaterSurface_SitsAboveCarvedBowlFloor_NotOccluded_AndKneeDeep()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");
            var waterT = pond.transform.Find("PondWater");
            Assert.IsNotNull(waterT, "the pond must carry the PondWater surface child");
            float waterWorldY = waterT.position.y;

            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the play-space terrain (Ground_Play) must exist (the bowl is carved into it)");
            var col = ground.GetComponent<MeshCollider>();
            Assert.IsNotNull(col, "the terrain must carry a MeshCollider (the bowl-floor raycast surface)");

            Vector3 p = pond.transform.position;
            var ray = new Ray(new Vector3(p.x, 200f, p.z), Vector3.down);
            Assert.IsTrue(col.Raycast(ray, out RaycastHit hit, 400f),
                $"the terrain ray at the pond XZ ({p.x:F1},{p.z:F1}) must hit Ground_Play (the carved bowl floor)");
            float floorY = hit.point.y;

            // The water surface must sit ABOVE the carved floor by a clear margin — the disc renders over the
            // floor (never occluded), and the gap IS the WADE depth: the dispatch's "knee-deep 0.75u at the
            // centre" (the player's NavMesh-agent Y follows this floor), not a flush puddle. A floor band catches a
            // re-sink; a ceiling keeps it from being a deep WELL. ROUND 9: the carve is PondBowlFloorDrop (RECESS
            // 0.30 + WADE 0.75 = 1.05) with the water PondWadeDepth (0.75) up from the floor → expect ~0.75u here
            // (bounded 0.2..0.9). The WADE rose 0.45→0.75 (the SUNK percept is now this DEPTH below the waterline,
            // per the dispatch redefine), while the RECESS dropped 0.75→0.30 so the dry shore lip stays a thin
            // traversable step-over (fill to ≈0.90 of the mouth, no walkable dry slope).
            float depth = waterWorldY - floorY;
            Assert.Greater(depth, 0.20f,
                $"the pond WATER surface (worldY {waterWorldY:F3}) must sit clearly ABOVE the carved bowl floor " +
                $"({floorY:F3}) — depth {depth:F3} reads knee-deep + un-occluded (the #130 defect was a flush/" +
                "lifted disc; the bowl carve recesses it). LowPolyZoneGen.PondDepressionDelta + GroundPondInBowl.");
            Assert.Less(depth, 0.90f,
                $"the pond water depth ({depth:F3}) must stay KNEE-deep (the dispatch's 0.75u target), not a deep " +
                "well — the player wades in, not swims (bounded by PondWaterDepthAboveFloor; a runaway depth means " +
                "the bowl/grounding drifted).");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 #130 ROUND 8) — the WATER FILLS THE CARVED BOWL TO ITS RIM ======
        // The round-7 defect (Sponsor soak): the carved bowl (floor radius 3.0u → wall up to the rim at
        // PondBowlOuterRadius 5.4u) was LARGER than the water disc (old nominal 2.6u × rim 0.82–1.18 = 2.13–3.07u,
        // entirely on the floor/lower wall), so a DRY carved margin showed between the water edge and the hole rim
        // — the player walked DOWN that dry slope into a smaller pool. THE FIX: the disc must reach the WATERLINE
        // (where the bowl wall rises to meet the knee-deep water surface, LowPolyZoneGen.PondWaterlineRadius ≈ 4.0u)
        // on EVERY azimuth — the disc MIN reach (nominal × min rim factor) must clear it, so the visible (terrain-
        // clipped) shoreline lands at the wall, NOT short of it. This guards the bug CLASS: a future shrink of the
        // disc (or a deeper recess that pushes the waterline out) that re-opens a dry margin reds HERE — BEFORE a
        // soak. Pure geometry (no scene): the recess solver + the disc-builder's rim bound. (The -verifyPond
        // FILL-TO-RIM gate is the shipped-pixel sibling; this is the cheap headless EditMode catch.)
        [Test]
        public void Pond_WaterDisc_FillsTheCarvedBowl_ToItsWaterline_NoDryMargin()
        {
            // The waterline (ROUND 9 — the TWO-SEGMENT wall): now DEFINED as PondWaterlineFillFraction (0.90) × the
            // bowl mouth (5.4u) ≈ 4.86u, and the wall is built so carve(waterline) == −PondRecessKneeDeep exactly
            // (the gentle submerged lower bowl meets the steep dry lip there). Well inside the bowl mouth (5.4u),
            // well outside the floor edge (3.0u) — a thin steep dry lip from here to the rim, no walkable dry slope.
            float waterline = LowPolyZoneGen.PondWaterlineRadius;
            Assert.That(waterline, Is.InRange(LowPolyZoneGen.PondBowlInnerRadius, LowPolyZoneGen.PondBowlOuterRadius),
                $"the waterline ({waterline:F3}u) must sit between the floor edge ({LowPolyZoneGen.PondBowlInnerRadius}u) " +
                $"and the bowl mouth ({LowPolyZoneGen.PondBowlOuterRadius}u) — it's where the wall meets the water surface");
            float expectedWaterline = LowPolyZoneGen.PondWaterlineFillFraction * LowPolyZoneGen.PondBowlOuterRadius;
            Assert.That(waterline, Is.EqualTo(expectedWaterline).Within(1e-3f),
                $"the waterline must equal PondWaterlineFillFraction × the bowl mouth ({expectedWaterline:F3}u, got " +
                $"{waterline:F3}u) — the round-9 fill fraction (0.90) DEFINES the waterline; a drift means the fill " +
                "fraction or the mouth re-tuned — re-check the disc radius + the calibration-test constants");
            // The fill fraction itself must clear the FINALIZED FILL-TO-RIM gate bar (≥0.85) so the -verifyPond gate
            // (RimFillFraction 0.85) passes: a future shrink of the fill fraction below the gate reds HERE. The
            // geometry fill fraction stays 0.90 (Sponsor-approved round-9 look); the gate bar was finalized 0.88 → 0.85
            // (#130 FINALIZE) because the shipped pixel-measured waterline lands at 0.88-epsilon (capture noise under
            // the geometric 0.90) — 0.85 PASSES the approved fill yet still catches a real dry-margin regression.
            Assert.GreaterOrEqual(LowPolyZoneGen.PondWaterlineFillFraction, 0.85f,
                $"the pond fill fraction ({LowPolyZoneGen.PondWaterlineFillFraction:F2}) must be ≥ 0.85 (the -verifyPond " +
                "FILL-TO-RIM gate bar) — the water must fill the bowl to a thin steep lip, no walkable dry slope (#130 round 9)");

            // The disc's MINIMUM reach across all azimuths = the actual scene radius × the min organic rim factor.
            // It must CLEAR the waterline so NO azimuth leaves a dry crescent (the round-7 defect). Read the scene's
            // shipped disc radius from the built mesh (the radial-fan max rim radius is the disc's outermost reach;
            // its min is the disc's tightest lobe) so this tracks the actual authored PondSurfaceRadius, not a literal.
            var mesh = MovementCameraScene.BuildShippedPondWaterMeshForTest();
            float minRim = float.MaxValue, maxRim = 0f;
            foreach (var v in mesh.vertices)
            {
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (r < 0.01f) continue; // skip the centre verts
                if (r < minRim) minRim = r;
                if (r > maxRim) maxRim = r;
            }
            Assert.Greater(minRim, waterline,
                $"the water disc MIN reach ({minRim:F3}u) must exceed the waterline ({waterline:F3}u) so the water " +
                "fills the bowl to the wall on EVERY azimuth — a min reach short of the waterline leaves a DRY carved " +
                "margin (the #130 round-7 dry-slope defect). Raise PondSurfaceRadius.");

            // The disc MAX reach must REACH PAST the bowl mouth so the overshoot is submerged in the wall + terrain-
            // occluded (a clean terrain-clipped shoreline), but the overshoot is harmless because the disc Y (recess
            // below the plateau) is everywhere ≤ the terrain there. Just assert it clears the waterline comfortably.
            Assert.Greater(maxRim, waterline,
                $"the water disc MAX reach ({maxRim:F3}u) must clear the waterline ({waterline:F3}u)");
        }

        // === NEW (ticket 86cadj4g7 #130) — the bowl is a LOCAL carve; the seed-42 island is UNCHANGED away ===
        // The depression must be a deliberate LOCAL dip at the pond ONLY: full depth at the centre, zero past
        // PondBowlOuterRadius. This guards the seed-42 silhouette lock — a carve that bled across the island
        // would perturb the locked outline / scatter / NavMesh. Pure-function check (no scene needed): sample
        // PondDepressionDelta at the centre (deep), at the mouth (≈0), and far away (exactly 0).
        [Test]
        public void PondDepression_IsLocalCarve_ZeroAwayFromTheBowl_Seed42Unchanged()
        {
            // At the pond CENTRE: the full floor drop (a real bowl).
            float atCentre = LowPolyZoneGen.PondDepressionDelta(LowPolyZoneGen.PondCenterX, LowPolyZoneGen.PondCenterZ);
            Assert.Less(atCentre, -0.30f,
                $"the bowl must carve a real depression at the pond centre (delta {atCentre:F3} ≪ 0) — a recessed " +
                "bowl, not a flush disc (Sponsor #130: 'carve a recessed bowl')");
            Assert.AreEqual(-LowPolyZoneGen.PondBowlFloorDrop, atCentre, 1e-4f,
                "the bowl centre delta must equal −PondBowlFloorDrop (the flat floor at full depth)");

            // JUST past the bowl mouth + FAR away: EXACTLY zero — the island elsewhere is UNTOUCHED (seed-42 lock).
            float justOutside = LowPolyZoneGen.PondDepressionDelta(
                LowPolyZoneGen.PondCenterX + LowPolyZoneGen.PondBowlOuterRadius + 0.5f, LowPolyZoneGen.PondCenterZ);
            Assert.AreEqual(0f, justOutside, 1e-6f,
                "the carve must be EXACTLY 0 just past PondBowlOuterRadius — a local dip, not an island-wide warp");
            float farAway = LowPolyZoneGen.PondDepressionDelta(60f, -40f); // a random inland point well off the pond
            Assert.AreEqual(0f, farAway, 1e-6f,
                "the carve must be EXACTLY 0 far from the pond — the seed-42 island silhouette/scatter/NavMesh " +
                "elsewhere are unchanged (the depression is a LOCAL deliberate carve at the pond ONLY)");
        }

        // === NEW (ticket 86cadj4g7 #130 re-soak — the ASYMMETRIC-MOUND bug class) ============================
        // The defect: the pond rendered as a one-sided MOUND (pond_b yaw=70° bulged, pond_c yaw=-70° sunk). ROOT:
        // the bowl carve is a FIXED depth and GroundPondInBowl grounds the water on the CENTRE floor, but the
        // residual Perlin HILLS still added ~0.5u of AZIMUTHALLY-VARYING elevation across the pond footprint —
        // so on the low-hill azimuth the centre-grounded water surface landed ABOVE the local rim (a mound on
        // that side). This test guards the bug CLASS, not the instance: sample the ACTUAL carved terrain height
        // (HeightAtRadial, with the new PondHillFlatten applied) all the way around the rim at MANY azimuths and
        // assert the water surface sits BELOW the surrounding terrain on EVERY side. A future regression that
        // re-introduces azimuthal rim variation > the wade margin (e.g. removing PondHillFlatten, or deepening
        // the hills) reds HERE — which the old centre-only depth test (PondBowl_FloorIsBelowWaterSurface) and the
        // colour metric both MISSED (a mound passes both). Pure-function check (no scene; HeightAtRadial +
        // PondDepressionDelta are the single source of truth the carve/collider/NavMesh all derive from).
        [Test]
        public void PondRim_WaterSurfaceBelowSurroundingTerrain_OnEveryAzimuth_NotAMound()
        {
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out float ox, out float oz);
            float cx = LowPolyZoneGen.PondCenterX, cz = LowPolyZoneGen.PondCenterZ;

            // The grounded water surface world Y = carved FLOOR at the centre + the wade depth (this is exactly
            // what WorldBootstrap.GroundPondInBowl computes: floorY + PondWadeDepth, then water child sits there).
            float floorYCentre = LowPolyZoneGen.HeightAtRadial(cx, cz, ox, oz);
            float waterSurfaceY = floorYCentre + LowPolyZoneGen.PondWadeDepth;

            // Walk the SURROUNDING terrain just past the bowl mouth (where delta=0 → the real local ground the
            // collar must end flush with) at 72 azimuths (every 5°). Every sample must sit ABOVE the water
            // surface — i.e. the player looks DOWN into a hole from any direction; no side bulges above the water.
            float rimSampleR = LowPolyZoneGen.PondBowlOuterRadius + 0.05f; // just outside the mouth: undisturbed ground
            float worstAboveWater = float.MaxValue; float worstAzimuth = 0f;
            int below = 0;
            for (int deg = 0; deg < 360; deg += 5)
            {
                float a = deg * Mathf.Deg2Rad;
                float wx = cx + Mathf.Cos(a) * rimSampleR;
                float wz = cz + Mathf.Sin(a) * rimSampleR;
                float terrainY = LowPolyZoneGen.HeightAtRadial(wx, wz, ox, oz);
                float margin = terrainY - waterSurfaceY; // > 0 means the ground rises above the water (a hole)
                if (margin < worstAboveWater) { worstAboveWater = margin; worstAzimuth = deg; }
                if (margin > 0f) below++;
            }

            Assert.AreEqual(72, below,
                $"the surrounding terrain must sit ABOVE the water surface on ALL 72 azimuths (the pool is a HOLE " +
                $"the player looks DOWN into from any direction). The WORST azimuth was {worstAzimuth:F0}° with the " +
                $"ground only {worstAboveWater:F3}u above the water (≤0 = the water bulges above that side = the " +
                $"#130 MOUND). waterSurfaceY={waterSurfaceY:F3} floorYCentre={floorYCentre:F3}. Cause-fix: " +
                "PondHillFlatten must keep the rim a uniform plateau so the centre-grounded water is below it.");
            Assert.Greater(worstAboveWater, 0.05f,
                $"even the LOWEST surrounding-ground azimuth ({worstAzimuth:F0}°) must clear the water by a margin " +
                $"(>0.05u, got {worstAboveWater:F3}u) so the rim reads unambiguously ABOVE the water from every " +
                "side — not flush/borderline (a borderline rim still reads as a flush lens on the soak).");
        }

        // === NEW (ticket 86cadj4g7 #130 re-soak) — the COLLAR is FLUSH, not a raised berm =====================
        // The #130 re-soak defect: the green collar/bank ring sat ABOVE the surrounding grass as a raised berm
        // casting a shadow on its outer edge. ROOT (diagnose-via-trace, seed-42 numeric): PondHillFlatten levelled
        // the pond footprint hill to ZERO, so the bowl-mouth ground sat at baseH=0.15 while the surrounding hilly
        // grass sits ~0.40 — the fade collar (4.8->7.8u) RAMPED UP 0.15->0.44, a raised rim. FIX: level the
        // footprint toward FootprintHillLevel (the LOCAL surrounding hill), so the mouth plateau sits AT the local
        // ground and the fade band is nearly flat (no rising rim). This guards the bug CLASS: sample the terrain
        // height at the bowl-MOUTH (the collar outer edge pins here) vs the FAR surrounding ground (well past the
        // fade collar) on MANY azimuths; the mouth must be ~LEVEL with the far ground (a small dip is fine — the
        // ground dipping into the hole — but the mouth must NOT sit ABOVE the far ground = the berm). A regression
        // that re-levels the footprint to a fixed plateau below the local hills (re-introducing the rising rim)
        // reds HERE. Pure-function check (HeightAtRadial is the single source of truth the collar pins to).
        [Test]
        public void PondCollar_MouthGroundFlushWithSurroundingTerrain_NotARaisedBerm_OnEveryAzimuth()
        {
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out float ox, out float oz);
            float cx = LowPolyZoneGen.PondCenterX, cz = LowPolyZoneGen.PondCenterZ;

            // The bowl MOUTH ground (where the collar outer edge pins, flatten≈0) vs the FAR surrounding ground
            // (well beyond the fade collar, flatten=1 — the real local hills). The collar reads as a raised berm
            // iff the mouth ground sits ABOVE the far ground (the green crests above the surrounding grass).
            float mouthR = LowPolyZoneGen.PondBowlOuterRadius;                                   // 4.8 — collar outer edge
            float farR   = LowPolyZoneGen.PondBowlOuterRadius + LowPolyZoneGen.PondRimFlattenFade + 1.5f; // ~9.3 — undisturbed local ground
            float worstBerm = float.MinValue; float worstAz = 0f;
            int flush = 0;
            for (int deg = 0; deg < 360; deg += 5)
            {
                float a = deg * Mathf.Deg2Rad;
                float mouthH = LowPolyZoneGen.HeightAtRadial(cx + Mathf.Cos(a) * mouthR, cz + Mathf.Sin(a) * mouthR, ox, oz);
                float farH   = LowPolyZoneGen.HeightAtRadial(cx + Mathf.Cos(a) * farR,   cz + Mathf.Sin(a) * farR,   ox, oz);
                float berm = mouthH - farH;     // > 0 means the collar-edge ground rises above the surrounding grass (a berm)
                if (berm > worstBerm) { worstBerm = berm; worstAz = deg; }
                if (berm <= 0.10f) flush++;     // the mouth is at/below the far ground (flush) within a 0.10u tolerance
            }
            Assert.AreEqual(72, flush,
                $"the collar-edge (bowl-mouth) ground must be FLUSH with — not a raised berm above — the surrounding " +
                $"terrain on ALL 72 azimuths. The WORST azimuth was {worstAz:F0}° with the mouth {worstBerm:F3}u ABOVE " +
                $"the far ground (>0.10u = a raised green rim casting a shadow, the #130 re-soak defect). Cause-fix: " +
                "FootprintHillLevel must level the footprint to the LOCAL hill, not to a fixed sub-hill plateau.");
        }

        // === NEW (ticket 86cadj4g7 #130) — the rim-hill flatten is a LOCAL no-op outside its collar ===========
        // PondHillFlatten suppresses the hills inside the pond footprint to kill the azimuthal mound. It MUST be
        // a strict no-op (weight == 1) beyond PondBowlOuterRadius + PondRimFlattenFade so the seed-42 island
        // silhouette / scatter / NavMesh elsewhere are byte-IDENTICAL to c7da32d (the silhouette lock). Guards
        // that a future widening of the flatten can't silently warp the locked island.
        [Test]
        public void PondHillFlatten_IsLocal_NoOpBeyondTheFadeCollar_Seed42Unchanged()
        {
            float cx = LowPolyZoneGen.PondCenterX, cz = LowPolyZoneGen.PondCenterZ;
            // Inside the bowl mouth: hills fully OFF (uniform rim plateau).
            Assert.AreEqual(0f, LowPolyZoneGen.PondHillFlatten(cx, cz), 1e-6f,
                "the hill flatten must be 0 (hills OFF) at the pond centre — a uniform rim plateau");
            // Just past the mouth, still inside the fade collar: a partial flatten (0..1) — a smooth easing, no crease.
            float midR = LowPolyZoneGen.PondBowlOuterRadius + LowPolyZoneGen.PondRimFlattenFade * 0.5f;
            float mid = LowPolyZoneGen.PondHillFlatten(cx + midR, cz);
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f),
                $"the hill flatten must ease (0..1) across the fade collar — no hard crease at the mouth (got {mid:F3})");
            // Beyond the collar + far away: EXACTLY 1 (hills full — the island is byte-unchanged, seed-42 lock).
            float beyondR = LowPolyZoneGen.PondBowlOuterRadius + LowPolyZoneGen.PondRimFlattenFade + 0.5f;
            Assert.AreEqual(1f, LowPolyZoneGen.PondHillFlatten(cx + beyondR, cz), 1e-6f,
                "the hill flatten must be EXACTLY 1 (no-op) just past the fade collar — the island silhouette there is unchanged");
            Assert.AreEqual(1f, LowPolyZoneGen.PondHillFlatten(60f, -40f), 1e-6f,
                "the hill flatten must be EXACTLY 1 far from the pond — the seed-42 island elsewhere is byte-identical");
        }

        // === REWORKED (ticket 86cadj4g7 #130 ROUND 9) — the WHOLE wall stays NavMesh-TRAVERSABLE (wade-in
        // intact) AND the DRY band is a SHORT thin lip (no LONG walkable dry slope) ===========================
        // ROUND-9 TWO-SEGMENT wall (Sponsor round-8 soak "step over the shore straight INTO knee-deep water — NO
        // walkable dry slope"). HARD CONSTRAINT (traced from the bake settings — MovementCameraScene
        // ConfigureIslandNavSettings / WorldBootstrap bake at the DEFAULT agent maxSlope 45°): the player wades in
        // via the NavMeshAgent (WasdMovement drives agent.velocity), so the WHOLE bowl wall — floor up to the rim —
        // must stay ≤ the 45° agent max or the floor becomes an UNREACHABLE NavMesh island (can't wade in; the
        // RoundIslandNavCoveragePlayModeTests pond coverage drops). So a STEEP un-bakeable lip is NOT an option.
        // Instead the "no walkable dry slope" is achieved by making the dry band SHORT, not steep:
        //   (1) SUBMERGED LOWER BOWL [inner, waterline] — carries the WADE drop (PondWadeDepth 0.75u) over the long
        //       inner→waterline run → gentle (~31° peak); the player wades IN down this, NavMesh covers it.
        //   (2) DRY SHORE LIP [waterline, outer] — carries the small recess (PondRecessKneeDeep 0.30u) over the
        //       SHORT waterline→outer run (~0.54u) → ~40° peak (still under the 45° agent max → BAKES, the wall
        //       stays connected) but THIN: you cross it in a step, you do NOT walk down a long slope into the pool.
        // So this test asserts (a) BOTH segments are NavMesh-traversable (< the 45° agent max, with margin), and
        // (b) the dry band is SHORT relative to the submerged run (a thin step-over lip, not a long dry slope).
        [Test]
        public void PondBowl_WallTraversableForWadeIn_DryLipIsShort_NotALongDrySlope()
        {
            float waterline = LowPolyZoneGen.PondWaterlineRadius;

            // (a1) LOWER (submerged, walkable) wall: floor → water surface = the WADE drop over inner→waterline.
            float lowerRun = waterline - LowPolyZoneGen.PondBowlInnerRadius;
            Assert.Greater(lowerRun, 0f, "the lower bowl wall must have a positive run (waterline > inner radius)");
            float lowerDrop = LowPolyZoneGen.PondWadeDepth; // floor → water surface
            // smoothstep's max gradient is 1.5× its average → the steepest local slope on each segment:
            float lowerMaxSlopeDeg = Mathf.Atan2(1.5f * lowerDrop, lowerRun) * Mathf.Rad2Deg;
            Assert.Less(lowerMaxSlopeDeg, 42f,
                $"the SUBMERGED LOWER bowl wall's steepest slope ({lowerMaxSlopeDeg:F1}°) must stay under the 45° " +
                "NavMesh agent max — the player WADES IN down this, so the bake must cover it (ticket 86cadj4g7).");

            // (a2) DRY SHORE LIP: water surface → plateau = the recess over the short waterline→outer run. Must
            // ALSO be traversable (< 45° agent max) so it BAKES — a steeper lip would NOT bake and the floor would
            // become an unreachable NavMesh island (the wade-in + coverage break). A small margin below 45°.
            float lipRun = LowPolyZoneGen.PondBowlOuterRadius - waterline;
            Assert.Greater(lipRun, 0f, "the dry shore lip must have a positive run (mouth > waterline)");
            float lipDrop = LowPolyZoneGen.PondRecessKneeDeep;
            float lipMaxSlopeDeg = Mathf.Atan2(1.5f * lipDrop, lipRun) * Mathf.Rad2Deg;
            Assert.Less(lipMaxSlopeDeg, 44f,
                $"the DRY shore lip's steepest slope ({lipMaxSlopeDeg:F1}°) must stay under the 45° NavMesh agent " +
                "max so it BAKES (a steeper lip → the bowl floor becomes an unreachable NavMesh island; the wade-in " +
                "+ RoundIslandNavCoveragePlayModeTests break). The 'no walkable dry slope' comes from the lip being " +
                "SHORT, not steep.");

            // (b) the dry band is a SHORT thin lip, NOT a long walkable dry slope: its run must be clearly less than
            // the submerged run AND a small fraction of the bowl radius. A regression to a long dry slope (the
            // round-8 defect: waterline at ~0.74 of the mouth left a ~1.4u dry band) reds here.
            Assert.Less(lipRun, lowerRun * 0.5f,
                $"the DRY shore lip run ({lipRun:F3}u) must be well under half the submerged lower-wall run " +
                $"({lowerRun:F3}u) — a thin step-over lip, NOT a long walkable dry slope (the #130 round-8 defect).");
            Assert.Less(lipRun, 0.75f,
                $"the DRY shore lip run ({lipRun:F3}u) must be short in absolute terms (< 0.75u) — you STEP OVER it " +
                "in one stride; a wide dry band is the round-8 walkable-dry-slope the Sponsor rejected.");
        }

        // === REWORKED (ticket 86cadj4g7 #130 ROUND 5) — the collar is FLAT terrain paint, walkable at ground ===
        // The raised PondBank collar MESH (the white-ring source) is GONE; the collar is now a flat darker-green
        // ring PAINTED into the terrain vertex colour. So "walk on the green at ground level, no raised lip" is
        // satisfied BY CONSTRUCTION — there is no collar geometry to rise above the terrain. This test pins that
        // the terrain ACTUALLY carries the painted darker-green ring (the bowl-wall + mouth band reads as the
        // PondCollarGreen, distinct from the surrounding grass) AND that the painted band coincides with WALKABLE
        // terrain at ground level (raycast the collar band → it hits the carved terrain, never floating geometry).
        [Test]
        public void Pond_Collar_IsFlatTerrainPaint_WalkableAtGroundLevel_NoRaisedGeometry()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");

            var ground = GameObject.Find("Ground_Play");
            var col = ground != null ? ground.GetComponent<MeshCollider>() : null;
            var mf = ground != null ? ground.GetComponent<MeshFilter>() : null;
            Assert.IsNotNull(col, "the terrain MeshCollider must exist (the collar paints onto the terrain mesh)");
            Assert.IsNotNull(mf, "the terrain MeshFilter must exist to read the painted vertex colours");
            var mesh = mf.sharedMesh;
            Assert.IsNotNull(mesh, "the terrain mesh must exist");

            // (1) NO raised collar geometry: there is no PondBank child to float above the terrain (the white-ring
            // source is gone). Asserted structurally in BootScene_HasNoRaisedPondBankCollarMesh; re-pin here.
            Assert.IsNull(pond.transform.Find("PondBank"),
                "there must be no PondBank collar GameObject — the collar is FLAT terrain paint, not raised geometry");

            // (2) the collar band coincides with WALKABLE terrain at ground level: raycast straight down through a
            // point in the painted collar band (just inside the bowl mouth) — it must hit the carved Ground_Play
            // terrain (the player walks on green at ground level), never empty space or floating geometry.
            float cx = LowPolyZoneGen.PondCenterX, cz = LowPolyZoneGen.PondCenterZ;
            float bandR = LowPolyZoneGen.PondBowlOuterRadius - 0.3f; // inside the mouth, in the full-paint band
            var ray = new Ray(new Vector3(cx + bandR, 200f, cz), Vector3.down);
            Assert.IsTrue(col.Raycast(ray, out RaycastHit hit, 400f),
                "the painted collar band must sit on WALKABLE carved terrain (the ray must hit Ground_Play) — " +
                "the player walks ON the green at ground level (ticket 86cadj4g7 #130 round 5)");

            // (3) the terrain ACTUALLY carries the painted darker-green ring: at least one terrain vertex inside
            // the collar paint band must read clearly GREENER + DARKER than the surrounding grass (the paint took).
            // Sample the nearest terrain verts to the collar band and assert one leans toward PondCollarGreen.
            var verts = mesh.vertices;
            var cols = mesh.colors;
            Assert.AreEqual(verts.Length, cols.Length, "the terrain mesh must carry a vertex colour per vertex");
            bool collarPainted = false;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i]; // terrain root at origin → world == local
                float w = LowPolyZoneGen.PondCollarPaintWeight(v.x, v.z);
                if (w < 0.5f) continue; // only verts inside the full-paint collar band
                Color c = cols[i];
                // The collar green is darker + greener than GrassHi/GrassLo: G dominates, and it is DARKER (lower
                // luma) than mid grass. A vert that took the paint reads close to PondCollarGreen.
                if (c.g > c.r + 0.05f && c.g > c.b + 0.05f && c.r < 0.34f) { collarPainted = true; break; }
            }
            Assert.IsTrue(collarPainted,
                "the terrain must carry the painted darker-green collar ring — at least one terrain vertex in the " +
                "collar band must read as PondCollarGreen (darker + greener than the surrounding grass). If this " +
                "reds, the terrain grid is too coarse to land a vert in the band, or IslandColorAt didn't apply " +
                "the collar paint (ticket 86cadj4g7 #130 round 5).");
        }

        // === NEW (ticket 86cadj4g7 #130) — the bank TUFTS read GREEN, not WHITE ==============================
        // The #130 white-grass defect: the bank tufts (GrassClump, which bakes NO vertex colours → white) used
        // _Tint = white on the LowPolyVertexColor shader (albedo = colour×_Tint = white×white = WHITE). Now they
        // must tint GREEN. Assert every BankTuft material's _Tint reads as a green (G dominates R and B) — a
        // revert to a white/neutral tint reds here.
        [Test]
        public void Pond_BankTufts_TintGreen_NotWhite()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");

            int tuftsChecked = 0;
            foreach (var mr in pond.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr.gameObject.name.StartsWith("BankTuft")) continue;
                tuftsChecked++;
                var mat = mr.sharedMaterial;
                Assert.IsNotNull(mat, "a bank tuft must carry a material");
                Assert.IsTrue(mat.HasProperty("_Tint"), "the bank tuft material must expose _Tint (LowPolyVertexColor)");
                Color tint = mat.GetColor("_Tint");
                // GREEN: G must clearly dominate R and B (a grass green) — white (R=G=B=1) fails both margins.
                Assert.Greater(tint.g, tint.r + 0.05f,
                    $"the bank tuft _Tint must read GREEN (G {tint.g:F2} > R {tint.r:F2}) — a white tint shipped " +
                    "WHITE tufts (GrassClump bakes no vertex colours; the #130 defect)");
                Assert.Greater(tint.g, tint.b + 0.05f,
                    $"the bank tuft _Tint must read GREEN (G {tint.g:F2} > B {tint.b:F2}), not white/neutral");
            }
            Assert.Greater(tuftsChecked, 0, "the pond must carry bank tufts to check (the BankTuft accents)");
        }

        // === REGRESSION GUARD (ticket 86cadr95t — #130 NIT #2, post-round-5 successor) — the bank ACCENTS DRAPE
        // FLUSH on the carved bowl wall, never floating proud / submerged ======================================
        // CONTEXT: the original #130 flush-collar test (Pond_BankCollar_DrapesOnBowlWall_FlushNotRaisedLip, with
        // its `if (v.y > -0.06f) continue;` cutoff Drew/Tess flagged as UNDER-checking the widest-lobe outer verts)
        // was REMOVED in #130 round 5 when the raised PondBank collar MESH was deleted (BootScene_HasNoRaisedPond
        // BankCollarMesh now pins its absence; the collar is FLAT terrain paint). So the cutoff to "tighten" no
        // longer exists — the under-check is GONE by construction. What STILL drapes on the carved wall is the
        // decorative bank ACCENTS (BankTuft0..4 + BankRock), placed via MovementCameraScene.CollarOuterLocalY,
        // which derives the wall Y DIRECTLY from PondDepressionDelta (no v.y cutoff). This guard is the living
        // successor to NIT #2's concern: it asserts every accent's BASE sits FLUSH on the carved Ground_Play
        // terrain — not floating above the sloped wall (the pre-#130 `y=0` flat-lip bug) nor sunk under it — at
        // EVERY accent (no vertex-subset cutoff that could skip an outer-lobe accent the way the old test did).
        [Test]
        public void Pond_BankAccents_DrapeFlushOnCarvedTerrain_NotFloatingNorSubmerged()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");

            var ground = GameObject.Find("Ground_Play");
            var col = ground != null ? ground.GetComponent<MeshCollider>() : null;
            Assert.IsNotNull(col, "Ground_Play MeshCollider must exist (the carved wall the accents drape on)");

            // Every BankTuft* + BankRock accent: raycast the carved terrain straight down through its world XZ and
            // assert its world-Y base sits within a tight band of the carved surface there. Tolerance covers the
            // CollarOuterLocalY vs MeshCollider voxel/raycast discretisation, but is far tighter than the pre-#130
            // float defect (accents sat at y=0 ≈ +0.30..+1.05u proud of the carved wall) — so a regression that
            // re-floats an accent (e.g. reverting CollarOuterLocalY to a flat lip) reds HERE. Checks ALL accents,
            // including the widest-lobe ones the old `v.y > -0.06f` cutoff skipped (NIT #2).
            const float flushTol = 0.20f;
            int accentsChecked = 0;
            foreach (var t in pond.GetComponentsInChildren<Transform>(true))
            {
                string n = t.gameObject.name;
                if (!n.StartsWith("BankTuft") && !n.StartsWith("BankRock")) continue;
                accentsChecked++;
                Vector3 w = t.position; // world position of the accent root (its base sits at the carved wall Y)
                var ray = new Ray(new Vector3(w.x, 200f, w.z), Vector3.down);
                Assert.IsTrue(col.Raycast(ray, out RaycastHit hit, 400f),
                    $"accent '{n}' at ({w.x:F2},{w.z:F2}) must sit over carved Ground_Play terrain (the wall it drapes on)");
                float dy = w.y - hit.point.y;
                Assert.That(dy, Is.InRange(-flushTol, flushTol),
                    $"the bank accent '{n}' must drape FLUSH on the carved bowl wall — its base worldY {w.y:F3} vs " +
                    $"carved terrain {hit.point.y:F3} (Δ {dy:F3}u) must stay within ±{flushTol}u. A large POSITIVE Δ " +
                    "is the pre-#130 floating-lip defect (accent at y=0 above the sloped wall); a large NEGATIVE Δ is " +
                    "an accent sunk under the wall. CollarOuterLocalY (← PondDepressionDelta) keeps it flush; this " +
                    "checks ALL accents incl. the widest-lobe ones the removed `v.y>-0.06f` cutoff skipped (86cadr95t).");
            }
            Assert.Greater(accentsChecked, 0,
                "the pond must carry bank accents (BankTuft*/BankRock) to check — the flush-drape successor to #130 NIT #2");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7) — the depth-fade foam must not FLOOD the flat disc ========
        // The shared depth-fade foam (foam = saturate(1 - gap/_FoamDistance)) fires UNIFORMLY across the disc
        // unless _FoamDistance is SMALLER than the water→floor gap — at the sea-scale _FoamDistance the pond
        // shipped PALE/near-white (the BROAD white surface band the Sponsor saw from above). With the DEEPER
        // BOWL CARVE (#130 third re-soak) the gap is now the wade water DEPTH (~0.45u above the carved floor) —
        // but the load-bearing OFF switch is the master _FoamAmount gate (asserted below), zeroing the whole
        // foam term regardless of the gap. This test reads the SCENE material AFTER the harness's bootstrap
        // regen; the COMMITTED-asset-ships-off guard (CommittedPondMaterialAsset_ShipsFoamOff_NotStale) is the
        // sibling that catches a STALE committed asset shipping foam ON (the actual #130 root cause).
        [Test]
        public void Pond_FoamDistance_BelowWaterTerrainGap_NoFoamFlood()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");
            var waterT = pond.transform.Find("PondWater");
            var wmr = waterT != null ? waterT.GetComponent<MeshRenderer>() : null;
            Assert.IsNotNull(wmr, "the pond water surface must carry a MeshRenderer");
            var mat = wmr.sharedMaterial;
            Assert.IsNotNull(mat, "the pond water must carry a material");

            // Only meaningful on the LowPolyWater shader (the depth-fade foam path). The fallback URP/Lit pond
            // has no foam, so the flood can't happen — skip the assert there.
            if (!mat.HasProperty("_FoamDistance"))
            {
                Assert.Pass("pond material has no _FoamDistance (URP/Lit fallback — no depth-fade foam to flood)");
                return;
            }
            float foamDistance = mat.GetFloat("_FoamDistance");

            // === #130 re-soak: the pond ships FOAM OFF (a STILL pool — "should not foam like the sea") ======
            // The DEFAULT pond foam is OFF (_FoamDistance == PondFoamOff == 0): the shader's
            // foam = saturate(1 - gap/max(distance,0.001)) is 0 everywhere for any gap > 0.001u → no foam ring
            // at all. This is the strongest no-flood guarantee (off, not just thin). A future re-tune that
            // turned foam back ON by default (sea-like) reds here.
            Assert.AreEqual(LowPolyZoneGen.PondFoamOff, foamDistance, 1e-4f,
                $"the pond must ship FOAM OFF by default (_FoamDistance {foamDistance:F3} must == " +
                $"PondFoamOff {LowPolyZoneGen.PondFoamOff:F3}) — Sponsor #130: 'the pond should not foam like the " +
                "sea; the freshwater pond must be STILL'. The live PondNudge [foam] handle dials it up for A/B.");

            // === #130 re-soak: the MASTER _FoamAmount gate must be 0 (the real OFF switch) ====================
            // _FoamDistance=0 alone leaves a razor white shoreline line at the gap≈0 bank intersection (foam=1
            // there). The master _FoamAmount==0 zeroes the WHOLE foam term incl. that ring — THIS is what removes
            // the shoreline foam the Sponsor still saw with FOAM:OFF. A revert that dropped the gate (or set it 1)
            // reds here. (The sea never sets _FoamAmount → defaults to 1 → keeps its foam; this is pond-only.)
            if (mat.HasProperty("_FoamAmount"))
                Assert.AreEqual(LowPolyZoneGen.PondFoamAmountOff, mat.GetFloat("_FoamAmount"), 1e-4f,
                    $"the pond must ship the master _FoamAmount gate OFF ({mat.GetFloat("_FoamAmount"):F3} must == " +
                    $"PondFoamAmountOff {LowPolyZoneGen.PondFoamAmountOff:F3}) — _FoamDistance=0 alone leaves the " +
                    "gap≈0 shoreline razor foam line; the master gate is what zeroes the whole foam term (#130 re-soak).");
            // The step->amount mapping the live nudge + bake share: off->0, light/sea-like->1.
            Assert.AreEqual(0f, LowPolyZoneGen.PondFoamAmountFor(LowPolyZoneGen.PondFoamOff), 1e-4f,
                "PondFoamAmountFor(off) must be 0 (master OFF)");
            Assert.AreEqual(1f, LowPolyZoneGen.PondFoamAmountFor(LowPolyZoneGen.PondFoamLight), 1e-4f,
                "PondFoamAmountFor(light) must be 1 (foam on, width per _FoamDistance)");
            Assert.AreEqual(1f, LowPolyZoneGen.PondFoamAmountFor(LowPolyZoneGen.PondFoamSeaLike), 1e-4f,
                "PondFoamAmountFor(sea-like) must be 1 (foam on, width per _FoamDistance)");

            // The actual water→terrain gap (the clearance the re-ground established) — even if a soak dials foam
            // up to LIGHT (0.06), this gap must dwarf it so the open disc never floods (only the bank band foams).
            var ground = GameObject.Find("Ground_Play");
            var col = ground != null ? ground.GetComponent<MeshCollider>() : null;
            Assert.IsNotNull(col, "Ground_Play MeshCollider must exist to measure the water→terrain gap");
            Vector3 p = pond.transform.position;
            Assert.IsTrue(col.Raycast(new Ray(new Vector3(p.x, 200f, p.z), Vector3.down), out RaycastHit hit, 400f),
                "the terrain ray at the pond XZ must hit Ground_Play");
            float gap = waterT.position.y - hit.point.y;

            Assert.Greater(gap, LowPolyZoneGen.PondFoamLight,
                $"the pond water→terrain gap ({gap:F3}) MUST exceed even the LIGHT foam distance " +
                $"({LowPolyZoneGen.PondFoamLight:F3}) so a soak that dials foam to LIGHT still reads foam=0 on the " +
                "open disc (only the bank band foams) — never a flat-disc flood (ticket 86cadj4g7).");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 #130 THIRD re-soak) — the COMMITTED material asset ships foam-OFF.
        // ROOT CAUSE of the Sponsor's white band: the white IS the depth-fade foam term (foam = saturate(1 −
        // gap/_FoamDistance) lerping to near-white _FoamColor). Over the shallow recessed pond the water→floor gap
        // is small across the WHOLE disc, so when foam is ON the term saturates to ~1 across the entire surface →
        // a BROAD white band visible from ABOVE (the Sponsor's 3-4 cam) but INVISIBLE EDGE-ON (so the side-profile
        // gate was blind). The COMMITTED PondWaterMat.mat asset was STALE — it predated the _FoamAmount gate
        // (last recommitted in #124, before cae8c52 added the gate) and shipped _FoamDistance=0.6 + NO _FoamAmount
        // (→ shader default 1 = foam ON). A build that does NOT re-run BootstrapProject before BuildPlayer ships
        // that stale asset → the white band. The other foam test (above) reads the scene material AFTER the test
        // harness's bootstrap regen, so it CANNOT catch the stale committed asset. THIS test loads the committed
        // asset directly from disk and asserts it ships foam-off — so a future stale-asset regression reds HERE,
        // not silently at the Sponsor's soak. (The dial was also dropped — foam is baked OFF, not a runtime knob.)
        [Test]
        public void CommittedPondMaterialAsset_ShipsFoamOff_NotStale()
        {
            const string matPath = "Assets/Settings/PondWaterMat.mat";
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Assert.IsNotNull(mat, $"the committed pond material asset must exist at {matPath}");

            // Only meaningful on the LowPolyWater shader (the depth-fade foam path). A URP/Lit fallback asset has
            // no foam to ship on, so skip the assert there (the build would have no depth-fade foam anyway).
            if (!mat.HasProperty("_FoamAmount") && !mat.HasProperty("_FoamDistance"))
            {
                Assert.Pass("committed pond asset has no foam properties (URP/Lit fallback — no depth-fade foam)");
                return;
            }

            // The master _FoamAmount gate MUST be present AND 0 on the committed asset — this is the load-bearing
            // OFF switch (it zeroes the WHOLE foam term incl. the broad shallow-disc band + the gap≈0 shoreline
            // razor). A missing _FoamAmount entry on the asset means the shader default (1 = foam ON) governs → the
            // white band ships. So we assert it is explicitly present and 0 (not relying on a default).
            Assert.IsTrue(mat.HasProperty("_FoamAmount"),
                "the committed pond material must carry the _FoamAmount master gate (a stale asset predating the " +
                "gate ships foam ON via the shader default = the #130 white band)");
            Assert.AreEqual(0f, mat.GetFloat("_FoamAmount"), 1e-4f,
                "the COMMITTED pond material asset must ship _FoamAmount = 0 (foam OFF) — a stale asset that ships " +
                "foam ON renders the broad white surface band the Sponsor saw (the #130 third re-soak root cause). " +
                "Re-run BootstrapProject + recommit Assets/Settings/PondWaterMat.mat if this reds.");
            if (mat.HasProperty("_FoamDistance"))
                Assert.AreEqual(0f, mat.GetFloat("_FoamDistance"), 1e-4f,
                    "the committed pond material must also ship _FoamDistance = 0 (belt-and-suspenders foam-off).");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7 #130 ROUND 9) — the recess + wade are BAKED to the fill-the-bowl
        // re-balance ===========================================================================================
        // ROUND-9 re-balance (Sponsor round-8 soak "step over the shore straight INTO knee-deep water — NO walkable
        // dry slope"): the RECESS (water surface below plateau) dropped 0.75 → 0.30 so the dry shore lip stays a
        // SHORT traversable step-over (the bowl fills to ≈0.90 of the mouth, no walkable dry slope), and the
        // knee-deep DEPTH moved into the WADE (0.45 → 0.75, the dispatch's "knee-deep 0.75u at the centre"). Pin
        // both so a future re-tune that re-opens a long dry slope (recess back up) or loses the knee-deep depth
        // reds here. The floor still sits FloorDrop = 1.05u below the plateau (a genuinely recessed bowl).
        [Test]
        public void PondRecessAndWade_BakedToFillTheBowlRebalance()
        {
            Assert.AreEqual(0.30f, LowPolyZoneGen.PondRecessKneeDeep, 1e-4f,
                "the baked pond RECESS (water surface below plateau) must be 0.30u — small enough that the dry shore " +
                "lip is a SHORT traversable step-over (fill ≈0.90 of the mouth, no walkable dry slope; #130 round 9)");
            Assert.AreEqual(0.75f, LowPolyZoneGen.PondWadeDepth, 1e-4f,
                "the baked pond WADE depth (floor below water) must be 0.75u — the dispatch's 'knee-deep 0.75u at " +
                "the centre' (the SUNK percept as DEPTH below the waterline; #130 round 9)");
            Assert.AreEqual(1.05f, LowPolyZoneGen.PondBowlFloorDrop, 1e-4f,
                "the bowl floor must sit RECESS + WADE = 1.05u below the plateau (a genuinely recessed bowl)");
            // The PondNudge default step must ALSO equal the baked recess so a no-key soak shows the shipped pond.
            Assert.AreEqual(LowPolyZoneGen.PondRecessKneeDeep,
                PondNudge.RecessStepValue[PondNudge.RecessDefaultStep], 1e-4f,
                "the PondNudge default recess step must equal the baked recess (a no-key soak sees the shipped pond)");
        }

        private static T FindAnyInScene<T>() where T : Component
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static Mesh FindChildMeshNamed(Transform root, string meshName)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name == meshName) return mf.sharedMesh;
            return null;
        }
    }
}
