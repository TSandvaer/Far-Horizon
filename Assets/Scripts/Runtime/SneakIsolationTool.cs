using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED SNEAK-WALK ISOLATION INSTRUMENT (ticket 86caa3kur re-soak ATTEMPT 3 — the /unstick handle).
    /// NOT a fix — a precision handle so the SPONSOR isolates the residual sneak-walk jerk BY EYE instead of the
    /// team grinding another blind iteration.
    ///
    /// THE PROBLEM THIS ISOLATES (Sponsor v3 soak, the RVO-fix build 3be3bfb): crouch sneak-walk "works but is
    /// NOT smooth — lags/jerks after every SECOND step (left, right, JERK, repeat)". That cadence = ONCE PER FULL
    /// GAIT CYCLE (L+R). The prior trace PROVED the CrouchWalk clip plays smooth (constant state, monotonic
    /// normalizedTime, clean wraps) and the RVO fix smoothed agent.velocity (CoV dropped) — yet a cycle-periodic
    /// jerk remains. The two cycle-periodic suspects:
    ///   #186 foot-sync — the system coupling walk-anim PLAYBACK speed to ground speed (walk ≈1.53×); it may
    ///                    snap/recompute once per stride.
    ///   clip loop-seam — a pose/root discontinuity when the CrouchWalk clip wraps.
    ///
    /// THE HANDLES (live, behind the F1 dev-overlay master gate — DEBUG-only, default OFF so shipped crouch is
    /// byte-unchanged):
    ///   F5 — toggle the #186 FOOT-SYNC coupling on/off (CastawayCharacter.footSync). With it OFF, animation
    ///        playback returns to a constant rate. If the per-second-step jerk VANISHES with foot-sync off →
    ///        foot-sync is the cause. (Foot-sync's LocoSpeedMul does NOT reach CrouchWalk in source — this
    ///        instrument lets the Sponsor CONFIRM that empirically by eye, the disconfirming control.)
    ///   F6 — toggle SNEAK-SPEED-SNAP → normal WALK speed (WasdMovement.SetSneakSpeedSnapToWalk). Rules out the
    ///        reduced-sneak-speed-SPECIFIC path: if the jerk is gone at walk speed → it's a slow-speed
    ///        root-translation/blend artifact; if it persists → an animation-clip artifact independent of speed.
    ///
    /// THE READOUT (live, every frame, so the cycle-periodic oscillation becomes VISIBLE in which NUMBER moves):
    ///   - agent.velocity.magnitude         (the simulated planar speed — the motion-layer ground truth)
    ///   - Animator Speed param + animator.speed   (the damped blend-tree Speed + the global anim clock)
    ///   - foot-sync multiplier (LocoSpeedMul)      (the #186 coupling value — 1 = authored cadence)
    ///   - active clip name + normalizedTime        (CrouchWalk + its loop phase — the clip-layer ground truth)
    ///   - the two toggle states (foot-sync ON/OFF, sneak-speed snap ON/OFF)
    ///
    /// DANISH-KEYBOARD-SAFE keys (the project rule — [[sponsor-danish-keyboard-layout]]): F5 / F6 are F-keys
    /// (same physical position on Danish vs US; NEVER US-position punctuation `; ' [ ] = -` which shift on the
    /// Sponsor's Danish layout). F5/F6 are verified UNBOUND elsewhere (existing dev keys are F1/F7/F8/F9/F10;
    /// F2/F3 were VACATED here — F2 now hosts #208's legacy overlays, so this tool moved off F2/F3 to F5/F6).
    ///
    /// ARCHITECTURE (the project's dev-overlay idiom — FloatDiagnostic/AxeNudgeTool siblings): pure legacy-Input
    /// + IMGUI (no New-Input-System / shader dependency, build-safe), gated behind the F1 master
    /// (DebugOverlays.Visible) + serialized onto Boot editor-time (the editor-vs-runtime serialization trap) so it
    /// ships but stays asleep behind F1. DEFAULT = inert: the toggles start in the SHIPPED state (foot-sync ON,
    /// sneak-speed reduced), so a normal soak / CI capture sees byte-unchanged crouch behavior until the Sponsor
    /// presses F1 then F5/F6 to A/B.
    /// </summary>
    public class SneakIsolationTool : MonoBehaviour
    {
        // SHOW/HIDE key for this overlay's panel (ticket 86cah90cp — Sponsor 2026-07-01 F10 soak). The panel
        // reveals with the rest of the debug-overlay layer via the shared DebugOverlays.Visible flag.
        // Historically that master lived on F1, then moved F1→F2 (#208 F1/F2 de-conflict), then the Sponsor
        // asked for the debug overlays GROUPED on F10 with the WorldLookNudgeTool (also F10) — "so F10 toggles
        // the debug overlays together" — and F1 kept FREE for the console. The legacy F2 master
        // (DebugOverlayToggle) was then REMOVED entirely (86cah90cp round-3, Sponsor-directed 2026-07-03): F10 is
        // now the SINGLE master. This tool flips DebugOverlays.Visible on F10; pressing F10 reveals BOTH this
        // panel and the world-look nudge panel together. F2 is UNBOUND. F1 (dev console) is NOT consumed here.
        [Tooltip("Show/hide the debug-overlay layer (this panel + the WorldLookNudgeTool panel together). F10 — " +
                 "the SINGLE debug-overlay master key (86cah90cp; the legacy F2 master was removed), " +
                 "Danish-keyboard-safe. Flips the shared DebugOverlays.Visible master. F1 is the dev console — " +
                 "NOT consumed by this overlay.")]
        public KeyCode overlayToggleKey = KeyCode.F10;

        [Tooltip("Toggle the #186 FOOT-SYNC coupling on/off. F5 — Danish-keyboard-safe F-key, verified unbound " +
                 "(dev keys are F1/F7-F10; F2/F3 vacated and now UNBOUND — the legacy F2 overlay master was removed). " +
                 "DEBUG-only; default restores foot-sync ON each play-entry.")]
        public KeyCode footSyncToggleKey = KeyCode.F5;

        [Tooltip("Toggle SNEAK speed → NORMAL WALK speed (rules out the slow-sneak-speed-specific path). F6 — " +
                 "Danish-keyboard-safe F-key, verified unbound. DEBUG-only; default OFF (reduced sneak speed).")]
        public KeyCode sneakSpeedSnapToggleKey = KeyCode.F6;

        // Resolved lazily (the avatar + player live under the player root, built editor-time).
        private CastawayCharacter _castaway;
        private WasdMovement _player;
        private NavMeshAgent _agent;

        private GUIStyle _titleStyle, _labelStyle, _valueStyle, _toggleOnStyle, _toggleOffStyle;

        // Panel geometry — RIGHT-anchored + vertically centred so it clears BootHud's top plates, the LEFT-anchored
        // F8 FloatDiagnostic panel, and SurvivalHud's bottom hotbar. Pure + static so the placement is regression-
        // guardable without a render. (FloatDiagnostic is LEFT, the F9 AxeNudge is upper-RIGHT; this sits lower-RIGHT.)
        public const float PanelWidth = 380f;
        public const float PanelHeight = 232f;

        /// <summary>The isolation-tool overlay screen rect for a given screen size — RIGHT-anchored, vertically
        /// centred toward the LOWER half (below the upper-right F9 nudge panel), clamped on-screen. Pure + static
        /// for the regression guard (no render needed).</summary>
        public static Rect PanelRect(float screenW, float screenH)
        {
            float w = Mathf.Min(PanelWidth, Mathf.Max(140f, screenW - 24f));
            float x = Mathf.Max(12f, screenW - w - 12f);                       // right-anchored
            float y = Mathf.Max(46f, screenH * 0.55f);                         // lower half, below the top plates
            // Clamp the bottom on-screen.
            if (y + PanelHeight > screenH - 8f) y = Mathf.Max(46f, screenH - 8f - PanelHeight);
            return new Rect(x, y, w, PanelHeight);
        }

        void Awake()
        {
            // No GUILayout.* in this OnGUI (explicit Rects only) — skip IMGUI's Layout event pass (86cahhfp4 C2a).
            useGUILayout = false;
        }

        void Update()
        {
            // The ONLY normal-play cost: three key polls. Everything else is gated behind DebugOverlays.Visible.
            // F10 flips the shared legacy debug-overlay master (86cah90cp — Sponsor-grouped: F10 reveals THIS
            // panel + the WorldLookNudgeTool panel together). The F5/F6 toggles fire regardless of the master (so
            // the Sponsor can A/B without the panel up), but the readout only RENDERS behind the master. Resolve
            // lazily on first use.
            if (Input.GetKeyDown(overlayToggleKey))
            {
                DebugOverlays.Toggle();
                Debug.Log("[SneakIsolation] debug overlays " +
                          (DebugOverlays.Visible ? "ON (F10 to hide) — sneak-isolation + world-look panels"
                                                 : "off (clean screen)"));
            }
            if (Input.GetKeyDown(footSyncToggleKey))
            {
                Resolve();
                if (_castaway != null)
                {
                    _castaway.footSync = !_castaway.footSync;
                    Debug.Log("[SneakIsolation] foot-sync (#186 coupling) " +
                              (_castaway.footSync ? "ON (authored→speed-scaled playback)"
                                                  : "OFF (constant authored cadence) — does the per-step jerk vanish?"));
                }
            }
            if (Input.GetKeyDown(sneakSpeedSnapToggleKey))
            {
                Resolve();
                if (_player != null)
                {
                    bool snap = !_player.SneakSpeedSnappedToWalk;
                    _player.SetSneakSpeedSnapToWalk(snap);
                    Debug.Log("[SneakIsolation] sneak-speed snap-to-walk " +
                              (snap ? "ON (crouch moves at NORMAL walk speed) — does the jerk vanish at walk speed?"
                                    : "off (reduced sneak speed — shipped behavior)"));
                }
            }
        }

        private void Resolve()
        {
            if (_castaway == null)
                _castaway = Object.FindAnyObjectByType<CastawayCharacter>(FindObjectsInactive.Include);
            if (_player == null)
                _player = Object.FindAnyObjectByType<WasdMovement>(FindObjectsInactive.Include);
            if (_agent == null && _player != null)
                _agent = _player.GetComponent<NavMeshAgent>();
        }

        void OnGUI()
        {
            if (!DebugOverlays.Visible) return; // F1 master gate (86cafd6d6) — clean screen by default

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = new Color(1f, 0.82f, 0.5f); // warm header (distinct from F8 cool-blue)
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                _labelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
                _valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _valueStyle.normal.textColor = new Color(0.95f, 0.95f, 1f);
                _toggleOnStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _toggleOnStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);   // GREEN = isolation control engaged
                _toggleOffStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _toggleOffStyle.normal.textColor = new Color(0.8f, 0.82f, 0.86f); // grey = shipped default
            }

            Resolve();

            Rect panel = PanelRect(Screen.width, Screen.height);
            GUI.color = new Color(0f, 0f, 0f, 0.74f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float lx = panel.x + 12f, lw = panel.width - 24f, y = panel.y;

            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "SNEAK ISOLATION  (debug — F10 to hide)", _titleStyle);

            if (_castaway == null && _player == null)
            {
                GUI.Label(new Rect(lx, y + 34f, lw, 22f), "(no Castaway/WasdMovement in scene)", _labelStyle);
                return;
            }

            // The two ISOLATION TOGGLES (the handles — the Sponsor flips these to A/B the cause).
            bool footSync = _castaway != null && _castaway.footSync;
            GUI.Label(new Rect(lx, y + 34f, lw, 20f),
                "F5  foot-sync (#186):  " + (footSync ? "ON  (speed-scaled)" : "OFF (constant cadence)"),
                footSync ? _toggleOffStyle : _toggleOnStyle);   // OFF is the isolation control → highlight green

            bool snap = _player != null && _player.SneakSpeedSnappedToWalk;
            GUI.Label(new Rect(lx, y + 56f, lw, 20f),
                "F6  sneak-speed snap→walk:  " + (snap ? "ON  (walk speed)" : "off (reduced sneak)"),
                snap ? _toggleOnStyle : _toggleOffStyle);       // ON is the isolation control → highlight green

            // The LIVE per-frame readout — which NUMBER oscillates per gait cycle is the discriminator.
            float vmag = _agent != null ? new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude : float.NaN;
            GUI.Label(new Rect(lx, y + 86f, lw, 20f), "agent vel mag:  " + Fmt(vmag) + " u/s", _valueStyle);

            float spd = _castaway != null ? _castaway.CurrentAnimatorSpeedParam : float.NaN;
            float gspd = _castaway != null ? _castaway.CurrentAnimatorGlobalSpeed : float.NaN;
            GUI.Label(new Rect(lx, y + 108f, lw, 20f),
                "Animator Speed param:  " + Fmt(spd) + "    animator.speed:  " + Fmt(gspd), _valueStyle);

            float mul = _castaway != null ? _castaway.CurrentLocoSpeedMul : float.NaN;
            GUI.Label(new Rect(lx, y + 130f, lw, 20f), "foot-sync mul (LocoSpeedMul):  " + Fmt(mul), _valueStyle);

            string clip = _castaway != null ? (_castaway.CurrentClipName ?? "<none>") : "<no avatar>";
            float nt = _castaway != null ? _castaway.CurrentStateNormalizedTime : float.NaN;
            GUI.Label(new Rect(lx, y + 152f, lw, 20f), "clip:  " + clip, _valueStyle);
            GUI.Label(new Rect(lx, y + 174f, lw, 20f),
                "normTime:  " + Fmt(nt) + "    effSpeed:  " +
                Fmt(_castaway != null ? _castaway.CurrentStateEffectiveSpeed : float.NaN), _valueStyle);

            GUI.Label(new Rect(lx, y + 200f, lw, 28f),
                "Sneak (Ctrl+W). Flip F5 off — does the 2-step jerk vanish?", _labelStyle);
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");
    }
}
