using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the I-3 FORGE (ticket 86cakkmvc) — the "work half" of the iron earn — is SERIALIZED
    /// into the Boot scene the exe ships, not added at Awake (the editor-vs-runtime serialization trap would
    /// mangle/drop an Awake-built furnace/glow/light). Sibling of CampfireSceneTests + MineSceneTests; same
    /// regression-guard intent: drop MovementCameraScene.BuildForge (or its wiring) and this goes RED in headless
    /// CI, rather than the shipped build silently lacking the smelt beat.
    /// </summary>
    public class ForgeSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesForge_WiredToInventoryPlayerGlowAndLight()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            Forge forge = FindInScene<Forge>(scene);
            Assert.IsNotNull(forge,
                "the Boot scene must carry the Forge — the iron-chain 'smelt' beat (serialized, not Awake-built)");
            Assert.IsNotNull(forge.inventory,
                "Forge.inventory must be wired editor-time so a smelt debits ore+fuel + adds ingots without an " +
                "Awake scene-search in the build");
            Assert.IsNotNull(forge.player,
                "Forge.player must be wired editor-time so the proximity smelt check has a target");
            Assert.IsNotNull(forge.glowVisual,
                "Forge.glowVisual must be wired editor-time so the working furnace shows the serialized glow " +
                "(no Awake-built hierarchy to ship mangled — the legs-up class)");
            Assert.IsNotNull(forge.forgeLight,
                "Forge.forgeLight must be wired editor-time so a smelting furnace glows into the Zone-D look");
        }

        [Test]
        public void BootScene_CarriesForgePlacement_WiredToInventoryAndForge()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            ForgePlacement place = FindInScene<ForgePlacement>(scene);
            Assert.IsNotNull(place,
                "the Boot scene must carry the ForgePlacement — the wood+stone-gated BUILD interaction");
            Assert.IsNotNull(place.inventory,
                "ForgePlacement.inventory must be wired editor-time so the mats cost debits in the build");
            Assert.IsNotNull(place.forge,
                "ForgePlacement.forge must be wired editor-time so reaching the spot builds the forge");
            Assert.IsNotNull(place.player,
                "ForgePlacement.player must be wired editor-time so the proximity build has a target");
            Assert.Greater(place.woodCost, 0, "the forge must cost wood — a zero-cost furnace isn't a build");
            Assert.Greater(place.stoneCost, 0, "the forge must cost STONE — a stone furnace needs stone (the I-3 gate)");
        }

        [Test]
        public void BootScene_Forge_ShipsUnbuilt_Cold_AndWithSaneSmeltDefaults()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            Forge forge = FindInScene<Forge>(scene);
            Assert.IsNotNull(forge, "the Boot scene must carry the Forge");
            Assert.IsFalse(forge.IsBuilt, "the forge ships UNBUILT — the castaway builds it from mats");
            Assert.IsFalse(forge.IsSmelting, "the forge ships IDLE — nothing smelting before it's built + fed");
            Assert.IsFalse(forge.forgeLight.enabled,
                "the forge Light ships DISABLED — a cold furnace must not glow before it's working");
            Assert.IsFalse(forge.glowVisual.activeSelf,
                "the forge glow ships HIDDEN — no glow before the furnace is smelting");
            Assert.Greater(forge.smeltRadius, 0f, "smeltRadius must be positive — a zero radius never smelts");

            // The smelt-cost defaults must sit within the registered dial bands (the live smelt_* clamp band).
            Assert.GreaterOrEqual(forge.orePerIngot, SettingsCatalog.SmeltOrePerIngotMin);
            Assert.LessOrEqual(forge.orePerIngot, SettingsCatalog.SmeltOrePerIngotMax);
            Assert.GreaterOrEqual(forge.fuelPerSmelt, SettingsCatalog.SmeltFuelCostMin);
            Assert.LessOrEqual(forge.fuelPerSmelt, SettingsCatalog.SmeltFuelCostMax);
            Assert.GreaterOrEqual(forge.smeltSeconds, SettingsCatalog.SmeltTimeMin);
            Assert.LessOrEqual(forge.smeltSeconds, SettingsCatalog.SmeltTimeMax);
        }

        // The forge is a DISTINCT structure from the campfire (Sponsor Q3) — pin that they are NOT the same
        // GameObject and sit at different spots, so a future refactor can't quietly collapse the furnace into the fire.
        [Test]
        public void BootScene_Forge_IsDistinctFromTheCampfire()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Forge forge = FindInScene<Forge>(scene);
            Campfire fire = FindInScene<Campfire>(scene);
            Assert.IsNotNull(forge, "the Boot scene must carry the Forge");
            Assert.IsNotNull(fire, "the Boot scene must carry the Campfire");
            Assert.AreNotSame(forge.gameObject, fire.gameObject,
                "the forge must be a DISTINCT GameObject from the campfire (Sponsor Q3 — a separate structure)");
            float d = Vector3.Distance(forge.transform.position, fire.transform.position);
            Assert.Greater(d, 1.0f,
                "the forge must sit at its OWN spot, not on top of the campfire (distinct build beats) — dist=" + d);
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
