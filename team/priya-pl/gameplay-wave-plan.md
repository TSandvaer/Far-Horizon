# Gameplay Wave — sequencing / dependency / risk note

Source: Sponsor ticket-prompts 2026-06-17. Four tickets, strict Sponsor-set order. This note is the planning companion to the DECISIONS.md entry "Gameplay wave: settings panel → inventory/belt → chop → stone". It exists to pin the SHARED contracts before any parallel dispatch and to flag the riskiest parts up front.

## The four tickets

| # | Ticket | Title | Owner rec | Reviewer |
|---|--------|-------|-----------|----------|
| 1 | `86caa4bqp` | feat(ui): in-game tweakable settings panel (extensible registry) | Devon | Drew |
| 2 | `86caa4bya` | feat(inventory): inventory (Tab) + belt hotbar + axe/wood PoC items | Devon | Drew |
| 3 | `86caa4c5c` | feat(gameplay): chop trees for wood + tweakable regrowth | Drew (Devon on inventory hook) | Devon |
| 4 | `86caa4c96` | feat(gameplay): pick up small stones + tweakable respawn | Drew (Devon on inventory hook) | Devon |

## Blocked-by chain (hard — this is a serial spine, not a fan-out)

```
settings (1) ──blocks──> inventory (2) ──blocks──> chop (3)
                  └──────────────────────────────> stone (4)
   (1) also blocks (3) and (4) directly — they register settings into it
```

- **(1) settings** blocks everything: (2)/(3)/(4) each REGISTER settings into the panel. Nothing downstream can wire a tweakable until the registry exists.
- **(2) inventory** blocks (3) and (4): chop yields `chopped wood`, stone yields `picked up stones` — both are inventory items DEFINED in (2) as stackable resources. (3)/(4) consume the item model + the inventory-add path.
- **(3) chop** and **(4) stone** are SIBLINGS — independent of each other. Once (1) + (2) are merged, 3 and 4 can run in PARALLEL (different gather verbs, both Drew-led with a Devon inventory hook; no shared edit beyond the inventory API both call).

Consequence: the wave is mostly SERIAL by construction. Only the last pair (chop ∥ stone) parallelizes, and only after the two foundation tickets land.

## SHARED contracts — pin these BEFORE any parallel dispatch (vocabulary-contract surfaces)

Two surfaces are referenced by name across multiple tickets. If two agents ever touch them concurrently (e.g. chop ∥ stone both registering settings + both adding inventory items), they MUST agree on exact identifiers or the PRs are non-mergeable. Pin these in the dispatch briefs (per the parallel-shared-concept vocabulary discipline):

### A. Settings-registry API (owned by ticket 1; consumed by 2/3/4)

Ticket 1 OWNS the registry; it must export a stable registration API the downstream tickets call. The brief for ticket 1 should NAME (not just shape) at minimum:
- The registry/registration entry point (e.g. the exact method name to register a setting + the exact setting-entry type).
- The setting kinds: a float slider, an int, and a min-max RANGE (zoom range + view-angle range are ranges; walk/run/jump/regrowth/respawn are scalars or ranges per the ticket).
- Live-binding: registering binds to a LIVE param (change → game updates immediately, no restart). The downstream tickets register: `belt slots`, `inventory slots`, `inventory stack size` (ticket 2); `tree regrowth time` (range), `tool-use speed` (ticket 3); `stone respawn time` (range) (ticket 4). Use these EXACT setting names across tickets so the panel reads consistently.
- Persistence: session-persisted (PlayerPrefs / settings asset) so soak tweaks survive a relaunch; Sponsor reports dialed values to BAKE as defaults.

### B. Inventory ITEM model (owned by ticket 2; consumed by 3/4)

Ticket 2 OWNS the item model; (3)/(4) define their resource items against it. Pin in ticket 2's brief and reference by name downstream:
- The **tool-vs-resource rule**: tools (axe) → belt-allowed, don't stack; resources (wood, stones) → inventory-only, stack to a cap (default 20, setting-adjustable). This rule is the load-bearing constraint — (3)/(4) MUST honor it (wood + stones are resources → inventory-only).
- The item identifiers: `chopped wood` (defined in ticket 2 as the PoC resource; YIELDED in ticket 3) and `picked up stones` (DEFINED + yielded in ticket 4 — note ticket 2 only defines wood as the PoC item; stones are defined by ticket 4 against the same model). Keep the item-id strings identical between the defining ticket and the gathering ticket.
- The inventory-add path: the exact method chop/stone call to add a stack to the inventory. Name it in ticket 2 so 3/4 call the same one.
- Selected-belt-slot → held-item show/hide wiring into `HeldAxeRig` (the axe shows in-hand only when in the SELECTED belt slot). This is ticket 2's, but chop (ticket 3) depends on "axe is the selected belt item" to gate the chop — so the "is the axe the selected item" query must be a named, callable surface.

