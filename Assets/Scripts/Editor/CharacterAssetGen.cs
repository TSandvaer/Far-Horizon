using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Prepares the Quaternius CC0 rigged low-poly CLOTHED character (Animated Men pack,
    /// Smooth_Male_Casual.fbx) for the player — U6 port (ticket 86ca86fz9) of the spike's
    /// CharacterAssetGen (EmbergraveUnitySlice iter7/8, the Sponsor-approved "appealing" character).
    /// Runs from batchmode (no GUI) so the whole project stays reproducible-from-code (the project
    /// invariant; CI re-runs BootstrapProject.Run which calls this):
    ///
    ///   1. Configure the FBX ModelImporter: Generic rig + CreateFromThisModel avatar, import its
    ///      bundled animation clips, normalize the import height to ~1u, and mark the locomotion clips
    ///      (Man_Idle, Man_Walk) as looping. Without the loop flag the Animator plays once + freezes
    ///      (T-pose-mid-walk). The clip names carry the "HumanArmature|" armature prefix, so we match
    ///      by .Contains, NOT exact equality (exact-match loops ZERO clips — see the guard below).
    ///   2. Build an AnimatorController asset with an Idle&lt;-&gt;Walk blend driven by a "Moving" bool,
    ///      cross-faded so the gait reads (no hard pop). CastawayCharacter.SetBool("Moving", ...) flips it.
    ///
    /// RIG CHOICE — GENERIC, not Humanoid (reconciliation note): the character ships its OWN
    /// Man_Idle/Man_Walk clips on its OWN armature, so NO cross-FBX retarget is needed — a Generic rig
    /// that creates an avatar from this model (CreateFromThisModel) binds the clips directly. This is
    /// the empirically-proven, Sponsor-approved spike configuration; a Humanoid retarget adds an
    /// avatar-build step that risks failing in batchmode on a single-model port for ZERO benefit here.
    /// unity-conventions.md §FBX documents the avatarSetup T-pose trap + the HumanArmature| clip-prefix
    /// finding, both honored below. (Humanoid retarget becomes relevant only when sharing one clip set
    /// across DIFFERENT character meshes — out of scope for the single-castaway port.)
    ///
    /// Source: Quaternius "Animated Men Pack" (CC0 1.0) — https://quaternius.com/packs/animatedmen.html
    /// (Google Drive folder id 17LibivOaUidsQhSkcxP3YYvDr0n7wIwu, FBX/Smooth_Male_Casual.fbx, file id
    /// 1m46QkeqCFktuL5Vl3FR6_PtTKSN3WszF). License committed alongside the FBX
    /// (Quaternius_AnimatedMen_License_CC0.txt). Animation set is the pack's own bundled clips (CC0).
    ///
    /// These are ASSETS the scene references; building them editor-time (not at runtime) is the same
    /// scene-embedded convention as the saved-asset NavMesh: the shipped scene must reference a
    /// serialized controller + skinned mesh, not assemble one in Awake (the editor-vs-runtime lesson).
    /// </summary>
    public static class CharacterAssetGen
    {
        public const string FbxPath = "Assets/Art/Character/Quaternius_AnimatedMan_SmoothCasual.fbx";
        public const string ControllerPath = "Assets/Art/Character/CastawayAnimator.controller";

        // Clip names inside the FBX we wire. Idle + Walk are the locomotion pair the controller blends
        // on the "Moving" bool. The FBX clip names carry the "HumanArmature|" prefix — match by Contains.
        public const string IdleClip = "Man_Idle";
        public const string WalkClip = "Man_Walk";

        // Normalize the FBX's intrinsic import height to ~1 world-unit so the avatar-root scale maps
        // directly onto on-screen height (the camera/NavMesh/grounding are calibrated to ~1u). The
        // Animated-Men FBX imports at ~4.96u intrinsic (measured) — un-normalized it scales to a giant.
        // Re-derived from the LIVE measured bounds so a future character swap self-corrects.
        public const float TargetImportHeightU = 1.0f;

        public static void PrepareCharacter()
        {
            ConfigureFbxImporter();
            BuildAnimatorController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterAssetGen] character assets prepared: " + FbxPath + " + " + ControllerPath);
        }

        private static void ConfigureFbxImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[CharacterAssetGen] FBX not found at " + FbxPath);
                return;
            }

            // Generic rig (the character's own skeleton). A Generic rig must CREATE AN AVATAR from this
            // model, or the imported Animator has no avatar + the clips cannot bind -> the model renders
            // frozen in its bind/T-pose (the spike's first-build T-pose bug). CreateFromThisModel builds
            // the avatar from the FBX's own bone hierarchy so Man_Idle/Man_Walk animate the skeleton.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            // HEIGHT NORMALIZATION: measure the imported model's intrinsic height + set globalScale so
            // it imports at ~TargetImportHeightU (1u). Done BEFORE the clip-loop reimport so a single
            // SaveAndReimport applies scale + loop flags together. Re-derived from live bounds.
            float measured = MeasureImportedModelHeight();
            if (measured > 0.01f)
            {
                float factor = importer.globalScale * (TargetImportHeightU / measured);
                importer.globalScale = factor;
                Debug.Log($"[CharacterAssetGen] height-normalize: measured={measured:F3}u -> globalScale={factor:F5} " +
                          $"(target {TargetImportHeightU}u)");
            }
            else
            {
                Debug.LogWarning("[CharacterAssetGen] could not measure model height — skipping normalize");
            }

            // Mark the locomotion clips as looping so the Animator doesn't freeze mid-cycle. The FBX
            // clip names carry the armature prefix ("HumanArmature|Man_Idle"), NOT bare "Man_Idle" —
            // match by Contains, not exact equality. An exact-match silently sets the loop flag on ZERO
            // clips (loopTime stays 0 in the .meta) and the Animator plays once + freezes (the
            // T-pose-mid-walk failure class). Pinned by the EditMode test that asserts isLooping on both.
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            var edited = new List<ModelImporterClipAnimation>();
            int looped = 0;
            foreach (var c in clips)
            {
                var cc = c;
                if (cc.name.Contains(IdleClip) || cc.name.Contains(WalkClip))
                {
                    cc.loopTime = true;
                    cc.loop = true;
                    looped++;
                }
                edited.Add(cc);
            }
            importer.clipAnimations = edited.ToArray();

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] FBX reimported: rig=Generic, {edited.Count} clips, " +
                      $"{looped} set to loop (matched {IdleClip}/{WalkClip} by Contains)");
            if (looped < 2)
                Debug.LogError($"[CharacterAssetGen] expected to loop 2 clips ({IdleClip}+{WalkClip}) " +
                               $"but matched {looped} — locomotion clips may freeze mid-cycle (T-pose risk)");
        }

        // Instantiate the currently-imported FBX, measure its renderer-bounds height (world Y extent),
        // and clean up. Used to derive the import-scale normalization factor. Returns 0 on failure.
        private static float MeasureImportedModelHeight()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) return 0f;
            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            float h = 0f;
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                h = b.size.y;
            }
            Object.DestroyImmediate(inst);
            return h;
        }

        private static AnimationClip FindClip(string name)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is AnimationClip clip && clip.name == name) return clip;
            }
            // Fallback: a clip whose name contains the token (the HumanArmature| prefix case).
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is AnimationClip clip && clip.name.Contains(name)) return clip;
            }
            return null;
        }

        private static void BuildAnimatorController()
        {
            AnimationClip idle = FindClip(IdleClip);
            AnimationClip walk = FindClip(WalkClip);
            if (idle == null || walk == null)
            {
                Debug.LogError($"[CharacterAssetGen] missing clips (idle={idle != null}, walk={walk != null}); " +
                               "controller not built");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            var walkState = sm.AddState("Walk");
            walkState.motion = walk;
            sm.defaultState = idleState;

            // Idle -> Walk when Moving, Walk -> Idle when !Moving; short blends for a smooth gait.
            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;

            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            Debug.Log("[CharacterAssetGen] AnimatorController built: Idle<->Walk on Moving bool");
        }
    }
}
