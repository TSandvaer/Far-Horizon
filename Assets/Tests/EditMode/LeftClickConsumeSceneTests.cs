using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the LEFT-CLICK CONSUME wiring (ticket 86caf7a30) is SERIALIZED into the Boot scene
    /// the exe ships — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md, would
    /// mangle/drop an Awake-built component). Sibling of ChopSceneTests; same regression-guard intent: drop the
    /// LeftClickConsume add (or its wiring) and this goes RED in headless CI, rather than the shipped build
    /// silently lacking the consume verb.
    ///
    /// ALSO guards AC4 — the OLD consume triggers stand down in the shipped scene:
    ///   • DrinkAction.inputEnabled == false (the proximity-drink-at-pond Q trigger is REMOVED — drinking is
    ///     left-click now);
    ///   • EatBerryAction.inputEnabled == false (the E-eat trigger is removed — E loots, eating is left-click).
    /// If either re-enabled, there'd be a SECOND consume path (the "two ways to eat/drink" silent regression).
    /// </summary>
    public class LeftClickConsumeSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesLeftClickConsume_WiredToInventoryAndNeeds()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            LeftClickConsume consume = FindInScene<LeftClickConsume>(scene);
            Assert.IsNotNull(consume,
                "the Boot scene must carry LeftClickConsume — the unified left-click 'use the selected belt " +
                "item' verb (serialized editor-time, not Awake-built — editor-vs-runtime trap)");
            Assert.IsNotNull(consume.inventory,
                "LeftClickConsume.inventory must be wired editor-time so left-click reads the SELECTED belt item " +
                "without an Awake-time scene search in the build");
            Assert.IsNotNull(consume.hunger,
                "LeftClickConsume.hunger must be wired editor-time so a berry left-click restores hunger (AddFood)");
            Assert.IsNotNull(consume.thirst,
                "LeftClickConsume.thirst must be wired editor-time so a water left-click restores thirst (AddWater)");
            Assert.IsNotNull(consume.eatSeam,
                "LeftClickConsume.eatSeam must be wired editor-time so a berry left-click reuses the SHIPPED " +
                "atomic EatBerryAction.TryEatOneBerry seam (no re-implemented restore)");
            Assert.IsNotNull(consume.inventoryUI,
                "LeftClickConsume.inventoryUI must be wired editor-time (in BuildInventoryUI, after the consume " +
                "add) so a left-click OVER the belt/inventory UI does NOT consume the item behind it — an Awake " +
                "FindObjectOfType is the build-safety fallback (editor-vs-runtime trap)");
        }

        [Test]
        public void BootScene_OldDrinkTrigger_StandsDown_ProximityDrinkRemoved()
        {
            // AC4 — the proximity-drink-at-pond Q trigger is REMOVED (drinking is left-click now). The DrinkAction
            // component stays (its seam is reused as coverage), but its key INPUT must be disabled in the shipped
            // scene — else Q is a SECOND drink path racing the left-click consume.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            DrinkAction drink = FindInScene<DrinkAction>(scene);
            Assert.IsNotNull(drink, "the Boot scene still carries DrinkAction (its tested seam is retained)");
            Assert.IsFalse(drink.inputEnabled,
                "DrinkAction.inputEnabled must be FALSE in the shipped scene (AC4) — the proximity-drink-at-pond Q " +
                "trigger is removed; drinking is left-click on the selected water belt item now. A live Q binding " +
                "would be a second drink path (the 'two ways to drink' silent regression).");
        }

        [Test]
        public void BootScene_OldEatTrigger_StandsDown_ENoLongerEats()
        {
            // AC4 — the E-eat trigger is removed (E is the LOOT key now; eating is left-click). EatBerryAction
            // stays (its TryEatOneBerry seam is REUSED by LeftClickConsume), but its key INPUT must be disabled.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            EatBerryAction eat = FindInScene<EatBerryAction>(scene);
            Assert.IsNotNull(eat, "the Boot scene still carries EatBerryAction (its tested seam is reused)");
            Assert.IsFalse(eat.inputEnabled,
                "EatBerryAction.inputEnabled must be FALSE in the shipped scene (AC4) — E loots now; eating is " +
                "left-click on the selected berry belt item. A live E binding would double-fire vs the looter.");
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
