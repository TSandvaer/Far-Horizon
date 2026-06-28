# Water Acquisition — Belt Item · Pond E-Prompt · Drink Feedback · Visual Micro-Spec

**Ticket:** `86cafc6vx`. **Owner of this spec:** Uma (UX). **Implements:** Devon (pond→IPickable wiring, drink seam) / Drew (icon bake if a render lands). **Reviewer:** Drew.
**Work-type:** `design(spec)` — docs only, no `Assets/`, no shader authoring. Direction Devon/Drew quote in their dispatch.

**Sponsor decision (2026-06-28, Option A):** `E` at the pond → a **'water' belt item** → **left-click drinks it**. Consistent with the other E-loot (sticks/stones/wood/berries). This spec is purely the LOOK/FEEL of those three beats; the model + input seams are ALREADY built (see "What's already built").

> **Read order for the implementer:** tonal anchor → §1 belt-item visual → §2 pond E-prompt → §3 drink feedback. The anchor for the pond + the drink GESTURE itself is unchanged from [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) — this doc covers ONLY what changed now that water is a held belt item (the loot beat is new; the drink beat moved from proximity-Q-at-pond to left-click-the-held-item).

---

## Tonal anchor (lead with this — everything serves it)

**Collecting water is the island handing you a small, carried kindness; drinking it later is that kindness spent.**

The pond's anchor is unchanged — *"a small, still, SAFE drink, the island looking after you"* ([`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md)). What Option A adds is a **carry beat in the middle**: the castaway crouches at the pond and **gathers** water into his pack, then can drink it **anywhere** — at the fire, on the trail, mid-journey. The fiction shifts from *"drink at the pond"* to *"the pond is a well you fill from."* That carry is hopeful and provisioning, not survival-panic: you stock up on the island's gift and take it with you toward the far horizon.

