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
            Assert.IsNotNull(fire.flameVisual,
                "Campfire's flame visual must be wired editor-time so lighting the fire shows the serialized " +
                "flame (no Awake-built hierarchy to ship mangled — the legs-up class)");
            Assert.IsNotNull(fire.fireLight,
                "Campfire's warm point Light must be wired editor-time so lighting glows into the Zone-D look");
        }

        [Test]
        public void BootScene_CarriesCampfirePlacement_WiredToInventoryAndCampfire()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            CampfirePlacement place = FindInScene<CampfirePlacement>(scene);
            Assert.IsNotNull(place,
                "the Boot scene must carry the CampfirePlacement — the wood-gated BUILD interaction");
            Assert.IsNotNull(place.inventory,
                "CampfirePlacement's Inventory must be wired editor-time so the wood cost debits in the build");
            Assert.IsNotNull(place.campfire,
                "CampfirePlacement's Campfire must be wired editor-time so reaching the pit lights the fire");
            Assert.IsNotNull(place.player,
                "CampfirePlacement's player must be wired editor-time so the proximity build has a target");
            Assert.Greater(place.woodCost, 0, "the campfire must cost wood — a zero-cost fire isn't a build");
        }

        [Test]
        public void BootScene_Campfire_ShipsUnlit_Affordable_AndDark()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            Campfire fire = FindInScene<Campfire>(scene);
            Assert.IsNotNull(fire, "the Boot scene must carry the Campfire");
            Assert.IsFalse(fire.IsLit, "the fire ships UNLIT — the castaway builds + lights it");
            Assert.IsFalse(fire.fireLight.enabled,
                "the warm light ships DISABLED — the fire must not glow before it's lit");
            Assert.IsFalse(fire.flameVisual.activeSelf,
                "the flame ships HIDDEN — no flame before the fire is lit");
            Assert.Greater(fire.warmRadius, 0f, "warmRadius must be positive — a zero radius never warms");
            Assert.Greater(fire.restoreRate, 0f, "restoreRate must be positive — a lit fire must restore warmth");

            // The campfire cost must be affordable from a single felled tree (chopsToFell * woodPerChop),
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
