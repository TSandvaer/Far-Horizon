# UI Iconography & Button-Art Sourcing — what makes each UI visual

**Status:** DRAFT — direction note, docs only. Authored in a side helper session (not the orchestrator); promote/edit via the normal UX-direction flow before treating as binding.
**Companion to:** [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — this answers the follow-on question *"if not PixelLab, what sources the icons/buttons?"* It does not change that doc; it expands its §6 (item icons) + §9 (USS chrome) into a full sourcing map.
**Source of truth:** the board PNGs in [`inspiration/`](../../inspiration/) + [`style-guide-v2.md`](style-guide-v2.md) (palette) + [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) (HUD cream/gold parents).

---

## 0. The headline: most "icons / buttons" here are NOT bespoke art

The gameplay-UI direction is deliberately **art-light** — text labels, colored USS shapes, and digits carry the load; decorative glyphs are marked *optional* (`gameplay-ui-direction.md` §2.1). So before reaching for any art tool, the first question is always *"does this need an image at all?"* — and usually the answer is no.

**Why PixelLab (and generative pixel/image tools generally) is the wrong default:** pixel art is a hard-edged, low-res, dithered, limited-palette language — the opposite of the warm, smooth, anti-aliased, sub-1.0 look the whole game commits to. And for a glyph *family*, generative tools drift (each gear/flame/arrow comes out a different weight/perspective), so the set never reads as siblings. PixelLab is also already ruled out for Far Horizon (`CLAUDE.md` — pixel-art-native; Sponsor uses it on other projects). The routes below stay on-style AND keep a glyph set internally consistent.

---

## 1. The three buckets (each has a different right tool)

| Bucket | Examples | Right tool | Bespoke art? |
|---|---|---|---|
| **A. Item icons** | axe, chopped wood, stone, future loot | **`IconBaker`** — render the real low-poly prop mesh to a flat PNG | No — it's a render of an existing asset |
| **B. Chrome** | panels, slots, buttons, sliders, steppers, badges, selection rim | **UI Toolkit USS** — `border-radius` + `border-width` + palette | No — it's styled markup, zero assets |
| **C. Functional glyphs** | gear, close ✕, flame, +/− , arrows, equip-notch | **USS shapes** (trivial ones) → else a **single-color vector set recolored to cream** | Minimal — a small, finite symbol set |

The buckets are ordered by volume: A is a handful of objects, B is *most* of the UI, C is the genuine (small) gap.

---

## 2. Bucket A — item icons → `IconBaker` (already decided)

Per `gameplay-ui-direction.md` §6.1: render the actual in-game low-poly prop to a flat sprite via a small offscreen-camera editor utility — **never hand-author a separate 2D drawing of the item**. The whole point is coherence: the icon *is* the same object the castaway holds, so the player never sees "two different axes."

- **Recipe (verbatim from §6.2):** prop FBX in an offscreen scene → world soft key + fill + contact shadow → orthographic camera at a ~3/4 hero angle → render a 256×256 transparent PNG (displayed at 64px) → import as `Sprite (2D and UI)` into `Assets/Art/Icons/`.
- **Source meshes:** axe = the shipped `Assets/Art/Props/CastawayAxe/` FBX (slate/steel head, **not** barn-red — DECISIONS 2026-06-14); wood/stone = simple faceted props scripted via Blender-MCP (`.claude/docs/unity-conventions.md` §Asset creation), the same mesh doubling as world pickup AND icon source.
- **Re-bake on style change** = one command; the icon never drifts out of sync with the prop. This is why renders beat any drawn/generated 2D art for game objects.
- **Fallback (§6.3):** a chunky warm-cream letter chip (`A`/`W`/`S` on a `slot-empty` well) if a bake lags — shippable, on-tone, swappable. Renders are still the target.

**Do not use** PixelLab / openai-image / hand-drawn 2D for item icons — all three reintroduce the "icon ≠ held prop" incoherence `IconBaker` exists to prevent.

---

## 3. Bucket B — chrome → UI Toolkit USS (no art assets)

Buttons, slots, sliders, steppers, badges, the gold selection rim — none are images. They are styled `VisualElement`s: `border-radius` + `border-width` + the §1 palette of `gameplay-ui-direction.md`. A button = walnut plate + cream label + `:hover` lift; a stepper = `[ − ] value [ + ]` text/shape; a badge = a walnut chip with a cream digit composited live over the icon.

- **There is nothing to source here.** Asking "what makes the button art?" mostly mis-frames the work — it's CSS-style markup, authored once as reusable USS classes (the extensible-registry contract, §9).
- The chunky-rounded-toy read is geometry-free: corners + rims + warm palette, per §9 ("no custom shader, no mesh… UI is 2D quads").

---

## 4. Bucket C — functional glyphs → USS shapes, else a recolored single-color vector set

This is the only place real symbol art is needed, and the set is small (gear, close, flame, +/−, arrows, the equip-notch). Route in order of preference:

1. **Author trivial glyphs directly in USS/UXML.** `+` / `−`, a close-✕ (two rotated rects), a simple arrow/chevron, the equip-notch dot — all primitive shapes; no external asset, perfectly on-palette by construction.
2. **For the rest, a single-color (monochrome) open-license vector icon set, recolored to `ink-cream`.** Single-color is the key constraint — it recolors cleanly and reads as the same hand-lettered ink as the text, never multicolor clip-art.
   - **Candidate sets (verify exact license + style-fit before shipping — do not assume):** `game-icons.net` (survival-relevant glyphs: axes, flames, etc.; single-color, broad coverage) for game symbols; `Lucide` / `Feather` / `Material Symbols` for neutral UI chrome (gear, close, arrows). Pick ONE family for chrome so weights match; game-icons can supply the game-flavored ones.
3. **Icon font** is an acceptable alternative to per-glyph sprites if Devon prefers it — same recolor rules apply (tint = text color).

### 4.1 The recolor recipe (one asset → every state via tint)
Ship the glyph **white/neutral** and tint it in USS, so a single asset serves active *and* disabled without duplicate files — mirroring how the count badge is composited live (§5 of the direction doc):

- Import the monochrome glyph as `Sprite (2D and UI)` into `Assets/Art/Icons/glyphs/`.
- Set it as a `VisualElement` background and tint via USS `-unity-background-image-tint-color` (Devon confirms exact property/version behavior):
  - active / primary → `ink-cream` `#EAD9B8`
  - secondary / disabled / hotkey-hint → `ink-dim` `#9C907A`
  - positive affordance → `accent-leaf` `#4C9E3A`; denied → `accent-deny` `#B5563C`; selected/active-warm → ember-gold `#E8B25C`
- One glyph asset + a tint class per state = the whole functional-icon set stays a consistent cream-ink family, and recoloring is a class swap, not a re-export.

**Discipline (carry from §1 of the direction doc):** monochrome only, single consistent line weight, sub-1.0 / no pure white, optical size ≈ the cap-height of the adjacent cream label. A glyph that arrives multicolor or photoreal is wrong even if it's clean.

---

## 5. Where `openai-image` (connected MCP) fits — and doesn't

- **Good for:** a one-off hero illustration, a title/menu background piece, a single unique decorative emblem — anything singular where cross-asset consistency doesn't matter.
- **Bad for:** a glyph *set* or item icons — generative drift breaks family consistency, and for game objects `IconBaker` is strictly better (coherence + auto re-bake).
- **Rule of thumb:** generative image tools are for *unique* art, never for *systematic* iconography.

---

## 6. One-line summary

**`IconBaker` for item icons · USS for all chrome/buttons · USS shapes or a recolored single-color vector set for the few functional glyphs · `openai-image` only for one-off unique art · never PixelLab/pixel-art for this UI.**

---

## Cross-references
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — §1 palette tokens (the colors above), §6 item-icon `IconBaker` decision, §9 USS-chrome / no-mesh discipline.
- [`style-guide-v2.md`](style-guide-v2.md) — whole-game palette + faceted material grammar.
- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — HUD ember-gold + ledger-cream (parents of the UI ink/gold).
- `.claude/docs/unity-conventions.md` §Asset creation — Blender-MCP route for the wood/stone props `IconBaker` renders.
- `.claude/docs/unity6-mastery.md` §UI Toolkit — the chrome implementation primitive.
- `CLAUDE.md` — PixelLab ruled out for Far Horizon (pixel-art-native); DECISIONS 2026-06-14 — axe slate/steel ACCEPTED, barn-red DROPPED (binds the AXE icon palette).
