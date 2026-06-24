using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the POND RECESS + FOAM live nudge handle (ticket 86cadj4g7 — Sponsor #130 re-soak,
    /// "live nudge handle" path). Pins the STEP CONTRACTS + the LAYOUT-AGNOSTIC keys + that the handle ships
    /// SERIALIZED into the Boot scene at the SHIPPED DEFAULTS (knee-deep recess + foam OFF) — the
    /// component-in-source-but-not-in-scene silent-killer (a handle that never reaches the build can't be
    /// soaked). Sibling of HeldAxeLengthPicker's Contract_ArraysAligned guard.
    /// </summary>
    public class PondNudgeTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // (1) The step arrays line up: one name per recess/foam value; the values are strictly increasing
        // (flush->knee-deep->deeper, off->light->sea-like). Catches a drift between the surfaced names + values.
        [Test]
        public void StepArrays_Aligned_AndIncreasing()
        {
            Assert.AreEqual(PondNudge.RecessStepNames.Length, PondNudge.RecessStepValue.Length,
                "one recess name per recess value");
            Assert.AreEqual(PondNudge.FoamStepNames.Length, PondNudge.FoamStepValue.Length,
                "one foam name per foam value");
            for (int i = 1; i < PondNudge.RecessStepValue.Length; i++)
                Assert.Greater(PondNudge.RecessStepValue[i], PondNudge.RecessStepValue[i - 1],
                    "recess steps must DEEPEN monotonically (flush -> knee-deep -> deeper)");
            for (int i = 1; i < PondNudge.FoamStepValue.Length; i++)
                Assert.Greater(PondNudge.FoamStepValue[i], PondNudge.FoamStepValue[i - 1],
                    "foam steps must increase monotonically (off -> light -> sea-like)");
        }

        // (2) The DEFAULT steps match the BAKED shipped values — so a soak that never presses a key sees exactly
        // the shipped pond, AND the live handle's knee-deep / foam-off defaults agree with the bake (a divergence
        // would mean pressing a key once changes nothing visible, or the default step is not the shipped pond).
        [Test]
        public void Defaults_MatchBakedShippedValues()
        {
            Assert.AreEqual(LowPolyZoneGen.PondRecessKneeDeep, PondNudge.RecessStepValue[PondNudge.RecessDefaultStep], 1e-4f,
                "the default recess step (KNEE-DEEP) must equal the baked LowPolyZoneGen.PondRecessKneeDeep");
            Assert.AreEqual(LowPolyZoneGen.PondFoamOff, PondNudge.FoamStepValue[PondNudge.FoamDefaultStep], 1e-4f,
                "the default foam step (OFF) must equal the baked LowPolyZoneGen.PondFoamOff (a STILL pool, #130)");
            // The foam steps must equal the shared LowPolyZoneGen levels (so the handle + the bake are one source).
            Assert.AreEqual(LowPolyZoneGen.PondFoamOff, PondNudge.FoamStepValue[0], 1e-4f, "foam step 0 == PondFoamOff");
            Assert.AreEqual(LowPolyZoneGen.PondFoamLight, PondNudge.FoamStepValue[1], 1e-4f, "foam step 1 == PondFoamLight");
            Assert.AreEqual(LowPolyZoneGen.PondFoamSeaLike, PondNudge.FoamStepValue[2], 1e-4f, "foam step 2 == PondFoamSeaLike");
        }

        // (3) The nudge keys are LAYOUT-AGNOSTIC (PgUp/PgDn/Home/End — Danish-keyboard-safe), NEVER US-position
        // punctuation (which shifts on the Sponsor's Danish layout — [[sponsor-danish-keyboard-layout]]).
        [Test]
        public void Keys_AreLayoutAgnostic_NeverUsPunctuation()
        {
            var go = new GameObject("PondNudgeRig");
            var nudge = go.AddComponent<PondNudge>();
            var keys = new[] { nudge.recessDeeperKey, nudge.recessShallowerKey, nudge.foamUpKey, nudge.foamDownKey };
            var allowed = new[] { KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home, KeyCode.End };
            foreach (var k in keys)
                Assert.Contains(k, allowed,
                    "every pond-nudge key must be a layout-safe navigation key (PgUp/PgDn/Home/End), got " + k);
            foreach (var punct in new[] { KeyCode.Semicolon, KeyCode.Quote, KeyCode.LeftBracket, KeyCode.RightBracket,
                KeyCode.Equals, KeyCode.Minus, KeyCode.Comma, KeyCode.Period, KeyCode.Slash })
                foreach (var k in keys)
                    Assert.AreNotEqual(punct, k, "no pond-nudge key may be US-position punctuation (shifts on Danish)");
            // The four keys must be distinct (no self-collision).
            CollectionAssert.AllItemsAreUnique(keys, "the four pond-nudge keys must be distinct");
            Object.DestroyImmediate(go);
        }

        // (4) The handle SERIALIZES into the Boot scene — without it a -verifyPond/-verifyPondSide soak with the
        // handle can't be driven + the Sponsor can't dial in the build (the component-in-source-not-in-scene trap).
        [Test]
        public void BootScene_CarriesPondNudge_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            PondNudge nudge = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                nudge = root.GetComponentInChildren<PondNudge>(true);
                if (nudge != null) break;
            }
            Assert.IsNotNull(nudge,
                "the Boot scene must carry the PondNudge handle serialized — without it the Sponsor cannot dial " +
                "the pond recess/foam in the shipped build (ticket 86cadj4g7 #130 live-nudge-handle path)");
        }
    }
}
