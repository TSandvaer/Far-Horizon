using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED debug WORLD-LOOK NUDGE TOOL (ticket 86ca8t9pq — Sponsor soak of 4457d47 "doesn't look
    /// good" + "consult Erik for fixing sky, cloud, mountain, water"). Instead of the team grinding blind
    /// soak iterations on a LOOK only the Sponsor can judge, this lets him dial the world-look IN THE
    /// SHIPPED BUILD and report the values to bake ([[sponsor-prefers-direct-tweak-tools-for-fiddly-
    /// placement]] + the unstick precision-handoff). Sibling of AxeNudgeTool (same F9-gated / IMGUI /
    /// legacy-Input idiom, same "dial then read the values off the panel + log to bake" workflow).
    ///
    /// BUILD-GATED / INERT IN NORMAL PLAY (the hard requirement): does NOTHING until the Sponsor TOGGLES
    /// it on with F9. Until then it never reads gameplay input, never mutates the look, draws no HUD — a
    /// normal soak is completely unaffected (a soak screenshot sees the shipped baked look, not a tool
    /// overlay). Serialized onto the Boot object editor-time (like the verify-capture siblings) so it ships,
    /// but stays asleep behind the toggle.
    ///
    /// TARGETS (Tab to cycle): SKY (3 gradient stops), FOG (distance/density + colour, seam-kill PRESERVED
    /// — the fog colour is kept == the skybox horizon stop on every nudge so the dissolve can't drift),
    /// CLOUDS (scale + altitude of every cloud), MOUNTAINS (distance + scale of every vista cluster).
    ///
    /// Pure legacy-Input + IMGUI (the project's input + HUD idiom), no new-Input-System or shader-asset
    /// dependency, build-safe. All mutations are to LIVE runtime state (RenderSettings, the skybox material
    /// instance, transforms) — they do NOT persist; the Sponsor bakes the reported values into
    /// WorldLookConfig / WorldBootstrap / QualityPassGen / GradientSkybox defaults.
    /// </summary>
    public class WorldLookNudgeTool : MonoBehaviour
    {
        [Tooltip("Debug toggle key. The tool is INERT until pressed — a normal soak never sees it.")]
        public KeyCode toggleKey = KeyCode.F9;
        [Tooltip("Cycle the nudge target (Sky / Fog / Clouds / Mountains).")]
        public KeyCode cycleKey = KeyCode.Tab;

        public enum Target { Sky, Fog, Clouds, Mountains }
        private static readonly string[] TargetNames = { "SKY (gradient stops)", "FOG (distance + colour)",
                                                          "CLOUDS (scale + altitude)", "MOUNTAINS (distance + scale)" };

        private bool _active;
        private Target _target = Target.Sky;
        private GUIStyle _style, _hintStyle, _titleStyle;

        // Cached live handles, resolved on first activation.
        private Material _skyMat;                         // RenderSettings.skybox instance
        private readonly List<Transform> _clouds = new List<Transform>();
        private readonly List<Vector3> _cloudBasePos = new List<Vector3>();   // for altitude re-base
        private readonly List<Transform> _mtnClusters = new List<Transform>();
        private readonly List<Vector3> _mtnBaseLocal = new List<Vector3>();   // for distance scaling

        // Live-dialed values (start at the baked defaults; the Sponsor reads these to bake).
        private float _cloudScale = 1f, _cloudAlt = 0f;   // cloudAlt is an additive +y offset
        private float _mtnDistScale = 1f, _mtnScale = 1f;

        // Sky stop indices for the SHIFT-selectable stop being edited.
        private int _skyStop; // 0 zenith, 1 mid, 2 horizon (horizon also drives the fog seam-kill)

        // The panel is RIGHT-anchored + vertically centred, off SurvivalHud's bottom-left hotbar (same as
        // AxeNudgeTool's SOAKFIX6 placement). Pure + static so the on-screen contract is testable w/o a render.
        public const float PanelWidth = 540f;
        public const float PanelHeight = 268f;
        public static Rect PanelRect(float screenW, float screenH)
        {
            float x = Mathf.Max(12f, screenW - PanelWidth - 12f);
            float y = Mathf.Max(46f, (screenH - PanelHeight) * 0.5f);
            return new Rect(x, y, PanelWidth, PanelHeight);
        }
        // SurvivalHud bottom-left hotbar footprint the panel must clear (mirrors AxeNudgeTool.HotbarZone).
        public static Rect HotbarZone(float screenW, float screenH)
        {
            float top = screenH - 86f, bottom = screenH - 14f;
            return new Rect(10f, top, 272f, bottom - top);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _active = !_active;
                if (_active) { Resolve(); LogCurrent(); }
                Debug.Log("[WorldLookNudgeTool] " + (_active
                    ? "ENABLED — dial sky/fog/clouds/mountains; values on HUD/log" : "disabled"));
            }
            if (!_active) return;

            if (Input.GetKeyDown(cycleKey))
            {
                _target = (Target)(((int)_target + 1) % 4);
                Debug.Log("[WorldLookNudgeTool] target = " + TargetNames[(int)_target]);
                LogCurrent();
            }

            bool changed = false;
            switch (_target)
            {
                case Target.Sky:      changed = NudgeSky();       break;
                case Target.Fog:      changed = NudgeFog();       break;
                case Target.Clouds:   changed = NudgeClouds();    break;
                case Target.Mountains:changed = NudgeMountains(); break;
            }
            if (changed) LogCurrent();
        }

        // ---- SKY: T/G cycle which stop; arrows nudge its R/G/B; the horizon stop also re-syncs the fog. ----
        private bool NudgeSky()
        {
            if (_skyMat == null) return false;
            bool changed = false;
            if (Input.GetKeyDown(KeyCode.Tab)) return false; // handled above
            if (Input.GetKeyDown(KeyCode.LeftBracket))  { _skyStop = (_skyStop + 2) % 3; LogCurrent(); }
            if (Input.GetKeyDown(KeyCode.RightBracket)) { _skyStop = (_skyStop + 1) % 3; LogCurrent(); }

            float s = ColorStep();
            Vector3 d = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.RightArrow)) d.x += s;   // R
            if (Input.GetKeyDown(KeyCode.LeftArrow))  d.x -= s;
            if (Input.GetKeyDown(KeyCode.UpArrow))    d.y += s;   // G
            if (Input.GetKeyDown(KeyCode.DownArrow))  d.y -= s;
            if (Input.GetKeyDown(KeyCode.PageUp))     d.z += s;   // B
            if (Input.GetKeyDown(KeyCode.PageDown))   d.z -= s;
            if (d != Vector3.zero)
            {
                string prop = _skyStop == 0 ? "_ZenithColor" : _skyStop == 1 ? "_MidColor" : "_HorizonColor";
                if (_skyMat.HasProperty(prop))
                {
                    Color c = _skyMat.GetColor(prop);
                    c = new Color(Mathf.Clamp01(c.r + d.x), Mathf.Clamp01(c.g + d.y), Mathf.Clamp01(c.b + d.z), 1f);
                    _skyMat.SetColor(prop, c);
                    // SEAM-KILL: if the HORIZON stop moves, keep the fog colour locked to it (Uma §3 / Erik
                    // Q2 — fog colour == horizon stop or a visible seam appears). Never let them drift.
                    if (_skyStop == 2) RenderSettings.fogColor = c;
                    changed = true;
                }
            }
            return changed;
        }

        // ---- FOG: PageUp/Down nudge density (distance); arrows nudge the fog/horizon colour together. ----
        private bool NudgeFog()
        {
            bool changed = false;
            float ds = 0.0002f * StepMul();
            if (Input.GetKeyDown(KeyCode.PageUp))   { RenderSettings.fogDensity = Mathf.Max(0f, RenderSettings.fogDensity + ds); changed = true; }
            if (Input.GetKeyDown(KeyCode.PageDown)) { RenderSettings.fogDensity = Mathf.Max(0f, RenderSettings.fogDensity - ds); changed = true; }

            float s = ColorStep();
            Vector3 d = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.RightArrow)) d.x += s;
            if (Input.GetKeyDown(KeyCode.LeftArrow))  d.x -= s;
            if (Input.GetKeyDown(KeyCode.UpArrow))    d.y += s;
            if (Input.GetKeyDown(KeyCode.DownArrow))  d.y -= s;
            if (d != Vector3.zero)
            {
                Color c = RenderSettings.fogColor;
                c = new Color(Mathf.Clamp01(c.r + d.x), Mathf.Clamp01(c.g + d.y), Mathf.Clamp01(c.b + d.z), 1f);
                RenderSettings.fogColor = c;
                // SEAM-KILL the other way: keep the skybox horizon stop == the fog colour.
                if (_skyMat != null && _skyMat.HasProperty("_HorizonColor")) _skyMat.SetColor("_HorizonColor", c);
                changed = true;
            }
            return changed;
        }

        // ---- CLOUDS: arrows up/down = uniform scale; PageUp/Down = altitude. ----
        private bool NudgeClouds()
        {
            bool changed = false;
            float ss = 0.05f * StepMul();
            if (Input.GetKeyDown(KeyCode.UpArrow))   { _cloudScale += ss; changed = true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { _cloudScale = Mathf.Max(0.1f, _cloudScale - ss); changed = true; }
            float ps = 1f * StepMul();
            if (Input.GetKeyDown(KeyCode.PageUp))    { _cloudAlt += ps; changed = true; }
            if (Input.GetKeyDown(KeyCode.PageDown))  { _cloudAlt -= ps; changed = true; }
            if (changed)
                for (int i = 0; i < _clouds.Count; i++)
                {
                    if (_clouds[i] == null) continue;
                    _clouds[i].localScale = Vector3.one * _cloudScale;
                    var p = _cloudBasePos[i]; p.y += _cloudAlt;
                    _clouds[i].position = new Vector3(_clouds[i].position.x, p.y, _clouds[i].position.z);
                }
            return changed;
        }

        // ---- MOUNTAINS: arrows up/down = distance scale (pull in / push out); PageUp/Down = peak scale. ----
        private bool NudgeMountains()
        {
            bool changed = false;
            float ds = 0.02f * StepMul();
            if (Input.GetKeyDown(KeyCode.UpArrow))   { _mtnDistScale += ds; changed = true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { _mtnDistScale = Mathf.Max(0.1f, _mtnDistScale - ds); changed = true; }
            float ss = 0.05f * StepMul();
            if (Input.GetKeyDown(KeyCode.PageUp))    { _mtnScale += ss; changed = true; }
            if (Input.GetKeyDown(KeyCode.PageDown))  { _mtnScale = Mathf.Max(0.1f, _mtnScale - ss); changed = true; }
            if (changed)
                for (int i = 0; i < _mtnClusters.Count; i++)
                {
                    if (_mtnClusters[i] == null) continue;
                    // Distance: scale the cluster's offset from the world origin (the player sits near origin).
                    _mtnClusters[i].position = _mtnBaseLocal[i] * _mtnDistScale;
                    _mtnClusters[i].localScale = Vector3.one * _mtnScale;
                }
            return changed;
        }

        private float StepMul()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 0.2f;
            return 1f;
        }
        private float ColorStep() => 0.02f * StepMul();

        private void Resolve()
        {
            _skyMat = RenderSettings.skybox; // the live instance (mutations are runtime-only)
            _clouds.Clear(); _cloudBasePos.Clear();
            _mtnClusters.Clear(); _mtnBaseLocal.Clear();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "LP_Cloud") { _clouds.Add(t); _cloudBasePos.Add(t.position); }
                // Vista clusters are the children of the "Vista" root (each named Vista_*).
                if (t.parent != null && t.parent.name == "Vista")
                {
                    _mtnClusters.Add(t);
                    _mtnBaseLocal.Add(t.position);
                }
            }
            if (_skyMat == null) Debug.LogWarning("[WorldLookNudgeTool] no RenderSettings.skybox to dial");
            Debug.Log($"[WorldLookNudgeTool] resolved {_clouds.Count} clouds, {_mtnClusters.Count} vista clusters, " +
                      $"skybox={(_skyMat != null ? _skyMat.shader.name : "none")}");
        }

        // Print the dialed values in a copy-pasteable form (the Sponsor reads these to bake).
        private void LogCurrent()
        {
            switch (_target)
            {
                case Target.Sky:
                    if (_skyMat != null)
                        Debug.Log($"[WorldLookNudgeTool] SKY zenith={Fmt("_ZenithColor")} mid={Fmt("_MidColor")} " +
                                  $"horizon={Fmt("_HorizonColor")}  (editing stop {(_skyStop==0?"ZENITH":_skyStop==1?"MID":"HORIZON")})");
                    break;
                case Target.Fog:
                    Debug.Log($"[WorldLookNudgeTool] FOG density={RenderSettings.fogDensity:F4} " +
                              $"colour=({RenderSettings.fogColor.r:F3},{RenderSettings.fogColor.g:F3},{RenderSettings.fogColor.b:F3})");
                    break;
                case Target.Clouds:
                    Debug.Log($"[WorldLookNudgeTool] CLOUDS scale={_cloudScale:F2} altOffset={_cloudAlt:F1}u ({_clouds.Count} clouds)");
                    break;
                case Target.Mountains:
                    Debug.Log($"[WorldLookNudgeTool] MOUNTAINS distScale={_mtnDistScale:F2} peakScale={_mtnScale:F2} " +
                              $"(bake into WorldLookConfig.MtnDistanceScale; {_mtnClusters.Count} clusters)");
                    break;
            }
        }
        private string Fmt(string prop)
        {
            if (_skyMat == null || !_skyMat.HasProperty(prop)) return "(n/a)";
            Color c = _skyMat.GetColor(prop);
            return $"({c.r:F3},{c.g:F3},{c.b:F3})";
        }

        void OnGUI()
        {
            if (!_active) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _style.normal.textColor = new Color(0.6f, 1f, 0.7f);
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _hintStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
            }
            Rect panel = PanelRect(Screen.width, Screen.height);
            float x = panel.x, y = panel.y, w = panel.width;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float lx = x + 12f, lw = w - 24f;
            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "WORLD-LOOK NUDGE TOOL  (debug — F9 to close)", _titleStyle);
            GUI.Label(new Rect(lx, y + 30f, lw, 20f),
                "Dial sky/fog/clouds/mountains in-game, then read the values to bake.", _hintStyle);
            GUI.Label(new Rect(lx, y + 56f, lw, 22f), "Editing: " + TargetNames[(int)_target], _style);

            string l1, l2;
            switch (_target)
            {
                case Target.Sky:
                    l1 = $"stop={(_skyStop==0?"ZENITH":_skyStop==1?"MID":"HORIZON")}  {Fmt(_skyStop==0?"_ZenithColor":_skyStop==1?"_MidColor":"_HorizonColor")}";
                    l2 = "[ / ] = pick stop   ←/→ = R   ↑/↓ = G   PgUp/Dn = B"; break;
                case Target.Fog:
                    l1 = $"density={RenderSettings.fogDensity:F4}  colour=({RenderSettings.fogColor.r:F2},{RenderSettings.fogColor.g:F2},{RenderSettings.fogColor.b:F2})";
                    l2 = "PgUp/Dn = density (distance)   ←/→ = R   ↑/↓ = G"; break;
                case Target.Clouds:
                    l1 = $"scale={_cloudScale:F2}   altOffset={_cloudAlt:F1}u";
                    l2 = "↑/↓ = scale   PgUp/Dn = altitude"; break;
                default:
                    l1 = $"distScale={_mtnDistScale:F2}   peakScale={_mtnScale:F2}";
                    l2 = "↑/↓ = distance (pull in/out)   PgUp/Dn = peak scale"; break;
            }
            GUI.Label(new Rect(lx, y + 80f, lw, 22f), l1, _style);
            GUI.Label(new Rect(lx, y + 110f, lw, 20f), "[Tab] cycle target (sky / fog / clouds / mountains)", _hintStyle);
            GUI.Label(new Rect(lx, y + 132f, lw, 20f), l2, _hintStyle);
            GUI.Label(new Rect(lx, y + 158f, lw, 20f), "Hold Shift = 5x step    Hold Ctrl = 0.2x step", _hintStyle);
            GUI.Label(new Rect(lx, y + 182f, lw, 20f),
                "Fog↔sky-horizon seam-kill stays locked automatically as you dial.", _hintStyle);
            GUI.Label(new Rect(lx, y + 210f, lw, 40f),
                "Values print to the log each nudge — copy them to bake the default\ninto WorldLookConfig / WorldBootstrap / GradientSkybox.", _hintStyle);
        }
    }
}
