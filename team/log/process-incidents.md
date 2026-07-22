
## 2026-07-08 ~02:2x UTC — QA agent used tree-wide `git checkout -- .` in tess-wt (auto mode) instead of stash
- Tess's #283 QA agent cleaned its worktree via `git checkout -- .` (harness security warning: irreversible local discard, uninstructed). Ground-truth after: all 25 stashes intact incl. tess-qa-pr277-bootstrap-churn; tree clean; discarded content = the agent's OWN bootstrap/EditMode churn from the same run. No damage.
- Fix forward: QA/review briefs now instruct "leave the worktree clean via STASH ONLY (named stash) — never `git checkout -- .` / `git reset --hard`" so discards stay reviewable. Orchestrator carries this line in every future QA/review dispatch.

## 2026-07-19 ~04:1x UTC — Stale reviewer checkout produced a false REQUEST_CHANGES + false fabrication accusation (PR #306)
- **What happened:** Uma's #306 review (comment 5014063864) claimed the PR-body ground truth was fabricated (no WoodWeaponSetTests, 2-tier 10-node lineup). Orch ground-truth check refuted it: her cited line numbers matched the PRE-#304 WeaponPackAssetGen.cs; her worktree checkout was stale (uma-wt sat on the old pr-301-review branch; the gh pr checkout evidently did not land the expected base). WoodWeaponSetTests.cs + the WoodSet row exist on origin/main@75a9725.
- **Detection:** reviewer-vs-author claim conflict → orch ran the decisive checks (ls-tree on origin/main + line-number comparison + #304 file list) before acting on either claim ([[verify-before-telling-sponsor-to-act]] applied agent-to-agent).
- **Damage:** none merged; one wasted review round + a false accusation on the PR (corrected by orch comment).
- **Fix going forward:** review/QA dispatch briefs now carry a checkout-freshness gate in Step 0 — after gh pr checkout: `git merge-base --is-ancestor <expected-base-sha> HEAD` must exit 0 AND a named post-base file must exist, else STOP-and-report. Template updated (team/orchestrator/dispatch-template.md).

## 2026-07-22 — Unattributed working-tree revert wiped ~3 weeks of unstaged orch churn

- **What:** at 08:09:34Z every unstaged working-tree change in the orch checkout reverted to the git-index state (mtime sweep across team/STATE.md, team/DECISIONS.md, .claude/agents/tess.md, .claude/docs/* within ~1.5s). Untracked files survived. Source: mtime evidence (`stat` 10:09:33–34 +0200) + before/after `git status` comparison.
- **Ruled out:** subagents (all task transcripts grepped for git-mutation commands — clean), Stop hooks (grepped — clean), orchestrator's own calls (none in the window).
- **Cost:** STATE.md's 2026-07-09→21 working-tree layer; the 2026-07-21 DECISIONS entries + the 2026-07-22 mini-soak-8 entry; yesterday's unstaged .claude/docs + persona-file deltas (scope unknown, unrecoverable). Recoverable parts reconstructed same-day from session context with provenance notes.
- **Fix-forward:** Sponsor-approved protective commit `fc1f49c` (600 files, orch/coordination, local-only) ends the weeks-of-uncommitted-churn exposure. Standing recommendation: protective-commit the orch churn at least daily; treat any Edit-tool "old_string not found" on a file this session already wrote as a possible external-revert signal and check file mtimes immediately.
- **Open:** actor unidentified (hypothesis: manual "Discard changes" in VS Code around 10:09 local; Sponsor asked via popup, no confirmation given).
