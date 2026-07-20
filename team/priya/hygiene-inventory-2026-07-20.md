# Hygiene inventory — 2026-07-20 (read-only; NO deletions performed)

**Author:** Priya (PL). **Scope:** inventory + sweep/keep recommendation for (a) superseded soak
builds, (b) the stranded dirty state in `Far-Horizon-drew-wt`, (c) untracked
`BuildMenuPanelSettings.asset` occurrences. **This doc performs zero deletions and zero git
mutations to any worktree.** Every deletion below is a *staged recommendation* — a destructive
action, so it is never auto-executed; the Sponsor/orchestrator runs it (or declines) after review.

All evidence in this doc was gathered from live tool output on 2026-07-20 (`ls`/`find`/`du`/`git`
in the named trees). Where a number is an estimate rather than a measured value, it is labelled.

---

## (a) Superseded soak builds under `Build/` (main project tree)

**Location:** `c:/Trunk/PRIVATE/Far-Horizon/Build/`
**Git status:** `Build` is **git-ignored** (`git check-ignore Build` → `Build`). Nothing here is
tracked; a sweep is a **local-disk operation only — no git, no PR**.

**Count:** 94 build subdirs (`ls -1d Build/*/ | wc -l` → 94), spanning `soak-pr83` (Jun 19)
through `soak-v4-dial-8` (Jul 20 09:30). Each full build is a complete Windows player
(`FarHorizon_Data/`, `D3D12/`, `MonoBleedingEdge/`, `*_BurstDebugInformation_DoNotShip/`).

**Footprint:** representative single build `Build/soak-forge` = **115 MB** (measured). A full-tree
`du -sh Build` **timed out at 2 min** (too many builds to walk) — so the total is an *estimate*:
~94 dirs × ~115 MB ≈ **on the order of ~8–11 GB** (caps-only dirs like `soak-254-caps`,
`sky194-caps-check`, `caps-186` are smaller; full-player dirs dominate). Treat the total as
approximate — it was not measured directly.

### Keep-list (do NOT sweep) — the harvested Sponsor dial-session logs

The dial-era soak builds themselves are superseded, but the **harvested `sponsor-dial*Player*.log`
files inside them are the durable evidence** of the v4 hand-dial sessions (rounds 2/6/7). Six files
(`find Build -iname 'sponsor-dial*Player*.log'`):

| Path | Note |
|---|---|
| `Build/soak-v4-dial-2/sponsor-dial-session-Player.log` | dial-2 session |
| `Build/soak-v4-dial-2/sponsor-dial-session-Player-prev.log` | dial-2 prev run |
| `Build/soak-v4-dial-6/sponsor-dial6-Player.log` | dial-6 session |
| `Build/soak-v4-dial-6/sponsor-dial6-Player-prev.log` | dial-6 prev run |
| `Build/soak-v4-dial-6/sponsor-dial6-run2-Player.log` | dial-6 run 2 |
| `Build/soak-v4-dial-7/sponsor-dial7-Player.log` | dial-7 session |

**Recommendation (staged, not executed):**
1. **Preserve the 6 logs FIRST** — copy them to a durable, non-ignored evidence location (e.g.
   `team/priya/evidence/v4-dial-logs/`) before any Build/ sweep, so the dial evidence survives.
   These are small text logs; committing them is cheap institutional memory.
