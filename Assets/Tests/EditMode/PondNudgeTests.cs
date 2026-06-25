using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the POND RECESS live nudge handle (ticket 86cadj4g7 — Sponsor #130 re-soak,
    /// "live nudge handle" path). Pins the STEP CONTRACT + the LAYOUT-AGNOSTIC keys + that the handle ships
    /// SERIALIZED into the Boot scene at the SHIPPED DEFAULT (the #130-round-9 baked recess 0.30u) — the
    /// component-in-source-but-not-in-scene silent-killer (a handle that never reaches the build can't be
    /// soaked). Sibling of HeldAxeLengthPicker's Contract_ArraysAligned guard.
    ///
    /// FOAM DIAL DROPPED (#130 third re-soak): the foam control was removed — the Sponsor always wants pond
    /// foam OFF (a still pool) and the old Home/End dial was DEAD at runtime, so foam is now baked OFF on the
    /// pond material unconditionally (see FreshwaterPondSceneTests.Pond_FoamDistance_* + the committed
    /// PondWaterMat.mat asset), not a dial. These guards now cover the recess handle only.
    /// </summary>
    public class PondNudgeTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // (1) The recess step arrays line up: one name per value; the values are strictly increasing
        // (flush->knee-deep->deeper). Catches a drift between the surfaced names + values.
        [Test]
        public void StepArrays_Aligned_AndIncreasing()
        {
            Assert.AreEqual(PondNudge.RecessStepNames.Length, PondNudge.RecessStepValue.Length,
                "one recess name per recess value");
            for (int i = 1; i < PondNudge.RecessStepValue.Length; i++)
                Assert.Greater(PondNudge.RecessStepValue[i], PondNudge.RecessStepValue[i - 1],
                    "recess steps must DEEPEN monotonically (flush -> knee-deep -> deeper)");
        }

        // (2) The DEFAULT recess step matches the BAKED shipped value — so a soak that never presses a key sees
        // exactly the shipped pond. After the #130 round-9 fill-the-bowl re-balance the baked recess is 0.30u (the
        // water surface 0.30u below the plateau, so the dry shore lip stays a short traversable step-over → fill
        // ≈0.90 of the mouth; the knee-deep DEPTH moved into PondWadeDepth 0.75). A divergence would mean the
        // default step is not the shipped pond (pressing a key once would jump the recess unexpectedly).
        [Test]
        public void Defaults_MatchBakedShippedValues()
        {
            Assert.AreEqual(LowPolyZoneGen.PondRecessKneeDeep, PondNudge.RecessStepValue[PondNudge.RecessDefaultStep], 1e-4f,
                "the default recess step must equal the baked LowPolyZoneGen.PondRecessKneeDeep (0.30, #130 round 9)");
        }

        // (3) The nudge keys are LAYOUT-AGNOSTIC (PgUp/PgDn — Danish-keyboard-safe), NEVER US-position
        // punctuation (which shifts on the Sponsor's Danish layout — [[sponsor-danish-keyboard-layout]]).
        [Test]
        public void Keys_AreLayoutAgnostic_NeverUsPunctuation()
        {
            var go = new GameObject("PondNudgeRig");
            var nudge = go.AddComponent<PondNudge>();
            var keys = new[] { nudge.recessDeeperKey, nudge.recessShallowerKey };
            var allowed = new[] { KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home, KeyCode.End };
            foreach (var k in keys)
                Assert.Contains(k, allowed,
                    "every pond-nudge key must be a layout-safe navigation key (PgUp/PgDn/Home/End), got " + k);
            foreach (var punct in new[] { KeyCode.Semicolon, KeyCode.Quote, KeyCode.LeftBracket, KeyCode.RightBracket,
                KeyCode.Equals, KeyCode.Minus, KeyCode.Comma, KeyCode.Period, KeyCode.Slash })
                foreach (var k in keys)
                    Assert.AreNotEqual(punct, k, "no pond-nudge key may be US-position punctuation (shifts on Danish)");
            // The keys must be distinct (no self-collision).
            CollectionAssert.AllItemsAreUnique(keys, "the pond-nudge keys must be distinct");
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
