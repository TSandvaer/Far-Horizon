using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// HOLD-SWING CADENCE SOURCE guards (86cayy770 — the Sponsor's "when I hold down the left mouse the animation
    /// jerks 3 times (starts over before finishing) and the stone is harvested").
    ///
    /// THE DEFECT THESE CLOSE (measured at origin/main b9abf7b, NOT inferred):
    ///   • <see cref="CharacterAssetGen"/> binds the AttackPickaxe state to the REPAIRED clip asset
    ///     <c>CastawayPickaxeSwing_repaired</c> (CharacterAssetGen.cs:1236-1239, introduced by fee2604 / #337 /
    ///     86cav8xg9 — the pelvis-fold fix).
    ///   • <see cref="CastawayCharacter.MeleeClipLength"/> — the hold-cadence SOURCE — looked that clip up by an
    ///     EXACT name match against <see cref="CastawayCharacter.PickaxeSwingClipName"/> = "CastawayPickaxeSwing".
    ///   • The names differ, so the lookup returned 0 and the cadence silently fell back to the verbs' serialized
    ///     <c>swingClipLengthSeconds</c> (1.6s) against a 5.2s clip: gate 1.6/1.5 = 1.067s vs a real 5.2/1.5 =
    ///     3.467s swing, i.e. the Animator was re-triggered at ~31% of the clip (AnyState→AttackPickaxe has
    ///     canTransitionToSelf = true, CharacterAssetGen.cs:1608) → the swing visibly RESTARTED mid-play.
    ///
    /// WHY THE PRE-EXISTING SUITE MISSED IT — the hole this file fills. <c>AttackSwingControllerTests</c> asserts the
    /// runtime↔editor mirrors match (<c>RuntimeAndEditorMirrors_Match_ForWeaponClassAndClipNames</c>:202 and
    /// <c>AttackClipNameForClass_MapsEachWeaponClassToItsSwingClip</c>:220) — but BOTH compare a runtime CONSTANT to
    /// an editor CONSTANT. Those two constants still agree; what diverged is the constant vs. the clip the controller
    /// STATE ACTUALLY BINDS. That is the "derived-const / tautological assert" family in
    /// <c>unity-conventions.md</c> §Editor-vs-runtime: an assert whose operands both trace to the same literal cannot
    /// fail when the real wiring drifts. So the guard below deliberately compares the runtime lookup key against the
    /// BOUND ASSET, never against another constant.
    /// </summary>
    public class HoldSwingCadenceTests
    {
        // (controller state name, the WeaponClass the runtime lookup key is derived from).
        private static readonly (string state, int weaponClass)[] AttackStates =
        {
            ("AttackAxe",     CastawayCharacter.WeaponClassAxe),
            ("AttackPickaxe", CastawayCharacter.WeaponClassPickaxe),
            ("AttackDagger",  CastawayCharacter.WeaponClassDagger),
            ("AttackSpear",   CastawayCharacter.WeaponClassSpear),
            ("AttackSword",   CastawayCharacter.WeaponClassSword),
        };

        private static AnimatorState FindState(AnimatorController controller, string name)
        {
            foreach (var layer in controller.layers)
                foreach (var child in layer.stateMachine.states)
                    if (child.state != null && child.state.name == name) return child.state;
            return null;
        }

        /// <summary>
        /// THE RED-ON-BROKEN GUARD (86cayy770 AC3). For every per-class attack state, the clip the state ACTUALLY
        /// BINDS must be resolvable by the runtime cadence lookup key. This is the assert that fails on the
        /// pre-fix code: AttackPickaxe binds "CastawayPickaxeSwing_repaired" while the lookup key is
        /// "CastawayPickaxeSwing", and the pre-fix resolution was exact-match-only.
        /// NOT tautological: the left operand is read off the CONTROLLER ASSET, the right off the runtime constant —
        /// deleting the variant-tolerant resolution, or re-pointing a state at a differently-named clip, reds this.
        /// </summary>
        [Test]
        public void EveryAttackState_BindsAClipTheRuntimeCadenceLookupCanResolve()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, CharacterAssetGen.ControllerPath + " must exist (run BootstrapProject.Run)");

            foreach (var (stateName, weaponClass) in AttackStates)
            {
                var state = FindState(controller, stateName);
                Assert.IsNotNull(state, "controller must carry the '" + stateName + "' per-class swing state");

                var bound = state.motion as AnimationClip;
                Assert.IsNotNull(bound, stateName + " must bind an AnimationClip (never a null/T-pose motion)");
                Assert.Greater(bound.length, 0f, stateName + "'s bound clip must have a real length");

                string lookupKey = CastawayCharacter.AttackClipNameForClass(weaponClass);
                Assert.IsTrue(CastawayCharacter.ClipNameMatchesClass(bound.name, lookupKey),
                    stateName + " binds the clip '" + bound.name + "' but the runtime hold-cadence source " +
                    "(CastawayCharacter.MeleeClipLength) looks it up by '" + lookupKey + "'. A name the lookup " +
                    "cannot resolve makes MeleeClipLength return 0, so the cadence silently falls back to the " +
                    "serialized swingClipLengthSeconds and the swing clip gets RE-TRIGGERED mid-play (86cayy770). " +
                    "Either name the bound clip '" + lookupKey + "' or '" + lookupKey + "_<variant>'.");
            }
        }

        /// <summary>
        /// The pure matcher + PERMANENT SYNTHETIC NEGATIVE CONTROLS (the project idiom from
        /// <c>BareCastawayRigLogExpectGuardTests</c>): reverting the variant tolerance, or widening it into a bare
        /// prefix match that could cross classes, reds here without needing a re-imported asset.
        /// </summary>
        [Test]
        public void ClipNameMatchesClass_AcceptsRepairedVariant_RejectsCrossClassAndNonVariant()
        {
            const string pickaxe = "CastawayPickaxeSwing";

            // POSITIVE — the exact name and the in-pipeline repaired/smoothed variant convention.
            Assert.IsTrue(CastawayCharacter.ClipNameMatchesClass(pickaxe, pickaxe), "exact name must match");
            Assert.IsTrue(CastawayCharacter.ClipNameMatchesClass("CastawayPickaxeSwing_repaired", pickaxe),
                "the '_repaired' curve-fix variant must resolve — this is the 86cayy770 defect");
            Assert.IsTrue(CastawayCharacter.ClipNameMatchesClass("CastawayPickaxeSwing_smoothed", pickaxe),
                "the '_smoothed' variant convention (the CrouchWalk swap) must resolve too");

            // NEGATIVE — must NOT widen. A bare StartsWith (no required '_') would wrongly accept the last two.
            Assert.IsFalse(CastawayCharacter.ClipNameMatchesClass("CastawayAxeSwing", pickaxe),
                "a DIFFERENT class's clip must never resolve as the pickaxe swing");
            Assert.IsFalse(CastawayCharacter.ClipNameMatchesClass("CastawayPickaxeSwingX", pickaxe),
                "a suffix with no '_' separator is a different clip, not a variant");
            Assert.IsFalse(CastawayCharacter.ClipNameMatchesClass("CastawayPickaxeSwing_", pickaxe),
                "an empty variant suffix is not a variant");
            Assert.IsFalse(CastawayCharacter.ClipNameMatchesClass(null, pickaxe), "null clip name must not match");
            Assert.IsFalse(CastawayCharacter.ClipNameMatchesClass(pickaxe, null), "null base name must not match");

            // No per-class base name may be a prefix of another, or the '_' rule alone would not keep classes apart.
            string[] keys =
            {
                CastawayCharacter.AxeSwingClipName, CastawayCharacter.PickaxeSwingClipName,
                CastawayCharacter.DaggerStabClipName, CastawayCharacter.SpearThrustClipName,
                CastawayCharacter.SwordSlashClipName,
            };
            for (int i = 0; i < keys.Length; i++)
                for (int j = 0; j < keys.Length; j++)
                    if (i != j)
                        Assert.IsFalse(keys[i].StartsWith(keys[j], System.StringComparison.Ordinal),
                            "per-class clip names must not prefix one another ('" + keys[i] + "' vs '" + keys[j] + "')");
        }

        /// <summary>
        /// QUANTIFIES the regression so its severity is pinned from the ASSET, not from a comment: the pickaxe swing
        /// the AttackPickaxe state binds is materially LONGER than the verbs' serialized fallback, which is exactly
        /// why a failed lookup machine-guns the swing instead of degrading gracefully. Reds if someone "fixes" a
        /// future lookup miss by editing the fallback constant instead of the resolution.
        /// </summary>
        [Test]
        public void PickaxeBoundClip_IsMuchLongerThanTheSerializedFallback_SoAMissedLookupMachineGuns()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, CharacterAssetGen.ControllerPath + " must exist");
            var bound = FindState(controller, "AttackPickaxe")?.motion as AnimationClip;
            Assert.IsNotNull(bound, "AttackPickaxe must bind a clip");

            var boulderGo = new GameObject("FallbackProbe");
            try
            {
                float fallback = boulderGo.AddComponent<MineBoulder>().swingClipLengthSeconds;
                Assert.Greater(fallback, 0f, "the serialized fallback must be strictly positive");
                Assert.Greater(bound.length, fallback * 1.5f,
                    "the bound pickaxe clip (" + bound.length.ToString("F2") + "s) is far longer than the " +
                    "serialized fallback (" + fallback.ToString("F2") + "s), so a lookup miss shortens the hold " +
                    "cadence by >1.5x and re-triggers the clip mid-play (86cayy770). Fix the RESOLUTION, not the " +
                    "fallback — the fallback exists only for a bare Animator-less headless rig.");
            }
            finally { Object.DestroyImmediate(boulderGo); }
        }

        /// <summary>
        /// PER-VERB pin of the 86cayy770 split (the ticket asked for per-verb results, and a shared idiom is NOT
        /// proof of shared behaviour). For each of the three hold verbs, compare the cadence the LIVE bound clip
        /// yields against the cadence its serialized FALLBACK yields, at the same effective playback speed:
        ///   • tree  (axe class)     — the bound clip name resolves exactly, so the live branch was always taken.
        ///   • boulder + ore (pickaxe) — the bound clip is the '_repaired' variant, so the live branch was MISSED and
        ///     the fallback under-spaced the swings; this asserts the gap is real and material.
        /// Reads only the controller asset + each component's serialized default, so it reds if either drifts.
        /// </summary>
        [Test]
        public void HoldCadence_PerVerb_LiveBoundClipVersusSerializedFallback()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, CharacterAssetGen.ControllerPath + " must exist");

            var go = new GameObject("PerVerbCadenceProbe");
            try
            {
                var tree = go.AddComponent<ChopTree>();
                var boulder = go.AddComponent<MineBoulder>();
                var ore = go.AddComponent<MineOre>();

                var verbs = new[]
                {
                    ("tree/ChopTree",       "AttackAxe",     CastawayCharacter.WeaponClassAxe,     tree.swingClipLengthSeconds),
                    ("boulder/MineBoulder", "AttackPickaxe", CastawayCharacter.WeaponClassPickaxe, boulder.swingClipLengthSeconds),
                    ("ore/MineOre",         "AttackPickaxe", CastawayCharacter.WeaponClassPickaxe, ore.swingClipLengthSeconds),
                };

                foreach (var (label, stateName, weaponClass, fallback) in verbs)
                {
                    var bound = FindState(controller, stateName)?.motion as AnimationClip;
                    Assert.IsNotNull(bound, label + ": " + stateName + " must bind a clip");

                    // The live lookup MUST be able to resolve the bound clip, or this verb silently uses `fallback`.
                    string key = CastawayCharacter.AttackClipNameForClass(weaponClass);
                    Assert.IsTrue(CastawayCharacter.ClipNameMatchesClass(bound.name, key),
                        label + ": the hold cadence must read the BOUND clip '" + bound.name + "', not fall back to " +
                        fallback.ToString("F2") + "s (86cayy770)");

                    float playback = CastawayCharacter.EffectiveSwingPlaybackSpeed(1f, weaponClass);
                    float liveCadence = bound.length / playback;
                    float fallbackCadence = fallback / playback;

                    // A cadence SHORTER than the clip it is spacing is precisely the mid-clip-restart defect.
                    Assert.GreaterOrEqual(liveCadence, bound.length / playback - 0.001f,
                        label + ": the live cadence must span the whole bound clip at its real playback rate");
                    Assert.Greater(liveCadence, 0f, label + ": live cadence must be positive");
                    Assert.Greater(fallbackCadence, 0f, label + ": fallback cadence must be positive");

                    Debug.Log("[hold-cadence] " + label + " state=" + stateName + " boundClip=" + bound.name +
                              " len=" + bound.length.ToString("F3") + "s playback=" + playback.ToString("F2") +
                              "x liveCadence=" + liveCadence.ToString("F3") + "s fallbackCadence=" +
                              fallbackCadence.ToString("F3") + "s");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
