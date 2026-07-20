# Heavy-Attack Second-Input UX Spec — `86cau6prr`

**Author:** Uma (UX / Visual / Audio Direction). **Status:** DECISION-READY spec — the input model is a
**Sponsor call**; this doc frames 2–3 candidates + a recommendation so the popup is one click. **Doc-only:**
no code, no build. `86cau6prr` stays `to do` and gated until (a) the Sponsor picks the input via this spec
AND (b) `86caffwv5` (light swings) completes.

> **This spec decides the INPUT, not the mechanic.** The animator wiring, damage/timing model, and
> single-flight interplay are the impl ticket's job (Devon/Drew, once ungated). This tells them WHICH input
> to wire and WHY, and restates the tonal caps the strike must obey.

---

## 0. Tonal anchor — what the heavy should FEEL like

> **The heavy attack is a COMMITTED power strike — the castaway plants, winds the whole body up, and
> brings the overhead down with weight behind it. It reads "I put my back into that one," never "violence."
> It is the slow, deliberate sibling of the quick light slash: you choose it when you have the beat to
> commit, and the trade-off is that you're planted while it lands. A child should feel decisive and a
> little powerful when they land one — never that they did something cruel.**

Every input candidate below is judged first on whether it serves that anchor: a **deliberate, committed**
input suits a deliberate, committed strike. A twitchy/ambiguous input (a chord you fumble, a hold you
mistime) fights the feel. The input should feel as intentional as the swing.

---

## 1. The two hard constraints (from the ticket — non-negotiable)

1. **Layout-agnostic on the Sponsor's Danish keyboard** (`[[sponsor-danish-keyboard-layout]]`). Candidate
   inputs use only layout-stable keys — right-mouse, hold-then-release LMB, a modifier+LMB, or a
   letter/F-key/PgUp-PgDn — **never US-position punctuation** (which shifts on a Danish layout). This is the
   exact rationale `WasdMovement.cs:291-293` cites for choosing Ctrl/Space over punctuation for crouch/jump.
2. **Must NOT overload or collide with the locked one-click-one-swing LEFT path, and a light + a heavy can
   never fire in the same frame** (`[[active-input-not-proximity-auto-for-actions]]`). The light left-click is
   shipped and locked (`MeleeAttack.cs` — `Input.GetMouseButtonDown(0)`, guarded by `ShouldAttackOnClick`,
   single-flight via `_lastAttackAt`/cooldown). The heavy is an ADDITION, guarded so neither auto-fires on
   proximity and the two paths are mutually exclusive per frame.

---

## 2. The live input landscape — collision evidence (cited)

Every shipped binding, from a grep of `Assets/Scripts/Runtime`. This is the collision table each candidate
is scored against.

| Input | Bound to | Source | Free for heavy? |
|---|---|---|---|
| **LMB** (button 0) | Light attack; chop; mine; eat/drink-consume; placement-confirm | `MeleeAttack.cs:131`, `ChopTree.cs:572`, `MineBoulder.cs:372`, `LeftClickConsume.cs:109` | **NO — the light path** |
| **RMB** (button 1) | Camera orbit-drag (yaw+pitch) HELD + cursor lock/hide on edge + the **rejection guard** in every LMB world-action | `OrbitCamera.cs:195-210`, `MeleeAttack.cs:142`, `ChopTree.cs:625` | **NO — core camera control** |
| Scroll wheel | Camera zoom | `OrbitCamera.cs:216` | no |
| **LeftShift/RightShift** | **Run** (sprint) | `WasdMovement.cs:288` | no (modifier taken) |
| **LeftCtrl/RightCtrl** | **Crouch** (hold) | `WasdMovement.cs:303` | no (modifier taken) |
| **Space** | Jump | `WasdMovement.cs:313` | no |
| **Q** | Drink water from hand | `DrinkAction.cs:54` | no |
| **E** | Eat berry / pickup prompt | `EatBerryAction.cs:54`, `PickableLooter.cs:69`, `LootPrompt.cs:52` | no |
| **Tab** | Inventory toggle | `InventoryUI.cs:54` | no |
| **1–9** (Alpha) | Belt-slot select | `InventoryUI.cs:119` | no |
| **C** | Build menu | `BuildMenuUI.cs:64` | no |
| **B** | Held-weapon cycle (debug) | `HeldWeaponCycleDebug.cs:63` | debug-only |
| **F1/F2/F7/F8/F9/F10** | Settings / build-place / dev nudge tools | multiple | no (function keys) |
| **R** | *(nothing)* | — | **YES — free** |
| **F** (letter) | *(nothing — only F1-F10 function keys used)* | — | **YES — free** |

