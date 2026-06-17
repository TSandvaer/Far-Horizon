using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC1 + AC2 + AC5 JUMP asset/controller guards (ticket 86ca9yq3q — jump on Space). These pin the asset-side
    /// contract the runtime jump relies on so the bug CLASSES can't recur silently in headless CI before a soak:
    ///
    ///   1. Jump.fbx imports like Walking/Running.fbx (GENERIC rig, CreateFromThisModel) BUT with a NON-LOOPING
    ///      Jump clip — a jump is a ONE-SHOT (a looping jump replays the push-off forever). The Mixamo take is
    ///      "mixamo.com" → renamed CastawayJump (an exact "Jump" match would loop ZERO clips — the
    ///      T-pose-mid-jump class). (AC2 import.)
    ///   2. The CastawayAnimator controller carries the Jump TRIGGER param + a Jump STATE whose motion is the
    ///      Jump clip + an AnyState transition gated on the Jump trigger (so the castaway can jump from idle OR
    ///      mid-locomotion — AC1) + an exit-time transition BACK to Idle (control returns on landing — AC1). A
    ///      controller missing the Jump state/trigger ships the jump anim inert (the body hops but never plays
    ///      the clip) — the "motion in source but not wired" silent-failure guard.
    ///   3. REGRESSION (AC5 — OOS protection): the Jump must NOT have been folded into the Walk<->Run blend tree.
    ///      The blend tree must STILL be exactly {Idle, Walk, Run} on the Speed param — adding the Jump here would
    ///      break the walk/run separation those (separate) tickets own. The Jump is an OVERLAYING one-shot state,
    ///      never a blend-tree child.
    /// </summary>
    public class CastawayJumpControllerTests
    {
        // AC2 import guard — Jump.fbx imports as a GENERIC rig (CreateFromThisModel) with a NON-LOOPING Jump clip.
        [Test]
        public void JumpFbx_ImportsGeneric_WithNonLoopingJumpClip()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/Jump.fbx", CharacterAssetGen.JumpFbxPath,
                "the Jump clip must come from the Hyper3D castaway Jump FBX (without skin)");

            var jumpImporter = AssetImporter.GetAtPath(CharacterAssetGen.JumpFbxPath) as ModelImporter;
            Assert.IsNotNull(jumpImporter, "Jump.fbx must be importable at " + CharacterAssetGen.JumpFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, jumpImporter.animationType,
                "Jump.fbx must import as a GENERIC rig (binds by transform path onto Idle's mesh, no Humanoid " +
                "muscle-space retarget — the retarget EXPLODED the mesh into a cone, 86ca8rdkp)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, jumpImporter.avatarSetup,
                "Jump.fbx must create its own avatar (Generic path binds by transform path onto Idle's mesh)");

            AnimationClip jump = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.JumpFbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    c.name.Contains(CharacterAssetGen.JumpClip)) jump = c;
            Assert.IsNotNull(jump, "Jump.fbx must contain a clip matching '" + CharacterAssetGen.JumpClip +
                "' (the Mixamo 'mixamo.com' take renamed on import — an exact 'Jump' match loops zero clips)");
            Assert.IsFalse(jump.isLooping, CharacterAssetGen.JumpClip + " must NOT loop — a jump is a one-shot " +
                "(a looping jump replays the push-off forever; AC2)");
        }

        // AC1 controller guard — the controller has a Jump TRIGGER, a Jump STATE motion'd to the Jump clip, an
        // AnyState→Jump transition on the trigger (jump from idle OR moving), and a Jump→Idle exit-time return.
        [Test]
        public void Controller_HasJumpTrigger_JumpState_AnyStateEntry_AndExitBackToIdle()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            // The Jump TRIGGER param (alongside the kept Moving bool + Speed float).
            bool hasJumpTrigger = false;
            foreach (var p in controller.parameters)
                if (p.name == CastawayCharacter.JumpParam && p.type == AnimatorControllerParameterType.Trigger)
                    hasJumpTrigger = true;
            Assert.IsTrue(hasJumpTrigger, "the controller must have a Jump TRIGGER param (the one-shot Jump fire) — " +
                "CastawayCharacter pulses it on the rising edge of a jump");

            var sm = controller.layers[0].stateMachine;

            // The Jump STATE must exist + be motion'd to the Jump clip (not empty — else the body hops but no anim).
            AnimatorState jumpState = null;
            foreach (var cs in sm.states)
                if (cs.state.name == "Jump") jumpState = cs.state;
            Assert.IsNotNull(jumpState, "the controller must have a 'Jump' state (the one-shot jump clip host)");
            var jumpMotion = jumpState.motion as AnimationClip;
            Assert.IsNotNull(jumpMotion, "the Jump state's motion must be an AnimationClip (the Jump clip)");
            Assert.IsTrue(jumpMotion.name.Contains(CharacterAssetGen.JumpClip),
                "the Jump state must be motion'd to the CastawayJump clip (got '" + jumpMotion.name + "') — else " +
                "the jump anim is inert");

            // An AnyState transition into Jump gated on the Jump trigger → jump from idle AND from locomotion (AC1).
            bool anyToJumpOnTrigger = false;
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState != jumpState) continue;
                foreach (var c in t.conditions)
                    if (c.parameter == CastawayCharacter.JumpParam) anyToJumpOnTrigger = true;
            }
            Assert.IsTrue(anyToJumpOnTrigger, "there must be an AnyState→Jump transition gated on the Jump trigger " +
                "so the castaway can jump from idle OR mid-locomotion (AC1)");

            // A Jump→Idle exit-time transition → control returns to the idle/locomotion graph on landing (AC1).
            bool jumpReturnsToIdle = false;
            foreach (var t in jumpState.transitions)
                if (t.destinationState != null && t.destinationState.name == "Idle" && t.hasExitTime)
                    jumpReturnsToIdle = true;
            Assert.IsTrue(jumpReturnsToIdle, "the Jump state must transition back to Idle on exit-time so control " +
                "returns after the arc (the Idle<->Locomotion graph re-acquires Walk/Run if still moving — AC1)");
        }

        // AC5 REGRESSION (OOS protection) — the Walk<->Run blend tree must be UNCHANGED: still exactly
        // {Idle, Walk, Run} on the Speed param, with NO Jump clip folded in. Adding the Jump as a blend-tree
        // child would corrupt the walk/run separation those separate tickets (86ca9yq2x/yq34) own.
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
                    if (clip.name.Contains(CharacterAssetGen.JumpClip)) hasJump = true;
                }
            }
            Assert.IsTrue(hasIdle && hasWalk && hasRun,
                "the blend tree must still carry Idle+Walk+Run (the locomotion contract is unchanged by the jump)");
            Assert.IsFalse(hasJump, "the Jump clip must NOT be a blend-tree child — the Jump is an OVERLAYING " +
                "one-shot state, NOT folded into the Walk<->Run blend (AC5 OOS protection: run/walk are their own " +
                "tickets and must not change)");
            Assert.AreEqual(3, tree.children.Length,
                "the Walk<->Run blend tree must still have exactly 3 children {Idle, Walk, Run} — a 4th child means " +
                "the jump (or something) was folded into the locomotion blend (regression)");
        }
    }
}
