# Unity Concurrent-Build Cache Isolation — Defeating the PackageCache EPERM

## Question

The project is limited to one Unity build at a time on the self-hosted runner because two concurrent
Unity invocations on the same machine share `Library/PackageCache` and collide on NTFS atomic-rename
operations (EPERM). Is there a viable isolation technique that would let a second concurrent Unity
build run, removing the single-build-slot bottleneck?

## Bottom line

The EPERM rename bug (UUM-142421) is already fixed in Unity 6000.4.11f1 — the editor the project
upgraded to (ci.yml `env.UNITY` pin). The fix reduces but does not fully eliminate rename collisions
from concurrent builds; two Unity processes sharing the same physical `Library/PackageCache`
directory are still structurally unsafe to run concurrently. The only architecturally sound isolation
route is a **second registered runner with its own checkout workspace** — giving each build an
independent `Library/PackageCache` subdirectory entirely. This is low-cost (copy the runner agent
folder, re-register with `--name far-horizon-2`), uses no extra licensing, and lifts the single-slot
cap. A global `UPM_CACHE_ROOT` redirect per-build can complement this but does not replace it: it
redirects only the *global* UPM registry-data cache, not the per-project `Library/PackageCache`
where the actual EPERM happens.

## Evidence

### Source 1 — Unity Issue Tracker, UUM-142421

