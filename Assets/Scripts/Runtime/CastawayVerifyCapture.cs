using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the castaway avatar (ticket 86ca8ca1m
    /// recolor). Sibling of AxeVerifyCapture / ChopVerifyCapture / MovementVerifyCapture.
    ///
    /// Why this exists: the committed gameplay-orbit capture path (CaptureGate / -captureGate) frames the
    /// castaway tiny at default orbit distance — the recolor's identity colours (warm khaki shirt,
    /// sandy-ginger hair, bare-feet skin) are NOT judgeable at that scale. Per the html5/shipped-build
    /// visual-verification gate AND the PR #21 lesson (a detail claim must ride a COMMITTED reproducible
    /// shipped path, never an uncommitted throwaway shot), this component IS that committed path: a
    /// -verifyCastaway flag that parks a dedicated camera in front of the avatar, frames the
    /// head-to-feet front, and captures castaway_front.png so a reviewer (Tess / Sponsor) can re-run it
    /// and judge the identity from a SHIPPED frame, not the editor.
    ///
    /// It does NOT touch gameplay: it finds the CastawayCharacter avatar, adds its own deterministic
    /// camera (the orbit follow-state is irrelevant), and shoots one front close-up. Inert unless launched
    /// with -verifyCastaway.
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyCastaway -captureDir &lt;dir&gt;
    /// Captures: castaway_front.png. Quits non-zero if no avatar with a SkinnedMeshRenderer is found in the
    /// scene (the build-side regression signal — the serialized avatar is missing).
    /// </summary>
    public class CastawayVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

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
            foreach (var s in smrs)
            {
                if (s.GetComponentInParent<CastawayCharacter>(true) != null) { avatar = s; break; }
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

            // Encapsulate the WHOLE avatar's renderer bounds (skinned mesh is multi-part) so the framing
            // is mesh-layout-independent. Wait a couple frames first so the skinned bounds are valid.
            for (int i = 0; i < 4; i++) yield return null;
            var allR = avatar.GetComponentsInParent<Transform>()[0]
                .GetComponentsInChildren<Renderer>(true);
            Bounds b = allR.Length > 0 ? allR[0].bounds : avatar.bounds;
            for (int i = 1; i < allR.Length; i++) b.Encapsulate(allR[i].bounds);
            // Aim a touch BELOW centre (toward mid-torso) so head-to-feet fits — the big chunky head
            // pulls the visual weight up, so centring on bounds-centre crops the feet.
            float height = Mathf.Max(0.5f, b.size.y);
            Vector3 lookAt = b.center - new Vector3(0f, height * 0.12f, 0f);

            // Dedicated capture camera in FRONT of the avatar (world -Z toward it, looking +Z), framed to
            // fit head-to-feet with a little margin. Front view so the face/eyes/shirt all read. Distance
            // derived from height + FOV so any height-normalize change keeps the avatar in frame.
            var camGo = new GameObject("CastawayCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f); // same deep-dusk neutral as the game cam
            cam.fieldOfView = 40f;
            // Frame head-to-feet with generous margin (1.7x height) so the WHOLE chunky silhouette reads,
            // not just the head filling the frame (the first cut framed the head only).
            float dist = (height * 2.3f) / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            // The avatar's runtime yaw (CastawayCharacter.LateUpdate) defaults its facing to +Z at rest,
            // so the FRONT (face/eyes/shirt) faces +Z. View from +Z looking back toward -Z, slightly to
            // the side + above, so we catch the front identity (not the back of the head — the first cut
            // viewed from -Z and shot the rear). TransformPoint not needed: world axes (avatar at origin).
            Vector3 viewDir = new Vector3(0.30f, 0.12f, 1.0f).normalized;
            Vector3 camPos = lookAt + viewDir * dist;
            camGo.transform.position = camPos;
            camGo.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            Debug.Log($"[CastawayVerifyCapture] cam at {camPos} looking at {lookAt} (height={height:F2} dist={dist:F2})");

            // Let lighting/post/skinning settle before the shot.
            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(dir, "castaway_front.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[CastawayVerifyCapture] wrote " + file + " (identity close-up)");
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[CastawayVerifyCapture] verification complete -> " + dir);
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
