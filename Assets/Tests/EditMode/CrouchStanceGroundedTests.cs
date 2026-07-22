using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// CROUCH-ABSENCE regression guards (ticket 86caa3kur — the bug class the v5 soak exposed + the existing
    /// WasdCrouchPlayModeTests MISSED). The Sponsor's v5 soak: "there is no crouch in this branch?" — Ctrl
    /// produced no crouch stance. The existing PlayMode test asserts the INPUT→bool→speed contract on a BARE
    /// synthetic rig (no modelPrefab — the modelPrefab-not-wired error is EXPECTED), so it green-passes during
    /// an ENTIRE crouch-broken era: it never loads the real crouch CLIP, never measures the rendered crouch
    /// DEPTH, and never checks the controller routing on the REGENERATED controller the shipped build runs.
    /// That is the "pickup_count > 0 passed during the dual-spawn era" silent-killer class.
    ///
    /// WHY EDITMODE + SampleAnimation (NOT PlayMode): in headless PlayMode Time.deltaTime≈0 → the ANIMATOR never
    /// ticks the clip, so the crouch pose never manifests (a PlayMode crouch-depth test green-passes trivially
    /// AND its deliberate-break can't see the depth — false-green by construction; unity-conventions §Headless
    /// time trap; the walk-float saga's lesson #2). AnimationClip.SampleAnimation poses the real imported clip
    /// deterministically regardless of Time.deltaTime — the only headless-valid way to guard the rendered depth.
    /// Diagnosed via CrouchPoseDiagnose (the loopBlend A/B that exonerated loopBlend) — this PERSISTS that
    /// instrument's measurement as a permanent guard.
    ///
    /// THE THREE BUG CLASSES THIS CATCHES (each independently re-opened the crouch over #197's life):
    ///   (1) THE POSE IS LOWERED — the crouch-idle clip must pose the body meaningfully LOWER than the standing
    ///       idle (a flatten — e.g. a future loopBlend/loopPose/keepOriginal import-flag change that neutralized
    ///       the crouch depth — reds here). Measured scale-immunely (the FBX 100× cm→m node never double-applies).
    ///   (2) THE LOWERING SURVIVES modelSoleGround — the production rendered-sole grounding (ComputeModelGroundLocalY,
    ///       which runs UNCONDITIONALLY every frame) must NOT lift the crouched body back to standing height. The
    ///       Mixamo crouch lowers the WHOLE skeleton (sole drops too), so a naive sole-ground could erase it; this
    ///       asserts the crouch hips stay clearly below the standing hips AFTER grounding. (CrouchPoseDiagnose
    ///       confirmed: grounded standing hips ≈0.58, grounded crouch-idle hips ≈0.31 — a real, surviving crouch.)
    ///   (3) THE CONTROLLER ROUTES CROUCH — both the committed AND the freshly-REGENERATED controller (the shipped
    ///       build re-bakes it via BootstrapProject.Run) must carry CrouchIdle + CrouchWalk states motion'd to the
    ///       crouch clips, with AnyState→Crouch* transitions gated on the Crouch bool (Crouch&&!Moving→CrouchIdle,
    ///       Crouch&&Moving→CrouchWalk). A regen that dropped the crouch lane ships an inert Crouch bool.
    ///
    /// (The judgment-grade proof is the gameplay-cam -verifySneak SHIPPED capture in the PR; this keeps the bug
    /// classes from recurring silently in CI before a soak — the testing-bar regression-guard line.)
    /// </summary>
    public class CrouchStanceGroundedTests
    {
        private const float PlayerVisualHeight = 1.8f; // the production avatar-root scale (mirror MovementCameraScene)

        // Production rig: player root → avatar root scaled 1.8 → Idle.fbx model child. SampleAnimation poses the
        // real skinned mesh exactly as the scene ships it. (Same builder as CastawayWalkClipGroundedTests.)
        private static (GameObject playerRoot, GameObject model, SkinnedMeshRenderer smr, Transform hips) BuildRig()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            Assert.IsNotNull(fbx, "the Idle FBX (with skin) must load at " + CharacterAssetGen.IdleFbxPath);

            var playerRoot = new GameObject("__crouchGuardPlayer");
            playerRoot.transform.position = Vector3.zero;
            var avatarRoot = new GameObject("__crouchGuardAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * PlayerVisualHeight;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the FBX must carry a SkinnedMeshRenderer");

            Transform hips = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.EndsWith("hips") || n == "mixamorig:hips") { hips = t; break; }
            }
            Assert.IsNotNull(hips, "the rig must carry a Hips bone (the crouch-depth body-height proxy)");
            return (playerRoot, model, smr, hips);
        }

        // SCALE-IMMUNE baked sole world-Y (unit-scale TRS — never smr.localToWorldMatrix; the 100× node trap).
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

        private static AnimationClip FindClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip c && c.name.Contains(token) && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }

        // Pose a clip mid-cycle, ground the rendered sole to world-0 via the PRODUCTION math, return the grounded
        // Hips world-Y (the body-height the player SEES after every-frame sole-grounding).
        private static float GroundedHipsWorldY(GameObject model, SkinnedMeshRenderer smr, Transform hips,
                                                AnimationClip clip)
        {
            clip.SampleAnimation(model, clip.length * 0.5f);
            float rawSole = ScaleImmuneSoleWorldY(smr);
            float modelLocalY = CastawayCharacter.ComputeModelGroundLocalY(
                rawSole, 0f, model.transform.localPosition.y, PlayerVisualHeight);
            var mlp = model.transform.localPosition; mlp.y = modelLocalY;
            model.transform.localPosition = mlp;
            clip.SampleAnimation(model, clip.length * 0.5f); // re-pose so the offset applies to the measured frame
            return hips.position.y;
        }

        // (1)+(2) THE DEPTH guard: the crouch-idle clip poses the body LOWER than the standing idle, AND that
        // lowering SURVIVES the production modelSoleGround (the crouch hips stay clearly below standing hips after
        // grounding). Catches a flatten (loopPose/import-flag) AND a sole-ground-cancels-crouch regression.
        [Test]
        public void CrouchIdle_PosesLowerThanStanding_AndSurvivesSoleGrounding()
        {
            var (playerRoot, model, smr, hips) = BuildRig();
            try
            {
                var stand = FindClip(CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip);
                var crouch = FindClip(CharacterAssetGen.CrouchIdleFbxPath, CharacterAssetGen.CrouchIdleClip);
                Assert.IsNotNull(stand, "the standing idle clip (CastawayBreathingIdle) must load");
                Assert.IsNotNull(crouch, "the crouch-idle clip (CastawayCrouchIdle) must load");

                // RAW (pre-grounding) hips: the crouch clip authors the body lower than standing.
                stand.SampleAnimation(model, stand.length * 0.5f);
                float standRawHips = hips.position.y;
                crouch.SampleAnimation(model, crouch.length * 0.5f);
                float crouchRawHips = hips.position.y;
                Assert.Less(crouchRawHips, standRawHips - 0.30f,
                    $"the crouch-idle clip must author the Hips meaningfully BELOW the standing idle (a crouch is a " +
                    $"LOWERED stance): standRawHips={standRawHips:F3} crouchRawHips={crouchRawHips:F3}. A small gap " +
                    "means a clip flatten (a loopPose/keepOriginalPositionY/import-flag change neutralized the depth) " +
                    "— the crouch reads as standing (the v5 soak symptom class).");

                // GROUNDED hips: the production sole-grounding must NOT lift the crouch back to standing height.
                float standGroundedHips = GroundedHipsWorldY(model, smr, hips, stand);
                float crouchGroundedHips = GroundedHipsWorldY(model, smr, hips, crouch);
                Assert.Less(crouchGroundedHips, standGroundedHips - 0.15f,
                    $"AFTER the production modelSoleGround (ComputeModelGroundLocalY, run every frame), the crouch " +
                    $"hips must stay clearly BELOW the standing hips: standGroundedHips={standGroundedHips:F3} " +
                    $"crouchGroundedHips={crouchGroundedHips:F3}. If they converge, sole-grounding is LIFTING the " +
                    "lowered crouch body back to standing height (a grounding-cancels-crouch regression) — the crouch " +
                    "is erased even though the clip poses it. (CrouchPoseDiagnose measured ~0.58 vs ~0.31 grounded.)");
            }
            finally { Object.DestroyImmediate(playerRoot); }
        }

        // (1)+(2) the sneak (crouch-walk) clip is ALSO a lowered stance and survives grounding (a softer drop than
        // crouch-idle, but still clearly below standing). Catches the same classes on the crouch-MOVE clip.
        [Test]
        public void CrouchWalk_PosesLowerThanStanding_AndSurvivesSoleGrounding()
        {
            var (playerRoot, model, smr, hips) = BuildRig();
            try
            {
                var stand = FindClip(CharacterAssetGen.BreathingIdleFbxPath, CharacterAssetGen.BreathingIdleClip);
                var sneak = FindClip(CharacterAssetGen.SneakWalkFbxPath, CharacterAssetGen.CrouchWalkClip);
                Assert.IsNotNull(stand, "the standing idle clip must load");
                Assert.IsNotNull(sneak, "the crouch-walk (Sneak Walk) clip must load");

                float standGroundedHips = GroundedHipsWorldY(model, smr, hips, stand);
                float sneakGroundedHips = GroundedHipsWorldY(model, smr, hips, sneak);
                Assert.Less(sneakGroundedHips, standGroundedHips - 0.08f,
                    $"AFTER modelSoleGround, the crouch-WALK hips must stay below the standing hips " +
                    $"(standGroundedHips={standGroundedHips:F3} sneakGroundedHips={sneakGroundedHips:F3}). A crouched " +
                    "sneak that grounds to standing height reads as a normal walk (no crouch).");
            }
            finally { Object.DestroyImmediate(playerRoot); }
        }

        // (3) CONTROLLER-ROUTING guard on the COMMITTED controller — the Crouch bool routes to CrouchIdle/CrouchWalk
        // states motion'd to the crouch clips, via AnyState transitions (Crouch&&!Moving→CrouchIdle,
        // Crouch&&Moving→CrouchWalk). A dropped crouch lane ships an inert Crouch bool (crouch never engages).
        [Test]
        public void Controller_RoutesCrouchBool_ToCrouchIdleAndCrouchWalk()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            // The Crouch BOOL param.
            bool hasCrouchBool = false;
            foreach (var p in controller.parameters)
                if (p.name == CastawayCharacter.CrouchParam && p.type == AnimatorControllerParameterType.Bool)
                    hasCrouchBool = true;
            Assert.IsTrue(hasCrouchBool, "the controller must have a Crouch BOOL param (the upright<->crouch lane select)");

            var sm = controller.layers[0].stateMachine;
            AnimatorState crouchIdle = null, crouchWalk = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "CrouchIdle") crouchIdle = cs.state;
                if (cs.state.name == "CrouchWalk") crouchWalk = cs.state;
            }
            Assert.IsNotNull(crouchIdle, "the controller must have a 'CrouchIdle' state (the lowered standing stance)");
            Assert.IsNotNull(crouchWalk, "the controller must have a 'CrouchWalk' state (the crouched sneak move)");

            // Each crouch state is motion'd to its crouch clip (a regen that dropped the motion ships an empty state).
            var ciClip = crouchIdle.motion as AnimationClip;
            var cwClip = crouchWalk.motion as AnimationClip;
            Assert.IsNotNull(ciClip, "CrouchIdle's motion must be an AnimationClip (the CastawayCrouchIdle clip)");
            Assert.IsTrue(ciClip.name.Contains(CharacterAssetGen.CrouchIdleClip),
                "CrouchIdle must be motion'd to CastawayCrouchIdle (got '" + ciClip.name + "')");
            Assert.IsNotNull(cwClip, "CrouchWalk's motion must be an AnimationClip (the CastawayCrouchWalk clip)");
            Assert.IsTrue(cwClip.name.Contains(CharacterAssetGen.CrouchWalkClip),
                "CrouchWalk must be motion'd to CastawayCrouchWalk (got '" + cwClip.name + "')");

            AssertAnyStateRoutesCrouch(sm, crouchIdle, crouchWalk, "committed");
        }

        // Shared: assert AnyState→CrouchIdle gated on (Crouch && !Moving) and AnyState→CrouchWalk gated on
        // (Crouch && Moving) both EXIST — the wire from the Crouch bool to the crouch lane.
        private static void AssertAnyStateRoutesCrouch(AnimatorStateMachine sm, AnimatorState crouchIdle,
                                                       AnimatorState crouchWalk, string which)
        {
            bool toCrouchIdleOnCrouchNotMoving = false;
            bool toCrouchWalkOnCrouchMoving = false;
            foreach (var t in sm.anyStateTransitions)
            {
                bool crouchIf = false, movingIf = false, movingIfNot = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.CrouchParam && c.mode == AnimatorConditionMode.If) crouchIf = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) movingIf = true;
                    if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) movingIfNot = true;
                }
                if (t.destinationState == crouchIdle && crouchIf && movingIfNot) toCrouchIdleOnCrouchNotMoving = true;
                if (t.destinationState == crouchWalk && crouchIf && movingIf) toCrouchWalkOnCrouchMoving = true;
            }
            Assert.IsTrue(toCrouchIdleOnCrouchNotMoving,
                $"the {which} controller must have an AnyState→CrouchIdle transition gated on (Crouch && !Moving) — " +
                "the wire from the Crouch bool to the lowered standing stance (a standstill Ctrl-hold).");
            Assert.IsTrue(toCrouchWalkOnCrouchMoving,
                $"the {which} controller must have an AnyState→CrouchWalk transition gated on (Crouch && Moving) — " +
                "the wire from the Crouch bool to the crouched sneak move (Ctrl + WASD).");
        }
    }
}
