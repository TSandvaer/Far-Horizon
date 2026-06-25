using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC1+AC3 guard for the CHOP settings wiring (ticket 86caa4c5c, V1/V2). Proves SettingsCatalog.PopulateChop:
    ///   • FLIPS the reserved `tool_use_speed` row LIVE (V1) — `Populate` registers it greyed; with a chop
    ///     driver it becomes Available and bound to ChopPoseDriver.swingSpeed (NOT a second row — no
    ///     duplicate-id throw, which Register would raise; the remove-then-add seam is the only safe path);
    ///   • registers `tree_regrowth_time` as a LIVE RANGE row (V2) driving ChopTree.regrowthMin/Max;
    ///   • is a NO-OP with a null driver/tree (the row stays greyed / absent — chop-less rig / bare test
    ///     unaffected, so SettingsCatalogTests' "tool-use speed is a greyed hook" assertion still holds).
    /// Drives the real components (plain public fields) so this proves the bindings hit the actual params.
    /// </summary>
    public class SettingsCatalogChopTests
    {
        private GameObject _driverGo, _treeGo;
        private ChopPoseDriver _driver;
        private ChopTree _tree;

        [SetUp]
        public void SetUp()
        {
            _driverGo = new GameObject("Castaway");
            _driver = _driverGo.AddComponent<ChopPoseDriver>();
            _driver.swingSpeed = 1f;

            _treeGo = new GameObject("ChopTree");
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.regrowthMinSeconds = 480f;
            _tree.regrowthMaxSeconds = 720f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_driverGo);
            Object.DestroyImmediate(_treeGo);
            // Clear any PlayerPrefs the SetValue path wrote, so runs don't leak across tests.
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ToolSpeedId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeRegrowthId + ".min");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeRegrowthId + ".max");
        }

        [Test]
        public void PopulateChop_FlipsToolUseSpeed_Live_BoundToSwingSpeed_NoDuplicateRow()
        {
            // Full Build path (orbit/wasd null is fine — those settings are simply skipped) with the chop refs.
            var reg = SettingsCatalog.Build(null, null, null, _driver, _tree);

            Assert.IsTrue(reg.Has(SettingsCatalog.ToolSpeedId), "tool-use speed row present");
            var toolSpeed = reg.Get(SettingsCatalog.ToolSpeedId) as FloatSettingEntry;
            Assert.IsNotNull(toolSpeed, "tool-use speed is a float row");
            Assert.IsTrue(toolSpeed.Available, "V1: tool-use speed is now LIVE (flipped from the greyed hook)");

            // Exactly ONE row with this id (the greyed hook was REPLACED, not duplicated — Register would have
            // thrown on a duplicate id).
            int count = 0;
            foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.ToolSpeedId) count++;
            Assert.AreEqual(1, count, "exactly one tool_use_speed row (no duplicate-id collision)");

            // The row READS the live swing speed...
            _driver.swingSpeed = 2.0f;
            Assert.AreEqual(2.0f, toolSpeed.Value, 1e-4f, "tool-use speed reads ChopPoseDriver.swingSpeed");
            // ...and WRITES it (drives the game immediately — AC2 of the settings ticket).
            toolSpeed.SetValue(0.5f);
            Assert.AreEqual(0.5f, _driver.swingSpeed, 1e-4f, "tool-use speed drives ChopPoseDriver.swingSpeed live");
        }

        [Test]
        public void PopulateChop_RegistersTreeRegrowthRange_Live_BoundToRegrowMinMax()
        {
            var reg = SettingsCatalog.Build(null, null, null, _driver, _tree);

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
                "with no chop driver, tool-use speed STAYS the greyed extension hook");
            Assert.IsFalse(reg.Has(SettingsCatalog.TreeRegrowthId),
                "with no chop tree, the tree regrowth row is absent (no dead knob)");
        }

        [Test]
        public void PopulateChop_DirectCall_DoesNotThrow_OnTheGreyedHookReplace()
        {
            // Direct PopulateChop on a registry that already has the greyed ToolSpeedId (via Populate) must not
            // throw on the remove-then-add (the duplicate-id guard would throw without the Remove seam).
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);     // adds ToolSpeedId greyed
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateChop(reg, _driver, _tree),
                "flipping the greyed tool_use_speed live must not throw (remove-then-add, not double-register)");
            Assert.IsTrue(reg.Get(SettingsCatalog.ToolSpeedId).Available, "it is now live after the flip");
        }
    }
}
