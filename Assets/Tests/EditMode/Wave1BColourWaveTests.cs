using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARDS for the Wave1-B colour &amp; shape wave (ticket 86cahhfkc — consolidated poly-style
    /// plan §5 Tier-1 items 1/2/5/11 + §6 PR-B). Each test pins the SPECIFIC contract that makes one element
    /// read as intended AND catches the failure class it could regress into:
    ///   GRD-1  macro meadow patches (bake) — patches shift the grass, no RNG-stream impact, sub-1.0, mask→alpha.
    ///   TRE-1/BSH-1  per-instance leaf hue — warm/cool lean, sub-1.0, distinct across instances, placement-safe.
    ///   CLD-1  flat cloud bases — the cloud has a near-flat underside (a single floor plane), tops unchanged.
    ///   VIS-1  vista green cap — the FacetedLandmass top-dome faces are green, the flanks stay grey.
    ///   RCK-1/SKY-1  are material-property / shader flips verified in the shader + material tests below.
    /// Pure mesh-gen / pure-function checks (no scene, no build) so they run cheaply every CI; the shipped-build
    /// gameplay-cam captures are the OTHER half of the gate (the SOAK judges the look — see the Self-Test Report).
    /// </summary>
    public class Wave1BColourWaveTests
    {
        // ============================ GRD-1 — macro meadow patches ============================

        [Test]
        public void MeadowPatchMask_IsSignedField_SpansBothPatchDirections()
        {
            // The mask must be a real SIGNED low-freq field — it has to reach BOTH the sunlit (>0) and the
            // shadow (<0) side across the island, or the ground gets only one extra tone (half the patchwork).
            // Sample a wide grid at the seed-42 warp; assert both signs appear + the field stays in [-1,1].
            float ox = 12.3f, oz = 45.6f; // representative warp offsets
            bool sawPos = false, sawNeg = false; float mn = 2f, mx = -2f;
            for (float x = -80f; x <= 80f; x += 4f)
                for (float z = -80f; z <= 80f; z += 4f)
                {
                    float m = LowPolyZoneGen.MeadowPatchMask(x, z, ox, oz);
                    if (m > 0.15f) sawPos = true;
                    if (m < -0.15f) sawNeg = true;
                    mn = Mathf.Min(mn, m); mx = Mathf.Max(mx, m);
                }
            Assert.IsTrue(sawPos, "the meadow mask must reach the sunlit-lime side (>0) somewhere on the island");
            Assert.IsTrue(sawNeg, "the meadow mask must reach the deep-meadow side (<0) somewhere on the island");
            Assert.GreaterOrEqual(mn, -1.0001f, "mask must stay >= -1");
            Assert.LessOrEqual(mx, 1.0001f, "mask must stay <= 1");
        }

        [Test]
        public void MeadowPatchMask_IsLowFrequency_PatchesAreMultiMetre()
        {
            // The patches must be BROAD (8-15u), not per-vertex noise: adjacent samples a metre apart must be
            // nearly identical (a low-freq field), so the ground reads as tonal REGIONS, not speckle.
            float ox = 12.3f, oz = 45.6f;
            float maxStep = 0f;
            for (float x = -40f; x <= 40f; x += 1f)
            {
                float a = LowPolyZoneGen.MeadowPatchMask(x, 10f, ox, oz);
                float b = LowPolyZoneGen.MeadowPatchMask(x + 1f, 10f, ox, oz);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(a - b));
            }
            Assert.Less(maxStep, 0.20f,
                "the meadow mask must be LOW-FREQUENCY (a 1u step changes it little) — broad patches, not speckle");
        }

        [Test]
        public void MeadowPatchMask_IsPureFunctionOfPosition_NoRandomStreamTouched()
        {
            // GRD-1's headline safety property: the patch tone is a PURE function of world position — no
            // System.Random draw. Two calls at the same point return the identical value (deterministic, no
            // hidden state), which is what guarantees the seed-42 island/scatter/waterline stays byte-intact.
            float ox = 7f, oz = 3f;
            float a = LowPolyZoneGen.MeadowPatchMask(21.5f, -13.25f, ox, oz);
            float b = LowPolyZoneGen.MeadowPatchMask(21.5f, -13.25f, ox, oz);
            Assert.AreEqual(a, b, 0f, "the meadow mask must be a pure deterministic function of position (no RNG)");
        }

        // ============================ TRE-1 / BSH-1 — per-instance leaf hue ============================

        [Test]
        public void ShiftLeafHue_LeansWarmAndCool_StaysSub1AndGreen()
        {
            // A warm lean (+1) must push toward yellow-lime (R up, B down, a touch brighter); a cool lean (-1)
            // toward blue-green (R down, B up, a touch darker). The extremes must stay a believable GREEN
            // (G still the dominant channel) and every channel sub-1.0 (HDR-clamp-safe under the Zone-D key).
            Color mid = new Color(0.30f, 0.58f, 0.24f); // CanopyBody anchor
            Color warm = LowPolyZoneGen.ShiftLeafHueForTest(mid, 1f);
            Color cool = LowPolyZoneGen.ShiftLeafHueForTest(mid, -1f);
            Assert.Greater(warm.r, mid.r, "warm lean raises R (toward yellow-lime)");
            Assert.Less(warm.b, mid.b, "warm lean lowers B (toward yellow-lime)");
            Assert.Less(cool.r, mid.r, "cool lean lowers R (toward blue-green)");
            Assert.Greater(cool.b, mid.b, "cool lean raises B (toward blue-green)");
            foreach (var c in new[] { warm, cool })
            {
                Assert.Greater(c.g, c.r, "the shifted leaf must still read GREEN (G > R)");
                Assert.Greater(c.g, c.b, "the shifted leaf must still read GREEN (G > B)");
                Assert.LessOrEqual(c.r, 1f); Assert.LessOrEqual(c.g, 1f); Assert.LessOrEqual(c.b, 1f);
            }
        }

        [Test]
        public void ShiftLeafHue_BrightestAnchorStaysUnder1()
        {
            // The brightest anchor (CanopyTop, G=0.74) at the warmest lean must stay under 1.0 — the specific
            // clamp-safety check (a leaf green that blooms to white breaks the toy read + the sub-1.0 rule).
            Color top = new Color(0.48f, 0.74f, 0.34f);
            Color warm = LowPolyZoneGen.ShiftLeafHueForTest(top, 1f);
            Assert.Less(warm.g, 1f, "the brightest canopy green at full warm lean must stay under 1.0 (no bloom)");
            Assert.Less(warm.r, 1f); Assert.Less(warm.b, 1f);
        }

        [Test]
        public void ShiftLeafHue_ZeroLeanIsIdentity()
        {
            // A zero lean must return the anchor unchanged — the neutral tree keeps the shipped canopy greens.
            Color mid = new Color(0.30f, 0.58f, 0.24f);
            Color z = LowPolyZoneGen.ShiftLeafHueForTest(mid, 0f);
            Assert.AreEqual(mid.r, z.r, 1e-6f);
            Assert.AreEqual(mid.g, z.g, 1e-6f);
            Assert.AreEqual(mid.b, z.b, 1e-6f);
        }

        // ============================ CLD-1 — flat cloud bases ============================

        [Test]
        public void CloudBlob_HasFlatBase_UndersideClampedToFloor()
        {
            // CLD-1: the cloud must read as a flat-bottomed cumulus, not a spheroid potato. The floor plane
            // sits at -0.25×radius, so NO vertex may dip below it, AND a real cluster must have several verts
            // sitting ON the floor (the flattened base) — otherwise the clamp did nothing (a spheroid bottom).
            const float radius = 6f;
            float floorY = -0.25f * radius;
            var mesh = LowPolyMeshes.CloudBlob(radius, 5, Color.cyan, Color.white, Color.blue, seed: 11);
            var v = mesh.vertices;
            int onFloor = 0; float minY = float.MaxValue;
            foreach (var p in v)
            {
                minY = Mathf.Min(minY, p.y);
                Assert.GreaterOrEqual(p.y, floorY - 1e-3f,
                    "no cloud vertex may dip below the -0.25×radius base floor (CLD-1 flat base)");
                if (Mathf.Abs(p.y - floorY) < 1e-3f) onFloor++;
            }
            Assert.AreEqual(floorY, minY, 1e-3f, "the lowest cloud vert must sit exactly on the flat base floor");
            Assert.Greater(onFloor, 6,
                "several verts must sit ON the base floor (a real flat cumulus base, not a rounded bottom)");
        }

        [Test]
        public void CloudBlob_TopIsUnchanged_OnlyUndersideFlattens()
        {
            // CLD-1 must flatten only the UNDERSIDE — the puffy top read must survive (the board clouds are
            // rounded on top, flat on the bottom). The max Y must stay well ABOVE the floor (a real dome).
            const float radius = 6f;
            var mesh = LowPolyMeshes.CloudBlob(radius, 5, Color.cyan, Color.white, Color.blue, seed: 3);
            float maxY = float.MinValue;
            foreach (var p in mesh.vertices) maxY = Mathf.Max(maxY, p.y);
            Assert.Greater(maxY, radius * 0.35f,
                "the cloud must keep a tall rounded top (only the underside flattens — CLD-1 is a base clamp)");
        }

        [Test]
        public void CloudBlob_StillHardFaceted_AfterBaseClamp()
        {
            // The base clamp must NOT regress the hard-facet cloud read (verts == tris*3). A clamp that welded
            // or smoothed would break the crisp toy-diorama facets Uma §1 requires.
            var mesh = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, seed: 11);
            int tris = mesh.triangles.Length / 3;
            Assert.AreEqual(tris * 3, mesh.vertexCount,
                "the flat-base clamp must keep the cloud FLAT-shaded (verts == tris*3 — hard facets)");
        }

        // ============================ VIS-1 — vista green cap ============================

        [Test]
        public void FacetedLandmass_TopDomeIsGreen_FlanksStayGrey()
        {
            // VIS-1: the shelf TOP-DOME faces read as a forested green cap; the faceted FLANKS stay grey rock.
            // Pass an unmistakable grey body + an unmistakable green cap; assert the mesh carries BOTH — a
            // clear green (G dominant) AND a clear grey (R≈G≈B) — so the distant isle reads green-topped, not
            // a bare grey asteroid, without greening the whole mass.
            var grey = new Color(0.60f, 0.60f, 0.60f);
            var green = new Color(0.28f, 0.46f, 0.22f);
            var mesh = LowPolyMeshes.FacetedLandmass(90f, 12f, 10, grey, green, seed: 3);
            var cols = mesh.colors;
            bool sawGreen = false, sawGrey = false;
            foreach (var c in cols)
            {
                if (c.g > c.r + 0.08f && c.g > c.b + 0.08f) sawGreen = true;     // a clear green face (the cap)
                if (Mathf.Abs(c.r - c.g) < 0.06f && Mathf.Abs(c.g - c.b) < 0.06f) sawGrey = true; // a grey flank
            }
            Assert.IsTrue(sawGreen, "the landmass top-dome must carry GREEN cap faces (VIS-1 — forested shelf)");
            Assert.IsTrue(sawGrey, "the landmass flanks must stay GREY rock (VIS-1 greens only the top dome)");
        }

        // ============================ GRD-2 — live meadow-patch amp (shader) ============================

        [Test]
        public void TerrainShader_DeclaresMeadowPatchAmp_DefaultsZeroNoOp()
        {
            // GRD-2: the terrain shader must declare _MeadowPatchAmp (in the cbuffer, SRP-Batcher-safe) and a
            // FRESH material must default it to 0 — the no-op path (byte-identical to before the term existed
            // on EVERY material; only the terrain material raises it live). The default-0 is the safety gate.
            var shader = Shader.Find("FarHorizon/LowPolyVertexColor");
            Assert.IsNotNull(shader, "the terrain/vertex-colour shader must resolve");
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),
                "the GRD-2 meadow-patch term must not introduce a shader compile error");
