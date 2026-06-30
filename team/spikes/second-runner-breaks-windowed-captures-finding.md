# Finding — a 2nd CI runner breaks the windowed-capture gates (A/B-confirmed)

**Status:** ✅ CONFIRMED 2026-06-29 (A/B test). Binding constraint on Unity-build concurrency.
**Ticket:** `86cafza2a` (this evidence record).
**Pairs with:** the CI-split unblock ticket `86cafz9tg` + the `single-unity-build-slot-serializes-orchestration` memory.
**Supersedes:** the cap→2 conclusion in `team/erik-consult/second-runner-setup-steps.md` §ACTUAL SETUP — that doc's "build-slot cap bumped 1→2" result was walked back (see below).

---

## The finding (observed, A/B-confirmed)

Bringing a **second** self-hosted CI runner online **breaks the windowed-capture gates** — the
shipped-build screenshot-evidence gate that launches the built exe windowed (`-screen-fullscreen 0`)
and captures it. The A/B result:

| Condition | Windowed-capture runs |
|---|---|
| **Single runner** (runner-2 OFFLINE) | **4/4 CLEAN** |
| **runner-2 ONLINE** (2 runners) | **3/3 FLAKED** |

A 7-for-7 separation: every run was clean with one runner and every run flaked with two. The
mechanism is the 2nd runner's concurrent **presence on the machine** disturbing the windowed
render/window session the capture depends on — the windowed captures need an undisturbed GUI/window
session. **This is NOT a code bug in the capture component** — the capture code is unchanged across
both legs of the A/B; only the runner-2 process being online differs.

## Refuted hypotheses (what it is NOT)

The flakes were checked against three plausible causes before landing on "runner-2 presence":

- **Concurrency / job overlap** — refuted. The flakes appeared with runner-2 merely ONLINE, not only
  when a second job was actively building; presence alone is sufficient.
- **Display-lock / screen sleep** — refuted. `keep-screens-alive` was verified ON during the test;
  the display stay-awake mitigation was active in both legs.
- **Zombie / resident Unity process** — refuted. No stray resident Unity/exe held the window session;
  the single-runner leg ran clean on the same machine state.

## Consequence (current policy)

- The merge-gate `unity` job **BUNDLES** headless-build + EditMode + windowed-captures into ONE job,
  so it must run on **ONE runner**.
- **runner-2 is kept OFFLINE.** (Setup steps remain in `team/erik-consult/second-runner-setup-steps.md`;
  runner-1 path `C:\actions-runner-farhorizon`, runner-2 at `C:\actions-runner-2`.)
- The **Unity-build concurrency cap STAYS ≤1.** This is the binding constraint that serializes the
  whole orchestration — the non-Unity lane (docs / research / spec / review / QA) still fans out, but
  Unity-build tickets are one-at-a-time (`single-unity-build-slot-serializes-orchestration` memory).
- **PR #182** (`chore(ci): 2nd runner verified — bump build-slot cap 1→2 + correct setup note`) bumped
  the cap to 2 on the earlier "2 runners verified" conclusion. That was premature — the windowed-capture
  flake surfaced after it merged.
- **PR #190** (`revert(ci): cap back to ≤1 Unity-build — 2nd runner breaks windowed captures (walks back #182)`)
  reverted the cap to ≤1. **#190 is the live policy.**

## The unblock — what raising the cap requires FIRST

The throughput win (2 concurrent Unity builds) is NOT abandoned — it is GATED on a CI restructure.
Headless build + EditMode are 2nd-runner-safe (no window session); only the **windowed captures** are
runner-2-fragile. So the cap can rise to 2 once the CI job is **split**:

- **headless-build + EditMode** → 2nd-runner-safe → can run on either runner.
- **windowed-captures** → 1-runner-pinned → must stay on a single runner.

That CI-split work is ticket **`86cafz9tg`**. Until it lands, the cap stays ≤1 and runner-2 stays
offline. After the split, re-run the A/B (captures pinned to one runner, builds free to parallelize)
to confirm the flake is gone before bumping the cap.

## Out of scope (other tickets)

- The actual CI split (→ `86cafz9tg`).
- Standing up / re-enabling runner-2 (`86caffc23`, Sponsor-gated; setup doc already written).
- Cache-isolation / build-slot hold-time spikes (`unity-concurrent-build-cache-isolation-spike.md` /
  `unity-build-slot-shortening-spike.md`) — those attack contention + hold-time, a separate axis from
  the windowed-capture fragility documented here.

---

**Evidence source:** orchestrator A/B test 2026-06-29 (4/4 clean single-runner vs 3/3 flaked with
runner-2 online); merged PRs #182 (cap→2) / #190 (revert). `keep-screens-alive` verified ON during the
test.
