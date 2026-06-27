using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC3/AC3a guard for the STONE settings wiring (ticket 86caa4c96). Proves SettingsCatalog.PopulateStones:
    ///   • registers `stone_respawn_time` as a LIVE RANGE row driving StoneRespawner.RespawnMin/Max (the shared
    ///     source — one slider retunes EVERY stone's respawn window, AC3a);
    ///   • is a NO-OP with a null respawner (the row is absent — a stone-less rig / bare test unaffected, so it
    ///     never null-refs and never adds a dead knob — the PopulateThirst/PopulateChop de-collision precedent);
    ///   • is registered via the SEPARATE method (NOT by appending to Populate — V3).
    /// Drives the real component (plain public fields) so this proves the bindings hit the actual params.
    /// </summary>
    public class SettingsCatalogStoneTests
    {
        private GameObject _respGo;
        private StoneRespawner _resp;

        [SetUp]
        public void SetUp()
        {
            _respGo = new GameObject("StoneRespawner");
            _resp = _respGo.AddComponent<StoneRespawner>();
            _resp.RespawnMinSeconds = 480f;
            _resp.RespawnMaxSeconds = 720f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_respGo);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.StoneRespawnId + ".min");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.StoneRespawnId + ".max");
        }

        [Test]
        public void PopulateStones_RegistersStoneRespawnRange_Live_BoundToRespawnMinMax()
        {
            // Full Build path (orbit/wasd/thirst/chop null is fine — those settings are simply skipped) + stone.
            var reg = SettingsCatalog.Build(null, null, null, null, null, _resp);

            Assert.IsTrue(reg.Has(SettingsCatalog.StoneRespawnId), "stone respawn time row present (AC3)");
            var respawn = reg.Get(SettingsCatalog.StoneRespawnId) as RangeSettingEntry;
            Assert.IsNotNull(respawn, "stone respawn time is a RANGE row (organic [min,max], AC3)");
            Assert.IsTrue(respawn.Available, "stone respawn time is LIVE (the settings panel is merged — V3)");

            // Reads the live min/max...
            Assert.AreEqual(480f, respawn.MinValue, 1e-3f, "respawn row reads StoneRespawner.RespawnMinSeconds");
            Assert.AreEqual(720f, respawn.MaxValue, 1e-3f, "respawn row reads StoneRespawner.RespawnMaxSeconds");
            // ...and writes them (drives every stone's window live — AC3a).
            respawn.SetMin(60f);
            respawn.SetMax(120f);
            Assert.AreEqual(60f, _resp.RespawnMinSeconds, 1e-3f, "respawn row drives StoneRespawner.RespawnMinSeconds");
            Assert.AreEqual(120f, _resp.RespawnMaxSeconds, 1e-3f, "respawn row drives StoneRespawner.RespawnMaxSeconds");
        }

        [Test]
        public void PopulateStones_NullRespawner_NoStoneRow()
        {
            // No respawner — the catalog must behave exactly as before stones (no dead knob, no null-ref).
            var reg = SettingsCatalog.Build(null, null, null, null, null, null);

            Assert.IsFalse(reg.Has(SettingsCatalog.StoneRespawnId),
                "with no StoneRespawner, the stone respawn row is absent (no dead knob)");
        }

        [Test]
        public void PopulateStones_DirectCall_AddsExactlyOneRow_NotViaPopulate()
        {
            // The de-collision precedent (V3): the row is registered by PopulateStones, NOT by Populate. A bare
            // Populate must NOT add the stone row; a subsequent PopulateStones adds exactly one.
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.StoneRespawnId),
                "Populate alone must NOT add the stone respawn row (it lives in PopulateStones — V3)");

            SettingsCatalog.PopulateStones(reg, _resp);
            int count = 0;
            foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.StoneRespawnId) count++;
            Assert.AreEqual(1, count, "PopulateStones adds exactly one stone respawn row (no duplicate)");
        }
    }
}