**Two decisive facts fall out of this table:**
- **Both natural chord modifiers are locomotion** — Shift = run, Ctrl = crouch. There is no free conventional
  modifier for a "modifier+LMB" heavy (§4 Candidate B).
- **The mouse is fully committed** — LMB world-actions, RMB camera, wheel zoom. The heavy targets the
  *nearest in-reach* enemy (`MeleeAttack.ResolveNearestTarget`), so it needs **no cursor aim** — a keyboard
  key is not a compromise, it's the natural fit, and it frees the mouse for movement/camera during the strike.

---

## 3. RMB is rejected up front (the ticket lists it — here's why it can't work)

Right-mouse is the **camera orbit-drag**: held = yaw+pitch, and the press/release EDGE locks + hides the
cursor (`OrbitCamera.cs:197-210`, `ResolveCursorForOrbit`). A heavy "right-click" is indistinguishable at
press-time from the start of an orbit drag, so every heavy would briefly lock+hide the cursor and require a
fiddly tap-vs-drag time/movement threshold. Worse, **RMB-held is the rejection guard** that stops a
camera-orbit from also triggering a light attack (`MeleeAttack.cs:142` → `ShouldAttackOnClick(..., rmbHeld)`
returns false). Overloading RMB for the heavy fights the single most-used control in the game.
**REJECT** — direct collision with the core camera control; also the worst kid-ergonomics (right-click-vs-drag
ambiguity).

---

## 4. Candidates

### Candidate A — Dedicated layout-agnostic key: **F** (recommended), **R** as the alternate ⭐ RECOMMENDED

Bind the heavy to a single free letter key, fired on the key's rising edge (`Input.GetKeyDown`, mirroring
jump's `Space` edge at `WasdMovement.cs:313` + its `RequestJump()` latch for headless/capture).

- **Interplay with the locked LMB light path:** *fully decoupled.* The heavy lives on a different device and
  its own edge; it never touches `GetMouseButtonDown(0)`. Same-frame double-fire is prevented by construction
  via a **shared attack-arbiter** in `MeleeAttack.Update`: resolve `if (heavyEdge) PerformHeavy(); else if
  (lightEdge) PerformAttack();` — mutually exclusive, **sharing the existing `_lastAttackAt`/cooldown
  single-flight gate**. A light LMB during heavy recovery is ignored, and vice-versa. The shipped
  `ShouldAttackOnClick` truth-table for the light is untouched; the heavy gets a sibling guard.
- **Collision risk:** LOW/none. `F` and `R` are the only free reachable letters (Q=drink, E=eat/pickup are
  taken; verified §2). **Ergonomic bonus unique to a keyboard key:** the heavy should NOT inherit the light's
  `rmbHeld` rejection — so you can **orbit-look with RMB and heavy-strike with F at the same time**, which the
  LMB light physically cannot do (LMB is swallowed during an RMB orbit). Look-and-commit reads great for a
  telegraphed boar fight.
- **Discoverability:** HIGH. `F` is the near-universal "action/heavy/use" convention; a small on-screen prompt
  ("F — heavy") when a heavy-capable weapon is equipped seals it.
- **Kid-friendly ergonomics** (`[[difficulty-settings-easy-medium-hard]]`): BEST. One key, no chord, no timing
  window. `F` sits under the WASD-hand index finger (right of D on the home row); tapping it slides the index
  off `D` for a beat — which **suits the heavy's "plant and commit" feel** (you're not strafing mid-power-swing
  anyway). `R` (above the home row) is the alternate for players who'd rather keep `D` held; the Sponsor's hand
  decides in soak.
- **Tonal fit:** a single deliberate press for a single deliberate strike. Clean.

### Candidate B — Modifier + left-click (documented, NOT recommended)

