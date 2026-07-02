using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED debug NUDGE TOOL for dialing the axe placements IN-GAME (ticket 86ca8ce6y SOAKFIX5 —
    /// the axe-nudge reframe). Instead of the team agonizing over the exact held-axe / stump-axe transforms
    /// headless, the Sponsor finalizes them himself in the shipped build: this tool lets him SELECT a target
    /// (the held axe or the stump axe), NUDGE its position (XYZ) + rotation (pitch/yaw/roll) in small steps,
    /// and READ the live values off the on-screen HUD + the log, then report the numbers to bake into the
    /// constants. 86caa83wn soak #4 — the HELD axe reports a SPLIT pose driven via its HeldAxeRig, dialed +
    /// displayed + baked in the HAND-LOCAL frame END TO END (the seat-doesn't-stick fix): POSITION is a
    /// HAND-LOCAL offset (rotated by the hand each frame so it TRACKS the hand through every facing — sensible
    /// ~cm units, a nudge step is ~2 cm) and ROTATION is a HAND-RELATIVE relEuler (turns with the hand). The
    /// tool nudges the RIG's hand-local fields DIRECTLY and REPORTS them DIRECTLY (NO hand.rotation factor) — so
    /// what the Sponsor dials == what bakes == what the rig applies, with NO facing injected at dial time. That
    /// is the soak-#4 fix: the OLD tool dialed/displayed/baked a WORLD vector and converted it via
    /// Inverse(hand.rotation) at bake, which made the dialed seat FACING-SPECIFIC (it only reproduced at the
    /// facing he dialed it at → wrong after a pickup at a different facing). held ->
    /// HeldAxeLocalOffsetFromHand / HeldAxeRelEuler (both facing-invariant). The STUMP axe is CraftSpot-
    /// local (unscaled, no bone trap): stump -> StumpAxeLocalPos/Euler.
    ///
    /// BUILD-GATED / INERT IN NORMAL PLAY (the hard requirement): the tool does NOTHING until the Sponsor
    /// TOGGLES it on with the debug key (F9). Until then it never reads gameplay input, never moves an axe,
    /// and draws no HUD — so a normal soak is completely unaffected (a soak screenshot/judgement sees the
    /// shipped default pose, not a tool overlay). The component is serialized onto the Boot object editor-
    /// time (like the verify-capture siblings) so it ships, but stays asleep behind the toggle.
    ///
    /// TARGET FRAMES handled correctly:
    ///   - HELD axe: parented to the right-hand bone, but POSE-DRIVEN by HeldAxeRig (86caa83wn hand-local END
    ///     TO END). The tool nudges the RIG's hand-local fields, NOT the transform: the arrow/PageUp keys move
    ///     the offset along the hand's LOCAL axes (~2 cm/click), and ROTATION moves relEuler (hand-relative, so
    ///     the haft keeps turning with the hand WHILE dialed). The rig re-applies position+rotation every frame
    ///     from those fields, so dial == in-motion. It REPORTS the hand-local offset DIRECTLY + the hand-relative
    ///     euler, ready to paste into HeldAxeLocalOffsetFromHand / HeldAxeRelEuler (both facing-invariant). No
    ///     hand.rotation enters the dial/display/bake, so the dialed seat reproduces at EVERY facing + pickup.
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
    /// 4TH-ATTEMPT (86ca8rdkp — the Sponsor STILL sees the castaway elevated WHILE WALKING). A FOURTH nudge
    /// target is added: the GROUND-Y OFFSET. Cycling onto it (Tab) lets the Sponsor dial CastawayCharacter's
    /// groundYOffset IN-GAME with PageUp/PageDown — a constant world-Y added to the snapped feet + shadow, so
    /// he plants the feet EXACTLY on the visible sand (rest AND walk — the snap+offset apply every frame),
    /// reads the value off the panel/log, and reports it to bake into CastawayCharacter.groundYOffset.
    /// Ground-Y has ONE scalar channel (PgUp/PgDn); X/Z + the rotation keys are inert on this target.
    ///
    /// 5TH TARGET (86caa83wn soak #2 — "when i run the axe is no longer in the hand"). The RUN ARM-LOWER. The
    /// Sponsor's chosen approach reversed the earlier axe-side ceiling clamp (which DETACHED the axe from the
    /// hand): the axe now rides the hand RIGIDLY, and the run into-head is fixed by LOWERING the right arm while
    /// running (CastawayArmPose.runLowerEuler), so the gripped axe — which follows the hand — stays below the
    /// head AND in the hand. Cycling onto this target (Tab) lets the Sponsor dial that run-lower offset IN-GAME
    /// while RUNNING: U/J (roll/Z) lowers/raises the run carry (a NEGATIVE Z lowers — the rig's raise axis),
    /// T/G (pitch) + Y/H (yaw) fine-tune. The lower is INERT at walk/idle (run weight 0 — the locked WALK pose
    /// untouched), so he tunes it by RUNNING; the panel surfaces the live RUN WEIGHT (0 walk/idle → 1 full run)
    /// so he knows when to judge. He reads RunLowerEuler off the panel/log and reports it to bake into
    /// CastawayArmPose.runLowerEuler (MovementCameraScene.ArmRunLowerEuler). Arms have no position channel.
    ///
    /// 86cabh907 SOAK ROUND 2 — GENERALIZED to the WEAPON FAMILY (the Sponsor: "nudged values only work for
    /// axe and not for the rest of the weapons"). The HELD target now edits WHICHEVER weapon [B] has selected
    /// (axe/knife/sword/spear): for the AXE it nudges the shared-seat HeldAxeRig (the locked baseline); for
    /// knife/sword/spear it routes the offset+angle into HeldWeaponCycleDebug's per-weapon arrays
    /// (WeaponMeshLocalOffset / WeaponMeshLocalEuler / WeaponMeshScale[index]) so each weapon is positioned +
    /// angled IN-HAND independently, with its own copy-pasteable bake values logged. A 6TH target — AXE HEAD
    /// SIZE — dials the axe BLADE-CLUSTER smaller/bigger relative to the haft (the head still read too big even
    /// after the 0.8x bake, and head proportion isn't a uniform scale) so the Sponsor shrinks the head by eye +
    /// reports the factor to bake into the .blend head re-author later. The arm-switch moved off [B] to [N] so
    /// it never cross-fires with the always-on weapon-cycle [B] (the [B]-binding-conflict fix).
    ///
    /// Pure legacy-Input + IMGUI (the project's input + HUD idiom — ClickToMove/OrbitCamera/BootHud), no
    /// new-Input-System or shader dependency, build-safe.
    /// </summary>
    public class AxeNudgeTool : MonoBehaviour, INudgePanel
    {
        // KEY-SPLIT (combined-#48 fix): the axe tool stays on F9; the WorldLookNudgeTool moved to F10, so the
        // Sponsor's two soak panels never collide and their shared Tab/PageUp/PageDown can never cross-fire
        // (toggling one ON forces the other OFF — see Update()'s mutual-exclusion).
        [Tooltip("Debug toggle key. The tool is INERT until pressed — a normal soak never sees it. " +
                 "F9 (the WorldLookNudgeTool is on F10) so the two soak panels never collide.")]
        public KeyCode toggleKey = KeyCode.F9;
        // CYCLE-KEY REBIND (86cabh907 dial-tool round, Sponsor blocker #3): the target-cycle was [Tab], which
        // is the INVENTORY toggle (InventoryUI.toggleKey = Tab) — pressing Tab to step the nudge target ALSO
        // opened/closed the inventory pack. Moved to [K] (a free key: not WASD/Space/Shift, not 1..9 belt, not
        // the [B] weapon-cycle, not the [N] arm-switch, not the ]/[ ;/' dials, not the F7-F10 toggles, not the
        // arrows/PgUp-Dn/TGYHUJ nudge keys, not the mouse-wheel zoom). The sibling WorldLookNudgeTool's cycle
        // is rebound to [K] too — the two panels are mutually exclusive, so they can share the cycle key.
        [Tooltip("Cycle the nudge target (held weapon -> stump axe -> arm -> GROUND-Y -> RUN -> AXE-HEAD). " +
                 "[K] (was [Tab]; Tab is the inventory toggle, so they no longer conflict).")]
        public KeyCode cycleKey = KeyCode.K;
        // [B]-CONFLICT FIX (86cabh907 soak round 2): the arm-switch was on [B], which ALSO cycles the held
        // weapon (HeldWeaponCycleDebug.cycleKey = B) — pressing [B] on the arm target fired BOTH. Moved to [N]
        // so [B] is solely the weapon-cycle (soak view) and [N] is solely the F9 arm right/left switch.
        [Tooltip("On the ARM-POSE target: switch which arm is dialed (right <-> left). [N] (was [B]; [B] now " +
                 "solely cycles the held weapon so the two never cross-fire).")]
        public KeyCode armSwitchKey = KeyCode.N;
        [Tooltip("Position nudge step (world units). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float posStep = 0.02f;
        [Tooltip("Rotation nudge step (degrees). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float rotStep = 2f;

        // Names of the two serialized axe objects (must match MovementCameraScene.HeroAxeObjectName /
        // StumpAxeObjectName — kept as string literals so Runtime has no Editor-asm dependency).
        private const string HeldAxeName = "HeroAxe";
        private const string StumpAxeName = "StumpAxe";

        private bool _active;
        private int _target;            // 0 = held, 1 = stump, 2 = arm pose, 3 = GROUND-Y offset, 4 = RUN dial, 5 = AXE HEAD size
        private const int TargetCount = 6;
        private int _armSel;            // on the arm target: 0 = right arm, 1 = left arm
        private HeldAxeRig _heldRig;    // SOAKFIX9 — the held axe is pose-driven; the tool nudges the RIG's fields
        // 86cabh907 soak round 2 — the HELD target is GENERALIZED to whatever weapon [B] has selected. For the
        // AXE the tool nudges the shared-seat _heldRig (the locked axe baseline); for knife/sword/spear it
        // routes the nudge into this component's per-weapon offset/euler/scale arrays so each weapon is
        // positioned + angled in-hand independently (the Sponsor's "nudged values only work for axe" report).
        private HeldWeaponCycleDebug _weaponCycle;
        private Transform _stump;
        private CastawayArmPose _armPose; // RE-SOAK — the tool nudges its per-arm LOCAL-euler offsets
        private CastawayCharacter _castaway; // 4th-attempt — the tool nudges its groundYOffset (feet-on-ground knob)
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

        /// <summary>Is this panel currently up? (read by the sibling tool's mutual-exclusion + by tests.)</summary>
        public bool IsActive => _active;

        /// <summary>
        /// Force this panel OFF (called by the sibling WorldLookNudgeTool when ITS panel toggles on, so only
        /// one nudge panel is ever active and their shared cycle/adjust keys can never cross-fire). Idempotent.
        /// </summary>
        public void Deactivate() => _active = false;

        /// <summary>
        /// Turn this panel ON (the toggle path). MUTUAL EXCLUSION (key-split fix): activating THIS panel forces
        /// the sibling world-look panel OFF, so only one nudge panel is ever active — its Tab/PageUp/arrow keys
        /// are the only ones that act and the two tools can never cross-fire even though some keys overlap.
        /// Public so the mutual-exclusion contract is testable without synthesizing the F9 legacy-Input key-down.
        /// </summary>
        public void Activate()
        {
            // Force EVERY sibling world-look panel off (FindObjectsByType, not FindAnyObjectByType — there can
            // be more than one in a scene, and the active one is the one that must be silenced).
            foreach (var world in Object.FindObjectsByType<WorldLookNudgeTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                world.Deactivate();
            foreach (var cam in Object.FindObjectsByType<CameraFollowNudgeTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                cam.Deactivate();
            _active = true;
            Resolve();
            LogCurrent();
        }

        void Awake()
        {
            // No GUILayout.* in this OnGUI (explicit Rects only) — skip IMGUI's Layout event pass (86cahhfp4 C2a).
            useGUILayout = false;
        }

        void Update()
        {
            // The ONLY thing that runs in normal play: watch for the debug toggle. Cheap, no allocs, no
            // gameplay effect. Everything else is gated behind _active.
            if (Input.GetKeyDown(toggleKey))
            {
                if (_active) { Deactivate(); Debug.Log("[AxeNudgeTool] disabled"); }
                else { Activate(); Debug.Log("[AxeNudgeTool] ENABLED — nudge the axes; values on HUD/log"); }
            }
            if (!_active) return;

            if (Input.GetKeyDown(cycleKey))
            {
                _target = (_target + 1) % TargetCount;
                Debug.Log("[AxeNudgeTool] target = " + TargetName());
                LogCurrent();
            }

            // On the ARM target, [N] switches which arm is dialed (right <-> left). [N] (was [B]) so it never
            // cross-fires with the always-on weapon-cycle [B] (86cabh907 soak round 2 [B]-conflict fix).
            if (_target == 2 && Input.GetKeyDown(armSwitchKey))
            {
                _armSel = 1 - _armSel;
                Debug.Log("[AxeNudgeTool] arm = " + (_armSel == 0 ? "RIGHT" : "LEFT"));
                LogCurrent();
            }

            // Bail if the current target isn't resolved (re-resolve on a cycle so a late-spawned axe is found).
            // The RUN target (4) lives on the CastawayArmPose, same as the arm pose (2); the HEAD target (5)
            // needs the weapon-cycle component (which owns the axe head dial).
            bool haveTarget = _target == 0 ? (_heldRig != null || _weaponCycle != null)
                            : _target == 1 ? _stump != null
                            : _target == 2 ? _armPose != null
                            : _target == 3 ? _castaway != null
                            : _target == 4 ? _armPose != null
                            : _weaponCycle != null;
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

            // HEAD-SIZE target (5): the axe head dial has ONE multiplicative channel. PageUp = bigger, PageDown
            // = smaller (±5% via the weapon-cycle's DialAxeHead). Inert unless the held weapon is the axe.
            if (_target == 5)
            {
                bool hb = Input.GetKeyDown(KeyCode.PageUp);
                bool hs = Input.GetKeyDown(KeyCode.PageDown);
                if ((hb || hs) && _weaponCycle != null)
                {
                    if (_weaponCycle.DialAxeHead(hb ? 1.05f : 1f / 1.05f)) changed = true;
                    else Debug.Log("[AxeNudgeTool] HEAD-SIZE: cycle [B] to the AXE first (only the axe has a head)");
                }
                if (changed) LogCurrent();
                return;
            }

            if (dp != Vector3.zero || dr != Vector3.zero)
            {
                if (_target == 0)
                {
                    // 86cabh907 soak round 2 — the HELD target is GENERALIZED to the currently-held weapon. If
                    // the weapon-cycle has a NON-axe weapon selected, route the offset+angle nudge into that
                    // weapon's per-weapon arrays (so knife/sword/spear are positioned + angled independently —
                    // the Sponsor's "nudged values only work for axe" report). If it's the axe (or there is no
                    // weapon-cycle), nudge the shared-seat rig as before (the axe IS the locked baseline).
                    if (_weaponCycle != null && _weaponCycle.CurrentIndex != 0)
                    {
                        _weaponCycle.NudgeCurrentWeapon(dp, dr, 1f);
                        changed = true;
                    }
                    else if (_heldRig != null)
                    {
                        // 86caa83wn soak #4 — the HELD axe is nudged via its RIG, NOT its transform, in the
                        // HAND-LOCAL frame END TO END (the seat-doesn't-stick fix). POSITION moves the rig's
                        // hand-local offset DIRECTLY (no hand.rotation conversion); ROTATION moves the hand-
                        // relative relEuler (the haft keeps turning with the hand WHILE dialed). Dialing in the
                        // hand-local frame means what the Sponsor dials == what bakes == what the rig applies,
                        // with NO hand.rotation injected at dial time — so the seat is FACING-INDEPENDENT (it
                        // reproduces at every facing AND after a pickup, the soak-#4 bug). The previous tool
                        // converted a WORLD-frame nudge via Inverse(hand.rotation), which is exactly what made
                        // the dialed seat facing-specific.
                        _heldRig.worldOffsetFromHand += dp;
                        _heldRig.relEuler += dr;
                    }
                }
                else if (_target == 1)
                {
                    // STUMP axe: CraftSpot-local (unscaled, no bone trap) — nudge its LOCAL transform directly.
                    _stump.localPosition += dp;
                    _stump.localEulerAngles += dr;
                }
                else if (_target == 2)
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
                else if (_target == 3)
                {
                    // GROUND-Y OFFSET (4th-attempt — 'STILL elevated WHILE WALKING'). Nudge CastawayCharacter's
                    // groundYOffset with PageUp/PageDown (dp.y). This is a constant world-Y added to the snapped
                    // feet + shadow, so the Sponsor dials the EXACT feet-on-ground value in-game (rest AND walk
                    // — the snap+offset apply every frame) and reads it off the HUD/log to bake. X/Z + rotation
                    // are inert on this target (one scalar channel).
                    _castaway.groundYOffset += dp.y;
                }
                else if (_armPose != null)
                {
                    // RUN dial (86caa83wn soak #2 — 'when i run the axe is no longer in the hand'). The detaching
                    // axe-side clamp is GONE; the run into-head is now fixed by LOWERING the right arm while
                    // running (CastawayArmPose.runLowerEuler), so the gripped axe (which follows the hand) stays
                    // BELOW the head AND in the hand. This target dials that run-lower offset (rotation only): U/J
                    // = roll/Z lowers/raises the run carry (NEGATIVE Z lowers — the rig's raise axis), T/G =
                    // pitch/X, Y/H = yaw/Y for fine-tuning. The lower is INERT at walk/idle (run weight 0 — the
                    // locked WALK pose untouched), so the Sponsor tunes it by RUNNING (the panel shows the run
                    // weight; judge while running). Position keys are inert (arms have no position channel).
                    _armPose.runLowerEuler += dr;
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
            // 86cabh907 soak round 2 — the weapon-cycle component owns the per-weapon offset/euler/scale + the
            // axe head dial; the generalized HELD target + the HEAD-SIZE target route through it.
            _weaponCycle = held != null ? held.GetComponent<HeldWeaponCycleDebug>()
                                        : Object.FindAnyObjectByType<HeldWeaponCycleDebug>(FindObjectsInactive.Include);
            _stump = FindByName(StumpAxeName);
            _armPose = Object.FindAnyObjectByType<CastawayArmPose>(FindObjectsInactive.Include);
            _castaway = Object.FindAnyObjectByType<CastawayCharacter>(FindObjectsInactive.Include);
            if (held == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName + "' not found");
            else if (_heldRig == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName +
                "' has no HeldAxeRig — cannot nudge its world-offset/relEuler (soakfix9 driver missing)");
            if (_stump == null) Debug.LogWarning("[AxeNudgeTool] stump axe '" + StumpAxeName + "' not found");
            if (_armPose == null) Debug.LogWarning("[AxeNudgeTool] no CastawayArmPose found — cannot nudge the arm pose");
            if (_castaway == null) Debug.LogWarning("[AxeNudgeTool] no CastawayCharacter found — cannot nudge the ground-Y offset");
        }

        private string TargetName() =>
            _target == 0 ? "HELD weapon (" + HeldWeaponLabel() + ")" : _target == 1 ? "STUMP axe"
            : _target == 2 ? "ARM pose (" + (_armSel == 0 ? "RIGHT" : "LEFT") + ")"
            : _target == 3 ? "GROUND-Y offset" : _target == 4 ? "RUN arm-lower"
            : "AXE HEAD size";

        // The currently-held weapon's label (AXE/KNIFE/SWORD/SPEAR) for the generalized HELD target panel.
        private string HeldWeaponLabel() => _weaponCycle != null ? _weaponCycle.CurrentLabel : "AXE";

        private Transform FindByName(string n)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == n) return t;
            return null;
        }

        // Log the values in a copy-pasteable form (the Sponsor reads these off the log to bake into the
        // constants). 86caa83wn soak #4 — the HELD axe reports its RIG's HAND-LOCAL offsetFromHand +
        // HAND-RELATIVE euler DIRECTLY (no hand.rotation factor) — paste into HeldAxeLocalOffsetFromHand /
        // HeldAxeRelEuler (both facing-invariant); the STUMP reports its LOCAL pose (StumpAxeLocalPos/Euler).
        // The held euler is NOT normalised-wrapped — relEuler accumulates as a raw hand-relative euler the rig
        // feeds straight to Quaternion.Euler, so it must round-trip exactly.
        private void LogCurrent()
        {
            if (_target == 0)
            {
                // 86cabh907 soak round 2 — the HELD target is per-weapon. NON-axe weapons report their
                // mesh-holder offset+euler+scale (bake into HeldWeaponCycleDebug.WeaponMeshLocalOffset/
                // WeaponMeshLocalEuler/WeaponMeshScale[index]). The AXE reports the shared-seat rig fields
                // (HeldAxeLocalOffsetFromHand / HeldAxeRelEuler — facing-invariant).
                if (_weaponCycle != null && _weaponCycle.CurrentIndex != 0)
                {
                    int idx = _weaponCycle.CurrentIndex;
                    Vector3 o = _weaponCycle.CurrentOffset, e = _weaponCycle.CurrentEuler;
                    Debug.Log($"[AxeNudgeTool] HELD {_weaponCycle.CurrentLabel}[{idx}]  " +
                              $"WeaponMeshLocalOffset=({o.x:F3}f,{o.y:F3}f,{o.z:F3}f)  " +
                              $"WeaponMeshLocalEuler=({e.x:F1}f,{e.y:F1}f,{e.z:F1}f)  " +
                              $"WeaponMeshScale={_weaponCycle.CurrentScale:F3}f");
                }
                else if (_heldRig != null)
                {
                    // 86caa83wn soak #4 — the AXE seat offset is HAND-LOCAL END TO END (facing-invariant).
                    Vector3 local = _heldRig.worldOffsetFromHand; // field IS the hand-local offset
                    Debug.Log($"[AxeNudgeTool] HELD AXE  HeldAxeLocalOffsetFromHand=({local.x:F4}f,{local.y:F4}f,{local.z:F4}f)  " +
                              $"HeldAxeRelEuler=({_heldRig.relEuler.x:F1}f,{_heldRig.relEuler.y:F1}f,{_heldRig.relEuler.z:F1}f)");
                }
            }
            else if (_target == 5 && _weaponCycle != null)
            {
                // 86cabh907 soak round 2 — the AXE HEAD factor (bake into the .blend head re-author; 1.000 ==
                // current shipped head). Inert unless the axe is held; the panel/log surfaces that.
                Debug.Log($"[AxeNudgeTool] AXE HEAD  factor={_weaponCycle.AxeHeadFactor:F3}f  " +
                          $"(held weapon = {_weaponCycle.CurrentLabel}; head dial applies to the AXE only)");
            }
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
            else if (_target == 3 && _castaway != null)
            {
                // The Sponsor reads this off the log to bake into CastawayCharacter.groundYOffset.
                Debug.Log($"[AxeNudgeTool] GROUND  groundYOffset={_castaway.groundYOffset:F4}f");
            }
            else if (_target == 4 && _armPose != null)
            {
                // 86caa83wn soak #2 — the Sponsor reads this off the log to bake into CastawayArmPose.runLowerEuler.
                // runWeight shows whether the run-lower is engaged THIS frame (rises toward 1 only while RUNNING —
                // when there's something to judge; 0 at walk/idle).
                Vector3 rl = _armPose.runLowerEuler;
                Debug.Log($"[AxeNudgeTool] RUN  RunLowerEuler=({rl.x:F1}f,{rl.y:F1}f,{rl.z:F1}f)  " +
                          $"(runWeight={_armPose.RunWeight:F2})");
            }
        }

        private static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; return a; }

        void OnGUI()
        {
            if (!DebugOverlays.Visible) return; // F1 master gate (86cafd6d6) — F9 is the sub-toggle below it
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
                ? "HELD weapon: " + HeldWeaponLabel() + " (cycle [B]; offset + angle — per weapon)"
                : _target == 1
                ? "STUMP axe (in block — local)"
                : _target == 2
                ? "ARM pose — " + (_armSel == 0 ? "RIGHT arm" : "LEFT arm") + " ([N] switch arm; rotation only)"
                : _target == 3
                ? "GROUND-Y offset (feet-on-ground — PgUp/PgDn; affects rest AND walk)"
                : _target == 4
                ? "RUN arm-lower (axe in hand, calmer run swing — U/J=lower/raise; RUN to judge)"
                : "AXE HEAD size (resize the whole head uniformly — PgUp/PgDn; cycle [B] to the axe)";
            // SOAKFIX10 — the position line and the euler line are now SEPARATE so neither can overflow the
            // box (the Sponsor's "the 3rd rotation value is cut off the right edge" report). Each is short.
            string posLine, eulerLine;
            if (_target == 0)
            {
                // 86cabh907 soak round 2 — per-weapon. NON-axe weapons show their mesh-holder offset+euler
                // (bake into WeaponMeshLocalOffset/Euler[index]); the AXE shows the shared-seat rig fields
                // (hand-local offset + hand-relative euler, facing-invariant).
                if (_weaponCycle != null && _weaponCycle.CurrentIndex != 0)
                {
                    Vector3 o = _weaponCycle.CurrentOffset, e = _weaponCycle.CurrentEuler;
                    posLine = $"offset=({o.x:F3}, {o.y:F3}, {o.z:F3})   scale={_weaponCycle.CurrentScale:F3}";
                    eulerLine = $"euler=({e.x:F1}, {e.y:F1}, {e.z:F1})";
                }
                else if (_heldRig != null)
                {
                    Vector3 local = _heldRig.worldOffsetFromHand; // field IS the hand-local offset (name kept)
                    posLine = $"offsetFromHand=({local.x:F4}, {local.y:F4}, {local.z:F4})";
                    eulerLine = $"euler=({_heldRig.relEuler.x:F1}, {_heldRig.relEuler.y:F1}, {_heldRig.relEuler.z:F1})";
                }
                else { posLine = "(held weapon not found)"; eulerLine = ""; }
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
            else if (_target == 3 && _castaway != null)
            {
                // 4th-attempt — the ground-Y knob has ONE scalar channel; show it big + a hint.
                posLine = $"groundYOffset={_castaway.groundYOffset:F4}   (PgUp/PgDn to dial; + = lift, − = drop)";
                // FLOAT-DIAGNOSTIC (86ca8rdkp instrument): surface the LIVE GAP right here so the Sponsor
                // WATCHES it shrink to ~0 AS he dials groundYOffset — dial + measurement together. feet−ground;
                // ~0 = planted, >1cm = floating. The same number the F8 overlay shows.
                float gap = _castaway.FloatGap;
                eulerLine = float.IsNaN(gap)
                    ? "GAP (feet−ground): N/A  —  no visible ground under the feet"
                    : $"GAP (feet−ground)={gap:F4}  {(Mathf.Abs(gap) > 0.01f ? "◄ FLOATING — keep dialing" : "◄ planted ✓")}";
            }
            else if (_target == 4 && _armPose != null)
            {
                // 86caa83wn soak #2 — the RUN arm-lower. Show the run-lower euler + the live run weight; surface
                // whether it is engaged so the Sponsor knows to RUN to judge (inert at walk/idle — the locked
                // WALK pose untouched). U/J (roll/Z) lowers/raises the run carry; a NEGATIVE Z lowers the arm.
                Vector3 rl = _armPose.runLowerEuler;
                posLine = $"RunLowerEuler=({rl.x:F1}, {rl.y:F1}, {rl.z:F1})  (U/J=roll/Z lowers/raises)";
                eulerLine = _armPose.RunWeight > 0.5f
                    ? $"RUN ENGAGED ✓ weight={_armPose.RunWeight:F2} (judge now; dial Z MORE negative to lower the arm)"
                    : $"run weight={_armPose.RunWeight:F2} — RUN (Shift) to engage + judge; walk/idle untouched";
            }
            else if (_target == 5 && _weaponCycle != null)
            {
                // 86cabh907 soak round 2 — the AXE HEAD-size dial (one multiplicative channel). PgUp/PgDn = ±5%.
                // Inert unless the axe is held; surface that so the Sponsor cycles [B] to the axe first.
                bool axeHeld = _weaponCycle.CurrentIndex == 0;
                posLine = $"axe head factor={_weaponCycle.AxeHeadFactor:F3}   (PgUp bigger / PgDn smaller; 1.000 = shipped)";
                eulerLine = axeHeld
                    ? "resizes the WHOLE head uniformly (shape kept) — read the factor to bake into the .blend head"
                    : $"◄ held weapon is {_weaponCycle.CurrentLabel} — cycle [B] to the AXE to dial its head";
            }
            else { posLine = _target == 2 ? "(arm pose not found)" : _target == 3 ? "(castaway not found)" : _target == 4 ? "(arm pose not found)" : "(weapon-cycle not found)"; eulerLine = ""; }

            float lx = x + 12f, lw = w - 24f;
            // PURPOSE header + a one-line "what this does" so the tool is self-explanatory (was unclear).
            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "WEAPON NUDGE TOOL  (debug — F9 to close)", _titleStyle);
            GUI.Label(new Rect(lx, y + 30f, lw, 20f),
                "Dial each weapon's position/angle (+axe head) in-game, then read the values to bake.", _hintStyle);

            GUI.Label(new Rect(lx, y + 56f, lw, 22f), "Editing: " + tgt, _style);
            // SOAKFIX10 — position + euler on their OWN lines so all three components of EACH are always
            // fully visible inside the (now wider) box, on any screen width. Copyable, never cut off.
            GUI.Label(new Rect(lx, y + 78f, lw, 22f), posLine, _style);
            GUI.Label(new Rect(lx, y + 100f, lw, 22f), eulerLine, _style);

            // 86cabh907 DANISH-KEYBOARD MOUSE CONTROL (primary deliverable): on the AXE-HEAD target, draw a
            // mouse-driven head-size control — a slider (drag to resize LIVE) + [Head -]/[Head +] buttons
            // (click for ±5% steps). The Sponsor cannot use ;/' (Danish punctuation) NOR PgUp/PgDn (laptop
            // Fn-layer), so the MOUSE is the layout-independent, laptop-independent control that cannot fail.
            // It drives HeldWeaponCycleDebug's EXISTING uniform-scale path (SetAxeHeadFactor / DialAxeHead ->
            // ApplyAxeHead — Vector3.one * factor about the junction); stone shape + material unchanged.
            if (_target == 5)
            {
                DrawHeadSizeMouseControl(lx, y + 126f, lw);
                GUI.Label(new Rect(lx, y + 184f, lw, 20f),
                    "Mouse drag / buttons resize the head LIVE (Danish-safe).  Keyboard fallback: [O]/[I] bigger/smaller.", _hintStyle);
                GUI.Label(new Rect(lx, y + 204f, lw, 20f),
                    "[K] cycle target   [B] cycle held weapon (cycle to the AXE to resize its head)", _hintStyle);
                GUI.Label(new Rect(lx, y + 224f, lw, 20f),
                    "Factor also prints to the log — copy it to bake the head default.", _hintStyle);
            }
            else
            {
                GUI.Label(new Rect(lx, y + 126f, lw, 20f), "[K] held weapon / stump / arm / GROUND-Y / RUN / AXE-HEAD    [N] right<->left arm", _hintStyle);
                GUI.Label(new Rect(lx, y + 146f, lw, 20f), "Move:   ←/→ = X    ↑/↓ = Z    PgUp/PgDn = Y (axe-head: MOUSE slider/buttons or [O]/[I])", _hintStyle);
                GUI.Label(new Rect(lx, y + 166f, lw, 20f), "Rotate: T/G = pitch   Y/H = yaw   U/J = roll    [B] cycle held weapon (axe/knife/sword/spear)", _hintStyle);
                GUI.Label(new Rect(lx, y + 186f, lw, 20f), "Hold Shift = 5x step    Hold Ctrl = 0.2x step", _hintStyle);
                GUI.Label(new Rect(lx, y + 210f, lw, 20f),
                    "Values also print to the log each nudge — copy them to bake the default.", _hintStyle);
            }
        }

        // 86cabh907 — the MOUSE-driven axe head-size control (the Danish-keyboard fix). A horizontal slider
        // (drag = resize LIVE) + [Head -] / [Head +] buttons (click = ±5% step) + the live factor label, all
        // pointer-driven so they are layout-independent + laptop-independent (no key registration involved).
        // Every path routes through HeldWeaponCycleDebug's EXISTING uniform-scale code (SetAxeHeadFactor for the
        // slider's absolute value; DialAxeHead for the ±5% buttons) — NO new scale path, NO mesh re-author. The
        // stone shape + material stay exactly the restored 4208067 head; only the SIZE changes. Inert (controls
        // disabled) unless the held weapon is the AXE — only the axe has a head.
        private void DrawHeadSizeMouseControl(float lx, float topY, float lw)
        {
            bool axeHeld = _weaponCycle != null && _weaponCycle.CurrentIndex == 0;
            float factor = _weaponCycle != null ? _weaponCycle.AxeHeadFactor : 1f;

            // Live factor read-out (always shown so the Sponsor sees the current head size).
            GUI.Label(new Rect(lx, topY, lw, 20f),
                "Head size  ×" + factor.ToString("F3") + "   (1.000 = shipped)", _style);

            // The control row is INERT unless the axe is held — grey it out + tell the Sponsor to cycle [B].
            bool prevEnabled = GUI.enabled;
            GUI.enabled = axeHeld && _weaponCycle != null;

            // --- the SLIDER (drag to resize LIVE). Bound to the head factor over the full clamp range; dragging
            //     it calls SetAxeHeadFactor (absolute) -> the same uniform-scale path. ---
            float sliderY = topY + 22f;
            float btnW = 74f, gap = 8f;
            float sliderW = lw - (btnW * 2f) - (gap * 2f);
            float newFactor = GUI.HorizontalSlider(
                new Rect(lx, sliderY + 3f, sliderW, 18f),
                factor, HeldWeaponCycleDebug.HeadFactorMin, HeldWeaponCycleDebug.HeadFactorMax);
            if (axeHeld && _weaponCycle != null && !Mathf.Approximately(newFactor, factor))
            {
                if (_weaponCycle.SetAxeHeadFactor(newFactor)) LogCurrent();
            }

            // --- the two ±5% BUTTONS (click = one step; same DialAxeHead path as PgUp/PgDn / [O]/[I]). ---
            float bx = lx + sliderW + gap;
            if (GUI.Button(new Rect(bx, sliderY, btnW, 24f), "Head −"))
            {
                if (_weaponCycle != null && _weaponCycle.DialAxeHead(1f / 1.05f)) LogCurrent();
            }
            if (GUI.Button(new Rect(bx + btnW + gap, sliderY, btnW, 24f), "Head +"))
            {
                if (_weaponCycle != null && _weaponCycle.DialAxeHead(1.05f)) LogCurrent();
            }

            GUI.enabled = prevEnabled;

            if (!axeHeld)
                GUI.Label(new Rect(lx, sliderY + 26f, lw, 20f),
                    "◄ held weapon is " + (_weaponCycle != null ? _weaponCycle.CurrentLabel : "?") +
                    " — cycle [B] to the AXE to resize its head", _hintStyle);
        }
    }
}
