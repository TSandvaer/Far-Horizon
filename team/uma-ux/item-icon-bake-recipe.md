# Item-Icon Bake Recipe — wave items (axe · chopped wood · stone · berry)

**Ticket:** `86caa4bya` AC5 (chopped-wood icon) + the M-U2 gameplay wave's item set (axe, stone, berry).
**Owner:** Uma (direction) → Devon / Drew (bake the icons via the `IconBaker` editor pass).
**Status:** DIRECTION — docs only. No code, no baked assets here. The recipe Devon/Drew execute.
**Supersedes-in-detail:** [`gameplay-ui-direction.md`](gameplay-ui-direction.md) §6 — that section established the *render-the-prop* route and the call; THIS doc is the bake-ready expansion (adds the **berry** icon the wave's hunger loop needs, and tightens the recipe into per-step build instructions). Where the two agree, §6 is the parent; where this doc adds detail (berry, exact camera/light/output values), this doc is the operative spec for the bake.
**Source of truth:** the board PNGs in [`inspiration/`](../../inspiration/) (looked at them: `21h08_08` axe, `21h12_49` log/stump, `21h10_44` rocks, `21h22_33` forage-ground) + [`art-direction.md`](../../.claude/docs/art-direction.md) (chunky warm low-poly board) + [`style-guide-v2.md`](style-guide-v2.md) §1.5 (studio light) / §6 (palette anchors) + [`need-meter-ui-direction.md`](need-meter-ui-direction.md) §4 (berry palette).

---

## 0. Tonal anchor (read this first)

**Every icon is the SAME chunky toy object the player holds in the world — photographed in warm studio light and dropped into a wooden slot.** Not a separate flat drawing. When the castaway opens his pack, the axe in the slot is *the axe in his hand*, the wood is *the log he just chopped*, the berry is *the cluster he picked off the bush*. The icon set should read like a row of little hand-made toys lined up on a sunlit shelf — faceted, saturated, soft-shadowed, cheerful. One material, four objects.

**The gate:** an icon that reads colder, flatter, or "stock-asset generic" than the world it came from is wrong even if it's clean. Warm faceted toy is the target. If you can't tell at a glance that the icon and the world-prop are the same object, the bake failed its only real job.

**Why render, not hand-draw (the call, from §6 — restated because it governs the whole recipe):** rendering the *actual prop mesh* guarantees icon ⇄ held-item coherence (the player never sees two different axes); faceted low-poly renders crisply at slot size (few big facets survive downscale where fussy 2D wouldn't); and re-baking after a prop recolor is one command, so the icon never drifts out of sync. Hand-authored sprites would re-break that coherence every time a prop changes.

---

## 1. THE RECIPE — one reusable `IconBaker` pass (applies to all four)

A small offscreen editor utility (Devon/Drew's structure call) that loads a prop, frames it, lights it, and renders a transparent PNG. Run it once per prop; the SAME rig produces a consistent set. Every value below is sub-1.0 / HDR-clamp-safe (saturated, never blown-out — the discipline that governs the world and the HUD governs the icons too).

### 1.1 Scene & background
- **Offscreen scene** (a temp scene or a render-only camera + temp root — not the game scene). One prop at world origin, rotated to its hero pose (§2 per-item).
- **Background = fully transparent.** Render with a transparent clear (camera `backgroundColor` alpha 0 + the render-texture has an alpha channel). The slot's `slot-empty` walnut well is the backdrop in-game; the icon must drop onto it cleanly with no fringe. **No baked-in colored backdrop** (the blue/grey backdrops in the reference PNGs are the *reference's* studio, not ours — bake on alpha).

### 1.2 Camera
- **Orthographic** (not perspective) — keeps the icon's scale stable across the set and avoids wide-angle distortion at close range. Orthographic size tuned so the prop fills ~80–85% of the frame (a small margin of transparent padding all around so nothing clips the slot edge; see §3 padding).
- **3/4 hero angle** — slightly ABOVE and slightly to ONE SIDE (the flattering angle every board shot uses; e.g. `21h08_08` views the axe at a gentle 3/4 from the front-left-above). Concretely: camera yaw ~25–35° off head-on, pitch ~20–30° looking down. **NOT** flat side-on (loses the chunky volume) and **NOT** top-down (loses the silhouette). The 3/4 is what reads both the silhouette AND the edge-bevel facets.
- **One angle for the whole set** — bake all four from the same camera transform (only the prop's hero rotation changes per §2) so the icons read as a consistent family on the shelf. Differences should come from the *objects*, not from inconsistent framing.

### 1.3 Lighting (the board's studio setup — `style-guide-v2` §1.5)
- **Soft even key light** from the camera's upper-front (warm-white, slightly warm tint to match the world's sunlit daylight — NOT a cold white). The key catches the top facets — that lighter-top-facet read is the board's signature (see the rocks/log facets in `21h10_44` / `21h12_49`).
- **Gentle fill** from the opposite/below side at ~30–40% key intensity so the shadow sides don't go to black — keeps the faceted form readable, warm, toy-like (not dramatic/contrasty).
- **Gentle AO** in the crevices (the recipe's "little ambient occlusion in the crevices," `style-guide-v2` §1.5) — just enough to seat the facets; not a heavy dirt pass.
- **Soft contact shadow under the prop** — a faint, soft blob/drop shadow baked into the PNG directly beneath the object so it "sits on the felt" of the slot rather than floating. Keep it subtle and warm-grey, sub-1.0; it should read as a hint of weight, not a hard black disc. (This is the one shadow that lives IN the sprite; the slot itself is flat.)

### 1.4 Output
- **Render at 256×256** (a square transparent PNG), display at 64px (inventory slot) / 56px (belt slot). Render big + downscale crisp — the faceted edges stay clean and you get one source that serves both slot sizes. Square 1:1 so the icon never distorts when the slot is square.
- **Import as `Sprite (2D and UI)`**, into `Assets/Art/Icons/` (one PNG per item: `icon_axe.png`, `icon_wood.png`, `icon_stone.png`, `icon_berry.png`). Filtering: bilinear is fine at this downscale (point if you want a crisper toy edge — Devon/Drew's call; bilinear is the safe default for clean downscale). Compression: keep alpha; no aggressive lossy that would halo the transparent edge.
- **Consistent in-frame scale across the set** — the four objects are different real sizes (an axe is big, a berry cluster is small), but in the ICON they should each fill a *similar* fraction of the frame so the slots read as a tidy, balanced set (you don't want a tiny berry lost in its slot next to a huge axe). Frame each to ~80–85% fill regardless of the object's true world size. Readability-at-slot-size beats true-scale here.

### 1.5 Legibility check (the bake's acceptance bar)
Before calling an icon done, view it **at 64px on the actual `slot-empty` walnut well** (RGB 0.227, 0.188, 0.165 — warm dark walnut). It must:
1. **Read as the right object in <1 second** from silhouette alone (squint test — the dark-on-walnut silhouette should be unmistakably axe / wood / stone / berry).
2. **Have enough value contrast against the walnut** — the prop's darkest facets must not melt into the dark slot. Warm props on a warm-dark slot is the risk; the key-lit top facets + the contact shadow give the separation. If an icon disappears into the slot, lift the key or add a hair of rim separation — never brighten past sub-1.0.
3. **Sit as one family** with the other three — same light direction, same 3/4 angle, same ~80% fill. Lay all four side by side on the walnut and they should look like one set, not four sourced-from-different-places assets.

---

## 2. PER-ITEM NOTES (hero pose · source prop · palette)

All palettes are sub-1.0, drawn from the world's own colors (so the icon literally shares the world's material). The bake renders the prop's real materials — these anchors are what those materials should BE, and the cross-check Tess eye-drops against.

### 2.1 AXE — `icon_axe.png`
- **Source prop:** the **SHIPPED** `Assets/Art/Props/CastawayAxe/` FBX. Render the real held axe — do not model a new one.
- **Hero pose:** 3/4 with the **head up-left, haft trailing down-right** — the instantly-readable axe silhouette (matches `21h08_08`'s read, mirrored to taste). Tilt it ~25–35° off vertical so it reads as a held tool, not a museum mount; the head's edge-bevel plane should catch the key.
- **Palette — match the SHIPPED prop, do NOT recolor:** slate/steel head, warm-brown haft, dark leather-wrapped grip. **Do NOT recolor the icon to the board's barn-red** (`21h08_08`'s red is the abstract style-guide ideal; the shipped accepted axe is the slate/steel rustic hatchet — DECISIONS 2026-06-14: "axe-head ACCEPTED… genuinely looks like an axe"; barn-red recolor DROPPED). **Icon ⇄ in-hand consistency wins over the board ideal** — the player sees the slate axe in his hand, so the slot must show the slate axe. (This is the one place the icon deliberately diverges from the reference PNG, and it's the *right* divergence — coherence with the shipped prop beats fidelity to the board.)
- **Equip-notch note:** the axe is a TOOL (belt-allowed) — the tiny `ink-cream` equip-notch (`gameplay-ui-direction.md` §4.2) is UI chrome composited over the slot, NOT part of the rendered sprite. Bake the axe clean.

### 2.2 CHOPPED WOOD — `icon_wood.png`
- **Source prop:** a small faceted **log bundle** — Blender-MCP scripted on the board's cut-log vocabulary (`21h12_49`'s cut log + `21h22_33` stumps). The SAME mesh doubles as the world chop-pickup AND the icon source (one asset, two uses).
- **Hero pose:** 3/4, a short stacked bundle of **2–3 cut log segments** with the **cut-end rings facing the camera**. The exposed cut-end rings are the load-bearing read — they say "chopped" unmistakably (a whole log reads as scenery; cut ends read as harvested resource). A loose bundle (slight asymmetry, segments not perfectly stacked) over a single perfect cylinder — hand-made toy, not machined dowel.
- **Palette (sub-1.0):** warm wood-brown body `#7A5230` (0.48, 0.32, 0.19 — the haft/trunk anchor), a lighter sun-caught top-facet `#9A6B40` (0.60, 0.42, 0.25), pale cut-end rings `#C8A878` (0.78, 0.66, 0.47 — the rings should be the LIGHTEST element so they pop as the "chopped" tell). Warm and sunny; this is the hearth/wood family.

### 2.3 STONE — `icon_stone.png`
- **Source prop:** a small faceted **rock cluster** — Blender-MCP scripted on the board's rock vocabulary (`21h10_44` bottom-row boulders, `style-guide-v2` §6 rocks). SAME mesh as the world pickup.
- **Hero pose:** 3/4, a chunky **2–3-rock cluster** (a couple big rocks + a smaller chip nestled in) — a few big planes, lighter top-facets catching the key, darker sides. Mild asymmetry (the board's rocks are never symmetric). A cluster over a single rock — reads richer at slot size and matches the board's always-grouped rocks.
- **Palette (sub-1.0) — WARM-grey, NOT blue-grey** (`style-guide-v2` §6 rocks rule — the single most-violated rock note): warm grey body `#8E8A82` (0.56, 0.54, 0.51), lighter top-facet `#A6A29A` (0.65, 0.64, 0.60), darker side `#6E6A63` (0.43, 0.42, 0.39). The grey must lean warm/tan, never cool/blue — a blue-grey rock instantly reads "different game."

### 2.4 BERRY — `icon_berry.png` (the wave's hunger item — NEW to this recipe)
- **Source prop:** a small faceted **berry cluster** — Blender-MCP scripted on the forage-ground vocabulary of `21h22_33` (the small saturated berry/wildflower clusters near the ground) + the bush ticket `86caa5zz3`. Ideally the SAME picked-berry mesh the bush drops, so the icon = the harvested item (coherence again). A small cluster of **3–5 chunky faceted spheres** on a tiny stem/sprig (the need-meter glyph is literally "three dots on a stem," `need-meter-ui-direction.md` §3.1 — the icon is the fleshed-out toy of that glyph).
- **Hero pose:** 3/4, the berry cluster sitting in a small leaf-cradle (a couple of faceted green leaves under/behind the berries to seat them and add the forage read). Berries front-and-up (the readable silhouette = round ripe cluster), leaves trailing behind/below. Keep it READABLE at slot size — 3–5 berries max; a dense bunch turns to mush at 64px. Chunky faceted spheres (a few big facets each), not smooth balls — same toy material as everything else.
- **Palette (sub-1.0) — the ripe-berry family, tied to the HUNGER meter so the icon and the need read as ONE thing:** ripe berry-red `#C24A4A` (0.76, 0.29, 0.29 — the exact "fed/ripe" fill from `need-meter-ui-direction.md` §4; the slot icon and the hunger bar share the color, so picking the berry visibly "is" feeding the hunger meter), a lighter sun-caught berry top-facet `#D66A6A` (0.84, 0.42, 0.42 — the highlight that makes them read ripe/round), leaves world leaf-green `#4C9E3A` (0.30, 0.62, 0.23 — the world canopy/affordance green, reused). Warm, ripe, appetizing — earthy-warm (hunger family), distinct from warmth's gold and thirst's cool blue. **NOT** a cool/purple berry — keep it in the warm ripe-red the need-meter locked, so a glance ties slot-berry → hunger-bar → bush instantly.

---

## 3. SET-WIDE CONSISTENCY (the four as one family)

- **One camera, one light rig, one ~80–85% fill** for all four (§1.2/§1.4). The set's coherence comes from baking them identically; only the prop + its hero rotation change.
- **All sub-1.0** — saturated warm props, never blown-out (HDR/sRGB-clamp discipline, world + HUD + UI all share it).
- **All warm-biased** except where an object earns a cool note — and none of these four do (axe slate is neutral-warm, wood/berry are warm, stone is warm-grey). The cool note in the gameplay UI belongs to THIRST/water only (`need-meter` §5); no icon here goes cool.
- **Each icon = its world-prop**, baked from the real (or same-vocab) mesh — axe is the shipped FBX; wood/stone/berry are the Blender-MCP props that double as world pickups. Re-baking after any prop recolor is one command, so the set never drifts.
- **Bake order recommendation:** AXE first (the prop already ships — proves the rig end-to-end against a real mesh), then WOOD / STONE / BERRY once their Blender-MCP props are scripted. Each new prop just re-runs the same `IconBaker` rig.

---

## 4. Fallback (if a bake lags the wire)

Per the HUD precedent (`u2-5` §4 — sprite swap is later polish, not a blocker) and `gameplay-ui-direction.md` §6.3: if a rendered icon isn't baked when the inventory/belt wires, fall back to a **chunky warm-cream letter chip** on the `slot-empty` well — `A` axe / `W` wood / `S` stone / `B` berry, in `ink-cream` (`#EAD9B8`), on-tone and legible. The renders are the target and they're cheap — **strongly prefer baking them** — but a missing icon must not block the inventory landing. Swap the letter-chip for the baked sprite when ready; the slot/badge system doesn't change.

---

## 5. Out of scope

The `IconBaker` utility's code/structure (Devon/Drew's call); the slot/belt/badge UI styling (lives in `gameplay-ui-direction.md` §1–5 — this doc is ONLY the icon sprites that fill the slots); the world-pickup gather mechanics (their own tickets); the Blender-MCP prop modeling itself (the recipe assumes the props exist or are scripted on the cited board vocab — modeling is the asset-creation route in `unity-conventions.md` §Asset creation, not this direction doc); tooltips/stats; any item beyond the four wave items (future items re-run the same recipe with a new per-item entry).

---

## Cross-references

- Ticket **`86caa4bya`** AC5 (chopped-wood icon) + the M-U2 wave item set (axe/stone/berry).
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) §6 — the parent decision (render-the-prop route) this doc bakes-out; §1 palette (slot-empty walnut the legibility check uses); §4.2 equip-notch (UI chrome, not in the sprite).
- [`need-meter-ui-direction.md`](need-meter-ui-direction.md) §4 — the HUNGER meter's ripe-berry-red `#C24A4A` the BERRY icon shares (slot ⇄ bar coherence); §3.1 the berry-sprig glyph the icon fleshes out.
- [`style-guide-v2.md`](style-guide-v2.md) §1.5 (studio light: soft key + gentle AO + soft contact shadow), §6 (palette anchors: axe/wood/rock/berry colors; warm-grey-not-blue rock rule).
- [`inspiration/`](../../inspiration/) board PNGs (ground truth, viewed): `21h08_08` (axe hero angle/read), `21h12_49` (cut log + facets), `21h10_44` (rock cluster + lit top-facets), `21h22_33` (forage-ground berry/wildflower clusters — the berry vocab).
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) — board v2 (chunky warm low-poly) + sub-1.0 carry-over.
- [`.claude/docs/unity-conventions.md`](../../.claude/docs/unity-conventions.md) §Asset creation — the Blender-MCP route for the wood/stone/berry props.
- `Assets/Art/Props/CastawayAxe/` — the shipped slate/steel hatchet FBX (the AXE icon's render source; slate NOT barn-red per DECISIONS 2026-06-14).
- DECISIONS 2026-06-14 — axe-head slate/steel ACCEPTED, barn-red recolor DROPPED (binds the AXE icon palette to the shipped prop, not the board's red).

---

## Decision drafts (for Priya to batch into DECISIONS.md)

- **Decision draft:** Wave item icons are baked orthographic 3/4 renders of the real low-poly props (axe = shipped FBX; wood/stone/berry = Blender-MCP props that double as world pickups), one shared `IconBaker` light/camera rig, 256×256 transparent PNG → `Assets/Art/Icons/`. Render-the-prop over hand-authored 2D for icon⇄held-item coherence + zero style-drift on prop recolor.
- **Decision draft:** The BERRY icon shares the HUNGER need-meter's ripe-berry-red `#C24A4A` (slot-berry → hunger-bar → bush read as one thing); berry stays warm ripe-red (earthy-warm hunger family), NOT cool/purple — the only cool note in the gameplay UI belongs to THIRST/water.
- **Decision draft:** The AXE icon matches the SHIPPED slate/steel hatchet, NOT the board's barn-red `21h08_08` ideal — icon⇄in-hand coherence wins over board fidelity (extends DECISIONS 2026-06-14 axe-head call to the icon).