E.g. `<modifier>` + LMB → heavy; bare LMB → light.

- **Fatal collision:** the two conventional modifiers are BOTH locomotion — **Shift = run** (`WasdMovement.cs:288`),
  **Ctrl = crouch** (`WasdMovement.cs:303`). So Shift+LMB fires a heavy *every time you attack while running*,
  and Ctrl+LMB *every time you attack while crouched* — both are common states, so the heavy flips on
  accidentally and constantly. The only uncontested modifier is Alt, which risks OS window-drag/menu behavior
  and is awkward for kids.
- **Interplay (if forced):** clean — a single `if (modifierHeld) heavy else light` branch prevents same-frame
  double-fire. But the locomotion collision makes it unpredictable in practice.
- **Discoverability / kid-ergonomics:** modifier chords are a known-but-not-obvious convention; a two-hand
  chord (hold modifier + click) is harder for a small child than one key.
- **Verdict:** only viable if a free modifier existed. It doesn't. Not recommended.

### Candidate C — Hold-left-then-release: tap = light, hold = heavy (documented, NOT recommended)

- **Violates constraint 1 by definition** — it *overloads the locked one-click-one-swing LEFT path*, exactly
  what the ticket forbids.
- **Collides with shipped hold-to-repeat:** ChopTree/MineBoulder/MineOre already use `Input.GetMouseButton(0)`
  HELD to auto-repeat the verb (`ChopTree.cs:572`, `MineBoulder.cs:372`). A weapon is often also a tool (the
  axe chops AND fights) — so "hold left" already means "keep chopping." A hold-release heavy is ambiguous with
  that.
- **Unavoidable same-frame / responsiveness tension:** you either fire light on press *then* heavy on the
  hold-threshold (= two strikes from one gesture — forbidden), OR delay the light until release to disambiguate
  (= the light attack feels laggy, breaking the crisp one-click feel). Neither is acceptable.
- **Charge-adjacent:** hold-to-release IS the charge-input pattern — conflicts with the ticket's default
  "instant heavy, no charge meter" (§5, Q3).
- **Verdict:** rejected on interplay grounds. Only revisit if the Sponsor picks hold-to-**charge** (Q3).

---

## 5. Recommendation

**Recommend Candidate A — a dedicated key, `F` (with `R` as the alternate letter offered in the same soak).**

Why `F` over the others, in one line each:
- It is the **only class that satisfies both hard constraints cleanly** — zero overload of the LMB light path,
  zero same-frame double-fire (mutually-exclusive arbiter sharing one cooldown), layout-agnostic, and it lands
  on a **verified-free** key with no shipped collision.
- It is the **most kid-friendly** (single key, no chord, no timing) — the difficulty-tier north-star wants the
  easy/kid tier to be reachable, and a chord (B) or a timing-hold (C) both raise the floor.
- It carries a **unique ergonomic win** — decoupled from the mouse, so orbit-look + heavy-strike compose, which
  the LMB light can't.
- It **matches the tonal anchor** — one deliberate press for one committed strike.

