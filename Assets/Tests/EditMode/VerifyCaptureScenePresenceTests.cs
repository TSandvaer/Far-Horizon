using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Scene-presence guards for the three verify-capture components that had NONE (ticket 86caz5jxq, raised by
    /// Drew in his PR #369 re-review): <see cref="SwingVerifyCapture"/>, <see cref="MineVerifyCapture"/> and
    /// <see cref="WeaponSetVerifyCapture"/>.
    ///
    /// === WHAT MECHANISM THIS ACTUALLY TESTS (read this before trusting a green) ===
    /// It asserts that the ON-DISK <c>Assets/Scenes/Boot.unity</c> -- the exact file the build step packages, and
    /// the exact file <c>bootstrap_with_retry.sh</c> re-bakes immediately before the EditMode step in the same CI
    /// job -- SERIALIZES each component onto the ACTIVE root GameObject named "Boot", ENABLED. That is the
    /// component-in-source-but-not-serialized-into-scene trap (unity-conventions.md): all three are added
    /// EDITOR-TIME (<c>BootstrapProject.BuildBootScene</c> / <c>MovementCameraScene.Wire*VerifyCapture</c>), never
    /// at Awake, so if the wiring call is dropped the saved scene loses the component and the shipped exe runs
    /// <c>-verifySwings</c>/<c>-verifyMine</c>/<c>-verifyWeaponSet</c> against a scene that contains no capture at
    /// all -- measuring nothing, logging nothing, and greening.
    ///
    /// Binary scenes cannot be GUID-grepped (Boot.unity is BINARY-serialized, not Force-Text YAML -- `file` reports
    /// `data`), so an EditMode open-and-inspect is the only authoritative reader of what the scene really carries.
    ///
    /// === WHAT THIS DOES *NOT* COVER -- do not let a green here stand in for any of these ===
    ///  1. That the component MEASURES anything. Presence is the layer BELOW #369's six-needle evidence check: a
    ///     present component can still take a silent guard path and assert nothing (SwingVerifyCapture's null-
    ///     Animator skip, fixed in #369). The wrapper's log-token needles are what cover that; this is not it.
    ///  2. That CI ever LAUNCHES the exe with the flag. Gate registration (HEADLESS_GATES / WINDOWED_GATES +
    ///     the ci.yml invocation) is the #351 layer and is guarded by the gate-wiring loop in
    ///     tests/scripts/test_gate_scripts.sh -- not here.
    ///  3. That the GIT-COMMITTED Boot.unity blob carries the component. In CI the bootstrap re-bakes the scene
    ///     before this test opens it, so a green here is about the freshly-generated scene. A locally-built exe
    ///     from a checkout that was never bootstrapped ships the committed snapshot
    ///     ([[unity-procedural-committed-assets-go-stale]]) and is NOT covered.
    ///  4. Any of the other verify-capture components. The AC3 audit of the rest is in the PR body; this file
    ///     deliberately fixes only the three the ticket names.
    ///
    /// === THE ANTI-VACUITY LEGS (why presence alone is not enough) ===
    /// A presence-only assert is satisfiable while the component is still inert, in three ways this file closes:
    ///   * DISABLED component or INACTIVE host -> Unity never calls Start(), so the -verify* branch is never
    ///     reached. Presence would be green; the capture would produce nothing. Asserted via enabled +
    ///     activeInHierarchy.
    ///   * DUPLICATE instances -> two coroutines racing the same capture directory. Asserted via a count of 1.
    ///   * PRESENT BUT UNWIRED serialized deps -> Swing/Mine both carry an Awake-time FindAnyObjectByType
    ///     FALLBACK, so a dropped editor wiring does NOT fail bare presence and does NOT fail the runtime
    ///     self-find; it slips past EditMode and only surfaces (opaquely) in the ~20-min capture gate if the
    ///     self-find mis-resolves. Same lever + same idiom as CaptureGateDepsSceneTests (86cafdevx), which
    ///     covers Chop/Campfire/Placement but never covered Mine or Swing.
    ///
    /// Regression guard: delete <c>WireSwingVerifyCapture(player);</c> / <c>WireMineVerifyCapture(player);</c> in
    /// MovementCameraScene.Author, or <c>hudGo.AddComponent&lt;WeaponSetVerifyCapture&gt;();</c> in
    /// BootstrapProject.BuildBootScene, re-bootstrap, and the matching test here goes RED. Demonstrated red for
    /// each of the three (plus the enabled leg and the Mine dep leg) -- mutations + verbatim counts in the PR body.
    /// </summary>
    public class VerifyCaptureScenePresenceTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // The host the three are wired onto: BootstrapProject.BuildBootScene creates `new GameObject("Boot")`
        // (WeaponSetVerifyCapture lands on it directly), and MovementCameraScene's Wire*VerifyCapture helpers
        // resolve the SAME object via GameObject.Find("Boot"). Naming it here is deliberate: "somewhere in the
        // scene" would stay green if a future refactor parked the component on a transient child.
        private const string BootHostName = "Boot";

        private static Scene OpenBoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), BootScenePath + " must open clean");
            // Guard against a vacuous pass: an empty/failed scene load would make every GetComponentsInChildren
            // sweep below return nothing, and a "no instances found" result must never be read as anything but
            // a failure. Asserting roots exist makes the load itself a checked precondition.
            Assert.IsNotEmpty(scene.GetRootGameObjects(),
                BootScenePath + " must load with root GameObjects -- an empty scene would make every sweep below " +
                "vacuously find nothing");
            return scene;
        }

        private static T[] AllInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<T>(true))
                        .ToArray();
        }

        /// <summary>
        /// The shared contract for a shipped-build verify-capture: exactly ONE instance, on the ACTIVE root
        /// GameObject named "Boot", ENABLED -- i.e. serialized into the scene AND on a code path Unity will
        /// actually reach at Start(). Returns the instance so per-component dep asserts can chain off it.
        /// </summary>
        private static T AssertSerializedOnActiveBoot<T>(Scene scene, string verifyFlag) where T : Component
        {
            var all = AllInScene<T>(scene);

            Assert.AreEqual(1, all.Length,
                $"{BootScenePath} must carry EXACTLY ONE {typeof(T).Name} (found {all.Length}). Zero = the " +
                $"component-in-source-but-not-serialized-into-scene trap: the exe would run {verifyFlag} against " +
                $"a scene with no capture component, measure nothing, log nothing, and the gate would green. " +
                $"Two = duplicate coroutines racing the same capture directory.");

            var cap = all[0];

            Assert.AreEqual(BootHostName, cap.gameObject.name,
                $"{typeof(T).Name} must be serialized on the root GameObject named '{BootHostName}' (the host " +
                $"BootstrapProject.BuildBootScene creates and MovementCameraScene.Wire*VerifyCapture resolves via " +
                $"GameObject.Find). Found it on '{cap.gameObject.name}' instead.");

            Assert.IsNull(cap.transform.parent,
                $"{typeof(T).Name}'s host must be a scene ROOT -- a component parked under a transient child can " +
                $"be deactivated or destroyed with its parent without this guard noticing.");

            Assert.IsTrue(cap.gameObject.activeInHierarchy,
                $"{typeof(T).Name}'s host GameObject must be ACTIVE -- Unity never calls Start() on a component " +
                $"of an inactive GameObject, so {verifyFlag} would be silently inert while presence stayed green.");

            var behaviour = cap as Behaviour;
            Assert.IsNotNull(behaviour, $"{typeof(T).Name} must be a Behaviour for the enabled check to apply");
            Assert.IsTrue(behaviour.enabled,
                $"{typeof(T).Name} must be ENABLED -- Start() does not run on a disabled component, so {verifyFlag} " +
                $"would be silently inert while presence stayed green. This is the vacuous-pass leg: absent and " +
                $"present-but-unreachable are indistinguishable from the capture's output (both produce nothing).");

            return cap;
        }

        // === SwingVerifyCapture (-verifySwings, ticket 86caffwv5) ===
        // Wired by MovementCameraScene.WireSwingVerifyCapture(player). This is the component whose null-Animator
        // silent-skip was #369's defect; that fix proves the component does not skip SILENTLY, and this proves the
        // component is there at all.
        [Test]
        public void BootScene_CarriesSwingVerifyCapture_OnActiveBootObject()
        {
            var scene = OpenBoot();
            var cap = AssertSerializedOnActiveBoot<SwingVerifyCapture>(scene, "-verifySwings");

            // Serialized dep. SwingVerifyCapture.Start falls back to FindAnyObjectByType<WasdMovement>() when this
            // is null, so a dropped wiring is INVISIBLE to bare presence and to the runtime -- exactly the masking
            // CaptureGateDepsSceneTests exists to surface, applied to a component it never covered.
            Assert.IsNotNull(cap.player,
                "SwingVerifyCapture.player must be wired editor-time (the WasdMovement the harness drives). A " +
                "dropped wiring leaves the harness self-finding at runtime, which can bind the wrong instance " +
                "and only surfaces in the ~20-min capture gate.");
            var wasd = AllInScene<WasdMovement>(scene).FirstOrDefault();
            Assert.IsNotNull(wasd, "the Boot scene must carry a WasdMovement for the swing harness to drive");
            Assert.AreSame(wasd, cap.player,
                "SwingVerifyCapture.player must be THE scene's WasdMovement (binding-identity, not merely non-null)");
        }

        // === MineVerifyCapture (-verifyMine, ticket 86cakkmr0 / I-2) ===
        // Wired by MovementCameraScene.WireMineVerifyCapture(player), which itself Debug.LogErrors on a null
        // player/mine -- but a LogError in a bootstrap log is not a gate: the console-error gate runs on the build
        // job's Unity log and nothing re-reads it per-dep. This makes the same condition a RED test.
        [Test]
        public void BootScene_CarriesMineVerifyCapture_OnActiveBootObject()
        {
            var scene = OpenBoot();
            var cap = AssertSerializedOnActiveBoot<MineVerifyCapture>(scene, "-verifyMine");

            // All five deps have an Awake-time FindAnyObjectByType fallback (MineVerifyCapture.Start), so each
            // dropped wiring is silent at runtime. Assert the SERIALIZED value, then the binding identity for the
            // two the wiring code itself calls out as load-bearing (player + mine).
            Assert.IsNotNull(cap.player,
                "MineVerifyCapture.player must be wired editor-time (the ClickToMove the harness teleports/drives)");
            Assert.IsNotNull(cap.inventory,
                "MineVerifyCapture.inventory must be wired editor-time (the harness reads iron_ore count to gate PASS)");
            Assert.IsNotNull(cap.mine,
                "MineVerifyCapture.mine must be wired editor-time (the MineOre node the harness RequestMineClick's)");
            Assert.IsNotNull(cap.looter,
                "MineVerifyCapture.looter must be wired editor-time (the PickableLooter that E-loots the ore pile)");
            Assert.IsNotNull(cap.pickaxePickup,
                "MineVerifyCapture.pickaxePickup must be wired editor-time (the grant seam that satisfies the mine gate)");

            var ctm = AllInScene<ClickToMove>(scene).FirstOrDefault();
            var ore = AllInScene<MineOre>(scene).FirstOrDefault();
            Assert.IsNotNull(ctm, "the Boot scene must carry a ClickToMove for the mine harness to drive");
            Assert.IsNotNull(ore, "the Boot scene must carry a MineOre node for the mine harness to break");
            Assert.AreSame(ctm, cap.player,
                "MineVerifyCapture.player must be THE scene's ClickToMove (binding-identity)");
            Assert.AreSame(ore, cap.mine,
                "MineVerifyCapture.mine must be THE scene's MineOre (binding-identity)");
        }

        // === WeaponSetVerifyCapture (-verifyWeaponSet / legacy -verifyWeaponAxe, ticket 86cabh907) ===
        // Added directly by BootstrapProject.BuildBootScene (hudGo.AddComponent<WeaponSetVerifyCapture>()), NOT by
        // a MovementCameraScene Wire* helper. It carries no serialized scene deps -- it loads its lineup from
        // Resources at runtime -- so presence + reachability IS the whole contract on this side. Whether that
        // Resources prefab matches the generator is a DIFFERENT guard (check_committed_lineup.py, PR #370) and is
        // deliberately not duplicated here.
        [Test]
        public void BootScene_CarriesWeaponSetVerifyCapture_OnActiveBootObject()
        {
            var scene = OpenBoot();
            var cap = AssertSerializedOnActiveBoot<WeaponSetVerifyCapture>(scene, "-verifyWeaponSet");

            // The capture writes into <captureDir>/<subDir>; an empty subDir would silently retarget the run's
            // output away from where the wrapper greps for weapon_set.png.
            Assert.IsNotEmpty(cap.subDir,
                "WeaponSetVerifyCapture.subDir must be non-empty -- the wrapper looks for weapon_set.png under it");
        }
    }
}
