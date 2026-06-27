using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// THE WADE REJECT-HINGE (ticket 86cadr95t — #130 NIT #1, flagged by Drew 4788883567 + Tess 4788937621 on
    /// the pond-depression rework). The pond is now a CARVED BOWL the player WADES INTO via the NavMeshAgent
    /// (WasdMovement drives agent.velocity), so the carved bowl FLOOR must bake WALKABLE and stay CONNECTED to
    /// the surrounding island NavMesh — otherwise the floor becomes an unreachable NavMesh island and the player
    /// cannot wade in (the live wade is the Sponsor-soak probe).
    ///
    /// THE COVERAGE GAP this closes: <see cref="RoundIslandNavCoveragePlayModeTests"/> samples a 12×16 polar grid
    /// whose innermost ring sits at ~9.5u from the WORLD ORIGIN, but the pond bowl is a ~3u-radius floor centred
    /// at world (7,−3) ≈ 7.6u from origin — so the pond footprint falls BETWEEN the origin and that first ring.
    /// NO existing PlayMode (or bake-time) sample lands inside the carved bowl floor → bowl-floor walkability was
    /// proven ONLY by the geometric wall-slope proxy (PondBowl_WallTraversableForWadeIn_DryLipIsShort), never by a
    /// DIRECT NavMesh sample. A future steeper-wall / deeper-recess / bake-restrict regression that ORPHANS the
    /// bowl floor would pass the geometry test (or only red at the Sponsor's soak); this test reds it at PR time.
    ///
    /// METHOD (mirrors the sibling coverage test + GroundPondInBowl's own grounding raycast):
    ///   - load the SHIPPED Boot scene; let OnEnable register the baked NavMesh + a few frames settle;
    ///   - find the serialized FreshwaterPond → its transform.position IS the grounded pond XZ (== PondCenterX/Z);
    ///   - raycast the real carved Ground_Play collider at each sample XZ to get the ACTUAL carved floor Y (the
    ///     same surface the NavMesh baked + the agent stands on — no recomputed height, no editor-only LowPolyZoneGen
    ///     dependency, which the PlayMode asmdef can't reference anyway);
    ///   - assert (1) every floor sample is NavMesh-WALKABLE (SamplePosition hits within a tight tolerance), and
    ///   - assert (2) the bowl floor is REACHABLE — a complete NavMesh path exists from a known-walkable island
    ///     anchor to the bowl-floor centre (PathComplete) — i.e. the floor is CONNECTED, not an orphaned island.
    /// </summary>
    public class PondBowlNavMeshPlayModeTests
    {
        // Mirrored from LowPolyZoneGen (the EDITOR asmdef, which the PlayMode asmdef cannot reference — same
        // reason RoundIslandNavCoveragePlayModeTests uses a literal IslandShoreR=120f). The flat bowl FLOOR band
        // extends to LowPolyZoneGen.PondBowlInnerRadius = 3.0u from the pond centre; sample WELL INSIDE it so the
        // samples land squarely on the carved floor (not on the submerged wall), robust to organic-rim jitter.
        private const float PondBowlInnerRadius = 3.0f; // == LowPolyZoneGen.PondBowlInnerRadius
        private const float FloorSampleRadius = PondBowlInnerRadius - 0.5f; // 2.5u — squarely on the flat floor

        // NavMesh.SamplePosition search radius. The bowl floor is carved ~1.05u below the plateau; the agent
        // stands ON the floor, so the baked NavMesh surface tracks the floor Y closely. A tight 1.0u proves the
        // sample hit the FLOOR's NavMesh (not the rim plateau a metre+ above), while absorbing the agent
        // base-offset + bake voxel snap.
        private const float SampleSnap = 1.0f;

        private static int GroundMask()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            return groundLayer >= 0 ? (1 << groundLayer) : ~0;
        }

        // Raycast the real carved terrain collider for the surface Y at (x,z); returns NaN if no terrain there.
        private static float CarvedFloorY(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out RaycastHit hit, 200f,
                    GroundMask(), QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return float.NaN;
        }

        private static IEnumerator LoadBootAndSettle()
        {
            // The standard PlayMode pattern (sibling coverage test): Boot is in the build scene list, so the real
            // serialized NavMeshSurface(s) + baked data register exactly as in the shipped build.
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate
            yield return null; // OnEnable registers the baked NavMesh data...
            yield return null;
            yield return null; // ...let a couple of frames settle
        }

        // === NIT #1 (1): the carved bowl FLOOR is NavMesh-WALKABLE on a direct SamplePosition inside the bowl ===
        [UnityTest]
        public IEnumerator BowlFloor_IsDirectlyNavMeshWalkable_InsideThePondBowl()
        {
            yield return LoadBootAndSettle();

            var pond = Object.FindAnyObjectByType<FreshwaterPond>(FindObjectsInactive.Include);
            Assert.IsNotNull(pond, "the shipped Boot scene must carry the FreshwaterPond (the bowl's pond XZ source)");
            Vector3 c = pond.transform.position; // grounded pond root XZ == PondCenterX/Z

            // Sample the centre + 8 points on a ring well inside the bowl floor band — every one must land on
            // the WALKABLE carved floor (the wade-in floor the agent stands on). The carved floor Y at each XZ is
            // taken from the real Ground_Play collider (the same raycast GroundPondInBowl uses to seat the pond).
            int total = 0, walkable = 0;
            float worstDeltaY = 0f; Vector3 worstAt = Vector3.zero;
            for (int i = 0; i < 9; i++)
            {
                float x, z;
                if (i == 0) { x = c.x; z = c.z; }                 // dead centre
                else
                {
                    float ang = (i - 1) / 8f * Mathf.PI * 2f;
                    x = c.x + Mathf.Cos(ang) * FloorSampleRadius;
                    z = c.z + Mathf.Sin(ang) * FloorSampleRadius;
                }

                float floorY = CarvedFloorY(x, z);
                Assert.IsFalse(float.IsNaN(floorY),
                    $"the carved Ground_Play collider must exist under the bowl floor sample ({x:F2},{z:F2}) — " +
                    "a missing hit means the terrain/bowl carve regressed or Ground_Play lost its collider");

                total++;
                var probe = new Vector3(x, floorY, z);
                if (NavMesh.SamplePosition(probe, out NavMeshHit nh, SampleSnap, NavMesh.AllAreas))
                {
                    walkable++;
                    float dY = Mathf.Abs(nh.position.y - floorY);
                    if (dY > worstDeltaY) { worstDeltaY = dY; worstAt = probe; }
                }
            }

            Debug.Log($"[world-trace] TEST pond-bowl floor NavMesh: {walkable}/{total} floor samples walkable " +
                      $"(pond centre {c.x:F1},{c.z:F1}; worst snap ΔY {worstDeltaY:F3}u at {worstAt})");

            Assert.AreEqual(total, walkable,
                $"EVERY carved bowl-floor sample inside PondBowlInnerRadius ({PondBowlInnerRadius}u, sampled at " +
                $"{FloorSampleRadius}u) must be NavMesh-WALKABLE — {walkable}/{total} hit within {SampleSnap}u. A " +
                "miss means the bowl floor did NOT bake walkable (a steeper-than-45° wall, a deeper recess, or a " +
                "bake restriction orphaned it) → the player can't WADE IN (the #130 wade reject-hinge; the " +
                "geometric wall-slope proxy alone never caught this — NIT 86cadr95t).");
        }

        // === NIT #1 (2): the bowl floor is REACHABLE — a complete path from the island connects to it ===========
        // Walkability alone is necessary-not-sufficient: the floor could be a walkable but DISCONNECTED island
        // (the orphan case the wall must prevent). Assert a COMPLETE NavMesh path from a known-walkable island
        // anchor to the bowl-floor centre — proving the wade-in route is connected end to end.
        [UnityTest]
        public IEnumerator BowlFloor_IsReachableFromTheIsland_NotAnOrphanedNavMeshIsland()
        {
            yield return LoadBootAndSettle();

            var pond = Object.FindAnyObjectByType<FreshwaterPond>(FindObjectsInactive.Include);
            Assert.IsNotNull(pond, "the shipped Boot scene must carry the FreshwaterPond");
            Vector3 c = pond.transform.position;

            // Snap the bowl-floor centre onto the NavMesh (the destination of the wade-in path).
            float floorY = CarvedFloorY(c.x, c.z);
            Assert.IsFalse(float.IsNaN(floorY), "the carved bowl floor must raycast under the pond centre");
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(c.x, floorY, c.z), out NavMeshHit floorHit,
                    SampleSnap, NavMesh.AllAreas),
                "the bowl-floor centre must be on the NavMesh (the wade-in destination) — covered by the walkable test");

            // A known-walkable ISLAND anchor well OUTSIDE the bowl mouth (the surrounding plateau the wade-in
            // starts from). The spawn region near the world origin is flat walkable land; snap it onto the mesh.
            float anchorY = CarvedFloorY(0f, 0f);
            Assert.IsFalse(float.IsNaN(anchorY), "the spawn-region terrain (0,0) must raycast (the island anchor)");
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, anchorY, 0f), out NavMeshHit anchorHit,
                    3f, NavMesh.AllAreas),
                "the island anchor near origin must be on the NavMesh (the surrounding walkable land)");

            var path = new NavMeshPath();
            bool found = NavMesh.CalculatePath(anchorHit.position, floorHit.position, NavMesh.AllAreas, path);

            Debug.Log($"[world-trace] TEST pond-bowl reachability: path status={path.status} corners={path.corners.Length} " +
                      $"from island anchor {anchorHit.position} to bowl floor {floorHit.position}");

            Assert.IsTrue(found && path.status == NavMeshPathStatus.PathComplete,
                $"a COMPLETE NavMesh path must connect the island ({anchorHit.position}) to the carved bowl floor " +
                $"({floorHit.position}) — status was {path.status}. A PARTIAL/INVALID path means the bowl floor is a " +
                "DISCONNECTED NavMesh island: the wall baked un-traversable and orphaned the floor, so the player " +
                "cannot wade in. This is the reachability half of the #130 wade reject-hinge the wall-slope proxy " +
                "could not assert (NIT 86cadr95t).");
        }
    }
}
