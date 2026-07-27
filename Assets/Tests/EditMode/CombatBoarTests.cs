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
    ///  - Reach: driven through the REAL MeleeAttack.ResolveNearestTarget seam (via the public ClickGateDiag
    ///    read) rather than a mirrored predicate (86cavg2k1 NIT 3) — so an axe that could reach as far as the
    ///    spear, a hardcoded range replacing the weapon's Reach attribute, a planar→3D metric swap, or a
    ///    resolver that stops skipping dead targets ALL fail here.
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

        // === AC7 — reach advantage, driven through the REAL MeleeAttack target-resolution seam ===
        // 86cavg2k1 NIT 3 (Devon's PR #332 review 4754125364): this test used to assert against a LOCAL
        // `InReach(dist, reach) => dist <= reach` predicate that MIRRORED MeleeAttack's rule. A test that
        // re-implements the thing it guards can only red when the MIRROR changes — a real reach-semantics
        // regression in production (sqrMagnitude↔magnitude, ≤↔<, planar↔3D, reading a hardcoded range instead
        // of the weapon attribute, resolving dead/self targets) left it GREEN. Same instrument-of-record class
        // as [[soak-fail-test-pass-instrument-runtime]] and the ClickGateDiagnosticTests accessor-vs-resolver
        // guard. The assertion now runs through MeleeAttack.ClickGateDiag() — the PUBLIC cold-path read whose
        // HasTarget is literally `ResolveNearestTarget(SelectedWeapon.Reach) != null`, i.e. the SAME resolver
        // the live left-click consumes (MeleeAttack.cs Update → ResolveNearestTarget(weapon.Reach)). The AC7
        // intent is unchanged: the spear's reach ATTRIBUTE lands a hit where the axe's does not.
        [Test]
        public void SpearReach_LandsAtADistance_TheAxeReachDoesNot_ThroughTheRealMeleeAttackSeam()
        {
            var cat = ScriptableObject.CreateInstance<WeaponCatalog>();
            cat.BuildDefaults();
            var axe = cat.ById(WeaponCatalog.AxeId);
            var spear = cat.ById(WeaponCatalog.SpearId);
            Assert.Greater(spear.Reach, axe.Reach, "the spear reaches FURTHER than the axe (AC4 attribute contrast)");

            // A distance strictly between the two reaches — the reach ATTRIBUTE alone decides the hit: here a
            // spear lands and an axe whiffs, so the charging boar is hit by the spear FIRST (AC3).
            float midDist = (axe.Reach + spear.Reach) * 0.5f; // 2.8 for 2.0 / 3.6

            // Park the rig FAR from the world origin. ResolveNearestTarget does a GLOBAL FindObjectsOfType<Health>,
            // so a Boot.unity left open by another EditMode fixture (BoarSceneTests / ClickGateDiagnosticTests
            // both OpenScene it) would otherwise put foreign Healths in the running — the process-global
            // cross-fixture bleed class (unity-conventions.md §Headless, the NavMesh.CalculateTriangulation
            // finding). The empty-field baseline below is the LIVE proof the isolation held on this run.
            Vector3 origin = new Vector3(9000f, 0f, 9000f);

            var invGo = new GameObject("Inv");
            var playerGo = new GameObject("Player");
            var attackGo = new GameObject("MeleeAttack");
            GameObject boarGo = null;
            try
            {
                // The weapon is chosen through the PRODUCTION selection surface (the selected belt item →
                // MeleeAttack.SelectedWeapon → its WeaponDef), not by handing the resolver a raw float.
                var inv = invGo.AddComponent<Inventory>();
                var axeSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.AxeId));
                var spearSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.SpearId));
                Assert.IsTrue(axeSlot.HasValue && spearSlot.HasValue, "axe + spear both land on the belt");

                playerGo.transform.position = origin;
                var attack = attackGo.AddComponent<MeleeAttack>();
                attack.inventory = inv;            // EditMode has no Awake on AddComponent —
                attack.player = playerGo.transform; // wire exactly what the build's Awake wires.

                // The seam reports the SELECTED weapon's reach attribute — a production path that hardcoded a
                // range instead of reading WeaponDef.Reach reds here.
                inv.Model.SelectBelt(spearSlot.Value.Index);
                Assert.AreEqual(WeaponCatalog.SpearId, attack.ClickGateDiag().WeaponId, "the spear is the selected weapon");
                Assert.AreEqual(spear.Reach, attack.ClickGateDiag().Reach, 1e-4f,
                    "the live gate resolves targets at the SPEAR's reach ATTRIBUTE (not a hardcoded range)");

                // BASELINE / isolation guard — an empty field: even the long-reach spear finds NOTHING. If a
                // foreign Health were parked near this rig, THIS reds loudly instead of silently making the
                // spear-lands assert below pass for the wrong reason.
                Assert.IsFalse(attack.ClickGateDiag().HasTarget,
                    "no enemy anywhere near the rig → the real resolver finds no target (isolation holds)");

                // The boar at the mid-reach distance — the charge distance the AC7 contrast is about.
                boarGo = new GameObject("boar");
                boarGo.transform.position = origin + new Vector3(midDist, 0f, 0f);
                var boarHp = boarGo.AddComponent<Health>();
                boarHp.max = BoarEnemy.BoarMedMaxHp; boarHp.startFull = true;
                boarHp.resistance = BoarEnemy.BoarResistance;

                Assert.IsTrue(attack.ClickGateDiag().HasTarget,
                    "the SPEAR's reach lands the hit at the charge distance — through the real resolver (AC3)");

                inv.Model.SelectBelt(axeSlot.Value.Index);
                Assert.AreEqual(WeaponCatalog.AxeId, attack.ClickGateDiag().WeaponId, "the axe is now the selected weapon");
                Assert.AreEqual(axe.Reach, attack.ClickGateDiag().Reach, 1e-4f, "the gate now resolves at the AXE's reach");
                Assert.IsFalse(attack.ClickGateDiag().HasTarget,
                    "the AXE's reach does NOT — it must let the boar close first (AC3), through the real resolver");

                // …and the axe is not merely broken: walk the boar inside the axe's reach and it lands. This
                // pins the contrast to the REACH ATTRIBUTE rather than to any weapon-specific target filter.
                boarGo.transform.position = origin + new Vector3(axe.Reach * 0.5f, 0f, 0f);
                Assert.IsTrue(attack.ClickGateDiag().HasTarget,
                    "inside its own reach the axe DOES find the boar — the contrast is the reach, not the weapon");

                // Height-robustness: the production metric is PLANAR XZ (the same one ChopTree/CraftSpot use —
                // MeleeAttack.cs ResolveNearestTarget). A regression to a 3D distance would silently shrink
                // every weapon's effective reach on sloped island ground; this reds it.
                boarGo.transform.position = origin + new Vector3(axe.Reach * 0.5f, 40f, 0f);
                Assert.IsTrue(attack.ClickGateDiag().HasTarget,
                    "reach is measured PLANAR (XZ) — a boar 40u above/below is still in reach (height-robust)");

                // A DEAD target is not a target — the resolver skips it (a regression here would let the
                // player keep 'hitting' a corpse and the whiff/land read would lie).
                boarGo.transform.position = origin + new Vector3(axe.Reach * 0.5f, 0f, 0f);
                boarHp.ApplyDamage(BoarEnemy.BoarMedMaxHp * 2f, DamageType.Blunt);
                Assert.IsTrue(boarHp.IsDead, "the boar is dead for the next assert");
                Assert.IsFalse(attack.ClickGateDiag().HasTarget,
                    "a DEAD boar in reach is not a target — the real resolver skips dead Healths");
            }
            finally
            {
                if (boarGo != null) Object.DestroyImmediate(boarGo);
                Object.DestroyImmediate(attackGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(invGo);
                Object.DestroyImmediate(cat);
            }
        }

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
