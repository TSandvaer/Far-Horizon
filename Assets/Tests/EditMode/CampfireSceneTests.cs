using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the U2-4 campfire (ticket 86ca8bdep) — the loop's CLOSE — is SERIALIZED into
    /// the Boot scene the exe ships, not added at Awake (the editor-vs-runtime serialization trap,
    /// unity-conventions.md, would mangle/drop an Awake-built campfire/flame/light). Sibling of
    /// ChopSceneTests; same regression-guard intent: drop MovementCameraScene.BuildCampfire (or its
    /// wiring) and this goes RED in headless CI, rather than the shipped build silently lacking the
    /// thing that closes the survival loop.
    /// </summary>
    public class CampfireSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesCampfire_WiredToWarmthAndPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            Campfire fire = FindInScene<Campfire>(scene);
            Assert.IsNotNull(fire,
                "the Boot scene must carry the Campfire — the loop's CLOSE the castaway builds + lights " +
                "(unity-conventions.md editor-vs-runtime trap: serialized, not Awake-built)");
            Assert.IsNotNull(fire.warmth,
                "Campfire's WarmthNeed reference must be wired editor-time so the lit fire restores warmth " +
                "without an Awake-time scene search in the build");
            Assert.IsNotNull(fire.player,
                "Campfire's player reference (the moving agent root) must be wired editor-time so the " +
                "proximity warmth-restore has a target without an Awake-time scene search");
            Assert.IsNotNull(fire.visual,
                "Campfire's visual root must be wired editor-time (the structure the place-to-build flow reveals — ⑤)");
            Assert.IsNotNull(fire.flameVisual,
                "Campfire's flame visual must be wired editor-time so lighting the fire shows the serialized " +
                "flame (no Awake-built hierarchy to ship mangled — the legs-up class)");
            Assert.IsNotNull(fire.fireLight,
                "Campfire's warm point Light must be wired editor-time so lighting glows into the Zone-D look");
            Assert.IsNotNull(fire.placementObstacle,
                "Campfire's placementObstacle must be wired editor-time so the PLACED campfire self-registers a " +
                "no-build zone (the #302 seam — a later placement ghost reads red over it)");
        }

        [Test]
        public void BootScene_CarriesCampfirePlacement_WiredForPlaceToBuild()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            CampfirePlacement place = FindInScene<CampfirePlacement>(scene);
            Assert.IsNotNull(place,
                "the Boot scene must carry the CampfirePlacement — the wood+stone place-to-build driver (⑤)");
            Assert.IsNotNull(place.inventory,
                "CampfirePlacement.inventory must be wired editor-time so the mats cost debits on confirm");
            Assert.IsNotNull(place.campfire,
                "CampfirePlacement.campfire must be wired editor-time so confirm reveals + lights the campfire");
            Assert.IsNotNull(place.player,
                "CampfirePlacement.player must be wired editor-time (the off-self distance check)");
            Assert.IsNotNull(place.ghost,
                "CampfirePlacement.ghost must be wired editor-time (the translucent placement preview — ⑤ place-to-build)");
            Assert.IsNotNull(place.warmth,
                "CampfirePlacement.warmth must be wired editor-time so the placed fire binds the need it restores");
            Assert.AreNotEqual(0, place.groundMask.value,
                "CampfirePlacement.groundMask must be wired (the ghost ground snap + the navmesh-availability gate)");
            Assert.AreEqual(CampfirePlacement.CampfireWoodCostDefault, place.woodCost,
                "campfire wood cost = the §5 baseline default (3)");
            Assert.AreEqual(CampfirePlacement.CampfireStoneCostDefault, place.stoneCost,
                "campfire STONE cost = the NIT-3 default (2 — the vision's 'stone AND wood')");
            Assert.Greater(place.stoneCost, 0, "the campfire must cost stone too — the vision's 'stone AND wood' (NIT-3)");

            // The ghost ships hidden too (shown only during placement).
            foreach (var r in place.ghost.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(r.enabled, "the campfire placement ghost must be serialized hidden (shown only while placing)");
        }

        [Test]
        public void BootScene_Campfire_ShipsUnlit_Invisible_Affordable_AndDark()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            Campfire fire = FindInScene<Campfire>(scene);
            Assert.IsNotNull(fire, "the Boot scene must carry the Campfire");
            Assert.IsFalse(fire.IsLit, "the fire ships UNLIT — the castaway builds + lights it");
            Assert.IsFalse(fire.IsPlaced, "the campfire ships UNPLACED — invisible until the player places it (⑤ §0.1)");
            Assert.IsFalse(fire.fireLight.enabled,
                "the warm light ships DISABLED — the fire must not glow before it's lit");
            Assert.IsFalse(fire.flameVisual.activeSelf,
                "the flame ships HIDDEN — no flame before the fire is lit");

            // ⑤ invisible-until-placed: the campfire STRUCTURE renderers (stone ring + logs) ship DISABLED — there
            // is NO pre-visible fire pit (Sponsor-locked, spec §0.1). The flame child is lit-state-driven (SetActive
            // off), so exclude it from this check (its renderers may be enabled under an inactive GameObject).
            Assert.IsNotNull(fire.visual, "the campfire visual root must be wired");
            foreach (var r in fire.visual.GetComponentsInChildren<Renderer>(true))
            {
                if (fire.flameVisual != null && r.transform.IsChildOf(fire.flameVisual.transform)) continue;
                Assert.IsFalse(r.enabled,
                    "the campfire structure renderers must ship DISABLED — invisible until the player places it (§0.1)");
            }
            Assert.IsNotNull(fire.placementObstacle, "the campfire placementObstacle must be wired");
            Assert.IsFalse(fire.placementObstacle.enabled,
                "the campfire placementObstacle ships DISABLED — an unplaced/invisible campfire must NOT block placement");
            Assert.Greater(fire.warmRadius, 0f, "warmRadius must be positive — a zero radius never warms");
            Assert.Greater(fire.restoreRate, 0f, "restoreRate must be positive — a lit fire must restore warmth");

            // The campfire wood cost must be affordable from a single felled tree (chopsToFell * woodPerChop),
            // so the loop closes within one chop session — else the loop is un-closable as authored.
            CampfirePlacement place = FindInScene<CampfirePlacement>(scene);
            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(place); Assert.IsNotNull(tree);
            Assert.LessOrEqual(place.woodCost, tree.chopsToFell * tree.woodPerChop,
                "the campfire's wood cost must be affordable from ONE felled tree — else the authored loop " +
                "can never close (a one-tree chop session must fund one fire)");

            // And the restore must outpace decay so the bar visibly CLIMBS at the fire (the felt payoff).
            WarmthNeed need = FindInScene<WarmthNeed>(scene);
            Assert.IsNotNull(need);
            Assert.Greater(fire.restoreRate, need.decayPerSecond,
                "restoreRate must exceed WarmthNeed.decayPerSecond so warmth net-RISES at the fire");
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
    }
}