2. **Then the Build/ payloads are sweepable** — all 94 dirs are git-ignored throwaway soak builds
   whose code is on `main`. Suggested conservative sweep: remove all `Build/*` EXCEPT keep the
   **latest 1–2 v4 dial builds** (`soak-v4-dial-8`, and `soak-v4-dial-7` for its log) until the v4
   activation soak (#317) is fully closed by the Sponsor. This is a Sponsor/orchestrator disk-cleanup
   action, not a Priya action — **deletion is destructive and never auto-decided.**

---

## (b) Stranded dirty state in `Far-Horizon-drew-wt`

**Worktree:** `c:/Trunk/PRIVATE/Far-Horizon-drew-wt`
**Branch:** `drew/86catvb6u-v4-activation`, HEAD `793862c` ("...86catvb6u round-9").
**No checkout / reset / clean was performed — status read only (`git status --porcelain`).**

### Merge-status ground truth (the brief called this "the merged branch" — CONFIRMED)

- PR **#317** ("feat(character): activate castaway v4 as default hero + 15-seat F9 dial wiring
  (86catvb6u)") is **MERGED** — `mergedAt` `2026-07-20T08:56:06Z`; squash commit **`af6f93c`** is on
  `origin/main`.
- The branch HEAD `793862c` is **NOT an ancestor of `origin/main`**
  (`git merge-base --is-ancestor` → NO). **This is expected for a squash-merge** — the branch's own
  commits are not replayed onto main; the squashed equivalent (`af6f93c`) is. So the branch is
  correctly "merged" and its substantive v4 work is already on `main`.

### The dirty working tree — 52 modified + 3 untracked

`git status --porcelain` count by type: **52 ` M` (modified) + 3 `??` (untracked).**

**Classification of the 52 modified files → WORTHLESS post-merge re-serialization churn.** They are
all Unity serialized-asset re-writes triggered by opening the project in the editor after the merge:
`.meta` bumps, `.mat`/`.mat.meta` (CastawayMat, ClickMarkerMat, GradientSky, LowPoly*, Pond*, Test*),
`.controller`/`.controller.meta` (CastawayAnimator), render/URP assets
(`FarHorizonRenderer`, `FarHorizonURP`, `ZoneD_PostProfile`,
`UniversalRenderPipelineGlobalSettings`), `ProjectSettings/*` (Graphics/Project/Quality/ShaderGraph),
`Assets/Resources/BuildStamp.txt`, the 15 `wpn_*_*.fbx.meta`, `Boot.unity`, etc. **The real v4 work
merged via #317; this is the editor re-emitting YAML on open. Nothing to salvage from the ` M` set.**

**The 3 untracked (`??`):**
- `Assets/Settings/BuildMenuPanelSettings.asset` + `.meta` — see (c) below (one item to VERIFY, not
  blindly sweep).
- `scratch_v4/` — scratch working dir; **worthless throwaway**, nothing to salvage.

**Recommendation (staged, not executed):** the branch is merged, the churn is worthless, the scratch
dir is throwaway. The clean way to retire this worktree is a Sponsor/orchestrator-run
`git checkout -- .` + remove-untracked (`scratch_v4/`) **only after** the PanelSettings question in
(c) is settled — but **do NOT `reset`/`clean` blindly** while that one untracked asset is
unresolved. Priya performs none of this (destructive + shared-worktree). Per
`[[capture-stranded-edits-via-worktree-apply-3way]]`, if any doubt remains, capture via a fresh
worktree + `apply --3way` rather than touching this dirty branch directly.

---

## (c) Untracked `BuildMenuPanelSettings.asset` occurrences

**Occurrences (across all worktrees, `find Far-Horizon Far-Horizon-*-wt -iname 'BuildMenuPanelSettings.asset*'`):**
only in **`Far-Horizon-drew-wt/Assets/Settings/BuildMenuPanelSettings.asset` (+ `.meta`)**. Not in
the main tree, not in any other worktree. Untracked (`??`), GUID `7b26f4fae4d21194f8f981aaac8122df`.

**Is it a salvage-critical missing asset, or regenerable churn?** Investigated:
- The **BuildMenu UI feature IS on `main`** — `Assets/Scripts/Runtime/BuildMenuUI.cs`,
  `BuildMenuVerifyCapture.cs`, `IBuildPlaceable.cs`, `CampfirePlacement.cs`/`CraftingTablePlacement.cs`/
  `ForgePlacement.cs`, plus `Assets/Tests/EditMode/BuildMenuTests.cs` + `BuildMenuSceneTests.cs`.
- The **name** `BuildMenuPanelSettings` is referenced on `main` by the editor script
  `Assets/Scripts/Editor/MovementCameraScene.cs` (a scene-setup/editor script — i.e. the PanelSettings
  is ensured/created programmatically by name).
- The specific untracked **GUID** `7b26f4fae4d21194f8f981aaac8122df` is **NOT referenced by any
  tracked file on `origin/main`** (`git grep <guid> origin/main` → no hits).

**Reading:** the build-menu feature is fully on main; the PanelSettings asset is referenced *by name*
from an editor script, and the drew-wt instance is a **locally-generated, non-referenced-by-GUID
artifact** — i.e. it regenerates when the build-menu scene/editor path runs, and no committed asset
on main points at this GUID. So it is **most likely regenerable worktree-local churn, NOT a
missing-from-main asset gap.**

**Recommendation (staged, not executed):** LOW-RISK-but-VERIFY. Before the (b) worktree cleanup,
have Devon/Drew confirm in one pass that `MovementCameraScene.cs`'s editor path *creates/ensures*
the PanelSettings at build/scene-setup time (rather than expecting a committed asset). If it
regenerates → the untracked instance is safe to drop with the rest of the churn (no salvage, no
new ticket). If instead the build-menu genuinely needs a *committed* PanelSettings that never landed
→ that is a real gap and gets its own small `chore` ticket to commit it properly. **Do not delete it
in this pass** — the verification is cheap and the mis-classification cost (a broken build menu) is
not.

---

## Summary — what to sweep, what to keep, what to verify

| Item | Verdict | Action owner |
|---|---|---|
| 94 `Build/*` soak-build payloads (git-ignored, ~8–11 GB est.) | **Sweep** (local disk; keep latest 1–2 dial builds until #317 soak closes) | Sponsor/orch (destructive — staged) |
| 6 `sponsor-dial*Player*.log` files | **KEEP** — preserve to a durable evidence path first | Priya can PR the copy if wanted |
| drew-wt 52 ` M` re-serialization files | **Worthless churn** — merged via #317; nothing to salvage | Sponsor/orch (worktree retire) |
| drew-wt `scratch_v4/` | **Worthless throwaway** | Sponsor/orch |
| drew-wt untracked `BuildMenuPanelSettings.asset` | **VERIFY first** (likely regenerable; low risk) — do not delete blind | Devon/Drew 1-pass check |

**No deletions or git mutations were performed by this inventory.** All destructive steps are staged
recommendations for the Sponsor/orchestrator.
