using NUnit.Framework;
using UnityEngine;
using FarHorizon.Combat;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARDS for the shared enemy hit-feedback driver (ticket 86caxjwb3 AC1/AC2/AC3/AC5/AC7).
    ///
    /// Every test here is named against a defect that would otherwise ship SILENTLY:
    ///  • the whole package firing on every HEAL and every SPAWN (Health.Changed carries Current01 and fires on
    ///    damage / heal / RestoreFull / init alike — [DFC-B]);
    ///  • ONE part of a creature flashing instead of all of them (a singular GetComponentInChildren gives
    ///    exactly that: 1 of 7 boar parts, 1 of 13 snake segments);
    ///  • a per-enemy fork creeping into the driver, which is how the NEXT creature ships with half the package;
    ///  • a LINEAR decay ramp, which passes every numeric check and fails quality-bar #2;
    ///  • a flinch cancelling a COMMITTED, telegraphed charge — the Sponsor-PASSED boar feel (soak 2026-07-22);
    ///  • the dead-knob class: a per-tier dial the next ApplyDifficulty silently clobbers.
    ///
    /// ⚠ What is NOT here, deliberately: the flash DECAY. That is the [DFC-1] latch class and it is invisible to
    /// EditMode by construction — see HitFlashShaderTests' class note. It lives in PlayMode + the shipped gate.
    /// </summary>
    public class EnemyHitFeedbackTests
    {
        // Build a synthetic creature: a bare root with Health + the driver + N renderer children, mirroring the
        // shipped topology (root carries NO renderer; every renderer is on a child part). Created INACTIVE-free
        // because EditMode never runs Awake anyway — the driver's lazy EnsureInit is the seam under test.
        private static GameObject MakeCreature(int parts, out EnemyHitFeedback fb, out Health hp)
        {
            var shader = Shader.Find("FarHorizon/LowPolyVertexColor");
            Assert.IsNotNull(shader, "the world shader must resolve (test precondition)");

            var root = new GameObject("SyntheticCreature");
            hp = root.AddComponent<Health>();
            hp.max = 40f;
            hp.startFull = true;
            for (int i = 0; i < parts; i++)
            {
                var child = new GameObject("Part" + i);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 0f, i * 0.1f);
                var mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = new Mesh();
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(shader) { name = "SyntheticPartMat" + i };
            }
            fb = root.AddComponent<EnemyHitFeedback>();
            // EditMode runs NO component lifecycle — without this the driver never subscribes to Health.Changed
            // and every "a hit fires the package" assertion below would be vacuously true. The shipped Awake
            // calls the same seam (the BoarAI.SyncDeathState precedent).
            fb.EnsureReady();
            return root;
        }

        private static void Destroy(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        // PREFS HYGIENE (the FKeyMigrationTests precedent): FloatSettingEntry/IntSettingEntry/BoolSettingEntry
        // .SetValue persists to REAL PlayerPrefs (the Windows registry), so every SetValue in this fixture would
        // otherwise LEAK an fh.settings.* key onto the dev box + the runner and change what a later launch loads.
        [TearDown]
        public void ClearLeakedPrefs()
        {
            foreach (string id in new[] { SettingsCatalog.HitFeedbackEnabledId, SettingsCatalog.HitFlashIntensityId,
                                          SettingsCatalog.HitFlashDurationId, SettingsCatalog.HitFlinchAmplitudeId,
                                          SettingsCatalog.HitFlinchStaggerId, SettingsCatalog.HitPuffCountId })
            {
                PlayerPrefs.DeleteKey("fh.settings." + id);
                PlayerPrefs.DeleteKey("fh.settings." + id + ".def");
            }
        }

        // ---------------------------------------------------------------- AC1: damage ONLY

        [Test]
        public void Heal_RestoreFull_AndInit_DoNotFire_OnlyADamageDeltaDoes()
        {
            // [DFC-B] the previous-value guard is MANDATORY, not a nicety. Health.Changed fires on EVERY change
            // — the init seed, a regen tick, a respawn RestoreFull — so without the guard the flash + flinch +
            // puff fire on every heal and on every spawn.
            var go = MakeCreature(3, out var fb, out var hp);
            try
            {
                // Reading Current forces Health's lazy seed (which itself fires Changed) — the init case.
                Assert.AreEqual(40f, hp.Current, 1e-3f);
                fb.ResetVisuals();
                int baseline = fb.HitCount;

                hp.ApplyDamage(10f, DamageType.Slash);
                Assert.AreEqual(baseline + 1, fb.HitCount, "a DAMAGE delta must fire the package");
                Assert.IsTrue(fb.FlashActive, "…and it ARMS the flash");
                Assert.IsTrue(fb.FlinchActive, "…and the flinch");

                fb.ResetVisuals();
                hp.Heal(5f);
                Assert.AreEqual(baseline + 1, fb.HitCount, "a HEAL must NOT fire the package (a regen tick is not a hit)");
                Assert.IsFalse(fb.FlashActive, "a heal must not arm the flash");

                hp.RestoreFull();
                Assert.AreEqual(baseline + 1, fb.HitCount, "RestoreFull must NOT fire the package (a respawn is not a hit)");
                Assert.IsFalse(fb.FlashActive, "a respawn must not arm the flash");

                hp.ApplyDamage(3f, DamageType.Pierce);
                Assert.AreEqual(baseline + 2, fb.HitCount, "the next damage delta fires again");
                Assert.IsTrue(fb.FlashActive, "…and re-arms the flash");
            }
            finally { Destroy(go); }
        }

        [Test]
        public void RaisingHpMax_DoesNotFire_EvenThoughCurrent01Drops()
        {
            // The reason the guard compares ABSOLUTE Current and not Current01: dragging the dev-console
            // `Boar HP max` slider UP lowers Current01 without any HP being lost. On a Current01 comparison the
            // creature would flash while the Sponsor drags a difficulty dial.
            var go = MakeCreature(2, out var fb, out var hp);
            try
            {
                Assert.AreEqual(40f, hp.Current, 1e-3f);
                int baseline = fb.HitCount;
                float before01 = hp.Current01;
                hp.max = 200f;                       // Current01 falls 1.0 -> 0.2; Current is untouched
                hp.Heal(0.001f);                     // nudge the event to fire at all
                Assert.Less(hp.Current01, before01, "precondition: the normalized value really did drop");
                Assert.AreEqual(baseline, fb.HitCount,
                    "an HP-max dial is NOT a hit — the guard must compare absolute HP, not Current01");
            }
            finally { Destroy(go); }
        }

        // ---------------------------------------------------------------- AC2: the flash write

        [Test]
        public void Flash_WritesEVERY_PartMaterial_NotJustOne()
        {
            // The 1-of-7 defect. GetComponent<Renderer>() on the root returns null and a singular
            // GetComponentInChildren returns exactly one child — a flash on the body but not the head reads as
            // a bug, not as juice. Asserted at BOTH shipped counts.
            foreach (int n in new[] { BoarBodyRig.PartCount, 13 })
            {
                var go = MakeCreature(n, out var fb, out var hp);
                try
                {
                    Assert.IsNull(go.GetComponent<Renderer>(),
                        "precondition: the enemy ROOT carries no renderer (the shipped topology)");
                    Assert.AreEqual(n, fb.MaterialCount,
                        $"the driver must reach ALL {n} part-materials (GetComponentsInChildren, not the singular)");

                    // Drive one frame of the flash by hand (EditMode has no LateUpdate): the amplitude the
                    // runtime would write at the peak.
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial.SetFloat(EnemyHitFeedback.HitFlashProperty, 0.5f);
                    Assert.Greater(fb.MinMaterialFlash(), 0f,
                        "MinMaterialFlash reads the LOWEST across every part — the 'did they ALL light up' read");
                }
                finally { Destroy(go); }
            }
        }

        [Test]
        public void NoMaterialPropertyBlock_AnywhereInTheFeedbackPath()
        {
            // AC2 🔒 + AC7. An MPB breaks BOTH the SRP Batcher and GPU-Resident-Drawer eligibility; distinct
            // material instances break neither. This is a SOURCE assertion because the defect is a call that
            // would still render correctly — it costs FPS silently, which no rendering assertion can see.
            string driver = System.IO.File.ReadAllText(
                "Assets/Scripts/Runtime/Combat/EnemyHitFeedback.cs");
            StringAssert.DoesNotContain("MaterialPropertyBlock", driver,
                "AC2 🔒: the flash is a MATERIAL-INSTANCE write. An MPB here would silently drop both enemies " +
                "out of the SRP batch (unity-conventions.md §SRP-Batcher).");
            StringAssert.DoesNotContain("SetPropertyBlock", driver, "no SetPropertyBlock in the flash path");
        }

        // ---------------------------------------------------------------- AC1: ONE shared path

        [Test]
        public void TheDriver_ContainsNoPerEnemyBranch()
        {
            // AC1 🔒: "No BoarEnemy / SnakeEnemy branches in the feedback code" — a per-enemy fork is how the
            // NEXT creature ships with half the feedback. Source-level because a runtime test on two creatures
            // passes whether or not a third would work.
            string driver = System.IO.File.ReadAllText(
                "Assets/Scripts/Runtime/Combat/EnemyHitFeedback.cs");
            // Strip the XML-doc block comments: the class note legitimately NAMES both types while explaining
            // why neither is branched on. Only executable code is judged.
            var stripped = System.Text.RegularExpressions.Regex.Replace(driver, @"///.*", string.Empty);
            StringAssert.DoesNotContain("BoarEnemy", stripped, "AC1 🔒: no BoarEnemy branch in the driver");
            StringAssert.DoesNotContain("SnakeEnemy", stripped, "AC1 🔒: no SnakeEnemy branch in the driver");
            StringAssert.DoesNotContain("BoarAI", stripped, "AC1 🔒: no BoarAI branch in the driver");
            StringAssert.DoesNotContain("SnakeAI", stripped, "AC1 🔒: no SnakeAI branch in the driver");
            StringAssert.DoesNotContain("BoarBodyRig", stripped, "AC1 🔒: no BoarBodyRig branch in the driver");
            StringAssert.DoesNotContain("SnakeBodyChain", stripped, "AC1 🔒: no SnakeBodyChain branch in the driver");
        }

        [Test]
        public void TheSameDriver_ServesBothShippedCreatureShapes()
        {
            // The runtime half of the same claim: one component, two very different bodies (7 chunky parts vs
            // 13 chained segments), no configuration difference.
            var boarLike = MakeCreature(BoarBodyRig.PartCount, out var boarFb, out var boarHp);
            var snakeLike = MakeCreature(13, out var snakeFb, out var snakeHp);
            try
            {
                boarHp.ApplyDamage(5f, DamageType.Slash);
                snakeHp.ApplyDamage(5f, DamageType.Pierce);
                Assert.AreEqual(1, boarFb.HitCount, "the shared driver reacted on the 7-part body");
                Assert.AreEqual(1, snakeFb.HitCount, "the SAME shared driver reacted on the 13-segment body");
                Assert.AreEqual(BoarBodyRig.PartCount, boarFb.MaterialCount);
                Assert.AreEqual(13, snakeFb.MaterialCount);
            }
            finally { Destroy(boarLike); Destroy(snakeLike); }
        }

        // ---------------------------------------------------------------- AC2 [DFC-2]: the curve

        [Test]
        public void Impulse_StartsAtZero_PeaksAtOne_AndReturnsToExactlyZero()
        {
            Assert.AreEqual(0f, EnemyHitFeedback.Impulse01(0f, EnemyHitFeedback.SnapFraction), 1e-6f,
                "the impulse starts at rest");
            Assert.AreEqual(1f, EnemyHitFeedback.Impulse01(EnemyHitFeedback.SnapFraction, EnemyHitFeedback.SnapFraction), 1e-5f,
                "the impulse reaches full amplitude at the snap point");
            Assert.AreEqual(0f, EnemyHitFeedback.Impulse01(1f, EnemyHitFeedback.SnapFraction), 1e-6f,
                "the impulse RESOLVES to exactly 0 — the recoil resolves, and the flash returns to base colour");
            Assert.AreEqual(0f, EnemyHitFeedback.Impulse01(1.7f, EnemyHitFeedback.SnapFraction), 1e-6f,
                "past the window it stays at 0 (clamped, never wraps)");
        }

        [Test]
        public void Impulse_IsNotLinear_OnEitherHalf()
        {
            // [DFC-2] 🔒: `1 - x` is linear, and quality-bar #2 (motion defaults lively / EASED) is the bar AC1
            // names and AC6(b) claims — a linear ramp fails the bar this ticket is measured against. Both halves
            // are checked: a curve that eases the rise and then decays linearly would half-pass.
            float s = EnemyHitFeedback.SnapFraction;

            // RISE: at a quarter of the way to the peak, an eased rise is BELOW the linear 0.25.
            float quarterUp = EnemyHitFeedback.Impulse01(s * 0.25f, s);
            Assert.Less(quarterUp, 0.25f - 1e-3f,
                "the rise must be EASED (smoothstep), not a linear ramp-on — measured " + quarterUp.ToString("0.0000"));

            // DECAY: halfway through the settle, a quadratic ease-out is BELOW the linear 0.5.
            float halfDown = EnemyHitFeedback.Impulse01(s + (1f - s) * 0.5f, s);
            Assert.Less(halfDown, 0.5f - 1e-3f,
                "the decay must be EASED OUT, not linear — measured " + halfDown.ToString("0.0000"));

            // …and monotonically falling after the peak (no wobble/bounce — the recoil resolves).
            float a = EnemyHitFeedback.Impulse01(s + (1f - s) * 0.3f, s);
            float b = EnemyHitFeedback.Impulse01(s + (1f - s) * 0.6f, s);
            float c = EnemyHitFeedback.Impulse01(s + (1f - s) * 0.9f, s);
            Assert.Greater(a, b, "the settle falls monotonically (no sustained wobble)");
            Assert.Greater(b, c, "the settle falls monotonically (no sustained wobble)");
        }

        // ---------------------------------------------------------------- AC3: the AI contract

        [Test]
        public void HardTier_NeverStaggers_EasyTierDoes()
        {
            // brief §2.5 — at HARD the flinch "interrupts nothing (it keeps coming)"; at EASY it "staggers
            // briefly". Asserted through the SHIPPED ApplyDifficulty path, not by reading the fields raw.
            var go = MakeCreature(3, out var fb, out var hp);
            var tierHost = new GameObject("TierHost");
            var death = tierHost.AddComponent<DeathHandler>();
            fb.deathHandler = death;      // the LIVE tier surface the driver reads on EVERY hit
            try
            {
                Assert.Greater(fb.easyStaggerSeconds, fb.medStaggerSeconds, "easy staggers longer than medium");
                Assert.Greater(fb.medStaggerSeconds, fb.hardStaggerSeconds, "medium staggers longer than hard");

                // HARD — through the REAL hit path (Strike re-reads the live tier), not by poking the field.
                death.tier = SurvivalNeed.DifficultyTier.Hard;
                hp.ApplyDamage(5f, DamageType.Slash);
                Assert.AreEqual(0f, fb.staggerSeconds, 1e-6f, "HARD: zero stagger — the boar keeps coming");
                Assert.IsFalse(fb.IsStaggered, "HARD 🔒 (brief §2.5): a hit must NEVER stagger");

                // EASY — same path, the other end of the tier range.
                death.tier = SurvivalNeed.DifficultyTier.Easy;
                hp.ApplyDamage(5f, DamageType.Slash);
                Assert.Greater(fb.staggerSeconds, 0f, "EASY: a real stagger window");
                Assert.IsTrue(fb.IsStaggered, "EASY (brief §2.5): the creature 'staggers briefly'");
            }
            finally { Destroy(go); Destroy(tierHost); }
        }

        [Test]
        public void TheStagger_TouchesTheAI_InExactlyOnePlace_AndNeverWritesState()
        {
            // AC3 🔒, and the reason it is STRUCTURAL rather than remembered: the stagger gate lives inside
            // MoveTowards, which is reached ONLY from Wander and Chase. Windup and Cooldown call HoldStill,
            // Charge calls ChargeMove, Dead returns early — so a stagger physically CANNOT interrupt a
            // committed, telegraphed charge, whose feel is Sponsor-PASSED (boar soak 2026-07-22).
            foreach (string path in new[] { "Assets/Scripts/Runtime/Combat/BoarAI.cs",
                                            "Assets/Scripts/Runtime/Combat/SnakeAI.cs" })
            {
                string src = System.IO.File.ReadAllText(path);
                int gates = System.Text.RegularExpressions.Regex.Matches(src, @"_feedback\.IsStaggered").Count;
                Assert.AreEqual(1, gates,
                    path + ": the stagger must be consulted in EXACTLY ONE place (inside MoveTowards). A second " +
                    "consult site is how it reaches Windup/Charge and silently makes the telegraphed charge " +
                    "un-completable.");

                // The single gate must sit inside MoveTowards, not in Update's state switch.
                int moveIdx = src.IndexOf("private void MoveTowards", System.StringComparison.Ordinal);
                int gateIdx = src.IndexOf("_feedback.IsStaggered", System.StringComparison.Ordinal);
                Assert.Greater(moveIdx, 0, path + ": MoveTowards must exist");
                Assert.Greater(gateIdx, moveIdx,
                    path + ": the stagger gate must sit INSIDE MoveTowards (after its declaration), never in " +
                    "the Update state switch where it could reach Windup/Charge");

                // And it must never assign State.
                var afterGate = src.Substring(gateIdx, System.Math.Min(240, src.Length - gateIdx));
                StringAssert.DoesNotContain("State =", afterGate,
                    path + ": the stagger must NEVER write State — it suppresses movement only");
            }
        }

        // ---------------------------------------------------------------- AC5: dials

        [Test]
        public void OffSwitch_KillsEveryChannel_AndClearsAnyLiveFlash()
        {
            var go = MakeCreature(4, out var fb, out var hp);
            try
            {
                // POSITIVE CONTROL FIRST — otherwise "nothing happened" is satisfiable by a driver that never
                // works at all, and the off-switch test would pass on a broken feature.
                // (unwired deathHandler -> ActiveTier = Medium, whose stagger is non-zero)
                hp.ApplyDamage(5f, DamageType.Slash);
                Assert.IsTrue(fb.FlashActive, "control: with feedback ON a hit arms the flash");
                Assert.IsTrue(fb.FlinchActive, "control: …and the flinch");
                Assert.IsTrue(fb.IsStaggered, "control: …and the easy-tier stagger");

                fb.ResetVisuals();
                fb.feedbackEnabled = false;
                hp.ApplyDamage(10f, DamageType.Slash);
                Assert.IsFalse(fb.FlashActive, "off: the flash is never armed");
                Assert.IsFalse(fb.FlinchActive, "off: the flinch is never armed");
                Assert.AreEqual(0f, fb.FlashAmount, 1e-6f, "off: no flash amplitude");
                Assert.AreEqual(Vector3.zero, fb.FlinchOffset, "off: no flinch offset");
                Assert.IsFalse(fb.IsStaggered, "off: no stagger");
                Assert.AreEqual(0, fb.DeathPuffCount, "off: no death puff");
                Assert.AreEqual(0f, fb.MaxMaterialFlash(), 1e-6f, "off: every part-material rests at 0");
            }
            finally { Destroy(go); }
        }

        [Test]
        public void PerTierStaggerDial_SurvivesTheNextApplyDifficulty_TheDeadKnobGuard()
        {
            // AC5 🔒 — every per-tier dial must write BOTH the active field AND the active tier's map entry, or
            // ApplyDifficulty clobbers the live dial. ApplyDifficulty runs on EVERY hit here, so a dead knob
            // would revert the instant the Sponsor lands his next swing.
            var go = MakeCreature(2, out var fb, out var hp);
            var reg = new SettingsRegistry();
            try
            {
                fb.deathHandler = null; // ActiveTier -> Medium
                SettingsCatalog.PopulateHitFeedback(reg, new[] { fb });
                var row = reg.Get(SettingsCatalog.HitFlinchStaggerId) as FloatSettingEntry;
                Assert.IsNotNull(row, "the per-tier stagger row must be registered");

                row.SetValue(0.9f);
                Assert.AreEqual(0.9f, fb.staggerSeconds, 1e-4f, "the dial writes the ACTIVE field");
                Assert.AreEqual(0.9f, fb.medStaggerSeconds, 1e-4f,
                    "…AND the ACTIVE tier's map entry — without this the next hit clobbers it");

                fb.ApplyDifficulty(SurvivalNeed.DifficultyTier.Medium);
                Assert.AreEqual(0.9f, fb.staggerSeconds, 1e-4f,
                    "the dialled value SURVIVES ApplyDifficulty (the dead-knob guard)");
            }
            finally { Destroy(go); }
        }

        [Test]
        public void EveryHitFeedbackRow_IsDevConsole_NeverPlayerFacing()
        {
            // DECISIONS 2026-07-01 (the F1/F3 settings SPLIT): the player must never see juice amplitudes.
            foreach (string id in new[] { SettingsCatalog.HitFeedbackEnabledId, SettingsCatalog.HitFlashIntensityId,
                                          SettingsCatalog.HitFlashDurationId, SettingsCatalog.HitFlinchAmplitudeId,
                                          SettingsCatalog.HitFlinchStaggerId, SettingsCatalog.HitPuffCountId })
            {
                Assert.IsTrue(SettingsCategory.IsDev(id), id + " must be a DEV-console (F3) row");
                Assert.IsFalse(SettingsCategory.IsPlayer(id), id + " must NOT appear in the player F1 panel");
            }
        }

        [Test]
        public void TheDialsFanOutAcrossEveryEnemy_NotJustTheFirst()
        {
            // The berryBushes precedent: each creature carries its own driver (no shared manager), so a row that
            // writes only the first would leave the second creature on the baked defaults — a half-dialled world
            // the Sponsor would read as "the dial does nothing" on whichever enemy he tested second.
            var a = MakeCreature(3, out var fbA, out _);
            var b = MakeCreature(4, out var fbB, out _);
            var reg = new SettingsRegistry();
            try
            {
                SettingsCatalog.PopulateHitFeedback(reg, new[] { fbA, fbB });
                ((FloatSettingEntry)reg.Get(SettingsCatalog.HitFlashIntensityId)).SetValue(0.31f);
                Assert.AreEqual(0.31f, fbA.flashIntensity, 1e-4f, "driver A dialled");
                Assert.AreEqual(0.31f, fbB.flashIntensity, 1e-4f, "driver B dialled by the SAME row");

                ((BoolSettingEntry)reg.Get(SettingsCatalog.HitFeedbackEnabledId)).SetValue(false);
                Assert.IsFalse(fbA.feedbackEnabled, "the master switch reaches driver A");
                Assert.IsFalse(fbB.feedbackEnabled, "the master switch reaches driver B — one flag, whole feature");
            }
            finally { Destroy(a); Destroy(b); }
        }

        [Test]
        public void PuffCountRow_CannotExceedTheCalmToneCap()
        {
            // brief §1.2's <=12 is a TONE constraint, not a suggestion — the band ceiling and the emitter's own
            // clamp both hold it, so neither a dragged slider nor a hand-edited PlayerPrefs can crank past it.
            Assert.LessOrEqual(SettingsCatalog.HitPuffCountMax, 12,
                "the puff-count band must not reach past brief §1.2's hard cap of 12 particles per burst");
            var go = MakeCreature(2, out var fb, out _);
            var reg = new SettingsRegistry();
            try
            {
                SettingsCatalog.PopulateHitFeedback(reg, new[] { fb });
                ((IntSettingEntry)reg.Get(SettingsCatalog.HitPuffCountId)).SetValue(99);
                Assert.LessOrEqual(fb.puffCount, 12, "a dialled puff count is CLAMPED to the cap, not obeyed");
                Assert.LessOrEqual(fb.deathPuffCount, 12, "…and so is the death puff that scales off it");
            }
            finally { Destroy(go); }
        }
    }
}
