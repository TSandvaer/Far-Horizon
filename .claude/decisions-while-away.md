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
- **Status:** pending review

## 2026-06-15 1320 UTC — Split nudge-tool keys (WorldLookNudgeTool → F10) before serving the combined soak

- **Decided:** Before serving the Sponsor the combined soak (#48 `de97ba4`), dispatch Drew for a small key-split — `WorldLookNudgeTool` toggle F9→F10 (+ scope each tool's Tab/PageUp/PageDown to its OWN active panel) — so F9 = character dials (arm/axe/GROUND-Y) and F10 = world-look dials (sky/fog/clouds/mountains). Then rebuild + serve.
- **Foundation:** Drew's own flagged follow-up in the reconcile report (both nudge tools default `KeyCode.F9` + share Tab/PageUp/PageDown → collide when both active in the combined build) + the dial-philosophy memory [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]] — this soak's W2 mountain-warmth resolution is explicitly "Sponsor dials warmth via F9", so a broken dial would hand him a broken soak instruction (violates the [[soak-handoff-path-and-explicit-test-checklist]] bar).
- **Alternative:** Serve `de97ba4` now (look works; dialing collides) and split keys only if/when he tries to dial. Rejected: the W2 resolution depends on dialing → near-certain serve→fix→re-serve cycle; one short fix now yields a fully-working soak.
- **Reversibility:** one-file KeyCode change on #48, revert in 1 PR.
- **Status:** pending review

## 2026-06-22 1944 UTC — Armed auto-status AWAY + dispatched Priya for board hygiene
- **Decided:** Armed away-mode (cron `051f1771`, 15-min, session-only) on Sponsor's `/auto-status away`. First tick: dispatched Priya (`a885a92e35f4aa548`) for ClickUp board hygiene — reconcile statuses + file 2 follow-up tickets (devon-wt worktree-churn build block; 2 pre-existing EditMode fails HungerNeed.startFull / InventoryUI.themeStyleSheet) + flesh gameplay-wave ACs. Did NOT un-hold the sponsor-held gameplay wave (priority is the Sponsor's call). Devon `a555382c5819efa7d` already in flight on the #100 uniform axe-head resize.
- **Foundation:** auto-status away skill (STEP 4 → dispatch PL persona for hygiene) + CLAUDE.md "Priya owns the board".
- **Alternative:** surfacing would have asked the Sponsor whether to dispatch Priya / whether to start gameplay-wave prep.
- **Reversibility:** CronDelete `051f1771` stops the loop; Priya's ClickUp edits revert in ≤1 pass; the 2 filed tickets close if unwanted.
- **Status:** pending review

