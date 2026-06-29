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

        // (3) DECONFLICT REGRESSION GUARD (ticket 86cafjrxk — Sponsor #176: "pond were manipulated because the
        // pg up and down conflicts"). The bug class: the ALWAYS-LIVE pond handle consumed PgUp/PgDn at the SAME
        // time as an F-key WEAPON-NUDGE panel, so dialing the weapon's Y silently also stepped the pond recess.
        // The decision seam protecting the pond is PondNudge.AnyNudgePanelActive() — when ANY of the three F-key
        // nudge tools is toggled ON it returns true, and Update() yields PgUp/PgDn instead of stepping. This
        // tests the seam end-to-end with a REAL AxeNudgeTool: no panel → pond owns the key (false); panel ON →
        // pond yields (true); panel OFF again → pond resumes (false). A regression that drops the gate (the pond
        // listening through an open weapon panel) flips one of these asserts. Layout-agnostic / no Input synth.
        [UnityTest]
        public IEnumerator PondYieldsPgUpPgDn_WhileAnyNudgePanelActive()
        {
            BuildSyntheticPond();
            var toolGo = new GameObject("AxeNudgeRig");
            var axeTool = toolGo.AddComponent<AxeNudgeTool>();
            yield return null; // let Awake run

            try
            {
                // No F-key panel open → the always-live pond handle OWNS PgUp/PgDn (normal pond soak unchanged).
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "with no nudge panel open the pond handle must own PgUp/PgDn (normal soak)");
                Assert.IsFalse(axeTool.IsActive, "the axe tool starts inert");

                // Open the weapon-nudge panel (the F9 path) → it now OWNS PgUp/PgDn; the pond must yield.
                axeTool.Activate();
                Assert.IsTrue(axeTool.IsActive, "Activate() turns the axe panel on");
                Assert.IsTrue(PondNudge.AnyNudgePanelActive(),
                    "while a weapon-nudge panel is ON, PgUp/PgDn belongs to it — the pond handle must yield " +
                    "(the #176 collision: dialing the weapon Y also stepped the pond recess)");

                // Close the panel → the pond handle resumes owning PgUp/PgDn for a plain pond soak.
                axeTool.Deactivate();
                Assert.IsFalse(axeTool.IsActive, "Deactivate() turns the axe panel off");
                Assert.IsFalse(PondNudge.AnyNudgePanelActive(),
                    "with the panel closed the pond handle owns PgUp/PgDn again");
            }
            finally
            {
                Object.Destroy(toolGo);
            }
        }
    }
}
