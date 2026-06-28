using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the LOOT PROXIMITY PROMPT (ticket 86cafc6ud — <see cref="LootPrompt"/>) is SERIALIZED
    /// into the Boot scene the exe ships, with its <see cref="PickableLooter"/> ref wired editor-time — NOT
    /// added at Awake (the component-in-source-but-not-serialized-into-scene trap, unity-conventions.md). Sibling
    /// of PickableLooterSceneTests; same intent: drop the LootPrompt wiring in MovementCameraScene.BuildPickableLooter
    /// and this goes RED in headless CI rather than the shipped build silently lacking the on-screen prompt.
    ///
    /// The wiring guarantees the SINGLE source of truth (AC3): the prompt's looter ref IS the player's looter,
    /// so the prompt reads the SAME nearest-in-range resolve the E press uses — the prompt and the actual loot
    /// can never disagree.
    /// </summary>
    public class LootPromptSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesWiredLootPrompt_BoundToTheLooter()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var prompt = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<LootPrompt>(true))
                .FirstOrDefault();

            Assert.IsNotNull(prompt,
                "the Boot scene must carry the wired LootPrompt — the 'Press E to pick up {name}' tooltip. " +
                "Serialized editor-time (NOT Awake-added), or the feature ships inert.");
            Assert.IsNotNull(prompt.looter,
                "LootPrompt's PickableLooter reference must be wired editor-time — it IS the single source of " +
                "truth (the prompt reads the same nearest-in-range the E press loots)");

            // The prompt and the looter must be the SAME interaction — the prompt's looter is a looter that
            // actually lives in the scene (the player's), so what the prompt names is what E loots.
            var sceneLooter = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<PickableLooter>(true))
                .FirstOrDefault();
            Assert.IsNotNull(sceneLooter, "the Boot scene must carry the PickableLooter the prompt binds to");
            Assert.AreSame(sceneLooter, prompt.looter,
                "the prompt binds the SAME looter the player loots with (single source of truth — the prompt " +
                "names exactly what E loots)");
        }

        [Test]
        public void BootScene_LootPrompt_NamesTheLiteralEKey()
        {
            // The prompt's key must mirror the looter's E (a letter key — layout-agnostic on the Danish keyboard).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var prompt = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<LootPrompt>(true))
                .FirstOrDefault();
            Assert.IsNotNull(prompt, "the Boot scene must carry the wired LootPrompt");
            Assert.AreEqual(UnityEngine.KeyCode.E, prompt.lootKey,
                "the prompt names E (the universal loot key; a letter = Danish-layout safe)");
        }
    }
}
