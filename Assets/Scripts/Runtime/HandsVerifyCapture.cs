using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the castaway's HANDS — now across a WEAPON/LOCOMOTION
    /// STATE GRID (ticket 86cahnmjv "finger sticks out like it's broken", extending the PR #186 empty-idle gate).
    ///
    /// WHY THE GRID (the #186 coverage gap): the original gate shot the hands ONLY in the EMPTY-HANDED Breathing
    /// Idle — verified clean 2026-06-29 — yet the Sponsor STILL reported a broken-looking finger after that. The
    /// unverified surfaces are exactly the states the gate never framed: a HELD weapon (CastawayFingerCurl composes
    /// +26°/bone onto the clip pose when the axe/spear is the selected belt item — #232 extended the gate to the
    /// spear), and the non-idle clips (walk / run / crouch / chop-swing pose the fingers differently per clip).
    /// This grid drives each state through the REAL gameplay seams (Inventory.PickUpAxe/PickUpSpear +
    /// InventoryModel.SelectBelt; WasdMovement.SetInputOverride/SetSprintOverride/SetCrouchOverride;
    /// CastawayCharacter.TriggerChop) and shoots tight per-hand close-ups in every state, so a per-state finger
    /// defect is eyeball-judgeable from SHIPPED frames.
    ///
    /// WHY THE METRICS COULDN'T CATCH THIS (the bug-CLASS lesson, unchanged from #186): the stretch-RATIO trace
    /// (CharacterAssetGen.FingerDeformTrace) only catches a STRETCHED/torn finger (a weight defect); the rotation
    /// trace (FingerPoseRotationTrace) only catches a finger posed to a LARGE bad angle — and BOTH sample raw
    /// clips WITHOUT the runtime grip-curl composition. The CLOSE VISUAL of the LIVE composed pose is the only
    /// trustworthy proof. Per state it ALSO logs each curled finger bone's live local euler + flags any hand bone
    /// the curl does NOT cover ([hands-rig] lines) — the rig-layer trace that pins WHICH digit/joint if a frame
    /// shows a defect.
    ///
    /// It does NOT touch gameplay wiring: everything is driven through public runtime seams, inert unless
    /// launched with -verifyHands. MUST run WINDOWED (ScreenCapture needs a real swapchain).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHands -captureDir &lt;dir&gt;
    /// The empty-idle baseline keeps the ORIGINAL six file names (hands_right.png, hands_right_tips.png,
    /// hands_right_rear.png + left mirrors) so prior-run comparisons hold; grid states write
    /// hands_&lt;state&gt;_&lt;shot&gt;.png. Quits non-zero if no avatar / no hand bones / a grid state fails to engage.
    /// </summary>
    public class HandsVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // The hand reads cleanly when it spans a large share of a tight frame. A fixed half-extent (metres)
        // around the wrist bone gives a stable, hand-sized framing box independent of skinned-mesh bounds
        // (the wrist + fingers occupy roughly a 0.12m cube on this rig at avatar scale 1.8).
        public float handHalfExtent = 0.13f;
        public float fieldOfView = 35f;

        private Camera _cam;
        private GameObject _camGo;
        private float _aspect;
        private Vector3 _boxSize;
        private Transform _rightHand, _leftHand;
        private CastawayCharacter _castaway;
        private Inventory _inventory;
        private WasdMovement _player;
        private CastawayFingerCurl _curl;
        private string _dir;
        private bool _stateEngageFailed;

        void Start()
        {
            if (HasArg("-verifyHands"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            _dir = ResolveDir();
            Directory.CreateDirectory(_dir);

            // Find the serialized avatar + its CastawayCharacter (search inactive so a missing avatar is a hard fail).
            var smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            SkinnedMeshRenderer avatar = null;
            foreach (var s in smrs)
            {
                var cc = s.GetComponentInParent<CastawayCharacter>(true);
                if (cc != null) { avatar = s; _castaway = cc; break; }
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

            // The gameplay seams that drive the grid states (all public runtime APIs — no scene rewire needed).
            _inventory = Object.FindAnyObjectByType<Inventory>();
            _player = Object.FindAnyObjectByType<WasdMovement>();
            _curl = Object.FindAnyObjectByType<CastawayFingerCurl>();
            Debug.Log("[HandsVerifyCapture] seams: inventory=" + (_inventory != null) + " player=" + (_player != null) +
                      " fingerCurl=" + (_curl != null));

            // Pin facing to +Z (front) so the empty-idle baseline framing is deterministic run-to-run (the same
            // construction CastawayVerifyCapture uses). Movement states re-face naturally; framing is model-relative.
            if (_castaway != null) _castaway.FaceWorldYawInstant(0f);

            // Resolve each hand's WRIST bone exactly (exclude finger bones, which also contain "hand").
            _rightHand = FindBoneByExactToken(avatar, "righthand");
            _leftHand = FindBoneByExactToken(avatar, "lefthand");
            Debug.Log("[HandsVerifyCapture] rightHand=" + (_rightHand != null ? _rightHand.name : "<null>") +
                      " leftHand=" + (_leftHand != null ? _leftHand.name : "<null>"));
            if (_rightHand == null && _leftHand == null)
            {
                Debug.LogError("[HandsVerifyCapture] NO hand wrist bones resolved from the SMR bone array — the " +
                               "rig is missing mixamorig:RightHand/LeftHand (build-side regression signal)");
                Application.Quit(1);
                yield break;
            }

            // RIG-COVERAGE DUMP (once): every SMR bone under either hand, and whether the grip curl covers it.
            // A hand bone the curl does NOT cover (e.g. an auto-rig pinky the 4-fingered mesh shouldn't have)
            // stays in the OPEN clip pose while its neighbours curl — the "one finger sticks out" candidate.
            DumpHandRigCoverage(avatar);

            // Let the Animator settle into the looping Breathing Idle so the hands hold their idle-pose shape.
            for (int i = 0; i < 30; i++) yield return null;

            _camGo = new GameObject("HandCloseupCamera");
            _cam = _camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f); // neutral slate — non-blown, isolates the hand
            _cam.fieldOfView = fieldOfView;
            var camData = _camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; // gameplay-representative look
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            _aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            _boxSize = Vector3.one * (handHalfExtent * 2f);

            // ============================== THE STATE GRID ==============================

            // --- 1. EMPTY IDLE (the #186 baseline — ORIGINAL six file names kept) ---
            LogState("idle_empty");
            if (_rightHand != null)
            {
                yield return ShootHand("hands_right.png", _rightHand, new Vector3(0.7f, 0.35f, 1.0f));
                yield return ShootHand("hands_right_tips.png", _rightHand, new Vector3(0.15f, -0.9f, 0.5f));
                yield return ShootHand("hands_right_rear.png", _rightHand, new Vector3(0.4f, 0.5f, -1.0f));
                // 86cahnmjv soak-FAIL #2: a DEAD-FRONT "facing the camera" shot — the Sponsor's gameplay
                // read-angle. The prior three angles are OUTER-front / from-below / rear; NONE looks at the
                // right hand straight-on from the front, so a finger bent toward/away from a head-on viewer
                // was invisible to the grid (the coverage gap that let this soak-fail through). This is the
                // angle the Sponsor judges + the one the regression asserts.
                yield return ShootHand("hands_right_front.png", _rightHand, new Vector3(0.05f, 0.15f, 1.0f));
            }
            if (_leftHand != null)
            {
                yield return ShootHand("hands_left.png", _leftHand, new Vector3(-0.7f, 0.35f, 1.0f));
                yield return ShootHand("hands_left_tips.png", _leftHand, new Vector3(-0.15f, -0.9f, 0.5f));
                yield return ShootHand("hands_left_rear.png", _leftHand, new Vector3(-0.4f, 0.5f, -1.0f));
                yield return ShootHand("hands_left_front.png", _leftHand, new Vector3(-0.05f, 0.15f, 1.0f));
            }

            // --- 2. AXE HELD, idle (grip curl active — the prime-suspect surface the old gate never framed) ---
            yield return SelectWeapon(ItemCatalog.AxeId, "idle_axe");
            yield return GridShots("idle_axe", shootLeft: true, tips: true, rear: true);
            // Dead-front gripping shot (86cahnmjv soak-fail #2) — the same head-on angle as empty idle, so
            // empty-vs-gripping right-hand reads are directly comparable from the Sponsor's view direction.
            if (_rightHand != null)
                yield return ShootHand("hands_idle_axe_right_front.png", _rightHand, new Vector3(0.05f, 0.15f, 1.0f));

            // --- 3. SPEAR HELD, idle (#232 — the newest grip surface; spear haft is thinner than the axe's) ---
            yield return SelectWeapon(ItemCatalog.SpearId, "idle_spear");
            yield return GridShots("idle_spear", shootLeft: true, tips: true, rear: true);

            // --- 4. WALK with the axe (curl composes onto the Walk clip's finger pose; two gait phases) ---
            yield return SelectWeapon(ItemCatalog.AxeId, "walk_axe");
            if (_player != null) { _player.SetInputOverride(new Vector2(0f, 1f)); _player.SetSprintOverride(false); }
            yield return WaitSettle(1.4f);
            LogState("walk_axe");
            yield return GridShots("walk_axe", shootLeft: true, tips: true, rear: false);
            yield return WaitSettle(0.35f); // ~a third of the gait cycle — a second phase
            yield return ShootHand("hands_walk_axe_right_p2.png", _rightHand, new Vector3(0.7f, 0.35f, 1.0f));

            // --- 5. RUN with the axe (the run clip pumps the arms — the widest hand-pose range) ---
            if (_player != null) _player.SetSprintOverride(true);
            yield return WaitSettle(1.4f);
            LogState("run_axe");
            yield return GridShots("run_axe", shootLeft: true, tips: true, rear: false);
            yield return WaitSettle(0.25f);
            yield return ShootHand("hands_run_axe_right_p2.png", _rightHand, new Vector3(0.7f, 0.35f, 1.0f));
            if (_player != null) { _player.ClearSprintOverride(); _player.ClearInputOverride(); }
            yield return WaitSettle(1.0f); // settle back to idle

            // --- 6. CROUCH idle with the axe (the Crouching Idle clip's hand pose + curl) ---
            if (_player != null) _player.SetCrouchOverride(true);
            yield return WaitSettle(1.2f);
            LogState("crouch_axe");
            yield return GridShots("crouch_axe", shootLeft: true, tips: true, rear: false);
            if (_player != null) _player.ClearCrouchOverride();
            yield return WaitSettle(1.0f);

            // --- 7. CHOP SWING with the axe (the Attack clip animates the fingers; three swing phases) ---
            LogState("chop_axe");
            if (_castaway != null)
            {
                _castaway.TriggerChop();
                yield return ShootTimed("hands_chop_axe_right_t1.png", _rightHand, new Vector3(0.7f, 0.35f, 1.0f), 0.15f);
                yield return ShootTimed("hands_chop_axe_right_t2.png", _rightHand, new Vector3(0.7f, 0.35f, 1.0f), 0.2f);
                yield return ShootTimed("hands_chop_axe_right_t3.png", _rightHand, new Vector3(0.4f, 0.5f, -1.0f), 0.25f);
                yield return WaitSettle(1.2f); // let the one-shot return to idle
            }

            // --- 8. WALK EMPTY-HANDED (isolates clip-pose vs curl if a walk_axe frame shows a defect) ---
            yield return SelectEmptyBeltSlot("walk_empty");
            if (_player != null) { _player.SetInputOverride(new Vector2(0f, 1f)); _player.SetSprintOverride(false); }
            yield return WaitSettle(1.4f);
            LogState("walk_empty");
            yield return GridShots("walk_empty", shootLeft: true, tips: true, rear: false);
            if (_player != null) _player.ClearInputOverride();

            Debug.Log("[HandsVerifyCapture] verification complete -> " + _dir +
                      (_stateEngageFailed ? " (STATE-ENGAGE FAILURE — see [hands-state] lines)" : ""));
            Application.Quit(_stateEngageFailed ? 1 : 0);
        }

        // Standard per-state shot set: right outer-front (always) + optional right tips/rear + left outer-front.
        private IEnumerator GridShots(string state, bool shootLeft, bool tips, bool rear)
        {
            if (_rightHand != null)
            {
                yield return ShootHand($"hands_{state}_right.png", _rightHand, new Vector3(0.7f, 0.35f, 1.0f));
                if (tips)
                    yield return ShootHand($"hands_{state}_right_tips.png", _rightHand, new Vector3(0.15f, -0.9f, 0.5f));
                if (rear)
                    yield return ShootHand($"hands_{state}_right_rear.png", _rightHand, new Vector3(0.4f, 0.5f, -1.0f));
            }
            if (shootLeft && _leftHand != null)
                yield return ShootHand($"hands_{state}_left.png", _leftHand, new Vector3(-0.7f, 0.35f, 1.0f));
        }

        // Grant (idempotent) + belt-select the weapon through the REAL inventory seams, then verify the gate
        // actually engaged (selection + grip). A state that silently fails to engage would produce a lying
        // "clean" frame — the unsoakable-placeholder class — so engage-failure fails the run.
        private IEnumerator SelectWeapon(string itemId, string state)
        {
            if (_inventory == null)
            {
                Debug.LogError($"[hands-state] {state}: NO Inventory in scene — cannot drive the held-weapon state");
                _stateEngageFailed = true;
                yield break;
            }
            if (itemId == ItemCatalog.AxeId) _inventory.PickUpAxe();
            else if (itemId == ItemCatalog.SpearId) _inventory.PickUpSpear();
            int idx = FindBeltIndex(itemId);
            if (idx < 0)
            {
                Debug.LogError($"[hands-state] {state}: '{itemId}' not found in any belt slot after pickup");
                _stateEngageFailed = true;
                yield break;
            }
            _inventory.Model.SelectBelt(idx);
            // Let the selection propagate (HeldTool renderer toggle + CastawayFingerCurl gate are Changed-event
            // driven; the curl applies from the next LateUpdate) and the held visual settle in-hand.
            for (int i = 0; i < 12; i++) yield return null;
            bool axeSel = _inventory.IsAxeSelectedInBelt, spearSel = _inventory.IsSpearSelectedInBelt;
            bool expected = itemId == ItemCatalog.AxeId ? axeSel : spearSel;
            bool gripping = _curl != null && _curl.IsGripping;
            Debug.Log($"[hands-state] {state}: selected belt[{idx}]='{itemId}' axeSel={axeSel} spearSel={spearSel} " +
                      $"gripping={gripping}");
            if (!expected || !gripping)
            {
                Debug.LogError($"[hands-state] {state}: state did NOT engage (expected '{itemId}' selected + " +
                               "gripping) — the grid frame would lie; failing the run");
                _stateEngageFailed = true;
            }
        }

        // Select the first EMPTY belt slot so no weapon is shown (the empty-hand states after weapons were granted).
        private IEnumerator SelectEmptyBeltSlot(string state)
        {
            if (_inventory == null) yield break;
            var belt = _inventory.Model.BeltSlots;
            int idx = -1;
            for (int i = 0; i < belt.Count; i++)
                if (belt[i].IsEmpty) { idx = i; break; }
            if (idx < 0)
            {
                Debug.LogError($"[hands-state] {state}: no empty belt slot to deselect weapons into");
                _stateEngageFailed = true;
                yield break;
            }
            _inventory.Model.SelectBelt(idx);
            for (int i = 0; i < 12; i++) yield return null;
            bool gripping = _curl != null && _curl.IsGripping;
            Debug.Log($"[hands-state] {state}: selected EMPTY belt[{idx}] gripping={gripping}");
            if (gripping)
            {
                Debug.LogError($"[hands-state] {state}: curl STILL gripping with an empty slot selected — " +
                               "gate regression; failing the run");
                _stateEngageFailed = true;
            }
        }

        private int FindBeltIndex(string itemId)
        {
            var belt = _inventory.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
                if (!belt[i].IsEmpty && belt[i].Def != null && belt[i].Def.Id == itemId) return i;
            return -1;
        }

        private IEnumerator WaitSettle(float seconds)
        {
            float start = Time.time;
            while (Time.time - start < seconds) yield return null;
        }

        // One line of live-state evidence per grid state — proves in the log WHICH state each frame set was
        // shot under (selection + grip + locomotion flags), so a "clean"/"defective" verdict is attributable.
        private void LogState(string state)
        {
            bool axeSel = _inventory != null && _inventory.IsAxeSelectedInBelt;
            bool spearSel = _inventory != null && _inventory.IsSpearSelectedInBelt;
            bool gripping = _curl != null && _curl.IsGripping;
            bool walking = _castaway != null && _castaway.IsWalking;
            bool running = _castaway != null && _castaway.IsRunning;
            bool crouching = _castaway != null && _castaway.IsCrouching;
            Debug.Log($"[hands-state] {state}: axeSel={axeSel} spearSel={spearSel} gripping={gripping} " +
                      $"walking={walking} running={running} crouching={crouching}");
        }

        // One tightly-framed hand shot. The camera RE-FRAMES EVERY settle frame (model-relative view direction),
        // so a translating/turning character (walk/run/chop) keeps the hand centred — the original static one-shot
        // framing only worked for the pinned idle. viewDir is expressed in the MODEL (facing) frame: +Z = the way
        // the character faces; for the yaw-0 idle baseline this reproduces the original world-space framing.
        private IEnumerator ShootHand(string fileName, Transform hand, Vector3 viewDir)
        {
            if (hand == null) yield break;
            for (int i = 0; i < 6; i++)
            {
                FrameHand(hand, viewDir);
                yield return null;
            }
            FrameHand(hand, viewDir);
            LogHandPose(fileName, hand);
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(_dir, fileName);
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[HandsVerifyCapture] wrote " + file);
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        // A time-anchored shot (chop swing phases): wait delaySeconds re-framing every frame, then capture
        // immediately (no extra settle — the swing pose is transient).
        private IEnumerator ShootTimed(string fileName, Transform hand, Vector3 viewDir, float delaySeconds)
        {
            if (hand == null) yield break;
            float start = Time.time;
            while (Time.time - start < delaySeconds)
            {
                FrameHand(hand, viewDir);
                yield return null;
            }
            FrameHand(hand, viewDir);
            LogHandPose(fileName, hand);
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(_dir, fileName);
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[HandsVerifyCapture] wrote " + file);
        }

        private void FrameHand(Transform hand, Vector3 viewDirModel)
        {
            // Centre the frame slightly DOWN the hand toward the fingers so the digits — the symptom — are
            // centred, not cropped (the arm hangs, so world-down biases toward the fingertips).
            Vector3 center = hand.position + Vector3.down * (handHalfExtent * 0.4f);
            Quaternion modelRot = _castaway != null && _castaway.ModelTransform != null
                ? _castaway.ModelTransform.rotation : Quaternion.identity;
            Vector3 viewDir = modelRot * viewDirModel;
            var frame = VerifyCaptureFraming.ComputeFrame(center, _boxSize, viewDir, _cam.fieldOfView, _aspect, 0.85f);
            _camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
        }

        // The rig-layer trace behind every frame: the live LOCAL euler of each curl-covered bone at shot time.
        // If a frame shows a defective digit, these lines pin WHICH bone/joint/axis without a second build.
        private void LogHandPose(string fileName, Transform hand)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[hands-pose] ").Append(fileName).Append(" gripping=")
              .Append(_curl != null && _curl.IsGripping);
            if (_curl != null)
            {
                AppendBoneEulers(sb, " fingers:", _curl.fingerBones);
                AppendBoneEulers(sb, " thumbs:", _curl.thumbBones);
            }
            Debug.Log(sb.ToString());
        }

        private static void AppendBoneEulers(System.Text.StringBuilder sb, string label, Transform[] bones)
        {
            sb.Append(label);
            if (bones == null) { sb.Append("<null>"); return; }
            foreach (var b in bones)
            {
                if (b == null) continue;
                Vector3 e = b.localRotation.eulerAngles;
                sb.Append(' ').Append(ShortBone(b.name)).Append('=')
                  .Append($"({Norm(e.x):F0},{Norm(e.y):F0},{Norm(e.z):F0})");
            }
        }

        private static float Norm(float deg) => deg > 180f ? deg - 360f : deg;

        private static string ShortBone(string name)
        {
            int colon = name.LastIndexOf(':');
            return colon >= 0 ? name.Substring(colon + 1) : name;
        }

        // Enumerate EVERY SMR bone under either wrist and whether the grip curl covers it. An uncovered
        // right-hand digit bone (with the curl active) keeps the OPEN clip pose while its neighbours curl —
        // the "one finger sticks out" candidate this dump exists to catch or rule out.
        private void DumpHandRigCoverage(SkinnedMeshRenderer avatar)
        {
            var covered = new HashSet<Transform>();
            if (_curl != null)
            {
                if (_curl.fingerBones != null) foreach (var b in _curl.fingerBones) if (b != null) covered.Add(b);
                if (_curl.thumbBones != null) foreach (var b in _curl.thumbBones) if (b != null) covered.Add(b);
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[hands-rig] ===== HAND-BONE CURL COVERAGE (SMR bone array) =====");
            int uncoveredRight = 0;
            if (avatar != null && avatar.bones != null)
            {
                foreach (var bone in avatar.bones)
                {
                    if (bone == null) continue;
                    string tok = ExactBoneToken(bone.name);
                    bool isRight = tok.StartsWith("righthand") && tok != "righthand";
                    bool isLeft = tok.StartsWith("lefthand") && tok != "lefthand";
                    if (!isRight && !isLeft) continue;
                    bool isCovered = covered.Contains(bone);
                    if (isRight && !isCovered) uncoveredRight++;
                    sb.AppendLine($"[hands-rig] {bone.name} hand={(isRight ? "R" : "L")} " +
                                  $"curlCovered={isCovered}");
                }
            }
            sb.AppendLine($"[hands-rig] uncovered RIGHT-hand digit bones: {uncoveredRight} " +
                          "(>0 with a grip defect frame => the uncurled-digit hypothesis; " +
                          "expected 0 on the 4-fingered Hyper3D hand where all 12 digit bones are covered)");
            sb.AppendLine("[hands-rig] ===== END HAND-BONE CURL COVERAGE =====");
            Debug.Log(sb.ToString());
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
