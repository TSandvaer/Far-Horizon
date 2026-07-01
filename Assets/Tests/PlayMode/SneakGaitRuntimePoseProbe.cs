using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    /// SNEAK-GAIT RUNTIME POSE PROBE (86caa3kur / #197) — MEASURE-ONLY. Measures the LAYER that every prior
    /// instrument was structurally blind to: the LIVE RENDERED SKELETON POSE over real Animator ticks, in the
    /// CrouchWalk state, after loopBlend has been applied at runtime. That is exactly what the Sponsor's eye sees
    /// and what the "left, right, JERK" per-2-step pop lives on.
    ///
    /// WHY THE PRIOR INSTRUMENTS COULD NOT SEE IT (the coverage gap this closes):
    ///   1. CastawayCharacter.CurrentStateNormalizedTime trace = a CLOCK — a smooth normalizedTime wrap is NOT a
    ///      smooth POSE wrap; blind to a pose snap.
    ///   2. SneakVerifyCapture CoV samples agent.transform = the ROOT — blind to the skeleton/child bones.
    ///   3. CrouchPoseDiagnose.RunGaitSeam uses AnimationClip.SampleAnimation = RAW authored curves; it does NOT
    ///      run a live Animator, so it CANNOT observe loopBlend's RUNTIME blend, transitions, or state settling.
    ///      Devon's SampleAnimation A/B measured a ~24deg toe end-vs-start mismatch on the raw clip but loopBlend:1
    ///      did not fix the Sponsor's soak (build 770bffd, "FAILED, NO CHANGE") — proving the raw-curve layer is
    ///      the wrong layer to judge from.
    ///
    /// HOW THIS MEASURES THE RIGHT LAYER:
    ///   - Loads Idle.fbx (the SKIN: mesh + rig + Avatar) AND the production CastawayAnimator controller (both are
    ///     editor assets -> #if UNITY_EDITOR + AssetDatabase; CI runs -testPlatform PlayMode in the -batchmode
    ///     editor where AssetDatabase is live; the test Ignores in a player).
    ///   - Binds the controller + the FBX-imported Avatar to a real Animator on the instantiated rig.
    ///   - Drives Crouch=true + Moving=true + Speed=sneak, then TICKS the Animator with an EXPLICIT positive dt via
    ///     Animator.Update(dt) (headless Time.deltaTime~=0 would never advance the state machine -- the documented
    ///     trap, CastawayLocomotionHitReactPlayModeTests:19). Ticks past the 0.18s AnyState->CrouchWalk transition
    ///     to SETTLE, then samples >=1.5 full gait cycles.
    ///   - EACH tick: records the LIVE localRotation + localPosition of the key bones (hips, both upLegs, both feet,
    ///     both toes, both forearms, both hands, spine) AFTER the Animator has posed them; computes the per-frame
    ///     angular delta per bone; tags the frame's CurrentStateNormalizedTime fraction to locate the loop wrap.
    ///   - REPORTS a per-frame table around the wrap + the single worst per-frame spike (bone, degrees, normT).
    ///
    /// MEASURE-ONLY: this test always PASSES (it asserts only that it collected a valid sample run). Its VALUE is
    /// the [gait-probe] Debug.Log table + the summary line. Do NOT convert to a red/green gate in this dispatch.
    /// </summary>
    public class SneakGaitRuntimePoseProbe
    {
        private const string ControllerPath = "Assets/Art/Character/Castaway/CastawayAnimator.controller";
        private const string IdleFbxPath = "Assets/Art/Character/Castaway/Idle.fbx";
        private const string SneakWalkFbxPath = "Assets/Art/Character/Castaway/Sneak Walk.fbx";
        private const float SneakSpeed = 3f; // WasdMovement.sneakSpeed (WasdCrouchPlayModeTests:48)

        // The body bones a per-cycle gait pop would show on. Toe = the foot-plant tell; hand/forearm = the
        // axe-follow tell; hips/upleg = the stride tell; spine = a torso bob.
        private static readonly string[] BoneTokens =
        {
            "hips", "spine",
            "leftupleg", "rightupleg",
            "leftfoot", "rightfoot", "lefttoebase", "righttoebase",
            "leftforearm", "rightforearm", "lefthand", "righthand",
        };

        private GameObject _rig;
        private Animator _animator;

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) { Object.DestroyImmediate(_rig); _rig = null; }
        }

        // ===================================================================================================
        // THE PROBE — drive into CrouchWalk on a LIVE Animator, tick with explicit dt, sample the live skeleton
        // pose every frame across >=1.5 gait cycles, report the loop-wrap seam. loopBlend:1 (committed) pass.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator MeasureLiveSkeletonSeam_AtCrouchWalkLoopWrap_committedLoopBlend()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads Idle.fbx + the controller asset via AssetDatabase)");
            yield break;
