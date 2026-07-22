using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// GAIT-CURVE-SMOOTH regression guards (ticket 86caa3kur / #197 — the mid-cycle toe pop). The live-skeleton
    /// probe (SneakGaitRuntimePoseProbe) CONFIRMED the jerk was a keyframe discontinuity baked into the Sneak Walk
    /// clip's OWN authored curves: the LeftToeBase quaternion tumbled ~80.5deg in ONE frame at normT~=0.907 (once
    /// per gait cycle) — NOT the loop wrap (a clean ~7-13deg), NOT loopBlend (LIVE effect measured 0.000deg). The
    /// fix generates an editable CastawayCrouchWalk_smoothed.anim from the raw FBX clip with ONLY the foot-chain
    /// quaternion spike surgically slerp-smoothed; BuildAnimatorController binds CrouchWalk to that .anim.
    ///
    /// THESE GUARD THE BUG CLASS, not the instance:
    ///   (1) THE SMOOTHED CLIP EXISTS + IS WIRED — CrouchWalk's state motion is the smoothed .anim (a regen that
    ///       dropped the smoothing pass, or a hand-edit that re-pointed CrouchWalk at the RAW spiky FBX clip, reds
    ///       here). CrouchStanceGroundedTests only asserts the clip name Contains "CastawayCrouchWalk" — which the
    ///       smoothed clip satisfies too — so it CANNOT catch a regression back to the raw clip; this test can.
    ///   (2) NO FOOT-CHAIN AUTHORED SPIKE — every foot/toe rotation quaternion in the smoothed clip has per-key
    ///       adjacent angular deltas UNDER a ceiling well below the 80.5deg pop (a future re-import / re-gen that
    ///       re-introduced the tumble, or a threshold mis-tune that under-smoothed it, reds here). Measured on the
    ///       AUTHORED keys via AnimationUtility.GetEditorCurve (headless-valid; no Animator tick needed).
    ///   (3) THE RAW CLIP STILL HAS THE SPIKE — asserts the SOURCE FBX clip is still spiky, so the smoothed clip's
    ///       cleanliness is a REAL fix (the pass actually did something), not a source that silently got clean. If
    ///       the source is ever re-sourced clean (a Sponsor re-download), this reds as a NUDGE to retire the pass.
    /// </summary>
    public class SneakGaitCurveSmoothTests
    {
        // A foot-chain authored quaternion per-key delta ceiling. The raw spike was 156.74deg (LeftToeBase) at the
        // authored layer; a healthy gait per-key foot delta is well under this. 40deg cleanly separates a smoothed
        // clip (worst ~29deg on LeftFoot post-fix) from the raw tumble — leaves margin for legit foot-roll motion.
        private const float FootChainAuthoredCeilingDeg = 40f;
        // The raw clip's known spike floor (it was 156.74deg) — assert the SOURCE still visibly spikes above this,
        // so the fix is proven to be doing real work (guard (3)). Conservative floor well below the measured 156.
        private const float RawSpikeFloorDeg = 60f;

        private static readonly string[] FootChainTokens = { "toebase", "foot" };

        private static AnimationClip FindFbxClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }

        // worst adjacent-key quaternion angular delta (deg) across all foot-chain rotation curves in a clip.
        private static float WorstFootChainAuthoredDelta(AnimationClip clip, out string worstBone)
        {
            worstBone = "-";
            float worst = 0f;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            // group the 4 quaternion components per bone-path.
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (var b in bindings)
            {
                string path = b.path.ToLowerInvariant();
                bool footChain = false;
                foreach (var tok in FootChainTokens) if (path.Contains(tok)) { footChain = true; break; }
                string pl = b.propertyName.ToLowerInvariant();
                bool isQuat = pl == "m_localrotation.x" || pl == "m_localrotation.y" ||
                              pl == "m_localrotation.z" || pl == "m_localrotation.w";
                if (footChain && isQuat) paths.Add(b.path);
            }

            foreach (var path in paths)
            {
                AnimationCurve cx = null, cy = null, cz = null, cw = null;
                foreach (var b in bindings)
                {
                    if (b.path != path) continue;
                    switch (b.propertyName.ToLowerInvariant())
                    {
                        case "m_localrotation.x": cx = AnimationUtility.GetEditorCurve(clip, b); break;
                        case "m_localrotation.y": cy = AnimationUtility.GetEditorCurve(clip, b); break;
                        case "m_localrotation.z": cz = AnimationUtility.GetEditorCurve(clip, b); break;
                        case "m_localrotation.w": cw = AnimationUtility.GetEditorCurve(clip, b); break;
                    }
                }
                if (cx == null || cy == null || cz == null || cw == null) continue;
                int n = cx.length;
                Quaternion prev = Quaternion.identity;
                for (int i = 0; i < n; i++)
                {
                    var q = new Quaternion(cx.keys[i].value, cy.keys[i].value, cz.keys[i].value, cw.keys[i].value);
                    if (i > 0)
                    {
                        float d = Quaternion.Angle(prev, q);
                        if (d > worst) { worst = d; worstBone = path; }
                    }
                    prev = q;
                }
            }
            return worst;
        }

        // (1) the smoothed .anim EXISTS and CrouchWalk is wired to it (not the raw spiky FBX clip).
        [Test]
        public void SmoothedCrouchWalkClip_Exists_AndControllerBindsIt()
        {
            var smoothed = AssetDatabase.LoadAssetAtPath<AnimationClip>(SneakGaitCurveFix.SmoothedClipPath);
            Assert.IsNotNull(smoothed,
                "the smoothed crouch-walk clip must exist at " + SneakGaitCurveFix.SmoothedClipPath +
                " (generated by SneakGaitCurveFix.Generate in PrepareCharacter). A missing .anim means the " +
                "bootstrap/build ships the RAW spiky Sneak Walk clip — the #197 toe pop returns.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);
            AnimatorState crouchWalk = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.name == "CrouchWalk") crouchWalk = cs.state;
            Assert.IsNotNull(crouchWalk, "the controller must have a 'CrouchWalk' state");
            var cwClip = crouchWalk.motion as AnimationClip;
            Assert.IsNotNull(cwClip, "CrouchWalk's motion must be an AnimationClip");
            // The smoothed clip is CastawayCrouchWalk_smoothed; the raw FBX clip is CastawayCrouchWalk. Both would
            // pass a Contains("CastawayCrouchWalk") check (why CrouchStanceGroundedTests can't catch a raw-repoint),
            // so assert the EXACT smoothed name — a repoint to the raw clip has name "CastawayCrouchWalk" (no suffix).
            Assert.AreEqual(SneakGaitCurveFix.SmoothedClipName, cwClip.name,
                "CrouchWalk must be motion'd to the SMOOTHED .anim (" + SneakGaitCurveFix.SmoothedClipName +
                "), NOT the raw FBX clip. Got '" + cwClip.name + "'. A re-point back to the raw clip (name " +
                "'CastawayCrouchWalk') re-opens the #197 toe pop (the raw clip tumbles ~80deg/frame at normT~=0.907).");
            // and it must be the asset at the smoothed path (defensive — a same-named clip elsewhere).
            Assert.AreEqual(SneakGaitCurveFix.SmoothedClipPath, AssetDatabase.GetAssetPath(cwClip),
                "CrouchWalk's clip must be the asset at " + SneakGaitCurveFix.SmoothedClipPath);
        }

        // (2) the smoothed clip has NO foot-chain authored quaternion spike above the ceiling (the tumble is gone).
        [Test]
        public void SmoothedCrouchWalkClip_HasNoFootChainSpike()
        {
            var smoothed = AssetDatabase.LoadAssetAtPath<AnimationClip>(SneakGaitCurveFix.SmoothedClipPath);
            Assert.IsNotNull(smoothed, "the smoothed clip must exist at " + SneakGaitCurveFix.SmoothedClipPath);

            float worst = WorstFootChainAuthoredDelta(smoothed, out string worstBone);
            Assert.Less(worst, FootChainAuthoredCeilingDeg,
                $"the smoothed clip's worst foot-chain per-key quaternion delta must be under {FootChainAuthoredCeilingDeg}deg " +
                $"(got {worst:F2}deg @ {worstBone}). Above this = a re-introduced mid-cycle tumble (the #197 pop; raw was " +
                "156.74deg on LeftToeBase). A regen that dropped/under-tuned the smoothing pass reds here.");
        }

        // (3) the RAW source clip STILL spikes — proving the smoothing pass does real work (not a silently-clean source).
        [Test]
        public void RawSneakWalkClip_StillHasTheSpike_ProvingTheFixIsReal()
        {
            var raw = FindFbxClip(CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip);
            Assert.IsNotNull(raw, "the raw Sneak Walk FBX clip must load at " + CharacterAssetGen.SneakWalkFbxPath);

            float worst = WorstFootChainAuthoredDelta(raw, out string worstBone);
            Assert.Greater(worst, RawSpikeFloorDeg,
                $"the RAW Sneak Walk clip must still carry the foot-chain spike (got {worst:F2}deg @ {worstBone}) — this " +
                $"proves the smoothed clip's cleanliness is a REAL fix, not a source that got re-sourced clean. If the " +
                "Sponsor ever re-downloads a clean Sneak Walk clip this reds, a nudge to retire the smoothing pass.");
        }
    }
}
