# Crafting Chain End-to-End Soak Plan — ticket `86camz9vq` (④)

**Owner:** Tess (QA) · **Reviewer:** Uma (orch-routed) · **Design source (AUTHORITATIVE):**
`team/priya-pl/crafting-system-spec.md` §9-④ @ main · **Gate class:** `sponsor-gate` (the chain soak IS the gate).

> **What this doc is:** the sponsor-facing checklist for ONE shipped build where the Sponsor plays the whole
> crafting chain end to end — hand-gather → place table → WOOD tools → (② boulders → STONE tools) → (③ forge →
> IRON tools) — plus a 3-tier in-hand A/B and a 3-tier difficulty A/B. It is written NOW (before ②/③ land) so the
> soak dispatches instantly once ③ is on main. Phases D/E are pre-authored with `[SLOTS IN WHEN ② LANDS]` /
> `[SLOTS IN WHEN ③ LANDS]` markers; nothing else changes when they arrive.

---

## 0. Landed-vs-pending map (fill at staging; do NOT judge a step whose feature is not in the served build)

| Piece | Ticket | State (verify at staging) | Judged in this soak? |
|---|---|---|---|
| ① Crafting-table place-to-build + recipe menu + material-cost seam + WOOD tier | `86camz9vq`'s dep ① | LANDED — PR #294 (squash `a922bcf`, merged 2026-07-18) | YES (Phases B, C) |
| ① follow-up: placement ghost reads RED on OBJECT overlap | `86catqxm0` | in progress @ authoring | ONLY if merged into the served build — else FLAG NOT-JUDGED (§8) |
| ② Boulder-mining (wood-pick → stone) + STONE tier | `86camz9v7` | pending (build-lane, serialize) | Phase D — **judge ONLY if on main in the served build** |
| ③ Forge place-to-build rework + IRON tier | `86camz…` (③ id confirm at staging) | pending (deps ①+②) | Phase E — **judge ONLY if on main in the served build** |
| 3-tier difficulty A/B (ore-rarity × smelt-cost presets) | ④ (this ticket) | bakes ②/③ dialed values | Phase F — needs ②+③ live |

**Hard rule (`[[sponsor-rejects-unsoakable-placeholders]]`):** every step marked "judged" below MUST be FINDABLE +
LIVE-TRIGGERABLE in the served exe. Any step whose feature is not in the build gets an explicit **NOT-JUDGED**
flag in §8 — agreed with the orchestrator BEFORE the build is served, never discovered mid-soak.

---

## 1. Pre-serve gate — Tess owns this BEFORE the build reaches the Sponsor

Do not serve until every box is checked. These are the recurring soak-failure classes this project has paid for.

