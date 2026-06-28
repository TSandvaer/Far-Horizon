using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC1+AC3 guard for the CHOP settings wiring (ticket 86caa4c5c, V1/V2 / change-(b)). Proves
    /// SettingsCatalog.PopulateChop:
    ///   • FLIPS the reserved `tool_use_speed` row LIVE (V1) — `Populate` registers it greyed; with a chop
    ///     CASTAWAY it becomes Available and bound to CastawayCharacter.chopSpeed (the Mixamo melee Attack-state
    ///     playback rate — change-(b), replacing ChopPoseDriver.swingSpeed) (NOT a second row — no duplicate-id
    ///     throw, which Register would raise; the remove-then-add seam is the only safe path);
    ///   • registers `tree_regrowth_time` as a LIVE RANGE row (V2) driving ChopTree.regrowthMin/Max;
    ///   • is a NO-OP with a null character/tree (the row stays greyed / absent — chop-less rig / bare test
    ///     unaffected, so SettingsCatalogTests' "tool-use speed is a greyed hook" assertion still holds).
    /// Drives the real components (plain public fields) so this proves the bindings hit the actual params.
    ///
    /// ALSO guards the TREE-CHOP WOOD YIELD setting (ticket 86caf9u5t — the 86caa96rd AC4 follow-up):
    ///   • SettingsCatalog.PopulateWoodYield registers `tree_chop_wood_yield` as a LIVE int STEPPER row
    ///     bound to ChopTree.woodPerChop, defaulting to the chop's current value (DefaultChopYield);
    ///   • the row READS and WRITES the SAME woodPerChop field the chop reads per landed chop (the AC3
    ///     no-dead-knob EditMode binding guard — the PlayMode change-setting→chop-output test in
    ///     ChopTreePlayModeTests proves the full live drive end to end);
    ///   • a null tree registers NO wood-yield row (no dead knob with nothing behind it).
    /// </summary>
    public class SettingsCatalogChopTests
    {
        private GameObject _charGo, _treeGo;
        private CastawayCharacter _character;
        private ChopTree _tree;

        [SetUp]
        public void SetUp()
        {
            // CastawayCharacter.Awake logs a "modelPrefab not wired" error in a bare EditMode rig (no scene). Expect
            // it so the test isn't flagged for an unexpected log; we only exercise the chopSpeed field binding.
            LogAssert.ignoreFailingMessages = true;
            _charGo = new GameObject("Castaway");
            _character = _charGo.AddComponent<CastawayCharacter>();
            _character.chopSpeed = 1f;

            _treeGo = new GameObject("ChopTree");
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.regrowthMinSeconds = 480f;
            _tree.regrowthMaxSeconds = 720f;
            _tree.woodPerChop = ChopTree.DefaultChopYield; // the chop's current named-constant value (86caf9u5t)
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_charGo);
            Object.DestroyImmediate(_treeGo);
            LogAssert.ignoreFailingMessages = false;
            // Clear any PlayerPrefs the SetValue path wrote, so runs don't leak across tests.
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ToolSpeedId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeRegrowthId + ".min");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeRegrowthId + ".max");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.WoodYieldId);
        }

        [Test]
        public void PopulateChop_FlipsToolUseSpeed_Live_BoundToChopSpeed_NoDuplicateRow()
        {
            // Full Build path (orbit/wasd null is fine — those settings are simply skipped) with the chop refs.
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree);

            Assert.IsTrue(reg.Has(SettingsCatalog.ToolSpeedId), "tool-use speed row present");
            var toolSpeed = reg.Get(SettingsCatalog.ToolSpeedId) as FloatSettingEntry;
            Assert.IsNotNull(toolSpeed, "tool-use speed is a float row");
            Assert.IsTrue(toolSpeed.Available, "V1: tool-use speed is now LIVE (flipped from the greyed hook)");

            // Exactly ONE row with this id (the greyed hook was REPLACED, not duplicated — Register would have
            // thrown on a duplicate id).
            int count = 0;
            foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.ToolSpeedId) count++;
            Assert.AreEqual(1, count, "exactly one tool_use_speed row (no duplicate-id collision)");

            // The row READS the live chop speed...
            _character.chopSpeed = 2.0f;
            Assert.AreEqual(2.0f, toolSpeed.Value, 1e-4f, "tool-use speed reads CastawayCharacter.chopSpeed");
            // ...and WRITES it (drives the game immediately — AC2 of the settings ticket).
            toolSpeed.SetValue(0.5f);
            Assert.AreEqual(0.5f, _character.chopSpeed, 1e-4f, "tool-use speed drives CastawayCharacter.chopSpeed live");
        }

        [Test]
        public void PopulateChop_RegistersTreeRegrowthRange_Live_BoundToRegrowMinMax()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree);

            Assert.IsTrue(reg.Has(SettingsCatalog.TreeRegrowthId), "tree regrowth time row present (V2)");
            var regrow = reg.Get(SettingsCatalog.TreeRegrowthId) as RangeSettingEntry;
            Assert.IsNotNull(regrow, "tree regrowth time is a RANGE row (organic [min,max], AC3)");
            Assert.IsTrue(regrow.Available, "tree regrowth time is LIVE");

            // Reads the live min/max...
            Assert.AreEqual(480f, regrow.MinValue, 1e-3f, "regrow row reads ChopTree.regrowthMinSeconds");
            Assert.AreEqual(720f, regrow.MaxValue, 1e-3f, "regrow row reads ChopTree.regrowthMaxSeconds");
            // ...and writes them.
            regrow.SetMin(120f);
            regrow.SetMax(240f);
            Assert.AreEqual(120f, _tree.regrowthMinSeconds, 1e-3f, "regrow row drives ChopTree.regrowthMinSeconds");
            Assert.AreEqual(240f, _tree.regrowthMaxSeconds, 1e-3f, "regrow row drives ChopTree.regrowthMaxSeconds");
        }

        [Test]
        public void PopulateChop_NullTargets_LeavesToolSpeedGreyed_AndNoRegrowRow()
        {
            // No chop refs — the catalog must behave exactly as before chop (regression-safety for the existing
            // SettingsCatalogTests "tool-use speed is a greyed hook" assertion).
            var reg = SettingsCatalog.Build(null, null, null, null, null);

            Assert.IsTrue(reg.Has(SettingsCatalog.ToolSpeedId), "tool-use speed row still present");
            Assert.IsFalse(reg.Get(SettingsCatalog.ToolSpeedId).Available,
                "with no chop character, tool-use speed STAYS the greyed extension hook");
            Assert.IsFalse(reg.Has(SettingsCatalog.TreeRegrowthId),
                "with no chop tree, the tree regrowth row is absent (no dead knob)");
            Assert.IsFalse(reg.Has(SettingsCatalog.WoodYieldId),
                "with no chop tree, the tree-chop wood-yield row is absent (no dead knob — 86caf9u5t)");
        }

        // === 86caf9u5t AC1/AC3 — the tree-chop wood-yield row registers LIVE, defaults to the chop's current
        //     value, and READS/WRITES the SAME ChopTree.woodPerChop field the chop reads (the no-dead-knob
        //     EditMode binding guard; the full live change-setting→chop-output drive is the PlayMode test). ===
        [Test]
        public void PopulateWoodYield_RegistersStepper_Live_BoundToWoodPerChop()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree);

            Assert.IsTrue(reg.Has(SettingsCatalog.WoodYieldId), "tree-chop wood yield row present (86caf9u5t AC1)");
            var yield = reg.Get(SettingsCatalog.WoodYieldId) as IntSettingEntry;
            Assert.IsNotNull(yield, "tree-chop wood yield is an int STEPPER row");
            Assert.IsTrue(yield.Available, "tree-chop wood yield is a LIVE setting (not a greyed hook)");

            // Default = the chop's current named-constant value (DefaultChopYield seeds woodPerChop).
            Assert.AreEqual(ChopTree.DefaultChopYield, yield.Value,
                "the wood-yield row reads the chop's current woodPerChop (default = DefaultChopYield, AC2)");

            // It READS the live field...
            _tree.woodPerChop = 4;
            Assert.AreEqual(4, yield.Value, "the wood-yield row reads ChopTree.woodPerChop live");
            // ...and WRITES it (drives the game immediately — the chop's ApplyChopEffect reads this same field).
            yield.SetValue(7);
            Assert.AreEqual(7, _tree.woodPerChop, "the wood-yield row drives ChopTree.woodPerChop live (no dead knob)");
        }

        // === 86caf9u5t — exactly ONE wood-yield row, and it is a DISTINCT id from the tree-regrowth row (the two
        //     chop-tree settings are independent — no collision, registered via the SEPARATE PopulateWoodYield). ===
        [Test]
        public void PopulateWoodYield_IsASingleDistinctRow_NotTheRegrowthRow()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree);

            int count = 0;
            foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.WoodYieldId) count++;
            Assert.AreEqual(1, count, "exactly one tree_chop_wood_yield row (no duplicate)");

            Assert.AreNotEqual(SettingsCatalog.WoodYieldId, SettingsCatalog.TreeRegrowthId,
                "wood-yield is a DISTINCT setting id from tree-regrowth (two independent chop-tree rows)");
            Assert.AreEqual(SettingEntry.Archetype.Stepper,
                reg.Get(SettingsCatalog.WoodYieldId).Kind, "wood yield is a STEPPER (an int count)");
            Assert.AreEqual(SettingEntry.Archetype.Range,
                reg.Get(SettingsCatalog.TreeRegrowthId).Kind, "tree regrowth stays a RANGE (unchanged)");
        }

        // === 86caf9u5t — the wood-yield stepper clamps to its band, so a dial can't make a zero (dead) chop ===
        [Test]
        public void PopulateWoodYield_ClampsToBand_NeverZero()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree);
            var yield = (IntSettingEntry)reg.Get(SettingsCatalog.WoodYieldId);

            yield.SetValue(0);  // below the min
            Assert.AreEqual(SettingsCatalog.WoodYieldMin, _tree.woodPerChop,
                "wood yield clamps to WoodYieldMin (>=1) — a chop always yields some wood (no dead chop)");
            yield.SetValue(9999); // above the max
            Assert.AreEqual(SettingsCatalog.WoodYieldMax, _tree.woodPerChop, "wood yield clamps to WoodYieldMax");
        }

        [Test]
        public void PopulateChop_DirectCall_DoesNotThrow_OnTheGreyedHookReplace()
        {
            // Direct PopulateChop on a registry that already has the greyed ToolSpeedId (via Populate) must not
            // throw on the remove-then-add (the duplicate-id guard would throw without the Remove seam).
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);     // adds ToolSpeedId greyed
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateChop(reg, _character, _tree),
                "flipping the greyed tool_use_speed live must not throw (remove-then-add, not double-register)");
            Assert.IsTrue(reg.Get(SettingsCatalog.ToolSpeedId).Available, "it is now live after the flip");
        }
    }
}
