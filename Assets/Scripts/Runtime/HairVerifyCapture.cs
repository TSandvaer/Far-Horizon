using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build FIXED-ORBIT capture of the castaway's HAIR CROWN from the DEFAULT
    /// OVER-THE-SHOULDER GAMEPLAY camera angle + multiple angles (ticket 86ca8ce6y SOAKFIX4). Sibling of
    /// AxeVerifyCapture / CastawayVerifyCapture — but deliberately NOT a subject-fit close-up.
    ///
    /// Why this exists / WHY THE ANGLE CHANGED (SOAKFIX4 — the false-green miss): SOAKFIX3 captured the crown
    /// at a near-LEVEL tilt-to-horizon pitch (20°) and PASSED while a spike remained. The Sponsor sees the
    /// "brown spike" from the DEFAULT GAMEPLAY camera — the OVER-THE-SHOULDER view looking DOWN at the crown
    /// (OrbitCamera.defaultPitch 55°, up toward maxPitch 70°), NOT from a front-level tilt. A vertex standing
    /// proud of the dome pokes the TOP-DOWN silhouette but is hidden behind the dome curve from a level/front
    /// view — the exact wrong-angle false-green the -verifyAxe close-up class bit before. FIX: capture the
    /// crown from the DOWN-LOOKING gameplay pitch (the Sponsor's screenshot-1 angle) AND from multiple yaws
    /// (front / three-quarter / behind) + a steep top-down, so a proud apex can't hide behind the dome from
    /// any angle. Rides the REAL gameplay OrbitCamera (its post + lighting + the actual 14u distance) at a
    /// fixed orbit (deterministic pitch/distance), so the crown reads at its true gameplay apparent size.
    ///
    /// It does NOT touch gameplay: it finds the live OrbitCamera, drives it (SetYaw/SetPitch/SetDistance)
    /// to each known framing of the castaway's head, lets the orbit + skinning settle, and shoots one PNG
    /// per angle. Inert unless launched with -verifyHair. MUST run WINDOWED (ScreenCapture needs a real
    /// swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHair -captureDir &lt;dir&gt;
    /// Captures: hair_overshoulder.png (the DEFAULT gameplay down-at-crown angle — the load-bearing shot),
    /// hair_topdown.png, hair_behind.png, hair_front.png (multi-angle confirmation). Quits non-zero if no
    /// OrbitCamera or no castaway avatar is found (the build-side regression signals).
    /// </summary>
    public class HairVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Gameplay orbit distance (matches OrbitCamera.distance 14u). Fixed-orbit, NOT zoom-to-fit. The
        // crown is small at this distance — but that IS the Sponsor's real view; a proud spike pokes here.
        public float orbitDistance = 14f;
        // The DEFAULT over-the-shoulder gameplay pitch — looking DOWN at the crown (OrbitCamera.defaultPitch).
        // This is the load-bearing angle: it is exactly what the Sponsor sees the spike from.
        public float gameplayPitch = 55f;
        // A steeper near-top-down pitch (toward OrbitCamera.maxPitch 70) for the harshest crown-spike check.
        public float topDownPitch = 70f;

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

            orbit.SetDistance(orbitDistance);

            // The LOAD-BEARING shot FIRST: the DEFAULT over-the-shoulder gameplay angle (down at the crown,
            // three-quarter-front yaw) — the Sponsor's screenshot-1 angle where the spike showed.
            yield return Capture(orbit, dir, "hair_overshoulder.png", 25f, gameplayPitch);
            // Multi-angle confirmation so a proud apex can't hide behind the dome from any side.
            yield return Capture(orbit, dir, "hair_topdown.png", 25f, topDownPitch);  // harshest crown view
            yield return Capture(orbit, dir, "hair_behind.png", 180f, gameplayPitch); // over the back of the head
            yield return Capture(orbit, dir, "hair_front.png", 0f, gameplayPitch);    // straight-on front

            Debug.Log("[HairVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        // Drive the gameplay orbit to (yaw,pitch) at the fixed gameplay distance, settle, and shoot one PNG.
        private IEnumerator Capture(OrbitCamera orbit, string dir, string fileName, float yaw, float pitch)
        {
            orbit.SetYaw(yaw);
            orbit.SetPitch(pitch);
            // Let the orbit follow + skinning + post settle (the orbit Lerps toward the target each frame).
            for (int i = 0; i < 16; i++) { orbit.SetPitch(pitch); orbit.SetYaw(yaw); yield return null; }
            yield return new WaitForEndOfFrame();
            Debug.Log($"[HairVerifyCapture] {fileName}: orbit pitch={orbit.Pitch:F1} yaw={orbit.Yaw:F1} " +
                      $"dist={orbit.Distance:F1}");
            string file = Path.Combine(dir, fileName);
            ScreenCapture.CaptureScreenshot(file, 1);
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.4f);
            Debug.Log("[HairVerifyCapture] wrote " + file);
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
