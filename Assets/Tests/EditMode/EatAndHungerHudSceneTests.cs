using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode scene guards for the #101 loop-verify wiring (ticket 86caa5zz3 + folded-in 86caamkxv):
    ///   (1) the SurvivalHud's HUNGER reference is wired editor-time (serialized) so the hunger bar paints
    ///       in the shipped exe — the missing piece that makes the eat loop VISIBLE;
    ///   (2) the EatBerryAction (the in-game eat INPUT — "I can't eat berries") is serialized into Boot.unity
    ///       with its Inventory + HungerNeed refs wired, so pressing E in the build eats a berry without
    ///       relying on an Awake-time FindObjectOfType.
    ///
    /// Same regression-guard intent as WarmthNeedSceneTests / HungerNeedSceneTests: drop either bootstrap
    /// wiring (hud.hunger = hunger; or survivalGo.AddComponent&lt;EatBerryAction&gt;()) and this goes RED in
    /// headless CI — rather than the shipped build silently re-shipping the "no hunger bar / can't eat" state
    /// the #101 soak rejected (the editor-vs-runtime serialization trap, unity-conventions.md).
    /// </summary>
    public class EatAndHungerHudSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_SurvivalHud_IsWiredToTheHungerNeed()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            SurvivalHud hud = FindInScene<SurvivalHud>(scene);
            Assert.IsNotNull(hud, "the Boot scene must carry the SurvivalHud");
            Assert.IsNotNull(hud.hunger,
                "the HUD's HungerNeed reference must be wired editor-time (serialized) so the HUNGER BAR " +
                "paints in the build — the #101 loop-verify piece (the player SEES hunger deplete + refill)");
            // The wired need must be the same scene HungerNeed (not a stray detached instance).
            HungerNeed sceneHunger = FindInScene<HungerNeed>(scene);
            Assert.AreSame(sceneHunger, hud.hunger,
                "the HUD must bind the SCENE HungerNeed (the same instance the eat-action restores)");
        }

        [Test]
        public void BootScene_CarriesEatBerryAction_WiredToInventoryAndHunger()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            EatBerryAction eat = FindInScene<EatBerryAction>(scene);
            Assert.IsNotNull(eat,
                "the Boot scene must carry the EatBerryAction serialized into the scene — the in-game eat " +
                "INPUT (#101: 'I can't eat berries' = no input was bound to the existing eat seam)");
            Assert.IsNotNull(eat.inventory,
                "EatBerryAction.inventory must be wired editor-time so eating consumes from the live inventory");
            Assert.IsNotNull(eat.hunger,
                "EatBerryAction.hunger must be wired editor-time so eating restores hunger through the atomic seam");
            Assert.AreNotEqual(KeyCode.None, eat.eatKey, "the eat key must be a real bound key (default E)");
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
