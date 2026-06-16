using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the LIVE FLOAT-DIAGNOSTIC instrument (ticket 86ca8rdkp).
    /// Sibling of MovementVerifyCapture / CastawayVerifyCapture.
    ///
    /// Why this exists: the float-diagnostic overlay is the deliverable, and per the shipped-build visual-
    /// verification gate (unity-conventions.md §editor-vs-runtime; IMGUI/overlay rendering is exactly the
    /// editor-vs-runtime class) the overlay must be PROVEN to render the live GAP in the BUILT exe, not just
    /// the editor. This component drives the player SHOREWARD (toward the dipping foreshore where the float was
    /// worst — the −Z shore per the project's water-grid lore), force-shows the F8 overlay (the harness can't
    /// synthesize an F8 key-down), and captures a GAMEPLAY-ORBIT frame with the live feet/ground/GAP readout
    /// visible alongside the HUD build stamp. It ALSO dumps the same [FloatTrace] ground-truth line so the
    /// orchestrator reads the GAP from the log too.
    ///
    /// Inert unless launched with -verifyFloatDiag (a normal soak / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyFloatDiag -captureDir &lt;dir&gt;
    /// Captures: floatdiag_spawn.png (overlay at spawn) + floatdiag_shore.png (overlay after walking shoreward,
    /// where the foreshore dips), then quits. MUST run WINDOWED (ScreenCapture needs a real swapchain).
    /// </summary>
    public class FloatDiagnosticVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Shoreward destination relative to spawn — toward the −Z foreshore where the visible terrain dips
        // below the NavMesh slab (the worst-float zone per the ground-trace). A real distance across the beach.
        public Vector3 shoreDestination = new Vector3(0f, 0f, -10f);

        void Start()
        {
            if (HasArg("-verifyFloatDiag"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var diag = Object.FindAnyObjectByType<FloatDiagnostic>(FindObjectsInactive.Include);
            var castaway = Object.FindAnyObjectByType<CastawayCharacter>(FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<ClickToMove>();
            if (diag == null)
            {
                Debug.LogError("[FloatDiagnosticVerifyCapture] no FloatDiagnostic in scene — the instrument is " +
                               "missing from Boot.unity (build-side regression signal)");
                yield return null; Application.Quit(1); yield break;
            }
            if (castaway == null)
            {
                Debug.LogError("[FloatDiagnosticVerifyCapture] no CastawayCharacter in scene");
                yield return null; Application.Quit(1); yield break;
            }

            // Force the overlay ON (no F8 key-down available headless) so the live GAP renders into the frame.
            diag.ShowOverlay();

            // Wait for the agent to land on the NavMesh so the snap has a real ground to measure against.
            float t = 0f;
            while (t < 3f && (player == null || player.Agent == null || !player.Agent.isOnNavMesh))
            { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log("[FloatDiagnosticVerifyCapture] agent on NavMesh: " +
                      (player != null && player.Agent != null && player.Agent.isOnNavMesh) + " after " +
                      t.ToString("0.00") + "s");

            // Let the snap + overlay settle, dump the ground-truth line, capture the spawn frame.
            for (int i = 0; i < 8; i++) yield return null;
            LogTrace(castaway, "spawn");
            Shot(Path.Combine(dir, "floatdiag_spawn.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // Walk SHOREWARD into the dipping foreshore (the worst-float zone) so the capture proves the GAP
            // reads correctly WHILE MOVING + after arrival on the dipped sand.
            Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
            Vector3 target = spawn + shoreDestination;
            bool set = player != null && player.MoveTo(target);
            Debug.Log("[FloatDiagnosticVerifyCapture] MoveTo shore set=" + set + " target=" + target);

            float start = Time.time;
            while (Time.time - start < 10f)
            {
                if (player != null && player.Agent != null && !player.Agent.pathPending)
                {
                    float planar = Vector2.Distance(
                        new Vector2(player.transform.position.x, player.transform.position.z),
                        new Vector2(target.x, target.z));
                    if (planar <= 0.6f) break;
                }
                yield return null;
            }

            for (int i = 0; i < 8; i++) yield return null;
            LogTrace(castaway, "shore");
            Shot(Path.Combine(dir, "floatdiag_shore.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[FloatDiagnosticVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        private static void LogTrace(CastawayCharacter c, string where)
        {
            Debug.Log($"[FloatTrace] ({where}) feetY={Fmt(c.FeetWorldY)} groundY={Fmt(c.GroundHitWorldY)} " +
                      $"GAP={Fmt(c.FloatGap)} meshBottomY={Fmt(c.MeshBottomWorldY)} meshGAP={Fmt(c.MeshFloatGap)} " +
                      $"offset={c.groundYOffset:F4} moving={c.IsMovingForSnap} snapRate={c.ActiveSnapRate:F0}");
            // FULL one-frame ground-truth dump — every Y reference, so the hidden proxy-vs-rendered-sole offset
            // is MEASURED (86ca8rdkp breakthrough). This is the load-bearing diagnostic line.
            c.DumpGroundTruth(where);
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");

        private void Shot(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[FloatDiagnosticVerifyCapture] wrote " + file);
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
