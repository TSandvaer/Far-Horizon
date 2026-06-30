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
    /// Inert unless launched with -verifySneak (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySneak -captureDir &lt;dir&gt;
    /// </summary>
    public class SneakVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // Real wall-clock hold windows (headless deltas are ~0; sample over Time.time, never per-frame).
        public float walkSeconds = 2.5f;
        public float sneakSeconds = 4.5f; // longer — gather enough per-frame steps for a stable CoV

        void Start()
        {
            if (HasArg("-verifySneak"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
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

            // 3. SNEAK — keep forward, now HOLD Ctrl. Measure the per-frame step smoothness + capture the cycle.
            if (player != null) player.SetCrouchOverride(true);
            var sneakStats = new StepStats();
            float lastShot = -1f;
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
                      $"SMOOTH={(sneakStats.IsSmooth ? "YES (low step variance)" : "NO — STUTTER (high step variance)")}");

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
