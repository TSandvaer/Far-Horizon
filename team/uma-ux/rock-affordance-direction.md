# Rock affordance direction — decorative scatter vs minable node

**Ticket:** `86cav8ybj` — *polish(art): disambiguate decorative scatter rocks from minable boulder/ore nodes*
**Scope of THIS doc:** DIRECTION only. No `Assets/**` change, no build, no capture, no test. Implementation
is the Unity-build half of the ticket and stays open behind the single build slot.
**Owner:** Uma (direction) → Drew/Devon (impl once Sponsor confirms the direction). **Reviewer:** Devon.
**Status:** direction proposal awaiting a Sponsor direction-pick (ticket AC2 forbids build-then-soak of a
guessed mechanism). Tagged `needs-soak`; unvalidated until a soak exercises it.

---

## 1. The tonal anchor

> **Far Horizon's shoreline is a place where stone is either LYING THERE or STANDING THERE.**
> Loose stone lies down — it is litter the sea and the weather left flat in the grass, and you walk over it
> without thinking. Stone worth a pickaxe stands UP out of the ground, alone, rooted, taller than you can
> step over. The player should learn that in the first two minutes without being told, and never unlearn it.

The failure this ticket names is not "the player clicked the wrong rock." It is that the world currently
makes a **promise it does not keep** — every grey lump in the grass carries the same posture, the same
proportion, the same grey, and one of the four classes silently has no verb behind it. The fix is to give
the non-interactive class a **posture of its own**, so a swing is never offered to something that cannot
take one.

Everything below serves that anchor. Any beat that does not reinforce *lying-down vs standing-up* is cut.

---

## 2. The measured problem — there is currently NO discriminating channel

Four distinct stone classes ship today. Only one of them is non-interactive, and it sits in the **middle**
of the size distribution. All values fetched from source at `840a1c6`; derived numbers show their arithmetic.

| Class | Object | Mesh (source) | World planar radius | Apex above ground (derived) | Verb |
|---|---|---|---|---|---|
| Pickup pebble | `LP_Stone` | `FacetedRock(0.22, jitter 0.34)` (`LowPolyZoneGen.cs:1573`) | 0.22 × [0.35, 0.80] = **0.077–0.176u** (`:1164`) | ≈ same | `E` → 1 stone |
| **Decorative scatter** | `LP_Rock` | `FacetedRock(0.55, jitter 0.38)` (`:1324`) | 0.55 × [0.55, 1.55] = **0.303–0.853u** (`:1031`) | ≈ radius = **0.30–0.85u** (centred at the ground point) | **NONE** |
| Minable ore node | `OreNode` | `FacetedRock(0.58, jitter 0.42)` (`MovementCameraScene.cs:3005`) | **0.58u** (fixed) | 0.58 + 0.58×0.55 = **0.899u** | pickaxe → mine |
| Minable boulder | `Boulder` | `FacetedRock(1.05–1.35, jitter 0.40)` (`:3178`) | **1.05–1.35u** | radius × 1.45 = **1.52–1.96u** | pickaxe → mine |

Player `NavMeshAgent.height` is **1.8u** (`MovementCameraScene.cs:4182`; the avatar root is scaled to it,
`:4206`) — so "0.85u" is roughly **mid-thigh** on the castaway and "0.90u" is **hip**. Those two are the
same read at gameplay orbit framing.

**Channel-by-channel audit — every one is empty:**

- **FORM (silhouette).** All four classes are the *same function*, `LowPolyMeshes.FacetedRock`, at
  jitter 0.34 / 0.38 / 0.40 / 0.42 — a spread of 0.08 on a single parameter. There is no silhouette
  language separating them; there is one language at four sizes.
- **FORM (size).** The ore node's 0.58u radius lands at the **50.5th percentile** of the decorative band
  ((0.58 − 0.3025) / (0.8525 − 0.3025)). The ore node is, dimensionally, *the median decorative rock*.
  The boulder's floor is only **1.05 / 0.853 = 1.23×** the decorative ceiling — and decorative rocks are
  built in **clusters of 2–4** (`:1025`) spread over ±1.8u (`:1028-1029`), so a cluster presents **more**
  visual mass than one boulder. Size, as shipped, argues the wrong way.
