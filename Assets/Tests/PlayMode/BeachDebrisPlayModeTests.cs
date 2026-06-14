using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode runtime-presence guard for the M-U3-SCENE-4 (86ca8feuf) washed-ashore debris.
    ///
    /// The EditMode BeachDebrisSceneTests prove the debris is serialized into Boot.unity in the editor.
    /// This is the RUNTIME complement (the editor-vs-runtime-divergence trap, unity-conventions.md — a
    /// hierarchy can pass EditMode checks but ship MANGLED in the loaded scene, the "legs-up" class):
    /// load the actual Boot scene at runtime, step a frame, and assert the debris is THERE, RENDERS, and
    /// — load-bearing — carries NO colliders so it can't block click-move pathing / the ground raycast.
    /// A debris build that survives the editor but drops at runtime fails here, not in a Sponsor soak.
    /// </summary>
    public class BeachDebrisPlayModeTests
    {
        private static GameObject FindDebrisRoot()
        {
            // The debris root may be at scene root or nested; find it by name across the loaded scene.
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go.name == "BeachDebris") return go.gameObject;
            return null;
        }

        [UnityTest]
        public IEnumerator BootScene_CarriesBeachDebris_AtRuntime_RendersAndIsNonBlocking()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // step one real frame

            var debris = FindDebrisRoot();
            Assert.IsNotNull(debris,
                "the loaded Boot scene must carry the BeachDebris root at runtime (the washed-ashore " +
                "scatter must survive serialization into the shipped scene — the legs-up trap)");

            var renderers = debris.GetComponentsInChildren<MeshRenderer>(true);
            Assert.Greater(renderers.Length, 2,
                "the debris must have its serialized mesh pieces live at runtime (planks + crate + barrel)");
            foreach (var mr in renderers)
            {
                Assert.IsTrue(mr.enabled, $"{mr.name} renderer must be enabled (the piece must actually show)");
                Assert.IsNotNull(mr.GetComponent<MeshFilter>().sharedMesh,
                    $"{mr.name} must have a live mesh at runtime");
            }

            // THE non-blocking contract at RUNTIME: no debris piece may carry a Collider — it must not
            // block the click-move ground raycast or the NavMesh near the spawn (AC2).
            var colliders = debris.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(0, colliders.Length,
                "NO debris piece may have a Collider at runtime — it is set-dressing, not an obstacle " +
                "(AC2: must not block pathing / the ground raycast). Found: " +
                string.Join(", ", colliders.Select(c => c.gameObject.name)));
        }
    }
}
