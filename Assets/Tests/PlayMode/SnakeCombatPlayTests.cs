using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon.Combat;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode guards for the REAL snake loop (ticket 86caaz4vn AC7): the LIVE state machine closes both
    /// ways — (a) player proximity → aggro → telegraph → lunge → BITE damages the player through the shared
    /// seam, tier-scaled; (b) weapon damage kills the snake → death state → DESPAWN.
    ///
    /// Rig discipline: a bare NavMesh-FREE rig (SnakeAI's no-agent transform fallback — the DeathHandler
    /// bare-rig precedent) with a renderer-ENABLED Ground plane for the chain's snap. Headless traps per
    /// procedural-animation-verbs.md: WaitForSeconds ONLY (WaitForEndOfFrame never fires in -batchmode);
    /// phases are Time.time-anchored so they complete even at deltaTime≈0; the proximity placements put
    /// the player INSIDE the trigger radii so no assert depends on locomotion distance covered per frame.
    /// (CI's playmode job is advisory — EditMode carries the deterministic logic; the shipped -verifySnake
    /// capture carries the interaction gate.)
    /// </summary>
    public class SnakeCombatPlayTests
    {
        private GameObject _ground;
        private GameObject _snakeGo;
        private GameObject _playerGo;

        private SnakeAI BuildRig(out Health playerHp, out SnakeEnemy enemy)
        {
            // Renderer-ENABLED ground on the Ground layer (the chain snaps to VISIBLE terrain only).
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "snake-test-ground";
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(60f, 1f, 60f);
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) _ground.layer = groundLayer;

            _playerGo = new GameObject("snake-test-player");
            _playerGo.transform.position = new Vector3(10f, 0f, 0f); // outside aggro (4.5) initially
            playerHp = _playerGo.AddComponent<Health>();            // default 100 max, damageTakenMul 1

            _snakeGo = new GameObject("snake-test-snake");
            _snakeGo.transform.position = Vector3.zero;
            var hp = _snakeGo.AddComponent<Health>();
            hp.max = SnakeEnemy.SnakeMaxHp;
            hp.startFull = true;
            enemy = _snakeGo.AddComponent<SnakeEnemy>();
            var ai = _snakeGo.AddComponent<SnakeAI>();
            ai.player = _playerGo.transform;
            ai.playerHealth = playerHp;
            // Short phases so the loop closes fast under the test clock (Time.time-anchored → real seconds).
            ai.telegraphSeconds = 0.25f;
            ai.lungeSeconds = 0.15f;
            ai.cooldownSeconds = 0.3f;
            ai.despawnSeconds = 0.6f;

            // A minimal 3-segment chain so the pose path runs live too (bare transforms — the chain
            // tolerates segments without MeshFilters via the default plant offset).
            var chain = _snakeGo.AddComponent<SnakeBodyChain>();
            chain.ai = ai;
            var segs = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var seg = new GameObject("seg" + i).transform;
                seg.SetParent(_snakeGo.transform, false);
                seg.localPosition = new Vector3(0f, 0f, -0.14f * i);
                segs[i] = seg;
            }
            chain.segments = segs;
            return ai;
        }

        [TearDown]
        public void TearDown()
        {
            if (_ground != null) Object.Destroy(_ground);
            if (_snakeGo != null) Object.Destroy(_snakeGo);
            if (_playerGo != null) Object.Destroy(_playerGo);
        }

        [UnityTest]
        public IEnumerator ProximityAggro_Telegraph_Lunge_Bite_DamagesPlayer_TierScaled()
        {
            var ai = BuildRig(out Health playerHp, out SnakeEnemy enemy);
            yield return null; // one frame: Awake/OnEnable/first Update

            Assert.AreEqual(SnakeAI.SnakeState.Wander, ai.State, "starts wandering (AC2)");
            float hpBefore = playerHp.Current;

            // Walk-up equivalent: place the player INSIDE the aggro radius (locomotion distance per frame
            // is deltaTime-bound and ≈0 headlessly — the RADIUS check is the live trigger under test).
            _playerGo.transform.position = ai.transform.position + new Vector3(3f, 0f, 0f);
            float deadline = Time.time + 3f;
            while (ai.State != SnakeAI.SnakeState.Chase && Time.time < deadline) yield return null;
            Assert.AreEqual(SnakeAI.SnakeState.Chase, ai.State, "proximity aggros the snake (AC2)");

            // Close INSIDE both the strike range (1.7) AND the bite radius (1.0): headlessly the lunge dash
            // covers ~zero distance (deltaTime≈0), so the bite must be connectable from where the player
            // STANDS — the radius checks are the live triggers under test, not locomotion coverage.
            _playerGo.transform.position = ai.transform.position + new Vector3(0.9f, 0f, 0f);
            deadline = Time.time + 3f;
            while (ai.State != SnakeAI.SnakeState.Telegraph && Time.time < deadline) yield return null;
            Assert.AreEqual(SnakeAI.SnakeState.Telegraph, ai.State, "in strike range the tell starts (AC3)");

            // The anchored phase advances in REAL time (headless-safe — never a deltaTime accumulation).
            // Race-robust: a coarse frame can carry the state past Telegraph into Lunge — advanced-OR-fired
            // both prove the anchored phase ran.
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(ai.TelegraphNormT > 0f || ai.LungesFired >= 1,
                "the telegraph phase advances (Time.time-anchored) or has already completed into the lunge");

            deadline = Time.time + 3f;
            while (ai.LungesFired < 1 && Time.time < deadline) yield return null;
            Assert.GreaterOrEqual(ai.LungesFired, 1, "the telegraph completes into a LUNGE (AC3)");

            // The bite lands during the lunge (player inside biteRadius) — tier-scaled through the seam.
            deadline = Time.time + 3f;
            while (ai.BitesLanded < 1 && Time.time < deadline) yield return null;
            Assert.GreaterOrEqual(ai.BitesLanded, 1, "the lunge BITES the in-range player (AC3/AC4)");
            float removed = hpBefore - playerHp.Current;
            Assert.AreEqual(SnakeEnemy.SnakeMedBiteDamage, removed, 0.01f,
                "an unwired-tier (Medium default) bite removes exactly the medium map through the seam (AC4)");
            Assert.AreEqual(ai.LastBiteDamage, removed, 0.01f, "the AI records what the seam removed");
        }

        [UnityTest]
        public IEnumerator WeaponKill_DeathReaction_ThenDespawn()
        {
            var ai = BuildRig(out Health playerHp, out SnakeEnemy enemy);
            yield return null;

            var snakeHp = enemy.Health;
            // Two axe-class hits through the SHARED seam (the weapon-vs-snake surface soak-224 validated).
            snakeHp.ApplyDamage(WeaponCatalog.AxeDamage, DamageType.Slash);
            Assert.IsFalse(snakeHp.IsDead, "one axe hit doesn't kill the 24 HP snake");
            snakeHp.ApplyDamage(WeaponCatalog.AxeDamage, DamageType.Slash);
            Assert.IsTrue(snakeHp.IsDead, "two axe hits kill it (AC5)");

            yield return null; // let Update fold the death in (event already did — belt and suspenders)
            Assert.AreEqual(SnakeAI.SnakeState.Dead, ai.State, "death reaction state (AC5)");

            // A dead snake never bites (the seam no-ops + the AI is in Dead).
            float hpBefore = playerHp.Current;
            _playerGo.transform.position = ai.transform.position + new Vector3(0.5f, 0f, 0f);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(hpBefore, playerHp.Current, 1e-3f, "a corpse doesn't bite");

            // DESPAWN after the (shortened) settle window (AC5).
            float deadline = Time.time + 3f;
            while (_snakeGo.activeInHierarchy && Time.time < deadline) yield return null;
            Assert.IsFalse(_snakeGo.activeInHierarchy, "the dead snake despawns (AC5)");
        }

        [UnityTest]
        public IEnumerator PlayerDeath_DisengagesTheSnake()
        {
            var ai = BuildRig(out Health playerHp, out SnakeEnemy enemy);
            yield return null;

            _playerGo.transform.position = ai.transform.position + new Vector3(3f, 0f, 0f);
            float deadline = Time.time + 3f;
            while (ai.State != SnakeAI.SnakeState.Chase && Time.time < deadline) yield return null;
            Assert.AreEqual(SnakeAI.SnakeState.Chase, ai.State);

            // Kill the player mid-chase → the snake must break off (never maul a corpse — the enemy
            // disengage the DeathHandler docstring assigns to this ticket).
            playerHp.ApplyDamage(9999f, DamageType.Pierce);
            Assert.IsTrue(playerHp.IsDead);
            deadline = Time.time + 3f;
            while (ai.State == SnakeAI.SnakeState.Chase && Time.time < deadline) yield return null;
            Assert.AreEqual(SnakeAI.SnakeState.Wander, ai.State, "a dead player is disengaged (AC2)");
        }
    }
}
