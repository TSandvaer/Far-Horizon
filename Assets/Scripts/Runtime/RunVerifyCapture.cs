using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for RUN-ON-SHIFT (ticket 86ca9yq34).
    ///
    /// Sibling of WasdVerifyCapture (86ca9yq2x): the testing bar's shipped-build capture gate
    /// (unity-conventions.md §editor-vs-runtime) requires proving the UX-visible RUN feature works in the
    /// BUILT exe, not just the editor. A real Shift keystroke can't be injected into a scripted/windowed
    /// build, so this drives the input-independent seams (WasdMovement.SetInputOverride for "W" +
    /// SetSprintOverride(true) for Shift — the EXACT same Update path a real keypress drives). It captures
    /// from the REAL OrbitCamera (the gameplay framing — an isolated rig is the false-green class,
    /// unity-conventions.md §"capture must use the GAMEPLAY camera") so the orchestrator/QA judge the run
    /// the way the player sees it.
    ///
    /// CAPTURES three frames, then quits:
    ///   run_walk.png  — moving WITHOUT Shift (walk speed, Walk anim) — the contrast baseline.
    ///   run_mid.png   — moving WITH Shift held (RUN speed, Run anim) — the run cycle from the gameplay cam.
    ///   run_after.png — settled after release (back to walk/idle).
    /// and LOGS, from ground truth, the run-vs-walk evidence the brief asks for:
    ///   - the walk speed vs the run speed the agent actually reached (run strictly faster — AC1),
    ///   - WasdMovement.IsSprinting + CastawayCharacter.IsRunning flipping true under Shift,
    ///   - the live grounding gap (CastawayCharacter.FloatGap / MeshBottomWorldY − GroundHitWorldY) WHILE
    ///     RUNNING (feet planted on the visible terrain through the run cycle — AC3),
    ///   - the held-axe seat is owned by HeldAxeRig (raw-hand-follow, OOS here) — logged for the QA cross-check.
    ///
    /// Inert unless launched with -verifyRun (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyRun -captureDir &lt;dir&gt;
    /// </summary>
    public class RunVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // Real wall-clock hold windows (headless deltas are ~0; sample over Time.time, never per-frame).
        public float walkSeconds = 2.5f;
        public float runSeconds = 3.5f;

        void Start()
        {
            if (HasArg("-verifyRun"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            NavMeshAgent agent = player != null ? player.GetComponent<NavMeshAgent>() : null;
            CastawayCharacter castaway = Object.FindAnyObjectByType<CastawayCharacter>();

            // 1. Wait up to 3s for the agent to land on the NavMesh.
            float t = 0f;
            while (t < 3f && (agent == null || !agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[RunVerifyCapture] agent on NavMesh: " + (agent != null && agent.isOnNavMesh) +
                      " after " + t.ToString("0.00") + "s");
            for (int i = 0; i < 5; i++) yield return null;

            // 2. WALK first (forward, no Shift) — the contrast baseline + the walk speed reference.
            if (player != null) { player.SetInputOverride(new Vector2(0f, 1f)); player.SetSprintOverride(false); }
            float walkPeak = 0f;
            float start = Time.time;
            while (Time.time - start < walkSeconds)
            {
                if (agent != null) walkPeak = Mathf.Max(walkPeak, Planar(agent.velocity));
                yield return null;
            }
            Debug.Log($"[RunVerifyCapture] WALK peak agent speed={walkPeak:F2}u/s IsSprinting={Sprinting()} " +
                      $"IsRunning={Running(castaway)} IsWalking={(castaway != null && castaway.IsWalking)}");
            ShotTo(Path.Combine(dir, "run_walk.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 3. RUN — keep forward, now HOLD Shift. Capture the run cycle from the gameplay cam.
            if (player != null) player.SetSprintOverride(true);
            float runPeak = 0f, worstGap = float.NegativeInfinity, bestGapValidFrames = 0f;
            start = Time.time;
            float lastShot = -1f;
            while (Time.time - start < runSeconds)
            {
                if (agent != null) runPeak = Mathf.Max(runPeak, Planar(agent.velocity));
                if (castaway != null && !float.IsNaN(castaway.FloatGap))
                {
                    worstGap = Mathf.Max(worstGap, castaway.FloatGap);
                    bestGapValidFrames++;
                }
                // Shoot the run frame ~midway through the hold (settled into the run cycle + the cam followed).
                if (lastShot < 0f && Time.time - start > runSeconds * 0.55f)
                {
                    ShotTo(Path.Combine(dir, "run_mid.png"));
                    lastShot = Time.time;
                }
                yield return null;
            }
            Debug.Log($"[RunVerifyCapture] RUN peak agent speed={runPeak:F2}u/s (walk was {walkPeak:F2}) " +
                      $"IsSprinting={Sprinting()} IsRunning={Running(castaway)} " +
                      $"runStrictlyFaster={(runPeak > walkPeak + 1f)} " +
                      $"worstFloatGapWhileRunning={Fmt(worstGap)}u (~0 = feet planted on the visible sand, AC3) " +
                      $"validGapFrames={bestGapValidFrames:F0} " +
                      $"meshBottomY={Fmt(castaway != null ? castaway.MeshBottomWorldY : float.NaN)} " +
                      $"groundHitY={Fmt(castaway != null ? castaway.GroundHitWorldY : float.NaN)}");
            if (lastShot < 0f) ShotTo(Path.Combine(dir, "run_mid.png")); // defensive: ensure the run frame exists

            // 4. RELEASE — back to walk/idle.
            if (player != null) { player.ClearSprintOverride(); player.ClearInputOverride(); }
            if (agent != null && agent.isOnNavMesh) agent.velocity = Vector3.zero;
            for (int i = 0; i < 10; i++) yield return null;
            Debug.Log($"[RunVerifyCapture] RELEASED Shift+input: IsSprinting={Sprinting()} " +
                      $"IsRunning={Running(castaway)} (must be false — back to walk/idle, AC1).");
            ShotTo(Path.Combine(dir, "run_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[RunVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        private float Planar(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        private bool Sprinting() => player != null && player.IsSprinting;
        private bool Running(CastawayCharacter c) => c != null && c.IsRunning;
        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[RunVerifyCapture] wrote " + file);
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