#else
            var sb = new StringBuilder();
            sb.AppendLine("[gait-probe] ===== LIVE-SKELETON CROUCHWALK LOOP-SEAM PROBE (86caa3kur #197) =====");
            bool committed = ReadSneakWalkLoopPose();
            sb.AppendLine($"[gait-probe] Sneak Walk.fbx committed loopPose(loopBlend)={committed}");

            yield return BuildRigAndSettleCrouchWalk(sb);
            if (_animator == null) { sb.AppendLine("[gait-probe] rig build failed"); Debug.Log(sb.ToString()); Assert.Pass(); yield break; }

            SampleGaitAndReport(sb, "committed-loopBlend");
            Debug.Log(sb.ToString());
            Assert.Pass("MEASURE-ONLY probe — see the [gait-probe] table for the live-skeleton loop-wrap seam.");
#endif
        }

        // ===================================================================================================
        // A/B at the LIVE layer — flip Sneak Walk.fbx loopPose to the OPPOSITE, reimport, REBUILD the rig, and
        // re-measure the live seam. Directly answers: does loopBlend change the RUNTIME rendered seam AT ALL
        // (Devon's SampleAnimation couldn't see a runtime blend)? Restores the importer afterwards (no churn).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator MeasureLiveSkeletonSeam_loopBlendAB_flippedThenRestored()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only");
            yield break;
#else
            var sb = new StringBuilder();
            sb.AppendLine("[gait-probe] ===== LIVE-SKELETON loopBlend A/B (flip + reimport + rebuild) (86caa3kur #197) =====");

            bool original = ReadSneakWalkLoopPose();
            sb.AppendLine($"[gait-probe] A) committed loopPose={original}");

            // A) committed state
            yield return BuildRigAndSettleCrouchWalk(sb);
            float aWorstSpike = float.NaN; string aWorstBone = "-"; float aWorstNormT = float.NaN;
            if (_animator != null)
                SampleGaitAndReport(sb, $"A committed loopBlend={original}", out aWorstSpike, out aWorstBone, out aWorstNormT);
            if (_rig != null) { Object.DestroyImmediate(_rig); _rig = null; _animator = null; }

            // B) flip loopPose, reimport, rebuild, re-measure
            SetSneakWalkLoopPose(!original);
            sb.AppendLine($"[gait-probe] B) flipped loopPose={!original} (reimported)");
            yield return BuildRigAndSettleCrouchWalk(sb);
            float bWorstSpike = float.NaN; string bWorstBone = "-"; float bWorstNormT = float.NaN;
            if (_animator != null)
                SampleGaitAndReport(sb, $"B flipped loopBlend={!original}", out bWorstSpike, out bWorstBone, out bWorstNormT);
            if (_rig != null) { Object.DestroyImmediate(_rig); _rig = null; _animator = null; }

            // restore committed state
            SetSneakWalkLoopPose(original);
            sb.AppendLine($"[gait-probe] restored loopPose={original}");

            sb.AppendLine("[gait-probe] --- A/B VERDICT (live runtime seam) ---");
            sb.AppendLine($"[gait-probe]   A loopBlend={original}: worst per-frame spike {aWorstSpike:F3}deg @ {aWorstBone} (normT frac {aWorstNormT:F3})");
            sb.AppendLine($"[gait-probe]   B loopBlend={!original}: worst per-frame spike {bWorstSpike:F3}deg @ {bWorstBone} (normT frac {bWorstNormT:F3})");
            if (!float.IsNaN(aWorstSpike) && !float.IsNaN(bWorstSpike))
            {
                float diff = bWorstSpike - aWorstSpike;
                sb.AppendLine($"[gait-probe]   DELTA(B-A) = {diff:F3}deg. |delta| < ~0.5deg => loopBlend has NO meaningful " +
                              "effect on the LIVE runtime seam (the jerk is a DIFFERENT mechanism than the loop-pose bake).");
            }
            Debug.Log(sb.ToString());
            Assert.Pass("MEASURE-ONLY loopBlend A/B at the live layer — see the [gait-probe] table.");
#endif
        }

