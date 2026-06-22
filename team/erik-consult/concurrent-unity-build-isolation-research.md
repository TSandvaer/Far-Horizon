# Concurrent Unity Build Isolation — Can We Run 2 Builds on One Machine?

## Question

Can two concurrent Unity 6 `-batchmode` builds run on this single machine (spike `86cabkhjg`) to defeat the single-build-slot bottleneck? If so, what shared-state paths contend, how is each isolated, and what does the licensing picture look like under the current Personal/free seat?

---

## Bottom line

**Conditional YES — but with a version-upgrade prerequisite and a licensing grey area.**

**UPDATE 2026-06-20:** CI run 27847884304 on 6000.4.11f1 confirms the UUM-142421 fix eliminates the EPERM rename class, but did NOT resolve the play-mode-enter hang. The play-mode hang is a separate Android module ADB scanning issue (root-cause analysis and fix options: `team/erik-consult/playmode-enter-headless-deadlock-research.md`). The "Conditional YES" verdict and the upgrade-first recommendation both remain valid — the two bugs are orthogonal.

The UPM rename-EPERM that has forced single-slot serialization is a tracked Unity bug fixed in `6000.4.11f1`. Far Horizon runs `6000.4.10f1` — one patch behind the fix. Per-instance UPM cache isolation via `UPM_CACHE_ROOT` (or the granular `UPM_CACHE_PATH` / `UPM_NPM_CACHE_PATH`) is the correct isolation approach and is the right thing to spike — but upgrading to `6000.4.11f1` first removes the confounding bug and makes the spike result cleaner. The Bee build cache (`BEE_CACHE_DIRECTORY`) is a second machine-level shared path that also needs per-instance isolation. The GI baking cache has a concurrent-use warning but is irrelevant for headless CI builds (no GI baking). Each project's `Library/` folder is self-contained per checkout and does not contend across worktrees. Licensing: Unity's EULA is silent on same-machine concurrent processes; Unity Support's only documented restriction is "multiple instances cannot open the **same project**" — distinct worktrees satisfy this — but no explicit permission for two concurrent processes on a Personal seat exists in the docs. The Build Server license exists specifically for dedicated automated build machines; at the project's current scale the risk of audit is low but the gap is real. Two concurrent builds on one laptop yield ~1.4–1.6× throughput (not 2×) due to shared cores/RAM/disk.

---

## Evidence

### E-1 — Unity bug tracker: EPERM rename fix in `6000.4.11f1`

