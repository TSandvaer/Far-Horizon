using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode runtime-presence guard for the scatter ROCKS (ticket 86ca8m5zu v2 — the flat-shaded faceted
    /// stone redo + density restore).
    ///
    /// The EditMode RockBoulderSceneTests prove the rocks are serialized into Boot.unity in the editor with
    /// the right SHAPE (flat-shaded, value-contrast), density and placement. This is the RUNTIME complement
    /// (the editor-vs-runtime-divergence trap, unity-conventions.md — a hierarchy can pass EditMode but ship
    /// MANGLED in the loaded scene, the "legs-up" class): load the actual Boot scene at runtime, step a
    /// frame, and assert the rocks are THERE, RENDER, keep their flat-shaded geometry + vertex colours, and
    /// are present in the "decent amount" (~22) the Sponsor wants — not silently dropped or thinned at load.
    /// </summary>
    public class RockScatterPlayModeTests
    {
        private static MeshFilter[] FindRuntimeRocks()
        {
            return Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "RockMesh" && mf.sharedMesh != null)
                .ToArray();
        }

        [UnityTest]
        public IEnumerator BootScene_CarriesFacetedStoneRocks_AtRuntime_RendersDecentAmount()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // step one real frame

            var rocks = FindRuntimeRocks();
            // BIG ROUND ISLAND (86ca9a7qn): the island is ~50x the old strip area, so the rock count scales
            // to ~60 clustered outcrops on the hills (still sparse per area). Floor 30 (the old ~22 strip
            // count reads too-empty on the big island), ceiling 90 (a uniform all-over speckle still fails).
            Assert.GreaterOrEqual(rocks.Length, 30,
                $"the loaded Boot scene must carry a DECENT AMOUNT of rocks at runtime (found {rocks.Length}) — " +
                "the scatter must survive serialization AND keep the big-island density (~60 outcrops)");
            Assert.LessOrEqual(rocks.Length, 90,
                $"rocks must not be a uniform 'all over' speckle at runtime (found {rocks.Length})");

            foreach (var mf in rocks)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, $"{mf.name} must have a MeshRenderer at runtime");
                Assert.IsTrue(mr.enabled, $"{mf.name} renderer must be enabled (the rock must actually show)");

                var m = mf.sharedMesh;
                int tris = m.triangles.Length / 3;
                // The flat-shaded stone geometry must survive into the runtime scene (verts == tris*3) — a
                // re-smoothed / welded mesh at runtime would be the rejected mound.
                Assert.AreEqual(tris * 3, m.vertexCount,
                    $"runtime rock '{mf.name}' must keep FLAT-SHADED geometry (verts == tris*3); got " +
                    $"{m.vertexCount}/{tris} — a smooth mound is the rejected regression");
                Assert.AreEqual(m.vertexCount, m.colors.Length,
                    $"runtime rock '{mf.name}' must keep its per-facet vertex COLOURS (the stone value contrast)");
            }
        }
    }
}
