using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Imports the IN-HOUSE weapon pack (Route A — DECISIONS.md 2026-06-19) and wires the
    /// SHARED palette material. STYLE-CHECKPOINT scope: the re-made hero axe (wpn_axe_01.fbx)
    /// only — knife/sword/spear follow in the next dispatch once the Sponsor locks the look.
    ///
    /// Pipeline contract (`.claude/docs/blender-asset-pipeline.md`):
    ///   - ONE shared 128x128 sRGB palette PNG (weapon_palette.png, locked 9 hexes) + ONE
    ///     URP/Unlit material (Mat_WeaponPalette). Every weapon UVs to the palette blocks, so
    ///     the SRP Batcher folds the whole set into ~1 SetPass (batches by shader variant).
    ///   - FBX exported from Blender with Normals Only (Mark Sharp -> custom split normals);
    ///     Unity reads them (importNormals = Import). Faceted look preserved exactly — do NOT
    ///     recalculate (that would smooth the facets and lose the flat-shaded read).
    ///   - Bake Axis Conversion ON (Blender Z-up -> Unity Y-up, no 90deg-X hack).
    ///   - Material Creation Mode = None (we assign Mat_WeaponPalette by hand; no auto stubs).
    ///   - Box Collider on the prop instance (not MeshCollider — cheap hand tool).
    ///
    /// Reproducible in batchmode:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.WeaponPackAssetGen.PrepareWeaponPack
    /// </summary>
    public static class WeaponPackAssetGen
    {
        public const string Dir = "Assets/Art/Props/WeaponPack";
        public const string AxeFbxPath = Dir + "/wpn_axe_01.fbx";
        public const string PalettePngPath = Dir + "/weapon_palette.png";
        public const string MaterialPath = Dir + "/Mat_WeaponPalette.mat";

        public static void PrepareWeaponPack()
        {
            ConfigurePalette();
            var mat = CreateOrUpdateMaterial();
            ConfigureFbxImporter();
            AssignMaterial(AxeFbxPath, mat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeaponPackAssetGen] weapon pack prepared (axe-only checkpoint): " + AxeFbxPath);
        }

        // Palette PNG: flat-colour blocks viewed at fixed UV dots -> sRGB, point-filtered, no mips,
        // Read/Write off. Power-of-two (128) so GPU compression works; but compression would muddy
        // the crisp palette blocks, so keep it uncompressed (truecolor).
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
            ti.mipmapEnabled = false;       // UI-like fixed-size sampling; mips waste memory + bleed blocks
            ti.filterMode = FilterMode.Point; // crisp palette blocks, no inter-block bleed
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 128;
            var plat = ti.GetDefaultPlatformTextureSettings();
            plat.format = TextureImporterFormat.RGBA32; // uncompressed truecolor — exact hexes
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

        private static void ConfigureFbxImporter()
        {
            var importer = AssetImporter.GetAtPath(AxeFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[WeaponPackAssetGen] axe FBX not found at " + AxeFbxPath);
                return;
            }
            // Static prop, no rig/anim.
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            // Read Blender's exported (Mark-Sharp-derived) custom split normals AS-IS — the faceted
            // read IS the style; recalculating would smooth the facets. (Pipeline doc §4 + §9.)
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.Import;
            importer.bakeAxisConversion = true;   // Blender Z-up -> Unity Y-up
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            // No auto material stubs — we assign Mat_WeaponPalette explicitly.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            // FBX exported in metres at the real ~1.0u prop length; import at unit scale.
            importer.globalScale = 1f;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[WeaponPackAssetGen] axe FBX reimported: static prop, Normals=Import, " +
                      "BakeAxisConversion=ON, Material=None");
        }

        private static void AssignMaterial(string fbxPath, Material mat)
        {
            if (mat == null) return;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) return;
            // The shared material binds at instantiate time via the capture/scene wiring; nothing to
            // remap on the importer itself when Material Creation Mode = None (no sub-asset slots).
            Debug.Log("[WeaponPackAssetGen] (material assigned at scene/instantiate time: " + mat.name + ")");
        }
    }
}
