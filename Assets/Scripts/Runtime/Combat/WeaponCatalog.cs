using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The weapon lookup (Combat POC 86cah7xxp, AC4). Holds the canonical <see cref="WeaponDef"/> set +
    /// id lookup — the mirror of <see cref="FarHorizon.ItemCatalog"/> for combat. The POC ships TWO
    /// CONTRASTING weapons (AC4), both craftable (acquisition = CRAFT at a station; find-in-world is OOS):
    ///   • AXE   — Slash, MEDIUM reach, hits harder up close (higher damage, faster swing), light Bleed.
    ///   • SPEAR — Pierce, LONG reach, lower per-hit damage, slower swing, no on-hit status.
    /// The contrast is real in the numbers so AC10 can assert axe vs spear produce DIFFERENT outcomes
    /// through the damage seam (not the same magic number).
    ///
    /// A ScriptableObject so it ships as an asset; built in code via <see cref="BuildDefaults"/> for
    /// bootstrap + tests (no .asset round-trip). Ids match the inventory <see cref="FarHorizon.ItemCatalog"/>
    /// ids ("axe" / "spear") so the held-weapon selection resolves the WeaponDef by the selected belt item.
    /// </summary>
    [CreateAssetMenu(menuName = "Far Horizon/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        /// <summary>Canonical AXE id (matches ItemCatalog.AxeId "axe").</summary>
        public const string AxeId = "axe";
        /// <summary>Canonical SPEAR id (matches the spear ItemDef id "spear").</summary>
        public const string SpearId = "spear";
        /// <summary>Stone pickaxe id (ticket 86cakkmgw / I-0 — §1; matches ItemCatalog.PickaxeStoneId
        /// "pickaxe_stone"). A WeaponDef so the held-tool chain + mine verb resolve the pickaxe when it is the
        /// selected belt item (the axe/spear precedent).</summary>
        public const string PickaxeStoneId = "pickaxe_stone";
        /// <summary>Iron pickaxe id (matches ItemCatalog.PickaxeIronId "pickaxe_iron").</summary>
        public const string PickaxeIronId = "pickaxe_iron";

        // === Named default attributes (the single source AC4/AC10 read; NOT magic literals) ===
        // AXE — Slash, medium reach, hits harder up close, faster cadence, a light bleed on hit.
        public const float AxeDamage = 14f;
        public const float AxeReach = 2.0f;
        public const float AxeAttackSpeed = 1.4f;
        public const float AxeBleedDps = 2f;
        public const float AxeBleedDuration = 3f;
        // SPEAR — Pierce, LONG reach, lower per-hit damage, slower cadence, no on-hit status.
        public const float SpearDamage = 9f;
        public const float SpearReach = 3.6f;
        public const float SpearAttackSpeed = 0.9f;
        // PICKAXE (ticket 86cakkmgw / I-0) — the 5th tool type, both tiers. A PEACEFUL mining tool (no combat
        // dep, Sponsor Q4 locked): the WeaponDef exists so the held-tool chain + the I-2 mine swing resolve it,
        // but its damage is NOT a combat number — the mining verb reads its own strikes-to-break. Modest Slash
        // stats; iron is the upgrade (marginally better). default X — Sponsor-soak tunes. No on-hit status.
        public const float PickaxeStoneDamage = 8f;
        public const float PickaxeStoneReach = 2.0f;
        public const float PickaxeStoneAttackSpeed = 1.0f;
        public const float PickaxeIronDamage = 12f;
        public const float PickaxeIronReach = 2.1f;
        public const float PickaxeIronAttackSpeed = 1.2f;

        [SerializeField] private List<WeaponDef> _all = new List<WeaponDef>();
        private Dictionary<string, WeaponDef> _byId;

        /// <summary>All registered weapon defs.</summary>
        public IReadOnlyList<WeaponDef> All => _all;

        /// <summary>Look up a weapon def by id (null if not present). The selected-belt-item → weapon seam.</summary>
        public WeaponDef ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        private void EnsureIndex()
        {
            if (_byId != null && _byId.Count == _all.Count) return;
            _byId = new Dictionary<string, WeaponDef>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && !string.IsNullOrEmpty(_all[i].Id))
                    _byId[_all[i].Id] = _all[i];
        }

        /// <summary>Replace the def set (bootstrap authoring + tests). Rebuilds the index.</summary>
        public void SetAll(IEnumerable<WeaponDef> defs)
        {
            _all = new List<WeaponDef>(defs);
            _byId = null;
            EnsureIndex();
        }

        /// <summary>
        /// Build the two canonical POC weapons in code (bootstrap + EditMode tests) — the CONTRASTING axe +
        /// spear (AC4). The attributes come from the named consts above so the tests + the bootstrap read
        /// ONE source. Idempotent: clears + rebuilds.
        /// </summary>
        public void BuildDefaults()
        {
            var axe = WeaponDef.Create(AxeId, "Axe",
                AxeDamage, AxeReach, AxeAttackSpeed, DamageType.Slash,
                StatusEffectSpec.MakeBleed(AxeBleedDps, AxeBleedDuration), "axe_chop");

            var spear = WeaponDef.Create(SpearId, "Spear",
                SpearDamage, SpearReach, SpearAttackSpeed, DamageType.Pierce,
                StatusEffectSpec.None, "spear_thrust");

            // Pickaxes (ticket 86cakkmgw / I-0) — the 5th tool type, both tiers. Peaceful mining tools; the
            // WeaponDef exists for held-tool resolution + the mine swing ("pickaxe_mine" swing ref). No on-hit
            // status. Ids match the ItemCatalog pickaxe ids so the belt-item → WeaponDef lookup binds.
            var pickaxeStone = WeaponDef.Create(PickaxeStoneId, "Stone Pickaxe",
                PickaxeStoneDamage, PickaxeStoneReach, PickaxeStoneAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "pickaxe_mine");

            var pickaxeIron = WeaponDef.Create(PickaxeIronId, "Iron Pickaxe",
                PickaxeIronDamage, PickaxeIronReach, PickaxeIronAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "pickaxe_mine");

            SetAll(new[] { axe, spear, pickaxeStone, pickaxeIron });
        }
    }
}
