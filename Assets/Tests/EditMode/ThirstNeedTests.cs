using System;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the THIRST need model (ticket 86caamkv7).
    ///
    /// Drives decay DETERMINISTICALLY via TickSeconds (no scene/Update/wall-clock) — the math
    /// (decay-over-a-window, floor, clamp, AddWater restore, the drink-from-hand seam, Changed event,
    /// difficulty tiers) is proven fast in headless CI. The COMPLEMENTARY PlayMode test
    /// (ThirstNeedPlayModeTests) proves the same decay actually fires through Update over a REAL
    /// Time.time window (the Time.deltaTime~=0 trap, unity-conventions.md). Together they catch the
    /// bug CLASS: a decay that integrates wrong, or one that silently never ticks in a build.
    ///
    /// Mirrors HungerNeedTests 1:1 (the HUD-contract surface is byte-identical) + swaps the eat-seam for
    /// the drink-from-hand seam (AC3a, owned here — gated on PROXIMITY, not an inventory consume) and the
    /// surface-parity / faster-than-hunger decay guards.
    /// </summary>
    public class ThirstNeedTests
    {
        private GameObject _go;
        private ThirstNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ThirstTest");
            _need = _go.AddComponent<ThirstNeed>();
            // Deterministic config independent of inspector tuning drift.
            _need.max = 100f;
            _need.decayPerSecond = 1f; // 1 thirst/sec -> trivially checkable
            _need.floor01 = 0.1f;      // floor at 10
            _need.criticalThreshold01 = 0.25f;
            _need.waterScoopAmount = 12f;
            _need.startFull = true;
            // Seed _current = max WITHOUT running Start() (EditMode has no lifecycle); do it explicitly.
            _need.AddWater(_need.max); // _current starts 0; add max -> 100, also exercises clamp
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_go);

        // ===== Decay / floor / clamp (mirror HungerNeedTests) =====

        [Test]
        public void Decay_OverAWindow_ReducesThirst_AtTheConfiguredRate()
        {
            Assert.AreEqual(100f, _need.Current, 0.001f, "starts full");
            _need.TickSeconds(10f); // 10s * 1/sec = 10 lost
            Assert.AreEqual(90f, _need.Current, 0.001f,
                "thirst must decay decayPerSecond*seconds over a window, not per-frame");
            Assert.AreEqual(0.9f, _need.Current01, 0.001f, "Current01 tracks the raw value normalized");
        }

        [Test]
        public void Decay_StopsAtFloor_NeverBelow()
        {
            _need.TickSeconds(1000f); // way past empty
            Assert.AreEqual(10f, _need.Current, 0.001f,
                "decay must rest at floor01*max (10), a simple floor — NOT a fail/dehydration state");
            _need.TickSeconds(1000f);
            Assert.AreEqual(10f, _need.Current, 0.001f, "floor holds across repeated ticks");
        }

        // ===== Satisfaction: AddWater / the drink seam =====

        [Test]
        public void AddWater_RestoresAndClampsToMax()
        {
            _need.TickSeconds(60f); // 100 -> 40
            Assert.AreEqual(40f, _need.Current, 0.001f);

            _need.AddWater(30f); // explicit amount
            Assert.AreEqual(70f, _need.Current, 0.001f, "AddWater raises thirst");

            _need.AddWater(9999f); // over-satisfy
            Assert.AreEqual(100f, _need.Current, 0.001f, "AddWater clamps to max");
        }

        [Test]
        public void AddWater_NoArg_RestoresExactlyTheScoopAmount()
        {
            _need.TickSeconds(60f); // 100 -> 40
            _need.AddWater();       // default scoop restore
            Assert.AreEqual(40f + _need.waterScoopAmount, _need.Current, 0.001f,
                "the no-arg AddWater restores exactly waterScoopAmount (the per-scoop sip)");
        }

        // ===== THE DRINK-FROM-HAND SEAM (AC3a — owned here; PROXIMITY-gated, NOT an inventory consume) =====

        [Test]
        public void TryDrink_InPondReach_RestoresExactlyTheScoopAmount()
        {
            _need.TickSeconds(60f); // 100 -> 40
            bool drank = _need.TryDrink(true); // in pond reach

            Assert.IsTrue(drank, "drinking while in pond reach succeeds");
            Assert.AreEqual(40f + _need.waterScoopAmount, _need.Current, 0.001f,
                "thirst rises by exactly the per-scoop amount when in reach (a scoop = a small sip)");
        }

        [Test]
        public void TryDrink_FarFromPond_ChangesNothing()
        {
            _need.TickSeconds(60f); // 100 -> 40
            int changedFires = 0;
            _need.Changed += _ => changedFires++;

            bool drank = _need.TryDrink(false); // NOT in reach

            Assert.IsFalse(drank, "drinking far from the pond returns false");
            Assert.AreEqual(40f, _need.Current, 0.001f,
                "NO thirst change when out of pond reach — a scoop from anywhere is the silent killer " +
                "(thirst is a PROXIMITY interact, not a free top-up)");
            Assert.AreEqual(0, changedFires, "no Changed fired on an out-of-reach drink (the HUD must not repaint)");
        }

        [Test]
        public void TryDrink_IsRepeatable_EachInReachCallIsAnotherScoop()
        {
            _need.TickSeconds(80f); // 100 -> 20
            _need.TryDrink(true);
            Assert.AreEqual(20f + _need.waterScoopAmount, _need.Current, 0.001f, "first scoop");
            _need.TryDrink(true);
            Assert.AreEqual(20f + 2f * _need.waterScoopAmount, _need.Current, 0.001f,
                "drinking is repeatable — each in-reach scoop restores another waterScoopAmount");
        }

        [Test]
        public void TryDrink_NeverTouchesInventory_ThirstIsNotBerries()
        {
            // Thirst restore takes NO consume delegate (unlike HungerNeed.TryEatBerry) — there is no item
            // to debit. This test documents that contract: the seam's only gate is the proximity bool.
            // (A compile-time guarantee: TryDrink's signature is (bool), not (Func<bool>).)
            var m = typeof(ThirstNeed).GetMethod("TryDrink");
            Assert.IsNotNull(m, "TryDrink exists");
            var ps = m.GetParameters();
            Assert.AreEqual(1, ps.Length, "TryDrink takes exactly one parameter");
            Assert.AreEqual(typeof(bool), ps[0].ParameterType,
                "TryDrink's gate is a proximity bool, NOT an inventory consume delegate (thirst is NOT berries)");
        }

        // ===== Changed event discipline (mirror HungerNeedTests) =====

        [Test]
        public void Changed_Fires_OnDecayAndDrink_WithCurrent01()
        {
            float last = -1f;
            int count = 0;
            _need.Changed += v => { last = v; count++; };

            _need.TickSeconds(50f); // 100 -> 50
            Assert.AreEqual(0.5f, last, 0.001f, "Changed reports Current01 on decay");
            int afterDecay = count;
            Assert.Greater(afterDecay, 0, "decay must fire Changed (the HUD subscribes, never polls)");

            _need.TryDrink(true); // 50 -> 50 + scoop
            Assert.AreEqual((50f + _need.waterScoopAmount) / 100f, last, 0.001f,
                "Changed reports Current01 on a drink-scoop");
            Assert.Greater(count, afterDecay, "drinking must fire Changed (so the thirst bar updates)");
        }

        [Test]
        public void Changed_DoesNotFire_OnNoOpChange()
        {
            // already full from SetUp
            int count = 0;
            _need.Changed += _ => count++;
            _need.AddWater(0f);        // no-op
            _need.TickSeconds(0f);     // no-op
            _need.AddWater(9999f);     // already at max -> clamps to same value
            _need.TryDrink(false);     // out of reach -> no change
            Assert.AreEqual(0, count, "Changed must not fire when the clamped value is unchanged");
        }

        [Test]
        public void IsCritical_TracksThreshold()
        {
            Assert.IsFalse(_need.IsCritical, "full thirst is not critical");
            _need.TickSeconds(80f); // 100 -> 20 == 0.2 <= 0.25 threshold
            Assert.IsTrue(_need.IsCritical, "thirst at/below criticalThreshold01 reads critical");
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
        public void ThirstDefaultDecay_IsFasterThanHunger_SlowerThanWarmth()
        {
            // Thirst becomes pressing "after eating the berries" — faster background pressure than hunger
            // (food is found) but not as fast as warmth (constantly lost wet+cold). Guard the shipped
            // default so the fiction holds: hunger 0.35 < thirst 0.45 < warmth 0.55.
            Assert.Greater(ThirstNeed.ThirstMedDecayPerSecond, HungerNeed.HungerMedDecayPerSecond,
                "thirst default decay must be FASTER than hunger's (pressing after eating)");
            Assert.Less(ThirstNeed.ThirstMedDecayPerSecond, 0.55f,
                "thirst default decay stays slower than warmth's 0.55 (warmth is the most pressing)");
            Assert.Less(ThirstNeed.ThirstEasyDecayPerSecond, ThirstNeed.ThirstMedDecayPerSecond,
                "easy < medium");
            Assert.Less(ThirstNeed.ThirstMedDecayPerSecond, ThirstNeed.ThirstHardDecayPerSecond,
                "medium < hard");
        }

        // ===== Surface-parity (the HUD CONTRACT — ThirstNeed binds IDENTICALLY to Warmth/Hunger) =====

        [Test]
        public void Surface_MirrorsSurvivalNeed_HudContract()
        {
            // The need-meter HUD (86caamkxv) binds all three needs through the SurvivalNeed base. Assert
            // ThirstNeed exposes the exact contract members WarmthNeed/HungerNeed do, with the same shapes.
            Assert.IsTrue(_need is SurvivalNeed, "ThirstNeed IS-A SurvivalNeed (the shared base)");

            // Current01 is 0..1 normalized + clamped (NOT raw 0..max — a classic HUD-break).
            _need.AddWater(9999f);
            Assert.AreEqual(1f, _need.Current01, 0.001f, "Current01 is normalized 0..1, clamped at 1");

            Type t = typeof(ThirstNeed);
            Assert.IsNotNull(t.GetProperty("Current01"), "Current01 property exists");
            Assert.IsNotNull(t.GetProperty("Current"),   "Current property exists");
            Assert.IsNotNull(t.GetProperty("Max"),       "Max property exists");
            Assert.IsNotNull(t.GetProperty("IsCritical"),"IsCritical property exists");
            Assert.IsNotNull(t.GetEvent("Changed"),      "Changed event exists (HUD subscribes to it)");
            Assert.IsNotNull(t.GetMethod("TickSeconds"), "TickSeconds exists (EditMode decay driver)");
        }
    }
}
