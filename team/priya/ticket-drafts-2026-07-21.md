# Ticket drafts — MCP backfill 2026-07-21

> **PROMOTED to ClickUp 2026-07-22** (MCP restored): a→`86cav8xg9` · b→`86cav8xu8` (+ Devon r7 NITs 1-3 folded) · c→`86cav8y1u` · d→`86cav8y74` · e→`86cav8ybj` (+ Devon #328 Sponsor-confirm-direction note) · f (NEW — Tess r7 soak spot-checks, not one of these 5 drafts)→`86cav8yjv`. Boar `86cah7ydt` full ACs fleshed the same pass. **This file is now HISTORY — the ClickUp tickets are authoritative.**

ClickUp MCP was DOWN when these were authored (2026-07-21). Each section below is paste-ready:
title (conventional-commit) · source · priority suggestion · description · acceptance criteria
(Commander's-Intent 3-bucket: 🎯 Destination / 🔒 Constraints / 🎚️ Defaults) · out-of-scope · cross-refs.

All file:line refs and quotes are grounded in the cited PR #327 review comments (read 2026-07-21) —
they are the reviewers' OBSERVED values, not extrapolated. Parent work: `86caffwv5` (soak-swings round 5).

ClickUp priority scale: 1 = Urgent, 2 = High, 3 = Normal, 4 = Low.

---

## a. `fix(anim): re-source or bpy-repair the contorted pickaxe_mine clip on the mixamorig skeleton`

**Source.** Drew round-5 PR #327 comment `5032217364` (item 3): *"the `pickaxe_mine` clip itself
poses the body contorted (a CLIP-quality issue, not the seat) → the 'contorted pickaxe body' is a
follow-up (clip re-source), OOS here."* Evidence: Sponsor soak-5 screenshots + that comment.
Route guidance: Erik's `team/erik-consult/rigify-vs-mixamo-research.md` §Application (recommendation 2)
— the pain is a bad CLIP not a bad RIG; fix in the clip layer.

**Priority suggestion.** 2 (High) — a live soak-5 defect that blocks the pickaxe mine verb's feel.

**Description.** The `pickaxe_mine` clip poses the castaway's body contorted mid-mine (Sponsor soak-5).
Per Drew's round-5 analysis the seat is hand-relative and mathematically invariant across clip poses,
so this is NOT a seat-space bug — it is the clip itself. Per Erik's Rigify-vs-Mixamo evaluation, the
fix stays in the clip layer on the existing Mixamo Generic `mixamorig:*` skeleton; a rig swap is
rejected (produces no clips, exports poorly to engines, and breaks the whole Generic wiring).

**Acceptance criteria.**
- 🎯 **Destination.** The pickaxe mine action plays with a natural, upright body pose — no contorted
  torso/limbs mid-swing — when the player mines a boulder with a pickaxe in the SHIPPED exe. *Why:*
  the current clip reads as broken and blocks the mine verb's feel (Sponsor soak-5).
- 🔒 **Constraint — clip layer only, rig untouched.** Keep the Mixamo Generic 41-bone `mixamorig:*`
  skeleton unchanged. *Why:* per `rigify-vs-mixamo-research.md`, any rig swap breaks the Generic
  clip-bind + Animator states + `HeldAxeRig`/`CastawayArmPose` bone-name lookups + every dialed seat.
- 🔒 **Constraint — two-route order.** Route 1 (preferred): re-source a cleaner Mixamo mine/swing clip.
  Route 2 (fallback if re-source fails): repair the corrupted bone curves on the `mixamorig` skeleton
  in Blender via headless `bpy` — the **`SneakGaitCurveFix` slerp-resample pattern**
  (`Assets/Scripts/Editor/SneakGaitCurveFix.cs`; see `procedural-animation-verbs.md`) moved to
  Blender — then re-export **Without-Skin** FBX, bind by transform path. *Why:* keeps the fix
  in-house, free, `bpy`-scriptable, and low-blast-radius; a contorted clip is the same defect class
  (bad curve on a good skeleton) the sneak-gait jerk was.
- 🔒 **Constraint — no downstream churn.** Do NOT re-wire the Animator, re-seat props, or touch the
  `HeldAxeRig`/`CastawayArmPose` chain; the repaired/re-sourced clip's `.fbx.meta` stays
  `animationType: 2` (Generic). *Why:* Generic binds by transform path/name; a Humanoid flip explodes
  under the scaled scene hierarchy.
- 🎚️ **Predict-Before-Soak (feel-gated).** Author states a falsifiable prediction that the mine pose
  reads upright/natural at the next soak (bar: mine-pose body posture; NOT tested: mine cadence feel,
  seat dial-in). Graded at the Sponsor soak.

**Out of scope.** The pickaxe SEAT re-dial (F9 gimbal hunt kept unsatisfied at `(8,10,0)` — separate
follow-up); the wood→ore spec decision; ANY rig swap (Rigify evaluated + rejected 2026-07-21).

**Cross-refs.** Parent `86caffwv5`; Drew comment `5032217364`; `team/erik-consult/rigify-vs-mixamo-research.md`;
`Assets/Scripts/Editor/SneakGaitCurveFix.cs`; `procedural-animation-verbs.md`.

---

## b. `chore(inventory): future-4th-tier fall-through hardening + arbitration NITs follow-up (86caffwv5 r4+r5)`

**Source.** Devon PR #327 comments `5031488132` (r4 NITs 1, 2, 4) + `5032563742` (r5 NITs 1, 2, 3).
Mechanical — every sub-item's scope is derivable from the reviewer comment text; no new scope added.

**Priority suggestion.** 3 (Normal) — hardening + coverage, no live defect (three tiers is today's universe).

**Description.** Round 4/5 landed the wood/iron chop-gate widening + stateless verb-vs-whiff arbitration.
Six mechanical follow-ups from the two Devon reviews: close the future-tier fall-through class by
construction, close the diagnostic's silent-drift/precedence gaps, cover the F9 non-axe round-trip, and
confirm the held-visual grip sibling.

**Acceptance criteria.**
- 🎯 **Destination.** The enumerated-tier fall-through class is structurally closed and the diagnostic
  can't silently drift or misorder — a future 4th axe/pickaxe tier is covered by construction, and the
  ClickGateDiag has equivalence + precedence guards. *Why:* the soak-4 regression (wood axe fell through
  the hand-enumerated gate) is patched-not-closed; the same class re-opens on the next tier, and a
  trust-critical instrument that re-implements the resolver can LIE if the resolver metric changes.
- 🔒 **Constraint — WeaponClass-derive the category.** Replace the hand-enumerated tier-id lists at
  `Inventory.cs:184` (`IsAnyAxeSelectedInBelt`) and `MineBoulder.cs:428` (`IsBoulderPickaxeSelected`)
  with a `WeaponClass`-derived category (`WeaponCatalog.WeaponClassAxe` exists); the guard test
  `InventoryFacadeTests.cs:108` must assert category-derivation, not the 3 hardcoded ids. *Why:*
  hand-enumeration is the exact soak-4 root pattern; the hardcoded guard would not red on a new-tier
  omission.
- 🔒 **Constraint — arbitration contract preserved.** Additive coverage only; keep
  `WouldClaimClick`/`VerbClaimsClick`/`ShouldSwingOnClick` stateless + execution-order-independent.
  *Why:* round-4/5 established it; a regression re-opens the double-consumer / whiff gap.
- 🔒 **Constraint — instrument guards.** Add (i) an accessor-vs-resolver equivalence guard at the
  radius boundary — `VerbGateDiag.TargetInRange == (ResolveNearest…() != null)` — against
  `ChopTree.cs:742` / `MineBoulder.cs:458` / `MineOre.cs:462`; (ii) a `ClassifyClick` precedence
  cross-check pinning the chop→boulder→ore order to the real `MeleeAttack.VerbClaimsClick`
  (`ClickGateDiagnostic.cs:105-115` currently asserts against itself). *Why:* the diagnostic is
  instrument-of-record; silent drift is the `soak-fail-test-pass-instrument-runtime` trap class.
- 🔒 **Constraint — F9 non-axe round-trip test.** Add a "post-nudge `CurrentEuler` reproduces the
  composed rotation" assert for the `NudgeCurrentWeapon(dp, targetEuler − CurrentEuler)` subtract-then-`+=`
  path (exact today via unclamped `+= dr`, but uncovered). *Why:* closes the r5 NIT-3 coverage hole.
- 🔒 **Constraint — held-visual grip, not the chop gate.** Confirm the wood/iron held-visual path
  engages the finger-curl (`CastawayFingerCurl.cs:107` + `HeldWeaponCycleDebug.cs:549,590` still read
  the stone-only `IsAxeSelectedInBelt`), or widen the held-visual predicate — do NOT touch the chop
  gate. *Why:* cosmetic sibling; widening the gameplay gate is out of this ticket's lane.
- 🔒 **Constraint — disabled-verb dead-click guard.** Guard the silent-fail edge where a present-but-
  DISABLED verb component's `WouldClaimClick()` returns true and suppresses the whiff while never
  chopping. *Why:* ~nil risk in the shipped scene, but it's the exact silent-fail class this round
  guards against (Devon r4 NIT 4).
- 🎚️ **Defaults.** None — mechanical hardening + tests.

**Out of scope.** Widening the wood pickaxe to ORE (`MineOre.IsPickaxeSelected` stays stone/iron-only —
separate Sponsor decision per Drew's r5 ADVISEMENT); any gameplay-behavior change; the ClickGateDiag
REGISTRY entry + keep/strip decision (→ ticket c).

**Cross-refs.** Parent `86caffwv5`; comments `5031488132` + `5032563742`; ticket c (registry);
`weapon-two-tier-style-stone-iron` (now three-tier) memory.

---

## c. `chore(debug): register [ClickGateDiag] in tools/debug/REGISTRY.md + removal-tracking decision`

**Source.** Devon PR #327 comment `5032563742` (r5 NIT 4): *"(a) no `tools/debug/REGISTRY.md` entry
exists yet (add one so future sessions find it), and (b) track gating/removal once the live mine
failure is diagnosed+fixed."* Note: `ClickGateDiagnostic.cs` is in the PR #327 delta, NOT yet on
`main` — this ticket backfills once #327 merges.

**Priority suggestion.** 4 (Low) — instrument hygiene.

**Description.** `ClickGateDiagnostic.cs` (`[ClickGateDiag]`) is a runtime click-arbitration diagnostic
behind the F10 debug-overlay master (`OnGUI` early-returns when F10 off, `:180`; fires per-CLICK on the
`GetMouseButtonDown(0)` edge, `:86` — not per-frame) that ALSO emits a plain, non-`Conditional`
`Debug.Log` on every LMB edge in RELEASE (`:153`, deliberate per the instrument convention + cold-path).
It has no `tools/debug/REGISTRY.md` entry, so future sessions can't reuse-before-rebuild.

**Acceptance criteria.**
- 🎯 **Destination.** `[ClickGateDiag]` appears as a one-line entry in `tools/debug/REGISTRY.md` so a
  future session finds it before rebuilding a parallel instrument, AND a keep-vs-strip decision is
  recorded. *Why:* the reuse index is the whole point of the registry; an unregistered instrument gets
  re-invented.
- 🔒 **Constraint — match the file's shape.** Add the entry under the "Runtime live-toggle dev overlays
  (F-key, behind the F1/F10 dev-overlay master gate)" section using the existing
  `Instrument | Keys | Purpose` columns; name the F10 gating, the per-click `Debug.Log` at `:153`, and
  its purpose (click-arbitration ground truth). *Why:* consistency with the existing 3 sections.
- 🔒 **Constraint — keep/strip is a surfaced call, recorded not self-decided.** The keep-as-standing-
  instrument vs strip-before-1.0 decision (specifically: whether to gate the RELEASE `Debug.Log` at
  `:153`) is a Priya/Sponsor call; the ticket records it once made. *Why:* standing-instrument policy
  is a taste/scope call, not a mechanical one.
- 🎚️ **Default (tunable — Sponsor confirms).** Recommend KEEP as a standing instrument until the live
  mine failure (Drew's r5 item 1) is diagnosed + fixed, THEN re-evaluate gating the release `Debug.Log`.

**Out of scope.** The equivalence-guard + precedence-pin tests (→ ticket b); the live mine-failure
diagnosis itself (Drew r5 item 1).

**Cross-refs.** Comment `5032563742` NIT 4; `tools/debug/REGISTRY.md`; ticket b; Drew comment
`5032217364` (item 1, live mine failure).

---

## d. `test(capture): close wood-in-hand + wood-chop shipped-build capture gaps (86caffwv5)`

**Source.** Tess PR #327 comments `5025894753` (soak-4 coverage-gap NIT) + `5031539815` (r4 NIT 2).

**Priority suggestion.** 3 (Normal) — closes shipped-build gates before the next wood-tier change.

**Description.** Two shipped-build capture gaps let the soak-3 (wood invisible in-hand) and soak-4
(wood-axe whiff) bug classes ship: (1) round-3 fixed the wood belt→held path but shipped NO capture that
drives a wood index through the hand — `-verifyHeldBelt` covers stone axe/spear, `-verifyHeldPickaxe`
covers stone/iron pickaxe; (2) `-verifyChop` acquires the STONE axe via `PickUpAxe`, so the WOOD-tier
chop (the exact soak-4 repro) is not shipped-build-gated.

**Acceptance criteria.**
- 🎯 **Destination.** The wood tier is exercised through the shipped-build capture gates — a wood weapon
  rendered in-hand AND a wood-axe chop landing in the built exe — so the soak-3 and soak-4 regression
  classes are shipped-build-gated, not reliant on PlayMode `renderer.enabled` asserts alone. *Why:* both
  bugs shipped because "green test ≠ rendered-in-hand / wood-driven."
- 🔒 **Constraint — reuse the existing capture hook.** Use
  `HeldWeaponCycleDebug.ShowWeaponForCaptureDebug(index)` (already accepts wood indices 10-14) — extend
  `-verifyHeldPickaxe` OR add a `-verifyHeldWood` flag; do NOT add a new mesh path. *Why:* the mechanism
  exists; this is capture-driving only.
- 🔒 **Constraint — drive the wood axe via belt-selection.** In `ChopVerifyCapture`, select a wood axe
  from the belt (not `PickUpAxe`, which grabs stone) so the wood-tier chop is captured; `RequestChopClick`
  already routes through `ShouldChopOnClick`, so only the tool source changes. *Why:* the soak-4 repro is
  specifically the wood tier.
- 🔒 **Constraint — windowed capture.** Run the new/extended gate windowed (NOT batchmode) per the
  `verify_*_gate.sh` convention. *Why:* `ScreenCapture` + a live Animator need a real swapchain; headless
  lies about live runtime.
- 🎚️ **Defaults.** None — test infrastructure.

**Out of scope.** Any change to the wood mesh / belt logic itself (covered + shipped); non-wood tiers.

**Cross-refs.** Parent `86caffwv5`; comments `5025894753` + `5031539815`; `HeldWeaponCycleDebug.ShowWeaponForCaptureDebug`
(indices 10-14); `served-unverified-soaks-need-played-verification` memory.

---

## e. `polish(art): disambiguate decorative scatter rocks from minable boulder/ore nodes`

**Source.** Sponsor accepted the current state after the ClickGateDiag session 2026-07-21 — deferred
low-priority visual-affordance ticket (the ClickGateDiag session confirmed the mine GATE is correct;
the residual is a readability affordance issue: decorative scatter rocks read as minable and invite
dead-clicks).

**Priority suggestion.** 4 (Low) — Sponsor already accepted the current state; deferred polish, not a blocker.

**Description.** Decorative scatter rocks visually resemble minable boulder/ore nodes, so players click
non-interactive props expecting to mine them. The 2026-07-21 ClickGateDiag session established that the
mine gate + arbitration are CORRECT — this is an affordance/readability problem, not a behavior bug.

**Acceptance criteria.**
- 🎯 **Destination.** A player can distinguish decorative scatter rocks from minable boulder/ore nodes at
  a glance in the shipped exe — the affordance reads WITHOUT a click. *Why:* scatter rocks currently read
  as minable and invite dead-clicks.
- 🔒 **Constraint — affordance only, gate untouched.** Do NOT change the mine gate logic or the
  minable-node data — the gate is correct (ClickGateDiag session 2026-07-21). *Why:* this is readability,
  not behavior.
- 🔒 **Constraint — style-honest.** Stay within the low-poly smooth-shaded + shared-palette style;
  material-honest (stone reads as stone). *Why:* art-direction invariant.
- 🎚️ **Default — mechanism is the dev's to discover.** Route via POC/soak, not prescribed (candidates:
  silhouette/scale contrast, a subtle rim/outline on interactive nodes, a distinct scatter-rock mesh,
  or placement rules). **Name the bar via `/name-the-bar` before build (currently unconfirmed) + carry a
  Predict-Before-Soak line** — this is a Sponsor-taste/feel call, soak-gated.

**Out of scope.** The mine gate / arbitration (confirmed correct); any input-handling change.

**Cross-refs.** ClickGateDiag session 2026-07-21; Sponsor acceptance of current state; `team/quality-bars.md`
(name the affordance bar).
