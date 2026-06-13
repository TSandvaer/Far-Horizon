using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Prepares the "Mini Chibi Kid" sourced CC0-Attribution rigged low-poly character (Sketchfab,
    /// joaobaltieri) for the player — the chunky-cartoon castaway base (ticket 86ca8ca1m). This
    /// SUPERSEDES the Quaternius Animated-Men character: the realistic Quaternius head could not be
    /// cartoon-ified, so the Sponsor swapped to a purpose-built chunky base whose big-head toy
    /// proportions are INTRINSIC to the mesh — no bone-scale dials needed (the PR #25 bone-scale path
    /// is dropped for this base; the mesh is already chunky).
    ///
    /// Runs from batchmode (no GUI) so the whole project stays reproducible-from-code (the project
    /// invariant; CI re-runs BootstrapProject.Run which calls this):
    ///
    ///   1. Configure the FBX ModelImporter: Generic rig + CreateFromThisModel avatar (the FBX imports
    ///      as NoAvatar — without an avatar the clips cannot bind and the model freezes in its bind
    ///      pose), import its bundled animation clips, normalize the intrinsic ~1.82u import height to
    ///      ~1u, and mark the locomotion clips (Idle/Walk) as looping. The clip names carry the
    ///      "Object_5|" armature prefix (e.g. "Object_5|Idle 01") — match by .Contains, NOT exact
    ///      equality (exact-match loops ZERO clips — the T-pose-mid-walk failure class, see the guard).
    ///   2. The material atlas binds out of the box: ImportStandard imports two URP/Lit materials
    ///      (mini_material / mini_material_secondary) with _BaseMap = mini_material_baseColor (the 256²
    ///      toon atlas) — verified by probe. We KEEP the FBX's own flat toon materials (no recolor:
    ///      identity/recolor is OUT OF SCOPE per the ticket — ship the kid's default look). We DO assert
    ///      the atlas binds (a regression to a missing-texture grey would fail the import).
    ///   3. Build an AnimatorController asset with an Idle&lt;-&gt;Walk blend driven by a "Moving" bool,
    ///      cross-faded so the gait reads (no hard pop). CastawayCharacter.SetBool("Moving", ...) flips it.
    ///
    /// RIG CHOICE — GENERIC, not Humanoid (same rationale as the prior base): the character ships its
    /// OWN Idle/Walk clips on its OWN 29-bone armature (Hips_00..Head_05, Right/LeftFoot_0xx), so NO
    /// cross-FBX retarget is needed — a Generic rig that creates an avatar from this model
    /// (CreateFromThisModel) binds the clips directly. A Humanoid retarget adds an avatar-build step that
    /// risks failing in batchmode on a single-model port for ZERO benefit here. unity-conventions.md
    /// §FBX documents the avatarSetup T-pose trap + the armature clip-prefix finding, both honored below.
    ///
    /// Source: "Mini Chibi Kid" by joaobaltieri (Sketchfab, CC-Attribution). ~1442 faces, 29-bone
    /// Mixamo-style rig, big-head toy proportions, 2 flat toon materials on a 256² atlas. License/
    /// attribution committed alongside the FBX. Imports UPRIGHT (probe-verified: head world-Y 0.785
    /// above feet world-Y 0.019, feet at origin) — no -90° X bake required for this asset.
    /// </summary>
    public static class CharacterAssetGen
    {
        public const string FbxPath = "Assets/Art/Character/MiniChibiKid/MiniChibiKid.fbx";
        public const string ControllerPath = "Assets/Art/Character/CastawayAnimator.controller";

        // Clip name TOKENS inside the FBX we wire. The chibi ships an alt-set ("Idle 01"/"Walk 01"/
        // "Run 01") AND the FBX clip names carry the "Object_5|" armature prefix (e.g.
        // "Object_5|Idle 01") — match by Contains on these tokens, NOT exact equality. (Empirically the
        // shipped clip names are the " 01" alt-set, NOT "Man_Idle"/"Man_Walk" — verified by probe;
        // matching the real names is load-bearing.)
        public const string IdleClip = "Idle 01";
        public const string WalkClip = "Walk 01";

        // The toon atlas the imported materials bind. Asserted present so a missing-texture grey
        // regression (the flat toon look silently lost) fails the import.
        public const string AtlasTextureName = "mini_material_baseColor";

        // Normalize the FBX's intrinsic import height to ~1 world-unit so the avatar-root scale maps
        // directly onto on-screen height (the camera/NavMesh/grounding are calibrated to ~1u). The chibi
        // FBX imports at ~1.82u intrinsic (measured) — un-normalized it ships ~1.8× tall on the 1.8u
        // avatar root → a giant. Re-derived from the LIVE measured bounds so a future swap self-corrects.
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
            // frozen in its bind/T-pose. CreateFromThisModel builds the avatar from the FBX's own bone
            // hierarchy so Idle/Walk animate the skeleton. (The chibi imports as NoAvatar by default.)
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            // Keep the FBX's own flat toon materials + the embedded 256² atlas (ImportStandard binds
            // _BaseMap = mini_material_baseColor — verified by probe). Identity/recolor is OUT OF SCOPE:
            // ship the kid's DEFAULT look (cap/grey tee/navy shorts/tan skin).
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
            // clip names carry the armature prefix ("Object_5|Idle 01"), NOT bare "Idle 01" — match by
            // Contains, not exact equality. An exact-match silently sets the loop flag on ZERO clips
            // (loopTime stays 0 in the .meta) and the Animator plays once + freezes (the T-pose-mid-walk
            // failure class). Pinned by the EditMode test that asserts isLooping on both.
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

            // Verify the toon atlas binds (the flat toon look must ship — a missing-texture grey is a
            // silent identity loss). Checks the imported materials carry the atlas on _BaseMap.
            int atlasBound = 0;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is Material m && m.HasProperty("_BaseMap"))
                {
                    var tex = m.GetTexture("_BaseMap");
                    if (tex != null && tex.name.Contains(AtlasTextureName)) atlasBound++;
                }
            }
            if (atlasBound == 0)
                Debug.LogError("[CharacterAssetGen] no imported material binds the toon atlas '" +
                               AtlasTextureName + "' on _BaseMap — the flat toon look would ship as grey");
            else
                Debug.Log($"[CharacterAssetGen] toon atlas bound on {atlasBound} material(s)");
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
            // Fallback: a clip whose name CONTAINS the token (the armature-prefix case, e.g.
            // "Object_5|Idle 01"). Skip Unity's "__preview__" mirror clips.
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is AnimationClip clip && clip.name.Contains(name) &&
                    !clip.name.StartsWith("__preview__")) return clip;
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
