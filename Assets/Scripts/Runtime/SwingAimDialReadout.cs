using System.Globalization;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// THE READ-BACK HALF OF THE SWING-AIM NUDGE HANDLE (86cb6v03j round 2).
    ///
    /// The F3 console rows let the Sponsor DIAL <see cref="SwingAimNudge"/>; this component is how those numbers
    /// COME BACK. Two independent channels, on purpose — a soak hand-off that depends on the Sponsor remembering
    /// to do something is a hand-off that loses the values:
    ///
    ///   1. **ON SCREEN** — an IMGUI block listing every class's dialled nudge AND its EFFECTIVE euler (the exact
    ///      triple to bake), plus the LIVE engagement readout. He screenshots it.
    ///   2. **IN THE PLAYER LOG** — the same block written as <c>[swing-aim-dial]</c> lines, flushed whenever a
    ///      dial moves (coalesced) and once more on quit. Survives a forgotten screenshot; one grep recovers it.
    ///
    /// === Why the ENGAGEMENT readout is not optional ===
    /// The swing-aim delta is ENGAGEMENT-WEIGHTED — it is 0 unless that class's attack state owns Animator layer
    /// 0. `procedural-animation-verbs.md` §"Debug-instrument caveat: run-lower's engagement is state-gated" makes
    /// the rule explicit: *"any debug/nudge instrument that targets an engagement-weighted field must either
    /// drive/force the gating state or surface the current engagement weight on-screen — a raw value dial with no
    /// engagement readout can't be told apart from a broken handler"*. The Sponsor was burned by exactly that
    /// twice on run-lower. So the panel prints <c>live: class=… weight=…</c> every frame: at rest it reads
    /// <c>class=- weight=0.00</c>, which says "not engaged", not "broken". Forcing the gating state was rejected
    /// as the alternative — the delta is applied on top of the SWING's hand rotation, so previewing it at rest
    /// would show him a pose the strike never passes through.
    ///
    /// === Why it does not ride the F10 DebugOverlays master (a deliberate divergence from the sibling overlays) ===
    /// Two reasons, both concrete. (a) The F10 master does NOT protect the capture gates: `SwingVerifyCapture`
    /// itself calls <c>DebugOverlays.Show()</c> before shooting, so an overlay gated only on the master WOULD
    /// paint over the swing side-profile PNGs. What actually protects them is
    /// <see cref="SwingAimNudge.IsPristine"/> — at the shipped default there is nothing dialled, so this
    /// component draws nothing and logs nothing, in any capture, on any run. (b) Hiding an engagement-weighted
    /// dial's only feedback behind a SECOND key is the "reads as broken" trap this panel exists to prevent.
    /// Net effect on a normal launch: invisible and silent until a dial moves.
    ///
    /// NO MUTABLE RUNTIME STATICS (instance fields only) — the dial state + its mandatory SubsystemRegistration
    /// reset live on <see cref="SwingAimNudge"/>, so the StaticStateResetTests whole-asmdef audit is unaffected.
    /// </summary>
    public class SwingAimDialReadout : MonoBehaviour
    {
        /// <summary>The hero held-tool object the swing-aim weight is read from — the SAME name
        /// <c>SwingVerifyCapture</c> resolves ("HeroAxe": the one seated tool, whatever mesh it displays).</summary>
        public const string HeroToolObjectName = "HeroAxe";

        [Tooltip("Force the readout visible even with every dial still at 0 — so the Sponsor can watch the LIVE " +
                 "engagement (class + weight) before he touches a dial and confirm the swing is being seen at " +
                 "all. F4: an F-key, so it sits at the same physical position on a Danish keyboard " +
                 "([[sponsor-danish-keyboard-layout]]), and it is FREE — F1 player Settings / F3 dev console / " +
                 "F7 camera-follow / F8 float diag / F9 axe nudge / F10 overlay master are the bound ones; F2 is " +
                 "UNBOUND by decision and F5/F6 died with the sneak panel. Once any dial is non-zero the readout " +
                 "shows regardless of this key.")]
        public KeyCode readoutKey = KeyCode.F4;

        [Tooltip("Minimum seconds between two [swing-aim-dial] log flushes. A slider drag fires the setter many " +
                 "times per second; without coalescing the Player.log fills with near-duplicate blocks and the " +
                 "final value gets buried. Every flush is a COMPLETE block, so the LAST one in the log is always " +
                 "the current state — and a quit flush guarantees there is a last one.")]
        public float logCoalesceSeconds = 0.5f;

        private HeldToolRig _rig;
        private int _renderedRevision = -1;    // which SwingAimNudge.Revision the cached text was built from
        private string _cachedBlock;           // the per-class block (rebuilt only when a dial moves)
        private bool _forced;                  // F4 state
        private int _pendingRevision = -1;     // the revision awaiting a log flush (-1 = nothing pending)
        private float _lastFlushAt = -999f;
        private GUIStyle _style;

        /// <summary>Whether the on-screen readout is currently drawn (public so a test/capture can assert the
        /// pristine-build invariant without scraping pixels).</summary>
        public bool ReadoutVisible => _forced || !SwingAimNudge.IsPristine;

        private void Awake() => ResolveRig();

        private void ResolveRig()
        {
            // Prefer the hero tool by NAME (the object the swing-aim weight actually eases on); fall back to any
            // rig in the scene so a bare test rig still reports. Re-resolved lazily below while still null —
            // never permanently cached from a miss (unity6-mastery §5, the OnEnable null-cache trap).
            var all = Object.FindObjectsByType<HeldToolRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.name == HeroToolObjectName) { _rig = all[i]; return; }
            _rig = all.Length > 0 ? all[0] : null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(readoutKey))
            {
                _forced = !_forced;
                // One line on an explicit keypress — NOT a per-frame log (unity6-mastery §5).
                Debug.Log(SwingAimNudge.LogTag + " readout " + (_forced ? "SHOWN (F4 to hide)" : "hidden") +
                          " — dials live in the F3 dev console under 'Swing aim …'");
                if (_forced) FlushLog("F4");
            }

            if (_rig == null) ResolveRig();

            // A dial moved → schedule a flush. The revision counter is the whole change-detection mechanism, so
            // no per-frame vector diffing and no per-frame string work happens while nothing is being dialled.
            int rev = SwingAimNudge.Revision;
            if (rev != _renderedRevision && _pendingRevision != rev) _pendingRevision = rev;

            if (_pendingRevision >= 0 && Time.unscaledTime - _lastFlushAt >= Mathf.Max(0.05f, logCoalesceSeconds))
                FlushLog("dial");
        }

        /// <summary>Write the complete current dial state to the Player log as <c>[swing-aim-dial]</c> lines.
        /// Public so the quit path, the F4 path and a test can all drive the one implementation.</summary>
        public void FlushLog(string reason)
        {
            _pendingRevision = -1;
            _lastFlushAt = Time.unscaledTime;
            Debug.Log(SwingAimNudge.LogTag + " BLOCK (" + reason + ") — bake the 'effective' triples into " +
                      "HeldToolRig.SwingAim<Class>\n" + SwingAimNudge.Readout());
        }

        private void OnApplicationQuit()
        {
            // The guarantee that the soak's values survive even if he never screenshots and never re-touches a
            // dial before closing: the LAST [swing-aim-dial] block in the log is always the final state.
            if (!SwingAimNudge.IsPristine) FlushLog("quit");
        }

        private void OnGUI()
        {
            // PRISTINE => SILENT. This single line is what keeps every -verify* capture byte-identical to
            // 70583d8: at the shipped default there is nothing dialled, so nothing paints.
            if (!ReadoutVisible) return;

            if (SwingAimNudge.Revision != _renderedRevision || _cachedBlock == null)
            {
                _cachedBlock = SwingAimNudge.Readout();
                _renderedRevision = SwingAimNudge.Revision;
            }
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false, wordWrap = false };

            int liveClass = _rig != null ? _rig.SwingAimClass : -1;
            float liveWeight = _rig != null ? _rig.SwingAimWeight : 0f;
            // ONE small formatted string per frame while the panel is open (a dev-only, non-default path). The
            // five-class block above is cached and only rebuilt when a dial actually moves.
            string live = "live: class=" + (liveClass >= 0 ? SwingAimNudge.ClassName(liveClass) : "-") +
                          "  weight=" + liveWeight.ToString("0.00", CultureInfo.InvariantCulture) +
                          (liveWeight <= 0.001f ? "  (not swinging — the dial only shows mid-swing)" : "");

            const float w = 620f, h = 168f;
            GUI.Box(new Rect(8f, Screen.height - h - 8f, w, h), GUIContent.none);
            GUI.Label(new Rect(16f, Screen.height - h - 2f, w - 16f, h),
                      "SWING-AIM DIALS — F3 console rows 'Swing aim <class> pitch/yaw/roll'   [F4] hide\n" +
                      live + "\n" + _cachedBlock + "\n" +
                      "these lines are also in Player.log — grep " + SwingAimNudge.LogTag, _style);
        }
    }
}