- **FORM (attitude/seating).** Decorative rocks take a yaw + a **0–10° tilt** (`:1317-1318`) and a
  **uniform** scale (`:1319`), centred at the ground point. Minable nodes take yaw only and are lifted
  (`radius×0.45` boulder / `radius×0.55` ore). Both therefore read as *the same half-embedded chunk*,
  upright, ungraded.
- **POSITION.** Decorative rocks scatter across the whole plantable disc from `spawnClearR + 4` outward
  (`:1024`). Both minable pools sit in the **9–17u walkable loop annulus** (boulders `:3112`, ore pool
  `:2931` — the same `9.0 + rnd × 8.0`). They fully overlap, and **nothing excludes a decorative cluster
  from standing next to a minable node.**
- **COLOUR.** `RockCol = (0.62, 0.60, 0.555)` (`LowPolyZoneGen.cs:92`) and
  `BoulderStoneGrey = (0.62, 0.60, 0.555)` (`MovementCameraScene.cs:3055`) are **bit-identical**.
  `OreRockGrey = (0.50, 0.48, 0.45)` (`:2876`) is *darker* than the decorative rock — so the minable ore
  node is the dullest stone in the world and the decorative prop is the brightest.

### 2.1 The one live attract channel is currently painted on the WRONG object

`RockVertexColorMat` — the **decorative** scatter-rock material — opts in to:

- `_RimIntensity = RockRimIntensity = 0.12f` (`LowPolyZoneGen.cs:96`, set at `:1937`) — the RCK-1
  caught-sun silhouette highlight (ticket `86cahhfkc`), and
- `_AOStrength = 0.5f` (same method) — crevice contact-darkening.

The **minable** materials set `_Tint` and nothing else: `boulderMat` (`MovementCameraScene.cs:3075`),
`rockMat`/`veinMat` for the ore pool (`:2894`, `:2896`).

So the only silhouette-attract term the project has built is **live on the prop you cannot mine and dead
on the two you can.** This is Bar 10's second motivating instance (Devon, PR #349 — "a find-in-world
attract cue lost its Fresnel rim") in mirror image: the cue did not merely collapse, it **inverted**.
Correcting the inversion is necessary hygiene. It is *not* the cue — see §5.3 for why.

### 2.2 A behavioural discriminator already exists, and is invisible

Minable nodes carve the NavMesh at runtime so the player is **blocked at the stone's surface**
(`MineableNodeState._carve`; `MovementCarveWorldRadius` = `footprint + 0.15 − 0.40`, floored at 0.20 —
`MineOre.cs`). Decorative scatter rocks carve nothing and hold no collider — **the player walks straight
through them.**

The world therefore *already teaches* the distinction — by bumping into it. The affordance exists in the
simulation and is simply not readable by eye. **That fixes the direction of the answer:** the visual cue
must agree with the physics that already ships. Minable = solid, seated, has mass, you go around it.
Decorative = loose, low, you walk over it. Any cue that contradicts that (a glow, a marker, a hue) adds a
second vocabulary on top of a truth the world is already telling.

---

## 3. PRIMARY CHANNEL — **FORM: attitude + aspect ratio** ("lying down vs standing up")

> **One sentence:** *Decorative stone lies DOWN — wider than it is tall, tilted, half-buried, in a huddle
> of peers. Minable stone stands UP — taller than it is wide, level, alone.*

This is **FORM** in Bar 10's first rank, on two of its three named sub-axes at once (silhouette **and**
size), delivered as an **aspect-ratio inversion**: the decorative class crosses from `taller-than-wide` to
`wider-than-tall`. That is a *categorical* read, not a magnitude read — the eye does not have to compare
two objects to judge it, which is what makes it work at a glance on a lone rock.

