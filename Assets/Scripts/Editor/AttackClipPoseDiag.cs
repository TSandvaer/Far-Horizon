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
            // 86cay4282 round 4 — the PALM proxy for the left-arm IK: the KNUCKLE the palm centre is measured against.
            // Resolved from a CANDIDATE LIST, not from one assumed name: the v4 hero is a fist-hand variant whose rig
            // carries only 3 finger + 3 thumb bones (bootstrap: "fist-hand-variant index+thumb only"), so
            // `LeftHandMiddle1` — the obvious palm proxy on a full Mixamo hand — does NOT exist here. Every candidate
            // tried and the winner are both PRINTED, so the palm anchor is evidenced rather than asserted.
            Transform lKnuckle = null; string lKnuckleName = "<none>";
            foreach (string cand in new[] { "LeftHandMiddle1", "LeftHandIndex1", "LeftHandRing1", "LeftHandPinky1" })
            {
                lKnuckle = Get(bones, cand);
                if (lKnuckle != null) { lKnuckleName = cand; break; }
            }
            var handBones = new List<string>();
            foreach (var k in bones.Keys) if (k.Contains("Hand")) handBones.Add(k);
            handBones.Sort();
            sb.AppendLine("[clip-diag] HAND-SUBTREE bones on this rig (" + handBones.Count + "): " +
                          string.Join(", ", handBones));
            sb.AppendLine("[clip-diag] palm-proxy knuckle resolved = " + lKnuckleName +
                          " (86cay4282 r4 — the palm centre is midpoint(wrist, this bone))");
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

            // 86cay4282 — WHAT ACTUALLY SHIPS for the arm-pose offset. The suspect in the ticket is the
            // CastawayArmPose.rightArmEuler FIELD DEFAULT, but MovementCameraScene.AddArmPose OVERWRITES it on the
            // live hero; dump both so the |Q| in play is read, never assumed.
            ShippedArmPose(sb);

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

            // 86cay4282 — PROP-IN-HAND SEAT pass. The SHIPPED pickaxe clip is the repaired .anim when it exists
            // (CharacterAssetGen.BuildAnimatorController binds it that way); the axe swing is the class CONTROL.
            AnimationClip shippedPickaxe = repaired ?? targets.Find(t => t.label == "pickaxe").clip;
            AnimationClip shippedAxe = targets.Find(t => t.label == "axe").clip;
            PropSeatSection(sb, model, hips, head, lArm, rArm, lHand, rHand, rFore,
                            lFore, lKnuckle, shippedPickaxe, shippedAxe);

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

        // ==================================================================================================
        // 86cay4282 — PROP-IN-HAND SEAT GEOMETRY (the Sponsor's "swinging with both hands" + "the axe is still
        // pivoting and not sitting right during the swing", soak `soak-pickaxe-1`, stamp 1194927).
        //
        // WHY THIS PASS EXISTS: the clip-geometry passes above measure the BODY. Neither of the reported defects
        // is a body defect — one is a relationship between the two HANDS, the other a relationship between the
        // TOOL and the hand. Judging either from a body number (or from a per-bone quaternion) is measuring the
        // wrong layer, the documented trap on this surface. So this pass reconstructs the FULL SHIPPED held-prop
        // chain on the live rig and measures both relationships as GEOMETRY in the character's own frame:
        //
        //   mixamorig:RightHand
        //     └─ HeroAxe            localScale = MovementCameraScene.HeldAxeLocalScaleUniform
        //        │                  world pos/rot RE-DRIVEN every frame by HeldToolRig.LateUpdate (order 100):
        //        │                    position = hand.position + hand.rotation * seatOffsetFromHand
        //        │                    rotation = hand.rotation * Euler(seatEuler)
        //        └─ WeaponMeshHolder localPos = (0, HeldAxeGripShiftY, 0) + WeaponMeshLocalOffset[idx]
        //                            localRot = Euler(WeaponMeshLocalEuler[idx])
        //                            localScale = WeaponMeshScale[idx]
        //
        // Every constant is READ from the shipping source (MovementCameraScene / HeldWeaponCycleDebug /
        // CharacterAssetGen toggles), never copied — so a re-bake cannot silently drift this instrument.
        // ==================================================================================================

        /// <summary>Dump the arm-pose offset THAT ACTUALLY SHIPS. The ticket's prime suspect for the pivot is
        /// <c>CastawayArmPose.rightArmEuler</c>'s FIELD DEFAULT — but <c>MovementCameraScene.AddArmPose</c>
        /// overwrites it on the live hero, and CI re-runs bootstrap before every build, so the field default is
        /// only the ROLLBACK value. Print both plus each one's |Q| against the ~25/~40deg blast-radius bands.</summary>
        private static void ShippedArmPose(StringBuilder sb)
        {
            var probe = new GameObject("__armPoseProbe");
            var comp = probe.AddComponent<CastawayArmPose>();
            Vector3 fieldDefaultR = comp.rightArmEuler, fieldDefaultL = comp.leftArmEuler;
            Object.DestroyImmediate(probe);

            bool v4 = CharacterAssetGen.UseCastawayV4;
            Vector3 shippedR = v4 ? MovementCameraScene.CastawayV4RightArmEuler : fieldDefaultR;
            Vector3 shippedL = v4 ? MovementCameraScene.CastawayV4LeftArmEuler : fieldDefaultL;

            sb.AppendLine("[armpose] ===== WHAT SHIPS FOR CastawayArmPose (86cay4282) =====");
            sb.AppendLine($"[armpose] CharacterAssetGen.UseCastawayV4 = {v4} " +
                          $"(UseCastawayV4Default={CharacterAssetGen.UseCastawayV4Default}) -> rig {CharacterAssetGen.FbxPath}");
            sb.AppendLine($"[armpose] field default   rightArmEuler={fieldDefaultR:F1} |Q|={QMag(fieldDefaultR):F1}deg " +
                          $"| leftArmEuler={fieldDefaultL:F1} |Q|={QMag(fieldDefaultL):F1}deg   (the ROLLBACK path)");
            sb.AppendLine($"[armpose] SHIPPED (bake)  rightArmEuler={shippedR:F1} |Q|={QMag(shippedR):F1}deg " +
                          $"| leftArmEuler={shippedL:F1} |Q|={QMag(shippedL):F1}deg   " +
                          "(MovementCameraScene.AddArmPose, applied when UseCastawayV4)");
            sb.AppendLine("[armpose] |Q| bands (procedural-animation-verbs.md, 86caxgwbz): <~25deg clip-safe by " +
                          "construction; >~40deg needs a state gate.");
            sb.AppendLine($"[armpose] runLowerEuler={MovementCameraScene.ArmRunLowerEuler:F1} " +
                          $"|Q|={QMag(MovementCameraScene.ArmRunLowerEuler):F1}deg — GATED off the locomotion lane " +
                          "since 884c611, so its weight is 0 through an attack swing (not in play here).");
            sb.AppendLine("[armpose] ===== END =====");
        }

        private static float QMag(Vector3 euler)
        {
            Quaternion.Euler(euler).ToAngleAxis(out float a, out _);
            return a > 180f ? 360f - a : a;
        }

        private struct PropRig
        {
            public Transform root;        // the HeroAxe-equivalent (the transform HeldToolRig re-drives every frame)
            public Transform holder;      // the WeaponMeshHolder child carrying the per-class dial
            public Vector3 gripLocal;     // grip end of the mesh, in the holder's local mesh space
            public Vector3 headLocal;     // working end (head/point) of the mesh, same space
            // 86cay4282 round 3 — the mesh + its resolved long axis, kept so the HAFT PROFILE pass can ask WHERE
            // along that axis the bare haft ends and the head geometry begins. Round 2 knew the haft's LENGTH but
            // nothing about its SHAPE, so "slide the grip up the haft" had no measured ceiling and the fit was free
            // to park a hand inside the pick head.
            public Mesh mesh;
            public int axis;              // 0/1/2 = the mesh-local component the long axis runs along
            public bool loIsGrip;         // true when the LOW end of that component is the grip (u=0) end
            public string note;
        }

        /// <summary>
        /// Reconstruct the shipped held-prop chain under the hand bone. Returns false (with a logged reason) if
        /// any shipping asset is missing — never a silent half-rig, which would measure a fiction.
        /// </summary>
        private static bool BuildPropRig(StringBuilder sb, string fbxPath, int idx, Transform hand, out PropRig rig)
        {
            rig = default;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) { sb.AppendLine($"[prop-diag] ERROR: weapon FBX missing @ {fbxPath}"); return false; }

            var go = Object.Instantiate(fbx);
            go.name = "__diagProp";
            go.transform.SetParent(hand, false);
            go.transform.localScale = Vector3.one * MovementCameraScene.HeldAxeLocalScaleUniform;

            var mf = go.GetComponent<MeshFilter>() ?? go.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null)
            { sb.AppendLine($"[prop-diag] ERROR: no mesh on {fbxPath}"); Object.DestroyImmediate(go); return false; }
            Mesh mesh = mf.sharedMesh;

            // Re-home onto a holder child exactly like MovementCameraScene.EnsureWeaponMeshHolder + the per-class
            // dial HeldWeaponCycleDebug.ApplyCurrent composes on it.
            var holder = new GameObject("WeaponMeshHolder").transform;
            holder.SetParent(go.transform, false);
            holder.localPosition = new Vector3(0f, MovementCameraScene.HeldAxeGripShiftY, 0f)
                                 + HeldWeaponCycleDebug.WeaponMeshLocalOffset[idx];
            holder.localRotation = Quaternion.Euler(HeldWeaponCycleDebug.WeaponMeshLocalEuler[idx]);
            holder.localScale = Vector3.one * HeldWeaponCycleDebug.WeaponMeshScale[idx];

            // LONG AXIS FROM THE MESH, not from an assumed convention (the bakeAxisConversion trap — the axe's
            // long axis imports as Unity +Y, not the Blender +Z it was authored on). Take the widest bounds axis,
            // then call the endpoint NEARER the mesh origin the GRIP (blender-asset-pipeline §6: grip origin is
            // (0,0,0)) — and print BOTH endpoint distances so that choice is evidenced rather than asserted.
            Bounds b = mesh.bounds;
            int ax = 0;
            if (b.size.y > b.size[ax]) ax = 1;
            if (b.size.z > b.size[ax]) ax = 2;
            Vector3 lo = b.center, hi = b.center;
            lo[ax] = b.min[ax]; hi[ax] = b.max[ax];
            bool loIsGrip = lo.magnitude <= hi.magnitude;
            rig.gripLocal = loIsGrip ? lo : hi;
            rig.headLocal = loIsGrip ? hi : lo;
            rig.root = go.transform;
            rig.holder = holder;
            rig.mesh = mesh;
            rig.axis = ax;
            rig.loIsGrip = loIsGrip;
            rig.note = $"mesh={mesh.name} longAxis={"XYZ"[ax]} len={b.size[ax]:F3} " +
                       $"endA={lo:F3}(|{lo.magnitude:F3}|) endB={hi:F3}(|{hi.magnitude:F3}|) -> grip=end" +
                       (loIsGrip ? "A" : "B");
            return true;
        }

        private static void PropSeatSection(StringBuilder sb, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm,
            Transform lHand, Transform rHand, Transform rFore,
            Transform lFore, Transform lMid1,
            AnimationClip pickaxeClip, AnimationClip axeClip)
        {
            sb.AppendLine("[prop-diag] ===== PROP-IN-HAND SEAT GEOMETRY (86cay4282) =====");
            sb.AppendLine("[prop-diag] LEGEND (lengths in SHOULDER WIDTHS = scale-immune):");
            sb.AppendLine("[prop-diag]   gripHand  = grip point to the RightHand bone. RIGIDITY CONTROL — the seat is");
            sb.AppendLine("[prop-diag]               hand-local + rigid, so a CONSTANT here REFUTES 'the tool pivots");
            sb.AppendLine("[prop-diag]               relative to the hand' at the seating layer.");
            sb.AppendLine("[prop-diag]   axisSpread= angular spread of the tool's long axis measured IN THE HAND's own");
            sb.AppendLine("[prop-diag]               frame over the clip. Same control, rotation channel: 0 = rigid.");
            sb.AppendLine("[prop-diag]   toolFore  = angle between the tool's long axis and the FOREARM (elbow->wrist).");
            sb.AppendLine("[prop-diag]               Its RANGE over the swing is the wrist-driven excursion — the");
            sb.AppendLine("[prop-diag]               'pivoting' percept lives here if the seat layer is rigid.");
            sb.AppendLine("[prop-diag]   headOut/Fwd/Up = the tool HEAD in the character's own torso frame.");
            sb.AppendLine("[prop-diag]   lHaft     = LEFT hand's distance to the tool's haft LINE (u = where along it,");
            sb.AppendLine("[prop-diag]               0=grip 1=head). SMALL lHaft = the left hand is ON the haft (the");
            sb.AppendLine("[prop-diag]               clip reads two-handed); LARGE = the left hand is nowhere near it.");
            sb.AppendLine("[prop-diag]   lRHand    = left-hand to right-hand distance (the 'both hands together' read).");
            sb.AppendLine("[prop-diag]   toolVsHandLine = angle between the tool's long axis and the LINE THROUGH BOTH");
            sb.AppendLine("[prop-diag]               HANDS. On a two-handed clip that line IS the haft the animation");
            sb.AppendLine("[prop-diag]               implies, so this angle is how far the real tool disagrees with the");
            sb.AppendLine("[prop-diag]               grip the viewer reads. 0 = the tool lies along the implied haft.");
            sb.AppendLine("[prop-diag]   NOTE gripHand is normalised by shoulder width, which itself breathes with the");
            sb.AppendLine("[prop-diag]        clip — read axisSpreadInHand (an ANGLE, unnormalised) for rigidity.");

            // The CARRY reference. 'Not sitting right DURING THE SWING' is a contrast against the pose the Sponsor
            // approved at rest, so the swings must be judged against that baseline, not against zero.
            AnimationClip idle = FindFbxClip(CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip);
            AnimationClip walk = FindFbxClip(CharacterAssetGen.WalkFbxPath, CharacterAssetGen.WalkClip);

            var work = new (string label, AnimationClip clip, string fbx, int idx)[]
            {
                ("idle_CARRYREF", idle, WeaponPackAssetGen.PickaxeStoneFbxPath, HeldWeaponCycleDebug.PickaxeStoneFamilyIndex),
                ("walk_CARRYREF", walk, WeaponPackAssetGen.PickaxeStoneFbxPath, HeldWeaponCycleDebug.PickaxeStoneFamilyIndex),
                ("pickaxe", pickaxeClip, WeaponPackAssetGen.PickaxeStoneFbxPath, HeldWeaponCycleDebug.PickaxeStoneFamilyIndex),
                ("axe",     axeClip,     WeaponPackAssetGen.AxeFbxPath,          HeldWeaponCycleDebug.AxeFamilyIndex),
            };

            // The seat the live hero actually rides (highest-version-first, same ternary MovementCameraScene bakes).
            Vector3 seatOffset = CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.HeldAxeV4LocalOffsetFromHand
                               : CharacterAssetGen.UseCastawayV3 ? MovementCameraScene.HeldAxeV3LocalOffsetFromHand
                               : CharacterAssetGen.UseCastawayV2 ? MovementCameraScene.HeldAxeV2LocalOffsetFromHand
                               : MovementCameraScene.HeldAxeLocalOffsetFromHand;
            Vector3 seatEuler = CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.HeldAxeV4RelEuler
                              : CharacterAssetGen.UseCastawayV3 ? MovementCameraScene.HeldAxeV3RelEuler
                              : CharacterAssetGen.UseCastawayV2 ? MovementCameraScene.HeldAxeV2RelEuler
                              : MovementCameraScene.HeldAxeRelEuler;
            sb.AppendLine($"[prop-diag] shipped seat: offsetFromHand={seatOffset:F4} relEuler={seatEuler:F1} " +
                          $"heroScale={MovementCameraScene.HeldAxeLocalScaleUniform:F3} " +
                          $"gripShiftY={MovementCameraScene.HeldAxeGripShiftY:F4}");

            Vector3 armR = CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.CastawayV4RightArmEuler : Vector3.zero;
            Vector3 armL = CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.CastawayV4LeftArmEuler : Vector3.zero;
            // ⚠ THE WRIST OFFSETS ARE LOAD-BEARING FOR A HELD PROP AND WERE MISSING FROM THIS INSTRUMENT.
            // CastawayHandPose (DefaultExecutionOrder 65) right-multiplies these onto the HAND bones — i.e. AFTER
            // CastawayArmPose (50) and BEFORE HeldToolRig (100), which seats the prop off `hand.rotation`. The v4
            // right wrist ships (-22, 250, -30), so omitting it measures the prop against a right-hand orientation
            // that differs from the live one by a quarter-turn. That omission made this pass (and the PlayMode
            // fixture, which shared it) predict a 0.611 SW two-hand fit that the SHIPPED exe measured at 1.220 SW —
            // both instruments were blind the same way, and only the shipped-build gate caught it. The commonly
            // quoted chain "Animator -> CastawayArmPose (50) -> HeldAxeRig (100)" is INCOMPLETE for anything that
            // reads a hand transform.
            Vector3 wristR = CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.CastawayV4RightWristEuler : Vector3.zero;
            Vector3 wristL = CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.CastawayV4LeftWristEuler : Vector3.zero;
            sb.AppendLine($"[prop-diag] shipped wrist (order 65, CastawayHandPose): right={wristR:F1} left={wristL:F1} " +
                          "— composed onto the HAND bones BEFORE the seat reads hand.rotation.");

            foreach (var (label, clip, fbx, idx) in work)
            {
                if (clip == null) { sb.AppendLine($"[prop-diag] {label}: clip MISSING — skipped"); continue; }
                if (!BuildPropRig(sb, fbx, idx, rHand, out PropRig prop)) continue;
                sb.AppendLine($"[prop-diag] --- {label} (clip '{clip.name}', {clip.length:F2}s, family idx {idx}) ---");
                sb.AppendLine($"[prop-diag]   {prop.note}");
                sb.AppendLine($"[prop-diag]   holder dial: offset={HeldWeaponCycleDebug.WeaponMeshLocalOffset[idx]:F3} " +
                              $"euler={HeldWeaponCycleDebug.WeaponMeshLocalEuler[idx]:F1} " +
                              $"scale={HeldWeaponCycleDebug.WeaponMeshScale[idx]:F3}");

                // A/B on the ONE thing the ticket names as the pivot suspect: the always-on CastawayArmPose carry
                // offset. Pass 1 = the shipped chain. Pass 2 = the SAME chain with the arm offset forced to
                // identity. If the two agree, the arm offset does not own the defect — measured, not argued.
                PropSeatPass(sb, label, clip, model, hips, head, lArm, rArm, lHand, rHand, rFore,
                             prop, seatOffset, seatEuler, armR, armL, wristR, wristL, applyArmPose: true, verbose: true);
                PropSeatPass(sb, label, clip, model, hips, head, lArm, rArm, lHand, rHand, rFore,
                             prop, seatOffset, seatEuler, armR, armL, wristR, wristL, applyArmPose: false, verbose: false);

                // The de-grip SIZING pass runs only on the two-handed suspect.
                if (label == "pickaxe")
                {
                    DeGripSweep(sb, clip, model, hips, head, lArm, rArm, lHand, rHand, rFore,
                                prop, seatOffset, seatEuler, armR, armL);
                    // 86cay4282 ROUND 3 — WHERE ALONG THE HAFT can a hand actually go? Measured from the mesh
                    // itself, before any fit uses it, because round 2 chose u_right = 0.80 with no evidence about
                    // what geometry lives at 0.80 (or at 0.95).
                    float bareHaftTopU = HaftProfile(sb, prop);
                    // 86cay4282 ROUND 2 — the Sponsor REVERSED the direction ("we need to position the axe for a
                    // two hand grip"), so the clip is right and the TOOL is in the wrong place. Fit the seat.
                    MineSeatFit(sb, clip, model, hips, head, lArm, rArm, lHand, rHand,
                                prop, seatOffset, seatEuler, armR, armL, wristR, wristL, bareHaftTopU);
                    // 86cay4282 ROUND 4 — the Sponsor: "R/V only manipulates the right hand, which is great, but what
                    // about the left hand? its not even touching the shaft". A CONSTANT seat cannot close it (the
                    // hand-line direction wanders 21.1deg mean / 36.5deg MAX about its own mean — measured above), so
                    // the left hand gets a PER-FRAME two-bone IK. Everything the fix needs is measured first: what
                    // TOUCHING means off the meshes, and how far up the haft the arm can actually reach.
                    HandTouchGeometry(sb, model, lHand, lMid1, prop, bareHaftTopU);
                    LeftArmIkSweep(sb, clip, model, hips, head, lArm, lFore, lHand, lMid1, rArm, rHand,
                                   prop, seatOffset, seatEuler, armR, armL, wristR, wristL, bareHaftTopU);
                }

                Object.DestroyImmediate(prop.root.gameObject);
            }
            sb.AppendLine("[prop-diag] ===== END =====");
        }

        /// <summary>
        /// SIZING the left-arm DE-GRIP offset by measurement (86cay4282). Two stages, in the order the authoring
        /// checklist mandates:
        ///   (1) AXIS PROBE — +30deg on each of the LEFT upper arm's local X / Y / Z singly, reporting where the
        ///       left hand actually goes in the character's own torso frame. The Mixamo Generic rig's bone-local
        ///       axes are arbitrary; the shipped cheat-sheet was measured on the RIGHT arm, so the left arm's
        ///       signs are NOT derivable from it. Guessing here is how an offset swings the wrong way.
        ///   (2) SWEEP — candidate offsets scored on the metric the defect is defined by: hand SEPARATION
        ///       (lRHand). The two-handed read is the hands being LOCKED CLOSE; the target band is the Sponsor-
        ///       approved carry's own separation. Also reports the left hand's clearance to the torso axis so a
        ///       candidate that solves the grip by driving the arm INTO the body is visible, not silently picked.
        /// </summary>
        private static void DeGripSweep(StringBuilder sb, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm,
            Transform lHand, Transform rHand, Transform rFore,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler, Vector3 armR, Vector3 armL)
        {
            sb.AppendLine("[prop-diag]   --- DE-GRIP SIZING (86cay4282): left-arm additive offset ---");
            sb.AppendLine("[prop-diag]   (1) AXIS PROBE — +30deg singly on the LEFT upper arm's local axes, at the");
            sb.AppendLine("[prop-diag]       clip's tightest-hands frame. dOut/dFwd/dUp = where the LEFT HAND moves,");
            sb.AppendLine("[prop-diag]       torso frame, shoulder-widths (+out = away from the midline, left side).");

            // Locate the frame where the hands are TIGHTEST — the worst frame for the two-handed read, and the one
            // any candidate must fix.
            int N = 61; float worstT = 0f, worstLR = float.MaxValue;
            for (int i = 0; i < N; i++)
            {
                float nt = i / (float)(N - 1);
                clip.SampleAnimation(model, nt * clip.length);
                rArm.localRotation = rArm.localRotation * Quaternion.Euler(armR);
                lArm.localRotation = lArm.localRotation * Quaternion.Euler(armL);
                float sw = (rArm.position - lArm.position).magnitude;
                if (sw < 1e-5f) continue;
                float lr = (lHand.position - rHand.position).magnitude / sw;
                if (lr < worstLR) { worstLR = lr; worstT = nt; }
            }
            sb.AppendLine($"[prop-diag]       tightest-hands frame t={worstT:F3} (lRHand={worstLR:F2})");

            var probes = new (string name, Vector3 e)[]
            {
                ("baseline", Vector3.zero),
                ("+30 X", new Vector3(30f, 0f, 0f)), ("-30 X", new Vector3(-30f, 0f, 0f)),
                ("+30 Y", new Vector3(0f, 30f, 0f)), ("-30 Y", new Vector3(0f, -30f, 0f)),
                ("+30 Z", new Vector3(0f, 0f, 30f)), ("-30 Z", new Vector3(0f, 0f, -30f)),
            };
            Vector3 baseOut = Vector3.zero;
            foreach (var (name, e) in probes)
            {
                clip.SampleAnimation(model, worstT * clip.length);
                rArm.localRotation = rArm.localRotation * Quaternion.Euler(armR);
                lArm.localRotation = lArm.localRotation * Quaternion.Euler(armL) * Quaternion.Euler(e);
                if (!TorsoFrame(hips, head, lArm, rArm, out Vector3 rightAxis, out Vector3 up,
                                out Vector3 fwdAxis, out Vector3 chest, out float sw)) continue;
                Vector3 d = (lHand.position - chest) / sw;
                Vector3 v = new Vector3(-Vector3.Dot(d, rightAxis), Vector3.Dot(d, fwdAxis), Vector3.Dot(d, up));
                if (name == "baseline") baseOut = v;
                Vector3 delta = v - baseOut;
                float lr = (lHand.position - rHand.position).magnitude / sw;
                sb.AppendLine($"[prop-diag]       {name,-9} lHand out={v.x,6:F2} fwd={v.y,6:F2} up={v.z,6:F2}  " +
                              $"d=({delta.x,6:F2},{delta.y,6:F2},{delta.z,6:F2})  lRHand={lr:F2}");
            }

            sb.AppendLine("[prop-diag]   (2) SWEEP — whole-clip bands per candidate. TARGET: lift lRHand's MINIMUM");
            sb.AppendLine("[prop-diag]       to at least the approved carry's separation, without collapsing");
            sb.AppendLine("[prop-diag]       lTorso (left-hand clearance to the hips->head axis; small = arm in body).");
            var cands = new Vector3[]
            {
                Vector3.zero,
                new Vector3(25f, 0f, 0f), new Vector3(40f, 0f, 0f),
                new Vector3(0f, 0f, -25f), new Vector3(0f, 0f, -40f),
                new Vector3(25f, 0f, -25f), new Vector3(40f, 0f, -25f), new Vector3(40f, 0f, -40f),
                new Vector3(-25f, 0f, 0f), new Vector3(-40f, 0f, 0f),
                new Vector3(-55f, 0f, 0f), new Vector3(-70f, 0f, 0f),
                new Vector3(-40f, 0f, 20f), new Vector3(-55f, 0f, 20f),
                new Vector3(0f, 0f, 25f), new Vector3(0f, 0f, 40f),
            };
            foreach (var e in cands)
            {
                float lrMin = float.MaxValue, lrMax = float.MinValue;
                float torsoMin = float.MaxValue, haftMin = float.MaxValue;
                for (int i = 0; i < N; i++)
                {
                    float nt = i / (float)(N - 1);
                    clip.SampleAnimation(model, nt * clip.length);
                    rArm.localRotation = rArm.localRotation * Quaternion.Euler(armR);
                    lArm.localRotation = lArm.localRotation * Quaternion.Euler(armL) * Quaternion.Euler(e);
                    prop.root.position = rHand.position + rHand.rotation * seatOffset;
                    prop.root.rotation = rHand.rotation * Quaternion.Euler(seatEuler);
                    if (!TorsoFrame(hips, head, lArm, rArm, out _, out Vector3 up2, out _, out _, out float sw)) continue;

                    float lr = (lHand.position - rHand.position).magnitude / sw;
                    lrMin = Mathf.Min(lrMin, lr); lrMax = Mathf.Max(lrMax, lr);
                    // clearance from the left hand to the torso AXIS (hips->head line) — the self-intersection proxy.
                    Vector3 ax = head.position - hips.position;
                    float t = Mathf.Clamp01(Vector3.Dot(lHand.position - hips.position, ax) / ax.sqrMagnitude);
                    torsoMin = Mathf.Min(torsoMin, (lHand.position - (hips.position + ax * t)).magnitude / sw);
                    Vector3 gripW = prop.holder.TransformPoint(prop.gripLocal);
                    Vector3 gh = prop.holder.TransformPoint(prop.headLocal) - gripW;
                    float u = Mathf.Clamp01(Vector3.Dot(lHand.position - gripW, gh) / gh.sqrMagnitude);
                    haftMin = Mathf.Min(haftMin, (lHand.position - (gripW + gh * u)).magnitude / sw);
                    _ = up2;
                }
                sb.AppendLine($"[prop-diag]       leftArmDeGrip={e,-18:F0} lRHand {lrMin:F2}..{lrMax:F2} " +
                              $"| lTorsoClearMin {torsoMin:F2} | lHaftMin {haftMin:F2}");
            }
        }

        /// <summary>
        /// 86cay4282 ROUND 3 — THE HAFT'S OWN SHAPE ALONG ITS LENGTH, measured from the mesh.
        ///
        /// WHY THIS EXISTS. Round 2 fitted the seat with the right hand at u = 0.80 of the haft and the left hand at
        /// u = 0.10..0.30, then described that as "the grip geometry a real two-handed swing has". The Sponsor soaked
        /// it and disagreed: "how can i dial that the left hand is not on the bottom of the axe". Sliding the pair UP
        /// the haft is the fix — but "how far up can they go" is a question about the MESH, and nothing in round 2
        /// measured it. Without this number a fit is free to park the working hand inside the pick head, which reads
        /// worse than the defect it replaces.
        ///
        /// WHAT IT MEASURES. Every mesh vertex is bucketed by its position along the resolved long axis (u, 0 = butt
        /// / grip end, 1 = head end — the same convention <see cref="HeldToolRig.TryGetHaftSegment"/> and
        /// <see cref="TwoHandGripRead"/> use), and each bucket reports its CROSS-SECTION RADIUS: the largest
        /// perpendicular distance from that slice's own perpendicular centroid, as a fraction of the haft's length so
        /// the figure is scale-free. A bare wooden haft is a thin near-constant cylinder; the pick head is a wide
        /// crossing mass. So the head announces itself as a step change in radius, and the top of the BARE haft is the
        /// last slice before that step.
        ///
        /// RETURNS the u of the top of the bare haft — the ceiling any grip-position fit must respect. The whole
        /// profile table is printed so the number is evidenced rather than asserted.
        /// </summary>
        /// <summary>86cay4282 round 4 — the bare-haft cross-section radius as a FRACTION of the haft length, carried out
        /// of <see cref="HaftProfile"/> so <see cref="HandTouchGeometry"/> can turn it into metres against the WORLD haft
        /// length rather than re-deriving the bucket maths beside it. -1 = not measurable (never substitute a guess).</summary>
        private static float _bareHaftRadiusFrac = -1f;

        private static float HaftProfile(StringBuilder sb, in PropRig prop)
        {
            _bareHaftRadiusFrac = -1f;
            sb.AppendLine("[haft-profile]   --- HAFT PROFILE ALONG ITS OWN LENGTH (86cay4282 round 3) ---");
            sb.AppendLine("[haft-profile]   u: 0 = BUTT / grip end, 1 = HEAD end (the TwoHandGripRead convention).");
            sb.AppendLine("[haft-profile]   r = that slice's cross-section radius as a FRACTION of the haft length");
            sb.AppendLine("[haft-profile]       (scale-free). A bare haft is thin + near-constant; the head is a step.");

            Vector3[] verts = prop.mesh != null ? prop.mesh.vertices : null;
            if (verts == null || verts.Length == 0)
            {
                sb.AppendLine("[haft-profile]   ABORT — no readable vertices; NO grip-position ceiling measured, so a " +
                              "fit below must NOT claim one (do not substitute a guess).");
                return -1f;
            }

            const int Buckets = 20;
            int ax = prop.axis;
            int p1 = (ax + 1) % 3, p2 = (ax + 2) % 3;
            Bounds b = prop.mesh.bounds;
            float lo = b.min[ax], span = b.size[ax];
            if (span < 1e-6f) { sb.AppendLine("[haft-profile]   ABORT — degenerate long axis"); return -1f; }

            var perp = new List<Vector2>[Buckets];
            for (int i = 0; i < Buckets; i++) perp[i] = new List<Vector2>();
            foreach (var v in verts)
            {
                float f = (v[ax] - lo) / span;                 // 0..1 along the mesh's own low->high axis
                float u = prop.loIsGrip ? f : 1f - f;          // flip so 0 is always the GRIP/butt end
                int bi = Mathf.Clamp((int)(u * Buckets), 0, Buckets - 1);
                perp[bi].Add(new Vector2(v[p1], v[p2]));
            }

            var radii = new float[Buckets];
            for (int i = 0; i < Buckets; i++)
            {
                if (perp[i].Count == 0) { radii[i] = -1f; continue; }
                Vector2 c = Vector2.zero;
                foreach (var q in perp[i]) c += q;
                c /= perp[i].Count;
                float rMax = 0f;
                foreach (var q in perp[i]) rMax = Mathf.Max(rMax, (q - c).magnitude);
                radii[i] = rMax / span;                        // fraction of the haft length
            }

            // BASELINE = the median radius of the slices in the LOWER HALF of the haft. The lower half is where the
            // bare stick lives on every weapon in this family (the grip origin is (0,0,0) by the export contract), and
            // a MEDIAN is used rather than a mean so a butt flare or one stray vertex cannot set the baseline.
            var lowHalf = new List<float>();
            for (int i = 0; i < Buckets / 2; i++) if (radii[i] > 0f) lowHalf.Add(radii[i]);
            lowHalf.Sort();
            float baseR = lowHalf.Count > 0 ? lowHalf[lowHalf.Count / 2] : -1f;
            _bareHaftRadiusFrac = baseR;

            // The head is the first slice, scanning UP from the middle, whose radius exceeds the bare-haft baseline by
            // this factor. 2.0x is deliberately generous: a subtle taper or a collar must not be mistaken for the
            // head, while a pick/blade crossing the haft is several times wider than the stick.
            const float HeadRadiusFactor = 2.0f;
            int headBucket = -1;
            if (baseR > 0f)
                for (int i = Buckets / 2; i < Buckets; i++)
                    if (radii[i] > baseR * HeadRadiusFactor) { headBucket = i; break; }

            for (int i = 0; i < Buckets; i++)
            {
                float u0 = i / (float)Buckets, u1 = (i + 1) / (float)Buckets;
                string mark = radii[i] < 0f ? "(empty)"
                            : headBucket >= 0 && i >= headBucket ? "<= HEAD"
                            : "bare haft";
                sb.AppendLine($"[haft-profile]   u {u0:F2}-{u1:F2}  n={perp[i].Count,5}  r={radii[i]:F4}  {mark}");
            }

            if (baseR <= 0f || headBucket < 0)
            {
                sb.AppendLine("[haft-profile]   NO head step found (baseline r=" + baseR.ToString("F4") + ") — this " +
                              "mesh does not separate into haft + head by radius, so NO ceiling is measured here.");
                return -1f;
            }

            float bareTopU = headBucket / (float)Buckets;
            sb.AppendLine($"[haft-profile]   bare-haft radius baseline r={baseR:F4}; head geometry starts at " +
                          $"u={bareTopU:F2} (first slice above {HeadRadiusFactor:F1}x baseline). => A HAND MUST STAY " +
                          $"BELOW u={bareTopU:F2}: above it the palm is inside the head mass, which reads worse than " +
                          "the defect being fixed.");
            return bareTopU;
        }

        /// <summary>
        /// 86cay4282 ROUND 2 — MINE-STATE SEAT FIT. The Sponsor reversed the round-1 premise: "we need to position
        /// the axe for a two hand grip". So the two-handed clip is CORRECT and the one-handed SEAT is the defect —
        /// the fix is to move the haft onto the hands, not the hand off the haft.
        ///
        /// WHAT IT SOLVES, and why it is a SOLVE rather than a sweep. The seat is rigid in the hand's own frame
        /// (`axisSpreadInHand` measured 0.000deg), so everything below is expressed in that frame and the geometry
        /// closes analytically:
        ///     Gh, Hh  = the tool's grip / head endpoints in the RIGHT HAND's frame (metres) — CONSTANT.
        ///     Lh_i    = the LEFT HAND's offset from the right hand in that same frame, per clip sample.
        ///     Rh      = 0 (the right hand IS that frame's origin).
        /// A two-hand grip means the haft LINE contains both hand points. So the required rotation delta is the
        /// one that turns the haft direction onto the hand-to-hand direction, and the required position delta is
        /// the one that slides the resulting line onto the right hand. Both are computed, not searched.
        ///
        /// THE LOAD-BEARING NUMBER is the ANGULAR SPREAD of Lh_i over the swing: the delta is ONE constant, so a
        /// small spread means one constant fits the whole swing and a large spread means it cannot (and the honest
        /// answer would be that no constant seat can). It is reported FIRST, before any candidate.
        ///
        /// TWO ORIENTATIONS are fitted and printed, never assumed — a haft read is a LINE, so which END carries
        /// the head is a free choice the geometry does not make for us:
        ///     A = head beyond the LEFT hand  (grip end at the right hand; right hand low on the haft)
        ///     B = head beyond the RIGHT hand (grip end at the left hand = the butt; right hand up the haft)
        /// The discriminator printed for each is where the tool HEAD lands in the character's own torso frame at
        /// the STRIKE frame: a mining strike drives the head AWAY from the body and DOWN/FORWARD, so the fit whose
        /// head goes there is the one that reads as a strike rather than as a shouldered pole.
        /// </summary>
        private static void MineSeatFit(StringBuilder sb, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm,
            Transform lHand, Transform rHand,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler, Vector3 armR, Vector3 armL,
            Vector3 wristR, Vector3 wristL, float bareHaftTopU)
        {
            // DENSE sampling (86cay4282 round 2). 61 samples over a ~5.2 s clip is one every ~0.085 s, and the
            // FINE-window pass measures whole-skeleton steps up to ~20.8 deg per AUTHORED frame — so a coarse grid
            // can STEP OVER a fast hand-line excursion and report a fit that the 60 fps shipped gate then fails.
            // That is exactly what happened: the 61-sample fit predicted a 0.612 SW worst frame and the shipped
            // exe measured 1.220 SW. This is the registry's own documented caveat on this instrument ("a feature
            // narrower than clip_length/24 is invisible") biting the pass that was added to it.
            const int N = 361;
            var qR = Quaternion.Euler(armR);
            var qL = Quaternion.Euler(armL);
            var qWristR = Quaternion.Euler(wristR);
            var qWristL = Quaternion.Euler(wristL);
            var qSeat = Quaternion.Euler(seatEuler);

            sb.AppendLine("[seat-fit]   --- MINE-STATE SEAT FIT (86cay4282 round 2: move the HAFT to the HANDS) ---");
            sb.AppendLine("[seat-fit]   All vectors in the RIGHT HAND's own frame (metres). Right hand = origin.");

            // ---- pass 1: gather the constants + the per-sample hand line ----
            Vector3 gh = Vector3.zero, hh = Vector3.zero;      // grip / head in the hand frame (constant)
            float ghDrift = 0f, hhDrift = 0f;
            var lhs = new List<Vector3>(N);                    // left hand in the hand frame, per sample
            var sws = new List<float>(N);
            float swMean = 0f;
            for (int i = 0; i < N; i++)
            {
                float nt = i / (float)(N - 1);
                clip.SampleAnimation(model, nt * clip.length);
                rArm.localRotation = rArm.localRotation * qR;
                lArm.localRotation = lArm.localRotation * qL;
                rHand.localRotation = rHand.localRotation * qWristR;   // order 65 — feeds the seat's hand.rotation
                lHand.localRotation = lHand.localRotation * qWristL;
                prop.root.position = rHand.position + rHand.rotation * seatOffset;
                prop.root.rotation = rHand.rotation * qSeat;

                float sw = (rArm.position - lArm.position).magnitude;
                if (sw < 1e-5f) continue;

                Quaternion inv = Quaternion.Inverse(rHand.rotation);
                Vector3 g = inv * (prop.holder.TransformPoint(prop.gripLocal) - rHand.position);
                Vector3 h = inv * (prop.holder.TransformPoint(prop.headLocal) - rHand.position);
                Vector3 l = inv * (lHand.position - rHand.position);
                if (i == 0) { gh = g; hh = h; }
                else { ghDrift = Mathf.Max(ghDrift, (g - gh).magnitude); hhDrift = Mathf.Max(hhDrift, (h - hh).magnitude); }
                lhs.Add(l); sws.Add(sw); swMean += sw;
            }
            if (lhs.Count < 8) { sb.AppendLine("[seat-fit]   ABORT — too few valid samples"); return; }
            swMean /= lhs.Count;

            float haftLen = (hh - gh).magnitude;
            Vector3 dCur = (hh - gh).normalized;
            sb.AppendLine($"[seat-fit]   RIGIDITY CHECK: grip drift {ghDrift:F6} m, head drift {hhDrift:F6} m over " +
                          $"{lhs.Count} samples (0 = the seat is rigid in the hand frame, so ONE constant delta " +
                          "applies to the whole swing by construction).");
            sb.AppendLine($"[seat-fit]   haft length {haftLen:F4} m = {haftLen / swMean:F2} shoulder-widths " +
                          $"(mean SW {swMean:F4} m). grip {gh:F4} head {hh:F4}");

            // The hand-to-hand line, in the hand frame. Mean direction + the SPREAD about it = whether one
            // constant delta can fit the whole swing at all.
            Vector3 dSum = Vector3.zero;
            float sepMin = float.MaxValue, sepMax = 0f, sepMean = 0f;
            foreach (var l in lhs)
            {
                dSum += l.normalized;
                float s = l.magnitude;
                sepMin = Mathf.Min(sepMin, s); sepMax = Mathf.Max(sepMax, s); sepMean += s;
            }
            sepMean /= lhs.Count;
            Vector3 dMean = dSum.normalized;
            float spreadMax = 0f, spreadMean = 0f;
            foreach (var l in lhs)
            {
                float a = Vector3.Angle(l.normalized, dMean);
                spreadMax = Mathf.Max(spreadMax, a); spreadMean += a;
            }
            spreadMean /= lhs.Count;
            sb.AppendLine($"[seat-fit]   HAND-LINE in the hand frame: dir spread about its mean {spreadMean:F1}deg " +
                          $"mean / {spreadMax:F1}deg MAX  |  separation {sepMin:F4}..{sepMax:F4} m " +
                          $"({sepMin / swMean:F2}..{sepMax / swMean:F2} SW), mean {sepMean:F4} m");
            sb.AppendLine($"[seat-fit]   fits-on-the-haft? max separation {sepMax / haftLen:F2} of the haft length " +
                          "(<1 = both hands can sit ON the haft; >1 = the haft is too short and no seat can do it).");
            sb.AppendLine($"[seat-fit]   current haft dir vs the mean hand line: " +
                          $"{Vector3.Angle(dCur, dMean):F1}deg (fold to a LINE: " +
                          $"{Mathf.Min(Vector3.Angle(dCur, dMean), 180f - Vector3.Angle(dCur, dMean)):F1}deg) " +
                          "— this IS the disagreement the Sponsor sees.");

            // ---- the two candidate fits ----
            // A: head beyond the LEFT hand — the haft runs right-hand -> left-hand, grip end just below the
            //    right hand. B: head beyond the RIGHT hand — the grip end (butt) sits at the LEFT hand.
            float uRightOnHaftA = 0.10f;                       // where the right hand sits along the haft, fit A
            var fits = new (string tag, Vector3 dWant, float aAlong)[]
            {
                ("A head-past-LEFT ", dMean,  uRightOnHaftA * haftLen),
                ("B head-past-RIGHT", -dMean, sepMean),
            };

            foreach (var (tag, dWant, aAlong) in fits)
            {
                // ROTATION: the minimal rotation taking the haft direction onto the wanted hand-line direction,
                // expressed back in the TOOL's own frame (the frame HeldToolRig right-multiplies the delta in).
                Quaternion mHand = Quaternion.FromToRotation(dCur, dWant);
                Quaternion eQ = Quaternion.Inverse(qSeat) * mHand * qSeat;
                Vector3 eEuler = NormEuler(eQ.eulerAngles);

                // POSITION: after the rotation, slide the haft so its line passes through the right hand with the
                // grip end `aAlong` metres BEFORE it along the haft.
                Vector3 gRot = seatOffset + mHand * (gh - seatOffset);
                Vector3 dPos = -aAlong * dWant - gRot;

                // Re-measure the WHOLE clip with the candidate applied — the fit is judged on the real per-frame
                // geometry, never on the mean it was derived from.
                Fitted(sb, tag, clip, model, hips, head, lArm, rArm, lHand, rHand, prop,
                       seatOffset, seatEuler, armR, armL, wristR, wristL, dPos, eEuler, haftLen);
                sb.AppendLine($"[seat-fit]     {tag} BAKE  HeldToolMineSeatOffsetDelta=" +
                              $"({dPos.x:F4}f,{dPos.y:F4}f,{dPos.z:F4}f)  HeldToolMineSeatEulerDelta=" +
                              $"({eEuler.x:F1}f,{eEuler.y:F1}f,{eEuler.z:F1}f)");
            }

            // The ZERO-DELTA control, same metrics, so the improvement is a measured delta not a claim.
            Fitted(sb, "ZERO (shipped today)", clip, model, hips, head, lArm, rArm, lHand, rHand, prop,
                   seatOffset, seatEuler, armR, armL, wristR, wristL, Vector3.zero, Vector3.zero, haftLen);

            // ---- REFINE. The two closed-form fits above aim the haft at the MEAN hand-line direction and pin the
            // line through the right hand exactly, so the whole 36.6deg direction spread lands on the LEFT hand.
            // A constant seat cannot beat that spread, but it CAN spend it better: the search below sweeps the haft
            // DIRECTION over a cone about the chosen orientation, how far along the haft the grip end sits, and how
            // far the line slides off the right hand toward the hand midpoint — scoring the real per-frame geometry.
            // It is exact + cheap because the seat is RIGID in the hand frame (drift 1e-6 m above): the hand points
            // in that frame were already captured, so every candidate is pure vector maths with NO re-sampling. The
            // winner is then RE-MEASURED through the live SampleAnimation path (Fitted) as the cross-check that the
            // closed-form maths and the real skeleton agree.
            RefineMineSeat(sb, clip, model, hips, head, lArm, rArm, lHand, rHand, prop,
                           seatOffset, seatEuler, armR, armL, wristR, wristL, lhs, sws, gh, hh, dMean, haftLen);

            // ---- ROUND 3: WHERE ON THE HAFT the grip pair sits. Everything above optimises each hand's DISTANCE to
            // the haft LINE and leaves the along-haft position wherever it falls — which is how round 2 shipped the
            // left hand at u 0.10..0.30 (the butt) while scoring a clean PASS. This pass adds the missing objective.
            ChokeUpMineSeat(sb, clip, model, hips, head, lArm, rArm, lHand, rHand, prop,
                            seatOffset, seatEuler, armR, armL, wristR, wristL, lhs, sws, gh, hh, dMean, haftLen,
                            bareHaftTopU);
        }

        /// <summary>
        /// Search the best CONSTANT MINE seat delta against the captured per-frame hand geometry (86cay4282 round 2).
        /// Objective: get BOTH hands onto the haft line — minimise the LEFT hand's worst-frame distance to the haft
        /// while holding the RIGHT hand (the real, physical grip) within <c>RightHaftCapSW</c>, because a right hand
        /// visibly off its own haft is a worse defect than a phantom left hand slightly off it.
        /// </summary>
        private static void RefineMineSeat(StringBuilder sb, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm, Transform lHand, Transform rHand,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler, Vector3 armR, Vector3 armL,
            Vector3 wristR, Vector3 wristL,
            List<Vector3> lhs, List<float> sws, Vector3 gh, Vector3 hh, Vector3 dMean, float haftLen)
        {
            // The right hand IS the physical grip; keep it essentially on the haft. 0.08 SW ~= 3.7 cm at this rig.
            const float RightHaftCapSW = 0.08f;
            Vector3 dCur = (hh - gh).normalized;
            Vector3 dBase = -dMean;                       // orientation B (head past the RIGHT hand) — chosen above
            // Two axes perpendicular to dBase to sweep the cone in.
            Vector3 p1 = Vector3.Cross(dBase, Vector3.up);
            if (p1.sqrMagnitude < 1e-4f) p1 = Vector3.Cross(dBase, Vector3.right);
            p1.Normalize();
            Vector3 p2 = Vector3.Cross(dBase, p1).normalized;

            Vector3 midMean = Vector3.zero;
            foreach (var l in lhs) midMean += l;
            midMean /= lhs.Count;                          // the mean left-hand offset; its half is the hand midpoint

            float bestScore = float.MaxValue;
            Vector3 bestD = dBase, bestG = Vector3.zero;
            float bestL = 0f, bestLMean = 0f, bestR = 0f, bestA = 0f, bestB = 0f, bestAx = 0f, bestAy = 0f;

            for (int ix = -6; ix <= 6; ix++)
            for (int iy = -6; iy <= 6; iy++)
            {
                float ax = ix * 5f, ay = iy * 5f;
                Vector3 d = (Quaternion.AngleAxis(ax, p1) * Quaternion.AngleAxis(ay, p2) * dBase).normalized;
                for (int ib = 0; ib <= 4; ib++)
                {
                    float beta = ib * 0.125f;              // 0 = the line through the right hand, 1 = through the mid
                    Vector3 through = midMean * 0.5f * beta;
                    for (int ia = 6; ia <= 17; ia++)
                    {
                        float aFrac = ia * 0.05f;          // where along the haft the grip end sits
                        Vector3 g = through - d * (aFrac * haftLen);
                        Vector3 h = g + d * haftLen;

                        float lMax = 0f, lSum = 0f, rMax = 0f;
                        for (int i = 0; i < lhs.Count; i++)
                        {
                            float dl = SegDist(lhs[i], g, h) / sws[i];
                            float dr = SegDist(Vector3.zero, g, h) / sws[i];
                            lMax = Mathf.Max(lMax, dl); lSum += dl; rMax = Mathf.Max(rMax, dr);
                        }
                        if (rMax > RightHaftCapSW) continue;
                        float score = lMax + 0.5f * (lSum / lhs.Count);
                        if (score < bestScore)
                        {
                            bestScore = score; bestD = d; bestG = g;
                            bestL = lMax; bestLMean = lSum / lhs.Count; bestR = rMax;
                            bestA = aFrac; bestB = beta; bestAx = ax; bestAy = ay;
                        }
                    }
                }
            }

            if (bestScore == float.MaxValue) { sb.AppendLine("[seat-fit]   REFINE: no candidate met the right-hand cap"); return; }

            Quaternion mHand = Quaternion.FromToRotation(dCur, bestD);
            Vector3 eEuler = NormEuler((Quaternion.Inverse(Quaternion.Euler(seatEuler)) * mHand *
                                        Quaternion.Euler(seatEuler)).eulerAngles);
            Vector3 gRot = seatOffset + mHand * (gh - seatOffset);
            Vector3 dPos = bestG - gRot;

            sb.AppendLine($"[seat-fit]   REFINE (cone {bestAx:F0}/{bestAy:F0}deg off B, gripAt {bestA:F2} of the " +
                          $"haft, slide {bestB:F3} toward the hand midpoint): predicted lHaft mean {bestLMean:F3} " +
                          $"MAX {bestL:F3} SW, rHaft MAX {bestR:F3} SW (cap {RightHaftCapSW:F2})");
            Fitted(sb, "REFINED (live re-measure)", clip, model, hips, head, lArm, rArm, lHand, rHand, prop,
                   seatOffset, seatEuler, armR, armL, wristR, wristL, dPos, eEuler, haftLen);
            sb.AppendLine($"[seat-fit]     REFINED BAKE  HeldToolMineSeatOffsetDelta=" +
                          $"({dPos.x:F4}f,{dPos.y:F4}f,{dPos.z:F4}f)  HeldToolMineSeatEulerDelta=" +
                          $"({eEuler.x:F1}f,{eEuler.y:F1}f,{eEuler.z:F1}f)");
        }

        /// <summary>
        /// 86cay4282 ROUND 3 — FIT THE ALONG-HAFT GRIP POSITION, not just the distance to the haft line.
        ///
        /// THE DEFECT THIS EXISTS FOR (Sponsor soak of round 2, verbatim): "how can i dial that the left hand is not
        /// on the bottom of the axe". Round 2's objective was purely each hand's PERPENDICULAR distance to the haft
        /// line; where along the haft the hands landed was never scored, so a butt-end grip and a mid-haft grip were
        /// indistinguishable to every gate, panel and test. The round-2 fit happened to land the left hand at
        /// u 0.10..0.30 = clamped at the butt, and it read exactly as badly as the original defect.
        ///
        /// THE TARGET (Sponsor-chosen): MID-HAFT, CHOKED UP — left hand up off the butt with VISIBLE haft remaining
        /// below it, right hand above it nearer the head. So the objective here is to MAXIMISE the left hand's
        /// WORST-FRAME u (its lowest point over the swing, since that worst frame is what reads as "on the bottom"),
        /// subject to three hard constraints that keep the round-2 win intact:
        ///   • the RIGHT hand stays essentially ON the haft (it is the tool's real physical grip);
        ///   • the RIGHT hand stays BELOW the measured top of the bare haft (<see cref="HaftProfile"/>) — above that
        ///     the palm is inside the pick head, a worse read than the defect;
        ///   • the LEFT hand stays within the shipped two-hand cap, so this does not buy grip position by giving back
        ///     the "haft through both hands" property the whole round-2 fix is defined by.
        ///
        /// THE CEILING IS ARITHMETIC, AND IT IS REPORTED. u_right - u_left equals the hand separation PROJECTED onto
        /// the haft, divided by the haft length. That projection is a property of the CLIP and the mesh, not of any
        /// fit — so once u_right is capped, the best possible u_left follows and no amount of searching beats it.
        /// Printing that number is the honest answer to "why isn't the left hand at 0.5".
        /// </summary>
        private static void ChokeUpMineSeat(StringBuilder sb, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm, Transform lHand, Transform rHand,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler, Vector3 armR, Vector3 armL,
            Vector3 wristR, Vector3 wristL,
            List<Vector3> lhs, List<float> sws, Vector3 gh, Vector3 hh, Vector3 dMean, float haftLen,
            float bareHaftTopU)
        {
            const float RightHaftCapSW = 0.08f;
            // Keep the working hand clear of the head mass by this much of the haft, so a frame of jitter (or the
            // Sponsor nudging one step further) cannot push the palm into the pick.
            const float HeadMarginU = 0.04f;
            // FALLBACK when HaftProfile could not measure a head step: 0.85 = round 2's own sweep ceiling, i.e. no
            // NEW claim is made. Never silently invent a higher ceiling than something measured.
            float gripCeiling = bareHaftTopU > 0f ? bareHaftTopU - HeadMarginU : 0.85f;

            sb.AppendLine("[choke-up]   --- ALONG-HAFT GRIP POSITION FIT (86cay4282 round 3) ---");
            sb.AppendLine($"[choke-up]   right-hand ceiling u <= {gripCeiling:F2} " +
                          (bareHaftTopU > 0f
                            ? $"(measured bare-haft top {bareHaftTopU:F2} minus a {HeadMarginU:F2} margin)"
                            : "(UNMEASURED head step — falling back to round 2's own 0.85 sweep ceiling, no new claim)"));

            Vector3 dCur = (hh - gh).normalized;
            Vector3 dBase = -dMean;   // orientation B: head beyond the RIGHT hand = right hand nearer the head.

            // THE ARITHMETIC CEILING, before any search. u_right - u_left IS the hand separation PROJECTED onto the
            // haft, over the haft length. With the haft ALIGNED to the hand line (which is what makes both hands sit
            // ON it) that projection is the full separation, so the left hand's along-haft position is not a free
            // parameter at all — it is u_right minus a number the CLIP owns.
            float spanMaxU = 0f, spanMinU = 9f;
            foreach (var l in lhs)
            {
                float s = Mathf.Abs(Vector3.Dot(l, dBase)) / haftLen;
                spanMaxU = Mathf.Max(spanMaxU, s); spanMinU = Mathf.Min(spanMinU, s);
            }
            sb.AppendLine($"[choke-up]   hand span PROJECTED on the ALIGNED haft: {spanMinU:F2}..{spanMaxU:F2} of the " +
                          $"haft length ({spanMinU * haftLen * 100f:F0}..{spanMaxU * haftLen * 100f:F0} cm of a " +
                          $"{haftLen * 100f:F0} cm tool, of which only {bareHaftTopU * haftLen * 100f:F0} cm is BARE " +
                          "haft). The pair therefore fills the stick.");
            sb.AppendLine($"[choke-up]   => with the haft aligned and the right hand at the very top of the bare haft " +
                          $"(u {bareHaftTopU:F2}), the left hand's WORST frame lands at u " +
                          $"{bareHaftTopU - spanMaxU:F2}. A truly MID-HAFT left hand (u 0.50) would need the right " +
                          $"hand at u {0.50f + spanMaxU:F2} — {(0.50f + spanMaxU - 1f) * 100f:F0}% PAST the head end " +
                          "of the whole tool. That is not a fit quality; it is arithmetic.");
            sb.AppendLine("[choke-up]   The only lever left is TILTING the haft off the hand line, which shortens the " +
                          "projection (lifting u_left) at the exact cost of the left hand's distance to the haft — the " +
                          "property the two-hand read IS. The Pareto front below prices that trade.");

            Vector3 p1 = Vector3.Cross(dBase, Vector3.up);
            if (p1.sqrMagnitude < 1e-4f) p1 = Vector3.Cross(dBase, Vector3.right);
            p1.Normalize();
            Vector3 p2 = Vector3.Cross(dBase, p1).normalized;

            Vector3 midMean = Vector3.zero;
            foreach (var l in lhs) midMean += l;
            midMean /= lhs.Count;

            float bestULeftMin = -99f, bestScore = float.MaxValue;
            Vector3 bestD = dBase, bestG = Vector3.zero;
            float bestL = 0f, bestLMean = 0f, bestR = 0f, bestUR = 0f, bestULMax = 0f;
            float bestA = 0f, bestB = 0f, bestAx = 0f, bestAy = 0f;
            int considered = 0, rejectRight = 0, rejectCeiling = 0, rejectLeftCap = 0;

            // THE TRADE CURVE (Pareto front). Buckets of the left hand's worst-frame DISTANCE to the haft (the
            // two-hand-read quality), each holding the best along-haft position achievable at that quality. This is
            // the honest answer to "just slide it up a bit more": it prices every step in the currency the fix is
            // defined by, so neither the Sponsor nor a reviewer has to take a trade on trust.
            const int Fronts = 14;                   // 0.05-SW buckets from 0.40 to 1.10 SW
            const float FrontLo = 0.40f, FrontStep = 0.05f;
            var frontU = new float[Fronts];
            for (int i = 0; i < Fronts; i++) frontU[i] = -99f;

            for (int ix = -6; ix <= 6; ix++)
            for (int iy = -6; iy <= 6; iy++)
            {
                float ax = ix * 5f, ay = iy * 5f;
                Vector3 d = (Quaternion.AngleAxis(ax, p1) * Quaternion.AngleAxis(ay, p2) * dBase).normalized;
                for (int ib = 0; ib <= 4; ib++)
                {
                    float beta = ib * 0.125f;
                    Vector3 through = midMean * 0.5f * beta;
                    // FINER + HIGHER than round 2's 0.30..0.85 in 0.05 steps: the whole point of this pass is to push
                    // the pair up the haft, so the sweep must reach the measured ceiling and resolve it finely.
                    for (int ia = 30; ia <= 100; ia++)
                    {
                        float aFrac = ia * 0.01f;
                        Vector3 g = through - d * (aFrac * haftLen);
                        Vector3 h = g + d * haftLen;
                        considered++;

                        // u of each hand along THIS candidate haft (right hand is the frame origin).
                        float uR = Vector3.Dot(-g, d) / haftLen;
                        if (uR > gripCeiling) { rejectCeiling++; continue; }

                        float lMax = 0f, lSum = 0f, rMax = 0f, uLMin = 9f, uLMax = -9f;
                        for (int i = 0; i < lhs.Count; i++)
                        {
                            float dl = SegDist(lhs[i], g, h) / sws[i];
                            float dr = SegDist(Vector3.zero, g, h) / sws[i];
                            lMax = Mathf.Max(lMax, dl); lSum += dl; rMax = Mathf.Max(rMax, dr);
                            float uL = Vector3.Dot(lhs[i] - g, d) / haftLen;
                            uLMin = Mathf.Min(uLMin, uL); uLMax = Mathf.Max(uLMax, uL);
                        }
                        if (rMax > RightHaftCapSW) { rejectRight++; continue; }

                        // Record the trade curve BEFORE the left-cap filter, so the front shows what a LOOSER cap
                        // would (and would not) buy — the question a reviewer will ask about the cap.
                        int fi = (int)((lMax - FrontLo) / FrontStep);
                        if (fi >= 0 && fi < Fronts && uLMin > frontU[fi]) frontU[fi] = uLMin;

                        if (lMax > TwoHandGripRead.LeftHaftPassSW) { rejectLeftCap++; continue; }

                        // PRIMARY: lift the left hand's WORST frame off the butt. SECONDARY (tie-break within 0.01 u):
                        // keep it closest to the haft line, so grip position is never bought with a worse two-hand read.
                        float score = -uLMin + 0.02f * lMax;
                        if (uLMin > bestULeftMin + 0.01f || (uLMin > bestULeftMin - 0.01f && score < bestScore))
                        {
                            if (uLMin > bestULeftMin) bestULeftMin = uLMin;
                            bestScore = score; bestD = d; bestG = g;
                            bestL = lMax; bestLMean = lSum / lhs.Count; bestR = rMax;
                            bestUR = uR; bestULMax = uLMax;
                            bestA = aFrac; bestB = beta; bestAx = ax; bestAy = ay;
                        }
                    }
                }
            }

            sb.AppendLine($"[choke-up]   searched {considered} candidates; rejected {rejectCeiling} on the head " +
                          $"ceiling, {rejectRight} on the right-hand cap ({RightHaftCapSW:F2} SW), {rejectLeftCap} on " +
                          $"the left-hand two-hand cap ({TwoHandGripRead.LeftHaftPassSW:F2} SW).");
            sb.AppendLine("[choke-up]   TRADE CURVE — best worst-frame u_left buyable at each left-hand-to-haft " +
                          "quality (right hand pinned on the haft + below the head throughout):");
            for (int i = 0; i < Fronts; i++)
            {
                if (frontU[i] < -50f) continue;
                float lo = FrontLo + i * FrontStep;
                sb.AppendLine($"[choke-up]     lHaft MAX <= {lo + FrontStep:F2} SW  ->  u_left worst {frontU[i]:F2} " +
                              $"({frontU[i] * haftLen * 100f:F0} cm of haft below the left hand)" +
                              (lo + FrontStep > TwoHandGripRead.LeftHaftPassSW ? "   [BEYOND the shipped cap]" : ""));
            }
            if (bestULeftMin < -50f)
            {
                sb.AppendLine("[choke-up]   NO candidate satisfied all three constraints — the round-2 fit stands and " +
                              "this ticket's grip-position ask needs a LONGER HAFT (a Blender re-author), not a seat " +
                              "delta. Do not ship a fit that violated a constraint.");
                return;
            }

            Quaternion mHand = Quaternion.FromToRotation(dCur, bestD);
            Vector3 eEuler = NormEuler((Quaternion.Inverse(Quaternion.Euler(seatEuler)) * mHand *
                                        Quaternion.Euler(seatEuler)).eulerAngles);
            Vector3 gRot = seatOffset + mHand * (gh - seatOffset);
            Vector3 dPos = bestG - gRot;

            sb.AppendLine($"[choke-up]   WINNER (cone {bestAx:F0}/{bestAy:F0}deg off B, gripAt {bestA:F2} of the " +
                          $"haft, slide {bestB:F3} toward the hand midpoint): u_right {bestUR:F2}, u_left " +
                          $"{bestULeftMin:F2}..{bestULMax:F2}, predicted lHaft mean {bestLMean:F3} MAX {bestL:F3} SW, " +
                          $"rHaft MAX {bestR:F3} SW. Haft remaining BELOW the left hand at its worst frame: " +
                          $"{bestULeftMin:F2} of the haft = {bestULeftMin * haftLen * 100f:F0} cm.");
            Fitted(sb, "CHOKED-UP (live re-measure)", clip, model, hips, head, lArm, rArm, lHand, rHand, prop,
                   seatOffset, seatEuler, armR, armL, wristR, wristL, dPos, eEuler, haftLen);
            sb.AppendLine($"[choke-up]     CHOKED-UP BAKE  HeldToolMineSeatOffsetDelta=" +
                          $"({dPos.x:F4}f,{dPos.y:F4}f,{dPos.z:F4}f)  HeldToolMineSeatEulerDelta=" +
                          $"({eEuler.x:F1}f,{eEuler.y:F1}f,{eEuler.z:F1}f)");
        }

        /// <summary>Distance from a point to the SEGMENT a..b (the same clamped measure the runtime read uses).</summary>
        // ==================================================================================================
        // 86cay4282 ROUND 4 — WHAT "TOUCHING" MEANS, AND WHETHER THE ARM CAN REACH IT.
        //
        // The Sponsor, soaking round 3, verbatim: "R/V only manipulates the right hand, which is great, but what about
        // the left hand? its not even touching the shaft". He is right, and the numbers to prove it were already in
        // round 3's own log: at the measured mean shoulder width 0.4580 m the shipped left hand sits 0.445 SW = 20.4 cm
        // mean / 0.615 SW = 28.2 cm worst off the haft, while TwoHandGripRead.LeftHaftPassSW = 0.80 SW PERMITS 36.6 cm.
        // That cap was calibrated from what a static seat could ACHIEVE, not from what "one haft through both hands"
        // MEANS — so the gate was green on a hand gripping air by a quarter of a metre.
        //
        // TWO THINGS ROUND 3 ALREADY SETTLED, not re-derived here: the haft is NOT too short ("fits-on-the-haft? max
        // separation 0.72"), and no single CONSTANT seat can close the gap (the hand-line direction wanders 21.1deg
        // mean / 36.5deg MAX about its own mean, and the residual IS that wander). Hence a per-frame solve.
        //
        // THESE TWO PASSES ARE THE MEASUREMENTS THE FIX IS ALLOWED TO USE:
        //   (1) HandTouchGeometry — the geometric definition of touching, off the MESHES: two cylinders touch when
        //       their axis separation is <= the sum of their radii. So hand radius + haft radius, in metres, plus the
        //       wrist->palm offset (because the metric's anchor is the WRIST BONE while the percept is the PALM).
        //   (2) LeftArmIkSweep — the reach envelope. The seat is fixed, so the haft moves through the swing; whether a
        //       pin at a given u_left is reachable at EVERY frame is a measurement, not an assumption. This is the pass
        //       that decides the DIAL's honest range, and it also scores the pole plane's conditioning per u so the
        //       elbow-flip guard is chosen from evidence.
        // ==================================================================================================

        /// <summary>
        /// 86cay4282 round 4 — measure TOUCHING off the meshes. Prints, in metres AND in shoulder-widths (the units the
        /// caps live in), so a cap can be re-derived from a definition instead of from what a fit happened to achieve.
        ///
        /// SCALE DISCIPLINE (unity-conventions.md §FBX — the walk-float saga's Bug B). The castaway's
        /// SkinnedMeshRenderer transform carries a baked 100x cm->m scale, so ANY world-space vertex measurement via
        /// <c>localToWorldMatrix</c> DOUBLE-APPLIES it and returns garbage. This pass never does that: the hand's
        /// vertex cloud is measured in the HAND BONE's own BIND space (via <c>mesh.bindposes</c>), and the single scale
        /// factor to metres is derived from two LIVE BONE POSITIONS (wrist and knuckle) whose distance is already
        /// world-correct. No skinned-mesh matrix is touched.
        /// </summary>
        private static void HandTouchGeometry(StringBuilder sb, GameObject model, Transform lHand, Transform lMid1,
                                              in PropRig prop, float bareHaftTopU)
        {
            sb.AppendLine("[hand-mesh]   --- WHAT 'TOUCHING' MEANS, MEASURED OFF THE MESHES (86cay4282 round 4) ---");
            sb.AppendLine("[hand-mesh]   Two cylinders TOUCH when their axis separation <= rHand + rHaft. That is the");
            sb.AppendLine("[hand-mesh]   definition TwoHandGripRead's left cap must come from — round 3's 0.80 SW came");
            sb.AppendLine("[hand-mesh]   from what a constant seat could achieve, which is a different question.");

            if (lMid1 == null)
            {
                sb.AppendLine("[hand-mesh]   ABORT — 'mixamorig:LeftHandMiddle1' not on this rig, so there is NO palm");
                sb.AppendLine("[hand-mesh]   proxy and NO measured hand radius. Do NOT substitute a guessed cap.");
                return;
            }

            // ---- the HAFT radius: the bare-stick cross-section, as a fraction of the haft length x the WORLD length.
            Vector3 gripW = prop.holder.TransformPoint(prop.gripLocal);
            Vector3 headW = prop.holder.TransformPoint(prop.headLocal);
            float haftLenWorld = (headW - gripW).magnitude;
            float rHaft = _bareHaftRadiusFrac > 0f ? _bareHaftRadiusFrac * haftLenWorld : -1f;

            // ---- the HAND radius, in the hand bone's own bind space, scaled to metres by a live bone distance.
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            float rHandMedian = -1f, rHandMax = -1f;
            int handVerts = 0;
            if (smr != null && smr.sharedMesh != null && smr.bones != null)
            {
                Mesh mesh = smr.sharedMesh;
                int handIdx = -1, mid1Idx = -1;
                for (int i = 0; i < smr.bones.Length; i++)
                {
                    if (smr.bones[i] == null) continue;
                    if (smr.bones[i] == lHand) handIdx = i;
                    else if (smr.bones[i] == lMid1) mid1Idx = i;
                }
                var bind = mesh.bindposes;
                var bw = mesh.boneWeights;
                var verts = mesh.vertices;
                if (handIdx < 0 || mid1Idx < 0 || bind == null || bind.Length <= Mathf.Max(handIdx, mid1Idx) ||
                    bw == null || bw.Length != verts.Length)
                {
                    sb.AppendLine($"[hand-mesh]   hand-radius UNMEASURABLE (handIdx={handIdx} mid1Idx={mid1Idx} " +
                                  $"bindposes={(bind == null ? 0 : bind.Length)} weights={(bw == null ? 0 : bw.Length)} " +
                                  $"verts={verts.Length}) — no cap may be derived from a missing measurement.");
                }
                else
                {
                    // The knuckle origin expressed in the HAND bone's bind space, and the same distance in WORLD
                    // metres off the live bones. Their ratio is the ONE scale factor used below.
                    Vector3 knuckleInHandBind = bind[handIdx].MultiplyPoint3x4(
                        bind[mid1Idx].inverse.MultiplyPoint3x4(Vector3.zero));
                    float dBind = knuckleInHandBind.magnitude;
                    float dWorld = (lMid1.position - lHand.position).magnitude;
                    if (dBind < 1e-6f || dWorld < 1e-6f)
                    {
                        sb.AppendLine("[hand-mesh]   hand-radius UNMEASURABLE — degenerate wrist->knuckle distance " +
                                      $"(bind {dBind:F6}, world {dWorld:F6}).");
                    }
                    else
                    {
                        float k = dWorld / dBind;                       // bind units -> world metres
                        Vector3 axisBind = knuckleInHandBind / dBind;   // the palm's own long axis, bind space
                        var radii = new List<float>();
                        for (int v = 0; v < verts.Length; v++)
                        {
                            // DOMINANT-weight membership: a vertex belongs to the hand when the hand carries its
                            // largest weight. Enumerated from the real weights rather than assumed from a Y-band —
                            // the documented mis-attribution trap (unity-conventions.md §FBX, the chibi atlas round).
                            var w = bw[v];
                            int top = w.boneIndex0;
                            float best = w.weight0;
                            if (w.weight1 > best) { best = w.weight1; top = w.boneIndex1; }
                            if (w.weight2 > best) { best = w.weight2; top = w.boneIndex2; }
                            if (w.weight3 > best) { top = w.boneIndex3; }
                            if (top != handIdx) continue;
                            Vector3 p = bind[handIdx].MultiplyPoint3x4(verts[v]);
                            radii.Add(Vector3.ProjectOnPlane(p, axisBind).magnitude * k);
                        }
                        handVerts = radii.Count;
                        if (handVerts > 0)
                        {
                            radii.Sort();
                            rHandMedian = radii[radii.Count / 2];
                            rHandMax = radii[radii.Count - 1];
                        }
                    }
                }
            }
            if (handVerts == 0 && rHandMedian < 0f)
                sb.AppendLine("[hand-mesh]   NOTE: zero vertices dominant-weighted to the LEFT HAND bone.");

            float palmOffset = (lMid1.position - lHand.position).magnitude * 0.5f;
            sb.AppendLine($"[hand-mesh]   haft: length {haftLenWorld:F4} m; bare-stick radius fraction " +
                          $"{_bareHaftRadiusFrac:F4} => rHaft {rHaft:F4} m ({rHaft * 100f:F1} cm; diameter " +
                          $"{rHaft * 200f:F1} cm) — bare haft spans u 0.00..{bareHaftTopU:F2}.");
            sb.AppendLine($"[hand-mesh]   hand: {handVerts} verts dominant-weighted to mixamorig:LeftHand; " +
                          $"cross-section radius about the wrist->knuckle axis MEDIAN {rHandMedian:F4} m " +
                          $"({rHandMedian * 100f:F1} cm), MAX {rHandMax:F4} m ({rHandMax * 100f:F1} cm).");
            sb.AppendLine($"[hand-mesh]   palm centre = midpoint(wrist, knuckle) => {palmOffset:F4} m " +
                          $"({palmOffset * 100f:F1} cm) IN FRONT OF THE WRIST BONE. This is why a wrist-anchored " +
                          "metric is not a palm-anchored one: pinning the WRIST to the axis would drive the haft " +
                          "through the back of the hand by that much.");
            if (rHaft > 0f && rHandMedian > 0f)
                sb.AppendLine($"[hand-mesh]   => TOUCH TOLERANCE rHand + rHaft = {rHandMedian + rHaft:F4} m " +
                              $"({(rHandMedian + rHaft) * 100f:F1} cm). Divide by the live shoulder width to get the " +
                              "cap in SW; the sweep below prints it at the measured mean SW.");
        }

        /// <summary>
        /// 86cay4282 round 4 — THE LEFT-ARM IK REACH ENVELOPE, and the post-solve palm-to-haft it actually achieves.
        ///
        /// The seat is unchanged (it is the RIGHT hand's grip and the Sponsor approved its head-driving-down read), so
        /// the haft sweeps through the swing and the question is entirely about the LEFT arm: for a pin at u_left, is
        /// the target within reach at EVERY judged frame, and where does the palm actually land once solved?
        ///
        /// Three things are reported per candidate u, and each one is a decision the fix would otherwise have to guess:
        ///   • reach: the worst-frame target distance as a fraction of full extension, and how many frames exceed the
        ///     solver's shell — i.e. where the DIAL's honest range ends. Beyond it the arm blends out rather than
        ///     snapping straight, so an over-reaching u is not a crash, it is a silently-inert dial position.
        ///   • the post-solve PALM-to-haft distance (metres + SW), through the PRODUCTION solver. If this is not ~0 at
        ///     a reachable u, the solve is wrong and no amount of dialling will fix it.
        ///   • the pole plane's conditioning: the clip elbow's perpendicular offset off the shoulder->target axis, and
        ///     how often it falls below the solver's threshold and needs the named fallback. That is the elbow-flip
        ///     guard chosen from evidence rather than from a hunch.
        ///
        /// The judged window starts at <c>EasedInNt</c>, matching the shipped gate, which scores only frames at seat
        /// weight >= 0.95: the first ~0.25 s is a deliberate hand-over where the tool is CORRECTLY still at the
        /// approved one-handed seat, and scoring it manufactures a failure that has nothing to do with the fix.
        /// </summary>
        private static void LeftArmIkSweep(StringBuilder sb, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform lFore, Transform lHand, Transform lMid1,
            Transform rArm, Transform rHand,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler, Vector3 armR, Vector3 armL,
            Vector3 wristR, Vector3 wristL, float bareHaftTopU)
        {
            sb.AppendLine("[left-ik]   --- LEFT-ARM TWO-BONE IK: REACH ENVELOPE + POST-SOLVE PALM (86cay4282 r4) ---");
            if (lFore == null || lMid1 == null)
            {
                sb.AppendLine("[left-ik]   ABORT — LeftForeArm=" + (lFore != null) + " LeftHandMiddle1=" +
                              (lMid1 != null) + ". No chain, no measurement; a fix must NOT be sized without one.");
                return;
            }

            const int N = 181;                       // ~0.03 s per sample on a 5.2 s clip
            const float EasedInNt = 0.08f;           // the gate's >=0.95 seat-weight window, in clip-normalised time
            const float ReachFalloff = 0.06f;        // 6 cm of over-reach to fully blend out (sized below, reported)
            var qR = Quaternion.Euler(armR);
            var qL = Quaternion.Euler(armL);
            var qWristR = Quaternion.Euler(wristR);
            var qWristL = Quaternion.Euler(wristL);
            Quaternion seatQ = Quaternion.Euler(seatEuler) * Quaternion.Euler(MovementCameraScene.HeldToolMineSeatEulerDelta);
            Vector3 seatPos = seatOffset + MovementCameraScene.HeldToolMineSeatOffsetDelta;

            sb.AppendLine($"[left-ik]   chain = mixamorig:LeftArm -> LeftForeArm -> PALM (midpoint of LeftHand and the " +
                          $"resolved knuckle '{lMid1.name}' — the point the haft must pass through, not the wrist bone).");
            sb.AppendLine($"[left-ik]   seat under test = the SHIPPED round-3 delta (offset " +
                          $"{MovementCameraScene.HeldToolMineSeatOffsetDelta:F4}, euler " +
                          $"{MovementCameraScene.HeldToolMineSeatEulerDelta:F1}) — UNCHANGED by this round; the IK is " +
                          "purely additive on the left arm, and the right hand's grip is out of scope.");
            sb.AppendLine($"[left-ik]   judged window nt >= {EasedInNt:F2} (the gate scores seat weight >= 0.95 only). " +
                          $"reachFalloff = {ReachFalloff:F2} m.");

            // Per-u accumulators. u runs to the measured bare-haft top: above it the palm is inside the head mass.
            float uTop = bareHaftTopU > 0f ? bareHaftTopU : 0.80f;
            int steps = 17;                                       // 0.00 .. uTop inclusive
            var rows = new List<string>();
            float swMean = 0f; int swN = 0;
            float aLenMean = 0f, bLenMean = 0f;
            Vector3 poleLocalSum = Vector3.zero;
            int bestUIdx = -1; float bestPalmMax = float.MaxValue;
            float reachableLo = float.NaN, reachableHi = float.NaN;

            for (int s = 0; s < steps; s++)
            {
                float u = uTop * s / (float)(steps - 1);
                float reachFracMax = 0f, palmMax = 0f, palmSum = 0f;
                float poleParMin = float.MaxValue;
                int overShell = 0, fallbackFrames = 0, unsolved = 0, n = 0;
                float swSum = 0f;

                for (int i = 0; i < N; i++)
                {
                    float nt = i / (float)(N - 1);
                    if (nt < EasedInNt) continue;
                    clip.SampleAnimation(model, nt * clip.length);
                    rArm.localRotation = rArm.localRotation * qR;              // order 50
                    lArm.localRotation = lArm.localRotation * qL;
                    rHand.localRotation = rHand.localRotation * qWristR;       // order 65
                    lHand.localRotation = lHand.localRotation * qWristL;
                    prop.root.position = rHand.position + rHand.rotation * seatPos;   // order 100
                    prop.root.rotation = rHand.rotation * seatQ;

                    float sw = (rArm.position - lArm.position).magnitude;
                    if (sw < 1e-5f) continue;
                    Vector3 gripW = prop.holder.TransformPoint(prop.gripLocal);
                    Vector3 headW = prop.holder.TransformPoint(prop.headLocal);
                    Vector3 target = Vector3.Lerp(gripW, headW, u);

                    Vector3 root = lArm.position, mid = lFore.position;
                    Vector3 palm = (lHand.position + lMid1.position) * 0.5f;
                    float aLen = (mid - root).magnitude, bLen = (palm - mid).magnitude;
                    float c = (target - root).magnitude;
                    float shell = (aLen + bLen) * TwoBoneIkSolver.StraightArmFraction;
                    reachFracMax = Mathf.Max(reachFracMax, c / (aLen + bLen));
                    if (c > shell) overShell++;

                    // pole conditioning: how far the CLIP's elbow stands off the shoulder->target axis.
                    Vector3 axis = (target - root).normalized;
                    float polePar = Vector3.ProjectOnPlane(mid - root, axis).magnitude;
                    poleParMin = Mathf.Min(poleParMin, polePar);

                    var res = TwoBoneIkSolver.Solve(root, lArm.rotation, mid, lFore.rotation, palm, target,
                                                    poleHint: mid,
                                                    poleFallbackDir: model.transform.rotation * Vector3.back,
                                                    reachFalloff: ReachFalloff);
                    if (!res.solved) { unsolved++; continue; }
                    if (res.poleFromFallback) fallbackFrames++;

                    // APPLY exactly as the runtime will (upper first, then lower), then RE-MEASURE off the live bones
                    // — never off the solver's own prediction.
                    lArm.rotation = res.upperRotation;
                    lFore.rotation = res.lowerRotation;
                    Vector3 palmAfter = (lHand.position + lMid1.position) * 0.5f;
                    float d = SegDist(palmAfter, gripW, headW);
                    palmMax = Mathf.Max(palmMax, d);
                    palmSum += d;
                    swSum += sw; n++;
                    if (s == 0) { aLenMean += aLen; bLenMean += bLen; poleLocalSum += Quaternion.Inverse(model.transform.rotation) * (mid - root).normalized; }
                }

                if (n == 0) { rows.Add($"[left-ik]   u {u:F2}: no valid samples"); continue; }
                float sw0 = swSum / n;
                if (s == 0) { swMean = sw0; swN = n; aLenMean /= n; bLenMean /= n; poleLocalSum /= n; }
                bool reachable = overShell == 0 && unsolved == 0;
                if (reachable)
                {
                    if (float.IsNaN(reachableLo)) reachableLo = u;
                    reachableHi = u;
                }
                if (reachable && palmMax < bestPalmMax) { bestPalmMax = palmMax; bestUIdx = s; }
                rows.Add($"[left-ik]   u {u:F2}: reach worst {reachFracMax:F3} of full extension, {overShell}/{n} " +
                         $"frames past the {TwoBoneIkSolver.StraightArmFraction:F2} shell{(reachable ? " (REACHABLE)" : "")} | " +
                         $"post-solve PALM->haft mean {palmSum / n:F4} m ({palmSum / n * 100f:F1} cm) MAX {palmMax:F4} m " +
                         $"({palmMax * 100f:F1} cm; {palmMax / sw0:F3} SW) | pole perp MIN {poleParMin:F4} m, " +
                         $"{fallbackFrames} fallback frames, {unsolved} unsolved");
            }

            sb.AppendLine($"[left-ik]   left arm: shoulder->elbow {aLenMean:F4} m ({aLenMean * 100f:F1} cm), " +
                          $"elbow->palm {bLenMean:F4} m ({bLenMean * 100f:F1} cm) => FULL EXTENSION " +
                          $"{aLenMean + bLenMean:F4} m ({(aLenMean + bLenMean) * 100f:F1} cm), usable shell " +
                          $"{(aLenMean + bLenMean) * TwoBoneIkSolver.StraightArmFraction:F4} m. Mean shoulder width " +
                          $"{swMean:F4} m over {swN} judged samples.");
            sb.AppendLine($"[left-ik]   measured pole direction (clip elbow off the shoulder, MODEL frame, mean) = " +
                          $"{poleLocalSum.normalized:F3} — the FALLBACK constant to bake, so a degenerate frame uses a " +
                          "measured direction rather than a guessed one.");
            foreach (var r in rows) sb.AppendLine(r);
            if (!float.IsNaN(reachableLo))
                sb.AppendLine($"[left-ik]   => REACHABLE u_left RANGE (all judged frames inside the shell): " +
                              $"{reachableLo:F2}..{reachableHi:F2} of the haft = " +
                              $"{reachableLo * (prop.holder.TransformPoint(prop.headLocal) - prop.holder.TransformPoint(prop.gripLocal)).magnitude * 100f:F0}" +
                              $"..{reachableHi * (prop.holder.TransformPoint(prop.headLocal) - prop.holder.TransformPoint(prop.gripLocal)).magnitude * 100f:F0} cm up an " +
                              $"{(prop.holder.TransformPoint(prop.headLocal) - prop.holder.TransformPoint(prop.gripLocal)).magnitude * 100f:F0} cm haft. " +
                              "THIS is the dial's honest range — a choked-up grip is now bounded by ARM REACH, not by " +
                              "the clip's hand spacing.");
            else
                sb.AppendLine("[left-ik]   => NO u is reachable at every judged frame. Report that, do not pick one.");
            if (bestUIdx >= 0)
                sb.AppendLine($"[left-ik]   best reachable u = {uTop * bestUIdx / (float)(steps - 1):F2} " +
                              $"(worst-frame palm->haft {bestPalmMax:F4} m = {bestPalmMax * 100f:F1} cm)");

            LeftArmReachableSpan(sb, clip, model, lArm, lFore, lHand, lMid1, rArm, rHand,
                                 prop, seatPos, seatQ, qR, qL, qWristR, qWristL, uTop, N, EasedInNt);
        }

        /// <summary>
        /// 86cay4282 round 4 — THE DECISIVE PASS, added AFTER the fixed-pin sweep above REFUTED the obvious design.
        ///
        /// The sweep found a pin at ANY fixed u_left is beyond the left arm's 54.0 cm full extension on ~64% of judged
        /// frames at best (worst-frame reach 1.18-1.49x extension across u 0.00..0.80), so a fixed pin plus a
        /// blend-out-on-over-reach would leave the IK INERT for most of the swing and the Sponsor would see the same
        /// defect. That is a design assumption dying to a measurement, which is the point of measuring first.
        ///
        /// So the real question is not "is u reachable" but "WHICH PART of the haft is reachable, per frame". This pass
        /// answers it directly: intersect the haft SEGMENT with the sphere of the arm's usable shell about the left
        /// shoulder and report the interval. If that interval is non-empty at (nearly) every frame, the honest design is
        /// a pin that tracks the Sponsor's PREFERRED u but is CLAMPED into the reachable interval each frame — the palm
        /// then sits ON the haft always, the elbow only extends as far as it must, and u stays a real dial whose
        /// ACHIEVED value is reported. If the interval is empty, no left-arm IK can satisfy the anchor against this seat
        /// and the right answer is to say so rather than ship a stretch.
        ///
        /// The clip's own tightest LEFT elbow is 90deg (measured above), so how far the solve EXTENDS that elbow is
        /// itself a judgement quantity — a near-straight arm is the ugly failure mode the brief names. It is reported
        /// per preferred-u alongside the palm distance, never left implicit.
        /// </summary>
        private static void LeftArmReachableSpan(StringBuilder sb, AnimationClip clip, GameObject model,
            Transform lArm, Transform lFore, Transform lHand, Transform lMid1, Transform rArm, Transform rHand,
            in PropRig prop, Vector3 seatPos, Quaternion seatQ,
            Quaternion qR, Quaternion qL, Quaternion qWristR, Quaternion qWristL,
            float uTop, int N, float easedInNt)
        {
            sb.AppendLine("[left-span]   --- WHICH PART OF THE HAFT IS REACHABLE, PER FRAME (86cay4282 r4) ---");
            sb.AppendLine("[left-span]   The fixed-pin sweep above is REFUTED: no single u sits inside the arm's shell at");
            sb.AppendLine("[left-span]   every frame. This intersects the haft SEGMENT with the shell SPHERE about the");
            sb.AppendLine("[left-span]   left shoulder instead, so a pin can be CLAMPED into what the arm can hold.");

            int n = 0, empty = 0;
            float closestWorst = 0f, closestBest = float.MaxValue;
            float spanLoMin = 9f, spanLoMax = -9f, spanHiMin = 9f, spanHiMax = -9f;
            float uAtClosestMin = 9f, uAtClosestMax = -9f;
            // THE PRODUCTION STRATEGY, swept over its two free parameters so the choice is priced, not guessed:
            //   preferred u  x  shell fraction (how straight the arm is allowed to go when the haft is out of reach).
            var prefs = new[] { 0.20f, 0.30f, 0.35f, 0.40f, 0.50f };
            var shells = new[] { 0.90f, 0.94f, 0.98f };
            int C = prefs.Length * shells.Length;
            var achLo = new float[C]; var achHi = new float[C];
            var palmMax = new float[C]; var palmSum = new float[C];
            var elbowMin = new float[C]; var elbowMax = new float[C]; var swSum = new float[C];
            var fellBack = new int[C]; var uSum = new float[C];
            // 86cay4282 round 4 — the PER-FRAME ELBOW STEP and the pole's own conditioning, on the REAL clip. The
            // EditMode continuity test found that a pole nearly parallel to the chain axis amplifies target motion into
            // elbow motion ~5x; the production idiom (pole = the clip's own elbow) should de-amplify instead, and that
            // claim has to be MEASURED on the shipped clip rather than argued from the algebra.
            var elbowStepMax = new float[C]; var prevElbow = new Vector3[C]; var haveElbow = new bool[C];
            float polePerpMin = float.MaxValue, poleAmpMax = 0f;
            for (int p = 0; p < C; p++) { achLo[p] = 9f; achHi[p] = -9f; elbowMin[p] = 999f; elbowMax[p] = -999f; }

            for (int i = 0; i < N; i++)
            {
                float nt = i / (float)(N - 1);
                if (nt < easedInNt) continue;

                clip.SampleAnimation(model, nt * clip.length);
                rArm.localRotation = rArm.localRotation * qR;
                lArm.localRotation = lArm.localRotation * qL;
                rHand.localRotation = rHand.localRotation * qWristR;
                lHand.localRotation = lHand.localRotation * qWristL;
                prop.root.position = rHand.position + rHand.rotation * seatPos;
                prop.root.rotation = rHand.rotation * seatQ;

                float sw = (rArm.position - lArm.position).magnitude;
                if (sw < 1e-5f) continue;
                Vector3 gripW = prop.holder.TransformPoint(prop.gripLocal);
                Vector3 headW = prop.holder.TransformPoint(prop.headLocal);
                Vector3 S = lArm.position;
                float aLen = (lFore.position - S).magnitude;
                float bLen = ((lHand.position + lMid1.position) * 0.5f - lFore.position).magnitude;
                float R = (aLen + bLen) * TwoBoneIkSolver.StraightArmFraction;

                // Closest approach of the haft SEGMENT to the shoulder, and where along it that lands. THIS is the one
                // number that decides whether any left-arm IK can work against this seat at all.
                float dClosest = SegDistU(S, gripW, headW, out float uClosest);
                closestWorst = Mathf.Max(closestWorst, dClosest);
                closestBest = Mathf.Min(closestBest, dClosest);
                uAtClosestMin = Mathf.Min(uAtClosestMin, uClosest);
                uAtClosestMax = Mathf.Max(uAtClosestMax, uClosest);

                n++;
                for (int si = 0; si < shells.Length; si++)
                {
                    float Rs = (aLen + bLen) * shells[si];
                    // Segment-sphere span, restricted to the BARE haft (a palm above uTop is inside the head mass).
                    bool has = SegmentSphereSpan(gripW, headW, S, Rs, out float t0, out float t1);
                    t0 = Mathf.Max(t0, 0f); t1 = Mathf.Min(t1, uTop);
                    bool spanEmpty = !has || t1 < t0;
                    if (si == shells.Length - 1)   // the span statistics are reported for the widest shell
                    {
                        if (spanEmpty) empty++;
                        else
                        {
                            spanLoMin = Mathf.Min(spanLoMin, t0); spanLoMax = Mathf.Max(spanLoMax, t0);
                            spanHiMin = Mathf.Min(spanHiMin, t1); spanHiMax = Mathf.Max(spanHiMax, t1);
                        }
                    }

                    for (int p = 0; p < prefs.Length; p++)
                    {
                        int k = si * prefs.Length + p;
                        // THE STRATEGY. Span non-empty -> pin at the reachable point NEAREST the Sponsor's preferred u
                        // (palm lands exactly ON the haft, elbow only as extended as it must be). Span EMPTY -> pin at
                        // the CLOSEST point of the haft and let the solver's shell clamp hold the arm aimed at it. The
                        // fallback is the load-bearing choice: blending the IK OUT there would hand the frame back to
                        // the clip pose (the 20-28 cm defect) on roughly half the swing, which is worse than a reach.
                        float u = spanEmpty ? uClosest : Mathf.Clamp(prefs[p], t0, t1);
                        if (spanEmpty) fellBack[k]++;
                        achLo[k] = Mathf.Min(achLo[k], u); achHi[k] = Mathf.Max(achHi[k], u); uSum[k] += u;
                        Vector3 target = Vector3.Lerp(gripW, headW, u);
                        Vector3 mid = lFore.position, palm = (lHand.position + lMid1.position) * 0.5f;
                        Quaternion upper0 = lArm.rotation, lower0 = lFore.rotation;
                        var res = TwoBoneIkSolver.Solve(S, upper0, mid, lower0, palm, target,
                                                        poleHint: mid,
                                                        poleFallbackDir: model.transform.rotation * Vector3.back,
                                                        reachFalloff: 0.30f,
                                                        straightArmFraction: shells[si]);
                        if (!res.solved) continue;
                        // POLE CONDITIONING, on the production idiom (pole = the clip's own elbow). The amplification of
                        // axis rotation into plane rotation is (parallel / perpendicular) of the pole about the axis;
                        // <= 1 means the plane is DE-sensitised, which is the flip-free regime.
                        if (si == shells.Length - 1 && p == 0)
                        {
                            Vector3 ax = (target - S).normalized;
                            Vector3 rel = mid - S;
                            float perp = Vector3.ProjectOnPlane(rel, ax).magnitude;
                            float par = Mathf.Abs(Vector3.Dot(rel, ax));
                            polePerpMin = Mathf.Min(polePerpMin, perp);
                            if (perp > 1e-6f) poleAmpMax = Mathf.Max(poleAmpMax, par / perp);
                        }
                        lArm.rotation = res.upperRotation;
                        lFore.rotation = res.lowerRotation;
                        Vector3 palmAfter = (lHand.position + lMid1.position) * 0.5f;
                        float d = SegDist(palmAfter, gripW, headW);
                        palmMax[k] = Mathf.Max(palmMax[k], d); palmSum[k] += d; swSum[k] += sw;
                        // the ELBOW INTERIOR angle after the solve — the "never snap the arm straight" read.
                        float e = Vector3.Angle(S - lFore.position, palmAfter - lFore.position);
                        elbowMin[k] = Mathf.Min(elbowMin[k], e);
                        elbowMax[k] = Mathf.Max(elbowMax[k], e);
                        // …and the FRAME-TO-FRAME elbow displacement, the quantity a flip actually renders as.
                        Vector3 elbowNow = lFore.position;
                        if (haveElbow[k]) elbowStepMax[k] = Mathf.Max(elbowStepMax[k], (elbowNow - prevElbow[k]).magnitude);
                        prevElbow[k] = elbowNow; haveElbow[k] = true;
                        lArm.rotation = upper0; lFore.rotation = lower0;   // restore before the next candidate
                    }
                }
            }

            if (n == 0) { sb.AppendLine("[left-span]   no valid samples"); return; }
            sb.AppendLine($"[left-span]   CLOSEST haft point to the LEFT SHOULDER over {n} judged frames: best " +
                          $"{closestBest:F4} m ({closestBest * 100f:F1} cm), WORST {closestWorst:F4} m " +
                          $"({closestWorst * 100f:F1} cm); it lands at u {uAtClosestMin:F2}..{uAtClosestMax:F2}. " +
                          "Against the arm's usable shell printed above: WORST < shell means SOME part of the haft is " +
                          "always holdable, which is what makes a clamped pin viable.");
            sb.AppendLine($"[left-span]   frames with an EMPTY reachable span at the 0.98 shell: {empty}/{n} — on those " +
                          "the WHOLE haft is beyond the arm, so the strategy pins the CLOSEST haft point and the shell " +
                          "clamp holds the arm aimed at it. Reachable interval over the bare haft: lo " +
                          $"{spanLoMin:F2}..{spanLoMax:F2}, hi {spanHiMin:F2}..{spanHiMax:F2}.");
            sb.AppendLine("[left-span]   TRADE CURVE — shell fraction (how straight the arm may go) vs how close the palm");
            sb.AppendLine("[left-span]   gets. 'elbow' is the INTERIOR angle range after the solve; the clip's own");
            sb.AppendLine("[left-span]   tightest is 90deg and 180 would be a locked/straight arm.");
            for (int si = 0; si < shells.Length; si++)
                for (int p = 0; p < prefs.Length; p++)
                {
                    int k = si * prefs.Length + p;
                    if (n <= 0 || swSum[k] <= 0f) { sb.AppendLine($"[left-span]   shell {shells[si]:F2} pref u {prefs[p]:F2}: nothing scored"); continue; }
                    float swAvg = swSum[k] / n;
                    sb.AppendLine($"[left-span]   shell {shells[si]:F2} pref u {prefs[p]:F2}: ACHIEVED u " +
                                  $"{achLo[k]:F2}..{achHi[k]:F2} MEAN {uSum[k] / n:F3} ({fellBack[k]}/{n} frames on the closest-point " +
                                  $"fallback) | palm->haft mean {palmSum[k] / n:F4} m ({palmSum[k] / n * 100f:F1} cm) " +
                                  $"MAX {palmMax[k]:F4} m ({palmMax[k] * 100f:F1} cm; {palmMax[k] / swAvg:F3} SW) | " +
                                  $"elbow {elbowMin[k]:F0}..{elbowMax[k]:F0}deg | worst frame-to-frame elbow step " +
                                  $"{elbowStepMax[k] * 100f:F2} cm");
                }
            sb.AppendLine($"[left-span]   POLE CONDITIONING on the production idiom (pole = the clip's OWN elbow): " +
                          $"perpendicular offset off the shoulder->target axis MIN {polePerpMin:F4} m " +
                          $"({polePerpMin * 100f:F1} cm); worst plane-sensitivity amplification (parallel/perp) " +
                          $"{poleAmpMax:F2}. <= ~1 means axis motion is DE-amplified into plane motion, i.e. the " +
                          "flip-free regime. A FIXED world-space pole nearly parallel to the axis measures ~5x " +
                          "amplification instead (TwoBoneIkSolverTests), which is why the fallback is a last resort.");
        }

        /// <summary>Distance from <paramref name="p"/> to the segment a..b, also reporting the CLAMPED position along it
        /// — the "which part of the haft is nearest" read.</summary>
        private static float SegDistU(Vector3 p, Vector3 a, Vector3 b, out float u)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) { u = 0f; return (p - a).magnitude; }
            u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return (p - (a + ab * u)).magnitude;
        }

        /// <summary>
        /// The sub-interval [t0,t1] of the segment a..b (parameterised 0..1) lying INSIDE the sphere of radius
        /// <paramref name="r"/> about <paramref name="c"/>. False when the segment misses the sphere entirely.
        /// Plain quadratic on |a + t(b−a) − c|² = r².
        /// </summary>
        private static bool SegmentSphereSpan(Vector3 a, Vector3 b, Vector3 c, float r, out float t0, out float t1)
        {
            t0 = t1 = 0f;
            Vector3 d = b - a, f = a - c;
            float A = d.sqrMagnitude;
            if (A < 1e-12f) { t1 = 1f; return f.magnitude <= r; }
            float B = 2f * Vector3.Dot(f, d);
            float C = f.sqrMagnitude - r * r;
            float disc = B * B - 4f * A * C;
            if (disc < 0f) return false;
            float sq = Mathf.Sqrt(disc);
            t0 = (-B - sq) / (2f * A);
            t1 = (-B + sq) / (2f * A);
            if (t1 < 0f || t0 > 1f) return false;
            t0 = Mathf.Max(t0, 0f); t1 = Mathf.Min(t1, 1f);
            return true;
        }

        private static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return (p - a).magnitude;
            float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return (p - (a + ab * u)).magnitude;
        }

        /// <summary>Re-measure the whole pickaxe clip with a candidate MINE seat delta applied, reporting the two
        /// quantities the two-hand read is DEFINED by — each hand's distance to the haft LINE (shoulder-widths) and
        /// where along the haft it lands — plus the tool head's torso-frame landing at the strike frame (the
        /// orientation discriminator).</summary>
        private static void Fitted(StringBuilder sb, string tag, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm, Transform lHand, Transform rHand,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler, Vector3 armR, Vector3 armL,
            Vector3 wristR, Vector3 wristL, Vector3 posDelta, Vector3 eulerDelta, float haftLen)
        {
            // DENSE — see the note in MineSeatFit: a coarse grid steps over the transient the shipped 60 fps gate
            // catches, so a fit scored on it over-promises.
            const int N = 361;
            var qR = Quaternion.Euler(armR);
            var qL = Quaternion.Euler(armL);
            Quaternion seatQ = Quaternion.Euler(seatEuler) * Quaternion.Euler(eulerDelta);

            float lMax = 0f, rMax = 0f, lSum = 0f, rSum = 0f;
            float ulMin = 9f, ulMax = -9f, urMin = 9f, urMax = -9f;
            float angMax = 0f;
            // SELF-INTERSECTION GUARD. Aligning the haft with the hand line puts a long butt end SOMEWHERE — and
            // a fit that solves the grip read by driving the haft THROUGH the torso or the head has traded one
            // defect for a worse one. Same discipline as the de-grip sweep's lTorsoClearMin: measure the cost of
            // the candidate, don't just score its benefit.
            float torsoClearMin = float.MaxValue, headClearMin = float.MaxValue;
            // the head's torso-frame landing at the DEEPEST-fold frame (the strike) — the orientation read.
            float deepest = -1f; Vector3 headAtStrike = Vector3.zero;
            int n = 0;
            for (int i = 0; i < N; i++)
            {
                float nt = i / (float)(N - 1);
                clip.SampleAnimation(model, nt * clip.length);
                rArm.localRotation = rArm.localRotation * qR;
                lArm.localRotation = lArm.localRotation * qL;
                rHand.localRotation = rHand.localRotation * Quaternion.Euler(wristR);   // order 65
                lHand.localRotation = lHand.localRotation * Quaternion.Euler(wristL);
                prop.root.position = rHand.position + rHand.rotation * (seatOffset + posDelta);
                prop.root.rotation = rHand.rotation * seatQ;

                if (!TorsoFrame(hips, head, lArm, rArm, out Vector3 rightAxis, out Vector3 up,
                                out Vector3 fwdAxis, out Vector3 chest, out float sw)) continue;
                Vector3 gripW = prop.holder.TransformPoint(prop.gripLocal);
                Vector3 headW = prop.holder.TransformPoint(prop.headLocal);
                Vector3 seg = headW - gripW;
                if (seg.sqrMagnitude < 1e-8f) continue;

                float ul = Vector3.Dot(lHand.position - gripW, seg) / seg.sqrMagnitude;
                float ur = Vector3.Dot(rHand.position - gripW, seg) / seg.sqrMagnitude;
                float dl = (lHand.position - (gripW + seg * Mathf.Clamp01(ul))).magnitude / sw;
                float dr = (rHand.position - (gripW + seg * Mathf.Clamp01(ur))).magnitude / sw;
                float ang = Vector3.Angle(seg, rHand.position - lHand.position);
                if (ang > 90f) ang = 180f - ang;

                lMax = Mathf.Max(lMax, dl); rMax = Mathf.Max(rMax, dr);
                lSum += dl; rSum += dr; n++;
                ulMin = Mathf.Min(ulMin, ul); ulMax = Mathf.Max(ulMax, ul);
                urMin = Mathf.Min(urMin, ur); urMax = Mathf.Max(urMax, ur);
                angMax = Mathf.Max(angMax, ang);

                // Sample the haft and measure its closest approach to the torso AXIS (hips->head) and to the head
                // bone, both in shoulder-widths.
                for (int k = 0; k <= 8; k++)
                {
                    Vector3 pt = gripW + seg * (k / 8f);
                    torsoClearMin = Mathf.Min(torsoClearMin, SegDist(pt, hips.position, head.position) / sw);
                    headClearMin = Mathf.Min(headClearMin, (pt - head.position).magnitude / sw);
                }

                float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                if (tilt > deepest)
                {
                    deepest = tilt;
                    Vector3 dh = (headW - chest) / sw;
                    headAtStrike = new Vector3(Vector3.Dot(dh, rightAxis), Vector3.Dot(dh, fwdAxis),
                                               Vector3.Dot(dh, up));
                }
            }
            if (n == 0) { sb.AppendLine($"[seat-fit]     {tag}: no valid samples"); return; }
            sb.AppendLine($"[seat-fit]     {tag}: lHaft mean {lSum / n:F3} MAX {lMax:F3} SW | rHaft mean " +
                          $"{rSum / n:F3} MAX {rMax:F3} SW | u_left {ulMin:F2}..{ulMax:F2} u_right " +
                          $"{urMin:F2}..{urMax:F2} | toolVsHandLine MAX {angMax:F1}deg | head at the deepest fold " +
                          $"(tilt {deepest:F0}deg) out={headAtStrike.x:F2} fwd={headAtStrike.y:F2} up={headAtStrike.z:F2}");
            sb.AppendLine($"[seat-fit]     {tag}: CLEARANCE haft-to-torso-axis MIN {torsoClearMin:F3} SW | " +
                          $"haft-to-head MIN {headClearMin:F3} SW  (small = the haft passes through the body)");
        }

        private static Vector3 NormEuler(Vector3 e)
        {
            return new Vector3(NormAngle(e.x), NormAngle(e.y), NormAngle(e.z));
        }

        private static float NormAngle(float a) { a %= 360f; if (a > 180f) a -= 360f; return a; }

        private static bool TorsoFrame(Transform hips, Transform head, Transform lArm, Transform rArm,
            out Vector3 rightAxis, out Vector3 up, out Vector3 fwdAxis, out Vector3 chest, out float sw)
        {
            Vector3 shoulder = rArm.position - lArm.position;
            sw = shoulder.magnitude;
            rightAxis = up = fwdAxis = chest = Vector3.zero;
            if (sw < 1e-5f) return false;
            rightAxis = shoulder / sw;
            up = (head.position - hips.position).normalized;
            fwdAxis = Vector3.Cross(up, rightAxis).normalized;
            chest = (lArm.position + rArm.position) * 0.5f;
            return true;
        }

        private static void PropSeatPass(StringBuilder sb, string label, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm,
            Transform lHand, Transform rHand, Transform rFore,
            in PropRig prop, Vector3 seatOffset, Vector3 seatEuler,
            Vector3 armR, Vector3 armL, Vector3 wristR, Vector3 wristL, bool applyArmPose, bool verbose)
        {
            const int N = 31;
            var qR = Quaternion.Euler(armR);
            var qL = Quaternion.Euler(armL);
            var qWristR = Quaternion.Euler(wristR);
            var qWristL = Quaternion.Euler(wristL);

            float gripMin = float.MaxValue, gripMax = float.MinValue;
            float foreMin = 999f, foreMax = -999f;
            float lHaftMin = float.MaxValue, lHaftMax = float.MinValue;
            float lrMin = float.MaxValue, lrMax = float.MinValue;
            float hlMin = 999f, hlMax = -999f;
            // TORSO-FRAME extremes. These are the ONLY metrics in this pass that an upper-arm offset can move:
            // gripHand / axisSpreadInHand / toolFore are all invariant under a RightArm rotation BY CONSTRUCTION
            // (the hand, forearm and prop rotate together), so an A/B that reports only those is BLIND to the very
            // thing it is A/B-ing. Track the body-frame head position so the A/B can actually see the offset.
            float hoMin = 999f, hoMax = -999f, hfMin = 999f, hfMax = -999f, huMin = 999f, huMax = -999f;
            Vector3 axisInHand0 = Vector3.zero; float axisSpread = 0f;
            float headTravel = 0f; Vector3 headPrevW = Vector3.zero;
            string tag = applyArmPose ? "SHIPPED (arm offset ON)" : "CONTROL (arm offset = identity)";

            if (verbose)
            {
                sb.AppendLine($"[prop-diag]   {tag} — per-sample:");
                sb.AppendLine("[prop-diag]     t     gripHand toolFore  headOut headFwd  headUp   lHaft   u    lRHand  tVsHL");
            }

            for (int i = 0; i < N; i++)
            {
                float nt = i / (float)(N - 1);
                clip.SampleAnimation(model, nt * clip.length);

                // Replicate CastawayArmPose.LateUpdate (order 50) — the additive right-multiply on the UPPER arms,
                // composed on the clip pose the Animator just wrote. run-lower is omitted deliberately: since
                // 884c611 its weight is released off the locomotion lane, so it is 0 through an attack swing.
                if (applyArmPose)
                {
                    rArm.localRotation = rArm.localRotation * qR;
                    lArm.localRotation = lArm.localRotation * qL;
                }

                // Replicate CastawayHandPose.LateUpdate (order 65) — the WRIST offsets, composed on the HAND bones
                // between the arm pose and the seat. Load-bearing: the seat below reads rHand.ROTATION.
                rHand.localRotation = rHand.localRotation * qWristR;
                lHand.localRotation = lHand.localRotation * qWristL;

                // Replicate HeldToolRig.LateUpdate (order 100) on the already-posed hand.
                prop.root.position = rHand.position + rHand.rotation * seatOffset;
                prop.root.rotation = rHand.rotation * Quaternion.Euler(seatEuler);

                Vector3 shoulder = rArm.position - lArm.position;
                float sw = shoulder.magnitude;
                if (sw < 1e-5f) continue;
                Vector3 rightAxis = shoulder / sw;
                Vector3 up = (head.position - hips.position).normalized;
                Vector3 fwdAxis = Vector3.Cross(up, rightAxis).normalized;
                Vector3 chest = (lArm.position + rArm.position) * 0.5f;

                Vector3 gripW = prop.holder.TransformPoint(prop.gripLocal);
                Vector3 headW = prop.holder.TransformPoint(prop.headLocal);
                Vector3 axisW = (headW - gripW);
                float toolLen = axisW.magnitude;
                if (toolLen < 1e-6f) continue;
                axisW /= toolLen;

                float gripHand = (gripW - rHand.position).magnitude / sw;
                float toolFore = Vector3.Angle(axisW, rHand.position - rFore.position);

                Vector3 dh = (headW - chest) / sw;
                float headOut = Vector3.Dot(dh, rightAxis), headFwd = Vector3.Dot(dh, fwdAxis), headUp = Vector3.Dot(dh, up);

                // LEFT hand vs the haft LINE (segment grip->head), + where along it the closest point falls.
                Vector3 gh = headW - gripW;
                float u = Mathf.Clamp01(Vector3.Dot(lHand.position - gripW, gh) / gh.sqrMagnitude);
                float lHaft = (lHand.position - (gripW + gh * u)).magnitude / sw;
                Vector3 handLine = rHand.position - lHand.position;
                float lr = handLine.magnitude / sw;
                // The IMPLIED HAFT. A two-handed clip poses both hands on one shaft, so the hand-to-hand line is the
                // haft the viewer reads; the angle between it and the real tool axis is the disagreement the eye sees.
                // Un-oriented (fold to 0..90) — a haft read is a LINE, not an arrow.
                float toolVsHandLine = Vector3.Angle(axisW, handLine);
                if (toolVsHandLine > 90f) toolVsHandLine = 180f - toolVsHandLine;

                // The RIGIDITY control on the rotation channel: the tool axis expressed in the hand's own frame.
                Vector3 axisInHand = Quaternion.Inverse(rHand.rotation) * axisW;
                if (i == 0) { axisInHand0 = axisInHand; headPrevW = headW; }
                else
                {
                    axisSpread = Mathf.Max(axisSpread, Vector3.Angle(axisInHand, axisInHand0));
                    headTravel += (headW - headPrevW).magnitude / sw;
                    headPrevW = headW;
                }

                gripMin = Mathf.Min(gripMin, gripHand); gripMax = Mathf.Max(gripMax, gripHand);
                foreMin = Mathf.Min(foreMin, toolFore); foreMax = Mathf.Max(foreMax, toolFore);
                lHaftMin = Mathf.Min(lHaftMin, lHaft); lHaftMax = Mathf.Max(lHaftMax, lHaft);
                lrMin = Mathf.Min(lrMin, lr); lrMax = Mathf.Max(lrMax, lr);
                hlMin = Mathf.Min(hlMin, toolVsHandLine); hlMax = Mathf.Max(hlMax, toolVsHandLine);
                hoMin = Mathf.Min(hoMin, headOut); hoMax = Mathf.Max(hoMax, headOut);
                hfMin = Mathf.Min(hfMin, headFwd); hfMax = Mathf.Max(hfMax, headFwd);
                huMin = Mathf.Min(huMin, headUp); huMax = Mathf.Max(huMax, headUp);

                if (verbose && i % 2 == 0)
                    sb.AppendLine($"[prop-diag]     {nt:F2}  {gripHand,7:F3} {toolFore,8:F1}  {headOut,7:F2} " +
                                  $"{headFwd,7:F2} {headUp,7:F2} {lHaft,7:F2} {u,5:F2} {lr,7:F2} {toolVsHandLine,7:F1}");
            }

            sb.AppendLine($"[prop-diag]   {label} {tag}: " +
                          $"gripHand {gripMin:F4}..{gripMax:F4} (range {gripMax - gripMin:F4}) | " +
                          $"axisSpreadInHand {axisSpread:F3}deg | toolFore {foreMin:F1}..{foreMax:F1} " +
                          $"(range {foreMax - foreMin:F1}deg) | lHaft {lHaftMin:F2}..{lHaftMax:F2} | " +
                          $"lRHand {lrMin:F2}..{lrMax:F2} (range {lrMax - lrMin:F2}) | " +
                          $"toolVsHandLine {hlMin:F1}..{hlMax:F1}deg | headPathLen {headTravel:F2}");
            sb.AppendLine($"[prop-diag]   {label} {tag} TORSO-FRAME head: out {hoMin:F2}..{hoMax:F2} " +
                          $"fwd {hfMin:F2}..{hfMax:F2} up {huMin:F2}..{huMax:F2}   " +
                          "(the ONLY channel an upper-arm offset can move — the A/B discriminator)");
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
