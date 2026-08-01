# CI: Split Headless Unity Build from Windowed Captures — Spec + Ordering Call for `86cabkhjg`

> **Harvest-time corrections — added 2026-07-30, by the harvest PR, NOT by the author.** This note was
> written without a Bash tool (see the Methods note at the bottom); two of its factual claims were checked
> against ground truth at the moment it was committed. Erik's analysis is otherwise unmodified — nothing
> was rewritten and no findings were added.
>
> 1. **Only ONE runner is registered — there is no runner-2.** This closes Open question 2, and it bears
>    on Q2 and Q3, which reason about a `build` job on runner-2 overlapping `capture` on runner-1 as a
>    live configuration. Measured at harvest time:
>    `gh api repos/TSandvaer/Far-Horizon/actions/runners -q '.total_count'` → `1`, and the sole runner is
>    `far-horizon-local | status=online | labels=self-hosted,Windows,X64,unity,capture`. Because that one
>    runner carries BOTH `unity` and `capture`, every Unity job on `main` today — `build`, `capture`,
>    `playmode` — necessarily lands on it. So Q2's residual gap ("nothing stops `build` from landing ON
>    the capture-labeled runner") is not a scheduling *risk* today; it is the only available schedule. And
>    Q3's concurrency win ("`capture` for run A overlapping `build` for run B") currently has no second
>    machine to be realized on. Erik's conditional reasoning is left intact — it goes live the day a
>    second runner is registered.
> 2. **`runs-on:` line numbers corrected, +2 each,** in the Evidence section below: `build` is at
>    `ci.yml:209` (not 207) and `capture` at `ci.yml:487` (not 485). 207 and 485 are the *job-key* lines;
>    each `runs-on:` sits two lines below its key. Verified at harvest time via
>    `git show origin/main:.github/workflows/ci.yml`, which returns
>    `207:  build:` / `209:    runs-on: [self-hosted, windows, unity]` /
>    `485:  capture:` / `487:    runs-on: [self-hosted, windows, unity, capture]`. The other two cited
>    numbers (`structure:` 133, `playmode:` 1231) are job-key lines and are correct as written.

## Question

`86cabkhjg` (Unity concurrent-build cache-isolation spike) was just cleared for implementation. The
board treats a CI split — headless build+EditMode in one job, windowed shipped-build captures in
another, pinned to one runner — as its unspecced hard prerequisite. Priya/the orchestrator need: (1)
confirmation the constraint is real and current, (2) the split's concrete shape, (3) its wall-clock
cost, (4) whether the dependency is actually hard, (5) a mechanically-obeyable pinning rule so no
future CI edit re-breaks captures.

## Bottom line

