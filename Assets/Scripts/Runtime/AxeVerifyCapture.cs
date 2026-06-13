using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the hero axe (ticket 86ca8ce6y — RE-DONE).
    ///
    /// The axe is now the SOURCED rustic hatchet HELD in the chibi's right hand (no longer the retired
    /// procedural wedge resting in the stump). This committed path frames the held hatchet close so the
    /// silhouette / leather-wrap / blade-forward read rides repeatable committed shipped-build evidence
    /// (sibling of CraftVerifyCapture / MovementVerifyCapture). Carried from the PR #21/#26 NIT: a
    /// committed reproducible close-up so a reviewer can re-run + judge their own artifact.
    ///
    /// It does NOT touch gameplay: it finds the HeroAxe in the scene, FORCE-SHOWS its renderers (the
    /// held axe is HasAxe-gated and hidden at spawn — verification needs it visible), parks a dedicated
    /// capture camera in front of it framing the whole hatchet, and captures axe_closeup.png. The orbit
    /// gameplay camera is left untouched (we add our own camera so the shot is deterministic regardless
    /// of orbit follow state). Inert unless launched with -verifyAxe.
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyAxe -captureDir &lt;dir&gt;
    /// Captures: axe_closeup.png (the held hatchet, framed close). Quits non-zero if the axe was not
    /// found in the scene (the build-side failure signal — the serialized hero-axe geometry is missing).
    /// </summary>
    public class AxeVerifyCapture : MonoBehaviour
    {
        // Name of the serialized hero-axe GameObject (matches MovementCameraScene.HeroAxeObjectName).
        public const string HeroAxeName = "HeroAxe";
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyAxe"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the serialized hero axe (built editor-time into Boot.unity). Search inactive too so a
            // missing axe is a hard failure, not a silent black frame.
            GameObject axe = FindHeroAxe();
            bool found = axe != null;
            Debug.Log("[AxeVerifyCapture] hero axe found in scene: " + found);
            if (!found)
            {
                Debug.LogError("[AxeVerifyCapture] HeroAxe not in scene — the serialized hero-axe geometry " +
                               "is missing from Boot.unity (the build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // FORCE-SHOW the held axe: it is HasAxe-gated (HeldAxe) and hidden at spawn, so verification
            // would otherwise capture nothing. This is verification-only — gameplay visibility is untouched.
            var rendersToShow = axe.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rendersToShow) if (r != null) r.enabled = true;
            Debug.Log("[AxeVerifyCapture] force-showed " + rendersToShow.Length + " held-axe renderer(s) for the close-up");

            // Park a dedicated capture camera framing the WHOLE held hatchet. The axe rides the chibi's
            // hand bone (an animated, scaled transform), so we frame from its WORLD renderer bounds rather
            // than mesh-private constants — pose- and scale-robust. Approach from the front (world +Z) and
            // a touch to the side + above so the silhouette + leather-wrap + blade read three-quarter.
            Transform axeT = axe.transform;
            var mr0 = axe.GetComponentInChildren<MeshRenderer>();
            Bounds wb = mr0 != null ? mr0.bounds : new Bounds(axeT.position, Vector3.one * 0.5f);
            foreach (var r in rendersToShow)
                if (r is MeshRenderer && r != mr0) wb.Encapsulate(r.bounds);
            Vector3 lookAt = wb.center;
            float frameDist = Mathf.Max(wb.size.magnitude * 2.2f, 0.6f);

            var camGo = new GameObject("AxeCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f); // same deep-dusk neutral as the game cam
            cam.fieldOfView = 40f;                                // frame the whole hatchet so the silhouette reads
            // Place the camera on the FAR side of the axe FROM THE CHIBI BODY, looking back toward the body
            // — so the axe sits between camera and torso and the body can never occlude it (the prior fixed-
            // offset attempts kept putting the torso in front of the held axe). Derive the body->axe direction
            // from the player's body center; extend past the axe + lift above. Robust to facing/pose.
            Vector3 bodyCenter = lookAt + Vector3.up * 0.2f; // fallback if no body found
            var castaway = Object.FindAnyObjectByType<FarHorizon.CastawayCharacter>();
            if (castaway != null)
            {
                var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null) bodyCenter = smr.bounds.center;
            }
            Vector3 awayFromBody = (lookAt - bodyCenter); awayFromBody.y = 0f;
            if (awayFromBody.sqrMagnitude < 0.0001f) awayFromBody = Vector3.right;
            Vector3 camDir = (awayFromBody.normalized + Vector3.up * 0.55f).normalized;
            Vector3 camPos = lookAt + camDir * frameDist;
            camGo.transform.position = camPos;
            camGo.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            Debug.Log("[AxeVerifyCapture] capture cam at " + camPos + " looking at held axe " + lookAt +
                      " (bodyCenter " + bodyCenter + ", bounds size " + wb.size + ")");

            // Let the frame settle (lighting/post a few frames) before the shot.
            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(dir, "axe_closeup.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[AxeVerifyCapture] wrote " + file + " (bevel-edge close-up)");
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[AxeVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        private GameObject FindHeroAxe()
        {
            // Include inactive so a present-but-disabled axe still resolves (and a truly-absent one fails).
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == HeroAxeName) return t.gameObject;
            return null;
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
