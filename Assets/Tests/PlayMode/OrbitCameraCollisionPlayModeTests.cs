using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// BIG ROUND ISLAND N2 (ticket 86ca9a7qn — "player disappears under a hill"). The orbit camera placed
    /// itself at a fixed distance behind the target with NO terrain awareness, so on the hilly island it could
    /// (a) sink BELOW the terrain or (b) have a hill between it and the character → the player vanishes.
    /// OrbitCamera.ResolveCameraCollision keeps the camera ABOVE the ground AND pulls it IN when a hill
    /// occludes the player.
    ///
    /// These exercise the collision resolution DIRECTLY against synthetic terrain colliders (a Ground-layer
    /// hill + floor), so the bug class is caught deterministically without a full scene build:
    ///  - occlusion: a hill BETWEEN the player and the desired cam position → the cam is pulled IN to just
    ///    before the hill (so the line of sight to the player is clear);
    ///  - above-ground: a desired cam position UNDER the terrain → the cam is lifted ABOVE the surface;
    ///  - no-mask: terrainMask 0 → no collision (the camera behaves exactly as before — tests/rigs without a
    ///    Ground layer are unaffected).
    /// A regression that drops either raycast (re-burying the player under a hill) fails here.
    /// </summary>
    public class OrbitCameraCollisionPlayModeTests
    {
        private GameObject _camGo;
        private OrbitCamera _orbit;
        private GameObject _hill;
        private GameObject _floor;
        private int _groundLayer;
        private int _useLayer;

        // ISOLATION (full-run cross-fixture leak fix, mirrors CastawayGroundSnapPlayModeTests): other PlayMode
        // fixtures LoadScene("Boot") — which carries the REAL Ground_Play island terrain spanning the origin.
        // A deferred LoadScene can leave that terrain resident when THIS fixture's camera raycasts fire, so the
        // raycast hits the real island terrain instead of this test's synthetic floor (proven: these tests pass
        // in isolation, foul in the full run). Load a fresh EMPTY scene + unload every other scene first, so the
        // synthetic Ground colliders are the ONLY ones the camera-collision raycasts can hit. THEN build the rig.
        [UnitySetUp]
        public IEnumerator IsolateSceneAndBuildRig()
        {
            yield return PlayModeSceneIsolation.IsolateInFreshScene("OrbitCamIsolated");
            yield return null;

            _groundLayer = LayerMask.NameToLayer("Ground");
            // Fall back to Default if the project lacks a Ground layer in this runner — the collision logic is
            // layer-driven, so we use whatever layer the colliders are actually on for the mask.
            _useLayer = _groundLayer >= 0 ? _groundLayer : 0;

            _camGo = new GameObject("OrbitCam");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();
            _orbit.target = new GameObject("OrbitTarget").transform;
            _orbit.groundClearance = 0.6f;
            _orbit.collisionPadding = 0.35f;
            _orbit.minCollisionDistance = 2.5f;
            _orbit.terrainMask = 1 << _useLayer;

            // A big flat floor at y=0 (top surface at y=0).
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(200f, 1f, 200f);
            _floor.layer = _useLayer;
            Physics.SyncTransforms();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_orbit != null && _orbit.target != null) Object.Destroy(_orbit.target.gameObject);
            Object.Destroy(_camGo);
            if (_hill != null) Object.Destroy(_hill);
            Object.Destroy(_floor);
        }

        [UnityTest]
        public IEnumerator NoTerrainMask_ReturnsDesiredPosition_Unchanged()
        {
            yield return null;
            _orbit.terrainMask = 0; // collision disabled
            Vector3 follow = new Vector3(0f, 2f, 0f);
            Vector3 desired = new Vector3(0f, 8f, -14f);
            Vector3 resolved = _orbit.ResolveCameraCollision(follow, desired);
            Assert.AreEqual(desired, resolved,
                "with no terrainMask the camera must use the desired position unchanged (collision off)");
        }

        [UnityTest]
        public IEnumerator HillBetweenPlayerAndCamera_PullsCameraIn_LineOfSightClear()
        {
            // A tall hill sits BEHIND the player (between the player and the desired far camera position). The
            // camera must be pulled IN to just before the hill so it can see the player over open ground.
            _hill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _hill.transform.position = new Vector3(0f, 5f, -8f); // behind the player toward the cam
            _hill.transform.localScale = new Vector3(8f, 10f, 4f);
            _hill.layer = _useLayer;
            Physics.SyncTransforms();
            yield return null;

            Vector3 follow = new Vector3(0f, 2f, 0f);
            Vector3 desired = new Vector3(0f, 8f, -16f); // far behind the hill
            Vector3 resolved = _orbit.ResolveCameraCollision(follow, desired);

            float desiredDist = Vector3.Distance(follow, desired);
            float resolvedDist = Vector3.Distance(follow, resolved);
            Assert.Less(resolvedDist, desiredDist,
                "a hill between the player and the desired cam pos must pull the camera IN (closer than desired)");
            Assert.GreaterOrEqual(resolvedDist, _orbit.minCollisionDistance - 0.01f,
                "the pull-in must never come closer than minCollisionDistance (no face-zoom)");

            // The resolved position must NOT be on the far side of the hill from the player: the line from the
            // player to the resolved cam must be unobstructed by terrain.
            Vector3 dir = (resolved - follow).normalized;
            bool blocked = Physics.Raycast(follow, dir, out _, Vector3.Distance(follow, resolved) - 0.05f,
                _orbit.terrainMask, QueryTriggerInteraction.Ignore);
            Assert.IsFalse(blocked,
                "after the pull-in the line of sight from the player to the camera must be CLEAR of terrain " +
                "(the player is never behind a hill)");
        }

        [UnityTest]
        public IEnumerator DesiredCameraUnderTerrain_IsLiftedAboveSurface()
        {
            // Desired cam position BELOW the floor surface (e.g. it dropped into a dip behind a ridge). The
            // above-ground clamp must lift it to (surface + groundClearance) so it never sinks under the hill.
            yield return null;
            Vector3 follow = new Vector3(0f, 2f, 0f);
            Vector3 desired = new Vector3(20f, -3f, 0f); // under the y=0 floor surface
            Vector3 resolved = _orbit.ResolveCameraCollision(follow, desired);
            Assert.GreaterOrEqual(resolved.y, 0f + _orbit.groundClearance - 0.01f,
                "a camera position below the terrain must be lifted to at least (surface + groundClearance) — " +
                "the camera must never sink under/into a hill (the N2 'disappears under a hill' fix). " +
                "resolved.y was " + resolved.y.ToString("0.00"));
        }
    }
}
