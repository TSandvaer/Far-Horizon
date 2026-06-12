using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the standard shipped-build capture component (ticket 86ca86g7k).
    ///
    /// The shipped-build capture gate is only meaningful if CaptureGate is actually SERIALIZED
    /// into the Boot scene that the exe ships — not added at Awake (the editor-vs-runtime
    /// serialization trap, unity-conventions.md, would mangle/drop it). This asserts the SAVED
    /// scene carries CaptureGate, so a future change that drops the bootstrap wiring
    /// (`hudGo.AddComponent&lt;CaptureGate&gt;()`) fails HERE in headless CI, not by the capture step
    /// silently inspecting a scene that has no capture component.
    ///
    /// Regression guard: delete the AddComponent line in BootstrapProject.BuildBootScene and this
    /// test goes red.
    /// </summary>
    public class CaptureGateSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesCaptureGate_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            CaptureGate gate = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                gate = root.GetComponentInChildren<CaptureGate>();
                if (gate != null) break;
            }
            Assert.IsNotNull(gate,
                "the Boot scene must carry the CaptureGate component serialized into the scene " +
                "(the shipped-build capture gate inspects exactly these bytes; an Awake-only add " +
                "would not ship — unity-conventions.md editor-vs-runtime trap)");
            Assert.Greater(gate.defaultFrames, 0,
                "CaptureGate.defaultFrames must be a positive frame count");
        }
    }
}