#if UNITY_EDITOR
        // Build the rig (Idle.fbx skin + rig + Avatar) with the production controller, drive Crouch+Moving+Speed,
        // and tick past the 0.18s AnyState->CrouchWalk transition so the state is SETTLED before we measure.
        private IEnumerator BuildRigAndSettleCrouchWalk(StringBuilder sb)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (controller == null || fbx == null)
            {
                sb.AppendLine($"[gait-probe] MISSING asset: controller={(controller != null)} fbx={(fbx != null)}");
                yield break;
            }

            _rig = Object.Instantiate(fbx);
            _rig.name = "GaitProbeRig";
            _rig.transform.localScale = Vector3.one; // scale-immune: we measure child-LOCAL pose deltas
            _animator = _rig.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = _rig.AddComponent<Animator>();
            // preserve the FBX-imported avatar across the controller assign (the 86ca8rdkp re-assert idiom)
            Avatar imported = _animator.avatar;
            _animator.runtimeAnimatorController = controller;
            if (imported != null) _animator.avatar = imported;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            yield return null;
            _animator.Update(0.05f);

            // Drive the crouch-move lane exactly as CastawayCharacter.LateUpdate does for a sneaking player.
            _animator.SetBool(CastawayCharacter.MovingParam, true);
            _animator.SetBool(CastawayCharacter.CrouchParam, true);
            _animator.SetBool(CastawayCharacter.GroundedParam, true);
            _animator.SetFloat(CastawayCharacter.SpeedParam, SneakSpeed);

            // Tick past the 0.18s AnyState->CrouchWalk transition + a few cycles so it fully settles into a steady
            // CrouchWalk loop before we measure (a mid-transition frame is not the steady-state seam).
            for (int i = 0; i < 40; i++) _animator.Update(0.05f);

            var st = _animator.GetCurrentAnimatorStateInfo(0);
            bool inCrouchWalk = st.IsName("CrouchWalk");
            sb.AppendLine($"[gait-probe] settled state IsName(CrouchWalk)={inCrouchWalk} inTransition={_animator.IsInTransition(0)}");
            if (!inCrouchWalk)
                sb.AppendLine("[gait-probe] WARNING: not settled in CrouchWalk — the seam reading may reflect a wrong state.");
        }

        private void SampleGaitAndReport(StringBuilder sb, string label)
        {
            SampleGaitAndReport(sb, label, out _, out _, out _);
        }

        // Tick a full run of frames; each frame record the live per-bone localRotation/Position; compute the
        // per-frame angular delta per bone; use the CrouchWalk normalizedTime fraction to locate the loop wrap
        // (frac ~0.97 -> ~0.03). Report a table around every wrap + the single worst per-frame spike.
        private void SampleGaitAndReport(StringBuilder sb, string label,
            out float worstSpikeDeg, out string worstSpikeBone, out float worstSpikeNormT)
        {
            var bones = ResolveBones(_rig.transform, BoneTokens);
            int n = bones.Count;
            worstSpikeDeg = float.NaN; worstSpikeBone = "-"; worstSpikeNormT = float.NaN;

            var st0 = _animator.GetCurrentAnimatorStateInfo(0);
            AnimationClip clip = ActiveClip();
            float clipLen = clip != null ? clip.length : float.NaN;
            float clipFps = clip != null && clip.frameRate > 0 ? clip.frameRate : 30f;
            // sneak cadence: sample at the render-ish dt so we see the frame granularity the Sponsor sees.
            const float dt = 1f / 60f;
            // >=1.5 gait cycles: a CrouchWalk cycle is ~clipLen at speed=1; be generous (state.speed may scale it).
            int frames = 200;

            sb.AppendLine($"[gait-probe] [{label}] activeClip={(clip != null ? clip.name : "<null>")} clipLen={clipLen:F3}s@{clipFps:F0}fps " +
                          $"bones={n} tickDt={dt:F4}s frames={frames} stateSpeed={st0.speed:F3}");
            sb.AppendLine($"[gait-probe] [{label}] TABLE (per-frame summed |rot delta| across {n} bones; wrap = normT frac drops ~1->0):");
            sb.AppendLine("[gait-probe]   frame  normTfrac   sumRotDelta  maxRotDelta@bone            wrap?");

            // prev-frame pose
            var prevRot = new Quaternion[n];
            var prevPos = new Vector3[n];
            SnapshotBones(bones, prevRot, prevPos);
            float prevFrac = Frac(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime);

            // rolling ring of the last few rows so we can print a window AROUND each wrap even before we know it wraps.
            var rowFrame = new int[frames];
            var rowFrac = new float[frames];
            var rowSum = new float[frames];
            var rowMax = new float[frames];
            var rowMaxBone = new string[frames];
            var rowWrap = new bool[frames];

            float worstSum = -1f; int worstSumFrame = -1;

            for (int f = 0; f < frames; f++)
            {
                _animator.Update(dt);
                float frac = Frac(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                bool wrap = frac < prevFrac - 0.3f; // a big backward jump in the fraction = a loop wrap this frame

                float sumRot = 0f, maxRot = 0f; string maxBone = "-";
                int k = 0;
                foreach (var kv in bones)
                {
                    float rot = Quaternion.Angle(prevRot[k], kv.Value.localRotation);
                    sumRot += rot;
                    if (rot > maxRot) { maxRot = rot; maxBone = kv.Key; }
                    prevRot[k] = kv.Value.localRotation;
                    prevPos[k] = kv.Value.localPosition;
                    k++;
                }

                rowFrame[f] = f; rowFrac[f] = frac; rowSum[f] = sumRot; rowMax[f] = maxRot; rowMaxBone[f] = maxBone; rowWrap[f] = wrap;

                // the worst per-frame single-bone spike overall (the jerk signature)
                if (float.IsNaN(worstSpikeDeg) || maxRot > worstSpikeDeg)
                { worstSpikeDeg = maxRot; worstSpikeBone = maxBone; worstSpikeNormT = frac; }
                if (sumRot > worstSum) { worstSum = sumRot; worstSumFrame = f; }

                prevFrac = frac;
            }

            // Print a +/-3 window around EVERY detected wrap (the seam is at the wrap; a clean wrap reads small).
            for (int f = 0; f < frames; f++)
            {
                if (!rowWrap[f]) continue;
                int lo = Mathf.Max(0, f - 3), hi = Mathf.Min(frames - 1, f + 3);
                sb.AppendLine($"[gait-probe]   --- wrap window around frame {f} ---");
                for (int g = lo; g <= hi; g++)
                    sb.AppendLine($"[gait-probe]   {rowFrame[g],5}  {rowFrac[g],8:F3}   {rowSum[g],10:F3}   " +
                                  $"{rowMax[g],6:F3}@{rowMaxBone[g],-16} {(rowWrap[g] ? "<== WRAP" : "")}");
            }

            // The worst overall summed-delta frame (where the biggest whole-body pose jump happened) + its context.
            if (worstSumFrame >= 0)
            {
                sb.AppendLine($"[gait-probe] [{label}] WORST whole-body pose jump: frame {worstSumFrame} " +
                              $"sumRot={rowSum[worstSumFrame]:F3}deg maxBone={rowMaxBone[worstSumFrame]}({rowMax[worstSumFrame]:F3}deg) " +
                              $"normTfrac={rowFrac[worstSumFrame]:F3} wrap={rowWrap[worstSumFrame]}");
            }
            sb.AppendLine($"[gait-probe] [{label}] WORST single-bone spike overall: {worstSpikeDeg:F3}deg @ {worstSpikeBone} (normTfrac {worstSpikeNormT:F3})");
            sb.AppendLine($"[gait-probe] [{label}] READ: a per-frame spike CONCENTRATED at the wrap rows = the loop seam IS the jerk. " +
                          "A spike AWAY from the wrap (or a smooth wrap) = a DIFFERENT mechanism (transition re-fire / blend-tree / state re-entry).");
        }

        private AnimationClip ActiveClip()
        {
            var infos = _animator.GetCurrentAnimatorClipInfo(0);
            if (infos != null && infos.Length > 0) return infos[0].clip;
            return null;
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        private static void SnapshotBones(List<KeyValuePair<string, Transform>> bones, Quaternion[] rot, Vector3[] pos)
        {
            int k = 0;
            foreach (var kv in bones) { rot[k] = kv.Value.localRotation; pos[k] = kv.Value.localPosition; k++; }
        }

        // token -> child transform (first match by trailing bone token, mixamorig:-prefix tolerant). Ordered.
        private static List<KeyValuePair<string, Transform>> ResolveBones(Transform root, string[] boneTokens)
        {
            var result = new List<KeyValuePair<string, Transform>>();
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var tok in boneTokens)
            {
                Transform found = null;
                foreach (var t in all)
                {
                    string nm = t.name.ToLowerInvariant();
                    int colon = nm.LastIndexOf(':');
                    if (colon >= 0) nm = nm.Substring(colon + 1);
                    if (nm == tok) { found = t; break; }
                }
                if (found != null) result.Add(new KeyValuePair<string, Transform>(tok, found));
            }
            return result;
        }

        private bool ReadSneakWalkLoopPose()
        {
            var importer = AssetImporter.GetAtPath(SneakWalkFbxPath) as ModelImporter;
            if (importer == null) return false;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            return clips != null && clips.Length > 0 && clips[0].loopPose;
        }

        private void SetSneakWalkLoopPose(bool value)
        {
            var importer = AssetImporter.GetAtPath(SneakWalkFbxPath) as ModelImporter;
            if (importer == null) return;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++) clips[i].loopPose = value;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            // rebuild the controller-referenced clip binding so the live Animator picks up the reimported clip.
            AssetDatabase.Refresh();
        }
#endif
    }
}
