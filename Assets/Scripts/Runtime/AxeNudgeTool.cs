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
    /// angled IN-HAND independently, with its own copy-pasteable bake values logged. The arm-switch moved off
    /// [B] to [N] so it never cross-fires with the always-on weapon-cycle [B] (the [B]-binding-conflict fix).
    ///
    /// 86cakkfz9 v3 DIAL-IN — the 6TH target (AXE HEAD SIZE) + its mouse slider are REMOVED (absorbs 86cajuuz0):
    /// the axe head is AUTHORED Blender geometry now (wpn_axe_stone_01), so runtime vertex-scaling it distorts
    /// the knapped biface (the rejected "chipping") — head SIZE is a Blender re-author, not a runtime dial.
    /// Overall held-scale is dialed on the HELD target (HeldWeaponCycleDebug's O/I Danish-safe letter keys) +
    /// the settings-console HeldScale row. **TargetCount is 11**: held / stump / arm / GROUND-Y / RUN / FOOT-YAW /
    /// GRIP-CURL / WRIST / HAND / MINE / MINE-SEAT (FOOT-YAW..GRIP-CURL added 86catvb6u for the v4-activation defect
    /// round; WRIST is L/R-switchable ([N]) and drives BOTH hand bones; HAND (round-8) is a per-side THUMB knob so
    /// the Sponsor can orient the thumb independently of the wrist; MINE + MINE-SEAT added 86cay4282 — MINE dials
    /// the left-arm de-grip, MINE-SEAT dials the two-hand haft placement). *(The count in this comment was stale at
    /// "9" while the code already had 10 — it is now derived from the same list the code enumerates; if you add a
    /// target, update BOTH.)* A NOT-ENGAGED signpost (absorbs 86caju055) shows when the debug-overlay layer is up
    /// but F9 is asleep, so the Sponsor doesn't nudge into the void.
    ///
    /// 86cay4282 ROUND 3 — the MINE-SEAT target gains an ALONG-HAFT SLIDE ([R] = hands up toward the HEAD, [V] = down
    /// toward the BUTT) and a THREE-ROW measurement block. The Sponsor, soaking round 2: "how can i dial that the left
    /// hand is not on the bottom of the axe" — and he could not, for two independent reasons this round fixes together.
    /// (1) NO AXIS: sliding the grip along the stick is ONE intent, but the dial was hand-local X/Z (arrows) + Y
    /// (PgUp/PgDn) composed through a ~(-25,70,24) seat rotation, so the one motion he wanted was a three-key blend.
    /// (2) NO NUMBER: the panel drew each hand's PERPENDICULAR distance to the haft and a PASS verdict, while the
    /// ALONG-haft position — TwoHandGripRead.Read.leftU/rightU, computed since round 2 — was drawn nowhere, so a
    /// butt-end grip and a mid-haft grip printed the identical "PASS". That is the same omission as round 1 (hand
    /// separation: also computed, also undrawn), which is why the fix is to render EVERY judgeable field of the read
    /// through pure, length-budgeted, test-callable formatters rather than to add one more ad-hoc line.
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
        // ...and [R]/[V] (round 3, the along-haft slide) join that inventory of taken keys — neither was bound
        // anywhere in the project before, and both are LETTERS (Danish-layout-safe).
        [Tooltip("Cycle the nudge target (held weapon -> stump axe -> arm -> GROUND-Y -> RUN). " +
                 "[K] (was [Tab]; Tab is the inventory toggle, so they no longer conflict).")]
        public KeyCode cycleKey = KeyCode.K;
        // [B]-CONFLICT FIX (86cabh907 soak round 2): the arm-switch was on [B], which ALSO cycles the held
        // weapon (HeldWeaponCycleDebug.cycleKey = B) — pressing [B] on the arm target fired BOTH. Moved to [N]
        // so [B] is solely the weapon-cycle (soak view) and [N] is solely the F9 arm right/left switch.
        [Tooltip("On the ARM-POSE target: switch which arm is dialed (right <-> left). [N] (was [B]; [B] now " +
                 "solely cycles the held weapon so the two never cross-fire).")]
        public KeyCode armSwitchKey = KeyCode.N;
        // 86cay4282 ROUND 3 — the ALONG-HAFT slide keys. The Sponsor, soaking round 2: "how can i dial that the left
        // hand is not on the bottom of the axe". He could not, because sliding the grip along the stick — ONE physical
        // intent — was a blend of arrows (X/Z) and PgUp/PgDn (Y) through a ~(-25,70,24) seat rotation. These two keys
        // are that one intent on one axis (HeldToolRig.TrySlideMineSeatAlongHaft).
        // KEYS: [R] / [V] — a VERTICAL pair in one physical column (R above F above V), matching the T/G, Y/H, U/J
        // idiom, and LETTERS, so they are Danish-layout-safe (unity-conventions.md §Input System: never bind a
        // soak-facing control to punctuation; the alpha block is the same physical position on Danish vs US). Both
        // were unused anywhere in the project before this change.
        [Tooltip("On the MINE-SEAT target: slide the grip UP the haft (hands toward the HEAD — choking up). [R]")]
        public KeyCode haftUpKey = KeyCode.R;
        [Tooltip("On the MINE-SEAT target: slide the grip DOWN the haft (hands toward the BUTT). [V]")]
        public KeyCode haftDownKey = KeyCode.V;

        [Tooltip("Position nudge step (world units). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float posStep = 0.02f;
        [Tooltip("Rotation nudge step (degrees). Hold Shift for 5x; Ctrl for 0.2x.")]
        public float rotStep = 2f;

        // Names of the two serialized axe objects (must match MovementCameraScene.HeroAxeObjectName /
        // StumpAxeObjectName — kept as string literals so Runtime has no Editor-asm dependency).
        private const string HeldAxeName = "HeroAxe";
        private const string StumpAxeName = "StumpAxe";

        private bool _active;
        private int _target;            // 0=held,1=stump,2=arm,3=GROUND-Y,4=RUN,5=FOOT-YAW,6=GRIP-CURL,7=WRIST(L/R),8=HAND(thumb,L/R),9=MINE de-grip,10=MINE SEAT
        // 86cakkfz9: the old AXE-HEAD-size target + its mouse slider are REMOVED — head SIZE is authored Blender
        // geometry now, not a runtime dial. 86catvb6u: FOOT-YAW (CastawayFootYaw.footYawDeg, the v4 pigeon-toe
        // counter-rotate; Y/H) + GRIP-CURL (CastawayFingerCurl.fingerCurlDeg, softens the chunky-hand grip fold;
        // T/G) added. Round-8: WRIST now dials CastawayHandPose's per-side WRIST euler (both hand bones, [N]-switch
        // L/R) + HAND dials its per-side THUMB euler (orient the thumb below the wrist) — T/G/Y/H/U/J = all 3 axes.
        // 86cay4282: 9 -> 10, adding 9=MINE (CastawayArmPose.mineDeGripEuler — the left-arm de-grip; RETAINED as a
        // live A/B knob but now SHIPPING ZERO after the Sponsor's direction reversal). 86cay4282 round 2: 10 -> 11,
        // adding 10=MINE SEAT (HeldToolRig.mineSeatOffsetDelta / mineSeatEulerDelta — the state-gated two-hand haft
        // placement, the round-2 fix). MINE SEAT is the ONLY target with BOTH a position and a rotation channel on
        // an engagement-weighted value, so its panel draws the live grip measurement + a PASS/FAIL line.
        private const int TargetCount = 11;
        private int _armSel;            // shared L/R side selector for the ARM(2)/WRIST(7)/HAND(8) targets: 0=right, 1=left
        private HeldAxeRig _heldRig;    // SOAKFIX9 — the held axe is pose-driven; the tool nudges the RIG's fields
        private CastawayFootYaw _footYaw;      // 86catvb6u — FOOT-YAW target dials its footYawDeg (v4 pigeon-toe fix)
        private CastawayFingerCurl _fingerCurl; // 86catvb6u — GRIP-CURL target dials its fingerCurlDeg (v4 grip fold)
        private CastawayHandPose _hand;  // 86catvb6u round-8 — WRIST + HAND(thumb) targets dial its per-side eulers
        // 86cabh907 soak round 2 — the HELD target is GENERALIZED to whatever weapon [B] has selected. For the
        // AXE the tool nudges the shared-seat _heldRig (the locked axe baseline); for knife/sword/spear it
        // routes the nudge into this component's per-weapon offset/euler/scale arrays so each weapon is
        // positioned + angled in-hand independently (the Sponsor's "nudged values only work for axe" report).
        private HeldWeaponCycleDebug _weaponCycle;
        private Transform _stump;
        private CastawayArmPose _armPose; // RE-SOAK — the tool nudges its per-arm LOCAL-euler offsets
        private CastawayCharacter _castaway; // 4th-attempt — the tool nudges its groundYOffset (feet-on-ground knob)
        // 86cay4282 round 2 — the arm/hand bones the TWO-HAND GRIP read is measured from. Resolved on Resolve()
        // alongside every other target so the MINE + MINE-SEAT panels can DRAW the live geometry: round 1 shipped a
        // panel that printed only the engagement weight while the number the fix is defined by existed solely inside
        // the shipped-build gate — so the Sponsor was sent looking for a value the panel never printed.
        private Transform _lArmBone, _rArmBone, _lHandBone, _rHandBone;
        private GUIStyle _style, _hintStyle, _titleStyle, _measStyle;

        // Panel size (SOAKFIX6 — carries a purpose header + a "what this does" line + the controls).
        // SOAKFIX10 — the offsetFromHand + euler values now live on their OWN lines (no single packed
        // value line that overflows the box), so the panel is WIDER (fits the longest value/hint line with
        // margin) and TALLER (one extra value row). The width still leaves the right-anchored box fully on
        // any screen ≥ the narrowest test size (800px: 532 + 0 margin < 800 → x ≥ 0; PanelRect also clamps).
        // 86cay4282 round 3: 532 -> 616. The measurement block grew from one line to THREE (distance-to-haft,
        // ALONG-haft position, and the separation/angle context row), and at the 14px bold value style the existing
        // one-line grip verdict already needed ~680px of the 508px inner width — i.e. it was being CLIPPED on the
        // Sponsor's own screen, which is a fresh instance of the very failure class this ticket keeps paying for
        // (a number that exists but is not legible is a number he was never shown). The measurement rows also drop to
        // their own 12px style. Per-line char budgets are regression-guarded in AxeNudgeToolPlayModeTests.
        public const float PanelWidth = 616f;
        // 86cay4282 round 2: 236 -> 262 for a THIRD value row. The MINE + MINE-SEAT targets draw a live
        // measurement + PASS/FAIL line under their dial values, so the box needs one more row of height; every
        // other target leaves that row blank. PanelRect keeps the box on-screen + off the hotbar at the new height
        // (guarded by AxeNudgeToolPlayModeTests, which derives from these consts rather than hard-coding them).
        // Round 3: 262 -> 328 for the two ADDITIONAL measurement rows (along-haft position + the context row) PLUS a
        // second line of room for the "Editing:" header, whose longest target labels word-wrapped onto the value row
        // below (seen in this round's own shipped panel capture).
        public const float PanelHeight = 328f;
        // The point size the measurement rows are drawn at, and the inner text width available to them — exposed so
        // the per-line width budget is asserted against the SAME numbers OnGUI uses rather than a copy.
        public const float MeasFontSize = 12f;
        public const float LabelInset = 24f;   // OnGUI draws every label at lx = x + 12 with lw = w - 24
        // The hint block's geometry, as constants OnGUI itself uses — so "nothing is drawn outside the box" is a
        // testable contract rather than five hand-kept offsets that silently outgrow PanelHeight the next time a row is
        // added. Round 3 added two measurement rows and a second header line; without this guard the fifth hint row
        // would have spilled below the panel with nothing to catch it.
        public const float FirstHintY = 218f;
        public const float HintRowStep = 20f;
        public const int HintRowCount = 5;
        /// <summary>The MINE-SEAT target's "Editing:" header. A const so its length is assertable: several targets'
        /// headers are longer than the box is wide and IMGUI word-wraps them, which is why the header now gets two
        /// lines of room — this one is kept inside ONE line.</summary>
        // ("MINE to judge" is not repeated here — the euler row already carries the live ENGAGED / not-engaged state,
        // which is the actionable form of it.)
        public const string MineSeatHeader = "MINE SEAT — two-hand haft ([R]/[V] slides it)";

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
        public void Deactivate()
        {
            _active = false;
            // 86catvb6u — release the GRIP-CURL force so the curl reverts to its normal gate (belt-selection) in
            // play; else leaving the F9 tool would strand the right hand curled with no weapon held.
            if (_fingerCurl != null) _fingerCurl.alwaysCurl = false;
        }

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

            // 86catvb6u — FORCE the finger-curl to APPLY while the GRIP-CURL target is selected, so the Sponsor
            // SEES the curl at the dialed angle even with an unequipped/[B]-displayed weapon (the belt-selection
            // gate would otherwise leave it inert — the "no effect at 390°" report). Cleared off-target here +
            // in Deactivate, so normal play keeps the belt-gated curl.
            if (_fingerCurl != null) _fingerCurl.alwaysCurl = (_target == 6);

            // On the ARM(2) / WRIST(7) / HAND(8) targets, [N] switches which SIDE is dialed (right <-> left). [N]
            // (was [B]) so it never cross-fires with the always-on weapon-cycle [B] (86cabh907 soak round 2 fix).
            if ((_target == 2 || _target == 7 || _target == 8) && Input.GetKeyDown(armSwitchKey))
            {
                _armSel = 1 - _armSel;
                Debug.Log("[AxeNudgeTool] side = " + (_armSel == 0 ? "RIGHT" : "LEFT"));
                LogCurrent();
            }

            // Bail if the current target isn't resolved (re-resolve on a cycle so a late-spawned axe is found).
            // The RUN target (4) lives on the CastawayArmPose, same as the arm pose (2).
            bool haveTarget = _target == 0 ? (_heldRig != null || _weaponCycle != null)
                            : _target == 1 ? _stump != null
                            : _target == 2 ? _armPose != null
                            : _target == 3 ? _castaway != null
                            : _target == 4 ? _armPose != null        // RUN arm-lower
                            : _target == 5 ? _footYaw != null        // FOOT-YAW (v4 pigeon-toe counter-rotate)
                            : _target == 6 ? _fingerCurl != null     // GRIP-CURL (v4 grip fold)
                            : _target == 7 ? _hand != null           // WRIST (both hand bones, L/R)
                            : _target == 8 ? _hand != null           // HAND (thumb, L/R)
                            : _target == 9 ? _armPose != null        // MINE de-grip (86cay4282) — on the arm pose
                            : _heldRig != null;                      // MINE SEAT (86cay4282 r2) — on the held rig
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

            // 86cay4282 ROUND 3 — the ALONG-HAFT slide, MINE-SEAT target only. Handled BEFORE the generic
            // position/rotation block because it is neither: it is one physical degree of freedom (slide the grip up or
            // down the stick) that the rig resolves onto the haft's OWN axis, then writes into the hand-local position
            // delta. Doing it here rather than folding it into `dp` keeps the axis resolution — and its failure mode —
            // in one place.
            if (_target == MineSeatTargetIndex && _heldRig != null)
            {
                float slide = 0f;
                if (Input.GetKeyDown(haftUpKey)) slide += ps;
                if (Input.GetKeyDown(haftDownKey)) slide -= ps;
                if (slide != 0f)
                {
                    // A refused slide is REPORTED, never silent: an unresolvable haft axis is exactly the case where a
                    // guessed axis would move the tool somewhere plausible-looking and wrong (the bakeAxisConversion
                    // trap), and a dial that quietly does nothing is the trap that burned the Sponsor twice already.
                    if (ApplyHaftSlide(slide)) { changed = true; }
                    else Debug.LogWarning("[AxeNudgeTool] MINE SEAT along-haft slide REFUSED — the held tool's haft " +
                                          "axis could not be resolved (no displayed mesh?), so nothing moved. Select " +
                                          "the pickaxe ([B] / the belt) and try again; the tool will NOT guess an axis.");
                }
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
                        // POSITION: additive offset (unchanged). ROTATION: compose dr in the weapon's LOCAL frame
                        // via QUATERNIONS (soak-5 round-5 GIMBAL fix). Euler-COMPONENT addition (the old `_liveEuler
                        // += dr`) is DEGENERATE near ±90° pitch — the Sponsor's pickaxe -362° yaw HUNT: at pitch
                        // ~-70..-80 the yaw component stopped mapping to the rotation he wanted, so orientations were
                        // UNREACHABLE. ComposeLocalRot right-multiplies the delta about the weapon's own axes, making
                        // EVERY orientation reachable. The seat APPLICATION is unchanged (Quaternion.Euler(stored)),
                        // and Quaternion.Euler(result) == the composed rotation, so committed/baked eulers are exact.
                        Vector3 targetEuler = ComposeLocalRot(_weaponCycle.CurrentEuler, dr);
                        _weaponCycle.NudgeCurrentWeapon(dp, targetEuler - _weaponCycle.CurrentEuler, 1f);
                        changed = true;
                    }
                    else if (_heldRig != null)
                    {
                        // 86caa83wn soak #4 — the HELD axe is nudged via its RIG, NOT its transform, in the
                        // HAND-LOCAL frame END TO END (the seat-doesn't-stick fix). POSITION moves the rig's
                        // hand-local offset DIRECTLY (no hand.rotation conversion); ROTATION composes the hand-
                        // relative relEuler in the weapon's LOCAL frame (soak-5 gimbal fix, same as the non-axe
                        // path above — the axe's low pitch never hit gimbal, but keep both seat paths consistent so
                        // a future high-pitch axe dial is reachable too). Dialing in the hand-local frame means
                        // what the Sponsor dials == what bakes == what the rig applies, with NO hand.rotation
                        // injected at dial time — so the seat is FACING-INDEPENDENT (it reproduces at every facing
                        // AND after a pickup, the soak-#4 bug).
                        _heldRig.worldOffsetFromHand += dp;
                        _heldRig.relEuler = ComposeLocalRot(_heldRig.relEuler, dr);
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
                else if (_target == 4)
                {
                    // RUN dial (86caa83wn soak #2 — 'when i run the axe is no longer in the hand'). The detaching
                    // axe-side clamp is GONE; the run into-head is now fixed by LOWERING the right arm while
                    // running (CastawayArmPose.runLowerEuler), so the gripped axe (which follows the hand) stays
                    // BELOW the head AND in the hand. This target dials that run-lower offset (rotation only): U/J
                    // = roll/Z lowers/raises the run carry (NEGATIVE Z lowers — the rig's raise axis), T/G =
                    // pitch/X, Y/H = yaw/Y for fine-tuning. The lower is INERT at walk/idle (run weight 0 — the
                    // locked WALK pose untouched), so the Sponsor tunes it by RUNNING (the panel shows the run
                    // weight; judge while running). Position keys are inert (arms have no position channel).
                    if (_armPose != null) _armPose.runLowerEuler += dr;
                }
                else if (_target == 9)
                {
                    // MINE de-grip (86cay4282 — "he is swinging like he is handing the axe with both hands"). Dials
                    // the LEFT-upper-arm offset that opens the arm off the mine clip's phantom haft. T/G = pitch/X
                    // is the LOAD-BEARING axis here (MEASURED: on the left arm a NEGATIVE X separates the hands AND
                    // increases torso clearance; the +X the cheat-sheet calls "outward" pulls them TOGETHER on this
                    // clip). Rotation only — arms have no position channel. INERT except during the pickaxe swing
                    // (weight 0 elsewhere), so MINE A BOULDER to judge; the panel shows the live weight.
                    if (_armPose != null) _armPose.mineDeGripEuler += dr;
                }
                else if (_target == 10)
                {
                    // MINE SEAT (86cay4282 round 2 — the Sponsor: "we need to position the axe for a two hand grip").
                    // The ONLY dual-channel target: arrows/PgUp/PgDn slide the haft (hand-local position delta) and
                    // T/G/Y/H/U/J turn it (tool-local rotation delta) so its LINE runs through both hands. Rotation
                    // composes via ComposeLocalRot — the shipped delta pitches ~56/89/56 deg, so per-component euler
                    // accumulation would hit the documented gimbal dead zone (unity6-mastery.md §5) and some
                    // orientations would be UNREACHABLE. INERT except during the pickaxe swing (weight 0 elsewhere),
                    // so MINE A BOULDER to judge; the panel draws the live weight AND the hand-to-haft distances.
                    if (_heldRig != null)
                    {
                        _heldRig.mineSeatOffsetDelta += dp;
                        _heldRig.mineSeatEulerDelta = ComposeLocalRot(_heldRig.mineSeatEulerDelta, dr);
                    }
                }
                else if (_target == 5)
                {
                    // FOOT-YAW (86catvb6u — the v4 pigeon-toe counter-rotate). Y/H dials the per-foot OUTWARD yaw
                    // (mirrored L/R) until the toes read straight; the rest of the keys are inert (one scalar
                    // channel). Bake the dialed value into MovementCameraScene.CastawayV4FootYawDeg.
                    if (_footYaw != null) _footYaw.footYawDeg += dr.y;
                }
                else if (_target == 6)
                {
                    // GRIP-CURL (86catvb6u — soften v4's chunky-hand grip that reads dark/segmented when gripping).
                    // T/G dials the right-hand finger-curl degrees, CLAMPED to [0,90] (the unclamped write ran to
                    // 390° = a wrapped ~30° that looked like "no effect" — the Sponsor's report). RebuildCached so
                    // it shows this frame (with the force above, on any weapon). Bake into CastawayFingerCurl.fingerCurlDeg.
                    if (_fingerCurl != null)
                    {
                        _fingerCurl.fingerCurlDeg = Mathf.Clamp(_fingerCurl.fingerCurlDeg + dr.x, 0f, 90f);
                        _fingerCurl.RebuildCached();
                    }
                }
                else if (_target == 7)
                {
                    // WRIST (86catvb6u round-8 — the both-hand un-twist). All 3 axes (T/G=X, Y/H=Y, U/J=Z) added to
                    // the SELECTED hand bone's offset ([N] switches R/L; rotation only, position keys inert). Bake
                    // into MovementCameraScene.CastawayV4RightWristEuler / CastawayV4LeftWristEuler.
                    if (_hand != null)
                    {
                        if (_armSel == 0) _hand.rightWristEuler += dr;
                        else _hand.leftWristEuler += dr;
                    }
                }
                else if (_target == 8)
                {
                    // HAND (86catvb6u round-8 — the thumb-segment knob: orient the thumb BELOW the wrist, without
                    // twisting the wrist/arm — the Sponsor's residual). All 3 axes on the SELECTED thumb base bone
                    // ([N] switches R/L). Bake into MovementCameraScene.CastawayV4RightThumbEuler / ...LeftThumbEuler.
                    if (_hand != null)
                    {
                        if (_armSel == 0) _hand.rightThumbEuler += dr;
                        else _hand.leftThumbEuler += dr;
                    }
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
            // 86cabh907 soak round 2 — the weapon-cycle component owns the per-weapon offset/euler/scale; the
            // generalized HELD target routes through it. (86cakkfz9: the axe head-size dial + its HEAD-SIZE
            // target are removed — head SIZE is authored Blender geometry now, not a runtime dial.)
            _weaponCycle = held != null ? held.GetComponent<HeldWeaponCycleDebug>()
                                        : Object.FindAnyObjectByType<HeldWeaponCycleDebug>(FindObjectsInactive.Include);
            _stump = FindByName(StumpAxeName);
            _armPose = Object.FindAnyObjectByType<CastawayArmPose>(FindObjectsInactive.Include);
            _castaway = Object.FindAnyObjectByType<CastawayCharacter>(FindObjectsInactive.Include);
            // 86catvb6u — the FOOT-YAW + GRIP-CURL + WRIST targets (the v4-activation defect fixes).
            _footYaw = Object.FindAnyObjectByType<CastawayFootYaw>(FindObjectsInactive.Include);
            _fingerCurl = Object.FindAnyObjectByType<CastawayFingerCurl>(FindObjectsInactive.Include);
            _hand = Object.FindAnyObjectByType<CastawayHandPose>(FindObjectsInactive.Include);
            // 86cay4282 round 2 — the four bones the two-hand-grip read needs. Resolved by exact Mixamo name off the
            // live model (the same names AttackClipPoseDiag + SwingVerifyCapture use), so the panel measures the
            // SAME geometry the shipped gate scores.
            ResolveGripBones();
            if (held == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName + "' not found");
            else if (_heldRig == null) Debug.LogWarning("[AxeNudgeTool] held axe '" + HeldAxeName +
                "' has no HeldAxeRig — cannot nudge its world-offset/relEuler (soakfix9 driver missing)");
            if (_stump == null) Debug.LogWarning("[AxeNudgeTool] stump axe '" + StumpAxeName + "' not found");
            if (_armPose == null) Debug.LogWarning("[AxeNudgeTool] no CastawayArmPose found — cannot nudge the arm pose");
            if (_castaway == null) Debug.LogWarning("[AxeNudgeTool] no CastawayCharacter found — cannot nudge the ground-Y offset");
        }

        private string TargetName() =>
            _target == 0 ? "HELD weapon (" + HeldWeaponLabel() + ")" : _target == 1 ? "STUMP axe"
            : _target == 2 ? "ARM pose (" + Side() + ")"
            : _target == 3 ? "GROUND-Y offset" : _target == 4 ? "RUN arm-lower"
            : _target == 5 ? "FOOT-YAW (v4 pigeon-toe)" : _target == 6 ? "GRIP-CURL (v4 hand)"
            : _target == 7 ? "WRIST (v4 hand un-twist, " + Side() + ")"
            : _target == 8 ? "HAND (v4 thumb, " + Side() + ")"
            : _target == 9 ? "MINE de-grip (left arm, ships ZERO — A/B knob)"
            : "MINE SEAT (two-hand haft placement)";

        // 86cay4282 round 2 — resolve the four bones the two-hand grip read is measured from, off the live model.
        private void ResolveGripBones()
        {
            _lArmBone = _rArmBone = _lHandBone = _rHandBone = null;
            Transform root = _castaway != null ? _castaway.ModelTransform : null;
            if (root == null && _castaway != null) root = _castaway.transform;
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "mixamorig:LeftArm") _lArmBone = t;
                else if (t.name == "mixamorig:RightArm") _rArmBone = t;
                else if (t.name == "mixamorig:LeftHand") _lHandBone = t;
                else if (t.name == "mixamorig:RightHand") _rHandBone = t;
            }
        }

        /// <summary>
        /// The LIVE two-hand grip geometry this frame, or valid=false when it cannot be measured. Round 1's MINE
        /// panel printed only the engagement weight, so the number the whole fix is DEFINED by (each hand's distance
        /// to the haft line) existed only inside the shipped-build gate's log — the Sponsor was pointed at a value
        /// the panel never drew. This is that gap closed: same <see cref="TwoHandGripRead"/> maths, same thresholds,
        /// same numbers the gate asserts.
        /// </summary>
        private bool TryGripRead(out TwoHandGripRead.Read read)
        {
            read = default;
            if (_lArmBone == null || _rArmBone == null || _lHandBone == null || _rHandBone == null) return false;
            if (_heldRig == null || !_heldRig.TryGetHaftSegment(out Vector3 grip, out Vector3 head)) return false;
            read = TwoHandGripRead.Measure(_lArmBone.position, _rArmBone.position,
                                           _lHandBone.position, _rHandBone.position, grip, head);
            return read.valid;
        }

        /// <summary>
        /// THE THREE MEASUREMENT ROWS exactly as the panel draws them, for a given engagement weight — one seam used by
        /// OnGUI, by the bake log, AND by the shipped-build gate's panel pass, so those three can never show different
        /// subsets of the same read. (Round 2's along-haft numbers went undrawn precisely because the panel assembled
        /// its own lines inline; a single seam makes "computed but undrawn" a test-visible property — see
        /// MineSeatAlongHaftTests.EveryJudgeableFieldOfTheGripRead_IsRenderedBySomePanelRow.)
        ///
        /// Row 0 = distance to the haft + PASS/FAIL, row 1 = ALONG-haft position, row 2 = separation/angle context.
        /// An unmeasurable rig yields the unavailable notice in row 0 and empty rows after it — never a plausible zero.
        /// </summary>
        public string[] GripReadoutRows(float weight)
        {
            if (!TryGripRead(out TwoHandGripRead.Read r))
                return new[] { GripUnavailableLine, "", "" };
            return new[] { GripDistanceLine(r, weight), AlongHaftLine(r), GripContextLine(r) };
        }

        /// <summary>The PASS/FAIL line for the MINE panels — the explicit threshold read, never just a raw value
        /// (a bare number leaves the Sponsor guessing what "good" is; the round-1 panel's omission of it is exactly
        /// what sent him hunting).</summary>
        private string GripVerdictLine(float weight) => GripReadoutRows(weight)[0];

        /// <summary>The MINE-SEAT target's index in the [K] cycle. Named rather than left as a literal 10 because a
        /// dispatch brief that says "press K ten times" breaks silently the moment a target is added or reordered
        /// (unity-conventions.md §Input System — instruct a cycler by its on-screen LABEL, never by press-count).</summary>
        public const int MineSeatTargetIndex = 10;

        /// <summary>
        /// VERIFY/CAPTURE-ONLY: select a nudge target directly, so the shipped-build gate can photograph the MINE-SEAT
        /// panel without synthesizing eleven [K] key-downs (legacy Input cannot be driven from inside a player).
        /// Resolves the target's components, exactly as the [K] handler does, so the captured panel is the real one.
        /// </summary>
        public void SelectTargetForVerify(int target)
        {
            _target = ((target % TargetCount) + TargetCount) % TargetCount;
            Resolve();
        }

        /// <summary>
        /// THE ALONG-HAFT SLIDE, as one seam. <see cref="Update"/>'s [R]/[V] handler calls THIS, and so does the
        /// shipped-build gate's panel pass — so the gate proves the real mechanism (axis resolution, sign, and that the
        /// live along-haft read actually moves) rather than a re-implementation beside it. The only link a headless or
        /// automated pass structurally cannot close is legacy Input's key-down itself; the key CONSTANTS and the panel's
        /// hint text are pinned by MineSeatAlongHaftTests instead, and a human keypress closes it at the soak.
        ///
        /// Returns false when the haft axis cannot be resolved — the caller must SAY SO rather than move nothing quietly.
        /// </summary>
        public bool ApplyHaftSlide(float metres)
        {
            if (_heldRig == null) return false;
            return _heldRig.TrySlideMineSeatAlongHaft(metres);
        }

        // ==============================================================================================================
        // 86cay4282 ROUND 3 — THE MEASUREMENT ROWS, as PURE formatters.
        //
        // WHY THEY ARE PURE + PUBLIC. This ticket has now shipped the SAME defect twice: a quantity that the code had
        // already computed, that the whole judgement rests on, and that the panel never drew — round 1 it was hand
        // SEPARATION, round 2 it was the ALONG-HAFT position (`Read.leftU`/`rightU`, computed since round 2 and drawn
        // nowhere, so a butt-end grip and a mid-haft grip printed identical PASS lines). The Sponsor was twice asked to
        // judge something the build never showed him. Extracting the rows as pure functions closes the class rather than
        // the instance: every field of TwoHandGripRead.Read that a human would judge on is now rendered by a function a
        // test can call, so "computed but undrawn" is a test failure instead of a soak failure.
        //
        // They are also LENGTH-BUDGETED (AxeNudgeToolPlayModeTests): an IMGUI label longer than its Rect is CLIPPED, so
        // a line that overflows is another way to compute a number and not show it — which is what was happening to the
        // one-line round-2 verdict at 532px.
        // ==============================================================================================================

        /// <summary>Shown in place of every measurement row when the rig/mesh cannot be measured. "We do not know" must
        /// never render as "the grip is fine" — the metric-green-on-nonsense guard.</summary>
        public const string GripUnavailableLine =
            "grip read UNAVAILABLE — arm/hand bones or mesh unresolved (NOT a pass)";

        /// <summary>Row 1 — each hand's PERPENDICULAR distance to the haft line, against its shipped cap. "Is the one
        /// stick running through both hands?"</summary>
        public static string GripDistanceLine(in TwoHandGripRead.Read r, float weight)
        {
            if (!r.valid) return GripUnavailableLine;
            return $"L->haft {r.leftHaftSW:F3} / R->haft {r.rightHaftSW:F3} SW  (caps " +
                   $"{TwoHandGripRead.LeftHaftPassSW:F2}/{TwoHandGripRead.RightHaftPassSW:F2})  " +
                   (TwoHandGripRead.Pass(r) ? "PASS ✓" : "FAIL ✗") + $"  w={weight:F2}";
        }

        /// <summary>Row 2 — WHERE ALONG THE HAFT each hand sits. THE row this round exists for: `Pass()` scores only
        /// the perpendicular distance, so a hand clamped at the butt end and a hand at mid-haft are indistinguishable
        /// to it — which is precisely how round 2 shipped a panel reading PASS while the left hand was at the butt.
        /// The 0 = BUTT / 1 = HEAD legend is IN the line so the number needs no source lookup, and an off-the-end hand
        /// is flagged loudly rather than left as a quiet negative.</summary>
        public static string AlongHaftLine(in TwoHandGripRead.Read r)
        {
            if (!r.valid) return GripUnavailableLine;
            return $"ALONG haft 0=BUTT 1=HEAD:  L {AlongTag(r.leftU, true)}  R {AlongTag(r.rightU, false)}";
        }

        /// <summary>One hand's along-haft position. UNCLAMPED by design (TwoHandGripRead keeps it so), because the
        /// whole point is that a hand which has slid off an END is VISIBLE. For the LEFT hand the in-range form also
        /// states how much haft remains BELOW it — the Sponsor's actual judgement quantity ("not on the bottom of the
        /// axe"), so he never has to convert a fraction in his head.</summary>
        private static string AlongTag(float u, bool isLeft)
        {
            if (u < 0f) return $"{u:F2} !!OFF-BUTT";
            if (u > 1f) return $"{u:F2} !!OFF-HEAD";
            if (isLeft) return $"{u:F2} = {u * 100f:F0}% below it";
            return $"{u:F2}";
        }

        /// <summary>Row 3 — the remaining computed-but-previously-undrawn fields of the read: hand SEPARATION (round
        /// 1's metric — no longer a pass criterion after the Sponsor's reversal, but it is what EXPLAINS the residual,
        /// since a wide pair eats the haft) and the tool's ANGLE off the line through both hands (the dominant percept
        /// pre-fix at 90 deg). Shoulder width is printed as the normaliser so every SW figure above is convertible to
        /// something a human can picture.</summary>
        public static string GripContextLine(in TwoHandGripRead.Read r)
        {
            if (!r.valid) return GripUnavailableLine;
            return $"hands {r.handSepSW:F2} SW apart | tool {r.toolVsHandLineDeg:F1}deg off the hand line | " +
                   $"1 SW = {r.shoulderWidth:F3}m";
        }

        // Shared L/R label for the ARM/WRIST/HAND targets ([N] toggles _armSel).
        private string Side() => _armSel == 0 ? "RIGHT" : "LEFT";

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
            else if (_target == 9 && _armPose != null)
            {
                // 86cay4282 — the Sponsor reads this off the log to bake into MovementCameraScene.ArmMineDeGripEuler.
                // mineWeight shows whether the de-grip is engaged THIS frame (rises toward 1 only while the
                // AttackPickaxe swing owns layer 0 — i.e. only while there IS something to judge). The grip verdict
                // rides along so the log carries the SAME measured numbers the panel draws.
                Vector3 mg = _armPose.mineDeGripEuler;
                Debug.Log($"[AxeNudgeTool] MINE  ArmMineDeGripEuler=({mg.x:F1}f,{mg.y:F1}f,{mg.z:F1}f)  " +
                          $"(mineWeight={_armPose.MineDeGripWeight:F2})  " +
                          GripVerdictLine(_armPose.MineDeGripWeight) + "  " + GripLogSuffix());
            }
            else if (_target == 10 && _heldRig != null)
            {
                // 86cay4282 round 2 — the Sponsor reads these off the log to bake into
                // MovementCameraScene.HeldToolMineSeatOffsetDelta / HeldToolMineSeatEulerDelta.
                Vector3 o = _heldRig.mineSeatOffsetDelta, e = _heldRig.mineSeatEulerDelta;
                Debug.Log($"[AxeNudgeTool] MINE SEAT  HeldToolMineSeatOffsetDelta=({o.x:F4}f,{o.y:F4}f,{o.z:F4}f)  " +
                          $"HeldToolMineSeatEulerDelta=({e.x:F1}f,{e.y:F1}f,{e.z:F1}f)  " +
                          GripVerdictLine(_heldRig.MineSeatWeight) + "  " + GripLogSuffix());
            }
            else if (_target == 5 && _footYaw != null)
                // 86catvb6u — bake into MovementCameraScene.CastawayV4FootYawDeg (the v4 pigeon-toe counter-rotate).
                Debug.Log($"[AxeNudgeTool] FOOT-YAW  CastawayV4FootYawDeg={_footYaw.footYawDeg:F1}f  (NEGATIVE = toes outward/un-pigeon — his straight-feet value was -15)");
            else if (_target == 6 && _fingerCurl != null)
                // 86catvb6u — bake into CastawayFingerCurl.fingerCurlDeg (soften v4's chunky-hand grip fold).
                Debug.Log($"[AxeNudgeTool] GRIP-CURL  fingerCurlDeg={_fingerCurl.fingerCurlDeg:F1}f  (lower = less fold/dark)");
            else if (_target == 7 && _hand != null)
            {
                // 86catvb6u round-8 — bake into CastawayV4RightWristEuler / CastawayV4LeftWristEuler (both hand bones).
                Vector3 r = _hand.rightWristEuler, l = _hand.leftWristEuler;
                Debug.Log($"[AxeNudgeTool] WRIST ({Side()} selected)  CastawayV4RightWristEuler=({r.x:F1}f,{r.y:F1}f,{r.z:F1}f)  " +
                          $"CastawayV4LeftWristEuler=({l.x:F1}f,{l.y:F1}f,{l.z:F1}f)  ([N] switch side; dial until both hands mirror)");
            }
            else if (_target == 8 && _hand != null)
            {
                // 86catvb6u round-8 — bake into CastawayV4RightThumbEuler / CastawayV4LeftThumbEuler (thumb below the wrist).
                Vector3 r = _hand.rightThumbEuler, l = _hand.leftThumbEuler;
                Debug.Log($"[AxeNudgeTool] HAND ({Side()} selected)  CastawayV4RightThumbEuler=({r.x:F1}f,{r.y:F1}f,{r.z:F1}f)  " +
                          $"CastawayV4LeftThumbEuler=({l.x:F1}f,{l.y:F1}f,{l.z:F1}f)  ([N] switch side; orient the thumb, wrist unaffected)");
            }
        }

        /// <summary>86cay4282 round 3 — the along-haft + context rows appended to the MINE bake log lines, so the
        /// Player.log the Sponsor reports back carries the SAME numbers the panel drew. A value that lives only on
        /// screen cannot be quoted in a soak report; a value that lives only in the log cannot be judged live. Both.</summary>
        private string GripLogSuffix()
        {
            if (!TryGripRead(out TwoHandGripRead.Read r)) return "";
            return AlongHaftLine(r) + "  " + GripContextLine(r);
        }

        private static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; return a; }

        /// <summary>
        /// soak-5 round-5 — compose a ROTATION nudge <paramref name="deltaEuler"/> onto a current euler in the
        /// weapon's LOCAL frame via QUATERNIONS, returning the resulting euler (each component normalised to
        /// [-180,180]). This fixes the F9 GIMBAL DEAD ZONE: adding a delta to a single euler COMPONENT (the old
        /// `relEuler += dr` / `_liveEuler += dr`) is degenerate near ±90° pitch — at the pickaxe's ~-70..-80 pitch
        /// the yaw component no longer maps to the rotation the Sponsor wants, so orientations were UNREACHABLE
        /// (his -362° yaw hunt through a full circle without ever landing). Right-multiplying Quaternion.Euler(delta)
        /// applies the nudge about the weapon's OWN axes, so EVERY orientation is reachable. The result is EXACT for
        /// baking: Quaternion.Euler(result) equals the composed rotation, and the seat application is unchanged
        /// (Quaternion.Euler(storedEuler)) — so committed/baked eulers keep their meaning. Near gimbal the euler
        /// TRIPLE may re-decompose (the display can jump to an equivalent form), but the ROTATION + the bake are
        /// exact and reachable. A zero delta returns the input untouched (a pure position nudge never round-trips).
        /// Pure + static so an EditMode test can pin reachability without a live panel.
        /// </summary>
        public static Vector3 ComposeLocalRot(Vector3 currentEuler, Vector3 deltaEuler)
        {
            if (deltaEuler == Vector3.zero) return currentEuler;
            Quaternion q = Quaternion.Euler(currentEuler) * Quaternion.Euler(deltaEuler);
            Vector3 e = q.eulerAngles;
            return new Vector3(Norm(e.x), Norm(e.y), Norm(e.z));
        }

        /// <summary>
        /// 86caju055 — true when the dev-overlay layer is revealed (F10 master ON) but THIS F9 dial tool is NOT
        /// engaged. Drives the "not engaged" indicator so the Sponsor knows the nudge keys are asleep (he isn't
        /// nudging into the void). Pure + public so the indicator's show-condition is regression-tested without a
        /// render. Note: F10 (DebugOverlays) is the master reveal; the indicator only shows within that layer.
        /// </summary>
        public bool ShowNotEngagedHint => DebugOverlays.Visible && !_active;

        void OnGUI()
        {
            if (!DebugOverlays.Visible) return; // F1 master gate (86cafd6d6) — F9 is the sub-toggle below it
            if (!_active)
            {
                // 86caju055 — F9 dial mode is OFF but the debug-overlay layer is up: draw a small "not engaged"
                // signpost so the Sponsor knows to press F9 before nudging (else the keys silently do nothing).
                DrawNotEngagedHint();
                return; // still INERT — no nudge panel until toggled on
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _style.normal.textColor = new Color(0.6f, 1f, 0.7f);
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _hintStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.45f); // warm-gold header
                // 86cay4282 round 3 — the MEASUREMENT block gets its own smaller style + its own colour, so three
                // data rows fit the box legibly and read as measurements rather than as more dial values.
                _measStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = (int)MeasFontSize, fontStyle = FontStyle.Bold };
                _measStyle.normal.textColor = new Color(1f, 1f, 0.72f);
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
                ? "HELD weapon: " + HeldWeaponLabel() + " (cycle [B]; offset + angle + [O]/[I] scale — per weapon)"
                : _target == 1
                ? "STUMP axe (in block — local)"
                : _target == 2
                ? "ARM pose — " + (_armSel == 0 ? "RIGHT arm" : "LEFT arm") + " ([N] switch arm; rotation only)"
                : _target == 3
                ? "GROUND-Y offset (feet-on-ground — PgUp/PgDn; affects rest AND walk)"
                : _target == 4
                ? "RUN arm-lower (axe in hand, calmer run swing — U/J=lower/raise; RUN to judge)"
                : _target == 5
                ? "FOOT-YAW (v4 pigeon-toe — H=toes outward / Y=inward; dial to straight, default -15)"
                : _target == 6
                ? "GRIP-CURL (v4 hand — T/G softens the grip fold; lower = less dark/segmented)"
                : _target == 7
                ? "WRIST — " + (_armSel == 0 ? "RIGHT hand" : "LEFT hand") + " un-twist ([N] switch; T/G/Y/H/U/J all 3 axes)"
                : _target == 8
                ? "HAND (thumb) — " + (_armSel == 0 ? "RIGHT" : "LEFT") + " ([N] switch; orient the thumb below the wrist)"
                : _target == 9
                ? "MINE de-grip (left arm — SHIPS ZERO; T/G opens the arms as an A/B; MINE to judge)"
                // Round 3: shortened to ONE line's worth. The key list moved to the value row + the hint rows, which is
                // where it belongs — this header only has to say WHAT is being edited.
                : MineSeatHeader;
            // SOAKFIX10 — the position line and the euler line are now SEPARATE so neither can overflow the
            // box (the Sponsor's "the 3rd rotation value is cut off the right edge" report). Each is short.
            // 86cay4282 round 2 — a THIRD value row, used only by the MINE + MINE-SEAT targets, carrying the live
            // two-hand grip MEASUREMENT + an explicit PASS/FAIL against the shipped caps. Blank for every other
            // target (they have nothing measurable to draw).
            // Round 3: the measurement block is THREE rows — distance-to-haft, ALONG-haft position, and the
            // separation/angle context. Blank for every target that has nothing measurable to draw.
            string posLine, eulerLine, gripLine = "", alongLine = "", contextLine = "";
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
            else if (_target == 9 && _armPose != null)
            {
                // 86cay4282 — the MINE de-grip. Surfacing the live WEIGHT is MANDATORY on an engagement-weighted
                // CastawayArmPose field (procedural-animation-verbs.md §Debug-instrument caveat): without it a dial
                // that is simply not engaged is indistinguishable from a broken handler — the exact trap that burned
                // the Sponsor twice on run-lower. Round 2: it SHIPS ZERO (the Sponsor reversed the direction), so the
                // label says so — a knob reading 0 with no explanation looks broken too.
                Vector3 mg = _armPose.mineDeGripEuler;
                posLine = $"MineDeGripEuler=({mg.x:F1}, {mg.y:F1}, {mg.z:F1})  (ships ZERO — T/G opens the arms as an A/B)";
                eulerLine = _armPose.MineDeGripWeight > 0.5f
                    ? $"MINE ENGAGED ✓ weight={_armPose.MineDeGripWeight:F2} (judge NOW — mid-swing)"
                    : $"mine weight={_armPose.MineDeGripWeight:F2} — equip the PICKAXE + click a boulder to engage; every other state untouched";
                string[] rows = GripReadoutRows(_armPose.MineDeGripWeight);
                gripLine = rows[0]; alongLine = rows[1]; contextLine = rows[2];
            }
            else if (_target == 10 && _heldRig != null)
            {
                // 86cay4282 round 2 — the MINE SEAT (the two-hand haft placement). Dual channel: the position delta
                // slides the haft, the euler delta turns it. Both are engagement-weighted, so the weight is drawn
                // for the same reason as above — and the THIRD row carries the live hand-to-haft measurement plus an
                // explicit PASS/FAIL against the shipped caps, which is what round 1's panel was missing.
                Vector3 o = _heldRig.mineSeatOffsetDelta, e = _heldRig.mineSeatEulerDelta;
                posLine = $"SeatOffsetDelta=({o.x:F3}, {o.y:F3}, {o.z:F3})   ([R]/[V] slide ALONG the haft)";
                eulerLine = $"SeatEulerDelta=({e.x:F1}, {e.y:F1}, {e.z:F1})   (T/G/Y/H/U/J — turn it onto the hand line)" +
                            (_heldRig.MineSeatWeight > 0.5f ? "  ENGAGED ✓" : "  [not engaged — MINE to judge]");
                string[] rows = GripReadoutRows(_heldRig.MineSeatWeight);
                gripLine = rows[0]; alongLine = rows[1]; contextLine = rows[2];
            }
            else if (_target == 5 && _footYaw != null)
            {
                // 86catvb6u — the v4 pigeon-toe counter-rotate. One scalar (both feet mirror); Y/H dials it.
                posLine = $"CastawayV4FootYawDeg={_footYaw.footYawDeg:F1}   (H = toes OUTWARD / Y = inward; both feet mirror)";
                eulerLine = "walk to judge — NEGATIVE un-pigeons (default -15); dial to straight, then bake CastawayV4FootYawDeg";
            }
            else if (_target == 6 && _fingerCurl != null)
            {
                // 86catvb6u — soften v4's chunky-hand grip fold (dark/segmented right hand when gripping). T/G dials
                // [0,90]. The APPLIED readout (per the doc rule) proves the write reaches the pose — the curl is
                // FORCED on while this target is selected, so it shows on any weapon, not only a belt-selected axe.
                posLine = $"fingerCurlDeg={_fingerCurl.fingerCurlDeg:F1}   (T/G = more/less curl [0-90]; lower = less dark fold)";
                eulerLine = (_fingerCurl.IsApplied ? "APPLIED ✓ (forced on for dialing)" : "NOT applied")
                          + " — dial until the right hand reads as clean as the left, then bake fingerCurlDeg";
            }
            else if (_target == 7 && _hand != null)
            {
                // 86catvb6u round-8 — the both-hand un-twist. All 3 axes (T/G=X, Y/H=Y, U/J=Z) on the SELECTED hand
                // ([N] switch). Front view, empty-handed, idle: dial each hand natural, then bake the two constants.
                Vector3 we = _armSel == 0 ? _hand.rightWristEuler : _hand.leftWristEuler;
                Vector3 oth = _armSel == 0 ? _hand.leftWristEuler : _hand.rightWristEuler;
                posLine = $"{(_armSel == 0 ? "RightWristEuler" : "LeftWristEuler")}=({we.x:F1}, {we.y:F1}, {we.z:F1})   (T/G=X  Y/H=Y  U/J=Z)";
                eulerLine = $"other {(_armSel == 0 ? "LeftWristEuler" : "RightWristEuler")}=({oth.x:F1}, {oth.y:F1}, {oth.z:F1})  — dial each hand natural, then bake both";
            }
            else if (_target == 8 && _hand != null)
            {
                // 86catvb6u round-8 — the THUMB knob (orient the thumb below the wrist, wrist unaffected). SELECTED
                // side ([N] switch); ships 0. Dial the thumb toward/away the body, then bake the two thumb constants.
                Vector3 te = _armSel == 0 ? _hand.rightThumbEuler : _hand.leftThumbEuler;
                Vector3 oth = _armSel == 0 ? _hand.leftThumbEuler : _hand.rightThumbEuler;
                posLine = $"{(_armSel == 0 ? "RightThumbEuler" : "LeftThumbEuler")}=({te.x:F1}, {te.y:F1}, {te.z:F1})   (T/G=X  Y/H=Y  U/J=Z; wrist unaffected)";
                eulerLine = $"other {(_armSel == 0 ? "LeftThumbEuler" : "RightThumbEuler")}=({oth.x:F1}, {oth.y:F1}, {oth.z:F1})  — point the thumb toward the body, then bake";
            }
            else { posLine = _target == 2 ? "(arm pose not found)" : _target == 3 ? "(castaway not found)"
                            : _target == 5 ? "(foot-yaw not found)" : _target == 6 ? "(finger-curl not found)"
                            : (_target == 7 || _target == 8) ? "(hand pose not found)"
                            : _target == 10 ? "(held-tool rig not found — the MINE seat cannot be dialed)"
                            : "(arm pose not found)"; eulerLine = ""; }

            float lx = x + 12f, lw = w - 24f;
            // PURPOSE header + a one-line "what this does" so the tool is self-explanatory (was unclear).
            GUI.Label(new Rect(lx, y + 8f, lw, 22f), "WEAPON NUDGE TOOL  (debug — F9 to close)", _titleStyle);
            GUI.Label(new Rect(lx, y + 30f, lw, 20f),
                "Dial each weapon's position/angle in-game, then read the values to bake.", _hintStyle);

            // The "Editing:" header gets TWO lines of vertical room (86cay4282 round 3). Several targets' labels are
            // longer than the box is wide — FOOT-YAW's is ~83 chars — and IMGUI WORD-WRAPS by default, so the second
            // wrapped line was landing ON TOP of the value row below it. Caught by eyeballing this round's own shipped
            // panel capture, which is exactly what the capture gate is for. Two lines rather than wordWrap=false,
            // because clipping would silently amputate the tail of those labels (FOOT-YAW would lose its
            // "default -15") — a fresh instance of the very "the number exists but he cannot read it" failure this
            // round is closing. Shortening the other targets' labels is left alone: out of scope here.
            GUI.Label(new Rect(lx, y + 56f, lw, 44f), "Editing: " + tgt, _style);
            // SOAKFIX10 — position + euler on their OWN lines so all three components of EACH are always
            // fully visible inside the (now wider) box, on any screen width. Copyable, never cut off.
            GUI.Label(new Rect(lx, y + 100f, lw, 22f), posLine, _style);
            GUI.Label(new Rect(lx, y + 122f, lw, 22f), eulerLine, _style);
            // The MEASUREMENT block (MINE / MINE-SEAT only) — every field of the grip read a human would judge on.
            // Row 1 = distance to the haft (is one stick through both hands?), row 2 = ALONG-haft position (is a hand
            // stuck at an END? — the round-3 defect), row 3 = separation + angle (what EXPLAINS the residual). Drawn in
            // the smaller _measStyle so all three fit the box; an IMGUI label wider than its Rect is CLIPPED, and a
            // clipped number is a number the Sponsor was not shown.
            if (gripLine.Length > 0) GUI.Label(new Rect(lx, y + 144f, lw, 20f), gripLine, _measStyle);
            if (alongLine.Length > 0) GUI.Label(new Rect(lx, y + 166f, lw, 20f), alongLine, _measStyle);
            if (contextLine.Length > 0) GUI.Label(new Rect(lx, y + 188f, lw, 20f), contextLine, _measStyle);

            string[] hints =
            {
                "[K] held/stump/arm/GROUND-Y/RUN/FOOT-YAW/GRIP-CURL/WRIST/HAND/MINE/MINE-SEAT    [N] right<->left",
                "Move:   ←/→ = X    ↑/↓ = Z    PgUp/PgDn = Y      [R]/[V] = slide ALONG the haft (MINE SEAT)",
                "Rotate: T/G = pitch   Y/H = yaw   U/J = roll    [F] front-view snap   [B] cycle held weapon",
                "Scale (held weapon): [O] bigger / [I] smaller — Danish-safe (axe LOCKED; use settings HeldScale row)",
                "Hold Shift = 5x step    Hold Ctrl = 0.2x step    Values print to the log to bake.",
            };
            for (int i = 0; i < hints.Length && i < HintRowCount; i++)
                GUI.Label(new Rect(lx, y + FirstHintY + i * HintRowStep, lw, 20f), hints[i], _hintStyle);
        }

        // 86caju055 — the "F9 dial: NOT ENGAGED" signpost drawn when the debug-overlay layer is up but this tool
        // is asleep. A small dim badge, top-right, BELOW BootHud's build-stamp plate (y 8..34) and clear of the
        // bottom HUD zones. So the Sponsor never nudges into the void wondering why nothing moves.
        private void DrawNotEngagedHint()
        {
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _hintStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            }
            const float w = 340f, h = 22f;
            float x = Mathf.Max(12f, Screen.width - w - 12f);
            float y = 40f; // just under the top-right build stamp
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y + 2f, w - 12f, 18f),
                "WEAPON NUDGE (F9): NOT ENGAGED — press F9 to dial", _hintStyle);
        }
    }
}
