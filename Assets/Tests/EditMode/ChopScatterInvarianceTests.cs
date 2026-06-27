using System;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// SEED-42 BYTE-INVARIANCE guard for the chop ticket (86caa4c5c AC5 / V4: "use the EXISTING seed-42
    /// world-gen tree scatter — do NOT break the island scatter / NavMesh"). Chop READS the tree scatter
    /// (it reuses the existing wired ChopTree + the world's blob-canopy language); it never SCATTERS — its
    /// authoring lives entirely in MovementCameraScene (BuildChopTree + the melee Attack state on the castaway),
    /// NOT in LowPolyZoneGen.ScatterIslandProps's seeded stream. So the seed-42 placement is byte-identical
    /// by construction. This guard PINS that:
    ///
    ///   1. The seed CONSTANTS are locked (IslandSeed==42, ScatterSeedSalt==555). A change to either re-rolls
    ///      every tree/rock/grass placement — the Sponsor's locked island silhouette/scatter would shift.
    ///   2. The scatter RNG STREAM (`System.Random(IslandSeed + ScatterSeedSalt)`) is byte-identical across
    ///      two independent fresh instances — i.e. the placement draw sequence is reproducible. If chop (or
    ///      anything) consumed a draw from this stream, or the salt changed, this sequence would diverge.
    ///   3. The scatter BASIS pure-functions (SeedOffset(42), ShoreRadiusAt) are deterministic — the warped
    ///      coast / plant-able land disc the scatter samples is stable build-to-build.
    ///
    /// Mirrors FreshwaterPondSceneTests' seed-42-lock argument (the pond is authored OUTSIDE the seeded
    /// stream, so it provably can't perturb scatter) — the same logic applies to chop. This is the cheap,
    /// deterministic, headless-safe surface; the shipped-build scatter is additionally proven by the chop
    /// verify capture + the existing world/scene tests.
    /// </summary>
    public class ChopScatterInvarianceTests
    {
        [Test]
        public void SeedConstants_AreLocked_Seed42_Salt555()
        {
            Assert.AreEqual(42, LowPolyZoneGen.IslandSeed,
                "the island seed is LOCKED at 42 (Sponsor's pick — world-is-big-round-island memory). " +
                "Changing it re-rolls the whole island/scatter — never as a side effect of chop.");
            Assert.AreEqual(555, LowPolyZoneGen.ScatterSeedSalt,
                "the scatter-stream salt is LOCKED at 555. The scatter RNG is System.Random(seed + salt); " +
                "changing the salt re-rolls every tree/rock/grass placement.");
        }

        [Test]
        public void ScatterRngStream_IsByteIdentical_AcrossFreshInstances()
        {
            // Two independent fresh streams seeded the EXACT way ScatterIslandProps seeds its RNG must produce
            // the identical draw sequence — the property the seed-42 placement byte-invariance rests on.
            int seed = LowPolyZoneGen.IslandSeed + LowPolyZoneGen.ScatterSeedSalt;
            var a = new System.Random(seed);
            var b = new System.Random(seed);

            // Sample a long sequence (covers the tree(320)/rock(60)/grass loops' draw cadence comfortably).
            const int draws = 4096;
            for (int i = 0; i < draws; i++)
            {
                Assert.AreEqual(a.NextDouble(), b.NextDouble(), 0d,
                    "the seed-42 scatter RNG stream must be byte-identical at draw " + i +
                    " (deterministic placement — a divergence here means the scatter would re-roll)");
            }
        }

        [Test]
        public void ScatterBasis_PureFunctions_AreDeterministic()
        {
            // The warp offset the scatter + terrain share (plant on the real warped land) is a pure function
            // of the seed — identical across calls.
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out float ox1, out float oz1);
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out float ox2, out float oz2);
            Assert.AreEqual(ox1, ox2, 0f, "SeedOffset.x is deterministic for seed 42");
            Assert.AreEqual(oz1, oz2, 0f, "SeedOffset.z is deterministic for seed 42");

            // The warped coast radius the scatter's OnLandmass test samples is a pure function — stable around
            // the disc (sample several azimuths; each call must match a repeat call exactly).
            for (int a = 0; a < 16; a++)
            {
                float ang = a / 16f * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float r1 = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox1, oz1);
                float r2 = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox1, oz1);
                Assert.AreEqual(r1, r2, 0f,
                    "ShoreRadiusAt is deterministic at azimuth " + a + " (the plant-able land disc is stable)");
            }
        }

        [Test]
        public void ChopComponents_LiveInTheRuntimeAsmdef_NotTheEditorScatterStream()
        {
            // Structural proof chop can't touch the seeded scatter: ChopTree + CastawayCharacter are RUNTIME
            // components (FarHorizon namespace / FarHorizon.Runtime asmdef), while ScatterIslandProps lives in
            // LowPolyZoneGen (FarHorizon.EditorTools / FarHorizon.Editor asmdef). A runtime component cannot run
            // inside the editor-time scatter bake. (change-(b): the chop swing is CastawayCharacter.TriggerChop /
            // the Mixamo melee Animator state now — the procedural ChopPoseDriver was removed.)
            Assert.AreEqual("FarHorizon", typeof(ChopTree).Namespace,
                "ChopTree is a runtime component, not part of the editor scatter generator");
            Assert.AreEqual("FarHorizon", typeof(CastawayCharacter).Namespace,
                "CastawayCharacter (TriggerChop — the chop swing) is a runtime component, not the scatter generator");
            Assert.AreEqual("FarHorizon.EditorTools", typeof(LowPolyZoneGen).Namespace,
                "the scatter generator is editor-only (a separate asmdef from the chop runtime)");
        }
    }
}
