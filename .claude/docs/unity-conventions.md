# Unity Conventions — hard-won findings (eval spike + bootstrap harvest)

Findings proven empirically during the Embergrave engine-eval spike (`c:/Trunk/PRIVATE/EmbergraveUnitySlice`, iter1–8, RandomGame tickets `86ca7y46c` / `86ca7zhyk` / `86ca7zkyr`) and the Far Horizon bootstrap (U1 `86ca86fb7`, root commit `3a6ef5c`). Each cost at least one debugging round to learn — read before touching the relevant surface.

## Headless / CLI rituals

- **Runtime captures must run WINDOWED, not `-batchmode`.** `-batchmode` produces no real rendered frames; screenshot evidence comes from launching the built exe windowed (`-screen-fullscreen 0`) with an in-game capture component.
- **Editor automation pattern:** `Unity.exe -batchmode -quit -projectPath <p> -executeMethod <Class.Method> -logFile <log>` for scene/asset work; `-runTests -testPlatform EditMode|PlayMode -testResults <xml>` for tests (NO `-quit` with `-runTests`). Always grep the XML's `<test-run ... result= total= passed= failed=` line — exit code alone lies on some failure classes.
- **Headless PlayMode time trap:** `Time.deltaTime ≈ 0` per frame in headless runs — never assert on per-frame deltas; sample over a real `Time.time` window instead.
- **Build verification:** the builder must exit non-zero on failure and log `result=Succeeded size=<bytes>`; grep that line, never trust silence.
- **Build-stamp ritual:** HUD shows `BUILD <tag> | <UTC> | <sha>`; every soak/judgment verifies the stamp first (three-builds-in-play identity confusion is a proven failure mode).

## Editor-vs-runtime divergence (the #1 trap class)

- **Awake-built procedural hierarchies don't serialize.** A hierarchy assembled in `Awake()` passes editor/EditMode checks but can ship MANGLED in the player (spike iter6 "legs pointing upwards" incident). Anything that must exist in the build is built editor-time (executeMethod) and saved into the scene/prefab; runtime code only animates what's serialized.
- **EditMode tests can't see Awake-only structure** — same root cause; give procedural builders an editor-time entry point and test THAT.
- **Shipped-build capture gate exists because of this class:** editor evidence is necessary, never sufficient — final evidence comes from the built exe.

## Build stripping & shaders

- **Custom shaders must be registered in `AlwaysIncludedShaders`** (Project Settings → Graphics) or the build strips them (spike: vertex-color terrain shader rendered editor-only until registered).
- **URP/Lit ignores vertex color.** Vertex-colored lit geometry (low-poly terrain ramps) needs a custom URP shader.
- **URP package emits `Terrain Standard 4 Layers URP` shader-dependency warnings on first import** — package-internal noise, not project errors; CI "zero console errors" gates must allowlist them (U1 bootstrap finding).

## NavMesh

- **NavMesh must be SAVED as an asset and assigned** — bake-in-memory works in the editor but ships a dead click-to-move (no surface) in the standalone player.
- **The voxelizer auto-bridges small coplanar gaps**, so synthetic test scenes can't reproduce island defects — connectivity regression guards must run against the REAL scene.
- **Flat connector strips won't stitch to sloped low-poly meshes** beyond `agentClimb` — pin the terrain's seam-edge vertex columns to the connector height in the mating band.

## FBX / rigs / characters

- **`avatarSetup` T-pose trap:** imported humanoids need the avatar configured from the FBX's own T-pose or retargeted animation mangles limbs.
- **Quaternius Animated-Men FBX clip names carry the `HumanArmature|` prefix** — exact-name clip lookups silently match zero clips (T-pose-mid-walk symptom); use `.Contains` matching plus a `looped < expected` error guard.
- **Normalize intrinsic import height on EVERY character-mesh swap** — meshes vary wildly (UAL mannequin ~1.0u vs Animated-Men ~4.96u); measure bounds and normalize `globalScale` to ~1u before any size system applies, or the camera/shadow/size calibration breaks.
- **Recolor must enumerate ALL materials** — the Animated-Men mesh has six (Shirt/Skin/Pants/Eyes/Socks/Hair); a 4-slot assumption silently erased the face (iter7→iter8 finding).

## Low-poly mesh patterns

- **Welded verts + `RecalculateNormals` = the smooth-shaded look** (averaged vertex normals over coarse facets). Import equivalents: Normals=Calculate with smoothing angle ~60°, not 0°.
- **Thin double-sided foliage: never reuse verts with opposite winding** — coincident opposite-facing triangles average their normals to ~zero → near-black "shards" (iter8 grass bug). Use distinct verts per face AND **up-bias the normals on both faces** (foliage reads as overhead-lit, not as a solid). A normal-distribution probe (count verts with `N·L < threshold` against the key light) is the load-bearing verification — the first plausible fix still shaded dark.
- **Per-instance color-jittered materials cause asset churn** — quantize to a coarse palette and keep materials inline (not `CreateAsset`) so they serialize into the scene.

## Process notes

- `.gitignore` ignores `*.log` / `test-results*.xml` / `Captures/` / `Build/` — CI artifact uploads must run before cleanup; add any new throwaway-dir convention there.
- Empty Assets dirs survive only via their `.meta` files — don't "clean up" metas of empty folders.
- The spike (`EmbergraveUnitySlice`) is the working reference for the M-U1 ports — read its `FINDINGS.txt` + sources; never modify it.
