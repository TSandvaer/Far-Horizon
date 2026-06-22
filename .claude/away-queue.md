# Away-queue — Far Horizon

Sponsor sign-off items the orchestrator does NOT decide. Drain on return via `/sponsor-questions-walkthrough`.

---

## AWAY RUN armed 2026-06-19 ~2150Z (auto-status AWAY, cron `777e4523`, 15-min)

keep-screens-alive state file = `enabled` (pid 36316, since 2026-06-18) — couldn't live-verify the process (PowerShell deny-listed via Bash); if this is an extended/overnight away, glance the display isn't sleeping (a slept display suspends the local loop).

### 🔴 RUNNER BLOCKER (NEW ~2200Z — blocks the ENTIRE Unity build lane)
The self-hosted runner's `Library/PackageCache` is **EPERM hard-wedged**. Evidence: #101 re-validation (run `27849432912`) bootstrap hit `EPERM-rename` on **all 3 retry attempts** then gave up — log: *"EPERM persisted through 3 attempts — giving up (runner cache may be hard-wedged; manual Library/PackageCache delete needed)."* The OLD bootstrap (on #101's branch, pre-#103-fix) exits 0 anyway (silent-green) → scene not re-baked → 2 stale-scene EditMode tests failed (`BootScene_CarriesHungerNeed_Serialized`, `BootScene_CarriesInventoryUI_Wired...`). **This is a runner-env issue, NOT a code bug.**
- **Your action:** on the runner host (`C:\actions-runner-farhorizon`), delete the wedged `Library/PackageCache` for the Far-Horizon work dir (regenerable) + restart `run.cmd` if needed. (The orchestrator can't: recursive delete is deny-listed + it's the runner host.)
- **Impact while wedged:** ALL Unity builds EPERM → #101 re-validation, #100 held-scale build, and any new build-bound work are blocked. Non-build work (docs/review/spec) still runs.
- **Durable fix:** merging #103 (below) makes future EPERM bootstraps fail RED honestly (not silent-green); the slot-unblock spike (`86cabkhjg`, `BEE_CACHE_DIRECTORY` isolation) is the structural fix. The wedge itself still needs the manual cache delete now.

