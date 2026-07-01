using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// IN-PIPELINE gait-curve smoothing (86caa3kur / #197) — the surgical fix for the MID-CYCLE toe pop that the
    /// live-skeleton probe (SneakGaitRuntimePoseProbe) confirmed: the LeftToeBase quaternion TUMBLES ~80deg in one
    /// frame at normT ~= 0.907 (once per gait cycle) inside the Sneak Walk (CastawayCrouchWalk) clip's OWN authored
    /// curves. It is NOT the loop wrap (a clean ~7-13deg wrap) and NOT loopBlend (LIVE effect measured 0.000deg —
    /// proven inert). It is a keyframe discontinuity baked into the source Mixamo clip.
    ///
    /// WHY A GENERATED .anim (not an importer flag): a ModelImporter-embedded FBX clip's curves are READ-ONLY; you
    /// cannot re-key them through the importer. So this reads the FBX clip's curve bindings, SMOOTHS only the
    /// per-frame rotation spike(s) on the foot/toe bones (targeted slerp-replacement of the anomalous interior
    /// key(s) across the stable bracket + auto-tangent flatten — NOT flattening the whole toe motion), copies
    /// every OTHER curve unchanged, writes an editable {SmoothedClipPath}, and preserves the clip's loop settings.
    /// BuildAnimatorController points CrouchWalk at this .anim instead of the raw FBX clip.
    ///
    /// SCOPE: only ROTATION curves on the toe/foot/leg bones, only where a genuine single-frame quaternion spike
    /// is detected (per-frame Quaternion.Angle far above the local neighbourhood). Every non-spiking curve is a
    /// verbatim copy. Idempotent + reproducible-from-code (the bootstrap re-runs it) — the committed .anim + .meta
    /// ship the fix ([[unity-procedural-committed-assets-go-stale]]).
    /// </summary>
    public static class SneakGaitCurveFix
    {
        // The editable smoothed clip the controller's CrouchWalk state binds instead of the raw FBX take.
        public const string SmoothedClipPath = "Assets/Art/Character/Castaway/CastawayCrouchWalk_smoothed.anim";
        public const string SmoothedClipName = "CastawayCrouchWalk_smoothed";

        // A single-frame per-key quaternion angular jump (deg) above this = a spike to smooth. The live probe read
        // 80.5deg; a healthy gait per-frame toe delta is single-digit-to-~20deg. 30deg cleanly separates them.
        private const float SpikeAngleDeg = 30f;
        // Only smooth bones on the foot chain (the foot-plant/toe pop lives here; hands/spine are a different tell
        // and out of scope for this fix). Matches lefttoebase/righttoebase/leftfoot/rightfoot + the ...Leg bones.
        private static readonly string[] FootChainTokens = { "toebase", "foot" };

        [MenuItem("FarHorizon/Fix/Smooth Sneak Gait Toe Curves (generate .anim)")]
        public static void RunMenu()
        {
            var sb = new StringBuilder();
            bool ok = Generate(sb);
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Reads the raw FBX CrouchWalk clip, smooths the foot-chain rotation spikes, writes the editable .anim.
        /// Returns true on success. Call from PrepareCharacter BEFORE BuildAnimatorController.
        /// </summary>
        public static bool Generate(StringBuilder sb)
        {
            sb.AppendLine("[gait-fix] ===== SMOOTH SNEAK GAIT TOE CURVES (86caa3kur #197) =====");
            AnimationClip src = FindClip(CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip);
            if (src == null)
            {
                sb.AppendLine("[gait-fix] ERROR: raw CastawayCrouchWalk clip not found @ " + CharacterAssetGen.SneakWalkFbxPath);
                return false;
            }
            sb.AppendLine($"[gait-fix] source clip={src.name} len={src.length:F4}s fps={src.frameRate:F0}");

            var dst = new AnimationClip { name = SmoothedClipName, frameRate = src.frameRate };
            // preserve loop settings (the CrouchWalk clip is a LOOP with loopPose on; ConfigureGenericClipFbx/
            // LoopAndRename set loopTime + loopPose on the importer — carry the equivalent onto the .anim).
            var srcSettings = AnimationUtility.GetAnimationClipSettings(src);
            AnimationUtility.SetAnimationClipSettings(dst, srcSettings);

            var bindings = AnimationUtility.GetCurveBindings(src);
            int smoothedCurves = 0, smoothedKeys = 0, copied = 0;
            float worstBefore = 0f, worstAfter = 0f; string worstBoneBefore = "-";

            // Group rotation bindings per bone-path so we can smooth the 4 quaternion components TOGETHER (a
            // quaternion spike must be smoothed as a unit + re-normalized, not per-scalar-component).
            var rotGroups = new Dictionary<string, List<EditorCurveBinding>>();
            var passthrough = new List<EditorCurveBinding>();
            foreach (var b in bindings)
            {
                string pl = b.propertyName.ToLowerInvariant();
                bool isQuatRot = pl == "m_localrotation.x" || pl == "m_localrotation.y" ||
                                 pl == "m_localrotation.z" || pl == "m_localrotation.w";
                string path = b.path.ToLowerInvariant();
                bool footChain = false;
                foreach (var tok in FootChainTokens) if (path.Contains(tok)) { footChain = true; break; }
                if (isQuatRot && footChain)
                {
                    if (!rotGroups.TryGetValue(b.path, out var list)) { list = new List<EditorCurveBinding>(); rotGroups[b.path] = list; }
                    list.Add(b);
                }
                else passthrough.Add(b);
            }

            // 1) copy every non-foot-chain / non-quaternion curve verbatim.
            foreach (var b in passthrough)
            {
                var c = AnimationUtility.GetEditorCurve(src, b);
                if (c != null) { AnimationUtility.SetEditorCurve(dst, b, c); copied++; }
            }

            // 2) smooth each foot-chain quaternion group.
            foreach (var kv in rotGroups)
            {
                string bonePath = kv.Key;
                // resolve the 4 component curves (x,y,z,w) for this bone; require all 4 with matching key times.
                AnimationCurve cx = null, cy = null, cz = null, cw = null;
                EditorCurveBinding bx = default, by = default, bz = default, bw = default;
                foreach (var b in kv.Value)
                {
                    var c = AnimationUtility.GetEditorCurve(src, b);
                    switch (b.propertyName.ToLowerInvariant())
                    {
                        case "m_localrotation.x": cx = c; bx = b; break;
                        case "m_localrotation.y": cy = c; by = b; break;
                        case "m_localrotation.z": cz = c; bz = b; break;
                        case "m_localrotation.w": cw = c; bw = b; break;
                    }
                }
                if (cx == null || cy == null || cz == null || cw == null || cx.length < 3)
                {
                    // can't smooth as a quaternion; copy verbatim.
                    if (cx != null) { AnimationUtility.SetEditorCurve(dst, bx, cx); copied++; }
                    if (cy != null) { AnimationUtility.SetEditorCurve(dst, by, cy); copied++; }
                    if (cz != null) { AnimationUtility.SetEditorCurve(dst, bz, cz); copied++; }
                    if (cw != null) { AnimationUtility.SetEditorCurve(dst, bw, cw); copied++; }
                    continue;
                }

                int n = cx.length;
                // assemble the per-key quaternions (assume aligned key times across x/y/z/w — Mixamo exports them so).
                var quats = new Quaternion[n];
                var times = new float[n];
                for (int i = 0; i < n; i++)
                {
                    times[i] = cx.keys[i].time;
                    quats[i] = new Quaternion(cx.keys[i].value, cy.keys[i].value, cz.keys[i].value, cw.keys[i].value);
                }

                // per-key incoming angular delta (deg) — the spike signature.
                float boneWorstBefore = 0f;
                for (int i = 1; i < n; i++)
                {
                    float d = Quaternion.Angle(quats[i - 1], quats[i]);
                    if (d > boneWorstBefore) boneWorstBefore = d;
                }
                if (boneWorstBefore > worstBefore) { worstBefore = boneWorstBefore; worstBoneBefore = bonePath; }

                // detect the CORRUPTED RUN: a key is "hot" if EITHER its in-delta or out-delta exceeds the spike
                // threshold (a thrash region touches every key with at least one big edge — the LeftToeBase pop is
                // a 5-6-key tumble, NOT a single frame, so a both-edges rule under-catches and leaves thrashing
                // anchors). Then every key STRICTLY BETWEEN the first and last hot key is part of the corrupt run
                // (interior keys with small individual edges are still riding the bad quaternion path).
                bool[] hot = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    float dIn = i > 0 ? Quaternion.Angle(quats[i - 1], quats[i]) : 0f;
                    float dOut = i < n - 1 ? Quaternion.Angle(quats[i], quats[i + 1]) : 0f;
                    if (dIn > SpikeAngleDeg || dOut > SpikeAngleDeg) hot[i] = true;
                }
                // expand hot into contiguous runs, then mark every INTERIOR key of each run's [firstHot..lastHot]
                // span as "resample" — the run's clean anchors are the keys JUST OUTSIDE the span.
                bool[] resample = new bool[n];
                int boneSmoothedKeys = 0;
                int r = 0;
                while (r < n)
                {
                    if (!hot[r]) { r++; continue; }
                    int runLo = r; while (runLo > 0 && hot[runLo - 1]) runLo--; // (r is already the first hot of this run via the scan)
                    int runHi = r; while (runHi < n - 1 && hot[runHi + 1]) runHi++;
                    // clean anchors bracket the run: the key before runLo and the key after runHi (clamped).
                    int lo = Mathf.Max(0, runLo - 1);
                    int hi = Mathf.Min(n - 1, runHi + 1);
                    for (int j = lo + 1; j < hi; j++) resample[j] = true;
                    r = runHi + 1;
                }

                for (int i = 1; i < n - 1; i++)
                {
                    if (!resample[i]) continue;
                    int lo = i - 1; while (lo > 0 && resample[lo]) lo--;
                    int hi = i + 1; while (hi < n - 1 && resample[hi]) hi++;
                    float span = times[hi] - times[lo];
                    float t = span > 1e-6f ? (times[i] - times[lo]) / span : 0.5f;
                    quats[i] = Quaternion.Slerp(quats[lo], quats[hi], t).normalized;
                    boneSmoothedKeys++;
                    smoothedKeys++;
                }

                if (boneSmoothedKeys > 0) smoothedCurves++;

                // recompute the worst per-key delta after smoothing (for the report).
                float boneWorstAfter = 0f;
                for (int i = 1; i < n; i++)
                {
                    float d = Quaternion.Angle(quats[i - 1], quats[i]);
                    if (d > boneWorstAfter) boneWorstAfter = d;
                }
                if (boneSmoothedKeys > 0)
                    sb.AppendLine($"[gait-fix]   {bonePath}: smoothed {boneSmoothedKeys} key(s); worst per-key delta " +
                                  $"{boneWorstBefore:F2}deg -> {boneWorstAfter:F2}deg");
                if (boneWorstAfter > worstAfter) worstAfter = boneWorstAfter;

                // write the (possibly modified) quaternion components back. Auto-tangent ONLY the resampled keys +
                // their bracketing anchors (whose tangents point into a changed span); every other key keeps its
                // ORIGINAL FBX tangent verbatim (#197 NIT — SmoothTangents on ALL keys re-derived the non-spiking
                // keys' tangents sub-degree off-verbatim; scoping to the changed range keeps them byte-verbatim).
                WriteQuatCurves(dst, bx, by, bz, bw, cx, cy, cz, cw, times, quats, resample);
            }

            // 3) write the asset (overwrite if present — idempotent bootstrap re-run).
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(SmoothedClipPath);
            if (existing != null) EditorUtility.CopySerialized(dst, existing);
            else AssetDatabase.CreateAsset(dst, SmoothedClipPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SmoothedClipPath);

            sb.AppendLine($"[gait-fix] copied {copied} curves verbatim; smoothed {smoothedCurves} foot-chain bone group(s), " +
                          $"{smoothedKeys} key(s) total.");
            sb.AppendLine($"[gait-fix] WORST per-key quaternion delta on the foot chain: BEFORE={worstBefore:F2}deg " +
                          $"(@{worstBoneBefore}) -> AFTER={worstAfter:F2}deg. Wrote {SmoothedClipPath}");
            if (smoothedKeys == 0)
                sb.AppendLine("[gait-fix] WARNING: NO spike keys smoothed — the spike threshold may be mis-tuned OR the " +
                              "clip already clean. Verify against the runtime probe before shipping.");
            return true;
        }

        // Write x/y/z/w curves from the (times, quats) arrays. Keys that were NOT resampled keep their ORIGINAL FBX
        // tangents (verbatim copy from the source curves — no re-derivation). Auto (smooth) tangents are applied ONLY
        // to the resampled keys and their bracketing anchors, so the interpolation through the CORRECTED span is clean
        // (C1-continuous, no re-introduced kink) while every non-spiking key stays byte-verbatim (#197 SmoothTangents
        // scope NIT — running SmoothTangents on ALL keys re-derived even the untouched keys' tangents sub-degree off).
        private static void WriteQuatCurves(AnimationClip dst,
            EditorCurveBinding bx, EditorCurveBinding by, EditorCurveBinding bz, EditorCurveBinding bw,
            AnimationCurve srcX, AnimationCurve srcY, AnimationCurve srcZ, AnimationCurve srcW,
            float[] times, Quaternion[] quats, bool[] resample)
        {
            int n = times.Length;
            // start every key from its ORIGINAL FBX keyframe (preserves time + tangents + weights verbatim); overwrite
            // only the VALUE, which is unchanged for non-resampled keys (verbatim) and the slerp result for resampled.
            var kx = new Keyframe[n]; var ky = new Keyframe[n]; var kz = new Keyframe[n]; var kw = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                kx[i] = srcX.keys[i]; kx[i].value = quats[i].x;
                ky[i] = srcY.keys[i]; ky[i].value = quats[i].y;
                kz[i] = srcZ.keys[i]; kz[i].value = quats[i].z;
                kw[i] = srcW.keys[i]; kw[i].value = quats[i].w;
            }
            var ax = new AnimationCurve(kx); var ay = new AnimationCurve(ky);
            var az = new AnimationCurve(kz); var aw = new AnimationCurve(kw);
            // A key needs a re-derived Auto tangent if it was itself resampled OR it is a clean anchor immediately
            // bracketing a resampled run (its tangent points into the changed span). Every other key keeps verbatim.
            for (int i = 0; i < n; i++)
            {
                bool touched = resample[i]
                               || (i > 0 && resample[i - 1])
                               || (i < n - 1 && resample[i + 1]);
                if (!touched) continue;
                ax.SmoothTangents(i, 0f); ay.SmoothTangents(i, 0f);
                az.SmoothTangents(i, 0f); aw.SmoothTangents(i, 0f);
            }
            AnimationUtility.SetEditorCurve(dst, bx, ax);
            AnimationUtility.SetEditorCurve(dst, by, ay);
            AnimationUtility.SetEditorCurve(dst, bz, az);
            AnimationUtility.SetEditorCurve(dst, bw, aw);
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
