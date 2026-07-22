using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the Combat POC (ticket 86cah7xxp) — the DETERMINISTIC, headless-drivable half of
    /// the AC10 regression guard. Drives HP damage/death, weapon-attribute-driven damage, the damage-type ↔
    /// resistance matchup, bleed DoT over a TickSeconds window, and the needs-gated regen truth-table WITHOUT
    /// a scene/Update/wall-clock (headless Time.deltaTime≈0, unity-conventions.md §Headless). The PlayMode
    /// siblings (CombatPlayModeTests) prove the same seams fire through Update over a REAL Time.time window +
    /// the scene wiring. Together they catch the bug CLASS: damage that mis-computes, death that mis-fires,
    /// or a DoT/regen that silently never ticks in a build.
    /// </summary>
    public class CombatHealthTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown() { if (_go != null) Object.DestroyImmediate(_go); }

        private Health NewHealth(float max = 100f)
        {
            _go = new GameObject("HealthTest");
            var h = _go.AddComponent<Health>();
            h.max = max;
            h.startFull = true;
            h.resistance = ResistanceProfile.Neutral;
            h.damageTakenMul = 1f;
            // Force the lazy seed NOW (EditMode has no Start), BEFORE the test subscribes to Changed — so the
            // seed's one-shot Changed (the HUD-initial-paint fire) never miscounts as a damage/heal Changed.
            _ = h.Current;
            return h;
        }

        // === AC1 — HP damage + death (ApplyDamage reduces Current, 0 → IsDead, Changed fires) ===
        [Test]
        public void ApplyDamage_ReducesCurrent_FiresChanged()
        {
            var h = NewHealth(100f);
            float last = -1f; int count = 0;
            h.Changed += v => { last = v; count++; };

            float removed = h.ApplyDamage(30f, DamageType.Slash);
            Assert.AreEqual(30f, removed, 0.001f, "returns the HP actually removed");
            Assert.AreEqual(70f, h.Current, 0.001f, "ApplyDamage reduces Current by the effective amount");
            Assert.AreEqual(0.7f, last, 0.001f, "Changed reports Current01 on damage (HUD never polls)");
            Assert.Greater(count, 0, "damage must fire Changed");
        }

        [Test]
        public void ApplyDamage_ToZero_IsDead_FiresDiedOnce()
        {
            var h = NewHealth(50f);
            int died = 0;
            h.Died += () => died++;

            Assert.IsFalse(h.IsDead, "full HP is not dead");
            h.ApplyDamage(50f, DamageType.Slash);
            Assert.IsTrue(h.IsDead, "0 HP is death");
            Assert.AreEqual(1, died, "Died fires exactly once on the 0-crossing");

            // A hit on an already-dead target is a no-op and does NOT re-fire Died.
            float removed = h.ApplyDamage(10f, DamageType.Slash);
            Assert.AreEqual(0f, removed, 0.001f, "a dead target takes no more damage");
            Assert.AreEqual(1, died, "Died does not re-fire on a dead target");
        }

        [Test]
        public void ApplyDamage_NonPositive_IsNoOp()
        {
            var h = NewHealth(100f);
            int count = 0; h.Changed += _ => count++;
            Assert.AreEqual(0f, h.ApplyDamage(0f, DamageType.Slash), 0.001f);
            Assert.AreEqual(0f, h.ApplyDamage(-5f, DamageType.Slash), 0.001f);
            Assert.AreEqual(100f, h.Current, 0.001f, "non-positive damage never changes HP");
            Assert.AreEqual(0, count, "no Changed on a no-op");
        }

        // === AC8a — damage-type ↔ resistance (pierce on pierce-weak > neutral/resistant) ===
        [Test]
        public void Resistance_PierceOnPierceWeak_ExceedsNeutralAndResistant()
        {
            // A pierce-weak target (pierceMul 1.6, slash neutral 1.0). Same base amount, different type.
            var weak = NewHealth(1000f);
            weak.resistance = new ResistanceProfile { slashMul = 1f, pierceMul = 1.6f, bluntMul = 1f };

            float pierce = weak.ApplyDamage(10f, DamageType.Pierce); // weak → 16
            Object.DestroyImmediate(_go); _go = null;

            var neutral = NewHealth(1000f); // all-neutral
            float neutralHit = neutral.ApplyDamage(10f, DamageType.Slash); // 10
            Object.DestroyImmediate(_go); _go = null;

            var resistant = NewHealth(1000f);
            resistant.resistance = new ResistanceProfile { slashMul = 0.5f, pierceMul = 1f, bluntMul = 1f };
            float resistantHit = resistant.ApplyDamage(10f, DamageType.Slash); // resistant → 5

            Assert.Greater(pierce, neutralHit, "pierce on a pierce-WEAK target does MORE than a neutral hit (AC8a)");
            Assert.Greater(neutralHit, resistantHit, "a neutral hit does more than a resistant hit (AC8a)");
            Assert.AreEqual(16f, pierce, 0.001f, "pierce-weak (1.6x) of 10 = 16");
        }

        [Test]
        public void Resistance_UnauthoredProfile_TreatedAsNeutral_NotInvincible()
        {
            // A default(struct) reads all-zero — must NOT make the target invincible (the silent-zero trap).
            var h = NewHealth(100f);
            h.resistance = default; // all-zero
            float removed = h.ApplyDamage(20f, DamageType.Slash);
            Assert.AreEqual(20f, removed, 0.001f, "an unauthored (all-zero) profile takes NORMAL damage, not zero");
        }

        // === AC8b — per-tier difficulty (HP-max + damage-taken vary by tier) ===
        [Test]
        public void Difficulty_TierSetsMaxAndDamageTaken()
        {
            var h = NewHealth(100f);
            h.easyMax = 120f; h.medMax = 100f; h.hardMax = 80f;
            h.easyDamageTakenMul = 0.5f; h.medDamageTakenMul = 1f; h.hardDamageTakenMul = 1.5f;

            h.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
            Assert.AreEqual(120f, h.Max, 0.001f, "easy tier raises the HP max");
            Assert.AreEqual(0.5f, h.damageTakenMul, 0.001f, "easy tier softens incoming damage");
            // A full-at-max entity refills to the new max on a tier change.
            Assert.AreEqual(120f, h.Current, 0.001f, "a full entity stays full at the new max");

            float easyHit = h.ApplyDamage(10f, DamageType.Slash); // 10 * 0.5 = 5
            Assert.AreEqual(5f, easyHit, 0.001f, "easy damageTakenMul halves the hit");

            h.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
            Assert.AreEqual(80f, h.Max, 0.001f, "hard tier lowers the HP max");
            Assert.AreEqual(1.5f, h.damageTakenMul, 0.001f, "hard tier amplifies incoming damage");
        }

        // === AC6 — bleed DoT ticks over a TickSeconds window, then expires ===
        [Test]
        public void Bleed_TicksHpDownOverWindow_ThenExpires()
        {
            var h = NewHealth(100f);
            var sec = _go.AddComponent<StatusEffectController>();
            sec.health = h;

            sec.Apply(StatusEffectSpec.MakeBleed(damagePerSecond: 4f, durationSeconds: 3f));
            Assert.AreEqual(1, sec.ActiveCount, "a bleed is active after Apply");

            sec.TickSeconds(1f); // 4 dmg
            Assert.AreEqual(96f, h.Current, 0.001f, "bleed ticks damagePerSecond*seconds through ApplyDamage");
            Assert.AreEqual(1, sec.ActiveCount, "bleed still active mid-duration");

            sec.TickSeconds(5f); // 2s remaining -> 8 more dmg, then expires (not 20)
            Assert.AreEqual(88f, h.Current, 0.001f, "bleed deals only its REMAINING duration, not the full over-tick");
            Assert.AreEqual(0, sec.ActiveCount, "bleed expires when its duration is spent");

            sec.TickSeconds(10f);
            Assert.AreEqual(88f, h.Current, 0.001f, "an expired bleed deals nothing further");
        }

        [Test]
        public void Bleed_RoutesThroughResistance()
        {
            // A bleed is Slash-typed; a slash-resistant target takes a reduced bleed (proves it uses the seam).
            var h = NewHealth(100f);
            h.resistance = new ResistanceProfile { slashMul = 0.5f, pierceMul = 1f, bluntMul = 1f };
            var sec = _go.AddComponent<StatusEffectController>();
            sec.health = h;
            sec.Apply(StatusEffectSpec.MakeBleed(10f, 1f));
            sec.TickSeconds(1f); // 10 * 0.5 = 5
            Assert.AreEqual(95f, h.Current, 0.001f, "a bleed is modulated by the target's resistance (shared seam)");
        }

        [Test]
        public void StatusEffect_None_AppliesNothing()
        {
            var h = NewHealth(100f);
            var sec = _go.AddComponent<StatusEffectController>();
            sec.health = h;
            sec.Apply(StatusEffectSpec.None);
            Assert.AreEqual(0, sec.ActiveCount, "an inactive spec (None) adds no effect");
        }
    }
}
