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
    /// === The swing (AC5) — PER-CLASS Mixamo swing (86caffwv5 — RESOLVED) ===
    /// The Combat-POC placeholder (all weapons rode the single chop Attack state) is REPLACED: each weapon now
    /// plays its OWN Mixamo swing. <see cref="PerformAttack"/> maps the weapon's <see cref="WeaponDef.AnimationId"/>
    /// through <see cref="WeaponCatalog.WeaponClassForAnimationId"/> to a WeaponClass and fires
    /// <see cref="CastawayCharacter.TriggerAttack"/>, which sets the Animator's WeaponClass int + ChopSpeed then
    /// pulses the shared Chop trigger → the controller plays the per-class AttackX state (axe_chop→AttackAxe …
    /// sword_slash→AttackSword). This is the Sponsor-ruled animator-driven-Mixamo approach
    /// (<see cref="WeaponClassForSwing"/> is the pure, testable map). No new INPUT path — the guard truth-table +
    /// single-flight below are unchanged; only the swing FIRED is now per-class (the shared Animator → CastawayArmPose
    /// (order 50) → HeldAxeRig (order 100) chain is intact — the swing moves the arm, HeldAxeRig follows the hand).
    ///
    /// === Guards (the click must only swing in the game world) ===
    /// The pure <see cref="ShouldSwingOnClick"/>: a weapon must be selected + no modal panel open + not over the
    /// inventory/belt UI + RMB not held (a camera-orbit drag). A valid TARGET is NOT required — one click = one
    /// swing of the equipped weapon, TARGET OR NOT (the active-input AC; soak-2 fix 86caffwv5). A click at empty
    /// air WHIFFS: the swing plays and lands nothing; DAMAGE stays gated on a target actually being in reach.
    /// <see cref="RequestAttackClick"/> is the input-independent latch (the analog of ChopTree.RequestChopClick)
    /// so headless PlayMode + the shipped capture exercise the SAME attack path.
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

        [Header("Verb arbitration (round-4 86caffwv5 — verb-wins-over-whiff)")]
        [Tooltip("The chop verb. When an axe is selected AND a tree is in chop range, the CHOP owns the left-click " +
                 "and this attack must NOT also fire a whiff swing (the double-consumer regression). Queried via " +
                 "ChopTree.WouldClaimClick(). Wired at bootstrap; Awake scene-search fallback. Null → the chop " +
                 "never claims (a bare rig / no chop in the scene).")]
        public ChopTree chopTree;

        [Tooltip("The boulder-mine verb — same arbitration: a pickaxe selected + a boulder in range means MINING " +
                 "owns the click (no whiff). Queried via MineBoulder.WouldClaimClick(). Bootstrap-wired; Awake fallback.")]
        public MineBoulder mineBoulder;

        [Tooltip("The ore-mine verb — same arbitration (stone/iron pickaxe + an ore node in range owns the click). " +
                 "Queried via MineOre.WouldClaimClick(). Bootstrap-wired; Awake fallback.")]
        public MineOre mineOre;

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
            // Verb arbitration refs (round-4) — the shipped scene has one of each; the Awake scene-search is the
            // reliable resolve (all verb components exist by Awake, regardless of bootstrap-wiring order). Null-safe.
            if (chopTree == null) chopTree = FindObjectOfType<ChopTree>();
            if (mineBoulder == null) mineBoulder = FindObjectOfType<MineBoulder>();
            if (mineOre == null) mineOre = FindObjectOfType<MineOre>();
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
        /// PURE swing-on-a-left-click decision (the unit-testable guard truth-table). One click = one swing of the
        /// equipped weapon, TARGET OR NOT (soak-2 fix 86caffwv5 — a click at empty air WHIFFS). All of: a weapon is
        /// selected, NO verb claimed the click, no modal panel owns the screen, the click is NOT over the
        /// inventory/belt UI, the RMB is NOT held (no orbit drag). NO target-in-reach requirement — DAMAGE (not the
        /// swing) is what a target gates.
        ///
        /// === VERB-WINS-OVER-WHIFF arbitration (round-4 regression fix, 86caffwv5) ===
        /// <paramref name="verbClaimedClick"/> is true when a VERB consumer (chop / boulder-mine / ore-mine) OWNS
        /// this click — its matching tool is selected AND a valid target is in that verb's range (computed by the
        /// caller from <see cref="ChopTree.WouldClaimClick"/> / <see cref="MineBoulder.WouldClaimClick"/> /
        /// <see cref="MineOre.WouldClaimClick"/>). When a verb claims the click the attack does NOT swing — the verb
        /// (chop/mine) owns the turn + its own swing; a spurious combat whiff on top is the soak-4 double-consumer
        /// regression this suppresses. When NOTHING claims the click, the attack swings (enemy in reach → lands
        /// damage; empty air → whiffs — AC3, the soak-2 behavior is preserved). AMBIGUITY (an enemy AND a verb
        /// target both in reach): the VERB wins (conservative / pre-round-2 behavior) — see the PR body.
        /// Static + dependency-free so the EditMode guard asserts the whole table with no scene/Input/UI rig.
        /// </summary>
        public static bool ShouldSwingOnClick(bool weaponSelected, bool verbClaimedClick,
                                              bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)
            => weaponSelected && !verbClaimedClick && !uiPanelOpen && !pointerOverUI && !rmbHeld;

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

            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);

            // VERB-WINS-OVER-WHIFF (round-4): if a verb (chop / boulder-mine / ore-mine) owns this click — its tool
            // is selected AND its target is in range — the verb swings + turns + damages; the attack must NOT also
            // fire a whiff on top (the soak-4 "cannot chop, only whiffs" double-consumer regression). Only queried
            // when a weapon is selected (else no swing regardless); a stateless recompute per click (a cold path).
            bool verbClaimed = weaponSelected && VerbClaimsClick();

            // One click = one swing of the equipped weapon, TARGET OR NOT (whiff-allowed — soak-2 fix), UNLESS a
            // verb claimed the click. The gate does NOT require an enemy target; a target only gates the DAMAGE below.
            if (!ShouldSwingOnClick(weaponSelected, verbClaimed, UiInputGate.CaptureWorldInput, overUI, rmbHeld))
                return;

            // Attack cooldown, scaled down by the weapon's attackSpeed (a faster weapon swings sooner).
            float cooldown = baseAttackCooldown / Mathf.Max(0.01f, weapon.AttackSpeed);
            if (Time.time - _lastAttackAt < Mathf.Max(0f, cooldown)) return;

            // Resolve the nearest enemy in this weapon's reach for the DAMAGE (null → a whiff: the swing plays,
            // lands nothing). Only computed once the swing is going to fire (a cold path, no per-frame alloc).
            Health target = ResolveNearestTarget(weapon.Reach);
            PerformAttack(weapon, target);
        }

        /// <summary>
        /// Perform ONE attack with <paramref name="weapon"/> against <paramref name="target"/> (public so
        /// PlayMode/EditMode drive it deterministically). Swings the weapon's PER-CLASS arm swing
        /// (attackSpeed-scaled), deals weapon.Damage through the shared <see cref="Health.ApplyDamage"/>
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

            // SWING the arm — the PER-CLASS swing (86caffwv5). Map the weapon's AnimationId → its WeaponClass and
            // fire that class's Mixamo swing state (axe_chop→AttackAxe, spear_thrust→AttackSpear, …). The swing
            // playback speed is passed DIRECTLY to TriggerAttack as the weapon's attackSpeed — we do NOT mutate
            // character.chopSpeed here (that is the GLOBAL tool-use-speed dial the tree-chop/mine verbs read;
            // clobbering it on a combat swing would leak this weapon's cadence into those verbs — soak-2 cleanup).
            // Face the target so the strike reads as hitting it. An UNMAPPED id defensively falls back to axe.
            if (character != null)
            {
                if (target != null) character.FaceWorldTarget(target.transform.position);
                int weaponClass = WeaponClassForSwing(weapon);
                character.TriggerAttack(weaponClass, weapon.AttackSpeed);
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

        /// <summary>The WeaponClass swing for a weapon (86caffwv5) — its AnimationId mapped through the single
        /// selection seam (<see cref="WeaponCatalog.WeaponClassForAnimationId"/>). An unmapped id (-1) falls back to
        /// the axe class (never a silent wrong swing). Pure + static so an EditMode test asserts the whole mapping.</summary>
        public static int WeaponClassForSwing(WeaponDef weapon)
        {
            if (weapon == null) return CastawayCharacter.WeaponClassAxe;
            int cls = WeaponCatalog.WeaponClassForAnimationId(weapon.AnimationId);
            return cls >= 0 ? cls : CastawayCharacter.WeaponClassAxe;
        }

        /// <summary>VERB-WINS-OVER-WHIFF (round-4 86caffwv5) — true when a verb consumer (chop / boulder-mine /
        /// ore-mine) OWNS the current left-click: its matching tool is selected AND a valid target is in that verb's
        /// range. A STATELESS query (each verb recomputes its own range), so it does NOT depend on Update execution
        /// order between MeleeAttack and the verbs. When true, MeleeAttack suppresses its whiff swing (the verb owns
        /// the swing + turn + damage). Null verb refs never claim (a bare rig). Called only on a click edge with a
        /// weapon selected — a cold path (no per-frame alloc; unity6 §5).</summary>
        private bool VerbClaimsClick()
            => AnyVerbClaims(VerbClaims(chopTree),
                             VerbClaims(mineBoulder),
                             VerbClaims(mineOre));

        /// <summary>PURE verb-suppression predicate (86cav8xu8 — extracted so the ClickGateDiagnostic's ClassifyClick
        /// precedence can be CROSS-CHECKED against the real MeleeAttack rule instead of asserting against itself). A
        /// verb owns the click iff ANY verb claims it — chop → boulder → ore (the order is the deterministic tie-break
        /// the live query short-circuits in; in play only one tool is ever selected, so at most one claims). Static +
        /// dependency-free.</summary>
        public static bool AnyVerbClaims(bool chopClaims, bool boulderClaims, bool oreClaims)
            => chopClaims || boulderClaims || oreClaims;

        // A verb claims the click ONLY when its component is ALIVE (active GameObject + enabled) AND it WouldClaim.
        // 86cav8xu8 (Devon r4 NIT 4) — the disabled-verb DEAD-CLICK guard: a present-but-DISABLED verb's Update never
        // runs (so it can never actually chop/mine), yet its pure WouldClaimClick() would still return true → the
        // attack would suppress its whiff for a verb that never fires → a dead click that neither chops nor whiffs.
        // Gating on isActiveAndEnabled means an inert verb never suppresses the whiff. (~nil risk in the shipped
        // scene — all verbs are enabled — but it closes the exact silent-fail class this round guards.)
        private static bool VerbClaims(ChopTree v)      => v != null && v.isActiveAndEnabled && v.WouldClaimClick();
        private static bool VerbClaims(MineBoulder v)   => v != null && v.isActiveAndEnabled && v.WouldClaimClick();
        private static bool VerbClaims(MineOre v)       => v != null && v.isActiveAndEnabled && v.WouldClaimClick();

#if UNITY_INCLUDE_TESTS
        /// <summary>86cav8xu8 — TEST-ONLY (STRIPPED via UNITY_INCLUDE_TESTS): expose the private verb-claim
        /// arbitration so an EditMode test can pin the disabled-verb dead-click guard (a disabled verb that WouldClaim
        /// must NOT be counted). No InternalsVisibleTo → the codebase "public for tests" seam convention.</summary>
        public bool VerbClaimsClickForTest() => VerbClaimsClick();
#endif

        /// <summary>86caffwv5 diagnostic (PR #327 — the ClickGateDiagnostic instrument, read-only, NOT a fix): the
        /// melee gate ground truth — the SELECTED weapon (its id; null = the selected belt item is not a weapon /
        /// nothing selected) + whether an enemy is in reach (a whiff vs a landed swing). Uses the SAME SelectedWeapon
        /// + ResolveNearestTarget the live swing reads. A cold-path read (called only on a click by the diagnostic).</summary>
        public MeleeGateDiag ClickGateDiag()
        {
            WeaponDef w = SelectedWeapon;
            var d = new MeleeGateDiag
            {
                WeaponSelected = w != null,
                WeaponId = w != null ? w.Id : null,
                Reach = w != null ? w.Reach : 0f,
            };
            if (w != null) d.HasTarget = ResolveNearestTarget(w.Reach) != null;
            return d;
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