**What it reads as for someone who cannot separate the hues** (the Bar 10 obligation): the cue is **100%
geometric**. Grey-scale the frame and it survives untouched, because it is carried by silhouette
proportion and ground contact, not by any tint, rim, or emission. A fully colour-blind player, a
desaturated capture, and a player at 4 m from a 24" monitor all get the same read: *the flat ones are
scenery, the ones that stick up are stone you can work.* Stated as the falsifiable test — **desaturate the
shipped-build capture; the cue must be entirely intact.** (Bar 10's own check.)

**Why this and not a re-authored mesh.** Of the four stone classes, exactly one — `LP_Rock` — has **no
gameplay contract attached**: no verb, no yield, no navmesh carve, no respawn timer, no capture harness, no
difficulty dial. It is also the class that is *dimensionally wrong* (§2, the median-decorative-rock
finding). So the whole cue can be bought by re-posturing the one class nobody depends on, using
**transform values only** — no new mesh, no chamfer pass, no material authoring, and therefore no
collision with `86cacewju`'s hero-prop bevel lineage.

---

## 4. SECONDARY CHANNEL — **POSITION: solitude vs huddle**

> **One sentence:** *A stone standing by itself is minable. A committee of stones is scenery.*

This is Bar 10's second rank, read literally — *a fixed slot per kind*, where the "slot" is spatial
company. Decorative rocks are **already** built 2–4 per outcrop, so half of this channel ships today; the
missing half is that nothing keeps the huddle away from a minable node. Adding a **clearance ring** around
each minable node completes it, and gives the player a second, independent, hue-free confirmation.

**Ordering matters and protects the seed lock.** `LowPolyZoneGen`'s scatter runs BEFORE
`MovementCameraScene.BuildOreNodes`/`BuildBoulders` (the boulder loop already discovers the ore pool by
`GameObject.Find("OreNodes")`, `:3098`). So the exclusion must run **minable-side**: the minable placement
loops reject a candidate that is too close to an existing `LP_Rock`, exactly as they already reject
landmarks (list `:3083`, reject `:3117`) and each other (`:3121`). **This does not touch the seed-42 scatter
stream at all** — the minable pools run on their own deterministic RNGs (`91442` boulders `:3104`, and the
ore pool's own seed), which is precisely the property their own authoring comment claims (`:3062`,
`[[world-is-big-round-island]]`). Doing it the other way round (rejecting scatter candidates) would consume
different draws from the seed-42 stream and move the island's trees. **Do not do it that way.**

---

## 5. Concrete values — Tier 1 (the whole recommendation)

Three edits. All are **value/transform changes at existing call sites**; none authors geometry or
materials.

### 5.1 D1 — Decorative scatter rock lies down (`LowPolyZoneGen.BuildRock`, `:1304-1326` + the scale line `:1031`)

| Knob | Today | Proposed | Why |
|---|---|---|---|
| `localScale` | `Vector3.one * scale` (uniform, `:1319`) | `new Vector3(s, 0.60f * s, s)` | The aspect inversion. Max aspect becomes 0.69u wide × 0.41u tall = **1.67 : 1 wider-than-tall**. |
| `scale` band | `0.55 + rnd × 1.00` → 0.55–1.55 (`:1031`) | `0.55 + rnd × 0.70` → **0.55–1.25** | Caps the apex; keeps the planar footprint generous so the world does not read emptier. |
| Tilt (Euler X/Z) | `rnd × 10f` each (`:1317-1318`) | `8f + rnd × 14f` → **8–22°** each | A tilted slab shows a **plane and an edge**; this is what kills the mound read (see the risk note). |
| Sink | none (centred at `GroundPoint`) | centre **−0.08u** in Y | Buries the low edge so it reads *settled into the grass*, not *placed on it*. |
| Shadow casting | ON (`MakeMeshObject` default) | **keep ON** | The contact shadow of a low slab is the ground-contact evidence. Do not "optimise" it off. |

**Resulting bands** (derived): planar radius `0.55 × [0.55, 1.25]` = **0.30–0.69u**; apex
`0.55 × 0.60 × [0.55, 1.25] − 0.08` = **0.10–0.33u**.

Against the minable floor of **0.899u** (ore node), that is an apex ratio of **0.899 / 0.33 = 2.7×**.
Against the pickup pebble ceiling of 0.176u it stays comfortably above (0.30u planar vs 0.176u), so the
new decorative posture does **not** collapse into the pickup class — the three-tier ladder holds:

> **litter you walk over (≤0.33u) → a stone you pick up by hand (≤0.18u, small and proud) → stone you
> swing at (≥0.90u, standing).**

Real-world anchor sentence, per Bar 4 / `lowpoly-quality.md` §0: **"A decorative shore rock is a flat slab
half-sunk in the grass that you could step onto without lifting your knee."** The side-profile capture must
satisfy that sentence, not a number.

⚠ **Named risk — the "no thin Y-squash" precedent.** `BuildRock`'s own comment (`:1315`, soakfix2
`86ca8m5zu`) records that a Y-squash was rejected because it "flattened it toward a mound." **That reject
was against the *welded* subdiv-2 `FacetedSphere`**, whose weld averages facets into a continuous
gradient — the mound read came from the *smooth normals*, not from the squash. Today's mesh is the
flat-shaded `FacetedRock` (unwelded, explicit per-face normals, per-facet value baked to vertex colour),
whose facets survive a 0.60 squash intact. The tilt raise (8–22°) is the belt-and-braces: a mound has no
visible edge plane, a tilted slab does. **Still, this is the one item that can fail the soak.**
Lower-risk fallback if the Sponsor's verdict is "mound": squash **0.72**, tilt **8–22°**, scale cap
**1.15** — a weaker but still categorical aspect inversion (1.39 : 1). Do not go below squash 0.55; that
crosses into disc/pancake territory and stops reading as stone.

