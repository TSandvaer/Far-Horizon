using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the pure placement-validity + free-cursor helpers (ticket 86camz9uz / crafting-redesign
    /// ① + soak fix round F1–F4). No scene needed — every decision is a static:
    ///   • F4 — the ghost's valid/invalid read INCLUDES affordability (insufficient materials → invalid/red);
    ///   • F3 — the scroll-wheel ghost rotation is one predictable step per notch, wraps [0,360);
    ///   • F1 — the no-ground-hit fallback ray/plane intersection.
    /// </summary>
    public class CraftingTablePlacementTests
    {
        private const float MinDist = 1.0f;
        private const float MinNormalY = 0.85f;

        // ---- F4: ground validity (spatial) ----

        [Test]
        public void Ground_Valid_OnFlatGround_FarEnough()
        {
            Assert.IsTrue(CraftingTablePlacement.IsGroundValid(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY),
                "flat ground, ghost 2.5u out → ground valid");
        }

        [Test]
        public void Ground_Invalid_NoGround()
        {
            Assert.IsFalse(CraftingTablePlacement.IsGroundValid(
                groundFound: false, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY),
                "no ground under the cursor (over water / off the island edge / at the sky) → BLOCKED");
        }

        [Test]
        public void Ground_Invalid_TooSteep()
        {
            Assert.IsFalse(CraftingTablePlacement.IsGroundValid(
                groundFound: true, normalY: 0.50f, distFromPlayer: 2.5f, MinDist, MinNormalY),
                "a steep slope (normalY 0.5 < 0.85) → BLOCKED (a table can't stand on a cliff)");
        }

        [Test]
        public void Ground_Invalid_TooCloseToPlayer()
        {
            Assert.IsFalse(CraftingTablePlacement.IsGroundValid(
                groundFound: true, normalY: 1.0f, distFromPlayer: 0.5f, MinDist, MinNormalY),
                "ghost right on top of the player (0.5 < 1.0) → BLOCKED (never build on self)");
        }

        // ---- F4: full validity = ground AND affordability ----

        [Test]
        public void Full_Valid_WhenGroundGood_AndAffordable()
        {
            Assert.IsTrue(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY, canAfford: true),
                "good ground + can afford → valid (green, READY)");
        }

        [Test]
        public void Full_Invalid_WhenGroundGood_ButCannotAfford()
        {
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY, canAfford: false),
                "F4: good ground but insufficient materials → INVALID (red, 'need materials')");
        }

        [Test]
        public void Full_Invalid_WhenAffordable_ButGroundBad()
        {
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: false, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY, canAfford: true),
                "can afford but no ground → still INVALID (red, 'move to flat open ground')");
        }

        // ---- F3: scroll-wheel ghost rotation ----

        [Test]
        public void Rotation_ScrollUp_AddsOneStep()
        {
            Assert.AreEqual(60f, CraftingTablePlacement.ApplyRotation(45f, +0.1f, 15f), 1e-3f,
                "a positive scroll notch rotates the ghost yaw by +step");
        }

        [Test]
        public void Rotation_ScrollDown_SubtractsOneStep_Wraps()
        {
            Assert.AreEqual(350f, CraftingTablePlacement.ApplyRotation(5f, -0.1f, 15f), 1e-3f,
                "a negative notch below 0 wraps into [0,360)");
        }

        [Test]
        public void Rotation_ZeroScroll_NoChange()
        {
            Assert.AreEqual(200f, CraftingTablePlacement.ApplyRotation(200f, 0f, 15f), 1e-3f,
                "no scroll → no rotation");
        }

        [Test]
        public void Rotation_SignBased_MagnitudeIndependent()
        {
            // A big platform scroll magnitude still rotates exactly ONE step (predictable per notch).
            Assert.AreEqual(30f, CraftingTablePlacement.ApplyRotation(15f, +5f, 15f), 1e-3f,
                "sign-based: any positive magnitude = one step");
        }

        // ---- F1: no-ground-hit ray/plane fallback ----

        [Test]
        public void PlaneIntersect_RayDown_HitsPlane()
        {
            var ray = new Ray(new Vector3(3f, 10f, 4f), Vector3.down);
            Assert.IsTrue(CraftingTablePlacement.PlaneIntersect(ray, 0f, out Vector3 hit),
                "a downward ray hits the y=0 plane");
            Assert.AreEqual(3f, hit.x, 1e-3f);
            Assert.AreEqual(0f, hit.y, 1e-3f);
            Assert.AreEqual(4f, hit.z, 1e-3f);
        }

        [Test]
        public void PlaneIntersect_RayParallel_NoHit()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.forward);
            Assert.IsFalse(CraftingTablePlacement.PlaneIntersect(ray, 0f, out _),
                "a ray parallel to the plane never intersects it");
        }

        [Test]
        public void PlaneIntersect_RayAway_NoHit()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.up);
            Assert.IsFalse(CraftingTablePlacement.PlaneIntersect(ray, 0f, out _),
                "a ray pointing away from the plane (upward, plane below) never intersects it");
        }

        // ---- 86catqxm0: object-overlap validity (the ghost reads RED over an object) ----

        [Test]
        public void Full_Invalid_WhenObstructed_EvenOnGoodGround_AndAffordable()
        {
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY,
                canAfford: true, obstructed: true),
                "86catqxm0: good flat ground + affordable BUT the footprint overlaps an object → INVALID " +
                "(red, 'overlaps an object')");
        }

        [Test]
        public void Full_Valid_WhenUnobstructed_GroundGood_Affordable()
        {
            Assert.IsTrue(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY,
                canAfford: true, obstructed: false),
                "good ground + affordable + clear of objects → valid (green, READY)");
        }

        [Test]
        public void Full_SixArgOverload_TreatsObstructionAsFalse_NoRegression()
        {
            // The pre-86catqxm0 F4 cells must still pass unchanged — the 6-arg overload defaults obstructed:false.
            Assert.IsTrue(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY, canAfford: true),
                "6-arg overload on good ground + affordable stays valid (obstruction dimension absent)");
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: false, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY, canAfford: true),
                "6-arg overload: no ground still INVALID (F1/F4 unchanged)");
        }

        // ---- 86catqxm0: PURE planar circle-overlap core (the collider-free obstruction test) ----

        [Test]
        public void CircleOverlaps_Overlapping_True()
        {
            Assert.IsTrue(PlacementObstacleRegistry.CircleOverlaps(0f, 0f, 0.55f, 0.5f, 0f, 0.6f),
                "centres 0.5u apart with radii 0.55+0.6 (sum 1.15) overlap");
        }

        [Test]
        public void CircleOverlaps_Apart_False()
        {
            Assert.IsFalse(PlacementObstacleRegistry.CircleOverlaps(0f, 0f, 0.55f, 3f, 0f, 0.6f),
                "centres 3u apart with radii summing 1.15 do NOT overlap");
        }

        [Test]
        public void CircleOverlaps_ExactlyTouching_False()
        {
            Assert.IsFalse(PlacementObstacleRegistry.CircleOverlaps(0f, 0f, 0.5f, 1.0f, 0f, 0.5f),
                "planar distance 1.0 == radii sum 1.0 → boundary treated as clear (strict less-than)");
        }

        // ---- 86catqxm0: the registration seam (② boulders adopt this) ----

        [Test]
        public void Registry_RegisteredZone_BlocksFootprint_ClearsOnUnregister()
        {
            var go = new GameObject("ObstacleProbe");
            go.transform.position = new Vector3(5f, 0f, 5f);
            try
            {
                PlacementObstacleRegistry.Register(go.transform, 0.6f);
                Assert.IsTrue(PlacementObstacleRegistry.IsFootprintBlocked(new Vector3(5f, 0f, 5f), 0.55f),
                    "a footprint on the registered zone is blocked (RED)");
                Assert.IsFalse(PlacementObstacleRegistry.IsFootprintBlocked(new Vector3(20f, 0f, 20f), 0.55f),
                    "a footprint far from any registered zone is clear (GREEN)");
                PlacementObstacleRegistry.Unregister(go.transform);
                Assert.IsFalse(PlacementObstacleRegistry.IsFootprintBlocked(new Vector3(5f, 0f, 5f), 0.55f),
                    "after Unregister the zone no longer blocks");
            }
            finally
            {
                PlacementObstacleRegistry.Unregister(go.transform); // keep the static registry hermetic across tests
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Registry_IgnoreRoot_DoesNotBlockItself()
        {
            var root = new GameObject("GhostRoot");
            var child = new GameObject("GhostPart");
            child.transform.SetParent(root.transform, false);
            child.transform.position = new Vector3(1f, 0f, 1f);
            try
            {
                PlacementObstacleRegistry.Register(child.transform, 0.6f);
                Assert.IsFalse(PlacementObstacleRegistry.IsFootprintBlocked(new Vector3(1f, 0f, 1f), 0.55f, root.transform),
                    "a zone under the ignoreRoot (the ghost/table) never blocks itself");
                Assert.IsTrue(PlacementObstacleRegistry.IsFootprintBlocked(new Vector3(1f, 0f, 1f), 0.55f),
                    "the same zone DOES block when no ignoreRoot is passed");
            }
            finally
            {
                PlacementObstacleRegistry.Unregister(child.transform);
                Object.DestroyImmediate(root);
            }
        }
    }
}
