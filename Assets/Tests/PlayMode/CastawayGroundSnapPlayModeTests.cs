using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Regression guard for soak-fix #1 — "when he walks he is walking in the air" (ticket 86ca8rdkp).
    ///
    /// DIAGNOSE-VIA-TRACE (diagnostic-traces-before-hypothesized-fixes): the ground-trace (-groundTrace,
    /// 2026-06-15) OVERTURNED the ticket's hypothesis (a Walk-vs-Idle clip root-Y mismatch). Measured data:
    /// the WALK feet ride only +0.0005u above the IDLE feet (no float difference), and the sole sits at world
    /// Y 0.081 in BOTH states — but the VISIBLE Zone-D terrain under the player is at Y 0.020, so the feet
    /// float ~0.06u ABOVE the sand the player SEES. ROOT CAUSE: the NavMeshAgent grounds the player ROOT on
    /// the flat NavMesh collider (which rides above the dipping visual terrain); the FBX-origin feet hang
    /// above the visible surface. (Same family as "the asset is fine, the VIEW is the problem" — here it is
    /// the GROUND the agent stands on that diverges from the visible terrain.)
    ///
    /// FIX: CastawayCharacter.ApplyGroundSnap raycasts the Ground layer each frame and drives the avatar
    /// root's local Y so the feet plant on the VISIBLE terrain.
    ///
    /// THE BUG CLASS: the avatar feet must sit ON the visible terrain, regardless of where the agent's NavMesh
    /// ground point is. We reproduce the divergence directly: a player root parked ABOVE a visible terrain
    /// collider, with a real CastawayCharacter child. The guard asserts the feet end up AT the terrain (planted),
    /// and the deliberate-break half (snap off) leaves them floating — proving the snap is load-bearing.
    /// </summary>
    public class CastawayGroundSnapPlayModeTests
    {
        private GameObject _terrain;
        private GameObject _playerGo;
        private GameObject _avatarGo;
        private CastawayCharacter _castaway;
        private GameObject _shadowGo;

        // The agent grounds the player root HERE (above the visible terrain) — the float source.
        private const float RootY = 0.60f;
        // The visible terrain top sits HERE (below the root) — where the feet must end up.
        private const float TerrainY = 0.10f;

        // ISOLATION (full-run cross-fixture leak fix): other PlayMode fixtures LoadScene("Boot") — which carries
        // the REAL renderer-enabled Zone-D Ground_Play terrain. A LoadScene is deferred + may leave that terrain
        // resident when THIS fixture's snap-raycast fires, so the snap picks the real dipping terrain instead of
        // this test's synthetic surface (proven: these tests pass in isolation, foul in the full run, picking the
        // real Boot terrain Y). Load a fresh EMPTY scene before each test so the synthetic Ground colliders are
        // the ONLY ones the snap can hit. (This fixture must control every Ground collider in play.)
        [UnitySetUp]
        public IEnumerator IsolateScene()
        {
            var empty = SceneManager.CreateScene("GroundSnapIsolated_" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            // Unload every OTHER loaded scene (e.g. a leaked Boot) so no foreign Ground collider remains.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != empty && s.isLoaded) { var op = SceneManager.UnloadSceneAsync(s); if (op != null) while (!op.isDone) yield return null; }
            }
            yield return null;
        }

        private void BuildRig(bool snap)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            // A flat MeshCollider plane on the Ground layer whose surface is at TerrainY. A 10u quad collider.
            _terrain = new GameObject("VisibleTerrain");
            if (groundLayer >= 0) _terrain.layer = groundLayer;
            _terrain.transform.position = new Vector3(0f, TerrainY, 0f);
            var mf = _terrain.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, -5f),
                new Vector3(-5f, 0f, 5f), new Vector3(5f, 0f, 5f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            var col = _terrain.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            // The player root parked at the agent's (higher) ground Y. The avatar child hangs off it; without
            // the snap, the FBX-origin feet sit at RootY — floating RootY-TerrainY above the visible terrain.
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, RootY, 0f);

            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero; // FBX origin == feet, at the root Y (the float)
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = snap;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;

            // No modelPrefab: the ground-snap logic is independent of the instantiated mesh (it raycasts +
            // sets the avatar-root local Y). CastawayCharacter LogErrors on a null modelPrefab (a real error
            // in the game), so expect that one log rather than suppress the production guard.
            LogAssert.Expect(LogType.Error,
                "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate (not Destroy): Object.Destroy is DEFERRED to end-of-frame, so a leftover Ground
            // COLLIDER from this test could still be live when the NEXT test's first snap-raycast fires —
            // polluting its pick (proven: both ground-snap tests passed in isolation but fouled each other in
            // the full run). Immediate destruction guarantees a clean Ground layer for the next test.
            if (_terrain != null) { Object.DestroyImmediate(_terrain); _terrain = null; }
            if (_playerGo != null) { Object.DestroyImmediate(_playerGo); _playerGo = null; }
            if (_proxySlab != null) { Object.DestroyImmediate(_proxySlab); _proxySlab = null; }
            _shadowGo = null; // a child of _playerGo — destroyed with it above
        }

        // RE-SOAK #2 regression (86ca8rdkp — 'he STILL seems elevated'). The foot-trace OVERTURNED the feet-
        // float hypothesis: the feet ARE planted on the visible terrain (the PR #47 snap works). The real cause
        // was the BLOB SHADOW — a child of the PLAYER ROOT that does NOT inherit the avatar ground-snap, so it
        // stayed at root level while the feet snapped DOWN onto the dipping foreshore (~9cm gap measured), and
        // the body read as floating ABOVE its own contact shadow = "elevated". The fix drives the shadow's
        // world-Y onto the snapped feet. This guard reproduces the divergence: a shadow parked at the (high)
        // root level, and asserts CastawayCharacter grounds it to the visible terrain (the snapped feet) — and
        // the deliberate-break (shadow unwired) leaves it stranded above the feet.
        [UnityTest]
        public IEnumerator BlobShadow_GroundsToTheSnappedFeet_NotStrandedAtTheAgentRoot()
        {
            BuildRig(snap: true);
            // A blob shadow as a child of the PLAYER ROOT (like the real BuildBlobShadow), parked at the root
            // level — i.e. floating RootY-TerrainY above the visible terrain (the stranded "elevated" state).
            _shadowGo = new GameObject("BlobShadow");
            _shadowGo.transform.SetParent(_playerGo.transform, false);
            _shadowGo.transform.localPosition = new Vector3(0f, 0.02f, 0f); // ~root level, the float
            _castaway.blobShadow = _shadowGo.transform;
            _castaway.blobShadowLift = 0.02f;

            for (int i = 0; i < 40; i++) yield return null; // let the snap settle + the shadow ground each frame

            float shadowY = _shadowGo.transform.position.y;
            float feetY = _avatarGo.transform.position.y;
            // The shadow must sit AT the visible terrain / snapped feet (within the small lift), NOT stranded
            // up at the agent root Y.
            Assert.That(shadowY, Is.EqualTo(TerrainY + _castaway.blobShadowLift).Within(0.03f),
                $"the blob shadow must ground to the SNAPPED feet (Y≈{TerrainY + _castaway.blobShadowLift:F3}); got " +
                $"{shadowY:F3}. Left at the agent root ({RootY}) it strands ABOVE the feet — the 'floats above " +
                "its shadow' = elevated re-soak #2 percept.");
            Assert.That(Mathf.Abs(shadowY - (feetY + _castaway.blobShadowLift)), Is.LessThan(0.04f),
                $"the shadow (Y={shadowY:F3}) must sit at the feet (Y={feetY:F3}) + the lift — a contact read.");
        }

        // The deliberate-break half: with the shadow UNWIRED, it stays stranded at the root level (the bug),
        // proving the wiring is load-bearing for the contact-grounding fix.
        [UnityTest]
        public IEnumerator BlobShadowUnwired_StaysStrandedAtRoot_ProvingTheWiringIsLoadBearing()
        {
            BuildRig(snap: true);
            _shadowGo = new GameObject("BlobShadow");
            _shadowGo.transform.SetParent(_playerGo.transform, false);
            _shadowGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            _castaway.blobShadow = null; // UNWIRED — CastawayCharacter must not touch it

            for (int i = 0; i < 40; i++) yield return null;

            float shadowY = _shadowGo.transform.position.y;
            Assert.That(shadowY, Is.EqualTo(RootY + 0.02f).Within(0.01f),
                $"with the shadow UNWIRED it stays at the root level ({RootY + 0.02f:F3}) — the stranded float. " +
                $"Got {shadowY:F3}. (The fix grounds it only when wired.)");
        }

        // The CORE grounding guard: the avatar feet snap DOWN onto the visible terrain (Y≈TerrainY), not left
        // floating at the agent's higher root Y.
        [UnityTest]
        public IEnumerator AvatarFeet_SnapToVisibleTerrain_NotLeftFloatingAtAgentRoot()
        {
            BuildRig(snap: true);
            for (int i = 0; i < 40; i++) yield return null; // the snap lerps toward the target — let it settle

            float feetY = _avatarGo.transform.position.y;
            Assert.That(feetY, Is.EqualTo(TerrainY).Within(0.03f),
                $"the avatar feet must snap onto the VISIBLE terrain (Y≈{TerrainY}); got {feetY:F3}. " +
                $"Leaving them at the agent root Y ({RootY}) is the 'walking in the air' float.");

            // And the snap must have moved them DOWN by ~the float gap (not a no-op).
            Assert.That(_castaway.GroundSnapLocalY, Is.LessThan(-0.1f),
                $"the ground-snap must drive the avatar-root local Y DOWN onto the terrain " +
                $"(got {_castaway.GroundSnapLocalY:F3}); a near-zero snap means the fix regressed.");
        }

        // The deliberate-break half (success-test discipline): with the snap OFF the feet stay at the agent
        // root Y (the float). Proves the snap — not some other effect — is what grounds the feet.
        [UnityTest]
        public IEnumerator GroundSnapOff_FeetFloatAtAgentRoot_ProvingTheSnapIsLoadBearing()
        {
            BuildRig(snap: false);
            for (int i = 0; i < 40; i++) yield return null;

            float feetY = _avatarGo.transform.position.y;
            Assert.That(feetY, Is.EqualTo(RootY).Within(0.01f),
                $"with the snap OFF the feet stay at the agent root Y ({RootY}) — the float. Got {feetY:F3}.");
        }

        // ====================================================================================================
        // RE-SOAK regression (the bug class PR #47 MISSED — "he STILL walks elevated"). The -groundTrace proved
        // the snap snapped to the wrong collider: a flat collision-PROXY slab (renderer DISABLED) that rides
        // ABOVE the dipping visible terrain on the foreshore, so a single-topmost-hit raycast picked the slab
        // and the feet floated above the visible sand. These guards reproduce that exact two-collider geometry
        // and assert the snap tracks the VISIBLE (renderer-enabled) terrain across a MOVING path — not just at
        // spawn. The original spawn-only assert above passed during the entire walk-float era — this is the
        // listener-wiring-grade guard that actually catches the class.
        // ====================================================================================================
        private GameObject _proxySlab;

        // Build the two-collider re-soak geometry: a renderer-DISABLED flat proxy slab at Y=0 (the TestGround /
        // NavMesh stand-in) covering the whole area, PLUS a renderer-ENABLED "visible" terrain whose surface Y
        // is a function of Z (flat near the start, DIPPING below the slab as Z decreases — the foreshore). The
        // player root is driven along −Z (shoreward) while the snap must keep the feet on the VISIBLE terrain,
        // i.e. BELOW the slab once the visible terrain dips under it.
        private GameObject BuildVisible(float zStart, float zEnd, float ySlope, int groundLayer)
        {
            // Visible terrain: a 2-tri ramp on the Ground layer with an ENABLED MeshRenderer. Y decreases as Z
            // decreases (shoreward dip). Flat at zStart (Y=0), dipping to ySlope at zEnd.
            var vis = new GameObject("Ground_Play");
            if (groundLayer >= 0) vis.layer = groundLayer;
            var mf = vis.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-6f, 0f,      zStart), new Vector3(6f, 0f,      zStart),
                new Vector3(-6f, ySlope,  zEnd),   new Vector3(6f, ySlope,  zEnd)
            };
            // Wind for UP-facing (+Y) normals so the MeshCollider is hittable by a DOWNWARD ray (a MeshCollider
            // is single-sided — a down-facing ramp would be missed from above, masking the real snap behavior).
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            var mr = vis.AddComponent<MeshRenderer>();
            mr.enabled = true; // VISIBLE — the surface the player sees
            var col = vis.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            return vis;
        }

        // Walking shoreward, the feet must stay on the VISIBLE dipping terrain — NOT pinned to the renderer-
        // disabled proxy slab above it. This is the exact -groundTrace failure: slab Y=0, visible sand dipping
        // to ~−0.4, single-topmost-raycast pinned the feet at 0.0. Asserts the feet TRACK the visible Y across
        // multiple positions along the path (not just at spawn).
        [UnityTest]
        public IEnumerator AvatarFeet_TrackTheVisibleTerrain_AcrossAMovingPath_NotThePoxySlabAbove()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");

            // Renderer-DISABLED proxy slab at Y=0 over the whole area (the NavMesh/collision proxy).
            _proxySlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _proxySlab.name = "TestGround";
            if (groundLayer >= 0) _proxySlab.layer = groundLayer;
            _proxySlab.transform.position = new Vector3(0f, -0.5f, 0f);   // top face at Y=0
            _proxySlab.transform.localScale = new Vector3(40f, 1f, 40f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;       // collision proxy ONLY (grey-slab fix)

            // Visible terrain dipping shoreward: flat (Y=0) at Z=+6, dipping to Y=−0.4 at Z=−6.
            _terrain = BuildVisible(zStart: 6f, zEnd: -6f, ySlope: -0.4f, groundLayer);

            // Player root + avatar (the snap drives the avatar-root local Y; the root rides the NavMesh slab at
            // a fixed Y above, like the real agent). Root Y stays at the slab top (0) — the agent grounds there.
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 6f);
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");

            // Walk the root shoreward (−Z) in steps. At each position assert the snap SELECTS the VISIBLE
            // terrain (LastSnapTargetWorldY), NOT the proxy slab at Y=0. We read the snap TARGET (pre-smoothing)
            // rather than the settled feet Y because headless PlayMode has Time.deltaTime≈0 (the documented
            // headless-time trap, unity-conventions.md §Headless) — the smoothed lerp barely advances per frame,
            // so a settled-feet assert would be testing the lerp, not the pick. The TARGET is the load-bearing
            // thing: the PR #47 bug was that the snap picked the SLAB target once the sand dipped below it.
            float[] zSamples = { 6f, 3f, 0f, -3f, -6f };
            foreach (float z in zSamples)
            {
                _playerGo.transform.position = new Vector3(0f, 0f, z);
                for (int i = 0; i < 5; i++) yield return null; // a few frames so the raycast re-samples this pos

                // Expected visible Y at this Z (the ramp: flat 0 at Z=+6 down to −0.4 at Z=−6).
                float t = Mathf.InverseLerp(6f, -6f, z);
                float expectedVisY = Mathf.Lerp(0f, -0.4f, t);
                float target = _castaway.LastSnapTargetWorldY;

                Assert.IsFalse(float.IsNaN(target), $"at Z={z} the snap must hit a Ground surface");
                Assert.That(target, Is.EqualTo(expectedVisY).Within(0.03f),
                    $"at Z={z} the snap must SELECT the VISIBLE terrain (Y≈{expectedVisY:F3}), not the proxy " +
                    $"slab at Y=0. Selected {target:F3}. This is the 'still walks elevated' bug: a single " +
                    "topmost-hit raycast picks the renderer-disabled slab once the visible sand dips below it.");

                // Once the visible terrain has dipped below the slab, the SELECTED target MUST be below 0
                // (the slab top) — proving the snap follows the sand DOWN, not pinned to the slab.
                if (expectedVisY < -0.05f)
                    Assert.Less(target, -0.03f,
                        $"at Z={z} the visible sand dips to {expectedVisY:F3} (below the slab at 0) — the snap " +
                        $"must follow it BELOW 0, but selected {target:F3} (pinned to the slab = the bug).");
            }

            // And the deliberate-break: with the snap reading the SLAB (the old single-topmost behavior), the
            // selected target at the shoreward end would be 0 (the slab), NOT the dipped sand. We prove the new
            // behavior diverges from that: at the most-dipped sample the target is well below the slab top.
            _playerGo.transform.position = new Vector3(0f, 0f, -6f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.Less(_castaway.LastSnapTargetWorldY, -0.2f,
                "at the shoreward end the visible sand dips to ~−0.4; the snap target must follow it (≪ the " +
                "slab's Y=0). A target near 0 here means the snap regressed to picking the proxy slab.");
        }
    }
}
