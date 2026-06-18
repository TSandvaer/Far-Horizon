using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// LOCOMOTION TRANSITION SMOOTHING guards (ticket 86caay44r). The Sponsor reported idle&lt;-&gt;walk&lt;-&gt;run
    /// transitions as too ABRUPT — most noticeable stopping a walk (release W). Two levers smooth it:
    ///   (a) the Speed param fed to the blend tree is DAMPED (CastawayCharacter.speedDampTime &gt; 0), so the
    ///       blend EASES rather than snaps on start/stop/speed-change;
    ///   (b) the Idle&lt;-&gt;Locomotion crossfade DURATIONS are lengthened so the state transition glides.
    ///
    /// These pin the BUG CLASS (the transitions must EASE, not snap), not one feel value — and crucially they
    /// also pin that the JUMP-return transitions (#69's floating-bug fix) are NOT collateral-damaged by the
    /// smoothing pass: those stay no-exit-time, grounded-edge, short crossfades. A regression that snaps the
    /// locomotion transitions back to the old short durations (or zeroes the Speed damp) fails here in headless
    /// CI before a soak. (Cross-lane: Idle&lt;-&gt;Locomotion + Walk&lt;-&gt;Run blend (#68) + Jump-return (#69).)
    /// </summary>
    public class LocomotionTransitionSmoothingTests
    {
        // The smoothing floor: a transition shorter than this reads as a snap. The shipped values are 0.22
        // (start) / 0.30 (stop); this asserts they cleared the snappy old 0.12/0.15 by a clear margin — pinning
        // the eased FEEL without locking an exact number the Sponsor may re-tune up.
        private const float SmoothFloor = 0.18f;

        private static AnimatorController LoadController()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(c, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);
            return c;
        }

        private static AnimatorState FindState(AnimatorController c, string name)
        {
            foreach (var cs in c.layers[0].stateMachine.states)
                if (cs.state.name == name) return cs.state;
            return null;
        }

        // Is `t` a transition INTO the named destination state?
        private static bool ToState(AnimatorStateTransition t, string destName)
            => t.destinationState != null && t.destinationState.name == destName;

        [Test]
        public void SpeedParam_IsDamped_SoTheBlendEasesNotSnaps()
        {
            // The runtime lever: CastawayCharacter must ship a POSITIVE speedDampTime so the Speed param eases
            // toward its target (Animator.SetFloat with a dampTime) instead of a raw instant set. A 0 here reverts
            // to the snappy old behavior — the exact "release W and the walk SNAPS to idle" complaint.
            var go = new GameObject("castaway-damp-probe");
            var cc = go.AddComponent<CastawayCharacter>();
            Assert.Greater(cc.speedDampTime, 0.0001f,
                "CastawayCharacter.speedDampTime must be > 0 so the locomotion blend EASES on start/stop/" +
                "speed-change (a 0 reverts to the abrupt raw Speed set — the Sponsor's 'too abrupt' report, 86caay44r)");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void IdleToLocomotion_AndBack_CrossfadesAreSoftened_NotSnappy()
        {
            var c = LoadController();
            var idle = FindState(c, "Idle");
            var loco = FindState(c, "Locomotion");
            Assert.IsNotNull(idle, "the controller must have an Idle state");
            Assert.IsNotNull(loco, "the controller must have a Locomotion state");

            // Idle -> Locomotion (start moving) must be a softened, no-exit-time crossfade.
            AnimatorStateTransition toLoco = null;
            foreach (var t in idle.transitions) if (ToState(t, "Locomotion")) toLoco = t;
            Assert.IsNotNull(toLoco, "there must be an Idle->Locomotion transition");
            Assert.IsFalse(toLoco.hasExitTime, "Idle->Locomotion must have NO exit time (responds to input immediately)");
            Assert.GreaterOrEqual(toLoco.duration, SmoothFloor,
                $"Idle->Locomotion crossfade must be softened (>= {SmoothFloor}s) so starting to move EASES in — " +
                $"the old 0.12s snapped (86caay44r). Got {toLoco.duration:F3}s.");

            // Locomotion -> Idle (stop — THE most-noticed case: release W) must ease out at least as long.
            AnimatorStateTransition toIdle = null;
            foreach (var t in loco.transitions) if (ToState(t, "Idle")) toIdle = t;
            Assert.IsNotNull(toIdle, "there must be a Locomotion->Idle transition");
            Assert.IsFalse(toIdle.hasExitTime, "Locomotion->Idle must have NO exit time (stops when input releases)");
            Assert.GreaterOrEqual(toIdle.duration, SmoothFloor,
                $"Locomotion->Idle crossfade must be softened (>= {SmoothFloor}s) — this is the 'release W and it " +
                $"SNAPS' case the Sponsor flagged most (86caay44r). Got {toIdle.duration:F3}s.");
            Assert.GreaterOrEqual(toIdle.duration, toLoco.duration - 1e-4f,
                "the stop (Locomotion->Idle) should ease out at least as long as the start eases in (the stop is " +
                "the most-noticed snap).");
        }

        [Test]
        public void Walk_Run_BlendTree_IsUntouched_StillBlendsOnSpeed()
        {
            // CROSS-LANE (#68): the smoothing pass must NOT disturb the Walk<->Run 1D blend tree. The Locomotion
            // state must still BE a 1D blend tree on the Speed param — the damp/duration changes only smooth HOW
            // the blend is reached, never replace the blend itself.
            var c = LoadController();
            var loco = FindState(c, "Locomotion");
            Assert.IsNotNull(loco);
            Assert.IsInstanceOf<BlendTree>(loco.motion, "Locomotion must still be a blend tree (the #68 Walk<->Run blend)");
            var tree = (BlendTree)loco.motion;
            Assert.AreEqual(BlendTreeType.Simple1D, tree.blendType, "the Walk<->Run blend tree must stay 1D");
            Assert.AreEqual(CastawayCharacter.SpeedParam, tree.blendParameter,
                "the Walk<->Run blend tree must still blend on the Speed param (#68 intact)");
        }

        [Test]
        public void JumpReturnTransitions_AreNotCollateralDamaged_StillNoExitTime_GroundedEdge()
        {
            // CROSS-LANE (#69 floating-bug fix): the locomotion-smoothing pass must NOT touch the jump-return
            // transitions. Both JumpIdle + JumpRunning must STILL return on the GROUNDED edge with NO exit time
            // (the physical arc owns landing — a regression to exit-time re-opens the 'land stalls in jump pose
            // while translating = floating' bug). Their short crossfades are deliberate (the landing is crisp);
            // we assert no-exit-time + a Grounded condition, NOT a long duration.
            var c = LoadController();
            foreach (var jumpName in new[] { "JumpIdle", "JumpRunning" })
            {
                var js = FindState(c, jumpName);
                Assert.IsNotNull(js, $"the controller must have the {jumpName} state (#69)");
                bool toLocoGroundedNoExit = false, toIdleGroundedNoExit = false;
                foreach (var t in js.transitions)
                {
                    bool hasGrounded = false;
                    foreach (var cond in t.conditions)
                        if (cond.parameter == CastawayCharacter.GroundedParam) hasGrounded = true;
                    if (ToState(t, "Locomotion") && hasGrounded && !t.hasExitTime) toLocoGroundedNoExit = true;
                    if (ToState(t, "Idle") && hasGrounded && !t.hasExitTime) toIdleGroundedNoExit = true;
                }
                Assert.IsTrue(toLocoGroundedNoExit,
                    $"{jumpName} must STILL return to Locomotion on the Grounded edge with no exit time (#69 — a " +
                    "regression here re-opens the 'floating on landing' bug; the smoothing pass must not touch it)");
                Assert.IsTrue(toIdleGroundedNoExit,
                    $"{jumpName} must STILL return to Idle on the Grounded edge with no exit time (#69)");
            }
        }
    }
}
