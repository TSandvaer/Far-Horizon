# Status-Effect Readability — Poison / Stun / Slow — Implementable Spec

**Ticket:** `86cah7yuh` (additional status effects — poison / stun / slow on the general framework). **Owner:** Devon (systems) · **Reviewer:** Drew (per ticket) · **Spec reviewer:** Priya.
**Work-type:** spec (design-only; no code in this PR). **Status:** SPEC — the doc `86cah7yuh` implements. Sections are citable from the ticket's ACs.
**Depends on:** the combat POC `86cah7xxp` (COMPLETE — `StatusEffectKind` / `StatusEffectSpec` / `StatusEffectController` shipped with **bleed only**).

> **What this doc is for (read first):** the ticket's framing is "just new effect DATA on the shipped framework". That is true for **poison** and false for **stun** and **slow** — bleed proved the DoT shape, and the shipped framework can only express DoTs. §2 names the exact framework gaps (with the offending line quoted) so Devon extends rather than forks, and §3–§6 spec the READ: what makes a player know, in half a second and without reading a word, that they are poisoned / stunned / slowed. The ticket says framework extension is IN scope — this spec bounds it to the minimum.

**Builds on (do NOT duplicate):** [`combat-cluster-design-brief.md`](combat-cluster-design-brief.md) §3.2 (the lighter first pass this deepens) + §1.2 (juice caps) + §4 (primitive discipline) · [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) §5 (the chip dock this fills) + §2.4 (the DoT wince-debounce — a shared requirement) · [`hud-three-bar-spec.md`](hud-three-bar-spec.md) (bar/plate/glyph grammar, no-numbers discipline) · [`difficulty-snake-poc-ux.md`](difficulty-snake-poc-ux.md) §A6 (tier-preset contract) + §B0/§B2 (lightly-menacing-not-horror; scariness scales by timing, never by gore) · [`style-guide-v2.md`](style-guide-v2.md) §6 (sub-1.0 anchors) · [`gameplay-ui-direction.md`](gameplay-ui-direction.md) (cream = the only text voice).
**Board:** looked at `inspiration/2026-06-12_21h00_32.png` (the chunky castaway — the ~1:3 head and the generous head-space is why the stun cue works above the head) and `21h16_13` (the **saturated mid-green** world — this is the source of the §4.2 rule that a green poison cue must win on VALUE, not hue).

---

## 0. Tonal anchor (read this first)

> **A status effect is the world telling you that something is temporarily wrong with your body — and then it passes. It must land in half a second, from the corner of your eye, and it must never feel like a punishment or a jump-scare. A kid should see stars over the castaway's head and instantly think "I can't swing for a moment", grin, and wait it out. Nobody should ever be reading a debuff spreadsheet.**

