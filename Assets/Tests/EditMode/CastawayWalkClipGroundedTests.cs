using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// THE WALK-FLOAT regression guard (ticket 86ca8rdkp attempt-9 — the bug class every PRIOR ground test
    /// MISSED). The Sponsor's soak of e1289ef showed the castaway hovering ~0.5-1u above the sand WHILE
    /// WALKING. Diagnose-via-trace OVERTURNED the e1289ef hypothesis (root-snap + "the clips are in-place"):
    /// the Mixamo WALK clip authors the body ~0.66u HIGHER than the IDLE clip, so the rendered mesh floats
    /// ONLY while walking even though the avatar ROOT is correctly grounded (shipped [FloatTrace]:
    /// proxyRootGap≈0 yet BAKED_SOLE rising to +0.66 mid-stride = REAL float).
    ///
    /// WHY EDITMODE + SampleAnimation (NOT PlayMode): in headless PlayMode Time.deltaTime≈0, so the ANIMATOR
    /// never advances the clip — the mesh stays at its bind/first-frame pose and the walk-lift never manifests
    /// (a PlayMode "walk" test green-passes trivially AND its deliberate-break can't see the lift — a
    /// false-green by construction; unity-conventions.md §Headless time trap). AnimationClip.SampleAnimation
    /// poses the mesh deterministically REGARDLESS of Time.deltaTime, so an EditMode test can pose the real
    /// WALK clip and measure the actual per-frame sole. This is the only headless-valid way to guard the lift.
    ///
    /// WHY THIS IS THE LISTENER-WIRING-GRADE CATCH: every prior ground test used a synthetic/static SMR or no
    /// mesh, so they passed during the ENTIRE walk-float era (the "pickup_count > 0 passed during the
    /// dual-spawn era" silent-killer class). THIS poses the REAL imported WALK clip and asserts BOTH halves:
    ///   (1) the asset's raw WALK sole-lift is LARGE (the bug is real in the clip — the fix is necessary);
    ///   (2) the production model-grounding math (CastawayCharacter.ComputeModelGroundLocalY — the exact
    ///       formula ApplyGroundSnap runs) CANCELS that lift to ~0 (the fix is correct);
    ///   (3) the IDLE clip's raw sole is already ~0 (the case e1289ef got right — not regressed).
    /// All measured SCALE-IMMUNELY (unit-scale TRS — the FBX 100× cm→m node never double-applies).
    ///
    /// (The judgment-grade proof is the gameplay-cam SHIPPED captures in the PR; this keeps the bug class from
    /// recurring silently in CI before a soak — the testing bar's regression-guard line.)
    /// </summary>
    public class CastawayWalkClipGroundedTests
    {
        private const float PlayerVisualHeight = 1.8f; // mirror MovementCameraScene's avatar-root scale

        // Instantiate the production rig (player root → avatar root scaled 1.8 → FBX child) and return the SMR +
        // the model child, so SampleAnimation poses the real skinned mesh exactly as the scene ships it.
        private static (GameObject playerRoot, GameObject model, SkinnedMeshRenderer smr, Transform avatarRoot)
            BuildRig()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            Assert.IsNotNull(fbx, "the Idle FBX (with skin) must load at " + CharacterAssetGen.IdleFbxPath);

            var playerRoot = new GameObject("__walkGuardPlayer");
            playerRoot.transform.position = Vector3.zero;
            var avatarRoot = new GameObject("__walkGuardAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * PlayerVisualHeight; // the production avatar-root scale
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

        // (1) THE ASSET-LIFT guard: with the player root grounded at 0, the IDLE clip plants the scale-immune
        // sole at ~0 (feet on the root), but the WALK clip lifts it well off the root across the stride. This
        // proves the bug is REAL in the clip (so the fix is necessary) — and reds if a future re-import / clip
        // swap accidentally flattens the walk lift (then the model-grounding would over-correct).
        [Test]
        public void WalkClip_LiftsTheSoleOffTheRoot_WhileIdleDoesNot_TheRealBug()
        {
            var (playerRoot, model, smr, _) = BuildRig();
            try
            {
                var idle = FindClip(CharacterAssetGen.IdleFbxPath, CharacterAssetGen.IdleClip);
                var walk = FindClip(CharacterAssetGen.WalkFbxPath, CharacterAssetGen.WalkClip);
                Assert.IsNotNull(idle, "the Idle clip must load");
                Assert.IsNotNull(walk, "the Walk clip must load");

                // IDLE: sample across the cycle; the sole must stay ~on the root (the e1289ef-correct case).
                float idleMaxAbs = 0f;
                for (int i = 0; i <= 8; i++)
                {
                    idle.SampleAnimation(model, idle.length * i / 8);
                    idleMaxAbs = Mathf.Max(idleMaxAbs, Mathf.Abs(ScaleImmuneSoleWorldY(smr)));
                }
                Assert.Less(idleMaxAbs, 0.05f,
                    $"the IDLE clip must plant the sole on the grounded root (|soleY| < 0.05); peaked at " +
                    $"{idleMaxAbs:F3}. (This is the standing case e1289ef got right.)");

                // WALK: sample across the cycle; the raw sole MUST lift well off the root (the bug).
                float walkMin = float.PositiveInfinity;
                for (int i = 0; i <= 12; i++)
                {
                    walk.SampleAnimation(model, walk.length * i / 12);
                    walkMin = Mathf.Min(walkMin, ScaleImmuneSoleWorldY(smr));
                }
                Assert.Greater(walkMin, 0.3f,
                    $"the WALK clip's MINIMUM scale-immune sole over the stride must lift well off the grounded " +
                    $"root (> 0.3; measured the Mixamo clip authors ~0.66u); got {walkMin:F3}. A small lift here " +
                    "means the asset's walk-float bug is gone (then the fix would over-correct) OR the clip " +
                    "isn't posing — re-run CharacterAssetGen.ClipBaselineDiagnose.");
            }
            finally { Object.DestroyImmediate(playerRoot); }
        }

        // (2) THE FIX-MATH guard: the production model-grounding formula (ComputeModelGroundLocalY — the exact
        // math ApplyGroundSnap runs) must CANCEL the WALK lift. We pose the WALK clip, measure the raw lifted
        // sole, apply the computed model local-Y, re-measure, and assert the sole lands on the plant target
        // (~0) across the WHOLE stride. This is the end-to-end proof the fix grounds the WALKING feet.
        [Test]
        public void ModelGroundingMath_CancelsTheWalkLift_AcrossTheStride()
        {
            var (playerRoot, model, smr, avatarRoot) = BuildRig();
            try
            {
                var walk = FindClip(CharacterAssetGen.WalkFbxPath, CharacterAssetGen.WalkClip);
                Assert.IsNotNull(walk, "the Walk clip must load");

                const float plantWorldY = 0f;            // root grounded at world 0
                float rootYScale = avatarRoot.lossyScale.y; // = PlayerVisualHeight
                float maxResidual = 0f;

                for (int i = 0; i <= 12; i++)
                {
                    walk.SampleAnimation(model, walk.length * i / 12);

                    // Raw lifted sole this clip-frame.
                    float rawSole = ScaleImmuneSoleWorldY(smr);
                    Assert.Greater(rawSole, 0.1f, $"frame {i}: the raw walk sole must be lifted (got {rawSole:F3})");

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
                    $"the production model-grounding math (ComputeModelGroundLocalY) must cancel the WALK clip's " +
                    $"~0.66u body-lift so the scale-immune sole lands on the plant target across the WHOLE stride " +
                    $"(|residual| < 0.02); peaked at {maxResidual:F4}. This is the e1289ef walk-float fix — a " +
                    "regression in the formula (or a revert to root-only grounding) reds here.");
            }
            finally { Object.DestroyImmediate(playerRoot); }
        }

        // (3) THE PURE-MATH guard: ComputeModelGroundLocalY over known inputs (no FBX) — the unit core. A sole
        // lifted +0.66 over a root scaled 1.8 must yield a model local-Y of -0.66/1.8 ≈ -0.367 (so the world
        // sole drops 0.66 onto the plant); a grounded sole yields ~0 (no offset). Catches a sign/scale flip.
        [Test]
        public void ComputeModelGroundLocalY_DividesTheResidualByRootScale_WithCorrectSign()
        {
            // Lifted sole +0.66, plant 0, current model localY 0, root scale 1.8 → push model DOWN by 0.66/1.8.
            float ly = CastawayCharacter.ComputeModelGroundLocalY(0.66f, 0f, 0f, 1.8f);
            Assert.That(ly, Is.EqualTo(-0.66f / 1.8f).Within(1e-4f),
                "a +0.66 lifted sole over a 1.8-scaled root must push the model local-Y down by 0.66/1.8");

            // Already grounded (sole == plant) → no offset.
            Assert.That(CastawayCharacter.ComputeModelGroundLocalY(0.10f, 0.10f, 0f, 1.8f),
                Is.EqualTo(0f).Within(1e-5f), "a sole already on the plant target needs zero model offset");

            // A degenerate (≈0) root scale must not divide-by-zero (falls back to scale 1).
            float safe = CastawayCharacter.ComputeModelGroundLocalY(0.5f, 0f, 0f, 0f);
            Assert.IsFalse(float.IsNaN(safe) || float.IsInfinity(safe),
                "a ~0 root scale must not produce NaN/Inf (the fallback-to-1 guard)");
        }

        // (4) WIRING guard: the shipped avatar must ship with modelSoleGround ENABLED (the walk-clip fix). A
        // disabled flag ships the walk-float back even with the math present (the fix is inert).
        [Test]
        public void Avatar_ShipsWithModelSoleGroundEnabled()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Boot.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open");
            CastawayCharacter castaway = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                castaway = root.GetComponentInChildren<CastawayCharacter>(true);
                if (castaway != null) break;
            }
            Assert.IsNotNull(castaway, "the Boot scene must carry a CastawayCharacter");
            Assert.IsTrue(castaway.modelSoleGround,
                "the shipped avatar must have modelSoleGround ENABLED — else the WALK clip's ~0.66u body-lift " +
                "floats the rendered feet while walking (the e1289ef walk-float the Sponsor soaked)");
        }
    }
}
