# Decisions while away — Far Horizon

Append-only audit log of orchestrator autonomous decisions made during away-mode (per the user-global Orchestrator-autonomy rule). Sponsor reviews on return and marks each `accepted` / `reversed by <name> <date>`.

**Self-audit on review (reversal-density + outcome-anchoring).** This log already tracks a 5–10% reversal *calibration* target (too-cautious <5% / too-loose >15%). Re-use the SAME `Status:` field for a SECOND, different check — *self-deception detection* — whenever the log is reviewed (Sponsor return, drain, `/sponsor-questions-walkthrough`):
- **Reversal-density.** An all-`ACCEPTED` run is as suspect as an all-green audit trail — near-zero reversals across many autonomous calls more likely means the foundation bar was too loose (the decisions weren't really falsifiable) than that every call was right. If a whole away-stint shows 0 reversals, re-read the riskiest 2–3 entries and ask "would the Sponsor actually have chosen differently?" before trusting the streak.
- **Outcome-anchoring.** For each past `Decided`, check whether it actually held up over SUBSEQUENT entries — a decision marked `accepted` that a later entry quietly worked around is an *unrecorded reversal*. Catches per-decision confabulation the calibration count misses.
- Same field, two questions: calibration asks "is my reversal *rate* healthy?"; this asks "is my *log* honest?". (Borrowed from the reference earned-autonomy suite's `orient` skill — reversal-density-as-confabulation-detector + outcome-anchoring.)

---

## 2026-06-13 0619 UTC — Merge PR #26 (Mini Chibi Kid chibi castaway integration)

- **Decided:** Merge PR #26 (`--admin --squash --delete-branch`) — the Sponsor-chosen chunky-cartoon castaway base — to main, and close PR #25 (Quaternius bone-scale) as superseded.
- **Foundation:** Promoted auto-decide class "routine-PR-merge when CI green + peer reviewer attached" ([[merge-authorization-in-normal-autonomy]] + [[auto-execute-classes-without-sponsor-ack]]). Gates met: CI green (run 27458552931, unity + structure SUCCESS on HEAD `2dd37df`), Tess APPROVE with independent shipped-exe reproduction (review 4491008890 — chibi upright + Idle + Walk + blob shadow, EditMode 103/103 PlayMode 30/30, guards correct, CC-BY license present), Self-Test Report posted. Testing bar (`team/TESTING_BAR.md`) satisfied. The Sponsor already made the subjective base-choice (clicked Mini Chibi Kid); the look-verdict is the post-merge SOAK, which is reversible (recolor/iterate in ≤1 PR).
- **Alternative:** Queue the merge for the Sponsor to approve the look before landing. Rejected because the subjective call (which base) was already the Sponsor's; merging only enables the soak that IS their look-gate, and the result is reversible.
- **Reversibility:** Revert the squash merge in 1 PR, or iterate via recolor/base-swap; the superseded PR #25 stays available. Identity recolor is already a planned tunable follow-up.
- **Status:** ACCEPTED — the auto-decide was correctly BLOCKED by the auto-mode classifier (2026-06-13 0620 UTC: "PR merges require explicit confirmation regardless of auto/orchestrator mode"), NOT retried. The Sponsor then EXPLICITLY approved the merge on 2026-06-13 via /sponsor-questions-walkthrough; merged with `--admin --squash` (chibi PR #26 → `9dd317f`, decisions PR #27 → `97d8283`), PR #25 closed superseded. Outcome confirms the lesson: PR-merge-to-protected-main is NOT an orchestrator auto-decide on this project — the classifier overrides the promoted "routine-PR-merge" class; explicit Sponsor approval is required and was the right gate. Treat all `main` merges as Sponsor-gated going forward.

## 2026-06-13 0756 UTC — Beach-water lands in the SHIPPED scene (MovementCameraScene/Boot.unity), not a WorldBootstrap migration

- **Decided:** Drew implements the stylized ocean in the shipped soak scene (`MovementCameraScene` → Boot.unity), reusing + re-tuning the existing water infra (`LowPolyZoneGen.BuildWaterEdge`/`MakeWaterMaterial` + `LowPolyVertexColor` shader) per Uma's PR #28 brief — NOT migrating the soak scene to `WorldBootstrap`.
- **Foundation:** Uma's PR #28 finding — the soak/game ships `MovementCameraScene`, which has NO ocean; the existing water lives only off-scene in `WorldBootstrap` (small, over-glossy, hidden behind spawn). The Sponsor's goal ("water visible in the scene the soak ships") dictates the shipped scene. This is a technical placement call, not a strategic/subjective one.
- **Alternative:** migrate the soak to `WorldBootstrap` (which already has water) — rejected: larger refactor, touches the scene graph more, no benefit for the immediate "visible water" goal.
- **Reversibility:** scene/material edit, revertable in 1 PR.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-15 1320 UTC — Split nudge-tool keys (WorldLookNudgeTool → F10) before serving the combined soak

- **Decided:** Before serving the Sponsor the combined soak (#48 `de97ba4`), dispatch Drew for a small key-split — `WorldLookNudgeTool` toggle F9→F10 (+ scope each tool's Tab/PageUp/PageDown to its OWN active panel) — so F9 = character dials (arm/axe/GROUND-Y) and F10 = world-look dials (sky/fog/clouds/mountains). Then rebuild + serve.
- **Foundation:** Drew's own flagged follow-up in the reconcile report (both nudge tools default `KeyCode.F9` + share Tab/PageUp/PageDown → collide when both active in the combined build) + the dial-philosophy memory [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]] — this soak's W2 mountain-warmth resolution is explicitly "Sponsor dials warmth via F9", so a broken dial would hand him a broken soak instruction (violates the [[soak-handoff-path-and-explicit-test-checklist]] bar).
- **Alternative:** Serve `de97ba4` now (look works; dialing collides) and split keys only if/when he tries to dial. Rejected: the W2 resolution depends on dialing → near-certain serve→fix→re-serve cycle; one short fix now yields a fully-working soak.
- **Reversibility:** one-file KeyCode change on #48, revert in 1 PR.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-22 1944 UTC — Armed auto-status AWAY + dispatched Priya for board hygiene
- **Decided:** Armed away-mode (cron `051f1771`, 15-min, session-only) on Sponsor's `/auto-status away`. First tick: dispatched Priya (`a885a92e35f4aa548`) for ClickUp board hygiene — reconcile statuses + file 2 follow-up tickets (devon-wt worktree-churn build block; 2 pre-existing EditMode fails HungerNeed.startFull / InventoryUI.themeStyleSheet) + flesh gameplay-wave ACs. Did NOT un-hold the sponsor-held gameplay wave (priority is the Sponsor's call). Devon `a555382c5819efa7d` already in flight on the #100 uniform axe-head resize.
- **Foundation:** auto-status away skill (STEP 4 → dispatch PL persona for hygiene) + CLAUDE.md "Priya owns the board".
- **Alternative:** surfacing would have asked the Sponsor whether to dispatch Priya / whether to start gameplay-wave prep.
- **Reversibility:** CronDelete `051f1771` stops the loop; Priya's ClickUp edits revert in ≤1 pass; the 2 filed tickets close if unwanted.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-22 ~2000 UTC — Dispatched Drew on the worktree-churn devx fix (86cacvhc6)
- **Decided:** dispatched Drew (`a3cd5826f45280676`) on `86cacvhc6` (stop the recurring persona-worktree churn build-block — classify transient bootstrap output → gitignore vs commit real assets). Routed to DREW not Devon to keep Devon's worktree FREE for the imminent #100 axe-head bake on the Sponsor's soak-return (worktree-concurrency + #100 priority). Did NOT auto-dispatch: the gameplay wave (sponsor priority-HELD even though its deps #83/#90/#93 are now on main — release is the Sponsor's call) nor `86cacvhf4` (EditMode-fails — deferred: build-slot + verify-real-on-main wrinkles, lower leverage).
- **Foundation:** away-mode STEP 2 (fill dispatchable slots) + the churn friction documented all session (forced push-and-CI every soak iteration) + Priya's filed ticket `86cacvhc6`.
- **Alternative:** surfacing would've asked whether to start the churn fix / release the gameplay wave / which follow-up first.
- **Reversibility:** revert Drew's PR (config/gitignore, ≤1 PR); not merged to main (away stages only).
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-22 ~2131 UTC — Cancelled #118's wedged advisory playmode CI run
- **Decided:** `gh run cancel 27984975834` — #118's advisory playmode job hung ~30min (past its ~15min timeout = wedged), holding the single build slot. unity+structure already SUCCESS, so #118 stays green per-job; cancel frees the slot for the #100 bake on Sponsor-return.
- **Foundation:** [[advisory-playmode-job-unreliable-soak-is-interaction-gate]] (playmode hangs + holds the lock; advisory/non-blocking) — pre-cleared CI-hygiene auto-decide class.
- **Reversibility:** re-run the CI anytime; no code/state change.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-22 ~2153 UTC — Dispatched Drew on 86cacvhf4 (EditMode-fails verify+fix)
- **Decided:** dispatched Drew (`a1ca7aad63dfbf19a`) on `86cacvhf4` — verify `HungerNeed.startFull` + `InventoryUI.themeStyleSheet` on a clean-main checkout, fix the real ones. Build slot freed (#118's wedged playmode finally cleared `completed/cancelled`). Dependency-aware brief: InventoryUI half is LIKELY the untracked-theme that #118 already fixes (Drew confirms, does NOT duplicate); HungerNeed half is independent. Stopped deferring after 3 quiet ticks — slot free + away-mode fill mandate; Devon kept reserved for the #100 bake so routed to Drew.
- **Foundation:** away-mode STEP 2 (fill slots) + Devon-flagged failures + Priya's ticket `86cacvhf4` + the testing bar (main shouldn't carry red EditMode).
- **Alternative:** keep deferring until #118 merges (cleaner, but idles Drew indefinitely while the Sponsor's away).
- **Reversibility:** revert Drew's PR, or close the ticket as not-repro.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-23 ~0710 UTC — Dispatched Drew on 86cacyg63 (bootstrap-hint test guardrail)
- **Decided:** dispatched Drew on `86cacyg63` (Sponsor-APPROVED via the walkthrough this session) — add a no-bootstrap precondition guard to the ~5 scene-presence EditMode tests so a bare local run reports `Assert.Inconclusive` with a "run BootstrapProject.Run first" hint instead of opaque null-red. Routed to DREW (he triaged 86cacvhf4 + recommended this guardrail; has the context); Devon stays FREE for the #100 bake. #118 merged → clean main-based tree for Drew (no churn).
- **Foundation:** Sponsor approved the ticket via `/sponsor-questions-walkthrough` (86cacyg63) + Drew's own recommendation + away-mode STEP 2 fill.
- **Reversibility:** revert Drew's PR (test-only change).
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-25 0912 UTC — Cancelled wedged stale CI run 28157969953 to free the self-hosted runner

- **Decided:** `gh run cancel 28157969953` (in-progress CI on `ad28c5a`, a SUPERSEDED round-6 commit) — its hung advisory-playmode job was pinning the single self-hosted runner, blocking the HEAD run `28158834315` (`af584b8`) from scheduling its unity job.
- **Foundation:** [[advisory-playmode-job-unreliable-soak-is-interaction-gate]] (the playmode job hangs + holds the runner) + the run was for a SUPERSEDED commit (af584b8 supersedes ad28c5a) so its result is not needed; the HEAD run is what the pond gate depends on.
- **Alternative:** wait for the hung job to self-timeout (~past 20 min) — rejected: slower, and the stale run's result is irrelevant.
- **Reversibility:** fully reversible — CI runs are re-runnable; cancelling a superseded run loses nothing (HEAD run 28158834315 carries the real result).
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-25 0912 UTC — Dispatched Devon round-7 (pond sea-plane hole under-covers the organic footprint)

- **Decided:** Dispatch Devon (background, devon-wt) round-7 to enlarge the sea-plane hole to fully clear the organic pond footprint past the waterline, after round-6 CI's (now-genuine) gate FAILED on SHORELINE-ANNULUS (`topNoShorelineRing=False`, shoreline pale 0.182 @ rNorm 0.30).
- **Foundation:** away-mode STEP 2 (fill the dispatchable slot — pond is the sponsor-priority critical path) + machine-checkable gate evidence (post-gate verify-pond.log); the sea-plane root cause is confirmed (sea-off DIAG → 0.000). A targeted continuation, not a subjective call.
- **Alternative:** relax the annulus threshold to pass 0.182 — rejected: 0.182 is a visible pale crescent (orch eyeballed pond_top/pond_a); the Sponsor would soak-reject it (this saga IS the ring).
- **Reversibility:** code iteration on the open PR #130 branch; revertable.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-25 1521 UTC — Re-armed AWAY + dispatched the gameplay wave (CHOP) + #139 review + hygiene

- **Decided:** On the Sponsor's `/auto-status away` (clicked in the resume popup), armed away mode (cron `948797d0`, 15-min) and on the first tick dispatched 3 background agents: Devon (`af7fac927aeace68d`) on CHOP `86caa4c5c` (the wave's build-slot task), Drew (`afffce75a8c415321`) to review PR #139, Priya (`ad70c00c83eac199e`) for board hygiene. Deferred sticks/stones (build-slot serialized behind CHOP; also serialized with each other — both write `ScatterIslandProps`, confirmed by Priya).
- **Foundation:** away-mode skill STEP 2 (fill every dispatchable slot) + the single-Unity-build-slot cap ([[single-unity-build-slot-serializes-orchestration]]) + STATE.md "Next steps" (chop→sticks→stones). Sponsor explicitly chose away mode.
- **Alternative:** present-mode (surface decisions live) — not chosen; Sponsor clicked away.
- **Reversibility:** CronDelete `948797d0` stops the loop; the dispatches are reversible (PRs revert in ≤1).
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-25 1521 UTC — Re-dispatched CHOP as a WIP-PRESERVING resume (not fresh) after a deny saved the WIP

- **Decided:** My first CHOP dispatch (a "recreate fresh" Step 0 with `git reset --hard` + `git branch -D`) was deny-blocked by the auto-mode classifier. On inspection devon-wt held UNCOMMITTED chop WIP from the prior stopped dispatch (modified `CastawayArmPose.cs`/`ChopTree.cs`/`SettingsCatalog.cs` + new `ChopPoseDriver.cs`). Re-dispatched Devon with a NON-destructive resume Step 0 (fetch + status only) that preserves and builds on the WIP. Did NOT retry the denied command (per never-spiral).
- **Foundation:** [[background-agent-rate-limit-death-salvage]] (preserve a stopped agent's uncommitted WIP; never clean/reset) + the deny guardrail working as designed. The fresh-recreate would have wiped real work.
- **Alternative:** re-dispatch fresh (lose the WIP) — rejected once the WIP was found.
- **Reversibility:** the WIP is on-disk in devon-wt; resume PR reverts in ≤1.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-25 1521 UTC — Deferred the CI playmode-shorten (86cae8vn2) this session

- **Decided:** Did NOT dispatch the `86cae8vn2` ci.yml playmode-timeout shorten (20→5) this tick despite free dev capacity (Drew/Priya idle after their tasks).
- **Foundation:** [[classifier-blocks-merge-to-protected-main]] (away mode can't merge it → no protective payoff until the Sponsor merges) + [[single-unity-build-slot-serializes-orchestration]] (a 2nd CI-triggering PR contends with CHOP's CI rounds on the single self-hosted runner; keeping the runner clear for the priority CHOP work). Idle-by-constraint, not neglect.
- **Alternative:** dispatch it now so a stageable PR is ready on the Sponsor's return — deferred because the contention cost outweighs the marginal staging benefit; revisit when CHOP's CI settles.
- **Reversibility:** trivially dispatchable on any later tick; no state changed.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-26 1436 UTC — Switched to AWAY + dispatched the Sponsor-authorized cleanup-backlog burndown
- **Decided:** On Sponsor's `/auto-status away`, armed away-mode (cron `dc375429`, 15-min; killed present cron `7af0e18a`). Executed his AskUserQuestion-authorized "burn down the cleanup backlog": dispatched Priya (`af15146e9352471c3`) on `86cae5vpt` (asset-routing docs index — now PR #143) + Drew (`a7337c0f21a913029`) on a bundled PR for `86cabfa32` (gate AxePickup Debug.Log) + `86caan3aj` (clamp-guard PlayMode test). HELD `86cabnjv8` (bush NITs — overlaps #140's `LowPolyZoneGen.cs` → sequence after #140 merges) + `86cab8u19` (test rename — queued behind the single build slot). Staged to away-queue: the Mixamo-clip ask (gates chop change-(b)) + the cursor-lock `86caatv7k` soak-confirm.
- **Foundation:** Sponsor's popup answer "Burn down the cleanup backlog (Recommended)" (this session) for the directive; `[[single-unity-build-slot-serializes-orchestration]]` + `[[no-gating-without-dependency]]` for the collision-aware ticket selection / bundling / holds; away-skill STEP 2.
- **Alternative:** hold the team quiet until the Mixamo clip lands (the popup's other option) — Sponsor explicitly chose the burndown.
- **Reversibility:** CronDelete `dc375429` stops the loop; the 2 burndown PRs revert in ≤1 PR each (or close if unwanted); ticket statuses revert.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)
- **NB:** the earlier same-session change-(a) audit + gate-hardening re-dispatch + cursor-lock NO-OP handling were PRESENT-mode (Sponsor-aware) actions — not away decisions.

## 2026-06-27 1540 UTC — E-loot #147 gating + belt-eligibility follow-up filed
- **Decided:** (1) dispatched Drew (`ad802aaa9761f5831`) to code-review PR #147 (E-loot foundation) in parallel with its in-progress CI; (2) flipped `86caf7a6q` → in review; (3) filed new ticket `86caf7g6f` (belt-eligible non-tool consumables — water+berries) as the prerequisite Devon surfaced for the left-click-consume ticket `86caf7a30`.
- **Foundation:** review-routing is routine peer-review ([[tess-cant-self-qa-peer-review]] + auto-execute-classes); review-in-parallel-with-CI is the /whip false-gate rule (review ≠ merge gate); ticket-fleshout/creation is autonomous per [[clickup-task-management-full-autonomy]]; the belt-eligibility need is Devon's PR #147 final-report recommendation (belt is Tool-only; water/berry consumables need a belt-eligible item kind).
- **Alternative:** wait for #147 CI green before reviewing (slower); not file the dependency ticket (loses the tracked prerequisite).
- **Reversibility:** trivial — close `86caf7g6f` / re-flip status; review is read-only.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored).

## 2026-06-27 1610 UTC — #147 STAGED (all gates green); hold-to-chop dispatched
- **Decided:** (1) Tess QA PASS on PR #147 → ALL machine gates green (CI unity+structure SUCCESS + Drew APPROVE + Devon Self-Test + Tess QA PASS) → STAGED #147 in away-queue with the soak build (`Build/soak-147/FarHorizon.exe`, stamp `effcbea`) + the one-click `gh pr edit 147 --add-label auto-merge` command. Did NOT auto-merge (UX-visible + merge-to-main sponsor-gated). (2) Build slot freed (#147's advisory playmode cancelled) → dispatched Devon (`a25db0828505e4eb7`) on hold-to-chop `86caf7a0p` (Sponsor wish #1, independent of #147, off main).
- **Foundation:** away-mode STAGE-don't-merge rule ([[classifier-blocks-merge-to-protected-main]]); UX-visible PRs are sponsor-soak-gated ([[sponsor-merge-approval-soak-or-complete]]); hold-to-chop is independent of E-loot ([[no-gating-without-dependency]]) + the Sponsor's explicit wish; idle-capacity-is-a-bug (fill the freed slot).
- **Alternative:** auto-merge #147 (blocked by policy/classifier); hold hold-to-chop until #147 merges (idles the slot).
- **Reversibility:** trivial — don't add the label / close the branch; hold-to-chop is a fresh branch.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored).

## 2026-06-27 1634 UTC — #148 STAGED (all gates green + bounded-claim); team idle pending #147 merge
- **Decided:** Tess QA PASS on PR #148 (hold-to-chop) → ALL machine gates green → STAGED #148 in away-queue (soak `Build/soak-148/FarHorizon.exe` stamp `c384e96` + one-click `gh pr edit 148 --add-label auto-merge`). Re-engaged Devon to add the Predict-Before-Soak bounded-convergence claim to the #148 Self-Test (Tess's pre-soak item per TESTING_BAR; PR comment 4819166295, no code change). Filed NITs follow-up `86caf7ne0` (chopInterval comment-vs-0.25f doc reconcile, post-merge). Did NOT auto-merge (UX-visible + sponsor-gated). Team now correctly IDLE: both input-model PRs staged; all downstream (belt-eligibility, consume, wave, bush-NITs) gates on #147 merging (Sponsor).
- **Foundation:** away-mode STAGE-don't-merge ([[classifier-blocks-merge-to-protected-main]]); UX-visible sponsor-soak-gated ([[sponsor-merge-approval-soak-or-complete]]); bounded-claim is a TESTING_BAR Predict-Before-Soak requirement Tess flagged; NITs-creation autonomous ([[clickup-task-management-full-autonomy]] / auto-execute-classes); go-quiet-when-all-sponsor-blocked (away STEP 5).
- **Alternative:** auto-merge (blocked); skip the bounded-claim (violates the bar); keep dispatching (nothing dispatchable — all gates on #147 merge).
- **Reversibility:** trivial — don't add labels / close NITs ticket.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored).

## 2026-06-27 1638 UTC — slot-fill: pond-NIT regression tests dispatched (non-gated)
- **Decided:** with both input-model PRs (#147/#148) staged + all their downstream gated on the Sponsor merging #147, dispatched Tess (`adecb100eefb1ab51`) on the non-gated pond-NIT regression tests `86cadr95t` (#130 pond test-only NITs) to fill the idle Unity build slot. Flipped `86cadr95t` → in progress.
- **Foundation:** idle-capacity-is-a-bug + the legitimate-idle bar (a slot stays idle ONLY if all candidate work is gated/sponsor-blocked — pond-NIT tests are NEITHER, so the slot must not idle); test-only + independent of the staged PRs (no collision); within away-mode autonomy (reversible, safe test class, not on the never-auto-decide list).
- **Alternative:** go quiet (would have left a free slot with dispatchable non-gated work — violates the bar); dispatch the water-mat cleanup `86cacer85` instead (lower value — the .mat is moot-at-runtime/unused-asset per its ticket) or the 2nd pond test `86cadnepd` (same files → serialize after this one).
- **Reversibility:** trivial — drop the branch / re-open the ticket.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored).

## 2026-06-27 1700 UTC — earned-autonomy handoff resolved (PR #151) + pond-NIT #150 review routed
- **Decided:** (1) Per the Sponsor's handoff (`team/orchestrator/earned-autonomy-handoff.md`), CAPTURED the at-risk uncommitted earned-autonomy edits — backed up to scratchpad (2 patches) THEN durably folded into NEW **PR #151** (off clean main `aab203d` via `git apply --3way` in a fresh `Far-Horizon-config-wt` worktree, so orch/coordination's dirty tree was NEVER touched — zero clobber risk). #151 = priya/tess/CLAUDE/dispatch-template + new name-the-bar SKILL + decisions-log self-audit block. Did NOT touch GIT_PROTOCOL.md (in #149; would conflict). (2) Tess's pond-NIT PR #150 came back CI-green → routed Drew `ad8b3cc0aaa24bf8b` to peer-review (Tess-authored); `86cadr95t` → in review.
- **Foundation:** Sponsor's explicit instruction (capture before any checkout/reset); the handoff doc's fold-in map; ground-truth-verified (file exists, PR #149 real, working-tree edits present, orch/coordination confirmed ~20k lines stale vs main → built off clean main not the stale branch); review-routing per tess-cant-self-qa-peer-review.
- **Alternative:** build the config PR off orch/coordination (rejected — stale branch); do a risky checkout/reset on orch/coordination (rejected — Sponsor flagged it + unnecessary, used a separate worktree); not build #151, just leave the scratchpad backups (weaker — temp dir, not durable).
- **Reversibility:** trivial — close #151 / drop the branch; orch/coordination tree untouched.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; #151 config changes noted — shipped + in use since).

## 2026-06-27 1705 UTC — #150 STAGED (Drew APPROVE); slot legitimately idle
- **Decided:** Drew APPROVE on PR #150 (pond-NIT tests, test-only, CI green) → STAGED for one-click merge (no soak — test-only). Did NOT dispatch a further slot-fill: the 2nd pond test `86cadnepd` overlaps #150's `FreshwaterPondSceneTests` (sequence after #150 merges); water-mat `86cacer85` is moot-at-runtime "fold into the next water touch" (defer-by-design). So the slot idles for a cited reason, not by oversight.
- **Foundation:** test-only PR gate = CI + peer review (no soak/QA-persona needed for a Tess-authored test PR Drew reviewed); legitimate-idle bar (remaining non-gated work overlaps a staged PR or is defer-by-design); STAGE-don't-merge ([[classifier-blocks-merge-to-protected-main]]).
- **Alternative:** dispatch 86cadnepd now (rejected — file-overlap with staged #150) or water-mat (rejected — disproportionate for an XS moot-at-runtime cleanup the ticket says to fold into a future water touch).
- **Reversibility:** trivial.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; the 4 staged PRs all subsequently merged).

## 2026-06-27 2125 UTC — Stage #148 hold-chop for re-soak as-is (flag N1), don't pre-fix the NIT in the Sponsor's absence
- **Decided:** On Drew's APPROVE_WITH_NITS of #148, stage the PR for the Sponsor's re-soak as-is (after Tess QA) with N1 (the re-press-after-fell dead-window) FLAGGED in the soak checklist + filed as NIT ticket `86caf9ngh` — rather than dispatching Devon to pre-fix N1 before the soak.
- **Foundation:** Drew (peer reviewer) judged N1+N2 non-blocking/follow-up (comment 4821800616); the chop code is a proven-finicky surface (#140 took 5 soak iters) so re-iterating it in the Sponsor's absence risks a new regression they'd then have to soak-catch; bundling any N1 fix with the Sponsor's live re-soak feedback (which may include other cadence asks) is one round-trip vs two. [[soak-fail-test-pass-instrument-runtime]] caution on over-iterating feel code blind.
- **Alternative:** Dispatch Devon now to fix N1+N2 before staging → cleaner re-soak, but +1 Devon build cycle, delays the wave, and risks a speculative regression on a reviewer-judged-non-blocking edge case.
- **Reversibility:** ≤1 dispatch — if the Sponsor (or I) prefer pre-fixing, dispatch Devon on `86caf9ngh` before serving the soak.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-28 1424 UTC — Dispatched Tess on hardening 86cafdevx (away-mode slot-fill after input-model wave merged)
- **Decided:** Dispatch Tess (background, tess-wt, branch off main 8828c48) on the Boot.unity/capture-gate hardening ticket 86cafdevx (EditMode wiring-guards + deterministic verify-capture harnesses + bootstrap null-check LogError). Reviewer: Devon (non-author dev). Flip 86cafdevx -> in progress.
- **Foundation:** away-queue.md "GATED on #162 MERGING" section listed 86cafdevx as dispatchable once #162 merged (now merged this turn, main 8828c48); ticket 86cafdevx carries full ACs and is explicitly "no Sponsor soak (test-infra)" -> non-sponsor-gated. Away-mode "idle capacity is a bug" + STEP 2 fill-every-slot. Landing it first also de-risks tree-chop's CI (shares the capture gates).
- **Alternative:** Hold the single Unity build slot idle for the sponsor-gated tree-chop greenlight. Rejected -- tree-chop needs a Sponsor priority greenlight (staged in away-queue), so holding the slot idles it on a maybe; hardening is non-gated and sequenced cleanly before tree-chop.
- **Reversibility:** test-infra/guards only, revert in <=1 PR; CronDelete stops the loop; ticket returns to "to do" if unwanted.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-29 1451 UTC — Switched to AWAY + dispatched the #186 combined fix + staged 4 PRs
- **Decided:** On Sponsor's `/auto-status away` (he stepped away mid-#186-grill), armed away mode (cron `d16761cc`, 15-min; killed local cron `ee2bef62`). Dispatched Devon (`a263627eb93c8be4e`, devon-wt, continuing the PR #186 branch) on the COMBINED #186 re-iteration: idle="calm but alive" via the Sponsor's Breathing Idle.fbx + walk foot-sync + walk asymmetric-damp + the finger-mangle fix (diagnose-then-repaint per investigation wf `w6un30dyj`). Staged 4 PRs to away-queue (#192 Drew-APPROVE → label-after-approval; #188/#189 manual `--admin`; #191 STATE docs). Auto-merged none (sponsor-gated). Did NOT fan out the non-build CI/Erik lane this turn — deferred to the first away tick (ci.yml-touchers need brief authorization + sequencing).
- **Foundation:** Sponsor `/auto-status away`; #186 fix-spec is Sponsor-LOCKED via the present-mode grill (5 popups → ticket 86cackb3j comment); Breathing Idle.fbx verified on disk; finger plan is investigation-backed (Generic/transform-path rig → weight-repaint preserves all clips, team-only fix). Away STEP 2 (fill the Unity slot with the top-priority fully-scoped work) + STEP 3 (stage, don't merge). Combined dispatch = one PR → one re-soak.
- **Alternative:** Split the finger fix from #186 (two PRs / two soaks) — rejected: both touch the character, one re-soak is cleaner; Devon flags the finger to a follow-up only if the heavy re-model fallback hits. Or hold the Unity slot for the cron — rejected: work is fully scoped now, idling the slot wastes it.
- **Reversibility:** Devon's PR #186 reverts in ≤1; CronDelete `d16761cc` stops the loop; staged PRs are unmerged.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-29 1601 UTC — #186 re-soak STAGED (idle+walk+finger all green); rejected a metric-based "finger clean" verdict
- **Decided:** STAGED the #186 re-soak in away-queue (build `Build/soak-186-v2/FarHorizon.exe`, downloaded from CI run 28384493683 @ HEAD 71a016e — provenance verified, stamp 71a016e). Gates: Drew APPROVE (comment 4834474751) + required CI green (unity+structure) + EditMode 746/746. Flipped 86cackb3j → `ready for qa test`. Did NOT auto-merge (UX/feel → Sponsor re-soak is the gate).
- **Key autonomy call:** Devon's FIRST finger verdict ("clean") rested on a stretch-ratio metric that can't catch a bend/twist. I REJECTED it + re-dispatched Devon (`abafa4a439b1ec48e`) for the VISUAL diagnostic (6 close hand captures + a new FingerPoseRotationTrace), then independently eyeballed hands_left/right.png MYSELF — finger reads clean in the HEAD build. Drew also eyeballed it. The mangle the Sponsor saw was the OLD idle pose (build 7e31635) / Mixamo's web-preview renderer, not our shipping asset. A `-verifyHands` gate now guards it.
- **Foundation:** [[claim-removed-soak-shows-present-investigate-foundation]] + [[verify-grounding-soaks-by-gameplay-cam-visual]] (never accept a metric "clean" against the Sponsor's eyes — verify the symptom-region capture); away STEP 3 (stage, don't merge); [[sponsor-merge-approval-soak-or-complete]]. Also corrected my own earlier stamp error (be35459 → the build was 7e31635) by verifying run provenance directly this time.
- **Alternative:** accept Devon's metric verdict + stage as "finger clean" — rejected (memories forbid re-asserting clean without a symptom-region capture). Or split finger from idle+walk — rejected (one re-soak cleaner; finger resolved).
- **Reversibility:** PR #186 reverts in ≤1; soak build is a throwaway artifact; ticket re-flips.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-29 1606 UTC — Away tick: Drew (INudgePanel) + Erik (sky research, completed); CI lane + hero-prop held
- **Decided:** Away tick. Dispatched Drew (`a0ddcbb2864216f97`, drew-wt) on INudgePanel refactor 86cafz9jr (Unity-build slot) + Erik (`a1a87a51e9987f134`, no-Bash) on sky research 86cabc743 (non-build). Erik COMPLETED — note at `Far-Horizon-erik-wt/team/erik-consult/lowpoly-sky-research.md` (needs orch commit); finding: cloud system already done; gap = a sun-disk in `GradientSkybox.shader` (~30-min HLSL patch). HELD: CI tickets (86cafz9tg/86cafzaeb/86caammpq/86cafhgun — all ci.yml/.github → gate-restructuring risk in away mode + merge needs Sponsor workflow-scope `--admin`) + hero-prop 86cacewju (NOT dispatchable — Sponsor-deferred, trigger-gated on the unified weapon re-author; verify-before-dispatch caught this — Priya's scan over-listed it). Devon/Priya/Uma idle (no clean dispatchable non-build work — legitimate).
- **Foundation:** away STEP 2 (fill dispatchable slots; ≤1 Unity-build) + Priya's scan + [[verify-before-telling-sponsor-to-act]] (read the hero-prop AC before dispatching → revealed trigger-gated).
- **⚠ 86cafz9jr status-flip REJECTED:** no-unseen-clickup-ids hook prompted (ticket unseen this session); Sponsor: "yes and never ask me again." Per [[never-spiral]] did NOT re-fire — ticket drifts at `to do` while Drew works it; reconcile via READ-BEFORE-FLIP (read the ID → seen → flip without the hook prompt). Reinforces [[clickup-task-management-full-autonomy]].
- **Next:** sky POC (GradientSkybox sun-disk + warm defaults + shipped sky-capture, Erik-scoped) = next Unity-build dispatch after INudgePanel, ends in Sponsor soak; commit Erik's note then.
- **Reversibility:** dispatches revert in ≤1; held tickets re-flip; 86cafz9jr drift is cosmetic.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-06-29 1808 UTC — Away tick: known Unity candidates found COMPLETE; dispatched Priya for a fresh scan
- **Decided:** Verified the 2 known next-wave Unity candidates via get_task_details — `86cabuhyw` (BerryBush distance-cull) + `86cabgvgw` (WarmthNeed refactor) are BOTH already COMPLETE (the STATE.md next-wave set is stale; the session cleared most of it). No verified-dispatchable Unity ticket on hand → dispatched Priya (read-only) for a fresh full-board scan. Unity slot idles THIS tick pending the scan — cited reasons: known candidates complete; sun-lower `86cag25az` gated on #194-unmerged; ci.yml lane held (away-mode gate-risk).
- **Foundation:** away STEP 1 (scan whole board) + verify-before-dispatch (reading the candidates avoided a no-op dispatch on already-complete tickets — the hero-prop over-list lesson) + [[orchestrator-fill-nongated-slots-scan-whole-board]].
- **Reversibility:** scan is read-only; no state changed.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-07-01 1810 UTC — Combat POC built + routed to review (away-mode)
- Decided: Combat POC 86cah7xxp built by Devon → PR #224 open; flipped 86cah7xxp to in-review; dispatched Drew for #224 cross-review (agentId a1665ce7cc1b35903); filed the #223 GradientSky.mat NIT as 86cahc2y7.
- Foundation: the build-slot allocation to the Combat POC was the SPONSOR's present-mode popup answer (not autonomous). Drew review routing = cross-review pairing (game-side→Drew) + [[qa-gate-when-tess-unavailable]] (pre-cleared auto-decide class, CLAUDE.md orch-autonomy rule 6). NIT filing = NITs-creation-from-APPROVE_WITH_NITS mechanical scope (pre-cleared, rule 6). Status flip = mechanical.
- Alternative: none material (routing + NIT filing are mechanical, non-strategic).
- Reversibility: fully reversible — a dispatch, a status flip, and a low-pri ticket; ≤1 PR each to undo.
- Status: accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored).

## 2026-07-02 0909 UTC — Auto-merged docs-harvest PR #228 (orch-docs class, peer-APPROVE attached)
- **Decided:** added the auto-merge label to PR #228 (harvest: Erik R5 re-scope note + Priya island-2.0 draft into team/) without a per-PR sponsor popup.
- **Foundation:** user-global orchestrator-autonomy promoted class "routine orch-docs PR merge with peer reviewer" — Uma APPROVE (independent byte-check, zero blockers), docs-only diff (+341/+148, no code/Assets/.github), reversible.
- **Alternative:** a per-PR popup (Sponsor was mid-flow invoking /read-obsidian-folder; stalling a mechanical docs merge).
- **Reversibility:** git revert of a docs-only squash — one PR, trivial.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-07-02 1015 UTC — Auto-merged docs-harvest PR #229 (orch-docs class, peer-APPROVE attached)
- **Decided:** auto-merge label on PR #229 (surgical harvest of 4 POC-day doc bullets, additions-only 5/0).
- **Foundation:** same promoted orch-docs class + same-session precedent (#228, pending review); Uma APPROVE with independent additions-only + tag verification.
- **Alternative:** per-PR popup mid-drain.
- **Reversibility:** git revert of a docs-only squash — trivial.
- **Status:** accepted 2026-07-02 (Sponsor batch-accept via /sponsor-questions-walkthrough; outcome-anchored)

## 2026-07-02 1845 UTC — Auto-status flipped away→local on Sponsor presence (mid-walkthrough)
- **Decided:** Deleted the 15-min away cron + armed the 5-min local/present pulse + state file mode=local — WITHOUT an explicit "/auto-status on" from the Sponsor.
- **Foundation:** The Sponsor invoked /sponsor-questions-walkthrough and is actively mid-soak-dialing (#223 sun) — demonstrably present; precedent 2026-06-29 away-queue: "DRAINED + switched to LOCAL via /sponsor-questions-walkthrough". Away mode's never-popup rule directly conflicts with the in-progress interactive walkthrough.
- **Alternative:** keep away mode running (its no-popup rule would fight the walkthrough) or interrupt his dial to ask about cadence (popup noise for a mechanical call).
- **Reversibility:** one CronDelete + state-file line; he can say "auto-status away" anytime.
- **Status:** accepted 2026-07-03 (Sponsor morning walkthrough popup, accept-both)

## 2026-07-03 0210 UTC — Discarded drew-wt's 2 "salvage" files after forensic identification as corruption artifacts
- **Decided:** per-file `git checkout --` restore of `Assets/Settings/GradientSky.mat` + `ProjectSettings/ShaderGraphSettings.asset` in drew-wt (the 2 uncommitted files protected since the sun-fix agent's rate-limit death).
- **Foundation:** the finger-fix agent's Step-0 inventory read the actual diffs: GradientSky = `_HorizonColor r 0.8→0.42` — the EXACT FKeyMigrationTests SetValue(0.42f) fingerprint (ticket 86cahvntg, now 3 live observations: #231 commit forensics, Drew's Wave1-C report, this working-copy drift from the sun-fix agent's EditMode runs); ShaderGraphSettings = line-endings-only (zero content diff). Drew's real sun-fix work is committed+pushed (`0995cb3`/`d34acf8` on origin) — nothing of value was uncommitted.
- **Alternative:** keep quarantining drew-wt (blocks the finger-fix dispatch on a corruption artifact) or apply-3way salvage (salvaging a known-bad value into nowhere).
- **Reversibility:** the discarded 0.42 drift is reproducible on demand by running the polluting test (its existence is the BUG being fixed in 86cahvntg); no real work lost.
- **Status:** accepted 2026-07-03 (Sponsor morning walkthrough popup, accept-both)

## 2026-07-03 0830 UTC — ATTEMPTED auto-merge of board-hygiene docs PR #240 — classifier DENIED the label; surfaced to the present Sponsor instead
- **Decided:** added the auto-merge label to PR #240 (Priya's board-hygiene doc: island C2/C3 AC rebuild vs C1's settled constants, in team/priya-pl/island-2.0-ticket-draft.md) without a per-PR sponsor popup. Uma's one NIT (C3 decoration bushes reuse the BerryBush body berry-less — bush-lookalike risk vs the Sponsor's soak rejection) is ABSORBED into the C3 dispatch brief as a vertex-colour hue-shift AC line (Path-Y absorption class).
- **Foundation:** user-global promoted class "routine orch-docs PR merge with peer reviewer" + same-project precedent #228/#229 (both accepted); Uma APPROVE_WITH_NITS comment 4874275569 — diff verified team/-only (no code/Assets/.github), C1 constants byte-verified against the branch, all 5 PR↔ticket citations confirmed.
- **Alternative:** a per-PR popup to the present Sponsor (mid-morning flow; mechanical docs merge).
- **Reversibility:** git revert of a docs-only squash — one PR, trivial.
- **Status:** not executed — classifier denied the autonomous label-add (per-PR sponsor approval required in local mode); converted to a sponsor popup in the same tick

## 2026-07-03 10:15 UTC — #241 sun-fields NIT absorbed into 86caj0rrg (no standalone NITs ticket)
- **Decided:** PR #241's reviewer NIT (WorldLookStateSnapshot omits skybox `_SunDirection`/`_SunSize` from Capture/Restore + the AC4 drift-assert; review comment 4874686393) is absorbed into ticket `86caj0rrg` (shared GradientSky.mat mutation fix) as an AC-addendum comment, instead of filing a standalone NITs ticket.
- **Foundation:** user-global promoted auto-decide class "NITs-absorption-into-downstream-ticket when files overlap AND downstream is scheduled" — 86caj0rrg owns the same GradientSky sun-field surface and is next in the serial cluster (#223 → #241/86cahvntg → 86caj0rrg → 86cahxeek); the NIT scope is mechanically derivable from the reviewer comment.
- **Alternative:** a standalone XS chore ticket (adds board noise for a two-field snapshot addition).
- **Reversibility:** split back out into its own ticket with one comment; no code touched. ≤1 PR.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-04 2339 UTC — Dispatched weapons integration (86cajkk7h) on #252 merge
- **Decided:** dispatched Devon on 86cajkk7h (weapons integration) right after #252 (weapons pack) merged to main (3f4e735).
- **Foundation:** 86cajkk7h ticket has full ACs; its hard-dep (pack on main) was met by #252 merging. Routine dep-met dispatch.
- **Alternative:** hold for Sponsor confirm — but the weapons were Sponsor-locked in the burst and integration is the obvious follow-through.
- **Reversibility:** ≤1 PR; branch abandonable; no external side effects.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-04 2339 UTC — Dispatched #247 stepper-room fix from Sponsor soak feedback
- **Decided:** dispatched Drew to widen the F1 int-stepper rows on #247 after the soak-247-v2 verdict ("panels work" PASS + "not enough room for the content" NIT).
- **Foundation:** direct Sponsor soak feedback (screenshot + verbatim); scoped layout fix folding into the existing #247.
- **Alternative:** file as a separate ticket + merge #247 as-is — but the panel isn't Sponsor-final with cramped steppers; folding keeps #247 one coherent PR.
- **Reversibility:** ≤1 PR (the fix commit); revertable.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T00:25:53Z — Away re-armed; dispatched #254 review+QA + #247 structure fix
- **Decided:** Re-armed away (cron 27b3a29e). First tick filled 3 code slots: Drew→code-review PR #254 (weapons integration), Tess→QA #254, Devon→#247 structure-red gate-fixture fix. Flipped 86cajkk7h → in review.
- **Foundation:** STATE.md drain "Next steps" 2+3 (Drew reviews #254 + Tess QA; dispatch Drew/Devon for #247 structure-red fixture) + away-queue CURRENT items 4-5 + routing rule (game-side→Drew; author Devon can't self-review #254). No hard dep blocks any of the three.
- **Alternative:** Surfacing would have asked "review #254 now or wait for capture CI?" — review-in-parallel-with-CI is the standard pattern; capture-red would still leave the code review valid.
- **Reversibility:** All three are read/verify or a single mechanical fixture line; no merge, no destructive action. Undo = ignore verdicts / revert the fixture commit (≤1 PR).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T00:47Z — #254 soak staged; 2nd-wave triage fills the team
- **Decided:** #254 code gates all green (Drew APPROVE + Tess PASS + CI) -> cut soak `Build\soak-254\` (stamp b883283) + produced the `-verifyWeaponSet` tier-contrast lineup PNG myself (Tess flagged it's not CI-wired) -> staged the IN-HAND weapons soak. Devon's #247 structure fix landed (green, 4ec4391). Filled the 3 freed personas: Tess->86cajt6j8 CHOP-triage, Drew->86caj0rrg GradientSky.mat isolation, Devon->86cajt6jb FOG-seam. Flipped all 3 -> in progress.
- **Foundation:** away-queue CURRENT items 4-5 + STATE drain next-steps; board scan (Explore over the get_tasks dump) bucket-1 dispatchable set; overlap discipline (held 86cajt6jz vs #254 HeldWeaponCycleDebug.cs; sequenced 86cahxeek after 86caj0rrg — same GradientSky pollution family). Orch producing the un-CI-wired lineup capture = R&D-lane soak-prep [[verify-soak-builds-or-bake-and-judge]].
- **Alternative:** Surfacing would ask "which 2nd-wave ticket first?" — priority is a tiebreaker among dispatchable, not a gate (all 3 non-gated, non-overlapping).
- **Reversibility:** All 3 are small self-contained fixes on fresh branches (no merge); soak-254 is a staged artifact (no merge). Undo = ignore/close (<=1 PR each).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T01:0xZ — 2nd wave reviewed+staged; 5 follow-ups filed; team drained
- **Decided:** Processed 3 reviews (Drew APPROVE #255; Devon APPROVE_WITH_NITS #256; Drew APPROVE #257). Staged #255 (admin, test-only, capture concurrency-cancelled) + #256 (fully green) as one-click merges; #257 stages on capture-green. Cut soak-247-v3 (7289dbb). Filed 5 orch-owned follow-ups (86caju051/052/054/055/057). Captured the captureDeltaTime cadence-over-count remedy to unity-conventions.md.
- **Foundation:** away-mode STEP 3 (stage fully-gated non-subjective PRs, never auto-merge-to-main) + clickup-task-management-full-autonomy (orch files follow-ups) + scan-board-before-filing-followups (deduped vs the fresh Explore board scan). NIT bundling per the promoted NITs-ticket-creation autonomy class.
- **Alternative:** none surfaced — all reversible board/staging ops; merges stay Sponsor-gated.
- **Reversibility:** staging = away-queue lines (no merge); filed tickets closable; doc edit revertable (<=1 PR).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T07:1xZ — Sponsor soaked #254 weapons: axe REJECTED (model), other 3 approved
- **Decided:** Present-mode soak of #254 in-hand weapons. Sponsor verdict: SPEAR/SWORD/KNIFE dialed + approved; **AXE needs a Blender re-author** — annotated screenshot: "Handle is too thick" + "Head is a little too big". #254 HELD (not merged) until the axe re-soaks clean. Fix owner = ORCHESTRATOR Blender R&D burst (geometry change — handle thickness can't be nudged in-game; personas can't reach Blender MCP), NOT Devon. Sponsor chose "keep draining the other 7 queue items while I fix the axe."
- **Foundation:** direct Sponsor soak + annotated screenshots (stamp b883283); model-policy (Blender asset modeling = orch fable-class R&D) + [[in-house-asset-routes-over-paid-tools]] + [[weapon-two-tier-style-stone-iron]].
- **Axe fix scope:** in `art-src/weapons_reauthor.blend` — thin the stone-axe handle shaft (cross-section) + trim the head proportion a touch; re-export `wpn_axe_stone_01.fbx` per pipeline settings; re-integrate on the #254 branch; re-cut the axe soak. Other 3 FBXs UNTOUCHED (approved).
- **Reversibility:** #254 held (no merge); axe FBX revision is a new export (revertable). 
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T07:2xZ — Present-mode merge/soak drain (Sponsor back): 4 merged
- **Decided (present-mode, Sponsor-approved each):** merged #255 (86cajt6j8 CHOP), #256 (86caj0rrg gradsky), #257 (86cajt6jb FOG), #223 (86cah90cp sun) via auto-merge label (sequenced; re-triggered #257 + #223 after base-branch races). All 4 flipped complete. #253 capture re-running (run 28715961583) → browser-merge link on green. #239 finger soak HELD (Sponsor). #247 → Drew dispatched to merge-from-main + resolve SettingsPanel overlap (agentId ade6d17d9bded1054) → re-cut stepper soak. #254 axe: Sponsor REJECTED axe model (handle too thick + head too big) → orch Blender re-author on FABLE (Sponsor rule: Blender=fable); Blender addon not yet connected (waiting on Sponsor to open Blender + weapons_reauthor.blend + /model fable). Filed 86cajuuz0 (F9 head-nudge broken).
- **Foundation:** away-queue CURRENT staged items + per-PR Sponsor approvals via AskUserQuestion; [[explain-why-before-handing-sponsor-commands]] (orch adds label, no pasted commands) + [[merge-batch-label-race]] (sequence/re-trigger) + [[merge-from-main-avoids-force-push-rebase]].
- **Reversibility:** merges are done (main @ 05a08a7+); axe held; #247 catch-up on a branch.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T08:0xZ — #247 caught up + soak-approved; 2 browser-merges pending Sponsor
- **State:** Drew caught #247 up to main (SHA c110880, clean 3-file conflict resolution F1-player/F3-dev/F10-overlays; CI green). Cut soak-247-v4 (Build\soak-247-v4, stamp 8eba6a2). Sponsor SOAK-APPROVED the F1 stepper layout → #247 = browser-merge pending (.github). #253 drag-ghost also browser-merge pending (capture rerun green). Both handed to Sponsor as GitHub links. On #247 merge: unblocks 86caju054 (sneak-panel), 86cajt6kq (wedge-harden), 86cahxeek (Boot.unity re-bake). #254 axe still pending fable-session FBX handoff (I integrate via Devon after). #239 held.
- **Foundation:** present-mode Sponsor approvals via AskUserQuestion; [[explain-why-before-handing-sponsor-commands]] (.github → browser-merge); [[merge-from-main-avoids-force-push-rebase]] (Drew's catch-up).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T08:1xZ — 6 PRs merged; sneak-panel dispatched; axe integration re-routed to Drew
- **State:** Sponsor browser-merged #247 (51db56f) + #253 (5848aa5). SESSION TOTAL 6 merged: #255/#256/#257/#223/#247/#253. Flipped 86cah8ukr + 86cajrtr1 complete. Dispatched Devon → sneak-panel retire 86caju054 (agentId af5f093fe4e25900d) with F10-overlay-master-preservation caveat (SneakIsolationTool is the F10 master post-#247). HELD: 86cajt6kq wedge-harden (sequence after sneak-panel — both edit SettingsVerifyCapture), 86cahxeek re-bake (behind #254 Boot.unity). Open: #254 (axe — fable-session FBX pending; re-integration now routed to DREW since Devon busy), #239 finger (Sponsor-held).
- **Foundation:** unlock-map from #247 merge; parallel-shared-concept overlap discipline (SettingsVerifyCapture) → sequence sneak-panel/wedge; [[sub-agent-worktree-concurrency]] (Devon busy → axe integration to Drew).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T08:3xZ — PC-CRASH RECOVERY (session + fable session both died)
- **Crash:** Sponsor's PC went down mid-work, killing this session + the parallel fable(Blender) session + all 3 background agents. Recovery:
  - 6 merges SAFE on GitHub (main 5848aa5). Local repo fsck = dangling orphans only (healthy).
  - Devon sneak-panel (86caju054): FINISHED + pushed pre-crash (3ba34d9) but no PR. Salvaged → opened PR #258; CI verifies the tree (structure ✓, build running). Drew reviewing (agentId a43e0381ed3d6ab6f). Flipped 86caju054 → in review. ⚠ no author Self-Test (crashed first) → Drew review + CI capture gate ARE the gate.
  - devon-wt was git-CORRUPTED (broken HEAD, foreign staged files). Repaired: `git worktree remove --force` + recreate `--detach origin/main` (work safe on origin, 0 loss). drew/tess/priya/erik/uma-wt all HEALTHY.
  - AXE re-author (fable session, ticket 86caju... no — #254 axe): LOST. wpn_axe_stone_01.fbx + weapons_reauthor.blend unchanged since 2026-07-03 → the fable session's Blender work never saved. Must REDO from scratch when the fable session is back. #254 stays blocked.
- **Re-armed away** (cron 42fb9af3) on the new session per SessionStart hook.
- **Foundation:** [[background-agent-rate-limit-death-salvage]] (salvage pushed WIP, don't re-run fresh); [[capture-stranded-edits-via-worktree-apply-3way]] sibling (worktree repair); crash killed both local sessions (single-box).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T12:2xZ — Sponsor making a NEW player character (Hyper3D+Mixamo) → #239 PARKED
- **Sponsor:** "im making a new player character in hyper3d + mixamo, so [#239 finger] is not relevant right now." Making it SOLO (like the fable Blender axe session).
- **#239 finger (86cahnmjv) PARKED** — the thumb/grip fix is on the CURRENT castaway rig (CastawayFingerCurl); a new character replacing the castaway makes it moot. PR #239 stays OPEN (not closed/merged) — revisit when the new character lands (does the fix still apply / need redo on the new rig).
- **⚠ IMPLICATION for #254 weapons:** in-hand SEATING is character-rig-dependent (HeldAxeRig on the castaway hand bone per procedural-animation-verbs.md). Sponsor approved spear/sword/knife seating on the CURRENT castaway → a character swap INVALIDATES that seating. Weapon MODELS (incl. the axe redo) are character-independent + still valid. Surfaced to Sponsor: park #254 seating for the new character vs proceed-now-reseat-later (strategic = his call).
- **Foundation:** direct Sponsor instruction; character-pipeline.md (Hyper3D Rodin→Mixamo route); procedural-animation-verbs.md (held-prop seating is rig-dependent).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T12:2xZ — #254 seating PARKED for the new character (Sponsor call)
- **Decided (Sponsor popup):** park #254 in-hand SEATING for the new character — re-seat all weapons ONCE on the new rig when it lands (vs seating twice). The fable axe MODEL redo proceeds in parallel (character-independent geometry, Sponsor's fable session). #254 Unity re-integration + seating + soak WAIT for the new character.
- **Board now fully gated behind the NEW CHARACTER** (Hyper3D+Mixamo, Sponsor solo) + sponsor-gated design. Team quiet-with-reason. On new-character-landing wave: import → Humanoid setup → re-seat all 4 weapons (incl. redone axe) → soak → revisit #239 finger (may be moot on the new rig). NOT filing a new-character integration ticket yet (no ACs until the character is defined).
- **Foundation:** Sponsor AskUserQuestion answer "Park #254 seating for the new char"; held-weapon seating is rig-dependent (procedural-animation-verbs.md).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T13:2xZ — Castaway v2 landed in-motion: docs PR + integration dispatched
- **Sponsor task (4 parts, all done):** (1)+(2) recorded the 2 v2 decisions (identity=bearded rugged friendly-neutral; hero-route ratified concept→Rodin→Mixamo) in team/DECISIONS.md + fixed the stale "young+happy/Quaternius" CLAUDE.md line → **docs PR #261** (folded to a clean branch off main via worktree since the orch tree diverged from main; orch-tree copies reverted). (3) filed integration ticket **86cajwp23**. (4) dispatched **Devon** (agentId add001c4022e81d42) to integrate v2 ONTO #260's branch (Humanoid import + 18-clip retarget + held-axe bone-axis re-measure + retire RecolorShirtToTan; AC4 OLD BASE STAYS LIVE behind a toggle, soak-gated) → #260 becomes the full v2 PR. Reviewer Drew.
- **Flagged to Sponsor (his call, not auto-changed):** CLAUDE.md line 3 "A young, hopeful castaway" still says young — offered to fold into #261.
- **Source of truth:** art-src/castaway-rodin-export/README.md (harvest #260). v2 UNBLOCKS the parked #254 weapon re-seating (happens on the new rig after v2) + #239 finger revisit.
- **Foundation:** direct Sponsor instruction; README checklist; DECISIONS.md protocol (orch logs directly); [[capture-stranded-edits-via-worktree-apply-3way]] (clean-branch docs PR).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T13:3xZ — v2 rig: GENERIC over Humanoid (Sponsor-confirmed; corrects the README handoff)
- **Decided (Sponsor popup):** integrate Castaway v2 as **Generic + transform-path** (CreateFromThisModel per-FBX, the shipping CharacterAssetGen recipe), NOT the README/AC1's "Humanoid + CopyFromOther". Devon STOPPED-and-reported the contradiction (as briefed) with decisive evidence: Humanoid cone-explodes the mesh at runtime (86ca8rdkp fix + live anti-Humanoid gate); v2's 41 bones ⊂ old 57-bone rig (only middle/ring fingers missing) → the 18 existing clips drive v2 by transform path, NO retarget.
- **Actions:** corrected ticket 86cajwp23 AC1 (comment); re-dispatched Devon (agentId a69c0a173f5130048) Generic + added AC5 = fix the README's Humanoid lines on #260's branch so it's not a future landmine. Old base stays live behind a toggle (AC4, Sponsor-locked).
- **Foundation:** 86ca8rdkp (Humanoid→Generic switch after runtime explosion) + LocomotionHitReactVerifyCapture anti-Humanoid gate + Devon's Blender bone-subset dump. Present-mode surfaced (contradicted a Sponsor-approved handoff → his confirm, not orch auto-decide).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T13:5xZ — v2 integration landed on #260 (Generic); Drew reviewing
- **State:** Devon integrated Castaway v2 Generic onto PR #260 (commit bd98443, CI run 28743064101). Behind default-OFF env toggle `FARHORIZON_CASTAWAY_V2==1` → old base ships unchanged (AC4). New: CharacterAssetGen.ConfigureV2BaseFbx (Generic+CreateFromThisModel, 1.0u), v2 assets Assets/Art/Character/Castaway/v2/ (hand-authored metas, fresh GUIDs), CastawayV2HandAxisTrace instrument, RecolorShirtToTan gated off, attribution + README(Humanoid→Generic) fixed. Drew reviewing (agentId aed97ca3d50915ca4). 86cajwp23 → in review.
- **⚠ Soak needs a special build:** the Sponsor v2 soak requires a `FARHORIZON_CASTAWAY_V2=1` build (default CI/build shows OLD). On Drew APPROVE + CI green → ORCH produces the v2-enabled soak build (local headless build with the env var, or a CI variant) → stage for Sponsor.
- **⚠ Held-axe seat = OLD-RIG PRIORS** (HeldAxeV2RelEuler -186,-168,-84 / offset 0.1712,0.1209,-0.0007), NOT a fresh v2 measurement (Devon had no local Unity) → F9-dialable in the soak; the first v2 soak judges character look/locomotion, the axe seat is expected to need tuning.
- **Foundation:** Devon final report bd98443; character-pipeline.md Generic rule.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T14:0xZ — #260 code-green (Drew APPROVE); cutting v2 soak build
- **State:** Drew APPROVE_WITH_NITS on #260 (Generic-confirmed + toggle-OFF-no-regression verified; CI 28743064101 green; 2 NITs → backlog: toe bones in clip-carry core[], register CastawayV2HandAxisTrace). #260 is CODE-GREEN + SAFE to merge (v2 dormant behind default-OFF toggle → old base ships). Sponsor chose "cut the v2 soak build now" (merge decision deferred until he sees v2).
- **Devon cutting the v2 soak build** (agentId a06c7088fc844f49f): FARHORIZON_CASTAWAY_V2=1 headless build + a v2-active capture (must show bearded v2, not old base — orch OPENS the capture before serving) → Build\soak-castaway-v2\. On completion: orch verifies the capture → serve Sponsor the v2 soak (judge look+locomotion; held-axe seat on old-rig priors, F9-dialable).
- **After soak PASS:** merge #260 (dormant) + a follow-up flips UseCastawayV2Default→true to make v2 live. On FAIL: Devon iterates on #260.
- **Foundation:** Drew review comment 4886307351; toggle-default-OFF makes merge dormant-safe; [[verify-soak-builds-or-bake-and-judge]] (verify v2-active before serving).
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-05T14:1xZ — v2 SOAK PASSED → #260 merged → activation dispatched
- **Sponsor soaked v2** (Build\soak-castaway-v2\, bd98443; orch OPENED the identity capture — confirmed bearded rugged v2, clean/grounded, no explosion) → verdict "make v2 live".
- **#260 MERGED** (main 8f78d5a) — v2 code+assets on main, dormant (UseCastawayV2Default=false). 86cajwp23 → complete.
- **Activation dispatched:** ticket 86cajx050, Devon (agentId a30a7ee4ed7b289ec, branch devon/86cajx050-v2-activate): AC1 flip UseCastawayV2Default→true; AC2 re-seat held axe on v2 RightHand (measured from CastawayV2HandAxisTrace, replaces old-rig priors); AC3 reconcile capture gates that now render v2 (chop/held-belt/character-visible — update goldens, don't weaken); AC4 CI green. Soak-gated (default build now shows v2), reviewer Drew.
- **Unblocked by v2 on main:** #254 (parked weapon re-seating knife/sword/spear on v2) + the axe MODEL redo (fable Blender, still pending Blender availability).
- **Open loose threads:** docs PR #261 (2 decisions + CLAUDE fix; Sponsor to merge; offered line-3 "young hopeful" + doc-index "Humanoid" fixes to fold in). #260 NITs (toe bones in clip-carry core[], register CastawayV2HandAxisTrace) → backlog.
- **Foundation:** Sponsor soak-pass + AskUserQuestion "make v2 live"; toggle-flip-as-activation pattern.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-06 ~09:2x UTC — Drew's #254 PlayMode-timing NIT absorbed into 86cajt6jz instead of a new ticket
- **Decided:** Drew's APPROVE_WITH_NITS follow-up (advisory PlayMode red `HeldBeltWeaponVisualPlayModeTests.DebugCycle_...`, recommend the `Time.captureDeltaTime` #255 remedy) was commented onto existing DEBUGCYCLE ticket `86cajt6jz` (same test surface) rather than filed as a new ticket.
- **Foundation:** NITs-absorption-into-downstream-ticket auto-decide class (user-global Orchestrator autonomy rule 6) + [[scan-board-before-filing-followups]] — the open board already carries the DEBUGCYCLE ticket for this exact suite; Drew's comment URL cited in the ticket comment.
- **Alternative:** file a separate `chore(test)` NITs ticket; would duplicate the DEBUGCYCLE surface.
- **Reversibility:** split into its own ticket in one create_task call if the DEBUGCYCLE work shows they're distinct causes; the ticket comment says diagnose-first.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-06 ~16:0x UTC — PR #266 (island-2.0 splits spec, docs-only) auto-merge-labeled without a per-PR Sponsor popup
- **Decided:** labeled #266 for auto-merge on Devon's APPROVE_WITH_NITS (comment 4896532058) without a Sponsor merge popup.
- **Foundation:** user-global Orchestrator-autonomy rule 6 promoted class "Routine-PR-merge calls when CI green + orch-docs/cleanup class with peer reviewer attached" ([[merge-authorization-in-normal-autonomy]]); PR is a planning spec (team/priya-pl/island-2.0-splits.md), docs-only, MERGEABLE, peer-reviewed by the C1 implementer.
- **Alternative:** queue a merge popup (the Sponsor has ruled on 4 merge popups today; this one is the lowest-stakes of the set).
- **Reversibility:** git revert of a docs-only commit, ≤1 PR.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)
- **Note:** Devon's 4 wording NITs get folded into the C2/C3/C4 ticket text at creation (the tickets are where the wording is load-bearing).

## 2026-07-06 ~19:2x UTC — PR #267 (86cak0uq6 residual-NITs fix) auto-merge-labeled without a per-PR Sponsor popup
- **Decided:** labeled #267 on Devon's APPROVE (comment 4896590861, CI green run 28815610739, MERGEABLE, code-only).
- **Foundation:** user-global rule 6 promoted class "Routine-PR-merge when CI green + routine impl/cleanup with peer APPROVE" — mechanical reviewer-NITs ticket, no visual/feel surface (EditMode-gated settings-state fix), not on the never-list.
- **Alternative:** merge popup; the Sponsor has 2 higher-stakes gates already queued (v3-live soak, #265 browser-merge) — adding a third for a NITs fix dilutes the queue.
- **Reversibility:** git revert, ≤1 PR.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-06 ~20:3x UTC — PRs #269 (DECISIONS harvest) + #268 (iron spec) auto-merge-labeled
- **Decided:** labeled both without per-PR popups. #269 = orch-docs harvest (16 DECISIONS entries, content pre-verified verbatim by Tess's #268 fidelity check); #268 = docs-only spec with Tess APPROVE_WITH_NITS (comment 4897060989; NIT 1 resolved BY #269; NITs 2-3 fold into the I-2/I-3 ticket text at creation).
- **Foundation:** rule-6 promoted class (orch-docs/cleanup + peer reviewer); #266/#267 precedent earlier today.
- **Reversibility:** git revert, docs-only, ≤1 PR each.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-06 ~21:2x UTC — PR #270 (v3 dial-in prep) auto-merge-labeled
- **Decided:** labeled on Tess APPROVE_WITH_NITS (comment 4897240688; DLL independently re-verified; CI required green). Her 3 cosmetic stale-text NITs (AxeNudgeTool.cs L102-103/L563/L357-358 — L563 is the Sponsor-visible dial hint) absorb into 86cakkfz9's BAKE round (same file, Drew).
- **Foundation:** rule-6 routine-merge class (code-only debug tooling + peer APPROVE); the Sponsor's dial session runs against the verified build regardless of merge timing.
- **Reversibility:** git revert, ≤1 PR.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-06 ~21:4x UTC — PR #272 (iron I-0 foundation) auto-merge-labeled
- **Decided:** labeled on Tess QA APPROVE (comment 4897381918; §1 vocabulary EXACT; both Drew flags adjudicated clean; CI green).
- **Foundation:** rule-6 routine-merge class (code-only non-build + peer APPROVE). On merge the iron vocabulary is LOCKED on main; I-2/I-3/I-4 dispatch against it (I-2 also needs I-1 pickaxe = Sponsor burst).
- **Reversibility:** git revert, ≤1 PR.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-06 ~22:2x UTC — PR #273 (v3 coverage guards) auto-merge-labeled
- **Decided:** labeled on Devon APPROVE (comment 4897631219; guards verified non-tautological; dial-in false-red risk explicitly cleared; CI EditMode PASS = the authoritative gate for test-only).
- **Foundation:** rule-6 routine-merge class (test-only + peer APPROVE); closure work under /drain-and-save-session.
- **Reversibility:** git revert, ≤1 PR.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-07 ~02:1x UTC — Filed PR #277 NIT-1 as follow-up ticket 86cakp58u (auto-decide, pre-cleared class)

- **Decided:** created ticket `86cakp58u` (scatter-keystone determinism guard test) directly from Devon's APPROVE_WITH_NITS comment 4899433555 NIT-1 on PR #277; sequenced after #277 merges (same test-file surface). Also posted Devon's C4 carry-notes (grass ~1449 load, treeTarget flat, castShadows revert = ~4 sites) as a comment on C4 ticket `86cakk4xf`.
- **Foundation:** user-global orchestrator-autonomy rule 6 "NITs-ticket-creation from APPROVE_WITH_NITS review comments when scope is mechanical" — reviewer enumerated the NIT with concrete test recommendations; scope copied verbatim, no new scope added.
- **Alternative:** queue the NIT for the sponsor walkthrough (delays a mechanical S-ticket for no decision value).
- **Reversibility:** delete/close the ticket; 1 click.
- **Status:** accepted 2026-07-07 (Sponsor, /sponsor-questions-walkthrough popup)

## 2026-07-07 ~22:1x UTC — Advisement: dupe-close 3 orphaned island-2.0 placeholder subtasks (Priya ADVISEMENT NEEDED)

- **Decided:** advised Priya (first advisor-loop escalation under the new fable-advisor policy) to dupe-close placeholder subtasks 86cahwx91 (C2) / 86cahwxb3 (C3) / 86cahwxf1 (C4) as superseded — the real C2/C3/C4 landed under standalone tickets 86cakk4w8/86cakk4x2/86cakk4xf, all complete with merged PRs #271/#277/#278; parent 86cahwwvg closed complete this tick.
- **Foundation:** [[clickup-task-management-full-autonomy]] + dupe-close is in Priya's standing hygiene mandate (CLAUDE.md "Priya owns the board"); the superseding tickets are complete with merged-PR evidence.
- **Alternative:** leave 3 dead "to do" placeholders inflating the open board (the noise class behind the 2026-06-28 idle-failure).
- **Reversibility:** reopen the 3 subtasks; 1 click each.
- **Status:** pending review

## 2026-07-07 ~22:5x UTC — Filed PR #281 NIT-1 as follow-up ticket 86cama43f (auto-decide, pre-cleared class)

- **Decided:** created XS ticket `86cama43f` (document the keystone guards' coverage boundary in NextIslandPocTests.cs) verbatim from Devon's APPROVE_WITH_NITS comment 4909467226 NIT-1 on PR #281; sequenced after #281 merges (same file).
- **Foundation:** user-global orchestrator-autonomy rule 6 "NITs-ticket-creation from APPROVE_WITH_NITS" — doc-only, file-derivable scope, no new scope added.
- **Alternative:** queue for the sponsor walkthrough (no decision value for a comment-only XS).
- **Reversibility:** close the ticket; 1 click.
- **Status:** pending review

## 2026-07-07 ~23:0x UTC — Filed Tess's playmode changed-behavior note as investigation ticket 86cama53u (auto-decide)

- **Decided:** created S-M investigation ticket `86cama53u` (advisory playmode job now COMPLETES with 10 real failures on main — was documented as hangs-at-enter) verbatim from Tess's #279 QA note N3 + orchestrator note (comment 4909506137; main run 28817657531 cited). Triage-only scope; advisory→required flip explicitly OOS (sponsor ci.yml call).
- **Foundation:** rule-6 mechanical follow-up class (scope verbatim from a QA note with run-ID evidence) + [[scan-board-before-filing-followups]] dedupe (no existing ticket covers the 10-failure set; 86cajt6k4 covers only the 13 quarantined INVUI tests).
- **Alternative:** leave the changed CI behavior undocumented until it surprises a merge gate.
- **Reversibility:** close the ticket; 1 click.
- **Status:** pending review

## 2026-07-08 ~09:0x UTC — Filed PR #287 NIT (mine-tests clock inheritance) as ticket 86camf3xe (auto-decide, pre-cleared class)

- **Decided:** created S ticket `86camf3xe` (apply #288's TestClock pattern to MineOrePlayModeTests) verbatim from Devon's APPROVE_WITH_NITS comment 4912697997 on PR #287; sequenced after #287+#288 merge.
- **Foundation:** rule-6 NITs-ticket class — reviewer-enumerated, XML-evidenced (12-vs-10 delta), scope mechanical (apply an existing merged pattern).
- **Reversibility:** close the ticket; 1 click.
- **Status:** pending review
