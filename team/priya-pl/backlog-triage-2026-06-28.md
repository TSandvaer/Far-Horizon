# Backlog triage — Far Horizon (2026-06-28)

**Trigger:** Sponsor flagged the board is unwieldy (33 open `to do`) and wants it shaped so the orchestrator can keep the team fed with non-gated work.

**Board state (read 2026-06-28):** 35 open tickets total — `in progress` ×1, `ready for qa test` ×1, `to do` ×33. Status drift is minor (the #161 hygiene pass already reconciled); the real problem is the board is **flat** — 33 undifferentiated `to do` with no theme map and no clearly-tagged non-gated set. This doc supplies both.

**Diagnosis (honest):** the backlog is NOT full of dead tickets. Almost every `to do` is a traceable follow-up (a NIT from a merged PR's review, a deferred sub-AC, a Sponsor-authored vision prompt, or a CI/perf hardening item). It reads as unwieldy because it's un-grouped and un-prioritized, not because it's junk. Two tickets the brief flagged as possibly-stale (Snake, Juice) are real Sponsor vision items — **park, don't close** (see §Recommended dispositions).

---

## 1. Status reconciliation (vs PR/CI ground truth)

| Ticket | PR | Reality (verified) | Board status | Action |
|---|---|---|---|---|
| `86caf9u5t` tree-chop | #165 MERGED (main `18c8b74`) | merged | — | should be `complete` (orch merge-flip) |
| `86cafdevx` capture-hardening | #164 MERGED | merged | — | `complete` (orch flipped per STATE) |
| `86caf8dj1` GIT_PROTOCOL doc | #163 MERGED | merged | — | `complete` (orch flipped per STATE) |
| `86cafecuj` verifyChop CI gate | #166 OPEN, **unity job FAILURE**, no review | actively in-work, CI-red | `in progress` | **CORRECT — no change.** (Brief said "merge-ready"; ground truth = CI red + unreviewed. NOT merge-ready.) |
| `86cafc6vx` water spec→impl | #167 (uma docs spec) OPEN, no CI/review | impl ticket, sponsor-gated, ACs authored | `to do` + `sponsor-gate` | **CORRECT.** The spec is a sub-artifact; the impl ticket stays `to do` until dispatched. |
| `86caff4ad` fade-out NIT | #168 OPEN, **CI green + Drew APPROVE + Tess QA PASS** | merge-ready | `ready for qa test` | terminal pre-merge status; **merge-flip is orch-owned** (no soak — NIT). Stage for one-click merge. |

**Net:** no Priya-side status writes needed — the board is already reconciled. The only pending flips are the orch-owned merge flips (#165 → complete; #168 stage→merge→complete).

---

## 2. Themed map of the 33 `to do` (the prioritization surface)

### A. Chop NITs — mechanical follow-ups from the chop saga (3) — NON-GATED, dispatch-ready
- `86caf9ngh` — #148 hold-chop N1 re-press dead-window after fell + N2 floor swingClipLength fallback
- `86caf7ne0` — reconcile hold-to-chop `chopInterval` default (comment/PR say 0, field is 0.25f)
- `86caf6bjd` — drop dead `+ 0f` no-op in ChopTreePlayModeTests face-turn assert
> All three are Unity-build tickets (touch chop code/tests) → serialize on the 1 build slot + on each other (overlapping `ChopTree.cs`). Bundle into ONE chop-NITs PR.

### B. Dev-tweak console — Sponsor-directed unified soak-tuning panel (6) — partially gated
- `86cabeqj9` — console FOUNDATION (open-while-play + type-or-nudge on #83 infra) ← **the gate for the rest of this cluster**
- `86cabeqwf` — per-need on/off + decay-rate entries (warmth/hunger/thirst)
- `86caber95` — migrate F7/F8/F9/F10 handles into the console
- `86cabfa4e` — register belt/inventory/stack-size settings
- `86cabn67w` — register `berry regrowth time` setting
- `86cabd75y` — wire HUNGER tweakables into SettingsCatalog (86caamkp8 AC4 follow-up)
> `86cabd75y` and `86cabn67w` are standalone SettingsCatalog registrations (do NOT need the FOUNDATION). The other 4 sequence behind `86cabeqj9`. **Sponsor priority Q:** is the dev-tweak console a now-priority or a later-milestone item?

### C. Visual polish — Erik-research-first POCs (4) — SPONSOR-GATED (soak/greenlight)
- `86cabc73q` — nicer + more diverse low-poly trees (Erik deep-research first)
- `86cabc737` — grass POC (Erik research → POC → Sponsor soak before integrate)
- `86cabc743` — low-poly stylized sky w/ clouds + sunshine (Erik research)
- `86cacewju` — modeled chamfer-highlight bevel on hero props (DEFERRED by Sponsor; fold into the unified weapon/prop Blender re-author)
> All four open with an Erik research spike → POC → Sponsor soak. The RESEARCH halves are dispatchable to Erik NOW (non-build, parallel) without a Sponsor priority call; only the integration is gated. **High-leverage non-gated fill: dispatch Erik research.**

### D. World POC — the "big world / journey" north-star (2) — SPONSOR-GATED (greenlight)
- `86caa9zju` — POC: build a boat to sail from the start island to the next
- `86caa9zpp` — POC: generate a much-bigger random-shape next island
> Both are big-scope POCs tied to the M3 journey vision. Sponsor priority call to greenlight. Sequence: next-island before boat (boat needs a destination).

### E. Perf (2) — NON-GATED, dispatch-ready
- `86cabuhyw` — distance-cull `BerryBush.Update()` polling (deferred from #101 codereview)
- `86caammpq` — repo-wide CI concurrency-group fix (merged-branch runs orphan-hold the single runner)
> `86cabuhyw` is a Unity-build ticket. `86caammpq` is a CI/workflow edit (ci.yml — classifier-guarded; brief must authorize the ci.yml edit).

### F. Locomotion (3) — mixed
- `86caambxh` — airborne A/D nudge still slightly too speedy (Sponsor: "handle later") — sponsor-soak-gated (feel)
- `86caa3kur` — crouch on Ctrl-hold (sneak stance + movement) — sponsor-gated (feel-soak) + needs the preserved FBX clips (`86cackb3j`)
- `86cackb3j` — integrate the 11 preserved castaway locomotion + hit-react FBX clips into the animator — NON-GATED impl (clips already in-repo from #113); a prereq for crouch + future anim work

### G. Survival refactor / test-hardening (5) — NON-GATED, dispatch-ready
- `86cabgvgw` — `WarmthNeed` onto the `SurvivalNeed` base (all 3 needs one code path) — refactor
- `86cabugc3` — harden #102 drag-source-dim PlayMode coverage — test
- `86cabnjv8` — PR #101 bushes perf/hygiene NITs follow-up
- `86cabe3e5` — properly fix the #83 synthetic tweaked-frame capture sub-gate (currently QUARANTINED `86cabe3e5`)
- `86cabfa21` — get the advisory PlayMode job into a reliably-running CI slot (gates Inventory + Hunger PlayMode suites)
> `86cabgvgw`/`86cabugc3`/`86cabnjv8` are clean non-gated fill. `86cabe3e5` + `86cabfa21` touch the known PlayMode-env-deadlock surface (`86cab7u42` / `86cabkhqn` / `86cabkhjg`) — sequence them together, treat as a "CI/test-infra" sub-cluster.

### H. CI / build-slot spikes (3) — 2 SPONSOR-GATED, 1 design-linked
- `86cab7u42` — re-enable full BLOCKING PlayMode CI once the env-deadlock root cause is fixed — sponsor-gate (depends on the spikes)
- `86cabkhqn` — spike: shorten the Unity build hold-time — sponsor-gate (timeboxed; lever-pull is a Sponsor call)
- `86cabkhjg` — spike: concurrent-build cache-isolation (defeat PackageCache EPERM) — sponsor-gate
> The single-build-slot bottleneck is the team's structural throughput cap. These spikes are high-leverage but Sponsor-timeboxed. Worth a Sponsor priority Q.

### I. Design — grill-Sponsor-first (1) — SPONSOR-GATED (design input)
- `86cabcdpn` — Combat / HP / death system DESIGN (grill Sponsor first) — depends on the Snake POC introducing an enemy

### J. Vision prompts — Sponsor-authored Obsidian, un-scheduled (2) — PARK, don't close
- `86caaz4vn` — **Snake** (enemies POC, "natural enemies to make a survival game work")
- `86caaz4un` — **Game-development Juice concept** (Erik deep-dive to "elevate the game")
> Both are legitimate Sponsor vision items (verbatim Obsidian prompts), NOT stale junk. They have no schedule/greenlight yet. **Recommend: keep open, tag for a Sponsor priority call** — do NOT close.

### K. Misc cleanup (1) — NON-GATED, low-value
- `86cacer85` — point `LowPolyWaterMat.mat` at the `LowPolyWater` shader (moot-at-runtime/unused asset; "fold into the next water touch")

---

## 3. The dispatch-ready non-gated set (orchestrator can pull to fill idle slots — NO priority call needed)

These have **no hard dependency** and **need no Sponsor decision** — pure cleanup / refactor / test-hardening / mechanical NITs. The orchestrator can fill idle slots from this list freely:

**Unity-build lane (≤1 at a time — serialize):**
- `86caf9ngh` + `86caf7ne0` + `86caf6bjd` — chop NITs (bundle into ONE PR; they overlap `ChopTree.cs`)
- `86cabgvgw` — WarmthNeed → SurvivalNeed base refactor
- `86cabugc3` — drag-source-dim PlayMode test hardening
- `86cabnjv8` — bushes NITs follow-up
- `86cabuhyw` — BerryBush distance-cull perf
- `86cabd75y` + `86cabn67w` — standalone SettingsCatalog registrations (don't need the console FOUNDATION)
- `86cackb3j` — integrate preserved FBX locomotion clips (clips in-repo)

**Non-build lane (fan out in parallel — docs/research/CI):**
- Erik research spikes for cluster C: `86cabc73q` (trees), `86cabc737` (grass), `86cabc743` (sky) — the RESEARCH half is non-gated; integration is the gated half. **Highest-leverage idle-fill** — keeps Erik productive while the build slot is saturated.
- `86caammpq` — CI concurrency-group fix (⚠ ci.yml edit = classifier-guarded; the dispatch brief MUST authorize the ci.yml change).

**Sequencing note:** `86cabe3e5` + `86cabfa21` + `86cab7u42` are technically non-gated but all touch the PlayMode-env-deadlock surface — best done together as a deliberate CI-infra sub-wave, not as random slot-fill.

---

## 4. Recommended dispositions

- **Close as dupe/superseded:** none found. The board is clean of true duplicates — the NITs cluster around the same PRs but each is a distinct deliverable.
- **Park (keep open, Sponsor priority call):** `86caaz4vn` Snake, `86caaz4un` Juice, `86cabcdpn` Combat-design — the vision/design backlog. They are real, just un-scheduled.
- **Fold-into-future-work (don't dispatch standalone):** `86cacer85` (water-mat → next water touch), `86cacewju` (chamfer bevel → unified weapon/prop Blender re-author).
- **No status writes required** — the board is reconciled; only orch-owned merge flips (#165, #168) remain.

---

## 5. Priority questions for the Sponsor

1. **Visual-polish wave (cluster C):** greenlight the Erik research spikes (trees / grass / sky) to run now in the non-build lane? (Non-gated to START; you soak the POCs before any integration.)
2. **World POC (cluster D):** is the boat + next-island journey POC a now-priority or an M3 item? (Sequence: next-island → boat.)
3. **Dev-tweak console (cluster B):** now-priority, or later-milestone? (`86cabd75y`/`86cabn67w` can land as cheap fill regardless.)
4. **Build-slot spikes (cluster H):** worth timeboxing now? They're the structural throughput cap — every Unity ticket serializes behind the single slot.
5. **Vision backlog (cluster J/I):** Snake + Combat-design + Juice — confirm "park for later" vs schedule one.
