using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using FarHorizon;
using UnityIs = UnityEngine.TestTools.Constraints.Is;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the ③ forge-rework (ticket 86camz9vh): the folded GC NIT (Forge.CurrentRecipe no longer
    /// allocates per access — 86camw8rm), the forge place-to-build cost defaults (6w+12s), the #302 self-register
    /// WIRING (Build/Reveal enable the PlacementObstacle), and the iron in-hand picker append contract (Bar 5).
    /// Pure-logic (no scene) — the runtime OnEnable→Register lifecycle + the shipped visual are proven in PlayMode
    /// + the shipped-build capture.
    /// </summary>
    public class IronForgeTests
    {
        // === Folded GC NIT (86camw8rm) — Forge.CurrentRecipe caches; no per-frame GC.Alloc ===

        [Test]
        public void CurrentRecipe_IsCached_SameReference_WhenDialsUnchanged()
        {
            var go = new GameObject("Forge");
            var forge = go.AddComponent<Forge>();
            forge.orePerIngot = 2; forge.fuelPerSmelt = 1; forge.smeltSeconds = 5f;

            var a = forge.CurrentRecipe; // first access builds the cache
            var b = forge.CurrentRecipe; // stable dials → the SAME cached instance (no new alloc)
            Assert.AreSame(a, b,
                "CurrentRecipe must return the CACHED instance when the dials are unchanged (no per-frame GC.Alloc — 86camw8rm)");
            Assert.AreEqual(2, a.InputCount); Assert.AreEqual(1, a.FuelCost); Assert.AreEqual(5f, a.Seconds);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CurrentRecipe_RebuildsOnDialChange_StaysCorrect()
        {
            var go = new GameObject("Forge");
            var forge = go.AddComponent<Forge>();
            forge.orePerIngot = 2; forge.fuelPerSmelt = 1; forge.smeltSeconds = 5f;

            var before = forge.CurrentRecipe;
            forge.SetOrePerIngot(4); // a dial changes → the cache must rebuild
            var after = forge.CurrentRecipe;
            Assert.AreNotSame(before, after, "a dial change rebuilds the cached recipe (the loop + dials never disagree)");
            Assert.AreEqual(4, after.InputCount, "the rebuilt recipe reflects the new dial value");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CurrentRecipe_RepeatedAccess_DoesNotAllocate()
        {
            var go = new GameObject("Forge");
            var forge = go.AddComponent<Forge>();
            forge.orePerIngot = 2; forge.fuelPerSmelt = 1; forge.smeltSeconds = 5f;
            var warm = forge.CurrentRecipe; // warm the cache + JIT the getter path
            System.GC.KeepAlive(warm);

            // The literal GC.Alloc assertion the ticket asks for: with stable dials, per-frame reads allocate 0.
            Assert.That(() =>
            {
                var r = forge.CurrentRecipe;
                System.GC.KeepAlive(r);
            }, UnityIs.Not.AllocatingGCMemory(), "CurrentRecipe must NOT allocate on a warm-cache read (the per-frame Update path)");
            Object.DestroyImmediate(go);
        }

        // === Forge place-to-build cost defaults (spec §5 — "forge >> weapons") ===

        [Test]
        public void ForgeCostDefaults_AreSixWood_TwelveStone_MoreStoneThanWood()
        {
            Assert.AreEqual(6, ForgePlacement.ForgeWoodCostDefault, "forge default wood cost = 6 (§5)");
            Assert.AreEqual(12, ForgePlacement.ForgeStoneCostDefault, "forge default STONE cost = 12 (§5 — a stone furnace)");
            Assert.Greater(ForgePlacement.ForgeStoneCostDefault, ForgePlacement.ForgeWoodCostDefault,
                "the forge costs MORE stone than wood (Sponsor-locked: 'forge >> weapons')");
            // And much more stone than the crafting table (5w+3s) — the iron tier is earned.
            Assert.Greater(ForgePlacement.ForgeStoneCostDefault, CraftingRecipeBook.TableStoneCost * 3,
                "the forge dwarfs the table's stone cost (a real furnace, not a bench)");
        }

        [Test]
        public void CanAfford_StillAllOrNothing_AcrossBothMats()
        {
            // The shipped I-3 pure gate is KEPT through the place-to-build rewrite.
            Assert.IsTrue(ForgePlacement.CanAfford(6, 12, 6, 12), "exactly enough of both affords");
            Assert.IsFalse(ForgePlacement.CanAfford(5, 12, 6, 12), "1 wood short → no build");
            Assert.IsFalse(ForgePlacement.CanAfford(6, 11, 6, 12), "1 stone short → no build");
        }

        // === #302 self-register WIRING (Build/Reveal enable the PlacementObstacle) ===

        [Test]
        public void ForgeBuild_RevealsVisual_AndEnablesTheObstacle()
        {
            var go = new GameObject("Forge");
            var visual = new GameObject("Vis"); visual.transform.SetParent(go.transform, false);
            visual.AddComponent<MeshFilter>(); var mr = visual.AddComponent<MeshRenderer>(); mr.enabled = false;
            var obstacle = go.AddComponent<PlacementObstacle>(); obstacle.enabled = false;
            var forge = go.AddComponent<Forge>();
            forge.visual = visual.transform;
            forge.placementObstacle = obstacle;

            Assert.IsFalse(mr.enabled, "precondition: the forge structure ships invisible");
            Assert.IsFalse(obstacle.enabled, "precondition: an unbuilt forge does NOT register a no-build zone");

            forge.Build();

            Assert.IsTrue(forge.IsBuilt, "Build latches built");
            Assert.IsTrue(mr.enabled, "Build reveals the forge structure (invisible-until-placed → placed)");
            Assert.IsTrue(obstacle.enabled,
                "Build enables the PlacementObstacle so the placed forge self-registers a no-build zone (#302 seam)");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ForgeBuildAtPose_MovesThenBuilds()
        {
            var go = new GameObject("Forge");
            var forge = go.AddComponent<Forge>();
            var pose = new Vector3(3f, 0f, -4f);
            forge.Build(pose, Quaternion.Euler(0f, 90f, 0f));
            Assert.IsTrue(forge.IsBuilt, "Build(pose) builds");
            Assert.AreEqual(pose, go.transform.position, "the forge moves to the confirmed ghost pose");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TableReveal_EnablesTheObstacle_ForCrossStructureBlocking()
        {
            var go = new GameObject("Table");
            var obstacle = go.AddComponent<PlacementObstacle>(); obstacle.enabled = false;
            var table = go.AddComponent<CraftingTable>();
            table.placementObstacle = obstacle;
            Assert.IsFalse(obstacle.enabled, "precondition: an unplaced table does not register");
            table.Reveal(Vector3.zero, Quaternion.identity);
            Assert.IsTrue(obstacle.enabled,
                "Reveal enables the PlacementObstacle so a later forge/table placement reads red over the placed table (③)");
            Object.DestroyImmediate(go);
        }

        // === Iron in-hand picker APPEND contract (Bar 5) — append-only, 0-5 byte-identical ===

        [Test]
        public void HeldWeaponPicker_AppendsFourIron_ArraysConsistent_StoneTierUnchanged()
        {
            int n = HeldWeaponCycleDebug.WeaponNodeNames.Length;
            Assert.AreEqual(10, n, "the picker has 6 stone-era nodes + 4 appended iron weapons (③)");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponLabels.Length, "labels array length matches");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponMeshScale.Length, "scale seat array length matches");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponMeshLocalOffset.Length, "offset seat array length matches");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponMeshLocalEuler.Length, "euler seat array length matches");

            // 0-5 (the soaked stone tier + both pickaxes) are BYTE-IDENTICAL (append-only, no regression).
            Assert.AreEqual("wpn_axe_stone_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeFamilyIndex]);
            Assert.AreEqual("wpn_pickaxe_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.PickaxeIronFamilyIndex]);
            Assert.AreEqual(1f, HeldWeaponCycleDebug.WeaponMeshScale[HeldWeaponCycleDebug.AxeFamilyIndex], "axe seat locked (idx 0 == 1.0)");

            // The 4 appended iron weapons map to their iron FBX nodes.
            Assert.AreEqual("wpn_axe_iron_01",   HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeIronFamilyIndex]);
            Assert.AreEqual("wpn_knife_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.DaggerIronFamilyIndex]);
            Assert.AreEqual("wpn_sword_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SwordIronFamilyIndex]);
            Assert.AreEqual("wpn_spear_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SpearIronFamilyIndex]);

            // The iron variants share their stone counterpart's soaked held scale (same family grip).
            Assert.AreEqual(HeldWeaponCycleDebug.WeaponMeshScale[1], HeldWeaponCycleDebug.WeaponMeshScale[HeldWeaponCycleDebug.DaggerIronFamilyIndex],
                "iron dagger seats at the stone knife's soaked held scale (shared family grip)");
        }
    }
}
