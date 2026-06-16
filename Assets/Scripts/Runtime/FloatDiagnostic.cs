using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED LIVE FLOAT-DIAGNOSTIC instrument (ticket 86ca8rdkp — "we have been chasing this floating
    /// issue for many iterations, you have to add logging or nudging"). Instead of one more BLIND snap tweak,
    /// this makes the castaway's feet-vs-sand float MEASURABLE on-screen and DIALABLE to zero. The Sponsor
    /// walks the shoreline, SEES the exact GAP at any spot, dials GROUND-Y until GAP≈0 (on the F9 AxeNudgeTool
    /// GROUND-Y target — which now shows this same GAP), reports the value, and we bake it. No more "is it
    /// fixed" arguments — the number says so.
    ///
    /// WHAT IT SHOWS (a persistent overlay, updating EVERY FRAME during gameplay):
    ///   - feet world-Y   — the avatar feet (CastawayCharacter.FeetWorldY; the FBX origin sits at the feet).
    ///   - ground hit-Y   — the visible terrain raycast-hit directly under him (CastawayCharacter.GroundHitWorldY).
    ///   - GAP = feet − ground   — highlighted RED if &gt; ~1 cm (= floating); GREEN if planted (the bug readout).
    ///   - moving?        — rest vs walk (the during-walk float was state-dependent).
    ///   - snap rate      — the active convergence rate (rest vs move).
    ///   - groundYOffset  — the current Sponsor-dialable offset (the knob he turns to drive the GAP to 0).
    ///
    /// TWO SURFACES (per the ticket):
    ///   1. STANDALONE always-visible TOGGLE on its OWN key (F8 — distinct from F9 character / F10 world dials)
    ///      so the Sponsor walks around freely with the readout up. INERT until F8 is pressed (a normal soak
    ///      never sees it — the build-gated/inert contract every dial tool in this project follows).
    ///   2. The SAME GAP is surfaced RIGHT IN the F9 AxeNudgeTool GROUND-Y panel (AxeNudgeTool.OnGUI reads
    ///      CastawayCharacter.FloatGap), so as he dials groundYOffset (PgUp/PgDn) he WATCHES the GAP shrink to
    ///      ~0 in real time — dial + measurement together.
    ///   3. The same line is LOGGED ~1×/sec (feet-Y / ground-Y / gap / offset / moving) so the ORCHESTRATOR
    ///      can read ground truth from the player log too (we keep getting surprised at runtime — dump it).
    ///      The log runs when the overlay is toggled ON, OR when the build is launched with -floatTrace (a
    ///      log-only soak dump with no visible overlay — for an orchestrator-driven ground-truth capture).
    ///
    /// Pure legacy-Input + IMGUI (the project's input + HUD idiom — ClickToMove/OrbitCamera/BootHud/AxeNudgeTool),
    /// no new-Input-System or shader dependency, build-safe. Serialized onto the Boot object editor-time (like
    /// the verify-capture + AxeNudgeTool siblings) so it ships, but stays asleep behind the F8 toggle.
    /// </summary>
    public class FloatDiagnostic : MonoBehaviour
    {
        [Tooltip("Standalone overlay toggle key. The overlay is INERT until pressed — a normal soak never " +
                 "sees it. F8 is distinct from F9 (character/axe nudge) and F10 (world dials).")]
        public KeyCode toggleKey = KeyCode.F8;

        [Tooltip("GAP threshold (world units) above which the readout flags RED = floating. ~1 cm — below " +
                 "this the feet read as planted on the visible sand.")]
        public float floatRedThreshold = 0.01f;

        [Tooltip("How often (seconds) to emit the ~1Hz log line (so the orchestrator can read ground truth " +
                 "from the player log). 1 = once per second.")]
        public float logInterval = 1f;

        // Command-line flag that drives the ~1Hz LOG even with the overlay OFF (a log-only soak dump for an
        // orchestrator-driven ground-truth capture — no visible overlay, just the log line).
        private const string LogOnlyArg = "-floatTrace";

        private bool _active;          // the on-screen overlay (F8). INERT until toggled.
        private bool _logOnly;         // -floatTrace: drive the log even with the overlay off.
        private CastawayCharacter _castaway;
        private float _nextLogTime;
        private GUIStyle _labelStyle, _valueStyle, _gapGoodStyle, _gapBadStyle, _titleStyle;

        // Panel geometry — LEFT-anchored + vertically centred so it clears BootHud's top plates AND the F9
        // AxeNudgeTool panel (which is RIGHT-anchored, AxeNudgeTool.PanelRect) AND SurvivalHud's bottom-left
        // hotbar. Pure + static so the placement contract is regression-guarded without a render.
        public const float PanelWidth = 360f;
        public const float PanelHeight = 196f;

        /// <summary>The float-diagnostic overlay screen rect for a given screen size — LEFT-anchored,
        /// vertically centred, clamped on-screen. Placed on the LEFT so it never overlaps the RIGHT-anchored
        /// F9 nudge panel (both can be up at once — F8 overlay + F9 dialing). Pure + static for the
        /// regression guard (FloatDiagnosticPlayModeTests).</summary>
        public static Rect PanelRect(float screenW, float screenH)
        {
            float x = 12f;
            float y = Mathf.Max(46f, (screenH - PanelHeight) * 0.5f); // below the top-left title plate
            // On a window narrower than the panel, clamp width so the box stays fully on-screen.
            float w = Mathf.Min(PanelWidth, Mathf.Max(120f, screenW - 24f));
            return new Rect(x, y, w, PanelHeight);
        }

        /// <summary>Force the overlay ON (verification-only hook). The shipped-build capture
        /// (FloatDiagnosticVerifyCapture) calls this so the live GAP readout renders into the captured frame
        /// without synthesizing an F8 key-down (the harness can't). Inert for normal play — Update's F8 toggle
        /// still owns the interactive on/off.</summary>
        public void ShowOverlay()
        {
            _active = true;
            Resolve();
            // EXTENSIVE-DEBUG round (86ca8rdkp): drive the castaway's per-frame [FloatTrace] while the overlay
            // is up, so the orchestrator reads the full per-frame discrepancy dump (bounds.min vs baked-actual
            // vs shadow vs ground) from the player log too — not just the ~1Hz summary line.
            if (_castaway != null) _castaway.SetFrameTrace(true);
        }

        /// <summary>Whether the overlay is currently drawing (for the regression guard — proves the build-gated
        /// default is OFF, and that ShowOverlay flips it on).</summary>
        public bool OverlayActive => _active;

        void Update()
        {
            // Resolve once -floatTrace presence (drives the log-only dump). Cheap; do it lazily so a test that
            // adds the component mid-run picks it up.
            if (!_resolvedArgs)
            {
                _logOnly = HasArg(LogOnlyArg); _resolvedArgs = true;
                if (_logOnly) { Resolve(); if (_castaway != null) _castaway.SetFrameTrace(true); }
            }

            // The ONLY gameplay-affecting work in normal play: watch the F8 toggle. No allocs, no gameplay
            // effect. Everything else is gated behind _active || _logOnly.
            if (Input.GetKeyDown(toggleKey))
            {
                _active = !_active;
                if (_active) Resolve();
                // Drive the castaway's per-frame [FloatTrace] in lock-step with the overlay (extensive logging).
                if (_castaway != null) _castaway.SetFrameTrace(_active || _logOnly);
                Debug.Log("[FloatDiagnostic] overlay " + (_active ? "ON — feet/ground/GAP live; dial GROUND-Y to GAP≈0"
                                                                  : "off"));
            }

            if (!_active && !_logOnly) return; // inert in a normal soak

            if (_castaway == null) Resolve();

            // ~1Hz ground-truth log (runs when the overlay is on OR -floatTrace). Time.time so it ticks in
            // real seconds regardless of frame rate. The line is copy-pasteable / greppable ([FloatTrace]).
            if (Time.time >= _nextLogTime)
            {
                _nextLogTime = Time.time + Mathf.Max(0.05f, logInterval);
                LogLine();
            }
        }

        private bool _resolvedArgs;

        private void Resolve()
        {
            if (_castaway == null)
                _castaway = Object.FindAnyObjectByType<CastawayCharacter>(FindObjectsInactive.Include);
        }

        // The ~1Hz ground-truth log line — feet-Y / ground-Y / gap / offset / moving / rate. Greppable tag
        // [FloatTrace] so the orchestrator can pull it out of the player log (Player.log) directly.
        private void LogLine()
        {
            if (_castaway == null)
            {
                Debug.Log("[FloatTrace] no CastawayCharacter found");
                return;
            }
            // FeetWorldY / FloatGap now read the BAKED actual lowest-vertex SOLE (86ca8rdkp EXTENSIVE-DEBUG
            // round) — NOT SMR.bounds.min.y. Surface the bounds-min proxy + the proxy-root gap alongside so the
            // log always proves the gauge reads the TRUE sole, and the bounds floor sits below it (false-green).
            float feet = _castaway.FeetWorldY, ground = _castaway.GroundHitWorldY, gap = _castaway.FloatGap;
            bool floating = !float.IsNaN(gap) && Mathf.Abs(gap) > floatRedThreshold;
            Debug.Log($"[FloatTrace] bakedSoleY={Fmt(feet)} boundsMinY(OLD proxy)={Fmt(_castaway.SmrBoundsMinWorldY)} " +
                      $"groundY={Fmt(ground)} GAP(bakedSole-ground)={Fmt(gap)} " +
                      $"({(floating ? "FLOATING" : "planted")})  proxyRootGap={Fmt(_castaway.ProxyRootFloatGap)} " +
                      $"offset={_castaway.groundYOffset:F4} moving={_castaway.IsMovingForSnap} " +
                      $"snapRate={_castaway.ActiveSnapRate:F0}");
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");

        void OnGUI()
        {
            if (!_active) return; // INERT in normal play — no overlay unless toggled on with F8

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = new Color(0.55f, 0.85f, 1f); // cool-blue header (distinct from F9 gold)
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                _labelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
                _valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _valueStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
                _gapGoodStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                _gapGoodStyle.normal.textColor = new Color(0.4f, 1f, 0.5f); // GREEN = planted
                _gapBadStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                _gapBadStyle.normal.textColor = new Color(1f, 0.45f, 0.4f); // RED = floating
            }

            Rect panel = PanelRect(Screen.width, Screen.height);
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float lx = panel.x + 12f, lw = panel.width - 24f, y = panel.y;

            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "FLOAT DIAGNOSTIC  (debug — F8 to close)", _titleStyle);

            if (_castaway == null)
            {
                GUI.Label(new Rect(lx, y + 34f, lw, 22f), "(no CastawayCharacter in scene)", _labelStyle);
                return;
            }

            float feet = _castaway.FeetWorldY, ground = _castaway.GroundHitWorldY, gap = _castaway.FloatGap;
            bool valid = !float.IsNaN(gap);
            bool floating = valid && Mathf.Abs(gap) > floatRedThreshold;

            GUI.Label(new Rect(lx, y + 34f, lw, 20f), "rendered sole-Y:  " + Fmt(feet), _valueStyle);
            GUI.Label(new Rect(lx, y + 56f, lw, 20f), "ground hit-Y:  " + Fmt(ground), _valueStyle);

            // THE number — big, colour-coded. Now the HONEST gap: rendered SOLE − ground (GAP=0 ⟺ visible soles
            // on the sand). GREEN ≈ planted, RED = floating. The old proxy-root gap is shown small below so the
            // false-green (root-gap 0 while the mesh floats) can never masquerade as planted again.
            string gapText = "GAP (sole−ground):  " + Fmt(gap) +
                             (valid ? (floating ? "   ◄ FLOATING" : "   ◄ planted") : "");
            GUI.Label(new Rect(lx, y + 80f, lw, 24f), gapText, floating ? _gapBadStyle : _gapGoodStyle);

            GUI.Label(new Rect(lx, y + 110f, lw, 20f),
                "moving:  " + (_castaway.IsMovingForSnap ? "YES (walking)" : "no (rest)") +
                "    snap-rate:  " + _castaway.ActiveSnapRate.ToString("F0"), _labelStyle);
            GUI.Label(new Rect(lx, y + 132f, lw, 20f),
                "groundYOffset:  " + _castaway.groundYOffset.ToString("F4"), _labelStyle);
            GUI.Label(new Rect(lx, y + 158f, lw, 30f),
                "Dial GROUND-Y on the F9 panel (PgUp/PgDn) until GAP≈0 while WALKING.", _labelStyle);
        }

        private static bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