**Bookkeeping that needs NO change (do not "fix" it):** `RockFootprintRadius = 0.55f` (`:1388`) is
consumed by `OverlapsAnyRock` as `RockFootprintRadius * scale`, so the grass- and pebble-exclusion
footprints shrink proportionally with the new scale cap and stay correct automatically.

### 5.2 D2 — Clearance ring around minable nodes (minable-side placement, per §4)

- Collect `LP_Rock` world positions (they exist by the time the minable pools author) and add them to the
  reject list the boulder loop already walks (`:3117`).
- **Clearance radius: 2.6u** from a minable node centre to any `LP_Rock` centre. Derivation: a decorative
  cluster spreads ±1.8u about its outcrop centre (`:1028-1029`) and the new decorative planar radius tops
  out at 0.69u, so 2.6u keeps the nearest slab clearly outside a minable node's silhouette and outside the
  boulder's `mineRadius = 2.4f` (`:3147`) — i.e. **nothing decorative is inside the reach the pickaxe
  actually has.** That last property is what makes the ring a *readable* rule rather than a cosmetic one.
- **Starvation guard.** The boulder loop is bounded at `guard < 12000` (`:3106`) for a pool of 7 in a
  9–17u annulus that already carries an 11-entry landmark list + 4.5u self-spacing. Adding ~60 more reject
  points can starve it. **Required behaviour:** attempt at 2.6u; if the guard exhausts before the pool
  fills, relax to **1.8u** and log the achieved radius + count; never ship a short pool silently. The same
  two-tier relax applies to the ore pool.
- **Expected side effect to plan for:** boulder/ore positions WILL move (different draws consumed on their
  own RNGs). The seed-42 island is untouched, but **the boulder/ore capture baselines move** —
  `BoulderVerifyCapture` (`-verifyBoulder`) and `MineVerifyCapture` (`-verifyMine`) side-profile/gameplay
  shots need re-baselining in the same PR. Call that out in the PR body rather than discovering it in CI.

### 5.3 D3 — De-invert the rim, as hygiene, NOT as the cue

Minimum: the minable materials must not be the **duller** of the two. Set `_RimIntensity` on
`boulderMat` (`:3075`) and the ore `rockMat` (`:2894`) to **at least** the decorative 0.12
(`LowPolyZoneGen.cs:96`), with `_RimPower 3` to match, both `HasProperty`-guarded exactly as the rock
material does it (`:1937`) — an unguarded set on a material whose shader lacks the property is the silent
no-op Bar 10 explicitly warns about. Also mirror `_AOStrength 0.5` onto them so the minable stone is not
the only stone in the world with flat crevices.

