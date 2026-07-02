using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// A craftable WEAPON's data-driven attributes (Combat POC 86cah7xxp, AC4). Identity = type × material
    /// tier (POC = wood / stone). Two CONTRASTING weapons ship: the AXE (Slash, medium reach, hits harder
    /// up close) and the SPEAR (Pierce, long reach). The attributes DRIVE the damage through the AC1 seam
    /// (<see cref="Health.ApplyDamage"/>) — the swing computes damage from THESE fields, never a magic
    /// number at the call site (AC10 asserts the mapping, not a literal).
    ///
    /// A ScriptableObject per unity6-mastery §6 (all tuning/content config = SO assets, not MonoBehaviour
    /// fields or JSON). For pure-logic tests it is built in code via <see cref="Create"/> (no .asset
    /// round-trip). The POC's two defs are minted in <see cref="WeaponCatalog"/> (the axe/spear lookup).
    ///
    /// The <see cref="animationId"/> is the swing ANIMATION REF (AC4/AC5) — an opaque id the swing driver
    /// maps to a per-weapon swing. Kept a string (not a hard Animator ref) so the SWING APPROACH is not
    /// baked in here: the ⚠ procedural-vs-Mixamo swing decision (AC5 scope question) is resolved in the
    /// swing driver, and this def just names which swing to play. The POC ships a placeholder swing.
    /// </summary>
    [CreateAssetMenu(menuName = "Far Horizon/Weapon Def", fileName = "weapon")]
    public sealed class WeaponDef : ScriptableObject
    {
        [SerializeField, Tooltip("Stable id (matches the ItemDef id when the weapon is also an inventory " +
                                 "item — e.g. \"axe\" / \"spear\"). The single lookup key.")]
        private string _id = "";
        [SerializeField, Tooltip("Player-facing name.")]
        private string _displayName = "";
        [SerializeField, Tooltip("Base damage this weapon deals per hit BEFORE the target's resistance " +
                                 "matchup + difficulty tier (AC8) modulate it in Health.ApplyDamage.")]
        private float _damage = 10f;
        [SerializeField, Tooltip("Reach (world units) — the max planar distance a hit lands. Axe = medium; " +
                                 "spear = LONG (AC4 contrast). Drives the hit-scan range.")]
        private float _reach = 2.0f;
        [SerializeField, Tooltip("Attacks per second — the swing cadence (higher = faster swings). Axe is " +
                                 "faster up close; spear is slower but longer.")]
        private float _attackSpeed = 1.2f;
        [SerializeField, Tooltip("The damage TYPE this weapon deals (AC4/AC8): axe = Slash, spear = Pierce.")]
        private DamageType _damageType = DamageType.Slash;
        [SerializeField, Tooltip("Optional on-hit status effect (AC4/AC6) — e.g. the axe applies a light " +
                                 "Bleed. StatusEffectSpec.None = no on-hit status.")]
        private StatusEffectSpec _onHitStatus = StatusEffectSpec.None;
        [SerializeField, Tooltip("The swing ANIMATION ref (AC5) — an opaque id the swing driver maps to a " +
                                 "per-weapon swing. \"axe_chop\" / \"spear_thrust\". The swing APPROACH " +
                                 "(procedural vs Mixamo) is resolved in the driver, not baked here.")]
        private string _animationId = "";

        /// <summary>Stable id — matches the inventory ItemDef id (axe / spear).</summary>
        public string Id => _id;
        /// <summary>Player-facing name.</summary>
        public string DisplayName => _displayName;
        /// <summary>Base damage per hit (before resistance + tier modulation in Health.ApplyDamage).</summary>
        public float Damage => _damage;
        /// <summary>Reach in world units — axe medium, spear long (AC4).</summary>
        public float Reach => _reach;
        /// <summary>Attacks per second — the swing cadence (AC4).</summary>
        public float AttackSpeed => _attackSpeed;
        /// <summary>The damage type (AC4/AC8) — axe Slash, spear Pierce.</summary>
        public DamageType DamageType => _damageType;
        /// <summary>Optional on-hit status effect (AC4/AC6) — None if the weapon has no on-hit status.</summary>
        public StatusEffectSpec OnHitStatus => _onHitStatus;
        /// <summary>The swing animation ref id (AC5) — the driver maps it to a per-weapon swing.</summary>
        public string AnimationId => _animationId;

        /// <summary>
        /// Build a WeaponDef in code (bootstrap + EditMode tests — no .asset round-trip). The identity
        /// fields are set once; do not mutate after the catalog ships (like ItemDef.Init).
        /// </summary>
        public static WeaponDef Create(string id, string displayName, float damage, float reach,
            float attackSpeed, DamageType damageType, StatusEffectSpec onHitStatus, string animationId)
        {
            var w = CreateInstance<WeaponDef>();
            w.name = id;
            w._id = id;
            w._displayName = displayName;
            w._damage = damage;
            w._reach = reach;
            w._attackSpeed = attackSpeed;
            w._damageType = damageType;
            w._onHitStatus = onHitStatus;
            w._animationId = animationId;
            return w;
        }
    }
}
