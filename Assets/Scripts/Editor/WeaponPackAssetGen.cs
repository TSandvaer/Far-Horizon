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

        // Normalize the hero axe by its HEAD HEIGHT (not its longest axis) to this many world units, so the
        // Sponsor-LOCKED 0.65x head keeps its approved ABSOLUTE in-game size INDEPENDENTLY of the haft length
        // (86cabh907 FINAL bake — 2.0x straight haft + locked head). WHY head-height, not longest-axis: the
        // longest axis is now the 2.0x HAFT, so normalizing the longest axis to 1.0u would SHRINK the byte-locked
        // head ~33% (1.418u total -> 1.0u) — defeating "head 0.65x LOCKED". 0.4710u == the head's size under the
        // PRIOR shipped normalization (old head_h 0.4453 mesh-u x the old longest-axis globalScale 1.05781), so
        // pinning the head to 0.4710u reproduces the exact approved head while the haft grows to ~1.50u total
        // (1.418 mesh-u x 1.05781). The haft:head RATIO (~2.12:1) is scale-invariant either way; this preserves
        // the head's ABSOLUTE size. headJunctionFraction (0.50) splits head-from-haft the same way the runtime
        // dial does (HeldWeaponCycleDebug). If the head .blend is ever re-baked at a different size, re-derive
        // this target from the new approved head.
        public const float HeroAxeTargetHeadHeightU = 0.4710f;
        // The head's long-axis height in the .blend / FINAL-bake mesh units: head_top (z=+0.495347) minus the
        // byte-locked head<->haft junction (z=+0.022674) = 0.47267u. The head is byte-LOCKED, so this distance
        // from the blade TIP down to the junction is INVARIANT to haft length (the 2.0x haft only extends the
        // OTHER end downward). MeasureImportedModelHeadHeight locates the blade-TIP end (the wide cross-section
        // end) and reads the head as the verts within this distance of the tip — SIGN-ROBUST to the bake-axis
        // conversion (no assumption about which Unity axis/sign the Blender +Z maps to). Do NOT use a
        // fraction-of-span junction — the 2x haft moves the 50%-of-span point mid-haft.
        public const float HeroAxeHeadHeightFromTipU = 0.47267f;

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
                // Normalize by HEAD HEIGHT (not longest axis) so the byte-locked 0.65x head keeps its approved
                // absolute size while the 2.0x haft grows the total length (86cabh907 FINAL bake). The head is
                // identified by its RADIAL WIDTH (the blade is far wider than the ~0.07u haft), so the split is
                // independent of haft LENGTH — robust to the 2x handle.
                float headH = MeasureImportedModelHeadHeight(fbxPath);
                if (headH > 0.0001f)
                {
                    importer.globalScale = importer.globalScale * (HeroAxeTargetHeadHeightU / headH);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    float total = MeasureImportedModelLongestAxis(fbxPath);
                    Debug.Log($"[WeaponPackAssetGen] hero-axe HEAD-height normalize: headH={headH:F4}u -> " +
                              $"globalScale={importer.globalScale:F5} (target head {HeroAxeTargetHeadHeightU}u; " +
                              $"resulting longest-axis total={total:F3}u — the haft grew, the head held).");
                }
            }
            Debug.Log("[WeaponPackAssetGen] reimported " + fbxPath +
                      ": static prop, Normals=Import, BakeAxisConversion=ON, Material=None");
        }

        // Head height of the imported axe, robust to haft LENGTH: the head = the verts ABOVE the byte-locked
        // head<->haft junction on the long axis (HeroAxeHeadBaseLongAxisCoordU). Head height = the long-axis SPAN
        // of those head verts. The 2.0x haft only extends DOWNWARD (below the junction), so the measured head
        // height is INVARIANT to haft length — exactly what keeps the head's absolute size locked (86cabh907).
        // The model is imported at globalScale=1 when this runs (ConfigureFbxImporter sets it to 1f first), so the
        // long-axis coords are in .blend/mesh units and the junction const compares directly.
        private static float MeasureImportedModelHeadHeight(string fbxPath)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) return 0f;
            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            var mf = inst.GetComponentInChildren<MeshFilter>();
            float headH = 0f;
            if (mf != null && mf.sharedMesh != null)
            {
                var verts = mf.sharedMesh.vertices; // LOCAL space (model units at globalScale=1)
                int n = verts.Length;
                if (n > 0)
                {
                    // long axis = the widest bounds axis (the haft+head run along it)
                    Vector3 bMin = verts[0], bMax = verts[0];
                    for (int i = 1; i < n; i++) { bMin = Vector3.Min(bMin, verts[i]); bMax = Vector3.Max(bMax, verts[i]); }
                    Vector3 ext = bMax - bMin;
                    int la = (ext.x >= ext.y && ext.x >= ext.z) ? 0 : (ext.y >= ext.z ? 1 : 2);
                    int o0 = (la + 1) % 3, o1 = (la + 2) % 3;
                    float loEnd = bMin[la], hiEnd = bMax[la];
                    // Which long-axis END is the blade (head)? The blade has the WIDE off-axis cross-section; the
                    // haft is thin. Measure the off-axis spread of the verts within 15% of span of each end and
                    // pick the wider end as the TIP. Sign-robust to the bake-axis conversion.
                    float span = Mathf.Max(1e-4f, hiEnd - loEnd);
                    float band = span * 0.15f;
                    float SpreadNear(float endCoord)
                    {
                        float mn0 = float.MaxValue, mx0 = float.MinValue, mn1 = float.MaxValue, mx1 = float.MinValue;
                        for (int i = 0; i < n; i++)
                            if (Mathf.Abs(verts[i][la] - endCoord) <= band)
                            {
                                mn0 = Mathf.Min(mn0, verts[i][o0]); mx0 = Mathf.Max(mx0, verts[i][o0]);
                                mn1 = Mathf.Min(mn1, verts[i][o1]); mx1 = Mathf.Max(mx1, verts[i][o1]);
                            }
                        return Mathf.Max(mx0 - mn0, mx1 - mn1);
                    }
                    float tipCoord = SpreadNear(hiEnd) >= SpreadNear(loEnd) ? hiEnd : loEnd;
                    int dir = (tipCoord == hiEnd) ? -1 : 1; // toward the junction from the tip
                    float junctionCoord = tipCoord + dir * HeroAxeHeadHeightFromTipU;
                    float headLo = float.MaxValue, headHi = float.MinValue;
                    for (int i = 0; i < n; i++)
                    {
                        float lc = verts[i][la];
                        bool isHead = (dir == -1) ? (lc >= junctionCoord) : (lc <= junctionCoord);
                        if (isHead) { if (lc < headLo) headLo = lc; if (lc > headHi) headHi = lc; }
                    }
                    if (headHi > headLo) headH = headHi - headLo;
                }
            }
            Object.DestroyImmediate(inst);
            return headH;
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
