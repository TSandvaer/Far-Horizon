# Team State — Far Horizon

**This file is a RESUME HEADER, not a log.** One current section. When it is superseded, it is
**replaced**, not appended to — the previous content stays retrievable with
`git log -p team/STATE.md`. It grew to 479 lines of stacked superseded headers before
2026-08-02; do not let that happen again.

Decisions go in `team/DECISIONS.md` (append-only). Operational scrollback goes nowhere.

---

## ▶ NEXT 3 — the ONLY queue the team may pull from (Sponsor-ranked, added 2026-08-18)

**This list is the demand signal. Nothing else is.** No board scan, no priority field, no
"what's dispatchable" sweep may put work into a slot. A ticket enters only when the Sponsor
puts it here.

**When all three slots are empty, the team STOPS and asks.** Idle is the correct state, not a
gap to be filled — the 2026-08-02 doctrine already ruled that an unjustified dispatch is the
bug. The failure this queue exists to prevent is the *other* one: 2026-08-03 → 2026-08-17,
**fifteen consecutive days with zero commits**, because restricting supply left nothing to pull.

| # | Ticket | What | Tier | State |
|---|---|---|---|---|
| 1 | `86cb6v03j` | Held weapons/tools do not POINT in the swing direction — Sponsor: "the single most important issue" | FULL (combat) | **in flight** — PR #439 |
| 2 | `86cb6vjf8` | Boar charge-snap: no NavMesh at runtime → `BoarAI.MoveTowards` transform fallback snaps the boar onto the player's XZ | FULL (enemy AI) | queued, explicitly BEHIND slot 1 |
| 3 | `86cb87tyy` | `capture` greens every gameplay gate against a STALE exe when `build`'s bootstrap fails — the upload step succeeds with no build produced | CI/infra | queued — Sponsor yes 2026-08-21 |

Slots 1–2 are transcribed from the Sponsor decision "Post-#436 priorities: swing direction FIRST,
boar charge-snap ticketed behind it" (`team/DECISIONS.md`, 2026-08-18, merged `8c1b479`). Slot 3
is deliberately blank — filling it is the Sponsor's call, not the orchestrator's.

**Rules for this queue:**
1. **One developer works slot 1.** Slot 2 starts only when slot 1 has an open PR or is merged.
2. **A reviewer is dispatched when a PR EXISTS**, never speculatively, never before.
3. **Finishing a slot does not authorise picking a new one** — report the completion and ask.
4. **Nothing outside this queue gets dispatched** except a bug reproduced in a built exe, which
   may be added to slot 3 with the Sponsor's yes.
5. **Tier comes from `team/TESTING_BAR.md` § Which gates apply** and is named in the PR body.

---

## ⚠ STALE HEADER BELOW — written 2026-08-02, superseded by events on 2026-08-18

Kept only until the next session refreshes it. **Do not act on it without re-verifying:** #436
has since MERGED (`bf833fc`, 2026-08-18) and #438 merged after it (`8c1b479`), so its "FIRST
ACTION" and its open-PR count are both wrong. Agent-liveness and cron state below are unverified
— probe, never assume (`agent-liveness-stop.sh` rule).

## RESUME NEXT-ACTION — 2026-08-02 (Sponsor PRESENT; auto-status OFF and staying off)

**Agents in flight: ZERO. Crons: ZERO. Open PRs: ONE — #436, PARKED ON PURPOSE.**
Session drained and saved 2026-08-02 ~19:55Z at the Sponsor's request.

### ▶ FIRST ACTION NEXT SESSION — check CI on #436, then the runner

**#436** (`86caxjwb3`, enemy hit feedback) sits at **`df5edf7`**, `MERGEABLE`, reviewed and
`APPROVE`d by Tess at the previous head. CI run **`30764256201`** was left **`queued`** because
the sole self-hosted runner **`far-horizon-local` was `offline`** at 19:53Z (API ground truth;
hosted `structure` passed in 21 s, so it was NOT an Actions outage — same class as the ~4.5 h
starvation earlier today).

1. `gh run view 30764256201` — did it ever run?
2. `gh api repos/TSandvaer/Far-Horizon/actions/runners --jq '.runners[].status'` — is it back?
   **`FarHorizon-RunnerKeepAlive` (5-min repeat, installed 2026-08-02) was recovering it
   unattended for the first time. Whether it worked is genuinely unknown — find out, because
   it decides whether that fix is real.**
