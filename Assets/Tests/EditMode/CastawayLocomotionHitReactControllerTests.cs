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
    /// 86cackb3j — CROUCH + HIT-REACT clip-integration guards. These pin the asset-side contract for the 10
    /// newly-wired clips (Sneak Walk crouch-move, Crouching Idle, Stunned, Getting Up, Picking Up, and the five
    /// hit reactions Head/BigStomach/Stomach/Rib/Hit-To-Body) so the bug CLASS the ticket names — a clip silently
    /// "dropped" (an empty/missing AnimationClip reference, the run-inert / T-pose failure family) — cannot recur
    /// in headless CI before a soak.
    ///
    /// WHY EDITMODE FOR THE WIRING: a headless PlayMode Animator does NOT tick clips (Time.deltaTime≈0 — a
    /// documented false-green, unity-conventions.md §verification-lessons), so the CONTROLLER WIRING (which state
    /// each param routes to, that every state is motion'd to a real clip) is pinned here against the production
    /// controller asset; the PlayMode companion pins that the params actually route by manually advancing the
    /// Animator (CastawayLocomotionHitReactPlayModeTests).
    ///
    /// REGRESSION GUARD named by the ticket: Controller_EveryIntegratedState_HasANonEmptyClip catches the
    /// "clip dropped" class — if a future import rename or controller-build edit leaves a state's motion null or
    /// an FBX clip un-renamed (so FindClip returns null and the state ships motionless), this goes red.
    ///
    /// OOS PROTECTION (don't regress the upright locomotion): the Walk<->Run blend tree must STAY exactly
    /// {Idle, Walk, Run} — the crouch / hit-react / stunned / pickup clips are OVERLAY states (the Attack/Jump
    /// idiom), never folded into the blend tree.
    /// </summary>
    public class CastawayLocomotionHitReactControllerTests
    {
        private static AnimatorController LoadController()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(c, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);
            return c;
        }

        private static AnimationClip FindFbxClip(string fbxPath, string clipName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") && c.name.Contains(clipName))
                    return c;
            return null;
        }

        // AC import guard — every crouch/hit-react FBX imports as a GENERIC rig (CreateFromThisModel) — the
        // hard-won mesh-explosion fix (86ca8rdkp): a Humanoid retarget explodes the skinned mesh into a cone under
        // a scaled scene hierarchy; Generic binds by transform path onto Idle's mesh.
        [TestCase("Sneak Walk.fbx")]
        [TestCase("Crouching Idle.fbx")]
        [TestCase("Stunned.fbx")]
        [TestCase("Getting Up.fbx")]
        [TestCase("Picking Up.fbx")]
        [TestCase("Head Hit.fbx")]
        [TestCase("Big Stomach Hit.fbx")]
        [TestCase("Stomach Hit.fbx")]
        [TestCase("Rib Hit.fbx")]
        [TestCase("Hit To Body.fbx")]
        public void IntegratedFbx_ImportsGeneric(string fbxFile)
        {
            string path = "Assets/Art/Character/Castaway/" + fbxFile;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.IsNotNull(importer, path + " must be importable");
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                fbxFile + " must import as a GENERIC rig (binds by transform path onto Idle's mesh — the Humanoid " +
                "muscle-space retarget EXPLODED the mesh into a cone under a scaled hierarchy, 86ca8rdkp)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                fbxFile + " must create its own avatar (the Generic path binds by transform path onto Idle)");
        }

        // AC import guard — the SUSTAINED states LOOP (a non-looping crouch/stun hold freezes mid-cycle), the
        // ONE-SHOTS do NOT loop (a looping flinch/recovery/interaction replays forever — the ticket constraint).
        [Test]
        public void SustainedClipsLoop_OneShotsDoNot()
        {
            // Sustained → loop.
            foreach (var (fbx, name) in new[]
            {
                (CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip),
                (CharacterAssetGen.CrouchIdleFbxPath, CharacterAssetGen.CrouchIdleClip),
                (CharacterAssetGen.StunnedFbxPath, CharacterAssetGen.StunnedClip),
            })
            {
                var clip = FindFbxClip(fbx, name);
                Assert.IsNotNull(clip, fbx + " must contain a clip matching '" + name + "'");
                Assert.IsTrue(clip.isLooping, name + " must LOOP (a sustained crouch/stun hold freezes if it doesn't)");
            }

            // One-shot → no loop.
            foreach (var (fbx, name) in new[]
            {
                (CharacterAssetGen.GettingUpFbxPath, CharacterAssetGen.GettingUpClip),
                (CharacterAssetGen.PickingUpFbxPath, CharacterAssetGen.PickingUpClip),
                (CharacterAssetGen.HeadHitFbxPath, CharacterAssetGen.HeadHitClip),
                (CharacterAssetGen.BigStomachHitFbxPath, CharacterAssetGen.BigStomachHitClip),
                (CharacterAssetGen.StomachHitFbxPath, CharacterAssetGen.StomachHitClip),
                (CharacterAssetGen.RibHitFbxPath, CharacterAssetGen.RibHitClip),
                (CharacterAssetGen.HitToBodyFbxPath, CharacterAssetGen.HitToBodyClip),
            })
            {
                var clip = FindFbxClip(fbx, name);
                Assert.IsNotNull(clip, fbx + " must contain a clip matching '" + name + "'");
                Assert.IsFalse(clip.isLooping, name + " must NOT loop — a flinch/recovery/interaction is a one-shot " +
                    "(a looping reaction replays forever)");
            }
        }

        // AC1 REGRESSION GUARD (the "clip dropped" silent-killer) — EVERY integrated state must be motion'd to a
        // REAL, NON-EMPTY AnimationClip. A null/empty motion is exactly the silent regression the ticket names.
        [Test]
        public void Controller_EveryIntegratedState_HasANonEmptyClip()
        {
            var controller = LoadController();
            var byName = new Dictionary<string, AnimatorState>();
            foreach (var cs in controller.layers[0].stateMachine.states)
                byName[cs.state.name] = cs.state;

            // The 9 new clip-states (CrouchIdle/CrouchWalk, Stunned, GettingUp, PickingUp + the 5 hit-reacts).
            string[] expected =
            {
                "CrouchIdle", "CrouchWalk", "Stunned", "GettingUp", "PickingUp",
                "HitToBody", "HeadHit", "BigStomachHit", "StomachHit", "RibHit",
            };
            foreach (var name in expected)
            {
                Assert.IsTrue(byName.ContainsKey(name), "the controller must have a '" + name + "' state (86cackb3j wiring)");
                var clip = byName[name].motion as AnimationClip;
                Assert.IsNotNull(clip, "'" + name + "' must be motion'd to a real AnimationClip (NOT null/empty — the " +
                    "'clip dropped' silent-failure class the ticket guards: an un-renamed FBX clip or a build edit " +
                    "leaving the state motionless ships it as a T-pose).");
                Assert.Greater(clip.length, 0f, "'" + name + "' clip must have a positive authored length");
            }

            // And the already-wired upright states are untouched (regression: don't break Idle/Locomotion/jumps/Attack).
            foreach (var name in new[] { "Idle", "Locomotion", "JumpIdle", "JumpRunning", "Attack" })
                Assert.IsTrue(byName.ContainsKey(name), "the pre-existing '" + name + "' state must still exist (no regress)");
        }

        // The controller carries the new params with the right TYPES (Crouch/Stunned bool, Hit/PickUp trigger,
        // HitRegion int) alongside the kept locomotion/jump/chop params. A missing/wrong-type param ships the
        // clips UNREACHABLE.
        [Test]
        public void Controller_HasCrouchHitStunnedPickUpParams_WithCorrectTypes()
        {
            var controller = LoadController();
            var types = new Dictionary<string, AnimatorControllerParameterType>();
            foreach (var p in controller.parameters) types[p.name] = p.type;

            Assert.AreEqual(AnimatorControllerParameterType.Bool, types[CastawayCharacter.CrouchParam], "Crouch must be a bool");
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, types[CastawayCharacter.HitParam], "Hit must be a trigger");
            Assert.AreEqual(AnimatorControllerParameterType.Int, types[CastawayCharacter.HitRegionParam], "HitRegion must be an int");
            Assert.AreEqual(AnimatorControllerParameterType.Bool, types[CastawayCharacter.StunnedParam], "Stunned must be a bool");
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, types[CastawayCharacter.PickUpParam], "PickUp must be a trigger");

            // The kept params (regression: the new params must not have clobbered the locomotion/jump/chop set).
            Assert.IsTrue(types.ContainsKey(CastawayCharacter.MovingParam), "Moving must still exist");
            Assert.IsTrue(types.ContainsKey(CastawayCharacter.SpeedParam), "Speed must still exist");
            Assert.IsTrue(types.ContainsKey(CastawayCharacter.JumpParam), "Jump must still exist");
            Assert.IsTrue(types.ContainsKey(CastawayCharacter.ChopParam), "Chop must still exist");
        }

        // Hit-react routing — each region state has an AnyState→<state> transition gated on (Hit && HitRegion==value),
        // and each returns to Locomotion (Moving) / Idle (!Moving) so a mid-walk hit resumes locomotion on the flinch
        // end (the no-stall idiom). A missing region condition would fire the WRONG (or every) flinch on one Hit.
        [TestCase("HitToBody", CharacterAssetGen.HitRegionBody)]
        [TestCase("HeadHit", CharacterAssetGen.HitRegionHead)]
        [TestCase("BigStomachHit", CharacterAssetGen.HitRegionBigStomach)]
        [TestCase("StomachHit", CharacterAssetGen.HitRegionStomach)]
        [TestCase("RibHit", CharacterAssetGen.HitRegionRib)]
        public void HitReact_AnyStateOnHitAndRegion_ReturnsToLocomotionOrIdle(string stateName, int region)
        {
            var controller = LoadController();
            var sm = controller.layers[0].stateMachine;
            AnimatorState state = null, idleState = null, locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == stateName) state = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(state, "the '" + stateName + "' hit-react state must exist");
            Assert.IsNotNull(idleState); Assert.IsNotNull(locoState);

            bool anyOnHitAndRegion = false;
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState != state) continue;
                bool hit = false, regionMatch = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.HitParam) hit = true;
                    if (c.parameter == CastawayCharacter.HitRegionParam &&
                        c.mode == AnimatorConditionMode.Equals && Mathf.RoundToInt(c.threshold) == region) regionMatch = true;
                }
                if (hit && regionMatch) anyOnHitAndRegion = true;
            }
            Assert.IsTrue(anyOnHitAndRegion, "there must be an AnyState→" + stateName + " transition gated on (Hit && " +
                "HitRegion==" + region + ") — else one Hit fires the wrong region (or every region) flinch");

            bool toLocoOnMoving = false, toIdleOnNotMoving = false;
            foreach (var t in state.transitions)
            {
                bool mIf = false, mIfNot = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) mIf = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) mIfNot = true;
                }
                if (t.destinationState == locoState && mIf) toLocoOnMoving = true;
                if (t.destinationState == idleState && mIfNot) toIdleOnNotMoving = true;
            }
            Assert.IsTrue(toLocoOnMoving, stateName + " must return to Locomotion on (Moving) — a hit while walking " +
                "resumes Walk/Run when the flinch ends (no-stall idiom)");
            Assert.IsTrue(toIdleOnNotMoving, stateName + " must return to Idle on (!Moving)");
        }

        // Crouch lane — AnyState→CrouchWalk on (Crouch && Moving), AnyState→CrouchIdle on (Crouch && !Moving); each
        // releases back to Locomotion (Moving) / Idle on (!Crouch). The clips are reachable + the lane flips on Moving.
        [Test]
        public void CrouchLane_EngagesOnCrouch_ReleasesOnNotCrouch()
        {
            var controller = LoadController();
            var sm = controller.layers[0].stateMachine;
            AnimatorState crouchIdle = null, crouchWalk = null, idleState = null, locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "CrouchIdle") crouchIdle = cs.state;
                if (cs.state.name == "CrouchWalk") crouchWalk = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(crouchIdle); Assert.IsNotNull(crouchWalk);

            bool anyToCrouchWalkOnCrouchMoving = false, anyToCrouchIdleOnCrouchNotMoving = false;
            foreach (var t in sm.anyStateTransitions)
            {
                bool crouch = false, mIf = false, mIfNot = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.CrouchParam && c.mode == AnimatorConditionMode.If) crouch = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) mIf = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) mIfNot = true;
                }
                if (t.destinationState == crouchWalk && crouch && mIf) anyToCrouchWalkOnCrouchMoving = true;
                if (t.destinationState == crouchIdle && crouch && mIfNot) anyToCrouchIdleOnCrouchNotMoving = true;
            }
            Assert.IsTrue(anyToCrouchWalkOnCrouchMoving, "AnyState→CrouchWalk must be gated on (Crouch && Moving)");
            Assert.IsTrue(anyToCrouchIdleOnCrouchNotMoving, "AnyState→CrouchIdle must be gated on (Crouch && !Moving)");

            // Each crouch state releases on (!Crouch) → Locomotion (Moving) / Idle.
            foreach (var crouchState in new[] { crouchIdle, crouchWalk })
            {
                bool releasesToLoco = false, releasesToIdle = false;
                foreach (var t in crouchState.transitions)
                {
                    bool notCrouch = false, mIf = false, mIfNot = false;
                    foreach (var c in t.conditions)
                    {
                        if (c.parameter == CastawayCharacter.CrouchParam && c.mode == AnimatorConditionMode.IfNot) notCrouch = true;
                        if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) mIf = true;
                        if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) mIfNot = true;
                    }
                    if (t.destinationState == locoState && notCrouch && mIf) releasesToLoco = true;
                    if (t.destinationState == idleState && notCrouch && mIfNot) releasesToIdle = true;
                }
                Assert.IsTrue(releasesToLoco, crouchState.name + " must release to Locomotion on (!Crouch && Moving)");
                Assert.IsTrue(releasesToIdle, crouchState.name + " must release to Idle on (!Crouch && !Moving)");
            }
        }

        // Stunned → GettingUp recovery chain: AnyState→Stunned on (Stunned), Stunned→GettingUp on (!Stunned), and
        // GettingUp is a DEDICATED transition (NOT AnyState) so the recovery only plays as the END of a stun.
        [Test]
        public void Stunned_HoldsThenRecoversViaGettingUp()
        {
            var controller = LoadController();
            var sm = controller.layers[0].stateMachine;
            AnimatorState stunned = null, gettingUp = null, idleState = null, locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "Stunned") stunned = cs.state;
                if (cs.state.name == "GettingUp") gettingUp = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(stunned); Assert.IsNotNull(gettingUp);

            bool anyToStunnedOnStunned = false;
            foreach (var t in sm.anyStateTransitions)
                if (t.destinationState == stunned)
                    foreach (var c in t.conditions)
                        if (c.parameter == CastawayCharacter.StunnedParam && c.mode == AnimatorConditionMode.If)
                            anyToStunnedOnStunned = true;
            Assert.IsTrue(anyToStunnedOnStunned, "AnyState→Stunned must be gated on (Stunned) — a stun can land from any state");

            // GettingUp must NOT be an AnyState target (it only plays as a stun's END, never spuriously).
            foreach (var t in sm.anyStateTransitions)
                Assert.AreNotEqual(gettingUp, t.destinationState,
                    "GettingUp must NOT be an AnyState target — the recovery only plays as the END of a stun");

            bool stunnedToRecover = false;
            foreach (var t in stunned.transitions)
                if (t.destinationState == gettingUp)
                    foreach (var c in t.conditions)
                        if (c.parameter == CastawayCharacter.StunnedParam && c.mode == AnimatorConditionMode.IfNot)
                            stunnedToRecover = true;
            Assert.IsTrue(stunnedToRecover, "Stunned→GettingUp must fire on (!Stunned) — the knocked-down loop ends into recovery");

            // GettingUp returns to Locomotion/Idle on exit.
            bool toLoco = false, toIdle = false;
            foreach (var t in gettingUp.transitions)
            {
                if (t.destinationState == locoState) toLoco = true;
                if (t.destinationState == idleState) toIdle = true;
            }
            Assert.IsTrue(toLoco && toIdle, "GettingUp must return to Locomotion AND Idle on exit (no-stall recovery)");
        }

        // PickingUp — AnyState→PickingUp on the PickUp trigger; returns to Locomotion/Idle on exit.
        [Test]
        public void PickingUp_AnyStateOnPickUpTrigger_ReturnsToLocomotionOrIdle()
        {
            var controller = LoadController();
            var sm = controller.layers[0].stateMachine;
            AnimatorState pickingUp = null, idleState = null, locoState = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "PickingUp") pickingUp = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(pickingUp);

            bool anyOnPickUp = false;
            foreach (var t in sm.anyStateTransitions)
                if (t.destinationState == pickingUp)
                    foreach (var c in t.conditions)
                        if (c.parameter == CastawayCharacter.PickUpParam) anyOnPickUp = true;
            Assert.IsTrue(anyOnPickUp, "AnyState→PickingUp must be gated on the PickUp trigger");

            bool toLoco = false, toIdle = false;
            foreach (var t in pickingUp.transitions)
            {
                if (t.destinationState == locoState) toLoco = true;
                if (t.destinationState == idleState) toIdle = true;
            }
            Assert.IsTrue(toLoco && toIdle, "PickingUp must return to Locomotion AND Idle on exit (no-stall)");
        }

        // OOS PROTECTION — the Walk<->Run blend tree must STAY exactly {Idle, Walk, Run}. None of the new clips
        // (crouch / hit-react / stunned / pickup) may be folded into it — they are OVERLAY states (the Attack/Jump
        // idiom). A crouch/hit clip leaking into the blend tree would corrupt the upright locomotion blend.
        [Test]
        public void WalkRunBlendTree_StaysExactlyIdleWalkRun()
        {
            var controller = LoadController();
            BlendTree tree = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.motion is BlendTree bt) { tree = bt; break; }
            Assert.IsNotNull(tree, "the Locomotion blend tree must still exist");
            Assert.AreEqual(3, tree.children.Length, "the Walk<->Run blend tree must still have exactly 3 children {Idle, Walk, Run}");

            string[] forbidden =
            {
                CharacterAssetGen.CrouchWalkClip, CharacterAssetGen.CrouchIdleClip, CharacterAssetGen.StunnedClip,
                CharacterAssetGen.GettingUpClip, CharacterAssetGen.PickingUpClip, CharacterAssetGen.HeadHitClip,
                CharacterAssetGen.BigStomachHitClip, CharacterAssetGen.StomachHitClip, CharacterAssetGen.RibHitClip,
                CharacterAssetGen.HitToBodyClip,
            };
            foreach (var child in tree.children)
            {
                var clip = child.motion as AnimationClip;
                if (clip == null) continue;
                foreach (var f in forbidden)
                    Assert.IsFalse(clip.name.Contains(f), "the blend tree must NOT contain the overlay clip '" + f +
                        "' — crouch/hit-react/stunned/pickup are OVERLAY states, NOT folded into the upright Walk<->Run blend");
            }
        }

        // 86caa3kur RE-SOAK loop-hitch GUARD (candidate #2 — foot-sync stall — ruled out IN SOURCE, pinned here).
        // The sneak-walk loop hitch was suspected to be foot-sync (#186 LocoSpeedMul) STALLING the Sneak Walk clip
        // at the slow sneak speed. The trace + this guard CONFIRM it cannot: the CrouchWalk state must NOT read a
        // speedParameter, so LocoSpeedMul (which CastawayCharacter drives every frame) NEVER scales the crouch-walk
        // clip's playback rate — CrouchWalk always plays at its authored cadence. ONLY the upright Locomotion blend
        // tree reads LocoSpeedMul (that's where foot-sync belongs). A future edit that wires foot-sync onto CrouchWalk
        // would re-introduce the candidate-#2 risk (a velocity dip → near-zero clip speed → a stall) → this goes red.
        [Test]
        public void CrouchWalk_DoesNotReadFootSyncSpeedParameter_OnlyLocomotionDoes()
        {
            var controller = LoadController();
            AnimatorState crouchWalk = null, locoState = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
            {
                if (cs.state.name == "CrouchWalk") crouchWalk = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(crouchWalk, "the CrouchWalk state must exist");
            Assert.IsNotNull(locoState, "the Locomotion blend-tree state must exist");

            Assert.IsFalse(crouchWalk.speedParameterActive,
                "CrouchWalk must NOT use a speedParameter — foot-sync's LocoSpeedMul must NOT scale the Sneak Walk " +
                "clip's playback rate (a velocity dip would stall it = the candidate-#2 loop hitch). The crouch-walk " +
                "clip plays at its authored cadence; only the upright Locomotion blend tree rides LocoSpeedMul.");

            // And the upright Locomotion DOES read LocoSpeedMul (regression: don't accidentally strip foot-sync from
            // the upright walk/run while fixing the sneak — that's the approved #186 behavior).
            Assert.IsTrue(locoState.speedParameterActive,
                "the upright Locomotion blend tree must STILL read its foot-sync speedParameter (don't regress #186)");
            Assert.AreEqual(CharacterAssetGen.LocoSpeedMulParam, locoState.speedParameter,
                "the Locomotion blend tree's speedParameter must be LocoSpeedMul (the #186 foot-sync param)");
        }

        // 86caa3kur RE-SOAK loop-hitch GUARD (candidate #3 — state re-entry). The "two steps repeated, lags between
        // each" symptom would be produced by the AnyState→CrouchWalk transition RE-FIRING each cycle (restarting the
        // looping clip from normalizedTime 0). canTransitionToSelf=false is the structural guard that a steady crouch
        // hold does NOT restart the clip via a self-re-entry — pinned so a future edit can't silently flip it on.
        [Test]
        public void CrouchLane_AnyStateTransitions_DoNotTransitionToSelf()
        {
            var controller = LoadController();
            var sm = controller.layers[0].stateMachine;
            AnimatorState crouchIdle = null, crouchWalk = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "CrouchIdle") crouchIdle = cs.state;
                if (cs.state.name == "CrouchWalk") crouchWalk = cs.state;
            }
            Assert.IsNotNull(crouchIdle); Assert.IsNotNull(crouchWalk);

            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState == crouchWalk || t.destinationState == crouchIdle)
                    Assert.IsFalse(t.canTransitionToSelf,
                        "the AnyState→" + t.destinationState.name + " crouch transition must have " +
                        "canTransitionToSelf=false — else a steady crouch hold re-fires the transition each cycle, " +
                        "restarting the looping clip from 0 = the 'two steps repeated, lags between each' loop hitch (candidate #3)");
            }
        }

        // The editor-side clip names must MATCH the runtime-side param names (the project idiom: every controller
        // param has a CastawayCharacter mirror). A drift makes a future system SetBool/SetTrigger the wrong name.
        [Test]
        public void EditorAndRuntimeParamNames_Match()
        {
            Assert.AreEqual(CastawayCharacter.CrouchParam, CharacterAssetGen.CrouchParam, "Crouch param name must match");
            Assert.AreEqual(CastawayCharacter.HitParam, CharacterAssetGen.HitParam, "Hit param name must match");
            Assert.AreEqual(CastawayCharacter.HitRegionParam, CharacterAssetGen.HitRegionParam, "HitRegion param name must match");
            Assert.AreEqual(CastawayCharacter.StunnedParam, CharacterAssetGen.StunnedParam, "Stunned param name must match");
            Assert.AreEqual(CastawayCharacter.PickUpParam, CharacterAssetGen.PickUpParam, "PickUp param name must match");
            Assert.AreEqual(CastawayCharacter.HitRegionBody, CharacterAssetGen.HitRegionBody, "HitRegionBody must match");
            Assert.AreEqual(CastawayCharacter.HitRegionHead, CharacterAssetGen.HitRegionHead, "HitRegionHead must match");
            Assert.AreEqual(CastawayCharacter.HitRegionBigStomach, CharacterAssetGen.HitRegionBigStomach, "HitRegionBigStomach must match");
            Assert.AreEqual(CastawayCharacter.HitRegionStomach, CharacterAssetGen.HitRegionStomach, "HitRegionStomach must match");
            Assert.AreEqual(CastawayCharacter.HitRegionRib, CharacterAssetGen.HitRegionRib, "HitRegionRib must match");
        }
    }
}
