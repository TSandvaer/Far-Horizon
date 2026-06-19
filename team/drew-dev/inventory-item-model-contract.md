# Item-Model + Tool-vs-Resource Contract (VOCABULARY CONTRACT)

**Status:** DESIGN spec — NO implementation here. This is the shared vocabulary the inventory/belt
ticket (`86caa4bya`) and the whole world-resource family (`86caa4c5c` chop-wood, `86caa4c96` stones,
`86caa96rd` sticks, `86caa5zz3` berries/bushes) plug into so they can be built in **parallel** later
without divergence. Per the user-global *Parallel-agent shared-concept vocabulary discipline*: every
type / enum / discriminator / field / event name below is **exact and load-bearing** — implementers
read these names verbatim, do NOT invent their own.

**Owner:** Drew (Dev2 — content/data). **Reviewer:** Devon (peer). Harvested via a docs PR.

**Reads from:** `unity6-mastery.md §6` (ScriptableObject architecture + SO event channels),
`team/erik-consult/ui-toolkit-vs-ugui-fh.md` + `team/erik-consult/ui-toolkit-inventory-settings-research.md`
(`[CreateProperty]` binding, dynamic atlas, PointerManipulator drag), `team/uma-ux/ui-toolkit-panels-ux-spec.md`
(slot BEM vocabulary, the tool-vs-resource teach), and the SHIPPED `Assets/Scripts/Runtime/Inventory.cs`
(the thin ledger this model supersedes — see §7 migration seam).

---

## 0. Ground truth — what ships today vs. what this model introduces

The CURRENTLY-SHIPPED `Inventory.cs` (ticket 86ca8bdaq) is a **thin two-field ledger**, NOT a slot model:
`bool HasAxe`, `int WoodCount`, `event Action Changed`, write paths `CraftAxe()` / `AddWood(int)` /
`SpendWood(int)`. The U2-5 HUD consumes `HasAxe` / `WoodCount` / `Changed` **verbatim**, and
`ChopTree.cs` calls `inventory.AddWood(woodPerChop)`. Those call-sites are load-bearing.

This contract defines the **new slot/item-grid model** that `86caa4bya` builds. §7 specifies the
**migration seam** so the HUD and `ChopTree` keep working (no stranded callers, no second data-surface PR).

---

## 1. The item DEFINITION type — `ItemDef` (ScriptableObject)

The canonical item type. ONE ScriptableObject asset per distinct item kind (axe, wood, stone, berry).
Asset, not a MonoBehaviour field, not JSON (`unity6-mastery.md §6`: all tuning/content config = SO assets).

```
[CreateAssetMenu(menuName = "Far Horizon/Item Def")]
public sealed class ItemDef : ScriptableObject
```

**Exact field/property names (the binding + gameplay surface — do NOT rename):**

| Name | Type | Meaning |
|---|---|---|
| `Id` | `string` | Stable string key, lowercase-kebab. The persistence + lookup key. NEVER reuse/reassign. Canonical ids in §3. |
| `DisplayName` | `string` | Player-facing name (e.g. "Wood", "Berries"). The HUD/tooltip text. |
| `Kind` | `ItemKind` | The tool-vs-resource discriminator (§2). The SINGLE source of belt-eligibility + stackability. |
| `Icon` | `Sprite` | The slot icon (IconBaker-rendered prop sprite per Uma §4.6). Null → letter-chip fallback. |
| `MaxStack` | `int` | Per-slot stack cap. **Derived from `Kind`, not free-authored** — see §2 / §4. Tools = 1; resources = the tweakable resource stack-size (default 20). |

`Id`, `DisplayName`, `Kind`, `MaxStack` are exposed as `[CreateProperty]` get-only properties (backing
fields `[SerializeField, DontCreateProperty]`) so UI-Toolkit slot binding reads them with zero
reflection cost (`ui-toolkit-inventory-settings-research.md §E4`; both annotations required or it
silently falls back to reflection).

**`ItemDef` assets live at** `Assets/Data/Items/<id>.asset` (e.g. `Assets/Data/Items/wood.asset`).
A single `ItemCatalog : ScriptableObject` (`Assets/Data/Items/ItemCatalog.asset`) holds
`IReadOnlyList<ItemDef> All` + `ItemDef ById(string id)` for lookup — the export site. Consumers
reference the catalog, never a hard `ItemDef` field array.

