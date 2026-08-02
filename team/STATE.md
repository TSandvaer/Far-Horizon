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

**Agents in flight: ONE — Drew, on `86cah7y5b` / PR #351 (dispatched 2026-08-02, agentId
`af92602cad52eb357`, worktree `Far-Horizon-drew-wt`, branch `drew/86cah7y5b-find-in-world`).**
⚠ Per [[agent-resume-ids-die-with-session]] that agentId does NOT survive a session restart —
if this session dies, **re-dispatch fresh**. The full brief is recoverable: it is the
"Follow-up scope" block of the soak-verdict comment on PR #351 (2026-08-02).

**The #351 soak FAILED on the attract cue — this is what Drew is fixing.** Sponsor played
`Build\soak-351\FarHorizon.exe` (stamp `zoned | 2026-07-30T22:19:59Z | d9b88cd`, verified from
the shipped `resources.assets`). Verbatim: *"the sword is floating, moving in the stump."*
Acquisition mechanics all PASSED (E-loot, no re-loot, equip, in-hand, no duplicate). The
float-bob animates the sword relative to the stump so it reads as hovering **in** it rather
than driven **into** it.

**Sponsor's rule (general, not a patch):** motion cues are a property of PLACEMENT — an item
driven into or resting on something is STILL; an item lying loose may bob.

⚠ **The confound that decides the re-soak.** The Sponsor DID spot the sword before the prompt
at default framing — but he spotted a *moving* one, and the fix removes that motion. That PASS
does **not** transfer to a still sword; nobody has seen one. So the re-soak answers exactly ONE
question: *walking up at default framing, does the motionless sword-in-stump still catch the
eye?* **Yes** → FORM silhouette suffices, done, no marker mesh. **No** → only then spend a
per-instance FORM/POSITION channel. A marker mesh is deliberately NOT pre-built.

Also still open: the F3 dev-console "Weapon finds" default (1 per region) — never reached.

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