**And do NOT sell a rim differential as the discriminator.** A "minable rocks are rimmed at 0.20,
decorative at 0.12" cue is a *fine luminance comparison between two objects*, which (a) requires both in
frame, (b) dies on a bright-sky frame where warm-grey stone is already near the top of the value range,
and (c) is a single-channel cue by Bar 10's definition once you grant that it is neither form nor position.
It reads as "one rock is lit slightly differently," which is weather, not affordance. **Keep the rim as a
world-look term (RCK-1's original purpose — a whisper of caught sun, board `21h10_44`) and let it be equal
across the stone family.** Do not remove it from the decorative rock: that would regress a landed,
deliberate Tier-1 look item.

---

## 6. Tier 2 — OPTIONAL, only if the Tier-1 soak says the boulder still reads ambiguous

The ore node already carries a genuine form discriminator: **3 rusty vein lumps clustered on its upper
surface** (`:3022-3036`), which is Bar 3's "pattern via geometry" and reads as *iron in rock*. The
**boulder has literally nothing** — it is `FacetedRock` in the decorative tint at 1.23× the decorative
size ceiling. If Tier 1 lands and the boulder is still the weak one, the cheapest next form beat is:

**A subordinate chip skirt at the boulder's foot** — 3–4 `FacetedRock(0.10–0.18)` children at planar
`radius × 0.55–0.75`, sitting at the ground line, on the *same* `boulderMat`. It reuses the ore-vein loop
idiom verbatim, authors no new mesh, and lands the board's own language (`21h21_30`: the standing outcrop
has loose rounds at its foot; `21h10_44`: the shard cluster has chips + grass at its base). The read it
buys is **silhouette hierarchy** — *one dominant mass with subordinate crumbs* vs *a peer group of
equals* — which is a different gestalt from the decorative huddle and cannot be confused with it once the
decorative class is lying flat.

