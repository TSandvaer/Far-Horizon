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

        // === WOOD-tier weapon ids (ticket 86camz9uz / crafting-redesign ① — §6b). Match the ItemCatalog
        // wood ids so the belt-item → WeaponDef lookup binds (the axe/spear precedent). The wood tier is the
        // crudest rung; combat WIRING for wood tools (chop/mine/attack verbs) is ②+/follow-up — these defs
        // exist so the id resolves in BOTH catalogs (① success test) + future held-tool/verb resolution. ===
        public const string AxeWoodId = "axe_wood";
        public const string PickaxeWoodId = "pickaxe_wood";
        public const string SpearWoodId = "spear_wood";
        public const string DaggerWoodId = "dagger_wood";
        public const string SwordWoodId = "sword_wood";

        // === STONE-tier NEW weapon ids (ticket 86camz9v7 / crafting-redesign ② — §6b). Only dagger + sword are
        // new stone cells (axe/spear/pickaxe_stone already ship). Match the ItemCatalog stone ids so the
        // belt-item → WeaponDef lookup binds. Combat WIRING for these is OOS here (combat balance is OOS on ②);
        // the defs exist so the ids resolve in BOTH catalogs (the ② success test mirrors ①'s). ===
        public const string DaggerStoneId = "dagger_stone";
        public const string SwordStoneId = "sword_stone";

        // === IRON-tier NEW weapon ids (ticket 86camz9vh / crafting-redesign ③ — §6b). pickaxe_iron already
        // ships (I-0); the ONLY new IRON cells are axe/spear/dagger/sword. Match the ItemCatalog iron ids so
        // the belt-item → WeaponDef lookup binds (the axe/spear precedent). Combat WIRING for these is OOS on
        // ③ (peaceful; combat balance stays OOS); the defs exist so the ids resolve in BOTH catalogs (the ③
        // "ids resolve in both catalogs" success test mirrors ①/②'s) + future held-tool/verb resolution. ===
        public const string AxeIronId = "axe_iron";
        public const string SpearIronId = "spear_iron";
        public const string DaggerIronId = "dagger_iron";
        public const string SwordIronId = "sword_iron";

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
        // WOOD tier (ticket 86camz9uz ①) — the crudest rung, ~0.7× the stone-tier where a stone baseline
        // exists (§7-B). Reach is a physical length (unscaled by tier); wood weapons carry NO on-hit status
        // (bleed is the forged stone-axe's edge). dagger/sword have no stone WeaponDef yet, so their wood stats
        // are standalone predictions. ALL 🎚️ default — Sponsor-soak tunes (combat with wood tools is ②+, so
        // these do not yet drive gameplay). Opaque animationIds name a future swing (unwired in ①).
        public const float AxeWoodDamage = 10f;      // ~0.7×14
        public const float AxeWoodReach = 2.0f;
        public const float AxeWoodAttackSpeed = 1.3f;
        public const float PickaxeWoodDamage = 6f;   // ~0.7×8
        public const float PickaxeWoodReach = 2.0f;
        public const float PickaxeWoodAttackSpeed = 0.95f;
        public const float SpearWoodDamage = 6f;     // ~0.7×9
        public const float SpearWoodReach = 3.6f;
        public const float SpearWoodAttackSpeed = 0.85f;
        public const float DaggerWoodDamage = 6f;    // fast + short (no stone baseline)
        public const float DaggerWoodReach = 1.6f;
        public const float DaggerWoodAttackSpeed = 1.8f;
        public const float SwordWoodDamage = 12f;    // reach + heft (no stone baseline)
        public const float SwordWoodReach = 2.4f;
        public const float SwordWoodAttackSpeed = 1.15f;
        // STONE tier (ticket 86camz9v7 ②) — the STONE dagger/sword are the baseline the wood tier was ~0.7× of;
        // seed them at ~wood/0.7 (a rung above wood). Combat with these is OOS on ② (combat balance OOS), so these
        // do not yet drive gameplay — ALL 🎚️ default — Sponsor-soak tunes. No on-hit status (bleed is the forged
        // stone-AXE's edge, per the existing axe def). Opaque animationIds name a future swing (unwired).
        public const float DaggerStoneDamage = 8f;    // ~dagger_wood/0.7
        public const float DaggerStoneReach = 1.6f;
        public const float DaggerStoneAttackSpeed = 1.8f;
        public const float SwordStoneDamage = 16f;    // ~sword_wood/0.7
        public const float SwordStoneReach = 2.4f;
        public const float SwordStoneAttackSpeed = 1.15f;
        // IRON tier (ticket 86camz9vh ③) — the forged upgrade, ~1.3× the stone-tier damage (§7-B). Reach is a
        // physical length (unscaled by tier); attackSpeed is marginally faster (a keener edge). The iron AXE
        // carries the forged bleed (a stronger edge than the stone axe — the only on-hit status, mirroring the
        // stone axe's bleed). Combat with these is OOS on ③ (peaceful), so they do not yet drive gameplay —
        // ALL 🎚️ default — Sponsor-soak tunes. Stone baselines: axe 14 / spear 9 / dagger_stone 8 / sword_stone 16.
        public const float AxeIronDamage = 18f;       // ~1.3×14
        public const float AxeIronReach = 2.0f;
        public const float AxeIronAttackSpeed = 1.5f;
        public const float AxeIronBleedDps = 3f;      // forged edge — stronger than the stone axe's 2 dps
        public const float AxeIronBleedDuration = 3f;
        public const float SpearIronDamage = 12f;     // ~1.3×9
        public const float SpearIronReach = 3.6f;
        public const float SpearIronAttackSpeed = 1.0f;
        public const float DaggerIronDamage = 10f;    // ~1.3×8
        public const float DaggerIronReach = 1.6f;
        public const float DaggerIronAttackSpeed = 1.9f;
        public const float SwordIronDamage = 21f;     // ~1.3×16
        public const float SwordIronReach = 2.4f;
        public const float SwordIronAttackSpeed = 1.25f;

        // === Swing ANIMATION ids (86caffwv5) — the opaque WeaponDef.AnimationId values, ONE per weapon class
        // (shared across wood/stone/iron — the material tier does NOT change the motion). The single source the
        // BuildDefaults defs + the AnimationId→WeaponClass map read (no magic strings scattered at call sites). ===
        public const string AnimIdAxeChop = "axe_chop";
        public const string AnimIdPickaxeMine = "pickaxe_mine";
        public const string AnimIdDaggerStab = "dagger_stab";
        public const string AnimIdSpearThrust = "spear_thrust";
        public const string AnimIdSwordSlash = "sword_slash";

        /// <summary>
        /// The SINGLE swing-selection seam (86caffwv5 §4.2) — map a <see cref="WeaponDef.AnimationId"/> to the
        /// Animator <c>WeaponClass</c> int the shared Chop trigger reads (axe_chop→0 … sword_slash→4). MeleeAttack
        /// calls this to pick the per-class swing on a left-click; the WeaponSetTests assert EVERY def's AnimationId
        /// maps to a defined class (no orphan id → no weapon with an unmapped swing). Unknown id → -1 (caller treats
        /// as "no per-class swing" — defensively falls back to the axe class rather than a silent wrong swing).
        /// </summary>
        public static int WeaponClassForAnimationId(string animationId)
        {
            switch (animationId)
            {
                case AnimIdAxeChop:      return CastawayCharacter.WeaponClassAxe;
                case AnimIdPickaxeMine:  return CastawayCharacter.WeaponClassPickaxe;
                case AnimIdDaggerStab:   return CastawayCharacter.WeaponClassDagger;
                case AnimIdSpearThrust:  return CastawayCharacter.WeaponClassSpear;
                case AnimIdSwordSlash:   return CastawayCharacter.WeaponClassSword;
                default:                 return -1; // unmapped — caller decides the fallback
            }
        }

        /// <summary>
        /// The WeaponClass of an ITEM / belt id (86cav8xu8) — resolves the id to its <see cref="WeaponDef"/> in the
        /// canonical <see cref="BuildDefaults"/> set, then maps that def's <see cref="WeaponDef.AnimationId"/>
        /// through <see cref="WeaponClassForAnimationId"/>. Returns <c>-1</c> when the id is not a catalog weapon
        /// (a non-weapon belt item — wood / berry / stone / water — or an unknown id). Callers compare against the
        /// SPECIFIC class they gate on (axe / pickaxe), so an unmapped id is correctly NOT that class — there is NO
        /// axe-fallback here (that fallback is for the SWING in <see cref="MeleeAttack.WeaponClassForSwing"/>, never a
        /// tool-select gate, where it would silently mis-classify wood/berry as an axe).
        ///
        /// WHY (the soak-4 fall-through fix): the chop/mine tool-select gates previously hand-ENUMERATED the known
        /// tier ids (axe / axe_wood / axe_iron). A future 4th tier added to <see cref="BuildDefaults"/> would fall
        /// through that list (the exact soak-4 root pattern — a wood axe fell through the enumerated chop gate). This
        /// map is DERIVED ONCE from BuildDefaults, so a new tier that carries an existing class's AnimationId (e.g.
        /// axe_chop) joins the map — and every gate that reads it — BY CONSTRUCTION, with no gate edit.
        /// </summary>
        public static int WeaponClassForItemId(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            return ItemIdToClass.Value.TryGetValue(id, out int cls) ? cls : -1;
        }

        // The canonical item-id → WeaponClass map, derived ONCE from BuildDefaults (86cav8xu8 — NOT a
        // hand-enumerated list: a new tier in BuildDefaults joins this map automatically). Built LAZILY on first use,
        // NOT at type-init: Unity forbids ScriptableObject.CreateInstance from a static/instance field initializer of
        // a ScriptableObject type ("call it in OnEnable instead"), and BuildDefaults creates WeaponDef SOs — so the
        // map build must run from a normal call (a gate check), which Lazy defers it to. `static readonly Lazy`
        // (immutable REFERENCE) is excluded from StaticStateResetTests's mutable-static audit; the map content is
        // never mutated after construction, and it derives from immutable const attributes so a stale cache across
        // editor play-entries is byte-identical to a fresh one (the reason readonly/const are exempt).
        private static readonly System.Lazy<Dictionary<string, int>> ItemIdToClass =
            new System.Lazy<Dictionary<string, int>>(BuildItemIdToClassMap);

        private static Dictionary<string, int> BuildItemIdToClassMap()
        {
            var map = new Dictionary<string, int>();
            var defaults = CreateInstance<WeaponCatalog>();
            defaults.BuildDefaults();
            var all = defaults.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    map[def.Id] = WeaponClassForAnimationId(def.AnimationId);
            }
            // The throwaway defaults catalog + its WeaponDef ScriptableObjects are needed only to READ the
            // id→animId→class relationship; free them so the one-time static build leaks nothing.
            for (int i = 0; i < all.Count; i++) DestroyTemp(all[i]);
            DestroyTemp(defaults);
            return map;
        }

        private static void DestroyTemp(Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(o); return; }
