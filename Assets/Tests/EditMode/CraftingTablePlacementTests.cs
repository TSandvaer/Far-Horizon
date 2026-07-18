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
    }
}
