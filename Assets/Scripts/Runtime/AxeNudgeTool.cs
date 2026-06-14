using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED debug NUDGE TOOL for dialing the axe placements IN-GAME (ticket 86ca8ce6y SOAKFIX5 —
    /// the axe-nudge reframe). Instead of the team agonizing over the exact held-axe / stump-axe transforms
    /// headless, the Sponsor finalizes them himself in the shipped build: this tool lets him SELECT a target
    /// (the held axe or the stump axe), NUDGE its position (XYZ) + rotation (pitch/yaw/roll) in small steps,
    /// and READ the live values off the on-screen HUD + the log, then report the numbers to bake into the
    /// constants (MovementCameraScene.HeldAxeWorldOffsetFromHand/Euler + StumpAxeLocalPos/Euler).
    ///
    /// BUILD-GATED / INERT IN NORMAL PLAY (the hard requirement): the tool does NOTHING until the Sponsor
    /// TOGGLES it on with the debug key (F9). Until then it never reads gameplay input, never moves an axe,
    /// and draws no HUD — so a normal soak is completely unaffected (a soak screenshot/judgement sees the
    /// shipped default pose, not a tool overlay). The component is serialized onto the Boot object editor-
    /// time (like the verify-capture siblings) so it ships, but stays asleep behind the toggle.
    ///
    /// TARGET FRAMES handled correctly:
    ///   - HELD axe: parented to the right-hand bone; its serialized pose is a LOCAL transform that Unity
    ///     back-solved from a WORLD pose set after parenting (the rotated-bone-frame finding). The tool
    ///     nudges the axe's WORLD position/rotation directly (intuitive for the Sponsor, and the bone frame
    ///     is irrelevant when nudging in world), then REPORTS the WORLD OFFSET-FROM-HAND + WORLD EULER —
    ///     exactly the two constants AttachHeroAxeToHand consumes (axe.transform.position = hand.position +
    ///     HeldAxeWorldOffsetFromHand; rotation = Euler(HeldAxeWorldEuler)). So the reported numbers paste
    ///     straight into the constants. (The axe still rides the hand each frame — the world nudge re-applies
    ///     against the LIVE hand position every frame while the tool is active.)
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
        private Transform _held, _stump, _hand;
        private GUIStyle _style, _hintStyle;

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
                if (_target == 0)
                {
                    // HELD: nudge in WORLD space (the serialized pose was authored in world; the bone frame
                    // is irrelevant for a world nudge). The axe rides the hand, so we re-apply a stored WORLD
                    // OFFSET-FROM-HAND each frame below; here we just adjust that offset + the world euler.
                    _heldWorldOffset += dp;
                    _heldWorldEuler += dr;
                }
                else
                {
                    // STUMP: nudge its LOCAL transform directly (that IS what serializes).
                    t.localPosition += dp;
                    t.localEulerAngles += dr;
                }
                changed = true;
            }

            // HELD axe rides the live hand: re-apply the world pose every frame while active so the nudge
            // sticks against the moving hand (and the reported offset stays hand-relative).
            if (_held != null && _hand != null)
            {
                _held.position = _hand.position + _heldWorldOffset;
                _held.rotation = Quaternion.Euler(_heldWorldEuler);
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

        // Stored WORLD pose for the held axe (offset-from-hand + euler) — the values that map to the
        // HeldAxeWorldOffsetFromHand / HeldAxeWorldEuler constants. Seeded from the live axe on resolve.
        private Vector3 _heldWorldOffset, _heldWorldEuler;

        private void Resolve()
        {
            _held = FindByName(HeldAxeName);
            _stump = FindByName(StumpAxeName);
            _hand = _held != null ? _held.parent : null;
            if (_held != null && _hand != null)
            {
                _heldWorldOffset = _held.position - _hand.position;
                _heldWorldEuler = _held.rotation.eulerAngles;
            }
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
        // constants). Held = WORLD offset-from-hand + euler; stump = LOCAL pos + euler.
        private void LogCurrent()
        {
            if (_target == 0 && _held != null)
                Debug.Log($"[AxeNudgeTool] HELD  HeldAxeWorldOffsetFromHand=({_heldWorldOffset.x:F3}f,{_heldWorldOffset.y:F3}f,{_heldWorldOffset.z:F3}f)  " +
                          $"HeldAxeWorldEuler=({Norm(_heldWorldEuler.x):F1}f,{Norm(_heldWorldEuler.y):F1}f,{Norm(_heldWorldEuler.z):F1}f)");
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
            }

            float w = 540f, h = 168f, x = 8f, y = Screen.height - h - 8f;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string tgt = _target == 0 ? "HELD axe (world)" : "STUMP axe (local)";
            string vals;
            if (_target == 0 && _held != null)
                vals = $"offsetFromHand=({_heldWorldOffset.x:F3},{_heldWorldOffset.y:F3},{_heldWorldOffset.z:F3})  " +
                       $"euler=({Norm(_heldWorldEuler.x):F1},{Norm(_heldWorldEuler.y):F1},{Norm(_heldWorldEuler.z):F1})";
            else if (_target == 1 && _stump != null)
                vals = $"localPos=({_stump.localPosition.x:F3},{_stump.localPosition.y:F3},{_stump.localPosition.z:F3})  " +
                       $"euler=({Norm(_stump.localEulerAngles.x):F1},{Norm(_stump.localEulerAngles.y):F1},{Norm(_stump.localEulerAngles.z):F1})";
            else vals = "(axe not found)";

            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), "AXE NUDGE — target: " + tgt, _style);
            GUI.Label(new Rect(x + 10, y + 28, w - 20, 22), vals, _style);
            GUI.Label(new Rect(x + 10, y + 54, w - 20, 20), "[Tab] cycle target   [F9] close tool", _hintStyle);
            GUI.Label(new Rect(x + 10, y + 74, w - 20, 20), "Move: ←/→ X   ↑/↓ Z   PgUp/PgDn Y", _hintStyle);
            GUI.Label(new Rect(x + 10, y + 94, w - 20, 20), "Rotate: T/G pitch   Y/H yaw   U/J roll", _hintStyle);
            GUI.Label(new Rect(x + 10, y + 114, w - 20, 20), "Hold Shift = 5x step   Hold Ctrl = 0.2x step", _hintStyle);
            GUI.Label(new Rect(x + 10, y + 138, w - 20, 20), "values logged each nudge — read them off the log to bake the constants", _hintStyle);
        }
    }
}