## 2026-06-22 ~2000 UTC — Dispatched Drew on the worktree-churn devx fix (86cacvhc6)
- **Decided:** dispatched Drew (`a3cd5826f45280676`) on `86cacvhc6` (stop the recurring persona-worktree churn build-block — classify transient bootstrap output → gitignore vs commit real assets). Routed to DREW not Devon to keep Devon's worktree FREE for the imminent #100 axe-head bake on the Sponsor's soak-return (worktree-concurrency + #100 priority). Did NOT auto-dispatch: the gameplay wave (sponsor priority-HELD even though its deps #83/#90/#93 are now on main — release is the Sponsor's call) nor `86cacvhf4` (EditMode-fails — deferred: build-slot + verify-real-on-main wrinkles, lower leverage).
- **Foundation:** away-mode STEP 2 (fill dispatchable slots) + the churn friction documented all session (forced push-and-CI every soak iteration) + Priya's filed ticket `86cacvhc6`.
- **Alternative:** surfacing would've asked whether to start the churn fix / release the gameplay wave / which follow-up first.
- **Reversibility:** revert Drew's PR (config/gitignore, ≤1 PR); not merged to main (away stages only).
- **Status:** pending review

## 2026-06-22 ~2131 UTC — Cancelled #118's wedged advisory playmode CI run
- **Decided:** `gh run cancel 27984975834` — #118's advisory playmode job hung ~30min (past its ~15min timeout = wedged), holding the single build slot. unity+structure already SUCCESS, so #118 stays green per-job; cancel frees the slot for the #100 bake on Sponsor-return.
- **Foundation:** [[advisory-playmode-job-unreliable-soak-is-interaction-gate]] (playmode hangs + holds the lock; advisory/non-blocking) — pre-cleared CI-hygiene auto-decide class.
- **Reversibility:** re-run the CI anytime; no code/state change.
- **Status:** pending review

## 2026-06-22 ~2153 UTC — Dispatched Drew on 86cacvhf4 (EditMode-fails verify+fix)
- **Decided:** dispatched Drew (`a1ca7aad63dfbf19a`) on `86cacvhf4` — verify `HungerNeed.startFull` + `InventoryUI.themeStyleSheet` on a clean-main checkout, fix the real ones. Build slot freed (#118's wedged playmode finally cleared `completed/cancelled`). Dependency-aware brief: InventoryUI half is LIKELY the untracked-theme that #118 already fixes (Drew confirms, does NOT duplicate); HungerNeed half is independent. Stopped deferring after 3 quiet ticks — slot free + away-mode fill mandate; Devon kept reserved for the #100 bake so routed to Drew.
- **Foundation:** away-mode STEP 2 (fill slots) + Devon-flagged failures + Priya's ticket `86cacvhf4` + the testing bar (main shouldn't carry red EditMode).
- **Alternative:** keep deferring until #118 merges (cleaner, but idles Drew indefinitely while the Sponsor's away).
- **Reversibility:** revert Drew's PR, or close the ticket as not-repro.
- **Status:** pending review

## 2026-06-23 ~0710 UTC — Dispatched Drew on 86cacyg63 (bootstrap-hint test guardrail)
- **Decided:** dispatched Drew on `86cacyg63` (Sponsor-APPROVED via the walkthrough this session) — add a no-bootstrap precondition guard to the ~5 scene-presence EditMode tests so a bare local run reports `Assert.Inconclusive` with a "run BootstrapProject.Run first" hint instead of opaque null-red. Routed to DREW (he triaged 86cacvhf4 + recommended this guardrail; has the context); Devon stays FREE for the #100 bake. #118 merged → clean main-based tree for Drew (no churn).
- **Foundation:** Sponsor approved the ticket via `/sponsor-questions-walkthrough` (86cacyg63) + Drew's own recommendation + away-mode STEP 2 fill.
- **Reversibility:** revert Drew's PR (test-only change).
- **Status:** pending review

## 2026-06-25 0912 UTC — Cancelled wedged stale CI run 28157969953 to free the self-hosted runner

- **Decided:** `gh run cancel 28157969953` (in-progress CI on `ad28c5a`, a SUPERSEDED round-6 commit) — its hung advisory-playmode job was pinning the single self-hosted runner, blocking the HEAD run `28158834315` (`af584b8`) from scheduling its unity job.
- **Foundation:** [[advisory-playmode-job-unreliable-soak-is-interaction-gate]] (the playmode job hangs + holds the runner) + the run was for a SUPERSEDED commit (af584b8 supersedes ad28c5a) so its result is not needed; the HEAD run is what the pond gate depends on.
- **Alternative:** wait for the hung job to self-timeout (~past 20 min) — rejected: slower, and the stale run's result is irrelevant.
- **Reversibility:** fully reversible — CI runs are re-runnable; cancelling a superseded run loses nothing (HEAD run 28158834315 carries the real result).
- **Status:** pending review

## 2026-06-25 0912 UTC — Dispatched Devon round-7 (pond sea-plane hole under-covers the organic footprint)

- **Decided:** Dispatch Devon (background, devon-wt) round-7 to enlarge the sea-plane hole to fully clear the organic pond footprint past the waterline, after round-6 CI's (now-genuine) gate FAILED on SHORELINE-ANNULUS (`topNoShorelineRing=False`, shoreline pale 0.182 @ rNorm 0.30).
- **Foundation:** away-mode STEP 2 (fill the dispatchable slot — pond is the sponsor-priority critical path) + machine-checkable gate evidence (post-gate verify-pond.log); the sea-plane root cause is confirmed (sea-off DIAG → 0.000). A targeted continuation, not a subjective call.
- **Alternative:** relax the annulus threshold to pass 0.182 — rejected: 0.182 is a visible pale crescent (orch eyeballed pond_top/pond_a); the Sponsor would soak-reject it (this saga IS the ring).
- **Reversibility:** code iteration on the open PR #130 branch; revertable.
- **Status:** pending review

## 2026-06-25 1521 UTC — Re-armed AWAY + dispatched the gameplay wave (CHOP) + #139 review + hygiene

- **Decided:** On the Sponsor's `/auto-status away` (clicked in the resume popup), armed away mode (cron `948797d0`, 15-min) and on the first tick dispatched 3 background agents: Devon (`af7fac927aeace68d`) on CHOP `86caa4c5c` (the wave's build-slot task), Drew (`afffce75a8c415321`) to review PR #139, Priya (`ad70c00c83eac199e`) for board hygiene. Deferred sticks/stones (build-slot serialized behind CHOP; also serialized with each other — both write `ScatterIslandProps`, confirmed by Priya).
- **Foundation:** away-mode skill STEP 2 (fill every dispatchable slot) + the single-Unity-build-slot cap ([[single-unity-build-slot-serializes-orchestration]]) + STATE.md "Next steps" (chop→sticks→stones). Sponsor explicitly chose away mode.
- **Alternative:** present-mode (surface decisions live) — not chosen; Sponsor clicked away.
- **Reversibility:** CronDelete `948797d0` stops the loop; the dispatches are reversible (PRs revert in ≤1).
- **Status:** pending review

