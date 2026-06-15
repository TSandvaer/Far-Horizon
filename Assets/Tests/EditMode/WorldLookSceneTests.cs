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
        public void Clouds_Present_SixToTen_WithDrift()
        {
            // DREW SOAK-REWORK: eased the count up to 6-10 (WorldLookConfig.CloudCount{Min,Max}) now the
            // mountain WALL is gone and the sky OPENS — the Sponsor's soak #4 was "I can't see any sky or
            // clouds," so with the sky restored, put a few more up there. Still sparse-but-alive (Uma §1).
            var clouds = Object.FindObjectsByType<FarHorizon.CloudDrift>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(clouds.Length, FarHorizon.WorldLookConfig.CloudCountMin,
                $"the sky must carry {FarHorizon.WorldLookConfig.CloudCountMin}-{FarHorizon.WorldLookConfig.CloudCountMax} " +
                "drifting clouds (Uma §1 sparse-but-alive, eased up post-wall-removal) — found " + clouds.Length);
            Assert.LessOrEqual(clouds.Length, FarHorizon.WorldLookConfig.CloudCountMax,
                $"clouds must stay SPARSE (<= {FarHorizon.WorldLookConfig.CloudCountMax}, Uma §1: alive, never crowding)");

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

            // Depth stack: peaks at BOTH a near band AND a farther cluster so the eye ladders out (Uma §2
            // layered depth). DREW double-fade fix PULLED the clusters in (×0.55) + dropped the 950u ghost;
            // the PALE-FRAME FIX (86ca8t9pq, this pass) then pushed every island BACK OUT so its landmass base
            // can no longer drape over the play space (the near cluster's footprint was burying Ground_Play —
            // the "pale void" regression). The near backing range now grounds ~230-250u; the farthest island
            // ~390-410u. Bands shifted out accordingly (still inside the fog's atmospheric-but-solid window).
            float maxDist = 0f, nearCount = 0, farCount = 0;
            foreach (var p in peaks)
            {
                float d = new Vector2(p.transform.position.x, p.transform.position.z).magnitude;
                maxDist = Mathf.Max(maxDist, d);
                if (d >= 200f && d <= 270f) nearCount++;
                if (d >= 290f) farCount++;
            }
            Assert.Greater(nearCount, 0, "there must be a NEAR backing range (200-270u — grounded clear of the play space, Uma §2)");
            Assert.Greater(farCount, 0, "there must be a FARTHER vista cluster (>=290u — the layered depth)");
            Assert.Greater(maxDist, 290f,
                "the farthest grounded island must reach ~390-410u (atmospheric-but-SOLID, NOT the dropped " +
                "950u fog-ghost that read as a floating translucent shard — Drew double-fade fix)");
            Assert.Less(maxDist, 500f,
                "no cluster may sit beyond ~500u — past that the Exp^2 fog ghosts it back to a translucent " +
                "shard (the 950u Vista_Far was dropped for exactly this reason)");
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
        public void Vista_FartherRingTintsTowardHorizonStop_AtmosphericFade_ButCapped()
        {
            // Uma §2: the farther ranges fade toward the horizon sky stop (#DCE8E4) so the silhouette
            // desaturates + lifts toward the colour it dissolves into. DREW SOAK-REWORK (double-fade fix):
            // the per-cluster tint fade is now CAPPED at WorldLookConfig.MtnFadeCap — the OLD uncapped fade
            // (fadeK up to 0.82) faded the mesh 82% to sky BEFORE the fog then faded it AGAIN ~38-90%, the
            // double-fade that ghosted the far clusters into "floating translucent shards." Two asserts:
            //   (1) the fade DIRECTION is still correct (farther tint CLOSER to the horizon stop than near);
            //   (2) even the farthest tint is NOT washed past the cap — it stays a readable silhouette colour.
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
                if (d <= 270f && nearDist < 0f) nearDist = toHoriz;        // the near inland backing range (~230-250u)
                if (d >= 300f) farDist = (farDist < 0f) ? toHoriz : Mathf.Min(farDist, toHoriz);
            }
            Assert.GreaterOrEqual(nearDist, 0f, "a near-band peak with a _Tint must exist");
            Assert.GreaterOrEqual(farDist, 0f, "a farther-band peak with a _Tint must exist");
            Assert.Less(farDist, nearDist,
                "the farther tint must be CLOSER to the horizon stop than the near band — the atmospheric " +
                "fade direction (Uma §2), preserved under the cap");
            // CAPPED: the farthest tint must NOT be washed all the way to the horizon stop (the double-fade
            // ghost). With MtnFadeCap=0.25 and white -> horizon being ~0.4 total channel distance, the tint
            // can move AT MOST ~0.25*0.4 ≈ 0.10 toward the horizon, so it stays >0.27 away (white is ~0.37).
            Assert.Greater(farDist, 0.22f,
                "even the FARTHEST tint must stay a readable SILHOUETTE colour (>0.22 from the horizon stop) " +
                "— NOT washed to near-invisible (the double-fade 'floating translucent shard' regression). " +
                "The tint is capped at WorldLookConfig.MtnFadeCap so FOG, not the tint, does the recession.");
        }

        [Test]
        public void Vista_EachCluster_SitsOnALandmassBase_NotFloatingPeaks()
        {
            // DREW "floating translucent shards" GROUNDING FIX: each vista cluster must carry a LANDMASS BASE
            // (LP_Landmass) — a broad faceted island shelf the peaks foot on, extending from below the sea up
            // to the cluster's raise height. Without it, peaks on thin +2-6u shelves over fogged-out sea read
            // as floating. Assert there is at least one landmass per cluster + every landmass dips BELOW the
            // sea surface (so the visible coast is the waterline, no gap under the peaks).
            var vista = GameObject.Find("Vista");
            Assert.IsNotNull(vista, "the Vista root must exist");
            var landmasses = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "LP_Landmass" && mf.sharedMesh != null)
                .ToArray();
            // One landmass per cluster (5 clusters in the constrained-grounded model).
            Assert.GreaterOrEqual(landmasses.Length, 4,
                "each vista cluster must carry a landmass base (LP_Landmass) the peaks stand on — found "
                + landmasses.Length + " (the grounding fix for 'floating translucent shards')");
            foreach (var lm in landmasses)
            {
                float worldBottom = lm.GetComponent<MeshRenderer>().bounds.min.y;
                Assert.Less(worldBottom, -0.2f,
                    $"landmass '{lm.name}' bottom (y={worldBottom:F2}) must dip BELOW the sea surface (< WaterY " +
                    "-0.20) so the coast is the waterline — no floating gap under the peaks (Drew grounding fix)");
            }
        }

        [Test]
        public void Vista_LandmassBases_DoNotDrapeOverThePlaySpace_PaleFrameRegressionGuard()
        {
            // REGRESSION GUARD for the PALE-FRAME bug (86ca8t9pq, instrument-confirmed via -hideVista): the
            // feeeaad landmass bases were placed too CLOSE with too LARGE a footprint — Vista_Inland's grey-blue
            // dome (centre ~110u, radius ~118u, near-edge worldZ ≈ -32) DRAPED OVER the entire play terrain
            // (X±45, Z -12..56). At the gameplay orbit the camera saw the dome, not the sand/grass — the "pale
            // void / floating water / elevated" percept (water-Y + sea-extent + occlusion all refuted; hiding
            // the vista restored perfect sand/grass). The landmass has NO collider, so a physics ray passes
            // THROUGH it — only a RENDER-bounds check catches this. Assert every LP_Landmass renderer's XZ
            // bounds clear the play footprint by a margin: a regression that pulls an island back in fails HERE.
            var landmasses = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "LP_Landmass" && mf.sharedMesh != null)
                .Select(mf => mf.GetComponent<MeshRenderer>())
                .Where(r => r != null)
                .ToArray();
            Assert.Greater(landmasses.Length, 0, "vista landmass bases must exist to guard their placement");

            // The play space is X[-45,45], Z[-12,56]. The landmass is a DOME (a circular shelf), so the right
            // test is RADIAL, not an axis-aligned box overlap (a far island's AABB can clip the play box at a
            // corner while the circular dome itself stays well clear). For each landmass, take its XZ centre +
            // its planar radius (max of half-width/half-depth of the renderer bounds) and require the gap from
            // the dome edge to the NEAREST point of the play box to be positive with margin. This is the exact
            // occlusion geometry: gap<0 means the dome covers part of the walkable terrain (the pale frame).
            const float playMaxX = 45f, playMinZ = -12f, playMaxZ = 56f, margin = 15f;
            foreach (var r in landmasses)
            {
                var b = r.bounds;
                float cx = b.center.x, cz = b.center.z;
                float domeR = Mathf.Max(b.extents.x, b.extents.z); // planar dome radius
                float nx = Mathf.Clamp(cx, -playMaxX, playMaxX);    // nearest play-box point to the dome centre
                float nz = Mathf.Clamp(cz, playMinZ, playMaxZ);
                float gap = new Vector2(cx - nx, cz - nz).magnitude - domeR;
                Assert.Greater(gap, margin,
                    $"landmass '{r.gameObject.name}' (centre ({cx:0},{cz:0}) r={domeR:0}) edge is only {gap:0}u from " +
                    "the play space — an island dome that close drapes over the walkable terrain, burying the " +
                    "sand/grass and rendering the gameplay frame pale (the pale-frame regression). Push it out.");
            }
        }

        [Test]
        public void BootScene_CarriesWorldLookNudgeTool_OnTheBootObject()
        {
            // The BUILD-GATED debug WorldLookNudgeTool (the soak-rework dial) must be SERIALIZED onto the Boot
            // object (sibling of AxeNudgeTool / the verify captures) so the Sponsor can finalize the LOOK in
            // the shipped build + report values to bake. It is INERT until F9 (PlayMode inertness test), so its
            // presence does NOT affect a soak. Drop the AddComponent wiring in BootstrapProject -> RED (the
            // component-in-source-but-not-serialized class — binary scenes can't be GUID-grepped).
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the debug tools)");
            Assert.IsNotNull(boot.GetComponent<FarHorizon.WorldLookNudgeTool>(),
                "the Boot object must carry the WorldLookNudgeTool (the build-gated F9 world-look dial), " +
                "serialized into the scene — not Awake-added");
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
