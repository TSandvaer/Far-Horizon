using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the LIVE PER-CLASS SWING-AIM NUDGE HANDLE (86cb6v03j round 2).
    ///
    /// THE LOAD-BEARING TEST OF THIS ROUND is
    /// <see cref="AllDialsZero_EveryClass_ReturnsBakedEulerBitForBit"/> plus its ComposeSeat sibling: the round
    /// promised the built exe behaves BIT-FOR-BIT as 70583d8 at the dial default, and that promise is the only
    /// thing that makes shipping a tuning knob into the aim path safe while the aim itself is still under
    /// dispute. Everything else here defends that promise's edges — the negative control still zeroing, a
    /// no-op write not marking the build dialled, the readout naming what it claims to name.
    ///
    /// The suite drives the SHIPPED seams (<see cref="HeldToolRig.SwingAimEulerForClass"/>,
    /// <see cref="HeldToolRig.ComposeSeat"/>, <see cref="SettingsCatalog.PopulateSwingAim"/>) rather than a
    /// mirrored re-implementation — the tautological-assert trap (unity-conventions.md §Editor-vs-runtime): a
    /// test that re-lists the baked constants beside the rig goes green against a rig that ignores them.
    /// </summary>
    public class SwingAimNudgeTests
    {
        private static readonly int[] AllClasses =
        {
            CastawayCharacter.WeaponClassAxe,
            CastawayCharacter.WeaponClassPickaxe,
            CastawayCharacter.WeaponClassDagger,
            CastawayCharacter.WeaponClassSpear,
            CastawayCharacter.WeaponClassSword,
        };

        [SetUp]
        public void SetUp()
        {
            // EditMode has no play-entry, so the [RuntimeInitializeOnLoadMethod] reset never fires here — clear
            // by hand so one test's dial cannot leak into the next (the same static-leak class the runtime reset
            // exists for).
            SwingAimNudge.ClearAll();
            HeldToolRig.SwingAimForcedZero = false;
        }

        [TearDown]
        public void TearDown()
        {
            SwingAimNudge.ClearAll();
            HeldToolRig.SwingAimForcedZero = false;
        }

        // ---------------------------------------------------------------------------------------------------
        // THE IDENTITY PROMISE — all dials at 0 == exactly what ships today.
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AllDialsZero_EveryClass_ReturnsBakedEulerBitForBit()
        {
            foreach (int c in AllClasses)
            {
                Vector3 baked = HeldToolRig.SwingAimBakedEulerForClass(c);
                Vector3 live = HeldToolRig.SwingAimEulerForClass(c);
                // EXACT float equality, not Approximately: the round's promise is bit-for-bit, and a quaternion
                // round-trip would pass an epsilon comparison while shipping a literal 70583d8 never had.
                Assert.AreEqual(baked.x, live.x, 0f, "pitch must be the baked literal for " + SwingAimNudge.ClassName(c));
                Assert.AreEqual(baked.y, live.y, 0f, "yaw must be the baked literal for " + SwingAimNudge.ClassName(c));
                Assert.AreEqual(baked.z, live.z, 0f, "roll must be the baked literal for " + SwingAimNudge.ClassName(c));
            }
        }

        [Test]
        public void Compose_ZeroNudge_ShortCircuits_NoQuaternionRoundTrip()
        {
            // A euler triple whose CANONICAL decomposition is a different triple naming the same rotation:
            // Quaternion.eulerAngles returns pitch inside [-90, 90] (mapped into [0, 360)), so a 120 deg pitch
            // comes back re-expressed. If the short-circuit in Compose were ever removed "for consistency", a
            // baked delta would ship as a DIFFERENT literal than the one in source — and this test reds.
            var awkward = new Vector3(120f, 30f, 0f);
            Vector3 composed = SwingAimNudge.Compose(awkward, Vector3.zero);
            Assert.AreEqual(awkward.x, composed.x, 0f);
            Assert.AreEqual(awkward.y, composed.y, 0f);
            Assert.AreEqual(awkward.z, composed.z, 0f);

            Vector3 roundTripped = Quaternion.Euler(awkward).eulerAngles;
            float worst = Mathf.Max(Mathf.Abs(roundTripped.x - awkward.x),
                          Mathf.Max(Mathf.Abs(roundTripped.y - awkward.y),
                                    Mathf.Abs(roundTripped.z - awkward.z)));
            Assert.Greater(worst, 1f,
                "PRECONDITION: this triple must genuinely re-decompose differently, otherwise the assert above " +
                "proves nothing about the short-circuit.");
            // And the rotation itself is of course unchanged by the round trip — the point is that the TRIPLE is
            // not, and the triple is what gets baked into source.
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(awkward), Quaternion.Euler(roundTripped)), 1e-3f);
        }

        [Test]
        public void AllDialsZero_ComposeSeat_IsIdenticalToTheNoAimOverload()
        {
            // The seat maths is what the player actually sees. Drive the SHIPPED pure function at swing-aim
            // weight 1 (the fully-engaged strike) with the dials at default and assert it lands exactly where
            // the pre-round-2 back-compat overload does with the same baked delta.
            var followPos = new Vector3(1.3f, 1.7f, -0.4f);
            Quaternion followRot = Quaternion.Euler(23f, -117f, 61f);
            var seatOffset = new Vector3(0.13f, 0.14f, 0.06f);
            var seatEuler = new Vector3(-14f, 92f, 8f);

            foreach (int c in AllClasses)
            {
                HeldToolRig.ComposeSeat(followPos, followRot, seatOffset, seatEuler,
                                        Vector3.zero, Vector3.zero, 0f,
                                        HeldToolRig.SwingAimEulerForClass(c), 1f,
                                        out Vector3 posLive, out Quaternion rotLive);
                HeldToolRig.ComposeSeat(followPos, followRot, seatOffset, seatEuler,
                                        Vector3.zero, Vector3.zero, 0f,
                                        HeldToolRig.SwingAimBakedEulerForClass(c), 1f,
                                        out Vector3 posBaked, out Quaternion rotBaked);
                Assert.AreEqual(posBaked, posLive, "seat POSITION must be untouched at the dial default (" +
                                                   SwingAimNudge.ClassName(c) + ")");
                Assert.AreEqual(0f, Quaternion.Angle(rotBaked, rotLive), 1e-4f,
                    "seat ROTATION must be untouched at the dial default (" + SwingAimNudge.ClassName(c) + ")");
            }
        }

        [Test]
        public void ApplyingTheRegisteredRowsAtTheirDefaults_LeavesTheBuildPristine()
        {
            // SettingsRegistry.ApplyAll() runs on every launch and pushes each row's CURRENT value back through
            // its setter. If a no-op write bumped the revision, a stock launch would look "dialled" — the
            // readout would paint and the log would fill on a build nobody touched, and every -verify* capture
            // would change. This pins the SetAxis no-change guard.
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateSwingAim(reg);
            reg.ApplyAll();
            Assert.IsTrue(SwingAimNudge.IsPristine, "ApplyAll at defaults must leave every dial at 0");
            Assert.AreEqual(0, SwingAimNudge.Revision, "a no-op write must not bump the revision");
        }

        // ---------------------------------------------------------------------------------------------------
        // THE DIAL ACTUALLY DIALS.
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void NonZeroNudge_RotatesTheEffectiveAim_ByExactlyThatAmount_InTheToolsOwnFrame()
        {
            const int c = CastawayCharacter.WeaponClassSword;
            Vector3 baked = HeldToolRig.SwingAimBakedEulerForClass(c);
            SwingAimNudge.SetAxis(c, 1, 30f);   // +30 deg yaw

            Quaternion expected = Quaternion.Euler(baked) * Quaternion.Euler(0f, 30f, 0f);
            Quaternion actual = Quaternion.Euler(HeldToolRig.SwingAimEulerForClass(c));
            Assert.AreEqual(0f, Quaternion.Angle(expected, actual), 1e-3f,
                "the nudge must RIGHT-multiply in the tool's own frame (unity6-mastery §5 — never a " +
                "component-wise euler sum, which is gimbal-locked at these magnitudes)");
            Assert.AreEqual(30f, Quaternion.Angle(Quaternion.Euler(baked), actual), 1e-3f,
                "and it must move the aim by exactly the dialled angle");
        }

        [Test]
        public void EveryClassAndAxis_IsIndependentlyDialable()
        {
            // The closure-capture bug this guards is silent and total: one mis-captured loop variable and all 15
            // rows drive the sword's roll while every label still reads correctly.
            for (int c = 0; c < SwingAimNudge.ClassCount; c++)
                for (int a = 0; a < SwingAimNudge.AxisNames.Length; a++)
                {
                    SwingAimNudge.ClearAll();
                    SwingAimNudge.SetAxis(c, a, 12f);
                    for (int c2 = 0; c2 < SwingAimNudge.ClassCount; c2++)
                        for (int a2 = 0; a2 < SwingAimNudge.AxisNames.Length; a2++)
                            Assert.AreEqual(c2 == c && a2 == a ? 12f : 0f, SwingAimNudge.GetAxis(c2, a2), 1e-6f,
                                "dial (" + SwingAimNudge.ClassName(c) + "," + SwingAimNudge.AxisNames[a] +
                                ") must move ONLY itself");
                }
        }

        [Test]
        public void RowSetters_DriveTheirOwnClassAndAxis()
        {
            // Same independence claim, but through the REGISTERED ROWS rather than the static — so a correct
            // static plus a mis-wired Populate loop cannot pass.
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateSwingAim(reg);
            for (int c = 0; c < SwingAimNudge.ClassCount; c++)
                for (int a = 0; a < SwingAimNudge.AxisNames.Length; a++)
                {
                    SwingAimNudge.ClearAll();
                    var row = reg.Get(SettingsCatalog.SwingAimRowId(c, a)) as FloatSettingEntry;
                    Assert.IsNotNull(row, "row must exist: " + SettingsCatalog.SwingAimRowId(c, a));
                    row.SetValue(-7.5f);
                    Assert.AreEqual(-7.5f, SwingAimNudge.GetAxis(c, a), 1e-6f);
                    Assert.AreEqual(-7.5f, row.Value, 1e-6f, "the row must READ BACK what it wrote");
                }
        }

        [Test]
        public void Dials_AreClampedToTheStatedBand()
        {
            SwingAimNudge.SetAxis(CastawayCharacter.WeaponClassAxe, 0, 900f);
            Assert.AreEqual(SwingAimNudge.LimitDeg, SwingAimNudge.GetAxis(CastawayCharacter.WeaponClassAxe, 0), 1e-6f);
            SwingAimNudge.SetAxis(CastawayCharacter.WeaponClassAxe, 0, -900f);
            Assert.AreEqual(-SwingAimNudge.LimitDeg, SwingAimNudge.GetAxis(CastawayCharacter.WeaponClassAxe, 0), 1e-6f);
        }

        // ---------------------------------------------------------------------------------------------------
        // THE NEGATIVE CONTROL AND THE GATE ARE NOT DISTURBED.
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void SwingAimForcedZero_StillZeroes_EvenWithEveryDialTurnedUp()
        {
            // -swingAimFaultZero must keep reproducing the PRE-86cb6v03j seat exactly. If the dial leaked past
            // it, the shipped gate's proven-RED negative control would quietly stop being the unfixed build.
            for (int c = 0; c < SwingAimNudge.ClassCount; c++)
                for (int a = 0; a < 3; a++) SwingAimNudge.SetAxis(c, a, 45f);

            HeldToolRig.SwingAimForcedZero = true;
            foreach (int c in AllClasses)
            {
                Assert.AreEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(c),
                    "the negative control must zero " + SwingAimNudge.ClassName(c) + " regardless of the dial");
                Assert.AreEqual(Vector3.zero, HeldToolRig.SwingAimBakedEulerForClass(c));
            }
        }

        [Test]
        public void PickaxeHasADial_AndItsShippedDefaultIsUnchanged()
        {
            // The pickaxe is EXCLUDED from the round-1 fix by scope (its swing seat is the Sponsor-passed
            // mineSeatEulerDelta) but the Sponsor judges it by eye at THIS soak, so it must carry dials like the
            // rest while its shipped default stays exactly zero swing-aim.
            const int c = CastawayCharacter.WeaponClassPickaxe;
            Assert.AreEqual(Vector3.zero, HeldToolRig.SwingAimBakedEulerForClass(c),
                "the pickaxe's shipped swing-aim delta must stay ZERO — this round must not re-aim it");

            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateSwingAim(reg);
            for (int a = 0; a < 3; a++)
                Assert.IsTrue(reg.Has(SettingsCatalog.SwingAimRowId(c, a)),
                    "the pickaxe must carry a " + SwingAimNudge.AxisNames[a] + " dial like every other class");
        }

        // ---------------------------------------------------------------------------------------------------
        // REGISTRATION SHAPE.
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void Populate_RegistersFifteenDevRows_AllPersistFalse_AllDefaultZero()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateSwingAim(reg);
            Assert.AreEqual(15, reg.Count, "5 classes x pitch/yaw/roll");

            foreach (var e in reg.Entries)
            {
                var f = e as FloatSettingEntry;
                Assert.IsNotNull(f, e.Id + " must be a float slider row");
                Assert.AreEqual(0f, f.Default, 0f, e.Id + " must default to 0 (== ships today)");
                Assert.IsFalse(f.Persist,
                    e.Id + " must be persist:false — a DIAL-TO-BAKE instrument. A persisted swing-aim override " +
                    "would become the shipped aim at the next launch without ever being baked, and would poison " +
                    "every -verify* run (the 86cah90cp round-3 sun incident, on this surface).");
                Assert.IsTrue(SettingsCategory.IsDev(e.Id), e.Id + " must land in the F3 DEV console, not F1");
                Assert.AreEqual(-SwingAimNudge.LimitDeg, f.Min, 1e-6f);
                Assert.AreEqual(SwingAimNudge.LimitDeg, f.Max, 1e-6f);
            }
        }

        [Test]
        public void Populate_IsNullRegistrySafe()
        {
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateSwingAim(null));
        }

        // ---------------------------------------------------------------------------------------------------
        // THE READ-BACK — the values must come back in a form the orchestrator can bake.
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void Readout_NamesEveryClass_AndCarriesNudgeAndEffectiveTriples()
        {
            SwingAimNudge.SetAxis(CastawayCharacter.WeaponClassSword, 2, 17.5f);
            string text = SwingAimNudge.Readout();

            for (int c = 0; c < SwingAimNudge.ClassCount; c++)
                StringAssert.Contains(SwingAimNudge.ClassName(c), text,
                    "the hand-back block must name every class — the orchestrator bakes from THIS text");
            Assert.AreEqual(SwingAimNudge.ClassCount, text.Split('\n').Length, "one line per class");
            StringAssert.Contains("nudge=", text);
            StringAssert.Contains("effective=", text);
            // INVARIANT-CULTURE DOTS. The Sponsor's machine is Danish-locale and this project has already
            // shipped a gate log mixing comma- and dot-decimals on one run; "17,500" in a bake artefact reads as
            // two numbers. A comma anywhere except the vector separators would break the bake.
            StringAssert.Contains("17.500", text);
            StringAssert.DoesNotContain("17,500", text);
        }

        [Test]
        public void Readout_EffectiveTriple_IsExactlyWhatTheRigApplies()
        {
            // The bake contract: paste `effective` into HeldToolRig.SwingAim<Class> and the build reproduces
            // what the Sponsor approved. That is only true if Effective() and the rig read the same value.
            SwingAimNudge.SetAxis(CastawayCharacter.WeaponClassSpear, 0, -21f);
            foreach (int c in AllClasses)
                Assert.AreEqual(HeldToolRig.SwingAimEulerForClass(c), SwingAimNudge.Effective(c),
                    "the printed 'effective' triple must BE the rig's applied delta for " +
                    SwingAimNudge.ClassName(c));
        }

        [Test]
        public void ClassNamesAndRowIds_AreStableAndDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int c = 0; c < SwingAimNudge.ClassCount; c++)
                for (int a = 0; a < SwingAimNudge.AxisNames.Length; a++)
                    Assert.IsTrue(seen.Add(SettingsCatalog.SwingAimRowId(c, a)),
                        "row ids must be unique — SettingsRegistry.Register THROWS on a duplicate, which would " +
                        "take the whole console down at Start");
            Assert.AreEqual("swing_aim_axe_pitch", SettingsCatalog.SwingAimRowId(CastawayCharacter.WeaponClassAxe, 0));
            Assert.AreEqual("swing_aim_sword_roll", SettingsCatalog.SwingAimRowId(CastawayCharacter.WeaponClassSword, 2));
        }

        // ---------------------------------------------------------------------------------------------------
        // THE READOUT COMPONENT — present in the shipped scene, and SILENT until a dial moves.
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void BootScene_CarriesTheSwingAimDialReadout_OnF4()
        {
            // The component-in-source-but-not-in-scene trap: a readout that never serializes into Boot.unity
            // ships inert, and the soak loses exactly the numbers this round exists to capture. CI bootstraps
            // before EditMode; a bare LOCAL run against a stale committed Boot.unity may red here until the
            // scene is regenerated (unity-conventions.md §"Run BootstrapProject.Run BEFORE any LOCAL EditMode run").
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            SwingAimDialReadout readout = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                readout = root.GetComponentInChildren<SwingAimDialReadout>(true);
                if (readout != null) break;
            }
            Assert.IsNotNull(readout,
                "the Boot scene must carry the SwingAimDialReadout — without it the Sponsor's dialled values " +
                "never reach the Player.log and cannot be baked.");
            Assert.AreEqual(KeyCode.F4, readout.readoutKey,
                "the readout key must be F4 — free (F1/F3/F7/F8/F9/F10 are bound, F2 is unbound by decision) " +
                "and an F-key, so it sits at the same physical position on the Sponsor's Danish keyboard.");
        }

        [Test]
        public void Readout_DrawsNothing_WhileEveryDialIsAtDefault()
        {
            // THIS is what keeps every -verify* capture byte-identical: the readout is gated on the dial state,
            // NOT on the F10 overlay master (SwingVerifyCapture itself calls DebugOverlays.Show(), so the master
            // would not have protected the capture PNGs).
            var go = new GameObject("readout-test");
            try
            {
                var readout = go.AddComponent<SwingAimDialReadout>();
                Assert.IsTrue(SwingAimNudge.IsPristine);
                Assert.IsFalse(readout.ReadoutVisible,
                    "a pristine build must draw NOTHING — otherwise the swing side-profile captures change");

                SwingAimNudge.SetAxis(CastawayCharacter.WeaponClassAxe, 0, 5f);
                Assert.IsFalse(SwingAimNudge.IsPristine);
                Assert.IsTrue(readout.ReadoutVisible,
                    "the moment a dial moves the readout must appear — the Sponsor should never have to hunt " +
                    "for a second key to see what he just changed");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
