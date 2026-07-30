# Away-queue — Far Horizon

## ▶▶▶ CURRENT — 2026-07-30 ~21:25Z (AWAY armed, cron `471609c0`, 15-min; main `51f4623`)

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)

0. **⭐ ONE-CLICK MERGE — PR #369 (`86caynve9`, CI-wire `-verifySwings`), head `81b71a2`.** Say the word and I run it; it touches `.github/**` so it cannot be label-merged, and unlike #365 I have no specific approval for it.
   ```
   gh pr merge 369 --admin --squash --delete-branch
   ```
   **Gate evidence — the strongest-evidenced PR of the night:**
   - **All FOUR jobs SUCCESS** on run `30589555914` — `structure`, `build`, `capture` AND `playmode`. Capture log confirms `SWINGS CAPTURE GATE PASSED`, all six needles `evidence OK` **on the shipped exe**.
   - **Gate suite 188 passed / 0 failed**, and Devon did NOT treat his clean merge as proof: he mutation-tested the wiring twice — de-registering the gate → `185p/1f`, unwiring the `ci.yml` invocation → `186p/1f`, both restored. That directly answers the #351 trap where a clean merge left a gate registered nowhere.
   - **What it fixes:** `-verifySwings` had **zero** occurrences in `ci.yml`, so the two-hand palm gate and #354's arm-release term had only ever been proven by an author running the exe by hand.
   - **Drew's NIT folded in and NARROWED:** `castaway == null` was already fatal via `allRouted` (`:164`), so the real false-green was specifically `animator == null`. Devon added the missing `else` emitting the existing `fold pass SKIPPED` needle (inert on green), a header sentence marking the four in-block needles LOAD-BEARING, and **S4b** — a fixture for the silent-guard shape, mutation-proven (pruning those four needles yields `rc=0` on a run that measured nothing, `185p/3f`).
   - ⚠ **Merge-order:** its diff enters `ci.yml`'s stale-clear region (adds `verify-swings.log` + `swings-caps`). Verified still `MERGEABLE/CLEAN` against post-#365 `main` (`e054aa7`), `merge-tree` clean. **#370 collides in that same region** — whichever lands second needs an **additive union** of the log/caps names, never take-one-side.
   - ✅ **Drew re-reviewed the delta: APPROVE** (comment 5137338621). He confirmed the hole is genuinely CLOSED rather than merely documented — the new `else` (`SwingVerifyCapture.cs:541`, warning `:554`) emits the *existing* `fold pass SKIPPED` ABSENT needle → `evidence_rc=1` → gate `exit 1`; S4b independently reds the pre-fix silent shape via the in-block needles alone. All 12 re-shifted line citations verified exact.
   - **He measured the gate suite himself on three trees:** `origin/main` @ `e054aa7` = **186/0**; PR head `81b71a2` = **188/0** (matches Devon); **PR head merged with current main = 205/0**, zero `[FAIL]`, all three #351-class wiring assertions green.
   - ⚠ **The one qualification on this merge, stated by Drew not extracted from him:** #369's **CI** green predates 2 main commits that touch the same files, so the 205/0 against current main is **author-measured, not CI-corroborated**. If you want a CI-verified green on the current base before merging, the fix is a `git merge origin/main` + push on #369 (one more cycle). Merging on Drew's measurement is defensible; merging while *believing* CI covered the current base would not be.
   - **Two NITs from his review are ticketed, not folded:** `86caz5jxp` (needle mis-attribution) and `86caz5jxq` (no scene-presence test for `SwingVerifyCapture` / `MineVerifyCapture` / `WeaponSetVerifyCapture`).
     - ⚠ *Orchestrator note: this line first carried two FABRICATED ids (`86caz5b8k` / `86caz5b9x`) — written in the same message that created the tickets, i.e. before their real ids existed. Corrected to the real ids from the create responses. The two tickets' own `RELATED:` cross-references were fabricated the same way and have been fixed by comment.*

1. **#351 SOAK — STAGED AND PLAYED. Run this:**
   ```
   C:\Trunk\PRIVATE\Far-Horizon\Build\soak-351\FarHorizon.exe
   ```
   **HUD stamp must read exactly:** `BUILD zoned | 2026-07-30T22:19:59Z | d9b88cd` — `d9b88cd` **independently verified** by the orchestrator as `refs/pull/351/merge`, so it is the merge sha, not the branch sha, and the string is correct as printed. (The stamp *string* itself rests on Drew's HUD + `resources.assets` read; the orchestrator's `strings` extraction returned nothing on the packed assets, same as on soak-366.)
   **Full 9-step checklist is posted on the PR:** https://github.com/TSandvaer/Far-Horizon/pull/351#issuecomment-5137194461
   - **Getting there:** at spawn just **hold `S` for ~4 s**. No hunting. Then right-mouse-drag to lower the camera — from directly overhead your own character hides the stump.
   - **The real question is step 3, not "does it work":** does the sword-in-stump read as *special* on bob+sway alone, without becoming a loot beacon? Drew's honest finding: **at default overhead framing it did NOT draw his eye** — the prompt told him he'd arrived; it only read clearly once the camera dropped. That is the bar this soak exists to settle.
   - **The Fresnel rim the ticket asked for was CUT and is genuinely unreachable** — the whole weapon set shares one URP/**Unlit** material with no rim property; a rim means forking it and breaking the one-material invariant. So if bob+sway is too quiet, that is a finding about the material model, not a dev tweak.
   - ⚠ **Step 9's dial STICKS across restart** (he set 4, quit, relaunched, still 4). He left it at 1 for you. Pre-existing console behaviour, not introduced here.
   - Known, not a new defect: the iron sword reads **short in the hand** — held-seat scaling is `86catvb6u` §3. Judge step 7 on visibility, not scale.

1b. **Original entry — every machine gate is clear; you are the only thing left.** Drew un-conflicted it (head `b1560cf`) and run `30586659470` is **`structure` + `build` + `capture` all SUCCESS** (playmode cancelled — advisory, and main's own run shows the same shape, so pre-existing). Drew is staging the exe from the CI artifact into `Build/soak-351/` with a played-verification and a keys-named checklist; the exe path + verbatim HUD stamp will be appended here when he reports.
   - ⚠ **The stamp will be the MERGE-commit sha, NOT `b1560cf`** — do not try to verify the branch sha against the HUD.
   - **What the ticket ships:** a second weapon-acquisition route — `sword_iron` found in a stump.
   - **How #351 nearly shipped broken, worth knowing before you judge it:** git merged `test_gate_scripts.sh` **cleanly** while leaving #351's new capture gate **unregistered** in both `HEADLESS_GATES` and `WINDOWED_GATES` — a clean auto-merge is NOT evidence a gate is wired. Caught at `170 pass / 1 fail`, fixed to `176/0`.
     - ⚠ **CORRECTED — the orchestrator initially credited the wrong guard.** I claimed #363's two-sided `assert_launch_windowed` caught it. It did not, and cannot: the `gate-wiring` loop that produced the `[FAIL]` was **already present at `c8ce948~1`** (5 occurrences, verified) — i.e. before #363 merged. Priya attributes it to **#355's wiring loop (`a5fee62`)**; that specific attribution is hers and is NOT independently re-verified here, but the disconfirming half is: **#363 gets no credit for this catch.** Drew never made the claim — he cited #363 only for the *mode measurement* (his new gate's anchored `-screen-fullscreen 0` satisfying the two-sided assert). The conflation was mine, laundering his report into a stronger story.
   - **Follow-up filed, not bundled:** `86caz56g4` — `verify-weaponfind.log` / `weaponfind-caps` are missing from the capture job's stale-clear step, so a prior run's caps can survive on the warm runner. That is the documented #130 false-GREEN class.
2. **#368 — Uma's About/credits spec needs 4 decisions from you.** She established the attribution actually owed is exactly two: **Mixamo/Adobe** (rig + clips, covers live v4) and **Hyper3D Rodin** (v1/v2/v3 meshes) — **no CC-BY debt**. Open: **S1** the upstream Rodin/Mixamo terms have *never been read* (the attribution file says so itself, `:49-52`); **S2** team-credits/tooling in or out; **S3** whether to file an engine-licence follow-up; **S4** copy sign-off.
3. **#365** — you approved merging it on green. Its `capture` re-run has NOT gone green yet (see below).
4. ⚠ **The runner disconnect watchdog is DISABLED** (your call, 2026-07-23, over the 5-min window flash). Nothing self-heals a runner death. It died tonight and stalled all CI for ~8h.
5. **#371 — Uma's enemy-HP-read spec needs 7 decisions.** The load-bearing one is **Q3: does a half-lit leading pip read as "partly gone" or as a glitch?** Her key finding, computed from the shipped defs rather than assumed: hits-to-kill on a medium boar spans **2 … 9** across the 15 `WeaponCatalog` weapons (`spear_iron` 2, `dagger_wood` 9), so a pip CANNOT mean "one hit" — she re-specced it as a 5-block quantized proportion (5 is also geometry-forced: 10 pips in the pinned 64 px pill = 4.0 px each). She added a draining-pip alpha because low-damage weapons move no pip at all on ~44% (medium) / ~55% (hard) of hits.
   - She also made **two verified corrections to already-merged material:** `LootPrompt` anchors above the **player's** head (`:112`), not the target's; and `SurvivalHud` still ships `SegmentCount = 10` (`:44`).
6. **`86caz42tv` double-dispatch risk (Priya, report-only):** that batch ticket claims to retire `86cayp1w2` / `86caywf84` / `86cavj6p1`, but all three are still open. Nothing has been closed — decide whether the batch supersedes them or they stay independent. Priya recommends closing them as duplicate-of-batch; I did NOT, because the board has no "superseded" status and marking them `complete` would assert work that isn't done. Her absorption notices on all three are what actually prevents double-dispatch.
7. **#376 — Uma's body-level hit-feedback spec: 8 decisions in §13** (none block implementation). The two sharpest: **flash-as-warm-LIFT not tint** (it protects the boar's eye/tusk vertex tones), and **`Windup` staying non-interruptible even on easy**. She also parked **12 decision drafts in §15** for Priya's next batch.
   - Her two blocking code findings, both worth knowing because they'd have shipped as bugs: bleed calls `Health.ApplyDamage` **every frame**, so a naive fire-on-damage-delta strobes all three channels for 3 s and mimics the `[DFC-1]` latch bug (gated by a 2%-of-`Max` magnitude gate + 0.12 s refractory); and `ApplyDamage` returns **clamped** `removed`, so a damage-proportional amplitude makes **the killing blow the quietest hit** — on a hard boar it lands exactly on the gate.
   - **The divide-the-labour rule she settled:** the body says *"that landed, and this hard"* (analog, instantaneous, on the creature); the pip row says *"and it's this close to down"* (quantized, cumulative, above it). #371's lost-pip extinguish flash is amended to fire only when no body flash fired, so the two never double-signal.
8. **#368 — about/credits: 4 decisions.** The load-bearing one is **S1: the upstream Rodin/Mixamo terms have never been read** — the attribution file says so itself (`:49-52`). That is a licence-compliance gap, not a design question.
9. **`86caz4wwx` filed — 2 dead `team/erik-consult` citations.** `.claude/docs/game-juice.md:55` points at `game-juice-concepts.md`, which does not exist on `main`; PR #201 (`187e486`) **wrote the citation and deleted the file in the same commit**, so it shipped dead from birth. Low severity (it is a historical footnote, not a live pointer) but `game-juice.md` is a MANDATORY pre-work read. AC3 asks for a mechanical guard, since this class shipped silently for weeks.

### ✅ RESOLVED TONIGHT — do not re-ask
- **#366 soak APPROVED** ("soak approved" + "can hold mouse down on boulder, iron ore and tree no problem"). The two defects it exposed were **FILED, not folded**: `86caz4mpy` (3 visible hits, 1 rock reaction) + `86caz4mq8` (impact delay — boulder shakes while the pickaxe is still at your side). Both `needs-soak`.
- **Icon picks made** — treatment **B** (slot-well chip `#3A302A`) + ore variant **S2** (rust-vein pile). Recorded on `86camyvwn`; `sponsor-gate` removed; it is now a Unity-build implementation ticket.
- **Build-slot spike `86cabkhjg` cleared** for implementation (sponsor-gate removed). Erik's Q4 verdict: it is NOT hard-dependent on the CI split — dispatchable now.
- **#354 merged** (`3992e96`) · **#367 merged** (`51f4623`).
- **Runner root-caused and durably fixed** — `NoDefaultCurrentDirectoryInExePath=1` blocks the bare `run.cmd` in `start-fh-runner.cmd`; launcher now uses the full path. Memory: `runner-start-blocked-by-nodefaultcurrentdir`.

### ⚠ OPEN TECHNICAL ISSUE — capture-job cancellations, now 4 instances and PARTLY diagnosable

**The blocker:** #366 (your soak-approved cadence fix) has everything green except its `capture` gate. Attempt 1 = `failure`, attempt 2 (my rerun) = `cancelled`.

**Breakthrough — the per-attempt endpoint WORKS.** Devon reported the decisive check was unanswerable because `runs/<id>/jobs` returns only the latest attempt. But `gh api repos/<owner>/<repo>/actions/runs/<id>/attempts/<n>/jobs` enumerates per-attempt:
```
#366 capture attempt 1: failure    steps=40
#366 capture attempt 2: cancelled  steps=0
```
`steps=0` means the job was **created and cancelled without ever starting** — matching Devon's measurement of instance 2 (1 s, `steps: 0`). The diagnosis he had to leave `SPECULATIVE` is now reachable; use this endpoint.

**Hypothesis — NOT confirmed, do not cite as settled:** every instance may share *"the capture job was created while the single runner was already occupied."* Tonight's cancellation happened while main's run `30583441672` was `in_progress` with #370's queued behind it. **The case any real mechanism must explain:** #363's capture DID queue successfully for 5 m 23 s in the same `unity-capture` group (`cancel-in-progress: false`, `ci.yml:491-493`), so queueing demonstrably works sometimes.

**Actionable half, needs no mechanism: request a capture rerun ONLY when `gh api …/actions/runners` reports `busy=false`.** Retrying while busy reproduced the zero-step cancellation.

**Separately — the pond gate failure on #366 attempt 1 is a WEDGE, not a regression.** `verify-pond.log` ends after the world-trace lines with **no `GATE-PASS` and no `GATE-FAIL` verdict line**; the gate found the pond (`FreshwaterPond found: True`), framed it, traced the water material, then stopped. A real assertion failure writes an explicit `GATE-FAIL` with a measurement. Caveat carried honestly: the log also shows `Failed to create agent because there is no valid NavMesh` ×3, which the triage doc lists as a corrupt-build canary — unknown whether that is normal for this gate. Both readings point to re-run, not code. **Main's own run at `c8ce948` will settle flaky-vs-genuinely-broken; its capture was `in_progress` at ~21:5xZ.**

### ⚠ #373 needs a Priya round 2 — Uma REQUEST_CHANGES (scoped)
Uma refuted the PR's own premise with data: **11 of 102 `DECISIONS.md` entries already amend an earlier one** via title markers (`supersedes` / `reverses` / `WITHDRAWN`), and `main:595` already performs Priya's exact three-way split inline against a merged entry. So "no correction shape existed" is false, and the new header paragraph (`DECISIONS.md:16`) introduces a SECOND convention while implying `grep CORRECTION:` is complete. Scoped: header paragraph only is `REQUEST_CHANGES`; the `:279` fix and the `CORRECTION:` entry are APPROVE. She also found "still-stands" undersells once — the withdrawn clause was *"beside the source `.blend`"*, i.e. provenance-by-adjacency, not a path.

### Sequencing (from Drew's #369 review — 4-way, not 2-way)
`#351` adds a capture step at the same insertion point as `#363` and must register in the same glob-driven `gate-wiring` loop (`test_gate_scripts.sh:902`); `#370` and `#365` collide on the `ci.yml` stale-clear region. Whichever merges second needs a mechanical merge-from-main.

---

## ▶▶▶ SUPERSEDED — 2026-07-19 ~03:2xZ (AWAY armed, cron `15416af4` fires :07/:22/:37/:52; main `e4ba470`; resumed from the 07-18 drain-save)

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
1. ✅ RESOLVED 2026-07-19 07:34Z — **#299 MERGED (`d757c2e`) by ORCH DIRECT `gh pr merge --admin`** on the Sponsor's in-walkthrough authorization ("you can do it. yes merge now") — the workflow-file wall applies only to the auto-merge ACTION's token, NOT the orch CLI. `86camk1x4` complete; #308 rebase dispatched. Original: **⭐ BROWSER-MERGE — PR #299 (-verifyMine CI gate, `86camk1x4`): FULLY GATED** (Devon gate-evidence run 29655784048 + Drew APPROVE 5012453071 + required CI green; workflow-file token wall `86cafhehe`): https://github.com/TSandvaer/Far-Horizon/pull/299 — one browser click; on merge the orch flips `86camk1x4` complete and `86cag93zb` AC4 un-holds. **SEQUENCE NOTE (Drew, #308 review): merge #299 FIRST** — #308 (placement gate, staging once its gates land) edits the same ci.yml capture-job/stale-log lists; whichever merges second needs a mechanical rebase, and the orch will route #308's merge-from-main to a persona after your #299 click. Also unlocks `86catr79g` (-verifyBoulder clone of the mine gate).
1b. **⭐ BROWSER-MERGE — PR #308 (placement verify gate + boulder obstruction, `86catr49m`): FULLY GATED ~06:4xZ, merge AFTER #299** (Drew's sequence note — same ci.yml lists; after your #299 click the orch routes #308's merge-from-main to a persona, then you click #308): https://github.com/TSandvaer/Far-Horizon/pull/308. Gates: required CI SUCCESS on run 29673988758 with the `-verifyPlacement` step EXECUTED+PASSED on the PR's own run (RED-over-boulder proven in the shipped exe — orch job-level verified) + Drew APPROVE_WITH_NITS (5014459865, condition satisfied) + Tess QA PASS_WITH_NOTES (5014462325). On merge: orch flips `86catr49m` complete. Her soak-eyeball note for a FUTURE post-#308 build (not soak-crafting-4): boulder break-fade (~2.3s) + regrow-rise (0.6s) read GREEN to the ghost while still visible — designed transient, eyeball it whenever you next soak a #308-inclusive build.
2. ✅ RESOLVED 2026-07-19 ~10:3xZ — **④ CHAIN SOAK = SPONSOR PASS** ("chain works, forge reads right"; walkthrough popup) → `86camz9v7` + `86catqxm0` + `86camz9vh` + `86camz9vq` ALL COMPLETE — the crafting redesign wave ①-④ is CLOSED. Scatter-rock residual (`86catr49t`): blanket PASS suggests accepted — confirm at walkthrough closure. Original: **⭐⭐ FULL CRAFTING-CHAIN SOAK (④) — READY TO PLAY (supersedes the earlier ②-only soak-crafting-3 item; Tess play-verify = SERVE ~04:5xZ):** exe `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-crafting-4\FarHorizon.exe` — **confirm the HUD stamp reads `75a9725`** before judging (Tess ground-truthed it on the HUD + in resources.assets). Provenance: LOCAL build of main `75a9725` (label-merged main spawns no CI artifact — `PROVENANCE.txt` in the folder); contains the WHOLE wave: table ① #294 + ghost-RED #302 + boulders ② #303 + wood FBXs #304 + forge/IRON ③ #305. Her headless pass: verifyForge/verifyBoulder/verifyMine/verifyWeaponSet ALL PASS, console clean; evidence `Build\soak-crafting-4\verify-captures\` (9 PNGs). Plan doc: `team/tess-qa/crafting-chain-soak-plan.md`.
   **Keys:** WASD move · Shift run · Space jump · mouse-drag orbit · scroll zoom · **Tab** = Pack · **E** = use/loot · **C** = place table · **V** = place forge (interim key — the build-menu ticket `86catpvpa` retires it later) · left-click = strike/place · number keys / **[B]** = belt select.
   **TEST (judged):**
   (1) **Forge SILHOUETTE — your #1 probe (the one thing Tess could NOT pre-confirm; headless caps only see it top-down):** place the forge, walk around it — does it read as a stone FURNACE side-on (firebox opening, sits ON the ground, distinct from the campfire)?
   (2) **Full chain end-to-end:** hand-gather sticks/pebbles → **C** places the table (ghost + left-click confirm, Escape cancels) → craft WOOD tools → wood pickaxe breaks a boulder → StonePile → **E**-loot → STONE rows in Pack → craft STONE tools → gather ≥6 wood + 12 stone → **V** places the forge → smelt ore→ingots → craft IRON tools at the table.
   (3) **Ghost RED over a TREE** (#302's fix, first in-game proof): with mats, drive the placement ghost over a tree → RED + `[X] BLOCKED — overlaps an object`; clear ground still places.
   (4) **3-tier in-hand A/B** (belt/[B]) + recipe-menu tier-gating (locked → craftable rows; short-mats greys, no partial debit).
   (5) Difficulty presets sanity (easy/med/hard smelt+cost dials).
   **OBSERVED, judge lightly:** bootstrap sufficiency near spawn · scatter-rock ground clip (`86catr49t`). **EXPECTED, not a fail:** ghost over a BOULDER doesn't read blocked (registry deferred, `86catr49m`).
   **Verdict:** PASS → say the word and orch flips `86camz9v7` + `86catqxm0` + `86camz9vh` + ④ `86camz9vq` complete (the whole crafting wave closes) · issues → name them per phase, they route back to the devs.
3. **decisions-while-away pending review:** ghost-mechanism ruling (2026-07-18) + #304 auto-merge label (2026-07-19).
4. ✅ RESOLVED 2026-07-19 ~1x:xxZ — **v4 SOAK = SPONSOR PASS** ("v4 soak approved"; soak-v4 @ bb40fe7) → v4 IS the hero; activation ticket `86catvb6u` dispatches to Drew (default flip + measured re-seat + sponsor F9 nudge pass across ALL weapons/tools, then confirm-soak). Original: **NEW — castaway v4 is ON MAIN, dormant (your call when to look):** #307 merged 04:16Z (`bd7c7d9`) — v4 integrated behind a default-OFF toggle, v3 byte-unchanged (traced + QA'd, EditMode 1123/1123). The v4-ACTIVE look is yours to judge whenever you want: say "stage the v4 soak" and the orch cuts a `FARHORIZON_CASTAWAY_V4=1` build (needs the build lane) + files the activation ticket (measured held-prop re-seat + default flip + the 3 review NITs ride it). No urgency — v4 costs nothing sitting dormant.
5. **Carried:** icons `86camyvwn` (sponsor-queued design) · `86cafhehe` PAT creation (durable fix for the workflow-file token wall — sponsor infra action, whenever convenient).

2b. **⭐⭐ C BUILD-MENU SOAK (#311, `86catpvpa`) — READY, your mid-soak ask fixed same-day:** exe `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-buildmenu\FarHorizon.exe` — **confirm HUD stamp `fb82f35`** (merge-ref of run 29681195265; all machine gates green: Devon APPROVE_WITH_NITS holds satisfied + Tess content-PASS auto-converted + -verifyBuildMenu step completed/success). **TEST (Tess's keys-named checklist):**
   1. Press **C** → BUILD menu opens modal (world frozen). Rows: **Crafting Table** (5w+3s) + **Forge** (6w+12s), both greyed "(need more)" at empty pack; **C**/**Esc** closes.
   2. Click a greyed row → nothing happens (menu stays, no ghost).
   3. Gather ≥5w+3s → **C** → Table un-greys → click → ghost under cursor; **scroll** rotates, **LMB** places (debits), **Esc** cancels no-debit.
   4. Gather ≥6w+12s → **C** → **Forge** un-greys → click → forge ghost → **LMB** places (debits 6w+12s) — your exact ask ("build the forge also").
   5. Old **V** builds nothing on its own; **C** during an active ghost does NOT reopen the menu. (Campfire row deferred to ⑤ by design — NOT judged.)
   **Verdict:** PASS → orch direct-merges #311 (your delegation) + flips `86catpvpa` complete + ⑤ campfire dispatches onto the new seam · issues → name them.

### Tick log 2026-07-19
- **~03:5xZ: ③ SHIPPED — #305 MERGED (`75a9725`)** on full gates (Drew APPROVE + Tess QA PASS + CI; standing auto-merge; soak rides ④) · #304 MERGED earlier (`dd5dd11`). Post-③ wave dispatched ×3: Tess ④ full-chain soak build+play-verify (soak-crafting-4, will SUPERSEDE item 2's soak-crafting-3 when it passes — hold off soaking item 2 if you're reading this fresh) · Drew v4 phase C · Devon #304-NITs chore. Priya's AC-flesh applied to all 4 next-wave tickets (comments). NITs ticket filed: `86cattbca`.
- ~03:1xZ resumed; main post-#303 CI verified green (run 29661283924); away-mode armed; soak build cut to `Build\soak-crafting-3\`.
- Reviews: **#304 Devon APPROVE_WITH_NITS (5013939510) → auto-merge LABELED** (standing policy; NITs → chore ticket) · **#305 Drew APPROVE (5013945742, 0 blockers, build-key V ratified)** — remaining gate = fresh Tess QA (queued behind her play-verify); #305 labels only AFTER #304 merges (label-race sequencing).

## ▶▶▶ (superseded) — 2026-07-08 ~16:1xZ (AWAY armed, cron `c3746bda` fires :08/:23/:38/:53; 2 agents in flight; main `b9e1240`)

