using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86ca9qwvd regression guard for the held-axe POSITION SPACE bug — the axe must stay seated in the hand
    /// from ALL facings.
    ///
    /// THE BUG (Sponsor-proven): the Sponsor dialed the held axe to perfection via the F9 tool, but it only
    /// looked right when the castaway faced ONE direction — turning the character swung the axe out of the hand.
    /// ROOT CAUSE: HeldAxeRig applied the POSITION offset in WORLD space (transform.position = hand.position +
    /// worldOffset). A raw world offset does NOT rotate with the hand, so as the castaway re-faces during a
    /// click-move the offset keeps pointing the SAME world direction while the hand turns away — the axe leaves
    /// the grip. (The ROTATION channel was already hand-relative + correct; only POSITION was wrong.)
    ///
    /// FIX (86ca9qwvd): the offset is applied in HAND-LOCAL space — transform.position = hand.position +
    /// hand.rotation * offset — so the cm offset is rotated by the (stabilized) hand rotation and TRACKS the
    /// hand through every facing. It is rotated by hand.rotation ONLY (never the bone's lossyScale), so the
    /// offset stays in cm and the F9 nudge step stays ~2 cm (NOT hand.TransformPoint — the §FBX lossy trap).
    ///
    /// THE BUG CLASS (not just the instance): the load-bearing invariant is that the held axe's offset
    /// EXPRESSED IN THE HAND'S LOCAL FRAME (hand-local position of the axe pivot) is INVARIANT across facings,
    /// AND the axe's WORLD position stays within a tight tolerance of where it sits at the baseline facing
    /// (re-expressed per-facing). The pre-fix world-fixed offset makes the hand-local position SWING as the
    /// character turns (the axe drifts out of the grip) — this test reds against it. We run the REAL HeldAxeRig
    /// driver (the shipped per-frame code), not a hand-tuned hierarchy, so a regression in the position SPACE
    /// reds here. swingStabilize is OFF so the assertion targets the position-space contract directly, free of
    /// the (separately-tested) walk-swing low-pass.
    /// </summary>
    public class HeldAxeStaysSeatedAcrossFacingsPlayModeTests
    {
        private GameObject _root;
        private Transform _hand;
        private Transform _axe;
        private HeldAxeRig _rig;

        // The same cm-scale offset + rotated bone frame the shipped rig uses (representative non-trivial values).
        private static readonly Vector3 Offset = new Vector3(0.08f, -0.14f, -0.04f);

        [SetUp]
        public void SetUp()
        {
            // Character root -> hand bone -> the held axe driven by the SHIPPED HeldAxeRig. The bone carries a
            // non-identity LOCAL rotation + a large lossy scale, so the test exercises the same rotated/scaled
            // bone frame as the real rig (the §FBX trap class). Rotating the ROOT re-faces the whole rig — the
            // exact click-move re-facing that exposed the bug.
            _root = new GameObject("CharacterRoot");
            var handGo = new GameObject("mixamorig:RightHand");
            handGo.transform.SetParent(_root.transform, false);
            handGo.transform.localPosition = new Vector3(0.3f, 1.2f, 0.1f);
            handGo.transform.localRotation = Quaternion.Euler(35f, 12f, -48f); // a rotated bone frame
            handGo.transform.localScale = Vector3.one * 1.8f;                  // avatar-root scale (Hyper3D rig)
            _hand = handGo.transform;

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            axeGo.transform.localScale = Vector3.one * 0.45f;
            _rig = axeGo.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.worldOffsetFromHand = Offset; // HAND-LOCAL units under the fix
            _rig.relEuler = new Vector3(16f, 2f, -82f);
            _rig.swingStabilize = 0f;          // target the position SPACE directly (no swing low-pass here)
            _axe = axeGo.transform;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        // Sweep the character through distinct facings (the click-move re-facings). The axe pivot expressed in
        // the HAND's LOCAL frame must be INVARIANT — the axe stays seated in the same spot of the grip no matter
        // which way the character turns. The bug = the world-fixed offset made this SWING as the hand turned.
        [UnityTest]
        public IEnumerator HeldAxe_PositionInHandLocalFrame_IsInvariantAcrossFacings()
        {
            yield return null; // let the rig drive the seated pose

            // Baseline: the axe pivot in the hand-local frame at facing 0.
            _root.transform.rotation = Quaternion.identity;
            yield return null;
            Vector3 handLocalBaseline = _hand.InverseTransformPoint(_axe.position);

            float[] facings = { 0f, 45f, 90f, 137f, 180f, 233f, 270f, 315f };
            foreach (float yaw in facings)
            {
                _root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                yield return null; // propagate the transform down the hierarchy + let the rig re-apply

                Vector3 handLocalNow = _hand.InverseTransformPoint(_axe.position);
                float drift = Vector3.Distance(handLocalBaseline, handLocalNow);
                // The hand carries lossyScale 1.8, so the hand-local offset of an axe seated at a 0.16u world
                // offset is ~0.09u; a 0.01u (hand-local) tolerance is tight (~6% of seat distance) yet immune to
                // float noise. The pre-fix world-fixed offset drifts this by ~the full seat magnitude on a turn.
                Assert.Less(drift, 0.01f,
                    $"at facing {yaw}° the held axe's pivot in the HAND-LOCAL frame drifted {drift:F4}u from the " +
                    "baseline — it must be INVARIANT across facings (a world-fixed POSITION offset, the 86ca9qwvd " +
                    "bug, drifts here; a hand-local offset does not). The axe must stay seated in the grip when " +
                    "the character turns.");
            }
        }

        // The same contract in WORLD terms (the ticket's AC3 wording): the axe's WORLD position must stay within
        // tolerance of the SEATED position re-expressed for each facing (hand.position + hand.rotation * offset).
        // With the world-fixed bug the axe sits at hand.position + offset regardless of facing, so it diverges
        // from the correctly-seated position by up to ~2× the offset magnitude on a half-turn — caught here.
        [UnityTest]
        public IEnumerator HeldAxe_WorldPosition_StaysAtTheSeatedGripAcrossFacings()
        {
            yield return null;

            float[] facings = { 0f, 60f, 120f, 180f, 240f, 300f };
            foreach (float yaw in facings)
            {
                _root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                yield return null;

                // Where a correctly-seated axe MUST be at this facing: the hand-local offset rotated into world.
                Vector3 expectedSeated = _hand.position + _hand.rotation * Offset;
                float err = Vector3.Distance(_axe.position, expectedSeated);
                Assert.Less(err, 0.005f,
                    $"at facing {yaw}° the held axe's WORLD position is {err:F4}u from the seated grip " +
                    "(hand.position + hand.rotation * offset) — it must track the hand-rotated seat at every " +
                    "facing. The world-fixed offset (the bug) would diverge by up to ~2× the offset on a turn.");
            }
        }

        // Deliberate-break: force the PRE-FIX world-fixed behavior (offset applied raw, NOT rotated by the hand)
        // and prove the hand-local pivot SWINGS — i.e. this test SUITE actually distinguishes the two spaces (so
        // it can't silently pass on a regression). Mirrors the rig's old line: position = hand.position + offset.
        [UnityTest]
        public IEnumerator WorldFixedOffset_MakesTheHandLocalPivotSwing_ProvingTheTestDiscriminates()
        {
            yield return null;
            _rig.enabled = false; // stop the rig; we drive the OLD world-fixed formula by hand

            _root.transform.rotation = Quaternion.identity;
            yield return null;
            _axe.position = _hand.position + Offset; // OLD bug formula (raw world offset)
            Vector3 handLocalAt0 = _hand.InverseTransformPoint(_axe.position);

            _root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            _axe.position = _hand.position + Offset; // OLD bug formula again, after the half-turn
            Vector3 handLocalAt180 = _hand.InverseTransformPoint(_axe.position);

            float swing = Vector3.Distance(handLocalAt0, handLocalAt180);
            Assert.Greater(swing, 0.02f,
                $"the world-fixed (pre-fix) offset must make the hand-local pivot SWING across a half-turn " +
                $"(got {swing:F4}u) — proving the invariance test above actually discriminates the buggy space " +
                "from the fixed one (else it could pass even with the bug present).");
        }
    }
}
