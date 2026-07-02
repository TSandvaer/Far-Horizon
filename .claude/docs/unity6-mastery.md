# Unity 6 / URP Mastery — Far Horizon Daily-Use Guardrails

**MANDATORY pre-work read for all Far Horizon Unity work.**
This is the concise decision-forcing checklist. Full citations and depth at `team/erik-consult/unity6-mastery-research.md`.

---

## 1. Rendering Path — Set This First, Everything Else Depends on It

- **Universal Renderer → Rendering Path = Forward+.** Required for GPU Resident Drawer and GPU Occlusion Culling. Removes per-object light cap (campfires/torches have no artificial limit). Loss of Reflection Probe Blending is acceptable for the stylized low-poly look.
  - **Deliberate deviation (do NOT "fix"):** the shipped config runs plain **Forward + GPU Resident Drawer OFF** — at 1 shadowed directional + 1 unshadowed point light Forward+ buys nothing today, and GRD can't instance the world's unique per-instance meshes. Flip both at the **night / torches / many-campfires milestone** (the trigger where clustered lights + GPU-occlusion actually pay). See `team/analysis/2026-07-01-poly-style/consolidated.md` §4 T-H / §5 Tier-3 R6.
- **Colour space = Gamma is a deliberate LOOK-LOCK (do NOT "fix" to Linear).** Technically wrong for the HDR + bloom + tonemap stack, but every palette constant was soaked against gamma output; a casual flip re-opens the whole colour bar. Revisit ONLY in an explicit look-overhaul milestone (zero desktop perf impact either way). See `team/analysis/2026-07-01-poly-style/consolidated.md` §1 item 5 / §4 T-H.
- **Render Graph Compatibility Mode = OFF** in `Project Settings > Graphics > URP > Render Graph`. Compatibility Mode is a migration crutch, not a shipping state. GPU Occlusion Culling requires it OFF.
- Any custom Renderer Feature (Zone-D fog/bloom pass, stylized water) MUST use the **Render Graph two-stage authoring model** (record → execute; system owns resource lifetime). Do NOT allocate/dispose RTs manually inside custom passes.

---

## 2. Draw-Call Batching — Priority Order for URP

| Do | Do NOT |
|---|---|
| Enable **SRP Batcher** (default ON; confirm not disabled) | Use Static Batching — incompatible with GPU Resident Drawer |
| Enable **GPU Resident Drawer = Instanced Drawing** in the URP Asset | Enable GPU Instancing checkbox per-material when GPU Resident Drawer is on (redundant; adds shader variants) |
| Set `BatchRendererGroup Variants = Keep All` in Project Settings > Graphics | Use Dynamic Batching — deprecated |
| Keep a small number of **shader types**; proliferate material instances freely | Proliferate unique shaders (SRP Batcher batches by shader variant, not by material count) |

**GPU Resident Drawer disqualifiers:** MaterialPropertyBlocks on MeshRenderer; `sortingLayerID`/`sortingOrder` set; >128 materials per GO; `OnWillRenderObject`/`OnBecameVisible`/`OnBecameInvisible` callbacks; Realtime Enlighten GI; Light Probe Proxy Volumes. Keep world props as plain MeshRenderers without these to stay in the instanced path.

**LOD + GPU Resident Drawer:** Distance-based LOD switching only — cross-fade animated transitions fall back. Acceptable for low-poly.

---

## 3. Lighting — Budget for a Static Outdoor World

