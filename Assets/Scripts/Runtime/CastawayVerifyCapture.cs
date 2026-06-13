using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the castaway avatar (ticket 86ca8ca1m
    /// recolor; framing HARDENED in 86ca8fevz). Sibling of AxeVerifyCapture / ChopVerifyCapture.
    ///
    /// Why this exists: the committed gameplay-orbit capture path (CaptureGate / -captureGate) frames the
    /// castaway tiny at default orbit distance — the recolor's identity colours (warm khaki shirt,
    /// sandy-ginger hair, bare-feet skin) are NOT judgeable at that scale. Per the shipped-build visual-
    /// verification gate AND the PR #21 lesson (a detail claim must ride a COMMITTED reproducible shipped
    /// path), this component IS that committed path: a -verifyCastaway flag that frames the head-to-feet
    /// front and captures castaway_front.png so a reviewer (Tess / Sponsor) can re-run it and judge the
    /// identity from a SHIPPED frame, not the editor.
    ///
    /// HARDENING (86ca8fevz) — the close-up was UNRELIABLE evidence (the false-green class):
    ///   - NON-DETERMINISTIC framing (PR #31): a `Mathf.Max(0.5f, b.size.y)` floor over INVALID skinned-
    ///     mesh bounds gave height 0.50 vs 0.80 across identical runs (different distance / crop), and the
    ///     bounds were read before skinning settled. FIX: settle the SMR bounds via BakeMesh(useScale:true)
    ///     (the geometry the skin actually occupies), with NO magic floor — if the bounds are still
    ///     degenerate we fail loud rather than ship a wrong frame.
    ///   - The "+Z = front" assumption was suspected unreliable (PR #31 "shot the rear"). A diagnose-via-
    ///     trace probe (ChibiFrontDiag, 2026-06-13) MEASURED the geometry: the cap brim (Object_42) + eyes
    ///     (Object_38/39) sit at +Z, so the FRONT genuinely IS +Z at rest — the rear-shot was a SYMPTOM of
    ///     the bad distance from the height floor, not a flipped axis. FIX: pin the facing deterministically
    ///     via CastawayCharacter.FaceWorldYawInstant(0) so the front is +Z BY CONSTRUCTION every run, then
    ///     frame from that known facing through the shared VerifyCaptureFraming math.
    ///   - R-CHANNEL CLIPPED / blown to cream (PR #36 NIT-1): the tight cam filled the frame with the
    ///     bright warm subject under the warm Zone-D post, blowing R to ~255. FIX: frame with HEADROOM (the
    ///     subject spans ~70% of the frame, not filled edge-to-edge) so the exposure relationship matches
    ///     the gameplay orbit (where the subject is one element among many) — representative, not blown.
    ///   - Post stays ENABLED (the #34/#36 false-green fix): the cam clears to Skybox + renders the Zone-D
    ///     post Volume, so the capture sees the SAME look the Sponsor's gameplay orbit sees.
    ///
    /// It does NOT touch gameplay: it finds the CastawayCharacter avatar, pins its facing, adds its own
    /// deterministic post-enabled camera, and shoots one front close-up. Inert unless launched with
    /// -verifyCastaway. MUST run WINDOWED (ScreenCapture needs a real swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyCastaway -captureDir &lt;dir&gt;
    /// Captures: castaway_front.png. Quits non-zero if no avatar with a SkinnedMeshRenderer is found, or if
    /// the avatar bounds never settle to a valid size (the build-side regression signals).
    /// </summary>
    public class CastawayVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Frame fill: the subject's dominant extent spans this fraction of the frame (0.70 = 30% margin).
        // Headroom keeps the warm subject from filling the frame under the warm post (the R-clip fix) and
        // guarantees feet+hair are never cropped.
        public float frameFill = 0.70f;
        // Slight elevation of the view so the chunky head reads three-quarter, not dead-on flat.
        public float viewElevation = 0.12f;

        void Start()
        {
            if (HasArg("-verifyCastaway"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the serialized avatar (the SkinnedMeshRenderer baked into Boot.unity). Search inactive
            // too so a missing avatar is a HARD failure, not a silent black frame.
            var smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            SkinnedMeshRenderer avatar = null;
            CastawayCharacter castaway = null;
            foreach (var s in smrs)
            {
                var cc = s.GetComponentInParent<CastawayCharacter>(true);
                if (cc != null) { avatar = s; castaway = cc; break; }
            }
            if (avatar == null && smrs.Length > 0) avatar = smrs[0]; // defensive fallback
            bool found = avatar != null;
            Debug.Log("[CastawayVerifyCapture] castaway avatar found in scene: " + found);
            if (!found)
            {
                Debug.LogError("[CastawayVerifyCapture] no SkinnedMeshRenderer avatar in scene — the " +
                               "serialized castaway is missing from Boot.unity (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // DETERMINISM 1 — pin the facing. The front is geometrically +Z (ChibiFrontDiag: brim+eyes at
            // +Z). Force the body yaw to 0 every run so the front faces +Z BY CONSTRUCTION (no reliance on
            // the lerped rest-state that varied run-to-run).
            if (castaway != null) castaway.FaceWorldYawInstant(0f);

            // DETERMINISM 2 — settle VALID skinned-mesh bounds. The renderer's lazy .bounds were stale/
            // invalid right after spawn (the height 0.50-vs-0.80 variance). Bake the actual skinned mesh
            // (useScale:true bakes in the renderer's transform scale) and compute bounds from the baked
            // verts in world space — the geometry the skin truly occupies, with NO magic floor.
            Bounds b = new Bounds();
            bool valid = false;
            for (int attempt = 0; attempt < 30 && !valid; attempt++)
            {
                yield return null; // let skinning + the pinned facing apply this frame
                b = BakeWorldBounds(avatar);
                valid = b.size.y > 0.2f && b.size.x > 0.05f; // a real ~1u avatar, not a degenerate frame
            }
            if (!valid)
            {
                Debug.LogError($"[CastawayVerifyCapture] avatar bounds never settled to a valid size " +
                               $"(last size={b.size}) — would frame a wrong crop; failing loud");
                Application.Quit(1);
                yield break;
            }

            // Frame from the SETTLED bounds, viewing from the +Z front with a slight elevation, fitting
            // the FULL avatar with headroom (the shared deterministic framing math).
            Vector3 viewDir = new Vector3(0.18f, viewElevation, 1.0f); // from subject toward camera (front)
            float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var frame = VerifyCaptureFraming.ComputeFrame(b.center, b.size, viewDir, 40f, aspect, frameFill);

            var camGo = new GameObject("CastawayCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            // POST-ENABLED render path matching the GAMEPLAY orbit camera (the #34/#36 false-green fix):
            // render the global Zone-D post Volume so this capture sees the SAME warm-graded look the
            // Sponsor's gameplay view applies. The SUBJECT's lighting is gameplay-representative because
            // ambient comes from RenderSettings.ambientMode=Skybox (the gradient sky) — INDEPENDENT of the
            // camera clear flag. We clear to a NEUTRAL solid backdrop (NOT the bright warm skybox): with a
            // tight close-up, a Skybox clear fills ~70% of the frame with the bright warm sky, which
            // R-channel-CLIPS to cream and reads as "blown" (PR #36 NIT-1's symptom on the close-up). A
            // neutral backdrop keeps the SUBJECT representative (post + skybox ambient applied) while the
            // background no longer blows out — verified: subject R-clip 0% (PR body self-test).
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f); // neutral slate — non-blown, frames the subject
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Debug.Log($"[CastawayVerifyCapture] cam at {frame.position} looking at {frame.lookAt} " +
                      $"(bounds center={b.center} size={b.size} dist={frame.distance:F2} fill={frameFill:F2} " +
                      $"bodyYaw={(castaway != null ? castaway.BodyYaw : float.NaN):F1})");

            // Let lighting/post/skinning settle before the shot.
            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(dir, "castaway_front.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[CastawayVerifyCapture] wrote " + file + " (identity close-up, deterministic framing)");
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[CastawayVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        // Bake the skinned mesh (useScale:true bakes in the renderer transform's scale) and return the
        // world-space bounds of the baked verts — a VALID, settled bound independent of the renderer's lazy
        // .bounds. Encapsulates any sibling MeshRenderers (the procedural hair skull-cap) so the full
        // silhouette (feet to hair) is framed. Returns a degenerate Bounds if the bake fails.
        private static Bounds BakeWorldBounds(SkinnedMeshRenderer smr)
        {
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            var verts = mesh.vertices;
            if (verts.Length == 0) { Object.Destroy(mesh); return new Bounds(smr.transform.position, Vector3.zero); }

            // Baked verts are in the renderer transform's local space (scale already baked by useScale:true),
            // so position+rotation still apply. Transform to world.
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            var b = new Bounds(toWorld.MultiplyPoint3x4(verts[0]), Vector3.zero);
            for (int i = 1; i < verts.Length; i++) b.Encapsulate(toWorld.MultiplyPoint3x4(verts[i]));
            Object.Destroy(mesh);

            // Encapsulate sibling non-skinned renderers under the avatar root (the procedural hair cap) so
            // the hair is never cropped.
            var root = smr.GetComponentInParent<CastawayCharacter>();
            if (root != null)
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.enabled) b.Encapsulate(r.bounds);
            return b;
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
