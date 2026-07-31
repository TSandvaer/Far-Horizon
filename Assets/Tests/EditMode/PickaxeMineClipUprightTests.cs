using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// PICKAXE-MINE UPRIGHT-POSE regression guards (ticket 86cav8xg9 — the Sponsor's soak-5 "contorted body").
    ///
    /// MEASURED cause (AttackClipPoseDiag, live v4 rig, all 5 per-class attack clips): the raw
    /// CastawayPickaxeSwing hinges the body RIGIDLY AT THE PELVIS — mixamorig:Hips deviates 104.8deg and bends the
    /// torso to 66.3deg off vertical at t~0.564, while Spine1/Spine2 move &lt;1deg. Reverting Hips alone at that
    /// frame drops the tilt to 19.9deg. Sibling peaks: axe 43.3 / spear 27 / sword 25 / dagger 19 — the axe's 43deg
    /// is the deepest fold the Sponsor has NOT flagged, so it is the band this fix targets.
    ///
    /// THESE GUARD THE BUG CLASS, not the instance — each one reds on a DIFFERENT way the fix can regress:
    ///   (1) WIRED — AttackPickaxe's state motion is the repaired .anim (a dropped regen, or a re-point back to
    ///       the raw FBX clip, reds here). Name + asset path are both asserted: SwingsRound5Tests / the controller
    ///       tests only check the state EXISTS, so they cannot catch a raw-clip re-point.
    ///   (2) UPRIGHT — the repaired clip's peak torso tilt is inside the unflagged axe band. This is the AC's
    ///       actual bar, measured on the LIVE rig via SampleAnimation (headless-valid; an Animator never ticks in
    ///       a headless run — the walk-float saga lesson), NOT a proxy on a quaternion component.
    ///   (3) THE RAW CLIP STILL FOLDS — asserts the SOURCE still exceeds the ceiling, so (2) proves the pass did
    ///       REAL work rather than a source that silently got clean. If the Sponsor ever re-sources the clip
    ///       (Route 1), this reds as a NUDGE to retire the pass instead of silently double-correcting.
    ///   (4) NO NEW POP — the repaired clip's worst per-authored-frame step may not exceed the raw clip's, ON EACH
    ///       EDITED BONE and over the whole skeleton. Re-keying + re-smoothing tangents is exactly how a fold fix
    ///       can trade a bad pose for a jerk; this refuses that trade.
    ///   (5) FEET STAY PLANTED — un-hinging a pelvis swings the legs with it unless compensated, which would trade
    ///       a contorted torso for FLOATING feet (the walk-float saga's failure mode). Asserts the repaired foot
    ///       Y-band + horizontal travel do not grow materially over raw.
    ///   (6) SCOPE — the set of curves the repair CHANGES, derived by DIFFING raw against repaired, is exactly the
    ///       12 pelvis + upper-leg rotation curves; every other curve is key-for-key IDENTICAL to the raw clip.
    ///       This is the machine proof of the ticket's "no downstream churn" constraint: arms, shoulders, head,
    ///       spine, lower legs and the hips POSITION/root-motion are untouched.
    ///   (7) STILL GENERIC — the source FBX importer stays animationType Generic. The ticket says to verify this
    ///       rather than trust a claim; a Humanoid flip explodes the mesh under the scaled hierarchy (86ca8rdkp).
    ///
    /// ===== 86caxgyc4 — GUARDS (2), (4) and (6) SHARPENED (Devon's #337 review NITs N2/N3/N4) =====
    /// Each of the three had a hole that let a real regression through while the guard stayed green. All three
    /// holes are the SAME shape — a check that cannot distinguish "absent" from "passed":
    ///   • (4) N2 — <c>WorstPerFrameStep</c> took the max over the WHOLE skeleton. Raw's worst is at
    ///     mixamorig:RightHand, a bone this repair never touches, so the three EDITED bones inherited that bone's
    ///     headroom: a pelvis pop well under it passed. Now guarded PER EDITED BONE, with the whole-skeleton
    ///     assert KEPT and a per-unedited-bone equality assert added. The three edited bones do NOT all take the
    ///     same form, and that split is MEASURED, not stylistic: the BLENDED bone (Hips) is compared against its
    ///     own raw local step, while the two COMPENSATED legs are pinned on WORLD orientation — writing the local
    ///     form for all three red on the SHIPPED clip (RightUpLeg 5.13deg vs raw 3.98deg), because the
    ///     compensation re-expresses the pelvis correction in the leg's own frame and its LOCAL rotation
    ///     necessarily picks that up while the rendered pose does not move at all. Widening a slack to admit that
    ///     would be the loosen-until-green failure; the world-orientation assert is ~100x tighter instead.
    ///   • (2) N3 — <c>PeakTorsoTilt</c> swept 41 samples over a 5.2s clip (~0.13s step) while <c>FootMetrics</c>
    ///     deliberately walked the authored 30fps grid because "a coarse sweep can step straight over a brief
    ///     one-frame dip". The same argument applies to a tilt SPIKE. Both now walk the clip's own frame grid.
    ///   • (6) N4 — the exclusion set was computed with <c>PickaxeMineCurveFix.QuatComponent/LastSeg/MatchesUpLeg</c>,
    ///     the generator's OWN filter. Widen <c>UpLegBones</c> and the generator edits more bones while the guard
    ///     skips exactly those bones: both move together and the guard passes. The edited set is now derived by
    ///     DIFF and asserted against a LITERAL bone/property list held here, not by asking the generator.
    ///
    /// EVERY SHARPENED GUARD CARRIES A NEGATIVE CONTROL (4 tests / 5 cases, the Guard2_/Guard4_/Guard6_ methods
    /// below) that runs the guard's OWN assertion body — not a re-implementation of it — against a clip with the
    /// defect INJECTED, and requires it to throw. Each one ALSO requires the RETIRED form to PASS on that same
    /// clip, in the same test. A guard whose red has never been demonstrated is not a guard; and the retired-form
    /// precondition is what proves the sharpening changed the OUTCOME rather than merely the code — without it,
    /// a negative control cannot tell "the guard caught the defect" from "the guard was always going to throw".
    /// </summary>
    public class PickaxeMineClipUprightTests
    {
        private const string BonePrefix = "mixamorig:";

        /// <summary>The pre-86caxgyc4 tilt sweep's sample count. RETIRED as a measurement — kept ONLY as the
        /// negative-control foil in <see cref="Guard2_RedsOnAOneFrameTiltSpike_ThatTheRetired41SampleSweepStepsOver"/>,
        /// where it must PASS on a clip the frame-grid sweep reds. Do NOT measure anything real with it.</summary>
        private const int RetiredCoarseSamples = 41;

        // Tolerances: the fix re-keys the pelvis + upper legs, so bit-exact equality on THOSE is not the bar —
        // "no WORSE than raw" is. Small absolute slacks keep the guards from flapping on float noise.
        private const float StepSlackDeg = 1.0f;
        /// <summary>Slack on an UNEDITED bone's per-frame step. Those curves are byte-copied by the generator, so
        /// both clips render the identical local rotation at every frame and the measured delta is float noise;
        /// this is deliberately ~20x tighter than <see cref="StepSlackDeg"/> so a leak into an unedited bone reds
        /// long before it could hide under the whole-skeleton ceiling.</summary>
        private const float UneditedStepSlackDeg = 0.05f;

        /// <summary>Slack on a COMPENSATED upper leg's WORLD orientation vs the raw clip. The compensation
        /// (<c>upLeg' = Inverse(H') * H * upLeg</c>, resampled onto the authored frame grid) preserves each leg's
        /// world orientation EXACTLY at every frame the engine renders — that is the generator's own stated
        /// invariant — so anything above float noise here is a broken compensation. Calibrated from measurement,
        /// see the value quoted in <see cref="AssertNoNewPerFrameStep"/>'s (d) block.</summary>
        private const float LegWorldSlackDeg = 0.05f;

        /// <summary>The three bones <c>PickaxeMineCurveFix</c> actually edits, stated as LITERALS on purpose
        /// (86caxgyc4 / Devon N4). Deriving this from the generator's own <c>MatchesUpLeg</c> is precisely what
        /// made guard (6) blind to a widened filter — the guard and the generator would move together.</summary>
        private static readonly string[] EditedBones =
            { BonePrefix + "Hips", BonePrefix + "LeftUpLeg", BonePrefix + "RightUpLeg" };

        /// <summary>The bone whose rotation is BLENDED toward the frame-0 anchor (the repair proper).</summary>
        private const string BlendedBone = BonePrefix + "Hips";

        /// <summary>The two bones that are COMPENSATED rather than authored — a different contract from
        /// <see cref="BlendedBone"/>, and guarded differently (see (a) vs (d)).</summary>
        private static readonly string[] CompensatedLegBones =
            { BonePrefix + "LeftUpLeg", BonePrefix + "RightUpLeg" };

        /// <summary>The four local-rotation quaternion component properties, also a LITERAL for the same reason.</summary>
        private static readonly string[] QuatProps =
            { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };

        // FOOT GUARDS — calibrated against MEASURED values so each one discriminates the real failure mode:
        //   • travel: reducing the pelvis rotation TRANSLATES the hip sockets (the leg compensation preserves the
        //     legs' world ORIENTATION exactly, but their root offset from the Hips pivot still moves), so a small
        //     increase is inherent, NOT a bug. Measured: with both legs compensated the worst foot's horizontal
        //     travel grows +0.101m over raw; with the LEFT leg compensation DROPPED (the first pass — LeftUpLeg has
        //     27 keys vs Hips' 24 and was skipped) it grew +0.274m. 0.15m sits cleanly between, so this guard PASSES
        //     the inherent socket shift and REDS a dropped/broken compensation.
        private const float FootTravelSlackM = 0.15f;
        //   • dip: a foot BELOW the height it started at is a foot inside the ground. The repaired clip's worst is
        //     0.0812m — INSIDE the band of the four sibling clips that ship today with no complaint (sword 0.0065,
        //     axe 0.0135, dagger 0.0602, spear 0.1077). 0.10m keeps it inside that shipped band while catching gross
        //     sinking. If this ever reds, the pelvis correction is dragging a foot through the terrain.
        private const float FootDipCeilingM = 0.10f;

        // INJECTED-DEFECT amplitudes for the negative controls. Each is sized against a MEASURED property of the
        // real clips (see the per-test preconditions), not picked for convenience.
        private const float InjectedHipsJerkDeg = 15f;   // must land BELOW raw's whole-skeleton worst (~20.8deg)
        private const float InjectedTiltSpikeDeg = 60f;  // must push one frame's torso tilt PAST the 46deg ceiling
        private const float InjectedSpineKeyDelta = 0.01f;

        private GameObject _root;
        private Transform _hips, _head, _lFoot, _rFoot;
        private List<Transform> _bones;

        [SetUp]
        public void SetUp()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            Assert.IsNotNull(fbx, "the live rig FBX must exist at " + CharacterAssetGen.FbxPath);
            _root = new GameObject("__pickaxeTiltRig");
            var avatar = new GameObject("__pickaxeTiltAvatar");
            avatar.transform.SetParent(_root.transform, false);
            avatar.transform.localScale = Vector3.one * 1.8f;
            var model = UnityEngine.Object.Instantiate(fbx, avatar.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            Model = model;

            _bones = new List<Transform>();
            _hips = _head = _lFoot = _rFoot = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(BonePrefix)) continue;
                _bones.Add(t);
                if (t.name == BonePrefix + "Hips") _hips = t;
                else if (t.name == BonePrefix + "Head") _head = t;
                else if (t.name == BonePrefix + "LeftFoot") _lFoot = t;
                else if (t.name == BonePrefix + "RightFoot") _rFoot = t;
            }
            Assert.IsNotNull(_hips, "rig must carry " + BonePrefix + "Hips");
            Assert.IsNotNull(_head, "rig must carry " + BonePrefix + "Head");
            Assert.IsNotNull(_lFoot, "rig must carry " + BonePrefix + "LeftFoot");
            Assert.IsNotNull(_rFoot, "rig must carry " + BonePrefix + "RightFoot");
        }

        private GameObject Model { get; set; }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
        }

        // ---------- (1) wired ----------
        [Test]
        public void RepairedPickaxeClip_Exists_AndIsWiredToTheAttackPickaxeState()
        {
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(repaired,
                "the repaired pickaxe-mine clip must exist at " + PickaxeMineCurveFix.RepairedClipPath +
                " (generated by PickaxeMineCurveFix.Generate in PrepareCharacter). A missing .anim means the " +
                "bootstrap/build ships the RAW clip — the 66deg pelvis fold the Sponsor saw at soak-5 returns.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);
            AnimatorState attackPickaxe = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.name == "AttackPickaxe") attackPickaxe = cs.state;
            Assert.IsNotNull(attackPickaxe, "the controller must have an 'AttackPickaxe' state");
            var clip = attackPickaxe.motion as AnimationClip;
            Assert.IsNotNull(clip, "AttackPickaxe's motion must be an AnimationClip");
            Assert.AreEqual(PickaxeMineCurveFix.RepairedClipName, clip.name,
                "AttackPickaxe must be motion'd to the REPAIRED .anim (" + PickaxeMineCurveFix.RepairedClipName +
                "), NOT the raw FBX clip (name '" + CharacterAssetGen.PickaxeSwingClip + "'). Got '" + clip.name +
                "'. A re-point back to the raw clip re-opens the 66deg pelvis fold.");
            Assert.AreEqual(PickaxeMineCurveFix.RepairedClipPath, AssetDatabase.GetAssetPath(clip),
                "AttackPickaxe's clip must be the asset at " + PickaxeMineCurveFix.RepairedClipPath);
        }

        // ---------- (2) upright ----------
        [Test]
        public void RepairedPickaxeClip_PeakTorsoTilt_IsInsideTheUnflaggedAxeBand()
        {
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(repaired, "the repaired clip must exist at " + PickaxeMineCurveFix.RepairedClipPath);
            AssertPeakTorsoTiltIsInsideTheBand(repaired, "repaired");
        }

        /// <summary>Guard (2)'s assertion body, factored out so the negative control can run THIS code — not a
        /// re-implementation of it — against an injected defect.</summary>
        private void AssertPeakTorsoTiltIsInsideTheBand(AnimationClip clip, string label)
        {
            float peak = PeakTorsoTilt(clip, out float atT, out int atFrame);
            Debug.Log($"[pickaxe-guard] tilt({label}) frame-grid peak={peak:F2}deg @ t={atT:F4}s frame {atFrame} " +
                      $"(ceiling {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg)");
            Assert.Less(peak, PickaxeMineCurveFix.PeakTiltCeilingDeg,
                $"the {label} mine clip's peak torso tilt must stay under {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg " +
                $"off vertical (the axe swing's unflagged 43.3deg band) — measured {peak:F1}deg at t={atT:F3}s, " +
                $"frame {atFrame}. Above this the body reads as doubling over at a chest-height boulder (the " +
                "soak-5 defect). The sweep walks the clip's OWN authored frame grid (86caxgyc4/N3), so a " +
                "single-frame spike cannot hide between sample points.");
        }

        // ---------- (3) the raw clip still folds (the fix does real work) ----------
        [Test]
        public void RawPickaxeClip_StillFoldsPastTheCeiling_SoTheRepairIsProvenToDoWork()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            Assert.IsNotNull(raw, "the raw Attack_Pickaxe clip must import at " + CharacterAssetGen.AttackPickaxeFbxPath);
            float peak = PeakTorsoTilt(raw, out float atT, out int atFrame);
            Debug.Log($"[pickaxe-guard] tilt(raw) frame-grid peak={peak:F2}deg @ t={atT:F4}s frame {atFrame}");
            Assert.Greater(peak, PickaxeMineCurveFix.PeakTiltCeilingDeg,
                $"the RAW source clip is expected to still fold past {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg " +
                $"(measured {peak:F1}deg at t={atT:F3}s, frame {atFrame}) — that is what makes the repaired-clip " +
                "assertion meaningful. If this reds, the SOURCE clip was re-sourced/replaced (ticket Route 1): " +
                "re-measure with AttackClipPoseDiag and RETIRE PickaxeMineCurveFix rather than double-correcting " +
                "a clean clip.");
        }

        // ---------- (4) no new pop ----------
        [Test]
        public void RepairedPickaxeClip_AddsNoPerFrameStep_SoAFoldFixCannotBecomeAJerk()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");
            AssertNoNewPerFrameStep(raw, repaired, "repaired");
        }

        /// <summary>Guard (4)'s assertion body, factored out so the negative controls run THIS code against an
        /// injected defect. Four assertions, each covering a different way the repair can go wrong:
        /// (a) the BLENDED bone's local step vs its OWN raw step — the N2 fix; (b) the whole-skeleton max, KEPT
        /// because it is the coarse net for a repair that leaks into a bone the generator never names;
        /// (c) per-UNEDITED-bone equality, which catches a leak the whole-skeleton max swallows whenever the leak
        /// lands below raw's worst bone; (d) the two COMPENSATED legs' WORLD orientation, which is where a visible
        /// jerk on those two bones actually lives.</summary>
        private void AssertNoNewPerFrameStep(AnimationClip raw, AnimationClip repaired, string label)
        {
            var rawSteps = PerBoneWorstStep(raw);
            var fixSteps = PerBoneWorstStep(repaired);
            foreach (var bone in EditedBones)
            {
                Assert.IsTrue(rawSteps.ContainsKey(bone) && fixSteps.ContainsKey(bone),
                    $"the rig must carry {bone} — this guard is scoped to the bones PickaxeMineCurveFix edits, and " +
                    "a missing one means the rig or the generator's bone set moved (re-derive both).");
                Debug.Log($"[pickaxe-guard] step({label}) {bone}: raw={rawSteps[bone]:F2}deg fix={fixSteps[bone]:F2}deg");
            }

            // (a) THE BLENDED BONE — the N2 fix. mixamorig:Hips is compared against ITS OWN raw per-frame step, so
            //     it can no longer hide under a bone the repair never touches. Hips' parent is not animated by this
            //     clip, so its LOCAL step IS its world step: this is the visible-jerk quantity for the pelvis.
            Assert.LessOrEqual(fixSteps[BlendedBone], rawSteps[BlendedBone] + StepSlackDeg,
                $"the {label} clip's worst per-authored-frame step ON {BlendedBone} ({fixSteps[BlendedBone]:F2}deg) " +
                $"must not exceed the RAW clip's step on THAT SAME BONE ({rawSteps[BlendedBone]:F2}deg) + " +
                $"{StepSlackDeg:F1}deg. Compared per-bone on purpose (86caxgyc4 / Devon N2): a whole-skeleton max is " +
                "set by mixamorig:RightHand, which this repair never touches, so it hands the edited bones ~21deg of " +
                "free headroom and a ~15deg pelvis pop from the re-smoothed tangents passes.");

            // (b) WHOLE SKELETON — KEPT (86caxgyc4 constraint). Its job is different from (a): it is the coarse net
            //     for a repair that leaks into a bone nobody named up front.
            float rawWorst = WorstOf(rawSteps, out string rawBone);
            float fixWorst = WorstOf(fixSteps, out string fixBone);
            Debug.Log($"[pickaxe-guard] step({label}) whole-skeleton: raw={rawWorst:F2}deg @{rawBone} " +
                      $"fix={fixWorst:F2}deg @{fixBone}");
            Assert.LessOrEqual(fixWorst, rawWorst + StepSlackDeg,
                $"the {label} clip's worst per-authored-frame WHOLE-SKELETON step ({fixWorst:F2}deg @{fixBone}) must " +
                $"not exceed the raw clip's ({rawWorst:F2}deg @{rawBone}) — re-keying the pelvis + re-smoothing " +
                "tangents must not trade the contorted pose for a visible jerk (the #197 defect class).");

            // (c) EVERY UNEDITED BONE — byte-copied curves must render the identical per-frame step. This is the
            //     half of (b)'s old job that the max could never do: a 5deg leak onto Spine sits far under raw's
            //     20.8deg worst bone and passes (b) silently.
            foreach (var kv in fixSteps)
            {
                if (EditedBones.Contains(kv.Key)) continue;
                Assert.IsTrue(rawSteps.ContainsKey(kv.Key), $"bone {kv.Key} is absent from the raw sweep");
                Assert.AreEqual(rawSteps[kv.Key], kv.Value, UneditedStepSlackDeg,
                    $"the {label} clip's per-frame step on the UNEDITED bone {kv.Key} moved " +
                    $"({rawSteps[kv.Key]:F3}deg raw -> {kv.Value:F3}deg) — the repair is scoped to the pelvis hinge " +
                    "plus its upper-leg compensation ONLY, so every other bone must render byte-identically.");
            }

            // (d) THE TWO COMPENSATED UPPER LEGS — guarded on WORLD orientation, not on a local step, and this is a
            //     MEASURED decision rather than a stylistic one. Authoring (a) over all three bones red on the
            //     SHIPPED clip: mixamorig:RightUpLeg's worst local step is 5.13deg against raw's 3.98deg. That
            //     growth is INHERENT, not a defect — the compensation re-expresses the pelvis correction in the
            //     leg's own frame (upLeg' = Inverse(H') * H * upLeg), so the leg's LOCAL rotation necessarily picks
            //     up the correction's per-frame rate while the pose the player sees does not move at all. Widening
            //     a slack to admit that would have been the "guard loosened until it passes" failure; the honest
            //     fix is to guard the quantity a jerk actually lives in. Measured on the shipped clip, BOTH legs'
            //     worst world delta vs raw prints 0.0000deg at 4-decimal precision across all 157 authored frames
            //     — i.e. the invariant holds to float noise, so this assert is orders of magnitude TIGHTER than
            //     the local-step form it replaces, and a genuine leg jerk (one the pelvis does not cancel) still
            //     reds here at its full amplitude (measured 15.0000 / 14.9999deg). See the negative control
            //     Guard4_RedsOnACompensatedUpperLegPop_ThatTheRetiredWholeSkeletonCeilingWouldHaveHidden.
            var legWorld = PerBoneWorstWorldDelta(raw, repaired, CompensatedLegBones);
            foreach (var bone in CompensatedLegBones)
            {
                Assert.IsTrue(legWorld.ContainsKey(bone), $"the rig must carry {bone}");
                Debug.Log($"[pickaxe-guard] legworld({label}) {bone}: worst raw-vs-fix world delta " +
                          $"{legWorld[bone]:F4}deg (slack {LegWorldSlackDeg:F2}deg)");
                Assert.LessOrEqual(legWorld[bone], LegWorldSlackDeg,
                    $"the {label} clip's {bone} WORLD orientation drifts {legWorld[bone]:F4}deg from the raw clip's " +
                    $"at some authored frame (slack {LegWorldSlackDeg:F2}deg). The upper-leg compensation exists to " +
                    "keep each leg's world pose EXACTLY where the source clip put it (PickaxeMineCurveFix step 3, " +
                    "resampled onto the authored frame grid so the correction is exact at every frame the engine " +
                    "renders). A drift here means the compensation is broken or was evaluated on the wrong key " +
                    "grid — the failure mode that let the left foot's travel nearly double on the first pass.");
            }
        }

        // ---------- (5) feet stay planted ----------
        [Test]
        public void RepairedPickaxeClip_FeetStayPlanted_UpperLegCompensationHeld()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");

            FootMetrics(raw, out float rawTravel, out float rawDip);
            FootMetrics(repaired, out float fixTravel, out float fixDip);
            Assert.LessOrEqual(fixTravel, rawTravel + FootTravelSlackM,
                $"the repaired clip's worst foot horizontal travel ({fixTravel:F4}m) must not grow beyond the raw " +
                $"clip's ({rawTravel:F4}m) + {FootTravelSlackM:F2}m — an UNcompensated upper leg swings with the " +
                "un-hinged pelvis and the foot slides (measured +0.274m when the left leg's compensation was " +
                "skipped, vs +0.101m for the inherent hip-socket shift once both legs are compensated).");
            Assert.LessOrEqual(fixDip, FootDipCeilingM,
                $"the repaired clip's worst foot dip below its own start height ({fixDip:F4}m, raw {rawDip:F4}m) " +
                $"must stay under {FootDipCeilingM:F2}m — a foot below where it started is a foot inside the " +
                "terrain. This ceiling is the band the shipped sibling clips already occupy (spear 0.1077m).");
        }

        // ---------- (6) scope: nothing but the pelvis + upper legs changed ----------
        [Test]
        public void RepairedPickaxeClip_LeavesEveryNonPelvisCurveVerbatim()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");
            AssertOnlyTheTwelveEditedCurvesChanged(raw, repaired, "repaired");
        }

        /// <summary>Guard (6)'s assertion body, factored out so the negative control runs THIS code against an
        /// injected defect. Two independent properties: the IDENTITY of the changed set (derived by DIFF, asserted
        /// against a LITERAL list — the N4 fix) and the key-for-key verbatim carry of everything else, with the
        /// anti-vacuity floor kept.</summary>
        private static void AssertOnlyTheTwelveEditedCurvesChanged(AnimationClip raw, AnimationClip repaired, string label)
        {
            // The EXPECTED edited set, resolved from the LITERAL bone + property names held in this file. Nothing
            // here calls PickaxeMineCurveFix.MatchesUpLeg / QuatComponent / LastSeg: if the generator's own filter
            // widens, this list does NOT move with it, which is the entire point (86caxgyc4 / Devon N4).
            var expected = new SortedSet<string>();
            foreach (var b in AnimationUtility.GetCurveBindings(raw))
                if (EditedBones.Contains(LastSegment(b.path)) && QuatProps.Contains(b.propertyName))
                    expected.Add(BindingKey(b));
            Assert.AreEqual(12, expected.Count,
                "expected exactly 12 edited bindings (Hips + LeftUpLeg + RightUpLeg x m_LocalRotation.x/y/z/w) to " +
                "exist on the RAW clip — found " + expected.Count + ": " + string.Join(", ", expected) + ". A " +
                "different count means the rig's bone paths or the clip's binding set moved; re-derive the edited " +
                "list from the rig before touching this guard.");

            var changed = DiffChangedBindings(raw, repaired);
            Debug.Log($"[pickaxe-guard] diff({label}) changed={changed.Count} bindings: {string.Join(", ", changed)}");

            var unexpected = changed.Where(k => !expected.Contains(k)).ToList();
            Assert.IsEmpty(unexpected,
                $"the {label} clip changed curve(s) OUTSIDE the pelvis + upper-leg rotation set: " +
                string.Join(", ", unexpected) + ". The edited set is derived by DIFFING raw against repaired and " +
                "compared to a LITERAL {Hips, LeftUpLeg, RightUpLeg} x {x,y,z,w} list (86caxgyc4 / Devon N4) — " +
                "widening PickaxeMineCurveFix.UpLegBones no longer moves this guard's exclusion set with it, so a " +
                "generator that starts editing the spine reds HERE instead of silently skipping itself.");
            var missing = expected.Where(k => !changed.Contains(k)).ToList();
            Assert.IsEmpty(missing,
                $"the {label} clip left curve(s) the repair is supposed to edit UNCHANGED: " +
                string.Join(", ", missing) + ". Either the repair silently no-op'd on those bones (the fix is " +
                "INERT — see PickaxeMineCurveFix's 'FIX INERT' error path) or the blend factor went to zero.");

            // The verbatim carry, key-for-key. Redundant with the diff by construction, but it is what names the
            // exact curve + key index when something does move, and it carries the anti-vacuity floor.
            int compared = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(raw))
            {
                if (expected.Contains(BindingKey(b))) continue;

                var rc = AnimationUtility.GetEditorCurve(raw, b);
                var fc = AnimationUtility.GetEditorCurve(repaired, b);
                Assert.IsNotNull(fc, $"the {label} clip must still carry the curve {b.path}.{b.propertyName} " +
                                     "— a dropped passthrough curve silently deletes motion.");
                Assert.AreEqual(rc.length, fc.length,
                    $"key count changed on an UNEDITED curve {b.path}.{b.propertyName}");
                var rk = rc.keys;
                var fk = fc.keys;
                for (int i = 0; i < rk.Length; i++)
                {
                    Assert.AreEqual(rk[i].time, fk[i].time, 1e-6f,
                        $"key time changed on an UNEDITED curve {b.path}.{b.propertyName} @{i}");
                    Assert.AreEqual(rk[i].value, fk[i].value, 1e-6f,
                        $"key VALUE changed on an UNEDITED curve {b.path}.{b.propertyName} @{i} — the fix is scoped " +
                        "to the pelvis hinge + its upper-leg compensation ONLY (ticket 86cav8xg9 'no downstream " +
                        "churn'); arms/shoulders/head/spine/lower-legs/root-motion must be byte-carried.");
                }
                compared++;
            }
            Assert.Greater(compared, 50,
                "expected many verbatim curves to compare — a near-zero count means the binding filter is wrong " +
                "and this guard is vacuous.");
        }

        // ---------- (7) the source FBX stays Generic ----------
        [Test]
        public void PickaxeSourceFbx_StaysGenericRig_NotHumanoid()
        {
            var importer = AssetImporter.GetAtPath(CharacterAssetGen.AttackPickaxeFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, "Attack_Pickaxe.fbx must have a ModelImporter");
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                "the pickaxe clip FBX must stay Generic (animationType: 2). Generic binds by transform path; a " +
                "Humanoid flip explodes the skinned mesh under the scaled scene hierarchy (86ca8rdkp / PR #47).");
        }

        // ===================== NEGATIVE CONTROLS (86caxgyc4 AC4) =====================
        // Each builds the regression its guard exists to catch, runs the GUARD'S OWN assertion body against it,
        // and requires a throw. Each ALSO requires the RETIRED form to PASS on that same clip — that half is what
        // proves the sharpening changed the OUTCOME, not merely the code, and it is why these cannot go green by
        // simply never reaching the defect.

        /// <summary>NEGATIVE CONTROL for guard (4) / Devon N2. A 15deg single-frame pelvis pop — deliberately sized
        /// to sit BELOW raw's whole-skeleton worst step, which is at mixamorig:RightHand, a bone the repair never
        /// touches. The retired whole-skeleton-max form must PASS this clip (that is the hole); the per-edited-bone
        /// form must RED and must name Hips.</summary>
        [Test]
        public void Guard4_RedsOnAPelvisPop_ThatTheRetiredWholeSkeletonCeilingWouldHaveHidden()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");

            var mutant = CloneClip(repaired, PickaxeMineCurveFix.RepairedClipName + "__hipsPopMutant");
            int frames = FrameCount(mutant);
            int spikeFrame = frames / 2;
            var jerk = Quaternion.AngleAxis(InjectedHipsJerkDeg, Vector3.right);
            ReKeyBoneRotationOnFrameGrid(mutant, BonePrefix + "Hips", (f, q) => f == spikeFrame ? jerk * q : q);

            var rawSteps = PerBoneWorstStep(raw);
            var mutSteps = PerBoneWorstStep(mutant);
            float rawWorst = WorstOf(rawSteps, out string rawBone);
            float mutWorst = WorstOf(mutSteps, out string mutBone);
            Debug.Log($"[pickaxe-guard] NEGCTRL(4) injected {InjectedHipsJerkDeg}deg pop on Hips @frame {spikeFrame}: " +
                      $"Hips raw={rawSteps[BonePrefix + "Hips"]:F2} -> mutant={mutSteps[BonePrefix + "Hips"]:F2}deg; " +
                      $"whole-skeleton raw={rawWorst:F2}@{rawBone} -> mutant={mutWorst:F2}@{mutBone}");

            // The RETIRED form. If this ever reds, the injected pop grew past raw's whole-skeleton worst and this
            // test stops demonstrating the hole — shrink InjectedHipsJerkDeg rather than deleting the assert.
            Assert.LessOrEqual(mutWorst, rawWorst + StepSlackDeg,
                $"PRECONDITION of this negative control: the injected {InjectedHipsJerkDeg}deg pelvis pop must stay " +
                $"UNDER the retired whole-skeleton ceiling (raw worst {rawWorst:F2}deg @{rawBone}), so that the " +
                "retired form demonstrably PASSES a clip the sharpened form reds. Measured mutant whole-skeleton " +
                $"worst {mutWorst:F2}deg @{mutBone}.");

            var ex = Assert.Throws<AssertionException>(
                () => AssertNoNewPerFrameStep(raw, mutant, "hips-pop mutant"),
                "guard (4) must RED on a 15deg single-frame pelvis pop. If it passes, the per-edited-bone " +
                "comparison is not wired and the guard is back to being blind under the RightHand ceiling.");
            StringAssert.Contains(BonePrefix + "Hips", ex.Message);
        }

        /// <summary>NEGATIVE CONTROL for guard (4) / Devon N2, second half — the two COMPENSATED bones. Same 15deg
        /// single-frame pop, injected on an upper leg instead of the pelvis, so the AC1 destination ("a jerk on ONE
        /// OF THE THREE bones the repair edits reds") is demonstrated for all three and not argued for two of them.
        /// The pop is not cancelled by any pelvis change, so it moves the leg's WORLD orientation and reds (d) —
        /// while the retired whole-skeleton local-max form still passes, because 15deg sits under raw's
        /// mixamorig:RightHand worst.</summary>
        [TestCase("mixamorig:LeftUpLeg")]
        [TestCase("mixamorig:RightUpLeg")]
        public void Guard4_RedsOnACompensatedUpperLegPop_ThatTheRetiredWholeSkeletonCeilingWouldHaveHidden(string bone)
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");

            var mutant = CloneClip(repaired, PickaxeMineCurveFix.RepairedClipName + "__legPopMutant");
            int spikeFrame = FrameCount(mutant) / 2;
            var jerk = Quaternion.AngleAxis(InjectedHipsJerkDeg, Vector3.right);
            ReKeyBoneRotationOnFrameGrid(mutant, bone, (f, q) => f == spikeFrame ? jerk * q : q);

            var rawSteps = PerBoneWorstStep(raw);
            var mutSteps = PerBoneWorstStep(mutant);
            float rawWorst = WorstOf(rawSteps, out string rawBone);
            float mutWorst = WorstOf(mutSteps, out string mutBone);
            Debug.Log($"[pickaxe-guard] NEGCTRL(4-leg) injected {InjectedHipsJerkDeg}deg pop on {bone} @frame " +
                      $"{spikeFrame}: whole-skeleton raw={rawWorst:F2}@{rawBone} -> mutant={mutWorst:F2}@{mutBone}");
            Assert.LessOrEqual(mutWorst, rawWorst + StepSlackDeg,
                $"PRECONDITION of this negative control: the injected {InjectedHipsJerkDeg}deg pop on {bone} must " +
                $"stay UNDER the retired whole-skeleton ceiling (raw worst {rawWorst:F2}deg @{rawBone}) so the " +
                $"retired form demonstrably PASSES it. Measured mutant worst {mutWorst:F2}deg @{mutBone}.");

            var ex = Assert.Throws<AssertionException>(
                () => AssertNoNewPerFrameStep(raw, mutant, "leg-pop mutant"),
                $"guard (4) must RED on a 15deg single-frame pop on {bone}. If it passes, the compensated legs are " +
                "guarded by nothing: their local step carries an inherent, slack-covered growth, so the WORLD " +
                "orientation assert is the only thing standing between a leg jerk and a green gate.");
            StringAssert.Contains(bone, ex.Message);
        }

        /// <summary>NEGATIVE CONTROL for guard (2) / Devon N3. A one-frame torso-tilt spike placed at the frame
        /// FURTHEST from every retired 41-sample point (measured, not assumed) so the retired sweep steps straight
        /// over it. The retired sweep must read the clip as inside the band; the frame-grid sweep must RED.</summary>
        [Test]
        public void Guard2_RedsOnAOneFrameTiltSpike_ThatTheRetired41SampleSweepStepsOver()
        {
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(repaired, "repaired clip missing");

            float fps = ClipFps(repaired);
            int spikeFrame = FrameFurthestFromRetiredSamples(repaired, out float gapSec, out float spikeT);
            Debug.Log($"[pickaxe-guard] NEGCTRL(2) spike frame {spikeFrame} @ t={spikeT:F4}s; nearest retired " +
                      $"41-sample point is {gapSec:F4}s away (one frame = {1f / fps:F4}s)");
            Assert.Greater(gapSec, 1f / fps,
                "PRECONDITION of this negative control: the spiked frame must be MORE than one frame away from " +
                "every retired 41-sample point, so the retired sweep provably samples un-spiked curve values " +
                $"(measured gap {gapSec:F4}s vs one frame {1f / fps:F4}s).");

            var mutant = CloneClip(repaired, PickaxeMineCurveFix.RepairedClipName + "__tiltSpikeMutant");
            float achieved = InjectTorsoTiltSpikeAtFrame(mutant, spikeFrame, InjectedTiltSpikeDeg);
            float retired = PeakTorsoTiltRetiredCoarse(mutant, out float retiredT);
            Debug.Log($"[pickaxe-guard] NEGCTRL(2) injected spike -> tilt {achieved:F2}deg @frame {spikeFrame}; " +
                      $"retired {RetiredCoarseSamples}-sample sweep peak={retired:F2}deg @ t={retiredT:F4}s " +
                      $"(ceiling {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg)");

            Assert.Greater(achieved, PickaxeMineCurveFix.PeakTiltCeilingDeg,
                $"PRECONDITION: the injected spike must push the torso tilt PAST the {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg " +
                $"ceiling at that frame — measured {achieved:F2}deg. Raise InjectedTiltSpikeDeg if the clip moved.");
            // The RETIRED form steps over it.
            Assert.Less(retired, PickaxeMineCurveFix.PeakTiltCeilingDeg,
                $"PRECONDITION of this negative control: the retired {RetiredCoarseSamples}-sample sweep must NOT " +
                $"see the one-frame spike (measured peak {retired:F2}deg @ t={retiredT:F4}s), so that it " +
                "demonstrably PASSES a clip the frame-grid sweep reds.");

            var ex = Assert.Throws<AssertionException>(
                () => AssertPeakTorsoTiltIsInsideTheBand(mutant, "tilt-spike mutant"),
                "guard (2) must RED on a one-frame torso-tilt spike. If it passes, the sweep is back on a coarse " +
                "grid and can step over exactly the defect the ceiling exists to catch.");
            StringAssert.Contains("frame " + spikeFrame, ex.Message);
        }

        /// <summary>NEGATIVE CONTROL for guard (6) / Devon N4. Devon's exact scenario: the generator's own bone
        /// filter widens to include Spine, so the generator edits a spine curve AND the retired guard — which asked
        /// that same filter what to skip — skips exactly that curve and passes. The widened filter is injected as a
        /// parameter here rather than by editing PickaxeMineCurveFix.UpLegBones: MatchesUpLeg is the ONLY one of
        /// the three production helpers the retired form used whose answer a widening would change, so substituting
        /// it reproduces the scenario faithfully AND keeps the demonstration in CI forever.</summary>
        [Test]
        public void Guard6_RedsWhenTheGeneratorEditsASpineCurve_WhileTheRetiredProductionFilterFormSkipsIt()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");

            var mutant = CloneClip(repaired, PickaxeMineCurveFix.RepairedClipName + "__spineEditMutant");
            string spineKey = PerturbCurve(mutant, BonePrefix + "Spine", QuatProps[0], InjectedSpineKeyDelta);
            Debug.Log($"[pickaxe-guard] NEGCTRL(6) perturbed {spineKey} by {InjectedSpineKeyDelta} " +
                      "(stands in for a generator whose UpLegBones gained \"Spine\")");

            // The RETIRED form, with the generator's filter widened exactly as Devon described.
            Func<string, bool> widened = seg => seg.EndsWith("LeftUpLeg") || seg.EndsWith("RightUpLeg") ||
                                                seg.EndsWith("Spine");
            bool retiredPassed = RetiredProductionFilterFormPasses(raw, mutant, widened, out int retiredCompared,
                                                                  out string retiredWhy);
            Debug.Log($"[pickaxe-guard] NEGCTRL(6) retired production-filter form (widened): passed={retiredPassed} " +
                      $"compared={retiredCompared} why={retiredWhy}");
            Assert.IsTrue(retiredPassed,
                "PRECONDITION of this negative control: with the generator's filter widened to include Spine, the " +
                "RETIRED form must PASS a clip that carries a spine edit — that is the hole Devon N4 named. Got: " +
                retiredWhy);
            Assert.Greater(retiredCompared, 50,
                "PRECONDITION: the retired form must not have passed VACUOUSLY — it has to have actually compared " +
                $"curves (compared={retiredCompared}).");

            var ex = Assert.Throws<AssertionException>(
                () => AssertOnlyTheTwelveEditedCurvesChanged(raw, mutant, "spine-edit mutant"),
                "guard (6) must RED on a spine edit even when the generator's own filter would have excused it. If " +
                "it passes, the exclusion set is back to being derived from the generator and the two move together.");
            StringAssert.Contains(spineKey, ex.Message);
        }

        // ===================== measurement helpers =====================

        private static float ClipFps(AnimationClip clip) => clip.frameRate > 0f ? clip.frameRate : 30f;

        private static int FrameCount(AnimationClip clip) =>
            Mathf.Max(2, Mathf.RoundToInt(clip.length * ClipFps(clip)));

        /// <summary>Peak torso lean off VERTICAL (hips-&gt;head vs world up), sampled on the clip's OWN authored
        /// frame grid — the same grid <see cref="FootMetrics"/> walks, and for the same reason: a coarse sweep can
        /// step straight over a brief one-frame spike (86caxgyc4 / Devon N3).</summary>
        private float PeakTorsoTilt(AnimationClip clip, out float atT, out int atFrame)
        {
            float fps = ClipFps(clip);
            int frames = FrameCount(clip);
            float peak = -1f; atT = 0f; atFrame = 0;
            for (int f = 0; f <= frames; f++)
            {
                float t = Mathf.Min(clip.length, f / fps);
                clip.SampleAnimation(Model, t);
                float tilt = Vector3.Angle(_head.position - _hips.position, Vector3.up);
                if (tilt > peak) { peak = tilt; atT = t; atFrame = f; }
            }
            return peak;
        }

        /// <summary>The RETIRED coarse tilt sweep. Present ONLY so the negative control can show it passing a clip
        /// the frame-grid sweep reds — never use it to measure anything real.</summary>
        private float PeakTorsoTiltRetiredCoarse(AnimationClip clip, out float atT)
        {
            float peak = -1f; atT = 0f;
            for (int i = 0; i < RetiredCoarseSamples; i++)
            {
                float nt = i / (float)(RetiredCoarseSamples - 1);
                float t = nt * clip.length;
                clip.SampleAnimation(Model, t);
                float tilt = Vector3.Angle(_head.position - _hips.position, Vector3.up);
                if (tilt > peak) { peak = tilt; atT = t; }
            }
            return peak;
        }

        /// <summary>The authored frame whose time is FURTHEST from every retired 41-sample point. Measured rather
        /// than hard-coded so the negative control survives a clip-length change.</summary>
        private static int FrameFurthestFromRetiredSamples(AnimationClip clip, out float gapSec, out float frameTime)
        {
            float fps = ClipFps(clip);
            int frames = FrameCount(clip);
            int best = 1; float bestGap = -1f; frameTime = 0f;
            for (int f = 1; f < frames; f++)
            {
                float t = f / fps;
                float gap = float.MaxValue;
                for (int i = 0; i < RetiredCoarseSamples; i++)
                    gap = Mathf.Min(gap, Mathf.Abs(t - (i / (float)(RetiredCoarseSamples - 1)) * clip.length));
                if (gap > bestGap) { bestGap = gap; best = f; frameTime = t; }
            }
            gapSec = bestGap;
            return best;
        }

        /// <summary>Worst single-authored-frame local-rotation change PER BONE (bone name -&gt; degrees). Per-bone
        /// rather than a whole-skeleton max because the max is set by mixamorig:RightHand, a bone this repair never
        /// touches — see <see cref="AssertNoNewPerFrameStep"/> (86caxgyc4 / Devon N2).</summary>
        private Dictionary<string, float> PerBoneWorstStep(AnimationClip clip)
        {
            float fps = ClipFps(clip);
            int frames = FrameCount(clip);
            var prev = new Quaternion[_bones.Count];
            var worst = new float[_bones.Count];
            for (int f = 0; f <= frames; f++)
            {
                clip.SampleAnimation(Model, Mathf.Min(clip.length, f / fps));
                for (int b = 0; b < _bones.Count; b++)
                {
                    var q = _bones[b].localRotation;
                    if (f > 0)
                    {
                        float d = Quaternion.Angle(q, prev[b]);
                        if (d > worst[b]) worst[b] = d;
                    }
                    prev[b] = q;
                }
            }
            var map = new Dictionary<string, float>(_bones.Count);
            for (int b = 0; b < _bones.Count; b++)
                map[_bones[b].name] = map.TryGetValue(_bones[b].name, out float had) ? Mathf.Max(had, worst[b]) : worst[b];
            return map;
        }

        /// <summary>Worst per-authored-frame WORLD-rotation difference between two clips, per requested bone. This
        /// is the quantity a VISIBLE jerk lives in: a bone's LOCAL step can grow on a bone whose world pose never
        /// moves (exactly what the upper-leg compensation does by construction), and the eye judges world pose.
        /// It is deliberately NOT used for per-bone ATTRIBUTION — a pop on a parent propagates into every
        /// descendant's world rotation, so a world-space whole-skeleton scan would indict a leaf carried along by
        /// the bone that actually owns the defect (procedural-animation-verbs.md, "a per-bone quaternion deviation
        /// cannot tell a big swing from a contorted pose"). Local step attributes; world delta judges.</summary>
        private Dictionary<string, float> PerBoneWorstWorldDelta(AnimationClip a, AnimationClip b,
                                                                IEnumerable<string> boneNames)
        {
            var wanted = new HashSet<string>(boneNames);
            var bones = _bones.Where(t => wanted.Contains(t.name)).ToList();
            float fps = ClipFps(a);
            int frames = FrameCount(a);
            var qa = new Quaternion[bones.Count];
            var worst = new float[bones.Count];
            for (int f = 0; f <= frames; f++)
            {
                float t = f / fps;
                a.SampleAnimation(Model, Mathf.Min(a.length, t));
                for (int i = 0; i < bones.Count; i++) qa[i] = bones[i].rotation;
                b.SampleAnimation(Model, Mathf.Min(b.length, t));
                for (int i = 0; i < bones.Count; i++)
                    worst[i] = Mathf.Max(worst[i], Quaternion.Angle(qa[i], bones[i].rotation));
            }
            var map = new Dictionary<string, float>(bones.Count);
            for (int i = 0; i < bones.Count; i++) map[bones[i].name] = worst[i];
            return map;
        }

        private static float WorstOf(Dictionary<string, float> steps, out string worstBone)
        {
            worstBone = "-"; float worst = 0f;
            foreach (var kv in steps) if (kv.Value > worst) { worst = kv.Value; worstBone = kv.Key; }
            return worst;
        }

        /// <summary>Worst-of-the-two-feet horizontal travel and dip-below-own-start, in metres. Sampled on the
        /// clip's own authored FRAME grid — a coarse sweep can step straight over a brief one-frame dip.</summary>
        private void FootMetrics(AnimationClip clip, out float worstTravel, out float worstDip)
        {
            float lMin = float.MaxValue, rMin = float.MaxValue;
            Vector3 lFirst = Vector3.zero, rFirst = Vector3.zero;
            float lTravel = 0f, rTravel = 0f, lY0 = 0f, rY0 = 0f;
            float fps = ClipFps(clip);
            int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * fps));
            for (int f = 0; f <= frames; f++)
            {
                clip.SampleAnimation(Model, Mathf.Min(clip.length, f / fps));
                lMin = Mathf.Min(lMin, _lFoot.position.y);
                rMin = Mathf.Min(rMin, _rFoot.position.y);
                if (f == 0) { lFirst = _lFoot.position; rFirst = _rFoot.position; lY0 = lFirst.y; rY0 = rFirst.y; }
                lTravel = Mathf.Max(lTravel, Vector3.ProjectOnPlane(_lFoot.position - lFirst, Vector3.up).magnitude);
                rTravel = Mathf.Max(rTravel, Vector3.ProjectOnPlane(_rFoot.position - rFirst, Vector3.up).magnitude);
            }
            worstTravel = Mathf.Max(lTravel, rTravel);
            worstDip = Mathf.Max(Mathf.Max(0f, lY0 - lMin), Mathf.Max(0f, rY0 - rMin));
        }

        // ===================== binding / diff helpers (no production filter) =====================

        private static string LastSegment(string path)
        {
            int i = path.LastIndexOf('/');
            return i >= 0 ? path.Substring(i + 1) : path;
        }

        private static string BindingKey(EditorCurveBinding b) => b.path + "|" + b.propertyName;

        private static Dictionary<string, EditorCurveBinding> BindingMap(AnimationClip clip)
        {
            var map = new Dictionary<string, EditorCurveBinding>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip)) map[BindingKey(b)] = b;
            return map;
        }

        /// <summary>Every binding whose curve DIFFERS between the two clips — key count, key time or key value —
        /// plus any binding present in exactly one of them. A pure diff: it never asks the generator which bones
        /// it edits, which is what lets guard (6) red on a generator that widened its own filter.</summary>
        private static List<string> DiffChangedBindings(AnimationClip raw, AnimationClip repaired)
        {
            var rawMap = BindingMap(raw);
            var fixMap = BindingMap(repaired);
            var keys = new SortedSet<string>(rawMap.Keys);
            keys.UnionWith(fixMap.Keys);

            var changed = new List<string>();
            foreach (var k in keys)
            {
                bool inRaw = rawMap.TryGetValue(k, out var rb);
                bool inFix = fixMap.TryGetValue(k, out var fb);
                if (!inRaw || !inFix) { changed.Add(k); continue; }
                var rc = AnimationUtility.GetEditorCurve(raw, rb);
                var fc = AnimationUtility.GetEditorCurve(repaired, fb);
                if (rc == null || fc == null) { changed.Add(k); continue; }
                if (rc.length != fc.length) { changed.Add(k); continue; }
                var rk = rc.keys;
                var fk = fc.keys;
                bool differs = false;
                for (int i = 0; i < rk.Length && !differs; i++)
                    differs = Mathf.Abs(rk[i].time - fk[i].time) > 1e-6f ||
                              Mathf.Abs(rk[i].value - fk[i].value) > 1e-6f;
                if (differs) changed.Add(k);
            }
            return changed;
        }

        /// <summary>The RETIRED guard-(6) form: exclusion computed with the generator's OWN helpers, with the
        /// up-leg matcher injected so a test can model a widened <c>UpLegBones</c>. Present ONLY as the negative
        /// control's foil — never call it as a real check.</summary>
        private static bool RetiredProductionFilterFormPasses(AnimationClip raw, AnimationClip repaired,
                                                              Func<string, bool> upLegMatcher,
                                                              out int compared, out string why)
        {
            compared = 0;
            why = "retired form passed";
            foreach (var b in AnimationUtility.GetCurveBindings(raw))
            {
                bool isQuat = PickaxeMineCurveFix.QuatComponent(b.propertyName) >= 0;
                string seg = PickaxeMineCurveFix.LastSeg(b.path);
                if (isQuat && (seg.EndsWith("Hips") || upLegMatcher(seg))) continue;

                var rc = AnimationUtility.GetEditorCurve(raw, b);
                var fc = AnimationUtility.GetEditorCurve(repaired, b);
                if (rc == null || fc == null || rc.length != fc.length)
                {
                    why = $"retired form REDDED on {BindingKey(b)} (missing curve or key-count change)";
                    return false;
                }
                var rk = rc.keys;
                var fk = fc.keys;
                for (int i = 0; i < rk.Length; i++)
                    if (Mathf.Abs(rk[i].time - fk[i].time) > 1e-6f || Mathf.Abs(rk[i].value - fk[i].value) > 1e-6f)
                    {
                        why = $"retired form REDDED on {BindingKey(b)} @key {i}";
                        return false;
                    }
                compared++;
            }
            return true;
        }

        // ===================== injected-defect helpers (negative controls only) =====================

        /// <summary>The 4 local-rotation component curves of ONE bone, kept together so the rotation is read and
        /// written as a unit.</summary>
        private sealed class QuatCurves
        {
            public readonly EditorCurveBinding[] B = new EditorCurveBinding[4];
            public readonly AnimationCurve[] C = new AnimationCurve[4];
            public bool Complete => C[0] != null && C[1] != null && C[2] != null && C[3] != null;
        }

        private static QuatCurves FindQuatCurves(AnimationClip clip, string boneLastSeg)
        {
            var q = new QuatCurves();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (LastSegment(b.path) != boneLastSeg) continue;
                int c = Array.IndexOf(QuatProps, b.propertyName);
                if (c < 0) continue;
                q.B[c] = b;
                q.C[c] = AnimationUtility.GetEditorCurve(clip, b);
            }
            return q;
        }

        /// <summary>An in-memory copy of a clip with every curve carried verbatim — the base for the injected
        /// defects below. Never written to disk.</summary>
        private static AnimationClip CloneClip(AnimationClip src, string name)
        {
            var dst = new AnimationClip { name = name, frameRate = src.frameRate };
            AnimationUtility.SetAnimationClipSettings(dst, AnimationUtility.GetAnimationClipSettings(src));
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                var c = AnimationUtility.GetEditorCurve(src, b);
                if (c != null) AnimationUtility.SetEditorCurve(dst, b, c);
            }
            return dst;
        }

        /// <summary>Re-key ONE bone's rotation quaternion onto the clip's OWN authored frame grid, applying
        /// <paramref name="mutate"/> at each frame index. Re-keying at the frame times is metric-transparent for
        /// every frame-grid read in this file — the curve values AT those times are reproduced exactly — so the
        /// only thing that moves is what <paramref name="mutate"/> moves.</summary>
        private static void ReKeyBoneRotationOnFrameGrid(AnimationClip clip, string boneLastSeg,
                                                         Func<int, Quaternion, Quaternion> mutate)
        {
            var q = FindQuatCurves(clip, boneLastSeg);
            Assert.IsTrue(q.Complete, $"the clip must carry a complete local-rotation quad for {boneLastSeg}");

            float fps = ClipFps(clip);
            int frames = FrameCount(clip);
            var times = new float[frames + 1];
            var quats = new Quaternion[frames + 1];
            for (int f = 0; f <= frames; f++)
            {
                float t = Mathf.Min(clip.length, f / fps);
                times[f] = t;
                var src = new Quaternion(q.C[0].Evaluate(t), q.C[1].Evaluate(t), q.C[2].Evaluate(t), q.C[3].Evaluate(t));
                quats[f] = mutate(f, src.normalized).normalized;
            }
            // The 4 component curves interpolate as SCALARS, so keep the hemisphere continuous (same reason
            // PickaxeMineCurveFix does it) — q and -q are the same rotation, a sign flip is a curve kink.
            for (int f = 1; f <= frames; f++)
                if (Quaternion.Dot(quats[f], quats[f - 1]) < 0f)
                    quats[f] = new Quaternion(-quats[f].x, -quats[f].y, -quats[f].z, -quats[f].w);

            for (int k = 0; k < 4; k++)
            {
                var keys = new Keyframe[frames + 1];
                for (int f = 0; f <= frames; f++)
                    keys[f] = new Keyframe(times[f],
                        k == 0 ? quats[f].x : k == 1 ? quats[f].y : k == 2 ? quats[f].z : quats[f].w);
                AnimationUtility.SetEditorCurve(clip, q.B[k], new AnimationCurve(keys));
            }
        }

        /// <summary>Tip the torso past the ceiling at ONE frame. The rotation AXIS is MEASURED off the live rig
        /// (the world axis perpendicular to the hips-&gt;head vector and world up, pulled back into the Hips bone's
        /// PARENT frame) and the SIGN is chosen by trying both and keeping the one that actually increases tilt —
        /// this rig's bone-local axes are arbitrary and a guessed axis tips the wrong way
        /// (procedural-animation-verbs.md, "Measure bone axes FIRST"). Returns the achieved tilt in degrees.</summary>
        private float InjectTorsoTiltSpikeAtFrame(AnimationClip clip, int frame, float spikeDeg)
        {
            float t = Mathf.Min(clip.length, frame / ClipFps(clip));
            clip.SampleAnimation(Model, t);
            Vector3 v = _head.position - _hips.position;
            Vector3 axW = Vector3.Cross(v, Vector3.up);
            if (axW.sqrMagnitude < 1e-8f) axW = Vector3.right;
            axW.Normalize();
            Transform parent = _hips.parent;
            Vector3 axP = parent != null ? parent.InverseTransformDirection(axW).normalized : axW;

            float best = -1f;
            Quaternion bestDelta = Quaternion.identity;
            foreach (float sign in new[] { 1f, -1f })
            {
                var delta = Quaternion.AngleAxis(sign * spikeDeg, axP);
                clip.SampleAnimation(Model, t);
                _hips.localRotation = delta * _hips.localRotation;
                float tilt = Vector3.Angle(_head.position - _hips.position, Vector3.up);
                if (tilt > best) { best = tilt; bestDelta = delta; }
            }
            ReKeyBoneRotationOnFrameGrid(clip, BonePrefix + "Hips", (f, q) => f == frame ? bestDelta * q : q);
            return best;
        }

        /// <summary>Nudge one key of one curve so the clip carries an edit the generator never made. Returns the
        /// perturbed binding's key, so the negative control can assert the guard's failure message NAMES it.</summary>
        private static string PerturbCurve(AnimationClip clip, string boneLastSeg, string propertyName, float delta)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (LastSegment(b.path) != boneLastSeg || b.propertyName != propertyName) continue;
                var c = AnimationUtility.GetEditorCurve(clip, b);
                Assert.IsNotNull(c, $"no curve for {BindingKey(b)}");
                Assert.Greater(c.length, 0, $"curve {BindingKey(b)} carries no keys");
                var keys = c.keys;
                int at = keys.Length / 2;
                keys[at].value += delta;
                AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
                return BindingKey(b);
            }
            Assert.Fail($"the clip must carry a {boneLastSeg}.{propertyName} curve to perturb");
            return null;
        }

        private static AnimationClip FindFbxClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }
    }
}