### 🟡 RUNNER FIX — REFUTED (~0830Z): the play-mode-enter hang is NOT the Android module
Erik HYPOTHESIZED the hang = the Android module's ADB/USB device-scan, fixable by removing the module. **Refuted by ground truth: NO AndroidPlayer module is installed on EITHER editor** (`6000.4.11f1` and `6000.4.10f1` PlaybackEngines each have only `windowsstandalonesupport`), yet the hang occurs on both. So "Scanning for USB devices" is a general Unity headless input/USB enumeration, NOT Android. **Nothing to remove** — the Hub "Add modules" dialog showed Android unchecked because it's available-to-ADD, not installed.
- The play-mode-enter hang root-cause is **UNRESOLVED**. It's ADVISORY/non-blocking (job timeout caps it ~18min; PlayMode is `continue-on-error`), so it does NOT block builds or merges.
- Possible real fix (untried): drop `-nographics` from the PlayMode step in `ci.yml` (Erik's moderate-evidence Option 2), or a headless input-system workaround. Investigate only if interaction-test CI coverage becomes worth it.
- Note: `team/erik-consult/playmode-enter-headless-deadlock-research.md` (the Android root-cause is refuted; harvest with a correction).
> ⚠ Second refuted Erik playmode hypothesis (after UUM-142421). Both were web-research-based, not verified against the actual machine. Lesson: verify the editor install before acting on a hang root-cause.

### ⭐ #2 — #103 Unity 6000.4.11f1 upgrade — FULLY GATED, one-click ready (value RE-SCOPED — see ⚠)
**All gates GREEN:** required CI (run `27847884304`: structure + unity build/capture SUCCESS — upgrade VALIDATED) + **Devon peer review APPROVE** (gate b) + **codereview workflow APPROVE, zero findings** (gate a, `wf_b404a62a-616`). Infra PR → not UX-visible → "complete", no soak:
```
gh pr merge 103 --admin --squash --delete-branch
```
> **⚠ VALUE CORRECTION (evidence ~2219Z — supersedes my earlier "kills the hang" claim):** the upgrade does NOT fix the advisory play-mode-ENTER hang. #103's OWN playmode job (run `27847884304`, on 6000.4.11f1): bootstrap COMPLETED + re-baked Boot.unity fine, but PlayMode then hung on play-mode-ENTER ~18 min → killed by the job timeout (env-deadlock; the CI note tracks it as `86caapwmt`). So Erik's "the ~20min hang = UUM-142421, fixed in 6000.4.11f1" hypothesis is **REFUTED by this run** — the hang persists.
> **#103's REAL value:** (a) Drew's bootstrap-robustness fix (silent-green → fail-RED honest failures — this is genuinely good, it's why the #101 EPERM now surfaces instead of silently passing a stale scene) + (b) version currency. NOT a hang fix. Still worth merging (required gates green, harmless, trivially reverted), but **the runner bottleneck is NOT solved by it.** Two live runner issues remain: the EPERM cache wedge (🔴 above) + this play-mode-enter deadlock (`86caapwmt`, advisory + timeout-mitigated). Durable fix = the slot-unblock spike (`86cabkhjg`, BEE_CACHE_DIRECTORY). **Your call** whether the bootstrap-fix + version-currency value alone justifies merging now (recommend yes — it's net-positive and the honest-failure behavior already paid off catching the #101 EPERM).

### ✅ #1 — #102 inventory drag-source-dim — COMPLETE (you already chose "complete"; one-click)
All gates green: codereview **APPROVE_WITH_NITS** (fix sound; findings are test-coverage only) + Drew peer APPROVE + required CI green. You picked **complete** in the walkthrough before stepping away — just run it:
```
gh pr merge 102 --admin --squash --delete-branch
```
Test-hardening NITs ticket already filed: `86cabugc3`.

### 🌿 #3 — #101 berry bushes — fix is SOUND but CI-BLOCKED on the EPERM wedge (🔴 above)
Devon's codereview fix-pass (`804f936`) is correct: harvestRadius now scales with bush size, AC2/AC3 tests added, Debug.Logs stripped, lookups cached — and his logic tests all pass. Its CI failure (run `27849432912`) is purely the runner EPERM wedge (🔴 above), not the code. **On the cache-clear → re-run #101 CI → green → Tess QA → your soak-or-complete** (UX-visible; recommend a quick soak). Distance-cull deferred to perf ticket `86cabuhyw`.

### 🪓 #4 — #100 weapon set — your SOAK pending (held-scale dial + axe-head)
Family ACCEPTED. Remaining before merge: (a) **held-scale re-soak** — the live size-dial build (`]`/`[` to dial each weapon in-hand by eye → read the value to bake); build BLOCKED on the EPERM wedge. (b) **axe head ~20% smaller** — a Blender-MCP re-author (orchestrator R&D-lane), sequenced AFTER #103 merges. (c) **CC-BY axe `git rm`** — one-click in the PR #100 body (destructive hook blocks the agent). (d) codereview + Tess QA. Then soak-or-complete.

### 🔐 #5 — #82 / #83 rebases — FORCE-PUSH gated (your action)
Both need a rebase onto current main; the required `git push --force-with-lease` is deny-listed, and the classifier blocks delegating it in away mode (`autonomous-rebase-blocked-by-force-push-deny`). Either you run the rebase+force-push, or authorize lifting the deny for the one branch.
- **#82** gameplay-wave scope (Uma APPROVE_WITH_NITS; one cosmetic word-fix to fold in) — also **un-gates the gameplay wave** (thirst/chop/stones/sticks) once merged.
- **#83** settings engine (you soaked + approved the engine; Tess re-QA held until the rebase). Unblocks the dev-tweak-console foundation `86cabeqj9`.

### 🧭 #6 — Strategic / infra (your call — never auto-decided) [carried]
- **POC R&D kickoff** — boat `86caa9zju` + big-island gen `86caa9zpp` (and/or snake-POC `86caaz4vn`); gate = `/grill-me` design-lock first.
- **Slot-unblock spikes** `86cabkhjg` (concurrent-build cache-isolation; needs `BEE_CACHE_DIRECTORY` — ALSO the durable fix for the EPERM-wedge class above) + `86cabkhqn` (build-hold-time) — owner Drew; sponsor-gate on the BAKE.
- **Runner → install as a Windows service** (or `/runner-autostart` for reboot-survival) — offered, not done.
- **IMGUI→UI-Toolkit HUD migration** (unify the IMGUI HUD with the UI-Toolkit settings panel).
- **`block-destructive-bash.sh` standalone-leading-`rm` gap** (`86cabe3gx`) — fix offered, not go'd.

---

## Notes for the away loop
- **Build lane is WEDGED (EPERM)** — do NOT dispatch build-bound work until the sponsor clears the runner cache. Non-build work (docs/review/spec/research) only.
- **Do NOT auto-merge to main** (classifier blocks it even under bypass). Stage one-click commands here.
- **Tess QA on #101** dispatches once its CI greens (after the cache-clear) — gated, not idle-capacity.
- **Gameplay wave** is gated on #103 merge + #82 rebase — not dispatchable until both land.
