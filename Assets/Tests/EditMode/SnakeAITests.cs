using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FarHorizon.Combat;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the REAL snake (ticket 86caaz4vn AC2/AC3/AC4/AC5/AC7): the AI state-machine
    /// truth tables (the pure ShouldChopOnClick-idiom predicates), the per-tier bite-damage mapping through
    /// the SHARED Health.ApplyDamage seam, the body-chain math (trail spacing + slither bounds), the death
    /// transition, and the serpent meshes' outward winding (the Cull-Back class — lowpoly-quality §1).
    ///
    /// Regression guards (the bug CLASS, per test):
    ///  - Truth tables: an unfindable/unresponsive snake (aggro or strike never firing = the #224
    ///    "bite not live-triggerable" UNSOAKABLE class) fails here first.
    ///  - Tier mapping: a difficulty regression (easy ≥ hard) fails the ordering assert.
    ///  - Winding: an inward-wound serpent face (invisible/culled snake — unfindable) fails the face scan.
    ///  - Chain math: broken trail-following (body detaching from the head / segments piling up) fails
    ///    the monotonic-spacing assert with no scene.
    /// </summary>
    public class SnakeAITests
    {
        // ============================ AC2 — the aggro/chase truth tables ============================

        [Test]
        public void ShouldAggro_TruthTable()
        {
            // In radius + both alive → aggro.
            Assert.IsTrue(SnakeAI.ShouldAggro(3f, 4.5f, playerAlive: true, selfAlive: true));
            // Out of radius → no.
            Assert.IsFalse(SnakeAI.ShouldAggro(6f, 4.5f, true, true));
            // Dead player is never aggroed (the enemy-disengage the DeathHandler docstring assigns here).
            Assert.IsFalse(SnakeAI.ShouldAggro(3f, 4.5f, playerAlive: false, selfAlive: true));
            // A dead snake aggros nothing.
            Assert.IsFalse(SnakeAI.ShouldAggro(3f, 4.5f, true, selfAlive: false));
            // Boundary: exactly at the radius counts as in (<=) — walking up to it must trigger (AC6).
            Assert.IsTrue(SnakeAI.ShouldAggro(4.5f, 4.5f, true, true));
        }

        [Test]
        public void ShouldGiveUpChase_TruthTable()
        {
            // Mid-chase, in range, leashed, fresh → keep chasing.
            Assert.IsFalse(SnakeAI.ShouldGiveUpChase(5f, 9f, 4f, 12f, 2f, 6f, playerAlive: true));
            // Player escaped past the de-aggro radius → give up.
            Assert.IsTrue(SnakeAI.ShouldGiveUpChase(9.5f, 9f, 4f, 12f, 2f, 6f, true));
            // Dragged past the leash → give up (chases BRIEFLY, stays near home — AC2).
            Assert.IsTrue(SnakeAI.ShouldGiveUpChase(5f, 9f, 12.5f, 12f, 2f, 6f, true));
            // Chase timer expired → give up.
            Assert.IsTrue(SnakeAI.ShouldGiveUpChase(5f, 9f, 4f, 12f, 6.1f, 6f, true));
            // Player died mid-chase → disengage.
            Assert.IsTrue(SnakeAI.ShouldGiveUpChase(5f, 9f, 4f, 12f, 2f, 6f, playerAlive: false));
        }

        [Test]
        public void ShouldStrike_And_BiteConnects_TruthTables()
        {
            Assert.IsTrue(SnakeAI.ShouldStrike(1.5f, 1.7f, playerAlive: true));
            Assert.IsFalse(SnakeAI.ShouldStrike(2.0f, 1.7f, true));
            Assert.IsFalse(SnakeAI.ShouldStrike(1.5f, 1.7f, playerAlive: false));

            // One bite per lunge: connects in radius, never twice.
            Assert.IsTrue(SnakeAI.BiteConnects(0.8f, 1.0f, alreadyBitThisLunge: false));
            Assert.IsFalse(SnakeAI.BiteConnects(0.8f, 1.0f, alreadyBitThisLunge: true));
            // Dodged out of the bite radius → whiff (the telegraph makes it dodgeable — AC3).
            Assert.IsFalse(SnakeAI.BiteConnects(1.3f, 1.0f, false));
        }

        // ==================== 86cahzycp NIT 1 — the SetDestination repath throttle ====================

        [Test]
        public void ShouldRepath_TruthTable()
        {
            // Destination drifted past the move threshold → repath (the chase keeps tracking the player).
            Assert.IsTrue(SnakeAI.ShouldRepath(0.6f, 0.0f, 0.5f, 0.2f));
            // Staleness interval elapsed → repath (bounds drift below the move threshold).
            Assert.IsTrue(SnakeAI.ShouldRepath(0.0f, 0.25f, 0.5f, 0.2f));
            // Neither moved nor stale → HOLD the current path (the throttle — the NIT's point).
            Assert.IsFalse(SnakeAI.ShouldRepath(0.3f, 0.1f, 0.5f, 0.2f));
            // Boundaries are inclusive (a repath at exactly the threshold, never a dead band).
            Assert.IsTrue(SnakeAI.ShouldRepath(0.5f, 0f, 0.5f, 0.2f));
            Assert.IsTrue(SnakeAI.ShouldRepath(0f, 0.2f, 0.5f, 0.2f));
            // An intent-change reset (secondsSinceLast = +inf) always repaths — transitions stay
            // frame-identical to the unthrottled behavior (the feel-neutrality contract).
            Assert.IsTrue(SnakeAI.ShouldRepath(0f, float.PositiveInfinity, 0.5f, 0.2f));
        }

        [Test]
        public void RepathThrottle_StationaryTarget_BoundsPathRequestsPerSecond()
        {
            // The regression guard for the bug CLASS (SetDestination issued EVERY frame): simulate 1 s of
            // 60 fps chase toward a STATIONARY destination, mirroring MoveTowards' exact bookkeeping
            // (last-issued dest + last-issued time). Unthrottled this is 60 path requests; the throttle
            // must bound it to the initial repath + interval-driven refreshes (1 + 1s/0.2s = ~6).
            const float minMove = 0.5f, interval = 0.2f;
            Vector3 dest = new Vector3(3f, 0f, 4f); // fixed — the target never moves
            Vector3 lastDest = Vector3.zero;
            float lastAt = float.NegativeInfinity;  // the intent-change reset state MoveTowards starts from
            int repaths = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                float now = frame / 60f;
                float movedXz = Vector2.Distance(new Vector2(dest.x, dest.z), new Vector2(lastDest.x, lastDest.z));
                if (SnakeAI.ShouldRepath(movedXz, now - lastAt, minMove, interval))
                {
                    repaths++;
                    lastDest = dest;
                    lastAt = now;
                }
            }
            Assert.GreaterOrEqual(repaths, 1, "the first frame after an intent change must always repath");
            Assert.LessOrEqual(repaths, 6,
                "a stationary target must cost at most 1 + (1s / interval) path requests per second, " +
                "never one per frame (the 86cahzycp NIT-1 class)");
        }

        // ==================== AC4 — MODERATE, difficulty-scaled bite (the tier map) ====================

        [Test]
        public void SnakeEnemy_ApplyDifficulty_ScalesBite_GentleEasy_ThreateningHard()
        {
            var go = new GameObject("snake-tier-rig");
            try
            {
                go.AddComponent<Health>();
                var enemy = go.AddComponent<SnakeEnemy>();

                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
                float easy = enemy.biteDamage;
                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Medium);
                float med = enemy.biteDamage;
                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
                float hard = enemy.biteDamage;

                Assert.AreEqual(SnakeEnemy.SnakeEasyBiteDamage, easy, 1e-4f);
                Assert.AreEqual(SnakeEnemy.SnakeMedBiteDamage, med, 1e-4f);
                Assert.AreEqual(SnakeEnemy.SnakeHardBiteDamage, hard, 1e-4f);
                Assert.Less(easy, med, "easy bite must be GENTLER than medium (AC4)");
                Assert.Less(med, hard, "hard bite must be MORE THREATENING than medium (AC4)");
                // MODERATE: a medium bite is a real chunk but never one-shot territory (vs the 100 default HP).
                Assert.That(med, Is.InRange(5f, 25f), "a medium bite is a MODERATE chunk of the 100 HP bar");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SnakeBite_TierScaled_ThroughSharedSeam()
        {
            var snakeGo = new GameObject("snake-bite-rig");
            var playerGo = new GameObject("player-bite-rig");
            try
            {
                snakeGo.AddComponent<Health>();
                var enemy = snakeGo.AddComponent<SnakeEnemy>();
                var playerHp = playerGo.AddComponent<Health>(); // default 100 max, neutral resistance

                // The SAME bite on each tier — through the ONE ApplyDamage seam (AC4: the tier map is on
                // the snake; the player's damageTakenMul stays 1 in this rig so the removed HP IS the map).
                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
                float removedEasy = enemy.Bite(playerHp);
                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
                float removedHard = enemy.Bite(playerHp);

                Assert.AreEqual(SnakeEnemy.SnakeEasyBiteDamage, removedEasy, 1e-3f,
                    "an easy-tier bite removes exactly the easy map value through the seam");
                Assert.AreEqual(SnakeEnemy.SnakeHardBiteDamage, removedHard, 1e-3f,
                    "a hard-tier bite removes exactly the hard map value through the seam");
                Assert.Greater(removedHard, removedEasy, "hard > easy through the LIVE seam (AC4)");
            }
            finally
            {
                Object.DestroyImmediate(snakeGo);
                Object.DestroyImmediate(playerGo);
            }
        }

        // ==================== AC5 — death + despawn transition (weapon-vs-snake) ====================

        [Test]
        public void SnakeAI_EntersDead_WhenHealthDies()
        {
            var snakeGo = new GameObject("snake-death-rig");
            try
            {
                var hp = snakeGo.AddComponent<Health>();
                hp.max = SnakeEnemy.SnakeMaxHp;
                hp.startFull = true;
                snakeGo.AddComponent<SnakeEnemy>();
                var ai = snakeGo.AddComponent<SnakeAI>();
                // EditMode has NO component lifecycle (no OnEnable event-subscribe, no Update) — drive the
                // SAME death-transition path Update runs via the public SyncDeathState seam.
                Assert.IsFalse(ai.SyncDeathState(), "a full-HP snake is not dead");
                float removed1 = hp.ApplyDamage(WeaponCatalog.AxeDamage, DamageType.Slash); // 14
                Assert.Greater(removed1, 0f);
                Assert.IsFalse(hp.IsDead, "one axe hit (14) must not kill the 24 HP snake");
                hp.ApplyDamage(WeaponCatalog.AxeDamage, DamageType.Slash); // 28 total -> dead
                Assert.IsTrue(hp.IsDead, "two axe hits kill the snake (24 HP)");
                Assert.IsTrue(ai.SyncDeathState(), "the kill folds into the state machine");
                Assert.AreEqual(SnakeAI.SnakeState.Dead, ai.State,
                    "a dead Health must drive the AI into Dead (the death reaction + despawn state — AC5)");
            }
            finally { Object.DestroyImmediate(snakeGo); }
        }

        // ==================== AC1/AC3 — the body-chain math (pure, no scene) ====================

        [Test]
        public void PointAlongTrail_MonotonicSpacing_AlongAStraightTrail()
        {
            // A straight newest-first trail heading +X (head at x=10, older samples behind).
            var trail = new List<Vector3>();
            for (int i = 0; i <= 40; i++) trail.Add(new Vector3(10f - i * 0.25f, 0f, 0f));

            float spacing = 0.14f;
            float prevX = float.MaxValue;
            for (int i = 0; i < 13; i++)
            {
                Vector3 p = SnakeBodyChain.PointAlongTrail(trail, spacing * i, Vector3.left, out Vector3 tangent);
                Assert.Less(p.x, prevX + 1e-4f, "segments must lie strictly BEHIND the previous (no pile-up)");
                if (i > 0)
                    Assert.AreEqual(spacing, prevX - p.x, 1e-3f, "consecutive segments keep the arc spacing");
                Assert.Greater(Vector3.Dot(tangent, Vector3.right), 0.99f, "tangent points along travel (+X)");
                prevX = p.x;
            }
        }

        [Test]
        public void PointAlongTrail_ExtrapolatesPastAShortTrail()
        {
            // A freshly-spawned snake has almost no trail — the tail must still lay out straight behind.
            var trail = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(-0.1f, 0f, 0f) };
            Vector3 p = SnakeBodyChain.PointAlongTrail(trail, 1.0f, Vector3.left, out _);
            Assert.AreEqual(-1.0f, p.x, 1e-3f, "past the oldest sample the chain extrapolates straight back");
        }

        [Test]
        public void SlitherOffset_Bounded_And_QuietWhenStationary()
        {
            for (float t = 0f; t < 3f; t += 0.05f)
            {
                for (int seg = 0; seg < 13; seg++)
                {
                    float moving = SnakeBodyChain.SlitherOffset(t, seg, 1.6f, 1.1f, 0.055f, 0.012f, 1f);
                    float still = SnakeBodyChain.SlitherOffset(t, seg, 1.6f, 1.1f, 0.055f, 0.012f, 0f);
                    Assert.LessOrEqual(Mathf.Abs(moving), 0.055f + 1e-4f, "crawl slither stays in amplitude");
                    Assert.LessOrEqual(Mathf.Abs(still), 0.012f + 1e-4f,
                        "a stationary snake only carries the tiny alive-sway (no full-speed wave at rest)");
                }
            }
        }

        // ==================== AC1 — serpent meshes: outward winding + banded warm colour ====================

        [Test]
        public void SnakeMeshes_AllFacesPointOutward_NeverCullBackInvisible()
        {
            // The Cull-Back class (lowpoly-quality §1): an inward-wound face is culled from every camera —
            // an invisible snake is the UNFINDABLE failure. Scan every face of both generators.
            AssertOutward(LowPolyMeshes.SnakeLink(0.115f, 0.10f, 0.16f, new Color(0.78f, 0.38f, 0.16f), 61410),
                          "SnakeLink");
            AssertOutward(LowPolyMeshes.SnakeHead(0.115f, 0.26f, new Color(0.85f, 0.45f, 0.18f),
                          new Color(0.06f, 0.05f, 0.04f), 61404), "SnakeHead");
        }

        private static void AssertOutward(Mesh mesh, string name)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var normals = mesh.normals;
            Assert.Greater(tris.Length, 0, name + " emits faces");
            int bad = 0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 v0 = verts[tris[t]], v1 = verts[tris[t + 1]], v2 = verts[tris[t + 2]];
                Vector3 wound = Vector3.Cross(v1 - v0, v2 - v0);
                if (wound.sqrMagnitude < 1e-12f) { bad++; continue; }        // degenerate face = a defect too
                // The stored normal must match the WINDING (URP culls by winding, not the normal attribute —
                // the water-grid lesson: a normal-only guard is a proxy a culled mesh satisfies).
                if (Vector3.Dot(wound.normalized, normals[tris[t]]) < 0.99f) bad++;
            }
            Assert.AreEqual(0, bad, name + ": every face's winding must MATCH its stored outward normal " +
                "(mismatch = the Cull-Back invisible class)");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void SnakeMeshes_CarryWarmBandedVertexColour()
        {
            // The findability half that IS machine-checkable: the serpent's baked vertex colours are WARM
            // (R > G > B — rust/brown) so it can never silently regress to a green bush-blob lookalike
            // (the #224 UNSOAKABLE class). Subjective contrast stays with the capture + the Sponsor soak.
            var rust = LowPolyMeshes.SnakeLink(0.115f, 0.10f, 0.16f, new Color(0.78f, 0.38f, 0.16f), 61410);
            var dark = LowPolyMeshes.SnakeLink(0.10f, 0.09f, 0.16f, new Color(0.34f, 0.18f, 0.10f), 61411);
            try
            {
                Color meanRust = MeanColor(rust);
                Color meanDark = MeanColor(dark);
                Assert.Greater(meanRust.r, meanRust.g, "rust band reads WARM (R>G), never bush-green");
                Assert.Greater(meanRust.g, meanRust.b, "rust band is a warm ramp (G>B)");
                Assert.Greater(meanDark.r, meanDark.g, "dark band reads WARM (R>G), never bush-green");
                // BANDED: the two alternating bands are visibly distinct (the banding contrast).
                Assert.Greater(meanRust.r - meanDark.r, 0.15f,
                    "the two bands differ enough to read as BANDS at gameplay framing");
            }
            finally
            {
                Object.DestroyImmediate(rust);
                Object.DestroyImmediate(dark);
            }
        }

        private static Color MeanColor(Mesh m)
        {
            var cols = m.colors;
            Assert.Greater(cols.Length, 0, m.name + " bakes vertex colours");
            float r = 0, g = 0, b = 0;
            foreach (var c in cols) { r += c.r; g += c.g; b += c.b; }
            return new Color(r / cols.Length, g / cols.Length, b / cols.Length);
        }
    }
}
