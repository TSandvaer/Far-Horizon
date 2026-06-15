using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode runtime guard for the world-look polish (ticket 86ca8t9pq — Uma world-look brief): the
    /// EditMode WorldLookSceneTests prove the clouds / vista / sky ship in Boot.unity with the right
    /// shape + config in the EDITOR. This is the RUNTIME complement (the editor-vs-runtime-divergence
    /// trap, unity-conventions.md — a hierarchy can pass EditMode but ship MANGLED / inert in the loaded
    /// scene, the "legs-up" class): load the actual Boot scene, step real frames, and assert the clouds
    /// ACTUALLY DRIFT, the vista renders, and the sky reads at runtime — not silently dropped or frozen.
    /// </summary>
    public class WorldLookPlayModeTests
    {
        [UnityTest]
        public IEnumerator Clouds_ActuallyDrift_AtRuntime_NotFrozen()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // step one real frame so Start runs

            var clouds = Object.FindObjectsByType<FarHorizon.CloudDrift>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(clouds.Length, 5,
                $"the loaded Boot scene must carry 5-9 clouds at runtime (found {clouds.Length})");

            // Snapshot a cloud's along-wind position, let real time pass, confirm it MOVED (the drift is
            // alive, not a frozen serialized prop — the headline runtime contract for Uma §1).
            var probe = clouds[0];
            Vector3 before = probe.transform.position;
            // Step a real-time window. PlayMode time advances; sample over Time.time, not per-frame delta
            // (the headless deltaTime~0 trap — unity-conventions.md).
            float t0 = Time.time;
            while (Time.time - t0 < 1.0f) yield return null;
            Vector3 after = probe.transform.position;

            float moved = (after - before).magnitude;
            Assert.Greater(moved, 0.05f,
                $"a cloud must DRIFT at runtime (moved {moved:F3}u in ~1s) — a frozen cloud means the " +
                "CloudDrift translate isn't running (the editor-vs-runtime inert-component class)");
        }

        [UnityTest]
        public IEnumerator Vista_MountainRanges_RenderAtRuntime()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var peaks = Object.FindObjectsByType<MeshRenderer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.gameObject.name == "LP_Mountain")
                .ToArray();
            Assert.Greater(peaks.Length, 6,
                $"the loaded Boot scene must carry the vista island clusters at runtime (found {peaks.Length})");
            foreach (var r in peaks.Take(8))
            {
                Assert.IsTrue(r.enabled, "a vista peak renderer must be enabled (the silhouette must show)");
                var mf = r.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf?.sharedMesh, "a vista peak must keep its mesh at runtime");
                Assert.Greater(mf.sharedMesh.vertexCount, 0, "a vista peak mesh must carry geometry at runtime");
                Assert.IsNotNull(r.sharedMaterial, "a vista peak must keep its (vertex-colour) material at runtime");
                // NOTE: do NOT read mesh.colors here — the vista peaks are marked BatchingStatic (Erik
                // perf note: static-batched into 1-2 draw calls), so at runtime they share a non-readable
                // Combined Mesh and .colors throws "isReadable is false". The grey-to-snow vertex colours
                // are verified pre-batch by the EditMode WorldLookMeshTests/SceneTests; the runtime probe's
                // job is the editor-vs-runtime divergence check (renders + keeps its material), not re-
                // reading the (now-combined) colour buffer.
            }
        }

        [UnityTest]
        public IEnumerator Sky_GradientAndFog_PresentAtRuntime_SeamKillHolds()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(RenderSettings.skybox, "the gradient skybox must be assigned at runtime");
            Assert.IsTrue(RenderSettings.fog, "distance fog must be on at runtime (the atmospheric fade)");
            // The seam-kill must hold at runtime: fog colour == the horizon sky stop.
            Color fog = RenderSettings.fogColor;
            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            Assert.AreEqual(horizon.r, fog.r, 0.02f, "runtime fog R must == the horizon stop (seam-kill)");
            Assert.AreEqual(horizon.g, fog.g, 0.02f, "runtime fog G must == the horizon stop (seam-kill)");
            Assert.AreEqual(horizon.b, fog.b, 0.02f, "runtime fog B must == the horizon stop (seam-kill)");
        }
    }
}
