using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The pure-C# FPS measurement seam behind <see cref="FpsCounterHud"/> (ticket 86cahmxmt).
    ///
    /// Accumulates frame deltas into fixed windows (default 0.5s = the ~2Hz readout refresh) and, when a
    /// window closes, publishes <see cref="Current"/> (frames/seconds over that window) + <see cref="Average"/>
    /// (mean of the last N published windows — the short rolling average, default 10 × 0.5s = 5s). Publishing
    /// at window-close, not per frame, is the whole design: the HUD only rebuilds its label when
    /// <see cref="Tick"/> returns true, so the per-frame path allocates NOTHING (the C2a IMGUI/GC discipline,
    /// 86cahhfp4 / unity6-mastery §5).
    ///
    /// Plain class (no Unity lifecycle) so the cadence + math are EditMode-testable with synthetic deltas —
    /// the same testable-seam shape as the SettingsCatalog binding map. Zero allocations after construction
    /// (the ring is a fixed array; no LINQ, no boxing).
    ///
    /// Headless guard: a dt &lt;= 0 tick is IGNORED (never accumulates, never publishes) — headless runs see
    /// Time.deltaTime ≈ 0 per frame (unity-conventions §Headless rituals), and a 0-length window would divide
    /// by zero. In a headless/batchmode run the meter simply never publishes, which is correct: there is no
    /// real frame rate to report.
    /// </summary>
    public sealed class FpsMeter
    {
        /// <summary>Readout refresh window in seconds — 0.5s = the ticket's ~2Hz update cadence.</summary>
        public const float DefaultRefreshInterval = 0.5f;

        /// <summary>Windows in the rolling average — 10 × 0.5s = a 5s "short rolling average".</summary>
        public const int DefaultAverageWindows = 10;

        private readonly float _refreshInterval;
        private readonly float[] _recent;   // ring of the last N published window rates
        private int _recentCount;           // how many ring slots are filled (grows to _recent.Length)
        private int _recentNext;            // next ring write index
        private float _windowTime;          // seconds accumulated in the open window
        private int _windowFrames;          // frames accumulated in the open window

        /// <summary>FPS over the last COMPLETED window (0 until the first window closes).</summary>
        public float Current { get; private set; }

        /// <summary>Mean of the last N completed windows' rates (0 until the first window closes).</summary>
        public float Average { get; private set; }

        /// <summary>True once at least one window has published (the HUD shows a placeholder until then).</summary>
        public bool HasSample { get; private set; }

        /// <summary>The publish cadence in seconds (0.5 = 2Hz).</summary>
        public float RefreshInterval => _refreshInterval;

        public FpsMeter(float refreshInterval = DefaultRefreshInterval, int averageWindows = DefaultAverageWindows)
        {
            // Floor the interval so a mis-configured 0 can never publish per-frame (the discipline this
            // seam exists to enforce); floor the ring at 1 so Average is always defined.
            _refreshInterval = Mathf.Max(0.05f, refreshInterval);
            _recent = new float[Mathf.Max(1, averageWindows)];
        }

        /// <summary>
        /// Feed one frame's UNSCALED delta. Returns TRUE only when a window closed and Current/Average were
        /// republished (≤ 2Hz by construction) — the caller's cue to rebuild any cached display text. The
        /// per-frame false path does no allocation and no division.
        /// </summary>
        public bool Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f) return false; // headless Time.deltaTime≈0 guard — never publish on it
            _windowTime += unscaledDeltaTime;
            _windowFrames++;
            if (_windowTime < _refreshInterval) return false;

            Current = _windowFrames / _windowTime;
            _recent[_recentNext] = Current;
            _recentNext = (_recentNext + 1) % _recent.Length;
            if (_recentCount < _recent.Length) _recentCount++;

            float sum = 0f;
            for (int i = 0; i < _recentCount; i++) sum += _recent[i];
            Average = sum / _recentCount;

            _windowTime = 0f;
            _windowFrames = 0;
            HasSample = true;
            return true;
        }
    }

    /// <summary>
    /// On-screen FPS counter for the shipped build (ticket 86cahmxmt — Sponsor, #226 walk-soak item 3:
    /// "We need to introduce a FPS counter to be displayed"). Draws "FPS &lt;current&gt; | avg &lt;rolling&gt;"
    /// on a small plate directly UNDER the top-right BUILD stamp (BootHud), so every future perf soak has an
    /// on-screen ground-truth number in the same self-identifying corner the stamp ritual already uses.
    ///
    /// C2a IMGUI/GC discipline (86cahhfp4 / unity6-mastery §5) — zero per-frame GC:
    ///  • <c>useGUILayout = false</c> in Awake (explicit Rects only — skip IMGUI's Layout event pass);
    ///  • the label string is CACHED and rebuilt ONLY when the meter publishes (≤2Hz) AND the rounded
    ///    numbers actually changed — never per frame, never per IMGUI event (BootHud's cached-stamp pattern);
    ///  • Update's non-publish path is a couple of float adds (FpsMeter.Tick early-outs, no division);
    ///  • the GUIStyle is created once, lazily inside OnGUI (GUI.skin is only valid there — BootHud precedent).
    ///
    /// TOGGLE = this component's <c>enabled</c> flag, driven by the `FPS counter` dev-console row
    /// (SettingsCatalog.PopulateFps → BoolSettingEntry — the hunger.enabled idiom from that entry's own doc).
    /// Disabled ⇒ no Update, no OnGUI ⇒ literally zero cost. No dedicated hotkey: the row lives in the F1
    /// console (F-keys + PgUp/PgDn nudge are already Danish-keyboard-safe, [[sponsor-danish-keyboard-layout]]),
    /// and registering through the catalog/registry means the row mechanically survives the future F1/F3
    /// panel-split (86cah8ukr) with whichever panel hosts the registry rows.
    ///
    /// DEFAULT = ON (ships enabled in the scene, so the BoolSettingEntry captures default=true at
    /// registration) — deliberate for this first build so the Sponsor sees it immediately at soak; flagged
    /// "default — Sponsor-soak tunes" (he dials always-on vs off via the row, persisted by PlayerPrefs).
    ///
    /// Reads Time.unscaledDeltaTime so the readout stays truthful if anything ever scales Time.timeScale
    /// (the real frame rate is what a perf soak needs, not the game-time rate). Serialized into Boot.unity
    /// editor-time by BootstrapProject.BuildBootScene (NOT Awake-added — the editor-vs-runtime trap); also
    /// authored into the POC-island scene (NextIslandPocScene) since the ask came from an island walk-soak.
    /// </summary>
    public class FpsCounterHud : MonoBehaviour
    {
        /// <summary>Shown until the first 0.5s window publishes (and in headless runs, forever — honest).</summary>
        public const string PlaceholderLabel = "FPS -- | avg --";

        // Field-initialized (not Awake-built) so bare EditMode AddComponent rigs can Step()/read Label
        // without reflection-invoking Awake (Awake does not auto-run in EditMode — ImguiLayoutPassTests note).
        private readonly FpsMeter _meter = new FpsMeter();

        private string _label = PlaceholderLabel;
        private int _lastCur = int.MinValue;
        private int _lastAvg = int.MinValue;
        private GUIStyle _style;

        /// <summary>The measurement seam (public for EditMode assertions on cadence/rates).</summary>
        public FpsMeter Meter => _meter;

        /// <summary>The cached display string. Reference-stable between publishes — tests assert the SAME
        /// string instance across sub-window ticks (the no-per-frame-alloc proof) and across publishes whose
        /// rounded values did not change (the no-churn proof).</summary>
        public string Label => _label;

        void Awake()
        {
            // No GUILayout.* in this OnGUI (explicit Rects only) — skip IMGUI's Layout event pass entirely
            // (one fewer OnGUI invocation per frame + no layout bookkeeping; 86cahhfp4 C2a).
            useGUILayout = false;
        }

        void Update() => Step(Time.unscaledDeltaTime);

        /// <summary>
        /// One frame of the counter (called by Update with the live unscaled delta; called directly by
        /// EditMode tests with synthetic deltas — the 2Hz-cache test seam). Rebuilds the cached label ONLY
        /// when the meter publishes AND the rounded readout actually changed.
        /// </summary>
        public void Step(float unscaledDeltaTime)
        {
            if (!_meter.Tick(unscaledDeltaTime)) return;           // ≤2Hz beyond this line, by construction

            int cur = Mathf.RoundToInt(_meter.Current);
            int avg = Mathf.RoundToInt(_meter.Average);
            if (cur == _lastCur && avg == _lastAvg) return;        // text unchanged → keep the cached string

            _lastCur = cur;
            _lastAvg = avg;
            // The ONLY allocation this component ever makes after startup: a small string at most 2×/second,
            // and only when the displayed numbers moved — the ticket's stated budget ("cached strings; update
            // the text at ~2Hz, not every frame"). Explicit ToString()s so no compiler-version boxing surprise.
            _label = "FPS " + cur.ToString() + " | avg " + avg.ToString();
        }

        void OnGUI()
        {
            if (_style == null)
            {
                // GUI.skin is only valid inside OnGUI — lazy one-time style init (BootHud precedent).
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _style.normal.textColor = Color.white;
            }

            // Small plate directly UNDER the BUILD-stamp plate (BootHud: y=8 h=26 top-right) — same right
            // margin, same plate alpha, so the corner reads as one self-identifying block in every capture.
            const float w = 190f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(Screen.width - w - 8f, 38f, w, 22f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - w, 40f, w - 8f, 18f), _label, _style);
        }
    }
}
