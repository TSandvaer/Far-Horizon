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
            // Isolate in a fresh empty scene so no foreign Ground collider (e.g. a leaked Boot) remains.
            yield return PlayModeSceneIsolation.IsolateInFreshScene("GroundSnapIsolated");
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
            if (_smrGo != null) { Object.DestroyImmediate(_smrGo); _smrGo = null; } // child of _avatarGo; explicit for clarity
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

        // ====================================================================================================
        // DURING-WALK regression (86ca8rdkp 4th-attempt — 'STILL elevated WHILE WALKING'). The standing /
        // after-walk case was verified by the prior fixes; the hole was the MOVING case. The -walkTrace +
        // a 60fps smoothing simulation proved: (a) a CONSTANT-rate exp filter lags the descending foreshore
        // ~1.2cm WHILE MOVING then re-converges at rest (='grounded standing, elevated walking'), and (b) the
        // shadow was driven off the RAW target while the feet rode the SMOOTHED Y, so the shadow separated from
        // the feet ONLY in motion (re-elevated-while-walking). These guards pin the two fix invariants.
        // ====================================================================================================

        // INVARIANT 1 (the structural shadow fix): the shadow is locked to the avatar's ACTUAL feet world Y
        // (+lift) — NOT the raw terrain hit — so it can NEVER lead or lag the feet, in motion OR at rest.
        // DETERMINISTIC divergence WITHOUT relying on the smoothing converging (headless Time.deltaTime≈0 +
        // the _snapInit instant-first-frame snap make a smoothing-lag repro non-deterministic): use a NON-ZERO
        // groundYOffset so the avatar feet land at (terrain + offset) while the RAW terrain hit is just
        // (terrain). The old code drove the shadow off the raw hit (bestY) → it would sit `offset` BELOW the
        // feet; the fix drives it off the avatar's actual world Y → it sits AT the feet. This proves the
        // shadow's SOURCE is the feet, not the terrain hit — the during-walk separation bug by construction.
        [UnityTest]
        public IEnumerator BlobShadow_TracksTheActualFeet_NotTheRawTerrainHit()
        {
            BuildRig(snap: true);
            _shadowGo = new GameObject("BlobShadow");
            _shadowGo.transform.SetParent(_playerGo.transform, false);
            _shadowGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            _castaway.blobShadow = _shadowGo.transform;
            _castaway.blobShadowLift = 0.02f;
            // A non-zero offset makes the feet land OFF the raw terrain hit, so the shadow's source is provable:
            // feet = terrain+offset; raw hit = terrain. If the shadow tracked the raw hit it would be `offset`
            // below the feet (the old-code separation); the fix keeps it at the feet.
            const float off = 0.20f;
            _castaway.groundYOffset = off;

            for (int i = 0; i < 40; i++) yield return null; // let it settle

            float feetY = _avatarGo.transform.position.y;
            float shadowY = _shadowGo.transform.position.y;
            // Feet must be at terrain + offset (the snap drove them there).
            Assert.That(feetY, Is.EqualTo(TerrainY + off).Within(0.03f),
                $"with offset {off} the feet must plant at terrain+offset (Y≈{TerrainY + off:F3}); got {feetY:F3}");
            // The shadow must sit at the FEET (+lift), NOT at the raw terrain hit (which is `off` below).
            Assert.That(shadowY, Is.EqualTo(feetY + _castaway.blobShadowLift).Within(0.02f),
                $"the shadow ({shadowY:F3}) must sit at the ACTUAL feet ({feetY:F3}) + lift — not at the raw " +
                $"terrain hit ({TerrainY:F3}), which is {off} below. Driving the shadow off the raw hit is the " +
                "during-walk separation bug (feet ride the smoothed/offset Y; the raw hit does not).");
            Assert.Greater(shadowY, TerrainY + _castaway.blobShadowLift + off * 0.5f,
                "the shadow must NOT be stranded down at the raw terrain hit (the old-code behavior) — it must " +
                "rise with the feet's offset, proving it tracks the feet not the hit.");
        }

        // INVARIANT 2 (the Sponsor-dialable knob works end-to-end): groundYOffset shifts the snapped feet (and
        // hence the shadow, locked by Invariant 1) by exactly the dialed amount, vs an offset-0 baseline. Two
        // SEPARATE fixtures (offset set BEFORE settle so the deterministic first-frame snap captures it — a
        // mid-run change can't converge headless where Time.deltaTime≈0). A no-op means the dial is dead = the
        // knob the 4th attempt exists to give the Sponsor never reaches the feet.
        [UnityTest]
        public IEnumerator GroundYOffset_LiftsTheFeet_ByTheDialedAmount_VsBaseline()
        {
            // Baseline: offset 0 → feet plant on the terrain.
            BuildRig(snap: true);
            _castaway.groundYOffset = 0f;
            for (int i = 0; i < 40; i++) yield return null;
            float feet0 = _avatarGo.transform.position.y;
            Assert.That(feet0, Is.EqualTo(TerrainY).Within(0.03f),
                $"baseline (offset 0) feet must plant on the terrain (Y≈{TerrainY}); got {feet0:F3}");

            // Tear down + rebuild with a dialed offset (set BEFORE the rig settles so the first-frame snap
            // captures it deterministically). Same synthetic terrain → same baseline, shifted by the offset.
            Object.DestroyImmediate(_terrain); _terrain = null;
            Object.DestroyImmediate(_playerGo); _playerGo = null;
            const float off = 0.15f;
            BuildRig(snap: true);
            _castaway.groundYOffset = off;
            for (int i = 0; i < 40; i++) yield return null;
            float feet1 = _avatarGo.transform.position.y;

            Assert.That(feet1, Is.EqualTo(TerrainY + off).Within(0.03f),
                $"the dialed groundYOffset ({off}) must plant the feet at terrain+offset (Y≈{TerrainY + off:F3}); " +
                $"got {feet1:F3} — a no-op means the knob doesn't drive the snap (the Sponsor's dial is dead)");
            Assert.That(feet1 - feet0, Is.EqualTo(off).Within(0.03f),
                $"the offset must lift the feet by ~the dialed amount (feet {feet0:F3}→{feet1:F3})");
        }

        private GameObject _smrGo;

        // ====================================================================================================
        // ATTEMPT-8 ROOT-CAUSE GUARDS (86ca8rdkp — the SOURCE fix, SUPERSEDES the sole-chasing guards above).
        // The ~6-iteration float saga + the ±68 runaway all came from CHASING the rendered sole each frame, and
        // the deepest false-green was that the per-frame BakeMesh world-Y DOUBLE-APPLIED the FBX's intrinsic
        // 100× cm→m node scale (scale-trace.log: the SkinnedMeshRenderer's own "model" node is localScale=100;
        // BakeMesh(false)+smr.localToWorldMatrix blew the world Y to ~+283 standing / drove avatarRootY to ~−68
        // mid-walk in a false-green equilibrium while the character left frame).
        //
        // SOURCE FIX (probe-verified): the 100× is intrinsic to the FBX and no importer flag removes it; the
        // mesh RENDERS correct, only world-space BakeMesh measurements explode. So (1) the snap grounds the
        // avatar ROOT directly to (groundHit + K) — NO per-frame mesh chasing, no 100× exposure, K a small fixed
        // constant (the FBX origin is at the feet; clips are in-place); and (2) MeasureRenderedSoleWorldY uses a
        // SCALE-IMMUNE unit-scale world matrix so the F8 gauge reads a sane sole (not ±68 garbage).
        //
        // These guards assert the two invariants the SOURCE fix rests on:
        //   A. avatarRootY stays BOUNDED within a small band of (groundHit + K) across a MOVING path — REDS on
        //      the −68 runaway (the exact failure mode).
        //   B. the gauge's rendered-sole world-Y is SANE (bounded near the ground), NOT ±68 — i.e. the 100×
        //      cm→m node never double-applies (the scale-immunity guard).
        // ====================================================================================================

        // Attach a synthetic SkinnedMeshRenderer with an INTRINSIC 100× node scale on its OWN transform — the
        // exact cm→m trap the production FBX carries (the "model" node at localScale 100). The mesh verts are
        // authored TINY (metres ÷100) so the 100× brings them to ~real size, mirroring the FBX. A naive
        // BakeMesh(false)+smr.localToWorldMatrix on this DOUBLE-APPLIES the 100× and explodes the world Y; a
        // scale-immune (unit-scale-matrix) read stays sane. This lets the scale-immunity guard run headless
        // without the 5MB FBX (and reds if anyone reverts MeasureRenderedSoleWorldY to smr.l2w).
        private void AttachSyntheticSkinnedMesh_With100xCmToMNode(Transform avatarRoot)
        {
            _smrGo = new GameObject("Synthetic100xSkin");
            _smrGo.transform.SetParent(avatarRoot, false);
            _smrGo.transform.localPosition = Vector3.zero;
            _smrGo.transform.localScale = Vector3.one * 100f; // the cm→m trap node (FBX "model" node = 100×)
            var smr = _smrGo.AddComponent<SkinnedMeshRenderer>();

            var mesh = new Mesh();
            // Verts authored in metres ÷100 (so 100× node → ~0.5u tall, feet ≈ at the node origin). A standing
            // mesh: lowest vert ≈ 0 (the sole at the renderer origin = the avatar root, like the real bind).
            const float s = 0.01f; // 1/100
            float lo = 0f, hi = 50f * s; // 0 .. 0.5u after the 100× node
            mesh.vertices = new[]
            {
                new Vector3(-20f * s, lo, 0f), new Vector3(20f * s, lo, 0f),
                new Vector3(-20f * s, hi, 0f), new Vector3(20f * s, hi, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            var bw = new BoneWeight[4];
            for (int i = 0; i < 4; i++) { bw[i].boneIndex0 = 0; bw[i].weight0 = 1f; }
            mesh.boneWeights = bw;
            mesh.bindposes = new[] { Matrix4x4.identity };
            smr.bones = new[] { _smrGo.transform };
            smr.rootBone = _smrGo.transform;
            smr.sharedMesh = mesh;
            smr.updateWhenOffscreen = true;
        }

        // GUARD A — the avatar ROOT stays BOUNDED near (groundHit + K) across a MOVING shoreward path; REDS on
        // the −68 runaway. We drive the player root along the dipping foreshore (the two-collider geometry) and
        // at every position assert the avatar-root world-Y tracks the visible terrain within a tight band —
        // NEVER running away to a ±large equilibrium. This is THE regression for the attempt-8 root cause: the
        // ±68 divergence would blow this assert immediately (the prior per-frame mesh-chase drove the root to
        // −68 to compensate the exploded world-Y).
        [UnityTest]
        public IEnumerator AvatarRootY_StaysBounded_NearGroundPlusK_AcrossAMovingWalk_RedsOnRunaway()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");

            // Renderer-DISABLED proxy slab at Y=0 (the NavMesh/collision proxy) + a renderer-ENABLED visible
            // terrain dipping shoreward (flat Y=0 at Z=+6 → Y=−0.4 at Z=−6) — the production two-collider geometry.
            _proxySlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _proxySlab.name = "TestGround";
            if (groundLayer >= 0) _proxySlab.layer = groundLayer;
            _proxySlab.transform.position = new Vector3(0f, -0.5f, 0f); // top face at Y=0
            _proxySlab.transform.localScale = new Vector3(40f, 1f, 40f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;
            _terrain = BuildVisible(zStart: 6f, zEnd: -6f, ySlope: -0.4f, groundLayer);

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 6f);
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            // A REAL 100× cm→m skinned mesh so the gauge's BakeMesh path runs against the trap (without it the
            // snap is mesh-free anyway, but we want a representative rig). LogError still expected (modelPrefab null).
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);

            // Walk shoreward. The snap TARGET (LastSnapTargetWorldY = groundHit + K) is the bounded quantity the
            // snap drives the root toward; assert IT tracks the visible terrain within a tight band (the prior
            // per-frame BakeMesh chase computed a target driven by the 100×-blown world-Y → the target itself
            // ran away to compensate). Headless Time.deltaTime≈0 stalls the smoothing lerp between successive
            // positions (the documented headless-time trap — the sibling AvatarFeet_TrackTheVisibleTerrain test
            // reads the target for the same reason), so we read the TARGET (recomputed every frame) for the
            // tight-band assert AND apply a hard runaway gate to the settled root (which must never be metres off
            // the ground regardless of lerp progress — the −68 equilibrium would blow it).
            float[] zSamples = { 6f, 3f, 0f, -3f, -6f };
            foreach (float z in zSamples)
            {
                _playerGo.transform.position = new Vector3(0f, 0f, z);
                for (int i = 0; i < 8; i++) yield return null;

                float t = Mathf.InverseLerp(6f, -6f, z);
                float expectedVisY = Mathf.Lerp(0f, -0.4f, t); // K=0 default → plant at the visible terrain
                float target = _castaway.LastSnapTargetWorldY; // groundHit + K, recomputed each frame (no lerp lag)
                float rootY = _avatarGo.transform.position.y;

                // THE bounded assert: the snap TARGET tracks (groundHit + K) within a tight band — NEVER ±68.
                Assert.IsFalse(float.IsNaN(target), $"at Z={z} the snap must hit a Ground surface (target valid)");
                Assert.That(target, Is.EqualTo(expectedVisY).Within(0.06f),
                    $"at Z={z} the snap TARGET (groundHit+K) must stay bounded near {expectedVisY:F3}; got " +
                    $"{target:F3}. A runaway to a large ±value (the ±68 equilibrium) is the attempt-8 root cause: " +
                    "the per-frame BakeMesh world-Y double-applied the FBX 100× node and the snap chased it off " +
                    "into a false-green equilibrium while the character left frame.");
                // Hard runaway gate: the settled root must NEVER be metres off the ground (the −68 class), even
                // mid-lerp. With the source fix the target is bounded so the root can only converge to a bounded Y.
                Assert.Less(Mathf.Abs(rootY), 5f,
                    $"at Z={z} the avatar root Y ({rootY:F3}) must stay within metres of the ground — a |Y|≫1 " +
                    "value is the −68 runaway (REDS the exact failure this attempt fixes).");
            }
        }

        // GUARD B — SCALE-IMMUNITY: the gauge's rendered-sole world-Y (MeshBottomWorldY) is SANE (near the
        // ground), NOT the ±283 / ±68 garbage the cm→m 100× node produces when BakeMesh verts are multiplied by
        // smr.localToWorldMatrix. With the synthetic 100× node + the root grounded near 0, a scale-IMMUNE sole
        // measure reads ≈0; a regression back to smr.l2w would read ~+50 (the 100×-blown world Y). This is the
        // listener-wiring-grade guard for the cm→m trap: it reds if MeasureRenderedSoleWorldY reverts to l2w.
        [UnityTest]
        public IEnumerator RenderedSoleGauge_IsScaleImmune_NotBlownByThe100xCmToMNode()
        {
            BuildRig(snap: true); // terrain at TerrainY (0.10), root parked above
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);

            for (int i = 0; i < 40; i++) yield return null; // settle the snap (grounds the root to the terrain)

            float soleY = _castaway.MeshBottomWorldY;     // the SCALE-IMMUNE gauge readout
            float rootY = _avatarGo.transform.position.y; // grounded near TerrainY

            Assert.IsFalse(float.IsNaN(soleY), "the gauge must resolve the synthetic SMR's sole");
            // The synthetic sole sits at the node origin (lo=0), so after grounding the root to TerrainY the
            // sole world-Y must be ≈ TerrainY — a SANE value within centimetres of the ground.
            Assert.That(soleY, Is.EqualTo(rootY).Within(0.05f),
                $"the gauge's rendered-sole world-Y ({soleY:F4}) must be SANE — near the grounded root " +
                $"({rootY:F4}). A value tens-of-units off (e.g. ~+50 or ±283/±68) means MeasureRenderedSoleWorldY " +
                "reverted to smr.localToWorldMatrix and DOUBLE-APPLIED the FBX 100× cm→m node (the attempt-8 " +
                "root cause). The scale-immune unit-scale matrix is what keeps it bounded.");
            // Hard scale-explosion gate: the sole must NEVER be metres off the ground.
            Assert.Less(Mathf.Abs(soleY), 5f,
                $"the rendered-sole gauge ({soleY:F3}) must stay within metres of the ground — a |Y|≫1 reading " +
                "is the 100× cm→m blow-up (the ±68/±283 garbage the saga turned on).");
        }

        // ====================================================================================================
        // UNIFIED-RATE / KILL-THE-BOB guard (86ca8rdkp EXTENSIVE-DEBUG round — 'reaching a destination causes a
        // BOB'). The prior speed-adaptive split (60 moving / 18 rest) made ActiveSnapRate JUMP at the
        // moving→rest transition, so the still-converging snap visibly settled the instant the agent stopped =
        // the arrival bob. The fix uses ONE unified rate regardless of motion. Without a real NavMeshAgent in
        // this rig the agent is null → IsMovingForSnap is always false; we assert the ActiveSnapRate the snap
        // applies EQUALS the configured unified snapRate (NOT a separate move/rest value) — proving there is no
        // rate branch on motion that could discontinue at arrival.
        // ====================================================================================================
        [UnityTest]
        public IEnumerator Snap_UsesUnifiedRate_NoMoveRestRateDiscontinuity()
        {
            BuildRig(snap: true);
            _castaway.snapRate = 40f;
            // Deliberately set the legacy split fields to DIFFERENT values — if the snap still branched on them
            // the ActiveSnapRate would pick one of these, not the unified snapRate.
            _castaway.snapRateRest = 11f;
            _castaway.snapRateMove = 99f;

            for (int i = 0; i < 20; i++) yield return null;

            Assert.That(_castaway.ActiveSnapRate, Is.EqualTo(40f).Within(0.001f),
                $"the snap must apply the UNIFIED snapRate (40), not a move/rest split value (got " +
                $"{_castaway.ActiveSnapRate:F2}). A rate that varies with motion JUMPS at arrival = the bob. " +
                "The legacy snapRateRest/Move (11/99) must be ignored by the live snap.");
        }
    }
}
