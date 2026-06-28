# Visual micro-spec — Felled-tree LOG PILE (lootable wood drop)

**Ticket:** `86caf9u5t` (Tree-chop wood rework — award on FELL as lootable log piles). **Gated on:** loot-proximity `86cafc6ud` / PR #158 merge (shared PickableLooter + prompt path). **Author:** Uma (UX). **Status:** Wave-prep — concrete v1 starting target; Sponsor soaks the final feel.

This is a **doc-only** spec. It does NOT implement the mesh; it gives Devon/Drew a concrete v1 they can build without guessing. The Sponsor's eye is the final judge of feel.

---

## Tonal anchor (feel first)

> **A tree just fell here and a castaway bucked it into a few rough logs — a small, honest, grabbable woodpile sitting in the grass.** It reads as the *result of your own swing*, not a spawned game-token: warm sawn wood, freshly cut, low and humble on the ground. Walking up to it should feel like "ah — my wood is right there," not "an icon appeared."

Everything below serves that anchor. The single most important read is the **pale sawn cut-face on the log ends** — that one bright disc is what tells the eye "freshly chopped wood" at a glance and separates a log pile from a grey rock or a dropped stick. If a v1 beat doesn't reinforce *freshly-bucked warm wood on the ground*, cut it.

**Real-world anchor sentence (per `lowpoly-quality.md` §0):** *A log pile is a few short cut logs lying loosely stacked on the ground — round bark sides facing out, pale sawn discs on the ends, the whole thing resting in the grass, low and wide, never floating.* The build must satisfy that sentence — verify with a **side-profile silhouette** capture (grounded, not hovering; low, not a tall totem) before QA/Sponsor.

**Board reference:** `inspiration/2026-06-12_21h12_49.png` (the Blender nature kit) shows the exact target twice — the **felled log** (bottom-right: one faceted horizontal cylinder, warm brown bark sides, a pale lighter disc on the sawn end) and the **stump** (pale cut-top on a brown faceted base). Our log pile is "two or three of that log's little siblings, loosely cross-stacked." Also `21h10_44` for the warm faceted trunk-wood tone.

---

## 1. Mesh shape — low-poly faceted cross-stacked logs

**Primitive: reuse the existing `LowPolyMeshes.TaperedCylinder(botR, topR, height, sides)` — the same call the living tree trunks use** (`LowPolyZoneGen.cs:1178`). A log is just that cylinder laid **horizontal**, short, with near-equal end radii (`botR ≈ topR`, no taper). No new mesh primitive is required for v1; no Blender asset needed (this is procedural-mesh territory, not the weapon/tool Blender family).

- **`sides = 6`** — matches the trunk's 6-sided girth (`LowPolyZoneGen.cs:1178`). 6 reads chunky-faceted, not machined-round, and keeps the cross-section honest with the tree it came from. Do NOT go to 8+ (too round for the style — same rule as the weapon hafts in `blender-asset-pipeline.md` §3).
- **Faceted flat-shaded**, per `lowpoly-quality.md`: build with **explicit per-face normals / no `RecalculateNormals` self-smooth** so each of the 6 bark planes reads as a distinct facet. If routed through the shared `LowPolyVertexColor` material, this is the `_FlatShading` ddx/ddy opt-in path (Rec 2) — a log is a "prop," exactly the opt-in target, never the welded-smooth terrain.

**v1 arrangement — a small criss-cross stack of 3 logs (build this exactly):**

| Log | Length | Radius (both ends) | Lies along | Local position (x, y, z) | Yaw |
|---|---|---|---|---|---|
| Bottom A | 0.85u | 0.16u | mostly +X | (−0.07, 0.16, −0.10) | 0° |
| Bottom B | 0.85u | 0.16u | mostly +X | (+0.10, 0.16, +0.12) | ~12° |
| Top C | 0.75u | 0.15u | crosses the pair | ( 0.00, 0.40, 0.02) | ~70° |

