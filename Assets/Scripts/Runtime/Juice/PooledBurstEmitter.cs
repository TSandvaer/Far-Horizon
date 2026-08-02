using UnityEngine;
using UnityEngine.Pool;

namespace FarHorizon.Juice
{
    /// <summary>
    /// THE PROJECT'S FIRST OBJECT POOL, and its first <see cref="ParticleSystem"/> (ticket 86caxjwb3 AC4).
    /// Before this component `git grep -n 'ParticleSystem' -- Assets/` and `ObjectPool` both returned ZERO hits:
    /// `game-juice.md` §1.4's pooling guidance was a PRESCRIPTION, never a record of shipped code (DECISIONS
    /// 2026-07-27, "Prescribed-not-shipped"). There was no berry-pop precedent to copy — earlier spec drafts
    /// cited one that does not exist.
    ///
    /// === SO THIS IS THE PRECEDENT — read this before building the SECOND juice burst ===
    /// It is deliberately GENERIC: nothing here knows about enemies, hits, or dust. A burst is
    /// (position, count, colour, size) and the emitter owns pooling, recycling and the bounded-allocation
    /// property. A second effect (chop chips, berry pop, water droplets, item-land dust) adds ANOTHER
    /// <see cref="PooledBurstEmitter"/> with its OWN authored template — it does NOT add a second pool
    /// implementation, and it does NOT reach for per-event <c>Instantiate</c>/<c>Destroy</c> (that spikes GC,
    /// which is the whole reason §1.4 asks for a pool).
    ///
    /// The five things that make it work, each of which is a trap if you skip it:
    ///
    ///  1. <b>The template is AUTHORED EDITOR-TIME and SERIALIZED, never built in Awake.</b> Its mesh, material
    ///     and every module value are baked into Boot.unity by <c>MovementCameraScene</c>
    ///     (Awake-built hierarchies do not serialize and can ship mangled — unity-conventions.md
    ///     §Editor-vs-runtime, the spike's "legs pointing upwards" incident). This runtime component only
    ///     CLONES what is already serialized.
    ///  2. <b>The template GameObject is INACTIVE.</b> So it never plays on its own, and a clone of it starts
    ///     inactive too — the pool activates on Get and deactivates on Release.
    ///  3. <b><c>main.stopAction = ParticleSystemStopAction.Callback</c> is LOAD-BEARING</b> ([DFC-4c]): it is
    ///     the ONLY reason <c>OnParticleSystemStopped</c> is delivered, and that message is the ONLY thing that
    ///     hands an instance back. Without it the pool silently never recycles while every other assertion still
    ///     passes. Asserted in EditMode (serialized template) AND PlayMode (live instances), and re-asserted
    ///     defensively here at pool-create time.
    ///  4. <b>The material is SEPARATE from the world palette material</b> and is NOT a
    ///     <c>MaterialPropertyBlock</c> (brief §1.2): the palette material is the world's ~1-draw-call batch.
    ///     Particles are explicitly EXEMPT from the no-MPB rule anyway (`game-juice.md` §2) — a
    ///     <c>ParticleSystemRenderer</c> is not the disqualified <c>MeshRenderer</c> path — so there is no
    ///     conflict between "pool it" and "never MPB a juice VFX".
    ///  5. <b>Per-burst variation goes through <c>main.startColor</c> / <c>main.startSizeMultiplier</c></b>,
    ///     never through a second material. One material, many looks.
    ///
    /// No per-frame allocation and no per-frame work at all: this component has no Update. NO MUTABLE STATICS.
    /// </summary>
    [DefaultExecutionOrder(-50)] // pool exists before any gameplay component can ask it to emit
    [DisallowMultipleComponent]
    public sealed class PooledBurstEmitter : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time — never built in Awake)")]
        [Tooltip("The INACTIVE authored ParticleSystem this pool clones. Its mesh / material / modules are " +
                 "baked into Boot.unity by MovementCameraScene.BuildHitFeedback and serialize with the scene.")]
        public ParticleSystem template;

        [Header("Pool bounds (unity6-mastery §5 — UnityEngine.Pool.ObjectPool<T>)")]
        [Tooltip("Instances pre-sized on first use.")]
        public int defaultCapacity = 4;
        [Tooltip("Hard ceiling on RETAINED instances. Releases beyond this destroy instead of retaining, so " +
                 "the pool can never grow without bound even under a pathological burst rate.")]
        public int maxPoolSize = 12;

        [Header("Burst bounds (brief §1.2 — the calm-tone cap)")]
        [Tooltip("Hard cap on particles per burst. brief §1.2: <=12. A caller asking for more is CLAMPED, not " +
                 "obeyed — the cap is a tone constraint, not a suggestion.")]
        public int maxParticlesPerBurst = 12;

        // === Observable outcomes (the AC7 pool-integrity test + the shipped -verifyHitFeedback gate read these) ===
        /// <summary>How many ParticleSystem instances the pool has ever CREATED. The bounded-allocation
        /// observable: N recycled bursts must NOT create N instances.</summary>
        public int CreatedCount { get; private set; }
        /// <summary>How many bursts have been emitted.</summary>
        public int EmitCount { get; private set; }
        /// <summary>How many instances have been handed back through OnParticleSystemStopped. If this stays 0
        /// while EmitCount climbs, the stop-action is wrong and the pool is leaking ([DFC-4c]).</summary>
        public int ReleaseCount { get; private set; }
        /// <summary>Instances currently checked OUT of the pool (playing).</summary>
        public int LiveCount { get; private set; }

        /// <summary>True when the AUTHORED template carries the load-bearing Callback stop-action ([DFC-4c]).
        /// False means the pool cannot recycle — the silent-leak class.</summary>
        public bool TemplateStopActionIsCallback =>
            template != null && template.main.stopAction == ParticleSystemStopAction.Callback;

        /// <summary>The instance the most recent <see cref="Emit"/> checked out. Diagnostic handle only — the
        /// pool may already have taken it back. See <see cref="DescribeLive"/>.</summary>
        public ParticleSystem LastEmitted { get; private set; }

        /// <summary>
        /// GROUND TRUTH about the most recent burst, for when a burst is provably firing (EmitCount climbs,
        /// OnParticleSystemStopped fires) and yet NOTHING IS ON SCREEN. That combination has exactly one honest
        /// next step — dump what the live system actually is — because every cheap hypothesis for it (wrong
        /// position, culled renderer, zero particles, stripped material, zero-size mesh) is indistinguishable
        /// from the outside and they are NOT distinguishable by staring at a frame. This is a permanent
        /// diagnostic surface, not scaffolding: the shipped `-verifyHitFeedback` gate logs it every run, so the
        /// next person who meets an invisible burst reads the answer instead of re-deriving it.
        /// </summary>
        public string DescribeLive()
        {
            if (LastEmitted == null) return "<no burst emitted yet>";
            var r = LastEmitted.GetComponent<ParticleSystemRenderer>();
            var m = LastEmitted.main;
            return "pos=" + LastEmitted.transform.position.ToString("F2") +
                   " particles=" + LastEmitted.particleCount +
                   " playing=" + LastEmitted.isPlaying +
                   " activeInHierarchy=" + LastEmitted.gameObject.activeInHierarchy +
                   " rendererEnabled=" + (r != null && r.enabled) +
                   " rendererVisible=" + (r != null && r.isVisible) +
                   " renderMode=" + (r != null ? r.renderMode.ToString() : "<null>") +
                   " meshVerts=" + (r != null && r.mesh != null ? r.mesh.vertexCount : 0) +
                   " meshExtent=" + (r != null && r.mesh != null
                        ? (r.mesh.bounds.extents.magnitude * 2f).ToString("F3") : "0") +
                   " shader=" + (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null
                        ? r.sharedMaterial.shader.name : "<null>") +
                   " startSize=" + m.startSize.constantMin.ToString("F2") + ".." + m.startSize.constantMax.ToString("F2") +
                   " startColor=" + m.startColor.color.ToString("F2") +
                   " lifetime=" + m.startLifetime.constantMax.ToString("F2");
        }

        private ObjectPool<ParticleSystem> _pool;
        private bool _initialized;

        private void Awake() => EnsureInit();

        // Lazy init (EditMode has no Awake on AddComponent — the Health/BoarAI lazy-resolve precedent).
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (template == null) return;
            _pool = new ObjectPool<ParticleSystem>(
                CreateInstance, OnTakeFromPool, OnReturnToPool, OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: Mathf.Max(1, defaultCapacity),
                maxSize: Mathf.Max(1, maxPoolSize));
        }

        private ParticleSystem CreateInstance()
        {
            var go = Instantiate(template.gameObject, transform);
            go.name = template.name + "_pooled" + CreatedCount.ToString("00");
            go.SetActive(false); // the template is inactive, so the clone is too — belt-and-braces
            var ps = go.GetComponent<ParticleSystem>();

            // [DFC-4c] defensive re-assert on the LIVE clone: without Callback, OnParticleSystemStopped is never
            // delivered and this instance never comes back. The authored template is the source of truth (and is
            // asserted in EditMode) — this only stops a clone from diverging.
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;
            main.playOnAwake = false;

            var ret = go.GetComponent<PooledBurstReturn>();
            if (ret == null) ret = go.AddComponent<PooledBurstReturn>();
            ret.system = ps;
            ret.onStopped = HandleStopped;

            CreatedCount++;
            return ps;
        }

        private void OnTakeFromPool(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.gameObject.SetActive(true);
            LiveCount++;
        }

        private void OnReturnToPool(ParticleSystem ps)
        {
            if (ps == null) return;
            // Clear (not Stop) — Stop from INSIDE the OnParticleSystemStopped callback is a re-entrancy risk on
            // an already-stopped system; the system is by definition finished when this runs.
            ps.Clear(true);
            ps.gameObject.SetActive(false);
            LiveCount = Mathf.Max(0, LiveCount - 1);
            ReleaseCount++;
        }

        private void OnDestroyInstance(ParticleSystem ps)
        {
            if (ps != null) Destroy(ps.gameObject);
        }

        private void HandleStopped(PooledBurstReturn ret)
        {
            if (ret == null || ret.system == null || _pool == null) return;
            if (!ret.system.gameObject.activeSelf) return; // already returned — never double-release
            _pool.Release(ret.system);
        }

        /// <summary>
        /// Emit ONE burst at <paramref name="worldPos"/>. Returns false when the emitter is unwired (a template-
        /// less rig / bare test) so a caller can log rather than null-ref. <paramref name="count"/> is CLAMPED to
        /// <see cref="maxParticlesPerBurst"/> (brief §1.2's calm-tone cap — a caller cannot crank past it), and
        /// <paramref name="sizeScale"/> multiplies the authored start size so one template serves a hit puff and
        /// a softer death puff without a second material or a second pool.
        /// </summary>
        public bool Emit(Vector3 worldPos, int count, Color color, float sizeScale)
        {
            EnsureInit();
            if (_pool == null || template == null) return false;

            ParticleSystem ps = _pool.Get();
            if (ps == null) return false;

            ps.transform.position = worldPos;

            var main = ps.main;
            main.startColor = color;
            main.startSizeMultiplier = Mathf.Max(0.01f, sizeScale);

            // ONE burst at t=0 of the requested (clamped) count. The template authors burstCount = 1 so index 0
            // always exists; the guard covers a hand-edited template.
            var em = ps.emission;
            if (em.burstCount < 1) em.burstCount = 1;
            int n = Mathf.Clamp(count, 1, Mathf.Max(1, maxParticlesPerBurst));
            em.SetBurst(0, new ParticleSystem.Burst(0f, (short)n));

            ps.Play(true);
            LastEmitted = ps;
            EmitCount++;
            return true;
        }
    }
}