The heavy input should be **active only when the equipped weapon defines a heavy** (sword-first: just the
sword — see Q2). Concretely, gate on a `WeaponDef` heavy flag so the roster-wide follow-up is a data change,
not a new input path. Guards mirror the light (weapon-selected, target-in-reach, no modal panel via
`UiInputGate.CaptureWorldInput`), **minus** the `rmbHeld` rejection (a keyboard key isn't a camera drag —
that's the look-and-commit bonus).

---

## 6. The 3 remaining open questions — popup-ready options for the Sponsor

*(Framed as decision-ready choices. The orchestrator can drop each straight into an `AskUserQuestion` popup.)*

### Q2 — Sword-only, or roster-wide heavies?
- **A (Recommended): Sword-first / sword-only this ticket.** Wire the reserved overhead `Melee_Attack.fbx` as
  the sword heavy; **build the input + mechanic seam roster-EXTENSIBLE** (a `WeaponDef` heavy flag + optional
  `HeavyAnimationId`) so adding other weapons later is data, not code. Ship the sword, learn the feel, then
  decide the roster. *Rationale:* the overhead reads natural for axe/pickaxe but odd for knife/spear; matches
  the ticket's scope; keeps this ticket M-sized.
- **B: Roster-wide now.** Every weapon gets a heavy immediately. *Rejected-scope:* needs 4+ new heavy clips
  sourced (out of this ticket) AND a knife/spear overhead reads wrong.

### Q3 — Charge, or instant?
- **A (Recommended): Instant heavy.** The chosen input fires the overhead immediately (the ticket default).
  *Rationale:* a charge meter adds a tension-building hold + a HUD gauge that reads more "action game" — the
  calm north-star discourages it; instant keeps the heavy a clean deliberate strike and avoids the
  hold-release input collision (Candidate C).
- **B: Hold-to-charge.** Hold the input to build a power meter, release for a bigger hit. *Trade-off:* more
  depth, but adds a charge-meter HUD element (tonally heavy) and only works if the input is a hold (revives
  Candidate C). Defer to a later "combat depth" pass if wanted.

### Q4 — Stamina cost, or none?
- **A (Recommended): No stamina — commitment IS the cost.** The heavy's price is its slow wind-up + longer
  recovery/punish-window (the ticket's recovery default), not a resource. *Rationale:* keeps the calm-tone HUD
  free of a new bar; the survival needs today are warmth/hunger/thirst, not stamina; risk/reward reads through
  timing, which suits kids.
- **B: Stamina cost.** The heavy spends a stamina resource (ties to the combat-POC systems) + a stamina HUD
  bar. *Trade-off:* more strategy, but another bar on a calm HUD and the heavy starts to feel "expensive/gated"
  rather than "a choice of tempo." Defer to a combat-depth pass.

---

## 7. Tonal guardrails restated for the implementer (impl-ticket must obey)

- **Power strike, not violence.** The heavy reads bigger through **motion** (bigger overhead arc, longer
  follow-through) + a slightly meatier hit-flash + a deeper thud — **never** through blood/gore/gibs, at any
  difficulty tier (`game-juice.md` §0, combat-cluster brief §0). Death stays a topple.
- **Hit-stop ≤ 3 frames — HARD CAP** (`game-juice.md` §1.2, brief §1.2). Default 3 for the heavy (it's the
  weightiest strike), but the Sponsor soak-tunes whether 3 reads "stunned" vs "solid." Camera + UI run on
  `Time.unscaledDeltaTime` so they don't freeze.
- **Restrained impact juice, top-of-band for the heavy:** micro Cinemachine **Impulse** ~0.10u single-frame
  decay (NEVER sustained Noise/shake — `game-juice.md` §2); a pooled faceted warm dust/impact puff **≤12
  particles**, `Unlit/Particle` material, **sub-1.0 channels, never red**; material-honest audio (a heavy iron
  overhead = a slightly brighter honed *shk*, still warm — never a metal-clang action register).
- **Primitive discipline** (carried from brief §4): hit-flash = a `_HitFlash` float in
  `CBUFFER_START(UnityPerMaterial)` on a **per-enemy material instance**, NOT a `MaterialPropertyBlock` (MPB
  disqualifies the GPU Resident Drawer instanced path — `unity6-mastery.md` §2); **no full-screen post-process
  Volume pulse** (Render-Graph cost + tonally wrong); every tint channel **sub-1.0** (HDR-clamp,
  `style-guide-v2.md` §5) so nothing bloom-blows-out.
- **Animator-driven, not procedural** (`[[chop-swing-mixamo-clip-not-procedural]]`): wire the reserved overhead
  as a base-layer overlay state (`AnyState→state` on a trigger; the `Attack`/`Jump` idiom in
  `procedural-animation-verbs.md`). It composes with `CastawayArmPose`→`HeldAxeRig` — the held sword follows
  the hand automatically (`HeldAxeRig` order 100 seats the prop on the final posed hand).
- **⚠ Rig-fact correction the impl-ticket must not chase:** the ticket says "confirm the reserved clip's
  **Humanoid** avatar imports correctly." **The live v4 rig AND `Melee_Attack.fbx` are BOTH `animationType:2`
  = GENERIC, not Humanoid** — verified in `team/drew-dev/weapon-swings-clip-plan.md` §1.1
  (`…/v4/castaway_v4_rigged.fbx.meta:101`, `Melee_Attack.fbx.meta:130`). Do NOT set the clip to Humanoid (the
  known "explode-to-a-cone" trap on this scaled hierarchy). Retarget is by transform-path bone-name binding —
  the overhead already plays on v4 today as the chop, so it's proven.

---

## 8. Predict-Before-Soak (impl author fills the chosen input)

> "Pressing **`F`** (with a heavy-capable weapon equipped) plays the overhead heavy swing once and returns to
> idle; impact lands ~0.40s in when the swing connects; it deals ~2× the light slash; a light left-click during
> heavy recovery is ignored (shared single-flight); the heavy reads as a committed power strike, calm-tone,
> hit-stop ≤3 frames." — graded against the Sponsor soak.

---

## 9. Cross-references

- **Ticket:** `86cau6prr` (this heavy-attack mechanic); depends-on/sequence-after `86caffwv5` (light swings);
  feeds `86cah7ym9` (roster — the "do other weapons get heavies" answer, Q2).
- **Docs:** `.claude/docs/game-juice.md` §0 (tonal anchor) / §1.2 (hit-stop cap) / §2 (hard don'ts),
  `.claude/docs/procedural-animation-verbs.md` (overlay-state idiom; chop→Mixamo-clip ruling),
  `.claude/docs/unity6-mastery.md` §2 (GRD/MPB).
- **Team specs:** `team/uma-ux/combat-cluster-design-brief.md` §0 (tonal anchor), §1.1 (sword=slash class,
  swing feel), §1.2 (impact-feedback caps), §4 (primitive/HDR discipline);
  `team/drew-dev/weapon-swings-clip-plan.md` §1.1 (GENERIC-not-Humanoid rig facts), §4.2 (the `MeleeAttack`
  left-click routing seam this heavy extends), §5 (reserved-overhead = sword heavy).
- **Code (cited, read-only):** `Assets/Scripts/Runtime/Combat/MeleeAttack.cs` (light path + `ShouldAttackOnClick`
  + single-flight), `Assets/Scripts/Runtime/OrbitCamera.cs` (RMB orbit + cursor-lock), `Assets/Scripts/Runtime/
  WasdMovement.cs` (Shift=run/Ctrl=crouch/Space=jump), `Assets/Scripts/Runtime/Settings/UiInputGate.cs`
  (`CaptureWorldInput` panel gate), and the §2 binding-table sources.
- **Bars/memories:** `[[sponsor-danish-keyboard-layout]]`, `[[active-input-not-proximity-auto-for-actions]]`,
  `[[chop-swing-mixamo-clip-not-procedural]]`, `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`,
  `[[difficulty-settings-easy-medium-hard]]`; `quality-bars.md` #2 (lively motion), #7 (3 tiers).

---

## Sponsor-input items (for the popup / soak — decided by the Sponsor, not here)

1. **The input** (§4/§5): `F` recommended — confirm `F` vs the `R` alternate (both offered in soak).
2. **Q2** (§6): sword-only (recommended, seam built roster-extensible) vs roster-wide now.
3. **Q3** (§6): instant (recommended) vs hold-to-charge.
4. **Q4** (§6): no stamina (recommended — commitment is the cost) vs a stamina cost.
5. **Feel tunables at soak** (impl defaults): heavy `swingImpactDelaySeconds` ~0.40s; damage ~2× light;
   hit-stop 3 frames (does 3 read "stunned"?); recovery/punish-window length.

---

## Decision drafts (for Priya's weekly DECISIONS batch — do not edit `DECISIONS.md` directly)

- **Decision draft:** "Heavy-attack second input (`86cau6prr`): Uma recommends a dedicated layout-agnostic key
  — **`F`** (heavy), `R` offered as the soak alternate — over RMB (camera-orbit collision), modifier+LMB (both
  Shift/Ctrl are locomotion), and hold-release-LMB (overloads the locked light path + collides with
  hold-to-chop). Pending Sponsor pick via popup."
- **Decision draft:** "Heavy-attack scope defaults (`86cau6prr`): sword-only this ticket with a roster-extensible
  seam (Q2-A); instant, no charge meter (Q3-A); no stamina — commitment is the cost (Q4-A). Pending Sponsor
  confirmation."
