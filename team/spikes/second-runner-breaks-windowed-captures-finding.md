# Finding — a 2nd CI runner breaks the windowed-capture gates (A/B-confirmed)

**Status:** ✅ **The A/B RESULT is CONFIRMED (2026-06-29) and still load-bearing** — it is why the `capture`
job is label-pinned to one runner. ⚠ **Two POLICY clauses derived from it are SUPERSEDED as of 2026-07-31
(`86cazhtn1`). Read the box below BEFORE citing this file for why the Unity-build cap is 1, or for what
raising it would require.**
**Ticket:** `86cafza2a` (this evidence record).
**Pairs with:** the CI-split ticket `86cafz9tg` — **complete**, merged as PR #203 (`d5c3e7d`, 2026-07-01) —
and the `single-unity-build-slot-serializes-orchestration` memory.
**Supersedes:** the cap→2 conclusion in `team/erik-consult/second-runner-setup-steps.md` §ACTUAL SETUP — that doc's "build-slot cap bumped 1→2" result was walked back (see below).

---

## ⚠ SUPERSEDED 2026-07-31 (`86cazhtn1`) — which parts of this file are no longer policy

`CLAUDE.md` § *Autonomous orchestration* → the **Unity-build cap = 1** bullet is the **single authoritative
statement** of why the cap is 1, and it points readers HERE for the A/B evidence. So this file must not
contradict it. Two clauses below did. They are marked in place; **do not cite either**:

| Clause | Where | Why it is retired |
|---|---|---|
| "the merge-gate `unity` job **BUNDLES** … so it must run on **ONE runner**" | § Consequence, 1st bullet | ❌ **That job does not exist.** `86cafz9tg` split it into `build` (`ci.yml:223`) + `capture` (`:501`); `grep -cE '^  unity:$' .github/workflows/ci.yml` → `0`. |
| "the cap can rise to 2 once the CI job is **split** … That CI-split work is ticket `86cafz9tg`. Until it lands…" | § The unblock | ❌ **That split SHIPPED 2026-07-01** (PR #203, `d5c3e7d`). The prerequisite is discharged and the cap did **not** follow — because the split was never what held it. Anyone dispatched off that section would be rebuilding merged work. |

**What actually holds the cap at 1** — two independent things, each checkable in one command (measured
2026-07-31 on `origin/main` @ `721701d`; **re-measure before citing, these anchors drift**):

- **(a)** `.github/workflows/ci.yml:226-228` puts every `build` job in an **ABSOLUTE** `concurrency:
  group: unity-build` — no ref suffix, `cancel-in-progress: false` — so all `build` jobs queue repo-wide
  into **ONE lane, regardless of runner count or labels**.
- **(b)** `gh api repos/TSandvaer/Far-Horizon/actions/runners` → **`total_count: 1`** (`far-horizon-local`).

**BOTH must change to lift the cap — a 2nd runner ALONE buys nothing**, because the group would still
serialize the builds.

**⛔ NOT superseded — the A/B result itself** (4/4 clean single-runner vs 3/3 flaked with runner-2 online)
**and everything it implies about captures.** It is why the `capture` job carries the extra `capture` label
(`ci.yml:503`) and its own absolute `unity-capture` group (`:507-509`). **Do not unpin it**, and do not read
this box as clearing the way for runner-2.

⛔ **The cap NUMBER is a Sponsor decision.** Nothing in this box authorises changing it.

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

- ❌ **RETIRED 2026-07-31 — historical, do NOT cite (see the box above):** ~~The merge-gate `unity` job
  **BUNDLES** headless-build + EditMode + windowed-captures into ONE job, so it must run on **ONE
  runner**.~~ `86cafz9tg` split that job into `build` + `capture` on 2026-07-01. **The capture-pinning
  survived the split** — `capture` keeps the extra `capture` label (`ci.yml:503`) — so the A/B result
  still binds *captures*; it is simply no longer what binds *builds*.
- **runner-2 is kept OFFLINE.** (Setup steps remain in `team/erik-consult/second-runner-setup-steps.md`;
  runner-1 path `C:\actions-runner-farhorizon`, runner-2 at `C:\actions-runner-2`.)
- The **Unity-build concurrency cap STAYS ≤1** — still true, ⚠ but **not for the reason this bullet
  originally gave.** What serializes the builds is the absolute `unity-build` concurrency group plus the
  single registered runner (box above), not this capture finding. The non-Unity lane (docs / research /
  spec / review / QA) still fans out; Unity-build tickets are one-at-a-time
  (`single-unity-build-slot-serializes-orchestration` memory; authoritative statement: `CLAUDE.md`
  § Autonomous orchestration → **Unity-build cap = 1**).
- **PR #182** (`chore(ci): 2nd runner verified — bump build-slot cap 1→2 + correct setup note`) bumped
  the cap to 2 on the earlier "2 runners verified" conclusion. That was premature — the windowed-capture
  flake surfaced after it merged.
- **PR #190** (`revert(ci): cap back to ≤1 Unity-build — 2nd runner breaks windowed captures (walks back #182)`)
  reverted the cap to ≤1. **#190 is the live policy.**

## ❌ SUPERSEDED 2026-07-31 — ~~The unblock: what raising the cap requires FIRST~~ (historical)

⚠ **Do NOT dispatch anyone against this section.** The CI-split it names as the prerequisite **shipped**
on 2026-07-01 (`86cafz9tg` / PR #203 / `d5c3e7d`) and the cap did not rise — because the split was never
what held it. The real mechanism is in the box at the top of this file: an **absolute `unity-build`
concurrency group** (`ci.yml:226-228`) plus **one registered runner**, and **both** must change. Retained
below only as the 2026-06-29 reasoning.

The throughput win (2 concurrent Unity builds) is NOT abandoned — it is GATED on a CI restructure.
Headless build + EditMode are 2nd-runner-safe (no window session); only the **windowed captures** are
runner-2-fragile. So the cap can rise to 2 once the CI job is **split**:

- **headless-build + EditMode** → 2nd-runner-safe → can run on either runner.
- **windowed-captures** → 1-runner-pinned → must stay on a single runner.

That CI-split work is ticket **`86cafz9tg`**. Until it lands, the cap stays ≤1 and runner-2 stays
offline. After the split, re-run the A/B (captures pinned to one runner, builds free to parallelize)
to confirm the flake is gone before bumping the cap.

## Out of scope (other tickets)

- The actual CI split (→ `86cafz9tg` — **complete**; PR #203, `d5c3e7d`, 2026-07-01).
- Standing up / re-enabling runner-2 (`86caffc23`, Sponsor-gated; setup doc already written).
- Cache-isolation / build-slot hold-time spikes (`unity-concurrent-build-cache-isolation-spike.md` /
  `unity-build-slot-shortening-spike.md`) — those attack contention + hold-time, a separate axis from
  the windowed-capture fragility documented here.

---

**Evidence source:** orchestrator A/B test 2026-06-29 (4/4 clean single-runner vs 3/3 flaked with
runner-2 online); merged PRs #182 (cap→2) / #190 (revert). `keep-screens-alive` verified ON during the
test.
