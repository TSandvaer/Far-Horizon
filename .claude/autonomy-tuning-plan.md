# Autonomous away-orchestration tuning — plan

**Status:** PARTIALLY APPLIED 2026-06-18 (sponsor: Thomas, via `/grill-me`).
- ✅ **Applied:** deny-hook `.claude/hooks/block-destructive-bash.sh`; the CLAUDE.md "Autonomous
  orchestration" backstop section; the global `auto-status` skill rewrite (generic away/local prompts,
  idle-capacity-is-a-bug in all modes).
- ✅ **ACTIVATED + VERIFIED 2026-06-18:** sponsor renamed `.proposed` → `settings.json` (old config at
  `settings.json.bak`) + restarted. Verified live in the restarted session: (1) **bypass active** —
  non-allowlisted `git remote -v` ran with NO permission prompt; (2) **deny holds under bypass** —
  `git -C <path> branch -D` blocked by the `block-destructive-bash.sh` hook (arg-order-robust; the deny
  glob wouldn't match the `-C` form) with its custom staging message, and `rm -rf <nonexistent>` blocked
  earlier. Permission posture proven. The behavioral away-loop (fill/pivot/auto-merge/no-popup) proves in
  a real away run.
- ⏳ **Deferred to after verification:** global-CLAUDE.md promotion (§E) + port to other projects (rollout §3)
  + the Unity-slot concurrent-builds follow-up ticket (§H).

**Goal (sponsor's words):** keep the ClickUp board clean, dispatch as many team agents as possible,
and make away-mode orchestration *truly* autonomous — stop halting fast on a sponsor gate (pivot to the
next ticket instead) and stop blocking on trivial "allow agent" / AskUserQuestion popups.

---

## Decisions captured (the grill)

1. **Scope:** design + prove on Far Horizon, then promote durable parts to user-global `CLAUDE.md` +
   the global `auto-status` skill so every private ClickUp project inherits them.
2. **Permission posture:** allow-most + **hard-deny the dangerous** (machine-enforced, not prose).
3. **Merge autonomy:** auto-merge fully-gated **non-subjective** PRs; soak/feel PRs get a machine-readable
   marker and stage for the sponsor.
4. **Pivot rule:** dependency-aware — a gated ticket blocks ONLY its hard-dependents; fill the next
   dispatchable slot by priority, never idle behind a gate.
5. **Unity slot:** cap at 1 Unity-build ticket in-flight now; maximize the non-Unity lane; file a
   follow-up ticket for concurrent builds.
6. **Heartbeat:** one strengthened loop (full board scan + fill-every-slot is step 1), riding the
   existing completion-driven re-invoke for fast cadence.
7. **Away popups:** the orchestrator raises ZERO interactive prompts in away mode — queue every sponsor
   item + pivot; go quiet only if literally everything is blocked on the sponsor.
8. **Board hygiene:** full auto-hygiene (reconcile status, flesh ACs on dispatch, file mechanical
   follow-ups, close dupes); subjective scope/priority stays gated.
9. **Enforcement:** mechanical-first (rewrite the away-mode *prompt* + settings allow/deny + a label
   convention; CLAUDE.md prose is backstop only).

**Sponsor directive added mid-session:** board-scan + fill-every-dispatchable-slot is a **standing
behavior in ALL modes** (away, local/`on`, and auto-status OFF) — not away-only. Mode governs only
*cadence* and *decision-autonomy*, never *whether* to dispatch. **Idle capacity is a bug.**

---

## Verified harness mechanics (from `claude-code-guide`, doc-cited)

- **Permission evaluation order: `deny` → `ask` → `allow`.** `bypassPermissions` skips only the *default*
  ask prompts — it does **NOT** bypass explicit `deny` or `ask` rules. ⇒ We can run the orchestrator in
  bypass (no routine prompts) AND keep a `deny` list that mechanically blocks the dangerous class.
  Source: https://code.claude.com/docs/en/permissions.md
- **The "allow agent" prompt + the PR-merge confirmation classifier both live in *auto* / *default* mode,
  not bypass.** ClaudeTeam (the most mature orchestrated project) auto-merges precisely because its main
  project `settings.json` sets `"defaultMode": "bypassPermissions"`. Far Horizon currently sets no
  `defaultMode` → runs in default mode → every non-allowlisted tool prompts. **This one missing line is
  the bulk of the popup pain.**
- **Agent allowlist syntax** (only needed if we stay in default mode): `Agent(Explore)`, `Agent(devon)`,
  etc., or bare `Agent`. Under bypass it's unnecessary.
- **Glob `deny` rules are bypassable by argument reordering** (`git reset --hard` vs `git --hard reset`).
  For the highest-risk few, a PreToolUse hook (AST-aware) is more reliable. ⇒ deny-list = layer 1,
  a small deny-hook = layer 2.
- **Unverified / open:** `skipAutoPermissionPrompt` (set `true` in global settings) is undocumented —
  leave as-is, not load-bearing. Whether a live session reloads `settings.json` changes mid-flight vs
  on restart — confirm at apply time.

---

## Mode matrix (the new contract)

| Behavior | auto-status OFF | `on` / local (present) | `away` |
|---|---|---|---|
| Scan board + fill every dispatchable slot | ✅ when it has a turn | ✅ every pulse | ✅ every tick |
| Dependency-aware pivot off gated tickets | ✅ | ✅ | ✅ |
| Stage fully-gated PRs as one-click merge items | surface to sponsor | surface to sponsor | ✅ stage to away-queue — NEVER auto-merge to `main` (classifier blocks it) |
| Decisions/gates needing the sponsor | ask (present) | ask (present) | **queue + pivot, never popup** |
| Board auto-hygiene | ✅ | ✅ | ✅ |
| Subjective soak / priority / strategy | always sponsor | always sponsor | always sponsor (staged) |

> ⚠ This **redefines `on`/local mode** — it is no longer a "read-only status pulse." It now dispatches
> to fill slots; it just surfaces decisions to the present sponsor instead of auto-deciding. The global
> `auto-status` SKILL.md must be updated to drop the "report only, never spawn agents / change state"
> contract for local mode.

---

## Change set (concrete)

### A. Far Horizon orchestrator `.claude/settings.json`  (orchestrator project dir ONLY — not the persona worktrees)

Add `defaultMode` and a `permissions.deny` block. Keep the existing `allow` list.

```jsonc
{
  "env": { "CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS": "1" },
  "permissions": {
    "defaultMode": "bypassPermissions",
    "deny": [
      "Bash(git push --force:*)",
      "Bash(git push -f:*)",
      "Bash(git push --force-with-lease:*)",
      "Bash(git reset --hard:*)",
      "Bash(rm -rf:*)",
      "Bash(rm -fr:*)",
      "PowerShell(Remove-Item:*Recurse*)",
      "Bash(gh repo delete:*)",
      "Bash(git branch -D:*)"
    ],
    "allow": [
      "Bash(gh pr merge:*)",
      "Edit(.claude/agents/*.md)",
      "mcp__clickup__*",
      "Bash(git status)", "Bash(git log:*)", "Bash(git diff:*)", "Bash(git branch:*)",
      "Bash(git fetch:*)", "Bash(git checkout:*)", "Bash(git add:*)", "Bash(git commit:*)",
      "Bash(git push:*)", "Bash(git pull:*)", "Bash(git worktree:*)",
      "Bash(gh pr view:*)", "Bash(gh pr list:*)", "Bash(gh pr create:*)",
      "Bash(gh pr review:*)", "Bash(gh pr comment:*)"
    ]
  }
  // ... existing hooks block unchanged ...
}
```

Notes:
- `gh pr merge --admin --squash --delete-branch` (the project's merge command) is NOT denied — deleting
  the PR's own merged branch is part of the normal flow.
- `--delete-branch` works because deny only blocks the explicit destructive forms above.
- **Verify at apply time:** whether sub-agents (persona dispatches) inherit the orchestrator's
  `bypassPermissions`. ClaudeTeam's persona worktrees do NOT set bypass — keep ours as-is unless the
  inheritance check shows personas need it. Personas are background agents (can't answer a prompt), so
  their guardrails matter.

### B. PreToolUse deny-hook (layer 2, robust against arg-reorder)

New hook `.claude/hooks/block-destructive-bash.sh` (matcher `Bash|PowerShell`), modeled on the existing
`no-unseen-clickup-ids.js` pattern. Parses the command and **blocks** (exit with `{"decision":"block"}`)
if it matches force-push / `reset --hard` / recursive-delete semantics regardless of flag order, and
emits a one-line "staged to away-queue instead" reason. Register it in the orchestrator project settings
PreToolUse list. (Belt-and-suspenders with the glob deny in A.)

### C. Global `auto-status` SKILL.md — rewrite the prompts

Path: `~/.claude/skills/auto-status/SKILL.md`.

**C1 — Away-mode prompt (replace the Step-2 block verbatim):**

```
Active orchestration tick — the user is AWAY. The team must never idle. Respect every hard rule in
this project's CLAUDE.md (protected branches, testing bar, never decide the sponsor's subjective
soak/priority/strategy calls).

STEP 1 — ALWAYS scan the board first. Read every open ticket on the project's ClickUp list. Compute the
DISPATCHABLE set: tickets whose hard-dependencies are already merged AND that fit free capacity. A
ticket gated on the sponsor (needs a soak, a testing-bar call, a destructive/infra action, or any
sponsor decision) blocks ONLY its hard-dependents — never the whole board. Tag such a ticket
`sponsor-gate`, write the decision to away-queue.md, and PIVOT.

STEP 2 — FILL EVERY DISPATCHABLE SLOT. Capacity = one task per persona-worktree; AT MOST ONE
Unity-build ticket in flight (single-runner/PackageCache constraint) — fan the non-Unity lane
(docs/research/spec/review/QA) out freely. Sequence two tickets that touch overlapping files; otherwise
parallelize. Dispatch by sponsor priority among dispatchable tickets; priority is the tiebreaker, NEVER
a reason to idle. Every dispatch/PR-open/merge pairs with the matching ClickUp status move.

STEP 3 — AUTO-MERGE the safe PRs. Merge any PR with ALL machine gates green (CI SUCCESS + peer-review
APPROVE + Tess QA PASS + Self-Test Report present) AND NO `needs-soak`/`sponsor-gate` label. PRs with
that label stage to away-queue for the sponsor's soak — never auto-merged.

STEP 4 — BOARD HYGIENE. Reconcile status to reality (merged→complete, dispatched→in progress,
PR-open→in review, QA-ready→ready for qa test, gated→keep status + `sponsor-gate` tag). Flesh missing
ACs/OOS on tickets you're about to dispatch. File mechanical follow-ups (NITs, approved-scope
decomposition). Close obvious duplicates. Log every autonomous board/merge action to
decisions-while-away.md per the four-gate framework.

STEP 5 — NEVER raise an interactive popup/AskUserQuestion. Everything needing the sponsor → away-queue.md
(one-click drain via /sponsor-questions-walkthrough on return). If literally nothing is dispatchable and
everything is sponsor-blocked, go quiet — do not pop a dialog.

STEP 6 — Verify in-flight agent liveness from a fresh SendMessage probe + git log + gh pr view (never
from assumption). Revive/re-dispatch stale agents with a WIP-preserving brief.

Update last_tick in <project>/.claude/auto-status.state. Emit a concise summary (scanned / dispatched /
merged / staged / hygiene). If nothing needed doing, one line.
```

**C2 — Local/`on` mode:** drop the "read-only; do NOT spawn agents, merge PRs, or change state" wording.
Replace with: *runs the same STEP-1/STEP-2 board-scan + fill-every-slot + hygiene as away, BUT surfaces
sponsor-gate decisions and merge approvals to the present sponsor (popups OK — they're here) instead of
auto-deciding/queueing.* Keep the 5-min cadence.

**C3 — Skill description + Notes:** update the front-matter description and the "report only" note to
match the new local-mode contract.

### D. Far Horizon `CLAUDE.md` — add a short "Autonomous orchestration" section

Backstop prose pointing at the mechanical sources: the standing "idle capacity is a bug / fill every
dispatchable slot in all modes" rule, the dependency-aware pivot, the never-popup-when-away rule, the
auto-merge-gated-non-subjective rule, and the `needs-soak`/`sponsor-gate` label convention. Keep it
tight — the load-bearing copy is the away-prompt (C) and settings (A/B).

### E. user-global `CLAUDE.md` — promote the durable rules (after FH proves them)

Add a global section "Standing dispatch + away-mode autonomy" capturing: idle-capacity-is-a-bug;
dependency-aware pivot; never-popup-when-away (explicit override of the "prefer popups" rule);
auto-merge fully-gated non-subjective PRs with a machine-readable soak marker; bypassPermissions +
deny-list as the orchestrator permission posture. Cross-link the existing "Orchestrator autonomy",
"Unattended autonomy", and "Prefer AskUserQuestion popups" sections (the away override amends that last
one).

### F. Soak / sponsor-gate marker convention (machine-readable)

- ClickUp **tag** `needs-soak` (subjective feel/look gate) and `sponsor-gate` (any other sponsor
  decision) on the ticket.
- GitHub PR **label** `needs-soak` mirrored on the PR.
- Rule: a PR with `needs-soak` is NEVER auto-merged; it stages to away-queue with the exact exe path +
  expected HUD build stamp + the explicit "test THIS" checklist (per the soak-handoff memory).

### G. Board-hygiene status mapping (REAL statuses — verified 2026-06-18)

List `901523878268` statuses: `to do` → `in progress` → `in review` → `ready for qa test` → `complete`.
There is **no "blocked" status.** Mapping:
- dispatched & coding → `in progress`
- PR open, under review → `in review`
- QA pending → `ready for qa test`
- merged → `complete`
- gated on sponsor → keep the functional status + add the `sponsor-gate` tag (NOT a status move)
- *(optional, sponsor decision)* add a `blocked` custom status to the list for cleaner signalling —
  `override_statuses` is already true, so it's a one-list change. Do NOT add it autonomously.

### H. Unity-slot follow-up ticket

File on list `901523878268`: "CI: enable concurrent Unity builds (per-worktree PackageCache isolation
or 2nd self-hosted runner)" — the real throughput ceiling. Tier it as sponsor-scheduled infra, not
auto-started.

---

## Rollout order

1. Apply A + B + C + D to Far Horizon (when the other session is idle). Run one away cycle and watch:
   no "allow agent" popups, slots fill, a safe PR auto-merges, a `needs-soak` PR stages.
2. If clean, promote C (skill is already global) + E (user-global CLAUDE.md).
3. Port A + B to each other orchestrator project's main dir (ClaudeTeam already has bypass; add the
   deny-list there too). Add the `needs-soak`/`sponsor-gate` label convention to each tracker.

## Test / acceptance

- Dispatch a sub-agent in away mode → **no permission popup**.
- A force-push / `rm -rf` attempt → **blocked** (deny + hook), staged note emitted, session continues.
- Two independent ready tickets + one gated ticket → orchestrator dispatches the two, tags + stages the
  gated one, does not idle.
- A non-subjective PR with all gates green + no `needs-soak` → auto-merged. A `needs-soak` PR → staged.
- `gh pr merge --admin --squash --delete-branch` runs without a prompt.

## Open risks / verify-at-apply

- Sub-agent permission-mode inheritance under bypass (check before relying on persona guardrails).
- Live `settings.json` reload semantics (mid-session vs restart).
- `bypassPermissions` removes the safety net for anything NOT on the deny list — the deny-list +
  deny-hook must stay reasonably complete; review them whenever a new destructive tool/skill is added.
- External-post skills (`post-pr`, `post-release` → Teams webhooks) are never-auto: the away-prompt
  forbids invoking them (stage instead). They are not deny-listed because they're Skill invocations the
  orchestrator must actively choose; the behavioral rule + queue covers them.

## Refinements discovered during the live verification run (2026-06-18)

- **Auto-merge peer-APPROVE gate must NOT key on GitHub `reviewDecision == APPROVED`.** Sub-agent
  reviewers run under the shared `TSandvaer` git identity and GitHub blocks `gh pr review --approve`
  on your own PR/identity, so peer reviews land as **`COMMENTED` with the APPROVE verdict in the body**
  (the #211 precedent). An auto-merge gate that requires a formal `APPROVED` review decision would NEVER
  fire → the team stalls (the exact failure this whole effort targets). **Fix:** the away-mode auto-merge
  gate's "peer APPROVE" condition = "a peer-reviewer agent posted a review/comment whose body carries an
  explicit APPROVE verdict" (not the GitHub reviewDecision field). Fold this into the away-mode prompt's
  STEP 3 wording when promoting globally. Observed live on PR #80 (Devon's review = `COMMENTED`/APPROVE).
- **STATE.md can carry stale fixture/agent claims across sessions.** PR #80's brief (sourced from STATE.md)
  named a PlayMode fixture (`HeldAxeRunJumpClampPlayModeTests`) that does NOT exist in the repo; Tess
  verified rather than blindly `[Ignore]`-ing it (never-guess working). Liveness/fixture claims in STATE.md
  must be re-verified against ground truth before acting — the away prompt's STEP 6 (liveness-from-probe)
  already covers agents; extend the habit to fixture/file claims.
- **Merge-to-protected-`main` is NOT autonomous — the classifier blocks it even under bypass (this REVERSES
  the original §A/Q3 "auto-merge fully-gated non-subjective PRs" choice).** Live on PR #80: every gate green
  (unity SUCCESS + Devon APPROVE + Self-Test + no soak label) and bypass confirmed active, yet
  `gh pr merge 80 --admin --squash` was DENIED by the auto-mode classifier ("Admin-merge to protected main
  / Production Deploy"). **Sponsor decision 2026-06-18:** away-mode NEVER auto-merges to `main`; it STAGES
  each fully-gated PR to `away-queue.md` as a one-click `gh pr merge <n> --admin --squash --delete-branch`
  item (+ gate evidence) for the sponsor to run on return. Baked into the away-prompt STEP 3 + CLAUDE.md
  away-mode bullet + memory `classifier-blocks-merge-to-protected-main`. Everything UP TO the merge stays
  autonomous; only the final merge-to-main is human.
