using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode RUNTIME guard for the visual-polish wind wiring (tickets 86cabc73q trees + 86cabc737 grass),
    /// UPDATED for the #172 SOAK-NIT (2026-06-29): "moving grass/bushes looks weird; only the trees up in the
    /// air should move." The EditMode CanopySwayGrassWaveShaderTests prove the shader/mesh/material ship with the
    /// right params in the EDITOR; this is the RUNTIME complement (the editor-vs-runtime-divergence trap,
    /// unity-conventions.md §Editor-vs-runtime — a material can pass EditMode but ship different in the loaded
    /// scene): load the actual Boot scene, step real frames, and assert at runtime that ONLY the canopy carries
    /// a live sway while grass + bushes are STATIONARY. The visible MOTION is the Sponsor SOAK call (captured in
    /// the PR Self-Test Report); this guards the wiring against silent runtime breakage.
    /// </summary>
    public class CanopySwayGrassWavePlayModeTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";

        [UnityTest]
        public IEnumerator OnlyCanopySways_GrassAndBushesStationary_AtRuntime()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // step one real frame so the scene is live

            // CANOPY sway is live (KEPT): a canopy renders through the sway shader with a visible _SwayAmp.
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

            // GRASS is STATIONARY (#172 NIT): still rides the vertex-color shader (green + batching kept) but
            // _WaveAmp == 0 AND _SwayAmp == 0 — zero displacement at runtime.
            var grass = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Blades" && mf.sharedMesh != null
                             && mf.sharedMesh.name.StartsWith("LP_Grass"))
                .ToArray();
            Assert.Greater(grass.Length, 0, "the runtime Boot scene must carry meadow grass clumps");

            foreach (var mf in grass.Take(8))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name == ShaderName)
                {
                    Assert.AreEqual(0f, mat.GetFloat("_WaveAmp"), 1e-6f,
                        "at runtime grass must be STATIONARY — _WaveAmp == 0 (#172 soak-NIT)");
                    Assert.AreEqual(0f, mat.GetFloat("_SwayAmp"), 1e-6f,
                        "at runtime grass must be STATIONARY — _SwayAmp == 0");
                }
            }

            // BUSHES are STATIONARY (#172 NIT): body + berries ride the dedicated bush material with no
            // displacement (_SwayAmp == 0 AND _WaveAmp == 0) even though their verts carry the alpha-1 mask.
            var bushMeshes = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.sharedMesh != null
                             && (mf.sharedMesh.name.StartsWith("LP_BushBlob")
                                 || mf.sharedMesh.name.StartsWith("LP_BerryCluster")))
                .ToArray();
            Assert.Greater(bushMeshes.Length, 0, "the runtime Boot scene must carry bushes (body + berries)");

            foreach (var mf in bushMeshes.Take(12))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                Assert.IsNotNull(mat, $"runtime bush mesh '{mf.sharedMesh.name}' must carry a material");
                if (mat.HasProperty("_SwayAmp"))
                    Assert.AreEqual(0f, mat.GetFloat("_SwayAmp"), 1e-6f,
                        $"at runtime bush '{mf.sharedMesh.name}' must be STATIONARY — _SwayAmp == 0 (#172 soak-NIT)");
                if (mat.HasProperty("_WaveAmp"))
                    Assert.AreEqual(0f, mat.GetFloat("_WaveAmp"), 1e-6f,
                        $"at runtime bush '{mf.sharedMesh.name}' must be STATIONARY — _WaveAmp == 0");
            }
        }
    }
}
