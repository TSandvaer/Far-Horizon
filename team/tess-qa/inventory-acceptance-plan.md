# Inventory / Belt — Acceptance Plan (ticket `86caa4bya`)

**Status:** TEST-DESIGN-AHEAD — authored before Devon's impl PR opens. AC-tied check list + the
silent-killer surfaces QA hammers. PlayMode-sample where correctness is per-frame; EditMode for the
data model + guards.

**Sources pinned:**
- Drew's vocabulary contract `origin/drew/item-model-contract:team/drew-dev/inventory-item-model-contract.md`
  (`ItemDef` / `ItemKind{Tool,Resource}` / `ItemStack` / `SlotRef{SlotArea{Inventory,Belt}}` / `InventoryModel`;
  guards `ItemDef.IsBeltEligible` + `ItemDef.IsStackable`; canonical ids `axe`/`wood`/`stone`/`berry`;
  the §7 façade migration seam).
- Uma `team/uma-ux/ui-toolkit-panels-ux-spec.md` (slot BEM vocabulary, input-bleed gating §4.1, Tab focusable-false §4.2).
- `team/TESTING_BAR.md` (paired tests, locomotion-sampling, shipped-build capture, Self-Test Report, Diagnose-Before-Fix for any `fix(...)`).
- SHIPPED `Assets/Scripts/Runtime/Inventory.cs` (thin ledger) + its callers (see §SILENT-KILLERS).

This plan is QA-side; it does NOT pre-write Devon's AC8 PlayMode tests (he owns those). It is the
verdict checklist Tess scores the PR against + the bug-class probes a green AC8 suite can still miss.

---

## A. Item model + discriminator + guards (EditMode — pure data, no scene)

| # | Check | Pass criterion | Maps to |
|---|---|---|---|
| A1 | `ItemDef.Kind` is the SINGLE source of belt-eligibility | `IsBeltEligible(toolDef)==true`, `IsBeltEligible(resourceDef)==false`, `IsBeltEligible(null)==false` | AC6, contract §2 |
| A2 | Stackability derived from `Kind`, NOT a free bool | `IsStackable(resourceDef)==true`, `IsStackable(toolDef)==false`; `MaxStack` for Tool == 1; for Resource == stack-size setting (default 20) | AC7, contract §2/§4 |
| A3 | No `bool stackable` field that can disagree with `Kind` | grep the diff — assert there is no per-asset stackable bool; `IsStackable` reads `Kind` only | contract §2 (silent-killer) |
| A4 | `ItemStack.Empty` / `IsEmpty` canonical empty test | `Empty.Def==null && Count==0`; `IsEmpty <=> Def==null` (Count==0 with non-null Def is NOT "empty") | contract §4 |
| A5 | Canonical ids exact + lowercase-kebab | catalog holds `axe`/`wood`/`stone`/`berry`; `ItemCatalog.ById("wood")` resolves; `ById("chopped wood")` and `ById("Wood")` do NOT (id is the kebab key, prose is `DisplayName`) | AC5, contract §3 |
| A6 | `wood` is ONE item | only ONE `wood.asset`; `DisplayName=="Wood"`; chop + sticks both target the same def (verify no second wood id exists) | contract §3 (silent-killer — two tickets minting two "wood") |

## B. Slot / belt moves + selection + consume (EditMode on `InventoryModel`)

| # | Check | Pass criterion | Maps to |
|---|---|---|---|
| B1 | `AddItem` returns the unfit remainder | adding within cap returns 0; overflow spills to next free slot; over-capacity returns the leftover amount (NOT silently dropped) | AC7, contract §4 |
| B2 | `AddItem` stacks to cap then spills | N+M ≤ cap merges in one slot; exceeding cap fills slot to `MaxStack` then opens the next; tool `AddItem` never merges (MaxStack 1) | AC7 |
| B3 | `TryMove` resource→belt is REJECTED | `TryMove(invWoodSlot, beltSlot)` returns false AND is a no-op (source unchanged, belt slot still empty) — the data half of `.slot--drop-deny` | **AC6 (load-bearing)** |
| B4 | `TryMove` tool→belt ALLOWED | axe inv→belt returns true, seats in belt; belt→inv returns true | AC6 |
| B5 | `TryMove` inv↔inv + merge | same-def stacks merge respecting cap; different-def swap; the spill/merge math is exact (no item duplication, no loss) | AC6 |
| B6 | `SelectBelt` clamps + fires `Changed` | index clamped to `[0, beltCount-1]`; out-of-range clamps (not throw); `Changed` fires once | AC2, contract §4 |
| B7 | `TryConsumeSelected` removes exactly 1 | berry stack of 3 → 2 → 1 → empty; on empty/non-consumable target returns false, no underflow to -1 | AC-berry/§6 |
| B8 | Settings drive counts | inventory-slot / belt-slot / stack-size counts read the registered settings (default 20/5/20); changing the setting changes the model's slot-array length (registry wiring to `86caa4bqp`) | AC1/AC2/AC7 |

