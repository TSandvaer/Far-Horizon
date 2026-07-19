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
