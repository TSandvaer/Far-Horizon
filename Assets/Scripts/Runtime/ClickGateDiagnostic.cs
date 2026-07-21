using UnityEngine;
using FarHorizon.Combat;

namespace FarHorizon
{
    /// <summary>
    /// LEFT-CLICK VERB-GATE DIAGNOSTIC (ticket 86caffwv5, PR #327 — the [[soak-fail-test-pass-instrument-runtime]]
    /// instrument). Round-5 verified the mine LOGIC correct via teleport-gates, yet the Sponsor's REAL-input path
    /// (walk + real mouse + real belt) still fails. This is NOT a fix — it is a read-only instrument that, on EVERY
    /// real left-click, dumps the GROUND TRUTH of which consumer claimed the click and, for every verb that did NOT,
    /// its FIRST failing guard WITH VALUES — so the live failure is PINNED from one line instead of hypothesised.
    ///
    /// === What it emits (ONE consolidated <c>[ClickGateDiag]</c> line, to Player.log AND the F10 overlay) ===
    ///   WIN=&lt;consumer&gt;  — who claimed: chopTree / mineBoulder / mineOre / meleeAttack-target /
    ///                       meleeAttack-whiff / NONE (the pure <see cref="ClassifyClick"/> — the SAME precedence the
    ///                       live Update chain applies: guards suppress ALL, then verb-wins-over-whiff, then melee).
    ///   sel=&lt;id&gt;        — the SELECTED belt item id (what is ACTUALLY in hand) — the belt-overflow tell (hyp b).
    ///   panel / overUI[elem] / rmb — the three GLOBAL world-click guards; overUI names the UI element the pointer
    ///                       hit (belt-strip[N] / pack-inv[N] / belt-dock[N] / pack-chrome) — the over-belt-strip tell (hyp a).
    ///   chop/boulder/ore   — per-verb: tool-selected bool + planar XZ distance to the nearest candidate target vs
    ///                       that verb's range (IN / FAR / none) — the FIRST failing guard with values.
    ///   melee              — weapon-selected(id) + target-in-reach.
    ///
    /// === The two round-5 live hypotheses, distinguishable from ONE line ===
    ///   (a) over-belt-strip click on a LOW boulder → <c>WIN=NONE overUI=1[belt-strip[N]]</c> while
    ///       <c>boulder pick=1 d=1.2/2.4 IN</c> (the boulder WAS reachable; the belt-strip UI ate the click).
    ///   (b) belt-overflow → <c>sel=&lt;not-a-pickaxe&gt;</c> and <c>boulder pick=0</c> (the pickaxe overflowed to the
    ///       pack, so it is not the selected belt item — it can never be selected — and melee whiffs with whatever IS).
    ///
    /// === Ships in the SOAK (deliberately) ===
    /// Unlike the [chop/mine/boulder-trace] lines (which are [Conditional("UNITY_EDITOR")] and STRIP from the exe),
    /// this <c>[ClickGateDiag]</c> line is a PLAIN Debug.Log so it lands in the shipped soak's Player.log. It only
    /// fires on a click EDGE (a cold path — no per-frame log/alloc; unity6-mastery §5). The overlay draw rides the
    /// shared <see cref="DebugOverlays.Visible"/> master (F10 — Danish-keyboard-safe, no new punctuation key; the
    /// project overlay convention) and PERSISTS the last click's line until the next click.
    ///
    /// === Self-wiring (unity-conventions.md §editor-vs-runtime) ===
    /// The component is authored onto Boot editor-time (MovementCameraScene.WireClickGateDiagnostic) so it SERIALIZES
    /// into Boot.unity (the component-in-source-but-not-in-scene trap); its refs self-resolve in Awake via
    /// FindObjectOfType (the project idiom — MeleeAttack.Awake does the same; every verb component exists by Awake).
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no [RuntimeInitializeOnLoadMethod] reset.
    /// </summary>
    public sealed class ClickGateDiagnostic : MonoBehaviour
    {
        [Tooltip("The melee attack (whiff/target consumer). Self-resolved in Awake if unset.")]
        public MeleeAttack melee;
        [Tooltip("The chop verb. Self-resolved in Awake if unset.")]
        public ChopTree chop;
        [Tooltip("The boulder-mine verb. Self-resolved in Awake if unset.")]
        public MineBoulder mineBoulder;
        [Tooltip("The ore-mine verb. Self-resolved in Awake if unset.")]
        public MineOre mineOre;
        [Tooltip("The inventory (selected belt item read). Self-resolved in Awake if unset.")]
        public Inventory inventory;
        [Tooltip("The inventory/belt UI (over-UI guard + element name). Self-resolved in Awake if unset.")]
        public InventoryUI inventoryUI;

