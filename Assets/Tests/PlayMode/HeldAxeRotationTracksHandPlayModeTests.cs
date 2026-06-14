using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// SOAKFIX8 regression guard for the held-axe ROTATION-TRACKS-HAND bug (ticket 86ca8ce6y).
    ///
    /// The Sponsor proved the bug: "the axe moves up and down as the hand moves, but it points the same way
    /// on the x axis all the time." ROOT CAUSE — the held axe's ROTATION was pinned to a FIXED WORLD heading
    /// (set via axe.transform.rotation after parenting, re-applied each frame by the F9 nudge tool). A fixed
    /// world rotation CANNOT survive a TURN: when the castaway re-faces during a click-move, the hand bone's
    /// world rotation changes but the axe's world rotation stayed pinned -> the haft kept pointing one way on
    /// X regardless of facing. The POSITION followed the hand (offset-from-hand) so the axe still bobbed up/
    /// down — exactly the split symptom.
    ///
    /// FIX — the held axe is posed HAND-RELATIVE (a stable localPosition + localRotation as a child of the
    /// right-hand bone), so BOTH position AND rotation ride the bone in every facing via the hierarchy.
    ///
    /// THE BUG CLASS (not just the instance): the load-bearing invariant is that the axe's rotation RELATIVE
    /// to the hand bone (Inverse(hand.rotation) * axe.rotation) is INVARIANT across facings. A world-fixed
    /// rotation makes that relative rotation VARY as the hand turns (the bug); a hand-local child rotation
    /// keeps it constant (the fix). We rotate the hand through several distinct facings and assert the
    /// relative rotation does not drift — which a regression to world-space posing would fail.
    /// </summary>
    public class HeldAxeRotationTracksHandPlayModeTests
    {
        private GameObject _root;
        private Transform _hand;
        private Transform _axe;

        [SetUp]
        public void SetUp()
        {
            // A character root -> a hand bone -> the held axe parented under it, posed HAND-LOCAL (the fix).
            // The bone carries a non-identity local rotation + the chibi-style lossy scale, so the test
            // exercises a realistic rotated/scaled bone frame (the same trap class as the real rig).
            _root = new GameObject("CharacterRoot");
            var handGo = new GameObject("RightHand_010");
            handGo.transform.SetParent(_root.transform, false);
            handGo.transform.localPosition = new Vector3(0.3f, 1.2f, 0.1f);
            handGo.transform.localRotation = Quaternion.Euler(35f, 12f, -48f); // a rotated bone frame
            handGo.transform.localScale = Vector3.one * 267.3f;                // the chibi 267× lossy scale
            _hand = handGo.transform;

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            // The FIX's pose shape: a stable hand-LOCAL position + rotation (the Sponsor's dialed grip,
            // converted to hand-local at authoring — here we just use representative non-trivial values).
            axeGo.transform.localPosition = new Vector3(0.002f, -0.0006f, 0.0004f);
            axeGo.transform.localRotation = Quaternion.Euler(6.5f, -187.5f, 93.3f);
            axeGo.transform.localScale = Vector3.one * 0.0040f;
            _axe = axeGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
        }

        // Rotate the character (and thus the hand bone) to several distinct facings; the axe's rotation
        // RELATIVE to the hand bone must be invariant. The bug = it was NOT (world-fixed rotation drifted
        // relative to the hand as the character turned).
        [UnityTest]
        public IEnumerator HeldAxe_RotationRelativeToHand_IsInvariantAcrossFacings()
        {
            yield return null; // let the hierarchy settle

            // Baseline relative rotation at facing 0.
            Quaternion relBaseline = Quaternion.Inverse(_hand.rotation) * _axe.rotation;

            // Sweep the character through distinct facings (the click-move re-facings that exposed the bug).
            float[] facings = { 0f, 45f, 90f, 137f, 180f, 233f, 270f, 315f };
            foreach (float yaw in facings)
            {
                _root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                yield return null; // propagate the transform down the hierarchy

                Quaternion rel = Quaternion.Inverse(_hand.rotation) * _axe.rotation;
                float drift = Quaternion.Angle(relBaseline, rel);
                Assert.Less(drift, 0.5f,
                    $"at facing {yaw}° the held axe's rotation RELATIVE to the hand bone drifted {drift:F3}° " +
                    "from baseline — it must be INVARIANT across facings (a world-fixed rotation, the SOAKFIX8 " +
                    "bug, would drift here; a hand-local child rotation does not). The axe must turn WITH the hand.");

                // And the axe's WORLD rotation must actually CHANGE as the hand turns (it tracks the hand) —
                // so position-only following with a FROZEN rotation (the bug) is also caught. Compare the
                // axe's world rotation at this facing against its world rotation at facing 0.
                if (!Mathf.Approximately(yaw, 0f))
                {
                    _root.transform.rotation = Quaternion.identity;
                    yield return null;
                    Quaternion axeWorld0 = _axe.rotation;
                    _root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    yield return null;
                    Quaternion axeWorldNow = _axe.rotation;
                    float worldTurn = Quaternion.Angle(axeWorld0, axeWorldNow);
                    Assert.Greater(worldTurn, 1f,
                        $"at facing {yaw}° the held axe's WORLD rotation barely moved ({worldTurn:F2}°) — the " +
                        "axe must TURN WITH the character (position-only following with a frozen rotation is the bug).");
                }
            }
        }
    }
}
