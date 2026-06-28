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
    /// </summary>
    public class SettingsCatalogChopTests
    {
        private GameObject _charGo, _treeGo, _spawnerGo;
        private CastawayCharacter _character;
        private ChopTree _tree;
        private LogPileSpawner _spawner;

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
            _tree.chopsToFell = 3;

            // REWORK 86caf9u5t — the shared log-pile spawner the wood-yield + despawn settings bind to.
            _spawnerGo = new GameObject("LogPileSpawner");
            _spawner = _spawnerGo.AddComponent<LogPileSpawner>();
            _spawner.WoodYield = 10;
            _spawner.DespawnSeconds = 180f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_charGo);
            Object.DestroyImmediate(_treeGo);
            Object.DestroyImmediate(_spawnerGo);
            LogAssert.ignoreFailingMessages = false;
            // Clear any PlayerPrefs the SetValue path wrote, so runs don't leak across tests.
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ToolSpeedId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeRegrowthId + ".min");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeRegrowthId + ".max");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeWoodYieldId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ChopsToFellId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.LogPileDespawnId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.TreeFadeOutId);
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

        // ============================================================================================
        // REWORK 86caf9u5t — the 3 NEW chop settings: `tree-chop wood yield` + `chops-to-fell` (int steppers)
        // + `log-pile despawn` (float slider). Each is a no-dead-knob guard (reads AND writes the live field).
        // ============================================================================================

        [Test]
        public void PopulateChop_RegistersChopsToFell_Live_BoundToChopTree_NoDeadKnob()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree, null, _spawner);

            Assert.IsTrue(reg.Has(SettingsCatalog.ChopsToFellId), "chops-to-fell row present (AC4)");
            var chops = reg.Get(SettingsCatalog.ChopsToFellId) as IntSettingEntry;
            Assert.IsNotNull(chops, "chops-to-fell is an INT stepper row");
            Assert.IsTrue(chops.Available, "chops-to-fell is LIVE");
            Assert.AreEqual(ChopTree.ChopsToFellMin, chops.Min, "stepper floor == ChopTree.ChopsToFellMin (1)");
            Assert.AreEqual(ChopTree.ChopsToFellMax, chops.Max, "stepper ceiling == ChopTree.ChopsToFellMax (10)");

            // Reads the live value...
            Assert.AreEqual(3, chops.Value, "chops-to-fell reads ChopTree.chopsToFell");
            // ...and WRITES it (no dead knob): set to 6 → the tree's chopsToFell is 6.
            chops.SetValue(6);
            Assert.AreEqual(6, _tree.chopsToFell, "chops-to-fell drives ChopTree.chopsToFell live (no dead knob)");
            // Clamp: a value above the band clamps to the ceiling.
            chops.SetValue(99);
            Assert.AreEqual(ChopTree.ChopsToFellMax, _tree.chopsToFell, "chops-to-fell clamps to the [1,10] band");
        }

        [Test]
        public void PopulateChop_RegistersTreeWoodYield_Live_BoundToSpawner_NoDeadKnob()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree, null, _spawner);

            Assert.IsTrue(reg.Has(SettingsCatalog.TreeWoodYieldId), "tree-chop wood yield row present (AC3)");
            var yield = reg.Get(SettingsCatalog.TreeWoodYieldId) as IntSettingEntry;
            Assert.IsNotNull(yield, "tree-chop wood yield is an INT stepper row");
            Assert.IsTrue(yield.Available, "tree-chop wood yield is LIVE");
            Assert.AreEqual(LogPileSpawner.WoodYieldMin, yield.Min, "stepper floor == WoodYieldMin (1)");
            Assert.AreEqual(LogPileSpawner.WoodYieldMax, yield.Max, "stepper ceiling == WoodYieldMax (50)");

            // Reads the live value...
            Assert.AreEqual(10, yield.Value, "tree-chop wood yield reads LogPileSpawner.WoodYield (default 10)");
            // ...and WRITES it (no dead knob): the next felled tree's pile holds the dialed amount.
            yield.SetValue(25);
            Assert.AreEqual(25, _spawner.WoodYield, "tree-chop wood yield drives LogPileSpawner.WoodYield live (no dead knob)");
            // Clamp: above the band clamps to the ceiling.
            yield.SetValue(999);
            Assert.AreEqual(LogPileSpawner.WoodYieldMax, _spawner.WoodYield, "wood yield clamps to the [1,50] band");
        }

        [Test]
        public void PopulateChop_RegistersLogPileDespawn_Live_BoundToSpawner_NoDeadKnob()
        {
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree, null, _spawner);

            Assert.IsTrue(reg.Has(SettingsCatalog.LogPileDespawnId), "log-pile despawn row present (AC5)");
            var despawn = reg.Get(SettingsCatalog.LogPileDespawnId) as FloatSettingEntry;
            Assert.IsNotNull(despawn, "log-pile despawn is a FLOAT slider row");
            Assert.IsTrue(despawn.Available, "log-pile despawn is LIVE");

            // Reads the live value...
            Assert.AreEqual(180f, despawn.Value, 1e-3f, "log-pile despawn reads LogPileSpawner.DespawnSeconds (default 180s)");
            // ...and WRITES it (no dead knob).
            despawn.SetValue(60f);
            Assert.AreEqual(60f, _spawner.DespawnSeconds, 1e-3f, "log-pile despawn drives LogPileSpawner.DespawnSeconds live");
        }

        [Test]
        public void PopulateChop_NullSpawner_SkipsYieldAndDespawn_ButKeepsChopsToFell()
        {
            // No spawner → the yield/despawn rows are absent (bound to the spawner) but chops-to-fell stays (bound
            // to the tree). No dead knob on a spawner-less rig.
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree, null, null);

            Assert.IsTrue(reg.Has(SettingsCatalog.ChopsToFellId), "chops-to-fell present (binds to the tree, not the spawner)");
            Assert.IsFalse(reg.Has(SettingsCatalog.TreeWoodYieldId), "no spawner → no wood-yield row (no dead knob)");
            Assert.IsFalse(reg.Has(SettingsCatalog.LogPileDespawnId), "no spawner → no despawn row (no dead knob)");
        }

        [Test]
        public void PopulateChop_NullTree_SkipsChopsToFell()
        {
            // No tree → chops-to-fell absent (binds to the tree). The spawner-bound rows still register.
            var reg = SettingsCatalog.Build(null, null, null, _character, null, null, _spawner);

            Assert.IsFalse(reg.Has(SettingsCatalog.ChopsToFellId), "no tree → no chops-to-fell row (no dead knob)");
            Assert.IsTrue(reg.Has(SettingsCatalog.TreeWoodYieldId), "spawner present → wood-yield row registers");
            Assert.IsTrue(reg.Has(SettingsCatalog.LogPileDespawnId), "spawner present → despawn row registers");
        }

        // ============================================================================================
        // 86caff4ad — `fallen-tree fade-out` (the #165-soak NIT): default 2s + a LIVE float-slider row driving
        // ChopTree.fadeOutDelaySeconds. AC1 (default 2s) + AC2 (live, no dead knob) + AC3 (paired test).
        // ============================================================================================

        [Test]
        public void ChopTree_FadeOutDelay_DefaultsTo2Seconds()
        {
            // AC1 — the NAMED default is 2s (the Sponsor's #165 NIT: 10s felt too long). The constant and the
            // field initializer must agree, and a freshly-added ChopTree component ships the 2s default. (_tree
            // in SetUp never sets fadeOutDelaySeconds, so it carries the field default.)
            Assert.AreEqual(2f, ChopTree.FadeOutDelayDefault, 1e-4f, "the NAMED fade-out default is 2s (AC1)");
            Assert.AreEqual(ChopTree.FadeOutDelayDefault, _tree.fadeOutDelaySeconds, 1e-4f,
                "a fresh ChopTree ships the 2s fade-out default (the field initializer == the constant)");
        }

        [Test]
        public void PopulateChop_RegistersFallenTreeFadeOut_Live_BoundToChopTree_NoDeadKnob()
        {
            // AC2 — the row registers LIVE, bound to ChopTree.fadeOutDelaySeconds, and DRIVES it (no dead knob:
            // change the setting → the tree's fade delay changes → the next felled tree fades after the new delay).
            var reg = SettingsCatalog.Build(null, null, null, _character, _tree, null, _spawner);

            Assert.IsTrue(reg.Has(SettingsCatalog.TreeFadeOutId), "fallen-tree fade-out row present (AC2)");
            var fade = reg.Get(SettingsCatalog.TreeFadeOutId) as FloatSettingEntry;
            Assert.IsNotNull(fade, "fallen-tree fade-out is a FLOAT slider row");
            Assert.IsTrue(fade.Available, "fallen-tree fade-out is LIVE");
            Assert.AreEqual(ChopTree.FadeOutDelayMin, fade.Min, 1e-4f, "slider floor == ChopTree.FadeOutDelayMin (0)");
            Assert.AreEqual(ChopTree.FadeOutDelayMax, fade.Max, 1e-4f, "slider ceiling == ChopTree.FadeOutDelayMax (30)");

            // Reads the live value (the 2s default)...
            Assert.AreEqual(2f, fade.Value, 1e-3f, "fade-out row reads ChopTree.fadeOutDelaySeconds (default 2s)");
            // ...and WRITES it live (no dead knob): set to 5s → the tree fades after 5s, not the 2s default.
            fade.SetValue(5f);
            Assert.AreEqual(5f, _tree.fadeOutDelaySeconds, 1e-3f,
                "fallen-tree fade-out drives ChopTree.fadeOutDelaySeconds live (no dead knob)");
            // Clamp: a value above the band clamps to the ceiling; a negative clamps to the floor.
            fade.SetValue(999f);
            Assert.AreEqual(ChopTree.FadeOutDelayMax, _tree.fadeOutDelaySeconds, 1e-3f,
                "fade-out clamps to the [0,30]s band ceiling");
            fade.SetValue(-5f);
            Assert.AreEqual(ChopTree.FadeOutDelayMin, _tree.fadeOutDelaySeconds, 1e-3f,
                "fade-out clamps to the [0,30]s band floor");
        }

        [Test]
        public void PopulateChop_NullTree_SkipsFallenTreeFadeOut()
        {
            // No tree → the fade-out row is absent (binds to the tree, like chops-to-fell). No dead knob on a
            // tree-less rig. The spawner-bound rows still register.
            var reg = SettingsCatalog.Build(null, null, null, _character, null, null, _spawner);

            Assert.IsFalse(reg.Has(SettingsCatalog.TreeFadeOutId), "no tree → no fallen-tree fade-out row (no dead knob)");
        }
    }
}
