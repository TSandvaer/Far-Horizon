using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the Combat POC (ticket 86cah7xxp) — weapon-attribute-driven damage (AC4), the
    /// needs-gated regen truth-table (AC3), and tiered death (AC2). The deterministic half of AC10; the
    /// PlayMode siblings prove the scene wiring + Time.time windows.
    /// </summary>
    public class CombatWeaponRegenDeathTests
    {
        // === AC4 — weapon attributes drive damage (axe vs spear DIFFERENT outcomes through the seam) ===
        // Asserts the MAPPING (weapon.Damage × resistance × tier), NOT a magic number.
        [Test]
        public void WeaponCatalog_AxeAndSpear_HaveContrastingAttributes()
        {
            var cat = ScriptableObject.CreateInstance<WeaponCatalog>();
            cat.BuildDefaults();
            var axe = cat.ById(WeaponCatalog.AxeId);
            var spear = cat.ById(WeaponCatalog.SpearId);
            Assert.IsNotNull(axe, "axe def exists");
            Assert.IsNotNull(spear, "spear def exists");

            Assert.AreEqual(DamageType.Slash, axe.DamageType, "axe is Slash");
            Assert.AreEqual(DamageType.Pierce, spear.DamageType, "spear is Pierce");
            Assert.Greater(axe.Damage, spear.Damage, "axe hits harder per-hit than the spear (AC4 contrast)");
            Assert.Greater(spear.Reach, axe.Reach, "spear reaches FURTHER than the axe (AC4 contrast)");
            Assert.IsTrue(axe.OnHitStatus.IsActive, "axe carries an on-hit status (bleed)");
            Assert.IsFalse(spear.OnHitStatus.IsActive, "spear carries no on-hit status");

            Object.DestroyImmediate(cat);
        }

        [Test]
        public void WeaponDamage_AxeVsSpear_ProduceDifferentOutcomes_ThroughSeam()
        {
            var cat = ScriptableObject.CreateInstance<WeaponCatalog>();
            cat.BuildDefaults();
            var axe = cat.ById(WeaponCatalog.AxeId);
            var spear = cat.ById(WeaponCatalog.SpearId);

            // Neutral target — the outcome difference is purely the weapon attribute mapping.
            var go1 = new GameObject("t1"); var h1 = go1.AddComponent<Health>();
            h1.max = 1000f; h1.startFull = true;
            float axeDmg = h1.ApplyDamage(axe.Damage, axe.DamageType);

            var go2 = new GameObject("t2"); var h2 = go2.AddComponent<Health>();
            h2.max = 1000f; h2.startFull = true;
            float spearDmg = h2.ApplyDamage(spear.Damage, spear.DamageType);

            // The MAPPING assertion (not a magic number): damage-out equals the weapon's Damage on a neutral
            // target, and the two weapons differ (the axe out-damages the spear on a neutral target).
            Assert.AreEqual(axe.Damage, axeDmg, 0.001f, "axe damage-out = its Damage attribute on a neutral target");
            Assert.AreEqual(spear.Damage, spearDmg, 0.001f, "spear damage-out = its Damage attribute on a neutral target");
            Assert.AreNotEqual(axeDmg, spearDmg, "axe and spear produce DIFFERENT outcomes through the seam (AC4)");

            Object.DestroyImmediate(go1); Object.DestroyImmediate(go2); Object.DestroyImmediate(cat);
        }

        [Test]
        public void WeaponDamage_SpearBeatsAxe_OnPierceWeakTarget()
        {
            // The full AC4×AC8a interaction: the spear (Pierce, lower base) OUT-damages the axe (Slash, higher
            // base) against a PIERCE-WEAK snake — attributes × matchup, the point of the data-driven system.
            var cat = ScriptableObject.CreateInstance<WeaponCatalog>();
            cat.BuildDefaults();
            var axe = cat.ById(WeaponCatalog.AxeId);   // 14 slash
            var spear = cat.ById(WeaponCatalog.SpearId); // 9 pierce

            var goA = new GameObject("snakeA"); var hA = goA.AddComponent<Health>();
            hA.max = 1000f; hA.startFull = true;
            hA.resistance = new ResistanceProfile { slashMul = 1f, pierceMul = SnakeEnemy.SnakePierceWeakness, bluntMul = 1f };
            float axeOnSnake = hA.ApplyDamage(axe.Damage, axe.DamageType); // 14 * 1 = 14

            var goB = new GameObject("snakeB"); var hB = goB.AddComponent<Health>();
            hB.max = 1000f; hB.startFull = true;
            hB.resistance = new ResistanceProfile { slashMul = 1f, pierceMul = SnakeEnemy.SnakePierceWeakness, bluntMul = 1f };
            float spearOnSnake = hB.ApplyDamage(spear.Damage, spear.DamageType); // 9 * 1.6 = 14.4

            Assert.Greater(spearOnSnake, axeOnSnake,
                "the spear (pierce) out-damages the higher-base axe (slash) on a pierce-weak snake (AC4×AC8a)");

            Object.DestroyImmediate(goA); Object.DestroyImmediate(goB); Object.DestroyImmediate(cat);
        }

        // === AC7 — enemy HP + bite through the shared seam ===
        [Test]
        public void SnakeBite_DamagesPlayer_ThroughSharedSeam()
        {
            var snakeGo = new GameObject("snake");
            var snakeHp = snakeGo.AddComponent<Health>();
            snakeHp.max = SnakeEnemy.SnakeMaxHp; snakeHp.startFull = true;
            snakeHp.resistance = new ResistanceProfile { slashMul = 1f, pierceMul = SnakeEnemy.SnakePierceWeakness, bluntMul = 1f };
            var snake = snakeGo.AddComponent<SnakeEnemy>();
            snake.biteDamage = SnakeEnemy.SnakeBiteDamage; // author the bite directly (no Awake in EditMode)

            var playerGo = new GameObject("player");
            var playerHp = playerGo.AddComponent<Health>();
            playerHp.max = 100f; playerHp.startFull = true;
            playerGo.AddComponent<StatusEffectController>().health = playerHp;

            float removed = snake.Bite(playerHp);
            Assert.Greater(removed, 0f, "a snake bite removes player HP through the shared ApplyDamage seam (AC7)");
            Assert.Less(playerHp.Current, 100f, "the player took bite damage");

            // The snake dies at 0 (mirror of the player model) — a few pierce hits fell it.
            snakeHp.ApplyDamage(SnakeEnemy.SnakeMaxHp, DamageType.Pierce);
            Assert.IsTrue(snakeHp.IsDead, "the snake dies at 0 HP (mirror of the player model, AC7)");
            Assert.AreEqual(0f, snake.Bite(playerHp), 0.001f, "a dead snake doesn't bite");

            Object.DestroyImmediate(snakeGo); Object.DestroyImmediate(playerGo);
        }

        // === AC3 — needs-gated regen truth-table (raises while satisfied, STALLS on critical) ===
        [Test]
        public void Regen_Gate_AllSatisfied_True()
        {
            Assert.IsTrue(HealthRegen.ShouldRegen(
                0.9f, false, 0.8f, false, 0.7f, false, threshold01: 0.4f),
                "all needs above threshold + none critical → regen runs");
        }

        [Test]
        public void Regen_Gate_OneBelowThreshold_False()
        {
            Assert.IsFalse(HealthRegen.ShouldRegen(
                0.9f, false, 0.3f, false, 0.7f, false, threshold01: 0.4f),
                "one need below threshold stalls regen");
        }

        [Test]
        public void Regen_Gate_OneCritical_False()
        {
            Assert.IsFalse(HealthRegen.ShouldRegen(
                0.9f, false, 0.9f, true, 0.9f, false, threshold01: 0.4f),
                "a CRITICAL need stalls regen even if its value reads above threshold (AC3)");
        }

        [Test]
        public void Regen_Gate_AbsentNeed_DoesNotGate()
        {
            // A null need reads NaN → doesn't gate (a bare rig with one need still regenerates on it).
            Assert.IsTrue(HealthRegen.ShouldRegen(
                0.9f, false, float.NaN, false, float.NaN, false, threshold01: 0.4f),
                "an absent (NaN) need does not block regen");
        }

        [Test]
        public void Regen_TicksHp_WhileSatisfied_StallsOnCritical()
        {
            // Full scene-less integration of the gate + TickSeconds via the SurvivalNeed test surface.
            var hGo = new GameObject("hp"); var h = hGo.AddComponent<Health>();
            h.max = 100f; h.startFull = true;
            h.ApplyDamage(40f, DamageType.Slash); // 100 -> 60, room to regen

            var wGo = new GameObject("warmth"); var warmth = wGo.AddComponent<WarmthNeed>();
            warmth.max = 100f; warmth.criticalThreshold01 = 0.25f; warmth.AddWarmth(100f); // full
            var huGo = new GameObject("hunger"); var hunger = huGo.AddComponent<HungerNeed>();
            hunger.max = 100f; hunger.criticalThreshold01 = 0.25f; hunger.AddFood(100f); // full
            var tGo = new GameObject("thirst"); var thirst = tGo.AddComponent<ThirstNeed>();
            thirst.max = 100f; thirst.criticalThreshold01 = 0.25f; thirst.AddWater(100f); // full

            var rGo = new GameObject("regen"); var regen = rGo.AddComponent<HealthRegen>();
            regen.health = h; regen.warmth = warmth; regen.hunger = hunger; regen.thirst = thirst;
            regen.regenPerSecond = 5f; regen.needThreshold01 = 0.4f; regen.criticalSlowDrains = false;

            regen.TickSeconds(2f); // all needs full -> +10 HP
            Assert.AreEqual(70f, h.Current, 0.001f, "regen raises HP while all needs are satisfied (AC3)");

            // Drive one need CRITICAL via the SurvivalNeed test surface, then regen must STALL.
            hunger.decayPerSecond = 100f; hunger.floor01 = 0f;
            hunger.TickSeconds(1f); // hunger drops to ~0 (critical)
            Assert.IsTrue(hunger.IsCritical, "hunger is now critical");

            float hpBefore = h.Current;
            regen.TickSeconds(5f);
            Assert.AreEqual(hpBefore, h.Current, 0.001f, "regen STALLS while a need is critical (AC3 default)");

            Object.DestroyImmediate(hGo); Object.DestroyImmediate(wGo); Object.DestroyImmediate(huGo);
            Object.DestroyImmediate(tGo); Object.DestroyImmediate(rGo);
        }

        // === AC2 — tiered death (easy faint-in-place / medium campfire+keep / hard camp+drop) ===
        private (GameObject player, Health hp, DeathHandler death, Inventory inv) BuildDeathRig()
        {
            var playerGo = new GameObject("player");
            playerGo.transform.position = new Vector3(50f, 0f, 50f); // the death spot / start beach
            var hp = playerGo.AddComponent<Health>();
            hp.max = 100f; hp.startFull = true;
            var inv = playerGo.AddComponent<Inventory>();
            var death = playerGo.AddComponent<DeathHandler>();
            death.health = hp; death.playerRoot = playerGo.transform; death.inventory = inv;
            death.CaptureSpawn(); // capture the world spawn (Start not run in EditMode; direct call, no SendMessage)
            return (playerGo, hp, death, inv);
        }

        [Test]
        public void Death_Easy_FaintsInPlace_NoTravelSetback()
        {
            var r = BuildDeathRig();
            r.death.HandleDeath(SurvivalNeed.DifficultyTier.Easy);
            Assert.IsTrue(r.death.LastFaintedInPlace, "easy = faint & recover IN PLACE (AC2)");
            Assert.AreEqual(new Vector3(50f, 0f, 50f), r.death.LastRespawnPosition, "easy does not move the player (no travel setback)");
            Assert.IsFalse(r.hp.IsDead, "the castaway revives (HP restored)");
            Assert.AreEqual(100f, r.hp.Current, 0.001f, "revive is at full HP");
            Object.DestroyImmediate(r.player);
        }

        [Test]
        public void Death_Medium_RespawnsAtCampfire_KeepsInventory()
        {
            var r = BuildDeathRig();
            // Give some wood; medium must KEEP it.
            r.inv.AddWood(5);
            // A lit campfire at a distinct spot.
            var fireGo = new GameObject("fire"); fireGo.transform.position = new Vector3(10f, 0f, 10f);
            var fire = fireGo.AddComponent<Campfire>(); fire.Light();
            r.death.campfire = fire;

            r.death.HandleDeath(SurvivalNeed.DifficultyTier.Medium);
            Assert.AreEqual(new Vector3(10f, 0f, 10f), r.death.LastRespawnPosition, "medium respawns at the lit campfire (AC2)");
            Assert.IsFalse(r.death.LastDroppedInventory, "medium KEEPS the inventory (no drop)");
            Assert.AreEqual(5, r.inv.WoodCount, "medium keeps the wood");

            Object.DestroyImmediate(r.player); Object.DestroyImmediate(fireGo);
        }

        [Test]
        public void Death_Medium_NoCampfire_FallsBackToWorldSpawn()
        {
            var r = BuildDeathRig(); // no campfire wired
            r.player.transform.position = new Vector3(200f, 0f, 200f); // walked far from the start beach
            r.death.HandleDeath(SurvivalNeed.DifficultyTier.Medium);
            Assert.AreEqual(new Vector3(50f, 0f, 50f), r.death.LastRespawnPosition,
                "medium with no lit campfire falls back to the world spawn / start beach (AC2)");
            Object.DestroyImmediate(r.player);
        }

        [Test]
        public void Death_Hard_RespawnsAtCamp_DropsInventoryAtDeathSpot()
        {
            var r = BuildDeathRig();
            r.inv.AddWood(7);
            r.inv.Model.AddItem(r.inv.Catalog.ById(ItemCatalog.StoneId), 3);
            var fireGo = new GameObject("fire"); fireGo.transform.position = new Vector3(10f, 0f, 10f);
            var fire = fireGo.AddComponent<Campfire>(); fire.Light();
            r.death.campfire = fire;
            r.player.transform.position = new Vector3(90f, 0f, 90f); // die out here

            r.death.HandleDeath(SurvivalNeed.DifficultyTier.Hard);

            Assert.AreEqual(new Vector3(10f, 0f, 10f), r.death.LastRespawnPosition, "hard respawns at the camp (AC2)");
            Assert.IsTrue(r.death.LastDroppedInventory, "hard DROPS the inventory (AC2)");
            Assert.AreEqual(7, r.death.LastDropWood, "the dropped wood is captured (reclaimable)");
            Assert.AreEqual(3, r.death.LastDropStone, "the dropped stone is captured");
            Assert.AreEqual(new Vector3(90f, 0f, 90f), r.death.LastDropPosition, "the drop lands at the DEATH spot (reclaimable)");
            Assert.AreEqual(0, r.inv.WoodCount, "the wood was dropped OFF the player");
            Assert.AreEqual(0, r.inv.Model.CountItem(ItemCatalog.StoneId), "the stone was dropped off the player");

            Object.DestroyImmediate(r.player); Object.DestroyImmediate(fireGo);
        }
    }
}
