# Far Horizon — Performance & Technical-Architecture Analysis (generated poly world)

**Scope:** procedural world pipeline (sky, clouds, sun, water/foam, beach, terrain, trees, stones, bushes, grass, pond), runtime hot paths, URP/project/build settings, generated-asset hygiene.
**Method:** static analysis of the working tree at `c:\Trunk\PRIVATE\Far-Horizon` (branch `orch/coordination`, HEAD `844e8d5`). No profiler data was captured — every magnitude below is **derived from code/settings, labeled as estimate**, and per `unity6-mastery.md §4` the first action for any item marked "verify" is a Profiler/Frame-Debugger pass on a development build of `FarHorizon.exe`.
**Branch note:** `origin/main` has the sun-disk skybox (PR #194, `b7a1c47`); this working tree does not (`git merge-base --is-ancestor` → NOT in HEAD; no `_SunDirection` hits in `Assets/`). Findings below apply to both states — the sun disk only adds a few ALU to the background pass.

---

## 0. Scene inventory (derived from the generators — the numbers everything else hangs on)

| Item | Count / size | Source |
|---|---|---|
| Terrain mesh | 201×201 grid = **40,401 verts** (UInt16), clipped to the island + skirt; MeshCollider on the same mesh | `LowPolyZoneGen.cs:210-213, 627-672` |
| Sea mesh | 160×160×2 tris emitted **unwelded** = **153,600 verts** (UInt32) with colors + normals, all at y=0 | `LowPolyZoneGen.cs:1511, 1544-1648` |
| Trees | 320 × 2 renderers (trunk + canopy) = **640 renderers**, every mesh unique (seeded) | `LowPolyZoneGen.cs:932-951, 1178-1192` |
| Rocks (boulders) | 60, unique `FacetedRock` meshes | `LowPolyZoneGen.cs:962-980` |
| Grass clumps | 360, unique meshes | `LowPolyZoneGen.cs:988-1001` |
| Bushes | 80 (+~32 berry-cluster child renderers, 40% berry rate) | `LowPolyZoneGen.cs:1013-1032, 1361-1403` |
| Sticks | 70 | `LowPolyZoneGen.cs:1053-1070` |
| Small stones | 70 (child `StoneMesh` renderer each) | `LowPolyZoneGen.cs:1093-1111, 1458-1486` |
| Clouds | 6-10, unique `CloudBlob` meshes, `CloudDrift` each | `WorldBootstrap.cs:315-364` |
| Vista | 15 peaks + 5 landmass bases across 5 island clusters | `WorldBootstrap.cs:444-455, 520-552` |
| **Total renderers** | **≈1,350** before culling (~1,312 scatter + world/gameplay objects) | derived |
| Materials | Small set by design: quantize-cached (~15-25 total; canopy 1, bush 1, rock ~4, grass few, trunk/stick 1-2 flat URP/Lit) | `LowPolyZoneGen.cs:1720-1845` |
| Shaders in the hot path | 2 opaque (`LowPolyVertexColor`, URP/Lit) + 1 transparent (`LowPolyWater`) + skybox | `Assets/Shaders/` |
| Committed scene | `Boot.unity` = **14.17 MB, binary** (all unique meshes serialized inline; 1,173 scatter-name hits in the binary) | `ls Assets/Scenes`; `od` header check |

**Honest headline:** on the Windows desktop target this scene is *small*. Total scene geometry is well under 1M verts, texture memory is near zero (vertex-color world), and there are only two realtime lights (1 shadowed directional + 1 unshadowed campfire point — `MovementCameraScene.cs:2234` correctly `LightShadows.None`). A mid-range discrete GPU will not be GPU-bound on the main pass. The real costs, in likely order: **(1) the shadow pipeline** (4096 map, 4 cascades, 220u distance, ~1,300 casters), **(2) per-frame full-screen copies nothing consumes** (Opaque Texture), **(3) draw-call count without any batching-friendly structure** (unique mesh per instance defeats instancing; nothing static-batched except the vista), **(4) scene-load/memory overhead of the 14MB embedded-mesh scene**. CPU scripting and GC are basically healthy.

### What classic optimizations DON'T matter here (don't spend on these)
- **Poly-count reduction / LODs / mesh simplification** — the whole island is a rounding error for a desktop GPU. LOD groups would *add* CPU cost and (per `unity6-mastery.md §2`) cross-fade LOD is GRD-incompatible anyway. Skip.
- **Texture compression/streaming** — the world has ~3 small textures (castaway diffuse/normal 2048 BC-compressed, `weapon_palette.png` 128 point-filtered, both `isReadable: 0`, correct — meta files verified). Nothing to do.
- **GC pressure in gameplay code** — no LINQ in `Runtime/` (grep verified), no per-frame `new List`, `gcIncremental: 1` (`ProjectSettings.asset`), needs/pickables are event-driven or cached-list. The only per-frame allocs are IMGUI strings (C2) and the jump trace (C1) — fix those two, and GC is a non-issue.
- **Update()-count micro-optimization** — ~200 MonoBehaviour Updates (70 stones, ~32 bushes, needs, tools) is negligible on desktop. A central ticker is nice-to-have hygiene, not a perf item.
- **Occlusion culling** — no baked occlusion data exists (bootstrap never bakes it; the `OccluderStatic` flags on vista peaks at `WorldBootstrap.cs:550-551, 582-583` are inert without it). At this GPU load, don't bother; GPU occlusion culling would additionally require Forward+ + Render Graph (S1).

---

## 1. Rendering findings (R#)

### R1 — Unused full-screen Opaque Texture copy every frame — **HIGH payoff, S effort, LOW risk**
**Now:** `BootstrapProject.ConfigureUrp` sets `urp.supportsCameraOpaqueTexture = true` (`BootstrapProject.cs:188`), baked into `FarHorizonURP.asset` (`m_RequireOpaqueTexture: 1`, `m_OpaqueDownsampling: 1`). Grep of all shaders/code: **nothing samples `_CameraOpaqueTexture`** — `LowPolyWater.shader` samples only scene **depth** (`DeclareDepthTexture.hlsl`, `LowPolyWater.shader:95, 185-189`). The only references are the bootstrap line and the EditMode test that pins it ON.
**Why it matters:** URP performs a color copy of the opaque target every frame (plus the downsample) that no shader ever reads — pure bandwidth/blit waste on every shipped frame.
**Fix:** set `supportsCameraOpaqueTexture = false` in `ConfigureUrp`, regen + commit the URP asset, and update `ActiveUrpAsset_HasDepthAndOpaqueTextureEnabled` to assert depth-ON / opaque-OFF. Keep `supportsCameraDepthTexture = true` (the foam needs it).
**Payoff:** med-high (a full-res copy at 1600×900+ per frame). **Effort:** S. **Risk:** low — verify with the existing sea/pond capture gates.

### R2 — Shadow pipeline is the single biggest configured cost — **HIGH payoff, M effort, MED risk (soak-gated)**
**Now:** `FarHorizonURP.asset`: `m_MainLightShadowmapResolution: 4096`, `m_ShadowCascadeCount: 4`, `m_ShadowDistance: 220`, soft shadows ON at `m_SoftShadowQuality: 2` (High, 7×7 tent). Set deliberately in `BootstrapProject.cs:189-237` to kill the shadow-boundary/flicker percepts (86ca9qwr3, 86caayvfz — history documented in-file). Every scatter prop casts shadows: `MakeMeshObject` (`LowPolyZoneGen.cs:1343-1352`) leaves `shadowCastingMode` at the default **On** for all trees, rocks, **360 grass clumps**, bushes, berry clusters, **70 sticks**, **70 pebbles**. Only clouds/vista/sea explicitly opt out (`WorldBootstrap.cs:355, 546, 579`; `LowPolyZoneGen.cs:1535`).
**Why it matters:** the shadow pass re-renders every caster into up to 4 cascade slices — with ~1,300 casters this roughly **doubles-to-triples total draw calls**, and the 4096 soft-filtered map is the heaviest GPU line item in the frame. A 0.5u grass tuft or a 5cm-girth stick produces no visible shadow at gameplay framing (orbit ~6-20u, pitch ~55°) but still costs its cascade draws.
**Fix (two independent halves):**
1. **Safe half (no look change):** in the scatter builders, set `ShadowCastingMode.Off` on grass clumps, sticks, small stones, and berry-cluster children (keep trees, boulders, bush bodies). That removes ~530 casters (~40%) from every cascade. Add an optional `castShadows` arg to `MakeMeshObject`. Regen + commit scene.
2. **Soak-gated half:** trim `shadowDistance` 220 → ~120-140 and/or cascades 4 → 3. The 220u value exists because the WASD player can roam the island and the boundary ring was visible (`BootstrapProject.cs:198-206`) — so this half MUST go through a Sponsor soak with the cascade-border fade re-checked. Do not bundle it with the safe half.
**Payoff:** high (shadow pass is the dominant configured cost). **Effort:** M (regen+commit+capture). **Risk:** half 1 near-zero; half 2 re-opens a soaked percept — sequence separately.

### R3 — No batching-friendly structure in the scatter: unique mesh per instance, no static flags — **MED payoff, M effort, LOW-MED risk**
**Now:** every scatter instance gets a **freshly generated Mesh object** (seeded variation: `LowPolyZoneGen.cs:1178, 1190, 1258, 1276, 1377, 1388, 1436, 1475`) — even the fixed-geometry mid-tree trunk (`trunkH=1.6, botR/topR` constants, `:1176-1178`) creates a new mesh per tree. Consequences: (a) GPU instancing / GPU Resident Drawer can never merge them (instancing requires identical meshes), (b) nothing in the scatter is marked `BatchingStatic` — only the vista peaks/bases are (`WorldBootstrap.cs:550, 582`) — so URP issues ~1,300 individual SRP-batched draws in the main pass + the cascade passes.
**Why it matters (honestly bounded):** the SRP Batcher already makes per-draw CPU cost small on desktop; at ~1,300 draws + shadows the total render-thread cost is real but not fatal (estimate: a few ms). This matters most **combined with R2** (cascades multiply the draw count) and as scaling headroom (Sponsor wants MORE world: bigger island, more density).
**Fix options (pick per class):**
1. **Static-batch the non-animated scatter:** rocks, grass, sticks, stones, bush bodies, berry clusters never move — they only `SetActive`-toggle on loot (`StoneProp.cs:251-255`), which static batching supports. Add `GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic)` in their builders. **Trees must stay dynamic** — felling tips/sinks/scales the transform (`ChopTree.cs:103-107, 244`), and clouds drift. Static batching is the right tool *here* because GPU Resident Drawer is OFF and can't help unique meshes anyway; the mastery doc's "don't static-batch" rule (`unity6-mastery.md §2`) is conditioned on GRD being the batching path — record the deviation + rationale in the doc when adopting.
2. **(Bigger rework, only if scaling demands it):** move per-instance variation from geometry into transform (yaw/scale/lean already exist) + a small pool of N shared meshes per prop class (e.g. 8 seeded rock variants reused). That unlocks instancing/GRD later and shrinks the scene file (G1). Costs visual-diversity review — the per-instance mesh jitter is part of the approved look, so a shared-pool change is soak-gated.
**Payoff:** med now, high at 2-3× density. **Effort:** M (option 1) / L (option 2). **Risk:** option 1 low (verify chop/loot/berry toggles in PlayMode + capture); option 2 med (look).

### R4 — Transparent sea: overdraw is bounded, but the mesh is 3× bigger than it needs to be — see G2 for the weld; keep the queue decision as-is
**Now:** the sea is one 1400×1400u transparent plane (`ZWrite Off`, full-screen-ish when framed; `LowPolyWater.shader:70-81`), alpha 1, depth-fade foam sampling `_CameraDepthTexture` per fragment.
**Why it matters:** transparent water re-shades every visible sea pixel over the already-shaded opaque scene — this was a **deliberate, documented trade** (foam requires depth sampling; `LowPolyWater.shader:10-28`, `lowpoly-quality.md` Rec 3 flags the overdraw budget). Sea pixels under the island fail depth test and cost ~nothing. One layer of transparency at desktop res is acceptable; the frag itself is cheap (one depth sample + a few lerps + fog).
**Verify, don't churn:** the ticket's own AC5 (FPS A/B on the shipped build) is the right gate; nothing further needed unless the profiler shows GPU-bound at the sea view. Do NOT add a second transparent layer (e.g. pond + sea overlapping) — the pond hole in the sea mesh (`LowPolyZoneGen.cs:1587-1637`) already prevents stacking. Good pattern; keep.

### R5 — `URP/Lit` + `URP/Unlit` in AlwaysIncludedShaders force-compiles their full variant space — **MED payoff (build time/size), S effort, LOW-MED risk**
**Now:** `ProjectSettings/GraphicsSettings.asset m_AlwaysIncludedShaders` contains, besides the four tiny FarHorizon shaders, `933532a4fcc9baf4fa0491de14d08ed7` = **URP `Lit.shader`** and `650dd9526735d5b46b79224bc6e94025` = **URP `Unlit.shader`** (both GUIDs verified against `Library/PackageCache/com.unity.render-pipelines.universal@cdf909593d80/Shaders/*.meta`). Pinned by `WorldBootstrap.cs:106-108` ("pin it so scatter never strips").
**Why it matters:** Always-Included shaders skip build-time variant stripping — Unity compiles **every** variant of URP/Lit (a notoriously huge keyword space) into every build. That inflates build time on the **single CI Unity slot** (the project's stated bottleneck) and player shader data. The pin is unnecessary for the scatter: the trunk/stick materials are **serialized into Boot.unity at editor time** (`LowPolyZoneGen.cs:1713-1732` — "assigned to sharedMaterial they serialize INLINE into the saved scene"), so normal build collection includes URP/Lit with exactly the used variants. The magenta-strip failure class that motivated pinning applies to shaders only referenced via runtime `Shader.Find` — which in the shipped path is only the four custom FarHorizon shaders (fallback branches).
**Fix:** remove URP/Lit + URP/Unlit from the always-included list in `WorldBootstrap.BuildEnvironment` (keep the four FarHorizon shaders — tiny variant spaces, genuinely runtime-found in fallbacks); regen, commit, and gate on the existing shipped-build capture (any strip regression = magenta = instantly caught by the capture gates).
**Payoff:** med — CI build minutes + player size; zero frame-rate effect. **Effort:** S. **Risk:** low-med (strip regressions are loud and capture-gated).

### R6 — Renderer is plain Forward, not Forward+; GPU Resident Drawer off — **alignment item, LOW payoff today, defer deliberately**
**Now:** `FarHorizonRenderer.asset m_RenderingMode: 0` (Forward), `FarHorizonURP.asset m_GPUResidentDrawerMode: 0` (off). The project's own mandatory doc says Forward+ first (`unity6-mastery.md §1`).
**Honest assessment:** with exactly one directional + one unshadowed point light, Forward+ buys nothing today (its win is the per-object-light cap removal and clustered lighting). GRD would buy nothing either — it needs shared meshes (R3) to instance. So this is doc-vs-config drift, not a live perf bug.
**Fix/when:** flip to Forward+ in a quiet window **before** the night/torches/many-campfires milestone (the vision doc's bonfire/night arc) and before any GPU-occlusion experiment, since both require it; verify via capture (Forward+ should be visually identical here). Update either the renderer asset generation or the mastery doc so they agree — today each contradicts the other, and per the project's committed-asset rule a hand-edit of `FarHorizonRenderer.asset` would be silently reverted by the next bootstrap (`BootstrapProject.cs:173-175` regenerates it).
**Payoff:** ~0 now, prerequisite later. **Effort:** S. **Risk:** low (capture-verified).

### R7 — All quality tiers share one URP asset — no perf scaling story on weaker desktops — **LOW-MED payoff, M effort, LOW risk**
**Now:** all 6 quality levels point at the same `FarHorizonURP.asset` (`QualitySettings.asset` — every tier's `customRenderPipeline` = guid `74382d06729bd8d4bb19c4446f4ca44b`; `BootstrapProject.cs:240-245` assigns the one asset to every level). Current level = Ultra (`m_CurrentQuality: 5`, vSync 1 — compliant with `unity6-mastery.md §12`).
**Why it matters:** the 4096/4-cascade/soft-high shadow stack (R2) is tuned for the Sponsor's machine. On an iGPU laptop it will be the first thing to hurt, and there's no dial. The game already ships difficulty *gameplay* tiers; graphics has none.
**Fix (when a second machine matters, not before):** generate 2-3 URP assets in `ConfigureUrp` (e.g. Low = 2048 map / 2 cascades / 90u distance / soft-low) and assign per quality level. Zero effect on the Sponsor's soaks (Ultra unchanged).

---

## 2. Project/build settings (S#)

### S1 — Gamma color space — technically wrong for a URP+HDR+post pipeline, but LOOK-LOCKED; document, don't flip casually
**Now:** `ProjectSettings.asset m_ActiveColorSpace: 0` (**Gamma**). The pipeline runs HDR (`m_SupportsHDR: 1`, R11G11B10) with Bloom + filmic-ish Neutral tonemap + WhiteBalance (`QualityPassGen.cs:114-184`).
**Why it matters:** lighting math, bloom thresholds, and tonemapping are all designed for linear; in gamma space light accumulation is physically off and banding behavior differs. **However** — every palette constant in the generators was empirically soaked against the *gamma* output (dozens of documented pixel-sampled tuning rounds, e.g. `LowPolyZoneGen.cs:104-140` sea-tuning saga). Flipping to Linear re-lights the whole approved look and would invalidate essentially all Sponsor-soaked color work.
**Recommendation:** accept gamma as a **deliberate, documented constraint** (add one line to `unity6-mastery.md` so future agents stop "fixing" it), and only revisit as part of an explicit look-overhaul milestone. Perf impact: none either way on desktop.

### S2 — Mono scripting backend (IL2CPP mandated by the project's own doc) — **LOW-MED payoff, M effort, MED risk to CI time**
**Now:** `ProjectSettings.asset scriptingBackend: {}` (empty → default **Mono**) + `managedStrippingLevel: {}` (default). `unity6-mastery.md §10` mandates IL2CPP + source-line numbers for the Windows player. `FarHorizonBuilder.BuildWindows` builds with `BuildOptions.None` (`FarHorizonBuilder.cs:40-46`) — no development-build path exists for profiling either.
**Honest assessment:** the runtime scripts are light (Section 3), so IL2CPP's CPU win here is small; the bigger wins are shipping-hygiene (stripping, no JIT hitches) and crash-stack quality. The real cost: IL2CPP roughly doubles+ build time on the **single CI runner** — that's the project's scarcest resource.
**Fix:** (a) add a `-development` flag path to `FarHorizonBuilder` now (S effort — required for honest profiling per `unity6-mastery.md §4`); (b) move release soaks to IL2CPP only when a nightly/second-lane CI exists, or accept Mono explicitly in the doc. Either way, resolve the doc-vs-config contradiction on purpose. (Note: several code comments already *assume* IL2CPP release stripping — e.g. `StoneProp.cs:257-263` — the assumption currently holds only via `[Conditional("UNITY_EDITOR")]`, which strips in ANY player build, so those traces are fine under Mono too.)

### S3 — `m_UseAdaptivePerformance: 1` (URP asset) — mobile feature, no package installed; harmless no-op. Clear it next time the URP asset regenerates, purely for intent-clarity. **Trivial.**

### S4 — Committed `FarHorizonURP.asset`/`FarHorizonRenderer.asset` are bootstrap-regenerated — settings drift is structurally prevented (good). `ConfigureUrp` re-creates both from code every run (`BootstrapProject.cs:163-247`), so the committed assets always match code — the same committed-generated pattern as the scene. Any fix in R1/R2/R6 must be made in `ConfigureUrp`, never by editing the .asset (it would be silently reverted — the in-file comments already warn this).

---

## 3. Runtime hot paths (C#)

### C1 — Shipped per-frame `Debug.Log` jump trace — **MED payoff (hitch removal), S effort, LOW risk**
**Now:** `CastawayCharacter.EmitJumpTrace` fires a ~700-char interpolated `Debug.Log` **every frame** from lift-off to ~post-land, in the shipped build, on every jump, by design ("no toggle, no launch-arg" — `CastawayCharacter.cs:1197-1275`, called from `LateUpdate` at `:1194`). `m_StackTraceTypes` = ScriptOnly for Log (`ProjectSettings.asset`), so each call also captures a managed stack trace and writes to `Player.log`.
**Why it matters:** string interpolation + stack capture + synchronous-ish log I/O during the most motion-sensitive moment (a jump) is a classic micro-hitch source, and it violates the project's own rule (`unity6-mastery.md §5/§10`). Magnitude: est. 0.1-0.5ms/frame + GC while jumping — small, but it lands exactly where frame consistency is felt.
**Fix:** keep the instrument (it earned its keep) but gate it: fire only when `-jumpTrace` is on the command line (the project's established launch-flag idiom, cf. `FullscreenBoot.CaptureFlags`) or behind `DebugOverlays.Visible`. The soak workflow keeps the diagnosis path (Sponsor launches with the flag when a jump feels wrong).

### C2 — IMGUI HUD layer: ~10 `OnGUI` components, per-frame string concat — **LOW-MED payoff, S effort short-term, M long-term**
**Now:** shipping UI is IMGUI: `BootHud` (`"BUILD " + BuildInfo.Stamp` allocates every OnGUI call — `BootHud.cs:36`), `SurvivalHud` (ledger string rebuilt every call — `SurvivalHud.cs:228-231`; 3 bars × ~13 `GUI.DrawTexture` quads), `LootPrompt`, plus 7 debug tools with `OnGUI` (grep: 10 files). Debug panels early-return when hidden (`DebugOverlays.Visible` master gate), but Unity still invokes each enabled `OnGUI` multiple times per frame (Layout+Repaint) and runs the IMGUI event loop.
**Why it matters:** `unity6-mastery.md` Quick-Reference explicitly bans `OnGUI` for runtime UI. Magnitude on desktop: est. 0.1-0.4ms/frame + a steady trickle of GC (strings, GUIContent) — not a frame-killer, but it is the largest steady per-frame GC source in the build.
**Fix, cheap first:** (a) set `useGUILayout = false` on every OnGUI MonoBehaviour (kills the Layout pass — one line each); (b) cache `BootHud`'s stamp string in `Awake` (it never changes) and rebuild `SurvivalHud`'s ledger string only on `Inventory.Changed` (event already exists — `InventoryUI.cs:95` subscribes to it). **Long-term:** fold BootHud/SurvivalHud/LootPrompt into the existing UI Toolkit stack (InventoryUI/SettingsPanel already use UXML/USS properly, `InventoryUI.cs:141-154`) — do it when the HUD next needs a feature, not as a standalone perf PR.

### C3 — Per-frame proximity/tick scans are all bounded and fine — **no action**
Verified clean: `PickableLooter` caches the pickable list, resolves only on E-press; `LootPrompt` polls `NearestInRange()` once/frame over ~170 cached entries — microseconds (`PickableLooter.cs:73-104, 165-199`). `ChopTree` is ONE manager ticking 321 tree states/frame (cheap branch checks, `ChopTree.cs:480-484`). `SurvivalNeed.Update` ×3, `StoneProp.Update` ×70 + `BerryBush.Update` ×~32 are timer checks. `OrbitCamera.LateUpdate` = 2 raycasts (`OrbitCamera.cs:285-315`); ground-snap + camera basis a few more — single-digit raycasts/frame total. `CloudDrift` ×~8 trivial. No `Camera.main` in per-frame paths (`ClickToMove` caches at `:68` with a null-refresh fallback). Movement reads legacy Input directly — the deliberate, documented choice (`activeInputHandler: 0`, rationale in `WasdMovement.cs:199-216`); leave it.

### C4 — Skinned character: one rig, full bone hierarchy (`optimizeGameObjects: 0` in the FBX metas) — required by the procedural pose chain (`CastawayArmPose`/`HeldAxeRig` need exposed bones). One character = negligible. Revisit only if NPC count grows (then: exposed-bone whitelist instead of full hierarchy). Animator culling stays default AlwaysAnimate — correct for the always-on-screen player.

---

## 4. Generated content & pipeline hygiene (G#)

### G1 — 14.2MB binary Boot.unity: unique meshes inline; growth is unbounded as the world grows — **MED payoff (repo/CI/load), M-L effort, structural**
**Now:** every generated mesh (sea 153K verts, terrain 40K, ~1,600 scatter meshes) serializes inline into the committed binary `Boot.unity` (14,173,596 bytes). `EditorSettings m_SerializationMode: 2` (ForceText) notwithstanding, the scene is binary on disk (verified by header) — consistent with the project's own "Boot.unity is binary" convention note (`unity-conventions.md` §Binary-scene PR conflicts). Every world regen re-commits ~14MB; binary scene = unmergeable (the documented regenerate-on-rebase workflow exists to cope).
**Why it matters:** repo growth per regen, slower CI checkouts/clones, slower scene load (deserializing 1,600 mesh objects), and the merge-conflict tax the team already pays. The committed-snapshot-goes-stale failure class ([[unity-procedural-committed-assets-go-stale]]) gets worse as the payload grows.
**Fix options, in ascending ambition:**
1. **Shrink the payload in place:** G2 (weld the sea, −~5-6MB) + R3-option-2 (shared mesh pools) would cut the scene by well over half.
2. **Split meshes out of the scene:** have the generators `AssetDatabase.CreateAsset` each mesh (or one combined asset per prop class) under `Assets/Generated/` — text-or-binary per-file, stable GUIDs, only *changed* meshes re-serialize on regen, and the scene file itself becomes small + rebase-friendlier. Moderate bootstrap rework; no runtime change.
3. **Runtime-generate the scatter from the seed at Awake** — smallest repo, but it contradicts the project's hard-won editor-vs-runtime serialization doctrine (shipped-absent bug class) and changes the test surface. Only with an explicit, Sponsor-acked exception + scene-presence tests rewritten around a bootstrap marker. Not recommended now.
Recommend 1 now, 2 when the next big world expansion lands.

### G2 — Sea mesh is unwelded for no visual effect: 153,600 verts where ~26K would render identically — **MED payoff, S-M effort, LOW risk**
**Now:** `BuildIslandWater` emits every triangle with 3 fresh verts + per-face normals ("UNWELDED FLAT-SHADED facets", `LowPolyZoneGen.cs:1571-1648`). But the grid is **flat** (all y=0, `:1551`), so every per-face normal is exactly +Y — identical to welded shared-vert normals. The swell displaces verts in the *vertex shader* keyed off **world position** (`LowPolyWater.shader:135-144`), so coincident duplicated verts displace identically and normals are never recomputed — the unweld changes nothing on screen. It only: triples vertex count (forces UInt32), triples the scene-file/water memory (~6MB of the 14MB scene, est. from 153,600 × ~40B/vert), and slows the emit loop.
**Fix:** emit the sea as an indexed welded grid (161×161 = 25,921 verts, UInt16) with normals all +Y; keep the pond-hole tri filter (drop indices, not verts) and the winding fix (emit CCW-from-above). Gate on `-seaDiag`/capture byte-comparison — expected identical frames.
**Payoff:** −~5-6MB scene, faster load/regen; zero frame-rate change (GPU didn't care either way). **Risk:** low — the existing sea capture gates catch any regression. (If a future ticket wants *actual* per-face shading on a displaced sea, that's what the shader's `_FLATSHADING_ON` ddx/ddy path is for — runtime-true facets without unwelding, `LowPolyVertexColor.shader:232-236`.)

### G3 — Generated meshes stay CPU-readable — free memory on the table — **LOW payoff, S effort, LOW risk**
**Now:** no generator calls `UploadMeshData(true)`; all ~1,600 meshes keep a CPU copy (Unity default `isReadable=true` for script-created meshes).
**Why it matters:** est. 10-20MB RAM duplicated. Desktop has headroom — this is hygiene, not a bottleneck.
**Fix:** `mesh.UploadMeshData(markNoLongerReadable: true)` at the end of the scatter/cloud/vista/water builders. **Exclude** the terrain mesh (its MeshCollider + editor-time raycast grounding path reads it; scene-serialized collider cooking is safest left alone) and any mesh a runtime system reads. Verify chop/fell (which manipulates transforms, not mesh data — safe) and the verify-capture suite.

### G4 — Editor-time generation cost is healthy; the NavMesh bake is the one big line item — **no action, watch it**
The mesh/scatter emit loops are allocation-churny (per-mesh `List<>`s) but editor-time only and small. The whole-island NavMesh bake pins `voxelSize = 0.16` over a 330×330u collect-All surface (`WorldBootstrap.cs:634-643`) — that voxel density over that area is the bootstrap's dominant cost and grows quadratically with island size. CI's `unity` job is ~6 min healthy today (per `unity-conventions.md` #101 note), so fine — but if bootstrap time starts hurting the single build slot, the first lever is voxel 0.16 → 0.20-0.25 with the coverage trace (`TraceNavMeshCoverage`, `:671-704`) as the regression gate (it already prints walkable %).

### G5 — Committed-vs-code drift check: **clean today.** `LowPolyWaterMat.mat` on disk matches `MakeWaterMaterial` code values (WaveAmp 0.45/Len 11/Speed 1.1/FogCap 0.5/FoamAmount 1 — file vs `LowPolyZoneGen.cs:1933-1952`). The regeneration-required pattern is documented and test-guarded elsewhere; nothing stale found in `Assets/Settings` in this pass. (`GradientSky.mat` dated Jun 17 predates the sun-disk PR — on main it should have been re-baked by #194's bootstrap run; verify on a main checkout, not this branch.)

### G6 — Structural: generator quality-iteration ergonomics — **LOW-MED payoff, S-M effort**
Pragmatic improvements that make the *next* look-tune cheaper, no behavior change:
1. **One shadow/static policy helper:** `MakeMeshObject` (`LowPolyZoneGen.cs:1343`) is the single chokepoint for scatter renderers — extend it with `(castShadows, staticFlags)` args and the R2/R3 fixes become one-line-per-callsite, self-documenting.
2. **Central scene-stats trace:** the generators already log counts piecemeal (`[world-trace]` lines). Add one summary line at end of `BuildEnvironment`: total renderers, total verts, casters, materials — so every bootstrap log carries the perf inventory and a regression (e.g. tree target 320 → 800) is visible in CI diff, not discovered in a soak.
3. **Config object over constants:** the island/scatter knobs are ~40 `public const` fields spread across `LowPolyZoneGen` — fine for tests (compile-time pins), but the *scatter counts* (`treeTarget 320` etc., `:932, 962, 988, 1013, 1053, 1093`) are exactly the values a perf pass tunes; hoisting just those six into one struct (still compile-time) with the summary trace above makes A/B density tests one edit. Low priority; do it opportunistically.

---

## 5. Ranked Top-10 (perf/tech angle, quick wins first)

| # | Action | Payoff | Effort | Risk |
|---|---|---|---|---|
| 1 | **R1** Turn off the unused Opaque Texture copy (`BootstrapProject.cs:188` → false; fix the pinning test) | Med-High | S | Low |
| 2 | **R2a** Stop grass/stick/pebble/berry shadow casting (~530 casters out of all cascades; visual no-op) | High | S-M | Low |
| 3 | **C1** Gate the shipped jump-trace `Debug.Log` behind a `-jumpTrace` flag | Med (hitches) | S | Low |
| 4 | **C2a** `useGUILayout=false` on all 10 OnGUI components + cache BootHud/SurvivalHud strings (event-driven rebuild) | Low-Med | S | Low |
| 5 | **R5** Unpin URP/Lit + URP/Unlit from AlwaysIncludedShaders (keep the 4 FarHorizon shaders) — CI build time + player size | Med (build) | S | Low-Med |
| 6 | **G2** Weld the sea grid: 153,600 → 25,921 verts, −~5-6MB Boot.unity, byte-identical frames | Med (load/repo) | S-M | Low |
| 7 | **R3-1** `BatchingStatic` on non-animated scatter (rocks/grass/sticks/stones/bushes/berries; NOT trees/clouds) | Med | M | Low-Med |
| 8 | **S2a** Add a `-development` build path to `FarHorizonBuilder` and take the first real Profiler capture of the shipped exe — turns every "verify" above into data | Med (enabler) | S | Low |
| 9 | **R2b** Soak-gated: shadowDistance 220→~130, cascades 4→3 (respect the 86caayvfz flicker history; separate PR + Sponsor soak) | High | M | Med |
| 10 | **G1/G3** Scene-weight program: `UploadMeshData(true)` on scatter meshes now; split generated meshes into standalone assets at the next world expansion | Med (structural) | M-L | Med |

Deliberately **not** on the list: Forward+/GRD flip (R6 — zero win until many-lights; schedule with the night milestone), Linear color space (S1 — look-locked; document instead), LODs/occlusion/texture work (don't matter at this scene scale), quality-tier split (R7 — when a second machine matters).

*Estimates are code-derived; per `unity6-mastery.md §4`, confirm #2/#9's shadow share and #1's copy cost with the Profiler + Frame Debugger on a development `FarHorizon.exe` before/after (Profile Analyzer compare).*
