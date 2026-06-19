using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the HUNGER need (ticket 86caamkp8) is SERIALIZED into the Boot scene the exe
    /// ships — not added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md, would
    /// mangle/drop an Awake-built component). Sibling of WarmthNeedSceneTests; same regression-guard
    /// intent: drop the bootstrap wiring (survivalGo.AddComponent&lt;HungerNeed&gt;()) and this goes RED in
    /// headless CI, rather than the shipped build silently lacking the hunger need.
    /// </summary>
    public class HungerNeedSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesHungerNeed_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            HungerNeed need = FindInScene<HungerNeed>(scene);
            Assert.IsNotNull(need,
                "the Boot scene must carry the HungerNeed component serialized into the scene — the " +
                "second survival need ships from this scene, not an Awake add (unity-conventions.md " +
                "editor-vs-runtime trap)");
            Assert.Greater(need.max, 0f, "HungerNeed.max must be a positive bar value");
            Assert.Greater(need.decayPerSecond, 0f,
                "HungerNeed.decayPerSecond must be positive — a non-decaying need has no loop pressure");

            // Hunger reads as a SLOWER background pressure than warmth — guard the shipped scene default
            // against WarmthNeed's 0.55 so the fiction holds in the actual build, not just in unit config.
            Assert.Less(need.decayPerSecond, 0.55f,
                "the shipped HungerNeed decay must be slower than warmth's 0.55 (slower background pressure)");
            Assert.Greater(need.berryRestoreAmount, 0f,
                "HungerNeed.berryRestoreAmount must be positive — eating a berry must restore SOMETHING");

            // #101 EAT-REFILL FIX (regression guard): the shipped hunger must NOT start FULL, else an early
            // eat clamps against an already-full bar with NO visible refill (the exact percept the #101 soak
            // rejected) AND SetCurrent's Approximately early-return means Changed never fires. It must ship
            // pressured-WITH-HEADROOM: below max so an eat climbs visibly, above the floor so it can decay too.
            Assert.IsFalse(need.startFull,
                "the shipped HungerNeed must NOT start full — an eat against a full bar shows no refill (#101). " +
                "Ship pressured-with-headroom (startFull=false + a mid startFraction01).");
            Assert.Greater(need.startFraction01, need.floor01,
                "the shipped hunger start fraction must sit ABOVE the floor (room to decay into)");
            Assert.Less(need.startFraction01, 1f,
                "the shipped hunger start fraction must sit BELOW full (room for an eat to VISIBLY refill into)");
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