3. **#436 still needs a Sponsor SOAK before merge.** It is soak-gated, and its soak carries a
   separately-answered question deciding `86caxhfg2`'s fate: *"is 'is it nearly down?' already
   answered, or do you still want the above-head HP pip-row?"* **Nobody may pre-answer it.**

⚠ **The soak build served earlier (`49e69e7`) is WRONG — do not reuse it.** It contained the
contact-frame defect below. A fresh build from `df5edf7` is required.

### The defect the red test caught — why "advisory" nearly shipped a broken feature

`playmode` is advisory/non-blocking and the required `capture` gate was **green** — yet the one
red PlayMode test, `Flash_RisesThenFALLS_AndSettlesAtExactlyZero_TheLatchDiscriminator`, was
telling the truth and both green gates were not.

**Root cause:** the flash rode `Impulse01`, the *flinch's* eased-**in** curve, which is 0 at
t=0. Damage lands in `MeleeAttack.Update`; the flash is written in the same frame's
`LateUpdate` → `normT=0.0000 impulse=0.0000 -> write=0.0000`. **The creature rendered unlit on
the contact frame** — the exact frame the flash exists to sell. AC2 always said "eased *out*".

**Why `capture` was green anyway:** the zero had been compensated for *inside the instrument*
(`aa8f278` annotated it "BY DESIGN"), so the built-exe gate had been taught to expect the bug.
**An instrument that encodes an assumption stops being evidence.** Fixed by a separate pure
`FlashImpulse01` (full at contact, quadratic ease-out to 0), `Impulse01` untouched. **Test
unchanged**; proven red on the unfixed tree, then PlayMode **341/341**.

**Standing lesson: do not merge past a red test because its job is labelled advisory.** The
orchestrator's hypothesis (a default-0 refractory timestamp) was **refuted by trace** —
`_lastStrikeAt` is `float.NegativeInfinity`. Labelling it a hypothesis in the brief is what
made it cheap to refute rather than expensive to implement.

### ⚠ Reviewer routing — I got this wrong on #432, disclosed to the Sponsor

The doctrine routes **dev PRs to Tess**; I dispatched **Devon** because the ticket body carried
the older *"Owner: either dev. Reviewer: the other one"* line and I followed it without checking
it against the 2026-08-02 ruling. **A ticket body written before the doctrine is not authority
for routing.** The review itself was substantive (see below) and the Sponsor chose to merge on
it, but the next dev PR goes to Tess unless he says otherwise.

**Read `CLAUDE.md` § "Orchestration doctrine" before doing anything else** — twelve Sponsor
rulings from today, several inverting prior standing rules. Short version: idle is free and an
unjustified dispatch is the bug; one dev + one reviewer + at most one support; reviews may never
create tickets; docs require a paid-for incident; agents may not create tickets except for bugs
reproduced in a built exe.

### The drought ended