Two consequences govern every call below:
- **The water item must read as POND water, not ocean/generic blue** — the same fresh blue-cyan the pond pushes toward (the freshwater tell from [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §1a: cooler, brighter, cleaner than the warm teal sea). The slot, the pond, and the thirst bar share ONE cool-blue family so a glance ties slot-water → pond → thirst-bar.
- **Both beats stay GENTLE rituals, no spectacle** — the gather is a soft scoop-into-the-pack; the drink is a small sip. Lively + lightly damped ([[sponsor-prefers-natural-lively-motion]]). No gulp-VFX, no splash. The thirst bar nudges up a *little* per drink.

---

## What's already built (do NOT re-spec — this doc is the FEEL layer on top)

The Sponsor's Option A is already wired in code; verified on `origin/main`:
- **`water` ItemDef** — minted in `ItemCatalog.BuildDefaults` as a belt-eligible `ItemKind.Consumable` (`WaterId = "water"`, DisplayName "Water"). Stacks like berries.
- **Fallback icon** — `ItemIconGen.WaterDrop()` draws a procedural faceted blue water-drop (`Water (0.36,0.62,0.82)` / `WaterDark (0.24,0.46,0.66)`) so a looted unit reads as water even before a baked render exists.
- **Loot prompt path** — `LootPrompt.BuildLabel` is GENERIC: it reads the resolved `IPickable.DisplayName` and renders "Press E to pick up {name}". When the pond becomes an `IPickable`, its DisplayName flows straight through (Devon's wiring; see §2 for the one copy call this spec makes).
- **Left-click drink** — `LeftClickConsume.TryDrinkOneWater()` removes one `water` unit + restores via the SHIPPED `ThirstNeed.AddWater()` (per-scoop amount, atomic). The proximity-drink-at-pond `DrinkAction` is REMOVED (`inputEnabled=false`); drinking is belt-item-driven now.

So Devon/Drew's remaining work is small: (a) make the pond an `IPickable` that yields `water` on E (loot seam, mostly mechanical), and (b) apply the THREE visual/feel notes below. The icon (§1) is the one asset that may want a bake.

---

## §1 — The 'water' belt/inventory item VISUAL

**The read:** a small **carried handful of fresh pond-water** — a waterskin/scooped-water object the castaway tucked into his pack. NOT a manufactured canteen (no container is crafted yet — OOS), NOT a generic UI water-drop forever. It must read as *the pond's water, carried*.

### 1a. v1 ships TODAY on the procedural fallback — it's correct, not a placeholder-to-fix
`ItemIconGen.WaterDrop()` already gives a recognizable faceted blue drop. **That is an acceptable, on-tone v1** — do NOT block the loot/drink loop on a baked render. The drop reads "water," it's cool-blue (right family), and it's reproducible-from-code (no PNG that silently reverts). The only tweak if a quick win is wanted: nudge the drop's blue toward the **pond's freshwater hue** so slot ⇄ pond match (see 1c).

### 1b. The TARGET visual (when a bake lands — the IconBaker route)
Per [`item-icon-bake-recipe.md`](item-icon-bake-recipe.md), the icon set is baked orthographic 3/4 renders of the real low-poly props on the shared `IconBaker` rig (same camera/light as axe/wood/stone/berry). The water item's TARGET prop:

- **A cupped double-handful of water** — the castaway's two hands cupped together holding a small faceted dome of pond-water, OR a **small skin/scoop pouch** if hands-render is awkward at slot size. The **handful read is preferred** (it ties to the drink GESTURE — he scoops with his hands; the carried item is literally that scoop saved). A faceted water dome (a few big chunky facets, a lighter sun-caught top facet, a slightly darker underside) sitting in the cupped hands.
- **Hero pose:** 3/4 from upper-front (the set's one angle), the water dome catching the key light on its top facet so it reads wet/bright; the hands warm castaway-skin below cradling it. ~80–85% frame fill (set consistency).
- **Why a handful over a canteen:** no container is crafted in the fiction yet (a craftable cup/skin is `later` / OOS in [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §2b). A handful is *honest* — it's exactly the gather gesture, frozen. It also keeps the item from reading as manufactured tech in a wash-ashore-with-nothing world.

### 1c. Palette — the freshwater family, sub-1.0, cool-blue (ties slot ⇄ pond ⇄ thirst-bar)
Water is **the one cool note** in the item set (axe slate-neutral, wood/berry/stone warm — [`item-icon-bake-recipe.md`](item-icon-bake-recipe.md) §3). Pull the body color from the pond's freshwater constants so the slot literally shares the pond's water:

- **Water body** `≈ (0.22, 0.66, 0.74)` — the pond's `PondShallow` bright blue-cyan ([`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §1a). Sub-1.0.
- **Sun-caught top facet** `≈ (0.40, 0.78, 0.86)` — a lighter wet highlight so the dome reads as a catching-the-light water surface (the "wet" tell). Sub-1.0.
- **Shadowed underside** `≈ (0.14, 0.48, 0.70)` — the pond's `PondDeep` (B > G freshwater lean) so the dome has faceted depth. Sub-1.0.
- **Cupped hands** — warm castaway skin (reuse the character's hand material/anchor) so the cool water sits in warm hands — the warm/cool contrast makes the water POP and reinforces "his hands, the island's water."
- **Discipline:** every channel sub-1.0 (HDR/sRGB-clamp — the world + HUD + every icon share it). NOT a saturated electric blue; the muted fresh blue-cyan keeps it in the world's palette, distinct from the sea's warm teal.

### 1d. Belt/inventory slot behavior — no new chrome
Water is a stacking Consumable: a belt slot holds a STACK (count badge already handled by the inventory UI). It selects like any belt item (so left-click drinks the selected stack). **No new slot chrome** — it uses the existing `slot-empty` walnut well + count badge + selection highlight, exactly like berries. The cool-blue icon on the warm walnut well gives clean value separation (the [`item-icon-bake-recipe.md`](item-icon-bake-recipe.md) §1.5 legibility check applies: read it at 64px on the walnut, squint-test as "water" in <1s).

---

## §2 — The pond E-PROMPT

**Reuse the generic loot-prompt path — do NOT invent a pond-specific prompt UI.** The pond becomes an `IPickable` (Devon's wiring); its `DisplayName` flows through the SAME `LootPrompt` the sticks/stones/berries use (centred-low cream-on-dark IMGUI plate, the literal `E` key — layout-agnostic on the Danish keyboard, [[sponsor-danish-keyboard-layout]]). One prompt vocabulary across all gather-able things — that consistency IS the read.

### 2a. The COPY — "collect water," not "pick up water"
The generic template is `"Press E to pick up {DisplayName}"`. For most loot (a stick, a stone) "pick up" is right — you bend and grab an object. **Water is different: you don't pick water up, you GATHER it.** The Sponsor's framing is "collect water." Two ways to honor that, in order of preference:

1. **PREFERRED — the pond's `IPickable.DisplayName` carries the verb-fitting noun phrase so the existing template still reads right.** But `"Press E to pick up water"` reads slightly off ("pick up water" is awkward). So:
2. **Add a per-pickable VERB to the prompt.** Give `IPickable` an optional gather-verb (default "pick up"; pond overrides to **"collect"**) so the pond prompt reads **"Press E to collect water"** while sticks/stones keep "Press E to pick up wood/stone." This is a tiny generic extension (one optional string on the interface + the template using it), NOT a pond-special-case prompt. It keeps the single-source-of-truth prompt path while letting the COPY fit the action. **This is the call:** prompt reads **"Press E to collect water."**

> Devon: if the verb-extension is more than a trivial add, v1 may ship `"Press E to pick up water"` on the unmodified generic template (the loop works either way) and the "collect" verb lands as a fast follow. But "collect" is the right word — flag it, don't silently drop it. The READ we want is *gather/fill-from-the-well*, not *grab-an-object*.

### 2b. Prompt timing + placement — unchanged from the shared path
- Appears when the player is in pond drink-range (the same proximity the pond's `IPickable` resolve uses — single source of truth, prompt + actual-loot agree, per `LootPrompt`'s contract). Hides when out of range.
- Centred low, clear of the bottom-left need bars + top stamp plates (the existing `LootPrompt` layout — do not move it).
- **One pond E yields ONE water unit into the belt** (mirrors one-stick/one-stone-per-E). Repeatable: walk up, E, E, E to stock several units — the gather is the provisioning ritual (you fill your pack the way you'd drink several scoops). The per-E yield is Devon's tweakable (default 1; soak-tunable).

---

## §3 — DRINK FEEDBACK (reuse + extend the M-U2 drink-from-hand)

**Anchor restated:** the drink is a small, gentle, repeatable sip; lively + lightly damped; no spectacle. The thirst bar nudges up a *little*. ALL of [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §2 (the crouch-cup-sip-rise gesture timing, the relieved settle, the thirst-bar fill feel) **carries unchanged** — that work is done and right. This section is ONLY the deltas now that water is a held belt item drunk anywhere, not a proximity scoop at the pond.

### 3a. What CHANGED — the drink is now ANYWHERE, off the held water
- **Trigger:** left-click with the **water belt item selected** (`LeftClickConsume`), NOT proximity-Q at the pond. The drink can fire **anywhere** — at the fire, on the trail — because the water is carried. (The pond proximity-drink is removed.)
- **The GESTURE adapts:** [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §2b specified a *crouch-cup-sip-rise* AT the pond (he bends to the water). Now he drinks from his HAND/held water while standing — **drop the crouch-toward-water dip; keep the lift-to-mouth + sip + settle.** The beat becomes **raise-the-cupped-hands(or held water)-to-mouth → small head-tilt-back sip → settle**, ~0.7–1.0s (shorter than the pond's 1.0–1.3s — no crouch-down phase). Still eager + lightly over-damped on the settle ([[sponsor-prefers-natural-lively-motion]]) — the hands follow through, don't snap.
  - **v1 stand-in** (if no cupped-hand anim): a small **head-tilt-back + brief hand-to-mouth** body beat reads "he drank from his hand," and survives an anim upgrade. Same fallback discipline as the pond spec.
- **The cue lands on the SIP beat** (restore + bar-tick fire together at the sip) — cause-and-effect reads clean.

### 3b. What's GONE / changed in the WORLD cue
- **The pond RIPPLE cue is no longer part of the drink** — you're not dipping a hand in the pond when you drink the carried water. The ripple ([`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §2c) moves to the **GATHER beat at the pond** instead: when the castaway collects water (the E-loot), a small soft concentric ripple expands from where he scoops, fading ~0.6–0.9s. Low amplitude (a hand, not a stone). **Nice-to-have, NOT a v1 blocker** — the gather can ship cue-light; the ripple is garnish that reinforces the domestic-calm pond anchor if cheaply driven via the existing water-surface vertex motion. Keep it ~1-draw-call-cheap + transient.
- **The DRINK (anywhere) has no world-water cue** — there's no pond surface to ripple when he sips on the trail. Optional tiny warm-white **droplets off the hand** on the sip (sub-handful, brief) as garnish; cut before it costs draw calls. The load-bearing drink feedback is the gesture + the bar-tick (§3c), not a VFX.

### 3c. Thirst-bar FILL — unchanged, the M-U2 feel carries
- **Each drink restores a SMALL amount** via the SHIPPED `ThirstNeed.AddWater()` (per-scoop amount, Devon's tweakable). The vision's "satisfying small amount with each scoop" holds — tune so a thirsty castaway needs **~4–6 drinks** back to comfortable. That cadence IS the ritual.
- **The per-drink bar rise** wants the quick ease-up + tiny over-shoot-and-settle the HUD ticket (`86caamkxv`) already specs (lively + lightly damped — same motion grammar as the gesture). One soft "tick up" per drink. This is unchanged from [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §2d — the HUD binds the same `ThirstNeed.Current01` read surface.
- **Provisioning note:** because water is now carried, the player can run their water STACK to zero — at which point left-click drinks nothing (clean no-op, already handled). The need to re-collect at the pond IS the loop. No new feedback needed for the empty case beyond the existing "no water held → nothing happens" (the count badge reading 0 + the bar continuing to fall is the read).

### 3d. AUDIO — defer the authored cue, set the target (unchanged from M-U2)
No audio bus/asset pipeline is in scope. **Flag as a follow-up.** Targets, low/intimate, NOT a splash:
- **Gather (at pond):** a soft, short, wet **"scoop"** + a faint trickle as it fills the pack.
- **Drink (anywhere):** a soft **sip/swallow** — quieter than the gather, no pond ambience under it (he's away from the pond).

Leave a hook; don't block thirst on audio.

---

## Cross-references & summary (for the implementer's dispatch)

| Beat | Old (M-U2, proximity) | New (Option A, belt item) | This spec's call |
|---|---|---|---|
| Collect | (n/a — drink-at-pond) | E at pond → 1 `water` unit | "Press E to **collect** water"; pond ripple cue (nice-to-have) |
| Item | (n/a) | `water` belt Consumable | v1 = procedural `WaterDrop` (fine); target = cupped-handful bake, pond-blue palette §1c |
| Drink | crouch-cup-sip-rise at pond | left-click held water, anywhere | drop the crouch dip; keep lift-to-mouth + sip + settle, ~0.7–1.0s |
| World cue | pond ripple on the dip | (drink has none) | ripple MOVES to the gather beat; drink = gesture + bar-tick only |
| Thirst bar | small per-scoop nudge | small per-drink nudge | UNCHANGED — `ThirstNeed.AddWater`, ~4–6 drinks to comfortable |

- **Already-built seams (reuse, do NOT re-spec):** `ItemCatalog` water def + `ItemIconGen.WaterDrop`, generic `LootPrompt.BuildLabel`, `LeftClickConsume.TryDrinkOneWater` → `ThirstNeed.AddWater`. Devon's remaining mechanical work = pond→`IPickable` loot seam + the "collect" prompt-verb extension.
- **Icon route:** [`item-icon-bake-recipe.md`](item-icon-bake-recipe.md) (same `IconBaker` rig; water = the cupped-handful prop, pond-blue palette, the one cool note in the set). v1 procedural fallback is acceptable — don't block.
- **Pond LOOK + drink GESTURE base:** [`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) (§1 pond water deltas + §2 drink gesture — both carry; this doc only deltas the trigger/cue for the carried-water model).
- **Palette discipline:** all sub-1.0 (HDR/sRGB-clamp); water = the freshwater cool-blue family (slot ⇄ pond ⇄ thirst-bar), distinct from the sea's warm teal ([`thirst-pond-drink-feel.md`](thirst-pond-drink-feel.md) §1a; [`item-icon-bake-recipe.md`](item-icon-bake-recipe.md) §3).
- **OOS (look/feel side):** a craftable cup/canteen; a modelled/refractive pond bottom; a big splash VFX; an authored audio asset; saltwater-as-drink; any drink-from-pond proximity path (removed). All `later`.

**Self-Test (Sponsor-input items in PR body):** checked against the art-direction board (freshwater read = cool blue-cyan, [`inspiration/2026-06-12_21h16_13`](../../inspiration/2026-06-12_21h16_13.png) river / `21h16_52` lake) + [`lowpoly-quality.md`](../../.claude/docs/lowpoly-quality.md) (sub-1.0, faceted, no toon-band) + the live built seams (`ItemCatalog`/`ItemIconGen`/`LootPrompt`/`LeftClickConsume`/`ThirstNeed`) the deltas are measured against.

---

## Decision drafts (for Priya to batch into DECISIONS.md)

- **Decision draft:** Water-acquisition visual model (Option A, Sponsor 2026-06-28) — the `water` belt item reads as a **carried cupped-handful of pond-water** (NOT a manufactured canteen — no container crafted yet), pond-freshwater cool-blue palette (`body ≈ (0.22,0.66,0.74)` = pond `PondShallow`), the one cool note in the item set. v1 ships on the procedural `WaterDrop` fallback; the cupped-handful bake is the target, non-blocking.
- **Decision draft:** The pond E-prompt reads **"Press E to collect water"** — a generic per-`IPickable` gather-VERB extension ("collect" for the pond vs the default "pick up" for objects), NOT a pond-special-case prompt; keeps the single-source-of-truth `LootPrompt` path.
- **Decision draft:** With Option A the drink GESTURE drops the crouch-toward-pond dip and becomes a stand-and-sip-from-held-water beat (~0.7–1.0s); the pond RIPPLE cue moves from the drink to the GATHER beat at the pond; the thirst-bar fill feel + `ThirstNeed.AddWater` restore are UNCHANGED from M-U2.
