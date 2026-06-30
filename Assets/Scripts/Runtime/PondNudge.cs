using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POND RECESS + FOAM LIVE NUDGE HANDLE (ticket 86cadj4g7 — Sponsor #130 re-soak of d6bf755: rejected
    /// 3 issues + chose the "live nudge handle" path so HE dials the final look and we bake his values).
    /// CLONES the PROVEN discrete-picker pattern of <see cref="HeldAxeLengthPicker"/> (the on-screen
    /// "AXE SHAFT LENGTH" control): an on-screen label + value readout + a one-shot log line per step,
    /// LAYOUT-AGNOSTIC keys, NO continuous runtime dial (the #100 axe-head dial failed because it deformed
    /// mesh verts at runtime + silently no-opped — discrete picker steps are the mechanism that always worked).
    ///
    /// ONE HANDLE (the foam dial was DROPPED — #130 third re-soak):
    ///  • POND RECESS DEPTH — [PgUp]/[PgDn] cycle discrete recess steps (flush -> knee-deep -> deeper).
    ///    DEFAULT = DEEPER (the Sponsor's chosen 0.75u-below-ground recess, baked #130 re-soak). The recess is
    ///    how far the WATER SURFACE sits BELOW the surrounding GROUND LEVEL. CRITICAL (the #130 mound defect): a
    ///    step moves the REAL terrain/collar relationship, not just the water-surface Y — it sinks the whole pond
    ///    ROOT (water + collar + accents) DOWN by the recess delta, so the green stays WALKABLE at ground level
    ///    while the water visibly sinks. (The terrain bowl is baked at the DEEPER default; this handle moves the
    ///    visual pond within/relative to it so the Sponsor judges the recess look.)
    ///
    /// FOAM DIAL DROPPED (#130 third re-soak): the Sponsor ALWAYS wants the freshwater pond foam OFF (a still
    /// pool — "should not foam like the sea"), so a foam control here is moot — AND the old Home/End foam dial
    /// was DEAD at runtime (it drove the shared depth-fade foam term, which the shipped pond ships zeroed). Foam
    /// is now baked OFF unconditionally on the pond material (MakePondMaterial _FoamAmount=0 + the committed
    /// PondWaterMat.mat asset), not a runtime dial. The white band the Sponsor saw was the depth-fade foam term
    /// rendering when a STALE committed material shipped foam-ON; the fix is the baked-off material + the
    /// top-down no-surface-white verify gate, not a dial.
    ///
    /// KEYS — LAYOUT-AGNOSTIC ONLY ([[sponsor-danish-keyboard-layout]]): PageUp/PageDown. NEVER US-position
    /// punctuation ([ ] ; ' - =) — they shift on the Sponsor's Danish laptop. This handle is ALWAYS-LIVE (like
    /// the axe LENGTH picker), but the F-key NUDGE TOOLS (AxeNudgeTool F9 / WorldLookNudgeTool F10 /
    /// CameraFollowNudgeTool F7) ALSO bind PgUp/PgDn. To stop a single press driving BOTH (ticket 86cafjrxk —
    /// Sponsor #176: "pond were manipulated because the pg up and down conflicts" while dialing the weapon),
    /// this handle YIELDS PgUp/PgDn whenever ANY of those nudge panels is toggled ON — the active panel owns the
    /// key. A normal pond soak (no F-key tool open) is unchanged. See <see cref="AnyNudgePanelActive"/>.
    ///
    /// ALWAYS-LIVE (like HeldAxeLengthPicker, NOT toggle-gated like the F9/F10 nudge tools): the panel shows
    /// the current recess value the whole soak so the Sponsor reads it off-screen to report for baking. It
    /// starts at the SHIPPED default (DEEPER recess) so a soak that never presses a key sees exactly the
    /// shipped pond; pressing a key enters the recess step cycle.
    ///
    /// VERIFICATION entry point (-verifyPond / -verifyPondSide shipped-build captures): ForceRecessStep drives
    /// the SAME step path the Sponsor uses, so the shipped-build capture proves the step actually moves the pool
    /// (the #100 dial passed tests but no-opped at runtime — this drives the real path).
    ///
    /// STATIC STATE: instance fields only — NO mutable runtime statics, so no SubsystemRegistration reset is
    /// needed (StaticStateResetTests stays green).
    /// </summary>
    public class PondNudge : MonoBehaviour
    {
        // ---- LAYOUT-AGNOSTIC keys (Danish-safe — PgUp/PgDn, never US punctuation) ----
        [Tooltip("Deepen the pond recess one step (flush -> knee-deep -> deeper -> wrap). PageUp = layout-safe.")]
        public KeyCode recessDeeperKey = KeyCode.PageUp;
        [Tooltip("Shallow the pond recess one step (the reverse cycle). PageDown = layout-safe.")]
        public KeyCode recessShallowerKey = KeyCode.PageDown;

        // ---- RECESS STEPS (how far the WATER SURFACE sits BELOW the surrounding GROUND LEVEL) ----
        // PUBLIC + static so the EditMode guard reads the contract directly (mirrors HeldAxeLengthPicker).
        public static readonly string[] RecessStepNames = { "FLUSH", "SHIPPED", "DEEPER" };
        // The recess (below-plateau drop of the water surface) for each step, in cycle order. ROUND 9: the baked
        // recess dropped 0.75 → 0.30 (LowPolyZoneGen.PondRecessKneeDeep — so the dry shore lip rising the recess
        // back to the rim stays a SHORT traversable step-over → the bowl fills to ≈0.90 of the mouth with no
        // walkable dry slope; the knee-deep DEPTH moved into PondWadeDepth 0.75 instead). The DEFAULT step (index
        // 1, SHIPPED) matches the baked recess (0.30). FLUSH (shallower) + DEEPER remain A/B contrast steps the
        // Sponsor can dial. So a soak with NO key-press sees exactly the shipped pool. NOTE: this handle nudges the
        // WATER-DISC root Y relative to the baked terrain bowl (it does NOT re-carve the terrain), so DEEPER here
        // only sinks the visible surface — a re-bake is needed to move the carved bowl itself.
        public static readonly float[] RecessStepValue = { 0.12f, 0.30f, 0.55f };
        public const int RecessDefaultStep = 1; // SHIPPED — the shipped default (baked recess, #130 round 9)

        private int _recessStep = RecessDefaultStep;

        // Resolved live handles (lazily, on first key OR first Force* call).
        private bool _resolved;
        private FreshwaterPond _pond;        // the pond root we sink/raise
        private Transform _pondRoot;         // _pond.transform — the unit (water + collar + accents) we move
        private Transform _waterT;           // PondWater child (for the surface-Y readout)
        private float _baseRootY;            // the SHIPPED root Y (recess = DEEPER default). The recess delta
                                             // re-positions relative to this so a step is reproducible + the
                                             // default step lands exactly on the shipped pond.

        private GUIStyle _labelStyle, _keyStyle;

        /// <summary>The currently-selected recess (water-below-ground drop) in world units — for the verify
        /// capture + the EditMode/PlayMode tests to read what the handle applied.</summary>
        public float CurrentRecess => RecessStepValue[Mathf.Clamp(_recessStep, 0, RecessStepValue.Length - 1)];

        private void Awake()
        {
            // Resolve in Awake is NOT safe — the pond root is grounded by WorldBootstrap.GroundPondInBowl at
            // bootstrap (editor-time) but the runtime scene-find must run after the pond's own Awake. Resolve
            // lazily on first use instead (a soak that never presses a key pays nothing + never re-bases off a
            // half-initialized scene).
        }

        private void Resolve()
        {
            _resolved = true;
            _pond = FindObjectOfType<FreshwaterPond>();
            if (_pond == null)
            {
                Debug.LogWarning("[PondNudge] no FreshwaterPond in scene — the pond nudge handle is inert");
                return;
            }
            _pondRoot = _pond.transform;
            _baseRootY = _pondRoot.position.y; // the shipped (DEEPER) root Y — recess deltas re-base off this
            _waterT = _pondRoot.Find("PondWater");
        }

        private void Update()
        {
            bool deeper = Input.GetKeyDown(recessDeeperKey);
            bool shallower = Input.GetKeyDown(recessShallowerKey);
            if (!deeper && !shallower) return; // no PgUp/PgDn this frame — pay nothing (no Find allocs)

            // KEYBIND DECONFLICT (ticket 86cafjrxk — Sponsor #176 weapon soak: "pond were manipulated because
            // the pg up and down conflicts"). The F-key NUDGE TOOLS (AxeNudgeTool F9 / WorldLookNudgeTool F10 /
            // CameraFollowNudgeTool F7) ALSO bind PgUp/PgDn (the weapon-head Y dial etc.), but they are toggle-
            // gated and only ONE is ever active (they enforce mutual exclusion among themselves). This pond
            // handle is ALWAYS-LIVE, so while a nudge panel is open the SAME PgUp/PgDn press drove BOTH — the
            // pond recess got accidentally set to DEEPER while the Sponsor was only dialing the weapon. Fix
            // (the ticket's "only the ACTIVE/focused debug tool consumes PgUp/PgDn" option): when ANY F-key
            // nudge panel is active it OWNS PgUp/PgDn, so this always-live pond handle yields. A normal pond
            // soak (no F-key tool open) is unchanged — PgUp/PgDn still steps the recess. Gated behind the
            // keypress above so the Find* scan only runs on a PgUp/PgDn frame, never per-frame.
            if (AnyNudgePanelActive()) return;

            int dir = deeper ? +1 : -1; // PgUp deepens, PgDn shallows (PgDn only reached if !deeper)
            Step(ref _recessStep, dir, RecessStepValue.Length);
            if (!_resolved) Resolve();
            ApplyRecess();
            LogRecess();
        }

        /// <summary>
        /// True if ANY debug NUDGE-PANEL tool (<see cref="INudgePanel"/>) is currently toggled ON. While one
        /// is, it OWNS PgUp/PgDn and this always-live pond handle must not also consume the same press (ticket
        /// 86cafjrxk deconflict). REFACTORED (86cafz9jr, #187 follow-up): the gate used to name the three
        /// concrete tool types (AxeNudgeTool / WorldLookNudgeTool / CameraFollowNudgeTool) one-by-one, which
        /// meant a 4th panel had to be remembered into a hand-maintained list — a missed edit silently
        /// re-introduced the collision class #187 fixed. Now it discovers active panels THROUGH the interface
        /// (a MonoBehaviour scene scan filtered by `is INudgePanel`), so the deconflict stays correct
        /// automatically as nudge panels are added/removed — any new INudgePanel is covered with no edit here.
        ///
        /// Only called on a PgUp/PgDn frame, so the Find* scan never runs per-frame. Includes inactive objects
        /// (FindObjectsInactive.Include) so a serialized-but-disabled tool still counts — identical to the
        /// behavior #187 shipped (an INudgePanel on a disabled GameObject is still discovered). Interfaces
        /// can't be passed to FindObjectsByType<T> (T must be Component-derived), so the scan is over
        /// MonoBehaviour + an `is INudgePanel` filter — the canonical Unity idiom for interface discovery.
        /// PUBLIC so the deconflict contract is testable without synthesizing a legacy-Input PgUp key-down
        /// (mirrors AxeNudgeTool.Activate/IsActive being public for the same reason).
        /// </summary>
        public static bool AnyNudgePanelActive()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is INudgePanel panel && panel.IsActive) return true;
            return false;
        }

        private static void Step(ref int idx, int dir, int count) => idx = ((idx + dir) % count + count) % count;

        // === RECESS: move the REAL relationship (sink the pond root AND keep the collar at ground level) ====
        // The recess is how far the water surface sits BELOW the surrounding ground. The shipped (knee-deep)
        // root sits at _baseRootY (water = ground − knee-deep). To apply a DIFFERENT recess we move the root by
        // the delta from the knee-deep default: a deeper recess sinks the whole pond UNIT (water + collar +
        // accents) further down; a flush recess raises it. The collar drapes with the root, so its outer rim
        // (authored at ground level for the knee-deep bake) shifts by the same delta — which means at a deeper
        // recess the collar outer edge would dip BELOW ground. We REBUILD nothing at runtime (the baked collar
        // mesh is fixed); instead we move the root by the recess delta and the Sponsor judges the RECESS LOOK
        // (how sunk the water reads). The collar-stays-at-ground-level invariant is enforced at BAKE time by
        // CollarOuterLocalY reaching the bowl mouth; the runtime handle's job is to let the Sponsor pick how
        // DEEP, then we re-bake the bowl + collar + grounding to that recess. So a deeper recess shows the
        // water sunk further (the look he judges) with the collar tracking; we bake the chosen recess so the
        // shipped collar is re-authored to reach ground level at that depth.
        private void ApplyRecess()
        {
            if (_pondRoot == null) return;
            float delta = CurrentRecess - RecessStepValue[RecessDefaultStep]; // + = deeper than the knee-deep default
            Vector3 p = _pondRoot.position;
            _pondRoot.position = new Vector3(p.x, _baseRootY - delta, p.z);
        }

        /// <summary>VERIFICATION: force a recess step (0..N) + apply — for the shipped-build capture to prove
        /// the step moves the real root. Returns the applied recess (or -1 if out of range).</summary>
        public float ForceRecessStep(int step)
        {
            if (step < 0 || step >= RecessStepValue.Length) return -1f;
            if (!_resolved) Resolve();
            _recessStep = step;
            ApplyRecess();
            return CurrentRecess;
        }

        // [pond-trace]-family logging — EDITOR/dev-only; stripped from the shipped IL2CPP release exe (and its
        // string-concat arg eval) so it never costs the player (unity6-mastery §5 / §10). The Sponsor reads the
        // value off the on-screen panel; this log line is the off-screen record he reports back to bake.
        private void LogRecess() => PondNudgeLog(
            "POND RECESS -> " + RecessStepNames[_recessStep] + " (" + CurrentRecess.ToString("F2") +
            "u below ground)  [PgUp/PgDn; bake into LowPolyZoneGen.PondRecessKneeDeep]");

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void PondNudgeLog(string msg) => Debug.Log("[pond-nudge] " + msg);

        private void OnGUI()
        {
            // F1 master gate (86cafd6d6): the dev/debug overlay layer is HIDDEN by default (clean screen for
            // normal play / soak / CI captures). F1 (DebugOverlayToggle) reveals it.
            if (!DebugOverlays.Visible) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = new Color(0.55f, 0.85f, 1f); // cool-blue (the pond is blue)
                _keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _keyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            }

            // Top-CENTER, just BELOW where HeldAxeLengthPicker draws (y=10, h=44) so the two never overlap.
            float w = 540f, h = 44f;
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);
            float y = 60f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 10f, y + 4f, w - 20f, 20f),
                "POND RECESS: " + RecessStepNames[_recessStep] + " (" + CurrentRecess.ToString("F2") +
                "u below ground)    FOAM: OFF (baked)", _labelStyle);
            GUI.Label(new Rect(x + 10f, y + 23f, w - 20f, 18f),
                "[PgUp/PgDn] recess  flush -> knee-deep -> deeper   (pick + report)",
                _keyStyle);
        }
    }
}
