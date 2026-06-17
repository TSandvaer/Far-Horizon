using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC3 RUN-clip grounding regression guard (ticket 86ca9yq34 — run on Shift-hold). The Walk-float saga
    /// (CastawayWalkClipGroundedTests) proved the Mixamo WALK clip authors the hips ~0.66u HIGHER than IDLE, so
    /// the rendered mesh floats while walking unless CastawayCharacter.modelSoleGround cancels the per-clip lift.
    /// A RUN clip can author its OWN hip baseline too — the ClipBaselineDiagnose MEASURED it: the RUN clip plants
    /// the scale-immune sole at +0.62..+0.67 (essentially the same lift as Walk). This guards that:
    ///   (1) the RUN clip's raw sole-lift is REAL + large (so the fix is necessary — and a future re-import that
    ///       flattened it would red, catching a silent over-correction), and
    ///   (2) the PRODUCTION model-grounding math (CastawayCharacter.ComputeModelGroundLocalY — the EXACT formula
    ///       ApplyGroundSnap runs each frame) CANCELS the RUN lift to ~0 across the WHOLE run cycle — so the feet
    ///       plant on the visible sand while RUNNING, with modelSoleGround UNTOUCHED (it reads the live baked sole
    ///       every frame, so it grounds ANY clip — Idle/Walk/Run — by construction, no per-clip constant).
    ///
    /// WHY EDITMODE + SampleAnimation (NOT PlayMode): in headless PlayMode Time.deltaTime≈0, so the Animator never
    /// advances the clip — the mesh stays at its bind pose and the run-lift never manifests (a PlayMode "run"
    /// test green-passes trivially, the documented headless-time false-green class). AnimationClip.SampleAnimation
    /// poses the mesh deterministically REGARDLESS of Time.deltaTime, so this is the only headless-valid way to
    /// guard the run-clip lift. All measured SCALE-IMMUNELY (unit-scale TRS — the FBX 100× cm→m node never
    /// double-applies; the deeper false-green the Walk saga uncovered).
    ///
    /// (The judgment-grade proof is the gameplay-cam SHIPPED run-cycle captures in the PR; this keeps the bug
    /// class from recurring silently in CI before a soak — the testing bar's regression-guard line.)
    /// </summary>
    public class CastawayRunClipGroundedTests
    {
        private const float PlayerVisualHeight = 1.8f; // mirror MovementCameraScene's avatar-root scale

        // Instantiate the production rig (player root → avatar root scaled 1.8 → FBX child); return the SMR +
        // model so SampleAnimation poses the real skinned mesh exactly as the scene ships it. (Mirrors
        // CastawayWalkClipGroundedTests.BuildRig — kept local so the two guards stay independent.)
        private static (GameObject playerRoot, GameObject model, SkinnedMeshRenderer smr, Transform avatarRoot)
            BuildRig()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            Assert.IsNotNull(fbx, "the Idle FBX (with skin) must load at " + CharacterAssetGen.IdleFbxPath);

            var playerRoot = new GameObject("__runGuardPlayer");
            playerRoot.transform.position = Vector3.zero;
            var avatarRoot = new GameObject("__runGuardAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * PlayerVisualHeight;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one; // matches CastawayCharacter.BuildModel
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the FBX must carry a SkinnedMeshRenderer");
            return (playerRoot, model, smr, avatarRoot.transform);
        }

        // SCALE-IMMUNE baked sole world-Y (the exact path CastawayCharacter.MeasureRenderedSoleWorldY uses: a
        // UNIT-SCALE TRS, never smr.localToWorldMatrix — so the FBX 100× cm→m node never double-applies).
        private static float ScaleImmuneSoleWorldY(SkinnedMeshRenderer smr)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, false); // useScale:false — verts in the mesh's authored metres space
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

        // (1) THE RUN-CLIP-LIFT guard: with the player root grounded at 0, the RUN clip lifts the scale-immune
        // sole well off the root across the cycle (ClipBaselineDiagnose measured +0.62..+0.67). Proves the lift
        // is REAL in the run clip (so the model-sole grounding is load-bearing for run too) — and reds if a
        // future re-import / clip swap accidentally flattens it (then the model-grounding would over-correct).
        [Test]
        public void RunClip_LiftsTheSoleOffTheRoot_TheRealBug()
        {
            var (playerRoot, model, smr, _) = BuildRig();
            try
            {
                var run = FindClip(CharacterAssetGen.RunFbxPath, CharacterAssetGen.RunClip);
                Assert.IsNotNull(run, "the Run clip must load (CastawayRun in Running.fbx)");

                float runMin = float.PositiveInfinity;
                for (int i = 0; i <= 12; i++)
                {
                    run.SampleAnimation(model, run.length * i / 12);
                    runMin = Mathf.Min(runMin, ScaleImmuneSoleWorldY(smr));
                }
                Assert.Greater(runMin, 0.3f,
                    $"the RUN clip's MINIMUM scale-immune sole over the cycle must lift well off the grounded root " +
                    $"(> 0.3; ClipBaselineDiagnose measured ~0.62); got {runMin:F3}. A small lift here means the " +
                    "asset's run hip-baseline is gone (then modelSoleGround would over-correct) OR the clip isn't " +
                    "posing — re-run CharacterAssetGen.ClipBaselineDiagnose.");
            }
            finally { Object.DestroyImmediate(playerRoot); }
        }

        // (2) THE FIX-MATH guard: the production model-grounding formula (ComputeModelGroundLocalY — the exact
        // math ApplyGroundSnap runs every frame) must CANCEL the RUN lift across the WHOLE cycle. We pose the RUN
        // clip, measure the raw lifted sole, apply the computed model local-Y, re-measure, and assert the sole
        // lands on the plant target (~0). This is the end-to-end proof modelSoleGround grounds the RUNNING feet
        // — the SAME mechanism the walk fix uses, UNTOUCHED, now proven against the run clip (AC3).
        [Test]
        public void ModelGroundingMath_CancelsTheRunLift_AcrossTheCycle()
        {
            var (playerRoot, model, smr, avatarRoot) = BuildRig();
            try
            {
                var run = FindClip(CharacterAssetGen.RunFbxPath, CharacterAssetGen.RunClip);
                Assert.IsNotNull(run, "the Run clip must load");

                const float plantWorldY = 0f;             // root grounded at world 0
                float rootYScale = avatarRoot.lossyScale.y; // = PlayerVisualHeight
                float maxResidual = 0f;

                for (int i = 0; i <= 12; i++)
                {
                    run.SampleAnimation(model, run.length * i / 12);

                    float rawSole = ScaleImmuneSoleWorldY(smr);
                    Assert.Greater(rawSole, 0.1f, $"frame {i}: the raw run sole must be lifted (got {rawSole:F3})");

                    // Apply the PRODUCTION model-grounding math (current model localY starts at 0).
                    float modelLocalY = CastawayCharacter.ComputeModelGroundLocalY(
                        rawSole, plantWorldY, model.transform.localPosition.y, rootYScale);
                    var mlp = model.transform.localPosition; mlp.y = modelLocalY;
                    model.transform.localPosition = mlp;

                    // Re-measure: the grounded sole must now sit on the plant target.
                    float groundedSole = ScaleImmuneSoleWorldY(smr);
                    maxResidual = Mathf.Max(maxResidual, Mathf.Abs(groundedSole - plantWorldY));

                    // Reset the model offset for the next clean per-frame measurement.
                    mlp.y = 0f; model.transform.localPosition = mlp;
                }

                Assert.Less(maxResidual, 0.02f,
                    $"the production model-grounding math (ComputeModelGroundLocalY — modelSoleGround, UNTOUCHED) " +
                    $"must cancel the RUN clip's hip-lift so the scale-immune sole lands on the plant target across " +
                    $"the WHOLE run cycle (|residual| < 0.02); peaked at {maxResidual:F4}. A regression in the " +
                    "formula (or modelSoleGround being disabled) floats the running feet (AC3).");
            }
            finally { Object.DestroyImmediate(playerRoot); }
        }
    }
}
