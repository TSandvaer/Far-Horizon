using UnityEngine;
using FarHorizon.Juice;

namespace FarHorizon.Combat
{
    /// <summary>
    /// ENEMY BODY-LEVEL HIT FEEDBACK (ticket 86caxjwb3) — the ONE shared driver that makes a landed strike READ
    /// on the creature's own body. Three channels fire off a single impulse: a warm-white `_HitFlash` pulse
    /// across every part-material, a procedural FLINCH recoil on the already-posed body parts, and a pooled
    /// dust PUFF at the contact point. Before this, a landed hit produced NO visual response on the enemy at
    /// all — the only feedback was the player's own swing, so a hit and a whiff looked identical.
    ///
    /// === ONE PATH, EVERY ENEMY (AC1) ===
    /// It fires from <see cref="Health.Changed"/> on a detected HP DROP, never from the attacker, and it
    /// contains NO <see cref="BoarEnemy"/> / <see cref="SnakeEnemy"/> branch of any kind — everything it needs
    /// (the renderers, their materials, the part transforms, the body centre) is discovered generically from the
    /// children. A THIRD creature gets the full package by having this component added and nothing else.
    ///
    /// === [DFC-B] THE TWO ACCESS TRAPS, BOTH LOAD-BEARING ===
    ///  1. <b><c>GetComponent&lt;Renderer&gt;()</c> on an enemy ROOT returns NULL.</b> Both roots are bare
    ///     GameObjects carrying Health / AI / BodyRig only (`MovementCameraScene.BuildSnake` / `BuildBoar`);
    ///     EVERY renderer sits on a CHILD part. Even a singular <c>GetComponentInChildren</c> would flash 1 of
    ///     the boar's 7 parts / 1 of the snake's 13 — a flash on the body but not the head reads as a bug, not
    ///     as juice. So: <c>GetComponentsInChildren&lt;Renderer&gt;(true)</c>, cached ONCE in Awake.
    ///  2. <b><see cref="Health.Changed"/> fires on HEAL, <c>RestoreFull</c> and INIT too</b> (`Health.cs`
    ///     Changed / EnsureStarted) — it carries Current01 on ANY change. Without the previous-value guard the
    ///     whole package fires on every regen tick and on every spawn. The guard compares ABSOLUTE
    ///     <see cref="Health.Current"/>, not Current01: a dev-console `Boar HP max` dial moves Current01 without
    ///     any HP being lost, and that must not read as a hit.
    ///
    /// === WHY THERE IS NO CLOCK IN THE SHADER (AC2 [DFC-1]) ===
    /// The flash amplitude is computed HERE, in C#, and written to the material as a plain 0..1 float. The
    /// shader has no `_Time` read for this term at all. That is what makes the ticket's BLOCKING latch trap
    /// structurally unreachable: `_Time.y` is <c>Time.timeSinceLevelLoad</c>, NOT <c>Time.time</c>, so a
    /// C#-written `Time.time` stamp differenced against it saturates to full intensity FOREVER — a permanently
    /// white enemy from the first hit, green in EditMode, visible only in the shipped exe. With no stamp there
    /// is nothing to get wrong, and the decay is directly READ BACK from the material
    /// (<see cref="SampleMaterialFlash"/>) by the PlayMode decay test and the shipped `-verifyHitFeedback` gate.
    ///
    /// === MATERIAL INSTANCES, NEVER A MaterialPropertyBlock (AC2 [DFC-3]) ===
    /// An MPB breaks BOTH the SRP Batcher and GPU-Resident-Drawer eligibility; distinct material instances break
    /// neither (unity-conventions.md §SRP-Batcher). The marginal cost here is ZERO, not "small": both enemies
    /// ALREADY carry 20 unique inline `new Material(vc)` instances (7 boar + 13 snake) baked into Boot.unity, so
    /// the flash writes to materials that were already unique.
    ///
    /// === EXECUTION ORDER (AC3) ===
    /// Order 70, AFTER <see cref="BoarBodyRig"/> / <see cref="SnakeBodyChain"/> (both order 60), which write
    /// every part's world transform ABSOLUTELY each LateUpdate. The flinch is therefore a pure ADDITIVE post-pass
    /// on top of the posed body — the same idiom as the mandated CastawayArmPose (50) → HeldAxeRig (100) chain,
    /// and the reason two components can write the same transform here without fighting.
    ///
    /// No per-frame allocation, no per-frame Find, and no work at all while idle. NO MUTABLE STATICS.
    /// </summary>
    [DefaultExecutionOrder(70)] // after the body rigs (60) — the flinch is an additive pass on the posed parts
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyHitFeedback : MonoBehaviour
    {
        /// <summary>The shader float every part-material carries (default 0 = exact no-op). Cached id, never
        /// the string overload per frame (unity6-mastery §5).</summary>
        public const string HitFlashProperty = "_HitFlash";
        /// <summary>The warm-white pulse tone property on the shared world shader.</summary>
        public const string HitFlashColorProperty = "_HitFlashColor";

        [Header("Master switch (AC5 — `enemy_hit_feedback_enabled`, the one-flag revert path)")]
        [Tooltip("OFF = no flash, no flinch, no puff, under any condition. Defaults ON. The dev-console row " +
                 "drives this on EVERY enemy at once.")]
        public bool feedbackEnabled = true;

        [Header("Wiring (serialized editor-time; lazy fallbacks for bare rigs)")]
        [Tooltip("The pooled dust-puff emitter (the project's first pool). Null = flash + flinch still fire; " +
                 "the puff is simply skipped (a puff-less bare test rig never null-refs).")]
        public PooledBurstEmitter puff;
        [Tooltip("Biases the puff toward the side the hit came from — wired to the PLAYER root editor-time. " +
                 "Generic (a Transform, not an enemy type). Null = the puff plays at the body centre.")]
        public Transform contactBias;
        [Tooltip("The active-difficulty surface (the settings stepper drives DeathHandler.tier) the per-tier " +
                 "flinch STAGGER reads live. Null → Medium.")]
        public DeathHandler deathHandler;

        [Header("Flash (AC2 — ~0.08s, eased out, warm white, sub-1.0)")]
        [Tooltip("Flash duration in seconds (Sponsor-soak dial).")]
        public float flashSeconds = 0.08f;
        [Tooltip("PEAK flash amplitude — how far the lit colour lerps toward the warm-white flash tone at the " +
                 "top of the pulse. Sub-1.0 so the body never fully blanks out; the Sponsor rides this at soak.")]
        public float flashIntensity = 0.62f;
        [Tooltip("Warm-white pulse tone written to every part-material at init. Every channel sub-1.0 (HDR " +
                 "clamp) and warm — NEVER red (red on a creature reads as gore; kid-safe tone).")]
        public Color flashColor = new Color(0.95f, 0.92f, 0.86f, 1f);

        [Header("Flinch (AC3 — procedural recoil on the existing body rigs; NO Animator, NO clip)")]
        [Tooltip("Flinch duration in seconds — longer than the flash so the recoil visibly RESOLVES.")]
        public float flinchSeconds = 0.22f;
        [Tooltip("Backward hitch distance (u) at the peak of the recoil, along the body's own -forward.")]
        public float recoilBack = 0.14f;
        [Tooltip("Upward hitch (u) at the peak — the little lift that reads as 'that connected'.")]
        public float recoilLift = 0.05f;
        [Tooltip("Nose-UP toss (degrees) applied additively to every part at the peak — the head-toss read.")]
        public float tossDegrees = 11f;

        [Header("Flinch STAGGER per tier (AC3/AC5 — brief §2.6 'flinch stagger duration')")]
        [Tooltip("ACTIVE stagger seconds (the single field the AI reads). ApplyDifficulty writes it from the " +
                 "per-tier map below on every hit — so the per-tier dials must ALSO write their map entry or " +
                 "the live dial is clobbered (the dead-knob class).")]
        public float staggerSeconds = 0.15f;
        [Tooltip("EASY: 'staggers briefly' (brief §2.5) — the most forgiving tier.")]
        public float easyStaggerSeconds = 0.35f;
        [Tooltip("MEDIUM: a short hitch.")]
        public float medStaggerSeconds = 0.15f;
        [Tooltip("HARD: 'interrupts nothing (it keeps coming)' (brief §2.5) — 0 by design.")]
        public float hardStaggerSeconds = 0f;

        [Header("Dust puff (AC4 — dust-brown, never red, <=12 per burst)")]
        [Tooltip("Particles per HIT puff. brief §1.2 caps a burst at 12; the emitter clamps regardless.")]
        public int puffCount = 7;
        [Tooltip("Particles per DEATH puff — the softer beat (brief §2.5). Without it the soak's death moment " +
                 "has no feedback at all.")]
        public int deathPuffCount = 10;
        [Tooltip("Dust-brown, every channel sub-1.0, NEVER red (brief §2.5).")]
        public Color puffColor = new Color(0.55f, 0.44f, 0.31f, 1f);
        [Tooltip("Start-size multiplier on the hit puff (the authored template size is the 1.0 baseline).")]
        public float puffSize = 1f;
        [Tooltip("Start-size multiplier on the death puff — a touch broader + softer than the hit puff.")]
        public float deathPuffSize = 1.35f;
        [Tooltip("How far the puff is nudged from the body centre TOWARD contactBias (u) — the contact-point read.")]
        public float contactOffset = 0.28f;

        // === Observable outcomes (AC7 tests + the -verifyHitFeedback shipped gate read these) ===
        /// <summary>Hits (detected HP drops) this component has reacted to.</summary>
        public int HitCount { get; private set; }
        /// <summary>Death puffs fired (Health.Died is one-shot, so this is 0 or 1).</summary>
        public int DeathPuffCount { get; private set; }
        /// <summary>The flash amplitude last WRITTEN to the materials (0 when resting).</summary>
        public float FlashAmount { get; private set; }
        /// <summary>The flinch world-offset last APPLIED to the parts (Vector3.zero when resting).</summary>
        public Vector3 FlinchOffset { get; private set; }
        /// <summary>How many part-materials the flash writes — 7 on the boar, 13 on the snake.</summary>
        public int MaterialCount => _mats != null ? _mats.Length : 0;
        /// <summary>True while the brief post-hit movement stagger is active (AC3). Always false at HARD tier
        /// (hardStaggerSeconds = 0) and always false with the master switch off.</summary>
        public bool IsStaggered => feedbackEnabled && _staggerUntil > 0f && Time.time < _staggerUntil;

        /// <summary>True between the strike and the flash completing. ARMED AT STRIKE TIME, not at the first
        /// LateUpdate — so EditMode (which has no LateUpdate) can assert "the hit armed the flash" without the
        /// assertion being tautologically satisfiable by a driver that does nothing.</summary>
        public bool FlashActive => _flashActive;

        /// <summary>True between the strike and the flinch resolving. Armed at strike time (see FlashActive).</summary>
        public bool FlinchActive => _flinchActive;

        /// <summary>Flash phase 0→1 (Time.time-anchored, never deltaTime accumulation — the WindupNormT idiom,
        /// so headless PlayMode where deltaTime≈0 still advances it). 0 when no flash is running.</summary>
        public float FlashNormT => _flashActive
            ? Mathf.Clamp01((Time.time - _flashStartAt) / Mathf.Max(0.01f, flashSeconds)) : 0f;

        /// <summary>Flinch phase 0→1 (Time.time-anchored). 0 when no flinch is running.</summary>
        public float FlinchNormT => _flinchActive
            ? Mathf.Clamp01((Time.time - _flinchStartAt) / Mathf.Max(0.01f, flinchSeconds)) : 0f;

        /// <summary>The ACTIVE difficulty tier — read LIVE from the DeathHandler surface (the BoarAI.ActiveTier
        /// idiom); Medium when unwired.</summary>
        public FarHorizon.SurvivalNeed.DifficultyTier ActiveTier =>
            deathHandler != null ? deathHandler.tier : FarHorizon.SurvivalNeed.DifficultyTier.Medium;

        // --- runtime state (instance-only; no statics) ---
        private Health _health;
        private Renderer[] _renderers;
        private Transform[] _parts;
        private Material[] _mats;
        private bool _ownsMats;          // true only when WE instantiated the clones (play mode) — see EnsureInit
        private int _flashId;
        private int _flashColorId;
        private float _prevHp;
        private bool _flashActive;
        private bool _flinchActive;
        private float _flashStartAt;
        private float _flinchStartAt;
        private float _staggerUntil;
        private bool _initialized;
        private bool _subscribed;

        // ===================== PURE curve (the EditMode truth table — AC7) =====================

        /// <summary>
        /// THE impulse curve both the flash and the flinch ride: an EASED snap in over the first
        /// <paramref name="snapFrac"/> of the window, then a QUADRATIC ease-out settle back to exactly 0.
        ///
        /// 🔒 It is deliberately NOT LINEAR anywhere ([DFC-2]). The research note's `1 - x` is linear, and
        /// `game-juice.md` §1 must-have #1 is explicit that "under-applying easing (leaving linear) is the single
        /// most common 'feels cheap' defect" — quality-bar #2 (motion defaults lively / eased, never a static
        /// state change) is the bar AC1 names and AC6(b) claims, so a linear ramp fails the bar this whole
        /// ticket is measured against. Pure + static: EditMode asserts the shape with no scene.
        /// </summary>
        public static float Impulse01(float t01, float snapFrac)
        {
            float u = Mathf.Clamp01(t01);
            if (u <= 0f || u >= 1f) return 0f;
            float s = Mathf.Clamp(snapFrac, 0.01f, 0.9f);
            if (u < s)
            {
                // Eased SNAP in — smoothstep, so the rise has zero slope at the start (no linear ramp-on).
                return Mathf.SmoothStep(0f, 1f, u / s);
            }
            // Quadratic ease-OUT settle: fast off the peak, gentle into rest, exactly 0 at u = 1.
            float d = (u - s) / (1f - s);
            float inv = 1f - d;
            return inv * inv;
        }

        /// <summary>The snap fraction the shipped curve uses (exposed so tests + the gate assert the same shape
        /// the runtime rides, rather than a re-implemented proxy).</summary>
        public const float SnapFraction = 0.22f;

        // ======================================================================================

        private void Awake() => EnsureReady();

        /// <summary>
        /// Resolve the body, clone the part-materials, seed the HP watermark and SUBSCRIBE — idempotent.
        /// PUBLIC for the same reason <see cref="BoarAI.SyncDeathState"/> is: EditMode has NO component
        /// lifecycle, so an `AddComponent`ed driver never gets Awake or OnEnable and would silently sit
        /// unsubscribed, making every "a hit fires the package" assertion vacuously true. A test calls this
        /// once after wiring, exactly as the shipped Awake does.
        /// </summary>
        public void EnsureReady() => EnsureInit();

        // Lazy init (EditMode has no Awake on AddComponent — the Health / BoarAI lazy-resolve precedent). Never
        // caches a MISS: a null reference is re-resolved on the next call while it is still null (the
        // OnEnable-one-shot-cache trap, unity-conventions.md §Editor-vs-runtime).
        private void EnsureInit()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_initialized) return;
            _initialized = true;

