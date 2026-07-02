using Unity.Profiling;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Shipped-build performance probe (ticket 86cahhfp4, S2a — the honest-profiling enabler feeding the
    /// R2b shadow-trim / R3-1 static-batching go/no-go with DATA instead of code-derived estimates).
    ///
    /// Inert by default: self-instantiates ONLY under the <c>-perfProbe</c> launch flag via
    /// [RuntimeInitializeOnLoadMethod] — deliberately NOT a scene component, so the instrument ships in code
    /// with ZERO Boot.unity bytes (the Wave1-C "code-only, no regen" constraint) and zero cost in a normal
    /// launch. Run it against the -development build (FarHorizonBuilder S2a): a release-shape player compiles
    /// the profiler out, so the recorders go invalid there (the probe logs that loudly instead of lying).
    ///
    /// What it does: waits a warmup (shader-compile/startup hitches excluded), then WALKS the island via the
    /// established WasdMovement.SetInputOverride seam (the SneakVerifyCapture idiom — the REAL locomotion
    /// path: agent + animator + orbit camera all live, so the sampled frame is the frame the player pays),
    /// sweeping the heading through a slow arc so the view crosses forest/coast/interior. Samples per frame
    /// (no per-frame allocations; strings only at the 1 Hz report + summary):
    ///   - frame ms (unscaledDeltaTime) → fps
    ///   - Render counters: Draw Calls / SetPass Calls / Shadow Casters / Triangles / Vertices
    ///   - the URP main-light shadow CPU marker (candidate names probed; the log NAMES which one had data)
    ///   - GC allocated per frame (the C1/C2a steady-alloc evidence surface)
    /// Logs [PerfProbe] lines to Player.log and quits when the window closes (bounded run for scripting).
    /// Pair with the player's own -profiler-log-file &lt;path.raw&gt; -profiler-capture-frame-count N args for
    /// a full .raw capture loadable in the Profiler window — the probe numbers and the .raw come from the
    /// same run.
    ///
    /// NO MUTABLE STATICS (instance state only; the bootstrap method holds nothing) — needs no
    /// [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        /// <summary>The launch flag (established HasArg idiom).</summary>
        public const string Arg = "-perfProbe";

        /// <summary>Startup/shader-compile exclusion window before sampling starts.</summary>
        public const float WarmupSeconds = 4f;

        /// <summary>The sampled walking window.</summary>
        public const float SampleSeconds = 20f;

        /// <summary>One full input-heading revolution takes this long — over SampleSeconds the walk sweeps
        /// a ~120° arc, crossing multiple view loads (forest / coast / interior) instead of one straight line.</summary>
        public const float HeadingRevolutionSeconds = 60f;

        // Candidate CPU marker names for the URP main-light shadowmap pass. URP wraps the pass in a
        // ProfilingSampler whose public name has moved across URP versions — probe all three and REPORT
        // which one carried data rather than guessing (the log names the winner; consumers read that line).
        private static readonly string[] ShadowMarkerCandidates =
        {
            "MainLightShadow", "Main Light Shadowmap", "Shadows.Draw",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapIfRequested()
        {
            if (!WantsProbe(System.Environment.GetCommandLineArgs())) return;
            var go = new GameObject("PerfProbe");
            go.AddComponent<PerfProbe>();
            Debug.Log("[PerfProbe] armed by -perfProbe: warmup " + WarmupSeconds + "s, then a " + SampleSeconds +
                      "s island walk; summary + quit at window end");
        }

        /// <summary>Pure arg parse (EditMode-pinned; same testability pattern as
        /// FarHorizonBuilder.ResolveDevelopmentFlag).</summary>
        public static bool WantsProbe(string[] args)
        {
            if (args == null) return false;
            foreach (string a in args)
                if (a == Arg) return true;
            return false;
        }

        /// <summary>The walk heading at a given time into the sample window: starts straight ahead (0,1) and
        /// rotates a full revolution per <see cref="HeadingRevolutionSeconds"/> — always unit length, so the
        /// override input reads as a full held key. Pure (EditMode-pinned).</summary>
        public static Vector2 WalkHeading(float secondsIntoSample)
        {
            float a = 2f * Mathf.PI * (secondsIntoSample / HeadingRevolutionSeconds);
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        private WasdMovement _wasd;          // resolved once in Start (never per-frame — unity6-mastery §5)
        private bool _drivingWalk;

        private float _phaseT;               // seconds into the current phase
        private bool _sampling;              // false = warmup, true = sample window
        private bool _done;

        // Frame accumulators (sample window only; no per-frame allocations).
        private int _frames;
        private double _sumMs, _minMs = double.MaxValue, _maxMs;
        private long _sumDraw, _maxDraw, _sumSetPass, _sumShadowCasters, _maxShadowCasters, _sumTris, _sumVerts;
        private double _sumShadowMs;
        private long _sumGcBytes;
        private int _shadowMarkerIndex = -1;  // which candidate carried data (-1 = none yet)
        private float _nextReportT;           // 1 Hz progress line

        private ProfilerRecorder _drawCalls, _setPass, _shadowCasters, _tris, _verts, _gcAlloc;
        private ProfilerRecorder[] _shadowMarkers;

        private void Start()
        {
            _wasd = FindObjectOfType<WasdMovement>();
            if (_wasd == null)
                Debug.LogWarning("[PerfProbe] no WasdMovement in the scene — sampling a STATIONARY view " +
                                 "(numbers still valid, but this is not the walking capture the plan asks for)");

            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _verts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            _shadowMarkers = new ProfilerRecorder[ShadowMarkerCandidates.Length];
            for (int i = 0; i < ShadowMarkerCandidates.Length; i++)
                _shadowMarkers[i] = ProfilerRecorder.StartNew(ProfilerCategory.Render, ShadowMarkerCandidates[i]);

            if (!_drawCalls.Valid)
                Debug.LogWarning("[PerfProbe] Render counters are INVALID — this is a release-shape player " +
                                 "(profiler compiled out). Build with -development (FarHorizonBuilder S2a) " +
                                 "for honest numbers; frame-ms will still be reported.");
        }

        private void OnDestroy()
        {
            _drawCalls.Dispose(); _setPass.Dispose(); _shadowCasters.Dispose();
            _tris.Dispose(); _verts.Dispose(); _gcAlloc.Dispose();
            if (_shadowMarkers != null)
                for (int i = 0; i < _shadowMarkers.Length; i++) _shadowMarkers[i].Dispose();
            StopWalk();
        }

        private void Update()
        {
            if (_done) return;
            _phaseT += Time.unscaledDeltaTime;

            if (!_sampling)
            {
                if (_phaseT >= WarmupSeconds)
                {
                    _sampling = true;
                    _phaseT = 0f;
                    _nextReportT = 1f;
                    StartWalk();
                    Debug.Log("[PerfProbe] warmup over — sampling starts (walking the island)");
                }
                return;
            }

            // Drive the walk heading (the override reads as fully-held keys; unit length per WalkHeading).
            if (_drivingWalk) _wasd.SetInputOverride(WalkHeading(_phaseT));

            SampleFrame();

            if (_phaseT >= _nextReportT)
            {
                _nextReportT += 1f;
                ReportProgress();
            }

            if (_phaseT >= SampleSeconds)
            {
                _done = true;
                StopWalk();
                ReportSummary();
                Application.Quit();
            }
        }

        private void StartWalk()
        {
            if (_wasd == null) return;
            _wasd.SetInputOverride(WalkHeading(0f));
            _drivingWalk = true;
        }

        private void StopWalk()
        {
            if (_drivingWalk && _wasd != null) _wasd.ClearInputOverride();
            _drivingWalk = false;
        }

        private void SampleFrame()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            _frames++;
            _sumMs += ms;
            if (ms < _minMs) _minMs = ms;
            if (ms > _maxMs) _maxMs = ms;

            if (_drawCalls.Valid)
            {
                long draw = _drawCalls.LastValue;
                _sumDraw += draw;
                if (draw > _maxDraw) _maxDraw = draw;
            }
            if (_setPass.Valid) _sumSetPass += _setPass.LastValue;
            if (_shadowCasters.Valid)
            {
                long sc = _shadowCasters.LastValue;
                _sumShadowCasters += sc;
                if (sc > _maxShadowCasters) _maxShadowCasters = sc;
            }
            if (_tris.Valid) _sumTris += _tris.LastValue;
            if (_verts.Valid) _sumVerts += _verts.LastValue;
            if (_gcAlloc.Valid) _sumGcBytes += _gcAlloc.LastValue;

            // First candidate marker that reports non-zero time wins; stays the reported marker from then on
            // (the summary NAMES it, so the consumer knows which URP sampler the ms came from).
            if (_shadowMarkerIndex < 0)
            {
                for (int i = 0; i < _shadowMarkers.Length; i++)
                    if (_shadowMarkers[i].Valid && _shadowMarkers[i].LastValue > 0) { _shadowMarkerIndex = i; break; }
            }
            if (_shadowMarkerIndex >= 0 && _shadowMarkers[_shadowMarkerIndex].Valid)
                _sumShadowMs += _shadowMarkers[_shadowMarkerIndex].LastValue / 1_000_000.0; // ns → ms
        }

        private void ReportProgress()
        {
            double avgMs = _frames > 0 ? _sumMs / _frames : 0;
            Debug.Log($"[PerfProbe] t={_phaseT:F0}s frames={_frames} avgMs={avgMs:F2} " +
                      $"draws(last)={(_drawCalls.Valid ? _drawCalls.LastValue : -1)} " +
                      $"shadowCasters(last)={(_shadowCasters.Valid ? _shadowCasters.LastValue : -1)} " +
                      $"setPass(last)={(_setPass.Valid ? _setPass.LastValue : -1)}");
        }

        private void ReportSummary()
        {
            if (_frames == 0) { Debug.LogWarning("[PerfProbe] SUMMARY: zero frames sampled"); return; }

            double avgMs = _sumMs / _frames;
            double fps = avgMs > 0 ? 1000.0 / avgMs : 0;
            double avgDraw = (double)_sumDraw / _frames;
            double avgSetPass = (double)_sumSetPass / _frames;
            double avgShadowCasters = (double)_sumShadowCasters / _frames;
            double avgTrisM = _sumTris / (double)_frames / 1_000_000.0;
            double avgVertsM = _sumVerts / (double)_frames / 1_000_000.0;
            double avgShadowMs = _sumShadowMs / _frames;
            double shadowDrawShare = avgDraw > 0 ? avgShadowCasters / avgDraw : 0;
            double shadowCpuShare = avgMs > 0 ? avgShadowMs / avgMs : 0;
            double avgGcKb = _sumGcBytes / (double)_frames / 1024.0;
            string shadowMarker = _shadowMarkerIndex >= 0 ? ShadowMarkerCandidates[_shadowMarkerIndex] : "NONE-VALID";

            Debug.Log(
                $"[PerfProbe] SUMMARY frames={_frames} window={SampleSeconds}s | " +
                $"frameMs avg={avgMs:F2} min={_minMs:F2} max={_maxMs:F2} fps={fps:F1} | " +
                $"drawCalls avg={avgDraw:F0} max={_maxDraw} setPass avg={avgSetPass:F0} | " +
                $"shadowCasterDraws avg={avgShadowCasters:F0} max={_maxShadowCasters} " +
                $"shadowShareOfDraws={shadowDrawShare:P1} | " +
                $"shadowCpuMs avg={avgShadowMs:F3} ({shadowCpuShare:P1} of frame, marker={shadowMarker}) | " +
                $"tris avg={avgTrisM:F2}M verts avg={avgVertsM:F2}M | gcPerFrame avg={avgGcKb:F2}KB");
        }
    }
}