- [ ] **PLAY the exe at real gameplay framing** end-to-end myself first (`[[served-unverified-soaks-need-played-verification]]` — 3 unplayed serves all failed). Not a capture-only pass; actually walk the chain.
- [ ] **Consolidated VerifyCaptures all GREEN from the BUILT exe** (not editor): table (`CraftingTable`/placement), boulder (② `StonePile`/mine), forge (③ place-to-build + smelt glow), wood/stone/iron in-hand picker. Quote each PASS line + build stamp.
- [ ] **Side-profile (silhouette) captures** for every Bar-4 physical feature the build contains — table, boulder, forge — eyeballed against its real-world anchor sentence (§2). A metric can be green on nonsense (`-verifyPond` passed on a raised mound; `[[physical-features-anchor-realworld-not-metric]]`).
- [ ] **Descope agreement locked** — §8 NOT-JUDGED register filled + confirmed with orchestrator.
- [ ] **Predict-Before-Soak line + bounded-convergence claim written** (§6/§7) — author fills BEFORE serving; graded AFTER.
- [ ] **Stamp + exe path filled** (§9 placeholder) and the HUD stamp on the served build matches (merge-ref sha, NOT branch HEAD — `[[soak-build-stamp-is-merge-ref-not-headsha]]`).
- [ ] **Serve from `Build\soak-<N>\`** (`[[soak-builds-go-in-project-build-folder]]`), never the scratchpad.

---

## 2. Real-world anchor sentences (Bar 4 — the human-eye read the metric can't do)

Before judging shape, state what each thing IS in one plain sentence; the side-profile capture is eyeballed against it.

- **Crafting table:** "A waist-high workbench a person walks up to and builds tools on — it reads as a TABLE, not a crate or a stump."
- **Boulder (②):** "A rock outcrop big enough to mine repeatedly — it reads as a real boulder/rock, not a pebble and not a cube."
- **Forge (③):** "A stone furnace hot enough to smelt iron — it reads as a FURNACE (mass + a fire mouth), and it GLOWS while smelting."

Human-eye question at each: *"Would a person standing here call this a <table / boulder / forge>?"* — sits BESIDE the byte/seed/metric checks, not instead of them.

---

## 3. Input-key reference card (grounded in the shipped source — no omitted keys)

> **Why this card leads:** an omitted key failed us twice (Sponsor-flagged process fix). Every step below names
> its key(s); this card is the single source. Keys verified against `Assets/Scripts` (not assumed).

### Movement & camera
| Key | Action | Source |
|---|---|---|
| `W` `A` `S` `D` | Walk (arrow-free; arrows are NOT bound to movement) | `WasdMovement.cs` |
| `Shift` (Left/Right, hold) | Run / sprint | `WasdMovement.cs` (86ca9yq34) |
| `Space` | Jump | `CastawayCharacter.TryJump` (86ca9yq3q) |
| `Ctrl` (Left/Right, hold) | Crouch | `WasdMovement.cs` (86caa3kur) |
| `RMB` (hold + drag) | Orbit camera (yaw + pitch) | `OrbitCamera.cs` |
| Mouse **scroll wheel** | Zoom camera distance — **when NOT in placement mode** | `OrbitCamera.cs` |

### Interaction verbs
| Key | Action | Source |
|---|---|---|
| `E` | Universal loot / pickup **and** eat-berry (one key, Danish-safe) | `MovementCameraScene` lootKey / `EatBerryAction` (DECISIONS 2026-06-27) |
| `LMB` (left-click) | Active strike — **chop / mine / attack** (one click = one strike); also **drink at pond**; also **confirm placement** | `ChopTree` / `MineOre` / `MeleeAttack` / `DrinkAction` (86caf7a30) / `CraftingTablePlacement` |
| `Q` | **DISABLED** — legacy proximity-drink, removed 86caf7a30. Do NOT list it as a live drink key. | `DrinkAction.cs` |

### Crafting table & place-to-build (① — `CraftingTablePlacement` / `CraftingMenuUI`)
| Key / trigger | Action | Source |
|---|---|---|
| `C` | Enter PLACEMENT mode (ghost appears) | `CraftingTablePlacement.buildKey` |
| Mouse **scroll wheel** (in placement) | Rotate the ghost (one notch per scroll, sign of delta) | `CraftingTablePlacement.ApplyRotation` |
| `LMB` (in placement) | Confirm placement — debits materials all-or-nothing | `CraftingTablePlacement.TryConfirm` |
| `Escape` | Cancel placement (no debit) **and** close the recipe menu | `CraftingTablePlacement.cancelKey` / `CraftingMenuUI.closeKey` |
| Walk near a placed table (proximity) | Opens the recipe MENU (auto-handoff right after confirm) | `CraftingMenuUI.Open` |
| `LMB` on a recipe row | Craft it (click-to-craft, NO drag); `UiInputGate` swallows world verbs while the menu is up | `CraftingMenuUI` |

### Inventory / belt
| Key | Action | Source |
|---|---|---|
| `Tab` | Open / close the inventory Pack | `InventoryUI.toggleKey` |
| `1` `2` `3` `4` `5` | Select belt slot N | `InventoryUI` (`Alpha1 + i`) |

### Dev / settings — NOT part of the judged chain (listed only so the Sponsor doesn't mistake them for chain steps)
`F1` player Settings · `F3` dev console · `F7/F8/F9/F10` camera/float/axe-nudge + overlay tools ·
`B` held-weapon cycle (dev look aid) · `O` / `I` held-weapon scale up/down (Danish-safe).

---

## 4. The chain checklist — play in order; verdict each step

**Legend per step:** **Input** = keys · **Expect** = the observable · **Bar** = quality bar(s) exercised ·
**Live?** = LIVE-TRIGGERABLE in this build (YES) or NOT-JUDGED (→ §8) · **Verdict** = PASS / FAIL / N-A.

### Phase 0 — Boot + stamp
- **0.1 Boot to gameplay.** Input: launch exe. Expect: castaway on the start island, three-bar need HUD visible, HUD build stamp reads `BUILD <tag> | <UTC> | <sha>` matching §9. Bar: —. Live: YES. Verdict: __
- **0.2 Move + camera sanity.** Input: `W A S D`, `Shift` (run), `Space` (jump), `RMB`-drag (orbit), scroll (zoom). Expect: responsive locomotion + orbit + zoom; small-player/big-world read. Bar: 2. Live: YES. Verdict: __

### Phase A — Hand-gather bootstrap (wood + stone)
- **A.1 Gather sticks → wood.** Input: walk (`WASD`) to a fallen stick, press `E`. Expect: `+1 wood`; stick consumed (no respawn). Bar: —. Live: YES. Verdict: __
- **A.2 Gather pebbles → stone.** Input: walk to a loose pebble, press `E`. Expect: `+1 stone`; pebble consumed (respawns later). Bar: —. Live: YES. Verdict: __
- **A.3 Enough to bootstrap.** Input: repeat A.1/A.2. Expect: reach ≥5 wood + 3 stone (table cost) with hand-gather ALONE — no pickaxe needed yet (bootstrap coherence, spec §4). Bar: 7 (does the easy tier bootstrap without grind?). Live: YES. Verdict: __

### Phase B — Place the crafting table (place-to-build, invisible-until-placed)
- **B.1 Enter placement.** Input: `C`. Expect: a translucent GHOST table tracks the ground in front of the player; nothing was pre-visible before this. Bar: 4. Live: YES. Verdict: __
- **B.2 Rotate + read validity.** Input: scroll wheel (rotate ghost); move to vary ground. Expect: ghost rotates in notches; tint reads GREEN + `[OK]` on clear flat ground, RED + `[X]` cue with reason on invalid (steep / water / player-overlap). Bar: 4. Live: YES. Verdict: __
- **B.3 GHOST-OBSTRUCTION PROBE (see §5).** Input: aim ghost at a **tree or rock**. Expect: ghost reads RED + `[X] BLOCKED — overlaps object`; `LMB` refuses to place. Bar: 4. Live: **YES only if `86catqxm0` merged into this build — else NOT-JUDGED (§8)**. Verdict: __
- **B.4 Cancel path.** Input: `Escape` while in placement. Expect: placement exits, NO materials debited. Bar: —. Live: YES. Verdict: __
- **B.5 Confirm placement.** Input: re-enter (`C`), aim at clear flat ground, `LMB`. Expect: table instantiates at the ghost pose; 5 wood + 3 stone debited all-or-nothing; if short → no table, no debit. Bar: 4. Live: YES. Verdict: __
- **B.6 Table reads as a table.** Input: `RMB`-orbit around it. Expect: it looks like a real crafting table (anchor §2), side-on too. Bar: 4. Live: YES. Verdict: __

### Phase C — Craft the WOOD tier at the table
- **C.1 Menu opens on proximity.** Input: walk (`WASD`) up to the placed table. Expect: the recipe MENU opens; three tier sections (WOOD / STONE / IRON) visible. Bar: —. Live: YES. Verdict: __
- **C.2 Tier gating reads.** Input: read the menu. Expect: WOOD rows active (unlocked on table-placed); STONE + IRON rows greyed as **Locked** with an unlock hint (the ladder is visible). Bar: —. Live: YES. Verdict: __
- **C.3 Affordability read.** Input: read a WOOD row you can't afford yet. Expect: greyed with the cost + what's missing (distinct from Locked). Bar: —. Live: YES. Verdict: __
- **C.4 Craft a wood pickaxe (click-to-craft).** Input: `LMB` on the WOOD pickaxe row (needs 3 wood default). Expect: 3 wood debited; `pickaxe_wood` granted to the belt; NO free mint. Bar: 3, 5. Live: YES. Verdict: __
- **C.5 Menu click does NOT leak to a world verb.** Input: `LMB` on the menu. Expect: no chop/mine fires behind the panel (`UiInputGate` swallow). Bar: —. Live: YES. Verdict: __
- **C.6 Close + select from belt.** Input: `Escape` (close menu), then `1`–`5` to select the wood pickaxe; optional `Tab` to view the Pack. Expect: wood tool appears IN-HAND at the right seat/size. Bar: 3, 5. Live: YES. Verdict: __
- **C.7 Regression — the old free stump is GONE.** Input: look where `CraftSpot` used to be. Expect: no auto-craft stump; the only craft path is the table. Bar: —. Live: YES. Verdict: __

### Phase D — Boulder-mining → STONE tier  `[SLOTS IN WHEN ② (86camz9v7) LANDS]`
- **D.1 Select the wood pickaxe.** Input: `1`–`5` to select `pickaxe_wood`. Expect: wood pickaxe in-hand. Bar: 5. Live: **YES iff ② on main — else NOT-JUDGED**. Verdict: __
- **D.2 Mine a boulder (active click).** Input: face a boulder in range, `LMB` (hold to repeat). Expect: ~4 strikes (default) with a lively swing → boulder yields a **stone pile**; a wrong tool selected does NOT mine (tool-gated). Bar: 2, 3. Live: iff ②. Verdict: __
- **D.3 Loot the stone.** Input: press `E` on the stone pile. Expect: `+stone` (volume source beyond pebbles). Bar: —. Live: iff ②. Verdict: __
- **D.4 Boulder reads as rock + organic placement.** Input: `RMB`-orbit; scan the field. Expect: boulder looks like a real rock outcrop (anchor §2, side-profile); boulders placed organically, no grid. Bar: 4, 1. Live: iff ②. Verdict: __
- **D.5 Craft the STONE tier.** Input: return to table, `LMB` on a STONE row (e.g. stone axe 3 wood + 3 stone). Expect: STONE rows now LIVE (unlocked on first-wood-pickaxe-owned); debits + grants; stone tool reads as knapped stone. Bar: 3, 5. Live: iff ②. Verdict: __
- **D.6 Regression — iron-ore mining unaffected.** Input: (if reachable) confirm the shipped `MineOre` #287 still mines ore. Expect: unchanged. Bar: —. Live: iff ②. Verdict: __

### Phase E — Forge place-to-build + smelt → IRON tier  `[SLOTS IN WHEN ③ LANDS]`
- **E.1 Gather the heavy stone cost.** Input: mine boulders (`LMB`) + gather. Expect: reach forge cost (default 6 wood + 12 stone) — the forge dwarfs a weapon, so iron feels earned. Bar: 7. Live: **YES iff ③ on main — else NOT-JUDGED**. Verdict: __
- **E.2 Place the forge (invisible until placed).** Input: `C` → ghost → scroll (rotate) → `LMB` (confirm) / `Escape` (cancel). Expect: nothing was pre-visible; forge instantiates on confirm; stone debited all-or-nothing. Bar: 4. Live: iff ③. Verdict: __
- **E.3 Forge reads as a stone furnace.** Input: `RMB`-orbit + side-profile. Expect: mass + fire-mouth, anchor §2. Bar: 4. Live: iff ③. Verdict: __
- **E.4 Mine iron ore (stone pickaxe).** Input: select `pickaxe_stone` (`1`–`5`), `LMB` on an ore node. Expect: ore drops + loots via `E`. Bar: 2, 3. Live: iff ③ (+② for the stone pick). Verdict: __
- **E.5 Smelt ore → ingots.** Input: bring ore near the built forge. Expect: auto-tend smelt (shipped #292) runs ore + fuel → iron bars over the timer; the forge GLOWS while smelting. Bar: 2. Live: iff ③. Verdict: __
- **E.6 Craft + equip an IRON tool at the TABLE.** Input: return to table, `LMB` on an IRON row (e.g. iron axe 2 wood + 3 ingot); then `1`–`5` to equip. Expect: ingots debited all-or-nothing; iron FBX (#254/#283) wielded; reads as forged iron in-hand. Bar: 3, 5. Live: iff ③. Verdict: __
- **E.7 Regression — the shipped smelt timer stays correct.** Input: observe smelt duration. Expect: unchanged from #292. Bar: —. Live: iff ③. Verdict: __

### Phase F — 3-tier reads (in-hand A/B + difficulty A/B)  `[NEEDS ②+③ LIVE]`
- **F.1 In-hand 3-tier A/B (Bar 5).** Input: equip wood → stone → iron of the SAME tool via `1`–`5` (discrete picker, never a bare render/broken dial — `[[verify-soak-builds-or-bake-and-judge]]`). Expect: each tier reads as its material + a coherent size progression in-hand. Bar: 3, 5. Live: iff ②+③. Verdict: __
- **F.2 Difficulty 3-tier A/B (Bar 7).** Input: run the chain under each preset (easy / medium / hard) of ore-rarity × smelt-cost. Expect: easy = kid-friendly (ore common, smelt cheap); hard = adult-challenging (ore rare, smelt costly); medium between. Bar: 7. Live: iff the presets are dial-selectable in the served build — **else NOT-JUDGED (§8)** and flag as bake-and-judge. Verdict: __

---

## 5. Ghost-obstruction probe (dedicated — ticket `86catqxm0`)

**Origin:** Sponsor re-soak on PR #294 (stamp `c54b1c8`), verbatim: *"soak passed but the ghost table should be red when colliding with other objects (now it only collides with the player)."*

**The probe (add to the checklist as B.3):**
1. Input: `C` to enter placement.
2. Input: `WASD` / scroll to aim the ghost so its footprint overlaps a **tree, rock, prop, water, or a placed structure**.
3. **Expect:** ghost reads **RED + `[X] BLOCKED — overlaps object`** (non-color cue present for colorblind-safety); `LMB` REFUSES to place (no clipped table).
4. Input: move to clear flat ground. **Expect:** ghost flips **GREEN + `[OK]`**; `LMB` places.

**Live-triggerable gate:** this probe is judged ONLY if `86catqxm0` is merged into the served build. If it is
NOT in the build, do NOT stage a build claiming the fix — flag it NOT-JUDGED (§8) and note "player-overlap only"
is the current behavior. (Do not let a green metric on the OLD player-only check masquerade as this fix.)

---

## 6. Predict-Before-Soak — line templates (author fills BEFORE serving; grade AFTER)

Per `team/TESTING_BAR.md` §Predict-Before-Soak. One falsifiable prediction per judged surface; concrete + observable.

- **Chain completeness:** *"I expect the Sponsor to complete gather → table → wood → stone → iron in ONE build with no dead step; I expect NO placeholder gap (every judged AC live-triggerable). I expect NO `USER WARNING:`/`USER ERROR:` and NO item-id resolution failure in the console."*
- **Placement (Bar 4):** *"I expect the table/forge to be invisible before placement and to read as a real table/furnace side-on; I expect the ghost to read RED over a tree/rock and refuse placement (86catqxm0)."*
- **Material honesty (Bar 3):** *"I expect wood tools to read as sharpened wood, stone as knapped stone, iron as forged iron — no arbitrary colors; I expect NO detail-texture cheat (pattern is modeled facets)."*
- **In-hand (Bar 5):** *"I expect the wood→stone→iron picker to show a coherent material + size progression in-hand; I expect NO broken continuous dial and NO bare render."*
- **Difficulty (Bar 7):** *"I expect easy/med/hard to visibly change ore-rarity + smelt-cost end-to-end; I expect easy to be completable without grind and hard to be a real gate."*
- **Motion (Bar 2):** *"I expect the mine/chop swing to be lively (follows the arm) and the working forge to glow while smelting; I expect NO static-locked verb."*

**Outcome vs prediction (post-soak, fill in):** __ (A refuted prediction is a FINDING — STOP and deep-investigate WHY the foundation was wrong before re-fixing, per `[[claim-removed-soak-shows-present-investigate-foundation]]`; route to deep-investigation, do not blind re-bounce.)

---

## 7. Bounded-convergence claim (name the bars tested + the bars NOT tested)

Per spec §9-④: bars **1, 2, 3, 4, 5, 7 tested**; name what is NOT.

> **Tested this soak:** Bar 1 (organic boulder placement, no grid — Phase D), Bar 2 (lively mine/chop swing +
> forge glow — D/E), Bar 3 (wood/stone/iron read as material — C/D/E/F), Bar 4 (table/boulder/forge read as the
> real thing + side-profiles — B/D/E), Bar 5 (3-tier in-hand via picker — F.1), Bar 7 (3 difficulty tiers — F.2).
>
> **NOT tested this soak:** Bar 6 (art-board-is-a-guide — not a pass/fail gate; a praised divergence is not a
> defect), Bar 8 (direct-tweak instrument — fires only on a stall, not a standing check). Also out of chain
> scope: combat balance, durability, find-in-world acquisition, distinct ore-vs-ingot ICONS (`86camyvwn`).

*(An unbounded "the crafting chain looks done" is NOT a convergence claim — it is the form most often overturned
by the next soak testing a different bar. Name the bar.)*

---

## 8. Deferred / NOT-JUDGED register (agree with orchestrator BEFORE the build; fill at staging)

List every judged step whose feature is not in the served build — so nothing is silently untested and no
placeholder is served as if live (`[[sponsor-rejects-unsoakable-placeholders]]`).

| Step | Reason not live | Disposition |
|---|---|---|
| B.3 ghost-obstruction | `86catqxm0` not merged | NOT-JUDGED — note "player-overlap only" |
| Phase D (all) | ② `86camz9v7` not on main | NOT-JUDGED until ② lands |
| Phase E (all) | ③ not on main | NOT-JUDGED until ③ lands |
| F.2 difficulty A/B | presets not dial-selectable | NOT-JUDGED / bake-and-judge |
| … | … | … |

---

## 9. Serve artifact — filled at staging (do NOT serve with blanks)

- **Exe path:** `Build\soak-<N>\FarHorizon.exe`  ← fill `<N>`
- **Expected HUD build stamp:** `BUILD <tag> | <UTC> | <merge-ref sha>`  ← fill from the merged PR merge-ref (NOT branch HEAD; `[[soak-build-stamp-is-merge-ref-not-headsha]]`)
- **Provenance:** confirm the served build is THIS chain build (check the stamp on the HUD before judging — `[[soak-handoff-path-and-explicit-test-checklist]]`).
- **Test-THIS one-liner for the Sponsor:** "Play gather → place table (`C`, scroll to rotate, `LMB` to confirm) → craft wood tools → mine boulders (`LMB`) → craft stone → place forge → smelt → craft + equip iron; then compare the three difficulty presets."

---

## 10. Journey-probe overlay (mandatory before serving — testing-bar RC-boundary)

Run ONE complete journey myself and log to `team/tess-qa/journey-probe-<date>.md`. Blockers (any = do not serve):
- Any console `USER WARNING:` / `USER ERROR:`.
- Any item-id resolution failure (wood/stone/iron ids must resolve in both `ItemCatalog` + `WeaponCatalog`).
- Any missing / uncollectable loot in the chain (stick, pebble, stone pile, ore pile, ingot, crafted tool).