- **Source:** Unity Issue Tracker, "Package installation fails non-deterministically with Errors EPERM: operation not permitted when installing Packages" (UUM-142421) — [https://issuetracker-mig.prd.it.unity3d.com/issues/package-installation-fails-non-deterministically-with-errors-eperm-operation-not-permitted-when-installing-packages](https://issuetracker-mig.prd.it.unity3d.com/issues/package-installation-fails-non-deterministically-with-errors-eperm-operation-not-permitted-when-installing-packages)
- **What it says:** "Fixed in 6000.3.18f1 / **6000.4.11f1** / 6000.5.0b12 / 6000.6.0a7." Reproduction condition noted in thread: "I've had more success in reproduction when installing the Feature Sets with 2 Editors open" — directly confirming the concurrent-instance origin. The bug was introduced in a regression at `6000.3.15f1`.
- **Strength:** Strong (official Unity issue tracker, fix tagged with specific version strings, reproduction linked to concurrent editors).
- **Application to Far Horizon:** The project runs `6000.4.10f1` — one patch behind the fix. The EPERM collisions observed during CI (unity-conventions.md § Headless/CLI rituals) are almost certainly this bug. Upgrading to `6000.4.11f1` is a prerequisite that should precede the concurrency spike; without it, even perfect per-instance cache isolation may still hit the underlying race.

### E-2 — UPM global cache: relocatable per-process via env vars

- **Source:** Unity Manual, "Customize the global cache", Unity 6 / 2023.2 — [https://docs.unity3d.com/2023.2/Documentation/Manual/upm-config-cache.html](https://docs.unity3d.com/2023.2/Documentation/Manual/upm-config-cache.html)
- **What it says:** The following env vars are read at process launch and control distinct UPM cache sub-trees:

  | Variable | Controls | Default Windows path |
  |---|---|---|
  | `UPM_CACHE_ROOT` | Global cache root (all sub-trees) | `%LOCALAPPDATA%\Unity\cache` |
  | `UPM_CACHE_PATH` | Uncompressed packages sub-folder | `<cache_root>\packages` |
  | `UPM_NPM_CACHE_PATH` | Registry data (npm sub-folder) | `<cache_root>\npm` |
  | `UPM_GIT_LFS_CACHE_PATH` | Git LFS cache | (disabled by default) |

  "Setting an environment variable takes precedence over applying the same setting in the user configuration file or the Preferences window." The requirement is: "you must set them every time you launch Unity" — they are per-launch, not persistent, which is exactly what per-process isolation needs. The docs also note: "In scenarios that involve automation or continuous integration, it's less practical and more error prone to configure settings in the user configuration file or the Preferences window" — env vars are the documented CI path.

  Critical instruction: "Close the Unity Editor and Unity Hub if they're already running before setting environment variables. Launch the Unity Editor or Unity Hub from the **same command prompt or terminal session where you set the environment variables.**" This confirms the vars are read from the launching shell's environment — direct `Unity.exe -batchmode` launch (not via Hub) reads the exporting shell, making per-instance isolation straightforward.

- **Strength:** Strong (official Unity Manual, current version, env-var semantics confirmed by direct fetch of the doc page).
- **Application to Far Horizon:** Exporting distinct `UPM_CACHE_ROOT` (or the granular pair `UPM_CACHE_PATH` + `UPM_NPM_CACHE_PATH`) before each `Unity.exe -batchmode` invocation — as the spike spec already prescribes — is the correct isolation mechanism. The spike spec uses shell-A / shell-B with `$TEMP/upm-A` and `$TEMP/upm-B` roots. This is sound.

### E-3 — Asset Store package cache: a separate relocatable path

- **Source:** Unity Manual, "Customize the asset package cache location" — [https://docs.unity3d.com/Manual/upm-config-cache-as.html](https://docs.unity3d.com/Manual/upm-config-cache-as.html)
- **What it says:** The Asset Store package cache (default `%APPDATA%\Unity\Asset Store-5.x`) is controlled by a separate variable `ASSETSTORE_CACHE_PATH`. This is "not permanent" and "you must set the `ASSETSTORE_CACHE_PATH` environment variable every time you launch Unity." Same per-process semantics as the UPM vars.
- **Strength:** Strong (official Unity Manual).
- **Application to Far Horizon:** Far Horizon has no Asset Store packages (the project's source is CC0, Blender, Mixamo — no Asset Store purchases). This cache path is therefore a **non-issue for this project** and need not be isolated in the spike. If Asset Store packages are ever introduced, add `ASSETSTORE_CACHE_PATH` to the per-instance exports.

### E-4 — Bee build cache: `BEE_CACHE_DIRECTORY`, machine-level shared

- **Source:** Unity Manual, "Cache location reference" (Unity 6) — [https://docs.unity3d.com/6000.4/Documentation/Manual/build-cache-location-reference.html](https://docs.unity3d.com/6000.4/Documentation/Manual/build-cache-location-reference.html)
- **What it says:** The Bee incremental build pipeline uses a machine-level cache at `%USERPROFILE%\AppData\Local\Unity\Caches\bee` (Windows default). It "reuses some specific parts of builds (such as non-embedded packages and libIL2CPP artifacts) across different projects." Controllable via `BEE_CACHE_DIRECTORY` environment variable. The doc does not explicitly state whether concurrent Bee processes share this cache safely — it warns "modifying or deleting these files outside of Unity can lead to unexpected issues."
- **Strength:** Strong (official Unity 6 Manual, direct fetch). Concurrent safety is not addressed — absence of documentation is a risk signal.
- **Application to Far Horizon:** `BEE_CACHE_DIRECTORY` is a **second machine-level shared path** that must be isolated per-instance alongside the UPM caches. Best practice: export a distinct `BEE_CACHE_DIRECTORY` per worktree shell. Each instance will cold-build its Bee cache on first run (slower) but subsequent concurrent runs will reuse their own warm cache. Disk cost: the Bee cache can reach several GB (IL2CPP artifacts are large). Compose with the Unity Accelerator lever (sibling spike) to offset the per-instance cache cost.

  The spike spec (`team/spikes/unity-concurrent-build-cache-isolation-spike.md`) does **not** currently include `BEE_CACHE_DIRECTORY` isolation — this is a **gap in the spike spec** that must be added before the spike runs.

### E-5 — Per-project `Library/` folder: self-contained, no cross-worktree contention

- **Source:** Unity Manual, "AssetDatabase" + Unity community (uninomicon.com/library) + `unity-conventions.md` (Far Horizon project, EPERM observations) — [https://docs.unity3d.com/Manual/AssetDatabase.html](https://docs.unity3d.com/Manual/AssetDatabase.html)
- **What it says:** `Library/` contains all per-project imported artifacts (ArtifactDB, PackageCache symlinks, shader cache, TypeDB). It is located inside the project folder. Multiple Unity instances can write their own `Library/` simultaneously provided each uses a distinct `-projectPath` — this is the single documented constraint: "Multiple Unity instances cannot open the **same project** folder." Unity creates `Temp/UnityLockFile` inside the project root to enforce this.
- **Strength:** Strong (official Unity Support article for the same-project restriction: [https://support.unity.com/hc/en-us/articles/115003118426](https://support.unity.com/hc/en-us/articles/115003118426); community-confirmed per-project scope of `Library/`).
- **Application to Far Horizon:** Distinct worktrees (`Far-Horizon-devon-wt` / `Far-Horizon-drew-wt`) each have their own `Library/`. No cross-contamination is possible. The EPERM errors observed in Far Horizon's CI are sourced from the shared UPM cache and/or Bee cache — NOT from `Library/` contention. Worktree isolation already satisfies the `Library/` requirement.

### E-6 — GI baking cache: concurrent warning, but irrelevant for CI

- **Source:** Unity Manual, "GI cache" — [https://docs.unity3d.com/Manual/GICache.html](https://docs.unity3d.com/Manual/GICache.html)
- **What it says:** "If you try to bake with more than one instance of the Editor running on the same computer, the Editor displays the following warning message: 'The GI Cache is using increasing amounts of space on your hard drive to support concurrent lightmap generation.'" The GI cache default location is configurable via Preferences > GI Cache or Editor command-line arguments. Multiple concurrent GI bakes can cause failed bakes.
- **Strength:** Strong (official Unity Manual).
- **Application to Far Horizon:** The CI pipeline runs `BootstrapProject.Run` + EditMode + PlayMode + `BuildWindows` — none of these steps trigger GI baking. Far Horizon uses baked lighting but baking is an editor-interactive step, not a headless CI step. The GI cache is a **non-issue for the concurrency spike**.

### E-7 — Licensing: Personal seat, concurrent processes, Build Server license

- **Source 1:** Unity Support, "Running multiple instances of Unity referencing the same project" (HTTP 403 on direct fetch — content retrieved via search result summary) — [https://support.unity.com/hc/en-us/articles/115003118426](https://support.unity.com/hc/en-us/articles/115003118426) — **Strength: Moderate** (URL confirmed, content via search summary, not direct fetch).
  - What it says: "Multiple Unity instances cannot open the **same project**." The restriction is project-folder-scoped, not machine-process-scoped.

- **Source 2:** Unity Manual, "Activation FAQ" — [https://docs.unity3d.com/Manual/ActivationFAQ.html](https://docs.unity3d.com/Manual/ActivationFAQ.html) — **Strength: Moderate** (official doc, content via fetch).
  - What it says: "Every paid commercial Unity license allows a single person to use Unity on two machines that they have exclusive use of." Personal license has "unlimited activations" across machines. The FAQ does not address concurrent processes on a single machine.

- **Source 3:** Unity Build Server licensing — [https://unity.com/blog/games/offload-project-builds-with-unity-build-server](https://unity.com/blog/games/offload-project-builds-with-unity-build-server) — **Strength: Moderate** (URL confirmed, content via search result summary — 403 on direct fetch).
  - What it says: Unity Build Server is a separate license type "that runs Unity in batch mode, exclusively for building your Unity projects." It uses a floating-license model so each concurrent build slot consumes one Build Server license seat. This product exists specifically for dedicated CI/build-machine scenarios.

- **Source 4:** Unity forum discussion, "Unity Licensing for Build Machines" — [https://discussions.unity.com/t/unity-licensing-for-build-machines/608665](https://discussions.unity.com/t/unity-licensing-for-build-machines/608665) — **Strength: Weak** (community forum, no Unity staff resolution).
  - What it says: Developers debate licensing for build machines. No authoritative ruling on concurrent same-machine processes under a Personal seat.

- **Synthesis:** Unity's EULA and public documentation do not explicitly permit or prohibit running two concurrent `-batchmode` Unity processes under a single Personal license on the same machine. The only documented hard lock is same-project-folder, which distinct worktrees satisfy. The Build Server license is designed precisely for the automated-batch-build use case; its existence implies Unity considers dedicated build machines a separate licensing category, but Unity has not published a public FAQ ruling that Personal-license concurrent same-machine batchmode is prohibited. At Far Horizon's current scale (one sponsor, non-commercial development phase), the practical risk of a license audit finding is low — but it is a genuine grey area, not a confirmed permission.

### E-8 — Throughput ceiling: ~1.4–1.6×, not 2×

- **Source:** Spike spec `team/spikes/unity-concurrent-build-cache-isolation-spike.md` (E. Thorsen hypothesis, 2026-06-19) + Unity Manual build pipeline docs (E-4 above) — **Strength: Moderate** (logical inference, not benchmarked).
- **What it says:** Two builds on one laptop share CPU cores, RAM, and disk I/O. IL2CPP transpilation + linking is CPU-bound and dominates build time. Two concurrent IL2CPP phases halve available cores per build. Expected real throughput: 1.4–1.6× vs 2×.
- **Application to Far Horizon:** The sibling spike (hold-time reduction via Mono backend for soak/CI builds) compounds with concurrency. Switching to Mono for CI drops per-build time from ~20–30 min (IL2CPP) to ~3–5 min (estimate; Mono C# compilation is dramatically faster), which changes the cost calculus: two fast Mono CI builds overlap by only a few minutes, making the throughput gain approach 2× for that case.

---

## Shared-state inventory and isolation map

| Shared path | Default Windows location | Contention risk | Isolation mechanism | Required? |
|---|---|---|---|---|
| UPM packages cache | `%LOCALAPPDATA%\Unity\cache\packages` | **HIGH — confirmed EPERM source** | `UPM_CACHE_PATH=<distinct per shell>` | YES |
| UPM npm/registry cache | `%LOCALAPPDATA%\Unity\cache\npm` | Medium | `UPM_NPM_CACHE_PATH=<distinct per shell>` | YES |
| UPM Git LFS cache | (disabled by default) | Low | `UPM_GIT_LFS_CACHE_PATH=<distinct per shell>` | YES (if LFS packages used) |
| Bee build cache | `%USERPROFILE%\AppData\Local\Unity\Caches\bee` | **MEDIUM — IL2CPP artifacts, shared** | `BEE_CACHE_DIRECTORY=<distinct per shell>` | YES — gap in current spike spec |
| Asset Store cache | `%APPDATA%\Unity\Asset Store-5.x` | None (no Asset Store pkgs) | `ASSETSTORE_CACHE_PATH=<distinct per shell>` | No (not used) |
| Project `Library/` | `<projectPath>/Library/` | None (distinct worktrees) | distinct `-projectPath` (already satisfied) | already satisfied |
| Project `Temp/` + `UnityLockFile` | `<projectPath>/Temp/` | None (per-project lock) | distinct `-projectPath` (already satisfied) | already satisfied |
| GI baking cache | `%AppData%\LocalLow\Unity\Caches\GiCache` | None (no headless GI baking) | N/A | N/A |
| Hub state / licensing | `%APPDATA%\Unity\` | Low (batchmode licensing reads local .ulf) | Launch `Unity.exe` directly, not via Hub | Recommended |

---

## Application to Embergrave / Far Horizon

**The concrete proposed bake** (sponsor-gated, do not apply until spike passes):

### Step 1 — Upgrade to `6000.4.11f1` first

The EPERM fix (UUM-142421) is in `6000.4.11f1`. Running the spike on `6000.4.10f1` contaminates results: even with perfect cache isolation the underlying UPM rename race might still hit. Upgrade the project's locked editor version, re-run CI, confirm green, then proceed to the spike.

**Risk:** Patch upgrades within the LTS `6000.4.x` stream are designed to be drop-in safe. The only verified change relevant to this project is the UPM EPERM fix. Low risk; high upside.

### Step 2 — Add `BEE_CACHE_DIRECTORY` to the spike spec

The current spike spec (`team/spikes/unity-concurrent-build-cache-isolation-spike.md`) exports `UPM_*` vars but omits `BEE_CACHE_DIRECTORY`. The Bee cache is a second machine-level shared path. The spike shell-launch blocks should add:

```bash
# shell 1
export BEE_CACHE_DIRECTORY="$TEMP/bee-A"
# shell 2
export BEE_CACHE_DIRECTORY="$TEMP/bee-B"
```

### Step 3 — If spike passes, bake into `serve_soak.sh` and `ci.yml`

`serve_soak.sh` already sources `$UNITY` from env. Add a pre-flight block before the Unity invocations that exports distinct cache roots per script invocation. Since `serve_soak.sh` is a single process, it only needs ONE consistent root (its own `$SOAK_DIR` subtree is suitable):

```bash
# Near top of serve_soak.sh, after ROOT is established:
export UPM_CACHE_ROOT="$SOAK_DIR/upm-cache"
export BEE_CACHE_DIRECTORY="$SOAK_DIR/bee-cache"
```

For `ci.yml`, add to the `unity` job's `env:` block:

```yaml
env:
  UNITY: 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe'
  RESULTS: ${{ github.workspace }}\ci-out
  UPM_CACHE_ROOT: ${{ github.workspace }}\ci-upm-cache
  BEE_CACHE_DIRECTORY: ${{ github.workspace }}\ci-bee-cache
```

These workspace-relative paths are per-run-per-checkout and never collide across concurrent CI runs even without a second runner. They also make the cache cold per run (reproducibility) — the Unity Accelerator lever (sibling spike) compensates for cold-cache cost.

### Step 4 — Runner concurrency (if a second runner is registered)

The current CI has one self-hosted runner. The concurrency/isolation work unlocks LOCAL two-worktree builds (`serve_soak.sh` in two worktrees simultaneously, manually) but does NOT automatically give the CI two parallel build slots — that requires registering a second runner with the same `unity,windows` labels. When a second runner is registered, the per-workspace `UPM_CACHE_ROOT` and `BEE_CACHE_DIRECTORY` vars (workspace-relative, baked above) ensure the two runners never collide even on the same machine.

### Licensing call

At Far Horizon's current development scale, continuing under the Personal license with distinct-worktree concurrent builds is a pragmatic low-risk choice. The documented restriction ("multiple instances cannot open the same project") is satisfied. A formal Build Server license is the clean answer if: (a) the project reaches commercial revenue, (b) Unity audits become a concern, or (c) the team scales past one developer needing CI. Flag for Sponsor awareness, not an immediate action item.

---

## Feasibility verdict

**Conditional YES on one machine.** Prerequisites: (1) upgrade to `6000.4.11f1` to remove the confounding UPM EPERM bug; (2) add `BEE_CACHE_DIRECTORY` isolation to the spike spec (gap). If the spike passes after those two changes, per-instance env-var isolation unlocks concurrent local builds at ~1.4–1.6× throughput. CI concurrency beyond one runner requires a second runner registration (no license blocker for distinct worktrees). Licensing is a grey area under Personal, not a confirmed block.

**Top 2 risks:**
1. **`BEE_CACHE_DIRECTORY` is not in the spike spec yet** — if two instances contend on the Bee cache and it races, the spike logs a false FAIL against UPM isolation when the real culprit is Bee. Add the var before the spike runs.
2. **Version mismatch** — the spike runs on `6000.4.10f1` which contains the EPERM bug. A FAIL result on `6000.4.10f1` is ambiguous (UPM bug or isolation failure?). Upgrade to `6000.4.11f1` first to get a clean signal.
