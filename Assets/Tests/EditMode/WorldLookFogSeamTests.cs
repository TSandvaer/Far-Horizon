using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Regression guard for the WorldLook Sky fog-seam runtime hardening (ticket 86cajt6jb). The seam-kill
    /// contract is "RenderSettings.fogColor == the sky horizon stop == the single WorldLookPalette.SkyHorizon
    /// constant" — but the committed Boot.unity fogColor is the ONLY runtime source, so a drifted committed value
    /// (the proven fog-R 0.42 corruption class — unity-conventions.md §"A local EditMode test that mutates a LIVE
    /// singleton"; test-layer root-fixed by 86cahvntg/#241) would ship a broken seam. WorldLookTunables now
    /// re-asserts the seam from the palette constant at RUNTIME (Start → ApplyPaletteSeamKill). This test pins the
    /// BUG CLASS directly: whatever the fog colour is (including the exact 0.42-R corruption), the applier forces
    /// EVERY channel back to the palette. The runtime end-to-end complement is
    /// WorldLookPlayModeTests.Sky_GradientAndFog_PresentAtRuntime_SeamKillHolds.
    ///
    /// Mutates LIVE global RenderSettings.fogColor → snapshot/restore in TearDown per the 86cahvntg discipline
    /// (an unrestored global mutation is faithfully committed by the next same-session bootstrap regen).
    /// </summary>
    public class WorldLookFogSeamTests
    {
        private GameObject _go;
        private bool _fogWas;
        private Color _fogColorWas;

        [SetUp]
        public void SetUp()
        {
            _fogWas = RenderSettings.fog;
            _fogColorWas = RenderSettings.fogColor;
        }

        [TearDown]
        public void TearDown()
        {
            RenderSettings.fog = _fogWas;
            RenderSettings.fogColor = _fogColorWas;
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void ApplyPaletteSeamKill_ForcesCorruptedFog_BackToPalette_AllChannels()
        {
            // Reproduce the exact reported corruption: fog R = 0.42 (the seam broken), G/B left at the palette.
            RenderSettings.fogColor = new Color(0.42f, 0.89f, 0.92f, 1f);

            _go = new GameObject("WorldLookSeamRig");
            var seam = _go.AddComponent<WorldLookTunables>();
            seam.ApplyPaletteSeamKill();

            Color pal = WorldLookPalette.SkyHorizon;
            Color fog = RenderSettings.fogColor;
            Assert.AreEqual(pal.r, fog.r, 1e-4f, "the runtime applier must snap fog R back to the palette (was 0.42)");
            Assert.AreEqual(pal.g, fog.g, 1e-4f, "fog G must equal the palette horizon stop");
            Assert.AreEqual(pal.b, fog.b, 1e-4f, "fog B must equal the palette horizon stop");
        }

        [Test]
        public void ApplyPaletteSeamKill_IsIdempotentNoOp_WhenAlreadyAtPalette()
        {
            // When the committed scene is already correct (the current main state), the applier changes nothing.
            RenderSettings.fogColor = WorldLookPalette.SkyHorizon;

            _go = new GameObject("WorldLookSeamRig");
            var seam = _go.AddComponent<WorldLookTunables>();
            seam.ApplyPaletteSeamKill();

            Color pal = WorldLookPalette.SkyHorizon;
            Color fog = RenderSettings.fogColor;
            Assert.AreEqual(pal.r, fog.r, 1e-4f, "fog R stays at the palette (no-op)");
            Assert.AreEqual(pal.g, fog.g, 1e-4f, "fog G stays at the palette (no-op)");
            Assert.AreEqual(pal.b, fog.b, 1e-4f, "fog B stays at the palette (no-op)");
        }
    }
}