## C. Held-item show/hide via `SelectedBeltIndex` (PlayMode — per-frame, sampled through a real selection sequence)

| # | Check | Pass criterion | Maps to |
|---|---|---|---|
| C1 | Axe in selected belt slot → shown | axe in belt 1, slot 1 selected → `HeldAxeRig` renderers ENABLED | AC4 |
| C2 | Axe in belt slot 2, slot 1 selected → HIDDEN | renderers disabled (axe present on belt but NOT selected) | AC4 |
| C3 | Select slot 2 → axe appears | `SelectBelt(1)` flips renderers on; rim ⇄ in-hand never disagree | AC4 |
| C4 | Axe moved OFF belt into inventory → hidden | held axe hidden whenever no belt slot holds `axe` as the selected item | AC4 |
| C5 | Show/hide cycles cleanly under repeated selection | scroll/number-key cycle 1→2→3→1 N≥8 times: every frame the renderer state matches `BeltSlots[SelectedBeltIndex].Def.Id=="axe"` (PlayMode `yield return null` sample, NOT a single snapshot — testing-bar locomotion-sampling) | AC4, AC8 |

## D. Persistence (EditMode/PlayMode)

| # | Check | Pass criterion |
|---|---|---|
| D1 | Slot layout survives relaunch | inventory + belt slot contents + `SelectedBeltIndex` persist by `Id` (the stable kebab key, contract §1 — NEVER by enum ordinal or asset GUID) and reload to the same slots |
| D2 | Settings persist + apply | belt/inv/stack-size persist via the settings panel's PlayerPrefs authority; reload restores counts |

---

## SILENT-KILLERS — the surfaces a green AC8 suite can still ship broken

