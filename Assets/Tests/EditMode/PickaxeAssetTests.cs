using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the PICKAXE — the 5th tool type, both tiers (ticket 86cam9q5f, I-1 productionization
    /// of the Sponsor-PASSED Blender burst; spec §4/I-1 + [[weapon-two-tier-style-stone-iron]]). Sibling of
    /// <see cref="WeaponSetTests"/> — the asset-presence + shared-material-honesty guards for the harvested
    /// FBXs, plus the [B]-picker cycle contract that seats both pickaxes in-hand (Bar 5).
    ///
    /// Regression-guard intent (the bug CLASS, not the instance):
    ///   - Both pickaxe FBXs must import as real modeled meshes (harvest didn't drop a placeholder).
    ///   - Both must stay chunky low-poly — a Sub-D / smooth blow-out reds the budget.
    ///   - The lineup prefab (the cycle's runtime mesh source + the contrast-capture subject) must carry BOTH
    ///     pickaxe nodes on the ONE shared Mat_WeaponPalette — a per-asset atlas (the retired outlier) reds
    ///     the material-honesty assert (blender-asset-pipeline §0/§2).
    ///   - The [B] picker cycle must NAME both pickaxe nodes at their indices with the axe-seat baseline —
    ///     a rename/reorder that crosses the held visual, or a non-axe-baseline seat, reds here.
    ///
    /// (The pickaxe ItemCatalog/WeaponCatalog id + belt-eligibility resolution is covered by I-0's
    /// ItemCatalogIronTests; this suite owns the ASSET + picker-wiring surface.)
    /// </summary>
    public class PickaxeAssetTests
    {
        private static readonly string[] PickaxeFbxPaths =
        {
            WeaponPackAssetGen.PickaxeStoneFbxPath,
            WeaponPackAssetGen.PickaxeIronFbxPath,
        };

        [Test]
        public void BothPickaxes_FbxImport_AsRealMeshes()
        {
            foreach (var path in PickaxeFbxPaths)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(fbx, $"pickaxe FBX must import at {path} (the harvested in-house pipeline export)");
                var mf = fbx.GetComponentInChildren<MeshFilter>(true);
                Assert.IsNotNull(mf, $"{path} must carry a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"{path} mesh must import");
                Assert.Greater(mf.sharedMesh.vertexCount, 8,
                    $"{path} must be a real modeled pickaxe, not a placeholder primitive");
            }
        }

        [Test]
        public void BothPickaxes_StayChunkyLowPoly_WithinTriBudget()
        {
            // The burst authored the stone pickaxe at 88 tris + the iron at 154 tris (family-extension route
            // off the approved axes). A generous 400 ceiling catches an accidental Sub-D/smooth blow-out; the
            // real values are far below it.
            foreach (var path in PickaxeFbxPaths)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(fbx, path);
                int tris = 0;
                foreach (var mf in fbx.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                Assert.That(tris, Is.GreaterThan(8).And.LessThanOrEqualTo(400),
                    $"{path} tri count {tris} must be chunky low-poly (<= 400); a blow-out means a Sub-D / " +
                    "smoothing modifier slipped into the export.");
            }
        }

        [Test]
        public void LineupPrefab_CarriesBothPickaxeNodes_OnTheSharedMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.PrefabPath);
            Assert.IsNotNull(prefab,
                $"the family lineup prefab (the picker mesh source + capture subject) must exist at " +
                $"{WeaponPackAssetGen.PrefabPath}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WeaponPackAssetGen.MaterialPath);
            Assert.IsNotNull(mat, "the shared Mat_WeaponPalette must exist");

            foreach (var nodeName in new[] { "wpn_pickaxe_stone_01", "wpn_pickaxe_iron_01" })
            {
                Transform node = FindByName(prefab.transform, nodeName);
                Assert.IsNotNull(node,
                    $"the lineup prefab must carry a '{nodeName}' node (WeaponPackAssetGen must add the pickaxe " +
                    "to Stone/IronSet, or the [B] picker resolves no mesh for it).");
                var mf = node.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, $"lineup node '{nodeName}' must carry a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"lineup node '{nodeName}' must carry a serialized mesh");
                Assert.Greater(mf.sharedMesh.vertexCount, 8, $"lineup node '{nodeName}' must be a real pickaxe mesh");
                var mr = node.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, $"lineup node '{nodeName}' must carry a MeshRenderer");
                Assert.AreEqual(mat, mr.sharedMaterial,
                    $"pickaxe lineup node '{nodeName}' must use the shared Mat_WeaponPalette — a per-asset " +
                    "material breaks the ~1-draw-call family contract (material-honesty; blender-asset-pipeline §2).");
            }
        }

        [Test]
        public void Picker_NamesBothPickaxeTiers_AtTheirIndices_WithLabels()
        {
            // The [B] picker cycle must name both pickaxe nodes at the appended indices (4 stone / 5 iron),
            // AND leave the existing axe(0)/spear(3) indices unchanged (append, never insert — the belt-sync
            // contract in HeldBeltVisualSyncTests depends on those).
            Assert.AreEqual(0, HeldWeaponCycleDebug.AxeFamilyIndex, "axe stays index 0 (locked default)");
            Assert.AreEqual(3, HeldWeaponCycleDebug.SpearFamilyIndex, "spear stays index 3 (belt-sync contract)");
            Assert.AreEqual("wpn_axe_stone_01", HeldWeaponCycleDebug.WeaponNodeNames[0], "axe index unchanged");
            Assert.AreEqual("wpn_spear_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SpearFamilyIndex], "spear index unchanged");

            Assert.AreEqual(4, HeldWeaponCycleDebug.PickaxeStoneFamilyIndex);
            Assert.AreEqual(5, HeldWeaponCycleDebug.PickaxeIronFamilyIndex);
            Assert.AreEqual("wpn_pickaxe_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.PickaxeStoneFamilyIndex],
                "the stone pickaxe node must sit at PickaxeStoneFamilyIndex (the picker resolves it by name)");
            Assert.AreEqual("wpn_pickaxe_iron_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.PickaxeIronFamilyIndex],
                "the iron pickaxe node must sit at PickaxeIronFamilyIndex");
            Assert.AreEqual("PICKAXE STONE",
                HeldWeaponCycleDebug.WeaponLabels[HeldWeaponCycleDebug.PickaxeStoneFamilyIndex]);
            Assert.AreEqual("PICKAXE IRON",
                HeldWeaponCycleDebug.WeaponLabels[HeldWeaponCycleDebug.PickaxeIronFamilyIndex]);
        }

        [Test]
        public void Picker_PickaxeSeats_StartFromTheAxeSeat_ZeroOffsetUnitScale()
        {
            // 86cam9q5f: the pickaxe shares the stone-axe haft (family-extension route) so it seats at the
            // axe's baseline — zero mesh-holder offset/euler + unit scale — with no per-weapon compensation.
            // The Sponsor micro-dials at the picker soak (Bar 5/8); this pins the shipped STARTING seat.
            foreach (int i in new[] { HeldWeaponCycleDebug.PickaxeStoneFamilyIndex,
                                      HeldWeaponCycleDebug.PickaxeIronFamilyIndex })
            {
                Assert.AreEqual(Vector3.zero, HeldWeaponCycleDebug.WeaponMeshLocalOffset[i],
                    $"{HeldWeaponCycleDebug.WeaponLabels[i]} must start from the axe seat (zero offset).");
                Assert.AreEqual(Vector3.zero, HeldWeaponCycleDebug.WeaponMeshLocalEuler[i],
                    $"{HeldWeaponCycleDebug.WeaponLabels[i]} must start from the axe seat (zero euler).");
                Assert.AreEqual(1f, HeldWeaponCycleDebug.WeaponMeshScale[i], 1e-4f,
                    $"{HeldWeaponCycleDebug.WeaponLabels[i]} must start from the axe seat (unit scale) — it " +
                    "shares the axe haft + familyGlobalScale.");
            }
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