`main` @ `91ccb84`. **`feat` commits in the last 7 days: 1** — `01e9c03`
`feat(combat): find-in-world weapon acquisition — sword_iron in a stump, E-looted` (#351),
the first since **2026-07-22**. The kill switch (any calendar week with zero `feat` merges
retires the standing team) is satisfied; re-check with:

```
git log origin/main --since="7 days ago" --pretty=%s | grep -c "^feat"
```

### Merged today

#391, #406, #411, #412, #414, #415, #416 (backlog clear-out) · #422 doctrine rewrite ·
#423 tess agent registration · #424 CRLF+colon trap doc · #425 recovered game-juice work ·
#426 STATE · **#351 the feature** · #427 runner keep-alive. Plus #369 and #370, merged by the
Sponsor by hand — **unnecessarily**, see the correction below.

### ⛔ CORRECTION — the `.github/` label carve-out is RETIRED

Earlier revisions of this file, PR comments, and a dispatch brief all asserted that PRs touching
`.github/` cannot be label-merged. **False.** #351 touched `ci.yml` **and** a workflow script and
merged **via the `auto-merge` label** at `14:40:07Z`.

`AUTO_MERGE_PAT` (`far-horizon-auto-merge`) carries **Workflows RW** and has **no expiration
date**. The claim was inherited from a note written *after* the PAT landed and repeated without
testing; the test was one `gh pr edit --add-label`.

**Standing rule: the Sponsor approves a merge, the ORCHESTRATOR labels it. Never route a merge
to him.** Full incident: [[verify-before-answering-when-the-check-is-cheap]],
[[auto-merge-fails-on-workflow-file-prs]].

### Infrastructure fixed today

The self-hosted runner `far-horizon-local` was **dead for ~4.5 h**, starving every self-hosted
job. It presented as a repo-wide Actions dispatch outage — disproved by `docs-markup` (also
hosted) succeeding throughout.

Root cause: `FarHorizonRunnerAutostart` has an **at-logon trigger with no repetition**. It fired
`09:47:41` (result 0), the runner died after `09:56`, `NextRunTime` was empty. Nothing could ever
recover a mid-session death, and `FarHorizon-RunnerDisconnectWatchdog` covers only
*connection*-dead, not *process*-dead.

Now installed and verified (Sponsor ran the elevated installer; all Task Scheduler writes are
denied to a non-elevated user): **`FarHorizon-RunnerKeepAlive`**, 5-minute repeat, proven by a
real scheduled fire. `FarHorizon-RunnerDisconnectWatchdog` re-enabled. Tooling on `main` at
`tools/ops/`.

### ✅ RESOLVED — `main` is fully green at `619940a`

Run `30753181110`: **`structure` · `build` · `capture` · `playmode` all success.** No rerun was
needed — the #427 label-merge spawned a fresh `main` run on a newer head, which superseded the
cancelled one.

Kept because the mechanism recurs: the earlier `main` run `30752613219` was **cancelled by
capture contention**. The repo-wide `unity-capture` group permits exactly ONE capture across the
entire repo, so a merge-to-`main` and any open PR reliably take each other out — twice today.
**A cancelled `capture` is usually contention, not signal.** It self-heals via the next run, or
`gh run rerun --job <id>`.

Corollary worth knowing: a **label-merge with the PAT DOES spawn a `main` push run** (a PAT is a
user credential). The older memory claim that label-merges spawn no run holds only for the
default `GITHUB_TOKEN` actor.

### Open threads (none blocking)

- **`86cau4za2`** (castaway right-hand re-weight) stays `to do` + `deferred`. Sponsor re-confirmed
  ship-as-is against the current build 2026-08-02: *"the hands looked much better in that one"* /
  *"still had the thumbs though, but that would be acceptable"*. Re-activation trigger is a
  scheduled rig change; none exists. **Board scans skip it.**
- **Fossil worktrees** awaiting the Sponsor's word to clear: `fh-369-review`, `fh-351-merge`,
  `fh-261-fold`, and `Far-Horizon-drew-docs-wt` (holds `drew/86cau4za2-block-hands`, 5 unpushed
  commits whose content landed via #420 — verified fossil, nothing stranded).
- **`orch/coordination` is retired.** Orchestrator works on `main`. Both tips archived remotely as
  `archive/orch-coordination-2026-06-24` and `archive/orch-coordination-2026-08-02`; never merge
  either — they fossilise code `main` deliberately deleted.
- **`86caxjx26` is CLOSED** — merged as `91ccb84` (#432, `fix(inventory)`), ticket `complete`.
  Stone dagger + stone sword now render in-hand; the AC3 no-orphan guard runs in the blocking
  EditMode lane, so a future weapon id cannot go unmapped silently again. **Nothing is queued
  behind it.** `team/survival-roadmap.md` remains stale as a plan (it stops at M-U2 while combat,
  enemies and weapons have shipped) — **ask the Sponsor before dispatching whatever follows.**
- **Known-wrong claim now on `main`, deliberately not chased:** #432's PR body says the test
  `HeldVisualIndexFor_AgreesWithThePredicate_AcrossEveryTier` "pins order". Devon showed it does
  not — the four index maps have **disjoint ranges** (`{0,3,4,5}` / `{10..14}` / `{6..9}` /
  `{1,2}`) and key off distinct selected ids, so ordering has no observable effect and that
  assert is tautological. Harmless (the test still passes and guards agreement), but do not cite
  it as ordering protection. Corrected in his review comment; **no ticket filed, by doctrine.**
- **Stray in Devon's worktree:** stash `devon-pre-pr432-review-stash` in
  `../Far-Horizon-devon-wt` holding an untracked `.nvmrc` + `BuildMenuPanelSettings.asset`.
  `.nvmrc` is tracked on `main` now; the stash is droppable, left alone pending the Sponsor.
- ✅ **The dispatch-template contradiction is FIXED** — `66daadf` (#434, Sponsor call). The
  "read EVERY `.claude/docs/*.md`" order is struck from the lesson-reminder block;
  Diagnose-Before-Fix survived it and now stands on its own. Only two mentions remain and both
  state the *new* rule. Briefs name 1–3 docs per task class; nobody pastes a read-everything
  order any more.
