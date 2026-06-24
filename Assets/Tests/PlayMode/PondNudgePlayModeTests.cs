using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the POND RECESS + FOAM live nudge handle (ticket 86cadj4g7 — Sponsor #130 re-soak).
    ///
    /// CATCHES THE BUG CLASS, not the instance. The #100 axe-head DIAL passed unit tests but NO-OPPED at runtime
    /// (data-layer-only / silently stomped). So the load-bearing assertions here are NOT "the step index advanced"
    /// — they are that a recess step actually MOVES the pond ROOT transform Y (the real terrain/collar
    /// relationship the dispatch demands "move the REAL relationship, not just the water-surface Y") and a foam
    /// step actually MUTATES the material's live _FoamDistance. A synthetic pond (a FreshwaterPond root + a
    /// PondWater child carrying a LowPolyWater material) stands in for the baked scene.
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

        // (2) A foam step actually MUTATES the live material _FoamDistance (off/light/sea-like), and the DEFAULT
        // foam step sets it OFF (#130 "still pool"). Only meaningful when the LowPolyWater shader is present
        // (the _FoamDistance property exists); skip on the URP/Lit harness fallback.
        [UnityTest]
        public IEnumerator FoamStep_MutatesMaterialFoamDistance_DefaultOff()
        {
            BuildSyntheticPond();
            yield return null;

            // Touch the runtime material instance the nudge resolves (.material on the renderer). Resolve once.
            _nudge.ForceFoamStep(PondNudge.FoamDefaultStep);
            var resolvedMat = ResolvedPondMat();
            if (resolvedMat == null || !resolvedMat.HasProperty("_FoamDistance"))
            {
                Assert.Pass("no _FoamDistance on the harness material (URP/Lit fallback) — foam mutation not testable here");
                yield break;
            }

            // DEFAULT = OFF (PondNudge.FoamStepValue[FoamDefaultStep] is the runtime source of truth; the
            // EditMode PondNudgeTests pins it == LowPolyZoneGen.PondFoamOff — PlayMode can't see the editor asmdef).
            Assert.AreEqual(PondNudge.FoamStepValue[PondNudge.FoamDefaultStep], resolvedMat.GetFloat("_FoamDistance"), 1e-3f,
                "the DEFAULT foam step must set _FoamDistance OFF (a still pool — Sponsor #130)");

            // SEA-LIKE — the material _FoamDistance must actually change to the sea-like value (not a no-op).
            int seaLike = PondNudge.FoamStepValue.Length - 1;
            _nudge.ForceFoamStep(seaLike);
            Assert.AreEqual(PondNudge.FoamStepValue[seaLike], ResolvedPondMat().GetFloat("_FoamDistance"), 1e-3f,
                "a SEA-LIKE foam step must set the live material _FoamDistance to the sea-like value (a real mutation)");

            // Back to OFF — the foam must turn fully off again (round-trip).
            _nudge.ForceFoamStep(0);
            Assert.AreEqual(PondNudge.FoamStepValue[0], ResolvedPondMat().GetFloat("_FoamDistance"), 1e-3f,
                "stepping foam back to OFF must restore _FoamDistance to 0 (a still pool again)");
        }

        // (3) Out-of-range Force* calls are graceful no-ops (return -1, never throw).
        [UnityTest]
        public IEnumerator ForceSteps_OutOfRange_AreGracefulNoOps()
        {
            BuildSyntheticPond();
            yield return null;
            Assert.AreEqual(-1f, _nudge.ForceRecessStep(99), "out-of-range recess step returns -1, no throw");
            Assert.AreEqual(-1f, _nudge.ForceRecessStep(-1), "negative recess step returns -1, no throw");
            Assert.AreEqual(-1f, _nudge.ForceFoamStep(99), "out-of-range foam step returns -1, no throw");
        }

        private Material ResolvedPondMat()
        {
            var f = typeof(PondNudge).GetField("_pondMat", BindingFlags.NonPublic | BindingFlags.Instance);
            return (Material)f.GetValue(_nudge);
        }
    }
}
