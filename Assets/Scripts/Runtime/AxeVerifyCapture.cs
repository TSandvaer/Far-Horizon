using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the hero axe (ticket 86ca8ce6y; framing
    /// HARDENED in 86ca8fevz). Sibling of CastawayVerifyCapture / ChopVerifyCapture.
    ///
    /// The axe is the SOURCED rustic hatchet HELD in the chibi's right hand. This committed path frames
    /// the held hatchet close so the silhouette / leather-wrap / blade-forward read rides repeatable
    /// committed shipped-build evidence — a reviewer can re-run + judge their own artifact.
    ///
    /// HARDENING (86ca8fevz) — the close-up was UNRELIABLE evidence (PR #29 NIT): the held axe rendered
    /// CLIPPED at the top frame edge, TINY, with the torso/legs filling ~80% of the shot — so it could not
    /// substantiate "reads unmistakably as an axe". Root cause: the camera was seated too low/close on the
    /// far side of the body, so the torso occluded the prop and the axe rode the frame edge. FIXES:
    ///   - Frame from the axe's SETTLED, encapsulated WORLD renderer bounds (not a fixed magic camera, not
    ///     a Mathf.Max floor), fitting the FULL hatchet with HEADROOM (spans ~70% of the frame) — the head,
    ///     blade and haft are all in-frame with margin, never clipping the edge.
    ///   - View from the side of the axe AWAY FROM THE BODY (derive the body->axe direction from the
    ///     castaway's bounds center) so the torso sits BEHIND the camera and can never occlude the prop.
    ///   - Settle a VALID bounds before framing (the renderer must be shown + one frame elapsed) — fail
    ///     loud if the bounds stay degenerate rather than ship a wrong crop.
    ///   - Post-ENABLED render path (Skybox clear + Zone-D post Volume + SMAA) matching the gameplay
    ///     camera, so the capture's exposure/grade is gameplay-representative, not an isolated flat-lit rig.
    ///
    /// It does NOT touch gameplay: it finds the HeroAxe, FORCE-SHOWS its renderers (the held axe is
    /// HasAxe-gated and hidden at spawn — verification needs it visible), parks a dedicated post-enabled
    /// capture camera framing the whole hatchet, and captures axe_closeup.png. Inert unless launched with
    /// -verifyAxe. MUST run WINDOWED (ScreenCapture needs a real swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyAxe -captureDir &lt;dir&gt;
    /// Captures: axe_closeup.png (the held hatchet, framed close). Quits non-zero if the axe was not found
    /// in the scene, or if its bounds never settle to a valid size (the build-side failure signals).
    /// </summary>
    public class AxeVerifyCapture : MonoBehaviour
    {
        // Name of the serialized hero-axe GameObject (matches MovementCameraScene.HeroAxeObjectName).
        public const string HeroAxeName = "HeroAxe";
        public string subDir = "Captures";
        // Frame fill: the hatchet's dominant extent spans this fraction of the frame (0.62 = generous
        // margin so the full head+blade+haft read, nothing clips the edge — the NIT-A fix).
        public float frameFill = 0.62f;

        void Start()
        {
            // SOAKFIX8 (86ca8ce6y FIX1): -verifyAxeFacings drives the held axe through MULTIPLE character
            // facings (rotating the player root) and captures each, so the shipped-build artifact VISUALLY
            // proves the axe ROTATION TRACKS THE HAND through turns (the Sponsor's "points the same way on X
            // always" bug). The PlayMode test pins the invariant deterministically; this is the eyes-on
            // committed evidence. Distinct flag so the default -verifyAxe close-up is unchanged.
            if (HasArg("-verifyAxeFacings"))
                StartCoroutine(RunFacingsVerification());
            else if (HasArg("-verifyAxe"))
                StartCoroutine(RunVerification());
        }

        // SOAKFIX8 FIX1 — capture the held axe at several distinct character facings to prove the axe turns
        // WITH the hand (rotation tracks the bone). Rotates the player root through yaws, framing the held axe
        // from a FIXED gameplay-style three-quarter view each time, so a viewer can see the haft re-orient
        // with the body across the frames (the bug = the haft stayed pinned on X regardless of facing).
        private IEnumerator RunFacingsVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            GameObject axe = FindHeroAxe();
            if (axe == null)
            {
                Debug.LogError("[AxeVerifyCapture] HeroAxe not in scene — cannot capture facings");
                yield return null; Application.Quit(1); yield break;
            }
            foreach (var r in axe.GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = true;

            var castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            // The player root is the rotating body the click-move re-faces; rotate THAT so the hand bone (and
            // thus the held axe) re-orients exactly as it does in gameplay turns.
            Transform playerRoot = castaway != null ? FindPlayerRoot(castaway.transform) : null;
            if (playerRoot == null)
            {
                Debug.LogError("[AxeVerifyCapture] no player root found to rotate — cannot capture facings");
                Application.Quit(1); yield break;
            }

            var camGo = new GameObject("AxeFacingsCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f);
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            float[] facings = { 0f, 90f, 180f, 270f };
            float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            for (int n = 0; n < facings.Length; n++)
            {
                playerRoot.rotation = Quaternion.Euler(0f, facings[n], 0f);
                // Let the pose/skinning settle so the hand bone (and the axe riding it) is at the new facing.
                for (int i = 0; i < 6; i++) yield return null;

                Bounds wb = EncapsulateRenderers(axe.GetComponentsInChildren<Renderer>(true));
                if (wb.size.magnitude < 0.02f) { yield return null; wb = EncapsulateRenderers(axe.GetComponentsInChildren<Renderer>(true)); }
                // A FIXED world-space three-quarter view (NOT body-relative) so the axe's re-orientation is
                // visible BETWEEN frames — if the axe tracks the hand, the haft direction changes frame to
                // frame; if it were world-pinned (the bug) it would look identical across facings.
                Vector3 viewDir = new Vector3(0.6f, 0.45f, -0.8f);
                var frame = VerifyCaptureFraming.ComputeFrame(wb.center, wb.size, viewDir, 40f, aspect, frameFill);
                camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);

                for (int i = 0; i < 6; i++) yield return null;
                yield return new WaitForEndOfFrame();
                string file = Path.Combine(dir, $"axe_facing_{(int)facings[n]:000}.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                Debug.Log($"[AxeVerifyCapture] facing {facings[n]}° -> {file} (axe localEuler={axe.transform.localEulerAngles:F1})");
                yield return new WaitForEndOfFrame();
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[AxeVerifyCapture] facings verification complete -> " + dir);
            Application.Quit(0);
        }

        // The player ROOT is the click-move body that re-faces (carries the NavMeshAgent/ClickToMove); walk up
        // from the castaway avatar to the topmost transform under the scene root.
        private static Transform FindPlayerRoot(Transform avatar)
        {
            Transform t = avatar;
            while (t.parent != null) t = t.parent;
            return t;
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

            // Settle a VALID world bounds for the held hatchet. The axe rides the hand bone (an animated,
            // scaled transform), so frame from its WORLD renderer bounds — pose- and scale-robust. Wait for
            // the renderer to be live + the bounds to be a real prop size (NOT a Mathf.Max floor on invalid
            // bounds). Fail loud if it never settles.
            Bounds wb = new Bounds();
            bool valid = false;
            for (int attempt = 0; attempt < 30 && !valid; attempt++)
            {
                yield return null; // let the force-show + skinning + pose apply this frame
                wb = EncapsulateRenderers(rendersToShow);
                valid = wb.size.magnitude > 0.02f; // a real held hatchet, not a degenerate frame
            }
            if (!valid)
            {
                Debug.LogError($"[AxeVerifyCapture] held-axe bounds never settled to a valid size " +
                               $"(last size={wb.size}) — would frame a wrong crop; failing loud");
                Application.Quit(1);
                yield break;
            }

            // View from the side of the axe AWAY FROM THE BODY so the torso can NEVER occlude the prop (the
            // NIT-A occlusion fix). Derive the body->axe planar direction from the castaway's bounds center;
            // extend past the axe + lift above so the silhouette reads three-quarter. Robust to facing/pose.
            Vector3 bodyCenter = wb.center + Vector3.up * 0.2f; // fallback if no body found
            var castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            if (castaway != null)
            {
                var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null) bodyCenter = smr.bounds.center;
            }
            Vector3 awayFromBody = wb.center - bodyCenter; awayFromBody.y = 0f;
            if (awayFromBody.sqrMagnitude < 0.0001f) awayFromBody = Vector3.right;
            Vector3 viewDir = awayFromBody.normalized + Vector3.up * 0.45f; // from subject toward camera

            float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var frame = VerifyCaptureFraming.ComputeFrame(wb.center, wb.size, viewDir, 40f, aspect, frameFill);

            var camGo = new GameObject("AxeCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            // POST-ENABLED render path matching the gameplay camera (gameplay-representative grade, not an
            // isolated flat-lit rig): render the Zone-D post Volume + SMAA. The hatchet's lighting is
            // representative via RenderSettings ambient (skybox), INDEPENDENT of the clear flag. We clear to
            // a NEUTRAL solid backdrop (not the bright warm skybox, which R-clips to cream on a tight
            // close-up and reads as "blown") so the prop reads against a clean background.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f); // neutral slate — non-blown, frames the prop
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Debug.Log($"[AxeVerifyCapture] cam at {frame.position} looking at {frame.lookAt} " +
                      $"(bodyCenter={bodyCenter} bounds center={wb.center} size={wb.size} " +
                      $"dist={frame.distance:F2} fill={frameFill:F2})");

            // Let the frame settle (lighting/post a few frames) before the shot.
            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(dir, "axe_closeup.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[AxeVerifyCapture] wrote " + file + " (held hatchet, deterministic framing)");
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[AxeVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        // Encapsulate the world bounds of all live MeshRenderers (the held hatchet's geometry).
        private static Bounds EncapsulateRenderers(Renderer[] renderers)
        {
            Bounds b = new Bounds();
            bool init = false;
            foreach (var r in renderers)
            {
                if (r == null || !(r is MeshRenderer) || !r.enabled) continue;
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private GameObject FindHeroAxe()
        {
            // Include inactive so a present-but-disabled axe still resolves (and a truly-absent one fails).
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
