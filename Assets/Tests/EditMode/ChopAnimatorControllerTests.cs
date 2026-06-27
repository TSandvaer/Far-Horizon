using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// CHOP-SWING asset/controller guards (ticket 86caa4c5c change-(b) — the Sponsor's Mixamo melee clip
    /// REPLACES the rejected procedural ChopPoseDriver). These pin the asset-side contract the runtime chop
    /// (CastawayCharacter.TriggerChop) relies on, so the bug CLASSES can't recur silently in headless CI:
    ///
    ///   1. Melee_Attack.fbx imports like the jump one-shots (GENERIC rig, CreateFromThisModel) with a
    ///      NON-LOOPING clip — a chop is a ONE-SHOT strike (a looping swing replays forever). The Mixamo take
    ///      is "mixamo.com" → renamed CastawayMelee (an exact "Melee" match loops ZERO clips — the
    ///      T-pose-mid-swing class, same finding the Idle/Walk/Run/Jump imports honor).
    ///   2. The CastawayAnimator controller carries the Chop TRIGGER + the ChopSpeed float, an 'Attack' state
    ///      motion'd to the melee clip with its speedParameter = ChopSpeed (so tool-use speed scales the swing
    ///      playback rate live), an AnyState→Attack transition on the Chop trigger (a chop fires from Idle OR
    ///      mid-locomotion), and the Attack state RETURNS to Locomotion (Moving) / Idle (!Moving) so a chop
    ///      while walking resumes locomotion (the no-stall-locomotion lesson from the jump rework).
    ///   3. NO-STALL REGRESSION GUARD: the Attack state must transition to LOCOMOTION on (Moving) — its absence
    ///      would strand a held-movement chop in the finished-swing pose while the body translates ("floating").
    ///   4. REGRESSION (AC5 — OOS protection): the melee clip must NOT be folded into the Walk<->Run blend tree
    ///      (it is an OVERLAYING one-shot, like the jumps). The blend tree stays exactly {Idle, Walk, Run}.
    /// </summary>
    public class ChopAnimatorControllerTests
    {
        // AC import guard — Melee_Attack.fbx imports as a GENERIC rig (CreateFromThisModel) with a NON-LOOPING clip.
        [Test]
        public void MeleeFbx_ImportsGeneric_WithANonLoopingClip()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/Melee_Attack.fbx", CharacterAssetGen.MeleeFbxPath,
                "the chop swing must come from Melee_Attack.fbx (the Sponsor's Mixamo melee clip)");

            var importer = AssetImporter.GetAtPath(CharacterAssetGen.MeleeFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, CharacterAssetGen.MeleeFbxPath + " must be importable");
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                "Melee_Attack.fbx must import as a GENERIC rig (binds by transform path onto Idle's mesh, no " +
                "Humanoid muscle-space retarget — the retarget EXPLODED the mesh into a cone, 86ca8rdkp)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                "Melee_Attack.fbx must create its own avatar (the Generic path binds by transform path onto Idle)");

            AnimationClip melee = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.MeleeFbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    c.name.Contains(CharacterAssetGen.MeleeClip)) melee = c;
            Assert.IsNotNull(melee, "Melee_Attack.fbx must contain a clip matching '" + CharacterAssetGen.MeleeClip +
                "' (the Mixamo 'mixamo.com' take renamed on import — an exact 'Melee' match loops zero clips)");
            Assert.IsFalse(melee.isLooping, "CastawayMelee must NOT loop — a chop is a one-shot strike " +
                "(a looping swing replays the strike forever)");
        }

        // AC controller guard — Chop TRIGGER + ChopSpeed float, an 'Attack' state on the melee clip with its
        // speedParameter = ChopSpeed, AnyState→Attack on the trigger, and Attack→{Locomotion if Moving | Idle}.
        [Test]
        public void Controller_HasAttackState_ChopTriggerAndSpeedParam_ReturnsToLocomotion()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            // The Chop TRIGGER + the ChopSpeed float (alongside the kept Moving/Speed/Jump/Grounded params).
            bool hasChopTrigger = false, hasChopSpeed = false;
            float chopSpeedDefault = -1f;
            foreach (var p in controller.parameters)
            {
                if (p.name == CastawayCharacter.ChopParam && p.type == AnimatorControllerParameterType.Trigger)
                    hasChopTrigger = true;
                if (p.name == CastawayCharacter.ChopSpeedParam && p.type == AnimatorControllerParameterType.Float)
                { hasChopSpeed = true; chopSpeedDefault = p.defaultFloat; }
            }
            Assert.IsTrue(hasChopTrigger, "the controller must have a Chop TRIGGER param (the one-shot chop swing fire)");
            Assert.IsTrue(hasChopSpeed, "the controller must have a ChopSpeed FLOAT param (the Attack-state speed " +
                "multiplier — tool-use speed scales the swing playback rate via it)");
            Assert.AreEqual(1f, chopSpeedDefault, 1e-4f,
                "ChopSpeed must default to 1 (the authored melee clip speed) — a 0 default would FREEZE the swing");

            var sm = controller.layers[0].stateMachine;
            AnimatorState attack = null, idleState = null, locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "Attack") attack = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(attack, "the controller must have an 'Attack' state (the chop melee swing)");
            Assert.IsNotNull(idleState, "the controller must still have an 'Idle' state");
            Assert.IsNotNull(locoState, "the controller must have a Locomotion (blend-tree) state");

            // The Attack state is motion'd to the melee clip and its speed is driven by the ChopSpeed param.
            var meleeMotion = attack.motion as AnimationClip;
            Assert.IsNotNull(meleeMotion, "Attack's motion must be an AnimationClip (the melee swing)");
            Assert.IsTrue(meleeMotion.name.Contains(CharacterAssetGen.MeleeClip),
                "Attack must be motion'd to the CastawayMelee clip (got '" + meleeMotion.name + "')");
            Assert.IsTrue(attack.speedParameterActive, "Attack's speed must be parameter-driven (tool-use speed)");
            Assert.AreEqual(CastawayCharacter.ChopSpeedParam, attack.speedParameter,
                "Attack's speedParameter must be ChopSpeed (so tool-use speed scales the swing playback rate live)");

            // AnyState→Attack gated on the Chop trigger (a chop can fire from Idle OR mid-locomotion).
            bool anyToAttackOnChop = false;
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState != attack) continue;
                foreach (var c in t.conditions)
                    if (c.parameter == CastawayCharacter.ChopParam) anyToAttackOnChop = true;
            }
            Assert.IsTrue(anyToAttackOnChop, "there must be an AnyState→Attack transition gated on the Chop trigger");

            // Attack returns to Locomotion (Moving) AND to Idle (!Moving) — so a held-movement chop resumes
            // locomotion on the swing's end (NOT stranded in the finished pose while translating).
            bool toLocoOnMoving = false, toIdleOnNotMoving = false;
            foreach (var t in attack.transitions)
            {
                bool movingIf = false, movingIfNot = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) movingIf = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) movingIfNot = true;
                }
                if (t.destinationState == locoState && movingIf) toLocoOnMoving = true;
                if (t.destinationState == idleState && movingIfNot) toIdleOnNotMoving = true;
            }
            Assert.IsTrue(toLocoOnMoving, "Attack must transition to Locomotion on (Moving) — a chop while walking " +
                "resumes Walk/Run when the swing ends, NOT stall in the finished-swing pose (no-stall lesson)");
            Assert.IsTrue(toIdleOnNotMoving, "Attack must transition to Idle on (!Moving) — a standing chop returns to idle");
        }

        // AC5 REGRESSION (OOS protection) — the melee clip must NOT be folded into the Walk<->Run blend tree
        // (it is an OVERLAYING one-shot Attack state, like the jumps). The blend tree stays {Idle, Walk, Run}.
        [Test]
        public void WalkRunBlendTree_DoesNotContainTheMeleeClip()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist");

            BlendTree tree = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.motion is BlendTree bt) { tree = bt; break; }
            Assert.IsNotNull(tree, "the Locomotion blend tree must still exist (Walk<->Run)");

            bool hasMelee = false;
            foreach (var child in tree.children)
                if (child.motion is AnimationClip clip && clip.name.Contains(CharacterAssetGen.MeleeClip)) hasMelee = true;
            Assert.IsFalse(hasMelee, "the melee swing clip must NOT be a blend-tree child — it is an OVERLAYING " +
                "one-shot Attack state, NOT folded into the Walk<->Run blend (AC5 OOS protection)");
            Assert.AreEqual(3, tree.children.Length,
                "the Walk<->Run blend tree must still have exactly 3 children {Idle, Walk, Run}");
        }
    }
}