---

## 2. The tool-vs-resource discriminator — `ItemKind` (enum)

The discriminator is an **enum**, not a bool, so a future third kind (consumable/food, equipment) slots
in without a wire-format break.

```
public enum ItemKind { Tool, Resource }
```

- Discriminator **values:** `ItemKind.Tool` and `ItemKind.Resource` (exact). Serialized name `Kind`.
- **Belt-eligibility guard** (the AC6 tool-vs-resource rule, ONE definition, used everywhere — UI deny-glow
  AND data-model move-rejection):

  ```
  public static bool IsBeltEligible(ItemDef def) => def != null && def.Kind == ItemKind.Tool;
  ```

  Exact name: **`ItemDef.IsBeltEligible(ItemDef)`** (static). Tools → belt-allowed; Resources → inventory-only.
  This is the guard Uma's `.slot--drop-deny` (resource dragged at the belt) and `.slot--equippable`
  (tool corner-notch) bind to (`ui-toolkit-panels-ux-spec.md §4.3`).
- **Stackability** is derived from `Kind`, NOT a separate bool: `Tool` → `MaxStack == 1` (never stacks,
  no count badge); `Resource` → `MaxStack == <resource stack-size setting>` (default 20). The guard
  **`ItemDef.IsStackable(ItemDef)`** = `def.Kind == ItemKind.Resource` is the single source; do NOT add a
  per-asset `bool stackable` that could disagree with `Kind`.

---

## 3. Canonical item ids (the registry — prevents two tickets minting two "wood")

Each world-resource ticket REUSES the id below; it does NOT mint its own. **`wood` is the single wood
item** — chop (`86caa4c5c`) and sticks (`86caa96rd`) BOTH add to it (sticks = 1, chop = N; same `ItemDef`).

| `Id` | `DisplayName` | `Kind` | Source ticket(s) | Notes |
|---|---|---|---|---|
| `axe` | Axe | `Tool` | `86caa4bya` (pickup PoC; later crafted) | Non-stacking. Auto-placed in belt slot 1 on pickup. |
| `wood` | Wood | `Resource` | `86caa4c5c` (chop, N/chop) + `86caa96rd` (stick, 1/pickup) | **ONE item, two sources.** Supersedes the ledger `WoodCount`. |
| `stone` | Stone | `Resource` | `86caa4c96` (small-stone pickup) | Ticket text says "picked up stones" — id is `stone`, DisplayName "Stone". |
| `berry` | Berries | `Resource` | `86caa5zz3` (berry-bush harvest) | Edible (§6). "What eating restores" is OOS (no hunger need yet). |

> Ticket text uses prose labels ("chopped wood", "picked up stones"). The **id is the kebab key above**;
> the prose maps to `DisplayName`. Sticks ticket `86caa96rd` explicitly says "follow the inventory ticket's
> name — single source of truth" → that name is `wood`.

---

## 4. The STACK + SLOT model

**`ItemStack` (struct, value type)** — what occupies a slot. Exact names:

```
public struct ItemStack {
    public ItemDef Def;     // null == empty slot (the canonical empty test: ItemStack.IsEmpty)
    public int     Count;   // 1..Def.MaxStack ; 0 only transiently before clearing to empty
}
```

- `ItemStack.Empty` (static readonly) = `{ Def = null, Count = 0 }`. `IsEmpty => Def == null`.
- A `Tool` stack is always `Count == 1`. A `Resource` stack is `1..MaxStack`; overflow spills to the
  next free slot (AC7 stack-to-cap).

