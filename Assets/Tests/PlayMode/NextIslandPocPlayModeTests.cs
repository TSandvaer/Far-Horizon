using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// NEXT-ISLAND POC walkable-mountain PlayMode proof (ticket 86caa9zpp — AC3: the hero peak is a real
    /// CLIMBABLE giant hill, not a wall). CI-safe + SELF-CONTAINED: it does NOT load the POC scene (the POC is
    /// a stand-alone scene NOT in the CI Boot build-scene list — SceneManager.LoadScene would fail in CI), and
    /// it does NOT reference the editor-only NextIslandPocGen. Instead it builds a SMALL terrain at runtime
    /// using the SAME raised-cosine mountain dome the gen uses (mirrored here as a pure local function — the
    /// calibration EditMode test pins the runtime mirror to the gen), bakes a runtime NavMesh over it via
    /// NavMeshSurface, and asserts an agent can PATH from the foot to the summit (a complete, non-partial path).
    ///
    /// This catches the BUG CLASS, not an instance: if a future change makes the mountain a VERTICAL WALL
    /// (slope > the agent max), the NavMesh would not cover the flank → the foot→summit path would be partial
    /// or fail → this test reds. (The full-size shipped-exe walkability is the -verifyPocIsland NavMesh-coverage
    /// trace; this is the deterministic CI gate for the climbable-mountain contract.)
    /// </summary>
    public class NextIslandPocPlayModeTests
    {
        // Scaled-down mirror of the gen's mountain (same DOME shape, smaller so the runtime bake is fast). The
        // proportion peak/foot is kept ~equal to the gen (135/300 = 0.45) so the SLOPE — the load-bearing
        // property — matches: if the gen's real mountain is walkable, this scaled one is too, and vice-versa.
        const float PeakH = 27f;    // 135/5
        const float FootR = 60f;    // 300/5

        static float MountainHeightAt(float x, float z, float cx, float cz)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
            if (d >= FootR) return 0f;
            float t = d / FootR;
            float dome = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI);
            return PeakH * Mathf.Pow(dome, 1.25f);
        }

        [UnityTest]
        public IEnumerator HeroMountain_IsClimbable_AgentPathsFromFootToSummit()
        {
            // Build a runtime terrain mesh: a flat base + the mountain dome centred at the middle.
            const int seg = 60;
            const float half = 60f;
            float cx = 0f, cz = 0f;
            var verts = new Vector3[(seg + 1) * (seg + 1)];
            for (int z = 0; z <= seg; z++)
            for (int x = 0; x <= seg; x++)
            {
                float wx = (x / (float)seg - 0.5f) * half * 2f;
                float wz = (z / (float)seg - 0.5f) * half * 2f;
                verts[z * (seg + 1) + x] = new Vector3(wx, MountainHeightAt(wx, wz, cx, cz), wz);
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int z = 0; z < seg; z++)
            for (int x = 0; x < seg; x++)
            {
                int i = z * (seg + 1) + x;
                tris.Add(i); tris.Add(i + seg + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + seg + 1); tris.Add(i + seg + 2);
            }
            var mesh = new Mesh { name = "PocTestTerrain" };
            mesh.vertices = verts;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var terrain = new GameObject("PocTestTerrain");
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>();
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Bake a runtime NavMesh over the terrain (default agent — max slope 45°).
            var surfGo = new GameObject("PocTestNavSurface");
            var surface = surfGo.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();
            yield return null;

            // Foot point (near the mountain foot) and summit point.
            Vector3 foot = new Vector3(cx + FootR * 0.95f, 0f, cz);
            foot.y = MountainHeightAt(foot.x, foot.z, cx, cz);
            Vector3 summit = new Vector3(cx, MountainHeightAt(cx, cz, cx, cz), cz);

            Assert.IsTrue(NavMesh.SamplePosition(foot, out NavMeshHit footHit, 5f, NavMesh.AllAreas),
                "the mountain FOOT must be on the walkable NavMesh (the flank bakes → the foot is reachable).");
            Assert.IsTrue(NavMesh.SamplePosition(summit, out NavMeshHit summitHit, 5f, NavMesh.AllAreas),
                "the mountain SUMMIT must be on the walkable NavMesh — a WALL would leave the summit un-baked (AC3).");

            // The agent must be able to PATH foot→summit COMPLETELY (not a partial path that stops at a wall).
            var path = new NavMeshPath();
            bool has = NavMesh.CalculatePath(footHit.position, summitHit.position, NavMesh.AllAreas, path);
            Debug.Log($"[poc-trace] TEST climb path: has={has} status={path.status} corners={path.corners.Length} " +
                      $"foot={footHit.position.ToString("F1")} summit={summitHit.position.ToString("F1")}");
            Assert.IsTrue(has, "CalculatePath foot→summit must succeed (the flank is a connected walkable surface).");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                "the foot→summit path must be COMPLETE — a partial path means the mountain is a WALL the agent " +
                "cannot climb (AC3: the hero peak must be a climbable giant hill, not a backdrop).");
            // And the path must actually GAIN height (it climbs to the summit, not skirt around a wall base).
            float topCorner = 0f;
            foreach (var cxz in path.corners) if (cxz.y > topCorner) topCorner = cxz.y;
            Assert.Greater(topCorner, PeakH * 0.7f,
                $"the climb path must REACH near the summit (top corner y={topCorner:F1} of peak {PeakH:F0}) — " +
                "proving the agent climbs the hill, not walks around its foot.");

            Object.Destroy(terrain);
            Object.Destroy(surfGo);
            yield return null;
        }
    }
}
