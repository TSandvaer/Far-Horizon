using System.Text;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Prepares the SOURCED hero axe — "One-handed stylized axe" by Viktor.G (Sketchfab,
    /// CC-Attribution, UID d2e3f8682d71425ba2bf72f3e3d78f7c) — a rustic leather-wrapped hatchet
    /// (wood handle + stone/steel head), single mesh + baseColor atlas (ticket 86ca8ce6y). This
    /// REPLACES the procedural HeroAxeMesh (retired): the procedural wedge did not read as an axe.
    ///
    /// What it does (batchmode-reproducible — BootstrapProject.Run calls PrepareAxe() before the
    /// scene is authored, so MovementCameraScene can attach the imported mesh to the chibi's hand):
    ///   1. Configure the FBX ModelImporter: keep its single mesh + ImportStandard material (the
    ///      baseColor atlas binds), NO rig/animation (a static prop), normalize the intrinsic import
    ///      height so the hatchet sits ~hand-sized when parented to the chibi's hand bone.
    ///   2. DOWNSAMPLE the 3.3MB baseColor atlas on import (maxTextureSize capped) — a prop texture
    ///      does not need 4K; the source ships oversized.
    ///   3. Fix any Sketchfab Y-up / scale quirk at import (probe-verified — see the diagnostic).
    ///
    /// Source: "One-handed stylized axe" by Viktor.G (Sketchfab, CC-Attribution). License/attribution
    /// committed alongside the FBX (CastawayAxe_License_CC-Attribution.txt).
    /// </summary>
    public static class AxeAssetGen
    {
        public const string FbxPath = "Assets/Art/Props/CastawayAxe/CastawayAxe.fbx";

        // Cap the oversized 3.3MB source atlas on import. 1024 keeps the leather-wrap / wood-grain
        // detail legible at hand scale without shipping a 4K prop texture.
        public const int MaxTextureSize = 1024;

        // Normalize the imported hatchet's intrinsic height (longest axis) to this many world units in
        // the FBX's own ~1u-normalized local space, so the avatar-root scale + a hand-local scale put
        // it at a believable hand-held size. Re-derived from live bounds so a future re-source self-
        // corrects. (The final hand-held size is tuned by the attach-local scale in MovementCameraScene.)
        public const float TargetImportHeightU = 1.0f;

        public static void PrepareAxe()
        {
            ConfigureTexture();
            ConfigureFbxImporter();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AxeAssetGen] sourced hatchet prepared: " + FbxPath);
        }

        private static void ConfigureTexture()
        {
            // The atlas may import either as the top-level PNG OR as the FBX .fbm sidecar copy; cap
            // whichever importer resolves. Both paths are committed; the FBX binds one of them.
            foreach (var p in new[]
            {
                "Assets/Art/Props/CastawayAxe/Material_002_baseColor_png.png",
                "Assets/Art/Props/CastawayAxe/CastawayAxe.fbm/Material_002_baseColor_png.png",
            })
            {
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                if (ti.maxTextureSize > MaxTextureSize)
                {
                    ti.maxTextureSize = MaxTextureSize;
                    EditorUtility.SetDirty(ti);
                    ti.SaveAndReimport();
                    Debug.Log($"[AxeAssetGen] downsampled atlas '{p}' -> maxTextureSize={MaxTextureSize}");
                }
            }
        }

        private static void ConfigureFbxImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[AxeAssetGen] FBX not found at " + FbxPath);
                return;
            }

            // A static prop: no rig, no animation. Keep the source material (ImportStandard binds the
            // baseColor atlas) so the leather-wrap / wood / steel read out of the box.
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            // Generate smoothed normals over the coarse facets (the low-poly smooth-shaded idiom,
            // unity-conventions.md §Low-poly mesh patterns) so the hatchet reads stylized, not flat-OBJ.
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
            importer.normalSmoothingAngle = 60f;

            // HEIGHT NORMALIZATION: measure the imported model's intrinsic longest-axis extent + set
            // globalScale so it imports at ~TargetImportHeightU. Done BEFORE the SaveAndReimport so scale
            // applies in one pass. Re-derived from live bounds (a future re-source self-corrects).
            float measured = MeasureImportedModelLongestAxis();
            if (measured > 0.0001f)
            {
                float factor = importer.globalScale * (TargetImportHeightU / measured);
                importer.globalScale = factor;
                Debug.Log($"[AxeAssetGen] height-normalize: longestAxis={measured:F3}u -> globalScale={factor:F5} " +
                          $"(target {TargetImportHeightU}u)");
            }
            else
            {
                Debug.LogWarning("[AxeAssetGen] could not measure axe height — skipping normalize");
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[AxeAssetGen] FBX reimported: static prop, ImportStandard material, normals=Calculate");
        }

        // Instantiate the currently-imported FBX, measure its renderer-bounds longest axis (the
        // hatchet's overall length, whichever axis it lies along), and clean up. Returns 0 on failure.
        private static float MeasureImportedModelLongestAxis()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) return 0f;
            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            float longest = 0f;
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            }
            Object.DestroyImmediate(inst);
            return longest;
        }

        // ===== DIAGNOSE-VIA-TRACE (throwaway entry; never on the ship path) =====
        // Dumps (a) the chibi rig's bone names (to find the exact right-hand bone — the brief says
        // RightHand_010 per PR #26's rig dump, but the rig has duplicate-name traps, e.g. the mesh-group
        // "head" node vs the Head_05 bone — so VERIFY, don't trust) and (b) the imported axe FBX's
        // intrinsic bounds + local axis layout (to derive the correct attach orientation/scale). Run via:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.AxeAssetGen.DiagnoseTrace
        public static void DiagnoseTrace()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[axe-trace] ===== DIAGNOSE TRACE =====");

            // (a) chibi rig bones from the SMR bone array (the actual skeleton — skips mesh-group nodes).
            var chibi = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            if (chibi == null)
            {
                sb.AppendLine("[axe-trace] chibi FBX NOT FOUND at " + CharacterAssetGen.FbxPath +
                              " — run BootstrapProject/CharacterAssetGen first");
            }
            else
            {
                var inst = Object.Instantiate(chibi);
                var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && smr.bones != null)
                {
                    sb.AppendLine($"[axe-trace] chibi SMR bone count = {smr.bones.Length}");
                    foreach (var bone in smr.bones)
                    {
                        if (bone == null) { sb.AppendLine("[axe-trace]   <null bone>"); continue; }
                        bool hand = bone.name.ToLowerInvariant().Contains("hand");
                        sb.AppendLine($"[axe-trace]   bone='{bone.name}'{(hand ? "   <-- HAND" : "")}");
                    }
                }
                else
                {
                    sb.AppendLine("[axe-trace] chibi has NO SkinnedMeshRenderer/bones");
                }
                Object.DestroyImmediate(inst);
            }

            // (b) axe FBX intrinsic bounds + local layout.
            var axe = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (axe == null)
            {
                sb.AppendLine("[axe-trace] axe FBX NOT FOUND at " + FbxPath);
            }
            else
            {
                var inst = Object.Instantiate(axe);
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                var rends = inst.GetComponentsInChildren<Renderer>();
                sb.AppendLine($"[axe-trace] axe renderer count = {rends.Length}");
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    sb.AppendLine($"[axe-trace] axe bounds center={b.center} size={b.size} min={b.min} max={b.max}");
                    sb.AppendLine($"[axe-trace] longest axis = " +
                                  $"{(b.size.x >= b.size.y && b.size.x >= b.size.z ? "X" : b.size.y >= b.size.z ? "Y" : "Z")}");
                }
                // also dump child mesh nodes to understand the FBX hierarchy
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                    sb.AppendLine($"[axe-trace]   node='{t.name}' localPos={t.localPosition} localRot={t.localEulerAngles}");
                Object.DestroyImmediate(inst);
            }

            sb.AppendLine("[axe-trace] ===== END TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ===== SCALE TRACE (throwaway): open Boot.unity, find the attached HeroAxe, dump the hand bone's
        // lossy scale + the axe's world bounds, so the hand-local scale can be derived to land a believable
        // hatchet size. Run via:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.AxeAssetGen.ScaleTrace
        public static void ScaleTrace()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[axe-scale] ===== SCALE TRACE =====");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            GameObject axe = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "HeroAxe") { axe = t.gameObject; break; }
            if (axe == null)
            {
                sb.AppendLine("[axe-scale] HeroAxe NOT FOUND in Boot.unity");
            }
            else
            {
                var t = axe.transform;
                sb.AppendLine($"[axe-scale] HeroAxe localScale={t.localScale} lossyScale={t.lossyScale}");
                sb.AppendLine($"[axe-scale] parent='{t.parent?.name}' parent.lossyScale={t.parent?.lossyScale}");
                var mr = axe.GetComponentInChildren<MeshRenderer>(true);
                if (mr != null)
                    sb.AppendLine($"[axe-scale] world bounds size={mr.bounds.size} center={mr.bounds.center}");
                var castaway = Object.FindAnyObjectByType<FarHorizon.CastawayCharacter>();
                if (castaway != null)
                {
                    var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null) sb.AppendLine($"[axe-scale] castaway SMR world bounds size={smr.bounds.size}");
                }
            }
            sb.AppendLine("[axe-scale] ===== END SCALE TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ===== SOAKFIX4 POSE TRACE (throwaway): the diagnose-via-trace for the two soak axe-pose notes —
        //   (1) the STUMP axe head is BURIED in the block (only the handle pokes out);
        //   (2) the HELD axe sits too HIGH (handle by the kid's EAR).
        // Opens Boot.unity and dumps, for BOTH the StumpAxe and the HeroAxe:
        //   - the axe's WORLD renderer bounds (min/max/center/size) so we can see exactly where the head
        //     and haft tips sit in world Y vs the reference surface;
        //   - the reference surface heights: the CraftStump's world TOP-Y (for the stump axe) and the
        //     castaway head-bone / shoulder world Y + the SMR bounds (for the held axe);
        //   - the axe FBX's INTRINSIC local geometry split along its longest axis: which half is the HEAD
        //     (the wide/heavy steel end) vs the HAFT (the thin wood end), so the pose math knows which way
        //     "blade" points (don't trust the source's axis convention — probe it).
        // Run via:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.AxeAssetGen.PoseTrace
        public static void PoseTrace()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[axe-pose] ===== SOAKFIX4 POSE TRACE =====");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            // (a) intrinsic FBX local geometry: split the mesh verts along the longest local axis into two
            // halves; the HEAD is the half with the larger cross-section (wider in the other two axes).
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx != null)
            {
                var inst = Object.Instantiate(fbx);
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                var mf = inst.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    var verts = mf.sharedMesh.vertices;
                    var b = mf.sharedMesh.bounds;
                    sb.AppendLine($"[axe-pose] FBX mesh bounds (local) center={b.center} size={b.size} min={b.min} max={b.max}");
                    int longAxis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
                    float mid = b.center[longAxis];
                    // cross-section width (max extent in the other two axes) for verts below vs above mid.
                    Vector3 loMin = Vector3.one * 1e9f, loMax = Vector3.one * -1e9f, hiMin = loMin, hiMax = loMax;
                    foreach (var v in verts)
                    {
                        if (v[longAxis] < mid) { loMin = Vector3.Min(loMin, v); loMax = Vector3.Max(loMax, v); }
                        else { hiMin = Vector3.Min(hiMin, v); hiMax = Vector3.Max(hiMax, v); }
                    }
                    Vector3 loSize = loMax - loMin, hiSize = hiMax - hiMin;
                    int a1 = (longAxis + 1) % 3, a2 = (longAxis + 2) % 3;
                    float loCross = Mathf.Max(loSize[a1], loSize[a2]);
                    float hiCross = Mathf.Max(hiSize[a1], hiSize[a2]);
                    sb.AppendLine($"[axe-pose] longAxis={longAxis} mid={mid:F3}; LOW-half cross={loCross:F3} HIGH-half cross={hiCross:F3}");
                    sb.AppendLine($"[axe-pose] => HEAD (wide end) is on the {(loCross > hiCross ? "LOW" : "HIGH")} side of local axis {longAxis} " +
                                  $"(head local extent {(loCross > hiCross ? b.min[longAxis] : mid):F3}..{(loCross > hiCross ? mid : b.max[longAxis]):F3})");
                }
                Object.DestroyImmediate(inst);
            }

            // (b) StumpAxe world bounds vs the CraftStump world top.
            DumpAxeVsSurface(sb, "StumpAxe", "CraftStump");
            // (c) HeroAxe world bounds vs the castaway head bone + shoulder + SMR bounds.
            DumpAxeVsSurface(sb, "HeroAxe", null);

            sb.AppendLine("[axe-pose] ===== END POSE TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void DumpAxeVsSurface(StringBuilder sb, string axeName, string surfaceName)
        {
            GameObject axe = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == axeName) { axe = t.gameObject; break; }
            if (axe == null) { sb.AppendLine($"[axe-pose] {axeName} NOT FOUND"); return; }

            // Force-show so the bounds are real (both axes are visibility-gated and hidden at spawn).
            foreach (var r in axe.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var mrs = axe.GetComponentsInChildren<MeshRenderer>(true);
            Bounds wb = new Bounds(); bool init = false;
            foreach (var r in mrs) { if (!init) { wb = r.bounds; init = true; } else wb.Encapsulate(r.bounds); }
            sb.AppendLine($"[axe-pose] {axeName} world bounds min.y={wb.min.y:F3} max.y={wb.max.y:F3} center={wb.center} size={wb.size}");
            sb.AppendLine($"[axe-pose] {axeName} localPos={axe.transform.localPosition} localEuler={axe.transform.localEulerAngles} lossyScale={axe.transform.lossyScale}");

            if (surfaceName != null)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == surfaceName)
                    {
                        var smr = t.GetComponentInChildren<MeshRenderer>(true);
                        if (smr != null) sb.AppendLine($"[axe-pose] {surfaceName} world TOP.y={smr.bounds.max.y:F3} center={smr.bounds.center}");
                        break;
                    }
            }
            else
            {
                var castaway = Object.FindAnyObjectByType<FarHorizon.CastawayCharacter>();
                if (castaway != null)
                {
                    var bodySmr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (bodySmr != null)
                        sb.AppendLine($"[axe-pose] castaway SMR world max.y={bodySmr.bounds.max.y:F3} center.y={bodySmr.bounds.center.y:F3} (head≈max, mid≈center)");
                    var head = FarHorizon.CastawayProportions.FindHeadBone(castaway.transform);
                    if (head != null) sb.AppendLine($"[axe-pose] head bone world.y={head.position.y:F3}");
                }
            }
        }
    }
}
