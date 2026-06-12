# Team State — Far Horizon

This file is the orchestrator's source of truth between ticks. The first section is always the live "Resume next-action" header — if a session dies, the next orchestrator starts there.

---

## RESUME NEXT-ACTION — 2026-06-12 late evening (post-resume: castaway wave open)

**If this session dies right now, the next orchestrator does this:** AUTO-STATUS OFF (drained earlier; re-arm only on Sponsor ask). MILESTONES: M-U1 + M-U2 CLOSED; style wave 2/4 merged — hero axe PR #21 + blob-canopy trees PR #22; main @ `8788d22`. IN FLIGHT: **Uma on castaway direction phase `86ca8ca1m`** (ticket "in progress"), agentId `a2381517cad9d8594`, branch `uma/86ca8ca1m-castaway-style-direction`, deliverable `team/uma-ux/castaway-style-v2.md` via PR (reviewer Tess); on her PR + Tess review → merge, then dispatch Devon on the implementation half. BACKGROUND: **serve_soak running in tess-wt** pinned to `8788d22` (bash task `bn2etygcn`) — tess-wt is BUSY until it completes (no Tess dispatch before then); on completion hand Sponsor the exe path + HUD stamp sha `8788d22` (soak covers loop feel + axe/trees/warm-first-frame in one pass). SPONSOR OWES: loop-soak verdict — THE gate for Priya filing the M-U3 board (thin-first; roadmap §3: second need / food / day-night subset). HOUSEKEEPING: orch-docs PR for unity-conventions bullet + this header (in flight as of this commit); Priya's DECISIONS.md weekly batch OVERDUE (founding day + 06-12 decisions unrecorded — vertex-color-inline-materials pattern included); Tess's PR #22 NIT (first-frame canopy left-of-center) folds into future framing work. RUNNER: serialize CI pushes (EPERM class). Godot archive: c:/Trunk/PRIVATE/RandomGame/team/STATE.md.

---

## Role sections

Each role updates its own section as it works M-U1 / M-U2 tickets. The orchestrator reads the whole file at every tick to spot stalls. Don't edit another role's section. Last-updated uses ISO date `YYYY-MM-DD`.

### Priya (Project Leader)
(idle — M-U1 docs spine current as of U10; next planning surface is the M-U2 thin-survival-loop backlog, which files when M-U1 closes)

### Uma (Game UX Designer)
(fresh — first M-U2 surface is the thin-loop UX once M-U1 closes)

### Devon (Game Developer #1, lead — engine/runtime/build/CI)
(fresh — ported U3 input + drove U4 CI; next is M-U2 survival-loop systems)

### Drew (Game Developer #2 — content systems/tools)
(fresh — populated as M-U2 content work dispatches)

### Tess (Tester)
(fresh — owns U7 testing-bar translation in QA; build-capture gate per `team/TESTING_BAR.md`)
