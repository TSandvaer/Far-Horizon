using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the castaway's HANDS while the Breathing Idle clip
    /// plays (PR #186 FINGER re-open). Sibling of <see cref="CastawayVerifyCapture"/> — but the avatar-wide
    /// close-up frames the hands far too small (the fingers are a few pixels at the frame bottom), so it CANNOT
    /// substantiate a finger-mangle claim. This capture frames EACH hand TIGHTLY (individual fingers resolvable)
    /// so the Sponsor / a reviewer can EYEBALL whether a finger is bent / fused / collapsed / twisted under the
    /// arms-down idle pose — the symptom region the Sponsor saw mangled in the Mixamo preview AND the game build.
    ///
    /// WHY THE METRICS COULDN'T CATCH THIS (the bug-CLASS lesson): the stretch-RATIO trace
    /// (CharacterAssetGen.FingerDeformTrace) only catches a STRETCHED/torn finger (a weight defect); the
    /// rotation trace (FingerPoseRotationTrace) only catches a finger posed to a LARGE bad angle. A mangle that
    /// is a subtle bend / self-intersection / collapse can read green on BOTH metrics. The CLOSE VISUAL is the
    /// only trustworthy proof — exactly the html5-visual-verification-gate / shipped-build-capture-gate spirit.
    ///
    /// It does NOT touch gameplay: finds the CastawayCharacter avatar, pins its facing to +Z (front), lets the
    /// Animator settle into the looping Breathing Idle, locates each hand's wrist bone from the SkinnedMeshRenderer
    /// bone array (the real skeleton), frames a tight close-up centred on the hand from a 3/4-front angle, and
    /// shoots one PNG per hand. Inert unless launched with -verifyHands. MUST run WINDOWED (ScreenCapture needs a
    /// real swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHands -captureDir &lt;dir&gt;
    /// Captures: hands_right.png + hands_left.png. Quits non-zero if no avatar / no hand bones are found.
    /// </summary>
    public class HandsVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // The hand reads cleanly when it spans a large share of a tight frame. A fixed half-extent (metres)
        // around the wrist bone gives a stable, hand-sized framing box independent of skinned-mesh bounds
        // (the wrist + fingers occupy roughly a 0.12m cube on this rig at avatar scale 1.8).
        public float handHalfExtent = 0.13f;
        public float fieldOfView = 35f;

        void Start()
        {
            if (HasArg("-verifyHands"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the serialized avatar + its CastawayCharacter (search inactive so a missing avatar is a hard fail).
            var smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            SkinnedMeshRenderer avatar = null;
            CastawayCharacter castaway = null;
            foreach (var s in smrs)
            {
                var cc = s.GetComponentInParent<CastawayCharacter>(true);
                if (cc != null) { avatar = s; castaway = cc; break; }
            }
            if (avatar == null && smrs.Length > 0) avatar = smrs[0];
            bool found = avatar != null;
            Debug.Log("[HandsVerifyCapture] castaway avatar found in scene: " + found);
            if (!found)
            {
                Debug.LogError("[HandsVerifyCapture] no SkinnedMeshRenderer avatar in scene — serialized castaway " +
                               "missing from Boot.unity (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Pin facing to +Z (front) so the framing angle is deterministic every run (the same construction
            // CastawayVerifyCapture uses — front is geometrically +Z on this rig).
            if (castaway != null) castaway.FaceWorldYawInstant(0f);

            // Resolve each hand's WRIST bone exactly (exclude finger bones, which also contain "hand").
            Transform rightHand = FindBoneByExactToken(avatar, "righthand");
            Transform leftHand = FindBoneByExactToken(avatar, "lefthand");
            Debug.Log("[HandsVerifyCapture] rightHand=" + (rightHand != null ? rightHand.name : "<null>") +
                      " leftHand=" + (leftHand != null ? leftHand.name : "<null>"));
            if (rightHand == null && leftHand == null)
            {
                Debug.LogError("[HandsVerifyCapture] NO hand wrist bones resolved from the SMR bone array — the " +
                               "rig is missing mixamorig:RightHand/LeftHand (build-side regression signal)");
                Application.Quit(1);
                yield break;
            }

            // Let the Animator settle into the looping Breathing Idle so the hands hold their idle-pose shape.
            // (The default Idle state is the breathing clip — 86cackb3j.) Sample several frames so skinning is
            // applied to the bone array we read.
            for (int i = 0; i < 30; i++) yield return null;

            // A 3/4-front-and-slightly-above view that shows the palm-side + finger curl of a relaxed arms-down
            // hand (the hand hangs by the hip; fingers face roughly forward/inward). viewDir is FROM the hand
            // TOWARD the camera. Mirror left/right so each hand is shot from its own outer-front.
            var camGo = new GameObject("HandCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f); // neutral slate — non-blown, isolates the hand
            cam.fieldOfView = fieldOfView;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; // gameplay-representative look
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            Vector3 boxSize = Vector3.one * (handHalfExtent * 2f);

            if (rightHand != null)
            {
                // right hand, outer-front-and-above (palm/back read).
                yield return ShootHand("hands_right.png", rightHand, new Vector3(0.7f, 0.35f, 1.0f),
                    boxSize, aspect, cam, camGo, dir);
                // right hand, FINGERTIP-ON from BELOW looking up (splay / fusion / a bent-back digit is most
                // visible looking along the fingers) — the angle a metric can't see.
                yield return ShootHand("hands_right_tips.png", rightHand, new Vector3(0.15f, -0.9f, 0.5f),
                    boxSize, aspect, cam, camGo, dir);
                // right hand from the GAMEPLAY-ish rear-orbit angle (behind + above, the orbit cam's read) —
                // the false-green-capture guard (an isolated front rig can hide what gameplay shows).
                yield return ShootHand("hands_right_rear.png", rightHand, new Vector3(0.4f, 0.5f, -1.0f),
                    boxSize, aspect, cam, camGo, dir);
            }
            if (leftHand != null)
            {
                yield return ShootHand("hands_left.png", leftHand, new Vector3(-0.7f, 0.35f, 1.0f),
                    boxSize, aspect, cam, camGo, dir);
                yield return ShootHand("hands_left_tips.png", leftHand, new Vector3(-0.15f, -0.9f, 0.5f),
                    boxSize, aspect, cam, camGo, dir);
                yield return ShootHand("hands_left_rear.png", leftHand, new Vector3(-0.4f, 0.5f, -1.0f),
                    boxSize, aspect, cam, camGo, dir);
            }

            Debug.Log("[HandsVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        private IEnumerator ShootHand(string fileName, Transform hand, Vector3 viewDir, Vector3 boxSize,
            float aspect, Camera cam, GameObject camGo, string dir)
        {
            // Centre the frame slightly DOWN the hand toward the fingers (the wrist bone origin sits at the
            // wrist; the fingers extend further along the hand) so the digits — the symptom — are centred,
            // not cropped. Offset along the hand's forward (the bone's local +Y on the Mixamo rig points down
            // the fingers; but to stay rig-agnostic we just nudge toward world-down a touch since the arm hangs).
            Vector3 center = hand.position + Vector3.down * (handHalfExtent * 0.4f);
            var frame = VerifyCaptureFraming.ComputeFrame(center, boxSize, viewDir, cam.fieldOfView, aspect, 0.85f);
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Debug.Log($"[HandsVerifyCapture] {fileName}: hand bone '{hand.name}' at {hand.position} " +
                      $"center={center} camPos={frame.position} dist={frame.distance:F3}");

            // Settle lighting/post/skinning, then shoot.
            for (int i = 0; i < 6; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(dir, fileName);
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[HandsVerifyCapture] wrote " + file);
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        // Resolve a bone whose colon-stripped lowered name EXACTLY equals the token (excludes finger bones,
        // which also contain "hand"), from the SMR bone array (the real skeleton). Mirrors
        // MovementCameraScene.FindBoneByExactToken.
        private static Transform FindBoneByExactToken(SkinnedMeshRenderer smr, string token)
        {
            if (smr != null && smr.bones != null)
                foreach (var bone in smr.bones)
                    if (bone != null && ExactBoneToken(bone.name) == token) return bone;
            return null;
        }

        private static string ExactBoneToken(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return "";
            string n = boneName.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            return n;
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
