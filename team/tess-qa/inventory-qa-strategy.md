# Inventory + Belt + Settings — QA Test-SURFACE Strategy

**Status:** PRE-STAGE (docs-only, pre-stages ticket `86caa4bya`). QA EXECUTION IS NOT AUTHORIZED YET —
the feature is unbuilt. This is an IMPLEMENTATION-AGNOSTIC test-surface matrix: it enumerates WHAT to
verify + the EVIDENCE FORM for each, NOT the exact API (the API does not exist). When the PR opens,
re-read it against the real surfaces (grep HEAD for the actual component/test names) before executing.

**Tickets covered:** `86caa4bya` (inventory/belt + axe & wood PoC) — primary; `86caa4bqp` (tweakable
settings panel) — the BLOCKED-BY foundation that registers belt-slot-count / inventory-slot-count /
stack-size live settings. Reviewer at QA time: **Drew** (game-side). This is Tess-authored → a
Drew/Devon peer review applies to THIS doc if it becomes a spec PR.

---

## 0. THE GOVERNING LESSON — why a static snapshot is the enemy here

Far Horizon's most expensive false-green class is the **static-snapshot / proxy-metric** trap
(`.claude/docs/unity-conventions.md` §Editor-vs-runtime, §FBX/rigs):

- The held-axe FACING fix (`86ca9xz00`, PR #52) passed its static `axe_facing_*` per-facing captures
  while LIVE PLAY failed — the grip-anchor settled to each pinned facing, so a per-facing snapshot was
  green while a mid-turn sweep through headings showed the axe lagging facing by seconds. **Only a
  DYNAMIC multi-frame facing change caught it.**
- The held-axe RATCHET (`86ca9ykp0`, PR #53) only surfaced under a MULTI-STEP walk — a single-frame /
  at-rest sample read "seated"; the cumulative drift integrated over N walk steps.
- The walk-float saga false-greened ~8× on AT-REST metrics that lied during motion.

**Direct consequence for THIS feature:** the AC4 held-item show/hide (and EVERY belt-selection-driven
state) is a STATE TRANSITION binding. A static snapshot of "axe visible while slot 1 selected" proves
nothing about whether SELECTING A DIFFERENT SLOT, or MOVING THE AXE OFF THE BELT, correctly flips it.
Each such surface needs a DYNAMIC SEQUENCE that drives ≥2 state transitions and asserts on EACH.

**The test for every item below:** "Could a wrong implementation pass this check?" If a static/single
snapshot could pass while the feature is broken, the check is a PROXY — escalate it to a dynamic
sequence. The FALSE-GREEN RISK column flags exactly where that bites.

---

## 1. TEST-SURFACE MATRIX — `86caa4bya` (inventory / belt / PoC items)

Legend — **Mode:** STATIC = single-state assertion sufficient · DYNAMIC = multi-step transition sequence
REQUIRED (a static snapshot is false-green-prone). **Evidence:** EM = EditMode test · PM = PlayMode test
· CAP = shipped-build gameplay-cam capture · MAN = manual soak observation.

### S1 — Axe pickup → auto-lands in belt slot 1 (AC3) — tool auto-place
- **Mode:** DYNAMIC (2 states: world-pickable present → picked-up + placed). A static "axe is in slot 1"
  scene snapshot can't distinguish auto-PLACEMENT from a hard-coded pre-seeded slot.
- **Verify:** (a) a pickable axe EXISTS in the world post-spawn; (b) the pickup ACTION (walk-into /
  interact) transfers it; (c) it lands specifically in BELT SLOT 1 (not inventory, not slot 2),
  BECAUSE it is a TOOL; (d) the world axe is consumed (not duplicated — pickup_count>0 ≠ "exactly one,
  in the right place"; cf. the dual-spawn bug class — assert EXACT slot + EXACT count).
- **Evidence:** PM (drive real pickup → assert slot-1 occupancy + world-axe gone) + CAP (belt shows axe
  in slot 1 after pickup).
- **FALSE-GREEN RISK:** a test that asserts only "axe somewhere in belt" or "pickup_count > 0" passes a
  wrong-slot or duplicate-pickup bug. Assert the EXACT slot index AND exactly-one.

### S2 — SELECTED belt slot drives held-item show/hide across ALL slots (AC4) — THE TRICKIEST
- **Mode:** DYNAMIC — MANDATORY MULTI-SLOT SWITCH SEQUENCE. This is the single highest false-green risk
  in the feature.
- **Verify the full transition table, not one cell:**
  - axe in slot 1, slot 1 selected → axe SHOWN in hand;
  - axe in slot 2, slot 1 selected → axe NOT shown; then SELECT slot 2 → axe APPEARS (the transition,
    not the end-state);
  - select an EMPTY selected slot → nothing in hand;
  - move the axe OFF the belt INTO the inventory → axe NOT shown even though it's still "owned" (the
    ticket's explicit case — held-item follows BELT-SELECTED, not mere possession);
  - move it BACK to a belt slot + select it → shown again.
- **Wire-to:** the existing `HeldAxeRig` (the single serialized axe object riding the right-hand bone,
  raw-hand-follow, `86ca9zcjn`). Show/hide will toggle that object's active/renderer state by selected
  slot — confirm the toggle reads the LIVE selection each frame, not a one-shot bind.
- **Evidence:** PM (drive the FULL transition sequence above, assert held-visibility state after EACH
  switch) + CAP MULTI-ANGLE from the DEFAULT GAMEPLAY ORBIT (~14u / 55°, over-shoulder) showing
  slot1-selected=axe-in-hand vs slot2-selected=empty-hand — NOT a flattering hero framing (the
  isolated-verify-cam false-green, §Editor-vs-runtime: a hero shot lies vs the player's actual view).
- **FALSE-GREEN RISK (the worst):** a STATIC snapshot of "slot 1 selected → axe visible" passes while
  slot-SWITCHING is broken, while moving-axe-to-inventory still shows the axe, or while an empty-slot
  selection still shows it. The axe-facing PR #52 false-green is the exact precedent — per-state
  snapshots green, live transitions broken. REQUIRE a sequence that drives ≥3 transitions and asserts
  on each. Also guard the PERCEPT (axe genuinely hidden in the gameplay-cam frame), not just a
  `SetActive(false)` flag — a renderer left enabled under a disabled parent, or a hide that doesn't
  reach the shipped scene, is the §editor-vs-runtime / component-not-serialized class.

### S3 — Drag/move items inventory ↔ belt (AC6)
- **Mode:** DYNAMIC (move is a transition: source slot empties, destination fills, no duplication/loss).
- **Verify:** move among inventory slots; move an item DOWN into a belt slot; pick up + REASSIGN to a
  different belt slot. After each move assert: destination occupied, source cleared, total item count
  invariant (no dupe, no vanish).
- **Evidence:** PM (programmatic move ops + invariants) + MAN (drag-drop feel in soak; UI Toolkit drag
  is pointer-driven — confirm the actual drag gesture, not just a model-level move call).
- **FALSE-GREEN RISK:** asserting only the destination ("item now in belt slot 2") while the source slot
  silently RETAINS a copy (item duplication) — a count-conservation assertion is the catch. Mirrors the
  pickup_count>0 dual-spawn lesson: assert the WHOLE invariant, not the happy half.

### S4 — Tool-vs-resource CONSTRAINT enforced: wood/stone CANNOT go on the belt (AC6)
- **Mode:** DYNAMIC + NEGATIVE-PATH (the rejection is the assertion).
- **Verify:** attempt to move chopped-wood (a RESOURCE) from inventory into a belt slot → REJECTED (item
  stays in inventory; belt slot stays empty). Confirm the axe (a TOOL) IS allowed onto the belt (the
  positive control, so the test isn't trivially passing by rejecting everything). State the rule under
  test: tools → belt-allowed; resources → inventory-only.
- **Evidence:** EM or PM (the constraint is data-logic — a fast headless negative-path test is ideal) +
  MAN (drag a wood stack at the belt in soak → it snaps back / refuses).
- **FALSE-GREEN RISK:** a test that only checks "wood rejected" without the "axe accepted" positive
  control could pass a belt that rejects EVERYTHING (feature inert). Pair negative + positive. Second
  risk: a UI that visually refuses the drag while the underlying model DID accept it (or vice-versa) —
  assert the MODEL state, not just the snap-back animation.

### S5 — Stacking to the cap (20) + stack-count display (AC7)
- **Mode:** DYNAMIC (accumulate across the boundary) + BOUNDARY probe.
- **Verify:** stack a resource up to the cap (default 20); the 21st unit either spills to a new slot or
  is refused per design — assert the boundary behavior explicitly; the displayed stack COUNT matches the
  model at 1, mid, AT-cap, and over-cap; TOOLS (axe) do NOT stack (a 2nd axe never merges into slot 1).
- **Evidence:** EM/PM (accumulate to 19→20→21, assert count + overflow rule) + CAP/MAN (the on-slot
  count badge reads "20" at cap).
- **FALSE-GREEN RISK:** off-by-one at the cap (allows 21, or caps at 19) — a test that stacks to "some
  large number" without asserting the EXACT cap boundary misses it. Test 19/20/21 explicitly. Second
  risk: the display reading a stale count (model says 20, badge says 19) — assert the DISPLAYED value,
  not only the model field.

### S6 — Inventory opens/closes on Tab; belt at bottom; slot grids render (AC1, AC2)
- **Mode:** STATIC for presence + DYNAMIC for the toggle.
- **Verify:** Tab opens the 20-slot inventory grid; Tab again closes it; the belt hotbar is present at
  screen bottom with N slots (default 5); the selected-slot highlight is clearly visible. Tab must NOT
  clash with WASD / Shift(run) / Ctrl(crouch) / the settings key (`86caa4bqp` AC1 reserves a non-Tab key).
- **Evidence:** EM scene-presence (the UI Toolkit document/components are SERIALIZED into Boot.unity —
  the component-in-source-but-not-in-scene silent-inert class needs a scene-presence assert) + CAP
  (Tab-open inventory + belt visible in the shipped exe) + MAN (open/close feel, highlight clarity).
- **FALSE-GREEN RISK:** the inventory MonoBehaviour/UIDocument compiles + passes script tests but is
  never serialized into the shipped scene → ships inert (the `CaptureGate.cs`-not-in-Boot precedent,
  §editor-vs-runtime). An EM scene-presence test is the only authoritative reader (binary scene can't be
  GUID-grepped). Also: editor-only UI that doesn't render in the BUILT exe — CAP from the exe is
  mandatory, editor evidence is never sufficient.

### S7 — Number-keys 1–5 + mouse-scroll slot selection (AC2)
- **Mode:** DYNAMIC (each input drives a selection transition).
- **Verify:** keys 1..N select the matching slot (range FOLLOWS belt-slot-count — key 5 invalid if belt
  has 3 slots); mouse-scroll CYCLES the selected slot (and wraps at the ends per design); the selection
  highlight tracks; and — composing with S2 — the held-item updates as selection moves.
- **Evidence:** PM (inject input → assert selected-index transitions + highlight) + MAN (scroll/keys
  feel in soak). Headless input injection: confirm the input path is drivable headlessly; if it routes
  through the new Input System, a PM test may need the test-input backend.
- **FALSE-GREEN RISK:** key-N selecting a slot that DOESN'T EXIST when belt-slot-count < N (out-of-range
  → null slot, silent no-op or exception). Test with a REDUCED belt count, not just the default 5.
  Scroll-wrap off-by-one at the ends is a second boundary risk.

### S8 — Chopped-wood item + icon defined (AC5)
- **Mode:** STATIC (asset/data presence) + visual MAN.
- **Verify:** the wood inventory item is defined as a stackable RESOURCE with name + icon; the icon is
  in-style (cute/warm low-poly). (Stone item is DEFINED-only here per OOS — assert its item definition
  exists as stackable, gathering is its own ticket.)
- **Evidence:** EM/data (item definition exists, flagged resource+stackable) + CAP/MAN (icon reads
  in-style at UI scale — a tiny icon can read as mush; judge at actual slot size, not zoomed).
- **FALSE-GREEN RISK:** low (data presence) — but the icon "reads in-style" is a SUBJECTIVE call → route
  to Sponsor soak, don't auto-pass on "an icon file exists."

### S9 — AC8 regression-guard PlayMode suite (the Done-clause gate)
- **Mode:** verify the SUITE itself covers the dynamic surfaces above (meta-check on the author's tests).
- **Verify the author's AC8 tests actually drive:** pickup→slot-1; selected-slot→held show/hide
  (MULTI-slot, per the S2 transition table — a single-slot assertion is insufficient); move
  inventory↔belt WITH the tool-vs-resource constraint; stack-to-cap. Confirm each is a SEQUENCE, not a
  pre-seeded static state. Confirm the universal "no console USER WARNING/ERROR" expectation holds
  across the run.
- **FALSE-GREEN RISK:** the AC8 suite green while S2/S3 are tested only at-rest. Tess BOUNCES (REQUEST
  CHANGES) if the selected-slot held-item test is a static per-state snapshot rather than a multi-slot
  switch sequence — that is the exact gap the §FBX axe-facing false-green warns against.

---

## 2. TEST-SURFACE MATRIX — `86caa4bqp` (settings panel) — the BLOCKED-BY foundation

The inventory ticket REGISTERS belt-slot-count / inventory-slot-count / stack-size into THIS panel, so
the settings live-update path is part of the inventory acceptance surface.

### T1 — Settings panel opens on a non-clashing key; extensible registry (AC1, AC2)
- **Mode:** STATIC presence + DYNAMIC toggle. **Verify:** the panel key does NOT clash with WASD /
  Shift / Ctrl / Tab(inventory) (cross-feature input-clash check — explicitly exercise ALL bindings in
  one soak pass). **Evidence:** EM scene-presence + CAP + MAN. **FALSE-GREEN RISK:** an input clash
  only manifests when BOTH features are active — a per-feature test misses it; needs a combined soak.

### T2 — Registered setting BINDS + drives its live param (AC2, AC6) — LIVE, no restart
- **Mode:** DYNAMIC (change value → param changes THIS frame). **Verify:** changing a registered setting
  updates the live gameplay param IMMEDIATELY (walk-speed, zoom range). **Evidence:** EM/PM (AC6 test:
  change value → param changes; range setting CLAMPS) + MAN (Sponsor dials it live in soak).
  **FALSE-GREEN RISK:** a setting that updates the STORED value but doesn't re-apply to the live system
  until restart — assert the LIVE param changed, not just the settings field.

### T3 — belt-slot-count / inventory-slot-count / stack-size live-update the UI (inventory ⇄ settings)
- **Mode:** DYNAMIC — the cross-lane integration surface. **Verify:** raising belt-slot-count adds belt
  slots in the UI LIVE (and the number-key range follows — composes with S7); changing inventory-slots
  reflows the grid; changing stack-size re-caps stacking (composes with S5 — does a stack already at the
  old cap re-clamp or stay?). **Evidence:** PM (change setting → assert UI slot count + the dependent
  behavior) + MAN. **FALSE-GREEN RISK (high, cross-lane):** the setting changes the stored count but the
  belt/inventory UI only reads it at STARTUP → no live reflow (the "live-update" claim is false). A
  static test that sets the count THEN builds the UI passes while a LIVE change is broken — REQUIRE a
  sequence that changes the setting AFTER the UI is built and asserts the UI reflowed. Second risk:
  stack-size lowered below an existing stack's count — assert the overflow/clamp behavior is defined,
  not left to crash.

### T4 — Values persist for the session / across runs (AC5)
- **Mode:** DYNAMIC across a relaunch. **Verify:** dialed values survive a session (and ideally a
  relaunch via PlayerPrefs/settings asset). **Evidence:** MAN (set → relaunch the exe → values retained)
  + EM if a serialization path exists. **FALSE-GREEN RISK:** PlayerPrefs key collision / not flushed →
  "persists" in-session but resets on relaunch. Test the actual RELAUNCH, not just in-session retention.

---

## 3. EVIDENCE-FORM SUMMARY + EXECUTION PRECONDITIONS

- **Shipped-build capture is MANDATORY** (UX-visible): every CAP item uses the DEFAULT GAMEPLAY ORBIT
  (~14u / 55°, over-shoulder), MULTIPLE angles where show/hide is involved — never an isolated hero
  framing (the §editor-vs-runtime false-green-capture class). Editor evidence is necessary, never
  sufficient.
- **Self-Test Report gate:** this is a UX-visible feature → the author MUST post a Self-Test Report
  (build stamp + what was observed) before Tess reviews. Missing Self-Test Report = hard REQUEST CHANGES.
- **Build-stamp freshness:** verify the HUD `BUILD <tag>|<UTC>|<sha>` == PR HEAD before judging any
  capture (the stale-stamp / verify-build-doesn't-refresh-exe trap, §Process notes). Re-run
  `serve_soak.sh`; never reuse an existing `Build/Windows` exe.
- **Cross-worktree checkout:** if Drew/Devon hold the inventory branch in their worktree, QA it by
  DETACHED SHA (`git checkout <sha>`) or `origin/<branch>`, never `git checkout <branch>` (the
  branch-locked-to-one-worktree silent-detach trap, §Process notes).
- **PlayMode headless caveats:** UI/inventory logic is NOT Animator-driven, so the `Time.deltaTime≈0`
  anim false-green is lower risk HERE — BUT if any held-item visibility rides an Animator/clip pose,
  the §FBX "PlayMode anim test is a false-green (Animator never ticks headless)" rule re-applies; guard
  the clip-relative state in EditMode via `SampleAnimation`. Also: `WaitForEndOfFrame` does NOT fire in
  `-batchmode` — sample post-LateUpdate state via the prior-frame-recorded-value pattern, never gate on
  end-of-frame.
- **Sample-size discipline (N≥8):** any "deterministic" / "always selects the right slot" claim on the
  selection or stacking sweeps needs N≥8 runs, not N=3.

---

## 4. TOP FALSE-GREEN RISKS (ranked — the orchestrator-bound summary)

1. **S2 selected-slot held-item show/hide** — a static per-state snapshot passes while slot-SWITCHING,
   move-to-inventory-hides, and empty-slot-hides are broken. EXACT precedent: held-axe facing PR #52.
   REQUIRES a multi-transition dynamic sequence + guard the gameplay-cam PERCEPT, not a `SetActive` flag.
2. **T3 belt/inventory/stack-size LIVE-update** (cross-lane) — UI reads the count only at startup →
   "live-update" is false; a set-then-build test passes while a live change is broken. REQUIRES
   change-AFTER-build sequence.
3. **S1 pickup → EXACT slot-1 + exactly-one** — "pickup_count>0" / "axe somewhere in belt" passes a
   wrong-slot or duplicate-pickup bug (the dual-spawn lesson).
4. **S6 / S1 UI serialized-into-Boot.unity** — compiles + script-tests green but never in the shipped
   scene → ships inert (the CaptureGate-not-in-Boot class). EM scene-presence assert is the only reader.
5. **S4 tool-vs-resource constraint** — negative-only test passes a belt that rejects everything; pair
   with the axe-accepted positive control; assert MODEL state not the snap-back animation.
6. **S5 stack cap off-by-one** — test 19/20/21 explicitly; assert the DISPLAYED count not only the model.
