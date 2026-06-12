using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode smoke + bootstrap-integrity guards. These assert the REAL project state the
    /// bootstrap produced — not a trivially-true tautology — so a broken bootstrap (URP not
    /// assigned, wrong product name, missing boot scene) fails in headless CI before it ships.
    ///
    /// Regression guard: if a future change reverts the project to built-in RP, renames the
    /// product, or drops the boot scene from EditorBuildSettings, exactly one of these fails.
    /// </summary>
    public class BootSmokeTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void ProductName_IsFarHorizon()
        {
            Assert.AreEqual("Far Horizon", PlayerSettings.productName,
                "bootstrap must set ProductName = 'Far Horizon' (ticket AC)");
        }

        [Test]
        public void RenderPipeline_IsUniversal_NotBuiltIn()
        {
            var rp = GraphicsSettings.defaultRenderPipeline;
            Assert.IsNotNull(rp,
                "URP must be assigned to GraphicsSettings.defaultRenderPipeline (bare createProject " +
                "yields built-in RP — the bootstrap assigns URP, per the spike's FINDINGS)");
            Assert.That(rp.GetType().Name, Does.Contain("Universal"),
                "the assigned pipeline must be a Universal (URP) asset, not built-in");
        }

        [Test]
        public void BootScene_IsRegisteredInBuildSettings()
        {
            bool found = false;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && s.path == BootScenePath) { found = true; break; }
            Assert.IsTrue(found,
                "the Boot scene must be the enabled build scene (the exe ships exactly these bytes)");
        }

        [Test]
        public void BootScene_OpensAndHasCameraAndHud()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            Camera cam = null;
            BootHud hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cam == null) cam = root.GetComponentInChildren<Camera>();
                if (hud == null) hud = root.GetComponentInChildren<BootHud>();
            }
            Assert.IsNotNull(cam, "the Boot scene must contain a Camera");
            Assert.IsNotNull(hud, "the Boot scene must carry the BootHud (build-stamp surface)");
        }
    }
}
