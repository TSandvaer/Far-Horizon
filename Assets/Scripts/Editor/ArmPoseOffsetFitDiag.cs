using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// MEASUREMENT instrument for ticket 86caxgwbz — "CastawayArmPose applies idle-carry arm eulers
    /// unconditionally on every clip frame (per-clip-pose-range trap)".
    ///
    /// WHAT IT ANSWERS. <see cref="FarHorizon.CastawayArmPose"/> right-multiplies a FIXED local-euler offset onto
    /// the upper-arm bones in LateUpdate, on EVERY frame of EVERY clip, with the values dialed against the IDLE
    /// carry. This measures, per LIVE clip, HOW FAR that fixed offset moves each hand and WHERE it moves it —
    /// so "fits vs distorts" is a number per clip instead of a guess.
    ///
    /// THE MECHANISM (why a per-clip range CAN matter even though the offset is constant). With
    /// bone.localRotation = R_clip and the offset Q right-multiplied, the hand's position relative to the
    /// SHOULDER goes from R_clip*u to R_clip*Q*u, where u is the hand's position expressed in the upper-arm
    /// bone's own frame — i.e. the arm's INTERNAL fold (elbow flexion + forearm twist) written by the clip.
    /// So the DISPLACEMENT MAGNITUDE |u - Q*u| and the shoulder ARC angle(u, Q*u) depend ONLY on the internal
    /// fold, not on where the clip points the arm. A straight arm whose axis is near Q's axis barely moves; a
    /// folded arm swings. The idle carry is a near-straight arm — hence the trap hypothesis.
    /// The CONSEQUENCE (torso/head penetration) additionally depends on the full pose, so both layers are
    /// measured: magnitude (fold-driven) AND world clearance (pose-driven).
    ///
    /// HEADLESS-SAFE: poses clips via <c>AnimationClip.SampleAnimation</c> on the LIVE rig
    /// (<see cref="CharacterAssetGen.FbxPath"/>) — never an Animator tick (headless Time.deltaTime is approx 0,
    /// so the Animator never advances; procedural-animation-verbs.md / unity-conventions.md FBX-rig traps).
    ///
    /// Run:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.ArmPoseOffsetFitDiag.Run
    ///
    /// Read-only: instantiates a throwaway rig, destroys it; touches no importer, no asset, no scene.
    /// NO pose-chain behavior change — this ticket is measure-only.
    /// </summary>
    public static class ArmPoseOffsetFitDiag
    {
        private const int Samples = 25;   // 0..1 inclusive

        [MenuItem("FarHorizon/Diagnose/Arm-Pose Offset Fit (per-clip)")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[armfit] ===== CASTAWAY ARM-POSE OFFSET FIT, PER LIVE CLIP (86caxgwbz) =====");

            // ---- the SHIPPED offsets (what MovementCameraScene.AddArmPose bakes into Boot.unity) ----
            bool v4 = CharacterAssetGen.UseCastawayV4;
            Vector3 rEul = v4 ? MovementCameraScene.CastawayV4RightArmEuler : new Vector3(-4f, -50f, -3f);
            Vector3 lEul = v4 ? MovementCameraScene.CastawayV4LeftArmEuler : new Vector3(-5f, 22f, 0f);
            Vector3 runEul = MovementCameraScene.ArmRunLowerEuler;
            sb.AppendLine($"[armfit] rig={CharacterAssetGen.FbxPath}  UseCastawayV4={v4}");
            sb.AppendLine($"[armfit] shipped rightArmEuler={rEul.ToString("F1")}  leftArmEuler={lEul.ToString("F1")}" +
                          $"  runLowerEuler={runEul.ToString("F1")} (right arm only, weight 0..1 by IsRunning)");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            if (fbx == null)
            {
                sb.AppendLine("[armfit] ERROR: live rig FBX missing @ " + CharacterAssetGen.FbxPath);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var playerRoot = new GameObject("__armFitPlayer");
            var avatarRoot = new GameObject("__armFitAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * 1.8f;   // matches the shipped avatar scale
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
                sb.AppendLine("[armfit] ERROR: bone lookup failed. mixamorig bones present:");
                foreach (var k in bones.Keys) if (k.StartsWith("mixamorig:")) sb.AppendLine("[armfit]   " + k);
                Object.DestroyImmediate(playerRoot);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var clips = LiveClips(sb);

            sb.AppendLine("[armfit] --- LEGEND (all lengths normalised by SHOULDER WIDTH = scale-immune) ---");
            sb.AppendLine("[armfit]   arc  = max angle the offset swings the hand about its SHOULDER (deg). " +
                          "Fold-driven: a straight arm on the offset axis barely moves, a folded arm swings.");
            sb.AppendLine("[armfit]   d    = max hand DISPLACEMENT the offset causes (shoulder-widths) @t.");
            sb.AppendLine("[armfit]   dOut/dFwd/dUp = that displacement decomposed in the TORSO frame at the peak " +
                          "sample (+out = away from midline, +fwd = in front of chest, +up = toward the head).");
            sb.AppendLine("[armfit]   torso = MIN hand-to-torso-axis clearance over the clip, base -> posed. A DROP " +
                          "toward 0 = the offset drives the hand INTO the body.");
            sb.AppendLine("[armfit]   head  = MIN hand-to-HEAD clearance over the clip, base -> posed (the " +
                          "'axe into the head' family).");
            sb.AppendLine("[armfit]   elbow = min elbow interior angle base -> posed. INVARIANT by construction " +
                          "(the offset rigidly rotates the whole sub-chain) — printed as a self-check.");

            // Pass 1: the always-on offsets (what every clip frame gets, run weight 0).
            sb.AppendLine("[armfit] ===== PASS 1: ALWAYS-ON OFFSETS (runWeight=0) — right & left =====");
            var baseline = new Dictionary<string, Row>();
            foreach (var (label, clip) in clips)
            {
                if (clip == null) { sb.AppendLine($"[armfit] {label,-20} MISSING"); continue; }
                var row = Measure(label, clip, model, hips, head, lArm, lFore, lHand, rArm, rFore, rHand,
                                  Quaternion.Euler(rEul), Quaternion.Euler(lEul), Quaternion.identity);
                baseline[label] = row;
                sb.AppendLine(row.Format());
            }

            // Ratio-to-idle table: the dial's reference pose is the IDLE carry, so every other clip is judged
            // against it rather than against an invented absolute threshold.
            if (baseline.TryGetValue("idle(breathing)", out var idleRow))
            {
                sb.AppendLine("[armfit] ===== RATIO TO THE IDLE CARRY (the pose the eulers were dialed against) =====");
                foreach (var kv in baseline)
                {
                    var r = kv.Value;
                    sb.AppendLine($"[armfit] {kv.Key,-20} arcR x{Safe(r.ArcR, idleRow.ArcR):F2}  dR x{Safe(r.DR, idleRow.DR):F2}" +
                                  $"   arcL x{Safe(r.ArcL, idleRow.ArcL):F2}  dL x{Safe(r.DL, idleRow.DL):F2}");
                }
            }

            // Pass 2: run-lower engaged. PRE-86caxj30g, IsRunning was VELOCITY-driven (CastawayCharacter.LateUpdate)
            // and INDEPENDENT of which Animator state plays — so any overlay clip reachable while the agent is at
            // run speed (jump-running, hit-reacts, attacks-on-the-move, pick-up) inherited the full run-lower.
            sb.AppendLine("[armfit] ===== PASS 2: RIGHT ARM WITH RUN-LOWER AT FULL WEIGHT (runWeight=1) =====");
            sb.AppendLine("[armfit]   (the UNGATED worst case — what shipped BEFORE the 86caxj30g locomotion-lane gate)");
            foreach (var (label, clip) in clips)
            {
                if (clip == null) continue;
                var row = Measure(label, clip, model, hips, head, lArm, lFore, lHand, rArm, rFore, rHand,
                                  Quaternion.Euler(rEul), Quaternion.Euler(lEul), Quaternion.Euler(runEul));
                sb.AppendLine(row.FormatRightOnly());
            }

            // Pass 2G (86caxj30g): the same sweep, but the run-lower weight is the one the PRODUCTION GATE yields
            // for the Animator state that plays each clip — CastawayCharacter.IsLocomotionLaneState, the SAME
            // predicate CastawayArmPose.LateUpdate consumes (not a re-implementation). A clip whose state is
            // outside the locomotion lane gets weight 0, so its row must be NUMERICALLY IDENTICAL to its Pass-1
            // row. That identity IS the fix's acceptance evidence: Pass 2 collapses to the Pass-1 band on the
            // attack clips while the locomotion clips keep their lowered carry.
            sb.AppendLine("[armfit] ===== PASS 2G: RUN-LOWER AT THE PRODUCTION-GATED WEIGHT (86caxj30g) =====");
            sb.AppendLine("[armfit]   gate = CastawayCharacter.IsLocomotionLaneState(<the state that plays this clip>)");
            foreach (var (label, clip) in clips)
            {
                if (clip == null) continue;
                string state = StateForClip(label);
                bool lane = CastawayCharacter.IsLocomotionLaneState(state);
                var row = Measure(label, clip, model, hips, head, lArm, lFore, lHand, rArm, rFore, rHand,
                                  Quaternion.Euler(rEul), Quaternion.Euler(lEul),
                                  lane ? Quaternion.Euler(runEul) : Quaternion.identity);
                sb.AppendLine($"{row.FormatRightOnly()}   [state={state} lane={(lane ? "YES w=1" : "no  w=0")}]");
            }

            Object.DestroyImmediate(playerRoot);
            sb.AppendLine("[armfit] ===== END =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static float Safe(float v, float b) => Mathf.Abs(b) < 1e-6f ? -1f : v / b;

        // ===================================================================================================
        // 86caxj30g — LOCOMOTION-GATE TRACE. A SECOND entry point on the same instrument (reuse-before-rebuild).
        //
        // WHY A TRACE AND NOT A HYPOTHESIS. The fix's whole correctness rests on one empirical claim about Unity:
        // "GetCurrentAnimatorStateInfo(0) reports the state being transitioned FROM for the whole duration of a
        // transition, so a naive current-state-only gate engages LATE by the crossfade duration." That claim was
        // raised in review, not measured. This ticks the SHIPPED CastawayAnimator.controller with an explicit
        // positive delta (headless Time.deltaTime≈0 never advances a state machine — unity-conventions.md
        // §Headless) through a real AnyState→AttackAxe swing entry, and dumps per frame: IsInTransition, the
        // CURRENT state, the NEXT state, the naive current-only verdict, the shipped transition-PAIRED verdict,
        // and the resulting run-lower weight under the OLD velocity-only policy vs the NEW gated policy.
        //
        // Run:
        //   Unity.exe -batchmode -quit -nographics -projectPath . \
        //     -executeMethod FarHorizon.EditorTools.ArmPoseOffsetFitDiag.TraceLocomotionGate
        // ===================================================================================================

        /// <summary>Every layer-0 state name the shipped controller authors (CharacterAssetGen.BuildAnimatorController).
        /// AnimatorStateInfo exposes a hash, not a string, so resolution is by IsName over this set.</summary>
        private static readonly string[] AllStateNames =
        {
            CastawayCharacter.IdleState, CastawayCharacter.LocomotionState,
            CastawayCharacter.JumpIdleState, CastawayCharacter.JumpRunningState,
            "Attack", "AttackAxe", "AttackPickaxe", "AttackDagger", "AttackSpear", "AttackSword",
            "CrouchIdle", "CrouchWalk", "Stunned", "GettingUp", "PickingUp",
            "HitToBody", "HeadHit", "BigStomachHit", "StomachHit", "RibHit",
        };

        /// <summary>Which layer-0 state plays each clip in <see cref="LiveClips"/> — the mapping Pass 2G gates on.
        /// Derived from CharacterAssetGen.BuildAnimatorController's state→motion wiring.</summary>
        private static string StateForClip(string label)
        {
            switch (label)
            {
                case "idle(breathing)":      return CastawayCharacter.IdleState;
                case "walk":
                case "run":                  return CastawayCharacter.LocomotionState;
                case "jump_idle":            return CastawayCharacter.JumpIdleState;
                case "jump_running":         return CastawayCharacter.JumpRunningState;
                case "melee(chop)":          return "Attack";          // the RESERVED overhead (no incoming transition)
                case "atk_axe":              return "AttackAxe";
                case "atk_pickaxe":          return "AttackPickaxe";
                case "atk_dagger":           return "AttackDagger";
                case "atk_spear":            return "AttackSpear";
                case "atk_sword":            return "AttackSword";
                case "crouch_idle":          return "CrouchIdle";
                case "crouch_walk":
                case "crouch_walk_smoothed": return "CrouchWalk";
                case "getting_up":           return "GettingUp";
                case "picking_up":           return "PickingUp";
                case "stunned":              return "Stunned";
                case "hit_body":             return "HitToBody";
                case "hit_head":             return "HeadHit";
                case "hit_bigstomach":       return "BigStomachHit";
                case "hit_stomach":          return "StomachHit";
                case "hit_rib":              return "RibHit";
                default:                     return "<unmapped>";
            }
        }

        [MenuItem("FarHorizon/Diagnose/Arm-Pose Locomotion-Gate Trace (swing entry)")]
        public static void TraceLocomotionGate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[gatetrace] ===== LOCOMOTION-LANE GATE, LIVE ANIMATOR TICK (86caxj30g) =====");

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetGen.ControllerPath);
            if (controller == null)
            {
                sb.AppendLine("[gatetrace] ERROR: controller missing @ " + CharacterAssetGen.ControllerPath);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var go = new GameObject("__gateTraceRig");
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();

            // Settle into the locomotion lane at RUN speed — the exact live precondition for the defect
            // (sprinting, so IsRunning is true and the run-lower weight is at 1) before the swing is thrown.
            anim.SetBool(CastawayCharacter.GroundedParam, true);
            anim.SetBool(CastawayCharacter.MovingParam, true);
            anim.SetFloat(CastawayCharacter.SpeedParam, CharacterAssetGen.RunBlendSpeed);
            anim.SetFloat(CastawayCharacter.LocoSpeedMulParam, 1f);
            for (int i = 0; i < 40; i++) anim.Update(0.05f);
            sb.AppendLine($"[gatetrace] settled state='{StateName(anim)}' inTransition={anim.IsInTransition(0)}");

            const float Dt = 1f / 60f;
            const float RunLowerBlendRate = 8f;      // CastawayArmPose.runLowerBlendRate (shipped)
            const float OverlayReleaseRate = 30f;    // CastawayArmPose.runLowerOverlayReleaseRate (86caxj30g)

            // Both policies start from a fully-engaged run-lower (the player has been sprinting).
            float wOld = 1f;      // OLD: velocity-only. IsRunning stays true through the swing -> target stays 1.
            float wNaive = 1f;    // current-state-only gate, slow rate — what the AC as first written implied.
            float wNew = 1f;      // shipped: transition-paired gate + asymmetric release.

            // Throw the axe swing WHILE still sprinting: WeaponClass=axe, pulse the shared Chop trigger.
            anim.SetInteger(CharacterAssetGen.WeaponClassParam, CharacterAssetGen.WeaponClassAxe);
            anim.SetTrigger(CharacterAssetGen.ChopParam);
            sb.AppendLine("[gatetrace] --- Chop trigger fired (WeaponClass=axe) at t=0; dt=1/60s ---");
            sb.AppendLine("[gatetrace]  t(s) | inTr | current        | next           | naive | paired | wOld  wNaive wNew");

            for (int f = 0; f < 90; f++)   // 1.5s — past the 1.05s fastest swing wall-time
            {
                anim.Update(Dt);
                float t = (f + 1) * Dt;

                bool inTr = anim.IsInTransition(0);
                var cur = anim.GetCurrentAnimatorStateInfo(0);
                string curName = ResolveName(cur);
                string nextName = inTr ? ResolveName(anim.GetNextAnimatorStateInfo(0)) : "-";

                bool naive = CastawayCharacter.IsLocomotionLaneState(cur.shortNameHash);
                bool paired = CastawayCharacter.LocomotionLaneOwnsPoseFor(
                    cur.shortNameHash, inTr, inTr ? anim.GetNextAnimatorStateInfo(0).shortNameHash : 0);

                // OLD policy: velocity-only, so IsRunning (still true — the player never released sprint) keeps the
                // target at 1 for the whole swing. NAIVE: current-state-only + the slow rate. NEW: the shipped
                // transition-paired gate + the asymmetric release.
                wOld = CastawayArmPose.NextRunWeight(wOld, true, true, RunLowerBlendRate, OverlayReleaseRate, Dt);
                wNaive = CastawayArmPose.NextRunWeight(wNaive, true, naive, RunLowerBlendRate, RunLowerBlendRate, Dt);
                wNew = CastawayArmPose.NextRunWeight(wNew, true, paired, RunLowerBlendRate, OverlayReleaseRate, Dt);

                // Log every frame for the first 0.35s (the decision window), then every 6th.
                if (t <= 0.35f || f % 6 == 0)
                    sb.AppendLine($"[gatetrace] {t,5:F3} | {(inTr ? "YES" : " no"),4} | {curName,-14} | {nextName,-14} | " +
                                  $"{(naive ? "lane" : "OUT "),5} | {(paired ? "lane" : "OUT "),6} | " +
                                  $"{wOld:F3} {wNaive:F3}  {wNew:F3}");
            }

            // PHASE 2 — the RETURN. The swing's own exit-time return needs the full clip; force the crossfade back
            // to the locomotion lane and show the gate RE-OPENING (and easing back at the SLOW in-lane rate, not
            // the fast release rate — the asymmetry's other half). Regression evidence for 86caa83wn: the axe must
            // get its run-carry back once the swing hands the pose over.
            sb.AppendLine("[gatetrace] --- PHASE 2: CrossFade back to Locomotion (still sprinting) ---");
            anim.CrossFade(CastawayCharacter.LocomotionState, 0.10f, 0, 0f);
            for (int f = 0; f < 60; f++)
            {
                anim.Update(Dt);
                float t = (f + 1) * Dt;
                bool inTr = anim.IsInTransition(0);
                var cur = anim.GetCurrentAnimatorStateInfo(0);
                bool paired = CastawayCharacter.LocomotionLaneOwnsPoseFor(
                    cur.shortNameHash, inTr, inTr ? anim.GetNextAnimatorStateInfo(0).shortNameHash : 0);
                wNew = CastawayArmPose.NextRunWeight(wNew, true, paired, RunLowerBlendRate, OverlayReleaseRate, Dt);
                if (t <= 0.20f || f % 6 == 0)
                    sb.AppendLine($"[gatetrace] {t,5:F3} | {(inTr ? "YES" : " no"),4} | {ResolveName(cur),-14} | " +
                                  $"{(inTr ? ResolveName(anim.GetNextAnimatorStateInfo(0)) : "-"),-14} | " +
                                  $"{"",5} | {(paired ? "lane" : "OUT "),6} | {"",5} {"",5}  {wNew:F3}");
            }

            Object.DestroyImmediate(go);
            sb.AppendLine("[gatetrace] ===== END =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static string StateName(Animator a) => ResolveName(a.GetCurrentAnimatorStateInfo(0));

        private static string ResolveName(AnimatorStateInfo info)
        {
            foreach (var n in AllStateNames) if (info.IsName(n)) return n;
            return "<unresolved>";
        }

        private class Row
        {
            public string Label;
            public float Len;
            public float ArcR, DR, DOutR, DFwdR, DUpR, TAtR;
            public float ArcL, DL, DOutL, DFwdL, DUpL, TAtL;
            public float TorsoBaseR, TorsoPosedR, TorsoBaseL, TorsoPosedL;
            public float HeadBaseR, HeadPosedR;
            public float ElbowBaseR, ElbowPosedR;

            public string Format() =>
                $"[armfit] {Label,-20} len={Len:F2}s | R arc={ArcR:F1}deg d={DR:F3}@t{TAtR:F2} " +
                $"(out={DOutR:+0.000;-0.000} fwd={DFwdR:+0.000;-0.000} up={DUpR:+0.000;-0.000}) " +
                $"torso {TorsoBaseR:F3}->{TorsoPosedR:F3} head {HeadBaseR:F3}->{HeadPosedR:F3} " +
                $"elbow {ElbowBaseR:F0}->{ElbowPosedR:F0} | L arc={ArcL:F1}deg d={DL:F3}@t{TAtL:F2} " +
                $"(out={DOutL:+0.000;-0.000} fwd={DFwdL:+0.000;-0.000} up={DUpL:+0.000;-0.000}) " +
                $"torso {TorsoBaseL:F3}->{TorsoPosedL:F3}";

            public string FormatRightOnly() =>
                $"[armfit] {Label,-20} R+run arc={ArcR:F1}deg d={DR:F3}@t{TAtR:F2} " +
                $"(out={DOutR:+0.000;-0.000} fwd={DFwdR:+0.000;-0.000} up={DUpR:+0.000;-0.000}) " +
                $"torso {TorsoBaseR:F3}->{TorsoPosedR:F3} head {HeadBaseR:F3}->{HeadPosedR:F3}";
        }

        private static Row Measure(string label, AnimationClip clip, GameObject model,
            Transform hips, Transform head, Transform lArm, Transform lFore, Transform lHand,
            Transform rArm, Transform rFore, Transform rHand,
            Quaternion rOff, Quaternion lOff, Quaternion runOff)
        {
            var row = new Row { Label = label, Len = clip.length };
            row.TorsoBaseR = row.TorsoPosedR = row.TorsoBaseL = row.TorsoPosedL = 999f;
            row.HeadBaseR = row.HeadPosedR = 999f;
            row.ElbowBaseR = row.ElbowPosedR = 999f;

            for (int i = 0; i < Samples; i++)
            {
                float nt = i / (float)(Samples - 1);
                clip.SampleAnimation(model, nt * clip.length);

                // Torso frame from GEOMETRY (imported-rig bone local axes are arbitrary — never assume them;
                // procedural-animation-verbs.md "measure bone axes FIRST").
                Vector3 up = head.position - hips.position;
                Vector3 shoulder = rArm.position - lArm.position;
                float sw = shoulder.magnitude;
                if (sw < 1e-5f || up.magnitude < 1e-5f) continue;
                Vector3 rightAxis = shoulder / sw;
                up.Normalize();
                Vector3 fwdAxis = Vector3.Cross(up, rightAxis).normalized;
                Vector3 chest = (lArm.position + rArm.position) * 0.5f;

                Vector3 rShoulderP = rArm.position, lShoulderP = lArm.position;
                Vector3 rBase = rHand.position, lBase = lHand.position;
                Vector3 headP = head.position, hipsP = hips.position;
                float elbowBase = Vector3.Angle(rArm.position - rFore.position, rHand.position - rFore.position);

                row.TorsoBaseR = Mathf.Min(row.TorsoBaseR, SegDist(rBase, hipsP, headP) / sw);
                row.TorsoBaseL = Mathf.Min(row.TorsoBaseL, SegDist(lBase, hipsP, headP) / sw);
                row.HeadBaseR = Mathf.Min(row.HeadBaseR, Vector3.Distance(rBase, headP) / sw);
                row.ElbowBaseR = Mathf.Min(row.ElbowBaseR, elbowBase);

                // Apply EXACTLY what CastawayArmPose.LateUpdate applies (right-multiply, bone-local frame).
                rArm.localRotation = rArm.localRotation * rOff * runOff;
                lArm.localRotation = lArm.localRotation * lOff;

                Vector3 rPosed = rHand.position, lPosed = lHand.position;
                float elbowPosed = Vector3.Angle(rArm.position - rFore.position, rHand.position - rFore.position);

                row.TorsoPosedR = Mathf.Min(row.TorsoPosedR, SegDist(rPosed, hipsP, headP) / sw);
                row.TorsoPosedL = Mathf.Min(row.TorsoPosedL, SegDist(lPosed, hipsP, headP) / sw);
                row.HeadPosedR = Mathf.Min(row.HeadPosedR, Vector3.Distance(rPosed, headP) / sw);
                row.ElbowPosedR = Mathf.Min(row.ElbowPosedR, elbowPosed);

                float arcR = Vector3.Angle(rBase - rShoulderP, rPosed - rShoulderP);
                float dR = Vector3.Distance(rBase, rPosed) / sw;
                if (dR > row.DR)
                {
                    row.DR = dR; row.TAtR = nt;
                    Vector3 dv = (rPosed - rBase) / sw;
                    row.DOutR = Vector3.Dot(dv, rightAxis);      // +out for the RIGHT hand
                    row.DFwdR = Vector3.Dot(dv, fwdAxis);
                    row.DUpR = Vector3.Dot(dv, up);
                }
                if (arcR > row.ArcR) row.ArcR = arcR;

                float arcL = Vector3.Angle(lBase - lShoulderP, lPosed - lShoulderP);
                float dL = Vector3.Distance(lBase, lPosed) / sw;
                if (dL > row.DL)
                {
                    row.DL = dL; row.TAtL = nt;
                    Vector3 dv = (lPosed - lBase) / sw;
                    row.DOutL = -Vector3.Dot(dv, rightAxis);     // +out for the LEFT hand = away from midline
                    row.DFwdL = Vector3.Dot(dv, fwdAxis);
                    row.DUpL = Vector3.Dot(dv, up);
                }
                if (arcL > row.ArcL) row.ArcL = arcL;
            }
            return row;
        }

        /// <summary>Distance from p to the SEGMENT ab (the torso axis hips->head).</summary>
        private static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-8f) return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>Every clip the shipped CastawayAnimator.controller can play (CharacterAssetGen constants).</summary>
        private static List<(string, AnimationClip)> LiveClips(StringBuilder sb)
        {
            var list = new List<(string, AnimationClip)>
            {
                // The IDLE state's clip IS BreathingIdle (86cackb3j) — this is the dial's reference pose.
                ("idle(breathing)", FindFbxClip(CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip)),
                ("walk",            FindFbxClip(CharacterAssetGen.WalkFbxPath,          CharacterAssetGen.WalkClip)),
                ("run",             FindFbxClip(CharacterAssetGen.RunFbxPath,           CharacterAssetGen.RunClip)),
                ("jump_idle",       FindFbxClip(CharacterAssetGen.JumpIdleFbxPath,      CharacterAssetGen.JumpIdleClip)),
                ("jump_running",    FindFbxClip(CharacterAssetGen.JumpRunningFbxPath,   CharacterAssetGen.JumpRunningClip)),
                ("melee(chop)",     FindFbxClip(CharacterAssetGen.MeleeFbxPath,         CharacterAssetGen.MeleeClip)),
                ("atk_axe",         FindFbxClip(CharacterAssetGen.AttackAxeFbxPath,     CharacterAssetGen.AxeSwingClip)),
                ("atk_pickaxe",     FindFbxClip(CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip)),
                ("atk_dagger",      FindFbxClip(CharacterAssetGen.AttackDaggerFbxPath,  CharacterAssetGen.DaggerStabClip)),
                ("atk_spear",       FindFbxClip(CharacterAssetGen.AttackSpearFbxPath,   CharacterAssetGen.SpearThrustClip)),
                ("atk_sword",       FindFbxClip(CharacterAssetGen.AttackSwordFbxPath,   CharacterAssetGen.SwordSlashClip)),
                ("crouch_idle",     FindFbxClip(CharacterAssetGen.CrouchIdleFbxPath,    CharacterAssetGen.CrouchIdleClip)),
                ("crouch_walk",     FindFbxClip(CharacterAssetGen.SneakWalkFbxPath,     CharacterAssetGen.CrouchWalkClip)),
                ("getting_up",      FindFbxClip(CharacterAssetGen.GettingUpFbxPath,     CharacterAssetGen.GettingUpClip)),
                ("picking_up",      FindFbxClip(CharacterAssetGen.PickingUpFbxPath,     CharacterAssetGen.PickingUpClip)),
                ("stunned",         FindFbxClip(CharacterAssetGen.StunnedFbxPath,       CharacterAssetGen.StunnedClip)),
                ("hit_body",        FindFbxClip(CharacterAssetGen.HitToBodyFbxPath,     CharacterAssetGen.HitToBodyClip)),
                ("hit_head",        FindFbxClip(CharacterAssetGen.HeadHitFbxPath,       CharacterAssetGen.HeadHitClip)),
                ("hit_bigstomach",  FindFbxClip(CharacterAssetGen.BigStomachHitFbxPath, CharacterAssetGen.BigStomachHitClip)),
                ("hit_stomach",     FindFbxClip(CharacterAssetGen.StomachHitFbxPath,    CharacterAssetGen.StomachHitClip)),
                ("hit_rib",         FindFbxClip(CharacterAssetGen.RibHitFbxPath,        CharacterAssetGen.RibHitClip)),
            };
            // The controller plays the SMOOTHED crouch-walk .anim (SneakGaitCurveFix) when it exists — measure
            // the shipped one too rather than assuming which is bound.
            var smoothed = AssetDatabase.LoadAssetAtPath<AnimationClip>(SneakGaitCurveFix.SmoothedClipPath);
            if (smoothed != null) list.Add(("crouch_walk_smoothed", smoothed));
            sb.AppendLine($"[armfit] clip set: {list.Count} entries");
            return list;
        }

        private static AnimationClip FindFbxClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }

        private static Transform Get(Dictionary<string, Transform> bones, string leaf)
        {
            if (bones.TryGetValue("mixamorig:" + leaf, out var t)) return t;
            foreach (var kv in bones) if (kv.Key.EndsWith(leaf)) return kv.Value;
            return null;
        }
    }
}
