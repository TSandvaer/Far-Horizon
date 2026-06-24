# THIRST — Freshwater Pond + Drink-from-Hand · Visual & Feel Brief

**Ticket:** `86caamkv7` (THIRST need). **Owner of this spec:** Uma (UX). **Implements:** Devon (need + drink), Drew (pond placement). **Reviewer:** Drew.
**Work-type:** `design(spec)` — no code, no `Assets/`, no shader authoring. This is direction Devon/Drew quote in their dispatch; they own the implementation.

> **Read order for the implementer:** tonal anchor → pond-look deltas (§1) → drink-feel (§2). The deltas in §1 are against the LIVE ocean infra (`LowPolyZoneGen.WaterShallow/WaterDeep/FoamEdge`, `MakeWaterMaterial`, `LowPolyWater.shader`) — REUSE that infra, do not author a new water shader.

---

## Tonal anchor (lead with this — everything below serves it)

**The pond reads as "a small, still, SAFE drink — the island looking after you."**

Where the OCEAN is the thing the castaway *washed in from* — vast, swelling, salt, an edge of the world — the **pond is the opposite read at every axis**: small, calm, sheltered, inland, *yours*. It's the first place on the island that feels domestic rather than wild. When the player walks up thirsty and the camera frames a quiet pool ringed by grass with the trees leaning in (`inspiration/21h13_31`, `21h16_52` lake), the feeling is **relief, not survival-panic**. Hopeful, human-scale, kid-safe. The drink is a small kindness the world gives back.

Two consequences fall straight out of the anchor and govern every call below:
- **Pond ≠ ocean by CONTRAST, not by realism.** The player must read "fresh, drinkable, different water" in under a second at orbit distance. We get that by pushing the pond's hue/calm/foam-edge *deltas AWAY from the sea*, not by simulating freshwater chemistry.
- **The drink is a gentle, repeatable RITUAL, not a power-up.** Lively + lightly damped (`sponsor-prefers-natural-lively-motion`): the gesture has life, the cue is soft, the bar nudges up a *little*. No gulp-VFX spectacle. A small scoop, again, again — that's the fiction ("satisfying small amount of thirst with each scoop").

---

## §1 — Freshwater POND look

**Hard rule: REUSE the ocean water infra. No new shader.** The pond mesh rides the SAME `FarHorizon/LowPolyWater` shader and a SIBLING material built from a copy of `MakeWaterMaterial` with the deltas below. Faceted low-poly water plane, ~1-draw-call-friendly (one extra material instance, one extra mesh — additive to the existing scatter, no per-frame CPU). Every colour stays **sub-1.0 on every channel** (HDR-clamp-safe, per the WaterShallow/WaterDeep convention the ocean already follows).

What the inspiration shows (look at `inspiration/21h16_13` river + `21h16_52` lake): freshwater reads as a **brighter, cleaner, more SATURATED cyan-blue** than the warm grey-teal sea, sitting as a **flat calm plane** with a **crisp green bank** — almost no surf, no swell, just a still sheet the bank wraps tight around.

### 1a. Colour delta — push BLUER, BRIGHTER, CLEANER than the sea
The ocean is a *warm-leaning teal* (`WaterShallow (0.10, 0.62, 0.66)` → `WaterDeep (0.10, 0.50, 0.60)`). The pond must read as a **cooler, brighter freshwater blue-cyan** — the single fastest "this is different water" signal.