- [Unity Issue Tracker — EPERM rename on Windows PackageCache](https://issuetracker-mig.prd.it.unity3d.com/issues/package-installation-fails-non-deterministically-with-errors-eperm-operation-not-permitted-when-installing-packages)
- **Publisher / year:** Unity Technologies, reported 2024, resolved 2024-2025.
- **What it says:** "Fixed intermittent EPERM: operation not permitted, rename errors on Windows
  during package installation." Fix shipped in 6000.4.11f1, 6000.3.18f1, 6000.5.0f1, 6000.6.0a7.
  The bug is a regression introduced between 6000.3.14f1 and 6000.3.15f1; root cause is not
  disclosed in the tracker text, but commenters note it is more frequent with multiple editors open
  simultaneously.
- **Strength: Strong** — official Unity issue tracker, closed/fixed status, lists exact build versions.
- **Caveat:** The fix addresses the *intermittent* single-process rename failure. It does not add
  file-level locking that would safely serialize two concurrent processes' writes to the same
  `Library/PackageCache` directory. The EPERM rename is one failure mode; concurrent write-then-rename
  races on the same target directory remain unsafe by design.

### Source 2 — Unity Manual, "Customize the global cache location" (UPM_CACHE_ROOT)

- [Unity 6 Manual: Customize the global cache](https://docs.unity3d.com/6000.2/Documentation/Manual/upm-config-cache.html)
- **Publisher / year:** Unity Technologies, Unity 6 (6000.x) docs, 2024-2025.
- **What it says:** `UPM_CACHE_ROOT` overrides the global UPM registry-data cache
  (`C:\Users\...\AppData\Local\Unity\cache\upm`). Setting it before the Unity process starts redirects
  that cache to a custom path, enabling per-build isolation of the *global* cache. The docs explicitly
  flag that this must be re-set on every launch and warn that CI use of env vars "may be considered in
  such scenarios" but "is less practical and more error prone than config files."
- **What it does NOT say:** `UPM_CACHE_ROOT` does not redirect `Library/PackageCache`. The
  per-project `Library/PackageCache` is a separate copy that Unity installs from the global cache
  into each project folder. Two builds targeting the *same project folder* would still share the
  same `Library/PackageCache`.
- **Strength: Strong** — official Unity 6 documentation, confirmed by community thread (Discussions
  2024, CodeSmile: "Nothing in the /Library folder can be shared").

### Source 3 — Unity Manual, "Global Cache" (architecture clarification)

- [Unity Manual: Global Cache](https://docs.unity3d.com/Manual/upm-cache.html)
- **Publisher / year:** Unity Technologies, 2024-2025.
- **What it says:** Unity maintains a machine-wide UPM cache at
  `C:\Users\...\AppData\Local\Unity\cache\upm` (Windows). Each project additionally maintains
  `Library/PackageCache` — a *per-project* unpacked copy that is separate from the global cache and
  is not shared across projects. This is the directory where the EPERM rename fires: the resolver
  moves `PackageCache/.tmp-XXXXX/package` → `PackageCache/com.unity.X@version` via atomic rename;
  if a second process concurrently holds an open handle to the destination, the rename fails EPERM.
- **Strength: Strong** — official docs; confirmed by the project's own `bootstrap_with_retry.sh`
  log trace (ticket 86caahtbe).

### Source 4 — GitHub Actions: running multiple self-hosted runners on one machine

- [GitHub Community Discussion #26258: Multiple runners on one host](https://github.com/orgs/community/discussions/26258)
- [GitHub Docs: Self-hosted runners](https://docs.github.com/en/actions/concepts/runners/self-hosted-runners)
- **Publisher / year:** GitHub, 2024-2025.
- **What it says:** Multiple runner instances can be installed on one machine by registering each in
  its own directory with a distinct `--name` and `--work` path. Each runner's `GITHUB_WORKSPACE`
  then points to its own subdirectory. A second runner registered as `far-horizon-2` with
  `--work C:\actions-runner-2\_work` would check out the project to a different path, giving each
  build its own independent `Library/PackageCache`. Both runners can pick up jobs for the same repo
  concurrently because GitHub dispatches by label (`[self-hosted, windows, unity]`) to whichever
  runner is idle.
- **Strength: Strong** — GitHub official docs + community confirmation (multiple productions report
  9+ runners on one machine, ~700-800 MB RAM at idle per runner).

### Source 5 — S. Schöner: "A non-exhaustive list of faults in Unity's Cache Server" (2025)

- [blog.s-schoener.com, 2025-02-26](https://blog.s-schoener.com/2025-02-26-unity-cache-server/)
- **Publisher / year:** Sebastian Schöner, independent engineer, February 2025.
- **What it says:** Unity Accelerator (the import Asset Cache Server) serializes uploads one-at-a-time,
  actively stalling parallel asset imports. Author measured a 4x *slowdown* from Accelerator on their
  workload. Notably, Shader Graph and VFX Graph assets — both used on this project — are not cached
  at all by the Accelerator.
- **Strength: Moderate** — well-sourced independent engineering analysis, reproducible methodology
  described. Single author, not Unity-official. Worth citing as ruling-out evidence.
- **Application:** Rules out Unity Accelerator as a concurrent-build isolation solution. Its
  concurrency story is "upload one at a time," it does not isolate PackageCache, and its documented
  4x slowdown + Shader Graph omission make it net-negative for Far Horizon's pipeline.

### Source 6 — Current project ci.yml + bootstrap_with_retry.sh (primary ground truth)

- `C:\Trunk\PRIVATE\Far-Horizon\.github\workflows\ci.yml` — read this session.
- `C:\Trunk\PRIVATE\Far-Horizon\.github\workflows\scripts\bootstrap_with_retry.sh` — read this session.
- **What it says:** The EPERM is understood in precise detail on this project: two EPERM signatures
  are handled — the rename of a `.tmp-XXXXX` staging dir during package resolution, and the
  UPM-IPC-drop on cold first-run of a new editor. The `clean_packagecache_tmp.sh` pre-clean and the
  3-attempt bootstrap retry already mitigate both. The serialization (`playmode: needs: unity`) is
  the current guard that prevents two concurrent Unity invocations on the same `GITHUB_WORKSPACE`.
- **Strength: Strong** — ground truth from the project itself, empirically proven across runs
  27692339123, 27699769706, etc. (ticket 86caahtbe).

## Application to Far Horizon

### What actually causes the EPERM

There are two distinct collision sources:

1. **Global UPM cache** (`AppData\Local\Unity\cache\upm`, Windows): machine-wide. If two Unity
   processes both try to populate the same package entry here, they can race. `UPM_CACHE_ROOT` can
   redirect this to a per-build path and eliminate this class of collision.

2. **Per-project `Library/PackageCache`**: each project folder's own unpacked copy. The fatal rename
   (`PackageCache/.tmp-XXXXX` → `PackageCache/com.unity.X@version`) happens here. If two CI jobs
   target the same checkout folder (`GITHUB_WORKSPACE`), they share this directory and are structurally
   unsafe to run concurrently regardless of UPM_CACHE_ROOT.

The project currently has a **single `GITHUB_WORKSPACE`** because there is exactly one registered
runner. That runner reuses the workspace (`clean: false`) across runs, so the per-run cache is warm.
This is the root reason only one Unity build can run at a time.

### Recommended approach: register a second runner with its own workspace

Install a second GitHub Actions runner in a distinct directory on the same machine (e.g.
`C:\actions-runner-2\`) with `--name far-horizon-2`, same labels (`self-hosted,windows,unity`), and
a separate `--work` path. After registration, this runner checks the project out at a completely
different absolute path, getting a fully isolated `Library/PackageCache`.

**What this unlocks:** `unity` (EditMode + build + capture) and `playmode` jobs from two different
PRs can execute simultaneously. On a single PR, the `unity` and `playmode` jobs are already
serialized by `needs: unity` — no change needed there. The gain is **cross-PR parallelism**: while
PR A's `unity` job builds on runner-1, PR B's `unity` job can build on runner-2. That is the
bottleneck: only one PR can CI-build at a time today.

**Remaining risk on the same physical machine:** two concurrent Unity processes will both access the
same global UPM cache at `AppData\Local\Unity\cache\upm`. Mitigate by setting `UPM_CACHE_ROOT` to a
per-runner constant path (e.g. `D:\upm-cache-1\` and `D:\upm-cache-2\`) in each runner's
environment so the global caches don't overlap. This is a belt-and-suspenders addition; the primary
isolation is the separate workspace.

**What this does NOT change:**
- `serve_soak.sh` local builds from a persona worktree still race with CI if pushed simultaneously.
  The existing "let local Unity fully exit before pushing" convention still applies.
- The `single-unity-build-slot-serializes-orchestration` memory should be updated to reflect that
  with two runners the cap is 2 concurrent Unity-heavy tickets — not unlimited, because the machine
  has finite RAM/CPU for parallel full builds.

### Ruled-out options

| Option | Why ruled out |
|---|---|
| **Unity Accelerator** | 4x slowdown on parallel imports (Schöner 2025), Shader Graph not cached, adds operational dependency (service must stay up), provides no PackageCache isolation |
| **`UPM_CACHE_ROOT` alone** | Redirects global UPM registry cache only — does not isolate `Library/PackageCache`, which is where the EPERM fires |
| **`-cacheServerEndpoint`** | Cache Server (legacy) is for imported asset hashes (Accelerator successor), not package resolution or PackageCache; does not address the EPERM class |
| **Per-build `Library/` deletion** | Already handled by bootstrap retry; a full Library wipe reverts to cold-build path (20-50 min); defeats the warm-cache advantage `clean: false` currently provides |
| **Symlinks / shared PackageCache** | Documented as unsupported by Unity ("Nothing in /Library can be shared"); concurrent symlinked writes would reproduce the exact EPERM being avoided |
| **Docker / GameCI** | Blocked on this project: no `unityci/editor` image for the 6000.4.x stream with Windows IL2CPP module; requires UNITY_LICENSE secret + billed private-repo minutes (ci.yml route rationale, confirmed) |

### Cost and risk summary

| Approach | Setup effort | Risk | Effect |
|---|---|---|---|
| **Second runner (recommended)** | ~30 min (re-run config script, register service, set labels, add `UPM_CACHE_ROOT` split) | Low — same binary, same license | Doubles Unity-CI throughput; cross-PR parallelism |
| **`UPM_CACHE_ROOT` per-runner** | ~15 min (env var in each runner's `.env` or service config) | Very low | Belt-and-suspenders; prevents global-cache race between two runners |

### Budget note

A second runner requires no additional software purchase, no Unity seat, no cloud service. The only
cost is disk space for a second warm `Library/` (~2-4 GB for this project based on current warm
build size) and RAM overhead for two potentially concurrent Unity processes (Unity headless is
typically 2-4 GB RSS for a mid-size project; the Sponsor's machine has sufficient headroom at the
current project scale). No tooling-budget concern.
