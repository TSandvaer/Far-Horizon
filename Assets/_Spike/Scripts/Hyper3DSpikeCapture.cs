using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon.Spike
{
    /// <summary>
    /// THROWAWAY R&amp;D-spike capture (ticket 86ca8r72j) — judges the Hyper3D-generated + Mixamo-Humanoid-
    /// rigged castaway IN the shipped Unity/URP build. NOT production code; lives under Assets/_Spike/ and
    /// is deleted when the spike's verdict is recorded.
    ///
    /// Mirrors the production CastawayVerifyCapture pattern (deterministic VerifyCaptureFraming + a
    /// post-enabled camera so the capture sees the SAME URP look the Sponsor judges), but drives the
    /// Animator directly: it shoots ONE Idle frame (Moving=false), then flips Moving=true and shoots ONE
    /// Walk frame mid-cycle — so a single shipped-exe launch proves BOTH clips deform the Humanoid rig in
    /// the build (the spike's core question: does it animate clean + read on-style in URP?).
    ///
    /// Why a bespoke spike component (not the production CaptureGate / CastawayVerifyCapture): the spike
    /// character has its OWN Animator (a 2-state Idle/Walk controller built by Hyper3DSpikeGen) and is NOT
    /// a CastawayCharacter, so the production capture's CastawayCharacter lookups don't apply. This drives
    /// the spike Animator by its "Moving" bool directly.
    ///
    /// Runs WINDOWED only (ScreenCapture needs a real swapchain — unity-conventions.md §Headless): the
    /// spike build is launched
    ///   FarHorizon.exe -screen-fullscreen 0 -spikeCapture -captureDir &lt;dir&gt;
    /// Captures: hyper3d_idle.png + hyper3d_walk.png. Quits non-zero if no Animator/SkinnedMeshRenderer is
    /// found or the bounds never settle (the build-side regression signals — same shape as the production
    /// gate's fail-loud).
    /// </summary>
    public class Hyper3DSpikeCapture : MonoBehaviour
    {
        // Subject spans this fraction of the frame (headroom so feet+hair never clip and the warm subject
        // under warm post doesn't R-clip — the CastawayVerifyCapture lesson).
        public float frameFill = 0.72f;
        public float viewElevation = 0.12f;

        void Start()
        {
            if (HasArg("-spikeCapture") || HasArg("-spikeRest"))
                StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var smr = Object.FindFirstObjectByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            var animator = Object.FindFirstObjectByType<Animator>(FindObjectsInactive.Include);
            bool found = smr != null && animator != null;
            Debug.Log("[Hyper3DSpikeCapture] smr=" + (smr != null) + " animator=" + (animator != null));
            if (!found)
            {
                Debug.LogError("[Hyper3DSpikeCapture] no SkinnedMeshRenderer or Animator in scene — the " +
                               "spike character did not serialize into the spike scene (build-side regression)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // --- REST: DISABLE the animator entirely → the avatar sits in its raw bind pose. The import
            // A/B baseline (proves the mesh/material/scale are clean independent of any clip). Used during
            // the spike to isolate a capture-tool bounds bug from a real defect. Diagnostic-only; -spikeRest. ---
            if (HasArg("-spikeRest"))
            {
                animator.enabled = false;
                yield return CaptureClip(smr, dir, "hyper3d_rest.png", "Rest(bind, animator OFF)");
                Debug.Log("[Hyper3DSpikeCapture] REST verification complete -> " + dir);
                Application.Quit(0);
                yield break;
            }

            // --- IDLE: ensure Moving=false, settle skinning, frame, shoot ---
            animator.SetBool("Moving", false);
            yield return CaptureClip(smr, dir, "hyper3d_idle.png", "Idle");

            // --- WALK: flip Moving=true, let the transition + a few cycle frames play, frame, shoot ---
            animator.SetBool("Moving", true);
            // Let the cross-fade complete + advance into the walk cycle so the legs are mid-stride
            // (not the transition's first blended frame) — proves the walk clip actually drives the rig.
            for (int i = 0; i < 24; i++) yield return null;
            yield return CaptureClip(smr, dir, "hyper3d_walk.png", "Walk");

            Debug.Log("[Hyper3DSpikeCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        // Settle valid skinned-mesh bounds (BakeMesh, never a magic floor — the false-green lesson), frame
        // deterministically through VerifyCaptureFraming-equivalent math, add a post-enabled camera, shoot.
        private IEnumerator CaptureClip(SkinnedMeshRenderer smr, string dir, string file, string label)
        {
            Bounds b = new Bounds();
            bool valid = false;
            for (int attempt = 0; attempt < 30 && !valid; attempt++)
            {
                yield return null; // let skinning apply this frame
                b = BakeWorldBounds(smr);
                valid = b.size.y > 0.2f && b.size.x > 0.02f;
            }
            if (!valid)
            {
                Debug.LogError($"[Hyper3DSpikeCapture] {label}: bounds never settled (last size={b.size}) — " +
                               "would frame a wrong crop; failing loud");
                Application.Quit(1);
                yield break;
            }

            // FIXED-DISTANCE framing (replaces bounds-fit zoom): the bounds-fit close-up rendered a
            // confusing smear because a zoom-to-fit can frame ANY subject the same apparent size (the
            // unity-conventions.md zoom-to-fit false-green). Frame the subject from a fixed gameplay-ish
            // distance at a known height so the WHOLE chibi reads at honest scale: camera at the subject's
            // mid-height, pulled back a fixed 2.6u on a +Z/elevated view, looking at the body center.
            Vector3 center = b.center;
            float h = Mathf.Max(b.size.y, 0.6f);
            Vector3 viewDir = new Vector3(0.30f, 0.28f, 1.0f).normalized; // front, slightly above + to the side
            float dist = 2.6f;
            Vector3 camPos = center + viewDir * dist;
            var frame = new Frame
            {
                position = camPos,
                rotation = Quaternion.LookRotation((center - camPos).normalized, Vector3.up),
                lookAt = center,
                distance = dist
            };

            var camGo = new GameObject("SpikeCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            // Post-enabled render path (the #34/#36 false-green fix): see the same URP look the Sponsor
            // judges. Neutral solid backdrop (not the bright warm skybox) so the background doesn't R-clip.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f); // neutral slate
            cam.fieldOfView = 40f;
            cam.farClipPlane = 200f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Debug.Log($"[Hyper3DSpikeCapture] {label} cam at {frame.position} center={b.center} size={b.size} " +
                      $"dist={frame.distance:F2}");

            for (int i = 0; i < 8; i++) yield return null; // settle lighting/post/skinning
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(dir, file);
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("[Hyper3DSpikeCapture] wrote " + path);
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.4f);

            Object.Destroy(camGo); // don't leave two cameras fighting for the next shot
        }

        // Return the SMR's WORLD-space render bounds directly. Scene-diag (Hyper3DSpikeSceneDiag, 2026-06-15)
        // proved why the BakeMesh-reconstruct path failed here: this Mixamo rig's SMR node carries a
        // localScale(100,100,100) (cm→m FBX compensation), so BakeMesh(useScale:true) bakes verts into a
        // ~0.005u local space, and a TRS(pos,rot,Vector3.one) reconstruction DROPS that 100× → a near-zero
        // bound → camera buried in the mesh (the giant-smear capture). smr.bounds is already the correct
        // world AABB (probed: center(0.01,0.42,-0.03) size(0.52,1.00,0.46)) and updates per skinned frame,
        // so it tracks the Idle vs Walk silhouette difference. (unity-conventions.md §SMR-bounds trap:
        // .bounds is unreliable on a NEVER-RENDERED deserialized scene — but this is a WINDOWED build that
        // renders every frame before we read, and the capture settles over up to 30 frames, so .bounds is
        // valid here; the trap applies to headless/never-rendered reads.)
        private static Bounds BakeWorldBounds(SkinnedMeshRenderer smr)
        {
            return smr.bounds;
        }

        // Deterministic framing (copy of the production VerifyCaptureFraming.ComputeFrame so the spike is
        // self-contained under Assets/_Spike/ — it does NOT reference the FarHorizon.Runtime assembly).
        private struct Frame { public Vector3 position; public Quaternion rotation; public Vector3 lookAt; public float distance; }

        private static Frame ComputeFrame(Vector3 center, Vector3 size, Vector3 viewDir, float fovDeg,
            float aspect, float fill)
        {
            if (aspect <= 0.0001f) aspect = 16f / 9f;
            fill = Mathf.Clamp(fill, 0.30f, 0.95f);
            Vector3 dir = viewDir; dir.Normalize();
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            float vExtent = Mathf.Max(size.y, 0.0001f);
            float hExtent = Mathf.Max(Mathf.Max(size.x, size.z), 0.0001f);
            float vFov = fovDeg * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * aspect);
            float distV = (vExtent / fill) / (2f * Mathf.Tan(vFov * 0.5f));
            float distH = (hExtent / fill) / (2f * Mathf.Tan(hFov * 0.5f));
            float distance = Mathf.Max(distV, distH);
            Vector3 pos = center + dir * distance;
            return new Frame
            {
                position = pos,
                rotation = Quaternion.LookRotation((center - pos).normalized, Vector3.up),
                lookAt = center,
                distance = distance
            };
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", "ci-out", "spike")
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Captures");
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
