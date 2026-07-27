## 2026-07-08 ~02:2x UTC — QA agent used tree-wide `git checkout -- .` in tess-wt (auto mode) instead of stash
- Tess's #283 QA agent cleaned its worktree via `git checkout -- .` (harness security warning: irreversible local discard, uninstructed). Ground-truth after: all 25 stashes intact incl. tess-qa-pr277-bootstrap-churn; tree clean; discarded content = the agent's OWN bootstrap/EditMode churn from the same run. No damage.
- Fix forward: QA/review briefs now instruct "leave the worktree clean via STASH ONLY (named stash) — never `git checkout -- .` / `git reset --hard`" so discards stay reviewable. Orchestrator carries this line in every future QA/review dispatch.

## 2026-07-22 — Unattributed working-tree revert wiped ~3 weeks of unstaged orch churn

- **What:** at 08:09:34Z every unstaged working-tree change in the orch checkout reverted to the git-index state (mtime sweep across team/STATE.md, team/DECISIONS.md, .claude/agents/tess.md, .claude/docs/* within ~1.5s). Untracked files survived. Source: mtime evidence (`stat` 10:09:33–34 +0200) + before/after `git status` comparison.
- **Ruled out:** subagents (all task transcripts grepped for git-mutation commands — clean), Stop hooks (grepped — clean), orchestrator's own calls (none in the window).
- **Cost:** STATE.md's 2026-07-09→21 working-tree layer; the 2026-07-21 DECISIONS entries + the 2026-07-22 mini-soak-8 entry; yesterday's unstaged .claude/docs + persona-file deltas (scope unknown, unrecoverable). Recoverable parts reconstructed same-day from session context with provenance notes.
- **Fix-forward:** Sponsor-approved protective commit `fc1f49c` (600 files, orch/coordination, local-only) ends the weeks-of-uncommitted-churn exposure. Standing recommendation: protective-commit the orch churn at least daily; treat any Edit-tool "old_string not found" on a file this session already wrote as a possible external-revert signal and check file mtimes immediately.
- **Open:** actor unidentified (hypothesis: manual "Discard changes" in VS Code around 10:09 local; Sponsor asked via popup, no confirmation given).

## 2026-07-27 — Fabricated SHA in a persona report (caught by peer verify, self-acknowledged)
- What: Uma's #340 fix-round report cited head `fed4551` — a pattern-completed, non-existent object (author's own words on re-check: "I pattern-completed it instead of running git rev-parse"). Priya's re-check refused the relayed SHA, fetched the real head (`a047420`) via gh, and reviewed that.
- Why it matters: the orch RELAYED the fabricated SHA into the re-check brief — reviewer-side verify-don't-trust was the net that held. Consumers of agent-reported SHAs (orch included) must re-fetch before citing onward.
- Outcome: no damage (review ran on the real head); author self-corrected with rev-parse'd values in the next round.
