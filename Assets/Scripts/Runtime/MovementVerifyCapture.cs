using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the U3 movement+camera port (ticket 86ca86fme).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires
    /// proving click-to-move works in the BUILT exe, not just the editor. A plain boot-launch
    /// screenshot proves the scene renders, but NOT that the agent pathfinds — and the agent's
    /// first-frame "no valid NavMesh" is a documented init-order race that ClickToMove.EnsureOnNavMesh
    /// recovers from within ~2s. A 5-frame capture quits before that recovery, so it can't judge
    /// click-move. This hook waits for the agent to land on the NavMesh, programmatically drives
    /// MoveTo (the same seam a real click uses), waits for ARRIVAL, then captures — proving
    /// end-to-end click-move in the shipped player, with the HUD build stamp visible.
    ///
    /// Inert unless launched with -verifyMove (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyMove -captureDir &lt;dir&gt;
    /// Captures: move_before.png (at spawn) + move_after.png (at destination), then quits.
    /// </summary>
    public class MovementVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public string subDir = "Captures";

        // Destination relative to spawn — a real distance across the flat test ground.
        public Vector3 destination = new Vector3(12f, 0f, 9f);

        void Start()
        {
            if (HasArg("-verifyMove"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // 1. Wait up to 3s for the agent to be placed on the NavMesh (EnsureOnNavMesh retry).
            float t = 0f;
            while (t < 3f && (player == null || player.Agent == null || !player.Agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            bool onMesh = player != null && player.Agent != null && player.Agent.isOnNavMesh;
            Debug.Log("[MovementVerifyCapture] agent on NavMesh: " + onMesh +
                      " after " + t.ToString("0.00") + "s");

            // Let a few frames render so the 'before' shot has content.
            for (int i = 0; i < 5; i++) yield return null;
            Vector3 spawnPos = player != null ? player.transform.position : Vector3.zero;
            ShotTo(Path.Combine(dir, "move_before.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. Drive MoveTo (the input-independent seam a real LMB click also calls).
            bool set = player != null && player.MoveTo(spawnPos + destination);
            Debug.Log("[MovementVerifyCapture] MoveTo set destination: " + set +
                      " target=" + (spawnPos + destination));

            // 3. Wait for arrival (real wall-clock window; headless deltas are ~0).
            float start = Time.time;
            float planar = 999f;
            while (Time.time - start < 10f)
            {
                if (player != null)
                {
                    planar = Vector2.Distance(
                        new Vector2(player.transform.position.x, player.transform.position.z),
                        new Vector2(spawnPos.x + destination.x, spawnPos.z + destination.z));
                    if (!player.Agent.pathPending && planar <= 0.6f) break;
                }
                yield return null;
            }
            Debug.Log("[MovementVerifyCapture] final planar distance to destination: " +
                      planar.ToString("0.00") + " (<=0.6 means click-move REACHED the target)");

            // Let the camera settle on the moved player, then capture the 'after' shot.
            for (int i = 0; i < 8; i++) yield return null;
            ShotTo(Path.Combine(dir, "move_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[MovementVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[MovementVerifyCapture] wrote " + file);
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
