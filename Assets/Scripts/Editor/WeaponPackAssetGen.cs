using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Imports the IN-HOUSE two-tier weapon SET (ticket 86cajkk7h — Sponsor-approved STONE + IRON recipes,
    /// 2026-07-03; [[weapon-two-tier-style-stone-iron]]) and wires the SHARED palette material. SCOPE: the
    /// matched family — axe / knife / sword / spear / PICKAXE (86cam9q5f — the 5th tool type) — in TWO tiers
    /// (stone = first-craft; iron = later progression), all on ONE shared URP/Unlit material so the SRP
    /// Batcher folds the whole set into ~1 SetPass (batches by shader variant, NOT material count).
    ///
    /// TIER DISPOSITION (86cajkk7h):
    ///   - STONE is the LIVE crafted tier: <see cref="HeroAxeFbxPath"/> == the stone axe, so the held /
    ///     stump / pickup gameplay axe (MovementCameraScene) + the [B] cycle + the belt-selection held
    ///     visual all resolve the STONE meshes. The stone family is the runtime source (WeaponSetLineup).
    ///   - IRON is imported + laid into the SAME family-display prefab for the soak's stone/iron CONTRAST
    ///     capture, but is NOT wielded (no iron-crafting system yet — that is an iron-progression DESIGN
    ///     follow-up, OOS here). The [B] cycle + belt sync read the STONE names only.
    ///
    /// Pipeline contract (`.claude/docs/blender-asset-pipeline.md`):
    ///   - ONE shared 128x128 sRGB palette PNG (weapon_palette.png; the +2 iron tone blocks landed with the
    ///     pack) + ONE URP/Unlit material (Mat_WeaponPalette). Every weapon UVs to the palette blocks.
    ///   - FBX exported from Blender with Normals Only (Mark Sharp -> custom split normals); Unity reads
    ///     them (importNormals = Import). Faceted look preserved exactly — do NOT recalculate (§4 + §9).
    ///   - Bake Axis Conversion ON (Blender Z-up -> Unity Y-up, no 90deg-X hack). (§9.)
    ///   - Material Creation Mode = None (we assign Mat_WeaponPalette by hand; no auto stubs). (§9.)
    ///   - animationType = None (static props). No colliders on the importer (the weapons are visual +
    ///     proximity-pickup; a MeshCollider is expensive per §8, and a collider on the hand-swung held axe
    ///     is undesirable — box colliders are deferred until a weapon needs physics interaction).
    ///
    /// SCALE — UNIFORM FAMILY NORMALIZATION (86cajkk7h). The new set is authored SMALLER than the retired
    /// axe (stone axe ~0.645u raw longest vs the retired ~1.02u) and to REAL scale against the 1.8m
    /// character reference (§1), so the Sponsor-approved family PROPORTIONS must be preserved. We measure
    /// the stone axe's imported longest-axis at globalScale=1, compute ONE globalScale that lands it at
    /// <see cref="NewFamilyAxeTargetLongestU"/> (== the retired axe's imported longest, so the stone axe is
    /// a DROP-IN for the existing held/stump/pickup seat + the HeroAxeSceneTests extent band — no seat
    /// re-derive, "don't regress the praised grip"), and apply that SAME globalScale to ALL 8 FBXs so
    /// every authored relative size (stone-vs-iron, axe-vs-sword-vs-spear) is preserved exactly. This
    /// REPLACES the retired axe's head-HEIGHT normalization (that machinery held a Sponsor-LOCKED head
    /// while the haft length varied — irrelevant to the fresh, approved-as-authored stone/iron meshes).
    ///
    /// Reproducible in batchmode:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.WeaponPackAssetGen.PrepareWeaponPack
    /// </summary>
    public static class WeaponPackAssetGen
    {
        public const string Dir = "Assets/Art/Props/WeaponPack";

        // === STONE tier — the LIVE crafted family (public API names kept so MovementCameraScene + the
        // WeaponSetTests carry forward; the VALUES now point at the stone FBXs). ===
        public const string AxeFbxPath = Dir + "/wpn_axe_stone_01.fbx";
        public const string KnifeFbxPath = Dir + "/wpn_knife_stone_01.fbx";
        public const string SwordFbxPath = Dir + "/wpn_sword_stone_01.fbx";
        public const string SpearFbxPath = Dir + "/wpn_spear_stone_01.fbx";

        // === IRON tier — imported + captured, NOT wielded (iron-progression is a DESIGN follow-up). ===
        public const string IronAxeFbxPath = Dir + "/wpn_axe_iron_01.fbx";
        public const string IronKnifeFbxPath = Dir + "/wpn_knife_iron_01.fbx";
        public const string IronSwordFbxPath = Dir + "/wpn_sword_iron_01.fbx";
        public const string IronSpearFbxPath = Dir + "/wpn_spear_iron_01.fbx";

        // === PICKAXE — the 5th tool type, both tiers (ticket 86cam9q5f — I-1 productionization of the
        // Sponsor-PASSED Blender burst, 2026-07-07; spec §4/I-1 + [[weapon-two-tier-style-stone-iron]]).
        // Authored via the family-extension route (blender-asset-pipeline §3): the approved stone/iron AXE
        // siblings duplicated, head swapped for a crosswise BOX-section pick head, haft/grip kept verbatim —
        // so both pickaxes share the axe family's grip origin + import normalization and drop onto the axe
        // seat (offset zero, unit scale) with no re-derive. Stone = 88 tris, iron = 154 tris. Both share the
        // one Mat_WeaponPalette (no per-asset atlas — the palette carries the pickaxe coords already). ===
        public const string PickaxeStoneFbxPath = Dir + "/wpn_pickaxe_stone_01.fbx";
        public const string PickaxeIronFbxPath = Dir + "/wpn_pickaxe_iron_01.fbx";

        public const string PalettePngPath = Dir + "/weapon_palette.png";
        public const string MaterialPath = Dir + "/Mat_WeaponPalette.mat";

        // The LIVE crafted axe FBX — consumed by MovementCameraScene for the held / stump / pickup axe.
        // 86cajkk7h: the STONE axe is the first-craft tier, so this is the stone axe (REPLACES the retired
        // wpn_axe_01 re-made flint axe + all its head-height-normalized length variants).
        public const string HeroAxeFbxPath = AxeFbxPath;

        // The family-display prefab (the shipped-build -verifyWeaponSet capture subject) carries BOTH tiers
        // (stone front row + iron back row) so the soak judges the stone/iron CONTRAST in ONE frame. The [B]
        // cycle + the belt-selection sync read the STONE nodes from it by name (HeldWeaponCycleDebug
        // .WeaponNodeNames); the iron nodes are display-only and ignored by the cycle.
        public const string PrefabPath = "Assets/Resources/WeaponSetLineup.prefab";

        // Target imported longest-axis for the STONE AXE (world units). == the retired flint axe's imported
        // longest (~1.08u: the byte-locked 0.65x head + 1.1x straight haft, held at HeldAxeLocalScaleUniform
        // 0.45 -> ~0.49u in-hand world extent), so the stone axe drops into the UNCHANGED held/stump/pickup
        // seat + the HeroAxeSceneTests extent band [0.4, 3.0] with no re-derive. The whole family is scaled
        // by the SINGLE globalScale this yields for the stone axe, so proportions are preserved. If the
        // stone axe .blend is ever re-authored at a different size, this target is the ONE knob to re-tune.
        public const float NewFamilyAxeTargetLongestU = 1.08f;

        // The LIVE stone family, paired with the family-display layout column (x) — the stone row (z=0).
        // 86cam9q5f: the stone pickaxe joins as the 5th column (the family-extension route shares the axe
        // haft, so it rides the SAME familyGlobalScale as the rest — proportions held).
        private static readonly (string path, float x)[] StoneSet =
        {
            (AxeFbxPath,          -0.75f),
            (KnifeFbxPath,        -0.25f),
            (SwordFbxPath,         0.25f),
            (SpearFbxPath,         0.85f),
            (PickaxeStoneFbxPath,  1.45f),
        };

        // The IRON family, same column layout — the iron row (z behind stone) for the contrast capture.
        private static readonly (string path, float x)[] IronSet =
        {
            (IronAxeFbxPath,      -0.75f),
            (IronKnifeFbxPath,    -0.25f),
            (IronSwordFbxPath,     0.25f),
            (IronSpearFbxPath,     0.85f),
            (PickaxeIronFbxPath,   1.45f),
        };

        public static void PrepareWeaponPack()
        {
            ConfigurePalette();
            var mat = CreateOrUpdateMaterial();

            // 1) Configure all 8 FBX importers at globalScale=1 (Normals=Import, BakeAxisConversion=ON,
            //    Material=None, animationType=None). The globalScale=1 pass makes the measured longest-axis
            //    read the real authored geometry before the family normalize.
            foreach (var (path, _) in StoneSet) ConfigureFbxImporter(path, 1f);
            foreach (var (path, _) in IronSet) ConfigureFbxImporter(path, 1f);

            // 2) Derive ONE family globalScale from the STONE AXE (the seat reference) and apply it to ALL 8
            //    so the Sponsor-approved family proportions are preserved and the stone axe lands at the
            //    retired axe's imported size (drop-in seat). knife/sword/spear/iron ride the same factor.
            float axeLongest = MeasureImportedModelLongestAxis(AxeFbxPath);
            float familyGlobalScale = 1f;
            if (axeLongest > 0.0001f)
            {
                familyGlobalScale = NewFamilyAxeTargetLongestU / axeLongest;
                foreach (var (path, _) in StoneSet) ConfigureFbxImporter(path, familyGlobalScale);
                foreach (var (path, _) in IronSet) ConfigureFbxImporter(path, familyGlobalScale);
                float axeFinal = MeasureImportedModelLongestAxis(AxeFbxPath);
                Debug.Log($"[WeaponPackAssetGen] family scale normalize: stone-axe longest {axeLongest:F4}u @gs1 -> " +
                          $"globalScale={familyGlobalScale:F5} (target {NewFamilyAxeTargetLongestU}u; " +
                          $"resulting stone-axe longest={axeFinal:F4}u — same factor on all 8, proportions held).");
            }
            else
            {
                Debug.LogError("[WeaponPackAssetGen] could not measure the stone axe longest-axis — family " +
                               "scale left at globalScale=1 (weapons will read undersized; check the FBX imported).");
            }

            // 3) Build the family-display prefab (both tiers) — the shipped-build capture subject + the [B]
            //    cycle / belt-sync runtime mesh source (stone nodes).
            BuildFamilyPrefab(mat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeaponPackAssetGen] two-tier weapon SET prepared: STONE (live) + IRON (capture-only), " +
                      "axe/knife/sword/spear, shared palette material.");
        }

        // Build Resources/WeaponSetLineup.prefab = the weapon family stood up in rows, all sharing
        // Mat_WeaponPalette + a directional light. STONE front row (z=0), IRON back row (z behind), so
        // WeaponSetVerifyCapture captures the tier CONTRAST from the BUILT exe (AssetDatabase is editor-only;
        // the build path needs a Resources source). Standing upright (the Blender +Z blade-up axis maps to
        // +Y up after Bake Axis Conversion). The child nodes are named after their FBX (file-name-without-
        // extension) so HeldWeaponCycleDebug resolves the STONE meshes by name; iron nodes are display-only.
        private static void BuildFamilyPrefab(Material mat)
        {
            if (mat == null) return;
            var root = new GameObject("WeaponSetLineup");

            AddRow(root.transform, StoneSet, 0f, mat);   // stone — front row
            AddRow(root.transform, IronSet, 1.1f, mat);  // iron  — back row (clear of the stone row)

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
            Debug.Log("[WeaponPackAssetGen] built " + PrefabPath + " (10-item family: stone + iron rows incl. " +
                  "pickaxe, shared material)");
        }

        private static void AddRow(Transform parent, (string path, float x)[] set, float z, Material mat)
        {
            foreach (var (path, x) in set)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (fbx == null) { Debug.LogWarning("[WeaponPackAssetGen] family prefab: FBX missing " + path); continue; }
                var item = Object.Instantiate(fbx);
                item.name = Path.GetFileNameWithoutExtension(path);
                item.transform.SetParent(parent, false);
                item.transform.localPosition = new Vector3(x, 0f, z);
                foreach (var mr in item.GetComponentsInChildren<MeshRenderer>(true))
                    mr.sharedMaterial = mat;
            }
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

        // Configure ONE weapon FBX importer per pipeline §9 at the given globalScale. Static prop: no
        // animation, no auto-material, faceted normals imported AS-IS (Mark-Sharp custom split normals),
        // Blender Z-up -> Unity Y-up via Bake Axis Conversion.
        private static void ConfigureFbxImporter(string fbxPath, float globalScale)
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
            importer.addCollider = false;         // no MeshCollider (visual + proximity-pickup; §8)
            importer.globalScale = globalScale;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[WeaponPackAssetGen] reimported " + fbxPath + " @ globalScale=" + globalScale.ToString("F5") +
                      ": static prop, Normals=Import, BakeAxisConversion=ON, Material=None");
        }

        // Longest world-axis of the imported model at its current import scale — the family-normalize
        // reference. Instantiate + encapsulate renderer bounds (post-import, in world units).
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
