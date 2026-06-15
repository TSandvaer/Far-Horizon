using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// RE-SOAK #3 regression guard for the held-axe SWING STABILIZATION (ticket 86ca8rdkp — "the axe in hand
    /// position changes when I walk around").
    ///
    /// DIAGNOSE-VIA-TRACE: the -walkAxeTrace measured the held axe riding the hand bone's full Walk-clip
    /// arm-swing — ~0.93u peak-to-peak (a ~0.64u vertical bob) — so the axe visibly "changed position" each
    /// step. FIX: HeldAxeRig rides a GRIP ANCHOR held in the BODY-ROOT local frame (which translates + yaws
    /// with the body but does NOT arm-swing), so the per-step swing is removed while body FACING still passes
    /// through (the soakfix8 rotation-tracks-hand contract is preserved). Trace after the fix: 0.93u → 0.18u.
    ///
    /// THE BUG CLASS (not just the instance): the load-bearing invariants are (a) the axe FOLLOW position, in
    /// the body-root frame, has a MUCH smaller peak-to-peak than the raw hand swing (the per-step swing is
    /// damped), AND (b) the axe still TRACKS body facing (a frozen-in-world axe is also wrong). This runs the
    /// REAL HeldAxeRig driver against a synthetic body+hand rig whose hand SWINGS in the body-local frame, so a
    /// regression in the stabilization (or one that re-introduces the world-space-lag bug) reds here.
    /// </summary>
    public class HeldAxeSwingStabilizePlayModeTests
    {
        private GameObject _body;        // the body root (the stabilizeFrame) — translates, does not arm-swing
        private Transform _hand;         // the hand bone — SWINGS in the body-local frame (the Walk arm-swing)
        private Transform _axe;
        private HeldAxeRig _rig;

        [SetUp]
        public void SetUp()
        {
            // Body root -> hand bone -> axe. The hand SWINGS relative to the body (the test drives it); the
            // body itself only translates (locomotion) + yaws (facing). This is the exact geometry the fix
            // targets: stabilize the swing in the body frame, pass facing through.
            _body = new GameObject("BodyRoot");
            var handGo = new GameObject("mixamorig:RightHand");
            handGo.transform.SetParent(_body.transform, false);
            handGo.transform.localPosition = new Vector3(0.3f, 1.2f, 0.1f);
            _hand = handGo.transform;

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            _rig = axeGo.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.stabilizeFrame = _body.transform;     // stabilize in the body frame (the fix)
            _rig.worldOffsetFromHand = new Vector3(0.08f, -0.14f, -0.04f);
            _rig.relEuler = new Vector3(16f, 2f, -82f);
            _rig.swingStabilize = 1f;
            _rig.anchorTrackPerSec = 0.12f;
            _axe = axeGo.transform;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_body);

        // Drive the hand bone through a sustained body-local SWING (the Walk arm-swing) and measure the axe's
        // FOLLOW position peak-to-peak in the BODY-LOCAL frame. With stabilization ON it must be a small
        // fraction of the raw hand's body-local swing — the per-step swing is removed.
        [UnityTest]
        public IEnumerator StabilizedAxe_BodyLocalSwing_IsMuchSmallerThanTheRawHandSwing()
        {
            // Let the anchor seat at the rest pose first.
            for (int i = 0; i < 30; i++) yield return null;

            Vector3 handMin = Vector3.positiveInfinity, handMax = Vector3.negativeInfinity;
            Vector3 follMin = Vector3.positiveInfinity, follMax = Vector3.negativeInfinity;
            // Swing the hand ±0.45u vertically + ±0.2u fore/aft in the body-local frame over several cycles.
            for (int f = 0; f < 120; f++)
            {
                float ph = f * 0.20f;
                _hand.localPosition = new Vector3(0.3f, 1.2f + 0.45f * Mathf.Sin(ph), 0.1f + 0.2f * Mathf.Cos(ph));
                yield return null; // HeldAxeRig.LateUpdate(100) sets FollowPos this frame
                Vector3 handBL = _body.transform.InverseTransformPoint(_hand.position);
                Vector3 follBL = _body.transform.InverseTransformPoint(_rig.FollowPos);
                handMin = Vector3.Min(handMin, handBL); handMax = Vector3.Max(handMax, handBL);
                follMin = Vector3.Min(follMin, follBL); follMax = Vector3.Max(follMax, follBL);
            }
            float handPP = (handMax - handMin).magnitude;
            float follPP = (follMax - follMin).magnitude;

            Assert.Greater(handPP, 0.5f, $"the synthetic hand must actually swing (got {handPP:F3}u) — the test setup");
            // The stabilized follow must be a SMALL fraction of the raw swing (the per-step swing is removed).
            Assert.Less(follPP, handPP * 0.5f,
                $"the stabilized axe follow-pos swings {follPP:F3}u in the body frame vs the raw hand's {handPP:F3}u " +
                "— it must be MUCH smaller (≤50%); the axe must read as a steady grip, not ride the full arm-swing " +
                "(re-soak #3). A follow-pos ~= the hand swing means the stabilization regressed.");
        }

        // The deliberate-break: with swingStabilize = 0 the axe follows the RAW hand swing (the pre-re-soak
        // bug), proving the stabilization is what steadies it.
        [UnityTest]
        public IEnumerator SwingStabilizeOff_AxeFollowsTheRawHandSwing_ProvingItIsLoadBearing()
        {
            _rig.swingStabilize = 0f;
            for (int i = 0; i < 5; i++) yield return null;

            Vector3 handMin = Vector3.positiveInfinity, handMax = Vector3.negativeInfinity;
            Vector3 follMin = Vector3.positiveInfinity, follMax = Vector3.negativeInfinity;
            for (int f = 0; f < 120; f++)
            {
                float ph = f * 0.20f;
                _hand.localPosition = new Vector3(0.3f, 1.2f + 0.45f * Mathf.Sin(ph), 0.1f + 0.2f * Mathf.Cos(ph));
                yield return null;
                Vector3 handBL = _body.transform.InverseTransformPoint(_hand.position);
                Vector3 follBL = _body.transform.InverseTransformPoint(_rig.FollowPos);
                handMin = Vector3.Min(handMin, handBL); handMax = Vector3.Max(handMax, handBL);
                follMin = Vector3.Min(follMin, follBL); follMax = Vector3.Max(follMax, follBL);
            }
            float handPP = (handMax - handMin).magnitude;
            float follPP = (follMax - follMin).magnitude;
            Assert.That(follPP, Is.EqualTo(handPP).Within(0.02f),
                $"with stabilization OFF the axe follow-pos ({follPP:F3}u) must equal the raw hand swing " +
                $"({handPP:F3}u) — the pre-re-soak behavior (proves the stabilization is what steadies it).");
        }

        // The soakfix8 contract must SURVIVE the stabilization: the axe still TURNS with body facing. Rotate
        // the body to distinct yaws (idle — no arm-swing) and assert the axe's world rotation tracks the body.
        [UnityTest]
        public IEnumerator StabilizedAxe_StillTurnsWithBodyFacing_Soakfix8ContractPreserved()
        {
            for (int i = 0; i < 30; i++) yield return null; // anchor seats

            _body.transform.rotation = Quaternion.identity;
            yield return null;
            Quaternion axeAt0 = _axe.rotation;

            _body.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            for (int i = 0; i < 30; i++) yield return null; // let the anchor frame re-orient
            Quaternion axeAt90 = _axe.rotation;

            float turn = Quaternion.Angle(axeAt0, axeAt90);
            Assert.Greater(turn, 45f,
                $"the held axe's world rotation barely moved ({turn:F1}°) when the body yawed 90° — the axe must " +
                "still TURN WITH the body (the soakfix8 rotation-tracks-hand contract must survive the swing " +
                "stabilization — the anchor is body-LOCAL, so body facing passes through).");
        }
    }
}
