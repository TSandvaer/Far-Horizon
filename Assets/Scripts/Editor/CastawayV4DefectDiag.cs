using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// DIAGNOSTIC instrument (ticket 86catvb6u Sponsor defect round) for the two v4-activation defects the
    /// Sponsor saw on the ecd4ba1 dial build: (1) PIGEON-TOED WALK (both foot tips angle inward), (2) MANGLED
    /// right hand/thumb in the F9 closeup. Measures the LIVE skeleton — NOT guesses — per the sneak-jerk
    /// precedent (procedural-animation-verbs.md "measure the LIVE Animator skeleton").
    ///
    /// Run headless:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CastawayV4DefectDiag.Run
    /// </summary>
    public static class CastawayV4DefectDiag
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[v4-diag] ===== CASTAWAY v4 DEFECT DIAGNOSTIC (86catvb6u) =====");

            var walk = LoadClip(CharacterAssetGen.WalkFbxPath, CharacterAssetGen.WalkClip);
            sb.AppendLine($"[v4-diag] walk clip '{CharacterAssetGen.WalkClip}' loaded: {walk != null}");
            if (walk != null) ReportClipFootCurves(sb, "walk", walk);
            var idle = LoadClip(CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip);
            sb.AppendLine($"[v4-diag] idle clip '{CharacterAssetGen.BreathingIdleClip}' loaded: {idle != null}");
            if (idle != null) ReportClipFootCurves(sb, "idle", idle);

            // ---- DEFECT 1: foot/toe yaw, v3 vs v4, at BIND and at a WALK-sampled frame ----
            foreach (var (label, path) in new[] {
                ("v3", CharacterAssetGen.V3RiggedFbxPath), ("v4", CharacterAssetGen.V4RiggedFbxPath) })
            {
                var go = Instantiate(path);
                if (go == null) { sb.AppendLine($"[v4-diag] {label} FBX not found at {path}"); continue; }
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;

                sb.AppendLine($"[v4-diag] --- {label} FEET (bind pose) ---");
                LogFeet(sb, label + " BIND", go.transform);

                if (walk != null)
                {
                    walk.SampleAnimation(go, walk.length * 0.35f);
                    sb.AppendLine($"[v4-diag] --- {label} FEET (walk t=0.35) ---");
                    LogFeet(sb, label + " WALK", go.transform);
                }
                ReportCurlAxis(sb, label, go.transform);
                if (label == "v4") ReportRightHand(sb, go.transform);
                Object.DestroyImmediate(go);
            }

            // ---- DEFECT 1 DEFINITIVE: tick the LIVE Animator (the sneak-jerk precedent — SampleAnimation is
            // blind to what the player actually sees; only the real Animator + controller reveals it). ----
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetGen.ControllerPath);
            sb.AppendLine($"[v4-diag] controller loaded: {ctrl != null}");
            foreach (var (label, path) in new[] {
                ("v3", CharacterAssetGen.V3RiggedFbxPath), ("v4", CharacterAssetGen.V4RiggedFbxPath) })
                LiveWalkTick(sb, label, path, ctrl);

            // ---- DEFECT 2 (revised): RIGHT hand reads BLACK/segmented at the thumb WHEN GRIPPING; LEFT (never
            // curled) is clean. Bake the skinned mesh at REST vs GRIPPED + count INWARD-facing normals (a normal
            // pointing back at the hand centroid renders dark/black under URP/Lit). If gripping spikes the RIGHT
            // hand's inward count vs its rest AND vs the left, the curl FOLDS/INVERTS v4's thumb geometry. ----
            ReportHandGrip(sb, CharacterAssetGen.V4RiggedFbxPath);

            // ---- DEFECT 3 (core): the RIGHT hand is ROLLED (palm-back) vs the LEFT, unarmed, no F9. Suspect the
            // v3-dialed CastawayArmPose right-arm offset (-4,-50,-3) over-rolling v4's differently-framed arm bone
            // (left is -5,22,0). Idle-tick the LIVE Animator, apply candidate right-arm eulers, and measure the
            // right-hand rotation vs the MIRRORED left-hand (X-plane) — the candidate that minimizes the mismatch
            // is the v4 symmetric default. Validates the mirror on the arm-pose-OFF (clip-only) idle pose. ----
            ReportArmRoll(sb, CharacterAssetGen.V4RiggedFbxPath, ctrl);

            // ---- DEFECT 3 (round-5, DIRECT-KNOB): the mirror-left ARM euler did NOT fix the right hand — the
            // auto-rig gave the right HAND BONE a differently-ROLLED bind frame than the left, so a mirrored
            // upper-arm OFFSET can't mirror the hand RESULT. Measure the L/R hand-BONE bind frames + derive the
            // right-WRIST correction (bone-local) that makes the rendered right hand == mirror(left) at idle. ----
            ReportWristCorrection(sb, CharacterAssetGen.V4RiggedFbxPath, ctrl);

            // DEFECT 3 ROUND-8 (the Mixamo RE-RIG) — the Sponsor sees BOTH arms twisted at the round-7 defaults
            // (arm eulers (-5,±22,0), wrist 0); via the F9 WRIST dial he reached a CORRECT right arm at
            // CastawayV4RightWristEuler=(10,-120,-20). Root-cause the both-arms twist on the LIVE re-rig skeleton +
            // derive the LEFT-hand mirror correction anchored on the Sponsor-correct right + dump the thumb bones.
            ReportRound8(sb, CharacterAssetGen.V4RiggedFbxPath, ctrl);

            // DEFECT 4 ROUND-9 (the SKIN-WEIGHT layer — the one rung of the layer-elimination ladder
            // (clip → live pose → bind frames → WEIGHTS → geometry) never measured on the RE-RIG).
            // Sponsor on the dial-7 build: "its not a fist, its a block" / "its a block with a thumb" /
            // right thumb when dialed "moves a little / hard to tell", while a logged sweep drove
            // RightThumbEuler X=1448, Y=90→130 with no visible form change. That combination — huge
            // commanded rotation, barely-perceptible deformation — is the signature of WEAK PARTIAL
            // weights (thumb verts mostly bound to the palm/hand bone, small residual thumb-chain
            // influence), NOT zero weights (would be perfectly frozen) and NOT correct weights.
            // Hence the probe measures MAGNITUDE, not presence: identical test angle per side, then
            // report moved-vert COUNT + MEAN + MAX displacement in mm. A binary moved/didn't check
            // would read "both moved" and wrongly refute the hypothesis.
            ReportRound9(sb, CharacterAssetGen.V4RiggedFbxPath);

            sb.AppendLine("[v4-diag] ===== END =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void LiveWalkTick(StringBuilder sb, string label, string path, RuntimeAnimatorController ctrl)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null || ctrl == null) { sb.AppendLine($"[v4-diag] {label} LIVE: fbx/ctrl missing"); return; }
            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) avatar = a;
            var go = Object.Instantiate(fbx);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            anim.Play("Locomotion", 0, 0.4f);
            anim.SetBool("Moving", true);
            anim.SetBool("Grounded", true);
            anim.SetFloat("Speed", CharacterAssetGen.WalkBlendSpeed);
            anim.SetFloat("LocoSpeedMul", 1f);
            for (int i = 0; i < 20; i++) anim.Update(0.1f);
            sb.AppendLine($"[v4-diag] --- {label} FEET (LIVE Animator, Walk state) ---");
            LogFeet(sb, label + " LIVE", go.transform);
            Object.DestroyImmediate(go);
        }

        // ---- DEFECT 1 helpers ----
        // Signed horizontal yaw (deg) of the foot-pointing vector (toeBase - foot) vs hips-forward. A foot that
        // toes INWARD yaws toward the body centerline: left foot yaws NEGATIVE-ish, right foot POSITIVE-ish (or
        // both converge). Position-based (toe-minus-ankle) so NO bone-axis assumption is needed.
        private static void LogFeet(StringBuilder sb, string label, Transform root)
        {
            var hips = FindBone(root, "hips");
            Vector3 fwd = hips != null ? Horiz(hips.forward) : Vector3.forward;
            // Mixamo hips 'forward' can be any local axis; use the average foot-pointing as a body-forward proxy too.
            LogFoot(sb, label, root, "leftfoot", "lefttoebase", fwd, +1);
            LogFoot(sb, label, root, "rightfoot", "righttoebase", fwd, -1);
        }

        private static void LogFoot(StringBuilder sb, string label, Transform root, string footTok, string toeTok,
                                    Vector3 bodyFwd, int side)
        {
            var foot = FindBone(root, footTok);
            var toe = FindBone(root, toeTok);
            if (foot == null || toe == null)
            {
                sb.AppendLine($"[v4-diag] {label} {footTok}: MISSING (foot={foot != null} toe={toe != null})");
                return;
            }
            Vector3 pointH = Horiz(toe.position - foot.position);
            float yawVsBody = SignedYaw(bodyFwd, pointH);
            // Also report the two feet's convergence: angle between L and R pointing vectors (0 = parallel/straight).
            sb.AppendLine($"[v4-diag] {label} {footTok}: pointDir=({pointH.x:F3},{pointH.z:F3}) " +
                          $"yawVsHipsFwd={yawVsBody:F1}deg  toeLocalEuler={NormE(toe.localEulerAngles)}  footLocalEuler={NormE(foot.localEulerAngles)}");
        }

        private static void ReportClipFootCurves(StringBuilder sb, string clipLabel, AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var hit = new HashSet<string>();
            foreach (var b in bindings)
            {
                string p = b.path.ToLowerInvariant();
                foreach (var tok in new[] { "lefttoebase", "righttoebase", "leftfoot", "rightfoot" })
                    if (p.EndsWith(tok) && b.propertyName.StartsWith("m_LocalRotation")) hit.Add(tok);
            }
            sb.AppendLine($"[v4-diag] {clipLabel} clip KEYFRAMES rotation for: " +
                          $"leftToeBase={hit.Contains("lefttoebase")} rightToeBase={hit.Contains("righttoebase")} " +
                          $"leftFoot={hit.Contains("leftfoot")} rightFoot={hit.Contains("rightfoot")} " +
                          $"(if NOT keyframed, that bone stays at the rig BIND rotation for this clip)");
        }

        // DEFECT 2 confirm: the finger-curl (CastawayFingerCurl) rotates each right index bone +26deg about
        // LOCAL-X, an axis MEASURED ON v3's rig ("+X curls toward the palm"). If v4's index-bone local frame
        // differs, the SAME curl swings the wrong way -> a crumpled/mangled mitten. Measure the index-TIP's
        // hand-local displacement when the v3-tuned curl is applied: a CORRECT palm-curl moves the tip DOWN
        // (-Y) + FORWARD (+Z) in hand-local space; a large +/-X (sideways) or +Y (up) delta = wrong axis.
        private static void ReportCurlAxis(StringBuilder sb, string label, Transform root)
        {
            var hand = FindBone(root, "righthand");
            var i1 = FindBone(root, "righthandindex1");
            var i2 = FindBone(root, "righthandindex2");
            var i3 = FindBone(root, "righthandindex3");
            var tip = FindBone(root, "righthandindex4") ?? i3;
            if (hand == null || i1 == null || tip == null)
            { sb.AppendLine($"[v4-diag] {label} curl-axis: index chain incomplete (i1={i1 != null} tip={tip != null})"); return; }

            Vector3 before = hand.InverseTransformPoint(tip.position);
            var curl = Quaternion.Euler(26f, 0f, 0f); // == CastawayFingerCurl.fingerCurlDeg about local X
            if (i1 != null) i1.localRotation = i1.localRotation * curl;
            if (i2 != null) i2.localRotation = i2.localRotation * curl;
            if (i3 != null) i3.localRotation = i3.localRotation * curl;
            Vector3 after = hand.InverseTransformPoint(tip.position);
            Vector3 d = after - before;
            string verdict = (d.y < -0.005f && d.z > 0.005f && Mathf.Abs(d.x) < Mathf.Abs(d.z))
                ? "OK (tip -> palm: down+forward, small sideways)"
                : "WRONG-AXIS (tip does NOT curl cleanly toward the palm -> mangled mitten)";
            sb.AppendLine($"[v4-diag] {label} curl-axis (+26degX x3): tipHandLocalDelta=({d.x:F4},{d.y:F4},{d.z:F4})  {verdict}");
        }

        // ---- DEFECT 2 helpers: right hand + thumb bones & skin weights ----
        private static void ReportRightHand(StringBuilder sb, Transform root)
        {
            sb.AppendLine("[v4-diag] --- v4 RIGHT-HAND SUBTREE bones (thumb present?) ---");
            var hand = FindBone(root, "righthand");
            if (hand == null) { sb.AppendLine("[v4-diag] righthand bone MISSING"); return; }
            var names = new List<string>();
            foreach (var t in hand.GetComponentsInChildren<Transform>(true)) names.Add(Tok(t.name));
            sb.AppendLine("[v4-diag] righthand subtree: " + string.Join(",", names));
            bool hasThumb = names.Exists(n => n.Contains("thumb"));
            sb.AppendLine($"[v4-diag] right THUMB bone present: {hasThumb}");

            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) { sb.AppendLine("[v4-diag] no SMR/mesh"); return; }
            var bones = smr.bones; var mesh = smr.sharedMesh;
            var bw = mesh.boneWeights; var verts = mesh.vertices;
            // Index of the right-hand bone in the SMR bone array.
            int handIdx = -1;
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null && Tok(bones[i].name) == "righthand") handIdx = i;
            // Thumb geometry sits near the right hand, offset toward +thumb. Identify verts dominantly weighted to
            // the right hand (or its children) and report: how many, total-weight sanity, and the dominant-bone spread.
            int handVerts = 0, lowWeight = 0;
            var domBoneCount = new Dictionary<string, int>();
            // world positions of hand-region verts to measure the mitten extent (mangle = exploded extent).
            Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            var handSubtree = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && (bones[i] == hand || bones[i].IsChildOf(hand))) handSubtree.Add(i);
            for (int v = 0; v < bw.Length; v++)
            {
                var w = bw[v];
                float tot = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                int dom = w.boneIndex0;
                if (handSubtree.Contains(dom))
                {
                    handVerts++;
                    if (tot < 0.99f) lowWeight++;
                    string dn = dom < bones.Length && bones[dom] != null ? Tok(bones[dom].name) : "?";
                    domBoneCount.TryGetValue(dn, out int c); domBoneCount[dn] = c + 1;
                    Vector3 wp = l2w.MultiplyPoint3x4(verts[v]);
                    mn = Vector3.Min(mn, wp); mx = Vector3.Max(mx, wp);
                }
            }
            sb.AppendLine($"[v4-diag] right-hand-subtree verts: {handVerts} (lowWeight<0.99 tot: {lowWeight})");
            sb.AppendLine($"[v4-diag] dominant-bone spread of hand verts: " +
                          string.Join(", ", DictStr(domBoneCount)));
            sb.AppendLine($"[v4-diag] hand-region bind extent (world): {(mx - mn).ToString("F3")} " +
                          "(mitten should be small/compact; a huge extent = exploded/mangled geometry)");
            sb.AppendLine($"[v4-diag] TOTAL mesh verts: {verts.Length}, unweighted(tot<0.5): {CountUnweighted(bw)}");
        }

        // DEFECT 2 (revised) — bake REST vs GRIPPED + count inward-facing (dark-rendering) normals per hand.
        private static void ReportHandGrip(StringBuilder sb, string path)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) { sb.AppendLine("[v4-diag] hand-grip: v4 fbx missing"); return; }
            var go = Object.Instantiate(fbx);
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var rHand = FindBone(go.transform, "righthand");
            var lHand = FindBone(go.transform, "lefthand");
            if (smr == null || rHand == null) { sb.AppendLine("[v4-diag] hand-grip: smr/righthand missing"); Object.DestroyImmediate(go); return; }
            int[] rIdx = SubtreeVertIdx(smr, rHand);
            int[] lIdx = lHand != null ? SubtreeVertIdx(smr, lHand) : new int[0];

            var (rRestIn, rTot, rRestC) = InwardCount(smr, rIdx);
            var (lRestIn, lTot, _) = InwardCount(smr, lIdx);

            // Apply the grip curl (== CastawayFingerCurl: +26deg local-X on right index 1..3).
            var curl = Quaternion.Euler(26f, 0f, 0f);
            foreach (var t in new[] { "righthandindex1", "righthandindex2", "righthandindex3" })
            { var b = FindBone(go.transform, t); if (b != null) b.localRotation = b.localRotation * curl; }

            var (rGripIn, _, rGripC) = InwardCount(smr, rIdx);
            sb.AppendLine($"[v4-diag] HAND inward-normal count (verts facing INWARD = render dark/black): " +
                          $"RIGHT rest={rRestIn}/{rTot}  RIGHT gripped={rGripIn}/{rTot}  LEFT rest={lRestIn}/{lTot}");
            sb.AppendLine($"[v4-diag]   (a jump RIGHT rest->gripped = the curl folds/inverts geometry -> the black read; " +
                          $"RIGHT gripped >> LEFT rest = the L/R asymmetry the Sponsor sees)");
            // Handle intersection: gripped right-hand verts near the axe seat (hand-local offset) = thumb-in-handle.
            Vector3 seat = MovementCameraScene.HeldAxeV4LocalOffsetFromHand;
            int nearSeat = 0; var bw = smr.sharedMesh.boneWeights;
            var baked = new Mesh(); smr.BakeMesh(baked, true); var bv = baked.vertices;
            Matrix4x4 handInv = rHand.worldToLocalMatrix, l2w = smr.transform.localToWorldMatrix;
            foreach (int vi in rIdx)
            { Vector3 hl = handInv.MultiplyPoint3x4(l2w.MultiplyPoint3x4(bv[vi])); if ((hl - seat).magnitude < 0.06f) nearSeat++; }
            Object.DestroyImmediate(baked);
            sb.AppendLine($"[v4-diag]   gripped right-hand verts within 6cm of the axe seat {seat:F3}: {nearSeat} " +
                          $"(>0 = thumb/hand geometry INSIDE the handle -> dark wood shows through)");
            Object.DestroyImmediate(go);
        }

        private static int[] SubtreeVertIdx(SkinnedMeshRenderer smr, Transform handRoot)
        {
            var bones = smr.bones; var bw = smr.sharedMesh.boneWeights;
            var sub = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && (bones[i] == handRoot || bones[i].IsChildOf(handRoot))) sub.Add(i);
            var idx = new List<int>();
            for (int v = 0; v < bw.Length; v++) if (sub.Contains(bw[v].boneIndex0)) idx.Add(v);
            return idx.ToArray();
        }

        // Bake the current pose; count verts (of idx) whose WORLD normal points INWARD (back toward the vert
        // cluster's centroid) — those render dark/black under URP/Lit. Returns (inwardCount, total, centroid).
        private static (int, int, Vector3) InwardCount(SkinnedMeshRenderer smr, int[] idx)
        {
            if (idx.Length == 0) return (0, 0, Vector3.zero);
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var bv = baked.vertices; var bn = baked.normals;
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            Vector3 c = Vector3.zero; foreach (int vi in idx) c += l2w.MultiplyPoint3x4(bv[vi]); c /= idx.Length;
            int inward = 0;
            foreach (int vi in idx)
            {
                Vector3 wp = l2w.MultiplyPoint3x4(bv[vi]);
                Vector3 wn = l2w.MultiplyVector(bn[vi]).normalized;
                if (Vector3.Dot(wn, (wp - c).normalized) < -0.1f) inward++;
            }
            Object.DestroyImmediate(baked);
            return (inward, idx.Length, c);
        }

        // DEFECT 3 — idle-tick the live Animator, then apply candidate right-arm eulers + measure the right-hand
        // world rotation vs the MIRRORED left-hand (X-plane). The v3 arm-pose ships left=(-5,22,0), right=(-4,-50,-3).
        private static void ReportArmRoll(StringBuilder sb, string path, RuntimeAnimatorController ctrl)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null || ctrl == null) { sb.AppendLine("[v4-diag] arm-roll: fbx/ctrl missing"); return; }
            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) avatar = a;
            var go = Object.Instantiate(fbx);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rArm = FindBone(go.transform, "rightarm"); var lArm = FindBone(go.transform, "leftarm");
            var rHand = FindBone(go.transform, "righthand"); var lHand = FindBone(go.transform, "lefthand");
            if (rArm == null || lArm == null || rHand == null || lHand == null)
            { sb.AppendLine("[v4-diag] arm-roll: arm/hand bones missing"); Object.DestroyImmediate(go); return; }

            // Settle the IDLE state (Moving=false, Speed=0), then snapshot each arm bone's ticked clip pose.
            void TickIdle() { anim.Rebind(); anim.Play("Idle", 0, 0f); anim.SetBool("Moving", false); anim.SetBool("Grounded", true); anim.SetFloat("Speed", 0f); for (int i = 0; i < 12; i++) anim.Update(0.1f); }
            TickIdle();
            Vector3 leftOff = new Vector3(-5f, 22f, 0f); // == CastawayArmPose.leftArmEuler (v3 ship)
            var candidates = new (string name, Vector3 rEuler)[] {
                ("clip-only(no arm-pose)", Vector3.positiveInfinity),
                ("current v3 right(-4,-50,-3)", new Vector3(-4f, -50f, -3f)),
                ("mirror-left(-5,-22,0)", new Vector3(-5f, -22f, 0f)),
                ("same-as-left(-5,22,0)", new Vector3(-5f, 22f, 0f)),
                ("spread-only(-5,0,0)", new Vector3(-5f, 0f, 0f)),
            };
            foreach (var (name, rE) in candidates)
            {
                TickIdle();
                bool armPoseOn = !float.IsInfinity(rE.x);
                if (armPoseOn)
                {
                    lArm.localRotation = lArm.localRotation * Quaternion.Euler(leftOff);
                    rArm.localRotation = rArm.localRotation * Quaternion.Euler(rE);
                }
                // Mirror the left-hand WORLD rotation across the X-plane (negate y,z of the quaternion) — the
                // expected right if perfectly symmetric — and measure the mismatch to the actual right hand.
                Quaternion lq = lHand.rotation, mir = new Quaternion(lq.x, -lq.y, -lq.z, lq.w);
                float mismatch = Quaternion.Angle(rHand.rotation, mir);
                sb.AppendLine($"[v4-diag] arm-roll [{name}]: right-vs-mirrored-left mismatch = {mismatch:F1}deg  (lower = more symmetric; clip-only ~ the rig's inherent asymmetry)");
            }
            Object.DestroyImmediate(go);
        }

        // DEFECT 3 round-5 — measure the L/R hand-BONE bind frames + derive the right-WRIST bone-local correction
        // that makes the rendered right hand == mirror(left) at idle (the seed for the F9 WRIST knob).
        private static void ReportWristCorrection(StringBuilder sb, string path, RuntimeAnimatorController ctrl)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null || ctrl == null) { sb.AppendLine("[v4-diag] wrist: fbx/ctrl missing"); return; }
            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) avatar = a;
            var go = Object.Instantiate(fbx);
            var rHand = FindBone(go.transform, "righthand"); var lHand = FindBone(go.transform, "lefthand");
            var rArm = FindBone(go.transform, "rightarm"); var lArm = FindBone(go.transform, "leftarm");
            if (rHand == null || lHand == null) { sb.AppendLine("[v4-diag] wrist: hand bones missing"); Object.DestroyImmediate(go); return; }

            // (1) BIND frames (at instantiate, no tick = rest pose). Mirror = negate y,z of the quaternion
            // (reflection across the X-plane, axis-angle-derived). Validate the mirror on the UPPER ARMS (an
            // A-pose is ~symmetric → small delta EXPECTED); a LARGE hand delta = the auto-rig's rolled hand frame.
            Quaternion Mir(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
            void LogFrame(string lbl, Transform t) => sb.AppendLine(
                $"[v4-diag] {lbl}: localEuler=({N(t.localEulerAngles.x):F1},{N(t.localEulerAngles.y):F1},{N(t.localEulerAngles.z):F1}) " +
                $"+X=({t.right.x:F2},{t.right.y:F2},{t.right.z:F2}) +Y=({t.up.x:F2},{t.up.y:F2},{t.up.z:F2}) +Z=({t.forward.x:F2},{t.forward.y:F2},{t.forward.z:F2})");
            LogFrame("v4 BIND leftHand ", lHand);
            LogFrame("v4 BIND rightHand", rHand);
            if (rArm != null && lArm != null)
                sb.AppendLine($"[v4-diag] BIND upper-ARM mirror delta (validate the mirror; small=OK): {Quaternion.Angle(rArm.rotation, Mir(lArm.rotation)):F1}deg");
            sb.AppendLine($"[v4-diag] BIND HAND mirror delta (right vs mirrored-left) = {Quaternion.Angle(rHand.rotation, Mir(lHand.rotation)):F1}deg  (large = the rolled right-hand bind frame the auto-rig gave)");

            // (2) LIVE idle + the SHIPPED v4 arm-pose (right (-5,-22,0), left (-5,22,0)) — the dial-4 state the
            // Sponsor sees. Derive the bone-local WRIST correction: corr = Inv(R_right) * mirror(R_left), so
            // rightHand.local *= corr => rightHand.world == mirror(leftHand.world) == the rendered mirror.
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind(); anim.Play("Idle", 0, 0f); anim.SetBool("Moving", false); anim.SetBool("Grounded", true); anim.SetFloat("Speed", 0f);
            for (int i = 0; i < 12; i++) anim.Update(0.1f);
            if (lArm != null) lArm.localRotation = lArm.localRotation * Quaternion.Euler(-5f, 22f, 0f);
            if (rArm != null) rArm.localRotation = rArm.localRotation * Quaternion.Euler(-5f, -22f, 0f);
            Quaternion RL = lHand.rotation, RR = rHand.rotation, mirL = Mir(RL);
            sb.AppendLine($"[v4-diag] LIVE idle HAND mirror delta (pre-correction) = {Quaternion.Angle(RR, mirL):F1}deg");
            Quaternion corr = Quaternion.Inverse(RR) * mirL;
            Vector3 corrE = new Vector3(N(corr.eulerAngles.x), N(corr.eulerAngles.y), N(corr.eulerAngles.z));
            sb.AppendLine($"[v4-diag] --- SEED: CastawayV4RightWristEuler = new Vector3({corrE.x:F1}f, {corrE.y:F1}f, {corrE.z:F1}f);");
            // Verify: apply the correction bone-local + re-measure (should be ~0).
            rHand.localRotation = rHand.localRotation * corr;
            sb.AppendLine($"[v4-diag] POST-correction HAND mirror delta = {Quaternion.Angle(rHand.rotation, mirL):F1}deg (expect ~0 — the seed makes the right hand render-mirror the left)");
            Object.DestroyImmediate(go);
        }

        // DEFECT 3 ROUND-8 — root-cause the BOTH-arms twist on the LIVE re-rig + derive the LEFT-hand correction
        // anchored on the Sponsor-verified right (wrist (10,-120,-20)) + dump the L/R thumb bones for the HAND knob.
        private static void ReportRound8(StringBuilder sb, string path, RuntimeAnimatorController ctrl)
        {
            sb.AppendLine("[v4-diag] ===== ROUND-8 (Mixamo RE-RIG: both-arms twist root-cause + left-hand mirror) =====");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null || ctrl == null) { sb.AppendLine("[v4-diag] round8: fbx/ctrl missing"); return; }
            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) avatar = a;
            var go = Object.Instantiate(fbx);
            var lArm = FindBone(go.transform, "leftarm"); var rArm = FindBone(go.transform, "rightarm");
            var lHand = FindBone(go.transform, "lefthand"); var rHand = FindBone(go.transform, "righthand");
            var lThumb = FindBone(go.transform, "lefthandthumb1"); var rThumb = FindBone(go.transform, "righthandthumb1");
            if (lHand == null || rHand == null || lArm == null || rArm == null)
            { sb.AppendLine("[v4-diag] round8: arm/hand bones missing"); Object.DestroyImmediate(go); return; }

            // (0) BONE INVENTORY — the HAND knob needs a real thumb bone on BOTH sides (the OLD v4 rig had ZERO
            // thumb bones; the round-6 comment claims the re-rig has "real hand+thumb bones" — VERIFY on the live rig).
            sb.AppendLine($"[v4-diag] round8 THUMB bones present: left='lefthandthumb1'={lThumb != null}  right='righthandthumb1'={rThumb != null}");
            void DumpSub(string lbl, Transform hand)
            {
                if (hand == null) { sb.AppendLine($"[v4-diag] round8 {lbl}: MISSING"); return; }
                var names = new List<string>();
                foreach (var t in hand.GetComponentsInChildren<Transform>(true)) names.Add(Tok(t.name));
                sb.AppendLine($"[v4-diag] round8 {lbl} subtree ({names.Count}): " + string.Join(",", names));
            }
            DumpSub("LEFT hand", lHand); DumpSub("RIGHT hand", rHand);

            Quaternion Mir(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // The round-7 SHIPPED defaults the Sponsor judged: arm eulers (-5,±22,0), wrist 0. Front view, idle.
            Vector3 leftArmE = new Vector3(-5f, 22f, 0f), rightArmE = new Vector3(-5f, -22f, 0f);
            Vector3 sponsorRightWrist = new Vector3(10f, -120f, -20f); // the Sponsor F9-dialed CORRECT right (round-8)

            // Tick idle to the shipped clip pose, then apply the shipped arm eulers to the UPPER ARMS.
            void TickIdleAndArmPose()
            {
                anim.Rebind(); anim.Play("Idle", 0, 0f); anim.SetBool("Moving", false); anim.SetBool("Grounded", true); anim.SetFloat("Speed", 0f);
                for (int i = 0; i < 12; i++) anim.Update(0.1f);
                lArm.localRotation = lArm.localRotation * Quaternion.Euler(leftArmE);
                rArm.localRotation = rArm.localRotation * Quaternion.Euler(rightArmE);
            }

            // (1) BASELINE at the shipped defaults (wrist 0). Is the twist SYMMETRIC (both wrong the same way ->
            // small mismatch) or ASYMMETRIC (right rolled off the mirrored left -> large mismatch)? Also validate
            // the mirror operator on the upper arms (an A/T-pose is ~symmetric -> small delta EXPECTED).
            TickIdleAndArmPose();
            float armMirror = Quaternion.Angle(rArm.rotation, Mir(lArm.rotation));
            float handMirror0 = Quaternion.Angle(rHand.rotation, Mir(lHand.rotation));
            sb.AppendLine($"[v4-diag] round8 [defaults: armEuler+wrist0] upper-ARM mirror delta = {armMirror:F1}deg (small = the mirror operator is valid on this rig)");
            sb.AppendLine($"[v4-diag] round8 [defaults: armEuler+wrist0] HAND mirror delta (right vs mirrored-left) = {handMirror0:F1}deg " +
                          "(this is the both-hands asymmetry the Sponsor sees at the round-7 defaults; wrist 0 provides NO correction)");

            // (2) Apply the Sponsor's CORRECT right wrist (10,-120,-20) to the RIGHT HAND bone. The right hand is
            // now Sponsor-verified-correct; capture its world rotation as the mirror ANCHOR for the left.
            TickIdleAndArmPose();
            rHand.localRotation = rHand.localRotation * Quaternion.Euler(sponsorRightWrist);
            Quaternion Rright = rHand.rotation, Rleft = lHand.rotation;
            float postRightVsMirL = Quaternion.Angle(Rright, Mir(Rleft));
            sb.AppendLine($"[v4-diag] round8 [right wrist (10,-120,-20), left uncorrected] right vs mirrored-left = {postRightVsMirL:F1}deg " +
                          "(LARGE is EXPECTED + CONFIRMS the left needs its OWN mirror correction — the round-7 'symmetric rig needs no compensation' assumption is REFUTED)");

            // (3) Derive the LEFT-hand bone-local correction so the LEFT hand renders == mirror(Sponsor-correct right):
            // world' = Rleft * corrL == mirror(Rright)  ->  corrL = Inv(Rleft) * mirror(Rright).
            Quaternion corrL = Quaternion.Inverse(Rleft) * Mir(Rright);
            Vector3 corrLE = new Vector3(N(corrL.eulerAngles.x), N(corrL.eulerAngles.y), N(corrL.eulerAngles.z));
            sb.AppendLine($"[v4-diag] --- SEED: CastawayV4LeftWristEuler = new Vector3({corrLE.x:F1}f, {corrLE.y:F1}f, {corrLE.z:F1}f);  (mirror of the Sponsor-correct right)");
            sb.AppendLine($"[v4-diag] round8 (sanity: the naive local mirror of (10,-120,-20) = (10,120,20); compare to the DERIVED seed above)");

            // (4) VERIFY: apply the derived left correction bone-local + re-measure right-vs-mirrored-left (expect ~0).
            lHand.localRotation = lHand.localRotation * corrL;
            float verify = Quaternion.Angle(rHand.rotation, Mir(lHand.rotation));
            sb.AppendLine($"[v4-diag] round8 POST both-hand corrections: right vs mirrored-left = {verify:F1}deg (expect ~0 -> both hands render as mirrors)");

            // (5) THUMB bind frames (for the HAND knob seed). Report L/R thumb1 local frames + mirror delta.
            if (lThumb != null && rThumb != null)
            {
                TickIdleAndArmPose();
                float thumbMirror = Quaternion.Angle(rThumb.rotation, Mir(lThumb.rotation));
                sb.AppendLine($"[v4-diag] round8 THUMB1 mirror delta (right vs mirrored-left, at defaults) = {thumbMirror:F1}deg " +
                              "(the HAND/thumb knob defaults 0 — the Sponsor taste-dials thumb orientation; this is informational)");
            }
            else sb.AppendLine("[v4-diag] round8 THUMB: no thumb1 bone on one/both sides -> the HAND knob cannot rotate a thumb chain (fall back to hand-bone only)");

            Object.DestroyImmediate(go);
        }

        // ================= ROUND-9: SKIN-WEIGHT / ARTICULATION MEASUREMENT =================
        // Three independent measurements, each reported per SIDE so left (Sponsor-ACCEPTED, reads as an
        // articulated hand) is the control and right (the defect, reads as "a block with a thumb") is the test:
        //   (A) PER-BONE INFLUENCE CENSUS — for every bone in each hand subtree: how many verts it influences at
        //       ALL (any of the 4 weight slots > 0.001) and its summed weight MASS. If the right thumb/finger
        //       bones carry near-zero mass while the left's carry real mass, the right hand's articulation was
        //       never wired and every rotation fix since round 3 was correcting a hand that cannot deform.
        //   (B) THE DECISIVE PROBE — rotate one bone by an IDENTICAL test angle per side, re-bake the skinned
        //       mesh, and diff vertex positions: moved-count + MEAN + MAX displacement in mm. This is the only
        //       measurement that sees what the RENDERER sees (weights × bone transform), and magnitude is the
        //       load-bearing number (weak-partial weights move verts a little; the eye reads that as "a block").
        //   (C) PER-SIDE FINGER GEOMETRY — vert/tri counts + region extent for each hand, plus a mirror-compare
        //       of right-hand verts against mirrored-left. Answers the alternative reading of "block": that the
        //       left's finger SEPARATION comes from geometry the right lacks. (Round-6 measured a perfect vertex
        //       mirror — but on the OLD rig/mesh; re-measure here rather than inherit that result.)
        private static void ReportRound9(StringBuilder sb, string path)
        {
            sb.AppendLine("[v4-diag] ===== ROUND-9 (SKIN WEIGHTS + ARTICULATION: 'its a block with a thumb') =====");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) { sb.AppendLine("[v4-diag] round9: v4 fbx missing"); return; }
            var go = Object.Instantiate(fbx);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null)
            { sb.AppendLine("[v4-diag] round9: no SMR/mesh"); Object.DestroyImmediate(go); return; }

            var lHand = FindBone(go.transform, "lefthand");
            var rHand = FindBone(go.transform, "righthand");
            if (lHand == null || rHand == null)
            { sb.AppendLine("[v4-diag] round9: hand bones missing"); Object.DestroyImmediate(go); return; }

            // ---------- (A) PER-BONE INFLUENCE CENSUS ----------
            sb.AppendLine("[v4-diag] round9 (A) PER-BONE INFLUENCE CENSUS — verts influenced (any weight>0.001) + summed weight mass");
            InfluenceCensus(sb, smr, lHand, "LEFT ");
            InfluenceCensus(sb, smr, rHand, "RIGHT");

            // ---------- (B) THE DECISIVE PROBE: rotate-and-diff, magnitude in mm ----------
            // Identical test angle both sides. 40deg is large enough that CORRECT weights produce an unmistakable
            // displacement, while weak-partial weights produce a small-but-nonzero one (the Sponsor's "moves a
            // little / hard to tell"). Applied bone-LOCAL so each side's own frame is respected — we are measuring
            // whether the SKIN follows the bone at all, not whether the bone points the right way.
            sb.AppendLine("[v4-diag] round9 (B) DECISIVE PROBE — rotate bone 40deg (local X, then local Y), re-bake, diff verts");
            sb.AppendLine("[v4-diag]   compare LEFT (Sponsor-accepted, articulated) vs RIGHT (the 'block'); RIGHT << LEFT displacement = weights confirmed");
            foreach (var (boneTok, label) in new[] {
                ("thumb1", "THUMB1"), ("thumb2", "THUMB2"),
                ("index1", "INDEX1"), ("middle1", "MIDDLE1"), ("hand", "HAND(control)") })
            {
                foreach (var axis in new[] { 'X', 'Y' })
                {
                    var lRes = RotateAndDiff(smr, go.transform, "left", boneTok, axis, 40f);
                    var rRes = RotateAndDiff(smr, go.transform, "right", boneTok, axis, 40f);
                    sb.AppendLine($"[v4-diag]   {label} +40deg local-{axis}: " +
                        $"LEFT moved={lRes.moved} mean={lRes.meanMm:F2}mm max={lRes.maxMm:F2}mm | " +
                        $"RIGHT moved={rRes.moved} mean={rRes.meanMm:F2}mm max={rRes.maxMm:F2}mm | " +
                        $"ratio(R/L) mean={Ratio(rRes.meanMm, lRes.meanMm)} max={Ratio(rRes.maxMm, lRes.maxMm)}");
                }
            }
            sb.AppendLine("[v4-diag]   INTERPRETATION: ratio ~1.0 = both sides skinned equally (weights hypothesis REFUTED); " +
                          "ratio ~0.0 = right bone has no skin influence (frozen); 0 < ratio << 1 = WEAK PARTIAL weights " +
                          "(the 'moves a little / hard to tell' signature) — right thumb/finger verts mostly bound to the palm.");

            // ---------- (C) PER-SIDE FINGER GEOMETRY ----------
            sb.AppendLine("[v4-diag] round9 (C) PER-SIDE HAND GEOMETRY — is the right hand missing finger separation as GEOMETRY?");
            HandGeometryCensus(sb, smr, lHand, "LEFT ");
            HandGeometryCensus(sb, smr, rHand, "RIGHT");
            MirrorCompareHands(sb, smr, lHand, rHand);

            // ---------- (D) MECHANISM: what actually drives the RIGHT thumb's geometry? ----------
            // (A)+(B) show the right thumb chain carries little mass and barely deforms — but "bound to the palm"
            // is an ASSUMPTION until measured. (C) proves the two hands are the same geometry, so the LEFT thumb's
            // verts identify exactly which verts form the thumb. Mirror those to the right side, find the matching
            // right verts, and report which bone actually DOMINATES them. That names the mis-binding directly.
            ThumbAttribution(sb, smr, lHand, rHand);

            Object.DestroyImmediate(go);
        }

        // (A) For each bone in the hand subtree: vert count influenced through ANY of the 4 weight slots + the
        // summed weight mass. Dominant-bone-only counting (the round-8 helper) HIDES weak partial influence —
        // exactly the signal this round is hunting — so this walks all four slots.
        private static void InfluenceCensus(StringBuilder sb, SkinnedMeshRenderer smr, Transform handRoot, string side)
        {
            var bones = smr.bones; var bw = smr.sharedMesh.boneWeights;
            var idxToName = new Dictionary<int, string>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && (bones[i] == handRoot || bones[i].IsChildOf(handRoot)))
                    idxToName[i] = Tok(bones[i].name);
            if (idxToName.Count == 0) { sb.AppendLine($"[v4-diag]   {side}: no bones under hand root"); return; }

            var count = new Dictionary<int, int>();
            var mass = new Dictionary<int, float>();
            foreach (var k in idxToName.Keys) { count[k] = 0; mass[k] = 0f; }
            foreach (var w in bw)
            {
                Accum(count, mass, w.boneIndex0, w.weight0);
                Accum(count, mass, w.boneIndex1, w.weight1);
                Accum(count, mass, w.boneIndex2, w.weight2);
                Accum(count, mass, w.boneIndex3, w.weight3);
            }
            var parts = new List<string>();
            foreach (var kv in idxToName)
                parts.Add($"{kv.Value}[verts={count[kv.Key]} mass={mass[kv.Key]:F2}]");
            sb.AppendLine($"[v4-diag]   {side} hand subtree ({idxToName.Count} bones): " + string.Join(" ", parts));

            void Accum(Dictionary<int, int> c, Dictionary<int, float> m, int bi, float w)
            {
                if (w <= 0.001f || !c.ContainsKey(bi)) return;
                c[bi]++; m[bi] += w;
            }
        }

        // (B) Bake the skinned mesh, rotate ONE bone by a fixed angle, re-bake, and diff. Returns moved-vert count
        // (>0.05mm, i.e. above float noise) plus mean/max displacement in MILLIMETRES over the moved set. Restores
        // the bone afterwards so probes don't contaminate each other.
        private static (int moved, float meanMm, float maxMm) RotateAndDiff(
            SkinnedMeshRenderer smr, Transform root, string sidePrefix, string boneTok, char axis, float deg)
        {
            var bone = FindBone(root, sidePrefix + "hand" + (boneTok == "hand" ? "" : boneTok));
            if (bone == null) bone = FindBone(root, sidePrefix + boneTok);
            if (bone == null) return (-1, 0f, 0f);

            var before = new Mesh(); smr.BakeMesh(before, true);
            var bv = before.vertices;
            Quaternion saved = bone.localRotation;
            Vector3 e = axis == 'X' ? new Vector3(deg, 0f, 0f)
                      : axis == 'Y' ? new Vector3(0f, deg, 0f) : new Vector3(0f, 0f, deg);
            bone.localRotation = saved * Quaternion.Euler(e);

            var after = new Mesh(); smr.BakeMesh(after, true);
            var av = after.vertices;
            bone.localRotation = saved;

            int moved = 0; float sum = 0f, max = 0f;
            int n = Mathf.Min(bv.Length, av.Length);
            // BakeMesh returns RENDERER-LOCAL vertices, and this FBX imports at a small local scale (the hand
            // region measures ~0.001 in local units), so raw deltas are NOT physical. Push the delta through the
            // renderer's localToWorld to get real-world millimetres. 0.05mm threshold rejects float noise.
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            for (int i = 0; i < n; i++)
            {
                float d = l2w.MultiplyVector(av[i] - bv[i]).magnitude * 1000f;
                if (d > 0.05f) { moved++; sum += d; if (d > max) max = d; }
            }
            Object.DestroyImmediate(before); Object.DestroyImmediate(after);
            return (moved, moved > 0 ? sum / moved : 0f, max);
        }

        private static string Ratio(float r, float l) => l > 0.0001f ? (r / l).ToString("F3") : "n/a";

        // (C) Vert/tri census + region extent for one hand, counting verts by DOMINANT bone so the numbers are
        // comparable to the round-6 geometry measurement.
        private static void HandGeometryCensus(StringBuilder sb, SkinnedMeshRenderer smr, Transform handRoot, string side)
        {
            int[] idx = SubtreeVertIdx(smr, handRoot);
            var mesh = smr.sharedMesh; var verts = mesh.vertices;
            if (idx.Length == 0) { sb.AppendLine($"[v4-diag]   {side}: 0 hand-region verts"); return; }
            Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
            foreach (int v in idx) { mn = Vector3.Min(mn, verts[v]); mx = Vector3.Max(mx, verts[v]); }
            var set = new HashSet<int>(idx);
            var tris = mesh.triangles; int triCount = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
                if (set.Contains(tris[t]) || set.Contains(tris[t + 1]) || set.Contains(tris[t + 2])) triCount++;
            sb.AppendLine($"[v4-diag]   {side} hand region: verts={idx.Length} tris={triCount} extent={(mx - mn).ToString("F4")}m " +
                          "(a hand with modelled finger separation carries MORE verts/tris than an undifferentiated block)");
        }

        // (C cont.) Mirror the LEFT hand verts across the X-plane and nearest-neighbour match them to the RIGHT
        // hand verts. Near-zero distances = the two hands are the same GEOMETRY (so 'block vs articulated' cannot
        // be a modelling difference, and the defect must live in the weights/pose layers).
        private static void MirrorCompareHands(StringBuilder sb, SkinnedMeshRenderer smr, Transform lHand, Transform rHand)
        {
            int[] lIdx = SubtreeVertIdx(smr, lHand), rIdx = SubtreeVertIdx(smr, rHand);
            if (lIdx.Length == 0 || rIdx.Length == 0) { sb.AppendLine("[v4-diag]   mirror-compare: a hand region is empty"); return; }
            var verts = smr.sharedMesh.vertices;
            // Mirror plane = the mesh's own X centre (the model is authored symmetric about local X=0).
            float sum = 0f, max = 0f;
            foreach (int r in rIdx)
            {
                Vector3 p = verts[r]; Vector3 m = new Vector3(-p.x, p.y, p.z);
                float best = float.MaxValue;
                foreach (int l in lIdx) { float d = (verts[l] - m).sqrMagnitude; if (d < best) best = d; }
                float mm = Mathf.Sqrt(best) * 1000f;
                sum += mm; if (mm > max) max = mm;
            }
            sb.AppendLine($"[v4-diag]   mirror-compare RIGHT vs mirrored-LEFT: verts R={rIdx.Length} L={lIdx.Length} " +
                          $"meanNearest={sum / rIdx.Length:F3}mm maxNearest={max:F3}mm " +
                          "(~0 = identical geometry both sides -> 'block' is NOT a modelling difference)");
        }

        // (D) Identify the thumb verts via the LEFT (correct) side, mirror them onto the RIGHT, and report which
        // bone dominates the corresponding right verts + how much thumb-chain weight they actually carry.
        private static void ThumbAttribution(StringBuilder sb, SkinnedMeshRenderer smr, Transform lHand, Transform rHand)
        {
            var bones = smr.bones; var bw = smr.sharedMesh.boneWeights; var verts = smr.sharedMesh.vertices;
            string NameOf(int i) => i >= 0 && i < bones.Length && bones[i] != null ? Tok(bones[i].name) : "?";

            // Left thumb verts = dominant bone name contains "thumb" and sits under the LEFT hand.
            var leftThumbVerts = new List<int>();
            for (int v = 0; v < bw.Length; v++)
            {
                int d = bw[v].boneIndex0;
                if (d < bones.Length && bones[d] != null && bones[d].IsChildOf(lHand) && Tok(bones[d].name).Contains("thumb"))
                    leftThumbVerts.Add(v);
            }
            // Candidate right verts = anything under the right hand subtree (dominant-bone based).
            int[] rIdx = SubtreeVertIdx(smr, rHand);
            if (leftThumbVerts.Count == 0 || rIdx.Length == 0)
            { sb.AppendLine("[v4-diag] round9 (D) thumb-attribution: insufficient verts"); return; }

            var domCount = new Dictionary<string, int>();
            float thumbMassSum = 0f; int matched = 0;
            foreach (int lv in leftThumbVerts)
            {
                Vector3 m = new Vector3(-verts[lv].x, verts[lv].y, verts[lv].z);
                int best = -1; float bestD = float.MaxValue;
                foreach (int rv in rIdx)
                { float d = (verts[rv] - m).sqrMagnitude; if (d < bestD) { bestD = d; best = rv; } }
                if (best < 0 || Mathf.Sqrt(bestD) * 1000f > 1f) continue; // require a real mirror match
                matched++;
                var w = bw[best];
                string dn = NameOf(w.boneIndex0);
                domCount.TryGetValue(dn, out int c); domCount[dn] = c + 1;
                // How much of this vert's weight actually belongs to the right THUMB chain?
                float tm = 0f;
                if (NameOf(w.boneIndex0).Contains("thumb")) tm += w.weight0;
                if (NameOf(w.boneIndex1).Contains("thumb")) tm += w.weight1;
                if (NameOf(w.boneIndex2).Contains("thumb")) tm += w.weight2;
                if (NameOf(w.boneIndex3).Contains("thumb")) tm += w.weight3;
                thumbMassSum += tm;
            }
            sb.AppendLine($"[v4-diag] round9 (D) THUMB ATTRIBUTION — {leftThumbVerts.Count} left-thumb verts, {matched} mirror-matched on the right");
            sb.AppendLine($"[v4-diag]   the RIGHT verts occupying the THUMB's geometry are dominated by: " + string.Join(", ", DictStr(domCount)));
            sb.AppendLine($"[v4-diag]   mean THUMB-CHAIN weight on those right verts = {(matched > 0 ? thumbMassSum / matched : 0f):F3} " +
                          "(1.0 = fully thumb-driven like the left; ~0 = the thumb geometry is driven by another bone entirely)");
            // The same attribution on the LEFT, as the control.
            float lMass = 0f;
            foreach (int lv in leftThumbVerts)
            {
                var w = bw[lv]; float tm = 0f;
                if (NameOf(w.boneIndex0).Contains("thumb")) tm += w.weight0;
                if (NameOf(w.boneIndex1).Contains("thumb")) tm += w.weight1;
                if (NameOf(w.boneIndex2).Contains("thumb")) tm += w.weight2;
                if (NameOf(w.boneIndex3).Contains("thumb")) tm += w.weight3;
                lMass += tm;
            }
            sb.AppendLine($"[v4-diag]   CONTROL: mean thumb-chain weight on the LEFT thumb verts = {lMass / leftThumbVerts.Count:F3}");
        }

        // =====================================================================================================
        // ROUND-10 (86cau4za2, RE-SCOPED by the Sponsor 2026-08-01) — "BOTH HANDS READ AS PLAIN BLOCKS"
        //
        // THE NEW REQUIREMENT (verbatim): "fix that one hand is not the same as the other. If both hands are
        // just a square with no thumb thats fine, its meant to be low poly minecraft style." Follow-ups: both
        // hands MAY become blocks (the accepted dial-7 LEFT is explicitly released); "no thumb" means it must
        // not READ as a thumb (geometry may stay — NO mesh surgery); a STATIC block is fine; he judges by soak.
        //
        // WHAT IS A THUMB, IN PLAIN LANGUAGE (the physical-world anchor this instrument must satisfy — a metric
        // is green on nonsense): a thumb READS as a thumb when a lump of geometry sticks OUT past the side of
        // the hand block. It stops reading as a thumb when it is FLUSH — nothing protruding past the block.
        // So the load-bearing number here is PROTRUSION: how far the thumb verts sit OUTSIDE the box formed by
        // the rest of the hand, in millimetres, in the hand's own frame. Symmetry = |protrusion_L - protrusion_R|.
        //
        // WHY A NEW PROBE AND NOT ROUND-9's NUMBERS: round-9's RotateAndDiff averages displacement over EVERY
        // vert in the WHOLE MESH that the bone moved (see its loop: `for i in 0..n` over all baked verts). Its
        // headline "rotating right index1 moves them 1.77mm" is therefore the mean motion of everything index1
        // drives — dominated by the INDEX FINGER — NOT the motion of the thumb geometry. That number cannot
        // answer "can I tuck the right thumb by dialling index1", which is exactly the question the re-scoped
        // ticket turns on. Round-10 scopes every displacement to a NAMED vert set, sweeps all THREE axes (round-9
        // tested only X and Y) across a range of angles (round-9 tested only 40deg), and reports the geometric
        // CEILING (2r) so an impossibility can be stated as a number rather than guessed at.
        // =====================================================================================================

        /// <summary>
        /// Round-10 entry point (86cau4za2 re-scope). Focused — does NOT run rounds 1-9. Headless:
        ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CastawayV4DefectDiag.RunBlockHands
        /// </summary>
        public static void RunBlockHands()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[blockhands] ===== ROUND-10 — BOTH-HANDS-AS-BLOCKS (86cau4za2 re-scope) =====");
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetGen.ControllerPath);
            sb.AppendLine($"[blockhands] controller loaded: {ctrl != null}");
            ReportRound10(sb, CharacterAssetGen.V4RiggedFbxPath, ctrl);
            sb.AppendLine("[blockhands] ===== END =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void ReportRound10(StringBuilder sb, string path, RuntimeAnimatorController ctrl)
        {
            var go = PoseShipped(path, ctrl, sb);
            if (go == null) return;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var lHand = FindBone(go.transform, "lefthand");
            var rHand = FindBone(go.transform, "righthand");
            if (smr == null || lHand == null || rHand == null)
            { sb.AppendLine("[blockhands] missing smr/hand bones"); Object.DestroyImmediate(go); return; }

            // ---------- vert sets ----------
            var verts = smr.sharedMesh.vertices;
            var bw = smr.sharedMesh.boneWeights;
            var bones = smr.bones;
            string NameOf(int i) => i >= 0 && i < bones.Length && bones[i] != null ? Tok(bones[i].name) : "?";

            // LEFT thumb verts: dominant bone is a thumb bone under the left hand (the side whose weights are correct).
            var lThumb = new List<int>();
            for (int v = 0; v < bw.Length; v++)
            {
                int d = bw[v].boneIndex0;
                if (d < bones.Length && bones[d] != null && bones[d].IsChildOf(lHand) && NameOf(d).Contains("thumb"))
                    lThumb.Add(v);
            }
            // RIGHT thumb verts: CANNOT be found by bone name (they are skinned to the INDEX chain — the whole
            // defect). Found by mirroring the left thumb verts across the mesh's X centre and nearest-matching.
            int[] rSub = SubtreeVertIdx(smr, rHand);
            var rThumb = new List<int>();
            foreach (int lv in lThumb)
            {
                Vector3 m = new Vector3(-verts[lv].x, verts[lv].y, verts[lv].z);
                int best = -1; float bestD = float.MaxValue;
                foreach (int rv in rSub) { float d = (verts[rv] - m).sqrMagnitude; if (d < bestD) { bestD = d; best = rv; } }
                if (best >= 0 && Mathf.Sqrt(bestD) * 1000f <= 1f) rThumb.Add(best);
            }
            int[] lSub = SubtreeVertIdx(smr, lHand);
            var lBlock = new List<int>(); foreach (int v in lSub) if (!lThumb.Contains(v)) lBlock.Add(v);
            var rThumbSet = new HashSet<int>(rThumb);
            var rBlock = new List<int>(); foreach (int v in rSub) if (!rThumbSet.Contains(v)) rBlock.Add(v);
            sb.AppendLine($"[blockhands] vert sets: LEFT thumb={lThumb.Count} block={lBlock.Count} | " +
                          $"RIGHT thumb={rThumb.Count} (mirror-matched) block={rBlock.Count}");
            if (lThumb.Count == 0 || rThumb.Count == 0)
            { sb.AppendLine("[blockhands] ABORT — could not isolate a thumb vert set"); Object.DestroyImmediate(go); return; }

            // Which bone actually owns the right thumb verts (restates round-9 (D) on the scoped set, as a control).
            var dom = new Dictionary<string, int>();
            foreach (int v in rThumb) { string n = NameOf(bw[v].boneIndex0); dom.TryGetValue(n, out int c); dom[n] = c + 1; }
            sb.AppendLine("[blockhands] RIGHT thumb verts are driven by: " + string.Join(", ", DictStr(dom)));

            // ---------- (A) BASELINE PROTRUSION — the anchor number ----------
            sb.AppendLine("[blockhands] (A) BASELINE PROTRUSION — mm of thumb geometry sticking OUT past the hand block");
            sb.AppendLine("[blockhands]   (a thumb READS as a thumb when it protrudes; flush == 0 == reads as a plain block)");
            var pL0 = Protrusion(smr, lHand, lThumb, lBlock);
            var pR0 = Protrusion(smr, rHand, rThumb, rBlock);
            sb.AppendLine($"[blockhands]   LEFT  (shipped dial-7): max={pL0.max:F2}mm mean={pL0.mean:F2}mm outside={pL0.frac:P0}");
            sb.AppendLine($"[blockhands]   RIGHT (shipped dial-7): max={pR0.max:F2}mm mean={pR0.mean:F2}mm outside={pR0.frac:P0}");
            sb.AppendLine($"[blockhands]   ASYMMETRY (what the Sponsor sees): |L-R| max-protrusion delta = {Mathf.Abs(pL0.max - pR0.max):F2}mm");

            // ---------- (A2) CORROBORATING METRICS — the AABB above is a LOOSE hull ----------
            // A thumb pointing diagonally can sit INSIDE the block's axis-aligned box and still read as a lump,
            // so an AABB-protrusion of 0.00mm is necessary-but-not-sufficient evidence of "reads as a block".
            // Two hull-free cross-checks; all three must agree before the verdict is trusted.
            //   STANDOFF: for each thumb vert, distance to the NEAREST block vert. A flush/absorbed thumb has
            //   every vert close to the block surface; a protruding lump has tip verts far from any block vert.
            //   SHAPE-ASYMMETRY: express each hand's thumb verts in its OWN hand-bone frame, mirror one across
            //   X, and nearest-match. This isolates hand SHAPE from where the arm happens to put the hand, and
            //   is the most direct reading of the Sponsor's actual words ("one hand is not the same as the other").
            sb.AppendLine("[blockhands] (A2) CORROBORATION — hull-free cross-checks (the AABB in (A) is a loose bound)");
            var sL = Standoff(smr, lThumb, lBlock);
            var sR = Standoff(smr, rThumb, rBlock);
            sb.AppendLine($"[blockhands]   STANDOFF LEFT : max={sL.max:F2}mm mean={sL.mean:F2}mm (distance from thumb verts to nearest block vert)");
            sb.AppendLine($"[blockhands]   STANDOFF RIGHT: max={sR.max:F2}mm mean={sR.mean:F2}mm");
            float asym0 = ShapeAsymmetry(smr, lHand, rHand, lThumb, rThumb);
            sb.AppendLine($"[blockhands]   SHAPE-ASYMMETRY (thumb region, hand-local, mirrored): mean={asym0:F2}mm  <- the Sponsor's complaint, as one number");

            // ---------- (B) RIGHT: what can dialling the INDEX chain actually achieve? ----------
            // Scoped to the right THUMB verts (round-9's gap), all three axes, a real angle sweep, plus the
            // collateral cost on the index FINGER block (the brief's second unverified item).
            sb.AppendLine("[blockhands] (B) RIGHT — sweep righthandindex1 (the bone that actually drives the right thumb verts)");
            sb.AppendLine("[blockhands]   thumbDisp = motion of the RIGHT THUMB verts only | blockDisp = collateral motion of the rest of the hand");
            var rIndex1 = FindBone(go.transform, "righthandindex1");
            if (rIndex1 == null) sb.AppendLine("[blockhands]   righthandindex1 NOT FOUND");
            else
            {
                float ceilingMax = 0f;
                foreach (char ax in new[] { 'X', 'Y', 'Z' })
                    foreach (float deg in new[] { 20f, 40f, 60f, 90f, 120f, 160f, 180f })
                    {
                        var t = ScopedDisp(smr, rIndex1, ax, deg, rThumb);
                        var b = ScopedDisp(smr, rIndex1, ax, deg, rBlock);
                        var pr = ProtrusionWith(smr, rIndex1, ax, deg, rHand, rThumb, rBlock);
                        if (t.max > ceilingMax) ceilingMax = t.max;
                        sb.AppendLine($"[blockhands]   index1 +{deg,3}deg local-{ax}: thumbDisp mean={t.mean,6:F2}mm max={t.max,6:F2}mm | " +
                                      $"blockDisp mean={b.mean,6:F2}mm max={b.max,6:F2}mm | resulting thumb protrusion max={pr.max:F2}mm");
                    }
                // Geometric ceiling: a rotation moves a vert on a circle of radius r about the bone axis, so the
                // FURTHEST any dial can EVER displace it is the diameter 2r. Recover r from the largest observed
                // chord: chord = 2*r*sin(theta/2).
                sb.AppendLine($"[blockhands]   GEOMETRIC CEILING: largest observed thumb-vert displacement over the whole sweep = {ceilingMax:F2}mm");
                sb.AppendLine("[blockhands]   (a rotation can never exceed the diameter 2r of the vert's circle about the bone axis —");
                sb.AppendLine("[blockhands]    so if this ceiling is below the baseline protrusion in (A), tucking the RIGHT thumb by");
                sb.AppendLine("[blockhands]    dialling is GEOMETRICALLY IMPOSSIBLE, not merely un-found.)");
                sb.AppendLine($"[blockhands]   VERDICT-INPUT: right baseline protrusion {pR0.max:F2}mm vs achievable {ceilingMax:F2}mm");
            }

            // ---------- (C) LEFT: find the euler that tucks the left thumb flush ----------
            sb.AppendLine("[blockhands] (C) LEFT — search leftThumbEuler for minimum protrusion (the left thumb DOES articulate: weight 0.919)");
            var lThumbBone = FindBone(go.transform, "lefthandthumb1");
            if (lThumbBone == null) sb.AppendLine("[blockhands]   lefthandthumb1 NOT FOUND");
            else
            {
                Quaternion saved = lThumbBone.localRotation;
                Vector3 bestE = Vector3.zero; float bestScore = float.MaxValue;
                // OBJECTIVE = the SHAPE-ASYMMETRY against the right hand (the Sponsor's literal ask: make one
                // hand the same as the other), NOT the loose AABB protrusion — optimising the AABB alone would
                // let the search exploit its slack and "win" on a number while still reading as a thumb.
                float Score()
                {
                    float asym = ShapeAsymmetry(smr, lHand, rHand, lThumb, rThumb);
                    float stand = Standoff(smr, lThumb, lBlock).max;
                    return asym + stand; // both in mm; both must come down for a genuine block read
                }
                // Coarse 45deg grid over the full sphere, then a 15deg refine in a +-45 window around the winner.
                for (float x = 0; x < 360f; x += 45f)
                    for (float y = 0; y < 360f; y += 45f)
                        for (float z = 0; z < 360f; z += 45f)
                        {
                            lThumbBone.localRotation = saved * Quaternion.Euler(x, y, z);
                            float s = Score();
                            if (s < bestScore) { bestScore = s; bestE = new Vector3(x, y, z); }
                        }
                Vector3 c0 = bestE;
                for (float x = c0.x - 45f; x <= c0.x + 45f; x += 15f)
                    for (float y = c0.y - 45f; y <= c0.y + 45f; y += 15f)
                        for (float z = c0.z - 45f; z <= c0.z + 45f; z += 15f)
                        {
                            lThumbBone.localRotation = saved * Quaternion.Euler(x, y, z);
                            float s = Score();
                            if (s < bestScore) { bestScore = s; bestE = new Vector3(x, y, z); }
                        }
                // Report every metric at the winner so the verdict is corroborated, not single-metric.
                lThumbBone.localRotation = saved * Quaternion.Euler(bestE);
                var pBest = Protrusion(smr, lHand, lThumb, lBlock);
                var sBest = Standoff(smr, lThumb, lBlock);
                float aBest = ShapeAsymmetry(smr, lHand, rHand, lThumb, rThumb);
                lThumbBone.localRotation = saved;
                sb.AppendLine($"[blockhands]   BEST leftThumbEuler (ADDITIVE on top of the shipped dial-7 left thumb) = " +
                              $"({bestE.x:F0},{bestE.y:F0},{bestE.z:F0})");
                sb.AppendLine($"[blockhands]     protrusion      {pL0.max:F2}mm -> {pBest.max:F2}mm (outside {pL0.frac:P0} -> {pBest.frac:P0})");
                sb.AppendLine($"[blockhands]     standoff        {sL.max:F2}mm -> {sBest.max:F2}mm");
                sb.AppendLine($"[blockhands]     shape-asymmetry {asym0:F2}mm -> {aBest:F2}mm  <- the number that must approach 0");
                // The bake-ready ABSOLUTE value: the shipped dial-7 left thumb composed with the winning additive,
                // read back as a single euler so it can replace CastawayV4LeftThumbEuler directly.
                Vector3 abs = (Quaternion.Euler(MovementCameraScene.CastawayV4LeftThumbEuler)
                               * Quaternion.Euler(bestE)).eulerAngles;
                sb.AppendLine($"[blockhands]   ABSOLUTE leftThumbEuler to bake = ({abs.x:F1},{abs.y:F1},{abs.z:F1})");
            }

            Object.DestroyImmediate(go);
        }

        // Instantiate v4, tick the LIVE Animator to the shipped idle pose, then apply the four shipped
        // CastawayHandPose eulers exactly as the order-65 driver does — so every round-10 number is measured on
        // the pose the SPONSOR ACTUALLY SEES, not on the bind pose (the live-skeleton rule from
        // procedural-animation-verbs.md: only the rendered skeleton is what the player sees).
        private static GameObject PoseShipped(string path, RuntimeAnimatorController ctrl, StringBuilder sb)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) { sb.AppendLine($"[blockhands] v4 fbx missing at {path}"); return null; }
            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) avatar = a;
            var go = Object.Instantiate(fbx);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            if (ctrl != null)
            {
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Rebind();
                anim.Play("Idle", 0, 0.25f);
                anim.SetBool("Moving", false);
                anim.SetBool("Grounded", true);
                for (int i = 0; i < 12; i++) anim.Update(0.1f);
            }
            // CastawayHandPose (order 65) composition, replicated: bone.localRotation *= Euler(offset).
            ApplyEuler(FindBone(go.transform, "righthand"), MovementCameraScene.CastawayV4RightWristEuler);
            ApplyEuler(FindBone(go.transform, "lefthand"), MovementCameraScene.CastawayV4LeftWristEuler);
            ApplyEuler(FindBone(go.transform, "righthandthumb1"), MovementCameraScene.CastawayV4RightThumbEuler);
            ApplyEuler(FindBone(go.transform, "lefthandthumb1"), MovementCameraScene.CastawayV4LeftThumbEuler);
            sb.AppendLine($"[blockhands] posed at shipped dial-7: rightWrist={NormE(MovementCameraScene.CastawayV4RightWristEuler)} " +
                          $"leftWrist={NormE(MovementCameraScene.CastawayV4LeftWristEuler)} " +
                          $"rightThumb={NormE(MovementCameraScene.CastawayV4RightThumbEuler)} " +
                          $"leftThumb={NormE(MovementCameraScene.CastawayV4LeftThumbEuler)}");
            return go;
        }

        private static void ApplyEuler(Transform bone, Vector3 euler)
        {
            if (bone == null || euler == Vector3.zero) return;
            bone.localRotation = bone.localRotation * Quaternion.Euler(euler);
        }

        // PROTRUSION — the anchor metric. Build the axis-aligned box of the hand BLOCK verts in the HAND BONE's
        // own frame, then measure how far each THUMB vert sits OUTSIDE that box. max = the worst protruding
        // corner (what the eye catches as "a thumb"); frac = share of thumb verts outside the block at all.
        private static (float max, float mean, float frac) Protrusion(
            SkinnedMeshRenderer smr, Transform handBone, List<int> thumbIdx, List<int> blockIdx)
        {
            if (thumbIdx.Count == 0 || blockIdx.Count == 0) return (0f, 0f, 0f);
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var bv = baked.vertices;
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            Vector3 HandLocal(int v) => handBone.InverseTransformPoint(l2w.MultiplyPoint3x4(bv[v]));

            Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
            foreach (int v in blockIdx) { Vector3 p = HandLocal(v); mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }

            float max = 0f, sum = 0f; int outside = 0;
            // Scale factor: hand-bone local units -> millimetres (the bone carries the avatar's lossy scale).
            float u2mm = handBone.lossyScale.magnitude / Mathf.Sqrt(3f) * 1000f;
            foreach (int v in thumbIdx)
            {
                Vector3 p = HandLocal(v);
                // Distance outside the AABB (0 when inside).
                Vector3 d = Vector3.Max(mn - p, p - mx);
                d = Vector3.Max(d, Vector3.zero);
                float mm = d.magnitude * u2mm;
                sum += mm; if (mm > max) max = mm; if (mm > 0.05f) outside++;
            }
            Object.DestroyImmediate(baked);
            return (max, sum / thumbIdx.Count, (float)outside / thumbIdx.Count);
        }

        // STANDOFF — hull-free companion to Protrusion. For each thumb vert, the distance to the NEAREST block
        // vert, in world millimetres. A thumb tucked flush against/into the block has a small max; a lump
        // sticking out has a large one (its tip verts have no block vert near them). No hull assumption at all.
        private static (float max, float mean) Standoff(SkinnedMeshRenderer smr, List<int> thumbIdx, List<int> blockIdx)
        {
            if (thumbIdx.Count == 0 || blockIdx.Count == 0) return (0f, 0f);
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var bv = baked.vertices;
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            var blockW = new List<Vector3>(blockIdx.Count);
            foreach (int v in blockIdx) blockW.Add(l2w.MultiplyPoint3x4(bv[v]));
            float max = 0f, sum = 0f;
            foreach (int v in thumbIdx)
            {
                Vector3 p = l2w.MultiplyPoint3x4(bv[v]);
                float best = float.MaxValue;
                foreach (var b in blockW) { float d = (b - p).sqrMagnitude; if (d < best) best = d; }
                float mm = Mathf.Sqrt(best) * 1000f;
                sum += mm; if (mm > max) max = mm;
            }
            Object.DestroyImmediate(baked);
            return (max, sum / thumbIdx.Count);
        }

        // SHAPE-ASYMMETRY — "is one hand the same as the other?" as a single number. Each hand's thumb verts are
        // expressed in ITS OWN hand-bone local frame (so arm placement/orientation cancels out), the left set is
        // mirrored across local X, and each right vert is nearest-matched to it. Mean distance in mm: ~0 means
        // the two hands present the SAME thumb shape; large means they differ, which is exactly the defect.
        private static float ShapeAsymmetry(SkinnedMeshRenderer smr, Transform lHand, Transform rHand,
                                            List<int> lThumb, List<int> rThumb)
        {
            if (lThumb.Count == 0 || rThumb.Count == 0) return -1f;
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var bv = baked.vertices;
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            float u2mm = rHand.lossyScale.magnitude / Mathf.Sqrt(3f) * 1000f;
            var lSet = new List<Vector3>(lThumb.Count);
            foreach (int v in lThumb)
            {
                Vector3 p = lHand.InverseTransformPoint(l2w.MultiplyPoint3x4(bv[v]));
                lSet.Add(new Vector3(-p.x, p.y, p.z)); // mirror the left hand's own frame across X
            }
            float sum = 0f;
            foreach (int v in rThumb)
            {
                Vector3 p = rHand.InverseTransformPoint(l2w.MultiplyPoint3x4(bv[v]));
                float best = float.MaxValue;
                foreach (var l in lSet) { float d = (l - p).sqrMagnitude; if (d < best) best = d; }
                sum += Mathf.Sqrt(best) * u2mm;
            }
            Object.DestroyImmediate(baked);
            return sum / rThumb.Count;
        }

        // Protrusion measured WHILE a candidate bone rotation is applied (restores the bone afterwards).
        private static (float max, float mean, float frac) ProtrusionWith(
            SkinnedMeshRenderer smr, Transform bone, char axis, float deg,
            Transform handBone, List<int> thumbIdx, List<int> blockIdx)
        {
            Quaternion saved = bone.localRotation;
            bone.localRotation = saved * Quaternion.Euler(AxisEuler(axis, deg));
            var r = Protrusion(smr, handBone, thumbIdx, blockIdx);
            bone.localRotation = saved;
            return r;
        }

        // Displacement of a NAMED vert set (mm) when one bone is rotated — the scoping round-9's whole-mesh
        // average lacked. Restores the bone afterwards.
        private static (float mean, float max) ScopedDisp(
            SkinnedMeshRenderer smr, Transform bone, char axis, float deg, List<int> idx)
        {
            if (idx.Count == 0) return (0f, 0f);
            var before = new Mesh(); smr.BakeMesh(before, true); var bv = before.vertices;
            Quaternion saved = bone.localRotation;
            bone.localRotation = saved * Quaternion.Euler(AxisEuler(axis, deg));
            var after = new Mesh(); smr.BakeMesh(after, true); var av = after.vertices;
            bone.localRotation = saved;

            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            float sum = 0f, max = 0f;
            foreach (int v in idx)
            {
                if (v >= bv.Length || v >= av.Length) continue;
                float d = l2w.MultiplyVector(av[v] - bv[v]).magnitude * 1000f;
                sum += d; if (d > max) max = d;
            }
            Object.DestroyImmediate(before); Object.DestroyImmediate(after);
            return (sum / idx.Count, max);
        }

        private static Vector3 AxisEuler(char axis, float deg)
            => axis == 'X' ? new Vector3(deg, 0f, 0f)
             : axis == 'Y' ? new Vector3(0f, deg, 0f)
             : new Vector3(0f, 0f, deg);

        // ---- shared ----
        private static GameObject Instantiate(string path)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return fbx != null ? Object.Instantiate(fbx) : null;
        }
        private static AnimationClip LoadClip(string fbxPath, string clipName)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (o is AnimationClip c && c.name == clipName) return c;
            return null;
        }
        private static Transform FindBone(Transform root, string tok)
        {
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null)
                foreach (var b in smr.bones) if (b != null && Tok(b.name) == tok) return b;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (Tok(t.name) == tok) return t;
            return null;
        }
        private static string Tok(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            n = n.ToLowerInvariant(); int c = n.LastIndexOf(':'); return c >= 0 ? n.Substring(c + 1) : n;
        }
        private static Vector3 Horiz(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        private static float SignedYaw(Vector3 fromH, Vector3 toH)
        {
            float a = Vector3.SignedAngle(fromH, toH, Vector3.up); return a;
        }
        private static string NormE(Vector3 e)
        {
            return $"({N(e.x):F1},{N(e.y):F1},{N(e.z):F1})";
        }
        private static float N(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }
        private static int CountUnweighted(BoneWeight[] bw)
        {
            int n = 0; foreach (var w in bw) if (w.weight0 + w.weight1 + w.weight2 + w.weight3 < 0.5f) n++; return n;
        }
        private static IEnumerable<string> DictStr(Dictionary<string, int> d)
        {
            foreach (var kv in d) yield return $"{kv.Key}:{kv.Value}";
        }
    }
}