⚠ **Hard implementation hazard, must be resolved before this is attempted.**
`MineableNodeState.TryPlanarFootprint` (`MineOre.cs:812`) encapsulates **every** renderer's bounds in the
node (`:820-821`) and feeds the min XZ half-extent into `MovementCarveWorldRadius`. Adding chip children
inside the node **widens the measured footprint and therefore the NavMesh carve** — which re-opens exactly
the round-8 defect the Sponsor already rejected once ("im blocked but already at this distance… not at the
edge of the boulder" — the invisible-wall verdict recorded in `MineOre.cs`'s round-8 comment). Required
resolution: either keep the chips strictly inside the body's planar silhouette so `_carveFootprint` is
provably unchanged, **or** restrict `TryPlanarFootprint` to the body renderer (`BoulderMesh` / `OreRock`)
and re-verify. Either way, re-run `MineBoulderPlacementObstacleTests` and `BoulderSceneTests` and quote
`_carveFootprint` before/after in the PR. **Chips must live inside the node** (not as pool-root siblings)
so they sink/fade/regrow with the break tween — a broken boulder must not leave orphan rubble on a spot
that regrows.

This tier is deliberately **not** in the recommendation. It is a labelled escape hatch so a soak-reject has
a next move that does not require re-opening mesh authoring.

---

## 7. What I am deliberately NOT proposing, and why

| Not proposing | Why not |
|---|---|
| **Re-authoring the boulder or ore-node mesh** (new silhouette, bevel/chamfer, split-rock variant) | `86cacewju` owns the hero-prop bevel lineage and is separately deferred. And it is unnecessary: the whole cue is buyable from the one class with no gameplay contract (§3). Re-authoring a hero mesh to fix a prop's readability is paying at the wrong end. |
| **A hue/tint separation** ("make the minable ones warmer") | Fails Bar 10 outright (colour ranks last, never first) and fails Bar 3 (material-honest — warm-grey stone is warm-grey stone; the red axe-head reject is the precedent). It would also be *invisible* against a saturated mid-green world that eats hue cues, which is Bar 10's stated WHY. |
| **A rim/glow differential as the cue** | §5.3. Fine luminance comparison between two objects, needs both in frame, dies on a bright frame, and is single-channel by Bar 10's definition. Kept as equalised world-look only. |
| **Micro-animation / pulse / bob on minable nodes** | `game-juice.md` §0 caps amplitude to the calm tone ("alive and satisfying, never violent or chaotic") and §"hard don'ts" bars sustained motion. More decisively, the #172 soak verdict is already on record — *"moving grass/bushes looks weird; only the trees up in the air should move"* — which is why meadow grass ships with `_WaveAmp = 0` (`LowPolyZoneGen.GrassWaveMat`). A shoreline of throbbing rocks is the theme-park failure. **Amplitude I would accept: zero.** Bar 10 ranks motion third anyway; we have two better channels. |
| **Screen-space outline on interactives** (Sobel depth/normal Renderer Feature) | Explicitly ruled out in `lowpoly-quality.md` §3 — expensive at desktop res on a large island, and the board's edge language is a per-face chamfer, not a full-silhouette outline. |
| **Hover highlight / cursor change on the minable node** | Wrong for the control scheme: since the WASD pivot the mouse **orbits the camera**, it is not a world pointer, so there is no reliable hover state to read. It also fails the AC as written — "the affordance reads WITHOUT a click" means readable while *scanning*, and a hover cue requires you to already be pointing at the answer. And it is input-handling, which the ticket puts out of scope. |
| **HUD marker / minimap pip / floating icon or glyph** | Bar 10 makes text/iconography the last-resort fallback. It also violates the tonal anchor: the world should teach this, not a UI layer narrating it. Deferring it costs nothing; a marker layer is very hard to remove later. |
| **Re-siting the minable pools out of the scatter annulus** | Would move findability, which the pools' own placement comments deliberately tuned ("findable without heavy exploration and on the proven-walkable NavMesh loop"). The clearance ring (§5.2) buys the same separation at ~15 lines without touching discoverability. |
| **Touching the mine gate / `ClickGateDiagnostic` / arbitration** | Confirmed correct by the 2026-07-21 ClickGateDiag session; ticket AC3 forbids it. Nothing in this direction changes a single gate predicate. |
| **A second decorative rock MESH variant** (a distinct "scenery-only" shape) | Tempting and it would be the strongest possible form cue — but it is mesh authoring on 60 instances, it triples the world-scatter pass, and the transform-only route already yields a categorical aspect inversion. Held in reserve behind Tier 2. |

**Scope total for the recommendation: three call sites, ~35 lines, one capture re-baseline.** That is the
point — the Sponsor has already accepted the current state, so a modest direction that lands is worth more
than an ambitious one that queues behind the build slot forever.

---

## 8. Verification, and the falsifiable pre-soak claim

**Bar 10 channel declaration (the required naming):**

1. **FORM** — aspect-ratio inversion (wider-than-tall vs taller-than-wide) + apex-height separation ≥ 2×.
   Hue-independent. **Live on:** transform values in `BuildRock` — no shader property involved, so it
   *cannot* silently no-op the way a `HasProperty`-guarded material set can. This is a deliberate
   robustness property of choosing transform over material.
2. **POSITION** — solitude vs huddle, enforced by a 2.6u clearance ring. Hue-independent.
   **Live on:** the minable placement loops' reject list.

Both channels are independent of hue; neither depends on a shader term; the cue therefore cannot collapse
to one channel through a silent property no-op.

**Capture protocol (impl PR, from the SHIPPED exe — editor framing is not evidence):**

1. Gameplay-orbit frame containing at least one decorative cluster **and** one minable node in shot.
2. The **same frame desaturated** — Bar 10's check. The cue must be fully intact. If it is not, the direction failed, not the tuning.
3. A **side-profile** shot of a decorative cluster against the anchor sentence in §5.1 (Bar 4 /
   `lowpoly-quality.md` §0 — up-vs-down is invisible from player-eye and obvious side-on).
4. Quote the achieved clearance radius + placed pool counts from the placement log (§5.2 starvation guard).

**Soak probe targets for the Sponsor** (one-line asks, not a checklist to interpret):

- *"Walk the 9–17u loop. Without clicking anything, point at every stone you think you can mine."*
- *"Do the flat rocks still read as stone, or did they become puddles/pancakes?"* — the D1 risk.
- *"Does the shoreline still feel decorated, or did it get emptier?"* — the scale-cap risk.

**Predict-Before-Soak (falsifiable, graded against the soak):**

> With D1 + D2 shipped, the Sponsor will identify minable stones with **zero dead-click attempts** on a
> single pass of the 9–17u loop, and will NOT report the decorative rocks reading as "puddles," "pancakes,"
> or "mounds." **The prediction I expect to be wrong, if any:** that the *boulder* is now unambiguous —
> the boulder gains no positive cue in Tier 1, only the contrast of everything around it lying down, so
> "the big one is fine but I'm still not sure about it" is the most likely partial verdict, and Tier 2 (§6)
> is pre-staged for exactly that.

**Bounded convergence claim.** This document is **spec-only**: no build, no capture, no test, no shipped
evidence. Bars tested: **none.** The direction is unvalidated until a soak exercises it. What IS verified
here is the *diagnosis* — every value in §2 was read from source at `840a1c6` with the cited line numbers,
including the rim inversion (§2.1), which is a present-tense defect independent of whether this direction
is picked.

---

## 9. Candidate bar wording (ticket AC1 asks for the bar to be named)

Offered as a **candidate**, not an entry — `team/quality-bars.md` is Sponsor-confirmed and maintained via
`/name-the-bar`, so this belongs in its "Open / unconfirmed" queue, not in the Bars table, until the soak
confirms or corrects it.

> **Candidate Bar — interactive-vs-scenery must be readable by POSTURE.** Two world objects that share a
> material family must not share a *posture*. If one carries a verb and the other does not, the
> non-interactive one changes — it lies down, shrinks below the interactive class's floor by ≥2× in
> standing height, and travels in company; the interactive one stands alone. **The class that changes is
> always the one with no gameplay contract attached** (no verb, no yield, no carve, no timer, no capture
> harness) — never the hero prop. **Check: desaturate the shipped-build capture and ask "point at the ones
> you can use."** WHY: the mine gate can be perfectly correct and the world still invite dead-clicks; a
> shared-palette style deliberately removes hue as a discriminator, so posture is the only channel left
> that scales across the whole prop family.

Falsifiable, and it fails loudly: if a soak shows players still dead-clicking a lying-down slab, the bar
is wrong and the discriminator has to move up to mesh authoring.

---

## 10. Open items for the Sponsor (direction-pick, per ticket AC2)

1. **Pick the direction.** Recommended: **Tier 1 = posture + solitude** (§3–§5). Alternatives on the table
   and their costs are enumerated in §7 so the pick is informed rather than a menu.
2. **The one taste call inside Tier 1:** squash **0.60 + tilt 8–22°** (stronger read, small mound risk) vs
   the conservative **0.72 + tilt 8–22° + cap 1.15** (safer, weaker read). §5.1.
3. **Does the shoreline tolerate lower rocks at all?** The scale cap trades world "decoratedness" for
   readability. If the answer is "keep the mass," the fallback is cap unchanged at 1.55 and rely on squash
   + tilt alone (apex 0.10–0.43u; ratio still 2.1×, but a wider planar footprint).
4. **Tier 2 pre-authorisation?** If the boulder reads ambiguous at soak, may the chip skirt (§6) go
   straight into a follow-up, or should it come back for a second direction-pick?

---

## 11. Cross-references

- **Ticket** `86cav8ybj` (this direction) · `86cacewju` (hero-prop bevel/chamfer lineage — deferred, NOT
  touched) · `86cahhfkc` (RCK-1 rock rim) · `86caamnnj` (the shader rim term) · `86ca8m5zu` (rock soakfix2,
  the Y-squash precedent) · `86caffwv5` round-7/8 (the minable-node navmesh carve + invisible-wall verdict)
  · `86cadj4g7` (grass-in-stone footprint rule).
- **Bars:** `team/quality-bars.md` Bar 10 (single-channel collapse), Bar 3 (material-honest, pattern via
  geometry), Bar 4 (real-world anchor + side profile), Bar 6 (the board is a guide, not a contract).
- **Docs:** `.claude/docs/art-direction.md` (board; the rock language in `21h10_44`, `21h12_49`,
  `21h21_30`, `21h22_52`) · `.claude/docs/lowpoly-quality.md` §0 (anchor + silhouette), §1 (do-not-regress
  flat-shading/normals), §2 Rec 4 (rim), §3 (outlines ruled out) · `.claude/docs/game-juice.md` §0 +
  hard-don'ts (amplitude ceiling) · `.claude/docs/unity6-mastery.md` §2 (shared-material batching — the
  clearance ring and the transform changes add **zero** new materials and zero draw calls).
- **Board references looked at for this spec:** `inspiration/2026-06-12_21h10_44.png` (low half-buried
  rounds vs the standing shard cluster — the two postures, side by side, in the Sponsor's own reference),
  `21h12_49` (flat grey rounds settled in the grass by the stump), `21h21_30` (standing columnar outcrop
  with loose rounds at its foot), `21h22_52` (decorative stone as low half-buried litter along a path).
- **Sibling specs:** `team/uma-ux/world-look-polish-direction.md`, `team/uma-ux/pre-soak-visual-audit.md`,
  `team/uma-ux/status-effect-readability-spec.md` (the same channel-discipline reasoning applied to HUD).
