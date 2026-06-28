# Board hygiene pass — 2026-06-28

Sponsor: "the ClickUp board is a mess." Full reconcile of list `901523878268` against main `e59977e`.

## Verdict
Board was in **better shape than reported** — the merged set (#147–156) was already at `complete` and there were **no duplicate tickets**. One genuine stale-status defect (the repurposed tree-chop ticket sitting at `in review` with no live impl PR) + four gated/in-flight tickets missing their gate markers/tags. All fixed.

## Reconciled

| Ticket | Was | Now | Action |
|--------|-----|-----|--------|
| `86caf9u5t` tree-chop rework | **`in review`** (stale — PR #157 CLOSED/rejected, ticket repurposed) | `to do` | Status reset to `to do`; prepended `sponsor-gate` marker (blocked on #158 merge — shared PickableLooter/Boot/SettingsCatalog); Sponsor-grilled spec left intact. |
| `86cafc6ud` #158 loot-proximity | `ready for qa test` (no gate marker) | `ready for qa test` + `needs-soak` marker | OPEN PR #158, CI green, Tess re-QA in flight. Marked "do not merge until Sponsor soak." |
| `86cafc6ty` #159 hunger-cap HUD | `ready for qa test` (no gate marker) | `ready for qa test` + `needs-soak` marker | OPEN PR #159, fully gated green, awaiting Sponsor soak only. |
| `86cafc6vx` pond water-acquisition | `to do` (no gate marker) | `to do` + `sponsor-gate` marker | Blocked on #158 merge (shared PickableLooter.cs). Branch off main after #158. |

## Verified clean (no change needed)
- **Merged set already `complete`:** #147 `86caf7a6q` E-loot, #148 `86caf7a0p` hold-chop, #152 `86caf7g6f` belt-elig, #153 `86caa96rd` sticks, #154 `86cadnepd` pond-guard, #155 `86caa4c96` stones, #156 `86caf7a30` left-click-consume. (#149/#150/#151 docs+tests likewise closed/complete.)
- **No duplicate tickets** on the open board (37 open: 35 `to do`, 2 `ready for qa test`, no `in progress`/`in review` stragglers after the one fix).
- **Deferred NITs correctly `to do`:** `86caf8dj1` (GIT_PROTOCOL doc), `86caf9ngh` (#148 N1), `86caf7ne0` (#148 chopInterval), `86caf6bjd` (#140 dead `+ 0f`), bush-perf `86cabnjv8`/`86cabuhyw`. Self-evidently scoped from title + linked PR — no OOS-boilerplate added (would be noise).
- The settings-registration cluster (`86cabn67w`/`86cabfa4e`/`86cabd75y`/`86cabe3e5`) each registers a DISTINCT tweakable set — NOT duplicates.

## Surfaced to orchestrator (out of my scope)
- **PR #160** (`docs(uma): felled-tree log-pile visual micro-spec`) is open & linked to `86caf9u5t` — a Uma docs micro-spec, not the impl. Consistent with `86caf9u5t` = `to do` (impl pending). Orchestrator gates #160 independently.
- **Tag application:** `update_task` MCP can't set ClickUp tags — I wrote the gate intent as `## GATE` description markers (machine-greppable). The literal `needs-soak`/`sponsor-gate` tags still need applying in the ClickUp UI/API.
- **`get_tasks` vs `get_task_details` discrepancy:** the `include_closed:false` list reported `86caf9u5t` as `to do` while `get_task_details` reported `in review` (the true state I corrected). Trust `get_task_details` for status reconcile.

No priority or scope changes made (OOS).
