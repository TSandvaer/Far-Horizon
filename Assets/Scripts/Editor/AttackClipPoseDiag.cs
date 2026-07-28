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
                            shippedPickaxe, shippedAxe);

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
            rig.note = $"mesh={mesh.name} longAxis={"XYZ"[ax]} len={b.size[ax]:F3} " +
                       $"endA={lo:F3}(|{lo.magnitude:F3}|) endB={hi:F3}(|{hi.magnitude:F3}|) -> grip=end" +
                       (loIsGrip ? "A" : "B");
            return true;
        }

        private static void PropSeatSection(StringBuilder sb, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform rArm,
            Transform lHand, Transform rHand, Transform rFore,
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
                             prop, seatOffset, seatEuler, armR, armL, applyArmPose: true, verbose: true);
                PropSeatPass(sb, label, clip, model, hips, head, lArm, rArm, lHand, rHand, rFore,
                             prop, seatOffset, seatEuler, armR, armL, applyArmPose: false, verbose: false);

                // The de-grip SIZING pass runs only on the two-handed suspect.
                if (label == "pickaxe")
                    DeGripSweep(sb, clip, model, hips, head, lArm, rArm, lHand, rHand, rFore,
                                prop, seatOffset, seatEuler, armR, armL);

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
            Vector3 armR, Vector3 armL, bool applyArmPose, bool verbose)
        {
            const int N = 31;
            var qR = Quaternion.Euler(armR);
            var qL = Quaternion.Euler(armL);

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