These are the bug classes QA hammers regardless of a green suite (the "pickup_count > 0 passed through
the entire dual-spawn era" class — a passing assertion that asserts the wrong thing).

1. **FAÇADE MIGRATION SEAM — the #1 silent-killer.** The shipped `Inventory.cs` is consumed VERBATIM by a
   wide blast radius: `SurvivalHud` (`HasAxe`/`WoodCount`/`Changed`), `ChopTree` (`AddWood` + reads
   `WoodCount`/`HasAxe`), `CraftSpot`/`StumpAxe`/`CampfirePlacement`, and the `*VerifyCapture` rigs.
   Per contract §7 the façade MUST keep `HasAxe` (→ any slot holds `axe`), `WoodCount` (→ summed `Count`
   of all `wood` stacks), `Changed` (fires on ANY slot/selection change), plus shims `AddWood`/`CraftAxe`/
   `SpendWood` forwarding to the slot model.
   - **CHECK:** existing `InventoryTests.cs` (12 tests: `CraftAxe_*`, `AddWood_*`, `SpendWood_*`) MUST stay
     green UNCHANGED — they are the regression pin on the façade. "Extend, don't replace" (AC8): if those
     tests were edited/deleted to make the slot model pass, that is a REGRESSION, bounce it.
   - **CHECK:** `WoodCount` after `AddItem(woodDef, 5)` reads 5; after spilling across two stacks still sums
     correctly (the silent-killer: a façade that reads only the FIRST wood stack reports a low count to the
     HUD — "pickup worked but HUD says 3 not 23"). Probe with a count that forces a stack spill (≥ cap+1).
   - **CHECK:** `ChopTree.AddWood(n)` shim still compiles + chops still increment the HUD (PlayMode chop
     beat); `SpendWood` all-or-nothing build-gate still holds (campfire).

2. **HELD-AXE VISIBILITY MIGRATES FROM `HasAxe` TO `SelectedBeltIndex` — a coherence trap.** TWO components
   gate on `HasAxe` TODAY: `HeldAxe.cs` (renderers on when `HasAxe`) AND `CastawayFingerCurl.cs` (curl when
   `HasAxe`). AC4 changes the show rule to "axe is the SELECTED belt item." If only `HeldAxe` is migrated and
   `CastawayFingerCurl` is left on `HasAxe`, the hand CURLS as if gripping while the axe is HIDDEN (axe owned
   but not selected) — empty-hand grip pose. Conversely if the rig hides but the renderers are toggled on a
   STALE `HasAxe` subscription, the axe shows when it shouldn't.
   - **CHECK:** finger-curl gate and held-axe visibility BOTH follow `SelectedBeltIndex` (axe selected), not
     `HasAxe`, OR the divergence is explicitly justified. Sample in PlayMode: axe owned + slot 2 selected →
     renderers OFF **and** curl OFF together. (Captured the analogous coherence gap for settings input-bleed.)
   - **CHECK:** the LOCKED rig internals are UNTOUCHED — `worldOffsetFromHand` (`0.003,-0.017,0.009`),
     `relEuler` (`4.1,95.8,-56.1`), the `LateUpdate` follow/raw-hand math, baked seat `7f4bc6b`. AC4 is
     VISIBILITY-ONLY. Diff `HeldAxeRig.cs`: any change to pose/follow/finger-curl math = REQUEST_CHANGES.
     The PR must touch only the renderer-enable path, never the seat.

3. **INPUT-BLEED (the one I caught for settings).** Number keys 1–5 + mouse-scroll select belt slots via
   legacy `Input` polling. Per Uma §4.1 these MUST be gated by explicit `_inventoryOpen`/`_settingsOpen`
   flags — `Input.*` polling bleeds THROUGH open UI Toolkit panels.
   - **CHECK:** open inventory (Tab) → scroll wheel does NOT cycle the belt selection underneath; number
     keys do NOT re-select; open settings (Esc) → same. Sample in shipped build, not just editor.
   - **CHECK:** `Tab` over the grid does NOT navigate focus within slots (`.slot focusable=false`, Uma §4.2)
     — it closes the inventory. A focusable slot silently eats the Tab-close.

4. **STACK MATH — item dup / loss under move+merge.** A naive drag-drop that adds-to-target without
   debiting-source duplicates items; a merge that overflows without spilling loses them.
   - **CHECK:** total item count is CONSERVED across every `TryMove`/`AddItem` (sum before == sum after +
     returned-remainder). Probe move-into-near-full-stack (merge with overflow), move onto self (no-op, no
     dup), move tool onto a resource stack (rejected or swap, never merge).

5. **DENY IS A NO-OP, NOT A HALF-MOVE.** A rejected resource→belt `TryMove` must leave BOTH slots exactly
   as they were. The dangerous failure: source cleared but target rejected → item vanishes. Assert source
   unchanged on every `false` return (B3).

---

## Process gates (testing-bar — Tess enforces on the PR)

- **Paired tests in-PR:** AC8 PlayMode (pickup→belt 1, selected-slot show/hide, move inv↔belt with
  tool-vs-resource enforced + wood/stone/berry REJECTED from belt, stack-to-cap) + extended EditMode on the
  model. Existing 12 `InventoryTests` stay green unchanged.
- **Shipped-build capture (UX-visible):** `CaptureGate` evidence from the BUILT exe — Tab inventory open +
  belt strip + axe pickup auto-to-belt-1 + select-slot show/hide. Editor evidence is necessary, never
  sufficient. Verify the HUD build-stamp before judging.
- **Self-Test Report comment** before QA review: what was run, on which build stamp, what was observed.
- **Frame-Debugger / SRP-batcher audit:** UI Toolkit is geometry-free (no new shader/mesh per Uma §6) — but
  if Devon adds the icon Sprite Atlas, confirm icon-bearing slots stay in ONE draw batch under the 8-texture
  cap (Uma §4.6). No `MaterialPropertyBlock` on any new renderer.
- **Diagnose-Before-Fix:** any follow-up `fix(...)` PR states the diagnosed root cause + one cited isolation
  result before the fix.
- **Sponsor soak:** subjective feel (drag-drop tactility, select-slot snap, carved-wood warmth) — exe path +
  expected HUD stamp in the ask.

## Out of scope (per ticket / contract — do NOT bounce for these)

Crafting the axe (pickup-for-testing here); actual chop/stone/berry gathering (own tickets, against this
model); `ItemKind.Consumable` + `RestoreAmount` (no hunger need yet, contract §6); equipment/armor; item
tooltips/stats beyond name+icon+count.