- **`PondShallow ≈ (0.22, 0.66, 0.74)`** — bank-edge fresh water: lighter + bluer than `WaterShallow` (R up for brightness, B up for the cool freshwater lean). Sub-1.0.
- **`PondDeep ≈ (0.14, 0.48, 0.70)`** — pool-centre: holds saturation, leans further BLUE than the sea's teal-green centre (note B > G is the freshwater tell; the sea keeps G ≥ B). Sub-1.0.
- **Gradient direction:** shallow→deep from bank inward (same vertex-colour gradient idiom the sea uses via `BuildIslandWater`'s `depthT` lerp — here keyed off distance to the pond centre, not the coast).
- **Rationale for the implementer:** the sea's hue lives in `WaterShallow/WaterDeep`; the pond gets its OWN two constants (`PondShallow`/`PondDeep`) used by the pond mesh's vertex-colour build. Do NOT retune the sea's constants — they are coast-tuned and Sponsor-soaked.

### 1b. Calm delta — STILL water, kill the swell
The sea sets `_WaveAmp = 0.45`, `_WaveLen = 11`, `_WaveSpeed = 1.1` (a travelling swell at sea scale). A small pond with sea-scale swell reads WRONG — a pond is **glassy-calm with the faintest breath**.

- **`_WaveAmp ≈ 0.04`** (down from 0.45) — barely-there shimmer, not a swell. Just enough that the surface isn't dead-static (`sponsor-prefers-natural-lively-motion`: lightly animated, not locked).
- **`_WaveLen ≈ 4`, `_WaveSpeed ≈ 0.4`** — short, slow ripple suited to a few-metre pool (the sea's 11u wavelength / 1.1 speed would put one lazy crest across the whole pond — reads static AND wrong-scale).
- **Feel target:** a still pool that *just* catches a moving glint when the light hits it. If in doubt, calmer.

### 1c. Foam/edge delta — a SOFT damp bank, NOT a surf ring
The ocean bakes a warm surf ring on the warped coast (`WaterFoamStrength 0.92`, `FoamEdge #E8E2D0`) AND the depth-fade foam fires where water meets geometry (`_FoamDistance 1.5`). A pond has **no surf** — but the depth-fade foam where the still water meets the bank is exactly right at LOW strength (the wet, slightly-lighter waterline you see ringing the `21h16_52` lake).

- **Keep the depth-fade foam ON** (it's the shared shader; the pond meets its bank exactly like the sea meets the beach) but **dial `_FoamDistance` DOWN to ≈ 0.6** — a thin, tight damp line hugging the bank, not a wide breaking-surf band. A pond's edge is crisp.
- **`_FoamColor`:** keep `FoamEdge (#E8E2D0)` — the same warm near-white reads as a damp bank as readily as surf; one palette constant, no new colour. (Per `lowpoly-quality.md` §1 fog-cap/foam discipline: don't drift `FoamEdge`.)
- **Do NOT bake a static surf RING** into the pond mesh (the ocean's `WaterFoamCoreU/BandU` plateau) — that's a wave-break read; a pond has none. The dynamic depth-fade line at the bank is the whole foam story.

### 1d. Fog-cap delta — the pond is NEAR, drop the horizon floor
The sea raises `_FogCap = 0.5` to keep its teal distinct from the sky at the far horizon. **The pond is inland and small — it never reaches the fog horizon**, so the fog-floor is irrelevant to it.

- **`_FogCap ≈ 0.0`** on the pond material — let normal fog apply; there's no sea↔sky seam to protect at pond range. (Harmless either way since the pond is always near, but 0 is the honest value + avoids a faint over-bright cast up close.)
- `_WaterAlpha = 1` (solid, not see-through) — same as the sea; the pond is a readable coloured sheet, not a transparent window to a modelled bottom. (OOS: a visible pebble bottom / refraction — `later` territory.)

### 1e. The BANK — the pond's silhouette is half its read
A pond is only as readable as its EDGE. The bank is Drew's terrain-side call, but the FEEL direction:
- **A clean, slightly-raised grassy lip** ringing the pool (see the tight green collar on the `21h16_52` lake and `21h16_13` river banks) — the land dips into the water over a SHORT gentle band so the depth-fade foam has a waterline to ride (mirror the beach's `WetShelfWidth` idea at pond scale: a narrow band where the bank dips just below the pond surface).
- **Nestle it, don't stamp it.** The pool should sit in a shallow natural depression with a few rocks/grass tufts/a reed or two on the bank (reuse existing scatter prims — `FacetedRock`, `GrassClump`) so it reads *found*, not *placed*. A couple of the toy clouds' worth of care; don't over-decorate (anchor: domestic-calm, not a built fountain).
- **Scale:** small — a few metres across (the vision says "a small pond"), readable as one pool at orbit distance, walkable-up-to on the NavMesh. Big enough to frame nicely when the player stands at the edge; small enough that it's clearly a *pond*, not a second sea.

> **Seed-42 discipline (AC2a):** the pond is a DETERMINISTIC ADD on the locked seed-42 terrain — it must NOT re-roll the seed, shift the island silhouette, or move existing scatter. That's Drew's placement constraint; this brief only governs how the pond LOOKS once placed. If an inland pond can't be placed without perturbing the seed stream, that's a Sponsor-gate escalation, not a silent re-roll.

---

## §2 — Drink-from-Hand FEEL

**Anchor restated for this section:** a small, gentle, repeatable RITUAL. The castaway crouches, cups water, sips, the thirst bar nudges up a little, again. Lively + lightly damped. No spectacle.

### 2a. The interaction shape — proximity + interact, mirrors BerryBush/ChopTree
Reuse the proven seam: planar XZ distance to the pond edge + an edge-triggered interact, exactly the `BerryBush`/`ChopTree`/`CraftSpot` idiom (no tool, no inventory item — drinking is NOT berries; AC3). Devon owns the seam; the FEEL layer on top:

- **Interact prompt:** when the player is in drink-range at the pond, the same lightweight on-screen affordance the harvest/chop interactions use ("press E to drink"). Consistent with the existing interact vocabulary — don't invent a new prompt style.
- **Repeatable, no cooldown gate beyond the gesture itself** — the fiction is *scoop, scoop, scoop*. Each press = one small scoop. The gesture's own duration (below) is the natural pacing; no artificial lockout.

### 2b. The GESTURE — cupped-hands scoop, lively + lightly damped
A single drink is a **crouch-cup-sip-rise** beat. Keep it SHORT and readable; the castaway is young + hopeful, so the motion is eager, not laboured.

- **Timing: ~1.0–1.3s total**, broken roughly:
  - **Dip (~0.3s):** castaway bends toward the water, hands cupping down to the surface. Eased-in (slow-to-fast) so it has weight.
  - **Lift + sip (~0.4s):** hands rise to the mouth, a small head-tilt back on the sip. This is the beat the thirst restore + cue land on.
  - **Settle (~0.3–0.5s):** return to stand, lightly over-damped (a tiny settle, not a snap). Per `sponsor-prefers-natural-lively-motion` — the hands FOLLOW through and settle, they don't lock.
- **Hands cupped, not a tool** — v1 is bare hands (a cup is explicitly OOS / `later`). If a full cupped-hand animation isn't authored yet for the placeholder castaway, an acceptable v1 stand-in is a **crouch-toward-water + head-dip** body beat (same crouch the future cupped anim will hang on) — the READ is "he leaned down to the water and drank," and that survives an anim upgrade later.
- **Face/posture:** ends settled and a touch relieved (anchor: relief, not exertion). Don't over-act it.

### 2c. The WATER cue — a small ripple where the hand dips
The single most important *world* feedback: the water should **acknowledge the scoop**. Small, soft, lightly-damped — a hand dipped in a still pool.

- **A small concentric RIPPLE** expanding from the dip point on the pond surface, fading in ~0.6–0.9s. Low amplitude — this is a hand, not a thrown stone. Reuse the existing water-surface motion vocabulary (the shader's vertex displacement) rather than authoring a particle system if a cheap ripple can be driven that way; a tiny short-lived expanding ring decal/mesh is the fallback. Keep it ~1-draw-call-cheap and transient.
- **Optionally a few small water DROPLETS** off the cupped hands on the lift (sub-handful, warm-white, brief) — nice-to-have, not required for v1; cut it before it costs draw calls or reads busy. The ripple is the load-bearing cue; droplets are garnish.
- **Tone:** the ripple should feel like the pond *responding gently*, reinforcing the domestic-calm anchor. NOT a splash.

### 2d. Thirst-bar FILL feedback — a small, satisfying nudge
The bar is RENDERED by the HUD ticket (`86caamkxv`), but the FEEL of the per-scoop restore is set here (and the per-scoop amount is Devon's tweakable, AC3). Direction:

- **Each scoop restores a SMALL amount** — the vision is explicit ("satisfying small amount of thirst with each scoop"). Tune so a thirsty castaway needs **~4–6 scoops** to come back to comfortable. That cadence IS the ritual; a one-scoop full-restore kills the fiction.
- **The fill should READ as satisfying despite being small:** when the HUD ticket animates the thirst bar, the per-scoop rise wants a **quick ease-up with a tiny over-shoot-and-settle** (lively + lightly damped — same motion grammar as the gesture) rather than an instant jump. One soft "tick up" per scoop. (This is a note FOR the HUD ticket `86caamkxv` — flag it cross-ticket; this ticket only exposes the read surface the HUD binds.)
- **The cue lands on the SIP beat (2b), not the dip** — restore + ripple + bar-tick all fire together at the moment he actually drinks, so cause-and-effect reads clean.

### 2e. AUDIO note (light — defer the authored cue, set the target)
A soft **water-scoop / gentle sip** sound on the drink would complete the cue, but no audio bus/asset pipeline is in scope here and none is wired on main for this. **Flag as a follow-up**, not part of this ticket's build: target is a *soft, short, wet "scoop" + small sip* — low, intimate, NOT a big splash (anchor: gentle ritual). Leave a hook; don't block thirst on audio.

---

## Cross-references & deltas summary (for the implementer's dispatch)

| Surface | Ocean (live, DO NOT retune) | Pond (this spec) | Why |
|---|---|---|---|
| Shallow colour | `WaterShallow (0.10,0.62,0.66)` warm teal | `PondShallow ≈ (0.22,0.66,0.74)` brighter blue-cyan | fresh ≠ salt — bluer/cleaner read |
| Deep colour | `WaterDeep (0.10,0.50,0.60)` teal | `PondDeep ≈ (0.14,0.48,0.70)` B>G freshwater | cool blue centre, not teal-green |
| `_WaveAmp` | `0.45` (sea swell) | `≈0.04` | glassy-calm pool, faint breath |
| `_WaveLen` / `_WaveSpeed` | `11` / `1.1` | `≈4` / `≈0.4` | short slow ripple at pond scale |
| `_FoamDistance` | `1.5` (wide surf band) | `≈0.6` (tight damp bank) | crisp pond edge, no surf |
| Static foam ring | `WaterFoamStrength 0.92` baked | **none** | pond has no wave-break |
| `_FogCap` | `0.5` (horizon teal floor) | `≈0.0` | pond is near, no sea↔sky seam |
| `_FoamColor` | `FoamEdge #E8E2D0` | `FoamEdge` (unchanged) | one palette constant, reads as damp bank |

- **Infra reuse:** `LowPolyZoneGen.MakeWaterMaterial` / `BuildIslandWater` / `LowPolyVertexColor`-family + `LowPolyWater.shader` — copy the material build, swap the deltas. (`lowpoly-quality.md` §1: don't regress the fog-cap/foam discipline; §3: no toon-band ramp, keep smooth water.)
- **Interaction reuse:** `BerryBush` / `ChopTree` / `CraftSpot` proximity-XZ + edge-trigger seam (proximity, no tool, no inventory item).
- **HUD hand-off:** the per-scoop bar-fill animation note (§2d) is for `86caamkxv`; this ticket only exposes the `Current01`/`Max`/`IsCritical`/`Changed` read surface (mirror `WarmthNeed`/`SurvivalNeed` exactly).
- **Seed-42 lock:** placement is a deterministic ADD (AC2a) — Drew's constraint, not a look call.
- **OOS (look/feel side):** a craftable cup; a modelled/refractive pond bottom; a big splash VFX; an authored audio asset; saltwater-as-drink. All `later` / out of this ticket.

**Self-Test:** checked against the art-direction board (`inspiration/21h16_13` river + `21h16_52` lake freshwater read) + `lowpoly-quality.md` water patterns (depth-fade foam, fog-cap discipline, smooth-not-toon water) + the live `LowPolyZoneGen`/`LowPolyWater` ocean values the deltas are measured against.
