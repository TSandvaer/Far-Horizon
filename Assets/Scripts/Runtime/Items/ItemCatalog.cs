using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The item lookup/export site (ticket 86caa4bya / item-model contract §1). Holds the canonical
    /// <see cref="ItemDef"/> set + <see cref="ById"/> / <see cref="All"/> lookup. Consumers reference the
    /// catalog, never a hard ItemDef field array — so the world-resource tickets do
    /// <c>AddItem(catalog.ById("wood"/"stone"/"berry"), amount)</c> (contract §9).
    ///
    /// The catalog is a ScriptableObject so it ships as `Assets/Data/Items/ItemCatalog.asset` (authored at
    /// bootstrap with the canonical defs — axe/wood/stone/berry/water). For pure-logic tests it can be built via
    /// <see cref="BuildDefaults"/> — no .asset round-trip needed to drive the model (AC8).
    /// </summary>
    [CreateAssetMenu(menuName = "Far Horizon/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        // === Canonical ids (contract §3) — the single registry; two tickets never mint two "wood". ===
        public const string AxeId = "axe";
        public const string WoodId = "wood";
        public const string StoneId = "stone";
        public const string BerryId = "berry";

        /// <summary>
        /// Canonical WATER id (pinned by 86caf7a6q AC5; the water def is MINTED here by 86caf7g6f). Sponsor's
        /// water-acquisition answer (2026-06-27): E at the pond loots ONE "water" unit into the belt (NO
        /// container/canteen); the drink is left-click (86caf7a30). The id is the SINGLE source so the loot side
        /// (E-at-pond, 86caf7a6q) and the consume side (left-click drink, 86caf7a30) bind to the SAME id, never
        /// a parallel one. The water ItemDef IS built into <see cref="BuildDefaults"/> as a belt-eligible
        /// <see cref="ItemKind.Consumable"/> (86caf7g6f resolved the model decision: a new third kind, NOT a
        /// per-asset bool — see <see cref="ItemDef.IsBeltEligible"/>). The model-decision that was deferred here
        /// is now made; the loot (86caf7a6q AC5) and consume (86caf7a30) follow-ups land their EFFECTS onto this
        /// existing def. Berries share the kind (also a belt-eligible Consumable).
        /// </summary>
        public const string WaterId = "water";

        [SerializeField,
         Tooltip("The canonical item defs (axe / wood / stone / berry). Authored at bootstrap; the world-" +
                 "resource tickets look these up by id, never mint their own.")]
        private List<ItemDef> _all = new List<ItemDef>();

        private Dictionary<string, ItemDef> _byId;

        /// <summary>All registered defs, in registration order.</summary>
        public IReadOnlyList<ItemDef> All => _all;

        /// <summary>Look up a def by its canonical id (null if not present). The downstream-pickup seam.</summary>
        public ItemDef ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>True if a def with this id is registered.</summary>
        public bool Has(string id) => ById(id) != null;

        private void EnsureIndex()
        {
            if (_byId != null && _byId.Count == _all.Count) return;
            _byId = new Dictionary<string, ItemDef>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && !string.IsNullOrEmpty(_all[i].Id))
                    _byId[_all[i].Id] = _all[i];
        }

        /// <summary>Replace the catalog's def set (bootstrap authoring + tests). Rebuilds the index.</summary>
        public void SetAll(IEnumerable<ItemDef> defs)
        {
            _all = new List<ItemDef>(defs);
            _byId = null;
            EnsureIndex();
        }

        /// <summary>
        /// Build the canonical defs IN CODE and populate the catalog (bootstrap + EditMode tests). axe = Tool;
        /// wood / stone = stackable Resources (inventory-only); berry / water = stackable belt-eligible
        /// CONSUMABLES (ticket 86caf7g6f — holdable so left-click can eat/drink them at 86caf7a30). Idempotent:
        /// clears + rebuilds. Icons are optional (the UI falls back to a letter-chip when null).
        /// </summary>
        public void BuildDefaults(Sprite axeIcon = null, Sprite woodIcon = null,
                                  Sprite stoneIcon = null, Sprite berryIcon = null,
                                  Sprite waterIcon = null)
        {
            var axe = CreateInstance<ItemDef>(); axe.name = "axe";
            axe.Init(AxeId, "Axe", ItemKind.Tool, axeIcon);

            // BUG 3 (#90): chopped wood shipped with a NULL icon → the slot rendered only a bare "W" letter-
            // chip (model + paint were correct — invDiag trace `0=woodx3` / `chip='W'`), which the Sponsor
            // did not read as "wood obtained". AC5 calls for a recognizable wood ICON. Until a 3D render-the-
            // prop IconBaker lands (Uma's direction), bake a small procedural wood-bundle sprite IN CODE so
            // obtained wood reads as a wood item. Reproducible-from-code (no hand-edited PNG that silently
            // reverts on re-import — unity-conventions.md §recolor-must-be-reproducible). A caller-supplied
            // icon (a future baked sprite) still wins.
            var wood = CreateInstance<ItemDef>(); wood.name = "wood";
            wood.Init(WoodId, "Wood", ItemKind.Resource, woodIcon ?? ItemIconGen.WoodBundle());

            var stone = CreateInstance<ItemDef>(); stone.name = "stone";
            stone.Init(StoneId, "Stone", ItemKind.Resource, stoneIcon ?? ItemIconGen.StonePile());

            // 86caf7g6f: berries flip Resource→Consumable so they're belt-eligible (holdable + selectable for
            // a future left-click eat — 86caf7a30). The berry-cluster icon is unchanged.
            var berry = CreateInstance<ItemDef>(); berry.name = "berry";
            berry.Init(BerryId, "Berries", ItemKind.Consumable, berryIcon ?? ItemIconGen.BerryCluster());

            // 86caf7g6f: mint the WATER def (id pinned by #147) as a belt-eligible Consumable so E-at-pond loot
            // (86caf7a6q AC5) has a belt item to land into + left-click can drink it (86caf7a30). A procedural
            // blue water-drop icon gives a recognizable read (the BUG 3 #90 lesson: a bare letter-chip read
            // poorly); a baked 3D-prop icon still wins when one lands (water-icon follow-up).
            var water = CreateInstance<ItemDef>(); water.name = "water";
            water.Init(WaterId, "Water", ItemKind.Consumable, waterIcon ?? ItemIconGen.WaterDrop());

            SetAll(new[] { axe, wood, stone, berry, water });
        }
    }
}
