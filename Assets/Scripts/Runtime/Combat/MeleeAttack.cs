using UnityEngine;
using FarHorizon.Settings;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The player's melee ATTACK (Combat POC 86cah7xxp, AC4/AC5/AC6/AC8) — swing the SELECTED weapon on a
    /// LEFT-CLICK (active input, one click = one strike; NOT proximity-auto), hit the nearest in-reach enemy
    /// <see cref="Health"/>, deal <see cref="WeaponDef"/>-driven damage through the shared AC1 seam, and
    /// apply the weapon's on-hit status (AC6). The selected weapon is resolved from the selected belt item
    /// (axe / spear) — the SAME selection surface the chop uses (Inventory.IsAxeSelectedInBelt sibling).
    ///
    /// === The swing (AC5) — PLACEHOLDER pending the Sponsor's procedural-vs-Mixamo call ===
    /// ⚠ SCOPE QUESTION (raised in the dispatch): [[chop-swing-mixamo-clip-not-procedural]] says the Sponsor
    /// wants a proper MIXAMO attack clip; procedural-animation-verbs.md says NO new Animator clip. These
    /// CONFLICT for the per-weapon swing. Until the Sponsor rules, this POC reuses the EXISTING chop swing
    /// (<see cref="CastawayCharacter.TriggerChop"/> — the already-wired Mixamo melee Attack state) as the
    /// PLACEHOLDER swing for BOTH weapons, scaled by the weapon's attackSpeed via the existing ChopSpeed
    /// param. This proves the reach/damage/status SYSTEM (the POC's job) without committing the swing art:
    /// the per-weapon DISTINCT swing (axe_chop vs spear_thrust) is deferred to the AC5 decision — the driver
    /// maps <see cref="WeaponDef.AnimationId"/> to a swing there. NO new Animator clip/state/layer is added
    /// here (procedural-animation-verbs.md rule respected); the placeholder rides the existing state.
    ///
    /// === Guards (mirror ChopTree — the click must only attack in the game world) ===
    /// The pure <see cref="ShouldAttackOnClick"/> mirrors ChopTree.ShouldChopOnClick: a weapon must be
    /// selected + a target in reach + no modal panel open + not over the inventory/belt UI + RMB not held
    /// (a camera-orbit drag). <see cref="RequestAttackClick"/> is the input-independent latch (the analog of
    /// ChopTree.RequestChopClick) so headless PlayMode + the shipped capture exercise the SAME attack path.
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no reset here.
    /// </summary>
    public sealed class MeleeAttack : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory whose SELECTED belt item chooses the active weapon (axe / spear). Wired at " +
                 "bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The weapon catalog (axe/spear attributes). Built in code at Awake if unset (no .asset " +
                 "wiring required for the POC, mirroring Inventory.Catalog).")]
        public WeaponCatalog weaponCatalog;

        [Tooltip("The player ROOT transform the reach is measured from (the moving agent). Wired at " +
                 "bootstrap; falls back to this GameObject's transform.")]
        public Transform player;

        [Tooltip("The castaway whose swing plays on each attack (the PLACEHOLDER swing — the existing chop " +
                 "Attack state via TriggerChop, pending the AC5 procedural-vs-Mixamo decision). Wired at " +
                 "bootstrap; scene-search fallback. Null → the attack still lands damage, just no arm swing.")]
        public CastawayCharacter character;

        [Tooltip("The inventory/belt UI for the over-UI left-click guard (a click over the belt must NOT " +
                 "attack). Wired at bootstrap; null → the over-UI guard is skipped (a bare rig).")]
        public InventoryUI inventoryUI;

        [Header("Interaction")]
        [Tooltip("Minimum seconds between landed attacks. The attack is per LEFT-CLICK; this is a small " +
                 "cooldown so a stray double-edge can't out-pace the swing read. Also scaled DOWN by the " +
                 "weapon's attackSpeed so a faster weapon can swing sooner. 0 = no cooldown.")]
        public float baseAttackCooldown = 0.35f;

        private WeaponCatalog _catalog;
        private bool _attackClickRequested;
        private float _lastAttackAt = float.NegativeInfinity;

        // === Observable outcomes (AC5/AC10 — the tests assert these) ===
        /// <summary>How many attacks have LANDED a hit on a target (a swing that connected).</summary>
        public int HitsLanded { get; private set; }
        /// <summary>How many swings have been SWUNG (whether or not they hit) — a click that passed the gate.</summary>
        public int SwingsFired { get; private set; }
        /// <summary>The damage the LAST landed hit actually removed (through the seam, after resistance +
        /// tier). Exposed so a test asserts axe vs spear produce different outcomes.</summary>
        public float LastDamageDealt { get; private set; }
        /// <summary>The id of the weapon the LAST swing used ("axe" / "spear" / null if none selected).</summary>
        public string LastWeaponId { get; private set; }

        private void Awake()
        {
            if (player == null) player = transform;
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (character == null) character = FindObjectOfType<CastawayCharacter>();
            if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
            EnsureCatalog();
        }

        private void EnsureCatalog()
        {
            if (_catalog != null) return;
            if (weaponCatalog != null && weaponCatalog.All != null && weaponCatalog.All.Count > 0)
            {
                _catalog = weaponCatalog;
            }
            else
            {
                _catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
                _catalog.BuildDefaults();
            }
        }

        /// <summary>The WeaponDef for the currently-selected belt item (axe / spear), or null if the
        /// selected item is not a weapon (or nothing selected). The selection surface the swing reads.</summary>
        public WeaponDef SelectedWeapon
        {
            get
            {
                EnsureCatalog();
                if (inventory == null || inventory.Model == null) return null;
                var sel = inventory.Model.SelectedBeltStack;
                if (sel.IsEmpty || sel.Def == null) return null;
                return _catalog.ById(sel.Def.Id);
            }
        }

        /// <summary>
        /// PURE attack-on-a-left-click decision (the unit-testable guard truth-table, mirrors
        /// ChopTree.ShouldChopOnClick): all of — a weapon is selected, a target is in reach, no modal panel
        /// owns the screen, the click is NOT over the inventory/belt UI, the RMB is NOT held (no orbit drag).
        /// Static + dependency-free so the EditMode guard asserts the whole table with no scene/Input/UI rig.
        /// </summary>
        public static bool ShouldAttackOnClick(bool weaponSelected, bool targetInReach,
                                               bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)
            => weaponSelected && targetInReach && !uiPanelOpen && !pointerOverUI && !rmbHeld;

        /// <summary>Request ONE attack swing programmatically — the input-independent analog of a left-click
        /// (mirrors ChopTree.RequestChopClick). Latched + consumed on the next Update so headless PlayMode +
        /// the shipped capture exercise the SAME attack path where a real mouse button can't be injected.</summary>
        public void RequestAttackClick() => _attackClickRequested = true;

        private void Update()
        {
            bool clickEdge = _attackClickRequested || Input.GetMouseButtonDown(0);
            _attackClickRequested = false;
            if (!clickEdge) return;

            WeaponDef weapon = SelectedWeapon;
            bool weaponSelected = weapon != null;

            // Resolve the nearest enemy in this weapon's reach (null if none / no weapon).
            Health target = weaponSelected ? ResolveNearestTarget(weapon.Reach) : null;

            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);

            if (!ShouldAttackOnClick(weaponSelected, target != null,
                                     UiInputGate.CaptureWorldInput, overUI, rmbHeld))
                return;

            // Attack cooldown, scaled down by the weapon's attackSpeed (a faster weapon swings sooner).
            float cooldown = baseAttackCooldown / Mathf.Max(0.01f, weapon.AttackSpeed);
            if (Time.time - _lastAttackAt < Mathf.Max(0f, cooldown)) return;

            PerformAttack(weapon, target);
        }

        /// <summary>
        /// Perform ONE attack with <paramref name="weapon"/> against <paramref name="target"/> (public so
        /// PlayMode/EditMode drive it deterministically). Swings the PLACEHOLDER arm (existing chop Attack
        /// state, attackSpeed-scaled), deals weapon.Damage through the shared <see cref="Health.ApplyDamage"/>
        /// seam with the weapon's <see cref="DamageType"/> (so the target's resistance + tier modulate it —
        /// AC8), and applies the weapon's on-hit status (AC6). Records the observable outcome. A null target
        /// still SWINGS (a miss) but lands no damage. A null weapon is a no-op.
        /// </summary>
        public void PerformAttack(WeaponDef weapon, Health target)
        {
            if (weapon == null) return;
            _lastAttackAt = Time.time;
            SwingsFired++;
            LastWeaponId = weapon.Id;

            // SWING the arm — the PLACEHOLDER swing (existing chop Attack state) scaled by attackSpeed. Face
            // the target so the strike reads as hitting it. NO new Animator clip/state (AC5 scope question):
            // the per-weapon distinct swing (axe_chop vs spear_thrust) is deferred to the Sponsor's ruling.
            if (character != null)
            {
                if (target != null) character.FaceWorldTarget(target.transform.position);
                character.chopSpeed = Mathf.Clamp(weapon.AttackSpeed,
                    CastawayCharacter.ChopSpeedMin, CastawayCharacter.ChopSpeedMax);
                character.TriggerChop(); // placeholder swing — rides the existing Mixamo melee Attack state
            }

            if (target == null || target.IsDead) return;

            // DEAL DAMAGE through the shared AC1 seam — weapon.Damage × the target's resistance matchup (AC8a)
            // × the tier's damage-taken multiplier (AC8b), all inside Health.ApplyDamage. The damage COMES FROM
            // the weapon attributes (AC4), never a magic number here.
            float removed = target.ApplyDamage(weapon.Damage, weapon.DamageType);
            LastDamageDealt = removed;
            if (removed > 0f) HitsLanded++;

            // Apply the weapon's ON-HIT STATUS (AC6) — e.g. the axe's light Bleed. Skipped for None (spear).
            if (weapon.OnHitStatus.IsActive)
            {
                var sec = target.GetComponent<StatusEffectController>();
                if (sec != null) sec.Apply(weapon.OnHitStatus);
            }
        }

        // Resolve the nearest ALIVE enemy Health within `reach` of the player (planar XZ, height-robust —
        // the same metric ChopTree/CraftSpot use). Skips the player's OWN Health (an attack never hits self).
        // Uses FindObjectsOfType only on a click (NOT per-frame) — a cold path, no per-frame alloc (unity6 §5).
        private Health ResolveNearestTarget(float reach)
        {
            if (player == null) return null;
            Health self = player.GetComponentInChildren<Health>();
            Health best = null;
            float bestSq = reach * reach;
            Vector2 here = new Vector2(player.position.x, player.position.z);

            var all = FindObjectsOfType<Health>();
            for (int i = 0; i < all.Length; i++)
            {
                Health h = all[i];
                if (h == null || h == self || h.IsDead) continue;
                Vector3 p = h.transform.position;
                float dSq = (here - new Vector2(p.x, p.z)).sqrMagnitude;
                if (dSq <= bestSq) { bestSq = dSq; best = h; }
            }
            return best;
        }
    }
}