**`InventoryModel`** — the new slot-array owner (replaces the thin ledger's two scalars):

| Member | Signature | Meaning |
|---|---|---|
| `InventorySlots` | `IReadOnlyList<ItemStack>` | The grid (default 20; count = `inventory slots` setting). |
| `BeltSlots` | `IReadOnlyList<ItemStack>` | The hotbar (default 5; count = `belt slots` setting). |
| `SelectedBeltIndex` | `int` | 0-based selected belt slot. Drives held-item (§5). |
| `Changed` | `event Action` | Fires on ANY slot/selection change. UI + HUD subscribe, never poll (same pattern as the ledger's `Changed` / `WarmthNeed.Changed`). |

**Write paths (the ONLY mutators — exact names; these are what the resource tickets call):**

| Method | Signature | Contract |
|---|---|---|
| `AddItem` | `int AddItem(ItemDef def, int amount)` | Add to existing stacks then free slots; returns the amount that did NOT fit (0 = all fit). THE seam every world-resource pickup calls (chop/stick→`wood`, stone→`stone`, berry→`berry`). Respects `MaxStack`. |
| `TryMove` | `bool TryMove(SlotRef from, SlotRef to)` | Move/merge between any two slots. Returns false (no-op) when the move violates belt-eligibility (resource→belt) — the data-model half of `.slot--drop-deny`. |
| `SelectBelt` | `void SelectBelt(int index)` | Set `SelectedBeltIndex` (clamped to belt-slot-count); fires `Changed`. Number keys 1–5 / scroll call this. |
| `TryConsumeSelected` | `bool TryConsumeSelected()` | Remove 1 from the selected belt OR a targeted slot — the berry "eat" action (§6). |

**`SlotRef`** — addresses a slot across both arrays: `public readonly struct SlotRef { public SlotArea Area; public int Index; }`
with `public enum SlotArea { Inventory, Belt }`. (Exact names — drag/drop source+target use `SlotRef`.)

---

## 5. Held-item coherence (selected belt slot ⇄ in-world tool)

`InventoryModel.SelectedBeltIndex` is the SINGLE source of "what's in hand". The held-item driver reads:
the `ItemStack` at `BeltSlots[SelectedBeltIndex]`; if its `Def.Id == "axe"` → show the axe, else hide it.
The show/hide + grip coherence drivers are **`HeldAxe.cs`** (`Assets/Scripts/Runtime/HeldAxe.cs` — gates the
held hatchet renderer's visibility, `r.enabled = show`) and **`CastawayFingerCurl.cs`**
(`Assets/Scripts/Runtime/CastawayFingerCurl.cs` — gates the hand grip/curl), both currently gated on
`Inventory.HasAxe`. (Note: `HeldAxeRig.cs` is **seat/follow only** — it carries NO visibility logic, so it is
NOT the coherence driver.) The migration repoints these `HasAxe` reads at the slot model's selected-axe test
(per §7's full-surface preservation); their seat/follow internals are UNTOUCHED (the held-axe saga is settled,
out of this scope). This is the load-bearing UI-rim ⇄ in-hand coherence: `.slot--selected` (Uma §4.1) and the
in-world axe never disagree.

---

## 6. Edible resources (berry) — consume action only

`berry` is a `Resource` with an **eat/consume** action: `InventoryModel.TryConsumeSelected()` removes one
from the slot. There is NO `Consumable` kind in v1 — eating is a method on the model, not a third
`ItemKind`. **What eating RESTORES (a hunger/food need) is OUT OF SCOPE** — no hunger need exists yet
(`86caa5zz3 AC5`). When a hunger need lands, add `ItemKind.Consumable` + an `ItemDef.RestoreAmount` field;
this contract reserves the enum-not-bool shape precisely so that extension is non-breaking.

---

## 7. Migration seam — thin ledger → slot model (no stranded callers)

`86caa4bya` replaces the thin `Inventory.cs` ledger. The migration MUST **preserve the FULL `Inventory`
public surface (`HasAxe` / `WoodCount` / `Changed` + ALL callers)** — NOT just the two named below. The
`HasAxe` / `WoodCount` ledger surface is **wider than the HUD + `ChopTree`**: ground-truth callers include
`SurvivalHud.cs` (HUD), `ChopTree.cs` (`AddWood`), `StumpAxe.cs` (`!HasAxe` gate), `CastawayFingerCurl.cs`
(grip gate on `HasAxe`), `HeldAxe.cs` (visibility gate on `HasAxe`), `CampfirePlacement.cs` (`SpendWood`),
`CraftSpot.cs` (`CraftAxe`), the `*VerifyCapture` probes (`AxeVerifyCapture` / `ChopVerifyCapture` /
`CampfireVerifyCapture` / `CraftVerifyCapture` / `WalkGroundingVerifyCapture`), plus ~14 EditMode/PlayMode
tests asserting these gates. An implementer who migrates only the 2 named call-sites strands the rest — so
the façade preserves the WHOLE surface:

- **Keep `Inventory` as a thin façade over `InventoryModel`** (or fold the model into it). It MUST keep
  exposing **every member every caller reads/writes**: `bool HasAxe` (→ any belt/inv slot holds `axe`),
  `int WoodCount` (→ summed `Count` of all `wood` stacks), `event Action Changed`, and the write paths
  `CraftAxe()` / `AddWood(int)` / `SpendWood(int)`. The U2-5 HUD wiring AND every other caller above are
  unchanged.
- **`ChopTree.cs` `inventory.AddWood(n)`** → becomes `AddItem(woodDef, n)` internally; keep an `AddWood(int)`
  shim that forwards to `AddItem(woodDef, n)` so the chop call-site compiles unchanged until `86caa4c5c`
  is rebased onto the slot model. (Chop ticket then switches to `AddItem` directly.)
- `CraftAxe()` / `SpendWood()` façade shims forward to the slot model the same way; `HasAxe` (read by
  `StumpAxe` / `HeldAxe` / `CastawayFingerCurl` / the probes) becomes a slot-model query (any slot holds `axe`).

> Implementers: do this full-surface façade in `86caa4bya` so the HUD, chop, axe-gate, held-axe visibility,
> grip, campfire spend, craft, AND every `*VerifyCapture` probe + test keep green; the resource tickets then
> call `AddItem(catalog.ById("wood"/"stone"/"berry"), amount)` directly. The façade is the no-second-PR bridge.

---

## 8. UI-Toolkit data-binding shape (for the slot views)

Per `ui-toolkit-panels-ux-spec.md §4.3` + research §E4: a slot `VisualElement` binds **`ToTarget`** (read-only
display) to its `ItemStack` projection — `Icon` (→ `style.backgroundImage`), `Count` (→ `.slot__badge`, shown
only at `Count >= 2`), and `IsBeltEligible` (→ `.slot--equippable` notch). Drag/drop is event-driven
(`PointerManipulator`, NOT editor `DragAndDrop`) and calls `TryMove(SlotRef, SlotRef)`; on a denied move the
view adds `.slot--drop-deny`. The `data-source` is assigned in C# (`element.dataSource = model`), left
unresolved in UXML — keeps the model swappable/testable (AC6 / research §E4).

---

## 9. Vocabulary contract summary (the verbatim name list)

| Concept | Exact name | Export site |
|---|---|---|
| Item definition type | `ItemDef` (sealed ScriptableObject) | `Assets/Scripts/Runtime/Items/ItemDef.cs` |
| Discriminator enum | `ItemKind { Tool, Resource }` | same file as `ItemDef` |
| Belt-eligibility guard | `ItemDef.IsBeltEligible(ItemDef def)` (static) | `ItemDef` |
| Stackable guard | `ItemDef.IsStackable(ItemDef def)` (static) | `ItemDef` |
| Stack value type | `ItemStack { ItemDef Def; int Count; }` + `ItemStack.Empty` / `IsEmpty` | `ItemStack.cs` |
| Slot address | `SlotRef { SlotArea Area; int Index; }` + `SlotArea { Inventory, Belt }` | `SlotRef.cs` |
| Model owner | `InventoryModel` (`InventorySlots`/`BeltSlots`/`SelectedBeltIndex`/`Changed`) | `InventoryModel.cs` |
| Add seam (resource pickups call this) | `InventoryModel.AddItem(ItemDef, int)` | `InventoryModel` |
| Move/equip seam | `InventoryModel.TryMove(SlotRef, SlotRef)` | `InventoryModel` |
| Belt select | `InventoryModel.SelectBelt(int)` | `InventoryModel` |
| Consume/eat | `InventoryModel.TryConsumeSelected()` | `InventoryModel` |
| Catalog/export | `ItemCatalog.ById(string)` / `.All` | `Assets/Data/Items/ItemCatalog.asset` |
| Canonical ids | `axe` · `wood` · `stone` · `berry` | §3 |
| Held-item source | `InventoryModel.SelectedBeltIndex` → `HeldAxe.cs` show/hide + `CastawayFingerCurl.cs` grip | §5 |

**The three things every parallel ticket MUST honor:** (1) `wood` is ONE item — chop + sticks both
`AddItem(woodDef, …)`; (2) belt-eligibility is `ItemDef.IsBeltEligible` (Tool only) — never a local bool;
(3) every world-resource pickup adds via `InventoryModel.AddItem(catalog.ById(id), amount)` — no
bespoke per-resource counter.
