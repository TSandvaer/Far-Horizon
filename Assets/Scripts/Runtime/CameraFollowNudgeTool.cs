using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED debug CAMERA-FOLLOW nudge tool (ticket 86caaqhj5 ATTEMPT 2 — the precision handoff). The
    /// jump-pull-back A/S/D failure is a too-slow HORIZONTAL follow that lags the player through a fast move+
    /// jump; the mechanism fix (OrbitCamera.DesiredFollowXZ velocity feed-forward + a tighter followLerp) cancels
    /// the steady-state lag in ALL directions. But the FEEL of the follow (snappy vs floaty) is the Sponsor's
    /// taste call — so rather than the team guessing the gains, this lets him DIAL them IN-GAME while jumping in
    /// every direction (W/A/S/D), READ the live values off the on-screen HUD + the log, and report the numbers
    /// to BAKE into OrbitCamera's defaults. The unstick "build the knob, don't grind blind iterations" handle.
    ///
    /// DIALS three OrbitCamera follow gains:
    ///   - HORIZONTAL follow lerp (followLerp, 1/s) — how fast the camera tracks the player's X/Z. Higher =
    ///     snappier (less lag) but can read jerky on normal walk; lower = floatier.   [PgUp/PgDn]
    ///   - VERTICAL follow lerp (verticalFollowLerp, 1/s) — how fast the camera tracks the JUMP-ARC Y. Already
    ///     high (60) so the camera rises/falls with the hop; dialable here too.        [T/G]
    ///   - LEAD time (followLeadTime, s) — the velocity feed-forward the horizontal follow leads by. 0 = AUTO
    ///     (= 1/followLerp, the exact lag-cancelling value); dial >0 to lead the player further ahead. [Y/H]
    ///
    /// BUILD-GATED / INERT IN NORMAL PLAY (the hard requirement, mirrors AxeNudgeTool): does NOTHING until the
    /// Sponsor TOGGLES it on with the debug key (F7). Until then it never reads gameplay input, never touches
    /// the camera, and draws no HUD — a normal soak is completely unaffected. Serialized onto the Boot object
    /// editor-time (MovementCameraScene) so it ships, but stays asleep behind the toggle.
    ///
    /// KEY-SPLIT: F7 (FloatDiagnostic F8, AxeNudgeTool F9, WorldLookNudgeTool F10) so the soak panels never
    /// collide. MUTUAL EXCLUSION: activating this panel forces the axe + world-look panels OFF (their shared
    /// PageUp/PageDown/T/G/Y/H keys can never cross-fire while two panels are up).
    ///
    /// Pure legacy-Input + IMGUI (the project's input + HUD idiom — OrbitCamera/AxeNudgeTool/BootHud), no
    /// new-Input-System or shader dependency, build-safe.
    /// </summary>
    public class CameraFollowNudgeTool : MonoBehaviour, INudgePanel
    {
        [Tooltip("Debug toggle key. The tool is INERT until pressed — a normal soak never sees it. " +
                 "F7 (FloatDiagnostic F8, AxeNudgeTool F9, WorldLookNudgeTool F10) so the soak panels never collide.")]
        public KeyCode toggleKey = KeyCode.F7;
        [Tooltip("Horizontal follow-lerp (followLerp) nudge step (1/s). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float horizStep = 1f;
        [Tooltip("Vertical follow-lerp (verticalFollowLerp) nudge step (1/s). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float vertStep = 5f;
        [Tooltip("Lead-time (followLeadTime) nudge step (s). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float leadStep = 0.01f;
        [Tooltip("Airborne horizontal follow-lerp (airborneFollowLerp) nudge step (1/s). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float airborneStep = 5f;

        private bool _active;
        private OrbitCamera _cam;
        private GUIStyle _style, _hintStyle, _titleStyle;

        public const float PanelWidth = 532f;
        public const float PanelHeight = 288f;

        /// <summary>The nudge-panel screen rect — RIGHT-anchored + vertically centred, x-clamped on-screen
        /// (mirrors AxeNudgeTool.PanelRect so the panels share placement). Pure + static so the on-screen
        /// contract is regression-guarded without a render.</summary>
        public static Rect PanelRect(float screenW, float screenH)
        {
            float x = Mathf.Max(12f, screenW - PanelWidth - 12f);
            float y = Mathf.Max(46f, (screenH - PanelHeight) * 0.5f);
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        /// <summary>Is this panel currently up? (read by the sibling tools' mutual-exclusion + by tests.)</summary>
        public bool IsActive => _active;

        /// <summary>Force this panel OFF (called by a sibling nudge tool when ITS panel toggles on, so only one
        /// nudge panel is ever active and their shared adjust keys can never cross-fire). Idempotent.</summary>
        public void Deactivate() => _active = false;

        /// <summary>
        /// Turn this panel ON. MUTUAL EXCLUSION: activating THIS panel forces the sibling axe + world-look panels
        /// OFF, so only one nudge panel is ever active. Public so the mutual-exclusion contract is testable
        /// without synthesizing the F7 legacy-Input key-down.
        /// </summary>
        public void Activate()
        {
            foreach (var axe in Object.FindObjectsByType<AxeNudgeTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                axe.Deactivate();
            foreach (var world in Object.FindObjectsByType<WorldLookNudgeTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                world.Deactivate();
            _active = true;
            Resolve();
            LogCurrent();
        }

        void Update()
        {
            // The ONLY thing that runs in normal play: watch for the debug toggle. Cheap, no allocs, no
            // gameplay effect. Everything else is gated behind _active.
            if (Input.GetKeyDown(toggleKey))
            {
                if (_active) { Deactivate(); Debug.Log("[CameraFollowNudgeTool] disabled"); }
                else { Activate(); Debug.Log("[CameraFollowNudgeTool] ENABLED — jump in every direction (W/A/S/D) + dial the follow; values on HUD/log"); }
            }
            if (!_active) return;

            if (_cam == null) { Resolve(); if (_cam == null) return; }

            float hs = horizStep * StepMul();
            float vs = vertStep * StepMul();
            float ls = leadStep * StepMul();
            float abs = airborneStep * StepMul();
            bool changed = false;

            // HORIZONTAL follow lerp — PageUp/PageDown.
            if (Input.GetKeyDown(KeyCode.PageUp)) { _cam.followLerp = Mathf.Max(0f, _cam.followLerp + hs); changed = true; }
            if (Input.GetKeyDown(KeyCode.PageDown)) { _cam.followLerp = Mathf.Max(0f, _cam.followLerp - hs); changed = true; }

            // VERTICAL (jump-arc) follow lerp — T/G.
            if (Input.GetKeyDown(KeyCode.T)) { _cam.verticalFollowLerp = Mathf.Max(0f, _cam.verticalFollowLerp + vs); changed = true; }
            if (Input.GetKeyDown(KeyCode.G)) { _cam.verticalFollowLerp = Mathf.Max(0f, _cam.verticalFollowLerp - vs); changed = true; }

            // LEAD time — Y/H. Clamped to [0, maxLeadTime]; 0 = AUTO (1/followLerp).
            if (Input.GetKeyDown(KeyCode.Y)) { _cam.followLeadTime = Mathf.Clamp(_cam.followLeadTime + ls, 0f, _cam.maxLeadTime); changed = true; }
            if (Input.GetKeyDown(KeyCode.H)) { _cam.followLeadTime = Mathf.Clamp(_cam.followLeadTime - ls, 0f, _cam.maxLeadTime); changed = true; }

            // AIRBORNE horizontal follow lerp (86caaqhj5 attempt 3 — the confirmed jump fix knob) — U/J. The
            // tight rate the X/Z follow uses WHILE AIRBORNE so the jump has ~zero lag (avatar stays centred in
            // all 4 headings). Higher = tighter/centred; lower = floatier (re-introduces the off-centre percept).
            if (Input.GetKeyDown(KeyCode.U)) { _cam.airborneFollowLerp = Mathf.Max(0f, _cam.airborneFollowLerp + abs); changed = true; }
            if (Input.GetKeyDown(KeyCode.J)) { _cam.airborneFollowLerp = Mathf.Max(0f, _cam.airborneFollowLerp - abs); changed = true; }

            if (changed) LogCurrent();
        }

        private float StepMul()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 0.2f;
            return 1f;
        }

        private void Resolve()
        {
            _cam = Object.FindAnyObjectByType<OrbitCamera>(FindObjectsInactive.Include);
            if (_cam == null) Debug.LogWarning("[CameraFollowNudgeTool] no OrbitCamera found — cannot nudge the follow gains");
        }

        // Log the values in a copy-pasteable form (the Sponsor reads these off the log to bake into the
        // OrbitCamera defaults). The lead line shows BOTH the configured followLeadTime AND the effective lead
        // (the auto-resolved 1/followLerp when configured == 0) so the Sponsor knows the value he's actually getting.
        private void LogCurrent()
        {
            if (_cam == null) return;
            Debug.Log($"[CameraFollowNudgeTool] followLerp={_cam.followLerp:F2}f  verticalFollowLerp={_cam.verticalFollowLerp:F2}f  " +
                      $"airborneFollowLerp={_cam.airborneFollowLerp:F2}f  " +
                      $"followLeadTime={_cam.followLeadTime:F4}f  (effectiveLead={_cam.EffectiveLead:F4}s)");
        }

        void OnGUI()
        {
            if (!DebugOverlays.Visible) return; // F1 master gate (86cafd6d6) — F7 is the sub-toggle below it
            if (!_active) return; // INERT in normal play — no overlay unless toggled on

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _style.normal.textColor = new Color(0.6f, 1f, 0.7f);
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _hintStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.45f); // warm-gold header
            }

            Rect panel = PanelRect(Screen.width, Screen.height);
            float x = panel.x, y = panel.y, w = panel.width;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float lx = x + 12f, lw = w - 24f;
            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "CAMERA FOLLOW  (debug — F7 to close)", _titleStyle);
            GUI.Label(new Rect(lx, y + 30f, lw, 20f),
                "Jump in every direction (W/A/S/D) + dial the follow, then read values to bake.", _hintStyle);

            if (_cam == null)
            {
                GUI.Label(new Rect(lx, y + 56f, lw, 22f), "(OrbitCamera not found)", _style);
                return;
            }

            GUI.Label(new Rect(lx, y + 56f, lw, 22f),
                $"HORIZONTAL followLerp = {_cam.followLerp:F2}   (PgUp/PgDn — higher = snappier, less lag)", _style);
            GUI.Label(new Rect(lx, y + 78f, lw, 22f),
                $"VERTICAL  verticalFollowLerp = {_cam.verticalFollowLerp:F2}   (T/G — tracks the jump arc Y)", _style);
            GUI.Label(new Rect(lx, y + 100f, lw, 22f),
                $"AIRBORNE  airborneFollowLerp = {_cam.airborneFollowLerp:F2}   (U/J — tight jump XZ, keeps centred)", _style);
            GUI.Label(new Rect(lx, y + 122f, lw, 22f),
                $"LEAD  followLeadTime = {_cam.followLeadTime:F4}   (Y/H — 0 = AUTO)", _style);
            GUI.Label(new Rect(lx, y + 144f, lw, 22f),
                $"   effective lead = {_cam.EffectiveLead:F4}s   (= 1/followLerp when 0; cancels the ground lag)", _hintStyle);

            GUI.Label(new Rect(lx, y + 172f, lw, 20f), "PgUp/PgDn = horizontal    T/G = vertical    U/J = airborne XZ", _hintStyle);
            GUI.Label(new Rect(lx, y + 192f, lw, 20f), "Y/H = lead time (0 = auto 1/followLerp)", _hintStyle);
            GUI.Label(new Rect(lx, y + 212f, lw, 20f), "Hold Shift = 5x step    Hold Ctrl = 0.2x step", _hintStyle);
            GUI.Label(new Rect(lx, y + 236f, lw, 20f),
                "Values also print to the log each nudge — copy them to bake the default.", _hintStyle);
        }
    }
}
