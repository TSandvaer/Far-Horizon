# Team State — Far Horizon

**This file is a RESUME HEADER, not a log.** One current section. When it is superseded, it is
**replaced**, not appended to — the previous content stays retrievable with
`git log -p team/STATE.md`. It grew to 479 lines of stacked superseded headers before
2026-08-02; do not let that happen again.

Decisions go in `team/DECISIONS.md` (append-only). Operational scrollback goes nowhere.

---

## RESUME NEXT-ACTION — 2026-08-02 (Sponsor PRESENT; auto-status OFF and staying off)

**The orchestration doctrine was rewritten today.** Read `CLAUDE.md` § "Orchestration doctrine"
before doing anything else — twelve Sponsor rulings, several of which invert prior standing
rules. The short version: idle is free, an unjustified dispatch is the bug; one dev + one
reviewer + at most one support; reviews may never create tickets; docs require a paid-for
incident; agents may not create tickets except for bugs reproduced in a built exe.

**Agents in flight: ZERO.** The prior orchestrating session was stopped by the Sponsor so the
rulebook could be rewritten without a live loop acting on the old doctrine.

**Verified state (measured 2026-08-02, `origin/main`):**

- Last `feat` commit: **2026-07-22** (`0dc4844`, wild boar, #332).
- 79 commits since: 47 docs, 12 chore, 10 fix, 8 test, 1 spike, 1 ci — **zero feat**.
- 10 open PRs at session start, of which **one was gameplay** (#351).

**Open-PR disposition executed 2026-08-02** (Sponsor decision: green merges with no further
review, the rest close — nothing needed closing, all were green). **Merged via the `auto-merge`
label, largest diff first to avoid the label race:** #391, #406, #411, #412, #414, #415, #416.

**Still open — three, each for a stated reason:**

- **#351** (`86cah7y5b`) `feat(combat): find-in-world weapon acquisition — sword_iron in a stump,
  E-looted`. **This is the destination.** Soak-gated; its AC7 is Predict-Before-Soak and the
  Sponsor's play is structurally the only real-input gate — no capture gate in this repo can
  inject a real mouse click.
- **#369** (`86caynve9`) `chore(ci): CI-wire -verifySwings`. Green, but touches
  `.github/workflows/` and **the auto-merge Action's token has no `workflow` scope, so the label
  silently no-ops on this class** (known carve-out `86cafhehe`). **Needs the Sponsor's hands.**
  It is what makes #354's palm + release guards actually run.
- **#370** (`86cayp0p9`) `test(ci): guard the COMMITTED WeaponSetLineup prefab against generator
  drift`. Green, same `.github/` carve-out. **Needs the Sponsor's hands.**

**Destination:** close out the weapon/combat line — find a weapon, fight the boar with it. Then
the Sponsor picks the next milestone deliberately. ⚠ `team/survival-roadmap.md` is **stale as a
plan**: its plan of record stops at M-U2 (one need → axe → chop → campfire) while the team has
shipped combat, enemies and weapons well past it. Do not treat it as the destination.

**Kill switch is armed.** Any calendar week with zero `feat` merges retires the standing team:
`git log origin/main --since="7 days ago" --pretty=%s | grep -c "^feat"` → `0` means collapse to
a single hands-on session + on-demand QA.

**Archived 2026-08-02** (away mode is off, so they have no consumer):
`team/log/away-queue-archive-2026-08-02.md` (2,172 lines) and
`team/log/decisions-while-away-archive-2026-08-02.md` (813 lines, **37 entries still marked
`pending review`** — archived unreviewed, with the Sponsor informed).
