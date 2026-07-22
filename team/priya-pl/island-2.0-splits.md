# Island 2.0 — SPLITS (finalized, dispatch-ready) — Priya 2026-07-06

> **Status:** FINALIZED for orchestrator → ClickUp create + dispatch. Report-only (Priya can't reach ClickUp — orchestrator creates the child tickets on list `901523878268` under parent `86cahwwvg` and owns all status writes).
> **Supersedes** the "SUGGESTED SPLIT" section of `island-2.0-ticket-draft.md` — that draft's parent-AC + post-C1 AC-update work remain the source; this doc is the finalized, dispatch-ready split set with dependency edges + dispatch order.
> **Parent:** `86cahwwvg` — `[L, SPLITS]` umbrella. Split before any dispatch (this doc IS the split).
> **C-series precedent:** **C1 (`86cahwx6w`) already MERGED via #246** (`feat(world): island 2.0-A — grow POC island 1.5× + multi-mountain range`). This doc finalizes the **remaining three** children — **C2, C3, C4** — continuing the same C-series numbering. C1 settled the size + peak vocabulary the three below extend.

---

## What C1 settled (the concrete base C2/C3/C4 extend — do NOT re-open)

Ground-truth from the merged #246 (`86cahwx6w`), carried from the post-C1 AC update in `island-2.0-ticket-draft.md`:

- **Island size FIXED at `MeanShoreR = 600f`** (~1200u diameter, ~1.5× #226). `CoreR = 450f`, `CoastIrregAmp = 105f`, `FalloffEnd = 705f`, `GridHalf = 745f`, `Seg = 386`. **The size knob is SPENT — C2/C3/C4 place onto the 600u land; do NOT re-grow.**
- **Three peaks in `NextIslandPocGen.Peaks[]`:** `[0]` HERO climbable snow-cap dome (`cx 90, cz -60, height 135, footR 300` — BYTE-KEPT, do NOT re-tune), `[1]` NE massif (`cx 330, cz 150, height 105, footR 200, power 1.8`, small snow crown), `[2]` SE bare-rock massif (no snow).
- **Per-peak rock band + tree line already baked into the terrain** (colour ramp + per-peak scatter rejection): hero `rockStart 0.40 / rockFull 0.62 / treeLine 0.45`; crag `rockStart 0.12 / rockFull 0.35 / treeLine 0.10`. Each `Peaks[i]` carries its own fractions.
- **`NextIslandPocScatter.Scatter(parent, seed, groundCol, treeTarget)`** already places `treeTarget` broadleaf trees (per-peak `treeLineFrac` reject), `rockTarget = Max(20, treeTarget/5)` rocks, and **`clumpTarget = Max(120, treeTarget)` GRASS CLUMPS** (grass EXISTS — extend, don't parallel-build). `plantOuterR = 705`. Emits `[poc-trace]`.

**Update since #240:** the stray-`GradientSky.mat`-diff foot-gun is **RESOLVED** — `86caj0rrg` merged via **#256** (2026-07-05, `fix(worldgen): isolate NextIslandPoc sky material`). `NextIslandPocScene.Build` no longer pollutes the shared sky mat. The "commit-by-path / verify `git status`" guard below is now belt-and-suspenders (good hygiene) rather than a live hazard.

---

## Dependency edges + dispatch order (READ FIRST)

- **Build slot:** all three are **REGEN + Unity-build** → each occupies the single Unity-build slot (`unity-build` group, captures runner-1-pinned; **≤1 Unity-build ticket in flight** per CLAUDE.md). They **serialize** on the build slot — never dispatch two at once.
- **Shared-scene collision (the real merge hazard):** all three re-commit **`NextIslandPoc.unity` + `PocNavMesh.asset`** (the committed POC scene). Two open at once → guaranteed conflict on the regenerated scene. This is why they serialize, not just for the build slot.
- **Boot.unity collision — NONE.** The POC is a **stand-alone parallel generator**; it does NOT load, reference, or regen `Boot.unity` / `WorldBootstrap.cs` / `LowPolyZoneGen.cs`. So island-2.0 children do **not** collide with any start-island / Boot.unity regen ticket, and vice-versa — they contend only on the build slot, not on scene bytes.
- **Snow-cap `86cahmxh6` cross-ref:** C1 already shipped the three peaks incl. the hero snow-cap dome. **Orch: verify `86cahmxh6`'s live status** — if it was folded into C1 it may be closeable/superseded; if still open for faceting-only it must sequence in the same `NextIslandPocGen` chain (do NOT run parallel to a C-child). Flagged, not resolved here (needs board read).

**Dispatch order:**

```
C1 (86cahwx6w) ── MERGED #246
       │
       ▼
   C2 ──┐  (rock props; after C1 merge)
        ├──►  C4  (perf re-measure; LAST — needs final content in scene)
   C3 ──┘  (vegetation; after C1 merge)
```

- **C2 and C3** both sequence **after C1's merge (done)** and are two **disjoint content surfaces** → either order, but they **serialize** on the build slot + POC scene. Recommend **C2 → C3** (rock silhouette read settles before the forest fills around it), but the reverse is fine.
- **C4 sequences LAST** — it can only measure the FINAL size + prop load, so it needs C2 **and** C3 in the committed scene.

---

## Child C2 — rocky walls + huge stone slabs

**Title:** `feat(world): island 2.0-B — rocky walls + huge stone slabs [M, REGEN, SOAK]`
**Parent:** `86cahwwvg` · **Tags:** `feat`, `world`, `poc` · **Owner:** Devon or Drew · **Reviewer:** the other · **Size:** M · **Lane:** Unity build (serial)

### 🎯 Destination (what + why)
The island gains dramatic rock features — near-vertical faceted **rocky walls** (default 3–6) + huge flat-topped **stone slabs** (default 8–15) — that read as real rock. *Bar 4:* a rocky wall is a near-vertical rock face **you can't walk up**; a stone slab is a big flat-topped boulder **sitting ON the ground**. *Bar 1:* organic/asymmetric/faceted, never geometric. **Strip-test:** the Sponsor walks the island and hits wild, dramatic rock features that read as real rock, not gameplay blocks.

### 🔒 Constraints (must obey)
- **C2's "rocky walls" are FREE-STANDING `FacetedRock` WALL PROPS scattered via `NextIslandPocScatter` — DISTINCT from C1's per-peak terrain rock-banding (already done).** Do NOT re-band the terrain colour or add massifs to `Peaks[]`. *Why:* C1 owns the terrain silhouette + crag banding; double-authoring re-opens C1's soaked terrain.
- **Place onto the settled 600u land; reject footprints overlapping the three `Peaks[]`** (hero `90,-60 r300`; NE `330,150 r200`; SE massif) — walls/slabs go on flats, foreshore, inter-peak cols, not clipped into a mountain foot. Outer bound `plantOuterR` (=705). *Why:* clipping a wall into a peak reads broken + can orphan NavMesh.
- **New seed salts `seed+1111` (walls) / `seed+1212` (slabs)** — never mutate an existing `System.Random` stream (tree/rock/grass streams are C1's). Mirror the `rockTarget` scatter idiom. *Why:* mutating a live stream re-rolls C1's soaked scatter.
- **Caster policy: large hero features (walls, big slabs) keep shadows + static-batch** — `MakeMeshObject(castShadows: true, BatchingStatic)`; decoration-scale debris (if any) ships `castShadows: false`. *Why:* the shadow pass is the dominant GPU cost (poly-plan §headline) — silhouette features need shadows, debris does not.
- **Faceted flat-shaded `FacetedRock` idiom, `QuantizeFine` near-neutral tints, NEVER `RecalculateNormals`.** Sub-1.0 warm palette all channels. *Why:* `RecalculateNormals` self-smooths → loses facets; QuantizeFine avoids the closed pink-cast bug (`lowpoly-quality.md` §1).
- **Tonal variation vertex-colour-baked, NEVER per-material** (T-A). *Why:* per-material tint breaks the SRP-Batcher ~1-draw-call model.
- **Walls off-NavMesh or carved so they don't orphan the walkable surface** — C1 regenerated `PocNavMesh.asset` at 600u; keep the NavMesh-coverage trace + PlayMode walkable gate green. *Why:* climb is Sponsor-APPROVED; a new spike that blocks the path regresses it.
- **Commit ONLY `NextIslandPoc.unity` + `PocNavMesh.asset` + your new mesh/mat** — `git add` by path, verify `git status` shows no shared-asset diff (`86caj0rrg` fixed the stray-`GradientSky.mat` foot-gun via #256, but commit-by-path stays the discipline; `[[stray-untracked-artifacts-contaminate-prs]]`). *Why:* a code-only PR ships the stale committed snapshot (`[[unity-procedural-committed-assets-go-stale]]`).

### 🎚️ Defaults (tunable — Sponsor-soak tunes; predict against these)
- **3–6 rocky walls** (`seed+1111`) — "default X — Sponsor-soak tunes".
- **8–15 stone slabs** (`seed+1212`), scale ~3–6, some partly embedded — "default X — Sponsor-soak tunes".

### ✅ Success test (gate: Sponsor SOAK)
Walk-soak the POC exe (exact path + expected HUD stamp handed at soak, `[[soak-handoff-path-and-explicit-test-checklist]]`). Confirm at gameplay framing AND side-on: (1) visible **rocky walls** that read near-vertical + un-walkable; (2) **huge stone slabs** sitting ON the ground; (3) both read **organic/faceted**, not geometric; (4) nothing clips into a peak or orphans the walkable surface; (5) climb still works. Ships a **side-profile silhouette capture per new class** from the shipped exe + a **Predict-Before-Soak** line (predict wall count/read + slab count/read against the defaults, graded at soak).

### 🚫 Out of scope
- Re-banding terrain / re-growing size / adding `Peaks[]` massifs (C1, done).
- Vegetation (C3). Perf re-measure verdict (C4).
- Wiring the POC into the shipped game; boat/journey (`86caa9zju`); berries/needs on the POC.
- Chunked-terrain architecture (only if C4's measurement demands it).

**Dependency:** sequences after **C1 merge (done)**; serializes with C3 on the build slot + POC scene. Recommend **first** of C2/C3.

---

## Child C3 — tree / bush / vegetation variety

**Title:** `feat(world): island 2.0-C — tree/bush/vegetation variety [M, REGEN, SOAK]`
**Parent:** `86cahwwvg` · **Tags:** `feat`, `world`, `poc` · **Owner:** Devon or Drew · **Reviewer:** the other · **Size:** M · **Lane:** Unity build (serial)

### 🎯 Destination (what + why)
The forest reads **layered + alive** — 2–3 tree species (broadleaf blob + `PineTree` cone-stack, ± a third), **bushes**, and richer **stationary ground vegetation** — up from #226's sparse single-species read. *Bar 1:* organic scatter, no grids. **Strip-test:** the Sponsor walks the island and the forest feels varied + rich + wild, not a single repeated tree on bare ground.

### 🔒 Constraints (must obey)
- **EXTEND C1's `NextIslandPocScatter.Scatter(parent, seed, groundCol, treeTarget)` — do NOT rewrite.** C1 already does the tree loop with per-peak `treeLineFrac` reject, `rockTarget` rocks, `clumpTarget = Max(120, treeTarget)` grass clumps. C3 adds species + bushes + denser/varied grass INTO these existing loops. *Why:* the per-peak reject logic (all THREE `Peaks[]` tree lines) is C1's — new species must inherit it, not bypass it.
- **New species selection is position-derived** (deterministic re-run) off the existing tree stream — `PineTree` cone-stack per poly-plan TRE-2, optional 3rd. **Every new species respects all three `Peaks[i].treeLineFrac`** (hero 0.45, crag 0.10) — a pine must not grow up the snow cap. *Why:* trees on the snow cap break the silhouette + the climb read.
- **Per-tree/bush hue via VERTEX COLOUR only (T-A) — never per-material.** Up-biased foliage normals, NEVER `RecalculateNormals`. *Why:* per-material multiplies material count; `RecalculateNormals` loses the intended soft-foliage read.
- **Grass stationary / no sway** (`art-direction.md` §Grass, #172). **Decoration (trees/bushes/grass) ships `MakeMeshObject(castShadows: false, BatchingStatic)`.** *Why:* Sponsor-locked no-sway; decoration in the shadow pass silently re-inflates the dominant GPU cost.
- **Bushes = the start island's `BerryBush` body mesh, decoration-only (no berries)**, off the steep peak flanks (reuse the per-peak reject). Denser/varied grass EXTENDS `clumpTarget` via patch masking, not a global flood. *Why:* berries = survival-loop wiring (OOS here); a global grass flood tanks perf + reads as a lawn.
- **`treeTarget` raise is perf-gated** — hand the raised value to C4's re-measure; do NOT flood before the FPS number confirms it holds at 600u. *Why:* more draws + scene weight; C4 is the honest go/no-go.
- **Commit ONLY `NextIslandPoc.unity` + `PocNavMesh.asset` + new mesh/mat** — `git add` by path, verify `git status` (same discipline as C2; `86caj0rrg`/#256 resolved the sky-mat foot-gun).

### 🎚️ Defaults (tunable — Sponsor-soak tunes; predict against these)
- **2–3 tree species** (broadleaf + `PineTree`, ± 3rd) — "default X — Sponsor-soak tunes".
- **Bush + grass density via patch masking**, `treeTarget` raise (perf-gated) — "default X — Sponsor-soak tunes".

### ✅ Success test (gate: Sponsor SOAK)
Walk-soak the POC exe (path + stamp handed at soak). Confirm: (1) **multiple tree species** visibly distinct; (2) **bushes** present + off the steep flanks; (3) **richer, varied, stationary** ground vegetation, not a repeated single tuft; (4) hue variation reads (vertex-colour); (5) nothing grows on the snow cap; (6) climb + walkable surface intact. Ships gameplay + canopy-level captures from the shipped exe + a **Predict-Before-Soak** line (predict species-read + density against the defaults).

### 🚫 Out of scope
- Rock props (C2). Perf verdict (C4). Berries / harvest / needs wiring. Terrain/size/peaks (C1). POC-into-game wiring; boat.

**Dependency:** sequences after **C1 merge (done)**; serializes with C2 on the build slot + POC scene. Recommend **after C2**.

---

## Child C4 — perf re-measure + tune at final size/density

**Title:** `perf(world): island 2.0-D — perf re-measure + tune at final size/density [S-M, REGEN, MACHINE]`
**Parent:** `86cahwwvg` · **Tags:** `perf`, `world`, `poc` · **Owner:** Devon or Drew · **Reviewer:** the other · **Size:** S–M · **Lane:** Unity build (serial)

### 🎯 Destination (what + why)
A **documented perf verdict** for the grown + fully-populated island: on-screen FPS (via the FPS counter `86cahmxmt`, merged #236) and/or a `-development` profiler capture (Wave1-C S2a `86cahhfp4`, merged #235) confirming the **shipped POC exe** holds target frame rate at the final size + prop load (C1 size + C2 rock + C3 vegetation) — OR a documented chunk-LOD adoption if the single-scaled-mesh approach doesn't hold. **Strip-test:** the team has a real number (not a vibe) for whether the populated 600u island holds frame rate, and an honest go/no-go on further scale-up.

### 🔒 Constraints (must obey)
- **Measure the SHIPPED exe, not the editor** — editor-vs-runtime divergence is a proven failure class (`[[verify-soak-builds-or-bake-and-judge]]`; spike iter6). *Why:* the number must reflect what the Sponsor plays.
- **The #226 perf verdict is BOUNDED** (60fps at ~800u diameter / MeanShoreR 400, single scaled mesh + static-batched scatter, vSync-capped, GPU Resident Drawer UNTESTED). The island is now 600u MeanShoreR (~1200u diameter) + populated → this re-measure is the gate that verdict does NOT cover. *Why:* scale-up past the measured point is a re-measure, not a free grow.
- **If — and only if — measurement demands it, adopt the Sebastian Lague chunk-LOD architecture** (`.claude/docs/elite-techniques.md` § Procedural terrain at scale). Do NOT chunk pre-emptively. *Why:* premature chunking is a big architecture change against an unproven need; the honest sequence is measure → decide.
- **Confirm the caster policy from C2/C3 kept the shadow pass in budget** (the scene-stats / G6-2 trace — decoration `castShadows: false`, hero features on). *Why:* a mis-set caster policy is the most likely silent perf regression from C2/C3.
- Commit-by-path discipline as C2/C3 (any regen/tune re-commits the POC scene).

### 🎚️ Defaults (tunable)
- The `treeTarget` / prop-count values handed up from C2/C3 are the **starting** load; C4 confirms or dials them down to hold frame rate, and the dialed values bake in ("default X — Sponsor-soak tunes"). No new content defaults — C4 measures + tunes what C2/C3 placed.

### ✅ Success test (gate: MACHINE — capture + profiler evidence)
A committed perf note (PR body + `team/analysis/` or the ticket) records: (1) on-screen FPS (current + 5s avg) from the shipped exe at gameplay framing on the final populated island; and/or (2) a `-development` profiler frame-time capture; (3) an explicit **verdict** — holds target (ship single-mesh) OR does not (documented chunk-LOD adoption or density dial-down). This child's finding is the honest go/no-go on any further island scale-up. **No Sponsor soak required** — machine-measured gate; the Sponsor sees the number, doesn't hunt for a defect.

### 🚫 Out of scope
- Adding NEW content (C2/C3). Growing size further (a fresh ticket, gated by THIS verdict). Start-island perf (different scene). Boat/journey.

**Dependency:** sequences **LAST** — needs C2 **and** C3 merged so the final populated scene exists to measure. Serializes on the build slot.

---

## Split rationale (honest)

C1 (merged) owns size + terrain silhouette + per-peak banding — the foundation. **C2** and **C3** are two **disjoint content surfaces** (discrete rock props vs vegetation scatter) with **separate soak-read verdicts** (does the rock read as rock? does the forest read as varied?) → kept split for clean per-surface soak grading + separate Predict-Before-Soak lines, even though both re-commit the same POC scene. **C4** is the measurement that cannot happen until the content is in.

If the orch wants **fewer build-slot burns**, C2+C3 *could* bundle into one regen PR — but they'd share one soak verdict, muddying which surface failed if the Sponsor rejects. My recommendation: **keep the 3-way split**; the build slot serializes them anyway, so the only cost of splitting is two extra PRs, and the benefit is clean per-surface soak signal. 3 children (C2/C3/C4) fits the parent's `[L, SPLITS]` cleanly alongside the merged C1.

---

# PART B — BOARD-DRIFT VERIFICATION REPORT (2026-07-06, git/PR evidence only)

> Report-only. Orchestrator executes the ClickUp flips (Priya has no ClickUp access this task). Every claim cites a merged PR # / SHA / mergedAt as ground truth; board **status** claims I cannot read are flagged **VERIFY**, not asserted.

## B1 — `86cag93zb` (RT-readback captures) → RE-SCOPE to the runner-unpin AC4

**Evidence:** AC1 (spike) merged **#248** (`feat(ci): RT-readback capture SPIKE (AC1 of 86cag93zb)`, 2026-07-03). AC2+AC3 merged **#250** (`feat(ci): headless RT-readback capture — convert 4 scene-content gates to -batchmode (AC2+AC3 of 86cag93zb)`, 2026-07-04). **No open PR** for the ticket (`gh pr list --state open` = only #264). Task states it's `in progress` with **no agent in flight** → genuine drift (merged shippable work + no live worker + no open PR).

**PR #250 explicitly bounds what remains, verbatim:** *"the capture ci.yml job … stays pinned to runner-1 (overlay gates still need a window) — **runner NOT unpinned; AC4 is separate**."* And #250 flags the AC4 blocker: the **overlay gates** (settings UI-Toolkit, loot/water IMGUI, inv-drag-ghost UI-Toolkit, + pond's 7 soak-tuned perceptual gates) **cannot go headless** without a `PanelSettings.targetTexture` redirect / IMGUI migration / careful soak-validated convert — a **separate follow-up**.

**Recommendation → RE-SCOPE, do NOT close.** AC4 (runner-unpin) is real remaining value — it's the enabler for the build-lane cap→2 CLAUDE.md wants (the 2nd runner currently breaks windowed captures; #250 got the 4 scene-content gates headless, which is *part* of the required CI-split). So:
- **Move `86cag93zb` `in progress` → `to do`** (no agent = not in progress).
- **Re-scope its remaining AC to AC4 (runner-unpin) ONLY** (AC1–3 done — cite #248/#250 on the ticket).
- **Note AC4 is BLOCKED** on an overlay-gate RT-migration follow-up that does not yet exist (settings/loot/water/inv-drag-ghost + pond). **Cleaner alternative (orch's call):** close `86cag93zb` as AC1–3-complete and spin a fresh ticket `finish the CI-split: migrate overlay gates → RT + unpin runner-1` — keeps the closed ticket honest + the open one accurately scoped. Either path works; **re-scope-to-AC4 is the lower-friction default.**

**Recommended action:** `86cag93zb` → `to do`, re-scope to AC4 runner-unpin, add overlay-migration dependency note (or close + fresh CI-split ticket per the alternative).

## B2 — `86caffwv5` (attack animation per weapon) → premise UNCHANGED

**Evidence:** PR **#263** (`feat(character): castaway v3 DORMANT integration + R&D harvest`, merged 2026-07-06 11:46Z) `mixamo/` folder contains exactly: **`Breathing Idle.fbx`, `Idle.fbx`, `Running.fbx`, `Walking.fbx`** (`art-src/castaway-v3-rodin-export-lowpoly/mixamo/`). These are **locomotion** clips for the new v3 rig — **no chop / swing / attack clip present.**

**Finding:** the v3 Mixamo set does **NOT** change `86caffwv5`'s premise. `86caffwv5` = per-weapon **attack-clip** import/wiring, sponsor-gated on the Sponsor supplying each per-weapon swing clip (`[[chop-swing-mixamo-clip-not-procedural]]`). #263 shipped only locomotion for the rig swap — the attack-clip supply gate stands. **Keep the `sponsor-gate` tag; no re-scope.**

**One coordination note (not a premise change):** v3 is becoming the **live hero rig** (#264 activation open). When `86caffwv5` eventually runs, its attack clips must be authored/retargeted against the **v3** rig (41 bones, `mixamorig:Hips`), not v2. Add a cross-ref to the v3 activation (`86cak9kau`) on the ticket so the clip target is unambiguous when the Sponsor supplies clips.

**Recommended action:** `86caffwv5` → no status change; confirm `sponsor-gate` tag present; add cross-ref "attack clips target the v3 rig (post-#264)".

## B3 — merged-PR tickets that may still show an open status → VERIFY the 2026-07-05 batch

**Confirmed drift:** `86cag93zb` (see B1) — merged work (#248/#250) but `in progress`. That's one.

**VERIFY (cannot confirm board status from git):** the **2026-07-05 merge batch** landed during the away / **PC-crash** window (STATE.md 2026-07-05 12:0x: *"PC CRASHED mid-session"*) — exactly when the merge-time `in review → complete` flip gets dropped (auto-merge-to-main is orch-gated + the crash killed the session). Orch: verify each ticket's board status + flip any still non-terminal. Evidence (PR → ticket → mergedAt):
- **#255** → `86cajt6j8` — 2026-07-05 07:19Z (`test(playmode): stabilize 8 chop-family reds`)
- **#256** → `86caj0rrg` — 2026-07-05 07:19Z (`fix(worldgen): isolate NextIslandPoc sky material`)
- **#257** → `86cajt6jb` — 2026-07-05 07:20Z (`fix(worldlook): enforce fog seam-kill`)
- **#258** → `86caju054` — 2026-07-05 10:04Z (`chore(debug): retire sneak-isolation panel`)
- **#259** → `86cajt6kq` — 2026-07-05 11:32Z (`fix(ci): settings-gate wedge-hardening`)
- **#262** → `86cajx050` — 2026-07-05 21:55Z (`feat(character): activate Castaway v2 as default`)

(#260 `castaway v2 harvest` + #261 `docs(castaway-v2)` carry no ticket-id in title — likely docs/harvest folds, no board flip needed; verify if either maps to a tracked ticket.)

**Already-confirmed-complete (NOT drift, per STATE.md 14:4x):** `86cak41d4` (#263), `86cajkk7h` (#254), `86cahnmjv` (closed). No action.

**Recommended action:** orch does a targeted board read of the six 2026-07-05-batch tickets + `86cag93zb`; flip any merged-but-open to `complete` (or `86cag93zb` per B1).

---

## Orchestrator action summary

1. **Create 3 child tickets** under parent `86cahwwvg` from C2/C3/C4 above (tags per child); dispatch order C2 → C3 → C4, serialized on the build slot; verify `86cahmxh6` snow-cap status before/if any C-child touches `NextIslandPocGen`.
2. **B1:** `86cag93zb` → `to do`, re-scope to AC4 runner-unpin + overlay-migration dependency note (or close + fresh CI-split ticket).
3. **B2:** `86caffwv5` → keep sponsor-gate; add v3-rig cross-ref.
4. **B3:** verify + flip the six 2026-07-05-batch tickets still showing open status.
