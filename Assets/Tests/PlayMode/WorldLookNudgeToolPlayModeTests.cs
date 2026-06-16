using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the BUILD-GATED debug WorldLookNudgeTool (ticket 86ca8t9pq — the soak-rework
    /// world-look dial). The tool ships so the Sponsor can finalize the LOOK in-game (sky stops / fog /
    /// clouds / mountains), but it MUST be INERT in normal play (asleep behind the F9 toggle) so a normal
    /// soak is unaffected — the same load-bearing safety property as AxeNudgeTool. This proves it does NOT
    /// touch RenderSettings (fog), does NOT move clouds/mountains, and does NOT activate while unpressed.
    ///
    /// (The actual dialing — key-driven mutations + value readout — the Sponsor exercises live in the
    /// shipped build; the harness can't synthesize legacy Input key-downs. The critical test for a SHIPPED
    /// debug tool is that it stays asleep in normal play, which is what this asserts, plus the pure-geometry
    /// panel-placement contract.)
    /// </summary>
    public class WorldLookNudgeToolPlayModeTests
    {
        private GameObject _bootGo;
        private GameObject _cloudGo;
        private GameObject _vistaRoot;
        private GameObject _clusterGo;
        private bool _fogWas;
        private Color _fogColWas;
        private float _fogDensWas;

        [SetUp]
        public void SetUp()
        {
            // A cloud + a vista cluster the tool would resolve + move if active.
            _cloudGo = new GameObject("LP_Cloud");
            _cloudGo.transform.position = new Vector3(20f, 40f, 30f);
            _vistaRoot = new GameObject("Vista");
            _clusterGo = new GameObject("Vista_Island_X");
            _clusterGo.transform.SetParent(_vistaRoot.transform, false);
            _clusterGo.transform.position = new Vector3(200f, 4f, 100f);

            // Record the live render state the tool would mutate (fog).
            _fogWas = RenderSettings.fog;
            _fogColWas = RenderSettings.fogColor;
            _fogDensWas = RenderSettings.fogDensity;

            _bootGo = new GameObject("Boot");
            _bootGo.AddComponent<WorldLookNudgeTool>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_bootGo);
            Object.Destroy(_cloudGo);
            Object.Destroy(_clusterGo);
            Object.Destroy(_vistaRoot);
            RenderSettings.fog = _fogWas;
            RenderSettings.fogColor = _fogColWas;
            RenderSettings.fogDensity = _fogDensWas;
        }

        // NORMAL PLAY (no F9): across many frames the tool stays asleep — clouds + mountains + fog are
        // byte-identical, so a soak sees the shipped baked look, not a tool effect.
        [UnityTest]
        public IEnumerator WorldLookNudgeTool_InertInNormalPlay_DoesNotTouchTheLook()
        {
            Vector3 cloud0 = _cloudGo.transform.position;
            Vector3 cloudScale0 = _cloudGo.transform.localScale;
            Vector3 cluster0 = _clusterGo.transform.position;
            Color fogCol0 = RenderSettings.fogColor;
            float fogDens0 = RenderSettings.fogDensity;

            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(cloud0, _cloudGo.transform.position, "cloud must NOT move in normal play (inert)");
            Assert.AreEqual(cloudScale0, _cloudGo.transform.localScale, "cloud scale must be unchanged (inert)");
            Assert.AreEqual(cluster0, _clusterGo.transform.position, "vista cluster must NOT move (inert)");
            Assert.AreEqual(fogCol0, RenderSettings.fogColor, "fog colour must be untouched in normal play (inert)");
            Assert.AreEqual(fogDens0, RenderSettings.fogDensity, "fog density must be untouched (inert)");
        }

        // KEY-SPLIT regression guard (combined-#48 fix — the Sponsor's two soak panels must not collide). A
        // single F9 used to bring up BOTH the AxeNudgeTool (character: arm/axe/ground-Y) AND the
        // WorldLookNudgeTool (sky/fog/clouds/mountains), and their shared Tab/PageUp/PageDown cross-fired.
        // This pins the two tools to DISTINCT default toggle keys so a regression that re-collides them
        // (e.g. someone re-defaulting world-look back to F9) reds in CI. Asserted on fresh components so it
        // reads the SERIALIZED default (what ships), not an inspector override.
        [Test]
        public void NudgeTools_UseDistinctToggleKeys_NoCollision()
        {
            var axeGo = new GameObject("AxeTool");
            var worldGo = new GameObject("WorldTool");
            try
            {
                var axe = axeGo.AddComponent<AxeNudgeTool>();
                var world = worldGo.AddComponent<WorldLookNudgeTool>();
                Assert.AreNotEqual(axe.toggleKey, world.toggleKey,
                    "the two nudge tools MUST default to DISTINCT toggle keys so the Sponsor's soak panels " +
                    "never collide (a single key brought up BOTH panels + cross-fired their shared Tab/Page " +
                    "keys — combined-#48 key-split fix)");
                // Pin the concrete convention so the doc/UX contract (F9=character, F10=world-look) is guarded.
                Assert.AreEqual(KeyCode.F9, axe.toggleKey, "the AxeNudgeTool (character) must default to F9");
                Assert.AreEqual(KeyCode.F10, world.toggleKey, "the WorldLookNudgeTool must default to F10");
            }
            finally
            {
                Object.Destroy(axeGo);
                Object.Destroy(worldGo);
            }
        }

        // MUTUAL-EXCLUSION guard: with both tools present, activating one forces the other OFF, so only one
        // panel is ever up — its cycle/adjust keys are the only ones that act and the two can NEVER
        // cross-fire even if both toggle keys are pressed in sequence. Drives the public Activate()/IsActive
        // path the toggle uses (the harness can't synthesize the F9/F10 legacy-Input key-downs, so the toggle
        // delegates to Activate() and the test calls it directly).
        [UnityTest]
        public IEnumerator ActivatingOneNudgePanel_ForcesTheOtherOff_NeverBothActive()
        {
            var axeGo = new GameObject("AxeTool");
            var worldGo = new GameObject("WorldTool");
            try
            {
                var axe = axeGo.AddComponent<AxeNudgeTool>();
                var world = worldGo.AddComponent<WorldLookNudgeTool>();
                yield return null;

                // Both start INERT (asleep behind their toggles).
                Assert.IsFalse(axe.IsActive, "axe panel must start inert");
                Assert.IsFalse(world.IsActive, "world panel must start inert");

                // Sponsor brings up the AXE panel (F9): it activates, world stays off.
                axe.Activate();
                Assert.IsTrue(axe.IsActive, "the axe panel must be active after Activate()");
                Assert.IsFalse(world.IsActive, "activating the axe panel must NOT bring up the world panel");

                // Now the Sponsor brings up the WORLD panel (F10) WHILE the axe panel is up — the axe panel
                // MUST be forced off (the cross-fire fix), so only one panel is ever active.
                world.Activate();
                Assert.IsTrue(world.IsActive, "the world panel must be active after Activate()");
                Assert.IsFalse(axe.IsActive,
                    "activating the world panel MUST force the axe panel off (mutual-exclusion — the two " +
                    "panels can never both be up, so their shared Tab/PageUp keys never cross-fire)");
                Assert.IsFalse(axe.IsActive && world.IsActive, "the two panels must never both be active");

                // And symmetrically: re-activating the axe panel forces the world panel off again.
                axe.Activate();
                Assert.IsTrue(axe.IsActive);
                Assert.IsFalse(world.IsActive,
                    "re-activating the axe panel must force the world panel off (exclusion is symmetric)");
            }
            finally
            {
                Object.Destroy(axeGo);
                Object.Destroy(worldGo);
            }
        }

        // OFF-HOTBAR + on-screen guard (same contract as AxeNudgeTool): across screen sizes the panel must
        // not overlap SurvivalHud's bottom-left hotbar and must stay fully on-screen. Pure-geometry.
        [Test]
        public void NudgePanel_ClearsTheHotbar_AndStaysOnScreen_AcrossSizes()
        {
            var sizes = new (float w, float h)[]
            {
                (1920f, 1080f), (1600f, 900f), (1280f, 720f), (1024f, 768f), (800f, 600f),
            };
            foreach (var (w, h) in sizes)
            {
                Rect panel = WorldLookNudgeTool.PanelRect(w, h);
                Rect hotbar = WorldLookNudgeTool.HotbarZone(w, h);
                Assert.IsFalse(panel.Overlaps(hotbar),
                    $"at {w}x{h} the world-look nudge panel {panel} must NOT overlap the hotbar {hotbar}");
                Assert.GreaterOrEqual(panel.x, 0f, $"at {w}x{h} the panel must not run off the left edge");
                Assert.LessOrEqual(panel.xMax, w, $"at {w}x{h} the panel must not run off the right edge");
                Assert.GreaterOrEqual(panel.y, 0f, $"at {w}x{h} the panel must not run off the top edge");
                Assert.LessOrEqual(panel.yMax, h, $"at {w}x{h} the panel must not run off the bottom edge");
            }
        }
    }
}