**The split shipped.** PR #203 ("ci: split unity job into build (2nd-runner-safe) + capture
(runner-1-pinned) (86cafz9tg)") merged to `main` 2026-07-01T08:26:57Z as commit `d5c3e7d8d9defdd73b3c649
f706895f102ee261b`. `origin/main`'s `.github/workflows/ci.yml` today has four jobs — `structure`
(ubuntu-latest), `build` (`runs-on: [self-hosted, windows, unity]`), `capture` (`runs-on: [self-hosted,
windows, unity, capture]`), and `playmode` — not the single bundled `unity` job. ClickUp `86cafz9tg`'s
"complete" status and the `single-unity-build-slot-serializes-orchestration` memory's PR #203/`d5c3e7d`
narrative are both accurate. (An earlier draft of this note asserted the opposite — see the Methods
note at the bottom for how that error entered and why it should not recur.)

**`86cabkhjg` is still NOT hard-dependent on the split, and that verdict now stands on firmer ground.**
The spike tests per-process `UPM_CACHE_PATH`/`BEE_CACHE_DIRECTORY` env-var isolation for two concurrent
Unity processes in **local worktrees** — nothing in its method or success test touches CI runner labels
or capture jobs, split or unsplit. **Dispatch it now.**

What the split actually buys, and what it doesn't yet prove, is the substance of the corrected Q2 below:
the label scheme mechanically guarantees windowed captures never land off the one runner proven clean,
but it does **not** by itself resolve Q1's still-unresolved mechanism, and CLAUDE.md's own capacity
line (`"≤1 Unity-build ticket in flight"`) has evidently not yet been revised upward even though its
stated prerequisite (the split) landed four weeks ago — that gap is worth surfacing, not assuming away.

## Evidence

- **`gh pr view 203`, relayed by the orchestrator 2026-07-30** — Strong (primary source: GitHub PR
  metadata, ground truth). PR #203, title "ci: split unity job into build (2nd-runner-safe) + capture
  (runner-1-pinned) (86cafz9tg)", state MERGED, merged 2026-07-01T08:26:57Z, merge commit
  `d5c3e7d8d9defdd73b3c649f706895f102ee261b`.
- **`git cat-file -t d5c3e7d`, relayed by the orchestrator** — Strong. Object type `commit`; it exists
  in the repo's object store. (My earlier claim that this SHA had "no trace in the repo" was not backed
  by running this or any equivalent check — I had no Bash tool and did not flag that gap loudly enough
  before asserting non-existence. That is the core error; see Methods note.)
- **`git show origin/main:.github/workflows/ci.yml`, relayed by the orchestrator** — Strong (the actual
  file content on `main`, fetched fresh). Confirms four jobs: `structure:` (ubuntu-latest, line 133),
  `build:` `runs-on: [self-hosted, windows, unity]` (line 209 — corrected at harvest from 207, see the
  corrections block at the top), `capture:` `runs-on: [self-hosted,
  windows, unity, capture]` (line 487 — corrected at harvest from 485), `playmode:` (line 1231). I have the job names, `runs-on` label
  sets, and line numbers, relayed secondhand — I do not have the full step-by-step body of `build` and
  `capture` (artifact upload/download mechanics, timeouts, whether `build` carries any label beyond
  plain `unity`). Flagged as an open gap below, not filled in from guesswork.
- **`.github/workflows/ci.yml` as checked out in `Far-Horizon-erik-wt`** — this worktree is pinned to
  commit `363c1a0` (branch `erik/86cae6d1p`, an action-verb-anim ticket), which sits well behind `main`
  and predates the split. The single-bundled-`unity`-job shape I read there is real, but it is a stale
  snapshot, not "the repo" — see Methods note. I am retaining this bullet only to document what was
  actually read, not as evidence about current `main`.
- **ClickUp `86cafz9tg`** ("ci: split headless build+EditMode from windowed-captures") — status
  **complete**. Given PR #203's confirmed merge, this status is accurate; my earlier framing of it as
  "contradicted by the repo" was backwards.
- **Memory `single-unity-build-slot-serializes-orchestration`** — its 2026-06-28/06-29 A/B-methodology
  entries were already corroborated by CLAUDE.md and `86cafz9tg`'s own ticket body and stand as Moderate
  evidence. Its 2026-07-01/07-02 entries (PR #203, `d5c3e7d`, canary-green, cap raised to 2) are now
  corroborated on the PR-merge fact specifically (Strong). I have **not** independently confirmed the
  "canary-green 2026-07-02" claim or that the orchestration cap was actually operationally raised to 2
  afterward — CLAUDE.md's still-current `"≤1 Unity-build ticket in flight"` line is in tension with
  "cap raised to 2," so that specific sub-claim stays Moderate/unconfirmed rather than assumed true.
- **Ticket `86cabkhjg`** (fetched fresh via ClickUp MCP this session) — status **to do**, not started.
  Method and success test make zero reference to CI job shape, runner labels, or captures — Strong
  evidence it is scoped independently of the split, split-landed or not.
- **I still have no Bash tool in this dispatch.** Every fact above that required `git`/`gh` was relayed
  by the orchestrator, not independently run by me. Where I'd normally re-verify, I'm instead naming the
  gap explicitly rather than either re-asserting a guess or silently trusting secondhand relay as if I'd
  run it myself.

## Q1 — What exactly breaks with a second runner present?

**Still correlation, not mechanism — this has not changed and I am not manufacturing one now that the
split is confirmed real.** Per the memory (moderately corroborated by CLAUDE.md and the ticket): 4/4
capture-gate runs SUCCESS with runner-2 offline; 3/3 flaked ("first-frame present-loop wedge") once
runner-2 came online, on the *same unhardened gate code*. The memory explicitly lists **refuted**
candidate mechanisms — "concurrency, display-lock, zombie-Unity" — without naming what remains. **No
confirmed root cause exists in the project's own record**, before or after the split landed.

Plausible, unverified hypothesis (label it exactly that, same as before): a second Unity process on the
same physical GPU/Windows desktop session — even fully headless (`-batchmode -nographics`) — still
touches the display driver stack (DXGI adapter enumeration, driver context creation), and this may
perturb the first process's present loop on the *same physical adapter* even when the two are otherwise
filesystem/cache-isolated. This would explain why the flake was reproducible regardless of which runner
was doing which role, and why pure concurrency/filesystem causes were refuted. **Not proven** — no
GPU/driver-level trace was captured during the original A/B, and none was captured as part of the split
landing either (per the evidence gaps above). If captures ever flake again with runner-2 fully idle,
that falsifies this hypothesis and points elsewhere (thermal throttling, TDR — see
`gpu-tdr-bsods-nvidia-driver-updated` memory for a precedent on this exact machine).

## Q2 — Assessment of the shipped split: does it achieve 2nd-runner-safety, and what still blocks cap→2?

The shipped shape (confirmed from `origin/main`, not my earlier speculative proposal):

- **`build`** — `runs-on: [self-hosted, windows, unity]` — the **plain, unqualified** label set.
- **`capture`** — `runs-on: [self-hosted, windows, unity, capture]` — the plain set **plus** a
  dedicated `capture` label — `needs: build` (name/line position implies this; I have not seen the
  `needs:` line directly, flagging as inferred-from-structure rather than confirmed).
- **`structure`** runs on `ubuntu-latest` (unrelated to the Unity-runner question).
- **`playmode`** exists as its own job (line 1231); I don't have its `runs-on:` labels from what was
  relayed, so I can't confirm whether it shares `build`'s plain label or was also given a dedicated
  label.

This is what I originally called **Option A** (shared pool for the non-windowed job, a dedicated add-on
label only on the windowed job) — not the **Option B** asymmetric-dedicated scheme I'd recommended in
the pre-correction draft of this note (where `build` would *also* carry its own exclusive label so it
could never land on the capture-pinned runner). The team shipped the simpler option.

**Does it achieve 2nd-runner-safety? Partially, and by construction only on one side.**

- **What IS guaranteed:** `capture` can only ever be scheduled onto a runner carrying the `capture`
  label. If exactly one runner (runner-1) carries that label, windowed capture work is mechanically
  pinned there — it cannot silently drift onto an unproven runner. This is the core safety property Q5's
  pinning rule below asks for, and the shipped labeling satisfies it.
- **What is NOT guaranteed:** `build` requests only the plain `[self-hosted, windows, unity]` set.
  Runner-1 (carrying `unity` + `capture`) is a superset match for that request — GitHub's scheduler is
  free to land a `build` job on runner-1 whenever it's idle, exactly the Option-A risk I flagged before
  I knew the shape. If that happens, a same-runner `capture` job for a different PR would simply queue
  behind it (a scheduling/throughput cost, not a correctness break) — but if `build` running on runner-1
  is itself part of what perturbs the present loop (per Q1's still-unconfirmed mechanism), then a
  `build` job opportunistically landing on the capture-pinned runner **while a capture step for a
  different concurrent PR is also trying to run there** could reproduce the exact flake the split exists
  to prevent. I can't resolve this either way without a real trace of the Q1 mechanism.
- **The harder open question for cap→2 specifically:** the original A/B established that a second Unity
  process being **online anywhere** (not necessarily doing windowed work) correlated with capture
  flakes. The split cleanly answers "can capture be kept off runner-2" — yes. It does **not** by itself
  answer "does a `build` job actively running on runner-2 *while* `capture` runs on runner-1 reproduce
  the flake" — because per Q1, the refuted-mechanism list ruled out plain concurrency/filesystem causes
  but never named what's left, and the GPU-driver-churn hypothesis (if true) predicts exactly this
  cross-runner interaction as a live risk even with clean label separation.

**What still blocks raising the cap to 2, concretely:**

> ⚠ **CORRECTED 2026-07-31 (`86cazhtn1`) — this list is INCOMPLETE and item 1 is out of date. Do not use it
> as the cap→2 checklist.** It omits the thing that actually holds the cap: `.github/workflows/ci.yml:226-228`
> puts every `build` job in an **ABSOLUTE** `concurrency: group: unity-build` (no ref suffix,
> `cancel-in-progress: false`), so all `build` jobs queue repo-wide into ONE lane **regardless of runner
> count or labels** — and `gh api repos/TSandvaer/Far-Horizon/actions/runners` → `total_count: 1`
> (measured 2026-07-31 on `origin/main` @ `721701d`). **BOTH must change; a 2nd runner alone buys nothing.**
> Item 1 is stale — the spike has since run (write-up in PR **#387**), and it found EPERM **absent** in its
> two resolving legs, so the EPERM axis cannot move the cap either. Item 2's "that discrepancy should be
> resolved" **is now resolved**: CLAUDE.md's prose was stale in its stated *mechanism*, and the cap is
> nonetheless still correctly 1 — see the ✅ RESOLVED action item below. Erik's numbered assessment is kept
> verbatim as his 2026-07 reading. ⛔ The cap NUMBER is Sponsor-gated; authoritative statement is `CLAUDE.md`
> § Autonomous orchestration → the **Unity-build cap = 1** bullet.

1. `86cabkhjg` itself hasn't run yet (status: to do) — the local-worktree EPERM question is untested.
2. No canary evidence I can verify shows `build`-on-runner-2 concurrent with `capture`-on-runner-1
   surviving cleanly under load, more than once, over time. The memory's "canary-green 2026-07-02" claim
   may be accurate (its central PR-merge fact now checks out) but I have not independently confirmed
   that specific sub-claim, and CLAUDE.md's still-live `"≤1 Unity-build ticket in flight"` line is
   itself evidence the cap has not actually been operationally raised, split-landed or not — that
   discrepancy should be resolved (ask whether CLAUDE.md is simply stale prose, or whether the cap-raise
   was deliberately held back pending more confidence) before anyone cites "cap should be 2 now" as
   settled.
3. Runner-2's actual live registration status is still open (see Open questions below, carried from the
   prior draft, unchanged).

## Q3 — What the split costs

**For a single PR building alone:** the split makes time-to-verdict *structurally* worse than the old
same-job handoff, not better — an in-job step transition (build finishes, capture step reads the same
local file, zero transfer) becomes a real `actions/upload-artifact` → `actions/download-artifact` round
trip between two jobs. This mechanical claim still holds regardless of the split's landed/unlanded
status and doesn't need re-verification.

**Unlike the pre-correction draft, this is no longer a hypothetical to "measure on the first live run" —
the split has been live on `main` since 2026-07-01, roughly four weeks of real CI history exists.** I
don't have `gh run list`/timing data in this dispatch to pull the real number, so I'm not going to invent
one; a Bash-capable persona or the orchestrator can pull actual `build`→`capture` job-duration deltas
from recent runs in a few minutes and replace the original 1–3-minute IL2CPP-artifact-size estimate with
a measured figure. Do that before citing a wall-clock cost to the Sponsor or Priya.

**Where the split wins:** unchanged from the original analysis — only under 2+ PRs building concurrently,
where `capture` for run A (runner-1) can overlap `build` for run B (runner-2). Still bounded by the
~1.4–1.6× ceiling from `unity-concurrent-build-isolation-research.md` (E-8), not 2×. Given CLAUDE.md's
cap still reads ≤1 (per Q2 above), this benefit is plausibly still unrealized in practice even though
the plumbing to realize it has existed for four weeks.

## Q4 — Ordering: does `86cabkhjg` depend on the split?

**No — this verdict stands, and now rests on a fact-checked foundation instead of a disputed one.**
`86cabkhjg`'s scope (spec + success test, fetched fresh from ClickUp) is entirely about **local
worktree** concurrency: two `BootstrapProject.Run` invocations in two worktrees with isolated
`UPM_CACHE_PATH`/`BEE_CACHE_DIRECTORY` env vars, judged purely on EPERM absence in the two local logs.
Nothing in it touches CI runner labels, job shapes, or windowed captures — whether the split existed,
didn't exist, or (as it turns out) had already existed for four weeks was never actually load-bearing
for this ticket. **Recommendation unchanged: dispatch `86cabkhjg` now, independently.**

The one asymmetric dependency also still holds: if `86cabkhjg` PASSES and gets productionized (baked
into `serve_soak.sh` + `ci.yml`'s build-job env block), *where* that env-var bake-in lands is now
resolved rather than forked — it lands in the confirmed `build` job, not a hypothetical future one.

## Q5 — The capture-pinning rule (mechanically obeyable)

Unchanged in substance from the original draft, and — per Q2 — it matches the spirit of what actually
shipped (capture gated behind a dedicated label), even though the shipped labeling is Option A rather
than the Option B I'd have recommended:

> **Any CI job that launches a Unity process WITHOUT `-batchmode` — i.e. anything that opens a real
> window/swapchain (shipped-exe capture gates, any future `-verify*` visual gate, `serve_soak`-class
> checks if ever ported to CI) — MUST set `runs-on:` to include the `capture` label, which per the
> confirmed `ci.yml` is assigned to the `capture` job alongside `[self-hosted, windows, unity]`. It must
> NEVER use the plain `[self-hosted, windows, unity]` set alone for windowed work, because GitHub will
> freely schedule against any runner matching that subset — including a runner NOT known-safe for
> windowed work.**
>
> **Every other Unity CI job (bootstrap, EditMode, PlayMode, Windows build — anything invoked with
> `-batchmode -nographics`) should use the plain `[self-hosted, windows, unity]` label so it can land on
> whichever runner is idle** — which is exactly what the shipped `build` job does.
>
> **Residual gap this rule does not close (see Q2):** nothing currently stops `build` from landing ON
> the capture-labeled runner when it's idle. If Q1's mechanism turns out to be sensitive to *any* second
> Unity process being active near runner-1 (not just windowed ones), this rule is necessary but not
> sufficient — Option B (an exclusive label on `build` too) would close that gap and should be revisited
> if cap→2 canary testing ever shows cross-runner flakes with `build` scheduled onto runner-1.

## Application to Far Horizon

- **The provenance discrepancy from the earlier draft is resolved — no further action needed there.**
  PR #203 merged 2026-07-01; the split is real and live on `main`. Do not re-open that question.
- **`86cabkhjg` is unblocked today; dispatch it independent of the split's status.**
- **✅ RESOLVED 2026-07-31 (`86cazhtn1`) — Reconcile CLAUDE.md's capacity line against the split's actual
  landing date.** ⚠ **The dichotomy this item posed was FALSE — do not act on it.** It offered "either the
  prose is accurate and needs a clarification, or the cap should have already moved to 2"; the real answer
  is neither. The split DID land (`86cafz9tg`), so CLAUDE.md's stated prerequisite was discharged and its
  prose was wrong — **but the cap is still correctly 1**, because what actually holds it was never the split:
  `.github/workflows/ci.yml:226-228`'s ABSOLUTE `unity-build` concurrency group (`cancel-in-progress: false`,
  no ref suffix → all `build` jobs queue repo-wide, independent of runner count) plus exactly ONE registered
  runner (`gh api repos/TSandvaer/Far-Horizon/actions/runners` → `total_count: 1`, `far-horizon-local`;
  measured 2026-07-31 on `origin/main` @ `721701d`). CLAUDE.md's mechanism text was corrected accordingly;
  **the number did not move and is Sponsor-gated.** Authoritative statement: `CLAUDE.md` § Autonomous
  orchestration → the **Unity-build cap** bullet. Do not re-open this as a cap→2 question.
- **Q5's rule is already close to what shipped; consider hardening to Option B** only if/when cap→2
  canary testing surfaces cross-runner flakes traceable to `build` landing on the capture-pinned runner.
  Don't pre-emptively re-architect the label scheme without that evidence.

## Open questions

1. Real artifact-transfer wall-clock (Q3) — four weeks of real CI history now exists; pull actual
   `build`→`capture` timing deltas instead of estimating.
2. Is runner-2 (`far-horizon-local-2`) currently registered/online, and has it actually been carrying
   `build`/`playmode` load, or is the cap still effectively 1 in practice? `second-runner-setup-steps.md`
   was DRAFT status as of the prior draft — recheck.
3. Was a genuine canary validation ever run and passed for `build`(runner-2)-concurrent-with-`capture`
   (runner-1), and does CLAUDE.md's still-≤1 cap reflect that it was NOT, or just stale prose? This is
   the single most decision-relevant open question for anyone considering raising the cap.
4. Full step bodies of `build` and `capture` (artifact upload/download mechanics, timeouts, `playmode`'s
   actual `runs-on:`) — I only have job names + `runs-on:` label sets + line numbers, relayed secondhand.

## Methods note — how the false alarm entered this note

The prior draft of this note asserted the split "does not exist," that ticket `86cafz9tg` was "falsely
marked complete," and that the memory's PR #203/`d5c3e7d` narrative had "no trace in the repo." All
three claims were wrong, and the mechanism was avoidable: this dispatch's worktree, `Far-Horizon-erik-wt`,
is checked out at commit `363c1a0` on branch `erik/86cae6d1p` (an unrelated action-verb-anim ticket) —
many commits behind `main`, and specifically behind the point where PR #203 merged. Reading
`.github/workflows/ci.yml` from that worktree returns *that worktree's* snapshot, not `main`'s current
state. I have no Bash tool in this dispatch, so I could not run `git fetch`/`git show origin/main:...`
myself to check freshness before asserting non-existence.

The compounding error: I cross-checked the stale `ci.yml` against the erik-wt copy of `CLAUDE.md` and
found them "internally consistent," and treated that agreement as corroboration. It wasn't — both were
stale snapshots of the same worktree, so of course they agreed with each other; agreement between two
stale artifacts is not evidence about current truth. The corroboration I actually needed was against
`origin/main`, which I could not reach without Bash.

**The generalizable rule:** any file read from a persona worktree is a snapshot pinned to whatever commit
that worktree happens to be checked out at, not "the repo" or "main," unless explicitly re-fetched/shown
against `origin/main`. When a claim's truth depends on the CURRENT state of `main` (does X exist, was Y
merged, is Z still true) and I lack the tool access to check that directly, the correct move is to say so
explicitly and ask the orchestrator to run the check — not to read the worktree copy, note it disagrees
with a ticket/memory, and report the disagreement as if the worktree copy were authoritative. "I can't
confirm this from my worktree — can you `git show origin/main:<path>` and relay it?" costs one sentence.
Asserting non-existence from a stale read cost a full correction pass and put a fabrication-adjacent
claim in front of the team.

Doc updates: none — this is the correction pass on this note itself. `unity-conventions.md` §CI
architecture should still gain the Q5 pinning rule (now confirmed to match the shipped shape, with the
Option-B residual-gap caveat) as a follow-up once someone with repo write access wants to make it
official project doc rather than research-note prose.
