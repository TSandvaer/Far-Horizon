using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the standard shipped-build capture component (ticket 86ca86g7k).
    ///
    /// The shipped-build capture gate is only meaningful if CaptureGate is actually SERIALIZED
    /// into the Boot scene that the exe ships — not added at Awake (the editor-vs-runtime
    /// serialization trap, unity-conventions.md, would mangle/drop it). This asserts the SAVED
    /// scene carries CaptureGate, so a future change that drops the bootstrap wiring
    /// (`hudGo.AddComponent&lt;CaptureGate&gt;()`) fails HERE in headless CI, not by the capture step
    /// silently inspecting a scene that has no capture component.
    ///
    /// Regression guard: delete the AddComponent line in BootstrapProject.BuildBootScene and this
    /// test goes red.
    /// </summary>
    public class CaptureGateSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesCaptureGate_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            CaptureGate gate = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                gate = root.GetComponentInChildren<CaptureGate>();
                if (gate != null) break;
            }
            Assert.IsNotNull(gate,
                "the Boot scene must carry the CaptureGate component serialized into the scene " +
                "(the shipped-build capture gate inspects exactly these bytes; an Awake-only add " +
                "would not ship — unity-conventions.md editor-vs-runtime trap)");
            Assert.Greater(gate.defaultFrames, 0,
                "CaptureGate.defaultFrames must be a positive frame count");
        }

        // The CASTAWAY CLOSE-UP capture (ticket 86ca8ca1m recolor) must ALSO be serialized into the Boot
        // scene — same component-not-serialized-into-scene failure class: the -verifyCastaway shipped-
        // build identity capture is inert if the scene never carries CastawayVerifyCapture. Regression
        // guard: delete WireCastawayVerifyCapture() in MovementCameraScene and this goes red.
        [Test]
        public void BootScene_CarriesCastawayVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            CastawayVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<CastawayVerifyCapture>(true);
                if (cap != null) break;
            }
            Assert.IsNotNull(cap,
                "the Boot scene must carry CastawayVerifyCapture serialized (the -verifyCastaway identity " +
                "close-up is inert if the scene never carries it — the component-not-serialized trap)");
        }

        // The LIVE FLOAT-DIAGNOSTIC instrument (ticket 86ca8rdkp — "add logging or nudging") must be
        // SERIALIZED onto the Boot object — same component-not-serialized-into-scene class: the F8 overlay
        // (feet/ground/GAP live) + the ~1Hz [FloatTrace] log are inert if the scene never carries it.
        // Regression guard: delete WireFloatDiagnostic() in MovementCameraScene and this goes red.
        [Test]
        public void BootScene_CarriesFloatDiagnostic_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            FloatDiagnostic diag = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                diag = root.GetComponentInChildren<FloatDiagnostic>(true);
                if (diag != null) break;
            }
            Assert.IsNotNull(diag,
                "the Boot scene must carry FloatDiagnostic serialized (the F8 float-gap overlay + the ~1Hz " +
                "[FloatTrace] log are inert if the scene never carries it — the component-not-serialized trap)");
        }

        // The committed shipped-build capture path for the instrument must ALSO be serialized — same class:
        // the -verifyFloatDiag overlay capture is inert if the scene never carries FloatDiagnosticVerifyCapture.
        [Test]
        public void BootScene_CarriesFloatDiagnosticVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            FloatDiagnosticVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<FloatDiagnosticVerifyCapture>(true);
                if (cap != null) break;
            }
            Assert.IsNotNull(cap,
                "the Boot scene must carry FloatDiagnosticVerifyCapture serialized (the -verifyFloatDiag " +
                "overlay capture is inert if the scene never carries it — the component-not-serialized trap)");
        }

        // (The -verifyHair tilt-to-horizon crown-silhouette capture is RETIRED for the Hyper3D castaway —
        // the chibi's procedural-hair-spike soak class does not apply; the new character ships sculpted hair
        // in the mesh. 86ca8rdkp.)

        // FullscreenBoot (now forces WINDOWED mode on a normal launch — BIG ROUND ISLAND N3, 86ca9a7qn; was
        // borderless-fullscreen under 86ca8ce6y FIX3) must be SERIALIZED into the Boot scene — same
        // component-not-serialized-into-scene class. If the scene never carries it, the exe ignores the new
        // windowed mode and a stale persisted fullscreen registry value wins. Regression guard: delete the
        // AddComponent<FullscreenBoot>() line in BootstrapProject.BuildBootScene and this goes red.
        [Test]
        public void BootScene_CarriesFullscreenBoot_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            FullscreenBoot fs = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                fs = root.GetComponentInChildren<FullscreenBoot>(true);
                if (fs != null) break;
            }
            Assert.IsNotNull(fs,
                "the Boot scene must carry FullscreenBoot serialized (else the exe keeps opening in the small " +
                "persisted window on a normal launch — the component-not-serialized trap; unity-conventions.md)");
        }

        // The FLAT-SHADING A/B verify capture (ticket 86caamnjb — _FlatShading ddx/ddy toggle) must be
        // SERIALIZED into the Boot scene — same component-not-serialized-into-scene class: the
        // -verifyFlatShading shipped-build smooth-vs-faceted A/B is inert if the scene never carries it.
        // Regression guard: delete the AddComponent<FlatShadingVerifyCapture>() line in
        // BootstrapProject.BuildBootScene and this goes red.
        [Test]
        public void BootScene_CarriesFlatShadingVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            FlatShadingVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<FlatShadingVerifyCapture>(true);
                if (cap != null) break;
            }
            Assert.IsNotNull(cap,
                "the Boot scene must carry FlatShadingVerifyCapture serialized (the -verifyFlatShading " +
                "smooth-vs-faceted A/B is inert if the scene never carries it — the component-not-serialized trap)");
        }

        // F2 REMOVED — F10 IS THE SINGLE DEBUG-OVERLAY MASTER (86cah90cp round-3, Sponsor-directed 2026-07-03).
        // The legacy #208-era DebugOverlayToggle (F2 master; formerly F1→F2 by the 86cabeqj9 soak NIT) is GONE.
        // F10 (DebugOverlayMaster.overlayToggleKey, which flips the shared DebugOverlays.Visible) is now the ONLY
        // toggle for the debug-overlay layer; F1 (dev console) is untouched. (86caju054 — the F10 master was
        // re-homed off the RETIRED SneakIsolationTool, which owned it before the sneak panel was retired.) This
        // guard asserts (a) NO component serialized on the Boot object binds KeyCode.F2 anymore (F2 is UNBOUND),
        // and (b) the F10 master is present + serialized. Regression guard: re-add an F2 binding, or drop the F10
        // master, and this reds.
        [Test]
        public void BootScene_F2Unbound_F10IsTheOverlayMaster()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            // (a) F2 UNBOUND — no MonoBehaviour on any Boot root has a public KeyCode field set to F2.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    foreach (var f in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (f.FieldType == typeof(KeyCode) && (KeyCode)f.GetValue(mb) == KeyCode.F2)
                            Assert.Fail($"F2 must be UNBOUND (86cah90cp round-3, Sponsor-directed) — " +
                                        $"{mb.GetType().Name}.{f.Name} still binds KeyCode.F2");
                    }
                }
            }

            // (b) F10 IS THE MASTER — DebugOverlayMaster serialized with overlayToggleKey==F10 (it flips the
            // shared DebugOverlays.Visible; the WorldLookNudgeTool also rides F10 so both panels reveal together).
            DebugOverlayMaster master = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                master = root.GetComponentInChildren<DebugOverlayMaster>(true);
                if (master != null) break;
            }
            Assert.IsNotNull(master,
                "the Boot scene must carry DebugOverlayMaster serialized (the F10 debug-overlay master — the " +
                "component-not-serialized trap; unity-conventions.md)");
            Assert.AreEqual(KeyCode.F10, master.overlayToggleKey,
                "F10 must be the SINGLE debug-overlay master (DebugOverlayMaster.overlayToggleKey) after the F2 removal");
        }

        // The FRESNEL/RIM A/B verify capture (ticket 86caamnnj — Fresnel/rim term) must be SERIALIZED into
        // the Boot scene — same component-not-serialized-into-scene class: the -verifyRim shipped-build
        // rim-OFF-vs-dialed A/B is inert if the scene never carries it.
        // Regression guard: delete the AddComponent<RimVerifyCapture>() line in
        // BootstrapProject.BuildBootScene and this goes red.
        [Test]
        public void BootScene_CarriesRimVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            RimVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<RimVerifyCapture>(true);
                if (cap != null) break;
            }
            Assert.IsNotNull(cap,
                "the Boot scene must carry RimVerifyCapture serialized (the -verifyRim rim-OFF-vs-dialed " +
                "A/B is inert if the scene never carries it — the component-not-serialized trap)");
        }

        // The LOOT-PROMPT SHOW-CASE verify capture (ticket 86cafc6ud — Tess QA #158 block: the prompt's SHOW
        // state had no built-frame evidence) must be SERIALIZED into the Boot scene — same component-not-
        // serialized-into-scene class: the -verifyLoot show-case capture (teleport the player into loot range +
        // assert the IMGUI prompt renders) is inert if the scene never carries it. Regression guard: delete the
        // AddComponent<LootPromptVerifyCapture>() line in BootstrapProject.BuildBootScene and this goes red.
        [Test]
        public void BootScene_CarriesLootPromptVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            LootPromptVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<LootPromptVerifyCapture>(true);
                if (cap != null) break;
            }
            Assert.IsNotNull(cap,
                "the Boot scene must carry LootPromptVerifyCapture serialized (the -verifyLoot prompt SHOW-case " +
                "capture is inert if the scene never carries it — the component-not-serialized trap; the prompt's " +
                "rendered-frame evidence is the whole reason the capture seam exists, Tess QA #158)");
        }
    }
}
