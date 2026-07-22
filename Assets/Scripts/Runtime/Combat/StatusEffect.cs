namespace FarHorizon.Combat
{
    /// <summary>
    /// The status-effect KIND discriminator (Combat POC 86cah7xxp, AC6). A general data-driven framework
    /// that is DoT/stun/slow-capable in shape, but the POC ships <see cref="Bleed"/> ONLY (poison/stun/slow
    /// are a later ticket — OOS). The enum + the <see cref="StatusEffectSpec"/> below are the framework; the
    /// StatusEffectController applies whichever specs are attached. Appended-only so a later kind never
    /// shifts Bleed=0's serialized int.
    /// </summary>
    public enum StatusEffectKind
    {
        Bleed,   // a damage-over-time: ticks HP down through Health.ApplyDamage. The ONLY kind shipped.
        // Poison, Stun, Slow — reserved framework kinds (later ticket; not applied by the POC controller).
    }

    /// <summary>
    /// A DATA-DRIVEN status-effect spec (AC6) — the general framework the POC ships with, populated for
    /// BLEED only. A weapon's <c>onHitStatus</c> (AC4) carries one of these; an enemy bite can carry one
    /// too (AC7 — bleed works BOTH ways). The controller (<see cref="StatusEffectController"/>) applies it
    /// as a DoT that ticks <see cref="damagePerSecond"/> through the shared <see cref="Health.ApplyDamage"/>
    /// seam (AC1/AC6) over its <see cref="durationSeconds"/>, then expires.
    ///
    /// A struct (value type) so a weapon's onHitStatus is a plain serialized field, not a reference that
    /// can dangle. <see cref="None"/> = no effect (a weapon with no on-hit status). Pure data — no Unity
    /// lifecycle — so the DoT math is unit-testable headlessly.
    /// </summary>
    [System.Serializable]
    public struct StatusEffectSpec
    {
        public StatusEffectKind kind;
        [UnityEngine.Tooltip("HP removed per second while the effect is active (the DoT rate). For Bleed.")]
        public float damagePerSecond;
        [UnityEngine.Tooltip("How long the effect lasts (seconds). 0 = no effect (None).")]
        public float durationSeconds;
        [UnityEngine.Tooltip("The DamageType the DoT ticks deal (so a target's resistance profile still " +
                             "modulates a bleed — a bleed is Slash-typed by default, an open cut).")]
        public DamageType damageType;

        /// <summary>An empty spec — no effect. A weapon with no on-hit status carries this (duration 0).</summary>
        public static StatusEffectSpec None => new StatusEffectSpec { kind = StatusEffectKind.Bleed, damagePerSecond = 0f, durationSeconds = 0f, damageType = DamageType.Slash };

        /// <summary>True if this spec would actually DO something (positive rate AND duration). A weapon
        /// with <see cref="None"/> (or a zeroed spec) applies nothing — the controller skips it.</summary>
        public bool IsActive => damagePerSecond > 0f && durationSeconds > 0f;

        /// <summary>Build a BLEED spec (the one kind the POC ships) — the convenience a weapon uses to
        /// author its onHitStatus. Bleed is Slash-typed (an open cut) by default.</summary>
        public static StatusEffectSpec MakeBleed(float damagePerSecond, float durationSeconds)
            => new StatusEffectSpec { kind = StatusEffectKind.Bleed, damagePerSecond = damagePerSecond, durationSeconds = durationSeconds, damageType = DamageType.Slash };
    }
}