> The 2026-07-06 block below is HISTORICAL — its items 0c/0d/0e/0f/1/2 all RESOLVED during the 2026-07-08 present-mode day (17 PRs merged: dial confirm-soak APPROVED, pickaxe soak done + baked #287, #281/#282 merged, iron I-1/I-2 shipped). Only 0a (feature-wave) + 0b (swing ruling) carry forward — restated here.

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
0a-RESOLVED 2026-07-18 ~19:00Z: **✅ RE-SOAK PASSED → #294 MERGED (a922bcf); ① complete; ② dispatched.** One named gap → follow-up ticket (ghost RED on object overlap; Sponsor verbatim in the ticket). Original: 0a-RESOAK 2026-07-18 evening: **⭐⭐ CRAFTING ① FIX-ROUND RE-SOAK STAGED — ALL MACHINE GATES GREEN** (Drew APPROVE_WITH_NITS 5012305859 + Tess QA PASS 5012303458 + required CI SUCCESS on `37a5358`; Devon play-verified probes a/c/d live + screenshots in `Build\soak-crafting-2\probe-caps\`). **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-crafting-2\FarHorizon.exe` — **confirm HUD stamp `c54b1c8`** before judging. **Checklist (every key named):** WASD move · Shift run · Space jump · mouse-drag orbit · scroll zoom · Tab = Pack. (1) no free axe at the old stump; (2) gather: walk to sticks/pebbles → **E** picks up (loot prompt) until ≥5 wood + 3 stone (Tab to check); (3) with 0 mats first: press **C** → ghost must read RED + `[X] NEED 5 wood + 3 stone` (non-color cue = F4); (4) **C** with mats → free-cursor ghost FOLLOWS THE MOUSE on the ground (F1); (5) **scroll wheel** while placing rotates the ghost, camera zoom must NOT change (F3); **Escape** cancels, no build (F2); (6) **left-click** on a valid `[OK]` spot → table builds, mats debited (Tab); (7) walk to the table → "Use crafting table" tooltip → **E** opens the menu (F5); **C does nothing**; with a stone NEARER than the table, E loots the stone (precedence); (8) menu = unlocked-only + **Show locked** button (F6); menu clicks never act in the world; Escape closes; (9) judge the deliberate v1: WASD+orbit FREEZE while placing — keep or want orbit-during-placement? **NOT judged (unchanged deferrals):** wood in-hand meshes/swing (art rides 86catqn5n/②) · axe_wood doesn't chop · interim stone-axe spawn (② reworks). **Verdict:** PASS → merge #294 on your word (soak-gated) → ② `86camz9v7` dispatches · issues → name them, back to Devon. Original: 0a-UPDATE 2026-07-18: **✅ SOAKED → FIX ROUND IN FLIGHT.** 3 ACs PASS (stump-gone · craft+debit/greyed-rows · Bar-4 table read); placement + menu interaction FAIL → Devon fix round F1–F6 on the PR branch: free-cursor mouse ghost (the accepted advisement default) + LEFT-CLICK place + SCROLL-wheel rotate (camera zoom gated) + real invalid-placement rules reading RED incl. insufficient-materials (non-color cue kept) + E=Use key w/ "Use crafting table" tooltip (C retired for the menu) + unlocked-only recipe menu w/ show-locked button. Sponsor also seeded "C = build menu of placeables" → follow-up ticket FILED 2026-07-18 post-restart: `86catpvpa` (dep ①-merge; coordinates ③/⑤). Re-soak re-stages after Drew delta-review + Tess delta-QA; checklist must name every input key. Original item: 0a. **⭐⭐ CRAFTING-TABLE ① SOAK (PR #294, `86camz9uz`) — ALL MACHINE GATES GREEN, staged ~23:4xZ:** Drew APPROVE_WITH_NITS (4919859554; 3 NITs folded into ②) + Tess QA FAIL→**PASS** (4920103228; the one blocker — chop-capture probe walking to the deleted CraftSpot — fixed by Devon `296834e`, probe-only diff, chop gate PASSED on run 28981894648) + required CI ALL SUCCESS. **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-crafting\FarHorizon.exe` — **confirm HUD stamp `499981a`** before judging. **TEST (the judged ACs):** (1) the old auto-craft stump is GONE (no free axe on walk-up); (2) hand-gather sticks + pebbles → with ≥5 wood + 3 stone a crafting TABLE can be PLACED where you choose (ghost + confirm; ghost shows a valid/invalid cue that is NOT just red/green); (3) walk to the placed table → recipe MENU opens: WOOD tier live, STONE/IRON rows visible-but-Locked; (4) craft a wood axe/pickaxe — materials are DEBITED (check Pack); short-mats → row greyed, no craft, no partial debit; menu clicks never move/act in the world; (5) Bar-4: does the placed table READ as a crafting table (slab on 4 legs, on the ground)? **⚠ NOT judged (deferred, flagged in the PR):** wood tools have NO in-hand mesh/swing yet (real wood art = item 0b below; verbs = ②); `axe_wood` doesn't chop (chop stays stone-axe); one visible stone axe still spawns (interim source, ② reworks it). **Verdict:** PASS → I merge #294 (unblocks ② boulders+stone) · issues → name them, back to Devon.
0b-RESOLVED 2026-07-18 (walkthrough): **✅ WOOD TIER PASSED as-is** — FBX export + integration ticketed (see STATE.md). Original item: **⭐⭐ WOOD-TIER WEAPON VERDICT (overnight burst, approved ~22:0xZ; NOT integrated — verdict renders only):** judge the 5 wood pieces in `art-src\wood-burst-renders\` — `family_lineup_three_tiers.png` (wood front row / stone mid / iron back — the tier progression read) + per-piece `{axe,pickaxe,spear,knife,sword}_{side,34}.png`. Design language: material-honest whittled wood from EXISTING palette tones — haft-brown bodies, pale worked-wood (tan) cut facets, dark fire-hardened spear tip; leaner poly counts than stone (28-41/piece = crude first tier). Source saved into `art-src/weapons_reauthor.blend` (wood row y=-0.6); FBX NOT exported (awaiting your PASS). **Verdict options:** PASS → integration rides ①/② (ids `*_wood` already live in #294's catalogs) · name fixes per piece ("axe head chunkier", "dagger blade narrower") → I iterate the burst.
1. **⭐ NEXT FEATURE-WAVE ORDER (carried 0a):** iron chain is on your locked Model-A priority (I-3 forge/smelt IN FLIGHT now; I-4 craft-unlock + I-5 chain-soak follow). What fills the feature lane AFTER/alongside iron? Candidates: **combat cluster** (boar 86cah7ydt / find-in-world 86cah7y5b / roster 86cah7ym9 / status-effects 86cah7yuh / HP-HUD 86cah7z2q) · **boat POC** `86caa9zju` · **open-horizon** `86cagfn8h` (Uma A/B/C spec awaits your pick). Say an order and the loop works it.
2. **⭐ SWING-APPROACH RULING (carried 0b — blocks `86caffwv5` attack-anim):** per-weapon FINAL swing = procedural additive-offset (procedural-animation-verbs.md) vs a new Mixamo attack clip (your earlier chop preference, [[chop-swing-mixamo-clip-not-procedural]])? Combat POC shipped a placeholder; the distinct swings need this call.
3. **⭐ ADVISORY→REQUIRED PLAYMODE FLIP (ci.yml — your call):** the last playmode fix `86camf3xe` (MineOre TestClock) is IN FLIGHT now. Once it merges AND the playmode job shows 0 failures on ×2 consecutive main runs, the advisory→required flip in `ci.yml` is your go/no-go. I'll stage the exact one-line diff + the 2 green-run URLs when the gate is met.
4. **✅ RESOLVED 2026-07-08 ~21:43Z — WATCHDOG INSTALLED + VERIFIED:** sponsor approved via popup; PS deny rule + self-mod guard blocked the orch path (3 classifier denials) → sponsor pasted the install (elevated PS; two machine findings folded into INSTALL.md: `[TimeSpan]::MaxValue`→3650d + elevation required). Task `FarHorizon-RunnerDisconnectWatchdog` registered (State Ready) + first fire verified in the log (lines 195-197: START→OK ONLINE→DONE 21:43Z). Self-fires at logon + every 5 min.
5. **decisions-while-away pending-review:** a few auto-decide entries from today await your accept/reverse mark (86camf3xe + 86camf6vz filings). Review in `.claude/decisions-while-away.md` on return.

### ⭐⭐ FORGE SMELT-LOOP SOAK — I-3 / PR #292 (`86cakkmvc`) — STAGED, sponsor-gated
**Machine gates ALL GREEN:** Drew APPROVE_WITH_NITS (comment 4917294109) + Tess PASS_WITH_NOTES (4917296130; independent EditMode 1051/1051) + required CI SUCCESS (structure+build+capture, merge-ref `4cb464b`). Reachability + the full gather→build→smelt loop VERIFIED functional end-to-end in the shipped exe by Devon (forge-soak-prep: ingot produced, D3D12). **Only the VISUAL/FEEL read remains — your soak.**
**Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-forge\FarHorizon.exe` — **confirm HUD stamp `4cb464b`** before judging. (Captures exist at `Build\soak-forge\forge-captures\forge_{before,built,after}.png` but they frame the forge POORLY — `-verifyForge` teleports the player onto the forge origin — so judge LIVE, approaching on foot.)
**Steps (shipped = Medium difficulty: build 4 wood+5 stone; smelt 2 ore+2 wood → 1 ingot / ~12s):** grab axe (auto belt-1) + pickaxe; number-keys select tool; WASD + Shift move. Left-click trees → ≥6 wood; press **E** on stones → ≥5 stone; left-click ore nodes then **E** → ≥2 iron-ore. (a) **Build:** walk within ~2m of the grey furnace (WEST of spawn, ~(-7,0,0)) with ≥4 wood+5 stone → auto-builds. (b) **Feed:** with ≥2 ore+2 wood, stand within ~3m of the built forge → smelt auto-begins. (c) **Timer:** firebox glows ~12s. (d) **Collect:** +1 iron-ingot (Tab to verify); next batch auto-starts while mats+proximity hold.
**JUDGE:** (1) does the built forge READ as a furnace side-on — firebox opening, sits ON the ground, distinct from the campfire (lowpoly-quality anchor)? (2) is the smelt GLOW readable? (3) **auto-smelt-on-proximity feel** (Drew's soak-probe): do you want smelt to auto-begin on proximity, or an explicit feed action? **Verdict:** good → I re-gate + merge #292 (flip `86cakkmvc` complete → unblocks I-4 `86cakkmy2` / I-5 `86cakkn15`; NITs `86camw8rm` fold on merge); silhouette/glow/feel wrong → back to Devon with your note.

### Resolved this session (context)
- **#291 MineOre TestClock MERGED 17:00Z** (`86camf3xe` complete) — playmode-greening arc DONE (failed=0). Advisory→required ci.yml flip = your call (see sponsor-queue item 3; needs skip=pass handling for the 13 `[Ignore]` tests).
- Hygiene: `86camdk4x` drift → complete (its PR #284 merged 07:56Z). NITs ticket `86camw8rm` filed from #292 review.
- **0 agents in flight — team DRAINED pending sponsor** (the forge soak above + the gated board: I-4/I-5 dep-on-I-3-merge; combat/boat/horizon/swing/zoom/spikes sponsor-gate).

## ▶▶▶ (superseded) — 2026-07-06 ~21:3xZ (AWAY armed, cron `12ee54c3` fires :07/:22/:37/:52; session model fable; keep-screens-alive verified pid 31308)

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
0a. **⭐ NEXT FEATURE WAVE — PRIORITY CALL (new 2026-07-07 late: island 2.0 C1–C4 ALL COMPLETE, perf verdict HOLDS 60fps/8× headroom):** the iron chain continues on your locked Model-A priority (pickaxe integration `86cam9q5f` next in the build lane, then I-2 `86cakkmr0`). What fills the feature lane AFTER/alongside iron? Candidates: **combat cluster** (boar 86cah7ydt / find-in-world 86cah7y5b / roster 86cah7ym9 / status-effects 86cah7yuh / HP-HUD 86cah7z2q) · **boat POC** `86caa9zju` · **open-horizon** `86cagfn8h` (Uma's A/B/C spec awaits your pick). Say an order (e.g. "iron then combat, boar first") and the away loop works it.
0b. **⭐ SWING-APPROACH RULING (re-surfaced — blocks `86caffwv5` attack-anim):** per-weapon FINAL swing = procedural additive-offset (procedural-animation-verbs.md) vs a new Mixamo attack clip (your earlier chop preference, [[chop-swing-mixamo-clip-not-procedural]])? The combat POC shipped a placeholder; the distinct swings need this call before 86caffwv5 dispatches.
0f. **⭐⭐ PICKAXE SOAK + SEAT DIAL (PR #283, `86cam9q5f`) — ALL MACHINE GATES GREEN** (required CI SUCCESS + Devon APPROVE_WITH_NITS 4910758021 + Tess QA PASS 4910815750 — she independently gate-ran the CI exe herself + Self-Test Report on the PR). **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-pickaxe\FarHorizon.exe` — **confirm HUD stamp `46ee5dd`** before judging. **TEST:** empty-handed **[B]**-cycle to the two NEW slots — pickaxe STONE (idx 4) and IRON (idx 5). ⚠ KNOWN + EXPECTED: both start at the axe-seat baseline, so the crosswise head reads EDGE-ON (blade-like) — that's the dial, not a defect. **F9-dial each pickaxe's seat euler until it reads as a T-pickaxe in hand** (values print to the log, same flow as your weapon dial). Judge the MESHES from your PASSED renders now in-engine + the family fit. On "dialed" (+ values or a screenshot): Drew bakes the seats ON THE #283 BRANCH (+ fixes the cosmetic .meta globalScale NIT in the same commit), re-gate, then merge. Mesh problems → name them, back to the burst.
0e. **⭐ ONE-CLICK MERGE — PR #282 (playmode triage investigation, `86cama53u`): ALL GATES GREEN** — docs-only (no CI checks spawn, state CLEAN) + Tess APPROVE (comment 4909667730; every claim ground-truth-verified: cause = PR #249, the 10 = all test-defects, 0 production bugs). Merge: `gh pr merge 282 --admin --squash --delete-branch` (or say the word and I label) → orch flips 86cama53u complete + files the 3 drafted test-fix tickets from team/analysis/2026-07-08-playmode-advisory-triage.md (they green the playmode set; advisory→required flip afterward is YOUR ci.yml call). Her 3 cosmetic NITs live in the review comment (analysis-note cites; no ticket warranted).
0d. **⭐ ONE-CLICK MERGE — PR #281 (keystone determinism guard, `86cakp58u`): ALL MACHINE GATES GREEN** — required CI SUCCESS + Devon APPROVE_WITH_NITS (4909467226, 0 blockers; doc-NIT filed as `86cama43f`) + Tess QA PASS_WITH_NOTES (4909556862; independent EditMode, both guards red-on-break verified, gap-A acceptance). Test-only, no soak surface. Merge: `gh pr merge 281 --admin --squash --delete-branch` (or say the word and I label) → orch flips 86cakp58u complete. Her note for the future C5 author (fully-symmetric per-tree draw stays green — accepted tradeoff) is in the QA comment.
0c-RESOLVED. **✅ #279 MERGED 22:13:05Z under your pre-auth (all gates: required CI green + Devon APPROVE_WITH_NITS 4909316469 + Self-Test Report 4909445219 + Tess QA PASS_WITH_NOTES 4909506137). 86cakkfz9 complete.** **⭐ CONFIRM-SOAK STAGED:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-dial-confirm\FarHorizon.exe` — **confirm HUD stamp `9ba6dd2`** before judging. TEST: empty-handed **[B]**-cycle knife → sword → spear (each should sit exactly as you dialed: knife scale 0.850, spear 0.900, sword 0.950, your offsets) + select belt slot **1** for the axe (your offsetFromHand/euler). Verdict "confirmed" closes the dial saga; name any weapon that drifted and it goes back to Drew with your note.
0c-old. **(superseded)** #279 dial-bake ladder (pre-auth CONDITIONAL, in motion):** required CI ALL GREEN + Devon APPROVE_WITH_NITS (4909316469; code clean, your values EXACT). Remaining: Drew's Self-Test Report (posts when drew-wt frees from the keystone task) → Tess QA → auto-merge label per your pre-auth → confirm-soak exe served. If anything reds, it stages as one-click instead.
1. **✅ DIAL DONE → DREW BAKING (2026-07-07). All 4 dialed:** AXE off=(0.0071,0.0599,0.0288) eul=(-152.5,-5.9,108.9) · KNIFE off=(-0.020,0.020,0) sc=0.850 · SPEAR off=(-0.020,0.560,0) sc=0.900 · SWORD off=(-0.020,0.040,0) sc=0.950. Drew agentId `ab8208ed5dd2d7370` (86cakkfz9 in progress; reviewer Devon; folds Tess's 3 AxeNudgeTool NITs). On PR merge → Sponsor confirm-soak. Original: (v3 weapons, `86cakkfz9`) `C:\Trunk\PRIVATE\Far-Horizon\Build\dial-v3-weapons\FarHorizon.exe` (F10 overlay → check the "F9 NOT ENGAGED" badge → F9 engage; [K] target, [B] weapon, [O]/[I] scale, arrows/PgUp-PgDn nudge; full cheat-sheet in PR #270 body). Values log to Player.log/HUD → on your word ("dialed") Drew bakes (Phase 3, carrying Tess's 3 AxeNudgeTool NITs).
2. **✅ PICKAXES PASSED BOTH (sponsor, 2026-07-07 walkthrough) → handoff ticket FILED: `86cam9q5f` (harvest + catalog + v3 seating + Bar-5 picker soak; build-lane after C4); `86cakkmmz` flipped complete; I-2 `86cakkmr0` unblocks on 86cam9q5f.** Original item: **(iron I-1, `86cakkmmz`)** — burst DONE (2026-07-07 ~01:5xZ, orch solo per your walkthrough approval). Judge these five renders in `art-src\pickaxe-burst-renders\`: `stone_side.png` + `stone_34.png` (knapped double-point head, wood haft + grip band carried from your approved stone axe, white working tips, dark belly facets — 88 tris), `iron_side.png` + `iron_34.png` (forged flat-smooth slab: pick point + honed chisel bevel both EDGE-white, iron handle + 3 leather grips + pommel carried from your approved iron axe — 154 tris), `family_lineup.png` (both beside the axes for style fit). Source saved in `art-src/weapons_reauthor.blend`; FBXs pre-exported (untracked, NOT integrated/merged — waiting on your PASS). **Verdict options:** PASS → I file the harvest+integration handoff (dev imports + seats on v3, in-hand picker soak per Bar 5) · or name fixes per piece ("pick arm longer", "head chunkier", "sharper chisel") → I iterate the burst.

### ⭐ ONE-CLICK MERGES — ✅ ALL 3 MERGED 2026-07-07 (#274 20:49Z / #275 21:01Z / #276 21:05Z; sequenced labels); tickets flipped complete. C3 #277 also MERGED 21:05:35Z post-soak-PASS → C4 dispatched (Devon)
- **⭐ PR #274 — weapons+gradsky NITs bundle (`86caju057`):** Tess APPROVE_WITH_NITS (comment 4897911958, 0 blockers; 1 record-only NIT: 7th file DebugOverlayMaster.cs missing from the PR-body list — comment-only change, on-theme) + required CI ALL SUCCESS (structure/build/capture; playmode cancelled = advisory). Mechanical comment/docstring/gate-script cleanup, zero behavior change, no soak surface. Merge: `gh pr merge 274 --admin --squash --delete-branch` → I flip `86caju057` complete on the next tick.

- **⭐ PR #276 — v2/v3 toe-bones clip-carry guard (`86cak0upj`):** Tess APPROVE (comment 4897984619, 0 blockers; all 4 claims verified incl. both rigs carrying ToeBase) + required CI ALL SUCCESS (structure/build/capture; playmode cancelled = advisory). Test-only, no soak surface. Merge: `gh pr merge 276 --admin --squash --delete-branch` → I flip `86cak0upj` complete on the next tick.

- **⭐ PR #275 — DEBUGCYCLE headless fix (`86cajt6jz`):** Tess APPROVE_WITH_NITS (comment 4897960078, 0 blockers; root cause + anti-mask verified by code trace) + required CI ALL SUCCESS after the 01:39Z capture-job rerun (run 28825356866 conclusion success). Code+test fix, no UX surface. Merge: `gh pr merge 275 --admin --squash --delete-branch` → I flip `86cajt6jz` complete on the next tick.

### Staging pipeline
- **✅ C3 SOAK PASSED (sponsor, 2026-07-07 walkthrough) — #277 queues for auto-merge label after #274/#275/#276 (label race); on merge: flip 86cakk4x2 complete + dispatch C4 `86cakk4xf`.** Original item: **(island vegetation, PR #277, `86cakk4x2` in review):** exe `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-c3\FarHorizon.exe` — **confirm HUD stamp `405c70b`** before judging. Judge: 2 distinct tree species (broadleaf blob + new pine cone-stack), bush understory, patch-masked varied grass; snow cap stays clean; C1/C2 features unchanged (trees/rocks/walls byte-identical per Drew, counts 252/6/12); climb intact; 60.1 fps measured. **One deliberate change to judge:** ALL vegetation ships castShadows OFF (spec-directed perf protection — C1's trees previously cast shadows; if the forest reads floaty/ungrounded, say "shadows back" — one-flag reversible). Predict-Before-Soak + full ACs in the PR body. **Machine gates ALL GREEN (02:4xZ):** Devon APPROVE_WITH_NITS (comment 4899433555 — keystone seed+555 stream-isolation verified line-by-line) + Tess QA PASS (comment 4899479293 — independent EditMode 1013/1013, stamp==HEAD verified) + required CI SUCCESS (structure/build/capture). **Only YOUR soak remains.** On your PASS → auto-merge label #277 → flip 86cakk4x2 complete → C4 perf re-measure dispatches (last island child; its ticket carries the grass-load notes). Follow-up test guard filed: 86cakp58u (Devon NIT-1 + Tess gap-A absorbed).

### Walkthrough 2026-07-06 outcomes (context)
- C2 #271: soak PASSED (stamp b52f36a) → capture rerun green → `auto-merge` labeled ~21:28Z → C3 `86cakk4x2` dispatches to first free dev post-merge.
- fh-261-fold cleanup: one-liner handed to sponsor live (he runs it himself).

## ▶▶ HISTORICAL — 2026-07-04 23:39Z (AWAY re-armed, cron `048b9637` 15-min; session model opus[1m]; verify keep-screens-alive is ON)

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
1. **⭐ BROWSER-MERGE — #223 sun** (86cah90cp): Sponsor-PASSED soak-223-v4 + all REQUIRED checks green + MERGEABLE + `auto-merge` label ON, but the Action DIDN'T fire (evaluated during pending CI, never re-fired on green; state UNSTABLE = advisory-playmode red only). NO .github files → the label *should* work; it's just stuck. Merge: `gh pr merge 223 --admin --squash --delete-branch`. Unblocks GradientSky cluster (86caj0rrg → 86cahxeek). ⚠ #223 overlaps #247 on SettingsPanel.cs → #247 will need a merge-from-main after #223 lands.
2. **⭐ BROWSER-MERGE — #253 drag-ghost gate fix** (86cajrtr1): Drew APPROVE_WITH_NITS; the reworked drag-ghost gate itself PASSED (errPanelPx=0.00, Drew-confirmed); the capture-job RED is the KNOWN settings-gate wedge FLAKE (ticket 86cajt6kq), NOT this PR's gate. Touches .github → `gh pr merge 253 --admin --squash --delete-branch`. Fixes the OLD drag-ghost gate currently red on main.
3. **⭐ SOAK — #239 v3 finger + arm** (86cahnmjv): **CUT + READY** (CI run 28719744042 green — structure/build/capture ✓): `Build\soak-239-v3\FarHorizon.exe`, HUD stamp `7c1b177` (confirm before judging). TEST: debug-cycle ([B]) to the SWORD + KNIFE → the thumb now WRAPS the grip (root cause was coverage: grip fired only for belt weapons axe/spear; sword/knife are debug-only so never gripped — Drew widened it to `DebugViewActive`). + the idle arm reads natural with your baked eulers R(-2,-34,-7)/L(-5,22,0). Known SEPARATE follow-up: sword grip-SEAT tightness (`WeaponMeshLocalOffset[sword]`) is its own dial, not this fix.
4. **⭐ SOAK + BROWSER-MERGE — #247 F1 stepper-room — CUT + READY** (86cah8ukr): you PASSED the panels-render fix (soak-247-v2 ✓). Drew fixed the cramped F1 int-steppers + Devon fixed the gate-fixture structure-red → **CI now FULLY GREEN** (head `4ec4391`, run 28724507188 structure/build/capture ✓; playmode advisory). **Soak exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-247-v3\FarHorizon.exe` — **confirm HUD stamp `7289dbb`** before judging. **TEST:** open F1 → the int-stepper rows (the `[−]`/value/`[+]` controls) now have ROOM — the row WRAPS instead of crushing the control below its button width (fix: `flex-shrink:0` + `min-width:108px`, mirrors F3). Root cause: the F1 stepper was the only shrinkable child in a no-scrollbar row. **VERDICT → browser-merge** (touches `.github/…/verify_settings_gate.sh` → label fails on .github): `gh pr merge 247 --admin --squash --delete-branch`. ⚠ needs a `git merge origin/main` AFTER #223 lands (both edit SettingsPanel). NIT to file (Devon): Check-5's absent-line grep under `pipefail`+`-e` aborts instead of printing the clean "missing proof" message (harmless in real runs).
5. **⭐⭐ IN-HAND WEAPONS SOAK — #254 — CUT + READY** (86cajkk7h) — the soak you've been waiting for. **ALL CODE GATES GREEN:** Drew APPROVE_WITH_NITS (comment 4884267877, 0 blockers) + Tess QA PASS_WITH_NOTES (comment 4884271409; EditMode 947/947, `-verifyHeldBelt` stone in-hand gate green) + CI run 28724098827 structure/build/capture ✓ (playmode = pre-existing advisory).
   - **Soak exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-254\FarHorizon.exe` — **confirm HUD stamp reads `b883283`** before judging (verified in Player.log + resources.assets).
   - **PROBE 1 — per-weapon IN-HAND (played):** launch, then debug-cycle **[B]** through the 4 STONE weapons (axe / knife / sword / spear) → judge each one's in-hand look + seating. Stone is the LIVE crafted tier (held/stump/pickup axe + spear pickup resolve real FBXs). Known separate dial (NOT this soak): sword grip-SEAT tightness (`WeaponMeshLocalOffset[sword]`).
   - **PROBE 2 — stone/iron TIER CONTRAST (capture, iron isn't wielded so it's invisible in play):** open **`C:\Trunk\PRIVATE\Far-Horizon\Build\soak-254-caps\weapon_set.png`** — the 8-weapon lineup (stone = off-white knapped blades; iron = blue-grey smooth blades). Orch produced it via `-verifyWeaponSet` (not CI-wired; Tess flagged). Does the stone→iron material step-up read?
   - **VERDICT → auto-merge:** #254 is code-only (no `.github`) → on your PASS the `auto-merge` label works. Bad → back to Devon.
   - **Then file iron-progression DESIGN follow-up** (stone→iron upgrade path/recipes/unlock) — iron is imported-not-wired by design; the progression is a future conversation.
   - Non-blocking NITs to file (orch): Drew — PondNudge.cs dangling `HeldAxeLengthPicker` cref CS1574 (:8/:38/:59/:222) + BootstrapProject.cs:356,422 stale comments; Tess — MovementCameraScene.cs:50/:600 "wpn_axe_01" stale comments; and the `-verifyWeaponSet` CI-wiring gap (WeaponSetVerifyCapture.cs exists but not in ci.yml → no `weaponset-caps` artifact).

### In flight (away loop watches) — refreshed 2026-07-05 01:0xZ
2nd-wave results: ✅ **Tess CHOP `86cajt6j8` → PR #255** (all 8 reds proven env/stale via `Time.captureDeltaTime` pinning, NOT a real double-apply; +1 stale SurvivalLoop fix; test-only). ✅ **Drew GradientSky.mat `86caj0rrg` → PR #256** (POC gets own `PocGradientSky.mat`, shared Boot byte-untouched, EditMode 949/949, guard added). ✅ **soak-247-v3 CUT** (`Build\soak-247-v3\`, stamp `7289dbb`; #247 CI fully green — item 4).
✅ **Devon FOG `86cajt6jb` → PR #257** (cause OVERTURNED: committed fogColor.r=0.80 not stale 0.42; the 0.42 was pre-#241 pollution already fixed; runtime `WorldLookTunables.Start` palette re-assert, byte-identical + regression guard + `FogSeamDump` diagnostic; EditMode 11/11).
NOW IN FLIGHT:
- **Devon** peer-review PR #256 (agentId `a4a3091e0f72474e2`, devon-wt) — byte-identical shared-mat + non-tautology-guard checks.
- **Drew** peer-review PR #257 (agentId `af3ffefcfb1b33a12`, drew-wt) — proven-cause soundness + no-regression on Devon's runtime-fog fix.
NEW PRs → review → one-click merge staging (all code/test-only, NO soak):
- **⭐ PR #255 STAGED** (Tess CHOP, 86cajt6j8): **Drew APPROVE** (comment 4884346618, anti-mask check HOLDS — the captureDeltaTime pin doesn't hide a real double-apply) + build/structure SUCCESS. Capture CANCELLED = concurrency-superseded on the runner-1 lane (NOT a failure); test-only diff (2 files, no visual/game code) so capture is irrelevant → **admin-merge overrides it**: `gh pr merge 255 --admin --squash --delete-branch`. (Optional pristine-green: rerun the capture job when the lane is quiet — cosmetic only.)
- **⭐ PR #256 STAGED** (Drew GradientSky.mat, 86caj0rrg): **Devon APPROVE_WITH_NITS** (comment 4884353186, 0 blockers — byte-identical shared-mat + non-tautology-guard both HOLD) + CI structure/build/**capture** ✓ (playmode advisory cancelled). Code-only (no .github): `gh pr merge 256 --admin --squash --delete-branch`. Devon's 2 NITs → filing backlog (non-blocking): (1) `PocGradientSky.mat` left uncommitted while sibling POC mats ARE committed → untracked-artifact WARN (stray-artifact class); (2) guard docstring overclaims CI coverage (reads the CI-regenerated on-disk asset → catches same-session/local pollution, NOT committed-only pollution reaching CI — reviewer-diffing still required).
- **⭐ PR #257 STAGED** (Devon FOG, 86cajt6jb): **Drew APPROVE** (comment 4884355813, no blockers/follow-ups) + CI structure/build/**capture** ✓ (playmode advisory). Code-only (no .github): `gh pr merge 257 --admin --squash --delete-branch`.
- **Team DRAINED — verified by FULL-BOARD SCAN 2026-07-05 01:1xZ** (fresh get_tasks → Explore, 34 open tickets: 0 dispatchable · 7 sponsor-gated · 18 dep/overlap-blocked · 9 staged). Bucket-1 EMPTY. **Per-slot machine-checkable idle reasons:** Devon/Drew/Tess = 0 dispatchable dev tickets (every to-do overlaps a staged-PR file surface or hard-deps the unmerged combat POC `86cah7xxp`/unmerged PRs); build slot = no dispatchable build ticket (regen tickets `86cahxeek`/`86cahne3d` collide with staged #223/#257 Boot.unity/RenderSettings; spikes `86cabkhqn`/`86cabkhjg` = sponsor-gated infra bake); Priya = no status drift (statuses match reality); Uma = open-horizon `86cagfn8h` soak-gated; Erik = R5 `86cahne3d` dep-blocked + spikes infra-gated.
  - **UNLOCK MAP** (which Sponsor merge frees what): **#247 merge** → `86cajt6kq` (wedge-hardening) + `86caju054` (sneak-panel). **#254 merge** → `86caju052` (verifyWeaponSet CI-wire) + `86cajt6jz` (DEBUGCYCLE) + `86caju057` (NITs bundle). **#239 merge** → `86caj7896` (finger NITs) + `86caju055` (F9-indicator). **#223/#257 merge** → `86cahxeek` (stale-asset re-bake). So DRAINING THE STAGED PRs is what re-fills the team.
  - **Cadence:** ticks now do a lightweight change-check (git fetch + main HEAD + open-PR count) — re-scan only when main moves. Quiet-with-reasons until the Sponsor drains.
- HELD (overlap): `86cajt6jz` DEBUGCYCLE (#254 HeldWeaponCycleDebug.cs — after #254 merge) · `86cahxeek` re-bake (GradientSky family — after #256 merges). **Tess idle-with-reason** (CHOP done; no un-held dispatchable; can't self-review #255). Priya/Uma/Erik idle-with-reason (design/vision/combat-POC gated).
- Prior-session agents (`a03288e3c2267ccde`, `ae228d8d0536a41f2`) DEAD (drained) — work safe on #254/#247; re-dispatched fresh above.
- Build lane: #254 (CI running) + #247 fix (shell fixture, NOT a Unity build) → within ≤2 Unity-build cap. #253/#223 = Sponsor browser-merge; #239-v3 soak already CUT (`Build\soak-239-v3\`, stamp `7c1b177`).

### Orch filing backlog (ClickUp, orch-owned — personas can't write ClickUp)
**FILED 2026-07-05 01:0xZ:** iron-progression design `86caju051` (sponsor-gate — design conversation) · verifyWeaponSet CI-wiring gate `86caju052` (.github) · sneak-panel retirement `86caju054` (Sponsor-requested) · F9-not-engaged indicator `86caju055` · weapons+gradsky integration-NITs bundle `86caju057` (absorbs #254 Drew/Tess stale comments + #256 guard-docstring + #256 stray PocGradientSky.mat + #247 Check-5 pipefail grep).
**Still to file (older, pre-this-session):** #246 line-120 vacuity · #247 overlapping-drawer pointer residual · #247 `_active`-not-cleared. Previously filed: FH-PMTRIAGE-CHOP/FOG/DEBUGCYCLE/INVUI (86cajt6j8/jb/jz/k4 — CHOP+FOG now in PRs #255/#257) + settings-wedge-hardening 86cajt6kq.

## ▶▶ HISTORICAL — 2026-07-03 18:30Z (AWAY armed, cron `9c91c234` 15-min; keep-screens-alive ON pid 25008; session model = fable, personas stay opus-pinned)

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
1. **⭐ 4 SOAKS TO PLAY** (each: confirm the HUD stamp before judging):
   - **#246 island 2.0 C1** — `Build\soak-246\FarHorizon.exe`, stamp `dfcecf0`. Judge: side-profile silhouette (steep crags = bare grey stone, hero dome keeps grass→rock→snow), organic bigger island, no forest on the crags. (Verified our side: 60fps, NavMesh 99%, climb OK.) PASS → `auto-merge` label (no `.github` in diff).
   - **#247 F1/F3 settings split** — `Build\soak-247\FarHorizon.exe`, stamp `61a6a9d`. Judge: F1 = ONLY the 8 player rows; need-toggle OFF hides its decay slider (×3); F3 = dev console; typing never moves the character. PASS → `auto-merge` label.
   - **#223 sun** — `Build\soak-223-v4\FarHorizon.exe`, stamp `13a9640`. Golden-yellow LARGER sun (size 0.954) at elev 8° per your F10 dial + second-boot persistence + F2-dead-is-expected. PASS → merge popup → GradientSky cluster unblocks.
   - **#239 finger F9 dial** — `Build\soak-239-v2\FarHorizon.exe`, stamp `c962ace`. F9 → dial RIGHT-arm twist empty-hand → screenshot the HUD (Player.log churns) → Drew bakes your values.
2. **⭐ ONE-CLICK MERGE — PR #248 RT-readback spike** (`86cag93zb` AC1): **FULLY-GATED (verified 19:2xZ)** — Drew APPROVE_WITH_NITS (comment 4878435575, NITs are AC2-forward) + ALL required checks SUCCESS (structure/build/capture; playmode advisory-cancelled) + MERGEABLE. Code-only, no `.github`: `gh pr merge 248 --admin --squash --delete-branch` (or approve the auto-merge label).
2b. **⭐ BROWSER-MERGE — PR #249 PlayMode hang fix** (`86cabfa21`): **Drew's conditional APPROVE is now EMPIRICALLY SATISFIED** — playmode job COMPLETED in 2m41s (no hang/timeout) on run `28679257846`, and the results artifact (`FarHorizon-playmode-9b1cd47…`, test-results-playmode.xml) shows 279 total / 246 passed / 33 failed = exactly the known pre-existing set, with **HungerNeed + InventoryBeltHeldAxe suites ALL Passed**. **FULLY-GATED (verified 19:38Z): run `28679257846` = completed/SUCCESS — structure/build/capture ALL SUCCESS** (playmode failure = the known 33, advisory). Workflow-file PR → your browser: https://github.com/TSandvaer/Far-Horizon/pull/249 (auto-merge label hits the workflow-perm wall; browser-merge is the reliable path). On merge: `86cabfa21` → complete; triage `86cajk7vb` unblocks.
3. **Q — RT-readback AC2/AC3 go/no-go:** the spike PROVED headless captures work via `-batchmode` (no `-nographics`) + offscreen RT → removes the 1-runner capture pin. Full refactor = ~8 gates + ci.yml (your browser-merge) + one unproven bit (shipped-PLAYER batchmode device-init — AC3 validates first). Say "go RT-readback" to dispatch after #248 merges.
4. **Q — zoom `86cahnmb4` tag conflict:** body says "grill-gate CLEARED, dispatch-ready" but the `sponsor-gate` TAG remains. Stale tag? Say "zoom is go" (build-slot feature ending in a feel-soak) or "keep parked".
5. **Q — weapons re-author `86cahnmf6` (fable-class Blender R&D):** fable is available again — schedule an interactive session with you iterating on the style? (Blocks chamfer-bevel `86cacewju` + roster expansion `86cah7ym9`.)

### Carried to next session (drain 2026-07-03 ~20:2xZ)
- **⚠ FIRST INVESTIGATION next session: MAIN's capture job is RED** on post-merge push run `28680351313` (job-level verified 20:4xZ: structure ✓ build ✓ playmode red=the known 33 advisory, **capture=FAILURE — UNDIAGNOSED**). Do NOT dismiss as flake ([[86cagr0zu lesson: the capture gate is the canary]]). Next session: read the failed gate's log/artifact + request a SERIAL rerun (no other runs in flight) → if rerun greens, likely contention-flake (the run overlapped #248's merge window 19:42-19:53Z); if still red, something on main (post #248/#249) genuinely broke a windowed gate → bisect #248 (RT-readback spike files) vs #249 (test-only+ci.yml) — #248 is the likelier suspect surface (capture-adjacent code).
- **PR #251 — PlayMode 33-failure triage** (`86cajk7vb` → in review; SHA `310c7e1`): 3 real bugs FIXED (SnakeBodyChain IndexOutOfRange — `_plantOffsets` never re-synced post-Awake) + 8 stale tests fixed + 13 env-quarantined (InventoryUiInteraction class — UI-Toolkit panel doesn't build headless; multi-covered) + 9 left RED w/ 4 follow-up DRAFTS in the PR body (FH-PMTRIAGE-CHOP/FOG/DEBUGCYCLE/INVUI — **Priya files these as tickets next session**). advisory→required flip: NOT yet (Sponsor ci.yml call after the follow-ups land). `86cab7u42` CLOSED superseded (refuted premise). **Next session: Devon reviews #251.**
- **PR #250 — RT-readback AC2/3** (`86cag93zb` → in review; SHA `660c725`, CI run `28681946779` was in_progress at drain): player-device-init gate PASSED (shipped exe `-batchmode` inits D3D12, SubmitRenderRequest frame valid); 4 gates converted headless (generic/chop/held-belt/sky, all frame_check PASS locally); 4 DEFERRED with documented reasons (settings/loot/water/inv-drag = IMGUI/UI-Toolkit overlays can't composite into a camera RT; pond = soak-fragile follow-up). Runner NOT unpinned (AC4 separate). **Next session: Drew reviews → CI green → your browser-merge** (workflow-file PR).
- **Weapons burst RESULTS (86cahnmf6):** 8 Sponsor-locked FBXs + updated `weapon_palette.png` sitting UNTRACKED/MODIFIED on the orch tree + `art-src/weapons_reauthor.blend` → **HARVEST PR next session** (first free persona; fresh worktree off main + fold per [[capture-stranded-edits-via-worktree-apply-3way]] if needed). Then integration ticket `86cajkk7h` (dep: harvest).

### In flight (away loop watches these)
- **PR #249 — PlayMode hang ROOT-CAUSED + FIXED** (`86cabfa21` → in review; SHA `f1092d0`, CI run `28679257846`): the "environmental deadlock" was WRONG — real cause = 10 fixtures' shared `[UnitySetUp]` async-unloading the framework's own `InitTestScene` with an unbounded wait (never completes headless). Fixed via a bounded `PlayModeSceneIsolation` helper; full suite completes ~200s; HungerNeed + InventoryBeltHeldAxe GREEN. **Drew reviewing.** ⚠ Workflow-file PR → **your browser-merge** when gates+review green: https://github.com/TSandvaer/Far-Horizon/pull/249
- **NEW TICKET `86cajk7vb`** — triage the 33 pre-existing PlayMode failures the fix EXPOSED (InventoryUiInteraction, ChopTree HoldChain, LeftClickConsume, SnakeCombat, WaterWaves/WorldLook…). Job stays ADVISORY until triaged. Hard-gated on #249 merging.
- **#248 CI** — capture job was PENDING; on full green the one-click above is complete.
- 3 follow-up NITs to file (orch): #246 line-120 vacuity guard · #247 overlapping-drawer pointer residual · #247 `_active`-not-cleared.
- Docs+memory corrected: the stale "playmode deadlock is environmental / UUM-142421" claims → the InitTestScene root cause (unity-conventions.md §CI architecture + the advisory-playmode memory).

## ▶▶ HISTORICAL — 2026-07-03 10:05Z (AWAY re-armed, cron `d9512461` 15-min; keep-screens-alive ON pid 28360; ClickUp MCP healthy again)

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
1. **⭐ #239 F9 pose-dial soak** (you chose the F9 route before stepping away): once CI run `28652781354` is green the orch cuts `Build\soak-239-v2\` from the artifact (stamp = merge-ref sha from the artifact name) and updates this item with the exact path+stamp. **Test:** F9 → empty-hand idle → dial the RIGHT-arm twist (currently −50°Y; left is +22°Y) until the hand reads natural → note the values here/in chat; also re-confirm thumb-wraps-grip on axe+spear. Drew bakes your values on the same branch → merge.
2. **⭐ ONE-CLICK MERGE — PR #241 test snapshot/restore** (`86cahvntg`): peer APPROVE_WITH_NITS zero blockers (comment 4874686393); pure-test PR, no soak surface. Staged when its capture rerun is green: `gh pr merge 241 --admin --squash --delete-branch`. (Its NIT — snapshot omits skybox `_SunDirection`/`_SunSize` — is absorbed into `86caj0rrg`, same-cluster downstream.)
3. **⭐ #223 sun re-soak — BUILD READY:** capture rerun GREEN (run `28649382726`, post-stray-exe-closure) → build cut to **`C:\Trunk\PRIVATE\Far-Horizon\Build\soak-223-v3\FarHorizon.exe`**, baked HUD stamp verified **`9f0dfb3`** (read from resources.assets). **Test:** sun low+warm at elev 8° (golden, not overhead-white), hue per your dial (.80/.815/.089), size .986 — does the BAKED sun now match what your dial showed? Orch still owes the one-off `-verifySky` (deferred until runner-1 has no capture in flight — a local exe launch starves captures, see the new unity-conventions entry); item updates when it's run. Do NOT touch the old `soak-223-v2` (prefs-polluted evidence).
3b. **⭐ ONE-CLICK MERGE — PR #242 stale-comment XS** (`86cah6n9w`): ALL required CI green (structure/build/capture pass, run `28653703933`) + Devon-type APPROVE (comment 4875117899, comment-only verified). `gh pr merge 242 --admin --squash --delete-branch`.
3c. **⭐ BROWSER-MERGE — PR #243 warm-runner corrupt-build detect+heal** (`86cagr0zu`): required jobs ALL GREEN on run `28654442411` (structure/build/capture — the reviewer's hold-until-green NIT is satisfied; advisory playmode ignored) + Drew-type APPROVE_WITH_NITS zero blockers (comment 4875247985). Workflow files → your browser `--admin` merge: https://github.com/TSandvaer/Far-Horizon/pull/243. On merge, `86cag93zb` (RenderTexture-readback captures) unblocks in the ci.yml lane.
3d. **#239 CI note:** its capture job failed at 09:55–09:58Z — straddles your soak-window closure, so likely the same starvation (hypothesis); #241's capture rerun is in flight now, #239's rerun gets requested serially after it. The F9 soak build (item 1) cuts once #239's run is fully green.
4. **Q — build-queue jump while the island lane is held:** C1 relaunch waits on your word; C2/C3 hard-gate behind C1. When the current CI-lane work drains, the only non-gated feature pool is the COMBAT cluster (Q2 ordered island first — so pulling combat is YOUR queue-jump call). Least-dependent candidate: `86cah7y5b` (find-in-world weapon acquisition). Say "pull combat" or "island stays first".
5. **Q — weapons re-author `86cahnmf6` (Blender, unified style):** personas can't reach the Blender MCP — this is an orch R&D burst (fable-class creative per model policy) and you wanted interactive iteration on style. Schedule for a present session? (Blocks `86cacewju` chamfer-bevel + feeds `86cah7ym9` roster expansion.)
6. **C1 relaunch on your word** — devon-wt branch `devon/86cahwx6w-island2-c1` @ `c5086df` all pushed; remaining: EditMode check → regen-if-needed → PR + Self-Test + local soak `Build\soak-c1\`. (Note: devon-wt is currently borrowed for `86cagr0zu`; C1 re-checkout is one command, no work lost.)

### In flight (away loop watches these)
- **Devon `86cagr0zu`** warm-runner corrupt-build detect+clean (branch `devon/86cagr0zu-warm-runner-corrupt`; ci.yml authorized; PR will need your browser-merge).
- **Drew `86cah6n9w`** stale airControlAccel comment XS (branch `drew/86cah6n9w-settingscatalog-comment`).
- CI: #239 run `28652781354`; capture reruns on #223 (`28649382726`) + #241 (`28650413512`) — reruns triggered AFTER the stray soak exe (PID 7012, confirmed culprit of the 09:2x capture hangs) was closed.

---

## ▶▶ HISTORICAL — 2026-07-03 01:25Z (AWAY armed overnight; drain-end staged items + morning queue)

> **DRAINED 2026-07-03 ~06:15Z morning walkthrough:** (1) #236 APPROVED → auto-merge label added; snake-soak build cuts from post-#236 main. (2) #237 = REVIEW-FIRST → Drew-type reviewer dispatched; browser-merge staged on APPROVE. (5) Q2 ordering = **island-2.0 C2/C3 next** after C1 (DECISIONS.md 2026-07-03). (3)+(4) are follow-on actions, proceeding. Items below are HISTORICAL.

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough in the morning)
1. **⭐ ONE-CLICK MERGE — PR #236 FPS counter:** ALL required CI green on the re-integrated head `e944fea` + Devon APPROVE_WITH_NITS (content-pinned, code byte-identical verified post-re-bake). Approve → orch adds the `auto-merge` label. **Merge #236 BEFORE cutting the snake-soak build so the FPS counter ships in it.**
2. **⭐ BROWSER-MERGE — PR #237 ci.yml bundle** (5 tickets: retention-days everywhere / -verifyHeldBelt gate / sky gate / gate hardening / drag-ghost gate): ALL required CI green — the new gates validated against the PR's own build. `.github/` → your browser `--admin` merge ([[auto-merge-fails-on-workflow-file-prs]]). Un-reviewed (drain skipped the review) — say the word if you want a peer review first.
3. **⭐ SNAKE SOAK (post-merge build):** the snake is ON MAIN (#234 merged 19:30:56Z). After #236 merges, the orch cuts a fresh main build → `Build\soak-snake\` + the find→aggro→telegraph→bite→kill checklist. The combat-feel verdict transferred from #224 lands HERE.
4. **#223 sun-fidelity re-soak (after Devon reviews Drew's fix):** Drew's fix is PUSHED (`0995cb3` + bake `d34acf8` — YOUR dialed values: elev 8° / hue .80,.815,.089 / size .986 + a verify-camera fix so the 8° sun verifies against sky not canopy). Next session: Devon reviews → CI → re-soak build: does the BAKED sun now match what your dial showed?
5. **Q2 CARRIED — build-queue ordering:** R2b shadow trim (data-GO: 87.3% of draws are shadow casters; soak-gated, re-opens flicker percepts) vs island-2.0 C1 (ungated) vs zoom (dispatch-ready) vs combat cluster (5 tickets). Orders the next feature wave; the overnight loop only takes the non-gated lane.

### Overnight-dispatchable (the away loop works this list, non-gated only)
- **island-2.0 C1 `86cahwx6w`** (ungated since #230 merged; REGEN — POC scene only, no Boot.unity → collides with nothing pending).
- **Snake NITs `86cahzycp`** (SnakeAI/SnakeBodyChain/SnakeVerifyCapture now ON MAIN post-#234; pure code XS).
- **Finger fix `86cahnmjv`** (character rig defect — repeat Sponsor annoyance; code+rig, no regen expected).
- R5-impl `86cahne3d` + stale-assets `86cahxeek`: HOLD — their regens collide with #236's pending merge (Boot.unity); dispatch after the Sponsor one-clicks #236.

---

## ▶▶ HISTORICAL — 2026-07-01 ~18:07Z (AWAY armed, cron `e493b763` 15-min; keep-screens-alive ON pid 26672)

This session opened LOCAL; merged docs **#221 + #222** (main @ `6278156`), then switched to AWAY 18:07Z. **⚠ Subagents CANNOT reach the ClickUp MCP ("Not logged in") — orch owns ALL ClickUp writes; personas report board changes back, they don't write.**

**IN FLIGHT:** #224 chop-gate regression **FIXED by Devon** (`d2bbbdf`) — **root cause CONFIRMED (not guessed):** the proximity-auto `SpearPickup` sat at `(2,0,6)` = exactly `pickupRadius` (2.0u) from spawn `(0,0,6)` → auto-grabbed the spear into belt **slot 0** (the default-selected slot) on frame 1, bumping the crafted axe to slot 1 → `IsAxeSelectedInBelt=FALSE` → chop no-op → gate red. (Drew's hypothesis 2 refined; hypothesis 1 — shared Attack state — ruled out; chop-path code byte-identical to main.) Fix relocates `SpearPickupPosition`→`(4,0,9)` (5u clear, mirroring AxePickup) + 2 regression guards (`ChopSceneTests` scene-geometry + `InventoryFacadeTests` axe-select ordering). **CI run 28540937383 GREEN** — structure + build/EditMode + **capture (chop gate)** all SUCCESS (playmode = advisory cancel, non-blocking). **Drew APPROVED** the fix (guards verified real, root cause empirically confirmed, no residual concern). #224 = code-gate GREEN (peer APPROVE + CI build/capture) → **combat soak STAGED for the Sponsor** (⭐ item 4 below). **NO agents in flight.** Build slot HELD by #224 (open PR, awaiting your soak + merge — single-build-slot cap forbids a 2nd build ticket). → **Board fully-gated; away loop QUIET** with cited per-slot reasons (build slot = #224; combat follow-ups dep on #224 merge; split gated on #223 soak+categorization; ci-cluster Sponsor-merge-gated; snake/boat grill-first; Priya can't write ClickUp; no un-gated Uma/Erik work). Next candidate on merge = next-island POC `86caa9zpp` (or the split once #223 soaks) — confirm priority.
> **DOC-ON-MERGE (defer per unmerged-API rule):** a proximity-auto pickup within `pickupRadius` of spawn auto-grabs into belt slot 0 on frame 1, silently stealing the default-selected slot from a later-crafted item → verb-gates keyed on "is X selected in belt" fail with GREEN EditMode; the windowed capture gate is what caught it. Capture into a Unity doc once #224 merges.

### ⭐ SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return)
1. **⭐ #223 soak — CODE GATE GREEN** (Drew APPROVE_WITH_NITS; the one NIT is filed as `86cahc2y7`, non-blocking — shipped exe is correct). **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-223\FarHorizon.exe` — confirm HUD stamp reads `b8d6e96` before judging. **Test:** (1) sun warm + low (~12°, golden not white/overhead); (2) debug overlay boxes DON'T overlap (world-look box above the sneak-isolation box); (3) **F10** toggles sneak + world-look together; (4) **F1** is free. **Verdict:** good → orch adds `auto-merge` label (code-only, no `.github` → label works); bad → back to Devon. **#223 blocks the panel-split.**
2. **Panel-split categorization confirm (`86cah8ukr`)** — needed before the split dispatches (also hard-gated on #223 landing, which frees F1). Confirm the player-vs-dev row list + the dev-console key (**F3** proposed) + 3 borderline rows: inventory-slots (rec DEV), walk/run speed (rec DEV), UI-text-scale (rec PLAYER/accessibility).
3. **⭐ Combat POC swing-approach — RULING NEEDED (`86cah7xxp`/PR #224).** #224 ships a PLACEHOLDER swing (reuses the existing chop `Attack` state, attackSpeed-scaled, no new clip) to prove the reach/damage/status SYSTEM. The FINAL per-weapon swing needs your call: **procedural additive-offset** (per `procedural-animation-verbs.md`) vs a **new Mixamo attack clip** (per `[[chop-swing-mixamo-clip-not-procedural]]`, which you previously preferred) — for the axe-chop + spear-thrust. #224 CAN merge on the placeholder (system proven); the distinct swings land as a follow-up ticket once you rule.

4. **⭐ #224 Combat POC soak — CODE GATE GREEN** (`86cah7xxp`; Drew APPROVE + CI build/capture SUCCESS run 28540937383). **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-224\FarHorizon.exe` — confirm HUD stamp `3756e1c` before judging. **Test (AC11 combat-feel):** craft the axe + spear; **left-click = one strike** (active input, not proximity-auto); the **spear out-reaches**, the **axe hits harder up close** (the reach/damage contrast should be FELT); **bleed** ticks HP down over time; the **snake** is damageable + its bite hurts you; **HP regen only while warmth/hunger/thirst satisfied**; **tiered death** per difficulty (easy faint-in-place / med respawn-at-camp-keep-items / hard respawn+drop-items). ⚠ The per-weapon **swing is a PLACEHOLDER** (reuses the chop Attack state) — judge the SYSTEM + feel, NOT the swing polish (swing ruling = item 3). **Verdict:** good → orch adds `auto-merge` label (code-only, no `.github`); needs work → back to Devon. Merging unblocks the 5 combat follow-ups.
5. **Build-slot priority during the soak-wait:** the single build slot is HELD by #224 (open PR). If you want the build lane busy while your #223 + #224 soaks wait, the next dispatchable build-lane ticket = **next-island POC `86caa9zpp`** (design-locked, build-ready) — but starting it commits the slot (the split waits behind it). Say "start next-island" to fill it, or leave it to hold for the split (once #223 soaks + you confirm categorization). *(Not auto-started — it's a priority/sequence call.)*

**Superseded/pending:** #220 (`86cabeqwf`) closes as superseded-by-`86cah8ukr` when the split dispatches. NIT `86cah6n9w` (air-control stale comment) — verify it's carried into the split, else keep open.

> **Everything dated 2026-06-30 and earlier below is HISTORICAL — all those PRs/soaks merged/resolved before this session.**

---

## ▶ ACTIVE PLAN — 2026-06-30 (present/local mode; pulse cron `4b8e4dcc`, 5-min)
- **6 non-soak PRs MERGED this morning:** #188/#189/#191/#195/#196/#198 → main `6991028`; tickets flipped complete (+ new #189 tracking `86cag6mr5`).
- **#197 crouch-STUTTER fix — Devon DONE, pushed `f159f2e`** (branch `devon/86caa3kur-crouch`, ticket `86caa3kur` → ready for qa test). Confirmed root cause: `WasdMovement` direct-drove `agent.velocity` each frame while the `NavMeshAgent` sim braked against it (`autoBraking=true`/`accel=30`/no path) → at slow sneak speed the ramp-noise dominated the per-frame step = hitching. Fix: `EnsureSmoothDirectDrive()` → `autoBraking=false`+`accel=1000`; camera-lead jitter fixed for free. #186 + #197 logic byte-unchanged; new `-verifySneak` instrument. **CI building on `f159f2e`.**
- **Drew IN FLIGHT reviewing #197** — agentId `a641a050fceb9c5ea`, drew-wt (absorbs QA, Tess out). Review-in-parallel-with-CI.
- **QUEUED — sun-lower (Drew, #194 branch, ticket `86cag25az`, FOLD into PR #194):** lower the warm-gold sun toward the horizon (baked too high to see; Sponsor likes the sky). Dispatch **when #197 CI green + Drew's #197 review done** (frees the build slot AND drew-wt — Drew can't review #197 + build #194 at once). Then re-soak #194 with a VISIBLE sun (judge hue/size) → Sponsor browser-merges (`.github` → `--admin`).
- **#197 next:** on CI green + Drew APPROVE + capture evidence → serve Sponsor the #197 re-soak (smooth crouch-walk). Wake = Drew's review completion + the 5-min pulse.

**↻ UPDATE (later 2026-06-30 — Whip fan-out + Sponsor decisions):**
- Drew **APPROVED** #197 (comment 4840939534). Waiting on #197 `unity` CI (run 28427300034) → on green: serve Sponsor re-soak + slot frees.
- **Non-build lane FILLED:** Erik `86caaz4un` Game Juice research (agentId `a0dd614a84376156e`, erik-wt; can't-git → orch harvest-PRs his note) + Uma `86cafffe8` open-horizon EXPLORATORY spec (agentId `a8eb02a9cae44469a`, uma-wt — ⚠ mountains = Sponsor VISION call; Uma explores options, Sponsor judges/vetoes). Priya scan+hygiene done (closed moot `86cacer85`).
- **Build-slot queue (Sponsor priority order):** (1) **CI-split `86cafz9tg` FIRST** → makes runner-2 safe + DOUBLES the build lane (the serialization fix); (2) sun-lower `86cag25az` (fold into #194); (3) airControl `86caambxh`.
- **Sponsor decisions 2026-06-30:** runner-2 = DON'T spin up yet (breaks windowed captures, #182→#190 revert; land CI-split first). Hook hardening **APPROVED** = extend `orchestrator-anti-idle-stop.sh` to fire on partial-idle/stale-scan (not just full-idle) + a SessionStart resume-scan nudge — orch implements + shows diff.
- **Orch harvest pending:** Erik's 4 research notes (3 stranded: lowpoly-sky/grass/stylized-sky + the new Game Juice) → harvest-PR to main.

**↻↻ STATUS (later 2026-06-30):** Erik DONE · Uma DONE (**PR #199** open-horizon spec, vision A/B/C queued) · Drew DONE → **PR #200** anti-idle hardening (`.github` wall → Sponsor browser-merge after review) · Priya DONE → **PR #201** Erik-notes harvest + `.claude/docs/game-juice.md` + index (docs-only → `auto-merge` label after Sponsor OK). **Devon RUNNING** reviewing #200 (agentId `a9c6b2e60082c4f5d`).
- ⚠ **BOTH self-hosted runners OFFLINE** (`far-horizon-local` + `-2`, gh-api-confirmed 08:18Z) → #197 `unity` CI stuck queued ~59 min (since 07:19Z, run 28427300034). **Re-soak + CI-split + sun-lower ALL blocked until Sponsor starts runner-1** (`C:\actions-runner-farhorizon`, interactive as logged-in user; runner-2 stays OFF — breaks captures). Surfaced to Sponsor.
- Wake: #197 CI (once runner online — bash poll `b57rzh59c` watching) + 5-min pulse.
- **Devon APPROVE_WITH_NITS on #200** (comment 4841343275; 13/13 tests + adversarial probes pass). NIT (non-blocking, to file): Branch B's `prior_tick` counting-grep includes the bare `never idle` alternative → a Sponsor message containing a cron-phrase can mis-anchor + fire a (self-correcting) false STALE block; fix = drop the bare `never idle` from the COUNTING grep only. #200 ready for Sponsor diff-review + browser-merge.
- ⚠ **Runner re-confirmed OFFLINE 08:21Z** (gh-api ×2) despite the runner window showing a STALE `06:25:04Z Listening for Jobs` line — the 62-min-queued #197 job was never grabbed (a connected idle runner grabs in seconds) = corroborates disconnect. Sponsor asked to RESTART runner-1 (Ctrl+C + `run.cmd`, interactive); watch for a fresh current-timestamp Listening line. Orch to re-verify via gh api after restart.
- **Staged for Sponsor merge:** PR #201 (`auto-merge` label after OK) · PR #200 (browser-merge, `.github`) · PR #199 (vision pick first). #197 crouch re-soak + sky #194 sun-lower gated on runner.

**↻↻↻ RUNNER INVESTIGATION DONE + decisions (2026-06-30, /investigate 3-angle):**
- **Decisions (Sponsor):** (1) **CI-split `86cafz9tg` now** → RT-readback as a follow-up; (2) **build the runner watchdog**.
- **CI-split design (Investigator A, HIGH-conf):** split monolithic `unity` job → `build` (headless build+EditMode, ANY runner, uploads `FarHorizon-Windows-<sha>` artifact) + `capture` (7 windowed gates, PINNED to runner-1 via a dedicated `capture` runner-label, downloads artifact, absolute concurrency `unity-capture`=1-ever) + `playmode`(needs build). runner-2 gets only `[self-hosted,windows,unity]`. Runner-2 already set up+validated (`team/erik-consult/second-runner-setup-steps.md`, cache-isolated, 0 EPERM, ~1.4–1.6× gain). **→ dispatch Devon with THIS design when #197 CI frees the slot** (CI-split is the Sponsor's #1 build-lane priority).
- **RT-readback follow-up FILED `86cag93zb`** (Investigator B): refactor ~8 capture gates to RenderTexture→ReadPixels→PNG (headless, no swapchain) → removes the 1-runner pin entirely + N-runner parallelism. SPIKE-one-gate-first AC. Sequenced AFTER the CI-split.
- **Watchdog: Drew RUNNING** (agentId `a16729749f5853b70`, PR pending) — PS Scheduled Task polling `gh api .../runners`, kills+relaunches `run.cmd` on offline-but-alive (the S0-Modern-Standby long-poll-drop that caused today's stall). Sponsor installs interactively.
- 3 runners NOT worth it on one box (Amdahl ~1.5×); cloud blocked by Unity licensing (Build Server $1,500/seat). **2 runners = the sweet spot.**
- #197 `unity` CI now **in_progress** (runner online+busy 08:30Z); poll `b57rzh59c` watching → on green, serve crouch re-soak + dispatch the CI-split.

**↻↻↻↻ LATEST (2026-06-30):** #197 unity CI **SUCCESS** → crouch re-soak SERVED (`Build/soak-197-v2/FarHorizon.exe`, stamp `0e8f518` = merge-ref). **Devon RUNNING the CI-split** `86cafz9tg` (agentId `a49ca3cf079931297`, devon-wt, A's 3-job design + ticket constraints, `.github` edit authorized → in progress; reviewer Drew; Sponsor steps on merge: branch-protection required-checks `structure`+`unity`→`structure`+`build`+`capture`, + add `capture` label to runner-1). Drew's **watchdog = PR #202** (review + interactive install). **SPONSOR QUEUE:** PR #199 (vision A/B/C) · #200 (anti-idle, browser-merge) · #201 (harvest, label-merge) · #202 (watchdog).

**↻↻↻↻↻↻ #203 CI-split DEV-COMPLETE + STAGED (codereview done):** built (Devon) → 2-Opus codereview (comment 4841684229) found 1 real regression (playmode `needs: build` could overlap captures once runner-2 online — regressed PR #80's serialization invariant) → Devon FIXED it (`2c8ae19`: playmode joined the `unity-capture` concurrency group; verified playmode.concurrency==capture.concurrency lines 702/386) → review-clean, `86cafz9tg` → ready for qa test. **Sponsor steps to merge #203:** (1) add `capture` label to runner-1 (`config.cmd --labels self-hosted,windows,unity,capture --replace`, interactive — unblocks the capture-job CI) · (2) browser-merge (`.github` wall) · (3) flip branch-protection required-checks `structure`+`unity`→`structure`+`build`+`capture`. **RT-readback `86cag93zb` gated on #203 merging.** Devon now idle-with-reason.

**↻↻↻↻↻↻↻ #197 crouch — TRUE CAUSE FOUND via the instrument (attempt 3 fix pushed `3be3bfb`):** the trace REFUTED animation (CrouchWalk plays smooth — constant state, monotonic normalizedTime, clean wraps). Real cause = **NavMeshAgent RVO collision-avoidance perturbing the commanded velocity** at slow sneak speed (SNEAK step CoV 0.086 = 21× WALK baseline 0.004). Fix = `obstacleAvoidanceType = NoObstacleAvoidance` in `SmoothDirectDriveConfig` (single-player → nothing to RVO-avoid; baked NavMesh handles static geo). EditMode 757/757. Verify-sneak CI run `28434750900` serial-queued; orch watch `b3j12sut8` → on green, check the CoV dropped to ~baseline + serve Sponsor re-soak. **DOC-ON-MERGE:** once #197 lands + Sponsor confirms, capture the NavMeshAgent direct-drive gotcha into a Unity doc (direct `agent.velocity` drive needs `autoBraking=false` + high `acceleration` + `NoObstacleAvoidance` to avoid sim-perturbation jitter at slow speeds — combines the #197 v2 + v3 findings). The instrument (committed `ba6175c`) earned its keep — refuted the animation guess that attempt-2 would've fixed blind.

**↻↻↻↻↻ #197 re-soak (v2) REJECTED — still hitches "between each walk animation, two steps repeated"** = ANIMATION-loop hitch (attempt-1 smooth-drive fixed the wrong/velocity layer). **Attempt 2 = INSTRUMENT-FIRST (/unstick):** **Drew RUNNING** (agentId `a13221256e92fbddc`, on #197 branch `devon/86caa3kur-crouch`) — extend `SneakVerifyCapture` to dump per-frame Animator state + clip normalizedTime + playback speed + #186 foot-sync multiplier + a foot-sync disable toggle → confirm WHICH of (clip loop-seam / foot-sync stalling clip / Animator state-reentry) → then fix → Sponsor re-soak. Devon (CI-split) + Drew (crouch) both build-lane; CI serializes on the single runner. Both in flight.
- **Sky #194 decision (Sponsor):** "lower on #194 branch, re-soak, then merge" — one cycle; don't merge an invisible sun.
- auto-status = **local/on** (present-mode pulse).

---

Sponsor items accumulated during away mode. Drain via `/sponsor-questions-walkthrough` on return.

Armed away 2026-06-29 ~14:51Z (cron `d16761cc`, 15-min). auto-status mode=away.

> **DRAINED + switched to LOCAL 2026-06-29 ~17:44Z** via /sponsor-questions-walkthrough: #186 + #194 soaks APPROVED (merging); the 5 staged merges approved (#192 merged, #186 labeled, #193/#191 sequencing, #188/#189/#194 = Sponsor `--admin`); sun-lower follow-up `86cag25az` queued (decision: lower the sun). Items below are HISTORICAL.

> **RE-ARMED AWAY 2026-06-29 ~17:50Z** (cron `ac536690`). Merged since: #192, #186. Auto-merging: #193 (#191 to sequence after). **STILL PENDING SPONSOR `--admin`** (you run on return — `.github/` scope, label can't): `gh pr merge 188 --admin --squash --delete-branch` · 189 · 194. **Queued:** sun-lower fix `86cag25az` (after #194 merges + slot frees) — re-soak to dial hue/size.

> **BOARD DRAINED — TEAM QUIET (2026-06-29 ~late, Priya confirmatory scan).** 0 agents in flight (all dispatches completed). All 32 open tickets = staged-for-you (the 6 merges + 3 soaks below) / sponsor-gated / soak-gated / blocked-by-a-staged-PR (airControl `86caambxh` ← #197 on WasdMovement.cs) / single-build-slot-serialized / ci.yml-held (the 8-ticket CI-lane cluster waits on the `.github/` PRs #188/#189/#194 clearing). Spikes `86cabkhqn`/`86cabkhjg` = sponsor-gated (NOT superseded). Water-mat `86cacer85` = MOOT (already fixed by #130; close pending — orch flips on next reconcile). **Nothing autonomously dispatchable remains** → quiet until your return. Draining the staged merges+soaks below unblocks the `.github/`, build-slot, and sun-lower lanes.

## ✅ Chop-test scare RESOLVED (2026-06-29 ~18:00Z)
Cause C — Devon's LOCAL stale Library import, NOT a main regression. Drew verified via #195's clean CI (run 28392083749): EditMode **746/746**, `ChopAnimatorControllerTests` 5/5 pass; `Melee_Attack.fbx` + controller present on main. Main EditMode is GREEN; NO merge blocker. (#186 regen regression + clip-never-imported both ruled out. Devon to clear Library/reimport on his next dispatch — local-only.)

## Merges (sponsor-gated — orch cannot merge to protected main even under bypass)

- **#192** perf(bushes): BerryBush self-disable while ripe (`86cabnjv8`) — **Drew APPROVE** (body verdict, comment 4833835294) + required CI green (unity+structure PASS, run 28380188471). `UNSTABLE` = advisory playmode only (always-hangs, non-blocking). Pure-code, NO `.github/`, NO soak → eligible for the **`auto-merge` label after your present-mode approval**.
- **#188** ci: structure_check NUnit-by-content (`86cafk5vb`) — review-clean, CI green. Touches `.github/` → **manual**: `gh pr merge 188 --admin --squash --delete-branch` (workflow-scope token).
- **#189** ci(capture-gate): wedge-harden — Devon APPROVED, CI green. Touches `.github/` → **manual**: `gh pr merge 189 --admin --squash --delete-branch`.
- **#191** docs(state): board catch-up STATE update (Priya) — XS docs-only, no code → `auto-merge` label after approval (or fold).
- **#193** refactor(debug): INudgePanel{IsActive} extraction (`86cafz9jr`) — **MERGED ✅** (ticket complete).
- **#195** chore: mechanical NIT bundle (#184 enum-compare `86cafu81n` + #183 hunger-const `86caft905`) — Drew APPROVE (comment 4835533342) + CI **746/746** (run 28392083749). Pure-code, no `.github/`, NO soak → eligible for the **`auto-merge` label after your approval** (held in away mode).
- **#196** docs(spikes): 2nd-runner-breaks-windowed-captures finding write-up (`86cafza2a`) — docs-only (`team/spikes/...md`), no code/CI. Orch-verified accurate (matches the A/B finding). No soak/peer-review needed → **`auto-merge` label after your approval**.
- **#198** chore(settings): register inventory slots/belt/stack into the Dev Tweak Console (`86cabfa4e`) — Devon APPROVE (comment 4836013002, byte-identical-untouched HARD-verified) + CI green (run 28395291385). Made the consts dev-adjustable (model-rebuild + mutable `ItemDef.ResourceStackSize`, domain-reload-safe). No `.github/`, NO soak → **`auto-merge` label after your approval**. NIT (non-blocking): InventoryUI caches its model on open → resizing slots while the pack is OPEN needs close+reopen to show (self-heals).

## Soaks (your eyes — feel/visual)

- **#186 RE-SOAK — READY ✅** (Drew APPROVE PR comment 4834474751 + required CI green, run 28384493683) — idle ("calm but alive" via your Breathing Idle.fbx) + walk **foot-sync** (walk ≈1.53×, run unchanged) + walk **asymmetric-damp** (start 0.04 / stop 0.18), EditMode 746/746. **FINGER: CONFIRMED CLEAN** — Devon re-verified with 6 close hand captures (`ci-out/hands_*.png`, build 24a5abc) + a rotation trace; **orchestrator independently verified** hands_left/right.png — normal relaxed hand, no mangle. Likely cause of what you saw: the OLD static idle posed the finger badly (build 7e31635) / the Mixamo web-preview renderer; the new Breathing Idle poses it cleanly. A `-verifyHands` regression gate now guards it. NOTE: confirmed for the **empty-handed idle** (the state you flagged); if a finger issue shows while HOLDING the axe, that's the separate CastawayFingerCurl path. **Soak build (HEAD-exact CI artifact, run 28384493683):** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-186-v2\FarHorizon.exe` — **HUD stamp should read `5135597`; CONFIRM it before judging** (that's the CI PR-merge-ref sha — NOT the branch HEAD `71a016e`; CI stamps the merge commit, confirmed via a #194 CI capture). **Judge:** (1) idle calm-but-alive (breathing + head-glance, not static); (2) walk legs match speed — NO skating; (3) walk-start snappy — NO slide before feet move; (4) finger clean in the idle pose. **Verdict** → I add the `auto-merge` label (no `.github/`, label works) or back to Devon.
- **#186 item-6 NIT** — you said "overall approved with NIT" but didn't name the NIT. **What is it?**
- **Sky soak (#194) — READY ✅** (Devon APPROVE_WITH_NITS comment 4835224295, 0 blocking + required CI green: unity+structure SUCCESS) — sun-disk POC: a warm-gold sun added to `GradientSkybox.shader` (clouds untouched). **Soak build:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-194\FarHorizon.exe` — **HUD stamp `bb93993`** (CI merge-ref sha; confirm before judging). Look UP/around in-game to see the sun. **Judge:** sun hue/size from live gameplay framing (defaults are dial-from, NOT final-tuned) + cloud-vs-sky contrast still reads. **Verdict → approve = manual `gh pr merge 194 --admin --squash --delete-branch`** (touches `.github/workflows/scripts/verify_sky_gate.sh` → auto-merge label fails on `.github/`, like #188/#189), or back to Drew to dial. NITs (non-blocking): PR-body "byte-identical ×3" overstated; stale `_MainLightPosition` doc comments (folding into CI-wiring ticket `86cag1xn0`).
- **Crouch soak (#197) — READY ✅** (Drew APPROVE comment 4835786272 + required CI green: unity+structure SUCCESS) — Ctrl-hold crouch (`86caa3kur`), builds on #186's crouch lane. **Soak build:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-197\FarHorizon.exe` — **HUD stamp `673f2c4`** (merge-ref; confirm before judging). **Test:** hold Ctrl (stand→crouch idle) · Ctrl+WASD (sneak-walk, slower than walk) · Ctrl+Shift+WASD (crouch WINS over sprint — stays sneak) · release Ctrl (back to normal) · jump while crouched (airborne suppresses crouch). **Judge:** does the crouch read + feel right. **AC6 open Q:** the camera does NOT lower when crouched (ticket-scoped as YOUR call, default = leave as-is) — tell me if you want a camera-lower follow-up. **Verdict → approve = `auto-merge` label** (no `.github/` → label works), or back to Devon. (Devon also recommends a `-verifyCrouch` CI capture-gate follow-up — ci.yml, joins the held lane.)

## Notes / context

- **Finger root cause** (investigation wf `w6un30dyj`): Mixamo auto-rig finger mis-weight on the chunky hand. Rig is **Generic/transform-path** → a weight-repaint preserves all clips by bone-path (no re-download). **Team task** — you do NOT need to re-enter Mixamo unless the last-resort re-model fallback hits.
- **Stale stamp note:** the soak you ran was build `7e31635` (my earlier "be35459" was wrong — conflated a CI headSha with the embedded stamp). Next re-soak build I'll verify is HEAD-exact.
- **Orch-main stray:** `Breathing Idle.fbx` is an untracked copy in the orchestrator's main checkout (Devon copies it into his worktree + commits to #186). Clean the orch-main copy after #186 lands.

---
## ▶▶ RESUME 2026-06-30 (present-mode walkthrough — Sponsor decisions)
- **#197 crouch CI = capture-flake CONFIRMED** (chop gate wedged after teleport-to-tree; build green + sneak gate passed). Re-running failed unity gate (watcher `bxks2ov8q`) → on green serve re-soak (stamp `ddc79f0`).
- **#203 CI-split — Sponsor chose VALIDATE-THEN-MERGE:** (a) Sponsor adds `capture` label to runner-1 → (b) orch re-triggers #203 CI, confirm build+capture green → (c) Sponsor browser-merges → (d) Sponsor flips branch-protection required-checks `structure`+`unity`→`structure`+`build`+`capture`. Proves the restructure before it gates main.
- **#200 anti-idle hooks — Sponsor APPROVED browser-merge** (`gh pr merge 200 --admin --squash --delete-branch`, `.github` wall). Orch files the non-blocking NIT (drop bare 'never idle' from the COUNTING grep only) as a tiny follow-up.
- **#199 open-horizon — Sponsor picked OPTION A (full open ocean):** remove the horizon mountains; open blue water dissolving into warm sky 360°. B = pre-planned soak fallback if empty horizon reads cheap. Vision decision → DECISIONS.md + file impl ticket (open-horizon impl, the fog-dissolve + remove-mountains + next-island occlusion-reveal). Spec #199 label-merges as the record.
- **#199 reveal feel — Sponsor picked NATURAL FOG-HAZE reveal** (Approach 1: island beyond fog limit, sharpens on approach; reuses Option-A's fog dissolve). Carries to the future next-island/journey POC impl ticket.

**↻↻ #197 v3 RE-SOAK REJECTED + /unstick (2026-06-30 present-mode):** Sponsor v3 soak (RVO-fix build, stamp `ddc79f0`): crouch wins over run ✓ + works, but STILL not smooth — "jerks after every SECOND step (left, right, jerk, repeat)" = ONCE PER GAIT CYCLE (L+R). RVO fix smoothed velocity but a cycle-periodic jerk remains. Sponsor invoked **/unstick**. Attempt-4 path = INSTRUMENT not blind-fix: **Devon RUNNING** (agentId `af28eb7349f63ed6e`, devon-wt → #197 branch) building a RUNTIME isolation handle: (1) Danish-safe key toggle to disable #186 foot-sync coupling while sneaking, (2) key toggle to snap sneak-speed→walk-speed, (3) live on-screen readout (agent.vel.mag / animator speed param + .speed / foot-sync multiplier / clip normalizedTime). Sponsor soaks → flips foot-sync off → reports if the 2-step jerk vanishes = isolates cause (foot-sync vs clip loop-seam). #197 → in progress. Prime suspect: #186 foot-sync snapping per stride.
- **maintain-docs (3/3 proposer consensus, PENDING HARVEST):** add a "CI capture-flake vs real-regression triage recipe" section to `.claude/docs/unity-conventions.md` (build-artifact-exists=build-passed; gate verdict lives in `verify-<gate>.log` inside the `<gate>-caps` artifact NOT unity-ci-logs; artifacts downloadable mid-run via gh api; wedge-flake = log stops after checkpoint w/ no GATE-FAIL line; rerun needs terminal run; `rerun --failed` reuses merge-ref so stamp unchanged). NOT applied (would strand on orch/coordination) — fold into the orch-clean-branch harvest.

**↻↻↻ #203 CI-split VALIDATED (2026-06-30, after Sponsor added `capture` label to runner-1):** run 28433229234 — structure ✅ + build ✅ + capture ✅ (playmode advisory pending, non-blocking). The split runs end-to-end on runner-1 with the new `capture` label. READY for Sponsor steps (b) browser-merge + (c) branch-protection flip. **#197 instrument build also GREEN** (run 28445883291, stamp `ce37466`) → soak served (Build/soak-197-v4, F1/F2/F3 isolation toggles). Note: after #203 merges + protection flips to structure+build+capture, open PRs #197/#194 (ran the OLD monolithic `unity` job) need a `git merge origin/main` to pick up the split ci.yml + satisfy the new required checks (`[[merge-from-main-avoids-force-push-rebase]]`).

**↻↻↻↻ #197 PRE-DIAGNOSIS COMPLETE (workflow w8p8rmh1b, 7 agents, HIGH-conf + adversarially verified):** ROOT CAUSE = **Sneak Walk.fbx clip loop-seam**. `Sneak Walk.fbx.meta` has **`loopBlend: 0`** (ground-truth confirmed) → frame-27→frame-0 POSE discontinuity snaps once per 28-frame clip cycle = ONE L+R gait cycle = the Sponsor's "left, right, JERK". INVISIBLE to all 3 prior instruments (normalizedTime monotonic + clean TIME-wraps + smooth velocity — but the POSE seam still snaps; a clock ≠ a pose). **Foot-sync EXONERATED:** CrouchWalk state has NO speedParameter wired (CharacterAssetGen.cs:877-878; speedParameter only on locoState 781-782 + attackState) → LocoSpeedMul architecturally unreachable from the crouch lane. Animator-damp + grounding + camera all ruled out.
- **FIX A (primary):** `CharacterAssetGen.cs` `LoopAndRename` (~after line 515 `cc.loop=true`): `cc.loopBlend=true; loopBlendOrientation=true; loopBlendPositionXZ=true; loopBlendPositionY=false;`. ⚠ **MUST regen+commit the .meta files** (`[[unity-procedural-committed-assets-go-stale]]` — build ships committed snapshot; verify `Sneak Walk.fbx.meta` shows `loopBlend: 1` post-regen). Applies to ALL looped clips (Idle/Walk/Run/CrouchIdle/CrouchWalk) = net improvement, low risk.
- **FIX B (fallback, only if A's signature fails):** state re-entry flap guard (Moving-bool hysteresis + CrouchWalk transition exitTime) at CharacterAssetGen.cs:877-895 + CastawayCharacter Moving source.
- **Toggle decision map:** F2-no-diff + F3-no-diff + jerk-at-normalizedTime-wrap → confirms FIX A. F2-kills-it → FIX B (foot-sync flapping a transition). 
- **DOC-WORTHY (harvest):** loopBlend=0 pose-seam jerk invisible to a normalizedTime trace — Mixamo In-Place looped clips need loopBlend=true; a clean TIME-wrap ≠ a clean POSE-wrap.

**↻↻↻↻↻ RESUME 2026-06-30 (Sponsor back, present-mode):** Sponsor chose **CONFIRM-VIA-F2/F3-FIRST** (not blind-dispatch Fix A) — honoring /unstick after 3 failed confident diagnoses. Ran the v4 build (`Build/soak-197-v4/FarHorizon.exe`, stamp `ce37466` ✅ screenshot-confirmed; readout clip=CastawayCrouchWalk, normTime 0.7410, foot-sync mul 1.5278).
- **SOAK RESULT (LIVE-CONFIRMED):** F2 (foot-sync on/off) = NO change. F3 (speed-snap) = jerk **becomes LESS but NOT gone** — "because you walk faster" (Sponsor's read). Neither toggle FIXES the every-2-steps jerk. → **F2&F3 don't fix it → loop-seam (Fix A) path.**
- **F3-less-not-gone CORROBORATES loop-seam (not refutes):** the pose-snap at the frame-27→0 wrap is a FIXED-magnitude pop independent of clip speed; at slow sneak speed the gentle surrounding motion makes a fixed pop stand out MORE, at walk speed the vigorous motion masks it. F3 only masks; it doesn't remove the seam. Fix A (loopBlend) removes the pose discontinuity at ALL speeds. (If a residual slow-speed jerk SURVIVES Fix A → then look at clip-rate / a sneak-speed floor — but loop-seam is the HIGH-conf first fix.)
- **DISPATCHED Devon on Fix A** — agentId **a04cc5c8441cfc108**, devon-wt → branch `devon/86caa3kur-crouch`, ticket `86caa3kur` → in progress. Loop-blend on all looped clips in CharacterAssetGen.LoopAndRename + regen/COMMIT the `.meta` (verify `Sneak Walk.fbx.meta` shows `loopBlend: 1`) + EditMode + push → monolithic `unity` CI builds the soak artifact (#203 split not merged → OLD ci.yml, correct). Reviewer Drew. Secondary AC: doc-note to procedural-animation-verbs.md. Wake = Devon's <task-notification>.
- Other gated PRs still queued: #203 (browser-merge+protection-flip), #206 (label-merge), #194 (sun-lower build).

**↻↻↻↻↻↻ AWAY MODE ARMED 2026-06-30 ~15:25Z (Sponsor stepped away):** auto-status=away, cron job **`0a1d8d68`** (15-min, session-only — SessionStart hook re-arms from state file). keep-screens-alive CONFIRMED ON (pid 35116) → loop survives. last_tick 2026-06-30T15:24:47Z.
- **#197 Fix A — Devon DONE + pushed `35826ee`** (PR #197). `cc.loopPose=true` on all 7 looped clips; `Sneak Walk.fbx.meta` line 46 `loopBlend: 0→1` CONFIRMED committed. Commits: source `3808e5e` / metas `b7d6982` / doc-note `35826ee`. EditMode 765/765; build + functional capture gates green (run `28454927241`). **Self-Test Report comment 4845077938.** The one CI red = `-verifySneak` DIAGNOSTIC step launch-flake (rc=124 @120s; 5 sibling windowed gates green same run, instrument passed 2 prior runs, change is import-settings-only) → Devon re-ran the failed job (in progress). **NOT dismissed — the re-run IS the ground-truth test; confirm green before staging.**
- **Drew REVIEWING #197** — agentId **`a890fd3028c6e00a0`**, drew-wt @ PR #197. Posts COMMENTED verdict in body. Ticket `86caa3kur` → in review.
- **Priya SCANNING the board** — agentId **`a8cffae8360f00013`**, priya-wt. Full open-ticket reconcile + dispatchable-set report (feeds next tick). Free personas if she finds non-gated work: Erik, Uma (Drew busy reviewing, Devon's slot frees when the re-run ends).
- **#197 NEXT ACTION (on resume / next tick):** when CI re-run GREEN **and** Drew APPROVE → **STAGE the #197 soak** to this queue for the Sponsor (it's a feel/soak-gated PR — NOT auto-mergeable). Stage: exe = the CI artifact `FarHorizon-Windows-<merge-ref>` downloaded to `Build/soak-197-v5/`, expected HUD stamp = the merge-ref sha (verify via artifact suffix, [[soak-build-stamp-is-merge-ref-not-headsha]]) + the test checklist (crouch-walk should be SMOOTH at sneak speed now — no every-2-steps jerk). If Drew REQUEST_CHANGES → back to Devon.
- **Follow-up noted (don't act yet):** once the Sponsor confirms the loopBlend fix works, the `-verifySneak` CI gate + SneakIsolationTool instrument can be RETIRED (soak-hunt-era diagnostics, ci.yml = .github wall + a decision gated on Sponsor confirmation). Priya can file a low-pri follow-up.
- Wake signals: Drew + Priya Agent notifications + cron `0a1d8d68` (backstop). CI re-run state checked at the review-completion wake / next tick (no separate poll — avoid the flaky-bash-poll trap).

**↻↻↻↻↻↻↻ #197 SOAK STAGED + reviews done (2026-06-30 ~15:40Z):**
- **Drew REVIEW = APPROVE_WITH_NITS** (comment 4845254887, soak-ready): `loopBlend: 1` committed CONFIRMED (`Sneak Walk.fbx.meta:46`, all 7 looped clips 0→1, 8 one-shots stay 0); `cc.loopPose=true` @ `CharacterAssetGen.cs:527`; EditMode 757/757; no movement/damage code touched. Concurred the `-verifySneak` red = environmental flake. **NIT (non-blocking) FILED as `86cagmwg9`** (to do): no test pins `loopPose` → a future `.meta` regen could silently revert the fix; add a `loopPose==true` assert at `CastawayLocomotionHitReactControllerTests.cs:89`. Depends on #197 merging.
- **CI ground-truth (verified, NOT assumed):** run 28454927241 — only the `-verifySneak` DIAGNOSTIC step failed (steps 24/25/27 chop+uploads succeeded; build succeeded). **Build artifact EXISTS:** `FarHorizon-Windows-dc20f800…` (45 MB) + all functional gates (chop/water/loot/pond/settings) green. The fix is sound; the soak-hunt diagnostic just flaked on windowed launch AGAIN (proven-flaky).

### ⭐ SOAK FOR SPONSOR — #197 crouch loopBlend fix (the every-2-steps jerk)
- **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-197-v5\FarHorizon.exe` (downloaded + verified on disk).
- **Confirm HUD stamp = `dc20f80`** before judging (merge-ref of run 28454927241, [[soak-build-stamp-is-merge-ref-not-headsha]]).
- **Test & confirm THIS:** (1) crouch-walk (hold Ctrl+W) — **the every-2-steps jerk should be GONE: smooth at sneak speed**, not just masked at higher speed; (2) release Ctrl → standing Idle/Walk/Run still feel right (loopPose was applied to those too — watch for any regression to the already-approved feel); (3) Ctrl+Shift+WASD still sneaks (crouch wins over run).
- **Verdict:** smooth → approve (then see the merge snag below); still jerks → back to Devon for **Fix B** (state re-entry guard, away-queue ↻↻↻↻ map).

### ⚠ #197 MERGE SNAG — decision needed (queued, NOT auto-decided — infra/ci.yml)
#197 is label-mergeable (no `.github` files) BUT the required `unity` check is **RED** because the `-verifySneak` diagnostic step flaked again. Its job is DONE (cause found + fixed). To clear the merge gate, pick one:
- **(a) RECOMMENDED — make `-verifySneak` advisory** (`continue-on-error: true`, mirrors the already-accepted advisory `playmode` job) or remove it. Small ci.yml edit → `.github` wall → your browser-merge. I'd dispatch Devon/Drew on your go.
- **(b)** re-run the `unity` job until verifySneak flakes green (passed on 2 prior runs — fragile, burns the build slot).
- **(c)** leave it (you re-run-to-green at merge time).

**↻↻↻↻↻↻↻↻ Board state after the scan (2026-06-30 ~15:40Z):** Priya's scan = board DRAINED except the in-flight NIT. **Devon RUNNING the #200 anti-idle NIT** `86cagfn9z` (agentId **`ae9c1ec67b079565b`**, devon-wt → fresh branch `devon/86cagfn9z-antiidle-nit`; XS `.sh` grep fix, not a Unity build → no slot contention; label-mergeable; reviewer = Drew when his PR is up). After Devon's NIT PR opens → **Drew reviews it** (next queued dispatch). Then the board is fully drained / sponsor-gated → away loop goes quiet with cited per-slot reasons. **drew-wt housekeeping:** Drew left a local `pr-197-review` branch + `ci-trace/` dir (cleanup denied by the destructive-bash hook, harmless local-only) — next drew dispatch's Step 0 cleans them.

**↻↻↻↻↻↻↻↻↻ #207 NIT done + in review (2026-06-30 ~15:45Z):** Devon DONE → **PR #207** (anti-idle NIT, `86cagfn9z` → in review): `TICK_SIG` line-84 counting grep narrowed (bare `never idle` dropped; gate-entry line-78 untouched); `bash -n` clean; 14 tests pass + new B6 regression PROVEN (fires against the un-fixed copy, silent against the fix). No `.github` → label-mergeable. **Drew APPROVED #207** (comment 4845349542; B6 regression proven to genuinely guard, 14 tests pass, narrowing scoped to line-89 counting grep). **#207 STAGED — one-click ready** (`86cagfn9z` → ready for qa test): all gates green — structure pass + **unity pass** (run 28456590723, 5m12s) + Drew APPROVE + 14-test self-test; MERGEABLE (UNSTABLE = advisory playmode only, non-blocking); no `.github` → label-mergeable. **On Sponsor approval → orch adds the `auto-merge` label (present-mode; NEVER away-label, NEVER hand `gh pr merge` — [[explain-why-before-handing-sponsor-commands]]).**
- **verifySneak FLAKE CONFIRMED intermittent:** #207's unity job ran the SAME `-verifySneak` diagnostic and it PASSED, while #197's run had it FAIL — same step, different outcome, independent of the code change. Strengthens the "retire/de-block verifySneak" case for the #197 merge-snag decision.
- **Board now FULLY DRAINED → away loop QUIET** with cited per-slot reasons: Devon/Drew/Erik/Uma idle = no non-gated work (Priya's 28-ticket scan); build slot free; everything else sponsor-gated/staged. Next ticks = lightweight change-checks ([[away-tick-cadence-on-drained-board]]) until a gate clears or the Sponsor returns.

**↻↻↻↻↻↻↻↻↻↻ ANTI-IDLE FULL SCAN + sun-lower dispatched (2026-06-30 ~15:55Z):** fresh whole-board scan (subagent `ad0458e5af635f851`, 28 open tickets). **DISPATCHED Drew → sun-lower `86cag25az`** (agentId **`a6a63844ab3c4ed9c`**, drew-wt @ #194 branch `drew/86cabc743-sky-poc` → in progress): lower the sun to a visible gameplay-framing default (Sponsor decided A = "lower on #194 branch", away-queue ↻↻↻↻↻) + a Danish-safe runtime nudge dial (elevation/hue/size) so he tunes it in ONE soak ([[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]). Reviewer Devon. UX-visible → Sponsor soak; #194 merge = browser-merge (.github `verify_sky_gate.sh`).
- **Per-slot idle accounting (machine-checkable, every other slot):**
  - **Build slot:** OCCUPIED by sun-lower (single-build-slot rule).
  - **Devon:** no non-gated work — the ci.yml cluster (`86cafzaeb`/`86caammpq`/`86cag1xn0`/`86cafhgun`/`86cabfa21`/`86cabe3e5`) all overlap STAGED #203's ci.yml restructure → sequence after #203 merges; loopPose-test `86cagmwg9` hard-dep on #197 merge; dev-console foundation `86cabeqj9` is Unity-build → build-slot-occupied.
  - **Erik:** prior sky/juice research shipped (#201); spikes `86cabkhqn`/`86cabkhjg` are sponsor-gate-tagged; no un-gated research filed.
  - **Uma:** open-horizon has its #199 spec + Option-A decision; combat DESIGN `86cabcdpn` + Snake `86caaz4vn` are grill-first; next-island POC `86caa9zpp` vision-soak-gated.
  - **Priya:** board reconciled this tick (28 tickets, 1 flip) — no hygiene backlog.
- After sun-lower's CI: stage its re-soak for the Sponsor. Then board fully gated again → quiet. Wake = Drew's notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻ sun-lower DONE + #194 DIRTY/no-CI → merge-from-main re-dispatched (2026-06-30 ~16:35Z):**
- **Drew sun-lower DONE** (commit `7f64c2a`): Sun elevation **48°→25°** (cause-level: the "Sun" key Euler X in `WorldBootstrap.cs` `SunElevationDeg`; disk follows via `ResolveSunDirection`); **F10 WorldLookNudgeTool gained a SUN target** (elev ↑/↓ · hue ←/→ · brightness Home/End · size PgUp/PgDn — Danish-safe); 3rd `-verifySky` gameplay capture added; EditMode **715/715**; shipped `-verifySky` PASSED; gameplay capture eyeballed = warm-gold sun above the treeline. Regenerated+committed `Boot.unity` + `GradientSky.mat` (8-file commit). Picked 25° over 18° because 18° sat the disk AT the blob-canopy treeline (occluded) — diagnosed via the shipped capture, not a metric.
- **TWO BLOCKERS found (verified by orch, not assumed):** (1) **CI did NOT trigger** on `7f64c2a` — no run exists (last branch run = yesterday's `ad3865e`, cancelled). (2) **PR #194 = DIRTY** — real merge **conflict** with current main `a8b3ca6` (main moved while #194 sat). A DIRTY PR can't merge + the conflict state likely also blocks the merge-ref CI.
- **FIX → Drew re-dispatched: merge-from-main** (agentId **`a84492904665e2428`**, drew-wt @ #194): `git merge origin/main` + resolve (preserve sun-lower AND main; regen committed assets if conflicted) + EditMode/build/`-verifySky` verify + plain push (no force) → clears DIRTY + re-triggers CI. Merging CURRENT pre-#203 main (correct; #194 needs ANOTHER merge-from-main after #203's CI-split lands). Reviewer Devon.
- **DOC-WORTHY (harvest to `lowpoly-quality.md` — NOT applied, would strand on orch/coordination):** a directional/at-infinity sun disk's SCREEN position is set by camera **rotation only** (camera height/distance don't move it); framing a low sun is a pitch+FOV+**occlusion** problem — at the gameplay orbit tilt, the sun's elevation must clear the blob-canopy treeline (~15–20° subtended) to read, hence 25° not 18°. (Adds to the [[verify-grounding-soaks-by-gameplay-cam-visual]] family: eyeball the gameplay-cam capture, not the metric.)
- After the merge + CI green: STAGE the #194 sun re-soak (exe + merge-ref stamp + "look at the warm-gold sun; F10→SUN to dial elev/hue/size" checklist). Wake = Drew's notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻ #194 merge DONE + Devon reviewing (2026-06-30 ~16:55Z):** Drew merge-from-main DONE — head **`31cd986`**, **#194 MERGEABLE** (was DIRTY), CI run **`28461187708`** in_progress, EditMode 756/756, `-verifySky` PASS (sun ~25°). Only `Boot.unity` conflicted → resolved via `git checkout --theirs` + `BootstrapProject.Run` regen (bakes both branches; carries SkyVerifyCapture + main's Hands/LocomotionHitReact captures). **Devon REVIEWING #194** — agentId **`aaa6bd4942dcc022b`**, devon-wt @ PR #194; flagged to verify the Boot.unity regen baked BOTH branches (highest-risk part). `86cag25az` → in review.
- **On Devon APPROVE + CI `28461187708` green → STAGE the #194 sun re-soak** (download `FarHorizon-Windows-<merge-ref>` → `Build/soak-194-v2/`, verify stamp = merge-ref; checklist: warm-gold sun reads above the treeline at gameplay framing + F10→SUN dials elev/hue/size). #194 merge = Sponsor browser-merge (`.github verify_sky_gate.sh`). Then board fully gated → quiet.
- In flight: Devon (#194 review) + CI `28461187708` (build slot). Idle-with-reason: Drew (just freed — no non-gated work, build slot occupied), Erik/Uma/Priya (per the ↻↻↻↻↻↻↻↻↻↻ accounting).

**↻↻↻↻↻↻↻↻↻↻↻↻↻ #194 APPROVED + sun SOAK STAGED; dev-console re-assess (2026-06-30 ~17:00Z):**
- **Devon APPROVED #194** (comment 4845956138): Boot.unity baked BOTH branches CONFIRMED (SkyVerifyCapture + main's Hands/LocomotionHitReact all present — no side dropped); sun-lower cause-level correct (25° == committed `_SunDirection` math); **CI `28461187708` = unity SUCCESS + structure SUCCESS** (756/756); F10 SUN keys Danish-safe; no blockers. `86cag25az` → ready for qa test.

### ⭐ SOAK FOR SPONSOR — #194 visible sun (sun-lower)
- **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-194-v2\FarHorizon.exe` (downloaded + verified). **HUD stamp = `55bde02`** (merge-ref of run 28461187708; confirm before judging).
- **Test & confirm THIS:** (1) warm-gold sun now reads at gameplay framing (lowered 48°→25°); (2) **⚠ Devon's NIT — the `sky_gameplay.png` CI capture used a deliberately WIDE 75° FOV to clear the canopy; eyeball the sun-vs-treeline at the REAL over-shoulder gameplay framing, not just "is the sun there"**; (3) press **F10 → SUN target** to dial elevation (↑/↓) · hue (←/→) · brightness (Home/End) · size (PgUp/PgDn) live; (4) clouds-vs-sky still reads.
- **Verdict:** good → **Sponsor browser-merge** `gh pr merge 194 --admin --squash --delete-branch` (`.github verify_sky_gate.sh` → label can't); needs dialing → tell me the F10 values you settled on and I re-bake. ⚠ After #203 merges, #194 needs ONE more `git merge origin/main` (pre-#203 ci.yml now).
- **Harvest downgrade:** Devon (read the docs) says the sun-framing finding is ALREADY covered by `unity-conventions.md` §26/§7 + `unity6-mastery.md` §3 — so the harvest item is likely moot; verify on the next clean-branch harvest, don't author a dup.
- **Priya RE-ASSESSING the dev-console** (agentId **`ada2b6196f4b29cd8`**, priya-wt): build slot is now FREE → is the dev-console foundation `86cabeqj9` dispatchable, already-built (per #198), or design-gated? + re-confirm the dispatchable set with the slot free. On her verdict: dispatch the foundation if genuinely ready, else go quiet with the refreshed accounting. Wake = Priya's notification + cron `0a1d8d68`.
- **SPONSOR QUEUE (drain via /sponsor-questions-walkthrough on return):** (1) ⭐ #197 crouch SOAK (Build/soak-197-v5, stamp `dc20f80`) + the verifySneak-gate merge-snag decision; (2) #203 browser-merge + branch-protection flip; (3) #206 label-merge; (4) #194 sun-lower build; (5) #207 label-merge (after Drew APPROVE).

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻ #194 CI clarified + dev-console foundation DISPATCHED (2026-06-30 ~17:10Z):**
- **#194 CI — orch-verified at JOB level (resolved a Devon-vs-Priya conflict):** run `28461187708` REQUIRED checks = **unity SUCCESS + structure SUCCESS** ✅; the run's overall conclusion shows `cancelled` ONLY because the **advisory playmode** job (non-blocking, always-flaky [[advisory-playmode-job-unreliable-soak-is-interaction-gate]]) was cancelled. Devon right; Priya read the run-LEVEL conclusion. **#194 soak build VALID + #194 admin-mergeable** (required checks green; `cancelled` run-conclusion does NOT block the browser-merge). `Build/soak-194-v2` stands.
- **Dev-console foundation `86cabeqj9` = (b) genuine unbuilt foundation** (Priya): the F1 / non-modal / BoolEntry / nudge / position-picker / differs-badge ACs are all ABSENT on main; #198 only registered consts into the #83 catalog. **#83 hard-dep CONFIRMED MET by orch ground-truth** — `git ls-tree origin/main` shows `Assets/Scripts/Runtime/Settings/{SettingsRegistry,SettingsPanel,SettingsCatalog,SettingEntry,UiInputGate}.cs` all present. The ticket's "do NOT start until #83 on main" warning is STALE (2026-06-19, pre-merge).
- **DISPATCHED Devon → dev-console foundation** (agentId **`a7731294d308bf9e9`**, devon-wt → `devon/86cabeqj9-devconsole-foundation`, `86cabeqj9` → in progress): 11 ACs (F1 toggle, non-modal open-while-play, typed+nudge entry, corner position-picker, BoolEntry, baked-default display, differs-badge, reset-to-defaults E2E, regression tests). Reviewer Drew. UX-visible → Sponsor soak (give-him-the-knob) is the END gate. **Build slot now OCCUPIED.**
- **Idle-with-reason:** Drew/Erik/Uma/Priya — the dev-console foundation was the ONLY slot-freed dispatchable; rest gated (ci.yml cluster ← unmerged #203; loopPose-test ← #197 merge; open-horizon ← #194 overlap; airControl ← #197 overlap; spikes sponsor-gate; combat/Snake grill-first; next-island vision-soak). Wake = Devon's notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ dev-console foundation DONE → #208 + Drew reviewing (2026-06-30 ~17:35Z):**
- **Devon DONE → PR #208** (`86cabeqj9`, → in review): all 11 ACs landed on the #83 infra; **EditMode 772/772** (21 new tests); shipped-build settings gate PASSED (sha `fbd4ab2`). **F1 design (good):** joined the EXISTING F1 dev-overlay master layer (`DebugOverlays.Visible`, `86cafd6d6`) — no 2nd F1 poll, the [[sponsor-wants-unified-dev-tweak-console]] convergence. Evidence (built exe): `worldInputGated=False` while open, live tweak applies, differs-badge + reset-to-default both work. New: `BoolSettingEntry.cs`/`ConsolePosition.cs` + `DevConsoleTests.cs`/`ConsolePositionTests.cs`.
- **Drew REVIEWING #208** — agentId **`a86b6b45c20bfa4f6`**, drew-wt @ PR #208. On Drew APPROVE + CI green → **STAGE the #208 dev-console soak** (UX-visible, give-him-the-knob: open on F1 while playing, reposition off the player, tweak live, differs-badge, reset-to-defaults). Label-mergeable (no `.github`) — but it's SOAK-gated, so stage the soak first.
- **After #208:** the downstream dev-console tickets (`86caber95` F-key migration, `86cabeqwf` per-need entries) are hard-dep on #208 MERGING → still gated. So once Drew's #208 review lands, board is fully staged/gated → away loop quiet.
- **Updated SPONSOR QUEUE:** ⭐soaks: #197 crouch (`dc20f80`) · #194 sun (`55bde02`) · #208 dev-console (pending CI). Merges: #203 (browser + protection-flip) · #206 (label) · #207 (label) · #194/#197/#208 (after soak). Wake = Drew's #208 notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ #208 APPROVED + soak staged; board re-confirm (2026-06-30 ~17:40Z):**
- **Drew APPROVED_WITH_NITS #208** (comment 4846249522): F1 master-layer convergence CONFIRMED real (`DebugOverlayToggle` sole F1 poller; `SettingsPanel:152` reads the flag — no double-toggle); all 11 ACs hold; **CI green** (unity pass 4m2s + structure; EditMode 772/772); cross-lane clean (SettingsCatalog.Build intact, no combat/Boot.unity touched). 2 non-blocking NITs → filed **`86cagpk72`** (dead `toggleKey` metadata + share the AC6 nudge-step formula with its test; can fold into `86caber95`).

### ⭐ SOAK FOR SPONSOR — #208 dev-console foundation
- **Exe:** `C:\Trunk\PRIVATE\Far-Horizon\Build\soak-208\FarHorizon.exe` (downloaded + verified). **HUD stamp = `e6c660a`** (merge-ref of run 28463326201).
- **Test & confirm THIS:** (1) **F1** opens the tweak console WHILE playing — WASD/run/jump/orbit all still work with it open (non-modal); (2) type a value OR nudge a focused entry (arrows; Shift=5× / Ctrl=0.2×) → applies live as you move; (3) reposition the panel off the player (corner picker); (4) the "differs-from-default" badge shows on changed entries; (5) reset-to-defaults reverts live + clears badges. `86cabeqj9` → ready for qa test.
- **Verdict:** good → label-merge (no `.github`); tune more → tell me. Soak-gated, so it's staged for your eyes first.
- **3 SOAKS now staged:** #197 crouch (`dc20f80`) · #194 sun (`55bde02`) · #208 dev-console (`e6c660a`).
- **Board re-confirm dispatched** (scan subagent) — verifying nothing new is dispatchable with the build slot free post-#208 (expected: downstream dev-console `86caber95`/`86cabeqwf` hard-dep on #208 merge; everything else gated on a Sponsor merge). Then quiet. Wake = scan + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ BOARD DRAINED — GENUINELY QUIET (2026-06-30 ~17:45Z, fresh-scan confirmed, NOT from memory):** scan subagent `a0667351b76ad8305` returned all 28 non-complete tickets; orch classified each. **Nothing dispatchable — every persona + the free build slot has a machine-checkable gate:**
- **6 STAGED (Sponsor merge/soak):** `86caa3kur` #197 crouch (soak) · `86cag25az` #194 sun (soak) · `86cabc743` #194 sky (soak parent) · `86cafz9tg` #203 CI-split (browser-merge+protection) · `86cagfn9z` #207 NIT (label) · `86cabeqj9` #208 dev-console (soak).
- **Hard-dep on an unmerged staged PR** (auto-unblocks on merge): `86cagmwg9`+`86caambxh` ← #197; `86cag93zb` ← #203; `86caber95`+`86cabeqwf`+`86cagpk72` ← #208; `86cagfn8h` open-horizon ← #194 (sky-file overlap; also high-aesthetic/Sponsor-steered).
- **ci.yml cluster — sequence after #203** (ci.yml conflict): `86cag1xn0`, `86cafzaeb`, `86cafhgun`, `86cabfa21`, `86caammpq`, `86cabe3e5`.
- **Sponsor-only:** `86cabcdpn` combat + `86caaz4vn` Snake (grill-first); `86caffwv5` attack-anim (Mixamo clips); `86cafhehe` workflows-PAT (secret); `86cabkhqn`+`86cabkhjg` spikes (sponsor-gate); `86cab7u42` blocking-playmode (unsolved env-deadlock); `86caa9zpp` next-island (vision-soak); `86caa9zju` boat (Sponsor-deferred); `86cacewju` chamfer (deferred → unified weapon re-author).
- **KEYSTONE:** merging the 5 staged PRs cascades open ~12 tickets. Until then, correctly QUIET. Away ticks → lightweight change-check only ([[away-tick-cadence-on-drained-board]]); cron `0a1d8d68` is the heartbeat. **0 agents in flight.**
- **Resume next-action:** drain the Sponsor queue via `/sponsor-questions-walkthrough` (3 soaks + 3 merges + the #197 verifySneak-gate decision). Everything staged is in this file.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ AUTONOMOUS FILL — Uma → open-horizon impl spec (2026-06-30 ~17:50Z, away cron tick):**
- **Decided:** with the board drained-pending-Sponsor but idle Uma capacity, dispatched **Uma → open-horizon Option-A IMPLEMENTATION spec** (agentId **`ae4954c531d50f50e`**, uma-wt → `uma/open-horizon-impl-spec`, docs-only PR → label-mergeable). Build plan for the Sponsor-decided Option A (open ocean + natural fog-haze): mountain-removal approach, fog-dissolve horizon, next-island reveal hook, ACs, + the aesthetic choices to surface at soak.
- **Foundation:** away-prompt explicitly clears **specs** for dispatch without sponsor priority ("idle capacity is a bug" + "research-spikes/specs need no sponsor priority"); Option A is already Sponsor-DECIDED (#199 + DECISIONS), so the spec executes his decision (not a new aesthetic call — feel is judged at the eventual soak); impl ticket `86cagfn8h` is #194-gated, so speccing NOW is free lead-time.
- **Alternative (if surfaced):** leave Uma idle / wait for the Sponsor to request the spec. **Reversibility:** it's a design doc — discard/revise in ≤1 PR if unwanted. **Status:** pending review.
- **Other slots idle-with-reason (main static `a8b3ca6`, nothing merged):** Devon/Drew — candidates all hard-dep on an unmerged staged PR (#197/#203/#208) or #194-overlap; build-slot — no un-gated Unity build; Erik — build-time spike `86cabkhqn` is sponsor-gate-tagged + can't-measure-live without Bash (research constrained), left for the Sponsor to un-defer. Wake = Uma's notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ Uma spec DONE (#209) + Erik build-time research dispatched (2026-06-30 ~17:55Z):**
- **Uma DONE → PR #209** (open-horizon Option-A impl spec, docs-only, MERGEABLE): re-points the EXISTING fog dissolve (no new system) for the open-ocean beauty; corrects the ticket — mountains spawn in **`WorldBootstrap.BuildVista`** (L398), not `LowPolyZoneGen`. Cites live files (LowPolyMeshes `FacetedMountain`/`FacetedLandmass`, QualityPassGen `EnableGlobalFog`, LowPolyVertexColor `_FogCap`, WorldLookPalette `SkyHorizon`). **STAGED → 4th label-merge item** (docs-only, no review/soak needed — Uma verified files live; impl `86cagfn8h` stays #194-gated). Confirms open-horizon ← #194 (same-3-files sky overlap). Option B (faint-rim) = pre-planned fallback; 4 soak dials (fog density / sea-teal floor / sun interplay / cloud density).
- **Erik DISPATCHED → build-time hold-time lever analysis** (`86cabkhqn` research half; agentId recorded next turn): the orthogonal "make each build FASTER" axis (Library cache / Accelerator / stripping / IL2CPP-vs-Mono / incremental) — distinct from the #203/RT-readback/runner-2 PARALLELISM work. Research-only (away-cleared spikes); OOS the lever-pull (Sponsor-gated action). Foundation: away-prompt research-spike clearance + the single-build-slot is the Sponsor's demonstrated #1 throughput priority (#203). Reversible (a note). Erik can't-git → Writes to `team/erik-consult/`, orch harvest-PRs later.
- **Updated SPONSOR QUEUE:** ⭐3 soaks (#197/#194/#208) + label-merges #206/#207/#209 + #203 (browser+protection) + the #197 verifySneak-gate decision. Wake = Erik's notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ SPONSOR BACK — /sponsor-questions-walkthrough drained (2026-06-30 ~18:00Z):**
- **Q1 — label-merges APPROVED (all 3):** `auto-merge` label ADDED to #207 (anti-idle NIT), #209 (open-horizon spec), #206 (unity6 harvest) — confirmed on all 3. Action merging async; **orch MONITORS + re-triggers base-race losers** ([[merge-batch-label-race]]); #206 has no CI checks → if it stalls on a missing required check, trigger its CI or browser-merge.
- **Q2 — #203 CI-split: MERGE + protection-flip APPROVED.** Handed Sponsor the 2 browser steps (UI merge — NOT a CLI `gh pr merge`, per [[explain-why-before-handing-sponsor-commands]]; .github needs his session's workflow perm — [[auto-merge-fails-on-workflow-file-prs]]): (1) browser-merge PR #203 squash+delete; (2) branch-protection required-checks `structure`+`unity` → `structure`+`build`+`capture`. **Awaiting his action.**
- **Q3 — #197 verifySneak → ADVISORY.** Ticket **`86cagqhez`** filed (continue-on-error, mirror the playmode advisory; SEQUENCED after #203's split → folds into the new capture job; .github → browser-merge). Owner Devon/Drew.
- **3 SOAKS handed off** (Sponsor playing): #197 crouch `Build/soak-197-v5` (`dc20f80`) · #194 sun `Build/soak-194-v2` (`55bde02`) · #208 dev-console `Build/soak-208` (`e6c660a`). Verdicts → orch merges (all 3 soak-gated; post-#203 each needs `git merge origin/main`; #197 also gets the verifySneak-advisory `86cagqhez`).
- **POST-#203-MERGE SEQUENCE (orch, when #203 lands + protection flips):** (a) #194/#197/#208 each `git merge origin/main` (persona, no force-push, [[merge-from-main-avoids-force-push-rebase]]) to pick up the split ci.yml + satisfy `build`+`capture`; (b) dispatch the verifySneak-advisory `86cagqhez`; (c) the 6-ticket ci.yml cluster + RT-readback `86cag93zb` UNBLOCK → dispatchable. 
- **Erik DONE:** `team/erik-consult/build-time-lever-analysis.md` (untracked in orch checkout → HARVEST-PR pending). Top lever = CI Mono-backend (~2-5 min, needs live-profiling confirm before the Sponsor-gated lever-pull). Lever 2 = RT-readback (already filed `86cag93zb`). 
- auto-status still **away** (cron `0a1d8d68`) — Sponsor PRESENT now; orch operating present-mode (surface, don't auto-decide). Flip to local on confirm he's staying.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ SOAK RESULTS + merges landing (2026-06-30 ~18:05Z):**
- **#206 + #207 MERGED** (main `a8b3ca6`→`8ac6843`); **#209 lost the base-race → re-triggered** (auto-merge re-added, MERGEABLE — will land). #203 still awaiting Sponsor browser-merge.
- **⚠ #197 crouch SOAK = REJECT (regression):** Sponsor on v5 (`dc20f80`): "there is no crouch in this branch." Ground-truthed: build IS from `devon/86caa3kur-crouch`@35826ee + crouch CODE present (`CastawayCharacter.SetCrouch`/Crouch param) — so it's a v4→v5 REGRESSION from the **loopBlend asset regen** (crouch worked in v4 `ce37466`, readout showed `clip: CastawayCrouchWalk`). **Devon DISPATCHED** (agentId **`a3968b59dd073f3f2`**, #197 branch): CONFIRM cause (diff controller ce37466 vs 35826ee; check loopPose flattening the crouch clips) BEFORE fixing; likely fix = loopPose on non-crouch clips only; add a test that catches crouch-ABSENCE (EditMode 765/765 missed it). `86caa3kur` → in progress.
- **⚠ #194 sun SOAK = still-not-visible:** Sponsor on v2 (`55bde02`): "cant see any sun even with view angle range maxed out." Ground-truth: sun baked at **25° elevation** — still ABOVE the camera's max look-up (even maxed view-angle-range can't reach it); the CI `-verifySky` passed only because it used a 75° FOV (Devon's NIT was right). The **F10→[K]→SUN dial works** (WorldLookNudgeTool, toggle F10, cycle [K] to Sun, nudge elev/hue/size live). **HANDED to Sponsor:** dial elevation DOWN toward the horizon until visible → report value → orch bakes `SunElevationDeg`; OR Option B (raise camera max look-up) if he wants a high sun. NOT a rebuild-blocker (dial-or-pitch). `86cag25az` stays in review pending his dialed value / the lower-vs-raise fork.
- **#208 dev-console** — not yet soaked (awaiting Sponsor).

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ RETRO + STANDING played-verification GATE (2026-06-30 ~18:15Z):** Sponsor asked why all 3 served soaks failed/NIT'd. **Root cause: I staged all 3 on green MACHINE gates (CI+EditMode+APPROVE) with NO played-verification at real gameplay framing — the Sponsor was the first to play them.** The capture gates that should've caught it were flaky (verifySneak hung) + low-fidelity (verifySky's 75° wide FOV ≠ gameplay). Under anti-idle "fill the queue" pressure I optimized for soaks-STAGED over soaks-VERIFIED → 3 rounds of rework. **NEW STANDING GATE (memory [[served-unverified-soaks-need-played-verification]]): no feel/visibility soak reaches the Sponsor until a persona PLAYS the built exe at REAL gameplay framing (or a real-FOV capture) + confirms it; flaky/skipped validating capture = NOT ready.** Applies to ALL re-serves below.
- **#208 verdict = APPROVE_WITH_NITS** (core non-modal-open-while-play PASSED ✓). 2 NITs. **F1 de-conflict decision (Sponsor): console KEEPS F1, legacy overlays MOVE OFF F1** to a free Danish-safe key. **Drew DISPATCHED** (agentId **`a7e4a8c4d3097da84`**, #208 branch in drew-wt): NIT1 = settings-UI scale range setting; NIT2 = F1 decouple (console-only on F1, overlays → new key; full F7-F10 absorption stays `86caber95`). **Played-verification gate in the brief.** Reviewer Devon (post-crouch). `86cabeqj9` → in progress.
- **In flight:** Devon #197 crouch-regression (`a3968b59dd073f3f2`) + Drew #208 NITs (`a7e4a8c4d3097da84`). Both re-serve THROUGH the played-verification gate.
- **Sponsor's open actions:** #194 sun (F10→[K]→SUN dial down + report value → I bake; OR pick raise-camera Option B) · #203 browser-merge + protection-flip · #209 auto-merge landing (re-triggered).

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ #208 NIT fix DONE (Drew, played-verified) — re-soak pending CI (2026-06-30 ~18:25Z):** Drew pushed **`0fbd701`**: NIT1 = Console UI scale slider (0.5–1.5×, `transform.scale`); NIT2 = **F1 = dev console ONLY** (direct poll), **legacy IMGUI overlays → F2** (axe/pond/F7-F10). EditMode 775/775; settings verify gate PASSED on the built exe. **Played-verification APPLIED** (the new gate): Drew eyeballed `settings_open.png`(1.0×) vs `settings_scaled.png`(0.5×) at real orbit-cam framing (panel halves) + shipped-exe ground truth `decoupled=True legacyOverlaysVisible=False`. **Honest residual:** the literal F1/F2 KEYPRESS can't be machine-synthesized in windowed captures → that's genuinely the Sponsor's interactive soak.
- **CI on `0fbd701` IN PROGRESS** (run 28472794106, structure pass + unity building). **SERVE the #208 re-soak WHEN GREEN** (download `FarHorizon-Windows-<merge-ref>` → `Build/soak-208-v2/`, stamp = merge-ref): test F1=console-only, F2=overlays, the scale slider, WASD-while-open. Reviewer Devon (post-crouch, code-review parallel). Then `86cabeqj9` → ready for qa test.
- **DECISION (Drew draft, record to DECISIONS.md): dev-overlay key map → F1 = dev console (only); F2 = legacy IMGUI debug overlays.** Supersedes the shared-F1 master from `86cafd6d6`; carries into the F7-F10→console migration `86caber95`.
- Wake = #208 CI green (cron/next-check) + Devon crouch notification + cron `0a1d8d68`.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ #197 crouch ROOT CAUSE = CORRUPT BUILD (not a regression) (2026-06-30 ~18:40Z):** Devon DONE — the v5 "no crouch" was a **CORRUPT BUILD** (warm `clean:false` runner shipped stale `Library`/partial `ScriptAssemblies` → missing scripts + `WasdMovement Read 84 expected 88` serialization mismatch + no NavMesh → WASD/NavMesh inert = "no crouch"). **NOT a crouch regression, NOT loopBlend** (exonerated 4 ways). A CLEAN re-run of the SAME SHA `35826ee` → crouch renders (`avatarCrouch=True clip=CastawayCrouchWalk`). 
- **⚠ CONFIRMS THE RETRO HARD:** the v5 `-verifySneak` RED (that I dismissed as a "flake") was the **canary catching the corruption** — missing-script → SneakVerifyCapture never ran → hang. I served on EditMode + APPROVE (which run in the EDITOR, not the built exe) while the one gate that RUNS the exe was failing. Artifact-exists ≠ build-good.
- **Devon's fix:** regression guard `Assets/Tests/EditMode/CrouchStanceGroundedTests.cs` (catches crouch flatten/grounding-cancel — the old WasdCrouchPlayModeTests missed it) + `CrouchPoseDiagnose.cs` ([MenuItem] A/B). Pushed **`9650b0b`**; unity merge-gate GREEN (EditMode 775 + build + `-verifySneak` now CLEAN-pass).
- **CLEAN crouch re-soak BUILD READY:** CI artifact `FarHorizon-Windows-01f5a42...` (run 28472859170; stamp **`01f5a42`** = merge-ref). **QUEUED — serve AFTER the Sponsor's sun dial** (ONE build at a time). The `-verifySneak` capture already played-verified it (avatarCrouch=True).
- **Drew REVIEWING #197** (agentId **`af2649667c5766a8f`**); `86caa3kur` → in review. **Infra ticket FILED `86cagr0zu`** (warm-runner corrupt builds — detect + clean; the bigger systemic issue).
- **Pending memory-updates (next turn):** loopBlend memory note (verifySneak-hang-with-missing-script = stale-Library flake, not a loopBlend break — re-run clean) + served-unverified memory (this is the strongest confirmation: the dismissed red was a real corruption signal).
- **Build divergence noted:** the #208 build's WorldLookNudgeTool has NO Sun target (old, `{Sky,Fog,Clouds,Mountains}`); the #194 build has `+Sun` — the Sponsor ran #208 for the sun by mistake (build-juggling, my fault). LESSON: serve ONE clearly-labeled build at a time.

**↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻↻ SUN ACCEPTED + crouch re-soak served (2026-06-30 ~18:50Z):**
- **☀ SUN ACCEPTED by Sponsor** (live-dialed on `55bde02`): **elevation 18° · hue (0.98,0.86,0.86) · size 0.95**. **Devon BAKING** these into the defaults (agentId **`ac78886d93fd26334`**, #194 branch: `WorldBootstrap.SunElevationDeg`=18 + `QualityPassGen.SunColor/SunSize`; regen+commit assets; merge-from-main if behind `8ac6843`; **played-verify the baked sun + corrupt-build check**). `86cag25az` → in progress. On Devon's verified bake + Drew review → **#194 ready for Sponsor browser-merge** (`.github verify_sky_gate.sh`); NO re-soak needed (Sponsor accepted live).
- **🧍 #197 crouch Drew-APPROVED** (comment 4847671849; verified diff = test+diagnostic ONLY, zero code change → confirms corrupt-build cause; new `CrouchStanceGroundedTests` genuinely guards crouch-flatten/grounding-cancel). **Clean crouch build SERVED:** `Build/soak-197-v6/FarHorizon.exe` stamp **`01f5a42`** (played-verified by `-verifySneak`: avatarCrouch=True). On Sponsor re-soak approve → **#197 label-merge** (no `.github`). NIT carried: loopPose importer-read guard = `86cagmwg9`.
- **Drew FREE** (post-#197-review) → will review the #194 sun bake when Devon pushes. **Devon's #208-NIT review** queued (Devon busy on sun bake). #208 re-soak pending its CI (`0fbd701`).
- Wake = Devon sun-bake notification + #208 CI + Sponsor (crouch verdict / #203 merge) + cron `0a1d8d68`.

**↻ #194 sun bake DONE + final review (2026-06-30 ~18:55Z):** Devon baked + pushed **`aafc7a6`** (elev 18 + hue (0.98,0.86,0.86) + size 0.95; assets regen'd; played-verified `sky_sun.png` low warm disk over ocean; NO corrupt-build — clean run; EditMode 756/756). `86cag25az` → ready for qa test. CI on `aafc7a6` building (structure pass, unity pending). **Drew REVIEWING the bake** (agentId **`a3d9bae5874c6051f`**). On Drew APPROVE + CI green → **#194 ready for Sponsor browser-merge** (`.github verify_sky_gate.sh`); told Sponsor I'll green-light it. Devon free (post-bake) — #208-NIT review queued (when #208 CI `0fbd701` green). Sponsor has: crouch re-soak (`soak-197-v6`/`01f5a42`) + #203 browser-merge open.

**↻ #194 sun CLEARED for merge (2026-06-30 ~19:05Z):** **Drew APPROVE** the bake (comment 4847872663; verified elev-18 ACTUALLY committed — `GradientSky.mat _SunDirection (0.5455,0.3090,-0.7791)`, asin=18.0° exact, not stale 25/48). **CI GREEN** (run 28474859843, unity 4m58s + structure SUCCESS; no corrupt build). All gates met → **Sponsor given GREEN LIGHT to browser-merge #194** (`.github` UI Merge, his workflow-perm session). **ON #194 MERGE:** flip `86cag25az`→complete + the parent `86cabc743`→complete; #197/#208 will then need `git merge origin/main`. **2 cosmetic stale-comment NITs** (Drew: `MovementCameraScene.cs:374` "~48deg" + `SkyVerifyCapture.cs:19-21` `_MainLightPosition`) → fold into next sky touch, non-blocking. Devon free; Drew free (post-review).

**↻ Away tick 2026-06-30 ~19:20Z:** **#209 (open-horizon spec) AUTO-MERGED** (main `8ac6843`→`36034db`; the re-trigger worked). #194/#197/#203/#208 still OPEN (Sponsor hasn't browser-merged the sun/CI-split yet). **#208 NIT-fix CI GREEN** (`0fbd701` unity+structure SUCCESS) → **Devon REVIEWING #208 NITs** (agentId **`aacbba3c5e113e871`**); on his APPROVE → serve the #208 re-soak (F1/F2 keypress feel). **MERGE-FROM-MAIN note:** #194/#197/#203/#208 now behind main `36034db` — #194's Sponsor browser-merge (admin) should still work even if behind; #197/#208/#203 will each need `git merge origin/main` before/after their merges (handled by a persona). last_tick stamped. Wake = Devon #208 review + Sponsor merges/crouch-verdict + cron `0a1d8d68`.

**↻ #208 NIT Devon-APPROVED + re-soak ready (2026-06-30 ~19:25Z):** Devon APPROVE (comment 4847916041; F1/F2 decouple CONFIRMED on shipped exe — `decoupled=True legacyOverlaysVisible=False`; scale slider `liveScale=0.50 changedLive=True`; no corrupt build `magenta=0.00`; EditMode 775). **#208 re-soak BUILD READY:** `Build/soak-208-v2/FarHorizon.exe` stamp **`37247d8`** — test F1=console-ONLY, F2=legacy-overlays, the UI-scale slider, WASD-while-open. `86cabeqj9` → ready for qa test. On Sponsor re-soak approve → **#208 label-merge** (no `.github`).
- **SPONSOR PLATE (4 ready, pace any order):** (1) browser-merge **#194 sun** (green-lit) · (2) browser-merge **#203 CI-split** + protection-flip · (3) re-soak **#197 crouch** (`soak-197-v6`/`01f5a42`) · (4) re-soak **#208 dev-console** (`soak-208-v2`/`37247d8`). Post-merge: orch catches up #194/#197/#208/#203 to main `36034db`.
- All 4 personas now FREE (Devon/Drew/Priya/Uma/Erik) — board is sponsor-gated on these 4 + the post-#203 cascade.

**↻ Priya hygiene done (2026-06-30 ~19:40Z):** 1 status flip — **#197 `86caa3kur` `in review`→`ready for qa test`** (was stuck post-Drew-APPROVE; #194/#208/#203 verified already-correct at ready-for-qa-test). 4 follow-ups fleshed (`86cagmwg9`/`86cagpk72`/`86cagqhez`/`86cagr0zu` were title-only → hard-dep + scope added; hard-dep notes not sponsor-gate tags, since each gates on a MERGE not the Sponsor). **`STATE.md` refreshed → PR #210** (docs-only, label-mergeable) = the clean 2026-06-30 resume-point (4 Sponsor-pending + post-#203 cascade + the 2 standing lessons). **STAGE #210** with the next Sponsor merge batch (routine orch-doc; no separate ping). ⚠ **Orch-on-stale-branch (`orch/coordination`) re-bit** — Priya's first STATE edit landed in the orch checkout ([[orchestrator-worktree-hygiene]]); recurring friction (doc-strand + this). Re-home the orch to a clean main-based checkout when convenient. **Board now fully idle/sponsor-gated** — nothing dispatchable (Devon/Drew = hard-dep-on-merge; Erik/Uma/Priya = research/spec/hygiene all done; build-slot free but no un-gated build). Quiet until a Sponsor merge/verdict moves main or unblocks the cascade.

**↻ Devon CI-doc harvest DONE → PR #211 (2026-07-01 ~early):** the 3 session findings landed in `.claude/docs/unity-conventions.md` (docs-only, label-mergeable, reviewer Drew): corrupt-build canary (§Headless rituals, "watch"→OBSERVED-CONFIRMED, cross-ref `86cagr0zu`) + flake-triage recipe + capture-FOV fidelity (§Editor-vs-runtime, 6th false-green instance). **STAGE #211** with the next Sponsor merge batch (routine orch-docs). Devon handled the orch-on-stale-branch trap (edits → his branch via git apply, reverted main-repo path; `orch/coordination` clean). **ALL un-gated fill-work now EXHAUSTED** (Uma spec / Erik analysis / Priya hygiene+STATE / Devon harvest all delivered). Board fully sponsor-gated: 5 staged PRs (#194/#197/#203/#208 sponsor-action + #210/#211 docs-label-merge) + the post-#203 cascade. Genuinely nothing left to dispatch. Wake = a Sponsor merge moving main, or his return.

**↻ Docs PRs Drew-APPROVED → both staged (2026-07-01):** **#211** (CI-docs harvest) + **#210** (STATE refresh) both **Drew APPROVE** (comments posted) — docs-only, label-mergeable, no soak → **one-click label-merge-ready, join the Sponsor batch.** #211 NIT (non-blocking, fix-on-touch): finding-3 says "Devon's own review NIT" but it was Drew's #194 NIT — loose attribution, substance correct. **TEAM FULLY DRAINED — all work delivered** (4 feature PRs Sponsor-action-pending + 2 docs PRs one-click-ready + the post-#203 cascade backlog). team/STATE.md (#210) is now the clean resume-point. **Nothing left to dispatch.** Wake = a Sponsor merge or return.

### 🟢 STAGED ONE-CLICK MERGES (Sponsor, label-merge — no .github, all gated):
- **#210** team/STATE.md refresh — Drew APPROVE, docs-only.
- **#211** unity-conventions CI-docs harvest — Drew APPROVE, docs-only.
- (**#207** anti-idle NIT already merged earlier; **#206/#209** already merged.)
### Sponsor BROWSER-merges (.github wall) / SOAK-then-merge:
- **#194** sun (browser-merge, green-lit) · **#203** CI-split (browser-merge + protection-flip) · **#197** crouch (re-soak `soak-197-v6` → label-merge) · **#208** dev-console (re-soak `soak-208-v2` → label-merge).

## ▶▶▶ RESUME 2026-07-01 (present-mode /sponsor-questions-walkthrough + Devon #197 gait-A/B)
- **Walkthrough decisions:** (Q1) #208 soak NOW (not wait-for-review) → served `Build/soak-208-v3/FarHorizon.exe` stamp `bcce7a3`. (Q2) #210 + #211 docs-only → auto-merge LABELED both (sequenced; verify base-branch race on next pulse, re-trigger loser if needed).
- **#197 gait-A/B (Devon DONE, 770bffd):** the Sneak Walk CLIP has a REAL raw end≠start pose delta (sum 69.94°, max 24.32° @ left-toe = 100x idle) → loop-seam pop source LOCATED + real. BUT loopBlend:1-vs-:0 via SampleAnimation = byte-identical (SampleAnimation reads RAW curves, BLIND to loopBlend's RUNTIME blend). loopBlend's runtime effect UNKNOWN to our instruments — only Sponsor's eyes OR a PlayMode Animator-tick probe can adjudicate. DO NOT ship a fix / raise sneak speed. New diagnostic RunGaitSeam in CrouchPoseDiagnose.cs.
- **#197 SOAK SERVED:** `Build/soak-197-v7-loopgait/FarHorizon.exe` stamp `770bffd` (clean, not corrupt, F5/F6 keys). Sponsor eye-checks: sneak still jerk on the loopBlend:1 build? SMOOTH -> loopBlend runtime-blend fixed it, ship. STILL JERKS -> loopBlend insufficient -> fix the CLIP loop (24deg toe mismatch) OR Devon's PlayMode-Animator-tick probe.
- **Doc-worthy (defer to #197-merge harvest):** SampleAnimation is BLIND to loopBlend (runtime-only) — loop-seam A/B must use PlayMode Animator ticks.
- **Board (Priya scan, no drift):** dispatch-ready non-Unity = Erik build-time-lever harvest `86cagvvuq` (stranded note in orch-main, cross-worktree handling — DEFERRED to next pulse). Follow-ups filed: `86cagvvhv` (#208 2.0x cosmetic), `86cagvvmz` (#211 attribution NIT).
- **Still Sponsor-action (6-PR menu):** #203 browser-merge + protection-flip (KEYSTONE) · #194 sun browser-merge · #210/#211 labeled (auto-merging) · #197/#208 soaks served above.

## ▶▶▶▶ #197 v7 SOAK = FAILED NO CHANGE (2026-07-01, Sponsor eyes on the MOVING gait)
- **loopBlend DEFINITIVELY OUT:** Sponsor soaked stamp 770bffd (readout confirms clip CastawayCrouchWalk, loopBlend:1 shipped) → "FAILED NO CHANGE", the 2-step jerk persists. His eyes did what our 3 instruments couldn't (all measured wrong layers: normTime=clock, agent.transform CoV=root, SampleAnimation=raw curves — ALL blind to the live skeleton pose).
- **NEXT INSTRUMENT (Drew, agentId a612fd45d7e1cdb8d, MEASURE-ONLY):** PlayMode-Animator-tick probe sampling LIVE model-bone localRotations frame-by-frame across the gait wrap (normTime ~0.97→0.03) — the never-measured layer = what the Sponsor sees. Pins whether the jerk IS the loop-seam (which bone, how many deg, what normTime) + loopBlend's live effect. NO fix this dispatch.
- **THEN:** Devon applies the informed CLIP-LEVEL fix (trim loop-range to clean full-stride / in-clip loop-bake — NOT the loopBlend flag, proven inert) based on Drew's measurement; re-measure to confirm seam gone; build clean; Sponsor re-soaks.
- **Do NOT:** raise sneak speed (masks + violates reduced-speed AC); blind-fix (7 soaks + 3 wrong-layer instruments say measure-first).
- **In flight:** Devon (#208 peer-review, ae7d6305...), Drew (#197 runtime probe, a612fd45...). #212 Erik-harvest PR OPEN (docs-only, one-click label-merge pending Sponsor OK).

## ▶▶▶▶▶ #197 TOE-FIX VERIFIED TWICE (2026-07-01) — awaiting Sponsor feel-soak
- **CAUSE (confirmed, live-Animator-skeleton probe):** mid-cycle keyframe defect — lefttoebase 80.5° single-frame pop @ normT 0.907. NOT loopBlend (0.000° live), NOT loop-seam (wrap clean), NOT foot-sync/speed/root. Method lesson in memory [[mixamo-looped-clip-loopblend-pose-seam]].
- **FIX (Devon, commit 86393b1, PR #197):** SneakGaitCurveFix slerp-resamples ONLY the corrupted LeftFoot/LeftToeBase/RightFoot/RightToeBase rotation curves → committed CastawayCrouchWalk_smoothed.anim; controller binds it by GUID. loopBlend/speed/root/position untouched.
- **VERIFIED TWICE:** Devon 80.5°→17.25°; Drew INDEPENDENT probe re-run reproduced EXACTLY (whole-body 106.9°→39.95°, wrap 7.8°, NO new spike anywhere in the 200-frame run). 17.25° = genuine toe-off foot-roll, not a pop. Drew APPROVE_WITH_NITS (comment 4851332053). EditMode 771/771.
- **SERVED to Sponsor:** Build/soak-197-v8-toefix/FarHorizon.exe stamp 86393b1 (⚠ folder has STALE leftover captures stamped 0af20b8 — ignore; exe is fresh). Awaiting feel-verdict. On approve → #197 label-mergeable (no .github) after CI green.
- **NIT to file on merge (cosmetic, non-blocking):** SneakGaitCurveFix.WriteQuatCurves runs SmoothTangents on ALL keys of the smoothed bones, not just the resampled range (sub-degree; scope to the resampled index range).
- **Idle-with-reason:** Devon/Drew free — no non-gated work (board drained per Priya scan; awaiting #203/#197/#208 merges + Sponsor soaks on #197/#208).

## ▶▶▶▶▶▶ #203+#194 MERGED → #197/#208 caught up (2026-07-01)
- **main = b7a1c47** (#194 sun on #203 CI-split). #203 `86cafz9tg` + #194 `86cabc743` + sun-lower `86cag25az` → complete.
- **#197 (crouch) caught up:** head `2ffdb0e`, .github==main (verifySneak gone, code-only), EditMode 781/781, crouch fix intact (3/3), Boot.unity re-bake carries #194 sky + crouch (ZoneDLookTests 16/16). Split CI running (run 28504933870: structure+build+capture).
- **#208 (dev-console) caught up:** head `f56e92a`, .github==main, EditMode 783/783, dev-console intact. Split CI running (28505047694). ⚠ Drew's churn-cleanup briefly clobbered #194's GradientSky.mat sun props (_SunHardness/_SunSize/_SunColor/_SunDirection) — he caught+restored; ORCH VERIFIED GradientSky.mat==main (sun preserved). GOTCHA (harvest to memory post-cascade): a catch-up merge's `git checkout HEAD -- <dir>` churn-cleanup can clobber main-only files — always diff staged-vs-main + audit main-only-changed files after.
- **BLOCKING BOTH MERGES:** Sponsor flipping branch-protection required checks unity→structure+build+capture (exact names: `build (self-hosted, EditMode + build)` + `capture`; walkthrough given). On flip + split-CI green → orch one-click-merges #197 then #208 (both code-only, no .github).
- **POST-MERGE (Priya's pre-computed set):** #197 merge → parallel airControl `86caambxh` + loopPose-guard `86cagmwg9` + SmoothTangents-NIT `86cagz0xh`. #208 merge → SEQUENCE Settings/* chain (NITs 86cagpk72/86cagz15v/86cagvvhv → 86caber95 F-key migration → 86cabeqwf). Post-#203: 9-ticket ci.yml cluster + runner-2 now workable.

## ▶▶▶▶▶▶▶ post-#203 ci.yml cluster PLAN (Priya, 2026-07-01) — HELD until cascade CIs drain
- **Dispatch sequence (STRICTLY SERIAL — all touch ci.yml, one PR at a time):** 86caammpq (orphan-hold top-level concurrency, biggest throughput win) → 86cafhgun (verifyInvDragGhostPos required gate) → 86cag1xn0 (wire verify_sky_gate — #194's script exists but ci.yml has 0 sky refs) → 86cagr0zu (corrupt-build canary) → 86cafzaeb (adopt #189 hardening on sibling gates) → 86cabfa21 (playmode reliability; RE-SCOPE off 'cancellable') → 86cag93zb (RT-readback LAST — removes the capture pin the others work around).
- **86cabe3e5 (#83 tweaked-frame sub-gate)** = OFF-lane (C# harness, NOT ci.yml) → dispatchable anytime the build slot frees.
- **86cagqhez (make -verifySneak advisory) = CLOSED MOOT** — #197 stripped the verifySneak gate from ci.yml (Priya grep-verified 0 hits on origin/main).
- **Capture-concurrency follow-up = 86cah17eq** (both captures cancel under 2-PR contention) → folds into 86caammpq, or MOOT via 86cag93zb's RT-readback.
- **86caammpq + 86cabfa21 need wording RE-SCOPE** (post-#203 the concurrency groups changed).
- **HOLD ALL until the current cascade (#208/#215/#216 CIs) drains** — single runner is the bottleneck + a ci.yml concurrency change (86caammpq) mid-cascade risks the in-flight CIs. Then dispatch the sequence one-at-a-time.

## ▶▶▶▶▶▶▶▶ SESSION END — /drain-and-save (2026-07-01) — Sponsor menu staged
main = 3fb5965. THIS SESSION MERGED: #203 CI-split, #194 sun, #197 crouch (8-soak toe-fix), #208 dev-console, #216 SmoothTangents-NIT, #210/#211/#212/#213 docs. Team DRAINED: 0 agents, cron `82349daf` killed, auto-status.state enabled=false. **runner-2 STARTED** (build-parallel available next session).

**SPONSOR MENU (all gates met — staged end-state, NOT blockers):**
- **#217** CI orphan-hold fix (86caammpq) — Devon APPROVE + CI green (structure/build/capture). ⚠ `.github` → **BROWSER-MERGE** (`gh pr merge 217 --admin --squash --delete-branch` in YOUR session — workflow perm; auto-merge label fails). Optional NIT: grep-fallback has no negative test.
- **#218** dev-console F-key migration (86caber95) — Drew APPROVE + CI green. **SOAK** (open the console, dial the migrated F7/F9/F10 rows: camera-follow / arm-pose / world-look). On approve → I add auto-merge label (code-only) → merge CLOSES the 3 folded NITs 86cagz15v/86cagpk72/86cagvvhv.
- **#215** airControl (86caambxh) — Drew APPROVE + CI green. **SOAK** (dial "Air-control accel" in the console; airControlAccel 8→5). On approve → auto-merge label.
- **#214** docs (procedural-animation-verbs.md live-skeleton anim-diagnosis) — CLEAN, docs-only → one-click auto-merge label (or a quick review first).

**NEXT-SESSION DISPATCH (post-merge; runner-2 now parallelizes builds):** ci.yml cluster STRICTLY SERIAL per Priya's plan (above ↻↻▶▶▶▶▶▶▶): 86cafhgun → 86cag1xn0 → 86cagr0zu → 86cafzaeb → 86cabfa21 → 86cag93zb. Plus dev-console per-need entries 86cabeqwf (after #218 merges) + 86cabe3e5 (#83 capture sub-gate, off-ci.yml-lane). Closed-moot this session: 86cagqhez (verifySneak stripped), 86cagmwg9 (loopPose exonerated), 86cah17eq (capture-concurrency = manual-cancel artifact).

## ✅ RESOLVED 2026-07-27 (walkthrough popup): SHIP-AS-IS; Option B (Mixamo re-rig) explicitly deferred and BUNDLED with the next rig change, whenever one is needed anyway. Recorded on 86cau4za2 (comment) + DECISIONS.md. Original: ## 2026-07-22 ~13:5xZ — Sponsor question (non-urgent): v4 right-hand fix route after Option-C kill
- Context: spike 86cavcy4u verdict INFEASIBLE (PR #331; identity round-trip through the Python FBX re-serializer fails Unity import — one-way tool; right hand also lacks the thumb skin-cluster structurally). Remaining routes for 86cau4za2: **Option B** (Mixamo re-rig — discards the accepted left-hand dial, re-rolls the auto-rig lottery, full re-seat after) or **SHIP-AS-IS** (defect is cosmetic, already twice-deferred).
- Orch recommendation: SHIP-AS-IS for now; revisit Option B only when the character next needs a rig change anyway (bundle the risk once). No dependency gates on this — nothing is blocked either way.
- Ask via /sponsor-questions-walkthrough or whenever convenient.

## ✅ RESOLVED 2026-07-27 (walkthrough popup): KEEP as standing instrument until the live mine failure (r5 item 1) is diagnosed + fixed, then re-evaluate gating the :153 release log. Recorded on 86cav8y1u (comment) + DECISIONS.md. Original: ## 2026-07-22 ~14:3xZ — Sponsor question (non-urgent, mechanical default available): ClickGateDiag keep-vs-strip
- Context: ticket `86cav8y1u` (register [ClickGateDiag] in tools/debug/REGISTRY.md) carries a decision AC: keep the instrument standing vs strip before 1.0 — specifically whether to gate its RELEASE-build `Debug.Log` on every LMB click (ClickGateDiagnostic.cs:153, deliberate per instrument convention, cold-path).
- Orch/ticket recommendation: **KEEP** as standing instrument until the live mine failure (Drew r5 item 1 on 86caffwv5) is diagnosed + fixed, THEN re-evaluate gating the release log. The registry ENTRY itself is mechanical and dispatches regardless; only the keep/strip call is yours.
- Ask via /sponsor-questions-walkthrough or whenever convenient.

## ✅ 2026-07-27 (walkthrough popup) — icons 86camyvwn UNPARKED: Sponsor chose "do it now, after the walkthrough" — orch R&D burst THIS session (iron-ore vs iron-bar first, Sponsor judges candidates live; closes with harvest PR + productionization ticket per the R&D-lane rule).

## ⭐ STAGED 2026-07-27 — branch protection on main (Sponsor-DECIDED in walkthrough; orch API write classifier-blocked → browser click)
Sponsor decision: repo stays PUBLIC + real branch protection. Do in browser: https://github.com/TSandvaer/Far-Horizon/settings/branches → Add classic rule → pattern `main` → tick "Require status checks to pass" (NOT "up to date"), select exactly: `structure (hosted, no license)` + `build (self-hosted, EditMode + build)` + `capture (self-hosted runner-1, windowed gates)` (NOT playmode — advisory, standing reds). Leave "Do not allow bypassing" UNticked (--admin flow + docs-only PRs depend on the admin bypass). Optional: "Require a pull request before merging" with no required approvals. Names verified from main run 29926544074 (2026-07-27).

## ⭐ QUEUED 2026-07-27 — 3 combat-pool design questions (from Priya's AC-flesh; next walkthrough — non-urgent, feature lane frees in days)
1. **Heal-item fiction** (86cah7z2q body): what heals beyond needs-gated regen — food/bandage/salve class? Kid-tone constraint applies.
2. **Find-in-world piece + tier** (86cah7y5b body): which weapon piece/tier spawns in-world as the 2nd acquisition route?
3. **Roster residuals** (86cah7ym9 body, retitled — core roster already shipped): add a BONE tier? any NEW weapon types beyond the shipped 15-id set?
Exact phrasing + context live in each ticket body (Priya, 2026-07-27). Also noted: she couldn't set tags (deferred/sponsor-gate) — title/body markers carry the semantics instead.

## ⭐ QUEUED 2026-07-27 — 3 Uma taste calls from the combat UX specs (PR #339; ask before/at the dev dispatches they shape)
1. **HP-bar form** (86cah7z2q): 5-segment TALLER bar so HP reads distinct from the three 10-segment need bars — changes shipped geometry (revert = two constants).
2. **Enemy HP visibility** (86cah7yuh/86cah7z2q): transient above-head pip-row on the LootPrompt anchor vs body-language-read-only.
3. **Stun on EASY** (86cah7yuh): ≤0.6s with chain-immunity vs OFF entirely — control-loss is the one thing a kid may hate.
Full context in the two specs (team/uma-ux/, pending PR #339).

## ⭐ QUEUED 2026-07-27 — 1 heavy-attack PRE-IMPL call (Uma PR #340 §8.2; ask BEFORE 86cau6prr dispatches — changes what gets built)
1. **Commitment weight**: heavy ≈0.95s committed (no player cancel, 0.4× movement damping) vs the light's ≈0.28s — is that the feel you want on paper, or lighter commitment?
(A second queued call — "the heavy clip is shared with the axe swing" — was REFUTED in Priya's #340 review 2026-07-27 (comment 5095091031): the reserved heavy is CastawayMelee/Melee_Attack.fbx, a DISTINCT clip; the fix round removes the premise from the spec. Struck before it reached the Sponsor.)
Her other 7 taste calls are soak-judged (flagged in the spec §8); 4 decision drafts sit in §12 for a future DECISIONS batch after #340 merges (§12 also gets the fix-round pass).

## ⭐⭐ SOAK — PR #337 pickaxe mine pose (86cav8xg9) — ALL machine gates + QA GREEN 2026-07-27; YOUR soak = the ONLY gate
**Exe:** `C:\Trunk\PRIVATE\Far-Horizon-drew-swings-wt\Build\soak-pickaxe-1\FarHorizon.exe` — **confirm HUD stamp `1194927`** (one comment-only commit behind head 171ce08 — verified functionally identical by Tess, comment 5094745288). Gates: Devon APPROVE_WITH_NITS 5094420111 (N1 fixed at 171ce08) + Tess QA PASS_WITH_NOTES + required CI green (capture false-RED cleared by quiet-queue rerun, job 90088419656).
**TEST (bar = mine-pose posture):** pickaxe from belt (number keys / B) → left-click a boulder → judge the body mid-swing: UPRIGHT working lean, head up, feet planted (old defect: contorted fold; fix measured 66°→42°). **Orbit to a TRUE side view** — Tess found the author capture was an oblique that flatters the lean. Watch loop seams. Judge lightly: bounded foot dip (worst 8.1cm, within sibling band — lever known if it reads wrong). NOT this soak: cadence feel, idle seat (your r7 dial).
**Verdict:** PASS → orch merges #337 (label-after-green), flips 86cav8xg9 complete, applies the 2 merge-rider doc captures, unblocks 86caxgyc4 · issues → name them, back to Drew.

## ✅✅ 2026-07-27 ~21:2xZ — WALKTHROUGH ROUND 2 COMPLETE: 9 decisions taken, all persisted
Sponsor drained the clickable queue in one pass before going away. **Every item below is CLOSED — do not re-ask.** Full entries in `team/DECISIONS.md` (2026-07-27) + a comment on each ticket.
1. **`86camz787` = GO** — playmode advisory → REQUIRED. Precondition verified first: 5/5 green playmode jobs since #338 (main runs 30296431820 + 30303098020 + 3 feature-branch runs), no wedges. Revert = one-line ci.yml. ⚠ The PR touches `.github` → token lacks `workflow` perm → **needs the Sponsor's browser merge** (see carried item C).
2. **Heavy-attack §8.2** — build Uma's spec'd defaults (≈0.95 s: 0.40 wind-up + 0.55 recovery, no cancel, 0.4× damping, §6 per-tier rows), judge the weight LIVE at soak via the console dials. Grounded: `heavy_windup`/`heavy_recovery`/`heavy_move_damping` are registered tweakables, so only the phase STRUCTURE is locked by building. **This was `86cau6prr`'s last pre-impl gate — the ticket is now dispatchable** (the input pick dissolved into a soak dial when #340 merged; light swings completed 07-22).
3. **`86cah7y5b` find-in-world = `sword_iron`, one per island region** (the ticket default). Progression objection refuted: `sword_iron` is weapon-only; the axe/pickaxe gate the resource ladder. Ticket has no open questions left.
4. **`86cah7z2q` AC4 heal item = FORAGED MEDICINAL HERB** (`IPickable`, berry-bush pattern). Watch: it adds a SECOND forageable plant that must read distinct from berry bushes at gameplay framing.
5. **`86cah7ym9` bone tier = RETIRED.** Three tiers final (wood/stone/iron). The 2026-07-01 "wood→stone→bone/metal" phrasing is superseded.
6. **`86cah7ym9` new weapon types = NONE.** Roster is the shipped five. Blunt + ranged surfaced and declined. → AC2 CLOSED; only AC1 (Uma's stale-spec correction, **dispatchable now, docs-only**) + AC3 (reusable recipe) remain.
7. **`86cah7z2q` HP bar = Uma's FULL §2.1 proposal** — `HpSegmentCount = 5`, box 260×34, baseline −162, ledger moves −188→−216. Watch: covering the BUILD stamp is a HARD FAIL, and the pre-existing 1 px HP/ledger plate overlap gets fixed incidentally (say so or it reads as a regression).
8. **`86caxhfg2` enemy-HP pip-row = DEFERRED behind body feedback.** `_HitFlash` / flinch / dust puff are verified ABSENT from `Assets/` — shipping the pip-row first would make it the only damage read. **Priya must file a body-hit-feedback ticket** (brief `combat-cluster-design-brief.md` §1.2/§2.5; first `ParticleSystem` in the project; no `MaterialPropertyBlock` on juice VFX). Its soak carries the pip-row question.
9. **`86cah7yuh` stun ON at easy** — ≤0.6 s, ≥3.0 s immunity; §7 tier table ships as written. Bounded by design: `ActionsBlocked` blocks verbs, not movement.

**Superseded by the above — the three "⭐ QUEUED 2026-07-27" blocks earlier in this file (Priya ×3 design Qs, Uma ×3 taste calls, the §8.2 pre-impl call) are ALL now answered. Ignore them.**

### ▶ CARRIED — the 3 items that need you in person (nothing else is blocked on you)
- **A. ⭐⭐ PR #337 pickaxe-mine soak** — unchanged, full package at the `⭐⭐ SOAK` block above (exe `C:\Trunk\PRIVATE\Far-Horizon-drew-swings-wt\Build\soak-pickaxe-1\FarHorizon.exe`, stamp `1194927`, TRUE-side-view instruction). Still the ONLY gate on #337.
- **B. Icon contact-sheet picks** — `C:\Trunk\PRIVATE\Far-Horizon-drew-wt\art-src\iconbaker-proto\contact-sheet.png` (`86camyvwn` in progress; ore S1/S2/S3 + ingot S4 + variant A–D; the ingot has NO real mesh — stand-in, flagged).
- **C. Branch protection + the `86camz787` merge** — the browser steps are in the `⭐ STAGED 2026-07-27` block above. Now paired: the ci.yml flip PR will ALSO need a browser merge (workflow-perm), and if protection is enabled first its required-check name list must include the renamed playmode job.

## ⭐ NEW 2026-07-27 ~23:1xZ — orch/coordination is 96 commits UNPUSHED (your call: push or leave)
**Measured, not estimated:** local `orch/coordination` HEAD `1c4b5aa`; `origin/orch/coordination` sits at `7afdb0d` = **96 commits behind**. Tonight's DECISIONS entries return **zero hits** on the remote ref.

**Two consequences:**
1. **Exposure.** 96 commits of coordination work (every decision logged tonight, all STATE headers, the away-queue, decisions-while-away) exist on ONE disk. This is the same class as the 2026-07-22 incident that cost three weeks of unstaged work — committed is safer than uncommitted, but unpushed is still a single point of failure.
2. **Nothing can cite them.** A PR, spec or research note that references `team/DECISIONS.md:<line>` hands its reader a dead link — this already produced a REQUEST_CHANGES on PR #348 tonight. Mitigated by a new rule (cite ticket ids + merged SHAs, never coordination-doc lines), but the underlying unreachability stays until a push or a harvest.

**Why I did not just push it:** the repo is PUBLIC (your 2026-07-27 decision), so pushing 96 commits of internal coordination — decision logs, away-queue, orchestrator process notes — is an outward-facing action on a public repo. That is on the never-auto-decide list, so it is your call, not mine.

**Options when you're back:** (a) push it — fastest fix for both problems, but it publishes the coordination trail; (b) leave it and rely on the next harvest PORT to carry the doc-worthy parts to `main` (slower, selective, keeps process notes private); (c) push it to a private mirror instead.

## 🛑🛑 2026-07-27 ~23:2xZ — SESSION RATE LIMIT HIT (resets 03:20 Copenhagen). Away loop STOPPED. Read this first.
**3 agents died mid-work on the limit.** No work is lost, but two worktrees hold UNCOMMITTED edits. **Do NOT run `git clean`, `git reset`, `git checkout --`, or `git stash drop` in any worktree below** ([[background-agent-rate-limit-death-salvage]]).

| Worktree | Branch | Uncommitted | What it was mid-doing |
|---|---|---|---|
| `Far-Horizon-erik2-wt` | `erik/hitflash-research` (PR #348, pushed @ `089eace`) | **1 file: `team/erik-consult/enemy-hit-feedback-hitflash-particle-flinch.md`** | Erik's 2nd fix round — Tess's REQUEST_CHANGES cite defects. He died on the Evidence bullet quoting Uma's §2.5. **PARTIAL EDIT — do not commit blind; inventory + finish, then commit.** |
| `Far-Horizon-drew-wt` | `drew/86cah7y5b-find-in-world` (no PR yet, branch not pushed) | Multiple `Assets/` files (Unity re-serialization churn visible; **full extent NOT enumerated — a `.cs` instrument may be in there**) | Find-in-world impl. Last words: *"`find=False` at runtime though authoring logged 4 seated sites. Not theorising — instrumenting the resolve."* **He was mid-diagnosis of a real runtime bug — that reasoning is the valuable part; resume, do not restart.** |
| `Far-Horizon-priya-wt` | `priya/hygiene-0727-late` (PR #349, pushed @ `9bed94c`) | only pre-existing `.claude/agents/tess.md` churn | Ticket-BODY edits (server-side, safe). Last words: she confirmed Devon's #349 Finding-1 correction and was fixing it **in the body, not a comment**. Some of the 5 bodies may be half-updated — re-check before trusting them. |

**Also `Far-Horizon-tess-wt` is dirty (53 modified + 2 untracked) — pre-existing, NOT from tonight's review, still awaiting your eyes. Untouched as promised.**

**Loop state:** cron `839dd854` DELETED; `auto-status.state` set `enabled=false`. Nothing will auto-resume — deliberate, so the reset window isn't burned by ticks that can't dispatch. **Re-arm with `/auto-status away` (or `on`) when you're back**, or swap accounts for a fresh window ([[sponsor-swaps-two-accounts-for-fresh-window]]) if you want it moving before 03:20.

**Resume order when capacity returns:** (1) inventory the two dirty worktrees BEFORE any git op; (2) resume Erik's #348 fix round; (3) resume Drew on the `find=False` diagnosis; (4) re-check Priya's 5 ticket bodies for half-applied edits; (5) then the queued items further down this file.

---

## ↻ 2026-07-28 ~20:2xZ — away tick 1 (session resumed, away armed at Sponsor's request)

### ✅ ONE-CLICK MERGE — PR #349 (all gates met)
```
gh pr merge 349 --admin --squash --delete-branch
```
**Gate evidence:** docs-only (2 files: `team/STATE.md`, `team/quality-bars.md`) so it spawns no CI run; `mergeable=MERGEABLE` at head `2d25340`; Devon peer review **APPROVE_WITH_NITS** (comment on the PR, both load-bearing claims independently re-derived); **all 3 of his NITs now applied** and orch-verified in the diff — NIT 1 took his wording verbatim ("colour ranks LAST of the four, never first"), NIT 2 rewrote Bar 10's lead so the invariant is *single-channel collapse* rather than colour-only (this was the hold — as originally worded the bar would not have caught its own motivating instance), NIT 3 widened the scope column to attract/affordance cues on world objects. `.claude/agents/tess.md` confirmed absent from the diff. No `needs-soak` / `sponsor-gate` marker. On merge: flip the Priya hygiene ticket to complete.

### 🔴 SPONSOR-GATED — carried
- **Canonical orch branch name (NEW, from tonight).** Your 103 local commits are safe on GitHub at `orch/coordination-2026-07-28` (`b2e355d`), and the old remote line is preserved at `archive/orch-coordination-2026-06-24` (`7afdb0d`). The force-push you approved onto `orch/coordination` was **denied by the repo's own hard deny rule** — orch cannot execute it. To finish it needs a **destructive** step: delete the stale remote `orch/coordination` (content already duplicated on `main` + the archive ref), then push local under the canonical name. **Not auto-decided — needs your explicit go.** Purely cosmetic; nothing is at risk if it never happens.
- **Icon contact-sheet picks** (`86camyvwn`) — `Far-Horizon-drew-wt/art-src/iconbaker-proto/contact-sheet.png`. Needs you to LOOK at the image; not a popup question.
- **Branch-protection browser click** — unchanged, staged from earlier.
- **`86camz787`** — the ci.yml advisory→REQUIRED flip is a `.github` PR and CANNOT be label-merged (token lacks `workflow` perm); needs your browser merge when it opens.

### Tick actions
- Dispatched: Drew (`86cah7y5b` find-in-world resume, holds the single Unity-build slot) · Devon (PR #348 dev factual-check — **DONE**, verdict below) · Uma (PR #348 citation fix round — **DONE**, `25f34ac`) · Priya (PR #349 + 3 board items — **DONE**) · Tess (PR #348 re-check, in flight).
- **PR #348 is NOT stageable.** Devon's factual-check returned **NO-GO on dispatching a dev against the note verbatim**, with 5 named corrections. The one that would have shipped a bug: the note's C# pairing writes `Time.time` into a shader that reads `_Time.y` (= `Time.timeSinceLevelLoad`) — constant-negative numerator → **permanently latched full-white enemy**, green in EditMode, broken only in the exe. Also: GPU Resident Drawer is **Disabled** (`FarHorizonURP.asset:86`), so the note's prescribed GRD draw-call A/B measures nothing; `Unlit/Particle` is not a URP shader; `LowPolyMeshes.cs` is Editor-only asmdef; and `GetComponent<Renderer>()` is null on both enemy roots (a blocker for the body-hit-feedback ticket).
- Doc captures applied to `.claude/docs/` (orch branch, ride the next harvest): the `_Time.y` clock trap · GRD-disabled · the three-different-shader-facts rule ("pinned" ≠ "assigned" ≠ "serialized") · corrected a **stale "ZERO hits" MaterialPropertyBlock claim** in `unity-conventions.md` (actually 5 files on `main`) · flagged the contested Fresnel-rim conclusion in `lowpoly-quality.md`.

### 🔴 NEW SPONSOR ITEMS — PR #351 (find-in-world, `86cah7y5b` → `ready for qa test`)
Drew shipped it; **soak-gated, NOT staged for merge.** https://github.com/TSandvaer/Far-Horizon/pull/351 (head `259d890`, MERGEABLE).
1. **A soak is needed** — exact exe path + expected HUD stamp to follow once the build artifact is confirmed. He played all three capture frames himself before serving.
2. **⚠ It touches `ci.yml`** (wiring `verify_weaponfind_gate.sh`, the only gate script in the repo that was never CI-wired) → auto-merge will fail on the token's missing `workflow` permission. **Expect a browser merge from you**, same as `86camz787`.
3. **One reviewer call for you or the reviewer, flagged in his PR body:** AC3 says the attract cue is "float-bob **ONLY**", but he added a non-harmonic yaw sway as a second channel under the newer quality-Bar-10 ruling (no cue may rest on a single channel). Read strictly, that is scope creep; read against Bar 10, it is required. One field pair + one `Update` line to strike if you want it literal.

**What he actually found — the ticket's premise was wrong.** `find=False` was not absent wiring: bootstrap authored all 4 sites and they were active. A `-1` sentinel (`WeaponFindPool.activeFindCount`) leaked into the AC5 settings row, `IntSettingEntry` clamped it to `0` and wrote it back, and because both are `Start()` with undefined order the pool skipped its tier seed and disabled every site. He reproduced it RED before fixing. Then eyeballing the frames caught a **second** defect the gate had passed: `PASS=True` on a frame showing the sword point-down in bare grass ~1m from an empty stump — both anchor checks were Y-only. Planar offset measured 1.800u → 0.104u after fixing the seat to measure XZ. **EditMode 1303/1303, PlayMode 7/7, `-verifyWeaponFind` PASS.**

**Deferred doc riders (apply at #351 merge, per the unmerged-cite rule):** `unity-conventions.md` — the "`-1` sentinel + a clamp band whose floor is 0, read across an undefined `Start` order, silently becomes *none*" class · `lowpoly-quality.md` §0 — an anchor/placement check must assert WHERE, not only height.

**Reviewer for #351 is NOT yet dispatched** — every persona is occupied (Devon `86caxjwev` · Uma #348 corrections · Tess `86caxj8zw` · Priya `86cay47zh` · Drew now on `86cay4282`). First free dev takes it; author cannot self-review.

**#348 update (~21:0xZ):** Uma's CORRECTIONS block landed at `b718042` — Devon's 5 defects are now transcribed at the TOP of the note, so it can no longer mislead a reader on `main`. **But it is still not staged:** Tess's APPROVE was against `25f34ac` and the head has moved, so the new block is unreviewed content and the reviewer gate is on the current head. Needs a delta re-check from the first free dev — Tess is occupied and Uma cannot self-review. Two PRs are now queued on that same free-reviewer slot: **#348** (delta re-check) and **#351** (full review).

**Deferred doc rider — apply at #348 merge** (held now per the unmerged-cite rule; the principle is general but its only instance lives on the unmerged branch): *a shader property's "inert at default" value attaches to WHAT THE PROPERTY CARRIES, not to a fixed number.* An amplitude/intensity float is inert at `0` (the shipped `_RimIntensity` / `_AOStrength` / `_MeadowPatchAmp` precedent, `unity-conventions.md:213`); a **timestamp** float is inert at a large negative (`-1000`). Opposite values, identical semantics. A reviewer auditing "does this default to a no-op?" by checking `== 0` will wrongly flag a correct timestamp default — which is exactly what happened on #348, where it read as a spec conflict between two properties that were never in conflict.

**~21:4xZ — PR #352 OPEN** (Priya, `86cay47zh` CC-BY purge, head `c751d43` off `fee2604`, ticket → `in review`). Reviewer Devon (he found the class and re-derived the anchors) — queued behind his #351 review. **7 of 8 files done; the 8th was blocked on me and is now cleared** — I applied her proposed `unity-conventions.md:173` replacement orch-side verbatim (rule unchanged, only its factual claims), so AC2 is satisfied.

**A correction she made to her OWN ticket, worth your eye:** the ticket asserted *"no license file ships"*. That was wrong — its grep pattern missed `Assets/Art/Character/Castaway/Castaway_Attribution.txt`, which has neither a `CC-` prefix nor the word "License". The CC-BY conclusion survives (that file covers Hyper3D-Rodin/Mixamo content, not CC-BY), **but a live retain-in-distribution attribution obligation exists and NO rendered credits surface exists to satisfy it** — her grep for `about-screen|AboutScreen|CreditsPanel|CreditsScreen|ShowCredits` across `Assets` matched only that `.txt`. Nothing stale is being *displayed*, so it is not urgent, but it is a real gap. She is filing two follow-ups: a credits/about surface (code) and the `Castaway_Attribution.txt`-vs-castaway-v4 staleness.

**`86camyvwn` reconciled:** flipped `in progress` → `to do` + **`sponsor-gate` tag added** (orch, via ClickUp REST — the MCP `update_task` has no `tags` param; verified live, tags now `["sponsor-gate","design"]`). Drew's dispatch was *delivered* — the contact sheet is pushed and there is deliberately no PR — so the next actor is YOU. A no-assignee `in progress` was inflating apparent occupancy in the idle-tick scan, which is the exact 2026-06-28 idle-failure shape.

### ✅ ONE-CLICK MERGE — PR #348 (all gates now met, ~22:1xZ)
```
gh pr merge 348 --admin --squash --delete-branch
```
**Gate evidence** (head `b718042`, `MERGEABLE/CLEAN`, no labels, 2 files: `team/erik-consult/enemy-hit-feedback-hitflash-particle-flinch.md` + `team/uma-ux/combat-cluster-design-brief.md`, docs-only so it spawns no CI run, no `needs-soak`/`sponsor-gate`):
1. **Tess `APPROVE_WITH_NITS`** on the citation/consistency layer (at `25f34ac`) — all 5 blocking items + 6 NITs cleared, and she independently confirmed the two defects the author had disclosed inside her OWN earlier "verified clean" list.
2. **Devon dev factual-check** — 5 technical defects found, incl. one that would have shipped (`Time.time` written into a shader reading `_Time.y` → permanently latched full-white enemy, green in EditMode).
3. **Uma's CORRECTIONS block** (`b718042`) transcribes all 5 at the TOP of the note, so it cannot mislead a reader on `main`.
4. **Devon delta re-check** of `25f34ac..b718042` — `APPROVE_WITH_NITS`, **nothing mis-transcribed**; he checked all 16 body line-refs (not the sample I asked for) and re-verified Uma's counter-evidence cites on `main` exactly. His words: the note is **safe to merge as a corrected research artifact**.

**Why this took four passes rather than one:** the citation layer and the technical layer were reviewed by different people with explicitly disjoint scopes, and Tess's approval did not cover the shader/GPU claims — she said so twice. A PR can be "approved" and still be a landmine when the approval's scope is narrower than the document. Worth remembering as a review-routing lesson, not just an incident.

**Merge riders to apply at #348 merge (3):** the `_HitFlash`-vs-`_HitFlashTime` inert-default rule (see the deferred-rider note above) · and confirm the two #351 riders stay pending until THAT merges.

**PR #352 — REQUEST_CHANGES (Devon, ~22:2xZ), NOT staged.** Two one-clause blockers, both worth the catch:
1. `weapon-tool-style-spec.md:268` — the ~49% midpoint-origin fact is attributed to the **CC-BY** axe, but her own cited source (`MovementCameraScene.cs:129`) says the retired **flint** axe = `wpn_axe_01`, the **in-house PR #100** axe, retired later under `86cajkk7h`. **Three axes and two retirements collapsed into one** — on the anchor he ranked first, i.e. the line a future modeller would act on.
2. `blender-asset-pipeline.md:427` still says "Tune the current shipped axe as the style reference | The shipped axe is a placeholder", contradicting her own `:17` rewrite in the same MANDATORY doc — her own `:10` self-consistency argument applies verbatim.

Everything else confirmed clean: all six anchors held at `fee2604`, the `031d43a` deletion + `#98`-predates-`#100` provenance exact, A2's shipped-asset half fully sourced, all AC2 greps reproduce, and the `:173` text I applied orch-side is sound.

**His gate judgment is the durable part:** her `git grep` guard is **honest, not tuned-to-pass** — but it is a **PATH guard, not a STALENESS guard.** It proves the routable path is gone; it does not prove the stale *claim* is gone. He proved that with 3 live construct-grep misses (`:427`, spec `:172`, erik `:95`), **two of them inside files she had edited.** Same family as the unanchored-needle class captured in `unity-conventions.md` §Process notes.

**Fix round is QUEUED for Priya** — she is mid-task on the attribution follow-ups and `priya-wt` holds this branch, so it waits rather than racing her worktree.

### 🔴 PR #354 — YOUR reported defects are fixed, soak pending (`86cay4282` → ready for qa test)
https://github.com/TSandvaer/Far-Horizon/pull/354 @ `f243e8a`. **Soak-gated, NOT staged.** Tess is on the QA pass now.

**Both of your complaints were ONE defect, and the ticket's theory was wrong.** You said the swing looked two-handed AND that the tool was still pivoting. Drew instrumented before fixing and found the seat measures **rigid** (`axisSpreadInHand 0.000°`, every clip) — nothing can pivot relative to the hand at any layer. What you were seeing both times is that **the Mixamo mine clip is authored two-handed**: hands locked 1.09–1.29 shoulder-widths apart during the swing, against 1.65–1.89 on the idle carry you approved and 1.77–2.86 on the axe chop, with the tool 63.8–89.7° off the hand line.

**The prime suspect on the ticket — and in my dispatch brief — was wrong.** Both named `rightArmEuler(-4,-50,-3)` (|Q| ≈ 50°, over the study's 40° gate threshold). That is the **v3/rollback default**; `MovementCameraScene.cs:1450` bakes `CastawayV4RightArmEuler(-5,-22,0)` = |Q| 22.6° on the live hero, comfortably inside the clip-safe band. He refuted it with measurement rather than inheriting it — which is the behaviour I want, and a reminder that my briefs carry theories that need testing, not facts.

**Fix:** a state-gated, transition-paired additive **left-arm de-grip** `(-40,0,20)` on the existing order-50 idiom. He measured the axis rather than trusting the doc's cheat-sheet — which turned out to be **inverted for this clip** (the documented `+X`-spreads-outward closes the hands, 1.08→0.86).

**Evidence:** PlayMode 306/306 · shipped-build `-verifySwings PASS=True` · `minHandSep` 1.508 SW vs the sweep's predicted 1.51 · `pickaxePeakTilt=42.2°` so #337's fold is unregressed. EditMode 1280/1285 with 5 reds he attributes to stale generated assets — **Tess is verifying that attribution specifically**, since 5 reds dismissed as environmental is the shape that hides a regression.

**⚠ WHEN YOU SOAK THIS, VIEWING MATTERS:** the gameplay orbit cam **cannot resolve hand separation** — from the normal camera you will not be able to tell. Use **`F9` → `[K]` → MINE** for a live before/after A/B at close/frontal framing. **Exact exe path + expected HUD stamp still to come** — I will not serve this without them (soak-handoff rule).

**Follow-up he flagged:** `-verifySwings` has **zero hits in `.github/`** — both pickaxe gates are manual-only and never run in CI.

### ✅ ONE-CLICK MERGE — PR #353 (~22:5xZ)
```
gh pr merge 353 --admin --squash --delete-branch
```
**Gate evidence** (head `b5e5724`, `MERGEABLE/CLEAN`, no labels, 1 file `tests/scripts/test_gate_scripts.sh`, shell-only so no Unity CI content gate applies, not soak-gated): **Devon `APPROVE_WITH_NITS`, no blockers** (comment `5109934787`) — and he **re-derived the negative control himself** rather than reading her table: pre-fix on the mutated tree gives `134 passed, 0 failed` exit 0 (the false-PASS reproduced), post-fix gives `133 passed, 1 failed` exit 1. All 8 windowed gates still match anchored, 1 hit each, so no over-tightening. On merge flip `86caxj8zw` → complete.

**Two findings from that review worth more than the PR:**
1. **Tess's non-blocking item should NOT be "fixed" — Devon proved anchoring it would make things worse.** `assert_launch_headless:849` is a **negated** find (asserts absence), so leaving it unanchored is structurally **false-RED-only**; anchoring it would narrow what counts as a hit and let a real offender slip through — i.e. it would CREATE a false-pass. **Leave it unanchored.** Captured in `unity-conventions.md`: anchor POSITIVE finds, leave NEGATED finds broad.
2. **A residual hole anchoring does not close (his mutation proof):** `assert_launch_windowed` is **one-sided** — it checks the window flags are present but never that `-batchmode` is absent. Adding `-batchmode` while keeping the window flags still greens the suite at `134 passed, 0 failed`. He offered a two-line fix. **Follow-up ticket wanted** (queued for Priya — she is mid-task, so not dispatched).

**Queued for Priya when she frees (3):** the #352 fix round (2 blockers) · this one-sided-assertion follow-up · a follow-up that `-verifySwings` has **zero hits in `.github/`** (both pickaxe gates are manual-only, never run in CI — flagged by Drew on #354).

**⚠ CORRECTION to my #354 note above — the rigid-seat claim as I wrote it was WRONG.** I told you "the seat measures rigid, nothing can pivot relative to the hand at any layer." Tess's QA shows that overstates it. The `axisSpreadInHand 0.000°` probe is a **tautology**: `HeldToolRig.cs:100` sets `transform.rotation = followRot * Euler(seatEuler)`, so that probe reads 0.000° whether or not a defect exists. The real proof is that formula **plus `followDamp == 0`** (`MovementCameraScene.cs:248`, pinned by `HeldToolRigTests:111` and `HeroAxeSceneTests:229`). At `followDamp > 0` (`HeldToolRig.cs:83-93`) the tool **does** pivot against the hand. **Drew's conclusion stands; his reasoning was conditional, not absolute.** So "the pivot was never a separate defect" is true *for the shipped configuration*, not by construction — and if anyone ever dials `followDamp` above zero, the pivot complaint can return.

**The 5 EditMode reds — attribution VERIFIED, not taken on trust.** Tess pulled the actual CI run: `30400341478`, `head_sha = f243e8a`, job `90413329638` SUCCESS, log `[EditMode] result=Passed total=1285 passed=1285 failed=0`, and the gate is strict (no `--allow-skips`). Same 1285 total as local. The local 5 reds were stale generated assets, as claimed.

**Prime-suspect refutation independently recomputed by her:** shipped `(-5,-22,0)` = |Q| 22.55°; `(-4,-50,-3)` = 50.34° and is rollback-only.

### 🔴 #354 SOAK BRIEF — do not serve until the build exists
Tess **opened the orbit capture herself** and confirmed my worry: the character renders ~60×100 px, so hand separation is **physically unresolvable** at gameplay framing. Serving this without instructions would waste your time. The soak ask must carry ALL of:
1. **Zoom in / use a front view** — the orbit cam cannot show it.
2. **`F9` → `[K]` → MINE** for the live A/B; **`(0,0,0)` is the "before"** state.
3. **⚠ Expect `weight = 0.00` until you actually swing** — otherwise it looks broken when it is idle-correct.

**BLOCKED ON A BUILD:** no soak exe has been cut for `86cay4282` yet, and `drew-swings-wt` (which holds the #354 branch) is currently occupied by Drew's #350 review. **I will not serve a soak without the exact exe path + expected HUD stamp** — queued to cut once that worktree frees.

Her 3 non-blocking nits are on the PR (unpinned PlayMode literals; mirrored composition; an `Assert.Greater(q,25f)` that would red on a *smaller* post-soak bake — worth knowing before you dial anything down).

**PR #350 — gate-green but HELD one round (~23:1xZ).** Drew's review: **APPROVE_WITH_NITS, no blockers** (comment `5110025056`). All three REQUIRED checks SUCCESS at `a972dfa` (structure / build / capture); the `UNSTABLE` mergeable state is only the advisory `playmode` job showing cancelled — routine, not a red.

**Why it is held rather than staged:** Drew found the AC5 sub-bullet attributes "tree-wide presence" to a guard that was **always per-file** (`202a4db:98/101`) — i.e. **the PR that exists to fix a wrong because-clause introduced a new wrong because-clause**, in the same mandatory doc. He rated it a NIT; I am not shipping it, because a wrong CAUSE is exactly what the next reader re-cites and that is this ticket's whole premise. Devon (the author) is applying the one-clause fix now. **On his push, CI re-runs — re-verify all 3 required checks on the NEW head before staging** (label-after-green rule).

**What Drew CONFIRMED, so nobody re-derives it:** AC1 states his `86cavj8pf` measurement correctly — 10 and 13 are reachable only by excluding `CombatPlayModeTests` (11−1 files, 16−3 sites), and "deliberately SIBLING under a full-set denominator" is what he meant; `2e58edc` is post-fix and `202a4db` added the bullet. The `15` floor is correctly bracketed `withRig=14 < 15 <= rigSites=19`, re-measured at head. AC6's zero-test-delta confirmed twice, including base-main run `30393626628` also reading `1274`.

**One caveat he raised, routed to Devon's judgement:** the floor's collapse-detection has **margin 1** — a 15th rig-carrying file would lapse it; the unconditional net is `Guard_RedsOnASecondBareRig`'s `AreEqual(4, RigSites)`.

**#350 fix LANDED at `e10b153`** (+7/−1). Devon verified Drew's NIT independently at `202a4db` before applying it — and found the guard's REAL holes were **no recursion** (`:59` `TopDirectoryOnly`) and **presence-not-count in-file** (`:101`); "tree-wide presence" named a defect it never had. He also took Drew's margin caveat as warranted and put it in the CODE COMMENT rather than the PR body, on the reasoning that the next reader reads the comment. Both changes trace to Drew's own review items, so no re-review round is needed. **CI at `e10b153`: `structure` SUCCESS, `build` still running, `capture` not reported yet — NOT stageable until all three are SUCCESS on THIS head.** Next tick re-checks and stages.

**#354 soak build DISPATCHED** (Drew, `drew-swings-wt` now free): `Build/soak-degrip-1/`, with instructions to read the HUD stamp out of the built exe rather than quoting a SHA, to PLAY it before reporting, and to confirm-or-correct the four viewing instructions (zoom/front, `F9`→`[K]`→MINE, `(0,0,0)`=before, weight reads 0.00 until he swings) — plus a Danish-layout check on any key binding. I will serve HIS wording, not mine.

---

## 🎯 END-OF-SESSION HANDOFF — 2026-07-29 ~00:0xZ (drain complete, cron killed)

### ⭐ THE SOAK YOU ASKED FOR — #354, ready to play
**Exe:** `C:\Trunk\PRIVATE\Far-Horizon-drew-swings-wt\Build\soak-degrip-1\FarHorizon.exe`
**Expected HUD stamp — verify before judging:** `BUILD zoned | 2026-07-28T22:05:21Z | f243e8a`
(Read out of the built exe by Drew, not computed. He played it: boots windowed 1600×900, 60 FPS, character intact.)

**⚠ MY EARLIER VIEWING INSTRUCTION WAS WRONG — use this one.** I wrote `F9 → [K]`. **F9 alone draws nothing**: the overlay gates on `DebugOverlays.Visible`, which is OFF by default and toggles on **F10**. Drew caught it by actually running the build.

**How to judge it:**
1. **Zoom in or use a front view** — mouse wheel 14u→6u takes the character from ~55×95 px to ~200×280 px; RMB-drag orbits. At the default camera the hand separation is **physically unresolvable**, so judging from there tells you nothing.
2. **F10, then F9, then `[K]` ×9** for the live A/B.
3. **`(0,0,0)` is the "before" state.** Getting there needs two axes: **Shift+T ×4, Shift+J ×2**.
4. **The weight reads `0,00` until you actually swing** — that is correct, not broken.

Danish layout: all keys safe. Decimals render comma-style.

**What it should look like:** hand separation **1.508 SW** (was 1.08–1.30 pre-fix), peak de-grip weight **1.00**, left arm clearly off the phantom haft. `-verifyMine` also passes end-to-end.

**⚠ Heads-up:** a boar and a snake maul the idle player at spawn. Pre-existing, not from this change — but it WILL interrupt you while you are trying to look at the hands.

### ✅ FOUR ONE-CLICK MERGES (all gate-verified; run in any order)
```
gh pr merge 348 --admin --squash --delete-branch
gh pr merge 349 --admin --squash --delete-branch
gh pr merge 350 --admin --squash --delete-branch
gh pr merge 353 --admin --squash --delete-branch
```
**#350 went green during the drain** — all four checks SUCCESS at `e10b153` (structure / build / capture / playmode), `MERGEABLE`. Gate evidence for the other three is in the blocks above.

### 🔴 STILL YOURS — nothing here is something I can do
- **#351** (find-in-world) — Devon `APPROVE_WITH_NITS`, but it **touches `ci.yml`**, so the token cannot merge it: **browser merge**. Also unsoaked.
- **#352** (CC-BY purge) — Devon `REQUEST_CHANGES`, 2 one-clause blockers. Priya's fix round is queued, not dispatched.
- **#354** — the soak above.
- The **canonical orch-branch cleanup** (needs a destructive delete; your 103 commits are already safe on `orch/coordination-2026-07-28` and the old line is archived).
- **Icon contact-sheet picks** (`86camyvwn`, now correctly `to do` + `sponsor-gate`).
- **Branch-protection browser click.**

### Corrections I made to my own claims this session (so you can calibrate)
1. Told you the seat "cannot pivot at any layer" — **overstated**; it holds only because `followDamp == 0`.
2. Told Drew the Fresnel doubt was live — **the doubt was mine and wrong**; the rim is genuinely unreachable.
3. Briefed a prime suspect (`rightArmEuler` |Q|≈50°) that turned out to be the **v3 rollback default**, not shipped.
4. Gave the `F9` instruction above without testing it — **wrong**, it needs F10 first.
5. Flipped `86cajt6k4` in-progress before reading its body — it is **hard-gated**; reverted.
6. Briefed Priya that two new tickets were non-build lane — **both are Unity-build lane** (`paths-ignore` covers only markdown/`team/`/`.claude/`).

### Open item worth a `/name-the-bar` pass
No confirmed quality bar covers **UI-panel visual read** — bars 1-9 are world/prop/motion. Priya flagged it on `86cay4k73` rather than inventing one.

---

## 2026-07-29 ~20:41Z — AWAY TICK 1: four consecutive agent deaths, API 529 storm

**Away mode armed 20:31Z** — cron `3100202c`, 15-min tick at :07/:22/:37/:52. Stay-awake verified ON (display mode, pwsh PID 24664 alive — checked the PID, not just the state file).

**FOUR consecutive background-agent deaths, all `API Error: 529 Overloaded`:**
1. Drew — `86cay4282` round 4 (left-arm Two-Bone IK), first dispatch
2. Drew — same task, re-dispatch
3. Priya — #352 fix round + `86caxjx26` re-scope + 2 follow-up tickets + board hygiene
4. Tess — QA pass on PR #351 / `86cah7y5b`

**Nothing was lost.** Verified: `drew-swings-wt` still at `1bc10ac` with 56 dirty entries (the pre-existing deliberate churn, intact); PR #354 unchanged at `1bc10ac`; no commits from any of the four dispatches.

**Decision: STOPPED dispatching** rather than re-firing into an overloaded API. This is a transient server-side fault, not a denial and not a per-task problem — a tight retry loop would just kill more agents. The away cron is the retry mechanism; next fire ~20:52Z will re-scan and re-dispatch.

**0 agents in flight as of 20:41Z.**

**Board scan (fresh, 31 open tickets) — the structural finding:** ZERO non-build tickets sit in `to do`. The only docs-lane ticket (`86cay47zh`, #352) is already `in review` awaiting Priya's fix round. So the non-build parallel lane can only be fed by review / spec / board work, never from the ticket pool as it currently stands. Worth a Sponsor decision on whether to seed some non-build tickets so the fan-out lane has fuel when the single Unity build slot is occupied.

**Not dispatched, with reasons (fill-or-justify):**
- **Uma** — candidates `86cagfn8h` (open-horizon / remove distant mountains) and `86cacewju` (chamfer-highlight bevel) exist, but only their TITLES have been read. Dispatching a UX spec off a title violates read-the-ticket-body-before-dispatch. Needs a body read first.
- **Devon** — every remaining `to do` ticket is build-lane and the single build slot belongs to Drew's IK round. He is also the assigned reviewer for both Drew's and Priya's in-flight work.

**Board hygiene deliberately NOT done inline** (Priya owns the board; inline ClickUp writes make the orchestrator unresponsive). Queued in her brief: `86cau4za2` sits in the open `to do` pool despite its own title saying `[DEFERRED — NOT standalone-dispatchable]`, and `86cavj6p1` has a mojibake char where an em-dash belongs. No status flips this tick either — a same-turn ClickUp read+write trips the anti-fabrication hook.

## 2026-07-29 ~20:47Z — AWAY TICK 2: 3 re-dispatched; a board-shelving defect found

**API recovered.** Three re-dispatches launched after tick 1's 529 storm (agentIds returned = launched; liveness not yet confirmed): Drew on `86cay4282` round-4 left-arm Two-Bone IK; Priya on the #352 fix round + `86caxjx26` re-scope + 2 follow-up tickets + hygiene; Tess on the never-QA'd PR #351.

**Incidental good news:** `priya-wt` HEAD is `c751d43` — her CC-BY purge work IS committed. The #352 blockers are review fixes on top of real work, not a lost branch.

### 🔴 BOARD-SHELVING DEFECT — the dispatch pool is overstated
Read the two Uma candidates' BODIES (not titles) before dispatching, and **both are non-dispatchable while sitting in plain `to do` with zero tags**:

- **`86cagfn8h`** (open-horizon / remove distant mountains) — its own body says *"NOT autonomously dispatchable; surfaces to the Sponsor for the soak verdict"*, is a Unity-build ticket behind the single slot, owner **Devon or Drew** (not Uma), and requires a **`/name-the-bar` pass to confirm the open-horizon quality bar BEFORE dispatch**. Uma's part is already DONE and MERGED (`team/uma-ux/open-horizon-direction-spec.md`, #199).
- **`86cacewju`** (chamfer-highlight bevel) — **explicitly DEFERRED by the Sponsor 2026-06-22** with a named trigger ("when hero props are re-authored in the unified Blender style"), owner orch Blender-MCP R&D-lane. Not Uma, not now.

**That makes THREE tickets now confirmed mis-shelved** — `86cau4za2`, `86cacewju`, `86cagfn8h` — all deferred-or-gated yet indistinguishable from dispatchable in a status scan. **This is why the "dispatch pool" reads as 22 tickets when the genuinely-dispatchable set is a fraction of that**, and it is the mechanism behind the 2026-06-28 idle-hours failure. The board has no `blocked` status by design, so the fix is tags (`sponsor-gate` / a deferred marker). Queued for Priya's NEXT round — I could not write it this tick because a same-turn ClickUp read+write trips the anti-fabrication hook, and I had just read these two bodies.

### Fill-or-justify, now evidence-backed
- **Uma — idle, and correctly so.** Both candidate tickets are non-dispatchable per their own bodies (above); her open-horizon spec is already merged. No UX work exists on this board that isn't gated or already done.
- **Devon — idle, justified.** Single Unity build slot is Drew's; every remaining `to do` is build-lane. He is reviewer-on-deck for both Drew's and Priya's in-flight work.

### 🟡 SPONSOR ITEM (new)
**`86cagfn8h` is blocked on a `/name-the-bar` pass** to confirm the open-horizon quality bar — and naming a standing quality bar is a Sponsor-confirmed, subjective call I will not auto-decide. That bar is a hard prerequisite in the ticket's own body, so the open-horizon work cannot dispatch until you name it.

**Staged this tick:** nothing new. #348/#349/#350/#353 remain the four one-click merges. #354 is soak-gated (not staged regardless of CI). #351 needs your browser merge; #352 is mid-fix.

## 2026-07-29 ~20:52Z — AWAY TICK 3: root cause found — OPUS is overloaded, not the tasks

**SEVEN consecutive persona-agent deaths**, all `API Error: 529 Overloaded`: Drew ×3 (`86cay4282` round-4 IK), Priya ×2 (#352 fix + hygiene), Tess ×2 (#351 QA).

**Root cause, evidence-backed — it is the MODEL, not the work.** In the same window a **Sonnet** Explore helper ran a full board scan cleanly. Checked the persona frontmatter: `drew`, `priya`, `tess`, `uma`, `devon` are all **`model: opus`**; `erik` is **`model: sonnet`**. So: Opus capacity is exhausted, Sonnet is healthy, and every opus-backed persona dies on dispatch while sonnet-backed agents succeed.

**Nothing lost across all seven.** `drew-swings-wt` still `1bc10ac` / 56 dirty (deliberate churn intact); `priya-wt` `c751d43`; `tess-wt` `b5e5724`; PR #354 unchanged. No half-written state anywhere.

**Dispatched the one persona that CAN run:** Erik (sonnet) on `86cag93zb` research — headless RenderTexture-readback capture viability. Chosen because it is the only research-shaped ticket on the board that needs no repo-code access (he has no worktree, and the orch tree's `Assets/` is ~230 commits stale), and because it is the ticket's own AC1 pre-work. High leverage: it targets the 1-runner capture pin that serializes the whole team behind a single Unity build slot.

**Board re-scanned fresh (gate-mandated): 31 open, 26 to do / 3 in review / 2 ready-for-qa — IDENTICAL to the 20:34 baseline.** No ticket had a `date_updated` inside the window, confirming none of the dead agents wrote anything.

### 🔴 SPONSOR DECISION NEEDED — the only lever that unblocks the team
**Should opus-backed personas temporarily fall back to `model: sonnet` while Opus is overloaded?**

I will NOT auto-decide this. The all-implementation-work-runs-on-opus policy is a documented Sponsor quality decision, and Drew's current task (two-bone IK maths, bone-axis measurement, reach clamping) is exactly the kind of work where model capability shows. Downgrading it is a quality-vs-throughput trade that is yours, not mine.

- **Option A — hold on Opus.** The cron keeps retrying; work resumes at full quality when capacity returns. Cost: the team is stalled meanwhile, possibly for hours.
- **Option B — temporary sonnet fallback for mechanical work only** (Priya's board hygiene, Tess's QA read) while Drew's IK waits for Opus. Keeps the non-build lane alive without risking the hard task.
- **Option C — sonnet across the board** until Opus recovers. Maximum throughput, accepts quality risk on the IK round.

I have queued nothing further into Opus — seven deaths is decisive evidence, not a guess, and re-firing would just burn more agents.

## 2026-07-29 ~23:0xZ — AWAY TICK 4: Erik delivered; Opus still down (8th death); hygiene defect is 2× bigger than thought

**Opus still 529.** An 8th persona death (Drew round-4 IK, 4th attempt). Sonnet remains healthy — both Explore scans and Erik's full research task completed cleanly on it. **The model-fallback decision below is still the only lever that unblocks Drew/Priya/Tess/Uma/Devon.**

**✅ Erik DELIVERED — and refuted the ticket's own AC1.** Committed at `b21f258`: `team/erik-consult/headless-rendertexture-readback-research.md` (24 KB). Verified the load-bearing claims are in the file itself, not just his summary.

### 🔴 `86cag93zb`'s AC1 is WRONG — correct it before anyone spends a build slot
AC1 currently says "prove it produces a valid frame under **`-batchmode -nographics`**". Per Erik (Strong evidence, official Unity Manual): **`-nographics` skips graphics-device init entirely, so it precludes ALL RenderTexture rendering — not just backbuffer capture.** That flag combo can never work. The viable target is **`-batchmode` alone**.
- **`Camera.Render()` is documented unsupported under URP** (Unity-staff-confirmed) and separately issue-tracker-reported to halt scene time under `-batchmode` on Windows. The correct path is passive per-frame rendering into `targetTexture`, or URP's `SubmitRenderRequest`/`SingleCameraRequest`. A naive implementation will hit this.
- **"Session-independent" is over-promised.** Removing the swapchain/compositor contention that pins captures to one runner IS achievable and evidence-supported — but true Session-0/no-login headless rendering is NOT: Windows Session-0 isolation blocks GPU/Direct3D for services at the OS level, independent of any Unity flag. The ticket title's promise needs narrowing.
- **Cheap side-finding worth testing first:** `Application.runInBackground` defaults to `false` — a candidate alternate explanation for some of the observed 2-runner contention.
- **Dependency satisfied:** `86cafz9tg` (the CI-split) is `complete` per live fetch, so the "sequence after" note no longer blocks.
- Erik flagged frame-warmup count, URP swapchain specifics, and N-runner GPU contention as **Moderate-to-Hypothesis** — exactly what the AC1 spike must MEASURE rather than assume.

### 🔴 HYGIENE DEFECT IS 10 TICKETS, NOT 5
A body-text sweep for gating language found **ten** open tickets that are deferred or soak-gated in their own text while carrying no `sponsor-gate`/`needs-soak` tag:
- Previously known: `86cau4za2`, `86cacewju`, `86cagfn8h`, `86cajt6k4`, `86caa9zju`
- **NEW:** `86caxjwb3` (AC6 soak-gated), `86cay4282` (soak-gated feel+visual), `86cah7z2q` (AC7), `86cah7yuh` (AC9), `86cah7y5b` (AC7)
- `86cag93zb` carries **no tag at all**.

**Why this matters:** a status-level scan cannot see AC-level gating, so the "dispatchable pool" reads far larger than it is — the exact mechanism behind the 2026-06-28 idle-hours failure. **Recommend a standing rule: if a ticket's ACs contain a soak gate, the ticket carries `needs-soak` at the ticket level.** That is a process change, so it is yours to approve, not mine to impose.

The sweep correctly rejected false positives (`86cay4k73` says "Not soak-gated" — negated; `86cau6prr`'s gating text is marked SUPERSEDED), which is the negated-find trap documented in `unity-conventions.md`.

**Board otherwise unchanged:** 31 open / 26 to do / 3 in review / 2 ready-for-qa, byte-identical to the 20:34 and 20:49 scans. No `date_updated` after 20:49Z — independent confirmation that none of the eight dead agents wrote anything.

**Staged:** nothing new. #348/#349/#350/#353 remain the four one-click merges.

### ⚠ CORRECTION to tick 4's hygiene claim (same tick, before you read it)
I wrote that ten tickets are mis-shelved and that this inflates the dispatchable pool. **That overstated it, and the distinction matters:**

- **An AC-level soak gate gates the MERGE, not the DISPATCH.** `86caxjwb3`, `86cah7z2q`, `86cah7yuh` are soak-gated in their ACs but their *implementation* is fully dispatchable — per CLAUDE.md a sponsor gate blocks only hard-dependents. Listing them as "non-dispatchable" was wrong. Same for `86cay4282` / `86cah7y5b`, already in QA.
- **The genuine, pool-inflating problem is narrower:** untagged *deferrals* and *hard-dependency* gates — `86cau4za2`, `86cacewju`, `86cagfn8h`, `86cajt6k4`, `86caa9zju` (deferred/gated in body), plus `86caxjwhh` (hard-dep: PR #346 must merge first), `86caxhk6v` (#341), `86caxgyc4` (#337). Those DO read as dispatchable to a status scan and are not.
- **`86cag93zb` is now UNBLOCKED** — its only gate was "sequence after `86cafz9tg`", and Erik confirmed that ticket is `complete` via live fetch. Its next step is the AC1 spike (build lane), with the corrected flags now on the ticket.

### ✅ THE ACCURATE PICTURE — the blocker is Opus, nothing else
**Ten tickets are genuinely dispatchable right now:** `86cay4k73`, `86cay4hyz`, `86caxjx26`, `86caxjwb3`, `86cavj6p1`, `86cav8y74`, `86cau6prr`, `86camz787`, `86cah7z2q`, `86cah7yuh`.

**All ten are build-lane, and the Unity build slot is FREE** (Drew is dead, not working). So the team is not blocked by dependencies, by the build slot, or by your gates — it is blocked *solely* because every build-capable persona runs on Opus and Opus is returning 529. Erik (sonnet) cleared the only research-shaped ticket on the board and has no further ungated, repo-free work.

**That makes the model-fallback question the entire critical path.** With sonnet fallback, ten tickets and a free build slot are immediately workable. Without it, the team does nothing until Opus recovers.

**Board verified unchanged:** 31 open / 26 to do / 3 in review / 2 ready-for-qa. Only `86cag93zb` moved (`date_updated` 2026-07-29T21:19:42Z) — my AC1-correction comment, confirmed landed.

---

## 2026-07-30 ~01:1xZ — AWAY TICK 6: OPUS RECOVERED. Drew's IK landed. Three slots filled.

**Opus is back** after 8 consecutive 529 deaths. **The model-fallback question from tick 3 is now MOOT — no decision needed from you.** Ignore Options A/B/C; the team is running on Opus as your policy requires.

### ⭐ #354 ROUND 4 — the left hand now actually touches the haft. SOAK READY.
**Exe:** `C:\Trunk\PRIVATE\Far-Horizon-drew-swings-wt\Build\soak-twohand-3\FarHorizon.exe`
**HUD stamp — verified baked into the build, not computed:** `zoned | 2026-07-29T23:03:52Z | 0a4af5e`
(`0a4af5e` is the parent of PR head `cd6fec1` — the usual committed-stamp lag. Drew played it before reporting and re-verified the gate against the served copy.)

**Steps:** pickaxe on belt → click a boulder → **F10** → **F9** → **K** until the panel reads `MINE SEAT` → **F** front-snap. `[R]`/`[V]` slide the haft; **`[Z]`/`[X]` are new reach keys**. All Danish-safe.

**Result, verified:** worst palm-to-haft **28.2 cm → 10.6 cm**, against a re-derived **13.0 cm** touching bound. PR head `cd6fec1`, MERGEABLE, **all four CI jobs SUCCESS**.

**The important structural fix:** the left-hand cap is no longer a hand-tuned number — it is now DERIVED from geometry: `LeftHaftPassSW = (LeftHandRadiusM + HaftRadiusM) / ReferenceShoulderWidthM` (`TwoHandGripRead.cs:110`). The old cap (0.80 SW ≈ 37 cm) had been calibrated from what a static seat could ACHIEVE, which is why it printed PASS for three rounds on a hand you could see wasn't touching. **Tightening it means the round-3 build now REDS** — a bar that rejects the previously-passing state is a real bar. He also moved the left measurement from hand-origin to PALM CENTRE.

**⚠ Honest limitation — the fix is good but not complete.** Drew measured that the seat parks the haft up to **63.4 cm** from a **54.0 cm** left arm, so **~47% of swing frames are genuinely out of reach**. The residual 10.6 cm is therefore seat DISTANCE, not IK solve slack — more IK tuning cannot fix it. Moving the seat ~10 cm closer drives it to ~0. Filed as a follow-up in Priya's brief (MINE-seat re-fit with a left-arm-reach objective). **Judge the soak knowing some frames still cannot reach.**

**Credit where due:** Drew refuted three of his own hypotheses this round and reported it — including that his "frontal" capture was actually showing the back of the head. The shipped-build gate caught every one that mattered.

### Dispatched this tick (3)
- **Devon** — peer-review PR #354 round 4 (solver reach-clamp + elbow-flip guards, the order-110 placement, the derived cap, and the two late test-fix commits whose own messages say the fixture had been measuring the ONE-HANDED seat).
- **Priya** — six items: #352 fix round, `86caxjx26` re-scope, `86cag93zb` AC1 rewrite, three follow-up tickets, the untagged-gating hygiene pass, status reconcile.
- **Tess** — the never-QA'd PR #351.
- **Uma** — still no ungated UX work on the board; her open-horizon spec is already merged.

**Staged:** #348/#349/#350/#353 unchanged. #354 stays unstaged — soak-gated on you, and now genuinely worth your eyes.

### ✅ #354 round 4 — Devon: APPROVE_WITH_NITS, no blockers (2026-07-30 ~01:3xZ)
Comment `5124613534`. Verified independently by him at `cd6fec1`, MERGEABLE/CLEAN, 4/4 SUCCESS (run `30499962400`). He **re-derived every number rather than reading Drew's table** — all six review axes pass:
- Solver clamps structurally (`TwoBoneIkSolver.cs:165,172`); he re-derived the shipped **157.0° elbow** from the segment lengths and it matches. Elbow side pinned via `n = axis × poleDir` using the clip's own elbow as pole (`:190,198`); an unusable plane refuses outright (`:184`). **Both ugly IK failure modes are genuinely guarded.**
- Ordering clean: 50/60/65/70/100/110 verified in source; orders 60+65 write `localRotation` only, so the order-110 world write preserves them, and nothing runs after 110.
- Cap genuinely mesh-derived: `(0.0894+0.0448)/0.4580 = 0.293 SW`. **Note a small figure discrepancy — Devon computes 13.42 cm where Drew's report said 13.0 cm.** Immaterial to the verdict (round-3's 28.2 cm reds under either anchor) but it is an unreconciled number between two reports; his NIT about "two test asserts being cross-anchor" may be the same root.
- He swept for the late test-fix defect shape and found `ApplyPoseChain` omits order 60 — harmless because finger-curl is right-hand-only (`MovementCameraScene.cs:1639-1668`).
- **He confirms the reach arithmetic and agrees the remedy is a seat re-fit, not IK tuning** (0.6340 − 0.5293 = 10.47 cm over-reach vs 10.75 cm residual; `shellFraction` is already at its hard ceiling).

**6 NITs, none blocking. The one that matters for YOUR soak:** the ~0.25 s ease-in is skipped by every judged assert (`MineSeatPlayModeTests.cs:347`) — so the blend-in is effectively untested. If the pin looks like it *snaps* on rather than easing, that is the untested path, not your imagination.

**#354 gate state:** CI ✅ · peer review ✅ · **soak ⏳ YOURS** · Tess QA not run on this PR. Staying unstaged — soak-gated by policy regardless of the green gates.

### ✅ PR #351 — Tess: PASS_WITH_NITS, no blockers (2026-07-30 ~01:4xZ)
Comment `5124620618`. AC1–AC6 met; **AC7 is present and complete and awaits YOUR soak** (gates the merge, not QA).

**She cleared Devon's B1 with independent evidence:** run `30396988792` is now `run_attempt 2 / success`; capture job `90407851703` = **40 steps, success**, `20:58:31Z→21:02:51Z` — versus attempt 1 (`90403560377`, `steps=0`, cancelled, 1 s). Step 37 "Weapon-find capture gate" succeeded. **Capture evidence genuine and fresh:** artifact created `21:02:43Z`, inside the executing window; `54f301c…` = `Merge 259d890 into fee2604` and `main` tip is still `fee2604`, so the merge-ref is current. She downloaded it, read `PASS=True`, and eyeballed all three frames.

**Her findings that Devon missed:**
- **F1 (the one that matters):** E-loot is exercised only via a `RequestLoot()` latch + `agent.Warp` teleport (`WeaponFindVerifyCapture.cs:217/240`, `WeaponFindPlayModeTests.cs:126`). `Input.GetKeyDown` is never tested — **your soak is the only real-input gate on this feature.** She notes it is the same layer that burned soak-5.
- **F3:** `DefaultLootRadius = 1.6f` is the loosest radius in the game (siblings are 1.4 / 1.0). Worth a look during the soak — loot may trigger further away than feels right.
- **F4/F5:** a seat-rig fork that a side-profile pose cannot discriminate.
- **B2** downgraded to a NIT (ticket body stale; corrected on-thread). Owed: a #351 body edit + an `86caxjx26` comment — routed to Priya, who is already mid-flight on that re-scope.

**⚠ CORRECTION — my dispatch brief contained a wrong premise, and she caught it.** I told her a new acquisition verb must join the shared left-click claim chain AND `MineVerbArbitrationTests`. **False:** E-loot is not on the left-click chain at all (`MeleeAttack.cs:276-278`), so those tests are correctly untouched. The real untested gap is nearest-wins arbitration against a second `IPickable`, plus ~32 scatter bushes missing from the avoid-list. I wrote an untested assumption as fact in a brief — the exact failure this project has a standing rule against.

**#351 gate state:** CI ✅ · Devon APPROVE_WITH_NITS ✅ · Tess PASS_WITH_NITS ✅ · **soak ⏳ YOURS** · and it **touches `ci.yml`, so only a browser merge works** (our token lacks `workflow` perm). Two independent sponsor gates — not stageable as a one-click command.

### Dispatched: Devon → `86cav8y74` (wood-in-hand + wood-chop capture gaps)
He is the ticket's named owner, Drew reviews. Takes the single Unity build slot. Briefed with Tess's F1 finding, since the teleport-not-real-input weakness she just documented is precisely the class of gap this ticket exists to close.

**Drew — idle, justified.** Round 4 is done and awaiting your soak; the build slot is now Devon's, and every remaining `to do` is build-lane. He is reviewer-on-deck for Devon's PR.

### ✅ Priya — all six items complete (2026-07-30 ~00:0xZ). She refuted THREE of my premises.
PR #352 @ `2dc6684`, MERGEABLE, no CI by design (`paths-ignore` skips docs-only). Drew dispatched to re-review (Devon raised the blockers but is on `86cav8y74`).

**1 — #352 fix round landed.** Both Devon blockers + both his optional items. The in-hand-scale bullet now enumerates **three** axes instead of collapsing them: CC-BY Viktor.G (*no origin measurement is held — the doc no longer asserts one*), in-house flint `wpn_axe_01` (**owns** the ~49% midpoint + `+0.34235` shift, retired `86cajkk7h`), `wpn_axe_stone_01` (grip origin ~24%, zero shift). She re-derived every fact from `origin/main` herself.

### 🔴 THREE CORRECTIONS TO MY OWN BRIEF — one would have caused real damage
1. **`86caxjwhh` / `86caxhk6v` / `86caxgyc4` are NOT dependency-gated.** I instructed her to tag them as blocked on PRs #346/#341/#337. **All three PRs are MERGED** — I verified independently: #346 `2026-07-27T21:53:02Z`, #341 `2026-07-27T19:01:45Z`, #337 `2026-07-28T19:49:46Z` (and `fee2604`, which I have been quoting as main's tip all night, *is* #337's merge commit — I had the evidence in front of me). **Tagging them would have HIDDEN three ready tickets from every future scan.** She refused the instruction, posted correction comments instead, and applied no tags. The genuinely dispatchable pool is **13, not 10**.
2. **`-verifySwings` has no gate script at all** — not merely no CI call, as I briefed. Her counts also differ from mine (`verifyMine` 19 / `verifyBoulder` 13 vs my 17/11) because we measured at different SHAs.
3. **`86cay4282`:** she found Devon's review had already landed and left the status alone, adding `needs-soak` + `sponsor-gate` rather than advancing it.

**2 — `86caxjx26`** stale scope block replaced: **2 of 6** (`dagger_stone` + `sword_stone` + AC3), conditional on #351. She verified 4 iron predicates on Drew's branch, 0 on main.
**3 — `86cag93zb`** retitled + rewritten: AC1/2/3 already DONE (#248/#250), AC1 corrected to `-batchmode` alone, `Camera.Render()` banned, AC4 narrowed to a conditional split (**the runner pin STAYS**), session-independence refuted, dep confirmed complete. Tagged `parked`.
**4 — Three tickets filed:** `86caynve7` (one-sided assert — **Devon wants it folded into open PR #353**, cross-posted), `86caynve9` (CI-wire `-verifySwings`), `86caynveb` (MINE-seat re-fit; **Bar 8 governs — you dial, no guessing**).
**5 — Tags applied:** `86cau4za2`/`86cacewju` `deferred`; `86cagfn8h` `sponsor-gate`+`needs-soak`; `86cajt6k4` `parked`; `86caa9zju` `sponsor-gate`+`deferred`. `86caxjwb3`/`86cah7z2q`/`86cah7yuh` correctly untouched (AC-level soak gates the merge, not the dispatch).

### 🟢 FIXED BY ORCH THIS TICK — a defect in the always-loaded config
`CLAUDE.md:66` claimed *"the shipped axe is a placeholder, not the anchor"* — the same stale claim she fixed at `blender-asset-pipeline.md:427`. Because `CLAUDE.md` auto-loads into **every** session, that line has been telling every agent the shipped asset is not the style reference. Corrected against her authoritative `:17` text: the live in-house `Assets/Art/Props/WeaponPack/` set **is** the anchor; the CC-BY `CastawayAxe/` placeholder was deleted with its licence in PR #100 (`031d43a`, `86cabh907`). She was right to leave project config to the orchestrator.

### 🟡 STILL OWED (not lost)
- **STATE.md not bumped** — `main` is 282 lines behind `orch/coordination`; she avoided editing it from her branch because it would collide at harvest. Correct call.
- **An unconfirmed quality-bar candidate** — #354's "one haft passing through both hands" — is recorded in `86caynveb` labelled `Hypothesis:`. **Promote it to `team/quality-bars.md` only if your #354 soak passes.** That is a `/name-the-bar` call and it is yours.

### 🔴 PR #352 — Drew: REQUEST_CHANGES (2026-07-30 ~00:2xZ). Priya dispatched on round 2.
Comment `5124773039`. **Devon's two original blockers ARE genuinely fixed** — Drew re-derived the attribution himself rather than reading her table: the ~49% midpoint + `+0.34235` shift belong to the retired **flint** `wpn_axe_01` (`MovementCameraScene.cs:129-130`, `WeaponPackAssetGen.cs:101-102`, `:1128`), and `031d43a --name-status` shows `D CastawayAxe.fbx` + `D …_License_CC-Attribution.txt` alongside `A wpn_axe_01.fbx`. Her gate-pair relabel also holds — he ran both greps and the result sets intersect in exactly one line, so neither half alone is sufficient.

**Two NEW blockers, both the same self-contradiction shape:**
1. **`team/uma-ux/gameplay-ui-direction.md:148`** — the §6.2 icon-recipe table still says *"the SHIPPED `CastawayAxe.fbx`"* and *"the SHIPPED atlas"*, **16 lines below this PR's own `:132` correction.**
2. **`weapon-tool-style-spec.md:34`** — she triaged it "live, correct"; refuted by this PR's own `item-icon-bake-recipe.md:60` and §4.1 (`:309` "NO lashing, NO rawhide, NO cord"), and `:34`'s citation resolves to `:148` — the line in blocker 1.

**The root cause is a grep-anchoring failure, and it is the interesting part.** Both of Priya's sweep patterns MISS `:148` because that line uses a **bare filename** (no path) and **capital `SHIPPED`**. A path-anchored, case-sensitive pattern is exactly why round 1 looked complete and wasn't. Her round-2 brief requires a case-insensitive unanchored re-sweep of `team/`, `.claude/`, and `docs/` — fixing **every** hit, not just the two he named. This is a fresh instance of the grep-anchoring rule already in `unity-conventions.md`.

**2 NITs folded in:** the construct-grep table enumerates 5 of 6 lines (`research:95` unlisted); the PR body cites `:139` for `HeldAxeGripShiftY = 0f` where the real line is `:138`.

**Reviewer stays Drew.** `CLAUDE.md` confirmed absent from her diff — the orchestrator's `:66` fix is separate and stays that way.

**Devon** — alive on `devon/86cav8y74-wood-capture-gaps` (branch cut off `fee2604`, dirty count 2→7 at 00:17Z). No PR yet. Verified from git, not probed.
**Uma** — still no ungated standalone work. The three tickets Priya un-gated (`86caxjwhh`, `86caxhk6v`, `86caxgyc4`) are all build/test lane and Devon holds the single build slot.

### ✅ PR #352 round 2 landed `588be01` — then round 3 dispatched to fix MY scope error (2026-07-30 ~00:4xZ)
Comment `5124878798`. **No blockers.** She did the re-sweep FIRST as briefed: case-insensitive, unanchored, 9 constructs, **67 raw hits counted from a clean `git archive`** (not her worktree), all 67 triaged in the PR body. Drew's root-cause call was right.

**Three in-class sites fixed — his two plus one he never named:**
- `gameplay-ui-direction.md:148` — both cells re-pointed to `wpn_axe_stone_01.fbx` + §4.1 hexes; the atlas named as banned.
- `weapon-tool-style-spec.md:34-35` — **she overturned her own round-1 "live, correct" verdict** and re-cited to Sponsor + DECISIONS 2026-06-14.
- `pre-soak-visual-audit.md` header + item 1 — was live present tense with no status marker; now dated + `CLOSED (PR #100)` with the prose kept verbatim.

Correctly left out of class: 12 `CastawayAxeSwing`/`CastawayMelee` clip names, 5 unrelated "placeholder" hits, and the DECISIONS/STATE historical records. Both NITs done.

### 🔴 MY ERROR — the scope boundary I set was blocking the fix from reaching anyone
I told her `CLAUDE.md` was project config and out of her scope; the orchestrator would own it. She complied and flagged it. **Then she verified the consequence and she is right:** `CLAUDE.md:66` is **still stale on `origin/main` @ `fee2604`** — I confirmed independently. My fix lives only on `orch/coordination`, a working branch that never merges (harvest is port-not-merge). So the correction reached **nobody**, while `CLAUDE.md` auto-loads into every session and every persona working off main keeps reading the wrong claim. **My scope rule produced a fix that cannot ship.**

Round 3 dispatched: fold `CLAUDE.md:66` into PR #352 (the sweep PR for this exact defect class), plus two dead-path references she had classed out-of-bounds in **main's** `.claude/docs/unity-conventions.md` — `:168` and `:173`, both citing the deleted `Assets/Art/Props/CastawayAxe/`. Instructed to **retire the EXAMPLE, not the rule** at `:173`: the CC-BY attribution obligation is still live because the castaway base is CC-BY and `86cay4hyz` exists for exactly that staleness. Then one final widened sweep including `CLAUDE.md` and `.claude/docs/`.

**Lesson worth keeping:** a doc-retirement sweep that excludes the always-loaded config leaves the most-read copy wrong, and an orchestrator "I'll own that file" carve-out is worthless if the orchestrator's branch never reaches main. Captured in `unity-conventions.md`.

**Orch churn protectively committed** at `77f7802` (CLAUDE.md correction + tonight's captures) so nothing is lost pending harvest.
**Devon** — alive: wrote `AxeVerifyCapture.cs` + `ChopVerifyCapture.cs` within the prior 20 min, dirty 7→8, no PR yet. From file mtimes, not a probe.

### 🔴 MY FOURTH WRONG PREMISE TONIGHT — and this one would have been a false LICENCE claim
Priya's round 3 landed `06596fa` (PR #352: `CLAUDE.md:66` + main's `unity-conventions.md`). She **refused part of my brief and was right.**

I asserted: *"the CC-BY attribution obligation is still LIVE: the castaway base is CC-BY."* **It is not CC-BY.** I read the real file to check her, at a path I had to find rather than guess (`Assets/Art/Character/Castaway/Castaway_Attribution.txt` — singular `Character`; my first guess used the plural and silently found nothing). Verbatim:

> *"This is GENERATED 3D content (Hyper3D Rodin, Creator-tier web export) animated with Mixamo clips (Adobe, free account). Retain this attribution in any distribution of the game (an in-game / about-screen credits entry covers it)."*

The obligation is real; the **mechanism** is generated-content + Mixamo clip terms, with **no Creative Commons licence anywhere**. Had she complied, a false licence claim would now sit in a MANDATORY-read doc. She named the risk in exactly those terms. **A licence statement is the one class of doc claim where a confident guess is worse than silence — and I guessed.**

**Her other claim also verified precisely:** `chibi|joaobalt` → **0 hits in asset/licence file paths** on `origin/main`, 25 in prose/code. So the chibi asset is genuinely discharged by the same test as the axe; only prose still discusses it. Her "zero hits" was correctly scoped to the asset.

**And the ticket she flagged as unverified is real:** `86cay4hyz` = *"Castaway_Attribution.txt is two hero-versions stale — it names v1 as LIVE and v2 as toggle-gated; v4 has been the live hero since 2026-07-19 [S, text-only]"*. Independently corroborates her `CharacterAssetGen.cs:217 UseCastawayV4Default = true` finding.

### Uma — finally has real work, but it is HARD-BLOCKED on #352 merging
The one in-class hit Priya deliberately left: `wpn_axe_01.fbx` at `blender-asset-pipeline.md:27`/`:220` — deleted in `1a55491` (#254) while the three-tier names arrived in `dd5dd11` (#304), so it is a **different retirement event** and the replacement naming convention is **Uma's call**, not a guess to make inside someone else's PR. Her judgement is correct.

**Why Uma cannot start yet:** `blender-asset-pipeline.md` is already in #352's diff (her round-1 edits). A parallel Uma branch touching the same file conflicts. This is a genuine hard dependency — the first time tonight Uma's idleness has a mechanical cause rather than an empty backlog. **Dispatch her the moment #352 merges.** A follow-up ticket for it goes in Priya's next board round.

### In flight
**Drew** — final #352 review pass @ `06596fa`, briefed to independently verify the licence mechanism, the third site, the overturned round-1 verdict, and to judge whether deferring the `wpn_axe_01` hit is correct.
**Devon** — alive on `86cav8y74`: wrote `WoodTierShippedGateTests.cs` (EditMode) + `HeldBeltWeaponVisualPlayModeTests.cs` (PlayMode) in the prior 15 min, dirty 11, no PR yet. Paired EditMode/PlayMode is the right shape per the testing bar. From file mtimes, not a probe.

### ✅ NEW PR #355 — Devon closed the wood capture gaps (2026-07-30 ~00:5xZ)
Head `8873f90`, `MERGEABLE`, **all four CI jobs pass** (run `30503644348`; `EditMode 1278/1278 inconclusive=0`, `PlayMode failed=0`). Self-Test Report `5125056059` + CI addendum `5125061768`. Ticket `86cav8y74`.

**He did the thing I asked for and it paid off — he stated the coverage BOUNDARY explicitly instead of implying full coverage.** Covered: the whole `SelectBelt → Inventory.Changed → SyncHeldVisualToSelection → WoodSelectionIndexFor → ApplyCurrent → HeldAxe.ShouldShow → pixels` chain, plus everything downstream of the click edge in `ChopTree.Update`. **NOT covered:** the raw `Input.GetMouseButton*` edge and the number-key→`SelectBelt` bind — *legacy Input Manager has no injection API, and he found zero captures repo-wide that drive real `Input`* — plus locomotion and look/proportion judgement. That last measurement is worth keeping: **no capture gate in this repo drives real input. Your soak is structurally the only real-input gate on every verb.**

### 🟡 ONE REVIEWER DECISION PENDING (Drew's, not mine to make)
He shipped the gate **HEADLESS**, contradicting AC4's "windowed" — flagged openly in the PR body under §DELIBERATE DEVIATION rather than silently overridden, with a two-line revert available. Drew decides as the ticket's named reviewer, after he finishes #352.

**Why this is bigger than one AC:** Erik's research established that `-nographics` precludes ALL RenderTexture rendering while `-batchmode` alone keeps a real graphics device — and the windowed launches exist *only* because backbuffer capture goes black. **If Devon's headless gate genuinely works, that is live evidence for `86cag93zb`** — the ticket about removing the 1-runner capture pin that serializes the whole team behind a single build slot. Tess has been asked to say whether it counts.

### 🔴 A GUARD WE BELIEVE IN MAY BE VACUOUS
Devon's side-finding: committed `WeaponSetLineup.prefab` on `main` still has **10 of 15 nodes** (no wood — `86catwzhy`), so `CommittedLineupDriftGuardTests` is **vacuous in CI**, because CI re-bakes before EditMode runs. He A/B-proved it: 10 nodes → `failed=1`; post-bootstrap 15 nodes → `18/18`. Same family as the known committed-procedural-assets-go-stale trap. Tess is verifying independently. **If it holds, a drift guard we rely on is protecting nothing** — that outlives this PR.

**Also:** his cleanup command was denied by the destructive-bash hook and **he left the scratch artifacts in place rather than routing around the block.** Correct call, worth noting as the behaviour we want.

**Doc-worthy (queued for capture):** the launch-mode invariant greps the *whole* gate script for the windowed-flag literal — so merely *mentioning* that flag in a headless gate's comment reds the check.

**Dispatched:** Tess → QA on #355. She is the right reviewer because **these were HER gaps** (from her own PR #327 comments `5025894753` / `5031539815`), and she has been told to judge the diff against what she actually asked for rather than the ticket's paraphrase.

### ✅ PR #352 — Drew: APPROVE_WITH_NITS @ `06596fa`, blockers cleared (2026-07-30 ~01:0xZ)
Comment `5125072153`. Merge-base = `fee2604` = current `origin/main`, so no drift. `unity-conventions.md` carries only her two round-3 hunks. The CC-BY **rule** at `:175` survived clause-by-clause — only the examples moved into the `:176`-`:178` ledger, and `:179` strengthens it. `CLAUDE.md:66` and `blender-asset-pipeline.md:17` now agree. `.claude/agents/tess.md` correctly absent (11 files).

**My licence error is now confirmed wrong by BOTH of them, independently.** Drew: `Castaway_Attribution.txt` never says CC-BY; **zero `*_License*` files anywhere in the tree**; `031d43a` shows `D Assets/Art/Props/CastawayAxe/CastawayAxe_License_CC-Attribution.txt`. Priya's "zero hits" was path-scoped **by construction** (`ls-tree` emits paths) — he reproduced it at exit 1, and the 8 remaining `chibi` prose hits are covered by `:177`'s historical-notes disclaimer. **Refusing my instruction was correct.**

**He closed my open question too:** the `wpn_axe_01` deferral is right — but on **scope**, not ownership: `:27`/`:220` are filename *examples*, never "a retired asset ships" claims, so they were never this sweep's class. And he sharpened the ownership split I had accepted too loosely: the shipped **names** are observable from the repo, hence mechanical; only *"is the tier token mandatory"* is genuinely Uma's. The follow-up ticket now carries that split.

**Orch action he raised — CLOSED.** He asked for `86cay4hyz` to be verified at `unity-conventions.md:178` ("unverified by both of us"). I had already verified it: the ticket is real, *"Castaway_Attribution.txt is two hero-versions stale … v4 has been the live hero since 2026-07-19"*. Citation stands.

**Final NIT round dispatched.** Two of his three NITs applied; the third (`ui-toolkit-panels-ux-spec.md:179`) is out of scope and goes to the follow-up. ⚠ **I am treating `item-icon-bake-recipe.md:120` as effectively blocking despite his NIT rating** — staging a staleness-purge PR that still contains a stale line is precisely the self-contradiction this PR spent three rounds removing. Same call I made on PR #350.

**#352 will be STAGEABLE the moment that lands:** peer APPROVE ✅ · no `sponsor-gate`/`needs-soak` ✅ · no CI by design (docs-only `paths-ignore`, same basis on which #348/#349 were staged) ✅. **Uma unblocks the moment #352 merges** — `blender-asset-pipeline.md` leaves #352's diff and her naming-convention question can start.

### ✅ PR #352 NIT round landed `b11cfcf` — awaiting a confirm-only APPROVE at the new SHA
Both of Drew's in-scope NITs applied (`unity-conventions.md:175` EXAMPLES plural + the second example named; `item-icon-bake-recipe.md:120` re-anchored to `wpn_axe_stone_01.fbx` from that file's own §2.1). NIT 3 correctly left out of the PR.

**Why one more Drew pass instead of staging now:** his APPROVE read `06596fa`; the head is `b11cfcf`. **An APPROVE covers only the SHA it read**, so staging on the older one would be a false gate. Dispatched a short delta-only check, with one real question in it: she added a **leave-alone note for `item-icon-bake-recipe.md:112`** (protecting a correct historical DECISIONS citation from a future sweep) that he did not ask for — his call whether it stays.

### 🆕 Ticket `86caynyq7` — VERIFIED REAL, and the best-authored ticket of the session
*"dead flint `wpn_*_01` filename examples + one stale shipped slate/steel axe claim — mechanical rename pass + 1 scoped Uma convention call [S, text-only]"*, `to do`, priority normal. Every claim carries its own verification command and SHA. Structure: **Part A mechanical** (A1/A2 the four dead flint filenames at `blender-asset-pipeline.md:27`/`:220`; A3 `ui-toolkit-panels-ux-spec.md:179`; **A4 an explicit leave-alone guard** for `prop_crate_wood_01`/`env_rock_03`, which read as dead but are forward-looking placeholders for classes that never shipped) and **Part B exactly one yes/no for Uma** — is the tier token mandatory in the naming convention — with an AC that **A does not wait on B**.

### 🔴 A FINDING THAT LIMITS WHAT I WROTE IN THE DOCS TONIGHT
The ticket records that **all nine grep constructs in #352's sweep missed A3**, because `shipped[ _-]*(axe|atlas|hatchet)` cannot match *"shipped slate/steel FBX"* — the line names the retired asset by **descriptor**, using none of its names. Drew rules this a **confirmation** of the honest-residual claim, not a contradiction, and his conclusion is the durable part: **"No grep closes this class; only reading the file does."**

That materially qualifies the rule I captured earlier tonight (case-insensitive + unanchored). Case-insensitivity and dropping the path anchor fix the *syntactic* misses; they do nothing for a **semantic** miss where the prose describes the thing without naming it. **A staleness sweep's grep bounds the mechanical pass; it can never bound the class — the honest PR body says which residual class remains unswept.** Queued for capture.

### Sponsor note — #352 is a small merge that UNBLOCKS things
Merging #352 releases `blender-asset-pipeline.md` from its diff, which unblocks **Uma** (idle all session for want of ungated work) **and** Part A of `86caynyq7`. It is docs-only, peer-approved, no CI by design. Worth doing early in your queue rather than last.

---

## ⭐ 2026-07-30 ~01:10Z — PR #352 IS NOW STAGED. Five one-click merges waiting.

### ✅ #352 gate evidence (all machine gates met)
```
gh pr merge 352 --admin --squash --delete-branch
```
- **Peer APPROVE at the EXACT head SHA:** Drew, `APPROVE` at `b11cfcf003c0322fdff2187f025941ac70dbefee`, comment `5125147126`. He measured the delta rather than assuming it: `compare/06596fa...b11cfcf` = **ahead 1, behind 0, 2 files, +1/−1 each**. This mattered — his earlier `APPROVE_WITH_NITS` read `06596fa`, and an APPROVE covers only the SHA it read, so I sent him back rather than stage on a stale approval.
- **Verified now:** head `b11cfcf`, `MERGEABLE`, `OPEN`, **no `sponsor-gate` / `needs-soak` labels**.
- **0 CI checks — expected, not missing:** docs-only, and `ci.yml`'s `paths-ignore` covers `**/*.md` + `.claude/**`. Same basis on which #348 and #349 were staged.
- **Four review rounds:** Devon's 2 original blockers → Drew's 2 more → my scope reversal (`CLAUDE.md`) → NITs. Every fact re-derived from `origin/main` by the author, then independently re-derived by the reviewer.
- On merge, flip `86cay47zh` → `complete`.

### 📋 ALL FIVE ONE-CLICK MERGES — run in this order
```
gh pr merge 352 --admin --squash --delete-branch
gh pr merge 348 --admin --squash --delete-branch
gh pr merge 349 --admin --squash --delete-branch
gh pr merge 350 --admin --squash --delete-branch
gh pr merge 353 --admin --squash --delete-branch
```
**#352 first, deliberately** — it is the one that unblocks other work (see below). The rest are order-independent; run them one at a time rather than pasting all five, since batching merge-gate label operations has raced before.

### 🔓 WHY #352 FIRST — it is the only merge tonight that unblocks people
Merging it releases `blender-asset-pipeline.md` from its diff, which immediately unblocks:
- **Uma** — idle the entire session for want of ungated work. Her naming-convention question (`86caynyq7` Part B) can start.
- **`86caynyq7` Part A** — the mechanical dead-filename rename pass, which is currently conflict-blocked on the same file.

### Still yours, unchanged
- **#354** — the two-hand grip soak. `Build\soak-twohand-3\FarHorizon.exe`, stamp `zoned | 2026-07-29T23:03:52Z | 0a4af5e`. Left hand now genuinely touches the haft (28.2 → 10.6 cm against a 13.0 cm derived bound). ⚠ ~47% of frames remain out of reach by seat geometry, and the ~0.25 s ease-in is untested by any assert.
- **#351** — find-in-world. Devon APPROVE_WITH_NITS + Tess PASS_WITH_NITS, but **touches `ci.yml` so only a browser merge works**, and AC7 is soak-gated. Your soak is its ONLY real-input gate.
- **#355** — Devon's wood capture gaps, CI 4/4 green, **in Tess's QA now**. Carries one open reviewer decision: he shipped the gate HEADLESS against AC4's "windowed", flagged openly. That decision may bear on `86cag93zb` (removing the 1-runner capture pin).
- The orch-branch cleanup (needs a hard-denied force-push), the icon contact-sheet picks (`86camyvwn`), the branch-protection click.
- **A `/name-the-bar` call:** promote #354's *"one haft passing through both hands"* to `team/quality-bars.md` only if the soak passes (recorded as `Hypothesis:` on `86caynveb`).

**Tess** — no #355 verdict yet at 01:09Z, ~14 min in; worktree clean **by design** (read-only review), so there is no git tell for a reviewer. Inside the expected window; not probed, because a truncated QA pass reads like a verdict.

### ✅ PR #355 — Tess: PASS_WITH_NITS (2026-07-30 ~01:1xZ). Comment `5125171680`.
**Her two original gaps are closed — and gap 1 closed HARDER than she asked.** She had offered `ShowWeaponForCaptureDebug` as the mechanism; **Devon refused her own suggestion for judged states** (using it only for the baseline, `AxeVerifyCapture.cs:395`) and drove the real `SelectBelt` seam instead, then added true **mesh identity** (`:464` — versus the pickaxe sibling's weaker vertex-count proxy at `:286`), a pre-grant negative control (`:388`), and a crossed-state return (`:497`). Gap 2: `woodAxeSelected` is a genuine `pass` term (`ChopVerifyCapture.cs:242`), not decoration. An author declining a reviewer's suggested implementation because it was weaker is the behaviour we want.

### 🔴 THREE UNCOVERED LAYERS DEVON DID NOT NAME — this qualifies the praise I gave his boundary statement
I credited him for stating the coverage boundary honestly. It was honest but **incomplete**, and one of the gaps is serious:
1. **The chain ends at `Renderer.enabled`, NOT pixels.** `frame_check.py` floors are luma 6.0 / var 8.0, and `held_wood_empty` = **75.2 / 263.7** vs `held_wood_axe` = **75.3 / 258.4** — **the control and the positive case are indistinguishable to the only pixel check that runs.** The PR body's "→ pixels" is unsupported.
2. **The strongest finding, and it ties tonight's two threads together:** the weapon is evidenced only **AT REST** — no `TriggerChop` — so the order-50/65/100 seat chain is never exercised. **`86cay4282` round 4 proved a seat can be ~20 cm wrong while renderer + mesh identity are both green.** So this gate structurally cannot exclude the exact defect class Drew just spent four rounds fixing.
3. Recipe→item-id layer (`InventoryModel.cs:340`) unnamed — and **soak-3, the escape this ticket exists to prevent, started at "I craft."**

### ⚠ CORRECTION — I overstated the CI-vacuity alarm, and I have corrected the doc
I logged it as "a guard we believe in may be protecting nothing." Tess: the **mechanism holds** (main `fee2604` = 10 nodes/0 wood; bootstrap `ci.yml:279` precedes EditMode `:290`) but **no false belief existed** — the guard's own docstring already declares that scope verbatim. The alarm framing was mine, not the evidence's. **The real residue is narrower and more useful: `86catwzhy` has NO CI guard at all**, fixable with a `git show` read in the hosted `structure` job (which doesn't bootstrap).
She also caught a **circularity in my documentation**: `unity-conventions.md:421-422` were written *from this PR's own report*, so citing them back at the PR would manufacture a second source for a single observation. Both the scope correction and an explicit circularity warning are now in the doc.

### ⚠ CORRECTION — I was too optimistic about `86cag93zb`
I wrote that a working headless gate would be "live evidence" for removing the 1-runner capture pin. Tess's measured read: **supporting, NOT unblocking — 8 gates remain structurally windowed.** Headless does answer the ScreenCapture half decisively (`device=Direct3D12`; she eyeballed **11/11 real frames** from artifact `8744596538`), but it does not answer "live Animator", because the gate drives no Animator at all — **the gap is the missing swing, not the window.**

**Dispatched Drew** for the #355 peer review plus the AC4 headless-vs-windowed ruling, which is his alone. Briefed with Tess's three layers to verify (not to re-derive) and asked to rate each blocking vs NIT himself — I am not pre-judging it.

**Board hygiene owed (mine):** `86cav8y74` still reads `to do` despite an open PR with QA passed — flip to `in review` next tick; the orchestrator owns status.
**Liveness note:** the probe I sent Tess did NOT truncate her — she completed a full 45-tool-use pass. Framing it explicitly as "not a stop signal, do not post a partial verdict" appears to have worked.

---

## ⭐ 2026-07-30 ~01:30Z — PR #355 STAGED. Six one-click merges now waiting.

### ✅ #355 gate evidence — every machine gate met
```
gh pr merge 355 --admin --squash --delete-branch
```
- **CI:** all FOUR jobs SUCCESS at head `8873f90` (run `30503644348`), including the advisory PlayMode lane. Verified now: `MERGEABLE`, `OPEN`, **no labels** — so no `sponsor-gate`, no `needs-soak`.
- **Self-Test Report:** comment `5125056059` + CI addendum `5125061768` — Tess verified it **accurate**, every value re-derived from the run.
- **QA:** Tess `PASS_WITH_NITS`, comment `5125171680`. Both of her original #327 gaps closed — gap 1 closed *harder* than she asked.
- **Peer review:** Drew `APPROVE_WITH_NITS`, comment `5125266402`.
- Not soak-gated: a test/capture ticket with no visual or feel judgement in it.
- On merge, flip `86cav8y74` → `complete`.

### ⚖️ AC4 headless-vs-windowed — DECIDED by Drew (reviewer), accepted, no revert
Devon deviated from AC4's "windowed" and shipped headless, flagging it openly. Drew accepted it, and the reasoning is the strongest of the session — all three of AC4's grounds fail **on this gate**, verified from source:
1. `grep "ScreenCapture\|WaitForEndOfFrame" AxeVerifyCapture.cs` → **0 hits**; it uses the pre-existing `CaptureHeldFrame` → **`SubmitRenderRequest`** (`:537`) — which is *exactly* the render path Erik's research prescribed hours earlier, independently arrived at.
2. `grep "Trigger\|Animator\|Swing\|SetBool\|Play("` → **1 hit, and it is a comment.** So AC4's "live Animator" ground is both mis-derived **and vacuous** — the gate drives no Animator at all.
3. **Decisive:** `verify_chop_gate.sh` and `verify_heldbelt_gate.sh` were **already headless on `main`**, so obeying AC4 would have **regressed already-merged `86cag93zb` work.**

What AC4 actually protected — false-empty frames — is now covered by `device=Direct3D12` (artifact `8744596538`, which he read himself), `frame_check` 11/11 at luma 73.1–75.4 against a floor of 6.0, and `test_gate_scripts.sh:863-873`. **What AC4 reached for — a judged live Animator — is genuinely unprotected, and never was the window: it is the missing swing.** That is the honest residual.

### Tess's three layers — Drew rates all NIT, none blocking, with reasons
- **(a) Confirmed exact**, and worse than she framed it: control variance is *higher* than the positive case (`75.2/263.7` vs `75.3/258.4`). Claim-accuracy issue only; already propagated into `unity-conventions.md`.
- **(b) The strongest — needs a TICKET, not a block.** His own round 4 recorded a **28.2 cm** worst seat error passing under a **36.6 cm** cap that printed `PASS`. He rules it a *different bug class* from soak-3 and therefore out of this ticket's scope. **A follow-up is owed: a gate that exercises the seat chain during the action, not just the prop at rest.**
- **(c) Thinner than graded** — `CraftingSeamTests.cs:53-54` already covers the recipe→item-id layer.
- **Her CI-vacuity correction HOLDS:** `CommittedLineupDriftGuardTests.cs:27-34` on `origin/main` declares that scope verbatim. My "guard protecting nothing" alarm was wrong; the doc is corrected.

### 📋 ALL SIX ONE-CLICK MERGES — #352 first, then any order, one at a time
```
gh pr merge 352 --admin --squash --delete-branch   # do this one FIRST — it unblocks Uma + 86caynyq7 Part A
gh pr merge 355 --admin --squash --delete-branch
gh pr merge 348 --admin --squash --delete-branch
gh pr merge 349 --admin --squash --delete-branch
gh pr merge 350 --admin --squash --delete-branch
gh pr merge 353 --admin --squash --delete-branch
```
Run them individually rather than pasting the block — batching merge-gate label operations has raced before.

### Still genuinely yours
- **#354 soak** — `Build\soak-twohand-3\FarHorizon.exe`, stamp `zoned | 2026-07-29T23:03:52Z | 0a4af5e`. ⚠ ~47% of frames still out of reach by seat geometry; the ~0.25 s ease-in is untested by any assert.
- **#351 soak + browser merge** (touches `ci.yml`; your soak is its ONLY real-input gate — no capture gate in this repo drives real input).
- Orch-branch force-push · icon contact-sheet picks (`86camyvwn`) · branch-protection click.
- **`/name-the-bar`:** promote #354's *"one haft passing through both hands"* only if the soak passes (`86caynveb`, recorded as `Hypothesis:`).

### ⚠ CORRECTION to my #355 AC4 summary — I read the actual AC text after logging, and it is more specific than either report conveyed
`86cav8y74` status flipped `to do` → `ready for qa test` (verified from the API response). Reading the body that came back, **AC4 says more than "run it windowed"**, verbatim:

> *"🔒 Constraint — windowed capture. Run the new/extended gate windowed (NOT batchmode) per the `verify_*_gate.sh` convention. WHY: ScreenCapture + a live Animator need a real swapchain; headless lies about live runtime (unity-conventions §Headless — **RT-readback works for pure world-camera gates, but a held-mesh + live Animator judge stays windowed**)."*

So AC4 already contained the carve-out. Its exclusion is a **conjunction** — *held-mesh **+** live Animator* — and this gate is unambiguously a held-mesh judge while provably not an Animator judge. Drew read the conjunction as unsatisfied and accepted headless. **That is a defensible reading, not an obvious one**, and I presented his ruling as cleaner than the AC text supports. Recording it so a future reader can disagree with us.

**His third ground is the one that actually carries the decision, and it is stronger than the Animator argument:** AC4's stated WHY appeals to "the `verify_*_gate.sh` convention" — but `verify_chop_gate.sh` and `verify_heldbelt_gate.sh` were **already headless on `main`**. **The AC cites a convention the repo no longer follows.** Obeying it would have regressed merged `86cag93zb` work. A stale premise inside an AC is a stronger reason to deviate than a vacuous one.

**Second, UNFLAGGED deviation — both reviewers accepted it, nobody labelled it.** AC4's sibling constraint reads *"reuse the existing capture hook … Use `HeldWeaponCycleDebug.ShowWeaponForCaptureDebug(index)` … do NOT add a new mesh path."* Devon **declined it for judged states** and drove the real `SelectBelt` seam instead (using the hook only for the baseline). Tess — who originally *offered* that hook — approved of the refusal, saying gap 1 closed harder than she asked. So the change is better than the AC asked for. But **Devon's PR body flagged only the headless deviation, not this one**, and two reviewers passed over it without naming it as a deviation. No harm here; the pattern is the note: an AC deviation that IMPROVES on the AC still needs flagging, or the ticket's record silently diverges from what shipped.

**Doc follow-up:** `unity-conventions.md` §Headless is cited *by this ticket* as the source of the windowed rule for held-mesh gates. Devon's `SubmitRenderRequest` gate plus Erik's research now partly supersede that. The doc should say RT-readback/`SubmitRenderRequest` covers held-mesh gates too, and that only a **judged live Animator** still needs a window — otherwise the next ticket will inherit the same stale constraint. Queued for capture.

### 🔧 Erik died mid-task and was SALVAGED by resume, not re-dispatch (2026-07-30 ~01:4xZ)
His agent terminated on `API Error: Connection closed mid-response` — a transport failure, not a task failure, and distinct from the Opus 529 storm earlier (he is `model: sonnet`, which stayed healthy all night).

**The tell that made salvage worth trying:** his final output line was *"Now I have sufficient evidence. Let me write the research note."* — so he had finished the expensive part (research) and died before the cheap part (writing). Verified `team/erik-consult/what-still-needs-a-window.md` does **not** exist, confirming nothing reached disk.

**Resumed via `SendMessage` to the agentId instead of dispatching fresh** → `"was stopped (failed); resumed it in the background with your message"`. His transcript context is intact, so the gathered evidence survives. A fresh dispatch would have discarded ~40 tool-uses of research and re-run every fetch.

**Reusable lesson (worth promoting on `/save-session`):** a `status: failed` agent is not necessarily lost work. **Read its final output line before deciding** — it reveals how far it got. If it died *after* the expensive phase, resume by agentId with an explicit "your connection dropped, do not re-run the research, resume from exactly there" message. This matters most for **write-only agents with no worktree** (Erik): there is nothing on disk to salvage the usual way, so the transcript IS the only copy, and resume is the only recovery.

**Also in flight:** Devon on `86cay4hyz` (attribution file, build slot), Priya on three follow-up tickets + a reconcile-report.

### ✅ Erik delivered after resume — and I tested his free-answer proposal. It FAILS. (2026-07-30 ~01:5xZ)
Note committed at `ac58d4b`: `team/erik-consult/what-still-needs-a-window.md`.

**(a) "Live Animator ⇒ must be windowed" is VERY LIKELY A MYTH.** Animator state-machine/clip time is CPU-side and already ticks under `-batchmode` (proven by the repo's own `WaitForSeconds` swing tests). The single sourced gap: default **`AnimatorCullingMode.CullUpdateTransforms` gates bone-transform WRITES on `Renderer.isVisible`**, and no source says whether `SubmitRenderRequest` — which runs *outside* the render loop — updates that flag in time. Mitigation regardless of the answer: force **`AlwaysAnimate`**.

**⚠ His cheapest-resolution idea does NOT work — I checked it myself rather than banking it.** He proposed: if the already-headless-and-green `verify_chop_gate.sh` captures mid-swing, (a) closes for free. It does not. `ChopVerifyCapture` captures **before/after STATE**: `chop_before.png` at spawn with no wood, then it *"waits for the chop to yield wood — then captures"* `chop_after.png`. **No frame is rendered during the swing**, so the gate cannot speak to mid-clip bone writes. **(a) remains OPEN and needs the purpose-built experiment, not an inference from an existing gate.** Good instinct, wrong gate — and worth catching before it became a doc claim.

**(b) The genuine residual is UI-Toolkit Screen-Space-Overlay** — but `PanelSettings.targetTexture` (official, documented for Unity 6000.4) is an **untried escape hatch** that contradicts the repo's current "hard boundary" framing. OS-cursor/focus reads stay windowed but are near-moot (no gate judges them as content). Post-processing/Volumes: no obstacle found, Likely-fine but untested.

**(c) He REFUSED to fabricate a precise count** of how many of the 8 gates could convert, having no repo access — bounding it instead at "≥3 of 8 are UI-overlay-inclusive, so not auto-convertible" and supplying a 4-question per-gate checklist. That refusal is the right call and exactly the discipline the brief asked for.

### 🔴 THE CAUSAL CHAIN FOR HOW AC4 GOT OVER-CONSTRAINED — now fully visible
`verify_chop_gate.sh`'s own **comment block contradicts its own code**. Lines 8-29 still read *"This launches the BUILT exe **WINDOWED**"* and *"Windowed (NOT `-batchmode` — ScreenCapture needs a real swapchain, spike iter-4 / **unity-conventions.md**)"* — while **line 70 actually runs `-batchmode` headless RT-readback** (`# HEADLESS (86cag93zb): -batchmode, NO -nographics (real D3D12 device), NO window`).

So the chain is: a doc rule → a script comment citing that doc → an AC citing the same rule → a ticket over-constrained for weeks. **The AC author read the comment, not the code.** Two follow-ups owed: (1) fix the stale comment block in `verify_chop_gate.sh` — a gate whose prose contradicts its own launch line will keep misinforming readers; (2) file the **mid-swing headless experiment** that actually settles (a), with `AlwaysAnimate` as the mitigation to test alongside.

**Independent corroboration found while checking:** `ChopVerifyCapture`'s own docstring (`:20-21`) states *"the exe can't inject a real mouse button into this scripted capture"* — confirming, from a second source, the repo-wide no-capture-gate-drives-real-input invariant. Your soak really is the only real-input gate.

**In flight:** Devon on `86cay4hyz` (branch cut, attribution file edited, verified from git); Priya on three follow-up tickets (probe-confirmed alive).

### ✅ Priya: 3 tickets filed + a REAL staging hazard caught (2026-07-30 ~02:0xZ)
- **`86cayp0ay`** — swing-time held-weapon seat gate (`test`, M, Unity-build). Drew's owed follow-up.
- **`86cayp0p9`** — committed-lineup drift guard via `git show` in the hosted `structure` job (`test`+`asset-hygiene`, S).
- **`86cayp0re`** — `unity-conventions.md` §Headless de-over-constrain (S, docs; blocked on #352).

### 🔴 REAL HAZARD SHE CAUGHT — my own staging rule could have mis-merged #351
`86cah7y5b` (PR #351) sits at `ready for qa test` with **zero tags**, while its AC7 is Predict-Before-Soak. My away-staging rule promotes a PR when **all machine gates are green AND no `sponsor-gate`/`needs-soak` marker exists** — and #351 has CI green + Devon APPROVE + Tess PASS. **A tick reading only status+tags+checks would have staged it.** This tick didn't, but only because I carried the context in-session, which is exactly what does not survive a session boundary. **A tag is the only form of that gate that survives; prose is not.** Posted as comment `90150246202769` on the ticket, with the reason tags could not be written.

### ⚠ ENVIRONMENT BLOCKER — tag writes are unavailable right now
`update_task` has **no `tags` parameter**, and the REST fallback needs the ClickUp token from `.claude.json` — **the classifier blocked her token read.** So neither of us can attach tags this session. Consequence: `86cayp0re`'s `deferred` shelf-state lives only in its body ⛔ block, and the untagged gates on `86cah7y5b` stay untagged. **Needs your hand or a permission change** — and until then the tag-based away-staging gate has a hole in it, which is worth knowing before you rely on it.

### ⚠ HER #354 CLAIM IS WRONG — and the ambiguity was mine
She reported *"Drew's #355 review states his round-4 IK work is uncommitted — the PR does not carry the fix."* **Refuted by direct check:** `cd6fec1` contains `CastawayLeftArmHaftIk.cs`, `TwoBoneIkSolver.cs`, `TwoHandGripRead.cs` (+ metas), the derived cap is at `TwoHandGripRead.cs:110` **in the commit**, and **no IK file appears in the uncommitted churn**. PR #354 fully carries the fix; the soak build is backed by the PR.

**Root cause is my phrasing.** She read Drew's *"Worktree untouched: 54 dirty"* as the IK being uncommitted. I have written "~56 DELIBERATE dirty entries" into every brief tonight without once saying what that churn IS or stating that the ticket's own work is committed. **Fix going forward: never cite a dirty count without saying (a) what it consists of and (b) that the ticket's deliverable is committed.** A bare number invites exactly this inference.

### 💡 ADOPTING HER PROCESS SUGGESTION — solves the board-only liveness gap
A ticket-authoring round is invisible to both `git` and `gh`, so the liveness gate can only be satisfied by interrupting the agent (I had to probe her tonight). Her fix: **brief board-only agents to post a one-line "started, reading X" comment on the source ticket in their first tool round** — visible to `get_task_comments` immediately, costs one call, needs no push. Adopting this for every future ClickUp-only dispatch.

**Other drift she flagged, not fixed:** PRs **#348/#349 carry no ticket id** (hygiene gap). `86catwzhy` confirmed `complete` (`date_closed` 1784476523029). `86caxj8zw`/`86caxjwev` correctly `in review`. **Sequencing warning:** `86cayp0p9`, `86cayp0re` and PR #350 all touch `unity-conventions.md` / `test_gate_scripts.sh` — do not run them in parallel.

**No new dispatch available this tick:** all three new tickets are blocked — `86cayp0ay` (Unity-build) behind Devon's slot, `86cayp0p9` touches `.github/` so it is also build-lane, `86cayp0re` blocked on #352 merging. Tess (no PR awaiting QA), Uma (#352 file conflict), Drew (both worktrees hold uncommitted state), Erik (delivered) all idle with mechanical reasons.

### ✅ NEW PR #356 — Devon: attribution file refreshed to the live v4 hero. PASS. (2026-07-30 ~01:5xZ)
Head `538dcfa`, +115/−13, one `.txt`. **All four CI jobs pass**, `mergeStateStatus=CLEAN`. `86cay4hyz` flipped `to do` → `in review`. Awaiting Drew's review (dispatched).

**What the file actually claimed vs reality:** it named **v1 LIVE** and **v2 as toggle-gated awaiting soak** (*"then v2 is promoted to the default + v1 is removed"*), with **no v3 or v4 section at all**. Reality at `fee2604`: `UseCastawayV4Default=true` (`:217`), `V3=true` (`:164`), `V2=true` (`:130`), and `FbxPath` (`:228-231`) resolves **highest-first** → **v4 LIVE, v3 ROLLBACK**. Nice subtlety he handled: because all three consts are `true`, a line saying "v2 is off" would contradict `:130` — so each status line states the const's value **and** its losing ladder position.

**The licence trap — handled exactly right.** No CC-BY existed and none was introduced. He verified zero `*_License*` files tracked; the only CC-BY text under `Assets/` is `.cs` comments calling the axe + chibi **retired**; `CastawayAxe/` untracked. The retain sentence is preserved verbatim. He **added an explicit "NOT a Creative Commons obligation" negative**, and marked the licence text behind the retain instruction as **`OPEN QUESTION (unverified)`** because no repo source records it. That is the correct output where no source exists — this is the file a distribution reviewer would read, and it now says what is known and labels what is not.

**v4 provenance DERIVED, not pattern-copied** (the ticket's AC2 required exactly this): mesh + palette **in-house** (Blender/Blender-MCP), rig + clips **third-party** (Mixamo). The ticket's own "hand-model vs Mixamo `Idle.fbx` don't agree" tension resolves — `:204`'s FBX is what Mixamo **returned** after rigging our own export (`character-pipeline.md:50`).

**Both of his flags cross-check against the ticket's own text:**
- **The uncommitted v4 provenance artifacts are a CONFIRMED, pre-documented finding, not a new one.** Priya's AC3 already recorded it, verified at `fee2604` — `art-src/castaway-v4-export` returns nothing while v2's and v3's equivalents are committed — with an explicit *"flag, do not fix."* Devon extended the list: **`castaway-v4-README.md`, `castaway_v4.blend`, `castaway_v4_palette.png` are also untracked**, and v2/v3 both ship READMEs. **The shipped hero's source of truth lives on one machine.** Priya is filing the follow-through ticket (this is what AC3 deferred, so it is not a dupe); Drew is verifying independently, and I told them to say so if they disagree rather than reconcile silently.
- **`CharacterAssetGen.cs:51-52` says *"License is CC-style generated content"*** — vague and now wrong-hero. **That exact vagueness is what made my false CC-BY claim feel safe tonight.** Code lane, correctly not fixed here; ticket being filed.

**The drift guard was correctly NOT added** — `86cay4k73` AC2 owns it, which Devon confirmed by reading it and Priya's own OOS section states verbatim. My brief had floated adding one; he was right to decline.

### 🔒 THE QUEUE IS NOW GATED ON YOUR MERGES
Every remaining ticket is blocked behind a staged PR: `86cayp0ay` (swing-seat gate) overlaps #354's files; `86cayp0p9` overlaps staged PR #350 (`test_gate_scripts.sh`); `86cayp0re` is blocked on #352; Uma is blocked on #352. **Six staged merges — #352 first — are the thing that unblocks the next wave.** Not urgent-in-the-night, but that is the shape of the morning.

---

## ⭐ 2026-07-30 ~02:1xZ — PR #356 STAGED (seventh). Plus a real data-loss exposure you should know about.

### 🔴 THE LIVE HERO CAN SHIP BUT CANNOT BE FIXED — v4's Blender source is untracked
Precisely verified on `origin/main` (my first grep was too broad and I redid it — `castaway.?v4` also matches the *shipped* assets):
- `art-src/` **tracks** v1's `castaway_character.blend`, v3's `castaway_v3_lowpoly.blend`, v2's `castaway-rodin-export/`, v3's `castaway-v3-rodin-export-lowpoly/`, plus the weapon sources.
- For **v4 — the LIVE hero — `art-src/` contains NOTHING.** No export dir, no README, no `.blend`, no palette source. `git ls-tree … | grep -iE "^art-src/.*v4"` → empty. Tracked `.blend` files are v1's, v3's and two weapon ones; **there is no v4 `.blend` anywhere in the repo.**
- The **shipped** `Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx` + palette **are** tracked, so builds are fine. **The runtime asset exists; the editable source does not.**

**Why that is worse than an untidy repo — Drew's point, and it is the sharp one:** per `character-pipeline.md:51`, the deferred right-hand/thumb defect (`86cau4za2`) is fixable **only via a Mixamo re-rig** — both the re-export and binary-edit routes were investigated and refuted. A re-rig needs the `.blend`. **So the live hero can ship, but cannot be repaired, if that machine is lost.** The v1/v2/v3 asymmetry (all three have committed sources) is also good evidence the omission was accidental rather than a decision.
**Priya is filing the ticket; Drew independently confirmed the same four paths.** This one is worth your attention beyond the ticket — it is a one-disk-failure risk on the character the whole game ships with.

### ✅ #356 gate evidence
```
gh pr merge 356 --admin --squash --delete-branch
```
- **CI:** all four jobs SUCCESS at `538dcfa`; `MERGEABLE`, **no labels** (not soak-gated).
- **Self-Test Report:** posted `2026-07-30T01:48:16Z` — verified present, not assumed.
- **Peer review:** Drew `APPROVE_WITH_NITS`, comment `5125526267`, posted `02:07:08Z`. He verified **all five licence statements adversarially** and — the part that matters — **tried to falsify Devon's `unverified` label and could not**: the only EULA/terms text anywhere in the tree concerns *Unity's own seat licence* in an Erik research doc, so the Rodin/Mixamo terms genuinely are unrecorded. He also traced both retired assets to real deletion commits (`031d43a` #100 axe, `6aada8f` #50 chibi) and found all 11 CC-BY hits in the tree are comments describing *retirement*.
- **⚠ Tess QA was NOT run, and I am staging anyway — my reasoning, so you can overrule it.** My own staging rule lists a Tess QA PASS. Here the change is one text file, and the peer review was adversarial rather than confirmatory (it attempted falsification and re-derived every claim, incl. the ladder consts and a test-enforced v4 split at `CastawayCharacterTests.cs:703`). Per the project's precedent that a peer absorbs the QA checklist when QA is not separately warranted, I judged a second pass would re-derive rather than add. **If you would rather every `Assets/**` PR carry a QA pass regardless, say so and I will stop absorbing it.**
- On merge, flip `86cay4hyz` → `complete`.

### 📋 SEVEN ONE-CLICK MERGES — #352 first, then any order, one at a time
```
gh pr merge 352 --admin --squash --delete-branch   # FIRST — unblocks Uma + 86caynyq7 Part A
gh pr merge 356 --admin --squash --delete-branch
gh pr merge 355 --admin --squash --delete-branch
gh pr merge 348 --admin --squash --delete-branch
gh pr merge 349 --admin --squash --delete-branch
gh pr merge 350 --admin --squash --delete-branch
gh pr merge 353 --admin --squash --delete-branch
```

**Drew also expanded the second flag's scope:** beyond `CharacterAssetGen.cs:51-52` ("License is CC-style generated content"), he found **`:50` wrong-hero and stale siblings at `:197-202` / `:223-224` contradicting `:217=true`**. Priya's ticket was briefed with the narrower scope; I will comment the expansion onto it rather than interrupt her mid-round.

### ✅ Priya: 2 tickets filed (2026-07-30 ~05:2xZ) — and she caught my FIFTH wrong premise
- **`86cayp1vb`** — commit castaway v4's provenance artifacts. S, art-src lane, **READY** → **dispatched to Devon.**
- **`86cayp1w2`** — retire the vague "CC-style" licence claim in `CharacterAssetGen.cs:50-52`. S, code lane, **GATED on #356 merging** (the text to quote lands with it).

### 🔴 MY FIFTH WRONG PREMISE — and it violated a rule I wrote into the docs the same night
I suggested ticket 2's replacement wording should be *"Hyper3D Rodin generated content plus Mixamo clips."* **That kills the CC-BY error and reproduces the WRONG-HERO error.** Per #356's own AC2 table (`CharacterAssetGen.cs:181-184`), v4's **mesh AND texture are in-house Blender work**; only rig + clips are Mixamo. v4 is not a Rodin generation — that is v1/v2/v3's route.

**This is the exact failure mode I captured in `character-pipeline.md` hours earlier:** *"a licence claim must never be carried over from a sibling asset."* I wrote the rule, then carried v2's mechanism onto v4 in the very next brief. Priya caught it and encoded a named 🔒 constraint telling the implementer **not** to adopt my wording and to derive it from the file instead. **Five wrong premises tonight, five caught by a persona verifying rather than complying.**

### Flag 1 — VERIFIED three ways, with her refinement being the accurate framing
v4's **runtime** files ARE tracked (`castaway_v4_rigged.fbx` + palette), so builds are fine. What is absent is the **`art-src/` SOURCE layer**. So the exposure is precisely *"cannot re-author the hero"*, not *"cannot ship"*. Asymmetry holds three-for-three on the same code idiom: `:114` v2 → tracked + README · `:147` v3 → tracked + README · `:201` v4 → nothing. **None of the five paths is gitignored** (`git check-ignore -v`), so no `.gitignore` change is needed. She measured the render dir at **16 MB** vs **~890 KB** for everything else and made it an explicit tunable.

**My scope call on the dispatch:** commit the small load-bearing artifacts (`.blend`, README, palette source) — those alone close the exposure — and **leave the 16 MB render dir out unless the ACs require it**, flagging the size for you. Repo growth of that order is your decision, not mine; the data-loss fix should not wait on it.

**Her open question, my answer:** `art-src/castaway-v3-rodin-export/` (non-lowpoly v3) is also absent. Since v3 already has its lowpoly `.blend` + export dir tracked, this is preservation-of-an-intermediate rather than an exposure — I folded it into `86cayp1vb` as **explicitly optional**, not a separate ticket, so it cannot block.

**Drew's scope expansion relayed** to `86cayp1w2` as a comment rather than by interrupting her: beyond `:50-52`, he found `:197-202` and `:223-224` also contradicting `:217=true`.

### ⚠ A correction I made to my own ClickUp comment
I stamped that comment *"~02:2xZ"* when the real time was **05:22Z** — my mental clock was ~3 h stale because Priya's round ran **3h20m**. Corrected on-thread. Same failure class as the ticket itself: **a remembered value substituted for a checked one.** Noting it because it is the cheapest possible demonstration of why the rule matters.

**Session note:** the away run is now ~9 hours old (armed 20:31Z). Everything remaining is gated on you — **seven staged merges (#352 first), two soaks, the v4-source exposure, and the tag-write blocker.**
