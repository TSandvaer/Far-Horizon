using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED debug NUDGE TOOL for dialing the axe placements IN-GAME (ticket 86ca8ce6y SOAKFIX5 —
    /// the axe-nudge reframe). Instead of the team agonizing over the exact held-axe / stump-axe transforms
    /// headless, the Sponsor finalizes them himself in the shipped build: this tool lets him SELECT a target
    /// (the held axe or the stump axe), NUDGE its position (XYZ) + rotation (pitch/yaw/roll) in small steps,
    /// and READ the live values off the on-screen HUD + the log, then report the numbers to bake into the
    /// constants. SOAKFIX8 — BOTH axes now report a LOCAL pose (held = hand-local so its rotation tracks the
    /// bone through turns; stump = CraftSpot-local): held -> HeldAxeLocalPos/Euler, stump -> StumpAxeLocalPos/Euler.
    ///
    /// BUILD-GATED / INERT IN NORMAL PLAY (the hard requirement): the tool does NOTHING until the Sponsor
    /// TOGGLES it on with the debug key (F9). Until then it never reads gameplay input, never moves an axe,
    /// and draws no HUD — so a normal soak is completely unaffected (a soak screenshot/judgement sees the
    /// shipped default pose, not a tool overlay). The component is serialized onto the Boot object editor-
    /// time (like the verify-capture siblings) so it ships, but stays asleep behind the toggle.
    ///
    /// TARGET FRAMES handled correctly:
    ///   - HELD axe: parented to the right-hand bone. SOAKFIX8 — its serialized pose is now a HAND-LOCAL
    ///     transform (localPosition + localRotation), so BOTH position AND rotation ride the bone in every
    ///     facing (the prior world-fixed rotation could not survive a turn — the Sponsor's "points the same
    ///     way on X always" bug). The tool nudges the axe's LOCAL transform directly (localPosition +
    ///     localEulerAngles), which IS what serializes — so dial == baked == in-motion (no world-vs-local
    ///     discrepancy, and the axe keeps tracking the hand through turns WHILE being dialed). It REPORTS
    ///     localPos + localEuler, ready to paste into HeldAxeLocalPos / HeldAxeLocalEuler.
    ///   - STUMP axe: parented to the unscaled CraftSpot (world-1u); its serialized pose IS its LOCAL
    ///     transform (no bone-frame trap). The tool nudges localPosition/localEulerAngles directly and
    ///     reports them — exactly StumpAxeLocalPos / StumpAxeLocalEuler.
    ///
    /// Pure legacy-Input + IMGUI (the project's input + HUD idiom — ClickToMove/OrbitCamera/BootHud), no
    /// new-Input-System or shader dependency, build-safe.
    /// </summary>
    public class AxeNudgeTool : MonoBehaviour
    {
        [Tooltip("Debug toggle key. The tool is INERT until pressed — a normal soak never sees it.")]
        public KeyCode toggleKey = KeyCode.F9;
        [Tooltip("Cycle the nudge target (held axe <-> stump axe).")]
        public KeyCode cycleKey = KeyCode.Tab;
        [Tooltip("Position nudge step (world units). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float posStep = 0.02f;
        [Tooltip("Rotation nudge step (degrees). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float rotStep = 2f;

        // Names of the two serialized axe objects (must match MovementCameraScene.HeroAxeObjectName /
        // StumpAxeObjectName — kept as string literals so Runtime has no Editor-asm dependency).
        private const string HeldAxeName = "HeroAxe";
        private const string StumpAxeName = "StumpAxe";

        private bool _active;
        private int _target;            // 0 = held, 1 = stump
        private Transform _held, _stump;
        private GUIStyle _style, _hintStyle, _titleStyle;

        // Panel size (SOAKFIX6 — carries a purpose header + a "what this does" line + the controls).
        public const float PanelWidth = 470f;
        public const float PanelHeight = 214f;

        /// <summary>
        /// The nudge-panel screen rect for a given screen size — RIGHT-anchored + vertically centred
        /// (SOAKFIX6: moved OFF SurvivalHud's bottom-left hotbar). Pure + static so the off-hotbar contract
        /// is regression-guarded without a render (AxeNudgeToolPlayModeTests.NudgePanel_ClearsTheHotbar).
        /// </summary>
        public static Rect PanelRect(float screenW, float screenH)
        {
            float x = screenW - PanelWidth - 12f;                 // right edge — clear of the bottom-left hotbar
            float y = Mathf.Max(46f, (screenH - PanelHeight) * 0.5f); // vertically centred, below the top-right stamp
            return new Rect(x, y, PanelWidth, PanelHeight);
        }

        /// <summary>
        /// SurvivalHud's bottom-left hotbar footprint (warmth bar + inventory ledger) for a given screen
        /// size — the zone the nudge panel must NOT overlap. Mirrors SurvivalHud's anchor math (warmth bar
        /// x16 w260 y=H-44 h28; ledger y=H-80 h28), padded. Used by the off-hotbar regression guard.
        /// </summary>
        public static Rect HotbarZone(float screenW, float screenH)
        {
            // Left x10..280 (x16 w260 + 6px plate pad on each side); top = ledger y (H-83), bottom = warmth
            // bar bottom (H-16). A generous box covering both SurvivalHud rows.
            float top = screenH - 86f, bottom = screenH - 14f;
            return new Rect(10f, top, 272f, bottom - top);
        }

        void Update()
        {
            // The ONLY thing that runs in normal play: watch for the debug toggle. Cheap, no allocs, no
            // gameplay effect. Everything else is gated behind _active.
            if (Input.GetKeyDown(toggleKey))
            {
                _active = !_active;
                if (_active) Resolve();
                Debug.Log("[AxeNudgeTool] " + (_active ? "ENABLED — nudge the axes; values on HUD/log" : "disabled"));
                if (_active) LogCurrent();
            }
            if (!_active) return;

            if (Input.GetKeyDown(cycleKey))
            {
                _target = 1 - _target;
                Debug.Log("[AxeNudgeTool] target = " + (_target == 0 ? "HELD axe" : "STUMP axe"));
                LogCurrent();
            }

            Transform t = Current();
            if (t == null) { if (Input.GetKeyDown(cycleKey)) Resolve(); return; }

            float ps = posStep * StepMul();
            float rs = rotStep * StepMul();
            bool changed = false;

            // POSITION nudges (held = WORLD; stump = LOCAL). Arrow keys = X/Z; PageUp/Down = Y.
            Vector3 dp = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dp.x += ps;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) dp.x -= ps;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dp.z += ps;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dp.z -= ps;
            if (Input.GetKeyDown(KeyCode.PageUp)) dp.y += ps;
            if (Input.GetKeyDown(KeyCode.PageDown)) dp.y -= ps;

            // ROTATION nudges. T/G = pitch (X), Y/H = yaw (Y), U/J = roll (Z).
            Vector3 dr = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.T)) dr.x += rs;
            if (Input.GetKeyDown(KeyCode.G)) dr.x -= rs;
            if (Input.GetKeyDown(KeyCode.Y)) dr.y += rs;
            if (Input.GetKeyDown(KeyCode.H)) dr.y -= rs;
            if (Input.GetKeyDown(KeyCode.U)) dr.z += rs;
            if (Input.GetKeyDown(KeyCode.J)) dr.z -= rs;

            if (dp != Vector3.zero || dr != Vector3.zero)
            {
                // SOAKFIX8 — BOTH targets nudge their LOCAL transform (the held axe is now hand-LOCAL, like
                // the stump). For the held axe this is the FIX: a local nudge rides the bone, so the axe keeps
                // tracking the hand through turns WHILE being dialed (no world re-apply that pinned the
                // rotation to a fixed heading — the Sponsor's "points the same way on X always" bug). The
                // local transform IS exactly what serializes, so dial == baked == in-motion.
                t.localPosition += dp;
                t.localEulerAngles += dr;
                changed = true;
            }

            if (changed) LogCurrent();
        }

        private Transform Current() => _target == 0 ? _held : _stump;

        private float StepMul()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 0.2f;
            return 1f;
        }

        private void Resolve()
        {
            _held = FindByName(HeldAxeName);
            _stump = FindByName(StumpAxeName);
            if (_held == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName + "' not found");
            if (_stump == null) Debug.LogWarning("[AxeNudgeTool] stump axe '" + StumpAxeName + "' not found");
        }

        private Transform FindByName(string n)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == n) return t;
            return null;
        }

        // Log the values in a copy-pasteable form (the Sponsor reads these off the log to bake into the
        // constants). SOAKFIX8 — BOTH are now LOCAL pos + euler (the held axe is hand-LOCAL so it rides the
        // bone through turns); paste held into HeldAxeLocalPos/Euler, stump into StumpAxeLocalPos/Euler.
        private void LogCurrent()
        {
            if (_target == 0 && _held != null)
                Debug.Log($"[AxeNudgeTool] HELD  HeldAxeLocalPos=({_held.localPosition.x:F4}f,{_held.localPosition.y:F4}f,{_held.localPosition.z:F4}f)  " +
                          $"HeldAxeLocalEuler=({Norm(_held.localEulerAngles.x):F1}f,{Norm(_held.localEulerAngles.y):F1}f,{Norm(_held.localEulerAngles.z):F1}f)");
            else if (_target == 1 && _stump != null)
                Debug.Log($"[AxeNudgeTool] STUMP StumpAxeLocalPos=({_stump.localPosition.x:F3}f,{_stump.localPosition.y:F3}f,{_stump.localPosition.z:F3}f)  " +
                          $"StumpAxeLocalEuler=({Norm(_stump.localEulerAngles.x):F1}f,{Norm(_stump.localEulerAngles.y):F1}f,{Norm(_stump.localEulerAngles.z):F1}f)");
        }

        private static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; return a; }

        void OnGUI()
        {
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

            // PANEL PLACEMENT (86ca8ce6y SOAKFIX6 — "the overlay covers the inventory hotbar + its purpose was
            // unclear"). The prior panel sat bottom-LEFT (x8, y=height-176) — directly over SurvivalHud's
            // bottom-left warmth bar + inventory ledger. Move it RIGHT-anchored + VERTICALLY CENTRED, which is
            // clear of: SurvivalHud's bottom-left hotbar, BootHud's top-left title plate, AND BootHud's
            // top-right build-stamp plate (y 8..34). A taller panel now (it carries a purpose header + a
            // "what this does" line) so the controls read clearly. Rect computed by PanelRect (pure, testable)
            // so the off-hotbar contract is regression-guarded without a render.
            Rect panel = PanelRect(Screen.width, Screen.height);
            float x = panel.x, y = panel.y, w = panel.width, h = panel.height;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string tgt = _target == 0 ? "HELD axe (in hand — local, tracks the hand)" : "STUMP axe (in block — local)";
            string vals;
            if (_target == 0 && _held != null)
                vals = $"localPos=({_held.localPosition.x:F4},{_held.localPosition.y:F4},{_held.localPosition.z:F4})  " +
                       $"euler=({Norm(_held.localEulerAngles.x):F1},{Norm(_held.localEulerAngles.y):F1},{Norm(_held.localEulerAngles.z):F1})";
            else if (_target == 1 && _stump != null)
                vals = $"localPos=({_stump.localPosition.x:F3},{_stump.localPosition.y:F3},{_stump.localPosition.z:F3})  " +
                       $"euler=({Norm(_stump.localEulerAngles.x):F1},{Norm(_stump.localEulerAngles.y):F1},{Norm(_stump.localEulerAngles.z):F1})";
            else vals = "(axe not found)";

            float lx = x + 12f, lw = w - 24f;
            // PURPOSE header + a one-line "what this does" so the tool is self-explanatory (was unclear).
            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "AXE NUDGE TOOL  (debug — F9 to close)", _titleStyle);
            GUI.Label(new Rect(lx, y + 30f, lw, 20f),
                "Dial the axe's position/angle in-game, then read the values to bake.", _hintStyle);

            GUI.Label(new Rect(lx, y + 56f, lw, 22f), "Editing: " + tgt, _style);
            GUI.Label(new Rect(lx, y + 78f, lw, 22f), vals, _style);

            GUI.Label(new Rect(lx, y + 104f, lw, 20f), "[Tab] switch held / stump axe", _hintStyle);
            GUI.Label(new Rect(lx, y + 124f, lw, 20f), "Move:   ←/→ = X    ↑/↓ = Z    PgUp/PgDn = Y", _hintStyle);
            GUI.Label(new Rect(lx, y + 144f, lw, 20f), "Rotate: T/G = pitch   Y/H = yaw   U/J = roll", _hintStyle);
            GUI.Label(new Rect(lx, y + 164f, lw, 20f), "Hold Shift = 5x step    Hold Ctrl = 0.2x step", _hintStyle);
            GUI.Label(new Rect(lx, y + 188f, lw, 20f),
                "Values also print to the log each nudge — copy them to bake the default.", _hintStyle);
        }
    }
}
