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
        public void BootScene_CarriesDeterministicWiredBerryBush_TheLootGateTarget()
        {
            // REGRESSION GUARD (PR #162 loot-gate flake): the LootPromptVerifyCapture -verifyLoot gate now targets
            // the DETERMINISTIC wired bush — the GameObject named exactly "BerryBush" at the loop-centre clearing,
            // over solid navmesh, ripe + inventory-wired at spawn — instead of an arbitrary scatter bush picked by
            // (build-unstable) InstanceID order. If a future change renames/drops that wired bush, the gate would
            // SILENTLY fall back to a flaky coast-edge scatter bush (the NavMeshAgent-re-snap false-fail this PR
            // fixes). This test fails RED in CI the moment the deterministic target disappears, so the regression
            // can't slip back in. (The wired scatter bushes are named "LP_BerryBush" — distinct on purpose.)
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var wired = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BerryBush>(true))
                .FirstOrDefault(b => b.gameObject.name == "BerryBush");

            Assert.IsNotNull(wired,
                "the Boot scene must carry the deterministic wired BerryBush (GameObject named exactly " +
                "\"BerryBush\") — the -verifyLoot gate's stable target. A scatter bush (\"LP_BerryBush\") is " +
                "NOT a safe substitute (its InstanceID order is build-unstable + it can sit off-navmesh).");
            Assert.IsTrue(wired.hasBerries && wired.IsRipe,
                "the wired BerryBush must be a RIPE berry variant at spawn (the gate teleports the player into " +
                "its range to show the prompt — a bare/decorative bush is not loot-able)");
            Assert.IsNotNull(wired.inventory,
                "the wired BerryBush's inventory must be wired editor-time so it reports CanLoot==true at spawn " +
                "(the looter's nearest-in-range resolve skips a bush with no inventory)");
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
