using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the WOOD tier — the crudest first-craft rung (ticket 86catqn5n; the Sponsor-PASSED
    /// wood weapon burst, 2026-07-18; DECISIONS 2026-07-18). Sibling of <see cref="WeaponSetTests"/> /
    /// <see cref="PickaxeAssetTests"/> — the ASSET-presence + shared-material-honesty guards for the 5
    /// harvested wood FBXs, plus the lineup wiring so the wood tools resolve a REAL modeled mesh (not a
    /// letter-chip placeholder).
    ///
    /// The wood ITEM-ID → catalog resolution (all 5 ids resolve in BOTH ItemCatalog + WeaponCatalog) is
    /// already covered by <c>CraftingSeamTests.WoodTierIds_ResolveInBothCatalogs_AsTools</c> — NOT duplicated
    /// here. This suite owns the ASSET + lineup-wiring surface.
    ///
    /// Regression-guard intent (the bug CLASS, not the instance):
    ///   - All 5 wood FBXs must import as real modeled meshes (the export didn't drop a placeholder primitive).
    ///   - All 5 must stay chunky low-poly — a Sub-D / smooth blow-out reds the budget (real values 58-90 tris).
    ///   - The lineup prefab (the tier-contrast capture subject + the [B] picker mesh source) must carry all 5
    ///     wood nodes on the ONE shared Mat_WeaponPalette — a per-asset atlas reds the material-honesty assert
    ///     (blender-asset-pipeline §0/§2 — the CC-BY-axe outlier Route A retired).
    ///   - dagger_wood reuses the wpn_knife_* naming (§6a) — pin the id↔asset naming so a future rename reds.
    /// </summary>
    public class WoodWeaponSetTests
    {
        private static readonly string[] WoodFbxPaths =
        {
            WeaponPackAssetGen.AxeWoodFbxPath,
            WeaponPackAssetGen.PickaxeWoodFbxPath,
            WeaponPackAssetGen.SpearWoodFbxPath,
            WeaponPackAssetGen.KnifeWoodFbxPath,
            WeaponPackAssetGen.SwordWoodFbxPath,
        };

        private static readonly string[] WoodNodeNames =
        {
            "wpn_axe_wood_01", "wpn_pickaxe_wood_01", "wpn_spear_wood_01",
            "wpn_knife_wood_01", "wpn_sword_wood_01",
        };

        [Test]
        public void AllFiveWoodWeapons_FbxImport_AsRealMeshes()
        {
            foreach (var path in WoodFbxPaths)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(fbx, $"wood FBX must import at {path} (the in-house pipeline export)");
                var mf = fbx.GetComponentInChildren<MeshFilter>(true);
                Assert.IsNotNull(mf, $"{path} must carry a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"{path} mesh must import");
                Assert.Greater(mf.sharedMesh.vertexCount, 8,
                    $"{path} must be a real modeled wood weapon, not a placeholder primitive");
            }
        }

        [Test]
        public void EveryWoodWeapon_StaysChunkyLowPoly_WithinTriBudget()
        {
            // The wood burst authored 58-90 tris/piece (family-extension route off the approved siblings). A
            // generous 300 ceiling catches an accidental Sub-D/smooth blow-out; the real values are far below it.
            foreach (var path in WoodFbxPaths)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(fbx, path);
                int tris = 0;
                foreach (var mf in fbx.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                Assert.That(tris, Is.GreaterThan(8).And.LessThanOrEqualTo(300),
                    $"{path} tri count {tris} must be chunky low-poly (<= 300); a blow-out means a Sub-D / " +
                    "smoothing modifier slipped into the export.");
            }
        }

        [Test]
        public void LineupPrefab_CarriesAllFiveWoodNodes_OnTheSharedMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.PrefabPath);
            Assert.IsNotNull(prefab,
                $"the family lineup prefab (the capture subject + picker mesh source) must exist at " +
                $"{WeaponPackAssetGen.PrefabPath}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WeaponPackAssetGen.MaterialPath);
            Assert.IsNotNull(mat, "the shared Mat_WeaponPalette must exist");

            foreach (var nodeName in WoodNodeNames)
            {
                Transform node = FindByName(prefab.transform, nodeName);
                Assert.IsNotNull(node,
                    $"the lineup prefab must carry a '{nodeName}' node (WeaponPackAssetGen must add the wood " +
                    "row to the lineup, or the wood tool resolves no real mesh — a letter-chip placeholder).");
                var mf = node.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, $"lineup node '{nodeName}' must carry a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"lineup node '{nodeName}' must carry a serialized mesh");
                Assert.Greater(mf.sharedMesh.vertexCount, 8, $"lineup node '{nodeName}' must be a real wood mesh");
                var mr = node.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, $"lineup node '{nodeName}' must carry a MeshRenderer");
                Assert.AreEqual(mat, mr.sharedMaterial,
                    $"wood lineup node '{nodeName}' must use the shared Mat_WeaponPalette — a per-asset " +
                    "material breaks the ~1-draw-call family contract (material-honesty; blender-asset-pipeline §2).");
            }
        }

        [Test]
        public void DaggerWood_ReusesKnifeMeshNaming_PerConvention()
        {
            // §6a: the wood DAGGER item id (dagger_wood) reuses the wpn_knife_* FBX (an asset rename is pure
            // churn). Pin the id↔asset naming so a future rename that crosses them reds here.
            Assert.AreEqual(WeaponPackAssetGen.Dir + "/wpn_knife_wood_01.fbx", WeaponPackAssetGen.KnifeWoodFbxPath,
                "dagger_wood reuses the wpn_knife_wood_01 mesh (naming convention §6a).");
            Assert.AreEqual("dagger_wood", ItemCatalog.DaggerWoodId, "the wood dagger item id is dagger_wood");
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
