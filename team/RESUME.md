# Resume — Far Horizon

**Engine / repo / milestone (the one-line identity):** Far Horizon is a **Unity 6 (6000.4.10f1) / URP** desktop survival game, repo `TSandvaer/Far-Horizon` (`https://github.com/TSandvaer/Far-Horizon.git`), tracked on ClickUp list **Far Horizon** (`901523878268`). Active milestone: **M-U1 "Fresh Unity foundation + deliberate ports."** It succeeds the archived Godot project Embergrave/RandomGame (engine migration decided 2026-06-12).

**Current milestone:** M-U1 — 10 tickets (U1–U10) on list `901523878268`. The wave is **near-closed**:

- **Complete + merged:** U1 (Unity 6/URP skeleton, root commit `3a6ef5c`), U2 (orchestration scaffold, `bfe1328`), U4 (CI — GitHub Actions structure gate + EditMode/PlayMode/build, PR #2 `aedcad4`), U9 (survival-roadmap.md, PR #1 `3df80d5`), U3 (PoE click-to-move + orbit camera on NavMesh, PR #3 `99555c9`), U5 (Zone-D look as production environment, PR #4 `cec316d`), U6 (CC0 castaway avatar on the U3 movement rig, PR #5 `28d9de7`).
- **In QA:** U7 (testing-bar translation — EditMode + PlayMode + build-capture gate, PR #6, `ready for qa test`).
- **Open:** U8 (build-capture + soak-serve ritual for desktop builds, `to do`); U10 (this ticket — refresh RESUME.md + team docs, `in progress`).
- **Open bug tickets (filed mid-wave, `to do`):** `fix(build)` FarHorizonBuilder.BuildWindows hardcodes Boot.unity (should respect `EditorBuildSettings.scenes`); `fix(world)` pale-shore spawn framing (first gameplay frame reads pale).

**First Sponsor soak served** off `28d9de7` (U6 castaway-on-rig) today (2026-06-12) — the first interactive look at the ported foundation.

**What's next:** M-U1 closes when U7 clears QA and U8 + U10 land (plus the two `fix(...)` tickets). **M-U2 (the thin survival loop — ONE need → craft axe → chop → campfire, Sponsor-locked thin) files when M-U1 closes.**

**Live state:** `team/STATE.md` — read the "RESUME NEXT-ACTION" header FIRST; it is the orchestrator's point-in-time playbook and supersedes this doc for the exact next dispatch.

**Where things came from (Godot-era archive — referenceable, never resumed):** Far Horizon succeeds **Embergrave/RandomGame**, archived at `c:/Trunk/PRIVATE/RandomGame` (full repo history, the Godot-era `team/DECISIONS.md`, the `.claude/docs` Godot doc set, and the ClickUp list "RandomGame"). Cite it for history; never resume development there. The engine decision + all founding Sponsor decisions are dated **2026-06-12** — see `team/DECISIONS.md` here AND the archive's `team/DECISIONS.md` (Godot-era). The Unity eval spike (`c:/Trunk/PRIVATE/EmbergraveUnitySlice`, with its `FINDINGS.txt`) is the **read-only** working reference for the M-U1 ports — never modify it.

**Conventions to read on resume:** `CLAUDE.md` (hard rules) + `team/TESTING_BAR.md` (Unity testing bar) + `team/GIT_PROTOCOL.md` + `team/ROLES.md` + `.claude/docs/unity-conventions.md` + `.claude/docs/art-direction.md`.
