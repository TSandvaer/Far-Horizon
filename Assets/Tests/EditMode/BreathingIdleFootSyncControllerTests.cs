using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cackb3j RE-SOAK guards (idle + walk feel fix). The Sponsor soak FAILED on idle (too static) + walk
    /// (legs too slow vs move-speed → feet skate; body slides before feet engage on start). Three fixes, each
    /// pinned here at the BUG-CLASS level (not one feel value), with the run/jump/transitions kept intact:
    ///
    ///   PART 1 — IDLE: the Idle STATE + the blend-tree @0 idle FLOOR play the BREATHING idle clip
    ///            (CastawayBreathingIdle, looping) — "calm but clearly alive", replacing the static idle.
    ///   PART 2 — FOOT-SYNC: the Locomotion state has a speedParameter (LocoSpeedMul) so the leg cadence is
    ///            scaled to move-speed; CastawayCharacter.ComputeFootSyncMul speeds the walk up while keeping
    ///            run ≈1 (the approved run cadence). Runtime param mirror matches.
    ///   PART 3 — ASYMMETRIC DAMP: a SHORT ramp-UP (speedDampTimeUp) for a snappy start + the existing longer
    ///            ramp-DOWN (speedDampTime) for the approved smooth stop.
    ///
    /// CROSS-LANE: the Walk<->Run blend tree (#68), the Idle<->Locomotion crossfades (86caay44r), and the
    /// jump-return / hit-react / crouch wiring (86cackb3j Part 1 of the original PR) must NOT regress.
    /// </summary>
    public class BreathingIdleFootSyncControllerTests
    {
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

        // ===== PART 1 — IDLE (breathing) =====

        [Test]
        public void BreathingIdle_Fbx_ImportsALoopingClip()
        {
            // The Sponsor-sourced Breathing Idle.fbx must import a clip matching the renamed BreathingIdleClip
            // and LOOP (a sustained at-rest breathing cycle — a non-looping one would freeze mid-breath).
            AnimationClip breathing = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.BreathingIdleFbxPath))
                if (o is AnimationClip c && c.name.Contains(CharacterAssetGen.BreathingIdleClip)
                    && !c.name.StartsWith("__preview__")) breathing = c;
            Assert.IsNotNull(breathing, "Breathing Idle.fbx must contain a clip matching '" +
                CharacterAssetGen.BreathingIdleClip + "' (imported Generic, renamed from the 'mixamo.com' take)");
            Assert.IsTrue(breathing.isLooping, CharacterAssetGen.BreathingIdleClip +
                " must LOOP (the at-rest breathing cycle — a non-looping clip freezes mid-breath = the 'too static' complaint returns)");
            Assert.Greater(breathing.length, 0.1f, "the breathing idle clip must have real length (not an empty/T-pose clip)");
        }

        [Test]
        public void IdleState_PlaysTheBreathingClip_NotTheStaticIdle()
        {
            var c = LoadController();
            var idle = FindState(c, "Idle");
            Assert.IsNotNull(idle, "the controller must have an Idle state");
            Assert.IsInstanceOf<AnimationClip>(idle.motion,
                "the Idle state must play a single clip (the breathing idle), not a blend tree");
            var clip = (AnimationClip)idle.motion;
            Assert.IsTrue(clip.name.Contains(CharacterAssetGen.BreathingIdleClip),
                "the Idle state must play the BREATHING idle clip ('" + CharacterAssetGen.BreathingIdleClip +
                "') — 'calm but clearly alive', NOT the old static idle. Got '" + clip.name + "'.");
        }

        [Test]
        public void BlendTreeIdleFloor_IsTheBreathingClip_SoEdgeOfMotionStaysAlive()
        {
            // The @0 (IdleBlendSpeed) floor of the Locomotion blend tree is also the breathing idle, so a tiny
            // residual speed at the edge of motion reads "alive" rather than the old static idle.
            var c = LoadController();
            var loco = FindState(c, "Locomotion");
            Assert.IsNotNull(loco);
            Assert.IsInstanceOf<BlendTree>(loco.motion);
            var tree = (BlendTree)loco.motion;
            AnimationClip floorClip = null;
            foreach (var child in tree.children)
                if (child.motion is AnimationClip cl && Mathf.Approximately(child.threshold, CharacterAssetGen.IdleBlendSpeed))
                    floorClip = cl;
            Assert.IsNotNull(floorClip, "the blend tree must have an idle clip at the @0 floor threshold");
            Assert.IsTrue(floorClip.name.Contains(CharacterAssetGen.BreathingIdleClip),
                "the blend-tree @0 idle floor must be the BREATHING idle clip (the edge-of-motion at-rest pose " +
                "stays alive). Got '" + floorClip.name + "'.");
        }

        // ===== PART 2 — FOOT-SYNC =====

        [Test]
        public void LocomotionState_HasFootSyncSpeedParameter()
        {
            // The Locomotion state's playback speed must read the LocoSpeedMul param (foot-sync), so the leg
            // cadence is scaled to move-speed. Without speedParameterActive the legs play at authored cadence
            // (the skating walk the Sponsor flagged).
            var c = LoadController();
            var loco = FindState(c, "Locomotion");
            Assert.IsNotNull(loco);
            Assert.IsTrue(loco.speedParameterActive,
                "the Locomotion state must use a speedParameter (foot-sync) so the leg cadence tracks move-speed");
            Assert.AreEqual(CastawayCharacter.LocoSpeedMulParam, loco.speedParameter,
                "the Locomotion speedParameter must be LocoSpeedMul (the foot-sync multiplier CastawayCharacter drives)");
        }

        [Test]
        public void Controller_HasLocoSpeedMulFloatParam_DefaultOne()
        {
            var c = LoadController();
            AnimatorControllerParameter p = null;
            foreach (var prm in c.parameters)
                if (prm.name == CastawayCharacter.LocoSpeedMulParam) p = prm;
            Assert.IsNotNull(p, "the controller must declare the LocoSpeedMul param (foot-sync)");
            Assert.AreEqual(AnimatorControllerParameterType.Float, p.type, "LocoSpeedMul must be a float");
            Assert.That(p.defaultFloat, Is.EqualTo(1f).Within(1e-4f),
                "LocoSpeedMul must default to 1 (authored cadence) so an unbound rig plays the clip unscaled");
        }

        [Test]
        public void LocoSpeedMulParamName_MatchesRuntimeMirror()
        {
            Assert.AreEqual(CastawayCharacter.LocoSpeedMulParam, CharacterAssetGen.LocoSpeedMulParam,
                "the LocoSpeedMul param name must match between the runtime mirror + the editor controller builder");
        }

        [Test]
        public void FootSyncMul_SpeedsUpWalk_KeepsRunAtApproved()
        {
            // The PURE foot-sync math is the load-bearing contract: a faster move-speed yields a faster (or
            // equal) leg cadence, the WALK speed is sped up (>1, the skating fix), and the RUN speed stays ≈1
            // (run was APPROVED — must NOT regress) when runStrideRef == runSpeed.
            const float walkBlend = 5.5f, runBlend = 9.5f;
            const float walkRef = 3.6f, runRef = 9.5f;   // the shipped defaults
            const float mulMin = 0.5f, mulMax = 2.5f;

            float walkMul = CastawayCharacter.ComputeFootSyncMul(walkBlend, walkBlend, runBlend, walkRef, runRef, mulMin, mulMax);
            float runMul = CastawayCharacter.ComputeFootSyncMul(runBlend, walkBlend, runBlend, walkRef, runRef, mulMin, mulMax);

            Assert.Greater(walkMul, 1.05f,
                "at walk speed the foot-sync must SPEED UP the WALK clip (>1) so the feet plant instead of skating " +
                "(the Sponsor's 'walk legs too slow' report). Got " + walkMul.ToString("F3"));
            Assert.That(runMul, Is.EqualTo(1f).Within(0.05f),
                "at run speed (runStrideRef == runSpeed) the foot-sync multiplier must stay ≈1 — the APPROVED run " +
                "cadence must NOT regress. Got " + runMul.ToString("F3"));
            // NOTE: the multiplier is per-clip SCALE, NOT absolute cadence — it legitimately decreases walk->run
            // because the Run clip is authored for a faster ground speed than the Walk clip (so it needs less
            // speed-up). The contract is the two endpoints above (walk sped up, run unchanged), not monotonicity.
            float midMul = CastawayCharacter.ComputeFootSyncMul((walkBlend + runBlend) * 0.5f, walkBlend, runBlend,
                                                                walkRef, runRef, mulMin, mulMax);
            Assert.Greater(midMul, runMul - 1e-3f,
                "a mid-band speed must keep at least the run multiplier (the blended stride ref never over-slows the legs)");
        }

        [Test]
        public void FootSyncMul_IsClampedToTheSaneBand()
        {
            // A velocity spike/dip can't freeze (0) or blur (huge) the legs.
            const float walkBlend = 5.5f, runBlend = 9.5f, walkRef = 3.6f, runRef = 9.5f, mulMin = 0.5f, mulMax = 2.5f;
            float huge = CastawayCharacter.ComputeFootSyncMul(100f, walkBlend, runBlend, walkRef, runRef, mulMin, mulMax);
            float tiny = CastawayCharacter.ComputeFootSyncMul(0.01f, walkBlend, runBlend, walkRef, runRef, mulMin, mulMax);
            Assert.That(huge, Is.EqualTo(mulMax).Within(1e-4f), "a velocity spike must clamp to footSyncMulMax (no blur)");
            Assert.That(tiny, Is.EqualTo(mulMin).Within(1e-4f), "a near-zero speed must clamp to footSyncMulMin (no freeze)");
        }

        // ===== PART 3 — ASYMMETRIC DAMP =====

        [Test]
        public void SpeedDamp_IsAsymmetric_FastUp_SmoothDown()
        {
            // The fix: a SHORT ramp-UP so the legs engage instantly on W (snappy start — the 'body slides before
            // feet engage' fix) + the existing longer ramp-DOWN so the stop stays smooth (the approved transition).
            var go = new GameObject("castaway-asym-damp-probe");
            var cc = go.AddComponent<CastawayCharacter>();
            Assert.Greater(cc.speedDampTime, 0.0001f,
                "the ramp-DOWN damp (speedDampTime) must stay > 0 so the STOP eases (the approved smooth-stop — do not regress)");
            Assert.GreaterOrEqual(cc.speedDampTime, cc.speedDampTimeUp,
                "the ramp-DOWN (stop) must be at least as LONG as the ramp-UP (start) — the damp is asymmetric, " +
                "with the start SNAPPIER than the stop (the Sponsor's 'snappy start, smooth stop' fix-spec)");
            Assert.Less(cc.speedDampTimeUp, cc.speedDampTime - 1e-4f,
                "the ramp-UP (start) must be SHORTER than the ramp-DOWN (stop) so the legs engage instantly on W " +
                "(the 'body slides before feet engage' complaint) while the stop still eases. Up=" +
                cc.speedDampTimeUp.ToString("F3") + " Down=" + cc.speedDampTime.ToString("F3"));
            Object.DestroyImmediate(go);
        }

        // ===== CROSS-LANE — the approved lanes must not regress =====

        [Test]
        public void FootSync_DoesNotDisturbTheWalkRunBlendTree()
        {
            // Foot-sync scales HOW FAST the blend plays (speedParameter), never WHICH clip (the Speed BLEND param).
            // The blend tree must STILL be 1D on the Speed param with its Walk + Run children intact (#68).
            var c = LoadController();
            var loco = FindState(c, "Locomotion");
            Assert.IsInstanceOf<BlendTree>(loco.motion, "Locomotion must still be a blend tree (#68 Walk<->Run)");
            var tree = (BlendTree)loco.motion;
            Assert.AreEqual(BlendTreeType.Simple1D, tree.blendType, "the Walk<->Run blend tree must stay 1D");
            Assert.AreEqual(CastawayCharacter.SpeedParam, tree.blendParameter,
                "the blend tree must still blend on the SPEED param — foot-sync scales playback via a SEPARATE " +
                "speedParameter (LocoSpeedMul), never the blend selection");
            bool walk = false, run = false;
            foreach (var ch in tree.children)
                if (ch.motion is AnimationClip cl)
                {
                    if (cl.name.Contains(CharacterAssetGen.WalkClip)) walk = true;
                    if (cl.name.Contains(CharacterAssetGen.RunClip)) run = true;
                }
            Assert.IsTrue(walk && run, "the blend tree must still carry the Walk + Run clips (run NOT regressed)");
        }
    }
}