#endif
            Destroy(o);
        }

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

            // WOOD tier (ticket 86camz9uz ①) — mint the 5 wood weapon defs so the wood ids resolve in the
            // weapon lane too (① success test: "wood ids resolve in both catalogs"). No on-hit status (wood is
            // cruder than forged stone). Ids match the ItemCatalog wood ids.
            var axeWood = WeaponDef.Create(AxeWoodId, "Wood Axe",
                AxeWoodDamage, AxeWoodReach, AxeWoodAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "axe_chop");
            var pickaxeWood = WeaponDef.Create(PickaxeWoodId, "Wood Pickaxe",
                PickaxeWoodDamage, PickaxeWoodReach, PickaxeWoodAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "pickaxe_mine");
            var spearWood = WeaponDef.Create(SpearWoodId, "Wood Spear",
                SpearWoodDamage, SpearWoodReach, SpearWoodAttackSpeed, DamageType.Pierce,
                StatusEffectSpec.None, "spear_thrust");
            var daggerWood = WeaponDef.Create(DaggerWoodId, "Wood Dagger",
                DaggerWoodDamage, DaggerWoodReach, DaggerWoodAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "dagger_stab");
            var swordWood = WeaponDef.Create(SwordWoodId, "Wood Sword",
                SwordWoodDamage, SwordWoodReach, SwordWoodAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "sword_slash");

            // STONE tier (ticket 86camz9v7 ②) — mint the 2 new stone weapon defs so the stone dagger/sword ids
            // resolve in the weapon lane too (the ② "ids resolve in both catalogs" success test). Ids match the
            // ItemCatalog stone ids. No on-hit status. Combat is OOS on ②; these exist for id resolution + future.
            var daggerStone = WeaponDef.Create(DaggerStoneId, "Stone Dagger",
                DaggerStoneDamage, DaggerStoneReach, DaggerStoneAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "dagger_stab");
            var swordStone = WeaponDef.Create(SwordStoneId, "Stone Sword",
                SwordStoneDamage, SwordStoneReach, SwordStoneAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "sword_slash");

            // IRON tier (ticket 86camz9vh ③) — mint the 4 new iron weapon defs so the iron axe/spear/dagger/
            // sword ids resolve in the weapon lane too (the ③ "ids resolve in both catalogs" success test).
            // Ids match the ItemCatalog iron ids. The iron AXE carries the forged bleed (the only on-hit
            // status). Combat is OOS on ③; these exist for id resolution + the wielded upgrade + future.
            var axeIron = WeaponDef.Create(AxeIronId, "Iron Axe",
                AxeIronDamage, AxeIronReach, AxeIronAttackSpeed, DamageType.Slash,
                StatusEffectSpec.MakeBleed(AxeIronBleedDps, AxeIronBleedDuration), "axe_chop");
            var spearIron = WeaponDef.Create(SpearIronId, "Iron Spear",
                SpearIronDamage, SpearIronReach, SpearIronAttackSpeed, DamageType.Pierce,
                StatusEffectSpec.None, "spear_thrust");
            var daggerIron = WeaponDef.Create(DaggerIronId, "Iron Dagger",
                DaggerIronDamage, DaggerIronReach, DaggerIronAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "dagger_stab");
            var swordIron = WeaponDef.Create(SwordIronId, "Iron Sword",
                SwordIronDamage, SwordIronReach, SwordIronAttackSpeed, DamageType.Slash,
                StatusEffectSpec.None, "sword_slash");

            SetAll(new[] { axe, spear, pickaxeStone, pickaxeIron,
                           axeWood, pickaxeWood, spearWood, daggerWood, swordWood,
                           daggerStone, swordStone,
                           axeIron, spearIron, daggerIron, swordIron });
        }
    }
}
