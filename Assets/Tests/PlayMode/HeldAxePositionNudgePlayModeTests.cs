using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// SOAKFIX9 regression guard for the held-axe POSITION bug (ticket 86ca8ce6y).
    ///
    /// The Sponsor proved the bug with the F9 nudge tool: ONE arrow-left click (a 0.02 position step) flung
    /// the held axe ~5 METRES off-screen. ROOT CAUSE — soakfix8 posed the axe POSITION as a localPosition on
    /// RightHand_010, which carries a ~267× lossyScale (probe-verified, unity-conventions.md §FBX). A 0.02
    /// LOCAL step therefore became ~5.3 WORLD units: the documented lossy-bone-scale trap.
    ///
    /// FIX — HeldAxeRig drives POSITION as a WORLD-space offset from the hand bone
    /// (axe.position = hand.position + worldOffsetFromHand). A nudge moves worldOffsetFromHand in WORLD units,
    /// so a 0.02 step is ~2 cm of WORLD travel — never the 267×-amplified metre jump.
    ///
    /// THE BUG CLASS (not just the instance): the load-bearing contract is that ONE position-nudge step moves
    /// the held axe a SMALL, BOUNDED WORLD distance (≈ the world-unit step, ≤ ~0.05u) — NOT a huge jump. A
    /// regression that pushed POSITION back onto the 267×-scaled bone's localPosition would move the axe
    /// ~5 m for the same step and red here. We drive the actual HeldAxeRig + nudge its world-offset by a step
    /// and assert the axe's WORLD position moved by ~that step (bounded well under any lossy-scale blow-up).
    /// </summary>
    public class HeldAxePositionNudgePlayModeTests
    {
        private GameObject _root;
        private Transform _hand;
        private Transform _axe;
        private HeldAxeRig _rig;

        // The AxeNudgeTool's default position step (kept in sync with AxeNudgeTool.posStep = 0.02f). A single
        // un-multiplied nudge click moves the world-offset by this much.
        private const float NudgeStep = 0.02f;

        [SetUp]
        public void SetUp()
        {
            // A character root -> a 267×-lossy-scaled hand bone -> the held axe driven by the SHIPPED rig.
            // The 267× scale is the trap: a regression that nudged localPosition would amplify by it.
            _root = new GameObject("CharacterRoot");
            var handGo = new GameObject("RightHand_010");
            handGo.transform.SetParent(_root.transform, false);
            handGo.transform.localPosition = new Vector3(0.3f, 1.2f, 0.1f);
            handGo.transform.localRotation = Quaternion.Euler(35f, 12f, -48f);
            handGo.transform.localScale = Vector3.one * 267.3f; // the chibi 267× lossy scale (the trap)
            _hand = handGo.transform;

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            axeGo.transform.localScale = Vector3.one * 0.0040f;
            _rig = axeGo.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.worldOffsetFromHand = new Vector3(0.003f, -0.017f, 0.009f);
            _rig.relEuler = new Vector3(4.1f, 95.8f, -56.1f);
            _axe = axeGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
        }

        // ONE nudge step on the world-offset moves the axe a SMALL bounded WORLD distance (~the step), NOT a
        // ~5 m lossy-scale jump. This is the soakfix9 bug class: a localPosition-on-the-bone regression would
        // move the axe ~step × 267 here and fail.
        [UnityTest]
        public IEnumerator HeldAxe_OneNudgeStep_MovesASmallBoundedWorldDistance()
        {
            yield return null; // let the rig drive the seated pose
            Vector3 worldBefore = _axe.position;

            // One arrow-left-style nudge: move the WORLD offset by one un-multiplied step on X (what the tool
            // does — _heldRig.worldOffsetFromHand += dp).
            _rig.worldOffsetFromHand += new Vector3(NudgeStep, 0f, 0f);
            yield return null; // the rig re-applies position from the new offset
            Vector3 worldAfter = _axe.position;

            float moved = Vector3.Distance(worldBefore, worldAfter);

            // The move must be SMALL + bounded — about one world-unit step (~0.02u), comfortably ≤ 0.05u. The
            // bug moved it ~5 m (the 267× amplification); 0.05u is ~100× below that, so the guard is decisive.
            Assert.LessOrEqual(moved, 0.05f,
                $"one nudge step moved the held axe {moved:F3}u in WORLD space — it must be a SMALL bounded " +
                "move (≤0.05u, ~one 0.02u world step). A move of ~5 m is the soakfix9 lossy-bone-scale " +
                "regression (localPosition on the 267× bone instead of a WORLD offset).");

            // And it must ACTUALLY move (the offset is live, not ignored) — ~the step on X.
            Assert.That(moved, Is.GreaterThan(0.5f * NudgeStep),
                $"the nudge moved the axe only {moved:F4}u — the world-offset must actually drive the position " +
                "(a near-zero move means the rig isn't applying worldOffsetFromHand).");
            Assert.That(moved, Is.EqualTo(NudgeStep).Within(0.005f),
                $"one {NudgeStep:F2}u world-offset step should move the axe ~{NudgeStep:F2}u in world space " +
                $"(got {moved:F4}u) — a 1:1 world-unit nudge, NOT scaled by the bone's lossyScale.");
        }

        // Sanity: across a SWEEP of nudge steps the axe stays in a sane neighbourhood of the hand (never flung
        // metres away). Catches a regression where the step is fine for one click but compounds wrongly.
        [UnityTest]
        public IEnumerator HeldAxe_NudgeSweep_StaysNearTheHand()
        {
            yield return null;
            for (int i = 0; i < 10; i++)
            {
                _rig.worldOffsetFromHand += new Vector3(NudgeStep, 0f, 0f);
                yield return null;
                float fromHand = Vector3.Distance(_axe.position, _hand.position);
                Assert.LessOrEqual(fromHand, 0.6f,
                    $"after {i + 1} nudge steps the held axe sits {fromHand:F3}u from the hand — it must stay " +
                    "seated near the hand (10 × 0.02u ≈ 0.2u of travel). Metres-from-hand = the lossy-scale bug.");
            }
        }
    }
}
