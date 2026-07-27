using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86caxj30g — LIVE-ANIMATOR proof of the run-lower locomotion-lane gate. The EditMode
    /// <c>RunLowerLocomotionGateTests</c> pin the pure predicate + the ease profile; this pins the one claim they
    /// CANNOT: that the shipped <c>CastawayAnimator.controller</c> really reports the from-state through an
    /// <c>AnyState -&gt; AttackX</c> crossfade, so the transition pairing is load-bearing rather than defensive.
    ///
    /// HEADLESS-TICK TRAP (unity-conventions.md §Headless): a -batchmode frame has Time.deltaTime≈0, so an Animator
    /// on the engine clock never advances its state machine. Every tick here is an explicit
    /// <c>Animator.Update(dt)</c> with a positive delta — the same idiom as
    /// <c>CastawayLocomotionHitReactPlayModeTests</c>.
    ///
    /// No CastawayCharacter component is built (a bare rig would emit the "modelPrefab not wired" error that fails
    /// an undeclared PlayMode test — unity-conventions.md §Headless): the gate is read through the PURE
    /// <c>CastawayCharacter.LocomotionLaneOwnsPoseFor</c> overload fed from the live layer-0 readings, which is
    /// exactly what the instance property does.
    ///
    /// EDITOR-ONLY (loads the controller asset via AssetDatabase); Ignores in a player build.
    /// </summary>
    public class RunLowerLocomotionGatePlayModeTests
    {
        // Mirror CharacterAssetGen (the editor asmdef is intentionally NOT referenced by this all-platform
        // PlayTests asmdef — same convention as CastawayLocomotionHitReactPlayModeTests).
        private const string ControllerPath = "Assets/Art/Character/Castaway/CastawayAnimator.controller";
        private const float RunBlendSpeed = 9.5f;   // == CharacterAssetGen.RunBlendSpeed

        private const float InLaneRate = 8f;    // CastawayArmPose.runLowerBlendRate
        private const float ReleaseRate = 30f;  // CastawayArmPose.runLowerOverlayReleaseRate
        private const float Dt = 1f / 60f;

        private GameObject _go;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, "the production CastawayAnimator controller must exist at " + ControllerPath);

            _go = new GameObject("GateRig");
            _animator = _go.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

#if UNITY_EDITOR
        /// <summary>The gate, read from the LIVE layer-0 state the way CastawayCharacter.LocomotionLaneOwnsPose does.</summary>
        private bool LaneOwnsPose()
        {
            bool inTr = _animator.IsInTransition(0);
            int next = inTr ? _animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
            return CastawayCharacter.LocomotionLaneOwnsPoseFor(
                _animator.GetCurrentAnimatorStateInfo(0).shortNameHash, inTr, next);
        }

        /// <summary>The NAIVE current-state-only verdict — what a gate without the transition pairing would say.</summary>
        private bool NaiveLaneOwnsPose() =>
            CastawayCharacter.IsLocomotionLaneState(_animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

        private void SettleIntoRun()
        {
            _animator.SetBool(CastawayCharacter.GroundedParam, true);
            _animator.SetBool(CastawayCharacter.MovingParam, true);
            _animator.SetFloat(CastawayCharacter.SpeedParam, RunBlendSpeed);
            _animator.SetFloat(CastawayCharacter.LocoSpeedMulParam, 1f);
            for (int i = 0; i < 40; i++) _animator.Update(0.05f);
        }
#endif

        // The load-bearing live claim: through the AnyState->AttackAxe crossfade the layer still reports
        // Locomotion as CURRENT, so a current-only gate stays open while the swing is already taking over.
        [UnityTest]
        public IEnumerator SwingEntryCrossfade_ReportsLocomotionAsCurrent_SoTheGateMustPairWithTheNextState()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the controller asset via AssetDatabase)");
            yield break;
