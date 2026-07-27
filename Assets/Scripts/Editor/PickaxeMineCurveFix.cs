using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// IN-PIPELINE pickaxe-mine clip repair (86cav8xg9) — the surgical fix for the CONTORTED body the Sponsor saw
    /// at soak-5 in the <c>CastawayPickaxeSwing</c> (Attack_Pickaxe.fbx) mine strike.
    ///
    /// REAL-WORLD ANCHOR: mining a waist-to-chest-height BOULDER with a pickaxe is a swing performed by a person
    /// standing UPRIGHT — the torso stays roughly vertical and the arms + shoulders do the arc. A miner does NOT
    /// double over to the ground to hit a rock at his own chest height. The build must satisfy that sentence.
    ///
    /// DIAGNOSIS (measured, not assumed — <see cref="AttackClipPoseDiag"/> on the LIVE v4 rig, all five per-class
    /// attack clips side by side; raw logs quoted in the PR body):
    ///   • The clip bends the torso to a peak <b>66.3° off vertical</b> at t≈0.564 and holds &gt;60° for ~0.05 of
    ///     the clip. The four sibling clips peak at axe 43.3° / spear 27° / sword 25° / dagger 19° — so the
    ///     pickaxe folds <b>23° deeper than the deepest sibling</b>, and the axe swing at 43° is a pose the
    ///     Sponsor has NOT flagged. At that frame the right hand plunges a full shoulder-width below the chest
    ///     plane (upR −0.99): the clip is a swing at the FEET, not at a boulder.
    ///   • <b>The fold is owned by ONE bone: <c>mixamorig:Hips</c></b> — reverting Hips alone to its frame-0 pose
    ///     at the peak frame drops the tilt 66.3° → 19.9° (−46.4°), on a 104.8° own-local deviation. <c>Spine</c>
    ///     contributes only 7.5°, and <c>Spine1</c>/<c>Spine2</c> move &lt;1°. So the body hinges RIGIDLY at the
    ///     pelvis with an almost unbent back — which is exactly what reads as "contorted": a plank pivoting at
    ///     the hip rather than a person bending.
    ///
    /// TWO HYPOTHESES THIS REFUTES (both were on the table; both are wrong — do not re-try them):
    ///   1. <b>The ticket's framing — "corrupted bone curves", repair via the slerp-resample.</b> REFUTED: there
    ///      is no corruption. Sampled at the clip's own authored 30fps frame step across the whole transition, the
    ///      max per-frame whole-skeleton delta ramps smoothly and peaks at 20.8°, with no isolated spike. The
    ///      #197 defect the <see cref="SneakGaitCurveFix"/> resampler targets was 80.5° in ONE frame against
    ///      single-digit neighbours — that instrument would find nothing here.
    ///   2. <b>"The LEFT arm is flung out to the side."</b> REFUTED: the pickaxe clip has the LOWEST left-hand
    ///      outward peak of all five clips (outL 0.62 shoulder-widths vs axe 1.43 / spear 1.48 / sword 1.48 /
    ///      dagger 1.29), and its LeftArm local deviation (82°) is exceeded by the unflagged spear (90°) and
    ///      sword (83°). The left arm is the most TUCKED-IN of the set, not flung.
    ///
    /// ROUTE: the ticket orders Route 1 (re-source a cleaner Mixamo clip) before Route 2. Route 1 is not
    /// available from inside this task — a Mixamo download needs the Sponsor's Adobe session, and no committed
    /// FBX in <c>Assets/Art/Character/Castaway/</c> is an alternative upright mine take. So this is Route 2, done
    /// against the MEASURED cause.
    ///
    /// THE REPAIR — scale down the pelvis hinge, and keep the legs where they were:
    ///   (a) Blend every <c>Hips</c> per-key local ROTATION toward that clip's own frame-0 anchor by
    ///       <see cref="HipFoldBlend"/>. Because the deviation-from-anchor is ~0 at the clip's start and end, this
    ///       is seam-free BY CONSTRUCTION — it cannot introduce a window-edge pop (guarded: the repaired clip's
    ///       max per-frame step may not exceed the raw clip's).
    ///   (b) EXACTLY compensate both upper legs so the lower body keeps its world orientation:
    ///       <c>upLeg' = Inverse(H') * H * upLeg</c>. Without this, un-hinging the pelvis swings the legs with it
    ///       and lifts/slides the planted feet — trading a contorted torso for floating feet (the walk-float
    ///       saga's failure mode, `unity-conventions.md` §FBX/rigs). With it, the legs' world pose is preserved and
    ///       only the hip-joint ORIGINS shift (a few cm — measured in the PR).
    ///       ⚠ The compensation is evaluated BY TIME, never by key index: measured on this clip, <c>Hips</c>
    ///       carries <b>24</b> rotation keys while <c>LeftUpLeg</c> carries <b>27</b> — the bones are NOT on a
    ///       shared key grid, so an index-paired correction mis-pairs keys. A first pass that SKIPPED the
    ///       mismatched leg let the left foot's horizontal travel nearly DOUBLE (0.286m → 0.560m across the clip)
    ///       — caught by the foot-plant read in <see cref="AttackClipPoseDiag"/> before it could reach a soak, and
    ///       now guarded by <c>PickaxeMineClipUprightTests</c>. Both hips rotations are sampled from the actual
    ///       SOURCE and DESTINATION curves at each leg key's own time, so the correction matches the pose the
    ///       engine really interpolates rather than a key-index approximation of it.
    ///   (c) The SPINE is deliberately LEFT ALONE. Its 26.5° flexion is the anatomically-correct part of a bend;
    ///       removing the pelvis plank while keeping the back's own curve is what makes the pose read like a
    ///       person. Every other curve — arms, shoulders, head, legs below the upper leg, hips POSITION / root
    ///       motion — is copied VERBATIM.
    ///
    /// WHY A GENERATED .anim (not a Blender FBX re-export): a ModelImporter FBX clip's curves are read-only, so
    /// the fix reads the imported clip's curves and writes an editable {RepairedClipPath}. This keeps the edit in
    /// Unity's own coordinate space — NO Blender armature rest rebake, which for an already-rigged Mixamo
    /// character is a CONFIRMED dead end (the whole-skeleton "helicopter", `character-pipeline.md` Step 3, canary
    /// <c>CastawayCharacterTests.RiggedCastawayFbx_IsGenuineMixamoExport…</c>). It binds by the SAME transform
    /// path, the FBX + its <c>.meta</c> are UNTOUCHED (<c>animationType: 2</c> / Generic — verified), and it is
    /// idempotent + reproducible-from-code (the bootstrap re-runs it; the committed .anim ships the fix,
    /// [[unity-procedural-committed-assets-go-stale]]). <c>BuildAnimatorController</c> points the AttackPickaxe
    /// state at this .anim instead of the raw FBX clip — the SAME clip-swap <see cref="SneakGaitCurveFix"/> does
    /// for CrouchWalk. NOT an Animator re-wire: same state, same transitions, same parameters, different clip
    /// asset. No re-seat, no <c>HeldAxeRig</c>/<c>CastawayArmPose</c> change.
    /// </summary>
    public static class PickaxeMineCurveFix
    {
        // The editable repaired clip the controller's AttackPickaxe state binds instead of the raw FBX take.
        public const string RepairedClipPath = "Assets/Art/Character/Castaway/CastawayPickaxeSwing_repaired.anim";
        public const string RepairedClipName = "CastawayPickaxeSwing_repaired";

        /// <summary>
        /// Blend factor pulling each Hips key toward the clip's own frame-0 anchor rotation. 0 = raw (66.3° peak
        /// fold), 1 = the pelvis never hinges at all (a rigid, lifeless swing). CALIBRATED BY MEASUREMENT, not
        /// guessed, and not linear in K — the surviving tilt also carries the untouched Spine flexion, so the
        /// response had to be measured rather than derived: K=0.45 landed a 47.0° peak (raw 64.1°), i.e. ≈38° of
        /// tilt per unit K. The target is the axe swing's 43.3° peak — the deepest fold in the set the Sponsor has
        /// NOT called contorted — so K=0.58 lands ≈42°, just inside that band, while still keeping ~42% of the
        /// pelvis hinge plus ALL of the spine's own flexion, so the swing reads as a real body swing and not a
        /// stiff arms-only gesture. Re-measure with <see cref="AttackClipPoseDiag"/> if the source clip is ever
        /// re-imported; <c>PickaxeMineClipUprightTests</c> reds if the achieved peak leaves the band.
        /// </summary>
        public const float HipFoldBlend = 0.58f;

        /// <summary>The ceiling the repaired clip's peak torso tilt must respect (deg off vertical). Set just
        /// above the axe clip's unflagged 43.3° peak so the guard has a little numerical headroom without
        /// admitting a visibly deeper fold.</summary>
        public const float PeakTiltCeilingDeg = 46f;

        // The pelvis bone that OWNS the fold (measured: 46.4° of the 66.3° peak tilt), and the two upper legs the
        // compensation keeps world-fixed. Matched on the curve path's OWN last segment so a parent segment can
        // never sweep a sibling in, and "LeftUpLeg" cannot match "LeftLeg".
        private const string HipsBone = "Hips";
        private static readonly string[] UpLegBones = { "LeftUpLeg", "RightUpLeg" };

        [MenuItem("FarHorizon/Fix/Repair Pickaxe Mine Pelvis Fold (generate .anim)")]
        public static void RunMenu()
        {
            var sb = new StringBuilder();
            bool ok = Generate(sb);
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Reads the raw FBX CastawayPickaxeSwing clip, scales down the pelvis fold with an exact upper-leg
        /// compensation, writes the editable .anim. Returns true on success. Call from PrepareCharacter AFTER the
        /// attack-clip FBX import (it reads the imported clip) and BEFORE BuildAnimatorController (which binds it).
        /// </summary>
        public static bool Generate(StringBuilder sb)
        {
            sb.AppendLine("[pickaxe-fix] ===== REPAIR PICKAXE MINE PELVIS FOLD (86cav8xg9) =====");
            AnimationClip src = FindClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip);
            if (src == null)
            {
                sb.AppendLine("[pickaxe-fix] ERROR: raw CastawayPickaxeSwing clip not found @ " +
                              CharacterAssetGen.AttackPickaxeFbxPath);
                return false;
            }
            sb.AppendLine($"[pickaxe-fix] source clip={src.name} len={src.length:F4}s fps={src.frameRate:F0} " +
                          $"K={HipFoldBlend:F2}");

            var dst = new AnimationClip { name = RepairedClipName, frameRate = src.frameRate };
            // preserve the clip's import settings (one-shot: loop=false etc.).
            var srcSettings = AnimationUtility.GetAnimationClipSettings(src);
            AnimationUtility.SetAnimationClipSettings(dst, srcSettings);

            // Split the bindings: the Hips rotation quad, each upper-leg rotation quad, everything else verbatim.
            QuatGroup hipsRot = null;
            var upLegRot = new Dictionary<string, QuatGroup>();
            var passthrough = new List<EditorCurveBinding>();
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                int comp = QuatComponent(b.propertyName);
                string seg = LastSeg(b.path);
                if (comp >= 0 && seg.EndsWith(HipsBone))
                {
                    hipsRot = hipsRot ?? new QuatGroup(b.path);
                    hipsRot.Set(comp, b, AnimationUtility.GetEditorCurve(src, b));
                }
                else if (comp >= 0 && MatchesUpLeg(seg))
                {
                    if (!upLegRot.TryGetValue(b.path, out var g)) { g = new QuatGroup(b.path); upLegRot[b.path] = g; }
                    g.Set(comp, b, AnimationUtility.GetEditorCurve(src, b));
                }
                else passthrough.Add(b);
            }

            // 1) verbatim copy every non-Hips-rotation / non-upperleg-rotation curve (arms, shoulders, head,
            //    spine, lower legs, hips POSITION / root motion...).
            int copied = 0;
            foreach (var b in passthrough)
            {
                var c = AnimationUtility.GetEditorCurve(src, b);
                if (c != null) { AnimationUtility.SetEditorCurve(dst, b, c); copied++; }
            }

            if (hipsRot == null || !hipsRot.Complete)
            {
                sb.AppendLine("[pickaxe-fix] ERROR: no complete Hips rotation quaternion group found — the clip's " +
                              "bone paths may have changed. Nothing blended; every curve copied verbatim. FIX INERT.");
                hipsRot?.CopyVerbatim(dst, ref copied);
                foreach (var g in upLegRot.Values) g.CopyVerbatim(dst, ref copied);
                WriteAsset(dst);
                sb.AppendLine($"[pickaxe-fix] copied {copied} curves verbatim; wrote {RepairedClipPath}");
                return false;
            }

            // 2) blend the Hips rotation toward its frame-0 anchor, recording the per-key correction delta so the
            //    upper legs can be compensated EXACTLY (upLeg' = Inverse(H') * H * upLeg keeps their world pose).
            int n = hipsRot.KeyCount;
            var hipsOut = new Quaternion[n];
            Quaternion anchor = hipsRot.Read(0).normalized;
            float devBefore = 0f, devAfter = 0f;
            Quaternion prev = anchor;
            for (int i = 0; i < n; i++)
            {
                Quaternion q = hipsRot.Read(i).normalized;
                devBefore = Mathf.Max(devBefore, Quaternion.Angle(q, anchor));
                Quaternion qb = Quaternion.Slerp(q, anchor, HipFoldBlend).normalized;
                // hemisphere continuity: the 4 component curves are interpolated as SCALARS, so a sign flip
                // between neighbouring keys kinks them even though the rotation is identical.
                if (Quaternion.Dot(qb, prev) < 0f) qb = new Quaternion(-qb.x, -qb.y, -qb.z, -qb.w);
                prev = qb;
                hipsOut[i] = qb;
                devAfter = Mathf.Max(devAfter, Quaternion.Angle(qb, anchor));
            }
            hipsRot.Write(dst, hipsOut);
            sb.AppendLine($"[pickaxe-fix]   {LastSeg(hipsRot.Path)}: max dev-from-anchor {devBefore:F1}deg -> " +
                          $"{devAfter:F1}deg over {n} keys");

            // 3) compensate the upper legs so the planted feet do not swing with the un-hinged pelvis. Evaluated BY
            //    TIME against the SOURCE and the just-written DESTINATION hips curves — the bones are NOT on a
            //    shared key grid (Hips 24 keys vs LeftUpLeg 27 on this clip), so a per-INDEX pairing is wrong and
            //    silently swings one leg (measured: left-foot travel 0.286m -> 0.560m).
            int compensated = 0;
            float worstLegCorrection = 0f;
            var dstHips = hipsRot.ReadBack(dst);
            // RESAMPLE the compensated legs onto the clip's own authored FRAME grid rather than re-using their
            // sparse key times. The correction is exact only AT the times it is evaluated; with the bones on
            // different sparse grids (~0.2s apart here) the engine interpolates BETWEEN those times and the world
            // orientation drifts mid-interval. Measured: keeping the leg's own 27 keys still left the left foot
            // +10.2cm of travel; on the frame grid the correction is exact at every frame the engine renders.
            float fps = src.frameRate > 0f ? src.frameRate : 30f;
            int frames = Mathf.Max(1, Mathf.RoundToInt(src.length * fps));
            foreach (var g in upLegRot.Values)
            {
                if (!g.Complete) { g.CopyVerbatim(dst, ref copied); continue; }
                var times = new float[frames + 1];
                var outQ = new Quaternion[frames + 1];
                Quaternion prevLeg = Quaternion.identity;
                for (int f = 0; f <= frames; f++)
                {
                    float t = Mathf.Min(src.length, f / fps);
                    times[f] = t;
                    Quaternion hRaw = hipsRot.EvaluateAt(t).normalized;   // the pose the source clip renders at t
                    Quaternion hFix = dstHips.Evaluate(t).normalized;     // the pose the REPAIRED clip renders at t
                    Quaternion leg = g.EvaluateAt(t).normalized;          // the leg pose the source renders at t
                    // world_leg = H * leg must be preserved: leg' = Inverse(H') * H * leg. Sign-agnostic (q and -q
                    // are the same rotation), so no hemisphere handling is needed on the correction itself.
                    Quaternion legFixed = (Quaternion.Inverse(hFix) * hRaw * leg).normalized;
                    worstLegCorrection = Mathf.Max(worstLegCorrection, Quaternion.Angle(legFixed, leg));
                    // ...but the WRITTEN curves are interpolated as scalars, so keep their hemisphere continuous.
                    if (f > 0 && Quaternion.Dot(legFixed, prevLeg) < 0f)
                        legFixed = new Quaternion(-legFixed.x, -legFixed.y, -legFixed.z, -legFixed.w);
                    prevLeg = legFixed;
                    outQ[f] = legFixed;
                }
                g.WriteResampled(dst, times, outQ);
                compensated++;
                sb.AppendLine($"[pickaxe-fix]   {LastSeg(g.Path),-22} compensated: {g.KeyCount} source keys -> " +
                              $"{frames + 1} frame-grid keys (hips has {n})");
            }
            sb.AppendLine($"[pickaxe-fix]   compensated {compensated} upper-leg group(s); worst leg local " +
                          $"correction {worstLegCorrection:F1}deg (this is the pelvis correction re-expressed in " +
                          "the leg's frame — the leg's WORLD orientation is unchanged by construction)");
            if (compensated != 2)
                sb.AppendLine($"[pickaxe-fix] WARNING: expected 2 upper-leg groups, compensated {compensated} — " +
                              "an UNcompensated leg swings with the pelvis and slides the foot. Verify the " +
                              "foot-plant read before serving a soak.");

            WriteAsset(dst);
            sb.AppendLine($"[pickaxe-fix] copied {copied} curves verbatim. Wrote {RepairedClipPath}");
            if (compensated == 0)
                sb.AppendLine("[pickaxe-fix] WARNING: NO upper-leg groups compensated — verify the foot plant in " +
                              "the shipped capture before serving a soak.");
            return true;
        }

        private static void WriteAsset(AnimationClip dst)
        {
            // overwrite in place if present, so the .meta GUID (and every reference to it) survives a re-gen.
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(RepairedClipPath);
            if (existing != null) EditorUtility.CopySerialized(dst, existing);
            else AssetDatabase.CreateAsset(dst, RepairedClipPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RepairedClipPath);
        }

        /// <summary>-1 if the property is not a local-rotation quaternion component; else 0=x 1=y 2=z 3=w.</summary>
        public static int QuatComponent(string propertyName)
        {
            switch (propertyName.ToLowerInvariant())
            {
                case "m_localrotation.x": return 0;
                case "m_localrotation.y": return 1;
                case "m_localrotation.z": return 2;
                case "m_localrotation.w": return 3;
                default: return -1;
            }
        }

        public static bool MatchesUpLeg(string lastSegment)
        {
            foreach (var bone in UpLegBones) if (lastSegment.EndsWith(bone)) return true;
            return false;
        }

        public static string LastSeg(string path)
        {
            int i = path.LastIndexOf('/');
            return i >= 0 ? path.Substring(i + 1) : path;
        }

        private static AnimationClip FindClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }

        /// <summary>The 4 component curves of ONE bone's local-rotation quaternion, kept together so the rotation
        /// is edited as a unit (per-scalar edits de-normalize it) and each key's original TIME + tangents survive.</summary>
        private sealed class QuatGroup
        {
            public readonly string Path;
            private readonly EditorCurveBinding[] _b = new EditorCurveBinding[4];
            private readonly AnimationCurve[] _c = new AnimationCurve[4];

            public QuatGroup(string path) { Path = path; }

            public void Set(int comp, EditorCurveBinding b, AnimationCurve c) { _b[comp] = b; _c[comp] = c; }

            public bool Complete => _c[0] != null && _c[1] != null && _c[2] != null && _c[3] != null &&
                                    _c[0].length > 0 &&
                                    _c[1].length == _c[0].length && _c[2].length == _c[0].length &&
                                    _c[3].length == _c[0].length;

            public int KeyCount => _c[0] != null ? _c[0].length : 0;

            public Quaternion Read(int i) =>
                new Quaternion(_c[0].keys[i].value, _c[1].keys[i].value, _c[2].keys[i].value, _c[3].keys[i].value);

            public float KeyTime(int i) => _c[0].keys[i].time;

            /// <summary>The SOURCE rotation interpolated at an arbitrary time (for cross-bone, cross-key-grid work).</summary>
            public Quaternion EvaluateAt(float t) =>
                new Quaternion(_c[0].Evaluate(t), _c[1].Evaluate(t), _c[2].Evaluate(t), _c[3].Evaluate(t));

            /// <summary>Read the curves back OUT of the destination clip, so a later pass can ask what the repaired
            /// clip actually renders at a given time (splines + re-smoothed tangents, not the raw key values).</summary>
            public Sampler ReadBack(AnimationClip dst)
            {
                var c = new AnimationCurve[4];
                for (int k = 0; k < 4; k++) c[k] = AnimationUtility.GetEditorCurve(dst, _b[k]);
                return new Sampler(c);
            }

            /// <summary>A read-only 4-curve quaternion sampler over an already-written clip.</summary>
            public sealed class Sampler
            {
                private readonly AnimationCurve[] _c;
                public Sampler(AnimationCurve[] c) { _c = c; }
                public Quaternion Evaluate(float t) =>
                    new Quaternion(_c[0].Evaluate(t), _c[1].Evaluate(t), _c[2].Evaluate(t), _c[3].Evaluate(t));
            }

            public void CopyVerbatim(AnimationClip dst, ref int copied)
            {
                for (int k = 0; k < 4; k++)
                    if (_c[k] != null) { AnimationUtility.SetEditorCurve(dst, _b[k], _c[k]); copied++; }
            }

            /// <summary>Write edited quaternions on a NEW time grid (used for the resampled leg compensation, whose
            /// correction must be exact at every frame the engine renders, not only at the source's sparse keys).</summary>
            public void WriteResampled(AnimationClip dst, float[] times, Quaternion[] q)
            {
                int n = q.Length;
                for (int k = 0; k < 4; k++)
                {
                    var keys = new Keyframe[n];
                    for (int i = 0; i < n; i++)
                        keys[i] = new Keyframe(times[i],
                            k == 0 ? q[i].x : k == 1 ? q[i].y : k == 2 ? q[i].z : q[i].w);
                    var curve = new AnimationCurve(keys);
                    for (int i = 0; i < n; i++) curve.SmoothTangents(i, 0f);
                    AnimationUtility.SetEditorCurve(dst, _b[k], curve);
                }
            }

            /// <summary>Write the edited quaternions back, preserving every key's original TIME. Tangents are
            /// re-smoothed because the values moved; the guard test asserts this adds no per-frame step.</summary>
            public void Write(AnimationClip dst, Quaternion[] q)
            {
                int n = q.Length;
                for (int k = 0; k < 4; k++)
                {
                    var keys = new Keyframe[n];
                    for (int i = 0; i < n; i++)
                    {
                        keys[i] = _c[k].keys[i];
                        keys[i].value = k == 0 ? q[i].x : k == 1 ? q[i].y : k == 2 ? q[i].z : q[i].w;
                    }
                    var curve = new AnimationCurve(keys);
                    for (int i = 0; i < n; i++) curve.SmoothTangents(i, 0f);
                    AnimationUtility.SetEditorCurve(dst, _b[k], curve);
                }
            }
        }
    }
}
