using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode smoke guard. Loads the Boot scene, steps a real frame, and asserts the scene
    /// actually RUNS: the camera is live, the BootHud + BootScreenshot components instantiate,
    /// and BuildInfo.Stamp resolves to a non-empty value (proving Resources/BuildStamp.txt
    /// shipped + the self-identifying-screenshot ritual works). This is the runtime complement
    /// to the EditMode bootstrap-integrity guards — a scene that opens in the editor but throws
    /// at runtime fails here, not in a Sponsor soak.
    /// </summary>
    public class BootPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootScene_Loads_AndRunsAFrame()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // let the scene activate
            yield return null; // step one real frame

            var cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "the Boot scene must have a live Camera at runtime");

            var hud = Object.FindFirstObjectByType<BootHud>();
            Assert.IsNotNull(hud, "the BootHud must be live at runtime");

            var shot = Object.FindFirstObjectByType<BootScreenshot>();
            Assert.IsNotNull(shot, "the BootScreenshot capture hook must be live at runtime");
        }

        [Test]
        public void BuildInfo_Stamp_IsNonEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(BuildInfo.Stamp),
                "BuildInfo.Stamp must resolve (Resources/BuildStamp.txt shipped) — the self-" +
                "identifying-screenshot ritual depends on it");
        }
    }
}
