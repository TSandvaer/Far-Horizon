# Unity Conventions — hard-won findings (eval spike + bootstrap harvest)

Findings proven empirically during the Embergrave engine-eval spike (`c:/Trunk/PRIVATE/EmbergraveUnitySlice`, iter1–8, RandomGame tickets `86ca7y46c` / `86ca7zhyk` / `86ca7zkyr`) and the Far Horizon bootstrap (U1 `86ca86fb7`, root commit `3a6ef5c`). Each cost at least one debugging round to learn — read before touching the relevant surface.

## Headless / CLI rituals

- **Runtime captures must run WINDOWED, not `-batchmode`.** `-batchmode` produces no real rendered frames; screenshot evidence comes from launching the built exe windowed (`-screen-fullscreen 0`) with an in-game capture component.
- **Editor automation pattern:** `Unity.exe -batchmode -quit -projectPath <p> -executeMethod <Class.Method> -logFile <log>` for scene/asset work; `-runTests -testPlatform EditMode|PlayMode -testResults <xml>` for tests (NO `-quit` with `-runTests`). Always grep the XML's `<test-run ... result= total= passed= failed=` line — exit code alone lies on some failure classes.
- **Headless PlayMode time trap:** `Time.deltaTime ≈ 0` per frame in headless runs — never assert on per-frame deltas; sample over a real `Time.time` window instead.
- **Build verification:** the builder must exit non-zero on failure and log `result=Succeeded size=<bytes>`; grep that line, never trust silence.
- **Build-stamp ritual:** HUD shows `BUILD <tag> | <UTC> | <sha>`; every soak/judgment verifies the stamp first (three-builds-in-play identity confusion is a proven failure mode).
- **Stale-stamp trap (the soak handoff):** `FarHorizonBuilder.BuildWindows` does NOT regenerate `Assets/Resources/BuildStamp.txt` — ONLY `BootstrapProject.Run` (`WriteBuildStamp`) writes it, and the stamp is COMMITTED, so it is ALWAYS stale vs a later HEAD. A soak that runs a bare `BuildWindows` ships the old committed sha → build-identity ambiguity (bit the first manual desktop soak, 2026-06-12). **Never serve a soak with a bare build.** Use the one entry point `.github/workflows/scripts/serve_soak.sh` (ticket 86ca86gde): from a CLEAN checkout it chains bootstrap (fresh-stamps HEAD) → BuildWindows → `verify_build_stamp.py` (fails loud unless the shipped stamp's sha == HEAD) → `capture_gate.sh` (reuses U7's frame gate) → restores the bootstrap's tracked-asset churn (`git checkout -- Assets ProjectSettings`; the clean-tree precondition makes whole-root restore complete + drift-proof — bootstrap's churn set drifts as it grows, so an enumerated path list goes stale). Prints the exe path + HUD stamp + capture dir handoff block. Regression-guarded by `verify_build_stamp.py` + the stale-vs-HEAD case in `tests/scripts/test_gate_scripts.sh`.
- **Capture-evidence freshness: cited == attached == HEAD.** Recurring failure class (3× on 2026-06-12: PR #10 stale-stamp PNGs, PR #13 capture-not-attached): a Self-Test Report CITES a capture/stamp, but the artifact on disk is from an older sha, or lives only in gitignored `ci-out/`/`Captures/` where the reviewer can't see it. The gate rests on report text alone — exactly what the capture gate exists to prevent. Convention: (1) the capture's HUD stamp must read the PR HEAD sha at citation time; (2) include the verify-run's exit-code log line verbatim in the PR; (3) image attachment to PRs is structurally IMPOSSIBLE from agents (gitignore + structure_check.sh gate committed captures; gist upload classifier-denied for private-repo screenshots; gh has no comment-attachment API) — the canonical verification is the REVIEWER INDEPENDENTLY RE-RUNNING the verify gate (serve_soak.sh / -verifyChop class) and judging their own artifact (PR #13 precedent, 2026-06-12). Reviewers: always check cited-stamp == artifact-stamp == HEAD before trusting any visual claim. Same class, CI flavor (4th hit, PR #14): "CI green" claims must cite the actual run ID on the PR head — local EditMode/PlayMode/serve_soak results are NOT CI; a PR body asserting CI-green while the head's only run is red fails the evidence gate even when the code is approve-quality. Self-hosted-runner note (CONFIRMED 2× 2026-06-12, runs 27433014608 + PR #15 first attempt): concurrent Unity instances collide on PackageCache (`EPERM rename` in bootstrap) → environmental CI red. Policy: SERIALIZE CI-triggering pushes (don't push two PRs back-to-back while the runner is busy); on a hit, one re-run — it clears when the lock holder exits. Unity incremental builds can leave the old exe mtime unchanged even on a successful rebuild. Always grep the build log for `result=Succeeded` (plus `size=<bytes>`); use the `BurstDebugInformation_DoNotShip/` folder mtime as a secondary freshness check when the log is unavailable.
- **`BootstrapProject.Run` dirties tracked assets.** Running bootstrap dirtied Boot.unity, several materials, GraphicsSettings.asset, and BuildStamp.txt in observed runs. Always follow bootstrap with `git checkout -- Assets/ ProjectSettings/` before committing or opening PRs — bootstrap churn must not ship in unrelated work.
- **Binary-scene PR conflicts: regenerate-on-rebase, never hand-merge.** `Boot.unity` is binary AND bootstrap-generated — when two PRs both bake into it, the second is "not mergeable" and git cannot resolve it. Validated fix (PR #10 after PR #11 moved main, 2026-06-12): (1) `git rebase origin/main`, resolving CODE conflicts normally but taking `--theirs` provisionally on Boot.unity/BuildStamp.txt; (2) re-run `BootstrapProject.Run` headless — the regenerated scene bakes BOTH branches' contributions because both live in the bootstrap code path; (3) prove the carry with EVERY feature's scene-presence EditMode test green (e.g. `WarmthNeedSceneTests` + `CastawayCharacterTests` together); (4) fresh `serve_soak.sh` so captures/stamp match the new HEAD; (5) `git push --force-with-lease`, keeping the committed set surgical (Boot.unity + BuildStamp.txt only). Reviewer content-APPROVEs survive this — it's mechanical integration. Sequencing corollary: every scene-touching PR conflicts with every other; merge them tightly rather than letting approved PRs queue.

## Asset creation — Blender + Blender MCP (Sponsor-flagged capability, 2026-06-12)

The Sponsor runs Blender locally with the **Blender MCP** connected to orchestrator sessions
(`mcp__blender__*` tools; sub-agents load them via ToolSearch). This is the first-choice route for
CREATING low-poly objects, not just importing CC0 packs:

- **Procedural low-poly modeling:** `execute_blender_code` scripts meshes directly — ideal for the
  faceted/flat-shaded style (props, rocks, trees, campfire variants); `get_viewport_screenshot` for
  iteration; export FBX via scripted exporter into `Assets/Art/`.
- **Asset sources:** PolyHaven (CC0 download tools) + Sketchfab search/import; Hyper3D/Hunyuan3D
  text- or image-to-3D generation for rough bases to retopo/stylize.
- **Fit to pipeline:** exported FBX goes through the SAME editor-time import/serialization rituals
  as the Quaternius castaway (importer config + bake into scene/prefab; all trap classes apply).
- **Named use cases so far:** the cartoonish-castaway proportion edit (`86ca8ca1m` — edit the
  existing rigged mesh rather than sourcing a new one), future world props (the campfire visual
  bar, Zone-D trees/landmarks per the art board).

## Editor-vs-runtime divergence (the #1 trap class)

- **Awake-built procedural hierarchies don't serialize.** A hierarchy assembled in `Awake()` passes editor/EditMode checks but can ship MANGLED in the player (spike iter6 "legs pointing upwards" incident). Anything that must exist in the build is built editor-time (executeMethod) and saved into the scene/prefab; runtime code only animates what's serialized.
- **EditMode tests can't see Awake-only structure** — same root cause; give procedural builders an editor-time entry point and test THAT.
- **Shipped-build capture gate exists because of this class:** editor evidence is necessary, never sufficient — final evidence comes from the built exe.
- **`VolumeProfile.Add<T>()` components aren't serialized into the profile asset.** Post-processing overrides added via editor code (`profile.Add<Bloom>()` etc.) live only in memory — without `AssetDatabase.AddObjectToAsset(component, profile)` per component plus a save, the profile ships EMPTY and the whole post stack silently goes missing in the build (symptom: bloom/grading/vignette present in the editor, absent in the exe only). Regression-guarded by asserting the saved profile's component count (U5, Far Horizon PR #4).
- **Component-in-source-but-not-serialized-into-scene** is a named failure class alongside Awake-no-serialize: a MonoBehaviour can be committed, compile clean, and pass all script tests while the scene simply never carries it — the feature ships silently inert (U7, PR #6 `a3edf04`: `CaptureGate.cs` existed in source but Boot.unity had no CaptureGate component, so the capture gate would have produced zero frames — the exact silent failure it exists to catch). Regression-guard pattern: an EditMode scene-presence assert that loads the scene and requires the component (`CaptureGateSceneTests`); binary scenes can't be GUID-grepped, so the EditMode test is the only authoritative reader. Any "this component must exist in the shipped scene" contract needs one.

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
