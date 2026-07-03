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
    ///      (F1 console / F7 camera / F8 float / F9 axe / F10 the SINGLE debug-overlay master; F2/F3 vacated and
    ///      now UNBOUND — the legacy F2 overlay master (DebugOverlayToggle) was removed, 86cah90cp round-3).
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
                "Moved off F2 (Sponsor-directed); F2 is now UNBOUND (the legacy F2 overlay master was removed, 86cah90cp round-3).");
            Assert.AreEqual(KeyCode.F6, tool.sneakSpeedSnapToggleKey,
                "sneak-speed snap must toggle on F6 — a Danish-keyboard-safe F-key. Moved off F3.");
            // The F5/F6 SUB-toggles must not collide with the established dev keys (F1 console / F7 camera /
            // F8 float / F9 axe / F10 the SINGLE debug-overlay master). F2/F3 are kept in the avoid-set below
            // even though they are now UNBOUND (the legacy F2 master was removed, 86cah90cp round-3) so the
            // sub-toggles never re-collide with the historically-reserved keys. The overlayToggleKey is
            // DELIBERATELY F10 (grouped with the WorldLookNudgeTool, 86cah90cp) so it is excluded from this loop
            // and asserted separately below.
            Assert.AreNotEqual(tool.footSyncToggleKey, tool.sneakSpeedSnapToggleKey,
                "the two isolation SUB-toggles must be DISTINCT keys.");
            foreach (var taken in new[] { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10 })
            {
                Assert.AreNotEqual(taken, tool.footSyncToggleKey,
                    "the foot-sync key must not collide with the existing dev key " + taken);
                Assert.AreNotEqual(taken, tool.sneakSpeedSnapToggleKey,
                    "the sneak-speed key must not collide with the existing dev key " + taken);
            }
        }

        // Ticket 86cah90cp item 3: the Sponsor asked for the debug overlays GROUPED on F10 with the
        // WorldLookNudgeTool (also F10) — "so F10 toggles the debug overlays together" — and F1 kept FREE (F1 is
        // the dev console). The panel's stale on-screen title once read "F1 to hide" (a leftover from before the
        // #208 F1→F2 master move); the show/hide is now F10, flipping the shared DebugOverlays.Visible master. A
        // regression that re-binds this to F1 (re-consuming the console key) fails HERE.
        [Test]
        public void SneakIsolationTool_ShowHide_IsF10_GroupedWithWorldLook_F1IsFree()
        {
            var scene = OpenBoot();
            var tool = FindInScene<SneakIsolationTool>(scene);
            Assert.IsNotNull(tool);
            Assert.AreEqual(KeyCode.F10, tool.overlayToggleKey,
                "the sneak-isolation overlay show/hide must be F10 — grouped with the WorldLookNudgeTool (also " +
                "F10) so F10 toggles the debug overlays together (86cah90cp).");
            Assert.AreNotEqual(KeyCode.F1, tool.overlayToggleKey,
                "F1 must NOT be consumed by the sneak-isolation overlay — F1 is the dev console (86cah90cp).");
            // Same F10 as the WorldLookNudgeTool's default toggle — the grouping is the point.
            var world = new GameObject("WorldTool").AddComponent<WorldLookNudgeTool>();
            try
            {
                Assert.AreEqual(world.toggleKey, tool.overlayToggleKey,
                    "the sneak-isolation overlay show/hide must share the WorldLookNudgeTool's F10 (the grouping).");
            }
            finally { Object.DestroyImmediate(world.gameObject); }
        }

        // Ticket 86cah90cp item 2: the World-Look Nudge panel and the Sneak-Isolation panel are BOTH right-
        // anchored and BOTH revealed by the F10 master, so both can be up at once — they must NOT overlap (the
        // Sponsor's 2026-07-01 soak: the world-look box overlapped / sat behind the sneak box, both bottom-right).
        // The world-look panel moved to the UPPER portion; assert non-overlap across shipped resolutions.
        [Test]
        public void WorldLookPanel_DoesNotOverlapSneakIsolationPanel_AcrossSizes()
        {
            var sizes = new (float w, float h)[]
            {
                (1920f, 1080f), (1600f, 900f), (1366f, 768f), (1280f, 720f), (1024f, 768f),
            };
            foreach (var (w, h) in sizes)
            {
                Rect world = WorldLookNudgeTool.PanelRect(w, h);
                Rect sneak = SneakIsolationTool.PanelRect(w, h);
                Assert.IsFalse(world.Overlaps(sneak),
                    $"at {w}x{h} the World-Look Nudge panel {world} must NOT overlap the Sneak-Isolation panel " +
                    $"{sneak} — both are right-anchored + revealed by F10, so both can be up together (86cah90cp). " +
                    "The world-look panel is anchored UP; the sneak panel sits lower-right.");
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
