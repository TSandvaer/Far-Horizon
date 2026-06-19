using System;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the HUNGER need model (ticket 86caamkp8).
    ///
    /// Drives decay DETERMINISTICALLY via TickSeconds (no scene/Update/wall-clock) — the math
    /// (decay-over-a-window, floor, clamp, AddFood restore, the ATOMIC eat-a-berry seam, Changed
    /// event, difficulty tiers) is proven fast in headless CI. The COMPLEMENTARY PlayMode test
    /// (HungerNeedPlayModeTests) proves the same decay actually fires through Update over a REAL
    /// Time.time window (the Time.deltaTime~=0 trap, unity-conventions.md). Together they catch the
    /// bug CLASS: a decay that integrates wrong, or one that silently never ticks in a build.
    ///
    /// Mirrors WarmthNeedTests 1:1 (the HUD-contract surface is byte-identical) + adds the eat-seam
    /// atomicity (AC2a, owned here) and the surface-parity / slower-than-warmth guards Tess flagged.
    /// </summary>
    public class HungerNeedTests
    {
        private GameObject _go;
        private HungerNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HungerTest");
            _need = _go.AddComponent<HungerNeed>();
            // Deterministic config independent of inspector tuning drift.
            _need.max = 100f;
            _need.decayPerSecond = 1f; // 1 hunger/sec -> trivially checkable
            _need.floor01 = 0.1f;      // floor at 10
            _need.criticalThreshold01 = 0.25f;
            _need.berryRestoreAmount = 18f;
            _need.startFull = true;
            // Seed _current = max WITHOUT running Start() (EditMode has no lifecycle); do it explicitly.
            _need.AddFood(_need.max); // _current starts 0; add max -> 100, also exercises clamp
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ===== Decay / floor / clamp (mirror WarmthNeedTests) =====

        [Test]
        public void Decay_OverAWindow_ReducesHunger_AtTheConfiguredRate()
        {
            Assert.AreEqual(100f, _need.Current, 0.001f, "starts full");
            _need.TickSeconds(10f); // 10s * 1/sec = 10 lost
            Assert.AreEqual(90f, _need.Current, 0.001f,
                "hunger must decay decayPerSecond*seconds over a window, not per-frame");
            Assert.AreEqual(0.9f, _need.Current01, 0.001f, "Current01 tracks the raw value normalized");
        }

        [Test]
        public void Decay_StopsAtFloor_NeverBelow()
        {
            _need.TickSeconds(1000f); // way past empty
            Assert.AreEqual(10f, _need.Current, 0.001f,
                "decay must rest at floor01*max (10), a simple floor — NOT a fail/starvation state");
            _need.TickSeconds(1000f);
            Assert.AreEqual(10f, _need.Current, 0.001f, "floor holds across repeated ticks");
        }

        // ===== Satisfaction: AddFood / the eat seam =====

        [Test]
        public void AddFood_RestoresAndClampsToMax()
        {
            _need.TickSeconds(60f); // 100 -> 40
            Assert.AreEqual(40f, _need.Current, 0.001f);

            _need.AddFood(30f); // explicit amount
            Assert.AreEqual(70f, _need.Current, 0.001f, "AddFood raises hunger");

            _need.AddFood(9999f); // over-satisfy
            Assert.AreEqual(100f, _need.Current, 0.001f, "AddFood clamps to max");
        }

        [Test]
        public void AddFood_NoArg_RestoresExactlyTheBerryAmount()
        {
            _need.TickSeconds(60f); // 100 -> 40
            _need.AddFood();        // default berry restore
            Assert.AreEqual(40f + _need.berryRestoreAmount, _need.Current, 0.001f,
                "the no-arg AddFood restores exactly berryRestoreAmount (the per-berry top-up)");
        }

        // ===== THE ATOMIC EAT-A-BERRY SEAM (AC2a — owned here) =====

        [Test]
        public void TryEatBerry_WithABerry_ConsumesAndRestores_InOneAction()
        {
            // A minimal all-or-nothing berry store standing in for the inventory consume side
            // (86caa5zz3 / 86caa4bya pass () => inventory.Model.TryConsumeSelected() here). The seam
            // contract is the same: consume returns true iff a berry was actually debited.
            int berries = 3;
            Func<bool> consume = () => { if (berries <= 0) return false; berries--; return true; };

            _need.TickSeconds(60f); // 100 -> 40
            bool ate = _need.TryEatBerry(consume);

            Assert.IsTrue(ate, "eating with a berry in stock succeeds");
            Assert.AreEqual(2, berries, "exactly ONE berry consumed from the store");
            Assert.AreEqual(40f + _need.berryRestoreAmount, _need.Current, 0.001f,
                "hunger rises by exactly the restore amount IN THE SAME action as the consume " +
                "(a half-wired seam — consume w/o restore, or restore w/o consume — is the silent killer)");
        }

        [Test]
        public void TryEatBerry_WithNoBerry_ChangesNothing_AllOrNothing()
        {
            Func<bool> consumeEmpty = () => false; // no berries
            _need.TickSeconds(60f); // 100 -> 40
            int changedFires = 0;
            _need.Changed += _ => changedFires++;

            bool ate = _need.TryEatBerry(consumeEmpty);

            Assert.IsFalse(ate, "eating with no berry returns false");
            Assert.AreEqual(40f, _need.Current, 0.001f,
                "NO hunger change when the consume fails — restore is inseparable from consume");
            Assert.AreEqual(0, changedFires, "no Changed fired on a no-berry eat (the HUD must not repaint)");
        }

        [Test]
        public void TryEatBerry_NullDelegate_IsSafeNoOp()
        {
            Assert.IsFalse(_need.TryEatBerry(null), "a null consume delegate is a safe no-op (returns false)");
            Assert.AreEqual(100f, _need.Current, 0.001f, "null consume changes nothing");
        }

        // ===== Changed event discipline (mirror WarmthNeedTests) =====

        [Test]
        public void Changed_Fires_OnDecayAndEat_WithCurrent01()
        {
            float last = -1f;
            int count = 0;
            _need.Changed += v => { last = v; count++; };

            _need.TickSeconds(50f); // 100 -> 50
            Assert.AreEqual(0.5f, last, 0.001f, "Changed reports Current01 on decay");
            int afterDecay = count;
            Assert.Greater(afterDecay, 0, "decay must fire Changed (the HUD subscribes, never polls)");

            _need.AddFood(50f); // 50 -> 100
            Assert.AreEqual(1f, last, 0.001f, "Changed reports Current01 on eat");
            Assert.Greater(count, afterDecay, "eating must fire Changed (so the hunger bar updates)");
        }

        [Test]
        public void Changed_DoesNotFire_OnNoOpChange()
        {
            // already full from SetUp
            int count = 0;
            _need.Changed += _ => count++;
            _need.AddFood(0f);        // no-op
            _need.TickSeconds(0f);    // no-op
            _need.AddFood(9999f);     // already at max -> clamps to same value
            Assert.AreEqual(0, count, "Changed must not fire when the clamped value is unchanged");
        }

        [Test]
        public void IsCritical_TracksThreshold()
        {
            Assert.IsFalse(_need.IsCritical, "full hunger is not critical");
            _need.TickSeconds(80f); // 100 -> 20 == 0.2 <= 0.25 threshold
            Assert.IsTrue(_need.IsCritical, "hunger at/below criticalThreshold01 reads critical");
        }

        // ===== Difficulty tiers (difficulty directive — easy/med/hard) =====

        [Test]
        public void ApplyDifficulty_SwapsTheActiveDecayRate()
        {
            _need.easyDecayPerSecond = 0.2f;
            _need.medDecayPerSecond  = 0.5f;
            _need.hardDecayPerSecond = 1.0f;

            _need.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
            Assert.AreEqual(0.2f, _need.decayPerSecond, 0.001f, "Easy tier copies easyDecayPerSecond into the active rate");

            _need.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
            Assert.AreEqual(1.0f, _need.decayPerSecond, 0.001f, "Hard tier copies hardDecayPerSecond into the active rate");

            _need.ApplyDifficulty(SurvivalNeed.DifficultyTier.Medium);
            Assert.AreEqual(0.5f, _need.decayPerSecond, 0.001f, "Medium tier copies medDecayPerSecond into the active rate");
        }

        [Test]
        public void HungerDefaultDecay_IsSlowerThanWarmth()
        {
            // Hunger reads as a SLOWER background pressure than warmth (food is found, not constantly
            // lost). Guard the shipped default against WarmthNeed's default (0.55) so the fiction holds.
            Assert.Less(HungerNeed.HungerMedDecayPerSecond, 0.55f,
                "the hunger default decay must be slower than warmth's 0.55 (slower background pressure)");
            Assert.Less(HungerNeed.HungerEasyDecayPerSecond, HungerNeed.HungerMedDecayPerSecond,
                "easy < medium");
            Assert.Less(HungerNeed.HungerMedDecayPerSecond, HungerNeed.HungerHardDecayPerSecond,
                "medium < hard");
        }

        // ===== Surface-parity (the HUD CONTRACT — HungerNeed binds IDENTICALLY to WarmthNeed) =====

        [Test]
        public void Surface_MirrorsWarmthNeed_HudContract()
        {
            // The need-meter HUD (86caamkxv) binds all three needs through the SurvivalNeed base. Assert
            // HungerNeed exposes the exact contract members WarmthNeed does, with the same shapes.
            Assert.IsTrue(_need is SurvivalNeed, "HungerNeed IS-A SurvivalNeed (the shared base)");

            // Current01 is 0..1 normalized + clamped (NOT raw 0..max — a classic HUD-break).
            _need.AddFood(9999f);
            Assert.AreEqual(1f, _need.Current01, 0.001f, "Current01 is normalized 0..1, clamped at 1");

            Type t = typeof(HungerNeed);
            Assert.IsNotNull(t.GetProperty("Current01"), "Current01 property exists");
            Assert.IsNotNull(t.GetProperty("Current"),   "Current property exists");
            Assert.IsNotNull(t.GetProperty("Max"),       "Max property exists");
            Assert.IsNotNull(t.GetProperty("IsCritical"),"IsCritical property exists");
            Assert.IsNotNull(t.GetEvent("Changed"),      "Changed event exists (HUD subscribes to it)");
            Assert.IsNotNull(t.GetMethod("TickSeconds"), "TickSeconds exists (EditMode decay driver)");
        }
    }
}
