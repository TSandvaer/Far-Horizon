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
