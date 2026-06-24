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

        // The bank ring's inner edge must TRACK the organic water rim (same shared PondRimFactor) so the grassy
        // collar frames the lobed pool with no gap / poke-through. Asserts the bank carries an irregular inner
        // ring too (a circular bank around a lobed pool would gap). Guards the shared-outline contract.
        [Test]
        public void BootScene_PondBank_TracksOrganicWaterRim()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond, "the pond must be present");
            var bankMesh = FindChildMeshNamed(pond.transform, "LP_PondBank");
            Assert.IsNotNull(bankMesh, "the pond must carry the bank ring mesh (LP_PondBank)");

            // The bank's INNER ring sits at the water-lip Y (PondSurfaceY + 0.02). Collect those verts' radii and
            // assert they spread (the bank inner edge follows the organic water rim, not a circle).
            float minR = float.MaxValue, maxR = 0f;
            foreach (var v in bankMesh.vertices)
            {
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (r < 0.01f) continue;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
            }
            Assert.Greater(maxR - minR, 0.10f,
                "the pond bank ring must be ORGANIC too (its radii spread) so the grassy collar frames the " +
                "lobed pool with no gap/poke-through — a circular bank around a lobed pool would leave gaps " +
                "(shared LowPolyZoneGen.PondRimFactor; ticket 86cadj4g7)");
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
            // floor (never occluded), and the gap IS the wade depth: deep enough to read knee-deep (the player's
            // NavMesh-agent Y follows this floor), not a flush puddle. A floor band catches a re-sink; a ceiling
            // keeps it from being a deep WELL (knee, not waist). The carve is PondBowlFloorDrop (0.55) with the
            // water PondWaterDepthAboveFloor (0.45) up from the floor → expect ~0.45u here (bounded 0.2..0.9).
            float depth = waterWorldY - floorY;
            Assert.Greater(depth, 0.20f,
                $"the pond WATER surface (worldY {waterWorldY:F3}) must sit clearly ABOVE the carved bowl floor " +
                $"({floorY:F3}) — depth {depth:F3} reads knee-deep + un-occluded (the #130 defect was a flush/" +
                "lifted disc; the bowl carve recesses it). LowPolyZoneGen.PondDepressionDelta + GroundPondInBowl.");
            Assert.Less(depth, 0.90f,
                $"the pond water depth ({depth:F3}) must stay KNEE-deep, not a deep well — the player wades in, " +
                "not swims (bounded by PondWaterDepthAboveFloor; a runaway depth means the bowl/grounding drifted).");
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

        // === NEW (ticket 86cadj4g7 #130) — the bowl WALL is GENTLE so the NavMesh covers the floor =========
        // The player must be able to NAVIGATE INTO the bowl (wade in knee-deep). The NavMesh bakes on the
        // carved terrain collider with the default agent max-slope (45°). If the bowl wall were steeper than
        // 45° the bake would drop the floor (a hole the player can't enter). Assert the steepest wall slope
        // (PondBowlFloorDrop over the [inner,outer] run) is comfortably under 45° — so the floor stays walkable.
        // (End-to-end NavMesh COVERAGE is also asserted by RoundIslandNavCoveragePlayModeTests; this is the
        // cheap geometric guard that the wall can't regress steep.)
        [Test]
        public void PondBowl_WallSlope_WellUnderNavMeshAgentMaxSlope()
        {
            float run = LowPolyZoneGen.PondBowlOuterRadius - LowPolyZoneGen.PondBowlInnerRadius;
            Assert.Greater(run, 0f, "the bowl wall must have a positive run (outer radius > inner radius)");
            // The maximum wall slope (the average gradient across the wall; the smoothstep peaks ~1.5× the
            // average at the midpoint, so guard against a margin below 45° to cover the steepest point too).
            float avgSlopeDeg = Mathf.Atan2(LowPolyZoneGen.PondBowlFloorDrop, run) * Mathf.Rad2Deg;
            // smoothstep's max gradient is 1.5× its average → the steepest local slope:
            float maxSlopeDeg = Mathf.Atan2(1.5f * LowPolyZoneGen.PondBowlFloorDrop, run) * Mathf.Rad2Deg;
            Assert.Less(maxSlopeDeg, 40f,
                $"the bowl wall's steepest slope ({maxSlopeDeg:F1}°, avg {avgSlopeDeg:F1}°) must stay WELL under " +
                "the 45° NavMesh agent max — else the bake drops the bowl floor and the player can't wade in " +
                "(ticket 86cadj4g7: knee-deep wade-in requires NavMesh on the floor).");
        }

        // === NEW (ticket 86cadj4g7 #130) — the collar is FLUSH (drapes on the wall), NOT a raised lip =======
        // The #130 defect: the lift raised the whole pond so the collar floated ABOVE the surrounding terrain
        // (a raised lip casting a shadow). Now the collar must DRAPE on the carved bowl wall — its outer rim
        // must sit at/below the surrounding terrain, never proud of it. Sample the bank's outer-ring verts in
        // WORLD space and raycast the terrain at each: the collar vert must NOT poke up above the ground.
        [Test]
        public void Pond_BankCollar_DrapesOnBowlWall_FlushNotRaisedLip()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");
            var bankT = pond.transform.Find("PondBank");
            Assert.IsNotNull(bankT, "the pond must carry the PondBank collar child");
            var bankMesh = bankT.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(bankMesh, "the bank must carry its ring mesh");

            var ground = GameObject.Find("Ground_Play");
            var col = ground != null ? ground.GetComponent<MeshCollider>() : null;
            Assert.IsNotNull(col, "the terrain MeshCollider must exist to compare the collar against the ground");

            // The OUTER collar ring DRAPES on the bowl wall: its verts sit at a LOWER local Y than the inner
            // water-lip ring (which is at exactly local Y −0.04). The INNER ring is at the water lip — it is
            // SUPPOSED to sit above the (carved-floor) terrain, framing the recessed water, so we must NOT flag
            // it. Discriminate by LOCAL Y: the inner ring is at exactly −0.04; every outer vert (CollarOuterLocalY)
            // is ≤ −0.08, so a −0.06 cut cleanly selects the outer rim. TWO complementary checks (the second is
            // robust to terrain-grid discretization, which the analytic collar can diverge from on the 1.65u grid):
            //  (1) the outer collar must NOT float PROUD of the terrain beneath it (the raised-lip defect) —
            //      generous 0.15u tolerance so grid discretization of the 0.55u bowl never false-reds, but the
            //      old +0.08-and-floating flat lip (which sat well above the surrounding grass) still trips it;
            //  (2) the outer rim must sit clearly BELOW the inner water-lip plane (it slopes DOWN-into the bowl,
            //      NOT a flat raised collar) — a collar-to-collar check, immune to terrain discretization.
            var verts = bankMesh.vertices;
            int outerChecked = 0, raisedLip = 0;
            float innerLipWorldY = bankT.TransformPoint(new Vector3(0f, -0.04f, 0f)).y; // the water-lip ring plane
            float maxOuterWorldY = float.MinValue;
            foreach (var v in verts)
            {
                if (v.y > -0.06f) continue; // skip the inner water-lip ring (−0.04; it legitimately sits above the floor)
                Vector3 w = bankT.TransformPoint(v);
                maxOuterWorldY = Mathf.Max(maxOuterWorldY, w.y);
                var ray = new Ray(new Vector3(w.x, 200f, w.z), Vector3.down);
                if (!col.Raycast(ray, out RaycastHit hit, 400f)) continue; // off the mesh — skip
                outerChecked++;
                if (w.y > hit.point.y + 0.15f) raisedLip++; // proud of the ground = the raised-lip bug
            }
            Assert.Greater(outerChecked, 0, "must have sampled outer-collar verts against the terrain");
            Assert.AreEqual(0, raisedLip,
                $"the bank collar must DRAPE flush on the carved bowl wall — {raisedLip}/{outerChecked} outer-rim " +
                "verts float PROUD of the terrain (a raised lip casting a shadow, the #130 defect). " +
                "CollarOuterLocalY must track the PondDepressionDelta wall profile.");
            // (2) the outer rim does NOT rise above the inner water-lip plane — it drapes down-and-out onto the
            // bowl wall toward the waterline (a small tolerance for facet jitter). A collar whose outer rim rose
            // ABOVE the water lip would be the raised-lip defect (the old +0.04 outer-above-inner geometry).
            Assert.LessOrEqual(maxOuterWorldY, innerLipWorldY + 0.02f,
                $"the collar outer rim (max worldY {maxOuterWorldY:F3}) must NOT rise above the inner water-lip " +
                $"plane ({innerLipWorldY:F3}) — it drapes DOWN-and-out onto the bowl wall, never a RAISED lip (#130).");
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

        // === REGRESSION GUARD (ticket 86cadj4g7) — the depth-fade foam must not FLOOD the flat disc ========
        // The shared depth-fade foam (foam = saturate(1 - gap/_FoamDistance)) fires UNIFORMLY across the disc
        // unless _FoamDistance is SMALLER than the water→floor gap — at the sea-scale _FoamDistance the pond
        // shipped PALE/near-white (B-G≈-0.01), the foam-flood half of the original defect. With the BOWL CARVE
        // (#130 re-soak) the gap is now the knee-deep water DEPTH (~0.45u above the carved floor) — an even
        // larger margin than the old lift. This pins the invariant that makes the open-water disc read fresh-
        // blue: the pond material's _FoamDistance MUST be < the water→floor depth so gap > _FoamDistance →
        // foam=0 in open water (foam then only rides the thin bank band). Guards a future re-tune raising
        // _FoamDistance back into flood territory.
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

            // The actual water→terrain gap (the clearance the re-ground established).
            var ground = GameObject.Find("Ground_Play");
            var col = ground != null ? ground.GetComponent<MeshCollider>() : null;
            Assert.IsNotNull(col, "Ground_Play MeshCollider must exist to measure the water→terrain gap");
            Vector3 p = pond.transform.position;
            Assert.IsTrue(col.Raycast(new Ray(new Vector3(p.x, 200f, p.z), Vector3.down), out RaycastHit hit, 400f),
                "the terrain ray at the pond XZ must hit Ground_Play");
            float gap = waterT.position.y - hit.point.y;

            Assert.Greater(gap, foamDistance,
                $"the pond water→terrain gap ({gap:F3}) MUST exceed _FoamDistance ({foamDistance:F3}) so the " +
                "open-water disc reads foam=0 (fresh-blue) — a _FoamDistance >= the gap FLOODS the flat disc " +
                "toward warm-white FoamEdge (the pale/green-dominant defect, ticket 86cadj4g7).");
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
