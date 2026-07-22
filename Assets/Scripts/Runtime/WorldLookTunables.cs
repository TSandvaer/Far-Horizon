using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The SINGLE binding seam between the unified dev-tweak SETTINGS CONSOLE (<see cref="FarHorizon.SettingsPanel"/>)
    /// and the live WORLD-LOOK state the legacy F10 <see cref="WorldLookNudgeTool"/> dials (ticket 86caber95 AC2 —
    /// migrate F10 into the console as entries). The console's registry binds to plain <c>Func&lt;float&gt;</c> /
    /// <c>Action&lt;float&gt;</c> delegates on typed live params; the world-look targets are NOT plain fields — they
    /// are <see cref="RenderSettings"/> (fog), the live skybox MATERIAL instance (sky stops + sun), and COLLECTIONS
    /// of cloud/vista transforms + per-cluster materials resolved at runtime. This seam presents ONE uniform
    /// per-scalar get/set surface over those backends so the console can show one float row per dial.
    ///
    /// IT ADDS NO NEW LOOK PATH. It resolves + mutates the EXACT same live handles the F10 tool does
    /// (<c>RenderSettings.fogDensity/fogColor</c>, the skybox material's <c>_ZenithColor/_MidColor/_HorizonColor/
    /// _SunDirection/_SunColor/_SunSize</c>, every <c>LP_Cloud</c> transform, every <c>Vista_*</c> cluster + its
    /// <c>LP_Mountain</c>/<c>LP_Landmass</c> material <c>_Tint</c>), with the SAME seam-kill (fog colour == horizon
    /// stop) and the SAME per-cluster base-tint bookkeeping — so the console dial and the legacy F10 dial drive
    /// identical results (AC5: keep the legacy F-key live IN PARALLEL during migration; the console is a second
    /// front-end onto the same mutations). All mutations are LIVE/runtime-only; the Sponsor reads the values off
    /// the console readouts + bakes them into WorldLookConfig / WorldBootstrap / QualityPassGen / GradientSkybox.
    ///
    /// AC4 (per-axis, not a Vector3 archetype): each dial is a SCALAR float row (fog density; cloud scale/altitude;
    /// mountain distance/peak/warmth/brightness; sun elevation/size). The RGB colour STOPS the F10 tool dials
    /// per-channel are DECOMPOSED the same way — one float row per R/G/B channel (sky horizon + fog colour) — so no
    /// new archetype is needed (the spec §6 open-4 recommendation).
    ///
    /// STATIC STATE: instance fields only — NO mutable runtime statics, so no SubsystemRegistration reset is
    /// needed (unity-conventions.md §Configurable Enter Play Mode; StaticStateResetTests stays green).
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time onto the Boot object (like the
    /// verify-capture + nudge-tool siblings) so it ships in Boot.unity; the resolves are lazy (the skybox/clouds/
    /// vistas exist after WorldBootstrap builds the environment), re-resolved on demand so a late-built world is
    /// still found. NO gameplay work per frame — it only mutates on a console dial.
    /// </summary>
    public class WorldLookTunables : MonoBehaviour
    {
        // Cached live handles, resolved lazily on first get/set (the world is built after this component's Awake).
        private Material _skyMat;
        private readonly List<Transform> _clouds = new List<Transform>();
        private readonly List<Vector3> _cloudBasePos = new List<Vector3>();
        private readonly List<Transform> _mtnClusters = new List<Transform>();
        private readonly List<Vector3> _mtnBaseLocal = new List<Vector3>();
        private readonly List<Material> _mtnMats = new List<Material>();
        private readonly List<Color> _mtnBaseTint = new List<Color>();
        // GRD-2 / RCK-1 (ticket 86cahhfkc): the live terrain material (meadow-patch amp) + the rock
        // materials (rim intensity). Resolved lazily like the rest — the world is built after Awake.
        private readonly List<Material> _terrainMats = new List<Material>();
        private readonly List<Material> _rockMats = new List<Material>();
        private bool _resolved;

        // Live-dialed multipliers/offsets (mirror WorldLookNudgeTool's semantics exactly). Start at the baked
        // defaults so an untouched console leaves the world byte-identical (the differs badge stays off).
        private float _cloudScale = 1f, _cloudAlt = 0f;
        private float _mtnDistScale = 1f, _mtnScale = 1f;
        private float _mtnWarmth = 0f;                     // additive R-up/B-down warmth onto every cluster _Tint
        private float _mtnBright = 1f;                     // uniform brightness multiply onto every cluster _Tint

        // Sun elevation/azimuth (the disk is driven by the sky material's _SunDirection). Elevation is dialed;
        // azimuth held. Derived once from the baked _SunDirection so the first dial starts from the shipped sun.
        private const float SunElevFallbackDeg = 8f;  // mirrors WorldBootstrap.SunElevationDeg (86cah90cp round-2 bake)
        private const float SunAzimuthFallbackDeg = -35f;
        private float _sunElevDeg = SunElevFallbackDeg;
        private float _sunAzimuthDeg = SunAzimuthFallbackDeg;
        private bool _sunDerived;

        // ===== RUNTIME SEAM-KILL ENFORCEMENT (ticket 86cajt6jb — WorldLook Sky fog seam) =====
        // The seam-kill (fog colour == sky horizon stop == WorldLookPalette.SkyHorizon) is baked into Boot.unity
        // at bootstrap time (QualityPassGen.EnableGlobalFog / BuildGradientSkybox, both == the SkyHorizon
        // constant). But the COMMITTED scene value is the ONLY runtime source of the fog colour — so any drift of
        // the committed RenderSettings.fogColor ships a broken sky seam that only a re-bootstrap clears. That
        // drift is a PROVEN class: a same-session EditMode test that mutates the LIVE global RenderSettings before
        // a regen commits the polluted value (unity-conventions.md §"A local EditMode test that mutates a LIVE
        // singleton asset through global engine state" — the fog-R 0.42 corruption; root-fixed at the TEST layer
        // by 86cahvntg / #241's snapshot-restore, but the committed value is still the sole runtime authority).
        // Re-assert the seam from the SINGLE palette constant at RUNTIME so a drifted committed value can never
        // carry a broken seam into the shipped build — the "set fog from the palette at runtime" route (needs NO
        // Boot.unity regen: this seam already ships serialized on the active hudGo, so its Start runs at runtime).
        private void Start()
        {
            ApplyPaletteSeamKill();
        }

        /// <summary>
        /// Re-assert the seam-kill from <see cref="FarHorizon.WorldLookPalette.SkyHorizon"/>: BOTH
        /// RenderSettings.fogColor AND the live skybox <c>_HorizonColor</c> == the one palette constant.
        /// Idempotent + byte-identical (a no-op) when the committed scene is already correct; when the committed
        /// fog drifted (e.g. the 0.42 fog-R corruption class) it snaps every channel back to the palette. Public
        /// so the EditMode regression guard can drive it deterministically (Start does not auto-run headless).
        /// </summary>
        public void ApplyPaletteSeamKill()
        {
            EnsureResolved();
            Color seam = FarHorizon.WorldLookPalette.SkyHorizon;
            seam.a = 1f;
            RenderSettings.fogColor = seam;
            if (_skyMat != null && _skyMat.HasProperty("_HorizonColor")) _skyMat.SetColor("_HorizonColor", seam);
        }

        // ---- Resolve (lazy; re-resolve if the skybox/collections came up empty — a late-built world) ----
        private void EnsureResolved()
        {
            if (_resolved && _skyMat != null) return;
            _resolved = true;
            _skyMat = RenderSettings.skybox;
            _clouds.Clear(); _cloudBasePos.Clear();
            _mtnClusters.Clear(); _mtnBaseLocal.Clear();
            _mtnMats.Clear(); _mtnBaseTint.Clear();
            _terrainMats.Clear(); _rockMats.Clear();
            var seenMats = new HashSet<Material>();
            var seenTerrain = new HashSet<Material>();
            var seenRock = new HashSet<Material>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "LP_Cloud") { _clouds.Add(t); _cloudBasePos.Add(t.position); }
                if (t.parent != null && t.parent.name == "Vista") { _mtnClusters.Add(t); _mtnBaseLocal.Add(t.position); }
                if (t.name == "LP_Mountain" || t.name == "LP_Landmass")
                {
                    var mr = t.GetComponent<MeshRenderer>();
                    var mat = mr != null ? mr.sharedMaterial : null;
                    if (mat != null && mat.HasProperty("_Tint") && seenMats.Add(mat))
                    {
                        _mtnMats.Add(mat);
                        _mtnBaseTint.Add(mat.GetColor("_Tint"));
                    }
                }
                // GRD-2 / RCK-1: collect the live terrain material (carries _MeadowPatchAmp) + rock materials
                // (carry _RimIntensity), matched by the bootstrap material NAMES. sharedMaterial so we mutate
                // the one instance every renderer of that class shares (one console dial moves them all).
                var rend = t.GetComponent<MeshRenderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    var m = rend.sharedMaterial;
                    if (m.HasProperty("_MeadowPatchAmp") && m.name.StartsWith("LowPolyTerrainMat") && seenTerrain.Add(m))
                        _terrainMats.Add(m);
                    if (m.HasProperty("_RimIntensity") && m.name.StartsWith("LPRockMat") && seenRock.Add(m))
                        _rockMats.Add(m);
                }
            }
        }

        // ===== FOG (RenderSettings) =====
        public float FogDensity
        {
            get => RenderSettings.fogDensity;
            set => RenderSettings.fogDensity = Mathf.Max(0f, value);
        }
        // Fog colour R/G/B (per-axis, AC4). Setting the fog colour keeps the SKY HORIZON stop locked to it
        // (seam-kill — Uma §3 / Erik Q2; the F10 tool does the same both ways) so the dissolve can't drift.
        public float FogColorR { get => RenderSettings.fogColor.r; set => SetFogChannel(0, value); }
        public float FogColorG { get => RenderSettings.fogColor.g; set => SetFogChannel(1, value); }
        public float FogColorB { get => RenderSettings.fogColor.b; set => SetFogChannel(2, value); }
        private void SetFogChannel(int ch, float v)
        {
            EnsureResolved();
            Color c = RenderSettings.fogColor;
            v = Mathf.Clamp01(v);
            if (ch == 0) c.r = v; else if (ch == 1) c.g = v; else c.b = v;
            c.a = 1f;
            RenderSettings.fogColor = c;
            if (_skyMat != null && _skyMat.HasProperty("_HorizonColor")) _skyMat.SetColor("_HorizonColor", c);
        }

        // ===== SKY HORIZON stop R/G/B (per-axis, AC4) — the seam-driving stop. Locks the fog colour to it too. =====
        public float SkyHorizonR { get => SkyHorizon().r; set => SetSkyHorizon(0, value); }
        public float SkyHorizonG { get => SkyHorizon().g; set => SetSkyHorizon(1, value); }
        public float SkyHorizonB { get => SkyHorizon().b; set => SetSkyHorizon(2, value); }
        private Color SkyHorizon()
        {
            EnsureResolved();
            return (_skyMat != null && _skyMat.HasProperty("_HorizonColor")) ? _skyMat.GetColor("_HorizonColor") : Color.black;
        }
        private void SetSkyHorizon(int ch, float v)
        {
            EnsureResolved();
            if (_skyMat == null || !_skyMat.HasProperty("_HorizonColor")) return;
            Color c = _skyMat.GetColor("_HorizonColor");
            v = Mathf.Clamp01(v);
            if (ch == 0) c.r = v; else if (ch == 1) c.g = v; else c.b = v;
            c.a = 1f;
            _skyMat.SetColor("_HorizonColor", c);
            RenderSettings.fogColor = c; // seam-kill — keep the fog colour == the horizon stop
        }

        // ===== CLOUDS (scale + additive altitude offset, applied to every LP_Cloud) =====
        public float CloudScale
        {
            get { EnsureResolved(); return _cloudScale; }
            set { EnsureResolved(); _cloudScale = Mathf.Max(0.1f, value); ApplyClouds(); }
        }
        public float CloudAltitude
        {
            get { EnsureResolved(); return _cloudAlt; }
            set { EnsureResolved(); _cloudAlt = value; ApplyClouds(); }
        }
        private void ApplyClouds()
        {
            for (int i = 0; i < _clouds.Count; i++)
            {
                if (_clouds[i] == null) continue;
                _clouds[i].localScale = Vector3.one * _cloudScale;
                var p = _cloudBasePos[i]; p.y += _cloudAlt;
                _clouds[i].position = new Vector3(_clouds[i].position.x, p.y, _clouds[i].position.z);
            }
        }

        // ===== MOUNTAINS (distance-scale + peak-scale on the clusters; warmth + brightness on the _Tint) =====
        public float MountainDistanceScale
        {
            get { EnsureResolved(); return _mtnDistScale; }
            set { EnsureResolved(); _mtnDistScale = Mathf.Max(0.1f, value); ApplyMountainTransforms(); }
        }
        public float MountainPeakScale
        {
            get { EnsureResolved(); return _mtnScale; }
            set { EnsureResolved(); _mtnScale = Mathf.Max(0.1f, value); ApplyMountainTransforms(); }
        }
        // Warmth: positive = warmer/browner (R up, B down); negative = cooler/bluer. Additive onto every _Tint.
        public float MountainWarmth
        {
            get { EnsureResolved(); return _mtnWarmth; }
            set { EnsureResolved(); _mtnWarmth = value; ApplyMountainTint(); }
        }
        public float MountainBrightness
        {
            get { EnsureResolved(); return _mtnBright; }
            set { EnsureResolved(); _mtnBright = Mathf.Max(0.2f, value); ApplyMountainTint(); }
        }
        private void ApplyMountainTransforms()
        {
            for (int i = 0; i < _mtnClusters.Count; i++)
            {
                if (_mtnClusters[i] == null) continue;
                _mtnClusters[i].position = _mtnBaseLocal[i] * _mtnDistScale;
                _mtnClusters[i].localScale = Vector3.one * _mtnScale;
            }
        }
        private void ApplyMountainTint()
        {
            for (int i = 0; i < _mtnMats.Count; i++)
            {
                if (_mtnMats[i] == null || !_mtnMats[i].HasProperty("_Tint")) continue;
                Color b = _mtnBaseTint[i];
                // Warmth pushes R up + B down (matching the F10 tool's ← / → semantics).
                Color t = new Color(
                    Mathf.Clamp01((b.r + _mtnWarmth) * _mtnBright),
                    Mathf.Clamp01(b.g * _mtnBright),
                    Mathf.Clamp01((b.b - _mtnWarmth) * _mtnBright), 1f);
                _mtnMats[i].SetColor("_Tint", t);
            }
        }

        // ===== SUN (elevation rebuilds _SunDirection; size is a direct _SunSize nudge) =====
        public float SunElevationDeg
        {
            get { EnsureResolved(); EnsureSunDerived(); return _sunElevDeg; }
            set { EnsureResolved(); EnsureSunDerived(); _sunElevDeg = Mathf.Clamp(value, 2f, 80f); ApplySunDirection(); }
        }
        // _SunSize is the disk-edge dot threshold in [0.95, 0.9999]; HIGHER = SMALLER disk. We expose the raw
        // property so the console shows/drives the same value the F10 tool + the bake constant use.
        public float SunSize
        {
            get { EnsureResolved(); return (_skyMat != null && _skyMat.HasProperty("_SunSize")) ? _skyMat.GetFloat("_SunSize") : 0.995f; }
            set { EnsureResolved(); if (_skyMat != null && _skyMat.HasProperty("_SunSize")) _skyMat.SetFloat("_SunSize", Mathf.Clamp(value, 0.95f, 0.9999f)); }
        }
        private void EnsureSunDerived()
        {
            if (_sunDerived) return;
            _sunDerived = true;
            if (_skyMat == null || !_skyMat.HasProperty("_SunDirection")) return;
            Vector4 sd = _skyMat.GetVector("_SunDirection");
            Vector3 toSun = new Vector3(sd.x, sd.y, sd.z);
            if (toSun.sqrMagnitude < 1e-6f) return;
            Vector3 e = Quaternion.LookRotation(-toSun.normalized, Vector3.up).eulerAngles;
            float pitch = e.x > 180f ? e.x - 360f : e.x;
            _sunElevDeg = pitch;
            _sunAzimuthDeg = e.y;
        }
        private void ApplySunDirection()
        {
            if (_skyMat == null || !_skyMat.HasProperty("_SunDirection")) return;
            Vector3 toSun = -(Quaternion.Euler(_sunElevDeg, _sunAzimuthDeg, 0f) * Vector3.forward);
            toSun.Normalize();
            _skyMat.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
        }

        // ===== MEADOW PATCH AMP (GRD-2, ticket 86cahhfkc) — live extra meadow-patch contrast on the terrain =====
        // Reads/writes _MeadowPatchAmp on the terrain material(s). 0 = the shipped baked GRD-1 patches only;
        // higher = push the same baked regions further toward the sunlit/shadow tones (the soak A/B knob).
        public float MeadowPatchAmp
        {
            get { EnsureResolved(); return _terrainMats.Count > 0 && _terrainMats[0] != null
                    ? _terrainMats[0].GetFloat("_MeadowPatchAmp") : 0f; }
            set
            {
                EnsureResolved();
                float v = Mathf.Clamp(value, 0f, 1.5f);
                for (int i = 0; i < _terrainMats.Count; i++)
                    if (_terrainMats[i] != null) _terrainMats[i].SetFloat("_MeadowPatchAmp", v);
            }
        }

        // ===== ROCK RIM INTENSITY (RCK-1, ticket 86cahhfkc) — live caught-sun rim on the boulders =====
        // Reads/writes _RimIntensity on the rock material(s). 0 = today (no rim); ~0.12 = the shipped whisper.
        public float RockRimIntensity
        {
            get { EnsureResolved(); return _rockMats.Count > 0 && _rockMats[0] != null
                    ? _rockMats[0].GetFloat("_RimIntensity") : 0f; }
            set
            {
                EnsureResolved();
                float v = Mathf.Clamp(value, 0f, 0.5f);
                for (int i = 0; i < _rockMats.Count; i++)
                    if (_rockMats[i] != null) _rockMats[i].SetFloat("_RimIntensity", v);
            }
        }
    }
}
