using NUnit.Framework;
using UnityEngine;
using FarHorizon.Combat;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the wild boar's AI + per-tier data + meshes (ticket 86cah7ydt AC2/AC5/AC6/AC7) —
    /// the pure ShouldChopOnClick-idiom charge truth tables, the per-tier HP + gore mapping through the shared
    /// surface, the death transition, and the boar meshes' outward winding + warm-brown colour (the Cull-Back
    /// + anti-bush-green classes — lowpoly-quality §1). Mirrors SnakeAITests.
    /// </summary>
    public class BoarAITests
    {
        // ============================ AC2 — the aggro/chase/charge truth tables ============================

        [Test]
        public void ShouldAggro_TruthTable()
        {
            Assert.IsTrue(BoarAI.ShouldAggro(4f, 5.5f, playerAlive: true, selfAlive: true));
            Assert.IsFalse(BoarAI.ShouldAggro(6f, 5.5f, true, true));
            Assert.IsFalse(BoarAI.ShouldAggro(4f, 5.5f, playerAlive: false, selfAlive: true), "dead player never aggroed");
            Assert.IsFalse(BoarAI.ShouldAggro(4f, 5.5f, true, selfAlive: false), "a dead boar aggros nothing");
            Assert.IsTrue(BoarAI.ShouldAggro(5.5f, 5.5f, true, true), "boundary is inclusive (walk up must trigger)");
        }

        [Test]
        public void ShouldGiveUpChase_TruthTable()
        {
            Assert.IsFalse(BoarAI.ShouldGiveUpChase(6f, 11f, 5f, 15f, 2f, 7f, playerAlive: true), "in range, leashed, fresh");
            Assert.IsTrue(BoarAI.ShouldGiveUpChase(11.5f, 11f, 5f, 15f, 2f, 7f, true), "escaped past de-aggro");
            Assert.IsTrue(BoarAI.ShouldGiveUpChase(6f, 11f, 15.5f, 15f, 2f, 7f, true), "dragged past the leash");
            Assert.IsTrue(BoarAI.ShouldGiveUpChase(6f, 11f, 5f, 15f, 7.1f, 7f, true), "chase timer expired");
            Assert.IsTrue(BoarAI.ShouldGiveUpChase(6f, 11f, 5f, 15f, 2f, 7f, playerAlive: false), "player died → disengage");
        }

        [Test]
        public void ShouldCharge_And_GoreConnects_TruthTables()
        {
            // The charge starts at chargeRange (4.5) — DELIBERATELY beyond a spear's reach (3.6) so the rush
            // crosses the spear band before it can gore (the AC3 reach story is baked into the range gap).
            Assert.IsTrue(BoarAI.ShouldCharge(4.0f, 4.5f, playerAlive: true));
            Assert.IsFalse(BoarAI.ShouldCharge(5.0f, 4.5f, true));
            Assert.IsFalse(BoarAI.ShouldCharge(4.0f, 4.5f, playerAlive: false));

            Assert.IsTrue(BoarAI.GoreConnects(1.0f, 1.2f, alreadyGoredThisCharge: false));
            Assert.IsFalse(BoarAI.GoreConnects(1.0f, 1.2f, alreadyGoredThisCharge: true), "one gore per charge");
            Assert.IsFalse(BoarAI.GoreConnects(1.5f, 1.2f, false), "dodged out of gore radius → whiff (dodgeable)");
        }

        [Test]
        public void ShouldRepath_TruthTable()
        {
            Assert.IsTrue(BoarAI.ShouldRepath(0.6f, 0.0f, 0.5f, 0.2f), "drifted past move threshold");
            Assert.IsTrue(BoarAI.ShouldRepath(0.0f, 0.25f, 0.5f, 0.2f), "staleness interval elapsed");
            Assert.IsFalse(BoarAI.ShouldRepath(0.3f, 0.1f, 0.5f, 0.2f), "neither → HOLD the path (the throttle)");
            Assert.IsTrue(BoarAI.ShouldRepath(0f, float.PositiveInfinity, 0.5f, 0.2f), "intent-change reset always repaths");
        }

        // ==================== AC6 — per-tier HP + gore (gentle easy < threatening hard) ====================

        [Test]
        public void BoarEnemy_ApplyDifficulty_ScalesHpAndGore_GentleEasy_ThreateningHard()
        {
            var go = new GameObject("boar-tier-rig");
            try
            {
                var hp = go.AddComponent<Health>();
                var enemy = go.AddComponent<BoarEnemy>();

                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
                float easyGore = enemy.goreDamage; float easyHp = hp.max;
                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Medium);
                float medGore = enemy.goreDamage; float medHp = hp.max;
                enemy.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
                float hardGore = enemy.goreDamage; float hardHp = hp.max;

                Assert.AreEqual(BoarEnemy.BoarEasyGoreDamage, easyGore, 1e-4f);
                Assert.AreEqual(BoarEnemy.BoarHardGoreDamage, hardGore, 1e-4f);
                Assert.Less(easyGore, medGore, "easy gore GENTLER than medium (AC6)");
                Assert.Less(medGore, hardGore, "hard gore MORE THREATENING than medium (AC6)");

                Assert.AreEqual(BoarEnemy.BoarEasyMaxHp, easyHp, 1e-4f, "easy HP = the easy map (AC6)");
                Assert.AreEqual(BoarEnemy.BoarHardMaxHp, hardHp, 1e-4f, "hard HP = the hard map (AC6)");
                Assert.Less(easyHp, medHp, "easy boar frailer than medium");
                Assert.Less(medHp, hardHp, "hard boar tougher than medium");
                // A boar is TOUGHER than the snake (24) — the matchup-proof enemy is a real fight.
                Assert.Greater(medHp, SnakeEnemy.SnakeMaxHp, "the boar out-HPs the snake (a tougher 2nd enemy)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BoarEnemy_AuthorsWeakToPierceAndSlashResist()
        {
            var r = BoarEnemy.BoarResistance;
            Assert.Greater(r.Multiplier(DamageType.Pierce), 1f, "weak to PIERCE (spear amplified — AC1/AC3)");
            Assert.Less(r.Multiplier(DamageType.Slash), 1f, "RESISTANT to slash (axe worse, not blocked — AC3)");
            Assert.AreEqual(1f, r.Multiplier(DamageType.Blunt), 1e-4f, "blunt neutral");
            // The pierce weakness reads MORE than the snake's — the boar is THE matchup-proof enemy.
            Assert.Greater(r.Multiplier(DamageType.Pierce), SnakeEnemy.SnakePierceWeakness,
                "the boar's pierce weakness reads at least as strongly as the snake's (matchup legibility)");
        }

        // ==================== AC5 — death transition (weapon-vs-boar through the shared seam) ====================

        [Test]
        public void BoarAI_EntersDead_WhenHealthDies()
        {
            var go = new GameObject("boar-death-rig");
            try
            {
                var hp = go.AddComponent<Health>();
                hp.max = BoarEnemy.BoarMedMaxHp; hp.startFull = true;
                hp.resistance = BoarEnemy.BoarResistance;
                go.AddComponent<BoarEnemy>();
                var ai = go.AddComponent<BoarAI>();

                Assert.IsFalse(ai.SyncDeathState(), "a full-HP boar is not dead");
                // Kill it with spears (the matchup weapon) — 9 × 2.0 = 18/hit; 40 HP → 3 hits.
                hp.ApplyDamage(WeaponCatalog.SpearDamage, DamageType.Pierce);
                hp.ApplyDamage(WeaponCatalog.SpearDamage, DamageType.Pierce);
                Assert.IsFalse(hp.IsDead, "two spear hits (36) must not fell the 40 HP boar");
                hp.ApplyDamage(WeaponCatalog.SpearDamage, DamageType.Pierce);
                Assert.IsTrue(hp.IsDead, "three spear hits (54) kill it");
                Assert.IsTrue(ai.SyncDeathState(), "the kill folds into the state machine");
                Assert.AreEqual(BoarAI.BoarState.Dead, ai.State, "a dead Health drives the AI into Dead (AC5)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ==================== AC5 — boar meshes: outward winding + warm-brown colour ====================

        [Test]
        public void BoarMeshes_AllFacesPointOutward_NeverCullBackInvisible()
        {
            AssertOutward(LowPolyMeshes.BoarBody(1.1f, 0.28f, new Color(0.42f, 0.32f, 0.22f), 74110), "BoarBody");
            AssertOutward(LowPolyMeshes.BoarHead(0.22f, 0.42f, new Color(0.42f, 0.32f, 0.22f),
                          new Color(0.52f, 0.42f, 0.34f), new Color(0.90f, 0.88f, 0.78f),
                          new Color(0.06f, 0.05f, 0.04f), 74111), "BoarHead");
            AssertOutward(LowPolyMeshes.BoarLeg(0.075f, 0.05f, 0.44f, new Color(0.36f, 0.27f, 0.19f),
                          new Color(0.14f, 0.11f, 0.09f), 74112), "BoarLeg");
            AssertOutward(LowPolyMeshes.BoarTail(0.22f, 0.03f, new Color(0.42f, 0.32f, 0.22f), 74116), "BoarTail");
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
                if (wound.sqrMagnitude < 1e-12f) { bad++; continue; }
                if (Vector3.Dot(wound.normalized, normals[tris[t]]) < 0.99f) bad++;
            }
            Assert.AreEqual(0, bad, name + ": every face's winding must MATCH its stored outward normal " +
                "(mismatch = the Cull-Back invisible class)");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BoarBody_CarriesWarmBrownVertexColour_NeverBushGreen()
        {
            var body = LowPolyMeshes.BoarBody(1.1f, 0.28f, new Color(0.42f, 0.32f, 0.22f), 74110);
            try
            {
                var cols = body.colors;
                Assert.Greater(cols.Length, 0, "the boar body bakes vertex colours");
                float r = 0, g = 0, b = 0;
                foreach (var c in cols) { r += c.r; g += c.g; b += c.b; }
                r /= cols.Length; g /= cols.Length; b /= cols.Length;
                Assert.Greater(r, g, "the boar body reads WARM (R>G) — never a green bush-blob (the #224 class)");
                Assert.Greater(g, b, "a warm brown ramp (G>B)");
            }
            finally { Object.DestroyImmediate(body); }
        }
    }
}
