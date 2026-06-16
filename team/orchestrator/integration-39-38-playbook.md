# Integration playbook — #39 (castaway/axe/hair) + #38 (sea/debris)

**Status:** READY. Validated by the `integration-readiness-39-38` workflow (run `wf_d63e952a-804`, 2026-06-13). Verdict: **clean-integration-expected.** Gated on the Sponsor's #39 soak APPROVE before executing.

**Branches:** base `origin/devon/castaway-axe-soakfix2` (#39, HEAD `5f4d6cc`) ← merge `origin/drew/ocean-beach-soakfix2` (#38, HEAD `24bb62e`). main = `d9dbf62`.

## The conflict surface (only 3 files overlap)
- `Assets/Scripts/Editor/MovementCameraScene.cs` — **AUTO-MERGES CLEAN** (0 conflict markers, verified via `git merge-tree --write-tree`). Devon's craft/axe/hair edits and Drew's `BuildBeachDebris` land in disjoint methods >160 lines apart. Keep both sides.
- `Assets/Scenes/Boot.unity` — **binary, REGENERATE** (never text-merge). Take `--theirs/--ours` provisionally, then re-bake via bootstrap.
- `Assets/Resources/BuildStamp.txt` — **regenerate** (bootstrap's `WriteBuildStamp` rewrites it to HEAD). Take `--ours` provisionally.
- Everything else (LowPolyZoneGen.cs Drew-only; StumpAxe.cs / HairVerifyCapture.cs Devon-only; atlas PNG; all tests/metas) is non-overlapping → carries clean.

## Steps
0. **Pre-flight serialize (PackageCache EPERM):** no in-flight CI on either branch (`gh run list --branch ...`), and let any local Unity/exe fully EXIT first. (`git fetch origin` already current.)
1. **Integration branch off Devon** (the larger changeset) in the MAIN worktree (`c:/Trunk/PRIVATE/Far-Horizon`, NOT a persona worktree): `git checkout -b integ/castaway-axe-ocean-beach origin/devon/castaway-axe-soakfix2`
2. **Merge Drew:** `git merge --no-ff origin/drew/ocean-beach-soakfix2` → expect exactly 3 conflicting paths (the two binaries + Boot.unity; MovementCameraScene.cs auto-resolves).
3. **Code conflict (MovementCameraScene.cs):** git's auto-merge is correct (both feature sets present). Verify `git grep -c -nE '^(<{7}|={7}|>{7})' -- Assets/Scripts/Editor/MovementCameraScene.cs` returns 0. Dead-code scan (PR #31 MakeAxeMat trap — low risk, both off recent main): drop anything now orphaned. `git add` it.
4. **Binaries PROVISIONAL:** `git checkout --theirs Assets/Scenes/Boot.unity Assets/Resources/BuildStamp.txt && git add` them. (Overwritten in step 6.)
5. **Commit the merge** (`git commit --no-edit`). Tree now has BOTH features' CODE but Boot.unity carries only one branch's bake — the regenerate in step 6 is **MANDATORY**.
6. **REGENERATE once via `serve_soak.sh`** (the canonical entry): runs `BootstrapProject.Run` (re-bakes Boot.unity from the merged code → BOTH features land; rewrites BuildStamp to HEAD) → `BuildWindows` → `verify_build_stamp.py` (fails unless shipped stamp sha == HEAD) → capture_gate. **Never a bare BuildWindows** (won't regenerate scene/stamp).
7. **Surgical re-add:** `git checkout -- Assets/ ProjectSettings/` (drop bootstrap's unrelated asset churn) then `git add Assets/Scenes/Boot.unity Assets/Resources/BuildStamp.txt` (+ atlas PNG if re-baked). Commit.
8. **Prove the COMBINED carry — both features' scene-presence tests GREEN TOGETHER** (the half-baked-scene gate; binary scenes can't be GUID-grepped):
   - Devon: `HeroAxeSceneTests` (HeldAxe big-enough-in-gameplay + StumpAxe visible-from-spawn/inverse-gated), `CastawayCharacterTests.MessyHairCap_CrownIsFlat_NoProudApexSpike`, `CaptureGateSceneTests.BootScene_CarriesHairVerifyCapture_Serialized`.
   - Drew: `WaterFacesUpTests` (every Water_Play normal·+Y>0 — **re-run against the regenerated scene**), `BeachDebrisSceneTests` (root, 3–10 pieces, ZERO colliders), `WaterSceneTests` color-pins.
   - PlayMode: `CraftToVisibleAxePlayModeTests`, `StumpAxePlayModeTests`, `BeachDebrisPlayModeTests`.
9. **Push** (serialized) `integ/castaway-axe-ocean-beach`, CI green on the HEAD (cite the run id). Open the integration PR referencing #38 + #39; ClickUp status move same round.
10. **ONE fresh combined soak** from the regenerated build → Sponsor: exe path + HUD stamp `BUILD zoned | <UTC> | <integ-HEAD-sha>` + the checklist below. **Judge from a GAMEPLAY-ORBIT frame** showing held-axe/character AND ocean+debris TOGETHER (false-green-capture class — #39's axe passed `-verifyAxe` but was an invisible sliver in gameplay).

## Sponsor verification checklist (combined soak)
1. Held axe RENDERS in-hand at believable size (not a sliver) + HUD "axe 1".
2. Stump-axe planted/visible from spawn, swaps to held on craft (an axe always visible).
3. Shirt olive-khaki, separates from skin.
4. NO hair crown spike at tilt-to-horizon; messier hair.
5. Beach debris near spawn (no pathing block).
6. Ocean RENDERS as a teal sea (magenta-diff >0, ~55k px) — judge SATURATION (the open Sponsor call; Drew didn't re-tune).
7. Stamp inside FarHorizon_Data == integ HEAD (not the exe-stub mtime).

## Top risks (from the analysis)
- **SILENT-DROP #1:** `--theirs/--ours` on Boot.unity + stopping ships only ONE branch's scene. The regenerate (step 6) is mandatory; step-8 dual-feature tests are the gate.
- Re-run `WaterFacesUpTests` AFTER the regenerate (the whole #38 point was the water was invisible until the winding flipped).
- Stale-stamp/stale-exe: judge freshness by the baked stamp only.
- Runner EPERM: serialize the regenerate + CI push.
- False-green-capture: combined soak judged from the gameplay-orbit view, not isolated `-verify` shots.

_Full analysis: workflow run `wf_d63e952a-804` (s39/s38 surface + conflict + playbook)._
