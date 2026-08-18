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
        // The live mode's shared slate (same reason: a coroutine cannot return a value).
        private bool _liveCreaturePass;

        void Start()
        {
            // ⚠ -verifyHitFeedbackLive is checked FIRST and is a DIFFERENT flag, not a modifier: the two modes
            // are mutually exclusive because the isolating mode PARKS the AI and the live mode must not.
            if (HasArg("-verifyHitFeedbackLive"))
            {
                Application.runInBackground = true;
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunLiveVerification());
                return;
            }
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
            // …and WALK BACK to it. The snake measurement leaves the player ~11 u away across the loop, and
            // PerformAttack reaches through the shared seam regardless of distance — so without this the kill
            // still "worked" while hit_boar_death.png framed the boar as a few dark pixels at the top of the
            // screen. AC7(d) wants the death SETTLE + death puff legible, and a frame nobody can read is not
            // evidence (the fixed-orbit-vs-subject-fit false-green family, `unity-conventions.md`
            // §Editor-vs-runtime).
            yield return StartCoroutine(WalkTo(cam, boarAi != null ? boarAi.transform : null, 3.0f, 22f));
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
            yield return StartCoroutine(WalkTo(cam, target, 3.0f, 22f));

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
            _puffed = emitter.EmitCount > emitsBefore;

            // SAMPLE ACROSS THE WINDOW, do not spot-check one frame — but note WHY, because the reason written
            // here originally was WRONG and the wrongness cost a red PlayMode test to find.
            //   (a) TRUE, and the reason the loop exists: `yield return null` resumes in the UPDATE phase of the
            //       next frame, so the freshest value it can read is the PREVIOUS frame's LateUpdate write.
            //   (b) ⚠ WHAT THIS COMMENT USED TO SAY, AND IT WAS A DEFECT, NOT A DESIGN: "the previous frame is
            //       the STRIKE frame, where the impulse is 0 BY DESIGN — the curve starts at rest, so exactly
            //       one yield after the strike always reads 0 on a perfectly working flash." That zero was NOT
            //       fine. It meant the creature rendered UNLIT on the contact frame in the shipped exe, because
            //       the flash was riding the flinch's eased-IN curve and being sampled at t = 0. Working around
            //       it HERE — taking a max across the rise so the gate stopped reporting it — is compensating in
            //       the INSTRUMENT instead of fixing the product, and it is exactly why this gate read 0.6150
            //       while `HitFeedbackPlayModeTests` read 0.0000: the two sampled different frames, and the test
            //       sampled the right one. Fixed at the source: the flash now rides `FlashImpulse01` (full at
            //       contact, eased OUT — AC2's "snap-then-fade"), so the strike frame is at PEAK.
            // The loop stays: it is still correct under (a), it keeps the peak, and it shoots the impact frame on
            // the first lit frame. It must NEVER again be the thing that makes a dark contact frame invisible.
            float minFlash = 0f, maxFlash = 0f;
            bool shotImpact = false;
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                float mx = fb.MaxMaterialFlash();
                if (mx > maxFlash) { maxFlash = mx; minFlash = fb.MinMaterialFlash(); }
                if (!shotImpact && mx > 0.0001f)
                {
                    shotImpact = true;
                    ShotTo(Path.Combine(dir, prefix + "_impact.png"));
                    yield return new WaitForEndOfFrame();
                }
                if (!fb.FlashActive && shotImpact) break;
            }
            // Always leave the artifact behind, even on a failure — a missing frame is worse evidence than a
            // frame showing an un-flashed creature.
            if (!shotImpact) ShotTo(Path.Combine(dir, prefix + "_impact.png"));
            _flashedAllTogether = _seenMaterials == expectedMaterials && minFlash > 0.0001f;
            // GROUND TRUTH on the burst, every run. `puffed=True` only says a burst was REQUESTED; it is green
            // on a burst that renders nothing, which is exactly what shipped once here (sub-pixel chunks). This
            // line names the live system's real position / particle count / renderer state / mesh extent, so an
            // invisible puff is DIAGNOSED from the log instead of guessed at from a frame.
            Debug.Log("[HitFeedbackVerifyCapture] " + prefix + " burst: " + emitter.DescribeLive() +
                      " contactPoint=" + fb.ContactPoint().ToString("F2"));
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

        // ==================================================================================================
        // ============================ THE LIVE GATE (-verifyHitFeedbackLive) ==============================
        // ==================================================================================================
        //
        // WHY A SECOND MODE EXISTS, WRITTEN THE DAY IT WAS PAID FOR. On 2026-08-14 the isolating gate above
        // PASSED against build `zoned | 2026-08-14T09:41:56Z | df5edf7` — snake materials 13/13 lit at 0.6200,
        // boar decayed to zero, deathPuff=True, pool recycled — and ~30 minutes later the Sponsor played THE
        // SAME EXE and reported: the snake never flashes, the boar flashes on the first hit only, and both
        // deaths are an instant disappearance. Two instruments disagreeing IS the finding, so this mode exists
        // to close the three specific gaps that let the first one be green on all three:
        //
        //  1. IT READ BACK ITS OWN WRITE. `MinMaterialFlash()` asks the material for the float the driver just
        //     SetFloat'd into it. That is circular: it is green whether or not a single PIXEL changed. This mode
        //     measures the creature's SCREEN-SPACE BOX in the actual framebuffer
        //     (ScreenCapture.CaptureScreenshotAsTexture) before and at the hit, and requires the rendered
        //     luminance to RISE. It also logs the box's px size — the figure that decides whether a 0.08 s pulse
        //     on a 20 px creature was ever perceivable at all (game-juice.md §2b: stated WITH its framing).
        //  2. IT PARKED THE AI AND CALLED PerformAttack DIRECTLY. `attack.PerformAttack(axe, hp)` skips the
        //     whole real click path — the click gate, the verb arbitration, the cooldown, and above all
        //     ResolveNearestTarget — and a parked creature never charges, lunges, staggers or walks out of
        //     reach. This mode leaves both AIs LIVE and drives `RequestAttackClick()`, so every hit is resolved
        //     the way the Sponsor's mouse resolves one, and it keeps hitting until the creature is DEAD.
        //  3. IT ASSERTED A COUNTER FOR THE DEATH. `deathPuff=True` came from `DeathPuffCount > 0`. A counter
        //     cannot see that the body stands frozen and upright for four seconds and then pops out of
        //     existence in one frame — which is what `hit_boar_death.png` shows, and what "they just disappear"
        //     means. This mode samples the body's POSE (mean part Y + uprightness) at death, through the
        //     settle, and one frame before the despawn.
        //
        // ⚠ WHAT THIS MODE DOES NOT OBSERVE — named, so no future reader mistakes its PASS for a total one:
        //   • It raises the PLAYER's HP for the run and logs that it did. The boar gores for 18 HP and the run
        //     spends ~30 s inside its charge range; without this the player dies and the gate is flaky rather
        //     than honest. The player's survival is not what is under judgement — the ENEMY's read is.
        //   • It does not judge TONE. "Is a 0.62 warm-white pulse the right amplitude" is a Sponsor call and no
        //     assert here can make it. It judges only that a rendered change EXISTS and is not a one-frame blip.
        //   • It does not observe AUDIO (there is none on this surface yet), the HUD, or the flinch's direction.
        //   • Its luminance read is a MEAN over the body box, so a flash on one part of a 13-part snake could
        //     in principle clear the threshold. The isolating gate's min-across-materials assert is what covers
        //     that half — the two modes are complementary, and neither replaces the other.
        private IEnumerator RunLiveVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            Camera cam = Camera.main;
            var emitter = Object.FindAnyObjectByType<PooledBurstEmitter>();
            var boarAi = Object.FindAnyObjectByType<BoarAI>();
            var snakeAi = Object.FindAnyObjectByType<SnakeAI>();
            MeleeAttack attack = player != null ? player.GetComponent<MeleeAttack>() : null;
            Health playerHp = player != null ? player.GetComponentInChildren<Health>() : null;

            bool wired = cam != null && player != null && attack != null && emitter != null &&
                         boarAi != null && snakeAi != null && playerHp != null;
            Debug.Log("[HitFeedbackLive] wired=" + wired + " (cam=" + (cam != null) + " player=" +
                      (player != null) + " attack=" + (attack != null) + " emitter=" + (emitter != null) +
                      " boar=" + (boarAi != null) + " snake=" + (snakeAi != null) + ")");
            if (!wired)
            {
                Debug.Log("[HitFeedbackLive] GATE-FAIL: scene wiring incomplete");
                Application.Quit(1);
                yield break;
            }

            // STATED DEVIATION 1: survive the boar's gore for the length of the run.
            SnakeVerifyCapture.FreezeHpOverTime(player.gameObject);
            playerHp.max = 100000f;
            playerHp.RestoreFull();
            Debug.Log("[HitFeedbackLive] DEVIATION 1: player HP raised to " + playerHp.Max.ToString("0") +
                      " so a 30s stay inside boar charge-range cannot end the run. The enemy read is what is " +
                      "under judgement; the player's survival is not.");

            // STATED DEVIATION 2, and it is a MEASUREMENT before it is a deviation. The first live run logged
            // `clicks=24 landed=0 swallowedClicks=24` on the boar, and the ClickGateDiagnostic named the cause:
            // `sel=- | melee wpn=0(-)` — A FRESH LAUNCH STARTS WITH AN EMPTY BELT. The isolating gate never met
            // this because it calls PerformAttack with a locally-built WeaponCatalog axe, so it hands the player
            // a weapon the real player has to FIND first. Granting it here keeps the fight testable; the finding
            // loop itself is WorldWeaponFind's surface, not this ticket's.
            var inventory = Object.FindAnyObjectByType<Inventory>();
            bool gotAxe = inventory != null && inventory.PickUpWeapon(ItemCatalog.AxeId);
            yield return null;
            int axeSlot = -1;
            if (inventory != null && inventory.Model != null)
            {
                var belt = inventory.Model.BeltSlots;
                for (int i = 0; i < belt.Count; i++)
                    if (!belt[i].IsEmpty && belt[i].Def != null && belt[i].Def.Id == ItemCatalog.AxeId)
                    { axeSlot = i; break; }
                if (axeSlot >= 0) inventory.Model.SelectBelt(axeSlot);
            }
            yield return null;
            Debug.Log("[HitFeedbackLive] DEVIATION 2: granted the axe (pickedUp=" + gotAxe + " beltSlot=" +
                      axeSlot + " selectedWeapon=" + (attack.SelectedWeapon != null ? attack.SelectedWeapon.Id
                      : "<none>") + ") — a fresh launch starts with an EMPTY belt, which is why the first live " +
                      "run swallowed 24/24 clicks on the boar.");
            if (attack.SelectedWeapon == null)
            {
                Debug.Log("[HitFeedbackLive] GATE-FAIL: no weapon selected — the fight cannot be driven");
                Application.Quit(1);
                yield break;
            }

            // Pin the virtual clock: a framebuffer readback stalls a frame far longer than the 0.08 s flash, so
            // an unpinned run measures a curve that already finished inside one hitched frame (the ruler, not
            // the feature — the same lesson MeasureCreature records). Time.captureDeltaTime makes Time.time
            // advance a fixed 1/60 per frame, which is also the cadence real 60 fps play gives the AI.
            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 30; i++) yield return null;

            yield return StartCoroutine(LiveMeasure(cam, attack, boarAi.transform, dir, "live_boar"));
            bool boarOk = _liveCreaturePass;
            yield return StartCoroutine(LiveMeasure(cam, attack, snakeAi.transform, dir, "live_snake"));
            bool snakeOk = _liveCreaturePass;
            bool allPass = boarOk && snakeOk;

            Time.captureDeltaTime = 0f;
            Debug.Log("[HitFeedbackLive] GATE " + (allPass ? "PASS" : "FAIL") +
                      ": boar=" + boarOk + " snake=" + snakeOk + " -> " + dir);
            yield return new WaitForSeconds(0.3f);
            Application.Quit(allPass ? 0 : 1);
        }

        /// <summary>
        /// Fight ONE creature to death the way a player does — walk in, left-click, re-close the distance when
        /// it moves, keep going until it is dead — measuring, per landed hit, whether the flash produced a
        /// RENDERED luminance rise on the creature's own screen box; and after the kill, whether the death is
        /// something the eye can see.
        /// </summary>
        private IEnumerator LiveMeasure(Camera cam, MeleeAttack attack, Transform target, string dir,
                                        string prefix)
        {
            _liveCreaturePass = false;
            var fb = target.GetComponent<EnemyHitFeedback>();
            var hp = target.GetComponent<Health>();
            if (fb == null || hp == null)
            {
                Debug.Log("[HitFeedbackLive] " + prefix + " GATE-FAIL: no EnemyHitFeedback/Health on the target");
                yield break;
            }

            int landed = 0, flashedInPixels = 0, flashedInMaterial = 0, clicks = 0, swallowed = 0;
            float minLitFrac = float.MaxValue, maxLitFrac = 0f;
            int boxW = 0, boxH = 0, minFlashFrames = int.MaxValue;
            string firstMissDetail = "-";

            // STATED DEVIATION 3: with an AXE selected, ChopTree.WouldClaimClick() wins the Sponsor-ruled
            // VERB-WINS-OVER-WHIFF arbitration whenever a tree is inside chop range, and the melee swing never
            // fires. That arbitration is not this ticket's surface, so the gate steps around it rather than
            // "fixing" it here — but the ClickGateDiagnostic line is emitted first, every run, so a creature can
            // never go untested for a reason nobody wrote down.
            var diag = Object.FindAnyObjectByType<ClickGateDiagnostic>();
            if (diag != null) diag.ReportClick("live-preflight-" + prefix);
            foreach (var v in new Behaviour[] { attack.chopTree, attack.mineBoulder, attack.mineOre })
            {
                if (v == null || !v.enabled) continue;
                v.enabled = false;
                Debug.Log("[HitFeedbackLive] DEVIATION 3: disabled verb consumer " + v.GetType().Name +
                          " for the fight (verb arbitration is not this ticket's surface).");
            }

            // STATED DEVIATION 4: HP raised so the measurement gets N >= 8 landed hits. The shipped snake dies
            // in TWO axe hits (24 HP / 14 dmg — measured: `STRIKE #1 hp=10,0/24,0` then `STRIKE #2 hp=0,0/24,0`)
            // and a two-sample read cannot tell a per-hit defect from a coincidence — the project's own
            // sample-size discipline is N >= 8. This changes how MANY reads are taken, never what a read SAYS;
            // the kill still happens, from the same seam, at the end. It is ALSO the honest reason the Sponsor
            // sees so little of this feature: two flashes is the entire life of a snake.
            float liveBodyHeight = Mathf.Max(0.05f, fb.BodyBounds().size.y);
            float authoredMax = hp.Max;
            hp.max = Mathf.Max(authoredMax, 14f * 12f);
            hp.RestoreFull();
            Debug.Log("[HitFeedbackLive] DEVIATION 4: " + prefix + " HP max " + authoredMax.ToString("0") +
                      " -> " + hp.Max.ToString("0") + " for an N>=8 read (the authored value kills it in " +
                      Mathf.CeilToInt(authoredMax / 14f) + " axe hits).");

            while (!hp.IsDead && clicks < 24)
            {
                // Re-apply the raise EVERY iteration UNTIL the sample quota is met, then stop and hand the
                // creature back its authored HP so the KILL can actually land. Measured, not guessed, in both
                // directions: on the boar a one-shot raise evaporated (`STRIKE #1 hp=157,5/168,0` then
                // `STRIKE #2 hp=40,0/40,0` — the difficulty path re-applies the authored per-tier max and
                // Health.ApplyDifficulty re-clamps Current into it), and re-applying it unconditionally healed
                // the boar faster than the axe could take it down (`landed=23 … dead=False`).
                if (landed < 8) { if (hp.Max < 100f) { hp.max = 14f * 12f; hp.RestoreFull(); } }
                else if (hp.Max > authoredMax) hp.max = authoredMax;   // Current re-clamps on the next damage

                // Close the distance EVERY iteration: the creature is alive and moving, and a real player has to
                // chase it. Axe reach is 2.0 u, so stop at 1.5 — a swing at 3.0 (what the isolating gate walks
                // to) would resolve NO target through ResolveNearestTarget and whiff every time.
                yield return StartCoroutine(WalkTo(cam, target, 1.5f, 12f));
                YawAt(cam, target);
                // ⚠ CaptureScreenshotAsTexture must be called at END OF FRAME (Unity reads the back buffer);
                // calling it in the Update phase samples a stale/black surface and would make every rise read
                // as noise — a false FAIL, which is the better failure direction but still a broken ruler.
                yield return new WaitForEndOfFrame();

                // === THE CONTROL, taken immediately before every hit ===
                // Without it the pixel read is not evidence. The creature breathes, the camera drifts, and the
                // grass under a moving body changes plenty of pixels on its own; a "the box got brighter"
                // number with no noise floor beside it is exactly the kind of metric that is green on nonsense.
                // So: two consecutive UN-HIT frames through the identical path, and the hit's number is only
                // meaningful as a multiple of THIS.
                int cx0, cy0, cbw, cbh;
                float controlFrac = 0f;
                if (BodyBox(cam, fb, out cx0, out cy0, out cbw, out cbh))
                {
                    float[] c0 = GrabLuma(cx0, cy0, cbw, cbh);
                    yield return new WaitForEndOfFrame();
                    float[] c1 = GrabLuma(cx0, cy0, cbw, cbh);
                    float cmax;
                    DiffLuma(c0, c1, out controlFrac, out cmax);
                }

                int hitsBefore = fb.HitCount;
                int swingsBefore = attack.SwingsFired;
                // Freeze the measurement BOX on the pre-hit frame and reuse it for every subsequent sample, so
                // the per-pixel diff compares the same screen rectangle. Under the pinned 1/60 clock the body
                // moves a fraction of a pixel per frame, so the rect stays valid across the flash window.
                int x0, y0, bw, bh;
                bool boxed = BodyBox(cam, fb, out x0, out y0, out bw, out bh);
                boxW = bw; boxH = bh;
                float[] before = boxed ? GrabLuma(x0, y0, bw, bh) : null;
                // The pulse ledger BEFORE the swing — the only way to tell this hit's eye-time from the last
                // hit's. See the read below for the false-green this closes.
                int pulsesBefore = fb.FlashPulsesCompleted;

                attack.RequestAttackClick();          // the REAL path: Update → gate → verb arbitration →
                clicks++;                             // cooldown → ResolveNearestTarget → PerformAttack
                yield return null;                    // the click is consumed in the NEXT frame's Update…
                yield return new WaitForEndOfFrame();  // …and LateUpdate has written the flash by end of frame.

                float matAtImpact = fb.MaxMaterialFlash();
                bool hitLanded = fb.HitCount > hitsBefore;

                if (hitLanded)
                {
                    landed++;
                    if (matAtImpact > 0.0001f) flashedInMaterial++;

                    // === THE HONEST FLASH READ, AND WHY IT IS NOT A BOX MEAN ===
                    // The first cut of this gate averaged luminance over the body's screen box and measured a
                    // 0.005 rise on the snake — but a snake is a THIN chain lying on grass, so its own pixels
                    // are a few percent of its bounding box and the mean is DILUTED by the grass around it. A
                    // mean cannot distinguish "the flash rendered on a small body" from "the flash rendered on
                    // nothing", which is the exact question the Sponsor's "snake does not flash" asks. So:
                    // count the pixels that actually BRIGHTENED, and for HOW MANY FRAMES they stayed bright.
                    // litFrac = fraction of the box whose luminance rose by >= 0.05 (a change an eye can see on
                    // a mid-tone body); litFrames = how many consecutive frames from contact clear that bar,
                    // which is the "how much eye-time did the player get" figure a peak amplitude cannot give.
                    float litFrac = 0f, peakRise = 0f;
                    int litFrames = 0;
                    for (int f = 0; f < 10; f++)
                    {
                        if (before == null) break;
                        float[] now = GrabLuma(x0, y0, bw, bh);
                        float frac, mx;
                        DiffLuma(before, now, out frac, out mx);
                        if (f == 0) { litFrac = frac; peakRise = mx; }
                        if (frac >= LitFracFloor) litFrames++;
                        else if (f > 0) break;   // the flash has come back down — stop sampling
                        if (f == 0 && landed <= 3) ShotTo(Path.Combine(dir, prefix + "_hit" + landed + ".png"));
                        yield return new WaitForEndOfFrame();
                    }
                    if (litFrac < minLitFrac) minLitFrac = litFrac;
                    if (litFrac > maxLitFrac) maxLitFrac = litFrac;
                    // === WAIT FOR *THIS* PULSE, AND SCORE A PULSE THAT NEVER FINISHED AS ZERO ===
                    // LastFlashFrames is published only when a pulse completes, so it must never be read
                    // unconditionally. Two separate ways that bit:
                    //  (a) reading it straight after a fixed-length sampling loop returned the PREVIOUS hit's
                    //      number, so hit#1 reported 0 while hits 2+ reported the one before them;
                    //  (b) FAR worse, waiting on `FlashActive` cannot terminate when the pulse is STUCK — which
                    //      is exactly what a fatal hit did while the corpse pass returned above the flash block.
                    //      FlashActive stayed true forever, this loop burned its 40 frames, and the gate then
                    //      scored the KILLING BLOW on the previous hit's 11 frames. The gate was green on a hit
                    //      that rendered no flash at all: the Sponsor's original report, still live, still
                    //      invisible to the instrument built to catch it.
                    // So: wait on the monotonic PULSE LEDGER, and if this hit's pulse never completes, the
                    // eye-time for it is 0 — a FAILURE, never a stale pass. The 90-frame bound is ~1.5 s of
                    // virtual time, generously over the 0.18 s window, so a timeout means genuinely stuck.
                    for (int g = 0; g < 90 && fb.FlashPulsesCompleted == pulsesBefore; g++) yield return null;
                    bool pulseCompleted = fb.FlashPulsesCompleted > pulsesBefore;
                    // EYE-TIME, read off the driver's OWN frame counter rather than off the framebuffer. The
                    // pixel numbers above cannot separate the flash from the dust puff and the flinch — all
                    // three fire on the same impulse and all three change pixels inside the same box — so they
                    // are reported as diagnosis and NOT asserted on. This counter can be attributed: it counts
                    // the frames the driver actually rendered a lit body for, which is the quantity the soak
                    // proved was the defect and which the amplitude-only isolating gate could not see.
                    int flashFrames = pulseCompleted ? fb.LastFlashFrames : 0;
                    if (flashFrames < minFlashFrames) minFlashFrames = flashFrames;

                    bool visible = flashFrames >= FlashFramesFloor;
                    if (visible) flashedInPixels++;
                    else if (firstMissDetail == "-")
                        firstMissDetail = "hit#" + landed + " flashFrames=" + flashFrames +
                                          " pulseCompleted=" + pulseCompleted +
                                          " fatal=" + hp.IsDead +
                                          " litFrac=" + litFrac.ToString("0.0000") +
                                          " mat=" + matAtImpact.ToString("0.000");
                    Debug.Log("[HitFeedbackLive] " + prefix + " hit#" + landed + " hp=" +
                              hp.Current.ToString("0.0") + "/" + hp.Max.ToString("0.0") +
                              (hp.IsDead ? " FATAL" : "") +
                              " flashFrames=" + flashFrames + " (floor=" + FlashFramesFloor + ")" +
                              " pulseCompleted=" + pulseCompleted +
                              " box=" + boxW + "x" + boxH + "px litFrac=" + litFrac.ToString("0.0000") +
                              " controlFrac=" + controlFrac.ToString("0.0000") +
                              " peakPxRise=" + peakRise.ToString("0.000") +
                              " litFramesRaw=" + litFrames + " matFlash=" + matAtImpact.ToString("0.000") +
                              " visible=" + visible);
                }
                else if (attack.SwingsFired == swingsBefore)
                {
                    swallowed++;   // the click never became a swing (a verb claimed it / cooldown / no weapon)
                    if (swallowed == 1 && diag != null) diag.ReportClick("live-swallowed-" + prefix);
                }
                yield return new WaitForSeconds(0.5f);
            }

            // ---------------- THE DEATH: is it something the eye can SEE? ----------------
            // FRAME THE CORPSE FIRST. The first cut of this gate shot the death frames wherever the camera
            // happened to be pointing after the last swing, and produced three PNGs per creature in which the
            // body was not visible AT ALL — indistinguishable, to a human opening the artifact, from the very
            // "it just disappeared" defect being measured. A frame nobody can read is not evidence (the same
            // rule the isolating gate's WalkTo-before-the-death-shot was written for). The numeric asserts did
            // not need it; the human check does, and the human check is the one that caught this ticket.
            yield return StartCoroutine(WalkTo(cam, target, 2.5f, 5f));
            YawAt(cam, target);
            for (int i = 0; i < 3; i++) yield return null;
            int dbx, dby, dbw, dbh;
            bool corpseFramed = BodyBox(cam, fb, out dbx, out dby, out dbw, out dbh);
            Debug.Log("[HitFeedbackLive] " + prefix + " corpse framing: onScreen=" + corpseFramed +
                      " box=" + dbw + "x" + dbh + "px at (" + dbx + "," + dby + ") screen=" +
                      Screen.width + "x" + Screen.height +
                      " — the death PNGs are only human-readable evidence when this is true");

            float deathY = fb.BodyMeanY(), deathUp = fb.BodyUprightness();
            Bounds db = fb.BodyBounds();
            float bodyH = Mathf.Max(0.05f, db.size.y);
            int deathPuffs = fb.DeathPuffCount;
            ShotTo(Path.Combine(dir, prefix + "_death0.png"));
            yield return new WaitForSeconds(1.0f);
            YawAt(cam, target);
            yield return null;
            float settleY = fb.BodyMeanY(), settleUp = fb.BodyUprightness();
            ShotTo(Path.Combine(dir, prefix + "_death1.png"));

            // Ride to one frame before the despawn. The AI owns that clock; poll activeSelf rather than
            // duplicating its constant here (a duplicated timer is the dead-knob class).
            float lastY = settleY, lastUp = settleUp;
            float deadline = Time.time + 8f;
            while (target.gameObject.activeSelf && Time.time < deadline)
            {
                lastY = fb.BodyMeanY(); lastUp = fb.BodyUprightness();
                yield return null;
            }
            bool despawned = !target.gameObject.activeSelf;
            ShotTo(Path.Combine(dir, prefix + "_death_prevanish.png"));

            // A death the player can see must change the BODY, not only a counter. Either channel counts: a
            // topple (uprightness falls) or a sink (the body goes down by a real fraction of its own height).
            float upDrop = deathUp - lastUp;
            float yDrop = deathY - lastY;
            bool settleVisible = upDrop >= 0.20f || yDrop >= 0.45f * bodyH;

            bool everyHitFlashed = landed > 0 && flashedInPixels == landed;
            bool enoughHits = landed >= 8;   // sample-size discipline: a 2-hit read cannot claim "every hit"
            bool vanishCovered = fb.VanishPuffCount > 0;
            // THE CORPSE MUST NOT GET TALLER, and this assert exists because the first death-settle written for
            // this ticket rotated the body about the wrong axis: 80 degrees of PITCH stood both creatures on
            // their noses and drove the front half through the terrain, so the corpse was invisible from the
            // instant it died — the very defect the settle was added to fix. Every other assertion here was
            // GREEN on it (the rotation happened, the body went down, the puffs fired). The one figure that
            // named it was the body's own height GROWING, 1.02u -> 1.76u, because a creature longer than it is
            // tall becomes taller when you stand it on end. A body that goes OVER gets shorter, never taller.
            bool corpseStaysLow = bodyH <= liveBodyHeight * 1.25f;
            bool pass = enoughHits && everyHitFlashed && hp.IsDead && deathPuffs > 0 && settleVisible &&
                        vanishCovered && corpseStaysLow;

            Debug.Log("[HitFeedbackLive] " + prefix + ": clicks=" + clicks + " landed=" + landed +
                      " swallowedClicks=" + swallowed + " flashedLongEnough=" + flashedInPixels +
                      " flashedInMaterial=" + flashedInMaterial +
                      " litFrac=[" + (landed > 0 ? minLitFrac.ToString("0.0000") : "-") + ".." +
                      maxLitFrac.ToString("0.0000") + "] minFlashFrames=" +
                      (minFlashFrames == int.MaxValue ? "-" : minFlashFrames.ToString()) +
                      " (floor=" + FlashFramesFloor + ") firstMiss=" + firstMissDetail +
                      " box=" + boxW + "x" + boxH + "px" +
                      " | death: dead=" + hp.IsDead + " puffs=" + deathPuffs +
                      " bodyH " + liveBodyHeight.ToString("0.00") + "->" + bodyH.ToString("0.00") +
                      " staysLow=" + corpseStaysLow +
                      " uprightness " + deathUp.ToString("0.000") + "->" + lastUp.ToString("0.000") +
                      " (drop=" + upDrop.ToString("0.000") + ")" +
                      " meanY " + deathY.ToString("0.000") + "->" + lastY.ToString("0.000") +
                      " (drop=" + yDrop.ToString("0.000") + ")" +
                      " despawned=" + despawned + " settleVisible=" + settleVisible +
                      " vanishPuffs=" + fb.VanishPuffCount +
                      " || enoughHits=" + enoughHits + " everyHitFlashed=" + everyHitFlashed +
                      " -> " + (pass ? "PASS" : "FAIL"));
            _liveCreaturePass = pass;
        }

        /// <summary>Fraction of the body box that must BRIGHTEN by >= <see cref="PxRiseFloor"/> for the flash to
        /// count as rendered. 1.5 % of the box: measured for calibration on the unfixed tree, the snake's own
        /// pixels are a small share of its bounding box, so this is deliberately a low bar — it separates "the
        /// flash reached the screen" from "the flash reached nothing", and it is NOT a tone judgement.</summary>
        private const float LitFracFloor = 0.015f;
        /// <summary>Per-pixel luminance rise that counts as a visible change on a mid-tone body.</summary>
        private const float PxRiseFloor = 0.05f;
        /// <summary>
        /// MINIMUM FRAMES a landed hit must render a lit body for — the assertion the 2026-08-14 soak bought.
        /// The isolating gate asserted the flash's peak AMPLITUDE (0.6200 on 13/13 snake materials) and was
        /// green while the Sponsor, on the same exe, reported the snake never flashing at all. Amplitude cannot
        /// see this: at the shipped `flashSeconds = 0.08` the pulse rendered for FIVE frames
        /// (`[HitFeedback] Snake FLASH done peak=0,620 frames=5 over 0,080s`, measured), of which only the first
        /// two carry more than half amplitude on the quadratic fade — on a creature that dies in TWO hits, so a
        /// snake's ENTIRE life contains ~10 frames of flash.
        ///
        /// EIGHT is deliberately a floor and not a target: it is ~133 ms at 60 fps, which is above the ~100 ms
        /// at which a brief luminance change stops being reported as a blink-and-miss event, and it still reads
        /// as a snap-and-fade rather than a glow. The Sponsor's dial (`hit_flash_duration`) tunes the shipped
        /// value; this only forbids going back under the bar the soak already failed at.
        /// </summary>
        private const int FlashFramesFloor = 8;

        /// <summary>The creature's screen-space bounding box (bottom-left origin, matching both
        /// WorldToScreenPoint and CaptureScreenshotAsTexture). False when the body projects to nothing readable
        /// — off-screen / behind the camera / degenerate — which the caller must treat as a NON-observation, not
        /// as a pass.</summary>
        private static bool BodyBox(Camera cam, EnemyHitFeedback fb, out int x0, out int y0, out int w, out int h)
        {
            x0 = y0 = w = h = 0;
            if (cam == null || fb == null) return false;
            Bounds b = fb.BodyBounds();
            Vector3 c = b.center, e = b.extents;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            bool anyInFront = false;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3(((i & 1) == 0 ? -e.x : e.x),
                                             ((i & 2) == 0 ? -e.y : e.y),
                                             ((i & 4) == 0 ? -e.z : e.z));
                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) continue;
                anyInFront = true;
                if (sp.x < minX) minX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y > maxY) maxY = sp.y;
            }
            if (!anyInFront) return false;
            // Pad by 2 px so a body that shifts sub-pixel between the two samples stays inside the rect.
            x0 = Mathf.Clamp(Mathf.FloorToInt(minX) - 2, 0, Screen.width - 1);
            y0 = Mathf.Clamp(Mathf.FloorToInt(minY) - 2, 0, Screen.height - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX) + 2, 0, Screen.width - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY) + 2, 0, Screen.height - 1);
            w = x1 - x0 + 1; h = y1 - y0 + 1;
            return w >= 4 && h >= 4;
        }

        /// <summary>Read the RENDERED luminance of a screen rect out of the actual framebuffer. This is the whole
        /// point of the live mode: the isolating gate asks the MATERIAL for the float it just wrote, which is
        /// green on a flash that changes no pixel at all. Must be called at end-of-frame. Null on failure.</summary>
        private static float[] GrabLuma(int x0, int y0, int w, int h)
        {
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) return null;
            try
            {
                int tw = tex.width, th = tex.height;
                // The framebuffer may not be the logical Screen size (DPI scaling); map proportionally.
                float sx = (float)tw / Mathf.Max(1, Screen.width);
                float sy = (float)th / Mathf.Max(1, Screen.height);
                var outPx = new float[w * h];
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int tx = Mathf.Clamp(Mathf.FloorToInt((x0 + i) * sx), 0, tw - 1);
                        int ty = Mathf.Clamp(Mathf.FloorToInt((y0 + j) * sy), 0, th - 1);
                        Color p = tex.GetPixel(tx, ty);
                        outPx[j * w + i] = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
                    }
                return outPx;
            }
            finally { Object.Destroy(tex); }
        }

        /// <summary>Fraction of pixels that BRIGHTENED by at least <see cref="PxRiseFloor"/>, plus the largest
        /// single-pixel rise. Sign matters: a darkening (a shadow moving, the flinch shifting the body) must not
        /// count as a flash.</summary>
        private static void DiffLuma(float[] before, float[] now, out float litFraction, out float maxRise)
        {
            litFraction = 0f; maxRise = 0f;
            if (before == null || now == null || before.Length == 0 || before.Length != now.Length) return;
            int lit = 0;
            for (int i = 0; i < before.Length; i++)
            {
                float d = now[i] - before[i];
                if (d > maxRise) maxRise = d;
                if (d >= PxRiseFloor) lit++;
            }
            litFraction = (float)lit / before.Length;
        }

        /// <summary>Walk the REAL player toward <paramref name="target"/> through the production
        /// <see cref="WasdMovement"/> override seam — the actual gameplay Update path, nothing teleports —
        /// stopping inside <paramref name="stopDist"/> or when the deadline expires. Degrades gracefully: the
        /// measurements are position-independent, so a missed deadline costs framing quality, never a verdict.</summary>
        private IEnumerator WalkTo(Camera cam, Transform target, float stopDist, float seconds)
        {
            if (target == null || player == null) yield break;
            float deadline = Time.time + seconds;
            while (Time.time < deadline)
            {
                Vector3 to = target.position - player.transform.position;
                to.y = 0f;
                if (to.magnitude <= stopDist) break;
                player.SetInputOverride(WorldDirToInput(cam, to.normalized));
                yield return null;
            }
            player.SetInputOverride(Vector2.zero);
            yield return null;
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
