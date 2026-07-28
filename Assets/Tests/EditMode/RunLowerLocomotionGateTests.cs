using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86caxj30g — the RUN-LOWER LOCOMOTION-LANE GATE.
    ///
    /// THE DEFECT (measured, not assumed — 86caxgwbz / team/drew-dev/armpose-offset-fit-86caxgwbz.md §4):
    /// CastawayArmPose's run-lower was weighted by <see cref="CastawayCharacter.IsRunning"/> alone, a VELOCITY read
    /// blind to which Animator state poses the arm. The five per-class attack swings fire with NO locomotion gate
    /// anywhere on their trigger path, so a strike thrown while sprinting inherited the full composed ~47.6 deg /
    /// up to 0.896 shoulder-width hand displacement — 2.15x-3.64x the always-on carry offsets' worst case, on an
    /// authored + soak-approved strike silhouette.
    ///
    /// WHAT THIS FILE PINS — three separable contracts, each a bug CLASS:
    ///   1. THE ALLOW-LIST: exactly the four locomotion-lane states, verified against the state set the SHIPPED
    ///      controller actually authors (so a state rename in CharacterAssetGen reds here instead of silently
    ///      un-gating or over-gating the run-lower).
    ///   2. THE TRANSITION PAIRING: GetCurrentAnimatorStateInfo(0) reports the state being transitioned FROM for
    ///      the whole crossfade, and every swing is reached by AnyState->AttackX. A current-state-only gate is
    ///      therefore still "in-lane" through the entry crossfade. MEASURED on the live shipped controller
    ///      (ArmPoseOffsetFitDiag.TraceLocomotionGate): frames t=0.017..0.067 report
    ///      current=Locomotion / next=AttackAxe / inTransition=YES. These cases pin the paired verdict.
    ///   3. THE EASE PROFILE, TIME-QUALIFIED: the AC as first drafted ("arc back in the Pass-1 band while an attack
    ///      state is active") is unachievable at swing ENTRY at any finite blend rate. The achievable contract is
    ///      an asymmetric release that is spent inside the swing's own opening: the run-lower must be a negligible
    ///      residual by 0.15 s after the gate closes (14% into the FASTEST ~1.05 s swing), while a plain sprint
    ///      still reaches full engagement at the LOCKED 86caa83wn rate.
    ///
    /// The ease tests drive <see cref="CastawayArmPose.NextRunWeight"/> — the PRODUCTION function LateUpdate calls
    /// — not a mirrored copy, so they cannot go tautologically green against a re-implementation.
    /// </summary>
    public class RunLowerLocomotionGateTests
    {
        // The composed ceilings this fix moves between, from the 86caxgwbz study (both independently reproduced by
        // the reviewer): the always-on carry alone, and carry x run-lower at full weight.
        private static readonly Vector3 CarryEuler = MovementCameraScene.CastawayV4RightArmEuler;   // (-5,-22,0)
        private static readonly Vector3 RunLowerEuler = MovementCameraScene.ArmRunLowerEuler;       // (-10,12,-42)

        private const float InLaneRate = 8f;    // CastawayArmPose.runLowerBlendRate (shipped, 86caa83wn)
        private const float ReleaseRate = 30f;  // CastawayArmPose.runLowerOverlayReleaseRate (86caxj30g)
        private const float Dt = 1f / 60f;

        // ==============================================================================================
        // 1 — THE ALLOW-LIST, checked against the state set the SHIPPED controller authors.
        // ==============================================================================================

        [Test]
        public void LaneAllowList_IsExactlyTheFourLocomotionStates_AndEveryOtherShippedStateIsOut()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the shipped controller must exist @ " + CharacterAssetGen.ControllerPath);

            var authored = new List<string>();
            foreach (var child in controller.layers[0].stateMachine.states) authored.Add(child.state.name);
            CollectionAssert.IsNotEmpty(authored, "layer 0 must author states");

            var lane = new[]
            {
                CastawayCharacter.IdleState, CastawayCharacter.LocomotionState,
                CastawayCharacter.JumpIdleState, CastawayCharacter.JumpRunningState,
            };

            // (a) every lane name must actually EXIST in the shipped controller — a rename in CharacterAssetGen
            //     would otherwise leave the gate matching nothing and permanently closed (the run-lower would
            //     silently stop engaging at all, regressing 86caa83wn's "axe into the head" fix).
            foreach (var n in lane)
                CollectionAssert.Contains(authored, n,
                    $"the run-lower lane allow-list names '{n}', which the shipped controller no longer authors — " +
                    "CastawayCharacter's lane constants and CharacterAssetGen's state names have drifted apart.");

            // (b) the predicate must accept exactly those four and reject EVERY other authored state — the swings,
            //     the crouch lane, the hit-reacts, stunned/getting-up/picking-up, the reserved overhead Attack.
            foreach (var name in authored)
            {
                bool expected = System.Array.IndexOf(lane, name) >= 0;
                Assert.AreEqual(expected, CastawayCharacter.IsLocomotionLaneState(name),
                    $"state '{name}' is classified wrong by IsLocomotionLaneState. The lane is the upright " +
                    "Idle/Locomotion/JumpIdle/JumpRunning family the run-lower was dialed against; every one-shot " +
                    "overlay authors its own arm pose and must get the arm handed back to the clip.");
            }
        }

        [Test]
        public void LaneHashForm_AgreesWithTheNameForm_ForEveryShippedState()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller);
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                string name = child.state.name;
                Assert.AreEqual(CastawayCharacter.IsLocomotionLaneState(name),
                                CastawayCharacter.IsLocomotionLaneState(Animator.StringToHash(name)),
                    $"the hash form of the lane predicate must agree with the name form for '{name}' — the runtime " +
                    "reads AnimatorStateInfo.shortNameHash, the tests + the instrument read names.");
            }
        }

        // ==============================================================================================
        // 2 — TRANSITION PAIRING. The AnyState->AttackX entry crossfade is the case a current-state-only
        //     gate gets WRONG; the in-lane Idle<->Locomotion crossfade is the case an over-eager gate
        //     would get wrong in the other direction (a spurious run-lower drop mid-run).
        // ==============================================================================================

        private static int H(string s) => Animator.StringToHash(s);

        [Test]
        public void Gate_ClosesOnTheFIRSTFrameOfTheCrossfadeIntoASwing_NotAfterItCompletes()
        {
            // MEASURED shape (ArmPoseOffsetFitDiag.TraceLocomotionGate on the shipped controller): for the whole
            // 0.06 s AnyState->AttackAxe crossfade the layer reports current=Locomotion, next=AttackAxe.
            Assert.IsFalse(
                CastawayCharacter.LocomotionLaneOwnsPoseFor(H(CastawayCharacter.LocomotionState), true, H("AttackAxe")),
                "during the crossfade INTO a swing the gate must already be CLOSED. GetCurrentAnimatorStateInfo " +
                "still reports the from-state (Locomotion) for the whole transition, so a current-only check would " +
                "hold the run-lower engaged through the entire entry crossfade and start releasing only once the " +
                "swing is already posing the arm.");

            // A current-only read genuinely says 'in lane' here — this is the trap, asserted so it can't be
            // "simplified" back into the production gate.
            Assert.IsTrue(CastawayCharacter.IsLocomotionLaneState(H(CastawayCharacter.LocomotionState)),
                "sanity: the from-state IS a lane state — which is exactly why the pairing is load-bearing.");
        }

        [Test]
        public void Gate_StaysClosedThroughTheSwingAndItsReturnCrossfade()
        {
            Assert.IsFalse(CastawayCharacter.LocomotionLaneOwnsPoseFor(H("AttackAxe"), false, 0),
                "while the swing state owns the pose the gate must be closed.");
            Assert.IsFalse(
                CastawayCharacter.LocomotionLaneOwnsPoseFor(H("AttackAxe"), true, H(CastawayCharacter.LocomotionState)),
                "on the way BACK out of a swing the gate must stay closed until the crossfade settles — the tail " +
                "of the strike is still posing the arm, so re-engaging the run-lower there would put the defect " +
                "back on the last frames of the swing.");
        }

        [Test]
        public void Gate_StaysOPEN_AcrossInLaneTransitions_NoSpuriousRunLowerDrop()
        {
            // Idle<->Locomotion crossfades run 0.22 s / 0.30 s and fire on every start and stop. If the gate closed
            // on ANY transition, a plain accelerate-into-a-run would drop the run-lower for a third of a second
            // right when it is needed — a 86caa83wn regression.
            Assert.IsTrue(CastawayCharacter.LocomotionLaneOwnsPoseFor(
                H(CastawayCharacter.IdleState), true, H(CastawayCharacter.LocomotionState)),
                "Idle->Locomotion is an IN-LANE transition; the gate must stay open.");
            Assert.IsTrue(CastawayCharacter.LocomotionLaneOwnsPoseFor(
                H(CastawayCharacter.LocomotionState), true, H(CastawayCharacter.JumpRunningState)),
                "Locomotion->JumpRunning is in-lane (the run-jump shares the run arm pose the dial was tuned on — " +
                "the run-jump axe coda).");
            Assert.IsTrue(CastawayCharacter.LocomotionLaneOwnsPoseFor(H(CastawayCharacter.LocomotionState), false, 0),
                "a settled Locomotion state must own the pose.");
        }

        [Test]
        public void Gate_ClosesForEveryOverlayFamily_NotJustTheAxeSwing()
        {
            foreach (var overlay in new[]
            {
                "Attack", "AttackAxe", "AttackPickaxe", "AttackDagger", "AttackSpear", "AttackSword",
                "CrouchIdle", "CrouchWalk", "Stunned", "GettingUp", "PickingUp",
                "HitToBody", "HeadHit", "BigStomachHit", "StomachHit", "RibHit",
            })
            {
                Assert.IsFalse(CastawayCharacter.LocomotionLaneOwnsPoseFor(H(overlay), false, 0),
                    $"'{overlay}' authors its own arm pose — the run-lower must not reach it.");
                Assert.IsFalse(CastawayCharacter.LocomotionLaneOwnsPoseFor(
                    H(CastawayCharacter.LocomotionState), true, H(overlay)),
                    $"the crossfade from locomotion INTO '{overlay}' must close the gate on its first frame.");
            }
        }

        // ==============================================================================================
        // 3 — THE EASE PROFILE (time-qualified). Drives CastawayArmPose.NextRunWeight = production math.
        // ==============================================================================================

        /// <summary>Step the PRODUCTION weight function for <paramref name="seconds"/> at 60 Hz.</summary>
        private static float Ease(float from, bool isRunning, bool laneOwnsPose, float seconds)
        {
            float w = from;
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
                w = CastawayArmPose.NextRunWeight(w, isRunning, laneOwnsPose, InLaneRate, ReleaseRate, Dt);
            return w;
        }

        [Test]
        public void OverlayRelease_IsSpentBy150ms_TimeQualified_NotInstant()
        {
            // NOT instant: an immediate zero is a one-frame ~43.7 deg arm pop. One frame in must still be
            // meaningfully non-zero so the hand-back reads as a blend, not a snap.
            float oneFrame = Ease(1f, true, false, Dt);
            Assert.Greater(oneFrame, 0.4f,
                $"the release must EASE, not snap: after one frame the weight is {oneFrame:F3}. An instant zero " +
                "trades the silhouette defect for a visible single-frame arm pop.");

            // Spent by 0.15 s = 14% into the FASTEST swing (2.0 s clip at the live ~1.9x playback = ~1.05 s
            // wall-time), and only ~0.09 s past the AnyState->AttackX crossfade that visually masks it.
            float atCrossfadeEnd = Ease(1f, true, false, 0.0667f);
            float at150 = Ease(1f, true, false, 0.15f);
            Assert.LessOrEqual(at150, 0.05f,
                $"0.15 s after an overlay takes the pose the run-lower must be a negligible residual (got " +
                $"{at150:F3}). This is the TIME-QUALIFIED form of the regression AC — the literal 'in the Pass-1 " +
                "band while an attack state is active' cannot hold at swing entry at any finite blend rate.");
            Assert.Less(atCrossfadeEnd, 0.20f,
                $"most of the release must happen inside the 0.06 s entry crossfade that masks it (got " +
                $"{atCrossfadeEnd:F3} at crossfade end).");

            // The residual expressed where the study measured it: the run-lower's own rotation magnitude.
            float residualDeg = Quaternion.Angle(Quaternion.identity, Quaternion.Euler(RunLowerEuler * at150));
            Assert.Less(residualDeg, 1.5f,
                $"the residual run-lower rotation at 0.15 s is {residualDeg:F2} deg; ungated it is " +
                $"{Quaternion.Angle(Quaternion.identity, Quaternion.Euler(RunLowerEuler)):F1} deg.");
        }

        [Test]
        public void OverlayRelease_ReturnsTheCompositeCeilingToThePass1Band()
        {
            // The 86caxgwbz study's two ceilings: Pass 1 = |carry| (22.55 deg, max measured hand arc 19.9 deg);
            // Pass 2 = |carry * runLower| (47.61 deg, measured arcs 45.8-47.6 deg). Post-gate the composite must
            // be back at the Pass-1 ceiling, which is what collapses the measured arc back into the Pass-1 band.
            float pass1 = Quaternion.Angle(Quaternion.identity, Quaternion.Euler(CarryEuler));
            float pass2 = Quaternion.Angle(Quaternion.identity,
                                           Quaternion.Euler(CarryEuler) * Quaternion.Euler(RunLowerEuler));
            Assert.Greater(pass2, 2f * pass1 - 5f, "sanity: the ungated composite is roughly double the carry-only ceiling.");

            float w = Ease(1f, true, false, 0.15f);
            float gated = Quaternion.Angle(Quaternion.identity,
                                           Quaternion.Euler(CarryEuler) * Quaternion.Euler(RunLowerEuler * w));
            Assert.Less(gated, pass1 + 1f,
                $"0.15 s into a swing the composite offset ceiling must be back within 1 deg of the always-on " +
                $"carry ceiling ({pass1:F2} deg); got {gated:F2} deg (ungated: {pass2:F2} deg).");
        }

        [Test]
        public void PlainSprint_StillEngagesFully_AtTheLockedInLaneRate_86caa83wn()
        {
            // THE REGRESSION GUARD. The run-lower exists because the Mixamo RUN clip pumps the right arm up near
            // the head and the gripped axe follows it (86caa83wn). Gating must not weaken that: inside the lane,
            // a sprint still reaches full engagement on the LOCKED rate.
            float at400 = Ease(0f, true, true, 0.4f);
            Assert.Greater(at400, 0.95f,
                $"a plain sprint must still drive the run-lower to full engagement (got {at400:F3} after 0.4 s) — " +
                "otherwise the axe rides back into the head, the defect 86caa83wn fixed.");

            // ...and it must use the SLOW in-lane rate, not the fast release rate — i.e. the engage feel is
            // unchanged. At 0.1 s the locked rate gives ~0.55; the fast rate would give ~0.95.
            float at100 = Ease(0f, true, true, 0.1f);
            Assert.Less(at100, 0.75f,
                $"the in-lane sprint ease must keep the locked ~8/s rate (got {at100:F3} at 0.1 s) — the fast " +
                "release rate is for overlay hand-back ONLY; applying it to sprint engage would change the feel " +
                "the Sponsor soaked.");
        }

        [Test]
        public void WalkIdle_RestsAtZero_LockedPoseByteUnchanged()
        {
            Assert.AreEqual(0f, Ease(0f, false, true, 1f), 1e-4f,
                "at walk/idle the run weight must rest at 0 so the run-lower offset is the identity and the " +
                "Sponsor's locked WALK/IDLE pose is byte-unchanged.");
            Assert.Less(Ease(1f, false, true, 0.6f), 0.01f,
                "releasing sprint inside the lane must still ease the run-lower back out on the locked rate.");
        }

        [Test]
        public void RunLowerCannotEngage_WhileAnOverlayOwnsThePose_EvenAtFullRunSpeed()
        {
            // The core of the fix: velocity alone is no longer sufficient. A player who sprints for the whole
            // swing (IsRunning true throughout) still gets zero run-lower while the overlay holds the pose.
            Assert.AreEqual(0f, Ease(0f, true, false, 1f), 1e-4f,
                "IsRunning must no longer be able to engage the run-lower on its own — the locomotion lane has to " +
                "own the pose too. This is the whole 86caxj30g defect: a sprinting strike used to inherit the " +
                "full run-lower on all five per-class swings.");
        }

        [Test]
        public void ShippedComponentDefaults_CarryTheAsymmetry()
        {
            var go = new GameObject("armPoseDefaults");
            try
            {
                var pose = go.AddComponent<CastawayArmPose>();
                Assert.AreEqual(InLaneRate, pose.runLowerBlendRate, 1e-4f,
                    "the in-lane blend rate must stay at the 86caa83wn-locked value.");
                Assert.Greater(pose.runLowerOverlayReleaseRate, pose.runLowerBlendRate * 2f,
                    "the overlay release must be materially FASTER than the in-lane rate — that asymmetry is what " +
                    "hands the arm back before the strike reads.");
                Assert.AreEqual(ReleaseRate, pose.runLowerOverlayReleaseRate, 1e-4f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
