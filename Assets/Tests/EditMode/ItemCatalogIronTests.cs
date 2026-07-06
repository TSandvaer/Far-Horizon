using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// I-0 foundation guard (ticket 86cakkmgw) — the shared iron VOCABULARY exists in code so the mining /
    /// forge / craft tickets build against merged-on-main names. Proves BuildDefaults mints the §1 ids with
    /// the expected ItemKind + belt-eligibility, and that the pickaxe ids resolve in BOTH catalogs (ItemCatalog
    /// + WeaponCatalog) as §1 prescribes. Pure-logic (BuildDefaults in code, no .asset round-trip).
    ///
    /// Guards BITE: a wrong kind (ore made belt-eligible, or a pickaxe made a Resource), a missing id, or a
    /// catalog that forgot to register a pickaxe turns these red — the vocabulary-divergence class.
    /// </summary>
    public class ItemCatalogIronTests
    {
        private ItemCatalog _items;
        private WeaponCatalog _weapons;

        [SetUp]
        public void SetUp()
        {
            _items = ScriptableObject.CreateInstance<ItemCatalog>();
            _items.BuildDefaults();
            _weapons = ScriptableObject.CreateInstance<WeaponCatalog>();
            _weapons.BuildDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var d in _items.All) if (d != null) Object.DestroyImmediate(d);
            foreach (var w in _weapons.All) if (w != null) Object.DestroyImmediate(w);
            Object.DestroyImmediate(_items);
            Object.DestroyImmediate(_weapons);
        }

        [Test]
        public void IronOreAndIngot_AreResources_InventoryOnly_NotBeltEligible()
        {
            var ore = _items.ById(ItemCatalog.IronOreId);
            var ingot = _items.ById(ItemCatalog.IronIngotId);

            Assert.IsNotNull(ore, "iron_ore def is registered (§1)");
            Assert.IsNotNull(ingot, "iron_ingot def is registered (§1)");
            Assert.AreEqual(ItemKind.Resource, ore.Kind, "iron_ore is a RAW MATERIAL (Resource, like wood/stone)");
            Assert.AreEqual(ItemKind.Resource, ingot.Kind, "iron_ingot is a craft-input Resource");
            // Resources are inventory-only — the belt-eligibility guard must NOT let raw ore/ingot onto the belt.
            Assert.IsFalse(ItemDef.IsBeltEligible(ore), "iron_ore stays inventory-only (Resource, not belt)");
            Assert.IsFalse(ItemDef.IsBeltEligible(ingot), "iron_ingot stays inventory-only (Resource, not belt)");
            Assert.IsTrue(ItemDef.IsStackable(ore), "iron_ore stacks (Resource)");
        }

        [Test]
        public void Pickaxes_AreTools_BeltEligible_NonStacking_InItemCatalog()
        {
            var stone = _items.ById(ItemCatalog.PickaxeStoneId);
            var iron = _items.ById(ItemCatalog.PickaxeIronId);

            Assert.IsNotNull(stone, "pickaxe_stone def is registered (§1)");
            Assert.IsNotNull(iron, "pickaxe_iron def is registered (§1)");
            Assert.AreEqual(ItemKind.Tool, stone.Kind, "pickaxe_stone is the 5th TOOL type (belt-eligible, like the axe)");
            Assert.AreEqual(ItemKind.Tool, iron.Kind, "pickaxe_iron is a Tool");
            Assert.IsTrue(ItemDef.IsBeltEligible(stone), "a pickaxe is belt-eligible (held + selectable)");
            Assert.IsTrue(ItemDef.IsBeltEligible(iron), "a pickaxe is belt-eligible");
            Assert.IsFalse(ItemDef.IsStackable(stone), "a Tool does not stack (cap 1)");
            Assert.AreEqual(1, stone.MaxStack, "a Tool's per-slot cap is 1");
        }

        [Test]
        public void Pickaxes_ResolveInWeaponCatalog_WithMatchingIds()
        {
            var stone = _weapons.ById(WeaponCatalog.PickaxeStoneId);
            var iron = _weapons.ById(WeaponCatalog.PickaxeIronId);

            Assert.IsNotNull(stone, "pickaxe_stone resolves in the WeaponCatalog (held-tool + mine-verb seam, §1)");
            Assert.IsNotNull(iron, "pickaxe_iron resolves in the WeaponCatalog");
            // The item + weapon lanes share ONE id (the axe/spear precedent) — a parallel id breaks equip.
            Assert.AreEqual(ItemCatalog.PickaxeStoneId, WeaponCatalog.PickaxeStoneId, "item + weapon ids match for the stone pickaxe");
            Assert.AreEqual(ItemCatalog.PickaxeIronId, WeaponCatalog.PickaxeIronId, "item + weapon ids match for the iron pickaxe");
            Assert.AreEqual("pickaxe_mine", stone.AnimationId, "the pickaxe swing ref is the mine swing");
            // Iron is the upgrade — marginally stronger stats (a monotonic tier read for I-4's contrast).
            Assert.Greater(iron.Damage, stone.Damage, "iron pickaxe is the upgrade over stone");
        }

        [Test]
        public void CanonicalIds_MatchTheVocabularyContract()
        {
            // The §1 table strings are LOAD-BEARING — downstream tickets import these verbatim. Pin them.
            Assert.AreEqual("iron_ore", ItemCatalog.IronOreId);
            Assert.AreEqual("iron_ingot", ItemCatalog.IronIngotId);
            Assert.AreEqual("pickaxe_stone", ItemCatalog.PickaxeStoneId);
            Assert.AreEqual("pickaxe_iron", ItemCatalog.PickaxeIronId);
        }
    }
}
