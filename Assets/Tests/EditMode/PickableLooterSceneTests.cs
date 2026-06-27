using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the E-LOOT interactor (ticket 86caf7a6q — <see cref="PickableLooter"/>) is
    /// SERIALIZED into the Boot scene the exe ships, with its Inventory + player refs wired editor-time —
    /// NOT added at Awake (the component-in-source-but-not-serialized-into-scene trap, unity-conventions.md:
    /// a MonoBehaviour can compile + pass every script test while the scene never carries it, shipping the
    /// feature silently inert — exactly the CaptureGate U7 failure class). Sibling of BushSceneTests /
    /// ChopSceneTests; same intent: drop MovementCameraScene.BuildPickableLooter (or its wiring) and this
    /// goes RED in headless CI rather than the shipped build silently lacking the E-loot input.
    ///
    /// Binary scenes can't be GUID-grepped, so the EditMode scene-presence assert is the only authoritative
    /// reader (unity-conventions.md §Component-in-source-but-not-serialized-into-scene).
    /// </summary>
    public class PickableLooterSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesWiredPickableLooter_WithInventoryAndPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var looter = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<PickableLooter>(true))
                .FirstOrDefault();

            Assert.IsNotNull(looter,
                "the Boot scene must carry the wired PickableLooter — the E-loot input the player presses to " +
                "loot world items. Serialized editor-time (NOT Awake-added), or the feature ships inert.");
            Assert.IsNotNull(looter.inventory,
                "PickableLooter's Inventory reference must be wired editor-time so looting adds items without " +
                "an Awake-time scene search in the build");
            Assert.IsNotNull(looter.player,
                "PickableLooter's player reference (the position the nearest-in-range resolve measures from) " +
                "must be wired editor-time");
        }

        [Test]
        public void BootScene_PickableLooter_BindsLootToE()
        {
            // The loot key must be E — the universal pick-up/loot key (DECISIONS 2026-06-27). A letter key,
            // so it is layout-agnostic on the Sponsor's Danish keyboard ([[sponsor-danish-keyboard-layout]]).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var looter = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<PickableLooter>(true))
                .FirstOrDefault();
            Assert.IsNotNull(looter, "the Boot scene must carry the wired PickableLooter");
            Assert.AreEqual(UnityEngine.KeyCode.E, looter.lootKey,
                "E is the universal loot key (DECISIONS 2026-06-27; a letter key = Danish-layout safe)");
        }

        [Test]
        public void BootScene_EatBerryAction_DoesNotKeyEatOnE_ENowReclaimedForLoot()
        {
            // 86caf7a6q reclaims E for LOOT — so the shipped EatBerryAction must NOT also key-eat (a live E
            // binding would double-fire against the looter, the 'dead second path' AC4 forbids). The eat
            // INPUT moves to left-click (86caf7a30); EatBerryAction.inputEnabled must be FALSE in the build.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var eat = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<EatBerryAction>(true))
                .FirstOrDefault();
            Assert.IsNotNull(eat, "the Boot scene must still carry EatBerryAction (its consume seam is preserved)");
            Assert.IsFalse(eat.inputEnabled,
                "EatBerryAction must NOT read its key in the shipped scene — E is reclaimed for loot (no " +
                "double-fire). The eat INPUT moves to left-click-consume (86caf7a30).");
        }
    }
}
