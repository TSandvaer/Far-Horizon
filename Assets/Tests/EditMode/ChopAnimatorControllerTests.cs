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

        // AC controller guard (86caffwv5 — tree-chop's swing is now the per-class AXE swing state) — Chop TRIGGER +
        // ChopSpeed float, an 'AttackAxe' state on the CastawayAxeSwing clip with speedParameter = ChopSpeed,
        // AnyState→AttackAxe on (Chop && WeaponClass==0), and AttackAxe→{Locomotion if Moving | Idle}. ALSO pins the
        // RESERVED overhead 'Attack' state is NO LONGER reachable by Chop (Devon-NIT #1 double-fire guard).
        [Test]
        public void Controller_HasAxeSwingState_ChopTriggerAndSpeedParam_ReturnsToLocomotion()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            // The Chop TRIGGER + the ChopSpeed float (alongside the kept Moving/Speed/Jump/Grounded params).
            bool hasChopTrigger = false, hasChopSpeed = false, hasWeaponClass = false;
            float chopSpeedDefault = -1f;
            foreach (var p in controller.parameters)
            {
                if (p.name == CastawayCharacter.ChopParam && p.type == AnimatorControllerParameterType.Trigger)
                    hasChopTrigger = true;
                if (p.name == CastawayCharacter.ChopSpeedParam && p.type == AnimatorControllerParameterType.Float)
                { hasChopSpeed = true; chopSpeedDefault = p.defaultFloat; }
                if (p.name == CastawayCharacter.WeaponClassParam && p.type == AnimatorControllerParameterType.Int)
                    hasWeaponClass = true;
            }
            Assert.IsTrue(hasChopTrigger, "the controller must have a Chop TRIGGER param (the one-shot swing fire)");
            Assert.IsTrue(hasChopSpeed, "the controller must have a ChopSpeed FLOAT param (the swing-state speed multiplier)");
            Assert.IsTrue(hasWeaponClass, "the controller must have a WeaponClass INT param (the per-class swing selector, 86caffwv5)");
            Assert.AreEqual(1f, chopSpeedDefault, 1e-4f,
                "ChopSpeed must default to 1 (the authored clip speed) — a 0 default would FREEZE the swing");

            var sm = controller.layers[0].stateMachine;
            AnimatorState attackAxe = null, reservedAttack = null, idleState = null, locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "AttackAxe") attackAxe = cs.state;
                if (cs.state.name == "Attack") reservedAttack = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(attackAxe, "the controller must have an 'AttackAxe' state (the tree-chop's per-class swing)");
            Assert.IsNotNull(reservedAttack, "the RESERVED overhead 'Attack' state must still exist (future sword HEAVY)");
            Assert.IsNotNull(idleState, "the controller must still have an 'Idle' state");
            Assert.IsNotNull(locoState, "the controller must have a Locomotion (blend-tree) state");

            // AttackAxe is motion'd to the AXE swing clip and its speed is driven by the ChopSpeed param.
            var axeMotion = attackAxe.motion as AnimationClip;
            Assert.IsNotNull(axeMotion, "AttackAxe's motion must be an AnimationClip (the axe swing)");
            Assert.IsTrue(axeMotion.name.Contains(CharacterAssetGen.AxeSwingClip),
                "AttackAxe must be motion'd to the CastawayAxeSwing clip (got '" + axeMotion.name + "')");
            Assert.IsTrue(attackAxe.speedParameterActive, "AttackAxe's speed must be parameter-driven (tool-use speed)");
            Assert.AreEqual(CastawayCharacter.ChopSpeedParam, attackAxe.speedParameter,
                "AttackAxe's speedParameter must be ChopSpeed (so tool-use speed scales the swing playback rate live)");

            // AnyState→AttackAxe gated on the Chop trigger AND WeaponClass==Axe(0).
            bool anyToAxeOnChopAndClass = false;
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState != attackAxe) continue;
                bool onChop = false, onAxeClass = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.ChopParam) onChop = true;
                    if (c.parameter == CastawayCharacter.WeaponClassParam &&
                        c.mode == AnimatorConditionMode.Equals &&
                        Mathf.RoundToInt(c.threshold) == CastawayCharacter.WeaponClassAxe) onAxeClass = true;
                }
                if (onChop && onAxeClass) anyToAxeOnChopAndClass = true;
            }
            Assert.IsTrue(anyToAxeOnChopAndClass,
                "there must be an AnyState→AttackAxe transition gated on (Chop AND WeaponClass==0)");

            // DOUBLE-FIRE GUARD (Devon-NIT #1) — the RESERVED overhead 'Attack' state must have NO AnyState→Attack
            // transition on the Chop trigger (else Chop with WeaponClass==0 double-matches Attack + AttackAxe).
            bool reservedReachableByChop = false;
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState != reservedAttack) continue;
                foreach (var c in t.conditions)
                    if (c.parameter == CastawayCharacter.ChopParam) reservedReachableByChop = true;
            }
            Assert.IsFalse(reservedReachableByChop, "the RESERVED overhead 'Attack' state must NOT be reachable by an " +
                "AnyState→Attack-on-Chop transition — its ungated Chop transition was REMOVED so a class swing and " +
                "the legacy chop can never double-fire (86caffwv5 Devon-NIT #1); it is reserved for the sword HEAVY");

            // AttackAxe returns to Locomotion (Moving) AND to Idle (!Moving) — held-movement swing resumes locomotion.
            bool toLocoOnMoving = false, toIdleOnNotMoving = false;
            foreach (var t in attackAxe.transitions)
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
            Assert.IsTrue(toLocoOnMoving, "AttackAxe must transition to Locomotion on (Moving) — a swing while walking " +
                "resumes Walk/Run when it ends, NOT stall in the finished-swing pose (no-stall lesson)");
            Assert.IsTrue(toIdleOnNotMoving, "AttackAxe must transition to Idle on (!Moving) — a standing swing returns to idle");
        }

        // 86caf7a0p RE-ITER — the CLIP-COMPLETION CADENCE contract (the Sponsor soak-reject fix). The hold-chop
        // repeat gates the next swing on the swing CLIP finishing, so the authored melee clip must have a POSITIVE
        // length that is LONGER than the down-stroke impact delay (the impact lands mid-clip; the clip finishes
        // after). If the clip were zero-length or shorter than the impact delay, the cadence gate would collapse
        // toward the impact delay and the over-pacing soak bug would return. This pins the asset-side assumption
        // the runtime cadence (ChopTree.ComputeSwingDuration / CastawayCharacter.MeleeClipLength) relies on.
        [Test]
        public void AxeSwingClip_HasPositiveLength_LongerThanTheImpactDownStroke()
        {
            // 86caffwv5 — the tree-chop hold cadence now reads the AXE SWING clip (CastawayCharacter.MeleeClipLength
            // resolves the clip for WeaponClass==Axe), NOT the reserved overhead. Pin the axe swing (the live
            // cadence source) has a positive length longer than the impact down-stroke, exactly as the chop cadence
            // contract requires (the Sponsor soak-reject: 'the animation is not allowed to finish').
            AnimationClip axe = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.AttackAxeFbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    c.name.Contains(CharacterAssetGen.AxeSwingClip)) axe = c;
            Assert.IsNotNull(axe, "the CastawayAxeSwing clip must exist (the tree-chop cadence source)");

            Assert.Greater(axe.length, 0f,
                "the axe swing clip must have a POSITIVE authored length — the hold-chop cadence gates the next swing " +
                "on this clip finishing; a zero length would collapse the cadence to the impact delay (over-pacing)");

            // The runtime impact down-stroke default (ChopTree.swingImpactDelaySeconds = 0.4s) lands MID-clip; the
            // clip must be longer so 'wait for the clip to finish' is a STRICTER gate than 'wait for impact'.
            const float DefaultImpactDelay = 0.4f;
            Assert.Greater(axe.length, DefaultImpactDelay,
                "the axe swing clip must be LONGER than the impact down-stroke (~0.4s) — the clip-completion cadence " +
                "must be a stricter gate than the impact delay, else the swing animation would be cut off (the " +
                "Sponsor soak-reject: 'the animation is not allowed to finish ... 1 hit is not = on finished animation')");
        }

        // 86caf7a0p / 86caffwv5 — the runtime CastawayCharacter.MeleeClipName (the RESERVED overhead clip name, now
        // the future sword-HEAVY clip) must MATCH the editor-side CharacterAssetGen.MeleeClip the FBX is renamed to
        // on import. (The live tree-chop cadence source moved to the per-class swing names in 86caffwv5 — those
        // runtime↔editor mirrors are pinned in AttackSwingControllerTests; this pins the reserved-clip name match.)
        [Test]
        public void RuntimeMeleeClipName_MatchesTheImportedClipName()
        {
            Assert.AreEqual(CharacterAssetGen.MeleeClip, CastawayCharacter.MeleeClipName,
                "CastawayCharacter.MeleeClipName (the reserved overhead clip name) must equal " +
                "CharacterAssetGen.MeleeClip (what the FBX clip is renamed to on import); kept in sync so the " +
                "reserved state's clip resolves when the future sword-HEAVY ticket wires it");
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
