using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guard for the INVENTORY tweakables in the settings binding map (ticket 86cabfa4e — the #90 / 86caa4bya
    /// AC1/AC2/AC7 settings-registration follow-up, deferred until the SettingsRegistry foundation #83 landed).
    /// The SettingsCatalog 11-arg Build / PopulateInventory registers THREE INT-stepper rows LIVE-bound through the
    /// real <see cref="Inventory"/> façade: `inventory slots` (AC1) + `belt slots` (AC2) drive Inventory's authoring
    /// counts (a model REBUILD — they are construction-time, not live fields), and `inventory stack size` (AC7)
    /// drives <see cref="ItemDef.ResourceStackSize"/> (the shared resource/consumable per-slot cap; tools stay 1).
    ///
    /// Drives the real component so this proves the bindings hit the actual params, the labels are the AC-mandated
    /// names, the bands clamp, slot-count changes RESIZE the model, stack-size changes affect the NEXT add/merge,
    /// the prior 10-arg overload stays inventory-free (backward compatibility), and a null inventory adds no dead
    /// knob. Mirrors SettingsCatalogStoneTests / SettingsCatalogHungerTests (the Populate* de-collision precedent).
    /// </summary>
    public class SettingsCatalogInventoryTests
    {
        private GameObject _invGo;
        private Inventory _inv;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();
            // Touch the model so EnsureModel built it at the default counts (20 / 5).
            _ = _inv.Model;
            // Re-seed the shared stack-size static to the canonical default (the SubsystemRegistration reset only
            // fires on a real play-entry, not in this EditMode run — so set it explicitly for a clean baseline).
            ItemDef.ResourceStackSize = ItemDef.DefaultResourceStack;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_invGo);
            ItemDef.ResourceStackSize = ItemDef.DefaultResourceStack; // never leak a dialed cap into a sibling test
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.InventorySlotsId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.BeltSlotsId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.StackSizeId);
        }

        // The full 11-arg Build with only the inventory target wired (everything else null — the catalog null-skips).
        private SettingsRegistry BuildWithInventory()
            => SettingsCatalog.Build(null, null, null, null, null, null, null, null, null, null, _inv);

        [Test]
        public void InventoryBuild_RegistersThreeStepperRows_WithAcMandatedNames()
        {
            var reg = BuildWithInventory();

            Assert.IsTrue(reg.Has(SettingsCatalog.InventorySlotsId), "inventory slots registered (AC1)");
            Assert.IsTrue(reg.Has(SettingsCatalog.BeltSlotsId), "belt slots registered (AC2)");
            Assert.IsTrue(reg.Has(SettingsCatalog.StackSizeId), "inventory stack size registered (AC7)");

            // All three are LIVE INT steppers (the settings panel #83 is merged).
            Assert.IsInstanceOf<IntSettingEntry>(reg.Get(SettingsCatalog.InventorySlotsId), "inventory slots is an INT stepper");
            Assert.IsInstanceOf<IntSettingEntry>(reg.Get(SettingsCatalog.BeltSlotsId), "belt slots is an INT stepper");
            Assert.IsInstanceOf<IntSettingEntry>(reg.Get(SettingsCatalog.StackSizeId), "stack size is an INT stepper");
            Assert.IsTrue(reg.Get(SettingsCatalog.InventorySlotsId).Available, "inventory slots is LIVE");
            Assert.IsTrue(reg.Get(SettingsCatalog.BeltSlotsId).Available, "belt slots is LIVE");
            Assert.IsTrue(reg.Get(SettingsCatalog.StackSizeId).Available, "inventory stack size is LIVE");

            // AC-mandated names (the #90 AC1/AC2/AC7 vocabulary).
            Assert.AreEqual("Inventory slots", reg.Get(SettingsCatalog.InventorySlotsId).Label);
            Assert.AreEqual("Belt slots", reg.Get(SettingsCatalog.BeltSlotsId).Label);
            Assert.AreEqual("Inventory stack size", reg.Get(SettingsCatalog.StackSizeId).Label);
        }

        [Test]
        public void InventorySlots_ReadsDefault_AndRebuildsModelOnChange()
        {
            var reg = BuildWithInventory();
            var slots = (IntSettingEntry)reg.Get(SettingsCatalog.InventorySlotsId);

            // Reads the default authoring count (20) + the model is sized to it.
            Assert.AreEqual(InventoryModel.DefaultInventorySlots, slots.Value, "inventory slots reads the 20 default");
            Assert.AreEqual(InventoryModel.DefaultInventorySlots, _inv.Model.InventorySlots.Count,
                "the model grid is sized to the default count");

            // Dialing it RESIZES the model (the grid array is readonly — a dev re-size rebuilds it).
            slots.SetValue(30);
            Assert.AreEqual(30, _inv.InventorySlotCount, "the slider drove the authoring count");
            Assert.AreEqual(30, _inv.Model.InventorySlots.Count, "the model grid was rebuilt to 30 slots (not a dead knob)");
        }

        [Test]
        public void BeltSlots_ReadsDefault_AndRebuildsModelOnChange()
        {
            var reg = BuildWithInventory();
            var belt = (IntSettingEntry)reg.Get(SettingsCatalog.BeltSlotsId);

            Assert.AreEqual(InventoryModel.DefaultBeltSlots, belt.Value, "belt slots reads the 5 default");
            Assert.AreEqual(InventoryModel.DefaultBeltSlots, _inv.Model.BeltSlots.Count, "the model belt is sized to the default");

            belt.SetValue(8);
            Assert.AreEqual(8, _inv.BeltSlotCount, "the slider drove the belt authoring count");
            Assert.AreEqual(8, _inv.Model.BeltSlots.Count, "the model belt was rebuilt to 8 slots (not a dead knob)");
        }

        [Test]
        public void StackSize_DrivesResourceStackCap_OnNextAdd()
        {
            var reg = BuildWithInventory();
            var stack = (IntSettingEntry)reg.Get(SettingsCatalog.StackSizeId);

            Assert.AreEqual(ItemDef.DefaultResourceStack, stack.Value, "stack size reads the 20 default");

            // Dial the cap down to 5; a resource def's MaxStack now reads the new shared cap (the NEXT add/merge).
            stack.SetValue(5);
            Assert.AreEqual(5, ItemDef.ResourceStackSize, "the slider drove the shared resource stack cap");

            var wood = _inv.Catalog.ById(ItemCatalog.WoodId);
            Assert.IsNotNull(wood, "the catalog has a wood def");
            Assert.AreEqual(5, wood.MaxStack, "a Resource's MaxStack now reflects the dialed cap (AC7)");

            // Tools are UNAFFECTED — the axe still caps at 1 (derived from Kind, not the shared cap).
            var axe = _inv.Catalog.ById(ItemCatalog.AxeId);
            Assert.AreEqual(1, axe.MaxStack, "a Tool's MaxStack stays 1 regardless of the resource stack cap");
        }

        [Test]
        public void InventorySettings_ClampToTheirBands()
        {
            var reg = BuildWithInventory();
            var slots = (IntSettingEntry)reg.Get(SettingsCatalog.InventorySlotsId);
            var belt = (IntSettingEntry)reg.Get(SettingsCatalog.BeltSlotsId);
            var stack = (IntSettingEntry)reg.Get(SettingsCatalog.StackSizeId);

            slots.SetValue(9999);
            Assert.AreEqual(SettingsCatalog.InventorySlotsMax, _inv.InventorySlotCount, "inventory slots clamps to the ceiling");
            slots.SetValue(-9999);
            Assert.AreEqual(SettingsCatalog.InventorySlotsMin, _inv.InventorySlotCount, "inventory slots clamps to the floor");

            belt.SetValue(9999);
            Assert.AreEqual(SettingsCatalog.BeltSlotsMax, _inv.BeltSlotCount, "belt slots clamps to the ceiling");
            belt.SetValue(-9999);
            Assert.AreEqual(SettingsCatalog.BeltSlotsMin, _inv.BeltSlotCount, "belt slots clamps to the floor");

            stack.SetValue(9999);
            Assert.AreEqual(SettingsCatalog.StackSizeMax, ItemDef.ResourceStackSize, "stack size clamps to the ceiling");
            stack.SetValue(-9999);
            Assert.AreEqual(SettingsCatalog.StackSizeMin, ItemDef.ResourceStackSize, "stack size clamps to the floor");
        }

        [Test]
        public void TenArgBuild_StaysInventoryFree_BackwardCompatible()
        {
            // The 10-arg Build (the prior full overload, before inventory) must NOT add the inventory rows —
            // backward compatibility. Only the 11-arg overload (with an inventory target) registers them.
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.InventorySlotsId), "the 10-arg Build does not register inventory slots");
            Assert.IsFalse(reg.Has(SettingsCatalog.BeltSlotsId), "the 10-arg Build does not register belt slots");
            Assert.IsFalse(reg.Has(SettingsCatalog.StackSizeId), "the 10-arg Build does not register stack size");
        }

        [Test]
        public void NullInventory_RegistersNoInventoryRows()
        {
            // A bare rig / an inventory-less scene passes null inventory → no inventory rows (no dead knob, no null-ref).
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.InventorySlotsId), "null inventory → no inventory-slots row");
            Assert.IsFalse(reg.Has(SettingsCatalog.BeltSlotsId), "null inventory → no belt-slots row");
            Assert.IsFalse(reg.Has(SettingsCatalog.StackSizeId), "null inventory → no stack-size row");
        }
    }
}
