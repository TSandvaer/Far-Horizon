# U2-5 — Minimal Survival HUD Spec (warmth + inventory readout)

**Ticket:** 86ca8bdge · **Owner:** Uma (spec) → Devon (wiring) · **Reviewer:** Tess
**Depends on:** U2-1 warmth model (86ca8bd9m, Drew, in flight) · U2-2 inventory seed (axe/wood)
**Status:** SPEC — Devon implements this verbatim in a later phase. No code here.

---

## 1. Tonal anchor (read this first)

**The castaway is cold, but the world is warm and waiting.** The island references
(`inspiration/2026-06-08_08h01_13.png` — wreck-strewn shore, a small figure under a
big alive island; the lush garden/village boards) all say the same thing: the world is
the star, the player is a *small hopeful element inside it*. The HUD must never argue
with that. It is a **quiet diegetic glow at the edge of vision**, not a game-console
readout bolted over the painting.

So the design rule that governs every decision below: **the warmth need reads as a warm
light the cold is eating into — not as a red "health bar" with numbers shouting.** When
the player glances at the corner they should *feel* "I'm losing my warmth" the way you
feel a fire dying across the room, then look back at the world. The inventory is a
**small honest ledger** — what's in the castaway's hands — in the same warm, unobtrusive
ink. Zero chrome that says "UI engine." The world keeps the frame.

This is the same discipline that governs the Zone-D look: warm, lush, cohesive, the
player small. The HUD is sub-1.0 warm ink on near-zero-chrome plates, and it *recedes*
when warmth is full so the world is uninterrupted in the happy case.

---

## 2. Layout — where it lives (relative to existing BootHud)

`BootHud.cs` already owns two corners via IMGUI (`OnGUI`):

- **Top-left:** "Far Horizon" title plate (`Rect(8,8,300,40)`).
- **Top-right:** `BUILD <stamp>` plate (`Rect(Screen.width-428, 8, 420, 26)`) — the
  soak-identity stamp. **Do not move or cover this; it is load-bearing for every soak.**

The survival HUD takes the **bottom-left corner**, the calmest region of the frame and
farthest from both BootHud plates. Reading order bottom-up matches "ground truth about
my body": warmth (the urgent thing) sits lowest and largest; the inventory ledger sits
just above it.

```
+--------------------------------------------------------------+
| [Far Horizon]                              BUILD <tag|utc|sha>|   <- BootHud (unchanged)
|                                                              |
|                                                              |
|                      ( the world — the star )                |
|                                                              |
|                                                              |
|   axe  x1     wood  x3                                       |   <- inventory ledger
|   (  warmth  ▰▰▰▰▰▰▱▱▱▱  )                                   |   <- warmth glow-bar
+--------------------------------------------------------------+
```

