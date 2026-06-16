using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// BIG ROUND ISLAND N1 ("click-to-move only covers part of the island", ticket 86ca9a7qn). The
    /// END-TO-END coverage proof: load the SHIPPED Boot scene, let the NavMeshSurface(s) register their baked
    /// data (OnEnable), and sample NavMesh.SamplePosition across the whole walkable land disc (rings ×
    /// azimuths). The surface Y at each (x,z) is taken by RAYCASTING the real shipped terrain collider
    /// (Ground_Play) — so this proves coverage against the ACTUAL shipped geometry, not a recomputed height
    /// (and keeps this PlayMode test free of the editor-only LowPolyZoneGen height field). The agent must be
    /// able to reach the WHOLE island — so the walkable fraction must be HIGH (≥90%), not just the flat centre.
    ///
    /// This catches the BUG CLASS, not an instance: the OLD flat-slab-only / layer-restricted bake left the
    /// hills (beyond ±30u) with no walkable surface → coverage would crater to the centre disc and FAIL here.
    /// A future regression that re-restricts the bake, drops the island bake, or re-introduces the competing
    /// disconnected slab disc fails the same assertion. (Trees carve NavMeshObstacles at RUNTIME — they don't
    /// subtract from the static bake — so the static coverage IS the reachable land.)
    /// </summary>
    public class RoundIslandNavCoveragePlayModeTests
    {
        // IslandShoreR is 120u (LowPolyZoneGen) — the walkable land disc is just inside the sand ring.
        private const float IslandShoreR = 120f;
        private const float PlantR = IslandShoreR - 6f;

        [UnityTest]
        public IEnumerator ShippedNavMesh_CoversTheWholeIsland_NotJustTheCentre()
        {
            // Load the SHIPPED Boot scene (the standard PlayMode pattern — Boot is in the build scene list)
            // so the real serialized NavMeshSurface(s) + baked data register exactly as in the build.
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // let the scene activate
            // Let OnEnable register the baked NavMesh data + a couple of frames settle.
            yield return null;
            yield return null;
            yield return null;

            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundMask = groundLayer >= 0 ? (1 << groundLayer) : ~0;

            const int rings = 12, azimuths = 16;
            int total = 0, covered = 0, noTerrain = 0;
            float worstRing = -1f; int worstMiss = 0;
            for (int ri = 1; ri <= rings; ri++)
            {
                float rr = PlantR * ri / rings;
                int miss = 0;
                for (int ai = 0; ai < azimuths; ai++)
                {
                    float ang = ai / (float)azimuths * Mathf.PI * 2f;
                    float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;

                    // Real surface Y from the shipped terrain collider (raycast down from high above).
                    float y;
                    if (Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out RaycastHit ghit, 200f,
                            groundMask, QueryTriggerInteraction.Ignore))
                        y = ghit.point.y;
                    else { noTerrain++; continue; } // no terrain here at all — not a navmesh-coverage miss

                    total++;
                    if (NavMesh.SamplePosition(new Vector3(x, y, z), out _, 3f, NavMesh.AllAreas))
                        covered++;
                    else
                        miss++;
                }
                if (miss > worstMiss) { worstMiss = miss; worstRing = rr; }
            }

            float pct = total > 0 ? 100f * covered / total : 0f;
            Debug.Log($"[world-trace] TEST NavMesh coverage: {covered}/{total} ({pct:F1}%) walkable across the " +
                      $"island (worst ring r={worstRing:F0}u missed {worstMiss}/{azimuths}; {noTerrain} no-terrain pts)");

            Assert.Greater(total, rings * azimuths / 2,
                "the terrain raycast must hit the island over most of the land disc (else the terrain collider " +
                "regressed) — only " + total + " land points found");
            Assert.GreaterOrEqual(pct, 90f,
                $"the shipped NavMesh must cover the WHOLE island (≥90% of the land disc walkable) — got " +
                $"{pct:F1}% ({covered}/{total}). The OLD flat-slab-only bake left the hills unreachable (N1 " +
                $"'can't walk everywhere'); a partial number means the agent can't reach the whole island.");
            Assert.LessOrEqual(worstMiss, azimuths / 4,
                $"no island ring may be a dead band — worst ring r={worstRing:F0}u missed {worstMiss}/{azimuths} " +
                "(a whole-ring miss is the flat-slab-only or fragmented-bake regression)");
        }
    }
}
