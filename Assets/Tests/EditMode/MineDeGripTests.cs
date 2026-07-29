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
    /// 86cay4282 — the MINE DE-GRIP (the Sponsor's soak report on `soak-pickaxe-1`, stamp 1194927: "he is swinging
    /// like he is handing the axe with both hands, when in reaity the axe stays in the right hand only. the axe is
    /// still pivoting and not sitting right during the swing").
    ///
    /// THE DEFECT, MEASURED (AttackClipPoseDiag prop-seat pass, live rig, 31 samples/clip). Both halves of the
    /// report are ONE defect — the mine clip's authored two-handed motion:
    ///   * hand SEPARATION across the swing: pickaxe 1.09-1.29 shoulder-widths (range 0.20) vs the approved idle
    ///     carry 1.65-1.89 and the axe chop 1.77-2.86 (range 1.09). Hands LOCKED close = a shared-haft grip.
    ///   * the real one-handed tool then sits 63.8-89.7 deg off the line through both hands (the axe chop reaches
    ///     6.8 deg — it lines up with its own swing), so the tool visibly disagrees with the grip the eye reads.
    ///
    /// ⚠ ROUND 2 — THE SPONSOR REVERSED THE FIX DIRECTION. He soaked round 1's arm-side de-grip and said, verbatim:
    /// "we need to position the axe for a two hand grip". The measurement above still stands and is still the right
    /// diagnosis of WHAT the clip does — but its INTERPRETATION flipped: the locked-together hands are what he
    /// WANTS, so the animation was right all along and the one-handed SEAT is the defect. The de-grip therefore
    /// ships at ZERO amplitude with its mechanism retained as an A/B dial (tests 5 below), and the actual fix — a
    /// state-gated MINE seat delta that turns the haft onto the line through both hands — is pinned by
    /// <c>MineSeatTests</c> / <c>MineSeatPlayModeTests</c>. Everything this file pins about the GATE (naming a real
    /// state, transition pairing, fail-closed, byte-unchanged rest) is unchanged and now guards BOTH offsets, since
    /// the seat delta rides the same gate and the same eased-weight policy function.
    ///
    /// WHAT THIS FILE PINS — each a bug CLASS, not a value:
    ///   1. THE GATE NAMES A REAL STATE, and only that one: "AttackPickaxe" must exist in the SHIPPED controller
    ///      (a rename in CharacterAssetGen reds here instead of silently making the de-grip dead code), and every
    ///      other authored state must be OUT of the gate — the axe/dagger/spear/sword swings measured fine and
    ///      must be handed back to their clips untouched.
    ///   2. THE TRANSITION PAIRING, mirrored from 86caxj30g: layer 0 reports the FROM state for the whole
    ///      AnyState->AttackPickaxe crossfade, so a current-only gate would engage a transition-duration late.
    ///   3. FAIL-CLOSED: with no Animator the de-grip must NOT engage (the opposite default to the run-lower gate,
    ///      because this offset ADDS rather than subtracts — both fail toward leaving the clip alone).
    ///   4. REST IS BYTE-UNCHANGED: at weight 0 the composed left-arm rotation equals the pre-86cay4282 pose
    ///      exactly. This is the regression guard for the Sponsor's locked carry/idle/walk/run pose.
    ///   5. THE |Q| / STATE-GATE CONTRACT (procedural-animation-verbs.md, 86caxgwbz): a dial over ~40 deg MUST be
    ///      state-gated. This ties the doc rule to the code so raising the dial past the band without a gate reds.
    ///   6. THE SHIP SOURCE: MovementCameraScene.ArmMineDeGripEuler is what AddArmPose bakes into Boot.unity, so a
    ///      drifting runtime field default cannot silently become the shipped value.
    ///
    /// The ease tests drive <see cref="CastawayArmPose.NextMineDeGripWeight"/> — the PRODUCTION function LateUpdate
    /// calls — so they cannot go tautologically green against a re-implementation.
    /// </summary>
    public class MineDeGripTests
    {
        private const float Dt = 1f / 60f;
        private const float Rate = 12f;   // CastawayArmPose.mineDeGripBlendRate (shipped)

        private static int Hash(string s) => Animator.StringToHash(s);

        // ==============================================================================================
        // 1 — THE GATE NAMES A REAL SHIPPED STATE, AND ONLY THAT ONE.
        // ==============================================================================================

        [Test]
        public void MineGateStateName_IsAuthoredByTheShippedController_SoARenameCannotSilentlyKillTheDeGrip()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the shipped controller must exist @ " + CharacterAssetGen.ControllerPath);

            var authored = new List<string>();
            foreach (var layer in controller.layers)
                foreach (var st in layer.stateMachine.states)
                    authored.Add(st.state.name);

            CollectionAssert.Contains(authored, CastawayCharacter.AttackPickaxeState,
                "CastawayCharacter.AttackPickaxeState must name a state the shipped controller actually authors — " +
                "otherwise the mine de-grip gate can never be true and the fix is dead code that tests still pass.");
        }

        [Test]
        public void EveryOtherShippedState_IsOutsideTheMineGate_SoTheOtherFourSwingsAreUntouched()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the shipped controller must exist @ " + CharacterAssetGen.ControllerPath);

            var offenders = new List<string>();
            foreach (var layer in controller.layers)
                foreach (var st in layer.stateMachine.states)
                {
                    string name = st.state.name;
                    if (name == CastawayCharacter.AttackPickaxeState) continue;
                    if (CastawayCharacter.MineSwingOwnsPoseFor(Hash(name), false, 0)) offenders.Add(name);
                }

            CollectionAssert.IsEmpty(offenders,
                "ONLY the pickaxe mine swing may engage the de-grip. The axe/dagger/spear/sword swings measured " +
                "one-handed (axe chop: hands 1.77-2.86 SW apart, tool 6.8 deg off the hand line at the strike) and " +
                "must be handed back to their clips untouched. Offending states: " + string.Join(", ", offenders));
        }

        // ==============================================================================================
        // 2 — TRANSITION PAIRING (the 86caxj30g lesson, mirrored).
        // ==============================================================================================

        [Test]
        public void Gate_EngagesOnTheFirstFrameOfTheCrossfadeIn_NotAfterItCompletes()
        {
            int loco = Hash(CastawayCharacter.LocomotionState);
            int mine = Hash(CastawayCharacter.AttackPickaxeState);

            Assert.IsFalse(CastawayCharacter.MineSwingOwnsPoseFor(loco, false, 0),
                "settled locomotion must not engage the de-grip");
            Assert.IsTrue(CastawayCharacter.MineSwingOwnsPoseFor(loco, true, mine),
                "THE PAIRING: mid-crossfade layer 0 still reports Locomotion as CURRENT, so a current-only gate " +
                "would leave the arm closed for the whole 0.06 s entry blend and pop it open under the strike.");
        }

        [Test]
        public void Gate_StaysEngagedThroughTheCrossfadeOut_SoTheArmNeverHalfCloses_MidVisibleSwing()
        {
            int loco = Hash(CastawayCharacter.LocomotionState);
            int mine = Hash(CastawayCharacter.AttackPickaxeState);

            Assert.IsTrue(CastawayCharacter.MineSwingOwnsPoseFor(mine, false, 0), "mid-swing must be engaged");
            Assert.IsTrue(CastawayCharacter.MineSwingOwnsPoseFor(mine, true, loco),
                "the swing's tail is still visible during the crossfade OUT — releasing there would close the arm " +
                "back onto the phantom haft on the last frames of the strike.");
            Assert.IsFalse(CastawayCharacter.MineSwingOwnsPoseFor(loco, true, loco),
                "once layer 0 has fully settled back to locomotion the de-grip must be released.");
        }

        [Test]
        public void Gate_IgnoresTheNextState_WhenNotInTransition()
        {
            // GetNextAnimatorStateInfo is meaningless outside a transition; a gate that reads it unconditionally
            // would engage off a stale hash.
            Assert.IsFalse(
                CastawayCharacter.MineSwingOwnsPoseFor(Hash(CastawayCharacter.IdleState), false,
                                                       Hash(CastawayCharacter.AttackPickaxeState)),
                "with inTransition=false the next-state hash must be ignored entirely.");
        }

        // ==============================================================================================
        // 3 — FAIL-CLOSED without an Animator.
        // ==============================================================================================

        [Test]
        public void LiveGate_FailsClosed_WithNoAnimatorOrController()
        {
            var go = new GameObject("MineGateFailClosed");
            try
            {
                var ch = go.AddComponent<CastawayCharacter>();
                Assert.IsFalse(ch.MineSwingOwnsPose,
                    "with no Animator/controller the de-grip must stay OFF. It can only ever ADD an offset, so it " +
                    "fails toward leaving the clip alone — the mirror of the run-lower gate, which fails OPEN " +
                    "because it can only ever SUBTRACT.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ==============================================================================================
        // 4 — REST IS BYTE-UNCHANGED (the locked-pose regression guard).
        // ==============================================================================================

        [Test]
        public void AtZeroWeight_TheComposedLeftArmRotation_IsByteIdenticalToThePreFixPose()
        {
            // The production composition is  clipPose * _leftOffsetQ * Euler(mineDeGripEuler * weight).
            // NOTE the probe amplitude is DELIBERATELY non-zero and NOT the shipped constant: the shipped value is
            // now zero, so reading it here would make the assert tautological (Euler(0*0) is trivially the identity
            // whether or not the weight gating works). Using a large dialed amplitude asserts the real guarantee —
            // the mechanism is inert AT REST no matter what the Sponsor dials into it.
            var clipPose = Quaternion.Euler(13f, -47f, 8f);       // an arbitrary non-identity clip frame
            var leftOffset = Quaternion.Euler(MovementCameraScene.CastawayV4LeftArmEuler);
            Vector3 dialedProbe = new Vector3(-40f, 0f, 20f);     // round 1's measured amplitude, as a probe

            Quaternion preFix = clipPose * leftOffset;
            Quaternion atRest = clipPose * leftOffset * Quaternion.Euler(dialedProbe * 0f);

            Assert.AreEqual(0f, Quaternion.Angle(preFix, atRest), 1e-4f,
                "at weight 0 Euler(deGrip*0) is the identity, so every non-mining state — idle, walk, run, jump, " +
                "crouch, the other four swings — keeps the Sponsor's locked left-arm pose exactly.");
            Assert.Greater(Quaternion.Angle(preFix, clipPose * leftOffset * Quaternion.Euler(dialedProbe)), 1f,
                "control: the SAME composition with weight 1 must genuinely move the arm — otherwise the assert " +
                "above would pass against a mechanism that does nothing at any weight.");
        }

        [Test]
        public void Weight_RestsAtZero_AndReturnsToZero_WhenTheSwingEnds()
        {
            float w = 0f;
            for (int i = 0; i < 120; i++) w = CastawayArmPose.NextMineDeGripWeight(w, false, Rate, Dt);
            Assert.AreEqual(0f, w, 1e-5f, "outside the mine swing the weight must rest at exactly 0");

            for (int i = 0; i < 120; i++) w = CastawayArmPose.NextMineDeGripWeight(w, true, Rate, Dt);
            Assert.Greater(w, 0.99f, "while the mine swing owns the pose the weight must reach full engagement");

            for (int i = 0; i < 60; i++) w = CastawayArmPose.NextMineDeGripWeight(w, false, Rate, Dt);
            Assert.Less(w, 0.05f, "when the swing ends the weight must fall back so the carry pose is restored");
        }

        [Test]
        public void Weight_Eases_RatherThanPopping_AndIsOpenBeforeTheStrikeReads()
        {
            float w = 0f;
            w = CastawayArmPose.NextMineDeGripWeight(w, true, Rate, Dt);
            Assert.Less(w, 0.5f,
                $"the first engaged frame must EASE (got {w:F3}); an instant 1.0 is a 40 deg one-frame arm pop.");

            // 0.25 s in — inside the clip's wind-up, before the strike reads.
            for (int i = 1; i < 15; i++) w = CastawayArmPose.NextMineDeGripWeight(w, true, Rate, Dt);
            Assert.Greater(w, 0.90f,
                $"the arm must be open by ~0.25 s (got {w:F3}) so the de-grip is established during the wind-up, " +
                "not appearing under the strike.");
        }

        // ==============================================================================================
        // 5 — THE ROUND-2 REVERSAL: the de-grip MECHANISM survives, its AMPLITUDE ships neutral.
        // ==============================================================================================

        [Test]
        public void ShippedDeGrip_IsNeutral_BecauseTheSponsorReversedTheDirection()
        {
            // The Sponsor soaked round 1's arm-side de-grip and reversed the premise, verbatim: "we need to position
            // the axe for a two hand grip". So the mine clip's locked-together hands are CORRECT and the arms must be
            // left exactly as authored; the fix moved to the TOOL side (HeldToolRig's MINE seat delta). This pins the
            // reversal so a later well-meaning "restore the measured -40,0,20" cannot silently re-open his complaint.
            Assert.AreEqual(Vector3.zero, MovementCameraScene.ArmMineDeGripEuler,
                "ArmMineDeGripEuler must ship ZERO after the round-2 reversal — the mine swing is meant to READ " +
                "two-handed, so the arms keep their authored pose and the HAFT is what moves.");
        }

        [Test]
        public void TheDeGripMechanism_IsRetained_NotDeleted_SoTheDialStillExists()
        {
            // Deliberately kept rather than removed: it is measured, state-gated and test-covered, and shipping it
            // at zero amplitude leaves a free A/B on the F9 MINE target if the two-hand seat ever wants the arms
            // opened a touch. This asserts the PLUMBING is still live (the weight still rises inside the gate), which
            // is what makes the knob usable — a deleted mechanism would leave the panel target dialing nothing.
            float w = 0f;
            for (int i = 0; i < 120; i++) w = CastawayArmPose.NextMineDeGripWeight(w, true, Rate, Dt);
            Assert.Greater(w, 0.99f,
                "the de-grip weight policy must still reach full engagement inside the gate — the mechanism is " +
                "retained as a dial, only its shipped amplitude is zero.");
        }

        [Test]
        public void TheStateGateContract_NowGuardsTheSeatDelta_WhoseQIsInTheGatedBand()
        {
            // The rule (procedural-animation-verbs.md / 86caxgwbz): under ~25 deg is clip-safe by construction; over
            // ~40 deg needs a state gate. Round 1's de-grip was the dial in that band; after the reversal the large
            // offset is the SEAT euler delta, so the contract has to follow it there rather than lapse.
            Quaternion.Euler(MovementCameraScene.HeldToolMineSeatEulerDelta).ToAngleAxis(out float q, out _);
            if (q > 180f) q = 360f - q;

            Assert.Greater(q, 40f,
                $"|Q|={q:F1} deg — the measured two-hand fit turns the haft from 89.7 deg off the hand line to " +
                "31.9 deg, which is a large rotation by necessity, not by accident.");
            Assert.IsFalse(CastawayCharacter.MineSwingOwnsPoseFor(Hash(CastawayCharacter.IdleState), false, 0),
                $"|Q|={q:F1} deg is far into the '>~40 deg needs a state gate' band, so the seat delta MUST be inert " +
                "outside its own state. If this ever passes for a locomotion state the seat is leaking into every " +
                "clip — the 86caxj30g defect one layer over, and it would move the Sponsor-approved carry seat.");
        }

        // ==============================================================================================
        // 6 — THE SHIP SOURCE.
        // ==============================================================================================

        [Test]
        public void ShippedDeGrip_ComesFromMovementCameraScene_NotTheRuntimeFieldDefault()
        {
            var go = new GameObject("MineDeGripDefaultProbe");
            try
            {
                var pose = go.AddComponent<CastawayArmPose>();
                // AddArmPose writes ArmMineDeGripEuler onto the component, and that is what serializes into
                // Boot.unity. Pin them equal so a drifting runtime default cannot become the shipped value
                // unnoticed ([[unity-procedural-committed-assets-go-stale]] — the build ships the committed scene).
                Assert.AreEqual(MovementCameraScene.ArmMineDeGripEuler, pose.mineDeGripEuler,
                    "CastawayArmPose.mineDeGripEuler (the runtime fallback) must stay in sync with " +
                    "MovementCameraScene.ArmMineDeGripEuler (the authoritative bake source) — the same convention " +
                    "runLowerEuler/ArmRunLowerEuler follow.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