- **Bake everything static** with the Unity 6 GPU Lightmapper (now production-ready, dramatically faster than CPU lightmapper).
- **Single directional light (sun)** + APV for GI. Keep additional lights few; avoid real-time shadows on secondary lights.
- **NEVER use a shadowed point light for campfire glow** — each one costs 6 shadow-map render passes. Use an unshadowed point light or baked/emissive.
- **Disable shadow casting** per-MeshRenderer on small props and distant foliage that do not need it.
- **APV (Adaptive Probe Volumes):** volume-based GI; no hand-placed probe grids. Good fit for the big open island with warm gradient lighting. Measure cost before committing to APV in a live scene.
- **Fog** (URP Volume + camera far-clip-plane): hides the draw-distance edge and contributes to the "world feels BIG" feel. Set the far-clip-plane aggressively; open only as needed.
- **Skybox / Background-pass shaders do NOT receive the main light.** `GetMainLight()` / `_MainLightPosition` is UNBOUND in the URP skybox (Background) pass — empirically confirmed in the shipped IL2CPP exe (2026-06-29): a sun-disk term reading `GetMainLight()` rendered nothing. To use the sun direction in a skybox shader (sun disk, horizon tint), **bake it into a material/global property** (e.g. `_SunDirection`) from the real directional light at bootstrap (update it if the sun rotates). Also: in a skybox shader compute the view ray in WORLD space (object-space is wrong there), and LERP toward the sun core rather than pure-additive (additive clips the disk to white). (Far Horizon's sky sun-disk, PR #194: `GradientSkybox.shader` + `QualityPassGen.ResolveSunDirection`.)

---

## 4. Performance — Profile Before Optimizing

**The rule:** determine CPU-bound vs GPU-bound first. Profile on a **development build of `FarHorizon.exe`**, not the editor. Editor profiler numbers do not match the built player.

**The tools (in use order):**
1. **Profiler Highlights module** (Unity 6) — first-stop bottleneck identification.
2. **CPU Usage module > GC.Alloc column** — per-frame allocation check.
3. **Frame Debugger** — verify GPU Resident Drawer merged draw calls; check overdraw.
4. **Profile Analyzer Compare** — validate before/after of any optimization change.
5. **Memory Profiler v1.1** (Unity 6) — resident memory + graphics breakdown.

**GPU Occlusion Culling:** A/B test on the built exe (dense jungle/mountain scenes are a strong candidate; low-poly low-vertex geometry is the weaker case). Never assume a win — measure.

**STP Upscaling** (`URP Asset > Quality > Upscaling Filter > STP`): GPU-headroom lever if ever GPU-bound at native resolution. Test against the low-poly look (upscalers can soften hard polygon edges).

**World-scale data point:** a big-island POC (~800u, PR #226, merged 2026-07-02) held 60fps with a single scaled mesh + STATIC batching — see `elite-techniques.md` § "Procedural terrain at scale" for the full figures and a reconciliation flag against §2's Static-Batching/GPU-Resident-Drawer incompatibility before this is carried into production world-gen.

---

## 5. Scripting — Rules That Apply to Every PR

**Garbage collection (GC.Alloc in hot paths = reject in review):**
- Cache `GetComponent<T>()` in `Awake`/`Start`; never call per-frame.
- Cache `Camera.main`; never call per-frame (tag search).
- No per-frame `new List<>()` / `new []` / LINQ / string concatenation / boxing in `Update`.
- Use `UnityEngine.Pool.ObjectPool<T>` (built-in) for any frequently spawned/despawned object.
- No `Debug.Log` in Update/FixedUpdate/LateUpdate. Strip all logging from shipping builds.

**Lifecycle order (get this wrong = null refs and jitter):**
- `Awake` → self-init, cache own components.
- `Start` → cross-object refs, register to events.
- `Update` → input, per-frame logic. Multiply by `Time.deltaTime`.
- `FixedUpdate` → Rigidbody/physics forces only.
- `LateUpdate` → **orbit camera follow MUST go here.**

**Fundamentals:**
- File name MUST match class name or the component shows "Missing".
- Prefer `[SerializeField] private` over `public` for Inspector fields. Inspector-serialized value overrides the code initializer at runtime.
- Never lerp Euler angles across the 0/360 wrap. Use `Quaternion.Slerp`.
- Multiply ALL per-frame movement/rotation by `Time.deltaTime`.
- Enter Play Mode Options: enable for iteration speed. **Audit all statics** — they don't reset on domain reload unless you add a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset.

---

## 6. Architecture — ScriptableObjects and Patterns

**Survival-loop data:** all tuning config (need decay rates, craft recipes, item stats, world-gen params) lives in **ScriptableObject assets**, NOT in MonoBehaviour fields or serialized JSON.

**Cross-system events:** use **SO event channels** (a ScriptableObject sits between publisher and subscriber). Decouples inventory ↔ HUD ↔ crafting with no hard references. Avoids singleton spaghetti.

**Design patterns for the survival loop** (see "Level Up Your Code with Design Patterns and SOLID" Unity 6 sample):
- **Object pooling** (spawned props, particles, resource drops) — `UnityEngine.Pool.ObjectPool<T>`
- **State machine** (player: idle/move/chop; campfire: unlit/lit/burning-out)
- **Command** (click-to-move instruction queue)
- **Observer** (need/inventory events — prefer SO event channels)

**Avoid:** global static singletons; per-frame `Find`/`FindObjectOfType`; `SendMessage`/`BroadcastMessage`.

---

## 7. Version Control — Scene and Prefab Safety

- **Force Text serialization** (`Edit > Project Settings > Editor > Asset Serialization > Mode = Force Text`) and **Visible Meta Files** (`Version Control > Mode = Visible Meta Files`). Both already implied by the project's `.meta` discipline.
- **UnityYAMLMerge:** wire into git for semantic scene/prefab merging when multiple personas edit the same file. Tool: `C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Data\Tools\UnityYAMLMerge.exe`. Far Horizon headlessly regenerates `Boot.unity` (sidesteps the merge) — but hand-edited prefabs need this.
- **`Boot.unity` must be index 0** in Scenes-In-Build (the entry point for the built player).
- **A local EditMode failure on tests that read `AssetImporter`/`AssetDatabase` import state can be a STALE `Library/` artifact, NOT a real regression.** After a branch switch Unity may serve a stale import (FBX/`.meta` not reimported), so import-state assertions fail locally while the PR's CLEAN CI re-imports → green. If a local run shows EditMode fails on import-reading tests (e.g. `ChopAnimatorControllerTests` reading `Melee_Attack.fbx` import state), do NOT conclude a `main` regression — clear `Library/` (or reimport the asset) and trust the PR's CI EditMode count over the local run. (2026-06-29: a local 743/746 was a stale-Library artifact; #195's clean CI = 746/746, all `ChopAnimatorControllerTests` green.)

---

## 8. Textures and Meshes — Import Rules

| Asset type | Rule | Reason |
|---|---|---|
| All static textures | Read/Write = OFF | ON doubles memory (CPU + GPU copy) |
| 3D world textures (distance-varying) | Mip Maps = ON | Prevents aliasing on distant surfaces |
| UI / constant-size sprites | Mip Maps = OFF | Wastes memory for textures viewed at fixed size |
| Texture dimensions | Power-of-two | Required for GPU compression to work |
| Desktop compression | BC7/BC1 (DXT) | ASTC is mobile/XR only |
| Texture Max Size | Smallest visually acceptable | Cuts bandwidth; diffuse 1024, roughness/metallic 512 |
| Mesh import: Optimize Mesh Data | ON, but verify normals survive | Normals are load-bearing for the low-poly smooth-faceted look |
| Colliders on interactable props | Primitive (sphere/capsule/box) preferred | Mesh Colliders are accurate but expensive |
| Low-poly faceted meshes | Expect more vertices than triangle count | Hard-edge normal splits add vertices; keep meshes genuinely low-poly |

---

## 9. UI Toolkit (When UI Work Is Needed)

**Use UI Toolkit for any new runtime UI.** No package install needed in Unity 6.

**Core setup:**
- `UIDocument` component → Panel Settings (`Scale With Screen Size` + Reference Resolution) → Source UXML.
- Separate Panel Settings for HUD vs menus if they need different scaling.

**Authoring rules:**
- All styling in **USS selectors with BEM names** (`block__element--modifier`). No inline styles.
- Keep USS selectors specific and shallow — broad selectors (`*`, `.unity-*`) are expensive on large trees.
- Animate **transforms** (`translate`/`scale`/`rotate`), NOT layout properties (`width`/`height`). Add `UsageHints.DynamicTransform` on animated elements.

**Show/hide cost (cheapest to most expensive):**
1. `style.display = DisplayStyle.None` — no layout, no render (use for frequent toggles).
2. `RemoveFromHierarchy` — no memory (use for rarely-shown dialogs).
3. `Visibility.Hidden` — still in layout (use only when space must be preserved).
4. `opacity = 0` — **NEVER for hiding** (everything still runs; pure cost with no benefit).

**Data binding (Unity 6):**
- Mark bindable properties `[CreateProperty]` (compile-time; no reflection cost).
- `ToTarget` for read-only HUD readouts; `TwoWay` for settings inputs.
- Leave UXML `data-source` unresolved for panels that re-point at runtime; assign `element.dataSource = x` in C#.

**Performance numbers:**
- ≤8 textures per batch (UI Toolkit's uber shader limit). Atlas icons via Sprite Atlas (static content) + dynamic atlas (runtime inventory).
- Raise Vertex Budget in Panel Settings if Frame Debugger shows many UI draw calls.
- Max 7 nested rounded-corner masks (stencil-based); prefer rectangular masks.

---

## 10. Build — Windows Desktop Only

- **Scripting backend = IL2CPP** for the Windows player.
  - **Deliberate deviation (do NOT "fix"):** the shipped config is **Mono, not IL2CPP** — runtime scripts are light so the CPU win is small, while IL2CPP ~doubles build time on the single (scarce) CI runner. Adopt trigger = **a second CI lane / nightly lane** exists to absorb the build-time cost. See `team/analysis/2026-07-01-poly-style/consolidated.md` §4 T-H / §5 Tier-3 S2b.
- **Enable IL2CPP C# source line numbers** in Player Settings — required for meaningful crash call stacks from the shipped `FarHorizon.exe`.
- All `Debug.Log` calls stripped from release builds (`[Conditional("DEVELOPMENT_BUILD")]` or disable logger in build pipeline).
- `Build/`, `Captures/`, `*.log`, `test-results*.xml` are gitignored — **CI must upload these artifacts before cleanup.**
- Use **Build Profiles** (Unity 6) to manage the Windows config; the deprecated Build Settings window is no longer the canonical path.
- The standalone Resolution Dialog was removed in Unity 2019.1 — not present in Unity 6 builds.

---

## Quick Reference: Critical Don'ts

| If you're tempted to... | Do this instead |
|---|---|
| Enable Static Batching | Disable it; rely on GPU Resident Drawer |
| Add a shadowed point light for the campfire | Use unshadowed point + baked emissive |
| Use `OnGUI()` for runtime UI | Use UI Toolkit (or uGUI if touching existing) |
| Use the built-in Standard shader | Use URP Lit / Simple Lit / Unlit or Shader Graph |
| Call `GetComponent` in `Update` | Cache in `Awake`; reuse the field |
| Allocate a `new List<>()` in `Update` | Declare as a field; call `.Clear()` each frame |
| Use `Debug.Log` in a hot path | Remove or gate behind `#if DEVELOPMENT_BUILD` |
| Animate `width`/`height` in UI | Animate `translate`/`scale` + set `DynamicTransform` hint |
| Hide a UI element with `opacity = 0` | Use `style.display = DisplayStyle.None` |
| Run ForceInput Euler-lerp across 360° | Use `Quaternion.Slerp` |
| Profile in the editor and trust the numbers | Profile on a development build of `FarHorizon.exe` |

---

---

## 11. Input System — WASD + Jump + Sprint (Locomotion Milestone)

Full evidence at `team/erik-consult/unity6-mastery-research.md § GF-5`. This section is the daily-use summary; use the research note for the full step-by-step setup guide.

**Install:** `Window > Package Manager > Unity Registry > Input System`. Disable the legacy Input Manager when prompted.

**Asset:** `Assets > Create > Input Actions` → `FarHorizonInputActions.inputactions`. Open it; create an action map `Player` with three actions:

| Action | Type | Binding |
|---|---|---|
| `Move` | Value / Vector2 | 2D Vector Composite: W=up `<Keyboard>/w`, S=down `/s`, A=left `/a`, D=right `/d`. Also arrow keys + `<Gamepad>/leftStick` |
| `Jump` | Button | `<Keyboard>/space` + `<Gamepad>/buttonSouth` |
| `Sprint` | Button | `<Keyboard>/leftShift` + `<Gamepad>/buttonEast` |

Enable **Generate C# Class** in the asset's Inspector → Apply. This produces `FarHorizonInputActions.cs` with typed accessors (no string lookups).

**Direct polling (recommended for single-player):**
```csharp
private FarHorizonInputActions _input;
void Awake() { _input = new FarHorizonInputActions(); _input.Player.Enable(); }
void OnDisable() => _input.Player.Disable();
void Update()
{
    Vector2 move    = _input.Player.Move.ReadValue<Vector2>();
    bool    jumping = _input.Player.Jump.IsPressed();
    bool    sprint  = _input.Player.Sprint.IsPressed();
}
```

**`PlayerInput` component (if preferred):** Add to player GameObject; assign the asset; set Notification = `Invoke C# Events`; wire `OnMove(InputAction.CallbackContext)`, `OnJump(...)`, `OnSprint(...)` on the same MonoBehaviour. Access actions through the component's copy, not `InputSystem.actions`, to preserve future multiplayer device filtering.

**Do NOT use the old `Input.GetAxis` / `Input.GetButtonDown` path for new code.**

---

## 12. Frame Rate — vSyncCount vs targetFrameRate

**Desktop rule: use `QualitySettings.vSyncCount`, not `Application.targetFrameRate`.**

`vSyncCount` is hardware-based and eliminates microstutter. `targetFrameRate` is software-based and is silently ignored when `vSyncCount != 0`.

```csharp
// Shipped Far Horizon desktop build — set in startup or Quality Settings:
QualitySettings.vSyncCount = 1;   // sync to display refresh (60/144 Hz etc.)
// Do NOT also set targetFrameRate — it is ignored.
```

`vSyncCount = 2` → half the refresh rate (30 fps on 60 Hz). `vSyncCount = 0` → disable vSync; targetFrameRate governs (subject to microstutter).

**GC allocations in hot paths — the short list (see research note GF-1 for full detail):**

| Pattern | No-alloc alternative |
|---|---|
| `new WaitForSeconds(t)` inside a coroutine | Cache as `private static readonly WaitForSeconds s_wait = new WaitForSeconds(t)` |
| LINQ in `Update` / `FixedUpdate` | Pre-compute at `Start`; use `for` loops on cached arrays |
| String `+` / `+=` in hot paths | `StringBuilder`; TextMeshPro `SetText(float)` |
| Boxing: `someInt` as `object` | Generic APIs (`List<T>`, `Dictionary<K,V>`) |
| Lambda capturing outer variable | Named static method/comparer |
| `GetComponents<T>()` per frame | Cache in `Awake`; or use `GetComponents<T>(preAllocList)` |
| `Physics.RaycastAll()` per frame | `Physics.RaycastNonAlloc(ray, results[])` |

---

## Unresolved — Read These Before Starting the Relevant Work

- **URP post-processing / Volume / Renderer Features / Render Graph authoring:** read the URP Advanced Creators e-book (Unity 6, free) at `unity.com/resources/introduction-to-urp-advanced-creators-unity-6` before any custom pass or post-process work.
- **Performance optimization beyond profiling basics:** read the Console/PC Performance e-book (Unity 6, free) at `unity.com/resources/console-pc-game-performance-optimization-unity-6`.
- **NavMesh / pathfinding for click-to-move:** not in this doc; use Unity Manual NavMesh docs directly.
