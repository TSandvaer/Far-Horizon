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
    /// bone array (the real skeleton), MEASURES the rendered hand geometry, frames a tight close-up on it from a
    /// 3/4-front angle, and shoots one PNG per hand. Inert unless launched with -verifyHands. MUST run WINDOWED
    /// (ScreenCapture needs a real swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHands -captureDir &lt;dir&gt;
    /// Captures: hands_right.png + hands_left.png. Quits non-zero if no avatar / no hand bones are found / the
    /// rendered hand geometry cannot be measured (fail loud, never ship a wrong crop).
    ///
    /// REFRAME (86cavaxk7, the PR #330 AC6 follow-up) — WHY THE OLD FRAMING MISSED THE HAND ON THE v4 RIG:
    /// the frame was anchored on the wrist BONE ORIGIN plus a fixed world-DOWN nudge, inside a HARDCODED 0.26m
    /// box. Both were fitted to the v3 hand. Neither tracks the rendered geometry, so the v4 blocky-mitten hand
    /// framed off-centre and under-filled — measured on the live v4 rig (headless probe, ticket 86cavaxk7):
    ///   rendered right-hand AABB  centre (0.6422, 0.7571, 6.0157)  size (0.1884, 0.1884, 0.1390)
    ///   old anchor (bone + down)  centre (0.5907, 0.7546, 5.9985)   box  0.26 cubed
    ///   => anchor off by 0.0545u = 29% of the hand's OWN extent, and the box 1.38x larger than the hand.
    /// The result reads as "frames the world, not the hand" (Devon review 4753044764 NIT 2). NOTE his stated
    /// mechanism — "the wrist bone world pos DIVERGES from the rendered mesh on the 100x rig" — is REFUTED: the
    /// shipped-exe trace has the bone at a sane (0.37, 1.14, 6.12) and the probe measures it INSIDE smr.bounds.
    /// The bone position is fine; the fixed OFFSET + fixed BOX around it are what was rig-specific.
    /// FIX (cause, not symptom): measure the rendered hand AABB every shot and frame THAT — so the capture
    /// re-fits itself on any rig swap (v2/v3/v4 all live behind toggles) instead of needing a re-tuned constant.
    /// </summary>
    public class HandsVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Hand-scale reference length in METRES. RETASKED in 86cavaxk7: this is NO LONGER the framing box
        // half-extent (the box is MEASURED now — TryComputeRenderedHandBounds). It is the world-space distance
        // the wrist bone is briefly nudged by the geometry probe to isolate the hand-influenced vertices. Any
        // hand-scale length works (vertex selection is RELATIVE to the max displacement); kept at 0.13 so the
        // HandsVerifyCapture serialized into Boot.unity stays byte-identical.
        public float handHalfExtent = 0.13f;
        public float fieldOfView = 35f;

        // The measured hand spans this fraction of the frame's binding extent. 0.72 keeps ~28% margin so a
        // splayed finger / a held haft never clips the frame edge (the CastawayVerifyCapture headroom
        // convention) while the hand still DOMINATES the shot.
        private const float FrameFill = 0.72f;
        // Vertex selection: keep verts whose probe displacement exceeds this fraction of the max displacement
        // (== hand-chain skin weight above ~0.6). MEASURED STABLE on the live v4 rig — thresholds 0.6 / 0.4 /
        // 0.25 all produced an IDENTICAL AABB (224 / 228 / 232 verts), so the hand set is cleanly separated
        // from the forearm weight-bleed; 0.6 is the tightest of the three.
        private const float HandWeightFraction = 0.6f;
        // Below this the probe did not isolate a hand (a rig/bone regression) — fail loud, do not guess a box.
        private const int MinHandVerts = 8;

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

            if (rightHand != null)
            {
                // right hand, outer-front-and-above (palm/back read).
                yield return ShootHand("hands_right.png", avatar, rightHand, new Vector3(0.7f, 0.35f, 1.0f),
                    aspect, cam, camGo, dir);
                // right hand, FINGERTIP-ON from BELOW looking up (splay / fusion / a bent-back digit is most
                // visible looking along the fingers) — the angle a metric can't see.
                yield return ShootHand("hands_right_tips.png", avatar, rightHand, new Vector3(0.15f, -0.9f, 0.5f),
                    aspect, cam, camGo, dir);
                // right hand from the GAMEPLAY-ish rear-orbit angle (behind + above, the orbit cam's read) —
                // the false-green-capture guard (an isolated front rig can hide what gameplay shows).
                yield return ShootHand("hands_right_rear.png", avatar, rightHand, new Vector3(0.4f, 0.5f, -1.0f),
                    aspect, cam, camGo, dir);
            }
            if (leftHand != null)
            {
                yield return ShootHand("hands_left.png", avatar, leftHand, new Vector3(-0.7f, 0.35f, 1.0f),
                    aspect, cam, camGo, dir);
                yield return ShootHand("hands_left_tips.png", avatar, leftHand, new Vector3(-0.15f, -0.9f, 0.5f),
                    aspect, cam, camGo, dir);
                yield return ShootHand("hands_left_rear.png", avatar, leftHand, new Vector3(-0.4f, 0.5f, -1.0f),
                    aspect, cam, camGo, dir);
            }

            if (_measureFailed)
            {
                Debug.LogError("[HandsVerifyCapture] at least one shot could NOT measure the rendered hand " +
                               "geometry — the captures above are NOT trustworthy evidence (failing loud rather " +
                               "than shipping a wrong crop)");
                Application.Quit(1);
                yield break;
            }

            Debug.Log("[HandsVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        // Set when any shot's rendered-hand measurement failed, so the run exits non-zero instead of shipping
        // a silently-wrong crop as evidence (the false-green class this whole tool exists to close).
        private bool _measureFailed;

        private IEnumerator ShootHand(string fileName, SkinnedMeshRenderer avatar, Transform hand, Vector3 viewDir,
            float aspect, Camera cam, GameObject camGo, string dir)
        {
            // Frame the RENDERED hand, measured THIS frame (the idle clip keeps posing the arm between shots).
            // The legacy anchor (bone origin + a fixed world-down nudge, in a hardcoded box) is logged beside it
            // as the divergence instrument — that offset IS the bug this reframe fixes (86cavaxk7).
            if (!TryComputeRenderedHandBounds(avatar, hand, handHalfExtent, out Bounds handBounds, out int handVerts))
            {
                Debug.LogError($"[HandsVerifyCapture] {fileName}: could NOT measure the rendered geometry driven " +
                               $"by '{hand.name}' (probe isolated {handVerts} verts, need >= {MinHandVerts}) — " +
                               "skipping the shot; a wrist bone with no skin influence is a rig regression signal");
                _measureFailed = true;
                yield break;
            }

            var frame = FitFrameToBox(handBounds, viewDir, cam.fieldOfView, aspect, FrameFill);
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Vector3 legacyAnchor = hand.position + Vector3.down * (handHalfExtent * 0.4f);
            Debug.Log($"[HandsVerifyCapture] {fileName}: bone '{hand.name}' at {hand.position} | MEASURED hand " +
                      $"centre={handBounds.center} size={handBounds.size} verts={handVerts} | legacy anchor=" +
                      $"{legacyAnchor} (off by {Vector3.Distance(legacyAnchor, handBounds.center):F4}u = " +
                      $"{(Vector3.Distance(legacyAnchor, handBounds.center) / Mathf.Max(0.0001f, MaxAxis(handBounds.size)) * 100f):F0}% " +
                      $"of the hand extent) | camPos={frame.position} dist={frame.distance:F3} fill={FrameFill:F2}");

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

        /// <summary>
        /// The camera frame that fits EVERY CORNER of <paramref name="box"/> inside <paramref name="fill"/> of
        /// the frame. <see cref="VerifyCaptureFraming.ComputeFrame"/> alone is a PLANAR estimate — it sizes the
        /// distance from the bounds' extents as if the subject were flat at the look-at plane. That is fine for
        /// the whole-avatar / whole-prop captures it was written for (subject depth is small next to the framing
        /// distance), but a HAND is near-cubic and framed at ~0.4u, so two effects push corners off-frame:
        /// perspective inflates the NEAR face, and an oblique view (the 3/4 and from-below angles) mixes the
        /// box's x/z extents into the SCREEN vertical. MEASURED on the live v4 rig: the planar frame put the
        /// right hand's lowest corner at ndc y = −1.150, i.e. 1.34x over-framed and clipped off the bottom edge
        /// (this test-caught it: HandsVerifyFramingTests).
        /// FIX WITHOUT A TUNED CONSTANT (a tuned pad is exactly what de-tuned across the v3→v4 swap): measure the
        /// worst corner's projected |ndc| and scale the distance by its overshoot, then re-check. The relation is
        /// ~linear in distance so it converges in 1–2 passes; the loop is bounded so the result is deterministic.
        /// VerifyCaptureFraming itself is deliberately NOT changed — six other verify captures share it and their
        /// framings are already Sponsor/QA-accepted.
        /// </summary>
        public static VerifyCaptureFraming.Frame FitFrameToBox(Bounds box, Vector3 viewDir, float fov,
            float aspect, float fill)
        {
            var frame = VerifyCaptureFraming.ComputeFrame(box.center, box.size, viewDir, fov, aspect, fill);
            Vector3 dir = viewDir.sqrMagnitude > 1e-6f ? viewDir.normalized : Vector3.forward;
            for (int pass = 0; pass < 6; pass++)
            {
                float worst = WorstCornerNdc(box, frame.position, frame.rotation, fov, aspect);
                if (worst <= fill * 1.001f || worst <= 0.0001f) break;
                float dist = Vector3.Distance(frame.position, box.center) * (worst / fill);
                frame.position = box.center + dir * dist;
                frame.rotation = Quaternion.LookRotation((box.center - frame.position).normalized, Vector3.up);
                frame.lookAt = box.center;
                frame.distance = dist;
            }
            return frame;
        }

        /// <summary>The largest |ndc.x| / |ndc.y| over the box's 8 corners (1.0 == the frame edge).</summary>
        private static float WorstCornerNdc(Bounds box, Vector3 camPos, Quaternion camRot, float fov, float aspect)
        {
            // Unity's view matrix negates Z (right-handed world -> left-handed view).
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                             Matrix4x4.TRS(camPos, camRot, Vector3.one).inverse;
            Matrix4x4 vp = Matrix4x4.Perspective(fov, aspect, 0.01f, 1000f) * view;
            Vector3 c = box.center, e = box.extents;
            float worst = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                            (i & 2) == 0 ? -e.y : e.y,
                                            (i & 4) == 0 ? -e.z : e.z);
                Vector4 clip = vp * new Vector4(p.x, p.y, p.z, 1f);
                if (clip.w <= 0.0001f) return float.MaxValue; // corner behind the camera — push out hard
                worst = Mathf.Max(worst, Mathf.Max(Mathf.Abs(clip.x / clip.w), Mathf.Abs(clip.y / clip.w)));
            }
            return worst;
        }

        /// <summary>
        /// The WORLD-space AABB of the geometry actually RENDERED by the hand chain rooted at
        /// <paramref name="handRoot"/> (the wrist bone + every finger bone under it), measured from the LIVE
        /// skinned pose. This is the framing anchor the capture uses — NOT the bone origin (86cavaxk7).
        ///
        /// HOW THE HAND VERTS ARE ISOLATED — and why NOT via skin weights: reading
        /// <c>smr.sharedMesh.boneWeights</c> would be the obvious route, but the castaway FBX imports with
        /// <c>isReadable: 0</c> (probe-verified: <c>sharedMesh.isReadable == False</c>), so a CPU mesh read
        /// SUCCEEDS in the editor and THROWS in the shipped player — the editor-vs-runtime false-green class
        /// (unity-conventions.md §Editor-vs-runtime). Instead this does a DISPLACEMENT CENSUS, which needs no
        /// source-mesh CPU data at all: bake the pose, nudge the wrist bone by a known WORLD delta, re-bake,
        /// restore. Each vertex moves by (its hand-chain weight x delta), so verts displaced more than
        /// <see cref="HandWeightFraction"/> of the max are the hand-dominated set. Both bakes happen
        /// SYNCHRONOUSLY inside one frame, so no LateUpdate driver (CastawayArmPose / CastawayHandPose /
        /// HeldAxeRig) can observe the nudged bone. (Same two-bake idiom as CastawayV4DefectDiag.RotateAndDiff,
        /// but TRANSLATING rather than rotating — a translation's displacement is proportional to WEIGHT, while
        /// a rotation's is proportional to distance-from-axis, which would drop the palm and keep only fingertips.)
        ///
        /// LOCAL->WORLD MATRIX: <c>BakeMesh(useScale:false)</c> verts x a UNIT-scale
        /// <c>Matrix4x4.TRS(pos, rot, Vector3.one)</c> — never <c>smr.localToWorldMatrix</c>, which
        /// DOUBLE-APPLIES the FBX's 100x cm->m node scale (the walk-float saga's Bug B). MEASURED on the live v4
        /// rig (ticket 86cavaxk7 probe): the SMR node is <c>localScale (100,100,100)</c> / <c>lossyScale 180</c>;
        /// bake(false)+TRS-unit gives a full-mesh AABB of centre (0, 0.8521, 5.9865) size (1.4727, 1.7043,
        /// 0.4575) — consistent with the trusted world AABB <c>smr.bounds</c> (centre (0.0136, 0.8043, 6.0096)
        /// size (1.5258, 1.8, 0.6033)) — while bake(false)+l2w explodes to a 265u box.
        /// </summary>
        /// <param name="probeNudgeMetres">World distance the wrist bone is temporarily moved. Any hand-scale
        /// length works (selection is relative to the max displacement).</param>
        public static bool TryComputeRenderedHandBounds(SkinnedMeshRenderer smr, Transform handRoot,
            float probeNudgeMetres, out Bounds worldBounds, out int handVertCount)
        {
            worldBounds = new Bounds();
            handVertCount = 0;
            if (smr == null || smr.sharedMesh == null || handRoot == null) return false;
            if (probeNudgeMetres <= 0.0001f) return false;

            var restVerts = new List<Vector3>(4096);
            var movedVerts = new List<Vector3>(4096);
            var bakeRest = new Mesh { name = "HandsVerifyBakeRest" };
            var bakeMoved = new Mesh { name = "HandsVerifyBakeMoved" };
            try
            {
                smr.BakeMesh(bakeRest, false);
                bakeRest.GetVertices(restVerts);
                if (restVerts.Count == 0) return false;

                // Nudge the wrist in WORLD space, re-bake, restore. localPosition is saved/restored (the parent
                // chain carries the 100x node scale, so writing world position is the scale-agnostic way in).
                Vector3 savedLocal = handRoot.localPosition;
                handRoot.position = handRoot.position + Vector3.right * probeNudgeMetres;
                smr.BakeMesh(bakeMoved, false);
                handRoot.localPosition = savedLocal;
                bakeMoved.GetVertices(movedVerts);

                int n = Mathf.Min(restVerts.Count, movedVerts.Count);
                if (n == 0) return false;
                float maxD = 0f;
                for (int i = 0; i < n; i++)
                {
                    float d = (movedVerts[i] - restVerts[i]).magnitude;
                    if (d > maxD) maxD = d;
                }
                // A wrist bone that moves nothing = no skin influence resolved (rig regression) — fail loud.
                if (maxD <= 1e-5f) return false;

                float threshold = maxD * HandWeightFraction;
                Matrix4x4 l2w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    if ((movedVerts[i] - restVerts[i]).magnitude <= threshold) continue;
                    Vector3 p = l2w.MultiplyPoint3x4(restVerts[i]);
                    mn = Vector3.Min(mn, p);
                    mx = Vector3.Max(mx, p);
                    handVertCount++;
                }
                if (handVertCount < MinHandVerts) return false;
                worldBounds = new Bounds((mn + mx) * 0.5f, mx - mn);
                return worldBounds.size.x > 0f && worldBounds.size.y > 0f && worldBounds.size.z > 0f;
            }
            finally
            {
                if (Application.isPlaying) { Destroy(bakeRest); Destroy(bakeMoved); }
                else { DestroyImmediate(bakeRest); DestroyImmediate(bakeMoved); }
            }
        }

        private static float MaxAxis(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

        // Resolve a bone whose colon-stripped lowered name EXACTLY equals the token (excludes finger bones,
        // which also contain "hand"), from the SMR bone array (the real skeleton). Mirrors
        // MovementCameraScene.FindBoneByExactToken.
        public static Transform FindBoneByExactToken(SkinnedMeshRenderer smr, string token)
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
