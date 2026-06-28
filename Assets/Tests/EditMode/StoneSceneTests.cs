using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the small-STONE E-loot feature (ticket 86caa4c96) is SERIALIZED into the Boot scene
    /// the exe ships — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md,
    /// would mangle/drop an Awake-built component or visual). Sibling of StickSceneTests; same regression-guard
    /// intent: drop MovementCameraScene.BuildWiredStone (or its wiring), or the LowPolyZoneGen stone scatter,
    /// and this goes RED in headless CI — rather than the shipped build silently lacking the small-stone gather.
    /// Also guards the REGRESSION constraint: the existing tree/rock/grass/bush/STICK scatter must still be
    /// present (the stone pass is ADDITIVE, sub-seed seed+999 — it must not have perturbed them).
    ///
    /// Binary scenes can't be GUID-grepped, so the EditMode scene-presence assert is the only authoritative
    /// reader (unity-conventions.md §Component-in-source-but-not-serialized-into-scene).
    /// </summary>
    public class StoneSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesWiredStone_WithInventoryAndRespawnerRefs()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            StoneProp stone = FindWiredStone(scene);
            Assert.IsNotNull(stone,
                "the Boot scene must carry the wired StoneProp — the deterministic loot target the castaway " +
                "walks up to (a fixed-position stone, vs the random scatter ones, so the PlayMode/capture has " +
                "a deterministic loot). Serialized, not Awake-built (editor-vs-runtime trap).");
            Assert.IsNotNull(stone.inventory,
                "StoneProp's Inventory reference must be wired editor-time so looting adds stone without an " +
                "Awake-time scene search in the build");
            Assert.IsNotNull(stone.respawner,
                "StoneProp's shared StoneRespawner reference must be wired editor-time so the `stone respawn " +
                "time` setting retunes it (AC3/AC3a)");
            Assert.IsTrue(stone.IsAvailable, "the wired stone ships present (loot-able)");
            Assert.Greater(stone.lootRadius, 0f, "lootRadius must be positive — a zero radius never loots");
            Assert.AreEqual(StoneProp.StonePerPickupDefault, stone.stonePerPickup,
                "the wired stone yields the named-constant ONE stone (the small-stone gather — AC2)");
        }

        [Test]
        public void BootScene_WiredStone_HasMesh()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            StoneProp stone = FindWiredStone(scene);
            Assert.IsNotNull(stone, "the Boot scene must carry the wired StoneProp");

            var mf = stone.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault();
            Assert.IsNotNull(mf, "the wired stone must carry a mesh child (the faceted stone)");
            Assert.IsNotNull(mf.sharedMesh, "the stone mesh must be assigned");
            Assert.Greater(mf.sharedMesh.vertexCount, 0, "the stone mesh must have geometry");
        }

        // === F3 (Devon REQUEST_CHANGES): exactly ONE shared StoneRespawner ships ===
        // The dead-knob bug shipped TWO StoneRespawners — one on the player (BuildWiredStone, pre-scatter, bound
        // NOTHING) + one on the scatter root (the 70 stones bound to it). The old assert (respawners.Length > 0)
        // was GREEN with 2, which is exactly why the dead knob slipped. Tighten to EXACTLY ONE: WireStoneScatterRoot
        // canonicalises + destroys the stray. Re-introduce the second add -> this goes RED.
        [Test]
        public void BootScene_CarriesExactlyOneSharedStoneRespawner_WithDefaultWindow()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var respawners = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<StoneRespawner>(true))
                .ToArray();

            Assert.AreEqual(1, respawners.Length,
                "the Boot scene must carry EXACTLY ONE shared StoneRespawner (F3) — the dead-knob bug shipped " +
                "TWO (a stray player-side one binding nothing + the scatter-bound one), and the `stone respawn " +
                "time` slider then tuned an arbitrary population. WireStoneScatterRoot must canonicalise to one. " +
                "Re-add the second respawner -> red.");
            var resp = respawners[0];
            Assert.GreaterOrEqual(resp.RespawnMinSeconds, 0f, "the respawn min must be non-negative");
            Assert.GreaterOrEqual(resp.RespawnMaxSeconds, resp.RespawnMinSeconds,
                "the respawn max must be >= min (a sane window — the ~10-min default, AC3)");
        }

        // === F3 BINDING-IDENTITY (Devon REQUEST_CHANGES): the SETTINGS slider + the wired stone + the scatter
        // stones all bind the SAME StoneRespawner instance ===
        // This is the assert that actually proves the `stone respawn time` knob is LIVE: SettingsPanel.stoneRespawner
        // (what SettingsCatalog.PopulateStones drives) must be reference-equal to the respawner the 70 scatter
        // stones read AND to the wired stone's respawner. With the old dual-respawner bug, SettingsPanel resolved
        // an ARBITRARY one (could be the player-side dead one) -> the slider drove a population the scatter stones
        // never read. WireStoneScatterRoot serializes SettingsPanel.stoneRespawner to the scatter-bound instance.
        [Test]
        public void BootScene_SettingsSlider_BindsSameRespawner_AsTheScatterStones()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            var panel = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<SettingsPanel>(true))
                .FirstOrDefault();
            Assert.IsNotNull(panel, "the Boot scene must carry the SettingsPanel (the `stone respawn time` host)");
            Assert.IsNotNull(panel.stoneRespawner,
                "SettingsPanel.stoneRespawner must be SERIALIZED (not left null for an Awake FindObjectOfType to " +
                "resolve arbitrarily — the F3 dead-knob path). WireStoneScatterRoot must wire it.");

            var scatterStones = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<StoneProp>(true))
                .Where(s => s.gameObject.name == "LP_Stone")
                .ToArray();
            Assert.Greater(scatterStones.Length, 0, "the scatter must include wired LP_Stone props to compare against");

            // Binding-identity: the slider drives the SAME instance the scatter stones read (reference-equal).
            foreach (var s in scatterStones.Take(10))
                Assert.AreSame(panel.stoneRespawner, s.respawner,
                    "the `stone respawn time` slider (SettingsPanel.stoneRespawner) must be the SAME StoneRespawner " +
                    "instance the scatter stones read — else the slider tunes a population no stone reads (F3 dead " +
                    "knob). WireStoneScatterRoot canonicalises both to one instance.");

            // ...and the deterministic WIRED stone binds the same one too (it was left null pre-scatter, then
            // wired by WireStoneScatterRoot).
            var wired = FindWiredStone(scene);
            Assert.IsNotNull(wired, "the Boot scene must carry the wired StoneProp");
            Assert.AreSame(panel.stoneRespawner, wired.respawner,
                "the wired stone must bind the SAME canonical StoneRespawner the slider drives (F3)");
        }

        // === AC1: SMALL stones scattered across the seed-42 island in VARIOUS (SMALL) SIZES ===
        [Test]
        public void BootScene_ScattersStones_AcrossTheIsland_VariedSmallSizes()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var stones = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.name == "LP_Stone")
                .ToArray();

            Assert.Greater(stones.Length, 20,
                "the island must be scattered with small stones (>20) — AC1 (drop the scatter loop -> red)");

            // VARIOUS (SMALL) SIZES (AC1): scale spans the authored 0.35-0.80 band, with real spread, and stays
            // distinctly SMALLER than the boulders (only small stones are pickable; bigger rocks are OOS).
            float minScale = stones.Min(s => s.localScale.x);
            float maxScale = stones.Max(s => s.localScale.x);
            Assert.GreaterOrEqual(minScale, 0.35f - 0.001f, "no stone smaller than the authored min (0.35)");
            Assert.LessOrEqual(maxScale, 0.80f + 0.001f,
                "no scatter stone larger than the authored small-stone max (0.80) — bigger rocks are OOS");
            Assert.Less(minScale, 0.55f, "the scatter includes genuinely SMALL pebbles (various sizes, AC1)");
            Assert.Greater(maxScale, 0.60f, "the scatter includes the larger small-stones (various sizes, AC1)");
        }

        // === AC2: every scattered stone is a WIRED StoneProp (IPickable) so the castaway can loot ANY of them ===
        [Test]
        public void BootScene_ScatterStones_AreWiredLootable_WithRespawner()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var scatterStones = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<StoneProp>(true))
                .Where(s => s.gameObject.name == "LP_Stone")
                .ToArray();

            Assert.Greater(scatterStones.Length, 0, "the scatter must include wired LP_Stone props");
            foreach (var s in scatterStones.Take(5)) // sample a few (don't loop all for speed)
            {
                Assert.IsNotNull(s.inventory, "a scatter stone is wired to the Inventory (loot-able)");
                Assert.IsNotNull(s.respawner, "a scatter stone is wired to the shared StoneRespawner (AC3)");
                Assert.IsTrue(s.IsAvailable, "a scatter stone ships present");
            }
        }

        // === AC4 grounding: scatter stones rest ON the terrain (not floating far above / buried far below) ===
        [Test]
        public void BootScene_ScatterStones_RestOnTheGround()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var stones = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.name == "LP_Stone")
                .ToArray();

            Assert.Greater(stones.Length, 0, "the scatter must place stones to ground-check");
            // GroundPoint raycasts onto the terrain. A stone sitting ON it stays within a sane vertical band
            // (never a far-floating or deep-buried prop). A loose band — the grounding internals (GroundPoint)
            // are UNTOUCHED, this just guards that stones aren't authored at a silly Y (mirror of the sticks).
            foreach (var s in stones.Take(20))
                Assert.That(s.position.y, Is.InRange(-2f, 8f),
                    "a scatter stone rests within the terrain's vertical band (grounded, not floating/buried)");
        }

        // === REGRESSION: the existing tree/rock/grass/bush/STICK scatter is STILL present (the stone pass is
        // ADDITIVE — a separate sub-seed seed+999; it must not have removed or perturbed the prior passes,
        // INCLUDING the sticks now on main — the byte-stable carry the ticket calls out). ===
        [Test]
        public void BootScene_ExistingScatter_StillPresent_AfterAddingStones()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .ToArray();

            int trees = all.Count(t => t.gameObject.name == ChopTree.ScatterTreeName); // "LP_Tree"
            int rocks = all.Count(t => t.gameObject.name == "LP_Rock");
            int grass = all.Count(t => t.gameObject.name == "LP_GrassClump");
            int bushes = all.Count(t => t.gameObject.name == "LP_Bush" || t.gameObject.name == "LP_BerryBush");
            int sticks = all.Count(t => t.gameObject.name == "LP_Stick");

            Assert.Greater(trees, 0, "trees must still scatter (the stone pass must not have removed them)");
            Assert.Greater(rocks, 0, "rocks must still scatter (additive stone pass — no regression)");
            Assert.Greater(grass, 0, "grass tufts must still scatter (additive stone pass — no regression)");
            Assert.Greater(bushes, 0, "bushes must still scatter (additive stone pass — no regression)");
            Assert.Greater(sticks, 0,
                "the 70 sticks (now on main) must still scatter (the stone pass is additive seed+999 — the " +
                "sticks stream is byte-stable, not perturbed)");
        }

        // Find the FIXED wired stone authored by MovementCameraScene (named "WiredStone"), preferring it over
        // the scatter ones (named LP_Stone) so the wired-stone asserts target the deterministic one.
        private static StoneProp FindWiredStone(Scene scene)
        {
            var all = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<StoneProp>(true))
                .ToArray();
            return all.FirstOrDefault(s => s.gameObject.name == "WiredStone") ?? all.FirstOrDefault();
        }
    }
}
