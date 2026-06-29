using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// The Sponsor-approved "Zone D" QUALITY PASS — the actual look target (verbatim: "i love zone D
    /// + quality"). Ported from the eval spike (EmbergraveUnitySlice,
    /// Assets/Scripts/Editor/QualityPassGen.cs, iter-5 — READ-ONLY working spec per ticket 86ca86fux).
    ///
    /// In the spike this was a LOCAL post Volume scoped to a side-by-side comparison vignette (the
    /// Sponsor walked A/B/C then stepped into D and the post kicked in). In PRODUCTION there is only
    /// ONE play space, so the Zone-D quality pass is the BASELINE look everywhere:
    ///   - warm directional key + cool ambient fill (applied in WorldBootstrap.BuildLighting),
    ///   - a soft gradient SKYBOX (warm horizon -> cool sky),
    ///   - global depth FOG (warm haze),
    ///   - a GLOBAL post-processing Volume (Bloom + warm Color Grading + WhiteBalance + Vignette +
    ///     filmic Tonemap) so the whole play space reads "graded/premium", not raw lit low-poly.
    /// </summary>
    public static class QualityPassGen
    {
        const string SettingsDir = "Assets/Settings";

        // ---- WORLD-LOOK POLISH sky-tint anchors (ticket 86ca8t9pq — Uma world-look brief §3) ----
        // The 3-stop warm-bright vertical gradient: clean open-day sky (NOT cold-clear, NOT hazy-warm).
        // All sub-1.0 / HDR-clamp-safe (Uma per-swatch verification — a pure-white horizon + bloom would
        // bloom-clip). The canonical values live in FarHorizon.WorldLookPalette (the RUNTIME layer) so
        // the editor bootstrap + the runtime/PlayMode seam-kill guards read the SAME constants — fog
        // colour == horizon sky stop relies on an EXACT match (Erik Q2: tune in lockstep). Forwarded here
        // so existing editor call sites read them locally.
        public static readonly Color SkyZenith  = FarHorizon.WorldLookPalette.SkyZenith;  // #7FB4D6
        public static readonly Color SkyMid      = FarHorizon.WorldLookPalette.SkyMid;     // #AAD0E2
        public static readonly Color SkyHorizon  = FarHorizon.WorldLookPalette.SkyHorizon; // #DCE8E4 (seam-kill anchor)

        // ---- SUN-DISK defaults (ticket 86cabc743 — Erik low-poly-sky research, POC item 2) ----
        // Warm-gold toy-like sun disk in the GradientSkybox. Starting values for the Sponsor soak; the
        // Sponsor dials hardness/size. Additive in the shader → the post Bloom lifts a soft warm corona.
        // SATURATED warm gold, retuned after the shipped -verifySky capture: the first warm-white
        // (1.0,0.92,0.70) blew the additive disk core to WHITE through the bloom+filmic tonemap (the disk
        // read blue-white, not a sun). A deeper, more saturated amber-gold with R clearly dominating B
        // survives the tonemap so the disk reads unmistakably WARM. Still the dial-from soak value.
        public static readonly Color SunColor   = new Color(1.0f, 0.74f, 0.34f, 1f); // saturated amber-gold
        // SIZE/HARDNESS retuned after the shipped -verifySky capture: the original 0.9985/120 rendered the
        // disk as a sub-pixel PINPOINT (the dot in the first sky_sun.png) — far too small to read as a sun.
        // 0.992 puts the disk edge at ~7deg (a chunky toy sun, board-scale) and hardness 60 keeps a crisp
        // low-poly edge with a touch of softness for the bloom corona. These remain the dial-from soak values.
        public const float SunSize     = 0.992f; // disk edge ~7deg in dot space (1.0 = a point; lower = bigger)
        public const float SunHardness = 60f;     // crisp-but-not-pinpoint low-poly disk edge

        // Build the 3-STOP GRADIENT SKYBOX (Uma world-look brief §3). RE-TUNES the Zone-D 2-color
        // Skybox/Procedural toward Uma's clean warm-bright 3-stop vertical gradient via a dedicated
        // FarHorizon/GradientSkybox shader (zenith -> mid -> warm horizon). The custom shader is
        // registered in AlwaysIncludedShaders so it does not strip in the standalone build (the spike's
        // magenta class). Falls back to the built-in Skybox/Procedural (2-color, horizon-warm) if the
        // custom shader is somehow unresolved — never a broken/magenta sky.
        public static void BuildGradientSkybox()
        {
            var grad = Shader.Find("FarHorizon/GradientSkybox");
            Material sky;
            if (grad != null)
            {
                EnsureShaderAlwaysIncluded(grad); // do not strip the sky in the build
                sky = new Material(grad) { name = "GradientSky" };
                sky.SetColor("_ZenithColor", SkyZenith);
                sky.SetColor("_MidColor", SkyMid);
                sky.SetColor("_HorizonColor", SkyHorizon);
                // CHEERFUL-SKY SOAK-FIX (86ca8t9pq S2): LOWER the mid-point 0.35 -> 0.18 so the saturated
                // cheerful MID blue drops into the LOW dir.y band the gameplay over-shoulder orbit (pitch 55)
                // actually frames — at 0.35 the cheerful blue sat too high (overhead-only) and the orbit saw
                // only the pale horizon->mid blend ("greyish blue"). A wider Softness keeps the lowered blend
                // smooth (no banding seam) so the cheerful blue eases into the warm horizon haze.
                sky.SetFloat("_MidPoint", 0.18f);
                sky.SetFloat("_Softness", 0.85f);
                // SUN DISK (ticket 86cabc743 — Erik low-poly-sky research, POC item 2). Warm-gold starting
                // values for the Sponsor soak; additive in the shader so the post Bloom lifts a soft corona.
                // The sun appears where the view ray faces the Sun's direction (the warm directional key at
                // Quaternion.Euler(48,-35,0)); these are the dial-from defaults, NOT a final-tuned value.
                sky.SetColor("_SunColor", SunColor);
                sky.SetFloat("_SunSize", SunSize);
                sky.SetFloat("_SunHardness", SunHardness);
                // BAKE the world-space direction TOWARD the Sun into the material. The URP _MainLightPosition
                // global is NOT bound in the Background/skybox pass (verified empirically in the shipped exe
                // via -verifySky — the first attempt rendered NO disk because the dot used an unbound global),
                // so the disk direction must come from a material property. BuildLighting runs BEFORE this, so
                // the "Sun" key exists; to-sun = -light.forward. Fall back to the known bootstrap Euler if the
                // Sun is somehow absent so the asset still reads sanely if opened raw.
                Vector3 toSun = ResolveSunDirection();
                sky.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
            }
            else
            {
                var proc = Shader.Find("Skybox/Procedural");
                sky = new Material(proc) { name = "GradientSky" };
                sky.SetFloat("_SunSize", 0.03f);
                sky.SetFloat("_AtmosphereThickness", 1.0f);
                sky.SetColor("_SkyTint", SkyZenith);    // warm-bright upper sky
                sky.SetColor("_GroundColor", SkyHorizon); // warm horizon
                sky.SetFloat("_Exposure", 1.0f);
                Debug.LogWarning("[QualityPassGen] FarHorizon/GradientSkybox unresolved; falling back to Skybox/Procedural (2-color)");
            }
            AssetDatabase.CreateAsset(sky, SettingsDir + "/GradientSky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox; // sky drives ambient (warm-bright fill)
            DynamicGI.UpdateEnvironment();
            Debug.Log("[QualityPassGen] 3-stop gradient skybox assigned (warm-bright, Uma §3)");
        }

        // World-space direction TOWARD the Sun disk (= -light.forward of the warm "Sun" directional key
        // WorldBootstrap.BuildLighting added before this runs). Baked into the sky material's _SunDirection
        // because the URP _MainLightPosition global is unbound in the Background/skybox pass (the empirical
        // -verifySky finding). Falls back to the known bootstrap Sun Euler (48,-35,0) if no Sun is found.
        static Vector3 ResolveSunDirection()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject.name == "Sun")
                {
                    var d = -l.transform.forward;
                    Debug.Log($"[QualityPassGen] sun-disk direction baked from the 'Sun' key: {d}");
                    return d.normalized;
                }
            Vector3 fallback = -(Quaternion.Euler(48f, -35f, 0f) * Vector3.forward);
            Debug.LogWarning($"[QualityPassGen] no 'Sun' key found — baking sun-disk direction from the " +
                             $"known bootstrap Euler (48,-35,0): {fallback}");
            return fallback.normalized;
        }

        // Enable the warm-haze global fog. In production (single play space) the Zone-D fog is the
        // baseline atmosphere, not a per-zone scoped effect — so it is global at the spike's gentle
        // density (the spike kept global density low + let the local Volume add a densification feel;
        // here the global value IS the look).
        public static void EnableGlobalFog()
        {
            RenderSettings.fog = true;
            // EXPONENTIAL-SQUARED distance-fade fog (Erik far-vista research 86ca8t9rh, Route A — the
            // Strong-sourced standard for vista atmospherics: 2^(-(cd)^2) keeps the IMMEDIATE play space
            // CRISP, then accelerates density into a dense haze band at distance — exactly Uma §3's "crisp
            // to mid-distance, fog engages only in the far band" intent + the §2 atmospheric fade that
            // dissolves the distant mountain rings into the sky). Reconciles Uma's "distance-only" intent
            // with Erik's mode recommendation: a low Exp² density stays clear across the near/mid field
            // and the NEAR vista ring (150-400u) keeps silhouette contrast, while the FAR ring (~1000u)
            // reads faintly misty — a hard Linear end-distance would instead abruptly clip the far rings.
            // Density 0.0016 tuned so the near vista ring keeps crisp facets and the far ring dissolves
            // (Erik's "start at 0.0003 and walk up"; the play space is ~200u so a higher value than his
            // 1000u-ring assumption is needed to engage across the nearer 150-400u near-vista band).
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0016f;
            // THE SEAM-KILL / THE TIE-IN THAT MUST NOT DRIFT (Uma §3 + Erik Q2): fog colour == the horizon
            // sky stop (#DCE8E4) AND the bottom of the gradient skybox == the same stop, so the distant
            // mountains fade to the SAME colour the sky fades to — they meet seamlessly with no horizon
            // line (URP does NOT fog the skybox, so a colour mismatch is a visible seam — Erik §1, Strong
            // source). Bound to the single SkyHorizon constant so fog + sky + far-range tint can never
            // drift apart (Erik Q2: "if the horizon stop shifts later, fog color updates in lockstep").
            RenderSettings.fogColor = SkyHorizon;
            Debug.Log("[QualityPassGen] Exp^2 distance fog (density 0.0016, colour == horizon stop) — Uma §3 / Erik Route A seam-kill");
        }

        // Build the GLOBAL post-processing Volume for the production play space: Bloom + warm Color
        // Grading + WhiteBalance + Vignette + filmic Tonemap. The profile is a persisted asset so it
        // serializes into the build. (Spike: a LOCAL box-collider Volume over Zone D; production: one
        // global Volume so the whole play space reads graded.)
        public static void BuildGlobalPostVolume()
        {
            string profilePath = SettingsDir + "/ZoneD_PostProfile.asset";
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            // Bloom — soft glow on the brightest lit facets (cloud caps, snow, water glints).
            // WORLD-LOOK RE-TUNE (ticket 86ca8t9pq — Uma world-look brief §3 "Bloom DOWN; a touch on the
            // brightest highlights is fine, heavy bloom is OUT — board objects have crisp facet edges, not
            // glowy ones; heavy bloom softens facets + bloom-clips the sub-1.0 brights"). Pull intensity
            // 0.40->0.25 and raise the threshold further (1.02) so ONLY the genuine brights (cloud top-lit
            // caps, snow-cap facets, water glints) get a touch of bloom and the broad chunky facets stay
            // crisp. (Sponsor subjective gate #2: exact intensity is a soak A/B call — this is the
            // directional "bloom down" lock, the value tunes in soak.)
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.02f);
            bloom.scatter.Override(0.5f);

            // Color grading — LIGHTER, neutral-warm so the saturated toy colours speak.
            // WORLD-LOOK RE-TUNE (Uma §3 "Color grading: LIGHTER, neutral-warm — pull any heavy filmic/
            // contrast grade DOWN, let the saturated toy greens/reds/cyans speak; slight warm-temperature
            // nudge only"). Drop contrast 12->6 (heavy contrast muddies the clean toy read) and ease the
            // postExposure to a flat 0.0 (the daylight key already lights the diorama; +exposure was a
            // hold-over that lifted brights toward bloom-clip). Keep the gentle warm colour filter +
            // saturation — warmth + saturation are hard carry-overs (Uma §tonal anchor).
            var cg = profile.Add<ColorAdjustments>(true);
            cg.postExposure.Override(0.0f);
            cg.contrast.Override(6f);
            cg.colorFilter.Override(new Color(1.03f, 1.0f, 0.94f)); // gentle warm filter (lighter)
            cg.saturation.Override(12f);

            var wb = profile.Add<WhiteBalance>(true);
            // WORLD-LOOK RE-TUNE (Uma §3 "slight warm-temperature nudge only"): ease +12 -> +8 so the
            // grade is warm-bright, not warm-heavy (a heavy temperature push muddied the clean cyan sky +
            // cyan clouds toward cream). Still warm (the hard carry-over), just lighter-handed.
            wb.temperature.Override(8f);
            wb.tint.Override(-2f);

            // Vignette — gentle framing so the eye centers on the world.
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.5f);

            // Tonemapping for a filmic rolloff (premium read).
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);

            // PERSIST EACH COMPONENT AS A SUB-ASSET of the profile. profile.Add<T>() only adds the
            // component to the IN-MEMORY profile; without AddObjectToAsset the components are NOT
            // serialized into the .asset, so on a fresh scene-open (and in the shipped build) the
            // profile loads EMPTY — the whole Bloom/Grade/Vignette/Tonemap stack silently vanishes.
            // Caught empirically by ZoneDLookTests.PostProcessing_GlobalVolumeWithProfile (Has<Bloom>
            // was False after re-opening the saved scene). This is the same class as the spike's
            // shader-strip "looks fine in the live editor, gone in the build" trap.
            foreach (var comp in profile.components)
            {
                comp.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(profilePath); // re-import so the sub-assets are written to disk

            var volGo = new GameObject("ZoneD_PostVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true; // production: graded everywhere, no box bounds
            vol.sharedProfile = profile;
            vol.priority = 1f;

            Debug.Log("[QualityPassGen] global post volume built (Bloom+Grade+WhiteBalance+Vignette+Tonemap)");
        }

        // Enable post-processing + SMAA on a camera so the Volume stack renders. In production this is
        // applied to whatever main camera exists at bootstrap time (the Boot scene's camera today;
        // U3's orbit camera once it lands — both pick up the global Volume). Defensive: no-op if null.
        public static void EnableCameraPostProcessing(Camera cam)
        {
            if (cam == null)
            {
                Debug.LogWarning("[QualityPassGen] no camera to enable post-processing on");
                return;
            }
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Debug.Log("[QualityPassGen] camera post-processing + SMAA enabled -> " + cam.name);
        }

        // Register a shader in GraphicsSettings.AlwaysIncludedShaders so the standalone build does NOT
        // strip it (the spike's magenta failure class — unity-conventions.md §Build stripping). The
        // gradient skybox shader is custom, so without this it ships stripped (a missing/magenta sky).
        // Mirrors WorldBootstrap.EnsureShaderAlwaysIncluded (kept local so the sky-registration is
        // self-contained in the quality pass).
        static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) { Debug.LogWarning("[QualityPassGen] AlwaysIncludedShaders prop not found"); return; }
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return; // already registered
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[QualityPassGen] added shader to AlwaysIncludedShaders -> " + shader.name);
        }
    }
}