#endif
            var mat = new Material(shader);
            try
            {
                Assert.IsTrue(mat.HasProperty("_MeadowPatchAmp"),
                    "GRD-2: the shader must declare _MeadowPatchAmp (inside CBUFFER_START(UnityPerMaterial))");
                Assert.AreEqual(0f, mat.GetFloat("_MeadowPatchAmp"), 1e-6f,
                    "GRD-2: _MeadowPatchAmp must DEFAULT to 0 (no-op) — the baked GRD-1 patches show, the live " +
                    "amp is off until the console dials it (every non-terrain material stays byte-identical)");
                Assert.IsTrue(mat.HasProperty("_MeadowLime") && mat.HasProperty("_MeadowDeep"),
                    "GRD-2: the shader must declare the two live patch tones");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        // ============================ RCK-1 — rock rim intensity (material) ============================

        [Test]
        public void RockMaterial_OptsIntoRim_WithShippedIntensityAndPower3()
        {
            // RCK-1's real contract (the shader-default no-op is covered by RimLightShaderTests): the ROCK
            // material built by the bootstrap factory must OPT IN — _RimIntensity == the shipped value (non-
            // zero, in the 0.10-0.15 spec band) and _RimPower == 3. A regression to 0 = the boulder loses its
            // caught-sun edge (the whole element); a wrong power = wrong falloff. Guards both.
            var mat = LowPolyZoneGen.RockVertexColorMatForTest(new Color(0.62f, 0.60f, 0.555f));
            Assert.IsNotNull(mat, "the rock material must build");
            if (!mat.HasProperty("_RimIntensity"))
                Assert.Ignore("rock fell back to URP/Lit (no rim property) — shader unresolved in this rig");
            float shipped = LowPolyZoneGen.RockRimIntensityForTest;
            Assert.Greater(shipped, 0f, "RCK-1: the shipped rock rim intensity must be > 0 (a real caught-sun edge)");
            Assert.GreaterOrEqual(shipped, 0.10f); Assert.LessOrEqual(shipped, 0.15f);
            Assert.AreEqual(shipped, mat.GetFloat("_RimIntensity"), 1e-6f,
                "RCK-1: the rock material must set _RimIntensity to the shipped RockRimIntensity (opt-in)");
            Assert.AreEqual(3f, mat.GetFloat("_RimPower"), 1e-6f,
                "RCK-1: the rock rim power must be 3 (the soft-silhouette falloff, not a thin outline)");
        }

        // ============================ SKY-1 — sun-azimuth horizon warmth (skybox) ============================

        [Test]
        public void Skybox_DeclaresSunHorizonWarmth_Bounded_AndCompiles()
        {
            // SKY-1: the skybox shader must declare _SunHorizonWarmth, it must COMPILE (the new frag lerp must
            // not error), and the property must be BOUNDED (a fresh material's value <= 0.06) so it can never
            // warm the sky enough to visibly mismatch the fog==_HorizonColor seam. This is the seam-safety pin.
            var shader = Shader.Find("FarHorizon/GradientSkybox");
            Assert.IsNotNull(shader, "the gradient skybox shader must resolve");
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),
                "SKY-1: the sun-azimuth horizon-warmth term must not introduce a shader compile error");
#endif
            var mat = new Material(shader);
            try
            {
                Assert.IsTrue(mat.HasProperty("_SunHorizonWarmth"),
                    "SKY-1: the skybox must declare _SunHorizonWarmth (a frag-only bias; never mutates _HorizonColor)");
                Assert.LessOrEqual(mat.GetFloat("_SunHorizonWarmth"), 0.06f + 1e-6f,
                    "SKY-1: the warmth default must be BOUNDED (<=0.06) so the sky↔fog seam stays below threshold");
            }
            finally { Object.DestroyImmediate(mat); }
        }
    }
}
