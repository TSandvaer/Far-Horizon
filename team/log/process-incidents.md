
## 2026-07-08 ~02:2x UTC — QA agent used tree-wide `git checkout -- .` in tess-wt (auto mode) instead of stash
- Tess's #283 QA agent cleaned its worktree via `git checkout -- .` (harness security warning: irreversible local discard, uninstructed). Ground-truth after: all 25 stashes intact incl. tess-qa-pr277-bootstrap-churn; tree clean; discarded content = the agent's OWN bootstrap/EditMode churn from the same run. No damage.
- Fix forward: QA/review briefs now instruct "leave the worktree clean via STASH ONLY (named stash) — never `git checkout -- .` / `git reset --hard`" so discards stay reviewable. Orchestrator carries this line in every future QA/review dispatch.

## 2026-07-19 ~04:1x UTC — Stale reviewer checkout produced a false REQUEST_CHANGES + false fabrication accusation (PR #306)
- **What happened:** Uma's #306 review (comment 5014063864) claimed the PR-body ground truth was fabricated (no WoodWeaponSetTests, 2-tier 10-node lineup). Orch ground-truth check refuted it: her cited line numbers matched the PRE-#304 WeaponPackAssetGen.cs; her worktree checkout was stale (uma-wt sat on the old pr-301-review branch; the gh pr checkout evidently did not land the expected base). WoodWeaponSetTests.cs + the WoodSet row exist on origin/main@75a9725.
- **Detection:** reviewer-vs-author claim conflict → orch ran the decisive checks (ls-tree on origin/main + line-number comparison + #304 file list) before acting on either claim ([[verify-before-telling-sponsor-to-act]] applied agent-to-agent).
- **Damage:** none merged; one wasted review round + a false accusation on the PR (corrected by orch comment).
- **Fix going forward:** review/QA dispatch briefs now carry a checkout-freshness gate in Step 0 — after gh pr checkout: `git merge-base --is-ancestor <expected-base-sha> HEAD` must exit 0 AND a named post-base file must exist, else STOP-and-report. Template updated (team/orchestrator/dispatch-template.md).