*(Wording is deliberate: stun blocks the **action verbs**, not movement — ticket AC2 🎚️, *"movement is NOT blocked by default… Slow is the movement effect."* A stunned castaway can still walk away; he just can't swing. `ActionsBlocked` is the contract name for exactly that reason — §2 G3.)*

The tone gates that govern every beat below:
- **Legible, never alarming.** These cues telegraph *state*, not panic. Same register as the snake's telegraph: the information is generous, the presentation is calm (`difficulty-snake-poc-ux.md` §B1).
- **No horror, no gore, at any tier** — no green slime-face, no skulls, no screen distortion, no toxic-neon, no sickly desaturation post-process. Menace scales by **timing and potency only**, exactly as the snake and boar do (`§7`).
- **Losing control is the scariest thing you can do to a kid.** Stun is therefore the most tightly capped effect in this spec (§5) — short, never chainable, and always visibly explained.
- **Amplitude is the whole tuning variable.** When in doubt, smaller (`game-juice.md` §0).

**The load-bearing call of this spec:** each effect is separable on **THREE independent channels — silhouette, motion-speed, and fixed HUD position — so hue is never load-bearing.** That is what makes it read for a colour-blind player, at peripheral glance, and against a saturated-green world that will happily eat a green particle.

---

## 1. Ground truth — the shipped framework (quoted, not guessed)

From `Assets/Scripts/Runtime/Combat/`:

```csharp
public enum StatusEffectKind {
    Bleed,   // a damage-over-time: ticks HP down through Health.ApplyDamage. The ONLY kind shipped.
    // Poison, Stun, Slow — reserved framework kinds (later ticket; not applied by the POC controller).
}
```
- `StatusEffectSpec` (a serializable **struct**): `kind`, `damagePerSecond`, `durationSeconds`, `damageType`; helpers `None`, `MakeBleed(dps, duration)`, and:
  ```csharp
  public bool IsActive => damagePerSecond > 0f && durationSeconds > 0f;
  ```
- `StatusEffectController` (MonoBehaviour, sits on the same GameObject as the `Health` it damages — **works both ways**, player and enemy): `public Health health`, `public int ActiveCount`, `Apply(StatusEffectSpec)`, `TickSeconds(float)`; internal `List<Active>` of `{ Spec, Elapsed }`; `Update` accumulates **real `Time.time`** deltas (headless PlayMode sees `deltaTime≈0`), `TickSeconds` is the deterministic EditMode hook; an effect's tick is clamped to its remaining duration; effects drop on expiry or on target death.
- Shipped stacking policy, verbatim from `Apply`'s doc comment: *"Bleed does NOT stack refresh-to-full in the POC: each Apply adds a fresh instance (the framework is list-based so a later ticket can add stack/refresh policy without a reshape)."* — **this ticket is that later ticket** (§6).
- Apply sources today: a weapon's on-hit status (`WeaponDef.OnHitStatus`, backed by the serialized `_onHitStatus`) and an enemy attack (`BoarEnemy.goreBleed`, snake bite). Both `BoarEnemy` and `SnakeEnemy` are `[RequireComponent(typeof(Health))]`, so a status can be applied to either from day one.
- **No audio system exists** — no `.ogg`/`.wav`/`.mp3` under `Assets/`, no `AudioSource`/`PlayOneShot` reference in `Assets/Scripts` (checked 2026-07-27). Every audio line here is marked `<deferred — no audio bus>` and is **not authorable in this ticket**.

---

## 2. Framework gaps — what "just add data" does NOT cover

Poison is genuinely just data. Stun and slow are **control** effects and the shipped shape cannot express them. Minimum extensions, in the order Devon should land them.

> **🔒 VOCABULARY AUTHORITY.** Every identifier below is the one pinned in `86cah7yuh`'s **VOCABULARY CONTRACT**, as amended by ticket comment `90150245438801` (2026-07-27), which added the two zero-alloc chip queries in G4. **The ticket contract wins over this spec if the two ever diverge** — a divergence at review is REQUEST_CHANGES, not a NIT. This section is the *rationale*; the ticket is the *pin*. (An earlier revision of this spec proposed `magnitude`, `IsStunned` and an `IReadOnlyList<ActiveEffectView>` — all three are superseded; see G4.)

| # | Gap | Fix (minimum, contract-pinned names) | Why it's load-bearing |
|---|---|---|---|
| **G1** | `StatusEffectSpec.IsActive => damagePerSecond > 0f && durationSeconds > 0f` — a **stun or slow carries no DPS**, so `Apply` silently drops it (`StatusEffectController.cs:73`, `if (!spec.IsActive) return;`). | Make `StatusEffectSpec.IsActive` **kind-aware**: DoT kinds (`Bleed`, `Poison`) require `damagePerSecond > 0 && durationSeconds > 0`; control kinds (`Stun`, `Slow`) require `durationSeconds > 0` only. | Without this, a correctly-authored `MakeStun` is a **silent no-op** — the worst class of bug (looks wired, does nothing). Success-test: `Apply(MakeStun(1.2f))` raises `ActiveCount` to 1. |
| **G2** | No magnitude field for non-damage effects. Overloading `damagePerSecond` as a move-multiplier would be a semantic trap. | Add **`public float magnitude01;`** to the EXISTING `StatusEffectSpec` struct (contract item 2) — 0..1; **Slow** reads it as the move-speed multiplier, `Stun` and both DoTs ignore it. Factories mirror `MakeBleed` (contract item 3): **`MakePoison(float damagePerSecond, float durationSeconds)`**, **`MakeStun(float durationSeconds)`**, **`MakeSlow(float magnitude01, float durationSeconds)`**. No second spec type, no subclass, no parallel `ControlEffectSpec`. | A struct-field append is serialization-safe; reusing `damagePerSecond` for a multiplier would confuse every future author and every test. |
| **G3** | `TickSeconds` only acts on `kind == Bleed` (`StatusEffectController.cs:98`). Control effects need no tick — they need **aggregate queries**. | `StatusEffectController` exposes the pinned trio (contract item 4): **`bool IsActive(StatusEffectKind kind)`**, **`bool ActionsBlocked`** (true while any `Stun` is active), **`float MoveSpeedMultiplier`** (1.0 = unaffected; the composed Slow factor — **STRONGEST-WINS: the single smallest active `magnitude01`, clamped to the §6.2 floor**; full rule in §6.3). Consumers **read**, they don't subscribe: `WasdMovement` multiplies its commanded speed by `MoveSpeedMultiplier`; the click-verb truth-tables take `ActionsBlocked` as one new term (§5.2). | Read-only scalar queries keep the coupling one-directional and allocation-free. No new events, no new manager, no second controller. **`ActionsBlocked` (not `IsStunned`) is deliberate:** stun blocks action verbs, NOT movement (ticket AC2 🎚️) — `IsStunned` invites a movement root the design does not want. |
| **G4** | No read surface for the chips. They must not touch `_active`, and contract item 6 forbids handing the HUD an enumerable. | Two **zero-alloc scalar queries** (the 2026-07-27 amendment): **`float RemainingSeconds(StatusEffectKind kind)`** (drives §3.1's duration underline) and **`int Stacks(StatusEffectKind kind)`** (drives the stack pips). With `IsActive(kind)` these serve every chip need. | ⚠ **`IReadOnlyList<ActiveEffectView>` is REJECTED** — `IReadOnlyList<T>` *is* an `IEnumerable<T>`, and `foreach` over the interface boxes an enumerator **every frame** in `OnGUI` (contract item 6, `unity6-mastery.md` §GC). Scalar queries carry the same information at zero allocation. |
| **G5** | Enum ordering. | Append **exactly** `Poison, Stun, Slow` after `Bleed`, in that order (contract item 1). **`Bleed` MUST stay ordinal 0.** Never reorder. | Serialized ints must not shift on already-serialized specs (`BoarEnemy.goreBleed`, `SnakeEnemy.biteBleed`, the axe's `OnHitStatus`). Success-test: a hard assert `(int)StatusEffectKind.Bleed == 0`. |

**⚠ Naming hazard — three different `IsActive`s exist. Type-qualify them in code and review:**
- `StatusEffectSpec.IsActive` — a *validity* predicate on the struct (G1 changes its rule, not its name).
- `StatusEffectController.IsActive(StatusEffectKind)` — the pinned per-kind *query* (G3).
- `INudgePanel.IsActive` — unrelated and pre-existing (`PondNudge.cs:160`).

**Recommendation — do NOT rename the struct member.** Renaming `StatusEffectSpec.IsActive` to `IsValid`/`IsAuthored` for clarity was considered and is **not** advised: it has **six** call sites on main (`BoarEnemy.cs:143,161`, `SnakeEnemy.cs:119,137`, `MeleeAttack.cs:234`, `StatusEffectController.cs:73`), so the churn touches four shipped files for a readability gain, and the contract pins neither name. Type-qualification is the cheaper fix. *(Devon may overrule, with that call-site list in hand.)*

**Explicitly NOT in scope of the extension:** no ScriptableObject effect assets, no effect-authoring editor window, no visual-effect registry, no cleanse/cure mechanic, no resistance-to-status tags. Four kinds, one struct field, five query members. If the implementation grows past that, stop and raise it.

### 2.1 Amendment requests — two policy additions beyond the pinned surface

These are framework growth this spec **asks the ticket to authorize**, not silent redefinitions. Both should land as contract amendments on `86cah7yuh` (Priya owns the ticket edit) before implementation starts:

- **A1 — stun chain-immunity window (§5.3).** Needs a per-kind "last expiry" timestamp on the controller so a re-apply inside the window is refused. A chainable stun is unacceptable in a kid-facing game, and the shipped list-add `Apply` would stack stuns forever.
- **A2 — per-kind stacking policy + `Stacks(kind)` (§6.3).** The shipped `Apply` comment explicitly defers this (*"a later ticket can add stack/refresh policy without a reshape"*) — this is that ticket. A linear 3× DoT on hard's `damageTakenMul 1.35` is a stealth instant-death, which is why the intensify curve is sub-linear.

Both are bounded (one timestamp + one count per kind) and neither adds a type.

---

## 3. The three-channel read (the readability contract)

Every effect must be identifiable on **each** of these channels independently. A cue that only works because of its colour is a failed cue.

| Channel | Bleed (shipped) | Poison | Stun | Slow |
|---|---|---|---|---|
| **Chip silhouette** (shape) | `▼` downward teardrop | three stacked rising circles (`bubble` mark) | 4-point star burst `✦` | doubled downward chevron `⌄⌄` (a weight/sinking mark) |
| **Motion signature** (speed is the code) | chip **dims once per DoT tick** — a slow drip | bubbles **rise, ~1.2 s loop** — medium | stars **orbit fast, ~1.5 rev/s** — the ONLY fast motion in the set | chip **sinks and settles, ~1.8 s** — the slowest motion in the set |
| **Fixed HUD position** (positional constancy) | slot 1 | slot 2 | slot 3 | slot 4 |
| **Chip colour** (the *third* cue, never the first) | dying-ember red `#B5563C` | pale sick-green `#C3D68A` | warm cream-gold `#EAD9B8` | cool slate `#6E8A9C` |
| **Text fallback** (last resort, §3.3) | "Bleeding" | "Poisoned" | "Stunned" | "Slowed" |

**Why motion-speed carries the meaning:** stun is the only effect that takes agency away, so it gets the only urgent motion. Slow is the only effect that makes you heavy, so it gets the heaviest, slowest motion. Poison sits between. A player learns this in one exposure without being told, and it holds when the chips are 22 px in the corner of a 1080p frame.

**⚠ Acknowledged palette collision — deliberate, and this is why the three-channel rule exists.** Three of the four chip hexes are byte-identical to shipped `SurvivalHud` colours that carry a different meaning: bleed `#B5563C` = `CoalRed` (warmth-critical band, `:82`), slow `#6E8A9C` = `DryGreyBlue` (thirst-parched band, `:102`), stun `#EAD9B8` = `Cream` (ledger ink, `:84`). So a slowed player who is also parched shows two byte-identical slate elements in the same corner. **Reusing the world/HUD palette is the intent** (one palette, no new hexes — `style-guide-v2.md` §6), and shape + motion + fixed slot keep the two readable apart. But it is a real collision, named here rather than discovered at soak: **if the Sponsor's eye flags it, the cheapest fix is nudging the SLOW chip one step cooler/darker (e.g. `#5E7A8C`, still sub-1.0, still of-the-world)** — do not resolve it by changing a shipped need-bar colour.

### 3.1 Chip layout — fixed slots on the HP row

The dock is reserved by [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) §5: HP row (`y = Screen.height - 162f`), chips start at **`x = 284f`** (= `16 + 260 + 8`), **`22 × 22`** each, `4 px` gaps, up to **4** chips → right edge `384 px`.

- **Slots are FIXED by kind, not packed.** An inactive kind leaves its slot **empty** (draw nothing — no placeholder). So "the third slot is lit" *is* the stun read even before the icon resolves. This positional channel is free and it is the most robust of the three.
- **Plate:** one small dark plate per chip (`PlateAlpha = 0.55f`, ~3 px pad) — the same plate family as the bars and the `LootPrompt` pill. Discrete plates, not one long tray (a tray reads "panel"; discrete pips read "glows", per `hud-three-bar-spec.md` §1).
- **Data binding — the three pinned zero-alloc queries, nothing else.** A chip is drawn when `controller.IsActive(kind)`; its underline reads `controller.RemainingSeconds(kind)`; its pips read `controller.Stacks(kind)` (§2 G3/G4). **No enumeration, no `foreach`, no list** — `OnGUI` runs every frame and boxing an enumerator there is the exact allocation contract item 6 forbids.
- **Duration:** a **2 px underline** beneath the chip, in the chip's colour, shrinking left→right as `RemainingSeconds(kind)` falls toward 0. Non-numeric, glanceable, one `GUI.DrawTexture`. **No countdown digits, ever.**
- **Stacks:** **1–3 small pips above** the chip (2 × 2 px, chip colour), driven by `Stacks(kind)`. Never a `×3` label. (Cap is 3 — §6.3.)
- **Primitive discipline:** flat IMGUI — `GUI.DrawTexture(rect, Texture2D.whiteTexture)` + `GUI.Label` glyphs, drawn inside the existing `SurvivalHud.OnGUI` (explicit `Rect`s, no `GUILayout.*` — the shipped rule). **No new UI system, no UI-Toolkit panel, no post-process.** This corrects `combat-cluster-design-brief.md` §3.2's "UI Toolkit panel / UI Image" phrasing: the live HUD is IMGUI, and a second UI stack for four 22 px chips is unjustifiable. **One HUD code path.**
- **Icon upgrade path:** glyphs now; if `ui-iconography-sourcing.md` later bakes a faceted icon set, the chips swap sprite-for-glyph with no layout change. Never a blocker.

### 3.2 On-character world cues — anchored above the head, ONE place to look

World-space cues use the **`LootPrompt` above-head path** (Sponsor-decided anchor, DECISIONS 2026-07-21): `Camera.WorldToScreenPoint(root + Vector3.up * headAnchorHeight /* 2.2f */)`, screen-clamped (`ScreenMargin = 8f`), **hidden when the projection is behind the camera (`z <= 0`)**. One predictable location for every "something is happening to this body" signal — player *and* enemy (status works both ways).

**Arbitration with the interaction prompt:** a status cue and a loot/verb prompt can both want the head. **Status wins the head anchor**; the interaction prompt shifts up by the cue's height (or the cue sits one line above the pill — implementer's choice, but it must be deterministic and never overlap). Add it to `LootPrompt.ResolveInteractionPrompt`'s priority reasoning rather than inventing a second arbiter.

### 3.3 The text layer — last resort, cream, one line, layout-agnostic

Text is the **fallback**, not the primary. If the shape+motion+position triad ever proves insufficient at soak, add the effect name as a single cream line in the **`LootPrompt` pill idiom** — same plate, same `PillH = 30f`, same cream ink (`gameplay-ui-direction.md`: cream is the only text voice). Words: "Bleeding" / "Poisoned" / "Stunned" / "Slowed" — nothing else, no duration, no magnitude.

**Danish-keyboard rule (verified convention):** `LootPrompt.BuildLabel` builds `"Press " + lootKey + " to …"` and its own comment pins the reason — *"The key is the LITERAL letter (E) — layout-agnostic on the Danish keyboard."* Consequence for this spec: **no status effect may require an input to resolve.** No mash-to-break-free, no shake-off, no key-prompt during stun. Stun is a *wait*, which is both the kid-friendly choice and the layout-safe one (`[[sponsor-danish-keyboard-layout]]`). If a future ticket ever wants a break-out input, it must use a literal letter or an arrow/F-key — never US punctuation.

---

## 4. POISON — the non-red DoT sibling

### 4.1 What it is
A data-only DoT: `MakePoison(dps, duration)` ticking through `Health.ApplyDamage`. Distinguishing feature vs bleed: **lower DPS, longer duration** (bleed = a sharp cut that closes; poison = a lingering sickness). That contrast is the reason to have both, and it must be legible in the *duration underline*, not just the number.

### 4.2 The green problem — win on VALUE, not hue

Looked at `21h16_13`: the world is **saturated mid-green** grass and canopy (`#4C9E3A` body-green, `#7BC65A` top-lit — `style-guide-v2.md` §6). A mid-value green particle in that world **disappears**. So:

- **World cue = pale sick-green pips `#C3D68A`** (0.77, 0.84, 0.54) — *higher value* than any world green, so it separates by luminance rather than hue. Sub-1.0, no bloom.
- **Shape + motion do the identifying:** 3–5 small **rising bubble pips** above the head, drifting up ~0.35 u and fading over ~1.2 s, looped while poisoned. Pooled faceted particles (`Unlit/Particle` material, `ObjectPool<T>` + `OnParticleSystemStopped`) — **≤ 8 per loop**, well under the ≤12 burst cap.
- **On the enemy** (poison works both ways — a future poison weapon/snake) the same pips over its head. Same anchor, same read.
- **FORBIDDEN:** a green screen tint, a green vignette, green skin/material tinting on the castaway (it would fight the prescribed `_HitFlash` tint and reads as horror), toxic-neon, drip/sludge decals.

> **⚠ PRESCRIBED, NOT SHIPPED — the pooled-particle stack does not exist yet.** Verified 2026-07-27 on `main`: **zero** `ParticleSystem`, `ObjectPool` or `OnParticleSystemStopped` references anywhere in `Assets/Scripts`. `game-juice.md` §1.4's "pool every system via `ObjectPool<T>`… berry-pop on harvest" is a **prescription**, not a record of shipped code, and earlier drafts of this spec cited a "berry-pop precedent" that does not exist. **Consequence for sizing:** whichever of `86cah7yuh` / `86cah7z2q` lands first builds the project's **FIRST** particle system — pool, material, prefab and all. That is materially more work than "reuse the existing pattern", and it applies to every world-space cue in §4.2, §5.1 and §6.1. The IMGUI fallbacks in §5.1 exist precisely so the *readability* is not hostage to that lift. (`_HitFlash` is in the same class — see the note in `hp-hud-polish-spec.md` §6.)

### 4.3 Feedback per tick — the debounce is mandatory
Poison ticks route through `Health.ApplyDamage` → `Health.Changed` → the HP-HUD wince. **A per-tick wince strobes.** The HUD-side debounce (≥0.35 s, amplitude scaled by damage fraction) specified in [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) §2.4 is the fix, and it is a **hard requirement of this ticket too** — if `86cah7z2q` hasn't landed yet, the poison implementation must not ship a per-tick screen pulse of its own. The chip's per-tick dim (§3) is the correct tick-level feedback: it lives in a 22 px square and cannot strobe the frame.

**No hit-stop and no camera Impulse on a DoT tick — ever.** A 4 Hz DoT with hit-stop would stutter the whole game. (`game-juice.md` §2: juice fires on discrete strike/impact moments only.)

**Audio `<deferred — no audio bus>`:** a soft, low, wet bubbling tick at ~-20 dB, 3–4 pitch-varied clips, SFX bus, spatialized on the afflicted body. Never a stinger, never per-tick prominence.

---

## 5. STUN — the tightest-capped effect in the game

### 5.1 The cue — toy stars above the head
**Three chunky faceted 4-point stars orbiting the head at ~1.5 rev/s**, warm cream-gold `#EAD9B8` (sub-1.0). The universal, instantly-readable, entirely non-frightening "seeing stars" language — it is *funny* rather than *scary*, which is exactly right for the kid audience, and it belongs to the toy-band world.

- **Preferred implementation:** pooled faceted particles in world space (chunky quads, `Unlit/Particle`), positioned on a circle around the head anchor. Gives real depth + the toy read.
- **Zero-risk fallback:** three `✦` `GUI.Label` glyphs at three advancing positions on a screen-space circle around the head projection (no IMGUI rotation needed — advance the *positions*, not the glyph). Pure IMGUI never strips in the built exe.
  Ship whichever reads better in the **built exe** — verify there, not in the editor (editor-vs-runtime divergence is a proven failure class in this project).
- **Do NOT** hide the held weapon, dim the character, tilt the camera, blur the screen, or play a "dizzy" camera roll. The stars carry it.

### 5.2 The dead-click problem — the most important beat in this section
Stun **cancels the active-click strike** (ticket AC2). A player who clicks and gets nothing will assume the game is broken unless we tell them why:

- **How the block is wired (name the predicate, not a call-site).** The click/E arbitration lives in **pure static truth-tables** — `MeleeAttack.ShouldSwingOnClick(bool weaponSelected, bool verbClaimedClick, bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)` (`MeleeAttack.cs:154`, called at `:183`) plus the sibling guards `ChopTree.ShouldChopOnClick`, `PickableLooter.ShouldLootOnKey`, `MineOre`, `LeftClickConsume`. Per ticket AC2 🔒, add **ONE new term** (`stunned`) to each and pass `controller.ActionsBlocked` — **not** an early-return at the call site and **not** a new input-intercept layer. That truth-table set is the settled arbitration surface (`86caffwv5` round 4) and is the only reason the whole guard set is EditMode-testable without a scene.
- **Every swallowed left-click flashes the stun chip once** — α bump to 1.0, ease back over ~0.15 s (and, if the stars are drawn, one brief scale-pop on them). "Your click was heard; you can't act yet."
- **The click is DROPPED, never buffered** (ticket AC2 🔒) — one input = one strike stays true, so a stun must not queue a swing that fires on recovery.
- The belt/held weapon stays visible and selected — do not unequip.
- **No error sound, no red X, no "You are stunned!" toast.** The chip flash is the whole answer.
- **Success-test to name:** while `ActionsBlocked` is true, N left-clicks produce **0** attacks, **0** buffered strikes on recovery, and **N** chip-flash events; the pre-existing truth-table rows are unchanged (no behaviour drift).

### 5.3 Caps — these are hard, not tunables

| Rule | Value | Why |
|---|---|---|
| **Max stun duration, any tier** | **2.0 s** | Longer than ~2 s of lost control stops being tension and becomes a punishment. |
| **Easy tier** | **≤ 0.6 s** | Control-loss is the scariest thing for a kid. Short enough to read as a hiccup. |
| **Medium** | ≤ 1.2 s | |
| **Hard** | ≤ 2.0 s | |
| **Chain immunity (ALL tiers)** | after a stun ends, a **stun-immunity window ≥ the stun's own duration** (easy: ≥3.0 s regardless) during which further stuns are ignored | A chained stun is the single most rage-inducing mechanic in games and is unacceptable in a kid-facing game. **Framework amendment request A2/A1 — see §2.1**; the shipped list-add `Apply` would happily stack stuns forever. |
| **Stun never stacks or extends** | a second stun inside the window is **dropped entirely** (not queued, not refreshed) | Predictability beats sophistication here. |
| **Movement is NOT blocked** | a stunned castaway can still walk (ticket AC2 🎚️); `ActionsBlocked` gates the verbs only. Slow is the movement effect. | A total freeze reads as a lockout, not a hiccup — and it is the beat a kid would hate most. If the Sponsor later wants stun to root as well, that is a tier dial, not a redesign. |
| **A stun landing MID-SWING must not strand the Animator** | `CastawayCharacter.TriggerAttack`'s single-flight state returns to idle cleanly (ticket AC2 🔒) | Prevents the stuck-mid-attack class of bug. Verify in the **built exe**, not the editor. |

**⚠ Sponsor-judges-at-soak:** whether stun should exist **at all** on the EASY tier. Spec'd as YES with a ≤0.6 s + no-chain cap, because the stars read as charming rather than punishing — but "stun OFF on easy" is a completely legitimate kid-first call and is a one-flag change (`stun_enabled_easy`). See §8 Q1.

**Audio `<deferred — no audio bus>`:** a light, warm, descending two-note chime (bell-ish, not comedic slide-whistle, not a synth). SFX bus, ~-16 dB, one clip is acceptable here (stun is a low-frequency event, so broken-record fatigue doesn't apply).

---

## 6. SLOW — the effect the game feel already tells you

### 6.1 The cue — the movement IS the primary cue
Slow is unique: the player *feels* it before any cue can tell them. So the visual layer stays deliberately minimal, and the job is to explain **why** movement changed (else it reads as lag or a bug).

- **Chip (§3) carries the identity** — slot 4, slate `#6E8A9C`, the slow sinking motion.
- **World cue (minimal):** a faint cool-slate tint on the **ground-contact dust** at the feet each step + a slightly larger, slower dust settle. Reads "heavy legs". Pooled particles, **≤ 6 per step** — subject to the §4.2 "first particle system in the project" sizing note. **No** body tinting, **no** ice/frost/web overlay, **no** trailing motion streaks, **no** vignette.
- **Locomotion cadence — VERIFIED, nothing to build (finding retired 2026-07-27).** The Animator blend **is** speed-driven: `CastawayCharacter`'s `SpeedParam = "Speed"` is fed from *"the SAME `agent.velocity` magnitude `WasdMovement` commands"* (`CastawayCharacter.cs:209-213`), and foot-sync scales clip playback by `actualSpeed / strideRef` inside a clamp band of `footSyncMulMin = 0.5f` … `footSyncMulMax = 2.5f` (`:87-88`). So a `MoveSpeedMultiplier` of 0.6 flows straight through to a slower Walk blend **and** a 0.6× stride cadence — comfortably inside the foot-sync band, so the legs neither freeze nor skate. **A slow reads as heavy legs for free; do NOT add a second speed source or a new Animator state** (ticket AC3 🔒). *(Verified by Priya during PR #339 review and re-checked against source here.)*
- **Camera FOV / lens tricks: forbidden** (`game-juice.md` §2 rules out lens-distortion pulses; the +5° sprint-FOV idiom is for sprint, not for debuffs).

### 6.2 Caps — the `MoveSpeedMultiplier` floor lives HERE
- **Move-speed multiplier floor: 0.6× on any tier** (easy ≥0.8×). Below ~0.6× the character reads as broken/stuck rather than slowed, and it makes the boar charge unavoidable — which converts a fair telegraph into a trap, breaking the snake/boar fairness contract (`difficulty-snake-poc-ux.md` §B1). **This is the floor §2 G3 and §6.3 both clamp to.**
- **Slow must never reach 0×** (that's a stun, and it must go through stun's caps).
- **Duration:** longer than stun, shorter than poison — it's a positioning penalty, not a DoT.

### 6.3 Stacking policy (applies to all kinds — the §1 "later ticket" this closes; amendment request A2, §2.1)
The shipped `Apply` adds an unbounded fresh instance per call. New policy:

| Kind | Same-kind policy |
|---|---|
| **Bleed / Poison** (DoT) | **Refresh-and-intensify, capped at 3 stacks.** Duration = `max(remaining, new)`. Magnitude scales **sub-linearly**: `1.0× / 1.6× / 2.0×` of base DPS — so three bleeds cannot one-shot a player who is already hurt (a linear 3× on hard's `damageTakenMul 1.35` is a stealth instant-death). |
| **Slow** | **STRONGEST WINS, no stacking.** `MoveSpeedMultiplier` = the single **smallest** active `magnitude01`, then clamped to the §6.2 floor. **Never a product** — multiplying two slows (0.7 × 0.8 = 0.56) drops straight through the 0.6 floor, which is exactly the fairness break §6.2 forbids. This is the ONE rule; §2 G3 states it identically. |
| **Stun** | **Never stacks or extends** — plus the immunity window (§5.3). |
| **Cross-kind** | independent; up to all 4 active at once (that's what the 4 chip slots are for). |

**Success-tests to name:** applying bleed 5× yields `Stacks(Bleed) == 3` and total DPS `== 2.0×` base, not 5×; applying two slows (`magnitude01` 0.7 and 0.8) yields `MoveSpeedMultiplier == 0.7` (**not** 0.56); applying stun twice inside the immunity window yields one stun and `ActiveCount == 1`; `WasdMovement.walkSpeed` / `runSpeed` are **byte-unchanged** throughout (ticket AC8's anti-clobber assert — the multiplier is read at command time, never written back).

---

## 7. Per-tier potency (quality-bar #7) — same cues, different generosity

**The cues, shapes, colours and motions are IDENTICAL across all three tiers.** Only duration, potency and the immunity windows change — the same model the snake (telegraph length) and boar (telegraph + charge speed) already use. **Nothing gets scarier-looking on hard.**

| Effect | Easy (kid) | Medium (baseline) | Hard (adult) |
|---|---|---|---|
| **Poison** | low DPS, short duration; chip + pips identical | baseline | higher DPS, longer duration |
| **Stun** | ≤0.6 s, immunity ≥3.0 s (or OFF — §8 Q1) | ≤1.2 s, immunity ≥1.5 s | ≤2.0 s, immunity ≥ duration |
| **Slow** | ≥0.8× speed, short | **0.7×** (see note) | ≥0.6× speed (floor), longer |
| **Bleed** (existing) | shortest / gentlest (boar gore-bleed is already OFF-or-tiny on easy per `combat-cluster-design-brief.md` §2.3) | baseline | longest |

> **Note — this spec SUPERSEDES the ticket's medium slow default.** Ticket AC3 🎚️ proposes `magnitude01` **0.6** for medium; this spec sets medium to **0.7** and keeps **0.6 as the HARD-tier floor** (§6.2). Reason: if medium already sits at the floor, hard has nowhere to go and the tier ladder collapses to two rungs. A 🎚️ default is explicitly tunable, so tightening it here is legitimate — but **Devon must ship ONE number, not both**: use 0.7 medium / 0.6 hard-floor unless the Sponsor's soak says otherwise.

Per-tier values must write **both** the active field **and** the active tier's map entry, or `ApplyDifficulty` clobbers the live dial (the documented dead-knob class; `boar_*` precedent in `SettingsCatalog.cs`).

---

## 8. Tunables + registry ids

Convention (shipped): stable `snake_case` ids in `SettingsCatalog`, registered by a **new dedicated `PopulateStatusEffects`** method — *"each feature adds its OWN Populate method; never grows the base Populate signature."*

Ids carry the **`status_`** prefix, matching the examples the ticket's AC7 🔒 gives (`status_poison_dps`, `status_stun_duration`, `status_slow_mul`) — earlier unprefixed drafts of this table are superseded so Devon ships one set of names.

| Proposed id | Drives |
|---|---|
| `status_poison_dps` / `status_poison_duration` | §4 (per-tier) |
| `status_stun_duration` | §5.3 (per-tier; hard ceiling 2.0) |
| `status_stun_immunity_window` | §5.3 (per-tier) |
| `status_stun_enabled_easy` | §8 Q1 — the easy-tier off switch |
| `status_slow_mul` / `status_slow_duration` | §6 (per-tier; hard floor 0.6, medium 0.7) |
| `status_bleed_dps` / `status_bleed_duration` | existing bleed, exposed for parity |
| `status_stack_cap` | §6.3 DoT stack cap (default 3) |
| `status_world_cues_enabled` | master off switch for the world-space cues (the soak's revert path; chips stay) |

### Sponsor-input items / open questions (NONE block implementation)
- **Q1 — stun on EASY: ≤0.6 s with no-chain, or OFF entirely?** Spec'd ON (short + charming stars). Control-loss is the one thing a kid may hate; `stun_enabled_easy` is the flip. `needs-soak`.
- **Q2 — the stun stars: pooled world-space particles or the IMGUI glyph ring?** Both spec'd (§5.1). Judge in the **built exe**. Do the stars read "toy dizzy" (intended) or "cartoon slapstick, wrong game" (possible)?
- **Q3 — poison pip visibility (§4.2).** Does the pale sick-green hold against saturated grass at gameplay framing? The value-contrast reasoning is sound but only his eye settles it.
- **Q4 — chip position:** right of the HP segment run (spec'd) vs a row above the HP bar. Right-of keeps the column height fixed and the BUILD stamp clear.
- **Q5 — slow floor:** is 0.6× still "playable heavy" or already "stuck"? And does the boar charge stay dodgeable at the floor (fairness check)?
- **Q6 — does the DoT contrast read?** Bleed = sharp/short vs poison = mild/long. If they feel like the same effect in two colours, one of them should be retuned rather than recoloured.

## 9. Predict-Before-Soak (the Self-Test Report must carry these)

- *"Getting stunned shows orbiting stars above the castaway's head and lights the THIRD chip slot; every left-click during the stun flashes that chip and produces no swing; the stun ends inside 1.2 s on medium and a second stun in the following 1.5 s does nothing. Poison lights slot 2 with rising pale-green pips that stay visible over saturated grass, and its ticks produce NO screen strobe (≤1 wince per 0.35 s). Slow lights slot 4 and the castaway visibly walks heavier, never below 0.6× and never stuck. All four cues are identifiable with the chip colours ignored — by shape, motion speed and slot position alone. Nothing at any tier looks gory, toxic-neon, or frightening; no numbers or countdown digits appear anywhere."*
- **Bounded convergence claim:** tested bars — **#7** (3 tiers: same cues, per-tier potency + stun caps), **#2** (motion lively/eased, never linear), **#9** (untouched — no status text hint that could substitute for the emergent matchup read). **NOT tested:** #3 (material-honest — no meshes), #5 (in-hand sizing), #1 (world organic-ness), #4 (real-world anchor — stars are a convention, not a physical feature).
- **Refuted prediction = a finding.** Stop and investigate the foundation before re-fixing.

## 10. Out of scope

HP-HUD polish itself (`86cah7z2q` — the sibling spec; this ticket only fills the reserved chip dock); the `Health`/damage/death mechanics (shipped POC); new weapons or enemies that apply these effects (roster `86cah7ym9`, enemies own their own tickets — this ticket may author `onHitStatus`/attack specs on **existing** weapons/enemies only); a cleanse/cure/antidote item; status resistance tags; effect ScriptableObject assets or an authoring window (§2); **any audio file** (no bus exists); a UI-Toolkit HUD migration (standing separate follow-up); damage numbers or a combat log (forbidden, not deferred); status effects applied by environment hazards (no hazard system exists).

## 11. Decision drafts (for Priya's DECISIONS.md batch — I do not edit that file)

- **Decision draft:** Status effects are identified on **three channels independent of colour** — chip **silhouette**, **motion speed** (stun fastest / slow slowest / poison medium / bleed tick-synced), and a **fixed HUD slot per kind** (empty slot = effect absent, never packed). Colour is the third cue, never the first; text is a last-resort fallback in the `LootPrompt` cream-pill idiom. (`status-effect-readability-spec.md` §3.)
- **Decision draft:** Status chips render as flat IMGUI inside the existing `SurvivalHud.OnGUI` (22 px, `x = 284`, 4 fixed slots, 2 px duration underline, 1–3 stack pips, no numbers) — **not** a UI-Toolkit panel. This corrects `combat-cluster-design-brief.md` §3.2's "UI Toolkit / UI Image" phrasing: the live HUD is IMGUI and one HUD code path is the rule. (`status-effect-readability-spec.md` §3.1.)
- **Decision draft:** **Stun caps are hard, not tunable:** ≤2.0 s at any tier (easy ≤0.6 s), a chain-immunity window ≥ the stun's own duration on every tier, never stacking or extending, no input required to break out (Danish-layout + kid-friendly), and every swallowed left-click flashes the stun chip so a dead click is never mistaken for a bug. (`status-effect-readability-spec.md` §5.)
- **Decision draft:** Slow floors at **0.6× move speed** (easy ≥0.8×, medium 0.7× — superseding the ticket's 0.6 medium default so the tier ladder keeps three rungs) and never reaches 0×. `MoveSpeedMultiplier` is **STRONGEST-WINS — the smallest active `magnitude01`, never a product** (0.7 × 0.8 = 0.56 would fall straight through the floor); DoTs use **refresh + sub-linear intensify capped at 3** (1.0/1.6/2.0×); stun never stacks. Closes the "a later ticket can add stack/refresh policy" note in `StatusEffectController.Apply`. (`status-effect-readability-spec.md` §6.)
- **Decision draft:** Poison's world cue wins on **VALUE, not hue** — pale sick-green `#C3D68A` rising bubble pips above the head, because the world is saturated mid-green and a mid-value green cue disappears against grass. No green screen tint, no body tinting, no toxic-neon at any tier. (`status-effect-readability-spec.md` §4.2.)
- **Decision draft (framework):** Adding poison/stun/slow requires **five bounded framework extensions**, not just data, all named per `86cah7yuh`'s pinned VOCABULARY CONTRACT as amended 2026-07-27 (comment `90150245438801`): kind-aware `StatusEffectSpec.IsActive` (a 0-DPS stun is currently a **silent no-op**), a **`magnitude01`** field, the **`IsActive(kind)` / `ActionsBlocked` / `MoveSpeedMultiplier`** query trio, the two zero-alloc chip queries **`RemainingSeconds(kind)` / `Stacks(kind)`** (an `IReadOnlyList<ActiveEffectView>` is REJECTED — it boxes an enumerator every `OnGUI` frame), and append-only enum ordering `Poison, Stun, Slow` with `Bleed` pinned at ordinal 0. Nothing beyond that (no SO assets, no authoring window). (`status-effect-readability-spec.md` §2.)
- **Decision draft (process):** Two policy additions are requested as **contract amendments**, not silent spec redefinitions — the stun chain-immunity window and the per-kind stacking policy / `Stacks(kind)`. Both are bounded (one timestamp + one count per kind) and both need to land on the ticket before implementation. (`status-effect-readability-spec.md` §2.1.)
- **Decision draft (sizing, shared with `86cah7z2q`):** There is **no particle system in the project** — zero `ParticleSystem` / `ObjectPool` / `OnParticleSystemStopped` references in `Assets/Scripts` (2026-07-27). `game-juice.md` §1.4's pooling guidance is a **prescription**, not a shipped precedent, and the "berry-pop precedent" cited in earlier spec drafts does not exist. Whichever combat ticket lands first builds the project's FIRST pooled particle system; both tickets must size for that. (`status-effect-readability-spec.md` §4.2, `hp-hud-polish-spec.md` §4/§6.)

## Cross-references

- **Tickets:** `86cah7yuh` (this spec) · `86cah7xxp` (POC — the framework; COMPLETE) · `86cah7z2q` (HP HUD polish — the sibling spec / the chip dock) · `86cah7ydt` (boar — `goreBleed`, the fairness contract slow must not break) · `86caaz4vn` (snake) · `86cah7ym9` (weapon roster — future `onHitStatus` authors) · `86cabcdpn` (combat design lock, decision 6).
- **Code (ground truth):** `Assets/Scripts/Runtime/Combat/StatusEffect.cs` (`StatusEffectKind`, `StatusEffectSpec`, the `IsActive` line in §2 G1) · `StatusEffectController.cs` (`Apply`/`TickSeconds`/`ActiveCount`, the real-`Time.time` model, the stacking-policy comment §6.3 closes) · `Health.cs` (`ApplyDamage` — the shared DoT seam; `Changed` — why §4.3's debounce exists) · `MeleeAttack.cs` (the left-click strike stun must cancel) · `CastawayCharacter.cs` (locomotion blend — the §6.1 verify-don't-assume item) · `BoarEnemy.cs` (`goreBleed`, per-tier gore) · `SurvivalHud.cs` (IMGUI grammar, `PlateAlpha`, glyph styles, the chip host) · `LootPrompt.cs` (above-head anchor, screen clamp, `z <= 0` hide, literal-letter/Danish rule) · `Settings/SettingsCatalog.cs` (`Populate*` convention + dead-knob precedent).
- **Docs:** `.claude/docs/game-juice.md` (§1 must-haves incl. pooling + ≤12 particles; §2 hard don'ts — no hit-stop on a tick, no lens/CA pulse, no MPB) · `.claude/docs/art-direction.md` + `inspiration/21h00_32`, `21h16_13` (looked at them — head-space, saturated green) · `.claude/docs/lowpoly-quality.md` (faceted particle shapes) · `.claude/docs/unity6-mastery.md` §2 (GRD / MPB) · `.claude/docs/vision-far-horizon-game-concept.md` (kid→adult difficulty).
- **Uma specs:** `combat-cluster-design-brief.md` §3.2 (the first pass — corrected on the UI-Toolkit point here) · `hp-hud-polish-spec.md` §2.4/§5 (debounce + chip dock) · `hud-three-bar-spec.md` (plate/glyph/no-numbers grammar) · `difficulty-snake-poc-ux.md` §A6/§B0/§B1/§B2 (tier presets; menace-by-timing; the fairness contract) · `style-guide-v2.md` §6 (sub-1.0 anchors) · `gameplay-ui-direction.md` (cream text voice) · `ui-iconography-sourcing.md` (the later icon-swap path).
- **Bars / memories:** `quality-bars.md` #2, #7, #9 · `[[difficulty-settings-easy-medium-hard]]` · `[[sponsor-danish-keyboard-layout]]` · `[[active-input-not-proximity-auto-for-actions]]` · `[[served-unverified-soaks-need-played-verification]]` · `[[verify-soak-builds-or-bake-and-judge]]` · DECISIONS 2026-07-21 (above-head prompt anchor), 2026-07-22 (boar soak PASS / bar #9 confirmed).
