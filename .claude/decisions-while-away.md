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
