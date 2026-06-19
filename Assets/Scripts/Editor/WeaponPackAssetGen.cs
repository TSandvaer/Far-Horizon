using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Imports the IN-HOUSE weapon pack (Route A — DECISIONS.md 2026-06-19) and wires the SHARED palette
    /// material. SCOPE (ticket 86cabh907): the matched SET — re-made knapped-flint hero axe (wpn_axe_01)
    /// + knife (wpn_knife_01) + sword (wpn_sword_01) + spear (wpn_spear_01) — all on ONE shared URP/Unlit
    /// material so the SRP Batcher folds the whole set into ~1 SetPass (batches by shader variant).
    ///
    /// Pipeline contract (`.claude/docs/blender-asset-pipeline.md`):
    ///   - ONE shared 128x128 sRGB palette PNG (weapon_palette.png, 9 locked hexes) + ONE URP/Unlit
    ///     material (Mat_WeaponPalette). Every weapon UVs to the palette blocks.
    ///   - FBX exported from Blender with Normals Only (Mark Sharp -> custom split normals); Unity reads
    ///     them (importNormals = Import). Faceted look preserved exactly — do NOT recalculate.
    ///   - Bake Axis Conversion ON (Blender Z-up -> Unity Y-up, no 90deg-X hack).
    ///   - Material Creation Mode = None (we assign Mat_WeaponPalette by hand; no auto stubs).
    ///   - The hero axe is HEIGHT-NORMALIZED to ~1.0u longest axis (like the retired CC-BY AxeAssetGen)
    ///     so the soak-locked held-axe scale (MovementCameraScene.HeldAxeLocalScaleUniform) preserves the
    ///     established in-hand reference size without a re-derive. (86cabh907 — CC-BY retirement.)
    ///
    /// Reproducible in batchmode:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.WeaponPackAssetGen.PrepareWeaponPack
    /// </summary>
    public static class WeaponPackAssetGen
    {
        public const string Dir = "Assets/Art/Props/WeaponPack";
        public const string AxeFbxPath = Dir + "/wpn_axe_01.fbx";
        public const string KnifeFbxPath = Dir + "/wpn_knife_01.fbx";
        public const string SwordFbxPath = Dir + "/wpn_sword_01.fbx";
        public const string SpearFbxPath = Dir + "/wpn_spear_01.fbx";
        public const string PalettePngPath = Dir + "/weapon_palette.png";
        public const string MaterialPath = Dir + "/Mat_WeaponPalette.mat";

        // The re-made hero axe FBX + its shared material — consumed by MovementCameraScene for the held /
        // stump / pickup axe (86cabh907: REPLACES the CC-BY AxeAssetGen.FbxPath + its baked atlas material).
        public const string HeroAxeFbxPath = AxeFbxPath;

        public const string PrefabPath = "Assets/Resources/WeaponSetLineup.prefab";

        // Normalize the hero axe's intrinsic longest-axis height to this many world units (matches the
        // retired CC-BY AxeAssetGen.TargetImportHeightU), so the held-axe scale is unchanged on swap.
        public const float HeroAxeTargetImportHeightU = 1.0f;

        // The set, paired with whether to height-normalize (only the hero axe rides the held-rig scale).
        private static readonly (string path, bool normalizeHeight)[] Set =
        {
            (AxeFbxPath,   true),
            (KnifeFbxPath, false),
            (SwordFbxPath, false),
            (SpearFbxPath, false),
        };

        public static void PrepareWeaponPack()
        {
            ConfigurePalette();
            var mat = CreateOrUpdateMaterial();
            foreach (var (path, norm) in Set)
                ConfigureFbxImporter(path, norm);
            BuildLineupPrefab(mat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeaponPackAssetGen] weapon SET prepared: axe + knife + sword + spear (shared palette)");
        }

        // Build Resources/WeaponSetLineup.prefab = the four weapons stood up in a row, all sharing
        // Mat_WeaponPalette + a directional light, so WeaponSetVerifyCapture loads + captures the FAMILY
        // from the BUILT exe (AssetDatabase is editor-only; the build path needs Resources). Standing
        // upright (the Blender +Z blade-up axis maps to +Y up after Bake Axis Conversion).
        private static void BuildLineupPrefab(Material mat)
        {
            if (mat == null) return;
            var root = new GameObject("WeaponSetLineup");

            (string path, float x)[] layout =
            {
                (AxeFbxPath,   -0.75f),
                (KnifeFbxPath, -0.25f),
                (SwordFbxPath,  0.25f),
                (SpearFbxPath,  0.85f),
            };
            foreach (var (path, x) in layout)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (fbx == null) { Debug.LogWarning("[WeaponPackAssetGen] lineup: FBX missing " + path); continue; }
                var item = Object.Instantiate(fbx);
                item.name = Path.GetFileNameWithoutExtension(path);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(x, 0f, 0f);
                foreach (var mr in item.GetComponentsInChildren<MeshRenderer>(true))
                    mr.sharedMaterial = mat;
            }

            var lightGo = new GameObject("StandLight");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = Color.white;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Directory.CreateDirectory("Assets/Resources");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[WeaponPackAssetGen] built " + PrefabPath + " (4-item family lineup, shared material)");
        }

        // Palette PNG: flat-colour blocks viewed at fixed UV dots -> sRGB, point-filtered, no mips,
        // Read/Write off. Power-of-two (128); kept uncompressed (truecolor) so the crisp blocks survive.
        private static void ConfigurePalette()
        {
            var ti = AssetImporter.GetAtPath(PalettePngPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogError("[WeaponPackAssetGen] palette PNG missing at " + PalettePngPath);
                return;
            }
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 128;
            var plat = ti.GetDefaultPlatformTextureSettings();
            plat.format = TextureImporterFormat.RGBA32;
            plat.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SetPlatformTextureSettings(plat);
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
            Debug.Log("[WeaponPackAssetGen] palette import configured (sRGB, point, no-mip, RGBA32)");
        }

        private static Material CreateOrUpdateMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePngPath);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("[WeaponPackAssetGen] URP/Unlit shader not found — is URP installed?");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(unlit);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else
            {
                mat.shader = unlit;
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            Debug.Log("[WeaponPackAssetGen] Mat_WeaponPalette ready (URP/Unlit + palette BaseMap)");
            return mat;
        }

        private static void ConfigureFbxImporter(string fbxPath, bool normalizeHeight)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[WeaponPackAssetGen] FBX not found at " + fbxPath);
                return;
            }
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            // Read Blender's exported (Mark-Sharp-derived) custom split normals AS-IS — the faceted read IS
            // the style; recalculating would smooth the facets. (Pipeline doc §4 + §9.)
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.Import;
            importer.bakeAxisConversion = true;   // Blender Z-up -> Unity Y-up
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.globalScale = 1f;
            // First reimport at unit scale so MeasureLongestAxis reads the real geometry.
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            if (normalizeHeight)
            {
                float measured = MeasureImportedModelLongestAxis(fbxPath);
                if (measured > 0.0001f)
                {
                    importer.globalScale = importer.globalScale * (HeroAxeTargetImportHeightU / measured);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    Debug.Log($"[WeaponPackAssetGen] hero-axe height-normalize: longestAxis={measured:F3}u -> " +
                              $"globalScale={importer.globalScale:F5} (target {HeroAxeTargetImportHeightU}u)");
                }
            }
            Debug.Log("[WeaponPackAssetGen] reimported " + fbxPath +
                      ": static prop, Normals=Import, BakeAxisConversion=ON, Material=None");
        }

        private static float MeasureImportedModelLongestAxis(string fbxPath)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
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
    }
}