            _flashId = Shader.PropertyToID(HitFlashProperty);
            _flashColorId = Shader.PropertyToID(HitFlashColorProperty);

            // [DFC-B] the ROOT has no Renderer — every one is on a CHILD part. `true` includes inactive parts so
            // a part hidden at author time still flashes when it comes back.
            _renderers = GetComponentsInChildren<Renderer>(true);
            int n = _renderers != null ? _renderers.Length : 0;
            _parts = new Transform[n];
            _mats = new Material[n];

            // `.material` INSTANTIATES a runtime clone the caller owns — exactly what we want at play time (the
            // per-enemy material instance the flash writes), and exactly what we must NOT do in the editor, where
            // it leaks a clone into the scene ("Instantiating material due to calling renderer.material during
            // edit mode"). Outside play mode we read sharedMaterial instead and own nothing, so EditMode rigs
            // exercise the same code path silently.
            _ownsMats = Application.isPlaying;
            for (int i = 0; i < n; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                _parts[i] = r.transform;
                _mats[i] = _ownsMats ? r.material : r.sharedMaterial;
                if (_mats[i] != null)
                {
                    if (_mats[i].HasProperty(_flashColorId)) _mats[i].SetColor(_flashColorId, flashColor);
                    if (_mats[i].HasProperty(_flashId)) _mats[i].SetFloat(_flashId, 0f); // rest state
                }
            }