        // The last click's consolidated line — persisted on the overlay until the next click.
        private string _lastLine = "[ClickGateDiag] (no click yet — F10 shows this; LEFT-CLICK to diagnose)";
        private GUIStyle _style;

        /// <summary>The consumer that OWNS a left-click (read by tests + the diagnostic line).</summary>
        public enum ClickConsumer { None, ChopTree, MineBoulder, MineOre, MeleeAttackTarget, MeleeAttackWhiff }

        /// <summary>The last click's WIN classification (exposed for a PlayMode/capture read).</summary>
        public ClickConsumer LastWinner { get; private set; } = ClickConsumer.None;
        /// <summary>The last click's consolidated line (exposed so a capture/test can read it without the overlay).</summary>
        public string LastLine => _lastLine;

        private void Awake()
        {
            if (melee == null) melee = FindObjectOfType<MeleeAttack>();
            if (chop == null) chop = FindObjectOfType<ChopTree>();
            if (mineBoulder == null) mineBoulder = FindObjectOfType<MineBoulder>();
            if (mineOre == null) mineOre = FindObjectOfType<MineOre>();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
            useGUILayout = false; // explicit-Rect OnGUI — skip IMGUI's layout pass (86cahhfp4 C2a)
        }

        private void Update()
        {
            // Fire on the REAL left-click EDGE — the Sponsor's failing path (walk + real mouse + real belt). The
            // programmatic latches drive the SAME state; ReportClick(source) is the seam a capture/PlayMode can poke.
            if (Input.GetMouseButtonDown(0)) EmitDiag("LMB");
        }

        /// <summary>Emit the diagnostic for a programmatic click (capture / PlayMode) — the analog of the real edge
        /// above, so the shipped capture can exercise the same read where a mouse button can't be injected.</summary>
        public void ReportClick(string source) => EmitDiag(source);

        /// <summary>
        /// PURE click-consumer classification (the unit-testable seam — matches the ShouldChopOnClick / ShouldSwingOnClick
        /// guard-table style). Models the SAME precedence the live Update chain produces:
        ///   1. ANY global world-click guard (panel open / pointer over UI / RMB orbit-drag) suppresses EVERY consumer
        ///      (chop/mine/melee all re-apply these — a guarded click fires nothing). → None.
        ///   2. Otherwise VERB-WINS-OVER-WHIFF (MeleeAttack.VerbClaimsClick order: chop → boulder → ore). In play only
        ///      one tool is ever selected, so at most one verb claims; the order is the deterministic tie-break.
        ///   3. Otherwise MeleeAttack swings iff a weapon is selected — a target in reach → lands; empty air → whiffs.
        /// The verb *Claims flags are the guard-IGNORING <c>WouldClaimClick()</c> (tool selected + a target in range);
        /// this fn layers the shared guards on top, matching the real outcome that a guarded click near a tree
        /// consumes NOTHING (not even the chop).
        /// </summary>
        public static ClickConsumer ClassifyClick(bool chopClaims, bool boulderClaims, bool oreClaims,
                                                  bool weaponSelected, bool meleeHasTarget,
                                                  bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)
        {
            if (uiPanelOpen || pointerOverUI || rmbHeld) return ClickConsumer.None;
            if (chopClaims) return ClickConsumer.ChopTree;
            if (boulderClaims) return ClickConsumer.MineBoulder;
            if (oreClaims) return ClickConsumer.MineOre;
            if (weaponSelected) return meleeHasTarget ? ClickConsumer.MeleeAttackTarget : ClickConsumer.MeleeAttackWhiff;
            return ClickConsumer.None;
        }

