using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode runtime guard for the WATER fix (ticket 86ca9yn57 — "water reads SAME as sky" + static).
    /// The EditMode WaterSceneTests / WaterFacesUpTests prove the sea ships with the right winding + colour +
    /// wave params in the EDITOR. This is the RUNTIME complement (the editor-vs-runtime-divergence trap,
    /// unity-conventions.md — a mesh/material can pass EditMode but ship inert/mangled in the loaded scene):
    /// load the actual Boot scene, step real frames, and assert the water renders with the moving-swell
    /// material + capped fog at runtime (the AC1/AC2 contract holds in the player, not just the editor).
    /// </summary>
    public class WaterWavesPlayModeTests
    {
        [UnityTest]
        public IEnumerator Water_RendersWithMovingSwellAndFogCap_AtRuntime()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // step one real frame so the scene is live

            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the loaded Boot scene must carry the ocean (Water_Play) at runtime");

            var mr = water.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr, "the ocean must keep its MeshRenderer at runtime");
            Assert.IsTrue(mr.enabled, "the ocean renderer must be ENABLED at runtime (a disabled sea is invisible)");

            var mat = mr.sharedMaterial;
            Assert.IsNotNull(mat, "the ocean must keep its material at runtime");
            Assert.AreEqual("FarHorizon/LowPolyVertexColor", mat.shader.name,
                "the ocean must render through FarHorizon/LowPolyVertexColor at runtime (the swell + fog-cap shader)");

            // AC2: the moving swell is live (a visible amplitude + a non-zero speed so it travels — the
            // displacement is keyed off _Time.y in the vertex shader).
            Assert.Greater(mat.GetFloat("_WaveAmp"), 0.2f,
                "the runtime water material must carry a VISIBLE swell amplitude (_WaveAmp > 0.2) — a frozen/" +
                "tiny swell reads as static water (the Sponsor's complaint)");
            Assert.Greater(mat.GetFloat("_WaveSpeed"), 0f,
                "the runtime water material must carry a non-zero _WaveSpeed so the swell ADVANCES over time");

            // AC1: the fog cap is live so the far sea keeps its teal at the horizon (distinct from the sky).
            Assert.Greater(mat.GetFloat("_FogCap"), 0.2f,
                "the runtime water material must cap the fog (_FogCap > 0.2) so the far sea keeps its teal at " +
                "the horizon — without it the global fog washes the far sea to the sky stop");

            // The mesh must carry geometry (the editor-vs-runtime inert/mangled-mesh check).
            var mf = water.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf?.sharedMesh, "the ocean must keep its mesh at runtime");
            Assert.Greater(mf.sharedMesh.vertexCount, 200, "the ocean mesh must carry its subdivided geometry at runtime");
        }
    }
}
