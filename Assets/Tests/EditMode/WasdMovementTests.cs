using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode tests for the CAMERA-RELATIVE direction math of WASD locomotion (ticket 86ca9yq2x, AC1).
    ///
    /// These pin the BUG CLASS, not one instance: WasdMovement.CameraRelativeDirection is the pure,
    /// dependency-free core that maps a WASD input into the camera's GROUND-PLANE basis. The classic WASD
    /// bugs this guards are (a) "W moves world-forward, not camera-forward" (camera basis ignored), (b)
    /// "the character flies/sinks when the camera is pitched down" (the camera's −Y forward leaks into the
    /// move vector), and (c) A/D inverted. Asserting the pure function from multiple camera headings + a
    /// steep pitch catches all three with no scene rig (deterministic, no Animator/NavMesh/headless-time).
    /// </summary>
    public class WasdMovementTests
    {
        // The orbit camera at the Sponsor-locked default pitch 55° facing +Z (yaw 0): forward points
        // down-and-forward, right points +X. This is the real gameplay basis the move math must flatten.
        private static void DefaultOrbitBasis(float yawDeg, out Vector3 fwd, out Vector3 right)
        {
            // Pitch 55° down, yaw about Y. Quaternion.Euler(pitch, yaw, 0) is exactly how OrbitCamera builds it.
            Quaternion rot = Quaternion.Euler(55f, yawDeg, 0f);
            fwd = rot * Vector3.forward;   // has a large −Y component (camera looks down)
            right = rot * Vector3.right;
        }

        [Test]
        public void ForwardW_WithCameraFacingPlusZ_MovesPlusZ_AndIsPlanar()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            // W = (x:0, y:+1).
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));

            Assert.AreEqual(0f, dir.y, 1e-4f,
                "the move direction must be PLANAR — the camera's steep −Y forward must NOT leak into the " +
                "move vector (else the character flies/sinks when the orbit cam pitches down). dir.y was " + dir.y);
            Assert.Greater(dir.z, 0.9f,
                "W with the camera facing +Z must move the character +Z (camera-relative forward), not some " +
                "other axis — dir was " + dir);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f, "the move direction must be unit-length while moving");
        }

        [Test]
        public void BackS_IsOppositeForward()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            Vector3 w = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));
            Vector3 s = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, -1f));
            Assert.Less(Vector3.Dot(w, s), -0.99f, "S must move opposite to W (back vs forward).");
        }

        [Test]
        public void StrafeAD_IsPerpendicularToForward_AndCorrectHandedness()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            Vector3 d = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(1f, 0f)); // D = +strafe
            Vector3 a = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(-1f, 0f)); // A = −strafe
            Vector3 w = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));

            Assert.AreEqual(0f, Vector3.Dot(d, w), 1e-3f, "strafe must be perpendicular to forward");
            Assert.Less(Vector3.Dot(d, a), -0.99f, "A and D must be opposite strafe directions");
            // With the camera facing +Z, D (right strafe) must be +X (camera-right projected to the plane).
            Assert.Greater(d.x, 0.9f, "D (right strafe) with the camera facing +Z must move +X (camera-right)");
        }

        [Test]
        public void Rotated45Camera_ForwardFollowsCameraHeading()
        {
            // Orbit the camera 45° about Y; forward must now point into the +X/+Z quadrant (diagonal), not +Z.
            DefaultOrbitBasis(45f, out var fwd, out var right);
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));
            Assert.AreEqual(0f, dir.y, 1e-4f, "still planar at a rotated heading");
            Assert.Greater(dir.x, 0.5f, "forward at yaw 45 must carry +X — the move tracks the camera heading");
            Assert.Greater(dir.z, 0.5f, "forward at yaw 45 must carry +Z — the move tracks the camera heading");
            // The heading must equal the camera's PLANAR forward (the camera-relative guarantee).
            Vector3 camPlanar = new Vector3(fwd.x, 0f, fwd.z).normalized;
            Assert.Greater(Vector3.Dot(dir, camPlanar), 0.999f,
                "W must move along the camera's planar forward at every heading (the camera-relative AC).");
        }

        [Test]
        public void ZeroInput_YieldsZeroDirection()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, Vector2.zero);
            Assert.AreEqual(Vector3.zero, dir, "no input → no move direction (the character holds Idle)");
        }

        [Test]
        public void NearlyTopDownCamera_StillProducesAPlanarForward_NoNaN()
        {
            // A near-vertical camera (pitch ~89°) has an almost-zero planar forward; the degenerate guard must
            // still produce a finite, planar, unit direction (never NaN / never a vertical move).
            Quaternion rot = Quaternion.Euler(89f, 0f, 0f);
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right;
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));
            Assert.IsFalse(float.IsNaN(dir.x) || float.IsNaN(dir.y) || float.IsNaN(dir.z),
                "the degenerate near-top-down camera must not yield a NaN move direction");
            Assert.AreEqual(0f, dir.y, 1e-4f, "even a near-top-down camera yields a planar move (no vertical creep)");
            Assert.AreEqual(1f, dir.magnitude, 1e-3f, "still unit-length");
        }
    }
}
