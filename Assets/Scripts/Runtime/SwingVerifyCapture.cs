using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the PER-CLASS WEAPON SWINGS (ticket 86caffwv5 — attack
    /// animation per weapon).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving the swings
    /// play in the BUILT exe — not just the editor (the legs-up divergence class). This drives each of the 5
    /// per-class swings and captures each mid-strike so the Sponsor's soak (+ the PR reviewer) judge the real
    /// runtime read of every class's motion.
    ///
    /// HOW: for each WeaponClass 0..4 it calls <see cref="CastawayCharacter.TriggerAttack"/> (the SAME seam the
    /// gameplay left-click + tree-chop/mine verbs use) — which sets the Animator's WeaponClass int + ChopSpeed and
    /// pulses the shared Chop trigger, so the controller plays that class's AttackX one-shot. It then lets REAL
    /// frames advance the Animator (a swing pose only advances with real frames — headless deltaTime≈0 never poses
    /// it, so this MUST run WINDOWED) and captures a frame ~mid-swing from the REAL OrbitCamera (gameplay framing —
    /// an isolated hero rig is the false-green class, unity-conventions.md §"capture must use the GAMEPLAY camera").
    ///
    /// SELF-ASSERTS (LOGIC — auditable in a headless log too):
    ///   • each class routed: after each TriggerAttack, <see cref="CastawayCharacter.LastWeaponClass"/> == the class
    ///     (proves the per-class routing fired for the right class, independent of whether the pose is observable);
    ///   • CONE-EXPLOSION GUARD (the Generic-rig bind, 86ca8rdkp): the skinned mesh bounds stay within a sane radius
    ///     of the player across every swing (a Humanoid-retarget explosion flings it thousands of units off-spawn).
    /// Fails non-zero if any class failed to route OR the mesh ever exploded.
    ///
    /// PICKAXE DEEP-FOLD PASS (86cav8xg9). The 5 shots above fire at a fixed 0.28s after TriggerAttack. For the
    /// pickaxe that is only t≈0.08 of its swing (a 5.20s clip at the 1.5× pickaxe playback ≈ 3.47s), so those shots
    /// land in the WIND-UP and structurally CANNOT show the mid-swing body fold the Sponsor flagged at soak-5 — a
    /// green swing_pickaxe.png said nothing about it (the false-green-capture class, unity-conventions.md
    /// §editor-vs-runtime: here the subject is in frame but at the wrong MOMENT). So after the five, this drives the
    /// pickaxe swing twice more: pass 1 MEASURES the live composed torso tilt (hips→head vs world up, sampled off
    /// the real runtime skeleton every frame — so it includes the Animator playback AND the CastawayArmPose
    /// composition, not just the authored curves) and records when it peaks; pass 2 re-fires and shoots that exact
    /// moment from BOTH the gameplay orbit cam and a dedicated SIDE-PROFILE cam. The side profile is required
    /// because an upright-vs-folded-over read is nearly invisible from the player's over-the-shoulder angle and
    /// obvious side-on (lowpoly-quality.md §0 — the pond lift→mound lesson).
    ///
    /// Inert unless launched with -verifySwings (the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySwings -captureDir &lt;dir&gt;
    /// Captures: swing_axe.png, swing_pickaxe.png, swing_dagger.png, swing_spear.png, swing_sword.png,
    ///           swing_pickaxe_fold.png (gameplay cam at the peak fold), swing_pickaxe_fold_side.png (side profile).
    /// </summary>
    public class SwingVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // A cone-explosion sends the mesh far off-spawn; a clean Generic bind keeps the mesh bounds within a few
        // units of the player root (the castaway is ~1.8u tall). 8u discriminates explosion-vs-clean unambiguously
        // (a cone blows past by thousands). Mirrors LocomotionHitReactVerifyCapture.ConeExplosionRadiusU.
        private const float ConeExplosionRadiusU = 8f;

        // === PICKAXE DEEP-FOLD PASS (86cav8xg9) ===
        // The pickaxe swing is a 5.20s clip at 1.5x playback ~= 3.47s; the fold peaks around t~0.56 (~1.95s in), so
        // the measure window must span well past it.
        private const float FoldWindowSec = 2.6f;
        // Live composed-pose ceiling for the torso lean off vertical. The authored raw clip measures 66.3deg and the
        // repaired clip 41deg (AttackClipPoseDiag); the axe swing's unflagged peak is 43.3deg. 50deg sits clear of
        // the repaired value (so real frame-timing jitter cannot flap the gate) yet far below the raw fold, so this
        // fires if the build ever plays the RAW clip again.
        private const float LiveFoldCeilingDeg = 50f;
        // Side-profile stand-off. The castaway is ~1.8u tall; 3u at 45deg FOV frames the whole body with headroom.
        private const float SideProfileDistU = 3f;

        // The 5 per-class swings, in WeaponClass order (mirror CastawayCharacter.WeaponClass*). Names drive the PNG.
        private static readonly (int weaponClass, string name)[] Swings =
        {
            (CastawayCharacter.WeaponClassAxe,     "axe"),
            (CastawayCharacter.WeaponClassPickaxe, "pickaxe"),
            (CastawayCharacter.WeaponClassDagger,  "dagger"),
            (CastawayCharacter.WeaponClassSpear,   "spear"),
            (CastawayCharacter.WeaponClassSword,   "sword"),
        };

        void Start()
        {
            if (HasArg("-verifySwings"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            NavMeshAgent agent = player != null ? player.GetComponent<NavMeshAgent>() : null;
            CastawayCharacter castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            Animator animator = castaway != null && castaway.ModelTransform != null
                ? castaway.ModelTransform.GetComponentInChildren<Animator>()
                : Object.FindAnyObjectByType<Animator>();
            SkinnedMeshRenderer smr = castaway != null && castaway.ModelTransform != null
                ? castaway.ModelTransform.GetComponentInChildren<SkinnedMeshRenderer>()
                : Object.FindAnyObjectByType<SkinnedMeshRenderer>();
            Transform playerRoot = player != null ? player.transform : (castaway != null ? castaway.transform : null);

            float t = 0f;
            while (t < 3f && (agent == null || !agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[SwingVerifyCapture] agent on NavMesh: " + (agent != null && agent.isOnNavMesh) +
                      " castaway=" + (castaway != null) + " animator=" + (animator != null) + " smr=" + (smr != null));
            for (int i = 0; i < 5; i++) yield return null;

            bool allRouted = castaway != null;
            float worstMeshGap = 0f;

            foreach (var (weaponClass, name) in Swings)
            {
                // Fire this class's swing through the production seam. Standing at spawn so the gameplay orbit cam
                // stays framed on the character (the swing frame must SHOW the motion, not empty terrain).
                bool routed = false;
                if (castaway != null)
                {
                    castaway.TriggerAttack(weaponClass, 1f);
                    routed = castaway.LastWeaponClass == weaponClass;
                }
                allRouted &= routed;
                Debug.Log($"[SwingVerifyCapture] fired swing class={weaponClass} ({name}) routed={routed} " +
                          $"(LastWeaponClass={(castaway != null ? castaway.LastWeaponClass : -1)})");

                // Let REAL frames advance the swing to ~mid-strike, then capture. Track the cone guard the whole window.
                float start = Time.time;
                bool shot = false;
                while (Time.time - start < 0.9f)
                {
                    worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                    if (!shot && Time.time - start > 0.28f)
                    {
                        ShotTo(Path.Combine(dir, "swing_" + name + ".png"));
                        shot = true;
                    }
                    yield return null;
                }
                if (!shot) ShotTo(Path.Combine(dir, "swing_" + name + ".png"));
                // Let the one-shot finish + return to idle before the next class (a clean start per swing).
                for (int i = 0; i < 18; i++) yield return null;
            }

            // ===== PICKAXE DEEP-FOLD PASS (86cav8xg9) =====
            float peakTilt = 0f;
            bool foldOk = true;
            if (castaway != null && animator != null)
            {
                Transform hips = FindBone(animator.transform, "mixamorig:Hips");
                Transform head = FindBone(animator.transform, "mixamorig:Head");
                if (hips == null || head == null)
                {
                    Debug.LogWarning("[SwingVerifyCapture] fold pass SKIPPED — mixamorig:Hips/Head not found on the " +
                                     "live rig; the mine-pose evidence is MISSING from this run (do not read a PASS " +
                                     "here as proof the fold is fixed).");
                }
                else
                {
                    // pass 1 — MEASURE. Sample the live composed torso tilt every frame; remember when it peaks.
                    castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
                    float start = Time.time, peakAt = 0f;
                    while (Time.time - start < FoldWindowSec)
                    {
                        float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                        if (tilt > peakTilt) { peakTilt = tilt; peakAt = Time.time - start; }
                        worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                        yield return null;
                    }
                    Debug.Log($"[SwingVerifyCapture] pickaxe fold pass 1: LIVE peak torso tilt {peakTilt:F1}deg off " +
                              $"vertical at +{peakAt:F2}s (measured on the runtime skeleton, so it includes the " +
                              "Animator playback AND the CastawayArmPose composition)");
                    for (int i = 0; i < 18; i++) yield return null;

                    // pass 2 — SHOOT that moment, gameplay cam then side profile.
                    castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
                    start = Time.time;
                    while (Time.time - start < peakAt) yield return null;
                    ShotTo(Path.Combine(dir, "swing_pickaxe_fold.png"));
                    yield return null;
                    yield return SideProfileShot(Path.Combine(dir, "swing_pickaxe_fold_side.png"), hips, head,
                                                castaway.ModelTransform);

                    foldOk = peakTilt <= LiveFoldCeilingDeg;
                    Debug.Log($"[SwingVerifyCapture] pickaxe fold: peakTilt={peakTilt:F1}deg <= " +
                              $"{LiveFoldCeilingDeg}deg ceiling => foldOk={foldOk}. The RAW clip measured 66.3deg " +
                              "(authored) — above the ceiling means the repaired .anim is not the clip being played.");
                }
            }

            yield return new WaitForSeconds(0.4f);

            bool meshStayed = smr != null && worstMeshGap <= ConeExplosionRadiusU;
            bool pass = allRouted && meshStayed && foldOk;
            Debug.Log($"[SwingVerifyCapture] verification complete -> {dir} allRouted={allRouted} " +
                      $"worstMeshGap={worstMeshGap:F2}u (<= {ConeExplosionRadiusU} = mesh stayed at the player, NO " +
                      $"cone-explosion — the Generic-rig bind, 86ca8rdkp) meshStayed={meshStayed} " +
                      $"pickaxePeakTilt={peakTilt:F1}deg foldOk={foldOk} => PASS={pass}");
            Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// One SIDE-PROFILE frame of the current pose — the angle an upright-vs-folded-over read is actually
        /// visible from (lowpoly-quality.md §0). Follows the #223 camera-race discipline: every other camera is
        /// disabled, this one takes depth 100, and the whole camera roster is logged — two enabled cameras at equal
        /// depth have UNDEFINED render order and the capture then intermittently samples the wrong one.
        /// </summary>
        private IEnumerator SideProfileShot(string file, Transform hips, Transform head, Transform facing)
        {
            var wasEnabled = new System.Collections.Generic.List<Camera>();
            foreach (var c in Camera.allCameras)
                if (c.enabled) { wasEnabled.Add(c); Debug.Log($"[swings-cam-roster] {c.name} depth={c.depth}"); }

            var go = new GameObject("__swingSideCam");
            var cam = go.AddComponent<Camera>();
            cam.depth = 100f;
            cam.fieldOfView = 45f;
            Vector3 centre = (hips.position + head.position) * 0.5f;
            // Stand off along the character's own RIGHT so the sagittal plane faces the lens (a true side profile),
            // at chest height and level — a raised/angled shot flattens the fold exactly like a top-down pond shot.
            // The side axis comes from the FACING-CARRYING model transform (CastawayCharacter yaws the _model child:
            // "the visual owns facing", unity-conventions.md §FBX/rigs) — NOT from a pelvis BONE axis, whose local
            // frame on an imported Mixamo rig is arbitrary and would aim the camera at a guessed angle.
            Vector3 side = facing != null ? facing.right : Vector3.right;
            side = Vector3.ProjectOnPlane(side, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            go.transform.position = centre + side.normalized * SideProfileDistU + Vector3.up * 0.1f;
            go.transform.LookAt(centre);
            foreach (var c in wasEnabled) c.enabled = false;
            yield return null;
            ShotTo(file);
            yield return null;
            foreach (var c in wasEnabled) c.enabled = true;
            Object.Destroy(go);
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // The skinned mesh's world-bounds center distance from the player root. A clean bind keeps this small; a
        // cone-explosion makes it huge. Returns 0 when no SMR (degenerate rig — the boot capture covers no-mesh).
        private static float MeshGap(SkinnedMeshRenderer smr, Transform playerRoot)
        {
            if (smr == null || playerRoot == null) return 0f;
            return Vector3.Distance(smr.bounds.center, playerRoot.position);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SwingVerifyCapture] wrote " + file);
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
