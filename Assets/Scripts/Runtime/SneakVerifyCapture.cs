using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for SNEAK-WALK SMOOTHNESS (ticket 86caa3kur re-soak — the
    /// "sneak-walking is STUTTERY/JITTERY" fix). Sibling of RunVerifyCapture (86ca9yq34).
    ///
    /// WHY THIS EXISTS (the ground-truth instrument, not a blind re-iteration): the Sponsor soaked PR #197 and
    /// reported crouch-walk (Ctrl+WASD) as JITTERY at the slow sneak speed. EditMode/PlayMode can't observe the
    /// stutter (headless Time.deltaTime≈0 stalls physical traversal — the documented trap), so the smoothness
    /// must be measured from the BUILT exe's LIVE per-frame motion. This drives the input-independent seams
    /// (WasdMovement.SetInputOverride for "W" + SetCrouchOverride(true) for Ctrl — the EXACT same grounded
    /// Update path a real Ctrl+W drives) and MEASURES the per-frame root displacement, the smoothness ground
    /// truth: a STUTTER shows as a high frame-to-frame variance in the step size (the agent-sim braking fight
    /// the fix removes); SMOOTH motion shows a near-constant step (= speed × dt). It captures from the REAL
    /// OrbitCamera (the gameplay framing — an isolated rig is the false-green class) so the orchestrator/QA/
    /// Sponsor judge the sneak the way the player sees it.
    ///
    /// THE SMOOTHNESS METRIC (logged from ground truth): over the sneak hold, the per-frame planar root step is
    /// sampled every frame. A smooth sneak has step ≈ sneakSpeed × dt with a SMALL coefficient-of-variation
    /// (stdev/mean); the stutter inflates the CoV (steps jump big↔small as the agent sim cancels/re-applies the
    /// commanded velocity). We log mean/stdev/CoV/min/max + the worst single-frame ratio so the fix is judged on
    /// a NUMBER, not just an eyeball — AND capture frames so the visual read is there too.
    ///
    /// CAPTURES three frames, then quits:
    ///   sneak_walk.png  — moving WITHOUT Ctrl (normal walk) — the contrast baseline.
    ///   sneak_mid.png   — moving WITH Ctrl held (crouched sneak) — the sneak cycle from the gameplay cam.
    ///   sneak_after.png — settled after release (back to standing idle).
    ///
    /// 86caa3kur RE-SOAK — ANIMATION-LOOP HITCH INSTRUMENT (the point of attempt 2): the position layer was
    /// already fixed (Devon's smooth-direct-drive), but the Sponsor re-soaked and refined the symptom to "the
    /// crouch sneak-walk lags between each walk animation — two steps repeated, lags between each" = an ANIMATION
    /// LOOP hitch. So this capture ALSO dumps the LIVE per-frame layer-0 Animator ground truth during the sneak
    /// hold (state hash + clip name + normalizedTime + EFFECTIVE playback speed + in-transition + the #186
    /// LocoSpeedMul foot-sync multiplier) as ~10Hz [SneakTrace] lines, and the AnimLoopTrace discriminator names
    /// WHICH of three candidate causes the trace points at:
    ///   #1 clip loop-seam   — Sneak Walk.fbx last frame ≠ first frame (a per-cycle pop with a CLEAN loop).
    ///   #2 foot-sync stall  — the clip playback drives near-zero (RULED OUT in source: CrouchWalk has NO
    ///                         speedParameter, so LocoSpeedMul never scales it; the trace re-confirms empirically).
    ///   #3 state re-entry   — the AnyState→CrouchWalk/CrouchIdle transition flaps on a Moving flicker, restarting
    ///                         the clip from 0 each "two-step" cycle (normalizedTime resets / hash changes).
    /// -sneakNoFootSync is the candidate-#2 disconfirming CONTROL (CastawayCharacter forces footSync off at boot).
    ///
    /// Inert unless launched with -verifySneak (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySneak -captureDir &lt;dir&gt;
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySneak -sneakNoFootSync -captureDir &lt;dir&gt;  (the #2 control)
    /// </summary>
    public class SneakVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        // The avatar whose LIVE Animator state the loop-hitch trace reads (86caa3kur re-soak). Resolved in Start
        // (off the player, else found in the scene). The trace samples castaway.CurrentStateHash / normalizedTime
        // / effective-speed / inTransition to discriminate the loop hitch (seam vs foot-sync vs re-entry).
        public CastawayCharacter castaway;
        public string subDir = "Captures";

        // Real wall-clock hold windows (headless deltas are ~0; sample over Time.time, never per-frame).
        public float walkSeconds = 2.5f;
        public float sneakSeconds = 4.5f; // longer — gather enough per-frame steps for a stable CoV

        void Start()
        {
            if (HasArg("-verifySneak"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                // Resolve the avatar for the Animator loop-hitch trace: prefer the player's wired castaway, else
                // its child, else find one in the scene. Null-tolerant (the trace simply doesn't emit).
                if (castaway == null && player != null) castaway = player.castaway;
                if (castaway == null && player != null) castaway = player.GetComponentInChildren<CastawayCharacter>(true);
                if (castaway == null) castaway = Object.FindAnyObjectByType<CastawayCharacter>();
                StartCoroutine(SneakVerification());
            }
        }

        private IEnumerator SneakVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            NavMeshAgent agent = player != null ? player.GetComponent<NavMeshAgent>() : null;
            CastawayCharacter castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            Transform root = agent != null ? agent.transform : (player != null ? player.transform : null);

            // 1. Wait up to 3s for the agent to land on the NavMesh.
            float t = 0f;
            while (t < 3f && (agent == null || !agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[SneakVerifyCapture] agent on NavMesh: " + (agent != null && agent.isOnNavMesh) +
                      " after " + t.ToString("0.00") + "s");
            for (int i = 0; i < 5; i++) yield return null;

            // 2. WALK first (forward, no Ctrl) — the contrast baseline + the walk-smoothness reference.
            if (player != null) { player.SetInputOverride(new Vector2(0f, 1f)); player.SetCrouchOverride(false); }
            var walkStats = new StepStats();
            yield return SampleSteps(root, agent, walkSeconds, walkStats);
            Debug.Log($"[SneakVerifyCapture] WALK {walkStats.Describe("walk")} " +
                      $"IsCrouching={Crouching()} agentSpeed={Fmt(agent != null ? Planar(agent.velocity) : float.NaN)}");
            ShotTo(Path.Combine(dir, "sneak_walk.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 3. SNEAK — keep forward, now HOLD Ctrl. Measure the per-frame step smoothness + capture the cycle
            //    AND — the 86caa3kur RE-SOAK INSTRUMENT — dump the LIVE per-frame Animator ground truth so the
            //    LOOP-HITCH ("two steps repeated, lags between each") is DISCRIMINATED among the three candidates
            //    (clip loop-seam / foot-sync stall / state re-entry). The position layer was already fixed
            //    (Devon's smooth-direct-drive); the Sponsor's re-soak says the ANIMATION LOOP hitches, which only
            //    the live Animator state/clip/normalizedTime/effective-speed can reveal (headless can't tick it).
            if (player != null) player.SetCrouchOverride(true);
            var sneakStats = new StepStats();
            var animTrace = new AnimLoopTrace();
            bool noFootSync = HasArg("-sneakNoFootSync"); // candidate #2 disconfirming control (CastawayCharacter forced footSync off at boot)
            float lastShot = -1f;
            float traceClock = -1f; // ~10Hz trace cadence (not every frame — keep the log greppable + bounded GC)
            float start = Time.time;
            // Sample inline so we can shoot mid-window once the cam has settled into the sneak.
            Vector3 prev = root != null ? new Vector3(root.position.x, 0f, root.position.z) : Vector3.zero;
            bool prevValid = root != null;
            while (Time.time - start < sneakSeconds)
            {
                yield return null;
                if (root != null)
                {
                    Vector3 now = new Vector3(root.position.x, 0f, root.position.z);
                    if (prevValid) sneakStats.Add((now - prev).magnitude);
                    prev = now; prevValid = true;
                }
                // ANIMATOR LOOP TRACE — feed the live state into the discriminator + emit a ~10Hz [SneakTrace] line.
                if (castaway != null)
                {
                    animTrace.Sample(castaway.CurrentStateHash, castaway.CurrentStateNormalizedTime,
                                     castaway.CurrentStateEffectiveSpeed, castaway.IsInTransition);
                    if (traceClock < 0f || Time.time - traceClock >= 0.1f)
                    {
                        traceClock = Time.time;
                        Debug.Log($"[SneakTrace] t={(Time.time - start):F2} state={castaway.CurrentStateHash} " +
                                  $"clip={castaway.CurrentClipName ?? "<none>"} " +
                                  $"normTime={Fmt(castaway.CurrentStateNormalizedTime)} " +
                                  $"effSpeed={Fmt(castaway.CurrentStateEffectiveSpeed)} " +
                                  $"inTransition={castaway.IsInTransition} " +
                                  $"locoSpeedMul={Fmt(castaway.CurrentLocoSpeedMul)} " +
                                  $"moving={castaway.IsWalking} crouch={castaway.IsCrouching}");
                    }
                }
                if (lastShot < 0f && Time.time - start > sneakSeconds * 0.5f)
                {
                    ShotTo(Path.Combine(dir, "sneak_mid.png"));
                    lastShot = Time.time;
                }
            }
            if (lastShot < 0f) ShotTo(Path.Combine(dir, "sneak_mid.png")); // defensive: ensure the sneak frame exists
            Debug.Log($"[SneakVerifyCapture] SNEAK {sneakStats.Describe("sneak")} " +
                      $"IsCrouching={Crouching()} avatarCrouch={(castaway != null && castaway.IsCrouching)} " +
                      $"commandedSpeed={Fmt(player != null ? player.CurrentSpeed : float.NaN)}u/s " +
                      $"footSync={(castaway != null ? castaway.footSync.ToString() : "N/A")} noFootSyncArg={noFootSync} " +
                      $"SMOOTH={(sneakStats.IsSmooth ? "YES (low step variance)" : "NO — STUTTER (high step variance)")}");
            // THE LOOP-HITCH VERDICT — name which candidate the live Animator trace points at (the discriminator).
            Debug.Log("[SneakVerifyCapture] ANIM-LOOP " + animTrace.Describe());

            // 4. RELEASE — back to standing idle.
            if (player != null) { player.ClearCrouchOverride(); player.ClearInputOverride(); }
            if (agent != null && agent.isOnNavMesh) agent.velocity = Vector3.zero;
            for (int i = 0; i < 10; i++) yield return null;
            Debug.Log($"[SneakVerifyCapture] RELEASED Ctrl+input: IsCrouching={Crouching()} " +
                      $"(must be false — back to standing idle).");
            ShotTo(Path.Combine(dir, "sneak_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[SneakVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        // Sample the per-frame planar root step over a real Time.time window into the stats accumulator.
        private IEnumerator SampleSteps(Transform root, NavMeshAgent agent, float seconds, StepStats stats)
        {
            float start = Time.time;
            Vector3 prev = root != null ? new Vector3(root.position.x, 0f, root.position.z) : Vector3.zero;
            bool prevValid = root != null;
            while (Time.time - start < seconds)
            {
                yield return null;
                if (root == null) continue;
                Vector3 now = new Vector3(root.position.x, 0f, root.position.z);
                if (prevValid) stats.Add((now - prev).magnitude);
                prev = now; prevValid = true;
            }
        }

        /// <summary>Per-frame step accumulator — the SMOOTHNESS ground truth. A smooth move has a near-constant
        /// step (speed×dt); a stutter has a high coefficient-of-variation (stdev/mean). Logs the full picture so
        /// the fix is judged on a number, not an eyeball.</summary>
        private class StepStats
        {
            private int _n;
            private double _sum, _sumSq, _min = double.MaxValue, _max;

            public void Add(float step)
            {
                // Ignore the occasional zero-dt / pre-warp frame (a 0 step would deflate the mean spuriously).
                if (step <= 1e-6f) return;
                _n++; _sum += step; _sumSq += (double)step * step;
                if (step < _min) _min = step;
                if (step > _max) _max = step;
            }

            public float Mean => _n > 0 ? (float)(_sum / _n) : 0f;
            public float Stdev
            {
                get
                {
                    if (_n < 2) return 0f;
                    double mean = _sum / _n;
                    double var = Mathf.Max(0f, (float)(_sumSq / _n - mean * mean));
                    return (float)System.Math.Sqrt(var);
                }
            }
            // Coefficient of variation: stdev / mean. The scale-free stutter metric — small = smooth.
            public float Cov => Mean > 1e-6f ? Stdev / Mean : 0f;
            // Worst-frame ratio: the biggest single step relative to the mean (a stutter spikes this).
            public float MaxRatio => Mean > 1e-6f ? (float)(_max / Mean) : 0f;
            // SMOOTH if the step variance is low. A constant-speed grounded move (the fix) has CoV well under
            // ~0.25 (frame-time jitter alone); the agent-sim braking-fight stutter pushes CoV far higher.
            public bool IsSmooth => _n >= 8 && Cov < 0.35f && MaxRatio < 2.0f;

            public string Describe(string label) =>
                $"steps n={_n} meanStep={Mean:F5}u stdev={Stdev:F5}u CoV={Cov:F3} " +
                $"min={(_min == double.MaxValue ? 0f : (float)_min):F5}u max={(float)_max:F5}u maxRatio={MaxRatio:F2}";
        }

        /// <summary>ANIMATOR LOOP-HITCH discriminator (86caa3kur re-soak) — accumulates the live layer-0 Animator
        /// signals across the sneak hold + names WHICH of the three candidate causes the trace points at:
        ///   #3 STATE RE-ENTRY  — the state hash CHANGES mid-hold, OR normalizedTime JUMPS BACKWARD without the
        ///                        integer loop-count advancing (the loop didn't wrap — the state re-entered),
        ///                        OR transitions fire repeatedly during a steady hold. This is the prime suspect
        ///                        for "two steps repeated, lags between each": the AnyState→CrouchWalk/CrouchIdle
        ///                        flapping on a Moving flicker restarts the clip from 0 each cycle.
        ///   #1 CLIP LOOP-SEAM  — the state stays put + normalizedTime advances monotonically + wraps cleanly
        ///                        (no re-entry, no stall), so any visible per-cycle pop is a CLIP-AUTHORING seam
        ///                        (Sneak Walk.fbx last frame ≠ first frame). Confirm against the captured frames.
        ///   #2 FOOT-SYNC STALL — the effective playback speed drives near-zero. EXPECTED NOT to fire for
        ///                        CrouchWalk (no speedParameter → effSpeed≈1); a near-1 minEffSpeed CONFIRMS
        ///                        foot-sync doesn't reach the sneak clip (candidate #2 ruled out empirically).</summary>
        private class AnimLoopTrace
        {
            private int _n;
            private int _firstHash, _lastHash;
            private bool _haveFirst;
            private int _distinctHashChanges;     // times the state hash changed frame-to-frame (re-entry signal)
            private float _lastNormTime = float.NaN;
            private int _normTimeResets;          // times normalizedTime jumped BACKWARD w/o the loop-count rising
            private int _cleanWraps;              // times the fractional part wrapped 0.9x→0.0x WITH the loop advancing
            private int _inTransitionFrames;      // frames mid-transition during the hold (steady hold should be ~0)
            private float _minEffSpeed = float.MaxValue;
            private float _maxEffSpeed;

            public void Sample(int stateHash, float normTime, float effSpeed, bool inTransition)
            {
                _n++;
                if (!_haveFirst) { _firstHash = stateHash; _haveFirst = true; }
                if (stateHash != _lastHash && _lastHash != 0) _distinctHashChanges++;
                _lastHash = stateHash;

                if (inTransition) _inTransitionFrames++;

                if (!float.IsNaN(normTime))
                {
                    if (!float.IsNaN(_lastNormTime))
                    {
                        // A clean LOOP keeps the integer part rising (or fractional rising within a loop). A
                        // BACKWARD jump in normalizedTime WITHOUT the integer loop-count advancing = a reset
                        // (state re-entry, candidate #3); a backward fractional jump WITH the int part rising =
                        // a clean wrap (candidate #1 territory — the loop completed normally).
                        int lastLoop = Mathf.FloorToInt(_lastNormTime);
                        int curLoop = Mathf.FloorToInt(normTime);
                        if (normTime < _lastNormTime - 1e-3f)
                        {
                            if (curLoop > lastLoop) _cleanWraps++;   // wrapped, loop count rose → clean loop
                            else _normTimeResets++;                  // jumped back, loop count flat → RE-ENTRY
                        }
                    }
                    _lastNormTime = normTime;
                }

                if (!float.IsNaN(effSpeed))
                {
                    if (effSpeed < _minEffSpeed) _minEffSpeed = effSpeed;
                    if (effSpeed > _maxEffSpeed) _maxEffSpeed = effSpeed;
                }
            }

            // The named verdict — which candidate the trace points at (the discriminator the dispatch asked for).
            public string Verdict()
            {
                if (_n < 4) return "INCONCLUSIVE (too few samples)";
                bool reentry = _distinctHashChanges > 0 || _normTimeResets > 0 || _inTransitionFrames > _n / 4;
                bool footSyncStall = _minEffSpeed != float.MaxValue && _minEffSpeed < 0.2f;
                if (footSyncStall)
                    return "CANDIDATE #2 (FOOT-SYNC STALL) — effSpeed drove near-zero during the sneak";
                if (reentry)
                    return "CANDIDATE #3 (STATE RE-ENTRY) — the crouch state re-entered/transitioned each cycle " +
                           "(hash-changes/normTime-resets/in-transition) → the AnyState→CrouchWalk flap restarts the clip";
                return "CANDIDATE #1 (CLIP LOOP-SEAM) — state steady, normTime advanced + wrapped cleanly, effSpeed≈1 " +
                       "→ no re-entry/stall; any per-cycle pop is the Sneak Walk.fbx loop SEAM (confirm vs frames)";
            }

            public string Describe(string label = "sneak") =>
                $"samples={_n} distinctStateChanges={_distinctHashChanges} normTimeResets={_normTimeResets} " +
                $"cleanWraps={_cleanWraps} inTransitionFrames={_inTransitionFrames}/{_n} " +
                $"effSpeed[min={(_minEffSpeed == float.MaxValue ? 0f : _minEffSpeed):F3} max={_maxEffSpeed:F3}] " +
                $"firstHash={_firstHash} lastHash={_lastHash} => {Verdict()}";
        }

        private float Planar(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        private bool Crouching() => player != null && player.IsCrouching;
        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SneakVerifyCapture] wrote " + file);
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", subDir)
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", subDir);
            return Path.GetFullPath(baseDir);
        }

        private bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
