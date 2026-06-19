using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the THIRST need (ticket 86caamkv7) is SERIALIZED into the Boot scene the exe
    /// ships — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md, would
    /// mangle/drop an Awake-built component). Sibling of HungerNeedSceneTests / WarmthNeedSceneTests; same
    /// regression-guard intent: drop the bootstrap wiring (survivalGo.AddComponent&lt;ThirstNeed&gt;()) and
    /// this goes RED in headless CI, rather than the shipped build silently lacking the thirst need.
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
                "the Boot scene must carry the ThirstNeed component serialized into the scene — the " +
                "third survival need ships from this scene, not an Awake add (unity-conventions.md " +
                "editor-vs-runtime trap)");
            Assert.Greater(need.max, 0f, "ThirstNeed.max must be a positive bar value");
            Assert.Greater(need.decayPerSecond, 0f,
                "ThirstNeed.decayPerSecond must be positive — a non-decaying need has no loop pressure");

            // Thirst reads as a FASTER background pressure than hunger (pressing after eating) but slower
            // than warmth — guard the shipped scene default so the fiction holds in the actual build.
            Assert.Greater(need.decayPerSecond, HungerNeed.HungerMedDecayPerSecond,
                "the shipped ThirstNeed decay must be faster than hunger's (pressing after eating)");
            Assert.Less(need.decayPerSecond, 0.55f,
                "the shipped ThirstNeed decay must stay slower than warmth's 0.55 (warmth is most pressing)");
            Assert.Greater(need.waterScoopAmount, 0f,
                "ThirstNeed.waterScoopAmount must be positive — a hand-scoop must restore SOMETHING");
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
