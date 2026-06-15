using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED debug NUDGE TOOL for dialing the axe placements IN-GAME (ticket 86ca8ce6y SOAKFIX5 —
    /// the axe-nudge reframe). Instead of the team agonizing over the exact held-axe / stump-axe transforms
    /// headless, the Sponsor finalizes them himself in the shipped build: this tool lets him SELECT a target
    /// (the held axe or the stump axe), NUDGE its position (XYZ) + rotation (pitch/yaw/roll) in small steps,
    /// and READ the live values off the on-screen HUD + the log, then report the numbers to bake into the
    /// constants. SOAKFIX9 — the HELD axe now reports a SPLIT pose driven via its HeldAxeRig: POSITION is a
    /// WORLD-space offsetFromHand (WORLD units — so a nudge step is a sensible ~2 cm, NOT a 267×-lossy-bone
    /// metre-jump) and ROTATION is a HAND-RELATIVE relEuler (turns with the hand). The tool nudges the RIG's
    /// fields (worldOffsetFromHand / relEuler), so dial == baked == in-motion: held -> HeldAxeWorldOffsetFromHand
    /// / HeldAxeRelEuler. The STUMP axe is CraftSpot-local (unscaled, no bone trap): stump -> StumpAxeLocalPos/Euler.
    ///
    /// BUILD-GATED / INERT IN NORMAL PLAY (the hard requirement): the tool does NOTHING until the Sponsor
    /// TOGGLES it on with the debug key (F9). Until then it never reads gameplay input, never moves an axe,
    /// and draws no HUD — so a normal soak is completely unaffected (a soak screenshot/judgement sees the
    /// shipped default pose, not a tool overlay). The component is serialized onto the Boot object editor-
    /// time (like the verify-capture siblings) so it ships, but stays asleep behind the toggle.
    ///
    /// TARGET FRAMES handled correctly:
    ///   - HELD axe: parented to the right-hand bone, but POSE-DRIVEN by HeldAxeRig (SOAKFIX9). The tool
    ///     nudges the RIG's fields, NOT the transform: POSITION moves worldOffsetFromHand in WORLD units
    ///     (~2 cm/click — NOT the 267×-lossy-bone metre jump soakfix8's localPosition nudge caused), and
    ///     ROTATION moves relEuler (hand-relative, so the haft keeps turning with the hand WHILE dialed). The
    ///     rig re-applies position+rotation every frame from those fields, so dial == baked == in-motion. It
    ///     REPORTS offsetFromHand (world) + euler (hand-relative), ready to paste into
    ///     HeldAxeWorldOffsetFromHand / HeldAxeRelEuler.
    ///   - STUMP axe: parented to the unscaled CraftSpot (world-1u); its serialized pose IS its LOCAL
    ///     transform (no bone-frame trap). The tool nudges localPosition/localEulerAngles directly and
    ///     reports them — exactly StumpAxeLocalPos / StumpAxeLocalEuler.
    ///
    /// RE-SOAK (86ca8rdkp — the Sponsor's "the auto arm pose made it even WORSE when the axe is equipped, axe
    /// held too high/forward — do we need a nudging tool for the arm?"). A THIRD nudge target is added: the
    /// ARM POSE. Cycling onto it (Tab) lets the Sponsor dial the CastawayArmPose per-arm LOCAL-euler offsets
    /// IN-GAME — the RIGHT arm (spread off torso = pitch/X, raise = roll/Z, plus yaw/Y) and the LEFT arm
    /// (spread), switching between the two arms with [B]. Same UX as the axe nudge: the rotation keys nudge the
    /// euler, the panel shows live values, and the log prints copy-pasteable values to bake
    /// (CastawayArmPose.RightArmEuler / LeftArmEuler). Arms have NO position channel (only rotation offsets),
    /// so the position keys are inert on the arm target. Dialing sets seedEulersFromDegFields=false so a
    /// RebuildCached can't clobber the live dial.
    ///
    /// Pure legacy-Input + IMGUI (the project's input + HUD idiom — ClickToMove/OrbitCamera/BootHud), no
    /// new-Input-System or shader dependency, build-safe.
    /// </summary>
    public class AxeNudgeTool : MonoBehaviour
    {
        [Tooltip("Debug toggle key. The tool is INERT until pressed — a normal soak never sees it.")]
        public KeyCode toggleKey = KeyCode.F9;
        [Tooltip("Cycle the nudge target (held axe -> stump axe -> arm pose -> ...).")]
        public KeyCode cycleKey = KeyCode.Tab;
        [Tooltip("On the ARM-POSE target: switch which arm is dialed (right <-> left).")]
        public KeyCode armSwitchKey = KeyCode.B;
        [Tooltip("Position nudge step (world units). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float posStep = 0.02f;
        [Tooltip("Rotation nudge step (degrees). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float rotStep = 2f;

        // Names of the two serialized axe objects (must match MovementCameraScene.HeroAxeObjectName /
        // StumpAxeObjectName — kept as string literals so Runtime has no Editor-asm dependency).
        private const string HeldAxeName = "HeroAxe";
        private const string StumpAxeName = "StumpAxe";

        private bool _active;
        private int _target;            // 0 = held, 1 = stump, 2 = arm pose (RE-SOAK)
        private const int TargetCount = 3;
        private int _armSel;            // on the arm target: 0 = right arm, 1 = left arm
        private HeldAxeRig _heldRig;    // SOAKFIX9 — the held axe is pose-driven; the tool nudges the RIG's fields
        private Transform _stump;
        private CastawayArmPose _armPose; // RE-SOAK — the tool nudges its per-arm LOCAL-euler offsets
        private GUIStyle _style, _hintStyle, _titleStyle;

        // Panel size (SOAKFIX6 — carries a purpose header + a "what this does" line + the controls).
        // SOAKFIX10 — the offsetFromHand + euler values now live on their OWN lines (no single packed
        // value line that overflows the box), so the panel is WIDER (fits the longest value/hint line with
        // margin) and TALLER (one extra value row). The width still leaves the right-anchored box fully on
        // any screen ≥ the narrowest test size (800px: 532 + 0 margin < 800 → x ≥ 0; PanelRect also clamps).
        public const float PanelWidth = 532f;
        public const float PanelHeight = 236f;

        /// <summary>
        /// The nudge-panel screen rect for a given screen size — RIGHT-anchored + vertically centred
        /// (SOAKFIX6: moved OFF SurvivalHud's bottom-left hotbar). SOAKFIX10: x is CLAMPED to ≥ 12 so a
        /// window narrower than the panel can never push the box off the LEFT edge (the value text would
        /// then clip) — on any width the full panel stays on-screen. Pure + static so the on-screen +
        /// off-hotbar contract is regression-guarded without a render
        /// (AxeNudgeToolPlayModeTests.NudgePanel_ClearsTheHotbar).
        /// </summary>
        public static Rect PanelRect(float screenW, float screenH)
        {
            // Right-anchored, but clamp so a too-narrow window keeps the whole box (and its value text)
            // on-screen — never let x go negative (which would clip the left side of the value lines).
            float x = Mathf.Max(12f, screenW - PanelWidth - 12f);
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
                _target = (_target + 1) % TargetCount;
                Debug.Log("[AxeNudgeTool] target = " + TargetName());
                LogCurrent();
            }

            // On the ARM target, [B] switches which arm is dialed (right <-> left).
            if (_target == 2 && Input.GetKeyDown(armSwitchKey))
            {
                _armSel = 1 - _armSel;
                Debug.Log("[AxeNudgeTool] arm = " + (_armSel == 0 ? "RIGHT" : "LEFT"));
                LogCurrent();
            }

            // Bail if the current target isn't resolved (re-resolve on a cycle so a late-spawned axe is found).
            bool haveTarget = _target == 0 ? _heldRig != null : _target == 1 ? _stump != null : _armPose != null;
            if (!haveTarget) { if (Input.GetKeyDown(cycleKey)) Resolve(); return; }

            float ps = posStep * StepMul();
            float rs = rotStep * StepMul();
            bool changed = false;

            // POSITION nudges. Arrow keys = X/Z; PageUp/Down = Y.
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
                    // SOAKFIX9 — the HELD axe is nudged via its RIG, NOT its transform. POSITION moves the
                    // rig's WORLD-space offsetFromHand (posStep 0.02 = ~2 cm/click; the bone's 267× lossy
                    // scale never touches a world-unit field — the soakfix9 fix), and ROTATION moves the
                    // hand-relative relEuler (the haft keeps turning with the hand WHILE dialed). The rig
                    // re-applies position+rotation every frame from these fields, so dial == baked == in-motion.
                    _heldRig.worldOffsetFromHand += dp;
                    _heldRig.relEuler += dr;
                }
                else if (_target == 1)
                {
                    // STUMP axe: CraftSpot-local (unscaled, no bone trap) — nudge its LOCAL transform directly.
                    _stump.localPosition += dp;
                    _stump.localEulerAngles += dr;
                }
                else
                {
                    // ARM POSE (RE-SOAK): nudge the selected arm's LOCAL-euler offset (ROTATION only — arms
                    // have no position channel, so dp is inert here). pitch/X = spread off the torso, roll/Z =
                    // raise/reach, yaw/Y = twist (mostly useless per the -armTrace). Stop seeding the eulers
                    // from the deg fields so a RebuildCached can't clobber the live dial; rebuild the cached
                    // quats so the new pose composes THIS frame (dial == what-you-see).
                    _armPose.seedEulersFromDegFields = false;
                    if (_armSel == 0) _armPose.rightArmEuler += dr;
                    else _armPose.leftArmEuler += dr;
                    _armPose.RebuildCached();
                }
                changed = true;
            }

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
            // SOAKFIX9 — resolve the held axe's RIG (the tool nudges its world-offset + relEuler fields, not
            // the transform). The stump stays a plain transform (CraftSpot-local, no rig). RE-SOAK — also
            // resolve the CastawayArmPose (the tool nudges its per-arm LOCAL-euler offsets).
            Transform held = FindByName(HeldAxeName);
            _heldRig = held != null ? held.GetComponent<HeldAxeRig>() : null;
            _stump = FindByName(StumpAxeName);
            _armPose = Object.FindAnyObjectByType<CastawayArmPose>(FindObjectsInactive.Include);
            if (held == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName + "' not found");
            else if (_heldRig == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName +
                "' has no HeldAxeRig — cannot nudge its world-offset/relEuler (soakfix9 driver missing)");
            if (_stump == null) Debug.LogWarning("[AxeNudgeTool] stump axe '" + StumpAxeName + "' not found");
            if (_armPose == null) Debug.LogWarning("[AxeNudgeTool] no CastawayArmPose found — cannot nudge the arm pose");
        }

        private string TargetName() =>
            _target == 0 ? "HELD axe" : _target == 1 ? "STUMP axe" : "ARM pose (" + (_armSel == 0 ? "RIGHT" : "LEFT") + ")";

        private Transform FindByName(string n)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == n) return t;
            return null;
        }

        // Log the values in a copy-pasteable form (the Sponsor reads these off the log to bake into the
        // constants). SOAKFIX9 — the HELD axe reports its RIG's WORLD offsetFromHand + HAND-RELATIVE euler
        // (paste into HeldAxeWorldOffsetFromHand / HeldAxeRelEuler); the STUMP reports its LOCAL pose
        // (StumpAxeLocalPos/Euler). The held euler is NOT normalised-wrapped — relEuler accumulates as a raw
        // hand-relative euler the rig feeds straight to Quaternion.Euler, so it must round-trip exactly.
        private void LogCurrent()
        {
            if (_target == 0 && _heldRig != null)
                Debug.Log($"[AxeNudgeTool] HELD  HeldAxeWorldOffsetFromHand=({_heldRig.worldOffsetFromHand.x:F4}f,{_heldRig.worldOffsetFromHand.y:F4}f,{_heldRig.worldOffsetFromHand.z:F4}f)  " +
                          $"HeldAxeRelEuler=({_heldRig.relEuler.x:F1}f,{_heldRig.relEuler.y:F1}f,{_heldRig.relEuler.z:F1}f)");
            else if (_target == 1 && _stump != null)
                Debug.Log($"[AxeNudgeTool] STUMP StumpAxeLocalPos=({_stump.localPosition.x:F3}f,{_stump.localPosition.y:F3}f,{_stump.localPosition.z:F3}f)  " +
                          $"StumpAxeLocalEuler=({Norm(_stump.localEulerAngles.x):F1}f,{Norm(_stump.localEulerAngles.y):F1}f,{Norm(_stump.localEulerAngles.z):F1}f)");
            else if (_target == 2 && _armPose != null)
            {
                // Log BOTH arms so the Sponsor can paste the full pose (he edits whichever arm is selected).
                Vector3 r = _armPose.rightArmEuler, l = _armPose.leftArmEuler;
                Debug.Log($"[AxeNudgeTool] ARM ({(_armSel == 0 ? "RIGHT" : "LEFT")} selected)  " +
                          $"RightArmEuler=({r.x:F1}f,{r.y:F1}f,{r.z:F1}f)  LeftArmEuler=({l.x:F1}f,{l.y:F1}f,{l.z:F1}f)");
            }
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
            // top-right build-stamp plate (y 8..34). SOAKFIX10 — the box is now WIDER + TALLER and the
            // position/euler values sit on SEPARATE lines, so all three components of each are always fully
            // visible (the Sponsor's "3rd rotation value cut off the right edge" report). Rect computed by
            // PanelRect (pure, testable, x-clamped on-screen) so the on-screen + off-hotbar contract is
            // regression-guarded without a render.
            Rect panel = PanelRect(Screen.width, Screen.height);
            float x = panel.x, y = panel.y, w = panel.width, h = panel.height;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string tgt = _target == 0
                ? "HELD axe (in hand — WORLD offset + hand-relative angle, tracks the hand)"
                : _target == 1
                ? "STUMP axe (in block — local)"
                : "ARM pose — " + (_armSel == 0 ? "RIGHT arm" : "LEFT arm") + " ([B] switch arm; rotation only)";
            // SOAKFIX10 — the position line and the euler line are now SEPARATE so neither can overflow the
            // box (the Sponsor's "the 3rd rotation value is cut off the right edge" report). Each is short.
            string posLine, eulerLine;
            if (_target == 0 && _heldRig != null)
            {
                // SOAKFIX9 — WORLD offsetFromHand (sensible ~cm units) + HAND-RELATIVE euler. NOT a localPosition
                // on the 267× bone (that made a 0.02 step = ~5 m — the bug soakfix9 fixed).
                posLine = $"offsetFromHand=({_heldRig.worldOffsetFromHand.x:F4}, {_heldRig.worldOffsetFromHand.y:F4}, {_heldRig.worldOffsetFromHand.z:F4})";
                eulerLine = $"euler=({_heldRig.relEuler.x:F1}, {_heldRig.relEuler.y:F1}, {_heldRig.relEuler.z:F1})";
            }
            else if (_target == 1 && _stump != null)
            {
                posLine = $"localPos=({_stump.localPosition.x:F3}, {_stump.localPosition.y:F3}, {_stump.localPosition.z:F3})";
                eulerLine = $"euler=({Norm(_stump.localEulerAngles.x):F1}, {Norm(_stump.localEulerAngles.y):F1}, {Norm(_stump.localEulerAngles.z):F1})";
            }
            else if (_target == 2 && _armPose != null)
            {
                // RE-SOAK — arms have NO position channel; show the SELECTED arm's euler offset + the other arm.
                Vector3 sel = _armSel == 0 ? _armPose.rightArmEuler : _armPose.leftArmEuler;
                Vector3 oth = _armSel == 0 ? _armPose.leftArmEuler : _armPose.rightArmEuler;
                posLine = $"{(_armSel == 0 ? "RightArmEuler" : "LeftArmEuler")}=({sel.x:F1}, {sel.y:F1}, {sel.z:F1})  (pitch=spread, roll=raise)";
                eulerLine = $"other {(_armSel == 0 ? "LeftArmEuler" : "RightArmEuler")}=({oth.x:F1}, {oth.y:F1}, {oth.z:F1})";
            }
            else { posLine = _target == 2 ? "(arm pose not found)" : "(axe not found)"; eulerLine = ""; }

            float lx = x + 12f, lw = w - 24f;
            // PURPOSE header + a one-line "what this does" so the tool is self-explanatory (was unclear).
            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "AXE NUDGE TOOL  (debug — F9 to close)", _titleStyle);
            GUI.Label(new Rect(lx, y + 30f, lw, 20f),
                "Dial the axe's position/angle in-game, then read the values to bake.", _hintStyle);

            GUI.Label(new Rect(lx, y + 56f, lw, 22f), "Editing: " + tgt, _style);
            // SOAKFIX10 — position + euler on their OWN lines so all three components of EACH are always
            // fully visible inside the (now wider) box, on any screen width. Copyable, never cut off.
            GUI.Label(new Rect(lx, y + 78f, lw, 22f), posLine, _style);
            GUI.Label(new Rect(lx, y + 100f, lw, 22f), eulerLine, _style);

            GUI.Label(new Rect(lx, y + 126f, lw, 20f), "[Tab] held / stump axe / arm pose    [B] right<->left arm (arm only)", _hintStyle);
            GUI.Label(new Rect(lx, y + 146f, lw, 20f), "Move:   ←/→ = X    ↑/↓ = Z    PgUp/PgDn = Y   (axe only)", _hintStyle);
            GUI.Label(new Rect(lx, y + 166f, lw, 20f), "Rotate: T/G = pitch(spread)   Y/H = yaw   U/J = roll(raise)", _hintStyle);
            GUI.Label(new Rect(lx, y + 186f, lw, 20f), "Hold Shift = 5x step    Hold Ctrl = 0.2x step", _hintStyle);
            GUI.Label(new Rect(lx, y + 210f, lw, 20f),
                "Values also print to the log each nudge — copy them to bake the default.", _hintStyle);
        }
    }
}
