using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the U2-1 survival need (ticket 86ca8bd9m) is SERIALIZED into the Boot
    /// scene the exe ships — not added at Awake (the editor-vs-runtime serialization trap,
    /// unity-conventions.md, would mangle/drop an Awake-built component). Sibling of
    /// CaptureGateSceneTests; same regression-guard intent: drop the bootstrap wiring
    /// (survivalGo.AddComponent&lt;WarmthNeed&gt;()/&lt;WarmthReadout&gt;()) and this goes RED in headless CI,
    /// rather than the shipped build silently lacking the need that drives the whole loop.
    /// </summary>
    public class WarmthNeedSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesWarmthNeed_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            WarmthNeed need = FindInScene<WarmthNeed>(scene);
            Assert.IsNotNull(need,
                "the Boot scene must carry the WarmthNeed component serialized into the scene — the " +
                "single survival need that drives the M-U2 loop ships from this scene, not an Awake add " +
                "(unity-conventions.md editor-vs-runtime trap)");
            Assert.Greater(need.max, 0f, "WarmthNeed.max must be a positive bar value");
            Assert.Greater(need.decayPerSecond, 0f,
                "WarmthNeed.decayPerSecond must be positive — a non-decaying need has no loop pressure");
        }

        [Test]
        public void BootScene_CarriesSurvivalHud_WiredToTheNeed()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            SurvivalHud hud = FindInScene<SurvivalHud>(scene);
            Assert.IsNotNull(hud,
                "the Boot scene must carry the U2-5 SurvivalHud (it SUPERSEDES the U2-1 WarmthReadout " +
                "placeholder) so warmth is VISIBLE in the shipped exe (AC: decay visible to the player)");
            Assert.IsNotNull(hud.warmth,
                "the HUD's WarmthNeed reference must be wired editor-time (serialized), so the bar paints " +
                "without depending on an Awake-time FindObjectOfType in the build");
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