**Anchor math (IMGUI, matches BootHud's pixel-rect idiom so Devon stays in one system):**

- Warmth element baseline: `y = Screen.height - 44`, `x = 16`. Element box ~`260 x 28`.
- Inventory ledger row: directly above, `y = Screen.height - 80`, `x = 16`, height ~28.
- Both **left-anchored at x=16** so they share a clean left margin with nothing on the
  right half of the screen — the world breathes on the right and top.
- **Safe-area:** keep everything ≥16 px from screen edges; the bottom-left cluster never
  rises above `Screen.height - 96` (one need + one ledger row only this milestone).

> **Why bottom-left, not bottom-center:** center-bottom is where the eye tracks the
> player character during click-to-move; an element there fights the player. The corner
> keeps the HUD in peripheral vision — glanceable, never in the way. (M-U3's second need
> stacks UP from the warmth bar in the same column; reserved, not built now.)

---

## 3. Warmth presentation — the glow-bar

**Concept:** a horizontal **ember bar** that reads as *banked warmth being eaten by
cold from the right*. Filled portion = warm firelight; empty portion = cold dark.
No number. No percent text. Glanceable feeling, not a stat.

**Form (segmented, not a smooth meter):** ~10 segments. Segmented reads more diegetic
("logs on the fire / embers remaining") and is forgiving of the build's HDR/sRGB clamp —
flat warm fills, no gradient texture needed. Filled segments use warm ember ink; emptied
segments dim to a cold near-charcoal. As warmth decays the bar **empties right-to-left**,
like a fire burning down.

**Color (all sub-1.0, HDR-clamp-safe — the world-palette discipline applies to UI too):**

| State band | Fill color (filled segs) | Reads as |
|---|---|---|
| Warm (≈100–60%) | ember gold `#E8B25C` (0.91, 0.70, 0.36) | safe, fire-lit |
| Cooling (≈60–30%) | dusk orange `#D98A4E` (0.85, 0.54, 0.31) | warmth slipping |
| Cold (≈30–0%) | low coal red `#B5563C` (0.71, 0.34, 0.24) | urgent, but not a screaming UI red |
| Emptied segments (all states) | cold charcoal `#2E2A2B` (0.18, 0.165, 0.17) | the cold |

Band transition is a **color shift of the whole filled run**, not a separate alarm
element — the bar quietly warms toward gold or cools toward coal-red as the need moves.
No flashing, no blink (flashing is console-game language; it breaks the painterly calm).

**Optional life (low cost, high charm — recommend, flag for Devon as a stretch):** a
very subtle **ember flicker** on the rightmost *filled* segment — a ±6% alpha breathe at
~1.5 s cycle — so the bar feels like live fire rather than a static fill. Pure alpha
modulation on one segment; no per-frame allocation. If it adds any flicker-judder risk
in the build, cut it — the static bar is the floor and is fully sufficient.

**Plate:** a single low-alpha dark plate behind the bar (`rgba(0,0,0,0.45)`, same idiom
as BootHud's plates) with ~6 px padding, so the warm ink stays legible over both bright
sand and dark foliage. A tiny **flame glyph** (▲ or a simple 12 px flame icon) sits left
of the bar as the only label — diegetic, language-free, says "warmth" without a word.

**Empty-state floor:** at warmth = 0 the bar is all charcoal with the flame glyph dimmed;
do NOT add a death-overlay or red vignette (out of scope — U2-1 caps at a simple floor,
no fail-state this milestone). The dim flame is the whole "you are cold" read.

---

## 4. Inventory readout — the ledger

**Concept:** an honest, quiet **one-line ledger** of what the castaway holds — small
warm-cream text, icon + count, left-aligned, in reading order acquired (axe, then wood).
This is "what's in my pack," not a grid inventory (grids are M-U3+). It reads like the
hand-lettered item labels in the village-board's signage tone — warm, low, unobtrusive.

**Form:** `[icon] x[count]` pairs, spaced along one row. Examples:

```
🪓 1     🪵 3
```

- **Axe:** show only once owned (count ≥1). Before the axe is crafted, the slot is
  absent — no "axe x0" clutter (the empty case is silence, per diegetic-light).
- **Wood:** show the live count; `wood x0` may show as absent OR as a dim `🪵 0` —
  **see open question Q4** (I lean absent-when-zero for maximum quiet; Devon, default to
  absent unless Sponsor soak wants the running tally always visible).
- Icons: prefer a small flat glyph/sprite per item (axe, log). If sprites aren't ready
  when Devon wires, **fall back to short warm-cream text labels** (`axe 1   wood 3`) —
  legible and on-tone; sprite swap is a later polish, not a blocker.

**Color:** warm cream `#EAD9B8` (0.92, 0.85, 0.72) text — the paver-cream family from the
world palette, so the ledger belongs to the same world. Counts in the same cream, bold.
Same low-alpha dark plate as the warmth bar for legibility over varied ground.

**No "+1" popups, no acquisition toasts** this milestone (that's juice for later) — the
count simply updates. Quiet ledger, not a notification feed.

---

## 5. Tonal fit with Zone-D / the warm-lush look

- **Same plate idiom as BootHud** (low-alpha black rounded-feel rects) → the survival
  HUD reads as one family with the existing chrome, not a second visual system.
- **Every UI color is sub-1.0 per channel** and drawn from the world palette (ember gold,
  dusk orange, coal red, paver cream, charcoal). The HUD literally uses the world's own
  colors — that's what makes it recede into the painting instead of floating over it.
- **No pure-white, no pure-saturated-red, no neon.** The cold-state coal red is muted
  (`#B5563C`), not `#FF0000` — a dying-ember red, not an error red. This is the single
  most important tonal call: a bright alarm red would shatter the warm-lush calm.
- **Minimal footprint:** two left-anchored elements in the quietest corner; the entire
  right 60% and top of the frame stay clean. World-stays-the-star, enforced by layout.
- **Recede-when-safe:** at full warmth the bar is calm gold and easy to ignore; the
  design only pulls the eye as warmth drops (color cools toward coal). The HUD earns
  attention exactly when the player needs it and stays out of the way otherwise.

---

## 6. Implementation note for Devon (rendering primitive)

`BootHud` is **IMGUI** (`OnGUI` + `GUI.DrawTexture`/`GUI.Label`). **Stay in IMGUI for
this HUD** — one system, build-safe (pure IMGUI never strips to magenta, per BootHud's
own note), and it sidesteps any uGUI Canvas/serialization setup. Draw the warmth segments
as `GUI.DrawTexture(rect, Texture2D.whiteTexture)` with `GUI.color` set per segment
(filled-band color vs charcoal) — the same flat-rect technique BootHud already uses for
its plates. No custom shader, no mesh, no Polygon primitive needed; flat color rects only.

> If the team later moves the whole HUD to uGUI/Canvas (e.g. for the M-U3 inventory grid),
> this layout + palette + tonal rules carry over unchanged — they're system-agnostic. For
> M-U2, IMGUI is the lowest-risk path and matches what's already shipping.

**Tests (per ticket AC — Devon writes):** paired EditMode/PlayMode asserting the HUD
reflects warmth state (e.g. segment-fill count tracks the warmth value across the band
thresholds) and inventory state (axe present/absent, wood count). Per `unity-conventions.md`,
sample warmth decay over a real `Time.time` window, never per-frame deltas.

---

## 7. Data contract I need from U2-1 (Drew, 86ca8bd9m) — open questions for his PR

U2-1's AC says it exposes "a minimal need readout … coordinate the data surface with
U2-5's spec." Here is the surface this HUD consumes. **These are asks for Drew's PR to
pin concrete names; flagged as open questions, not assumptions:**

- **Q1 — Normalized warmth accessor.** I need warmth as a **0..1 normalized float**
  (1 = full, 0 = freezing) for the segment-fill math, regardless of Drew's internal
  units. *Ask: expose `float WarmthNormalized01 { get; }` (or equivalent) on the warmth
  model — confirm the exact name + that it's clamped 0..1.* If Drew stores raw units +
  a max, I can normalize, but a ready-normalized accessor keeps the HUD dumb.

- **Q2 — Change signal vs poll.** Does the warmth model fire an event on change
  (`event Action<float> OnWarmthChanged`), or should the HUD **poll** the accessor each
  `OnGUI`? *Ask: confirm poll-per-frame is acceptable (IMGUI redraws anyway), OR provide
  the event name.* Poll is fine for IMGUI; just confirm there's no per-call cost surprise.

- **Q3 — The singleton/access path.** How does the HUD reach the warmth model instance —
  a singleton (`WarmthNeed.Instance`), a scene-found component, or injected reference?
  *Ask: name the access path Devon should use so the HUD binds without guessing.*

- **Q4 — Inventory access (U2-2 axe/wood seed).** Parallel ask to whoever lands the
  inventory seed: I need `bool HasAxe { get; }` and `int WoodCount { get; }` (or
  equivalent). *Ask: pin these names + the access path; and confirm the zero-wood display
  call (absent vs `🪵 0`) — I lean absent-when-zero; Sponsor soak can override.*

- **Q5 — Band thresholds.** My color bands (warm ≥60%, cooling 30–60%, cold <30%) are a
  starting proposal tied to the *feel* of the decay, not Drew's tuned decay rate. *Ask:
  once Drew tunes the decay window, confirm the band cutoffs still map to meaningful
  moments (e.g. "cold" band should roughly coincide with "you need to get to a fire NOW").
  Cheap to retune — they're constants.*

**None of these block me authoring this spec.** They block Devon's *wiring*, which is the
later phase — by then U2-1/U2-2 will have merged and the names will be concrete. This
section is the checklist Devon resolves against the merged U2-1/U2-2 PRs before wiring.

---

## 8. Out of scope (per ticket)

Menus, crafting UI, settings, second-need layouts (M-U3), inventory grid, acquisition
toasts, death/fail overlays. This is one warmth glow-bar + one inventory ledger row, in
the bottom-left corner, in the world's own warm colors. Nothing more.

---

## Cross-references

- Ticket 86ca8bdge (this spec) · 86ca8bd9m (U2-1 warmth model, data-contract source).
- `Assets/Scripts/Runtime/BootHud.cs` — existing IMGUI HUD; corner ownership + plate idiom.
- `.claude/docs/art-direction.md` + `inspiration/` — warm/lush, small-player/big-world,
  sub-1.0 cohesive palette (the HUD borrows the world's own colors).
- `.claude/docs/unity-conventions.md` — IMGUI build-safety; real-`Time.time`-window test rule.
- `team/survival-roadmap.md` §2 (HUD row) — source of the U2-5 scope.