- Two logs lie roughly parallel on the ground (the bottom course), the third rests **across** them at a clear angle — the classic readable "two-down, one-across" woodpile silhouette. The cross-log is what makes it read as a *pile* (deliberately stacked) rather than scattered debris.
- **Radius ≈ 0.16u** ties the log girth to the source trunk (`botR 0.18u` mid-tree, `LowPolyZoneGen.cs:1177`) — a log is *slightly* thinner than the standing trunk it was bucked from, which reads correctly.
- **Grounded, NOT floating** (per the `verify-grounding-soaks-by-gameplay-cam-visual` memory + `physical-features-anchor-realworld-not-metric`): the **lowest bark surface of the bottom course must sit at terrain y = 0** (bottom logs' centre at y ≈ radius ≈ 0.16u so they kiss the ground). The pile should also conform to terrain by raycasting the spawn point down to ground + aligning to the surface normal at fell-spot, so it never half-sinks or hovers on a slope. Spawn at the felled tree's base XZ (the tree's own `transform.position`), per AC2.
- **Per-log seeded variation (cheap, per `lowpoly-quality.md` Rec 7):** a small seeded ±15° yaw jitter and ±8% length scale per log instance so no two piles are identical twins. Costs nothing; raises the hand-bucked feel.

**Footprint / readable size at gameplay orbit-cam distance:** the whole pile occupies roughly **~0.9u wide × ~0.6u deep × ~0.55u tall** — about knee-to-shin height on the ~1.0u-ish castaway, distinctly *lower and wider* than a standing stump and unmistakably a horizontal pile (NOT a vertical bundle/totem). Low and wide is the silhouette that reads "pile of logs" from the rear-orbit gameplay cam; a tall stack reads as a fence post. **Do not scale the count with the wood-yield number** — yield is `1–50` and a 50-log literal stack would be a tower (OOS per ticket: yield is a number, not a mesh count). **The pile is a fixed 3-log visual at every yield**; the yield is data attached to the IPickable, shown via the proximity prompt, not modelled.

---

## 2. Style & palette — warm sawn wood on the shared world tone

**No new texture atlas** (per `blender-asset-pipeline.md` §0 — the shared-palette rule; and `weapon-asset-material-honest-pattern-via-geometry` memory: surface reads as its MATERIAL, pattern via geometry not a detail-texture). Flat per-mesh vertex/material colour only, on the existing low-poly flat-colour material path (`MakeFlatColorMat` / `LowPolyVertexColor`), so the pile batches with the rest of the world's props (~1 draw call).

Two colour reads, both already living in `LowPolyZoneGen.cs` — reuse, do not invent new hexes:

| Surface | Colour | Source (verbatim from `LowPolyZoneGen.cs`) | Why |
|---|---|---|---|
| **Log bark sides** | `(0.46, 0.34, 0.22)` warm dry dead-wood brown | `StickCol` (line 102) | A felled, bucked log is *dead* cut wood lying on the ground — the SAME material-honest tone the fallen-stick prop already uses (its comment, line 99, literally says "a fallen stick reads as weathered dead wood lying on the ground"). Coheres the whole "wood on the ground" family. Marginally lighter/warmer than the living `TrunkCol (0.42,0.30,0.19)` — correct: cut-and-drying, not living bark. |
| **Sawn cut-faces (the two end discs of each log)** | `(0.78, 0.66, 0.46)` pale warm sawn wood | derive from `SandHi (0.88,0.75,0.49)` nudged slightly browner | THE hero read. The bright pale disc on each log end = "freshly chopped" — it's the stump's pale cut-top (`21h12_49`) applied to log ends. Keep it clearly lighter than the bark sides but still warm wood (not white, not grey). HDR-safe: every channel sub-1.0. |

- **Cut-faces are the end-cap fans of the cylinder** (the `TaperedCylinder` top/bottom caps) — assign them the pale colour, the side wall gets the bark colour. With per-face normals this is a clean two-colour split, no texture. This is the geometry-carries-the-pattern rule: the "rings" detail is implied by the single pale disc, not painted.
- If routed through `LowPolyVertexColor`, set per-vertex colour (bark on side verts, pale on cap verts); the existing `_Tint`/value-step path handles the lit gradient. Do NOT add `_FlatShading`-incompatible smoothing.
- **Optional, soak-deferred (do NOT block v1):** a faint per-log value break (`±0.03`) on the bark facets so the three logs don't read as one flat-brown blob — the same tiny per-face break `FacetedMountain` uses (`LowPolyMeshes.cs:433`). Nice-to-have; Sponsor can call it in soak.

**Ruled out (would fight the look or the anchor):**
- ❌ A wood-grain / ring texture on the cut face — geometry + one pale flat colour carries it (atlas ban; ~1 draw call).
- ❌ Arbitrary saturated colour to "pop" the pile — it reads as wood, honestly, like the stones/sticks/berries family (`weapon-asset-material-honest` memory).
- ❌ Floating / hovering above terrain — proven failure class (`verify-grounding-soaks` memory); the side-profile gate exists to catch it.
- ❌ Scaling the log count to the yield number — a tower; yield is data, not mesh.
- ❌ A tall vertical bundle — reads as a post; the pile is LOW and WIDE.

---

## 3. Motion / juice (scope-honest)

Per the ticket OOS, **no wood-chip particles, no chop SFX** — the Sponsor chose shake-only juice for this pass. The log pile itself is **static** once spawned (it's a grounded lootable, not an animated prop). The per-chop tree shake/recoil (AC6) is on the *tree*, not the pile, and is out of this visual's scope. One small allowed touch, soak-optional: a tiny settle on spawn (the pile drops ~0.05u and seats in one frame) so it doesn't pop into existence rigidly — but a clean instant grounded spawn is fully acceptable for v1; do not gold-plate.

---

## 4. Implementer quick-start (the concrete v1, no options)

Build THIS, hand it to soak, let the Sponsor dial from a real thing:

1. **3 horizontal `TaperedCylinder` logs**, `sides=6`, `botR≈topR≈0.16u`, lengths/positions/yaws per the §1 table — two-down + one-across.
2. **Bark sides** = `StickCol (0.46,0.34,0.22)`; **end caps** = pale sawn `(0.78,0.66,0.46)`. Flat-shaded, per-face normals, shared flat-colour material (no atlas).
3. **Spawn at the felled tree's base XZ; raycast down + align to terrain normal so the bottom course rests ON the ground** (bottom-log centre y ≈ 0.16u). Side-profile silhouette gate before QA.
4. **Per-pile seeded jitter** (±15° yaw, ±8% length per log) so piles vary.
5. Footprint ≈ 0.9 × 0.6 × 0.55u — low + wide; pile is a **fixed 3-log visual regardless of yield**.
6. The IPickable / E-prompt / yield-count / despawn-timer wiring is the ticket's engineering ACs (1–8) — not this visual's concern; this spec only defines what the pile LOOKS like.

---

## Cross-references

- `lowpoly-quality.md` §0 (real-world anchor + side-profile gate), §1 (per-face normals / no `RecalculateNormals`), §2 Rec 2 (`_FlatShading` prop opt-in), Rec 7 (seeded scatter variation).
- `blender-asset-pipeline.md` §0 (shared-palette / no per-asset atlas) — applies to procedural props too: one flat-colour material, ~1 draw call.
- `art-direction.md` — board refs `21h12_49` (felled log + stump = the literal target), `21h10_44` (warm faceted trunk-wood tone).
- `LowPolyMeshes.cs:23` (`TaperedCylinder` — the reused primitive) / `LowPolyZoneGen.cs:75` (`TrunkCol`), `:102` (`StickCol` — the bark tone), `:48` (`SandHi` — sawn-face derivation), `:1177-1179` (trunk girth + the `TaperedCylinder` trunk call to match).
- Memories: `weapon-asset-material-honest-pattern-via-geometry` (material-honest + pattern-via-geometry), `verify-grounding-soaks-by-gameplay-cam-visual` + `physical-features-anchor-realworld-not-metric` (grounded, side-profile, anchor-not-metric), `pond-organic-not-round` / `sponsor-prefers-natural-lively-motion` (seeded irregularity over geometric perfection).

## Sponsor-input items (soaked, not decided here)

- **Pile density / count feel** — is 3 logs the right "pile," or does it want 4–5? (v1 is 3; Sponsor dials in soak.)
- **Cross-log angle & seat** — does the across-log read as a deliberate stack, or scattered? Soak-tune the yaw/positions.
- **Sawn-face brightness** — is `(0.78,0.66,0.46)` the right "freshly cut" pale, or push lighter/warmer? The one read that most sells "chopped wood."
- **Footprint scale vs the castaway** — knee-height-and-wide vs a touch bigger; judged in-hand at orbit distance.
- **Spawn settle** — instant grounded vs the ~0.05u soft seat — pure feel, Sponsor's call.
