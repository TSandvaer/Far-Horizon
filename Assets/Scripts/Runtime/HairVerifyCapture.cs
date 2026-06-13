using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build FIXED-ORBIT capture of the castaway's HAIR SILHOUETTE at the
    /// TILT-TO-HORIZON camera angle (ticket 86ca8ce6y SOAKFIX3). Sibling of AxeVerifyCapture /
    /// CastawayVerifyCapture — but deliberately NOT a subject-fit close-up.
    ///
    /// Why this exists: the Sponsor's "brown SPIKE on top" of the hair is only visible at the LOW
    /// (tilt-to-horizon) orbit pitch, where the crown reads as a silhouette against the sky. The existing
    /// -verifyCastaway is a head-to-feet FRONT CLOSE-UP that frames the subject at a fixed apparent size
    /// regardless of the real gameplay scale/angle — so it cannot validate a SILHOUETTE-at-the-horizon
    /// problem (unity-conventions.md §Editor-vs-runtime "Visibility gates need a FIXED-ORBIT capture, not a
    /// subject-fit close-up" — the -verifyAxe false-green precedent). This capture rides the REAL gameplay
    /// OrbitCamera (its post + lighting + the actual 14u distance) and drives it to the tilt-to-horizon
    /// pitch aimed at the crown — gameplay-representative AND fixed-orbit (deterministic pitch/distance).
    ///
    /// It does NOT touch gameplay: it finds the live OrbitCamera, drives it (SetYaw/SetPitch/SetDistance)
    /// to a known low-pitch framing of the castaway's head, lets the orbit + skinning settle, and shoots
    /// one PNG. Inert unless launched with -verifyHair. MUST run WINDOWED (ScreenCapture needs a real
    /// swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHair -captureDir &lt;dir&gt;
    /// Captures: hair_tilt.png (the crown silhouette at the tilt-to-horizon angle). Quits non-zero if no
    /// OrbitCamera or no castaway avatar is found (the build-side regression signals).
    /// </summary>
    public class HairVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // The tilt-to-horizon pitch the Sponsor sees the spike at (low = looking near-level). Inside the
        // OrbitCamera band [minPitch 8, maxPitch 70]; 20 frames the crown against the sky without grazing
        // the degenerate horizontal.
        public float tiltPitch = 20f;
        // Gameplay orbit distance (matches OrbitCamera.distance 14u). Fixed-orbit, NOT zoom-to-fit.
        public float orbitDistance = 14f;
        // Yaw so the crown reads three-quarter-front (the head + fringe both visible against the sky).
        public float viewYaw = 25f;

        void Start()
        {
            if (HasArg("-verifyHair"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var orbit = Object.FindAnyObjectByType<OrbitCamera>(FindObjectsInactive.Include);
            if (orbit == null)
            {
                Debug.LogError("[HairVerifyCapture] no OrbitCamera in scene — cannot frame the gameplay " +
                               "orbit (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            var castaway = Object.FindAnyObjectByType<CastawayCharacter>(FindObjectsInactive.Include);
            if (castaway == null)
            {
                Debug.LogError("[HairVerifyCapture] no CastawayCharacter in scene — nothing to frame the " +
                               "hair on (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Pin the body facing so the crown/fringe read deterministically (front = +Z by construction,
            // per ChibiFrontDiag — same convention CastawayVerifyCapture uses).
            castaway.FaceWorldYawInstant(0f);

            // Drive the REAL gameplay orbit camera to the tilt-to-horizon framing: gameplay distance, the
            // low tilt pitch, a three-quarter yaw. This is the FIXED-ORBIT (not zoom-to-fit) framing —
            // the crown reads at its true gameplay apparent size at the angle the Sponsor sees the spike.
            orbit.SetDistance(orbitDistance);
            orbit.SetYaw(viewYaw);
            orbit.SetPitch(tiltPitch);

            // Let the orbit follow + skinning + post settle (the orbit Lerps toward the target each frame).
            for (int i = 0; i < 20; i++) { orbit.SetPitch(tiltPitch); yield return null; }
            yield return new WaitForEndOfFrame();

            Debug.Log($"[HairVerifyCapture] orbit pitch={orbit.Pitch:F1} yaw={orbit.Yaw:F1} " +
                      $"dist={orbit.Distance:F1} (tilt-to-horizon fixed-orbit framing of the crown)");

            string file = Path.Combine(dir, "hair_tilt.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[HairVerifyCapture] wrote " + file + " (crown silhouette, tilt-to-horizon)");
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[HairVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
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
