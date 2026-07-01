using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the Combat POC (ticket 86cah7xxp) — the "seams actually FIRE through Update over
    /// a REAL Time.time window" half of AC10 (headless Time.deltaTime≈0, unity-conventions.md §Headless). The
    /// EditMode siblings (CombatHealthTests / CombatWeaponRegenDeathTests) prove the deterministic math via
    /// TickSeconds; these prove the DoT + regen actually tick through Update (not just via the test hook) and
    /// the death event fires end-to-end + the HP HUD band mapping (AC9). Together they catch a bleed/regen
    /// that silently never ticks in a build.
    /// </summary>
    public class CombatPlayModeTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown() { if (_go != null) Object.Destroy(_go); }

        // AC6 — a bleed actually ticks HP DOWN through Update over a real Time.time window (not just TickSeconds).
        [UnityTest]
        public IEnumerator Bleed_TicksHpDown_ThroughUpdate_OverRealTime()
        {
            _go = new GameObject("bleedTarget");
            var h = _go.AddComponent<Health>();
            h.max = 100f; h.startFull = true;
            var sec = _go.AddComponent<StatusEffectController>();
            sec.health = h;

            yield return null; // let Start() seed the tick window
            float before = h.Current;
            sec.Apply(StatusEffectSpec.MakeBleed(damagePerSecond: 20f, durationSeconds: 5f));

            // Let real wall-clock elapse — the bleed must tick HP down through Update (Time.time deltas).
            yield return new WaitForSeconds(0.6f);

            Assert.Less(h.Current, before, "a bleed ticks HP DOWN through Update over a real Time.time window (AC6)");
            Assert.AreEqual(1, sec.ActiveCount, "the bleed is still active mid-duration");
        }

        // AC3 — needs-gated regen actually HEALS through Update while needs are satisfied.
        [UnityTest]
        public IEnumerator Regen_HealsHp_ThroughUpdate_WhileNeedsSatisfied()
        {
            _go = new GameObject("regenPlayer");
            var h = _go.AddComponent<Health>();
            h.max = 100f; h.startFull = true;
            h.ApplyDamage(50f, DamageType.Slash); // 100 -> 50, room to regen

            var warmth = _go.AddComponent<WarmthNeed>(); warmth.max = 100f; warmth.AddWarmth(100f);
            var hunger = _go.AddComponent<HungerNeed>(); hunger.max = 100f; hunger.AddFood(100f);
            var thirst = _go.AddComponent<ThirstNeed>(); thirst.max = 100f; thirst.AddWater(100f);
            // Stop the needs decaying enough to fall below the gate in this short window.
            warmth.decayPerSecond = 0f; hunger.decayPerSecond = 0f; thirst.decayPerSecond = 0f;

            var regen = _go.AddComponent<HealthRegen>();
            regen.health = h; regen.warmth = warmth; regen.hunger = hunger; regen.thirst = thirst;
            regen.regenPerSecond = 30f; regen.needThreshold01 = 0.4f;

            float before = h.Current;
            yield return new WaitForSeconds(0.6f);

            Assert.Greater(h.Current, before, "regen HEALS HP through Update while needs are satisfied (AC3)");
        }

        // AC1/AC2 — HP death fires Died end-to-end, and the tiered death handler revives the player.
        [UnityTest]
        public IEnumerator Death_FiresDied_AndTieredHandlerRevives()
        {
            _go = new GameObject("dyingPlayer");
            _go.transform.position = new Vector3(5f, 0f, 5f);
            var h = _go.AddComponent<Health>();
            h.max = 40f; h.startFull = true;
            var death = _go.AddComponent<DeathHandler>();
            death.health = h; death.playerRoot = _go.transform; death.tier = SurvivalNeed.DifficultyTier.Easy;

            yield return null; // Start() captures the world spawn + subscribes

            int diedCount = 0;
            h.Died += () => diedCount++;
            h.ApplyDamage(40f, DamageType.Slash); // kill

            yield return null; // let the Died handler run
            Assert.AreEqual(1, diedCount, "Died fires on the 0-crossing (AC1)");
            Assert.AreEqual(1, death.DeathCount, "the tiered death handler ran (AC2)");
            Assert.IsTrue(death.LastFaintedInPlace, "easy tier faints in place (AC2)");
            Assert.IsFalse(h.IsDead, "the tiered handler revives the castaway (AC2)");
        }

        // AC5 — a left-click attack swings + lands weapon-driven damage on an in-reach enemy through the seam.
        [UnityTest]
        public IEnumerator MeleeAttack_Click_SwingsAndDamagesInReachEnemy()
        {
            _go = new GameObject("attackRig");
            // Player root at origin with an inventory holding the axe selected.
            _go.transform.position = Vector3.zero;
            var inv = _go.AddComponent<Inventory>();
            inv.PickUpAxe(); // axe onto the belt, auto-selected (slot 0)

            var attack = _go.AddComponent<MeleeAttack>();
            attack.player = _go.transform; attack.inventory = inv;

            // A snake within the axe's reach (2.0u) — place it 1u away.
            var snakeGo = new GameObject("snake");
            snakeGo.transform.position = new Vector3(1f, 0f, 0f);
            var snakeHp = snakeGo.AddComponent<Health>();
            snakeHp.max = 50f; snakeHp.startFull = true;
            snakeGo.AddComponent<StatusEffectController>().health = snakeHp;

            yield return null; // Awake/Start

            float before = snakeHp.Current;
            attack.RequestAttackClick(); // the input-independent analog of a left-click
            yield return null; // Update consumes the click

            Assert.Greater(attack.SwingsFired, 0, "the click fired a swing (AC5)");
            Assert.Less(snakeHp.Current, before, "the attack landed weapon-driven damage on the in-reach enemy (AC5)");
            Assert.AreEqual(WeaponCatalog.AxeId, attack.LastWeaponId, "the selected weapon (axe) was used");

            Object.Destroy(snakeGo);
        }

        // AC9 — the HP HUD band mapping (heart-red healthy / wound-orange hurt / dark-blood critical).
        [Test]
        public void SurvivalHud_HpBandColor_MapsHealthyHurtCritical()
        {
            // Static band mapping — no scene needed. Distinct hues per band; healthy is the warm heart-red.
            Color healthy = SurvivalHud.HpBandColor(0.9f);
            Color hurt = SurvivalHud.HpBandColor(0.45f);
            Color critical = SurvivalHud.HpBandColor(0.1f);

            Assert.AreNotEqual(healthy, hurt, "the HP band shifts between healthy and hurt (AC9)");
            Assert.AreNotEqual(hurt, critical, "the HP band shifts between hurt and critical (AC9)");
            // Healthy HP is dominantly RED (health = the heart; the one red note, never a need hue).
            Assert.Greater(healthy.r, healthy.g, "healthy HP reads RED (r > g)");
            Assert.Greater(healthy.r, healthy.b, "healthy HP reads RED (r > b)");
        }
    }
}
