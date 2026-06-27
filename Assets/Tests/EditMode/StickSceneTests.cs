using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the STICK / BRANCH E-loot feature (ticket 86caa96rd) is SERIALIZED into the Boot
    /// scene the exe ships — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md,
    /// would mangle/drop an Awake-built component or visual). Sibling of BushSceneTests; same regression-guard
    /// intent: drop MovementCameraScene.BuildWiredStick (or its wiring), or the LowPolyZoneGen stick scatter,
    /// and this goes RED in headless CI — rather than the shipped build silently lacking the low-yield wood
    /// source. Also guards the REGRESSION constraint: the existing tree/rock/grass/bush scatter must still be
    /// present (the stick pass is ADDITIVE, sub-seed seed+888 — it must not have perturbed them).
    ///
    /// Binary scenes can't be GUID-grepped, so the EditMode scene-presence assert is the only authoritative
    /// reader (unity-conventions.md §Component-in-source-but-not-serialized-into-scene).
    /// </summary>
    public class StickSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesWiredStick_WithInventoryRef()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            StickProp stick = FindWiredStick(scene);
            Assert.IsNotNull(stick,
                "the Boot scene must carry the wired StickProp — the deterministic loot target the castaway " +
                "walks up to (a fixed-position stick, vs the random scatter ones, so the PlayMode/capture has " +
                "a deterministic loot). Serialized, not Awake-built (editor-vs-runtime trap).");
            Assert.IsNotNull(stick.inventory,
                "StickProp's Inventory reference must be wired editor-time so looting adds wood without an " +
                "Awake-time scene search in the build");
            Assert.IsTrue(stick.IsAvailable, "the wired stick ships present (loot-able)");
            Assert.Greater(stick.lootRadius, 0f, "lootRadius must be positive — a zero radius never loots");
            Assert.AreEqual(StickProp.WoodPerStickDefault, stick.woodPerStick,
                "the wired stick yields the named-constant ONE wood (the low-yield contrast — AC3)");
        }

        [Test]
        public void BootScene_WiredStick_HasMesh()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            StickProp stick = FindWiredStick(scene);
            Assert.IsNotNull(stick, "the Boot scene must carry the wired StickProp");

            var mf = stick.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault();
            Assert.IsNotNull(mf, "the wired stick must carry a mesh child (the shaft)");
            Assert.IsNotNull(mf.sharedMesh, "the stick shaft mesh must be assigned");
            Assert.Greater(mf.sharedMesh.vertexCount, 0, "the stick shaft mesh must have geometry");
        }

        // === AC1: SMALL sticks scattered across the seed-42 island in VARIOUS SIZES ===
        [Test]
        public void BootScene_ScattersSticks_AcrossTheIsland_VariedSizes()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var sticks = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.name == "LP_Stick")
                .ToArray();

            Assert.Greater(sticks.Length, 20,
                "the island must be scattered with sticks (>20) — AC1 (drop the scatter loop -> red)");

            // VARIOUS SIZES (AC1): scale spans the authored 0.6–1.6 band, with real spread.
            float minScale = sticks.Min(s => s.localScale.x);
            float maxScale = sticks.Max(s => s.localScale.x);
            Assert.GreaterOrEqual(minScale, 0.6f - 0.001f, "no stick smaller than the authored min (0.6)");
            Assert.LessOrEqual(maxScale, 1.6f + 0.001f, "no stick larger than the authored max (1.6)");
            Assert.Less(minScale, 0.85f, "the scatter includes genuinely SMALL sticks (twigs — various sizes, AC1)");
            Assert.Greater(maxScale, 1.3f, "the scatter includes genuinely LARGE sticks (branches — various sizes, AC1)");
        }

        // === AC2: every scattered stick is a WIRED StickProp (IPickable) so the castaway can loot ANY of them ===
        [Test]
        public void BootScene_ScatterSticks_AreWiredLootable()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var scatterSticks = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<StickProp>(true))
                .Where(s => s.gameObject.name == "LP_Stick")
                .ToArray();

            Assert.Greater(scatterSticks.Length, 0, "the scatter must include wired LP_Stick props");
            foreach (var s in scatterSticks.Take(5)) // sample a few (don't loop all for speed)
            {
                Assert.IsNotNull(s.inventory, "a scatter stick is wired to the Inventory (loot-able)");
                Assert.IsTrue(s.IsAvailable, "a scatter stick ships present");
            }
        }

        // === AC1 grounding: scatter sticks rest ON the terrain (not floating far above / buried far below) ===
        [Test]
        public void BootScene_ScatterSticks_RestOnTheGround()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var sticks = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.name == "LP_Stick")
                .ToArray();

            Assert.Greater(sticks.Length, 0, "the scatter must place sticks to ground-check");
            // GroundPoint raycasts onto the terrain; the small +Y lift is ~girth*scale (≤0.08u). The island
            // terrain spans roughly [WaterY .. a few u of hills]; a stick sitting ON it stays within a sane
            // vertical band (never a far-floating or deep-buried prop). A loose band — the grounding internals
            // (GroundPoint) are UNTOUCHED, this just guards that sticks aren't authored at a silly Y.
            foreach (var s in sticks.Take(20))
                Assert.That(s.position.y, Is.InRange(-2f, 8f),
                    "a scatter stick rests within the terrain's vertical band (grounded, not floating/buried)");
        }

        // === REGRESSION: the existing tree/rock/grass/bush scatter is STILL present (the stick pass is
        // ADDITIVE — a separate sub-seed; it must not have removed or perturbed the prior passes). ===
        [Test]
        public void BootScene_ExistingScatter_StillPresent_AfterAddingSticks()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .ToArray();

            int trees = all.Count(t => t.gameObject.name == ChopTree.ScatterTreeName); // "LP_Tree"
            int rocks = all.Count(t => t.gameObject.name == "LP_Rock");
            int grass = all.Count(t => t.gameObject.name == "LP_GrassClump");
            int bushes = all.Count(t => t.gameObject.name == "LP_Bush" || t.gameObject.name == "LP_BerryBush");

            Assert.Greater(trees, 0, "trees must still scatter (the stick pass must not have removed them)");
            Assert.Greater(rocks, 0, "rocks must still scatter (additive stick pass — no regression)");
            Assert.Greater(grass, 0, "grass tufts must still scatter (additive stick pass — no regression)");
            Assert.Greater(bushes, 0, "bushes must still scatter (additive stick pass — no regression)");
        }

        // Find the FIXED wired stick authored by MovementCameraScene (named "WiredStick"), preferring it over
        // the scatter ones (named LP_Stick) so the wired-stick asserts target the deterministic one.
        private static StickProp FindWiredStick(Scene scene)
        {
            var all = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<StickProp>(true))
                .ToArray();
            return all.FirstOrDefault(s => s.gameObject.name == "WiredStick") ?? all.FirstOrDefault();
        }
    }
}
