# Rock affordance direction — decorative scatter vs minable node

**Ticket:** `86cav8ybj` — *polish(art): disambiguate decorative scatter rocks from minable boulder/ore nodes*
**Scope of THIS doc:** DIRECTION only. No `Assets/**` change, no build, no capture, no test. Implementation
is the Unity-build half of the ticket and stays open behind the single build slot.
**Owner:** Uma (direction) → Drew/Devon (impl once Sponsor confirms the direction). **Reviewer:** Devon.
**Status:** direction proposal awaiting a Sponsor direction-pick (ticket AC2 forbids build-then-soak of a
guessed mechanism). Tagged `needs-soak`; unvalidated until a soak exercises it.
**Revision:** **round 2** (peer review, Devon, PR #362 `REQUEST_CHANGES` at `a4db32f`). Corrected in this
round: **§2** POSITION bullet (the "fully overlap" premise was false) · **§2.3 NEW** (`FacetedRock` apex ≠
radius; the ≥2× floor withdrawn and re-derived) · **§4 rewritten** (the bootstrap ordering premise was
inverted; POSITION re-founded as an already-shipped invariant) · **§5.1** corrected apex bands + ratios,
sink pinned proportional, ladder reworded, pebble pair addressed · **§5.2 rewritten** (clearance ring
withdrawn → measure-and-lock) · **§5.3** rim number and `veinMat` pinned · **§8** channel declaration and
capture protocol corrected · **§9** + `team/quality-bars.md` candidate bar reworded off the ratio.
Unchanged and confirmed by review: §1, §2.1, §2.2, §3, §6, §7.

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

⚠ **Every "derived" figure in this table is a NOMINAL figure** — it assumes a `FacetedRock` half-extent
equal to its `radius` argument. That assumption is **false**; the real factor spans ~0.63–1.29. **§2.3
corrects it, and the corrected numbers — not these — are what §5.1 / §8 / §9 / §10 now quote.** The table is
kept in nominal form because the *relative* diagnosis (one silhouette language, colour arguing backwards,
the ore node as the median decorative rock) is unaffected by a factor that applies to all four classes alike.

| Class | Object | Mesh (source) | World planar radius (nominal) | Apex above ground (NOMINAL — see §2.3) | Verb |
|---|---|---|---|---|---|
| Pickup pebble | `LP_Stone` | `FacetedRock(0.22, jitter 0.34)` (`LowPolyZoneGen.cs:1573`) | 0.22 × [0.35, 0.80] = **0.077–0.176u** (`:1164`) | ≈ same | `E` → 1 stone |
| **Decorative scatter** | `LP_Rock` | `FacetedRock(0.55, jitter 0.38)` (`:1324`) | 0.55 × [0.55, 1.55] = **0.303–0.853u** (`:1031`) | ≈ radius = **0.30–0.85u** (centred at the ground point) | **NONE** |
| Minable ore node | `OreNode` | `FacetedRock(0.58, jitter 0.42)` (`MovementCameraScene.cs:3005`) | **0.58u** (fixed) | 0.58 + 0.58×0.55 = **0.899u** → **corrected: 0.864u nominal / 0.682u worst case** (§2.3) | pickaxe → mine |
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
- **POSITION.** ⚠ **CORRECTED in round 2 — this bullet previously claimed the two populations "fully
  overlap." They do not, and the correction re-founds §4 entirely.** Decorative outcrop *centres* are
  hard-rejected below `spawnClearR + 4` (`:1023`), and `spawnClearR = 13f` (`:981`) — so **no decorative
  outcrop centre exists below r = 17u.** Both minable pools sit in the **9–17u** walkable loop annulus
  (ore `:2931`, boulders `:3112` — the same `9.0 + rnd × 8.0`). The only bleed is the per-rock ±1.8u
  offset (`:1027-1028`), which lets an *individual* slab reach r ≈ 17 − √(1.8²+1.8²) = **14.45u**. So the
  populations meet only in a thin `[14.45, 17]` band. POSITION is therefore **not** an empty channel —
  it is a **live, shipped, undocumented and unguarded** one. See the rewritten §4.
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

### 2.3 CORRECTION (round 2) — `FacetedRock` apex ≠ radius, and the ≥2× floor was never a floor

Round-1 arithmetic treated a `FacetedRock(r, jitter)` as having a vertical half-extent of `r`. It does not.
`LowPolyMeshes.FacetedRock` (`LowPolyMeshes.cs:373-388`) applies, per instance and per vertex:

- `sy = 0.85f + rnd × 0.18f` → **[0.85, 1.03]**, one draw per instance (`:375`);
- `rj = 1f + (rnd − 0.5f) × jitter` → **[1 − j/2, 1 + j/2]**, one draw **per vertex** (`:382`);
- an absolute isotropic wobble of `radius × jitter × 0.22f`, i.e. **±`radius × j × 0.11`** (`:386-388`).

So the vertical half-extent factor is **`V = sy × rj(pole) ± j × 0.11`**, and the planar one is
**`P = sx × max(rj over the equatorial verts) ± j × 0.11`** with `sx = 0.92 + rnd × 0.20` (`:373`).

| Class | `jitter` | `V` floor | `V` nominal | `V` ceiling |
|---|---|---|---|---|
| Pickup pebble | 0.34 | 0.85×0.83 − 0.037 = **0.668** | 0.94 | 1.03×1.17 + 0.037 = **1.242** |
| Decorative scatter | 0.38 | 0.85×0.81 − 0.042 = **0.647** | 0.94 | 1.03×1.19 + 0.042 = **1.268** |
| Boulder | 0.40 | 0.85×0.80 − 0.044 = **0.636** | 0.94 | 1.03×1.20 + 0.044 = **1.280** |
| Ore node | 0.42 | 0.85×0.79 − 0.046 = **0.626** | 0.94 | 1.03×1.21 + 0.046 = **1.293** |

**The asymmetry that matters, and it is the whole answer to "name the spread."** The mesh is a subdiv-1
octahedron — 18 verts, 32 facets (`:1320`): one at `n.y = 1`, four at `0.7071`, eight at `0`, then mirrored.
So `V` is set by **essentially ONE vertex** (the pole; the next ring only overtakes it when
`0.7071 × rj > rj(pole)`, i.e. `rj(pole) < 0.841` — reachable but uncommon), which gives `V` a genuinely
low, broadly-spread tail. `P` is the **max over twelve** planar-bearing verts, so it concentrates near the
top of its range (≈ 0.96–1.38, nominal ≈ 1.00–1.05). **Vertical extent is one draw; planar extent is a
maximum of twelve.** That single fact decides which of this spec's two FORM guarantees survives:

- **The aspect inversion SURVIVES every tail.** It fails only if `V × q ≥ P` (`q` = the D1 squash). At
  `q = 0.60` that needs `V ≥ P / 0.60`, i.e. `V ≥ 1.60` at realistic `P` — and `V` cannot exceed **1.268**.
  Unreachable. The inversion is **categorical at every draw**, which is exactly why §3 leads with it.
- **The ≥2× apex-height separation DOES NOT survive.** Corrected numbers in §5.1. It holds ~3× nominal and
  degrades to **~1.3× worst-case**. It was quoted in round 1 as a floor; it is a *nominal*, and the
  round-1 figures (2.7× / 2.1×) were neither nominal-nominal nor worst-case but an inconsistent middle.

**Consequences, applied throughout this round-2 revision:** the height ratio is demoted from a *derived
guarantee* to a **measured report with a nominal target**; the impl must encapsulate the shipped
`Renderer.bounds` (the idiom `MineOre.TryPlanarFootprint`, `MineOre.cs:812`, already uses) and print the
achieved **minimum** ratio and the achieved **maximum** `b/a` aspect across every instance — failing the
gate on any instance whose aspect inversion is violated, and *reporting* (not failing) the height ratio.
§9's candidate bar and the `team/quality-bars.md` row are reworded to match.

**One correction to the reviewer's own figure, for the record:** the round-2 review estimated the worst
realized pair at "~1.7×" against "the shortest of the ~7 deterministic ore nodes." Seven is the **boulder**
pool (`BoulderPoolSize = 7`, `MovementCameraScene.cs:3049`). The **ore** pool is
`OreNodePoolSize => IronDifficultyPresets.Easy.OreNodeCount` (`:2871`) = **24 placed** (14 active at the
Medium default, `:2967`), each with its own mesh seed `86300 + i × 17` (`:2946`) — so the low `V` tail is
sampled across **24** independent draws, not 7, and the honest worst case is **lower** than 1.7×, not
higher. Figures in §5.1.

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

## 4. SECONDARY CHANNEL — **POSITION: the loop vs the horizon** (REWRITTEN, round 2)

> **One sentence:** *Stone you can work lives in the loop where you live. Stone that is only scenery lies
> out toward the horizon.*

### 4.1 What round 1 got wrong — two errors, and the second is the bigger one

**Error 1 — the ordering premise was inverted.** Round 1 asserted that the scatter runs *before* the
minable pools, and concluded the exclusion had to run minable-side. **The real order is the exact
opposite,** and I verified it myself at `main` (`b9abf7b`) rather than taking the correction on trust:

| # | Site | What it authors |
|---|---|---|
| 1 | `BootstrapProject.Run` **line 96** → `BuildBootScene()` → **`:559`** `MovementCameraScene.Author(camGo)` → `BuildOreNodes` **`:410`** / `BuildBoulders` **`:422`** | **both minable pools, complete** |
| 2 | `BootstrapProject.Run` **line 105** → `WorldBootstrap.BuildEnvironment()` → **`WorldBootstrap.cs:158`** `LowPolyZoneGen.BuildZone` → **`LowPolyZoneGen.cs:645`** `ScatterIslandProps` → the rock loop **`:1019-1036`** → `BuildRock` **`:1304`** | **every `LP_Rock`** |

The bootstrap states the order in its own comments twice — line 108 ("*BuildEnvironment has authored the
LowPolyScatter root … it did NOT exist at BuildChopTree time — the boot scene's player/craft/chop are
authored at line 96, the environment scatter only lands here*") and line 116 ("*BuildWiredStone
(pre-scatter, BuildBootScene) … ScatterIslandProps (here, inside BuildEnvironment)*"). `BuildChopTree` is
`:374`, *earlier* in `Author` than `BuildOreNodes` `:410` — so if the scatter root does not exist at `:374`
it certainly does not exist at `:410`/`:422`.

**Consequence, stated as the reviewer stated it, because it is correct:** a minable-side reject list would
collect **zero** `LP_Rock` positions, the ring would never reject a candidate, no position would move, no
baseline would shift — **and every test would pass.** The starvation guard would have logged a
healthy-looking `2.6u` from a pass that examined nothing. That is precisely the silent-no-op failure mode
§8 claims immunity from, arriving through the placement path instead of the material path. Round 1's §8
declared two channels; it would have shipped one.

**Error 2 — and this one survives the re-siting: the channel has almost nothing to exclude.** The
reviewer's proposed remedy (a post-scatter cull, correctly sited) fixes the *mechanism* but not the
*premise*. §2's corrected POSITION bullet is why:

- decorative outcrop **centres** are hard-rejected below `spawnClearR + 4f` (`:1023`) and
  `spawnClearR = 13f` (`:981`) → **no outcrop centre below r = 17u**;
- both minable pools draw `rad = 9.0 + rnd × 8.0` → **r ∈ [9, 17]** (ore `:2931`, boulders `:3112`);
- the per-rock offset is `(rnd − 0.5) × 3.6f` on each of x and z (`:1027-1028`) → an individual slab
  reaches inward to at most **r = 17 − √(1.8² + 1.8²) = 14.45u**.

So a rock can only ever come near a node inside the thin `[14.45, 17]` band, and only from an outcrop whose
centre sits in `[17, 19.55]`. Outcrop centres are **areally** uniform (`rr = plantOuterR × √rnd`, `:1021`)
over `plantOuterR = IslandShoreR + CoastIrregAmp = 120 + 26 = 146u` (`:979`, `:237`, `:271`), so the
fraction of outcrops eligible to contribute at all is `(19.55² − 17²) / (146² − 17²) = 91 / 21027 ≈ **0.43%**`.
With 60 rocks (`rockTarget = 60`, `:1018`) in clusters of 2–4 (`:1025`) ≈ 20 outcrops, that is **≈ 0.09
eligible outcrops** across the whole island. **The expected number of decorative rocks within 2.6u of any
minable node is far below one.**

**Therefore a cull pass — however correctly sited — would cull nothing, and would be the same silent no-op
one layer further along.** I am not taking the suggested re-siting as the fix. The re-siting is *necessary*
(it is the only order in which either class can see the other, and §5.2 uses it), but it is not
*sufficient*, and shipping ~15 lines of culling machinery whose expected yield is zero would be exactly the
kind of beat this spec's §7 exists to refuse.

### 4.2 What POSITION actually is here — an invariant that already ships, unnamed and unguarded

Read the shipped radial rules as one table and the channel is already there:

| Stone class | Radial domain | Verb |
|---|---|---|
| Minable ore node | r ∈ [9, 17] (`:2931`) | pickaxe → mine |
| Minable boulder | r ∈ [9, 17] (`:3112`) | pickaxe → mine |
| Pickup pebble | r ≥ 13 (`:1155`, `spawnClearR`) | `E` → 1 stone |
| **Decorative scatter** | **r ≥ 17** centres (`:1023`, `spawnClearR + 4`) | **NONE** |

**Every stone class that carries a verb reaches into the survival loop. The one class with no verb is the
one class excluded from it.** That is Bar 10's POSITION rank in its literal form — *a fixed slot per kind* —
and it is live in the shipped build today. It was simply never named, never documented, and is protected by
**nothing**: a future scatter re-tune that lowers `spawnClearR`, widens the annulus, or raises the ±1.8u
cluster spread would silently dissolve it, and no test would notice.

So the honest job for this channel is **not to build it. It is to name it, measure it, and lock it** — §5.2.

**Why this is a better outcome than round 1's design, not a retreat.** Round 1's minable-side reject would
have moved boulder and ore positions (different draws consumed), forcing a `BoulderVerifyCapture` +
`MineVerifyCapture` re-baseline in the same PR. The corrected reading needs **zero** placement change, so
the minable pools never move and **no capture baseline shifts.** The seed-42 stream is untouched either way
— but now trivially so, because nothing is added to or removed from any RNG.

**And the reviewer's seed-lock point is granted:** my round-1 prohibition ("*do not do it scatter-side*")
was over-broad. It is correct only about **rejecting inside the scatter loop** — that consumes different
draws and moves the island's trees. It is **not** correct about a **post-scatter pass**, which runs after
the seed-42 stream is fully consumed and therefore cannot perturb it. The project already has that idiom
four times over (`WireChopScatterRoot` `BootstrapProject.cs:113`, `WireStoneScatterRoot` `:123`,
`WireBerryBushes` `:134`, `WireWorldLookConsole` `:141`) — all of which exist *specifically because* the
scatter does not exist at `BuildBootScene` time. §5.2's measurement pass is sited there, as a fifth sibling.

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
| Sink | none (centred at `GroundPoint`) | **`−0.08 × s` in Y — PROPORTIONAL, pinned (N3)** | Buries the low edge so it reads *settled into the grass*, not *placed on it*. **Proportional, not fixed** — see the pin below. |
| Shadow casting | ON (`MakeMeshObject` default) | **keep ON** | The contact shadow of a low slab is the ground-contact evidence. Do not "optimise" it off. |

`MakeMeshObject` parents the mesh at identity local transform (`:1424`, `SetParent(parent, false)` with no
local offset), so the mesh is centred on the `LP_Rock` origin and **apex above ground = mesh vertical
half-extent × `localScale.y` − sink.** Confirmed by read, because the whole §5.1 derivation rests on it.

**PIN (N3) — the sink is `−0.08 × s`, PROPORTIONAL, and this reverses the reviewer's suggested default.**
The review asked me to state FIXED explicitly so an implementer would not "improve" it to `−0.08 × s`. The
corrected §2.3 arithmetic says the opposite, and the number decides it: at the low tail
(`s = 0.55`, `V = 0.647`) the pre-sink apex is `0.55 × 0.647 × 0.60 × 0.55 = 0.117u`, so a **fixed** 0.08
sink consumes **68%** of the smallest slab and leaves a ~0.04u nub — buried, not settled. Proportional
leaves `0.117 − 0.044 = 0.073u` (≈0.085u once the tilt term is included), which is a stone in grass.
A larger stone settling deeper into turf is also the physically honest read. **Safety clamp:** the sink must
never exceed **40% of that instance's measured pre-sink apex** — free to enforce, because §2.3 already
requires the impl to encapsulate `Renderer.bounds` per instance.

**Resulting bands, CORRECTED per §2.3** (round 1 quoted `0.10–0.33u` on the false `apex ≈ radius`
assumption). Apex = `√(b²cos²θ + a²sin²θ) − 0.08 s`, where `b = 0.55 · V · q · s`, `a = 0.55 · P · s`, and
`θ` is the combined tilt of the local +Y axis (Euler X and Z each 8–22° → θ ≈ 11.3–31.1°). **Tilting a
wider-than-tall shape RAISES its silhouette** — an effect round 1 omitted entirely, and it costs ~+18% at
the ceiling:

| | `s` | `V` | `θ` | Apex |
|---|---|---|---|---|
| Floor | 0.55 | 0.647 | 11.3° | **0.085u** |
| Nominal | 0.90 | 0.94 | 21° | **0.243u** |
| Ceiling (worst case) | 1.25 | 1.268 | 31.1° | **0.518u** |

Against the ore node — the minable floor — at **0.864u nominal** and **0.682u worst-case**
(`0.58 × (V + 0.55)`, `:3005`/`:3007`), the honest apex-height separation is:

| | Ratio | vs the round-1 claim |
|---|---|---|
| **Nominal** (both classes at mid draws) | **3.55×** | round 1 said 2.7× — it was neither nominal nor worst-case |
| **Worst case** (tallest slab vs shortest of 24 ore nodes) | **1.32×** | **below the ≥2× §8 and the bar asserted** |

**So the ≥2× height floor is withdrawn as a guarantee.** It is now stated as *nominal ≈ 3.5×, worst case
≈ 1.3×, measured and reported per build*. What carries the channel instead is the **aspect inversion**,
which §2.3 shows is categorical at every draw: `b/a = V · q / P` ≈ **0.54 → 1.86 : 1 wider-than-tall**
nominal, and mathematically unable to invert while `q = 0.60` (it would need `V ≥ 1.60`; `V` caps at 1.268).

**The three-tier ladder (N5 — reworded; round 1 read as a height ordering and the middle rung is the
shortest).** Ordered by *what your body does*, not by height:

> **you walk over it** (decorative slab — lies flat, tilted, 0.09–0.52u, no verb) → **you crouch and take it
> in your hand** (pickup pebble — round and proud, 0.05–0.22u, `E`) → **you plant your feet and swing at it**
> (ore node / boulder — stands up, 0.68u and above, pickaxe).

**PEBBLE PAIR (N4) — the honest answer: posture separates them, height does NOT.** Corrected per §2.3, the
pebble apex is `0.22 × V × [0.35, 0.80]` = **0.051–0.219u** (`:1164`, jitter 0.34), which **overlaps** the
decorative slab's 0.085–0.518u across most of the pebble's range — a worse overlap than the review's
planar-only 1.7× estimate suggested. The separation is therefore carried **entirely by posture**, and it is
robust: the pebble is **uniformly** scaled (`b/a ≈ V/P ≈ 0.90`, i.e. near-equidimensional and proud) and
untilted beyond its yaw, while the slab is 0.60-squashed **and** tilted 8–22° (`b/a ≈ 0.54`). 1.86 : 1
against 1.11 : 1 is a categorical read, and it is the same channel the primary cue rides — so it cannot
regress independently. Both classes also lie **below** the minable floor, so neither is confusable with a
swing target. **Residual risk ACCEPTED and named:** a player may occasionally press `E` at a decorative slab
expecting a pebble. The cost is one wasted keypress with no animation commitment — categorically cheaper
than a wasted pickaxe swing, which is the failure this ticket exists to remove. It is not worth a beat.

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
**1.15** — a weaker but still categorical aspect inversion (**1.48 : 1** nominal, corrected from round 1's
1.39 : 1 per §2.3; `a/b = P / (V·q) = 1.00 / (0.94 × 0.72)`). Do not go below squash 0.55; that crosses into
disc/pancake territory and stops reading as stone.

**Bookkeeping that needs NO change (do not "fix" it):** `RockFootprintRadius = 0.55f` (`:1388`) is
consumed by `OverlapsAnyRock` as `RockFootprintRadius * scale`, so the grass- and pebble-exclusion
footprints shrink proportionally with the new scale cap and stay correct automatically.

### 5.2 D2 — REWRITTEN (round 2): **measure the shipped invariant and lock it. Do not build a ring.**

Round 1 specified a minable-side reject list. §4.1 shows that is impossible (the pools author first) *and*
pointless (the radial separation leaves it ~nothing to reject). D2 is therefore re-scoped from *place
differently* to **assert what already holds** — and the assertion is the deliverable, because an invariant
nobody measures is an invariant that dissolves at the next re-tune.

**D2a — the invariant test (REQUIRED, and it is the whole of D2's normal path).**

A test over the **saved `Boot.unity`** (not a live bootstrap — the shipped scene is the artifact that
matters, per the editor-vs-runtime discipline) that:

1. Collects every `LP_Rock` under the `LowPolyScatter` root, and every `OreNode` (`MineOre.OreNodeName`,
   `MineOre.cs:66`) under `"OreNodes"` (`MovementCameraScene.cs:2900`) plus every `Boulder`
   (`MineBoulder.BoulderNodeName`, `MineBoulder.cs:64`) under `"Boulders"` (`:3079`). Discovery by root name
   has precedent in-tree: `BuildBoulders` already does `GameObject.Find("OreNodes")` at `:3098`.
2. **FAILS LOUDLY if either set is empty.** This is the R1 regression guard, stated as the review asked:
   *if the `LP_Rock` set comes back empty, `Debug.LogError` and fail — never a silent zero.* A pool-count
   assertion would not have caught round 1's bug; a **non-empty-both-sets** assertion is the one that would.
3. Computes and **prints** `min` planar XZ distance across the full cross-product, plus the count of pairs
   below 2.6u. **Planar, deliberately:** the minable pools are placed at `y = 0` (`:2934`, `:3115`) before
   the terrain exists at all (line 105), so any Y-inclusive metric would be measuring bootstrap order, not
   world layout. Planar distance is terrain-height-agnostic and is the same metric the shipped reject lists
   already use (`PlanarDistXZ`, `:3039`).
4. Asserts `min ≥ 2.6u`. **The test cannot pass vacuously** — it emits a measured number every run, so a
   green result carries evidence rather than silence.

**The 2.6u threshold, re-derived (and correcting round 1's single citation).** There are **two** shipped
reaches, not one: ore `mineRadius = 2.2f` (`:2966`) and boulder `mineRadius = 2.4f` (`:3147`). 2.6u clears
the larger with margin, so **nothing decorative sits inside the reach the pickaxe actually has** — the
property that makes this a readable rule rather than a cosmetic one. Round 1 cited only the boulder's 2.4;
the number survives the correction, its justification is now complete.

**D2b — the conditional remedy (only if D2a measures a violation).** If and only if `min < 2.6u`, cull the
offending `LP_Rock` instances in a **post-scatter pass** in `BootstrapProject.Run`, after
`WorldBootstrap.BuildEnvironment()` (line 105) and alongside the four existing `Wire*` siblings (`:113`,
`:123`, `:134`, `:141`). Properties, all of which are why this siting is right:

- **Cull, never reject.** Destroying an already-placed object consumes **zero** RNG draws, so the seed-42
  stream is byte-identical and every tree, grass tuft, stick, bush and surviving rock stays exactly where it
  is today. Rejecting inside the scatter loop would move all of them — that part of round 1's warning was
  correct and stands.
- **The minable pools never move**, so — unlike round 1's design — **`BoulderVerifyCapture` and
  `MineVerifyCapture` need no re-baseline.** Only `RockVerifyCapture` (which centroids the `LP_Rock` set,
  `RockVerifyCapture.cs:57-64`) shifts, and only if a rock was actually culled.
- **Company preserved (the §4 huddle read).** A per-rock cull can strand a cluster member alone. If any cull
  occurs, follow it with **one** non-cascading pass removing any survivor with no `LP_Rock` peer within 3.6u
  (the cluster's own diameter, `:1027-1028`). Single evaluation against the post-cull set — a mutual pair is
  company and survives. Deterministic and bounded; no iteration to fixpoint.
- **Known harmless side effect:** pebbles reject against `rockFootprints` (`:1161`, `OverlapsAnyRock`) during
  the scatter, so a pebble suppressed by a rock that is later culled simply does not exist. A handful of
  missing pebbles out of 70 (`:1149`); not worth compensating.

**Predict-Before-Soak for D2 specifically (falsifiable, graded by the impl's own log):**

> **D2a will measure `min > 2.6u` and cull count `0`.** Arithmetic in §4.1: only ~0.43% of outcrop centres
> are radially eligible to contribute, ≈0.09 outcrops island-wide. **If that holds, D2 ships as a guard only
> — one test, zero scene change, zero capture re-baseline, and D2b is never written.** If it fails, D2b is
> already specified and the measurement tells the implementer exactly how many instances it must handle.

**N6 is moot by construction.** Round 1 was asked to also cite the ore loop's bounds (`guard < 8000`
`:2924`, landmark 3.5u `:2937`, self-spacing 3.0u `:2940`) because §5.2 cited only the boulder side. The
re-scope touches **neither** placement loop, so there is no reject list to extend and no starvation to guard
— the round-1 starvation paragraph is withdrawn along with the mechanism it protected.

### 5.3 D3 — De-invert the rim, as hygiene, NOT as the cue

**PIN (N2a) — the number is exactly `0.12`, not "at least 0.12".** Round 1's "at least" contradicted this
section's own "let it be equal across the stone family," and an implementer had to guess. Set
`_RimIntensity = 0.12f` — the same literal as `RockRimIntensity` (`LowPolyZoneGen.cs:96`) — with
`_RimPower 3` to match, on **all three** minable materials, `HasProperty`-guarded exactly as the rock
material does it (`:1937`); an unguarded set on a material whose shader lacks the property is the silent
no-op Bar 10 explicitly warns about. Mirror `_AOStrength 0.5f` onto them too, so the minable stone is not
the only stone in the world with flat crevices. **Ceiling, and it is the point of §5.3:** do **not** raise
any minable material above 0.12 to "make the ore pop" — that is the rim-differential-as-cue route this
section rejects.

**PIN (N2b) — `veinMat` (`MovementCameraScene.cs:2896`) is INCLUDED. Yes to both rim and AO.** Three
reasons, in priority order: (1) **uniformity is the entire content of D3** — making the vein the single
exception would be arbitrary and would reintroduce a differential; (2) the `_AOStrength 0.5` crevice
darkening lands precisely where each vein lump meets the body, which is the contact shadow that makes the
lump read as a **separate mass** rather than a colour patch — so it strengthens the ore node's one genuine
form discriminator (§6, `:3018-3036`, Bar 3 pattern-via-geometry); (3) the rim at 0.12 is a whisper on a
0.15–0.20u lump (`:3033`), well below anything that reads as glow. **Watch item for the soak:** the vein is
the one *rust*-coloured surface in the stone family, so if the Sponsor reports the ore nodes reading
"highlighted" or "gamey," `veinMat`'s rim is the first thing to drop back to 0 — not the grey materials'.

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
| **Re-siting the minable pools out of the scatter annulus** | Would move findability, which the pools' own placement comments deliberately tuned ("findable without heavy exploration and on the proven-walkable NavMesh loop"). **And round 2 makes it doubly unnecessary:** §4.2 shows the radial separation the re-siting would buy **already ships** (`r ≥ 17u` decorative vs `[9,17]` minable), so the only gap was that nothing guarded it — which D2a closes with a test, not a placement change. |
| **Touching the mine gate / `ClickGateDiagnostic` / arbitration** | Confirmed correct by the 2026-07-21 ClickGateDiag session; ticket AC3 forbids it. Nothing in this direction changes a single gate predicate. |
| **A second decorative rock MESH variant** (a distinct "scenery-only" shape) | Tempting and it would be the strongest possible form cue — but it is mesh authoring on 60 instances, it triples the world-scatter pass, and the transform-only route already yields a categorical aspect inversion. Held in reserve behind Tier 2. |

**Scope total for the recommendation: three call sites, ~35 lines, one capture re-baseline.** That is the
point — the Sponsor has already accepted the current state, so a modest direction that lands is worth more
than an ambitious one that queues behind the build slot forever.

---

## 8. Verification, and the falsifiable pre-soak claim

**Bar 10 channel declaration (the required naming):**

1. **FORM** — the **aspect-ratio inversion** (wider-than-tall vs taller-than-wide). Hue-independent.
   **Live on:** transform values in `BuildRock` — no shader property involved, so it *cannot* silently no-op
   the way a `HasProperty`-guarded material set can. This is a deliberate robustness property of choosing
   transform over material. **CORRECTED (round 2):** round 1 declared this channel as "aspect inversion +
   apex-height separation ≥ 2×". The **≥2× is withdrawn** — §2.3 shows it is a nominal (≈3.5×) that degrades
   to ≈1.3× at the tail. The inversion alone carries the channel, and §2.3 proves it is categorical at every
   draw (`V·q ≥ P` is unreachable at `q = 0.60`). The height ratio is now **measured and reported**, not
   claimed: the impl encapsulates `Renderer.bounds` per instance and prints the achieved **minimum** ratio
   and **maximum** `b/a`, failing on any instance whose inversion is violated.
2. **POSITION** — the **radial domain split**: every verb-bearing stone class reaches into the 9–17u
   survival loop; the one verb-less class is excluded below r = 17u. Hue-independent. **Live on:** the
   shipped scatter and pool radial rules (`:1023`/`:981` vs `:2931`/`:3112`/`:1155`) — **already in the
   build today**, per §4.2. **CORRECTED (round 2):** round 1 declared this as "solitude vs huddle, enforced
   by a 2.6u clearance ring on the minable placement loops' reject list." That ring was unbuildable (wrong
   bootstrap order) and would have had ~nothing to reject (§4.1). What ships instead is **D2a, an invariant
   test that measures and locks the existing split** — so this channel's integrity is now *asserted* rather
   than *assumed*.

Both channels are independent of hue; neither depends on a shader term; the cue therefore cannot collapse
to one channel through a silent property no-op.

⚠ **Honest caveat on POSITION's strength, since it is now an already-shipped channel.** It has been live all
along and the world still invited dead-clicks — so POSITION alone provably does not teach the distinction
(you cannot learn "the far ones are scenery" while both classes look identical). Its role here is
**reinforcing confirmation** for a player who has already learned the posture rule, plus a global tonal
read that fits the north-star (*worked stone near home, scenery out toward the horizon*). The load-bearing
new work is FORM. I would rather state that than claim two equally-strong channels.

**Capture protocol (impl PR, from the SHIPPED exe — editor framing is not evidence):**

1. Gameplay-orbit frame containing at least one decorative cluster **and** one minable node in shot.
2. The **same frame desaturated** — Bar 10's check. The cue must be fully intact. If it is not, the direction failed, not the tuning.
3. A **side-profile** shot of a decorative cluster against the anchor sentence in §5.1 (Bar 4 /
   `lowpoly-quality.md` §0 — up-vs-down is invisible from player-eye and obvious side-on).
4. **Quote four measured numbers** (round 2 — replaces "the achieved clearance radius + placed pool counts",
   which described the withdrawn placement mechanism): (a) D2a's measured **minimum planar distance**
   `LP_Rock` → minable node, and the sub-2.6u pair count; (b) the `LP_Rock` and minable set **sizes**, to
   prove neither was empty; (c) the achieved **minimum apex-height ratio** across all instances; (d) the
   achieved **maximum `b/a` aspect** across all `LP_Rock` — the one figure that must hold below 1.0.

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
> non-interactive one changes **aspect ratio** — it crosses from taller-than-wide to **wider-than-tall**, and
> it stays there on **every instance**; the interactive one stands up. **The class that changes is
> always the one with no gameplay contract attached** (no verb, no yield, no carve, no timer, no capture
> harness) — never the hero prop. **State the cue as a categorical inversion, never as a size ratio:** on a
> procedurally-jittered mesh a height ratio is a *nominal* that collapses at the tail (`86cav8ybj` §2.3 —
> a claimed ≥2× floor measured 1.3× worst-case), whereas an aspect inversion holds at every draw and is
> cheap to assert per instance. **Check: desaturate the shipped-build capture and ask "point at the ones
> you can use"; and gate CI on the measured worst-case aspect, not on a derived constant.** WHY: the mine
> gate can be perfectly correct and the world still invite dead-clicks; a shared-palette style deliberately
> removes hue as a discriminator, so posture is the only channel left that scales across the whole prop
> family.

Falsifiable, and it fails loudly: if a soak shows players still dead-clicking a lying-down slab, the bar
is wrong and the discriminator has to move up to mesh authoring.

---

## 10. Open items for the Sponsor (direction-pick, per ticket AC2)

1. **Pick the direction.** Recommended: **Tier 1 = posture + solitude** (§3–§5). Alternatives on the table
   and their costs are enumerated in §7 so the pick is informed rather than a menu.
2 + 3. **The one taste call inside Tier 1 — three options, with HONEST numbers (round 2).** Round 1 quoted
   these at **2.7×** and **2.1×**; §2.3 shows both figures were an inconsistent middle — neither nominal nor
   worst-case. Corrected, with worst-case shown **alongside** nominal, because a number you pick between must
   be the number you actually get:

| Option | Squash `q` | Scale cap | Apex band | Aspect (nominal) | Height ratio **nominal** | Height ratio **worst case** |
|---|---|---|---|---|---|---|
| **A — recommended** | 0.60 | 1.25 | 0.09–0.52u | 1.86 : 1 | **3.55×** | **1.32×** |
| **B — conservative** ("mound" risk-averse) | 0.72 | 1.15 | 0.10–0.54u | 1.48 : 1 | **3.18×** | **1.27×** |
| **C — keep the mass** (cap unchanged) | 0.60 | 1.55 | 0.09–0.64u | 1.86 : 1 | **3.04×** | **1.06×** |

**Read the last column, because it is the whole reason this table was re-derived.** All three clear 2×
comfortably at nominal. **None** clears 2× at the tail. And **option C's worst case is 1.06× — the tallest
decorative slab essentially REACHES the apex of the shortest of the 24 ore nodes.** That is the concrete
cost of keeping the 1.55 cap, and round 1's "ratio still 2.1×" concealed it entirely. Option A has the best
worst case and the stronger read; C is the one I would now argue against on evidence rather than taste.

  In all three, the **aspect inversion holds at every draw** (§2.3) — so what the Sponsor is really choosing
  is how much "decoratedness" the shoreline keeps, not whether the cue works.
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
  transform changes add **zero** new materials and zero draw calls; D2a adds no runtime work at all — it is
  an editor/test-time measurement, and its normal outcome is zero scene change).
- **Board references looked at for this spec:** `inspiration/2026-06-12_21h10_44.png` (low half-buried
  rounds vs the standing shard cluster — the two postures, side by side, in the Sponsor's own reference),
  `21h12_49` (flat grey rounds settled in the grass by the stump), `21h21_30` (standing columnar outcrop
  with loose rounds at its foot), `21h22_52` (decorative stone as low half-buried litter along a path).
- **Sibling specs:** `team/uma-ux/world-look-polish-direction.md`, `team/uma-ux/pre-soak-visual-audit.md`,
  `team/uma-ux/status-effect-readability-spec.md` (the same channel-discipline reasoning applied to HUD).
