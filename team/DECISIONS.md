# Decision Log — Far Horizon

> **Append protocol (carried from RandomGame, adopted there 2026-05-15):** this file is centralized. Agents NEVER edit it directly — record `Decision draft:` lines in final reports; Priya batches them into a single PR. The orchestrator logs Sponsor-made and cross-role decisions directly.

Append-only. Format:

```
## YYYY-MM-DD — <short title>
- Decided by: <Sponsor | Priya | orchestrator>
- Decision: <one sentence>
- Why: <the load-bearing reason>
- Reversibility: <reversible | one-way>
- Affects: <roles or systems>
```

Godot-era decisions (2026-05-02 → 2026-06-12) live in the archived RandomGame repo: `c:/Trunk/PRIVATE/RandomGame/team/DECISIONS.md`.

---

## 2026-06-12 — Project founded: Far Horizon (Sponsor-directed)

- Decided by: Sponsor (sequence of popup decisions, recorded verbatim on RandomGame ClickUp ticket 86ca85ttd)
- Decision: The Unity production project is **Far Horizon** — a FRESH Unity 6/URP project (the eval spike stays a read-only reference, not graduated), new private GitHub repo `TSandvaer/Far-Horizon`, new ClickUp list `901523878268`; milestones split M-U1 (bootstrap + deliberate ports) / M-U2 (thin survival loop: ONE need → craft axe → chop → campfire); PixelLab subscription kept idle; the Godot repo/list archive read-only.
- Why: Engine decision 2026-06-12 (migrate to Unity — RandomGame DECISIONS.md; evidence: spike verdict YES on all capabilities + all style gates passed: character "appealing", "i love zone D + quality", "zone c approved"). Fresh-over-graduate and the name were the Sponsor's explicit picks.
- Reversibility: one-way in practice
- Affects: everything — repo, tracker, roadmap, all roles

## 2026-06-12 — Bootstrap exception: U1/U2 land direct on main

- Decided by: orchestrator
- Decision: The U1 Unity skeleton (root commit 3a6ef5c) and the U2 orchestration scaffold commit straight to `main`; PR-flow + protected-main discipline is binding from U3 onward.
- Why: An empty repo has no main to branch from and no reviewers' worktrees yet; both commits are recorded on their tickets (86ca86fb7 / 86ca86fgy) with full evidence.
- Reversibility: reversible (convention forward-looking)
- Affects: git protocol, all roles