            // Seed the previous-HP watermark DIRECTLY rather than relying on catching Health's init Changed:
            // Health.EnsureStarted fires Changed lazily on the FIRST read, which may already have happened
            // before we subscribed. Reading Current forces the seed and gives us an honest baseline in BOTH
            // orders ([DFC-B]).
            if (_health != null) _prevHp = _health.Current;

            ApplyDifficulty(ActiveTier);
            Subscribe();
        }

        // Subscribe/Unsubscribe are a balanced, _subscribed-guarded pair, driven from BOTH EnsureInit (so a
        // lazily-initialised driver in a lifecycle-less EditMode rig is live) and OnEnable (so a disable/enable
        // cycle re-arms). Double-subscription is impossible; a stale subscription after OnDisable likewise.
        private void Subscribe()
        {
            if (_health == null || _subscribed) return;
            _health.Changed += OnHealthChanged;
            _health.Died += OnDied;
            _subscribed = true;
            _prevHp = _health.Current;
        }

        private void Unsubscribe()
        {
            if (_health != null && _subscribed)
            {
                _health.Changed -= OnHealthChanged;
                _health.Died -= OnDied;
            }
            _subscribed = false;
        }

        private void OnEnable()
        {
            EnsureInit();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetVisuals();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            // `.material` clones are OURS — destroy them or an enemy respawn (DeathHandler) accumulates one set
            // per life ([DFC-B] hygiene). Never destroy sharedMaterials we only borrowed in edit mode.
            if (!_ownsMats || _mats == null) return;
            for (int i = 0; i < _mats.Length; i++)
                if (_mats[i] != null) Destroy(_mats[i]);
        }

        /// <summary>
        /// Set the ACTIVE per-tier stagger from the easy/med/hard map (AC5). Called on every hit so the LIVE tier
        /// is honoured (the BoarEnemy.ApplyDifficulty-on-gore idiom) — which is exactly why each per-tier dev
        /// console dial must write BOTH the active field AND its tier's map entry, or this call clobbers it back
        /// to the baked default (the dead-knob class, pinned by an AC7 test).
        /// </summary>
        public void ApplyDifficulty(FarHorizon.SurvivalNeed.DifficultyTier tier)
        {
            switch (tier)
            {
                case FarHorizon.SurvivalNeed.DifficultyTier.Easy: staggerSeconds = easyStaggerSeconds; break;
                case FarHorizon.SurvivalNeed.DifficultyTier.Hard: staggerSeconds = hardStaggerSeconds; break;
                default: staggerSeconds = medStaggerSeconds; break;
            }
        }

        // Health.Changed carries Current01 and fires on damage / heal / RestoreFull / INIT alike ([DFC-B]) — so
        // the previous-value guard is the whole gate. Compared on ABSOLUTE Current, not Current01: an `HP max`
        // dial moves the normalized value without any HP being lost, and that is not a hit.
        private void OnHealthChanged(float _)
        {
            if (_health == null) return;
            float now = _health.Current;
            float dropped = _prevHp - now;
            _prevHp = now;
            if (dropped <= 1e-4f) return;   // heal / RestoreFull / init / a pure max-dial — NOT a hit
            Strike();
        }

        // The one impulse: flash + flinch + puff + stagger, all off one detected HP drop.
        private void Strike()
        {
            EnsureInit();
            HitCount++;
            if (!feedbackEnabled) return;

            ApplyDifficulty(ActiveTier);       // read the LIVE tier on every hit (the per-gore idiom)
            _flashActive = true; _flashStartAt = Time.time;
            _flinchActive = true; _flinchStartAt = Time.time;
            _staggerUntil = staggerSeconds > 0f ? Time.time + staggerSeconds : 0f;
            EmitPuff(puffCount, puffSize);
        }

        private void OnDied()
        {
            EnsureInit();
            if (!feedbackEnabled) return;
            // brief §2.5 — the death beat. Without it the soak's death moment has NO feedback at all and the
            // "is it nearly down?" read is only half-testable: the player never sees the moment it goes down.
            DeathPuffCount++;
            EmitPuff(deathPuffCount, deathPuffSize);
        }

        private void EmitPuff(int count, float size)
        {
            if (puff == null || count <= 0) return;
            puff.Emit(ContactPoint(), count, puffColor, size);
        }

        /// <summary>The puff origin: the body's renderer-bounds centre, nudged toward <see cref="contactBias"/>
        /// (the player) so the burst reads as being AT the contact point rather than inside the creature.
        /// Generic — it knows nothing about which creature this is.</summary>
        public Vector3 ContactPoint()
        {
            EnsureInit();
            Vector3 c = BodyCentre();
            if (contactBias == null) return c;
            Vector3 d = contactBias.position - c;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return c;
            return c + d.normalized * Mathf.Max(0f, contactOffset);
        }

        private Vector3 BodyCentre()
        {
            if (_renderers == null || _renderers.Length == 0) return transform.position;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                sum += _renderers[i].bounds.center;
                n++;
            }
            return n > 0 ? sum / n : transform.position;
        }

        // Order 70: the body rigs (order 60) have already written every part's world transform ABSOLUTELY this
        // frame, so everything below is a pure additive post-pass — never a fight over the same transform.
        private void LateUpdate()
        {
            if (!_initialized) EnsureInit();

            if (!feedbackEnabled)
            {
                if (_flashActive || _flinchActive) ResetVisuals();
                return;
            }

            // --- FLASH: write the eased amplitude to every part-material; write exactly 0 ONCE on completion. ---
            if (_flashActive)
            {
                float t = FlashNormT;
                if (t >= 1f)
                {
                    _flashActive = false;
                    WriteFlash(0f);        // the resting value — this is what a latched flash would never reach
                }
                else
                {
                    WriteFlash(Mathf.Clamp01(flashIntensity) * Impulse01(t, SnapFraction));
                }
            }

            // --- FLINCH: additive recoil on the already-posed parts. ---
            if (!_flinchActive) return;
            float ft = FlinchNormT;
            if (ft >= 1f)
            {
                _flinchActive = false;
                FlinchOffset = Vector3.zero;   // the recoil RESOLVES (no sustained wobble)
                return;
            }
            ApplyFlinch(Impulse01(ft, SnapFraction));
        }

        private void ApplyFlinch(float e)
        {
            if (_parts == null || e <= 0f) return;

            Vector3 fwd = transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            Vector3 offset = (-fwd * recoilBack + Vector3.up * recoilLift) * e;
            FlinchOffset = offset;
            // NEGATIVE angle about the body's right axis = nose UP (Unity: +90° about right pitches forward DOWN).
            // A ROTATION + TRANSLATION only — never a scale: squash/stretch is a hard don't (game-juice.md §2).
            Quaternion toss = Quaternion.AngleAxis(-tossDegrees * e, right);

            for (int i = 0; i < _parts.Length; i++)
            {
                var p = _parts[i];
                if (p == null) continue;
                p.position += offset;
                p.rotation = toss * p.rotation;
            }
        }

        private void WriteFlash(float amount)
        {
            FlashAmount = amount;
            if (_mats == null) return;
            for (int i = 0; i < _mats.Length; i++)
                if (_mats[i] != null && _mats[i].HasProperty(_flashId))
                    _mats[i].SetFloat(_flashId, amount);
        }

        /// <summary>Hard-stop every channel and return the body to its resting look (used by the master switch,
        /// OnDisable, and the off-switch test). Idempotent.</summary>
        public void ResetVisuals()
        {
            _flashActive = false;
            _flinchActive = false;
            _staggerUntil = 0f;
            FlinchOffset = Vector3.zero;
            WriteFlash(0f);
        }

        /// <summary>Read the flash value actually STORED on part-material <paramref name="i"/> (the shipped-gate
        /// + PlayMode decay assertion read this — "SetFloat was called" is not evidence the value decays).</summary>
        public float SampleMaterialFlash(int i)
        {
            EnsureInit();
            if (_mats == null || i < 0 || i >= _mats.Length || _mats[i] == null) return -1f;
            return _mats[i].HasProperty(_flashId) ? _mats[i].GetFloat(_flashId) : -1f;
        }

        /// <summary>The HIGHEST flash value across every part-material — the "did ANY part light up" read.</summary>
        public float MaxMaterialFlash()
        {
            EnsureInit();
            float m = 0f;
            for (int i = 0; i < MaterialCount; i++) m = Mathf.Max(m, SampleMaterialFlash(i));
            return m;
        }

        /// <summary>The LOWEST flash value across every part-material — the "did they ALL light up TOGETHER"
        /// read (a flash on the body but not the head reads as a bug, not as juice — AC2).</summary>
        public float MinMaterialFlash()
        {
            EnsureInit();
            if (MaterialCount == 0) return -1f;
            float m = float.MaxValue;
            for (int i = 0; i < MaterialCount; i++) m = Mathf.Min(m, SampleMaterialFlash(i));
            return m;
        }
    }
}
