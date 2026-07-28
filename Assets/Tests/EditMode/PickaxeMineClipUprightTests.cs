using System.Collections.Generic;
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
    ///   (4) NO NEW POP — the repaired clip's worst per-authored-frame whole-skeleton step may not exceed the raw
    ///       clip's. Re-keying + re-smoothing tangents is exactly how a fold fix can trade a bad pose for a jerk;
    ///       this refuses that trade.
    ///   (5) FEET STAY PLANTED — un-hinging a pelvis swings the legs with it unless compensated, which would trade
    ///       a contorted torso for FLOATING feet (the walk-float saga's failure mode). Asserts the repaired foot
    ///       Y-band + horizontal travel do not grow materially over raw.
    ///   (6) SCOPE — every curve that is NOT Hips-rotation / upper-leg-rotation is key-for-key IDENTICAL to the
    ///       raw clip. This is the machine proof of the ticket's "no downstream churn" constraint: arms,
    ///       shoulders, head, spine, lower legs and the hips POSITION/root-motion are untouched.
    ///   (7) STILL GENERIC — the source FBX importer stays animationType Generic. The ticket says to verify this
    ///       rather than trust a claim; a Humanoid flip explodes the mesh under the scaled hierarchy (86ca8rdkp).
    /// </summary>
    public class PickaxeMineClipUprightTests
    {
        private const int Samples = 41;              // whole-clip sweep for the tilt/foot reads
        private const string BonePrefix = "mixamorig:";
        // Tolerances: the fix re-keys the pelvis + upper legs, so bit-exact equality on THOSE is not the bar —
        // "no WORSE than raw" is. Small absolute slacks keep the guards from flapping on float noise.
        private const float StepSlackDeg = 1.0f;
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
            var model = Object.Instantiate(fbx, avatar.transform, false);
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
            if (_root != null) Object.DestroyImmediate(_root);
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
            float peak = PeakTorsoTilt(repaired, out float atT);
            Assert.Less(peak, PickaxeMineCurveFix.PeakTiltCeilingDeg,
                $"the repaired mine clip's peak torso tilt must stay under {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg " +
                $"off vertical (the axe swing's unflagged 43.3deg band) — measured {peak:F1}deg at t={atT:F3}. " +
                "Above this the body reads as doubling over at a chest-height boulder (the soak-5 defect).");
        }

        // ---------- (3) the raw clip still folds (the fix does real work) ----------
        [Test]
        public void RawPickaxeClip_StillFoldsPastTheCeiling_SoTheRepairIsProvenToDoWork()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            Assert.IsNotNull(raw, "the raw Attack_Pickaxe clip must import at " + CharacterAssetGen.AttackPickaxeFbxPath);
            float peak = PeakTorsoTilt(raw, out float atT);
            Assert.Greater(peak, PickaxeMineCurveFix.PeakTiltCeilingDeg,
                $"the RAW source clip is expected to still fold past {PickaxeMineCurveFix.PeakTiltCeilingDeg}deg " +
                $"(measured {peak:F1}deg at t={atT:F3}) — that is what makes the repaired-clip assertion meaningful. " +
                "If this reds, the SOURCE clip was re-sourced/replaced (ticket Route 1): re-measure with " +
                "AttackClipPoseDiag and RETIRE PickaxeMineCurveFix rather than double-correcting a clean clip.");
        }

        // ---------- (4) no new pop ----------
        [Test]
        public void RepairedPickaxeClip_AddsNoPerFrameStep_SoAFoldFixCannotBecomeAJerk()
        {
            var raw = FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            Assert.IsNotNull(raw, "raw clip missing");
            Assert.IsNotNull(repaired, "repaired clip missing");
            float rawStep = WorstPerFrameStep(raw, out string rawBone);
            float fixStep = WorstPerFrameStep(repaired, out string fixBone);
            Assert.LessOrEqual(fixStep, rawStep + StepSlackDeg,
                $"the repaired clip's worst per-authored-frame whole-skeleton step ({fixStep:F1}deg @{fixBone}) must " +
                $"not exceed the raw clip's ({rawStep:F1}deg @{rawBone}) — re-keying the pelvis + re-smoothing " +
                "tangents must not trade the contorted pose for a visible jerk (the #197 defect class).");
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

            int compared = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(raw))
            {
                bool isQuat = PickaxeMineCurveFix.QuatComponent(b.propertyName) >= 0;
                string seg = PickaxeMineCurveFix.LastSeg(b.path);
                bool edited = isQuat && (seg.EndsWith("Hips") || PickaxeMineCurveFix.MatchesUpLeg(seg));
                if (edited) continue;

                var rc = AnimationUtility.GetEditorCurve(raw, b);
                var fc = AnimationUtility.GetEditorCurve(repaired, b);
                Assert.IsNotNull(fc, $"the repaired clip must still carry the curve {b.path}.{b.propertyName} " +
                                     "— a dropped passthrough curve silently deletes motion.");
                Assert.AreEqual(rc.length, fc.length,
                    $"key count changed on an UNEDITED curve {b.path}.{b.propertyName}");
                for (int i = 0; i < rc.length; i++)
                {
                    Assert.AreEqual(rc.keys[i].time, fc.keys[i].time, 1e-6f,
                        $"key time changed on an UNEDITED curve {b.path}.{b.propertyName} @{i}");
                    Assert.AreEqual(rc.keys[i].value, fc.keys[i].value, 1e-6f,
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

        // ===================== measurement helpers =====================

        /// <summary>Peak torso lean off VERTICAL (hips->head vs world up) over the clip, on the live rig.</summary>
        private float PeakTorsoTilt(AnimationClip clip, out float atT)
        {
            float peak = -1f; atT = 0f;
            for (int i = 0; i < Samples; i++)
            {
                float nt = i / (float)(Samples - 1);
                clip.SampleAnimation(Model, nt * clip.length);
                float tilt = Vector3.Angle(_head.position - _hips.position, Vector3.up);
                if (tilt > peak) { peak = tilt; atT = nt; }
            }
            return peak;
        }

        /// <summary>Worst single-authored-frame local-rotation change across every rig bone (the pop detector).</summary>
        private float WorstPerFrameStep(AnimationClip clip, out string worstBone)
        {
            worstBone = "-";
            float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
            int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps));
            var prev = new Quaternion[_bones.Count];
            float worst = 0f;
            for (int f = 0; f <= frames; f++)
            {
                clip.SampleAnimation(Model, Mathf.Min(clip.length, f / fps));
                for (int b = 0; b < _bones.Count; b++)
                {
                    var q = _bones[b].localRotation;
                    if (f > 0)
                    {
                        float d = Quaternion.Angle(q, prev[b]);
                        if (d > worst) { worst = d; worstBone = _bones[b].name.Replace(BonePrefix, ""); }
                    }
                    prev[b] = q;
                }
            }
            return worst;
        }

        /// <summary>Worst-of-the-two-feet horizontal travel and dip-below-own-start, in metres. Sampled on the
        /// clip's own authored FRAME grid — a coarse sweep can step straight over a brief one-frame dip.</summary>
        private void FootMetrics(AnimationClip clip, out float worstTravel, out float worstDip)
        {
            float lMin = float.MaxValue, rMin = float.MaxValue;
            Vector3 lFirst = Vector3.zero, rFirst = Vector3.zero;
            float lTravel = 0f, rTravel = 0f, lY0 = 0f, rY0 = 0f;
            float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
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

        private static AnimationClip FindFbxClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }
    }
}