## 2026-06-25 1521 UTC — Re-dispatched CHOP as a WIP-PRESERVING resume (not fresh) after a deny saved the WIP

- **Decided:** My first CHOP dispatch (a "recreate fresh" Step 0 with `git reset --hard` + `git branch -D`) was deny-blocked by the auto-mode classifier. On inspection devon-wt held UNCOMMITTED chop WIP from the prior stopped dispatch (modified `CastawayArmPose.cs`/`ChopTree.cs`/`SettingsCatalog.cs` + new `ChopPoseDriver.cs`). Re-dispatched Devon with a NON-destructive resume Step 0 (fetch + status only) that preserves and builds on the WIP. Did NOT retry the denied command (per never-spiral).
- **Foundation:** [[background-agent-rate-limit-death-salvage]] (preserve a stopped agent's uncommitted WIP; never clean/reset) + the deny guardrail working as designed. The fresh-recreate would have wiped real work.
- **Alternative:** re-dispatch fresh (lose the WIP) — rejected once the WIP was found.
- **Reversibility:** the WIP is on-disk in devon-wt; resume PR reverts in ≤1.
- **Status:** pending review

## 2026-06-25 1521 UTC — Deferred the CI playmode-shorten (86cae8vn2) this session

- **Decided:** Did NOT dispatch the `86cae8vn2` ci.yml playmode-timeout shorten (20→5) this tick despite free dev capacity (Drew/Priya idle after their tasks).
- **Foundation:** [[classifier-blocks-merge-to-protected-main]] (away mode can't merge it → no protective payoff until the Sponsor merges) + [[single-unity-build-slot-serializes-orchestration]] (a 2nd CI-triggering PR contends with CHOP's CI rounds on the single self-hosted runner; keeping the runner clear for the priority CHOP work). Idle-by-constraint, not neglect.
- **Alternative:** dispatch it now so a stageable PR is ready on the Sponsor's return — deferred because the contention cost outweighs the marginal staging benefit; revisit when CHOP's CI settles.
- **Reversibility:** trivially dispatchable on any later tick; no state changed.
- **Status:** pending review

## 2026-06-26 1436 UTC — Switched to AWAY + dispatched the Sponsor-authorized cleanup-backlog burndown
- **Decided:** On Sponsor's `/auto-status away`, armed away-mode (cron `dc375429`, 15-min; killed present cron `7af0e18a`). Executed his AskUserQuestion-authorized "burn down the cleanup backlog": dispatched Priya (`af15146e9352471c3`) on `86cae5vpt` (asset-routing docs index — now PR #143) + Drew (`a7337c0f21a913029`) on a bundled PR for `86cabfa32` (gate AxePickup Debug.Log) + `86caan3aj` (clamp-guard PlayMode test). HELD `86cabnjv8` (bush NITs — overlaps #140's `LowPolyZoneGen.cs` → sequence after #140 merges) + `86cab8u19` (test rename — queued behind the single build slot). Staged to away-queue: the Mixamo-clip ask (gates chop change-(b)) + the cursor-lock `86caatv7k` soak-confirm.
- **Foundation:** Sponsor's popup answer "Burn down the cleanup backlog (Recommended)" (this session) for the directive; `[[single-unity-build-slot-serializes-orchestration]]` + `[[no-gating-without-dependency]]` for the collision-aware ticket selection / bundling / holds; away-skill STEP 2.
- **Alternative:** hold the team quiet until the Mixamo clip lands (the popup's other option) — Sponsor explicitly chose the burndown.
- **Reversibility:** CronDelete `dc375429` stops the loop; the 2 burndown PRs revert in ≤1 PR each (or close if unwanted); ticket statuses revert.
- **Status:** pending review
- **NB:** the earlier same-session change-(a) audit + gate-hardening re-dispatch + cursor-lock NO-OP handling were PRESENT-mode (Sponsor-aware) actions — not away decisions.

## 2026-06-27 1540 UTC — E-loot #147 gating + belt-eligibility follow-up filed
- **Decided:** (1) dispatched Drew (`ad802aaa9761f5831`) to code-review PR #147 (E-loot foundation) in parallel with its in-progress CI; (2) flipped `86caf7a6q` → in review; (3) filed new ticket `86caf7g6f` (belt-eligible non-tool consumables — water+berries) as the prerequisite Devon surfaced for the left-click-consume ticket `86caf7a30`.
- **Foundation:** review-routing is routine peer-review ([[tess-cant-self-qa-peer-review]] + auto-execute-classes); review-in-parallel-with-CI is the /whip false-gate rule (review ≠ merge gate); ticket-fleshout/creation is autonomous per [[clickup-task-management-full-autonomy]]; the belt-eligibility need is Devon's PR #147 final-report recommendation (belt is Tool-only; water/berry consumables need a belt-eligible item kind).
- **Alternative:** wait for #147 CI green before reviewing (slower); not file the dependency ticket (loses the tracked prerequisite).
- **Reversibility:** trivial — close `86caf7g6f` / re-flip status; review is read-only.
- **Status:** pending review.

## 2026-06-27 1610 UTC — #147 STAGED (all gates green); hold-to-chop dispatched
- **Decided:** (1) Tess QA PASS on PR #147 → ALL machine gates green (CI unity+structure SUCCESS + Drew APPROVE + Devon Self-Test + Tess QA PASS) → STAGED #147 in away-queue with the soak build (`Build/soak-147/FarHorizon.exe`, stamp `effcbea`) + the one-click `gh pr edit 147 --add-label auto-merge` command. Did NOT auto-merge (UX-visible + merge-to-main sponsor-gated). (2) Build slot freed (#147's advisory playmode cancelled) → dispatched Devon (`a25db0828505e4eb7`) on hold-to-chop `86caf7a0p` (Sponsor wish #1, independent of #147, off main).
- **Foundation:** away-mode STAGE-don't-merge rule ([[classifier-blocks-merge-to-protected-main]]); UX-visible PRs are sponsor-soak-gated ([[sponsor-merge-approval-soak-or-complete]]); hold-to-chop is independent of E-loot ([[no-gating-without-dependency]]) + the Sponsor's explicit wish; idle-capacity-is-a-bug (fill the freed slot).
- **Alternative:** auto-merge #147 (blocked by policy/classifier); hold hold-to-chop until #147 merges (idles the slot).
- **Reversibility:** trivial — don't add the label / close the branch; hold-to-chop is a fresh branch.
- **Status:** pending review.

## 2026-06-27 1634 UTC — #148 STAGED (all gates green + bounded-claim); team idle pending #147 merge
- **Decided:** Tess QA PASS on PR #148 (hold-to-chop) → ALL machine gates green → STAGED #148 in away-queue (soak `Build/soak-148/FarHorizon.exe` stamp `c384e96` + one-click `gh pr edit 148 --add-label auto-merge`). Re-engaged Devon to add the Predict-Before-Soak bounded-convergence claim to the #148 Self-Test (Tess's pre-soak item per TESTING_BAR; PR comment 4819166295, no code change). Filed NITs follow-up `86caf7ne0` (chopInterval comment-vs-0.25f doc reconcile, post-merge). Did NOT auto-merge (UX-visible + sponsor-gated). Team now correctly IDLE: both input-model PRs staged; all downstream (belt-eligibility, consume, wave, bush-NITs) gates on #147 merging (Sponsor).
- **Foundation:** away-mode STAGE-don't-merge ([[classifier-blocks-merge-to-protected-main]]); UX-visible sponsor-soak-gated ([[sponsor-merge-approval-soak-or-complete]]); bounded-claim is a TESTING_BAR Predict-Before-Soak requirement Tess flagged; NITs-creation autonomous ([[clickup-task-management-full-autonomy]] / auto-execute-classes); go-quiet-when-all-sponsor-blocked (away STEP 5).
- **Alternative:** auto-merge (blocked); skip the bounded-claim (violates the bar); keep dispatching (nothing dispatchable — all gates on #147 merge).
- **Reversibility:** trivial — don't add labels / close NITs ticket.
- **Status:** pending review.

## 2026-06-27 1638 UTC — slot-fill: pond-NIT regression tests dispatched (non-gated)
- **Decided:** with both input-model PRs (#147/#148) staged + all their downstream gated on the Sponsor merging #147, dispatched Tess (`adecb100eefb1ab51`) on the non-gated pond-NIT regression tests `86cadr95t` (#130 pond test-only NITs) to fill the idle Unity build slot. Flipped `86cadr95t` → in progress.
- **Foundation:** idle-capacity-is-a-bug + the legitimate-idle bar (a slot stays idle ONLY if all candidate work is gated/sponsor-blocked — pond-NIT tests are NEITHER, so the slot must not idle); test-only + independent of the staged PRs (no collision); within away-mode autonomy (reversible, safe test class, not on the never-auto-decide list).
- **Alternative:** go quiet (would have left a free slot with dispatchable non-gated work — violates the bar); dispatch the water-mat cleanup `86cacer85` instead (lower value — the .mat is moot-at-runtime/unused-asset per its ticket) or the 2nd pond test `86cadnepd` (same files → serialize after this one).
- **Reversibility:** trivial — drop the branch / re-open the ticket.
- **Status:** pending review.

## 2026-06-27 1700 UTC — earned-autonomy handoff resolved (PR #151) + pond-NIT #150 review routed
- **Decided:** (1) Per the Sponsor's handoff (`team/orchestrator/earned-autonomy-handoff.md`), CAPTURED the at-risk uncommitted earned-autonomy edits — backed up to scratchpad (2 patches) THEN durably folded into NEW **PR #151** (off clean main `aab203d` via `git apply --3way` in a fresh `Far-Horizon-config-wt` worktree, so orch/coordination's dirty tree was NEVER touched — zero clobber risk). #151 = priya/tess/CLAUDE/dispatch-template + new name-the-bar SKILL + decisions-log self-audit block. Did NOT touch GIT_PROTOCOL.md (in #149; would conflict). (2) Tess's pond-NIT PR #150 came back CI-green → routed Drew `ad8b3cc0aaa24bf8b` to peer-review (Tess-authored); `86cadr95t` → in review.
- **Foundation:** Sponsor's explicit instruction (capture before any checkout/reset); the handoff doc's fold-in map; ground-truth-verified (file exists, PR #149 real, working-tree edits present, orch/coordination confirmed ~20k lines stale vs main → built off clean main not the stale branch); review-routing per tess-cant-self-qa-peer-review.
- **Alternative:** build the config PR off orch/coordination (rejected — stale branch); do a risky checkout/reset on orch/coordination (rejected — Sponsor flagged it + unnecessary, used a separate worktree); not build #151, just leave the scratchpad backups (weaker — temp dir, not durable).
- **Reversibility:** trivial — close #151 / drop the branch; orch/coordination tree untouched.
- **Status:** pending review (esp. #151 = YOUR config — CLAUDE.md + persona changes; review yourself or have Priya peer-review).

## 2026-06-27 1705 UTC — #150 STAGED (Drew APPROVE); slot legitimately idle
- **Decided:** Drew APPROVE on PR #150 (pond-NIT tests, test-only, CI green) → STAGED for one-click merge (no soak — test-only). Did NOT dispatch a further slot-fill: the 2nd pond test `86cadnepd` overlaps #150's `FreshwaterPondSceneTests` (sequence after #150 merges); water-mat `86cacer85` is moot-at-runtime "fold into the next water touch" (defer-by-design). So the slot idles for a cited reason, not by oversight.
- **Foundation:** test-only PR gate = CI + peer review (no soak/QA-persona needed for a Tess-authored test PR Drew reviewed); legitimate-idle bar (remaining non-gated work overlaps a staged PR or is defer-by-design); STAGE-don't-merge ([[classifier-blocks-merge-to-protected-main]]).
- **Alternative:** dispatch 86cadnepd now (rejected — file-overlap with staged #150) or water-mat (rejected — disproportionate for an XS moot-at-runtime cleanup the ticket says to fold into a future water touch).
- **Reversibility:** trivial.
- **Status:** pending review. 4 PRs now staged for the Sponsor: #147, #148, #150, #151.

## 2026-06-27 2125 UTC — Stage #148 hold-chop for re-soak as-is (flag N1), don't pre-fix the NIT in the Sponsor's absence
- **Decided:** On Drew's APPROVE_WITH_NITS of #148, stage the PR for the Sponsor's re-soak as-is (after Tess QA) with N1 (the re-press-after-fell dead-window) FLAGGED in the soak checklist + filed as NIT ticket `86caf9ngh` — rather than dispatching Devon to pre-fix N1 before the soak.
- **Foundation:** Drew (peer reviewer) judged N1+N2 non-blocking/follow-up (comment 4821800616); the chop code is a proven-finicky surface (#140 took 5 soak iters) so re-iterating it in the Sponsor's absence risks a new regression they'd then have to soak-catch; bundling any N1 fix with the Sponsor's live re-soak feedback (which may include other cadence asks) is one round-trip vs two. [[soak-fail-test-pass-instrument-runtime]] caution on over-iterating feel code blind.
- **Alternative:** Dispatch Devon now to fix N1+N2 before staging → cleaner re-soak, but +1 Devon build cycle, delays the wave, and risks a speculative regression on a reviewer-judged-non-blocking edge case.
- **Reversibility:** ≤1 dispatch — if the Sponsor (or I) prefer pre-fixing, dispatch Devon on `86caf9ngh` before serving the soak.
- **Status:** pending review

## 2026-06-28 1424 UTC — Dispatched Tess on hardening 86cafdevx (away-mode slot-fill after input-model wave merged)
- **Decided:** Dispatch Tess (background, tess-wt, branch off main 8828c48) on the Boot.unity/capture-gate hardening ticket 86cafdevx (EditMode wiring-guards + deterministic verify-capture harnesses + bootstrap null-check LogError). Reviewer: Devon (non-author dev). Flip 86cafdevx -> in progress.
- **Foundation:** away-queue.md "GATED on #162 MERGING" section listed 86cafdevx as dispatchable once #162 merged (now merged this turn, main 8828c48); ticket 86cafdevx carries full ACs and is explicitly "no Sponsor soak (test-infra)" -> non-sponsor-gated. Away-mode "idle capacity is a bug" + STEP 2 fill-every-slot. Landing it first also de-risks tree-chop's CI (shares the capture gates).
- **Alternative:** Hold the single Unity build slot idle for the sponsor-gated tree-chop greenlight. Rejected -- tree-chop needs a Sponsor priority greenlight (staged in away-queue), so holding the slot idles it on a maybe; hardening is non-gated and sequenced cleanly before tree-chop.
- **Reversibility:** test-infra/guards only, revert in <=1 PR; CronDelete stops the loop; ticket returns to "to do" if unwanted.
- **Status:** pending review
