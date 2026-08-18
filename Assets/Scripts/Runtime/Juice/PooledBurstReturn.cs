using System;
using UnityEngine;

namespace FarHorizon.Juice
{
    /// <summary>
    /// The RETURN half of <see cref="PooledBurstEmitter"/>'s pooling contract (ticket 86caxjwb3 AC4) — a
    /// two-field companion added to every POOLED ParticleSystem instance at pool-create time. Unity delivers
    /// <c>OnParticleSystemStopped</c> to components on the particle system's OWN GameObject, so the pool cannot
    /// receive it directly; this is the smallest possible bridge from that message back to
    /// <see cref="UnityEngine.Pool.ObjectPool{T}.Release"/>.
    ///
    /// ⚠ THE SILENT-LEAK TRAP THIS PAIRS WITH (ticket [DFC-4c]): <c>OnParticleSystemStopped</c> is delivered
    /// ONLY when the system's <c>main.stopAction == ParticleSystemStopAction.Callback</c>. With any other stop
    /// action the message NEVER arrives, this component never fires, the pool never gets its instance back —
    /// and EVERY other assertion still passes, because emitting keeps working (it just allocates a fresh
    /// instance every time). So the stop-action is asserted explicitly, in EditMode against the SERIALIZED
    /// template (<c>HitFeedbackSceneTests</c>) and in PlayMode against the live pooled instances
    /// (<c>HitFeedbackPlayModeTests</c>) — a "the puff played" assertion cannot see this class of defect.
    ///
    /// NOT serialized into the scene: pooled instances are created at runtime by the pool's create-func, so
    /// this component only ever exists on those clones, never on the authored template. <see cref="onStopped"/>
    /// is <see cref="NonSerializedAttribute"/> for the same reason (a delegate cannot serialize meaningfully).
    /// NO MUTABLE STATICS.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledBurstReturn : MonoBehaviour
    {
        /// <summary>The pooled system this companion returns. Wired by the pool's create-func.</summary>
        public ParticleSystem system;

        /// <summary>Invoked when the pooled system finishes (duration elapsed AND every particle died). Wired by
        /// the pool's create-func to its Release. NonSerialized — pooled instances are runtime-created.</summary>
        [NonSerialized] public Action<PooledBurstReturn> onStopped;

        /// <summary>How many times this instance has been handed back (diagnostic/test read).</summary>
        public int StoppedCount { get; private set; }

        // Unity's particle-stop message. Fires ONLY under stopAction == Callback (see the class note).
        private void OnParticleSystemStopped()
        {
            StoppedCount++;
            onStopped?.Invoke(this);
        }
    }
}