#else
            yield return null;
            SettleIntoRun();
            Assert.IsTrue(LaneOwnsPose(), "a settled sprint must have the locomotion lane owning the pose");

            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassAxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);
            _animator.Update(Dt);

            Assert.IsTrue(_animator.IsInTransition(0),
                "the Chop trigger must open the AnyState->AttackAxe transition on the next ticked frame");
            Assert.IsTrue(NaiveLaneOwnsPose(),
                "THE TRAP: mid-crossfade, GetCurrentAnimatorStateInfo still reports the FROM state (Locomotion), " +
                "so a current-state-only gate reads 'in lane' while the swing is already taking the pose.");
            Assert.IsFalse(LaneOwnsPose(),
                "the shipped gate pairs current WITH GetNextAnimatorStateInfo, so it closes on the FIRST frame of " +
                "the crossfade into the swing — not after the transition completes.");
            yield return null;
#endif
        }

        // The consequence, time-qualified: the run-lower weight is a negligible residual well before the strike
        // reads, and it gets there through an ease (no one-frame arm pop).
        [UnityTest]
        public IEnumerator RunLowerWeight_CollapsesWithin150ms_OfASprintingSwing_WhileStillSprinting()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the controller asset via AssetDatabase)");
            yield break;
#else
            yield return null;
            SettleIntoRun();

            float w = 1f;          // fully engaged: the player has been sprinting
            float wOld = 1f;       // the pre-fix velocity-only policy, for contrast
            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassAxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);

            float firstFrameW = -1f;
            for (int f = 0; f < 9; f++)   // 9 frames @ 60Hz = 0.15 s
            {
                _animator.Update(Dt);
                // IsRunning stays TRUE the whole time — the player never released sprint. That is the point.
                w = CastawayArmPose.NextRunWeight(w, true, LaneOwnsPose(), InLaneRate, ReleaseRate, Dt);
                wOld = CastawayArmPose.NextRunWeight(wOld, true, true, InLaneRate, ReleaseRate, Dt);
                if (f == 0) firstFrameW = w;
            }

            Assert.Greater(firstFrameW, 0.4f,
                $"the hand-back must EASE (first frame {firstFrameW:F3}) — an instant zero is a ~44 deg one-frame pop.");
            Assert.LessOrEqual(w, 0.05f,
                $"0.15 s into a sprinting swing the run-lower must be spent (got {w:F3}); the pre-fix policy is " +
                $"still at {wOld:F3} and stays there for the whole strike.");
            Assert.Greater(wOld, 0.95f, "contrast: velocity-only never releases while the player keeps sprinting.");
            yield return null;
#endif
        }

        // The other half of the asymmetry: once the pose comes back to the locomotion lane the run-lower
        // re-engages on the LOCKED rate (86caa83wn must not regress).
        [UnityTest]
        public IEnumerator GateReOpens_AndTheRunLowerReEngages_WhenTheLaneTakesThePoseBack()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the controller asset via AssetDatabase)");
            yield break;
#else
            yield return null;
            SettleIntoRun();
            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassAxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);
            for (int f = 0; f < 30; f++) _animator.Update(Dt);
            Assert.IsFalse(LaneOwnsPose(), "sanity: the swing owns the pose");

            // The swing's own return needs the full clip's exit-time; force the crossfade back to the lane.
            _animator.CrossFade(CastawayCharacter.LocomotionState, 0.10f, 0, 0f);
            float w = 0f;
            bool reOpened = false;
            for (int f = 0; f < 60; f++)   // 1 s
            {
                _animator.Update(Dt);
                bool lane = LaneOwnsPose();
                reOpened |= lane;
                w = CastawayArmPose.NextRunWeight(w, true, lane, InLaneRate, ReleaseRate, Dt);
            }

            Assert.IsTrue(reOpened, "the gate must RE-OPEN once the locomotion lane owns the pose again");
            Assert.Greater(w, 0.9f,
                $"the run-lower must re-engage for the continuing sprint (got {w:F3}) — otherwise the axe rides " +
                "back into the head after every swing, regressing 86caa83wn.");
            yield return null;
#endif
        }
    }
}