        private void EmitDiag(string source)
        {
            // Global world-click guards (the SAME three every consumer re-applies).
            bool panelOpen = UiInputGate.CaptureWorldInput;
            Vector3 mouse = Input.mousePosition;
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(mouse);
            string overElem = overUI ? (inventoryUI != null ? inventoryUI.DescribeOverUI(mouse) : "?") : "-";
            bool rmb = Input.GetMouseButton(1);

            // Selected belt item id — the belt-overflow tell (hyp b): what is ACTUALLY in hand.
            string selId = "-";
            if (inventory != null && inventory.Model != null)
            {
                var sel = inventory.Model.SelectedBeltStack;
                if (!sel.IsEmpty && sel.Def != null) selId = sel.Def.Id;
            }

            // Per-verb ground truth (each verb's OWN resolver — no duplicated/drifting distance math).
            VerbGateDiag chopD    = chop != null ? chop.ClickGateDiag()    : default;
            VerbGateDiag boulderD = mineBoulder != null ? mineBoulder.ClickGateDiag() : default;
            VerbGateDiag oreD     = mineOre != null ? mineOre.ClickGateDiag()     : default;
            MeleeGateDiag meleeD  = melee != null ? melee.ClickGateDiag()   : default;

            ClickConsumer win = ClassifyClick(chopD.WouldClaim, boulderD.WouldClaim, oreD.WouldClaim,
                                              meleeD.WeaponSelected, meleeD.HasTarget,
                                              panelOpen, overUI, rmb);
            LastWinner = win;

            _lastLine = "[ClickGateDiag] WIN=" + WinnerName(win)
                + " (" + source + ") | sel=" + selId
                + " | panel=" + B(panelOpen) + " overUI=" + B(overUI) + "[" + overElem + "] rmb=" + B(rmb)
                + " | chop axe=" + B(chopD.ToolSelected) + " " + DistStr(chopD)
                + " | boulder pick=" + B(boulderD.ToolSelected) + " " + DistStr(boulderD)
                + " | ore pick=" + B(oreD.ToolSelected) + " " + DistStr(oreD)
                + " | melee wpn=" + B(meleeD.WeaponSelected) + "(" + (meleeD.WeaponId ?? "-") + ") tgt=" + B(meleeD.HasTarget);

            Debug.Log(_lastLine);
        }

        /// <summary>Stable string for the winner enum (the log/overlay label — matches the ticket vocabulary).</summary>
        public static string WinnerName(ClickConsumer c)
        {
            switch (c)
            {
                case ClickConsumer.ChopTree:          return "chopTree";
                case ClickConsumer.MineBoulder:       return "mineBoulder";
                case ClickConsumer.MineOre:           return "mineOre";
                case ClickConsumer.MeleeAttackTarget: return "meleeAttack-target";
                case ClickConsumer.MeleeAttackWhiff:  return "meleeAttack-whiff";
                default:                              return "NONE";
            }
        }

        private static string B(bool v) => v ? "1" : "0";

        private static string DistStr(VerbGateDiag d)
        {
            if (d.NearestDist < 0f) return "d=none";
            return "d=" + d.NearestDist.ToString("F1") + "/" + d.Range.ToString("F1") + (d.TargetInRange ? " IN" : " FAR");
        }

        private void OnGUI()
        {
            if (!DebugOverlays.Visible) return; // F10 master — clean screen by default (overlay convention)
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                _style.normal.textColor = new Color(0.6f, 1f, 0.85f); // teal — distinct from the warm-gold nudge panels
            }

            // TOP-CENTER band, clear of the top-left BootHud stamp + the top-center PondNudge panel (drawn lower).
            float w = Mathf.Min(Screen.width - 20f, 900f);
            float x = Mathf.Max(10f, (Screen.width - w) * 0.5f);
            float y = 84f;
            float h = 58f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y + 4f, w - 16f, h - 8f), _lastLine, _style);
        }
    }

    /// <summary>Per-verb (chop / boulder-mine / ore-mine) left-click gate READOUT for the ClickGateDiagnostic — the
    /// tool-selected gate + the planar-XZ distance to the nearest CANDIDATE target vs that verb's range. Built by each
    /// verb's own resolver (ground truth, no duplicated distance math). NearestDist &lt; 0 = no candidate target exists.</summary>
    public struct VerbGateDiag
    {
        public bool ToolSelected;
        public float NearestDist;
        public float Range;
        /// <summary>A candidate target exists AND is within the verb's range.</summary>
        public bool TargetInRange => NearestDist >= 0f && NearestDist <= Range;
        /// <summary>The verb would claim the click absent the shared world-click guards (tool + target-in-range).</summary>
        public bool WouldClaim => ToolSelected && TargetInRange;
    }

    /// <summary>MeleeAttack's left-click gate READOUT for the ClickGateDiagnostic — the selected weapon + whether a
    /// target is in reach (whiff vs land). A whiff is a valid swing; only DAMAGE gates on a target.</summary>
    public struct MeleeGateDiag
    {
        public bool WeaponSelected;
        public string WeaponId;
        public bool HasTarget;
        public float Reach;
    }
}
