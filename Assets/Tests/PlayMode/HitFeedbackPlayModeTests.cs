using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon.Combat;
using FarHorizon.Juice;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PLAYMODE guards for enemy hit feedback (ticket 86caxjwb3 AC7). These carry the two assertions EditMode
    /// structurally cannot:
    ///
    ///  1. <b>THE FLASH COMES BACK DOWN.</b> A "flash fired" assertion is NOT sufficient — it passes on the
    ///     latched bug. If the decay were ever driven from a C#-written <c>Time.time</c> stamp differenced
    ///     against the shader's <c>_Time.y</c> (which is <c>Time.timeSinceLevelLoad</c>), the term would
    ///     saturate to full intensity FOREVER, and the impact frame would still look correct. So this fixture
    ///     reads the value BACK OFF THE MATERIAL over a sampled window and requires (a) a strictly falling
    ///     sequence — which a constant/latched implementation cannot produce — and (b) exactly 0 at rest.
    ///  2. <b>THE POOL ACTUALLY RECYCLES.</b> Without <c>main.stopAction = Callback</c>
    ///     <c>OnParticleSystemStopped</c> is never delivered, the pool never gets its instances back, and every
    ///     other assertion still passes while it silently allocates one system per burst.
    ///
    /// HEADLESS DISCIPLINE (unity-conventions.md §Headless): <c>Time.deltaTime ≈ 0</c> in a headless PlayMode
    /// run, which would freeze the particle simulation and make the pool test hang rather than fail. SetUp pins
    /// <c>Time.captureDeltaTime</c> (the documented remedy) and TearDown restores it to 0 so the pin cannot leak
    /// into another fixture. Every wait is frame- or Time.time-based; no <c>WaitForEndOfFrame</c> (never evoked
    /// in -batchmode). Rigs are built INACTIVE and activated only once fully wired — Unity fires Awake/OnEnable
    /// SYNCHRONOUSLY inside AddComponent on an already-active GameObject, which is how a driver ends up caching
    /// zero renderers (the InventoryUI one-shot-latch class).
    /// </summary>
    public class HitFeedbackPlayModeTests
    {
        private const float StepDt = 0.02f;
        private GameObject _creature;
        private GameObject _poolHost;
        private float _prevCaptureDt;

        [SetUp]
        public void PinTheClock()
        {
            _prevCaptureDt = Time.captureDeltaTime;
            Time.captureDeltaTime = StepDt; // a deterministic virtual step so particles + phases actually advance
        }

        [TearDown]
        public void Cleanup()
        {
            Time.captureDeltaTime = _prevCaptureDt; // SetUp/TearDown-scoped so it cannot leak across fixtures
            if (_creature != null) Object.Destroy(_creature);
            if (_poolHost != null) Object.Destroy(_poolHost);
        }

        // A synthetic creature mirroring the shipped topology: a bare root (NO renderer) + N renderer children
        // on the real world shader. Built INACTIVE, wired, THEN activated.
        private EnemyHitFeedback BuildCreature(int parts, out Health hp)
        {
            var shader = Shader.Find("FarHorizon/LowPolyVertexColor");
            Assert.IsNotNull(shader, "the world shader must resolve (test precondition)");

            _creature = new GameObject("hitfeedback-test-creature");
            _creature.SetActive(false);
            for (int i = 0; i < parts; i++)
            {
                var child = new GameObject("part" + i);
                child.transform.SetParent(_creature.transform, false);
                child.transform.localPosition = new Vector3(0f, 0f, i * 0.15f);
                child.AddComponent<MeshFilter>().sharedMesh = new Mesh();
                child.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader) { name = "partMat" + i };
            }
            hp = _creature.AddComponent<Health>();
            hp.max = 100f;
            hp.startFull = true;
            var fb = _creature.AddComponent<EnemyHitFeedback>();
            fb.flashSeconds = 0.20f;   // a little longer than the shipped 0.08 so the sampled window has frames
            fb.flinchSeconds = 0.30f;
            fb.flashIntensity = 0.8f;
            _creature.SetActive(true); // Awake/OnEnable fire HERE, with every child already present
            return fb;
        }

        [UnityTest]
        public IEnumerator Flash_RisesThenFALLS_AndSettlesAtExactlyZero_TheLatchDiscriminator()
        {
            var fb = BuildCreature(BoarBodyRig.PartCount, out var hp);
            yield return null;

            Assert.AreEqual(BoarBodyRig.PartCount, fb.MaterialCount,
                "the driver must reach every part-material (GetComponentsInChildren, not the singular)");
            Assert.AreEqual(0f, fb.MaxMaterialFlash(), 1e-5f, "at rest, before any hit, the body is untinted");

            hp.ApplyDamage(10f, DamageType.Slash);
            yield return null;   // one LateUpdate writes the first amplitude

            float peak = fb.MaxMaterialFlash();
            Assert.Greater(peak, 0f, "the hit must light the body up");
            Assert.Greater(fb.MinMaterialFlash(), 0f,
                "ALL parts must light TOGETHER — a flash on the body but not the head reads as a bug, not juice");

            // Sample the decay. A LATCHED implementation produces a CONSTANT here; only a real decay falls.
            float best = peak;
            int fallingSamples = 0;
            float deadline = Time.time + fb.flashSeconds + 0.5f;
            while (Time.time < deadline && fb.FlashActive)
            {
                yield return null;
                float now = fb.MaxMaterialFlash();
                if (now < best - 1e-5f) fallingSamples++;
                best = Mathf.Min(best, now);
            }
            Assert.Greater(fallingSamples, 1,
                "the flash must visibly DECAY across the window — a constant value is the LATCHED-flash " +
                "signature ([DFC-1]) and looks identical on the impact frame");

            // …and land at EXACTLY zero. This is the assertion the shipped -verifyHitFeedback after-0.5s frame
            // mirrors in the built exe.
            yield return null;
            Assert.AreEqual(0f, fb.MaxMaterialFlash(), 1e-5f,
                "after the flash window the creature is back at BASE COLOUR — no residual tint on ANY part");
            Assert.AreEqual(0f, fb.FlashAmount, 1e-5f, "and the driver's own resting amplitude is 0");
            Assert.IsFalse(fb.FlashActive, "the flash is no longer running");
        }

        [UnityTest]
        public IEnumerator Flinch_RecoilsTheParts_ThenRESOLVES()
        {
            var fb = BuildCreature(BoarBodyRig.PartCount, out var hp);
            fb.recoilBack = 0.2f;
            yield return null;

            Assert.AreEqual(Vector3.zero, fb.FlinchOffset, "at rest there is no recoil offset");
            hp.ApplyDamage(10f, DamageType.Slash);

            float peak = 0f;
            float deadline = Time.time + fb.flinchSeconds + 0.5f;
            while (Time.time < deadline && fb.FlinchActive)
            {
                yield return null;
                peak = Mathf.Max(peak, fb.FlinchOffset.magnitude);
            }
            Assert.Greater(peak, 0.01f, "the body must visibly recoil (AC3 — 'that connected')");
            yield return null;
            Assert.AreEqual(0f, fb.FlinchOffset.magnitude, 1e-5f,
                "the recoil RESOLVES — no sustained wobble, no permanent displacement (game-juice.md §2)");
        }

        [UnityTest]
        public IEnumerator Pool_RecyclesEveryBurst_AndAllocationIsBounded()
        {
            _poolHost = new GameObject("hitfeedback-test-pool");
            _poolHost.SetActive(false);

            var tmplGo = new GameObject("test-puff-template");
            tmplGo.transform.SetParent(_poolHost.transform, false);
            var ps = tmplGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.05f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.08f;
            main.startSpeed = 0.4f;
            main.maxParticles = 20;
            main.stopAction = ParticleSystemStopAction.Callback; // [DFC-4c] the load-bearing line
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.burstCount = 1;
            em.SetBurst(0, new ParticleSystem.Burst(0f, (short)4));
            tmplGo.SetActive(false);

            var emitter = _poolHost.AddComponent<PooledBurstEmitter>();
            emitter.template = ps;
            emitter.maxPoolSize = 8;
            _poolHost.SetActive(true);
            yield return null;

            Assert.IsTrue(emitter.TemplateStopActionIsCallback,
                "[DFC-4c] without stopAction = Callback the release callback NEVER fires and the pool leaks " +
                "silently while every other assertion still passes");

            const int bursts = 5;
            for (int i = 0; i < bursts; i++)
            {
                Assert.IsTrue(emitter.Emit(Vector3.zero, 4, Color.white, 1f), "burst " + i + " must emit");
                // Wait for THIS burst to finish and come home. Bounded so a broken stop-action FAILS rather
                // than hangs — a hang reads as an environment problem and gets quarantined instead of fixed.
                int frames = 0;
                while (emitter.LiveCount > 0 && frames < 400) { yield return null; frames++; }
                Assert.AreEqual(0, emitter.LiveCount,
                    "burst " + i + " never came back — OnParticleSystemStopped did not fire (stopAction?)");
            }

            Assert.AreEqual(bursts, emitter.EmitCount, "every burst was emitted");
            Assert.AreEqual(bursts, emitter.ReleaseCount, "every burst was RELEASED back to the pool");
            Assert.AreEqual(1, emitter.CreatedCount,
                "5 sequential bursts must REUSE one instance — the whole point of the pool. A CreatedCount of " +
                "5 here is per-event Instantiate wearing a pool's name (game-juice.md §1.4).");
            Assert.LessOrEqual(emitter.CreatedCount, emitter.maxPoolSize,
                "allocation stays inside the pool's retained ceiling");
        }

        [UnityTest]
        public IEnumerator EmitSizeScale_ScalesBOTHEndsOfTheAuthoredBand_NotJustTheMax()
        {
            // THE DEFECT THIS PINS, measured in the shipped exe: `main.startSizeMultiplier` on a TWO-CONSTANTS
            // start-size curve does not scale the band — it OVERWRITES constantMax and leaves constantMin. The
            // authored 0.80..1.80 came back from the live pooled system as 0.80..1.00 after a multiplier of 1.
            // So the emitter's `sizeScale` parameter silently meant "set the max", and the death puff's 1.35
            // would have rendered NARROWER than the hit puff's 1.0 — the opposite of "a touch broader".
            // Every future juice burst copies this emitter, so a lying size parameter would propagate with it.
            _poolHost = new GameObject("hitfeedback-test-pool");
            _poolHost.SetActive(false);
            var tmplGo = new GameObject("test-puff-template");
            tmplGo.transform.SetParent(_poolHost.transform, false);
            var ps = tmplGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.05f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.08f; main.stopAction = ParticleSystemStopAction.Callback;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f); // the AUTHORED band
            var em = ps.emission; em.rateOverTime = 0f; em.burstCount = 1;
            em.SetBurst(0, new ParticleSystem.Burst(0f, (short)3));
            tmplGo.SetActive(false);
            var emitter = _poolHost.AddComponent<PooledBurstEmitter>();
            emitter.template = ps;
            _poolHost.SetActive(true);
            yield return null;

            Assert.IsTrue(emitter.Emit(Vector3.zero, 3, Color.white, 2f), "a 2x burst must emit");
            yield return null;
            var live = emitter.LastEmitted.main.startSize;
            Assert.AreEqual(1.6f, live.constantMin, 1e-3f, "the band's LOW end scales (0.8 x 2)");
            Assert.AreEqual(3.6f, live.constantMax, 1e-3f, "…and so does the HIGH end (1.8 x 2) — not clobbered");

            // …and the AUTHORED template band survives, so a later differently-scaled burst is not compounding
            // on top of the previous one's rewrite.
            Assert.AreEqual(0.8f, emitter.template.main.startSize.constantMin, 1e-3f, "template min untouched");
            Assert.AreEqual(1.8f, emitter.template.main.startSize.constantMax, 1e-3f, "template max untouched");
        }

        [UnityTest]
        public IEnumerator DeathPuff_FiresExactlyOnce_OnTheKillingBlow()
        {
            _poolHost = new GameObject("hitfeedback-test-pool");
            _poolHost.SetActive(false);
            var tmplGo = new GameObject("test-puff-template");
            tmplGo.transform.SetParent(_poolHost.transform, false);
            var ps = tmplGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.05f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.08f; main.stopAction = ParticleSystemStopAction.Callback;
            var em = ps.emission; em.rateOverTime = 0f; em.burstCount = 1;
            em.SetBurst(0, new ParticleSystem.Burst(0f, (short)4));
            tmplGo.SetActive(false);
            var emitter = _poolHost.AddComponent<PooledBurstEmitter>();
            emitter.template = ps;
            _poolHost.SetActive(true);

            var fb = BuildCreature(4, out var hp);
            fb.puff = emitter;
            yield return null;

            hp.ApplyDamage(20f, DamageType.Slash);
            yield return null;
            Assert.AreEqual(0, fb.DeathPuffCount, "a non-fatal hit fires the HIT puff, not the death beat");
            int hitPuffs = emitter.EmitCount;
            Assert.Greater(hitPuffs, 0, "the hit puff fired");

            hp.ApplyDamage(500f, DamageType.Slash); // the killing blow
            yield return null;
            Assert.IsTrue(hp.IsDead, "precondition: the creature died");
            Assert.AreEqual(1, fb.DeathPuffCount,
                "AC4 🔒: the puff ALSO fires on death — without it the soak's death moment has no feedback at all");
            Assert.Greater(emitter.EmitCount, hitPuffs, "…and it reached the pool as a real burst");

            hp.ApplyDamage(50f, DamageType.Slash); // a hit on an already-dead target
            yield return null;
            Assert.AreEqual(1, fb.DeathPuffCount, "Health.Died is one-shot — the death beat never repeats");
        }

        // ============================ THE TWO 2026-08-14 SOAK GUARDS ============================
        // Both are RED on the tree the Sponsor soaked, and each closes a defect that every existing assertion in
        // this file AND in the shipped -verifyHitFeedback capture gate was green on.

        /// <summary>
        /// SOAK GUARD 1 — EYE-TIME, the figure amplitude cannot carry. The Sponsor reported "snake does not
        /// flash" on a build whose capture gate had just measured 13/13 snake part-materials lit at 0.6200. Both
        /// were true: the pulse fired at full amplitude and reached real pixels, but it lasted FIVE FRAMES
        /// (`[HitFeedback] Snake FLASH done peak=0,620 frames=5 over 0,080s` — shipped exe, every hit, N=13), of
        /// which only the first two carry more than half amplitude on the quadratic fade. The snake dies in two
        /// axe hits, so ~4 bright frames is a whole creature's worth of feedback.
        ///
        /// This asserts the SHIPPED DEFAULT, and it is the one place in the fixture that must NOT retune
        /// <c>flashSeconds</c>: the defect was entirely IN the default, and a fixture that dials the effect up
        /// before measuring is how it stayed invisible — see <c>BuildCreature</c>'s 0.20 override and the comment
        /// that came with it ("a little longer than the shipped 0.08 so the sampled window has frames"). A test
        /// that has to lengthen an effect to have something to sample is telling you the shipped value has
        /// nothing to sample. A FLOOR, not a target: `hit_flash_duration` tunes upward freely.
        /// </summary>
        [UnityTest]
        public IEnumerator FlashDefault_LastsAtLeastEightFramesAtSixtyFps_TheEyeTimeFloor()
        {
            var shader = Shader.Find("FarHorizon/LowPolyVertexColor");
            Assert.IsNotNull(shader, "the world shader must resolve (test precondition)");
            _creature = new GameObject("flash-default-creature");
            _creature.SetActive(false);
            var child = new GameObject("part0");
            child.transform.SetParent(_creature.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            child.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader);
            var hp = _creature.AddComponent<Health>();
            hp.max = 100f; hp.startFull = true;
            var fb = _creature.AddComponent<EnemyHitFeedback>();   // NO dial overrides — the shipped defaults
            _creature.SetActive(true);
            yield return null;

            const int FloorFrames = 8;
            Assert.GreaterOrEqual(fb.flashSeconds * 60f, FloorFrames,
                "the SHIPPED flashSeconds default must give the player at least " + FloorFrames +
                " frames at 60 fps. The 2026-08-14 soak failed at 0.08s = 5 frames, with the Sponsor reporting " +
                "the flash as absent on both creatures; a peak-amplitude assertion cannot see this.");

            // …and prove the runtime RENDERS that eye-time rather than trusting the arithmetic: count the frames
            // the driver actually wrote a non-zero amplitude for, under the fixture's pinned virtual step.
            int expectedFrames = Mathf.FloorToInt(fb.flashSeconds / StepDt);
            hp.ApplyDamage(10f, DamageType.Slash);
            int litFrames = 0;
            for (int i = 0; i < expectedFrames * 3 + 4; i++)
            {
                yield return null;
                if (fb.MaxMaterialFlash() > 1e-5f) litFrames++;
                else if (litFrames > 0) break;
            }
            Assert.GreaterOrEqual(litFrames, expectedFrames - 1,
                "the driver must actually render the eye-time its duration promises (measured " + litFrames +
                " lit frames, expected ~" + expectedFrames + " at a " + StepDt + "s virtual step)");
        }

        /// <summary>
        /// SOAK GUARD 2 — THE DEATH MUST MOVE THE BODY. Sponsor: "when both snake and boar dies they just
        /// disappear." He was describing the code exactly: the AIs set Dead, started a despawn timer, and left
        /// the body untouched until <c>SetActive(false)</c> removed it in one frame. Measured across the entire
        /// four-second window in the shipped exe — boar `uprightness 0,991 -> 0,991, meanY 0,944 -> 0,944`;
        /// snake `1,000 -> 1,000, 0,296 -> 0,296`. The isolating capture gate was GREEN on it, because its death
        /// assertion was <c>DeathPuffCount &gt; 0</c>: a counter cannot see a body that never moves.
        ///
        /// So this asserts the two channels a person actually reads — the body goes OVER and it goes DOWN — plus
        /// that the vanish is COVERED by dust rather than being a silent removal. It drives the real
        /// <c>BeginDeathSettle</c> → <c>Health.Died</c> → LateUpdate path; nothing here pokes the settle directly.
        /// </summary>
        [UnityTest]
        public IEnumerator Death_TopplesAndSinksTheBody_SoTheDespawnIsNotAPop()
        {
            _poolHost = new GameObject("death-settle-pool-host");
            _poolHost.SetActive(false);
            var tmplGo = new GameObject("tmpl");
            tmplGo.transform.SetParent(_poolHost.transform, false);
            var ps = tmplGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 0.12f;
            main.stopAction = ParticleSystemStopAction.Callback;
            var em = ps.emission; em.rateOverTime = 0f; em.burstCount = 1;
            em.SetBurst(0, new ParticleSystem.Burst(0f, (short)4));
            tmplGo.SetActive(false);
            var emitter = _poolHost.AddComponent<PooledBurstEmitter>();
            emitter.template = ps;
            _poolHost.SetActive(true);

            var fb = BuildCreature(BoarBodyRig.PartCount, out var hp);
            fb.puff = emitter;
            yield return null;

            float liveUpright = fb.BodyUprightness();
            float liveY = fb.BodyMeanY();
            float bodyH = Mathf.Max(0.05f, fb.BodyBounds().size.y);
            Assert.IsFalse(fb.IsDeathSettling, "precondition: a live creature is not settling");

            // The AI owns the despawn clock and hands it over (BoarAI/SnakeAI.OnDied) — mirror that seam here
            // rather than reaching past it with a second copy of the number.
            const float Window = 4f;
            hp.ApplyDamage(500f, DamageType.Slash);
            fb.BeginDeathSettle(Window);
            Assert.IsTrue(hp.IsDead, "precondition: the creature died");
            Assert.IsTrue(fb.IsDeathSettling, "the settle must run for the whole despawn window");

            int steps = Mathf.CeilToInt(Window / StepDt) + 4;
            for (int i = 0; i < steps; i++) yield return null;

            float deadUpright = fb.BodyUprightness();
            float deadY = fb.BodyMeanY();
            Assert.Less(deadUpright, liveUpright - 0.20f,
                "the body must visibly go OVER during the settle (uprightness " + liveUpright.ToString("0.000") +
                " -> " + deadUpright.ToString("0.000") + "); a frozen upright corpse followed by a one-frame pop " +
                "is exactly what the Sponsor read as 'they just disappear'");
            Assert.Less(deadY, liveY - 0.45f * bodyH,
                "…and it must SINK below the surface before the despawn, so nothing visible vanishes (meanY " +
                liveY.ToString("0.000") + " -> " + deadY.ToString("0.000") + ", bodyH " +
                bodyH.ToString("0.00") + ")");
            Assert.GreaterOrEqual(fb.VanishPuffCount, 1,
                "the moment the body goes under must be COVERED by dust — an uncovered removal is still a pop");
        }
    }
}
