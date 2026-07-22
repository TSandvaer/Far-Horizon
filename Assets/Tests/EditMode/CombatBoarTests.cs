using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the WILD BOAR (ticket 86cah7ydt) — the deterministic AC7 named success-tests that
    /// prove the weapon-vs-mob MATCHUP EMERGES from independent systemic facts (reach + the pierce tag), NOT a
    /// hardcoded table. The PlayMode sibling (BoarCombatPlayTests) proves the live charge; BoarAITests carries
    /// the AI truth tables + meshes; BoarSceneTests proves the scene wiring.
    ///
    /// Regression guards (the bug CLASS, per test):
    ///  - HP/death: a boar that doesn't take damage or die fails first.
    ///  - Weak-to-pierce: a broken resistance hook (pierce not amplified) fails the modulation assert.
    ///  - Reach: an axe that could reach as far as the spear (the contrast collapsing) fails the reach assert.
    ///  - Gore→bleed: the 2nd status consumer not firing (framework doesn't generalize) fails the bleed assert.
    ///  - No-matchup-table: a hardcoded "spear beats boar" would keep the spear's bonus even with the tag
    ///    removed — this test DELETES the tag and asserts the bonus VANISHES (the systemic path, not a lookup).
    /// </summary>
    public class CombatBoarTests
    {
        // === AC7 — boar HP + death on the SHARED enemy-Health surface (a mirror of the snake) ===
        [Test]
        public void Boar_TakesDamage_AndDiesAtZero_OnSharedHealthSurface()
        {
            var go = new GameObject("boar");
            var hp = go.AddComponent<Health>();
            hp.max = BoarEnemy.BoarMedMaxHp; hp.startFull = true;
            hp.resistance = BoarEnemy.BoarResistance;

            float before = hp.Current;
            float removed = hp.ApplyDamage(10f, DamageType.Blunt); // blunt is neutral on the boar
            Assert.Greater(removed, 0f, "ApplyDamage reduces the boar's Current (shared seam — AC1)");
            Assert.Less(hp.Current, before, "the boar took damage");
            Assert.IsFalse(hp.IsDead, "not dead yet");

            hp.ApplyDamage(BoarEnemy.BoarMedMaxHp, DamageType.Blunt); // overkill
            Assert.IsTrue(hp.IsDead, "the boar dies at 0 HP (mirror of the snake/player model — AC1)");

            Object.DestroyImmediate(go);
        }

        // === AC7 — weak-to-PIERCE modulation via the shared tag (pierce does MORE than slash/blunt) ===
        [Test]
        public void Boar_PierceHit_DoesMoreThanSlashOrBlunt_OfEqualBase()
        {
            const float baseDmg = 10f;
            float pierce = HitOnFreshBoar(baseDmg, DamageType.Pierce);
            float slash = HitOnFreshBoar(baseDmg, DamageType.Slash);
            float blunt = HitOnFreshBoar(baseDmg, DamageType.Blunt);

            Assert.Greater(pierce, blunt, "a PIERCE hit does MORE than a neutral (blunt) hit of equal base (AC1/AC3)");
            Assert.Greater(pierce, slash, "a PIERCE hit does MORE than a slash hit of equal base (the tag modulates)");
            Assert.Less(slash, blunt, "the boar is slash-RESISTANT (axe worse than blunt) — worse, not blocked (AC3)");
            // The MAPPING (not a magic number): the pierce amount is EXACTLY base × the boar's pierceMul.
            Assert.AreEqual(baseDmg * BoarEnemy.BoarPierceWeakness, pierce, 1e-3f,
                "pierce damage = base × the boar's pierce-weakness TAG (the shared hook, not a lookup)");
        }

        private static float HitOnFreshBoar(float baseDmg, DamageType type)
        {
            var go = new GameObject("boar");
            var hp = go.AddComponent<Health>();
            hp.max = 1000f; hp.startFull = true;
            hp.resistance = BoarEnemy.BoarResistance;
            float removed = hp.ApplyDamage(baseDmg, type);
            Object.DestroyImmediate(go);
            return removed;
        }

        // === AC7 (THE CORE PROOF) — "spear beats boar" EMERGES; delete the tag and the bonus VANISHES ===
        [Test]
        public void SpearBeatsBoar_IsTheTagComposition_NotAHardcodedMatchup()
        {
            var cat = ScriptableObject.CreateInstance<WeaponCatalog>();
            cat.BuildDefaults();
            var axe = cat.ById(WeaponCatalog.AxeId);     // 14 slash
            var spear = cat.ById(WeaponCatalog.SpearId); // 9 pierce

            // (a) On the pierce-weak / slash-resistant boar, the lower-base SPEAR out-damages the higher-base
            //     AXE — purely from the tag (attributes × the resistance hook), no table.
            var weak = new GameObject("boar-weak"); var hWeak = weak.AddComponent<Health>();
            hWeak.max = 1000f; hWeak.startFull = true; hWeak.resistance = BoarEnemy.BoarResistance;
            float axeOnBoar = hWeak.ApplyDamage(axe.Damage, axe.DamageType);     // 14 × 0.75 = 10.5
            var weak2 = new GameObject("boar-weak2"); var hWeak2 = weak2.AddComponent<Health>();
            hWeak2.max = 1000f; hWeak2.startFull = true; hWeak2.resistance = BoarEnemy.BoarResistance;
            float spearOnBoar = hWeak2.ApplyDamage(spear.Damage, spear.DamageType); // 9 × 2.0 = 18
            Assert.Greater(spearOnBoar, axeOnBoar,
                "the spear out-damages the higher-base axe on the pierce-weak boar (EMERGENT — AC3)");

            // (b) REMOVE the tag (Neutral) → the spear's bonus VANISHES: the spear now does exactly its base
            //     (9), NOT the amplified 18. The advantage was the TAG, not a hardcoded "spear beats boar".
            var neutral = new GameObject("boar-neutral"); var hNeutral = neutral.AddComponent<Health>();
            hNeutral.max = 1000f; hNeutral.startFull = true; hNeutral.resistance = ResistanceProfile.Neutral;
            float spearNoTag = hNeutral.ApplyDamage(spear.Damage, spear.DamageType);
            Assert.AreEqual(spear.Damage, spearNoTag, 1e-3f,
                "with the pierce-weak tag REMOVED the spear does exactly its base — no hidden matchup bonus");
            Assert.Greater(spearOnBoar, spearNoTag,
                "the boar's pierce bonus is PURELY the tag: delete it and the bonus is gone (no lookup — AC3)");

            Object.DestroyImmediate(weak); Object.DestroyImmediate(weak2);
            Object.DestroyImmediate(neutral); Object.DestroyImmediate(cat);
        }

        // === AC7 — reach advantage: the spear's reach attribute lands where the axe's does NOT ===
        [Test]
        public void SpearReach_LandsAtADistance_TheAxeReachDoesNot()
        {
            var cat = ScriptableObject.CreateInstance<WeaponCatalog>();
            cat.BuildDefaults();
            var axe = cat.ById(WeaponCatalog.AxeId);
            var spear = cat.ById(WeaponCatalog.SpearId);

            Assert.Greater(spear.Reach, axe.Reach, "the spear reaches FURTHER than the axe (AC4 attribute contrast)");

            // A distance strictly between the two reaches (the SAME planar `<= reach` metric
            // MeleeAttack.ResolveNearestTarget applies). The reach ATTRIBUTE alone decides the hit: at this
            // distance a spear lands, an axe whiffs — the charging boar is hit by the spear FIRST (AC3).
            float midDist = (axe.Reach + spear.Reach) * 0.5f; // 2.8 for 2.0 / 3.6
            Assert.IsTrue(InReach(midDist, spear.Reach), "the spear reach lands the hit at the charge distance");
            Assert.IsFalse(InReach(midDist, axe.Reach), "the axe reach does NOT — it must let the boar close first (AC3)");

            Object.DestroyImmediate(cat);
        }

        // The pure in-reach predicate MeleeAttack uses (planar distance <= weapon reach). Static so the reach
        // attribute → range outcome is asserted with no scene/MeleeAttack rig (the POC weapon-attribute shape).
        private static bool InReach(float planarDist, float reach) => planarDist <= reach;

        // === AC7 — gore optionally applies BLEED to the player (the 2nd status-framework consumer) ===
        [Test]
        public void BoarGore_AppliesBleedToPlayer_ThroughSharedStatusSeam()
        {
            var boarGo = new GameObject("boar");
            boarGo.AddComponent<Health>();
            var enemy = boarGo.AddComponent<BoarEnemy>();
            enemy.goreDamage = BoarEnemy.BoarMedGoreDamage;
            enemy.goreBleed = StatusEffectSpec.MakeBleed(2f, 3f); // author directly (no Awake in EditMode)

            var playerGo = new GameObject("player");
            var playerHp = playerGo.AddComponent<Health>();
            playerHp.max = 100f; playerHp.startFull = true;
            var fx = playerGo.AddComponent<StatusEffectController>();
            fx.health = playerHp; // EditMode has no Awake on AddComponent — wire what the build's Awake wires

            float removed = enemy.Gore(playerHp);
            Assert.Greater(removed, 0f, "the gore removes player HP through the shared seam (AC2)");
            Assert.AreEqual(1, fx.ActiveCount, "the gore APPLIES a bleed — the 2nd status-framework consumer (AC4)");

            // The bleed is a DoT: it ticks HP down over a Time.time window, then expires (driven via TickSeconds).
            float afterGore = playerHp.Current;
            fx.TickSeconds(1f); // 2 HP/s × 1s
            Assert.Less(playerHp.Current, afterGore, "the bleed ticks HP down over time (AC4 — the shared framework)");
            fx.TickSeconds(5f); // past the 3s duration → expires
            Assert.AreEqual(0, fx.ActiveCount, "the bleed expires after its duration (the framework, not a bespoke DoT)");

            Object.DestroyImmediate(boarGo); Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void BoarGore_TierScaled_ThroughSharedSeam()
        {
            var boarGo = new GameObject("boar");
            boarGo.AddComponent<Health>();
            var enemy = boarGo.AddComponent<BoarEnemy>();
            var playerGo = new GameObject("player");
            var playerHp = playerGo.AddComponent<Health>(); // default 100 max, neutral resistance, mul 1

            enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
            float easy = enemy.Gore(playerHp);
            enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
            float hard = enemy.Gore(playerHp);

            Assert.AreEqual(BoarEnemy.BoarEasyGoreDamage, easy, 1e-3f, "easy gore = the easy map through the seam (AC6)");
            Assert.AreEqual(BoarEnemy.BoarHardGoreDamage, hard, 1e-3f, "hard gore = the hard map through the seam (AC6)");
            Assert.Greater(hard, easy, "hard gore > easy through the LIVE seam (AC6)");

            Object.DestroyImmediate(boarGo); Object.DestroyImmediate(playerGo);
        }
    }
}
