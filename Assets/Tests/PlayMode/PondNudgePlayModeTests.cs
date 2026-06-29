using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the POND RECESS live nudge handle (ticket 86cadj4g7 — Sponsor #130 re-soak).
    ///
    /// CATCHES THE BUG CLASS, not the instance. The #100 axe-head DIAL passed unit tests but NO-OPPED at runtime
    /// (data-layer-only / silently stomped). So the load-bearing assertion here is NOT "the step index advanced"
    /// — it is that a recess step actually MOVES the pond ROOT transform Y (the real terrain/collar relationship
    /// the dispatch demands "move the REAL relationship, not just the water-surface Y"). A synthetic pond (a
    /// FreshwaterPond root + a PondWater child) stands in for the baked scene.
    ///
    /// FOAM DIAL DROPPED (#130 third re-soak): the foam step test was removed — foam is now baked OFF on the pond
    /// material unconditionally (no runtime dial; the old Home/End dial was DEAD). The pond's foam-off shipping
    /// is covered by FreshwaterPondSceneTests.Pond_FoamDistance_* + the top-down no-surface-white verify gate.
    /// </summary>
    public class PondNudgePlayModeTests
    {
        private GameObject _pondGo;
        private GameObject _hudGo;
        private PondNudge _nudge;
        private Material _pondMat;
        private const float BaseRootY = 5.0f; // an arbitrary shipped (knee-deep) root Y to re-base off

        [TearDown]
        public void TearDown()
        {
            if (_pondMat != null) Object.Destroy(_pondMat);
            if (_pondGo != null) Object.Destroy(_pondGo);
            if (_hudGo != null) Object.Destroy(_hudGo);
        }

        // Build a synthetic pond: a FreshwaterPond root at BaseRootY with a PondWater child whose MeshRenderer
        // carries a LowPolyWater material (the foam _FoamDistance handle). The PondNudge lives on a separate HUD
        // object (as in the real scene) so it must FIND the pond by scene search (the real resolve path).
        private void BuildSyntheticPond()
        {
            _pondGo = new GameObject("FreshwaterPond");
            _pondGo.AddComponent<FreshwaterPond>();
            _pondGo.transform.position = new Vector3(7f, BaseRootY, -3f);

            var water = new GameObject("PondWater");
            water.transform.SetParent(_pondGo.transform, false);
            var mf = water.AddComponent<MeshFilter>();
            mf.sharedMesh = new Mesh { name = "synthetic_pond_water" };
            var mr = water.AddComponent<MeshRenderer>();
            var sh = Shader.Find("FarHorizon/LowPolyWater");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit"); // harness fallback
            _pondMat = new Material(sh) { name = "synthetic_pond_mat" };
            mr.sharedMaterial = _pondMat;

            _hudGo = new GameObject("Hud");
            _nudge = _hudGo.AddComponent<PondNudge>();
        }

        // (1) THE LOAD-BEARING TEST: a DEEPER recess step actually SINKS the pond root Y (moves the real
        // relationship), and the DEFAULT step lands EXACTLY on the shipped root Y (so a soak that never presses
        // a key — or returns to the default — sees exactly the shipped pond). The #130 mound defect / the #100
        // no-op family is caught here: a step that didn't move the root would green an index-only assert.
        [UnityTest]
        public IEnumerator RecessStep_MovesRealRootY_DeeperSinks_DefaultIsShipped()
        {
            BuildSyntheticPond();
            yield return null; // let Awake run

            float defaultRecess = PondNudge.RecessStepValue[PondNudge.RecessDefaultStep];

            // Force the DEFAULT step — the root must stay at the shipped Y (delta 0).
            _nudge.ForceRecessStep(PondNudge.RecessDefaultStep);
            Assert.AreEqual(BaseRootY, _pondGo.transform.position.y, 1e-3f,
                "the DEFAULT recess step must leave the pond root at the shipped Y (a soak with no key-press " +
                "sees exactly the shipped pond)");

            // Force a DEEPER step — the root must SINK (lower Y) by the recess delta from the default.
            int deeperStep = PondNudge.RecessStepValue.Length - 1; // DEEPER
            float deeperRecess = PondNudge.RecessStepValue[deeperStep];
            _nudge.ForceRecessStep(deeperStep);
            float deeperY = _pondGo.transform.position.y;
            Assert.Less(deeperY, BaseRootY - 0.05f,
                $"a DEEPER recess step must SINK the pond ROOT (y {deeperY:F3} must drop below the shipped " +
                $"{BaseRootY:F3}) — moving the REAL terrain/collar relationship, NOT a no-op (the #100/#130 class)");
            Assert.AreEqual(BaseRootY - (deeperRecess - defaultRecess), deeperY, 1e-3f,
                "the deeper root Y must drop by exactly (deeperRecess − defaultRecess) below the shipped Y");

            // Force a FLUSH (shallow) step — the root must RISE back above the shipped Y (a flush/mound for A/B).
            float flushRecess = PondNudge.RecessStepValue[0];
            _nudge.ForceRecessStep(0);
            float flushY = _pondGo.transform.position.y;
            Assert.Greater(flushY, BaseRootY + 0.05f,
                "a FLUSH recess step must RAISE the pond root above the shipped Y (the rejected mound, for A/B)");
            Assert.AreEqual(BaseRootY - (flushRecess - defaultRecess), flushY, 1e-3f,
                "the flush root Y must rise by exactly (defaultRecess − flushRecess) above the shipped Y");
        }

        // (2) Out-of-range Force* calls are graceful no-ops (return -1, never throw).
        [UnityTest]
        public IEnumerator ForceSteps_OutOfRange_AreGracefulNoOps()
        {
            BuildSyntheticPond();
            yield return null;
            Assert.AreEqual(-1f, _nudge.ForceRecessStep(99), "out-of-range recess step returns -1, no throw");
            Assert.AreEqual(-1f, _nudge.ForceRecessStep(-1), "negative recess step returns -1, no throw");
        }

        // (3) DECONFLICT REGRESSION GUARD — ALL THREE NUDGE-PANEL LEGS (ticket 86cafz9jr broadening of the
        // 86cafjrxk / #187 pond-only test; Sponsor #176: "pond were manipulated because the pg up and down
        // conflicts"). The bug class: the ALWAYS-LIVE pond handle consumed PgUp/PgDn at the SAME time as an
        // F-key NUDGE panel, so dialing that panel's PgUp/PgDn silently also stepped the pond recess. The
        // decision seam protecting the pond is PondNudge.AnyNudgePanelActive() — after the 86cafz9jr refactor
        // it discovers active panels THROUGH the INudgePanel interface (no hard-coded type list), so a 4th
        // panel inherits the guard automatically. #187's test covered the AXE leg only; this closes the
        // coverage to EVERY existing INudgePanel implementer (Axe / WorldLook / CameraFollow): for each, no
        // panel → pond owns the key (false); THAT panel ON → pond yields (true); panel OFF → pond resumes
        // (false). A regression that drops a panel out of the interface-discovered set flips one of these
        // asserts. Layout-agnostic / no Input synth (mirrors the public Activate/IsActive contract).
        [UnityTest]
        public IEnumerator PondYieldsPgUpPgDn_WhileAnyNudgePanelActive_AllThreeLegs()
        {
            BuildSyntheticPond();
            var axeGo = new GameObject("AxeNudgeRig");
            var axeTool = axeGo.AddComponent<AxeNudgeTool>();
            var worldGo = new GameObject("WorldLookNudgeRig");
            var worldTool = worldGo.AddComponent<WorldLookNudgeTool>();
            var camGo = new GameObject("CameraFollowNudgeRig");
            var camTool = camGo.AddComponent<CameraFollowNudgeTool>();
            yield return null; // let Awake run

            try
            {
                // Every panel implements the shared INudgePanel contract (the refactor's named interface) —
                // this is what makes the gate type-list-free. If a panel ever stops implementing it, the
                // interface-discovery scan would silently skip it (re-opening the #176 collision); pin it.
                Assert.IsInstanceOf<INudgePanel>(axeTool, "AxeNudgeTool must implement INudgePanel");
                Assert.IsInstanceOf<INudgePanel>(worldTool, "WorldLookNudgeTool must implement INudgePanel");
                Assert.IsInstanceOf<INudgePanel>(camTool, "CameraFollowNudgeTool must implement INudgePanel");

                // No panel open → the always-live pond handle OWNS PgUp/PgDn (normal pond soak unchanged).
                Assert.IsFalse(axeTool.IsActive, "the axe tool starts inert");
                Assert.IsFalse(worldTool.IsActive, "the world-look tool starts inert");
                Assert.IsFalse(camTool.IsActive, "the camera-follow tool starts inert");
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "with no nudge panel open the pond handle must own PgUp/PgDn (normal soak)");

                // --- LEG 1: AXE panel. Open it → it OWNS PgUp/PgDn; the pond must yield. ---
                axeTool.Activate();
                Assert.IsTrue(axeTool.IsActive, "Activate() turns the axe panel on");
                Assert.IsTrue(PondNudge.AnyNudgePanelActive(),
                    "while the AXE nudge panel is ON the pond handle must yield PgUp/PgDn (#176 collision)");
                axeTool.Deactivate();
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "with the axe panel closed the pond handle owns PgUp/PgDn again");

                // --- LEG 2: WORLD-LOOK panel. Open it → pond must yield. ---
                worldTool.Activate();
                Assert.IsTrue(worldTool.IsActive, "Activate() turns the world-look panel on");
                Assert.IsTrue(PondNudge.AnyNudgePanelActive(),
                    "while the WORLD-LOOK nudge panel is ON the pond handle must yield PgUp/PgDn");
                worldTool.Deactivate();
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "with the world-look panel closed the pond handle owns PgUp/PgDn again");

                // --- LEG 3: CAMERA-FOLLOW panel. Open it → pond must yield. ---
                camTool.Activate();
                Assert.IsTrue(camTool.IsActive, "Activate() turns the camera-follow panel on");
                Assert.IsTrue(PondNudge.AnyNudgePanelActive(),
                    "while the CAMERA-FOLLOW nudge panel is ON the pond handle must yield PgUp/PgDn");
                camTool.Deactivate();
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "with the camera-follow panel closed the pond handle owns PgUp/PgDn again");
            }
            finally
            {
                Object.Destroy(axeGo);
                Object.Destroy(worldGo);
                Object.Destroy(camGo);
            }
        }

        // (4) MUTUAL-EXCLUSION PRESERVED ACROSS THE INTERFACE (ticket 86cafz9jr). The three panels enforce
        // mutual exclusion among themselves (each Activate() deactivates the siblings), so at most ONE is ever
        // active. The interface refactor must NOT regress this — AnyNudgePanelActive() still reports true while
        // the single surviving panel is up. Open all three in sequence; each Activate silences the prior, and
        // the gate stays true throughout (a panel is always the active owner of PgUp/PgDn).
        [UnityTest]
        public IEnumerator NudgePanels_MutualExclusion_Preserved_GateStaysTrue()
        {
            BuildSyntheticPond();
            var axeGo = new GameObject("AxeNudgeRig");
            var axeTool = axeGo.AddComponent<AxeNudgeTool>();
            var worldGo = new GameObject("WorldLookNudgeRig");
            var worldTool = worldGo.AddComponent<WorldLookNudgeTool>();
            var camGo = new GameObject("CameraFollowNudgeRig");
            var camTool = camGo.AddComponent<CameraFollowNudgeTool>();
            yield return null;

            try
            {
                axeTool.Activate();
                worldTool.Activate(); // must silence the axe panel
                Assert.IsFalse(axeTool.IsActive, "opening world-look must deactivate the axe panel (mutual exclusion)");
                Assert.IsTrue(worldTool.IsActive, "the world-look panel is now the active one");
                Assert.IsTrue(PondNudge.AnyNudgePanelActive(),
                    "exactly one panel is active — the gate must still report a panel owns PgUp/PgDn");

                camTool.Activate(); // must silence the world-look panel
                Assert.IsFalse(worldTool.IsActive, "opening camera-follow must deactivate the world-look panel");
                Assert.IsFalse(axeTool.IsActive, "the axe panel stays off");
                Assert.IsTrue(camTool.IsActive, "the camera-follow panel is now the active one");
                Assert.IsTrue(PondNudge.AnyNudgePanelActive(),
                    "still exactly one active panel — the gate stays true");

                // Close the lone survivor → no panel active → pond resumes ownership.
                camTool.Deactivate();
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "all panels closed — the pond handle owns PgUp/PgDn again");
            }
            finally
            {
                Object.Destroy(axeGo);
                Object.Destroy(worldGo);
                Object.Destroy(camGo);
            }
        }
    }
}
