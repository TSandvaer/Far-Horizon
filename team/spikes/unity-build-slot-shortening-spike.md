# Spike — shorten the Unity build-slot HOLD time (Accelerator + warm-Library + backend)

**Status:** proposed (compounding companion to the concurrency spike)
**Owner suggestion:** Devon (build/CI surface) or orchestrator R&D-lane
**Timebox:** measure = 1 hr; each lever = ~1 hr. Run measurement first, then only the lever the data points at.
**Tracker:** relates to the away-queue "harden CI" item + autonomy-tuning-plan item H.

---

## Why this is the OTHER half of the bottleneck

The slot pain is `hold_time × contention`. The sibling spike
(`unity-concurrent-build-cache-isolation-spike.md`) attacks **contention** (run two at once).
This one attacks **hold_time** — and it pays off on **every** build whether or not concurrency
ever lands, *and* it softens the concurrency spike's per-instance cache disk cost. Do both; they
compose.

## Step 0 — SAFETY / coordination

Same as the concurrency spike: this launches Unity and contends for the live build slot. Confirm
no in-flight CI (`gh run list`), no resident local Unity/exe, and have the orchestrator hold
Unity-slot dispatch for the window.

## Step 1 — MEASURE FIRST (do not skip; it picks which lever to pull)

Run one clean `serve_soak.sh` and read one CI `unity` run's logs. Attribute wall-clock to phases:

| Phase | Where it shows | Helped by |
|---|---|---|
| Asset import / bootstrap | `bootstrap.log` import lines | **Accelerator** (Lever A) + warm-Library (B) |
| Script compile | editmode/build log compile lines | warm-Library (B) |
| **Windows build (IL2CPP link)** | `build.log` IL2CPP phase | **NOT** Accelerator — see Lever C |
| Capture | `capture_gate.sh` | n/a |

**The whole point of measuring:** the Unity Accelerator only speeds **asset import**. If the
`BuildWindows` IL2CPP link dominates the hold time, Lever A barely helps and **Lever C is the real
win.** Pull the lever the data names, not the one that's fashionable.

## Lever A — local Unity Accelerator (import cache)

A free local cache server (Docker or standalone) at `localhost:10080`. Each worktree's project
points at it via the CacheServer fields **already present** in `ProjectSettings/EditorSettings.asset`
(currently `m_CacheServerMode: 0` = disabled; `m_CacheServerEndpoint` empty; the download/upload
flags exist). Flip mode on + set the endpoint. Effect: cold imports in a fresh worktree/CI checkout
pull warm artifacts from the Accelerator instead of re-importing — and it **dedupes import
artifacts across worktrees**, which directly offsets the concurrency spike's per-instance cache
disk cost.

⚠ `m_CacheServerMode` is committed under `ProjectSettings/` → flipping it on affects **every**
worktree on checkout. Decide project-wide-on vs per-machine (per-machine = leave the asset at 0 and
override via the editor pref / `-cacheServerEndpoint` CLI arg) before committing.

## Lever B — warm-Library reuse (`clean: false`)

CI's `actions/checkout` can wipe the workspace each run; warm-Library reuse keeps the imported
`Library/` across runs so import isn't repeated. **STATE.md flagged poisoned-warm-Library as a
MEDIUM risk** — a stale/corrupt Library yields false-green or false-red. Mitigation: with the
Accelerator (A) in place a *clean* checkout re-imports FAST, so warm-reuse becomes **less necessary
and lower-risk** — prefer A over B where they overlap. If keeping `clean: false`, add a periodic
forced-clean cadence + a poisoned-Library smoke check.

## Lever C — Mono scripting backend for SOAK/CI builds (the IL2CPP-dominates contingency)

CI.md implies the Windows build uses the **IL2CPP** module. IL2CPP transpile+link is the slowest
build phase by far; **Mono** compiles dramatically faster. Soak/iteration builds exist to judge
*look & feel*, not shipping perf — they likely don't NEED IL2CPP. If Step 1 shows IL2CPP dominates
the hold time, the highest-ROI lever is: **build soak/CI with the Mono backend, reserve IL2CPP for
actual release builds.**

⚠ Verify first: (a) the current backend (`PlayerSettings` / build log), (b) that nothing in the
soak path depends on IL2CPP-only behavior. Caveat: Mono vs IL2CPP can differ in rare runtime edge
cases, so any FINAL release build must still be IL2CPP-verified — this lever is for the fast
iteration loop only, not the ship build.

## Success metric

Quantified against the Step-1 baseline, e.g. "cold `serve_soak` slot-hold drops from `<X>`s to
`<Y>`s." Capture the before/after phase breakdown as evidence.

## Sequencing with the concurrency spike

Independent — can run before, after, or interleaved. Natural order: **measure (Step 1) once,
shared by both spikes** → pull the hold-time lever the data names here → run the concurrency spike.
The two compose: concurrency lets two builds run at once; this makes each cheaper and makes
concurrency's disk cost bearable.

## Out of scope

- The per-instance UPM cache-isolation / concurrency work (the sibling spike).
- 2nd physical machine / VM.
- The orphan-run-holds-runner concurrency-group fix (`86caammpq`).
- Deep IL2CPP/code-gen optimization beyond the backend swap.
