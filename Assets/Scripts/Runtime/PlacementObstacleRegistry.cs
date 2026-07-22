using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The forward-looking NO-BUILD registration seam for placement (ticket 86catqxm0 — ① soak follow-up:
    /// "the ghost table should be red when colliding with other objects").
    ///
    /// === WHY THIS EXISTS (diagnose-via-trace, 86catqxm0) ===
    /// The ticket's first hypothesis was a <c>Physics.OverlapBox</c> against colliders. A trace proved the Far
    /// Horizon world is DELIBERATELY COLLIDER-FREE: trees carry only a <c>NavMeshObstacle</c> (no Collider),
    /// rocks / ore-nodes / bushes / placed structures carry nothing, and chop/mine/loot all resolve by PLANAR
    /// DISTANCE (ChopTree/MineOre). The only colliders in the scene are the Ground MeshColliders. So a physics
    /// overlap detects NOTHING over any prop, and adding prop colliders would pollute the NavMesh bake + break
    /// the deliberate "walk up to / through props" gameplay. Obstruction is therefore detected by
    ///   (1) NavMesh-walkability under the footprint (catches TREES — they carve the navmesh), and
    ///   (2) a planar-distance test against the discrete interactable instances registered here.
    ///
    /// === THE SEAM (② boulder-mining 86camz9v7 adopts this) ===
    /// Any placeable / mineable that should block a build footprint registers a CIRCULAR no-build zone — either
    /// by adding a <see cref="PlacementObstacle"/> component (self-registers on enable) or by calling
    /// <see cref="Register"/> directly. NO collider required. Today's pools (active ore nodes + a lit campfire /
    /// built forge) are discovered by <see cref="CraftingTablePlacement"/> itself; NEW obstacle sources (②'s
    /// minable boulders) should adopt THIS seam so they read RED with no further placement edit.
    /// </summary>
    public static class PlacementObstacleRegistry
    {
        private struct Entry { public Transform t; public float radius; }

        // Mutable runtime state persisted across editor play-entries (domain reload off, unity-conventions.md
        // §Configurable-Enter-Play-Mode) — reset below. The FIELD is static readonly (an immutable reference),
        // so it is EXCLUDED from StaticStateResetTests' mutable-static scan, but the CONTENTS are cleared each
        // play-entry for correctness.
        private static readonly List<Entry> _obstacles = new List<Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _obstacles.Clear();

        /// <summary>Register a circular no-build zone at <paramref name="t"/> with planar <paramref name="radius"/>.
        /// Idempotent per transform (a re-register updates the radius). A null transform is ignored.</summary>
        public static void Register(Transform t, float radius)
        {
            if (t == null) return;
            for (int i = 0; i < _obstacles.Count; i++)
                if (_obstacles[i].t == t) { _obstacles[i] = new Entry { t = t, radius = radius }; return; }
            _obstacles.Add(new Entry { t = t, radius = radius });
        }

        /// <summary>Remove a previously-registered no-build zone (safe if it was never registered).</summary>
        public static void Unregister(Transform t)
        {
            for (int i = _obstacles.Count - 1; i >= 0; i--)
                if (_obstacles[i].t == null || _obstacles[i].t == t) _obstacles.RemoveAt(i);
        }

        /// <summary>Count of registered no-build zones (test / inspection seam).</summary>
        public static int Count => _obstacles.Count;

        /// <summary>
        /// True iff a footprint circle (planar <paramref name="footprintRadius"/> at <paramref name="center"/>)
        /// overlaps ANY registered no-build zone, skipping any zone whose transform is a child of
        /// <paramref name="ignoreRoot"/> (the ghost / table never blocks itself). Prunes destroyed entries as it
        /// scans. Allocation-free (safe to call per frame during placement).
        /// </summary>
        public static bool IsFootprintBlocked(Vector3 center, float footprintRadius, Transform ignoreRoot = null)
        {
            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                Entry e = _obstacles[i];
                if (e.t == null) { _obstacles.RemoveAt(i); continue; }
                if (ignoreRoot != null && e.t.IsChildOf(ignoreRoot)) continue;
                Vector3 p = e.t.position;
                if (CircleOverlaps(center.x, center.z, footprintRadius, p.x, p.z, e.radius)) return true;
            }
            return false;
        }

        /// <summary>
        /// PURE planar circle-overlap truth-table (unit-testable without a scene): two circles overlap iff their
        /// centre planar (XZ) distance is strictly less than the sum of their radii. Squared-distance form (no
        /// sqrt) — the domain seam the EditMode guard pins.
        /// </summary>
        public static bool CircleOverlaps(float ax, float az, float aRadius, float bx, float bz, float bRadius)
        {
            float dx = ax - bx, dz = az - bz;
            float sum = aRadius + bRadius;
            return dx * dx + dz * dz < sum * sum;
        }

        /// <summary>A discovered no-build source for a placement session (a transform + its planar footprint
        /// radius). The shared shape both placement drivers hold their session pool as.</summary>
        public struct SessionObstacle { public Transform t; public float radius; }

        // Planar no-build radii for the discrete collider-free pools discovered each placement session.
        private const float OreNodeObstacleRadius = 0.6f;   // an ore node's rough footprint
        private const float StructureObstacleRadius = 0.9f; // a lit campfire (a placed table/forge self-registers)

        /// <summary>
        /// Collect the NON-self-registering session obstacles into <paramref name="into"/> (③ reconciliation —
        /// the shared discovery both <see cref="CraftingTablePlacement"/> and <see cref="ForgePlacement"/> call
        /// at EnterPlacement, NOT per frame). Clears + fills the list with: active ore nodes (the mine pool) +
        /// LIT campfires. Placed TABLES + FORGES are NOT scanned here — they self-register via
        /// <see cref="PlacementObstacle"/> (their Reveal/Build enables it) and are caught by
        /// <see cref="IsFootprintBlocked"/>, so scanning them here would double-count. Allocation only grows the
        /// caller's reused list (called once per placement session, not per frame).
        /// </summary>
        public static void CollectSessionObstacles(List<SessionObstacle> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var mine in Object.FindObjectsByType<MineOre>(FindObjectsSortMode.None))
            {
                if (mine == null || mine.nodeRoot == null) continue;
                foreach (Transform node in mine.nodeRoot)
                    if (node != null && node.name == MineOre.OreNodeName && node.gameObject.activeInHierarchy)
                        into.Add(new SessionObstacle { t = node, radius = OreNodeObstacleRadius });
            }
            foreach (var fire in Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None))
                if (fire != null && fire.IsLit)
                    into.Add(new SessionObstacle { t = fire.transform, radius = StructureObstacleRadius });
        }
    }
}
