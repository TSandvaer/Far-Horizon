# Island 2.0-D (C4) — Perf re-measure + verdict at final size/density

**Ticket:** `86cakk4xf` (child C4 of `86cahwwvg`). **Date:** 2026-07-07. **Author:** Devon.
**Spec:** `team/priya-pl/island-2.0-splits.md` §C4. **Build under test:** HEAD `d9296e5` (C1 #246 + C2 #271 + C3 #277 all merged — the final populated POC scene).

**What this measures (real-world anchor):** the grown, fully-populated POC island — a ~1200u-diameter organic island with a climbable snow-capped hero mountain, a dense multi-species forest, meadow grass, rock scatter, and free-standing rocky walls + stone slabs. The question C4 answers: *does the shipped exe hold the target frame rate at this final size + prop load, or does it need chunk-LOD / a density dial-down?* A real number, not a vibe.

---

## TL;DR — VERDICT: HOLDS. Single scaled mesh + static-batched scatter. No chunk-LOD, no density cut.

- **Shipped (release) exe:** pegged at the **60 fps vSync cap** — `avgFPS 60.0` at gameplay framing AND while traversing/climbing.
- **Shipped (development) exe, vSync OFF:** **2.05 ms/frame avg = 488 fps uncapped** — ~8× frame-time headroom over the 60 fps (16.67 ms) budget. Even the single worst frame (11.52 ms = 87 fps) stayed above 60.
- **Caster policy HELD** — 35 avg / 42 max shadow-caster draws (dominated by rock, NOT vegetation); shadow pass is 0.054 ms = 2.7% of the CPU frame. No silent +vegetation regression from C2/C3.
- **GPU trivially loaded** (0.21 ms GPU frame time), 56 draw calls (SRP Batcher), 0.94M tris / 1.08M verts in frustum, 0.44 KB/frame GC.
- The #226 verdict (60 fps @ ~800u dia, MeanShoreR 400) **extends** to the 600u-MeanShoreR (~1200u dia) populated island. **Further scale-up has ample headroom** (go, with the bounds below).

---

## Method + provenance

Two shipped-build runs (never the editor — the proven divergence class, `unity-conventions.md`; `[[verify-soak-builds-or-bake-and-judge]]`):

1. **Release exe** `Build/Windows/FarHorizon.exe` (stamp `zoned | ... | d9296e5`), built via `tools/debug/build_poc_island.sh` (bootstrap → author POC scene → build → stamp-verify → capture-gate → `-verifyPocIsland`). vSync ON (the scene's shipped setting) → the number the Sponsor actually sees on the FPS counter (#236). Windowed 1280×720.
2. **Development exe** `Build/WindowsDev/FarHorizon.exe` (same HEAD stamp), built with `FarHorizonBuilder.BuildWindows -development` (#235 S2a) and run with `-perfProbe` (#235). The probe uncaps vSync + walks the island via `WasdMovement.SetInputOverride`, sweeping the heading through forest/coast/interior for 20 s, reading `ProfilerRecorder` render counters per frame. This is the honest frame COST + the scene-stats caster trace (a release-shape player compiles the profiler out).

**Hardware caveat:** measured on ONE machine — the dev workstation (the same GPU class the Sponsor soaks on). NOT tested on a weak iGPU. Dev-build profiler overhead makes the uncapped number CONSERVATIVE (release would be faster).

---

## Scene inventory (from the shipped-build `[poc-trace] Scatter` line, seed 7)

| Class | Count | Casts shadows? |
|---|---|---|
| Broadleaf trees (`LP_Tree`) | 825 (×2 renderers: trunk+canopy) | **No** |
| Pine trees (`LP_PineTree`) | 435 (×2 renderers: trunk+crown) | **No** |
| Bushes (`LP_Bush`) | 210 | **No** |
| Grass clumps (`LP_Grass`) | 1449 | **No** |
| Rock scatter (`LP_Rock`) | 252 | Yes (C1 default) |
| Rocky walls (`LP_RockWall`) | 6 | Yes (C2 hero) |
| Stone slabs (`LP_StoneSlab`) | 12 | Yes (C2 hero) |
| Terrain (single welded mesh, `Seg=386`) | 1 | Yes |
| Sea (single mesh) | 1 | No |

`treeTarget = 1260` (area-scaled from the #226 anchor). Total renderers ≈ 4.45k; total **shadow casters ≈ 271** (terrain + 252 rock + 18 hero features) — everything else (≈4.18k vegetation renderers) is `castShadows:false`.

---

## Measurement 1 — Release exe, vSync-capped (`-verifyPocIsland`, Sponsor-facing)

| Framing | avg FPS | min FPS (worst single frame) | window |
|---|---|---|---|
| `gameplay_static` (spawn, facing hero mountain, forest+sea+peak in frustum) | **60.0** | 52.5 | 151 frames / 2.51 s |
| `traversal_climb` (agent driven up the mountain flank, moving) | **60.0** | 58.0 | 721 frames / 12.01 s |

- NavMesh coverage: **190/192 (99.0%)** walkable across the ~1200u island; highest reachable Y 139.1u; climb `gainedY 50.3u` (the hero peak is a climbable giant hill, not a wall).
- The `gameplay_static` min of 52.5 fps is a SINGLE settle frame at window-open (right after the camera repositions) — the 12 s `traversal_climb` window (the steady-state read) never dropped below 58, and both averages are pinned to the 60 Hz cap. i.e. the release exe is vSync-bound, not compute-bound.

## Measurement 2 — Development exe, UNCAPPED (`-perfProbe`, honest cost — 9761 frames / 20 s island walk)

| Metric | Value | Read |
|---|---|---|
| **frame time** | avg **2.05 ms** · min 1.33 · max 11.52 ms | **488 fps uncapped** — ~8× under the 16.67 ms/60 fps budget |
| GPU frame time | 0.21 ms | GPU nowhere near bound |
| Draw calls | avg 56 · max 66 | SRP Batcher folding the ~1-material world; not draw-bound |
| SetPass calls | 23 | low |
| **Shadow-caster draws** | **avg 35 · max 42** (63% of draws) | see caster check below |
| Shadow-pass CPU | 0.054 ms (**2.7% of the CPU frame**, marker `MainLightShadow`) | the shadow pass is NOT the cost here (contrast the start island's ~1300-caster shadow headline) |
| Triangles / Vertices | 0.94M / 1.08M avg in frustum | comfortable for desktop |
| GC per frame | 0.44 KB | healthy (no per-frame churn) |

---

## Caster-policy check (the silent-regression gate C4 exists to confirm)

**Held.** The likeliest silent regression from C2/C3 was a mis-set caster flooding the shadow pass with vegetation. The empirical shadow-caster-draw count is **35 avg / 42 max** — tiny, and dominated by the `LP_Rock` scatter + hero wall/slab features near the camera (culled by the 220u shadow distance + frustum). Had any vegetation class (≈4.18k tree/bush/grass renderers) been casting, the in-view caster count would be in the hundreds-to-thousands and the shadow pass would dominate the frame. It is 2.7% of the frame. C3 added zero shadow casters (all vegetation `castShadows:false`); C2 added exactly the 18 intended hero rock features (`castShadows:true`). Policy confirmed against the SHIPPED scene bytes (not just code — guards the committed-scene-stale class `[[unity-procedural-committed-assets-go-stale]]`).

**Observation (not a change):** the 252 `LP_Rock` scatter boulders still cast shadows (C1's default). They are the bulk of the caster count. They are NOT a regression (present + measured since #226) and the frame has enormous headroom, so no action — but if perf ever tightens at a larger size, flipping `LP_Rock` to `castShadows:false` (R2a-style, ~decoration-scale) is the first, cheapest lever before any chunking.

---

## `treeTarget` recommendation (recommend only — the raise is a follow-up)

With ~8× frame-time headroom, `treeTarget` (currently 1260) could be raised substantially — perf is NOT the constraint on forest density. Per §C3/§C4, the raise itself is a **Sponsor-soak / follow-up decision** (density is a LOOK call, not a perf call), so this PR does NOT apply a raise — it records that the headroom exists. No density dial-down is needed (the anti-goal); the current load holds decisively.

---

## Bounds (what this verdict does and does NOT cover)

- **Bar tested:** 60 fps hold at the final size + prop load, on ONE desktop GPU, at 1280×720 windowed, both static and during traversal/climb. Uncapped headroom measured on the dev build.
- **NOT tested:** weak iGPU / a second machine (the quality-tier split, start-island perf note R7, is the lever if that matters); GPU Resident Drawer / Forward+ (still off — not needed at this load, `unity6-mastery.md` §2/R6); native/4K resolution (measured at 1280×720; the world is vertex-color + low draw-call, so resolution scales GPU-side only, which is at 0.21 ms — deep headroom); a much LARGER island (a fresh re-measure, gated by THIS verdict — do not free-grow past ~1200u without re-running).
- The release number is vSync-capped ("holds 60"), not an uncapped release ceiling; the uncapped ceiling comes from the (conservative) dev build.

---

## Regression guard

`Assets/Tests/EditMode/NextIslandPocTests.cs :: C4_VegetationCasterPolicy_ShadowsOff_KeepsShadowPassInBudget` — runs the real scatter over the real built terrain and asserts every `LP_Tree`/`LP_PineTree`/`LP_Bush`/`LP_Grass` renderer (trunk + canopy included) is `ShadowCastingMode.Off`, with per-class non-vacuity. Reds in CI if a future edit ships a vegetation caster (the silent shadow-pass inflation this note rules out). Pairs with the existing `C2_RockFeatures_HeroCasterPolicy_ShadowsOn_AndStaticBatched` (the ON side).
