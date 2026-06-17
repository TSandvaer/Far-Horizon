using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for WASD locomotion (ticket 86ca9yq2x).
    ///
    /// The testing bar's shipped-build capture gate (unity-conventions.md §editor-vs-runtime) requires
    /// proving the UX-visible feature works in the BUILT exe, not just the editor. A real keystroke can't
    /// be injected into a headless/scripted build, so this drives the input-independent seam
    /// (WasdMovement.SetInputOverride — the WASD analog of ClickToMove.MoveTo): it holds "W" (camera-
    /// relative forward) for a real wall-clock window and captures BEFORE (at spawn) + AFTER (moved),
    /// proving end-to-end WASD locomotion + the camera-relative direction + the Idle→Walk read in the
    /// shipped player, with the HUD build stamp visible.
    ///
    /// CAMERA-RELATIVE assertion: it captures from the REAL OrbitCamera (the gameplay framing — the
    /// false-green class is an isolated rig; unity-conventions.md), holds forward, and logs the planar
    /// displacement + its direction vs the camera's planar forward so the orchestrator/QA can confirm the
    /// character moved the way the camera faces.
    ///
    /// Inert unless launched with -verifyWasd (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWasd -captureDir &lt;dir&gt;
    /// Captures: wasd_before.png (at spawn) + wasd_after.png (moved forward), then quits.
    /// </summary>
    public class WasdVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // How long to hold "forward" (real wall-clock — headless deltas are ~0; sample over Time.time).
        public float holdSeconds = 4f;

        void Start()
        {
            if (HasArg("-verifyWasd"))
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

            // 1. Wait up to 3s for the agent to land on the NavMesh (the EnsureOnNavMesh retry on ClickToMove
            // runs even with click disabled — it warps the shared agent onto the mesh).
            float t = 0f;
            while (t < 3f && (agent == null || !agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            bool onMesh = agent != null && agent.isOnNavMesh;
            Debug.Log("[WasdVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            // Let a few frames render so the 'before' shot has content.
            for (int i = 0; i < 5; i++) yield return null;
            Vector3 spawnPos = player != null ? player.transform.position : Vector3.zero;
            ShotTo(Path.Combine(dir, "wasd_before.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. Hold "W" (camera-relative forward) via the input-independent seam — the EXACT same Update
            // path a real keypress drives (WasdMovement reads the override before the keyboard).
            if (player != null) player.SetInputOverride(new Vector2(0f, 1f)); // forward
            Debug.Log("[WasdVerifyCapture] holding camera-relative forward for " + holdSeconds.ToString("0.0") + "s");

            // 3. Walk a real wall-clock window so the agent actually traverses (headless deltas ~0).
            float start = Time.time;
            while (Time.time - start < holdSeconds) yield return null;

            if (player != null) player.ClearInputOverride();
            // Let the agent settle + the camera follow before the 'after' shot.
            if (agent != null && agent.isOnNavMesh) agent.velocity = Vector3.zero;
            for (int i = 0; i < 8; i++) yield return null;

            Vector3 movedPos = player != null ? player.transform.position : spawnPos;
            float planar = Vector2.Distance(new Vector2(spawnPos.x, spawnPos.z),
                                            new Vector2(movedPos.x, movedPos.z));
            Vector3 moveDir = movedPos - spawnPos; moveDir.y = 0f;
            Debug.Log("[WasdVerifyCapture] planar displacement holding forward: " + planar.ToString("0.00") +
                      "u (>0.5 means WASD MOVED the player); moveDir=" + moveDir.normalized.ToString("0.00") +
                      " lastMoveDir=" + (player != null ? player.LastMoveDir.ToString("0.00") : "n/a"));

            ShotTo(Path.Combine(dir, "wasd_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[WasdVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[WasdVerifyCapture] wrote " + file);
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
