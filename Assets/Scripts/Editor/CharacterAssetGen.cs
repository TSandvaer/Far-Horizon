using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Prepares the HYPER3D + MIXAMO castaway for the player — the generated, on-style chunky low-poly
    /// young/hopeful castaway (ticket 86ca8rdkp). This ADOPTS the viability-spike asset (spike 86ca8r72j /
    /// PR #45) into the live game, SUPERSEDING the Sketchfab "Mini Chibi Kid" (the chibi's realistic-leaning
    /// face + 6-SMR-atlas weathering grind is dropped). The Sponsor APPROVED the swap 2026-06-15 after the
    /// spike proved this asset animates clean + on-style in the shipped URP exe.
    ///
    /// PIPELINE — TWO Mixamo FBX, GENERIC rig:
    ///   1. Idle.fbx  (WITH skin = mesh + rig + Idle clip) → rig=Generic, avatarSetup=CreateFromThisModel
    ///      (builds an avatar from the Mixamo skeleton), import the clip, loop it, height-normalize the
    ///      intrinsic import to ~1u (the camera/NavMesh/grounding are ~1u-calibrated).
    ///   2. Walking.fbx (WITHOUT skin = Walk clip only) → rig=Generic, avatarSetup=CreateFromThisModel; the
    ///      Generic Walk clip binds by TRANSFORM PATH onto Idle's mesh (same mixamorig bone names) — no
    ///      Humanoid muscle-space retarget.
    ///
    /// RIG = GENERIC (86ca8rdkp — the runtime-explosion fix). The ticket's prescribed Humanoid path EXPLODED
    /// the skinned mesh into a cone / stretched arm at RUNTIME in the production scene (the Humanoid retarget
    /// reconstructs the pose in muscle space, which mismatched this FBX's internal 100× mesh node + the scene's
    /// avatar root). The spike's Humanoid captures only LOOKED clean because the spike capture camera FOLLOWED
    /// the displaced mesh (fixed scene camera would have shown empty). GENERIC binds clips by transform path
    /// onto the FBX's own mixamorig skeleton — NO muscle retarget — and renders clean at the player with sane
    /// bone world positions (rig-trace-verified: body at spawn, RightHand at the hand). The spike ALSO proved
    /// Generic clean (captures 04/05); it is the choice the live chibi made too.
    ///   3. A flat de-lit URP/Lit material from texture_diffuse as _BaseMap (smoothness ~0, no metallic) so
    ///      the baked toon-shaded albedo reads de-lit/toon (the project's URP/Lit toon idiom). Bound onto the
    ///      avatar's SkinnedMeshRenderer(s) editor-time by MovementCameraScene (the FBX imports its own
    ///      ImportStandard material, but we author a single shared de-lit mat for the toon look + the recolor).
    ///   4. IDENTITY RECOLOR (86ca8rdkp AC4) — warm the mustard-yellow generated shirt toward TAN toward the
    ///      concept (concepts/concept_01_front.png) WITHOUT flattening the toon gradient. A per-material tint
    ///      would WIPE the baked gradient to a flat colour (the trap CastawayCharacter always warned of); the
    ///      texture_diffuse atlas is NOT a 16x16 grid (it is an organic UV unwrap), so the chibi's per-cell
    ///      repaint does not apply. Instead a LUMA-PRESERVING HUE REMAP of the saturated YELLOW shirt pixels
    ///      shifts hue yellow->warm-tan + tones saturation while KEEPING each texel's value (so the toon
    ///      light->dark gradient survives). Reproducible-from-code + idempotent (absolute HSV remap keyed on
    ///      the SOURCE yellow band, NOT the prior pixels), so a bootstrap re-run converges. A REASONABLE pass
    ///      the Sponsor judges from the build — NOT a grind (per the ticket).
    ///
    /// unity-conventions.md §FBX documents the avatarSetup T-pose trap (honored by CreateFromThisModel) and
    /// the clip-take-name finding (the Mixamo take is "mixamo.com", NOT "Idle"/"Walk" — match by Contains +
    /// rename, or zero clips loop = the T-pose-mid-walk failure class).
    ///
    /// Source: Hyper3D Rodin Image-to-3D (Gen-2.5 Quad ~8000) + Mixamo auto-rig (Standard 65 skeleton),
    /// generated from an openai-image A-pose concept (see .claude/docs/character-pipeline.md). License is
    /// CC-style generated content; attribution committed alongside the FBX.
    /// </summary>
    public static class CharacterAssetGen
    {
        // PRODUCTION location (NOT _Spike). The two Mixamo FBX + the de-lit material + the controller.
        private const string CharDir = "Assets/Art/Character/Castaway";
        public const string IdleFbxPath = CharDir + "/Idle.fbx";   // WITH skin (mesh+rig+Idle clip)
        public const string WalkFbxPath = CharDir + "/Walking.fbx"; // WITHOUT skin (Walk clip only)
        public const string DiffusePngPath = CharDir + "/texture_diffuse.png";
        public const string NormalPngPath = CharDir + "/texture_normal.png";
        public const string MaterialPath = CharDir + "/CastawayMat.mat";
        public const string ControllerPath = CharDir + "/CastawayAnimator.controller";

        // The model prefab MovementCameraScene instantiates IS the Idle FBX (it carries the skin + rig).
        // Kept as the canonical "FbxPath" name so existing callers (MovementCameraScene, AxeAssetGen,
        // tests) need no rename — it now points at the with-skin Idle FBX.
        public const string FbxPath = IdleFbxPath;

        // Mixamo clip-take finding (EMPIRICAL, spike Hyper3DSpikeDiag 2026-06-15): BOTH FBX export their
        // single clip as the take name "mixamo.com" (NOT "Idle"/"Walk"). An exact/Contains "Idle"/"Walk"
        // match loops ZERO clips → the T-pose-mid-walk failure class (unity-conventions.md §FBX). So we match
        // the SOURCE take by "mixamo.com" and RENAME it on import to a stable per-FBX name, so the two clips
        // are distinct in the controller.
        public const string SourceTake = "mixamo.com";
        public const string IdleClip = "CastawayIdle"; // renamed-on-import (the controller binds this)
        public const string WalkClip = "CastawayWalk"; // renamed-on-import

        // The de-lit material binds texture_diffuse as the toon albedo. Asserted present so a missing-texture
        // grey regression (the flat toon look silently lost) fails the build/test.
        public const string DiffuseTextureName = "texture_diffuse";

        // Height normalization — the FBX imports at ~2.18u intrinsic; normalize the IMPORT directly to the
        // on-screen height (1.8u, the agent height) so the avatar root sits UNSCALED (scale 1) — a SINGLE
        // scale chain (the FBX globalScale only), matching the spike's clean single-instantiate. Wrapping the
        // 100×-internal-node FBX in a SECOND scaled ancestor (the old 1.8× root) made the Humanoid Animator's
        // retarget displace the mesh off-spawn at runtime (86ca8rdkp / rig-trace). useFileUnits/useFileScale
        // stay at import DEFAULTS (the spike's known-clean values). Re-derived from live bounds (self-corrects).
        public const float TargetImportHeightU = 1.0f;

        // ---- IDENTITY RECOLOR (86ca8rdkp AC4) — warm the mustard-yellow generated shirt toward TAN.
        // The shirt is the big saturated-yellow region of texture_diffuse (probe: ~12.9% of the atlas, mean
        // hue ~44°, sat ~0.92). The concept (concept_01_front.png) reads a warm tan/wheat shirt. We REMAP the
        // saturated-yellow band's hue toward warm-tan + tone its saturation, PRESERVING each pixel's VALUE
        // (HSV V) so the baked toon light→dark gradient survives (a flat tint would wipe it — the per-material
        // trap). Reasonable pass, Sponsor-judged from the build; NOT a grind. ----
        // The yellow shirt SOURCE band (HSV). Pixels inside this band are remapped; everything else (skin,
        // hair, shorts, face) is untouched. Derived empirically (the yellow shirt mean hue ~44°, sat ~0.92).
        public const float ShirtHueMinDeg = 38f;   // lower yellow edge (deg)
        public const float ShirtHueMaxDeg = 75f;   // upper yellow edge (deg) — excludes orange skin (~25-32°)
        public const float ShirtSatMin = 0.45f;    // only SATURATED yellow (skin/hair are lower-sat warm browns)
        public const float ShirtValMin = 0.40f;    // only reasonably-lit yellow (dark shadow stays)
        // The TARGET: warm tan/wheat. Hue shifted from yellow (~44°) down to warm-tan (~34°), saturation
        // pulled toward a muted tan. VALUE is preserved per-pixel (gradient kept). These are the post-remap
        // hue + a saturation SCALE applied to the in-band pixels.
        public const float ShirtTargetHueDeg = 34f; // warm tan/wheat (was ~44° mustard yellow)
        public const float ShirtSatScale = 0.62f;    // tone the saturation toward muted tan (keeps warmth)

        public static void PrepareCharacter()
        {
            ConfigureIdleFbx();   // Humanoid CreateFromThisModel + loop+rename Idle + height-normalize
            ConfigureWalkFbx();   // Humanoid CopyFromOther(Idle) + loop+rename Walk (retargets)
            // IDENTITY RECOLOR (86ca8rdkp) — REPRODUCIBLE-FROM-CODE (the project invariant: CI re-runs
            // bootstrap). Repaints the shirt region of texture_diffuse, idempotently. Runs AFTER the FBX
            // import (the material binds the diffuse PNG; repainting it does not need the FBX re-imported).
            RecolorShirtToTan();
            BuildMaterial();      // flat de-lit URP/Lit from the (now recolored) texture_diffuse
            BuildAnimatorController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterAssetGen] Hyper3D castaway prepared: " + IdleFbxPath + " + " + WalkFbxPath +
                      " + " + MaterialPath + " + " + ControllerPath);
        }

        // Idle.fbx carries the skin (mesh+rig) + the Idle clip. Humanoid rig, avatar created from THIS model
        // (CreateFromThisModel) — the Mixamo Standard-65 skeleton maps to the Humanoid avatar so the clips
        // bind. Loop the Idle clip; height-normalize the mesh to ~1u.
        private static void ConfigureIdleFbx()
        {
            var importer = AssetImporter.GetAtPath(IdleFbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] Idle.fbx not found at " + IdleFbxPath); return; }

            // GENERIC rig (86ca8rdkp — the runtime-explosion fix). The Humanoid retarget reconstructed the
            // pose in MUSCLE space and, combined with this Mixamo FBX's internal 100× mesh node, EXPLODED the
            // skinned mesh into a cone/stretched-arm at runtime in the production scene (verify + gameplay
            // captures; the spike's Humanoid mode only looked clean because its capture camera FOLLOWED the
            // displaced mesh). GENERIC binds the clip by TRANSFORM PATH onto the FBX's own mixamorig skeleton
            // — NO muscle-space retarget — which the spike ALSO proved animates clean (captures 04/05) and
            // which the live chibi used. Both FBX CreateFromThisModel (own avatar from own identical skeleton);
            // the Walk clip binds by path onto the shared bone names — no CopyFromOther retarget needed.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            // HEIGHT NORMALIZATION before the clip reimport so one SaveAndReimport applies scale + loop flags.
            float measured = MeasureHeight(IdleFbxPath);
            if (measured > 0.01f)
            {
                float factor = importer.globalScale * (TargetImportHeightU / measured);
                importer.globalScale = factor;
                Debug.Log($"[CharacterAssetGen] Idle height-normalize: measured={measured:F3}u -> globalScale={factor:F5} " +
                          $"(target {TargetImportHeightU}u)");
            }
            else
            {
                Debug.LogWarning("[CharacterAssetGen] could not measure Idle height — skipping normalize");
            }

            importer.clipAnimations = LoopAndRename(importer, IdleClip, out int looped);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] Idle.fbx reimported: rig=Humanoid CreateFromThisModel, " +
                      $"looped+renamed {looped} clip(s) -> {IdleClip}");

            var avatar = LoadAvatar(IdleFbxPath);
            bool ok = avatar != null && avatar.isValid;
            if (!ok)
                Debug.LogError("[CharacterAssetGen] Idle.fbx did not produce a VALID avatar. avatar=" +
                               (avatar != null) + " valid=" + (avatar != null && avatar.isValid) +
                               " — clips will not bind (the T-pose-mid-walk class)");
            else
                Debug.Log("[CharacterAssetGen] Idle.fbx Generic avatar valid");
        }

        // Walking.fbx is the Walk clip WITHOUT skin. Humanoid rig, avatar COPIED FROM Idle's avatar
        // (CopyFromOther) so the Walk clip RETARGETS onto Idle's mesh (the Mixamo Standard-65 skeletons
        // match). Loop the Walk clip. No skin → no material import (avoid stray materials).
        private static void ConfigureWalkFbx()
        {
            var importer = AssetImporter.GetAtPath(WalkFbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] Walking.fbx not found at " + WalkFbxPath); return; }

            // GENERIC: Walking.fbx creates its OWN avatar from its own (identical mixamorig) skeleton; the
            // Generic Walk clip binds by TRANSFORM PATH onto Idle's mesh (same bone names) — no Humanoid
            // muscle-space retarget (the runtime-explosion cause). The spike proved this path clean (capture
            // 05). No CopyFromOther / sourceAvatar needed.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            importer.clipAnimations = LoopAndRename(importer, WalkClip, out int looped);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] Walking.fbx reimported: rig=Humanoid CopyFromOther(Idle), " +
                      $"looped+renamed {looped} clip(s) -> {WalkClip}");
        }

        // Build a flat de-lit URP/Lit material from texture_diffuse: _BaseMap = diffuse, smoothness ~0, no
        // metallic, so the baked toon-shaded albedo reads toon (the project's URP/Lit toon idiom). Normal map
        // at a LOW strength (the baked albedo already carries shading; a strong normal would double up).
        // MovementCameraScene binds this onto the avatar's SkinnedMeshRenderer(s) editor-time.
        private static void BuildMaterial()
        {
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePngPath);
            if (diffuse == null) { Debug.LogError("[CharacterAssetGen] texture_diffuse not found at " + DiffusePngPath); return; }

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) { Debug.LogError("[CharacterAssetGen] URP/Lit shader not found"); return; }

            var mat = new Material(litShader) { name = "CastawayMat" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.03f); // matte/toon — no gloss
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPngPath);
            if (normalTex != null)
            {
                var ni = AssetImporter.GetAtPath(NormalPngPath) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap)
                {
                    ni.textureType = TextureImporterType.NormalMap;
                    ni.SaveAndReimport();
                }
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap", normalTex);
                    if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.4f);
                }
            }

            EnsureShaderAlwaysIncluded(litShader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterAssetGen] de-lit URP/Lit material built from texture_diffuse -> " + MaterialPath);
        }

        // IDENTITY RECOLOR (86ca8rdkp AC4) — warm the saturated-yellow SHIRT region of texture_diffuse toward
        // TAN, LUMA-PRESERVING (HSV value kept per-pixel so the toon gradient survives — NOT a flat material
        // tint). Reproducible-from-code + IDEMPOTENT: the remap is keyed on the SOURCE yellow band (a hue/sat/
        // value window), and a re-run sees the ALREADY-TANNED pixels OUTSIDE that band (tan hue ~34° is below
        // ShirtHueMin 38°), so a bootstrap re-run does NOT re-shift them — it converges. (Defensive: even if a
        // re-run caught an edge pixel still in-band, the absolute target hue makes it converge, not drift.)
        public static void RecolorShirtToTan()
        {
            string path = DiffusePngPath;
            if (!File.Exists(path))
            {
                Debug.LogError("[CharacterAssetGen] diffuse PNG not found at " + path + " — cannot recolor");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Debug.LogError("[CharacterAssetGen] diffuse PNG failed to decode — recolor aborted");
                Object.DestroyImmediate(tex);
                return;
            }

            var pixels = tex.GetPixels();
            int remapped = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float hueDeg = h * 360f;
                // Only the SATURATED YELLOW SHIRT band — skin (orange ~25-32°, lower sat), hair/shorts
                // (brown, lower value/sat), and the face are all outside this window and stay untouched.
                bool inShirt = hueDeg >= ShirtHueMinDeg && hueDeg <= ShirtHueMaxDeg &&
                               s >= ShirtSatMin && v >= ShirtValMin;
                if (!inShirt) continue;

                // HUE → warm tan; SAT toned; VALUE PRESERVED (the toon gradient lives in V). Absolute target
                // hue (not a relative shift) so the remap is idempotent + converges on a re-run.
                float newH = ShirtTargetHueDeg / 360f;
                float newS = Mathf.Clamp01(s * ShirtSatScale);
                Color nc = Color.HSVToRGB(newH, newS, v);
                nc.a = c.a;
                pixels[i] = nc;
                remapped++;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            float frac = pixels.Length > 0 ? (float)remapped / pixels.Length : 0f;
            Debug.Log($"[CharacterAssetGen] shirt recolored yellow->tan (luma-preserving HSV remap, {remapped} px " +
                      $"= {frac:P1} of the atlas) — reproducible-from-code, idempotent");
            if (remapped == 0)
                Debug.LogWarning("[CharacterAssetGen] recolor remapped ZERO pixels — the shirt band may have " +
                                 "already been tanned (idempotent re-run) OR the source band is mis-tuned");
        }

        // ----- helpers (mirror the spike's proven Hyper3DSpikeGen idioms) -----

        // Match the Mixamo source take ("mixamo.com"), set it to loop, and RENAME it to a stable per-FBX name
        // so the controller binds distinct Idle/Walk clips. Each FBX carries exactly one take; we loop+rename
        // every non-preview clip whose name/take matches the source take. Guarded by the looped>0 assert.
        private static ModelImporterClipAnimation[] LoopAndRename(ModelImporter importer, string newName, out int looped)
        {
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            var edited = new List<ModelImporterClipAnimation>();
            looped = 0;
            foreach (var c in clips)
            {
                var cc = c;
                if (cc.name.Contains(SourceTake) || cc.takeName.Contains(SourceTake))
                {
                    cc.name = newName;
                    cc.loopTime = true;
                    cc.loop = true;
                    // ROOT-TRANSFORM settings matching the spike's known-clean import EXACTLY (the spike's
                    // shipped meta: keepOriginalOrientation=0, keepOriginalPositionY=1, keepOriginalPositionXZ=0).
                    // In-place loco (NavMeshAgent owns world position; applyRootMotion=false).
                    cc.lockRootRotation = true;
                    cc.keepOriginalOrientation = false;
                    cc.lockRootPositionXZ = true;
                    cc.keepOriginalPositionXZ = false;
                    cc.lockRootHeightY = false;
                    cc.keepOriginalPositionY = true;
                    cc.heightFromFeet = false;
                    looped++;
                }
                edited.Add(cc);
            }
            if (looped == 0)
                Debug.LogError($"[CharacterAssetGen] no clip matched source take '{SourceTake}' to loop+rename " +
                               $"to '{newName}' — clip will freeze mid-cycle (T-pose risk). Re-run CharacterDiagnoseTrace.");
            return edited.ToArray();
        }

        private static Avatar LoadAvatar(string fbxPath)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is Avatar a) return a;
            return null;
        }

        private static AnimationClip FindClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip clip && clip.name.Contains(token) && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }

        private static float MeasureHeight(string fbxPath)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
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

        private static void BuildAnimatorController()
        {
            AnimationClip idle = FindClip(IdleFbxPath, IdleClip);
            AnimationClip walk = FindClip(WalkFbxPath, WalkClip);
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

            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;

            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            Debug.Log("[CharacterAssetGen] AnimatorController built: Idle<->Walk on Moving bool -> " + ControllerPath);
        }

        // Mirror MovementCameraScene.EnsureShaderAlwaysIncluded: GraphicsSettings.asset's
        // m_AlwaysIncludedShaders is editable via SerializedObject so the URP/Lit shader never strips.
        private static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        // ===== DIAGNOSE-VIA-TRACE (throwaway entry; never on the ship path) — dump the imported asset's
        // ground truth so the held-axe bone + the recolor + the proportion guards are tuned from EVIDENCE,
        // not guesses (diagnostic-traces-before-hypothesized-fixes). Dumps: every sub-asset (clips/avatar);
        // the SMR roster + the FULL bone list flagging hand/head bones; intrinsic + normalized height. Run:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CharacterAssetGen.CharacterDiagnoseTrace
        public static void CharacterDiagnoseTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[char-trace] ===== CHARACTER DIAGNOSE TRACE =====");
            foreach (var fbx in new[] { IdleFbxPath, WalkFbxPath })
            {
                sb.AppendLine("[char-trace] ===== " + fbx + " =====");
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    string extra = "";
                    if (o is AnimationClip c) extra = $" len={c.length:F2}s looping={c.isLooping}";
                    if (o is Avatar a) extra = $" valid={a.isValid} human={a.isHuman}";
                    sb.AppendLine($"[char-trace]   {o.GetType().Name}: '{o.name}'{extra}");
                }
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer != null)
                    foreach (var dc in importer.defaultClipAnimations)
                        sb.AppendLine($"[char-trace]     defaultClip '{dc.name}' take='{dc.takeName}'");
            }

            var idle = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (idle != null)
            {
                var inst = Object.Instantiate(idle);
                inst.transform.localScale = Vector3.one;
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                sb.AppendLine($"[char-trace] Idle instantiated: SMR count={smrs.Length}");
                var rends = inst.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    sb.AppendLine($"[char-trace] normalized height={b.size.y:F3}u bounds={b.size}");
                }
                if (smrs.Length > 0 && smrs[0].bones != null)
                {
                    sb.AppendLine($"[char-trace] bone count={smrs[0].bones.Length}:");
                    foreach (var bone in smrs[0].bones)
                    {
                        if (bone == null) { sb.AppendLine("[char-trace]   <null bone>"); continue; }
                        string n = bone.name.ToLowerInvariant();
                        string tag = n.Contains("righthand") || (n.Contains("right") && n.Contains("hand")) ? "  <-- RIGHT HAND"
                                   : n.Contains("hand") ? "  <-- hand"
                                   : n.Contains("head") ? "  <-- head" : "";
                        sb.AppendLine($"[char-trace]   bone='{bone.name}' lossyScale={bone.lossyScale}{tag}");
                    }
                }
                Object.DestroyImmediate(inst);
            }
            sb.AppendLine("[char-trace] ===== END TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ===== (attempt-8 throwaway import-flag fix-probes REMOVED post-diagnosis: useFileScale=false made it
        // WORSE — 218u tall + the 100× node SURVIVED; bakeAxisConversion=true ALSO kept the 100× node. Both
        // REFUTED the "an importer flag collapses the cm→m node" hypothesis — the 100× is intrinsic to the FBX
        // hierarchy. The remaining read-only ScaleChainDiagnose stays as the durable instrument that found it.) =====

        // ===== SCALE-CHAIN DIAGNOSE (86ca8rdkp attempt-8 — ROOT-CAUSE the 68u intrinsic offset). Read-only
        // diagnostic, KEPT as the durable instrument that found the root cause (re-runnable on any future
        // character swap to catch a cm→m node). Reproduces the SCENE setup (avatar root scaled PlayerVisualHeight under a
        // player root, the FBX instantiated as a child, BakeMesh the live mesh) and dumps EVERY node's
        // localScale + lossyScale + the SMR.transform scale + the BakeMesh actual extents in BOTH local AND
        // world space — so we MEASURE where the ~68 lives (the cm→m 100× node, or a bone, or the bake) instead
        // of guessing. Run:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CharacterAssetGen.ScaleChainDiagnose
        public static void ScaleChainDiagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[scale-trace] ===== SCALE-CHAIN DIAGNOSE (68u root-cause) =====");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (fbx == null)
            {
                sb.AppendLine("[scale-trace] FBX NOT FOUND at " + IdleFbxPath);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            // ---- (A) the importer's effective globalScale (the height-normalize result) ----
            var importer = AssetImporter.GetAtPath(IdleFbxPath) as ModelImporter;
            if (importer != null)
                sb.AppendLine($"[scale-trace] importer.globalScale={importer.globalScale:F6} " +
                              $"useFileScale={importer.useFileScale} useFileUnits={importer.useFileUnits} " +
                              $"animationType={importer.animationType}");

            // ---- (B) reproduce the SCENE: playerRoot -> avatarRoot(scale 1.8) -> FBX(scale 1) ----
            const float PlayerVisualHeight = 1.8f; // mirror MovementCameraScene
            var playerRoot = new GameObject("__diagPlayer");
            playerRoot.transform.position = Vector3.zero;
            var avatarRoot = new GameObject("__diagAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * PlayerVisualHeight;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one; // matches BuildModel

            sb.AppendLine($"[scale-trace] avatarRoot lossyScale={avatarRoot.transform.lossyScale}");
            sb.AppendLine($"[scale-trace] modelChild '{model.name}' localScale={model.transform.localScale} " +
                          $"lossyScale={model.transform.lossyScale}");

            // ---- (C) walk EVERY transform under the model, dump local+lossy scale (find the 100× node) ----
            sb.AppendLine("[scale-trace] --- full transform scale chain ---");
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                Vector3 ls = t.localScale, lossy = t.lossyScale;
                // FLAG any node whose local OR lossy scale is far from ~1 (the 100× / cm→m suspect).
                bool suspect = Mathf.Abs(ls.x) > 5f || Mathf.Abs(ls.x) < 0.2f ||
                               Mathf.Abs(lossy.x) > 5f || Mathf.Abs(lossy.x) < 0.02f;
                string flag = suspect ? "  <== SCALE SUSPECT" : "";
                sb.AppendLine($"[scale-trace]   '{t.name}' local={ls.ToString("F4")} lossy={lossy.ToString("F5")}{flag}");
            }

            // ---- (D) the SMR(s): transform scale + BakeMesh extents in LOCAL and WORLD ----
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.AppendLine($"[scale-trace] --- {smrs.Length} SMR(s) ---");
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                sb.AppendLine($"[scale-trace] SMR '{smr.name}' transform.localScale={smr.transform.localScale} " +
                              $"lossyScale={smr.transform.lossyScale}");
                sb.AppendLine($"[scale-trace]   sharedMesh.bounds.size={smr.sharedMesh.bounds.size} (import-baked)");
                sb.AppendLine($"[scale-trace]   SMR.bounds(world AABB).size={smr.bounds.size} min.y={smr.bounds.min.y:F4}");

                var baked = new Mesh();
                // useScale:FALSE — node scale applied via the matrix below (apply ONCE; matches runtime path).
                smr.BakeMesh(baked, false);
                var verts = baked.vertices;
                if (verts.Length > 0)
                {
                    float lMinY = float.PositiveInfinity, lMaxY = float.NegativeInfinity;
                    float wMinY = float.PositiveInfinity, wMaxY = float.NegativeInfinity;
                    Matrix4x4 l2w = smr.transform.localToWorldMatrix;
                    foreach (var v in verts)
                    {
                        if (v.y < lMinY) lMinY = v.y; if (v.y > lMaxY) lMaxY = v.y;
                        float wy = l2w.MultiplyPoint3x4(v).y;
                        if (wy < wMinY) wMinY = wy; if (wy > wMaxY) wMaxY = wy;
                    }
                    sb.AppendLine($"[scale-trace]   BakeMesh(useScale:false) LOCAL y=[{lMinY:F4}..{lMaxY:F4}] " +
                                  $"height={lMaxY - lMinY:F4}");
                    sb.AppendLine($"[scale-trace]   BakeMesh->WORLD via l2w  y=[{wMinY:F4}..{wMaxY:F4}] " +
                                  $"height={wMaxY - wMinY:F4}  <== THE SNAP READS THIS world-Y");
                }
                Object.DestroyImmediate(baked);
            }

            // ---- (E) overall rendered bounds (what MeasureHeight reads) ----
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                sb.AppendLine($"[scale-trace] OVERALL Renderer.bounds (scene, scaled 1.8): size={b.size} " +
                              $"center.y={b.center.y:F4} min.y={b.min.y:F4} max.y={b.max.y:F4}");
            }

            Object.DestroyImmediate(playerRoot);
            sb.AppendLine("[scale-trace] ===== END SCALE-CHAIN DIAGNOSE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
