using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Integration guard: the world-look polish (ticket 86ca8t9pq — Uma world-look brief + Erik far-
    /// vista research 86ca8t9rh) actually SHIPS in the production Boot scene — clouds, the vista mountain
    /// ranges, the 3-stop gradient skybox, and the RE-TUNED post/fog dials, all serialized into Boot.unity
    /// by the bootstrap (NOT assembled at runtime — the editor-vs-runtime serialization rule). These are
    /// the editor-side half of the shipped-build capture gate; the windowed orbit-cam capture is the other.
    ///
    /// The Component-in-source-but-not-serialized failure class (unity-conventions.md): a CloudDrift /
    /// cloud mesh / mountain range can be committed + compile clean while the scene never carries it — it
    /// ships silently inert. Binary scenes can't be GUID-grepped, so these EditMode scene-presence asserts
    /// are the only authoritative reader.
    /// </summary>
    public class WorldLookSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

        // ---- CLOUDS (Uma §1) ----

        [Test]
        public void Clouds_Present_FiveToNine_WithDrift()
        {
            var clouds = Object.FindObjectsByType<FarHorizon.CloudDrift>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(clouds.Length, 5,
                "the sky must carry 5-9 drifting clouds (Uma §1 sparse-but-alive) — found " + clouds.Length);
            Assert.LessOrEqual(clouds.Length, 9,
                "clouds must stay SPARSE (5-9, Uma §1: enough to feel alive, few enough to never crowd)");

            foreach (var d in clouds)
            {
                // Each cloud must carry a faceted cloud mesh + drift config.
                var mf = d.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, "a cloud must have a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, "a cloud mesh must be serialized into the scene");
                Assert.Greater(mf.sharedMesh.colors.Length, 0,
                    "the cloud mesh must carry per-vertex cyan COLOURS (the multi-value baked cloud)");
                // Drift: slow lateral, single wind, per-cloud speed in Uma's 0.2-0.5 u/s band.
                Assert.GreaterOrEqual(d.speed, 0.18f, "cloud drift must be slow (>=~0.2 u/s, Uma §1)");
                Assert.LessOrEqual(d.speed, 0.52f, "cloud drift must be slow (<=~0.5 u/s, not stormy, Uma §1)");
                Assert.That(d.windDir.sqrMagnitude, Is.GreaterThan(0.01f), "cloud must have a wind direction");
            }
        }

        [Test]
        public void Clouds_HighOverhead_NotInEyeLine()
        {
            // Uma §1: clouds sit ~30-60u above ground, in the upper third of the orbit frame — never in
            // the player's eye-line. Assert every cloud is well above the play space.
            var clouds = Object.FindObjectsByType<FarHorizon.CloudDrift>(FindObjectsSortMode.None);
            Assert.Greater(clouds.Length, 0, "clouds must exist to check altitude");
            foreach (var d in clouds)
                Assert.Greater(d.transform.position.y, 25f,
                    $"cloud at y={d.transform.position.y:F1} must sit high overhead (>~30u, Uma §1) — not " +
                    "in the player's eye-line");
        }

        [Test]
        public void Clouds_ScaleIsBigButNotCeiling_EightToEighteenX()
        {
            // Uma §1: each cloud spans ~8-18 world-units in its long axis (8-18x the ~1u player). Assert
            // the long-axis size of each serialized cloud mesh sits in that band (allow a little slack).
            var clouds = Object.FindObjectsByType<FarHorizon.CloudDrift>(FindObjectsSortMode.None);
            foreach (var d in clouds)
            {
                var b = d.GetComponent<MeshFilter>().sharedMesh.bounds;
                float longAxis = Mathf.Max(b.size.x, b.size.z);
                Assert.That(longAxis, Is.InRange(6f, 22f),
                    $"cloud long-axis {longAxis:F1}u must be ~8-18x the player (Uma §1 — big enough to read " +
                    "'up there and large', small enough not to be a ceiling)");
            }
        }

        // ---- VISTA (Uma §2 + Erik 86ca8t9rh Route A) ----

        // Helper: every serialized vista peak in the scene.
        private static MeshFilter[] VistaPeaks() =>
            Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "LP_Mountain" && mf.sharedMesh != null)
                .ToArray();

        [Test]
        public void Vista_MountainRanges_Present_NearAndFar()
        {
            var vista = GameObject.Find("Vista");
            Assert.IsNotNull(vista, "the Boot scene must carry the 'Vista' root (the far-horizon silhouettes)");

            var peaks = VistaPeaks();
            Assert.Greater(peaks.Length, 6,
                "the vista must still carry several faceted mountain peaks (a few discrete island clusters)");

            // Depth stack: there must be peaks at BOTH a near band AND a far cluster so the eye ladders out
            // (Uma §2 layered depth) — the constrained-island model keeps depth, just not a full ring.
            float maxDist = 0f, nearCount = 0, farCount = 0;
            foreach (var p in peaks)
            {
                float d = new Vector2(p.transform.position.x, p.transform.position.z).magnitude;
                maxDist = Mathf.Max(maxDist, d);
                if (d >= 150f && d <= 420f) nearCount++;
                if (d >= 480f) farCount++;
            }
            Assert.Greater(nearCount, 0, "there must be a NEAR vista cluster (150-420u, Uma §2)");
            Assert.Greater(farCount, 0, "there must be a FAR vista cluster (>=480u — the layered depth)");
            Assert.Greater(maxDist, 800f,
                "a distant island must reach ~900u+ (the endless-horizon read — one far landmass, not a ring)");
        }

        // ---- SPONSOR SOAK-FIX GUARDS (a89f508 / 86ca8t9pq reopened) — the constrained-island contract ----
        // These pin the TWO Sponsor complaints as the bug class so a regression to the full-encircling-ring
        // design fails in CI: (1) "too many mountains, can't see sky/clouds" -> the peaks must NOT wall the
        // whole horizon (open-sky azimuth gaps required); (2) "mountains on the water" -> the peaks must sit
        // on RAISED land (y>0), not at y=0 over the sea.

        [Test]
        public void Vista_PeaksAreFew_NotAHorizonWall()
        {
            // SPONSOR FIX #1: the first impl shipped 62 peaks across 4 full 360-degree rings — a wall. The
            // constrained model is a SPARSE set of discrete islands. Assert the total peak count stays well
            // below the old wall count so open sky dominates.
            var peaks = VistaPeaks();
            Assert.LessOrEqual(peaks.Length, 30,
                $"the vista carries {peaks.Length} peaks — must stay SPARSE (<=30, was 62 full-ring) so OPEN " +
                "SKY dominates the upper frame and the clouds read (Sponsor soak: 'too many, can't see sky')");
        }

        [Test]
        public void Vista_PeaksDoNotEncircleTheHorizon_OpenSkyGaps()
        {
            // SPONSOR FIX #1 (the structural guard): the OLD design spread peaks evenly around a full 360°
            // ring, so EVERY azimuth sector had a peak (a continuous wall). The constrained model leaves WIDE
            // open-sky/open-sea gaps. Bin every peak into 12 azimuth sectors (30° each) and assert a
            // meaningful number of sectors are EMPTY — proof the horizon is NOT walled all the way around.
            var peaks = VistaPeaks();
            Assert.Greater(peaks.Length, 0, "vista peaks must exist to check encirclement");
            const int sectors = 12;
            var occupied = new bool[sectors];
            foreach (var p in peaks)
            {
                float deg = Mathf.Atan2(p.transform.position.z, p.transform.position.x) * Mathf.Rad2Deg;
                if (deg < 0f) deg += 360f;
                occupied[Mathf.Clamp((int)(deg / (360f / sectors)), 0, sectors - 1)] = true;
            }
            int empty = occupied.Count(o => !o);
            Assert.GreaterOrEqual(empty, 5,
                $"only {sectors - empty}/{sectors} azimuth sectors hold a peak — the horizon must have WIDE " +
                "open-sky gaps (>=5 of 12 sectors empty), NOT a continuous encircling wall (Sponsor soak fix #1)");
        }

        [Test]
        public void Vista_PeaksSitOnRaisedLand_NotAtSeaLevel()
        {
            // SPONSOR FIX #2: "Mountains should not be on the water but on this island or other islands."
            // The old impl placed every peak at y=0 (the sea/shore level) so they read as rising out of the
            // water. Each cluster is now RAISED onto a landmass base (+y) so it reads as LAND. Assert NO peak
            // sits at/below sea level — every one is lifted onto a landmass.
            var peaks = VistaPeaks();
            Assert.Greater(peaks.Length, 0, "vista peaks must exist to check landmass lift");
            foreach (var p in peaks)
                Assert.Greater(p.transform.position.y, 0.5f,
                    $"peak '{p.name}' at y={p.transform.position.y:F2} must sit on RAISED land (>0.5u), not at " +
                    "sea level — mountains rise from land/islands, not the water (Sponsor soak fix #2)");
        }

        [Test]
        public void Vista_FarRingTintsTowardHorizonStop_AtmosphericFade()
        {
            // Uma §2: the farthest range DISSOLVES into the sky — its _Tint is lerped toward the horizon
            // sky stop (#DCE8E4) as the range recedes, so the far silhouette desaturates + lifts toward the
            // colour it dissolves into (the atmospheric fade + the seam-kill colour at the horizon line).
            // Assert the FAR ring's tint is CLOSER to the horizon stop than the NEAR band's tint — the
            // correct fade direction (a multiplicative tint toward the horizon, not toward pure white).
            var peaks = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.gameObject.name == "LP_Mountain")
                .ToArray();
            Assert.Greater(peaks.Length, 0, "vista peaks must exist");

            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            float DistToHorizon(Color c) =>
                Mathf.Abs(c.r - horizon.r) + Mathf.Abs(c.g - horizon.g) + Mathf.Abs(c.b - horizon.b);

            float nearDist = -1f, farDist = -1f;
            foreach (var r in peaks)
            {
                float d = new Vector2(r.transform.position.x, r.transform.position.z).magnitude;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty("_Tint")) continue;
                float toHoriz = DistToHorizon(mat.GetColor("_Tint"));
                if (d <= 280f && nearDist < 0f) nearDist = toHoriz;
                if (d >= 900f) farDist = (farDist < 0f) ? toHoriz : Mathf.Min(farDist, toHoriz);
            }
            Assert.GreaterOrEqual(nearDist, 0f, "a near-band peak with a _Tint must exist");
            Assert.GreaterOrEqual(farDist, 0f, "a far-ring peak with a _Tint must exist");
            Assert.Less(farDist, nearDist,
                "the FAR ring tint must be CLOSER to the horizon stop than the NEAR band — the atmospheric " +
                "fade that makes the farthest range dissolve into the sky with no seam (Uma §2)");
        }

        // ---- SKY-TINT (Uma §3) ----

        [Test]
        public void Skybox_IsThreeStopGradient_WarmHorizon()
        {
            var sky = RenderSettings.skybox;
            Assert.IsNotNull(sky, "a gradient skybox must be assigned");
            // The 3-stop gradient skybox shader (or its proc fallback). If the custom shader resolved,
            // assert all three stops are present + the horizon is WARM (the load-bearing carry-over).
            if (sky.shader != null && sky.shader.name == "FarHorizon/GradientSkybox")
            {
                Assert.IsTrue(sky.HasProperty("_ZenithColor") && sky.HasProperty("_MidColor") &&
                              sky.HasProperty("_HorizonColor"),
                    "the gradient skybox must expose all THREE stops (zenith/mid/horizon)");
                Color zen = sky.GetColor("_ZenithColor");
                Color hor = sky.GetColor("_HorizonColor");
                // Horizon warmer (less blue-dominant) than the zenith — the warm-bright dissolve.
                Assert.Greater(hor.r, zen.r,
                    "the HORIZON stop must be warmer/lighter than the zenith (the warm-bright dissolve, " +
                    "not a cold-white horizon — Uma §3 load-bearing carry-over)");
                // Every stop sub-1.0 (HDR-clamp-safe).
                foreach (var c in new[] { zen, sky.GetColor("_MidColor"), hor })
                    Assert.IsTrue(c.r < 1f && c.g < 1f && c.b < 1f,
                        $"sky stop ({c.r:F2},{c.g:F2},{c.b:F2}) must be sub-1.0 (HDR-clamp-safe)");
            }
        }

        [Test]
        public void Skybox_GradientShader_RegisteredAlwaysIncluded_NoStrip()
        {
            // The custom gradient skybox shader strips from the standalone build unless registered in
            // AlwaysIncludedShaders (the magenta class). Assert it's registered (when it resolves).
            var grad = Shader.Find("FarHorizon/GradientSkybox");
            if (grad == null) Assert.Ignore("gradient skybox shader did not resolve in this editor");
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            Assert.IsNotNull(arr, "GraphicsSettings must expose m_AlwaysIncludedShaders");
            bool registered = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == grad) { registered = true; break; }
            Assert.IsTrue(registered,
                "FarHorizon/GradientSkybox must be in AlwaysIncludedShaders or the sky ships stripped/magenta");
        }

        // ---- FOG RE-TUNE (Uma §3 + Erik seam-kill) ----

        [Test]
        public void Fog_DistanceFade_ColourEqualsHorizonStop_SeamKill()
        {
            Assert.IsTrue(RenderSettings.fog, "distance fog must be enabled (the §2 atmospheric fade)");
            // THE SEAM-KILL (Uma §3 + Erik Q2): fog colour == the horizon sky stop, so the distant
            // mountains fade to the SAME colour the sky fades to — no horizon seam. Bound to the single
            // QualityPassGen.SkyHorizon constant in code; assert the serialized scene matches it exactly.
            Color fog = RenderSettings.fogColor;
            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            Assert.AreEqual(horizon.r, fog.r, 0.01f, "fog R must == the horizon sky stop (seam-kill)");
            Assert.AreEqual(horizon.g, fog.g, 0.01f, "fog G must == the horizon sky stop (seam-kill)");
            Assert.AreEqual(horizon.b, fog.b, 0.01f, "fog B must == the horizon sky stop (seam-kill)");
            // Exp^2 distance-fade (crisp near, accelerating far — Erik Route A / Uma §3 distance-only).
            Assert.AreEqual(FogMode.ExponentialSquared, RenderSettings.fogMode,
                "fog must be Exp^2 (crisp near, accelerating far — the vista atmospherics mode, Erik §1)");
            Assert.Less(RenderSettings.fogDensity, 0.004f,
                "fog must be LIGHT (distance-only — a heavy near-field haze kills the crisp-bright read, Uma §3)");
        }

        // ---- POST RE-TUNE (Uma §3 — re-TUNE the serialized stack, do NOT re-add it) ----

        [Test]
        public void Post_StackStillFullyAddedAndSerialized_NotReBroken()
        {
            // Uma §3 + unity-conventions §Editor-vs-runtime: re-TUNE the dials, do NOT re-add the stack —
            // the VolumeProfile.Add<T> serialization carry (U5/PR #4) must NOT be re-broken. Assert all
            // five components are STILL present in the saved profile after the re-tune (the same guard
            // ZoneDLookTests carries — duplicated here so a world-look change that drops a component fails
            // in THIS suite too).
            var vol = GameObject.Find("ZoneD_PostVolume");
            Assert.IsNotNull(vol, "the post volume must still exist after the world-look re-tune");
            var v = vol.GetComponent<Volume>();
            Assert.IsNotNull(v?.sharedProfile, "the post volume must still reference a serialized profile");
            Assert.IsTrue(v.sharedProfile.Has<Bloom>(), "Bloom must survive the re-tune");
            Assert.IsTrue(v.sharedProfile.Has<UnityEngine.Rendering.Universal.ColorAdjustments>(),
                "ColorAdjustments must survive the re-tune");
            Assert.IsTrue(v.sharedProfile.Has<UnityEngine.Rendering.Universal.WhiteBalance>(),
                "WhiteBalance must survive the re-tune");
            Assert.IsTrue(v.sharedProfile.Has<UnityEngine.Rendering.Universal.Vignette>(),
                "Vignette must survive the re-tune");
            Assert.IsTrue(v.sharedProfile.Has<UnityEngine.Rendering.Universal.Tonemapping>(),
                "Tonemapping must survive the re-tune");
        }

        [Test]
        public void Post_BloomPulledDown_FacetsStayCrisp()
        {
            // Uma §3: bloom DOWN (heavy bloom softens the chunky facet edges + bloom-clips the sub-1.0
            // brights). Assert the serialized Bloom intensity is pulled down from the Zone-D 0.40.
            var v = GameObject.Find("ZoneD_PostVolume").GetComponent<Volume>();
            Assert.IsTrue(v.sharedProfile.TryGet(out Bloom bloom), "Bloom must be in the profile");
            Assert.IsTrue(bloom.intensity.overrideState, "Bloom intensity must be overridden");
            Assert.LessOrEqual(bloom.intensity.value, 0.30f,
                $"Bloom intensity {bloom.intensity.value:F2} must be pulled DOWN (<=0.30, was 0.40) so the " +
                "chunky facet edges stay crisp (Uma §3 — bloom down, board objects aren't glowy)");
        }

        [Test]
        public void Post_GradeLighter_ContrastPulledDown()
        {
            // Uma §3: lighter, neutral-warm grade — pull heavy filmic/contrast DOWN so the saturated toy
            // colours speak. Assert contrast is eased from the Zone-D 12.
            var v = GameObject.Find("ZoneD_PostVolume").GetComponent<Volume>();
            Assert.IsTrue(v.sharedProfile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments cg),
                "ColorAdjustments must be in the profile");
            Assert.LessOrEqual(cg.contrast.value, 8f,
                $"grade contrast {cg.contrast.value:F1} must be eased DOWN (<=8, was 12) — a heavy grade " +
                "muddies the clean toy colours (Uma §3 lighter/neutral-warm)");
            // Warmth is a HARD carry-over — the colour filter must stay warm (R >= B).
            Color f = cg.colorFilter.value;
            Assert.GreaterOrEqual(f.r, f.b,
                "the colour filter must stay WARM (R >= B) — warmth is the hard carry-over (Uma §tonal anchor)");
        }
    }
}
