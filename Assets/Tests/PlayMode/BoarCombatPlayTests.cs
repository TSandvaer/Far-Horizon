using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon.Combat;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode guards for the live boar loop (ticket 86cah7ydt AC2/AC4/AC5): the LIVE state machine closes
    /// both ways — (a) player proximity → aggro → WINDUP → CHARGE → GORE damages the player through the shared
    /// seam, tier-scaled + a bleed applied (AC4); (b) weapon damage kills the boar → death state → DESPAWN;
    /// (c) a dead player disengages the boar. Mirrors SnakeCombatPlayTests.
    ///
    /// Rig discipline: a NavMesh-FREE rig (BoarAI's no-agent transform fallback) with a renderer-ENABLED Ground
    /// plane for the rig's snap. Headless traps per procedural-animation-verbs.md: WaitForSeconds ONLY;
    /// Time.time-anchored phases complete at deltaTime≈0; proximity placements put the player INSIDE the trigger
    /// radii so no assert depends on locomotion distance covered per frame. (CI's playmode job is advisory —
    /// EditMode carries the deterministic logic; the shipped -verifyBoar capture carries the interaction gate.)
    /// </summary>
    public class BoarCombatPlayTests
    {
        private GameObject _ground;
        private GameObject _boarGo;
        private GameObject _playerGo;

        private BoarAI BuildRig(out Health playerHp, out BoarEnemy enemy)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "boar-test-ground";
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(60f, 1f, 60f);
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) _ground.layer = groundLayer;

            _playerGo = new GameObject("boar-test-player");
            _playerGo.transform.position = new Vector3(12f, 0f, 0f); // outside aggro (5.5) initially
            playerHp = _playerGo.AddComponent<Health>();            // default 100 max, damageTakenMul 1
            _playerGo.AddComponent<StatusEffectController>().health = playerHp; // for the gore→bleed (AC4)

            _boarGo = new GameObject("boar-test-boar");
            _boarGo.transform.position = Vector3.zero;
            var hp = _boarGo.AddComponent<Health>();
            hp.max = BoarEnemy.BoarMedMaxHp; hp.startFull = true;
            hp.resistance = BoarEnemy.BoarResistance;
            enemy = _boarGo.AddComponent<BoarEnemy>();
            var ai = _boarGo.AddComponent<BoarAI>();
            ai.player = _playerGo.transform;
            ai.playerHealth = playerHp;
            // Short phases so the loop closes fast (Time.time-anchored → real seconds).
            ai.windupSeconds = 0.25f;
            ai.chargeSeconds = 0.15f;
            ai.cooldownSeconds = 0.3f;
            ai.despawnSeconds = 0.6f;

            // A minimal 7-part rig so the pose path runs live (bare transforms; the rig tolerates them).
            var rig = _boarGo.AddComponent<BoarBodyRig>();
            rig.ai = ai;
            var parts = new Transform[BoarBodyRig.PartCount];
            for (int i = 0; i < parts.Length; i++)
            {
                var p = new GameObject("part" + i).transform;
                p.SetParent(_boarGo.transform, false);
                parts[i] = p;
            }
            rig.parts = parts;
            return ai;
        }

        [TearDown]
        public void TearDown()
        {
            if (_ground != null) Object.Destroy(_ground);
            if (_boarGo != null) Object.Destroy(_boarGo);
            if (_playerGo != null) Object.Destroy(_playerGo);
        }

        [UnityTest]
        public IEnumerator ProximityAggro_Windup_Charge_Gore_DamagesPlayer_TierScaled_AndBleeds()
        {
            var ai = BuildRig(out Health playerHp, out BoarEnemy enemy);
            yield return null; // Awake/OnEnable/first Update

            Assert.AreEqual(BoarAI.BoarState.Wander, ai.State, "starts wandering (AC2)");
            float hpBefore = playerHp.Current;
            var fx = _playerGo.GetComponent<StatusEffectController>();

            // Walk-up equivalent: place the player INSIDE the aggro radius.
            _playerGo.transform.position = ai.transform.position + new Vector3(4f, 0f, 0f);
            float deadline = Time.time + 3f;
            while (ai.State != BoarAI.BoarState.Chase && Time.time < deadline) yield return null;
            Assert.AreEqual(BoarAI.BoarState.Chase, ai.State, "proximity aggros the boar (AC2)");

            // Close INSIDE both chargeRange (4.5) AND goreRadius (1.2): headlessly the charge covers ~0 distance
            // (deltaTime≈0), so the gore must be connectable from where the player STANDS.
            _playerGo.transform.position = ai.transform.position + new Vector3(1.0f, 0f, 0f);
            deadline = Time.time + 3f;
            while (ai.State != BoarAI.BoarState.Windup && Time.time < deadline) yield return null;
            Assert.AreEqual(BoarAI.BoarState.Windup, ai.State, "in charge range the windup tell starts (AC2)");

            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(ai.WindupNormT > 0f || ai.ChargesFired >= 1,
                "the windup phase advances (Time.time-anchored) or has already completed into the charge");

            deadline = Time.time + 3f;
            while (ai.ChargesFired < 1 && Time.time < deadline) yield return null;
            Assert.GreaterOrEqual(ai.ChargesFired, 1, "the windup completes into a CHARGE (AC2)");

            deadline = Time.time + 3f;
            while (ai.GoresLanded < 1 && Time.time < deadline) yield return null;
            Assert.GreaterOrEqual(ai.GoresLanded, 1, "the charge GORES the in-range player (AC2)");

            float removed = hpBefore - playerHp.Current;
            // The gore itself is tier-scaled (Medium default). A bleed may ALSO tick a little between the gore
            // and this read, so assert the gore removed AT LEAST the medium map (never less) + the AI records it.
            Assert.GreaterOrEqual(removed, BoarEnemy.BoarMedGoreDamage - 0.01f,
                "the gore removes at least the medium-tier gore through the shared seam (AC2/AC6)");
            Assert.AreEqual(BoarEnemy.BoarMedGoreDamage, ai.LastGoreDamage, 0.01f,
                "the AI records the pure seam gore (the medium map, unwired tier)");
            Assert.AreEqual(1, fx.ActiveCount, "the gore APPLIED a bleed to the player — the 2nd status consumer (AC4)");
        }

        [UnityTest]
        public IEnumerator WeaponKill_DeathReaction_ThenDespawn()
        {
            var ai = BuildRig(out Health playerHp, out BoarEnemy enemy);
            yield return null;

            var boarHp = enemy.Health;
            // Spear-class hits through the SHARED seam (9 pierce × 2.0 weak = 18/hit; 40 HP → 3 hits).
            boarHp.ApplyDamage(WeaponCatalog.SpearDamage, DamageType.Pierce);
            boarHp.ApplyDamage(WeaponCatalog.SpearDamage, DamageType.Pierce);
            Assert.IsFalse(boarHp.IsDead, "two spear hits don't fell the 40 HP boar");
            boarHp.ApplyDamage(WeaponCatalog.SpearDamage, DamageType.Pierce);
            Assert.IsTrue(boarHp.IsDead, "three spear hits kill it (AC5)");

            yield return null; // let Update fold the death in (the event already did — belt + suspenders)
            Assert.AreEqual(BoarAI.BoarState.Dead, ai.State, "death reaction state (AC5)");

            // A dead boar never gores.
            float hpBefore = playerHp.Current;
            _playerGo.transform.position = ai.transform.position + new Vector3(0.5f, 0f, 0f);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(hpBefore, playerHp.Current, 1e-3f, "a corpse doesn't gore");

            // DESPAWN after the (shortened) settle window (AC5).
            float deadline = Time.time + 3f;
            while (_boarGo.activeInHierarchy && Time.time < deadline) yield return null;
            Assert.IsFalse(_boarGo.activeInHierarchy, "the dead boar despawns (AC5)");
        }

        [UnityTest]
        public IEnumerator PlayerDeath_DisengagesTheBoar()
        {
            var ai = BuildRig(out Health playerHp, out BoarEnemy enemy);
            yield return null;

            _playerGo.transform.position = ai.transform.position + new Vector3(4f, 0f, 0f);
            float deadline = Time.time + 3f;
            while (ai.State != BoarAI.BoarState.Chase && Time.time < deadline) yield return null;
            Assert.AreEqual(BoarAI.BoarState.Chase, ai.State);

            // Kill the player mid-chase → the boar must break off (never charge a corpse).
            playerHp.ApplyDamage(9999f, DamageType.Pierce);
            Assert.IsTrue(playerHp.IsDead);
            deadline = Time.time + 3f;
            while (ai.State == BoarAI.BoarState.Chase && Time.time < deadline) yield return null;
            Assert.AreEqual(BoarAI.BoarState.Wander, ai.State, "a dead player is disengaged (AC2)");
        }
    }
}
