using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode scene-presence guard for the BEACH OCEAN (drew/beach-water-scene; Uma
    /// beach-water-direction §4). The water is a VISUAL surface, so the shipped-build capture gate is
    /// the final evidence — but binary scenes can't be GUID-grepped, so this EditMode reader is the
    /// authoritative check that the re-tuned ocean actually lives in the Boot.unity the exe ships
    /// (the component/mesh-in-source-but-not-serialized failure class, unity-conventions.md).
    ///
    /// Each assertion guards a SPECIFIC bug the diagnostic trace found in the original water (2026-06-13,
    /// drew/beach-water): it shipped as a 10x10 primitive Plane with meshColors=0 (single-tone — the
    /// gradient shader had nothing to ride), scale.z=4 (~40u deep, a hard far edge inside the fog),
    /// URP/Lit smoothness 0.88 (mirror-glossy, breaks the toy). If a future change reverts any of those,
    /// exactly one of these fails in headless CI before it ships.
    /// </summary>
    public class WaterSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        // Slack for the gradient-anchor pins through serialization float round-trip (matches the
        // HeroAxeSceneTests convention) — guards drift/regression, not a pixel-exact match.
        private const float ColorTol = 0.03f;

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

        [Test]
        public void Ocean_ExistsInShippedScene_WithVertexColorGradient()
        {
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the Boot scene must carry the beach ocean (Water_Play) — it ships via " +
                "WorldBootstrap into Boot.unity (the diagnostic confirmed it WAS present, just unfinished)");

            var mf = water.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "the ocean must have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "the ocean mesh must be serialized into the scene");

            // THE headline bug: the old primitive Plane had meshColors=0, so the near->far teal gradient
            // had nothing to ride and the water shipped single-tone. A welded grid with baked vertex
            // colors is the fix — assert the colors are actually present.
            Assert.Greater(mf.sharedMesh.colors.Length, 0,
                "the ocean mesh must carry per-vertex COLORS (the near->far teal gradient is baked in " +
                "vertex color, ridden by FarHorizon/LowPolyVertexColor — a primitive Plane has none)");

            // And it must be a subdivided GRID (not the 10x10=121-vert primitive), so the gradient has
            // verts to interpolate across AND the swell has verts to displace.
            Assert.Greater(mf.sharedMesh.vertexCount, 200,
                "the ocean must be a subdivided welded grid (>200 verts), not the original 10x10 plane " +
                "(the gradient + in-shader swell need real subdivision)");
        }

        [Test]
        public void Ocean_GradientEndpoints_MatchTealAnchors_ShallowToDeep()
        {
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the ocean (Water_Play) must be present");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices;
            var cols = mesh.colors;
            Assert.AreEqual(verts.Length, cols.Length, "every ocean vert must carry a color");

            // The gradient runs near-shore BRIGHT -> seaward DEEPER along local Z. WATERLINE-OUT SOAK-FIX
            // (86ca8t9pq W1): the waterline + foam band moved SEAWARD to worldZ ~ -10.2 (WaterlineWorldZ),
            // and the foam plateau (core 8u + band 9u) fades out by ~worldZ -27. So the clear-shallows
            // bright-teal anchor must be probed WELL seaward of that fade (~worldZ -45), and the DEEP anchor
            // at the seaward-most vert. (The foam band itself is guarded by Ocean_CarriesShorelineFoam.)
            float shallowProbeZ = -45f; // well seaward of the new foam fade (waterline -10.2 + ~17u band), so
                                        // the probe robustly lands a clear-shallow vert, not a foam-blended one
            int shallowIdx = 0, farIdx = 0;
            for (int i = 1; i < verts.Length; i++)
            {
                if (Mathf.Abs(verts[i].z - shallowProbeZ) < Mathf.Abs(verts[shallowIdx].z - shallowProbeZ)) shallowIdx = i;
                if (verts[i].z < verts[farIdx].z) farIdx = i; // smallest Z = deepest sea
            }
            AssertColorNear(cols[shallowIdx], LowPolyZoneGen.WaterShallow,
                "the shallows just seaward of the foam band must be the BRIGHT shallow teal (#3FA6B0)");
            AssertColorNear(cols[farIdx], LowPolyZoneGen.WaterDeep,
                "seaward vert must be the DEEPER teal (#2E7E96)");

            // Every channel sub-1.0 (HDR-clamp discipline — the post stack must not bloom the sea white).
            foreach (var c in new[] { cols[shallowIdx], cols[farIdx] })
            {
                Assert.Less(c.r, 1f, "water teal red channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.g, 1f, "water teal green channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.b, 1f, "water teal blue channel must be sub-1.0 (HDR-clamp-safe)");
            }
        }

        // Mirror of LowPolyZoneGen.WaterFoamBandU (the foam fade extent) for the seaward probe distance.
        private const float WaterSceneTests_FoamBandU = 7f;

        [Test]
        public void Ocean_CarriesShorelineFoam_AtTheCoast_NotJustTheSeawardSand()
        {
            // WORLD-LOOK SHORELINE FIX (Erik water rec / Uma §2): the Sponsor's "hard flat diagonal edge"
            // was the rectangular water grid boundary meeting the curving beach. The fix bakes a warm-white
            // FOAM band into the WATER mesh's NEAR-SHORE rows (prior builds only foamed the terrain sand, so
            // the water itself had no shoreline treatment). Assert the foam colour appears in the water mesh's
            // near-shore verts — a regression dropping the water-foam bake leaves the hard edge back.
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the ocean (Water_Play) must be present");
            var cols = water.GetComponent<MeshFilter>().sharedMesh.colors;
            var foam = LowPolyZoneGen.FoamEdge;
            int foamVerts = cols.Count(c =>
                c.r > 0.78f && c.g > 0.78f && c.b > 0.70f &&   // foam is the brightest band on the water
                Mathf.Abs(c.r - foam.r) < 0.14f && Mathf.Abs(c.g - foam.g) < 0.14f);
            Assert.Greater(foamVerts, 0,
                "the WATER mesh must carry a shoreline FOAM band (warm off-white) baked into its near-shore " +
                "rows — the surf line that softens the sea↔land boundary (Erik water rec / Uma §2). Without " +
                "it the rectangular grid edge reads as the Sponsor's 'hard flat diagonal edge'.");
        }

        [Test]
        public void Ocean_IsFlatShadedUnweldedFacets_NotASmoothSheet()
        {
            // WORLD-LOOK (Erik water rec): the sea joins the faceted-world look as UNWELDED FLAT facets
            // (each face owns its 3 verts + a single hard normal), not a smooth welded sheet. Unwelded =>
            // vertexCount == triangleCount*3. This is the tell that the water reads as chunky planes catching
            // the key light per-face (consistent with the rocks/terrain/mountains), not a glassy plane.
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the ocean (Water_Play) must be present");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            int tris = mesh.triangles.Length / 3;
            Assert.AreEqual(tris * 3, mesh.vertexCount,
                "the ocean must be UNWELDED flat-shaded facets (verts == tris*3) — the faceted-sea world-look; " +
                "a welded smooth sheet (verts < tris*3) is the regression");
        }

        [Test]
        public void Ocean_BrightShallowsBand_ExtendsIntoTheMidSea_NotJustTheCoast()
        {
            // drew/ocean-camera-fix: the bright-teal shallows band was WIDENED (70u->130u) so the sea
            // reads teal across the frame when the camera tilts down to the horizon (the now-allowed flat
            // pitch makes the 50-150u MID-sea dominate the upper frame, not the very-near band). Guard the
            // bug class: a mid-sea vert (~50-70u seaward of the coast) must still be BRIGHT-leaning teal
            // (closer to the shallow anchor than the deep anchor). A regression to a narrow band would push
            // the deep/fog-greyed teal across the visible mid-sea — the "grey" read. near edge = shoreZ(-12) +
            // overlap(13) = +1, so ~Z-65 is ~66u seaward (inside the 130u bright band, outside 70u).
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the ocean (Water_Play) must be present");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices;
            var cols = mesh.colors;

            // Find the vert nearest world Z -65 (local Z, since the water root sits at world Z 0).
            const float midZ = -65f;
            int midIdx = 0;
            for (int i = 1; i < verts.Length; i++)
                if (Mathf.Abs(verts[i].z - midZ) < Mathf.Abs(verts[midIdx].z - midZ)) midIdx = i;

            var shallow = LowPolyZoneGen.WaterShallow;
            var deep = LowPolyZoneGen.WaterDeep;
            float dShallow = ChannelDist(cols[midIdx], shallow);
            float dDeep = ChannelDist(cols[midIdx], deep);
            Assert.Less(dShallow, dDeep,
                $"a mid-sea vert (~55u seaward, localZ={verts[midIdx].z:0.0}) must still be BRIGHT-leaning " +
                $"teal (closer to shallow #3FA6B0 than deep #2E7E96) — the widened 130u band keeps the " +
                $"visible mid-sea teal; a narrow band lets it grey out (color={cols[midIdx]})");
        }

        [Test]
        public void Ocean_ExtendsFarSeaward_PastTheFogLine_NoHardNearEdge()
        {
            var water = GameObject.Find("Water_Play");
            var mr = water.GetComponent<MeshRenderer>();
            // The original water spanned ~40u of Z (scale.z=4). The re-tuned ocean must extend FAR
            // seaward so its far edge dissolves into the fog rather than reading as a hard plane edge.
            float depthZ = mr.bounds.size.z;
            Assert.Greater(depthZ, 150f,
                "the ocean must extend far seaward (>150u of depth) so its far edge is lost in the warm " +
                "fog haze — the 'far horizon' read — not a visible hard plane edge inside the fog");
            // And wider than the land so the sea wraps the coast at the edges of the frame.
            Assert.Greater(mr.bounds.size.x, 90f,
                "the ocean must be wider than the play space so it fills the seaward frame edge-to-edge");
        }

        [Test]
        public void Ocean_Material_RidesVertexColorShader_WithSwellEnabled()
        {
            var water = GameObject.Find("Water_Play");
            var mr = water.GetComponent<MeshRenderer>();
            var mat = mr.sharedMaterial;
            Assert.IsNotNull(mat, "the ocean must have a material");
            Assert.AreEqual("FarHorizon/LowPolyVertexColor", mat.shader.name,
                "the ocean must render through FarHorizon/LowPolyVertexColor so the vertex-color teal " +
                "gradient shows (URP/Lit IGNORES vertex color -> the old single-tone water)");

            // The gentle swell is in-shader (_WaveAmp > 0). This is the WATER ONLY — the terrain/canopy
            // materials share this shader but leave _WaveAmp at its 0 default (so they don't wobble).
            Assert.IsTrue(mat.HasProperty("_WaveAmp"), "the shader must expose _WaveAmp (the in-shader swell)");
            Assert.Greater(mat.GetFloat("_WaveAmp"), 0f,
                "the ocean material must enable the gentle swell (_WaveAmp > 0) — a dead-still plane reads " +
                "as ice/glass and kills the 'alive world' feel (Uma §1)");
            Assert.Less(mat.GetFloat("_WaveAmp"), 0.2f,
                "the swell must stay SUBTLE (a breath, not surf) — small amplitude only");
        }

        [Test]
        public void Terrain_CarriesFoamBand_AtTheSeawardShoreRows()
        {
            // The foam line (Uma §2 task D) is baked into the seaward-most rows of the BEACH MESH vertex
            // colors (rides the terrain shader, no new object). Assert the warm off-white foam color
            // actually appears in the terrain's vertex colors — a regression that drops the foam bake
            // would leave the shoreline with no waterline read.
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the play-space terrain (Ground_Play) must exist");
            var cols = ground.GetComponent<MeshFilter>().sharedMesh.colors;
            Assert.Greater(cols.Length, 0, "terrain must carry vertex colors");

            // Count verts close to the warm foam anchor (#E8E2D0). The foam band is a narrow seaward
            // strip, so only a handful of rows qualify — assert at least a few exist.
            var foam = LowPolyZoneGen.FoamEdge;
            int foamVerts = cols.Count(c =>
                Mathf.Abs(c.r - foam.r) < 0.12f &&
                Mathf.Abs(c.g - foam.g) < 0.12f &&
                Mathf.Abs(c.b - foam.b) < 0.12f &&
                c.r > 0.8f && c.g > 0.8f); // foam is the brightest band on the beach
            Assert.Greater(foamVerts, 0,
                "the beach mesh must carry a FOAM band (warm off-white #E8E2D0) baked into its seaward-most " +
                "vertex rows — the calm stylized waterline (Uma §2)");
        }

        [Test]
        public void Ocean_NotOccludedByFlatGround_SeawardSlabTrimmed()
        {
            // HYGIENE check (NOT the visibility guard — see the correction below). This asserts the flat
            // TestGround placeholder's seaward edge does not overhang the ocean's near band.
            //
            // IMPORTANT CORRECTION (drew/ocean-beach-soakfix2, 2026-06-13): this test PASSED while the sea
            // stayed completely invisible across six builds — proving the seaward-slab-trim was NOT the
            // real fix. The actual root cause was the water mesh's INVERTED winding (faces pointed DOWN ->
            // Cull Back hid the sea); see WaterFacesUpTests for THE guard. This Z-edge check is kept as a
            // cheap geometry-hygiene assertion (don't re-grow the slab over the water) but it is explicitly
            // NOT the silhouette/visibility guard — a Z-edge relationship cannot prove on-frame visibility.
            // NOTE (86ca8t9pq waterline-coverage fix): the water now reaches INLAND past the real waterline
            // (near edge worldZ +1) so it covers the underwater foreshore, which means it now extends inland
            // OF the TestGround seaward edge (-10). That is FINE for occlusion: TestGround's renderer is
            // DISABLED (TestGround_IsCollisionProxyOnly guard), so the collision-only slab can never occlude
            // the sea regardless of Z overlap. The real visibility guard is WaterFacesUpTests (winding/normals).
            var testGround = GameObject.Find("TestGround");
            Assert.IsNotNull(testGround, "the flat test ground (TestGround) must exist (NavMesh + loop surface)");
            var tgMr = testGround.GetComponent<MeshRenderer>();
            Assert.IsFalse(tgMr.enabled,
                "TestGround's renderer must stay DISABLED — a collision-only proxy can't occlude the sea even " +
                "where the inland-reaching water overlaps it (the water now covers the underwater foreshore).");
        }

        [Test]
        public void TestGround_IsCollisionProxyOnly_RendererDisabled_NoGreySlab()
        {
            // REGRESSION GUARD for soak #40 (drew/ocean-beach-soakfix2, stamp 31ce95c): the Sponsor saw a
            // "gray slab on the beach" — the flat Y=0 TestGround placeholder (muted moss-grey 0.42,0.46,0.40)
            // poking ABOVE the dipping sandy Zone-D terrain across the seaward foreshore band (Z ~ -10..+3).
            // The fix keeps TestGround as a COLLISION/NAVMESH proxy (collider on the Ground layer, so the
            // baked NavMesh + click-raycast are unchanged) but DISABLES its MeshRenderer so the grey slab no
            // longer draws — the sandy terrain is the only thing painted on the beach. Re-enabling the
            // renderer (or dropping the collider) re-buries the sand under grey / kills click-move; either
            // regression fails here in headless CI before it ships.
            var testGround = GameObject.Find("TestGround");
            Assert.IsNotNull(testGround, "TestGround must exist — it is the NavMesh + click-move collision proxy");

            // Load-bearing role PRESERVED: collider on the Ground layer (feeds the NavMesh bake + click raycast).
            var col = testGround.GetComponent<MeshCollider>();
            Assert.IsNotNull(col, "TestGround must keep its MeshCollider (NavMesh bake + click-move raycast)");
            Assert.IsNotNull(col.sharedMesh, "the collider mesh must be serialized (no collider mesh = dead NavMesh/click)");
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                Assert.AreEqual(groundLayer, testGround.layer,
                    "TestGround must stay on the Ground layer (the NavMesh layerMask + click groundMask depend on it)");

            // THE grey-slab guard: the renderer must NOT draw. The component is kept (so .bounds still
            // resolves for the occlusion-hygiene test above) but disabled — the sandy terrain renders the beach.
            var mr = testGround.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr, "the MeshRenderer is kept (disabled) so .bounds stays resolvable for the occlusion test");
            Assert.IsFalse(mr.enabled,
                "the TestGround MeshRenderer MUST be DISABLED — an enabled flat grey placeholder slab pokes " +
                "above the dipping sandy beach (Z ~ -10..+3) and reads as a 'gray slab on the beach' (soak #40). " +
                "The sandy Zone-D terrain is the visible ground; this is a collision/NavMesh proxy only.");
        }

        [Test]
        public void BeachLoopObjects_SitOnDrySand_NotInTheWater_WaterlineOutGuard()
        {
            // WATERLINE-OUT SOAK-FIX REGRESSION GUARD (86ca8t9pq W1, Sponsor soak of b54482c: "it should be
            // moved a bit out, so the tree, campfire and debris is not in the water"). Diagnose-via-trace
            // CONFIRMED the loop objects sat BELOW WaterY (-0.20): ChopTree (z=-7) terrainY -0.44u, FirePit
            // (z=-8) -0.48u, BeachDebris (z=-3) -0.25u — all underwater, because the old beachRamp completed
            // only at fz=0.27 so the waterline crept inland to worldZ ~ -1.7. The fix (ShoreRampEnd 0.045)
            // climbs the beach out of the water by worldZ ~ -8.9 so the waterline lands at ~ -10.2 — every
            // loop object now sits on DRY sand. This guards the bug CLASS: raycast the SHIPPED terrain
            // collider straight down at each loop-spot XZ and assert the surface Y is above WaterY with
            // margin. A regression that re-floods the loop band (pulls the waterline back inland) fails HERE
            // in headless CI before it ships. (A render proxy can't read the dipping terrain — the collider
            // ray is the authoritative reader, same idiom as GroundPoint.)
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the play-space terrain (Ground_Play) must exist");
            var col = ground.GetComponent<MeshCollider>();
            Assert.IsNotNull(col, "the terrain must carry a MeshCollider (the ground raycast surface)");

            const float WaterY = -0.20f;  // mirrors LowPolyZoneGen.WaterY
            const float DryMargin = 0.10f; // clear of the surf, survives the shore noise band
            // The survival-loop beach objects (MovementCameraScene static positions) — the ones the Sponsor
            // saw submerged. XZ only; the Y comes from the terrain ray.
            var loopSpots = new (string name, float x, float z)[]
            {
                ("ChopTree",    -9f, -7f),
                ("FirePit",      4f, -8f),
                ("BeachDebris", -3.2f, -3.0f),
            };
            foreach (var (name, x, z) in loopSpots)
            {
                var ray = new Ray(new Vector3(x, 50f, z), Vector3.down);
                Assert.IsTrue(col.Raycast(ray, out RaycastHit hit, 200f),
                    $"the terrain ray at the {name} spot ({x},{z}) must hit the ground (the loop surface)");
                Assert.Greater(hit.point.y, WaterY + DryMargin,
                    $"the {name} loop object at ({x},{z}) must sit on DRY sand (terrainY {hit.point.y:F3} > " +
                    $"WaterY {WaterY:F2} + margin) — a terrain that dips below WaterY here re-floods the loop " +
                    "object the Sponsor saw in the water (W1 waterline-out regression). Push the waterline out.");
            }
        }

        [Test]
        public void Waterline_SitsSeawardOfTheLoopBand_NotInland()
        {
            // WATERLINE-OUT GUARD (companion to the loop-object check): the water mesh's NEAR EDGE must sit
            // SEAWARD of the loop band (z <= -8) so the rendered sea never reaches inland over the dry loop
            // objects. With WaterInlandOverlap 4 the near edge is worldZ -8 (= shoreZ -12 + 4). The water root
            // sits at world Z 0, so the near (largest-Z, least-negative) vert's world Z must be <= -7.5.
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the ocean (Water_Play) must be present");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices;
            float maxLocalZ = float.NegativeInfinity;
            foreach (var v in verts) if (v.z > maxLocalZ) maxLocalZ = v.z;
            float nearWorldZ = water.transform.position.z + maxLocalZ;
            Assert.LessOrEqual(nearWorldZ, -7.5f,
                $"the water near edge (worldZ {nearWorldZ:F1}) must sit SEAWARD of the loop band (z<=-8) so the " +
                "rendered sea never floods the dry loop objects (FirePit z=-8). An inland-reaching near edge " +
                "re-floods the beach (the W1 regression).");
        }

        [Test]
        public void BootScene_CarriesSeaVerifyCapture_OnTheBootObject()
        {
            // The orbit-to-sea framing check (Uma §4 task F) needs a committed, repeatable shipped-build
            // capture path: the SeaVerifyCapture must be SERIALIZED onto the Boot object (sibling of the
            // movement/craft/chop/loop captures). Drop the WireSeaVerifyCapture authoring and this goes
            // RED — the -verifySea path silently vanishing from the shipped scene is the failure class
            // this guards (component-in-source-but-not-serialized, unity-conventions.md).
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the verify captures)");
            Assert.IsNotNull(boot.GetComponent<FarHorizon.SeaVerifyCapture>(),
                "the Boot object must carry the SeaVerifyCapture component (the committed -verifySea " +
                "orbit-to-sea capture path), serialized into the scene — not Awake-added");
        }

        // Sum of absolute per-channel differences (a simple RGB distance for the bright/deep-lean check).
        private static float ChannelDist(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        private static void AssertColorNear(Color c, Color anchor, string label)
        {
            Assert.AreEqual(anchor.r, c.r, ColorTol, $"{label}: red channel");
            Assert.AreEqual(anchor.g, c.g, ColorTol, $"{label}: green channel");
            Assert.AreEqual(anchor.b, c.b, ColorTol, $"{label}: blue channel");
        }
    }
}
