# Island 2.0 — ticket DRAFT (Priya, 2026-07-02)

> **Status:** DRAFT for orchestrator review → ClickUp create. Report-only (Priya can't reach ClickUp — orchestrator creates the parent + children on list `901523878268`).
> **Route:** Sponsor picked "Priya drafts" over a `/grill-me` at the #226 walk-soak (2026-07-02). This is the destination-shaping doc; the dev picks the mechanism inside the constraints.
> **Feeds:** the parent below is the L umbrella; the "Suggested split" section proposes 4 child tickets the orchestrator can create + dispatch against the doubled build lane. Each child is written dispatch-ready.

---

## PARENT TICKET

**Title:** `feat(world): island 2.0 — bigger + more diverse next-island (mountains, rocky walls, stone slabs, tree/vegetation variety) [L, SPLITS]`

**Tags (orchestrator to apply — Priya can't set tags via MCP):** `feat`, `world`, `poc`
**List:** `901523878268` · **Status:** `to do`

### Source

Sponsor walk-soak verdict on **PR #226** (merged; the NextIslandPoc "big-island" proof — ticket `86caa9zpp`), verbatim 2026-07-02:

> "the island feels big for a POC, the Island should be bigger and more diverse with more mountains, rocky walls, huge stone slabs, different trees, bushes, vegetation."

Climb is **Sponsor-APPROVED** (the walkable snow-cap peak works — do not regress it). Perf on #226 read **smooth**. This ticket takes the proven #226 base and grows it toward the locked north-star: *the world feels BIG and ENDLESS — a journey.* It is the "island half" of the M-U5 journey arc; the boat/sail half (`86caa9zju`) stays deferred + OOS.

### 🎯 Destination (what + why) — LEAD with this

The next-island POC reads as a **markedly bigger, markedly more diverse** island than #226: a varied landmass with **more than one mountain**, **rocky walls / cliff faces**, **huge stone slabs**, and a **layered mixed forest + vegetation** (multiple tree species, bushes, ground cover) — while still holding a smooth frame rate at its new size and keeping the Sponsor-approved climbable peak.

**Strip-test** (strip every AC below — would a competent dev still know what "done" looks like?): *The Sponsor walks the new island and it feels like a real, wild, diverse place — bigger than before, with dramatic rock features and a rich varied forest — not the sparser single-species #226 island; and it doesn't stutter.* That sentence is the destination; the rest is constraints + tunables.

**Relevant quality bars** (predict against these, not a guess — `team/quality-bars.md`):
- **Bar 1 — organic / irregular, never geometric.** The bigger coast, the extra mountains, the rock features must all read organic — varied, asymmetric, faceted low-poly. No circles/stars/grids.
- **Bar 4 — physical features read as the real thing on the FIRST try.** Every new content class (rocky wall, stone slab, extra mountain) opens with a plain real-world anchor sentence + ships a **side-profile silhouette capture** from the shipped exe. A rocky wall is a near-vertical rock face you can't walk up; a stone slab is a big flat-topped boulder sitting ON the ground. Fix the CAUSE, not a metric.

### 🔒 Constraints (must obey — these prevent merge collisions + protect invariants)

- **EXTEND the #226 POC gen — do NOT rewrite it.** The base is on `main`: `Assets/Scripts/Editor/NextIslandPocGen.cs` (terrain + water + mountain + colour), `NextIslandPocScatter.cs` (forest/rock/grass scatter, deterministic from `seed+555`, exposes `treeTarget`), `NextIslandPocScene.cs` (stand-alone `Assets/Scenes/NextIslandPoc.unity` + `PocNavMesh.asset`, two-step build via `build_poc.sh`). Grow these files' `const` tunables + add new content classes to the existing scatter/gen idioms. *Why:* #226 is Sponsor-approved and perf-proven at its size; a rewrite re-opens settled ground (climb tuning, NavMesh coverage, the perf verdict) and wastes the proof.
- **Do NOT touch the seed-42 start island** (`LowPolyZoneGen.cs` / `Boot.unity` / `WorldBootstrap.cs`) — byte-untouched, "close to perfect", LOCKED (`[[world-is-big-round-island]]`; poly-plan §1.1). The POC stays a stand-alone parallel generator; it does not load, reference, or wire to Boot.unity. *Why:* the start island is the soaked baseline; the POC is where the big-world experiment lives.
- **Snow-cap faceting is `86cahmxh6` (SEPARATE in-flight ticket) — reference, do NOT duplicate.** `86cahmxh6` re-shapes the snow band on `NextIslandPocGen` from a smooth dome to a chunky/faceted peak. If island-2.0's terrain/silhouette child touches the same snow-band code, **sequence behind `86cahmxh6`** (or fold explicitly, orchestrator's call — do NOT double-author the snow faceting). *Why:* two tickets editing the same snow band race on the committed scene.
- **New content classes ship caster-policy-aware via the G6-1 args.** New props (rocky walls, stone slabs, extra vegetation) must be created through the `MakeMeshObject(castShadows, staticFlags)` chokepoint pattern (Wave1-A `86cahhff6`, on the start-island path — mirror the pattern in the POC scatter): decoration-scale props (grass, bushes, small rocks) ship `ShadowCastingMode.Off` + `BatchingStatic`; large hero features (rocky walls, big slabs, the mountains) keep shadows + static-batch. *Why:* the shadow pass is the dominant configured GPU cost (poly-plan §headline) — new props inheriting the wrong caster policy silently re-inflates it exactly where this ticket is trying to scale up.
- **T-A: tonal variation is vertex-colour-baked, NEVER per-material.** Tree/bush hue variety, rock tinting, ground tones — all via vertex colour on the shared `FarHorizon/LowPolyVertexColor` shader (the one `MakeMeshObject` path). *Why:* per-material tint multiplies the material count → breaks the SRP-Batcher + the shared-palette ~1-draw-call model (poly-plan §4 T-A).
- **Faceted flat-shaded family, sub-1.0 warm palette.** New rock/mountain/slab meshes use the outward-winding + explicit-per-face-normal idiom (`FacetedRock` / `FacetedMountain` in `LowPolyMeshes.cs`); NEVER `RecalculateNormals` on them (self-smooths → loses facets). All palette colours sub-1.0 all channels; near-neutral rock tints through `QuantizeFine` (avoids the pink-cast bug). Do NOT flat-shade the welded terrain (the smooth-normal roll IS the look). *Why:* poly-plan §1.4 / §1.8 + `lowpoly-quality.md` §1 — these are closed bugs; touching them re-opens them.
- **Committed-generated assets — regen + commit or the build ships nothing.** Every gen/scatter/const change ships ONLY after re-running the POC build chain (`NextIslandPocScene.Build`) and committing the regenerated `NextIslandPoc.unity` + `PocNavMesh.asset`. A code-only PR ships the stale committed snapshot (`[[unity-procedural-committed-assets-go-stale]]`). *Why:* proven failure class — the soak build won't show the change.
- **Climb + walkable surface preserved.** The hero peak stays climbable; new steep features (rocky walls) must be either off-NavMesh or carved so they don't orphan walkable patches. Keep the NavMesh-coverage trace + PlayMode walkable gate green. *Why:* climb is Sponsor-APPROVED; a new un-climbable spike that blocks the path regresses it.
- **Shipped-build capture gate + Predict-Before-Soak.** Everything here is feel/look → each child ships evidence captured from the BUILT POC exe (not the editor) + a falsifiable pre-soak prediction graded against the Sponsor soak (`team/TESTING_BAR.md`).

### 🔬 Perf re-measure gate (the load-bearing constraint on scale-up)

The #226 perf verdict is **BOUNDED, not a blank cheque**: 60fps at land diameter **~800u** (MeanShoreR 400), via a **single scaled terrain mesh + static-batched scatter**, **vSync-capped**, with **GPU Resident Drawer UNTESTED** and measured only at ~800u. Therefore:

- **Any scale-up beyond ~800u is a RE-MEASURE gate, not a free grow.** The FPS-counter tool (`86cahmxmt`) lands the on-screen ground-truth number — the perf child of this ticket uses it (or the `-development` profiler capture, Wave1-C `86cahhfp4` S2a) to confirm the new size + new prop load still holds frame rate in the shipped exe.
- **Chunked terrain ONLY if the measurement demands it.** Do NOT pre-emptively chunk. If the re-measure shows the single-scaled-mesh approach drops below target at the new size/density, THEN adopt the Sebastian Lague chunk-LOD architecture (`.claude/docs/elite-techniques.md` § Procedural terrain at scale). Until the number says so, extend the single-mesh approach. *Why:* premature chunking is a big architecture change against an unproven need; the honest sequence is measure → decide.
- **More content = more draws + more casters + more scene weight.** Every new tree species / rock feature / slab adds unique-mesh renderers to the committed scene and (unless `castShadows Off`) to every shadow cascade. Batch the content into few regen PRs (single build slot); the caster-policy constraint above keeps decoration out of the shadow pass.

### 🎚️ Defaults (tunable — Sponsor-soak tunes; the author predicts against these, they are NOT mandates)

These are the dev's *starting* values; the Sponsor dials them at soak and the dialed values bake in. Flag each as "default X — Sponsor-soak tunes" in the code + Self-Test Report:

- **Island size** — default: grow `MeanShoreR` from 400 → **~550-650u** (land diameter ~1100-1300u, ~1.4-1.6× #226) as the first step, gated by the perf re-measure. Scale `CoastIrregAmp` (currently 70) proportionally so the bigger coast stays organic (Bar 1). *Do NOT jump to the eventual ~10-min-crossing target — grow one measured step at a time.*
- **Mountain count** — default: **2-3 peaks** (up from 1). One stays the Sponsor-approved climbable hero peak (keep its climb tuning); the other(s) can be shorter / steeper / non-climbable rock massifs for silhouette variety. Off-centre, asymmetric placement (Bar 1).
- **Rocky walls** — default: **3-6** cliff-face features on strong-slope azimuths (a `CliffinessAt`-style gate, cf. the start-island BCH-2 pattern), each a large faceted near-vertical rock face; new seed salt (`seed+1111` precedent).
- **Stone slabs** — default: **8-15** large flat-topped boulders (scale ~3-6) scattered on the land, some partly embedded; new seed salt (`seed+1212`).
- **Tree species** — default: **2-3 species** (the existing blob-canopy broadleaf + a `PineTree` cone-stack variant, cf. poly-plan TRE-2; optionally a third). Species selection position-derived so a re-run is deterministic. Per-tree hue variation via vertex colour (TRE-1 pattern).
- **Bushes + ground vegetation** — default: add **bushes** (the start island's `BerryBush`-style body mesh, decoration-only here — no berries needed for the POC) + **denser/varied grass** (chunky blade tufts per `art-direction.md` §Grass — stationary, no sway). Vegetation stays off the steep mountain flank (existing scatter reject logic).
- **Forest density (`treeTarget`)** — the scatter already exposes this knob; default: raise it for the bigger island, gated by the perf re-measure. Grass/bush density via patch masking, not a global flood.

### ✅ Success test (gate: Sponsor SOAK)

The Sponsor walk-soaks the new POC exe (exact path + expected HUD build stamp handed at soak, per `[[soak-handoff-path-and-explicit-test-checklist]]`) and confirms, at gameplay framing AND side-on:
1. The island is **noticeably bigger** than #226 (longer to cross), coast still organic.
2. There are **multiple mountains** + visible **rocky walls** + **huge stone slabs** — the place reads wild/dramatic/diverse.
3. The forest is **layered + multi-species** with bushes + richer ground vegetation — not the sparse single-species #226 read.
4. **Climb still works** on the hero peak; no new spike orphans the walkable surface.
5. **Frame rate holds** (confirmed via the FPS counter `86cahmxmt` on-screen or the profiler capture) — no stutter at the new size/density.

### 🚫 Out of scope (OOS)

- The **boat / sail / journey** half (`86caa9zju`) — deferred, gets its own `/grill-me` after the island succeeds.
- **Wiring the POC into the shipped game** / replacing the start island — this stays a stand-alone POC; retiring the chunky horizon mountains (`86cagfn8h`) is a separate start-island decision.
- **Snow-cap faceting** (`86cahmxh6`) — separate in-flight ticket; reference only.
- **Berries / harvest / needs on the POC island** — the bushes here are decoration for the visual read, not a survival-loop wiring.
- **Chunked-terrain architecture** UNLESS the perf re-measure demands it (then it's the "verify + tune" child's finding, not pre-built).
- **Kid-friendly difficulty tiers** — N/A here (pure world-gen; Bar 7 does not apply).

### Meta

**Size:** L (SPLITS — see below). **Owner:** Devon / Drew (split across children). **Reviewer:** the other of Devon/Drew per child. **Lane:** Unity build (all children are REGEN + single-build-slot serialized). **Priority:** Sponsor to order vs the Wave1 visual/perf tickets (this is a POC-lane grow, not on the start-island critical path). **Cross-refs:** #226 / `86caa9zpp` (base), `86cahmxh6` (snow-cap, sequence/coordinate), `86cahmxmt` (FPS counter, perf-gate dependency), `86cahhfp4` (Wave1-C S2a profiler path), `86caa9zju` (boat, OOS), poly-plan `team/analysis/2026-07-01-poly-style/consolidated.md` (§1 locked constraints, §4 T-A, TRE-2 pine pattern, elite-techniques chunk-LOD), `team/quality-bars.md` Bars 1 + 4.

---

## SUGGESTED SPLIT (4 children — orchestrator creates + sequences)

All four are REGEN + single-build-slot → **serialized**, not parallel (each re-commits the ~POC scene + occupies the build slot). Sequence in the order below; child C1 lands first because it sets the size + silhouette that C2/C3 populate. Coordinate C1 with `86cahmxh6` (both touch `NextIslandPocGen` terrain/snow).

### Child C1 — `feat(world): island 2.0-A — grow size + multi-mountain silhouette [M, REGEN, SOAK]`
- **🎯 Destination:** the POC island is bigger (default MeanShoreR ~550-650u, perf-gated) with 2-3 organic asymmetric mountains (one = the approved climbable hero peak, kept). Coast + peaks read organic (Bar 1), silhouette reads dramatic side-on (Bar 4).
- **🔒 Constraints:** extend `NextIslandPocGen` const tunables + `MountainHeightAt` / `HeightAtRadial` (add extra peaks) — do NOT rewrite; do NOT touch the snow-band faceting owned by `86cahmxh6` (sequence behind it or coordinate); scale `CoastIrregAmp` with size; preserve climb + NavMesh coverage.
- **🎚️ Defaults:** MeanShoreR ~550-650u, 2-3 peaks, CoastIrregAmp scaled proportionally — all "default X — Sponsor-soak tunes".
- **Perf:** re-measure at the new size with the FPS counter / profiler BEFORE serving; single-mesh unless the number says chunk.
- **Owner** Devon or Drew · **Size** M · **Gate** SOAK (side-profile + gameplay capture, Predict-Before-Soak).

### Child C2 — `feat(world): island 2.0-B — rocky walls + huge stone slabs [M, REGEN, SOAK]`
- **🎯 Destination:** the island gains dramatic rock features — near-vertical faceted rocky walls (default 3-6) + huge flat-topped stone slabs (default 8-15) — that read as real rock (Bar 4: a wall you can't walk up; a slab sitting ON the ground).
- **🔒 Constraints:** new content classes via the `MakeMeshObject(castShadows, staticFlags)` chokepoint pattern (large features keep shadows + static-batch); new seed salts (`seed+1111` walls / `seed+1212` slabs — never mutate an existing `System.Random` stream); faceted flat-shaded `FacetedRock` idiom, `QuantizeFine` near-neutral tints; walls off-NavMesh or carved (don't orphan the walkable surface).
- **🎚️ Defaults:** 3-6 walls, 8-15 slabs, scale bands — "default X — Sponsor-soak tunes".
- **Owner** the other of Devon/Drew · **Size** M · **Gate** SOAK (side-profile per new class + gameplay capture, Predict-Before-Soak). **Sequences after C1** (needs the grown terrain to place onto).

### Child C3 — `feat(world): island 2.0-C — tree/bush/vegetation variety [M, REGEN, SOAK]`
- **🎯 Destination:** the forest reads layered + alive — 2-3 tree species (broadleaf blob + `PineTree` cone-stack, ± a third), bushes, and richer stationary ground vegetation — up from #226's single-species sparse read.
- **🔒 Constraints:** extend `NextIslandPocScatter` (position-derived species selection so re-runs are deterministic; raise `treeTarget` perf-gated); per-tree/bush hue via **vertex colour only** (T-A — never per-material); grass stationary/no-sway (`art-direction.md` §Grass, #172); up-biased foliage normals, never `RecalculateNormals`; vegetation stays off the steep mountain flank (existing reject logic); decoration ships `castShadows Off` + `BatchingStatic`.
- **🎚️ Defaults:** 2-3 species, bush + grass density via patch masking, `treeTarget` raise — "default X — Sponsor-soak tunes".
- **Owner** Devon or Drew · **Size** M · **Gate** SOAK. **Sequences after C1** (needs the grown terrain); can follow or pair-sequence with C2.

### Child C4 — `perf(world): island 2.0-D — perf re-measure + tune at new size/density [S-M, REGEN, MACHINE]`
- **🎯 Destination:** a documented perf verdict for the grown + populated island — on-screen FPS (via `86cahmxmt`) and/or a `-development` profiler capture (Wave1-C S2a `86cahhfp4`) confirming the shipped POC exe holds target frame rate at the final size + prop load, OR a documented chunk-LOD adoption if the single-mesh approach doesn't hold.
- **🔒 Constraints:** measure the SHIPPED exe (not the editor); if measurement demands chunking, adopt the Lague chunk-LOD architecture (`elite-techniques.md`) — do NOT chunk pre-emptively; confirm the caster policy from C2/C3 kept the shadow pass in budget (the G6-2 scene-stats trace).
- **Owner** Devon or Drew · **Size** S-M · **Gate** MACHINE (capture + profiler evidence; this child's finding is the honest go/no-go on further scale-up). **Sequences LAST** (needs the final content in place to measure).

**Split rationale (honest):** C1 owns size + terrain silhouette (the foundation the rest sits on + the snow-band coordination point with `86cahmxh6`); C2 and C3 are two disjoint content surfaces (rock features vs vegetation) that could run in either order after C1; C4 is the measurement that can't happen until the content is in. If the orchestrator wants fewer PRs, C2+C3 could bundle into one regen — but they're distinct soak surfaces (rock read vs forest read) with separate predict lines, so I've kept them split for cleaner soak verdicts. All four serialize on the single build slot regardless.

---

## AC UPDATE — post-C1 (Priya, 2026-07-03) — PASTE-READY for the C2 + C3 subtasks

> **Why this update:** C1 (`86cahwx6w`, branch `devon/86cahwx6w-island2-c1`, NO PR yet — in progress) has landed the size + multi-mountain foundation and SETTLED several values the C2/C3 drafts left as ranges. C2/C3 must now build against C1's **concrete** vocabulary, not the pre-C1 estimates. The blocks below REPLACE the matching bullets on the C2/C3 subtasks. Sequencing unchanged: **C2 and C3 both sequence behind C1's MERGE** (C1 has no PR yet). C1 verified CLEAN — it touches only POC files, no shared start-island/worldlook asset.

### What C1 settled (the concrete state C2/C3 extend)

- **Island size is now FIXED at `MeanShoreR = 600f`** (~1200u diameter, ~1.5× #226) — NOT the draft's "~550-650 range". `CoreR = 450f`, `CoastIrregAmp = 105f`, `FalloffEnd = 705f`, `GridHalf = 745f`, `Seg = 386`. **C2/C3 do NOT re-grow the island** — that knob is spent; place onto the 600u land.
- **Three peaks exist in `NextIslandPocGen.Peaks[]`** (`Peak` struct): `[0]` HERO climbable snow-cap dome (`cx 90, cz -60, height 135, footR 300` — BYTE-KEPT, do NOT re-tune), `[1]` NE massif (`cx 330, cz 150, height 105, footR 200, power 1.8`, small snow crown), `[2]` SE massif (smaller, bare rock, no snow).
- **Per-peak rock band + tree line already baked into the TERRAIN** via colour ramp + per-peak scatter rejection: `HeroRockStartFrac 0.40 / HeroRockFullFrac 0.62 / HeroTreeLineFrac 0.45`; `CragRockStartFrac 0.12 / CragRockFullFrac 0.35 / CragTreeLineFrac 0.10`. Each `Peaks[i]` carries its own `rockStartFrac / rockFullFrac / treeLineFrac`.
- **`NextIslandPocScatter.Scatter(parent, seed, groundCol, treeTarget)`** already: places `treeTarget` broadleaf trees rejecting each peak's upper flank (per-peak `treeLineFrac`), `rockTarget = Mathf.Max(20, treeTarget/5)` scatter rocks, and **`clumpTarget = Mathf.Max(120, treeTarget)` GRASS CLUMPS** (grass ALREADY exists — extend it, don't add a parallel system). `plantOuterR = MeanShoreR + CoastIrregAmp` (=705). Emits `[poc-trace]` scatter log.

### PASTE → C2 subtask (`feat(world): island 2.0-B — rocky walls + huge stone slabs`)

**🎯 Destination (unchanged):** dramatic rock features — near-vertical faceted rocky walls (default 3-6) + huge flat-topped stone slabs (default 8-15) reading as real rock (Bar 4: a wall you can't walk up; a slab sitting ON the ground).

**🔒 Constraints (REPLACES the C2 constraint bullet):**
- **C2's "rocky walls" are FREE-STANDING `FacetedRock` WALL PROPS scattered via `NextIslandPocScatter` — DISTINCT from C1's per-peak terrain rock-banding (already done).** Do NOT re-band the terrain colour or add more massifs to `Peaks[]`; add discrete near-vertical wall + slab prop meshes on the land. *Why:* C1 owns the terrain silhouette + crag banding; C2 owns discrete rock props — double-authoring the crag look re-opens C1's soaked terrain.
- **Place onto the settled 600u land; reject footprints overlapping the three `Peaks[]` massifs** (hero `90,-60 r300`; NE `330,150 r200`; SE massif) — walls/slabs go on the flats, foreshore, and inter-peak cols, not clipped into a mountain foot. Use `plantOuterR` (=705) as the outer bound.
- **New seed salts `seed+1111` (walls) / `seed+1212` (slabs)** — never mutate an existing `System.Random` stream (the tree/rock/grass streams are C1's). Mirror the existing `rockTarget` scatter idiom.
- **Caster policy: large hero features (walls, big slabs) keep shadows + static-batch** — `MakeMeshObject(castShadows: true, BatchingStatic)`. Faceted flat-shaded `FacetedRock` idiom, `QuantizeFine` near-neutral tints, NEVER `RecalculateNormals`.
- **Walls off-NavMesh or carved so they don't orphan the walkable surface** (C1 regenerated `PocNavMesh.asset` at 600u — keep the NavMesh-coverage trace + PlayMode walkable gate green).
- **Commit ONLY `NextIslandPoc.unity` + `PocNavMesh.asset` + your new mesh/mat — do NOT commit a stray `GradientSky.mat` diff** that `NextIslandPocScene.Build` may emit (root cause tracked in `86caj0rrg`; until it lands, `git add` by path + verify `git status` shows no shared-asset diff).

**🎚️ Defaults:** 3-6 walls (`seed+1111`), 8-15 slabs (`seed+1212`), scale bands — "default X — Sponsor-soak tunes". **Sequences after C1 MERGES.**

### PASTE → C3 subtask (`feat(world): island 2.0-C — tree/bush/vegetation variety`)

**🎯 Destination (unchanged):** the forest reads layered + alive — 2-3 tree species (broadleaf blob + `PineTree` cone-stack, ± a third), bushes, richer stationary ground vegetation — up from #226's single-species sparse read.

**🔒 Constraints (REPLACES the C3 constraint bullet):**
- **EXTEND C1's `NextIslandPocScatter.Scatter(parent, seed, groundCol, treeTarget)` — do NOT rewrite it.** C1 already does the tree loop with per-peak `treeLineFrac` rejection, `rockTarget` rocks, and `clumpTarget = Mathf.Max(120, treeTarget)` GRASS CLUMPS. C3 adds species + bushes + denser/varied grass INTO these existing loops. *Why:* the per-peak reject logic (rejecting all THREE `Peaks[]` tree lines) is C1's — new species must inherit it, not bypass it.
- **New species selection is position-derived** (deterministic re-run) off the existing tree stream — `PineTree` cone-stack per poly-plan TRE-2, optional 3rd. **Every new species respects all three `Peaks[i].treeLineFrac`** (hero 0.45, crag 0.10) — a pine must not grow up the snow cap either.
- **Per-tree/bush hue via VERTEX COLOUR only (T-A) — never per-material.** Up-biased foliage normals, NEVER `RecalculateNormals`. Grass stationary / no sway (`art-direction.md` §Grass, #172). Decoration (trees/bushes/grass) ships `MakeMeshObject(castShadows: false, BatchingStatic)`.
- **Bushes = the start island's `BerryBush` body mesh, decoration-only (no berries)**, scattered off the steep peak flanks (reuse the per-peak reject). Denser/varied grass EXTENDS `clumpTarget`, via patch masking not a global flood.
- **`treeTarget` raise is perf-gated** — hand the raised value to C4's re-measure; do NOT flood before the FPS number confirms it holds at 600u.
- **Same stray-`GradientSky.mat` watch as C2** — commit by path, verify `git status` (`86caj0rrg`).

**🎚️ Defaults:** 2-3 species, bush + grass density via patch masking, `treeTarget` raise — "default X — Sponsor-soak tunes". **Sequences after C1 MERGES; may pair-sequence with C2.**

---

## HYGIENE FINDINGS — 2026-07-03 (report-only; orchestrator executes ClickUp writes)

- **Status drift:** the 8 `in review` tickets all map 1:1 to open PRs (`86cahnmjv`→#239, `86cahzycp`→#238, the five-ticket bundle `86caj0ahr`/`86cahx2p5`/`86cag1xn0`/`86cafzaeb`/`86cafhgun`→#237, `86cah90cp`→#223) — **no drift**. The ONE stale item is **`86cabeqwf` `in progress`** (PR #220 open, no agent) — superseded-by-`86cah8ukr` per that ticket's own decided-handling. **Recommend:** move `86cabeqwf` `in progress`→`to do` now (nothing is actively working it) + formally close as superseded + close PR #220 (do NOT merge) when `86cah8ukr` dispatches.
- **Dupe check `86caffwv5` vs `86cah7ym9`:** **KEEP BOTH — not a dupe.** `86caffwv5` = per-weapon attack-CLIP import/wiring (sponsor-gated on the Sponsor supplying each Mixamo clip; `[[chop-swing-mixamo-clip-not-procedural]]`); `86cah7ym9` = weapon-roster DATA + mesh + material tiers (depends on combat POC `86cah7xxp`). Overlap seam: `86cah7ym9`'s phrase "each with its own mesh + swing animation" collides with `86caffwv5`'s whole scope. **Recommend:** (a) edit `86cah7ym9` to DEFER the per-weapon attack-animation wiring to `86caffwv5` (cross-ref) — keep `86cah7ym9` = data/mesh/tiers only; (b) apply the `sponsor-gate` tag to `86caffwv5` (it's gated on clip supply, recommends this itself). A new weapon is "done" only when both its data (`86cah7ym9`) and its attack clip (`86caffwv5`) land.
- **Queue-order sanity (`#223-conflict-rebake` → `86cahvntg`):** **no dependency inversion — order is correct** (bake #223's sun values into `GradientSky.mat` FIRST, then land the stop-mutating hygiene fix `86cahvntg`; reverse order would clobber the fresh values). **But the queue is INCOMPLETE:** `GradientSky.mat` is a 4-ticket contention cluster — `86cah90cp`(#223) + `86cahvntg` + **`86caj0rrg`** (POC-Build mutates the shared sky mat) + **`86cahxeek`** (re-bake 2 stale committed assets). All four mutate the same committed `GradientSky.mat` → **all four must serialize, not just the first two.** Recommend sequencing `86caj0rrg` + `86cahxeek` into the same chain after `86cahvntg`. Note: `86caj0rrg` is a soft foot-gun for the island-2.0 regen children (C2/C3 run `NextIslandPocScene.Build`) — fixing it removes the stray-`GradientSky.mat`-diff risk (baked into the C2/C3 constraints above as a commit-by-path guard).

---

## BOARD DEDUPE / HYGIENE REPORT (report-only — 2026-07-02)

Read from the full board dump (`get_tasks`, 33 open tasks, list `901523878268`). Orchestrator executes any status writes — Priya is report-only here.

### (a) Duplicate / overlap check vs FPS-counter `86cahmxmt`, snow-cap `86cahmxh6`, and this island-2.0 draft

- **FPS counter `86cahmxmt` — NO duplicate.** Scanned all 33 open tickets for an existing FPS/frame-rate/perf-HUD ticket: none. The nearest neighbours are perf-*measurement* tickets that do NOT render an on-screen counter — `86cahhfp4` (Wave1-C: S2a `-development` build + profiler capture — that's an off-screen profiler path, complementary not duplicate) and `86cag93zb` (RenderTexture-readback captures — CI capture infra, unrelated). **Verdict: unique. Ship it.** (Its own Meta note already asks for this confirming sweep — done.)
- **Snow-cap `86cahmxh6` — NO duplicate; it is the ONLY snow-cap ticket.** It is a child-follow-up of `86caa9zpp` (#226, now off the open board = merged/complete). **Overlap flag with the island-2.0 draft:** both `86cahmxh6` and island-2.0 child **C1** touch `NextIslandPocGen` terrain/snow code. This is a genuine merge-collision risk on the committed POC scene → I've baked the constraint into C1 (sequence behind `86cahmxh6` or fold explicitly; do NOT double-author snow faceting). **Verdict: not a dupe, but a sequencing dependency — flagged in the draft.**
- **island-2.0 draft — NO duplicate; supersedes nothing, but is the natural home for a deferred item.** No existing ticket covers "grow + diversify the next island". Adjacent tickets that are NOT duplicates: `86caa9zpp` (the #226 base this EXTENDS — off the board, merged), `86caa9zju` (boat/journey — deferred, explicitly OOS in the draft), `86cagfn8h` (open-horizon on the START island — different island, different surface). **Possible fold-in (orchestrator's call):** `86cacewju` (chamfer-highlight bevel on hero props) is unrelated and should NOT fold here. **Verdict: unique; create as new parent + 4 children.**

### (b) Status drift vs reality

- **`86cabeqwf` (dev-tweak console per-need) — CONFIRMED DRIFT.** Board shows it `in progress`, but its sibling `86cah8ukr` (settings-panel SPLIT) explicitly records: "#220 is SUPERSEDED / FOLDED INTO this split ticket … Orchestrator: close #220 as superseded-by-this-ticket + move 86cabeqwf accordingly once [the split] dispatches." So `86cabeqwf` should be **closed as superseded-by-`86cah8ukr`** (or parked `to do` + tagged superseded) — it is NOT actively in-work as an independent deliverable. **Recommend: orchestrator closes `86cabeqwf` as superseded when `86cah8ukr` dispatches** (per that ticket's own decided-handling section). This matches the brief's flagged example exactly.
- **`86cahk7k8` (R5 re-scope, Erik) — `in progress`.** Plausibly correct IF Erik is actively on it; if no branch/agent is live, it should drop to `to do`. Orchestrator to verify against agent liveness (I can't probe from here). Low-confidence flag.
- **`86cahhff6` (Wave1-A) `in review` + `86cah90cp` (sun bake) `in review`** — terminal pre-merge statuses; correct if their PRs are open with review pending. These are orch-owned merge-flip candidates (flip `in review → complete` in the same tool round as the merge) — not Priya writes. No drift, just a reminder they're staged.
- **`86cah7xxp` (combat POC) `ready for qa test` + `needs-soak`** — correct terminal-ish status; it's a soak-gated vertical slice. No drift.
- **No other status drift found.** The rest of the `to do` set reads as genuine un-dispatched backlog (combat-expansion cluster `86cah7*`, CI hardening cluster, NITs), not mis-statused work.

### (c) Tickets whose next action is orch-side + mechanical

- **`86cah6n9w`** (chore: #215 NIT — stale SettingsCatalog comment "lowered to 5", now 9) — mechanical 1-line comment fix; bundle into the next SettingsCatalog-touching PR (e.g. the `86cah8ukr` split) rather than a standalone build-slot burn.
- **`86cahc2y7`** (chore: #223 NIT — committed `GradientSky.mat` stray `_HorizonColor 0.42`) — mechanical regen+recommit; fold into the next worldlook REGEN (Wave1-B `86cahhfkc` is the natural carrier — both touch worldlook committed assets).
- **`86cabeqwf`** — orch-side close-as-superseded (see (b)).
- **`86cahhff6` / `86cah90cp`** — orch-side merge-flip when their PRs go green + reviewed (see (b)).
- **`86cafhehe`** (fix: auto-merge Action can't merge `.github/workflows/` PRs) — orch-side infra; known issue with a documented workaround (`[[auto-merge-fails-on-workflow-file-prs]]`) — browser `--admin` merge or a workflows-scoped PAT. Not a dispatch; an orch merge-mechanic note.

**Net:** no Priya-side ClickUp writes (report-only + I can't reach ClickUp anyway). Orchestrator actions: (1) create the island-2.0 parent + 4 children with tags `feat`/`world`/`poc`; (2) close `86cabeqwf` as superseded-by-`86cah8ukr` when the split dispatches; (3) fold the two mechanical NITs (`86cah6n9w`, `86cahc2y7`) into their natural carrier PRs rather than standalone; (4) confirm `86cahk7k8` liveness. No true duplicates anywhere on the board.
