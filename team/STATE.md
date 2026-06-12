# Team State — Far Horizon

This file is the orchestrator's source of truth between ticks. The first section is always the live "Resume next-action" header — if a session dies, the next orchestrator starts there.

---

## RESUME NEXT-ACTION — 2026-06-12 (M-U1 bootstrap wave)

**If this session dies right now, the next orchestrator does this:** M-U1 is live on ClickUp list `901523878268` (10 tickets, U1–U10). U1 (Unity skeleton, Devon) is COMPLETE + verified (root commit `3a6ef5c` on main). U2 (this orchestration scaffold) lands as the second bootstrap commit — after it: enable branch-protection-by-convention (PR-flow from U3 on; `gh pr merge --admin --squash --delete-branch`), per-role worktrees exist at `../Far-Horizon-{priya,uma,devon,drew,tess}-wt`. NEXT DISPATCHES: U4 (CI, Devon) + U9 (survival-roadmap.md, Priya) can run parallel in their worktrees; U3 (click-move+camera port, Devon) follows U4; U5 (Zone-D look) + U6 (castaway) follow U3; U7 (testing bar, Tess+Devon) + U8 (soak ritual) close the wave; U10 (RandomGame archive touch) last. Sponsor still owes the iter8 soak verdict on the SPIKE build (`c:/Trunk/PRIVATE/EmbergraveUnitySlice/Build/Windows/EmbergraveUnitySlice.exe`, HUD `BUILD iter8`) — tracked on RandomGame ticket `86ca7zkyr`. Godot-era history: `c:/Trunk/PRIVATE/RandomGame/team/STATE.md` (archive).

---

## Role sections

(fresh — populated as the team works)
