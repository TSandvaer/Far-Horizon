using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode RUNTIME guard for the visual-polish wave wiring (tickets 86cabc73q trees + 86cabc737 grass).
    /// The EditMode CanopySwayGrassWaveShaderTests prove the shader/mesh/material ship with the right params in
    /// the EDITOR; this is the RUNTIME complement (the editor-vs-runtime-divergence trap, unity-conventions.md
    /// §Editor-vs-runtime — a material can pass EditMode but ship inert in the loaded scene): load the actual
    /// Boot scene, step real frames, and assert the canopy + grass materials carry the live wind params at
    /// runtime. The visible sway/wind MOTION is the Sponsor SOAK call (captured in the PR Self-Test Report);
    /// this guards the wiring against silent runtime breakage.
    /// </summary>
    public class CanopySwayGrassWavePlayModeTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";

        [UnityTest]
        public IEnumerator CanopyAndGrass_CarryLiveWindParams_AtRuntime()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // step one real frame so the scene is live

            // CANOPY sway is live: a canopy renders through the sway shader with a visible _SwayAmp.
            var canopies = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Canopy" && mf.sharedMesh != null)
                .ToArray();
            Assert.Greater(canopies.Length, 0, "the runtime Boot scene must carry tree canopies");

            bool anyCanopySway = false;
            foreach (var mf in canopies.Take(8))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name == ShaderName
                    && mat.GetFloat("_SwayAmp") > 0f)
                    anyCanopySway = true;
            }
            Assert.IsTrue(anyCanopySway,
                "at runtime a canopy must render through the sway shader with _SwayAmp > 0 (the canopy wind is live)");

            // GRASS wind is live: a meadow clump renders through the vertex-color shader with a visible _WaveAmp.
            var grass = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Blades" && mf.sharedMesh != null
                             && mf.sharedMesh.name.StartsWith("LP_Grass"))
                .ToArray();
            Assert.Greater(grass.Length, 0, "the runtime Boot scene must carry meadow grass clumps");

            bool anyGrassWave = false;
            foreach (var mf in grass.Take(8))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name == ShaderName
                    && mat.GetFloat("_WaveAmp") > 0f)
                    anyGrassWave = true;
            }
            Assert.IsTrue(anyGrassWave,
                "at runtime a meadow grass clump must render through the vertex-color shader with _WaveAmp > 0 (the grass wind is live)");
        }
    }
}
