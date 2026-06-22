using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the BERRY BUSH feature (ticket 86caa5zz3) is SERIALIZED into the Boot scene
    /// the exe ships — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md,
    /// would mangle/drop an Awake-built component or visual). Sibling of ChopSceneTests; same regression-
    /// guard intent: drop MovementCameraScene.BuildBerryBush (or its wiring), or the LowPolyZoneGen bush
    /// scatter, and this goes RED in headless CI — rather than the shipped build silently lacking the food
    /// source for the merged hunger loop.
    ///
    /// Binary scenes can't be GUID-grepped, so the EditMode scene-presence assert is the only authoritative
    /// reader (unity-conventions.md §Component-in-source-but-not-serialized-into-scene).
    /// </summary>
    public class BushSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesWiredBerryBush_WithRefsAndBerriesVisual()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            BerryBush bush = FindWiredBerryBush(scene);
            Assert.IsNotNull(bush,
                "the Boot scene must carry the wired BerryBush — the foraging beat the castaway walks up " +
                "to (a fixed-position bush, vs the random scatter ones, so the PlayMode/capture has a " +
                "deterministic harvest target). Serialized, not Awake-built (editor-vs-runtime trap).");
            Assert.IsTrue(bush.hasBerries, "the wired bush is the BERRY variant (it must carry berries)");
            Assert.IsNotNull(bush.inventory,
                "BerryBush's Inventory reference must be wired editor-time so harvesting adds berries " +
                "without an Awake-time scene search in the build");
            Assert.IsNotNull(bush.player,
                "BerryBush's player reference (the moving agent root) must be wired editor-time so the " +
                "proximity check has a target without an Awake-time scene search");
            Assert.IsNotNull(bush.berriesVisual,
                "BerryBush's berries-visual reference must be wired so harvest/regrow toggles the berries " +
                "(the bush body persists; only the berries deplete + regrow — AC4)");
        }

        [Test]
        public void BootScene_BerryBush_ShipsRipeAndUnharvested_WithValidTuning()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            BerryBush bush = FindWiredBerryBush(scene);
            Assert.IsNotNull(bush, "the Boot scene must carry the wired BerryBush");

            Assert.IsTrue(bush.IsRipe, "the bush ships RIPE — the player harvests it");
            Assert.Greater(bush.harvestRadius, 0f, "harvestRadius must be positive — a zero radius never harvests");
            Assert.Greater(bush.berriesPerHarvest, 0, "berriesPerHarvest must be positive — a harvest must yield berries");
            Assert.GreaterOrEqual(bush.regrowMaxSeconds, bush.regrowMinSeconds,
                "regrow max must be >= min (a random delay in [min,max] — AC4)");
            Assert.GreaterOrEqual(bush.regrowMinSeconds, 0f, "regrow min must be non-negative");
        }

        [Test]
        public void BootScene_BerryBush_HasBerriesMesh_BlobBushBody()
        {
            // The bush must read in the leafy blob language (board v2 nature sheets) — a multi-blob body
            // + a separate berries mesh carrying per-vertex colour (the red berries on green leaves).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            BerryBush bush = FindWiredBerryBush(scene);
            Assert.IsNotNull(bush, "the Boot scene must carry the wired BerryBush");

            var meshes = bush.GetComponentsInChildren<MeshFilter>(true);
            var body = meshes.FirstOrDefault(mf => mf.gameObject.name == "BushBody");
            Assert.IsNotNull(body, "the bush must carry a BushBody mesh child");
            Assert.IsNotNull(body.sharedMesh, "the bush body mesh must be assigned");
            Assert.Greater(body.sharedMesh.vertexCount, 40,
                "the bush body must be a multi-blob CLUSTER (>40 verts), not a single dome (board v2)");
            Assert.Greater(body.sharedMesh.colors.Length, 0,
                "the bush body must carry per-vertex green COLORS (the multi-value blob clustering)");

            // The berries mesh lives under the berriesVisual subtree (toggled on harvest/regrow).
            var berriesMesh = bush.berriesVisual.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(mf => mf.sharedMesh != null && mf.sharedMesh.colors.Length > 0);
            Assert.IsNotNull(berriesMesh,
                "the bush must carry a berries mesh (small red faceted spheres) under the berries visual");
        }

        [Test]
        public void BootScene_ScattersBushes_AcrossTheIsland_VariedTypes()
        {
            // AC1/AC2: the world scatter (LowPolyZoneGen) must place bushes across the seed-42 island, in
            // varied types — plain (LP_Bush) AND berry-bearing (LP_BerryBush). This guards that the scatter
            // additive sub-seed actually ran + serialized (drop the bush scatter loop -> this goes red).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var allTransforms = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .ToArray();

            int plainBushes = allTransforms.Count(t => t.gameObject.name == "LP_Bush");
            int berryBushes = allTransforms.Count(t => t.gameObject.name == "LP_BerryBush");

            Assert.Greater(plainBushes + berryBushes, 10,
                "the island must be scattered with bushes (>10) — AC1 (drop the scatter loop -> red)");
            Assert.Greater(plainBushes, 0, "the scatter must include PLAIN bushes (variety — AC2)");
            Assert.Greater(berryBushes, 0, "the scatter must include BERRY bushes (the food source — AC3)");
        }

        [Test]
        public void BootScene_ScatterBerryBushes_AreWiredHarvestable()
        {
            // The scattered berry bushes (LP_BerryBush) must carry a wired BerryBush component so the
            // castaway can forage ANY of them, not only the fixed wired one.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var scatterBerry = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BerryBush>(true))
                .Where(b => b.gameObject.name == "LP_BerryBush")
                .ToArray();

            Assert.Greater(scatterBerry.Length, 0, "the scatter must include wired LP_BerryBush bushes");
            foreach (var b in scatterBerry.Take(5)) // sample a few (don't loop all for speed)
            {
                Assert.IsTrue(b.hasBerries, "a scatter berry bush carries berries");
                Assert.IsNotNull(b.inventory, "a scatter berry bush is wired to the Inventory (harvestable)");
                Assert.IsNotNull(b.berriesVisual, "a scatter berry bush has a berries visual to toggle");
            }
        }

        // === AC2: the scatter's SIZE distribution spans the authored range + its DENSITY rises inland ===
        // Guards the two AC2 "reads natural, not cloned" properties against the baked Boot scene:
        //   (1) bush localScale spans the authored 0.55–1.5 range (not all one size) — a wide spread, with
        //       at least one genuinely small and one genuinely large bush, every scale in-band;
        //   (2) density rises INLAND (LowPolyZoneGen.BuildBush density `0.30 + inlandT*0.55`) — bushes are
        //       AREA-DENSER near the island centre than at the outer plant ring. Asserting per-area density
        //       (not raw counts) is the gradient-honest check: an outer annulus has more area, so raw counts
        //       can mislead; density (count ÷ annulus area) must be higher inland.
        [Test]
        public void BootScene_BushScatter_HasVariedScale_AndInlandDensityGradient()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var bushes = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.name == "LP_Bush" || t.gameObject.name == "LP_BerryBush")
                .ToArray();

            Assert.Greater(bushes.Length, 20,
                "the scatter must place a meaningful population (>20) for the distribution check to be sound");

            // (1) SCALE distribution — every bush in the authored 0.55–1.5 band, with real spread.
            float minScale = bushes.Min(b => b.localScale.x);
            float maxScale = bushes.Max(b => b.localScale.x);
            Assert.GreaterOrEqual(minScale, 0.55f - 0.001f, "no bush smaller than the authored min (0.55)");
            Assert.LessOrEqual(maxScale, 1.5f + 0.001f, "no bush larger than the authored max (1.5)");
            Assert.Less(minScale, 0.75f, "the scatter includes genuinely SMALL bushes (varied, not cloned — AC2)");
            Assert.Greater(maxScale, 1.25f, "the scatter includes genuinely LARGE bushes (varied, not cloned — AC2)");

            // (2) INLAND DENSITY gradient — per-area density higher in the inner half than the outer half.
            // Split at the radius that halves the populated disc's AREA (so each region has equal area),
            // then compare counts (equal-area regions ⇒ count IS density). Planar XZ radius from origin.
            float maxR = bushes.Max(b => Mathf.Sqrt(b.position.x * b.position.x + b.position.z * b.position.z));
            float splitR = maxR / Mathf.Sqrt(2f); // inner disc area == outer annulus area
            int inner = bushes.Count(b =>
                Mathf.Sqrt(b.position.x * b.position.x + b.position.z * b.position.z) <= splitR);
            int outer = bushes.Length - inner;
            Assert.Greater(inner, outer,
                $"density rises inland: with the disc split into EQUAL-AREA inner/outer regions, the inner " +
                $"region must hold MORE bushes (inner={inner} > outer={outer}) — the BuildBush inland gradient");
        }

        // Find the FIXED wired berry bush authored by MovementCameraScene (named "BerryBush"), preferring
        // it over the scatter ones (named LP_BerryBush) so the wired-bush asserts target the deterministic one.
        private static BerryBush FindWiredBerryBush(Scene scene)
        {
            var all = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BerryBush>(true))
                .ToArray();
            return all.FirstOrDefault(b => b.gameObject.name == "BerryBush") ?? all.FirstOrDefault();
        }
    }
}
