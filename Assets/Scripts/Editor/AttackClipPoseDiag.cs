using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// REUSABLE clip-pose measurement instrument (built for 86cav8xg9 — the "contorted pickaxe_mine body" ticket).
    ///
    /// WHY IT EXISTS: a per-bone LOCAL-quaternion deviation number cannot tell a big legitimate swing from a
    /// contorted pose — a hard overhead strike deviates hugely from frame 0 and looks fine. What discriminates
    /// "contorted" is limb GEOMETRY in the character's own torso frame (is a hand flung out sideways? is an elbow
    /// hyper-extended?). So this instrument measures BOTH layers and prints them side-by-side for ALL FIVE
    /// per-class attack clips, so the suspect clip is judged against its four siblings rather than against a
    /// guess. It also measures any generated repaired .anim (before/after in ONE run).
    ///
    /// HEADLESS-SAFE: poses clips via <c>AnimationClip.SampleAnimation</c> on the LIVE rig
    /// (<see cref="CharacterAssetGen.FbxPath"/> — v4) — never an Animator tick (headless
    /// <c>Time.deltaTime≈0</c> means the Animator never advances; the walk-float saga lesson,
    /// unity-conventions.md §FBX/rigs Bug B / procedural-animation-verbs.md).
    ///
    /// Run:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.AttackClipPoseDiag.Run
    ///
    /// Read-only: instantiates a throwaway rig, destroys it, touches no importer and no asset.
    /// </summary>
    public static class AttackClipPoseDiag
    {
        private const int Samples = 21;          // 0..1 inclusive in 5% steps
        private const string BonePrefix = "mixamorig:";

        [MenuItem("FarHorizon/Diagnose/Attack Clip Poses (per-class arm geometry)")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[clip-diag] ===== ATTACK CLIP POSE GEOMETRY (86cav8xg9) =====");

            // Optional: regenerate the repaired pickaxe .anim first, so ONE headless pass shows before AND after
            // (the single Unity build slot makes every extra invocation expensive).
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a == "-regenPickaxeFix")
                {
                    var fixSb = new StringBuilder();
                    PickaxeMineCurveFix.Generate(fixSb);
                    sb.Append(fixSb);
                    break;
                }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            if (fbx == null)
            {
                sb.AppendLine("[clip-diag] ERROR: live rig FBX missing @ " + CharacterAssetGen.FbxPath);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            sb.AppendLine("[clip-diag] rig=" + CharacterAssetGen.FbxPath);

            var playerRoot = new GameObject("__clipDiagPlayer");
            var avatarRoot = new GameObject("__clipDiagAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * 1.8f;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            Transform hips = Get(bones, "Hips"), head = Get(bones, "Head");
            Transform lArm = Get(bones, "LeftArm"), lFore = Get(bones, "LeftForeArm"), lHand = Get(bones, "LeftHand");
            Transform rArm = Get(bones, "RightArm"), rFore = Get(bones, "RightForeArm"), rHand = Get(bones, "RightHand");
            if (hips == null || head == null || lArm == null || lFore == null || lHand == null ||
                rArm == null || rFore == null || rHand == null)
            {
                sb.AppendLine("[clip-diag] ERROR: bone lookup failed. Bones present:");
                foreach (var k in bones.Keys) if (k.StartsWith(BonePrefix)) sb.AppendLine("[clip-diag]   " + k);
                Object.DestroyImmediate(playerRoot);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var targets = new List<(string label, AnimationClip clip)>
            {
                ("axe",     FindFbxClip(CharacterAssetGen.AttackAxeFbxPath,     CharacterAssetGen.AxeSwingClip)),
                ("pickaxe", FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip)),
                ("dagger",  FindFbxClip(CharacterAssetGen.AttackDaggerFbxPath,  CharacterAssetGen.DaggerStabClip)),
                ("spear",   FindFbxClip(CharacterAssetGen.AttackSpearFbxPath,   CharacterAssetGen.SpearThrustClip)),
                ("sword",   FindFbxClip(CharacterAssetGen.AttackSwordFbxPath,   CharacterAssetGen.SwordSlashClip)),
            };
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(PickaxeMineCurveFix.RepairedClipPath);
            if (repaired != null) targets.Add(("pickaxe_REPAIRED", repaired));

            sb.AppendLine("[clip-diag] --- LEGEND (all lengths normalised by SHOULDER WIDTH = scale-immune) ---");
            sb.AppendLine("[clip-diag]   outL/outR = hand displacement OUTWARD from the body midline (+ = away " +
                          "sideways). A flung-out arm is a HIGH outward peak.");
            sb.AppendLine("[clip-diag]   fwd = hand forward of the chest plane; up = hand above the hip plane.");
            sb.AppendLine("[clip-diag]   elbowMin = tightest elbow INTERIOR angle (180 = straight arm).");
            sb.AppendLine("[clip-diag]   devL = LeftArm LOCAL-quat max deviation from the clip's own frame-0 pose; " +
                          "sustN = how many of 21 samples exceed 45deg (1 = a SPIKE, many = a SUSTAINED pose).");
            sb.AppendLine("[clip-diag]   tilt = torso lean off VERTICAL (hips->head vs world up), deg. An upright " +
                          "strike stays low; a ground/kneeling mine bends far over.");
            sb.AppendLine("[clip-diag]   drop = hips LOWERED below the clip's own frame-0 hip height (shoulder-" +
                          "widths, + = squatted DOWN). twist = shoulder-line vs hip-line yaw, deg.");

            foreach (var (label, clip) in targets)
            {
                if (clip == null) { sb.AppendLine($"[clip-diag] {label,-18} MISSING"); continue; }
                Measure(sb, label, clip, model, hips, head, lArm, lFore, lHand, rArm, rFore, rHand);
            }

            // PER-SAMPLE TRAJECTORY for the suspect clip + the clean reference — a max alone cannot show WHERE in
            // the swing the pose goes wrong, and a percept defect is about the shape over time.
            foreach (var (label, clip) in targets)
            {
                if (clip == null) continue;
                // Foot plant for EVERY clip — the accepted siblings are the reference band for what a normal mine/
                // strike swing does to the feet; without them a repaired-vs-raw delta has no scale to be judged on.
                FootPlant(sb, label, clip, model, bones);
                if (label != "pickaxe" && label != "axe" && label != "pickaxe_REPAIRED") continue;
                Trajectory(sb, label, clip, model, hips, head, lArm, lFore, lHand, rArm, rFore, rHand);
                FineWindow(sb, label, clip, model, hips, head, lArm, lHand, rArm, rFore, rHand);
                FoldOwnership(sb, label, clip, model, hips, head);
            }

            Object.DestroyImmediate(playerRoot);
            sb.AppendLine("[clip-diag] ===== END =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Measure(StringBuilder sb, string label, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform lFore, Transform lHand,
            Transform rArm, Transform rFore, Transform rHand)
        {
            float outLMax = float.MinValue, outRMax = float.MinValue;
            float outLAtT = 0f, fwdLAtPeak = 0f, upLAtPeak = 0f;
            float elbowLMin = 999f, elbowRMin = 999f;
            float devL = 0f; int sustN = 0;
            float tiltMax = 0f, tiltAtT = 0f, dropMax = 0f, twistMax = 0f;
            float hipY0 = 0f, shoulderWidth0 = 1f;
            Quaternion lArmAnchor = Quaternion.identity;

            for (int i = 0; i < Samples; i++)
            {
                float nt = i / (float)(Samples - 1);
                clip.SampleAnimation(model, nt * clip.length);

                // Torso frame from GEOMETRY (an imported rig's bone local axes are arbitrary — never assume them;
                // procedural-animation-verbs.md "measure bone axes FIRST"). up = hips->head, right = L->R shoulder.
                Vector3 up = (head.position - hips.position);
                Vector3 shoulder = (rArm.position - lArm.position);
                float shoulderWidth = shoulder.magnitude;
                if (shoulderWidth < 1e-5f || up.magnitude < 1e-5f) continue;
                Vector3 rightAxis = shoulder / shoulderWidth;
                up.Normalize();
                Vector3 fwdAxis = Vector3.Cross(up, rightAxis).normalized;   // handedness is consistent per-rig
                Vector3 chest = (lArm.position + rArm.position) * 0.5f;

                // LEFT hand: outward = AWAY from midline on the left side = the NEGATIVE right-axis direction.
                Vector3 dl = (lHand.position - chest) / shoulderWidth;
                float outL = -Vector3.Dot(dl, rightAxis);
                float fwdL = Vector3.Dot(dl, fwdAxis);
                float upL = Vector3.Dot(dl, up);
                if (outL > outLMax) { outLMax = outL; outLAtT = nt; fwdLAtPeak = fwdL; upLAtPeak = upL; }

                Vector3 dr = (rHand.position - chest) / shoulderWidth;
                float outR = Vector3.Dot(dr, rightAxis);
                if (outR > outRMax) outRMax = outR;

                elbowLMin = Mathf.Min(elbowLMin, Vector3.Angle(lArm.position - lFore.position, lHand.position - lFore.position));
                elbowRMin = Mathf.Min(elbowRMin, Vector3.Angle(rArm.position - rFore.position, rHand.position - rFore.position));

                // TORSO read — the "contorted body" percept lives here, not in a hand position.
                float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                if (tilt > tiltMax) { tiltMax = tilt; tiltAtT = nt; }
                Vector3 hipLine = Vector3.ProjectOnPlane(HipLine(hips), Vector3.up);
                Vector3 shLine = Vector3.ProjectOnPlane(shoulder, Vector3.up);
                if (hipLine.sqrMagnitude > 1e-8f && shLine.sqrMagnitude > 1e-8f)
                    twistMax = Mathf.Max(twistMax, Vector3.Angle(hipLine, shLine));

                if (i == 0) { lArmAnchor = lArm.localRotation; hipY0 = hips.position.y; shoulderWidth0 = shoulderWidth; }
                else
                {
                    float d = Quaternion.Angle(lArm.localRotation, lArmAnchor);
                    if (d > devL) devL = d;
                    if (d > 45f) sustN++;
                    dropMax = Mathf.Max(dropMax, (hipY0 - hips.position.y) / Mathf.Max(1e-5f, shoulderWidth0));
                }
            }

            sb.AppendLine($"[clip-diag] {label,-18} len={clip.length:F2}s  outL={outLMax:F2}@t{outLAtT:F2} " +
                          $"(fwd={fwdLAtPeak:F2} up={upLAtPeak:F2})  outR={outRMax:F2}  " +
                          $"elbowMin L={elbowLMin:F0} R={elbowRMin:F0}  devL={devL:F0}deg sustN={sustN}/20  " +
                          $"tilt={tiltMax:F0}deg@t{tiltAtT:F2} drop={dropMax:F2} twist={twistMax:F0}deg");
        }

        // Per-sample dump: torso tilt / hip drop / hand geometry across the whole clip.
        private static void Trajectory(StringBuilder sb, string label, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform lFore, Transform lHand,
            Transform rArm, Transform rFore, Transform rHand)
        {
            sb.AppendLine($"[clip-diag] --- TRAJECTORY {label} (t, tilt, hipDropSW, outL, outR, upR, elbowR) ---");
            float hipY0 = 0f, sw0 = 1f;
            for (int i = 0; i < Samples; i++)
            {
                float nt = i / (float)(Samples - 1);
                clip.SampleAnimation(model, nt * clip.length);
                Vector3 shoulder = rArm.position - lArm.position;
                float sw = shoulder.magnitude;
                if (sw < 1e-5f) continue;
                if (i == 0) { hipY0 = hips.position.y; sw0 = sw; }
                Vector3 rightAxis = shoulder / sw;
                Vector3 up = (head.position - hips.position).normalized;
                Vector3 chest = (lArm.position + rArm.position) * 0.5f;
                float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                float drop = (hipY0 - hips.position.y) / sw0;
                float outL = -Vector3.Dot((lHand.position - chest) / sw, rightAxis);
                Vector3 dr = (rHand.position - chest) / sw;
                float outR = Vector3.Dot(dr, rightAxis);
                float upR = Vector3.Dot(dr, up);
                float elbowR = Vector3.Angle(rArm.position - rFore.position, rHand.position - rFore.position);
                sb.AppendLine($"[clip-diag]   t={nt:F2} tilt={tilt,5:F1} drop={drop,6:F2} outL={outL,6:F2} " +
                              $"outR={outR,6:F2} upR={upR,6:F2} elbowR={elbowR,5:F0}");
            }
        }

        /// <summary>
        /// FINE window around the mid-clip transition, at the clip's own AUTHORED frame step, reporting the
        /// per-step MAX whole-skeleton bone-local-rotation delta. This is the discriminator the #197 sneak-gait
        /// fix turned on: a genuine keyframe POP shows one step with a delta far above its neighbours; a fast but
        /// legitimate motion shows a smooth ramp. Judging "contorted" without this cannot tell the two apart.
        /// </summary>
        private static void FineWindow(StringBuilder sb, string label, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform lHand,
            Transform rArm, Transform rFore, Transform rHand)
        {
            var bones = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(BonePrefix)) bones.Add(t);

            float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
            float step = 1f / fps / clip.length;           // one authored frame in normalised time
            sb.AppendLine($"[clip-diag] --- FINE {label} t=0.40..0.75 @1 authored frame ({fps:F0}fps, " +
                          $"dn={step:F4}) : tilt, outL, outR, upR, maxBoneStepDelta(deg)@bone ---");

            var prev = new Quaternion[bones.Count];
            bool havePrev = false;
            for (float nt = 0.40f; nt <= 0.7501f; nt += step)
            {
                clip.SampleAnimation(model, nt * clip.length);
                Vector3 shoulder = rArm.position - lArm.position;
                float sw = shoulder.magnitude;
                if (sw < 1e-5f) continue;
                Vector3 rightAxis = shoulder / sw;
                Vector3 up = (head.position - hips.position).normalized;
                Vector3 chest = (lArm.position + rArm.position) * 0.5f;
                float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                float outL = -Vector3.Dot((lHand.position - chest) / sw, rightAxis);
                Vector3 dr = (rHand.position - chest) / sw;
                float outR = Vector3.Dot(dr, rightAxis);
                float upR = Vector3.Dot(dr, up);

                float worst = 0f; string worstBone = "-";
                for (int b = 0; b < bones.Count; b++)
                {
                    var q = bones[b].localRotation;
                    if (havePrev)
                    {
                        float d = Quaternion.Angle(q, prev[b]);
                        if (d > worst) { worst = d; worstBone = bones[b].name.Replace(BonePrefix, ""); }
                    }
                    prev[b] = q;
                }
                havePrev = true;
                sb.AppendLine($"[clip-diag]   t={nt:F3} tilt={tilt,5:F1} outL={outL,6:F2} outR={outR,6:F2} " +
                              $"upR={upR,6:F2} maxStep={worst,6:F1}@{worstBone}");
            }
        }

        /// <summary>
        /// WHO OWNS THE FOLD. At the clip's peak torso-tilt frame, resets ONE bone at a time back to its frame-0
        /// local rotation and re-measures the tilt — so the per-bone CONTRIBUTION to the fold is measured, never
        /// guessed. This is the bone-axis-measurement discipline (procedural-animation-verbs.md) applied to a
        /// whole-chain pose: an imported rig's local axes are arbitrary, so "the spine must be the Spine bones"
        /// is a guess until the reset-and-remeasure says so.
        /// </summary>
        private static void FoldOwnership(StringBuilder sb, string label, AnimationClip clip, GameObject model,
            Transform hips, Transform head)
        {
            var bones = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(BonePrefix)) bones.Add(t);

            // frame-0 anchor pose.
            clip.SampleAnimation(model, 0f);
            var anchor = new Quaternion[bones.Count];
            for (int b = 0; b < bones.Count; b++) anchor[b] = bones[b].localRotation;

            // locate the peak-tilt time on the authored frame grid.
            float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
            float step = 1f / fps / clip.length;
            float peakT = 0f, peakTilt = -1f;
            for (float nt = 0f; nt <= 1.0001f; nt += step)
            {
                clip.SampleAnimation(model, nt * clip.length);
                float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                if (tilt > peakTilt) { peakTilt = tilt; peakT = nt; }
            }

            clip.SampleAnimation(model, peakT * clip.length);
            sb.AppendLine($"[clip-diag] --- FOLD OWNERSHIP {label} : peakTilt={peakTilt:F1}deg @t={peakT:F3} " +
                          "(per bone: tilt if THIS bone alone reverts to frame-0) ---");
            var rows = new List<(string bone, float tiltIfReset, float devDeg)>();
            for (int b = 0; b < bones.Count; b++)
            {
                var live = bones[b].localRotation;
                float dev = Quaternion.Angle(live, anchor[b]);
                if (dev < 1f) continue;                      // this bone barely moves — not a fold owner
                bones[b].localRotation = anchor[b];
                float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                bones[b].localRotation = live;
                rows.Add((bones[b].name.Replace(BonePrefix, ""), tilt, dev));
            }
            rows.Sort((x, y) => x.tiltIfReset.CompareTo(y.tiltIfReset));   // biggest tilt REDUCTION first
            int shown = 0;
            foreach (var r in rows)
            {
                if (shown++ >= 10) break;
                sb.AppendLine($"[clip-diag]   {r.bone,-16} tiltIfReset={r.tiltIfReset,5:F1} " +
                              $"(reduces {peakTilt - r.tiltIfReset,5:F1}deg)  ownLocalDev={r.devDeg,5:F1}deg");
            }
        }

        /// <summary>
        /// FOOT PLANT. Un-hinging a pelvis swings the legs with it unless they are compensated, which would trade
        /// a contorted torso for FLOATING/SLIDING FEET — the walk-float saga's exact failure mode
        /// (unity-conventions.md §FBX/rigs). So the repaired clip must be judged on the feet too, not only the
        /// torso: this reports each foot's world-Y band and horizontal travel over the whole clip, so a
        /// before/after diff shows whether the compensation held.
        /// </summary>
        private static void FootPlant(StringBuilder sb, string label, AnimationClip clip, GameObject model,
            Dictionary<string, Transform> boneMap)
        {
            Transform lFoot = Get(boneMap, "LeftFoot"), rFoot = Get(boneMap, "RightFoot");
            if (lFoot == null || rFoot == null) { sb.AppendLine($"[clip-diag] FOOT {label}: foot bones not found"); return; }

            float lMin = float.MaxValue, lMax = float.MinValue, rMin = float.MaxValue, rMax = float.MinValue;
            Vector3 lFirst = Vector3.zero, rFirst = Vector3.zero;
            float lTravel = 0f, rTravel = 0f, lY0 = 0f, rY0 = 0f;
            // Sample on the clip's own authored frame grid — a coarse 21-point sweep can step OVER a brief dip, and
            // a foot dipping below the ground plane is exactly the kind of defect that must not be sampled past.
            float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
            int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * fps));
            for (int f = 0; f <= frames; f++)
            {
                clip.SampleAnimation(model, Mathf.Min(clip.length, f / fps));
                lMin = Mathf.Min(lMin, lFoot.position.y); lMax = Mathf.Max(lMax, lFoot.position.y);
                rMin = Mathf.Min(rMin, rFoot.position.y); rMax = Mathf.Max(rMax, rFoot.position.y);
                if (f == 0) { lFirst = lFoot.position; rFirst = rFoot.position; lY0 = lFirst.y; rY0 = rFirst.y; }
                lTravel = Mathf.Max(lTravel, Vector3.ProjectOnPlane(lFoot.position - lFirst, Vector3.up).magnitude);
                rTravel = Mathf.Max(rTravel, Vector3.ProjectOnPlane(rFoot.position - rFirst, Vector3.up).magnitude);
            }
            // dipBelowStart is the load-bearing number: a foot BELOW the height it started at is a foot inside the
            // ground. The absolute band alone cannot distinguish "lifted a heel" from "sank through the terrain".
            sb.AppendLine($"[clip-diag] FOOT {label,-18} L y {lMin:F4}..{lMax:F4} band {lMax - lMin:F4} " +
                          $"dipBelowStart {Mathf.Max(0f, lY0 - lMin):F4} travel {lTravel:F4} | " +
                          $"R y {rMin:F4}..{rMax:F4} band {rMax - rMin:F4} " +
                          $"dipBelowStart {Mathf.Max(0f, rY0 - rMin):F4} travel {rTravel:F4}  (metres)");
        }

        // The hip line (left->right upper-leg) — the pelvis orientation reference for the spine-twist read.
        private static Vector3 HipLine(Transform hips)
        {
            Transform l = null, r = null;
            foreach (Transform c in hips)
            {
                if (c.name.Contains("LeftUpLeg")) l = c;
                else if (c.name.Contains("RightUpLeg")) r = c;
            }
            if (l != null && r != null) return r.position - l.position;
            return hips.right;   // fallback: the pelvis bone's own local right (arbitrary axis, but consistent)
        }

        private static Transform Get(Dictionary<string, Transform> bones, string leaf)
        {
            if (bones.TryGetValue(BonePrefix + leaf, out var t)) return t;
            bones.TryGetValue(leaf, out t);
            return t;
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
