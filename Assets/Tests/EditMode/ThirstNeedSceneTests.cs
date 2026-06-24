using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the THIRST need (ticket 86caamkv7) is SERIALIZED into the Boot scene the exe ships
    /// — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md, would mangle/drop
    /// an Awake-built component). Sibling of HungerNeedSceneTests / WarmthNeedSceneTests; same regression-guard
    /// intent: drop the bootstrap wiring (survivalGo.AddComponent&lt;ThirstNeed&gt;()) and this goes RED in
    /// headless CI, rather than the shipped build silently lacking the thirst need.
    /// </summary>
    public class ThirstNeedSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesThirstNeed_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            ThirstNeed need = FindInScene<ThirstNeed>(scene);
            Assert.IsNotNull(need,
                "the Boot scene must carry the ThirstNeed component serialized into the scene — the third " +
                "survival need ships from this scene, not an Awake add (unity-conventions.md editor-vs-runtime trap)");
            Assert.Greater(need.max, 0f, "ThirstNeed.max must be a positive bar value");
            Assert.Greater(need.decayPerSecond, 0f,
                "ThirstNeed.decayPerSecond must be positive — a non-decaying need has no loop pressure");

            // Thirst reads as a FASTER pressure than hunger but slower than warmth (the "thirsty after the
            // berries" fiction) — guard the shipped scene default ordering so the fiction holds in the actual
            // build, not just in unit config.
            Assert.Greater(need.decayPerSecond, HungerNeed.HungerMedDecayPerSecond,
                "the shipped ThirstNeed decay must be FASTER than hunger's (thirsty after the berries)");
            Assert.Less(need.decayPerSecond, 0.55f,
                "the shipped ThirstNeed decay must be slower than warmth's 0.55 (warmth bleeds fastest)");
            Assert.Greater(need.waterScoopAmount, 0f,
                "ThirstNeed.waterScoopAmount must be positive — a scoop must restore SOMETHING");

            // EAT-REFILL-PARITY (regression guard, the #101 class): the shipped thirst must NOT start FULL,
            // else an early scoop clamps against an already-full bar with NO visible refill AND Changed never
            // fires. It must ship pressured-WITH-HEADROOM: below max so a scoop climbs visibly, above the floor
            // so it can decay too.
            Assert.IsFalse(need.startFull,
                "the shipped ThirstNeed must NOT start full — a scoop against a full bar shows no refill (#101 class). " +
                "Ship pressured-with-headroom (startFull=false + a mid startFraction01).");
            Assert.Greater(need.startFraction01, need.floor01,
                "the shipped thirst start fraction must sit ABOVE the floor (room to decay into)");
            Assert.Less(need.startFraction01, 1f,
                "the shipped thirst start fraction must sit BELOW full (room for a scoop to VISIBLY refill into)");
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
