using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC8b guard for the Combat POC per-tier difficulty tweakables (ticket 86cah7xxp): PopulateCombat
    /// registers `HP max` / `Damage taken` / `HP regen rate` / `Death behavior` into the SettingsRegistry
    /// LIVE-bound to the player Health + HealthRegen + DeathHandler — the SAME pattern + naming as the
    /// per-need decay rows (each feature adds its OWN Populate method). Drives the real components (no
    /// scene/Update) to prove the bindings hit the actual params + a null target adds no dead knob.
    /// </summary>
    public class SettingsCatalogCombatTests
    {
        private GameObject _go;
        private Health _health;
        private HealthRegen _regen;
        private DeathHandler _death;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CombatSettings");
            _health = _go.AddComponent<Health>();
            _health.max = 100f; _health.damageTakenMul = 1f;
            _regen = _go.AddComponent<HealthRegen>();
            _regen.health = _health; _regen.regenPerSecond = 2f;
            _death = _go.AddComponent<DeathHandler>();
            _death.health = _health; _death.tier = SurvivalNeed.DifficultyTier.Medium;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void PopulateCombat_RegistersAllFourRows_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCombat(reg, _health, _regen, _death);

            Assert.IsTrue(reg.Has(SettingsCatalog.HpMaxId), "HP max registered (AC8b)");
            Assert.IsTrue(reg.Has(SettingsCatalog.DamageTakenMulId), "Damage-taken registered (AC8b)");
            Assert.IsTrue(reg.Has(SettingsCatalog.HpRegenRateId), "HP regen rate registered (AC8b)");
            Assert.IsTrue(reg.Has(SettingsCatalog.DeathBehaviorId), "Death behavior registered (AC8b)");

            Assert.IsTrue(reg.Get(SettingsCatalog.HpMaxId).Available, "HP max is a LIVE row (panel merged)");
            Assert.AreEqual("HP max", reg.Get(SettingsCatalog.HpMaxId).Label);
            Assert.AreEqual("Damage taken", reg.Get(SettingsCatalog.DamageTakenMulId).Label);
            Assert.AreEqual("HP regen rate", reg.Get(SettingsCatalog.HpRegenRateId).Label);
            Assert.AreEqual("Death behavior", reg.Get(SettingsCatalog.DeathBehaviorId).Label);
        }

        [Test]
        public void HpMax_DrivesHealthMax_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCombat(reg, _health, _regen, _death);
            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.HpMaxId);
            e.SetValue(150f);
            Assert.AreEqual(150f, _health.max, 1e-4f, "the HP-max slider drives Health.max live");
        }

        [Test]
        public void DamageTaken_DrivesHealthMultiplier_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCombat(reg, _health, _regen, _death);
            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.DamageTakenMulId);
            e.SetValue(0.5f);
            Assert.AreEqual(0.5f, _health.damageTakenMul, 1e-4f, "the damage-taken slider drives Health.damageTakenMul live");
        }

        [Test]
        public void RegenRate_DrivesHealthRegen_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCombat(reg, _health, _regen, _death);
            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.HpRegenRateId);
            e.SetValue(6f);
            Assert.AreEqual(6f, _regen.regenPerSecond, 1e-4f, "the regen-rate slider drives HealthRegen.regenPerSecond live");
        }

        [Test]
        public void DeathBehavior_StepsThroughTiers_Live()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCombat(reg, _health, _regen, _death);
            var e = (IntSettingEntry)reg.Get(SettingsCatalog.DeathBehaviorId);

            e.SetValue(0);
            Assert.AreEqual(SurvivalNeed.DifficultyTier.Easy, _death.tier, "0 → Easy");
            e.SetValue(2);
            Assert.AreEqual(SurvivalNeed.DifficultyTier.Hard, _death.tier, "2 → Hard");
            e.SetValue(1);
            Assert.AreEqual(SurvivalNeed.DifficultyTier.Medium, _death.tier, "1 → Medium");
        }

        [Test]
        public void NullTargets_RegisterNothing()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCombat(reg, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.HpMaxId), "null health → no HP-max row");
            Assert.IsFalse(reg.Has(SettingsCatalog.HpRegenRateId), "null regen → no regen row");
            Assert.IsFalse(reg.Has(SettingsCatalog.DeathBehaviorId), "null death → no death-behavior row");
            Assert.AreEqual(0, reg.Count, "no dead knobs on a combat-less rig");
        }
    }
}
