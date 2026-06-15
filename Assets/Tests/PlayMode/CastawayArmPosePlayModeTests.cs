using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Behavioral regression guard for soak-fixes #2 + #3 — the post-anim arm pose (ticket 86ca8rdkp).
    ///   #3 "his arms are very tight into the body when idle" → relax BOTH arms (move both hands).
    ///   #2 "when the axe is picked up his right hand should be a bit away from the body and raised 10-15°"
    ///      → the RIGHT arm gets an EXTRA away-from-body + raised carry (a bigger offset than the left).
    ///
    /// The pose is an ADDITIVE LateUpdate offset on the upper-arm bones (CastawayArmPose), composed on top of
    /// the imported clip pose (the arms are clip-driven, so this is the only place arm pose can change — a
    /// sibling driver to HeldAxeRig). The exact bone-LOCAL axes were measured empirically (-armTrace, on the
    /// real rig) and the OUTWARD/UP visual direction is judged from the SHIPPED CAPTURE (the authoritative
    /// visual gate — a geometry-space "outward is −X" assert would be a false-confidence proxy that depends on
    /// the rig's arbitrary local frames; unity-conventions.md "guard the percept, not a proxy metric").
    ///
    /// So these tests assert the RIG-INDEPENDENT INVARIANTS the directive reduces to, on a synthetic rig:
    ///   (a) enabling the pose MOVES BOTH hands a bounded non-zero amount (the offset is live; #3 un-pinches
    ///       BOTH arms — a zeroed/dead pose moves nothing and reds here).
    ///   (b) the RIGHT hand moves MORE than the left (the right arm gets the extra carry offset on top of the
    ///       relax — #2). These hold regardless of the rig's local-axis orientation.
    /// </summary>
    public class CastawayArmPosePlayModeTests
    {
        private GameObject _root;
        private Transform _rightArm, _leftArm, _rightHand, _leftHand;
        private CastawayArmPose _pose;

        [SetUp]
        public void SetUp()
        {
            // A torso at origin; two upper-arm bones with hands below them (arms-down rest). The exact local
            // frames are arbitrary — the tests assert rig-independent invariants (hands MOVE; right > left),
            // not a specific world direction (that is the shipped-capture's job).
            _root = new GameObject("Torso");

            var rArmGo = new GameObject("mixamorig:RightArm");
            rArmGo.transform.SetParent(_root.transform, false);
            rArmGo.transform.localPosition = new Vector3(-0.18f, 1.4f, 0f);
            rArmGo.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
            _rightArm = rArmGo.transform;
            var rHandGo = new GameObject("mixamorig:RightHand");
            rHandGo.transform.SetParent(_rightArm, false);
            rHandGo.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            _rightHand = rHandGo.transform;

            var lArmGo = new GameObject("mixamorig:LeftArm");
            lArmGo.transform.SetParent(_root.transform, false);
            lArmGo.transform.localPosition = new Vector3(0.18f, 1.4f, 0f);
            lArmGo.transform.localRotation = Quaternion.Euler(0f, 180f, 5f);
            _leftArm = lArmGo.transform;
            var lHandGo = new GameObject("mixamorig:LeftHand");
            lHandGo.transform.SetParent(_leftArm, false);
            lHandGo.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            _leftHand = lHandGo.transform;

            _pose = _root.AddComponent<CastawayArmPose>();
            _pose.rightUpperArm = _rightArm;
            _pose.leftUpperArm = _leftArm;
            _pose.RebuildCached();
            _pose.enabled = false; // start OFF so we measure the bare-rest hand positions first
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        // (a) #3 — enabling the pose MOVES BOTH hands a bounded non-zero amount (both arms un-pinch), and the
        // deliberate-break (pose off) leaves the rest pose unchanged.
        [UnityTest]
        public IEnumerator ArmPose_MovesBothHands_WhenEnabled_NoMove_WhenDisabled()
        {
            yield return null; // rest (pose disabled)
            Vector3 rRest = _rightHand.position, lRest = _leftHand.position;

            // Disabled: another frame must NOT move the hands (the offset is inert when off).
            yield return null;
            Assert.That(Vector3.Distance(_rightHand.position, rRest), Is.LessThan(1e-4f),
                "with the pose DISABLED the right hand must not move (the offset must be inert when off)");

            _pose.enabled = true;
            yield return null; // LateUpdate applies the offset
            yield return null;
            float rMove = Vector3.Distance(_rightHand.position, rRest);
            float lMove = Vector3.Distance(_leftHand.position, lRest);

            Assert.Greater(lMove, 0.02f,
                $"the LEFT hand must MOVE when the pose is on (moved {lMove:F3}u) — both arms must un-pinch (#3); " +
                "a zeroed/dead pose moves nothing");
            Assert.Greater(rMove, 0.02f,
                $"the RIGHT hand must MOVE when the pose is on (moved {rMove:F3}u) — #3 relax + #2 carry");
            // Bounded: a sane pose nudge, not a wild fling (catches a runaway offset).
            Assert.Less(rMove, 0.6f, $"the right-hand move must be a bounded pose nudge (got {rMove:F3}u)");
        }

        // (b) #2 — the RIGHT hand moves MORE than the left (the carry adds extra spread + raise to the right
        // arm only). Rig-independent: a bigger total offset => a bigger displacement, whatever the axis.
        [UnityTest]
        public IEnumerator ArmPose_RightHandMovesMoreThanLeft_ForTheHeldAxeCarry()
        {
            yield return null;
            Vector3 rRest = _rightHand.position, lRest = _leftHand.position;

            _pose.enabled = true;
            yield return null;
            yield return null;

            float rMove = Vector3.Distance(_rightHand.position, rRest);
            float lMove = Vector3.Distance(_leftHand.position, lRest);
            Assert.Greater(rMove, lMove + 0.01f,
                $"the RIGHT hand must move MORE than the left (right {rMove:F3}u vs left {lMove:F3}u) — the " +
                "held-axe carry (#2) adds extra spread + raise to the right arm on top of the shared relax (#3)");
        }
    }
}
