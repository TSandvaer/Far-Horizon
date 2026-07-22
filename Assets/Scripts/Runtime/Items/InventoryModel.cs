using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The slot/stack inventory MODEL (ticket 86caa4bya — implements Drew's item-model contract §4
    /// VERBATIM). Owns the inventory grid + belt hotbar slot arrays, the selected-belt index, and the
    /// FOUR write paths every consumer calls. Pure C# (no UnityEngine.Object, no MonoBehaviour lifecycle,
    /// no statics) so the whole contract — add/stack/spill, move/merge with the tool-vs-resource gate,
    /// belt-select, consume — is fully unit-testable in EditMode without a scene (AC8).
    ///
    /// The thin <see cref="Inventory"/> MonoBehaviour is a FAÇADE over this model (contract §7): it keeps
    /// exposing the WHOLE legacy ledger surface (HasAxe / WoodCount / Changed + CraftAxe / AddWood /
    /// SpendWood) so every existing caller (SurvivalHud / ChopTree / StumpAxe / HeldAxe /
    /// CastawayFingerCurl / CampfirePlacement / CraftSpot + the *VerifyCapture probes + ~14 tests) stays
    /// green. The resource tickets call <see cref="AddItem"/> directly against the catalog's ItemDef.
    ///
    /// NO MUTABLE STATICS (instance state only) — the Configurable-Enter-Play-Mode static-reset audit
    /// (StaticStateResetTests) needs no [RuntimeInitializeOnLoadMethod] reset for this class
    /// (unity-conventions.md §Configurable Enter Play Mode — the rule applies only if a static is added).
    /// </summary>
    public sealed class InventoryModel
    {
        /// <summary>Default inventory grid slot count (AC1 — adjustable via the future `inventory slots` setting).</summary>
        public const int DefaultInventorySlots = 20;

        /// <summary>Default belt hotbar slot count (AC2 — adjustable via the future `belt slots` setting).</summary>
        public const int DefaultBeltSlots = 5;

        private readonly ItemStack[] _inventory;
        private readonly ItemStack[] _belt;
        private int _selectedBelt;

        /// <summary>The inventory grid (count = the `inventory slots` setting; default 20).</summary>
        public IReadOnlyList<ItemStack> InventorySlots => _inventory;

        /// <summary>The belt hotbar (count = the `belt slots` setting; default 5).</summary>
        public IReadOnlyList<ItemStack> BeltSlots => _belt;

        /// <summary>0-based selected belt slot. Drives the held item (contract §5 / AC4).</summary>
        public int SelectedBeltIndex => _selectedBelt;

        /// <summary>Fires on ANY slot/selection change. UI + HUD subscribe, never poll (same pattern as
        /// the legacy ledger's Changed / WarmthNeed.Changed).</summary>
        public event Action Changed;

        public InventoryModel(int inventorySlots = DefaultInventorySlots, int beltSlots = DefaultBeltSlots)
        {
            _inventory = new ItemStack[Mathf.Max(1, inventorySlots)];
            _belt = new ItemStack[Mathf.Max(1, beltSlots)];
            for (int i = 0; i < _inventory.Length; i++) _inventory[i] = ItemStack.Empty;
            for (int i = 0; i < _belt.Length; i++) _belt[i] = ItemStack.Empty;
            _selectedBelt = 0;
        }

        // ============================================================================================
        // WRITE PATHS — the ONLY mutators (contract §4). Each fires Changed exactly once on a real change.
        // ============================================================================================

        /// <summary>
        /// Add <paramref name="amount"/> of <paramref name="def"/> to the inventory: top up existing
        /// stacks of the same item first (resources only), then fill free slots. THE seam every
        /// world-resource pickup calls (chop/stick→wood, stone→stone, berry→berry). Respects MaxStack
        /// (tools = 1/slot, resources to the cap). Returns the amount that did NOT fit (0 = all fit).
        /// Fires Changed if anything landed.
        /// </summary>
        public int AddItem(ItemDef def, int amount)
        {
            if (def == null || amount <= 0) return Mathf.Max(0, amount);

            int remaining = amount;
            bool changed = false;
            int cap = def.MaxStack;

            // Pass 1 — top up existing stacks of the same item (resources only; tools cap at 1 so a
            // second axe never merges into the first — contract §4 / AC7 "tools don't stack").
            if (ItemDef.IsStackable(def))
                remaining = TopUpExisting(def, remaining, cap, ref changed);

            // Pass 2 — fill free slots (inventory first, then belt only if the item is belt-eligible).
            remaining = FillFreeSlots(def, remaining, cap, ref changed);

            if (changed) Changed?.Invoke();
            return remaining;
        }

        // Top up partial stacks of the same item across inventory then belt. Resources only.
        private int TopUpExisting(ItemDef def, int remaining, int cap, ref bool changed)
        {
            remaining = TopUpArray(_inventory, def, remaining, cap, ref changed);
            remaining = TopUpArray(_belt, def, remaining, cap, ref changed);
            return remaining;
        }

        private static int TopUpArray(ItemStack[] arr, ItemDef def, int remaining, int cap, ref bool changed)
        {
            for (int i = 0; i < arr.Length && remaining > 0; i++)
            {
                if (arr[i].Def != def) continue;
                int room = cap - arr[i].Count;
                if (room <= 0) continue;
                int add = Mathf.Min(room, remaining);
                arr[i].Count += add;
                remaining -= add;
                changed = true;
            }
            return remaining;
        }

        // Fill empty slots: inventory first (everything goes in the pack), then the belt ONLY for
        // belt-eligible items — i.e. TOOLS or CONSUMABLES (ItemDef.IsBeltEligible; 86caf7g6f flipped
        // berries/water to belt-eligible Consumables, so a consumable CAN spill onto the belt). A pure
        // RESOURCE (wood/stone) never auto-lands on the belt (contract §2/§4).
        private int FillFreeSlots(ItemDef def, int remaining, int cap, ref bool changed)
        {
            remaining = FillArray(_inventory, def, remaining, cap, ref changed);
            if (remaining > 0 && ItemDef.IsBeltEligible(def))
                remaining = FillArray(_belt, def, remaining, cap, ref changed);
            return remaining;
        }

        private static int FillArray(ItemStack[] arr, ItemDef def, int remaining, int cap, ref bool changed)
        {
            for (int i = 0; i < arr.Length && remaining > 0; i++)
            {
                if (!arr[i].IsEmpty) continue;
                int add = Mathf.Min(cap, remaining);
                arr[i] = new ItemStack(def, add);
                remaining -= add;
                changed = true;
            }
            return remaining;
        }

        /// <summary>
        /// Add a TOOL and AUTO-PLACE it on the belt (AC3 — the axe lands in belt slot 1 on pickup). Puts
        /// it in the FIRST free belt slot (slot 0 = "belt slot 1") if the item is belt-eligible and the
        /// belt has room; otherwise falls back to <see cref="AddItem"/> (the pack). Returns the
        /// <see cref="SlotRef"/> it landed in, or null if nothing fit. Fires Changed on a real placement.
        /// </summary>
        public SlotRef? AddToolToBelt(ItemDef def)
        {
            if (def == null || !ItemDef.IsBeltEligible(def)) return null;
            for (int i = 0; i < _belt.Length; i++)
            {
                if (!_belt[i].IsEmpty) continue;
                _belt[i] = new ItemStack(def, 1);
                Changed?.Invoke();
                return SlotRef.Belt(i);
            }
            // Belt full — fall back to the pack so the pickup is never silently dropped. Record the
            // first EMPTY pack slot BEFORE the add: AddItem→FillFreeSlots fills the lowest-index empty
            // inventory slot first, and a tool (non-stackable, cap 1) never merges — so the tool lands
            // in exactly that slot. Returning it (not a re-scan for `Def == def`, which could match a
            // PRE-EXISTING same-def tool in a lower slot) reports the slot the pickup ACTUALLY landed in
            // (NIT fix, PR #90). This is a rare cold path (belt full on pickup) — no per-frame alloc.
            int landed = -1;
            for (int i = 0; i < _inventory.Length; i++)
                if (_inventory[i].IsEmpty) { landed = i; break; }
            int left = AddItem(def, 1);
            if (left == 0 && landed >= 0 && _inventory[landed].Def == def)
                return SlotRef.Inventory(landed);
            return null;
        }

        /// <summary>
        /// Move/merge a stack between any two slots (contract §4 / AC6). Returns false (NO-OP) when the
        /// move violates belt-eligibility (a RESOURCE dragged onto a belt slot) — the data-model half of
        /// the UI's .slot--drop-deny. A no-op move (same slot, or empty source) also returns false.
        /// Merges same-item resource stacks (respecting MaxStack); otherwise swaps. Fires Changed on a
        /// real move.
        /// </summary>
        public bool TryMove(SlotRef from, SlotRef to)
        {
            if (from.Area == to.Area && from.Index == to.Index) return false; // same slot
            if (!InBounds(from) || !InBounds(to)) return false;

            ItemStack src = Get(from);
            if (src.IsEmpty) return false; // nothing to move

            // THE LOAD-BEARING GATE (AC6): a non-belt-eligible item (any RESOURCE) can NEVER land on the
            // belt — tools → belt-allowed, resources → inventory-only (ItemDef.IsBeltEligible). Reject the
            // move so the belt stays empty and the resource stays in the pack.
            if (to.Area == SlotArea.Belt && !ItemDef.IsBeltEligible(src.Def))
                return false;

            ItemStack dst = Get(to);

            // Merge same-item resource stacks (respect the cap; spill stays in source).
            if (!dst.IsEmpty && dst.Def == src.Def && ItemDef.IsStackable(src.Def))
            {
                int cap = src.Def.MaxStack;
                int room = cap - dst.Count;
                if (room <= 0) return false; // destination full — no-op (don't swap a full same-item stack)
                int moved = Mathf.Min(room, src.Count);
                dst.Count += moved;
                src.Count -= moved;
                Set(to, dst);
                Set(from, src.Count <= 0 ? ItemStack.Empty : src);
                Changed?.Invoke();
                return true;
            }

            // Swap (or move into an empty slot). BOTH sides of the swap must respect belt-eligibility
            // (BLOCKER-2 fix, PR #90). The gate above guards the FORWARD item (src→to); the swap also
            // moves the DISPLACED item (dst→from), so if 'from' is a BELT slot the displaced 'dst' must
            // ALSO be belt-eligible — else a tool-out-of-belt drag lands a RESOURCE on the belt. Concrete
            // bug: Belt[0]=axe (tool), Inventory[0]=wood (resource); drag Belt[0]→Inventory[0] ⇒ to is
            // Inventory ⇒ forward gate skipped ⇒ defs differ ⇒ swap ⇒ wood→Belt[0], breaking the
            // belt=tools-only invariant (AC6 / contract §2). The prior comment wrongly assumed a belt
            // 'from' "only ever held a tool" and concluded the swap was safe — that reasoning WAS the bug.
            if (from.Area == SlotArea.Belt && !dst.IsEmpty && !ItemDef.IsBeltEligible(dst.Def))
                return false;

            Set(to, src);
            Set(from, dst);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Set the selected belt slot (clamped to the belt-slot count); fires Changed. Number
        /// keys 1–N / scroll call this (contract §4 / AC2).</summary>
        public void SelectBelt(int index)
        {
            int clamped = Mathf.Clamp(index, 0, _belt.Length - 1);
            if (clamped == _selectedBelt) return;
            _selectedBelt = clamped;
            Changed?.Invoke();
        }

        /// <summary>Cycle the selected belt slot by <paramref name="delta"/> (mouse-scroll), WRAPPING at
        /// the ends (AC2 "scroll to cycle"). Fires Changed on a real change.</summary>
        public void CycleBelt(int delta)
        {
            if (delta == 0 || _belt.Length <= 1) return;
            int n = _belt.Length;
            int next = ((_selectedBelt + delta) % n + n) % n; // wrap both directions
            if (next == _selectedBelt) return;
            _selectedBelt = next;
            Changed?.Invoke();
        }

        /// <summary>
        /// Remove ONE from the selected belt slot (contract §4 / §6 — the berry "eat" action; what eating
        /// RESTORES is OOS, no hunger need yet). Returns false if the selected slot is empty. Clears the
        /// slot to empty when the last one is consumed. Fires Changed on a real consume.
        /// </summary>
        public bool TryConsumeSelected()
        {
            ItemStack s = _belt[_selectedBelt];
            if (s.IsEmpty) return false;
            s.Count--;
            _belt[_selectedBelt] = s.Count <= 0 ? ItemStack.Empty : s;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// ALL-OR-NOTHING remove of <paramref name="amount"/> of item <paramref name="id"/> across stacks
        /// (inventory first, then belt). If fewer than <paramref name="amount"/> are held, NOTHING is
        /// debited and false is returned (no partial deduction, no Changed) — the legacy SpendWood
        /// "no wood -> no campfire" gate (contract §7). On success drains stacks to empty as needed, fires
        /// Changed exactly once, returns true. A zero/negative amount is a no-op success.
        /// </summary>
        public bool RemoveItem(string id, int amount)
        {
            if (amount <= 0) return true;
            if (CountItem(id) < amount) return false; // can't afford -> debit nothing

            int remaining = amount;
            remaining = DrainArray(_inventory, id, remaining);
            remaining = DrainArray(_belt, id, remaining);
            // remaining is 0 here by the affordability check above.
            Changed?.Invoke();
            return true;
        }

        private static int DrainArray(ItemStack[] arr, string id, int remaining)
        {
            for (int i = 0; i < arr.Length && remaining > 0; i++)
            {
                if (arr[i].IsEmpty || arr[i].Def.Id != id) continue;
                int take = Mathf.Min(arr[i].Count, remaining);
                arr[i].Count -= take;
                remaining -= take;
                if (arr[i].Count <= 0) arr[i] = ItemStack.Empty;
            }
            return remaining;
        }

        // ============================================================================================
        // MATERIAL-COST CRAFT (ticket 86camz9uz / crafting-redesign ① — the recipe seam). Debit inputs
        // all-or-nothing (the SpendWood idiom, generalised to multiple input lines) → grant the output tool
        // via AddToolToBelt. This COMPOSES the existing mutators (RemoveItem + AddToolToBelt) — it does NOT
        // fork a parallel inventory/recipe model, and it is NOT the free-mint CraftAxe path (which mints an
        // axe for free; this SPENDS materials). Pure C# so the affordability/debit/grant truth-table is
        // EditMode-testable without a scene.
        // ============================================================================================

        /// <summary>
        /// PURE affordability check ACROSS ALL cost lines (all-or-nothing): true iff the pack+belt hold at
        /// least the summed required amount of EVERY distinct input id. Sums per id so a recipe listing the
        /// same id twice (defensive — ① recipes use one line per id) is honoured. Debits NOTHING — the read-
        /// only gate the craft checks BEFORE touching any stack (so a short-on-line-2 recipe never debits
        /// line 1). The load-bearing "can't afford → no craft, no debit" contract.
        /// </summary>
        public bool CanAfford(IReadOnlyList<RecipeCost> costs)
        {
            if (costs == null || costs.Count == 0) return true; // a free recipe trivially affordable
            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i].Amount <= 0) continue;
                // Sum every line that shares this id (handles a repeated id) — count once per distinct id.
                bool seenEarlier = false;
                for (int j = 0; j < i; j++) if (costs[j].ItemId == costs[i].ItemId) { seenEarlier = true; break; }
                if (seenEarlier) continue;
                int required = 0;
                for (int j = 0; j < costs.Count; j++)
                    if (costs[j].ItemId == costs[i].ItemId && costs[j].Amount > 0) required += costs[j].Amount;
                if (CountItem(costs[i].ItemId) < required) return false;
            }
            return true;
        }

        /// <summary>
        /// Craft: check <paramref name="costs"/> are affordable across ALL lines, GRANT the output tool onto
        /// the belt (or the pack if the belt is full) FIRST, then debit the inputs. Granting first makes the
        /// craft LOSS-FREE — if the inventory is completely full so the grant cannot land, the craft aborts
        /// with NO debit (materials are never spent for a tool that had nowhere to go). Because granting a
        /// Tool never changes any RESOURCE count, the affordability check taken before the grant still holds,
        /// so the debit after it is guaranteed. Returns true iff the craft actually happened (output granted +
        /// inputs debited). A null output or an unaffordable/no-room recipe returns false and debits nothing.
        /// </summary>
        public bool TryCraft(IReadOnlyList<RecipeCost> costs, ItemDef output)
        {
            if (output == null) return false;
            if (!CanAfford(costs)) return false;            // can't afford → no craft, no debit
            var landed = AddToolToBelt(output);             // grant FIRST (loss-free) — fires Changed on placement
            if (landed == null) return false;               // inventory completely full → abort, nothing debited
            if (costs != null)
                for (int i = 0; i < costs.Count; i++)
                    if (costs[i].Amount > 0) RemoveItem(costs[i].ItemId, costs[i].Amount); // guaranteed by CanAfford
            return true;
        }

        // ============================================================================================
        // QUERIES — the façade + held-item drivers read these (contract §5/§7).
        // ============================================================================================

        /// <summary>The stack at a given slot (empty if out of bounds).</summary>
        public ItemStack At(SlotRef slot) => InBounds(slot) ? Get(slot) : ItemStack.Empty;

        /// <summary>The currently-selected belt stack (drives the held item — contract §5).</summary>
        public ItemStack SelectedBeltStack => _belt[_selectedBelt];

        /// <summary>True if ANY slot (inventory OR belt) holds an item with this id — the ownership query
        /// the legacy <c>HasAxe</c> façade maps to (contract §7).</summary>
        public bool OwnsItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return CountItem(id) > 0;
        }

        /// <summary>Summed Count of every stack of this id across BOTH arrays — what the legacy
        /// <c>WoodCount</c> façade maps to (contract §7: "summed Count of all wood stacks").</summary>
        public int CountItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int total = 0;
            for (int i = 0; i < _inventory.Length; i++)
                if (!_inventory[i].IsEmpty && _inventory[i].Def.Id == id) total += _inventory[i].Count;
            for (int i = 0; i < _belt.Length; i++)
                if (!_belt[i].IsEmpty && _belt[i].Def.Id == id) total += _belt[i].Count;
            return total;
        }

        /// <summary>True if the SELECTED belt slot holds an item with this id — the held-item show/hide
        /// query (contract §5 / AC4: the axe shows in-hand ONLY when it is the selected belt item).</summary>
        public bool IsSelectedBeltItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            ItemStack s = _belt[_selectedBelt];
            return !s.IsEmpty && s.Def.Id == id;
        }

        // ============================================================================================
        // Internals.
        // ============================================================================================

        private bool InBounds(SlotRef s)
        {
            int n = s.Area == SlotArea.Inventory ? _inventory.Length : _belt.Length;
            return s.Index >= 0 && s.Index < n;
        }

        private ItemStack Get(SlotRef s)
            => s.Area == SlotArea.Inventory ? _inventory[s.Index] : _belt[s.Index];

        private void Set(SlotRef s, ItemStack v)
        {
            if (s.Area == SlotArea.Inventory) _inventory[s.Index] = v;
            else _belt[s.Index] = v;
        }
    }
}
