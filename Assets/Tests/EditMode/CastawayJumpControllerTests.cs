using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// JUMP asset/controller guards (ticket 86ca9yq3q + the Sponsor-soak REWORK). These pin the asset-side
    /// contract the runtime jump relies on so the bug CLASSES can't recur silently in headless CI before a soak:
    ///
    ///   1. Jump_idle.fbx + Jump_running.fbx import like Walking/Running.fbx (GENERIC rig, CreateFromThisModel)
    ///      with NON-LOOPING clips — a jump is a ONE-SHOT (a looping jump replays the push-off forever). The
    ///      Mixamo take is "mixamo.com" → renamed CastawayJumpIdle / CastawayJumpRunning (an exact "Jump" match
    ///      would loop ZERO clips — the T-pose-mid-jump class). (AC: two clips by movement state, import.)
    ///   2. The CastawayAnimator controller carries the Jump TRIGGER + the GROUNDED bool + TWO jump STATES
    ///      (JumpIdle motion'd to the idle clip, JumpRunning to the running clip), AnyState→JumpIdle (Jump &&
    ///      !Moving) + AnyState→JumpRunning (Jump && Moving) so the clip choice keys off the movement state, and
    ///      each jump state RETURNS on the GROUNDED edge → Locomotion (Grounded && Moving) or Idle (Grounded &&
    ///      !Moving). A controller missing these ships the jump anim inert OR stalls in the finished jump pose on
    ///      landing while still translating (the "floating" bug the Sponsor soak rejected).
    ///   3. THE FLOATING-BUG REGRESSION GUARD: the jump states must NOT return only on clip exit-time to Idle.
    ///      The land→Locomotion transition (Grounded && Moving) is the load-bearing fix — its absence is exactly
    ///      the floating bug (grounded, no sink, but stuck in a non-locomotion pose while translating).
    ///   4. REGRESSION (AC5 — OOS protection): no jump clip may be folded into the Walk<->Run blend tree. The
    ///      blend tree must STILL be exactly {Idle, Walk, Run} on the Speed param.
    /// </summary>
    public class CastawayJumpControllerTests
    {
        // AC import guard — BOTH jump FBX import as a GENERIC rig (CreateFromThisModel) with NON-LOOPING clips.
        [Test]
        public void JumpFbx_ImportsGeneric_WithNonLoopingClips_ByMovementState()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/Jump_idle.fbx", CharacterAssetGen.JumpIdleFbxPath,
                "the idle/standing jump must come from Jump_idle.fbx (without skin)");
            Assert.AreEqual("Assets/Art/Character/Castaway/Jump_running.fbx", CharacterAssetGen.JumpRunningFbxPath,
                "the walk/run jump must come from Jump_running.fbx (without skin)");

            AssertJumpFbxImportsGenericNonLooping(CharacterAssetGen.JumpIdleFbxPath, CharacterAssetGen.JumpIdleClip);
            AssertJumpFbxImportsGenericNonLooping(CharacterAssetGen.JumpRunningFbxPath, CharacterAssetGen.JumpRunningClip);
        }

        private static void AssertJumpFbxImportsGenericNonLooping(string fbxPath, string clipName)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            Assert.IsNotNull(importer, fbxPath + " must be importable");
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                fbxPath + " must import as a GENERIC rig (binds by transform path onto Idle's mesh, no Humanoid " +
                "muscle-space retarget — the retarget EXPLODED the mesh into a cone, 86ca8rdkp)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                fbxPath + " must create its own avatar (Generic path binds by transform path onto Idle's mesh)");

            AnimationClip jump = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") && c.name.Contains(clipName)) jump = c;
            Assert.IsNotNull(jump, fbxPath + " must contain a clip matching '" + clipName +
                "' (the Mixamo 'mixamo.com' take renamed on import — an exact 'Jump' match loops zero clips)");
            Assert.IsFalse(jump.isLooping, clipName + " must NOT loop — a jump is a one-shot " +
                "(a looping jump replays the push-off forever)");
        }

        // AC controller guard — Jump TRIGGER + GROUNDED bool, TWO jump states by movement state, AnyState entries
        // gated on the Moving bool, and land→{Locomotion if Moving | Idle} on the GROUNDED edge.
        [Test]
        public void Controller_HasTwoJumpStates_ByMovementState_AndReturnsOnGroundedEdge()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            // The Jump TRIGGER + the GROUNDED bool (alongside the kept Moving bool + Speed float).
            bool hasJumpTrigger = false, hasGroundedBool = false;
            foreach (var p in controller.parameters)
            {
                if (p.name == CastawayCharacter.JumpParam && p.type == AnimatorControllerParameterType.Trigger)
                    hasJumpTrigger = true;
                if (p.name == CastawayCharacter.GroundedParam && p.type == AnimatorControllerParameterType.Bool)
                    hasGroundedBool = true;
            }
            Assert.IsTrue(hasJumpTrigger, "the controller must have a Jump TRIGGER param (the one-shot Jump fire)");
            Assert.IsTrue(hasGroundedBool, "the controller must have a GROUNDED bool param — the jump→locomotion " +
                "return fires on its edge (THE floating-bug fix: land→Walk/Run on the same frame W is held)");

            var sm = controller.layers[0].stateMachine;

            AnimatorState jumpIdle = null, jumpRunning = null, idleState = null;
            AnimatorState locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "JumpIdle") jumpIdle = cs.state;
                if (cs.state.name == "JumpRunning") jumpRunning = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(jumpIdle, "the controller must have a 'JumpIdle' state (the idle/standing jump)");
            Assert.IsNotNull(jumpRunning, "the controller must have a 'JumpRunning' state (the walk/run jump)");
            Assert.IsNotNull(idleState, "the controller must still have an 'Idle' state");
            Assert.IsNotNull(locoState, "the controller must have a Locomotion (blend-tree) state");

            // Each jump state is motion'd to its OWN clip (idle clip vs running clip — not the same / not empty).
            var idleMotion = jumpIdle.motion as AnimationClip;
            var runMotion = jumpRunning.motion as AnimationClip;
            Assert.IsNotNull(idleMotion, "JumpIdle's motion must be an AnimationClip");
            Assert.IsNotNull(runMotion, "JumpRunning's motion must be an AnimationClip");
            Assert.IsTrue(idleMotion.name.Contains(CharacterAssetGen.JumpIdleClip),
                "JumpIdle must be motion'd to the CastawayJumpIdle clip (got '" + idleMotion.name + "')");
            Assert.IsTrue(runMotion.name.Contains(CharacterAssetGen.JumpRunningClip),
                "JumpRunning must be motion'd to the CastawayJumpRunning clip (got '" + runMotion.name + "')");

            // AnyState→JumpIdle gated on (Jump && !Moving); AnyState→JumpRunning gated on (Jump && Moving) — clip
            // choice keys off the SAME Moving bool the locomotion graph uses (a standing jump → idle clip, a
            // moving jump → running clip).
            Assert.IsTrue(AnyStateEntryGatedOn(sm, jumpIdle, AnimatorConditionMode.IfNot),
                "there must be an AnyState→JumpIdle transition gated on Jump trigger AND NOT Moving (standing jump)");
            Assert.IsTrue(AnyStateEntryGatedOn(sm, jumpRunning, AnimatorConditionMode.If),
                "there must be an AnyState→JumpRunning transition gated on Jump trigger AND Moving (walk/run jump)");

            // Both jump states return to Locomotion (Grounded && Moving) AND to Idle (Grounded && !Moving) — on
            // the grounded edge, NOT clip exit-time. The land→Locomotion transition is the floating-bug fix.
            AssertJumpReturnsOnGroundedEdge(jumpIdle, locoState, idleState, "JumpIdle");
            AssertJumpReturnsOnGroundedEdge(jumpRunning, locoState, idleState, "JumpRunning");
        }

        // THE FLOATING-BUG REGRESSION GUARD (the rework's reason): the jump states must NOT return to Idle ONLY on
        // clip exit-time. The pre-rework controller had a single Jump→Idle exit-time transition, which stalled the
        // character in the finished jump pose after landing while it kept translating (W held) until exit-time
        // elapsed → the "floating" percept. There must be a transition straight to LOCOMOTION on the grounded edge.
        [Test]
        public void JumpStates_ReturnToLocomotion_NotExitTimeIdleOnly_FloatingBugGuard()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist");
            var sm = controller.layers[0].stateMachine;

            AnimatorState locoState = null;
            foreach (var cs in sm.states) if (cs.state.motion is BlendTree) locoState = cs.state;
            Assert.IsNotNull(locoState, "the Locomotion blend-tree state must exist");

            foreach (var name in new[] { "JumpIdle", "JumpRunning" })
            {
                AnimatorState jump = null;
                foreach (var cs in sm.states) if (cs.state.name == name) jump = cs.state;
                Assert.IsNotNull(jump, "the '" + name + "' state must exist");

                bool toLocoOnGroundedMoving = false;
                bool anyExitTimeReturn = false;
                foreach (var t in jump.transitions)
                {
                    if (t.hasExitTime) anyExitTimeReturn = true;
                    if (t.destinationState != locoState) continue;
                    bool grounded = false, moving = false;
                    foreach (var c in t.conditions)
                    {
                        if (c.parameter == CastawayCharacter.GroundedParam && c.mode == AnimatorConditionMode.If) grounded = true;
                        if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) moving = true;
                    }
                    if (grounded && moving && !t.hasExitTime) toLocoOnGroundedMoving = true;
                }
                Assert.IsTrue(toLocoOnGroundedMoving, name + " must transition to Locomotion on (Grounded && Moving) " +
                    "with NO exit-time — landing with W held must resume Walk/Run on the grounded frame, NOT stall in " +
                    "the finished jump pose (the floating bug the Sponsor soak rejected).");
                Assert.IsFalse(anyExitTimeReturn, name + " must NOT use a clip exit-time return — landing is owned by " +
                    "the physical arc (the Grounded edge), not the clip length (the pre-rework exit-time→Idle path " +
                    "is exactly the floating bug).");
            }
        }

        // AC5 REGRESSION (OOS protection) — the Walk<->Run blend tree must be UNCHANGED: still exactly
        // {Idle, Walk, Run} on the Speed param, with NO jump clip folded in.
        [Test]
        public void WalkRunBlendTree_IsUnchanged_JumpNotFoldedIn()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist");

            BlendTree tree = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.motion is BlendTree bt) { tree = bt; break; }
            Assert.IsNotNull(tree, "the Locomotion blend tree must still exist (Walk<->Run, unchanged)");
            Assert.AreEqual(CastawayCharacter.SpeedParam, tree.blendParameter,
                "the blend tree must still blend on Speed (unchanged)");

            bool hasIdle = false, hasWalk = false, hasRun = false, hasJump = false;
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    if (clip.name.Contains(CharacterAssetGen.IdleClip)) hasIdle = true;
                    if (clip.name.Contains(CharacterAssetGen.WalkClip)) hasWalk = true;
                    if (clip.name.Contains(CharacterAssetGen.RunClip)) hasRun = true;
                    if (clip.name.Contains(CharacterAssetGen.JumpIdleClip) ||
                        clip.name.Contains(CharacterAssetGen.JumpRunningClip)) hasJump = true;
                }
            }
            Assert.IsTrue(hasIdle && hasWalk && hasRun,
                "the blend tree must still carry Idle+Walk+Run (the locomotion contract is unchanged by the jump)");
            Assert.IsFalse(hasJump, "no jump clip may be a blend-tree child — the jumps are OVERLAYING one-shot " +
                "states, NOT folded into the Walk<->Run blend (AC5 OOS protection)");
            Assert.AreEqual(3, tree.children.Length,
                "the Walk<->Run blend tree must still have exactly 3 children {Idle, Walk, Run}");
        }

        // An AnyState→<state> transition gated on the Jump trigger AND a Moving condition with the given mode.
        private static bool AnyStateEntryGatedOn(AnimatorStateMachine sm, AnimatorState dest, AnimatorConditionMode movingMode)
        {
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState != dest) continue;
                bool onJump = false, onMoving = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.JumpParam) onJump = true;
                    if (c.parameter == "Moving" && c.mode == movingMode) onMoving = true;
                }
                if (onJump && onMoving) return true;
            }
            return false;
        }

        private static void AssertJumpReturnsOnGroundedEdge(AnimatorState jump, AnimatorState loco,
                                                            AnimatorState idle, string label)
        {
            bool toLoco = false, toIdle = false;
            foreach (var t in jump.transitions)
            {
                bool grounded = false, movingIf = false, movingIfNot = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.GroundedParam && c.mode == AnimatorConditionMode.If) grounded = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) movingIf = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) movingIfNot = true;
                }
                if (t.destinationState == loco && grounded && movingIf) toLoco = true;
                if (t.destinationState == idle && grounded && movingIfNot) toIdle = true;
            }
            Assert.IsTrue(toLoco, label + " must transition to Locomotion on (Grounded && Moving) — land into Walk/Run");
            Assert.IsTrue(toIdle, label + " must transition to Idle on (Grounded && !Moving) — land standing still");
        }
    }
}
