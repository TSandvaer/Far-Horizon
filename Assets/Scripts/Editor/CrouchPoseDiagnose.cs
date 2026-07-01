using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// ONE-SHOT diagnostic instrument (86caa3kur / #197 crouch-regression) — confirm WHY the loopBlend regen
    /// (loopBlend 0→1 on the crouch clips) broke the crouch stance the Sponsor saw vanish at v5.
    ///
    /// Poses the REAL Crouching Idle / Sneak Walk clips on the production Idle.fbx rig via
    /// AnimationClip.SampleAnimation (headless-safe — the Animator never ticks headlessly, deltaTime≈0; the walk-
    /// float saga lesson) and measures the scale-immune rendered-sole-to-Hips body height for EACH clip. Then it
    /// FLIPS the importer's loopPose OFF, re-imports, and re-measures — so the A/B directly shows whether
    /// loopBlend:1 raises the body / flattens the crouch depth. Run headless:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.CrouchPoseDiagnose.Run
    /// Restores every importer it touched (no asset churn left behind).
    /// </summary>
    public static class CrouchPoseDiagnose
    {
        [MenuItem("FarHorizon/Diagnose/Crouch Pose (loopBlend A/B)")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[crouch-diag] ===== CROUCH POSE loopBlend A/B (86caa3kur #197) =====");

            // Build the production rig: player root → avatar root scaled 1.8 → Idle.fbx model child.
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            if (fbx == null) { Debug.LogError("[crouch-diag] Idle FBX missing at " + CharacterAssetGen.IdleFbxPath); return; }

            var playerRoot = new GameObject("__crouchDiagPlayer");
            var avatarRoot = new GameObject("__crouchDiagAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * 1.8f;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null) { Debug.LogError("[crouch-diag] no SMR on Idle FBX"); Object.DestroyImmediate(playerRoot); return; }

            // Report the standing baseline (BreathingIdle = the Idle STATE clip) for comparison.
            ReportClip(sb, model, smr, CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip, "STAND-Idle");
            ReportClipAfterSoleGround(sb, model, smr, CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip, "STAND-Idle");

            // === Crouch Idle: measure WITH current committed meta (loopBlend:1), then flip loopPose OFF and re-measure ===
            MeasureWithLoopPoseAB(sb, model, smr, CharacterAssetGen.CrouchIdleFbxPath, CharacterAssetGen.CrouchIdleClip, "CROUCH-Idle");
            MeasureWithLoopPoseAB(sb, model, smr, CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip, "CROUCH-Sneak");

            // THE DECISIVE CHECK — does the production modelSoleGround math cancel the crouch (lift the hips back)?
            ReportClipAfterSoleGround(sb, model, smr, CharacterAssetGen.CrouchIdleFbxPath, CharacterAssetGen.CrouchIdleClip, "CROUCH-Idle");
            ReportClipAfterSoleGround(sb, model, smr, CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip, "CROUCH-Sneak");

            Object.DestroyImmediate(playerRoot);
            sb.AppendLine("[crouch-diag] ===== END =====");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// GAIT-SEAM loopPose A/B (86caa3kur / #197 — the OPEN coverage gap the adversarial verification named).
        ///
        /// The existing Run() A/B measures the crouch-IDLE body HEIGHT (single mid-clip frame + an 8-sample sole
        /// range). It is STRUCTURALLY BLIND to a MOVING-gait loop-seam pose pop: it never samples the SNEAK WALK
        /// clip at the frame-N→frame-0 WRAP, and it measures body height, not per-bone POSE discontinuity. And the
        /// -verifySneak CoV samples agent.transform ONLY — also blind to a body-pose seam on the CHILD/model bones.
        ///
        /// THIS measures the POSE discontinuity AT THE LOOP SEAM on the MODEL/CHILD bone transforms (Hips + both
        /// feet + both hands + both toes) — the exact place a "left, right, JERK" per-cycle pop would live. For a
        /// looped clip the seam is between the last authored frame (t = length − 1/frameRate) and frame 0. A clean
        /// loop has a near-zero seam delta; loopBlend:0 leaves the raw authored end≠start pose, which SNAPS once
        /// per ~28-frame gait cycle = the Sponsor's jerk. loopBlend (C# `loopPose`) is baked at IMPORT: it ramps
        /// the frame0−frameEnd delta across the clip so the ends meet. SampleAnimation on the RE-IMPORTED clip
        /// reflects the bake, so the A/B (committed loopBlend:1 vs flipped OFF, reimport, re-measure, restore) is
        /// valid — it is invisible to a normalizedTime trace (a clean TIME wrap is not a clean POSE wrap).
        ///
        /// Reports, in DEGREES (rotation) + METRES (position, scale-immune child-local), per bone AND summed:
        ///   seam pose delta with loopBlend:1  (committed / shipped) vs loopBlend:0 (flipped).
        /// Question answered: does loopBlend:1 actually collapse the gait seam, and what residual remains?
        /// Run headless:
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.CrouchPoseDiagnose.RunGaitSeam
        /// Restores the importer it touches (no asset churn left behind).
        /// </summary>
        [MenuItem("FarHorizon/Diagnose/Sneak-Gait Loop Seam (loopBlend A/B)")]
        public static void RunGaitSeam()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[gait-seam] ===== SNEAK-WALK GAIT LOOP-SEAM loopBlend A/B (86caa3kur #197) =====");
            sb.AppendLine("[gait-seam] Measures per-bone POSE discontinuity at the frame-last->frame-0 loop wrap on");
            sb.AppendLine("[gait-seam] the MODEL/CHILD bones (hips/feet/hands/toes) — the seam the CoV + height A/B are blind to.");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            if (fbx == null) { Debug.LogError("[gait-seam] Idle FBX missing at " + CharacterAssetGen.IdleFbxPath); return; }
            var model = Object.Instantiate(fbx);
            model.transform.localScale = Vector3.one; // scale-immune: measure child-LOCAL pose deltas
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null) { Debug.LogError("[gait-seam] no SMR on Idle FBX"); Object.DestroyImmediate(model); return; }

            // The body bones a per-cycle gait pop would show on. Toe = the foot-plant tell; Hand = the axe-follow tell.
            string[] boneTokens =
            {
                "hips", "leftupleg", "rightupleg",
                "leftfoot", "rightfoot", "lefttoebase", "righttoebase",
                "leftforearm", "rightforearm", "lefthand", "righthand",
            };

            // For contrast: also measure the standing BREATHING-IDLE seam (the shipped calm loop) so the sneak
            // seam has an in-run "this is what a clean-enough loop reads as" reference.
            MeasureSeamAB(sb, model, smr, boneTokens,
                CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip, "STAND-BreathingIdle");
            MeasureSeamAB(sb, model, smr, boneTokens,
                CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip, "SNEAK-Walk (the gait)");

            Object.DestroyImmediate(model);
            sb.AppendLine("[gait-seam] ===== END =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // Sample the clip at the last authored frame + at frame 0; sum the per-bone LOCAL rotation-angle (deg) +
        // LOCAL position (m) deltas across the named bones. That is the loop-seam pose discontinuity.
        private static void MeasureSeam(GameObject model, SkinnedMeshRenderer smr, string[] boneTokens,
            AnimationClip clip, out float sumRotDeg, out float maxRotDeg, out string worstRotBone,
            out float sumPosM, out float maxPosM, out string worstPosBone, out int boneN)
        {
            var bones = ResolveBones(model.transform, boneTokens); // token -> transform
            boneN = bones.Count;
            float frameRate = clip.frameRate > 0f ? clip.frameRate : 30f;
            float lastT = Mathf.Max(0f, clip.length - 1f / frameRate); // frame just before the wrap

            // pose at the last authored frame
            clip.SampleAnimation(model, lastT);
            var rotLast = new Quaternion[boneN];
            var posLast = new Vector3[boneN];
            int k = 0;
            foreach (var kv in bones) { rotLast[k] = kv.Value.localRotation; posLast[k] = kv.Value.localPosition; k++; }

            // pose at frame 0 (where the loop restarts)
            clip.SampleAnimation(model, 0f);
            sumRotDeg = 0f; maxRotDeg = 0f; worstRotBone = "-";
            sumPosM = 0f; maxPosM = 0f; worstPosBone = "-";
            k = 0;
            foreach (var kv in bones)
            {
                float rot = Quaternion.Angle(rotLast[k], kv.Value.localRotation); // degrees
                float pos = (posLast[k] - kv.Value.localPosition).magnitude;      // child-local metres
                sumRotDeg += rot; sumPosM += pos;
                if (rot > maxRotDeg) { maxRotDeg = rot; worstRotBone = kv.Key; }
                if (pos > maxPosM) { maxPosM = pos; worstPosBone = kv.Key; }
                k++;
            }
        }

        // A/B the seam: committed importer (loopBlend:1) -> flip loopPose OFF -> reimport -> re-measure -> restore.
        private static void MeasureSeamAB(StringBuilder sb, GameObject model, SkinnedMeshRenderer smr,
            string[] boneTokens, string fbxPath, string token, string label)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) { sb.AppendLine($"[gait-seam] {label}: importer missing @ {fbxPath}"); return; }
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) { sb.AppendLine($"[gait-seam] {label}: no clips @ {fbxPath}"); return; }
            bool committed = clips[0].loopPose;

            // A) committed (shipped) state
            var clipA = FindClipLocal(fbxPath, token);
            if (clipA == null) { sb.AppendLine($"[gait-seam] {label}: CLIP NOT FOUND ({token})"); return; }
            MeasureSeam(model, smr, boneTokens, clipA,
                out float aRot, out float aMaxRot, out string aRotB, out float aPos, out float aMaxPos, out string aPosB, out int nBones);
            sb.AppendLine($"[gait-seam] {label} [committed loopBlend={committed}] bones={nBones} clipLen={clipA.length:F3}s@{clipA.frameRate:F0}fps");
            sb.AppendLine($"[gait-seam]   SEAM rot: sum={aRot:F2}deg max={aMaxRot:F2}deg(@{aRotB})  pos: sum={aPos:F4}m max={aMaxPos:F4}m(@{aPosB})");

            // B) flip loopPose, reimport, re-measure
            bool original = clips[0].loopPose;
            for (int i = 0; i < clips.Length; i++) clips[i].loopPose = !original;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var clipB = FindClipLocal(fbxPath, token);
            if (clipB != null)
            {
                MeasureSeam(model, smr, boneTokens, clipB,
                    out float bRot, out float bMaxRot, out string bRotB, out float bPos, out float bMaxPos, out string bPosB, out int _);
                sb.AppendLine($"[gait-seam] {label} [flipped  loopBlend={!original}]");
                sb.AppendLine($"[gait-seam]   SEAM rot: sum={bRot:F2}deg max={bMaxRot:F2}deg(@{bRotB})  pos: sum={bPos:F4}m max={bMaxPos:F4}m(@{bPosB})");
                float rotDrop = bRot > 1e-4f ? (1f - aRot / bRot) * 100f : 0f;
                sb.AppendLine($"[gait-seam]   VERDICT: loopBlend:1 cuts the summed seam rotation by {rotDrop:F1}% " +
                              $"(OFF={bRot:F2}deg -> ON={aRot:F2}deg). Residual ON = {aRot:F2}deg summed / {aMaxRot:F2}deg worst-bone.");
            }
            // restore committed state
            clips = importer.clipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopPose = original;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            sb.AppendLine($"[gait-seam] {label}: restored loopBlend={original}");
        }

        // token -> child transform (first match by trailing bone token, mixamorig:-prefix tolerant). Ordered.
        private static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Transform>>
            ResolveBones(Transform root, string[] boneTokens)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Transform>>();
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var tok in boneTokens)
            {
                Transform found = null;
                foreach (var t in all)
                {
                    string n = t.name.ToLowerInvariant();
                    int colon = n.LastIndexOf(':');
                    if (colon >= 0) n = n.Substring(colon + 1);
                    if (n == tok) { found = t; break; }
                }
                if (found != null)
                    result.Add(new System.Collections.Generic.KeyValuePair<string, Transform>(tok, found));
            }
            return result;
        }

        private static AnimationClip FindClipLocal(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }

        // Pose every 1/8 of the clip; return (minSoleY, maxSoleY, hipsLocalYatMid) — scale-immune.
        private static void MeasureClip(GameObject model, SkinnedMeshRenderer smr, AnimationClip clip,
            out float minSole, out float maxSole, out float hipsWorldY)
        {
            minSole = float.PositiveInfinity; maxSole = float.NegativeInfinity;
            for (int i = 0; i <= 8; i++)
            {
                clip.SampleAnimation(model, clip.length * i / 8f);
                float s = ScaleImmuneSoleWorldY(smr);
                if (s < minSole) minSole = s;
                if (s > maxSole) maxSole = s;
            }
            // Hips world-Y at mid-clip (a body-height proxy independent of foot plant).
            clip.SampleAnimation(model, clip.length * 0.5f);
            var hips = FindHips(model.transform);
            hipsWorldY = hips != null
                ? Matrix4x4.TRS(hips.position, Quaternion.identity, Vector3.one).MultiplyPoint3x4(Vector3.zero).y
                : float.NaN;
        }

        private static void ReportClip(StringBuilder sb, GameObject model, SkinnedMeshRenderer smr,
            string fbxPath, string token, string label)
        {
            var clip = FindClip(fbxPath, token);
            if (clip == null) { sb.AppendLine($"[crouch-diag] {label}: CLIP NOT FOUND ({token} @ {fbxPath})"); return; }
            MeasureClip(model, smr, clip, out float lo, out float hi, out float hipsY);
            sb.AppendLine($"[crouch-diag] {label}: soleY[min={lo:F3} max={hi:F3}] hipsWorldY@mid={hipsY:F3}");
        }

        // THE DECISIVE CHECK: apply the EXACT production modelSoleGround math to the clip pose and measure the
        // resulting hips. If sole-grounding the lowered crouch pose RAISES the hips back toward standing, the
        // crouch is being cancelled by modelSoleGround (the real cause; loopBlend-independent). plantWorldY=0,
        // rootYScale=1.8 (the production avatar-root scale).
        private static void ReportClipAfterSoleGround(StringBuilder sb, GameObject model, SkinnedMeshRenderer smr,
            string fbxPath, string token, string label)
        {
            var clip = FindClip(fbxPath, token);
            if (clip == null) { sb.AppendLine($"[crouch-diag] {label}: CLIP NOT FOUND"); return; }
            const float plantWorldY = 0f;
            float rootYScale = 1.8f;
            // sample mid-clip, measure raw sole, apply production grounding, re-measure hips + sole
            clip.SampleAnimation(model, clip.length * 0.5f);
            float rawSole = ScaleImmuneSoleWorldY(smr);
            float modelLocalY = FarHorizon.CastawayCharacter.ComputeModelGroundLocalY(
                rawSole, plantWorldY, model.transform.localPosition.y, rootYScale);
            var mlp = model.transform.localPosition; float savedY = mlp.y; mlp.y = modelLocalY;
            model.transform.localPosition = mlp;
            // re-sample at the same frame so the offset applies to the posed mesh
            clip.SampleAnimation(model, clip.length * 0.5f);
            float groundedSole = ScaleImmuneSoleWorldY(smr);
            var hips = FindHips(model.transform);
            float hipsY = hips != null ? hips.position.y : float.NaN;
            sb.AppendLine($"[crouch-diag] {label} AFTER modelSoleGround: rawSole={rawSole:F3} -> groundedSole={groundedSole:F3} hipsWorldY@mid={hipsY:F3} (modelLocalY={modelLocalY:F3})");
            mlp.y = savedY; model.transform.localPosition = mlp;
        }

        // The A/B: measure the clip with the committed importer, then flip loopPose OFF, reimport, re-measure, restore.
        private static void MeasureWithLoopPoseAB(StringBuilder sb, GameObject model, SkinnedMeshRenderer smr,
            string fbxPath, string token, string label)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) { sb.AppendLine($"[crouch-diag] {label}: importer missing @ {fbxPath}"); return; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool committedLoopPose = clips != null && clips.Length > 0 && clips[0].loopPose;

            // A) committed state
            ReportClip(sb, model, smr, fbxPath, token, $"{label} [committed loopPose={committedLoopPose}]");

            // B) flip loopPose to the OPPOSITE, reimport, re-measure
            if (clips != null && clips.Length > 0)
            {
                bool original = clips[0].loopPose;
                for (int i = 0; i < clips.Length; i++) clips[i].loopPose = !original;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                ReportClip(sb, model, smr, fbxPath, token, $"{label} [flipped  loopPose={!original}]");

                // restore
                clips = importer.clipAnimations;
                for (int i = 0; i < clips.Length; i++) clips[i].loopPose = original;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                sb.AppendLine($"[crouch-diag] {label}: restored loopPose={original}");
            }
        }

        private static float ScaleImmuneSoleWorldY(SkinnedMeshRenderer smr)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, false);
            var verts = baked.vertices;
            Matrix4x4 l2w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            float minY = float.PositiveInfinity;
            foreach (var v in verts) { float y = l2w.MultiplyPoint3x4(v).y; if (y < minY) minY = y; }
            Object.DestroyImmediate(baked);
            return minY;
        }

        private static Transform FindHips(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.EndsWith("hips") || n == "mixamorig:hips") return t;
            }
            return null;
        }

        private static AnimationClip FindClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }
    }
}
