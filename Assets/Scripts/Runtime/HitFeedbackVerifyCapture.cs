using System.Collections;
using System.IO;
using UnityEngine;
using FarHorizon.Combat;
using FarHorizon.Juice;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for ENEMY BODY-LEVEL HIT FEEDBACK (ticket 86caxjwb3 AC7): prove
    /// in the BUILT exe — never just the editor — that a landed hit READS on the creature's own body, that all
    /// THREE channels fire, and above all that <b>the flash comes BACK DOWN</b>.
    ///
    /// === WHY THE "AFTER" FRAME IS THE POINT OF THIS GATE ([DFC-1]) ===
    /// The failure this exists to catch does not look like a failure on the impact frame. If the flash decay is
    /// driven from a C#-written <c>Time.time</c> stamp differenced against the shader's <c>_Time.y</c> — which is
    /// <c>Time.timeSinceLevelLoad</c>, not <c>Time.time</c> — the numerator is a CONSTANT NEGATIVE and the term
    /// saturates to 1.0 FOREVER: a permanently white creature from the first hit until the process exits. The
    /// impact frame of that build looks CORRECT. An EditMode test asserting "SetFloat was called with a rising
    /// timestamp" is GREEN on it. Only a frame taken a beat LATER, in a real player, can see it. So this gate
    /// shoots an UN-HIT control, the impact, and then the SAME creature at the SAME framing ~0.5 s later, and
    /// self-asserts the material value has returned to exactly 0.
    ///
    /// === WHY THE AI IS PARKED FOR THE MEASUREMENT WINDOW (and why that is not cheating) ===
    /// The un-hit control frame and the after-decay frame are only meaningful as a PAIR — they have to be the
    /// same creature at the same framing, or "it went back to base colour" is unfalsifiable. So the player WALKS
    /// in through the production <see cref="WasdMovement"/> override seam (nothing teleports), and only THEN is
    /// the creature's AI component parked so the three frames are comparable. The AI loop itself — aggro,
    /// windup, charge, gore, the kill, the despawn — is proven end-to-end by <see cref="BoarVerifyCapture"/>;
    /// this gate deliberately does NOT re-prove it, it isolates the feedback read.
    ///
    /// Also self-asserted, because each is a defect class that leaves every other assertion green:
    ///  • ALL of a creature's part-materials flash TOGETHER (7 boar / 13 snake) — a flash on the body but not
    ///    the head reads as a bug, not as juice, and a naive singular GetComponentInChildren gives exactly that;
    ///  • the SHARED path drives a SECOND creature with no per-enemy branch;
    ///  • the pooled puff actually RECYCLES ([DFC-4c]: without stopAction = Callback the pool leaks silently);
    ///  • the puff material's shader RESOLVES in the shipped player and is not Unity's error shader ([DFC-4b]:
    ///    the magenta strip class — this gate is the "verify in the BUILT exe, do not assume" half).
    ///
    /// Inert unless launched with -verifyHitFeedback:
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHitFeedback -captureDir &lt;dir&gt;
    /// Captures: hit_boar_unhit.png, hit_boar_impact.png, hit_boar_flinch.png, hit_boar_after_decay.png,
    ///           hit_snake_impact.png, hit_snake_after_decay.png, hit_boar_death.png.
    /// </summary>
    public class HitFeedbackVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // Per-creature measurement results (a coroutine cannot return a value; these are the shared slate).
        private bool _restedBefore, _flashedAllTogether, _flinched, _decayedToZero, _puffed;
        private int _seenMaterials;

        void Start()
        {
            if (HasArg("-verifyHitFeedback"))
            {
                // Run-in-background for THIS launch only (an unfocused window pauses the player mid-coroutine
                // and hangs the gate — the SnakeVerifyCapture lesson). Normal play keeps pause-on-unfocus.
                Application.runInBackground = true;
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            Camera cam = Camera.main;
            var emitter = Object.FindAnyObjectByType<PooledBurstEmitter>();
            var boarAi = Object.FindAnyObjectByType<BoarAI>();
            var snakeAi = Object.FindAnyObjectByType<SnakeAI>();
            EnemyHitFeedback boarFb = boarAi != null ? boarAi.GetComponent<EnemyHitFeedback>() : null;
            EnemyHitFeedback snakeFb = snakeAi != null ? snakeAi.GetComponent<EnemyHitFeedback>() : null;
            Health boarHp = boarAi != null ? boarAi.GetComponent<Health>() : null;
            Health snakeHp = snakeAi != null ? snakeAi.GetComponent<Health>() : null;
            MeleeAttack attack = player != null ? player.GetComponent<MeleeAttack>() : null;

            bool wired = cam != null && player != null && attack != null && emitter != null &&
                         boarFb != null && snakeFb != null && boarHp != null && snakeHp != null;
            Debug.Log("[HitFeedbackVerifyCapture] wired=" + wired + " (cam=" + (cam != null) +
                      " player=" + (player != null) + " attack=" + (attack != null) +
                      " emitter=" + (emitter != null) + " boarFb=" + (boarFb != null) +
                      " snakeFb=" + (snakeFb != null) + ")");
            if (!wired)
            {
                Debug.Log("[HitFeedbackVerifyCapture] GATE-FAIL: scene wiring incomplete");
                Application.Quit(1);
                yield break;
            }

            // [DFC-4c] the pooling contract, read off the AUTHORED template in the shipped player. Without
            // Callback the pool never recycles and the "pooled" claim is silently false.
            bool stopActionOk = emitter.TemplateStopActionIsCallback;
            // [DFC-4b] the strip question, answered in the BUILT exe rather than assumed. A stripped shader
            // resolves to Unity's error shader (magenta); a wrong shader NAME resolves to null at author time.
            var psr = emitter.template != null ? emitter.template.GetComponent<ParticleSystemRenderer>() : null;
            var puffMat = psr != null ? psr.sharedMaterial : null;
            string puffShaderName = puffMat != null && puffMat.shader != null ? puffMat.shader.name : "<null>";
            bool puffShaderOk = puffMat != null && puffMat.shader != null &&
                                !puffShaderName.Contains("InternalError") && !puffShaderName.Contains("Hidden/");
            bool puffMeshOk = psr != null && psr.mesh != null && psr.mesh.vertexCount > 0;
            Debug.Log($"[HitFeedbackVerifyCapture] pool: stopAction=Callback? {stopActionOk} " +
                      $"puffShader='{puffShaderName}' ok={puffShaderOk} chunkMeshVerts=" +
                      (psr != null && psr.mesh != null ? psr.mesh.vertexCount : 0) + " ok=" + puffMeshOk);

            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            WeaponDef axe = catalog.ById(WeaponCatalog.AxeId);

            // Freeze HP-over-time on the player so a gore/bleed ticker can't perturb the window.
            SnakeVerifyCapture.FreezeHpOverTime(player.gameObject);
            for (int i = 0; i < 30; i++) yield return null; // settle: agents on mesh, camera framed

            // ---------------- BOAR: the full six-frame read ----------------
            int emitsBefore = emitter.EmitCount;
            yield return StartCoroutine(MeasureCreature(cam, attack, axe, boarAi.transform, boarFb, boarHp,
                                                        emitter, BoarBodyRig.PartCount, dir, "hit_boar"));
            bool boarRested = _restedBefore, boarTogether = _flashedAllTogether, boarFlinched = _flinched,
                 boarDecayed = _decayedToZero, boarPuffed = _puffed;
            int boarMats = _seenMaterials;

            // ---------------- SNAKE: the SHARED path on a SECOND creature ----------------
            // 13 = head + SnakeBodyLinks(12). If the driver had ANY per-enemy branch, or resolved renderers
            // singularly, this count is where it shows.
            yield return StartCoroutine(MeasureCreature(cam, attack, axe, snakeAi.transform, snakeFb, snakeHp,
                                                        emitter, 13, dir, "hit_snake"));
            bool snakeTogether = _flashedAllTogether, snakeDecayed = _decayedToZero;
            int snakeMats = _seenMaterials;

            // ---------------- DEATH: the softer dust beat (AC4) ----------------
            // Back to the boar and finish it. Without a death puff the soak's death moment has NO feedback at
            // all and the "is it nearly down?" read is only half-testable.
            // Re-ARM the boar's AI first. MeasureCreature parked it so the before/after frames were comparable,
            // but BoarAI subscribes to Health.Died in OnEnable — leaving it parked would mean the kill never
            // transitions to BoarState.Dead and hit_boar_death.png would show a live-posed boar with no settle,
            // silently gutting the AC7 "death settle + death puff" frame while every assertion still passed.
            if (boarAi != null) boarAi.enabled = true;
            for (int i = 0; i < 5; i++) yield return null;
            YawAt(cam, boarAi != null ? boarAi.transform : null);
            int deathPuffsBefore = boarFb.DeathPuffCount;
            int hits = 0;
            while (!boarHp.IsDead && hits < 14)
            {
                attack.PerformAttack(axe, boarHp);
                hits++;
                yield return new WaitForSeconds(0.25f);
            }
            for (int i = 0; i < 3; i++) yield return null;
            ShotTo(Path.Combine(dir, "hit_boar_death.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            bool deathPuffed = boarFb.DeathPuffCount > deathPuffsBefore;
            Debug.Log($"[HitFeedbackVerifyCapture] death: axeHits={hits} dead={boarHp.IsDead} " +
                      $"deathPuff={deathPuffed} (DeathPuffCount={boarFb.DeathPuffCount})");

            // ---------------- POOL INTEGRITY: it must RECYCLE, not accumulate ----------------
            // Wait for every checked-out instance to come home through OnParticleSystemStopped, then compare
            // instances CREATED against bursts EMITTED. If the stop-action were wrong, LiveCount never falls.
            float poolDeadline = Time.time + 6f;
            while (emitter.LiveCount > 0 && Time.time < poolDeadline) yield return null;
            int emitted = emitter.EmitCount - emitsBefore;
            // RECYCLED = every checked-out instance came home through OnParticleSystemStopped. BOUNDED = the
            // pool created strictly FEWER instances than it served bursts, i.e. it genuinely reused them.
            // (Deliberately NOT `CreatedCount <= maxPoolSize`: ObjectPool's maxSize bounds RETAINED instances,
            // not concurrently-live ones — that assert would red on a legitimate burst overlap.)
            bool poolRecycled = emitter.ReleaseCount >= emitted && emitter.LiveCount == 0;
            bool poolBounded = emitted < 2 || emitter.CreatedCount < emitted;
            Debug.Log($"[HitFeedbackVerifyCapture] pool: emitted={emitted} created={emitter.CreatedCount} " +
                      $"released={emitter.ReleaseCount} live={emitter.LiveCount} maxPoolSize={emitter.maxPoolSize} " +
                      $"recycled={poolRecycled} bounded={poolBounded}");

            bool pass = stopActionOk && puffShaderOk && puffMeshOk &&
                        boarRested && boarTogether && boarFlinched && boarDecayed && boarPuffed &&
                        snakeTogether && snakeDecayed &&
                        boarMats == BoarBodyRig.PartCount && snakeMats == 13 &&
                        boarHp.IsDead && deathPuffed && poolRecycled && poolBounded;

            Debug.Log($"[HitFeedbackVerifyCapture] GATE {(pass ? "PASS" : "FAIL")}: " +
                      $"stopAction={stopActionOk} puffShader={puffShaderOk} chunkMesh={puffMeshOk} " +
                      $"boarRestedBefore={boarRested} boarAllPartsTogether={boarTogether}({boarMats}/{BoarBodyRig.PartCount}) " +
                      $"boarFlinched={boarFlinched} boarDecayedToZero={boarDecayed} boarPuffed={boarPuffed} " +
                      $"snakeAllPartsTogether={snakeTogether}({snakeMats}/13) snakeDecayedToZero={snakeDecayed} " +
                      $"died={boarHp.IsDead} deathPuff={deathPuffed} poolRecycled={poolRecycled} " +
                      $"poolBounded={poolBounded} -> {dir}");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// Walk to a creature through the PRODUCTION movement seam, park its AI so the before/after frames are
        /// comparable, then shoot the four-frame read: un-hit control → impact → flinch → after-decay.
        /// Results land on the shared slate fields.
        /// </summary>
        private IEnumerator MeasureCreature(Camera cam, MeleeAttack attack, WeaponDef weapon, Transform target,
                                            EnemyHitFeedback fb, Health hp, PooledBurstEmitter emitter,
                                            int expectedMaterials, string dir, string prefix)
        {
            _restedBefore = _flashedAllTogether = _flinched = _decayedToZero = _puffed = false;
            _seenMaterials = 0;

            // --- WALK IN through the same input-independent override seam the snake/boar captures use (the
            //     actual gameplay Update path — nothing teleports). ---
            float walkDeadline = Time.time + 22f;
            while (Time.time < walkDeadline)
            {
                Vector3 to = target.position - player.transform.position;
                to.y = 0f;
                if (to.magnitude <= 3.0f) break;
                player.SetInputOverride(WorldDirToInput(cam, to.normalized));
                yield return null;
            }
            player.SetInputOverride(Vector2.zero);

            // --- PARK the AI so the un-hit control and the after-decay frame are the SAME framing (see the
            //     class note — that pairing is the whole discriminator). The BODY RIG stays live, so the
            //     creature keeps breathing / posing; only its decision-making is held. ---
            var boarAi = target.GetComponent<BoarAI>();
            var snakeAi = target.GetComponent<SnakeAI>();
            if (boarAi != null) boarAi.enabled = false;
            if (snakeAi != null) snakeAi.enabled = false;
            for (int i = 0; i < 20; i++) yield return null;
            YawAt(cam, target);
            for (int i = 0; i < 5; i++) yield return null;

            // --- PIN THE CLOCK for the measurement window. MEASURED, not guessed: the FIRST run of this gate
            //     reported peakFlinchOffset=0 and minFlash@impact=0 on BOTH creatures while the puff fired
            //     normally — because ScreenCapture.CaptureScreenshot stalls a frame hard, and the resulting
            //     wall-clock hitch is LONGER than the whole 0.08 s flash and 0.22 s flinch. Both phases are
            //     Time.time-anchored, so they began and completed inside one hitched frame and every sample
            //     read a resting body. The feature was fine; the RULER was broken. Time.captureDeltaTime pins
            //     Time.time to a fixed virtual step per frame no matter how long the frame really takes
            //     (unity-conventions.md §Headless — the documented remedy for exactly this class), so the
            //     captures land at deterministic points on the curve and are comparable run to run. ---
            float prevCaptureDt = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;

            // --- (e) UN-HIT CONTROL: no residual tint before anything has happened. ---
            _seenMaterials = fb.MaterialCount;
            float restFlash = fb.MaxMaterialFlash();
            _restedBefore = restFlash <= 0.0001f;
            ShotTo(Path.Combine(dir, prefix + "_unhit.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // --- (a) IMPACT: land a real hit through the production MeleeAttack seam, then shoot the very next
            //     frame. Assert the MINIMUM across every part-material is lit — ALL parts together. ---
            int emitsBefore = emitter.EmitCount;
            attack.PerformAttack(weapon, hp);
            // ONE pinned frame: the impulse's eased rise reaches full amplitude at 22 % of the flash window, so
            // at 1/60 s per frame into an 0.08 s flash this lands at ~0.21 — essentially the peak. (Frame 0 is
            // the strike frame itself, where the curve is 0 BY DESIGN — it starts at rest.)
            yield return null;
            float minFlash = fb.MinMaterialFlash();
            float maxFlash = fb.MaxMaterialFlash();
            _flashedAllTogether = _seenMaterials == expectedMaterials && minFlash > 0.0001f;
            _puffed = emitter.EmitCount > emitsBefore;
            ShotTo(Path.Combine(dir, prefix + "_impact.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // --- (b) FLINCH a few frames later: the body has visibly recoiled off its posed rest. ---
            float flinchDeadline = Time.time + Mathf.Max(0.4f, fb.flinchSeconds * 2f);
            float peakOffset = 0f;
            bool shotFlinch = false;
            while (Time.time < flinchDeadline)
            {
                peakOffset = Mathf.Max(peakOffset, fb.FlinchOffset.magnitude);
                if (!shotFlinch && fb.FlinchNormT > 0.3f)
                {
                    shotFlinch = true;
                    ShotTo(Path.Combine(dir, prefix + "_flinch.png"));
                }
                if (fb.FlinchNormT <= 0f && shotFlinch) break;
                yield return null;
            }
            _flinched = peakOffset > 0.001f;

            // --- (f) AFTER-DECAY, THE LATCH DISCRIMINATOR: same creature, same framing, ~0.5 s after the hit.
            //     A latched flash looks CORRECT on the impact frame and only shows here. ---
            yield return new WaitForSeconds(0.5f);
            yield return null;
            float afterFlash = fb.MaxMaterialFlash();
            _decayedToZero = afterFlash <= 0.0001f;
            ShotTo(Path.Combine(dir, prefix + "_after_decay.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            Time.captureDeltaTime = prevCaptureDt; // unpin — the walk to the NEXT creature runs at real time

            Debug.Log($"[HitFeedbackVerifyCapture] {prefix}: materials={_seenMaterials}/{expectedMaterials} " +
                      $"restFlash={restFlash:F4} minFlash@impact={minFlash:F4} maxFlash@impact={maxFlash:F4} " +
                      $"peakFlinchOffset={peakOffset:F4} flashAfter0.5s={afterFlash:F4} " +
                      $"restedBefore={_restedBefore} allTogether={_flashedAllTogether} flinched={_flinched} " +
                      $"decayedToZero={_decayedToZero} puffed={_puffed}");
        }

        // Yaw the REAL orbit rig at the subject as a player would (pitch / distance / FOV stay the true
        // gameplay values — a non-representative FOV is the documented false-green class).
        private static void YawAt(Camera cam, Transform target)
        {
            if (target == null) return;
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit == null) return;
            var player = Object.FindAnyObjectByType<WasdMovement>();
            if (player == null) return;
            Vector3 to = target.position - player.transform.position;
            if (to.sqrMagnitude > 1e-4f) orbit.SetYaw(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        }

        private static Vector2 WorldDirToInput(Camera cam, Vector3 worldDir)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = cam.transform.right; right.y = 0f; right.Normalize();
            return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd)).normalized;
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[HitFeedbackVerifyCapture] wrote " + file);
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