## Riskiest parts (call these out in the dispatch briefs)

1. **Inventory drag-drop (ticket 2, AC6)** — drag/move items among inventory slots, move down into a belt slot, with the tool-vs-resource constraint enforced. UI Toolkit drag-drop is fiddly and easy to half-implement; the constraint (wood can't go on the belt) is a correctness rule a naive drag-drop will violate. This is the single most likely AC to need a second round. Budget it as the L-sized chunk of the wave.
2. **Selected-slot show/hide of the held axe (ticket 2, AC4)** — wiring the existing `HeldAxeRig` (axe-follows-arm, Sponsor-approved) to show/hide by selected belt slot, WITHOUT touching the rig internals (the follow-the-arm + finger-curl behavior is Sponsor-locked). Risk = regressing the locked held-axe feel while adding the show/hide gate. The brief must say: gate visibility only; do NOT touch the rig's pose/follow/curl code.
3. **Live-binding correctness in the settings panel (ticket 1, AC2/AC4)** — "change a slider → the game updates immediately, no restart" plus RANGE settings clamping BOTH ends of the live system (zoom min/max, pitch min/max). A registry that doesn't actually drive the live param (only stores a value) silently fails the whole soak-tuning purpose. The AC6 regression test (change value → param changes; range clamps) is the guard — make sure it's a real binding test, not a value-stored test.
4. **World-integrity on chop/stone (tickets 3/4, AC5/AC4)** — must use the EXISTING seed-42 world-gen scatter + the scale-immune grounding; do NOT break the island scatter or NavMesh. Seed 42 is LOCKED. Regrowth/respawn are timer-driven state on existing scatter instances — the risk is breaking the world-gen or NavMesh while adding interactable state to scattered props.

## Recommended dispatch order (once the wave starts)

1. **Dispatch ticket 1 (settings) SOLO first.** It's the foundation + it owns the registry API (contract A). Sequence-first per the vocabulary discipline (Pattern A — the type/API author lands before consumers). Merge it.
2. **Dispatch ticket 2 (inventory) SOLO next.** It owns the item model (contract B) + registers its settings into the now-merged panel. The two riskiest ACs (drag-drop, held-axe show/hide) live here — give it room, don't parallelize it with anything. Merge it.
3. **Dispatch tickets 3 (chop) + 4 (stone) in PARALLEL** once (1)+(2) are merged. They're siblings, both Drew-led with a Devon inventory hook, against the now-settled registry API + item model. Pattern-B vocabulary contract in BOTH briefs: same setting names (`tree regrowth time` / `tool-use speed` / `stone respawn time`), same item-add path, same tool-vs-resource rule. Cross-review check: each PR greps the other's branch for the registry/item identifiers it shares (none should collide — different settings, different items — but verify).

Why this order, not more parallelism: (1) and (2) are the contract OWNERS; dispatching consumers before the owners merge invites vocabulary divergence (the M3-10 `PersonaGroup`/`CollapsedPersonaGroup` non-mergeable trap). Sequencing the two foundations costs two merge cycles but removes the divergence by construction; the only parallel pair (3∥4) shares nothing it can't read from merged main.

## Notes for the orchestrator at dispatch time

- Every ticket is Sponsor-gated + UX-visible → shipped-build capture + Self-Test Report + Tess QA + Sponsor soak before "complete" (per each ticket's success-test).
- Key-binding hygiene (ticket 1, AC1): the settings-panel toggle key must NOT clash with WASD / Shift(run) / Ctrl(crouch, `86caa3kur`) / Tab(inventory, ticket 2). Esc-menu is the natural candidate; pin it in the brief.
- The locomotion wave (run `86ca9yq34` in flight → jump → crouch) runs in a SEPARATE lane (Devon-led, animation/Animator surface) from this gameplay wave's foundation tickets (also Devon-led, UI Toolkit surface). Watch Devon worktree concurrency — do NOT dispatch a locomotion ticket and a gameplay-wave ticket to Devon's worktree at the same time. If both lanes need to move in parallel, the chop/stone pair (Drew-led) is the natural parallel partner to a Devon locomotion ticket.
