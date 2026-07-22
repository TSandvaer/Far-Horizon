using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the ORE-RARITY difficulty dial (ticket 86cakkmr0 / I-2). Proves:
    ///   • the three iron difficulty presets (I-0 data) are MONOTONIC on the ore-rarity axis (easy common → hard
    ///     sparse) and inside the settings band — so the dial's presets are coherent;
    ///   • the authored node POOL size = the Easy preset's count, so the pool can honor the largest preset live;
    ///   • SettingsCatalog.PopulateIronLive FLIPS `iron_ore_rarity` LIVE (Available) bound to MineOre.ActiveNodeCount
    ///     (the I-0 extension hook goes live here — I-2), with exactly ONE row (no duplicate-id collision), reading
    ///     AND writing the live count; a null MineOre leaves the row greyed (no dead knob on a mine-less rig).
    /// </summary>
    public class IronOreRarityTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.IronOreRarityId);
        }

        [Test]
        public void DifficultyPresets_OreNodeCount_IsMonotonic_EasyCommon_HardSparse_WithinBand()
        {
            var easy = IronDifficultyPresets.Easy;
            var med = IronDifficultyPresets.Medium;
            var hard = IronDifficultyPresets.Hard;

            Assert.Greater(easy.OreNodeCount, med.OreNodeCount,
                "EASY has MORE ore nodes than MEDIUM (easy = common)");
            Assert.Greater(med.OreNodeCount, hard.OreNodeCount,
                "MEDIUM has more ore nodes than HARD (hard = sparse)");

            foreach (var c in new[] { easy.OreNodeCount, med.OreNodeCount, hard.OreNodeCount })
            {
                Assert.GreaterOrEqual(c, SettingsCatalog.IronOreRarityMin, "preset node count >= the dial floor");
                Assert.LessOrEqual(c, SettingsCatalog.IronOreRarityMax, "preset node count <= the dial ceiling");
            }
        }

        [Test]
        public void PopulateIronLive_FlipsRarityLive_BoundToActiveNodeCount_NoDuplicateRow()
        {
            LogAssert.ignoreFailingMessages = true; // MineOre bare-rig FindObjectOfType warnings are irrelevant here
            var mineGo = new GameObject("MineOre");
            try
            {
                var mine = mineGo.AddComponent<MineOre>();
                mine.activeNodeCount = 14; // explicit (Awake doesn't run in EditMode) so the row reads a known value

                var reg = new SettingsRegistry();
                SettingsCatalog.PopulateIron(reg);      // the greyed hook first (as SettingsPanel does)
                SettingsCatalog.PopulateIronLive(reg, mine); // then flip it live (I-2)

                Assert.IsTrue(reg.Has(SettingsCatalog.IronOreRarityId), "iron_ore_rarity row present");
                var rarity = reg.Get(SettingsCatalog.IronOreRarityId) as IntSettingEntry;
                Assert.IsNotNull(rarity, "iron_ore_rarity is an int row");
                Assert.IsTrue(rarity.Available, "I-2: iron_ore_rarity is now LIVE (flipped from the greyed hook)");

                int count = 0;
                foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.IronOreRarityId) count++;
                Assert.AreEqual(1, count, "exactly one iron_ore_rarity row (no duplicate-id collision)");

                // The row READS the live active count...
                Assert.AreEqual(14, rarity.Value, "iron_ore_rarity reads MineOre.ActiveNodeCount");
                // ...and WRITES it (drives the ore-rarity dial live).
                rarity.SetValue(8);
                Assert.AreEqual(8, mine.ActiveNodeCount, "iron_ore_rarity drives MineOre.ActiveNodeCount live (hard = sparse)");
            }
            finally
            {
                Object.DestroyImmediate(mineGo);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void PopulateIronLive_NullMineOre_LeavesRarityGreyed_NoDeadKnob()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateIron(reg);
            SettingsCatalog.PopulateIronLive(reg, null); // no MineOre → the row stays the greyed extension hook

            Assert.IsTrue(reg.Has(SettingsCatalog.IronOreRarityId), "iron_ore_rarity row still present");
            Assert.IsFalse(reg.Get(SettingsCatalog.IronOreRarityId).Available,
                "with no MineOre, iron_ore_rarity STAYS the greyed extension hook (no dead knob)");
        }
    }
}
