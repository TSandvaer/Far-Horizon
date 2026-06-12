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

        // Build the gradient skybox material (warm horizon, cool zenith) and assign it as the scene
        // skybox. Uses the built-in "Skybox/Procedural" or a gradient fallback — both build-safe.
        public static void BuildGradientSkybox()
        {
            var proc = Shader.Find("Skybox/Procedural");
            Material sky;
            if (proc != null)
            {
                sky = new Material(proc);
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_AtmosphereThickness", 1.1f);
                sky.SetColor("_SkyTint", new Color(0.55f, 0.62f, 0.72f));     // cool-ish upper sky
                sky.SetColor("_GroundColor", new Color(0.78f, 0.72f, 0.58f)); // warm horizon/ground
                sky.SetFloat("_Exposure", 1.05f);
            }
            else
            {
                sky = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                sky.SetColor("_BaseColor", new Color(0.6f, 0.68f, 0.78f));
            }
            AssetDatabase.CreateAsset(sky, SettingsDir + "/GradientSky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox; // sky drives ambient (cool fill)
            DynamicGI.UpdateEnvironment();
            Debug.Log("[QualityPassGen] gradient skybox assigned");
        }

        // Enable the warm-haze global fog. In production (single play space) the Zone-D fog is the
        // baseline atmosphere, not a per-zone scoped effect — so it is global at the spike's gentle
        // density (the spike kept global density low + let the local Volume add a densification feel;
        // here the global value IS the look).
        public static void EnableGlobalFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.80f, 0.80f, 0.74f); // warm haze
            // FIRST-FRAME TUNE (86ca8ce7j): the board v2 scene refs are CRISP to mid-distance, not
            // hazy-near (style-guide §4 "fog much lighter / distance-only"). The 0.006 near-field haze
            // was a top contributor to the washed-out spawn frame (it pales the near sand into the warm
            // fog colour). Halve it to 0.003 so fog serves only far-horizon depth, not a near-field
            // wash — the spawn frame stays warm-saturated, the far world still fades to the horizon.
            RenderSettings.fogDensity = 0.003f;
            Debug.Log("[QualityPassGen] global warm-haze fog enabled (density 0.003 — distance-only)");
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

            // Bloom — soft glow on the brightest lit facets (the sunlit meadow rise, water glint).
            // FIRST-FRAME TUNE (86ca8ce7j): board v2 wants bloom DOWN (style-guide §4 — heavy bloom
            // softens the chunky facet edges + blew the pale sand into a white wash). Pull intensity
            // 0.55->0.40 and raise the threshold so only genuinely bright highlights bloom, not the
            // broad pale ground.
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.40f);
            bloom.threshold.Override(0.98f);
            bloom.scatter.Override(0.55f);

            // Color grading — warm cinematic lift so the low-poly reads "graded", not raw.
            // FIRST-FRAME TUNE (86ca8ce7j): drop the +0.15 postExposure to +0.05 (it was over-lifting
            // the already-pale sand into blow-out) and bump saturation a touch (board v2 is
            // saturated-but-warm). Keep the warm colour filter — warmth is the hard carry-over.
            var cg = profile.Add<ColorAdjustments>(true);
            cg.postExposure.Override(0.05f);
            cg.contrast.Override(12f);
            cg.colorFilter.Override(new Color(1.04f, 1.0f, 0.92f)); // warm filter
            cg.saturation.Override(12f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(12f); // warmer
            wb.tint.Override(-3f);

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
    }
}
