using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the BUILD-GATED SNEAK-WALK ISOLATION tool (ticket 86caa3kur re-soak attempt-3 —
    /// the /unstick instrument). The tool lets the Sponsor isolate the residual per-gait-cycle sneak jerk by
    /// eye: F5 toggles #186 foot-sync, F6 snaps sneak→walk speed, with a live readout.
    ///
    /// These pin the load-bearing contracts WITHOUT a play loop (headless Time.deltaTime≈0 stalls the Animator —
    /// the documented trap; the toggle STATE/plumbing + placement are the testable surface, NOT the visual):
    ///   1. SCENE PRESENCE — the tool must SERIALIZE onto Boot (the component-in-source-but-not-in-scene trap;
    ///      a missing component ships the handle inert). Binary scenes can't be GUID-grepped, so this is the
    ///      authoritative reader. (CI bootstraps before EditMode, so the freshly-baked scene carries it; a bare
    ///      LOCAL run against a stale committed Boot.unity may red here until the scene is regenerated — see
    ///      unity-conventions.md §"Run BootstrapProject.Run BEFORE any LOCAL EditMode run".)
    ///   2. DANISH-SAFE KEYS, NO COLLISION — F5/F6 (F-keys, layout-agnostic) distinct from every other dev key
    ///      (F1 master / F7 camera / F8 float / F9 axe / F10 world; F2/F3 vacated — F2 now hosts #208 overlays).
    ///   3. PANEL placement — RIGHT-anchored so it never overlaps the LEFT-anchored F8 FloatDiagnostic panel
    ///      (both can be up at once behind the F1 master).
    /// </summary>
    public class SneakIsolationToolSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private Scene OpenBoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            return scene;
        }

        private T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        [Test]
        public void BootScene_HasSneakIsolationTool_OnBoot()
        {
            var scene = OpenBoot();
            var tool = FindInScene<SneakIsolationTool>(scene);
            Assert.IsNotNull(tool,
                "the Boot scene must carry the SneakIsolationTool (the 86caa3kur attempt-3 /unstick handle) — a " +
                "missing component ships the F5/F6 isolation toggles inert (the component-in-source-but-not-in-" +
                "scene trap; this scene-presence assert is the authoritative reader for the binary scene).");
        }

        [Test]
        public void SneakIsolationTool_TogglesAreDanishSafeFKeys_NoCollision()
        {
            var scene = OpenBoot();
            var tool = FindInScene<SneakIsolationTool>(scene);
            Assert.IsNotNull(tool);
            Assert.AreEqual(KeyCode.F5, tool.footSyncToggleKey,
                "foot-sync must toggle on F5 — a Danish-keyboard-safe F-key ([[sponsor-danish-keyboard-layout]]). " +
                "Moved off F2 (Sponsor-directed) — F2 now hosts #208's legacy overlays.");
            Assert.AreEqual(KeyCode.F6, tool.sneakSpeedSnapToggleKey,
                "sneak-speed snap must toggle on F6 — a Danish-keyboard-safe F-key. Moved off F3.");
            // No collision with the established dev keys (F1 master / F2 #208 overlays / F7 camera / F8 float /
            // F9 axe / F10 world). F2/F3 are explicitly checked as taken now (F2 = #208 legacy overlays).
            Assert.AreNotEqual(tool.footSyncToggleKey, tool.sneakSpeedSnapToggleKey,
                "the two isolation toggles must be DISTINCT keys.");
            foreach (var taken in new[] { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10 })
            {
                Assert.AreNotEqual(taken, tool.footSyncToggleKey,
                    "the foot-sync key must not collide with the existing dev key " + taken);
                Assert.AreNotEqual(taken, tool.sneakSpeedSnapToggleKey,
                    "the sneak-speed key must not collide with the existing dev key " + taken);
            }
        }

        // The isolation panel is RIGHT-anchored; the F8 FloatDiagnostic panel is LEFT-anchored. They must not
        // overlap so the Sponsor can have both up at once (the readout + the float gauge) behind the F1 master.
        [Test]
        public void PanelRect_DoesNotOverlapTheLeftAnchoredFloatDiagnosticPanel()
        {
            const float w = 1920f, h = 1080f;
            Rect iso = SneakIsolationTool.PanelRect(w, h);
            Rect floatPanel = FloatDiagnostic.PanelRect(w, h);
            Assert.IsFalse(iso.Overlaps(floatPanel),
                "the right-anchored SneakIsolation panel must NOT overlap the left-anchored FloatDiagnostic (F8) " +
                "panel — both can be up at once behind the F1 master. iso=" + iso + " float=" + floatPanel);
            // Right-anchored: the panel's right edge sits within a small margin of the screen's right edge.
            Assert.GreaterOrEqual(iso.xMax, w - 24f,
                "the SneakIsolation panel must be RIGHT-anchored (right edge near the screen edge).");
        }

        [Test]
        public void PanelRect_StaysOnScreen_OnANarrowWindow()
        {
            // A window narrower than the full panel width must clamp the box fully on-screen (no off-screen draw).
            Rect r = SneakIsolationTool.PanelRect(240f, 320f);
            Assert.GreaterOrEqual(r.xMin, 0f, "panel left edge on-screen");
            Assert.LessOrEqual(r.xMax, 240f + 1f, "panel right edge on-screen");
            Assert.GreaterOrEqual(r.yMin, 0f, "panel top edge on-screen");
            Assert.LessOrEqual(r.yMax, 320f + 1f, "panel bottom edge on-screen");
        }
    }
}
