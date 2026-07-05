using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the LIVE FLOAT-DIAGNOSTIC instrument (ticket 86ca8rdkp — "we have been chasing
    /// this floating issue for many iterations, you have to add logging or nudging"). The instrument makes the
    /// castaway feet-vs-sand float MEASURABLE on-screen + dialable to zero. These guards pin its load-bearing
    /// contracts:
    ///
    ///   1. THE GAP MATH (the deliverable's core): feet world-Y − ground hit-Y, asserted against a KNOWN setup
    ///      so a regression in the measurement (the thing the Sponsor dials to ~0) reds in CI. Tested both as
    ///      the pure static (CastawayCharacter.ComputeFloatGap) AND end-to-end on a real snap rig (the readouts
    ///      FeetWorldY / GroundHitWorldY / FloatGap that the overlay + F9 panel + log all read).
    ///   2. BUILD-GATED / INERT BY DEFAULT (the hard requirement every dial tool follows): the overlay does NOT
    ///      draw until F8 (here: ShowOverlay, since the harness can't synthesize a key-down). A normal soak
    ///      never sees it.
    ///   3. PANEL PLACEMENT: the overlay is LEFT-anchored + on-screen across sizes, and clears the RIGHT-
    ///      anchored F9 AxeNudgeTool panel so BOTH can be up at once (F8 readout + F9 dialing).
    ///
    /// The GAP-math guard is the SILENT-KILLER catch: a "pickup_count > 0"-style proxy ("the snap ran") passed
    /// during the entire walk-float era. THIS asserts the actual feet−ground delta — the number the percept
    /// ("is he floating?") reduces to — so a wrong measurement can't ship green.
    /// </summary>
    public class FloatDiagnosticPlayModeTests
    {
        // ---- 1. THE GAP MATH ---------------------------------------------------------------------------

        // Pure static: feet−ground over known inputs. Floating (feet above), planted (equal), sunk (below).
        [Test]
        public void ComputeFloatGap_IsFeetMinusGround_OverKnownInputs()
        {
            Assert.That(CastawayCharacter.ComputeFloatGap(0.081f, 0.020f), Is.EqualTo(0.061f).Within(1e-5f),
                "the measured -groundTrace case (feet 0.081, sand 0.020) must yield the 6.1cm float gap");
            Assert.That(CastawayCharacter.ComputeFloatGap(0.5f, 0.5f), Is.EqualTo(0f).Within(1e-6f),
                "feet ON the ground must read GAP 0 (planted)");
            Assert.That(CastawayCharacter.ComputeFloatGap(-0.40f, -0.20f), Is.EqualTo(-0.20f).Within(1e-5f),
                "feet BELOW the ground must read a NEGATIVE gap (sunk) — the math is signed feet−ground");
        }

        // ---- ISOLATION (the cross-fixture Ground-collider leak fix the ground-snap fixture documents) ----
        [UnitySetUp]
        public IEnumerator IsolateScene()
        {
            yield return PlayModeSceneIsolation.IsolateInFreshScene("FloatDiagIsolated");
            yield return null;
        }

        private GameObject _terrain, _playerGo, _avatarGo, _bootGo;
        private CastawayCharacter _castaway;

        [TearDown]
        public void TearDown()
        {
            if (_terrain != null) { Object.DestroyImmediate(_terrain); _terrain = null; }
            if (_playerGo != null) { Object.DestroyImmediate(_playerGo); _playerGo = null; }
            if (_bootGo != null) { Object.DestroyImmediate(_bootGo); _bootGo = null; }
        }

        // The visible-terrain top sits at this Y; the player root rides ABOVE it (the agent's NavMesh ground).
        private const float TerrainY = 0.10f;
        private const float RootY = 0.60f;

        // Build a snap rig: a renderer-ENABLED Ground collider at TerrainY, a player root parked at RootY, a
        // CastawayCharacter avatar child. The snap drives the feet onto the visible terrain.
        private void BuildSnapRig()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
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
            _terrain.AddComponent<MeshRenderer>().enabled = true;   // VISIBLE — the surface the player sees
            _terrain.AddComponent<MeshCollider>().sharedMesh = mesh;

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, RootY, 0f);
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
        }

        // End-to-end: after the snap settles with offset 0, the GAP the overlay/log/F9-panel read must be ~0
        // (the feet planted on the visible ground). This is the number that says "fixed" — it MUST be the
        // measured feet−ground, not a proxy. Also asserts the raw readouts (feet, ground) the GAP derives from.
        [UnityTest]
        public IEnumerator FloatGapReadout_IsZero_WhenFeetPlantedOnVisibleGround()
        {
            BuildSnapRig();
            _castaway.groundYOffset = 0f;
            for (int i = 0; i < 40; i++) yield return null;

            Assert.That(_castaway.GroundHitWorldY, Is.EqualTo(TerrainY).Within(0.02f),
                $"the diagnostic ground-hit readout must be the VISIBLE terrain Y (≈{TerrainY}); got " +
                $"{_castaway.GroundHitWorldY:F3}");
            Assert.That(_castaway.FeetWorldY, Is.EqualTo(TerrainY).Within(0.03f),
                $"the feet readout must be on the terrain after the snap (≈{TerrainY}); got {_castaway.FeetWorldY:F3}");
            Assert.That(_castaway.FloatGap, Is.EqualTo(0f).Within(0.03f),
                $"with the feet planted, the GAP (feet−ground) the Sponsor dials to 0 must read ~0; got " +
                $"{_castaway.FloatGap:F3}. A non-zero gap here = the instrument reports a float that isn't there " +
                "(or vice-versa) — the measurement the whole 'is it fixed' decision rests on.");
        }

        // The GAP must REFLECT a real float: with a dialed-in groundYOffset the feet lift OFF the visible
        // ground, so the GAP must read ~the offset (NOT stay 0). This proves the readout MEASURES the actual
        // feet-vs-sand delta — the deliberate-break direction of the silent-killer guard (a GAP stuck at 0
        // regardless of the real float would pass a naive "snap ran" proxy but is exactly the bug the
        // instrument exists to expose).
        [UnityTest]
        public IEnumerator FloatGapReadout_ReportsTheActualFloat_WhenFeetLiftedOffTheGround()
        {
            BuildSnapRig();
            const float off = 0.15f;
            _castaway.groundYOffset = off;          // lift the feet 15cm OFF the visible sand
            for (int i = 0; i < 40; i++) yield return null;

            // Ground hit is unchanged (the raw visible surface); the feet rode up by the offset.
            Assert.That(_castaway.GroundHitWorldY, Is.EqualTo(TerrainY).Within(0.02f),
                "the ground-hit readout measures the RAW visible surface (pre-offset) — unchanged by the dial");
            Assert.That(_castaway.FloatGap, Is.EqualTo(off).Within(0.03f),
                $"a {off} ground-Y offset lifts the feet {off} OFF the sand — the GAP must REPORT that float " +
                $"(≈{off}), not stay 0. Got {_castaway.FloatGap:F3}. A GAP that ignores the real float is the " +
                "silent-killer the instrument must NOT have (it would 'prove fixed' while he floats).");
        }

        // No visible ground under the feet → the GAP reads NaN (the overlay shows N/A), not a stale/garbage
        // number. The snap goes inert; the readouts must signal "no measurement", never a lie.
        [UnityTest]
        public IEnumerator FloatGapReadout_IsNaN_WhenNoVisibleGroundUnderTheFeet()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 5f, 0f); // floating in the void — no Ground below
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");

            for (int i = 0; i < 10; i++) yield return null;

            Assert.IsTrue(float.IsNaN(_castaway.GroundHitWorldY),
                "with no visible Ground under the feet, the ground-hit readout must be NaN (no measurement)");
            Assert.IsTrue(float.IsNaN(_castaway.FloatGap),
                "with no ground measurement the GAP must be NaN (overlay shows N/A) — never a stale/garbage gap");
        }

        // ---- 2. BUILD-GATED / INERT BY DEFAULT ---------------------------------------------------------

        // The overlay must NOT be active by default — a normal soak never sees it until F8 (ShowOverlay).
        [UnityTest]
        public IEnumerator FloatDiagnostic_InertByDefault_OverlayOff_UntilShown()
        {
            _bootGo = new GameObject("Boot");
            var diag = _bootGo.AddComponent<FloatDiagnostic>();
            for (int i = 0; i < 10; i++) yield return null; // normal play, no F8 synthesized

            Assert.IsFalse(diag.OverlayActive,
                "the float-diagnostic overlay must be INERT (off) in normal play — a soak must not see it " +
                "until the Sponsor presses F8 (the build-gated contract every dial tool follows)");

            diag.ShowOverlay();
            Assert.IsTrue(diag.OverlayActive,
                "ShowOverlay (the -verifyFloatDiag / F8 path) must flip the overlay ON so the capture renders " +
                "the live GAP into the frame");
        }

        // ---- 3. PANEL PLACEMENT ------------------------------------------------------------------------

        // The overlay panel stays on-screen across sizes AND clears the RIGHT-anchored F9 nudge panel, so both
        // can be up at once (F8 readout + F9 dialing — the "dial + measurement together" workflow).
        [Test]
        public void FloatDiagnosticPanel_OnScreen_AndClearsTheF9NudgePanel_AcrossSizes()
        {
            var sizes = new (float w, float h)[]
            {
                (1920f, 1080f), (1600f, 900f), (1280f, 720f), (1024f, 768f), (800f, 600f),
            };
            foreach (var (w, h) in sizes)
            {
                Rect floatPanel = FloatDiagnostic.PanelRect(w, h);
                Rect nudgePanel = AxeNudgeTool.PanelRect(w, h);

                Assert.GreaterOrEqual(floatPanel.x, 0f, $"at {w}x{h} the float panel must stay on the left edge");
                Assert.LessOrEqual(floatPanel.xMax, w, $"at {w}x{h} the float panel must not run off the right edge");
                Assert.GreaterOrEqual(floatPanel.y, 0f, $"at {w}x{h} the float panel must stay below the top edge");
                Assert.LessOrEqual(floatPanel.yMax, h, $"at {w}x{h} the float panel must stay above the bottom edge");

                // The F8 overlay (LEFT) and the F9 nudge panel (RIGHT) must NOT overlap — both up at once —
                // WHENEVER the window is wide enough to physically hold both side-by-side (overlay 360 + nudge
                // 532 + margins). On a window too narrow for both (e.g. 800px < 360+532), they CANNOT coexist
                // side-by-side by geometry — the real soak runs borderless-fullscreen at desktop width, where
                // they clear. We assert the contract where it's physically satisfiable (the soak case) and skip
                // it only on a sub-side-by-side window (still asserting on-screen above).
                if (w >= FloatDiagnostic.PanelWidth + AxeNudgeTool.PanelWidth + 48f)
                    Assert.IsFalse(floatPanel.Overlaps(nudgePanel),
                        $"at {w}x{h} the float-diagnostic overlay {floatPanel} must NOT overlap the F9 nudge " +
                        $"panel {nudgePanel} — both are up together when the Sponsor dials GROUND-Y while " +
                        "watching the GAP (the dial+measurement-together workflow)");
            }
        }
    }
}
