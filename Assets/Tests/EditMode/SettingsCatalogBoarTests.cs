using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC6 guard for the wild-boar per-tier difficulty tweakables (ticket 86cah7ydt): PopulateBoar registers
    /// `Boar HP max` / `Boar gore damage` / `Boar charge speed` into the SettingsRegistry LIVE-bound to the
    /// scene BoarEnemy (+ its Health / BoarAI) — the SAME pattern + naming as PopulateCombat. Drives the real
    /// components (no scene/Update) to prove the bindings hit the actual params AND — the dead-knob guard —
    /// that a slider also writes the ACTIVE tier's per-tier map entry so ApplyDifficulty-on-gore reads back the
    /// dialed value (not the baked default). A null target adds no dead knob.
    /// </summary>
    public class SettingsCatalogBoarTests
    {
        private GameObject _go;
        private BoarEnemy _boar;
        private Health _health;
        private BoarAI _ai;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BoarSettings");
            _health = _go.AddComponent<Health>();
            _boar = _go.AddComponent<BoarEnemy>();
            _ai = _go.AddComponent<BoarAI>();
            _health.max = BoarEnemy.BoarMedMaxHp;
            _boar.goreDamage = BoarEnemy.BoarMedGoreDamage;
            _boar.medMaxHp = BoarEnemy.BoarMedMaxHp;
            _boar.medGoreDamage = BoarEnemy.BoarMedGoreDamage;
            _ai.chaseSpeed = 3.2f;
            // deathHandler unwired → ai.ActiveTier == Medium (the tier the sliders write the map for).
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void PopulateBoar_RegistersAllThreeRows_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBoar(reg, _boar);

            Assert.IsTrue(reg.Has(SettingsCatalog.BoarHpMaxId), "Boar HP max registered (AC6)");
            Assert.IsTrue(reg.Has(SettingsCatalog.BoarGoreDamageId), "Boar gore damage registered (AC6)");
            Assert.IsTrue(reg.Has(SettingsCatalog.BoarChargeSpeedId), "Boar charge speed registered (AC6)");
            Assert.IsTrue(reg.Get(SettingsCatalog.BoarHpMaxId).Available, "Boar HP max is a LIVE row");
            Assert.AreEqual("Boar HP max", reg.Get(SettingsCatalog.BoarHpMaxId).Label);
            Assert.AreEqual("Boar gore damage", reg.Get(SettingsCatalog.BoarGoreDamageId).Label);
            Assert.AreEqual("Boar charge speed", reg.Get(SettingsCatalog.BoarChargeSpeedId).Label);
        }

        [Test]
        public void BoarHpMax_DrivesHealthMax_AndBakesTheActiveTierMap()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBoar(reg, _boar);
            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.BoarHpMaxId);
            e.SetValue(70f);
            Assert.AreEqual(70f, _health.max, 1e-4f, "the HP-max slider drives Health.max live (AC6)");
            Assert.AreEqual(70f, _boar.medMaxHp, 1e-4f,
                "the slider also BAKES the active (Medium) tier's HP map — no dead knob under ApplyDifficulty");
        }

        [Test]
        public void BoarGoreDamage_DrivesGore_AndBakesTheActiveTierMap_NotClobberedByApplyDifficulty()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBoar(reg, _boar);
            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.BoarGoreDamageId);
            e.SetValue(30f);
            Assert.AreEqual(30f, _boar.goreDamage, 1e-4f, "the gore slider drives BoarEnemy.goreDamage live (AC6)");
            Assert.AreEqual(30f, _boar.medGoreDamage, 1e-4f, "the slider bakes the active (Medium) gore map");
            // The dead-knob guard: re-applying the tier (as BoarAI does on every gore) must NOT clobber the dial.
            _boar.ApplyDifficulty(SurvivalNeed.DifficultyTier.Medium);
            Assert.AreEqual(30f, _boar.goreDamage, 1e-4f,
                "ApplyDifficulty reads back the DIALED gore, not the baked default (no dead knob — AC6)");
        }

        [Test]
        public void BoarChargeSpeed_DrivesChaseSpeed_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBoar(reg, _boar);
            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.BoarChargeSpeedId);
            e.SetValue(6f);
            Assert.AreEqual(6f, _ai.chaseSpeed, 1e-4f, "the charge-speed slider drives BoarAI.chaseSpeed live (AC6)");
        }

        [Test]
        public void NullBoar_RegistersNothing()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBoar(reg, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.BoarHpMaxId), "null boar → no boar rows");
            Assert.AreEqual(0, reg.Count, "no dead knobs on a boar-less rig");
        }
    }
}
